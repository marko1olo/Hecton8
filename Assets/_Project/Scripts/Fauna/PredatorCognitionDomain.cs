using System.IO;
using System.Runtime.InteropServices;
using Stopwatch = System.Diagnostics.Stopwatch;
using Hecton8.AI.Cognition;
using Hecton8.AI.Perception;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Core.Scheduling;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI
{
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    internal struct CognitionCore
    {
        // 64-byte cognition core layout:
        // Position         -> offset  0, size 12
        // QuantizedDrives  -> offset 12, size  4 (Hunger/Aggression/Fear/Threat)
        // Velocity         -> offset 16, size 12
        // StateFlags       -> offset 28, size  4
        // MemoryHead       -> offset 32, size  4
        // ClaimedBoidIndex -> offset 36, size  4
        // SpeciesId        -> offset 40, size  4
        // AcousticHead     -> offset 44, size  4
        // QuantizedFatigue -> offset 48, size  4
        // Reserved padding -> offset 52, size 12
        public float3 Position;
        public uint QuantizedDrives;
        public float3 Velocity;
        public uint StateFlags;
        public int MemoryHead;
        public int ClaimedBoidIndex;
        public int SpeciesId;
        public int AcousticMemoryHead;
        public uint QuantizedFatigue;
        public int Reserved1;
        public int Reserved2;
        public int Reserved3;
    }

    [StructLayout(LayoutKind.Sequential, Size = 24)]
    internal struct CognitionMemoryEntry
    {
        public float3 WorldPosition;
        public float Timestamp;
        public float Intensity;
        public int StimulusType;
    }

    [StructLayout(LayoutKind.Sequential, Size = 40)]
    internal struct AcousticMemoryEntry
    {
        public float3 WorldPosition;
        public float Timestamp;
        public float Intensity;
        public int3 BucketCoord;
        public uint BucketHash;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    internal unsafe struct PredatorCognitionDTO
    {
        [FieldOffset(0)] public double3 CurrentAUP;
        [FieldOffset(24)] public double3 TargetAUP;
        [FieldOffset(48)] public float3 ForwardVector;
        [FieldOffset(60)] public float Hunger;
        [FieldOffset(64)] public float Fear;
        [FieldOffset(68)] public uint TargetID;
        [FieldOffset(72)] public byte CurrentState;
        [FieldOffset(73)] public byte _pad0;
        [FieldOffset(74)] public byte _pad1;
        [FieldOffset(75)] public byte _pad2;
        [FieldOffset(76)] public byte _pad3;
        [FieldOffset(77)] public byte _pad4;
        [FieldOffset(78)] public byte _pad5;
        [FieldOffset(79)] public byte _pad6;

        public static ref PredatorCognitionDTO AsMutableRef(void* ptr)
        {
            return ref UnsafeUtility.AsRef<PredatorCognitionDTO>(ptr);
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 24)]
    internal partial struct PredatorMockAcousticSignal
    {
        public float3 Position;
        public float Timestamp;
        public float Intensity;
        public uint SourceId;
    }

    [StructLayout(LayoutKind.Sequential, Size = 24)]
    internal partial struct MockLightSource
    {
        public float3 Position;
        public float RangeSq;
        public float Intensity;
        public uint SourceId;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    internal struct ApexCortexTuningSnapshot
    {
        public float HungerWeight;
        public float FearWeight;
        public float LightAversion;
        public float AcousticMemoryDecay;
    }

    [StructLayout(LayoutKind.Sequential, Size = 96)]
    internal struct LightSourceData
    {
        public AbsoluteUniversePositionBlit128 PositionAup;
        public float3 Forward;
        public float RangeMeters;
        public float RangeSq;
        public float Intensity;
        public float SpotOuterCos;
        public uint SourceId;
        public uint LastFrame;
        public ushort Slot;
        public byte Flags;
        public byte Reserved;
        public uint ReservedTail0;
        public uint ReservedTail1;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    internal struct RetinalTelemetryEntry
    {
        public uint Frame;
        public ushort TotalBlindPredators;
        public byte ActiveLightCount;
        public byte Flags;
        public float MaxExposure;
        public float3 HottestLightPosition;
        public uint SourceId;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Size = 96)]
    internal struct CognitionControl
    {
        public float3 SpawnAnchor;
        public float3 WanderTarget;
        public float3 OverrideThreatPosition;
        public float3 ScatterDirection;
        public float LastVisualContactTime;
        public float OverrideUntilTime;
        public float NextWanderTargetRefreshTime;
        public float NextAttackAllowedTime;
        public float ScatterUntilTime;
        public float SatedUntilTime;
        public int WanderSequence;
        public int LastPredatorStateCode;
        public uint OverrideStateFlags;
        public int Flags;
        public int Reserved;
        public int Reserved2;
    }

    [StructLayout(LayoutKind.Sequential, Size = 480)]
    internal struct CognitionInput
    {
        public double3 FloatingOriginOffset;
        public AbsoluteUniversePositionBlit128 PlayerTargetAup;
        public AbsoluteUniversePositionBlit128 PackTargetAup;
        public float3 Position;
        public float3 Velocity;
        public float3 Forward;
        public float3 PlayerPosition;
        public float3 PlayerVelocity;
        public float3 PlayerForward;
        public float3 ThreatPosition;
        public float3 RivalApexPosition;
        public float3 PreyPosition;
        public float3 ScavengePosition;
        public float3 PackTargetPosition;
        public float3 PackTargetVelocity;
        public float3 FlockCenter;
        public float3 FlockDirection;
        public float3 FlockAvoidance;
        public float3 ScatterDirection;
        public float DistanceToPlayerSqr;
        public float AttackRange;
        public float HealthNormalized;
        public float FearPressure01;
        public float FleeHealthThreshold;
        public float DeltaTime;
        public float MetabolicDeltaTime;
        public float CurrentTime;
        public float AcousticPingStrength01;
        public float AcousticTransmission01;
        public float ChemicalSignal01;
        public float ChemicalSensitivity;
        public float PlayerLightExposure01;
        public float LightFrenzySpeedMultiplier;
        public float LightReactionFearBoost01;
        public float3 RetinalLightPosition;
        public float RetinalExposure01;
        public float HungerWeight;
        public float ThreatWeight;
        public float FearWeight;
        public float CuriosityWeight;
        public float AggressionWeight;
        public float EscapeDistance;
        public float EscapeSafeDistance;
        public float WanderRadius;
        public float PatrolRadius;
        public float ApexTerritoryRadius;
        public float ApexAggressionMultiplier;
        public float PackCoordinationRadius;
        public float PackFlankDistance;
        public float PackCommitDistance;
        public float FogEndDistanceMeters;
        public float BaseMaxSpeedMetersPerSecond;
        public float ImportanceScore;
        public int SpeciesId;
        public int ClaimedBoidIndex;
        public int FlockCount;
        public int LightReactionMode;
        public int RetinalBlindState;
        public int Flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    internal struct CognitionOutput
    {
        public float3 DesiredDirection;
        public float ForceMultiplier;
        public float SpeedMultiplier;
        public float TurnMultiplier;
        public float HungerScore;
        public float AggressionScore;
        public float FearScore;
        public int StateMask;
        public int LegacyState;
        public int ShouldAttack;
        public int EmitThreatPulse;
        public int PackRoleCode;
        public int FlankingManeuverDetected;
        public int Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Size = 48)]
    internal struct PackedCognitionOutput
    {
        public float3 DesiredDirection;
        public float ForceMultiplier;
        public float SpeedMultiplier;
        public float TurnMultiplier;
        public uint PackedScores;
        public uint StateMask;
        public int LegacyState;
        public uint OutputFlags;
        public uint Reserved0;
        public uint Reserved1;
    }

    [System.Flags]
    internal enum CognitionOutputFlags : uint
    {
        None = 0u,
        ShouldAttack = 1u << 0,
        EmitThreatPulse = 1u << 1,
        PackRoleBait = 1u << 2,
        PackRoleFlanker = 1u << 3,
        FlankingManeuverDetected = 1u << 4,
        BaseSiegeRammer = 1u << 5,
        BaseSiegeDistractor = 1u << 6,
        BaseSiegeLoiterer = 1u << 7,
        EcoHeadless = 1u << 8,
        RetinalBlind = 1u << 9,
    }

    internal enum PredatorPackRole : byte
    {
        None = 0,
        Bait = 1,
        Flanker = 2,
    }

    internal enum BaseSiegeRole : byte
    {
        None = 0,
        Rammer = 1,
        Distractor = 2,
        Loiterer = 3,
    }

    [System.Flags]
    internal enum CognitionControlFlags
    {
        None = 0,
        HasWanderTarget = 1 << 0,
        HasOverrideThreatPosition = 1 << 1,
        RetinalFlinch = 1 << 2,
    }

    [System.Flags]
    internal enum CognitionInputFlags
    {
        None = 0,
        Active = 1 << 0,
        PredatorRole = 1 << 1,
        CanFlee = 1 << 2,
        HasPlayerTarget = 1 << 3,
        HasThreatTarget = 1 << 4,
        HasPreyTarget = 1 << 5,
        HasScavengeTarget = 1 << 6,
        UseHomeTerritory = 1 << 7,
        IsFlocking = 1 << 8,
        HasScatterDirection = 1 << 9,
        IsAggressive = 1 << 10,
        HasVisualPlayerHint = 1 << 11,
        HasApexRivalTarget = 1 << 12,
        IsApexPredator = 1 << 13,
        IsAmbusher = 1 << 14,
        HasPackTarget = 1 << 15,
        HighTierSmoothSteering = 1 << 16,
        RetinalBlind = 1 << 17,
        UseAlphaLeviathanCognition = 1 << 18,
    }

    [System.Flags]
    internal enum CognitionStimulusType
    {
        None = 0,
        Visual = 1 << 0,
        Acoustic = 1 << 1,
        Chemical = 1 << 2,
    }

    [System.Flags]
    internal enum FaunaWorldStateFlags : uint
    {
        None = 0u,
        Active = 1u << 0,
        Hunting = 1u << 1,
        Fleeing = 1u << 2,
        Blind = 1u << 3,
    }

    /// <summary>
    /// Shared fauna cognition domain backed by contiguous native arrays.
    /// Owner: PredatorCognitionDomain. Cap: 256 slots. Eviction: explicit unregister.
    /// </summary>
    internal static class PredatorCognitionDomain
    {
        internal const int Capacity = 256;
        internal const int MemorySlotsPerCreature = 8;
        internal const int AcousticMemorySlotsPerCreature = 5;
        internal const int CognitionCoreSizeBytes = 64;
        private const int CognitionMemoryEntrySizeBytes = 24;
        private const int AcousticMemoryEntrySizeBytes = 40;
        private const int CognitionControlSizeBytes = 96;
        private const int CognitionInputSizeBytes = 480;
        private const int CognitionOutputSizeBytes = 64;
        private const int PackedCognitionOutputSizeBytes = 48;
        private const int SpeciesCognitionTuningSizeBytes = 32;
        private const int PredatorCognitionDtoSizeBytes = 80;
        private const int MesofaunaStateDtoSizeBytes = MesofaunaBehaviorConstants.StateDtoSizeBytes;
        private const int MesofaunaTargetDtoSizeBytes = MesofaunaBehaviorConstants.TargetDtoSizeBytes;
        private const int MesofaunaVisualSyncDtoSizeBytes = MesofaunaBehaviorConstants.VisualSyncDtoSizeBytes;
        private const int MesofaunaTelemetryEntrySizeBytes = MesofaunaBehaviorConstants.TelemetryEntrySizeBytes;
        private const int MesofaunaTuningDtoSizeBytes = MesofaunaBehaviorConstants.TuningDtoSizeBytes;
        private const int MesofaunaSpeciesProfileDtoSizeBytes = MesofaunaBehaviorConstants.SpeciesProfileDtoSizeBytes;
        private const int ApexCortexTuningFloat4Capacity = 1;
        internal static readonly int CognitionCoreAlignmentBytes = UnsafeUtility.AlignOf<CognitionCore>();

        private const float HungerRate = 0.045f;
        private const float FatigueRate = 0.018f;
        private const float FearDecayLogK = -2.302585093f;
        private const float ThreatDecayLogK = -2.995732274f;
        private const float ThreatSmoothingK = 3f;
        private const float MaxThreatSmoothingDeltaTime = 0.05f;
        private const float MemoryLifetimeSeconds = 45f;
        private const float MemoryLifetimeInvSeconds = 1f / MemoryLifetimeSeconds;
        private const float AcousticMemoryLifetimeSeconds = 45f;
        private const float AcousticMemoryLifetimeInvSeconds = 1f / AcousticMemoryLifetimeSeconds;
        private const float MinimumDistanceMeters = 1.25f;
        private const float MinimumAttackCooldown = 0.35f;
        private const float MinimumScoreThreshold = 0.01f;
        private const float WanderTargetRefreshSeconds = 4.5f;
        private const float WanderHashUshortInvScale = 1f / 65535f;
        private const float MaximumWanderVerticalOffset = 6f;
        private const float OverrideScoreBias = 1000f;
        private const float AttackStateBias = 1.25f;
        private const float PassiveLowHealthThreshold = 0.25f;
        private const float ScatterDurationSeconds = 3f;
        private const float AcousticBucketCellSize = 8f;
        private const float AcousticBucketHashBias = 0.15f;
        private const int AcousticBucketOriginBiasCells = 1 << 20;
        private const float AcousticStimulusThreshold = 0.015f;
        private const float ChemicalStimulusThreshold = 0.015f;
        private const float ChemicalSignalRangeMeters = 28f;
        private const float MemoryDistanceSqrFalloff = 0.04f;
        private const float PredatorScentFollowThreshold = 0.1f;
        private const float FearPheromoneContagionShare = 0.3f;
        private const float FearPheromoneInjectionThreshold = 0.1f;
        private const float MinimumDetailedThreatImportanceScore = 0.2f;
        private const float CenterEvaluationIntervalSeconds = 1.0f / 60.0f;
        private const float FocusEvaluationIntervalSeconds = 1.0f / 30.0f;
        private const float PeripheryEvaluationIntervalSeconds = 1.0f / 20.0f;
        private const float FarEvaluationIntervalSeconds = 1.0f / 10.0f;
        private const float RearEvaluationIntervalSeconds = 1.0f / 5.0f;
        private const float PredatorUtilityEvaluationIntervalSeconds = 0.5f;
        private const float PredatorUtilityEvaluationStaggerStepSeconds = PredatorUtilityEvaluationIntervalSeconds * 0.03125f;
        private const int RetinalLightCapacity = 4;
        private const int RetinalLightSignalConsumeLimit = 64;
        private const int RetinalTelemetryCapacity = 300;
        private const int AlphaLeviathanTelemetrySlotCapacity = 64;
        private const int AlphaLeviathanTelemetryVaultCapacity =
            RetinalTelemetryCapacity * AlphaLeviathanTelemetrySlotCapacity;
        private const int RetinalLightStaleFrameWindow = 8;
        private const float RetinalLowTierEvaluationIntervalSeconds = 1f;
        private const float RetinalFrameBudgetStressThresholdSeconds = 1f / 60f;
        private const float RetinalBlindThreshold = 1f;
        private const float RetinalBlindRecoveryThreshold = 0.28f;
        private const float RetinalExposureRiseScale = 0.72f;
        private const float RetinalExposureDecayPerSecond = 0.1f;
        private const float RetinalBlindHoldSeconds = 2.25f;
        private const float RetinalMinLightRangeMeters = 0.1f;
        private const float RetinalMaxLightRangeMeters = 10000f;
        private const float RetinalMaxLightIntensity = 100000f;
        private const uint RetinalBlindPredatorsTelemetryHash = 0x5242544Cu; // RBTL
        private const uint RetinalDumpFailureTelemetryHash = 0x5244464Cu; // RDFL
        private const uint RetinalTelemetryContextHash = 0x4641554Eu; // FAUN
        private const float AlphaLeviathanSlowTickIntervalSeconds = 0.1f;
        private const float AlphaLeviathanEvaluationStaggerStepSeconds = AlphaLeviathanSlowTickIntervalSeconds * 0.03125f;
        private const float AlphaFogFallbackEndMeters = 80f;
        private const float AlphaFogSilhouetteOffsetMeters = 10f;
        private const float AlphaFalseChargeSpeedMetersPerSecond = 30f;
        private const float AlphaFalseChargeVeerDistanceMeters = 15f;
        private const float AlphaFalseChargeMaxSeconds = 2.5f;
        private const float AlphaCirclingHoldSeconds = 2.0f;
        private const float AlphaHiddenHoldSeconds = 1.15f;
        private const float AlphaVeerHoldSeconds = 1.25f;
        private const float AlphaPlayerGazeDotThreshold = 0.8f;
        private const float AlphaRetinalDiveThreshold = 0.35f;
        private const float AlphaDiveDepthMeters = 24f;
        private const float AlphaVeerDistanceMeters = 32f;
        private const float AlphaRingCorrectionScale = 0.08f;
        private const int AlphaLeviathanOscillationLookbackFrames = 24;
        private const byte AlphaLeviathanTelemetryNoPlayerTarget = 1 << 5;
        private const uint AlphaLeviathanPhaseTelemetryHash = 0x414C5048u; // ALPH
        private const uint AlphaLeviathanDumpFailureTelemetryHash = 0x4144464Cu; // ADFL
        private const uint AlphaLeviathanTelemetryContextHash = 0x4C564354u; // LVCT
        private const float PredatorInterceptLeadSeconds = 0.65f;
        private const float PredatorHeadlessDistanceSqr = 1000000f;
        private const float PredatorVisionConeCosineThreshold = 0.28f;
        private const float PredatorVisionConeCosineThresholdSqr =
            PredatorVisionConeCosineThreshold * PredatorVisionConeCosineThreshold;
        private const float PredatorAcousticSightNoiseThreshold01 = 0.12f;
        private const float PredatorAcousticSightRangeSqr = 2500f;
        private const float PredatorAcousticSightInvRangeSqr = 1f / PredatorAcousticSightRangeSqr;
        private const float HighImportanceThreshold = 0.75f;
        private const float FocusImportanceThreshold = 0.50f;
        private const float MidImportanceThreshold = 0.30f;
        private const float LowImportanceThreshold = 0.15f;
        private const float SwarmBucketCellSize = 8f;
        private const float ContagionBucketCellSize = 8f;
        private const float SwarmPerceptionRadius = 8f;
        private const float SwarmSeparationRadius = 2.5f;
        private const float SwarmPbdMinDistance = 1.2f;
        private const float SwarmMaxForce = 6f;
        private const float SwarmSeparationWeight = 1.8f;
        private const float SwarmAlignmentWeight = 1f;
        private const float SwarmCohesionWeight = 0.8f;
        private const float SwarmPbdWeight = 1.5f;
        private const int MaxSwarmNeighborIterations = Capacity;
        private const int EvaluationJobBatchSize = 32;
        private static readonly double _StopwatchMillisecondsPerTick = 1000.0 / Stopwatch.Frequency;
        private const int UnclaimedBoidSlot = -1;
        private const byte SolidThreatVoxel = 255;
        private const byte SignedDistanceSolidThreshold = 128;
        private const float QuantizedByteScale = 255f;
        private const float QuantizedByteInvScale = 1f / QuantizedByteScale;
        private const float HungerMobilityPenaltyThreshold01 = 200f * QuantizedByteInvScale;
        private const float HungerMobilityPenaltySpeedScale = 0.7f;
        private const float DdaEpsilon = 0.000001f;
        private const float MathSafetyEpsilon = 0.0001f;
        private const float FlockCountInvSoftCap = 1f / 6f;
        private const float PlayerFacingBaitThreshold = 0.45f;
        private const float PackFlankHoldDistanceMeters = 3.5f;
        private const float BaseSiegeEngageRadiusMeters = 220f;
        private const float BaseSiegeRammerStandoffMeters = 1.5f;
        private const float BaseSiegeDistractorLateralOffsetMeters = 18f;
        private const float BaseSiegeDistractorForwardOffsetMeters = 8f;
        private const float BaseSiegeLoiterRadiusMeters = 10f;
        private const float BaseSiegeUtilityBias = 0.35f;
        private const float ApexSCurveFrequency = 0.42f;
        private const float ApexSCurvePhaseStep = 0.0625f;
        private const float ApexSCurveLateralWeight = 0.38f;
        private const float ApexSCurveNlerpStalk = 0.18f;
        private const float ApexSCurveNlerpAttack = 0.28f;
        private const float ApexSCurveMaxDistanceMeters = 80f;
        private const float ApexSCurveInvMaxDistanceSqr =
            1f / (ApexSCurveMaxDistanceMeters * ApexSCurveMaxDistanceMeters);
        private const float VortexProbeDistanceMeters = 4f;
        private const float VortexSteeringBlend = 0.72f;
        private const float AmbushSdfProbeDistanceMeters = 4f;
        private const float AmbushHoldDistanceMeters = 2.5f;
        private const float AmbushThreatWakeDistanceMeters = 36f;
        private const int SpatialMemoryRecallCount = 5;
        private const int MaxPackRoleCasAttempts = 3;
        private const int ApexCortexMockSpeciesId = 0x53480A10;
        private const int PredatorTargetSpatialHashBucketCount = 1024;
        private const int PredatorTargetSpatialHashBucketMask = PredatorTargetSpatialHashBucketCount - 1;
        private const int MesofaunaTargetSpatialHashBucketCount = MesofaunaBehaviorConstants.TargetSpatialHashBucketCount;
        private const int MesofaunaTargetSpatialHashBucketMask = MesofaunaTargetSpatialHashBucketCount - 1;
        private const BufferID MesofaunaStateDTOsBufferId = (BufferID)71180;
        private const BufferID MesofaunaMockPreyTargetsBufferId = (BufferID)71181;
        private const BufferID MesofaunaVisualSyncBufferId = (BufferID)71182;
        private const BufferID MesofaunaTelemetryRingBufferId = (BufferID)71183;
        private const BufferID MesofaunaTuningBufferId = (BufferID)71184;
        private const BufferID MesofaunaTargetHashBucketHeadsBufferId = (BufferID)71185;
        private const BufferID MesofaunaTargetHashNextBufferId = (BufferID)71186;
        private const BufferID MesofaunaSpeciesProfilesBufferId = (BufferID)71187;
        private const BufferID MesofaunaSpeciesProfileCountBufferId = (BufferID)71188;
        private const BufferID MesofaunaCsvScratchBufferId = (BufferID)71189;
        private const string ApexCortexBehaviorCsvName = "ai_behavior_overrides.csv";
        private const string MesofaunaSpeciesProfilesCsvName = "mesofauna_species_profiles.csv";

        private unsafe struct VaultArray<T> where T : struct
        {
            public VaultBufferHandle<T> Handle;

            public bool IsCreated => Handle.IsCreated;

            public int Length => Handle.Length;

            public T this[int index]
            {
                get => Handle.GetElementAsReadOnlyRef(_dataVault, index);
                set => Handle.GetElementAsRef(_dataVault, index) = value;
            }

            public NativeArray<T> Resolve()
            {
                return ResolveVaultBuffer(ref Handle);
            }

            public void* GetUnsafePtr()
            {
                return Handle.ResolvePointer(_dataVault);
            }

            public static implicit operator NativeArray<T>(VaultArray<T> array)
            {
                return array.Resolve();
            }

            public static implicit operator VaultArray<T>(VaultBufferHandle<T> handle)
            {
                return new VaultArray<T> { Handle = handle };
            }
        }

        private struct BorrowedArray<T> where T : struct
        {
            public NativeArray<T> Array;

            public bool IsCreated => Array.IsCreated;

            public int Length => Array.Length;

            public T this[int index]
            {
                get => Array[index];
                set => Array[index] = value;
            }

            public static implicit operator NativeArray<T>(BorrowedArray<T> array)
            {
                return array.Array;
            }

            public static implicit operator BorrowedArray<T>(NativeArray<T> array)
            {
                return new BorrowedArray<T> { Array = array };
            }
        }

        private static VaultArray<CognitionCore> _cores;
        private static VaultArray<CognitionControl> _controls;
        private static VaultArray<CognitionInput> _inputs;
        private static VaultArray<PackedCognitionOutput> _outputs;
        private static VaultArray<CognitionMemoryEntry> _memoryBank;
        private static VaultArray<AcousticMemoryEntry> _acousticMemoryBank;
        private static VaultArray<float4> _acousticMemoryFloat4Bank;
        private static VaultArray<float4> _apexCortexTuning;
        private static VaultArray<byte> _slotUsed;
        private static VaultArray<int> _activeSlots;
        private static VaultArray<float> _ambientThreats;
        private static VaultArray<float3> _swarmCenters;
        private static VaultArray<float3> _swarmDirections;
        private static VaultArray<float3> _swarmAvoidances;
        private static VaultArray<int> _swarmCounts;
        private static VaultArray<int> _claimedBoidIndices;
        private static VaultArray<float3> _claimedBoidPositions;
        private static VaultArray<byte> _chosenStates;
        private static VaultArray<byte> _stalkingPhases;
        private static VaultArray<float> _stalkingPhaseStartTimes;
        private static VaultArray<float3> _predatorPackTargets;
        private static VaultArray<float> _predatorPackWeights;
        private static VaultArray<float3> _predatorPackBaitPositions;
        private static VaultArray<float3> _predatorPackSharedPlayerPositions;
        private static VaultArray<AbsoluteUniversePositionBlit128> _predatorPackTargetAups;
        private static VaultArray<byte> _predatorPackRoles;
        private static VaultArray<int> _predatorSpeciesTargetIds;
        private static VaultArray<float3> _predatorSpeciesTargetPositions;
        private static VaultArray<int> _predatorSpeciesTargetCount;
        private static VaultArray<int> _boidClaimTable;
        private static VaultArray<int> _packBaitClaimTable;
        private static VaultArray<int> _packFlankerClaimTable;
        private static VaultArray<HabitatSiegeTargetSnapshot> _habitatSiegeTargets;
        private static VaultArray<int> _baseSiegeRammerClaimTable;
        private static VaultArray<int> _baseSiegeDistractorClaimTable;
        private static VaultArray<int> _baseSiegeLoitererClaimTable;
        private static VaultArray<byte> _evaluationDueFlags;
        private static VaultArray<float> _nextEvaluationTimes;
        private static VaultArray<float> _evaluationIntervals;
        private static VaultArray<float> _retinalExposure;
        private static VaultArray<byte> _blindnessState;
        private static VaultArray<byte> _lastPublishedBlindnessState;
        private static VaultArray<LightSourceData> _retinalLightSources;
        private static VaultArray<RetinalTelemetryEntry> _retinalTelemetryRing;
        private static VaultArray<AlphaLeviathanTelemetryEntry> _alphaLeviathanTelemetryRing;
        private static VaultArray<int> _speciesTuningIds;
        private static VaultArray<SpeciesCognitionTuning> _speciesTuningValues;
        private static VaultArray<int> _speciesTuningCount;
        private static VaultArray<int> _predatorTargetHashBucketHeads;
        private static VaultArray<int> _predatorTargetHashNext;
        private static VaultArray<MesofaunaStateDTO> _mesofaunaStates;
        private static VaultArray<MesofaunaTargetDTO> _mesofaunaMockTargets;
        private static VaultArray<MesofaunaVisualSyncDTO> _mesofaunaVisualSync;
        private static VaultArray<MesofaunaTelemetryEntry> _mesofaunaTelemetryRing;
        private static VaultArray<MesofaunaTuningDTO> _mesofaunaTuning;
        private static VaultArray<MesofaunaSpeciesProfileDTO> _mesofaunaSpeciesProfiles;
        private static VaultArray<int> _mesofaunaSpeciesProfileCount;
        private static VaultArray<byte> _mesofaunaCsvScratch;
        private static VaultArray<int> _mesofaunaTargetHashBucketHeads;
        private static VaultArray<int> _mesofaunaTargetHashNext;
        private static BorrowedArray<byte> _threatVoxelGrid;
        private static int3 _threatVoxelDimensions;
        private static float3 _threatVoxelOrigin;
        private static float3 _threatVoxelCellSize;
        private static byte _threatVoxelSolidThreshold = SolidThreatVoxel;
        private static bool _threatVoxelUsesSignedDistanceEncoding;
        private static BorrowedArray<float4> _chemicalFrontGrid;
        private static BorrowedArray<float4> _chemicalOverlayGrid;
        private static int3 _chemicalGridDimensions;
        private static float3 _chemicalGridOrigin;
        private static float3 _chemicalGridCellSize = new float3(1f, 1f, 1f);
        private static BorrowedArray<ChemicalInfluenceGrid.ChemicalBreadcrumbWaypoint> _chemicalBreadcrumbs;
        private static int _chemicalBreadcrumbCount;
        private static float _chemicalBreadcrumbFollowStepMeters = 12f;
        private static JobHandle _scheduledSwarmHandle;
        private static JobHandle _scheduledEvaluationHandle;
        private static IDataVault _dataVault;
        private static long _evaluationScheduleTimestamp;
        private static bool _evaluationScheduled;
        private static bool _predatorEvaluationJobScheduled;
        private static bool _mesofaunaEvaluationJobScheduled;
        private static int _activeSlotCount;
        private static int _lastEvaluatedFrame = -1;
        private static int _lastScheduledFrame = -1;
        private static int _lastThreatVoxelBindFrame = -1;
        private static int _lastChemicalGridBindFrame = -1;
        private static int _habitatSiegeTargetCount;
        private static int _retinalLightCount;
        private static int _retinalTelemetryCursor;
        private static int _alphaLeviathanTelemetryCursor;
        private static int _activeAlphaLeviathanTelemetryCount;
        private static int _totalBlindPredators;
        private static int _lastTelemetryBlindPredatorCount = -1;
        private static int _mesofaunaTelemetryCursor;
        private static int _mesofaunaLastActiveCount;
        private static int _mesofaunaLastHuntCount;
        private static int _mesofaunaLastNonFiniteFallbackCount;
        private static int _mesofaunaLastSliceModulo = 1;
        private static float _mesofaunaLastQualityWeight = 1f;
        private static float _mesofaunaLastChainMicroseconds;
        private static bool _retinalFaultDumped;
        private static bool _alphaLeviathanFaultDumped;
        private static bool _mesofaunaFaultDumped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDomain()
        {
            Dispose();
        }

        internal static int Register()
        {
            EnsureInitialized();
            if (!_cores.IsCreated ||
                !_slotUsed.IsCreated ||
                !_activeSlots.IsCreated ||
                !_evaluationDueFlags.IsCreated ||
                !_nextEvaluationTimes.IsCreated ||
                !_evaluationIntervals.IsCreated)
                return -1;

            for (int i = 0; i < Capacity; i++)
            {
                if (_slotUsed[i] != 0)
                    continue;

                _slotUsed[i] = 1;
                _cores[i] = default;
                _controls[i] = default;
                _inputs[i] = default;
                _outputs[i] = default;
                ResetMesofaunaSlot(i, float3.zero, 0, MesofaunaBehaviorConstants.StateIdle);
                _evaluationDueFlags[i] = 1;
                _chosenStates[i] = 0;
                _stalkingPhases[i] = AlphaLeviathanPhase.Hidden;
                _stalkingPhaseStartTimes[i] = 0f;
                _nextEvaluationTimes[i] = 0f;
                _evaluationIntervals[i] = CenterEvaluationIntervalSeconds;
                ClearRetinalSlot(i);
                _predatorPackTargets[i] = float3.zero;
                _predatorPackWeights[i] = 0f;
                _predatorPackBaitPositions[i] = float3.zero;
                _predatorPackSharedPlayerPositions[i] = float3.zero;
                _predatorPackTargetAups[i] = default;
                _predatorPackRoles[i] = (byte)PredatorPackRole.None;
                ClearMemoryEntries(i);
                ClearAcousticMemoryEntries(i);
                return i;
            }

            return -1;
        }

        internal static void RegisterSpeciesTuning(int speciesId, in SpeciesCognitionTuning tuning)
        {
            if (speciesId == 0)
                return;

            EnsureInitialized();
            if (!_speciesTuningIds.IsCreated ||
                !_speciesTuningValues.IsCreated ||
                !_speciesTuningCount.IsCreated ||
                _speciesTuningCount.Length <= 0)
                return;

            int count = math.min(
                math.max(_speciesTuningCount[0], 0),
                math.min(_speciesTuningIds.Length, _speciesTuningValues.Length));
            for (int i = 0; i < count; i++)
            {
                if (_speciesTuningIds[i] != speciesId)
                    continue;

                _speciesTuningValues[i] = tuning;
                return;
            }

            if (count >= _speciesTuningIds.Length || count >= _speciesTuningValues.Length)
                return;

            _speciesTuningIds[count] = speciesId;
            _speciesTuningValues[count] = tuning;
            _speciesTuningCount[0] = count + 1;
        }

        internal static void Unregister(int slot)
        {
            if (!IsValidSlot(slot) || !_slotUsed.IsCreated)
                return;

            SetSlotActive(slot, false);
            _slotUsed[slot] = 0;
            _cores[slot] = default;
            _controls[slot] = default;
            _inputs[slot] = default;
            _outputs[slot] = default;
            ResetMesofaunaSlot(slot, float3.zero, 0, MesofaunaBehaviorConstants.StateIdle);
            _evaluationDueFlags[slot] = 0;
            _chosenStates[slot] = 0;
            _stalkingPhases[slot] = AlphaLeviathanPhase.Hidden;
            _stalkingPhaseStartTimes[slot] = 0f;
            _nextEvaluationTimes[slot] = 0f;
            _evaluationIntervals[slot] = CenterEvaluationIntervalSeconds;
            ClearRetinalSlot(slot);
            _predatorPackTargets[slot] = float3.zero;
            _predatorPackWeights[slot] = 0f;
            _predatorPackBaitPositions[slot] = float3.zero;
            _predatorPackSharedPlayerPositions[slot] = float3.zero;
            _predatorPackTargetAups[slot] = default;
            _predatorPackRoles[slot] = (byte)PredatorPackRole.None;
            ClearMemoryEntries(slot);
            ClearAcousticMemoryEntries(slot);
        }

        private static void ClearRetinalSlot(int slot)
        {
            if (_retinalExposure.IsCreated)
                _retinalExposure[slot] = 0f;
            if (_blindnessState.IsCreated)
                _blindnessState[slot] = 0;
            if (_lastPublishedBlindnessState.IsCreated)
                _lastPublishedBlindnessState[slot] = 0;
        }

        internal static void SetSlotActive(int slot, bool active)
        {
            if (!IsValidSlot(slot))
                return;

            if (!_activeSlots.IsCreated ||
                !_evaluationDueFlags.IsCreated ||
                !_nextEvaluationTimes.IsCreated ||
                !_evaluationIntervals.IsCreated)
            {
                return;
            }

            bool currentlyActive = ContainsActiveSlot(slot);
            if (active == currentlyActive)
                return;

            if (active)
            {
                _evaluationDueFlags[slot] = 1;
                _nextEvaluationTimes[slot] = 0f;
                _evaluationIntervals[slot] = CenterEvaluationIntervalSeconds;
                if (_activeSlotCount < _activeSlots.Length)
                    _activeSlots[_activeSlotCount++] = slot;
                return;
            }

            _evaluationDueFlags[slot] = 0;
            _nextEvaluationTimes[slot] = 0f;
            _evaluationIntervals[slot] = CenterEvaluationIntervalSeconds;

            for (int i = 0; i < _activeSlotCount; i++)
            {
                if (_activeSlots[i] != slot)
                    continue;

                _activeSlotCount--;
                _activeSlots[i] = _activeSlots[_activeSlotCount];
                _activeSlots[_activeSlotCount] = 0;
                break;
            }
        }

        internal static void ResetSlot(int slot, float3 spawnAnchor, int speciesId)
        {
            if (!IsValidSlot(slot))
                return;

            CognitionCore core = default;
            core.Position = spawnAnchor;
            core.QuantizedDrives = PackDriveChannels(0.45f, 0f, 0f, 0f);
            core.QuantizedFatigue = PackSingleDrive(0f);
            core.Velocity = float3.zero;
            core.StateFlags = 0u;
            core.MemoryHead = 0;
            core.AcousticMemoryHead = 0;
            core.ClaimedBoidIndex = -1;
            core.SpeciesId = speciesId;
            _cores[slot] = core;

            CognitionControl control = default;
            control.SpawnAnchor = spawnAnchor;
            control.WanderTarget = spawnAnchor;
            control.LastVisualContactTime = float.NegativeInfinity;
            control.OverrideUntilTime = float.NegativeInfinity;
            control.NextWanderTargetRefreshTime = float.NegativeInfinity;
            control.NextAttackAllowedTime = float.NegativeInfinity;
            control.ScatterUntilTime = float.NegativeInfinity;
            control.SatedUntilTime = float.NegativeInfinity;
            control.LastPredatorStateCode = (int)PredatorUtilityState.Prowling;
            _controls[slot] = control;

            _inputs[slot] = default;
            _outputs[slot] = BuildDefaultPackedOutput(new float3(0f, 0f, 1f));
            ResetMesofaunaSlot(slot, spawnAnchor, speciesId, MesofaunaBehaviorConstants.StateSearch);
            _evaluationDueFlags[slot] = 1;
            _chosenStates[slot] = 0;
            _stalkingPhases[slot] = AlphaLeviathanPhase.Hidden;
            _stalkingPhaseStartTimes[slot] = 0f;
            _nextEvaluationTimes[slot] = 0f;
            _evaluationIntervals[slot] = CenterEvaluationIntervalSeconds;
            _retinalExposure[slot] = 0f;
            _blindnessState[slot] = 0;
            _lastPublishedBlindnessState[slot] = 0;
            _predatorPackTargets[slot] = float3.zero;
            _predatorPackWeights[slot] = 0f;
            _predatorPackBaitPositions[slot] = float3.zero;
            _predatorPackSharedPlayerPositions[slot] = float3.zero;
            _predatorPackTargetAups[slot] = default;
            _predatorPackRoles[slot] = (byte)PredatorPackRole.None;
            ClearMemoryEntries(slot);
            ClearAcousticMemoryEntries(slot);
        }

        internal static void SetSpawnAnchor(int slot, float3 spawnAnchor)
        {
            if (!IsValidSlot(slot))
                return;

            CognitionControl control = _controls[slot];
            control.SpawnAnchor = spawnAnchor;
            control.WanderTarget = spawnAnchor;
            control.Flags &= ~(int)CognitionControlFlags.HasWanderTarget;
            _controls[slot] = control;
        }

        internal static void ApplyExternalState(int slot, PredatorUtilityState stateMask, float currentTime)
        {
            if (!IsValidSlot(slot))
                return;

            CognitionControl control = _controls[slot];
            control.OverrideStateFlags = (uint)stateMask;
            control.OverrideUntilTime = currentTime + 4f;
            _controls[slot] = control;
        }

        internal static void ForceRetreat(int slot, float3 threatPosition, float currentTime, float duration)
        {
            if (!IsValidSlot(slot))
                return;

            CognitionControl control = _controls[slot];
            control.OverrideStateFlags = (uint)PredatorUtilityState.Fleeing;
            control.OverrideThreatPosition = threatPosition;
            control.OverrideUntilTime = currentTime + math.max(0.1f, duration);
            control.Flags |= (int)CognitionControlFlags.HasOverrideThreatPosition;
            _controls[slot] = control;
        }

        private static void ProcessMesofaunaDamageSignals(int frameId)
        {
            if (!_activeSlots.IsCreated ||
                !_inputs.IsCreated ||
                !_controls.IsCreated ||
                !_mesofaunaStates.IsCreated ||
                _activeSlotCount <= 0)
            {
                return;
            }

            System.ReadOnlySpan<CombatDamageSignal> damageSignals = SignalBus<CombatDamageSignal>.GetFrameSnapshot();
            if (damageSignals.Length <= 0)
                return;

            for (int signalIndex = 0; signalIndex < damageSignals.Length; signalIndex++)
            {
                CombatDamageSignal signal = damageSignals[signalIndex];
                if (signal.TargetHash == 0u && signal.TargetId == 0)
                    continue;

                for (int i = 0; i < _activeSlotCount; i++)
                {
                    int slot = _activeSlots[i];
                    if ((uint)slot >= (uint)_inputs.Length || (uint)slot >= (uint)_mesofaunaStates.Length)
                        continue;

                    CognitionInput input = _inputs[slot];
                    if (!IsMesofaunaPredator(in input))
                        continue;

                    uint predatorHash = BuildMesofaunaSlotHash(slot, input.SpeciesId);
                    ushort predatorShortId = unchecked((ushort)(predatorHash & 0xFFFFu));
                    if (signal.TargetHash != predatorHash &&
                        (signal.TargetId == 0 || signal.TargetId != predatorShortId))
                    {
                        continue;
                    }

                    MesofaunaStateDTO state = _mesofaunaStates[slot];
                    state.PreviousState = state.CurrentState;
                    state.CurrentState = MesofaunaBehaviorConstants.StateFlee;
                    state.TargetHashID = signal.SourceHash != 0u ? signal.SourceHash : signal.DamageType;
                    state.StateTimerTicks = 0;
                    _mesofaunaStates[slot] = state;

                    if (_chosenStates.IsCreated && (uint)slot < (uint)_chosenStates.Length)
                        _chosenStates[slot] = MesofaunaBehaviorConstants.StateFlee;
                    if (_evaluationDueFlags.IsCreated && (uint)slot < (uint)_evaluationDueFlags.Length)
                        _evaluationDueFlags[slot] = 1;
                    if (_nextEvaluationTimes.IsCreated && (uint)slot < (uint)_nextEvaluationTimes.Length)
                        _nextEvaluationTimes[slot] = 0f;

                    CognitionControl control = _controls[slot];
                    control.OverrideStateFlags = (uint)PredatorUtilityState.Fleeing;
                    control.Flags &= ~(int)CognitionControlFlags.HasOverrideThreatPosition;
                    control.OverrideThreatPosition = default;
                    if (CombatDamageSignalCodec.TryToRuntimePoint(in signal, out float3 runtimeThreat) &&
                        MathGuard.IsFinite(runtimeThreat))
                    {
                        control.OverrideThreatPosition = runtimeThreat;
                        control.Flags |= (int)CognitionControlFlags.HasOverrideThreatPosition;
                    }
                    control.OverrideUntilTime = input.CurrentTime + 2.5f;
                    _controls[slot] = control;
                }
            }
        }

        private static void ProcessMesofaunaRespawnSignals(int frameId)
        {
            if (!_activeSlots.IsCreated ||
                !_inputs.IsCreated ||
                !_controls.IsCreated ||
                !_mesofaunaStates.IsCreated ||
                _activeSlotCount <= 0)
            {
                return;
            }

            System.ReadOnlySpan<PlayerRespawnSignal> respawnSignals = SignalBus<PlayerRespawnSignal>.GetFrameSnapshot();
            if (respawnSignals.Length <= 0)
                return;

            for (int signalIndex = 0; signalIndex < respawnSignals.Length; signalIndex++)
            {
                PlayerRespawnSignal signal = respawnSignals[signalIndex];
                uint signalFlags = signal.Flags;
                bool requestPacket = signal.Phase == PlayerRespawnSignalPhase.Request &&
                                     (signalFlags & PlayerRespawnSignalFlags.Requested) != 0u &&
                                     (signalFlags & PlayerRespawnSignalFlags.Committed) == 0u;
                bool committedPacket = signal.Phase == PlayerRespawnSignalPhase.Committed &&
                                       (signalFlags & PlayerRespawnSignalFlags.Committed) != 0u;
                if (!requestPacket && !committedPacket)
                {
                    continue;
                }

                if (signal.Sequence == 0u ||
                    (signalFlags & PlayerRespawnSignalFlags.InvalidDeathAup) != 0u)
                    continue;

                uint playerHash = signal.PlayerHash != 0u ? signal.PlayerHash : 0x504C5952u;
                for (int i = 0; i < _activeSlotCount; i++)
                {
                    int slot = _activeSlots[i];
                    if ((uint)slot >= (uint)_inputs.Length ||
                        (uint)slot >= (uint)_controls.Length ||
                        (uint)slot >= (uint)_mesofaunaStates.Length)
                    {
                        continue;
                    }

                    CognitionInput input = _inputs[slot];
                    if (!IsMesofaunaPredator(in input))
                        continue;

                    MesofaunaStateDTO state = _mesofaunaStates[slot];
                    bool targetsPlayer = (input.Flags & (int)CognitionInputFlags.HasPlayerTarget) != 0 ||
                                         state.TargetHashID == playerHash;
                    if (!targetsPlayer)
                        continue;

                    state.PreviousState = state.CurrentState;
                    state.CurrentState = MesofaunaBehaviorConstants.StateIdle;
                    state.TargetHashID = 0u;
                    state.StateTimerTicks = 0;
                    state.AggressionScalar = math.min(state.AggressionScalar, 0.12f);
                    state.Velocity = float3.zero;
                    _mesofaunaStates[slot] = state;

                    if (_chosenStates.IsCreated && (uint)slot < (uint)_chosenStates.Length)
                        _chosenStates[slot] = MesofaunaBehaviorConstants.StateIdle;
                    if (_evaluationDueFlags.IsCreated && (uint)slot < (uint)_evaluationDueFlags.Length)
                        _evaluationDueFlags[slot] = 1;
                    if (_nextEvaluationTimes.IsCreated && (uint)slot < (uint)_nextEvaluationTimes.Length)
                        _nextEvaluationTimes[slot] = 0f;
                    if (_outputs.IsCreated && (uint)slot < (uint)_outputs.Length)
                        _outputs[slot] = BuildDefaultPackedOutput(new float3(0f, 0f, 1f));
                    if (_mesofaunaVisualSync.IsCreated && (uint)slot < (uint)_mesofaunaVisualSync.Length)
                        _mesofaunaVisualSync[slot] = default;

                    CognitionControl control = _controls[slot];
                    control.OverrideStateFlags = 0u;
                    control.OverrideThreatPosition = float3.zero;
                    control.OverrideUntilTime = 0f;
                    control.Flags &= ~(int)CognitionControlFlags.HasOverrideThreatPosition;
                    _controls[slot] = control;
                }
            }
        }

        private static bool IsMesofaunaPredator(in CognitionInput input)
        {
            return (input.Flags & (int)CognitionInputFlags.Active) != 0 &&
                   (input.Flags & (int)CognitionInputFlags.PredatorRole) != 0 &&
                   (input.Flags & (int)CognitionInputFlags.IsApexPredator) == 0 &&
                   (input.Flags & (int)CognitionInputFlags.UseAlphaLeviathanCognition) == 0;
        }

        internal static void ForceSated(int slot, float currentTime, float duration)
        {
            if (!IsValidSlot(slot))
                return;

            CognitionControl control = _controls[slot];
            control.SatedUntilTime = currentTime + math.max(0.1f, duration);
            _controls[slot] = control;
        }

        internal static void ReduceFatigue(int slot, float amount)
        {
            if (!IsValidSlot(slot) || amount <= 0f)
                return;

            CognitionCore core = _cores[slot];
            float fatigue = UnpackSingleDrive(core.QuantizedFatigue);
            fatigue = math.clamp(fatigue - amount, 0f, 1f);
            core.QuantizedFatigue = PackSingleDrive(fatigue);
            _cores[slot] = core;
        }

        internal static float GetHunger01(int slot)
        {
            if (!IsValidSlot(slot))
                return 0f;

            UnpackDriveChannels(_cores[slot].QuantizedDrives, out float hunger, out _, out _, out _);
            return hunger;
        }

        internal static void SetHunger01(int slot, float hunger01)
        {
            if (!IsValidSlot(slot))
                return;

            CognitionCore core = _cores[slot];
            UnpackDriveChannels(core.QuantizedDrives, out _, out float aggression, out float fear, out float threatLevel);
            core.QuantizedDrives = PackDriveChannels(math.saturate(hunger01), aggression, fear, threatLevel);
            _cores[slot] = core;
        }

        internal static void NotifyAttackPerformed(int slot, float currentTime, float cooldownSeconds)
        {
            if (!IsValidSlot(slot))
                return;

            CognitionControl control = _controls[slot];
            control.NextAttackAllowedTime = currentTime + math.max(MinimumAttackCooldown, cooldownSeconds);
            _controls[slot] = control;
        }

        internal static void RecordStimulus(int slot, float3 worldPosition, float timeStamp, float intensity, CognitionStimulusType stimulusType)
        {
            if (!IsValidSlot(slot))
                return;

            CognitionCore core = _cores[slot];
            int slotIndex = core.MemoryHead & (MemorySlotsPerCreature - 1);
            int memoryIndex = (slot * MemorySlotsPerCreature) + slotIndex;
            CognitionMemoryEntry entry = default;
            entry.WorldPosition = worldPosition;
            entry.Timestamp = timeStamp;
            entry.Intensity = math.max(0f, intensity);
            entry.StimulusType = (int)stimulusType;
            _memoryBank[memoryIndex] = entry;
            core.MemoryHead = (core.MemoryHead + 1) & (MemorySlotsPerCreature - 1);

            if ((((int)stimulusType) & (int)CognitionStimulusType.Acoustic) != 0)
            {
                int acousticSlotIndex = core.AcousticMemoryHead;
                int acousticMemoryIndex = (slot * AcousticMemorySlotsPerCreature) + acousticSlotIndex;
                int3 acousticBucket = ResolveAcousticBucketCoordinates(worldPosition, AcousticBucketCellSize);

                AcousticMemoryEntry acousticEntry = default;
                acousticEntry.WorldPosition = worldPosition;
                acousticEntry.Timestamp = timeStamp;
                acousticEntry.Intensity = math.max(0f, intensity);
                acousticEntry.BucketCoord = acousticBucket;
                acousticEntry.BucketHash = HashAcousticBucket(acousticBucket);
                _acousticMemoryBank[acousticMemoryIndex] = acousticEntry;
                NativeArray<float4> acousticMemoryFloat4Bank = ResolveAcousticMemoryFloat4Bank();
                if (acousticMemoryFloat4Bank.IsCreated && acousticMemoryIndex < acousticMemoryFloat4Bank.Length)
                    acousticMemoryFloat4Bank[acousticMemoryIndex] = new float4(worldPosition, timeStamp);

                int nextAcousticHead = acousticSlotIndex + 1;
                core.AcousticMemoryHead = math.select(nextAcousticHead, 0, nextAcousticHead >= AcousticMemorySlotsPerCreature);
            }

            _cores[slot] = core;
        }

        internal static void SubmitInput(int slot, in CognitionInput input)
        {
            if (!IsValidSlot(slot))
                return;

            _inputs[slot] = input;
        }

        internal static CognitionOutput GetOutput(int slot, float3 fallbackForward)
        {
            if (!IsValidSlot(slot))
                return BuildDefaultOutput(fallbackForward);

            PackedCognitionOutput packedOutput = _outputs[slot];
            CognitionOutput output = default;
            output.DesiredDirection = packedOutput.DesiredDirection;
            output.ForceMultiplier = packedOutput.ForceMultiplier;
            output.SpeedMultiplier = packedOutput.SpeedMultiplier;
            output.TurnMultiplier = packedOutput.TurnMultiplier;
            UnpackScoreTriplet(packedOutput.PackedScores, out output.HungerScore, out output.AggressionScore, out output.FearScore);
            output.StateMask = (int)packedOutput.StateMask;
            output.LegacyState = packedOutput.LegacyState;
            output.ShouldAttack = (packedOutput.OutputFlags & (uint)CognitionOutputFlags.ShouldAttack) != 0u ? 1 : 0;
            output.EmitThreatPulse = (packedOutput.OutputFlags & (uint)CognitionOutputFlags.EmitThreatPulse) != 0u ? 1 : 0;
            output.PackRoleCode = (packedOutput.OutputFlags & (uint)CognitionOutputFlags.PackRoleFlanker) != 0u
                ? (int)PredatorPackRole.Flanker
                : (packedOutput.OutputFlags & (uint)CognitionOutputFlags.PackRoleBait) != 0u
                    ? (int)PredatorPackRole.Bait
                    : (int)PredatorPackRole.None;
            output.FlankingManeuverDetected = (packedOutput.OutputFlags & (uint)CognitionOutputFlags.FlankingManeuverDetected) != 0u ? 1 : 0;
            if (!MathGuard.IsFinite(output.DesiredDirection) || math.lengthsq(output.DesiredDirection) <= 0.0001f)
                output.DesiredDirection = ResolveDominantAxis(fallbackForward, new float3(0f, 0f, 1f));
            return output;
        }

        internal static int GetChosenState(int slot)
        {
            if (!IsValidSlot(slot) || !_chosenStates.IsCreated)
                return 0;

            return _chosenStates[slot];
        }

        internal static void BeginDispatcherFrame(int frameId)
        {
            if (!_activeSlots.IsCreated)
                return;

            EnsureRetinalVaultBuffers();
            ProcessSubmarineLightSignals(frameId);
            ProcessMesofaunaDamageSignals(frameId);
            ProcessMesofaunaRespawnSignals(frameId);
            EmitFearPheromones();
            ChemicalInfluenceGrid.BeginAiFrame(frameId);
            RefreshThreatVoxelSnapshot(frameId);
            RefreshChemicalGridSnapshot(frameId);
        }

        internal static void LateFrameTick()
        {
            if (!_evaluationScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _scheduledEvaluationHandle, false))
                return;

            float chainMs = (float)((Stopwatch.GetTimestamp() - _evaluationScheduleTimestamp) * _StopwatchMillisecondsPerTick);
            int reportedJobCount = 1 + (_predatorEvaluationJobScheduled ? 1 : 0) + (_mesofaunaEvaluationJobScheduled ? 1 : 0);
            float perJobMs = chainMs / math.max(1, reportedJobCount);
            _mesofaunaLastChainMicroseconds = chainMs * 1000f;
            JobAdmissionScheduleExtensions.ReportAdmittedJobCompleted<SwarmAnalysisJob>(JobAdmissionLane.Lane3_AI, perJobMs);
            if (_predatorEvaluationJobScheduled)
                JobAdmissionScheduleExtensions.ReportAdmittedJobCompleted<PredatorCognitionJob>(JobAdmissionLane.Lane3_AI, perJobMs);
            if (_mesofaunaEvaluationJobScheduled)
                JobAdmissionScheduleExtensions.ReportAdmittedJobCompleted<MesofaunaBehaviorJob>(JobAdmissionLane.Lane3_AI, perJobMs);
            _scheduledSwarmHandle = default;
            _evaluationScheduled = false;
            _lastEvaluatedFrame = _lastScheduledFrame;
            if (_predatorEvaluationJobScheduled)
            {
                UpdateRetinalPostEvaluationTelemetry(_lastEvaluatedFrame);
                UpdateAlphaLeviathanPostEvaluationTelemetry(_lastEvaluatedFrame);
            }
            if (_mesofaunaEvaluationJobScheduled)
                UpdateMesofaunaPostEvaluationTelemetry(_lastEvaluatedFrame);
            _predatorEvaluationJobScheduled = false;
            _mesofaunaEvaluationJobScheduled = false;
        }

        internal static unsafe void ScheduleFrameEvaluation(int frameId)
        {
            if (!_activeSlots.IsCreated ||
                _activeSlotCount <= 0 ||
                _evaluationScheduled ||
                _lastScheduledFrame == frameId)
            {
                return;
            }

            _predatorEvaluationJobScheduled = false;
            _mesofaunaEvaluationJobScheduled = false;
            RefreshThreatVoxelSnapshot(frameId);
            bool hasDueEvaluations = PrepareEvaluationDueFlags();
            if (!hasDueEvaluations)
            {
                _lastScheduledFrame = frameId;
                _lastEvaluatedFrame = frameId;
                return;
            }

            RefreshHabitatSiegeSnapshot();
            ClearBoidClaims();
            ClearPredatorSpeciesTargets();

            float3 swarmBoundsMin = ComputeSwarmBoundsMin();
            float mesofaunaQualityWeight = ResolveMesofaunaGlobalQualityWeight();
            int mesofaunaSliceModulo = ResolveMesofaunaSliceModulo(mesofaunaQualityWeight);
            float mesofaunaVisionRadiusMeters = ResolveMesofaunaVisionRadius(mesofaunaQualityWeight);
            MesofaunaTuningDTO mesofaunaTuning = ResolveMesofaunaRuntimeTuning(mesofaunaQualityWeight);
            RebuildPredatorTargetSpatialHash(swarmBoundsMin);
            NativeArray<int> targetHashBucketHeads = ResolvePredatorTargetHashBucketHeads();
            NativeArray<int> targetHashNext = ResolvePredatorTargetHashNext();
            var swarmJob = new SwarmAnalysisJob
            {
                ActiveSlots = _activeSlots,
                ActiveSlotCount = _activeSlotCount,
                Inputs = _inputs,
                PriorCores = _cores,
                DueFlags = _evaluationDueFlags,
                AmbientThreats = _ambientThreats,
                SwarmCenters = _swarmCenters,
                SwarmDirections = _swarmDirections,
                SwarmAvoidances = _swarmAvoidances,
                SwarmCounts = _swarmCounts,
                ClaimedBoidIndices = _claimedBoidIndices,
                ClaimedBoidPositions = _claimedBoidPositions,
                PredatorPackTargets = _predatorPackTargets,
                PredatorPackWeights = _predatorPackWeights,
                PredatorPackBaitPositions = _predatorPackBaitPositions,
                PredatorPackSharedPlayerPositions = _predatorPackSharedPlayerPositions,
                PredatorPackTargetAups = _predatorPackTargetAups,
                PredatorPackRoles = _predatorPackRoles,
                PredatorSpeciesTargetIds = _predatorSpeciesTargetIds,
                PredatorSpeciesTargetPositions = _predatorSpeciesTargetPositions,
                PredatorSpeciesTargetCount = _predatorSpeciesTargetCount.IsCreated && _predatorSpeciesTargetCount.Length > 0
                    ? (int*)_predatorSpeciesTargetCount.GetUnsafePtr()
                    : null,
                PredatorSpeciesTargetCapacity = ResolvePredatorSpeciesTargetCapacity(),
                PackBaitClaimTable = (int*)_packBaitClaimTable.GetUnsafePtr(),
                PackFlankerClaimTable = (int*)_packFlankerClaimTable.GetUnsafePtr(),
                SwarmBoundsMin = swarmBoundsMin,
                TargetHashBucketHeads = targetHashBucketHeads,
                TargetHashNext = targetHashNext
            };

            if (!swarmJob.TryScheduleParallelAdmitted(
                    _activeSlotCount,
                    EvaluationJobBatchSize,
                    JobAdmissionLane.Lane3_AI,
                    default,
                    out _scheduledSwarmHandle))
            {
                _lastScheduledFrame = frameId;
                return;
            }

            var job = new PredatorCognitionJob
            {
                ActiveSlots = _activeSlots,
                Inputs = _inputs,
                Cores = _cores,
                Controls = _controls,
                MemoryBank = _memoryBank,
                AcousticMemoryBank = _acousticMemoryBank,
                AcousticMemoryFloat4Bank = ResolveAcousticMemoryFloat4Bank(),
                DueFlags = _evaluationDueFlags,
                AmbientThreats = _ambientThreats,
                SwarmCenters = _swarmCenters,
                SwarmDirections = _swarmDirections,
                SwarmAvoidances = _swarmAvoidances,
                SwarmCounts = _swarmCounts,
                ClaimedBoidIndices = _claimedBoidIndices,
                ClaimedBoidPositions = _claimedBoidPositions,
                PredatorPackTargets = _predatorPackTargets,
                PredatorPackWeights = _predatorPackWeights,
                PredatorPackBaitPositions = _predatorPackBaitPositions,
                PredatorPackSharedPlayerPositions = _predatorPackSharedPlayerPositions,
                PredatorPackTargetAups = _predatorPackTargetAups,
                PredatorPackRoles = _predatorPackRoles,
                PredatorSpeciesTargetIds = _predatorSpeciesTargetIds,
                PredatorSpeciesTargetPositions = _predatorSpeciesTargetPositions,
                PredatorSpeciesTargetCount = _predatorSpeciesTargetCount,
                HabitatSiegeTargets = _habitatSiegeTargets,
                HabitatSiegeTargetCount = _habitatSiegeTargetCount,
                BaseSiegeRammerClaimTable = (int*)_baseSiegeRammerClaimTable.GetUnsafePtr(),
                BaseSiegeDistractorClaimTable = (int*)_baseSiegeDistractorClaimTable.GetUnsafePtr(),
                BaseSiegeLoitererClaimTable = (int*)_baseSiegeLoitererClaimTable.GetUnsafePtr(),
                SpeciesTuningIds = _speciesTuningIds,
                SpeciesTuningValues = _speciesTuningValues,
                SpeciesTuningCount = _speciesTuningCount,
                ApexCortexTuning = ResolveApexCortexTuning(),
                ChosenStates = _chosenStates,
                StalkingPhases = _stalkingPhases,
                StalkingPhaseStartTimes = _stalkingPhaseStartTimes,
                BoidClaimTable = _boidClaimTable,
                Outputs = _outputs,
                ThreatVoxelGrid = _threatVoxelGrid,
                ThreatVoxelDimensions = _threatVoxelDimensions,
                ThreatVoxelOrigin = _threatVoxelOrigin,
                ThreatVoxelCellSize = _threatVoxelCellSize,
                ThreatVoxelSolidThreshold = _threatVoxelSolidThreshold,
                ThreatVoxelUsesSignedDistanceEncoding = _threatVoxelUsesSignedDistanceEncoding ? 1 : 0,
                ChemicalBreadcrumbs = _chemicalBreadcrumbs,
                ChemicalBreadcrumbCount = _chemicalBreadcrumbCount,
                ChemicalBreadcrumbFollowStepMeters = _chemicalBreadcrumbFollowStepMeters,
                RetinalLightSources = _retinalLightSources,
                RetinalLightCount = _retinalLightCount,
                RetinalExposure = _retinalExposure,
                BlindnessState = _blindnessState
            };

            if (job.TryScheduleParallelAdmitted(
                    _activeSlotCount,
                    EvaluationJobBatchSize,
                    JobAdmissionLane.Lane3_AI,
                    _scheduledSwarmHandle,
                    out _scheduledEvaluationHandle))
            {
                _predatorEvaluationJobScheduled = true;
            }
            else
            {
                _scheduledEvaluationHandle = _scheduledSwarmHandle;
            }

            NativeArray<int> mesofaunaTargetHashBucketHeads = ResolveMesofaunaTargetHashBucketHeads();
            NativeArray<int> mesofaunaTargetHashNext = ResolveMesofaunaTargetHashNext();
            var mesofaunaMockJob = new GenerateMesofaunaMockTargetsJob
            {
                ActiveSlots = _activeSlots,
                Inputs = _inputs,
                MockTargets = _mesofaunaMockTargets,
                FrameId = frameId,
                GlobalQualityWeight = mesofaunaQualityWeight
            };
            JobHandle mesofaunaMockHandle = mesofaunaMockJob.Schedule(_activeSlotCount, EvaluationJobBatchSize, default);
            var mesofaunaHashJob = new BuildMesofaunaTargetSpatialHashJob
            {
                ActiveSlots = _activeSlots,
                Inputs = _inputs,
                MockTargets = _mesofaunaMockTargets,
                TargetHashBucketHeads = mesofaunaTargetHashBucketHeads,
                TargetHashNext = mesofaunaTargetHashNext,
                ActiveSlotCount = _activeSlotCount,
                SwarmBoundsMin = swarmBoundsMin,
                CellSizeMeters = SwarmBucketCellSize,
                BucketMask = MesofaunaTargetSpatialHashBucketMask
            };
            JobHandle mesofaunaHashHandle = mesofaunaHashJob.Schedule(mesofaunaMockHandle);
            JobHandle mesofaunaDependency = JobHandle.CombineDependencies(
                _scheduledEvaluationHandle,
                mesofaunaHashHandle);
            var mesofaunaJob = new MesofaunaBehaviorJob
            {
                ActiveSlots = _activeSlots,
                Inputs = _inputs,
                Controls = _controls,
                MockTargets = _mesofaunaMockTargets,
                SpeciesProfiles = _mesofaunaSpeciesProfiles,
                States = _mesofaunaStates,
                VisualSync = _mesofaunaVisualSync,
                Outputs = _outputs,
                ChosenStates = _chosenStates,
                TargetHashBucketHeads = mesofaunaTargetHashBucketHeads,
                TargetHashNext = mesofaunaTargetHashNext,
                ThreatVoxelGrid = _threatVoxelGrid,
                ThreatVoxelDimensions = _threatVoxelDimensions,
                ThreatVoxelOrigin = _threatVoxelOrigin,
                ThreatVoxelCellSize = _threatVoxelCellSize,
                ThreatVoxelSolidThreshold = _threatVoxelSolidThreshold,
                ThreatVoxelUsesSignedDistanceEncoding = _threatVoxelUsesSignedDistanceEncoding ? 1 : 0,
                ChemicalBreadcrumbs = _chemicalBreadcrumbs,
                ChemicalBreadcrumbCount = _chemicalBreadcrumbCount,
                ChemicalBreadcrumbFollowStepMeters = _chemicalBreadcrumbFollowStepMeters,
                SwarmBoundsMin = swarmBoundsMin,
                TargetHashCellSizeMeters = SwarmBucketCellSize,
                TargetHashBucketMask = MesofaunaTargetSpatialHashBucketMask,
                FrameId = frameId,
                SliceModulo = mesofaunaSliceModulo,
                GlobalQualityWeight = mesofaunaQualityWeight,
                VisionRadiusMeters = mesofaunaVisionRadiusMeters,
                Tuning = mesofaunaTuning
            };
            if (mesofaunaJob.TryScheduleParallelAdmitted(
                    _activeSlotCount,
                    EvaluationJobBatchSize,
                    JobAdmissionLane.Lane3_AI,
                    mesofaunaDependency,
                    out JobHandle mesofaunaHandle))
            {
                _scheduledEvaluationHandle = mesofaunaHandle;
                _mesofaunaEvaluationJobScheduled = true;
                _mesofaunaLastQualityWeight = mesofaunaQualityWeight;
                _mesofaunaLastSliceModulo = mesofaunaSliceModulo;
            }
            else
            {
                _scheduledEvaluationHandle = mesofaunaDependency;
            }

            _evaluationScheduled = true;
            _evaluationScheduleTimestamp = Stopwatch.GetTimestamp();
            _lastScheduledFrame = frameId;
        }

        internal static void Dispose()
        {
            JobHandle releaseDependency = _evaluationScheduled
                ? JobHandle.CombineDependencies(_scheduledSwarmHandle, _scheduledEvaluationHandle)
                : _scheduledSwarmHandle;
            DispatcherJobFence.TryComplete(ref releaseDependency, forceComplete: true);
            ReleaseVaultHandle(ref _cores);
            ReleaseVaultHandle(ref _controls);
            ReleaseVaultHandle(ref _inputs);
            ReleaseVaultHandle(ref _outputs);
            ReleaseVaultHandle(ref _memoryBank);
            ReleaseVaultHandle(ref _acousticMemoryBank);
            _acousticMemoryFloat4Bank = default;
            _apexCortexTuning = default;
            ReleaseVaultHandle(ref _slotUsed);
            ReleaseVaultHandle(ref _activeSlots);
            ReleaseVaultHandle(ref _ambientThreats);
            ReleaseVaultHandle(ref _swarmCenters);
            ReleaseVaultHandle(ref _swarmDirections);
            ReleaseVaultHandle(ref _swarmAvoidances);
            ReleaseVaultHandle(ref _swarmCounts);
            ReleaseVaultHandle(ref _claimedBoidIndices);
            ReleaseVaultHandle(ref _claimedBoidPositions);
            ReleaseVaultHandle(ref _chosenStates);
            ReleaseVaultHandle(ref _stalkingPhases);
            ReleaseVaultHandle(ref _stalkingPhaseStartTimes);
            ReleaseVaultHandle(ref _predatorPackTargets);
            ReleaseVaultHandle(ref _predatorPackWeights);
            ReleaseVaultHandle(ref _predatorPackBaitPositions);
            ReleaseVaultHandle(ref _predatorPackSharedPlayerPositions);
            ReleaseVaultHandle(ref _predatorPackTargetAups);
            ReleaseVaultHandle(ref _predatorPackRoles);
            ReleaseVaultHandle(ref _predatorSpeciesTargetIds);
            ReleaseVaultHandle(ref _predatorSpeciesTargetPositions);
            ReleaseVaultHandle(ref _predatorSpeciesTargetCount);
            ReleaseVaultHandle(ref _boidClaimTable);
            ReleaseVaultHandle(ref _packBaitClaimTable);
            ReleaseVaultHandle(ref _packFlankerClaimTable);
            ReleaseVaultHandle(ref _habitatSiegeTargets);
            ReleaseVaultHandle(ref _baseSiegeRammerClaimTable);
            ReleaseVaultHandle(ref _baseSiegeDistractorClaimTable);
            ReleaseVaultHandle(ref _baseSiegeLoitererClaimTable);
            ReleaseVaultHandle(ref _evaluationDueFlags);
            ReleaseVaultHandle(ref _nextEvaluationTimes);
            ReleaseVaultHandle(ref _evaluationIntervals);
            ReleaseVaultHandle(ref _speciesTuningIds);
            ReleaseVaultHandle(ref _speciesTuningValues);
            ReleaseVaultHandle(ref _speciesTuningCount);
            ReleaseVaultHandle(ref _retinalExposure);
            ReleaseVaultHandle(ref _blindnessState);
            ReleaseVaultHandle(ref _lastPublishedBlindnessState);
            ReleaseVaultHandle(ref _retinalLightSources);
            ReleaseVaultHandle(ref _retinalTelemetryRing);
            ReleaseVaultHandle(ref _alphaLeviathanTelemetryRing);
            _predatorTargetHashBucketHeads = default;
            _predatorTargetHashNext = default;
            ReleaseVaultHandle(ref _mesofaunaStates);
            ReleaseVaultHandle(ref _mesofaunaMockTargets);
            ReleaseVaultHandle(ref _mesofaunaVisualSync);
            ReleaseVaultHandle(ref _mesofaunaTelemetryRing);
            ReleaseVaultHandle(ref _mesofaunaTuning);
            ReleaseVaultHandle(ref _mesofaunaSpeciesProfiles);
            ReleaseVaultHandle(ref _mesofaunaSpeciesProfileCount);
            ReleaseVaultHandle(ref _mesofaunaCsvScratch);
            _mesofaunaTargetHashBucketHeads = default;
            _mesofaunaTargetHashNext = default;

            _cores = default;
            _controls = default;
            _inputs = default;
            _outputs = default;
            _memoryBank = default;
            _acousticMemoryBank = default;
            _acousticMemoryFloat4Bank = default;
            _apexCortexTuning = default;
            _slotUsed = default;
            _activeSlots = default;
            _ambientThreats = default;
            _swarmCenters = default;
            _swarmDirections = default;
            _swarmAvoidances = default;
            _swarmCounts = default;
            _claimedBoidIndices = default;
            _claimedBoidPositions = default;
            _chosenStates = default;
            _stalkingPhases = default;
            _stalkingPhaseStartTimes = default;
            _predatorPackTargets = default;
            _predatorPackWeights = default;
            _predatorPackBaitPositions = default;
            _predatorPackSharedPlayerPositions = default;
            _predatorPackTargetAups = default;
            _predatorPackRoles = default;
            _predatorSpeciesTargetIds = default;
            _predatorSpeciesTargetPositions = default;
            _predatorSpeciesTargetCount = default;
            _boidClaimTable = default;
            _packBaitClaimTable = default;
            _packFlankerClaimTable = default;
            _habitatSiegeTargets = default;
            _baseSiegeRammerClaimTable = default;
            _baseSiegeDistractorClaimTable = default;
            _baseSiegeLoitererClaimTable = default;
            _evaluationDueFlags = default;
            _nextEvaluationTimes = default;
            _evaluationIntervals = default;
            _retinalExposure = default;
            _blindnessState = default;
            _lastPublishedBlindnessState = default;
            _retinalLightSources = default;
            _retinalTelemetryRing = default;
            _alphaLeviathanTelemetryRing = default;
            _speciesTuningIds = default;
            _speciesTuningValues = default;
            _speciesTuningCount = default;
            _predatorTargetHashBucketHeads = default;
            _predatorTargetHashNext = default;
            _mesofaunaStates = default;
            _mesofaunaMockTargets = default;
            _mesofaunaVisualSync = default;
            _mesofaunaTelemetryRing = default;
            _mesofaunaTuning = default;
            _mesofaunaSpeciesProfiles = default;
            _mesofaunaSpeciesProfileCount = default;
            _mesofaunaCsvScratch = default;
            _mesofaunaTargetHashBucketHeads = default;
            _mesofaunaTargetHashNext = default;
            _threatVoxelGrid = default;
            _threatVoxelDimensions = int3.zero;
            _threatVoxelOrigin = float3.zero;
            _threatVoxelCellSize = new float3(1f, 1f, 1f);
            _threatVoxelSolidThreshold = SolidThreatVoxel;
            _threatVoxelUsesSignedDistanceEncoding = false;
            _chemicalBreadcrumbs = default;
            _chemicalBreadcrumbCount = 0;
            _chemicalBreadcrumbFollowStepMeters = 12f;
            _scheduledSwarmHandle = default;
            _scheduledEvaluationHandle = default;
            _dataVault = null;
            _evaluationScheduled = false;
            _predatorEvaluationJobScheduled = false;
            _mesofaunaEvaluationJobScheduled = false;
            _activeSlotCount = 0;
            _lastEvaluatedFrame = -1;
            _lastScheduledFrame = -1;
            _lastThreatVoxelBindFrame = -1;
            _lastChemicalGridBindFrame = -1;
            _habitatSiegeTargetCount = 0;
            _retinalLightCount = 0;
            _retinalTelemetryCursor = 0;
            _alphaLeviathanTelemetryCursor = 0;
            _activeAlphaLeviathanTelemetryCount = 0;
            _totalBlindPredators = 0;
            _lastTelemetryBlindPredatorCount = -1;
            _mesofaunaTelemetryCursor = 0;
            _mesofaunaLastActiveCount = 0;
            _mesofaunaLastHuntCount = 0;
            _mesofaunaLastNonFiniteFallbackCount = 0;
            _mesofaunaLastSliceModulo = 1;
            _mesofaunaLastQualityWeight = 1f;
            _mesofaunaLastChainMicroseconds = 0f;
            _retinalFaultDumped = false;
            _alphaLeviathanFaultDumped = false;
            _mesofaunaFaultDumped = false;
        }

        private static void EnsureInitialized()
        {
            if (_cores.IsCreated)
            {
                if (_dataVault == null)
                    _dataVault = GlobalRegistry.DataVault;
                return;
            }

            if (!EnsureCoreCognitionVaultBuffers())
                return;

            // VAULT ALIAS: Retinal and Alpha black-box data live in GlobalDataVault.
            EnsureRetinalVaultBuffers();
            EnsureAlphaLeviathanTelemetryVaultBuffer();
            GenerateMockCognitionProfiles();
            ClearBoidClaims();
            ClearPredatorTargetSpatialHash();
            ClearMesofaunaTargetSpatialHash();
        }

        private static bool EnsureCoreCognitionVaultBuffers()
        {
            ValidateAbiLayout();

            if (_cores.IsCreated &&
                _controls.IsCreated &&
                _inputs.IsCreated &&
                _outputs.IsCreated &&
                _memoryBank.IsCreated &&
                _acousticMemoryBank.IsCreated &&
                _acousticMemoryFloat4Bank.IsCreated &&
                _apexCortexTuning.IsCreated &&
                _slotUsed.IsCreated &&
                _activeSlots.IsCreated &&
                _ambientThreats.IsCreated &&
                _swarmCenters.IsCreated &&
                _swarmDirections.IsCreated &&
                _swarmAvoidances.IsCreated &&
                _swarmCounts.IsCreated &&
                _claimedBoidIndices.IsCreated &&
                _claimedBoidPositions.IsCreated &&
                _chosenStates.IsCreated &&
                _stalkingPhases.IsCreated &&
                _stalkingPhaseStartTimes.IsCreated &&
                _predatorPackTargets.IsCreated &&
                _predatorPackWeights.IsCreated &&
                _predatorPackBaitPositions.IsCreated &&
                _predatorPackSharedPlayerPositions.IsCreated &&
                _predatorPackTargetAups.IsCreated &&
                _predatorPackRoles.IsCreated &&
                _predatorSpeciesTargetIds.IsCreated &&
                _predatorSpeciesTargetPositions.IsCreated &&
                _predatorSpeciesTargetCount.IsCreated &&
                _boidClaimTable.IsCreated &&
                _packBaitClaimTable.IsCreated &&
                _packFlankerClaimTable.IsCreated &&
                _habitatSiegeTargets.IsCreated &&
                _baseSiegeRammerClaimTable.IsCreated &&
                _baseSiegeDistractorClaimTable.IsCreated &&
                _baseSiegeLoitererClaimTable.IsCreated &&
                _evaluationDueFlags.IsCreated &&
                _nextEvaluationTimes.IsCreated &&
                _evaluationIntervals.IsCreated &&
                _speciesTuningIds.IsCreated &&
                _speciesTuningValues.IsCreated &&
                _speciesTuningCount.IsCreated &&
                _predatorTargetHashBucketHeads.IsCreated &&
                _predatorTargetHashNext.IsCreated &&
                _mesofaunaStates.IsCreated &&
                _mesofaunaMockTargets.IsCreated &&
                _mesofaunaVisualSync.IsCreated &&
                _mesofaunaTelemetryRing.IsCreated &&
                _mesofaunaTuning.IsCreated &&
                _mesofaunaSpeciesProfiles.IsCreated &&
                _mesofaunaSpeciesProfileCount.IsCreated &&
                _mesofaunaCsvScratch.IsCreated &&
                _mesofaunaTargetHashBucketHeads.IsCreated &&
                _mesofaunaTargetHashNext.IsCreated)
            {
                return true;
            }

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsAllocationLocked)
                return false;
            _dataVault = vault;

            _cores = vault.GetBufferHandle<CognitionCore>(
                BufferID.PredatorCognitionCores,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _controls = vault.GetBufferHandle<CognitionControl>(
                BufferID.PredatorCognitionControls,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _inputs = vault.GetBufferHandle<CognitionInput>(
                BufferID.PredatorCognitionInputs,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _outputs = vault.GetBufferHandle<PackedCognitionOutput>(
                BufferID.PredatorCognitionOutputs,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _memoryBank = vault.GetBufferHandle<CognitionMemoryEntry>(
                BufferID.PredatorCognitionMemoryBank,
                Capacity * MemorySlotsPerCreature,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _acousticMemoryBank = vault.GetBufferHandle<AcousticMemoryEntry>(
                BufferID.PredatorCognitionAcousticMemoryBank,
                Capacity * AcousticMemorySlotsPerCreature,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _acousticMemoryFloat4Bank = vault.GetBufferHandle<float4>(
                BufferID.PredatorCognitionAcousticFloat4Bank,
                Capacity * AcousticMemorySlotsPerCreature,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _apexCortexTuning = vault.GetBufferHandle<float4>(
                BufferID.PredatorCognitionApexCortexTuning,
                ApexCortexTuningFloat4Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _slotUsed = vault.GetBufferHandle<byte>(
                BufferID.PredatorCognitionSlotUsed,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _activeSlots = vault.GetBufferHandle<int>(
                BufferID.PredatorCognitionActiveSlots,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _ambientThreats = vault.GetBufferHandle<float>(
                BufferID.PredatorCognitionAmbientThreats,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _swarmCenters = vault.GetBufferHandle<float3>(
                BufferID.PredatorCognitionSwarmCenters,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _swarmDirections = vault.GetBufferHandle<float3>(
                BufferID.PredatorCognitionSwarmDirections,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _swarmAvoidances = vault.GetBufferHandle<float3>(
                BufferID.PredatorCognitionSwarmAvoidances,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _swarmCounts = vault.GetBufferHandle<int>(
                BufferID.PredatorCognitionSwarmCounts,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _claimedBoidIndices = vault.GetBufferHandle<int>(
                BufferID.PredatorCognitionClaimedBoidIndices,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _claimedBoidPositions = vault.GetBufferHandle<float3>(
                BufferID.PredatorCognitionClaimedBoidPositions,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _chosenStates = vault.GetBufferHandle<byte>(
                BufferID.PredatorCognitionChosenStates,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _stalkingPhases = vault.GetBufferHandle<byte>(
                BufferID.PredatorCognitionStalkingPhases,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _stalkingPhaseStartTimes = vault.GetBufferHandle<float>(
                BufferID.PredatorCognitionStalkingPhaseStartTimes,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _predatorPackTargets = vault.GetBufferHandle<float3>(
                BufferID.PredatorCognitionPackTargets,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _predatorPackWeights = vault.GetBufferHandle<float>(
                BufferID.PredatorCognitionPackWeights,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _predatorPackBaitPositions = vault.GetBufferHandle<float3>(
                BufferID.PredatorCognitionPackBaitPositions,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _predatorPackSharedPlayerPositions = vault.GetBufferHandle<float3>(
                BufferID.PredatorCognitionPackSharedPlayerPositions,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _predatorPackTargetAups = vault.GetBufferHandle<AbsoluteUniversePositionBlit128>(
                BufferID.PredatorCognitionPackTargetAups,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _predatorPackRoles = vault.GetBufferHandle<byte>(
                BufferID.PredatorCognitionPackRoles,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _predatorSpeciesTargetIds = vault.GetBufferHandle<int>(
                BufferID.PredatorCognitionSpeciesTargetIds,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _predatorSpeciesTargetPositions = vault.GetBufferHandle<float3>(
                BufferID.PredatorCognitionSpeciesTargetPositions,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _predatorSpeciesTargetCount = vault.GetBufferHandle<int>(
                BufferID.PredatorCognitionSpeciesTargetCount,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _boidClaimTable = vault.GetBufferHandle<int>(
                BufferID.PredatorCognitionBoidClaimTable,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _packBaitClaimTable = vault.GetBufferHandle<int>(
                BufferID.PredatorCognitionPackBaitClaimTable,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _packFlankerClaimTable = vault.GetBufferHandle<int>(
                BufferID.PredatorCognitionPackFlankerClaimTable,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _habitatSiegeTargets = vault.GetBufferHandle<HabitatSiegeTargetSnapshot>(
                BufferID.PredatorCognitionHabitatSiegeTargets,
                HabitatGraphManager.MaxSiegeTargetCount,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _baseSiegeRammerClaimTable = vault.GetBufferHandle<int>(
                BufferID.PredatorCognitionBaseSiegeRammerClaimTable,
                HabitatGraphManager.MaxSiegeTargetCount,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _baseSiegeDistractorClaimTable = vault.GetBufferHandle<int>(
                BufferID.PredatorCognitionBaseSiegeDistractorClaimTable,
                HabitatGraphManager.MaxSiegeTargetCount,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _baseSiegeLoitererClaimTable = vault.GetBufferHandle<int>(
                BufferID.PredatorCognitionBaseSiegeLoitererClaimTable,
                HabitatGraphManager.MaxSiegeTargetCount,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _evaluationDueFlags = vault.GetBufferHandle<byte>(
                BufferID.PredatorCognitionEvaluationDueFlags,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _nextEvaluationTimes = vault.GetBufferHandle<float>(
                BufferID.PredatorCognitionNextEvaluationTimes,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _evaluationIntervals = vault.GetBufferHandle<float>(
                BufferID.PredatorCognitionEvaluationIntervals,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _speciesTuningIds = vault.GetBufferHandle<int>(
                BufferID.PredatorCognitionSpeciesTuningIds,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _speciesTuningValues = vault.GetBufferHandle<SpeciesCognitionTuning>(
                BufferID.PredatorCognitionSpeciesTuningValues,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _speciesTuningCount = vault.GetBufferHandle<int>(
                BufferID.PredatorCognitionSpeciesTuningCount,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _predatorTargetHashBucketHeads = vault.GetBufferHandle<int>(
                BufferID.PredatorCognitionTargetHashBucketHeads,
                PredatorTargetSpatialHashBucketCount,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _predatorTargetHashNext = vault.GetBufferHandle<int>(
                BufferID.PredatorCognitionTargetHashNext,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _mesofaunaStates = vault.GetBufferHandle<MesofaunaStateDTO>(
                MesofaunaStateDTOsBufferId,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            _mesofaunaMockTargets = vault.GetBufferHandle<MesofaunaTargetDTO>(
                MesofaunaMockPreyTargetsBufferId,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            _mesofaunaVisualSync = vault.GetBufferHandle<MesofaunaVisualSyncDTO>(
                MesofaunaVisualSyncBufferId,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _mesofaunaTelemetryRing = vault.GetBufferHandle<MesofaunaTelemetryEntry>(
                MesofaunaTelemetryRingBufferId,
                MesofaunaBehaviorConstants.TelemetryCapacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _mesofaunaTuning = vault.GetBufferHandle<MesofaunaTuningDTO>(
                MesofaunaTuningBufferId,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _mesofaunaSpeciesProfiles = vault.GetBufferHandle<MesofaunaSpeciesProfileDTO>(
                MesofaunaSpeciesProfilesBufferId,
                MesofaunaBehaviorConstants.SpeciesProfileCapacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _mesofaunaSpeciesProfileCount = vault.GetBufferHandle<int>(
                MesofaunaSpeciesProfileCountBufferId,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _mesofaunaCsvScratch = vault.GetBufferHandle<byte>(
                MesofaunaCsvScratchBufferId,
                MesofaunaBehaviorConstants.CsvScratchBytes,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            _mesofaunaTargetHashBucketHeads = vault.GetBufferHandle<int>(
                MesofaunaTargetHashBucketHeadsBufferId,
                MesofaunaTargetSpatialHashBucketCount,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            _mesofaunaTargetHashNext = vault.GetBufferHandle<int>(
                MesofaunaTargetHashNextBufferId,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);

            bool resolvedAll =
                _cores.IsCreated &&
                _controls.IsCreated &&
                _inputs.IsCreated &&
                _outputs.IsCreated &&
                _memoryBank.IsCreated &&
                _acousticMemoryBank.IsCreated &&
                ResolveAcousticMemoryFloat4Bank().IsCreated &&
                ResolveApexCortexTuning().IsCreated &&
                _slotUsed.IsCreated &&
                _activeSlots.IsCreated &&
                _ambientThreats.IsCreated &&
                _swarmCenters.IsCreated &&
                _swarmDirections.IsCreated &&
                _swarmAvoidances.IsCreated &&
                _swarmCounts.IsCreated &&
                _claimedBoidIndices.IsCreated &&
                _claimedBoidPositions.IsCreated &&
                _chosenStates.IsCreated &&
                _stalkingPhases.IsCreated &&
                _stalkingPhaseStartTimes.IsCreated &&
                _predatorPackTargets.IsCreated &&
                _predatorPackWeights.IsCreated &&
                _predatorPackBaitPositions.IsCreated &&
                _predatorPackSharedPlayerPositions.IsCreated &&
                _predatorPackTargetAups.IsCreated &&
                _predatorPackRoles.IsCreated &&
                _predatorSpeciesTargetIds.IsCreated &&
                _predatorSpeciesTargetPositions.IsCreated &&
                _predatorSpeciesTargetCount.IsCreated &&
                _boidClaimTable.IsCreated &&
                _packBaitClaimTable.IsCreated &&
                _packFlankerClaimTable.IsCreated &&
                _habitatSiegeTargets.IsCreated &&
                _baseSiegeRammerClaimTable.IsCreated &&
                _baseSiegeDistractorClaimTable.IsCreated &&
                _baseSiegeLoitererClaimTable.IsCreated &&
                _evaluationDueFlags.IsCreated &&
                _nextEvaluationTimes.IsCreated &&
                _evaluationIntervals.IsCreated &&
                _speciesTuningIds.IsCreated &&
                _speciesTuningValues.IsCreated &&
                _speciesTuningCount.IsCreated &&
                ResolvePredatorTargetHashBucketHeads().IsCreated &&
                ResolvePredatorTargetHashNext().IsCreated &&
                _mesofaunaStates.IsCreated &&
                _mesofaunaMockTargets.IsCreated &&
                _mesofaunaVisualSync.IsCreated &&
                _mesofaunaTelemetryRing.IsCreated &&
                _mesofaunaTuning.IsCreated &&
                _mesofaunaSpeciesProfiles.IsCreated &&
                _mesofaunaSpeciesProfileCount.IsCreated &&
                _mesofaunaCsvScratch.IsCreated &&
                ResolveMesofaunaTargetHashBucketHeads().IsCreated &&
                ResolveMesofaunaTargetHashNext().IsCreated;
            if (!resolvedAll)
            {
                ReleaseCoreCognitionVaultHandles();
                _activeSlotCount = 0;
                return false;
            }

            ClearCoreCognitionVaultBuffers();
            InitializeMesofaunaVaultBuffersCold();
            TryLoadMesofaunaSpeciesProfilesCsvCold();
            _activeSlotCount = 0;
            return true;
        }

        private static void ReleaseCoreCognitionVaultHandles()
        {
            _activeSlotCount = 0;
            ReleaseVaultHandle(ref _cores);
            ReleaseVaultHandle(ref _controls);
            ReleaseVaultHandle(ref _inputs);
            ReleaseVaultHandle(ref _outputs);
            ReleaseVaultHandle(ref _memoryBank);
            ReleaseVaultHandle(ref _acousticMemoryBank);
            _acousticMemoryFloat4Bank = default;
            _apexCortexTuning = default;
            ReleaseVaultHandle(ref _slotUsed);
            ReleaseVaultHandle(ref _activeSlots);
            ReleaseVaultHandle(ref _ambientThreats);
            ReleaseVaultHandle(ref _swarmCenters);
            ReleaseVaultHandle(ref _swarmDirections);
            ReleaseVaultHandle(ref _swarmAvoidances);
            ReleaseVaultHandle(ref _swarmCounts);
            ReleaseVaultHandle(ref _claimedBoidIndices);
            ReleaseVaultHandle(ref _claimedBoidPositions);
            ReleaseVaultHandle(ref _chosenStates);
            ReleaseVaultHandle(ref _stalkingPhases);
            ReleaseVaultHandle(ref _stalkingPhaseStartTimes);
            ReleaseVaultHandle(ref _predatorPackTargets);
            ReleaseVaultHandle(ref _predatorPackWeights);
            ReleaseVaultHandle(ref _predatorPackBaitPositions);
            ReleaseVaultHandle(ref _predatorPackSharedPlayerPositions);
            ReleaseVaultHandle(ref _predatorPackTargetAups);
            ReleaseVaultHandle(ref _predatorPackRoles);
            ReleaseVaultHandle(ref _predatorSpeciesTargetIds);
            ReleaseVaultHandle(ref _predatorSpeciesTargetPositions);
            ReleaseVaultHandle(ref _predatorSpeciesTargetCount);
            ReleaseVaultHandle(ref _boidClaimTable);
            ReleaseVaultHandle(ref _packBaitClaimTable);
            ReleaseVaultHandle(ref _packFlankerClaimTable);
            ReleaseVaultHandle(ref _habitatSiegeTargets);
            ReleaseVaultHandle(ref _baseSiegeRammerClaimTable);
            ReleaseVaultHandle(ref _baseSiegeDistractorClaimTable);
            ReleaseVaultHandle(ref _baseSiegeLoitererClaimTable);
            ReleaseVaultHandle(ref _evaluationDueFlags);
            ReleaseVaultHandle(ref _nextEvaluationTimes);
            ReleaseVaultHandle(ref _evaluationIntervals);
            ReleaseVaultHandle(ref _speciesTuningIds);
            ReleaseVaultHandle(ref _speciesTuningValues);
            ReleaseVaultHandle(ref _speciesTuningCount);
            _predatorTargetHashBucketHeads = default;
            _predatorTargetHashNext = default;
            ReleaseVaultHandle(ref _mesofaunaStates);
            ReleaseVaultHandle(ref _mesofaunaMockTargets);
            ReleaseVaultHandle(ref _mesofaunaVisualSync);
            ReleaseVaultHandle(ref _mesofaunaTelemetryRing);
            ReleaseVaultHandle(ref _mesofaunaTuning);
            ReleaseVaultHandle(ref _mesofaunaSpeciesProfiles);
            ReleaseVaultHandle(ref _mesofaunaSpeciesProfileCount);
            ReleaseVaultHandle(ref _mesofaunaCsvScratch);
            _mesofaunaTargetHashBucketHeads = default;
            _mesofaunaTargetHashNext = default;
        }

        private static void ClearCoreCognitionVaultBuffers()
        {
            ClearArray(_cores);
            ClearArray(_controls);
            ClearArray(_inputs);
            ClearArray(_outputs);
            ClearArray(_memoryBank);
            ClearArray(_acousticMemoryBank);
            ClearArray(ResolveAcousticMemoryFloat4Bank());
            ClearArray(ResolveApexCortexTuning());
            ClearArray(_slotUsed);
            ClearArray(_activeSlots);
            ClearArray(_ambientThreats);
            ClearArray(_swarmCenters);
            ClearArray(_swarmDirections);
            ClearArray(_swarmAvoidances);
            ClearArray(_swarmCounts);
            ClearArray(_claimedBoidIndices);
            ClearArray(_claimedBoidPositions);
            ClearArray(_chosenStates);
            ClearArray(_stalkingPhases);
            ClearArray(_stalkingPhaseStartTimes);
            ClearArray(_predatorPackTargets);
            ClearArray(_predatorPackWeights);
            ClearArray(_predatorPackBaitPositions);
            ClearArray(_predatorPackSharedPlayerPositions);
            ClearArray(_predatorPackTargetAups);
            ClearArray(_predatorPackRoles);
            ClearArray(_predatorSpeciesTargetIds);
            ClearArray(_predatorSpeciesTargetPositions);
            ClearArray(_predatorSpeciesTargetCount);
            ClearArray(_boidClaimTable);
            ClearArray(_packBaitClaimTable);
            ClearArray(_packFlankerClaimTable);
            ClearArray(_habitatSiegeTargets);
            ClearArray(_baseSiegeRammerClaimTable);
            ClearArray(_baseSiegeDistractorClaimTable);
            ClearArray(_baseSiegeLoitererClaimTable);
            ClearArray(_evaluationDueFlags);
            ClearArray(_nextEvaluationTimes);
            ClearArray(_evaluationIntervals);
            ClearArray(_speciesTuningIds);
            ClearArray(_speciesTuningValues);
            ClearArray(_speciesTuningCount);
            ClearPredatorTargetSpatialHash();
            ClearMesofaunaTargetSpatialHash();
        }

        private static void ClearArray<T>(NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            for (int i = 0; i < array.Length; i++)
                array[i] = default;
        }

        private static void ClearArray<T>(VaultArray<T> array) where T : struct
        {
            ClearArray(array.Resolve());
        }

        private static NativeArray<float4> ResolveAcousticMemoryFloat4Bank()
        {
            return _acousticMemoryFloat4Bank.Resolve();
        }

        private static NativeArray<float4> ResolveApexCortexTuning()
        {
            return _apexCortexTuning.Resolve();
        }

        private static NativeArray<int> ResolvePredatorTargetHashBucketHeads()
        {
            return _predatorTargetHashBucketHeads.Resolve();
        }

        private static NativeArray<int> ResolvePredatorTargetHashNext()
        {
            return _predatorTargetHashNext.Resolve();
        }

        private static NativeArray<int> ResolveMesofaunaTargetHashBucketHeads()
        {
            return _mesofaunaTargetHashBucketHeads.Resolve();
        }

        private static NativeArray<int> ResolveMesofaunaTargetHashNext()
        {
            return _mesofaunaTargetHashNext.Resolve();
        }

        private static NativeArray<T> ResolveVaultBuffer<T>(ref VaultBufferHandle<T> handle) where T : struct
        {
            return handle.IsCreated && _dataVault != null ? handle.Resolve(_dataVault) : default;
        }

        private static void InitializeMesofaunaVaultBuffersCold()
        {
            NativeArray<MesofaunaStateDTO> states = _mesofaunaStates.Resolve();
            NativeArray<MesofaunaTargetDTO> mockTargets = _mesofaunaMockTargets.Resolve();
            NativeArray<MesofaunaVisualSyncDTO> visualSync = _mesofaunaVisualSync.Resolve();
            NativeArray<MesofaunaTelemetryEntry> telemetry = _mesofaunaTelemetryRing.Resolve();
            NativeArray<MesofaunaTuningDTO> tuning = _mesofaunaTuning.Resolve();
            NativeArray<MesofaunaSpeciesProfileDTO> speciesProfiles = _mesofaunaSpeciesProfiles.Resolve();
            NativeArray<int> speciesProfileCount = _mesofaunaSpeciesProfileCount.Resolve();
            NativeArray<byte> csvScratch = _mesofaunaCsvScratch.Resolve();
            NativeArray<int> bucketHeads = ResolveMesofaunaTargetHashBucketHeads();
            NativeArray<int> next = ResolveMesofaunaTargetHashNext();
            if (!states.IsCreated ||
                !mockTargets.IsCreated ||
                !visualSync.IsCreated ||
                !telemetry.IsCreated ||
                !tuning.IsCreated ||
                !speciesProfiles.IsCreated ||
                !speciesProfileCount.IsCreated ||
                !csvScratch.IsCreated ||
                !bucketHeads.IsCreated ||
                !next.IsCreated)
            {
                return;
            }

            int initCount = math.max(
                Capacity,
                math.max(
                    MesofaunaBehaviorConstants.CsvScratchBytes,
                    math.max(
                        MesofaunaBehaviorConstants.TelemetryCapacity,
                        MesofaunaTargetSpatialHashBucketCount)));
            var initJob = new InitializeMesofaunaStateJob
            {
                States = states,
                MockTargets = mockTargets,
                VisualSync = visualSync,
                TelemetryRing = telemetry,
                Tuning = tuning,
                SpeciesProfiles = speciesProfiles,
                SpeciesProfileCount = speciesProfileCount,
                CsvScratch = csvScratch,
                TargetHashBucketHeads = bucketHeads,
                TargetHashNext = next,
                DefaultTuning = MesofaunaTuningDTO.CreateDefault(ResolveMesofaunaGlobalQualityWeight())
            };
            // COLD SYNC JOB: Uninitialized mesofauna vault lanes are fully overwritten once before dispatch locks.
            for (int i = 0; i < initCount; i++)
                initJob.Execute(i);
        }

        private static void ResetMesofaunaSlot(int slot, float3 runtimePosition, int speciesId, int stateCode)
        {
            if (!_mesofaunaStates.IsCreated ||
                !_mesofaunaVisualSync.IsCreated ||
                slot < 0 ||
                slot >= Capacity)
            {
                return;
            }

            double3 aup = double3.zero;
            if (MathGuard.IsFinite(runtimePosition))
            {
                AbsoluteUniversePosition absolute = AbsoluteUniversePosition.FromRuntimePosition(
                    new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
                aup = absolute.ToAbsoluteDouble3();
            }

            MesofaunaStateDTO state = default;
            state.AUP_Position = aup;
            state.CurrentState = (byte)math.clamp(stateCode, MesofaunaBehaviorConstants.StateIdle, MesofaunaBehaviorConstants.StateTrackScent);
            state.PreviousState = state.CurrentState;
            state.AggressionScalar = speciesId == 0 ? 0f : 0.5f;
            state.TargetHashID = BuildMesofaunaSlotHash(slot, speciesId);
            _mesofaunaStates[slot] = state;
            _mesofaunaVisualSync[slot] = default;

            if (_mesofaunaMockTargets.IsCreated)
                _mesofaunaMockTargets[slot] = default;
            if (_mesofaunaTargetHashNext.IsCreated)
                _mesofaunaTargetHashNext[slot] = UnclaimedBoidSlot;
        }

        private static uint BuildMesofaunaSlotHash(int slot, int speciesId)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)slot) * 16777619u;
                hash = (hash ^ (uint)speciesId) * 16777619u;
                hash = (hash ^ 0x4D45534Fu) * 16777619u;
                return hash == 0u ? 0x4D45534Fu : hash;
            }
        }

        private static float ResolveMesofaunaGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return float.IsFinite(weight) ? math.saturate(weight) : 1f;
        }

        private static int ResolveMesofaunaSliceModulo(float qualityWeight)
        {
            float q = MesofaunaBehaviorConstants.Smooth01(qualityWeight);
            return math.clamp((int)math.round(math.lerp(10f, 1f, q)), 1, 10);
        }

        private static float ResolveMesofaunaVisionRadius(float qualityWeight)
        {
            float q = MesofaunaBehaviorConstants.Smooth01(qualityWeight);
            return math.lerp(22f, 104f, q);
        }

        private static MesofaunaTuningDTO ResolveMesofaunaRuntimeTuning(float qualityWeight)
        {
            MesofaunaTuningDTO tuning = MesofaunaTuningDTO.CreateDefault(qualityWeight);
            if (_mesofaunaTuning.IsCreated && _mesofaunaTuning.Length > 0)
            {
                MesofaunaTuningDTO stored = _mesofaunaTuning[0];
                if (float.IsFinite(stored.VisionRadiusLow) && stored.VisionRadiusLow > 0f)
                    tuning.VisionRadiusLow = math.clamp(stored.VisionRadiusLow, 4f, 160f);
                if (float.IsFinite(stored.VisionRadiusUltra) && stored.VisionRadiusUltra >= tuning.VisionRadiusLow)
                    tuning.VisionRadiusUltra = math.clamp(stored.VisionRadiusUltra, tuning.VisionRadiusLow, 220f);
                if (float.IsFinite(stored.ScentSensitivity) && stored.ScentSensitivity > 0f)
                    tuning.ScentSensitivity = math.clamp(stored.ScentSensitivity, 0.05f, 4f);
                if (float.IsFinite(stored.BaseSpeedMetersPerSecond) && stored.BaseSpeedMetersPerSecond > 0f)
                    tuning.BaseSpeedMetersPerSecond = math.clamp(stored.BaseSpeedMetersPerSecond, 0.5f, 30f);
                tuning.IdleToSearchTicks = (ushort)math.clamp(stored.IdleToSearchTicks > 0 ? stored.IdleToSearchTicks : tuning.IdleToSearchTicks, 1, ushort.MaxValue);
                tuning.SearchToIdleTicks = (ushort)math.clamp(stored.SearchToIdleTicks > 0 ? stored.SearchToIdleTicks : tuning.SearchToIdleTicks, 1, ushort.MaxValue);
                if (float.IsFinite(stored.StateTimeoutSeconds) && stored.StateTimeoutSeconds > 0f)
                    tuning.StateTimeoutSeconds = math.clamp(stored.StateTimeoutSeconds, 0.1f, 60f);
                tuning.Flags = stored.Flags == 0u ? tuning.Flags : stored.Flags;
            }

            tuning.GlobalQualityWeight = math.saturate(qualityWeight);
            if (_mesofaunaTuning.IsCreated && _mesofaunaTuning.Length > 0)
                _mesofaunaTuning[0] = tuning;
            return tuning;
        }

        private static void ClearPredatorTargetSpatialHash()
        {
            NativeArray<int> bucketHeads = ResolvePredatorTargetHashBucketHeads();
            if (bucketHeads.IsCreated)
            {
                for (int i = 0; i < bucketHeads.Length; i++)
                    bucketHeads[i] = UnclaimedBoidSlot;
            }

            NativeArray<int> hashNext = ResolvePredatorTargetHashNext();
            if (hashNext.IsCreated)
            {
                for (int i = 0; i < hashNext.Length; i++)
                hashNext[i] = UnclaimedBoidSlot;
            }
        }

        private static void ClearMesofaunaTargetSpatialHash()
        {
            NativeArray<int> bucketHeads = ResolveMesofaunaTargetHashBucketHeads();
            if (bucketHeads.IsCreated)
            {
                for (int i = 0; i < bucketHeads.Length; i++)
                    bucketHeads[i] = UnclaimedBoidSlot;
            }

            NativeArray<int> hashNext = ResolveMesofaunaTargetHashNext();
            if (hashNext.IsCreated)
            {
                for (int i = 0; i < hashNext.Length; i++)
                    hashNext[i] = UnclaimedBoidSlot;
            }
        }

        private static void RebuildPredatorTargetSpatialHash(float3 boundsMin)
        {
            NativeArray<int> bucketHeads = ResolvePredatorTargetHashBucketHeads();
            NativeArray<int> hashNext = ResolvePredatorTargetHashNext();
            if (!bucketHeads.IsCreated || !hashNext.IsCreated)
                return;

            ClearPredatorTargetSpatialHash();
            int safeCount = math.min(_activeSlotCount, _activeSlots.IsCreated ? _activeSlots.Length : 0);
            for (int i = 0; i < safeCount; i++)
            {
                int slot = _activeSlots[i];
                if ((uint)slot >= (uint)hashNext.Length)
                    continue;

                CognitionInput input = _inputs[slot];
                if ((input.Flags & (int)CognitionInputFlags.Active) == 0)
                    continue;

                int3 bucket = ResolveSpatialBucketCoordinates(input.Position, boundsMin, SwarmBucketCellSize);
                int bucketIndex = HashSpatialBucket(bucket) & PredatorTargetSpatialHashBucketMask;
                hashNext[slot] = bucketHeads[bucketIndex];
                bucketHeads[bucketIndex] = slot;
            }
        }

        private static void RebuildMesofaunaTargetSpatialHash(float3 boundsMin)
        {
            NativeArray<int> bucketHeads = ResolveMesofaunaTargetHashBucketHeads();
            NativeArray<int> hashNext = ResolveMesofaunaTargetHashNext();
            if (!bucketHeads.IsCreated || !hashNext.IsCreated)
                return;

            ClearMesofaunaTargetSpatialHash();
            int safeCount = math.min(_activeSlotCount, _activeSlots.IsCreated ? _activeSlots.Length : 0);
            for (int i = 0; i < safeCount; i++)
            {
                int slot = _activeSlots[i];
                if ((uint)slot >= (uint)hashNext.Length)
                    continue;

                CognitionInput input = _inputs[slot];
                if ((input.Flags & (int)CognitionInputFlags.Active) == 0)
                    continue;

                int3 bucket = ResolveSpatialBucketCoordinates(input.Position, boundsMin, SwarmBucketCellSize);
                int bucketIndex = HashSpatialBucket(bucket) & MesofaunaTargetSpatialHashBucketMask;
                hashNext[slot] = bucketHeads[bucketIndex];
                bucketHeads[bucketIndex] = slot;
            }
        }

        private static int HashSpatialBucket(int3 bucketCoord)
        {
            unchecked
            {
                uint x = (uint)bucketCoord.x;
                uint y = (uint)bucketCoord.y;
                uint z = (uint)bucketCoord.z;
                return (int)((x * 73856093u) ^ (y * 19349663u) ^ (z * 83492791u));
            }
        }

        internal static bool TryGetApexCortexTuning(out ApexCortexTuningSnapshot snapshot)
        {
            EnsureInitialized();
            return TryReadApexCortexTuningNoEnsure(out snapshot);
        }

        internal static bool TrySetApexCortexTuning(in ApexCortexTuningSnapshot snapshot)
        {
            EnsureInitialized();
            NativeArray<float4> apexCortexTuning = ResolveApexCortexTuning();
            if (!apexCortexTuning.IsCreated || apexCortexTuning.Length <= 0)
                return false;

            WriteApexCortexTuningNoEnsure(in snapshot);
            return true;
        }

        internal static bool TryGetMesofaunaTuning(out MesofaunaTuningDTO tuning)
        {
            EnsureInitialized();
            tuning = default;
            if (!_mesofaunaTuning.IsCreated || _mesofaunaTuning.Length <= 0)
                return false;

            tuning = _mesofaunaTuning[0];
            return true;
        }

        internal static bool TrySetMesofaunaTuning(in MesofaunaTuningDTO tuning)
        {
            EnsureInitialized();
            if (!_mesofaunaTuning.IsCreated || _mesofaunaTuning.Length <= 0)
                return false;

            MesofaunaTuningDTO sanitized = tuning;
            sanitized.VisionRadiusLow = math.clamp(
                float.IsFinite(sanitized.VisionRadiusLow) ? sanitized.VisionRadiusLow : 22f,
                4f,
                160f);
            sanitized.VisionRadiusUltra = math.clamp(
                float.IsFinite(sanitized.VisionRadiusUltra) ? sanitized.VisionRadiusUltra : 104f,
                sanitized.VisionRadiusLow,
                220f);
            sanitized.ScentSensitivity = math.clamp(
                float.IsFinite(sanitized.ScentSensitivity) ? sanitized.ScentSensitivity : 1f,
                0.05f,
                4f);
            sanitized.BaseSpeedMetersPerSecond = math.clamp(
                float.IsFinite(sanitized.BaseSpeedMetersPerSecond) ? sanitized.BaseSpeedMetersPerSecond : 6f,
                0.5f,
                30f);
            sanitized.StateTimeoutSeconds = math.clamp(
                float.IsFinite(sanitized.StateTimeoutSeconds) ? sanitized.StateTimeoutSeconds : 4.5f,
                0.1f,
                60f);
            sanitized.IdleToSearchTicks = (ushort)math.max(1, sanitized.IdleToSearchTicks);
            sanitized.SearchToIdleTicks = (ushort)math.max(1, sanitized.SearchToIdleTicks);
            sanitized.GlobalQualityWeight = ResolveMesofaunaGlobalQualityWeight();
            _mesofaunaTuning[0] = sanitized;
            return true;
        }

        internal static bool TryGetMesofaunaTelemetrySnapshot(out MesofaunaTelemetryEntry entry)
        {
            EnsureInitialized();
            entry = default;
            if (!_mesofaunaTelemetryRing.IsCreated || _mesofaunaTelemetryRing.Length <= 0)
                return false;

            int index = _mesofaunaTelemetryCursor - 1;
            if (index < 0)
                index += _mesofaunaTelemetryRing.Length;
            entry = _mesofaunaTelemetryRing[index];
            return true;
        }

        internal static bool TryGetMesofaunaVisualSync(int slot, out MesofaunaVisualSyncDTO visual)
        {
            EnsureInitialized();
            visual = default;
            if (!_mesofaunaVisualSync.IsCreated ||
                slot < 0 ||
                slot >= _mesofaunaVisualSync.Length)
            {
                return false;
            }

            visual = _mesofaunaVisualSync[slot];
            return true;
        }

        internal static bool TryGetMesofaunaState(int slot, out MesofaunaStateDTO state)
        {
            EnsureInitialized();
            state = default;
            if (!_mesofaunaStates.IsCreated ||
                slot < 0 ||
                slot >= _mesofaunaStates.Length)
            {
                return false;
            }

            state = _mesofaunaStates[slot];
            return true;
        }

        internal static bool TryReloadMesofaunaSpeciesProfiles()
        {
            EnsureInitialized();
            return TryLoadMesofaunaSpeciesProfilesCsvCold();
        }

        internal static bool TryGetMesofaunaSpeciesProfileCount(out int count)
        {
            EnsureInitialized();
            count = 0;
            if (!_mesofaunaSpeciesProfileCount.IsCreated || _mesofaunaSpeciesProfileCount.Length <= 0)
                return false;

            count = math.max(0, _mesofaunaSpeciesProfileCount[0]);
            return true;
        }

        internal static int CopyMesofaunaDebugGizmos(
            Vector3[] origins,
            Vector3[] desiredVelocities,
            Vector3[] targetVectors,
            byte[] states,
            uint[] targetHashes,
            int maxCount)
        {
            if (origins == null ||
                desiredVelocities == null ||
                targetVectors == null ||
                states == null ||
                targetHashes == null)
            {
                return 0;
            }

            EnsureInitialized();
            if (!_activeSlots.IsCreated ||
                !_inputs.IsCreated ||
                !_mesofaunaStates.IsCreated ||
                !_mesofaunaVisualSync.IsCreated)
            {
                return 0;
            }

            int capacity = math.min(
                math.max(0, maxCount),
                math.min(origins.Length, math.min(desiredVelocities.Length, math.min(targetVectors.Length, math.min(states.Length, targetHashes.Length)))));
            int count = 0;
            for (int i = 0; i < _activeSlotCount && count < capacity; i++)
            {
                int slot = _activeSlots[i];
                if ((uint)slot >= (uint)_inputs.Length ||
                    (uint)slot >= (uint)_mesofaunaStates.Length ||
                    (uint)slot >= (uint)_mesofaunaVisualSync.Length)
                {
                    continue;
                }

                CognitionInput input = _inputs[slot];
                bool midPredator = (input.Flags & (int)CognitionInputFlags.Active) != 0 &&
                                   (input.Flags & (int)CognitionInputFlags.PredatorRole) != 0 &&
                                   (input.Flags & (int)CognitionInputFlags.IsApexPredator) == 0 &&
                                   (input.Flags & (int)CognitionInputFlags.UseAlphaLeviathanCognition) == 0;
                if (!midPredator)
                    continue;

                MesofaunaStateDTO state = _mesofaunaStates[slot];
                MesofaunaVisualSyncDTO visual = _mesofaunaVisualSync[slot];
                Vector3 origin = HectonFloatingOrigin.ToRuntimePosition(state.AUP_Position);
                origins[count] = origin;
                desiredVelocities[count] = new Vector3(visual.DesiredVelocity.x, visual.DesiredVelocity.y, visual.DesiredVelocity.z);
                if ((visual.TargetFlags & MesofaunaBehaviorConstants.VisualTargetFlagValid) != 0 && math.all(math.isfinite(visual.TargetAup)))
                {
                    targetVectors[count] = HectonFloatingOrigin.ToRuntimePosition(visual.TargetAup) - origin;
                }
                else
                {
                    float3 targetVector = (input.Flags & (int)CognitionInputFlags.HasPreyTarget) != 0 && MathGuard.IsFinite(input.PreyPosition)
                        ? input.PreyPosition - input.Position
                        : visual.DesiredVelocity;
                    targetVectors[count] = new Vector3(targetVector.x, targetVector.y, targetVector.z);
                }

                states[count] = state.CurrentState;
                targetHashes[count] = state.TargetHashID;
                count++;
            }

            return count;
        }

        internal static bool TryReloadApexCortexBehaviorOverrides()
        {
            EnsureInitialized();
            return TryLoadBehaviorOverridesCsvCold();
        }

        internal static int CopyApexCortexDebugGizmos(
            Vector3[] origins,
            Vector3[] targets,
            Vector3[] wallRepulsions,
            Vector3[] desiredVelocities,
            Vector3[] acousticMemory,
            int maxCount)
        {
            if (origins == null ||
                targets == null ||
                wallRepulsions == null ||
                desiredVelocities == null ||
                acousticMemory == null)
            {
                return 0;
            }

            EnsureInitialized();
            if (!_activeSlots.IsCreated || !_inputs.IsCreated || !_outputs.IsCreated || !_cores.IsCreated)
                return 0;

            int capacity = math.min(
                math.max(0, maxCount),
                math.min(origins.Length, math.min(targets.Length, math.min(wallRepulsions.Length, math.min(desiredVelocities.Length, acousticMemory.Length)))));
            int count = 0;
            for (int i = 0; i < _activeSlotCount && count < capacity; i++)
            {
                int slot = _activeSlots[i];
                if ((uint)slot >= (uint)Capacity)
                    continue;

                CognitionInput input = _inputs[slot];
                if ((input.Flags & (int)CognitionInputFlags.PredatorRole) == 0)
                    continue;

                CognitionCore core = _cores[slot];
                PackedCognitionOutput output = _outputs[slot];
                float3 target = (input.Flags & (int)CognitionInputFlags.HasPackTarget) != 0
                    ? input.PackTargetPosition
                    : (input.Flags & (int)CognitionInputFlags.HasPlayerTarget) != 0
                        ? input.PlayerPosition
                        : input.Position + (output.DesiredDirection * 8f);

                origins[count] = new Vector3(core.Position.x, core.Position.y, core.Position.z);
                targets[count] = new Vector3(target.x, target.y, target.z);
                float3 wall = _swarmAvoidances.IsCreated ? _swarmAvoidances[slot] : float3.zero;
                wallRepulsions[count] = new Vector3(wall.x, wall.y, wall.z);
                desiredVelocities[count] = new Vector3(output.DesiredDirection.x, output.DesiredDirection.y, output.DesiredDirection.z);
                acousticMemory[count] = ResolveLatestAcousticDebugPosition(slot, core.Position);
                count++;
            }

            return count;
        }

        private static Vector3 ResolveLatestAcousticDebugPosition(int slot, float3 fallback)
        {
            NativeArray<float4> acousticMemoryFloat4Bank = ResolveAcousticMemoryFloat4Bank();
            if (!acousticMemoryFloat4Bank.IsCreated || slot < 0 || slot >= Capacity)
                return new Vector3(fallback.x, fallback.y, fallback.z);

            int startIndex = slot * AcousticMemorySlotsPerCreature;
            float bestTime = float.NegativeInfinity;
            float3 bestPosition = fallback;
            for (int i = 0; i < AcousticMemorySlotsPerCreature; i++)
            {
                float4 packed = acousticMemoryFloat4Bank[startIndex + i];
                if (packed.w <= bestTime)
                    continue;

                bestTime = packed.w;
                bestPosition = packed.xyz;
            }

            return new Vector3(bestPosition.x, bestPosition.y, bestPosition.z);
        }

        private static void GenerateMockCognitionProfiles()
        {
            NativeArray<float4> apexCortexTuning = ResolveApexCortexTuning();
            if (!apexCortexTuning.IsCreated || apexCortexTuning.Length <= 0)
                return;

            if (math.lengthsq(apexCortexTuning[0]) <= DdaEpsilon)
            {
                ApexCortexTuningSnapshot snapshot = default;
                snapshot.HungerWeight = 0.92f;
                snapshot.FearWeight = 1.16f;
                snapshot.LightAversion = 0.76f;
                snapshot.AcousticMemoryDecay = 0.68f;
                WriteApexCortexTuningNoEnsure(in snapshot);
                return;
            }

            TryReadApexCortexTuningNoEnsure(out ApexCortexTuningSnapshot existing);
            UpsertSpeciesTuningNoEnsure(ApexCortexMockSpeciesId, BuildApexCortexSpeciesTuning(in existing));
        }

        private static bool TryReadApexCortexTuningNoEnsure(out ApexCortexTuningSnapshot snapshot)
        {
            snapshot = default;
            NativeArray<float4> apexCortexTuning = ResolveApexCortexTuning();
            if (!apexCortexTuning.IsCreated || apexCortexTuning.Length <= 0)
                return false;

            float4 raw = apexCortexTuning[0];
            snapshot.HungerWeight = math.max(0.1f, raw.x);
            snapshot.FearWeight = math.max(0.1f, raw.y);
            snapshot.LightAversion = math.max(0f, raw.z);
            snapshot.AcousticMemoryDecay = math.max(0.01f, raw.w);
            return true;
        }

        private static void WriteApexCortexTuningNoEnsure(in ApexCortexTuningSnapshot snapshot)
        {
            NativeArray<float4> apexCortexTuning = ResolveApexCortexTuning();
            if (!apexCortexTuning.IsCreated || apexCortexTuning.Length <= 0)
                return;

            float4 raw = new float4(
                math.max(0.1f, snapshot.HungerWeight),
                math.max(0.1f, snapshot.FearWeight),
                math.max(0f, snapshot.LightAversion),
                math.max(0.01f, snapshot.AcousticMemoryDecay));
            apexCortexTuning[0] = raw;
            ApexCortexTuningSnapshot sanitized = default;
            sanitized.HungerWeight = raw.x;
            sanitized.FearWeight = raw.y;
            sanitized.LightAversion = raw.z;
            sanitized.AcousticMemoryDecay = raw.w;
            UpsertSpeciesTuningNoEnsure(ApexCortexMockSpeciesId, BuildApexCortexSpeciesTuning(in sanitized));
        }

        private static SpeciesCognitionTuning BuildApexCortexSpeciesTuning(in ApexCortexTuningSnapshot snapshot)
        {
            return new SpeciesCognitionTuning(
                snapshot.HungerWeight,
                snapshot.FearWeight,
                snapshot.AcousticMemoryDecay,
                FaunaLightReactionMode.Aversion,
                20f,
                0.8f,
                1f,
                math.saturate(snapshot.LightAversion));
        }

        private static void UpsertSpeciesTuningNoEnsure(int speciesId, in SpeciesCognitionTuning tuning)
        {
            if (speciesId == 0 ||
                !_speciesTuningIds.IsCreated ||
                !_speciesTuningValues.IsCreated ||
                !_speciesTuningCount.IsCreated ||
                _speciesTuningCount.Length <= 0)
            {
                return;
            }

            int count = math.min(
                math.max(_speciesTuningCount[0], 0),
                math.min(_speciesTuningIds.Length, _speciesTuningValues.Length));
            for (int i = 0; i < count; i++)
            {
                if (_speciesTuningIds[i] != speciesId)
                    continue;

                _speciesTuningValues[i] = tuning;
                return;
            }

            if (count >= _speciesTuningIds.Length || count >= _speciesTuningValues.Length)
                return;

            _speciesTuningIds[count] = speciesId;
            _speciesTuningValues[count] = tuning;
            _speciesTuningCount[0] = count + 1;
        }

        private static bool TryLoadBehaviorOverridesCsvCold()
        {
            NativeArray<float4> apexCortexTuning = ResolveApexCortexTuning();
            if (!apexCortexTuning.IsCreated || apexCortexTuning.Length <= 0)
                return false;

            string path = ResolveBehaviorOverridesPathCold();
            if (string.IsNullOrEmpty(path))
                return false;

            string csv = File.ReadAllText(path);
            System.ReadOnlySpan<char> csvSpan = csv;
            return TryApplyBehaviorOverridesCsv(csvSpan);
        }

        private static string ResolveBehaviorOverridesPathCold()
        {
            DirectoryInfo dataDirectory = Directory.GetParent(Application.dataPath);
            string projectRoot = dataDirectory != null ? dataDirectory.FullName : Application.dataPath;
            string path = Path.Combine(projectRoot, ApexCortexBehaviorCsvName);
            if (File.Exists(path))
                return path;

            path = Path.Combine(projectRoot, "Assets", "StreamingAssets", ApexCortexBehaviorCsvName);
            if (File.Exists(path))
                return path;

            path = Path.Combine(projectRoot, "Data", "AI", ApexCortexBehaviorCsvName);
            return File.Exists(path) ? path : null;
        }

        private static bool TryApplyBehaviorOverridesCsv(System.ReadOnlySpan<char> csv)
        {
            if (!TryReadApexCortexTuningNoEnsure(out ApexCortexTuningSnapshot snapshot))
                return false;

            bool changed = false;
            int lineStart = 0;
            while (lineStart < csv.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < csv.Length && csv[lineEnd] != '\n' && csv[lineEnd] != '\r')
                    lineEnd++;

                System.ReadOnlySpan<char> line = TrimCsvToken(csv.Slice(lineStart, lineEnd - lineStart));
                if (TryReadCsvKeyValue(line, out System.ReadOnlySpan<char> key, out float value))
                {
                    if (KeyEquals(key, "HungerWeight"))
                    {
                        snapshot.HungerWeight = value;
                        changed = true;
                    }
                    else if (KeyEquals(key, "FearWeight"))
                    {
                        snapshot.FearWeight = value;
                        changed = true;
                    }
                    else if (KeyEquals(key, "LightAversion") || KeyEquals(key, "LightAversionWeight"))
                    {
                        snapshot.LightAversion = value;
                        changed = true;
                    }
                    else if (KeyEquals(key, "AcousticMemoryDecay") || KeyEquals(key, "AcousticTrackingWeight"))
                    {
                        snapshot.AcousticMemoryDecay = value;
                        changed = true;
                    }
                }

                lineStart = lineEnd + 1;
                while (lineStart < csv.Length && (csv[lineStart] == '\n' || csv[lineStart] == '\r'))
                    lineStart++;
            }

            if (!changed)
                return false;

            WriteApexCortexTuningNoEnsure(in snapshot);
            return true;
        }

        private static bool TryReadCsvKeyValue(
            System.ReadOnlySpan<char> line,
            out System.ReadOnlySpan<char> key,
            out float value)
        {
            key = default;
            value = 0f;
            if (line.Length <= 0 || line[0] == '#')
                return false;

            int comma = FindComma(line);
            if (comma <= 0 || comma >= line.Length - 1)
                return false;

            key = TrimCsvToken(line.Slice(0, comma));
            System.ReadOnlySpan<char> valueToken = TrimCsvToken(line.Slice(comma + 1));
            return key.Length > 0 && TryParseFloatInvariant(valueToken, out value);
        }

        private static int FindComma(System.ReadOnlySpan<char> line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == ',')
                    return i;
            }

            return -1;
        }

        private static System.ReadOnlySpan<char> TrimCsvToken(System.ReadOnlySpan<char> token)
        {
            int start = 0;
            int end = token.Length - 1;
            while (start <= end && char.IsWhiteSpace(token[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(token[end]))
                end--;
            if (end >= start && token[start] == '"' && token[end] == '"')
            {
                start++;
                end--;
            }

            return start <= end ? token.Slice(start, end - start + 1) : default;
        }

        private static bool KeyEquals(System.ReadOnlySpan<char> key, string token)
        {
            if (key.Length != token.Length)
                return false;

            for (int i = 0; i < key.Length; i++)
            {
                if (char.ToUpperInvariant(key[i]) != char.ToUpperInvariant(token[i]))
                    return false;
            }

            return true;
        }

        private static bool TryParseFloatInvariant(System.ReadOnlySpan<char> token, out float value)
        {
            value = 0f;
            token = TrimCsvToken(token);
            if (token.Length <= 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (token[index] == '-')
            {
                sign = -1f;
                index++;
            }
            else if (token[index] == '+')
            {
                index++;
            }

            double result = 0d;
            bool hasDigits = false;
            while (index < token.Length && token[index] >= '0' && token[index] <= '9')
            {
                result = (result * 10d) + (token[index] - '0');
                index++;
                hasDigits = true;
            }

            if (index < token.Length && token[index] == '.')
            {
                index++;
                double scale = 0.1d;
                while (index < token.Length && token[index] >= '0' && token[index] <= '9')
                {
                    result += (token[index] - '0') * scale;
                    scale *= 0.1d;
                    index++;
                    hasDigits = true;
                }
            }

            if (!hasDigits)
                return false;

            value = (float)(result * sign);
            return float.IsFinite(value);
        }

        private static unsafe bool TryLoadMesofaunaSpeciesProfilesCsvCold()
        {
            if (!_mesofaunaSpeciesProfiles.IsCreated ||
                !_mesofaunaSpeciesProfileCount.IsCreated ||
                !_mesofaunaCsvScratch.IsCreated ||
                _mesofaunaSpeciesProfileCount.Length <= 0)
            {
                return false;
            }

            NativeArray<MesofaunaSpeciesProfileDTO> profiles = _mesofaunaSpeciesProfiles.Resolve();
            if (!profiles.IsCreated)
                return false;

            _mesofaunaSpeciesProfileCount[0] = 0;
            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;

            string path = ResolveMesofaunaSpeciesProfilesPathCold();
            if (string.IsNullOrEmpty(path))
                return false;

            NativeArray<byte> scratch = _mesofaunaCsvScratch.Resolve();
            int byteCount = ReadMesofaunaSpeciesProfilesFileCold(path, scratch);
            if (byteCount <= 0)
                return false;

            void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
            System.ReadOnlySpan<byte> csvBytes = new System.ReadOnlySpan<byte>(source, byteCount);
            int profileCount = ParseMesofaunaSpeciesProfilesCsv(csvBytes, profiles);
            if (profileCount <= 0)
                return false;

            _mesofaunaSpeciesProfileCount[0] = profileCount;
            return true;
        }

        private static string ResolveMesofaunaSpeciesProfilesPathCold()
        {
            DirectoryInfo dataDirectory = Directory.GetParent(Application.dataPath);
            string projectRoot = dataDirectory != null ? dataDirectory.FullName : Application.dataPath;
            string path = Path.Combine(projectRoot, MesofaunaSpeciesProfilesCsvName);
            if (File.Exists(path))
                return path;

            path = Path.Combine(Application.streamingAssetsPath, MesofaunaSpeciesProfilesCsvName);
            if (File.Exists(path))
                return path;

            path = Path.Combine(projectRoot, "Data", "AI", MesofaunaSpeciesProfilesCsvName);
            return File.Exists(path) ? path : null;
        }

        private static unsafe int ReadMesofaunaSpeciesProfilesFileCold(string path, NativeArray<byte> scratch)
        {
            if (!scratch.IsCreated || scratch.Length <= 0)
                return 0;

            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    if (stream.Length <= 0L || stream.Length > scratch.Length)
                        return -1;

                    int length = (int)stream.Length;
                    void* destination = NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                    System.Span<byte> target = new System.Span<byte>(destination, length);
                    int totalRead = 0;
                    while (totalRead < length)
                    {
                        int read = stream.Read(target.Slice(totalRead));
                        if (read <= 0)
                            break;

                        totalRead += read;
                    }

                    return totalRead;
                }
            }
            catch (IOException)
            {
                return 0;
            }
            catch (System.UnauthorizedAccessException)
            {
                return 0;
            }
            catch (System.ArgumentException)
            {
                return 0;
            }
        }

        internal static int ParseMesofaunaSpeciesProfilesCsv(
            System.ReadOnlySpan<byte> csv,
            NativeArray<MesofaunaSpeciesProfileDTO> profiles)
        {
            if (!profiles.IsCreated || profiles.Length <= 0 || csv.Length <= 0)
                return 0;

            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;

            int profileCount = 0;
            int lineStart = 0;
            while (lineStart < csv.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < csv.Length && csv[lineEnd] != (byte)'\n' && csv[lineEnd] != (byte)'\r')
                    lineEnd++;

                System.ReadOnlySpan<byte> line = TrimCsvToken(csv.Slice(lineStart, lineEnd - lineStart));
                if (TryParseMesofaunaSpeciesProfileLine(line, out MesofaunaSpeciesProfileDTO profile))
                    profileCount += UpsertMesofaunaSpeciesProfile(profiles, profile);

                lineStart = lineEnd + 1;
                while (lineStart < csv.Length && (csv[lineStart] == (byte)'\n' || csv[lineStart] == (byte)'\r'))
                    lineStart++;
            }

            return math.min(profileCount, profiles.Length);
        }

        private static bool TryParseMesofaunaSpeciesProfileLine(
            System.ReadOnlySpan<byte> line,
            out MesofaunaSpeciesProfileDTO profile)
        {
            profile = default;
            line = TrimCsvToken(line);
            if (line.Length <= 0 || line[0] == (byte)'#')
                return false;

            int cursor = 0;
            if (!TryReadCsvByteField(line, ref cursor, out System.ReadOnlySpan<byte> speciesToken) ||
                !TryParseSpeciesHash(speciesToken, out uint speciesHash) ||
                speciesHash == 0u)
            {
                return false;
            }

            if (!TryReadCsvFloatField(line, ref cursor, out float speedMultiplier) ||
                !TryReadCsvFloatField(line, ref cursor, out float aggressionMultiplier))
            {
                return false;
            }

            float scentMultiplier = TryReadCsvFloatField(line, ref cursor, out float parsedScent) ? parsedScent : 1f;
            float visionMultiplier = TryReadCsvFloatField(line, ref cursor, out float parsedVision) ? parsedVision : 1f;
            float huntBias = TryReadCsvFloatField(line, ref cursor, out float parsedHunt) ? parsedHunt : 1f;

            profile = MesofaunaSpeciesProfileDTO.CreateDefault(speciesHash);
            profile.SpeedMultiplier = math.clamp(SanitizeCsvScalar(speedMultiplier), 0.1f, 4f);
            profile.AggressionMultiplier = math.clamp(SanitizeCsvScalar(aggressionMultiplier), 0.1f, 4f);
            profile.ScentSensitivityMultiplier = math.clamp(SanitizeCsvScalar(scentMultiplier), 0.05f, 4f);
            profile.VisionRadiusMultiplier = math.clamp(SanitizeCsvScalar(visionMultiplier), 0.25f, 3f);
            profile.HuntBias = math.clamp(SanitizeCsvScalar(huntBias), 0.1f, 4f);
            return true;
        }

        private static int UpsertMesofaunaSpeciesProfile(
            NativeArray<MesofaunaSpeciesProfileDTO> profiles,
            in MesofaunaSpeciesProfileDTO profile)
        {
            int start = (int)(profile.SpeciesHash % (uint)profiles.Length);
            for (int probe = 0; probe < profiles.Length; probe++)
            {
                int index = start + probe;
                if (index >= profiles.Length)
                    index -= profiles.Length;

                MesofaunaSpeciesProfileDTO existing = profiles[index];
                if (existing.SpeciesHash != 0u && existing.SpeciesHash != profile.SpeciesHash)
                    continue;

                profiles[index] = profile;
                return existing.SpeciesHash == 0u ? 1 : 0;
            }

            return 0;
        }

        private static bool TryReadCsvFloatField(System.ReadOnlySpan<byte> line, ref int cursor, out float value)
        {
            value = 0f;
            return TryReadCsvByteField(line, ref cursor, out System.ReadOnlySpan<byte> token) &&
                   TryParseFloatInvariant(token, out value);
        }

        private static bool TryReadCsvByteField(
            System.ReadOnlySpan<byte> line,
            ref int cursor,
            out System.ReadOnlySpan<byte> token)
        {
            token = default;
            while (cursor < line.Length && (line[cursor] == (byte)',' || line[cursor] == (byte)';' || IsWhitespace(line[cursor])))
                cursor++;

            if (cursor >= line.Length)
                return false;

            int start = cursor;
            bool quoted = line[cursor] == (byte)'"';
            if (quoted)
                cursor++;

            while (cursor < line.Length)
            {
                byte b = line[cursor];
                if (quoted)
                {
                    if (b == (byte)'"')
                    {
                        cursor++;
                        break;
                    }
                }
                else if (b == (byte)',' || b == (byte)';')
                {
                    break;
                }

                cursor++;
            }

            int end = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',' && line[cursor] != (byte)';')
                cursor++;
            if (cursor < line.Length)
                cursor++;

            token = TrimCsvToken(line.Slice(start, end - start));
            return token.Length > 0;
        }

        private static bool TryParseSpeciesHash(System.ReadOnlySpan<byte> token, out uint hash)
        {
            hash = 0u;
            token = TrimCsvToken(token);
            if (token.Length <= 0)
                return false;

            if (token.Length > 2 &&
                token[0] == (byte)'0' &&
                (token[1] == (byte)'x' || token[1] == (byte)'X'))
            {
                return TryParseHexUint(token.Slice(2), out hash);
            }

            if (TryParseUint(token, out hash))
                return true;

            hash = Fnv1aLower(token);
            return hash != 0u;
        }

        private static bool TryParseUint(System.ReadOnlySpan<byte> token, out uint value)
        {
            value = 0u;
            token = TrimCsvToken(token);
            if (token.Length <= 0)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                byte b = token[i];
                if (b < (byte)'0' || b > (byte)'9')
                    return false;

                uint next = (value * 10u) + (uint)(b - (byte)'0');
                value = next < value ? uint.MaxValue : next;
            }

            return true;
        }

        private static bool TryParseHexUint(System.ReadOnlySpan<byte> token, out uint value)
        {
            value = 0u;
            if (token.Length <= 0)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                byte b = token[i];
                uint digit;
                if (b >= (byte)'0' && b <= (byte)'9')
                    digit = (uint)(b - (byte)'0');
                else if (b >= (byte)'a' && b <= (byte)'f')
                    digit = (uint)(10 + b - (byte)'a');
                else if (b >= (byte)'A' && b <= (byte)'F')
                    digit = (uint)(10 + b - (byte)'A');
                else
                    return false;

                value = (value << 4) | digit;
            }

            return true;
        }

        private static bool TryParseFloatInvariant(System.ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            token = TrimCsvToken(token);
            if (token.Length <= 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (token[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (token[index] == (byte)'+')
            {
                index++;
            }

            double result = 0d;
            bool hasDigits = false;
            while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
            {
                result = (result * 10d) + (token[index] - (byte)'0');
                index++;
                hasDigits = true;
            }

            if (index < token.Length && token[index] == (byte)'.')
            {
                index++;
                double scale = 0.1d;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    result += (token[index] - (byte)'0') * scale;
                    scale *= 0.1d;
                    index++;
                    hasDigits = true;
                }
            }

            if (!hasDigits)
                return false;

            value = (float)(result * sign);
            return float.IsFinite(value);
        }

        private static System.ReadOnlySpan<byte> TrimCsvToken(System.ReadOnlySpan<byte> token)
        {
            int start = 0;
            int end = token.Length - 1;
            while (start <= end && IsWhitespace(token[start]))
                start++;
            while (end >= start && IsWhitespace(token[end]))
                end--;
            if (end >= start && token[start] == (byte)'"' && token[end] == (byte)'"')
            {
                start++;
                end--;
            }

            return start <= end ? token.Slice(start, end - start + 1) : default;
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static uint Fnv1aLower(System.ReadOnlySpan<byte> token)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < token.Length; i++)
                {
                    byte b = token[i];
                    if (b >= (byte)'A' && b <= (byte)'Z')
                        b = (byte)(b + 32);
                    hash = (hash ^ b) * 16777619u;
                }

                return hash == 0u ? 2166136261u : hash;
            }
        }

        private static float SanitizeCsvScalar(float value)
        {
            return float.IsFinite(value) && value > 0f ? value : 1f;
        }

        private static void ValidateAbiLayout()
        {
            if (UnsafeUtility.SizeOf<CognitionCore>() != CognitionCoreSizeBytes ||
                UnsafeUtility.SizeOf<CognitionMemoryEntry>() != CognitionMemoryEntrySizeBytes ||
                UnsafeUtility.SizeOf<AcousticMemoryEntry>() != AcousticMemoryEntrySizeBytes ||
                UnsafeUtility.SizeOf<CognitionControl>() != CognitionControlSizeBytes ||
                UnsafeUtility.SizeOf<CognitionInput>() != CognitionInputSizeBytes ||
                UnsafeUtility.SizeOf<CognitionOutput>() != CognitionOutputSizeBytes ||
                UnsafeUtility.SizeOf<PackedCognitionOutput>() != PackedCognitionOutputSizeBytes ||
                UnsafeUtility.SizeOf<PredatorCognitionDTO>() != PredatorCognitionDtoSizeBytes ||
                UnsafeUtility.SizeOf<SpeciesCognitionTuning>() != SpeciesCognitionTuningSizeBytes ||
                UnsafeUtility.SizeOf<MesofaunaStateDTO>() != MesofaunaStateDtoSizeBytes ||
                UnsafeUtility.SizeOf<MesofaunaTargetDTO>() != MesofaunaTargetDtoSizeBytes ||
                UnsafeUtility.SizeOf<MesofaunaVisualSyncDTO>() != MesofaunaVisualSyncDtoSizeBytes ||
                UnsafeUtility.SizeOf<MesofaunaTelemetryEntry>() != MesofaunaTelemetryEntrySizeBytes ||
                UnsafeUtility.SizeOf<MesofaunaTuningDTO>() != MesofaunaTuningDtoSizeBytes ||
                UnsafeUtility.SizeOf<MesofaunaSpeciesProfileDTO>() != MesofaunaSpeciesProfileDtoSizeBytes ||
                !MesofaunaBehaviorConstants.ValidateLayout())
            {
                FatalMemoryException.ThrowAbiLayoutMismatch();
            }
        }

        private static void EnsureRetinalVaultBuffers()
        {
            if (_retinalExposure.IsCreated &&
                _blindnessState.IsCreated &&
                _lastPublishedBlindnessState.IsCreated &&
                _retinalLightSources.IsCreated &&
                _retinalTelemetryRing.IsCreated)
            {
                return;
            }

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsAllocationLocked)
                return;

            _dataVault = vault;
            _retinalExposure = vault.GetBufferHandle<float>(
                BufferID.PredatorRetinalExposure,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _blindnessState = vault.GetBufferHandle<byte>(
                BufferID.PredatorRetinalBlindnessState,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _lastPublishedBlindnessState = vault.GetBufferHandle<byte>(
                BufferID.PredatorRetinalLastPublishedBlindnessState,
                Capacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _retinalLightSources = vault.GetBufferHandle<LightSourceData>(
                BufferID.PredatorRetinalLightSources,
                RetinalLightCapacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            _retinalTelemetryRing = vault.GetBufferHandle<RetinalTelemetryEntry>(
                BufferID.PredatorRetinalTelemetryRing,
                RetinalTelemetryCapacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
        }

        private static void EnsureAlphaLeviathanTelemetryVaultBuffer()
        {
            if (_alphaLeviathanTelemetryRing.IsCreated)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsAllocationLocked)
                return;

            _dataVault = vault;
            _alphaLeviathanTelemetryRing = vault.GetBufferHandle<AlphaLeviathanTelemetryEntry>(
                BufferID.AlphaLeviathanTelemetryRing,
                AlphaLeviathanTelemetryVaultCapacity,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
        }

        private static void ReleaseVaultHandle<T>(ref VaultArray<T> array) where T : struct
        {
            array = default;
        }

        private static bool PrepareEvaluationDueFlags()
        {
            bool hasDueEvaluations = false;
            bool lowTierRetina = ResolveRetinalLowCadenceMode();
            for (int i = 0; i < _activeSlotCount; i++)
            {
                int slot = _activeSlots[i];
                CognitionInput input = _inputs[slot];
                bool isActive = (input.Flags & (int)CognitionInputFlags.Active) != 0;
                if (!isActive)
                {
                    _evaluationDueFlags[slot] = 0;
                    continue;
                }

                float currentTime = math.max(0f, input.CurrentTime);
                bool predatorRole = (input.Flags & (int)CognitionInputFlags.PredatorRole) != 0;
                bool alphaLeviathan = predatorRole && (input.Flags & (int)CognitionInputFlags.UseAlphaLeviathanCognition) != 0;
                float interval = predatorRole
                    ? math.select(
                        math.select(PredatorUtilityEvaluationIntervalSeconds, RetinalLowTierEvaluationIntervalSeconds, lowTierRetina),
                        AlphaLeviathanSlowTickIntervalSeconds,
                        alphaLeviathan)
                    : ResolveEvaluationInterval(input.ImportanceScore);
                float previousInterval = math.max(_evaluationIntervals[slot], CenterEvaluationIntervalSeconds);
                float scheduledTime = _nextEvaluationTimes[slot];
                bool firstPredatorSchedule = predatorRole && scheduledTime <= DdaEpsilon;
                float staggerStep = math.select(PredatorUtilityEvaluationStaggerStepSeconds, AlphaLeviathanEvaluationStaggerStepSeconds, alphaLeviathan);
                float staggerOffset = (slot & 31) * staggerStep;
                scheduledTime = math.select(scheduledTime, currentTime + staggerOffset, firstPredatorSchedule);
                scheduledTime = math.min(scheduledTime, currentTime + interval);
                if (interval + DdaEpsilon < previousInterval)
                    scheduledTime = currentTime;

                bool due = currentTime + DdaEpsilon >= scheduledTime;
                _evaluationDueFlags[slot] = due ? (byte)1 : (byte)0;
                _evaluationIntervals[slot] = interval;
                _nextEvaluationTimes[slot] = due ? currentTime + interval : scheduledTime;
                hasDueEvaluations |= due;
            }

            return hasDueEvaluations;
        }

        private static bool ResolveRetinalLowCadenceMode()
        {
            return GlobalRegistry.ScalabilityTierProfileByte == 0 ||
                   SystemDispatcher.HomeostasisPressureLevel != 0 ||
                   SystemDispatcher.CurrentFrameUnscaledDeltaTime > RetinalFrameBudgetStressThresholdSeconds;
        }

        private static void ProcessSubmarineLightSignals(int frameId)
        {
            if (!_retinalLightSources.IsCreated)
                return;

            System.ReadOnlySpan<SubmarineLightsChangedSignal> lightSignals =
                SignalBus<SubmarineLightsChangedSignal>.GetFrameSnapshot();
            int startIndex = math.max(0, lightSignals.Length - RetinalLightSignalConsumeLimit);
            for (int i = startIndex; i < lightSignals.Length; i++)
            {
                SubmarineLightsChangedSignal signal = lightSignals[i];
                if (signal.Operation == SubmarineLightsChangedSignalOperations.ClearSource)
                {
                    RemoveRetinalLightSource(signal.SourceId);
                    continue;
                }

                bool powered = (signal.Flags & SubmarineLightsChangedSignalFlags.Powered) != 0;
                if (signal.Operation == SubmarineLightsChangedSignalOperations.Remove ||
                    !powered ||
                    (signal.Flags & SubmarineLightsChangedSignalFlags.BrownoutSuppressed) != 0 ||
                    !MathGuard.IsFinite(in signal.PositionAup) ||
                    !MathGuard.IsFinite(signal.RangeMeters) ||
                    !MathGuard.IsFinite(signal.Intensity) ||
                    !MathGuard.IsFinite(signal.SpotOuterCos) ||
                    signal.RangeMeters <= 0.1f ||
                    signal.Intensity <= DdaEpsilon)
                {
                    RemoveRetinalLightSource(signal.SourceId, signal.Slot);
                    continue;
                }

                UpsertRetinalLightSource(in signal, frameId);
            }

            CullStaleRetinalLights(frameId);
        }

        private static void UpsertRetinalLightSource(in SubmarineLightsChangedSignal signal, int frameId)
        {
            LightSourceData light = BuildRetinalLightSource(in signal, frameId);
            if (light.Intensity <= DdaEpsilon || light.RangeSq <= DdaEpsilon)
                return;

            for (int i = 0; i < _retinalLightCount; i++)
            {
                LightSourceData existing = _retinalLightSources[i];
                if (existing.SourceId != light.SourceId || existing.Slot != light.Slot)
                    continue;

                _retinalLightSources[i] = light;
                return;
            }

            if (_retinalLightCount < RetinalLightCapacity)
            {
                _retinalLightSources[_retinalLightCount] = light;
                _retinalLightCount++;
                return;
            }

            int weakestIndex = 0;
            float weakestScore = ComputeLightPriority(_retinalLightSources[0]);
            for (int i = 1; i < RetinalLightCapacity; i++)
            {
                float score = ComputeLightPriority(_retinalLightSources[i]);
                if (score >= weakestScore)
                    continue;

                weakestScore = score;
                weakestIndex = i;
            }

            if (ComputeLightPriority(light) > weakestScore)
                _retinalLightSources[weakestIndex] = light;
        }

        private static LightSourceData BuildRetinalLightSource(in SubmarineLightsChangedSignal signal, int frameId)
        {
            float range = MathGuard.IsFinite(signal.RangeMeters)
                ? math.clamp(signal.RangeMeters, RetinalMinLightRangeMeters, RetinalMaxLightRangeMeters)
                : RetinalMinLightRangeMeters;
            float intensity = MathGuard.IsFinite(signal.Intensity)
                ? math.clamp(signal.Intensity, 0f, RetinalMaxLightIntensity)
                : 0f;
            float spotOuterCos = MathGuard.IsFinite(signal.SpotOuterCos)
                ? math.clamp(signal.SpotOuterCos, -1f, 1f)
                : 0f;
            float3 forward = ResolveFiniteDirection(signal.Forward, new float3(0f, 0f, 1f));
            return new LightSourceData
            {
                PositionAup = signal.PositionAup.ToAlignedBlit(),
                Forward = forward,
                RangeMeters = range,
                RangeSq = range * range,
                Intensity = intensity,
                SpotOuterCos = spotOuterCos,
                SourceId = signal.SourceId,
                LastFrame = unchecked((uint)math.max(0, frameId)),
                Slot = signal.Slot,
                Flags = signal.Flags
            };
        }

        private static float3 ResolveFiniteDirection(float3 direction, float3 fallback)
        {
            float lengthSq = math.lengthsq(direction);
            if (!MathGuard.IsFinite(direction) || lengthSq <= DdaEpsilon)
                direction = fallback;

            lengthSq = math.lengthsq(direction);
            if (!MathGuard.IsFinite(direction) || lengthSq <= DdaEpsilon)
                return new float3(0f, 0f, 1f);

            return direction * math.rsqrt(math.max(lengthSq, MathSafetyEpsilon));
        }

        private static float ComputeLightPriority(in LightSourceData light)
        {
            return math.max(0f, light.Intensity) * math.max(0f, light.RangeSq);
        }

        private static float3 ResolveTelemetryRuntimePosition(in AbsoluteUniversePositionBlit128 positionAup, double3 floatingOriginOffset)
        {
            double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            double3 absolutePosition = new double3(
                (positionAup.GridX * cellSize) + positionAup.Local.x,
                (positionAup.GridY * cellSize) + positionAup.Local.y,
                (positionAup.GridZ * cellSize) + positionAup.Local.z);
            double3 runtimePosition = absolutePosition - floatingOriginOffset;
            return new float3((float)runtimePosition.x, (float)runtimePosition.y, (float)runtimePosition.z);
        }

        private static void RemoveRetinalLightSource(uint sourceId)
        {
            for (int i = _retinalLightCount - 1; i >= 0; i--)
            {
                if (_retinalLightSources[i].SourceId == sourceId)
                    RemoveRetinalLightAt(i);
            }
        }

        private static void RemoveRetinalLightSource(uint sourceId, ushort slot)
        {
            for (int i = 0; i < _retinalLightCount; i++)
            {
                LightSourceData light = _retinalLightSources[i];
                if (light.SourceId != sourceId || light.Slot != slot)
                    continue;

                RemoveRetinalLightAt(i);
                return;
            }
        }

        private static void CullStaleRetinalLights(int frameId)
        {
            uint currentFrame = unchecked((uint)math.max(0, frameId));
            for (int i = _retinalLightCount - 1; i >= 0; i--)
            {
                uint lastFrame = _retinalLightSources[i].LastFrame;
                if (unchecked(currentFrame - lastFrame) > RetinalLightStaleFrameWindow)
                    RemoveRetinalLightAt(i);
            }
        }

        private static void RemoveRetinalLightAt(int index)
        {
            int lastIndex = _retinalLightCount - 1;
            if (index < 0 || index > lastIndex)
                return;

            _retinalLightSources[index] = index == lastIndex ? default : _retinalLightSources[lastIndex];
            _retinalLightSources[lastIndex] = default;
            _retinalLightCount = lastIndex;
        }

        private static void UpdateRetinalPostEvaluationTelemetry(int frameId)
        {
            if (!_activeSlots.IsCreated ||
                !_retinalExposure.IsCreated ||
                !_blindnessState.IsCreated ||
                !_lastPublishedBlindnessState.IsCreated ||
                !_retinalTelemetryRing.IsCreated)
                return;

            int totalBlind = 0;
            float maxExposure = 0f;
            float3 hottestPosition = float3.zero;
            double3 telemetryOriginOffset = _activeSlotCount > 0 ? _inputs[_activeSlots[0]].FloatingOriginOffset : double3.zero;
            uint hottestSource = 0u;
            bool foundFault = false;
            for (int i = 0; i < _activeSlotCount; i++)
            {
                int slot = _activeSlots[i];
                float exposure = _retinalExposure[slot];
                if (!float.IsFinite(exposure))
                {
                    foundFault = true;
                    exposure = 0f;
                    _retinalExposure[slot] = 0f;
                    _blindnessState[slot] = 0;
                }

                byte blind = _blindnessState[slot];
                if (blind != 0)
                    totalBlind++;

                if (exposure > maxExposure)
                    maxExposure = exposure;

                byte prior = _lastPublishedBlindnessState[slot];
                if (prior != blind)
                {
                    PublishFaunaBlindStateSignal(slot, frameId, blind != 0);
                    _lastPublishedBlindnessState[slot] = blind;
                }
            }

            if (_retinalLightCount > 0)
            {
                LightSourceData strongest = _retinalLightSources[0];
                float strongestScore = ComputeLightPriority(strongest);
                for (int i = 1; i < _retinalLightCount; i++)
                {
                    float score = ComputeLightPriority(_retinalLightSources[i]);
                    if (score <= strongestScore)
                        continue;

                    strongest = _retinalLightSources[i];
                    strongestScore = score;
                }

                hottestPosition = ResolveTelemetryRuntimePosition(in strongest.PositionAup, telemetryOriginOffset);
                hottestSource = strongest.SourceId;
            }

            RetinalTelemetryEntry entry = default;
            entry.Frame = unchecked((uint)math.max(0, frameId));
            entry.TotalBlindPredators = (ushort)math.min(totalBlind, ushort.MaxValue);
            entry.ActiveLightCount = (byte)math.min(_retinalLightCount, byte.MaxValue);
            entry.Flags = (byte)(foundFault ? 1 : 0);
            entry.MaxExposure = maxExposure;
            entry.HottestLightPosition = hottestPosition;
            entry.SourceId = hottestSource;
            _retinalTelemetryRing[_retinalTelemetryCursor] = entry;
            _retinalTelemetryCursor = (_retinalTelemetryCursor + 1) % RetinalTelemetryCapacity;
            _totalBlindPredators = totalBlind;

            if (foundFault)
                DumpRetinalBlackBoxCold(frameId);

            if (_lastTelemetryBlindPredatorCount != totalBlind || (frameId & 31) == 0)
            {
                _lastTelemetryBlindPredatorCount = totalBlind;
                GlobalTelemetryBus.PublishPerformanceWarning(
                    RetinalBlindPredatorsTelemetryHash,
                    RetinalTelemetryContextHash,
                    totalBlind);
            }
        }

        private static void PublishFaunaBlindStateSignal(int slot, int frameId, bool blind)
        {
            if (!IsValidSlot(slot))
                return;

            CognitionCore core = _cores[slot];
            AbsoluteUniversePosition signalPosition = ResolveBlindSignalAup(slot, in core);
            GlobalSignals.Publish(new FaunaStateChangedSignal
            {
                PositionAup = signalPosition,
                SpeciesHash = unchecked((uint)core.SpeciesId),
                StateFlags = core.StateFlags,
                Frame = unchecked((uint)math.max(0, frameId)),
                Slot = (ushort)math.clamp(slot, 0, ushort.MaxValue),
                StateKind = FaunaStateChangedSignalKinds.Blind,
                Flags = blind ? FaunaStateChangedSignalFlags.StateActive : (byte)0
            });
        }

        private static AbsoluteUniversePosition ResolveBlindSignalAup(int slot, in CognitionCore core)
        {
            float3 runtimePosition = core.Position;
            double3 floatingOriginOffset = double3.zero;
            if (_inputs.IsCreated && slot >= 0 && slot < _inputs.Length)
            {
                CognitionInput input = _inputs[slot];
                if (MathGuard.IsFinite(input.Position))
                    runtimePosition = input.Position;
                if (math.all(math.isfinite(input.FloatingOriginOffset)))
                    floatingOriginOffset = input.FloatingOriginOffset;
            }

            if (!MathGuard.IsFinite(runtimePosition))
                runtimePosition = float3.zero;

            double3 absolutePosition = new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z) + floatingOriginOffset;
            if (!math.all(math.isfinite(absolutePosition)))
                absolutePosition = double3.zero;

            return AbsoluteUniversePosition.FromAbsolutePosition(absolutePosition);
        }

        private static void DumpRetinalBlackBoxCold(int frameId)
        {
            if (_retinalFaultDumped || !_retinalTelemetryRing.IsCreated)
                return;

            _retinalFaultDumped = true;
            try
            {
                DirectoryInfo dataDirectory = Directory.GetParent(Application.dataPath);
                string projectRoot = dataDirectory != null ? dataDirectory.FullName : Application.dataPath;
                string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(logDirectory);
                WriteRetinalBlackBoxFile(Path.Combine(logDirectory, "Dump_FAUNA_RETINAL_ADAPTATION.bin"), frameId);
                WriteRetinalBlackBoxFile(Path.Combine(logDirectory, "Dump_FAUNA_RETINAL_ADAPTATION.h8dump"), frameId);
            }
            catch (System.Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    RetinalDumpFailureTelemetryHash,
                    RetinalTelemetryContextHash,
                    frameId);
            }
        }

        private static void WriteRetinalBlackBoxFile(string dumpPath, int frameId)
        {
            using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(frameId);
                writer.Write(_retinalTelemetryCursor);
                writer.Write(_totalBlindPredators);
                writer.Write(_retinalLightCount);
                for (int i = 0; i < RetinalTelemetryCapacity; i++)
                {
                    RetinalTelemetryEntry entry = _retinalTelemetryRing[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.TotalBlindPredators);
                    writer.Write(entry.ActiveLightCount);
                    writer.Write(entry.Flags);
                    writer.Write(entry.MaxExposure);
                    writer.Write(entry.HottestLightPosition.x);
                    writer.Write(entry.HottestLightPosition.y);
                    writer.Write(entry.HottestLightPosition.z);
                    writer.Write(entry.SourceId);
                }
            }
        }

        private static void UpdateAlphaLeviathanPostEvaluationTelemetry(int frameId)
        {
            EnsureAlphaLeviathanTelemetryVaultBuffer();
            if (!_activeSlots.IsCreated ||
                !_alphaLeviathanTelemetryRing.IsCreated ||
                !_stalkingPhases.IsCreated ||
                !_outputs.IsCreated)
            {
                return;
            }

            int activeAlphaCount = 0;
            byte lastPhase = AlphaLeviathanPhase.Hidden;
            uint lastStateHash = 0u;
            bool foundFault = false;
            for (int i = 0; i < _activeSlotCount; i++)
            {
                int slot = _activeSlots[i];
                CognitionInput input = _inputs[slot];
                bool isAlpha = (input.Flags & (int)CognitionInputFlags.Active) != 0 &&
                               (input.Flags & (int)CognitionInputFlags.PredatorRole) != 0 &&
                               (input.Flags & (int)CognitionInputFlags.UseAlphaLeviathanCognition) != 0;
                if (!isAlpha)
                    continue;

                activeAlphaCount++;
                CognitionCore core = _cores[slot];
                PackedCognitionOutput output = _outputs[slot];
                byte phase = _stalkingPhases[slot];
                byte flags = 0;
                bool highTierSmoothSteering = (input.Flags & (int)CognitionInputFlags.HighTierSmoothSteering) != 0;
                bool hasPlayerTarget = (input.Flags & (int)CognitionInputFlags.HasPlayerTarget) != 0;
                if (!highTierSmoothSteering)
                    flags |= AlphaLeviathanTelemetryFlags.LowTierRadialFallback;
                if ((output.OutputFlags & (uint)CognitionOutputFlags.EmitThreatPulse) != 0u)
                    flags |= AlphaLeviathanTelemetryFlags.RoarEmitted;

                float3 playerPosition = hasPlayerTarget ? input.PlayerPosition : core.Position;
                if (!hasPlayerTarget)
                {
                    flags |= AlphaLeviathanTelemetryNoPlayerTarget;
                }
                else
                {
                    float3 awayFromPlayer = ResolveAlphaTelemetryDirection(core.Position - playerPosition, new float3(0f, 0f, 1f));
                    float3 playerForward = ResolveAlphaTelemetryDirection(input.PlayerForward, -awayFromPlayer);
                    if (math.dot(playerForward, awayFromPlayer) >= AlphaPlayerGazeDotThreshold)
                        flags |= AlphaLeviathanTelemetryFlags.PlayerGazeBreak;
                    if (highTierSmoothSteering && phase == AlphaLeviathanPhase.Hidden)
                        flags |= AlphaLeviathanTelemetryFlags.SdfDiveRequested;
                }

                float distanceSq = hasPlayerTarget ? math.lengthsq(playerPosition - core.Position) : 0f;
                float distanceMeters = distanceSq > DdaEpsilon ? distanceSq * math.rsqrt(math.max(distanceSq, MathSafetyEpsilon)) : 0f;
                float fogRingDistance = math.max(
                    AlphaFalseChargeVeerDistanceMeters + 5f,
                    math.max(input.FogEndDistanceMeters, AlphaFogFallbackEndMeters) - AlphaFogSilhouetteOffsetMeters);

                bool invalid = !MathGuard.IsFinite(core.Position) ||
                               !MathGuard.IsFinite(playerPosition) ||
                               !MathGuard.IsFinite(output.DesiredDirection) ||
                               !float.IsFinite(distanceMeters) ||
                               !float.IsFinite(fogRingDistance);
                if (invalid)
                {
                    foundFault = true;
                    flags |= AlphaLeviathanTelemetryFlags.Fault;
                    distanceMeters = 0f;
                    fogRingDistance = 0f;
                    playerPosition = float3.zero;
                }

                uint stateHash = BuildAlphaLeviathanTelemetryHash(
                    slot,
                    phase,
                    flags,
                    _chosenStates.IsCreated ? _chosenStates[slot] : (byte)0);
                AlphaLeviathanTelemetryEntry entry = default;
                entry.Frame = unchecked((uint)math.max(0, frameId));
                entry.Slot = (ushort)math.clamp(slot, 0, ushort.MaxValue);
                entry.Phase = phase;
                entry.Flags = flags;
                entry.DistanceToPlayerMeters = distanceMeters;
                entry.FogRingDistanceMeters = fogRingDistance;
                entry.Position = invalid ? float3.zero : core.Position;
                entry.PlayerPosition = playerPosition;
                entry.DesiredDirection = invalid ? float3.zero : output.DesiredDirection;
                entry.StateHash = stateHash;
                int telemetryFrame = (frameId < 0 ? 0 : frameId) % RetinalTelemetryCapacity;
                int telemetrySlot = math.min(activeAlphaCount - 1, AlphaLeviathanTelemetrySlotCapacity - 1);
                int telemetryIndex = (telemetryFrame * AlphaLeviathanTelemetrySlotCapacity) + telemetrySlot;
                bool oscillating = IsAlphaPhaseOscillating(telemetryFrame, entry.Slot, phase);
                if (oscillating)
                {
                    foundFault = true;
                    flags |= AlphaLeviathanTelemetryFlags.Fault;
                    _stalkingPhases[slot] = AlphaLeviathanPhase.Hidden;
                    _stalkingPhaseStartTimes[slot] = 0f;
                    entry.Phase = AlphaLeviathanPhase.Hidden;
                    entry.Flags = flags;
                    entry.DesiredDirection = float3.zero;
                    entry.StateHash = BuildAlphaLeviathanTelemetryHash(
                        slot,
                        entry.Phase,
                        flags,
                        _chosenStates.IsCreated ? _chosenStates[slot] : (byte)0);
                }

                if ((uint)telemetryIndex < (uint)_alphaLeviathanTelemetryRing.Length)
                    _alphaLeviathanTelemetryRing[telemetryIndex] = entry;

                _alphaLeviathanTelemetryCursor = telemetryFrame;
                lastPhase = entry.Phase;
                lastStateHash = entry.StateHash;
            }

            _activeAlphaLeviathanTelemetryCount = activeAlphaCount;
            if (foundFault)
                DumpAlphaLeviathanBlackBoxCold(frameId);

            if (activeAlphaCount > 0 && (frameId & 31) == 0)
                GlobalTelemetryBus.PublishModTelemetry(AlphaLeviathanPhaseTelemetryHash, lastStateHash, lastPhase);
        }

        private static bool IsAlphaPhaseOscillating(int telemetryFrame, ushort slot, byte currentPhase)
        {
            if (!_alphaLeviathanTelemetryRing.IsCreated)
                return false;

            bool foundPrevious = false;
            byte previousPhase = AlphaLeviathanPhase.Hidden;
            int lookback = math.min(AlphaLeviathanOscillationLookbackFrames, RetinalTelemetryCapacity - 1);
            for (int frameOffset = 1; frameOffset <= lookback; frameOffset++)
            {
                int frame = telemetryFrame - frameOffset;
                if (frame < 0)
                    frame += RetinalTelemetryCapacity;

                int baseIndex = frame * AlphaLeviathanTelemetrySlotCapacity;
                for (int slotIndex = 0; slotIndex < AlphaLeviathanTelemetrySlotCapacity; slotIndex++)
                {
                    int telemetryIndex = baseIndex + slotIndex;
                    if ((uint)telemetryIndex >= (uint)_alphaLeviathanTelemetryRing.Length)
                        continue;

                    AlphaLeviathanTelemetryEntry entry = _alphaLeviathanTelemetryRing[telemetryIndex];
                    if (entry.Slot != slot)
                        continue;

                    if (!foundPrevious)
                    {
                        previousPhase = entry.Phase;
                        foundPrevious = true;
                        continue;
                    }

                    return previousPhase != currentPhase && entry.Phase == currentPhase;
                }
            }

            return false;
        }

        private static uint BuildAlphaLeviathanTelemetryHash(int slot, byte phase, byte flags, byte state)
        {
            uint hash = AlphaLeviathanTelemetryContextHash;
            hash ^= (uint)slot * 0x9E3779B9u;
            hash = (hash << 5) | (hash >> 27);
            hash ^= (uint)phase * 0x85EBCA6Bu;
            hash = (hash << 7) | (hash >> 25);
            hash ^= (uint)flags * 0xC2B2AE35u;
            hash ^= (uint)state * 0x27D4EB2Du;
            return hash == 0u ? AlphaLeviathanPhaseTelemetryHash : hash;
        }

        private static float3 ResolveAlphaTelemetryDirection(float3 direction, float3 fallback)
        {
            if (!MathGuard.IsFinite(direction) || math.lengthsq(direction) <= DdaEpsilon)
                direction = fallback;

            float lengthSq = math.lengthsq(direction);
            if (!MathGuard.IsFinite(direction) || lengthSq <= DdaEpsilon)
                return float3.zero;

            return direction * math.rsqrt(math.max(lengthSq, MathSafetyEpsilon));
        }

        private static void DumpAlphaLeviathanBlackBoxCold(int frameId)
        {
            if (_alphaLeviathanFaultDumped || !_alphaLeviathanTelemetryRing.IsCreated)
                return;

            _alphaLeviathanFaultDumped = true;
            try
            {
                DirectoryInfo dataDirectory = Directory.GetParent(Application.dataPath);
                string projectRoot = dataDirectory != null ? dataDirectory.FullName : Application.dataPath;
                string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(logDirectory);
                WriteAlphaLeviathanBlackBoxFile(Path.Combine(logDirectory, "Dump_LEVIATHAN_CORTEX.bin"), frameId);
                WriteAlphaLeviathanBlackBoxFile(Path.Combine(logDirectory, "Dump_LEVIATHAN_CORTEX.h8dump"), frameId);
            }
            catch (System.Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    AlphaLeviathanDumpFailureTelemetryHash,
                    AlphaLeviathanTelemetryContextHash,
                    frameId);
            }
        }

        private static void WriteAlphaLeviathanBlackBoxFile(string dumpPath, int frameId)
        {
            using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(frameId);
                writer.Write(_alphaLeviathanTelemetryCursor);
                writer.Write(_activeAlphaLeviathanTelemetryCount);
                writer.Write(RetinalTelemetryCapacity);
                writer.Write(AlphaLeviathanTelemetrySlotCapacity);
                int dumpCount = math.min(_alphaLeviathanTelemetryRing.Length, AlphaLeviathanTelemetryVaultCapacity);
                writer.Write(dumpCount);
                for (int i = 0; i < dumpCount; i++)
                {
                    AlphaLeviathanTelemetryEntry entry = _alphaLeviathanTelemetryRing[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.Slot);
                    writer.Write(entry.Phase);
                    writer.Write(entry.Flags);
                    writer.Write(entry.DistanceToPlayerMeters);
                    writer.Write(entry.FogRingDistanceMeters);
                    writer.Write(entry.Position.x);
                    writer.Write(entry.Position.y);
                    writer.Write(entry.Position.z);
                    writer.Write(entry.PlayerPosition.x);
                    writer.Write(entry.PlayerPosition.y);
                    writer.Write(entry.PlayerPosition.z);
                    writer.Write(entry.DesiredDirection.x);
                    writer.Write(entry.DesiredDirection.y);
                    writer.Write(entry.DesiredDirection.z);
                    writer.Write(entry.StateHash);
                }
            }
        }

        private static void UpdateMesofaunaPostEvaluationTelemetry(int frameId)
        {
            if (!_activeSlots.IsCreated ||
                !_mesofaunaStates.IsCreated ||
                !_mesofaunaVisualSync.IsCreated ||
                !_mesofaunaTelemetryRing.IsCreated ||
                !_inputs.IsCreated)
            {
                return;
            }

            int activePredators = 0;
            int huntPredators = 0;
            int fleePredators = 0;
            int nonFiniteFallbacks = 0;
            uint stateHash = MesofaunaBehaviorConstants.TelemetryContextHash;
            uint targetHash = 0u;
            double3 probeAup = double3.zero;
            bool foundFault = false;
            bool overBudget = false;
            for (int i = 0; i < _activeSlotCount; i++)
            {
                int slot = _activeSlots[i];
                if ((uint)slot >= (uint)_inputs.Length ||
                    (uint)slot >= (uint)_mesofaunaStates.Length ||
                    (uint)slot >= (uint)_mesofaunaVisualSync.Length)
                {
                    continue;
                }

                CognitionInput input = _inputs[slot];
                bool midPredator = (input.Flags & (int)CognitionInputFlags.Active) != 0 &&
                                   (input.Flags & (int)CognitionInputFlags.PredatorRole) != 0 &&
                                   (input.Flags & (int)CognitionInputFlags.IsApexPredator) == 0 &&
                                   (input.Flags & (int)CognitionInputFlags.UseAlphaLeviathanCognition) == 0;
                if (!midPredator)
                    continue;

                activePredators++;
                MesofaunaStateDTO state = _mesofaunaStates[slot];
                MesofaunaVisualSyncDTO visual = _mesofaunaVisualSync[slot];
                if (state.CurrentState == MesofaunaBehaviorConstants.StateHunt)
                    huntPredators++;
                if (state.CurrentState == MesofaunaBehaviorConstants.StateFlee)
                    fleePredators++;

                bool targetAupFinite = (visual.TargetFlags & MesofaunaBehaviorConstants.VisualTargetFlagValid) == 0 || math.all(math.isfinite(visual.TargetAup));
                if (!math.all(math.isfinite(state.AUP_Position)) ||
                    !MathGuard.IsFinite(visual.DesiredVelocity) ||
                    !float.IsFinite(visual.SpeedScalar) ||
                    !float.IsFinite(visual.TargetDistanceMeters) ||
                    !targetAupFinite)
                {
                    foundFault = true;
                    nonFiniteFallbacks++;
                    ResetMesofaunaSlot(slot, input.Position, input.SpeciesId, MesofaunaBehaviorConstants.StateSearch);
                    continue;
                }

                stateHash ^= BuildMesofaunaTelemetryHash(slot, state.CurrentState, visual.Flags);
                stateHash = (stateHash << 5) | (stateHash >> 27);
                targetHash ^= state.TargetHashID;
                probeAup = (visual.TargetFlags & MesofaunaBehaviorConstants.VisualTargetFlagValid) != 0 ? visual.TargetAup : state.AUP_Position;
            }

            MesofaunaTelemetryEntry entry = default;
            entry.Frame = unchecked((uint)math.max(0, frameId));
            entry.ActivePredators = (ushort)math.min(activePredators, ushort.MaxValue);
            entry.HuntingPredators = (ushort)math.min(huntPredators, ushort.MaxValue);
            entry.AvgSpatialHashQueryMicroseconds = activePredators > 0
                ? math.max(0.01f, _mesofaunaLastChainMicroseconds / math.max(1, activePredators) * 0.18f)
                : 0f;
            entry.FsmMicroseconds = activePredators > 0
                ? math.max(0.01f, _mesofaunaLastChainMicroseconds / math.max(1, activePredators) * 0.32f)
                : 0f;
            overBudget = _mesofaunaLastChainMicroseconds > 1000f;
            entry.GlobalQualityWeight = _mesofaunaLastQualityWeight;
            entry.SliceModulo = (byte)math.clamp(_mesofaunaLastSliceModulo, 1, byte.MaxValue);
            byte telemetryFlags = 0;
            if (foundFault)
                telemetryFlags |= MesofaunaBehaviorConstants.TelemetryFlagFault;
            if (overBudget)
                telemetryFlags |= MesofaunaBehaviorConstants.TelemetryFlagOverBudget;
            entry.Flags = telemetryFlags;
            entry.NonFiniteFallbackCount = (ushort)math.min(nonFiniteFallbacks, ushort.MaxValue);
            entry.StateHash = stateHash == 0u ? MesofaunaBehaviorConstants.TelemetryContextHash : stateHash;
            entry.TargetHash = targetHash;
            entry.ProbeAup = probeAup;
            entry.DumpReasonHash = foundFault
                ? MesofaunaBehaviorConstants.DumpReasonFaultHash
                : overBudget ? MesofaunaBehaviorConstants.DumpReasonOverBudgetHash : 0u;
            entry.FleeingPredators = (ushort)math.min(fleePredators, ushort.MaxValue);
            _mesofaunaTelemetryRing[_mesofaunaTelemetryCursor] = entry;
            _mesofaunaTelemetryCursor = (_mesofaunaTelemetryCursor + 1) % MesofaunaBehaviorConstants.TelemetryCapacity;
            _mesofaunaLastActiveCount = activePredators;
            _mesofaunaLastHuntCount = huntPredators;
            _mesofaunaLastNonFiniteFallbackCount = nonFiniteFallbacks;

            if (foundFault || overBudget)
                DumpMesofaunaBlackBoxCold(frameId);

            if (activePredators > 0 && (frameId & 31) == 0)
                GlobalTelemetryBus.PublishModTelemetry(MesofaunaBehaviorConstants.TelemetryContextHash, entry.StateHash, entry.SliceModulo);
        }

        private static uint BuildMesofaunaTelemetryHash(int slot, byte state, ushort flags)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)slot) * 16777619u;
                hash = (hash ^ state) * 16777619u;
                hash = (hash ^ flags) * 16777619u;
                return hash;
            }
        }

        private static void DumpMesofaunaBlackBoxCold(int frameId)
        {
            if (_mesofaunaFaultDumped || !_mesofaunaTelemetryRing.IsCreated)
                return;

            _mesofaunaFaultDumped = true;
            try
            {
                DirectoryInfo dataDirectory = Directory.GetParent(Application.dataPath);
                string projectRoot = dataDirectory != null ? dataDirectory.FullName : Application.dataPath;
                string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(logDirectory);
                WriteMesofaunaBlackBoxFile(Path.Combine(logDirectory, "Dump_MESOFAUNA_DIRECTOR.bin"), frameId);
                WriteMesofaunaBlackBoxFile(Path.Combine(logDirectory, "Dump_MESOFAUNA_DIRECTOR.h8dump"), frameId);
            }
            catch (System.Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    MesofaunaBehaviorConstants.DumpFailureTelemetryHash,
                    MesofaunaBehaviorConstants.TelemetryContextHash,
                    frameId);
            }
        }

        private static void WriteMesofaunaBlackBoxFile(string dumpPath, int frameId)
        {
            using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(frameId);
                writer.Write(_mesofaunaTelemetryCursor);
                writer.Write(_mesofaunaLastActiveCount);
                writer.Write(_mesofaunaLastHuntCount);
                writer.Write(_mesofaunaLastNonFiniteFallbackCount);
                writer.Write(MesofaunaBehaviorConstants.TelemetryCapacity);
                writer.Write(MesofaunaBehaviorConstants.TelemetryEntrySizeBytes);
                for (int i = 0; i < MesofaunaBehaviorConstants.TelemetryCapacity; i++)
                {
                    MesofaunaTelemetryEntry entry = _mesofaunaTelemetryRing[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.ActivePredators);
                    writer.Write(entry.HuntingPredators);
                    writer.Write(entry.AvgSpatialHashQueryMicroseconds);
                    writer.Write(entry.FsmMicroseconds);
                    writer.Write(entry.GlobalQualityWeight);
                    writer.Write(entry.SliceModulo);
                    writer.Write(entry.Flags);
                    writer.Write(entry.NonFiniteFallbackCount);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.TargetHash);
                    writer.Write(entry.ProbeAup.x);
                    writer.Write(entry.ProbeAup.y);
                    writer.Write(entry.ProbeAup.z);
                    writer.Write(entry.DumpReasonHash);
                    writer.Write(entry.FleeingPredators);
                    writer.Write(entry.Reserved0);
                }
            }
        }

        private static void RefreshThreatVoxelSnapshot(int frameId)
        {
            if (_lastThreatVoxelBindFrame == frameId)
                return;

            _lastThreatVoxelBindFrame = frameId;
            HectonMapMagicVegetationBridge bridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (bridge != null &&
                bridge.TryGetEcosystemThreatVoxelPayload(out NativeArray<byte> threatVoxels, out Vector3Int gridDimensions, out Vector3 gridOrigin, out Vector3 voxelCellSize))
            {
                _threatVoxelGrid = threatVoxels;
                _threatVoxelDimensions = new int3(gridDimensions.x, gridDimensions.y, gridDimensions.z);
                _threatVoxelOrigin = new float3(gridOrigin.x, gridOrigin.y, gridOrigin.z);
                _threatVoxelCellSize = new float3(voxelCellSize.x, voxelCellSize.y, voxelCellSize.z);
                _threatVoxelSolidThreshold = SolidThreatVoxel;
                _threatVoxelUsesSignedDistanceEncoding = false;
                return;
            }

            HectonCaveVoxelLightingVolume caveLightingVolume = HectonCaveVoxelLightingVolume.ActiveRuntimeInstance;
            if (caveLightingVolume != null &&
                caveLightingVolume.TryGetPublishedSignedDistanceVoxelPayload(out NativeArray<byte> signedDistanceVoxels, out Vector3Int sdfDimensions, out Vector3 sdfOrigin, out Vector3 sdfCellSize))
            {
                _threatVoxelGrid = signedDistanceVoxels;
                _threatVoxelDimensions = new int3(sdfDimensions.x, sdfDimensions.y, sdfDimensions.z);
                _threatVoxelOrigin = new float3(sdfOrigin.x, sdfOrigin.y, sdfOrigin.z);
                _threatVoxelCellSize = new float3(sdfCellSize.x, sdfCellSize.y, sdfCellSize.z);
                _threatVoxelSolidThreshold = SignedDistanceSolidThreshold;
                _threatVoxelUsesSignedDistanceEncoding = true;
                return;
            }

            _threatVoxelGrid = default;
            _threatVoxelDimensions = int3.zero;
            _threatVoxelOrigin = float3.zero;
            _threatVoxelCellSize = new float3(1f, 1f, 1f);
            _threatVoxelSolidThreshold = SolidThreatVoxel;
            _threatVoxelUsesSignedDistanceEncoding = false;
        }

        private static void RefreshChemicalGridSnapshot(int frameId)
        {
            if (_lastChemicalGridBindFrame == frameId)
                return;

            _lastChemicalGridBindFrame = frameId;
            if (ChemicalInfluenceGrid.TryGetPublishedSnapshot(
                    out NativeArray<float4> frontGrid,
                    out NativeArray<float4> overlayGrid,
                    out int3 dimensions,
                    out float3 origin,
                    out float3 cellSize))
            {
                _chemicalFrontGrid = frontGrid;
                _chemicalOverlayGrid = overlayGrid;
                _chemicalGridDimensions = dimensions;
                _chemicalGridOrigin = origin;
                _chemicalGridCellSize = cellSize;
            }
            else
            {
                _chemicalFrontGrid = default;
                _chemicalOverlayGrid = default;
                _chemicalGridDimensions = int3.zero;
                _chemicalGridOrigin = float3.zero;
                _chemicalGridCellSize = new float3(1f, 1f, 1f);
            }

            if (ChemicalInfluenceGrid.TryGetPublishedBreadcrumbs(
                out NativeArray<ChemicalInfluenceGrid.ChemicalBreadcrumbWaypoint> breadcrumbs,
                out int count,
                out float followStepMeters))
            {
                _chemicalBreadcrumbs = breadcrumbs;
                _chemicalBreadcrumbCount = count;
                _chemicalBreadcrumbFollowStepMeters = followStepMeters;
                return;
            }

            _chemicalBreadcrumbs = default;
            _chemicalBreadcrumbCount = 0;
            _chemicalBreadcrumbFollowStepMeters = 12f;
        }

        private static void EmitFearPheromones()
        {
            if (!_activeSlots.IsCreated || !_cores.IsCreated || !_inputs.IsCreated || !_outputs.IsCreated)
                return;

            for (int i = 0; i < _activeSlotCount; i++)
            {
                int slot = _activeSlots[i];
                if (slot < 0 || slot >= Capacity)
                    continue;

                bool predatorRole = (_inputs[slot].Flags & (int)CognitionInputFlags.PredatorRole) != 0;
                int chosenState = _chosenStates.IsCreated ? _chosenStates[slot] : 0;
                bool fleeing = predatorRole
                    ? chosenState == (int)PredatorUtilityState.Fleeing
                    : IsPassiveFleeState((FaunaBrain.AIState)_outputs[slot].LegacyState);
                if (!fleeing)
                    continue;

                UnpackDriveChannels(_cores[slot].QuantizedDrives, out _, out _, out float fear, out _);
                if (fear < FearPheromoneInjectionThreshold)
                    continue;

                float3 position = _cores[slot].Position;
                ChemicalInfluenceGrid.QueueFearPheromone(new Vector3(position.x, position.y, position.z), fear);
            }
        }

        private static bool IsPassiveFleeState(FaunaBrain.AIState state)
        {
            return state == FaunaBrain.AIState.Escape ||
                   state == FaunaBrain.AIState.ApexForcedRetreat ||
                   state == FaunaBrain.AIState.Retreat;
        }

        private static bool ContainsActiveSlot(int slot)
        {
            if (!_activeSlots.IsCreated)
                return false;

            for (int i = 0; i < _activeSlotCount; i++)
            {
                if (_activeSlots[i] == slot)
                    return true;
            }

            return false;
        }

        private static bool IsValidSlot(int slot)
        {
            return _cores.IsCreated &&
                   slot >= 0 &&
                   slot < Capacity &&
                   _slotUsed.IsCreated &&
                   _slotUsed[slot] != 0;
        }

        private static void ClearMemoryEntries(int slot)
        {
            if (!_memoryBank.IsCreated || slot < 0 || slot >= Capacity)
                return;

            int startIndex = slot * MemorySlotsPerCreature;
            for (int i = 0; i < MemorySlotsPerCreature; i++)
                _memoryBank[startIndex + i] = default;
        }

        private static void ClearAcousticMemoryEntries(int slot)
        {
            NativeArray<float4> acousticMemoryFloat4Bank = ResolveAcousticMemoryFloat4Bank();
            if ((!_acousticMemoryBank.IsCreated && !acousticMemoryFloat4Bank.IsCreated) || slot < 0 || slot >= Capacity)
                return;

            int startIndex = slot * AcousticMemorySlotsPerCreature;
            for (int i = 0; i < AcousticMemorySlotsPerCreature; i++)
            {
                if (_acousticMemoryBank.IsCreated)
                    _acousticMemoryBank[startIndex + i] = default;
                if (acousticMemoryFloat4Bank.IsCreated)
                    acousticMemoryFloat4Bank[startIndex + i] = default;
            }
        }

        private static void ClearBoidClaims()
        {
            if (!_boidClaimTable.IsCreated)
                return;

            for (int i = 0; i < _boidClaimTable.Length; i++)
                _boidClaimTable[i] = UnclaimedBoidSlot;

            if (_claimedBoidIndices.IsCreated)
            {
                for (int i = 0; i < _claimedBoidIndices.Length; i++)
                    _claimedBoidIndices[i] = UnclaimedBoidSlot;
            }

            if (_packBaitClaimTable.IsCreated)
            {
                for (int i = 0; i < _packBaitClaimTable.Length; i++)
                    _packBaitClaimTable[i] = UnclaimedBoidSlot;
            }

            if (_packFlankerClaimTable.IsCreated)
            {
                for (int i = 0; i < _packFlankerClaimTable.Length; i++)
                    _packFlankerClaimTable[i] = UnclaimedBoidSlot;
            }

            if (_baseSiegeRammerClaimTable.IsCreated)
            {
                for (int i = 0; i < _baseSiegeRammerClaimTable.Length; i++)
                    _baseSiegeRammerClaimTable[i] = UnclaimedBoidSlot;
            }

            if (_baseSiegeDistractorClaimTable.IsCreated)
            {
                for (int i = 0; i < _baseSiegeDistractorClaimTable.Length; i++)
                    _baseSiegeDistractorClaimTable[i] = UnclaimedBoidSlot;
            }

            if (_baseSiegeLoitererClaimTable.IsCreated)
            {
                for (int i = 0; i < _baseSiegeLoitererClaimTable.Length; i++)
                    _baseSiegeLoitererClaimTable[i] = UnclaimedBoidSlot;
            }
        }

        private static void ClearPredatorSpeciesTargets()
        {
            if (_predatorSpeciesTargetCount.IsCreated && _predatorSpeciesTargetCount.Length > 0)
                _predatorSpeciesTargetCount[0] = 0;
        }

        private static int ResolvePredatorSpeciesTargetCapacity()
        {
            if (!_predatorSpeciesTargetIds.IsCreated || !_predatorSpeciesTargetPositions.IsCreated)
                return 0;

            return math.min(_predatorSpeciesTargetIds.Length, _predatorSpeciesTargetPositions.Length);
        }

        private static void RefreshHabitatSiegeSnapshot()
        {
            int previousCount = _habitatSiegeTargetCount;
            _habitatSiegeTargetCount = 0;
            if (!_habitatSiegeTargets.IsCreated)
                return;

            if (!HabitatGraphManager.TryGetLatestSiegeTargets(out NativeArray<HabitatSiegeTargetSnapshot> source, out int sourceCount))
                return;

            int copyCount = math.min(sourceCount, math.min(source.Length, _habitatSiegeTargets.Length));
            for (int i = 0; i < copyCount; i++)
                _habitatSiegeTargets[i] = source[i];

            for (int i = copyCount; i < previousCount; i++)
                _habitatSiegeTargets[i] = default;

            _habitatSiegeTargetCount = copyCount;
        }

        private static float ResolveEvaluationInterval(float importanceScore)
        {
            float normalizedImportance = math.saturate(importanceScore);
            if (normalizedImportance >= HighImportanceThreshold)
                return CenterEvaluationIntervalSeconds;

            if (normalizedImportance >= FocusImportanceThreshold)
                return FocusEvaluationIntervalSeconds;

            if (normalizedImportance >= MidImportanceThreshold)
                return PeripheryEvaluationIntervalSeconds;

            if (normalizedImportance >= LowImportanceThreshold)
                return FarEvaluationIntervalSeconds;

            return RearEvaluationIntervalSeconds;
        }

        private static float3 ComputeSwarmBoundsMin()
        {
            if (!_activeSlots.IsCreated || _activeSlotCount <= 0)
                return float3.zero;

            float3 boundsMin = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
            for (int i = 0; i < _activeSlotCount; i++)
            {
                int slot = _activeSlots[i];
                boundsMin = math.min(boundsMin, _inputs[slot].Position);
            }

            return boundsMin.x == float.MaxValue ? float3.zero : boundsMin;
        }

        private static int3 ResolveSpatialBucketCoordinates(float3 worldPosition, float3 boundsMin, float bucketCellSize)
        {
            float safeCellSize = math.max(bucketCellSize, 0.001f);
            float invCellSize = math.rcp(safeCellSize);
            float3 localPosition = math.max(worldPosition - boundsMin, float3.zero);
            return new int3(
                (int)math.floor(localPosition.x * invCellSize),
                (int)math.floor(localPosition.y * invCellSize),
                (int)math.floor(localPosition.z * invCellSize));
        }

        private static int3 ResolveAcousticBucketCoordinates(float3 worldPosition, float bucketCellSize)
        {
            float safeCellSize = math.max(bucketCellSize, 0.001f);
            float invCellSize = math.rcp(safeCellSize);
            int3 rawBucket = new int3(
                (int)math.floor(worldPosition.x * invCellSize),
                (int)math.floor(worldPosition.y * invCellSize),
                (int)math.floor(worldPosition.z * invCellSize));
            return rawBucket + new int3(
                AcousticBucketOriginBiasCells,
                AcousticBucketOriginBiasCells,
                AcousticBucketOriginBiasCells);
        }

        private static uint HashAcousticBucket(int3 bucketCoord)
        {
            uint x = (uint)bucketCoord.x;
            uint y = (uint)bucketCoord.y;
            uint z = (uint)bucketCoord.z;
            return (x * 73856093u) ^ (y * 19349663u) ^ (z * 83492791u);
        }

        private static CognitionOutput BuildDefaultOutput(float3 fallbackForward)
        {
            CognitionOutput output = default;
            output.DesiredDirection = ResolveDominantAxis(fallbackForward, new float3(0f, 0f, 1f));
            output.ForceMultiplier = 1f;
            output.SpeedMultiplier = 1f;
            output.TurnMultiplier = 1f;
            output.LegacyState = (int)FaunaBrain.AIState.Wander;
            return output;
        }

        private static PackedCognitionOutput BuildDefaultPackedOutput(float3 fallbackForward)
        {
            PackedCognitionOutput output = default;
            output.DesiredDirection = ResolveDominantAxis(fallbackForward, new float3(0f, 0f, 1f));
            output.ForceMultiplier = 1f;
            output.SpeedMultiplier = 1f;
            output.TurnMultiplier = 1f;
            output.LegacyState = (int)FaunaBrain.AIState.Wander;
            return output;
        }

        private static float3 ResolveDominantAxis(float3 direction, float3 fallback)
        {
            if (math.lengthsq(direction) <= DdaEpsilon)
                direction = fallback;

            if (math.lengthsq(direction) <= DdaEpsilon)
                return new float3(0f, 0f, 1f);

            float3 absolute = math.abs(direction);
            if (absolute.x >= absolute.y && absolute.x >= absolute.z)
                return new float3(math.select(1f, -1f, direction.x < 0f), 0f, 0f);

            if (absolute.y >= absolute.z)
                return new float3(0f, math.select(1f, -1f, direction.y < 0f), 0f);

            return new float3(0f, 0f, math.select(1f, -1f, direction.z < 0f));
        }

        private static uint PackDriveChannels(float hunger, float aggression, float fear, float threatLevel)
        {
            return QuantizeToLane(hunger) |
                   (QuantizeToLane(aggression) << 8) |
                   (QuantizeToLane(fear) << 16) |
                   (QuantizeToLane(threatLevel) << 24);
        }

        private static void UnpackDriveChannels(uint packedDrives, out float hunger, out float aggression, out float fear, out float threatLevel)
        {
            hunger = DequantizeLane(packedDrives & 0xFFu);
            aggression = DequantizeLane((packedDrives >> 8) & 0xFFu);
            fear = DequantizeLane((packedDrives >> 16) & 0xFFu);
            threatLevel = DequantizeLane((packedDrives >> 24) & 0xFFu);
        }

        private static uint PackSingleDrive(float value)
        {
            return QuantizeToLane(value);
        }

        private static float UnpackSingleDrive(uint packedDrive)
        {
            return DequantizeLane(packedDrive & 0xFFu);
        }

        private static float UnpackThreatLevel(uint packedDrives)
        {
            return DequantizeLane((packedDrives >> 24) & 0xFFu);
        }

        private static uint PackScoreTriplet(float hungerScore, float aggressionScore, float fearScore)
        {
            return QuantizeToLane(hungerScore) |
                   (QuantizeToLane(aggressionScore) << 8) |
                   (QuantizeToLane(fearScore) << 16);
        }

        private static void UnpackScoreTriplet(uint packedScores, out float hungerScore, out float aggressionScore, out float fearScore)
        {
            hungerScore = DequantizeLane(packedScores & 0xFFu);
            aggressionScore = DequantizeLane((packedScores >> 8) & 0xFFu);
            fearScore = DequantizeLane((packedScores >> 16) & 0xFFu);
        }

        private static uint QuantizeToLane(float value)
        {
            return (uint)math.clamp((int)math.round(math.saturate(value) * QuantizedByteScale), 0, 255);
        }

        private static float DequantizeLane(uint lane)
        {
            return math.saturate(lane * QuantizedByteInvScale);
        }

        internal static JobHandle ScheduleMockStimulusProbe(
            NativeArray<PredatorCognitionDTO> dtos,
            NativeQueue<PredatorMockAcousticSignal>.ParallelWriter acousticSignals,
            NativeArray<MockLightSource> lightSources,
            NativeArray<SignalWardenMockDamageSignal> damageSignals,
            uint seed,
            float currentTime,
            JobHandle dependency)
        {
            if (!dtos.IsCreated || dtos.Length <= 0)
                return dependency;

            var job = new MockPredatorStimulusJob
            {
                Dtos = dtos,
                AcousticSignals = acousticSignals,
                LightSources = lightSources,
                DamageSignals = damageSignals,
                Seed = seed == 0u ? 1u : seed,
                CurrentTime = currentTime
            };
            return job.Schedule(dtos.Length, EvaluationJobBatchSize, dependency);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct MockPredatorStimulusJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<PredatorCognitionDTO> Dtos;
            public NativeQueue<PredatorMockAcousticSignal>.ParallelWriter AcousticSignals;
            [NativeDisableParallelForRestriction] public NativeArray<MockLightSource> LightSources;
            [NativeDisableParallelForRestriction] public NativeArray<SignalWardenMockDamageSignal> DamageSignals;
            public uint Seed;
            public float CurrentTime;

            public void Execute(int index)
            {
                PredatorCognitionDTO* dtoPtr = (PredatorCognitionDTO*)Dtos.GetUnsafePtr() + index;
                ref PredatorCognitionDTO dto = ref PredatorCognitionDTO.AsMutableRef(dtoPtr);
                uint localSeed = math.max(1u, Seed ^ ((uint)index * 0x9E3779B9u));
                Unity.Mathematics.Random random = new Unity.Mathematics.Random(localSeed);
                double3 currentAup = dto.CurrentAUP;
                float3 current = AupPrecisionMath.DowncastProceduralPhase(currentAup, float3.zero);
                float3 forward = ResolveDominantAxis(dto.ForwardVector, new float3(0f, 0f, 1f));

                if (random.NextFloat() < 0.25f)
                {
                    float3 noiseOffset = ResolveDominantAxis(new float3(random.NextFloat(-1f, 1f), random.NextFloat(-0.4f, 0.4f), random.NextFloat(-1f, 1f)), forward);
                    float acousticDistance = random.NextFloat(4f, 24f);
                    float3 acousticOffset = noiseOffset * acousticDistance;
                    double3 acousticAup = currentAup + new double3(acousticOffset.x, acousticOffset.y, acousticOffset.z);
                    PredatorMockAcousticSignal acoustic = default;
                    acoustic.Position = current + acousticOffset;
                    acoustic.Timestamp = CurrentTime;
                    acoustic.Intensity = random.NextFloat(0.35f, 1f);
                    acoustic.SourceId = unchecked((uint)index + 1u);
                    AcousticSignals.Enqueue(acoustic);
                    dto.TargetAUP = math.all(math.isfinite(acousticAup)) ? acousticAup : currentAup;
                }

                float fearSpike = 0f;
                if (LightSources.IsCreated && index < LightSources.Length && random.NextFloat() < 0.16f)
                {
                    float3 lightOffset = forward * random.NextFloat(5f, 18f);
                    MockLightSource light = default;
                    light.Position = current + lightOffset;
                    light.RangeSq = 400f;
                    light.Intensity = random.NextFloat(0.65f, 1f);
                    light.SourceId = unchecked(0x4C495448u + (uint)index);
                    LightSources[index] = light;
                }

                if (LightSources.IsCreated)
                {
                    int lightCount = math.min(LightSources.Length, 4);
                    for (int i = 0; i < lightCount; i++)
                    {
                        MockLightSource light = LightSources[i];
                        float3 toLight = light.Position - current;
                        float distanceSq = math.lengthsq(toLight);
                        if (distanceSq <= DdaEpsilon || distanceSq > math.max(1f, light.RangeSq))
                            continue;

                        float3 toLightDir = toLight * math.rsqrt(math.max(distanceSq, MathSafetyEpsilon));
                        float glareDot = math.dot(forward, toLightDir);
                        fearSpike = math.max(fearSpike, math.select(0f, light.Intensity, glareDot > 0.8f));
                    }
                }

                if (DamageSignals.IsCreated && index < DamageSignals.Length && random.NextFloat() < 0.08f)
                {
                    SignalWardenMockDamageSignal damage = default;
                    damage.Aup = dto.CurrentAUP;
                    damage.Damage = random.NextFloat(0.2f, 0.85f);
                    damage.EntityId = dto.TargetID;
                    DamageSignals[index] = damage;
                }

                if (DamageSignals.IsCreated)
                {
                    int damageCount = math.min(DamageSignals.Length, 4);
                    for (int i = 0; i < damageCount; i++)
                    {
                        SignalWardenMockDamageSignal damage = DamageSignals[i];
                        if (damage.EntityId != dto.TargetID)
                            continue;

                        fearSpike = math.max(fearSpike, damage.Damage);
                    }
                }

                dto.Fear = math.saturate(math.max(dto.Fear, fearSpike));
                bool enraged = dto.Fear > 0.75f && dto.Hunger > 0.8f;
                dto.CurrentState = (byte)math.select(
                    (int)math.select((int)PredatorUtilityState.Stalking, (int)PredatorUtilityState.Attacking, dto.Hunger > 0.55f),
                    (int)PredatorUtilityState.Fleeing,
                    dto.Fear > 0.65f && !enraged);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct SwarmAnalysisJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> ActiveSlots;
            public int ActiveSlotCount;
            [ReadOnly] public NativeArray<CognitionInput> Inputs;
            [ReadOnly] public NativeArray<CognitionCore> PriorCores;
            [ReadOnly] public NativeArray<byte> DueFlags;
            public NativeArray<float> AmbientThreats;
            public NativeArray<float3> SwarmCenters;
            public NativeArray<float3> SwarmDirections;
            public NativeArray<float3> SwarmAvoidances;
            public NativeArray<int> SwarmCounts;
            public NativeArray<int> ClaimedBoidIndices;
            public NativeArray<float3> ClaimedBoidPositions;
            public NativeArray<float3> PredatorPackTargets;
            public NativeArray<float> PredatorPackWeights;
            public NativeArray<float3> PredatorPackBaitPositions;
            public NativeArray<float3> PredatorPackSharedPlayerPositions;
            public NativeArray<AbsoluteUniversePositionBlit128> PredatorPackTargetAups;
            public NativeArray<byte> PredatorPackRoles;
            [NativeDisableParallelForRestriction] public NativeArray<int> PredatorSpeciesTargetIds;
            [NativeDisableParallelForRestriction] public NativeArray<float3> PredatorSpeciesTargetPositions;
            [NativeDisableUnsafePtrRestriction] public int* PredatorSpeciesTargetCount;
            public int PredatorSpeciesTargetCapacity;
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // Pack role claim tables are raw int pointers because this swarm analysis job uses Interlocked.CompareExchange
            // on reservation slots. Unity's safety system cannot model pointer atomics, but each pointer comes from a
            // persistent NativeArray owned by PredatorCognitionDomain and sized to Capacity before scheduling.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // NativeArray<int> with normal safety handles was rejected because Burst cannot pass its element address to
            // Interlocked.CompareExchange without unsafe access. Duplicating claim tables per worker was rejected because
            // pack roles must resolve to one shared reservation table for bait/flanker exclusivity in the same frame.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Safety invariant: each attempted write targets reservationIndex in [0, Capacity) and is guarded by atomic
            // compare-exchange. The Vault buffers are allocated once and released only
            // through PredatorCognitionDomain teardown after the scheduled job dependency is included.
            [NativeDisableUnsafePtrRestriction] public int* PackBaitClaimTable;
            [NativeDisableUnsafePtrRestriction] public int* PackFlankerClaimTable;
            public float3 SwarmBoundsMin;
            [ReadOnly] public NativeArray<int> TargetHashBucketHeads;
            [ReadOnly] public NativeArray<int> TargetHashNext;

            public void Execute(int index)
            {
                int slot = ActiveSlots[index];
                CognitionInput input = Inputs[slot];
                if ((input.Flags & (int)CognitionInputFlags.Active) == 0)
                {
                    AmbientThreats[slot] = 0f;
                    SwarmCenters[slot] = input.FlockCenter;
                    SwarmDirections[slot] = input.FlockDirection;
                    SwarmAvoidances[slot] = input.FlockAvoidance;
                    SwarmCounts[slot] = 0;
                    ClaimedBoidIndices[slot] = UnclaimedBoidSlot;
                    ClaimedBoidPositions[slot] = float3.zero;
                    PredatorPackTargets[slot] = input.PlayerPosition;
                    PredatorPackWeights[slot] = 0f;
                    PredatorPackBaitPositions[slot] = input.PlayerPosition;
                    PredatorPackSharedPlayerPositions[slot] = input.PlayerPosition;
                    PredatorPackRoles[slot] = (byte)PredatorPackRole.None;
                    return;
                }

                if (DueFlags[slot] == 0)
                    return;

                int3 contagionBucket = ResolveSpatialBucketCoordinates(input.Position, SwarmBoundsMin, ContagionBucketCellSize);
                int3 selfSwarmBucket = ResolveSpatialBucketCoordinates(input.Position, SwarmBoundsMin, SwarmBucketCellSize);
                float threatSum = 0f;
                int threatCount = 0;
                float3 separationForce = float3.zero;
                float3 alignmentSum = float3.zero;
                float3 cohesionSum = float3.zero;
                float3 pbdCorrection = float3.zero;
                int neighbourCount = 0;
                int claimedSlot = UnclaimedBoidSlot;
                float3 claimedPosition = float3.zero;
                float bestClaimDistanceSq = float.MaxValue;
                bool canClaimBoid = (input.Flags & (int)CognitionInputFlags.PredatorRole) != 0;
                bool selfHasPlayerTarget = (input.Flags & (int)CognitionInputFlags.HasPlayerTarget) != 0;
                bool selfHasPackTarget = (input.Flags & (int)CognitionInputFlags.HasPackTarget) != 0;
                float packCoordinationRadius = math.max(0f, input.PackCoordinationRadius);
                float packCoordinationRadiusSq = packCoordinationRadius * packCoordinationRadius;
                float packFlankDistance = math.max(0f, input.PackFlankDistance);
                bool canCoordinatePack = canClaimBoid &&
                                         packCoordinationRadius > DdaEpsilon &&
                                         packFlankDistance > DdaEpsilon;
                AbsoluteUniversePositionBlit128 predatorPackTargetAup = selfHasPackTarget
                    ? input.PackTargetAup
                    : default;
                float3 predatorPackSharedPlayerPosition = selfHasPackTarget
                    ? ResolveRuntimePosition(in input.PackTargetAup, input.FloatingOriginOffset)
                    : input.PackTargetPosition;
                if (canCoordinatePack && selfHasPackTarget && selfHasPlayerTarget && input.SpeciesId != 0)
                    AddPredatorSpeciesTarget(input.SpeciesId, predatorPackSharedPlayerPosition);

                float3 predatorPackTarget = predatorPackSharedPlayerPosition;
                float predatorPackWeight = 0f;
                float3 predatorPackBaitPosition = input.Position;
                PredatorPackRole predatorPackRole = PredatorPackRole.None;
                int bestBaitSlot = selfHasPackTarget ? slot : -1;
                float bestBaitDistanceSq = selfHasPackTarget
                    ? math.lengthsq(predatorPackSharedPlayerPosition - input.Position)
                    : float.MaxValue;
                float perceptionRadiusSq = SwarmPerceptionRadius * SwarmPerceptionRadius;
                float separationRadiusSq = SwarmSeparationRadius * SwarmSeparationRadius;
                float swarmPbdMinDistanceSq = SwarmPbdMinDistance * SwarmPbdMinDistance;
                int maxNeighborIterations = math.min(math.max(ActiveSlotCount, 0), MaxSwarmNeighborIterations);
                int processedNeighborIterations = 0;
                bool targetHashReady = TargetHashBucketHeads.IsCreated && TargetHashNext.IsCreated;
                for (int offsetX = -1; offsetX <= 1 && processedNeighborIterations < maxNeighborIterations; offsetX++)
                {
                    for (int offsetY = -1; offsetY <= 1 && processedNeighborIterations < maxNeighborIterations; offsetY++)
                    {
                        for (int offsetZ = -1; offsetZ <= 1 && processedNeighborIterations < maxNeighborIterations; offsetZ++)
                        {
                            if (!targetHashReady)
                                continue;

                            int neighborBucket = HashSpatialBucket(selfSwarmBucket + new int3(offsetX, offsetY, offsetZ)) &
                                                 PredatorTargetSpatialHashBucketMask;
                            if ((uint)neighborBucket >= (uint)TargetHashBucketHeads.Length)
                                continue;

                            int otherSlot = TargetHashBucketHeads[neighborBucket];
                            int chainGuard = 0;
                            while (otherSlot != UnclaimedBoidSlot &&
                                   (uint)otherSlot < (uint)TargetHashNext.Length &&
                                   chainGuard++ < MaxSwarmNeighborIterations)
                            {
                                int currentSlot = otherSlot;
                                otherSlot = TargetHashNext[currentSlot];

                                if (processedNeighborIterations++ >= maxNeighborIterations)
                                    break;

                                CognitionInput otherInput = Inputs[currentSlot];
                                if ((otherInput.Flags & (int)CognitionInputFlags.Active) == 0)
                                    continue;

                                int3 otherContagionBucket = ResolveSpatialBucketCoordinates(otherInput.Position, SwarmBoundsMin, ContagionBucketCellSize);
                                if (math.all(otherContagionBucket == contagionBucket))
                                {
                                    threatSum += UnpackThreatLevel(PriorCores[currentSlot].QuantizedDrives);
                                    threatCount++;
                                }

                                if (currentSlot == slot)
                                    continue;

                                float3 diff = input.Position - otherInput.Position;
                                float distSq = math.lengthsq(diff);
                                if (distSq <= DdaEpsilon || distSq > perceptionRadiusSq)
                                    continue;

                                bool sameSpecies = otherInput.SpeciesId == input.SpeciesId;
                                if (sameSpecies)
                                {
                                    float inSeparation = math.select(0f, 1f, distSq < separationRadiusSq);
                                    separationForce += (diff * math.rcp(math.max(distSq, MathSafetyEpsilon))) * inSeparation;
                                    alignmentSum += otherInput.Velocity;
                                    cohesionSum += otherInput.Position;
                                    neighbourCount++;

                                    if (distSq < swarmPbdMinDistanceSq)
                                    {
                                        float3 dir = ResolveDominantAxis(diff, float3.zero);
                                        float push01 = 1f - math.saturate(distSq * math.rcp(math.max(swarmPbdMinDistanceSq, MathSafetyEpsilon)));
                                        pbdCorrection += dir * (push01 * SwarmPbdMinDistance * 0.5f);
                                    }
                                }

                                if (canCoordinatePack &&
                                    sameSpecies &&
                                    (otherInput.Flags & (int)CognitionInputFlags.PredatorRole) != 0 &&
                                    (otherInput.Flags & (int)CognitionInputFlags.HasPackTarget) != 0 &&
                                    (PriorCores[currentSlot].StateFlags & (uint)FaunaWorldStateFlags.Hunting) != 0u)
                                {
                                    float3 otherTargetPosition = ResolveRuntimePosition(in otherInput.PackTargetAup, otherInput.FloatingOriginOffset);
                                    float otherDistanceToTargetSq = math.lengthsq(otherTargetPosition - otherInput.Position);
                                    if (otherDistanceToTargetSq < bestBaitDistanceSq)
                                    {
                                        bestBaitDistanceSq = otherDistanceToTargetSq;
                                        bestBaitSlot = currentSlot;
                                    }

                                    float coordinationWeight = 1f - math.saturate(distSq * math.rcp(math.max(packCoordinationRadiusSq, 1f)));
                                    if (coordinationWeight > predatorPackWeight)
                                    {
                                        float3 targetForward = ResolveDominantAxis(
                                            otherInput.PackTargetVelocity,
                                            ResolveDominantAxis(otherInput.PlayerForward, ResolveDominantAxis(otherInput.Forward, new float3(0f, 0f, 1f))));
                                        float3 packRight = ResolveDominantAxis(
                                            math.cross(new float3(0f, 1f, 0f), targetForward),
                                            math.cross(new float3(0f, 0f, 1f), targetForward));
                                        if (math.lengthsq(packRight) > DdaEpsilon)
                                        {
                                            float sideSign = math.select(-1f, 1f, math.dot(input.Position - otherTargetPosition, packRight) >= 0f);
                                            predatorPackSharedPlayerPosition = otherTargetPosition;
                                            predatorPackTarget = otherTargetPosition + (packRight * (packFlankDistance * sideSign));
                                            predatorPackBaitPosition = otherInput.Position;
                                            predatorPackTargetAup = otherInput.PackTargetAup;
                                            predatorPackWeight = coordinationWeight;
                                        }
                                    }
                                }

                                if (!canClaimBoid || (otherInput.Flags & (int)CognitionInputFlags.PredatorRole) != 0 || distSq >= bestClaimDistanceSq)
                                    continue;

                                claimedSlot = currentSlot;
                                claimedPosition = otherInput.Position;
                                bestClaimDistanceSq = distSq;
                            }
                        }
                    }
                }

                AmbientThreats[slot] = threatCount > 0
                    ? math.saturate(threatSum * math.rcp(math.max((float)threatCount, MathSafetyEpsilon)))
                    : 0f;
                ClaimedBoidIndices[slot] = claimedSlot;
                ClaimedBoidPositions[slot] = claimedPosition;

                if (canCoordinatePack)
                {
                    int reservationIndex = ResolvePackReservationIndex(input.SpeciesId);
                    if (bestBaitSlot == slot && selfHasPackTarget)
                    {
                        if (TryReservePackRole(PackBaitClaimTable, reservationIndex, slot))
                        {
                            predatorPackRole = PredatorPackRole.Bait;
                            predatorPackWeight = math.max(predatorPackWeight, 1f);
                            predatorPackSharedPlayerPosition = ResolveRuntimePosition(in input.PackTargetAup, input.FloatingOriginOffset);
                            predatorPackTarget = predatorPackSharedPlayerPosition;
                            predatorPackBaitPosition = input.Position;
                            predatorPackTargetAup = input.PackTargetAup;
                        }
                    }
                    else if (bestBaitSlot >= 0 && TryReservePackRole(PackFlankerClaimTable, reservationIndex, slot))
                    {
                        predatorPackRole = PredatorPackRole.Flanker;
                        predatorPackBaitPosition = Inputs[bestBaitSlot].Position;
                    }
                }

                float3 swarmCenter = input.FlockCenter;
                float3 swarmDirection = input.FlockDirection;
                float3 swarmAvoidance = input.FlockAvoidance;
                int swarmCount = math.max(0, input.FlockCount);
                if (neighbourCount > 0)
                {
                    float invNeighbourCount = math.rcp(math.max((float)neighbourCount, MathSafetyEpsilon));
                    float3 averageVelocity = alignmentSum * invNeighbourCount;
                    float3 centerOfMass = cohesionSum * invNeighbourCount;
                    float3 alignmentForce = averageVelocity - input.Velocity;
                    float3 cohesionForce = centerOfMass - input.Position;
                    float3 acceleration =
                        (separationForce * SwarmSeparationWeight) +
                        (alignmentForce * SwarmAlignmentWeight) +
                        (cohesionForce * SwarmCohesionWeight) +
                        (pbdCorrection * SwarmPbdWeight);

                    swarmCenter = centerOfMass;
                    swarmDirection = ResolveDominantAxis(averageVelocity, ResolveDominantAxis(input.FlockDirection, new float3(0f, 0f, 1f)));
                    swarmAvoidance = ResolveDominantAxis(acceleration, ResolveDominantAxis(input.FlockAvoidance, float3.zero));
                    swarmCount = neighbourCount;
                }

                SwarmCenters[slot] = swarmCenter;
                SwarmDirections[slot] = swarmDirection;
                SwarmAvoidances[slot] = swarmAvoidance;
                SwarmCounts[slot] = swarmCount;
                PredatorPackTargets[slot] = predatorPackTarget;
                PredatorPackWeights[slot] = predatorPackWeight;
                PredatorPackBaitPositions[slot] = predatorPackBaitPosition;
                PredatorPackSharedPlayerPositions[slot] = predatorPackSharedPlayerPosition;
                PredatorPackTargetAups[slot] = predatorPackTargetAup;
                PredatorPackRoles[slot] = (byte)predatorPackRole;
            }

            private void AddPredatorSpeciesTarget(int speciesId, float3 targetPosition)
            {
                if (speciesId == 0 ||
                    !PredatorSpeciesTargetIds.IsCreated ||
                    !PredatorSpeciesTargetPositions.IsCreated ||
                    PredatorSpeciesTargetCount == null)
                {
                    return;
                }

                int capacity = math.min(
                    math.max(PredatorSpeciesTargetCapacity, 0),
                    math.min(PredatorSpeciesTargetIds.Length, PredatorSpeciesTargetPositions.Length));
                if (capacity <= 0)
                    return;

                int writeIndex = System.Threading.Interlocked.Increment(ref *PredatorSpeciesTargetCount) - 1;
                if ((uint)writeIndex >= (uint)capacity)
                    return;

                PredatorSpeciesTargetIds[writeIndex] = speciesId;
                PredatorSpeciesTargetPositions[writeIndex] = targetPosition;
            }

            private static int ResolvePackReservationIndex(int speciesId)
            {
                return math.abs(speciesId) % Capacity;
            }

            private static unsafe bool TryReservePackRole(int* claimTable, int reservationIndex, int creatureSlot)
            {
                if (claimTable == null || reservationIndex < 0 || reservationIndex >= Capacity || creatureSlot < 0)
                    return false;

                for (int attempt = 0; attempt < MaxPackRoleCasAttempts; attempt++)
                {
                    int priorOwner = System.Threading.Interlocked.CompareExchange(
                        ref claimTable[reservationIndex],
                        creatureSlot,
                        UnclaimedBoidSlot);
                    if (priorOwner == UnclaimedBoidSlot || priorOwner == creatureSlot)
                        return true;
                }

                return false;
            }

            private static float3 ResolveRuntimePosition(in AbsoluteUniversePositionBlit128 positionAup, double3 floatingOriginOffset)
            {
                double cellSize = AbsoluteUniversePosition.CellSizeMeters;
                double3 absolutePosition = new double3(
                    (positionAup.GridX * cellSize) + positionAup.Local.x,
                    (positionAup.GridY * cellSize) + positionAup.Local.y,
                    (positionAup.GridZ * cellSize) + positionAup.Local.z);
                double3 runtimePosition = absolutePosition - floatingOriginOffset;
                return new float3((float)runtimePosition.x, (float)runtimePosition.y, (float)runtimePosition.z);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct PredatorCognitionJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> ActiveSlots;
            [ReadOnly] public NativeArray<CognitionInput> Inputs;
            public NativeArray<CognitionCore> Cores;
            public NativeArray<CognitionControl> Controls;
            [ReadOnly] public NativeArray<CognitionMemoryEntry> MemoryBank;
            [ReadOnly] public NativeArray<AcousticMemoryEntry> AcousticMemoryBank;
            [ReadOnly] public NativeArray<float4> AcousticMemoryFloat4Bank;
            [ReadOnly] public NativeArray<byte> DueFlags;
            [ReadOnly] public NativeArray<float> AmbientThreats;
            [ReadOnly] public NativeArray<float3> SwarmCenters;
            [ReadOnly] public NativeArray<float3> SwarmDirections;
            [ReadOnly] public NativeArray<float3> SwarmAvoidances;
            [ReadOnly] public NativeArray<int> SwarmCounts;
            [ReadOnly] public NativeArray<int> ClaimedBoidIndices;
            [ReadOnly] public NativeArray<float3> ClaimedBoidPositions;
            [ReadOnly] public NativeArray<float3> PredatorPackTargets;
            [ReadOnly] public NativeArray<float> PredatorPackWeights;
            [ReadOnly] public NativeArray<float3> PredatorPackBaitPositions;
            [ReadOnly] public NativeArray<float3> PredatorPackSharedPlayerPositions;
            [ReadOnly] public NativeArray<AbsoluteUniversePositionBlit128> PredatorPackTargetAups;
            [ReadOnly] public NativeArray<byte> PredatorPackRoles;
            [ReadOnly] public NativeArray<int> PredatorSpeciesTargetIds;
            [ReadOnly] public NativeArray<float3> PredatorSpeciesTargetPositions;
            [ReadOnly] public NativeArray<int> PredatorSpeciesTargetCount;
            [ReadOnly] public NativeArray<HabitatSiegeTargetSnapshot> HabitatSiegeTargets;
            public int HabitatSiegeTargetCount;
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // Base siege claim tables use raw pointers for the same atomic reservation pattern as pack roles. The safety
            // system sees an unsafe shared pointer, but the only writes are Interlocked.CompareExchange reservations for
            // bounded habitat target slots and do not alias any other container field.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // A managed lock is forbidden inside Burst. Per-role duplicated NativeArrays were rejected because they would
            // require a serial merge pass and would permit multiple predators to believe they own the same siege role for
            // one frame. Direct atomic slots keep the cinematic role fake deterministic and cheaper.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Safety invariant: HabitatSiegeTargetCount is clamped to the allocated table capacity, every target index is
            // range-checked before reservation, and disposal is deferred through the domain-owned job dependency path.
            [NativeDisableUnsafePtrRestriction] public int* BaseSiegeRammerClaimTable;
            [NativeDisableUnsafePtrRestriction] public int* BaseSiegeDistractorClaimTable;
            [NativeDisableUnsafePtrRestriction] public int* BaseSiegeLoitererClaimTable;
            [ReadOnly] public NativeArray<int> SpeciesTuningIds;
            [ReadOnly] public NativeArray<SpeciesCognitionTuning> SpeciesTuningValues;
            [ReadOnly] public NativeArray<int> SpeciesTuningCount;
            [ReadOnly] public NativeArray<float4> ApexCortexTuning;
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // ChosenStates and BoidClaimTable are intentionally shared output tables indexed by stable fauna slots, not
            // by the job iteration index. NativeDisableParallelForRestriction is required because the valid writer index
            // is ActiveSlots[index], which Unity cannot prove maps to disjoint slots.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Repacking ActiveSlots into dense output arrays was rejected because downstream consumers address these
            // tables by slot id. A post-job scatter copy was rejected because it adds a full extra pass over Capacity for
            // data that can be written directly once per active slot.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Safety invariant: ActiveSlots contains unique registered slots, due flags only skip work and never duplicate
            // a slot, and BoidClaimTable competing writes are resolved through atomic reservation logic before claims are
            // consumed by later code.
            [NativeDisableParallelForRestriction] public NativeArray<byte> ChosenStates;
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // Alpha stalking phase tables are slot-addressed output lanes. ActiveSlots is unique, so every job iteration
            // writes at most one distinct phase/start-time pair and no dense remap pass is required.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // A managed per-creature phase object was rejected because it would allocate or require main-thread sync. The
            // SoA byte/float lanes preserve deterministic Burst ownership and keep cold telemetry separate.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Safety invariant: slots are registered once, unregistered slots are not active, and reset/unregister clear
            // both phase lanes before the slot can be reused by another fauna brain.
            [NativeDisableParallelForRestriction] public NativeArray<byte> StalkingPhases;
            [NativeDisableParallelForRestriction] public NativeArray<float> StalkingPhaseStartTimes;
            [NativeDisableParallelForRestriction] public NativeArray<int> BoidClaimTable;
            public NativeArray<PackedCognitionOutput> Outputs;
            [ReadOnly] public NativeArray<byte> ThreatVoxelGrid;
            public int3 ThreatVoxelDimensions;
            public float3 ThreatVoxelOrigin;
            public float3 ThreatVoxelCellSize;
            public byte ThreatVoxelSolidThreshold;
            public int ThreatVoxelUsesSignedDistanceEncoding;
            [ReadOnly] public NativeArray<float4> ChemicalFrontGrid;
            [ReadOnly] public NativeArray<float4> ChemicalOverlayGrid;
            public int3 ChemicalGridDimensions;
            public float3 ChemicalGridOrigin;
            public float3 ChemicalGridCellSize;
            [ReadOnly] public NativeArray<ChemicalInfluenceGrid.ChemicalBreadcrumbWaypoint> ChemicalBreadcrumbs;
            public int ChemicalBreadcrumbCount;
            public float ChemicalBreadcrumbFollowStepMeters;
            [ReadOnly] public NativeArray<LightSourceData> RetinalLightSources;
            public int RetinalLightCount;
            [NativeDisableParallelForRestriction] public NativeArray<float> RetinalExposure;
            [NativeDisableParallelForRestriction] public NativeArray<byte> BlindnessState;

            private bool TryReadSpeciesTuning(int speciesId, out SpeciesCognitionTuning tuning)
            {
                tuning = default;
                if (!SpeciesTuningIds.IsCreated ||
                    !SpeciesTuningValues.IsCreated ||
                    !SpeciesTuningCount.IsCreated ||
                    SpeciesTuningCount.Length <= 0)
                {
                    return false;
                }

                int count = math.clamp(
                    SpeciesTuningCount[0],
                    0,
                    math.min(SpeciesTuningIds.Length, SpeciesTuningValues.Length));
                for (int i = 0; i < count; i++)
                {
                    if (SpeciesTuningIds[i] != speciesId)
                        continue;

                    tuning = SpeciesTuningValues[i];
                    return true;
                }

                return false;
            }

            private bool TryReadPredatorSpeciesTarget(int speciesId, out float3 targetPosition)
            {
                targetPosition = default;
                if (!PredatorSpeciesTargetIds.IsCreated ||
                    !PredatorSpeciesTargetPositions.IsCreated ||
                    !PredatorSpeciesTargetCount.IsCreated ||
                    PredatorSpeciesTargetCount.Length <= 0)
                {
                    return false;
                }

                int count = math.clamp(
                    PredatorSpeciesTargetCount[0],
                    0,
                    math.min(PredatorSpeciesTargetIds.Length, PredatorSpeciesTargetPositions.Length));
                for (int i = count - 1; i >= 0; i--)
                {
                    if (PredatorSpeciesTargetIds[i] != speciesId)
                        continue;

                    float3 candidate = PredatorSpeciesTargetPositions[i];
                    if (!MathGuard.IsFinite(candidate))
                        continue;

                    targetPosition = candidate;
                    return true;
                }

                return false;
            }

            public void Execute(int index)
            {
                int slot = ActiveSlots[index];
                CognitionInput input = Inputs[slot];
                float3 fallbackForward = ResolveDominantAxis(input.Forward, new float3(0f, 0f, 1f));
                if ((input.Flags & (int)CognitionInputFlags.Active) == 0)
                {
                    Outputs[slot] = BuildDefaultPackedOutput(fallbackForward);
                    ChosenStates[slot] = 0;
                    if (StalkingPhases.IsCreated)
                    {
                        StalkingPhases[slot] = AlphaLeviathanPhase.Hidden;
                        StalkingPhaseStartTimes[slot] = 0f;
                    }
                    return;
                }

                if (DueFlags[slot] == 0)
                    return;

                if (TryReadSpeciesTuning(input.SpeciesId, out SpeciesCognitionTuning tuning))
                {
                    input.HungerWeight = tuning.HungerWeight;
                    input.FearWeight = tuning.FearWeight;
                    input.CuriosityWeight = tuning.CuriosityWeight;
                    input.LightReactionMode = (int)tuning.LightReactionMode;
                    input.LightFrenzySpeedMultiplier = tuning.LightFrenzySpeedMultiplier;
                    input.LightReactionFearBoost01 = tuning.LightReactionFearBoost01;
                }
                else if ((input.Flags & (int)CognitionInputFlags.UseAlphaLeviathanCognition) != 0 &&
                         ApexCortexTuning.IsCreated &&
                         ApexCortexTuning.Length > 0)
                {
                    float4 apex = ApexCortexTuning[0];
                    input.HungerWeight = math.max(0.1f, apex.x);
                    input.FearWeight = math.max(0.1f, apex.y);
                    input.CuriosityWeight = math.max(0.1f, apex.w);
                    input.LightReactionMode = (int)FaunaLightReactionMode.Aversion;
                    input.LightReactionFearBoost01 = math.saturate(apex.z);
                }

                CognitionCore core = Cores[slot];
                CognitionControl control = Controls[slot];
                CognitionInput resolvedInput = input;
                resolvedInput.FlockCenter = SwarmCenters[slot];
                resolvedInput.FlockDirection = SwarmDirections[slot];
                resolvedInput.FlockAvoidance = SwarmAvoidances[slot];
                resolvedInput.FlockCount = math.max(input.FlockCount, SwarmCounts[slot]);
                resolvedInput.ClaimedBoidIndex = ClaimedBoidIndices[slot];
                if (resolvedInput.ClaimedBoidIndex >= 0)
                    resolvedInput.PreyPosition = ClaimedBoidPositions[slot];

                core.Position = input.Position;
                core.Velocity = input.Velocity;
                core.SpeciesId = input.SpeciesId;
                core.ClaimedBoidIndex = UnclaimedBoidSlot;

                UnpackDriveChannels(core.QuantizedDrives, out float hunger, out float aggression, out float fear, out float threatLevel);
                float fatigue = UnpackSingleDrive(core.QuantizedFatigue);
                float dt = math.max(0f, input.DeltaTime);
                float metabolicDt = math.max(0f, input.MetabolicDeltaTime);
                hunger = math.clamp(hunger + (HungerRate * metabolicDt), 0f, 1f);
                fatigue = math.clamp(fatigue + (FatigueRate * metabolicDt), 0f, 1f);
                aggression = math.clamp(input.AggressionWeight, 0f, 1f);
                float ambientThreat = AmbientThreats[slot];
                fear = math.clamp((fear * FastExpNegPade13(-FearDecayLogK * dt)) + (ambientThreat * dt), 0f, 1f);
                threatLevel = math.clamp(math.max(threatLevel * FastExpNegPade13(-ThreatDecayLogK * dt), ambientThreat), 0f, 1f);

                if ((input.Flags & (int)CognitionInputFlags.HasScatterDirection) != 0)
                {
                    control.ScatterDirection = ResolveDominantAxis(input.ScatterDirection, fallbackForward);
                    control.ScatterUntilTime = input.CurrentTime + ScatterDurationSeconds;
                }

                bool isPredator = (input.Flags & (int)CognitionInputFlags.PredatorRole) != 0;
                RetinalLightResult retinalLight = default;
                if (isPredator)
                {
                    retinalLight = ResolveRetinalExposure(slot, in resolvedInput, fallbackForward);
                    resolvedInput.RetinalLightPosition = retinalLight.LightPosition;
                    resolvedInput.RetinalExposure01 = retinalLight.Exposure01;
                    resolvedInput.RetinalBlindState = retinalLight.BlindState;
                    resolvedInput.PlayerLightExposure01 = math.max(resolvedInput.PlayerLightExposure01, retinalLight.Exposure01);
                    resolvedInput.Flags = retinalLight.BlindState != 0
                        ? resolvedInput.Flags | (int)CognitionInputFlags.RetinalBlind
                        : resolvedInput.Flags & ~(int)CognitionInputFlags.RetinalBlind;
                    if (retinalLight.BlindState == 0)
                        control.Flags &= ~(int)CognitionControlFlags.RetinalFlinch;
                }

                if (isPredator && input.DistanceToPlayerSqr > PredatorHeadlessDistanceSqr && retinalLight.BlindState == 0)
                {
                    core.StateFlags = 0u;
                    core.QuantizedDrives = PackDriveChannels(hunger, aggression, fear, threatLevel);
                    core.QuantizedFatigue = PackSingleDrive(fatigue);
                    control.LastPredatorStateCode = (int)PredatorUtilityState.None;
                    Cores[slot] = core;
                    Controls[slot] = control;
                    Outputs[slot] = BuildHeadlessPackedOutput(fallbackForward);
                    ChosenStates[slot] = (byte)PredatorUtilityState.None;
                    return;
                }

                bool canFlee = (input.Flags & (int)CognitionInputFlags.CanFlee) != 0;
                bool hasPlayerTarget = (input.Flags & (int)CognitionInputFlags.HasPlayerTarget) != 0;
                bool hasThreatTarget = (input.Flags & (int)CognitionInputFlags.HasThreatTarget) != 0;
                bool hasApexRivalTarget = (input.Flags & (int)CognitionInputFlags.HasApexRivalTarget) != 0;
                bool hasPreyTarget = (input.Flags & (int)CognitionInputFlags.HasPreyTarget) != 0;
                bool hasScavengeTarget = (input.Flags & (int)CognitionInputFlags.HasScavengeTarget) != 0;
                bool useHomeTerritory = (input.Flags & (int)CognitionInputFlags.UseHomeTerritory) != 0;
                bool isFlocking = (input.Flags & (int)CognitionInputFlags.IsFlocking) != 0;
                bool hasVisualPlayerHint = (input.Flags & (int)CognitionInputFlags.HasVisualPlayerHint) != 0;
                bool isApexPredator = (input.Flags & (int)CognitionInputFlags.IsApexPredator) != 0;
                bool useHighTierSmoothSteering = (input.Flags & (int)CognitionInputFlags.HighTierSmoothSteering) != 0;

                bool playerVisible = hasPlayerTarget && hasVisualPlayerHint && ResolveThreatVisibility(resolvedInput.Position, resolvedInput.PlayerPosition, resolvedInput.ImportanceScore);
                bool threatVisible = hasThreatTarget && ResolveThreatVisibility(resolvedInput.Position, resolvedInput.ThreatPosition, resolvedInput.ImportanceScore);
                bool rivalApexVisible = hasApexRivalTarget &&
                                        isApexPredator &&
                                        ResolveThreatVisibility(resolvedInput.Position, resolvedInput.RivalApexPosition, resolvedInput.ImportanceScore);
                bool preyVisible = (hasPreyTarget || resolvedInput.ClaimedBoidIndex >= 0) && ResolveThreatVisibility(resolvedInput.Position, resolvedInput.PreyPosition, resolvedInput.ImportanceScore);
                bool scavengeVisible = hasScavengeTarget && ResolveThreatVisibility(resolvedInput.Position, resolvedInput.ScavengePosition, resolvedInput.ImportanceScore);
                if (playerVisible)
                    control.LastVisualContactTime = input.CurrentTime;

                TryResolveChemicalGradient(resolvedInput.Position, input.CurrentTime, out _, out float fearPheromoneSignal, out _);
                float rawFear = math.saturate((1f - math.saturate(input.HealthNormalized)) + input.FearPressure01);
                fear = math.max(fear, math.max(rawFear, fearPheromoneSignal * FearPheromoneContagionShare));

                PackedCognitionOutput output = isPredator
                    ? EvaluatePredator(slot, ref core, ref control, in resolvedInput, fallbackForward, canFlee, hasPlayerTarget, playerVisible, threatVisible, rivalApexVisible, preyVisible, scavengeVisible, useHighTierSmoothSteering, aggression, ref hunger, ref fatigue, ref fear, ref threatLevel)
                    : EvaluatePassive(slot, ref control, in resolvedInput, fallbackForward, canFlee, hasPlayerTarget, playerVisible, threatVisible, useHomeTerritory, isFlocking, ref hunger, ref fatigue, ref fear, ref threatLevel);

                core.StateFlags = PackWorldStateFlags((FaunaBrain.AIState)output.LegacyState);
                if (resolvedInput.RetinalBlindState != 0)
                    core.StateFlags |= (uint)FaunaWorldStateFlags.Blind;
                core.QuantizedDrives = PackDriveChannels(hunger, aggression, fear, threatLevel);
                core.QuantizedFatigue = PackSingleDrive(fatigue);
                Cores[slot] = core;
                Controls[slot] = control;
                Outputs[slot] = output;
                ChosenStates[slot] = PackStateCodeToByte(output.StateMask != 0u ? (int)output.StateMask : output.LegacyState);
            }

            private static byte PackStateCodeToByte(int stateCode)
            {
                return (byte)math.clamp(stateCode, 0, byte.MaxValue);
            }

            [StructLayout(LayoutKind.Sequential, Size = 24)]
            private struct RetinalLightResult
            {
                public float Exposure01;
                public float3 LightPosition;
                public byte BlindState;
                public byte Reserved0;
                public byte Reserved1;
                public byte Reserved2;
                public uint ReservedTail;
            }

            [StructLayout(LayoutKind.Sequential, Size = 32)]
            private struct AlphaLeviathanDirective
            {
                public byte Phase;
                public byte Flags;
                public byte OverrideActive;
                public byte FalseChargeStarted;
                public float RingDistanceMeters;
                public float3 TargetPosition;
                public PredatorUtilityState StateMask;
                public byte Reserved0;
                public byte Reserved1;
                public byte Reserved2;
                public byte Reserved3;
                public byte Reserved4;
                public byte Reserved5;
                public byte Reserved6;
                public byte Reserved7;
            }

            private RetinalLightResult ResolveRetinalExposure(int slot, in CognitionInput input, float3 fallbackForward)
            {
                RetinalLightResult result = default;
                result.LightPosition = input.PlayerPosition;
                if (!RetinalExposure.IsCreated || !BlindnessState.IsCreated)
                    return result;

                float exposure = math.saturate(RetinalExposure[slot]);
                byte blindState = BlindnessState[slot];
                float dt = math.clamp(math.max(input.DeltaTime, input.MetabolicDeltaTime), CenterEvaluationIntervalSeconds, RetinalLowTierEvaluationIntervalSeconds);
                if (!RetinalLightSources.IsCreated || RetinalLightCount <= 0)
                {
                    exposure = DecayRetinalExposure(exposure, dt);
                    blindState = exposure <= RetinalBlindRecoveryThreshold ? (byte)0 : blindState;
                    RetinalExposure[slot] = exposure;
                    BlindnessState[slot] = blindState;
                    result.Exposure01 = exposure;
                    result.BlindState = blindState;
                    return result;
                }

                float3 predatorForward = ResolveRsqrtDirection(input.Forward, fallbackForward);
                float bestStimulus = 0f;
                float3 bestLightPosition = input.PlayerPosition;
                bool directGlare = false;
                bool holdGlare = false;
                int count = math.min(RetinalLightCount, RetinalLightSources.Length);
                for (int i = 0; i < count; i++)
                {
                    LightSourceData light = RetinalLightSources[i];
                    if (light.Intensity <= DdaEpsilon || light.RangeSq <= DdaEpsilon)
                        continue;

                    float3 lightPosition = ResolveRuntimePosition(in light.PositionAup, input.FloatingOriginOffset);
                    if (!MathGuard.IsFinite(lightPosition))
                        continue;

                    float3 lightToPredator = input.Position - lightPosition;
                    if (!MathGuard.IsFinite(lightToPredator))
                        continue;

                    float distanceSq = math.lengthsq(lightToPredator);
                    if (!MathGuard.IsFinite(distanceSq))
                        continue;

                    if (distanceSq > light.RangeSq || distanceSq <= DdaEpsilon)
                        continue;

                    float invDistance = math.rsqrt(math.max(distanceSq, MathSafetyEpsilon));
                    float3 lightToPredatorDir = lightToPredator * invDistance;
                    float coneDot = math.dot(light.Forward, lightToPredatorDir);
                    if (coneDot < light.SpotOuterCos)
                        continue;

                    float predatorToLightDot = RetinalExposureMath.ResolvePredatorToLightDot(predatorForward, lightToPredatorDir);
                    if (!MathGuard.IsFinite(predatorToLightDot))
                        continue;

                    holdGlare |= RetinalExposureMath.IsHoldingGlare(predatorToLightDot);
                    if (!RetinalExposureMath.IsLookingAtLight(predatorToLightDot))
                        continue;

                    float direct01 = RetinalExposureMath.ResolveDirectGlare01(predatorToLightDot);
                    float distance01 = 1f - math.saturate(distanceSq * math.rcp(math.max(light.RangeSq, 1f)));
                    float stimulus = math.max(0f, light.Intensity) * distance01 * direct01;
                    if (stimulus <= bestStimulus)
                        continue;

                    bestStimulus = stimulus;
                    bestLightPosition = lightPosition;
                    directGlare = true;
                }

                if (directGlare)
                {
                    exposure = math.saturate(exposure + bestStimulus * RetinalExposureRiseScale * dt);
                    result.LightPosition = bestLightPosition;
                }
                else if (!holdGlare)
                {
                    exposure = DecayRetinalExposure(exposure, dt);
                }

                blindState = exposure >= RetinalBlindThreshold
                    ? (byte)1
                    : exposure <= RetinalBlindRecoveryThreshold ? (byte)0 : blindState;
                RetinalExposure[slot] = exposure;
                BlindnessState[slot] = blindState;
                result.Exposure01 = exposure;
                result.BlindState = blindState;
                return result;
            }

            private static float DecayRetinalExposure(float exposure, float dt)
            {
                return math.saturate(exposure * FastExpNegPade13(RetinalExposureDecayPerSecond * math.max(0f, dt)));
            }

            private static float3 ResolveRuntimePosition(in AbsoluteUniversePositionBlit128 positionAup, double3 floatingOriginOffset)
            {
                double cellSize = AbsoluteUniversePosition.CellSizeMeters;
                double3 absolutePosition = new double3(
                    (positionAup.GridX * cellSize) + positionAup.Local.x,
                    (positionAup.GridY * cellSize) + positionAup.Local.y,
                    (positionAup.GridZ * cellSize) + positionAup.Local.z);
                double3 runtimePosition = absolutePosition - floatingOriginOffset;
                return new float3((float)runtimePosition.x, (float)runtimePosition.y, (float)runtimePosition.z);
            }

            private PackedCognitionOutput EvaluatePredator(
                int slot,
                ref CognitionCore core,
                ref CognitionControl control,
                in CognitionInput input,
                float3 fallbackForward,
                bool canFlee,
                bool hasPlayerTarget,
                bool playerVisible,
                bool threatVisible,
                bool rivalApexVisible,
                bool preyVisible,
                bool scavengeVisible,
                bool useHighTierSmoothSteering,
                float aggression,
                ref float hunger,
                ref float fatigue,
                ref float fear,
                ref float threatLevel)
            {
                bool hasClaimedBoid = input.ClaimedBoidIndex >= 0;
                float3 resolvedPreyPosition = hasClaimedBoid ? ClaimedBoidPositions[slot] : input.PreyPosition;
                bool resolvedPreyVisible = preyVisible;
                float3 sharedPackPlayerPosition = PredatorPackSharedPlayerPositions[slot];
                bool hasPackTarget = (input.Flags & (int)CognitionInputFlags.HasPackTarget) != 0;
                float3 speciesSharedTarget = sharedPackPlayerPosition;
                bool hasSpeciesSharedTarget = !hasPackTarget &&
                                              TryReadPredatorSpeciesTarget(input.SpeciesId, out speciesSharedTarget);
                sharedPackPlayerPosition = math.select(sharedPackPlayerPosition, speciesSharedTarget, hasSpeciesSharedTarget);
                float3 predictedPackTargetPosition = hasPackTarget
                    ? ResolvePredictedPackTargetIntercept(input)
                    : sharedPackPlayerPosition;
                float3 predictedPlayerPosition = hasPlayerTarget
                    ? ResolvePredictedPlayerIntercept(input)
                    : predictedPackTargetPosition;
                float directAcousticScore = hasPlayerTarget
                    ? ComputeAcousticScore(input.Position, input.PlayerPosition, input.AcousticPingStrength01, input.AcousticTransmission01)
                    : 0f;
                float3 acousticSightDelta = input.PlayerPosition - input.Position;
                bool acousticSight = hasPlayerTarget &&
                                      input.AcousticPingStrength01 > PredatorAcousticSightNoiseThreshold01 &&
                                      math.lengthsq(acousticSightDelta) < PredatorAcousticSightRangeSqr;
                if (acousticSight)
                    directAcousticScore = math.max(directAcousticScore, input.AcousticPingStrength01);

                bool playerSeen = playerVisible || acousticSight;
                float packFlankWeight = PredatorPackWeights[slot];
                float3 packFlankTarget = PredatorPackTargets[slot];
                float3 packBaitPosition = PredatorPackBaitPositions[slot];
                PredatorPackRole packRole = (PredatorPackRole)PredatorPackRoles[slot];
                if (hasSpeciesSharedTarget &&
                    packRole == PredatorPackRole.None &&
                    input.PackFlankDistance > DdaEpsilon)
                {
                    float3 targetForward = ResolveDominantAxis(input.PlayerForward, fallbackForward);
                    float3 packRight = ResolveDominantAxis(
                        math.cross(new float3(0f, 1f, 0f), targetForward),
                        math.cross(new float3(0f, 0f, 1f), targetForward));
                    float sideSign = (slot & 1) == 0 ? 1f : -1f;
                    packFlankTarget = sharedPackPlayerPosition + (packRight * input.PackFlankDistance * sideSign);
                    packBaitPosition = sharedPackPlayerPosition;
                    packFlankWeight = math.max(packFlankWeight, 0.65f);
                    packRole = PredatorPackRole.Flanker;
                }

                bool hasAcousticMemory = TryResolveStrongestAcousticMemory(slot, input.Position, input.CurrentTime, out float3 acousticMemoryPosition, out float acousticMemoryScore);
                float acousticScore = math.max(directAcousticScore, acousticMemoryScore);
                bool hasScavengeTarget = (input.Flags & (int)CognitionInputFlags.HasScavengeTarget) != 0;
                bool isApexPredator = (input.Flags & (int)CognitionInputFlags.IsApexPredator) != 0;
                bool useAlphaLeviathanCognition = (input.Flags & (int)CognitionInputFlags.UseAlphaLeviathanCognition) != 0;
                bool isAmbusher = (input.Flags & (int)CognitionInputFlags.IsAmbusher) != 0;
                bool hasApexRivalTarget = (input.Flags & (int)CognitionInputFlags.HasApexRivalTarget) != 0;
                bool hasChemicalTrail = TryResolveChemicalGradient(input.Position, input.CurrentTime, out float attractantSignal, out float fearPheromoneSignal, out float3 scentGradient);
                bool retinalBlindActive = (input.Flags & (int)CognitionInputFlags.RetinalBlind) != 0 || input.RetinalBlindState != 0;
                bool retinalFrenzyActive = retinalBlindActive && IsLightFrenzyActive(input);
                bool lightAversionActive = IsLightAversionActive(input) || (retinalBlindActive && !retinalFrenzyActive);
                bool lightFrenzyActive = (IsLightFrenzyActive(input) || retinalFrenzyActive) && hasPlayerTarget;
                if (retinalFrenzyActive)
                {
                    aggression = 1f;
                    threatLevel = math.max(threatLevel, input.PlayerLightExposure01);
                }

                if (lightAversionActive)
                {
                    control.OverrideThreatPosition = retinalBlindActive ? input.RetinalLightPosition : input.PlayerPosition;
                    control.OverrideUntilTime = math.max(control.OverrideUntilTime, input.CurrentTime + RetinalBlindHoldSeconds);
                    control.Flags |= (int)CognitionControlFlags.HasOverrideThreatPosition | (int)CognitionControlFlags.RetinalFlinch;
                    fear = math.max(fear, input.PlayerLightExposure01 * math.max(input.LightReactionFearBoost01, 0.1f));
                    threatLevel = math.max(threatLevel, input.PlayerLightExposure01);
                }
                else
                {
                    control.Flags &= ~(int)CognitionControlFlags.RetinalFlinch;
                }

                bool hasBaseSiegeTarget = TryResolveBaseSiegeTarget(
                    slot,
                    in input,
                    fallbackForward,
                    predictedPlayerPosition,
                    (isApexPredator || input.PackCoordinationRadius > DdaEpsilon) &&
                    !playerSeen &&
                    !resolvedPreyVisible &&
                    !scavengeVisible &&
                    !rivalApexVisible,
                    out float3 baseSiegeTarget,
                    out BaseSiegeRole baseSiegeRole,
                    out float baseSiegeScore);
                float chemicalScore = ComputeChemicalScore(
                    slot,
                    input.Position,
                    input.ScavengePosition,
                    hasScavengeTarget,
                    input.ChemicalSignal01,
                    input.ChemicalSensitivity,
                    input.CurrentTime);
                chemicalScore = math.max(chemicalScore, attractantSignal);
                fear = math.max(fear, fearPheromoneSignal * FearPheromoneContagionShare);

                float3 targetPosition = input.Position + (fallbackForward * 4f);
                bool hasTarget = false;
                bool usingAmbushPoint = false;
                if (lightFrenzyActive)
                {
                    targetPosition = predictedPlayerPosition;
                    hasTarget = true;
                    threatLevel = math.max(threatLevel, input.PlayerLightExposure01);
                }
                else if (isApexPredator && rivalApexVisible)
                {
                    targetPosition = input.RivalApexPosition;
                    hasTarget = true;
                }
                else if (scavengeVisible)
                {
                    targetPosition = input.ScavengePosition;
                    hasTarget = true;
                }
                else if (playerSeen)
                {
                    targetPosition = predictedPlayerPosition;
                    hasTarget = true;
                    threatLevel = math.max(threatLevel, directAcousticScore);
                }
                else if (hasSpeciesSharedTarget)
                {
                    targetPosition = predictedPackTargetPosition;
                    hasTarget = true;
                    threatLevel = math.max(threatLevel, 0.2f);
                }
                else if (resolvedPreyVisible)
                {
                    targetPosition = resolvedPreyPosition;
                    hasTarget = true;
                }
                else if (hasBaseSiegeTarget)
                {
                    targetPosition = baseSiegeTarget;
                    hasTarget = true;
                    threatLevel = math.max(threatLevel, baseSiegeScore * BaseSiegeUtilityBias);
                }
                else if (hasPlayerTarget && directAcousticScore > AcousticStimulusThreshold)
                {
                    targetPosition = predictedPlayerPosition;
                    hasTarget = true;
                }
                else if (hasAcousticMemory && acousticMemoryScore > AcousticStimulusThreshold)
                {
                    targetPosition = acousticMemoryPosition;
                    hasTarget = true;
                    threatLevel = math.max(threatLevel, acousticMemoryScore);
                }
                else if (hasChemicalTrail && chemicalScore > PredatorScentFollowThreshold && math.lengthsq(scentGradient) > DdaEpsilon)
                {
                    targetPosition = input.Position + (ResolveDominantAxis(scentGradient, fallbackForward) * math.max(1f, ChemicalBreadcrumbFollowStepMeters));
                    hasTarget = true;
                    threatLevel = math.max(threatLevel, chemicalScore * 0.65f);
                }
                else if (hasScavengeTarget && chemicalScore > ChemicalStimulusThreshold)
                {
                    targetPosition = input.ScavengePosition;
                    hasTarget = true;
                }
                else if (isAmbusher && TryResolveAmbushCreviceTarget(input.Position, fallbackForward, out float3 ambushPosition))
                {
                    targetPosition = ambushPosition;
                    hasTarget = true;
                    usingAmbushPoint = true;
                }
                else if (TryResolveSpatialMemoryBankTarget(slot, input.CurrentTime, out float3 memoryPosition, out float memoryThreatWeight))
                {
                    targetPosition = memoryPosition;
                    hasTarget = true;
                    threatLevel = math.max(threatLevel, memoryThreatWeight);
                }
                else
                {
                    float wanderRadius = math.max(1f, math.select(input.WanderRadius, input.PatrolRadius, (input.Flags & (int)CognitionInputFlags.UseHomeTerritory) != 0));
                    float3 wanderCenter = (input.Flags & (int)CognitionInputFlags.UseHomeTerritory) != 0
                        ? control.SpawnAnchor
                        : input.Position;
                    RefreshWanderTarget(ref control, input.CurrentTime, wanderCenter, wanderRadius);
                    if ((control.Flags & (int)CognitionControlFlags.HasWanderTarget) != 0)
                    {
                        targetPosition = control.WanderTarget;
                        hasTarget = true;
                    }
                }

                float packCommitDistance = math.max(input.PackCommitDistance, input.AttackRange * 1.25f);
                bool usePackFlank = packFlankWeight > DdaEpsilon &&
                                    packRole == PredatorPackRole.Flanker &&
                                    math.lengthsq(sharedPackPlayerPosition - input.Position) > DdaEpsilon &&
                                    math.lengthsq(predictedPlayerPosition - input.Position) > packCommitDistance * packCommitDistance &&
                                    !scavengeVisible &&
                                    (!resolvedPreyVisible || hasPackTarget);
                bool playerFacingBait = false;
                bool flankingManeuverDetected = false;
                if (usePackFlank)
                {
                    float3 playerToBait = ResolveDominantAxis(packBaitPosition - predictedPlayerPosition, float3.zero);
                    float3 playerForward = ResolveDominantAxis(input.PlayerForward, ResolveDominantAxis(input.PackTargetVelocity, float3.zero));
                    playerFacingBait = math.lengthsq(playerForward) > DdaEpsilon &&
                                       math.lengthsq(playerToBait) > DdaEpsilon &&
                                       math.dot(playerForward, playerToBait) >= PlayerFacingBaitThreshold;
                    float3 basePackPosition = hasPackTarget
                        ? input.PackTargetPosition
                        : hasSpeciesSharedTarget ? sharedPackPlayerPosition : input.PlayerPosition;
                    float3 packPredictionDelta = predictedPlayerPosition - basePackPosition;
                    float3 flankCandidate = packFlankTarget + packPredictionDelta;
                    if (IsThreatVoxelSolidOrOutOfBounds(flankCandidate))
                    {
                        targetPosition = predictedPlayerPosition;
                    }
                    else
                    {
                        targetPosition = math.lerp(predictedPlayerPosition, flankCandidate, math.saturate(packFlankWeight));
                        flankingManeuverDetected = true;
                    }

                    hasTarget = true;
                }

                AlphaLeviathanDirective alphaDirective = default;
                bool alphaOverrideActive = false;
                if (useAlphaLeviathanCognition && hasPlayerTarget && !rivalApexVisible)
                {
                    alphaDirective = ResolveAlphaLeviathanDirective(
                        slot,
                        in input,
                        fallbackForward,
                        hasPlayerTarget,
                        predictedPlayerPosition,
                        retinalBlindActive,
                        useHighTierSmoothSteering);
                    if (alphaDirective.OverrideActive != 0)
                    {
                        alphaOverrideActive = true;
                        targetPosition = alphaDirective.TargetPosition;
                        hasTarget = true;
                        usingAmbushPoint = false;
                        threatLevel = math.max(threatLevel, math.max(directAcousticScore, 0.35f));
                    }
                }

                float threatVisual = 0f;
                if (isApexPredator && rivalApexVisible)
                    threatVisual = ComputeThreatVisual(input.Position, input.RivalApexPosition, fallbackForward, math.max(input.AttackRange, input.ApexTerritoryRadius * 0.2f));
                else if (playerSeen)
                    threatVisual = ComputeThreatVisual(input.Position, predictedPlayerPosition, fallbackForward, input.AttackRange);
                else if (resolvedPreyVisible)
                    threatVisual = ComputeThreatVisual(input.Position, resolvedPreyPosition, fallbackForward, input.AttackRange * 1.5f) * 0.8f;
                else if (scavengeVisible)
                    threatVisual = ComputeThreatVisual(input.Position, input.ScavengePosition, fallbackForward, input.AttackRange * 2f) * 0.65f;
                else if (threatVisible)
                    threatVisual = ComputeThreatVisual(input.Position, input.ThreatPosition, fallbackForward, input.AttackRange * 1.5f) * 0.75f;

                float threatBlend = ResolveThreatBlend(input.DeltaTime);
                float threatRaw = math.max(math.max(threatVisual, acousticScore), threatLevel);
                threatLevel = math.lerp(threatLevel, threatRaw, threatBlend);
                threatLevel = math.clamp(threatLevel, 0f, 1f);
                fear = math.max(fear, ScoreThreat(threatRaw) * 0.35f);
                if (isApexPredator && hasApexRivalTarget)
                    threatLevel = math.max(threatLevel, math.saturate(input.ApexAggressionMultiplier * 0.55f));

                float hungerScore = ScoreHunger(hunger) * math.max(0.1f, input.HungerWeight);
                float fatigueScore = ScoreFatigue(fatigue);
                float curiosityWeight = math.max(0.1f, input.CuriosityWeight);
                float fearScore = canFlee
                    ? ScoreFear(math.max(fear, threatRaw * 0.45f)) * math.max(0.1f, input.FearWeight)
                    : 0f;
                float threatScore = ScoreThreat(threatLevel) * math.max(0.1f, input.ThreatWeight);
                float acousticUtility = ScoreThreat(acousticScore) * curiosityWeight;
                float chemicalUtility = ScoreThreat(chemicalScore) * math.max(0.1f, input.HungerWeight) * curiosityWeight;
                float baseSiegeUtility = ScoreThreat(baseSiegeScore) * math.select(0f, 1f, hasBaseSiegeTarget);
                float lightFrenzyUtility = ScoreThreat(input.PlayerLightExposure01) *
                                           math.select(0f, math.max(1f, input.LightFrenzySpeedMultiplier), lightFrenzyActive);
                float targetDistanceSq = math.lengthsq(targetPosition - input.Position);
                float attackRangeSq = math.max(input.AttackRange * input.AttackRange, 1f);
                bool holdingAmbushPoint = usingAmbushPoint &&
                                          targetDistanceSq <= AmbushHoldDistanceMeters * AmbushHoldDistanceMeters &&
                                          math.lengthsq(predictedPlayerPosition - input.Position) > AmbushThreatWakeDistanceMeters * AmbushThreatWakeDistanceMeters;
                float attackCommit01 = hasTarget
                    ? ScoreHunger(math.saturate(1f - (targetDistanceSq * math.rcp(math.max(attackRangeSq, MathSafetyEpsilon)))))
                    : 0f;

                bool overrideActive = control.OverrideUntilTime > input.CurrentTime;
                bool satedActive = control.SatedUntilTime > input.CurrentTime;
                float aggressionWeight = math.max(0.1f, aggression);
                float satedSuppression = math.select(1f, 0.05f, satedActive);
                float targetSignal = math.saturate(math.max(math.max(threatScore, baseSiegeUtility), math.max(acousticUtility, chemicalUtility)));
                float huntUtility = EvaluateHuntUtility(
                    hungerScore,
                    fearScore,
                    aggressionWeight,
                    targetSignal,
                    attackCommit01,
                    satedSuppression);
                float prowlingScore = math.max(
                    MinimumScoreThreshold,
                    EvaluatePatrolUtility(
                        hungerScore,
                        fearScore,
                        threatScore,
                        fatigueScore,
                        satedActive)) * math.lerp(0.85f, 1.25f, math.saturate(curiosityWeight * 0.5f));
                float stalkingScore = huntUtility * math.lerp(0.55f, 0.95f, ScoreThreat(targetSignal));
                float attackingScore = huntUtility *
                                       math.lerp(0.25f, 1f, SmoothStep01(attackCommit01)) *
                                       AttackStateBias *
                                       math.select(0.25f, 1f, hasTarget);
                if (hasBaseSiegeTarget)
                {
                    stalkingScore += baseSiegeUtility * BaseSiegeUtilityBias;
                    attackingScore += baseSiegeUtility * math.select(0.2f, 0.55f, baseSiegeRole == BaseSiegeRole.Rammer);
                }
                if (lightFrenzyActive)
                {
                    stalkingScore += lightFrenzyUtility;
                    attackingScore += lightFrenzyUtility * 1.35f;
                }

                if (isAmbusher)
                {
                    stalkingScore *= math.select(1.35f, 0.9f, playerSeen);
                    attackingScore *= math.select(0.35f, 1.15f, playerSeen);
                }
                float fleeingScore = canFlee
                    ? EvaluateFleeUtility(
                        fearScore,
                        threatScore,
                        1f - math.saturate(input.HealthNormalized))
                    : 0f;
                fleeingScore += math.select(0f, OverrideScoreBias * math.max(0.35f, input.PlayerLightExposure01), canFlee && lightAversionActive);
                float fleeHealthThreshold = math.clamp(
                    input.FleeHealthThreshold > 0f ? input.FleeHealthThreshold : PassiveLowHealthThreshold,
                    0.05f,
                    1f);
                if (isApexPredator && hasApexRivalTarget)
                {
                    float rivalAggressionScale = math.max(1f, input.ApexAggressionMultiplier);
                    stalkingScore *= rivalAggressionScale;
                    attackingScore *= math.lerp(1f, rivalAggressionScale, 0.85f);
                    if (input.HealthNormalized <= fleeHealthThreshold)
                    {
                        control.OverrideThreatPosition = input.RivalApexPosition;
                        control.Flags |= (int)CognitionControlFlags.HasOverrideThreatPosition;
                        fleeingScore += OverrideScoreBias * 0.95f;
                    }
                }

                prowlingScore += math.select(0f, OverrideScoreBias, overrideActive && control.OverrideStateFlags == (uint)PredatorUtilityState.Prowling);
                stalkingScore += math.select(0f, OverrideScoreBias, overrideActive && control.OverrideStateFlags == (uint)PredatorUtilityState.Stalking);
                attackingScore += math.select(0f, OverrideScoreBias, overrideActive && control.OverrideStateFlags == (uint)PredatorUtilityState.Attacking);
                fleeingScore += math.select(0f, OverrideScoreBias, overrideActive && control.OverrideStateFlags == (uint)PredatorUtilityState.Fleeing);
                prowlingScore += math.select(0f, OverrideScoreBias, satedActive);

                float4 stateScores = new float4(
                    SquareActionScore(prowlingScore),
                    SquareActionScore(stalkingScore),
                    SquareActionScore(attackingScore),
                    SquareActionScore(fleeingScore));
                float winningScore = math.cmax(stateScores);
                int winningMask = BuildWinningStateMask(stateScores, winningScore);
                int stateCode = DecodePredatorStateCode(winningMask);
                stateCode = math.select(stateCode, (int)PredatorUtilityState.Prowling, winningScore < MinimumScoreThreshold);
                PredatorUtilityState stateMask = (PredatorUtilityState)stateCode;
                bool wasHunting = control.LastPredatorStateCode == (int)PredatorUtilityState.Stalking ||
                                  control.LastPredatorStateCode == (int)PredatorUtilityState.Attacking;
                if (alphaOverrideActive)
                {
                    stateMask = alphaDirective.StateMask;
                    stateCode = (int)stateMask;
                }

                bool wantsBoidClaim =
                    stateMask == PredatorUtilityState.Attacking &&
                    input.ClaimedBoidIndex >= 0 &&
                    !playerSeen &&
                    !scavengeVisible;
                if (packRole == PredatorPackRole.Flanker && flankingManeuverDetected)
                {
                    float flankDistanceSq = math.lengthsq(targetPosition - input.Position);
                    bool holdingBlindSpot = flankDistanceSq <= PackFlankHoldDistanceMeters * PackFlankHoldDistanceMeters;
                    if (!playerFacingBait && holdingBlindSpot)
                    {
                        stateMask = PredatorUtilityState.Stalking;
                        stateCode = (int)stateMask;
                        wantsBoidClaim = false;
                    }
                }
                if (wantsBoidClaim)
                {
                    bool claimSucceeded = TryClaimBoid(slot, input.ClaimedBoidIndex);
                    if (claimSucceeded)
                    {
                        core.ClaimedBoidIndex = input.ClaimedBoidIndex;
                        resolvedPreyPosition = ClaimedBoidPositions[slot];
                        targetPosition = resolvedPreyPosition;
                    }
                    else
                    {
                        stateMask = PredatorUtilityState.Stalking;
                        stateCode = (int)stateMask;
                    }
                }

                float3 desiredDirection = ResolvePredatorDirection(slot, stateMask, input.Position, targetPosition, fallbackForward, input.CurrentTime, control, isApexPredator, useHighTierSmoothSteering);
                PackedCognitionOutput output = default;
                output.DesiredDirection = desiredDirection;
                output.PackedScores = PackScoreTriplet(hungerScore, aggressionWeight, fearScore);
                output.StateMask = (uint)stateMask;
                output.LegacyState = satedActive ? (int)FaunaBrain.AIState.Sated : (int)MapPredatorState(stateMask);
                output.ForceMultiplier = 1f;
                output.SpeedMultiplier = 1f;
                output.TurnMultiplier = 1f;
                output.OutputFlags = 0u;
                if (retinalBlindActive)
                    output.OutputFlags |= (uint)CognitionOutputFlags.RetinalBlind;
                bool isHunting = stateMask == PredatorUtilityState.Stalking || stateMask == PredatorUtilityState.Attacking;
                if (isHunting && !wasHunting)
                    output.OutputFlags |= (uint)CognitionOutputFlags.EmitThreatPulse;

                switch (stateMask)
                {
                    case PredatorUtilityState.Prowling:
                        output.ForceMultiplier = 1.05f;
                        output.SpeedMultiplier = satedActive ? 0.6f : 0.95f;
                        output.TurnMultiplier = satedActive ? 0.5f : 0.9f;
                        break;
                    case PredatorUtilityState.Stalking:
                        output.ForceMultiplier = 1.35f;
                        output.SpeedMultiplier = 1.15f;
                        output.TurnMultiplier = 1.1f;
                        break;
                    case PredatorUtilityState.Attacking:
                        output.ForceMultiplier = 2.15f;
                        output.SpeedMultiplier = math.max(1.15f, aggression);
                        output.TurnMultiplier = 1.2f;
                        bool canStrikeLiveTarget = (playerSeen || lightFrenzyActive || resolvedPreyVisible || scavengeVisible || rivalApexVisible) &&
                                                   targetDistanceSq <= math.max(1f, input.AttackRange * input.AttackRange);
                        float siegeStrikeRange = math.max(1f, input.AttackRange + BaseSiegeRammerStandoffMeters);
                        bool canStrikeBaseTarget = hasBaseSiegeTarget &&
                                                   baseSiegeRole == BaseSiegeRole.Rammer &&
                                                   targetDistanceSq <= siegeStrikeRange * siegeStrikeRange;
                        bool canStrike = (canStrikeLiveTarget || canStrikeBaseTarget) &&
                                         input.CurrentTime >= control.NextAttackAllowedTime;
                        if (packRole == PredatorPackRole.Flanker && flankingManeuverDetected)
                            canStrike &= playerFacingBait;

                        output.OutputFlags |= canStrike
                            ? (uint)CognitionOutputFlags.ShouldAttack
                            : 0u;
                        break;
                    case PredatorUtilityState.Fleeing:
                        output.ForceMultiplier = 2.4f;
                        output.SpeedMultiplier = math.max(1.2f, input.FearWeight);
                        output.TurnMultiplier = 1.15f;
                        break;
                }

                if (lightFrenzyActive && (stateMask == PredatorUtilityState.Stalking || stateMask == PredatorUtilityState.Attacking))
                {
                    output.ForceMultiplier = math.max(output.ForceMultiplier, input.LightFrenzySpeedMultiplier);
                    output.SpeedMultiplier = math.max(output.SpeedMultiplier, input.LightFrenzySpeedMultiplier);
                    output.TurnMultiplier = math.max(output.TurnMultiplier, 1.25f);
                }

                if (holdingAmbushPoint && !playerSeen)
                {
                    output.ForceMultiplier = 0f;
                    output.SpeedMultiplier = 0f;
                    output.TurnMultiplier = 0.35f;
                }

                if (hunger > HungerMobilityPenaltyThreshold01)
                    output.SpeedMultiplier *= HungerMobilityPenaltySpeedScale;

                if (alphaOverrideActive)
                {
                    output.OutputFlags &= ~((uint)CognitionOutputFlags.ShouldAttack | (uint)CognitionOutputFlags.EmitThreatPulse);
                    switch (alphaDirective.Phase)
                    {
                        case AlphaLeviathanPhase.FalseCharge:
                            output.LegacyState = (int)FaunaBrain.AIState.Feint;
                            output.ForceMultiplier = math.max(output.ForceMultiplier, 2.85f);
                            output.SpeedMultiplier = math.max(
                                output.SpeedMultiplier,
                                AlphaFalseChargeSpeedMetersPerSecond * math.rcp(math.max(0.1f, input.BaseMaxSpeedMetersPerSecond)));
                            output.TurnMultiplier = math.max(output.TurnMultiplier, 1.35f);
                            if (alphaDirective.FalseChargeStarted != 0)
                                output.OutputFlags |= (uint)CognitionOutputFlags.EmitThreatPulse;
                            break;
                        case AlphaLeviathanPhase.Hidden:
                            output.LegacyState = (int)FaunaBrain.AIState.Retreat;
                            output.ForceMultiplier = math.max(output.ForceMultiplier, 2.2f);
                            output.SpeedMultiplier = math.max(output.SpeedMultiplier, 1.35f);
                            output.TurnMultiplier = math.max(output.TurnMultiplier, 1.2f);
                            break;
                        case AlphaLeviathanPhase.VeerOff:
                            output.LegacyState = (int)FaunaBrain.AIState.Feint;
                            output.ForceMultiplier = math.max(output.ForceMultiplier, 2.4f);
                            output.SpeedMultiplier = math.max(output.SpeedMultiplier, 1.6f);
                            output.TurnMultiplier = math.max(output.TurnMultiplier, 1.35f);
                            break;
                        default:
                            output.LegacyState = (int)FaunaBrain.AIState.Stalk;
                            output.ForceMultiplier = math.max(output.ForceMultiplier, 1.35f);
                            output.SpeedMultiplier = math.max(output.SpeedMultiplier, 1.05f);
                            output.TurnMultiplier = math.max(output.TurnMultiplier, 1.1f);
                            break;
                    }
                }

                if (packRole == PredatorPackRole.Bait)
                    output.OutputFlags |= (uint)CognitionOutputFlags.PackRoleBait;
                else if (packRole == PredatorPackRole.Flanker)
                    output.OutputFlags |= (uint)CognitionOutputFlags.PackRoleFlanker;

                if (baseSiegeRole == BaseSiegeRole.Rammer)
                    output.OutputFlags |= (uint)CognitionOutputFlags.BaseSiegeRammer;
                else if (baseSiegeRole == BaseSiegeRole.Distractor)
                    output.OutputFlags |= (uint)CognitionOutputFlags.BaseSiegeDistractor;
                else if (baseSiegeRole == BaseSiegeRole.Loiterer)
                    output.OutputFlags |= (uint)CognitionOutputFlags.BaseSiegeLoiterer;

                if (flankingManeuverDetected)
                    output.OutputFlags |= (uint)CognitionOutputFlags.FlankingManeuverDetected;

                control.LastPredatorStateCode = (int)stateMask;
                return output;
            }

            private AlphaLeviathanDirective ResolveAlphaLeviathanDirective(
                int slot,
                in CognitionInput input,
                float3 fallbackForward,
                bool hasPlayerTarget,
                float3 predictedPlayerPosition,
                bool retinalBlindActive,
                bool useHighTierSmoothSteering)
            {
                AlphaLeviathanDirective directive = default;
                directive.Phase = AlphaLeviathanPhase.Hidden;
                directive.RingDistanceMeters = math.max(
                    AlphaFalseChargeVeerDistanceMeters + 5f,
                    math.max(input.FogEndDistanceMeters, AlphaFogFallbackEndMeters) - AlphaFogSilhouetteOffsetMeters);
                if (!hasPlayerTarget || !StalkingPhases.IsCreated || !StalkingPhaseStartTimes.IsCreated)
                    return directive;

                float3 playerPosition = ResolveRuntimePosition(in input.PlayerTargetAup, input.FloatingOriginOffset);
                if (!MathGuard.IsFinite(playerPosition) || math.lengthsq(playerPosition - input.Position) <= DdaEpsilon)
                    playerPosition = predictedPlayerPosition;

                float3 playerToSelf = input.Position - playerPosition;
                float distanceSq = math.lengthsq(playerToSelf);
                float3 awayFromPlayer = ResolveRsqrtDirection(playerToSelf, -fallbackForward);
                float currentDistance = distanceSq > DdaEpsilon
                    ? distanceSq * math.rsqrt(math.max(distanceSq, MathSafetyEpsilon))
                    : 0f;
                float3 playerForward = ResolveRsqrtDirection(input.PlayerForward, -awayFromPlayer);
                float playerLookDot = math.dot(playerForward, awayFromPlayer);
                bool playerGazeBreak = playerLookDot >= AlphaPlayerGazeDotThreshold;
                bool retinalBreak = retinalBlindActive || input.RetinalExposure01 >= AlphaRetinalDiveThreshold;

                byte priorPhase = StalkingPhases[slot];
                if (priorPhase > AlphaLeviathanPhase.VeerOff)
                    priorPhase = AlphaLeviathanPhase.Hidden;

                float startTime = StalkingPhaseStartTimes[slot];
                float phaseAge = startTime > DdaEpsilon ? math.max(0f, input.CurrentTime - startTime) : 0f;
                byte phase = priorPhase;
                if (playerGazeBreak || retinalBreak)
                {
                    phase = AlphaLeviathanPhase.Hidden;
                }
                else if (priorPhase == AlphaLeviathanPhase.Hidden &&
                         startTime > DdaEpsilon &&
                         phaseAge < AlphaHiddenHoldSeconds)
                {
                    phase = AlphaLeviathanPhase.Hidden;
                }
                else if (priorPhase == AlphaLeviathanPhase.Hidden)
                {
                    phase = AlphaLeviathanPhase.Circling;
                }
                else if (priorPhase == AlphaLeviathanPhase.Circling)
                {
                    float chargeWindow = directive.RingDistanceMeters + 12f;
                    if (phaseAge >= AlphaCirclingHoldSeconds && currentDistance <= chargeWindow)
                        phase = AlphaLeviathanPhase.FalseCharge;
                }
                else if (priorPhase == AlphaLeviathanPhase.FalseCharge)
                {
                    if (currentDistance <= AlphaFalseChargeVeerDistanceMeters || phaseAge >= AlphaFalseChargeMaxSeconds)
                        phase = AlphaLeviathanPhase.VeerOff;
                }
                else if (priorPhase == AlphaLeviathanPhase.VeerOff &&
                         phaseAge >= AlphaVeerHoldSeconds)
                {
                    phase = AlphaLeviathanPhase.Circling;
                }

                bool phaseChanged = phase != priorPhase || startTime <= DdaEpsilon;
                if (phaseChanged)
                    StalkingPhaseStartTimes[slot] = input.CurrentTime;
                StalkingPhases[slot] = phase;

                byte flags = 0;
                if (!useHighTierSmoothSteering)
                    flags |= AlphaLeviathanTelemetryFlags.LowTierRadialFallback;
                if (playerGazeBreak)
                    flags |= AlphaLeviathanTelemetryFlags.PlayerGazeBreak;

                directive.OverrideActive = 1;
                directive.Phase = phase;
                directive.FalseChargeStarted = (byte)(phaseChanged && phase == AlphaLeviathanPhase.FalseCharge ? 1 : 0);
                directive.StateMask = PredatorUtilityState.Stalking;
                switch (phase)
                {
                    case AlphaLeviathanPhase.Hidden:
                    {
                        float3 diveDirection = ResolveAlphaDiveDirection(
                            in input,
                            awayFromPlayer,
                            fallbackForward,
                            useHighTierSmoothSteering,
                            ref flags);
                        directive.TargetPosition = input.Position + (diveDirection * AlphaDiveDepthMeters);
                        directive.StateMask = PredatorUtilityState.Fleeing;
                        break;
                    }
                    case AlphaLeviathanPhase.FalseCharge:
                        directive.TargetPosition = predictedPlayerPosition;
                        directive.StateMask = PredatorUtilityState.Attacking;
                        break;
                    case AlphaLeviathanPhase.VeerOff:
                    {
                        float3 veerDirection = ResolveAlphaVeerDirection(awayFromPlayer, fallbackForward);
                        directive.TargetPosition = input.Position + (veerDirection * AlphaVeerDistanceMeters);
                        directive.StateMask = PredatorUtilityState.Fleeing;
                        break;
                    }
                    default:
                    {
                        float3 circleDirection = ResolveAlphaCircleDirection(
                            slot,
                            in input,
                            playerPosition,
                            awayFromPlayer,
                            directive.RingDistanceMeters,
                            useHighTierSmoothSteering);
                        directive.TargetPosition = input.Position + (circleDirection * math.max(8f, directive.RingDistanceMeters * 0.25f));
                        directive.StateMask = PredatorUtilityState.Stalking;
                        break;
                    }
                }

                directive.Flags = flags;
                return directive;
            }

            private float3 ResolveAlphaCircleDirection(
                int slot,
                in CognitionInput input,
                float3 playerPosition,
                float3 awayFromPlayer,
                float ringDistanceMeters,
                bool useHighTierSmoothSteering)
            {
                float3 up = new float3(0f, 1f, 0f);
                float3 tangent = ResolveRsqrtDirection(math.cross(up, awayFromPlayer), math.cross(new float3(0f, 0f, 1f), awayFromPlayer));
                tangent = math.select(tangent, -tangent, (slot & 1) != 0);
                float distanceSq = math.lengthsq(input.Position - playerPosition);
                float currentDistance = distanceSq > DdaEpsilon
                    ? distanceSq * math.rsqrt(math.max(distanceSq, MathSafetyEpsilon))
                    : ringDistanceMeters;
                float radialCorrection = math.clamp((ringDistanceMeters - currentDistance) * AlphaRingCorrectionScale, -1f, 1f);
                float3 desired = tangent + (awayFromPlayer * radialCorrection);
                return ResolveSteeringAxis(desired, tangent, useHighTierSmoothSteering);
            }

            private float3 ResolveAlphaDiveDirection(
                in CognitionInput input,
                float3 awayFromPlayer,
                float3 fallbackForward,
                bool useHighTierSmoothSteering,
                ref byte flags)
            {
                if (!useHighTierSmoothSteering)
                {
                    flags |= AlphaLeviathanTelemetryFlags.LowTierRadialFallback;
                    return ResolveDominantAxis(awayFromPlayer, fallbackForward);
                }

                flags |= AlphaLeviathanTelemetryFlags.SdfDiveRequested;
                float3 down = new float3(0f, -1f, 0f);
                float3 sdfBias = down;
                if (ThreatVoxelGrid.IsCreated && TryResolveThreatVoxelGradient(input.Position, out float3 gradient))
                    sdfBias = ResolveRsqrtDirection(gradient + (down * 1.5f), down);

                return ResolveRsqrtDirection((awayFromPlayer * 0.65f) + (sdfBias * 1.35f), down);
            }

            private static float3 ResolveAlphaVeerDirection(float3 awayFromPlayer, float3 fallbackForward)
            {
                return ResolveRsqrtDirection(awayFromPlayer + new float3(0f, 0.85f, 0f), fallbackForward);
            }

            private float3 ResolvePredictedPlayerIntercept(in CognitionInput input)
            {
                return input.PlayerPosition + (input.PlayerVelocity * PredatorInterceptLeadSeconds);
            }

            private float3 ResolvePredictedPackTargetIntercept(in CognitionInput input)
            {
                return input.PackTargetPosition + (input.PackTargetVelocity * PredatorInterceptLeadSeconds);
            }

            private PackedCognitionOutput EvaluatePassive(
                int slot,
                ref CognitionControl control,
                in CognitionInput input,
                float3 fallbackForward,
                bool canFlee,
                bool hasPlayerTarget,
                bool playerVisible,
                bool threatVisible,
                bool useHomeTerritory,
                bool isFlocking,
                ref float hunger,
                ref float fatigue,
                ref float fear,
                ref float threatLevel)
            {
                float escapeSafeDistance = math.max(input.EscapeSafeDistance, 1f);
                float escapeSafeDistanceSq = escapeSafeDistance * escapeSafeDistance;
                float playerDistanceSq = math.lengthsq(input.PlayerPosition - input.Position);
                float playerThreat = hasPlayerTarget
                    ? math.saturate(1f - (playerDistanceSq * math.rcp(math.max(escapeSafeDistanceSq, MathSafetyEpsilon))))
                    : 0f;
                bool lightAversionActive = IsLightAversionActive(input);
                if (lightAversionActive)
                {
                    control.OverrideThreatPosition = input.PlayerPosition;
                    control.Flags |= (int)CognitionControlFlags.HasOverrideThreatPosition;
                    playerThreat = math.max(playerThreat, input.PlayerLightExposure01);
                    fear = math.max(fear, input.PlayerLightExposure01 * math.max(input.LightReactionFearBoost01, 0.1f));
                }

                float directAcousticScore = hasPlayerTarget
                    ? ComputeAcousticScore(input.Position, input.PlayerPosition, input.AcousticPingStrength01, input.AcousticTransmission01)
                    : 0f;
                bool hasAcousticMemory = TryResolveStrongestAcousticMemory(slot, input.Position, input.CurrentTime, out float3 acousticMemoryPosition, out float acousticMemoryScore);
                float acousticScore = math.max(directAcousticScore, acousticMemoryScore);
                TryResolveChemicalGradient(input.Position, input.CurrentTime, out _, out float fearPheromoneSignal, out _);
                float threatVisual = playerVisible
                    ? ComputeThreatVisual(input.Position, input.PlayerPosition, fallbackForward, escapeSafeDistance)
                    : 0f;
                if (threatVisible)
                    threatVisual = math.max(threatVisual, ComputeThreatVisual(input.Position, input.ThreatPosition, fallbackForward, escapeSafeDistance));

                float threatBlend = ResolveThreatBlend(input.DeltaTime);
                float threatRaw = math.max(math.max(math.max(threatVisual, playerThreat), acousticScore), fearPheromoneSignal * FearPheromoneContagionShare);
                threatLevel = math.lerp(threatLevel, threatRaw, threatBlend);
                threatLevel = math.clamp(threatLevel, 0f, 1f);
                fear = math.max(fear, math.max(ScoreThreat(threatRaw) * 0.45f, fearPheromoneSignal * FearPheromoneContagionShare));

                bool retreatForced = control.OverrideUntilTime > input.CurrentTime;
                bool scatterActive = isFlocking && control.ScatterUntilTime > input.CurrentTime;
                bool satedActive = control.SatedUntilTime > input.CurrentTime;
                float fleeHealthThreshold = math.clamp(
                    input.FleeHealthThreshold > 0f ? input.FleeHealthThreshold : PassiveLowHealthThreshold,
                    0.05f,
                    1f);
                bool lowHealth = input.HealthNormalized <= fleeHealthThreshold;
                float patrolRadius = math.max(input.PatrolRadius, 1f);
                float patrolRadiusSq = patrolRadius * patrolRadius;
                float homeDistanceSq = math.lengthsq(input.Position - control.SpawnAnchor);
                bool homeOutOfBounds = useHomeTerritory &&
                                       input.PatrolRadius > 0f &&
                                       homeDistanceSq > patrolRadiusSq;
                bool shouldEscape = retreatForced ||
                                    (canFlee && lightAversionActive) ||
                                    (canFlee && hasPlayerTarget && (input.DistanceToPlayerSqr <= input.EscapeDistance * input.EscapeDistance || lowHealth || threatLevel >= 0.35f)) ||
                                    (canFlee && (threatVisible || acousticScore > AcousticStimulusThreshold));

                float fatigueScore = ScoreFatigue(fatigue);
                float curiosityWeight = math.max(0.1f, input.CuriosityWeight);
                float escapeScore = ScoreFear(math.max(fear, threatLevel)) * math.max(0.1f, input.FearWeight) * math.select(0f, 1f, shouldEscape);
                float homeDistance01 = useHomeTerritory && input.PatrolRadius > 0f
                    ? math.saturate(homeDistanceSq * math.rcp(math.max(patrolRadiusSq, MathSafetyEpsilon)))
                    : 0f;
                float returnScore = ScoreThreat(homeDistance01) * math.select(0f, 1f, homeOutOfBounds && !shouldEscape && !satedActive);
                float scatterScore = math.select(0f, OverrideScoreBias + ScoreThreat(math.max(acousticScore, threatLevel)), scatterActive);
                float flockingScore = ScoreThreat(math.saturate(input.FlockCount * FlockCountInvSoftCap)) * math.select(0f, math.max(0.25f, 1f - escapeScore), isFlocking && input.FlockCount > 1 && !scatterActive && !satedActive && !shouldEscape && !homeOutOfBounds);
                float curiosityScore = math.max(ScoreThreat(acousticScore), ScoreThreat(threatVisual * 0.5f)) * curiosityWeight * math.select(0f, 1f, !shouldEscape && !satedActive);
                float satedScore = math.select(0f, OverrideScoreBias + fatigueScore, satedActive);
                float wanderScore = math.max(
                    MinimumScoreThreshold,
                    ((1f - escapeScore) * 0.45f) +
                    ((1f - math.saturate(threatLevel)) * 0.35f) +
                    (fatigueScore * 0.2f) +
                    (curiosityScore * 0.15f)) *
                    math.select(1f, 0.15f, satedActive);

                int selectedStateCode = (int)FaunaBrain.AIState.Wander;
                float selectedScore = wanderScore;
                int escapeStateCode = math.select((int)FaunaBrain.AIState.Escape, (int)FaunaBrain.AIState.Retreat, retreatForced);
                SelectHigherUtilityState(ref selectedStateCode, ref selectedScore, escapeScore, escapeStateCode);
                SelectHigherUtilityState(ref selectedStateCode, ref selectedScore, returnScore, (int)FaunaBrain.AIState.Return);
                SelectHigherUtilityState(ref selectedStateCode, ref selectedScore, flockingScore, (int)FaunaBrain.AIState.Flocking);
                SelectHigherUtilityState(ref selectedStateCode, ref selectedScore, scatterScore, (int)FaunaBrain.AIState.Flocking);
                SelectHigherUtilityState(ref selectedStateCode, ref selectedScore, satedScore, (int)FaunaBrain.AIState.Sated);

                float3 desiredDirection = fallbackForward;
                FaunaBrain.AIState state = (FaunaBrain.AIState)selectedStateCode;
                float forceMultiplier = 1f;
                float speedMultiplier = 1f;
                float turnMultiplier = 1f;

                if (state == FaunaBrain.AIState.Sated)
                {
                    RefreshWanderTarget(ref control, input.CurrentTime, input.Position, math.max(1f, input.WanderRadius));
                    desiredDirection = ResolveDominantAxis(control.WanderTarget - input.Position, fallbackForward);
                    speedMultiplier = 0.6f;
                    turnMultiplier = 0.5f;
                }
                else if (state == FaunaBrain.AIState.Retreat || state == FaunaBrain.AIState.ApexForcedRetreat || state == FaunaBrain.AIState.Escape)
                {
                    float3 fleeFrom = input.PlayerPosition;
                    if ((control.Flags & (int)CognitionControlFlags.HasOverrideThreatPosition) != 0 && control.OverrideUntilTime > input.CurrentTime)
                        fleeFrom = control.OverrideThreatPosition;
                    else if (threatVisible)
                        fleeFrom = input.ThreatPosition;
                    else if (hasAcousticMemory && acousticMemoryScore > AcousticStimulusThreshold)
                        fleeFrom = acousticMemoryPosition;
                    desiredDirection = ResolveDominantAxis(input.Position - fleeFrom, -fallbackForward);
                    forceMultiplier = 2.35f;
                    speedMultiplier = math.max(1.2f, input.FearWeight);
                    turnMultiplier = 1.15f;
                }
                else if (state == FaunaBrain.AIState.Flocking && scatterActive)
                {
                    desiredDirection = ResolveDominantAxis(control.ScatterDirection, fallbackForward);
                    forceMultiplier = 4f;
                    speedMultiplier = 2f;
                    turnMultiplier = 1.2f;
                }
                else if (state == FaunaBrain.AIState.Return)
                {
                    desiredDirection = ResolveDominantAxis(control.SpawnAnchor - input.Position, fallbackForward);
                }
                else if (state == FaunaBrain.AIState.Flocking && isFlocking && input.FlockCount > 1)
                {
                    float3 cohesion = ResolveDominantAxis(input.FlockCenter - input.Position, float3.zero);
                    desiredDirection = ResolveDominantAxis(cohesion + input.FlockDirection + input.FlockAvoidance, fallbackForward);
                }
                else
                {
                    float wanderRadius = useHomeTerritory ? math.max(1f, input.PatrolRadius) : math.max(1f, input.WanderRadius);
                    float3 wanderCenter = useHomeTerritory ? control.SpawnAnchor : input.Position;
                    RefreshWanderTarget(ref control, input.CurrentTime, wanderCenter, wanderRadius);
                    desiredDirection = ResolveDominantAxis(control.WanderTarget - input.Position, fallbackForward);
                }

                PackedCognitionOutput output = default;
                output.DesiredDirection = desiredDirection;
                output.ForceMultiplier = forceMultiplier;
                output.SpeedMultiplier = speedMultiplier;
                output.TurnMultiplier = turnMultiplier;
                output.PackedScores = PackScoreTriplet(ScoreHunger(hunger), math.saturate(math.max(scatterScore, flockingScore)), math.saturate(escapeScore));
                output.StateMask = 0u;
                output.LegacyState = (int)state;
                output.OutputFlags = 0u;
                return output;
            }

            private static void SelectHigherUtilityState(ref int currentStateCode, ref float currentScore, float candidateScore, int candidateStateCode)
            {
                bool replace = candidateScore > currentScore;
                currentStateCode = math.select(currentStateCode, candidateStateCode, replace);
                currentScore = math.select(currentScore, candidateScore, replace);
            }

            private static bool IsLightAversionActive(in CognitionInput input)
            {
                return input.PlayerLightExposure01 > DdaEpsilon &&
                       input.LightReactionMode == (int)FaunaLightReactionMode.Aversion;
            }

            private static bool IsLightFrenzyActive(in CognitionInput input)
            {
                return input.PlayerLightExposure01 > DdaEpsilon &&
                       input.LightReactionMode == (int)FaunaLightReactionMode.Frenzy;
            }

            private unsafe bool TryResolveBaseSiegeTarget(
                int slot,
                in CognitionInput input,
                float3 fallbackForward,
                float3 predictedPlayerPosition,
                bool predatorEligible,
                out float3 targetPosition,
                out BaseSiegeRole siegeRole,
                out float siegeScore)
            {
                targetPosition = input.Position;
                siegeRole = BaseSiegeRole.None;
                siegeScore = 0f;
                if (!predatorEligible || HabitatSiegeTargetCount <= 0 || !HabitatSiegeTargets.IsCreated)
                    return false;

                float bestScore = 0f;
                int bestIndex = -1;
                HabitatSiegeTargetSnapshot bestTarget = default;
                float engageRadiusSq = BaseSiegeEngageRadiusMeters * BaseSiegeEngageRadiusMeters;
                float invEngageRadiusSq = math.rcp(math.max(engageRadiusSq, MathSafetyEpsilon));
                int targetCount = math.min(HabitatSiegeTargetCount, HabitatSiegeTargets.Length);
                for (int i = 0; i < targetCount; i++)
                {
                    HabitatSiegeTargetSnapshot target = HabitatSiegeTargets[i];
                    if (target.Vulnerability01 <= DdaEpsilon)
                        continue;

                    float3 delta = target.WeakPoint - input.Position;
                    float distanceSq = math.lengthsq(delta);
                    if (distanceSq <= DdaEpsilon || distanceSq > engageRadiusSq)
                        continue;

                    HabitatSiegeTargetFlags flags = (HabitatSiegeTargetFlags)target.Flags;
                    float range01 = 1f - math.saturate(distanceSq * invEngageRadiusSq);
                    float flagBias = 0f;
                    flagBias += math.select(0f, 0.25f, (flags & HabitatSiegeTargetFlags.Ruptured) != 0);
                    flagBias += math.select(0f, 0.15f, (flags & HabitatSiegeTargetFlags.Flooded) != 0);
                    flagBias += math.select(0f, 0.15f, (flags & HabitatSiegeTargetFlags.EmergencyAirlock) != 0);
                    flagBias += math.select(0f, 0.1f, (flags & HabitatSiegeTargetFlags.Brownout) != 0);
                    flagBias += math.select(0f, 0.1f, (flags & HabitatSiegeTargetFlags.Isolated) != 0);
                    float score = (target.Vulnerability01 * 2f) + range01 + flagBias;
                    if (score <= bestScore)
                        continue;

                    bestScore = score;
                    bestIndex = i;
                    bestTarget = target;
                }

                if (bestIndex < 0)
                    return false;

                if (TryReserveBaseSiegeRole(BaseSiegeRammerClaimTable, bestIndex, slot))
                    siegeRole = BaseSiegeRole.Rammer;
                else if (TryReserveBaseSiegeRole(BaseSiegeDistractorClaimTable, bestIndex, slot))
                    siegeRole = BaseSiegeRole.Distractor;
                else if (TryReserveBaseSiegeRole(BaseSiegeLoitererClaimTable, bestIndex, slot))
                    siegeRole = BaseSiegeRole.Loiterer;

                if (siegeRole == BaseSiegeRole.None)
                    return false;

                float3 toWeakPoint = ResolveDominantAxis(bestTarget.WeakPoint - input.Position, fallbackForward);
                float3 moduleToPlayer = ResolveDominantAxis(predictedPlayerPosition - bestTarget.ModuleCenter, fallbackForward);
                if (math.lengthsq(moduleToPlayer) <= DdaEpsilon)
                    moduleToPlayer = ResolveDominantAxis(input.PlayerPosition - bestTarget.ModuleCenter, fallbackForward);

                float3 lateral = ResolveDominantAxis(
                    math.cross(new float3(0f, 1f, 0f), moduleToPlayer),
                    math.cross(new float3(0f, 0f, 1f), moduleToPlayer));
                float sideSign = (slot & 1) == 0 ? 1f : -1f;

                if (siegeRole == BaseSiegeRole.Rammer)
                {
                    targetPosition = bestTarget.WeakPoint - (toWeakPoint * BaseSiegeRammerStandoffMeters);
                }
                else if (siegeRole == BaseSiegeRole.Distractor)
                {
                    targetPosition =
                        bestTarget.ModuleCenter +
                        (lateral * (BaseSiegeDistractorLateralOffsetMeters * sideSign)) +
                        (moduleToPlayer * BaseSiegeDistractorForwardOffsetMeters);
                }
                else
                {
                    float3 loiterDirection = ResolveDominantAxis(input.Position - bestTarget.ModuleCenter, -moduleToPlayer);
                    targetPosition = bestTarget.ModuleCenter + (loiterDirection * BaseSiegeLoiterRadiusMeters);
                }

                siegeScore = math.saturate(bestScore * 0.25f);
                return true;
            }

            private static unsafe bool TryReserveBaseSiegeRole(int* claimTable, int targetIndex, int creatureSlot)
            {
                if (claimTable == null || targetIndex < 0 || targetIndex >= HabitatGraphManager.MaxSiegeTargetCount)
                    return false;

                for (int attempt = 0; attempt < MaxPackRoleCasAttempts; attempt++)
                {
                    int priorOwner = System.Threading.Interlocked.CompareExchange(
                        ref claimTable[targetIndex],
                        creatureSlot,
                        UnclaimedBoidSlot);
                    if (priorOwner == UnclaimedBoidSlot || priorOwner == creatureSlot)
                        return true;
                }

                return false;
            }

            private unsafe bool TryClaimBoid(int creatureSlot, int boidSlot)
            {
                if (boidSlot < 0 || boidSlot >= BoidClaimTable.Length)
                    return false;

                int* claimPtr = (int*)BoidClaimTable.GetUnsafePtr();
                int priorOwner = System.Threading.Interlocked.CompareExchange(ref claimPtr[boidSlot], creatureSlot, UnclaimedBoidSlot);
                return priorOwner == UnclaimedBoidSlot || priorOwner == creatureSlot;
            }

            private static int BuildWinningStateMask(float4 scores, float winningScore)
            {
                float4 threshold = new float4(winningScore - DdaEpsilon);
                int winningMask = math.bitmask(scores >= threshold) & 0xF;
                return winningMask & -winningMask;
            }

            private static int DecodePredatorStateCode(int winningMask)
            {
                int stateCode = (int)PredatorUtilityState.Prowling;
                stateCode = math.select(stateCode, (int)PredatorUtilityState.Stalking, (winningMask & (1 << 1)) != 0);
                stateCode = math.select(stateCode, (int)PredatorUtilityState.Attacking, (winningMask & (1 << 2)) != 0);
                stateCode = math.select(stateCode, (int)PredatorUtilityState.Fleeing, (winningMask & (1 << 3)) != 0);
                return stateCode;
            }

            private bool TryResolveStrongestAcousticMemory(int slot, float3 currentPosition, float currentTime, out float3 position, out float acousticWeight)
            {
                float bestScore = 0f;
                float3 bestPosition = default;
                bool found = false;
                int startIndex = slot * AcousticMemorySlotsPerCreature;
                int3 selfBucket = ResolveAcousticBucketCoordinates(currentPosition, AcousticBucketCellSize);
                uint selfBucketHash = HashAcousticBucket(selfBucket);

                for (int i = 0; i < AcousticMemorySlotsPerCreature; i++)
                {
                    int memoryIndex = startIndex + i;
                    AcousticMemoryEntry entry = AcousticMemoryBank[memoryIndex];
                    float4 packedMemory = AcousticMemoryFloat4Bank.IsCreated && memoryIndex < AcousticMemoryFloat4Bank.Length
                        ? AcousticMemoryFloat4Bank[memoryIndex]
                        : new float4(entry.WorldPosition, entry.Timestamp);
                    float memoryTimestamp = math.select(entry.Timestamp, packedMemory.w, packedMemory.w > 0f);
                    float age = currentTime - memoryTimestamp;
                    if (age < 0f ||
                        age > AcousticMemoryLifetimeSeconds ||
                        entry.Intensity <= 0f)
                    {
                        continue;
                    }

                    float acousticDecay = ApexCortexTuning.IsCreated && ApexCortexTuning.Length > 0
                        ? math.max(0.01f, ApexCortexTuning[0].w)
                        : 1f;
                    float decayWeight = 1f - math.saturate(age * AcousticMemoryLifetimeInvSeconds * acousticDecay);
                    decayWeight *= decayWeight;

                    int3 bucketDelta = math.abs(selfBucket - entry.BucketCoord);
                    float bucketDistanceSq = math.dot((float3)bucketDelta, (float3)bucketDelta);
                    float bucketWeight = math.rcp(1f + bucketDistanceSq);
                    float hashWeight = math.select(1f, 1f + AcousticBucketHashBias, entry.BucketHash == selfBucketHash);
                    float candidateScore = entry.Intensity * decayWeight * bucketWeight * hashWeight;
                    if (candidateScore <= bestScore)
                        continue;

                    bestScore = candidateScore;
                    bestPosition = packedMemory.xyz;
                    found = true;
                }

                position = bestPosition;
                acousticWeight = math.saturate(bestScore);
                return found;
            }

            private bool TryResolveStrongestMemory(int slot, float3 currentPosition, float currentTime, out float3 position, out float threatWeight)
            {
                return TryResolveStrongestMemory(slot, currentPosition, currentTime, 0, out position, out threatWeight);
            }

            private bool TryResolveSpatialMemoryBankTarget(int slot, float currentTime, out float3 position, out float threatWeight)
            {
                float totalWeight = 0f;
                float3 weightedPosition = float3.zero;
                float strongestWeight = 0f;
                int recalledCount = 0;
                int startIndex = slot * MemorySlotsPerCreature;
                int nextWriteIndex = Cores[slot].MemoryHead & (MemorySlotsPerCreature - 1);
                for (int i = 1; i <= MemorySlotsPerCreature && recalledCount < SpatialMemoryRecallCount; i++)
                {
                    int memorySlot = (nextWriteIndex - i) & (MemorySlotsPerCreature - 1);
                    CognitionMemoryEntry entry = MemoryBank[startIndex + memorySlot];
                    float age = currentTime - entry.Timestamp;
                    if (age < 0f || age > MemoryLifetimeSeconds || entry.Intensity <= 0f)
                        continue;

                    float decayWeight = 1f - math.saturate(age * MemoryLifetimeInvSeconds);
                    decayWeight *= decayWeight;
                    float recallWeight = entry.Intensity * decayWeight;
                    if (recallWeight <= DdaEpsilon)
                        continue;

                    weightedPosition += entry.WorldPosition * recallWeight;
                    totalWeight += recallWeight;
                    strongestWeight = math.max(strongestWeight, recallWeight);
                    recalledCount++;
                }

                if (totalWeight <= DdaEpsilon)
                {
                    position = float3.zero;
                    threatWeight = 0f;
                    return false;
                }

                position = weightedPosition * math.rcp(math.max(totalWeight, MathSafetyEpsilon));
                threatWeight = math.saturate(strongestWeight);
                return true;
            }

            private bool TryResolveStrongestMemory(int slot, float3 currentPosition, float currentTime, int stimulusMask, out float3 position, out float threatWeight)
            {
                float bestScore = 0f;
                float3 bestPosition = default;
                bool found = false;
                int startIndex = slot * MemorySlotsPerCreature;
                for (int i = 0; i < MemorySlotsPerCreature; i++)
                {
                    CognitionMemoryEntry entry = MemoryBank[startIndex + i];
                    float age = currentTime - entry.Timestamp;
                    if (age < 0f ||
                        age > MemoryLifetimeSeconds ||
                        entry.Intensity <= 0f ||
                        (stimulusMask != 0 && (entry.StimulusType & stimulusMask) == 0))
                        continue;

                    float decayWeight = 1f - math.saturate(age * MemoryLifetimeInvSeconds);
                    decayWeight *= decayWeight;
                    float3 toMemory = entry.WorldPosition - currentPosition;
                    float distanceWeight = math.rcp(1f + (math.lengthsq(toMemory) * MemoryDistanceSqrFalloff));
                    float candidateScore = entry.Intensity * decayWeight * distanceWeight;
                    if (candidateScore <= bestScore)
                        continue;

                    bestScore = candidateScore;
                    bestPosition = entry.WorldPosition;
                    found = true;
                }

                position = bestPosition;
                threatWeight = math.saturate(bestScore);
                return found;
            }

            private float ComputeAcousticScore(float3 selfPosition, float3 sourcePosition, float sourceStrength01, float transmission01)
            {
                float strength = math.saturate(sourceStrength01) * math.saturate(math.max(transmission01, 0f));
                if (strength <= 0f)
                    return 0f;

                float distanceSq = math.lengthsq(sourcePosition - selfPosition);
                float range01 = math.saturate(1f - (distanceSq * PredatorAcousticSightInvRangeSqr));
                return strength * range01;
            }

            private float ComputeChemicalScore(
                int slot,
                float3 selfPosition,
                float3 scavengePosition,
                bool hasScavengeTarget,
                float directChemicalSignal01,
                float chemicalSensitivity,
                float currentTime)
            {
                float directScore = 0f;
                if (hasScavengeTarget)
                {
                    float distanceSq = math.lengthsq(scavengePosition - selfPosition);
                    float rangeSq = ChemicalSignalRangeMeters * ChemicalSignalRangeMeters;
                    float invRangeSq = math.rcp(math.max(rangeSq, 1f));
                    directScore = math.saturate(directChemicalSignal01) * math.saturate(math.rcp(1f + (distanceSq * invRangeSq)));
                }

                if (TryResolveStrongestMemory(slot, selfPosition, currentTime, (int)CognitionStimulusType.Chemical, out _, out float memoryScore))
                    directScore = math.max(directScore, memoryScore);

                return math.saturate(directScore * math.max(0f, chemicalSensitivity));
            }

            private bool TryResolveChemicalGradient(float3 worldPosition, float currentTime, out float attractantSignal, out float fearSignal, out float3 gradient)
            {
                attractantSignal = 0f;
                fearSignal = 0f;
                gradient = float3.zero;
                if (TrySampleChemicalGrid(worldPosition, out attractantSignal, out fearSignal, out gradient))
                    return true;

                if (!ChemicalBreadcrumbs.IsCreated ||
                    ChemicalBreadcrumbs.Length <= 0 ||
                    ChemicalBreadcrumbCount <= 0)
                {
                    return false;
                }

                int safeCount = math.min(ChemicalBreadcrumbCount, ChemicalBreadcrumbs.Length);
                for (int i = 0; i < safeCount; i++)
                {
                    ChemicalInfluenceGrid.ChemicalBreadcrumbWaypoint waypoint = ChemicalBreadcrumbs[i];
                    if (waypoint.ExpiresAt <= currentTime || waypoint.RadiusMeters <= DdaEpsilon)
                        continue;

                    float radius = math.max(1f, waypoint.RadiusMeters);
                    float3 delta = waypoint.RuntimePosition - worldPosition;
                    float distanceSq = math.lengthsq(delta);
                    if (distanceSq > radius * radius)
                        continue;

                    float invRadius = math.rcp(math.max(radius, 0.001f));
                    float radiusSq = radius * radius;
                    float falloff = SmoothStep01(1f - math.saturate(distanceSq * math.rcp(math.max(radiusSq, 0.001f))));
                    float4 sample = waypoint.Channels * falloff;
                    float attractant = math.saturate(sample.x + sample.y);
                    float fear = math.saturate(sample.z);
                    attractantSignal = math.max(attractantSignal, attractant);
                    fearSignal = math.max(fearSignal, fear);

                    if (attractant > DdaEpsilon)
                        gradient += ResolveDominantAxis(delta, float3.zero) * (attractant * invRadius);
                }

                return attractantSignal > DdaEpsilon || fearSignal > DdaEpsilon || math.lengthsq(gradient) > DdaEpsilon;
            }

            private bool TrySampleChemicalGrid(float3 worldPosition, out float attractantSignal, out float fearSignal, out float3 gradient)
            {
                attractantSignal = 0f;
                fearSignal = 0f;
                gradient = float3.zero;
                if (!ChemicalFrontGrid.IsCreated ||
                    ChemicalFrontGrid.Length <= 0 ||
                    ChemicalGridDimensions.x <= 0 ||
                    ChemicalGridDimensions.y <= 0 ||
                    ChemicalGridDimensions.z <= 0)
                {
                    return false;
                }

                float3 invCell = math.rcp(math.max(ChemicalGridCellSize, new float3(0.0001f)));
                float3 gridPosition = (worldPosition - ChemicalGridOrigin) * invCell;
                if (gridPosition.x < 0f ||
                    gridPosition.y < 0f ||
                    gridPosition.z < 0f ||
                    gridPosition.x > ChemicalGridDimensions.x - 1 ||
                    gridPosition.y > ChemicalGridDimensions.y - 1 ||
                    gridPosition.z > ChemicalGridDimensions.z - 1)
                {
                    return false;
                }

                float4 center = SampleChemicalGridNearest(gridPosition);
                attractantSignal = math.saturate(center.x + center.y);
                fearSignal = math.saturate(center.z);

                float4 plusX = SampleChemicalGridNearest(gridPosition + new float3(1f, 0f, 0f));
                float4 minusX = SampleChemicalGridNearest(gridPosition - new float3(1f, 0f, 0f));
                float4 plusZ = SampleChemicalGridNearest(gridPosition + new float3(0f, 0f, 1f));
                float4 minusZ = SampleChemicalGridNearest(gridPosition - new float3(0f, 0f, 1f));
                float gx = (plusX.x + plusX.y) - (minusX.x + minusX.y);
                float gz = (plusZ.x + plusZ.y) - (minusZ.x + minusZ.y);
                gradient = new float3(gx, 0f, gz);
                return attractantSignal > DdaEpsilon || fearSignal > DdaEpsilon || math.lengthsq(gradient) > DdaEpsilon;
            }

            private float4 SampleChemicalGridNearest(float3 gridPosition)
            {
                int x = math.clamp((int)math.round(gridPosition.x), 0, ChemicalGridDimensions.x - 1);
                int y = math.clamp((int)math.round(gridPosition.y), 0, ChemicalGridDimensions.y - 1);
                int z = math.clamp((int)math.round(gridPosition.z), 0, ChemicalGridDimensions.z - 1);
                int index = x + z * ChemicalGridDimensions.x + y * ChemicalGridDimensions.x * ChemicalGridDimensions.z;
                if ((uint)index >= (uint)ChemicalFrontGrid.Length)
                    return float4.zero;

                float4 sample = ChemicalFrontGrid[index];
                if (ChemicalOverlayGrid.IsCreated && (uint)index < (uint)ChemicalOverlayGrid.Length)
                    sample.w = math.min(sample.w, ChemicalOverlayGrid[index].w);

                return sample;
            }

            private static float SmoothStep01(float value)
            {
                float t = math.saturate(value);
                return t * t * (3f - 2f * t);
            }

            private static float EvaluateHuntUtility(
                float hungerScore,
                float fearScore,
                float aggressionWeight,
                float targetSignal,
                float attackCommit01,
                float satedSuppression)
            {
                float calm01 = math.saturate(1f - fearScore);
                float driveScore = ScoreHunger(hungerScore) * calm01;
                float aggressionDrive = ScoreThreat(aggressionWeight);
                float stimulusDrive = ScoreThreat(math.max(targetSignal, 0.15f));
                float commitDrive = math.lerp(0.45f, 1f, SmoothStep01(attackCommit01));
                return driveScore * aggressionDrive * stimulusDrive * commitDrive * satedSuppression;
            }

            private static float EvaluateFleeUtility(float fearScore, float threatScore, float damage01)
            {
                float threatDrive = ScoreFear(math.max(fearScore, threatScore));
                float damageDrive = 1f + (math.saturate(damage01) * 0.35f);
                return threatDrive * damageDrive;
            }

            private static float EvaluatePatrolUtility(
                float hungerScore,
                float fearScore,
                float threatScore,
                float fatigueScore,
                bool satedActive)
            {
                float calmDrive = ScoreHunger(1f - math.max(fearScore, threatScore));
                float recoveryDrive = ScoreThreat(1f - fatigueScore);
                float lowHungerDrive = ScoreThreat(1f - hungerScore);
                float satedBias = math.select(1f, 1.2f, satedActive);
                return lowHungerDrive * calmDrive * recoveryDrive * satedBias;
            }

            private static float ScoreHunger(float x)
            {
                float normalized = math.saturate(x);
                return normalized * normalized;
            }

            private static float ScoreFear(float x)
            {
                float normalized = math.saturate(x);
                return normalized * normalized * normalized;
            }

            private static float ScoreFatigue(float x)
            {
                float normalized = math.saturate(x);
                float cubic = normalized * normalized * normalized;
                return (0.4f * normalized) + (0.6f * cubic);
            }

            private static float ScoreThreat(float x)
            {
                float normalized = math.saturate(x);
                float inverse = 1f - normalized;
                return 1f - (inverse * inverse);
            }

            private static float SquareActionScore(float score)
            {
                float safeScore = math.max(0f, score);
                return safeScore * safeScore;
            }

            private static float3 ResolveDominantAxis(float3 direction, float3 fallback)
            {
                if (math.lengthsq(direction) <= DdaEpsilon)
                    direction = fallback;

                if (math.lengthsq(direction) <= DdaEpsilon)
                    return new float3(0f, 0f, 1f);

                float3 absolute = math.abs(direction);
                if (absolute.x >= absolute.y && absolute.x >= absolute.z)
                    return new float3(math.select(1f, -1f, direction.x < 0f), 0f, 0f);

                if (absolute.y >= absolute.z)
                    return new float3(0f, math.select(1f, -1f, direction.y < 0f), 0f);

                return new float3(0f, 0f, math.select(1f, -1f, direction.z < 0f));
            }

            private static int3 ResolveSpatialBucketCoordinates(float3 worldPosition, float3 boundsMin, float bucketCellSize)
            {
                float safeCellSize = math.max(bucketCellSize, 0.001f);
                float invCellSize = math.rcp(safeCellSize);
                float3 localPosition = math.max(worldPosition - boundsMin, float3.zero);
                return new int3(
                    (int)math.floor(localPosition.x * invCellSize),
                    (int)math.floor(localPosition.y * invCellSize),
                    (int)math.floor(localPosition.z * invCellSize));
            }

            private static float ResolveThreatBlend(float deltaTime)
            {
                float safeDeltaTime = math.min(math.max(0f, deltaTime), MaxThreatSmoothingDeltaTime);
                return math.saturate(1f - FastExpNegPade13(ThreatSmoothingK * safeDeltaTime));
            }

            private static float FastExpNegPade13(float positiveX)
            {
                float x = math.max(0f, positiveX);
                float x2 = x * x;
                float numerator = math.max(0f, 1f - 0.25f * x);
                float denominator = 1f + 0.75f * x + 0.25f * x2 + 0.0416666679f * x2 * x;
                return math.saturate(numerator * math.rcp(math.max(denominator, 0.0001f)));
            }

            private static int3 ResolveAcousticBucketCoordinates(float3 worldPosition, float bucketCellSize)
            {
                float safeCellSize = math.max(bucketCellSize, 0.001f);
                float invCellSize = math.rcp(safeCellSize);
                int3 rawBucket = new int3(
                    (int)math.floor(worldPosition.x * invCellSize),
                    (int)math.floor(worldPosition.y * invCellSize),
                    (int)math.floor(worldPosition.z * invCellSize));
                return rawBucket + new int3(
                    AcousticBucketOriginBiasCells,
                    AcousticBucketOriginBiasCells,
                    AcousticBucketOriginBiasCells);
            }

            private static uint HashAcousticBucket(int3 bucketCoord)
            {
                uint x = (uint)bucketCoord.x;
                uint y = (uint)bucketCoord.y;
                uint z = (uint)bucketCoord.z;
                return (x * 73856093u) ^ (y * 19349663u) ^ (z * 83492791u);
            }

            private static void RefreshWanderTarget(ref CognitionControl control, float currentTime, float3 center, float radius)
            {
                if ((control.Flags & (int)CognitionControlFlags.HasWanderTarget) != 0 &&
                    currentTime < control.NextWanderTargetRefreshTime &&
                    math.lengthsq(control.WanderTarget - center) > 9f)
                {
                    return;
                }

                uint wanderHash = HashWanderSeed(control.WanderSequence, center);
                int octant = (int)(wanderHash & 7u);
                float3 direction = ResolveOctantDirectionXZ(octant);
                float radiusT = ((wanderHash >> 8) & 0xFFFFu) * WanderHashUshortInvScale;
                float wanderRadius = math.max(1f, radius) * math.lerp(0.45f, 1f, radiusT);
                float verticalT = (((wanderHash >> 24) & 0xFFu) * QuantizedByteInvScale) - 0.5f;
                control.WanderTarget = center + new float3(
                    direction.x * wanderRadius,
                    verticalT * MaximumWanderVerticalOffset,
                    direction.z * wanderRadius);
                control.WanderSequence++;
                control.NextWanderTargetRefreshTime = currentTime + WanderTargetRefreshSeconds;
                control.Flags |= (int)CognitionControlFlags.HasWanderTarget;
            }

            private static uint HashWanderSeed(int sequence, float3 center)
            {
                unchecked
                {
                    uint state = ((uint)sequence + 1u) * 1664525u + 1013904223u;
                    state ^= math.asuint(center.x) * 0x85EBCA6Bu;
                    state = (state * 1664525u) + 1013904223u;
                    state ^= math.asuint(center.y) * 0x27D4EB2Du;
                    state = (state * 1664525u) + 1013904223u;
                    state ^= math.asuint(center.z) * 0xC2B2AE35u;
                    state = (state * 1664525u) + 1013904223u;
                    return state ^ (state >> 16);
                }
            }

            private static float3 ResolveOctantDirectionXZ(int octant)
            {
                switch (octant & 7)
                {
                    case 0:
                        return new float3(1f, 0f, 0f);
                    case 1:
                        return new float3(0.70710678f, 0f, 0.70710678f);
                    case 2:
                        return new float3(0f, 0f, 1f);
                    case 3:
                        return new float3(-0.70710678f, 0f, 0.70710678f);
                    case 4:
                        return new float3(-1f, 0f, 0f);
                    case 5:
                        return new float3(-0.70710678f, 0f, -0.70710678f);
                    case 6:
                        return new float3(0f, 0f, -1f);
                    default:
                        return new float3(0.70710678f, 0f, -0.70710678f);
                }
            }

            private static float ComputeThreatVisual(float3 selfPosition, float3 targetPosition, float3 fallbackForward, float range)
            {
                float3 toTarget = targetPosition - selfPosition;
                float distanceSq = math.lengthsq(toTarget);
                if (distanceSq <= DdaEpsilon)
                    return 0f;

                float forwardDot = math.dot(fallbackForward, toTarget);
                if (forwardDot <= 0f)
                    return 0f;

                float dotSq = forwardDot * forwardDot;
                if (dotSq < PredatorVisionConeCosineThresholdSqr * distanceSq)
                    return 0f;

                float safeRange = math.max(range, 1f);
                return math.saturate(1f - (distanceSq * math.rcp(math.max(safeRange * safeRange, MathSafetyEpsilon))));
            }

            private float3 ResolvePredatorDirection(
                int slot,
                PredatorUtilityState stateMask,
                float3 selfPosition,
                float3 targetPosition,
                float3 fallbackForward,
                float currentTime,
                CognitionControl control,
                bool isApexPredator,
                bool useHighTierSmoothSteering)
            {
                if (stateMask == PredatorUtilityState.Fleeing)
                {
                    float3 fleeFrom = ((control.Flags & (int)CognitionControlFlags.HasOverrideThreatPosition) != 0 &&
                                       control.OverrideUntilTime > currentTime)
                        ? control.OverrideThreatPosition
                        : targetPosition;
                    if ((control.Flags & (int)CognitionControlFlags.RetinalFlinch) != 0)
                    {
                        float3 awayFromLight = ResolveRsqrtDirection(selfPosition - fleeFrom, -fallbackForward);
                        float3 up = new float3(0f, 1f, 0f);
                        float3 lateral = ResolveRsqrtDirection(math.cross(up, awayFromLight), math.cross(new float3(0f, 0f, 1f), awayFromLight));
                        lateral = math.select(lateral, -lateral, (slot & 1) != 0);
                        float3 flinchDirection = useHighTierSmoothSteering
                            ? ResolveRetinalThrashDirection(slot, awayFromLight, lateral, currentTime)
                            : lateral;
                        return SanitizeSteeringVector(ApplyVortexSteering(selfPosition, flinchDirection, lateral, false), lateral);
                    }

                    float3 fleeDirection = ResolveDominantAxis(selfPosition - fleeFrom, -fallbackForward);
                    return SanitizeSteeringVector(ApplyVortexSteering(selfPosition, fleeDirection, -fallbackForward, false), -fallbackForward);
                }

                bool useApexSCurve =
                    isApexPredator &&
                    useHighTierSmoothSteering &&
                    (stateMask == PredatorUtilityState.Stalking || stateMask == PredatorUtilityState.Attacking);
                float3 desiredDirection = useApexSCurve
                    ? ResolveApexSCurveDirection(slot, stateMask, selfPosition, targetPosition, fallbackForward, currentTime)
                    : ResolveDominantAxis(targetPosition - selfPosition, fallbackForward);
                return SanitizeSteeringVector(ApplyVortexSteering(selfPosition, desiredDirection, fallbackForward, useApexSCurve), fallbackForward);
            }

            private static float3 ResolveRetinalThrashDirection(int slot, float3 awayFromLight, float3 lateral, float currentTime)
            {
                float phase = (slot * 0.6180339f) + (currentTime * 7.0f);
                float lateralJitter = RetinalExposureMath.SignedTriangle(phase);
                float verticalJitter = RetinalExposureMath.SignedTriangle((phase * 0.73f) + 0.37f);
                float3 mixed =
                    (lateral * (0.65f + (0.25f * lateralJitter))) +
                    (awayFromLight * (0.35f - (0.2f * lateralJitter))) +
                    new float3(0f, verticalJitter * 0.35f, 0f);
                return ResolveRsqrtDirection(mixed, lateral);
            }

            private float3 ApplyVortexSteering(float3 selfPosition, float3 desiredDirection, float3 fallbackForward, bool smoothSteering)
            {
                float3 forward = ResolveSteeringAxis(desiredDirection, fallbackForward, smoothSteering);
                if (!TryResolveVortexAvoidance(selfPosition, forward, out float3 avoidDir, out float pressure01))
                    return forward;

                return ResolveSteeringAxis(
                    math.lerp(forward, avoidDir, math.saturate(pressure01 * VortexSteeringBlend)),
                    forward,
                    smoothSteering);
            }

            private static float3 ResolveApexSCurveDirection(
                int slot,
                PredatorUtilityState stateMask,
                float3 selfPosition,
                float3 targetPosition,
                float3 fallbackForward,
                float currentTime)
            {
                float3 currentForward = ResolveRsqrtDirection(fallbackForward, new float3(0f, 0f, 1f));
                float3 toTarget = targetPosition - selfPosition;
                if (math.lengthsq(toTarget) <= DdaEpsilon)
                    return currentForward;

                float3 desiredForward = ResolveRsqrtDirection(toTarget, currentForward);
                float3 up = new float3(0f, 1f, 0f);
                float3 lateral = ResolveRsqrtDirection(math.cross(up, desiredForward), math.cross(new float3(0f, 0f, 1f), desiredForward));
                float attackWeight = math.select(0.62f, 1f, stateMask == PredatorUtilityState.Attacking);
                float distanceWeight = math.saturate(math.lengthsq(toTarget) * ApexSCurveInvMaxDistanceSqr);
                float phase = (currentTime * ApexSCurveFrequency) + ((slot & 15) * ApexSCurvePhaseStep);
                float curve = CinematicMath.FastTriangleWaveSigned(phase) * ApexSCurveLateralWeight * attackWeight * distanceWeight;
                float3 curvedForward = ResolveRsqrtDirection(desiredForward + (lateral * curve), desiredForward);
                quaternion fromRotation = quaternion.LookRotationSafe(currentForward, up);
                quaternion toRotation = quaternion.LookRotationSafe(curvedForward, up);
                float turnBlend = math.select(ApexSCurveNlerpStalk, ApexSCurveNlerpAttack, stateMask == PredatorUtilityState.Attacking);
                quaternion blendedRotation = CinematicMath.FastNlerp(fromRotation, toRotation, turnBlend);
                return ResolveRsqrtDirection(math.mul(blendedRotation, new float3(0f, 0f, 1f)), currentForward);
            }

            private bool TryResolveVortexAvoidance(float3 selfPosition, float3 forward, out float3 avoidDir, out float pressure01)
            {
                avoidDir = forward;
                pressure01 = 0f;

                float3 probePosition = selfPosition + (forward * VortexProbeDistanceMeters);
                if (!IsThreatVoxelSolidOrOutOfBounds(probePosition))
                    return false;

                if (!TryResolveThreatVoxelGradient(probePosition, out float3 wallNormal))
                    wallNormal = -forward;

                wallNormal = ResolveDominantHorizontalAxis(wallNormal, -forward);
                float3 up = new float3(0f, 1f, 0f);
                avoidDir = math.cross(wallNormal, up);
                if (math.lengthsq(avoidDir) <= DdaEpsilon)
                    avoidDir = math.cross(new float3(0f, 0f, 1f), up);

                avoidDir = ResolveSteeringAxis(avoidDir, forward, false);
                if (math.dot(avoidDir, forward) < 0f)
                    avoidDir = -avoidDir;

                pressure01 = 1f;
                return math.lengthsq(avoidDir) > DdaEpsilon;
            }

            private bool TryResolveAmbushCreviceTarget(float3 selfPosition, float3 fallbackForward, out float3 targetPosition)
            {
                targetPosition = selfPosition;
                if (!ThreatVoxelGrid.IsCreated ||
                    ThreatVoxelDimensions.x <= 0 ||
                    ThreatVoxelDimensions.y <= 0 ||
                    ThreatVoxelDimensions.z <= 0)
                {
                    return false;
                }

                if (!TryResolveThreatVoxelGradient(selfPosition, out float3 gradient))
                    return false;

                float3 creviceDirection = ResolveSteeringAxis(gradient, fallbackForward, false);
                targetPosition = selfPosition + (creviceDirection * AmbushSdfProbeDistanceMeters);
                return !IsThreatVoxelSolidOrOutOfBounds(targetPosition);
            }

            private bool TryResolveThreatVoxelGradient(float3 worldPosition, out float3 gradient)
            {
                gradient = float3.zero;
                if (!TryWorldToVoxel(worldPosition, out int3 voxel))
                    return false;

                float sample01 = SampleThreatVoxelScalar(voxel);
                float3 safeCellSize = math.max(ThreatVoxelCellSize, new float3(0.001f, 0.001f, 0.001f));
                float3 cellMin = ThreatVoxelOrigin + (new float3(voxel.x, voxel.y, voxel.z) * safeCellSize);
                float3 local01 = math.saturate((worldPosition - cellMin) * math.rcp(safeCellSize));
                uint hash = HashAcousticBucket(voxel);
                float3 hashAxis = ResolveOctantDirectionXZ((int)(hash & 7u));
                hashAxis.y = math.select(-0.35f, 0.35f, (hash & 8u) != 0u);
                float sign = ThreatVoxelUsesSignedDistanceEncoding != 0
                    ? math.select(-1f, 1f, sample01 >= 0.5f)
                    : math.select(-1f, 1f, sample01 > DdaEpsilon);
                // Cinematic SDF gradient: one-cell local offset plus stable hash bias. No neighbor voxel scan.
                gradient = ResolveDominantAxis(((local01 - new float3(0.5f, 0.5f, 0.5f)) + hashAxis) * sign, hashAxis);
                return true;
            }

            private float SampleThreatVoxelScalarWorld(float3 worldPosition)
            {
                return TryWorldToVoxel(worldPosition, out int3 voxel)
                    ? SampleThreatVoxelScalar(voxel)
                    : 0f;
            }

            private float SampleThreatVoxelScalar(int3 voxel)
            {
                if (!IsVoxelInside(voxel))
                    return 0f;

                byte sample = SampleThreatVoxel(voxel);
                if (ThreatVoxelUsesSignedDistanceEncoding != 0)
                    return sample * QuantizedByteInvScale;

                return IsThreatVoxelSolid(sample) ? 0f : 1f;
            }

            private static float3 ResolveSteeringAxis(float3 direction, float3 fallback, bool smoothSteering)
            {
                return smoothSteering
                    ? ResolveRsqrtDirection(direction, fallback)
                    : ResolveDominantAxis(direction, fallback);
            }

            private static float3 ResolveRsqrtDirection(float3 direction, float3 fallback)
            {
                if (!MathGuard.IsFinite(direction) || math.lengthsq(direction) <= DdaEpsilon)
                    direction = fallback;

                float lengthSq = math.lengthsq(direction);
                if (!MathGuard.IsFinite(direction) || lengthSq <= DdaEpsilon)
                    return new float3(0f, 0f, 1f);

                return direction * math.rsqrt(math.max(lengthSq, MathSafetyEpsilon));
            }

            private static float3 SanitizeSteeringVector(float3 direction, float3 fallback)
            {
                if (MathGuard.IsFinite(direction) && math.lengthsq(direction) > DdaEpsilon)
                    return direction;

                return ResolveDominantAxis(fallback, new float3(0f, 0f, 1f));
            }

            private static float3 ResolveDominantHorizontalAxis(float3 direction, float3 fallback)
            {
                if (math.lengthsq(direction) <= DdaEpsilon)
                    direction = fallback;

                if (math.lengthsq(direction) <= DdaEpsilon)
                    return new float3(0f, 0f, 1f);

                float absX = math.abs(direction.x);
                float absZ = math.abs(direction.z);
                if (absX >= absZ)
                    return new float3(math.select(1f, -1f, direction.x < 0f), 0f, 0f);

                return new float3(0f, 0f, math.select(1f, -1f, direction.z < 0f));
            }

            private bool ResolveThreatVisibility(float3 start, float3 end, float importanceScore)
            {
                return HasThreatGridHeuristic(start, end);
            }

            private bool HasThreatGridHeuristic(float3 start, float3 end)
            {
                if (!ThreatVoxelGrid.IsCreated ||
                    ThreatVoxelDimensions.x <= 0 ||
                    ThreatVoxelDimensions.y <= 0 ||
                    ThreatVoxelDimensions.z <= 0)
                {
                    return true;
                }

                if (!TryWorldToVoxel(end, out int3 endVoxel))
                    return true;

                if (IsThreatVoxelSolid(SampleThreatVoxel(endVoxel)))
                    return false;

                float3 midpoint = math.lerp(start, end, 0.5f);
                if (TryWorldToVoxel(midpoint, out int3 midpointVoxel) &&
                    IsThreatVoxelSolid(SampleThreatVoxel(midpointVoxel)))
                {
                    return false;
                }

                return true;
            }

            private bool TryWorldToVoxel(float3 worldPosition, out int3 voxel)
            {
                float3 local = worldPosition - ThreatVoxelOrigin;
                if (local.x < 0f || local.y < 0f || local.z < 0f)
                {
                    voxel = int3.zero;
                    return false;
                }

                float3 invCellSize = math.rcp(math.max(ThreatVoxelCellSize, new float3(MathSafetyEpsilon)));
                int3 candidate = new int3(
                    (int)math.floor(local.x * invCellSize.x),
                    (int)math.floor(local.y * invCellSize.y),
                    (int)math.floor(local.z * invCellSize.z));
                if (!IsVoxelInside(candidate))
                {
                    voxel = int3.zero;
                    return false;
                }

                voxel = candidate;
                return true;
            }

            private bool IsThreatVoxelSolidOrOutOfBounds(float3 worldPosition)
            {
                if (!ThreatVoxelGrid.IsCreated ||
                    ThreatVoxelDimensions.x <= 0 ||
                    ThreatVoxelDimensions.y <= 0 ||
                    ThreatVoxelDimensions.z <= 0)
                {
                    return false;
                }

                if (!TryWorldToVoxel(worldPosition, out int3 voxel))
                    return true;

                return IsThreatVoxelSolid(SampleThreatVoxel(voxel));
            }

            private bool IsVoxelInside(int3 voxel)
            {
                return voxel.x >= 0 &&
                       voxel.y >= 0 &&
                       voxel.z >= 0 &&
                       voxel.x < ThreatVoxelDimensions.x &&
                       voxel.y < ThreatVoxelDimensions.y &&
                       voxel.z < ThreatVoxelDimensions.z;
            }

            private byte SampleThreatVoxel(int3 voxel)
            {
                int flatIndex = FlattenThreatVoxelIndex(voxel, ThreatVoxelDimensions);
                if (flatIndex < 0 || flatIndex >= ThreatVoxelGrid.Length)
                    return 0;

                return ThreatVoxelGrid[flatIndex];
            }

            private bool IsThreatVoxelSolid(byte sample)
            {
                if (ThreatVoxelUsesSignedDistanceEncoding != 0)
                    return sample < ThreatVoxelSolidThreshold;

                return sample >= ThreatVoxelSolidThreshold;
            }

            private static int FlattenThreatVoxelIndex(int3 voxel, int3 dimensions)
            {
                if (dimensions.x <= 0 || dimensions.y <= 0 || dimensions.z <= 0)
                    return -1;

                if (voxel.x < 0 ||
                    voxel.y < 0 ||
                    voxel.z < 0 ||
                    voxel.x >= dimensions.x ||
                    voxel.y >= dimensions.y ||
                    voxel.z >= dimensions.z)
                    return -1;

                long xyStride = (long)dimensions.x * dimensions.y;
                long index = voxel.x + ((long)voxel.y * dimensions.x) + ((long)voxel.z * xyStride);
                return xyStride > 0L && xyStride <= int.MaxValue && index >= 0L && index <= int.MaxValue ? (int)index : -1;
            }

            private static uint PackWorldStateFlags(FaunaBrain.AIState legacyState)
            {
                if (legacyState == FaunaBrain.AIState.Idle)
                    return 0u;

                uint packedState = (uint)FaunaWorldStateFlags.Active;
                switch (legacyState)
                {
                    case FaunaBrain.AIState.Investigate:
                    case FaunaBrain.AIState.Threaten:
                    case FaunaBrain.AIState.Stalk:
                    case FaunaBrain.AIState.Loom:
                    case FaunaBrain.AIState.Feint:
                    case FaunaBrain.AIState.Aggressive:
                        packedState |= (uint)FaunaWorldStateFlags.Hunting;
                        break;

                    case FaunaBrain.AIState.Escape:
                    case FaunaBrain.AIState.Return:
                    case FaunaBrain.AIState.ApexForcedRetreat:
                    case FaunaBrain.AIState.Retreat:
                        packedState |= (uint)FaunaWorldStateFlags.Fleeing;
                        break;
                }

                return packedState;
            }

            private static FaunaBrain.AIState MapPredatorState(PredatorUtilityState stateMask)
            {
                switch (stateMask)
                {
                    case PredatorUtilityState.Stalking:
                        return FaunaBrain.AIState.Stalk;
                    case PredatorUtilityState.Attacking:
                        return FaunaBrain.AIState.Aggressive;
                    case PredatorUtilityState.Fleeing:
                        return FaunaBrain.AIState.Retreat;
                    default:
                        return FaunaBrain.AIState.Wander;
                }
            }

            private static PackedCognitionOutput BuildDefaultPackedOutput(float3 fallbackForward)
            {
                PackedCognitionOutput output = default;
                output.DesiredDirection = fallbackForward;
                output.ForceMultiplier = 1f;
                output.SpeedMultiplier = 1f;
                output.TurnMultiplier = 1f;
                output.LegacyState = (int)FaunaBrain.AIState.Wander;
                return output;
            }

            private static PackedCognitionOutput BuildHeadlessPackedOutput(float3 fallbackForward)
            {
                PackedCognitionOutput output = BuildDefaultPackedOutput(fallbackForward);
                output.ForceMultiplier = 0f;
                output.SpeedMultiplier = 0f;
                output.TurnMultiplier = 0f;
                output.LegacyState = (int)FaunaBrain.AIState.Idle;
                output.OutputFlags = (uint)CognitionOutputFlags.EcoHeadless;
                return output;
            }
        }
    }
}
