using System;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Systems.AI
{
    internal enum EncounterPhase : int
    {
        BuildUp = 0,
        Peak = 1,
        Decay = 2,
        Relax = 3
    }

    internal enum EncounterThreatClass : int
    {
        Drone = 0,
        Stalker = 1,
        Swarm = 2,
        Leviathan = 3
    }

    [Flags]
    internal enum EncounterBudgetFlags : int
    {
        None = 0,
        LoadSheddingActive = 1 << 0,
        SpawnSuspended = 1 << 1,
        EmergencyRecall = 1 << 2,
        RegenBlocked = 1 << 3
    }

    internal struct EncounterDirectorState
    {
        public float StressLevel;
        public float IntensityLevel;
        public float PacingPhaseTimer;
        public float TokenBudget;
        public float TokenRegenRate;
        public int ActivePhase;
        public int ActiveEnemyCount;
        public int BudgetFlags;
        public float RecoveryTimer;
        public float4 PlayerPosition;
        public float4 PlayerVelocity;
        public uint SpawnSequence;
    }

    internal struct EncounterEnemyToken
    {
        public int EntityId;
        public float TokenCost;
        public float DistSqToPlayer;
        public int VisibilityFlags;
        public float DepthPosition;
        public int ThreatClass;
        public float DespawnPriority;
        public float3 Position;
    }

    internal struct EncounterFrameContext
    {
        public float DeltaTime;
        public float3 PlayerPosition;
        public float3 PlayerVelocity;
        public float3 PlayerForward;
        public float PlayerHealthNormalized;
        public float PlayerOxygenNormalized;
        public float PlayerInternalStress;
        public float AcousticThreatLevel;
        public float PlayerDepth;
        public float AvgFrameTimeMs;
        public float SurfaceWorldY;
    }

    internal struct EncounterJobOutput
    {
        public int SpawnRequestCount;
        public int SpawnThreatClass;
        public float3 SpawnPosition;
        public float3 SpawnPosition1;
        public float3 SpawnPosition2;
        public uint SpawnVariantSeed;
        public uint SpawnVariantSeed1;
        public uint SpawnVariantSeed2;
        public uint SpawnSquadStateBits;
        public int ForcedSpawnConsumed;
        public int DespawnRequestCount;
        public int DespawnEntityId0;
        public int DespawnEntityId1;
        public int DespawnEntityId2;
        public int PhaseChanged;
        public int PreviousPhase;
        public int NewPhase;
    }

    internal struct EncounterDebugEvent
    {
        public float Timestamp;
        public int Code;
        public float Context;
        public float Auxiliary;
    }

    internal struct EncounterThreatAuthoringSnapshot
    {
        public float DroneMinIntensity;
        public float SwarmMinIntensity;
        public float StalkerMinIntensity;
        public float LeviathanMinIntensity;
        public float DroneTokenCost;
        public float SwarmTokenCost;
        public float StalkerTokenCost;
        public float LeviathanTokenCost;
        public float DroneDespawnPriorityBias;
        public float SwarmDespawnPriorityBias;
        public float StalkerDespawnPriorityBias;
        public float LeviathanDespawnPriorityBias;
        public int DroneMaxSimultaneous;
        public int SwarmMaxSimultaneous;
        public int StalkerMaxSimultaneous;
        public int LeviathanMaxSimultaneous;
        public int DroneAllowCriticalHealth;
        public int SwarmAllowCriticalHealth;
        public int StalkerAllowCriticalHealth;
        public int LeviathanAllowCriticalHealth;
    }

    internal sealed class EncounterDirector : IDisposable
    {
        internal const int FrustumPlaneCount = 6;
        private const int DebugEventRingCapacity = 256;
        private const int DebugEventCodePhaseChange = 0x04;

        private const int MaxActiveEnemies = 32;
        private const int BaseCandidateCount = 16;
        private const int HighCandidateCount = 32;
        private const float ColdTickIntervalSeconds = 1f;
        private const float MaxTokenBudget = 100f;
        private const float MinSpawnRadius = 50f;
        private const float MaxSpawnRadius = 150f;
        private const float SpawnClusterRadiusSq = 15f * 15f;
        private const float DespawnKeepDistanceSq = 25f * 25f;
        private const float FrustumRejectPadding = 3f;
        private const float SafeIdleStressDecayPerTick = 0.06f;

        private NativeArray<EncounterDirectorState> _frontState;
        private NativeArray<EncounterDirectorState> _backState;
        private NativeArray<EncounterEnemyToken> _enemyTokens;
        private NativeArray<float4> _frustumPlanes;
        private NativeArray<float3> _candidateDirections;
        private NativeArray<EncounterJobOutput> _jobOutput;
        private NativeArray<EncounterDebugEvent> _debugEventRing;
        private NativeArray<int> _debugEventHead;
        // COLD ALLOC: Transform[32] — tracked live encounter proxies for token refresh — owner: EncounterDirector
        private readonly Transform[] _trackedTransforms;
        // COLD ALLOC: int[32] — tracked live encounter entity ids — owner: EncounterDirector
        private readonly int[] _trackedEntityIds;
        // COLD ALLOC: EncounterThreatClass[32] — tracked encounter threat classes — owner: EncounterDirector
        private readonly EncounterThreatClass[] _trackedThreatClasses;
        // COLD ALLOC: float[32] — tracked encounter token costs — owner: EncounterDirector
        private readonly float[] _trackedTokenCosts;
        private EncounterThreatAuthoringSnapshot _threatAuthoring;

        private JobHandle _activeJobHandle;
        private bool _jobScheduled;
        private float _coldTickAccumulator;
        private int _frameIndex;
        private readonly int _candidateCount;
        private int _pendingPhaseOverride = -1;
        private bool _pendingReset;
        private int _pendingForcedThreatClass = -1;
        private int _pendingForcedThreatCount;

        internal EncounterDirector()
        {
            _frontState = new NativeArray<EncounterDirectorState>(1, Allocator.Persistent);
            _backState = new NativeArray<EncounterDirectorState>(1, Allocator.Persistent);
            _enemyTokens = new NativeArray<EncounterEnemyToken>(MaxActiveEnemies, Allocator.Persistent);
            _frustumPlanes = new NativeArray<float4>(FrustumPlaneCount, Allocator.Persistent);
            _candidateDirections = new NativeArray<float3>(HighCandidateCount, Allocator.Persistent);
            _jobOutput = new NativeArray<EncounterJobOutput>(1, Allocator.Persistent);
            _debugEventRing = new NativeArray<EncounterDebugEvent>(DebugEventRingCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _debugEventHead = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeMemorySentinel();
            _trackedTransforms = new Transform[MaxActiveEnemies];
            _trackedEntityIds = new int[MaxActiveEnemies];
            _trackedThreatClasses = new EncounterThreatClass[MaxActiveEnemies];
            _trackedTokenCosts = new float[MaxActiveEnemies];
            _candidateCount = ResolveCandidateCount();
            _threatAuthoring = BuildDefaultThreatAuthoringSnapshot();

            PrecomputeCandidateDirections();
            Reset();
        }

        internal void ApplyAuthoring(EncounterProfile encounterProfile, ThreatCostTable threatCostTable)
        {
            _threatAuthoring = BuildThreatAuthoringSnapshot(encounterProfile, threatCostTable);
        }

        internal float StressLevel => _frontState[0].StressLevel;
        internal float IntensityLevel => _frontState[0].IntensityLevel;
        internal float TokenBudget => _frontState[0].TokenBudget;
        internal int ActiveEnemyCount => _frontState[0].ActiveEnemyCount;
        internal int FrameIndex => _frameIndex;
        internal EncounterPhase CurrentPhase => (EncounterPhase)_frontState[0].ActivePhase;

        internal string CurrentPhaseName
        {
            get
            {
                switch (CurrentPhase)
                {
                    case EncounterPhase.BuildUp:
                        return "BuildUp";
                    case EncounterPhase.Peak:
                        return "Peak";
                    case EncounterPhase.Decay:
                        return "Decay";
                    default:
                        return "Relax";
                }
            }
        }

        internal void Reset()
        {
            EncounterDirectorState state = default;
            state.TokenBudget = 0f;
            state.ActivePhase = (int)EncounterPhase.BuildUp;
            _frontState[0] = state;
            _backState[0] = state;
            _jobOutput[0] = default;
            _coldTickAccumulator = 0f;
            _frameIndex = 0;
            _pendingPhaseOverride = -1;
            _pendingReset = false;
            _pendingForcedThreatClass = -1;
            _pendingForcedThreatCount = 0;
            if (_debugEventHead.IsCreated && _debugEventHead.Length > 0)
                _debugEventHead[0] = 0;

            for (int i = 0; i < MaxActiveEnemies; i++)
                ClearTrackedSlot(i);
        }

        internal void RequestPhaseOverride(EncounterPhase phase)
        {
            _pendingPhaseOverride = (int)phase;
        }

        internal void RequestReset()
        {
            _pendingReset = true;
        }

        internal void RequestForcedSquad(EncounterThreatClass threatClass, int count)
        {
            if (count <= 0)
                return;

            _pendingForcedThreatClass = (int)threatClass;
            _pendingForcedThreatCount = math.max(_pendingForcedThreatCount, count);
        }

        internal void CopyFrustumPlanes(Plane[] planes)
        {
            int count = math.min(FrustumPlaneCount, planes != null ? planes.Length : 0);
            for (int i = 0; i < count; i++)
            {
                Plane plane = planes[i];
                _frustumPlanes[i] = new float4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
            }

            for (int i = count; i < FrustumPlaneCount; i++)
                _frustumPlanes[i] = float4.zero;
        }

        internal void Advance(EncounterFrameContext frameContext, FaunaDirector faunaDirector, HectonDirectorAI bridge)
        {
            if (_jobScheduled && _activeJobHandle.IsCompleted)
            {
                if (!DispatcherJobSwap.TryComplete(ref _activeJobHandle, false))
                    return;

                _jobScheduled = false;
                _frontState[0] = _backState[0];
                ApplyCompletedOutput(faunaDirector, bridge);
            }

            if (_pendingReset)
            {
                Reset();
                bridge.HandleEncounterPhaseChanged(CurrentPhase, CurrentPhase);
            }

            if (_pendingPhaseOverride >= 0)
            {
                ApplyPhaseOverride((EncounterPhase)_pendingPhaseOverride, bridge);
                _pendingPhaseOverride = -1;
            }

            RefreshTrackedEnemies(frameContext.PlayerPosition);

            _coldTickAccumulator += frameContext.DeltaTime;
            if (_jobScheduled || _coldTickAccumulator < ColdTickIntervalSeconds)
                return;

            _coldTickAccumulator -= ColdTickIntervalSeconds;
            ScheduleColdTick(frameContext);
        }

        internal static float HashToUnit01(uint hash)
        {
            return (hash & 0x00FFFFFFu) / 16777215f;
        }

        internal static uint BuildDeterministicSeed(Vector3 position, int sequenceSalt, int phase, int activeEnemyCount)
        {
            int3 grid = new int3(
                (int)math.floor(position.x),
                (int)math.floor(position.y),
                (int)math.floor(position.z));
            uint hash = WangHash(unchecked((uint)grid.x) * 73856093u);
            hash ^= WangHash(unchecked((uint)grid.y) * 19349663u);
            hash ^= WangHash(unchecked((uint)grid.z) * 83492791u);
            hash ^= WangHash(unchecked((uint)sequenceSalt) * 1664525u);
            hash ^= WangHash(unchecked((uint)phase) * 1013904223u);
            hash ^= WangHash(unchecked((uint)activeEnemyCount) * 214013u);
            return hash == 0u ? 1u : hash;
        }

        internal static void FillFallbackFrustumPlanes(Vector3 origin, Vector3 forward, Plane[] destination)
        {
            Vector3 safeForward = NormalizeSafe(forward, Vector3.forward);
            Vector3 right = Vector3.Cross(Vector3.up, safeForward);
            if (right.sqrMagnitude <= 0.0001f)
                right = Vector3.right;
            else
                right = NormalizeSafe(right, Vector3.right);
            Vector3 up = NormalizeSafe(Vector3.Cross(safeForward, right), Vector3.up);

            Vector3 nearCenter = origin + safeForward * MinSpawnRadius;
            destination[0] = new Plane(safeForward, nearCenter);
            destination[1] = new Plane(-safeForward, origin + safeForward * MaxSpawnRadius);
            destination[2] = new Plane(NormalizeSafe(safeForward - right, safeForward), origin);
            destination[3] = new Plane(NormalizeSafe(safeForward + right, safeForward), origin);
            destination[4] = new Plane(NormalizeSafe(safeForward - up, safeForward), origin);
            destination[5] = new Plane(NormalizeSafe(safeForward + up, safeForward), origin);
        }

        private static Vector3 NormalizeSafe(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            return lengthSq > 0.000001f
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > 0.000001f
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        private static float EstimateLength(float3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float max = math.max(ax, math.max(ay, az));
            float min = math.min(ax, math.min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + (mid * 0.375f) + (min * 0.25f);
        }

        private static float ApproximateOneMinusExpNegPositive(float x)
        {
            return math.saturate(1f - ApproximateExpNegPositive(x));
        }

        private static float ApproximateExpNegPositive(float x)
        {
            float clamped = math.clamp(x, 0f, 8f);
            float x2 = clamped * clamped;
            float x3 = x2 * clamped;
            float numerator = 120f - (60f * clamped) + (12f * x2) - x3;
            float denominator = 120f + (60f * clamped) + (12f * x2) + x3;
            return math.saturate(numerator / math.max(denominator, 0.0001f));
        }

        public void Dispose()
        {
            JobHandle disposeHandle = default;
            bool hasDependency = _jobScheduled;
            if (_jobScheduled)
            {
                disposeHandle = _activeJobHandle;
                _jobScheduled = false;
            }

            DisposeNativeArray(ref _frontState, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _backState, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _enemyTokens, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _frustumPlanes, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _candidateDirections, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _jobOutput, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _debugEventRing, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _debugEventHead, ref disposeHandle, ref hasDependency);
        }

        private void RegisterNativeMemorySentinel()
        {
            NativeMemorySentinel.RegisterNativeArray(_frontState, nameof(EncounterDirector), nameof(_frontState), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_backState, nameof(EncounterDirector), nameof(_backState), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_enemyTokens, nameof(EncounterDirector), nameof(_enemyTokens), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_frustumPlanes, nameof(EncounterDirector), nameof(_frustumPlanes), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_candidateDirections, nameof(EncounterDirector), nameof(_candidateDirections), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_jobOutput, nameof(EncounterDirector), nameof(_jobOutput), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_debugEventRing, nameof(EncounterDirector), nameof(_debugEventRing), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_debugEventHead, nameof(EncounterDirector), nameof(_debugEventHead), NativeAllocationLifetime.Scene);
        }

        private void RefreshTrackedEnemies(float3 playerPosition)
        {
            for (int i = 0; i < MaxActiveEnemies; i++)
            {
                int entityId = _trackedEntityIds[i];
                if (entityId == 0)
                {
                    _enemyTokens[i] = default;
                    continue;
                }

                Transform trackedTransform = _trackedTransforms[i];
                if (trackedTransform == null || !trackedTransform.gameObject.activeInHierarchy)
                {
                    ClearTrackedSlot(i);
                    continue;
                }

                float3 position = trackedTransform.position;
                EncounterEnemyToken token = _enemyTokens[i];
                token.EntityId = entityId;
                token.Position = position;
                token.DepthPosition = position.y;
                token.ThreatClass = (int)_trackedThreatClasses[i];
                token.TokenCost = _trackedTokenCosts[i];
                token.DistSqToPlayer = math.lengthsq(position - playerPosition);
                token.VisibilityFlags = 0;
                token.DespawnPriority = ResolveDespawnPriorityBias(_trackedThreatClasses[i], _threatAuthoring);
                _enemyTokens[i] = token;
            }
        }

        private void ScheduleColdTick(EncounterFrameContext frameContext)
        {
            EncounterDirectorState currentState = _frontState[0];
            currentState.PlayerPosition = new float4(frameContext.PlayerPosition, frameContext.PlayerDepth);
            currentState.PlayerVelocity = new float4(frameContext.PlayerVelocity, EstimateLength(frameContext.PlayerVelocity));

            EncounterDirectorJob job = new EncounterDirectorJob
            {
                CurrentState = currentState,
                WriteState = _backState,
                ActiveEnemies = _enemyTokens,
                FrustumPlanes = _frustumPlanes,
                CandidateDirections = _candidateDirections,
                CandidateCount = _candidateCount,
                PlayerPosition = currentState.PlayerPosition,
                PlayerVelocity = currentState.PlayerVelocity,
                PlayerForward = new float4(NormalizeSafe(frameContext.PlayerForward, new float3(0f, 0f, 1f)), 0f),
                PlayerHealthNormalized = math.clamp(frameContext.PlayerHealthNormalized, 0f, 1f),
                PlayerOxygenNormalized = math.clamp(frameContext.PlayerOxygenNormalized, 0f, 1f),
                PlayerInternalStress = math.clamp(frameContext.PlayerInternalStress, 0f, 1f),
                AcousticThreatLevel = math.clamp(frameContext.AcousticThreatLevel, 0f, 1f),
                AvgFrameTimeMs = math.max(0f, frameContext.AvgFrameTimeMs),
                SurfaceWorldY = frameContext.SurfaceWorldY,
                ForcedThreatClass = _pendingForcedThreatClass,
                ForcedThreatCount = _pendingForcedThreatCount,
                ThreatAuthoring = _threatAuthoring,
                Output = _jobOutput
            };

            _frameIndex++;
            _jobOutput[0] = default;
            _activeJobHandle = job.Schedule(1, 1);
            _jobScheduled = true;
        }

        private void ApplyCompletedOutput(FaunaDirector faunaDirector, HectonDirectorAI bridge)
        {
            EncounterJobOutput output = _jobOutput[0];

            if (output.PhaseChanged != 0)
            {
                WriteDebugEvent(DebugEventCodePhaseChange, output.NewPhase, _frontState[0].IntensityLevel);
                bridge.HandleEncounterPhaseChanged((EncounterPhase)output.PreviousPhase, (EncounterPhase)output.NewPhase);
            }

            if (output.DespawnRequestCount > 0 && faunaDirector != null)
                ApplyDespawnRequests(output, faunaDirector);

            int forcedSpawnConsumed = 0;
            if (output.SpawnRequestCount > 0 && faunaDirector != null)
                forcedSpawnConsumed = ApplySpawnRequests(output, faunaDirector, bridge);

            if (forcedSpawnConsumed > 0 && _pendingForcedThreatCount > 0)
            {
                _pendingForcedThreatCount = math.max(0, _pendingForcedThreatCount - forcedSpawnConsumed);
                if (_pendingForcedThreatCount <= 0)
                    _pendingForcedThreatClass = -1;
            }
        }

        private int ApplySpawnRequests(EncounterJobOutput output, FaunaDirector faunaDirector, HectonDirectorAI bridge)
        {
            EncounterThreatClass threatClass = (EncounterThreatClass)output.SpawnThreatClass;
            int requestCount = math.min(output.SpawnRequestCount, EncounterDirectorJob.MaxSpawnRequestsPerTick);
            int forcedConsumed = 0;
            for (int i = 0; i < requestCount; i++)
            {
                Vector3 spawnPosition = GetSpawnRequestPosition(output, i);
                uint spawnSeed = GetSpawnRequestSeed(output, i);
                if (!faunaDirector.TrySpawnEncounterThreat(
                        threatClass,
                        spawnPosition,
                        spawnSeed,
                        output.SpawnSquadStateBits,
                        i,
                        out GameObject spawnedInstance))
                {
                    if (output.ForcedSpawnConsumed == 0)
                        RefundFailedSpawn(threatClass);
                    continue;
                }

                RegisterTrackedEntity(spawnedInstance, threatClass);
                bridge.HandleThreatSpawned(threatClass, spawnPosition);
                if (output.ForcedSpawnConsumed != 0)
                    forcedConsumed++;
            }

            return forcedConsumed;
        }

        private static Vector3 GetSpawnRequestPosition(EncounterJobOutput output, int index)
        {
            float3 position = index == 1
                ? output.SpawnPosition1
                : index == 2
                    ? output.SpawnPosition2
                    : output.SpawnPosition;
            return new Vector3(position.x, position.y, position.z);
        }

        private static uint GetSpawnRequestSeed(EncounterJobOutput output, int index)
        {
            return index == 1
                ? output.SpawnVariantSeed1
                : index == 2
                    ? output.SpawnVariantSeed2
                    : output.SpawnVariantSeed;
        }

        private void ApplyDespawnRequests(EncounterJobOutput output, FaunaDirector faunaDirector)
        {
            int requestCount = math.min(output.DespawnRequestCount, 3);
            if (requestCount > 0)
                ApplyDespawnRequestEntity(output.DespawnEntityId0, faunaDirector);
            if (requestCount > 1)
                ApplyDespawnRequestEntity(output.DespawnEntityId1, faunaDirector);
            if (requestCount > 2)
                ApplyDespawnRequestEntity(output.DespawnEntityId2, faunaDirector);
        }

        private void ApplyPhaseOverride(EncounterPhase phase, HectonDirectorAI bridge)
        {
            EncounterDirectorState state = _frontState[0];
            EncounterPhase previousPhase = (EncounterPhase)state.ActivePhase;
            state.ActivePhase = (int)phase;
            state.PacingPhaseTimer = 0f;
            state.RecoveryTimer = 0f;
            _frontState[0] = state;
            _backState[0] = state;

            if (previousPhase != phase)
            {
                WriteDebugEvent(DebugEventCodePhaseChange, (int)phase, _frontState[0].IntensityLevel);
                bridge.HandleEncounterPhaseChanged(previousPhase, phase);
            }
        }

        private void ApplyDespawnRequestEntity(int entityId, FaunaDirector faunaDirector)
        {
            if (entityId == 0)
                return;

            float refund = ResolveTrackedTokenCost(entityId) * 0.5f;
            if (faunaDirector.TryRecallEncounterThreat(entityId))
            {
                UntrackEntity(entityId);
                return;
            }

            EncounterDirectorState state = _frontState[0];
            state.TokenBudget = math.clamp(state.TokenBudget - refund, 0f, MaxTokenBudget);
            _frontState[0] = state;
            _backState[0] = state;
        }

        private void WriteDebugEvent(int code, float context, float auxiliary)
        {
            if (!_debugEventRing.IsCreated || !_debugEventHead.IsCreated || _debugEventHead.Length <= 0)
                return;

            int head = _debugEventHead[0];
            int slot = head & (DebugEventRingCapacity - 1);
            _debugEventRing[slot] = new EncounterDebugEvent
            {
                Timestamp = _frameIndex * ColdTickIntervalSeconds,
                Code = code,
                Context = context,
                Auxiliary = auxiliary
            };
            _debugEventHead[0] = head + 1;
        }

        private void RegisterTrackedEntity(GameObject spawnedInstance, EncounterThreatClass threatClass)
        {
            if (spawnedInstance == null)
                return;

            int entityId = unchecked((int)EntityId.ToULong(spawnedInstance.GetEntityId()));
            int slot = FindTrackedSlot(entityId);
            if (slot < 0)
                slot = FindFreeTrackedSlot();
            if (slot < 0)
                return;

            _trackedEntityIds[slot] = entityId;
            _trackedTransforms[slot] = spawnedInstance.transform;
            _trackedThreatClasses[slot] = threatClass;
            _trackedTokenCosts[slot] = ResolveTokenCost(threatClass, _threatAuthoring);
        }

        private void UntrackEntity(int entityId)
        {
            int slot = FindTrackedSlot(entityId);
            if (slot >= 0)
                ClearTrackedSlot(slot);
        }

        private void ClearTrackedSlot(int slot)
        {
            _trackedEntityIds[slot] = 0;
            _trackedTransforms[slot] = null;
            _trackedThreatClasses[slot] = default;
            _trackedTokenCosts[slot] = 0f;
            _enemyTokens[slot] = default;
        }

        private int FindTrackedSlot(int entityId)
        {
            for (int i = 0; i < MaxActiveEnemies; i++)
            {
                if (_trackedEntityIds[i] == entityId)
                    return i;
            }

            return -1;
        }

        private int FindFreeTrackedSlot()
        {
            for (int i = 0; i < MaxActiveEnemies; i++)
            {
                if (_trackedEntityIds[i] == 0)
                    return i;
            }

            return -1;
        }

        private float ResolveTrackedTokenCost(int entityId)
        {
            int slot = FindTrackedSlot(entityId);
            return slot >= 0 ? _trackedTokenCosts[slot] : 0f;
        }

        private void RefundFailedSpawn(EncounterThreatClass threatClass)
        {
            EncounterDirectorState state = _frontState[0];
            state.TokenBudget = math.clamp(state.TokenBudget + ResolveTokenCost(threatClass, _threatAuthoring), 0f, MaxTokenBudget);
            _frontState[0] = state;
            _backState[0] = state;
        }

        private void PrecomputeCandidateDirections()
        {
            float goldenRatio = 1.6180339887f;
            for (int i = 0; i < HighCandidateCount; i++)
            {
                float sample = i + 0.5f;
                float y = 1f - (2f * sample / HighCandidateCount);
                float radius = math.sqrt(math.max(0f, 1f - y * y));
                float theta = 2f * math.PI * sample / goldenRatio;
                _candidateDirections[i] = NormalizeSafe(new float3(math.cos(theta) * radius, y, math.sin(theta) * radius), new float3(0f, 0f, 1f));
            }
        }

        private static int ResolveCandidateCount()
        {
            bool highTier = SystemInfo.processorCount >= 8 && SystemInfo.graphicsMemorySize >= 4096;
            return highTier ? HighCandidateCount : BaseCandidateCount;
        }

        private static float ResolveTokenCost(EncounterThreatClass threatClass, EncounterThreatAuthoringSnapshot authoring)
        {
            switch (threatClass)
            {
                case EncounterThreatClass.Leviathan:
                    return math.max(0f, authoring.LeviathanTokenCost);
                case EncounterThreatClass.Stalker:
                    return math.max(0f, authoring.StalkerTokenCost);
                case EncounterThreatClass.Swarm:
                    return math.max(0f, authoring.SwarmTokenCost);
                default:
                    return math.max(0f, authoring.DroneTokenCost);
            }
        }

        private static EncounterThreatAuthoringSnapshot BuildThreatAuthoringSnapshot(EncounterProfile encounterProfile, ThreatCostTable explicitThreatCostTable)
        {
            EncounterThreatAuthoringSnapshot snapshot = BuildDefaultThreatAuthoringSnapshot();
            ThreatCostTable threatCostTable = explicitThreatCostTable != null
                ? explicitThreatCostTable
                : encounterProfile != null
                    ? encounterProfile.ThreatCostTable
                    : null;

            if (encounterProfile != null)
            {
                snapshot.DroneMinIntensity = encounterProfile.ResolveMinimumIntensity(EncounterThreatClass.Drone, snapshot.DroneMinIntensity);
                snapshot.SwarmMinIntensity = encounterProfile.ResolveMinimumIntensity(EncounterThreatClass.Swarm, snapshot.SwarmMinIntensity);
                snapshot.StalkerMinIntensity = encounterProfile.ResolveMinimumIntensity(EncounterThreatClass.Stalker, snapshot.StalkerMinIntensity);
                snapshot.LeviathanMinIntensity = encounterProfile.ResolveMinimumIntensity(EncounterThreatClass.Leviathan, snapshot.LeviathanMinIntensity);
                snapshot.DroneAllowCriticalHealth = encounterProfile.ResolveAllowDuringCriticalHealth(EncounterThreatClass.Drone, true) ? 1 : 0;
                snapshot.SwarmAllowCriticalHealth = encounterProfile.ResolveAllowDuringCriticalHealth(EncounterThreatClass.Swarm, true) ? 1 : 0;
                snapshot.StalkerAllowCriticalHealth = encounterProfile.ResolveAllowDuringCriticalHealth(EncounterThreatClass.Stalker, false) ? 1 : 0;
                snapshot.LeviathanAllowCriticalHealth = encounterProfile.ResolveAllowDuringCriticalHealth(EncounterThreatClass.Leviathan, false) ? 1 : 0;
            }

            if (threatCostTable != null)
            {
                ApplyThreatCostDefinition(threatCostTable, EncounterThreatClass.Drone, ref snapshot.DroneTokenCost, ref snapshot.DroneMaxSimultaneous, ref snapshot.DroneDespawnPriorityBias);
                ApplyThreatCostDefinition(threatCostTable, EncounterThreatClass.Swarm, ref snapshot.SwarmTokenCost, ref snapshot.SwarmMaxSimultaneous, ref snapshot.SwarmDespawnPriorityBias);
                ApplyThreatCostDefinition(threatCostTable, EncounterThreatClass.Stalker, ref snapshot.StalkerTokenCost, ref snapshot.StalkerMaxSimultaneous, ref snapshot.StalkerDespawnPriorityBias);
                ApplyThreatCostDefinition(threatCostTable, EncounterThreatClass.Leviathan, ref snapshot.LeviathanTokenCost, ref snapshot.LeviathanMaxSimultaneous, ref snapshot.LeviathanDespawnPriorityBias);
            }

            return snapshot;
        }

        private static EncounterThreatAuthoringSnapshot BuildDefaultThreatAuthoringSnapshot()
        {
            EncounterThreatAuthoringSnapshot snapshot = default;
            snapshot.DroneMinIntensity = 0f;
            snapshot.SwarmMinIntensity = 0.25f;
            snapshot.StalkerMinIntensity = 0.55f;
            snapshot.LeviathanMinIntensity = 0.85f;
            snapshot.DroneTokenCost = 10f;
            snapshot.SwarmTokenCost = 20f;
            snapshot.StalkerTokenCost = 35f;
            snapshot.LeviathanTokenCost = 80f;
            snapshot.DroneDespawnPriorityBias = 1.25f;
            snapshot.SwarmDespawnPriorityBias = 1f;
            snapshot.StalkerDespawnPriorityBias = 0.55f;
            snapshot.LeviathanDespawnPriorityBias = 0.15f;
            snapshot.DroneMaxSimultaneous = 8;
            snapshot.SwarmMaxSimultaneous = 4;
            snapshot.StalkerMaxSimultaneous = 3;
            snapshot.LeviathanMaxSimultaneous = 1;
            snapshot.DroneAllowCriticalHealth = 1;
            snapshot.SwarmAllowCriticalHealth = 1;
            snapshot.StalkerAllowCriticalHealth = 0;
            snapshot.LeviathanAllowCriticalHealth = 0;
            return snapshot;
        }

        private static void ApplyThreatCostDefinition(
            ThreatCostTable threatCostTable,
            EncounterThreatClass threatClass,
            ref float tokenCost,
            ref int maxSimultaneous,
            ref float despawnPriorityBias)
        {
            if (!threatCostTable.TryResolveDefinition(threatClass, out ThreatCostDefinition definition))
                return;

            tokenCost = math.max(0f, definition.tokenCost);
            maxSimultaneous = math.max(0, definition.maxSimultaneous);
            despawnPriorityBias = math.max(0f, definition.despawnPriorityBias);
        }

        private static float ResolveDespawnPriorityBias(EncounterThreatClass threatClass, EncounterThreatAuthoringSnapshot authoring)
        {
            switch (threatClass)
            {
                case EncounterThreatClass.Leviathan:
                    return math.max(0f, authoring.LeviathanDespawnPriorityBias);
                case EncounterThreatClass.Stalker:
                    return math.max(0f, authoring.StalkerDespawnPriorityBias);
                case EncounterThreatClass.Swarm:
                    return math.max(0f, authoring.SwarmDespawnPriorityBias);
                default:
                    return math.max(0f, authoring.DroneDespawnPriorityBias);
            }
        }

        private static uint WangHash(uint value)
        {
            value = (value ^ 61u) ^ (value >> 16);
            value *= 9u;
            value = value ^ (value >> 4);
            value *= 0x27d4eb2du;
            value = value ^ (value >> 15);
            return value;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, ref JobHandle handle, ref bool hasDependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            if (hasDependency)
            {
                handle = array.Dispose(handle);
            }
            else
            {
                array.Dispose();
            }

            array = default;
            hasDependency = true;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct EncounterDirectorJob : IJobParallelFor
    {
        private const float StressTau = 8f;
        private const float LoadShedThresholdMs = 20f;
        private const float EmergencyThresholdMs = 33.3f;
        private const float RecoveryThresholdMs = 16.67f;
        private const float BuildUpMinSeconds = 45f;
        private const float BuildUpMaxSeconds = 90f;
        private const float PeakMinSeconds = 15f;
        private const float PeakMaxSeconds = 30f;
        private const float DecayMinSeconds = 20f;
        private const float DecayMaxSeconds = 40f;
        private const float RelaxMinSeconds = 30f;
        private const float RelaxMaxSeconds = 60f;
        private const float MaxTokenBudget = 100f;
        private const float FrustumRejectPadding = 3f;
        private const float CriticalHealthSpawnSuppressionThreshold = 0.15f;
        private const float SpawnClusterRadiusSq = 15f * 15f;
        private const float MinSpawnRadius = 50f;
        private const float MaxSpawnRadius = 150f;
        private const float DespawnKeepDistanceSq = 25f * 25f;
        private const float SafeIdleStressDecayPerTick = 0.06f;
        private const float SelectionEpsilon = 0.0001f;
        private const int HunterSquadOverrideMinSimultaneous = 3;
        internal const int MaxSpawnRequestsPerTick = 3;
        private const uint HunterSquadHuntingFlankStateBits = (1u << 0) | (1u << 2);

        public EncounterDirectorState CurrentState;
        public NativeArray<EncounterDirectorState> WriteState;
        [ReadOnly] public NativeArray<EncounterEnemyToken> ActiveEnemies;
        [ReadOnly] public NativeArray<float4> FrustumPlanes;
        [ReadOnly] public NativeArray<float3> CandidateDirections;
        public int CandidateCount;
        public float4 PlayerPosition;
        public float4 PlayerVelocity;
        public float4 PlayerForward;
        public float PlayerHealthNormalized;
        public float PlayerOxygenNormalized;
        public float PlayerInternalStress;
        public float AcousticThreatLevel;
        public float AvgFrameTimeMs;
        public float SurfaceWorldY;
        public int ForcedThreatClass;
        public int ForcedThreatCount;
        public EncounterThreatAuthoringSnapshot ThreatAuthoring;
        public NativeArray<EncounterJobOutput> Output;

        public void Execute(int index)
        {
            if (index != 0)
                return;

            EncounterDirectorState state = CurrentState;
            EncounterJobOutput output = default;

            float3 playerPosition = PlayerPosition.xyz;
            float3 playerForward = NormalizeSafe(PlayerForward.xyz, new float3(0f, 0f, 1f));
            int activeEnemyCount = 0;
            int4 threatClassCounts = int4.zero;
            float nearestThreatDistanceSq = float.MaxValue;
            float bestPriority0 = float.MinValue;
            float bestPriority1 = float.MinValue;
            float bestPriority2 = float.MinValue;
            int bestEntity0 = 0;
            int bestEntity1 = 0;
            int bestEntity2 = 0;
            float bestCost0 = 0f;
            float bestCost1 = 0f;
            float bestCost2 = 0f;

            for (int i = 0; i < ActiveEnemies.Length; i++)
            {
                EncounterEnemyToken token = ActiveEnemies[i];
                if (token.EntityId == 0)
                    continue;

                float3 toThreat = token.Position - playerPosition;
                float distSq = math.lengthsq(toThreat);
                if (distSq < nearestThreatDistanceSq)
                    nearestThreatDistanceSq = distSq;

                bool insideOrIntersectingFrustum = TestPlanesAABB(token.Position, new float3(FrustumRejectPadding));
                float visibilityFactor = insideOrIntersectingFrustum ? 0f : 1f;
                float priority = distSq * visibilityFactor * math.max(token.DespawnPriority, 0f);

                activeEnemyCount++;
                switch ((EncounterThreatClass)token.ThreatClass)
                {
                    case EncounterThreatClass.Leviathan:
                        threatClassCounts.w++;
                        break;
                    case EncounterThreatClass.Stalker:
                        threatClassCounts.y++;
                        break;
                    case EncounterThreatClass.Swarm:
                        threatClassCounts.z++;
                        break;
                    default:
                        threatClassCounts.x++;
                        break;
                }

                if (AvgFrameTimeMs <= LoadShedThresholdMs)
                    continue;

                if (distSq <= DespawnKeepDistanceSq)
                    continue;

                if ((EncounterPhase)state.ActivePhase == EncounterPhase.Peak &&
                    state.StressLevel > 0.8f &&
                    token.ThreatClass == (int)EncounterThreatClass.Leviathan)
                {
                    continue;
                }

                InsertBestCandidate(priority, token.EntityId, token.TokenCost, ref bestPriority0, ref bestPriority1, ref bestPriority2, ref bestEntity0, ref bestEntity1, ref bestEntity2, ref bestCost0, ref bestCost1, ref bestCost2);
            }

            float proximityStress = nearestThreatDistanceSq < float.MaxValue
                ? math.saturate(1f - math.sqrt(nearestThreatDistanceSq) / 50f)
                : 0f;
            float depthStress = math.saturate(PlayerPosition.w / 4000f) * 0.4f;
            float velocityStress = math.saturate(PlayerVelocity.w / 12f) * 0.15f;
            float healthStress = 1f - PlayerHealthNormalized;
            float oxygenStress = 1f - PlayerOxygenNormalized;
            float acousticStress = math.saturate(AcousticThreatLevel);
            float rawStress = 0.35f * healthStress +
                              0.25f * oxygenStress +
                              0.25f * proximityStress +
                              0.10f * depthStress +
                              0.05f * velocityStress;
            rawStress = math.saturate(math.max(rawStress, acousticStress));
            float safeIdleRecovery = math.saturate(math.min(
                math.min(PlayerHealthNormalized, PlayerOxygenNormalized),
                math.min(
                    1f - proximityStress,
                    math.min(
                        1f - math.saturate(PlayerVelocity.w / 1.25f),
                        1f - math.max(acousticStress, PlayerInternalStress)))));
            rawStress *= math.lerp(1f, 0.25f, safeIdleRecovery);
            float alpha = ApproximateOneMinusExpNegPositive(1f / StressTau);
            state.StressLevel += alpha * (math.saturate(rawStress) - state.StressLevel);
            state.StressLevel = math.max(0f, state.StressLevel - safeIdleRecovery * SafeIdleStressDecayPerTick);
            state.StressLevel = math.clamp(state.StressLevel, 0f, 1f);

            state.PacingPhaseTimer += 1f;
            float phaseDuration = ResolvePhaseDuration((EncounterPhase)state.ActivePhase, state.StressLevel);
            if (state.PacingPhaseTimer >= phaseDuration)
            {
                output.PhaseChanged = 1;
                output.PreviousPhase = state.ActivePhase;
                state.PacingPhaseTimer = 0f;
                state.ActivePhase = (state.ActivePhase + 1) & 0x3;
                output.NewPhase = state.ActivePhase;
            }

            float phaseIntensity = ResolvePhaseIntensity((EncounterPhase)state.ActivePhase, state.PacingPhaseTimer, phaseDuration);
            state.IntensityLevel = math.clamp(phaseIntensity, 0f, 1f);
            if (!math.isfinite(state.IntensityLevel))
                state.IntensityLevel = 0f;

            EncounterBudgetFlags budgetFlags = (EncounterBudgetFlags)state.BudgetFlags;
            if (AvgFrameTimeMs > EmergencyThresholdMs)
            {
                budgetFlags |= EncounterBudgetFlags.LoadSheddingActive | EncounterBudgetFlags.SpawnSuspended | EncounterBudgetFlags.EmergencyRecall;
                state.RecoveryTimer = 0f;
            }
            else if (AvgFrameTimeMs > LoadShedThresholdMs)
            {
                budgetFlags |= EncounterBudgetFlags.LoadSheddingActive | EncounterBudgetFlags.SpawnSuspended;
                budgetFlags &= ~EncounterBudgetFlags.EmergencyRecall;
                state.RecoveryTimer = 0f;
            }
            else if (AvgFrameTimeMs <= RecoveryThresholdMs)
            {
                state.RecoveryTimer += 1f;
                if (state.RecoveryTimer >= 3f)
                {
                    budgetFlags &= ~(EncounterBudgetFlags.LoadSheddingActive | EncounterBudgetFlags.SpawnSuspended | EncounterBudgetFlags.EmergencyRecall);
                }
            }
            else
            {
                state.RecoveryTimer = 0f;
            }

            state.BudgetFlags = (int)budgetFlags;

            bool criticalHealthSuppressed = PlayerHealthNormalized <= CriticalHealthSpawnSuppressionThreshold;
            if (((EncounterBudgetFlags)state.BudgetFlags & EncounterBudgetFlags.RegenBlocked) != 0)
            {
                state.TokenRegenRate = 0f;
            }
            else if (criticalHealthSuppressed)
            {
                state.TokenRegenRate = 0f;
                state.TokenBudget = 0f;
            }
            else
            {
                float lowStressCredit01 = math.saturate(1f - (state.StressLevel / 0.35f));
                float lowStressRegen = lowStressCredit01 > 0f ? math.lerp(4f, 10f, lowStressCredit01) : 0f;
                float relaxRegen = (EncounterPhase)state.ActivePhase == EncounterPhase.Relax ? 8f : 0f;
                state.TokenRegenRate = math.max(relaxRegen, lowStressRegen);
            }

            state.TokenBudget = math.clamp(state.TokenBudget + state.TokenRegenRate, 0f, MaxTokenBudget);

            if (AvgFrameTimeMs > LoadShedThresholdMs)
            {
                int shedCount = AvgFrameTimeMs > EmergencyThresholdMs ? 3 : 1;
                if (bestEntity0 != 0 && shedCount > 0)
                {
                    output.DespawnRequestCount++;
                    output.DespawnEntityId0 = bestEntity0;
                    state.TokenBudget = math.clamp(state.TokenBudget + bestCost0 * 0.5f, 0f, MaxTokenBudget);
                    activeEnemyCount--;
                    DecrementThreatClassCount(ref threatClassCounts, bestEntity0, ActiveEnemies);
                }

                if (bestEntity1 != 0 && shedCount > 1)
                {
                    output.DespawnRequestCount++;
                    output.DespawnEntityId1 = bestEntity1;
                    state.TokenBudget = math.clamp(state.TokenBudget + bestCost1 * 0.5f, 0f, MaxTokenBudget);
                    activeEnemyCount--;
                    DecrementThreatClassCount(ref threatClassCounts, bestEntity1, ActiveEnemies);
                }

                if (bestEntity2 != 0 && shedCount > 2)
                {
                    output.DespawnRequestCount++;
                    output.DespawnEntityId2 = bestEntity2;
                    state.TokenBudget = math.clamp(state.TokenBudget + bestCost2 * 0.5f, 0f, MaxTokenBudget);
                    activeEnemyCount--;
                    DecrementThreatClassCount(ref threatClassCounts, bestEntity2, ActiveEnemies);
                }
            }

            if (criticalHealthSuppressed)
                state.TokenBudget = 0f;

            bool forceSpawn = !criticalHealthSuppressed && ForcedThreatCount > 0 && ForcedThreatClass >= 0;
            bool spawnCadenceOpen = forceSpawn || !criticalHealthSuppressed || ((((int)state.PacingPhaseTimer) & 0x3) == 0);
            if ((forceSpawn || (EncounterPhase)state.ActivePhase != EncounterPhase.Relax) &&
                ((EncounterBudgetFlags)state.BudgetFlags & (EncounterBudgetFlags.LoadSheddingActive | EncounterBudgetFlags.SpawnSuspended)) == 0 &&
                activeEnemyCount < 32 &&
                spawnCadenceOpen)
            {
                EncounterThreatClass threatClass;
                bool resolvedThreatClass = forceSpawn
                    ? TryResolveForcedThreatClass(ForcedThreatClass, threatClassCounts, ThreatAuthoring, out threatClass)
                    : TryResolveDesiredThreatClass(state.IntensityLevel, state.TokenBudget, criticalHealthSuppressed, threatClassCounts, ThreatAuthoring, out threatClass);
                if (resolvedThreatClass)
                {
                    float spawnCost = ResolveTokenCost(threatClass, ThreatAuthoring);
                    int maxSimultaneous = ResolveMaxSimultaneous(threatClass, ThreatAuthoring);
                    if (forceSpawn && threatClass == EncounterThreatClass.Stalker)
                        maxSimultaneous = math.max(maxSimultaneous, HunterSquadOverrideMinSimultaneous);

                    int availableThreatSlots = math.max(0, maxSimultaneous - ResolveThreatClassCount(threatClass, threatClassCounts));
                    int availableEnemySlots = math.max(0, 32 - activeEnemyCount);
                    int requestedSpawnCount = forceSpawn
                        ? math.min(math.min(ForcedThreatCount, HunterSquadOverrideMinSimultaneous), MaxSpawnRequestsPerTick)
                        : 1;
                    requestedSpawnCount = math.min(requestedSpawnCount, math.min(availableThreatSlots, availableEnemySlots));
                    float3 reserved0 = default;
                    float3 reserved1 = default;
                    int reservedCount = 0;
                    for (int spawnIndex = 0; spawnIndex < requestedSpawnCount; spawnIndex++)
                    {
                        if (!TryResolveSpawnCandidate(playerPosition, playerForward, forceSpawn, reservedCount, reserved0, reserved1, out float3 spawnPosition))
                            break;

                        uint spawnSequence = state.SpawnSequence + 1u;
                        uint spawnSeed = EncounterDirector.BuildDeterministicSeed(
                            new Vector3(playerPosition.x, playerPosition.y, playerPosition.z),
                            unchecked((int)spawnSequence),
                            state.ActivePhase,
                            activeEnemyCount + spawnIndex);
                        WriteSpawnSlot(ref output, spawnIndex, spawnPosition, spawnSeed);
                        state.SpawnSequence = spawnSequence;
                        if (!forceSpawn)
                            state.TokenBudget = math.clamp(state.TokenBudget - spawnCost, 0f, MaxTokenBudget);
                        activeEnemyCount++;
                        IncrementThreatClassCount(ref threatClassCounts, threatClass);
                        if (reservedCount == 0)
                            reserved0 = spawnPosition;
                        else if (reservedCount == 1)
                            reserved1 = spawnPosition;
                        reservedCount++;
                    }

                    if (reservedCount > 0)
                    {
                        output.SpawnRequestCount = reservedCount;
                        output.SpawnThreatClass = (int)threatClass;
                        output.ForcedSpawnConsumed = forceSpawn ? reservedCount : 0;
                        output.SpawnSquadStateBits = forceSpawn && threatClass == EncounterThreatClass.Stalker
                            ? HunterSquadHuntingFlankStateBits
                            : 0u;
                    }
                }
            }

            state.ActiveEnemyCount = math.max(0, activeEnemyCount);
            state.PlayerPosition = PlayerPosition;
            state.PlayerVelocity = PlayerVelocity;
            WriteState[0] = state;
            Output[0] = output;
        }

        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > 0.000001f
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        private static float ApproximateOneMinusExpNegPositive(float x)
        {
            return math.saturate(1f - ApproximateExpNegPositive(x));
        }

        private static float ApproximateExpNegPositive(float x)
        {
            float clamped = math.clamp(x, 0f, 8f);
            float x2 = clamped * clamped;
            float x3 = x2 * clamped;
            float numerator = 120f - (60f * clamped) + (12f * x2) - x3;
            float denominator = 120f + (60f * clamped) + (12f * x2) + x3;
            return math.saturate(numerator / math.max(denominator, 0.0001f));
        }

        private bool TryResolveSpawnCandidate(
            float3 playerPosition,
            float3 playerForward,
            bool preferFarEdge,
            int reservedCount,
            float3 reserved0,
            float3 reserved1,
            out float3 spawnPosition)
        {
            spawnPosition = float3.zero;
            float bestScore = float.MinValue;
            bool found = false;

            int directionCount = math.min(CandidateCount, CandidateDirections.Length);
            for (int i = 0; i < directionCount; i++)
            {
                float normalizedIndex = directionCount > 1 ? (float)i / (directionCount - 1) : 0f;
                if (preferFarEdge && normalizedIndex < 0.65f)
                    continue;

                float radius = preferFarEdge
                    ? MaxSpawnRadius
                    : math.lerp(MinSpawnRadius, MaxSpawnRadius, normalizedIndex);
                float3 candidate = playerPosition + CandidateDirections[i] * radius;

                if (candidate.y > SurfaceWorldY - 2f)
                    continue;

                if (TestPlanesAABB(candidate, new float3(FrustumRejectPadding)))
                    continue;

                if (!HasEnemyClearance(candidate))
                    continue;

                if (reservedCount > 0 && math.lengthsq(candidate - reserved0) < SpawnClusterRadiusSq)
                    continue;

                if (reservedCount > 1 && math.lengthsq(candidate - reserved1) < SpawnClusterRadiusSq)
                    continue;

                float3 toCandidate = NormalizeSafe(candidate - playerPosition, playerForward);
                float score = math.lengthsq(candidate - playerPosition) +
                              500f * (1f - math.dot(playerForward, toCandidate));
                if (score <= bestScore)
                    continue;

                bestScore = score;
                spawnPosition = candidate;
                found = true;
            }

            return found;
        }

        private static void WriteSpawnSlot(ref EncounterJobOutput output, int index, float3 position, uint seed)
        {
            if (index == 1)
            {
                output.SpawnPosition1 = position;
                output.SpawnVariantSeed1 = seed;
                return;
            }

            if (index == 2)
            {
                output.SpawnPosition2 = position;
                output.SpawnVariantSeed2 = seed;
                return;
            }

            output.SpawnPosition = position;
            output.SpawnVariantSeed = seed;
        }

        private static bool TryResolveForcedThreatClass(
            int forcedThreatClass,
            int4 threatClassCounts,
            EncounterThreatAuthoringSnapshot authoring,
            out EncounterThreatClass threatClass)
        {
            threatClass = (EncounterThreatClass)forcedThreatClass;
            if (forcedThreatClass < 0)
                return false;

            int maxSimultaneous = ResolveMaxSimultaneous(threatClass, authoring);
            if (threatClass == EncounterThreatClass.Stalker)
                maxSimultaneous = math.max(maxSimultaneous, HunterSquadOverrideMinSimultaneous);

            return ResolveThreatClassCount(threatClass, threatClassCounts) < maxSimultaneous;
        }

        private bool HasEnemyClearance(float3 candidate)
        {
            for (int i = 0; i < ActiveEnemies.Length; i++)
            {
                EncounterEnemyToken token = ActiveEnemies[i];
                if (token.EntityId == 0)
                    continue;

                if (math.lengthsq(token.Position - candidate) < SpawnClusterRadiusSq)
                    return false;
            }

            return true;
        }

        private static bool HasReservedSpawnClearance(float3 candidate, int reservedCount, float3 reserved0, float3 reserved1)
        {
            if (reservedCount > 0 && math.lengthsq(candidate - reserved0) < SpawnClusterRadiusSq)
                return false;

            if (reservedCount > 1 && math.lengthsq(candidate - reserved1) < SpawnClusterRadiusSq)
                return false;

            return true;
        }

        private bool TestPlanesAABB(float3 center, float3 extents)
        {
            if (FrustumPlanes.Length < EncounterDirector.FrustumPlaneCount)
                return true;

            for (int planeIndex = 0; planeIndex < EncounterDirector.FrustumPlaneCount; planeIndex++)
            {
                float4 plane = FrustumPlanes[planeIndex];
                float projectedRadius = math.dot(math.abs(plane.xyz), extents);
                float signedDistance = math.dot(plane.xyz, center) + plane.w;
                if (signedDistance + projectedRadius < 0f)
                    return false;
            }

            return true;
        }

        private static bool TryResolveDesiredThreatClass(
            float intensityLevel,
            float tokenBudget,
            bool criticalHealthSuppressed,
            int4 threatClassCounts,
            EncounterThreatAuthoringSnapshot authoring,
            out EncounterThreatClass threatClass)
        {
            if (CanSpawnThreatClass(EncounterThreatClass.Leviathan, intensityLevel, tokenBudget, criticalHealthSuppressed, threatClassCounts, authoring))
            {
                threatClass = EncounterThreatClass.Leviathan;
                return true;
            }

            if (CanSpawnThreatClass(EncounterThreatClass.Stalker, intensityLevel, tokenBudget, criticalHealthSuppressed, threatClassCounts, authoring))
            {
                threatClass = EncounterThreatClass.Stalker;
                return true;
            }

            if (CanSpawnThreatClass(EncounterThreatClass.Swarm, intensityLevel, tokenBudget, criticalHealthSuppressed, threatClassCounts, authoring))
            {
                threatClass = EncounterThreatClass.Swarm;
                return true;
            }

            if (CanSpawnThreatClass(EncounterThreatClass.Drone, intensityLevel, tokenBudget, criticalHealthSuppressed, threatClassCounts, authoring))
            {
                threatClass = EncounterThreatClass.Drone;
                return true;
            }

            threatClass = EncounterThreatClass.Drone;
            return false;
        }

        private static bool CanSpawnThreatClass(
            EncounterThreatClass threatClass,
            float intensityLevel,
            float tokenBudget,
            bool criticalHealthSuppressed,
            int4 threatClassCounts,
            EncounterThreatAuthoringSnapshot authoring)
        {
            if (criticalHealthSuppressed && !AllowsCriticalHealthSpawn(threatClass, authoring))
                return false;

            if (intensityLevel + SelectionEpsilon < ResolveMinimumIntensity(threatClass, authoring))
                return false;

            if (tokenBudget + SelectionEpsilon < ResolveTokenCost(threatClass, authoring))
                return false;

            return ResolveThreatClassCount(threatClass, threatClassCounts) < ResolveMaxSimultaneous(threatClass, authoring);
        }

        private static float ResolveMinimumIntensity(EncounterThreatClass threatClass, EncounterThreatAuthoringSnapshot authoring)
        {
            switch (threatClass)
            {
                case EncounterThreatClass.Leviathan:
                    return authoring.LeviathanMinIntensity;
                case EncounterThreatClass.Stalker:
                    return authoring.StalkerMinIntensity;
                case EncounterThreatClass.Swarm:
                    return authoring.SwarmMinIntensity;
                default:
                    return authoring.DroneMinIntensity;
            }
        }

        private static float ResolveTokenCost(EncounterThreatClass threatClass, EncounterThreatAuthoringSnapshot authoring)
        {
            switch (threatClass)
            {
                case EncounterThreatClass.Leviathan:
                    return authoring.LeviathanTokenCost;
                case EncounterThreatClass.Stalker:
                    return authoring.StalkerTokenCost;
                case EncounterThreatClass.Swarm:
                    return authoring.SwarmTokenCost;
                default:
                    return authoring.DroneTokenCost;
            }
        }

        private static int ResolveMaxSimultaneous(EncounterThreatClass threatClass, EncounterThreatAuthoringSnapshot authoring)
        {
            switch (threatClass)
            {
                case EncounterThreatClass.Leviathan:
                    return authoring.LeviathanMaxSimultaneous;
                case EncounterThreatClass.Stalker:
                    return authoring.StalkerMaxSimultaneous;
                case EncounterThreatClass.Swarm:
                    return authoring.SwarmMaxSimultaneous;
                default:
                    return authoring.DroneMaxSimultaneous;
            }
        }

        private static bool AllowsCriticalHealthSpawn(EncounterThreatClass threatClass, EncounterThreatAuthoringSnapshot authoring)
        {
            switch (threatClass)
            {
                case EncounterThreatClass.Leviathan:
                    return authoring.LeviathanAllowCriticalHealth != 0;
                case EncounterThreatClass.Stalker:
                    return authoring.StalkerAllowCriticalHealth != 0;
                case EncounterThreatClass.Swarm:
                    return authoring.SwarmAllowCriticalHealth != 0;
                default:
                    return authoring.DroneAllowCriticalHealth != 0;
            }
        }

        private static int ResolveThreatClassCount(EncounterThreatClass threatClass, int4 threatClassCounts)
        {
            switch (threatClass)
            {
                case EncounterThreatClass.Leviathan:
                    return threatClassCounts.w;
                case EncounterThreatClass.Stalker:
                    return threatClassCounts.y;
                case EncounterThreatClass.Swarm:
                    return threatClassCounts.z;
                default:
                    return threatClassCounts.x;
            }
        }

        private static void IncrementThreatClassCount(ref int4 threatClassCounts, EncounterThreatClass threatClass)
        {
            switch (threatClass)
            {
                case EncounterThreatClass.Leviathan:
                    threatClassCounts.w++;
                    break;
                case EncounterThreatClass.Stalker:
                    threatClassCounts.y++;
                    break;
                case EncounterThreatClass.Swarm:
                    threatClassCounts.z++;
                    break;
                default:
                    threatClassCounts.x++;
                    break;
            }
        }

        private static void DecrementThreatClassCount(ref int4 threatClassCounts, int entityId, NativeArray<EncounterEnemyToken> activeEnemies)
        {
            for (int i = 0; i < activeEnemies.Length; i++)
            {
                EncounterEnemyToken token = activeEnemies[i];
                if (token.EntityId != entityId)
                    continue;

                switch ((EncounterThreatClass)token.ThreatClass)
                {
                    case EncounterThreatClass.Leviathan:
                        threatClassCounts.w = math.max(0, threatClassCounts.w - 1);
                        break;
                    case EncounterThreatClass.Stalker:
                        threatClassCounts.y = math.max(0, threatClassCounts.y - 1);
                        break;
                    case EncounterThreatClass.Swarm:
                        threatClassCounts.z = math.max(0, threatClassCounts.z - 1);
                        break;
                    default:
                        threatClassCounts.x = math.max(0, threatClassCounts.x - 1);
                        break;
                }

                return;
            }
        }

        private static float ResolvePhaseDuration(EncounterPhase phase, float stressLevel)
        {
            switch (phase)
            {
                case EncounterPhase.BuildUp:
                    return math.lerp(BuildUpMinSeconds, BuildUpMaxSeconds, stressLevel);
                case EncounterPhase.Peak:
                    return math.lerp(PeakMinSeconds, PeakMaxSeconds, stressLevel);
                case EncounterPhase.Decay:
                    return math.lerp(DecayMinSeconds, DecayMaxSeconds, stressLevel);
                default:
                    return math.lerp(RelaxMinSeconds, RelaxMaxSeconds, stressLevel);
            }
        }

        private static float ResolvePhaseIntensity(EncounterPhase phase, float timer, float duration)
        {
            float normalizedTime = duration > 0.0001f ? math.saturate(timer / duration) : 0f;
            switch (phase)
            {
                case EncounterPhase.BuildUp:
                    return math.pow(math.sin((math.PI * 0.5f) * normalizedTime), 1.5f);
                case EncounterPhase.Peak:
                    return 1f - 0.1f * math.sin((2f * math.PI) * normalizedTime);
                case EncounterPhase.Decay:
                    return math.pow(math.cos((math.PI * 0.5f) * normalizedTime), 0.7f);
                default:
                    return 0.05f + 0.05f * math.sin(math.PI * normalizedTime);
            }
        }

        private static void InsertBestCandidate(
            float priority,
            int entityId,
            float tokenCost,
            ref float bestPriority0,
            ref float bestPriority1,
            ref float bestPriority2,
            ref int bestEntity0,
            ref int bestEntity1,
            ref int bestEntity2,
            ref float bestCost0,
            ref float bestCost1,
            ref float bestCost2)
        {
            if (priority > bestPriority0)
            {
                bestPriority2 = bestPriority1;
                bestEntity2 = bestEntity1;
                bestCost2 = bestCost1;
                bestPriority1 = bestPriority0;
                bestEntity1 = bestEntity0;
                bestCost1 = bestCost0;
                bestPriority0 = priority;
                bestEntity0 = entityId;
                bestCost0 = tokenCost;
                return;
            }

            if (priority > bestPriority1)
            {
                bestPriority2 = bestPriority1;
                bestEntity2 = bestEntity1;
                bestCost2 = bestCost1;
                bestPriority1 = priority;
                bestEntity1 = entityId;
                bestCost1 = tokenCost;
                return;
            }

            if (priority > bestPriority2)
            {
                bestPriority2 = priority;
                bestEntity2 = entityId;
                bestCost2 = tokenCost;
            }
        }
    }
}
