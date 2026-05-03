using System.Runtime.InteropServices;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI
{
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
    internal struct CognitionCore
    {
        // 64-byte cognition core layout:
        // Position         -> offset  0, size 12
        // QuantizedDrives  -> offset 12, size  4 (Hunger/Aggression/Fear/Threat)
        // Velocity         -> offset 16, size 12
        // StateFlags       -> offset 28, size  4
        // QuantizedFatigue -> offset 48, size  4
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

    [StructLayout(LayoutKind.Sequential)]
    internal struct AcousticMemoryEntry
    {
        public float3 WorldPosition;
        public float Timestamp;
        public float Intensity;
        public int3 BucketCoord;
        public uint BucketHash;
    }

    [StructLayout(LayoutKind.Sequential)]
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
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CognitionInput
    {
        public float3 Position;
        public float3 Velocity;
        public float3 Forward;
        public float3 PlayerPosition;
        public float3 FloatingOriginOffset;
        public float3 PlayerVelocity;
        public float3 PlayerForward;
        public float3 ThreatPosition;
        public float3 RivalApexPosition;
        public float3 PreyPosition;
        public float3 ScavengePosition;
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
        public float CurrentTime;
        public float AcousticPingStrength01;
        public float AcousticTransmission01;
        public float ChemicalSignal01;
        public float ChemicalSensitivity;
        public float PlayerLightExposure01;
        public float LightFrenzySpeedMultiplier;
        public float LightReactionFearBoost01;
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
        public float ImportanceScore;
        public AbsoluteUniversePositionBlit128 PlayerTargetAup;
        public int SpeciesId;
        public int ClaimedBoidIndex;
        public int FlockCount;
        public int LightReactionMode;
        public int Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
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
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 48)]
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
        internal static readonly int CognitionCoreAlignmentBytes = UnsafeUtility.AlignOf<CognitionCore>();

        private const float HungerRate = 0.045f;
        private const float FatigueRate = 0.018f;
        private const float FearDecayLogK = -2.302585093f;
        private const float ThreatDecayLogK = -2.995732274f;
        private const float ThreatSmoothingK = 3f;
        private const float MaxThreatSmoothingDeltaTime = 0.05f;
        private const float MemoryLifetimeSeconds = 45f;
        private const float AcousticMemoryLifetimeSeconds = 45f;
        private const float MinimumDistanceMeters = 1.25f;
        private const float MinimumAttackCooldown = 0.35f;
        private const float MinimumScoreThreshold = 0.01f;
        private const float WanderTargetRefreshSeconds = 4.5f;
        private const float MaximumWanderVerticalOffset = 6f;
        private const float OverrideScoreBias = 1000f;
        private const float AttackStateBias = 1.25f;
        private const float PassiveLowHealthThreshold = 0.25f;
        private const float ScatterDurationSeconds = 3f;
        private const float AcousticBucketCellSize = 8f;
        private const float AcousticBucketHashBias = 0.15f;
        private const float AcousticStimulusThreshold = 0.015f;
        private const float ChemicalStimulusThreshold = 0.015f;
        private const float ChemicalSignalRangeMeters = 28f;
        private const float PredatorScentFollowThreshold = 0.1f;
        private const float FearPheromoneContagionShare = 0.3f;
        private const float FearPheromoneInjectionThreshold = 0.1f;
        private const float MinimumDetailedThreatImportanceScore = 0.2f;
        private const float CenterEvaluationIntervalSeconds = 1.0f / 60.0f;
        private const float FocusEvaluationIntervalSeconds = 1.0f / 30.0f;
        private const float PeripheryEvaluationIntervalSeconds = 1.0f / 20.0f;
        private const float FarEvaluationIntervalSeconds = 1.0f / 10.0f;
        private const float RearEvaluationIntervalSeconds = 1.0f / 5.0f;
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
        private const int MaxThreatDdaSteps = 4096;
        private const int EvaluationJobBatchSize = 32;
        private const int UnclaimedBoidSlot = -1;
        private const byte SolidThreatVoxel = 255;
        private const byte SignedDistanceSolidThreshold = 128;
        private const float QuantizedByteScale = 255f;
        private const float DdaEpsilon = 0.000001f;
        private const float PlayerFacingBaitThreshold = 0.45f;
        private const float PackFlankHoldDistanceMeters = 3.5f;
        private const float BaseSiegeEngageRadiusMeters = 220f;
        private const float BaseSiegeRammerStandoffMeters = 1.5f;
        private const float BaseSiegeDistractorLateralOffsetMeters = 18f;
        private const float BaseSiegeDistractorForwardOffsetMeters = 8f;
        private const float BaseSiegeLoiterRadiusMeters = 10f;
        private const float BaseSiegeUtilityBias = 0.35f;
        private const int SpatialMemoryRecallCount = 5;
        private const int MaxPackRoleCasAttempts = 3;
        private const string NativeMemoryOwner = nameof(PredatorCognitionDomain);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;

        private static NativeArray<CognitionCore> _cores;
        private static NativeArray<CognitionControl> _controls;
        private static NativeArray<CognitionInput> _inputs;
        private static NativeArray<PackedCognitionOutput> _outputs;
        private static NativeArray<CognitionMemoryEntry> _memoryBank;
        private static NativeArray<AcousticMemoryEntry> _acousticMemoryBank;
        private static NativeArray<byte> _slotUsed;
        private static NativeList<int> _activeSlots;
        private static NativeArray<float> _ambientThreats;
        private static NativeArray<float3> _swarmCenters;
        private static NativeArray<float3> _swarmDirections;
        private static NativeArray<float3> _swarmAvoidances;
        private static NativeArray<int> _swarmCounts;
        private static NativeArray<int> _claimedBoidIndices;
        private static NativeArray<float3> _claimedBoidPositions;
        private static NativeArray<int> _chosenStates;
        private static NativeArray<float3> _predatorPackTargets;
        private static NativeArray<float> _predatorPackWeights;
        private static NativeArray<float3> _predatorPackBaitPositions;
        private static NativeArray<float3> _predatorPackSharedPlayerPositions;
        private static NativeArray<byte> _predatorPackRoles;
        private static NativeArray<int> _boidClaimTable;
        private static NativeArray<int> _packBaitClaimTable;
        private static NativeArray<int> _packFlankerClaimTable;
        private static NativeArray<HabitatSiegeTargetSnapshot> _habitatSiegeTargets;
        private static NativeArray<int> _baseSiegeRammerClaimTable;
        private static NativeArray<int> _baseSiegeDistractorClaimTable;
        private static NativeArray<int> _baseSiegeLoitererClaimTable;
        private static NativeArray<byte> _evaluationDueFlags;
        private static NativeArray<float> _nextEvaluationTimes;
        private static NativeArray<float> _evaluationIntervals;
        private static NativeParallelHashMap<int, SpeciesCognitionTuning> _speciesTuningById;
        private static NativeArray<byte> _threatVoxelGrid;
        private static int3 _threatVoxelDimensions;
        private static float3 _threatVoxelOrigin;
        private static float3 _threatVoxelCellSize;
        private static byte _threatVoxelSolidThreshold = SolidThreatVoxel;
        private static bool _threatVoxelUsesSignedDistanceEncoding;
        private static NativeArray<float4> _chemicalGrid;
        private static NativeArray<float4> _chemicalOverlayGrid;
        private static int3 _chemicalGridDimensions;
        private static float3 _chemicalGridOrigin;
        private static float3 _chemicalGridCellSize;
        private static JobHandle _scheduledSwarmHandle;
        private static JobHandle _scheduledEvaluationHandle;
        private static bool _evaluationScheduled;
        private static int _lastEvaluatedFrame = -1;
        private static int _lastScheduledFrame = -1;
        private static int _lastThreatVoxelBindFrame = -1;
        private static int _lastChemicalGridBindFrame = -1;
        private static int _habitatSiegeTargetCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDomain()
        {
            Dispose();
        }

        internal static int Register()
        {
            EnsureInitialized();
            for (int i = 0; i < Capacity; i++)
            {
                if (_slotUsed[i] != 0)
                    continue;

                _slotUsed[i] = 1;
                _cores[i] = default;
                _controls[i] = default;
                _inputs[i] = default;
                _outputs[i] = default;
                _evaluationDueFlags[i] = 1;
                _chosenStates[i] = 0;
                _nextEvaluationTimes[i] = 0f;
                _evaluationIntervals[i] = CenterEvaluationIntervalSeconds;
                _predatorPackTargets[i] = float3.zero;
                _predatorPackWeights[i] = 0f;
                _predatorPackBaitPositions[i] = float3.zero;
                _predatorPackSharedPlayerPositions[i] = float3.zero;
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
            _speciesTuningById[speciesId] = tuning;
        }

        internal static void Unregister(int slot)
        {
            if (!IsValidSlot(slot))
                return;

            SetSlotActive(slot, false);
            _slotUsed[slot] = 0;
            _cores[slot] = default;
            _controls[slot] = default;
            _inputs[slot] = default;
            _outputs[slot] = default;
            _evaluationDueFlags[slot] = 0;
            _chosenStates[slot] = 0;
            _nextEvaluationTimes[slot] = 0f;
            _evaluationIntervals[slot] = CenterEvaluationIntervalSeconds;
            _predatorPackTargets[slot] = float3.zero;
            _predatorPackWeights[slot] = 0f;
            _predatorPackBaitPositions[slot] = float3.zero;
            _predatorPackSharedPlayerPositions[slot] = float3.zero;
            _predatorPackRoles[slot] = (byte)PredatorPackRole.None;
            ClearMemoryEntries(slot);
            ClearAcousticMemoryEntries(slot);
        }

        internal static void SetSlotActive(int slot, bool active)
        {
            if (!IsValidSlot(slot))
                return;

            bool currentlyActive = ContainsActiveSlot(slot);
            if (active == currentlyActive)
                return;

            if (active)
            {
                _evaluationDueFlags[slot] = 1;
                _nextEvaluationTimes[slot] = 0f;
                _evaluationIntervals[slot] = CenterEvaluationIntervalSeconds;
                _activeSlots.AddNoResize(slot);
                return;
            }

            _evaluationDueFlags[slot] = 0;
            _nextEvaluationTimes[slot] = 0f;
            _evaluationIntervals[slot] = CenterEvaluationIntervalSeconds;

            for (int i = 0; i < _activeSlots.Length; i++)
            {
                if (_activeSlots[i] != slot)
                    continue;

                _activeSlots.RemoveAtSwapBack(i);
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
            _evaluationDueFlags[slot] = 1;
            _chosenStates[slot] = 0;
            _nextEvaluationTimes[slot] = 0f;
            _evaluationIntervals[slot] = CenterEvaluationIntervalSeconds;
            _predatorPackTargets[slot] = float3.zero;
            _predatorPackWeights[slot] = 0f;
            _predatorPackBaitPositions[slot] = float3.zero;
            _predatorPackSharedPlayerPositions[slot] = float3.zero;
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

        internal static float ResolveHibernationTimeAsleepSeconds(float decodedSleepStartSeconds, float currentTime)
        {
            if (!math.isfinite(decodedSleepStartSeconds) || !math.isfinite(currentTime) || decodedSleepStartSeconds <= 0f)
                return 0f;

            return math.max(0f, currentTime - decodedSleepStartSeconds);
        }

        internal static float HibernationHungerRatePerSecond => HungerRate;

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

        internal static bool ApplyHibernationCatchUp(int slot, float sleepSeconds, float currentTime)
        {
            if (!IsValidSlot(slot) || !math.isfinite(sleepSeconds) || sleepSeconds <= 0f)
                return false;

            CognitionCore core = _cores[slot];
            UnpackDriveChannels(core.QuantizedDrives, out float hunger, out float aggression, out float fear, out float threatLevel);
            float fatigue = UnpackSingleDrive(core.QuantizedFatigue);
            hunger = math.clamp(hunger + (HungerRate * sleepSeconds), 0f, 1f);
            fatigue = math.clamp(fatigue + (FatigueRate * sleepSeconds), 0f, 1f);
            core.QuantizedDrives = PackDriveChannels(hunger, aggression, fear, threatLevel);
            core.QuantizedFatigue = PackSingleDrive(fatigue);
            _cores[slot] = core;

            bool forceHunt = hunger >= 0.999f;
            if (forceHunt)
                ApplyExternalState(slot, PredatorUtilityState.Attacking, currentTime);

            return forceHunt;
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
            if (math.lengthsq(output.DesiredDirection) <= 0.0001f)
                output.DesiredDirection = math.normalizesafe(fallbackForward, new float3(0f, 0f, 1f));
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

            _scheduledSwarmHandle = default;
            _evaluationScheduled = false;
            _lastEvaluatedFrame = _lastScheduledFrame;
        }

        internal static unsafe void ScheduleFrameEvaluation(int frameId)
        {
            if (!_activeSlots.IsCreated ||
                _activeSlots.Length <= 0 ||
                _evaluationScheduled ||
                _lastScheduledFrame == frameId)
            {
                return;
            }

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
            float3 swarmBoundsMin = ComputeSwarmBoundsMin();
            var swarmJob = new SwarmAnalysisJob
            {
                ActiveSlots = _activeSlots.AsArray(),
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
                PredatorPackRoles = _predatorPackRoles,
                PackBaitClaimTable = (int*)_packBaitClaimTable.GetUnsafePtr(),
                PackFlankerClaimTable = (int*)_packFlankerClaimTable.GetUnsafePtr(),
                SwarmBoundsMin = swarmBoundsMin
            };

            _scheduledSwarmHandle = swarmJob.Schedule(_activeSlots.Length, EvaluationJobBatchSize);
            var job = new PredatorCognitionJob
            {
                ActiveSlots = _activeSlots.AsArray(),
                Inputs = _inputs,
                Cores = _cores,
                Controls = _controls,
                MemoryBank = _memoryBank,
                AcousticMemoryBank = _acousticMemoryBank,
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
                PredatorPackRoles = _predatorPackRoles,
                HabitatSiegeTargets = _habitatSiegeTargets,
                HabitatSiegeTargetCount = _habitatSiegeTargetCount,
                BaseSiegeRammerClaimTable = (int*)_baseSiegeRammerClaimTable.GetUnsafePtr(),
                BaseSiegeDistractorClaimTable = (int*)_baseSiegeDistractorClaimTable.GetUnsafePtr(),
                BaseSiegeLoitererClaimTable = (int*)_baseSiegeLoitererClaimTable.GetUnsafePtr(),
                SpeciesTuningById = _speciesTuningById,
                ChosenStates = _chosenStates,
                BoidClaimTable = _boidClaimTable,
                Outputs = _outputs,
                ThreatVoxelGrid = _threatVoxelGrid,
                ThreatVoxelDimensions = _threatVoxelDimensions,
                ThreatVoxelOrigin = _threatVoxelOrigin,
                ThreatVoxelCellSize = _threatVoxelCellSize,
                ThreatVoxelSolidThreshold = _threatVoxelSolidThreshold,
                ThreatVoxelUsesSignedDistanceEncoding = _threatVoxelUsesSignedDistanceEncoding ? 1 : 0,
                ChemicalGrid = _chemicalGrid,
                ChemicalOverlayGrid = _chemicalOverlayGrid,
                ChemicalGridDimensions = _chemicalGridDimensions,
                ChemicalGridOrigin = _chemicalGridOrigin,
                ChemicalGridCellSize = _chemicalGridCellSize
            };

            _scheduledEvaluationHandle = job.Schedule(_activeSlots.Length, EvaluationJobBatchSize, _scheduledSwarmHandle);
            _evaluationScheduled = true;
            _lastScheduledFrame = frameId;
        }

        internal static void Dispose()
        {
            JobHandle disposeDependency = _evaluationScheduled ? _scheduledEvaluationHandle : default;
            DisposeNativeArray(ref _cores, disposeDependency);
            DisposeNativeArray(ref _controls, disposeDependency);
            DisposeNativeArray(ref _inputs, disposeDependency);
            DisposeNativeArray(ref _outputs, disposeDependency);
            DisposeNativeArray(ref _memoryBank, disposeDependency);
            DisposeNativeArray(ref _acousticMemoryBank, disposeDependency);
            DisposeNativeArray(ref _slotUsed, disposeDependency);
            DisposeNativeList(ref _activeSlots, disposeDependency, nameof(_activeSlots));
            DisposeNativeArray(ref _ambientThreats, disposeDependency);
            DisposeNativeArray(ref _swarmCenters, disposeDependency);
            DisposeNativeArray(ref _swarmDirections, disposeDependency);
            DisposeNativeArray(ref _swarmAvoidances, disposeDependency);
            DisposeNativeArray(ref _swarmCounts, disposeDependency);
            DisposeNativeArray(ref _claimedBoidIndices, disposeDependency);
            DisposeNativeArray(ref _claimedBoidPositions, disposeDependency);
            DisposeNativeArray(ref _chosenStates, disposeDependency);
            DisposeNativeArray(ref _predatorPackTargets, disposeDependency);
            DisposeNativeArray(ref _predatorPackWeights, disposeDependency);
            DisposeNativeArray(ref _predatorPackBaitPositions, disposeDependency);
            DisposeNativeArray(ref _predatorPackSharedPlayerPositions, disposeDependency);
            DisposeNativeArray(ref _predatorPackRoles, disposeDependency);
            DisposeNativeArray(ref _boidClaimTable, disposeDependency);
            DisposeNativeArray(ref _packBaitClaimTable, disposeDependency);
            DisposeNativeArray(ref _packFlankerClaimTable, disposeDependency);
            DisposeNativeArray(ref _habitatSiegeTargets, disposeDependency);
            DisposeNativeArray(ref _baseSiegeRammerClaimTable, disposeDependency);
            DisposeNativeArray(ref _baseSiegeDistractorClaimTable, disposeDependency);
            DisposeNativeArray(ref _baseSiegeLoitererClaimTable, disposeDependency);
            DisposeNativeArray(ref _evaluationDueFlags, disposeDependency);
            DisposeNativeArray(ref _nextEvaluationTimes, disposeDependency);
            DisposeNativeArray(ref _evaluationIntervals, disposeDependency);
            DisposeNativeParallelHashMap(ref _speciesTuningById, nameof(_speciesTuningById));

            _cores = default;
            _controls = default;
            _inputs = default;
            _outputs = default;
            _memoryBank = default;
            _acousticMemoryBank = default;
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
            _predatorPackTargets = default;
            _predatorPackWeights = default;
            _predatorPackBaitPositions = default;
            _predatorPackSharedPlayerPositions = default;
            _predatorPackRoles = default;
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
            _speciesTuningById = default;
            _threatVoxelGrid = default;
            _threatVoxelDimensions = int3.zero;
            _threatVoxelOrigin = float3.zero;
            _threatVoxelCellSize = new float3(1f, 1f, 1f);
            _threatVoxelSolidThreshold = SolidThreatVoxel;
            _threatVoxelUsesSignedDistanceEncoding = false;
            _chemicalGrid = default;
            _chemicalOverlayGrid = default;
            _chemicalGridDimensions = int3.zero;
            _chemicalGridOrigin = float3.zero;
            _chemicalGridCellSize = new float3(1f, 1f, 1f);
            _scheduledSwarmHandle = default;
            _scheduledEvaluationHandle = default;
            _evaluationScheduled = false;
            _lastEvaluatedFrame = -1;
            _lastScheduledFrame = -1;
            _lastThreatVoxelBindFrame = -1;
            _lastChemicalGridBindFrame = -1;
            _habitatSiegeTargetCount = 0;
        }

        private static void EnsureInitialized()
        {
            if (_cores.IsCreated)
                return;

            // COLD ALLOC: NativeArray<CognitionCore>[Capacity] - contiguous fauna cognition cores for all registered fauna brains - owner: PredatorCognitionDomain
            _cores = new NativeArray<CognitionCore>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<CognitionControl>[Capacity] - shared fauna behavior control bank paired with cognition cores - owner: PredatorCognitionDomain
            _controls = new NativeArray<CognitionControl>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<CognitionInput>[Capacity] - per-frame sensory snapshots for Burst cognition evaluation - owner: PredatorCognitionDomain
            _inputs = new NativeArray<CognitionInput>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<PackedCognitionOutput>[Capacity] - packed Burst cognition outputs decoded at the compatibility boundary - owner: PredatorCognitionDomain
            _outputs = new NativeArray<PackedCognitionOutput>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<CognitionMemoryEntry>[Capacity * MemorySlotsPerCreature] - shared fauna spatial memory bank for all registered fauna brains - owner: PredatorCognitionDomain
            _memoryBank = new NativeArray<CognitionMemoryEntry>(Capacity * MemorySlotsPerCreature, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<AcousticMemoryEntry>[Capacity * 5] - acoustic memory ring buffer per fauna slot - owner: PredatorCognitionDomain
            _acousticMemoryBank = new NativeArray<AcousticMemoryEntry>(Capacity * AcousticMemorySlotsPerCreature, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[Capacity] - slot usage bitmap for PredatorCognitionDomain registration - owner: PredatorCognitionDomain
            _slotUsed = new NativeArray<byte>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeList<int>[Capacity] - active fauna cognition slot list for Burst batch evaluation - owner: PredatorCognitionDomain
            _activeSlots = new NativeList<int>(Capacity, Allocator.Persistent);
            // COLD ALLOC: NativeArray<float>[Capacity] - same-bucket ambient threat cache for fear contagion - owner: PredatorCognitionDomain
            _ambientThreats = new NativeArray<float>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[Capacity] - burst-computed swarm cohesion centers - owner: PredatorCognitionDomain
            _swarmCenters = new NativeArray<float3>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[Capacity] - burst-computed swarm alignment directions - owner: PredatorCognitionDomain
            _swarmDirections = new NativeArray<float3>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[Capacity] - burst-computed separation/PBD avoidance vectors - owner: PredatorCognitionDomain
            _swarmAvoidances = new NativeArray<float3>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int>[Capacity] - burst-computed neighbor counts for flock-state weighting - owner: PredatorCognitionDomain
            _swarmCounts = new NativeArray<int>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int>[Capacity] - claimed prey slot indices per predator slot - owner: PredatorCognitionDomain
            _claimedBoidIndices = new NativeArray<int>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[Capacity] - claimed prey positions paired with predator slots - owner: PredatorCognitionDomain
            _claimedBoidPositions = new NativeArray<float3>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int>[Capacity] - chosen utility state code per fauna slot for post-job consumers and diagnostics - owner: PredatorCognitionDomain
            _chosenStates = new NativeArray<int>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[Capacity] - burst-computed predator flank targets for coordinated pack strikes - owner: PredatorCognitionDomain
            _predatorPackTargets = new NativeArray<float3>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[Capacity] - burst-computed predator flank weights for coordinated pack strikes - owner: PredatorCognitionDomain
            _predatorPackWeights = new NativeArray<float>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[Capacity] - resolved bait-predator positions shared with flankers - owner: PredatorCognitionDomain
            _predatorPackBaitPositions = new NativeArray<float3>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float3>[Capacity] - shared player positions reconstructed from AUP for coordinated pack strikes - owner: PredatorCognitionDomain
            _predatorPackSharedPlayerPositions = new NativeArray<float3>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[Capacity] - resolved pack roles per predator slot - owner: PredatorCognitionDomain
            _predatorPackRoles = new NativeArray<byte>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int>[Capacity] - atomic prey claim table keyed by creature slot - owner: PredatorCognitionDomain
            _boidClaimTable = new NativeArray<int>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int>[Capacity] - atomic bait-role reservation table keyed by stable species hash - owner: PredatorCognitionDomain
            _packBaitClaimTable = new NativeArray<int>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int>[Capacity] - atomic flanker-role reservation table keyed by stable species hash - owner: PredatorCognitionDomain
            _packFlankerClaimTable = new NativeArray<int>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<HabitatSiegeTargetSnapshot>[64] — copied base weak-point snapshot for Burst predator siege cognition — owner: PredatorCognitionDomain
            _habitatSiegeTargets = new NativeArray<HabitatSiegeTargetSnapshot>(HabitatGraphManager.MaxSiegeTargetCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int>[64] — atomic base-siege rammer reservation table keyed by habitat target index — owner: PredatorCognitionDomain
            _baseSiegeRammerClaimTable = new NativeArray<int>(HabitatGraphManager.MaxSiegeTargetCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int>[64] — atomic base-siege distractor reservation table keyed by habitat target index — owner: PredatorCognitionDomain
            _baseSiegeDistractorClaimTable = new NativeArray<int>(HabitatGraphManager.MaxSiegeTargetCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int>[64] — atomic base-siege loiterer reservation table keyed by habitat target index — owner: PredatorCognitionDomain
            _baseSiegeLoitererClaimTable = new NativeArray<int>(HabitatGraphManager.MaxSiegeTargetCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[Capacity] - per-slot due flags for foveated cognition cadence - owner: PredatorCognitionDomain
            _evaluationDueFlags = new NativeArray<byte>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[Capacity] - next allowed cognition evaluation timestamps per slot - owner: PredatorCognitionDomain
            _nextEvaluationTimes = new NativeArray<float>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[Capacity] - resolved cognition cadence intervals per slot - owner: PredatorCognitionDomain
            _evaluationIntervals = new NativeArray<float>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeParallelHashMap<int,SpeciesCognitionTuning>[Capacity] - species cognition tuning table keyed by stable species id - owner: PredatorCognitionDomain
            _speciesTuningById = new NativeParallelHashMap<int, SpeciesCognitionTuning>(Capacity, Allocator.Persistent);
            RegisterNativeMemorySentinel();
            ClearBoidClaims();
        }

        private static void RegisterNativeMemorySentinel()
        {
            NativeMemorySentinel.RegisterNativeArray(_cores, NativeMemoryOwner, nameof(_cores), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_controls, NativeMemoryOwner, nameof(_controls), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_inputs, NativeMemoryOwner, nameof(_inputs), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_outputs, NativeMemoryOwner, nameof(_outputs), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_memoryBank, NativeMemoryOwner, nameof(_memoryBank), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_acousticMemoryBank, NativeMemoryOwner, nameof(_acousticMemoryBank), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_slotUsed, NativeMemoryOwner, nameof(_slotUsed), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(_activeSlots, NativeMemoryOwner, nameof(_activeSlots), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_ambientThreats, NativeMemoryOwner, nameof(_ambientThreats), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_swarmCenters, NativeMemoryOwner, nameof(_swarmCenters), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_swarmDirections, NativeMemoryOwner, nameof(_swarmDirections), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_swarmAvoidances, NativeMemoryOwner, nameof(_swarmAvoidances), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_swarmCounts, NativeMemoryOwner, nameof(_swarmCounts), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_claimedBoidIndices, NativeMemoryOwner, nameof(_claimedBoidIndices), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_claimedBoidPositions, NativeMemoryOwner, nameof(_claimedBoidPositions), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_chosenStates, NativeMemoryOwner, nameof(_chosenStates), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_predatorPackTargets, NativeMemoryOwner, nameof(_predatorPackTargets), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_predatorPackWeights, NativeMemoryOwner, nameof(_predatorPackWeights), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_predatorPackBaitPositions, NativeMemoryOwner, nameof(_predatorPackBaitPositions), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_predatorPackSharedPlayerPositions, NativeMemoryOwner, nameof(_predatorPackSharedPlayerPositions), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_predatorPackRoles, NativeMemoryOwner, nameof(_predatorPackRoles), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_boidClaimTable, NativeMemoryOwner, nameof(_boidClaimTable), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_packBaitClaimTable, NativeMemoryOwner, nameof(_packBaitClaimTable), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_packFlankerClaimTable, NativeMemoryOwner, nameof(_packFlankerClaimTable), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_habitatSiegeTargets, NativeMemoryOwner, nameof(_habitatSiegeTargets), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_baseSiegeRammerClaimTable, NativeMemoryOwner, nameof(_baseSiegeRammerClaimTable), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_baseSiegeDistractorClaimTable, NativeMemoryOwner, nameof(_baseSiegeDistractorClaimTable), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_baseSiegeLoitererClaimTable, NativeMemoryOwner, nameof(_baseSiegeLoitererClaimTable), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_evaluationDueFlags, NativeMemoryOwner, nameof(_evaluationDueFlags), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_nextEvaluationTimes, NativeMemoryOwner, nameof(_nextEvaluationTimes), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_evaluationIntervals, NativeMemoryOwner, nameof(_evaluationIntervals), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeParallelHashMap(_speciesTuningById, NativeMemoryOwner, nameof(_speciesTuningById), NativeMemoryLifetime);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle disposeDependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose(disposeDependency);
            array = default;
        }

        private static void DisposeNativeList<T>(ref NativeList<T> list, JobHandle disposeDependency, string label) where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, label);
            list.Dispose(disposeDependency);
            list = default;
        }

        private static void DisposeNativeParallelHashMap<TKey, TValue>(ref NativeParallelHashMap<TKey, TValue> map, string label)
            where TKey : unmanaged, System.IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeParallelHashMap(NativeMemoryOwner, label);
            map.Dispose();
            map = default;
        }

        private static bool PrepareEvaluationDueFlags()
        {
            bool hasDueEvaluations = false;
            for (int i = 0; i < _activeSlots.Length; i++)
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
                float interval = ResolveEvaluationInterval(input.ImportanceScore);
                float previousInterval = math.max(_evaluationIntervals[slot], CenterEvaluationIntervalSeconds);
                float scheduledTime = _nextEvaluationTimes[slot];
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
            if (ChemicalInfluenceGrid.TryGetPublishedSnapshot(out NativeArray<float4> grid, out NativeArray<float4> overlayGrid, out int3 dimensions, out float3 origin, out float3 cellSize))
            {
                _chemicalGrid = grid;
                _chemicalOverlayGrid = overlayGrid;
                _chemicalGridDimensions = dimensions;
                _chemicalGridOrigin = origin;
                _chemicalGridCellSize = cellSize;
                return;
            }

            _chemicalGrid = default;
            _chemicalOverlayGrid = default;
            _chemicalGridDimensions = int3.zero;
            _chemicalGridOrigin = float3.zero;
            _chemicalGridCellSize = new float3(1f, 1f, 1f);
        }

        private static void EmitFearPheromones()
        {
            if (!_activeSlots.IsCreated || !_cores.IsCreated || !_inputs.IsCreated || !_outputs.IsCreated)
                return;

            for (int i = 0; i < _activeSlots.Length; i++)
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
                   state == FaunaBrain.AIState.Retreat;
        }

        private static bool ContainsActiveSlot(int slot)
        {
            if (!_activeSlots.IsCreated)
                return false;

            for (int i = 0; i < _activeSlots.Length; i++)
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
            if (!_acousticMemoryBank.IsCreated || slot < 0 || slot >= Capacity)
                return;

            int startIndex = slot * AcousticMemorySlotsPerCreature;
            for (int i = 0; i < AcousticMemorySlotsPerCreature; i++)
                _acousticMemoryBank[startIndex + i] = default;
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
            if (!_activeSlots.IsCreated || _activeSlots.Length <= 0)
                return float3.zero;

            float3 boundsMin = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
            for (int i = 0; i < _activeSlots.Length; i++)
            {
                int slot = _activeSlots[i];
                boundsMin = math.min(boundsMin, _inputs[slot].Position);
            }

            return boundsMin.x == float.MaxValue ? float3.zero : boundsMin;
        }

        private static int3 ResolveSpatialBucketCoordinates(float3 worldPosition, float3 boundsMin, float bucketCellSize)
        {
            float safeCellSize = math.max(bucketCellSize, 0.001f);
            float3 localPosition = math.max(worldPosition - boundsMin, float3.zero);
            return new int3(
                (int)math.floor(localPosition.x / safeCellSize),
                (int)math.floor(localPosition.y / safeCellSize),
                (int)math.floor(localPosition.z / safeCellSize));
        }

        private static int3 ResolveAcousticBucketCoordinates(float3 worldPosition, float bucketCellSize)
        {
            float safeCellSize = math.max(bucketCellSize, 0.001f);
            return new int3(
                (int)math.floor(worldPosition.x / safeCellSize),
                (int)math.floor(worldPosition.y / safeCellSize),
                (int)math.floor(worldPosition.z / safeCellSize));
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
            output.DesiredDirection = math.normalizesafe(fallbackForward, new float3(0f, 0f, 1f));
            output.ForceMultiplier = 1f;
            output.SpeedMultiplier = 1f;
            output.TurnMultiplier = 1f;
            output.LegacyState = (int)FaunaBrain.AIState.Wander;
            return output;
        }

        private static PackedCognitionOutput BuildDefaultPackedOutput(float3 fallbackForward)
        {
            PackedCognitionOutput output = default;
            output.DesiredDirection = math.normalizesafe(fallbackForward, new float3(0f, 0f, 1f));
            output.ForceMultiplier = 1f;
            output.SpeedMultiplier = 1f;
            output.TurnMultiplier = 1f;
            output.LegacyState = (int)FaunaBrain.AIState.Wander;
            return output;
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
            return math.saturate(lane / QuantizedByteScale);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct SwarmAnalysisJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> ActiveSlots;
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
            public NativeArray<byte> PredatorPackRoles;
            [NativeDisableUnsafePtrRestriction] public int* PackBaitClaimTable;
            [NativeDisableUnsafePtrRestriction] public int* PackFlankerClaimTable;
            public float3 SwarmBoundsMin;

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
                float packCoordinationRadius = math.max(0f, input.PackCoordinationRadius);
                float packFlankDistance = math.max(0f, input.PackFlankDistance);
                bool canCoordinatePack = canClaimBoid &&
                                         packCoordinationRadius > DdaEpsilon &&
                                         packFlankDistance > DdaEpsilon;
                float3 predatorPackSharedPlayerPosition = selfHasPlayerTarget
                    ? ResolveRuntimePosition(in input.PlayerTargetAup, input.FloatingOriginOffset)
                    : input.PlayerPosition;
                float3 predatorPackTarget = predatorPackSharedPlayerPosition;
                float predatorPackWeight = 0f;
                float3 predatorPackBaitPosition = input.Position;
                PredatorPackRole predatorPackRole = PredatorPackRole.None;
                int bestBaitSlot = selfHasPlayerTarget ? slot : -1;
                float bestBaitDistanceSq = selfHasPlayerTarget ? input.DistanceToPlayerSqr : float.MaxValue;
                float perceptionRadiusSq = SwarmPerceptionRadius * SwarmPerceptionRadius;
                float separationRadiusSq = SwarmSeparationRadius * SwarmSeparationRadius;
                int maxNeighborIterations = math.min(ActiveSlots.Length, MaxSwarmNeighborIterations);
                for (int i = 0; i < maxNeighborIterations; i++)
                {
                    int otherSlot = ActiveSlots[i];
                    CognitionInput otherInput = Inputs[otherSlot];
                    if ((otherInput.Flags & (int)CognitionInputFlags.Active) == 0)
                        continue;

                    int3 otherContagionBucket = ResolveSpatialBucketCoordinates(otherInput.Position, SwarmBoundsMin, ContagionBucketCellSize);
                    if (math.all(otherContagionBucket == contagionBucket))
                    {
                        threatSum += UnpackThreatLevel(PriorCores[otherSlot].QuantizedDrives);
                        threatCount++;
                    }

                    int3 otherSwarmBucket = ResolveSpatialBucketCoordinates(otherInput.Position, SwarmBoundsMin, SwarmBucketCellSize);
                    int3 bucketDelta = math.abs(otherSwarmBucket - selfSwarmBucket);
                    if (math.any(bucketDelta > 1))
                        continue;

                    if (otherSlot == slot)
                        continue;

                    float3 diff = input.Position - otherInput.Position;
                    float distSq = math.lengthsq(diff);
                    if (distSq <= DdaEpsilon || distSq > perceptionRadiusSq)
                        continue;

                    float invDist = math.rsqrt(math.max(distSq, DdaEpsilon));
                    float dist = distSq * invDist;
                    bool sameSpecies = otherInput.SpeciesId == input.SpeciesId;
                    if (sameSpecies)
                    {
                        float inSeparation = math.select(0f, 1f, distSq < separationRadiusSq);
                        separationForce += (diff / math.max(distSq, DdaEpsilon)) * inSeparation;
                        alignmentSum += otherInput.Velocity;
                        cohesionSum += otherInput.Position;
                        neighbourCount++;

                        if (dist < SwarmPbdMinDistance && dist > DdaEpsilon)
                        {
                            float3 dir = diff * invDist;
                            pbdCorrection += dir * ((SwarmPbdMinDistance - dist) * 0.5f);
                        }
                    }

                    if (canCoordinatePack &&
                        sameSpecies &&
                        (otherInput.Flags & (int)CognitionInputFlags.PredatorRole) != 0 &&
                        (otherInput.Flags & (int)CognitionInputFlags.HasPlayerTarget) != 0 &&
                        (PriorCores[otherSlot].StateFlags & (uint)FaunaWorldStateFlags.Hunting) != 0u)
                    {
                        float3 otherPlayerPosition = ResolveRuntimePosition(in otherInput.PlayerTargetAup, otherInput.FloatingOriginOffset);
                        float otherDistanceToPlayerSq = math.lengthsq(otherPlayerPosition - otherInput.Position);
                        if (otherDistanceToPlayerSq < bestBaitDistanceSq)
                        {
                            bestBaitDistanceSq = otherDistanceToPlayerSq;
                            bestBaitSlot = otherSlot;
                        }

                        float coordinationDistance = distSq * invDist;
                        float coordinationWeight = 1f - math.saturate(coordinationDistance / packCoordinationRadius);
                        if (coordinationWeight > predatorPackWeight)
                        {
                            float3 playerForward = math.normalizesafe(otherInput.PlayerForward, math.normalizesafe(otherInput.Forward, new float3(0f, 0f, 1f)));
                            float3 packRight = math.normalizesafe(
                                math.cross(new float3(0f, 1f, 0f), playerForward),
                                math.cross(new float3(0f, 0f, 1f), playerForward));
                            if (math.lengthsq(packRight) > DdaEpsilon)
                            {
                                float sideSign = math.select(-1f, 1f, math.dot(input.Position - otherPlayerPosition, packRight) >= 0f);
                                predatorPackSharedPlayerPosition = otherPlayerPosition;
                                predatorPackTarget = otherPlayerPosition + (packRight * (packFlankDistance * sideSign));
                                predatorPackBaitPosition = otherInput.Position;
                                predatorPackWeight = coordinationWeight;
                            }
                        }
                    }

                    if (!canClaimBoid || (otherInput.Flags & (int)CognitionInputFlags.PredatorRole) != 0 || distSq >= bestClaimDistanceSq)
                        continue;

                    claimedSlot = otherSlot;
                    claimedPosition = otherInput.Position;
                    bestClaimDistanceSq = distSq;
                }

                AmbientThreats[slot] = threatCount > 0 ? math.saturate(threatSum / threatCount) : 0f;
                ClaimedBoidIndices[slot] = claimedSlot;
                ClaimedBoidPositions[slot] = claimedPosition;

                if (canCoordinatePack)
                {
                    int reservationIndex = ResolvePackReservationIndex(input.SpeciesId);
                    if (bestBaitSlot == slot && selfHasPlayerTarget)
                    {
                        if (TryReservePackRole(PackBaitClaimTable, reservationIndex, slot))
                        {
                            predatorPackRole = PredatorPackRole.Bait;
                            predatorPackWeight = math.max(predatorPackWeight, 1f);
                            predatorPackSharedPlayerPosition = ResolveRuntimePosition(in input.PlayerTargetAup, input.FloatingOriginOffset);
                            predatorPackTarget = predatorPackSharedPlayerPosition;
                            predatorPackBaitPosition = input.Position;
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
                    float invNeighbourCount = 1f / neighbourCount;
                    float3 averageVelocity = alignmentSum * invNeighbourCount;
                    float3 centerOfMass = cohesionSum * invNeighbourCount;
                    float3 alignmentForce = averageVelocity - input.Velocity;
                    float3 cohesionForce = centerOfMass - input.Position;
                    float3 acceleration =
                        (separationForce * SwarmSeparationWeight) +
                        (alignmentForce * SwarmAlignmentWeight) +
                        (cohesionForce * SwarmCohesionWeight) +
                        (pbdCorrection * SwarmPbdWeight);

                    float accelerationLengthSq = math.lengthsq(acceleration);
                    if (accelerationLengthSq > (SwarmMaxForce * SwarmMaxForce))
                        acceleration *= math.rsqrt(accelerationLengthSq) * SwarmMaxForce;

                    swarmCenter = centerOfMass;
                    swarmDirection = math.normalizesafe(averageVelocity, math.normalizesafe(input.FlockDirection, new float3(0f, 0f, 1f)));
                    swarmAvoidance = math.normalizesafe(acceleration, math.normalizesafe(input.FlockAvoidance, float3.zero));
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
                PredatorPackRoles[slot] = (byte)predatorPackRole;
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

            private static float3 ResolveRuntimePosition(in AbsoluteUniversePositionBlit128 positionAup, float3 floatingOriginOffset)
            {
                double cellSize = AbsoluteUniversePosition.CellSizeMeters;
                double3 absolutePosition = new double3(
                    (positionAup.GridX * cellSize) + positionAup.Local.x,
                    (positionAup.GridY * cellSize) + positionAup.Local.y,
                    (positionAup.GridZ * cellSize) + positionAup.Local.z);
                double3 runtimePosition = absolutePosition - new double3(floatingOriginOffset.x, floatingOriginOffset.y, floatingOriginOffset.z);
                return new float3((float)runtimePosition.x, (float)runtimePosition.y, (float)runtimePosition.z);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct PredatorCognitionJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> ActiveSlots;
            [ReadOnly] public NativeArray<CognitionInput> Inputs;
            public NativeArray<CognitionCore> Cores;
            public NativeArray<CognitionControl> Controls;
            [ReadOnly] public NativeArray<CognitionMemoryEntry> MemoryBank;
            [ReadOnly] public NativeArray<AcousticMemoryEntry> AcousticMemoryBank;
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
            [ReadOnly] public NativeArray<byte> PredatorPackRoles;
            [ReadOnly] public NativeArray<HabitatSiegeTargetSnapshot> HabitatSiegeTargets;
            public int HabitatSiegeTargetCount;
            [NativeDisableUnsafePtrRestriction] public int* BaseSiegeRammerClaimTable;
            [NativeDisableUnsafePtrRestriction] public int* BaseSiegeDistractorClaimTable;
            [NativeDisableUnsafePtrRestriction] public int* BaseSiegeLoitererClaimTable;
            [ReadOnly] public NativeParallelHashMap<int, SpeciesCognitionTuning> SpeciesTuningById;
            [NativeDisableParallelForRestriction] public NativeArray<int> ChosenStates;
            [NativeDisableParallelForRestriction] public NativeArray<int> BoidClaimTable;
            public NativeArray<PackedCognitionOutput> Outputs;
            [ReadOnly] public NativeArray<byte> ThreatVoxelGrid;
            public int3 ThreatVoxelDimensions;
            public float3 ThreatVoxelOrigin;
            public float3 ThreatVoxelCellSize;
            public byte ThreatVoxelSolidThreshold;
            public int ThreatVoxelUsesSignedDistanceEncoding;
            [ReadOnly] public NativeArray<float4> ChemicalGrid;
            [ReadOnly] public NativeArray<float4> ChemicalOverlayGrid;
            public int3 ChemicalGridDimensions;
            public float3 ChemicalGridOrigin;
            public float3 ChemicalGridCellSize;

            public void Execute(int index)
            {
                int slot = ActiveSlots[index];
                CognitionInput input = Inputs[slot];
                float3 fallbackForward = math.normalizesafe(input.Forward, new float3(0f, 0f, 1f));
                if ((input.Flags & (int)CognitionInputFlags.Active) == 0)
                {
                    Outputs[slot] = BuildDefaultPackedOutput(fallbackForward);
                    ChosenStates[slot] = 0;
                    return;
                }

                if (DueFlags[slot] == 0)
                    return;

                if (input.SpeciesId != 0 &&
                    SpeciesTuningById.IsCreated &&
                    SpeciesTuningById.TryGetValue(input.SpeciesId, out SpeciesCognitionTuning tuning))
                {
                    input.HungerWeight = tuning.HungerWeight;
                    input.FearWeight = tuning.FearWeight;
                    input.CuriosityWeight = tuning.CuriosityWeight;
                    input.LightReactionMode = (int)tuning.LightReactionMode;
                    input.LightFrenzySpeedMultiplier = tuning.LightFrenzySpeedMultiplier;
                    input.LightReactionFearBoost01 = tuning.LightReactionFearBoost01;
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
                hunger = math.clamp(hunger + (HungerRate * dt), 0f, 1f);
                fatigue = math.clamp(fatigue + (FatigueRate * dt), 0f, 1f);
                aggression = math.clamp(input.AggressionWeight, 0f, 1f);
                float ambientThreat = AmbientThreats[slot];
                fear = math.clamp((fear * math.exp(FearDecayLogK * dt)) + (ambientThreat * dt), 0f, 1f);
                threatLevel = math.clamp(math.max(threatLevel * math.exp(ThreatDecayLogK * dt), ambientThreat), 0f, 1f);

                if ((input.Flags & (int)CognitionInputFlags.HasScatterDirection) != 0)
                {
                    control.ScatterDirection = math.normalizesafe(input.ScatterDirection, fallbackForward);
                    control.ScatterUntilTime = input.CurrentTime + ScatterDurationSeconds;
                }

                bool isPredator = (input.Flags & (int)CognitionInputFlags.PredatorRole) != 0;
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

                bool playerVisible = hasPlayerTarget && hasVisualPlayerHint && ResolveThreatVisibility(resolvedInput.Position, resolvedInput.PlayerPosition, resolvedInput.ImportanceScore);
                bool threatVisible = hasThreatTarget && ResolveThreatVisibility(resolvedInput.Position, resolvedInput.ThreatPosition, resolvedInput.ImportanceScore);
                bool rivalApexVisible = hasApexRivalTarget &&
                                        isApexPredator &&
                                        ResolveThreatVisibility(resolvedInput.Position, resolvedInput.RivalApexPosition, resolvedInput.ImportanceScore);
                bool preyVisible = (hasPreyTarget || resolvedInput.ClaimedBoidIndex >= 0) && ResolveThreatVisibility(resolvedInput.Position, resolvedInput.PreyPosition, resolvedInput.ImportanceScore);
                bool scavengeVisible = hasScavengeTarget && ResolveThreatVisibility(resolvedInput.Position, resolvedInput.ScavengePosition, resolvedInput.ImportanceScore);
                if (playerVisible)
                    control.LastVisualContactTime = input.CurrentTime;

                TryResolveChemicalGradient(resolvedInput.Position, out _, out float fearPheromoneSignal, out _);
                float rawFear = math.saturate((1f - math.saturate(input.HealthNormalized)) + input.FearPressure01);
                fear = math.max(fear, math.max(rawFear, fearPheromoneSignal * FearPheromoneContagionShare));

                PackedCognitionOutput output = isPredator
                    ? EvaluatePredator(slot, ref core, ref control, in resolvedInput, fallbackForward, canFlee, hasPlayerTarget, playerVisible, threatVisible, rivalApexVisible, preyVisible, scavengeVisible, aggression, ref hunger, ref fatigue, ref fear, ref threatLevel)
                    : EvaluatePassive(slot, ref control, in resolvedInput, fallbackForward, canFlee, hasPlayerTarget, playerVisible, threatVisible, useHomeTerritory, isFlocking, ref hunger, ref fatigue, ref fear, ref threatLevel);

                core.StateFlags = PackWorldStateFlags((FaunaBrain.AIState)output.LegacyState);
                core.QuantizedDrives = PackDriveChannels(hunger, aggression, fear, threatLevel);
                core.QuantizedFatigue = PackSingleDrive(fatigue);
                Cores[slot] = core;
                Controls[slot] = control;
                Outputs[slot] = output;
                ChosenStates[slot] = output.StateMask != 0u ? (int)output.StateMask : output.LegacyState;
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
                float3 predictedPlayerPosition = hasPlayerTarget
                    ? ResolvePredictedPlayerIntercept(input, aggression)
                    : sharedPackPlayerPosition;
                float directAcousticScore = hasPlayerTarget
                    ? ComputeAcousticScore(input.Position, input.PlayerPosition, input.AcousticPingStrength01, input.AcousticTransmission01)
                    : 0f;
                float packFlankWeight = PredatorPackWeights[slot];
                float3 packFlankTarget = PredatorPackTargets[slot];
                float3 packBaitPosition = PredatorPackBaitPositions[slot];
                PredatorPackRole packRole = (PredatorPackRole)PredatorPackRoles[slot];
                bool hasAcousticMemory = TryResolveStrongestAcousticMemory(slot, input.Position, input.CurrentTime, out float3 acousticMemoryPosition, out float acousticMemoryScore);
                float acousticScore = math.max(directAcousticScore, acousticMemoryScore);
                bool hasScavengeTarget = (input.Flags & (int)CognitionInputFlags.HasScavengeTarget) != 0;
                bool isApexPredator = (input.Flags & (int)CognitionInputFlags.IsApexPredator) != 0;
                bool isAmbusher = (input.Flags & (int)CognitionInputFlags.IsAmbusher) != 0;
                bool hasApexRivalTarget = (input.Flags & (int)CognitionInputFlags.HasApexRivalTarget) != 0;
                bool hasChemicalTrail = TryResolveChemicalGradient(input.Position, out float attractantSignal, out float fearPheromoneSignal, out float3 scentGradient);
                bool lightAversionActive = IsLightAversionActive(input);
                bool lightFrenzyActive = IsLightFrenzyActive(input) && hasPlayerTarget;
                if (lightAversionActive)
                {
                    control.OverrideThreatPosition = input.PlayerPosition;
                    control.Flags |= (int)CognitionControlFlags.HasOverrideThreatPosition;
                    fear = math.max(fear, input.PlayerLightExposure01 * math.max(input.LightReactionFearBoost01, 0.1f));
                    threatLevel = math.max(threatLevel, input.PlayerLightExposure01);
                }

                bool hasBaseSiegeTarget = TryResolveBaseSiegeTarget(
                    slot,
                    in input,
                    fallbackForward,
                    predictedPlayerPosition,
                    (isApexPredator || input.PackCoordinationRadius > DdaEpsilon) &&
                    !playerVisible &&
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
                else if (playerVisible)
                {
                    targetPosition = predictedPlayerPosition;
                    hasTarget = true;
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
                    targetPosition = input.Position + (math.normalizesafe(scentGradient, fallbackForward) * math.cmax(ChemicalGridCellSize));
                    hasTarget = true;
                    threatLevel = math.max(threatLevel, chemicalScore * 0.65f);
                }
                else if (hasScavengeTarget && chemicalScore > ChemicalStimulusThreshold)
                {
                    targetPosition = input.ScavengePosition;
                    hasTarget = true;
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
                                    !resolvedPreyVisible;
                bool playerFacingBait = false;
                bool flankingManeuverDetected = false;
                if (usePackFlank)
                {
                    float3 playerToBait = math.normalizesafe(packBaitPosition - predictedPlayerPosition, float3.zero);
                    float3 playerForward = math.normalizesafe(input.PlayerForward, float3.zero);
                    playerFacingBait = math.lengthsq(playerForward) > DdaEpsilon &&
                                       math.lengthsq(playerToBait) > DdaEpsilon &&
                                       math.dot(playerForward, playerToBait) >= PlayerFacingBaitThreshold;
                    float3 packPredictionDelta = predictedPlayerPosition - input.PlayerPosition;
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

                float threatVisual = 0f;
                if (isApexPredator && rivalApexVisible)
                    threatVisual = ComputeThreatVisual(input.Position, input.RivalApexPosition, fallbackForward, math.max(input.AttackRange, input.ApexTerritoryRadius * 0.2f));
                else if (playerVisible)
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
                float attackCommit01 = hasTarget
                    ? ScoreHunger(math.saturate(1f - (math.sqrt(math.max(targetDistanceSq, 0f)) / math.max(input.AttackRange, 1f))))
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
                float stalkingScore = huntUtility * math.lerp(0.55f, 0.95f, Pow01(targetSignal, 1.2f));
                float attackingScore = huntUtility *
                                       math.lerp(0.25f, 1f, Pow01(attackCommit01, 1.5f)) *
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
                    stalkingScore *= math.select(1.35f, 0.9f, playerVisible);
                    attackingScore *= math.select(0.35f, 1.15f, playerVisible);
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

                float4 stateScores = new float4(prowlingScore, stalkingScore, attackingScore, fleeingScore);
                float winningScore = math.cmax(stateScores);
                int winningMask = BuildWinningStateMask(stateScores, winningScore);
                int stateCode = DecodePredatorStateCode(winningMask);
                stateCode = math.select(stateCode, (int)PredatorUtilityState.Prowling, winningScore < MinimumScoreThreshold);
                PredatorUtilityState stateMask = (PredatorUtilityState)stateCode;
                bool wasHunting = control.LastPredatorStateCode == (int)PredatorUtilityState.Stalking ||
                                  control.LastPredatorStateCode == (int)PredatorUtilityState.Attacking;

                bool wantsBoidClaim =
                    stateMask == PredatorUtilityState.Attacking &&
                    input.ClaimedBoidIndex >= 0 &&
                    !playerVisible &&
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

                float3 desiredDirection = ResolvePredatorDirection(stateMask, input.Position, targetPosition, fallbackForward, input.CurrentTime, control);
                PackedCognitionOutput output = default;
                output.DesiredDirection = desiredDirection;
                output.PackedScores = PackScoreTriplet(hungerScore, aggressionWeight, fearScore);
                output.StateMask = (uint)stateMask;
                output.LegacyState = satedActive ? (int)FaunaBrain.AIState.Sated : (int)MapPredatorState(stateMask);
                output.ForceMultiplier = 1f;
                output.SpeedMultiplier = 1f;
                output.TurnMultiplier = 1f;
                output.OutputFlags = 0u;
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
                        bool canStrikeLiveTarget = (playerVisible || lightFrenzyActive || resolvedPreyVisible || scavengeVisible || rivalApexVisible) &&
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

            private float3 ResolvePredictedPlayerIntercept(in CognitionInput input, float aggression)
            {
                float predatorSpeed = math.max(
                    1f,
                    math.max(math.length(input.Velocity), input.AttackRange * 0.65f) * math.max(1f, aggression));
                float distanceToPlayer = math.sqrt(math.max(input.DistanceToPlayerSqr, MinimumDistanceMeters * MinimumDistanceMeters));
                float interceptTime = math.clamp(distanceToPlayer / predatorSpeed, 0f, 3f);
                return input.PlayerPosition + (input.PlayerVelocity * interceptTime);
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
                float playerDistance = math.sqrt(math.max(input.DistanceToPlayerSqr, MinimumDistanceMeters * MinimumDistanceMeters));
                float playerThreat = hasPlayerTarget
                    ? math.saturate(1f - (playerDistance / math.max(input.EscapeSafeDistance, 1f)))
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
                TryResolveChemicalGradient(input.Position, out _, out float fearPheromoneSignal, out _);
                float threatVisual = playerVisible
                    ? ComputeThreatVisual(input.Position, input.PlayerPosition, fallbackForward, math.max(input.EscapeSafeDistance, 1f))
                    : 0f;
                if (threatVisible)
                    threatVisual = math.max(threatVisual, ComputeThreatVisual(input.Position, input.ThreatPosition, fallbackForward, math.max(input.EscapeSafeDistance, 1f)));

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
                bool homeOutOfBounds = useHomeTerritory &&
                                       input.PatrolRadius > 0f &&
                                       math.lengthsq(input.Position - control.SpawnAnchor) > (input.PatrolRadius * input.PatrolRadius);
                bool shouldEscape = retreatForced ||
                                    (canFlee && lightAversionActive) ||
                                    (canFlee && hasPlayerTarget && (input.DistanceToPlayerSqr <= input.EscapeDistance * input.EscapeDistance || lowHealth || threatLevel >= 0.35f)) ||
                                    (canFlee && (threatVisible || acousticScore > AcousticStimulusThreshold));

                float fatigueScore = ScoreFatigue(fatigue);
                float curiosityWeight = math.max(0.1f, input.CuriosityWeight);
                float escapeScore = ScoreFear(math.max(fear, threatLevel)) * math.max(0.1f, input.FearWeight) * math.select(0f, 1f, shouldEscape);
                float homeDistance01 = useHomeTerritory && input.PatrolRadius > 0f
                    ? math.saturate(math.sqrt(math.lengthsq(input.Position - control.SpawnAnchor)) / math.max(input.PatrolRadius, 1f))
                    : 0f;
                float returnScore = ScoreThreat(homeDistance01) * math.select(0f, 1f, homeOutOfBounds && !shouldEscape && !satedActive);
                float scatterScore = math.select(0f, OverrideScoreBias + ScoreThreat(math.max(acousticScore, threatLevel)), scatterActive);
                float flockingScore = ScoreThreat(math.saturate((float)input.FlockCount / 6f)) * math.select(0f, math.max(0.25f, 1f - escapeScore), isFlocking && input.FlockCount > 1 && !scatterActive && !satedActive && !shouldEscape && !homeOutOfBounds);
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
                    desiredDirection = math.normalizesafe(control.WanderTarget - input.Position, fallbackForward);
                    speedMultiplier = 0.6f;
                    turnMultiplier = 0.5f;
                }
                else if (state == FaunaBrain.AIState.Retreat || state == FaunaBrain.AIState.Escape)
                {
                    float3 fleeFrom = input.PlayerPosition;
                    if ((control.Flags & (int)CognitionControlFlags.HasOverrideThreatPosition) != 0 && control.OverrideUntilTime > input.CurrentTime)
                        fleeFrom = control.OverrideThreatPosition;
                    else if (threatVisible)
                        fleeFrom = input.ThreatPosition;
                    else if (hasAcousticMemory && acousticMemoryScore > AcousticStimulusThreshold)
                        fleeFrom = acousticMemoryPosition;
                    desiredDirection = math.normalizesafe(input.Position - fleeFrom, -fallbackForward);
                    forceMultiplier = 2.35f;
                    speedMultiplier = math.max(1.2f, input.FearWeight);
                    turnMultiplier = 1.15f;
                }
                else if (state == FaunaBrain.AIState.Flocking && scatterActive)
                {
                    desiredDirection = math.normalizesafe(control.ScatterDirection, fallbackForward);
                    forceMultiplier = 4f;
                    speedMultiplier = 2f;
                    turnMultiplier = 1.2f;
                }
                else if (state == FaunaBrain.AIState.Return)
                {
                    desiredDirection = math.normalizesafe(control.SpawnAnchor - input.Position, fallbackForward);
                }
                else if (state == FaunaBrain.AIState.Flocking && isFlocking && input.FlockCount > 1)
                {
                    float3 cohesion = math.normalizesafe(input.FlockCenter - input.Position, float3.zero);
                    desiredDirection = math.normalizesafe(cohesion + input.FlockDirection + input.FlockAvoidance, fallbackForward);
                }
                else
                {
                    float wanderRadius = useHomeTerritory ? math.max(1f, input.PatrolRadius) : math.max(1f, input.WanderRadius);
                    float3 wanderCenter = useHomeTerritory ? control.SpawnAnchor : input.Position;
                    RefreshWanderTarget(ref control, input.CurrentTime, wanderCenter, wanderRadius);
                    desiredDirection = math.normalizesafe(control.WanderTarget - input.Position, fallbackForward);
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
                    float distance = math.sqrt(distanceSq);
                    float range01 = 1f - math.saturate(distance / BaseSiegeEngageRadiusMeters);
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

                float3 toWeakPoint = math.normalizesafe(bestTarget.WeakPoint - input.Position, fallbackForward);
                float3 moduleToPlayer = math.normalizesafe(predictedPlayerPosition - bestTarget.ModuleCenter, fallbackForward);
                if (math.lengthsq(moduleToPlayer) <= DdaEpsilon)
                    moduleToPlayer = math.normalizesafe(input.PlayerPosition - bestTarget.ModuleCenter, fallbackForward);

                float3 lateral = math.normalizesafe(
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
                    float3 loiterDirection = math.normalizesafe(input.Position - bestTarget.ModuleCenter, -moduleToPlayer);
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
                bool4 matches = scores >= threshold;
                bool pickProwling = matches.x;
                bool pickStalking = !pickProwling && matches.y;
                bool pickAttacking = !pickProwling && !pickStalking && matches.z;
                bool pickFleeing = !pickProwling && !pickStalking && !pickAttacking && matches.w;

                int winningMask = 0;
                winningMask |= math.select(0, 1 << 0, pickProwling);
                winningMask |= math.select(0, 1 << 1, pickStalking);
                winningMask |= math.select(0, 1 << 2, pickAttacking);
                winningMask |= math.select(0, 1 << 3, pickFleeing);
                return winningMask;
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
                    AcousticMemoryEntry entry = AcousticMemoryBank[startIndex + i];
                    float age = currentTime - entry.Timestamp;
                    if (age < 0f ||
                        age > AcousticMemoryLifetimeSeconds ||
                        entry.Intensity <= 0f)
                    {
                        continue;
                    }

                    float decayWeight = 1f - math.saturate(age / AcousticMemoryLifetimeSeconds);
                    decayWeight *= decayWeight;

                    int3 bucketDelta = math.abs(selfBucket - entry.BucketCoord);
                    float bucketDistanceSq = math.dot((float3)bucketDelta, (float3)bucketDelta);
                    float bucketWeight = math.rcp(1f + bucketDistanceSq);
                    float hashWeight = math.select(1f, 1f + AcousticBucketHashBias, entry.BucketHash == selfBucketHash);
                    float candidateScore = entry.Intensity * decayWeight * bucketWeight * hashWeight;
                    if (candidateScore <= bestScore)
                        continue;

                    bestScore = candidateScore;
                    bestPosition = entry.WorldPosition;
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

                    float decayWeight = 1f - math.saturate(age / MemoryLifetimeSeconds);
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

                position = weightedPosition / totalWeight;
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

                    float decayWeight = 1f - math.saturate(age / MemoryLifetimeSeconds);
                    decayWeight *= decayWeight;
                    float3 toMemory = entry.WorldPosition - currentPosition;
                    float distanceWeight = math.rsqrt(math.max(math.lengthsq(toMemory), 1f));
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

                int3 selfBucket = ResolveAcousticBucketCoordinates(selfPosition, AcousticBucketCellSize);
                int3 sourceBucket = ResolveAcousticBucketCoordinates(sourcePosition, AcousticBucketCellSize);
                uint selfBucketHash = HashAcousticBucket(selfBucket);
                uint sourceBucketHash = HashAcousticBucket(sourceBucket);
                int3 bucketDelta = math.abs(selfBucket - sourceBucket);
                float bucketDistanceSq = math.dot((float3)bucketDelta, (float3)bucketDelta);
                float hashWeight = math.select(1f, 1f + AcousticBucketHashBias, selfBucketHash == sourceBucketHash);
                return math.saturate((strength * hashWeight) / (1f + bucketDistanceSq));
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
                    directScore = math.saturate(directChemicalSignal01) * math.saturate(1f / (1f + (distanceSq / math.max(rangeSq, 1f))));
                }

                if (TryResolveStrongestMemory(slot, selfPosition, currentTime, (int)CognitionStimulusType.Chemical, out _, out float memoryScore))
                    directScore = math.max(directScore, memoryScore);

                return math.saturate(directScore * math.max(0f, chemicalSensitivity));
            }

            private bool TryResolveChemicalGradient(float3 worldPosition, out float attractantSignal, out float fearSignal, out float3 gradient)
            {
                attractantSignal = 0f;
                fearSignal = 0f;
                gradient = float3.zero;
                if (!ChemicalGrid.IsCreated ||
                    ChemicalGrid.Length <= 0 ||
                    ChemicalGridDimensions.x <= 0 ||
                    ChemicalGridDimensions.y <= 0 ||
                    ChemicalGridDimensions.z <= 0 ||
                    !TryWorldToChemicalCell(worldPosition, out int3 cell))
                {
                    return false;
                }

                attractantSignal = SampleChemicalAttractant(cell);
                fearSignal = SampleChemicalFear(cell);

                float gradientX = SampleChemicalAttractant(cell + new int3(1, 0, 0)) - SampleChemicalAttractant(cell + new int3(-1, 0, 0));
                float gradientY = SampleChemicalAttractant(cell + new int3(0, 1, 0)) - SampleChemicalAttractant(cell + new int3(0, -1, 0));
                float gradientZ = SampleChemicalAttractant(cell + new int3(0, 0, 1)) - SampleChemicalAttractant(cell + new int3(0, 0, -1));
                float3 safeCellSize = math.max(ChemicalGridCellSize, new float3(DdaEpsilon, DdaEpsilon, DdaEpsilon));
                gradient = new float3(
                    gradientX / (safeCellSize.x * 2f),
                    gradientY / (safeCellSize.y * 2f),
                    gradientZ / (safeCellSize.z * 2f));
                return attractantSignal > DdaEpsilon || fearSignal > DdaEpsilon || math.lengthsq(gradient) > DdaEpsilon;
            }

            private float SampleChemicalAttractant(int3 cell)
            {
                float4 sample = SampleChemicalCell(cell);
                return math.saturate(sample.x + sample.y);
            }

            private float SampleChemicalFear(int3 cell)
            {
                return math.saturate(SampleChemicalCell(cell).z);
            }

            private float4 SampleChemicalCell(int3 cell)
            {
                if (!IsChemicalCellInside(cell))
                    return float4.zero;

                int flatIndex = FlattenChemicalCellIndex(cell, ChemicalGridDimensions);
                if (flatIndex < 0 || flatIndex >= ChemicalGrid.Length)
                    return float4.zero;

                float4 sample = ChemicalGrid[flatIndex];
                if (ChemicalOverlayGrid.IsCreated && flatIndex < ChemicalOverlayGrid.Length)
                    sample += ChemicalOverlayGrid[flatIndex];
                return sample;
            }

            private bool TryWorldToChemicalCell(float3 worldPosition, out int3 cell)
            {
                float3 local = worldPosition - ChemicalGridOrigin;
                if (local.x < 0f || local.y < 0f || local.z < 0f)
                {
                    cell = int3.zero;
                    return false;
                }

                float3 safeCellSize = math.max(ChemicalGridCellSize, new float3(DdaEpsilon, DdaEpsilon, DdaEpsilon));
                int3 candidate = new int3(
                    (int)math.floor(local.x / safeCellSize.x),
                    (int)math.floor(local.y / safeCellSize.y),
                    (int)math.floor(local.z / safeCellSize.z));
                if (!IsChemicalCellInside(candidate))
                {
                    cell = int3.zero;
                    return false;
                }

                cell = candidate;
                return true;
            }

            private bool IsChemicalCellInside(int3 cell)
            {
                return cell.x >= 0 &&
                       cell.y >= 0 &&
                       cell.z >= 0 &&
                       cell.x < ChemicalGridDimensions.x &&
                       cell.y < ChemicalGridDimensions.y &&
                       cell.z < ChemicalGridDimensions.z;
            }

            private static int FlattenChemicalCellIndex(int3 cell, int3 dimensions)
            {
                return cell.x + (cell.y * dimensions.x) + (cell.z * dimensions.x * dimensions.y);
            }

            private static float Pow01(float value, float exponent)
            {
                return math.pow(math.saturate(value), exponent);
            }

            private static float EvaluateHuntUtility(
                float hungerScore,
                float fearScore,
                float aggressionWeight,
                float targetSignal,
                float attackCommit01,
                float satedSuppression)
            {
                float driveScore = Pow01(hungerScore, 2.5f) * Pow01(1f - fearScore, 3f);
                float aggressionDrive = Pow01(aggressionWeight, 1.75f);
                float stimulusDrive = Pow01(math.max(targetSignal, 0.15f), 1.35f);
                float commitDrive = math.lerp(0.45f, 1f, Pow01(attackCommit01, 1.6f));
                return driveScore * aggressionDrive * stimulusDrive * commitDrive * satedSuppression;
            }

            private static float EvaluateFleeUtility(float fearScore, float threatScore, float damage01)
            {
                float threatDrive = Pow01(math.max(fearScore, threatScore), 2.75f);
                float damageDrive = math.lerp(1f, 1.35f, Pow01(damage01, 1.5f));
                return threatDrive * damageDrive;
            }

            private static float EvaluatePatrolUtility(
                float hungerScore,
                float fearScore,
                float threatScore,
                float fatigueScore,
                bool satedActive)
            {
                float calmDrive = Pow01(1f - math.max(fearScore, threatScore), 2f);
                float recoveryDrive = Pow01(1f - fatigueScore, 0.85f);
                float lowHungerDrive = Pow01(1f - hungerScore, 1.35f);
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

            private static int3 ResolveSpatialBucketCoordinates(float3 worldPosition, float3 boundsMin, float bucketCellSize)
            {
                float safeCellSize = math.max(bucketCellSize, 0.001f);
                float3 localPosition = math.max(worldPosition - boundsMin, float3.zero);
                return new int3(
                    (int)math.floor(localPosition.x / safeCellSize),
                    (int)math.floor(localPosition.y / safeCellSize),
                    (int)math.floor(localPosition.z / safeCellSize));
            }

            private static float ResolveThreatBlend(float deltaTime)
            {
                float safeDeltaTime = math.min(math.max(0f, deltaTime), MaxThreatSmoothingDeltaTime);
                return math.saturate(1f - math.exp(-ThreatSmoothingK * safeDeltaTime));
            }

            private static int3 ResolveAcousticBucketCoordinates(float3 worldPosition, float bucketCellSize)
            {
                float safeCellSize = math.max(bucketCellSize, 0.001f);
                return new int3(
                    (int)math.floor(worldPosition.x / safeCellSize),
                    (int)math.floor(worldPosition.y / safeCellSize),
                    (int)math.floor(worldPosition.z / safeCellSize));
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

                float sequence = control.WanderSequence;
                float phase = currentTime * 0.73f + (sequence * 2.39996323f);
                float radiusT = math.frac(sequence * 0.61803398875f);
                float wanderRadius = math.max(1f, radius) * math.lerp(0.45f, 1f, radiusT);
                float verticalT = math.frac(sequence * 0.41421356f) - 0.5f;
                control.WanderTarget = center + new float3(
                    math.cos(phase) * wanderRadius,
                    verticalT * MaximumWanderVerticalOffset,
                    math.sin(phase) * wanderRadius);
                control.WanderSequence++;
                control.NextWanderTargetRefreshTime = currentTime + WanderTargetRefreshSeconds;
                control.Flags |= (int)CognitionControlFlags.HasWanderTarget;
            }

            private static float ComputeThreatVisual(float3 selfPosition, float3 targetPosition, float3 fallbackForward, float range)
            {
                float3 toTarget = targetPosition - selfPosition;
                float distance = math.sqrt(math.max(math.lengthsq(toTarget), MinimumDistanceMeters * MinimumDistanceMeters));
                float3 direction = math.normalizesafe(toTarget, fallbackForward);
                float forwardDot = math.saturate((math.dot(fallbackForward, direction) * 0.5f) + 0.5f);
                float distance01 = math.saturate(1f - (distance / math.max(range, 1f)));
                return distance01 * forwardDot;
            }

            private static float3 ResolvePredatorDirection(
                PredatorUtilityState stateMask,
                float3 selfPosition,
                float3 targetPosition,
                float3 fallbackForward,
                float currentTime,
                CognitionControl control)
            {
                if (stateMask == PredatorUtilityState.Fleeing)
                {
                    float3 fleeFrom = ((control.Flags & (int)CognitionControlFlags.HasOverrideThreatPosition) != 0 &&
                                       control.OverrideUntilTime > currentTime)
                        ? control.OverrideThreatPosition
                        : targetPosition;
                    return math.normalizesafe(selfPosition - fleeFrom, -fallbackForward);
                }

                return math.normalizesafe(targetPosition - selfPosition, fallbackForward);
            }

            private bool ResolveThreatVisibility(float3 start, float3 end, float importanceScore)
            {
                if (importanceScore < MinimumDetailedThreatImportanceScore)
                    return HasThreatGridHeuristic(start, end);

                return HasVoxelLineOfSight(start, end);
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

            private bool HasVoxelLineOfSight(float3 start, float3 end)
            {
                if (!ThreatVoxelGrid.IsCreated ||
                    ThreatVoxelDimensions.x <= 0 ||
                    ThreatVoxelDimensions.y <= 0 ||
                    ThreatVoxelDimensions.z <= 0)
                {
                    return true;
                }

                float3 delta = end - start;
                float distanceSq = math.lengthsq(delta);
                if (distanceSq <= DdaEpsilon)
                    return true;

                if (!TryWorldToVoxel(start, out int3 currentVoxel) ||
                    !TryWorldToVoxel(end, out int3 targetVoxel))
                {
                    return true;
                }

                float3 rayDir = delta * math.rsqrt(distanceSq);
                bool3 positiveMask = rayDir >= 0f;
                bool3 activeAxisMask = math.abs(rayDir) > DdaEpsilon;
                int3 step = math.select(new int3(-1, -1, -1), new int3(1, 1, 1), positiveMask);
                float3 cellMin = ThreatVoxelOrigin + (new float3(currentVoxel.x, currentVoxel.y, currentVoxel.z) * ThreatVoxelCellSize);
                float3 voxelBoundary = cellMin + math.select(float3.zero, ThreatVoxelCellSize, positiveMask);
                float3 safeAbsDir = math.max(math.abs(rayDir), new float3(DdaEpsilon, DdaEpsilon, DdaEpsilon));
                float3 rayDirInv = 1f / safeAbsDir;
                float3 tMax = math.abs((voxelBoundary - start) * rayDirInv);
                float3 tDelta = ThreatVoxelCellSize * rayDirInv;
                tMax = math.select(new float3(1000000f, 1000000f, 1000000f), tMax, activeAxisMask);
                tDelta = math.select(new float3(1000000f, 1000000f, 1000000f), tDelta, activeAxisMask);
                int maxSteps = math.min(ThreatVoxelDimensions.x + ThreatVoxelDimensions.y + ThreatVoxelDimensions.z, MaxThreatDdaSteps);

                for (int i = 0; i < maxSteps; i++)
                {
                    if (IsThreatVoxelSolid(SampleThreatVoxel(currentVoxel)))
                        return false;

                    if (math.all(currentVoxel == targetVoxel))
                        return true;

                    bool3 axisMask = (tMax <= tMax.yzx) & (tMax <= tMax.zxy);
                    tMax += math.select(float3.zero, tDelta, axisMask);
                    currentVoxel += math.select(int3.zero, step, axisMask);
                    if (!IsVoxelInside(currentVoxel))
                        return true;
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

                int3 candidate = new int3(
                    (int)math.floor(local.x / math.max(ThreatVoxelCellSize.x, DdaEpsilon)),
                    (int)math.floor(local.y / math.max(ThreatVoxelCellSize.y, DdaEpsilon)),
                    (int)math.floor(local.z / math.max(ThreatVoxelCellSize.z, DdaEpsilon)));
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
                return voxel.x + (voxel.y * dimensions.x) + (voxel.z * dimensions.x * dimensions.y);
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
        }
    }
}
