using System.Runtime.InteropServices;
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
        public float3 PlayerVelocity;
        public float3 ThreatPosition;
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
        public float DeltaTime;
        public float CurrentTime;
        public float AcousticPingStrength01;
        public float AcousticTransmission01;
        public float ChemicalSignal01;
        public float HungerWeight;
        public float ThreatWeight;
        public float FearWeight;
        public float AggressionWeight;
        public float EscapeDistance;
        public float EscapeSafeDistance;
        public float WanderRadius;
        public float PatrolRadius;
        public float ImportanceScore;
        public int SpeciesId;
        public int ClaimedBoidIndex;
        public int FlockCount;
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
        private const float MinimumDetailedThreatImportanceScore = 0.2f;
        private const float CenterEvaluationIntervalSeconds = 1.0f / 60.0f;
        private const float FocusEvaluationIntervalSeconds = 1.0f / 30.0f;
        private const float PeripheryEvaluationIntervalSeconds = 1.0f / 20.0f;
        private const float FarEvaluationIntervalSeconds = 1.0f / 10.0f;
        private const float RearEvaluationIntervalSeconds = 1.0f / 5.0f;
        private const float PredatorPackFlankDistanceMeters = 20f;
        private const float PredatorPackCoordinationRadiusMeters = 48f;
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
        private static NativeArray<int> _boidClaimTable;
        private static NativeArray<byte> _evaluationDueFlags;
        private static NativeArray<float> _nextEvaluationTimes;
        private static NativeArray<float> _evaluationIntervals;
        private static NativeArray<byte> _threatVoxelGrid;
        private static int3 _threatVoxelDimensions;
        private static float3 _threatVoxelOrigin;
        private static float3 _threatVoxelCellSize;
        private static byte _threatVoxelSolidThreshold = SolidThreatVoxel;
        private static bool _threatVoxelUsesSignedDistanceEncoding;
        private static JobHandle _scheduledSwarmHandle;
        private static JobHandle _scheduledEvaluationHandle;
        private static bool _evaluationScheduled;
        private static int _lastEvaluatedFrame = -1;
        private static int _lastScheduledFrame = -1;
        private static int _lastThreatVoxelBindFrame = -1;

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
                ClearMemoryEntries(i);
                ClearAcousticMemoryEntries(i);
                return i;
            }

            return -1;
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
                _activeSlots.Add(slot);
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
            _controls[slot] = control;

            _inputs[slot] = default;
            _outputs[slot] = BuildDefaultPackedOutput(new float3(0f, 0f, 1f));
            _evaluationDueFlags[slot] = 1;
            _chosenStates[slot] = 0;
            _nextEvaluationTimes[slot] = 0f;
            _evaluationIntervals[slot] = CenterEvaluationIntervalSeconds;
            _predatorPackTargets[slot] = float3.zero;
            _predatorPackWeights[slot] = 0f;
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
            if (_evaluationScheduled)
            {
                _scheduledEvaluationHandle.Complete();
                _scheduledSwarmHandle = default;
                _scheduledEvaluationHandle = default;
                _evaluationScheduled = false;
                _lastEvaluatedFrame = _lastScheduledFrame;
            }

            if (!_activeSlots.IsCreated)
                return;

            RefreshThreatVoxelSnapshot(frameId);
        }

        internal static void ScheduleFrameEvaluation(int frameId)
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
                ChosenStates = _chosenStates,
                BoidClaimTable = _boidClaimTable,
                Outputs = _outputs,
                ThreatVoxelGrid = _threatVoxelGrid,
                ThreatVoxelDimensions = _threatVoxelDimensions,
                ThreatVoxelOrigin = _threatVoxelOrigin,
                ThreatVoxelCellSize = _threatVoxelCellSize,
                ThreatVoxelSolidThreshold = _threatVoxelSolidThreshold,
                ThreatVoxelUsesSignedDistanceEncoding = _threatVoxelUsesSignedDistanceEncoding ? 1 : 0
            };

            _scheduledEvaluationHandle = job.Schedule(_activeSlots.Length, EvaluationJobBatchSize, _scheduledSwarmHandle);
            _evaluationScheduled = true;
            _lastScheduledFrame = frameId;
        }

        internal static void Dispose()
        {
            JobHandle disposeDependency = _evaluationScheduled ? _scheduledEvaluationHandle : default;
            if (_cores.IsCreated)
                _cores.Dispose(disposeDependency);
            if (_controls.IsCreated)
                _controls.Dispose(disposeDependency);
            if (_inputs.IsCreated)
                _inputs.Dispose(disposeDependency);
            if (_outputs.IsCreated)
                _outputs.Dispose(disposeDependency);
            if (_memoryBank.IsCreated)
                _memoryBank.Dispose(disposeDependency);
            if (_acousticMemoryBank.IsCreated)
                _acousticMemoryBank.Dispose(disposeDependency);
            if (_slotUsed.IsCreated)
                _slotUsed.Dispose(disposeDependency);
            if (_activeSlots.IsCreated)
                _activeSlots.Dispose(disposeDependency);
            if (_ambientThreats.IsCreated)
                _ambientThreats.Dispose(disposeDependency);
            if (_swarmCenters.IsCreated)
                _swarmCenters.Dispose(disposeDependency);
            if (_swarmDirections.IsCreated)
                _swarmDirections.Dispose(disposeDependency);
            if (_swarmAvoidances.IsCreated)
                _swarmAvoidances.Dispose(disposeDependency);
            if (_swarmCounts.IsCreated)
                _swarmCounts.Dispose(disposeDependency);
            if (_claimedBoidIndices.IsCreated)
                _claimedBoidIndices.Dispose(disposeDependency);
            if (_claimedBoidPositions.IsCreated)
                _claimedBoidPositions.Dispose(disposeDependency);
            if (_chosenStates.IsCreated)
                _chosenStates.Dispose(disposeDependency);
            if (_predatorPackTargets.IsCreated)
                _predatorPackTargets.Dispose(disposeDependency);
            if (_predatorPackWeights.IsCreated)
                _predatorPackWeights.Dispose(disposeDependency);
            if (_boidClaimTable.IsCreated)
                _boidClaimTable.Dispose(disposeDependency);
            if (_evaluationDueFlags.IsCreated)
                _evaluationDueFlags.Dispose(disposeDependency);
            if (_nextEvaluationTimes.IsCreated)
                _nextEvaluationTimes.Dispose(disposeDependency);
            if (_evaluationIntervals.IsCreated)
                _evaluationIntervals.Dispose(disposeDependency);

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
            _boidClaimTable = default;
            _evaluationDueFlags = default;
            _nextEvaluationTimes = default;
            _evaluationIntervals = default;
            _threatVoxelGrid = default;
            _threatVoxelDimensions = int3.zero;
            _threatVoxelOrigin = float3.zero;
            _threatVoxelCellSize = new float3(1f, 1f, 1f);
            _threatVoxelSolidThreshold = SolidThreatVoxel;
            _threatVoxelUsesSignedDistanceEncoding = false;
            _scheduledSwarmHandle = default;
            _scheduledEvaluationHandle = default;
            _evaluationScheduled = false;
            _lastEvaluatedFrame = -1;
            _lastScheduledFrame = -1;
            _lastThreatVoxelBindFrame = -1;
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
            // COLD ALLOC: NativeArray<int>[Capacity] - atomic prey claim table keyed by creature slot - owner: PredatorCognitionDomain
            _boidClaimTable = new NativeArray<int>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[Capacity] - per-slot due flags for foveated cognition cadence - owner: PredatorCognitionDomain
            _evaluationDueFlags = new NativeArray<byte>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[Capacity] - next allowed cognition evaluation timestamps per slot - owner: PredatorCognitionDomain
            _nextEvaluationTimes = new NativeArray<float>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[Capacity] - resolved cognition cadence intervals per slot - owner: PredatorCognitionDomain
            _evaluationIntervals = new NativeArray<float>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            ClearBoidClaims();
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
            float3 localPosition = worldPosition - boundsMin;
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

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
                bool canCoordinatePack = canClaimBoid && (input.Flags & (int)CognitionInputFlags.HasPlayerTarget) != 0;
                float3 predatorPackTarget = input.PlayerPosition;
                float predatorPackWeight = 0f;
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
                        (otherInput.Flags & (int)CognitionInputFlags.PredatorRole) != 0 &&
                        (otherInput.Flags & (int)CognitionInputFlags.HasPlayerTarget) != 0 &&
                        (PriorCores[otherSlot].StateFlags & (uint)FaunaWorldStateFlags.Hunting) != 0u)
                    {
                        float coordinationDistance = distSq * invDist;
                        float coordinationWeight = 1f - math.saturate(coordinationDistance / PredatorPackCoordinationRadiusMeters);
                        if (coordinationWeight > predatorPackWeight)
                        {
                            float3 packForward = math.normalizesafe(
                                otherInput.Velocity + (otherInput.PlayerPosition - otherInput.Position),
                                math.normalizesafe(otherInput.Forward, new float3(0f, 0f, 1f)));
                            float3 packRight = math.normalizesafe(
                                math.cross(new float3(0f, 1f, 0f), packForward),
                                math.cross(new float3(0f, 0f, 1f), packForward));
                            if (math.lengthsq(packRight) > DdaEpsilon)
                            {
                                float sideSign = math.select(-1f, 1f, math.dot(input.Position - otherInput.PlayerPosition, packRight) >= 0f);
                                predatorPackTarget = otherInput.PlayerPosition + (packRight * (PredatorPackFlankDistanceMeters * sideSign));
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
            }
        }

        [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct PredatorCognitionJob : IJobParallelFor
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
            [NativeDisableParallelForRestriction] public NativeArray<int> ChosenStates;
            [NativeDisableParallelForRestriction] public NativeArray<int> BoidClaimTable;
            public NativeArray<PackedCognitionOutput> Outputs;
            [ReadOnly] public NativeArray<byte> ThreatVoxelGrid;
            public int3 ThreatVoxelDimensions;
            public float3 ThreatVoxelOrigin;
            public float3 ThreatVoxelCellSize;
            public byte ThreatVoxelSolidThreshold;
            public int ThreatVoxelUsesSignedDistanceEncoding;

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
                bool hasPreyTarget = (input.Flags & (int)CognitionInputFlags.HasPreyTarget) != 0;
                bool hasScavengeTarget = (input.Flags & (int)CognitionInputFlags.HasScavengeTarget) != 0;
                bool useHomeTerritory = (input.Flags & (int)CognitionInputFlags.UseHomeTerritory) != 0;
                bool isFlocking = (input.Flags & (int)CognitionInputFlags.IsFlocking) != 0;
                bool hasVisualPlayerHint = (input.Flags & (int)CognitionInputFlags.HasVisualPlayerHint) != 0;

                bool playerVisible = hasPlayerTarget && hasVisualPlayerHint && ResolveThreatVisibility(resolvedInput.Position, resolvedInput.PlayerPosition, resolvedInput.ImportanceScore);
                bool threatVisible = hasThreatTarget && ResolveThreatVisibility(resolvedInput.Position, resolvedInput.ThreatPosition, resolvedInput.ImportanceScore);
                bool preyVisible = (hasPreyTarget || resolvedInput.ClaimedBoidIndex >= 0) && ResolveThreatVisibility(resolvedInput.Position, resolvedInput.PreyPosition, resolvedInput.ImportanceScore);
                bool scavengeVisible = hasScavengeTarget && ResolveThreatVisibility(resolvedInput.Position, resolvedInput.ScavengePosition, resolvedInput.ImportanceScore);
                if (playerVisible)
                    control.LastVisualContactTime = input.CurrentTime;

                float rawFear = math.saturate((1f - math.saturate(input.HealthNormalized)) + input.FearPressure01);
                fear = math.max(fear, rawFear);

                PackedCognitionOutput output = isPredator
                    ? EvaluatePredator(slot, ref core, ref control, in resolvedInput, fallbackForward, canFlee, hasPlayerTarget, playerVisible, preyVisible, scavengeVisible, aggression, ref hunger, ref fatigue, ref fear, ref threatLevel)
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
                float3 predictedPlayerPosition = hasPlayerTarget
                    ? ResolvePredictedPlayerIntercept(input, aggression)
                    : input.PlayerPosition;
                float directAcousticScore = hasPlayerTarget
                    ? ComputeAcousticScore(input.Position, input.PlayerPosition, input.AcousticPingStrength01, input.AcousticTransmission01)
                    : 0f;
                float packFlankWeight = PredatorPackWeights[slot];
                float3 packFlankTarget = PredatorPackTargets[slot];
                bool hasAcousticMemory = TryResolveStrongestAcousticMemory(slot, input.Position, input.CurrentTime, out float3 acousticMemoryPosition, out float acousticMemoryScore);
                float acousticScore = math.max(directAcousticScore, acousticMemoryScore);
                bool hasScavengeTarget = (input.Flags & (int)CognitionInputFlags.HasScavengeTarget) != 0;
                float chemicalScore = ComputeChemicalScore(
                    slot,
                    input.Position,
                    input.ScavengePosition,
                    hasScavengeTarget,
                    input.ChemicalSignal01,
                    input.CurrentTime);

                float3 targetPosition = input.Position + (fallbackForward * 4f);
                bool hasTarget = false;
                if (scavengeVisible)
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
                else if (hasScavengeTarget && chemicalScore > ChemicalStimulusThreshold)
                {
                    targetPosition = input.ScavengePosition;
                    hasTarget = true;
                }
                else if (TryResolveStrongestMemory(slot, input.Position, input.CurrentTime, out float3 memoryPosition, out float memoryThreatWeight))
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

                bool usePackFlank = hasPlayerTarget &&
                                    packFlankWeight > DdaEpsilon &&
                                    !scavengeVisible &&
                                    !resolvedPreyVisible;
                if (usePackFlank)
                {
                    float3 packPredictionDelta = predictedPlayerPosition - input.PlayerPosition;
                    targetPosition = math.lerp(predictedPlayerPosition, packFlankTarget + packPredictionDelta, math.saturate(packFlankWeight));
                    hasTarget = true;
                }

                float threatVisual = 0f;
                if (playerVisible)
                    threatVisual = ComputeThreatVisual(input.Position, predictedPlayerPosition, fallbackForward, input.AttackRange);
                else if (resolvedPreyVisible)
                    threatVisual = ComputeThreatVisual(input.Position, resolvedPreyPosition, fallbackForward, input.AttackRange * 1.5f) * 0.8f;
                else if (scavengeVisible)
                    threatVisual = ComputeThreatVisual(input.Position, input.ScavengePosition, fallbackForward, input.AttackRange * 2f) * 0.65f;

                float threatBlend = 1f - math.exp(-ThreatSmoothingK * math.max(0f, input.DeltaTime));
                float threatRaw = math.max(math.max(threatVisual, acousticScore), threatLevel);
                threatLevel = math.lerp(threatLevel, threatRaw, threatBlend);
                threatLevel = math.clamp(threatLevel, 0f, 1f);
                fear = math.max(fear, ScoreThreat(threatRaw) * 0.35f);

                float hungerScore = ScoreHunger(hunger) * math.max(0.1f, input.HungerWeight);
                float fatigueScore = ScoreFatigue(fatigue);
                float fearScore = canFlee
                    ? ScoreFear(math.max(fear, threatRaw * 0.45f)) * math.max(0.1f, input.FearWeight)
                    : 0f;
                float threatScore = ScoreThreat(threatLevel) * math.max(0.1f, input.ThreatWeight);
                float acousticUtility = ScoreThreat(acousticScore) * math.max(0.1f, input.ThreatWeight);
                float chemicalUtility = ScoreThreat(chemicalScore) * math.max(0.1f, input.HungerWeight);
                float targetDistanceSq = math.lengthsq(targetPosition - input.Position);
                float attackCommit01 = hasTarget
                    ? ScoreHunger(math.saturate(1f - (math.sqrt(math.max(targetDistanceSq, 0f)) / math.max(input.AttackRange, 1f))))
                    : 0f;

                bool overrideActive = control.OverrideUntilTime > input.CurrentTime;
                bool satedActive = control.SatedUntilTime > input.CurrentTime;
                float aggressionWeight = math.max(0.1f, aggression);
                float satedSuppression = math.select(1f, 0.05f, satedActive);
                float targetSignal = math.saturate(math.max(threatScore, math.max(acousticUtility, chemicalUtility)));
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
                        satedActive));
                float stalkingScore = huntUtility * math.lerp(0.55f, 0.95f, Pow01(targetSignal, 1.2f));
                float attackingScore = huntUtility *
                                       math.lerp(0.25f, 1f, Pow01(attackCommit01, 1.5f)) *
                                       AttackStateBias *
                                       math.select(0.25f, 1f, hasTarget);
                float fleeingScore = canFlee
                    ? EvaluateFleeUtility(
                        fearScore,
                        threatScore,
                        1f - math.saturate(input.HealthNormalized))
                    : 0f;

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

                bool wantsBoidClaim =
                    stateMask == PredatorUtilityState.Attacking &&
                    input.ClaimedBoidIndex >= 0 &&
                    !playerVisible &&
                    !scavengeVisible;
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
                        output.OutputFlags = ((playerVisible || resolvedPreyVisible || scavengeVisible) &&
                                              targetDistanceSq <= math.max(1f, input.AttackRange * input.AttackRange) &&
                                              input.CurrentTime >= control.NextAttackAllowedTime)
                            ? (uint)CognitionOutputFlags.ShouldAttack
                            : 0u;
                        break;
                    case PredatorUtilityState.Fleeing:
                        output.ForceMultiplier = 2.4f;
                        output.SpeedMultiplier = math.max(1.2f, input.FearWeight);
                        output.TurnMultiplier = 1.15f;
                        break;
                }

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
                float directAcousticScore = hasPlayerTarget
                    ? ComputeAcousticScore(input.Position, input.PlayerPosition, input.AcousticPingStrength01, input.AcousticTransmission01)
                    : 0f;
                bool hasAcousticMemory = TryResolveStrongestAcousticMemory(slot, input.Position, input.CurrentTime, out float3 acousticMemoryPosition, out float acousticMemoryScore);
                float acousticScore = math.max(directAcousticScore, acousticMemoryScore);
                float threatVisual = playerVisible
                    ? ComputeThreatVisual(input.Position, input.PlayerPosition, fallbackForward, math.max(input.EscapeSafeDistance, 1f))
                    : 0f;
                if (threatVisible)
                    threatVisual = math.max(threatVisual, ComputeThreatVisual(input.Position, input.ThreatPosition, fallbackForward, math.max(input.EscapeSafeDistance, 1f)));

                float threatBlend = 1f - math.exp(-ThreatSmoothingK * math.max(0f, input.DeltaTime));
                float threatRaw = math.max(math.max(threatVisual, playerThreat), acousticScore);
                threatLevel = math.lerp(threatLevel, threatRaw, threatBlend);
                threatLevel = math.clamp(threatLevel, 0f, 1f);
                fear = math.max(fear, ScoreThreat(threatRaw) * 0.45f);

                bool retreatForced = control.OverrideUntilTime > input.CurrentTime;
                bool scatterActive = isFlocking && control.ScatterUntilTime > input.CurrentTime;
                bool satedActive = control.SatedUntilTime > input.CurrentTime;
                bool lowHealth = input.HealthNormalized <= PassiveLowHealthThreshold;
                bool homeOutOfBounds = useHomeTerritory &&
                                       input.PatrolRadius > 0f &&
                                       math.lengthsq(input.Position - control.SpawnAnchor) > (input.PatrolRadius * input.PatrolRadius);
                bool shouldEscape = retreatForced ||
                                    (canFlee && hasPlayerTarget && (input.DistanceToPlayerSqr <= input.EscapeDistance * input.EscapeDistance || lowHealth || threatLevel >= 0.35f)) ||
                                    (canFlee && (threatVisible || acousticScore > AcousticStimulusThreshold));

                float fatigueScore = ScoreFatigue(fatigue);
                float escapeScore = ScoreFear(math.max(fear, threatLevel)) * math.max(0.1f, input.FearWeight) * math.select(0f, 1f, shouldEscape);
                float homeDistance01 = useHomeTerritory && input.PatrolRadius > 0f
                    ? math.saturate(math.sqrt(math.lengthsq(input.Position - control.SpawnAnchor)) / math.max(input.PatrolRadius, 1f))
                    : 0f;
                float returnScore = ScoreThreat(homeDistance01) * math.select(0f, 1f, homeOutOfBounds && !shouldEscape && !satedActive);
                float scatterScore = math.select(0f, OverrideScoreBias + ScoreThreat(math.max(acousticScore, threatLevel)), scatterActive);
                float flockingScore = ScoreThreat(math.saturate((float)input.FlockCount / 6f)) * math.select(0f, math.max(0.25f, 1f - escapeScore), isFlocking && input.FlockCount > 1 && !scatterActive && !satedActive && !shouldEscape && !homeOutOfBounds);
                float satedScore = math.select(0f, OverrideScoreBias + fatigueScore, satedActive);
                float wanderScore = math.max(
                    MinimumScoreThreshold,
                    ((1f - escapeScore) * 0.45f) +
                    ((1f - math.saturate(threatLevel)) * 0.35f) +
                    (fatigueScore * 0.2f)) *
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

                return math.saturate(directScore);
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
                float3 localPosition = worldPosition - boundsMin;
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
