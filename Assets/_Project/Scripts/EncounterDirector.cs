using System;
using Hecton8.AI;
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
        public float PlayerDepth;
        public float AvgFrameTimeMs;
        public float SurfaceWorldY;
    }

    internal struct EncounterJobOutput
    {
        public int SpawnRequestCount;
        public int SpawnThreatClass;
        public float3 SpawnPosition;
        public uint SpawnVariantSeed;
        public int DespawnRequestCount;
        public int DespawnEntityId0;
        public int DespawnEntityId1;
        public int DespawnEntityId2;
        public int PhaseChanged;
        public int PreviousPhase;
        public int NewPhase;
    }

    internal sealed class EncounterDirector : IDisposable
    {
        internal const int FrustumPlaneCount = 6;

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

        private NativeArray<EncounterDirectorState> _frontState;
        private NativeArray<EncounterDirectorState> _backState;
        private NativeArray<EncounterEnemyToken> _enemyTokens;
        private NativeArray<float4> _frustumPlanes;
        private NativeArray<float3> _candidateDirections;
        private NativeArray<EncounterJobOutput> _jobOutput;
        // COLD ALLOC: Transform[32] — tracked live encounter proxies for token refresh — owner: EncounterDirector
        private readonly Transform[] _trackedTransforms;
        // COLD ALLOC: int[32] — tracked live encounter entity ids — owner: EncounterDirector
        private readonly int[] _trackedEntityIds;
        // COLD ALLOC: EncounterThreatClass[32] — tracked encounter threat classes — owner: EncounterDirector
        private readonly EncounterThreatClass[] _trackedThreatClasses;
        // COLD ALLOC: float[32] — tracked encounter token costs — owner: EncounterDirector
        private readonly float[] _trackedTokenCosts;

        private JobHandle _activeJobHandle;
        private bool _jobScheduled;
        private float _coldTickAccumulator;
        private int _frameIndex;
        private readonly int _candidateCount;
        private int _pendingPhaseOverride = -1;
        private bool _pendingReset;

        internal EncounterDirector()
        {
            _frontState = new NativeArray<EncounterDirectorState>(1, Allocator.Persistent);
            _backState = new NativeArray<EncounterDirectorState>(1, Allocator.Persistent);
            _enemyTokens = new NativeArray<EncounterEnemyToken>(MaxActiveEnemies, Allocator.Persistent);
            _frustumPlanes = new NativeArray<float4>(FrustumPlaneCount, Allocator.Persistent);
            _candidateDirections = new NativeArray<float3>(HighCandidateCount, Allocator.Persistent);
            _jobOutput = new NativeArray<EncounterJobOutput>(1, Allocator.Persistent);
            _trackedTransforms = new Transform[MaxActiveEnemies];
            _trackedEntityIds = new int[MaxActiveEnemies];
            _trackedThreatClasses = new EncounterThreatClass[MaxActiveEnemies];
            _trackedTokenCosts = new float[MaxActiveEnemies];
            _candidateCount = ResolveCandidateCount();

            PrecomputeCandidateDirections();
            Reset();
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
            state.TokenBudget = MaxTokenBudget;
            state.ActivePhase = (int)EncounterPhase.BuildUp;
            _frontState[0] = state;
            _backState[0] = state;
            _jobOutput[0] = default;
            _coldTickAccumulator = 0f;
            _frameIndex = 0;
            _pendingPhaseOverride = -1;
            _pendingReset = false;

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
                _activeJobHandle.Complete();
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
            uint hash = WangHash((uint)(grid.x * 73856093));
            hash ^= WangHash((uint)(grid.y * 19349663));
            hash ^= WangHash((uint)(grid.z * 83492791));
            hash ^= WangHash((uint)(sequenceSalt * 1664525));
            hash ^= WangHash((uint)(phase * 1013904223));
            hash ^= WangHash((uint)(activeEnemyCount * 214013));
            return hash == 0u ? 1u : hash;
        }

        internal static void FillFallbackFrustumPlanes(Vector3 origin, Vector3 forward, Plane[] destination)
        {
            Vector3 safeForward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, safeForward);
            if (right.sqrMagnitude <= 0.0001f)
                right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(safeForward, right).normalized;

            Vector3 nearCenter = origin + safeForward * MinSpawnRadius;
            destination[0] = new Plane(safeForward, nearCenter);
            destination[1] = new Plane(-safeForward, origin + safeForward * MaxSpawnRadius);
            destination[2] = new Plane((safeForward - right).normalized, origin);
            destination[3] = new Plane((safeForward + right).normalized, origin);
            destination[4] = new Plane((safeForward - up).normalized, origin);
            destination[5] = new Plane((safeForward + up).normalized, origin);
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
                token.DespawnPriority = 0f;
                _enemyTokens[i] = token;
            }
        }

        private void ScheduleColdTick(EncounterFrameContext frameContext)
        {
            EncounterDirectorState currentState = _frontState[0];
            currentState.PlayerPosition = new float4(frameContext.PlayerPosition, frameContext.PlayerDepth);
            currentState.PlayerVelocity = new float4(frameContext.PlayerVelocity, math.length(frameContext.PlayerVelocity));

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
                PlayerForward = new float4(math.normalizesafe(frameContext.PlayerForward, new float3(0f, 0f, 1f)), 0f),
                PlayerHealthNormalized = math.clamp(frameContext.PlayerHealthNormalized, 0f, 1f),
                PlayerOxygenNormalized = math.clamp(frameContext.PlayerOxygenNormalized, 0f, 1f),
                PlayerInternalStress = math.clamp(frameContext.PlayerInternalStress, 0f, 1f),
                AvgFrameTimeMs = math.max(0f, frameContext.AvgFrameTimeMs),
                SurfaceWorldY = frameContext.SurfaceWorldY,
                Output = _jobOutput
            };

            _frameIndex++;
            _jobOutput[0] = default;
            _activeJobHandle = job.Schedule();
            _jobScheduled = true;
        }

        private void ApplyCompletedOutput(FaunaDirector faunaDirector, HectonDirectorAI bridge)
        {
            EncounterJobOutput output = _jobOutput[0];

            if (output.PhaseChanged != 0)
                bridge.HandleEncounterPhaseChanged((EncounterPhase)output.PreviousPhase, (EncounterPhase)output.NewPhase);

            if (output.DespawnRequestCount > 0 && faunaDirector != null)
                ApplyDespawnRequests(output, faunaDirector);

            if (output.SpawnRequestCount > 0 && faunaDirector != null)
                ApplySpawnRequest(output, faunaDirector, bridge);
        }

        private void ApplySpawnRequest(EncounterJobOutput output, FaunaDirector faunaDirector, HectonDirectorAI bridge)
        {
            EncounterThreatClass threatClass = (EncounterThreatClass)output.SpawnThreatClass;
            if (!faunaDirector.TrySpawnEncounterThreat(threatClass, output.SpawnPosition, output.SpawnVariantSeed, out GameObject spawnedInstance))
            {
                RefundFailedSpawn(threatClass);
                return;
            }

            RegisterTrackedEntity(spawnedInstance, threatClass);
            bridge.HandleThreatSpawned(threatClass, output.SpawnPosition);
        }

        private void ApplyDespawnRequests(EncounterJobOutput output, FaunaDirector faunaDirector)
        {
            int[] ids = { output.DespawnEntityId0, output.DespawnEntityId1, output.DespawnEntityId2 };
            int requestCount = math.min(output.DespawnRequestCount, ids.Length);

            for (int i = 0; i < requestCount; i++)
            {
                int entityId = ids[i];
                if (entityId == 0)
                    continue;

                float refund = ResolveTrackedTokenCost(entityId) * 0.5f;
                if (faunaDirector.TryRecallEncounterThreat(entityId))
                {
                    UntrackEntity(entityId);
                    continue;
                }

                EncounterDirectorState state = _frontState[0];
                state.TokenBudget = math.clamp(state.TokenBudget - refund, 0f, MaxTokenBudget);
                _frontState[0] = state;
                _backState[0] = state;
            }
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
                bridge.HandleEncounterPhaseChanged(previousPhase, phase);
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
            _trackedTokenCosts[slot] = ResolveTokenCost(threatClass);
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
            state.TokenBudget = math.clamp(state.TokenBudget + ResolveTokenCost(threatClass), 0f, MaxTokenBudget);
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
                _candidateDirections[i] = math.normalizesafe(new float3(math.cos(theta) * radius, y, math.sin(theta) * radius), new float3(0f, 0f, 1f));
            }
        }

        private static int ResolveCandidateCount()
        {
            bool highTier = SystemInfo.processorCount >= 8 && SystemInfo.graphicsMemorySize >= 4096;
            return highTier ? HighCandidateCount : BaseCandidateCount;
        }

        private static float ResolveTokenCost(EncounterThreatClass threatClass)
        {
            switch (threatClass)
            {
                case EncounterThreatClass.Leviathan:
                    return 80f;
                case EncounterThreatClass.Stalker:
                    return 35f;
                case EncounterThreatClass.Swarm:
                    return 20f;
                default:
                    return 10f;
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

    [BurstCompile(CompileSynchronously = true)]
    internal struct EncounterDirectorJob : IJob
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
        private const float SpawnClusterRadiusSq = 15f * 15f;
        private const float MinSpawnRadius = 50f;
        private const float MaxSpawnRadius = 150f;
        private const float DespawnKeepDistanceSq = 25f * 25f;

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
        public float AvgFrameTimeMs;
        public float SurfaceWorldY;
        public NativeArray<EncounterJobOutput> Output;

        public void Execute()
        {
            EncounterDirectorState state = CurrentState;
            EncounterJobOutput output = default;

            float3 playerPosition = PlayerPosition.xyz;
            float3 playerForward = math.normalizesafe(PlayerForward.xyz, new float3(0f, 0f, 1f));
            int activeEnemyCount = 0;
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

                bool visible = TestPlanesAABB(token.Position, new float3(FrustumRejectPadding));
                float visibilityFactor = visible ? 0f : 1f;
                float priority = distSq * visibilityFactor * math.rcp(math.max(1f, token.TokenCost));

                activeEnemyCount++;

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
            float rawStress = 0.35f * healthStress +
                              0.25f * oxygenStress +
                              0.25f * proximityStress +
                              0.10f * depthStress +
                              0.05f * velocityStress;
            float alpha = 1f - math.exp(-1f / StressTau);
            state.StressLevel += alpha * (math.saturate(rawStress) - state.StressLevel);
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
            float amplitudeScale = math.lerp(0.35f, 1f, math.max(state.StressLevel, PlayerInternalStress));
            state.IntensityLevel = math.clamp(phaseIntensity * amplitudeScale, 0f, 1f);
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

            if (((EncounterBudgetFlags)state.BudgetFlags & EncounterBudgetFlags.RegenBlocked) != 0)
            {
                state.TokenRegenRate = 0f;
            }
            else if ((EncounterPhase)state.ActivePhase == EncounterPhase.Relax)
            {
                state.TokenRegenRate = 8f;
            }
            else
            {
                state.TokenRegenRate = 0f;
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
                }

                if (bestEntity1 != 0 && shedCount > 1)
                {
                    output.DespawnRequestCount++;
                    output.DespawnEntityId1 = bestEntity1;
                    state.TokenBudget = math.clamp(state.TokenBudget + bestCost1 * 0.5f, 0f, MaxTokenBudget);
                    activeEnemyCount--;
                }

                if (bestEntity2 != 0 && shedCount > 2)
                {
                    output.DespawnRequestCount++;
                    output.DespawnEntityId2 = bestEntity2;
                    state.TokenBudget = math.clamp(state.TokenBudget + bestCost2 * 0.5f, 0f, MaxTokenBudget);
                    activeEnemyCount--;
                }
            }

            if ((EncounterPhase)state.ActivePhase != EncounterPhase.Relax &&
                ((EncounterBudgetFlags)state.BudgetFlags & (EncounterBudgetFlags.LoadSheddingActive | EncounterBudgetFlags.SpawnSuspended)) == 0 &&
                activeEnemyCount < 32)
            {
                EncounterThreatClass threatClass = ResolveDesiredThreatClass(state.IntensityLevel, state.TokenBudget);
                if (TryResolveSpawnCandidate(playerPosition, playerForward, out float3 spawnPosition))
                {
                    uint spawnSequence = state.SpawnSequence + 1u;
                    output.SpawnRequestCount = 1;
                    output.SpawnThreatClass = (int)threatClass;
                    output.SpawnPosition = spawnPosition;
                    output.SpawnVariantSeed = EncounterDirector.BuildDeterministicSeed(
                        new Vector3(playerPosition.x, playerPosition.y, playerPosition.z),
                        unchecked((int)spawnSequence),
                        state.ActivePhase,
                        activeEnemyCount);
                    state.SpawnSequence = spawnSequence;
                    state.TokenBudget = math.clamp(state.TokenBudget - ResolveTokenCost(threatClass), 0f, MaxTokenBudget);
                    activeEnemyCount++;
                }
            }

            state.ActiveEnemyCount = math.max(0, activeEnemyCount);
            state.PlayerPosition = PlayerPosition;
            state.PlayerVelocity = PlayerVelocity;
            WriteState[0] = state;
            Output[0] = output;
        }

        private bool TryResolveSpawnCandidate(float3 playerPosition, float3 playerForward, out float3 spawnPosition)
        {
            spawnPosition = float3.zero;
            float bestScore = float.MinValue;
            bool found = false;

            int directionCount = math.min(CandidateCount, CandidateDirections.Length);
            for (int i = 0; i < directionCount; i++)
            {
                float normalizedIndex = directionCount > 1 ? (float)i / (directionCount - 1) : 0f;
                float radius = math.lerp(MinSpawnRadius, MaxSpawnRadius, normalizedIndex);
                float3 candidate = playerPosition + CandidateDirections[i] * radius;

                if (candidate.y > SurfaceWorldY - 2f)
                    continue;

                if (TestPlanesAABB(candidate, new float3(FrustumRejectPadding)))
                    continue;

                if (!HasEnemyClearance(candidate))
                    continue;

                float3 toCandidate = math.normalizesafe(candidate - playerPosition, playerForward);
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

        private bool TestPlanesAABB(float3 center, float3 extents)
        {
            for (int i = 0; i < FrustumPlanes.Length; i++)
            {
                float4 plane = FrustumPlanes[i];
                float projectedRadius = math.dot(math.abs(plane.xyz), extents);
                float distance = math.dot(plane.xyz, center) + plane.w;
                if (distance + projectedRadius < 0f)
                    return false;
            }

            return true;
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

        private static EncounterThreatClass ResolveDesiredThreatClass(float intensityLevel, float tokenBudget)
        {
            EncounterThreatClass desired;
            if (intensityLevel > 0.85f)
                desired = EncounterThreatClass.Leviathan;
            else if (intensityLevel > 0.55f)
                desired = EncounterThreatClass.Stalker;
            else if (intensityLevel > 0.25f)
                desired = EncounterThreatClass.Swarm;
            else
                desired = EncounterThreatClass.Drone;

            if (tokenBudget >= ResolveTokenCost(desired))
                return desired;

            if (tokenBudget >= ResolveTokenCost(EncounterThreatClass.Stalker))
                return EncounterThreatClass.Stalker;
            if (tokenBudget >= ResolveTokenCost(EncounterThreatClass.Swarm))
                return EncounterThreatClass.Swarm;
            if (tokenBudget >= ResolveTokenCost(EncounterThreatClass.Drone))
                return EncounterThreatClass.Drone;

            return EncounterThreatClass.Drone;
        }

        private static float ResolveTokenCost(EncounterThreatClass threatClass)
        {
            switch (threatClass)
            {
                case EncounterThreatClass.Leviathan:
                    return 80f;
                case EncounterThreatClass.Stalker:
                    return 35f;
                case EncounterThreatClass.Swarm:
                    return 20f;
                default:
                    return 10f;
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
