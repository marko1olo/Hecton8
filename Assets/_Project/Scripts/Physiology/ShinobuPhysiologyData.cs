using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
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
        public const uint SourceHash = PhysiologyStateSignal.SourceShinobuPhysiology;
        public const uint PlayerTargetHash = 0x504C5952u; // PLYR fallback when entity hash is not published.
        public const uint CombatDamageTypeToxic = 1u << 5;
        public const byte GasToxicitySignalCause = PhysiologyStateSignal.CauseGasToxicity;
        public const BufferID BreathingGasFractionsBuffer = (BufferID)70214;
        public const BufferID GasPhysiologyTuningBuffer = (BufferID)70215;
        public const BufferID GasPhysiologyStatesBuffer = (BufferID)70239;
        public const float AtmosphericPressureAtSurfaceAtm = 1f;
        public const float OxygenFraction = 0.2095f;
        public const float NitrogenFraction = 0.7902f;
        public const float CarbonDioxideFraction = 0.0004f;
        public const float HelioxOxygenFraction = 0.10f;
        public const float HelioxNitrogenFraction = 0f;
        public const float HelioxTransitionStartMeters = 60f;
        public const float HelioxTransitionSpanMeters = 120f;
        public const float SurfaceOxygenPartialPressureAtm = AtmosphericPressureAtSurfaceAtm * OxygenFraction;
        public const float SurfaceNitrogenPartialPressureAtm = AtmosphericPressureAtSurfaceAtm * NitrogenFraction;
        public const float HypoxiaPartialPressureAtm = 0.16f;
        public const float AnoxiaPartialPressureAtm = 0.08f;
        public const float CnsToxicityStartAtm = 1.4f;
        public const float CnsToxicityExtremeAtm = 2.0f;
        public const float CarbonDioxideToxicityStartAtm = 0.05f;
        public const float CarbonDioxideToxicityFullAtm = 0.10f;
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
        public const uint Hypoxia = 1u << 12;
        public const uint Hyperoxia = 1u << 13;
        public const uint CarbonDioxideToxicity = 1u << 14;
        public const uint CnsOxygenToxicity = 1u << 15;
        public const uint FatalGasToxicity = 1u << 16;
        public const uint BreathingGasHeliox = 1u << 17;
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
    /// Dalton-law gas physiology row. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct GasPhysiologyStateDTO
    {
        [FieldOffset(0)] public float OxygenPartialPressure;
        [FieldOffset(4)] public float NitrogenPartialPressure;
        [FieldOffset(8)] public float CarbonDioxidePartialPressure;
        [FieldOffset(12)] public float CnsToxicity01;
        [FieldOffset(16)] public float NarcosisLevel01;
        [FieldOffset(20)] public float StaminaDrainRate;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    /// <summary>
    /// Breathing gas fraction row. O2/N2/CO2 are used by Dalton's law; reserve carries helium or inert remainder. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BreathingGasFractionsDTO
    {
        [FieldOffset(0)] public float OxygenFraction;
        [FieldOffset(4)] public float NitrogenFraction;
        [FieldOffset(8)] public float CarbonDioxideFraction;
        [FieldOffset(12)] public float InertReserveFraction;
        [FieldOffset(16)] public uint GasHash;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

    /// <summary>
    /// Vault-backed gas physiology tuning. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct GasPhysiologyTuningDTO
    {
        [FieldOffset(0)] public float HypoxiaPartialPressureAtm;
        [FieldOffset(4)] public float AnoxiaPartialPressureAtm;
        [FieldOffset(8)] public float CnsToxicityStartAtm;
        [FieldOffset(12)] public float CnsToxicityExtremeAtm;
        [FieldOffset(16)] public float CnsAccumulationRate;
        [FieldOffset(20)] public float CnsExtremeRate;
        [FieldOffset(24)] public float CnsRecoveryPerSecond;
        [FieldOffset(28)] public float CnsRecoveryPressureScale;
        [FieldOffset(32)] public float NarcosisStartAtm;
        [FieldOffset(36)] public float NarcosisFullAtm;
        [FieldOffset(40)] public float CarbonDioxideToxicityStartAtm;
        [FieldOffset(44)] public float CarbonDioxideToxicityFullAtm;
        [FieldOffset(48)] public float ToxicDamageStart01;
        [FieldOffset(52)] public float ToxicDamagePerSecond;
        [FieldOffset(56)] public float StaminaStressScale;
        [FieldOffset(60)] public uint Version;
    }

    /// <summary>
    /// Fixed-buffer 16-compartment Haldane decompression state. Size: 80 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public unsafe struct DecompressionStateDTO
    {
        [FieldOffset(0)] public fixed float TissueTensions[16];
        [FieldOffset(64)] public float AmbientPressure;
        [FieldOffset(68)] public float AscentRate;
        [FieldOffset(72)] public ulong _pad0;
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
        public static bool ValidatePhysiologyLayouts()
        {
            return ValidateTissueCompartmentLayout() &&
                   ValidateGasPhysiologyStateLayout() &&
                   ValidateTelemetryAndSignalLayouts();
        }

        public static bool ValidateTissueCompartmentLayout()
        {
            return UnsafeUtility.SizeOf<TissueCompartmentDTO>() == ShinobuPhysiologyConstants.TissueCompartmentStrideBytes &&
                   Marshal.OffsetOf<TissueCompartmentDTO>(nameof(TissueCompartmentDTO.NitrogenTension)).ToInt32() == 0 &&
                   Marshal.OffsetOf<TissueCompartmentDTO>(nameof(TissueCompartmentDTO.Halftime)).ToInt32() == 4 &&
                   Marshal.OffsetOf<TissueCompartmentDTO>(nameof(TissueCompartmentDTO.MValue)).ToInt32() == 8 &&
                   Marshal.OffsetOf<TissueCompartmentDTO>(nameof(TissueCompartmentDTO.Flags)).ToInt32() == 12;
        }

        public static bool ValidateGasPhysiologyStateLayout()
        {
            return UnsafeUtility.SizeOf<GasPhysiologyStateDTO>() == 32 &&
                   Marshal.OffsetOf<GasPhysiologyStateDTO>(nameof(GasPhysiologyStateDTO.OxygenPartialPressure)).ToInt32() == 0 &&
                   Marshal.OffsetOf<GasPhysiologyStateDTO>(nameof(GasPhysiologyStateDTO.NitrogenPartialPressure)).ToInt32() == 4 &&
                   Marshal.OffsetOf<GasPhysiologyStateDTO>(nameof(GasPhysiologyStateDTO.CarbonDioxidePartialPressure)).ToInt32() == 8 &&
                   Marshal.OffsetOf<GasPhysiologyStateDTO>(nameof(GasPhysiologyStateDTO.CnsToxicity01)).ToInt32() == 12 &&
                   Marshal.OffsetOf<GasPhysiologyStateDTO>(nameof(GasPhysiologyStateDTO.NarcosisLevel01)).ToInt32() == 16 &&
                   Marshal.OffsetOf<GasPhysiologyStateDTO>(nameof(GasPhysiologyStateDTO.StaminaDrainRate)).ToInt32() == 20 &&
                   Marshal.OffsetOf<GasPhysiologyStateDTO>(nameof(GasPhysiologyStateDTO.Flags)).ToInt32() == 24 &&
                   Marshal.OffsetOf<GasPhysiologyStateDTO>(nameof(GasPhysiologyStateDTO._pad0)).ToInt32() == 28 &&
                   UnsafeUtility.SizeOf<BreathingGasFractionsDTO>() == 32 &&
                   Marshal.OffsetOf<BreathingGasFractionsDTO>(nameof(BreathingGasFractionsDTO.OxygenFraction)).ToInt32() == 0 &&
                   Marshal.OffsetOf<BreathingGasFractionsDTO>(nameof(BreathingGasFractionsDTO.NitrogenFraction)).ToInt32() == 4 &&
                   Marshal.OffsetOf<BreathingGasFractionsDTO>(nameof(BreathingGasFractionsDTO.CarbonDioxideFraction)).ToInt32() == 8 &&
                   Marshal.OffsetOf<BreathingGasFractionsDTO>(nameof(BreathingGasFractionsDTO.InertReserveFraction)).ToInt32() == 12 &&
                   Marshal.OffsetOf<BreathingGasFractionsDTO>(nameof(BreathingGasFractionsDTO.GasHash)).ToInt32() == 16 &&
                   Marshal.OffsetOf<BreathingGasFractionsDTO>(nameof(BreathingGasFractionsDTO.Flags)).ToInt32() == 20 &&
                   UnsafeUtility.SizeOf<GasPhysiologyTuningDTO>() == 64 &&
                   Marshal.OffsetOf<GasPhysiologyTuningDTO>(nameof(GasPhysiologyTuningDTO.HypoxiaPartialPressureAtm)).ToInt32() == 0 &&
                   Marshal.OffsetOf<GasPhysiologyTuningDTO>(nameof(GasPhysiologyTuningDTO.AnoxiaPartialPressureAtm)).ToInt32() == 4 &&
                   Marshal.OffsetOf<GasPhysiologyTuningDTO>(nameof(GasPhysiologyTuningDTO.CnsToxicityStartAtm)).ToInt32() == 8 &&
                   Marshal.OffsetOf<GasPhysiologyTuningDTO>(nameof(GasPhysiologyTuningDTO.CnsToxicityExtremeAtm)).ToInt32() == 12 &&
                   Marshal.OffsetOf<GasPhysiologyTuningDTO>(nameof(GasPhysiologyTuningDTO.CnsAccumulationRate)).ToInt32() == 16 &&
                   Marshal.OffsetOf<GasPhysiologyTuningDTO>(nameof(GasPhysiologyTuningDTO.CnsExtremeRate)).ToInt32() == 20 &&
                   Marshal.OffsetOf<GasPhysiologyTuningDTO>(nameof(GasPhysiologyTuningDTO.CnsRecoveryPerSecond)).ToInt32() == 24 &&
                   Marshal.OffsetOf<GasPhysiologyTuningDTO>(nameof(GasPhysiologyTuningDTO.CnsRecoveryPressureScale)).ToInt32() == 28 &&
                   Marshal.OffsetOf<GasPhysiologyTuningDTO>(nameof(GasPhysiologyTuningDTO.NarcosisStartAtm)).ToInt32() == 32 &&
                   Marshal.OffsetOf<GasPhysiologyTuningDTO>(nameof(GasPhysiologyTuningDTO.NarcosisFullAtm)).ToInt32() == 36 &&
                   Marshal.OffsetOf<GasPhysiologyTuningDTO>(nameof(GasPhysiologyTuningDTO.CarbonDioxideToxicityStartAtm)).ToInt32() == 40 &&
                   Marshal.OffsetOf<GasPhysiologyTuningDTO>(nameof(GasPhysiologyTuningDTO.CarbonDioxideToxicityFullAtm)).ToInt32() == 44 &&
                   Marshal.OffsetOf<GasPhysiologyTuningDTO>(nameof(GasPhysiologyTuningDTO.ToxicDamageStart01)).ToInt32() == 48 &&
                   Marshal.OffsetOf<GasPhysiologyTuningDTO>(nameof(GasPhysiologyTuningDTO.ToxicDamagePerSecond)).ToInt32() == 52 &&
                   Marshal.OffsetOf<GasPhysiologyTuningDTO>(nameof(GasPhysiologyTuningDTO.StaminaStressScale)).ToInt32() == 56 &&
                   Marshal.OffsetOf<GasPhysiologyTuningDTO>(nameof(GasPhysiologyTuningDTO.Version)).ToInt32() == 60;
        }

        public static bool ValidateTelemetryAndSignalLayouts()
        {
            return UnsafeUtility.SizeOf<PhysiologyTelemetryEntry>() == 64 &&
                   Marshal.OffsetOf<PhysiologyTelemetryEntry>(nameof(PhysiologyTelemetryEntry.StateHash)).ToInt32() == 0 &&
                   Marshal.OffsetOf<PhysiologyTelemetryEntry>(nameof(PhysiologyTelemetryEntry.Frame)).ToInt32() == 8 &&
                   Marshal.OffsetOf<PhysiologyTelemetryEntry>(nameof(PhysiologyTelemetryEntry.ActiveTraumaMask)).ToInt32() == 12 &&
                   Marshal.OffsetOf<PhysiologyTelemetryEntry>(nameof(PhysiologyTelemetryEntry.BloodOxygen)).ToInt32() == 16 &&
                   Marshal.OffsetOf<PhysiologyTelemetryEntry>(nameof(PhysiologyTelemetryEntry.NitrogenLoad)).ToInt32() == 20 &&
                   Marshal.OffsetOf<PhysiologyTelemetryEntry>(nameof(PhysiologyTelemetryEntry.CoreTemperature)).ToInt32() == 24 &&
                   Marshal.OffsetOf<PhysiologyTelemetryEntry>(nameof(PhysiologyTelemetryEntry.AmbientPressureAtm)).ToInt32() == 28 &&
                   Marshal.OffsetOf<PhysiologyTelemetryEntry>(nameof(PhysiologyTelemetryEntry.NarcosisSeverity)).ToInt32() == 32 &&
                   Marshal.OffsetOf<PhysiologyTelemetryEntry>(nameof(PhysiologyTelemetryEntry.SupersaturationScalar)).ToInt32() == 36 &&
                   Marshal.OffsetOf<PhysiologyTelemetryEntry>(nameof(PhysiologyTelemetryEntry.HeartRate)).ToInt32() == 40 &&
                   Marshal.OffsetOf<PhysiologyTelemetryEntry>(nameof(PhysiologyTelemetryEntry.Adrenaline)).ToInt32() == 44 &&
                   Marshal.OffsetOf<PhysiologyTelemetryEntry>(nameof(PhysiologyTelemetryEntry.FatalFlags)).ToInt32() == 48 &&
                    Marshal.OffsetOf<PhysiologyTelemetryEntry>(nameof(PhysiologyTelemetryEntry.TissueOverMValueMask)).ToInt32() == 52 &&
                    Marshal.OffsetOf<PhysiologyTelemetryEntry>(nameof(PhysiologyTelemetryEntry.DepthMeters)).ToInt32() == 56 &&
                    Marshal.OffsetOf<PhysiologyTelemetryEntry>(nameof(PhysiologyTelemetryEntry.ExecutionMicroseconds)).ToInt32() == 60 &&
                    UnsafeUtility.SizeOf<PhysiologyStateSignal>() == 64 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.PlayerStress01)).ToInt32() == 0 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.O2DrainMultiplier)).ToInt32() == 4 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.Recovery01)).ToInt32() == 8 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.Frame)).ToInt32() == 12 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.Cause)).ToInt32() == 16 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.Flags)).ToInt32() == 17 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.GasCnsSeverity)).ToInt32() == 18 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.GasCarbonDioxideSeverity)).ToInt32() == 19 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.Supersaturation01)).ToInt32() == 20 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.Narcosis01)).ToInt32() == 24 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.AmbientPressureAtm)).ToInt32() == 28 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.NitrogenLoadAtm)).ToInt32() == 32 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.AscentRateMetersPerSecond)).ToInt32() == 36 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.TissueOverMValueMask)).ToInt32() == 40 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.SourceHash)).ToInt32() == 44 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.EntityIndex)).ToInt32() == 48 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.ActiveCompartments)).ToInt32() == 52 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.FatalSeverity)).ToInt32() == 53 &&
                    Marshal.OffsetOf<PhysiologyStateSignal>(nameof(PhysiologyStateSignal.StatusFlags)).ToInt32() == 56 &&
                    UnsafeUtility.SizeOf<MockPressureSignal>() == 32 &&
                   Marshal.OffsetOf<MockPressureSignal>(nameof(MockPressureSignal.DepthMeters)).ToInt32() == 0 &&
                   Marshal.OffsetOf<MockPressureSignal>(nameof(MockPressureSignal.AmbientPressureAtm)).ToInt32() == 4 &&
                   Marshal.OffsetOf<MockPressureSignal>(nameof(MockPressureSignal.AscentRateMetersPerSecond)).ToInt32() == 8 &&
                   Marshal.OffsetOf<MockPressureSignal>(nameof(MockPressureSignal.Frame)).ToInt32() == 12 &&
                   Marshal.OffsetOf<MockPressureSignal>(nameof(MockPressureSignal.Flags)).ToInt32() == 16 &&
                   Marshal.OffsetOf<MockPressureSignal>(nameof(MockPressureSignal.AmbientTemperatureCelsius)).ToInt32() == 20 &&
                   Marshal.OffsetOf<MockPressureSignal>(nameof(MockPressureSignal.InventoryMask)).ToInt32() == 24 &&
                   Marshal.OffsetOf<MockPressureSignal>(nameof(MockPressureSignal._pad0)).ToInt32() == 28 &&
                   UnsafeUtility.SizeOf<MockCombatDamageSignal>() == 32 &&
                   Marshal.OffsetOf<MockCombatDamageSignal>(nameof(MockCombatDamageSignal.TraumaType)).ToInt32() == 0 &&
                   Marshal.OffsetOf<MockCombatDamageSignal>(nameof(MockCombatDamageSignal.Severity01)).ToInt32() == 4 &&
                   Marshal.OffsetOf<MockCombatDamageSignal>(nameof(MockCombatDamageSignal.Frame)).ToInt32() == 8 &&
                   Marshal.OffsetOf<MockCombatDamageSignal>(nameof(MockCombatDamageSignal.Flags)).ToInt32() == 12 &&
                   Marshal.OffsetOf<MockCombatDamageSignal>(nameof(MockCombatDamageSignal.SourceHash)).ToInt32() == 16 &&
                   Marshal.OffsetOf<MockCombatDamageSignal>(nameof(MockCombatDamageSignal._pad0)).ToInt32() == 20 &&
                   Marshal.OffsetOf<MockCombatDamageSignal>(nameof(MockCombatDamageSignal._pad1)).ToInt32() == 24 &&
                   Marshal.OffsetOf<MockCombatDamageSignal>(nameof(MockCombatDamageSignal._pad2)).ToInt32() == 28 &&
                   UnsafeUtility.SizeOf<CardiacPulseSignal>() == 32 &&
                   Marshal.OffsetOf<CardiacPulseSignal>(nameof(CardiacPulseSignal.HeartRate)).ToInt32() == 0 &&
                   Marshal.OffsetOf<CardiacPulseSignal>(nameof(CardiacPulseSignal.Adrenaline01)).ToInt32() == 4 &&
                   Marshal.OffsetOf<CardiacPulseSignal>(nameof(CardiacPulseSignal.BloodOxygen01)).ToInt32() == 8 &&
                   Marshal.OffsetOf<CardiacPulseSignal>(nameof(CardiacPulseSignal.Toxemia01)).ToInt32() == 12 &&
                   Marshal.OffsetOf<CardiacPulseSignal>(nameof(CardiacPulseSignal.Frame)).ToInt32() == 16 &&
                   Marshal.OffsetOf<CardiacPulseSignal>(nameof(CardiacPulseSignal.SourceHash)).ToInt32() == 20 &&
                   Marshal.OffsetOf<CardiacPulseSignal>(nameof(CardiacPulseSignal.PulseCount)).ToInt32() == 24 &&
                   Marshal.OffsetOf<CardiacPulseSignal>(nameof(CardiacPulseSignal.Flags)).ToInt32() == 28 &&
                   Marshal.OffsetOf<CardiacPulseSignal>(nameof(CardiacPulseSignal._pad0)).ToInt32() == 29 &&
                   Marshal.OffsetOf<CardiacPulseSignal>(nameof(CardiacPulseSignal._pad1)).ToInt32() == 30 &&
                   Marshal.OffsetOf<CardiacPulseSignal>(nameof(CardiacPulseSignal._pad2)).ToInt32() == 31;
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
        [FieldOffset(28)] public uint _pad0;
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
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public uint _pad1;
        [FieldOffset(28)] public uint _pad2;
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
        [FieldOffset(29)] public byte _pad0;
        [FieldOffset(30)] public byte _pad1;
        [FieldOffset(31)] public byte _pad2;
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
