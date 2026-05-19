using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Physiology
{
    /// <summary>
    /// Shared constants for the SHINOBU physiology vault contract.
    /// </summary>
    public static class ShinobuPhysiologyConstants
    {
        public const int TissueCompartmentCount = 16;
        public const int TissueCompartmentStrideBytes = 16;
        public const int TissueCompartmentBytesPerEntity = TissueCompartmentCount * TissueCompartmentStrideBytes;
        public const int TelemetryFrameCount = 300;
        public const int DefaultEntityCapacity = 64;
        public const int FrameJobBatchSize = 16;
        public const uint SourceHash = 0x53483231u; // SH21
        public const float AtmosphericPressureAtSurfaceAtm = 1f;
        public const float NitrogenFraction = 0.7902f;
        public const float OxygenDeathThreshold = 0.0001f;
        public const float MaxSimulationStepSeconds = 0.25f;
    }

    /// <summary>
    /// Bit allocation for <see cref="PhysiologyDTO.ActiveTraumaMask"/>.
    /// </summary>
    public static class ShinobuTraumaBits
    {
        public const uint Laceration = 1u << 0;
        public const uint Concussion = 1u << 1;
        public const uint Burn = 1u << 2;
        public const uint Barotrauma = 1u << 3;
    }

    /// <summary>
    /// Mock inventory bit allocation consumed by hypothermia math.
    /// </summary>
    public static class ShinobuInventoryBits
    {
        public const uint ThermalSuitUpgrade = 1u << 0;
    }

    /// <summary>
    /// Scalar status bits emitted by physiology jobs.
    /// </summary>
    public static class ShinobuPhysiologyFlags
    {
        public const uint Bends = 1u << 0;
        public const uint Narcosis = 1u << 1;
        public const uint Hypothermia = 1u << 2;
        public const uint OxygenCritical = 1u << 3;
        public const uint FatalOxygen = 1u << 4;
        public const uint InvalidMath = 1u << 5;
        public const uint EmergencyMockCoefficients = 1u << 6;
        public const uint CsvOverride = 1u << 7;
        public const uint AdrenalineSeen = 1u << 8;
        public const uint AdrenalineCrash = 1u << 9;
        public const uint HyperbaricOverride = 1u << 10;
        public const uint FatalBends = 1u << 11;
    }

    /// <summary>
    /// Runtime math level used by the decompression kernel.
    /// </summary>
    public enum ShinobuMathLod : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3
    }

    /// <summary>
    /// Player or humanoid physiology row. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PhysiologyDTO
    {
        [FieldOffset(0)] public float BloodOxygen;
        [FieldOffset(4)] public float TissueNitrogen;
        [FieldOffset(8)] public float CoreTemperature;
        [FieldOffset(12)] public uint ActiveTraumaMask;
        [FieldOffset(16)] public float HeartRate;
        [FieldOffset(20)] public float Adrenaline;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

    /// <summary>
    /// Fixed-buffer 16-compartment Haldane decompression state. Size: 80 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 80)]
    public unsafe struct DecompressionStateDTO
    {
        public fixed float TissueTensions[16];
        public float AmbientPressure;
        public float AscentRate;
        public ulong _pad0;
    }

    /// <summary>
    /// One active Haldanean tissue compartment row. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct TissueCompartmentDTO
    {
        [FieldOffset(0)] public float NitrogenTension;
        [FieldOffset(4)] public float Halftime;
        [FieldOffset(8)] public float MValue;
        [FieldOffset(12)] public uint Flags;
    }

    public static class ShinobuPhysiologyLayoutGuards
    {
        public static bool ValidateTissueCompartmentLayout()
        {
            return UnsafeUtility.SizeOf<TissueCompartmentDTO>() == ShinobuPhysiologyConstants.TissueCompartmentStrideBytes &&
                   Marshal.OffsetOf<TissueCompartmentDTO>(nameof(TissueCompartmentDTO.NitrogenTension)).ToInt32() == 0 &&
                   Marshal.OffsetOf<TissueCompartmentDTO>(nameof(TissueCompartmentDTO.Halftime)).ToInt32() == 4 &&
                   Marshal.OffsetOf<TissueCompartmentDTO>(nameof(TissueCompartmentDTO.MValue)).ToInt32() == 8 &&
                   Marshal.OffsetOf<TissueCompartmentDTO>(nameof(TissueCompartmentDTO.Flags)).ToInt32() == 12;
        }
    }

    /// <summary>
    /// One 16-byte coefficient row for Haldane tissue math.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct HaldaneTissueCoefficientDTO
    {
        [FieldOffset(0)] public float HalfTimeSeconds;
        [FieldOffset(4)] public float K;
        [FieldOffset(8)] public float MValueRatio;
        [FieldOffset(12)] public float NitrogenFraction;
    }

    /// <summary>
    /// Human-tunable physiology constants. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PhysiologyTuningDTO
    {
        [FieldOffset(0)] public float BaseO2DrainPerSecond;
        [FieldOffset(4)] public float NitrogenUptakeRate;
        [FieldOffset(8)] public float AdrenalineDecaySeconds;
        [FieldOffset(12)] public float HypothermiaCoolingRate;
        [FieldOffset(16)] public float MedicalPurgePerSecond;
        [FieldOffset(20)] public float HeartRateBase;
        [FieldOffset(24)] public float HeartRateTraumaSpike;
        [FieldOffset(28)] public float ToxemiaO2Penalty;
        [FieldOffset(32)] public float ThermalSuitInsulation01;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public float NarcosisStartAtm;
        [FieldOffset(44)] public float NarcosisFullAtm;
        [FieldOffset(48)] public float BendsRiskScale;
        [FieldOffset(52)] public float HaldaneTimeScale;
        [FieldOffset(56)] public float MinOxygen01;
        [FieldOffset(60)] public uint Version;
    }

    /// <summary>
    /// Mock environment payload written before physiology jobs. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct MockEnvironmentVitalsSignal
    {
        [FieldOffset(0)] public float DepthMeters;
        [FieldOffset(4)] public float AmbientPressureAtm;
        [FieldOffset(8)] public float AmbientTemperatureCelsius;
        [FieldOffset(12)] public float SystemHealthIndex01;
        [FieldOffset(16)] public uint InventoryMask;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public float AscentRateMetersPerSecond;
    }

    /// <summary>
    /// Mock pressure override for vacuum tests. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MockPressureSignal
    {
        public const uint ActiveFlag = 1u << 0;
        public const uint HabitatOverrideFlag = 1u << 1;
        public const uint HyperbaricTreatmentFlag = 1u << 2;

        [FieldOffset(0)] public float DepthMeters;
        [FieldOffset(4)] public float AmbientPressureAtm;
        [FieldOffset(8)] public float AscentRateMetersPerSecond;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public float AmbientTemperatureCelsius;
        [FieldOffset(24)] public uint InventoryMask;
    }

    /// <summary>
    /// Synthetic dive profile sample for deterministic editor/runtime smoke tests. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DiveProfileSampleDTO
    {
        [FieldOffset(0)] public float TimeSeconds;
        [FieldOffset(4)] public float DepthMeters;
        [FieldOffset(8)] public float AmbientPressureAtm;
        [FieldOffset(12)] public float AscentRateMetersPerSecond;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint ProfileHash;
        [FieldOffset(28)] public uint SampleIndex;
    }

    /// <summary>
    /// Mock combat damage packet mapped into the trauma bitmask. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MockCombatDamageSignal
    {
        [FieldOffset(0)] public int TraumaType;
        [FieldOffset(4)] public float Severity01;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public uint SourceHash;
    }

    /// <summary>
    /// Mock predator aggro packet for endocrine response. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MockPredatorAggroSignal
    {
        [FieldOffset(0)] public float Aggro01;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint SourceHash;
    }

    /// <summary>
    /// Mock toxemia delta packet. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MockToxemiaSignal
    {
        [FieldOffset(0)] public float Delta01;
        [FieldOffset(4)] public float Absolute01;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Flags;
    }

    /// <summary>
    /// Mock medical item signal that starts slow toxemia purge. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MockMedicalItemUsedSignal
    {
        [FieldOffset(0)] public float PurgeStrength01;
        [FieldOffset(4)] public uint ItemHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Flags;
    }

    /// <summary>
    /// Scalar outputs consumed by UI, audio, kinematics, and shader fakes. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PhysiologyScalarsDTO
    {
        [FieldOffset(0)] public float NarcosisSeverity;
        [FieldOffset(4)] public float HypothermiaShiver;
        [FieldOffset(8)] public float SwimSpeedBonus;
        [FieldOffset(12)] public float FatigueMultiplier;
        [FieldOffset(16)] public float Toxemia;
        [FieldOffset(20)] public float BendsRisk;
        [FieldOffset(24)] public float OxygenDrainPerSecond;
        [FieldOffset(28)] public float HeartbeatPhase;
        [FieldOffset(32)] public float MedicalPurgeSecondsRemaining;
        [FieldOffset(36)] public float MedicalPurgeStrength01;
        [FieldOffset(40)] public uint StatusFlags;
        [FieldOffset(44)] public uint TissueOverMValueMask;
        [FieldOffset(48)] public uint LastPulseFrame;
        [FieldOffset(52)] public uint PulseCount;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    /// <summary>
    /// Diegetic HUD export row. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct VitalsExportDTO
    {
        [FieldOffset(0)] public float BloodOxygen;
        [FieldOffset(4)] public float CoreTemperature;
        [FieldOffset(8)] public float DepthMeters;
        [FieldOffset(12)] public uint StatusMask;
    }

    /// <summary>
    /// Fixed 300-frame physiology black-box entry. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PhysiologyTelemetryEntry
    {
        [FieldOffset(0)] public ulong StateHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint ActiveTraumaMask;
        [FieldOffset(16)] public float BloodOxygen;
        [FieldOffset(20)] public float NitrogenLoad;
        [FieldOffset(24)] public float CoreTemperature;
        [FieldOffset(28)] public float AmbientPressureAtm;
        [FieldOffset(32)] public float NarcosisSeverity;
        [FieldOffset(36)] public float SupersaturationScalar;
        [FieldOffset(40)] public float HeartRate;
        [FieldOffset(44)] public float Adrenaline;
        [FieldOffset(48)] public uint FatalFlags;
        [FieldOffset(52)] public uint TissueOverMValueMask;
        [FieldOffset(56)] public float DepthMeters;
        [FieldOffset(60)] public float ExecutionMicroseconds;
    }

    /// <summary>
    /// Per-entity pulse integrator state. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct CardiacPulseStateDTO
    {
        [FieldOffset(0)] public float Phase;
        [FieldOffset(4)] public float LastHeartRate;
        [FieldOffset(8)] public uint PulseCount;
        [FieldOffset(12)] public uint Flags;
    }

    /// <summary>
    /// Heartbeat packet emitted into a typed SignalBus lane. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct CardiacPulseSignal : ISignal
    {
        public const uint LaneHash = 0x4350554Cu; // CPUL
        public const byte FlagAdrenaline = 1 << 0;
        public const byte FlagOxygenCritical = 1 << 1;

        [FieldOffset(0)] public float HeartRate;
        [FieldOffset(4)] public float Adrenaline01;
        [FieldOffset(8)] public float BloodOxygen01;
        [FieldOffset(12)] public float Toxemia01;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint SourceHash;
        [FieldOffset(24)] public uint PulseCount;
        [FieldOffset(28)] public byte Flags;
    }

    /// <summary>
    /// Hot-reloaded CSV key/value override row. Size: 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct BiologyConstantOverrideDTO
    {
        [FieldOffset(0)] public ulong KeyHash;
        [FieldOffset(8)] public float Value;
        [FieldOffset(12)] public uint Flags;
    }
}
