using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Physiology;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Physiology
{
    public static class ShinobuMetabolismConstants
    {
        public const int DefaultEntityCapacity = 5000;
        public const int MaxSpeciesRules = 128;
        public const int MaxSuitThermalProfiles = 32;
        public const int TelemetryFrameCount = 300;
        public const int FrameJobBatchSize = 64;
        public const int CsvMaxBytes = 32768;
        public const int ShaderGlobalsStrideBytes = 64;
        public const float DispatcherSlowTickSeconds = 0.1f;
        public const float NominalSlowTickSeconds = DispatcherSlowTickSeconds;
        public const float MaxAccumulatedDeltaSeconds = 6f;
        public const uint SourceHash = 0x53483135u; // SH15

        public const int MetabolismStateSizeBytes = ShinobuMetabolismVaultContract.MetabolicStateSizeBytes;
        public const int SpeciesRuleSizeBytes = 64;
        public const int SuitThermalProfileSizeBytes = 32;
        public const int TuningSizeBytes = 64;
        public const int TelemetrySizeBytes = 64;
        public const int DetailTelemetrySizeBytes = 64;
        public const int ExposureSignalSizeBytes = 64;
        public const int ShaderGlobalsSizeBytes = 64;
        public const int ChemicalMirrorSizeBytes = 64;
        public const int PhysiologySignalsPerEntity = 3;
        public const int MetabolicExposureSignalsPerEntity = 4;
        public const int MetabolicExposureSignalSlotToxic = 3;
        public const int ChemicalGridAxisX = 48;
        public const int ChemicalGridAxisY = 16;
        public const int ChemicalGridAxisZ = 48;
        public const int ChemicalGridCellCount = ChemicalGridAxisX * ChemicalGridAxisY * ChemicalGridAxisZ;
        public const float ChemicalDefaultCellSizeMeters = 8f;

        public const BufferID MetabolismStatesBuffer = BufferID.ShinobuMetabolismStates;
        public const BufferID MetabolismEntityAupsBuffer = (BufferID)70266;
        public const BufferID MetabolismExertionBuffer = (BufferID)70267;
        public const BufferID MetabolismSpeciesRulesBuffer = (BufferID)70268;
        public const BufferID MetabolismRuleIndicesBuffer = (BufferID)70269;
        public const BufferID MetabolismTelemetryRingBuffer = (BufferID)70270;
        public const BufferID MetabolismTuningBuffer = (BufferID)70271;
        public const BufferID MetabolismToxinSamplesBuffer = (BufferID)70272;
        public const BufferID MetabolismPhysiologySignalsBuffer = (BufferID)70274;
        public const BufferID MetabolismExposureSignalsBuffer = (BufferID)70275;
        public const BufferID MetabolismDetailTelemetryRingBuffer = (BufferID)73340;
        public const BufferID MetabolismSuitThermalProfilesBuffer = (BufferID)73341;
        public const BufferID MetabolismSuitProfileIndicesBuffer = (BufferID)73342;
        public const BufferID ChemicalPublishedGridReadbackBuffer = (BufferID)71152;
        public const BufferID ChemicalOverlayGridReadbackBuffer = (BufferID)71153;
        public const BufferID ChemicalTuningReadbackBuffer = (BufferID)71161;
        public const BufferID ChemicalTelemetryReadbackBuffer = (BufferID)71162;
        public const BufferID ChemicalTelemetryCursorReadbackBuffer = (BufferID)71163;

        public const uint StandardSuitHash = 0x53554954u; // SUIT
        public const uint ReinforcedSuitHash = 0x52455354u; // REST
        public const uint ExosuitHash = 0x5052574Eu; // PRWN
        public const uint SubmarineHullHash = 0x48554C4Cu; // HULL
        public const uint SuitProfileHashStandardWetsuit = 0x369A3586u;
        public const uint SuitProfileHashReinforcedSuit = 0xB31630FEu;
        public const uint SuitProfileHashReinforcedWetsuit = 0x962FFCA4u;
        public const uint SuitProfileHashThermalPrawnSuit = 0xC8754555u;
        public const uint SuitProfileHashSubmarineHull = 0x6C43821Fu;

        public const byte PhysiologyCauseStarvation = 11;
        public const byte PhysiologyCauseDehydration = 12;
        public const byte PhysiologyCauseHypothermia = 13;
    }

    public static class ShinobuMetabolismFlags
    {
        public const uint Starving = ShinobuMetabolismVaultContract.FlagStarving;
        public const uint Dehydrated = ShinobuMetabolismVaultContract.FlagDehydrated;
        public const uint Hypothermia = ShinobuMetabolismVaultContract.FlagHypothermia;
        public const uint Toxic = ShinobuMetabolismVaultContract.FlagToxic;
        public const uint InvalidMath = ShinobuMetabolismVaultContract.FlagInvalidMath;
        public const uint MockEntity = ShinobuMetabolismVaultContract.FlagMockEntity;
        public const uint ThermalSampled = 1u << 6;
        public const uint CsvProfile = 1u << 7;
        public const uint ChemicalSampled = 1u << 8;
        public const uint Fatigue = ShinobuMetabolismVaultContract.FlagFatigue;
        public const uint Hypoxia = ShinobuMetabolismVaultContract.FlagHypoxia;
        public const uint ExecutionBudgetExceeded = 1u << 30;
        public const uint NanDetected = 1u << 31;
    }

    public static class ShinobuMetabolismSuitProfileFlags
    {
        public const uint CsvProfile = 1u << 0;
        public const uint DefaultProfile = 1u << 1;
        public const uint HeatedSuit = 1u << 2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MetabolicExposureSignalDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public uint EntityHash;
        [FieldOffset(28)] public uint Frame;
        [FieldOffset(32)] public float Exposure01;
        [FieldOffset(36)] public float ToxemiaDelta;
        [FieldOffset(40)] public uint ChemicalHash;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MetabolicSpeciesRuleDTO
    {
        [FieldOffset(0)] public uint SpeciesHash;
        [FieldOffset(4)] public float MaxCalories;
        [FieldOffset(8)] public float MaxHydration;
        [FieldOffset(12)] public float BaseCalorieDrainPerSecond;
        [FieldOffset(16)] public float BaseHydrationDrainPerSecond;
        [FieldOffset(20)] public float ThermalConductance;
        [FieldOffset(24)] public float ToxinSusceptibility;
        [FieldOffset(28)] public float ShiverTemperatureCelsius;
        [FieldOffset(32)] public float HypothermiaTemperatureCelsius;
        [FieldOffset(36)] public float HeatHydrationLossScale;
        [FieldOffset(40)] public float ToxicDamagePerSecond;
        [FieldOffset(44)] public float RecoveryTemperatureCelsius;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MetabolicSuitThermalProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float ConductanceMultiplier;
        [FieldOffset(8)] public float Insulation01;
        [FieldOffset(12)] public float ShiverMultiplier;
        [FieldOffset(16)] public float HeatHydrationMultiplier;
        [FieldOffset(20)] public float BatteryHeatingCelsiusPerSecond;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MetabolismTuningDTO
    {
        [FieldOffset(0)] public float BaseCalorieDrainScale;
        [FieldOffset(4)] public float BaseHydrationDrainScale;
        [FieldOffset(8)] public float TemperatureLossRate;
        [FieldOffset(12)] public float ExertionMultiplier;
        [FieldOffset(16)] public float ExertionHydrationMultiplier;
        [FieldOffset(20)] public float ToxinAccumulationPerSecond;
        [FieldOffset(24)] public float ToxinPurgePerSecond;
        [FieldOffset(28)] public float ShiverCalorieBoost;
        [FieldOffset(32)] public float FrostStartTemperatureCelsius;
        [FieldOffset(36)] public float FrostFullTemperatureCelsius;
        [FieldOffset(40)] public float AmbientFallbackTemperatureCelsius;
        [FieldOffset(44)] public float ToxicDamageScale;
        [FieldOffset(48)] public float GlobalQualityWeight;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint Version;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MetabolicTelemetryEntry
    {
        [FieldOffset(0)] public ulong StateHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint EntityCount;
        [FieldOffset(16)] public float AverageCoreTemperature;
        [FieldOffset(20)] public float MinimumCoreTemperature;
        [FieldOffset(24)] public float MaximumToxicity;
        [FieldOffset(28)] public uint StarvationCount;
        [FieldOffset(32)] public uint DehydrationCount;
        [FieldOffset(36)] public uint ToxicityCount;
        [FieldOffset(40)] public float DeltaSeconds;
        [FieldOffset(44)] public float ExecutionMicroseconds;
        [FieldOffset(48)] public float GlobalQualityWeight;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint FirstInvalidIndex;
        [FieldOffset(60)] public uint SignalCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MetabolicDetailTelemetryEntry
    {
        [FieldOffset(0)] public double3 PlayerAup;
        [FieldOffset(24)] public float PlayerDepthMeters;
        [FieldOffset(28)] public float ActiveCalorieBurnPerSecond;
        [FieldOffset(32)] public float AmbientCelsius;
        [FieldOffset(36)] public float ThermalK;
        [FieldOffset(40)] public float CoreAmbientDeltaCelsius;
        [FieldOffset(44)] public float ThermalDeltaCelsiusPerSecond;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint EntityHashID;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint SuitProfileHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MetabolismShaderGlobalsDTO
    {
        [FieldOffset(0)] public float FrostScalar;
        [FieldOffset(4)] public float AverageCoreTemperature;
        [FieldOffset(8)] public float MinimumCoreTemperature;
        [FieldOffset(12)] public float GlobalQualityWeight;
        [FieldOffset(16)] public float ToxicityScalar;
        [FieldOffset(20)] public float StarvationScalar;
        [FieldOffset(24)] public float DehydrationScalar;
        [FieldOffset(28)] public float _pad0;
        [FieldOffset(32)] public float4 ReservedVisualOverkill;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint _pad1;
        [FieldOffset(60)] public uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MetabolismChemicalTuningMirrorDTO
    {
        [FieldOffset(0)] public double SimulationTickDelta;
        [FieldOffset(8)] public float BaseDiffusionRate;
        [FieldOffset(12)] public float AdvectionStrength;
        [FieldOffset(16)] public float DissipationRate;
        [FieldOffset(20)] public float EmitterRadiusScale;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public float MaxChannelIntensity;
        [FieldOffset(32)] public uint Revision;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public int Iterations;
        [FieldOffset(44)] public float CellSizeMeters;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MetabolismChemicalTelemetryMirrorDTO
    {
        [FieldOffset(0)] public double3 GridOriginAup;
        [FieldOffset(24)] public float MaxBlood;
        [FieldOffset(28)] public float SolverMicros;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public int ActiveEmitters;
        [FieldOffset(40)] public int MockEmitters;
        [FieldOffset(44)] public int Iterations;
        [FieldOffset(48)] public uint StateHash;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float GlobalQualityWeight;
        [FieldOffset(60)] public int GridShiftManhattan;
    }

#if UNITY_EDITOR
    public static class ShinobuMetabolismLayoutGuards
    {
        public static bool ValidateMetabolicStateLayout()
        {
            return UnsafeUtility.SizeOf<MetabolicStateDTO>() == ShinobuMetabolismConstants.MetabolismStateSizeBytes &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Calories))) == 0 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Hydration))) == 4 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.CoreTemperature))) == 8 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Toxicity))) == 12 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.EntityHashID))) == 16 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Flags))) == 20 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.Fatigue01))) == 24 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.RealO2))) == 28 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.AgonyTimeRemaining))) == 32 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicStateDTO).GetField(nameof(MetabolicStateDTO.IsInHypoxia))) == 36 &&
                   (UnsafeUtility.SizeOf<MetabolicStateDTO>() & 7) == 0;
        }

        public static bool ValidateMetabolismDetailLayout()
        {
            return UnsafeUtility.SizeOf<MetabolicSuitThermalProfileDTO>() == ShinobuMetabolismConstants.SuitThermalProfileSizeBytes &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicSuitThermalProfileDTO).GetField(nameof(MetabolicSuitThermalProfileDTO.ProfileHash))) == 0 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicSuitThermalProfileDTO).GetField(nameof(MetabolicSuitThermalProfileDTO.ConductanceMultiplier))) == 4 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicSuitThermalProfileDTO).GetField(nameof(MetabolicSuitThermalProfileDTO.Insulation01))) == 8 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicSuitThermalProfileDTO).GetField(nameof(MetabolicSuitThermalProfileDTO.ShiverMultiplier))) == 12 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicSuitThermalProfileDTO).GetField(nameof(MetabolicSuitThermalProfileDTO.HeatHydrationMultiplier))) == 16 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicSuitThermalProfileDTO).GetField(nameof(MetabolicSuitThermalProfileDTO.BatteryHeatingCelsiusPerSecond))) == 20 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicSuitThermalProfileDTO).GetField(nameof(MetabolicSuitThermalProfileDTO.Flags))) == 24 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicSuitThermalProfileDTO).GetField(nameof(MetabolicSuitThermalProfileDTO._pad0))) == 28 &&
                   UnsafeUtility.SizeOf<MetabolicDetailTelemetryEntry>() == ShinobuMetabolismConstants.DetailTelemetrySizeBytes &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicDetailTelemetryEntry).GetField(nameof(MetabolicDetailTelemetryEntry.PlayerAup))) == 0 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicDetailTelemetryEntry).GetField(nameof(MetabolicDetailTelemetryEntry.PlayerDepthMeters))) == 24 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicDetailTelemetryEntry).GetField(nameof(MetabolicDetailTelemetryEntry.ActiveCalorieBurnPerSecond))) == 28 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicDetailTelemetryEntry).GetField(nameof(MetabolicDetailTelemetryEntry.AmbientCelsius))) == 32 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicDetailTelemetryEntry).GetField(nameof(MetabolicDetailTelemetryEntry.ThermalK))) == 36 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicDetailTelemetryEntry).GetField(nameof(MetabolicDetailTelemetryEntry.CoreAmbientDeltaCelsius))) == 40 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicDetailTelemetryEntry).GetField(nameof(MetabolicDetailTelemetryEntry.ThermalDeltaCelsiusPerSecond))) == 44 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicDetailTelemetryEntry).GetField(nameof(MetabolicDetailTelemetryEntry.Frame))) == 48 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicDetailTelemetryEntry).GetField(nameof(MetabolicDetailTelemetryEntry.EntityHashID))) == 52 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicDetailTelemetryEntry).GetField(nameof(MetabolicDetailTelemetryEntry.Flags))) == 56 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicDetailTelemetryEntry).GetField(nameof(MetabolicDetailTelemetryEntry.SuitProfileHash))) == 60;
        }

        public static bool ValidateMetabolicExposureSignalLayout()
        {
            return UnsafeUtility.SizeOf<MetabolicExposureSignalDTO>() == ShinobuMetabolismConstants.ExposureSignalSizeBytes &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicExposureSignalDTO).GetField(nameof(MetabolicExposureSignalDTO.AUP))) == 0 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicExposureSignalDTO).GetField(nameof(MetabolicExposureSignalDTO.EntityHash))) == 24 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicExposureSignalDTO).GetField(nameof(MetabolicExposureSignalDTO.Frame))) == 28 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicExposureSignalDTO).GetField(nameof(MetabolicExposureSignalDTO.Exposure01))) == 32 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicExposureSignalDTO).GetField(nameof(MetabolicExposureSignalDTO.ToxemiaDelta))) == 36 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicExposureSignalDTO).GetField(nameof(MetabolicExposureSignalDTO.ChemicalHash))) == 40 &&
                   UnsafeUtility.GetFieldOffset(typeof(MetabolicExposureSignalDTO).GetField(nameof(MetabolicExposureSignalDTO.Flags))) == 44;
        }

        public static bool ValidateMetabolismLayouts()
        {
            return ValidateMetabolicStateLayout() &&
                   UnsafeUtility.SizeOf<MetabolicSpeciesRuleDTO>() == ShinobuMetabolismConstants.SpeciesRuleSizeBytes &&
                   ValidateMetabolicExposureSignalLayout() &&
                   ValidateMetabolismDetailLayout() &&
                   UnsafeUtility.SizeOf<MetabolismTuningDTO>() == ShinobuMetabolismConstants.TuningSizeBytes &&
                   UnsafeUtility.SizeOf<MetabolicTelemetryEntry>() == ShinobuMetabolismConstants.TelemetrySizeBytes &&
                   UnsafeUtility.SizeOf<MetabolismShaderGlobalsDTO>() == ShinobuMetabolismConstants.ShaderGlobalsSizeBytes &&
                   UnsafeUtility.SizeOf<MetabolismChemicalTuningMirrorDTO>() == ShinobuMetabolismConstants.ChemicalMirrorSizeBytes &&
                   UnsafeUtility.SizeOf<MetabolismChemicalTelemetryMirrorDTO>() == ShinobuMetabolismConstants.ChemicalMirrorSizeBytes;
        }
    }
#endif
}
