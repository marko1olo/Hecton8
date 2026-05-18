using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics.Vehicles
{
    public static class SubmarineDynamicsConstants
    {
        public const int MaxVehicles = 16;
        public const int DragLutSamples = 16;
        public const int BlackBoxFrames = 300;
        public const int CacheLineBytes = 64;
        public const int IntegratorBatchSize = 4;
        public const float Gravity = 9.80665f;
        public const uint SourceHashMock = 0x4B425553u; // SUBK
        public const uint SourceHashLegacy = 0x4F485355u; // USHO
        public const uint SourceHashCsv = 0x43535653u; // SVSC
        public const uint StateFlagInitialized = 1u << 0;
        public const uint StateFlagFatalNan = 1u << 1;
        public const uint StateFlagGyroSuppressed = 1u << 2;
        public const uint ForceFlagImpact = 1u << 0;
        public const uint ForceFlagImpactNormalLocal = 1u << 1;
        public const byte ConfigFlagThermalDilation = 1 << 0;
        public const byte ConfigFlagLegacyProfile = 1 << 1;
        public const byte ConfigFlagCsvOverride = 1 << 2;
    }

    /// <summary>
    /// Deterministic fallback fluid-density source for isolated submarine tests.
    /// </summary>
    public static class MockFluidDensityGenerator
    {
        public const float DefaultSeawaterDensityKgPerM3 = 1027f;
        private const float MinDensityKgPerM3 = 850f;
        private const float MaxDensityKgPerM3 = 1250f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveBaseDensityKgPerM3(float densityMultiplier)
        {
            float safeMultiplier = math.isfinite(densityMultiplier) ? math.clamp(densityMultiplier, 0.75f, 1.35f) : 1f;
            return math.clamp(DefaultSeawaterDensityKgPerM3 * safeMultiplier, MinDensityKgPerM3, MaxDensityKgPerM3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SampleDensityKgPerM3(float depthMeters, float baseDensityKgPerM3, uint frame, byte hardwareTier)
        {
            float depth = math.isfinite(depthMeters) ? math.clamp(depthMeters, 0f, 1200f) : 0f;
            float baseDensity = math.isfinite(baseDensityKgPerM3)
                ? math.clamp(baseDensityKgPerM3, MinDensityKgPerM3, MaxDensityKgPerM3)
                : DefaultSeawaterDensityKgPerM3;
            float compressionBias = depth * 0.0042f;
            if (hardwareTier == 0)
                return math.clamp(baseDensity + compressionBias, MinDensityKgPerM3, MaxDensityKgPerM3);

            uint phase = (frame * 1103515245u) + 12345u;
            float microLayerBias = (((phase >> 8) & 1023u) * (1f / 1023f) - 0.5f) * 0.55f;
            return math.clamp(baseDensity + compressionBias + microLayerBias, MinDensityKgPerM3, MaxDensityKgPerM3);
        }
    }

    /// <summary>
    /// Hot submarine pose and velocity state. Size: 192 bytes, exactly 3 L1 cache lines.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 192)]
    public struct SubmarineKinematicState
    {
        public double3 Aup;
        public quaternion Rotation;
        public float3 LocalPosition;
        public float3 LinearVelocity;
        public float3 AngularVelocity;
        public float3 CenterOfMassLocal;
        public float3 CenterOfBuoyancyLocal;
        public float3 InertiaTensor;
        public float TotalMassKg;
        public float BallastRatio01;
        public float GyroDisabledSeconds;
        public uint Flags;
        public uint TelemetryCursor;
        public uint EntityId;
        public uint ShiftFrameId;
        public byte MathLod;
        public byte HardwareTier;
        private ushort _pad0;
        private long _pad1;
        private long _pad2;
        private long _pad3;
        private long _pad4;
        private long _pad5;
        private long _pad6;
    }

    /// <summary>Per-frame control intent for one submarine. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct SubmarineKinematicControl
    {
        public float3 ThrustLocal;
        public float3 TorqueLocal;
        public float TargetDepthMeters;
        public float Throttle01;
        public float BallastCommand01;
        public float FloodWaterMassKg;
        public float CargoMassKg;
        public float ExternalImpulseMagnitude;
        public float3 ExternalImpulseLocal;
        public uint Flags;
    }

    /// <summary>
    /// Mass and local center data. Size: 128 bytes, exactly 2 L1 cache lines.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct SubmarineMassProperties
    {
        public double3 PivotAup;
        public float3 BaseCenterOfMassLocal;
        public float3 FloodCenterLocal;
        public float3 CargoCenterLocal;
        public float3 CenterOfMassLocal;
        public float3 CenterOfBuoyancyLocal;
        public float BaseMassKg;
        public float FloodMassKg;
        public float CargoMassKg;
        private long _pad0;
        private long _pad1;
        private long _pad2;
        private long _pad3;
    }

    /// <summary>Ballast PID and slosh oscillator state. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct SubmarinePidState
    {
        public float Integral;
        public float PreviousError;
        public float LastOutput;
        public float LastDerivative;
        public float LastTarget;
        public float SloshPosition;
        public float SloshVelocity;
        public float LowLodHoldSeconds;
        public uint Frame;
        public byte MathLod;
        public byte Flags;
        private ushort _pad0;
        private int _pad1;
        private long _pad2;
        private long _pad3;
    }

    /// <summary>Last solved forces for gameplay and visual consumers. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct SubmarineForceAccumulator
    {
        public float3 LinearForceWorld;
        public float3 TorqueWorld;
        public float3 LastThrustWorld;
        public float3 LastDragWorld;
        public float3 LastBuoyancyWorld;
        public float3 ImpactPointLocal;
        public float3 ImpactNormalWorld;
        public float CavitationIndex;
        public float ImpactMagnitude;
        public uint Flags;
        public uint Frame;
        private float4 _pad0;
        private float3 _pad1;
    }

    /// <summary>Designer-tunable constants mirrored into the Vault. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct SubmarineKinematicConfig
    {
        public double3 LocalOriginAup;
        public float BaseMassKg;
        public float HullVolumeM3;
        public float FluidDensityKgPerM3;
        public float DragScale;
        public float PidP;
        public float PidI;
        public float PidD;
        public float PidIntegralLimit;
        public float GyroStrength;
        public float GyroDamping;
        public float MaxThrustN;
        public float MaxTorqueNm;
        public float BallastLiftN;
        public float CavitationDepthMeters;
        public float CavitationThreshold;
        public float SloshSpring;
        public float SloshDamping;
        public float FloodComGain;
        public float CargoForwardMeters;
        public float TickDilationPressure01;
        public float3 MockFloodLocal;
        public uint SourceHash;
        public byte HardwareTier;
        public byte Flags;
        private ushort _pad0;
        private int _pad1;
    }

    /// <summary>300-frame blackbox telemetry entry. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct SubmarineKinematicTelemetry
    {
        public double3 Aup;
        public float3 LinearVelocity;
        public float3 AngularVelocity;
        public float3 CenterOfMassLocal;
        public float3 CenterOfBuoyancyLocal;
        public float3 LocalPosition;
        public uint Frame;
        public uint Flags;
        public float TotalMassKg;
        public float BallastRatio01;
        public float CavitationIndex;
        public float EstimatedCostUs;
        public uint StateHash;
        private uint _pad0;
        private long _pad1;
    }

    /// <summary>Fallback flood signal used when the real flood domain is unavailable. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public partial struct MockFloodSignal
    {
        public float3 LocalCompartment;
        public float WaterMassKg;
        public float FillRatio01;
        public uint Frame;
        public byte Flags;
        private byte _pad0;
        private ushort _pad1;
        private long _pad2;
        private long _pad3;
        private long _pad4;
        private uint _pad5;
    }

    /// <summary>Fallback impact signal used when the real hull domain is unavailable. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public partial struct MockImpactSignal
    {
        public float3 LocalPoint;
        public float3 NormalWorld;
        public float Magnitude;
        public float DepthMeters;
        public uint Frame;
        public byte TraumaLevel;
        private byte _pad0;
        private ushort _pad1;
        private long _pad2;
        private long _pad3;
        private long _pad4;
    }

    /// <summary>Cavitation cue emitted by the dynamics job. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public partial struct CavitationAcousticSignal
    {
        public float3 LocalPosition;
        public float Intensity01;
        public float FrequencyHz;
        public uint Frame;
        public byte Flags;
        private byte _pad0;
        private ushort _pad1;
        private uint _pad2;
        private long _pad3;
        private long _pad4;
        private long _pad5;
        private long _pad6;
    }

    public static unsafe class SubmarineKinematicAccess
    {
        public static ref SubmarineKinematicState GetStateRef(
            IDataVault vault,
            ref VaultBufferHandle<SubmarineKinematicState> handle,
            int index)
        {
            void* pointer = handle.ResolvePointer(vault);
            if (pointer == null || (uint)index >= (uint)handle.Length)
                FatalMemoryException.ThrowStaleVaultHandle();

            Hint.Assume(pointer != null);
            Hint.Assume(index >= 0);
            Hint.Assume(index < handle.Length);
            return ref UnsafeUtility.ArrayElementAsRef<SubmarineKinematicState>(pointer, index);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct MockFloodSignalSeederJob : IJob
    {
        public NativeQueue<MockFloodSignal>.ParallelWriter FloodWriter;
        public uint Frame;
        public uint Seed;
        public float3 LocalCompartment;
        public float MassKg;

        public void Execute()
        {
            uint hash = Seed ^ (Frame * 747796405u);
            hash = (hash ^ (hash >> 16)) * 2246822519u;
            hash ^= hash >> 13;
            if ((hash & 31u) != 0u)
                return;

            MockFloodSignal signal = default;
            signal.LocalCompartment = LocalCompartment;
            signal.WaterMassKg = math.max(0f, MassKg);
            signal.FillRatio01 = math.saturate(signal.WaterMassKg / 4000f);
            signal.Frame = Frame;
            signal.Flags = 1;
            FloodWriter.Enqueue(signal);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct Submarine6DIntegratorJob : IJobParallelFor
    {
        public NativeArray<SubmarineKinematicState> States;
        public NativeArray<SubmarineKinematicControl> Controls;
        public NativeArray<SubmarinePidState> PidStates;
        public NativeArray<SubmarineMassProperties> MassProperties;
        public NativeArray<SubmarineForceAccumulator> Forces;
        public NativeArray<SubmarineKinematicTelemetry> Telemetry;
        [ReadOnly] public NativeArray<SubmarineKinematicConfig> Configs;
        [ReadOnly] public NativeArray<float> DragLut;
        public NativeQueue<CavitationAcousticSignal>.ParallelWriter CavitationWriter;
        public float FixedDeltaTime;
        public uint Frame;
        public int VehicleCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)VehicleCount ||
                (uint)index >= (uint)States.Length ||
                (uint)index >= (uint)Controls.Length ||
                (uint)index >= (uint)PidStates.Length ||
                (uint)index >= (uint)MassProperties.Length ||
                (uint)index >= (uint)Forces.Length ||
                Configs.Length == 0 ||
                DragLut.Length == 0)
            {
                return;
            }

            SubmarineKinematicState state = States[index];
            SubmarineKinematicControl control = Controls[index];
            SubmarinePidState pid = PidStates[index];
            SubmarineMassProperties mass = MassProperties[index];
            SubmarineForceAccumulator force = Forces[index];
            SubmarineKinematicConfig config = Configs[0];

            float dt = math.clamp(FixedDeltaTime, 0.001f, 0.05f);
            bool lowMathRequested = (config.Flags & SubmarineDynamicsConstants.ConfigFlagThermalDilation) != 0;
            pid.LowLodHoldSeconds = lowMathRequested
                ? math.max(pid.LowLodHoldSeconds, 2f)
                : math.max(0f, pid.LowLodHoldSeconds - dt);
            bool lowMathLod = pid.LowLodHoldSeconds > 0f;
            bool runSlowSolvers = !lowMathLod || (Frame & 3u) == 0u;

            if ((state.Flags & SubmarineDynamicsConstants.StateFlagInitialized) == 0u)
                InitializeState(ref state, in config, index);

            float3 localPosition = ToLocal(in state.Aup, in config);
            state.LocalPosition = localPosition;
            state.HardwareTier = config.HardwareTier;
            state.MathLod = lowMathLod ? (byte)0 : (byte)1;

            state.LinearVelocity = SafeFinite(state.LinearVelocity, float3.zero);
            state.AngularVelocity = SafeFinite(state.AngularVelocity, float3.zero);
            state.InertiaTensor = SafePositive(state.InertiaTensor, new float3(1f));
            mass.BaseCenterOfMassLocal = SafeFinite(mass.BaseCenterOfMassLocal, float3.zero);
            mass.FloodCenterLocal = SafeFinite(mass.FloodCenterLocal, config.MockFloodLocal);
            mass.CargoCenterLocal = SafeFinite(mass.CargoCenterLocal, new float3(0f, -0.2f, config.CargoForwardMeters));
            mass.CenterOfMassLocal = SafeFinite(mass.CenterOfMassLocal, float3.zero);
            mass.CenterOfBuoyancyLocal = SafeFinite(mass.CenterOfBuoyancyLocal, new float3(0f, 0.7f, 0f));

            quaternion rotation = NormalizeSafe(state.Rotation);
            float depthMeters = math.max(0f, -localPosition.y);

            float baseMass = SafePositive(config.BaseMassKg, 1f);
            float floodMass = SafeNonNegative(mass.FloodMassKg);
            float cargoMass = SafeNonNegative(mass.CargoMassKg);
            mass.BaseMassKg = baseMass;
            mass.FloodMassKg = floodMass;
            mass.CargoMassKg = cargoMass;
            float totalMass = math.max(1f, baseMass + floodMass + cargoMass);
            if (runSlowSolvers)
            {
                UpdateSlosh(ref pid, ref mass, in state, in config, dt);
                float3 weightedCom = (mass.BaseCenterOfMassLocal * baseMass) +
                                     (mass.FloodCenterLocal * floodMass) +
                                     (mass.CargoCenterLocal * cargoMass);
                mass.CenterOfMassLocal = weightedCom / totalMass;
            }

            float3 centerOfMassLocal = mass.CenterOfMassLocal;
            float3 centerOfBuoyancyLocal = mass.CenterOfBuoyancyLocal;
            state.CenterOfMassLocal = centerOfMassLocal;
            state.CenterOfBuoyancyLocal = centerOfBuoyancyLocal;
            state.TotalMassKg = totalMass;
            state.BallastRatio01 = math.saturate(control.BallastCommand01);

            float pidOutput = pid.LastOutput;
            if (runSlowSolvers)
                pidOutput = SolveDepthPid(ref pid, depthMeters, control.TargetDepthMeters, in config, dt);
            pid.Frame = Frame;

            float3 forward = math.mul(rotation, new float3(0f, 0f, 1f));
            float3 throttleVector = math.lengthsq(control.ThrustLocal) > 0.0001f
                ? math.mul(rotation, math.normalizesafe(control.ThrustLocal, new float3(0f, 0f, 1f)))
                : forward;

            float throttle01 = math.saturate(control.Throttle01);
            float3 thrustWorld = throttleVector * (config.MaxThrustN * throttle01);
            float speedSq = math.lengthsq(state.LinearVelocity);
            speedSq = math.isfinite(speedSq) ? speedSq : 0f;
            float cavitationIndex = ComputeCavitation(depthMeters, throttle01, speedSq, in config);
            if (cavitationIndex < config.CavitationThreshold)
            {
                float stutter = 0.25f + 0.75f * Hash01(Frame + (uint)index * 101u);
                thrustWorld *= stutter;
                CavitationAcousticSignal signal = default;
                signal.LocalPosition = localPosition;
                signal.Intensity01 = math.saturate((config.CavitationThreshold - cavitationIndex) * 4f);
                signal.FrequencyHz = 80f + 420f * signal.Intensity01;
                signal.Frame = Frame;
                signal.Flags = 1;
                CavitationWriter.Enqueue(signal);
            }

            float dragCoefficient = SampleDragLut(speedSq, in DragLut) * SafePositive(config.DragScale, 0.01f);
            float3 dragWorld = -math.normalizesafe(state.LinearVelocity) * speedSq * dragCoefficient;

            float targetDepth = math.max(1f, control.TargetDepthMeters);
            float depthRatio = math.saturate((depthMeters + 1f) / (targetDepth + 1f));
            float buoyancyEase = depthRatio * depthRatio * (3f - (2f * depthRatio));
            float hullVolume = SafePositive(config.HullVolumeM3, 1f);
            float fluidDensity = MockFluidDensityGenerator.SampleDensityKgPerM3(
                depthMeters,
                config.FluidDensityKgPerM3,
                Frame,
                state.HardwareTier);
            float ballastLift = SafeNonNegative(config.BallastLiftN);
            float buoyancyN = hullVolume * fluidDensity * SubmarineDynamicsConstants.Gravity * buoyancyEase;
            buoyancyN += math.lerp(-ballastLift, ballastLift, state.BallastRatio01) + pidOutput;
            float3 buoyancyWorld = new float3(0f, buoyancyN, 0f);
            float3 gravityWorld = new float3(0f, -SubmarineDynamicsConstants.Gravity * totalMass, 0f);

            float3 torqueWorld = math.mul(rotation, control.TorqueLocal * config.MaxTorqueNm);
            float3 comWorld = math.mul(rotation, centerOfMassLocal);
            float3 cobWorld = math.mul(rotation, centerOfBuoyancyLocal);
            torqueWorld += math.cross(comWorld, gravityWorld) + math.cross(cobWorld, buoyancyWorld);

            if (state.GyroDisabledSeconds > 0f)
            {
                state.GyroDisabledSeconds = math.max(0f, state.GyroDisabledSeconds - dt);
                state.Flags |= SubmarineDynamicsConstants.StateFlagGyroSuppressed;
            }
            else
            {
                state.Flags &= ~SubmarineDynamicsConstants.StateFlagGyroSuppressed;
                float3 up = math.mul(rotation, new float3(0f, 1f, 0f));
                torqueWorld += math.cross(up, new float3(0f, 1f, 0f)) * config.GyroStrength;
                torqueWorld += -state.AngularVelocity * config.GyroDamping;
            }

            if ((force.Flags & SubmarineDynamicsConstants.ForceFlagImpact) != 0u && force.ImpactMagnitude > 0f)
            {
                float3 impactNormal = math.normalizesafe(force.ImpactNormalWorld, -forward);
                if ((force.Flags & SubmarineDynamicsConstants.ForceFlagImpactNormalLocal) != 0u)
                    impactNormal = math.normalizesafe(math.mul(rotation, impactNormal), -forward);

                float impulse = force.ImpactMagnitude;
                state.LinearVelocity += impactNormal * (impulse / totalMass);
                float3 angularImpulse = math.cross(force.ImpactPointLocal - centerOfMassLocal, impactNormal * impulse);
                state.AngularVelocity += angularImpulse / math.max(new float3(1f), state.InertiaTensor);
                if (impulse > 45000f)
                    state.GyroDisabledSeconds = 2f;
            }

            float3 totalForce = thrustWorld + dragWorld + buoyancyWorld + gravityWorld;
            state.LinearVelocity += (totalForce / totalMass) * dt;
            state.LinearVelocity = math.clamp(state.LinearVelocity, new float3(-90f), new float3(90f));
            localPosition += state.LinearVelocity * dt;
            state.Aup = SafeAup(config.LocalOriginAup) + new double3(localPosition);
            state.LocalPosition = localPosition;

            float3 inertia = math.max(new float3(1f), state.InertiaTensor);
            state.AngularVelocity += (torqueWorld / inertia) * dt;
            state.AngularVelocity = math.clamp(state.AngularVelocity, new float3(-2.8f), new float3(2.8f));
            float angularSpeed = math.length(state.AngularVelocity);
            if (angularSpeed > 0.0001f)
            {
                quaternion deltaRotation = quaternion.AxisAngle(state.AngularVelocity / angularSpeed, angularSpeed * dt);
                rotation = math.normalize(math.mul(deltaRotation, rotation));
            }

            state.Rotation = rotation;
            state.InertiaTensor = ResolveInertiaTensor(totalMass, centerOfMassLocal, mass.FloodMassKg, in config);
            state.ShiftFrameId = Frame;
            state.Flags |= SubmarineDynamicsConstants.StateFlagInitialized;

            bool finite = IsFinite(state) &&
                          IsFinite(mass.CenterOfMassLocal) &&
                          IsFinite(totalForce) &&
                          IsFinite(torqueWorld) &&
                          IsFinite(thrustWorld) &&
                          IsFinite(dragWorld) &&
                          IsFinite(buoyancyWorld);
            if (!finite)
            {
                state.Flags |= SubmarineDynamicsConstants.StateFlagFatalNan;
                state.LocalPosition = float3.zero;
                state.Aup = SafeAup(config.LocalOriginAup);
                state.LinearVelocity = float3.zero;
                state.AngularVelocity = float3.zero;
                state.Rotation = quaternion.identity;
                state.CenterOfMassLocal = float3.zero;
                state.CenterOfBuoyancyLocal = new float3(0f, 0.7f, 0f);
                state.InertiaTensor = new float3(1f);
                totalForce = float3.zero;
                torqueWorld = float3.zero;
                thrustWorld = float3.zero;
                dragWorld = float3.zero;
                buoyancyWorld = float3.zero;
                cavitationIndex = 1f;
            }

            force.LinearForceWorld = totalForce;
            force.TorqueWorld = torqueWorld;
            force.LastThrustWorld = thrustWorld;
            force.LastDragWorld = dragWorld;
            force.LastBuoyancyWorld = buoyancyWorld;
            force.CavitationIndex = cavitationIndex;
            force.ImpactMagnitude = 0f;
            force.Flags &= ~(SubmarineDynamicsConstants.ForceFlagImpact | SubmarineDynamicsConstants.ForceFlagImpactNormalLocal);
            force.Frame = Frame;

            WriteTelemetry(index, ref state, in force, ref Telemetry);

            States[index] = state;
            Controls[index] = control;
            PidStates[index] = pid;
            MassProperties[index] = mass;
            Forces[index] = force;
        }

        private static void InitializeState(ref SubmarineKinematicState state, in SubmarineKinematicConfig config, int index)
        {
            state.Aup = SafeAup(config.LocalOriginAup);
            state.Rotation = quaternion.identity;
            state.LocalPosition = float3.zero;
            state.LinearVelocity = float3.zero;
            state.AngularVelocity = float3.zero;
            state.CenterOfMassLocal = float3.zero;
            state.CenterOfBuoyancyLocal = new float3(0f, 0.7f, 0f);
            state.TotalMassKg = math.max(1f, config.BaseMassKg);
            state.InertiaTensor = ResolveInertiaTensor(state.TotalMassKg, float3.zero, 0f, in config);
            state.EntityId = (uint)index;
        }

        private static float3 ToLocal(in double3 aup, in SubmarineKinematicConfig config)
        {
            double3 delta = SafeAup(aup) - SafeAup(config.LocalOriginAup);
            float3 local = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            return math.all(math.isfinite(local)) ? local : float3.zero;
        }

        private static float SolveDepthPid(
            ref SubmarinePidState pid,
            float currentDepth,
            float targetDepth,
            in SubmarineKinematicConfig config,
            float dt)
        {
            float error = math.max(0f, targetDepth) - currentDepth;
            float integralLimit = SafePositive(config.PidIntegralLimit, 1f);
            pid.Integral = math.clamp(pid.Integral + (error * dt), -integralLimit, integralLimit);
            float derivative = (error - pid.PreviousError) / math.max(0.001f, dt);
            pid.PreviousError = error;
            pid.LastDerivative = derivative;
            pid.LastOutput = (SafeNonNegative(config.PidP) * error) +
                             (SafeNonNegative(config.PidI) * pid.Integral) +
                             (SafeNonNegative(config.PidD) * derivative);
            pid.LastTarget = targetDepth;
            return pid.LastOutput;
        }

        private static void UpdateSlosh(
            ref SubmarinePidState pid,
            ref SubmarineMassProperties mass,
            in SubmarineKinematicState state,
            in SubmarineKinematicConfig config,
            float dt)
        {
            if (mass.FloodMassKg <= 0.1f)
            {
                pid.SloshPosition *= 0.9f;
                pid.SloshVelocity *= 0.5f;
                return;
            }

            float rollVelocity = state.AngularVelocity.z;
            float acceleration = (-SafeNonNegative(config.SloshSpring) * pid.SloshPosition) -
                                 (SafeNonNegative(config.SloshDamping) * pid.SloshVelocity) +
                                 (rollVelocity * SafeFinite(config.FloodComGain, 0f));
            pid.SloshVelocity += acceleration * dt;
            pid.SloshVelocity = math.clamp(pid.SloshVelocity, -2.5f, 2.5f);
            pid.SloshPosition = math.clamp(pid.SloshPosition + (pid.SloshVelocity * dt), -1.4f, 1.4f);
            mass.FloodCenterLocal.x = pid.SloshPosition;
        }

        private static float SampleDragLut(float speedSq, in NativeArray<float> dragLut)
        {
            float normalized = math.saturate(speedSq * 0.0025f);
            float sample = normalized * (dragLut.Length - 1);
            int index = (int)math.floor(sample);
            int next = math.min(index + 1, dragLut.Length - 1);
            float t = sample - index;
            return math.lerp(dragLut[index], dragLut[next], t);
        }

        private static float ComputeCavitation(float depthMeters, float throttle01, float speedSq, in SubmarineKinematicConfig config)
        {
            float depthSafety = math.saturate(depthMeters / math.max(0.1f, SafePositive(config.CavitationDepthMeters, 0.1f)));
            float speedPenalty = math.saturate(speedSq * 0.01f);
            return depthSafety + (1f - throttle01) - (speedPenalty * throttle01 * 0.35f);
        }

        private static float3 ResolveInertiaTensor(float totalMass, float3 centerOfMassLocal, float floodMass, in SubmarineKinematicConfig config)
        {
            float length = 8f;
            float radius = 1.8f;
            float safeMass = SafePositive(totalMass, 1f);
            float ix = 0.5f * safeMass * radius * radius;
            float iz = (safeMass * ((3f * radius * radius) + (length * length))) / 12f;
            float pitchBias = 1f + (math.abs(centerOfMassLocal.z) * 0.12f) + (SafeNonNegative(floodMass) / math.max(1f, SafePositive(config.BaseMassKg, 1f)));
            return new float3(ix, iz * pitchBias, iz);
        }

        private static void WriteTelemetry(
            int vehicleIndex,
            ref SubmarineKinematicState state,
            in SubmarineForceAccumulator force,
            ref NativeArray<SubmarineKinematicTelemetry> telemetry)
        {
            int baseIndex = vehicleIndex * SubmarineDynamicsConstants.BlackBoxFrames;
            if ((uint)baseIndex >= (uint)telemetry.Length)
                return;

            uint cursor = state.TelemetryCursor;
            int local = (int)(cursor % SubmarineDynamicsConstants.BlackBoxFrames);
            int index = baseIndex + local;
            if ((uint)index >= (uint)telemetry.Length)
                return;

            SubmarineKinematicTelemetry entry = default;
            entry.Aup = state.Aup;
            entry.LinearVelocity = state.LinearVelocity;
            entry.AngularVelocity = state.AngularVelocity;
            entry.CenterOfMassLocal = state.CenterOfMassLocal;
            entry.CenterOfBuoyancyLocal = state.CenterOfBuoyancyLocal;
            entry.LocalPosition = state.LocalPosition;
            entry.Frame = state.ShiftFrameId;
            entry.Flags = state.Flags;
            entry.TotalMassKg = state.TotalMassKg;
            entry.BallastRatio01 = state.BallastRatio01;
            entry.CavitationIndex = force.CavitationIndex;
            entry.EstimatedCostUs = 0f;
            entry.StateHash = HashState(in state);
            telemetry[index] = entry;

            state.TelemetryCursor = cursor + 1u;
        }

        private static uint HashState(in SubmarineKinematicState state)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, math.asuint(state.LinearVelocity.x));
            hash = Mix(hash, math.asuint(state.LinearVelocity.y));
            hash = Mix(hash, math.asuint(state.LinearVelocity.z));
            hash = Mix(hash, math.asuint(state.AngularVelocity.x));
            hash = Mix(hash, math.asuint(state.AngularVelocity.y));
            hash = Mix(hash, math.asuint(state.AngularVelocity.z));
            hash = Mix(hash, state.Flags);
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }

        private static bool IsFinite(in SubmarineKinematicState state)
        {
            return math.all(math.isfinite(state.LocalPosition)) &&
                   math.all(math.isfinite(state.Aup)) &&
                   math.all(math.isfinite(state.LinearVelocity)) &&
                   math.all(math.isfinite(state.AngularVelocity)) &&
                   math.all(math.isfinite(state.CenterOfMassLocal)) &&
                   math.all(math.isfinite(state.CenterOfBuoyancyLocal)) &&
                   math.all(math.isfinite(state.InertiaTensor)) &&
                   math.isfinite(state.TotalMassKg) &&
                   IsFinite(state.Rotation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SafeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SafeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SafePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SafeFinite(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SafePositive(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? math.max(value, new float3(0.0001f)) : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 SafeAup(double3 value)
        {
            return math.all(math.isfinite(value)) ? value : double3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static quaternion NormalizeSafe(quaternion value)
        {
            if (!math.all(math.isfinite(value.value)))
                return quaternion.identity;

            float lenSq = math.lengthsq(value.value);
            return lenSq > 0.000001f ? new quaternion(value.value * math.rsqrt(lenSq)) : quaternion.identity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(quaternion value)
        {
            return math.all(math.isfinite(value.value));
        }
    }
}
