#if UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.AI.Cognition;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.AI.Cognition.Editor
{
    [InitializeOnLoad]
    internal static class AICognitionLayoutGuard1300
    {
        private const int ExpectedStructCount = 33;

        static AICognitionLayoutGuard1300()
        {
            Validate();
        }

        [MenuItem("Hecton8/AI/Validate Cognition DTO Layout 1300")]
        public static void Validate()
        {
            int checkedStructs = 0;

            AssertSize<CognitionStateDTO>(32, nameof(CognitionStateDTO), ref checkedStructs);
            AssertOffset<CognitionStateDTO>(nameof(CognitionStateDTO.Hunger01), 0);
            AssertOffset<CognitionStateDTO>(nameof(CognitionStateDTO.Fear01), 4);
            AssertOffset<CognitionStateDTO>(nameof(CognitionStateDTO.Aggression01), 8);
            AssertOffset<CognitionStateDTO>(nameof(CognitionStateDTO.ActiveActionHash), 12);
            AssertOffset<CognitionStateDTO>(nameof(CognitionStateDTO.TargetEntityHash), 16);
            AssertOffset<CognitionStateDTO>(nameof(CognitionStateDTO.ActionCooldown), 20);
            AssertOffset<CognitionStateDTO>("_pad0", 24);
            AssertOffset<CognitionStateDTO>("_pad1", 28);

            AssertSize<CognitionAupDTO>(32, nameof(CognitionAupDTO), ref checkedStructs);
            AssertOffset<CognitionAupDTO>(nameof(CognitionAupDTO.AUP), 0);
            AssertOffset<CognitionAupDTO>(nameof(CognitionAupDTO.EntityHash), 24);
            AssertOffset<CognitionAupDTO>(nameof(CognitionAupDTO.Flags), 28);

            AssertSize<CognitionTargetCandidateDTO>(64, nameof(CognitionTargetCandidateDTO), ref checkedStructs);
            AssertOffset<CognitionTargetCandidateDTO>(nameof(CognitionTargetCandidateDTO.AUP), 0);
            AssertOffset<CognitionTargetCandidateDTO>(nameof(CognitionTargetCandidateDTO.EntityHash), 24);
            AssertOffset<CognitionTargetCandidateDTO>(nameof(CognitionTargetCandidateDTO.SpeciesHash), 28);
            AssertOffset<CognitionTargetCandidateDTO>(nameof(CognitionTargetCandidateDTO.Threat01), 32);
            AssertOffset<CognitionTargetCandidateDTO>(nameof(CognitionTargetCandidateDTO.FoodValue01), 36);
            AssertOffset<CognitionTargetCandidateDTO>(nameof(CognitionTargetCandidateDTO.Weakness01), 40);
            AssertOffset<CognitionTargetCandidateDTO>(nameof(CognitionTargetCandidateDTO.Noise01), 44);
            AssertOffset<CognitionTargetCandidateDTO>(nameof(CognitionTargetCandidateDTO.SpatialHash), 48);
            AssertOffset<CognitionTargetCandidateDTO>(nameof(CognitionTargetCandidateDTO.Flags), 52);
            AssertOffset<CognitionTargetCandidateDTO>("_pad0", 53);
            AssertOffset<CognitionTargetCandidateDTO>("_pad1", 54);
            AssertOffset<CognitionTargetCandidateDTO>("_pad2", 55);
            AssertOffset<CognitionTargetCandidateDTO>("_pad3", 56);
            AssertOffset<CognitionTargetCandidateDTO>("_pad10", 63);

            AssertSize<CognitionUtilityTuningDTO>(128, nameof(CognitionUtilityTuningDTO), ref checkedStructs);
            AssertOffset<CognitionUtilityTuningDTO>(nameof(CognitionUtilityTuningDTO.HungerPolynomial), 0);
            AssertOffset<CognitionUtilityTuningDTO>(nameof(CognitionUtilityTuningDTO.FearPolynomial), 16);
            AssertOffset<CognitionUtilityTuningDTO>(nameof(CognitionUtilityTuningDTO.AggressionPolynomial), 32);
            AssertOffset<CognitionUtilityTuningDTO>(nameof(CognitionUtilityTuningDTO.ActionBiases), 48);
            AssertOffset<CognitionUtilityTuningDTO>(nameof(CognitionUtilityTuningDTO.SignalGains), 64);
            AssertOffset<CognitionUtilityTuningDTO>(nameof(CognitionUtilityTuningDTO.DistanceMeters), 80);
            AssertOffset<CognitionUtilityTuningDTO>(nameof(CognitionUtilityTuningDTO.Runtime), 96);
            AssertOffset<CognitionUtilityTuningDTO>(nameof(CognitionUtilityTuningDTO.Frame), 112);
            AssertOffset<CognitionUtilityTuningDTO>(nameof(CognitionUtilityTuningDTO.LastCsvHash), 116);
            AssertOffset<CognitionUtilityTuningDTO>(nameof(CognitionUtilityTuningDTO.CsvReloadVersion), 120);
            AssertOffset<CognitionUtilityTuningDTO>(nameof(CognitionUtilityTuningDTO.Flags), 124);

            AssertSize<CognitionActionOutputDTO>(64, nameof(CognitionActionOutputDTO), ref checkedStructs);
            AssertOffset<CognitionActionOutputDTO>(nameof(CognitionActionOutputDTO.Utilities), 0);
            AssertOffset<CognitionActionOutputDTO>(nameof(CognitionActionOutputDTO.DesiredLocalDirection), 16);
            AssertOffset<CognitionActionOutputDTO>(nameof(CognitionActionOutputDTO.MaxUtility), 28);
            AssertOffset<CognitionActionOutputDTO>(nameof(CognitionActionOutputDTO.ActionHash), 32);
            AssertOffset<CognitionActionOutputDTO>(nameof(CognitionActionOutputDTO.TargetEntityHash), 36);
            AssertOffset<CognitionActionOutputDTO>(nameof(CognitionActionOutputDTO.StateHash), 40);
            AssertOffset<CognitionActionOutputDTO>(nameof(CognitionActionOutputDTO.TickIntervalSeconds), 44);
            AssertOffset<CognitionActionOutputDTO>(nameof(CognitionActionOutputDTO.CooldownRemaining), 48);
            AssertOffset<CognitionActionOutputDTO>(nameof(CognitionActionOutputDTO.Frame), 52);
            AssertOffset<CognitionActionOutputDTO>(nameof(CognitionActionOutputDTO.Flags), 56);
            AssertOffset<CognitionActionOutputDTO>(nameof(CognitionActionOutputDTO.CandidateCount), 57);
            AssertOffset<CognitionActionOutputDTO>(nameof(CognitionActionOutputDTO.QualityWeightQ8), 58);
            AssertOffset<CognitionActionOutputDTO>("_pad0", 59);
            AssertOffset<CognitionActionOutputDTO>("_pad1", 60);
            AssertOffset<CognitionActionOutputDTO>("_pad4", 63);

            AssertSize<CognitionProfileDTO>(96, nameof(CognitionProfileDTO), ref checkedStructs);
            AssertOffset<CognitionProfileDTO>(nameof(CognitionProfileDTO.SpeciesHash), 0);
            AssertOffset<CognitionProfileDTO>(nameof(CognitionProfileDTO.Flags), 4);
            AssertOffset<CognitionProfileDTO>(nameof(CognitionProfileDTO.HungerPolynomial), 8);
            AssertOffset<CognitionProfileDTO>(nameof(CognitionProfileDTO.FearPolynomial), 24);
            AssertOffset<CognitionProfileDTO>(nameof(CognitionProfileDTO.AggressionPolynomial), 40);
            AssertOffset<CognitionProfileDTO>(nameof(CognitionProfileDTO.Weights), 56);
            AssertOffset<CognitionProfileDTO>(nameof(CognitionProfileDTO.DistanceMeters), 72);
            AssertOffset<CognitionProfileDTO>(nameof(CognitionProfileDTO.LastAppliedHash), 88);
            AssertOffset<CognitionProfileDTO>("_pad0", 92);

            AssertSize<CognitionMovementAcousticSignalDTO>(64, nameof(CognitionMovementAcousticSignalDTO), ref checkedStructs);
            AssertOffset<CognitionMovementAcousticSignalDTO>(nameof(CognitionMovementAcousticSignalDTO.PositionAup), 0);
            AssertOffset<CognitionMovementAcousticSignalDTO>(nameof(CognitionMovementAcousticSignalDTO.Volume), 24);
            AssertOffset<CognitionMovementAcousticSignalDTO>(nameof(CognitionMovementAcousticSignalDTO.VelocitySq), 28);
            AssertOffset<CognitionMovementAcousticSignalDTO>(nameof(CognitionMovementAcousticSignalDTO.SourceId), 32);
            AssertOffset<CognitionMovementAcousticSignalDTO>(nameof(CognitionMovementAcousticSignalDTO.Frame), 36);
            AssertOffset<CognitionMovementAcousticSignalDTO>(nameof(CognitionMovementAcousticSignalDTO.LocomotionMode), 40);
            AssertOffset<CognitionMovementAcousticSignalDTO>(nameof(CognitionMovementAcousticSignalDTO.SurfaceMode), 41);
            AssertOffset<CognitionMovementAcousticSignalDTO>(nameof(CognitionMovementAcousticSignalDTO.Flags), 42);
            AssertOffset<CognitionMovementAcousticSignalDTO>("_pad0", 43);
            AssertOffset<CognitionMovementAcousticSignalDTO>("_pad1", 44);
            AssertOffset<CognitionMovementAcousticSignalDTO>("_pad2", 45);
            AssertOffset<CognitionMovementAcousticSignalDTO>("_pad5", 48);
            AssertOffset<CognitionMovementAcousticSignalDTO>("_pad13", 56);
            AssertOffset<CognitionMovementAcousticSignalDTO>("_pad20", 63);

            AssertSize<CognitionCombatDamageSignalDTO>(64, nameof(CognitionCombatDamageSignalDTO), ref checkedStructs);
            AssertOffset<CognitionCombatDamageSignalDTO>(nameof(CognitionCombatDamageSignalDTO.ImpactAup), 0);
            AssertOffset<CognitionCombatDamageSignalDTO>(nameof(CognitionCombatDamageSignalDTO.Magnitude), 24);
            AssertOffset<CognitionCombatDamageSignalDTO>(nameof(CognitionCombatDamageSignalDTO.DamageType), 28);
            AssertOffset<CognitionCombatDamageSignalDTO>(nameof(CognitionCombatDamageSignalDTO.TargetHash), 32);
            AssertOffset<CognitionCombatDamageSignalDTO>(nameof(CognitionCombatDamageSignalDTO.SourceHash), 36);
            AssertOffset<CognitionCombatDamageSignalDTO>(nameof(CognitionCombatDamageSignalDTO.Frame), 40);
            AssertOffset<CognitionCombatDamageSignalDTO>(nameof(CognitionCombatDamageSignalDTO.Flags), 44);
            AssertOffset<CognitionCombatDamageSignalDTO>("_pad0", 45);
            AssertOffset<CognitionCombatDamageSignalDTO>("_pad1", 46);
            AssertOffset<CognitionCombatDamageSignalDTO>("_pad2", 47);
            AssertOffset<CognitionCombatDamageSignalDTO>("_pad3", 48);
            AssertOffset<CognitionCombatDamageSignalDTO>("_pad11", 56);
            AssertOffset<CognitionCombatDamageSignalDTO>("_pad18", 63);

            AssertSize<CognitionTelemetryEntry>(64, nameof(CognitionTelemetryEntry), ref checkedStructs);
            AssertOffset<CognitionTelemetryEntry>(nameof(CognitionTelemetryEntry.TargetHashFold), 0);
            AssertOffset<CognitionTelemetryEntry>(nameof(CognitionTelemetryEntry.Frame), 8);
            AssertOffset<CognitionTelemetryEntry>(nameof(CognitionTelemetryEntry.ActionHashFold), 12);
            AssertOffset<CognitionTelemetryEntry>(nameof(CognitionTelemetryEntry.HuntingCount), 16);
            AssertOffset<CognitionTelemetryEntry>(nameof(CognitionTelemetryEntry.FaultFlags), 20);
            AssertOffset<CognitionTelemetryEntry>(nameof(CognitionTelemetryEntry.AverageFear01), 24);
            AssertOffset<CognitionTelemetryEntry>(nameof(CognitionTelemetryEntry.AverageHunger01), 28);
            AssertOffset<CognitionTelemetryEntry>(nameof(CognitionTelemetryEntry.AverageAggression01), 32);
            AssertOffset<CognitionTelemetryEntry>(nameof(CognitionTelemetryEntry.MaximumUtility), 36);
            AssertOffset<CognitionTelemetryEntry>(nameof(CognitionTelemetryEntry.BurstMicroseconds), 40);
            AssertOffset<CognitionTelemetryEntry>(nameof(CognitionTelemetryEntry.GlobalQualityWeight), 44);
            AssertOffset<CognitionTelemetryEntry>(nameof(CognitionTelemetryEntry.ActiveCount), 48);
            AssertOffset<CognitionTelemetryEntry>(nameof(CognitionTelemetryEntry.NonFiniteCount), 52);
            AssertOffset<CognitionTelemetryEntry>("_pad0", 56);
            AssertOffset<CognitionTelemetryEntry>("_pad1", 60);

            AssertSize<CognitionDumpHeaderDTO>(32, nameof(CognitionDumpHeaderDTO), ref checkedStructs);
            AssertOffset<CognitionDumpHeaderDTO>(nameof(CognitionDumpHeaderDTO.Magic), 0);
            AssertOffset<CognitionDumpHeaderDTO>(nameof(CognitionDumpHeaderDTO.EndianMarker), 4);
            AssertOffset<CognitionDumpHeaderDTO>(nameof(CognitionDumpHeaderDTO.Version), 8);
            AssertOffset<CognitionDumpHeaderDTO>(nameof(CognitionDumpHeaderDTO.Frame), 12);
            AssertOffset<CognitionDumpHeaderDTO>(nameof(CognitionDumpHeaderDTO.EntryCount), 16);
            AssertOffset<CognitionDumpHeaderDTO>(nameof(CognitionDumpHeaderDTO.EntrySizeBytes), 20);
            AssertOffset<CognitionDumpHeaderDTO>(nameof(CognitionDumpHeaderDTO.Cursor), 24);
            AssertOffset<CognitionDumpHeaderDTO>(nameof(CognitionDumpHeaderDTO.AgentHash), 28);

            AssertSize<AnxietyProfileDTO>(16, nameof(AnxietyProfileDTO), ref checkedStructs);
            AssertOffset<AnxietyProfileDTO>(nameof(AnxietyProfileDTO.FearDecayRate), 0);
            AssertOffset<AnxietyProfileDTO>(nameof(AnxietyProfileDTO.AggressionDecayRate), 4);
            AssertOffset<AnxietyProfileDTO>(nameof(AnxietyProfileDTO.CalmingThreshold), 8);
            AssertOffset<AnxietyProfileDTO>("_pad0", 12);

            AssertSize<AnxietyRuntimeTuningDTO>(64, nameof(AnxietyRuntimeTuningDTO), ref checkedStructs);
            AssertOffset<AnxietyRuntimeTuningDTO>(nameof(AnxietyRuntimeTuningDTO.BaseFearDecayRate), 0);
            AssertOffset<AnxietyRuntimeTuningDTO>(nameof(AnxietyRuntimeTuningDTO.BaseAggressionDecayRate), 4);
            AssertOffset<AnxietyRuntimeTuningDTO>(nameof(AnxietyRuntimeTuningDTO.CalmingThreshold), 8);
            AssertOffset<AnxietyRuntimeTuningDTO>(nameof(AnxietyRuntimeTuningDTO.ShelterCoolingMultiplier), 12);
            AssertOffset<AnxietyRuntimeTuningDTO>(nameof(AnxietyRuntimeTuningDTO.LinearDecayScale), 16);
            AssertOffset<AnxietyRuntimeTuningDTO>(nameof(AnxietyRuntimeTuningDTO.SimulationDeltaSeconds), 20);
            AssertOffset<AnxietyRuntimeTuningDTO>(nameof(AnxietyRuntimeTuningDTO.GlobalQualityWeight), 24);
            AssertOffset<AnxietyRuntimeTuningDTO>(nameof(AnxietyRuntimeTuningDTO.ThermalPressure01), 28);
            AssertOffset<AnxietyRuntimeTuningDTO>(nameof(AnxietyRuntimeTuningDTO.ExactExpWeight01), 32);
            AssertOffset<AnxietyRuntimeTuningDTO>(nameof(AnxietyRuntimeTuningDTO.FaultMicroseconds), 36);
            AssertOffset<AnxietyRuntimeTuningDTO>(nameof(AnxietyRuntimeTuningDTO.Frame), 40);
            AssertOffset<AnxietyRuntimeTuningDTO>(nameof(AnxietyRuntimeTuningDTO.LastCsvHash), 44);
            AssertOffset<AnxietyRuntimeTuningDTO>(nameof(AnxietyRuntimeTuningDTO.CsvReloadVersion), 48);
            AssertOffset<AnxietyRuntimeTuningDTO>(nameof(AnxietyRuntimeTuningDTO.ActiveProfileCount), 52);
            AssertOffset<AnxietyRuntimeTuningDTO>(nameof(AnxietyRuntimeTuningDTO.Flags), 56);
            AssertOffset<AnxietyRuntimeTuningDTO>("_pad0", 60);

            AssertSize<AnxietyDecayScratchDTO>(64, nameof(AnxietyDecayScratchDTO), ref checkedStructs);
            AssertOffset<AnxietyDecayScratchDTO>(nameof(AnxietyDecayScratchDTO.Fear01), 0);
            AssertOffset<AnxietyDecayScratchDTO>(nameof(AnxietyDecayScratchDTO.Aggression01), 4);
            AssertOffset<AnxietyDecayScratchDTO>(nameof(AnxietyDecayScratchDTO.ShelterMultiplier), 8);
            AssertOffset<AnxietyDecayScratchDTO>(nameof(AnxietyDecayScratchDTO.Flags), 12);
            AssertOffset<AnxietyDecayScratchDTO>(nameof(AnxietyDecayScratchDTO.StateHash), 16);
            AssertOffset<AnxietyDecayScratchDTO>(nameof(AnxietyDecayScratchDTO.EntityHash), 20);
            AssertOffset<AnxietyDecayScratchDTO>("_pad0", 24);
            AssertOffset<AnxietyDecayScratchDTO>("_pad1", 28);
            AssertOffset<AnxietyDecayScratchDTO>("_pad2", 32);
            AssertOffset<AnxietyDecayScratchDTO>("_pad4", 40);
            AssertOffset<AnxietyDecayScratchDTO>("_pad6", 48);
            AssertOffset<AnxietyDecayScratchDTO>("_pad8", 56);
            AssertOffset<AnxietyDecayScratchDTO>("_pad9", 60);

            AssertSize<AnxietyTelemetryEntry>(64, nameof(AnxietyTelemetryEntry), ref checkedStructs);
            AssertOffset<AnxietyTelemetryEntry>(nameof(AnxietyTelemetryEntry.Frame), 0);
            AssertOffset<AnxietyTelemetryEntry>(nameof(AnxietyTelemetryEntry.ActiveDecayCount), 4);
            AssertOffset<AnxietyTelemetryEntry>(nameof(AnxietyTelemetryEntry.ShelterMultiplierCount), 8);
            AssertOffset<AnxietyTelemetryEntry>(nameof(AnxietyTelemetryEntry.NonFiniteCount), 12);
            AssertOffset<AnxietyTelemetryEntry>(nameof(AnxietyTelemetryEntry.FaultFlags), 16);
            AssertOffset<AnxietyTelemetryEntry>(nameof(AnxietyTelemetryEntry.AverageFear01), 20);
            AssertOffset<AnxietyTelemetryEntry>(nameof(AnxietyTelemetryEntry.AverageAggression01), 24);
            AssertOffset<AnxietyTelemetryEntry>(nameof(AnxietyTelemetryEntry.AverageShelterMultiplier), 28);
            AssertOffset<AnxietyTelemetryEntry>(nameof(AnxietyTelemetryEntry.BurstMicroseconds), 32);
            AssertOffset<AnxietyTelemetryEntry>(nameof(AnxietyTelemetryEntry.GlobalQualityWeight), 36);
            AssertOffset<AnxietyTelemetryEntry>(nameof(AnxietyTelemetryEntry.ExactExpWeight01), 40);
            AssertOffset<AnxietyTelemetryEntry>(nameof(AnxietyTelemetryEntry.ThermalPressure01), 44);
            AssertOffset<AnxietyTelemetryEntry>(nameof(AnxietyTelemetryEntry.StateHashFold), 48);
            AssertOffset<AnxietyTelemetryEntry>(nameof(AnxietyTelemetryEntry.ProfileHashFold), 52);
            AssertOffset<AnxietyTelemetryEntry>("_pad0", 56);
            AssertOffset<AnxietyTelemetryEntry>("_pad1", 60);

            AssertSize<AnxietyDumpHeaderDTO>(32, nameof(AnxietyDumpHeaderDTO), ref checkedStructs);
            AssertOffset<AnxietyDumpHeaderDTO>(nameof(AnxietyDumpHeaderDTO.Magic), 0);
            AssertOffset<AnxietyDumpHeaderDTO>(nameof(AnxietyDumpHeaderDTO.EndianMarker), 4);
            AssertOffset<AnxietyDumpHeaderDTO>(nameof(AnxietyDumpHeaderDTO.Version), 8);
            AssertOffset<AnxietyDumpHeaderDTO>(nameof(AnxietyDumpHeaderDTO.Frame), 12);
            AssertOffset<AnxietyDumpHeaderDTO>(nameof(AnxietyDumpHeaderDTO.EntryCount), 16);
            AssertOffset<AnxietyDumpHeaderDTO>(nameof(AnxietyDumpHeaderDTO.EntrySizeBytes), 20);
            AssertOffset<AnxietyDumpHeaderDTO>(nameof(AnxietyDumpHeaderDTO.Cursor), 24);
            AssertOffset<AnxietyDumpHeaderDTO>(nameof(AnxietyDumpHeaderDTO.AgentHash), 28);

            AssertSize<AnxietyShelterSdfHeaderDTO>(64, nameof(AnxietyShelterSdfHeaderDTO), ref checkedStructs);
            AssertOffset<AnxietyShelterSdfHeaderDTO>(nameof(AnxietyShelterSdfHeaderDTO.OriginAUP), 0);
            AssertOffset<AnxietyShelterSdfHeaderDTO>(nameof(AnxietyShelterSdfHeaderDTO.Dimensions), 24);
            AssertOffset<AnxietyShelterSdfHeaderDTO>(nameof(AnxietyShelterSdfHeaderDTO.VoxelSizeMeters), 36);
            AssertOffset<AnxietyShelterSdfHeaderDTO>(nameof(AnxietyShelterSdfHeaderDTO.SolidThreshold), 40);
            AssertOffset<AnxietyShelterSdfHeaderDTO>(nameof(AnxietyShelterSdfHeaderDTO.SdfRangeMeters), 44);
            AssertOffset<AnxietyShelterSdfHeaderDTO>(nameof(AnxietyShelterSdfHeaderDTO.Version), 48);
            AssertOffset<AnxietyShelterSdfHeaderDTO>(nameof(AnxietyShelterSdfHeaderDTO.Flags), 52);
            AssertOffset<AnxietyShelterSdfHeaderDTO>("_pad0", 56);
            AssertOffset<AnxietyShelterSdfHeaderDTO>("_pad1", 60);

            AssertSize<ApexStateDTO>(64, nameof(ApexStateDTO), ref checkedStructs);
            AssertOffset<ApexStateDTO>(nameof(ApexStateDTO.AUP), 0);
            AssertOffset<ApexStateDTO>(nameof(ApexStateDTO.Velocity), 24);
            AssertOffset<ApexStateDTO>(nameof(ApexStateDTO.AggressionLevel), 36);
            AssertOffset<ApexStateDTO>(nameof(ApexStateDTO.TargetHash), 40);
            AssertOffset<ApexStateDTO>(nameof(ApexStateDTO.AcousticMemoryHash), 44);
            AssertOffset<ApexStateDTO>(nameof(ApexStateDTO.Stamina), 48);
            AssertOffset<ApexStateDTO>("_padAlign0", 52);
            AssertOffset<ApexStateDTO>("_pad0", 56);
            AssertOffset<ApexStateDTO>("_pad1", 60);

            AssertSize<MockPlayerAUP>(128, nameof(MockPlayerAUP), ref checkedStructs);
            AssertOffset<MockPlayerAUP>(nameof(MockPlayerAUP.AUP), 0);
            AssertOffset<MockPlayerAUP>(nameof(MockPlayerAUP.LastAdvanceFrame), 24);
            AssertOffset<MockPlayerAUP>(nameof(MockPlayerAUP.Velocity), 32);
            AssertOffset<MockPlayerAUP>(nameof(MockPlayerAUP.Forward), 44);
            AssertOffset<MockPlayerAUP>(nameof(MockPlayerAUP.TargetHash), 56);
            AssertOffset<MockPlayerAUP>(nameof(MockPlayerAUP.BiomeHash), 60);
            AssertOffset<MockPlayerAUP>(nameof(MockPlayerAUP.Noise01), 64);
            AssertOffset<MockPlayerAUP>(nameof(MockPlayerAUP.AcousticMagnitude01), 68);
            AssertOffset<MockPlayerAUP>(nameof(MockPlayerAUP.SimulationTickDelta), 72);
            AssertOffset<MockPlayerAUP>(nameof(MockPlayerAUP.Flags), 76);
            AssertOffset<MockPlayerAUP>("_pad0", 80);
            AssertOffset<MockPlayerAUP>("_pad1", 84);
            AssertOffset<MockPlayerAUP>("_pad2", 88);
            AssertOffset<MockPlayerAUP>("_pad4", 96);
            AssertOffset<MockPlayerAUP>("_pad6", 104);
            AssertOffset<MockPlayerAUP>("_pad8", 112);
            AssertOffset<MockPlayerAUP>("_pad10", 120);
            AssertOffset<MockPlayerAUP>("_pad11", 124);

            AssertSize<ApexBrainAcousticEchoTap>(64, nameof(ApexBrainAcousticEchoTap), ref checkedStructs);
            AssertOffset<ApexBrainAcousticEchoTap>(nameof(ApexBrainAcousticEchoTap.AUP), 0);
            AssertOffset<ApexBrainAcousticEchoTap>(nameof(ApexBrainAcousticEchoTap.Magnitude01), 24);
            AssertOffset<ApexBrainAcousticEchoTap>(nameof(ApexBrainAcousticEchoTap.AgeSeconds), 28);
            AssertOffset<ApexBrainAcousticEchoTap>(nameof(ApexBrainAcousticEchoTap.SourceHash), 32);
            AssertOffset<ApexBrainAcousticEchoTap>(nameof(ApexBrainAcousticEchoTap.Frame), 36);
            AssertOffset<ApexBrainAcousticEchoTap>(nameof(ApexBrainAcousticEchoTap.AcousticMemoryHash), 40);
            AssertOffset<ApexBrainAcousticEchoTap>(nameof(ApexBrainAcousticEchoTap.Flags), 44);
            AssertOffset<ApexBrainAcousticEchoTap>("_pad0", 45);
            AssertOffset<ApexBrainAcousticEchoTap>("_pad1", 46);
            AssertOffset<ApexBrainAcousticEchoTap>("_pad2", 47);
            AssertOffset<ApexBrainAcousticEchoTap>("_pad3", 48);
            AssertOffset<ApexBrainAcousticEchoTap>("_pad11", 56);
            AssertOffset<ApexBrainAcousticEchoTap>("_pad18", 63);

            AssertSize<MockWorldSampler>(64, nameof(MockWorldSampler), ref checkedStructs);
            AssertOffset<MockWorldSampler>(nameof(MockWorldSampler.OriginLocal), 0);
            AssertOffset<MockWorldSampler>(nameof(MockWorldSampler.CaveRadiusMeters), 12);
            AssertOffset<MockWorldSampler>(nameof(MockWorldSampler.FloorY), 16);
            AssertOffset<MockWorldSampler>(nameof(MockWorldSampler.CeilingY), 20);
            AssertOffset<MockWorldSampler>(nameof(MockWorldSampler.GradientProbeMeters), 24);
            AssertOffset<MockWorldSampler>(nameof(MockWorldSampler.SpatialCellSizeMeters), 28);
            AssertOffset<MockWorldSampler>(nameof(MockWorldSampler.CanyonBias01), 32);
            AssertOffset<MockWorldSampler>(nameof(MockWorldSampler.WallRepulsionGain), 36);
            AssertOffset<MockWorldSampler>(nameof(MockWorldSampler.HeadOffsetMeters), 40);
            AssertOffset<MockWorldSampler>(nameof(MockWorldSampler.MidOffsetMeters), 44);
            AssertOffset<MockWorldSampler>(nameof(MockWorldSampler.TailOffsetMeters), 48);
            AssertOffset<MockWorldSampler>(nameof(MockWorldSampler.SdfSoftMarginMeters), 52);
            AssertOffset<MockWorldSampler>(nameof(MockWorldSampler.Seed), 56);
            AssertOffset<MockWorldSampler>(nameof(MockWorldSampler.Flags), 60);

            AssertSize<ApexBrainTuning>(128, nameof(ApexBrainTuning), ref checkedStructs);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.LastCsvWriteTicks), 0);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.AggressionMultiplier), 8);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.AcousticSensitivity), 12);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.TurnRate), 16);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.StalkingDistance), 20);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.LeviathanSpeed), 24);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.TerrorRadius), 28);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.BaseDamageMagnitude), 32);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.BiomeAggressionMultiplier), 36);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.GlobalQualityWeight), 40);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.SimulationTickDelta), 44);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.CurrentTimeSeconds), 48);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.StrikeDistance), 52);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.HeadOffsetMeters), 56);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.MidOffsetMeters), 60);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.TailOffsetMeters), 64);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.PreferredBiomeHash), 68);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.NoiseAggroGain), 72);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.StaminaRecoveryPerSecond), 76);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.StaminaStrikeCost), 80);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.SweetLieShadowGain), 84);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.SweetLieViewDotThreshold), 88);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.AmbushNodeRadiusMeters), 92);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.VisualOverkillGain), 96);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.BiteHeadLocalOffset), 100);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.SourceHash), 104);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.Flags), 108);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.LastCsvHash), 112);
            AssertOffset<ApexBrainTuning>(nameof(ApexBrainTuning.CsvReloadVersion), 116);
            AssertOffset<ApexBrainTuning>("_pad0", 120);
            AssertOffset<ApexBrainTuning>("_pad1", 124);

            AssertSize<ApexEmergencyStats>(64, nameof(ApexEmergencyStats), ref checkedStructs);
            AssertOffset<ApexEmergencyStats>(nameof(ApexEmergencyStats.AggressionBuildSeconds), 0);
            AssertOffset<ApexEmergencyStats>(nameof(ApexEmergencyStats.TurnRadiiMeters), 16);
            AssertOffset<ApexEmergencyStats>(nameof(ApexEmergencyStats.StrikeWindowsSeconds), 32);
            AssertOffset<ApexEmergencyStats>(nameof(ApexEmergencyStats.VisualOverkillScalars), 48);

            AssertSize<ApexInfluenceNode>(64, nameof(ApexInfluenceNode), ref checkedStructs);
            AssertOffset<ApexInfluenceNode>(nameof(ApexInfluenceNode.LocalPosition), 0);
            AssertOffset<ApexInfluenceNode>(nameof(ApexInfluenceNode.Score), 12);
            AssertOffset<ApexInfluenceNode>(nameof(ApexInfluenceNode.Direction), 16);
            AssertOffset<ApexInfluenceNode>(nameof(ApexInfluenceNode.SpatialHash), 28);
            AssertOffset<ApexInfluenceNode>(nameof(ApexInfluenceNode.SdfSafety01), 32);
            AssertOffset<ApexInfluenceNode>(nameof(ApexInfluenceNode.SweetLieWeight01), 36);
            AssertOffset<ApexInfluenceNode>(nameof(ApexInfluenceNode.FractionalWeight01), 40);
            AssertOffset<ApexInfluenceNode>(nameof(ApexInfluenceNode.NodeIndex), 44);
            AssertOffset<ApexInfluenceNode>(nameof(ApexInfluenceNode.Flags), 48);
            AssertOffset<ApexInfluenceNode>(nameof(ApexInfluenceNode.SurvivalNodeBudgetPressureQ8), 52);
            AssertOffset<ApexInfluenceNode>("_pad0", 52);
            AssertOffset<ApexInfluenceNode>("_pad1", 56);
            AssertOffset<ApexInfluenceNode>("_pad2", 60);

            AssertSize<ApexBrainOutputDTO>(192, nameof(ApexBrainOutputDTO), ref checkedStructs);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.DesiredVelocity), 0);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.DesiredSpeed), 12);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.IK_BiteTarget), 16);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.AggressionLevel), 28);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.InterceptLocal), 32);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.StalkUtility), 44);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.AcousticMemoryLocal), 48);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.AmbushUtility), 60);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.WallRepulsion), 64);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.StrikeUtility), 76);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.BestAmbushNodeLocal), 80);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.SweetLieLos01), 92);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.SpatialHash), 96);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.StateHash), 100);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.EvaluatedNodeCount), 104);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.FractionalNodeWeight01), 108);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.DesiredDirection), 112);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.TerrorRadiusMeters), 124);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.VisualOverkillScalars), 128);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.TargetHash), 144);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.AcousticMemoryHash), 148);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.Slot), 152);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.Phase), 154);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.Flags), 155);
            AssertOffset<ApexBrainOutputDTO>(nameof(ApexBrainOutputDTO.SurvivalNodeBudgetPressureQ8), 156);
            AssertOffset<ApexBrainOutputDTO>("_pad0", 156);
            AssertOffset<ApexBrainOutputDTO>("_pad1", 157);
            AssertOffset<ApexBrainOutputDTO>("_pad2", 158);
            AssertOffset<ApexBrainOutputDTO>("_pad4", 160);
            AssertOffset<ApexBrainOutputDTO>("_pad12", 168);
            AssertOffset<ApexBrainOutputDTO>("_pad20", 176);
            AssertOffset<ApexBrainOutputDTO>("_pad28", 184);
            AssertOffset<ApexBrainOutputDTO>("_pad35", 191);

            AssertSize<ApexTelemetryEntry>(128, nameof(ApexTelemetryEntry), ref checkedStructs);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.Frame), 0);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.StateHash), 4);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.SpatialHash), 8);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.AcousticMemoryHash), 12);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.InterceptLocal), 16);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.AggressionLevel), 28);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.DesiredVelocity), 32);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.SweetLieLos01), 44);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.WallRepulsion), 48);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.StrikeUtility), 60);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.UtilityScores), 64);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.TargetHash), 80);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.BiomeHash), 84);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.EvaluatedNodeCount), 88);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.GlobalQualityWeight), 92);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.ActiveLeviathans), 96);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.InterceptComputeTimeMs), 100);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.FaultCode), 104);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.Slot), 108);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.Phase), 110);
            AssertOffset<ApexTelemetryEntry>(nameof(ApexTelemetryEntry.Flags), 111);
            AssertOffset<ApexTelemetryEntry>("_pad0", 112);
            AssertOffset<ApexTelemetryEntry>("_pad1", 113);
            AssertOffset<ApexTelemetryEntry>("_pad8", 120);
            AssertOffset<ApexTelemetryEntry>("_pad15", 127);

            AssertSize<ApexProximitySignal>(64, nameof(ApexProximitySignal), ref checkedStructs);
            AssertOffset<ApexProximitySignal>(nameof(ApexProximitySignal.SourceAup), 0);
            AssertOffset<ApexProximitySignal>(nameof(ApexProximitySignal.Aggression01), 24);
            AssertOffset<ApexProximitySignal>(nameof(ApexProximitySignal.TerrorRadiusMeters), 28);
            AssertOffset<ApexProximitySignal>(nameof(ApexProximitySignal.Rumble01), 32);
            AssertOffset<ApexProximitySignal>(nameof(ApexProximitySignal.SourceHash), 36);
            AssertOffset<ApexProximitySignal>(nameof(ApexProximitySignal.Frame), 40);
            AssertOffset<ApexProximitySignal>(nameof(ApexProximitySignal.Slot), 44);
            AssertOffset<ApexProximitySignal>(nameof(ApexProximitySignal.Phase), 46);
            AssertOffset<ApexProximitySignal>(nameof(ApexProximitySignal.Flags), 47);
            AssertOffset<ApexProximitySignal>("_pad0", 48);
            AssertOffset<ApexProximitySignal>("_pad1", 49);
            AssertOffset<ApexProximitySignal>("_pad8", 56);
            AssertOffset<ApexProximitySignal>("_pad15", 63);

            AssertSize<MockCombatDamageSignal>(64, nameof(MockCombatDamageSignal), ref checkedStructs);
            AssertOffset<MockCombatDamageSignal>(nameof(MockCombatDamageSignal.TargetAup), 0);
            AssertOffset<MockCombatDamageSignal>(nameof(MockCombatDamageSignal.ImpactDirection), 24);
            AssertOffset<MockCombatDamageSignal>(nameof(MockCombatDamageSignal.Magnitude), 36);
            AssertOffset<MockCombatDamageSignal>(nameof(MockCombatDamageSignal.TargetHash), 40);
            AssertOffset<MockCombatDamageSignal>(nameof(MockCombatDamageSignal.SourceHash), 44);
            AssertOffset<MockCombatDamageSignal>(nameof(MockCombatDamageSignal.Frame), 48);
            AssertOffset<MockCombatDamageSignal>(nameof(MockCombatDamageSignal.Slot), 52);
            AssertOffset<MockCombatDamageSignal>(nameof(MockCombatDamageSignal.Flags), 54);
            AssertOffset<MockCombatDamageSignal>("_pad0", 55);
            AssertOffset<MockCombatDamageSignal>("_pad1", 56);
            AssertOffset<MockCombatDamageSignal>("_pad8", 63);

            AssertSize<ApexPanicSignal>(64, nameof(ApexPanicSignal), ref checkedStructs);
            AssertOffset<ApexPanicSignal>(nameof(ApexPanicSignal.SourceAup), 0);
            AssertOffset<ApexPanicSignal>(nameof(ApexPanicSignal.Direction), 24);
            AssertOffset<ApexPanicSignal>(nameof(ApexPanicSignal.RadiusMeters), 36);
            AssertOffset<ApexPanicSignal>(nameof(ApexPanicSignal.Intensity01), 40);
            AssertOffset<ApexPanicSignal>(nameof(ApexPanicSignal.SourceHash), 44);
            AssertOffset<ApexPanicSignal>(nameof(ApexPanicSignal.Frame), 48);
            AssertOffset<ApexPanicSignal>(nameof(ApexPanicSignal.Slot), 52);
            AssertOffset<ApexPanicSignal>(nameof(ApexPanicSignal.Flags), 54);
            AssertOffset<ApexPanicSignal>("_pad0", 55);
            AssertOffset<ApexPanicSignal>("_pad1", 56);
            AssertOffset<ApexPanicSignal>("_pad8", 63);

            AssertSize<AlphaLeviathanTelemetryEntry>(64, nameof(AlphaLeviathanTelemetryEntry), ref checkedStructs);
            AssertOffset<AlphaLeviathanTelemetryEntry>(nameof(AlphaLeviathanTelemetryEntry.Frame), 0);
            AssertOffset<AlphaLeviathanTelemetryEntry>(nameof(AlphaLeviathanTelemetryEntry.DistanceToPlayerMeters), 4);
            AssertOffset<AlphaLeviathanTelemetryEntry>(nameof(AlphaLeviathanTelemetryEntry.FogRingDistanceMeters), 8);
            AssertOffset<AlphaLeviathanTelemetryEntry>(nameof(AlphaLeviathanTelemetryEntry.Position), 12);
            AssertOffset<AlphaLeviathanTelemetryEntry>(nameof(AlphaLeviathanTelemetryEntry.PlayerPosition), 24);
            AssertOffset<AlphaLeviathanTelemetryEntry>(nameof(AlphaLeviathanTelemetryEntry.DesiredDirection), 36);
            AssertOffset<AlphaLeviathanTelemetryEntry>(nameof(AlphaLeviathanTelemetryEntry.StateHash), 48);
            AssertOffset<AlphaLeviathanTelemetryEntry>(nameof(AlphaLeviathanTelemetryEntry.LeviathanAgressivity01), 52);
            AssertOffset<AlphaLeviathanTelemetryEntry>(nameof(AlphaLeviathanTelemetryEntry.Reserved1), 56);
            AssertOffset<AlphaLeviathanTelemetryEntry>(nameof(AlphaLeviathanTelemetryEntry.Slot), 60);
            AssertOffset<AlphaLeviathanTelemetryEntry>(nameof(AlphaLeviathanTelemetryEntry.Phase), 62);
            AssertOffset<AlphaLeviathanTelemetryEntry>(nameof(AlphaLeviathanTelemetryEntry.Flags), 63);

            AssertSize<AlphaLeviathanAup>(48, nameof(AlphaLeviathanAup), ref checkedStructs);
            AssertOffset<AlphaLeviathanAup>(nameof(AlphaLeviathanAup.GridX), 0);
            AssertOffset<AlphaLeviathanAup>(nameof(AlphaLeviathanAup.GridY), 8);
            AssertOffset<AlphaLeviathanAup>(nameof(AlphaLeviathanAup.GridZ), 16);
            AssertOffset<AlphaLeviathanAup>(nameof(AlphaLeviathanAup.Reserved), 24);
            AssertOffset<AlphaLeviathanAup>(nameof(AlphaLeviathanAup.Local), 32);

            AssertSize<AlphaLeviathanCognitionState>(192, nameof(AlphaLeviathanCognitionState), ref checkedStructs);
            AssertOffset<AlphaLeviathanCognitionState>(nameof(AlphaLeviathanCognitionState.LeviathanAup), 0);
            AssertOffset<AlphaLeviathanCognitionState>(nameof(AlphaLeviathanCognitionState.TargetAnchorAup), 48);
            AssertOffset<AlphaLeviathanCognitionState>(nameof(AlphaLeviathanCognitionState.Forward), 96);
            AssertOffset<AlphaLeviathanCognitionState>(nameof(AlphaLeviathanCognitionState.PreviousSteeringDirection), 108);
            AssertOffset<AlphaLeviathanCognitionState>(nameof(AlphaLeviathanCognitionState.AgressionLevel01), 120);
            AssertOffset<AlphaLeviathanCognitionState>(nameof(AlphaLeviathanCognitionState.PhaseStartSeconds), 124);
            AssertOffset<AlphaLeviathanCognitionState>(nameof(AlphaLeviathanCognitionState.LastShiftFrameId), 128);
            AssertOffset<AlphaLeviathanCognitionState>(nameof(AlphaLeviathanCognitionState.StateHash), 132);
            AssertOffset<AlphaLeviathanCognitionState>(nameof(AlphaLeviathanCognitionState.Reserved0), 136);
            AssertOffset<AlphaLeviathanCognitionState>(nameof(AlphaLeviathanCognitionState.Slot), 140);
            AssertOffset<AlphaLeviathanCognitionState>(nameof(AlphaLeviathanCognitionState.CurrentPhase), 142);
            AssertOffset<AlphaLeviathanCognitionState>(nameof(AlphaLeviathanCognitionState.Flags), 143);
            AssertOffset<AlphaLeviathanCognitionState>("_pad0", 144);
            AssertOffset<AlphaLeviathanCognitionState>("_pad8", 152);
            AssertOffset<AlphaLeviathanCognitionState>("_pad16", 160);
            AssertOffset<AlphaLeviathanCognitionState>("_pad24", 168);
            AssertOffset<AlphaLeviathanCognitionState>("_pad32", 176);
            AssertOffset<AlphaLeviathanCognitionState>("_pad40", 184);
            AssertOffset<AlphaLeviathanCognitionState>("_pad47", 191);

            AssertSize<AlphaLeviathanSensoryStimulus>(176, nameof(AlphaLeviathanSensoryStimulus), ref checkedStructs);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.PlayerAup), 0);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.PingAup), 48);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.PlayerForward), 96);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.SdfGradient), 108);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.PlayerNoise01), 120);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.NoiseThreshold01), 124);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.HeadlightDot), 128);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.FogDistanceMeters), 132);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.DeltaTime), 136);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.SystemStress01), 140);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.SonarPingAgeSeconds), 144);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.SonarPingIntensity01), 148);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.CurrentTimeSeconds), 152);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.RuntimeFlags), 156);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.ObservedShiftFrameId), 160);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.Reserved0), 164);
            AssertOffset<AlphaLeviathanSensoryStimulus>(nameof(AlphaLeviathanSensoryStimulus.Reserved1), 168);
            AssertOffset<AlphaLeviathanSensoryStimulus>("_pad0", 172);

            AssertSize<AlphaLeviathanSteeringOutput>(128, nameof(AlphaLeviathanSteeringOutput), ref checkedStructs);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.DesiredDirection), 0);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.TargetRuntimeOffsetMeters), 12);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.DesiredRingDistanceMeters), 24);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.DistanceToAnchorMeters), 28);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.BioluminescenceIntensity), 32);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.AgressionLevel01), 36);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.StateHash), 40);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.SdfContourWeight01), 44);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.WakeSiltIntensity01), 48);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.VisualOverkill01), 52);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.RecommendedCadenceSeconds), 56);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.VisorSaltCrystalGrowth01), 60);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.HullDentImpulse01), 64);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.SubsurfaceScatterPulse01), 68);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.ParticleOverkillBudget01), 72);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.PredatorSilhouetteNoise01), 76);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.Slot), 80);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.CurrentPhase), 82);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.Flags), 83);
            AssertOffset<AlphaLeviathanSteeringOutput>(nameof(AlphaLeviathanSteeringOutput.IntentFlags), 84);
            AssertOffset<AlphaLeviathanSteeringOutput>("_pad0", 85);
            AssertOffset<AlphaLeviathanSteeringOutput>("_pad1", 86);
            AssertOffset<AlphaLeviathanSteeringOutput>("_pad2", 87);
            AssertOffset<AlphaLeviathanSteeringOutput>("_pad3", 88);
            AssertOffset<AlphaLeviathanSteeringOutput>("_pad11", 96);
            AssertOffset<AlphaLeviathanSteeringOutput>("_pad19", 104);
            AssertOffset<AlphaLeviathanSteeringOutput>("_pad27", 112);
            AssertOffset<AlphaLeviathanSteeringOutput>("_pad35", 120);
            AssertOffset<AlphaLeviathanSteeringOutput>("_pad42", 127);

            if (checkedStructs != ExpectedStructCount)
                Fail("validated struct count " + checkedStructs + " != " + ExpectedStructCount);
        }

        private static void AssertSize<T>(int expected, string typeName, ref int checkedStructs)
            where T : struct
        {
            int actual = UnsafeUtility.SizeOf<T>();
            if (actual != expected || (actual & 7) != 0)
                Fail(typeName + " size " + actual + " != " + expected + " or not divisible by 8");

            checkedStructs++;
        }

        private static void AssertOffset<T>(string fieldName, int expected)
            where T : struct
        {
            int actual = Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
            if (actual != expected)
                Fail(typeof(T).Name + "." + fieldName + " offset " + actual + " != " + expected);
        }

        private static void Fail(string detail)
        {
            throw new global::Hecton8.Core.FatalArchitectureException("AI_COGNITION_DTO_LAYOUT_1300: " + detail);
        }
    }
}
#endif
