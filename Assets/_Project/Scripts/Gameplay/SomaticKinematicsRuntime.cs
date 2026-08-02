using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
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

namespace Hecton8.Core.Contracts.Signals
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct ShinobuPlayerExertionSignal : ISignal
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint SourceId;
        [FieldOffset(8)] public float StrokeMagnitude;
        [FieldOffset(12)] public float AgainstCurrent01;
        [FieldOffset(16)] public float Stamina01;
        [FieldOffset(20)] public byte Flags;
        [FieldOffset(21)] public byte Reserved0;
        [FieldOffset(22)] public ushort Reserved1;
        [FieldOffset(24)] public ulong Reserved2;
    }
}

namespace Hecton8.Gameplay
{
    [StructLayout(LayoutKind.Explicit, Size = 160)]
    public struct PlayerStateDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public double3 SectorOriginAUP;
        [FieldOffset(48)] public float3 Velocity;
        [FieldOffset(60)] public float3 LocalPosition;
        [FieldOffset(72)] public float3 RequestedThrust;
        [FieldOffset(84)] public float3 SdfPushOut;
        [FieldOffset(96)] public float3 AbyssalCurrent;
        [FieldOffset(108)] public float PlayerRadius;
        [FieldOffset(112)] public float Stamina01;
        [FieldOffset(116)] public float FatigueWindow;
        [FieldOffset(120)] public float SurfaceSubmersion01;
        [FieldOffset(124)] public float LostKineticEnergy;
        [FieldOffset(128)] public uint Frame;
        [FieldOffset(132)] public uint Flags;
        [FieldOffset(136)] public uint StableId;
        [FieldOffset(140)] public uint ShiftFrameId;
        [FieldOffset(144)] public ulong Padding0;
        [FieldOffset(152)] public ulong Padding1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 208)]
    public struct PlayerKinematicState
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public double3 SectorOriginAup;
        [FieldOffset(48)] public float3 LocalPosition;
        [FieldOffset(60)] public float3 Velocity;
        [FieldOffset(72)] public float3 RequestedThrust;
        [FieldOffset(84)] public float3 SdfPushOut;
        [FieldOffset(96)] public float3 AbyssalCurrent;
        [FieldOffset(108)] public float3 LastValidLocalPosition;
        [FieldOffset(120)] public float3 HeadForward;
        [FieldOffset(132)] public float3 ControllerForward;
        [FieldOffset(144)] public float PlayerRadius;
        [FieldOffset(148)] public float Stamina01;
        [FieldOffset(152)] public float FatigueWindow;
        [FieldOffset(156)] public float SurfaceSubmersion01;
        [FieldOffset(160)] public float LastPushOutMeters;
        [FieldOffset(164)] public float LastLostKineticEnergy;
        [FieldOffset(168)] public float LastAcousticMagnitude;
        [FieldOffset(172)] public float LastHapticMagnitude;
        [FieldOffset(176)] public uint Frame;
        [FieldOffset(180)] public uint Flags;
        [FieldOffset(184)] public uint StableId;
        [FieldOffset(188)] public uint ShiftFrameId;
        [FieldOffset(192)] public ulong Padding0;
        [FieldOffset(200)] public ulong Padding1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PlayerBoundingSphere
    {
        [FieldOffset(0)] public float3 CenterLocal;
        [FieldOffset(12)] public float Radius;
        [FieldOffset(16)] public float3 PreviousCenterLocal;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SomaticHandStrokeSample
    {
        [FieldOffset(0)] public float3 TargetLocal;
        [FieldOffset(12)] public float Timestamp;
        [FieldOffset(16)] public float3 RelativeToHead;
        [FieldOffset(28)] public float DeltaMeters;
        [FieldOffset(32)] public float3 PhysicalLocal;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] public byte HandIndex;
        [FieldOffset(49)] public byte HasTracking;
        [FieldOffset(50)] public ushort Reserved0;
        [FieldOffset(52)] public uint Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct SomaticKinematicsTuningData
    {
        [FieldOffset(0)] public float BaseDrag;
        [FieldOffset(4)] public float StrokeMultiplier;
        [FieldOffset(8)] public float SeaglideAcceleration;
        [FieldOffset(12)] public float SurfaceBuoyancy;
        [FieldOffset(16)] public float CurrentAcceleration;
        [FieldOffset(20)] public float CurrentFatigueScale;
        [FieldOffset(24)] public float SdfGradientEpsilon;
        [FieldOffset(28)] public float PlayerRadius;
        [FieldOffset(32)] public float SeaLevelY;
        [FieldOffset(36)] public float Gravity;
        [FieldOffset(40)] public float SurfaceBlendMeters;
        [FieldOffset(44)] public float ChestOffsetY;
        [FieldOffset(48)] public float StealthDeltaThreshold;
        [FieldOffset(52)] public float HapticPushThreshold;
        [FieldOffset(56)] public float MassKilograms;
        [FieldOffset(60)] public float GyroDamping;
        [FieldOffset(64)] public float MaxSpeed;
        [FieldOffset(68)] public int MaxCcdSteps;
        [FieldOffset(72)] public int DragLutCount;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] public ulong Padding0;
        [FieldOffset(88)] public ulong Padding1;

        public static SomaticKinematicsTuningData CreateEmergency()
        {
            SomaticKinematicsTuningData tuning = default;
            tuning.BaseDrag = 1.65f;
            tuning.StrokeMultiplier = 4.2f;
            tuning.SeaglideAcceleration = 7.5f;
            tuning.SurfaceBuoyancy = 11.0f;
            tuning.CurrentAcceleration = 1.35f;
            tuning.CurrentFatigueScale = 0.75f;
            tuning.SdfGradientEpsilon = 0.08f;
            tuning.PlayerRadius = 0.38f;
            tuning.SeaLevelY = SomaticKinematicsRuntime.DefaultSeaLevelY;
            tuning.Gravity = HectonPhysicsContract.GravityMetersPerSecondSquaredConst;
            tuning.SurfaceBlendMeters = 1.2f;
            tuning.ChestOffsetY = 0.45f;
            tuning.StealthDeltaThreshold = 0.035f;
            tuning.HapticPushThreshold = 0.045f;
            tuning.MassKilograms = 82.0f;
            tuning.GyroDamping = 7.0f;
            tuning.MaxSpeed = 10.0f;
            tuning.MaxCcdSteps = 8;
            tuning.DragLutCount = SomaticKinematicsRuntime.DragLutCapacity;
            tuning.Flags = 1u;
            return tuning;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct SomaticKinematicsFrameInput
    {
        [FieldOffset(0)] public float3 HeadLocalPosition;
        [FieldOffset(12)] public float DeltaTime;
        [FieldOffset(16)] public float3 HeadForward;
        [FieldOffset(28)] public float SeaglideInput01;
        [FieldOffset(32)] public float3 ControllerForward;
        [FieldOffset(44)] public float TimeSeconds;
        [FieldOffset(48)] public float3 LeftHandLocal;
        [FieldOffset(60)] public uint FrameIndex;
        [FieldOffset(64)] public float3 RightHandLocal;
        [FieldOffset(76)] public byte LeftTracked;
        [FieldOffset(77)] public byte RightTracked;
        [FieldOffset(78)] public byte SeaglideActive;
        [FieldOffset(79)] public byte QualityPressureQ8;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct SomaticKinematicsFrameContext
    {
        [FieldOffset(0)] public double3 SectorOriginAup;
        [FieldOffset(24)] public float3 AbyssalCurrent;
        [FieldOffset(36)] public float SystemStress01;
        [FieldOffset(40)] public byte QualityPressureQ8;
        [FieldOffset(41)] public byte Reserved0;
        [FieldOffset(42)] public ushort Reserved1;
        [FieldOffset(44)] public uint Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct SomaticKinematicSignalScratch
    {
        [FieldOffset(0)] public float3 PreviousLocalPosition;
        [FieldOffset(12)] public float ExertionDelta;
        [FieldOffset(16)] public float3 ResolvedLocalPosition;
        [FieldOffset(28)] public float AcousticMagnitude;
        [FieldOffset(32)] public float3 ResolvedVelocity;
        [FieldOffset(44)] public float HapticAmplitude;
        [FieldOffset(48)] public float3 SdfPushOut;
        [FieldOffset(60)] public float LostKineticEnergy;
        [FieldOffset(64)] public float AgainstCurrent01;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public uint Flags;
        [FieldOffset(76)] public byte HapticHandIndex;
        [FieldOffset(77)] public byte Reserved0;
        [FieldOffset(78)] public ushort Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct SomaticKinematicBlackBoxEntry
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float3 LocalPosition;
        [FieldOffset(36)] public float3 Velocity;
        [FieldOffset(48)] public float3 RequestedThrust;
        [FieldOffset(60)] public float3 SdfPushOut;
        [FieldOffset(72)] public uint Frame;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] public float AcousticMagnitude;
        [FieldOffset(84)] public float LostKineticEnergy;
        [FieldOffset(88)] public uint StateHash;
        [FieldOffset(92)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct MockSDFCollisionPlane
    {
        [FieldOffset(0)] public float HeightY;
        [FieldOffset(4)] public float SlopeX;
        [FieldOffset(8)] public float SlopeZ;
        [FieldOffset(12)] public float Padding0;
        [FieldOffset(16)] public float3 Padding1;
        [FieldOffset(28)] public uint Flags;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SampleDistance(float3 position)
        {
            return position.y - (HeightY + (position.x * SlopeX) + (position.z * SlopeZ));
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MockWorldSampler
    {
        [FieldOffset(0)] public MockSDFCollisionPlane Plane;
        [FieldOffset(32)] public float3 CaveCenter;
        [FieldOffset(44)] public float CaveRadius;
        [FieldOffset(48)] public float3 Padding0;
        [FieldOffset(60)] public uint Flags;

        public static MockWorldSampler Create(float3 center)
        {
            MockWorldSampler sampler = default;
            sampler.Plane.HeightY = -0.4f;
            sampler.Plane.SlopeX = 0.025f;
            sampler.Plane.SlopeZ = -0.015f;
            sampler.CaveCenter = center;
            sampler.CaveRadius = 240.0f;
            sampler.Flags = 1u;
            return sampler;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SampleDistance(float3 position)
        {
            float planeDistance = Plane.SampleDistance(position);
            float caveDistance = CaveRadius - FastLengthFromSq(math.lengthsq(position - CaveCenter), 0f);
            return math.min(planeDistance, caveDistance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float FastLengthFromSq(float lengthSq, float minLengthSq)
        {
            if (!math.isfinite(lengthSq))
                return 0f;

            float safeLengthSq = math.max(lengthSq, minLengthSq);
            return safeLengthSq > 0f ? safeLengthSq * math.rsqrt(safeLengthSq) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 SampleNormalTetra(float3 position, float epsilon)
        {
            float e = math.max(0.005f, epsilon);
            float3 k0 = new float3(1f, -1f, -1f);
            float3 k1 = new float3(-1f, -1f, 1f);
            float3 k2 = new float3(-1f, 1f, -1f);
            float3 k3 = new float3(1f, 1f, 1f);
            float3 gradient =
                (k0 * SampleDistance(position + (k0 * e))) +
                (k1 * SampleDistance(position + (k1 * e))) +
                (k2 * SampleDistance(position + (k2 * e))) +
                (k3 * SampleDistance(position + (k3 * e)));
            float lengthSq = math.lengthsq(gradient);
            return lengthSq > 0.0000001f ? gradient * math.rsqrt(lengthSq) : new float3(0f, 1f, 0f);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct MockFluidDensityLUT
    {
        [FieldOffset(0)] public float BaseDrag;
        [FieldOffset(4)] public float MediumSpeedDrag;
        [FieldOffset(8)] public float HighSpeedDrag;
        [FieldOffset(12)] public float MaxSpeedSq;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(float speedSq)
        {
            float t = math.saturate(speedSq * math.rcp(math.max(0.25f, MaxSpeedSq)));
            float eased = t * t * (3.0f - (2.0f * t));
            return math.lerp(BaseDrag, math.lerp(MediumSpeedDrag, HighSpeedDrag, t), eased);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct SomaticKinematicsJob : IJob
    {
        [NoAlias] public NativeArray<PlayerKinematicState> State;
        [NoAlias] public NativeArray<PlayerBoundingSphere> BoundingSphere;
        [NoAlias] public NativeArray<SomaticHandStrokeSample> HandHistory;
        [NoAlias, ReadOnly] public NativeArray<float> DragLut;
        [NoAlias, ReadOnly] public NativeArray<SomaticKinematicsTuningData> Tuning;
        [NoAlias] public NativeArray<SomaticKinematicSignalScratch> SignalScratch;
        [NoAlias] public NativeArray<SomaticKinematicBlackBoxEntry> BlackBox;
        [NoAlias] public NativeArray<int> BlackBoxCursor;
        public SomaticKinematicsFrameInput Input;
        public SomaticKinematicsFrameContext Context;
        public MockWorldSampler WorldSampler;

        public void Execute()
        {
            if (!State.IsCreated || State.Length == 0 || !BoundingSphere.IsCreated || BoundingSphere.Length == 0)
                return;

            SomaticKinematicsTuningData tuning = Tuning.IsCreated && Tuning.Length > 0
                ? Tuning[0]
                : SomaticKinematicsTuningData.CreateEmergency();
            SanitizeTuning(ref tuning);
            PlayerKinematicState state = State[0];
            SomaticKinematicsFrameInput input = SanitizeFrameInput(Input, state.LastValidLocalPosition);
            Input = input;
            PlayerBoundingSphere sphere = BoundingSphere[0];
            float dt = math.clamp(input.DeltaTime, 0.001f, 0.05f);
            float radius = SanitizePositive(math.select(state.PlayerRadius, tuning.PlayerRadius, state.PlayerRadius <= 0.01f), 0.38f);

            double3 sector = Context.SectorOriginAup;
            state.SectorOriginAup = sector;
            float3 local = AupPrecisionMath.LocalDeltaFloat3Clamped(
                state.Aup,
                sector,
                AupPrecisionMath.DefaultMaxLocalCastMeters,
                float3.zero);
            if (!IsFinite(local))
                local = input.HeadLocalPosition;
            local = SnapMillimeter(local);

            float3 previousLocal = SanitizeFinite(sphere.CenterLocal, local);
            sphere.PreviousCenterLocal = previousLocal;
            sphere.CenterLocal = local;
            sphere.Radius = radius;

            float3 velocity = SanitizeFinite(state.Velocity, float3.zero);
            float3 headForward = NormalizeSafe(input.HeadForward, new float3(0f, 0f, 1f));
            float3 controllerForward = NormalizeSafe(input.ControllerForward, headForward);
            float stamina01 = math.saturate(math.select(state.Stamina01, 1.0f, state.Stamina01 <= 0.0f));

            UpdateHandHistory(in input, headForward);
            float againstCurrent01 = ResolveAgainstCurrent01(headForward, Context.AbyssalCurrent);
            float exertion = 0f;
            float3 requestedThrust = ResolveRequestedThrust(in input, headForward, controllerForward, stamina01, tuning, ref exertion);
            velocity += requestedThrust * dt;
            ApplyAbyssalCurrent(ref velocity, Context.AbyssalCurrent, dt, tuning);
            ApplyHydrodynamicDrag(ref velocity, dt, tuning);
            ApplySurfaceBuoyancy(ref velocity, local, dt, tuning, ref state);
            velocity = ClampSpeed(velocity, tuning.MaxSpeed);

            float3 sdfPushOut = float3.zero;
            float lostEnergy = 0f;
            byte hitHand = 255;
            IntegrateCcd(ref local, ref velocity, radius, dt, tuning, ref sdfPushOut, ref lostEnergy, ref hitHand);
            float safeExertion = math.isfinite(exertion) ? math.clamp(exertion, 0f, 8f) : 0f;
            float safeAgainstCurrent01 = math.saturate(againstCurrent01);

            bool invalid = !IsFinite(local) || !IsFinite(velocity) || !IsFinite(requestedThrust) || !IsFinite(sdfPushOut);
            if (invalid)
            {
                local = SanitizeFinite(state.LastValidLocalPosition, float3.zero);
                velocity = float3.zero;
                requestedThrust = float3.zero;
                sdfPushOut = float3.zero;
                safeExertion = 0f;
                safeAgainstCurrent01 = 0f;
                state.Flags |= SomaticKinematicsRuntime.StateFlagNonFinite;
            }
            else
            {
                state.LastValidLocalPosition = local;
            }

            double3 committedAup = sector + (double3)SnapMillimeter(local);
            state.Aup = committedAup;
            state.LocalPosition = local;
            state.Velocity = velocity;
            state.RequestedThrust = requestedThrust;
            state.SdfPushOut = sdfPushOut;
            state.AbyssalCurrent = Context.AbyssalCurrent;
            state.HeadForward = headForward;
            state.ControllerForward = controllerForward;
            state.PlayerRadius = radius;
            state.Stamina01 = stamina01;
            state.FatigueWindow += safeExertion * (1.0f + safeAgainstCurrent01 * tuning.CurrentFatigueScale);
            state.LastPushOutMeters = MockWorldSampler.FastLengthFromSq(math.lengthsq(sdfPushOut), 0f);
            state.LastLostKineticEnergy = lostEnergy;
            state.LastAcousticMagnitude = ResolveAcousticMagnitude(local, previousLocal, velocity, tuning);
            state.LastHapticMagnitude = ResolveHapticAmplitude(state.LastPushOutMeters, lostEnergy, tuning);
            state.Frame = input.FrameIndex;

            sphere.CenterLocal = local;
            BoundingSphere[0] = sphere;
            State[0] = state;
            WriteSignalScratch(previousLocal, local, velocity, requestedThrust, sdfPushOut, safeExertion, lostEnergy, safeAgainstCurrent01, hitHand, state);
            WriteBlackBox(in state);
        }

        private static SomaticKinematicsFrameInput SanitizeFrameInput(SomaticKinematicsFrameInput input, float3 fallbackLocal)
        {
            fallbackLocal = SanitizeFinite(fallbackLocal, float3.zero);
            input.HeadLocalPosition = SanitizeFinite(input.HeadLocalPosition, fallbackLocal);
            input.HeadForward = NormalizeSafe(input.HeadForward, new float3(0f, 0f, 1f));
            input.ControllerForward = NormalizeSafe(input.ControllerForward, input.HeadForward);
            bool leftFinite = IsFinite(input.LeftHandLocal);
            bool rightFinite = IsFinite(input.RightHandLocal);
            input.LeftHandLocal = leftFinite ? input.LeftHandLocal : input.HeadLocalPosition;
            input.RightHandLocal = rightFinite ? input.RightHandLocal : input.HeadLocalPosition;
            input.DeltaTime = math.isfinite(input.DeltaTime) ? math.clamp(input.DeltaTime, 0.001f, 0.05f) : 0.0166667f;
            input.TimeSeconds = math.isfinite(input.TimeSeconds) ? math.max(0f, input.TimeSeconds) : 0f;
            input.SeaglideInput01 = math.isfinite(input.SeaglideInput01) ? math.saturate(input.SeaglideInput01) : 0f;
            if (input.LeftTracked != 0 && !leftFinite)
                input.LeftTracked = 0;
            if (input.RightTracked != 0 && !rightFinite)
                input.RightTracked = 0;
            return input;
        }

        private void UpdateHandHistory(in SomaticKinematicsFrameInput input, float3 headForward)
        {
            if (!HandHistory.IsCreated || HandHistory.Length < SomaticKinematicsRuntime.HandHistoryCapacity)
                return;

            int slot = (int)(input.FrameIndex % 3u);
            WriteHandSample(slot, 0, input.LeftHandLocal, input.HeadLocalPosition, input.LeftTracked, input.TimeSeconds, input.FrameIndex);
            WriteHandSample(slot, 1, input.RightHandLocal, input.HeadLocalPosition, input.RightTracked, input.TimeSeconds, input.FrameIndex);
        }

        private void WriteHandSample(int slot, int hand, float3 handLocal, float3 headLocal, byte tracked, float timeSeconds, uint frame)
        {
            headLocal = SanitizeFinite(headLocal, float3.zero);
            bool handFinite = IsFinite(handLocal);
            handLocal = handFinite ? handLocal : headLocal;
            if (!handFinite)
                tracked = 0;

            int index = (hand * 3) + slot;
            SomaticHandStrokeSample sample = default;
            sample.TargetLocal = handLocal;
            sample.PhysicalLocal = handLocal;
            sample.RelativeToHead = handLocal - headLocal;
            sample.Timestamp = timeSeconds;
            sample.Frame = frame;
            sample.HandIndex = (byte)hand;
            sample.HasTracking = tracked;
            int previousSlot = (slot + 2) % 3;
            SomaticHandStrokeSample previous = HandHistory[(hand * 3) + previousSlot];
            if (tracked != 0 && previous.HasTracking != 0)
            {
                float3 delta = SanitizeFinite(sample.RelativeToHead - previous.RelativeToHead, float3.zero);
                float lengthSq = math.lengthsq(delta);
                sample.DeltaMeters = MockWorldSampler.FastLengthFromSq(lengthSq, 0f);
            }
            else
            {
                sample.DeltaMeters = 0f;
            }
            HandHistory[index] = sample;
        }

        private float3 ResolveRequestedThrust(
            in SomaticKinematicsFrameInput input,
            float3 headForward,
            float3 controllerForward,
            float stamina01,
            SomaticKinematicsTuningData tuning,
            ref float exertion)
        {
            float3 thrust = float3.zero;
            if (input.SeaglideActive != 0)
            {
                float input01 = math.saturate(input.SeaglideInput01);
                exertion = input01 * 0.05f;
                return controllerForward * (input01 * tuning.SeaglideAcceleration);
            }

            if (!HandHistory.IsCreated || HandHistory.Length < SomaticKinematicsRuntime.HandHistoryCapacity)
                return thrust;

            int slot = (int)(input.FrameIndex % 3u);
            int previousSlot = (slot + 2) % 3;
            thrust += ResolveHandStroke(0, slot, previousSlot, headForward, tuning, ref exertion);
            thrust += ResolveHandStroke(1, slot, previousSlot, headForward, tuning, ref exertion);
            float staminaMultiplier = stamina01 <= 0.001f ? 0.2f : 1.0f;
            return thrust * staminaMultiplier;
        }

        private float3 ResolveHandStroke(int hand, int slot, int previousSlot, float3 headForward, SomaticKinematicsTuningData tuning, ref float exertion)
        {
            SomaticHandStrokeSample current = HandHistory[(hand * 3) + slot];
            SomaticHandStrokeSample previous = HandHistory[(hand * 3) + previousSlot];
            if (current.HasTracking == 0 || previous.HasTracking == 0)
                return float3.zero;

            float3 delta = SanitizeFinite(current.RelativeToHead - previous.RelativeToHead, float3.zero);
            float backwardMeters = math.max(0f, -math.dot(delta, headForward));
            float lengthSq = math.lengthsq(delta);
            float deltaMeters = MockWorldSampler.FastLengthFromSq(lengthSq, 0f);
            exertion += math.min(deltaMeters, 4.0f);
            return headForward * (backwardMeters * tuning.StrokeMultiplier);
        }

        private void ApplyAbyssalCurrent(ref float3 velocity, float3 current, float dt, SomaticKinematicsTuningData tuning)
        {
            if (!IsFinite(current))
                return;

            float blend = math.saturate(tuning.CurrentAcceleration * dt);
            velocity += (current - velocity) * blend * 0.35f;
        }

        private void ApplyHydrodynamicDrag(ref float3 velocity, float dt, SomaticKinematicsTuningData tuning)
        {
            float speedSq = math.lengthsq(velocity);
            if (!math.isfinite(speedSq) || speedSq <= 0.000001f)
                return;

            float drag = SampleDrag(speedSq, tuning);
            velocity *= math.rcp(math.max(0.0001f, 1.0f + (drag * dt)));
        }

        private float SampleDrag(float speedSq, SomaticKinematicsTuningData tuning)
        {
            if (DragLut.IsCreated && DragLut.Length > 1)
            {
                int count = math.min(DragLut.Length, math.max(2, tuning.DragLutCount));
                float maxSpeedSq = math.max(1.0f, tuning.MaxSpeed * tuning.MaxSpeed);
                float scaled = math.saturate(speedSq * math.rcp(maxSpeedSq)) * (count - 1);
                int lo = (int)math.floor(scaled);
                int hi = math.min(lo + 1, count - 1);
                float t = scaled - lo;
                return math.max(0f, math.lerp(DragLut[lo], DragLut[hi], t));
            }

            MockFluidDensityLUT mock = default;
            mock.BaseDrag = tuning.BaseDrag;
            mock.MediumSpeedDrag = tuning.BaseDrag * 1.35f;
            mock.HighSpeedDrag = tuning.BaseDrag * 2.25f;
            mock.MaxSpeedSq = tuning.MaxSpeed * tuning.MaxSpeed;
            return mock.Evaluate(speedSq);
        }

        private void ApplySurfaceBuoyancy(ref float3 velocity, float3 local, float dt, SomaticKinematicsTuningData tuning, ref PlayerKinematicState state)
        {
            float blendDistance = math.max(0.1f, tuning.SurfaceBlendMeters);
            float invBlendDistance = math.rcp(blendDistance);
            float aboveSurface = local.y - tuning.SeaLevelY;
            float breach01 = math.saturate(aboveSurface * invBlendDistance);
            float chestDepth = tuning.SeaLevelY - (local.y + tuning.ChestOffsetY);
            float submerged01 = math.saturate(chestDepth * invBlendDistance);
            velocity.y -= tuning.Gravity * breach01 * dt;
            velocity.y += tuning.SurfaceBuoyancy * submerged01 * dt;
            state.SurfaceSubmersion01 = submerged01;
        }

        private void IntegrateCcd(
            ref float3 local,
            ref float3 velocity,
            float radius,
            float dt,
            SomaticKinematicsTuningData tuning,
            ref float3 sdfPushOut,
            ref float lostEnergy,
            ref byte hitHand)
        {
            float speedSq = math.lengthsq(velocity);
            float speed = MockWorldSampler.FastLengthFromSq(speedSq, 0f);
            int steps = math.max(1, (int)math.ceil(speed * math.rcp(math.max(0.05f, radius))));
            steps = math.min(steps, math.max(1, tuning.MaxCcdSteps));
            float quality01 = math.saturate(1f - math.saturate(Context.SystemStress01));
            float stepScale = math.lerp(0.25f, 1f, SmoothQuality01(quality01));
            steps = math.max(1, (int)math.ceil(steps * stepScale));

            float stepDt = dt * math.rcp(steps);
            for (int i = 0; i < steps; i++)
            {
                float3 candidate = local + (velocity * stepDt);
                float distance = WorldSampler.SampleDistance(candidate);
                if (distance < radius)
                {
                    float3 normal = WorldSampler.SampleNormalTetra(candidate, tuning.SdfGradientEpsilon);
                    float push = math.max(0f, radius - distance);
                    float3 pushVector = normal * push;
                    candidate += pushVector;
                    sdfPushOut += pushVector;
                    float intoWall = math.min(0f, math.dot(velocity, normal));
                    if (intoWall < 0f)
                    {
                        lostEnergy += 0.5f * math.max(1f, tuning.MassKilograms) * intoWall * intoWall;
                        velocity -= normal * intoWall;
                        hitHand = ResolveImpactHand();
                    }
                }

                local = candidate;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SmoothQuality01(float value)
        {
            float t = math.saturate(math.select(1f, value, math.isfinite(value)));
            return t * t * (3f - 2f * t);
        }

        private byte ResolveImpactHand()
        {
            if (!HandHistory.IsCreated || HandHistory.Length < SomaticKinematicsRuntime.HandHistoryCapacity)
                return 255;

            int slot = (int)(Input.FrameIndex % 3u);
            float left = HandHistory[slot].DeltaMeters;
            float right = HandHistory[3 + slot].DeltaMeters;
            return right > left ? (byte)1 : (byte)0;
        }

        private void WriteSignalScratch(
            float3 previousLocal,
            float3 resolvedLocal,
            float3 velocity,
            float3 requestedThrust,
            float3 sdfPushOut,
            float exertion,
            float lostEnergy,
            float againstCurrent01,
            byte hitHand,
            PlayerKinematicState state)
        {
            if (!SignalScratch.IsCreated || SignalScratch.Length == 0)
                return;

            SomaticKinematicSignalScratch scratch = default;
            scratch.PreviousLocalPosition = previousLocal;
            scratch.ResolvedLocalPosition = resolvedLocal;
            scratch.ResolvedVelocity = velocity;
            scratch.SdfPushOut = sdfPushOut;
            scratch.ExertionDelta = exertion;
            scratch.AcousticMagnitude = state.LastAcousticMagnitude;
            scratch.HapticAmplitude = state.LastHapticMagnitude;
            scratch.LostKineticEnergy = lostEnergy;
            scratch.AgainstCurrent01 = againstCurrent01;
            scratch.Frame = Input.FrameIndex;
            scratch.HapticHandIndex = hitHand;
            if (state.LastAcousticMagnitude > 0f)
                scratch.Flags |= SomaticKinematicsRuntime.SignalFlagAcoustic;
            if (state.LastHapticMagnitude > 0f)
                scratch.Flags |= SomaticKinematicsRuntime.SignalFlagHaptic;
            if ((state.Flags & SomaticKinematicsRuntime.StateFlagNonFinite) != 0u)
                scratch.Flags |= SomaticKinematicsRuntime.SignalFlagFault;
            SignalScratch[0] = scratch;
        }

        private void WriteBlackBox(in PlayerKinematicState state)
        {
            if (!BlackBox.IsCreated || BlackBox.Length == 0 || !BlackBoxCursor.IsCreated || BlackBoxCursor.Length == 0)
                return;

            int cursor = BlackBoxCursor[0];
            int index = PositiveModulo(cursor, BlackBox.Length);
            SomaticKinematicBlackBoxEntry entry = default;
            entry.Aup = state.Aup;
            entry.LocalPosition = state.LocalPosition;
            entry.Velocity = state.Velocity;
            entry.RequestedThrust = state.RequestedThrust;
            entry.SdfPushOut = state.SdfPushOut;
            entry.Frame = state.Frame;
            entry.Flags = state.Flags;
            entry.AcousticMagnitude = state.LastAcousticMagnitude;
            entry.LostKineticEnergy = state.LastLostKineticEnergy;
            entry.StateHash = HashState(in state);
            entry.Reserved = state.ShiftFrameId;
            BlackBox[index] = entry;
            BlackBoxCursor[0] = cursor + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PositiveModulo(int value, int length)
        {
            int modulo = value % length;
            return modulo < 0 ? modulo + length : modulo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashState(in PlayerKinematicState state)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, math.asuint(state.LocalPosition.x));
            hash = Mix(hash, math.asuint(state.LocalPosition.y));
            hash = Mix(hash, math.asuint(state.LocalPosition.z));
            hash = Mix(hash, math.asuint(state.Velocity.x));
            hash = Mix(hash, math.asuint(state.Velocity.y));
            hash = Mix(hash, math.asuint(state.Velocity.z));
            hash = Mix(hash, state.Frame);
            hash = Mix(hash, state.Flags);
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveAgainstCurrent01(float3 headForward, float3 current)
        {
            float currentSq = math.lengthsq(current);
            if (currentSq <= 0.0001f || !math.isfinite(currentSq))
                return 0f;
            float3 currentDir = current * math.rsqrt(currentSq);
            return math.saturate(-math.dot(headForward, currentDir));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ClampSpeed(float3 velocity, float maxSpeed)
        {
            float speedSq = math.lengthsq(velocity);
            float limit = math.max(0.5f, maxSpeed);
            float limitSq = limit * limit;
            return speedSq > limitSq ? velocity * (limit * math.rsqrt(speedSq)) : velocity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveAcousticMagnitude(float3 local, float3 previousLocal, float3 velocity, SomaticKinematicsTuningData tuning)
        {
            float delta = MockWorldSampler.FastLengthFromSq(math.lengthsq(local - previousLocal), 0f);
            if (delta <= tuning.StealthDeltaThreshold)
                return 0f;

            float jerkLie = MockWorldSampler.FastLengthFromSq(math.lengthsq(velocity), 0f) * math.max(0f, delta - tuning.StealthDeltaThreshold);
            return math.saturate(jerkLie * 0.45f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveHapticAmplitude(float pushOut, float lostEnergy, SomaticKinematicsTuningData tuning)
        {
            if (pushOut <= tuning.HapticPushThreshold && lostEnergy <= 0.001f)
                return 0f;

            return math.saturate((pushOut * 7.5f) + (lostEnergy * 0.0025f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SnapMillimeter(float3 value)
        {
            return math.round(value * HectonPhysicsContract.DeterministicMillimeterScale) *
                   HectonPhysicsContract.DeterministicInvMillimeterScale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFinite(float3 value, float3 fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        private static void SanitizeTuning(ref SomaticKinematicsTuningData tuning)
        {
            SomaticKinematicsTuningData fallback = SomaticKinematicsTuningData.CreateEmergency();
            tuning.BaseDrag = SanitizeRange(tuning.BaseDrag, fallback.BaseDrag, 0.01f, 8.0f);
            tuning.StrokeMultiplier = SanitizeRange(tuning.StrokeMultiplier, fallback.StrokeMultiplier, 0.1f, 30.0f);
            tuning.SeaglideAcceleration = SanitizeRange(tuning.SeaglideAcceleration, fallback.SeaglideAcceleration, 0.1f, 40.0f);
            tuning.SurfaceBuoyancy = SanitizeRange(tuning.SurfaceBuoyancy, fallback.SurfaceBuoyancy, 0.1f, 40.0f);
            tuning.CurrentAcceleration = SanitizeRange(tuning.CurrentAcceleration, fallback.CurrentAcceleration, 0.0f, 10.0f);
            tuning.CurrentFatigueScale = SanitizeRange(tuning.CurrentFatigueScale, fallback.CurrentFatigueScale, 0.0f, 4.0f);
            tuning.SdfGradientEpsilon = SanitizeRange(tuning.SdfGradientEpsilon, fallback.SdfGradientEpsilon, 0.005f, 0.5f);
            tuning.PlayerRadius = SanitizeRange(tuning.PlayerRadius, fallback.PlayerRadius, 0.1f, 2.0f);
            tuning.SeaLevelY = SanitizeSeaLevelY(tuning.SeaLevelY, fallback.SeaLevelY);
            tuning.Gravity = SanitizeRange(tuning.Gravity, fallback.Gravity, 0.0f, 30.0f);
            tuning.SurfaceBlendMeters = SanitizeRange(tuning.SurfaceBlendMeters, fallback.SurfaceBlendMeters, 0.1f, 10.0f);
            tuning.ChestOffsetY = SanitizeRange(tuning.ChestOffsetY, fallback.ChestOffsetY, 0.0f, 2.0f);
            tuning.StealthDeltaThreshold = SanitizeRange(tuning.StealthDeltaThreshold, fallback.StealthDeltaThreshold, 0.0f, 2.0f);
            tuning.HapticPushThreshold = SanitizeRange(tuning.HapticPushThreshold, fallback.HapticPushThreshold, 0.0f, 2.0f);
            tuning.MassKilograms = SanitizeRange(tuning.MassKilograms, fallback.MassKilograms, 1.0f, 300.0f);
            tuning.GyroDamping = SanitizeRange(tuning.GyroDamping, fallback.GyroDamping, 0.0f, 30.0f);
            tuning.MaxSpeed = SanitizeRange(tuning.MaxSpeed, fallback.MaxSpeed, 0.5f, 80.0f);
            tuning.MaxCcdSteps = SanitizeRange(tuning.MaxCcdSteps, fallback.MaxCcdSteps, 1, 32);
            tuning.DragLutCount = SanitizeRange(tuning.DragLutCount, fallback.DragLutCount, 2, SomaticKinematicsRuntime.DragLutCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeRange(float value, float fallback, float min, float max)
        {
            float resolved = math.isfinite(value) ? value : fallback;
            return math.clamp(resolved, min, max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeSeaLevelY(float value, float fallback)
        {
            float resolved = math.isfinite(value) &&
                math.abs(value) > 0.0001f &&
                math.abs(value) <= Hecton8.World.WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY
                ? value
                : fallback;
            return math.clamp(resolved, -100000.0f, 100000.0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SanitizeRange(int value, int fallback, int min, int max)
        {
            int resolved = value > 0 ? value : fallback;
            return math.min(max, math.max(min, resolved));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return math.isfinite(lengthSq) && lengthSq > 0.000001f ? value * math.rsqrt(lengthSq) : fallback;
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Somatic Kinematics Runtime")]
    public sealed class SomaticKinematicsRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, ISlowTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001SomaticKinematicsRuntimeSignalPushDropCount;
        public const int BlackBoxCapacity = 300;
        public const int DragLutCapacity = 16;
        public const int HandHistoryCapacity = 6;
        public const int CsvScratchCapacity = 32768;
        public const float DefaultSeaLevelY = 14.02f;
        public const uint StateFlagNonFinite = 1u << 0;
        public const uint StateFlagQualityPressureReserved = 1u << 1;
        public const uint StateFlagSeaglide = 1u << 2;
        public const uint SignalFlagAcoustic = 1u << 0;
        public const uint SignalFlagHaptic = 1u << 1;
        public const uint SignalFlagFault = 1u << 2;

        private const uint AcousticKinematicSoundHash = 0x53484E4Fu;
        private const int ShinobuExertionSignalCapacity = 32;
        private const uint ShinobuExertionSignalLaneHash = 0x53484558u;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_1335_SomaticKinematics.h8dump";
        private const string CsvOverrideFileName = "kinematic_overrides.csv";
        private const SystemID VaultOwnerSystem = SystemID.GameplayPlayer;

        private struct LocalSimulationScratch
        {
            public NativeArray<PlayerKinematicState> State;
            public NativeArray<PlayerBoundingSphere> Sphere;
            public NativeArray<SomaticHandStrokeSample> HandHistory;
            public NativeArray<SomaticKinematicsTuningData> Tuning;
            public NativeArray<float> DragLut;
            public NativeArray<SomaticKinematicSignalScratch> SignalScratch;
            public NativeArray<SomaticKinematicBlackBoxEntry> BlackBox;
            public NativeArray<int> BlackBoxCursor;

            public bool IsReady()
            {
                return State.IsCreated &&
                       Sphere.IsCreated &&
                       HandHistory.IsCreated &&
                       Tuning.IsCreated &&
                       DragLut.IsCreated &&
                       SignalScratch.IsCreated &&
                       BlackBox.IsCreated &&
                       BlackBoxCursor.IsCreated;
            }

            public bool Ensure()
            {
                if (IsReady())
                    return true;

                try
                {
                    if (!State.IsCreated)
                        State = CreateNativeArray<PlayerKinematicState>(1, nameof(State));
                    if (!Sphere.IsCreated)
                        Sphere = CreateNativeArray<PlayerBoundingSphere>(1, nameof(Sphere));
                    if (!HandHistory.IsCreated)
                        HandHistory = CreateNativeArray<SomaticHandStrokeSample>(HandHistoryCapacity, nameof(HandHistory));
                    if (!Tuning.IsCreated)
                        Tuning = CreateNativeArray<SomaticKinematicsTuningData>(1, nameof(Tuning));
                    if (!DragLut.IsCreated)
                        DragLut = CreateNativeArray<float>(DragLutCapacity, nameof(DragLut));
                    if (!SignalScratch.IsCreated)
                        SignalScratch = CreateNativeArray<SomaticKinematicSignalScratch>(1, nameof(SignalScratch));
                    if (!BlackBox.IsCreated)
                        BlackBox = CreateNativeArray<SomaticKinematicBlackBoxEntry>(BlackBoxCapacity, nameof(BlackBox));
                    if (!BlackBoxCursor.IsCreated)
                        BlackBoxCursor = CreateNativeArray<int>(1, nameof(BlackBoxCursor));
                }
                catch
                {
                    Dispose();
                    throw;
                }

                return IsReady();
            }

            public void Dispose()
            {
                DisposeNativeArray(ref State);
                DisposeNativeArray(ref Sphere);
                DisposeNativeArray(ref HandHistory);
                DisposeNativeArray(ref Tuning);
                DisposeNativeArray(ref DragLut);
                DisposeNativeArray(ref SignalScratch);
                DisposeNativeArray(ref BlackBox);
                DisposeNativeArray(ref BlackBoxCursor);
            }

            private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
            {
                if (!array.IsCreated)
                    return;

                H8Memory.Release(ref array, VaultOwnerSystem);
            }

            private static NativeArray<T> CreateNativeArray<T>(int length, string label) where T : struct
            {
                NativeArray<T> array = H8Memory.Allocate<T>(length, VaultOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                if (!array.IsCreated)
                    throw new InvalidOperationException($"{nameof(SomaticKinematicsRuntime)} native allocation failed for {label}.");

                return array;
            }
        }

        private VaultGenerationHandle<PlayerKinematicState> _stateHandle;
        private VaultGenerationHandle<PlayerBoundingSphere> _sphereHandle;
        private VaultGenerationHandle<SomaticHandStrokeSample> _handHistoryHandle;
        private VaultGenerationHandle<SomaticKinematicsTuningData> _tuningHandle;
        private VaultGenerationHandle<float> _dragLutHandle;
        private VaultGenerationHandle<SomaticKinematicSignalScratch> _signalScratchHandle;
        private VaultGenerationHandle<SomaticKinematicBlackBoxEntry> _blackBoxHandle;
        private VaultGenerationHandle<int> _blackBoxCursorHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private LocalSimulationScratch _localScratch;
        private IDataVault _dataVault;
        private IDataVault _stateWriteVault;
        private IDataVault _sphereWriteVault;
        private IDataVault _handHistoryWriteVault;
        private IDataVault _tuningWriteVault;
        private IDataVault _dragLutWriteVault;
        private IDataVault _signalScratchWriteVault;
        private IDataVault _blackBoxWriteVault;
        private IDataVault _blackBoxCursorWriteVault;
        private IDataVault _csvScratchWriteVault;
        private IWeatherService _weatherService;
        private IVRSomaticProvider _somaticProvider;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private Transform _cachedTransform;
        private SomaticKinematicsFrameInput _frameInput;
        private SomaticKinematicsFrameContext _frameContext;
        private MockWorldSampler _mockWorldSampler;
        private float _cachedGlobalQualityWeight01 = 1f;
        private float _slowExertionAccumulator;
        private float _slowAgainstCurrentAccumulator;
        private uint _sourceId;
        private uint _fixedFrameSequence;
        private uint _kccVelocitySequence;
        private string _projectRoot;
        private string _csvOverridePath;
        private long _csvLastWriteTicks;
        private byte _seaglideActive;
        private float _seaglideInput01;
        private float3 _seaglideForward;
        private int _localBlackBoxWriteIndex;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredSlow;
        private bool _registeredOriginShift;
        private bool _registeredHotSwap;
        private bool _frameReadyForPostFixed;
        private bool _kinematicsJobScheduled;
        private JobHandle _pendingKinematicsHandle;
        private bool _dumpWritten;
        private bool _legacyScanAttempted;
        private PlayerKinematicState _stateRefFallback;
        private static bool s_signalLanesConfigured;

        private void Awake()
        {
            _cachedTransform = transform;
            _sourceId = Hecton8.Core.RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));
            EnsureSignalLanesReady();
            _cachedGlobalQualityWeight01 = ResolveGlobalQualityWeight01(_cachedGlobalQualityWeight01);
            RebindServices();
            ResolveColdPaths();
            PrepareNativeStateCold();
            EnsureLocalSimulationScratch();
        }

        private void OnEnable()
        {
            EnsureSignalLanesReady();
            RebindServices();
            PrepareNativeStateCold();
            EnsureLocalSimulationScratch();
#if UNITY_EDITOR
            TryApplyCsvOverrides();
#endif
            RegisterRuntime();
        }

        private void OnDisable()
        {
            CompleteScheduledKinematicsInPostFixedOrShutdown(true);
            UnregisterRuntime();
            ReleaseViews();
        }

        private void OnDestroy()
        {
            CompleteScheduledKinematicsInPostFixedOrShutdown(true);
            UnregisterRuntime();
            ReleaseViews();
        }

        public unsafe ref PlayerKinematicState GetStateRef()
        {
            if (TryReadStateBuffer(out NativeArray<PlayerKinematicState>.ReadOnly stateBuffer))
                _stateRefFallback = stateBuffer[0];
            else
                _stateRefFallback = default;

            return ref _stateRefFallback;
        }

        public void SetSeaglideState(bool active, float analog01, float3 controllerForward)
        {
            _seaglideActive = active ? (byte)1 : (byte)0;
            _seaglideInput01 = math.saturate(analog01);
            _seaglideForward = NormalizeSafe(controllerForward, new float3(0f, 0f, 1f));
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (_kinematicsJobScheduled || _frameReadyForPostFixed || !HasNativeStateReady())
                return;

            if (!_localScratch.IsReady() || !HydrateLocalSimulationScratchFromVault())
                return;

            RefreshLocalTuningSeaLevelFromOcean();
            BuildFrameInput(fixedDeltaTime, NextSequence(ref _fixedFrameSequence), _localScratch.State);
            SomaticKinematicsJob job = new SomaticKinematicsJob
            {
                State = _localScratch.State,
                BoundingSphere = _localScratch.Sphere,
                HandHistory = _localScratch.HandHistory,
                DragLut = _localScratch.DragLut,
                Tuning = _localScratch.Tuning,
                SignalScratch = _localScratch.SignalScratch,
                BlackBox = _localScratch.BlackBox,
                BlackBoxCursor = _localScratch.BlackBoxCursor,
                Input = _frameInput,
                Context = _frameContext,
                WorldSampler = _mockWorldSampler
            };
            _localBlackBoxWriteIndex = ResolveLocalBlackBoxWriteIndex();
            _pendingKinematicsHandle = job.Schedule();
            _kinematicsJobScheduled = true;
            _frameReadyForPostFixed = true;
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            if (!_frameReadyForPostFixed)
                return;

            if (CompleteScheduledKinematicsInPostFixedOrShutdown(true))
                PublishCompletedFrame();
        }

        public void SlowTick()
        {
            if (!HasNativeStateReady())
                return;

            PublishExertionSignal();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            float3 shiftOffset = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);
            float shiftSqrMagnitude = math.lengthsq(shiftOffset);
            if (!math.all(math.isfinite(shiftOffset)) ||
                !math.isfinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f ||
                !math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))
            {
                return;
            }

            CompleteScheduledKinematicsInPostFixedOrShutdown(true);
            if (!TryAcquireStateWriteBuffer(out NativeArray<PlayerKinematicState> stateBuffer))
                return;

            PlayerKinematicState state = default;
            try
            {
                state = stateBuffer[0];
                state.SectorOriginAup = shiftData.NewTotalOffsetDouble;
                state.LocalPosition = AupPrecisionMath.LocalDeltaFloat3Clamped(
                    state.Aup,
                    state.SectorOriginAup,
                    AupPrecisionMath.DefaultMaxLocalCastMeters,
                    state.LastValidLocalPosition);
                state.LocalPosition = SanitizeFinite(state.LocalPosition, state.LastValidLocalPosition);
                state.ShiftFrameId = shiftData.Sequence;
                state.Frame = shiftData.Frame >= 0 ? unchecked((uint)shiftData.Frame) : state.Frame;
                stateBuffer[0] = state;
            }
            finally
            {
                ReleaseStateWriteBuffer();
            }

            if (TryAcquireSphereWriteBuffer(out NativeArray<PlayerBoundingSphere> sphereBuffer))
            {
                try
                {
                    PlayerBoundingSphere sphere = sphereBuffer[0];
                    sphere.CenterLocal = state.LocalPosition;
                    sphere.PreviousCenterLocal = state.LocalPosition;
                    sphereBuffer[0] = sphere;
                }
                finally
                {
                    ReleaseSphereWriteBuffer();
                }
            }

            PublishOriginShiftFence(in state, in shiftData);
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                BindDataVault(currentService as IDataVault, previousService as IDataVault);
                PrepareNativeStateCold();
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Weather ||
                serviceSlot == GlobalRegistryServiceSlot.VRSomaticProvider)
            {
                RebindServices();
            }

            if (serviceSlot == GlobalRegistryServiceSlot.OceanKinematics)
                _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
        }

        private void PublishOriginShiftFence(in PlayerKinematicState state, in OriginShiftEventData shiftData)
        {
            SyncFenceSignal fence = default;
            fence.PositionAup = Hecton8.World.AbsoluteUniversePosition.FromAbsolutePosition(state.Aup);
            fence.RuntimePosition = state.LocalPosition;
            fence.Velocity = SanitizeFinite(state.Velocity, float3.zero);
            fence.Rotation = quaternion.identity;
            fence.StateHash = state.StableId ^ state.Frame ^ state.ShiftFrameId;
            fence.Frame = shiftData.Frame >= 0 ? unchecked((uint)shiftData.Frame) : state.Frame;
            fence.SourceId = _sourceId;
            fence.Sequence = shiftData.Sequence;
            fence.Flags = shiftData.IsSafeTeleport != 0 ? (byte)1 : (byte)0;
            SignalBus<SyncFenceSignal>.TryPushTracked(in fence, ref s_x001SomaticKinematicsRuntimeSignalPushDropCount);
        }

        internal static void EnsureOnPlayerRoot(GameObject playerRoot)
        {
            if (playerRoot == null)
                return;

            if (!playerRoot.TryGetComponent(out SomaticKinematicsRuntime _))
            {
                // Player-build construction path: no authored/bootstrap instance reachable.
                // Must construct in player builds when bootstrap reorders or skips registration.
                playerRoot.AddComponent<SomaticKinematicsRuntime>(); // COLD ALLOC: SomaticKinematicsRuntime[1] - SHINOBU math KCC bridge attached to player root - owner: SHINOBU_06
            }
        }

        private void RegisterRuntime()
        {
            if (!_registeredFixed)
                _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);
            if (!_registeredPostFixed)
                _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Player);
            if (!_registeredSlow)
                _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
            if (!_registeredOriginShift)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShift = true;
            }
            if (!_registeredHotSwap)
            {
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
            }
        }

        private void UnregisterRuntime()
        {
            if (_registeredFixed)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
                _registeredFixed = false;
            }
            if (_registeredPostFixed)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Player);
                _registeredPostFixed = false;
            }
            if (_registeredSlow)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                _registeredSlow = false;
            }
            if (_registeredOriginShift)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShift = false;
            }
            if (_registeredHotSwap)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }

            _oceanKinematicsService = null;
        }

        private static void EnsureSignalLanesReady()
        {
            if (s_signalLanesConfigured)
                return;

            SignalBus<ShinobuPlayerExertionSignal>.Configure(
                ShinobuExertionSignalCapacity,
                maxFrameSignals: ShinobuExertionSignalCapacity,
                lowTierFrameSignals: 8,
                laneHash: ShinobuExertionSignalLaneHash);
            SignalBus<ShinobuPlayerExertionSignal>.EnsureInitialized();
            s_signalLanesConfigured = true;
        }

        private bool HasNativeStateReady()
        {
            IDataVault vault = _dataVault;
            return vault != null && AreSomaticVaultBuffersReady(vault);
        }

        private bool PrepareNativeStateCold()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            _dataVault = vault;
            if (!AreSomaticVaultBuffersReady(vault))
                AllocateVaultBuffers(vault);

            if (!AreSomaticVaultBuffersReady(vault))
                return false;

            if (!_legacyScanAttempted)
            {
                _legacyScanAttempted = true;
                LoadLegacyOrEmergencyKinematics();
            }

            return true;
        }

        private void AllocateVaultBuffers(IDataVault vault)
        {
            bool ready =
                EnsureSomaticVaultBuffer(ref _stateHandle, BufferID.ShinobuSomaticKinematicState, 1, NativeArrayOptions.ClearMemory) &&
                EnsureSomaticVaultBuffer(ref _sphereHandle, BufferID.ShinobuSomaticBoundingSphere, 1, NativeArrayOptions.ClearMemory) &&
                EnsureSomaticVaultBuffer(ref _handHistoryHandle, BufferID.ShinobuSomaticHandStrokeHistory, HandHistoryCapacity, NativeArrayOptions.ClearMemory) &&
                EnsureSomaticVaultBuffer(ref _tuningHandle, BufferID.ShinobuSomaticTuning, 1, NativeArrayOptions.ClearMemory) &&
                EnsureSomaticVaultBuffer(ref _dragLutHandle, BufferID.ShinobuSomaticDragLut, DragLutCapacity, NativeArrayOptions.ClearMemory) &&
                EnsureSomaticVaultBuffer(ref _signalScratchHandle, BufferID.ShinobuSomaticSignalScratch, 1, NativeArrayOptions.ClearMemory) &&
                EnsureSomaticVaultBuffer(ref _blackBoxHandle, BufferID.ShinobuSomaticBlackBox, BlackBoxCapacity, NativeArrayOptions.ClearMemory) &&
                EnsureSomaticVaultBuffer(ref _blackBoxCursorHandle, BufferID.ShinobuSomaticBlackBoxCursor, 1, NativeArrayOptions.ClearMemory) &&
                EnsureSomaticVaultBuffer(ref _csvScratchHandle, BufferID.ShinobuSomaticCsvScratch, CsvScratchCapacity, NativeArrayOptions.UninitializedMemory);

            if (ready)
            {
                InitializeBuffersIfCold(vault);
                return;
            }

            ReleaseSomaticVaultHandles(vault);
        }

        private void InitializeBuffersIfCold(IDataVault vault)
        {
            if (!TryAcquireStateWriteBuffer(out NativeArray<PlayerKinematicState> stateBuffer))
                return;

            float3 localPosition = _cachedTransform != null
                ? (float3)(_cachedTransform.position)
                : float3.zero;
            double3 sector = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            PlayerKinematicState state = default;
            try
            {
                state = stateBuffer[0];
                if (state.PlayerRadius <= 0.01f || !math.all(math.isfinite(state.LocalPosition)))
                {
                    state.LocalPosition = localPosition;
                    state.LastValidLocalPosition = localPosition;
                    state.Aup = sector + (double3)localPosition;
                    state.SectorOriginAup = sector;
                    state.PlayerRadius = SomaticKinematicsTuningData.CreateEmergency().PlayerRadius;
                    state.Stamina01 = 1.0f;
                    state.StableId = _sourceId;
                    stateBuffer[0] = state;
                }
            }
            finally
            {
                ReleaseStateWriteBuffer();
            }

            if (TryAcquireSphereWriteBuffer(out NativeArray<PlayerBoundingSphere> sphereBuffer))
            {
                try
                {
                    if (sphereBuffer[0].Radius <= 0.01f)
                    {
                        PlayerBoundingSphere sphere = default;
                        sphere.CenterLocal = state.LocalPosition;
                        sphere.PreviousCenterLocal = state.LocalPosition;
                        sphere.Radius = state.PlayerRadius;
                        sphereBuffer[0] = sphere;
                    }
                }
                finally
                {
                    ReleaseSphereWriteBuffer();
                }
            }

            if (TryAcquireTuningWriteBuffer(out NativeArray<SomaticKinematicsTuningData> tuningBuffer))
            {
                try
                {
                    if (tuningBuffer[0].PlayerRadius <= 0.01f)
                        tuningBuffer[0] = SomaticKinematicsTuningData.CreateEmergency();
                }
                finally
                {
                    ReleaseTuningWriteBuffer();
                }
            }

            if (TryAcquireDragLutWriteBuffer(out NativeArray<float> dragLut))
            {
                try
                {
                    if (dragLut[0] <= 0f)
                    {
                        float baseDrag = 1.65f;
                        if (TryReadTuningBuffer(out NativeArray<SomaticKinematicsTuningData>.ReadOnly tuningRead))
                            baseDrag = tuningRead[0].BaseDrag;
                        FillEmergencyDragLut(dragLut, baseDrag);
                    }
                }
                finally
                {
                    ReleaseDragLutWriteBuffer();
                }
            }

            _mockWorldSampler = MockWorldSampler.Create(state.LocalPosition);
        }

        private bool EnsureLocalSimulationScratch()
        {
            return _localScratch.Ensure();
        }

        private bool HydrateLocalSimulationScratchFromVault()
        {
            if (!TryReadStateBuffer(out NativeArray<PlayerKinematicState>.ReadOnly state) ||
                !TryReadSphereBuffer(out NativeArray<PlayerBoundingSphere>.ReadOnly sphere) ||
                !TryReadHandHistoryBuffer(out NativeArray<SomaticHandStrokeSample>.ReadOnly handHistory) ||
                !TryReadTuningBuffer(out NativeArray<SomaticKinematicsTuningData>.ReadOnly tuning) ||
                !TryReadDragLutBuffer(out NativeArray<float>.ReadOnly dragLut) ||
                !TryReadSignalScratchBuffer(out NativeArray<SomaticKinematicSignalScratch>.ReadOnly signalScratch) ||
                !TryReadBlackBoxCursorBuffer(out NativeArray<int>.ReadOnly blackBoxCursor))
            {
                return false;
            }

            _localScratch.State[0] = state[0];
            _localScratch.Sphere[0] = sphere[0];
            _localScratch.Tuning[0] = tuning[0];
            _localScratch.SignalScratch[0] = signalScratch[0];
            _localScratch.BlackBoxCursor[0] = blackBoxCursor[0];

            CopyReadOnlyToLocal(handHistory, _localScratch.HandHistory, HandHistoryCapacity);
            CopyReadOnlyToLocal(dragLut, _localScratch.DragLut, DragLutCapacity);
            return true;
        }

        private bool FlushLocalSimulationScratchToVault()
        {
            return TryFlushSignalScratch() &&
                   TryFlushSphereScratch() &&
                   TryFlushHandHistoryScratch() &&
                   TryFlushBlackBoxScratch() &&
                   TryFlushBlackBoxCursorScratch() &&
                   TryFlushStateScratch();
        }

        private static void CopyReadOnlyToLocal<T>(NativeArray<T>.ReadOnly source, NativeArray<T> destination, int count) where T : struct
        {
            int length = math.min(count, math.min(source.Length, destination.Length));
            for (int i = 0; i < length; i++)
                destination[i] = source[i];
        }

        private bool TryFlushStateScratch()
        {
            if (!TryAcquireStateWriteBuffer(out NativeArray<PlayerKinematicState> state))
                return false;

            try
            {
                state[0] = _localScratch.State[0];
                return true;
            }
            finally
            {
                ReleaseStateWriteBuffer();
            }
        }

        private bool TryFlushSphereScratch()
        {
            if (!TryAcquireSphereWriteBuffer(out NativeArray<PlayerBoundingSphere> sphere))
                return false;

            try
            {
                sphere[0] = _localScratch.Sphere[0];
                return true;
            }
            finally
            {
                ReleaseSphereWriteBuffer();
            }
        }

        private bool TryFlushHandHistoryScratch()
        {
            if (!TryAcquireHandHistoryWriteBuffer(out NativeArray<SomaticHandStrokeSample> handHistory))
                return false;

            try
            {
                CopyLocalToWrite(_localScratch.HandHistory, handHistory, HandHistoryCapacity);
                return true;
            }
            finally
            {
                ReleaseHandHistoryWriteBuffer();
            }
        }

        private bool TryFlushSignalScratch()
        {
            if (!TryAcquireSignalScratchWriteBuffer(out NativeArray<SomaticKinematicSignalScratch> signalScratch))
                return false;

            try
            {
                signalScratch[0] = _localScratch.SignalScratch[0];
                return true;
            }
            finally
            {
                ReleaseSignalScratchWriteBuffer();
            }
        }

        private bool TryFlushBlackBoxScratch()
        {
            if (!TryAcquireBlackBoxWriteBuffer(out NativeArray<SomaticKinematicBlackBoxEntry> blackBox))
                return false;

            try
            {
                int length = math.min(blackBox.Length, _localScratch.BlackBox.Length);
                if (length <= 0)
                    return false;

                int index = PositiveModulo(_localBlackBoxWriteIndex, length);
                blackBox[index] = _localScratch.BlackBox[index];
                return true;
            }
            finally
            {
                ReleaseBlackBoxWriteBuffer();
            }
        }

        private bool TryFlushBlackBoxCursorScratch()
        {
            if (!TryAcquireBlackBoxCursorWriteBuffer(out NativeArray<int> cursor))
                return false;

            try
            {
                cursor[0] = _localScratch.BlackBoxCursor[0];
                return true;
            }
            finally
            {
                ReleaseBlackBoxCursorWriteBuffer();
            }
        }

        private static void CopyLocalToWrite<T>(NativeArray<T> source, NativeArray<T> destination, int count) where T : struct
        {
            int length = math.min(count, math.min(source.Length, destination.Length));
            for (int i = 0; i < length; i++)
                destination[i] = source[i];
        }

        private int ResolveLocalBlackBoxWriteIndex()
        {
            if (!_localScratch.BlackBoxCursor.IsCreated || _localScratch.BlackBoxCursor.Length == 0)
                return 0;

            return PositiveModulo(_localScratch.BlackBoxCursor[0], math.max(1, BlackBoxCapacity));
        }

        private static int PositiveModulo(int value, int length)
        {
            int safeLength = math.max(1, length);
            int modulo = value % safeLength;
            return modulo < 0 ? modulo + safeLength : modulo;
        }

        private bool TryReadStateBuffer(out NativeArray<PlayerKinematicState>.ReadOnly state)
        {
            return TryReadOnlySomaticVaultBuffer(ref _stateHandle, BufferID.ShinobuSomaticKinematicState, 1, out state);
        }

        private bool TryReadSphereBuffer(out NativeArray<PlayerBoundingSphere>.ReadOnly sphere)
        {
            return TryReadOnlySomaticVaultBuffer(ref _sphereHandle, BufferID.ShinobuSomaticBoundingSphere, 1, out sphere);
        }

        private bool TryReadHandHistoryBuffer(out NativeArray<SomaticHandStrokeSample>.ReadOnly handHistory)
        {
            return TryReadOnlySomaticVaultBuffer(ref _handHistoryHandle, BufferID.ShinobuSomaticHandStrokeHistory, HandHistoryCapacity, out handHistory);
        }

        private bool TryReadTuningBuffer(out NativeArray<SomaticKinematicsTuningData>.ReadOnly tuning)
        {
            return TryReadOnlySomaticVaultBuffer(ref _tuningHandle, BufferID.ShinobuSomaticTuning, 1, out tuning);
        }

        private bool TryReadDragLutBuffer(out NativeArray<float>.ReadOnly dragLut)
        {
            return TryReadOnlySomaticVaultBuffer(ref _dragLutHandle, BufferID.ShinobuSomaticDragLut, DragLutCapacity, out dragLut);
        }

        private bool TryReadSignalScratchBuffer(out NativeArray<SomaticKinematicSignalScratch>.ReadOnly scratch)
        {
            return TryReadOnlySomaticVaultBuffer(ref _signalScratchHandle, BufferID.ShinobuSomaticSignalScratch, 1, out scratch);
        }

        private bool TryReadBlackBoxBuffer(out NativeArray<SomaticKinematicBlackBoxEntry>.ReadOnly blackBox)
        {
            return TryReadOnlySomaticVaultBuffer(ref _blackBoxHandle, BufferID.ShinobuSomaticBlackBox, BlackBoxCapacity, out blackBox);
        }

        private bool TryReadBlackBoxCursorBuffer(out NativeArray<int>.ReadOnly cursor)
        {
            return TryReadOnlySomaticVaultBuffer(ref _blackBoxCursorHandle, BufferID.ShinobuSomaticBlackBoxCursor, 1, out cursor);
        }

        private bool TryReadCsvScratchBuffer(out NativeArray<byte>.ReadOnly scratch)
        {
            return TryReadOnlySomaticVaultBuffer(ref _csvScratchHandle, BufferID.ShinobuSomaticCsvScratch, CsvScratchCapacity, out scratch);
        }

        private bool TryAcquireStateWriteBuffer(out NativeArray<PlayerKinematicState> state)
        {
            return TryAcquireSomaticWriteBuffer(ref _stateHandle, BufferID.ShinobuSomaticKinematicState, 1, out state, out _stateWriteVault);
        }

        private void ReleaseStateWriteBuffer()
        {
            IDataVault vault = _stateWriteVault;
            _stateWriteVault = null;
            ReleaseSomaticWriteBuffer(vault, in _stateHandle, BufferID.ShinobuSomaticKinematicState);
        }

        private bool TryAcquireSphereWriteBuffer(out NativeArray<PlayerBoundingSphere> sphere)
        {
            return TryAcquireSomaticWriteBuffer(ref _sphereHandle, BufferID.ShinobuSomaticBoundingSphere, 1, out sphere, out _sphereWriteVault);
        }

        private void ReleaseSphereWriteBuffer()
        {
            IDataVault vault = _sphereWriteVault;
            _sphereWriteVault = null;
            ReleaseSomaticWriteBuffer(vault, in _sphereHandle, BufferID.ShinobuSomaticBoundingSphere);
        }

        private bool TryAcquireHandHistoryWriteBuffer(out NativeArray<SomaticHandStrokeSample> handHistory)
        {
            return TryAcquireSomaticWriteBuffer(ref _handHistoryHandle, BufferID.ShinobuSomaticHandStrokeHistory, HandHistoryCapacity, out handHistory, out _handHistoryWriteVault);
        }

        private void ReleaseHandHistoryWriteBuffer()
        {
            IDataVault vault = _handHistoryWriteVault;
            _handHistoryWriteVault = null;
            ReleaseSomaticWriteBuffer(vault, in _handHistoryHandle, BufferID.ShinobuSomaticHandStrokeHistory);
        }

        private bool TryAcquireTuningWriteBuffer(out NativeArray<SomaticKinematicsTuningData> tuning)
        {
            return TryAcquireSomaticWriteBuffer(ref _tuningHandle, BufferID.ShinobuSomaticTuning, 1, out tuning, out _tuningWriteVault);
        }

        private void ReleaseTuningWriteBuffer()
        {
            IDataVault vault = _tuningWriteVault;
            _tuningWriteVault = null;
            ReleaseSomaticWriteBuffer(vault, in _tuningHandle, BufferID.ShinobuSomaticTuning);
        }

        private bool TryAcquireDragLutWriteBuffer(out NativeArray<float> dragLut)
        {
            return TryAcquireSomaticWriteBuffer(ref _dragLutHandle, BufferID.ShinobuSomaticDragLut, DragLutCapacity, out dragLut, out _dragLutWriteVault);
        }

        private void ReleaseDragLutWriteBuffer()
        {
            IDataVault vault = _dragLutWriteVault;
            _dragLutWriteVault = null;
            ReleaseSomaticWriteBuffer(vault, in _dragLutHandle, BufferID.ShinobuSomaticDragLut);
        }

        private bool TryAcquireSignalScratchWriteBuffer(out NativeArray<SomaticKinematicSignalScratch> scratch)
        {
            return TryAcquireSomaticWriteBuffer(ref _signalScratchHandle, BufferID.ShinobuSomaticSignalScratch, 1, out scratch, out _signalScratchWriteVault);
        }

        private void ReleaseSignalScratchWriteBuffer()
        {
            IDataVault vault = _signalScratchWriteVault;
            _signalScratchWriteVault = null;
            ReleaseSomaticWriteBuffer(vault, in _signalScratchHandle, BufferID.ShinobuSomaticSignalScratch);
        }

        private bool TryAcquireBlackBoxWriteBuffer(out NativeArray<SomaticKinematicBlackBoxEntry> blackBox)
        {
            return TryAcquireSomaticWriteBuffer(ref _blackBoxHandle, BufferID.ShinobuSomaticBlackBox, BlackBoxCapacity, out blackBox, out _blackBoxWriteVault);
        }

        private void ReleaseBlackBoxWriteBuffer()
        {
            IDataVault vault = _blackBoxWriteVault;
            _blackBoxWriteVault = null;
            ReleaseSomaticWriteBuffer(vault, in _blackBoxHandle, BufferID.ShinobuSomaticBlackBox);
        }

        private bool TryAcquireBlackBoxCursorWriteBuffer(out NativeArray<int> cursor)
        {
            return TryAcquireSomaticWriteBuffer(ref _blackBoxCursorHandle, BufferID.ShinobuSomaticBlackBoxCursor, 1, out cursor, out _blackBoxCursorWriteVault);
        }

        private void ReleaseBlackBoxCursorWriteBuffer()
        {
            IDataVault vault = _blackBoxCursorWriteVault;
            _blackBoxCursorWriteVault = null;
            ReleaseSomaticWriteBuffer(vault, in _blackBoxCursorHandle, BufferID.ShinobuSomaticBlackBoxCursor);
        }

#if UNITY_EDITOR
        private bool TryAcquireCsvScratchWriteBuffer(out NativeArray<byte> scratch)
        {
            return TryAcquireSomaticWriteBuffer(ref _csvScratchHandle, BufferID.ShinobuSomaticCsvScratch, CsvScratchCapacity, out scratch, out _csvScratchWriteVault);
        }

        private void ReleaseCsvScratchWriteBuffer()
        {
            IDataVault vault = _csvScratchWriteVault;
            _csvScratchWriteVault = null;
            ReleaseSomaticWriteBuffer(vault, in _csvScratchHandle, BufferID.ShinobuSomaticCsvScratch);
        }
#endif

        private bool AreSomaticVaultBuffersReady(IDataVault vault)
        {
            return HasSomaticVaultBuffer(vault, in _stateHandle, BufferID.ShinobuSomaticKinematicState, 1) &&
                   HasSomaticVaultBuffer(vault, in _sphereHandle, BufferID.ShinobuSomaticBoundingSphere, 1) &&
                   HasSomaticVaultBuffer(vault, in _handHistoryHandle, BufferID.ShinobuSomaticHandStrokeHistory, HandHistoryCapacity) &&
                   HasSomaticVaultBuffer(vault, in _tuningHandle, BufferID.ShinobuSomaticTuning, 1) &&
                   HasSomaticVaultBuffer(vault, in _dragLutHandle, BufferID.ShinobuSomaticDragLut, DragLutCapacity) &&
                   HasSomaticVaultBuffer(vault, in _signalScratchHandle, BufferID.ShinobuSomaticSignalScratch, 1) &&
                   HasSomaticVaultBuffer(vault, in _blackBoxHandle, BufferID.ShinobuSomaticBlackBox, BlackBoxCapacity) &&
                   HasSomaticVaultBuffer(vault, in _blackBoxCursorHandle, BufferID.ShinobuSomaticBlackBoxCursor, 1) &&
                   HasSomaticVaultBuffer(vault, in _csvScratchHandle, BufferID.ShinobuSomaticCsvScratch, CsvScratchCapacity);
        }

        private bool EnsureSomaticVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (HasSomaticVaultBuffer(vault, in handle, bufferId, requiredLength))
                return true;

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, VaultOwnerSystem, options);
            return HasSomaticVaultBuffer(vault, in handle, bufferId, requiredLength);
        }

        private bool TryAcquireSomaticWriteBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer,
            out IDataVault writeVault) where T : struct
        {
            buffer = default;
            writeVault = null;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (!IsSomaticVaultHandle(in handle, bufferId))
                return false;

            if (!vault.TryAcquireWriteLock(in handle, VaultOwnerSystem, out buffer))
                return false;

            bool releaseOnFailure = true;
            try
            {
                if (!vault.IsCompactionFenceActive &&
                    buffer.IsCreated &&
                    buffer.Length >= requiredLength)
                {
                    writeVault = vault;
                    releaseOnFailure = false;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (releaseOnFailure)
                    vault.ReleaseWriteLock(in handle, VaultOwnerSystem);
            }
        }

        private static void ReleaseSomaticWriteBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsSomaticVaultHandle(in handle, bufferId))
                vault.ReleaseWriteLock(in handle, VaultOwnerSystem);
        }

        private bool TryReadOnlySomaticVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            return IsSomaticVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   !vault.IsCompactionFenceActive &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool HasSomaticVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsSomaticVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer) &&
                   !vault.IsCompactionFenceActive &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsSomaticVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)VaultOwnerSystem &&
                   handle.Generation != 0u;
        }


        private void BuildFrameInput(float fixedDeltaTime, uint fixedFrame, NativeArray<PlayerKinematicState> stateBuffer)
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            IVRSomaticProvider provider = _somaticProvider;
            Vector3 headPosition = _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
            Quaternion headRotation = _cachedTransform != null ? _cachedTransform.rotation : Quaternion.identity;
            float oxygen01 = 1.0f;
            if (provider != null && provider.IsActive)
            {
                VRSomaticSnapshot snapshot = provider.CurrentSnapshot;
                headPosition = snapshot.HeadRuntimePosition;
                headRotation = snapshot.HeadRuntimeRotation;
                oxygen01 = math.isfinite(snapshot.Oxygen01) ? math.saturate(snapshot.Oxygen01) : 1.0f;
            }

            float3 headLocal = SanitizeFinite((float3)(headPosition), float3.zero);
            float3 headForward = NormalizeSafe(Forward(headRotation), new float3(0f, 0f, 1f));
            float3 leftHand = headLocal + new float3(-0.22f, -0.18f, 0.32f);
            float3 rightHand = headLocal + new float3(0.22f, -0.18f, 0.32f);
            byte leftTracked = 0;
            byte rightTracked = 0;
            if (provider != null && provider.TryGetHandPose(0, out VRSomaticHandPose leftPose))
            {
                float3 rawLeft = (float3)(leftPose.TargetRuntimePosition);
                bool finite = math.all(math.isfinite(rawLeft));
                leftHand = finite ? rawLeft : leftHand;
                leftTracked = leftPose.IsTracked && finite ? (byte)1 : (byte)0;
            }
            if (provider != null && provider.TryGetHandPose(1, out VRSomaticHandPose rightPose))
            {
                float3 rawRight = (float3)(rightPose.TargetRuntimePosition);
                bool finite = math.all(math.isfinite(rawRight));
                rightHand = finite ? rawRight : rightHand;
                rightTracked = rightPose.IsTracked && finite ? (byte)1 : (byte)0;
            }

            float3 controllerForward = _seaglideActive != 0
                ? NormalizeSafe(_seaglideForward, headForward)
                : NormalizeSafe(rightHand - headLocal, headForward);
            double3 sector = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            float3 flow = ResolveAbyssalFlow(headLocal);

            float qualityWeight01 = ResolveGlobalQualityWeight01(_cachedGlobalQualityWeight01);
            _cachedGlobalQualityWeight01 = qualityWeight01;
            float qualityPressure01 = 1f - SmoothQuality01(qualityWeight01);
            byte qualityPressureQ8 = EncodeUnitByte(qualityPressure01);
            _frameInput.HeadLocalPosition = headLocal;
            _frameInput.DeltaTime = math.isfinite(fixedDeltaTime) ? math.clamp(fixedDeltaTime, 0.001f, 0.05f) : 0.0166667f;
            _frameInput.HeadForward = headForward;
            _frameInput.SeaglideInput01 = _seaglideInput01;
            _frameInput.ControllerForward = controllerForward;
            float inputTimeSeconds = (float)Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds;
            _frameInput.TimeSeconds = math.isfinite(inputTimeSeconds) ? math.max(0f, inputTimeSeconds) : 0f;
            _frameInput.LeftHandLocal = leftHand;
            _frameInput.RightHandLocal = rightHand;
            _frameInput.FrameIndex = fixedFrame;
            _frameInput.LeftTracked = leftTracked;
            _frameInput.RightTracked = rightTracked;
            _frameInput.SeaglideActive = _seaglideActive;
            _frameInput.QualityPressureQ8 = qualityPressureQ8;
            _frameContext.SectorOriginAup = sector;
            _frameContext.AbyssalCurrent = flow;
            _frameContext.SystemStress01 = math.lerp(0.1f, 0.75f, qualityPressure01);
            _frameContext.QualityPressureQ8 = qualityPressureQ8;

            if (stateBuffer.IsCreated && stateBuffer.Length > 0)
            {
                PlayerKinematicState state = stateBuffer[0];
                state.Stamina01 = math.max(0f, oxygen01);
                state.Flags &= ~StateFlagQualityPressureReserved;
                if (_seaglideActive != 0)
                    state.Flags |= StateFlagSeaglide;
                else
                    state.Flags &= ~StateFlagSeaglide;
                stateBuffer[0] = state;
            }
        }

        private static float ResolveGlobalQualityWeight01(float fallback)
        {
            float value = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(fallback, value, math.isfinite(value)));
        }

        private static float SmoothQuality01(float value)
        {
            float t = math.saturate(math.select(1f, value, math.isfinite(value)));
            return t * t * (3f - 2f * t);
        }

        private static byte EncodeUnitByte(float value)
        {
            return (byte)math.clamp((int)math.round(math.saturate(value) * 255f), 0, 255);
        }

        private float3 ResolveAbyssalFlow(float3 localPosition)
        {
            float3 flow = float3.zero;
            IWeatherService weather = _weatherService;
            if (weather != null && weather.IsInitialized)
                flow = (float3)(weather.GlobalCurrentVector);

            if (!math.all(math.isfinite(flow)) || math.lengthsq(flow) <= 0.0001f)
            {
                float stripe = math.frac((localPosition.x * 0.013f) + (localPosition.z * 0.007f));
                float triangle = math.abs((stripe * 2.0f) - 1.0f);
                flow = new float3((triangle - 0.5f) * 0.18f, 0f, (0.5f - triangle) * 0.12f);
            }

            return SanitizeFinite(flow, float3.zero);
        }

        private bool CompleteScheduledKinematicsInPostFixedOrShutdown(bool forceComplete)
        {
            if (!_kinematicsJobScheduled && !_frameReadyForPostFixed)
                return false;

            if (_kinematicsJobScheduled)
            {
                if (!DispatcherJobFence.TryComplete(ref _pendingKinematicsHandle, forceComplete))
                    return false;

                _kinematicsJobScheduled = false;
                if (!FlushLocalSimulationScratchToVault())
                {
                    _frameReadyForPostFixed = false;
                    return false;
                }
            }

            _frameReadyForPostFixed = false;
            return true;
        }

        private void PublishCompletedFrame()
        {
            if (!TryReadStateBuffer(out NativeArray<PlayerKinematicState>.ReadOnly stateBuffer))
                return;

            if (!TryReadSignalScratchBuffer(out NativeArray<SomaticKinematicSignalScratch>.ReadOnly scratchBuffer))
                return;

            PlayerKinematicState state = stateBuffer[0];
            SomaticKinematicSignalScratch scratch = scratchBuffer[0];
            _slowExertionAccumulator += scratch.ExertionDelta;
            _slowAgainstCurrentAccumulator += scratch.AgainstCurrent01;
            Hecton8.World.AbsoluteUniversePosition aup = Hecton8.World.AbsoluteUniversePosition.FromAbsolutePosition(state.Aup);

            KccVelocitySignal velocitySignal = default;
            velocitySignal.BodyAup = aup;
            velocitySignal.Velocity = SanitizeFinite(state.Velocity, float3.zero);
            velocitySignal.PlanarSpeedSq = math.lengthsq(new float2(velocitySignal.Velocity.x, velocitySignal.Velocity.z));
            velocitySignal.Frame = state.Frame;
            velocitySignal.SourceId = _sourceId;
            velocitySignal.Sequence = NextSequence(ref _kccVelocitySequence);
            velocitySignal.Flags = 0;
            velocitySignal.QualityPressureQ8 = _frameContext.QualityPressureQ8;
            SignalBus<KccVelocitySignal>.TryPushTracked(in velocitySignal, ref s_x001SomaticKinematicsRuntimeSignalPushDropCount);

            if ((scratch.Flags & SignalFlagAcoustic) != 0u)
            {
                MovementAcousticSignal movement = default;
                movement.PositionAup = aup;
                movement.Volume = scratch.AcousticMagnitude;
                movement.VelocitySq = math.lengthsq(velocitySignal.Velocity);
                movement.SourceId = _sourceId;
                movement.LocomotionMode = state.PlayerRadius > 0.01f ? (byte)1 : (byte)0;
                movement.SurfaceMode = state.SurfaceSubmersion01 <= 0.001f ? (byte)1 : (byte)0;
                movement.Flags = state.LastPushOutMeters > 0.001f ? (byte)1 : (byte)0;
                SignalBus<MovementAcousticSignal>.TryPushTracked(in movement, ref s_x001SomaticKinematicsRuntimeSignalPushDropCount);
            }

            if ((scratch.Flags & SignalFlagHaptic) != 0u)
            {
                HapticRequest canonicalHaptic = default;
                canonicalHaptic.Intensity01 = scratch.HapticAmplitude;
                canonicalHaptic.DurationSeconds = math.lerp(0.035f, 0.12f, scratch.HapticAmplitude);
                canonicalHaptic.Frequency01 = scratch.HapticAmplitude;
                canonicalHaptic.SourceHash = AcousticKinematicSoundHash;
                canonicalHaptic.Frame = state.Frame;
                canonicalHaptic.Channel = HapticRequest.ChannelCollision;
                canonicalHaptic.Flags = HapticRequest.FlagLightThud;
                SignalBus<HapticRequest>.TryPushTracked(in canonicalHaptic, ref s_x001SomaticKinematicsRuntimeSignalPushDropCount);
            }

            if ((scratch.Flags & SignalFlagFault) != 0u)
                DumpBlackBoxOnce();
        }

        private void PublishExertionSignal()
        {
            if (_slowExertionAccumulator <= 0.0001f && _slowAgainstCurrentAccumulator <= 0.0001f)
                return;

            ShinobuPlayerExertionSignal exertion = default;
            PlayerKinematicState state = TryReadStateBuffer(out NativeArray<PlayerKinematicState>.ReadOnly stateBuffer)
                ? stateBuffer[0]
                : default;
            exertion.Frame = state.Frame != 0u ? state.Frame : _fixedFrameSequence;
            exertion.SourceId = _sourceId;
            exertion.StrokeMagnitude = math.isfinite(_slowExertionAccumulator) ? math.max(0f, _slowExertionAccumulator) : 0f;
            exertion.AgainstCurrent01 = math.isfinite(_slowAgainstCurrentAccumulator) ? math.saturate(_slowAgainstCurrentAccumulator) : 0f;
            exertion.Stamina01 = state.PlayerRadius > 0.01f ? math.saturate(state.Stamina01) : 1.0f;
            SignalBus<ShinobuPlayerExertionSignal>.TryPushTracked(in exertion, ref s_x001SomaticKinematicsRuntimeSignalPushDropCount);
            _slowExertionAccumulator = 0f;
            _slowAgainstCurrentAccumulator = 0f;
        }

        private void LoadLegacyOrEmergencyKinematics()
        {
            SomaticKinematicsTuningData tuning = GenerateEmergencyMockKinematics();
            try
            {
                if (TryReadLegacyBinary(ref tuning))
                    ApplyTuning(in tuning);
                else
                    ApplyTuning(in tuning);
            }
            catch (IOException)
            {
                ApplyEmergencyKinematics();
            }
            catch (UnauthorizedAccessException)
            {
                ApplyEmergencyKinematics();
            }
            catch (ObjectDisposedException)
            {
                ApplyEmergencyKinematics();
            }
            catch (InvalidOperationException)
            {
                ApplyEmergencyKinematics();
            }
            catch (ArgumentException)
            {
                ApplyEmergencyKinematics();
            }
            catch (NotSupportedException)
            {
                ApplyEmergencyKinematics();
            }
        }

        private void ApplyEmergencyKinematics()
        {
            SomaticKinematicsTuningData tuning = GenerateEmergencyMockKinematics();
            ApplyTuning(in tuning);
        }

        public SomaticKinematicsTuningData GenerateEmergencyMockKinematics()
        {
            SomaticKinematicsTuningData tuning = SomaticKinematicsTuningData.CreateEmergency();
            return tuning;
        }

        private bool TryReadLegacyBinary(ref SomaticKinematicsTuningData tuning)
        {
            if (string.IsNullOrEmpty(_projectRoot))
                return false;

            if (TryReadLegacyBinaryAt(Path.Combine(_projectRoot, "StreamingAssets", "hydro_drag_constants.bin"), ref tuning))
                return true;
            if (TryReadLegacyBinaryAt(Path.Combine(_projectRoot, "Assets", "StreamingAssets", "hydro_drag_constants.bin"), ref tuning))
                return true;
            if (TryReadLegacyBinaryAt(Path.Combine(_projectRoot, "StreamingAssets", "vr_comfort_profiles.h8bin"), ref tuning))
                return true;
            if (TryReadLegacyBinaryAt(Path.Combine(_projectRoot, "Assets", "StreamingAssets", "vr_comfort_profiles.h8bin"), ref tuning))
                return true;

            string archiveRoot = Path.Combine(_projectRoot, "Docs", "Archive");
            if (!Directory.Exists(archiveRoot))
                return false;

            string[] files = Directory.GetFiles(archiveRoot, "*.bin", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string name = Path.GetFileName(file);
                if (name == "hydro_drag_constants.bin" || name == "vr_comfort_profiles.h8bin")
                    return TryReadLegacyBinaryAt(file, ref tuning);
            }

            return false;
        }

        private static bool TryReadLegacyBinaryAt(string path, ref SomaticKinematicsTuningData tuning)
        {
            if (!File.Exists(path))
                return false;

            Span<byte> span = stackalloc byte[16];
            int read;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))
            {
                read = stream.Read(span);
            }

            if (read < 16)
                return false;

            float baseDrag = ReadFloat32LittleEndian(span, 0);
            float stroke = ReadFloat32LittleEndian(span, 4);
            float seaglide = ReadFloat32LittleEndian(span, 8);
            float buoyancy = ReadFloat32LittleEndian(span, 12);
            if (!math.isfinite(baseDrag) || !math.isfinite(stroke) || !math.isfinite(seaglide) || !math.isfinite(buoyancy))
                return false;

            tuning.BaseDrag = math.clamp(baseDrag, 0.05f, 8.0f);
            tuning.StrokeMultiplier = math.clamp(stroke, 0.1f, 20.0f);
            tuning.SeaglideAcceleration = math.clamp(seaglide, 0.1f, 30.0f);
            tuning.SurfaceBuoyancy = math.clamp(buoyancy, 0.1f, 30.0f);
            tuning.Flags |= 2u;
            return true;
        }

        private void ApplyTuning(in SomaticKinematicsTuningData tuning)
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            if (TryAcquireTuningWriteBuffer(out NativeArray<SomaticKinematicsTuningData> tuningBuffer))
            {
                try
                {
                    tuningBuffer[0] = tuning;
                }
                finally
                {
                    ReleaseTuningWriteBuffer();
                }
            }

            if (TryAcquireDragLutWriteBuffer(out NativeArray<float> dragLut))
            {
                try
                {
                    FillEmergencyDragLut(dragLut, tuning.BaseDrag);
                }
                finally
                {
                    ReleaseDragLutWriteBuffer();
                }
            }
        }

        private static void FillEmergencyDragLut(NativeArray<float> dragLut, float baseDrag)
        {
            if (!dragLut.IsCreated)
                return;

            float safeBase = math.isfinite(baseDrag) ? math.max(0.01f, baseDrag) : 1.65f;
            for (int i = 0; i < dragLut.Length; i++)
            {
                float t = dragLut.Length > 1 ? i * math.rcp(dragLut.Length - 1) : 0f;
                float eased = t * t * (3f - (2f * t));
                dragLut[i] = math.lerp(safeBase, safeBase * 2.35f, eased);
            }
        }

#if UNITY_EDITOR
        private unsafe void TryApplyCsvOverrides()
        {
            if (_frameReadyForPostFixed || string.IsNullOrEmpty(_csvOverridePath))
                return;

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            try
            {
                if (!File.Exists(_csvOverridePath))
                    return;

                long ticks = File.GetLastWriteTimeUtc(_csvOverridePath).Ticks;
                if (ticks == 0L || ticks == _csvLastWriteTicks)
                    return;

                int read = 0;
                Span<byte> chunk = stackalloc byte[1024];
                using (FileStream stream = new FileStream(_csvOverridePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
                {
                    long length = stream.Length;
                    if (length <= 0L || length > CsvScratchCapacity)
                        return;

                    int targetLength = (int)length;
                    while (read < targetLength)
                    {
                        int request = math.min(chunk.Length, targetLength - read);
                        int count = stream.Read(chunk.Slice(0, request));
                        if (count <= 0)
                            break;
                        if (!TryWriteCsvScratchChunk(vault, read, chunk.Slice(0, count)))
                            return;
                        read += count;
                    }

                    if (read <= 0)
                        return;

                    if (!TryApplyCsvScratchOverrides(vault, read))
                        return;
                }
                _csvLastWriteTicks = ticks;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        private unsafe bool TryWriteCsvScratchChunk(IDataVault vault, int offset, ReadOnlySpan<byte> source)
        {
            if (vault == null || vault.IsCompactionFenceActive || offset < 0 || source.Length <= 0)
                return false;

            bool csvScratchLocked = false;
            try
            {
                if (!TryAcquireCsvScratchWriteBuffer(out NativeArray<byte> csvScratch))
                    return false;
                csvScratchLocked = true;

                if (offset > csvScratch.Length ||
                    source.Length > csvScratch.Length - offset)
                    return false;

                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(csvScratch);
                source.CopyTo(new Span<byte>(ptr + offset, source.Length));
                return true;
            }
            finally
            {
                if (csvScratchLocked)
                    ReleaseCsvScratchWriteBuffer();
            }
        }

        private unsafe bool TryApplyCsvScratchOverrides(IDataVault vault, int byteCount)
        {
            if (vault == null || vault.IsCompactionFenceActive || byteCount <= 0 || byteCount > CsvScratchCapacity)
                return false;

            if (!TryReadTuningBuffer(out NativeArray<SomaticKinematicsTuningData>.ReadOnly tuningRead))
                return false;

            SomaticKinematicsTuningData tuning = tuningRead[0];
            if (!TryAcquireCsvScratchWriteBuffer(out NativeArray<byte> csvScratch))
                return false;

            try
            {
                if (byteCount > csvScratch.Length)
                    return false;

                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(csvScratch);
                ParseCsvOverrides(new ReadOnlySpan<byte>(ptr, byteCount), ref tuning);
            }
            finally
            {
                ReleaseCsvScratchWriteBuffer();
            }

            if (!TryAcquireTuningWriteBuffer(out NativeArray<SomaticKinematicsTuningData> tuningBuffer))
                return false;

            try
            {
                tuningBuffer[0] = tuning;
            }
            finally
            {
                ReleaseTuningWriteBuffer();
            }

            if (!TryAcquireDragLutWriteBuffer(out NativeArray<float> dragLut))
                return false;

            try
            {
                FillEmergencyDragLut(dragLut, tuning.BaseDrag);
                return true;
            }
            finally
            {
                ReleaseDragLutWriteBuffer();
            }
        }

        private static void ParseCsvOverrides(ReadOnlySpan<byte> bytes, ref SomaticKinematicsTuningData tuning)
        {
            int start = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != (byte)'\n' && bytes[i] != (byte)'\r')
                    continue;

                ReadOnlySpan<byte> line = Trim(bytes.Slice(start, i - start));
                ApplyCsvLine(line, ref tuning);
                while (i + 1 < bytes.Length && (bytes[i + 1] == (byte)'\n' || bytes[i + 1] == (byte)'\r'))
                    i++;
                start = i + 1;
            }
        }

        private static void ApplyCsvLine(ReadOnlySpan<byte> line, ref SomaticKinematicsTuningData tuning)
        {
            if (line.Length == 0 || line[0] == (byte)'#')
                return;

            int separator = -1;
            for (int i = 0; i < line.Length; i++)
            {
                byte b = line[i];
                if (b == (byte)',' || b == (byte)'=' || b == (byte)';')
                {
                    separator = i;
                    break;
                }
            }

            if (separator <= 0)
                return;

            ReadOnlySpan<byte> key = Trim(line.Slice(0, separator));
            ReadOnlySpan<byte> valueSpan = Trim(line.Slice(separator + 1));
            if (!TryParseFloat(valueSpan, out float value))
                return;

            uint hash = HashKey(key);
            if (hash == 0x37831E0Au)
                tuning.BaseDrag = math.clamp(value, 0.01f, 8.0f);
            else if (hash == 0x48A72356u)
                tuning.StrokeMultiplier = math.clamp(value, 0.1f, 30.0f);
            else if (hash == 0x06F7AA95u)
                tuning.SeaglideAcceleration = math.clamp(value, 0.1f, 40.0f);
            else if (hash == 0xD4440F8Au)
                tuning.SurfaceBuoyancy = math.clamp(value, 0.1f, 40.0f);
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start <= end && span[start] <= 32)
                start++;
            while (end >= start && span[end] <= 32)
                end--;
            return start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static uint HashKey(ReadOnlySpan<byte> key)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < key.Length; i++)
            {
                byte b = key[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                if ((b < (byte)'a' || b > (byte)'z') && (b < (byte)'0' || b > (byte)'9'))
                    continue;
                hash = (hash ^ b) * 16777619u;
            }
            return hash;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> span, out float value)
        {
            value = 0f;
            if (span.Length == 0)
                return false;

            int i = 0;
            float sign = 1f;
            if (span[i] == (byte)'-')
            {
                sign = -1f;
                i++;
            }
            else if (span[i] == (byte)'+')
            {
                i++;
            }

            float integer = 0f;
            bool any = false;
            while (i < span.Length && span[i] >= (byte)'0' && span[i] <= (byte)'9')
            {
                integer = (integer * 10f) + (span[i] - (byte)'0');
                i++;
                any = true;
            }

            float fraction = 0f;
            float scale = 1f;
            if (i < span.Length && span[i] == (byte)'.')
            {
                i++;
                while (i < span.Length && span[i] >= (byte)'0' && span[i] <= (byte)'9')
                {
                    fraction = (fraction * 10f) + (span[i] - (byte)'0');
                    scale *= 10f;
                    i++;
                    any = true;
                }
            }

            if (!any)
                return false;

            value = sign * (integer + (fraction * math.rcp(math.max(1f, scale))));
            return math.isfinite(value);
        }
#endif

        private unsafe void DumpBlackBoxOnce()
        {
            if (_dumpWritten || !TryResolveBlackBoxDumpHeader(out SomaticBlackBoxDumpHeader header, out int entryCount))
                return;

            const string dumpPayloadLabel = "SomaticKinematicsRuntime.BlackBoxDumpPayload";
            NativeArray<byte> payload = default;
            try
            {
                string path = ResolveProjectPath(DumpRelativePath);
                int headerBytes = UnsafeUtility.SizeOf<SomaticBlackBoxDumpHeader>();
                int entryBytes = UnsafeUtility.SizeOf<SomaticKinematicBlackBoxEntry>();
                int byteCount = headerBytes + entryCount * entryBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(SomaticKinematicsRuntime),
                    dumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                UnsafeUtility.MemCpy(target, &header, headerBytes);

                int cursor = headerBytes;
                for (int i = 0; i < entryCount; i++)
                {
                    if (!TryReadBlackBoxDumpEntry(i, out SomaticKinematicBlackBoxEntry entry))
                    {
                        _dumpWritten = false;
                        return;
                    }

                    UnsafeUtility.MemCpy(target + cursor, &entry, entryBytes);
                    cursor += entryBytes;
                }

                _dumpWritten = NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            catch (IOException)
            {
                _dumpWritten = false;
            }
            catch (UnauthorizedAccessException)
            {
                _dumpWritten = false;
            }
            catch (ObjectDisposedException)
            {
                _dumpWritten = false;
            }
            catch (InvalidOperationException)
            {
                _dumpWritten = false;
            }
            catch (ArgumentException)
            {
                _dumpWritten = false;
            }
            catch (NotSupportedException)
            {
                _dumpWritten = false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(SomaticKinematicsRuntime),
                    dumpPayloadLabel);
            }
        }

        private bool TryResolveBlackBoxDumpHeader(out SomaticBlackBoxDumpHeader header, out int entryCount)
        {
            header = default;
            entryCount = 0;

            if (!TryReadBlackBoxBuffer(out NativeArray<SomaticKinematicBlackBoxEntry>.ReadOnly blackBox) ||
                !TryReadBlackBoxCursorBuffer(out NativeArray<int>.ReadOnly blackBoxCursor) ||
                blackBox.Length <= 0 ||
                blackBoxCursor.Length <= 0)
                return false;

            entryCount = blackBox.Length;
            header.Magic = 0x53484E36u;
            header.Version = 1u;
            header.EntryCount = (uint)entryCount;
            header.EntryBytes = (uint)UnsafeUtility.SizeOf<SomaticKinematicBlackBoxEntry>();
            header.Cursor = (uint)blackBoxCursor[0];
            header.Frame = _fixedFrameSequence;
            return true;
        }

        private bool TryReadBlackBoxDumpEntry(int index, out SomaticKinematicBlackBoxEntry entry)
        {
            entry = default;

            if (index < 0 ||
                !TryReadBlackBoxBuffer(out NativeArray<SomaticKinematicBlackBoxEntry>.ReadOnly blackBox) ||
                index >= blackBox.Length)
                return false;

            entry = blackBox[index];
            return true;
        }

        private void RebindServices()
        {
            CacheDataVaultCold();
            _weatherService = GlobalRegistry.Weather;
            _somaticProvider = GlobalRegistry.VRSomatic;
            _oceanKinematicsService = GlobalRegistry.OceanKinematics;
        }

        private void RefreshLocalTuningSeaLevelFromOcean()
        {
            if (!_localScratch.Tuning.IsCreated || _localScratch.Tuning.Length <= 0)
                return;

            if (!TryResolveOceanSeaLevelY(out float seaLevelY))
                return;

            SomaticKinematicsTuningData tuning = _localScratch.Tuning[0];
            tuning.SeaLevelY = seaLevelY;
            _localScratch.Tuning[0] = tuning;
        }

        private bool TryResolveOceanSeaLevelY(out float seaLevelY)
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TrySanitizeOceanRuntimeSeaLevelY(oceanKinematics.SeaLevel, out seaLevelY))
            {
                return true;
            }

            seaLevelY = DefaultSeaLevelY;
            return false;
        }

        private static bool TrySanitizeOceanRuntimeSeaLevelY(float value, out float seaLevelY)
        {
            if (math.isfinite(value) &&
                math.abs(value) <= Hecton8.World.WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                seaLevelY = value;
                return true;
            }

            seaLevelY = DefaultSeaLevelY;
            return false;
        }

        private static float SanitizeRuntimeSeaLevelY(float value, float fallback)
        {
            return TrySanitizeRuntimeSeaLevelY(value, out float seaLevelY)
                ? seaLevelY
                : fallback;
        }

        private static bool TrySanitizeRuntimeSeaLevelY(float value, out float seaLevelY)
        {
            if (math.isfinite(value) &&
                math.abs(value) > 0.0001f &&
                math.abs(value) <= Hecton8.World.WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                seaLevelY = value;
                return true;
            }

            seaLevelY = DefaultSeaLevelY;
            return false;
        }

        private void CacheDataVaultCold()
        {
            if (_registeredHotSwap)
                return;

            BindDataVault(GlobalRegistry.DataVault, null);
        }

        private void BindDataVault(IDataVault currentVault, IDataVault previousVault)
        {
            if (ReferenceEquals(_dataVault, currentVault))
                return;

            CompleteScheduledKinematicsInPostFixedOrShutdown(true);
            ReleaseViews(previousVault ?? _dataVault);
            _dataVault = currentVault;
        }

        private void ResolveColdPaths()
        {
            _projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            _csvOverridePath = Path.Combine(_projectRoot, CsvOverrideFileName);
        }

        private string ResolveProjectPath(string relativePath)
        {
            if (string.IsNullOrEmpty(_projectRoot))
                ResolveColdPaths();
            return Path.GetFullPath(Path.Combine(_projectRoot, relativePath));
        }

        private void ReleaseViews(IDataVault releaseVault = null)
        {
            IDataVault vault = releaseVault ?? _dataVault;
            ReleaseLocalSimulationScratch();
            ReleaseSomaticVaultHandles(vault);
            _stateHandle = default;
            _sphereHandle = default;
            _handHistoryHandle = default;
            _tuningHandle = default;
            _dragLutHandle = default;
            _signalScratchHandle = default;
            _blackBoxHandle = default;
            _blackBoxCursorHandle = default;
            _csvScratchHandle = default;
            _frameReadyForPostFixed = false;
            _kinematicsJobScheduled = false;
            _pendingKinematicsHandle = default;
        }

        private void ReleaseLocalSimulationScratch()
        {
            _localScratch.Dispose();
        }

        private void ReleaseSomaticVaultHandles(IDataVault vault)
        {
            ReleaseSomaticVaultHandle(vault, ref _stateHandle, BufferID.ShinobuSomaticKinematicState);
            ReleaseSomaticVaultHandle(vault, ref _sphereHandle, BufferID.ShinobuSomaticBoundingSphere);
            ReleaseSomaticVaultHandle(vault, ref _handHistoryHandle, BufferID.ShinobuSomaticHandStrokeHistory);
            ReleaseSomaticVaultHandle(vault, ref _tuningHandle, BufferID.ShinobuSomaticTuning);
            ReleaseSomaticVaultHandle(vault, ref _dragLutHandle, BufferID.ShinobuSomaticDragLut);
            ReleaseSomaticVaultHandle(vault, ref _signalScratchHandle, BufferID.ShinobuSomaticSignalScratch);
            ReleaseSomaticVaultHandle(vault, ref _blackBoxHandle, BufferID.ShinobuSomaticBlackBox);
            ReleaseSomaticVaultHandle(vault, ref _blackBoxCursorHandle, BufferID.ShinobuSomaticBlackBoxCursor);
            ReleaseSomaticVaultHandle(vault, ref _csvScratchHandle, BufferID.ShinobuSomaticCsvScratch);
            ClearActiveSomaticWriteVaults();
        }

        private void ClearActiveSomaticWriteVaults()
        {
            _stateWriteVault = null;
            _sphereWriteVault = null;
            _handHistoryWriteVault = null;
            _tuningWriteVault = null;
            _dragLutWriteVault = null;
            _signalScratchWriteVault = null;
            _blackBoxWriteVault = null;
            _blackBoxCursorWriteVault = null;
            _csvScratchWriteVault = null;
        }

        private static void ReleaseSomaticVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsSomaticVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 Forward(Quaternion rotation)
        {
            quaternion q = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
            return math.mul(q, new float3(0f, 0f, 1f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return math.isfinite(lengthSq) && lengthSq > 0.000001f ? value * math.rsqrt(lengthSq) : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFinite(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static uint NextSequence(ref uint sequence)
        {
            unchecked
            {
                sequence++;
                if (sequence == 0u)
                    sequence = 1u;
            }

            return sequence;
        }

        private static float ReadFloat32LittleEndian(ReadOnlySpan<byte> span, int offset)
        {
            if (span.Length < offset + 4)
                return 0f;

            int raw = span[offset] |
                      (span[offset + 1] << 8) |
                      (span[offset + 2] << 16) |
                      (span[offset + 3] << 24);
            return BitConverter.Int32BitsToSingle(raw);
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct SomaticBlackBoxDumpHeader
        {
            [FieldOffset(0)] public uint Magic;
            [FieldOffset(4)] public uint Version;
            [FieldOffset(8)] public uint EntryCount;
            [FieldOffset(12)] public uint EntryBytes;
            [FieldOffset(16)] public uint Cursor;
            [FieldOffset(20)] public uint Frame;
            [FieldOffset(24)] public ulong Reserved;
        }
    }
}
