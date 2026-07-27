using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
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
        public const int GyroTuningBytes = 32;
        public const int GyroErrorBytes = 64;
        public const int GyroForcePacketBytes = 128;
        public const int GyroTelemetryBytes = 64;
        public const int GyroVisualStateBytes = 64;
        public const int GyroProfileBytes = 64;
        public const int GyroCounterBytes = 64;
        public const float Gravity = 9.80665f;
        public const uint SourceHashMock = 0x4B425553u; // SUBK
        public const uint SourceHashLegacy = 0x4F485355u; // USHO
        public const uint SourceHashCsv = 0x43535653u; // SVSC
        public const uint SourceHashAddedMass = 0x414D3235u; // AM25
        public const uint SourceHashGyro = 0x47333332u; // G332
        public const uint StateFlagInitialized = 1u << 0;
        public const uint StateFlagFatalNan = 1u << 1;
        public const uint StateFlagGyroSuppressed = 1u << 2;
        public const uint StateFlagSignalDrop = 1u << 3;
        public const uint HydroFlagTensorFallback = 1u << 0;
        public const uint HydroFlagFullTensorBlend = 1u << 1;
        public const uint HydroFlagFloodMassInjected = 1u << 2;
        public const uint ForceFlagImpact = 1u << 0;
        public const uint ForceFlagImpactNormalLocal = 1u << 1;
        public const uint ForceFlagGyroCorrection = 1u << 2;
        public const uint GyroFlagAutoLevelEnabled = 1u << 0;
        public const uint GyroFlagPacketQueued = 1u << 1;
        public const uint GyroFlagTensorFallback = 1u << 2;
        public const uint GyroFlagTorqueClamped = 1u << 3;
        public const uint GyroFlagSuppressed = 1u << 4;
        public const uint GyroFlagNonFinite = 1u << 31;
        public const byte ConfigFlagThermalDilation = 1 << 0;
        public const byte ConfigFlagLegacyProfile = 1 << 1;
        public const byte ConfigFlagCsvOverride = 1 << 2;
        public const float AuthoritativeQualityWeight = 1f;
    }

    /// <summary>
    /// Deterministic fallback fluid-density source for isolated submarine tests.
    /// </summary>
    public static class MockFluidDensityGenerator
    {
        public const float DefaultSeawaterDensityKgPerM3 = 1027f;
        private const float MinDensityKgPerM3 = 850f;
        private const float MaxDensityKgPerM3 = 1250f;
        // Two-metre stratification bands, so descending crosses discrete density layers instead of
        // resampling noise every centimetre. Amplitude matches the previous jitter so hull feel is
        // preserved; only what seeds it changes.
        private const float MicroLayerBandsPerMeter = 0.5f;
        private const float MicroLayerAmplitudeKgPerM3 = 0.55f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveBaseDensityKgPerM3(float densityMultiplier)
        {
            float safeMultiplier = math.isfinite(densityMultiplier) ? math.clamp(densityMultiplier, 0.75f, 1.35f) : 1f;
            return math.clamp(DefaultSeawaterDensityKgPerM3 * safeMultiplier, MinDensityKgPerM3, MaxDensityKgPerM3);
        }

        /// <summary>
        /// Ambient fluid density at depth: base density, depth compression, and a micro-layer
        /// stratification bias.
        ///
        /// The stratification is seeded from a quantised depth band, NOT the frame counter, and its
        /// amplitude is NOT scaled by GlobalQualityWeight. Both of those fed pseudo-random jitter
        /// directly into `buoyancyN = hullVolume * fluidDensity * Gravity * buoyancyEase`, so the
        /// buoyant force on the submarine varied with the frame number and differed between quality
        /// tiers. GlobalQualityWeight must not alter gameplay truth or deterministic state ownership,
        /// and a frame-seeded force is not replay- or rollback-safe.
        ///
        /// Seeding on depth keeps the intended effect - density layers the hull can feel on descent -
        /// while making it a pure deterministic function of position, identical on every tier and on
        /// every replay. `frame` and `globalQualityWeight` are retained so the public signature and
        /// all call sites stay unchanged; they deliberately no longer influence physics truth.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SampleDensityKgPerM3(float depthMeters, float baseDensityKgPerM3, uint frame, float globalQualityWeight)
        {
            _ = frame;
            _ = globalQualityWeight;
            float depth = math.isfinite(depthMeters) ? math.clamp(depthMeters, 0f, 1200f) : 0f;
            float baseDensity = math.isfinite(baseDensityKgPerM3)
                ? math.clamp(baseDensityKgPerM3, MinDensityKgPerM3, MaxDensityKgPerM3)
                : DefaultSeawaterDensityKgPerM3;
            float compressionBias = depth * 0.0042f;
            uint depthBand = (uint)(depth * MicroLayerBandsPerMeter);
            uint phase = (depthBand * 1103515245u) + 12345u;
            float microLayerBias = (((phase >> 8) & 1023u) * (1f / 1023f) - 0.5f) * MicroLayerAmplitudeKgPerM3;
            return math.clamp(baseDensity + compressionBias + microLayerBias, MinDensityKgPerM3, MaxDensityKgPerM3);
        }
    }

    /// <summary>
    /// Hot submarine pose and velocity state. Size: 192 bytes, exactly 3 L1 cache lines.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    public struct SubmarineKinematicState
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public quaternion Rotation;
        [FieldOffset(40)] public float3 LocalPosition;
        [FieldOffset(52)] public float3 LinearVelocity;
        [FieldOffset(64)] public float3 AngularVelocity;
        [FieldOffset(76)] public float3 CenterOfMassLocal;
        [FieldOffset(88)] public float3 CenterOfBuoyancyLocal;
        [FieldOffset(100)] public float3 InertiaTensor;
        [FieldOffset(112)] public float TotalMassKg;
        [FieldOffset(116)] public float BallastRatio01;
        [FieldOffset(120)] public float GyroDisabledSeconds;
        [FieldOffset(124)] public uint Flags;
        [FieldOffset(128)] public uint TelemetryCursor;
        [FieldOffset(132)] public uint EntityId;
        [FieldOffset(136)] public uint ShiftFrameId;
        [FieldOffset(140)] public byte MathLod;
        [FieldOffset(141)] public byte QualityWeightByte;
        [FieldOffset(142)] private ushort _pad0;
        [FieldOffset(144)] private ulong _pad1;
        [FieldOffset(152)] private ulong _pad2;
        [FieldOffset(160)] private ulong _pad3;
        [FieldOffset(168)] private ulong _pad4;
        [FieldOffset(176)] private ulong _pad5;
        [FieldOffset(184)] private ulong _pad6;
    }

    /// <summary>Per-frame control intent for one submarine. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SubmarineKinematicControl
    {
        [FieldOffset(0)] public float3 ThrustLocal;
        [FieldOffset(12)] public float3 TorqueLocal;
        [FieldOffset(24)] public float TargetDepthMeters;
        [FieldOffset(28)] public float Throttle01;
        [FieldOffset(32)] public float BallastCommand01;
        [FieldOffset(36)] public float FloodWaterMassKg;
        [FieldOffset(40)] public float CargoMassKg;
        [FieldOffset(44)] public float ExternalImpulseMagnitude;
        [FieldOffset(48)] public float3 ExternalImpulseLocal;
        [FieldOffset(60)] public uint Flags;
    }

    /// <summary>
    /// Mass and local center data. Size: 128 bytes, exactly 2 L1 cache lines.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct SubmarineMassProperties
    {
        [FieldOffset(0)] public double3 PivotAup;
        [FieldOffset(24)] public float3 BaseCenterOfMassLocal;
        [FieldOffset(36)] public float3 FloodCenterLocal;
        [FieldOffset(48)] public float3 CargoCenterLocal;
        [FieldOffset(60)] public float3 CenterOfMassLocal;
        [FieldOffset(72)] public float3 CenterOfBuoyancyLocal;
        [FieldOffset(84)] public float BaseMassKg;
        [FieldOffset(88)] public float FloodMassKg;
        [FieldOffset(92)] public float CargoMassKg;
        [FieldOffset(96)] private ulong _pad0;
        [FieldOffset(104)] private ulong _pad1;
        [FieldOffset(112)] private ulong _pad2;
        [FieldOffset(120)] private ulong _pad3;
    }

    /// <summary>Ballast PID and slosh oscillator state. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SubmarinePidState
    {
        [FieldOffset(0)] public float Integral;
        [FieldOffset(4)] public float PreviousError;
        [FieldOffset(8)] public float LastOutput;
        [FieldOffset(12)] public float LastDerivative;
        [FieldOffset(16)] public float LastTarget;
        [FieldOffset(20)] public float SloshPosition;
        [FieldOffset(24)] public float SloshVelocity;
        [FieldOffset(28)] public float LowLodHoldSeconds;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public byte MathLod;
        [FieldOffset(37)] public byte Flags;
        [FieldOffset(38)] private ushort _pad0;
        [FieldOffset(40)] private uint _pad1;
        [FieldOffset(44)] private uint _pad2;
        [FieldOffset(48)] private ulong _pad3;
        [FieldOffset(56)] private ulong _pad4;
    }

    /// <summary>Last solved forces for gameplay and visual consumers. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct SubmarineForceAccumulator
    {
        [FieldOffset(0)] public float3 LinearForceWorld;
        [FieldOffset(12)] public float3 TorqueWorld;
        [FieldOffset(24)] public float3 LastThrustWorld;
        [FieldOffset(36)] public float3 LastDragWorld;
        [FieldOffset(48)] public float3 LastBuoyancyWorld;
        [FieldOffset(60)] public float3 ImpactPointLocal;
        [FieldOffset(72)] public float3 ImpactNormalWorld;
        [FieldOffset(84)] public float CavitationIndex;
        [FieldOffset(88)] public float ImpactMagnitude;
        [FieldOffset(92)] public uint Flags;
        [FieldOffset(96)] public uint Frame;
        [FieldOffset(100)] private uint _pad0;
        [FieldOffset(104)] private ulong _pad1;
        [FieldOffset(112)] private ulong _pad2;
        [FieldOffset(120)] private ulong _pad3;
    }

    /// <summary>Designer-tunable constants mirrored into the Vault. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct SubmarineKinematicConfig
    {
        [FieldOffset(0)] public double3 LocalOriginAup;
        [FieldOffset(24)] public float BaseMassKg;
        [FieldOffset(28)] public float HullVolumeM3;
        [FieldOffset(32)] public float FluidDensityKgPerM3;
        [FieldOffset(36)] public float DragScale;
        [FieldOffset(40)] public float PidP;
        [FieldOffset(44)] public float PidI;
        [FieldOffset(48)] public float PidD;
        [FieldOffset(52)] public float PidIntegralLimit;
        [FieldOffset(56)] public float GyroStrength;
        [FieldOffset(60)] public float GyroDamping;
        [FieldOffset(64)] public float MaxThrustN;
        [FieldOffset(68)] public float MaxTorqueNm;
        [Obsolete("Deprecated ABI residue. SHINOBU_333 ballast force owns ballast truth; Submarine6DIntegratorJob ignores this scalar.")]
        [FieldOffset(72)] public float BallastLiftN;
        [FieldOffset(76)] public float CavitationDepthMeters;
        [FieldOffset(80)] public float CavitationThreshold;
        [FieldOffset(84)] public float SloshSpring;
        [FieldOffset(88)] public float SloshDamping;
        [FieldOffset(92)] public float FloodComGain;
        [FieldOffset(96)] public float CargoForwardMeters;
        [FieldOffset(100)] public float TickDilationPressure01;
        [FieldOffset(104)] public float3 MockFloodLocal;
        [FieldOffset(116)] public uint SourceHash;
        [FieldOffset(120)] public byte QualityWeightByte;
        [FieldOffset(121)] public byte Flags;
        [FieldOffset(122)] private ushort _pad0;
        [FieldOffset(124)] private uint _pad1;
    }

    /// <summary>300-frame blackbox telemetry entry. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct SubmarineKinematicTelemetry
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float3 LinearVelocity;
        [FieldOffset(36)] public float3 AngularVelocity;
        [FieldOffset(48)] public float3 CenterOfMassLocal;
        [FieldOffset(60)] public float3 CenterOfBuoyancyLocal;
        [FieldOffset(72)] public float3 LocalPosition;
        [FieldOffset(84)] public uint Frame;
        [FieldOffset(88)] public uint Flags;
        [FieldOffset(92)] public float TotalMassKg;
        [FieldOffset(96)] public float BallastRatio01;
        [FieldOffset(100)] public float CavitationIndex;
        [FieldOffset(104)] public float EstimatedCostUs;
        [FieldOffset(108)] public uint StateHash;
        [FieldOffset(112)] private uint _pad0;
        [FieldOffset(116)] private uint _pad1;
        [FieldOffset(120)] private ulong _pad2;
    }

    /// <summary>Added mass tensor payload. Size: 128 bytes, two matrix cache lines.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct AddedMassProfileDTO
    {
        [FieldOffset(0)] public float4x4 LinearAddedMass;
        [FieldOffset(64)] public float4x4 AngularAddedMass;
    }

    /// <summary>Hydrodynamic tensor blackbox entry. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct SubmarineHydrodynamicsTelemetry
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float DepthMeters;
        [FieldOffset(28)] public float FluidDensityKgPerM3;
        [FieldOffset(32)] public float DisplacedWaterMassKg;
        [FieldOffset(36)] public float FloodWaterMassKg;
        [FieldOffset(40)] public float3 LinearDiagKg;
        [FieldOffset(52)] public float3 AngularDiagKgm2;
        [FieldOffset(64)] public float MatrixBlend01;
        [FieldOffset(68)] public float RotationalDamping;
        [FieldOffset(72)] public uint Frame;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] public uint StateHash;
        [FieldOffset(84)] public uint TensorHash;
        [FieldOffset(88)] public float BurstElapsedUs;
        [FieldOffset(92)] public float DepthDensityScalar;
        [FieldOffset(96)] private ulong _pad0;
        [FieldOffset(104)] private ulong _pad1;
        [FieldOffset(112)] private ulong _pad2;
        [FieldOffset(120)] private ulong _pad3;
    }

    /// <summary>Cold imported hull profile row. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SubmarineHullProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float BaseMassKg;
        [FieldOffset(8)] public float HullVolumeM3;
        [FieldOffset(12)] public float LengthMeters;
        [FieldOffset(16)] public float RadiusMeters;
        [FieldOffset(20)] public float AddedMassMultiplier;
        [FieldOffset(24)] public float3 CenterOfBuoyancyLocal;
        [FieldOffset(36)] public float3 CenterOfMassLocal;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public float FloodVolumeScalar;
        [FieldOffset(56)] private ulong _pad0;
    }

    /// <summary>Editor/cold tuning lane for the added-mass solver. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SubmarineAddedMassTuningDTO
    {
        [FieldOffset(0)] public float BaseAddedMassMultiplier;
        [FieldOffset(4)] public float DepthDensityLinear;
        [FieldOffset(8)] public float DepthDensityQuadratic;
        [FieldOffset(12)] public float RotationalDampingScalar;
        [FieldOffset(16)] public float MatrixBlendBias;
        [FieldOffset(20)] public float MaxDepthMeters;
        [FieldOffset(24)] public float FloodVolumeScalar;
        [FieldOffset(28)] public float TensorAnisotropyScalar;
        [FieldOffset(32)] public uint SourceHash;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] private ulong _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
    }

    /// <summary>Vault-backed pitch/roll auto-level tuning. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = SubmarineDynamicsConstants.GyroTuningBytes)]
    public struct SubmarineGyroDTO
    {
        [FieldOffset(0)] public float ProportionalGainPitch;
        [FieldOffset(4)] public float DerivativeGainPitch;
        [FieldOffset(8)] public float ProportionalGainRoll;
        [FieldOffset(12)] public float DerivativeGainRoll;
        [FieldOffset(16)] public float MaxCorrectionTorque;
        [FieldOffset(20)] public uint AutoLevelEnabledFlag;
        [FieldOffset(24)] private uint _pad0;
        [FieldOffset(28)] private uint _pad1;
    }

    /// <summary>Quaternion-derived leveling error. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = SubmarineDynamicsConstants.GyroErrorBytes)]
    public struct SubmarineGyroErrorDTO
    {
        [FieldOffset(0)] public double3 CurrentAup;
        [FieldOffset(24)] public float3 ErrorVector;
        [FieldOffset(36)] public float3 CurrentUp;
        [FieldOffset(48)] public float ErrorMagnitude;
        [FieldOffset(52)] public uint TargetEntityHash;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public uint Flags;
    }

    /// <summary>Auto-level force handoff packet. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = SubmarineDynamicsConstants.GyroForcePacketBytes)]
    public struct SubmarineGyroForcePacketDTO
    {
        [FieldOffset(0)] public double3 CurrentAup;
        [FieldOffset(24)] public float3 CorrectiveTorque;
        [FieldOffset(36)] public float3 CorrectiveAngularAcceleration;
        [FieldOffset(48)] public float3 ErrorVector;
        [FieldOffset(60)] public float3 AngularVelocityWorld;
        [FieldOffset(72)] public uint TargetEntityHash;
        [FieldOffset(76)] public int StateIndex;
        [FieldOffset(80)] public uint Frame;
        [FieldOffset(84)] public uint Flags;
        [FieldOffset(88)] public float TorqueMagnitude;
        [FieldOffset(92)] public float MatrixBlend01;
        [FieldOffset(96)] public float PitchError;
        [FieldOffset(100)] public float RollError;
        [FieldOffset(104)] public float PitchOmega;
        [FieldOffset(108)] public float RollOmega;
        [FieldOffset(112)] private ulong _pad0;
        [FieldOffset(120)] private ulong _pad1;
    }

    /// <summary>300-frame auto-level blackbox entry. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = SubmarineDynamicsConstants.GyroTelemetryBytes)]
    public struct GyroTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public int ActiveControllers;
        [FieldOffset(8)] public float AveragePitchError;
        [FieldOffset(12)] public float AverageRollError;
        [FieldOffset(16)] public float MaxCorrectiveTorque;
        [FieldOffset(20)] public float BurstElapsedUs;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint StateHash;
        [FieldOffset(32)] public float MaxErrorMagnitude;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public uint NonFiniteCount;
        [FieldOffset(44)] public uint LastTargetEntityHash;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    /// <summary>GPU-facing artificial horizon payload. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = SubmarineDynamicsConstants.GyroVisualStateBytes)]
    public struct SubmarineGyroVisualStateDTO
    {
        [FieldOffset(0)] public float3 ErrorVector;
        [FieldOffset(12)] public float Effort01;
        [FieldOffset(16)] public float HorizonRollRadians;
        [FieldOffset(20)] public float HorizonPitchRadians;
        [FieldOffset(24)] public float3 CorrectiveTorque;
        [FieldOffset(36)] public uint TargetEntityHash;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    /// <summary>Cold CSV gyro profile row. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = SubmarineDynamicsConstants.GyroProfileBytes)]
    public struct SubmarineGyroProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float ProportionalGainPitch;
        [FieldOffset(8)] public float DerivativeGainPitch;
        [FieldOffset(12)] public float ProportionalGainRoll;
        [FieldOffset(16)] public float DerivativeGainRoll;
        [FieldOffset(20)] public float MaxCorrectionTorque;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] private uint _pad0;
        [FieldOffset(32)] private ulong _pad1;
        [FieldOffset(40)] private ulong _pad2;
        [FieldOffset(48)] private ulong _pad3;
        [FieldOffset(56)] private ulong _pad4;
    }

    /// <summary>Frame-local gyro packet counters and reductions. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = SubmarineDynamicsConstants.GyroCounterBytes)]
    public struct SubmarineGyroCounterDTO
    {
        [FieldOffset(0)] public int PacketCount;
        [FieldOffset(4)] public int ActiveControllers;
        [FieldOffset(8)] public int NonFiniteCount;
        [FieldOffset(12)] public int ReservedCount;
        [FieldOffset(16)] public float AveragePitchError;
        [FieldOffset(20)] public float AverageRollError;
        [FieldOffset(24)] public float MaxCorrectiveTorque;
        [FieldOffset(28)] public float MaxErrorMagnitude;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint StateHash;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint LastTargetEntityHash;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    /// <summary>Fallback flood signal used when the real flood domain is unavailable. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MockFloodSignal : ISignal
    {
        [FieldOffset(0)] public float3 LocalCompartment;
        [FieldOffset(12)] public float WaterMassKg;
        [FieldOffset(16)] public float FillRatio01;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public byte Flags;
        [FieldOffset(25)] private byte _pad0;
        [FieldOffset(26)] private ushort _pad1;
        [FieldOffset(28)] private uint _pad2;
        [FieldOffset(32)] private ulong _pad3;
        [FieldOffset(40)] private ulong _pad4;
        [FieldOffset(48)] private ulong _pad5;
        [FieldOffset(56)] private ulong _pad6;
    }

    /// <summary>Fallback impact signal used when the real hull domain is unavailable. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MockImpactSignal : ISignal
    {
        [FieldOffset(0)] public float3 LocalPoint;
        [FieldOffset(12)] public float3 NormalWorld;
        [FieldOffset(24)] public float Magnitude;
        [FieldOffset(28)] public float DepthMeters;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public byte TraumaLevel;
        [FieldOffset(37)] private byte _pad0;
        [FieldOffset(38)] private ushort _pad1;
        [FieldOffset(40)] private ulong _pad2;
        [FieldOffset(48)] private ulong _pad3;
        [FieldOffset(56)] private ulong _pad4;
    }

    /// <summary>Cavitation cue emitted by the dynamics job. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct CavitationAcousticSignal : ISignal
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float Intensity01;
        [FieldOffset(16)] public float FrequencyHz;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public byte Flags;
        [FieldOffset(25)] private byte _pad0;
        [FieldOffset(26)] private ushort _pad1;
        [FieldOffset(28)] private uint _pad2;
        [FieldOffset(32)] private ulong _pad3;
        [FieldOffset(40)] private ulong _pad4;
        [FieldOffset(48)] private ulong _pad5;
        [FieldOffset(56)] private ulong _pad6;
    }

    internal static class SubmarineDynamicsSimdMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LengthFromSq(float lengthSq)
        {
            float finiteSq = math.select(0f, lengthSq, math.isfinite(lengthSq));
            float safeSq = math.max(finiteSq, 0.0001f);
            return math.select(0f, safeSq * math.rsqrt(math.max(safeSq, 0.0001f)), finiteSq > 0.0001f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            bool valid = math.all(math.isfinite(value)) & math.isfinite(lenSq) & lenSq > 0.0001f;
            float3 source = math.select(fallback, value, valid);
            lenSq = math.lengthsq(source);
            bool fallbackValid = math.all(math.isfinite(source)) & math.isfinite(lenSq) & lenSq > 0.0001f;
            return math.select(float3.zero, source * math.rsqrt(math.max(lenSq, 0.0001f)), fallbackValid);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion IntegrateAngularVelocityNoTrig(float3 angularVelocity, float dt)
        {
            float safeDt = math.max(0f, math.select(0f, dt, math.isfinite(dt)));
            float3 safeAngularVelocity = math.select(float3.zero, angularVelocity, new bool3(math.all(math.isfinite(angularVelocity))));
            float thetaSq = math.lengthsq(safeAngularVelocity) * safeDt * safeDt;
            float halfThetaSq = thetaSq * 0.25f;
            float sinc = 1f + (halfThetaSq * (-0.1666666716f + (halfThetaSq * 0.0083333338f)));
            float cosHalf = 1f + (halfThetaSq * (-0.5f + (halfThetaSq * 0.0416666679f)));
            float4 delta = new float4(safeAngularVelocity * (0.5f * safeDt * sinc), cosHalf);
            float lenSq = math.lengthsq(delta);
            bool valid = math.all(math.isfinite(delta)) & math.isfinite(lenSq) & lenSq > 0.0001f & thetaSq > 0.00000001f;
            float4 normalized = delta * math.rsqrt(math.max(lenSq, 0.0001f));
            return new quaternion(math.select(quaternion.identity.value, normalized, new bool4(valid)));
        }
    }

    internal static class SubmarineAddedMassMath
    {
        private const float Pi = 3.14159265358979323846f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 SafeAup(double3 value)
        {
            return math.all(math.isfinite(value)) ? value : double3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ToLocal(in double3 aup, in SubmarineKinematicConfig config)
        {
            double3 delta = SafeAup(aup) - SafeAup(config.LocalOriginAup);
            float3 local = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            return math.all(math.isfinite(local)) ? local : float3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SafePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SafeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion NormalizeSafe(quaternion value)
        {
            if (!math.all(math.isfinite(value.value)))
                return quaternion.identity;

            float lenSq = math.lengthsq(value.value);
            return lenSq > 0.0001f ? new quaternion(value.value * math.rsqrt(math.max(lenSq, 0.0001f))) : quaternion.identity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveDepthDensityScalar(float depthMeters)
        {
            return ResolveDepthDensityScalar(depthMeters, 0.08f, 0.05f, 6000f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveDepthDensityScalar(float depthMeters, float linear, float quadratic, float maxDepthMeters)
        {
            float maxDepth = SafePositive(maxDepthMeters, 6000f);
            float depth = math.clamp(math.isfinite(depthMeters) ? depthMeters : 0f, 0f, maxDepth);
            float t = depth / maxDepth;
            float l = math.clamp(math.isfinite(linear) ? linear : 0.08f, 0f, 0.5f);
            float q = math.clamp(math.isfinite(quadratic) ? quadratic : 0.05f, 0f, 0.5f);
            return 1f + (l * t) + (q * t * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveTensorBlend(float globalQualityWeight, float lowLodHoldSeconds, float matrixBlendBias)
        {
            float quality = math.saturate(math.select(SubmarineDynamicsConstants.AuthoritativeQualityWeight, globalQualityWeight, math.isfinite(globalQualityWeight)));
            float bias = math.clamp(math.isfinite(matrixBlendBias) ? matrixBlendBias : 0f, -0.5f, 0.5f);
            float baseBlend = math.saturate((quality * 1.08f) + bias - 0.18f);
            float lodSuppression = math.saturate(1f - (SafeNonNegative(lowLodHoldSeconds) * 0.5f));
            float blended = math.saturate(baseBlend * lodSuppression);
            return blended * blended * (3f - (2f * blended));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SubmarineAddedMassTuningDTO SanitizeTuning(in SubmarineAddedMassTuningDTO tuning)
        {
            SubmarineAddedMassTuningDTO safe = default;
            safe.BaseAddedMassMultiplier = math.clamp(SafePositive(tuning.BaseAddedMassMultiplier, 1f), 0.25f, 4f);
            safe.DepthDensityLinear = math.clamp(math.isfinite(tuning.DepthDensityLinear) ? tuning.DepthDensityLinear : 0.08f, 0f, 0.5f);
            safe.DepthDensityQuadratic = math.clamp(math.isfinite(tuning.DepthDensityQuadratic) ? tuning.DepthDensityQuadratic : 0.05f, 0f, 0.5f);
            safe.RotationalDampingScalar = math.clamp(SafePositive(tuning.RotationalDampingScalar, 1f), 0.1f, 6f);
            safe.MatrixBlendBias = math.clamp(math.isfinite(tuning.MatrixBlendBias) ? tuning.MatrixBlendBias : 0f, -0.5f, 0.5f);
            safe.MaxDepthMeters = math.clamp(SafePositive(tuning.MaxDepthMeters, 6000f), 100f, 12000f);
            safe.FloodVolumeScalar = math.clamp(math.isfinite(tuning.FloodVolumeScalar) ? tuning.FloodVolumeScalar : 1f, 0f, 3f);
            safe.TensorAnisotropyScalar = math.clamp(SafePositive(tuning.TensorAnisotropyScalar, 1f), 0.25f, 4f);
            safe.SourceHash = tuning.SourceHash != 0u ? tuning.SourceHash : SubmarineDynamicsConstants.SourceHashAddedMass;
            safe.Flags = tuning.Flags;
            return safe;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SubmarineAddedMassTuningDTO DefaultTuning()
        {
            SubmarineAddedMassTuningDTO tuning = default;
            tuning.BaseAddedMassMultiplier = 1f;
            tuning.DepthDensityLinear = 0.08f;
            tuning.DepthDensityQuadratic = 0.05f;
            tuning.RotationalDampingScalar = 1f;
            tuning.MatrixBlendBias = 0f;
            tuning.MaxDepthMeters = 6000f;
            tuning.FloodVolumeScalar = 1f;
            tuning.TensorAnisotropyScalar = 1f;
            tuning.SourceHash = SubmarineDynamicsConstants.SourceHashAddedMass;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ResolveHullAxes(float hullVolumeM3, out float lengthMeters, out float radiusMeters)
        {
            float volume = SafePositive(hullVolumeM3, 1f);
            lengthMeters = math.clamp(CubeRootPositive(volume * 10f) * 1.8f, 4f, 80f);
            float radiusSq = volume / math.max(Pi * lengthMeters, 0.001f);
            radiusMeters = math.clamp(SubmarineDynamicsSimdMath.LengthFromSq(math.max(radiusSq, 0.04f)), 0.2f, 12f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CubeRootPositive(float value)
        {
            float target = math.max(0.000001f, math.select(1f, value, math.isfinite(value)));
            float high = 1f;
            for (int i = 0; i < 8; i++)
            {
                float highSq = high * high;
                if (highSq * high >= target || high >= 128f)
                    break;

                high *= 2f;
            }

            float low = 0f;
            for (int i = 0; i < 12; i++)
            {
                float mid = (low + high) * 0.5f;
                float cube = mid * mid * mid;
                if (cube <= target)
                    low = mid;
                else
                    high = mid;
            }

            return math.max(0.0001f, (low + high) * 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4x4 BuildWorldTensor(float3 diagonalLocal, quaternion rotation)
        {
            float3 safe = math.max(new float3(1f), math.abs(diagonalLocal));
            float3x3 r = new float3x3(NormalizeSafe(rotation));
            float3x3 d = new float3x3(
                new float3(safe.x, 0f, 0f),
                new float3(0f, safe.y, 0f),
                new float3(0f, 0f, safe.z));
            float3x3 m = math.mul(math.mul(r, d), math.transpose(r));
            return new float4x4(
                new float4(m.c0, 0f),
                new float4(m.c1, 0f),
                new float4(m.c2, 0f),
                new float4(0f, 0f, 0f, 1f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ExtractDiagonal(in float4x4 matrix)
        {
            return math.max(new float3(1f), new float3(matrix.c0.x, matrix.c1.y, matrix.c2.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryMulInverse3x3(in float4x4 matrix, float3 vector, out float3 result)
        {
            float a00 = matrix.c0.x;
            float a01 = matrix.c1.x;
            float a02 = matrix.c2.x;
            float a10 = matrix.c0.y;
            float a11 = matrix.c1.y;
            float a12 = matrix.c2.y;
            float a20 = matrix.c0.z;
            float a21 = matrix.c1.z;
            float a22 = matrix.c2.z;

            float c00 = (a11 * a22) - (a12 * a21);
            float c01 = (a12 * a20) - (a10 * a22);
            float c02 = (a10 * a21) - (a11 * a20);
            float c10 = (a02 * a21) - (a01 * a22);
            float c11 = (a00 * a22) - (a02 * a20);
            float c12 = (a01 * a20) - (a00 * a21);
            float c20 = (a01 * a12) - (a02 * a11);
            float c21 = (a02 * a10) - (a00 * a12);
            float c22 = (a00 * a11) - (a01 * a10);

            float determinant = (a00 * c00) + (a01 * c01) + (a02 * c02);
            bool determinantValid = math.isfinite(determinant) && math.abs(determinant) > 0.0001f;
            float invDeterminant = math.rcp(math.select(1f, determinant, determinantValid));

            result = new float3(
                ((c00 * vector.x) + (c10 * vector.y) + (c20 * vector.z)) * invDeterminant,
                ((c01 * vector.x) + (c11 * vector.y) + (c21 * vector.z)) * invDeterminant,
                ((c02 * vector.x) + (c12 * vector.y) + (c22 * vector.z)) * invDeterminant);

            return determinantValid && math.all(math.isfinite(result));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ResolveLinearAcceleration(float3 forceWorld, float baseMassKg, in AddedMassProfileDTO profile, float matrixBlend01)
        {
            float safeBaseMass = SafePositive(baseMassKg, 1f);
            float3 diagonal = ExtractDiagonal(in profile.LinearAddedMass) + new float3(safeBaseMass);
            float3 diagonalAcceleration = forceWorld / math.max(diagonal, new float3(1f));
            if (matrixBlend01 <= 0.001f)
                return diagonalAcceleration;

            float4x4 matrix = profile.LinearAddedMass;
            matrix.c0.x += safeBaseMass;
            matrix.c1.y += safeBaseMass;
            matrix.c2.z += safeBaseMass;
            matrix.c3 = new float4(0f, 0f, 0f, 1f);

            if (!TryMulInverse3x3(in matrix, forceWorld, out float3 tensorAcceleration))
                tensorAcceleration = diagonalAcceleration;

            return math.lerp(diagonalAcceleration, tensorAcceleration, math.saturate(matrixBlend01));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ResolveLinearVelocityDelta(float3 impulseWorld, float baseMassKg, in AddedMassProfileDTO profile, float matrixBlend01)
        {
            return ResolveLinearAcceleration(impulseWorld, baseMassKg, in profile, matrixBlend01);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ResolveAngularAcceleration(float3 torqueWorld, float3 baseInertia, in AddedMassProfileDTO profile, float matrixBlend01)
        {
            float3 diagonal = ExtractDiagonal(in profile.AngularAddedMass) + math.max(baseInertia, new float3(1f));
            float3 diagonalAcceleration = torqueWorld / math.max(diagonal, new float3(1f));
            if (matrixBlend01 <= 0.001f)
                return diagonalAcceleration;

            float4x4 matrix = profile.AngularAddedMass;
            matrix.c0.x += math.max(1f, baseInertia.x);
            matrix.c1.y += math.max(1f, baseInertia.y);
            matrix.c2.z += math.max(1f, baseInertia.z);
            matrix.c3 = new float4(0f, 0f, 0f, 1f);

            if (!TryMulInverse3x3(in matrix, torqueWorld, out float3 tensorAcceleration))
                tensorAcceleration = diagonalAcceleration;

            return math.lerp(diagonalAcceleration, tensorAcceleration, math.saturate(matrixBlend01));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ResolveAngularVelocityDelta(float3 angularImpulseWorld, float3 baseInertia, in AddedMassProfileDTO profile, float matrixBlend01)
        {
            return ResolveAngularAcceleration(angularImpulseWorld, baseInertia, in profile, matrixBlend01);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveRotationalDamping(in AddedMassProfileDTO profile, float totalMassKg, float globalQualityWeight)
        {
            return ResolveRotationalDamping(in profile, totalMassKg, globalQualityWeight, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveRotationalDamping(in AddedMassProfileDTO profile, float totalMassKg, float globalQualityWeight, float dampingScalar)
        {
            float quality = math.saturate(math.select(SubmarineDynamicsConstants.AuthoritativeQualityWeight, globalQualityWeight, math.isfinite(globalQualityWeight)));
            float3 angularDiag = ExtractDiagonal(in profile.AngularAddedMass);
            float trace = angularDiag.x + angularDiag.y + angularDiag.z;
            float scalar = math.clamp(SafePositive(dampingScalar, 1f), 0.1f, 6f);
            float scale = math.saturate(trace / math.max(1f, SafePositive(totalMassKg, 1f) * 42f));
            return math.lerp(0.04f, 0.18f, scale) *
                   math.lerp(0.65f, 1f, quality) *
                   scalar;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ApplyAngularDamping(float3 angularVelocity, float damping, float dt)
        {
            float decayInput = math.max(0f, damping) * math.clamp(dt, 0.001f, 0.05f);
            float decay = math.rcp(1f + decayInput + (0.48f * decayInput * decayInput));
            return angularVelocity * decay;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(in AddedMassProfileDTO profile)
        {
            return math.all(math.isfinite(profile.LinearAddedMass.c0)) &&
                   math.all(math.isfinite(profile.LinearAddedMass.c1)) &&
                   math.all(math.isfinite(profile.LinearAddedMass.c2)) &&
                   math.all(math.isfinite(profile.LinearAddedMass.c3)) &&
                   math.all(math.isfinite(profile.AngularAddedMass.c0)) &&
                   math.all(math.isfinite(profile.AngularAddedMass.c1)) &&
                   math.all(math.isfinite(profile.AngularAddedMass.c2)) &&
                   math.all(math.isfinite(profile.AngularAddedMass.c3));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashTensor(in AddedMassProfileDTO profile)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, math.asuint(profile.LinearAddedMass.c0.x));
            hash = Mix(hash, math.asuint(profile.LinearAddedMass.c1.y));
            hash = Mix(hash, math.asuint(profile.LinearAddedMass.c2.z));
            hash = Mix(hash, math.asuint(profile.AngularAddedMass.c0.x));
            hash = Mix(hash, math.asuint(profile.AngularAddedMass.c1.y));
            hash = Mix(hash, math.asuint(profile.AngularAddedMass.c2.z));
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }
    }

    internal static class SubmarineGyroMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SubmarineGyroDTO DefaultGyro()
        {
            SubmarineGyroDTO gyro = default;
            gyro.ProportionalGainPitch = 54000f;
            gyro.DerivativeGainPitch = 11000f;
            gyro.ProportionalGainRoll = 62000f;
            gyro.DerivativeGainRoll = 13000f;
            gyro.MaxCorrectionTorque = 85000f;
            gyro.AutoLevelEnabledFlag = SubmarineDynamicsConstants.GyroFlagAutoLevelEnabled;
            return gyro;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SubmarineGyroDTO Sanitize(in SubmarineGyroDTO source)
        {
            SubmarineGyroDTO fallback = DefaultGyro();
            SubmarineGyroDTO safe = default;
            safe.ProportionalGainPitch = SafeNonNegative(source.ProportionalGainPitch, fallback.ProportionalGainPitch);
            safe.DerivativeGainPitch = SafeNonNegative(source.DerivativeGainPitch, fallback.DerivativeGainPitch);
            safe.ProportionalGainRoll = SafeNonNegative(source.ProportionalGainRoll, fallback.ProportionalGainRoll);
            safe.DerivativeGainRoll = SafeNonNegative(source.DerivativeGainRoll, fallback.DerivativeGainRoll);
            safe.MaxCorrectionTorque = math.clamp(SafePositive(source.MaxCorrectionTorque, fallback.MaxCorrectionTorque), 1f, 10000000f);
            safe.AutoLevelEnabledFlag = source.AutoLevelEnabledFlag;
            return safe;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SafeFinite(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 SafeFinite(double3 value)
        {
            return math.all(math.isfinite(value)) ? value : double3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ResolveEntityHash(in SubmarineKinematicState state, int index)
        {
            uint entity = state.EntityId;
            return entity != 0u ? entity : SubmarineDynamicsConstants.SourceHashGyro ^ ((uint)index + 1u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SafePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SafeNonNegative(float value, float fallback)
        {
            return math.isfinite(value) && value >= 0f ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ClampMagnitude(float3 value, float maxMagnitude, out bool clamped)
        {
            float max = SafePositive(maxMagnitude, 1f);
            float lengthSq = math.lengthsq(value);
            bool valid = math.all(math.isfinite(value)) & math.isfinite(lengthSq);
            if (!valid || lengthSq <= 0.000001f)
            {
                clamped = false;
                return float3.zero;
            }

            float maxSq = max * max;
            clamped = lengthSq > maxSq;
            return clamped ? value * (max * math.rsqrt(math.max(lengthSq, 0.000001f))) : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashState(in SubmarineGyroForcePacketDTO packet)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, packet.TargetEntityHash);
            hash = Mix(hash, math.asuint(packet.CorrectiveTorque.x));
            hash = Mix(hash, math.asuint(packet.CorrectiveTorque.y));
            hash = Mix(hash, math.asuint(packet.CorrectiveTorque.z));
            hash = Mix(hash, math.asuint(packet.ErrorVector.x));
            hash = Mix(hash, math.asuint(packet.ErrorVector.y));
            hash = Mix(hash, math.asuint(packet.ErrorVector.z));
            hash = Mix(hash, packet.Flags);
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Mix(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockTurbulenceJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public SubmarineKinematicState* States;
        public float AmplitudeRadiansPerSecond;
        public uint Frame;
        public int StateLength;
        public int VehicleCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)VehicleCount ||
                (uint)index >= (uint)StateLength ||
                States == null)
            {
                return;
            }

            float amplitude = math.max(0f, math.isfinite(AmplitudeRadiansPerSecond) ? AmplitudeRadiansPerSecond : 0f);
            if (amplitude <= 0.0001f)
                return;

            SubmarineKinematicState state = States[index];
            uint seed = SubmarineGyroMath.Mix(Frame * 747796405u, (uint)index + 0x9E3779B9u);
            float t0 = ((seed & 1023u) * (1f / 1023f) - 0.5f) * 2f;
            float t1 = (((seed >> 10) & 1023u) * (1f / 1023f) - 0.5f) * 2f;
            float t2 = (((seed >> 20) & 1023u) * (1f / 1023f) - 0.5f) * 2f;
            float3 spike = new float3(t0, t1 * 0.35f, t2) * amplitude;
            state.AngularVelocity = SubmarineGyroMath.SafeFinite(state.AngularVelocity + spike, float3.zero);
            States[index] = state;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CalculateGyroscopicErrorJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public SubmarineKinematicState* States;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SubmarineGyroErrorDTO* Errors;
        public uint Frame;
        public int StateLength;
        public int ErrorLength;
        public int VehicleCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)VehicleCount ||
                (uint)index >= (uint)StateLength ||
                (uint)index >= (uint)ErrorLength ||
                States == null ||
                Errors == null)
            {
                return;
            }

            SubmarineKinematicState state = States[index];
            quaternion rotation = SubmarineAddedMassMath.NormalizeSafe(state.Rotation);
            float3 currentUp = math.mul(rotation, new float3(0f, 1f, 0f));
            currentUp = SubmarineDynamicsSimdMath.NormalizeOrFallback(currentUp, new float3(0f, 1f, 0f));
            float3 error = math.cross(currentUp, new float3(0f, 1f, 0f));
            float magnitudeSq = math.lengthsq(error);
            bool finite = math.all(math.isfinite(error)) & math.all(math.isfinite(currentUp)) & math.isfinite(magnitudeSq);

            SubmarineGyroErrorDTO dto = default;
            dto.CurrentAup = SubmarineGyroMath.SafeFinite(state.Aup);
            dto.CurrentUp = finite ? currentUp : new float3(0f, 1f, 0f);
            dto.ErrorVector = finite ? error : float3.zero;
            dto.ErrorMagnitude = finite ? SubmarineDynamicsSimdMath.LengthFromSq(magnitudeSq) : 0f;
            dto.TargetEntityHash = SubmarineGyroMath.ResolveEntityHash(in state, index);
            dto.Frame = Frame;
            dto.Flags = finite ? 0u : SubmarineDynamicsConstants.GyroFlagNonFinite;
            Errors[index] = dto;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluatePdControllerJob : IJobParallelFor
    {
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public SubmarineKinematicState* States;
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public SubmarineGyroDTO* Gyros;
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public SubmarineGyroErrorDTO* Errors;
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public AddedMassProfileDTO* AddedMassProfiles;
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public SubmarineAddedMassTuningDTO* AddedMassTuning;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SubmarineForceAccumulator* Forces;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SubmarineGyroForcePacketDTO* Packets;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SubmarineGyroVisualStateDTO* VisualStates;
        public float GlobalQualityWeight;
        public uint Frame;
        public int StateLength;
        public int GyroLength;
        public int ErrorLength;
        public int AddedMassLength;
        public int TuningLength;
        public int ForceLength;
        public int PacketLength;
        public int VisualLength;
        public int VehicleCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)VehicleCount ||
                (uint)index >= (uint)StateLength ||
                (uint)index >= (uint)GyroLength ||
                (uint)index >= (uint)ErrorLength ||
                (uint)index >= (uint)AddedMassLength ||
                (uint)index >= (uint)ForceLength ||
                (uint)index >= (uint)PacketLength ||
                (uint)index >= (uint)VisualLength ||
                States == null ||
                Gyros == null ||
                Errors == null ||
                AddedMassProfiles == null ||
                Forces == null ||
                Packets == null ||
                VisualStates == null)
            {
                return;
            }

            SubmarineKinematicState state = States[index];
            SubmarineGyroDTO rawGyro = Gyros[index];
            SubmarineGyroDTO gyro = SubmarineGyroMath.Sanitize(in rawGyro);
            SubmarineGyroErrorDTO error = Errors[index];
            AddedMassProfileDTO profile = AddedMassProfiles[index];
            SubmarineForceAccumulator force = Forces[index];
            SubmarineAddedMassTuningDTO tuning = SubmarineAddedMassMath.DefaultTuning();
            if (TuningLength > 0 && AddedMassTuning != null)
            {
                SubmarineAddedMassTuningDTO rawTuning = AddedMassTuning[0];
                tuning = SubmarineAddedMassMath.SanitizeTuning(in rawTuning);
            }

            bool enabled = (gyro.AutoLevelEnabledFlag & SubmarineDynamicsConstants.GyroFlagAutoLevelEnabled) != 0u;
            bool suppressed = state.GyroDisabledSeconds > 0f;
            bool nonFinite = (error.Flags & SubmarineDynamicsConstants.GyroFlagNonFinite) != 0u ||
                             !math.all(math.isfinite(state.AngularVelocity)) ||
                             !SubmarineAddedMassMath.IsFinite(in profile);

            quaternion rotation = SubmarineAddedMassMath.NormalizeSafe(state.Rotation);
            float3 right = SubmarineDynamicsSimdMath.NormalizeOrFallback(math.mul(rotation, new float3(1f, 0f, 0f)), new float3(1f, 0f, 0f));
            float3 forward = SubmarineDynamicsSimdMath.NormalizeOrFallback(math.mul(rotation, new float3(0f, 0f, 1f)), new float3(0f, 0f, 1f));
            float3 errorVector = SubmarineGyroMath.SafeFinite(error.ErrorVector, float3.zero);
            float3 angularVelocity = SubmarineGyroMath.SafeFinite(state.AngularVelocity, float3.zero);
            float pitchError = math.dot(errorVector, right);
            float rollError = math.dot(errorVector, forward);
            float pitchOmega = math.dot(angularVelocity, right);
            float rollOmega = math.dot(angularVelocity, forward);

            float3 torque = float3.zero;
            uint flags = 0u;
            if (enabled && !suppressed && !nonFinite)
            {
                float pitchTorque = global::Hecton8.PureLogic.Systems.PitchTrimCorrectionCalculator.Compute(pitchError, gyro.ProportionalGainPitch, gyro.MaxCorrectionTorque, pitchOmega, gyro.DerivativeGainPitch);
                float rollTorque = (rollError * gyro.ProportionalGainRoll) - (rollOmega * gyro.DerivativeGainRoll);
                torque = (right * pitchTorque) + (forward * rollTorque);
                torque = SubmarineGyroMath.ClampMagnitude(torque, gyro.MaxCorrectionTorque, out bool clamped);
                flags |= clamped ? SubmarineDynamicsConstants.GyroFlagTorqueClamped : 0u;
                flags |= SubmarineDynamicsConstants.GyroFlagPacketQueued;
            }
            else
            {
                flags |= suppressed ? SubmarineDynamicsConstants.GyroFlagSuppressed : 0u;
                flags |= nonFinite ? SubmarineDynamicsConstants.GyroFlagNonFinite : 0u;
            }

            float matrixBlend = SubmarineAddedMassMath.ResolveTensorBlend(GlobalQualityWeight, state.GyroDisabledSeconds, tuning.MatrixBlendBias);
            flags |= matrixBlend <= 0.001f ? SubmarineDynamicsConstants.GyroFlagTensorFallback : 0u;
            float3 angularAcceleration = SubmarineAddedMassMath.ResolveAngularAcceleration(
                torque,
                math.max(state.InertiaTensor, new float3(1f)),
                in profile,
                matrixBlend);
            angularAcceleration = SubmarineGyroMath.SafeFinite(angularAcceleration, float3.zero);
            float torqueMagnitude = SubmarineDynamicsSimdMath.LengthFromSq(math.lengthsq(torque));
            bool outputFinite = math.all(math.isfinite(torque)) &&
                                math.all(math.isfinite(angularAcceleration)) &&
                                math.isfinite(torqueMagnitude) &&
                                math.isfinite(pitchError) &&
                                math.isfinite(rollError) &&
                                math.isfinite(pitchOmega) &&
                                math.isfinite(rollOmega);
            if (!outputFinite)
            {
                torque = float3.zero;
                angularAcceleration = float3.zero;
                torqueMagnitude = 0f;
                flags |= SubmarineDynamicsConstants.GyroFlagNonFinite;
            }

            force.TorqueWorld = torque;
            if (torqueMagnitude > 0.0001f)
                force.Flags |= SubmarineDynamicsConstants.ForceFlagGyroCorrection;
            else
                force.Flags &= ~SubmarineDynamicsConstants.ForceFlagGyroCorrection;
            force.Frame = Frame;
            Forces[index] = force;

            SubmarineGyroForcePacketDTO packet = default;
            packet.CurrentAup = error.CurrentAup;
            packet.CorrectiveTorque = torque;
            packet.CorrectiveAngularAcceleration = angularAcceleration;
            packet.ErrorVector = errorVector;
            packet.AngularVelocityWorld = angularVelocity;
            packet.TargetEntityHash = error.TargetEntityHash;
            packet.StateIndex = index;
            packet.Frame = Frame;
            packet.Flags = flags;
            packet.TorqueMagnitude = torqueMagnitude;
            packet.MatrixBlend01 = matrixBlend;
            packet.PitchError = pitchError;
            packet.RollError = rollError;
            packet.PitchOmega = pitchOmega;
            packet.RollOmega = rollOmega;
            Packets[index] = packet;

            SubmarineGyroVisualStateDTO visual = default;
            visual.ErrorVector = errorVector;
            visual.Effort01 = math.saturate(torqueMagnitude / math.max(1f, gyro.MaxCorrectionTorque));
            visual.HorizonRollRadians = rollError;
            visual.HorizonPitchRadians = pitchError;
            visual.CorrectiveTorque = torque;
            visual.TargetEntityHash = error.TargetEntityHash;
            visual.Frame = Frame;
            visual.Flags = flags;
            VisualStates[index] = visual;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct RecordGyroTelemetryJob : IJob
    {
        [NoAlias, NativeDisableUnsafePtrRestriction, ReadOnly] public SubmarineGyroForcePacketDTO* Packets;
        [NoAlias, NativeDisableUnsafePtrRestriction] public GyroTelemetryEntry* Telemetry;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SubmarineGyroCounterDTO* Counters;
        public float GlobalQualityWeight;
        public uint Frame;
        public int PacketLength;
        public int TelemetryLength;
        public int CounterLength;
        public int VehicleCount;

        public void Execute()
        {
            if (Packets == null ||
                Telemetry == null ||
                Counters == null ||
                TelemetryLength <= 0 ||
                CounterLength <= 0 ||
                PacketLength <= 0)
            {
                return;
            }

            int count = math.min(math.max(0, VehicleCount), PacketLength);
            int active = 0;
            int nonFinite = 0;
            float pitchSum = 0f;
            float rollSum = 0f;
            float maxTorque = 0f;
            float maxError = 0f;
            uint flags = 0u;
            uint hash = 2166136261u;
            uint lastTarget = 0u;

            for (int i = 0; i < count; i++)
            {
                SubmarineGyroForcePacketDTO packet = Packets[i];
                if (packet.Frame != Frame)
                    continue;

                bool packetActive = (packet.Flags & SubmarineDynamicsConstants.GyroFlagPacketQueued) != 0u;
                active += packetActive ? 1 : 0;
                nonFinite += (packet.Flags & SubmarineDynamicsConstants.GyroFlagNonFinite) != 0u ? 1 : 0;
                pitchSum += math.abs(math.isfinite(packet.PitchError) ? packet.PitchError : 0f);
                rollSum += math.abs(math.isfinite(packet.RollError) ? packet.RollError : 0f);
                maxTorque = math.max(maxTorque, math.isfinite(packet.TorqueMagnitude) ? packet.TorqueMagnitude : 0f);
                maxError = math.max(maxError, SubmarineDynamicsSimdMath.LengthFromSq(math.lengthsq(SubmarineGyroMath.SafeFinite(packet.ErrorVector, float3.zero))));
                flags |= packet.Flags;
                hash = SubmarineGyroMath.Mix(hash, SubmarineGyroMath.HashState(in packet));
                lastTarget = packet.TargetEntityHash != 0u ? packet.TargetEntityHash : lastTarget;
            }

            float divisor = math.max(1f, count);
            GyroTelemetryEntry entry = default;
            entry.Frame = Frame;
            entry.ActiveControllers = active;
            entry.AveragePitchError = pitchSum / divisor;
            entry.AverageRollError = rollSum / divisor;
            entry.MaxCorrectiveTorque = maxTorque;
            entry.BurstElapsedUs = 0f;
            entry.Flags = flags;
            entry.StateHash = hash;
            entry.MaxErrorMagnitude = maxError;
            entry.GlobalQualityWeight = math.saturate(math.select(
                SubmarineDynamicsConstants.AuthoritativeQualityWeight,
                GlobalQualityWeight,
                math.isfinite(GlobalQualityWeight)));
            entry.NonFiniteCount = (uint)math.max(0, nonFinite);
            entry.LastTargetEntityHash = lastTarget;

            int telemetryIndex = (int)(Frame % (uint)math.max(1, TelemetryLength));
            Telemetry[telemetryIndex] = entry;

            SubmarineGyroCounterDTO counter = default;
            counter.PacketCount = count;
            counter.ActiveControllers = active;
            counter.NonFiniteCount = nonFinite;
            counter.AveragePitchError = entry.AveragePitchError;
            counter.AverageRollError = entry.AverageRollError;
            counter.MaxCorrectiveTorque = maxTorque;
            counter.MaxErrorMagnitude = maxError;
            counter.Flags = flags;
            counter.StateHash = hash;
            counter.Frame = Frame;
            counter.LastTargetEntityHash = lastTarget;
            Counters[0] = counter;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CalculateAddedMassTensorJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<SubmarineKinematicState> States;
        [ReadOnly, NoAlias] public NativeArray<SubmarineMassProperties> MassProperties;
        public SubmarineKinematicConfig Config;
        [ReadOnly, NoAlias] public NativeArray<SubmarineHullProfileDTO> HullProfiles;
        [ReadOnly, NoAlias] public NativeArray<SubmarineAddedMassTuningDTO> Tuning;
        [NoAlias] public NativeArray<AddedMassProfileDTO> AddedMassProfiles;
        [NoAlias] public NativeArray<SubmarineHydrodynamicsTelemetry> HydrodynamicsTelemetry;
        public float GlobalQualityWeight;
        public uint Frame;
        public int VehicleCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)VehicleCount ||
                (uint)index >= (uint)States.Length ||
                (uint)index >= (uint)MassProperties.Length ||
                (uint)index >= (uint)AddedMassProfiles.Length)
            {
                return;
            }

            SubmarineKinematicState state = States[index];
            SubmarineMassProperties mass = MassProperties[index];
            SubmarineKinematicConfig config = Config;
            SubmarineAddedMassTuningDTO tuning = ResolveTuning(in Tuning);
            float3 localPosition = SubmarineAddedMassMath.ToLocal(in state.Aup, in config);
            float depthMeters = math.max(0f, -localPosition.y);
            float baseDensity = SubmarineAddedMassMath.SafePositive(
                config.FluidDensityKgPerM3,
                MockFluidDensityGenerator.DefaultSeawaterDensityKgPerM3);
            float fluidDensity = MockFluidDensityGenerator.SampleDensityKgPerM3(
                depthMeters,
                baseDensity,
                Frame,
                GlobalQualityWeight);
            float depthDensityScalar = SubmarineAddedMassMath.ResolveDepthDensityScalar(
                depthMeters,
                tuning.DepthDensityLinear,
                tuning.DepthDensityQuadratic,
                tuning.MaxDepthMeters);
            bool hasHullProfile = HullProfiles.IsCreated &&
                                  (uint)index < (uint)HullProfiles.Length &&
                                  HullProfiles[index].ProfileHash != 0u;
            SubmarineHullProfileDTO hullProfile = hasHullProfile ? HullProfiles[index] : default;
            float hullVolume = hasHullProfile
                ? SubmarineAddedMassMath.SafePositive(hullProfile.HullVolumeM3, SubmarineAddedMassMath.SafePositive(config.HullVolumeM3, 1f))
                : SubmarineAddedMassMath.SafePositive(config.HullVolumeM3, 1f);
            float addedMassMultiplier = hasHullProfile
                ? math.clamp(SubmarineAddedMassMath.SafePositive(hullProfile.AddedMassMultiplier, 1f), 0.25f, 3f)
                : 1f;
            addedMassMultiplier *= tuning.BaseAddedMassMultiplier;
            float floodVolumeScalar = hasHullProfile
                ? math.clamp(math.isfinite(hullProfile.FloodVolumeScalar) ? hullProfile.FloodVolumeScalar : 1f, 0f, 2f)
                : 1f;
            floodVolumeScalar *= tuning.FloodVolumeScalar;
            float floodMass = SubmarineAddedMassMath.SafeNonNegative(mass.FloodMassKg);
            float floodVolume = (floodMass / math.max(850f, fluidDensity)) * floodVolumeScalar;
            float effectiveVolume = hullVolume + floodVolume;
            float displacedWaterMass = math.max(1f, effectiveVolume * fluidDensity * depthDensityScalar);

            SubmarineAddedMassMath.ResolveHullAxes(effectiveVolume, out float lengthMeters, out float radiusMeters);
            if (hasHullProfile)
            {
                lengthMeters = SubmarineAddedMassMath.SafePositive(hullProfile.LengthMeters, lengthMeters);
                radiusMeters = SubmarineAddedMassMath.SafePositive(hullProfile.RadiusMeters, radiusMeters);
            }
            float anisotropy01 = math.saturate((tuning.TensorAnisotropyScalar - 0.25f) * (1f / 3.75f));
            float forwardAddedMass = displacedWaterMass * math.lerp(0.30f, 0.18f, anisotropy01);
            float lateralAddedMass = displacedWaterMass * math.lerp(0.62f, 0.82f, anisotropy01);
            float verticalAddedMass = displacedWaterMass * math.lerp(0.68f, 0.94f, anisotropy01);
            float3 linearDiagonalLocal = new float3(lateralAddedMass, verticalAddedMass, forwardAddedMass) * addedMassMultiplier;

            float radiusSq = radiusMeters * radiusMeters;
            float lengthSq = lengthMeters * lengthMeters;
            float rollAddedInertia = 0.42f * displacedWaterMass * radiusSq;
            float pitchAddedInertia = (0.74f * displacedWaterMass * ((3f * radiusSq) + lengthSq)) / 12f;
            float yawAddedInertia = (0.82f * displacedWaterMass * ((3f * radiusSq) + lengthSq)) / 12f;
            float3 angularDiagonalLocal = new float3(rollAddedInertia, pitchAddedInertia, yawAddedInertia) * addedMassMultiplier;

            quaternion rotation = SubmarineAddedMassMath.NormalizeSafe(state.Rotation);
            AddedMassProfileDTO profile = default;
            profile.LinearAddedMass = SubmarineAddedMassMath.BuildWorldTensor(linearDiagonalLocal, rotation);
            profile.AngularAddedMass = SubmarineAddedMassMath.BuildWorldTensor(angularDiagonalLocal, rotation);
            bool finite = SubmarineAddedMassMath.IsFinite(in profile);
            if (!finite)
            {
                profile.LinearAddedMass = SubmarineAddedMassMath.BuildWorldTensor(new float3(1f), quaternion.identity);
                profile.AngularAddedMass = SubmarineAddedMassMath.BuildWorldTensor(new float3(1f), quaternion.identity);
            }

            AddedMassProfiles[index] = profile;
            WriteHydrodynamicsTelemetry(
                index,
                in state,
                in profile,
                depthMeters,
                fluidDensity,
                depthDensityScalar,
                displacedWaterMass,
                floodMass,
                tuning,
                finite,
                HydrodynamicsTelemetry);
        }

        private void WriteHydrodynamicsTelemetry(
            int vehicleIndex,
            in SubmarineKinematicState state,
            in AddedMassProfileDTO profile,
            float depthMeters,
            float fluidDensity,
            float depthDensityScalar,
            float displacedWaterMass,
            float floodMass,
            in SubmarineAddedMassTuningDTO tuning,
            bool finite,
            NativeArray<SubmarineHydrodynamicsTelemetry> telemetry)
        {
            if (!telemetry.IsCreated)
                return;

            int baseIndex = vehicleIndex * SubmarineDynamicsConstants.BlackBoxFrames;
            if ((uint)baseIndex >= (uint)telemetry.Length)
                return;

            int local = (int)(Frame % SubmarineDynamicsConstants.BlackBoxFrames);
            int index = baseIndex + local;
            if ((uint)index >= (uint)telemetry.Length)
                return;

            float matrixBlend = SubmarineAddedMassMath.ResolveTensorBlend(GlobalQualityWeight, 0f, tuning.MatrixBlendBias);
            SubmarineHydrodynamicsTelemetry entry = default;
            entry.Aup = state.Aup;
            entry.DepthMeters = depthMeters;
            entry.FluidDensityKgPerM3 = fluidDensity;
            entry.DepthDensityScalar = depthDensityScalar;
            entry.DisplacedWaterMassKg = displacedWaterMass;
            entry.FloodWaterMassKg = floodMass;
            entry.LinearDiagKg = SubmarineAddedMassMath.ExtractDiagonal(in profile.LinearAddedMass);
            entry.AngularDiagKgm2 = SubmarineAddedMassMath.ExtractDiagonal(in profile.AngularAddedMass);
            entry.MatrixBlend01 = matrixBlend;
            entry.RotationalDamping = SubmarineAddedMassMath.ResolveRotationalDamping(in profile, state.TotalMassKg, GlobalQualityWeight, tuning.RotationalDampingScalar);
            entry.Frame = Frame;
            entry.Flags = finite ? 0u : SubmarineDynamicsConstants.HydroFlagTensorFallback;
            entry.Flags |= matrixBlend > 0.001f ? SubmarineDynamicsConstants.HydroFlagFullTensorBlend : 0u;
            entry.Flags |= floodMass > 0.001f ? SubmarineDynamicsConstants.HydroFlagFloodMassInjected : 0u;
            entry.StateHash = HashHydroState(in state);
            entry.TensorHash = SubmarineAddedMassMath.HashTensor(in profile);
            entry.BurstElapsedUs = 0f;
            telemetry[index] = entry;
        }

        private static uint HashHydroState(in SubmarineKinematicState state)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, math.asuint(state.LocalPosition.x));
            hash = Mix(hash, math.asuint(state.LocalPosition.y));
            hash = Mix(hash, math.asuint(state.LocalPosition.z));
            hash = Mix(hash, state.Flags);
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        private static SubmarineAddedMassTuningDTO ResolveTuning(in NativeArray<SubmarineAddedMassTuningDTO> tuning)
        {
            if (!tuning.IsCreated || tuning.Length == 0)
                return SubmarineAddedMassMath.DefaultTuning();

            return SubmarineAddedMassMath.SanitizeTuning(tuning[0]);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ApplyTensorAccelerationJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<SubmarineKinematicState> States;
        [ReadOnly, NoAlias] public NativeArray<SubmarineForceAccumulator> Forces;
        [ReadOnly, NoAlias] public NativeArray<AddedMassProfileDTO> AddedMassProfiles;
        [ReadOnly, NoAlias] public NativeArray<SubmarineAddedMassTuningDTO> Tuning;
        public float FixedDeltaTime;
        public float GlobalQualityWeight;
        public int VehicleCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)VehicleCount ||
                (uint)index >= (uint)States.Length ||
                (uint)index >= (uint)Forces.Length ||
                (uint)index >= (uint)AddedMassProfiles.Length)
            {
                return;
            }

            SubmarineKinematicState state = States[index];
            SubmarineForceAccumulator force = Forces[index];
            AddedMassProfileDTO profile = AddedMassProfiles[index];
            SubmarineAddedMassTuningDTO tuning = !Tuning.IsCreated || Tuning.Length == 0
                ? SubmarineAddedMassMath.DefaultTuning()
                : SubmarineAddedMassMath.SanitizeTuning(Tuning[0]);
            float blend = SubmarineAddedMassMath.ResolveTensorBlend(GlobalQualityWeight, state.GyroDisabledSeconds, tuning.MatrixBlendBias);
            float dt = math.clamp(FixedDeltaTime, 0.001f, 0.05f);
            state.LinearVelocity += SubmarineAddedMassMath.ResolveLinearAcceleration(force.LinearForceWorld, state.TotalMassKg, in profile, blend) * dt;
            state.AngularVelocity += SubmarineAddedMassMath.ResolveAngularAcceleration(force.TorqueWorld, state.InertiaTensor, in profile, blend) * dt;
            States[index] = state;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ApplyHydrodynamicDampingJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<SubmarineKinematicState> States;
        [ReadOnly, NoAlias] public NativeArray<AddedMassProfileDTO> AddedMassProfiles;
        [ReadOnly, NoAlias] public NativeArray<SubmarineAddedMassTuningDTO> Tuning;
        public float FixedDeltaTime;
        public float GlobalQualityWeight;
        public int VehicleCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)VehicleCount ||
                (uint)index >= (uint)States.Length ||
                (uint)index >= (uint)AddedMassProfiles.Length)
            {
                return;
            }

            SubmarineKinematicState state = States[index];
            AddedMassProfileDTO profile = AddedMassProfiles[index];
            SubmarineAddedMassTuningDTO tuning = !Tuning.IsCreated || Tuning.Length == 0
                ? SubmarineAddedMassMath.DefaultTuning()
                : SubmarineAddedMassMath.SanitizeTuning(Tuning[0]);
            float damping = SubmarineAddedMassMath.ResolveRotationalDamping(in profile, state.TotalMassKg, GlobalQualityWeight, tuning.RotationalDampingScalar);
            state.AngularVelocity = SubmarineAddedMassMath.ApplyAngularDamping(state.AngularVelocity, damping, FixedDeltaTime);
            States[index] = state;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockAddedMassJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<AddedMassProfileDTO> AddedMassProfiles;
        [NoAlias] public NativeArray<SubmarineForceAccumulator> Forces;
        public uint Seed;
        public int VehicleCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)VehicleCount ||
                (uint)index >= (uint)AddedMassProfiles.Length ||
                (uint)index >= (uint)Forces.Length)
            {
                return;
            }

            uint hash = Seed ^ ((uint)index * 747796405u);
            float skew = 0.8f + (((hash >> 8) & 255u) * (0.4f / 255f));
            float3 linear = new float3(6400f * skew, 8200f, 2100f / skew);
            float3 angular = new float3(18000f, 92000f * skew, 104000f / skew);
            AddedMassProfileDTO profile = default;
            profile.LinearAddedMass = SubmarineAddedMassMath.BuildWorldTensor(linear, quaternion.identity);
            profile.AngularAddedMass = SubmarineAddedMassMath.BuildWorldTensor(angular, quaternion.identity);
            AddedMassProfiles[index] = profile;

            SubmarineForceAccumulator force = Forces[index];
            force.LinearForceWorld = new float3(5000f * skew, 1200f, 24000f);
            force.TorqueWorld = new float3(900f, 1800f * skew, 400f);
            force.Frame = Seed + (uint)index;
            Forces[index] = force;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct Submarine6DIntegratorJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<SubmarineKinematicState> States;
        [NoAlias] public NativeArray<SubmarineKinematicControl> Controls;
        [NoAlias] public NativeArray<SubmarinePidState> PidStates;
        [NoAlias] public NativeArray<SubmarineMassProperties> MassProperties;
        [NoAlias] public NativeArray<SubmarineForceAccumulator> Forces;
        [NoAlias] public NativeArray<SubmarineKinematicTelemetry> Telemetry;
        [ReadOnly, NoAlias] public NativeArray<AddedMassProfileDTO> AddedMassProfiles;
        [ReadOnly, NoAlias] public NativeArray<SubmarineAddedMassTuningDTO> Tuning;
        public SubmarineKinematicConfig Config;
        [ReadOnly, NoAlias] public NativeArray<float> DragLut;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // SignalBus owns the cavitation acoustic lane and this integrator receives a producer-only writer.
        // Unity cannot infer that external queue ownership from the field type, so its warning is a false
        // positive for this finite event emission path. [NoAlias] tells Burst the queue writer cannot alias
        // the submarine SoA state, control, PID, mass, force, telemetry, tuning, config DTO, or drag LUT buffers.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Rejected direct audio/runtime calls from the integrator because that would introduce managed side
        // effects and compile-wall coupling. Rejected atomics into a shared counter buffer because it would
        // serialize the vectorized integration lane. Rejected a second reduction job because acoustic events
        // are sparse and already have a typed SignalBus route.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Each scheduled submarine integration pass owns one CavitationWriter. The dispatcher fences the
        // returned JobHandle before draining, and the SignalBus lane is not resized or disposed while the job
        // can still execute. The job only enqueues finite payloads and never reads queue state.
        [NoAlias, NativeDisableContainerSafetyRestriction]
        public global::Hecton8.Core.MpscSignalRingBuffer<CavitationAcousticSignal>.ParallelWriter CavitationWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> CavitationWriterBudget;
        public float FixedDeltaTime;
        public float GlobalQualityWeight;
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
                (uint)index >= (uint)AddedMassProfiles.Length ||
                DragLut.Length == 0)
            {
                return;
            }

            SubmarineKinematicState state = States[index];
            SubmarineKinematicControl control = Controls[index];
            SubmarinePidState pid = PidStates[index];
            SubmarineMassProperties mass = MassProperties[index];
            SubmarineForceAccumulator force = Forces[index];
            SubmarineKinematicConfig config = Config;
            SubmarineAddedMassTuningDTO tuning = ResolveTuning(in Tuning);

            float dt = math.clamp(FixedDeltaTime, 0.001f, 0.05f);
            float quality = math.saturate(math.select(SubmarineDynamicsConstants.AuthoritativeQualityWeight, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            bool thermalDilation = (config.Flags & SubmarineDynamicsConstants.ConfigFlagThermalDilation) != 0;
            float updateFraction = ResolveAuthorityUpdateFraction();
            if (thermalDilation)
                updateFraction = math.min(updateFraction, 0.5f);
            bool skippedByCadence = !ShouldRunQualityCadence(Frame, index, updateFraction);
            float lowLodTargetSeconds = math.lerp(2f, 0f, updateFraction);
            pid.LowLodHoldSeconds = math.max(lowLodTargetSeconds, math.max(0f, pid.LowLodHoldSeconds - dt));
            bool lowMathLod = pid.LowLodHoldSeconds > 0.001f;
            bool runSlowSolvers = !skippedByCadence;

            if ((state.Flags & SubmarineDynamicsConstants.StateFlagInitialized) == 0u)
                InitializeState(ref state, in config, index);

            if (skippedByCadence)
            {
                ApplyDeadReckoning(ref state, in config, dt);
                pid.Frame = Frame;
                state.ShiftFrameId = Frame;
                WriteTelemetry(index, ref state, in force, ref Telemetry);
                States[index] = state;
                PidStates[index] = pid;
                Forces[index] = force;
                return;
            }

            float3 localPosition = ToLocal(in state.Aup, in config);
            state.LocalPosition = localPosition;
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

            AddedMassProfileDTO addedMassProfile = AddedMassProfiles[index];
            if (!SubmarineAddedMassMath.IsFinite(in addedMassProfile))
            {
                float fallbackWaterMass = math.max(1f, SafePositive(config.HullVolumeM3, 1f) * SafePositive(config.FluidDensityKgPerM3, MockFluidDensityGenerator.DefaultSeawaterDensityKgPerM3));
                addedMassProfile.LinearAddedMass = SubmarineAddedMassMath.BuildWorldTensor(new float3(0.72f, 0.86f, 0.18f) * fallbackWaterMass, rotation);
                addedMassProfile.AngularAddedMass = SubmarineAddedMassMath.BuildWorldTensor(ResolveInertiaTensor(fallbackWaterMass, centerOfMassLocal, floodMass, in config), rotation);
            }

            float matrixBlend = SubmarineAddedMassMath.ResolveTensorBlend(GlobalQualityWeight, pid.LowLodHoldSeconds, tuning.MatrixBlendBias);
            state.MathLod = matrixBlend < 0.001f ? (byte)0 : matrixBlend < 0.45f ? (byte)1 : matrixBlend < 0.85f ? (byte)2 : (byte)3;

            float pidOutput = pid.LastOutput;
            if (runSlowSolvers)
                pidOutput = SolveDepthPid(ref pid, depthMeters, control.TargetDepthMeters, in config, dt);
            pid.Frame = Frame;

            float3 forward = math.mul(rotation, new float3(0f, 0f, 1f));
            float3 throttleVector = math.lengthsq(control.ThrustLocal) > 0.0001f
                ? math.mul(rotation, SubmarineDynamicsSimdMath.NormalizeOrFallback(control.ThrustLocal, new float3(0f, 0f, 1f)))
                : forward;

            float throttle01 = math.saturate(control.Throttle01);
            float3 thrustWorld = throttleVector * (config.MaxThrustN * throttle01);
            float speedSq = math.lengthsq(state.LinearVelocity);
            speedSq = math.isfinite(speedSq) ? speedSq : 0f;
            // We must call the isolated pure logic static class
            // Derive inputs for the pure logic from the simulation variables
            float throttleToRpmScalar = 200f;
            float hullVolumePower = 0.33f;
            float propDiameterScalar = 0.4f;
            float propRPM = throttle01 * throttleToRpmScalar;
            float propDiameterM = math.pow(SafePositive(config.HullVolumeM3, 1f), hullVolumePower) * propDiameterScalar;
            float waterTemperature = 10f;
            float cavitationIndex = global::Hecton8.PureLogic.Systems.PropellerCavitationLimitCalculator.Compute(propRPM, depthMeters, waterTemperature, propDiameterM);

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
                if (!SignalBus<CavitationAcousticSignal>.TryEnqueueBounded(CavitationWriter, CavitationWriterBudget, signal))
                    state.Flags |= SubmarineDynamicsConstants.StateFlagSignalDrop;
            }

            float dragCoefficient = SampleDragLut(speedSq, in DragLut) * SafePositive(config.DragScale, 0.01f);
            float3 dragDirection = -SubmarineDynamicsSimdMath.NormalizeOrFallback(state.LinearVelocity, float3.zero);
            float3 cheapLinearDrag = -state.LinearVelocity * dragCoefficient * math.max(0.25f, totalMass * 0.015f);
            float3 polynomialDrag = dragDirection * speedSq * dragCoefficient;
            float3 dragWorld = math.lerp(cheapLinearDrag, polynomialDrag, quality);

            float targetDepth = math.max(1f, control.TargetDepthMeters);
            float depthRatio = math.saturate((depthMeters + 1f) / (targetDepth + 1f));
            float buoyancyEase = depthRatio * depthRatio * (3f - (2f * depthRatio));
            float hullVolume = SafePositive(config.HullVolumeM3, 1f);
            float fluidDensity = MockFluidDensityGenerator.SampleDensityKgPerM3(
                depthMeters,
                config.FluidDensityKgPerM3,
                Frame,
                GlobalQualityWeight);
            float buoyancyN = hullVolume * fluidDensity * SubmarineDynamicsConstants.Gravity * buoyancyEase;
            buoyancyN += pidOutput;
            float3 buoyancyWorld = new float3(0f, buoyancyN, 0f);
            float3 gravityWorld = new float3(0f, -SubmarineDynamicsConstants.Gravity * totalMass, 0f);

            float3 torqueWorld = ((force.Flags & SubmarineDynamicsConstants.ForceFlagGyroCorrection) != 0u && force.Frame == Frame)
                ? SafeFinite(force.TorqueWorld, float3.zero)
                : float3.zero;
            torqueWorld += math.mul(rotation, control.TorqueLocal * config.MaxTorqueNm);
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
            }

            if ((force.Flags & SubmarineDynamicsConstants.ForceFlagImpact) != 0u && force.ImpactMagnitude > 0f)
            {
                float3 impactNormal = SubmarineDynamicsSimdMath.NormalizeOrFallback(force.ImpactNormalWorld, -forward);
                if ((force.Flags & SubmarineDynamicsConstants.ForceFlagImpactNormalLocal) != 0u)
                    impactNormal = SubmarineDynamicsSimdMath.NormalizeOrFallback(math.mul(rotation, impactNormal), -forward);

                float impulse = force.ImpactMagnitude;
                state.LinearVelocity += SubmarineAddedMassMath.ResolveLinearVelocityDelta(impactNormal * impulse, totalMass, in addedMassProfile, matrixBlend);
                float3 angularImpulse = math.cross(force.ImpactPointLocal - centerOfMassLocal, impactNormal * impulse);
                state.AngularVelocity += SubmarineAddedMassMath.ResolveAngularVelocityDelta(angularImpulse, state.InertiaTensor, in addedMassProfile, matrixBlend);
                if (impulse > 45000f)
                    state.GyroDisabledSeconds = 2f;
            }

            float3 totalForce = thrustWorld + dragWorld + buoyancyWorld + gravityWorld;
            state.LinearVelocity += SubmarineAddedMassMath.ResolveLinearAcceleration(totalForce, totalMass, in addedMassProfile, matrixBlend) * dt;
            state.LinearVelocity = math.clamp(state.LinearVelocity, new float3(-90f), new float3(90f));
            localPosition += state.LinearVelocity * dt;
            state.Aup = SafeAup(config.LocalOriginAup) + new double3(localPosition);
            state.LocalPosition = localPosition;

            float3 inertia = math.max(new float3(1f), state.InertiaTensor);
            state.AngularVelocity += SubmarineAddedMassMath.ResolveAngularAcceleration(torqueWorld, inertia, in addedMassProfile, matrixBlend) * dt;
            float rotationalDamping = SubmarineAddedMassMath.ResolveRotationalDamping(in addedMassProfile, totalMass, GlobalQualityWeight, tuning.RotationalDampingScalar);
            state.AngularVelocity = SubmarineAddedMassMath.ApplyAngularDamping(state.AngularVelocity, rotationalDamping, dt);
            state.AngularVelocity = math.clamp(state.AngularVelocity, new float3(-2.8f), new float3(2.8f));
            quaternion deltaRotation = SubmarineDynamicsSimdMath.IntegrateAngularVelocityNoTrig(state.AngularVelocity, dt);
            rotation = NormalizeSafe(math.mul(deltaRotation, rotation));

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
                          IsFinite(buoyancyWorld) &&
                          SubmarineAddedMassMath.IsFinite(in addedMassProfile);
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

        private static SubmarineAddedMassTuningDTO ResolveTuning(in NativeArray<SubmarineAddedMassTuningDTO> tuning)
        {
            if (!tuning.IsCreated || tuning.Length == 0)
                return SubmarineAddedMassMath.DefaultTuning();

            return SubmarineAddedMassMath.SanitizeTuning(tuning[0]);
        }

        private static void ApplyDeadReckoning(ref SubmarineKinematicState state, in SubmarineKinematicConfig config, float dt)
        {
            state.LinearVelocity = SafeFinite(state.LinearVelocity, float3.zero);
            state.AngularVelocity = SafeFinite(state.AngularVelocity, float3.zero);
            state.LocalPosition = SafeFinite(state.LocalPosition + (state.LinearVelocity * dt), float3.zero);
            state.Aup = SafeAup(config.LocalOriginAup) + new double3(state.LocalPosition);
            quaternion rotation = NormalizeSafe(state.Rotation);
            quaternion deltaRotation = SubmarineDynamicsSimdMath.IntegrateAngularVelocityNoTrig(state.AngularVelocity, dt);
            rotation = NormalizeSafe(math.mul(deltaRotation, rotation));

            state.Rotation = rotation;
            state.Flags |= SubmarineDynamicsConstants.StateFlagInitialized;
        }

        private static float ResolveAuthorityUpdateFraction()
        {
            return 1f;
        }

        private static bool ShouldRunQualityCadence(uint frame, int index, float updateFraction)
        {
            float fraction = math.saturate(math.isfinite(updateFraction) ? updateFraction : 1f);
            if (fraction >= 0.999f)
                return true;

            uint hash = Mix(2166136261u, frame);
            hash = Mix(hash, (uint)index + 0x9E3779B9u);
            return Hash01(hash) <= fraction;
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
            return lenSq > 0.0001f ? new quaternion(value.value * math.rsqrt(math.max(lenSq, 0.0001f))) : quaternion.identity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(quaternion value)
        {
            return math.all(math.isfinite(value.value));
        }
    }
}
