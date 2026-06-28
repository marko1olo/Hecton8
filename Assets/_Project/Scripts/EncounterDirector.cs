using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Data;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Environment;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        RegenBlocked = 1 << 3,
        DespairModeActive = 1 << 4
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    internal struct EncounterDirectorState
    {
        [FieldOffset(0)]
        public float StressLevel;
        [FieldOffset(4)]
        public float IntensityLevel;
        [FieldOffset(8)]
        public float PacingPhaseTimer;
        [FieldOffset(12)]
        public float TokenBudget;
        [FieldOffset(16)]
        public float TokenRegenRate;
        [FieldOffset(20)]
        public int ActivePhase;
        [FieldOffset(24)]
        public int ActiveEnemyCount;
        [FieldOffset(28)]
        public int BudgetFlags;
        [FieldOffset(32)]
        public float RecoveryTimer;
        [FieldOffset(36)]
        public float4 PlayerPosition;
        [FieldOffset(52)]
        public float4 PlayerVelocity;
        [FieldOffset(68)]
        public uint SpawnSequence;
        [FieldOffset(72)]
        public uint SurvivalCriticalFlags;
        [FieldOffset(76)]
        public uint SurvivalCriticalSeverityPermille;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    internal struct EncounterEnemyToken
    {
        [FieldOffset(0)]
        public int EntityId;
        [FieldOffset(4)]
        public float TokenCost;
        [FieldOffset(8)]
        public float DistSqToPlayer;
        [FieldOffset(12)]
        public int VisibilityFlags;
        [FieldOffset(16)]
        public float DepthPosition;
        [FieldOffset(20)]
        public int ThreatClass;
        [FieldOffset(24)]
        public float DespawnPriority;
        [FieldOffset(28)]
        public float3 Position;
        [FieldOffset(40)]
        public uint Padding0;
        [FieldOffset(44)]
        public uint Padding1;
    }

    [Flags]
    internal enum HeadlessEntityFlags : byte
    {
        None = 0,
        Active = 1 << 0,
        Predator = 1 << 1,
        Apex = 1 << 2
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    internal struct HeadlessEntity
    {
        [FieldOffset(0)]
        public int EntityId;
        [FieldOffset(4)]
        public int ThreatClass;
        [FieldOffset(8)]
        public float TokenCost;
        [FieldOffset(12)]
        public float3 Position;
        [FieldOffset(24)]
        public AbsoluteUniversePositionBlit PositionAup;
        [FieldOffset(72)]
        public uint SpawnSeed;
        [FieldOffset(76)]
        public byte BiomeByte;
        [FieldOffset(77)]
        public byte Flags;
        [FieldOffset(78)]
        public ushort AgeColdTicks;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
    internal struct EncounterDirectorBlackBoxEntry
    {
        [System.Runtime.InteropServices.FieldOffset(0)]
        public uint FrameIndex;
        [System.Runtime.InteropServices.FieldOffset(4)]
        public uint DirectorStateHash;
        [System.Runtime.InteropServices.FieldOffset(8)]
        public uint ActiveThreatCount;
        [System.Runtime.InteropServices.FieldOffset(12)]
        public uint Flags;
        [System.Runtime.InteropServices.FieldOffset(16)]
        public float Stress01;
        [System.Runtime.InteropServices.FieldOffset(20)]
        public float Intensity01;
        [System.Runtime.InteropServices.FieldOffset(24)]
        public float SpawnCredits;
        [System.Runtime.InteropServices.FieldOffset(28)]
        public float PlayerSpeed;
        [System.Runtime.InteropServices.FieldOffset(32)]
        public float3 PlayerPosition;
        [System.Runtime.InteropServices.FieldOffset(44)]
        public uint SurvivalCriticalFlags;
        [System.Runtime.InteropServices.FieldOffset(48)]
        public uint SurvivalCriticalSeverityPermille;
        [System.Runtime.InteropServices.FieldOffset(52)]
        public uint ActivePhase;
        [System.Runtime.InteropServices.FieldOffset(56)]
        private byte _pad0;
        [System.Runtime.InteropServices.FieldOffset(57)]
        private byte _pad1;
        [System.Runtime.InteropServices.FieldOffset(58)]
        private byte _pad2;
        [System.Runtime.InteropServices.FieldOffset(59)]
        private byte _pad3;
        [System.Runtime.InteropServices.FieldOffset(60)]
        private byte _pad4;
        [System.Runtime.InteropServices.FieldOffset(61)]
        private byte _pad5;
        [System.Runtime.InteropServices.FieldOffset(62)]
        private byte _pad6;
        [System.Runtime.InteropServices.FieldOffset(63)]
        private byte _pad7;
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

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct EncounterSpawnRequest
    {
        [FieldOffset(0)]
        public int ThreatClass;
        [FieldOffset(4)]
        public float3 Position;
        [FieldOffset(16)]
        public uint VariantSeed;
        [FieldOffset(20)]
        public uint SquadStateBits;
        [FieldOffset(24)]
        private ulong _pad0;
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
        public float ComputeSpawningOddsForAmbientDynamicEncounters(float baseWeight, float playerStress01, float cooldownRemaining)
        {
            return Hecton8.PureLogic.Ecosystem.AmbientEncounterSpawningWeightCalculator.Compute(baseWeight, playerStress01, cooldownRemaining);
        }

        internal const int FrustumPlaneCount = 6;
        private const int DebugEventRingCapacity = 256;
        private const int DirectorBlackBoxCapacity = 300;
        private const int DebugEventCodePhaseChange = 0x04;
        private const int HeadlessEntityCapacity = 1024;
        private const int HeadlessSpawnRequestCapacity = 16;
        private const int HeadlessDespawnRequestCapacity = 16;
        private const int BiomeHeatmapResolution = 256;
        private const int PredatorAupBufferCapacity = 16;
        private const float PlayerPredatorAupRadiusMeters = 70f;
        private const int HeadlessEntityIdBase = 0x68000000;
        private const int HeadlessEntityIdLimit = HeadlessEntityIdBase + 0x01000000;
        private const float PredictiveSpawnLeadMeters = 200f;
        private const float StationaryVelocitySq = 0.25f;
        private const float HeadlessDespawnDistanceSq = 400f * 400f;
        private const ulong DirectorTelemetryDumpMagic = 0x0038444148454850UL;
        private const string DirectorTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_AI_ENCOUNTER_DIRECTOR.bin";

        private const int MaxActiveEnemies = 32;
        private const int BaseCandidateCount = 16;
        private const int HighCandidateCount = 32;
        private const float ColdTickIntervalSeconds = 1f;
        private const float MaxTokenBudget = 1000f;
        private const float MinSpawnRadius = 50f;
        private const float MaxSpawnRadius = 150f;
        private const float SpawnClusterRadiusSq = 15f * 15f;
        private const float CriticalHealthSpawnSuppressionThreshold = 0.15f;
        private const float CriticalOxygenSpawnSuppressionThreshold = 0.15f;
        private const uint SurvivalCriticalFlagHealth = 1u << 0;
        private const uint SurvivalCriticalFlagOxygen = 1u << 1;
        private const float DespawnKeepDistanceSq = 25f * 25f;
        private const float FrustumRejectPadding = 3f;
        private const float SafeIdleStressDecayPerTick = 0.06f;
        private const float InvHash24Max = 1f / 16777215f;
        private const Allocator DataVaultExemptSceneScratchAllocator = Allocator.Persistent;
        private const SystemID NativeArrayOwnerSystem = SystemID.GameplayCombat;

        private NativeArray<EncounterDirectorState> _frontState;
        private NativeArray<EncounterDirectorState> _backState;
        private NativeArray<EncounterEnemyToken> _enemyTokens;
        private NativeArray<float4> _frustumPlanes;
        private NativeArray<float3> _candidateDirections;
        private NativeArray<EncounterJobOutput> _jobOutput;
        private NativeArray<EncounterSpawnRequest> _spawnRequests;
        private NativeArray<int> _despawnRequests;
        private NativeArray<EncounterDebugEvent> _debugEventRing;
        private NativeArray<int> _debugEventHead;
        private NativeList<HeadlessEntity> _headlessEntities;
        private int _headlessEntitiesSentinelId;
        private NativeArray<float4> _predatorAupUpload;
        private NativeArray<EncounterDirectorBlackBoxEntry> _blackBox;
        private NativeArray<int> _blackBoxHead;
        private GraphicsBuffer _predatorAupBufferA;
        private GraphicsBuffer _predatorAupBufferB;
        private GraphicsBuffer _predatorAupPublishedBuffer;
        private static readonly int _PredatorAUPBufferId = Shader.PropertyToID("_PredatorAUPBuffer");
        private static readonly int _PredatorAUPCountId = Shader.PropertyToID("_PredatorAUPCount");
        private static readonly int _PredatorAUPParamsId = Shader.PropertyToID("_PredatorAUPParams");
        // COLD ALLOC: Transform[32] — tracked live encounter proxies for token refresh — owner: EncounterDirector
        private readonly Transform[] _trackedTransforms;
        // COLD ALLOC: int[32] — tracked live encounter entity ids — owner: EncounterDirector
        private readonly int[] _trackedEntityIds;
        // COLD ALLOC: EncounterThreatClass[32] — tracked encounter threat classes — owner: EncounterDirector
        private readonly EncounterThreatClass[] _trackedThreatClasses;
        // COLD ALLOC: float[32] — tracked encounter token costs — owner: EncounterDirector
        private readonly float[] _trackedTokenCosts;
        // COLD ALLOC: int[16] - published predator AUP source ids for in-place live threat refresh - owner: EncounterDirector
        private readonly int[] _predatorAupSourceIds;
        private EncounterThreatAuthoringSnapshot _threatAuthoring;

        private JobHandle _activeJobHandle;
        private bool _jobScheduled;
        private bool _blackBoxDumpedThisActivation;
        private bool _predatorAupGlobalsDirty = true;
        private float _coldTickAccumulator;
        private int _frameIndex;
        private uint _blackBoxFrameSequence;
        private int _nextHeadlessEntitySequence;
        private int _headlessFreeSearchCursor;
        private int _lastPublishedPredatorAupCount = -1;
        private bool _predatorAupWriteToA = true;
        private bool _predatorAupFullUploadPending;
        private bool _predatorAupPlayerUploadPending;
        private bool _predatorAupClearPending;
        private float3 _pendingPredatorAupPlayerPosition;
        private readonly float _candidateHardwareWeight01;
        private IMetaCampaignService _metaCampaignService;
        private int _pendingPhaseOverride = -1;
        private bool _pendingReset;
        private int _pendingForcedThreatClass = -1;
        private int _pendingForcedThreatCount;

        internal EncounterDirector()
        {
            _frontState = H8Memory.Allocate<EncounterDirectorState>(1, NativeArrayOwnerSystem, DataVaultExemptSceneScratchAllocator, NativeArrayOptions.ClearMemory);
            _backState = H8Memory.Allocate<EncounterDirectorState>(1, NativeArrayOwnerSystem, DataVaultExemptSceneScratchAllocator, NativeArrayOptions.ClearMemory);
            _enemyTokens = H8Memory.Allocate<EncounterEnemyToken>(MaxActiveEnemies, NativeArrayOwnerSystem, DataVaultExemptSceneScratchAllocator, NativeArrayOptions.ClearMemory);
            _frustumPlanes = H8Memory.Allocate<float4>(FrustumPlaneCount, NativeArrayOwnerSystem, DataVaultExemptSceneScratchAllocator, NativeArrayOptions.ClearMemory);
            _candidateDirections = H8Memory.Allocate<float3>(HighCandidateCount, NativeArrayOwnerSystem, DataVaultExemptSceneScratchAllocator, NativeArrayOptions.ClearMemory);
            _jobOutput = H8Memory.Allocate<EncounterJobOutput>(1, NativeArrayOwnerSystem, DataVaultExemptSceneScratchAllocator, NativeArrayOptions.ClearMemory);
            _spawnRequests = H8Memory.Allocate<EncounterSpawnRequest>(HeadlessSpawnRequestCapacity, NativeArrayOwnerSystem, DataVaultExemptSceneScratchAllocator, NativeArrayOptions.ClearMemory);
            _despawnRequests = H8Memory.Allocate<int>(HeadlessDespawnRequestCapacity, NativeArrayOwnerSystem, DataVaultExemptSceneScratchAllocator, NativeArrayOptions.ClearMemory);
            _debugEventRing = H8Memory.Allocate<EncounterDebugEvent>(DebugEventRingCapacity, NativeArrayOwnerSystem, DataVaultExemptSceneScratchAllocator, NativeArrayOptions.ClearMemory);
            _debugEventHead = H8Memory.Allocate<int>(1, NativeArrayOwnerSystem, DataVaultExemptSceneScratchAllocator, NativeArrayOptions.ClearMemory);
            _predatorAupUpload = H8Memory.Allocate<float4>(PredatorAupBufferCapacity, NativeArrayOwnerSystem, DataVaultExemptSceneScratchAllocator, NativeArrayOptions.ClearMemory);
            _blackBox = H8Memory.Allocate<EncounterDirectorBlackBoxEntry>(DirectorBlackBoxCapacity, NativeArrayOwnerSystem, DataVaultExemptSceneScratchAllocator, NativeArrayOptions.ClearMemory);
            _blackBoxHead = H8Memory.Allocate<int>(1, NativeArrayOwnerSystem, DataVaultExemptSceneScratchAllocator, NativeArrayOptions.ClearMemory);
            if (!AllNativeArraysCreated())
            {
                Dispose();
                throw new InvalidOperationException("EncounterDirector native allocation failed.");
            }

            try
            {
                _headlessEntities = new NativeList<HeadlessEntity>(HeadlessEntityCapacity, DataVaultExemptSceneScratchAllocator);
                // COLD ALLOC: HeadlessEntity[1024] - data-only encounter threat slots, no GameObject hydration - owner: EncounterDirector
                for (int i = 0; i < HeadlessEntityCapacity; i++)
                    _headlessEntities.Add(default);
            }
            catch
            {
                if (_headlessEntities.IsCreated)
                {
                    _headlessEntities.Dispose();
                    _headlessEntities = default;
                }

                Dispose();
                throw;
            }

            try
            {
                RegisterNativeMemorySentinel();
            }
            catch
            {
                Dispose();
                throw;
            }

            _trackedTransforms = new Transform[MaxActiveEnemies];
            _trackedEntityIds = new int[MaxActiveEnemies];
            _trackedThreatClasses = new EncounterThreatClass[MaxActiveEnemies];
            _trackedTokenCosts = new float[MaxActiveEnemies];
            _predatorAupSourceIds = new int[PredatorAupBufferCapacity];
            _candidateHardwareWeight01 = ResolveCandidateHardwareWeight01();
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
        internal bool CanProcessEntityDeathSignals => !_jobScheduled;

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
            ClearSpawnRequests();
            ClearDespawnRequests();
            _coldTickAccumulator = 0f;
            _frameIndex = 0;
            _blackBoxFrameSequence = 0u;
            _pendingPhaseOverride = -1;
            _pendingReset = false;
            _pendingForcedThreatClass = -1;
            _pendingForcedThreatCount = 0;
            if (_debugEventHead.IsCreated && _debugEventHead.Length > 0)
                _debugEventHead[0] = 0;
            if (_blackBoxHead.IsCreated && _blackBoxHead.Length > 0)
                _blackBoxHead[0] = 0;

            for (int i = 0; i < MaxActiveEnemies; i++)
                ClearTrackedSlot(i);

            ClearHeadlessEntities();
            _blackBoxDumpedThisActivation = false;
            _predatorAupGlobalsDirty = true;
            _lastPublishedPredatorAupCount = -1;
            _predatorAupPublishedBuffer = null;
            _predatorAupWriteToA = true;
            _nextHeadlessEntitySequence = 0;
            _headlessFreeSearchCursor = 0;
            QueuePredatorAupFullUpload();
        }

        internal void SetMetaCampaignService(IMetaCampaignService service)
        {
            _metaCampaignService = service;
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
            if (_jobScheduled)
            {
                RecordBlackBox(BuildTelemetryState(in frameContext));
                QueuePlayerPredatorAupSlot(frameContext.PlayerPosition);
                _coldTickAccumulator += frameContext.DeltaTime;
                return;
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

            RecordBlackBox(BuildTelemetryState(in frameContext));
            QueuePlayerPredatorAupSlot(frameContext.PlayerPosition);

            _coldTickAccumulator += frameContext.DeltaTime;
            if (_jobScheduled || _coldTickAccumulator < ColdTickIntervalSeconds)
                return;

            _coldTickAccumulator -= ColdTickIntervalSeconds;
            RefreshTrackedEnemies(frameContext.PlayerPosition);
            ScheduleColdTick(frameContext);
        }

        internal void CompleteReadyOutput(FaunaDirector faunaDirector, HectonDirectorAI bridge, bool forceComplete)
        {
            if (!_jobScheduled)
                return;

            if (!forceComplete && !_activeJobHandle.IsCompleted)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _activeJobHandle, forceComplete))
                return;

            _jobScheduled = false;
            _frontState[0] = _backState[0];
            ApplyCompletedOutput(faunaDirector, bridge);
        }

        internal void ForceStopAndReset()
        {
            ForceCompleteActiveJobForTeardown();
            Reset();
        }

        internal static float HashToUnit01(uint hash)
        {
            return (hash & 0x00FFFFFFu) * InvHash24Max;
        }

        internal static uint BuildDeterministicSeed(Vector3 position, int sequenceSalt, int phase, int activeEnemyCount)
        {
            return BuildDeterministicSeed(new float3(position.x, position.y, position.z), sequenceSalt, phase, activeEnemyCount);
        }

        internal static uint BuildDeterministicSeed(float3 position, int sequenceSalt, int phase, int activeEnemyCount)
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
            return math.saturate(numerator * math.rcp(math.max(denominator, 0.0001f)));
        }

        public void Dispose()
        {
            JobHandle disposeHandle = default;
            bool hasDependency = _jobScheduled;
            bool unregisterHeadlessEntities = _headlessEntitiesSentinelId > 0;
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
            DisposeNativeArray(ref _spawnRequests, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _despawnRequests, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _debugEventRing, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _debugEventHead, ref disposeHandle, ref hasDependency);
            DisposeNativeList(ref _headlessEntities, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _predatorAupUpload, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _blackBox, ref disposeHandle, ref hasDependency);
            DisposeNativeArray(ref _blackBoxHead, ref disposeHandle, ref hasDependency);
            if (hasDependency &&
                !DispatcherJobFence.TryComplete(ref disposeHandle, forceComplete: true))
            {
                throw new InvalidOperationException("EncounterDirector native disposal did not complete before sentinel unregister.");
            }

            Exception firstException = null;
            try
            {
                if (unregisterHeadlessEntities)
                {
                    NativeMemorySentinel.Unregister(_headlessEntitiesSentinelId);
                    _headlessEntitiesSentinelId = 0;
                }
            }
            catch (Exception exception)
            {
                firstException = exception;
            }

            try
            {
                ReleasePredatorAupBuffer();
            }
            catch (Exception exception)
            {
                if (firstException == null)
                    firstException = exception;
            }

            if (firstException != null)
                throw firstException;
        }

        private void RegisterNativeMemorySentinel()
        {
            _headlessEntitiesSentinelId = NativeMemorySentinel.RegisterNativeListInstance(
                _headlessEntities,
                nameof(EncounterDirector),
                nameof(_headlessEntities),
                NativeAllocationLifetime.Scene);
            if (_headlessEntitiesSentinelId <= 0)
            {
                _headlessEntitiesSentinelId = 0;
                throw new InvalidOperationException("Native memory sentinel registration failed for encounter headless entities.");
            }
        }

        private bool AllNativeArraysCreated()
        {
            return _frontState.IsCreated &&
                   _backState.IsCreated &&
                   _enemyTokens.IsCreated &&
                   _frustumPlanes.IsCreated &&
                   _candidateDirections.IsCreated &&
                   _jobOutput.IsCreated &&
                   _spawnRequests.IsCreated &&
                   _despawnRequests.IsCreated &&
                   _debugEventRing.IsCreated &&
                   _debugEventHead.IsCreated &&
                   _predatorAupUpload.IsCreated &&
                   _blackBox.IsCreated &&
                   _blackBoxHead.IsCreated;
        }

        internal void EnsureGpuResources()
        {
            if (_predatorAupBufferA != null && _predatorAupBufferB != null)
                return;

            if (_predatorAupBufferA == null)
                _predatorAupBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(PredatorAupBufferCapacity); // COLD ALLOC: GraphicsBuffer[16] - predator AUP upload buffer A for GPU/CPU double-buffering - owner: EncounterDirector
            if (_predatorAupBufferB == null)
                _predatorAupBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(PredatorAupBufferCapacity); // COLD ALLOC: GraphicsBuffer[16] - predator AUP upload buffer B for GPU/CPU double-buffering - owner: EncounterDirector
            _predatorAupGlobalsDirty = true;
        }

        private void EnsurePredatorAupBuffers()
        {
            EnsureGpuResources();
        }

        internal bool TryGetPredatorAupGpuBuffer(out GraphicsBuffer buffer, out int count)
        {
            buffer = _predatorAupPublishedBuffer;
            count = math.clamp(_lastPublishedPredatorAupCount, 0, PredatorAupBufferCapacity);
            return buffer != null && buffer.IsValid() && count > 0;
        }

        internal void ForceCompleteActiveJobForTeardown()
        {
            if (!_jobScheduled)
                return;

            DispatcherJobSwap.TryComplete(ref _activeJobHandle, true);
            _jobScheduled = false;
            if (_frontState.IsCreated && _backState.IsCreated)
                _frontState[0] = _backState[0];
        }

        internal void ClearPredatorAupPublication()
        {
            _lastPublishedPredatorAupCount = 0;
            _predatorAupPublishedBuffer = null;
            _predatorAupGlobalsDirty = true;
            _predatorAupFullUploadPending = false;
            _predatorAupPlayerUploadPending = false;
            _predatorAupClearPending = true;
            ClearPredatorAupSourceIds();
        }

        internal void FlushPredatorAupVisualSync()
        {
            if (_predatorAupClearPending)
            {
                Shader.SetGlobalInt(_PredatorAUPCountId, 0);
                _predatorAupClearPending = false;
                if (!_predatorAupFullUploadPending && !_predatorAupPlayerUploadPending)
                    return;
            }

            if (_predatorAupFullUploadPending)
            {
                FlushPredatorAupFullUpload();
                _predatorAupFullUploadPending = false;
                _predatorAupPlayerUploadPending = false;
                return;
            }

            if (_predatorAupPlayerUploadPending)
            {
                FlushPlayerPredatorAupSlot(_pendingPredatorAupPlayerPosition);
                _predatorAupPlayerUploadPending = false;
            }
        }

        internal void HandleEntityDeathSignal(in EntityDeathSignal signal)
        {
            int entityId = unchecked((int)signal.EntityHash);
            if (entityId == 0)
                return;

            if (TryReleaseHeadlessEntity(entityId, refundHalfCost: true, decrementActiveCount: true, out bool releasedPredator))
            {
                if (releasedPredator)
                    QueuePredatorAupFullUpload();
                return;
            }

            int trackedSlot = FindTrackedSlot(entityId);
            if (trackedSlot < 0)
                return;

            EncounterDirectorState state = _frontState[0];
            state.TokenBudget = math.clamp(state.TokenBudget + _trackedTokenCosts[trackedSlot] * 0.5f, 0f, MaxTokenBudget);
            state.ActiveEnemyCount = math.max(0, state.ActiveEnemyCount - 1);
            _frontState[0] = state;
            _backState[0] = state;
            bool trackedPredator = WritesPredatorAup(_trackedThreatClasses[trackedSlot]);
            ClearTrackedSlot(trackedSlot);
            if (trackedPredator)
                QueuePredatorAupFullUpload();
        }

        private void RefreshTrackedEnemies(float3 playerPosition)
        {
            for (int i = 0; i < MaxActiveEnemies; i++)
                _enemyTokens[i] = default;

            int tokenSlot = 0;
            RefreshHeadlessEnemyTokens(playerPosition, ref tokenSlot);

            for (int i = 0; i < MaxActiveEnemies && tokenSlot < MaxActiveEnemies; i++)
            {
                int entityId = _trackedEntityIds[i];
                if (entityId == 0)
                    continue;

                Transform trackedTransform = _trackedTransforms[i];
                if (trackedTransform == null || !trackedTransform.gameObject.activeInHierarchy)
                {
                    bool releasedPredator = WritesPredatorAup(_trackedThreatClasses[i]);
                    ClearTrackedSlot(i);
                    if (releasedPredator)
                        QueuePredatorAupFullUpload();
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
                _enemyTokens[tokenSlot] = token;
                tokenSlot++;
            }
        }

        private void RefreshHeadlessEnemyTokens(float3 playerPosition, ref int tokenSlot)
        {
            if (!_headlessEntities.IsCreated)
                return;

            int length = _headlessEntities.Length;
            for (int i = 0; i < length && tokenSlot < MaxActiveEnemies; i++)
            {
                HeadlessEntity entity = _headlessEntities[i];
                if ((entity.Flags & (byte)HeadlessEntityFlags.Active) == 0)
                    continue;

                entity.AgeColdTicks++;
                _headlessEntities[i] = entity;

                EncounterThreatClass threatClass = (EncounterThreatClass)entity.ThreatClass;
                float3 position = entity.Position;
                _enemyTokens[tokenSlot] = new EncounterEnemyToken
                {
                    EntityId = entity.EntityId,
                    TokenCost = entity.TokenCost,
                    DistSqToPlayer = math.lengthsq(position - playerPosition),
                    VisibilityFlags = 0,
                    DepthPosition = position.y,
                    ThreatClass = entity.ThreatClass,
                    DespawnPriority = ResolveDespawnPriorityBias(threatClass, _threatAuthoring),
                    Position = position
                };
                tokenSlot++;
            }
        }

        private void ScheduleColdTick(EncounterFrameContext frameContext)
        {
            EncounterDirectorState currentState = _frontState[0];
            currentState.PlayerPosition = new float4(frameContext.PlayerPosition, frameContext.PlayerDepth);
            currentState.PlayerVelocity = new float4(frameContext.PlayerVelocity, EstimateLength(frameContext.PlayerVelocity));
            EncounterThreatAuthoringSnapshot threatAuthoring = _threatAuthoring;
            IEcosystemDirectorService ecosystemDirector = GlobalRegistry.EcosystemDirector;
            if (ecosystemDirector != null &&
                ecosystemDirector.TryGetBiomassAvailability(
                    new Vector3(frameContext.PlayerPosition.x, frameContext.PlayerPosition.y, frameContext.PlayerPosition.z),
                    out float preyBiomass01,
                    out float predatorBiomass01,
                    out _))
            {
                ApplyBiomassThreatCostModifiers(ref threatAuthoring, preyBiomass01, predatorBiomass01);
            }

            IMetaCampaignService metaCampaign = _metaCampaignService;
            if (metaCampaign == null || !metaCampaign.IsLeviathanAwakened)
            {
                threatAuthoring.LeviathanMaxSimultaneous = 0;
                threatAuthoring.LeviathanAllowCriticalHealth = 0;
            }

            EncounterDirectorJob job = new EncounterDirectorJob
            {
                CurrentState = currentState,
                WriteState = _backState,
                ActiveEnemies = _enemyTokens,
                FrustumPlanes = _frustumPlanes,
                CandidateDirections = _candidateDirections,
                CandidateCount = ResolveCandidateCount(_candidateHardwareWeight01),
                PlayerPosition = currentState.PlayerPosition,
                PlayerVelocity = currentState.PlayerVelocity,
                PlayerForward = new float4(NormalizeSafe(frameContext.PlayerForward, new float3(0f, 0f, 1f)), 0f),
                PlayerHealthNormalized = SanitizeNormalized01(frameContext.PlayerHealthNormalized),
                PlayerOxygenNormalized = SanitizeNormalized01(frameContext.PlayerOxygenNormalized),
                PlayerInternalStress = SanitizeNormalized01(frameContext.PlayerInternalStress),
                AcousticThreatLevel = SanitizeNormalized01(frameContext.AcousticThreatLevel),
                AvgFrameTimeMs = SanitizeNonNegativeFinite(frameContext.AvgFrameTimeMs),
                SurfaceWorldY = frameContext.SurfaceWorldY,
                ForcedThreatClass = _pendingForcedThreatClass,
                ForcedThreatCount = _pendingForcedThreatCount,
                ThreatAuthoring = threatAuthoring,
                SpawnRequests = _spawnRequests,
                DespawnRequests = _despawnRequests,
                Output = _jobOutput
            };

            _frameIndex++;
            _jobOutput[0] = default;
            ClearSpawnRequests();
            ClearDespawnRequests();
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

            bool predatorAupDirty = false;
            if (output.DespawnRequestCount > 0)
                predatorAupDirty |= ApplyDespawnRequests(output, faunaDirector);

            int forcedSpawnConsumed = 0;
            if (output.SpawnRequestCount > 0)
                forcedSpawnConsumed = ApplySpawnRequests(output, bridge, ref predatorAupDirty);

            if (forcedSpawnConsumed > 0 && _pendingForcedThreatCount > 0)
            {
                _pendingForcedThreatCount = math.max(0, _pendingForcedThreatCount - forcedSpawnConsumed);
                if (_pendingForcedThreatCount <= 0)
                    _pendingForcedThreatClass = -1;
            }

            if (predatorAupDirty)
                QueuePredatorAupFullUpload();
        }

        private int ApplySpawnRequests(EncounterJobOutput output, HectonDirectorAI bridge, ref bool predatorAupDirty)
        {
            int requestCount = math.min(
                output.SpawnRequestCount,
                _spawnRequests.IsCreated ? _spawnRequests.Length : EncounterDirectorJob.LegacySpawnSlotCount);
            int forcedConsumed = 0;
            for (int i = 0; i < requestCount; i++)
            {
                if (!TryGetSpawnRequest(output, i, out EncounterSpawnRequest request))
                {
                    RollbackUnappliedSpawn();
                    continue;
                }

                EncounterThreatClass threatClass = (EncounterThreatClass)request.ThreatClass;
                Vector3 spawnPosition = new Vector3(request.Position.x, request.Position.y, request.Position.z);
                if (!TryAllocateHeadlessEntity(threatClass, spawnPosition, request.VariantSeed, request.SquadStateBits, i))
                {
                    RollbackFailedSpawn(threatClass, refundTokenCost: output.ForcedSpawnConsumed == 0);
                    continue;
                }

                bridge.HandleThreatSpawned(threatClass, spawnPosition);
                predatorAupDirty |= WritesPredatorAup(threatClass);
                if (output.ForcedSpawnConsumed != 0)
                    forcedConsumed++;
            }

            return forcedConsumed;
        }

        private bool TryGetSpawnRequest(EncounterJobOutput output, int index, out EncounterSpawnRequest request)
        {
            request = default;
            if (_spawnRequests.IsCreated)
            {
                if (index < 0 || index >= _spawnRequests.Length)
                    return false;

                request = _spawnRequests[index];
                return IsValidThreatClass(request.ThreatClass);
            }

            request = new EncounterSpawnRequest
            {
                ThreatClass = output.SpawnThreatClass,
                Position = GetSpawnRequestPosition(output, index),
                VariantSeed = GetSpawnRequestSeed(output, index),
                SquadStateBits = output.SpawnSquadStateBits
            };
            return IsValidThreatClass(request.ThreatClass);
        }

        private static float3 GetSpawnRequestPosition(EncounterJobOutput output, int index)
        {
            return index == 1
                ? output.SpawnPosition1
                : index == 2
                    ? output.SpawnPosition2
                    : output.SpawnPosition;
        }

        private static uint GetSpawnRequestSeed(EncounterJobOutput output, int index)
        {
            return index == 1
                ? output.SpawnVariantSeed1
                : index == 2
                    ? output.SpawnVariantSeed2
                    : output.SpawnVariantSeed;
        }

        private bool ApplyDespawnRequests(EncounterJobOutput output, FaunaDirector faunaDirector)
        {
            bool predatorAupDirty = false;
            if (_despawnRequests.IsCreated)
            {
                int nativeRequestCount = math.min(output.DespawnRequestCount, _despawnRequests.Length);
                for (int i = 0; i < nativeRequestCount; i++)
                    predatorAupDirty |= ApplyDespawnRequestEntity(_despawnRequests[i], faunaDirector);

                return predatorAupDirty;
            }

            int legacyRequestCount = math.min(output.DespawnRequestCount, EncounterDirectorJob.LegacyDespawnSlotCount);
            if (legacyRequestCount > 0)
                predatorAupDirty |= ApplyDespawnRequestEntity(output.DespawnEntityId0, faunaDirector);
            if (legacyRequestCount > 1)
                predatorAupDirty |= ApplyDespawnRequestEntity(output.DespawnEntityId1, faunaDirector);
            if (legacyRequestCount > 2)
                predatorAupDirty |= ApplyDespawnRequestEntity(output.DespawnEntityId2, faunaDirector);

            return predatorAupDirty;
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

        private bool ApplyDespawnRequestEntity(int entityId, FaunaDirector faunaDirector)
        {
            if (entityId == 0)
                return false;

            if (TryReleaseHeadlessEntity(entityId, refundHalfCost: false, decrementActiveCount: false, out bool releasedPredator))
                return releasedPredator;

            float refund = ResolveTrackedTokenCost(entityId) * 0.5f;
            if (faunaDirector != null && faunaDirector.TryRecallEncounterThreat(entityId))
            {
                return UntrackEntity(entityId);
            }

            EncounterDirectorState state = _frontState[0];
            state.TokenBudget = math.clamp(state.TokenBudget - refund, 0f, MaxTokenBudget);
            state.ActiveEnemyCount = math.min(MaxActiveEnemies, state.ActiveEnemyCount + 1);
            _frontState[0] = state;
            _backState[0] = state;
            return false;
        }

        private EncounterDirectorState BuildTelemetryState(in EncounterFrameContext frameContext)
        {
            EncounterDirectorState state = _frontState[0];
            float health01 = SanitizeNormalized01(frameContext.PlayerHealthNormalized);
            float oxygen01 = SanitizeNormalized01(frameContext.PlayerOxygenNormalized);
            state.PlayerPosition = new float4(frameContext.PlayerPosition, frameContext.PlayerDepth);
            state.PlayerVelocity = new float4(frameContext.PlayerVelocity, EstimateLength(frameContext.PlayerVelocity));
            state.SurvivalCriticalFlags = ResolveSurvivalCriticalFlags(health01, oxygen01);
            state.SurvivalCriticalSeverityPermille = ResolveSurvivalCriticalSeverityPermille(health01, oxygen01);
            return state;
        }

        private static float SanitizeNormalized01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizeNonNegativeFinite(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static uint ResolveSurvivalCriticalFlags(float health01, float oxygen01)
        {
            uint flags = 0u;
            if (health01 <= CriticalHealthSpawnSuppressionThreshold)
                flags |= SurvivalCriticalFlagHealth;
            if (oxygen01 <= CriticalOxygenSpawnSuppressionThreshold)
                flags |= SurvivalCriticalFlagOxygen;
            return flags;
        }

        private static uint ResolveSurvivalCriticalSeverityPermille(float health01, float oxygen01)
        {
            float healthSeverity = CriticalHealthSpawnSuppressionThreshold > 0f
                ? math.saturate((CriticalHealthSpawnSuppressionThreshold - health01) / CriticalHealthSpawnSuppressionThreshold)
                : 0f;
            float oxygenSeverity = CriticalOxygenSpawnSuppressionThreshold > 0f
                ? math.saturate((CriticalOxygenSpawnSuppressionThreshold - oxygen01) / CriticalOxygenSpawnSuppressionThreshold)
                : 0f;
            return unchecked((uint)math.round(math.max(healthSeverity, oxygenSeverity) * 1000f));
        }

        private void RecordBlackBox(in EncounterDirectorState state)
        {
            if (!_blackBox.IsCreated || !_blackBoxHead.IsCreated || _blackBoxHead.Length <= 0)
                return;

            uint stateHash = ComputeDirectorStateHash(in state);
            int head = _blackBoxHead[0];
            int slot = head % DirectorBlackBoxCapacity;
            uint frameSequence = _blackBoxFrameSequence++;
            _blackBox[slot] = new EncounterDirectorBlackBoxEntry
            {
                FrameIndex = frameSequence,
                DirectorStateHash = stateHash,
                ActiveThreatCount = unchecked((uint)math.max(0, state.ActiveEnemyCount)),
                Flags = unchecked((uint)state.BudgetFlags),
                Stress01 = state.StressLevel,
                Intensity01 = state.IntensityLevel,
                SpawnCredits = state.TokenBudget,
                PlayerSpeed = state.PlayerVelocity.w,
                PlayerPosition = state.PlayerPosition.xyz,
                SurvivalCriticalFlags = state.SurvivalCriticalFlags,
                SurvivalCriticalSeverityPermille = state.SurvivalCriticalSeverityPermille,
                ActivePhase = unchecked((uint)math.max(0, state.ActivePhase))
            };
            _blackBoxHead[0] = head + 1;

            if (!math.isfinite(state.StressLevel) ||
                !math.isfinite(state.IntensityLevel) ||
                !math.isfinite(state.TokenBudget) ||
                !math.all(math.isfinite(state.PlayerPosition)) ||
                !math.all(math.isfinite(state.PlayerVelocity)))
            {
                DumpBlackBoxOnce();
            }
        }

        private static uint ComputeDirectorStateHash(in EncounterDirectorState state)
        {
            uint hash = 2166136261u;
            hash = (hash ^ math.asuint(state.StressLevel)) * 16777619u;
            hash = (hash ^ math.asuint(state.IntensityLevel)) * 16777619u;
            hash = (hash ^ math.asuint(state.TokenBudget)) * 16777619u;
            hash = (hash ^ unchecked((uint)state.ActiveEnemyCount)) * 16777619u;
            hash = (hash ^ unchecked((uint)state.BudgetFlags)) * 16777619u;
            hash = (hash ^ unchecked((uint)state.ActivePhase)) * 16777619u;
            hash = (hash ^ state.SurvivalCriticalFlags) * 16777619u;
            hash = (hash ^ state.SurvivalCriticalSeverityPermille) * 16777619u;
            hash = (hash ^ math.asuint(state.PlayerVelocity.w)) * 16777619u;
            hash = (hash ^ math.asuint(state.PlayerPosition.x)) * 16777619u;
            hash = (hash ^ math.asuint(state.PlayerPosition.y)) * 16777619u;
            hash = (hash ^ math.asuint(state.PlayerPosition.z)) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private unsafe void DumpBlackBoxOnce()
        {
            if (_blackBoxDumpedThisActivation || !_blackBox.IsCreated || !_blackBoxHead.IsCreated)
                return;

            NativeArray<byte> payload = default;
            try
            {
                const int headerBytes = 16;
                const int rowBytes = 56;
                int head = _blackBoxHead[0];
                int count = math.min(DirectorBlackBoxCapacity, math.max(0, head));
                int byteCount = headerBytes + count * rowBytes;
                const string dumpPayloadLabel = "EncounterDirectorBlackBoxDumpPayload";
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(EncounterDirector),
                    dumpPayloadLabel);

                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                WriteUInt64LittleEndian(target, 0, DirectorTelemetryDumpMagic);
                WriteUInt32LittleEndian(target, 8, unchecked((uint)DirectorBlackBoxCapacity));
                WriteUInt32LittleEndian(target, 12, rowBytes);

                int cursor = headerBytes;
                for (int i = 0; i < count; i++)
                {
                    int index = (head - count + i) % DirectorBlackBoxCapacity;
                    if (index < 0)
                        index += DirectorBlackBoxCapacity;

                    EncounterDirectorBlackBoxEntry entry = _blackBox[index];
                    WriteEncounterBlackBoxRow(target + cursor, in entry);
                    cursor += rowBytes;
                }

                _blackBoxDumpedThisActivation = NativeFaultDumpWriter.TryWriteAll(DirectorTelemetryDumpRelativePath, payload, cursor);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            catch (Exception exception)
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
#else
            catch (Exception)
            {
            }
#endif
            finally
            {
                const string dumpPayloadLabel = "EncounterDirectorBlackBoxDumpPayload";
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(EncounterDirector),
                    dumpPayloadLabel);
            }
        }

        private static unsafe void WriteEncounterBlackBoxRow(byte* target, in EncounterDirectorBlackBoxEntry entry)
        {
            WriteUInt32LittleEndian(target, 0, entry.FrameIndex);
            WriteUInt32LittleEndian(target, 4, entry.DirectorStateHash);
            WriteUInt32LittleEndian(target, 8, entry.ActiveThreatCount);
            WriteUInt32LittleEndian(target, 12, entry.Flags);
            WriteFloatLittleEndian(target, 16, entry.Stress01);
            WriteFloatLittleEndian(target, 20, entry.Intensity01);
            WriteFloatLittleEndian(target, 24, entry.SpawnCredits);
            WriteFloatLittleEndian(target, 28, entry.PlayerSpeed);
            WriteFloatLittleEndian(target, 32, entry.PlayerPosition.x);
            WriteFloatLittleEndian(target, 36, entry.PlayerPosition.y);
            WriteFloatLittleEndian(target, 40, entry.PlayerPosition.z);
            WriteUInt32LittleEndian(target, 44, entry.SurvivalCriticalFlags);
            WriteUInt32LittleEndian(target, 48, entry.SurvivalCriticalSeverityPermille);
            WriteUInt32LittleEndian(target, 52, entry.ActivePhase);
        }

        private static unsafe void WriteUInt64LittleEndian(byte* target, int offset, ulong value)
        {
            WriteUInt32LittleEndian(target, offset, unchecked((uint)value));
            WriteUInt32LittleEndian(target, offset + 4, unchecked((uint)(value >> 32)));
        }

        private static unsafe void WriteUInt32LittleEndian(byte* target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        private static unsafe void WriteFloatLittleEndian(byte* target, int offset, float value)
        {
            WriteUInt32LittleEndian(target, offset, unchecked((uint)BitConverter.SingleToInt32Bits(value)));
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

        private bool TryAllocateHeadlessEntity(
            EncounterThreatClass threatClass,
            Vector3 spawnPosition,
            uint spawnSeed,
            uint squadStateBits,
            int squadOrdinal)
        {
            if (!_headlessEntities.IsCreated)
                return false;

            float3 position = new float3(spawnPosition.x, spawnPosition.y, spawnPosition.z);
            byte biomeByte = ResolveBiomeByte(position);
            if (!IsThreatAllowedInBiome(threatClass, biomeByte, position.y))
                return false;

            int slot = FindFreeHeadlessSlot();
            if (slot < 0)
                return false;

            _nextHeadlessEntitySequence = (_nextHeadlessEntitySequence + 1) & 0x00FFFFFF;
            if (_nextHeadlessEntitySequence == 0)
                _nextHeadlessEntitySequence = 1;

            if (!TryResolveRuntimeAup(spawnPosition, out AbsoluteUniversePosition aup))
                return false;

            HeadlessEntity entity = default;
            entity.EntityId = HeadlessEntityIdBase | _nextHeadlessEntitySequence;
            entity.ThreatClass = (int)threatClass;
            entity.TokenCost = ResolveTokenCost(threatClass, _threatAuthoring);
            entity.Position = position;
            entity.PositionAup = AbsoluteUniversePositionBlit.FromAup(in aup);
            entity.SpawnSeed = spawnSeed ^ (squadStateBits + (uint)squadOrdinal * 0x9E3779B9u);
            entity.BiomeByte = biomeByte;
            entity.Flags = (byte)(HeadlessEntityFlags.Active | ResolveHeadlessPredatorFlags(threatClass));
            entity.AgeColdTicks = 0;
            _headlessEntities[slot] = entity;
            return true;
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 local = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(local)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(local.x, local.y, local.z));
            return positionAup.IsFinite();
        }

        private bool TryReleaseHeadlessEntity(int entityId, bool refundHalfCost, bool decrementActiveCount, out bool releasedPredator)
        {
            releasedPredator = false;
            if (!_headlessEntities.IsCreated || entityId == 0)
                return false;

            int length = _headlessEntities.Length;
            for (int i = 0; i < length; i++)
            {
                HeadlessEntity entity = _headlessEntities[i];
                if ((entity.Flags & (byte)HeadlessEntityFlags.Active) == 0 || entity.EntityId != entityId)
                    continue;

                releasedPredator = (entity.Flags & (byte)HeadlessEntityFlags.Predator) != 0;
                EncounterDirectorState state = _frontState[0];
                if (refundHalfCost)
                    state.TokenBudget = math.clamp(state.TokenBudget + entity.TokenCost * 0.5f, 0f, MaxTokenBudget);

                if (decrementActiveCount)
                    state.ActiveEnemyCount = math.max(0, state.ActiveEnemyCount - 1);

                _frontState[0] = state;
                _backState[0] = state;
                _headlessEntities[i] = default;
                _headlessFreeSearchCursor = math.min(_headlessFreeSearchCursor, i);
                return true;
            }

            return false;
        }

        private void ClearHeadlessEntities()
        {
            if (!_headlessEntities.IsCreated)
                return;

            int length = _headlessEntities.Length;
            for (int i = 0; i < length; i++)
                _headlessEntities[i] = default;
            _headlessFreeSearchCursor = 0;
        }

        private void ClearSpawnRequests()
        {
            if (!_spawnRequests.IsCreated)
                return;

            int length = _spawnRequests.Length;
            for (int i = 0; i < length; i++)
                _spawnRequests[i] = new EncounterSpawnRequest { ThreatClass = -1 };
        }

        private void ClearDespawnRequests()
        {
            if (!_despawnRequests.IsCreated)
                return;

            int length = _despawnRequests.Length;
            for (int i = 0; i < length; i++)
                _despawnRequests[i] = 0;
        }

        private int FindFreeHeadlessSlot()
        {
            int length = _headlessEntities.Length;
            if (length <= 0)
                return -1;

            int start = math.clamp(_headlessFreeSearchCursor, 0, length - 1);
            for (int offset = 0; offset < length; offset++)
            {
                int i = start + offset;
                if (i >= length)
                    i -= length;

                if ((_headlessEntities[i].Flags & (byte)HeadlessEntityFlags.Active) == 0)
                {
                    _headlessFreeSearchCursor = i + 1;
                    if (_headlessFreeSearchCursor >= length)
                        _headlessFreeSearchCursor = 0;
                    return i;
                }
            }

            return -1;
        }

        private static HeadlessEntityFlags ResolveHeadlessPredatorFlags(EncounterThreatClass threatClass)
        {
            if (threatClass == EncounterThreatClass.Leviathan)
                return HeadlessEntityFlags.Predator | HeadlessEntityFlags.Apex;

            return threatClass == EncounterThreatClass.Stalker || threatClass == EncounterThreatClass.Swarm
                ? HeadlessEntityFlags.Predator
                : HeadlessEntityFlags.None;
        }

        private static bool WritesPredatorAup(EncounterThreatClass threatClass)
        {
            return threatClass == EncounterThreatClass.Stalker ||
                   threatClass == EncounterThreatClass.Swarm ||
                   threatClass == EncounterThreatClass.Leviathan;
        }

        private static byte ResolveBiomeByte(float3 position)
        {
            if (TryResolveBiomeHashFromActiveTerrain(position, out uint activeTerrainBiomeHash))
                return FoldBiomeHashToByte(activeTerrainBiomeHash);

            int heatmapX = ((int)math.floor(position.x)) & 255;
            int heatmapY = ((int)math.floor(position.z)) & 255;
            if (H8StaticDataArena.TryGetBiomeHeatmapCell(heatmapX, heatmapY, out uint biomeHash))
                return FoldBiomeHashToByte(biomeHash);

            BiomeMatrixDirector director = BiomeMatrixDirector.ActiveRuntimeInstance;
            HectonBiomeMatrixProfile profile = director != null ? director.CurrentProfile : null;
            int matrixIndex = profile != null ? profile.matrixIndex : 0;
            return (byte)math.clamp(matrixIndex, 0, 255);
        }

        private static bool TryResolveBiomeHashFromActiveTerrain(float3 position, out uint biomeHash)
        {
            biomeHash = 0u;
            if (!H8StaticDataArena.IsLoaded)
                return false;

            HectonMapMagicVegetationBridge bridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (bridge == null || !bridge.TryGetActiveHeightTexturePayload(out HectonMapMagicVegetationBridge.TerrainHeightTexturePayload payload))
                return false;

            Vector3 terrainSize = payload.TerrainSize;
            float u = (position.x - payload.TerrainPosition.x) * math.rcp(math.max(terrainSize.x, 0.001f));
            float v = (position.z - payload.TerrainPosition.z) * math.rcp(math.max(terrainSize.z, 0.001f));
            if (!math.isfinite(u) || !math.isfinite(v) || u < 0f || u > 1f || v < 0f || v > 1f)
                return false;

            int heatmapX = math.clamp((int)(u * (BiomeHeatmapResolution - 1) + 0.5f), 0, BiomeHeatmapResolution - 1);
            int heatmapY = math.clamp((int)(v * (BiomeHeatmapResolution - 1) + 0.5f), 0, BiomeHeatmapResolution - 1);
            return H8StaticDataArena.TryGetBiomeHeatmapCell(heatmapX, heatmapY, out biomeHash);
        }

        private static byte FoldBiomeHashToByte(uint biomeHash)
        {
            return (byte)((biomeHash ^ (biomeHash >> 8) ^ (biomeHash >> 16) ^ (biomeHash >> 24)) & 0xFFu);
        }

        private static bool IsThreatAllowedInBiome(EncounterThreatClass threatClass, byte biomeByte, float positionY)
        {
            if (threatClass == EncounterThreatClass.Drone)
                return true;

            byte depthBucket = (byte)(biomeByte & 0x0F);
            switch (threatClass)
            {
                case EncounterThreatClass.Leviathan:
                    return biomeByte >= 32 || positionY <= -180f;
                case EncounterThreatClass.Stalker:
                    return depthBucket >= 2 || positionY <= -60f;
                case EncounterThreatClass.Swarm:
                    return (biomeByte & 0x03) != 1;
                default:
                    return true;
            }
        }

        private void QueuePredatorAupFullUpload()
        {
            _predatorAupFullUploadPending = true;
            _predatorAupPlayerUploadPending = false;
            _predatorAupClearPending = false;
        }

        private void QueuePlayerPredatorAupSlot(float3 playerPosition)
        {
            _pendingPredatorAupPlayerPosition = playerPosition;
            if (!_predatorAupFullUploadPending)
                _predatorAupPlayerUploadPending = true;
            _predatorAupClearPending = false;
        }

        private void FlushPredatorAupFullUpload()
        {
            float3 playerPosition = _frontState.IsCreated ? _frontState[0].PlayerPosition.xyz : float3.zero;
            FlushPredatorAupFullUpload(playerPosition);
        }

        private void FlushPredatorAupFullUpload(float3 playerPosition)
        {
            if (!_predatorAupUpload.IsCreated)
                return;

            EnsureGpuResources();
            WritePlayerPredatorAupSlot(playerPosition);
            ClearPredatorAupSourceIds();
            _predatorAupSourceIds[0] = 0;
            int uploadCount = 1;
            int length = _headlessEntities.IsCreated ? _headlessEntities.Length : 0;
            for (int i = 0; i < length; i++)
            {
                HeadlessEntity entity = _headlessEntities[i];
                if ((entity.Flags & (byte)HeadlessEntityFlags.Active) == 0 ||
                    (entity.Flags & (byte)HeadlessEntityFlags.Predator) == 0)
                {
                    continue;
                }

                float radius = ResolvePredatorAupRadius((EncounterThreatClass)entity.ThreatClass);
                AbsoluteUniversePosition entityAup = entity.PositionAup.ToAup();
                float3 position = entityAup.ToRuntimeFloat3();
                if (!math.all(math.isfinite(position)))
                    position = entity.Position;
                else
                {
                    entity.Position = position;
                    _headlessEntities[i] = entity;
                }

                InsertPredatorAupEntrySorted(ref uploadCount, entity.EntityId, playerPosition, position, radius);
            }
            AppendTrackedPredatorAupEntries(ref uploadCount, playerPosition);

            GraphicsBuffer writeBuffer = ResolvePredatorAupWriteBuffer();
            if (writeBuffer != null && uploadCount > 0)
            {
                GraphicsBufferUploadUtility.UploadNativeArray(writeBuffer, _predatorAupUpload, uploadCount);
                _predatorAupPublishedBuffer = writeBuffer;
                _predatorAupWriteToA = !_predatorAupWriteToA;
            }

            if (_predatorAupPublishedBuffer != null)
            {
                Shader.SetGlobalBuffer(_PredatorAUPBufferId, _predatorAupPublishedBuffer);
                if (_predatorAupGlobalsDirty)
                {
                    Shader.SetGlobalVector(_PredatorAUPParamsId, new Vector4(120f, 1f, 0f, 0f));
                    _predatorAupGlobalsDirty = false;
                }
            }

            if (_lastPublishedPredatorAupCount == uploadCount)
                return;

            _lastPublishedPredatorAupCount = uploadCount;
            Shader.SetGlobalInt(_PredatorAUPCountId, uploadCount);
        }

        private void FlushPlayerPredatorAupSlot(float3 playerPosition)
        {
            if (!_predatorAupUpload.IsCreated)
                return;

            EnsureGpuResources();
            WritePlayerPredatorAupSlot(playerPosition);
            int uploadCount = math.max(1, math.clamp(_lastPublishedPredatorAupCount, 0, PredatorAupBufferCapacity));
            RefreshPublishedTrackedPredatorAupSlots(playerPosition, uploadCount);
            GraphicsBuffer writeBuffer = ResolvePredatorAupWriteBuffer();
            if (writeBuffer != null)
            {
                GraphicsBufferUploadUtility.UploadNativeArray(writeBuffer, _predatorAupUpload, uploadCount);
                _predatorAupPublishedBuffer = writeBuffer;
                _predatorAupWriteToA = !_predatorAupWriteToA;
            }

            if (_predatorAupPublishedBuffer != null)
            {
                Shader.SetGlobalBuffer(_PredatorAUPBufferId, _predatorAupPublishedBuffer);
                if (_predatorAupGlobalsDirty)
                {
                    Shader.SetGlobalVector(_PredatorAUPParamsId, new Vector4(120f, 1f, 0f, 0f));
                    _predatorAupGlobalsDirty = false;
                }
            }

            if (_lastPublishedPredatorAupCount == uploadCount)
                return;

            _lastPublishedPredatorAupCount = uploadCount;
            Shader.SetGlobalInt(_PredatorAUPCountId, uploadCount);
        }

        private void WritePlayerPredatorAupSlot(float3 playerPosition)
        {
            if (!math.all(math.isfinite(playerPosition)))
                playerPosition = float3.zero;

            _predatorAupUpload[0] = new float4(
                playerPosition.x,
                playerPosition.y,
                playerPosition.z,
                PlayerPredatorAupRadiusMeters);
        }

        private void AppendTrackedPredatorAupEntries(ref int uploadCount, float3 playerPosition)
        {
            for (int i = 0; i < MaxActiveEnemies; i++)
            {
                if (_trackedEntityIds[i] == 0 || !WritesPredatorAup(_trackedThreatClasses[i]))
                    continue;

                Transform trackedTransform = _trackedTransforms[i];
                if (trackedTransform == null || !trackedTransform.gameObject.activeInHierarchy)
                    continue;

                Vector3 position = trackedTransform.position;
                float radius = ResolvePredatorAupRadius(_trackedThreatClasses[i]);
                InsertPredatorAupEntrySorted(ref uploadCount, _trackedEntityIds[i], playerPosition, position, radius);
            }
        }

        private void InsertPredatorAupEntrySorted(ref int uploadCount, int sourceId, float3 playerPosition, float3 position, float radius)
        {
            if (!_predatorAupUpload.IsCreated ||
                uploadCount <= 0 ||
                radius <= 0f ||
                !math.all(math.isfinite(position)))
            {
                return;
            }

            float candidateDistanceSq = math.lengthsq(position - playerPosition);
            if (!math.isfinite(candidateDistanceSq))
                return;

            int insertIndex = uploadCount;
            if (uploadCount >= PredatorAupBufferCapacity)
            {
                float4 farthest = _predatorAupUpload[PredatorAupBufferCapacity - 1];
                float farthestDistanceSq = ResolvePredatorAupDistanceSq(farthest, playerPosition);
                if (candidateDistanceSq >= farthestDistanceSq)
                    return;

                insertIndex = PredatorAupBufferCapacity - 1;
            }
            else
            {
                uploadCount++;
            }

            while (insertIndex > 1)
            {
                float4 previous = _predatorAupUpload[insertIndex - 1];
                float previousDistanceSq = ResolvePredatorAupDistanceSq(previous, playerPosition);
                if (previousDistanceSq <= candidateDistanceSq)
                    break;

                _predatorAupUpload[insertIndex] = previous;
                _predatorAupSourceIds[insertIndex] = _predatorAupSourceIds[insertIndex - 1];
                insertIndex--;
            }

            _predatorAupUpload[insertIndex] = new float4(position.x, position.y, position.z, radius);
            _predatorAupSourceIds[insertIndex] = sourceId;
        }

        private void RefreshPublishedTrackedPredatorAupSlots(float3 playerPosition, int uploadCount)
        {
            int safeCount = math.clamp(uploadCount, 1, PredatorAupBufferCapacity);
            for (int i = 1; i < safeCount; i++)
            {
                int sourceId = _predatorAupSourceIds[i];
                if (sourceId == 0 || IsHeadlessEntityId(sourceId))
                    continue;

                int trackedSlot = FindTrackedSlot(sourceId);
                if (trackedSlot < 0 || !WritesPredatorAup(_trackedThreatClasses[trackedSlot]))
                {
                    _predatorAupUpload[i] = new float4(_predatorAupUpload[i].x, _predatorAupUpload[i].y, _predatorAupUpload[i].z, 0f);
                    continue;
                }

                Transform trackedTransform = _trackedTransforms[trackedSlot];
                if (trackedTransform == null || !trackedTransform.gameObject.activeInHierarchy)
                {
                    _predatorAupUpload[i] = new float4(_predatorAupUpload[i].x, _predatorAupUpload[i].y, _predatorAupUpload[i].z, 0f);
                    continue;
                }

                float3 position = trackedTransform.position;
                if (!math.all(math.isfinite(position)))
                {
                    _predatorAupUpload[i] = new float4(_predatorAupUpload[i].x, _predatorAupUpload[i].y, _predatorAupUpload[i].z, 0f);
                    continue;
                }

                float radius = ResolvePredatorAupRadius(_trackedThreatClasses[trackedSlot]);
                _predatorAupUpload[i] = new float4(position.x, position.y, position.z, radius);
            }

            SortPublishedPredatorAupEntries(playerPosition, safeCount);
        }

        private void SortPublishedPredatorAupEntries(float3 playerPosition, int uploadCount)
        {
            int safeCount = math.clamp(uploadCount, 1, PredatorAupBufferCapacity);
            for (int i = 2; i < safeCount; i++)
            {
                float4 value = _predatorAupUpload[i];
                int sourceId = _predatorAupSourceIds[i];
                float distanceSq = ResolvePredatorAupDistanceSq(value, playerPosition);
                int j = i - 1;
                while (j >= 1 && ResolvePredatorAupDistanceSq(_predatorAupUpload[j], playerPosition) > distanceSq)
                {
                    _predatorAupUpload[j + 1] = _predatorAupUpload[j];
                    _predatorAupSourceIds[j + 1] = _predatorAupSourceIds[j];
                    j--;
                }

                _predatorAupUpload[j + 1] = value;
                _predatorAupSourceIds[j + 1] = sourceId;
            }
        }

        private static float ResolvePredatorAupDistanceSq(float4 entry, float3 playerPosition)
        {
            if (entry.w <= 0f)
                return float.MaxValue;

            float3 position = entry.xyz;
            if (!math.all(math.isfinite(position)))
                return float.MaxValue;

            float distanceSq = math.lengthsq(position - playerPosition);
            return math.isfinite(distanceSq) ? distanceSq : float.MaxValue;
        }

        private void ClearPredatorAupSourceIds()
        {
            for (int i = 0; i < PredatorAupBufferCapacity; i++)
                _predatorAupSourceIds[i] = 0;
        }

        private static bool IsHeadlessEntityId(int sourceId)
        {
            return sourceId >= HeadlessEntityIdBase && sourceId < HeadlessEntityIdLimit;
        }

        private static float ResolvePredatorAupRadius(EncounterThreatClass threatClass)
        {
            return threatClass == EncounterThreatClass.Leviathan ? 120f : 55f;
        }

        private GraphicsBuffer ResolvePredatorAupWriteBuffer()
        {
            GraphicsBuffer preferred = _predatorAupWriteToA ? _predatorAupBufferA : _predatorAupBufferB;
            if (preferred != null && preferred.IsValid())
                return preferred;

            GraphicsBuffer fallback = _predatorAupWriteToA ? _predatorAupBufferB : _predatorAupBufferA;
            return fallback != null && fallback.IsValid() ? fallback : null;
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
            if (WritesPredatorAup(threatClass))
                QueuePredatorAupFullUpload();
        }

        private bool UntrackEntity(int entityId)
        {
            int slot = FindTrackedSlot(entityId);
            if (slot < 0)
                return false;

            bool releasedPredator = WritesPredatorAup(_trackedThreatClasses[slot]);
            ClearTrackedSlot(slot);
            return releasedPredator;
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

        private void RollbackFailedSpawn(EncounterThreatClass threatClass, bool refundTokenCost)
        {
            EncounterDirectorState state = _frontState[0];
            if (refundTokenCost)
                state.TokenBudget = math.clamp(state.TokenBudget + ResolveTokenCost(threatClass, _threatAuthoring), 0f, MaxTokenBudget);

            RollbackUnappliedSpawn(ref state);
            _frontState[0] = state;
            _backState[0] = state;
        }

        private void RollbackUnappliedSpawn()
        {
            EncounterDirectorState state = _frontState[0];
            RollbackUnappliedSpawn(ref state);
            _frontState[0] = state;
            _backState[0] = state;
        }

        private static void RollbackUnappliedSpawn(ref EncounterDirectorState state)
        {
            state.ActiveEnemyCount = math.max(0, state.ActiveEnemyCount - 1);
        }

        private static bool IsValidThreatClass(int threatClass)
        {
            return threatClass >= (int)EncounterThreatClass.Drone &&
                   threatClass <= (int)EncounterThreatClass.Leviathan;
        }

        private void PrecomputeCandidateDirections()
        {
            for (int i = 0; i < HighCandidateCount; i++)
                _candidateDirections[i] = ResolveCinematicCandidateDirection(i);
        }

        private static float3 ResolveCinematicCandidateDirection(int index)
        {
            int layer = (index >> 3) & 3;
            float y;
            float horizontal;
            switch (layer)
            {
                case 0:
                    y = -0.6f;
                    horizontal = 0.8f;
                    break;
                case 1:
                    y = -0.2f;
                    horizontal = 0.9797959f;
                    break;
                case 2:
                    y = 0.2f;
                    horizontal = 0.9797959f;
                    break;
                default:
                    y = 0.6f;
                    horizontal = 0.8f;
                    break;
            }

            float diagonal = horizontal * 0.70710678f;
            switch ((index + (layer << 1)) & 7)
            {
                case 0:
                    return new float3(0f, y, horizontal);
                case 1:
                    return new float3(diagonal, y, diagonal);
                case 2:
                    return new float3(horizontal, y, 0f);
                case 3:
                    return new float3(diagonal, y, -diagonal);
                case 4:
                    return new float3(0f, y, -horizontal);
                case 5:
                    return new float3(-diagonal, y, -diagonal);
                case 6:
                    return new float3(-horizontal, y, 0f);
                default:
                    return new float3(-diagonal, y, diagonal);
            }
        }

        private static int ResolveCandidateCount(float hardwareWeight01)
        {
            float runtimeWeight01 = math.min(
                SanitizeQualityWeight01(HomeostasisBrain.GlobalQualityWeight, 1f),
                SanitizeQualityWeight01(PlatformAdaptiveBudgetGovernor.RecommendedQualityWeight, 1f));
            float combinedWeight01 = math.saturate(SanitizeQualityWeight01(hardwareWeight01, 1f) * runtimeWeight01);
            return math.clamp(
                (int)math.round(math.lerp(BaseCandidateCount, HighCandidateCount, combinedWeight01)),
                BaseCandidateCount,
                HighCandidateCount);
        }

        private static float ResolveCandidateHardwareWeight01()
        {
            float cpuWeight01 = SmoothRange01(4f, 8f, SystemInfo.processorCount);
            float graphicsWeight01 = SmoothRange01(1536f, 4096f, SystemInfo.graphicsMemorySize);
            return math.min(cpuWeight01, graphicsWeight01);
        }

        private static float SmoothRange01(float minInclusive, float maxInclusive, float value)
        {
            float denominator = math.max(0.0001f, maxInclusive - minInclusive);
            float t = math.saturate((value - minInclusive) / denominator);
            return t * t * (3f - 2f * t);
        }

        private static float SanitizeQualityWeight01(float value, float fallback)
        {
            return math.saturate(math.isfinite(value) ? value : fallback);
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

        private static void ApplyBiomassThreatCostModifiers(
            ref EncounterThreatAuthoringSnapshot authoring,
            float preyBiomass01,
            float predatorBiomass01)
        {
            if (math.saturate(predatorBiomass01) < 0.1f)
                authoring.LeviathanTokenCost = math.max(0f, authoring.LeviathanTokenCost) * 2f;

            if (math.saturate(preyBiomass01) > 0.9f)
                authoring.SwarmTokenCost = math.max(0f, authoring.SwarmTokenCost) * 0.5f;
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

            ApplyCanonicalThreatCosts(ref snapshot);
            return snapshot;
        }

        private static EncounterThreatAuthoringSnapshot BuildDefaultThreatAuthoringSnapshot()
        {
            EncounterThreatAuthoringSnapshot snapshot = default;
            snapshot.DroneMinIntensity = 0f;
            snapshot.SwarmMinIntensity = 0.25f;
            snapshot.StalkerMinIntensity = 0.55f;
            snapshot.LeviathanMinIntensity = 0.85f;
            snapshot.DroneTokenCost = 5f;
            snapshot.SwarmTokenCost = 5f;
            snapshot.StalkerTokenCost = 50f;
            snapshot.LeviathanTokenCost = 500f;
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

        private static void ApplyCanonicalThreatCosts(ref EncounterThreatAuthoringSnapshot snapshot)
        {
            snapshot.DroneTokenCost = 5f;
            snapshot.SwarmTokenCost = 5f;
            snapshot.StalkerTokenCost = 50f;
            snapshot.LeviathanTokenCost = 500f;
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

            if (hasDependency)
            {
                handle = H8Memory.Release(ref array, handle, NativeArrayOwnerSystem);
                hasDependency = true;
            }
            else
            {
                H8Memory.Release(ref array, NativeArrayOwnerSystem);
            }
        }

        private static void DisposeNativeList<T>(ref NativeList<T> list, ref JobHandle handle, ref bool hasDependency) where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            if (hasDependency)
            {
                handle = list.Dispose(handle);
                hasDependency = true;
            }
            else
            {
                list.Dispose();
            }

            list = default;
        }

        private void ReleasePredatorAupBuffer()
        {
            if (_predatorAupBufferA != null)
            {
                _predatorAupBufferA.Release();
                _predatorAupBufferA = null;
            }

            if (_predatorAupBufferB != null)
            {
                _predatorAupBufferB.Release();
                _predatorAupBufferB = null;
            }

            _predatorAupPublishedBuffer = null;
            _predatorAupWriteToA = true;
            _lastPublishedPredatorAupCount = 0;
            _predatorAupGlobalsDirty = true;
            _predatorAupFullUploadPending = false;
            _predatorAupPlayerUploadPending = false;
            _predatorAupClearPending = true;
            ClearPredatorAupSourceIds();
        }
    
        #region JulesLink_SpawnCooldownGate
        private static void JulesLink_SpawnCooldownGate() { _ = typeof(Hecton8.PureLogic.Systems.SpawnCooldownGate); }
        #endregion
}

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
        private const float MaxTokenBudget = 1000f;
        private const float FrustumRejectPadding = 3f;
        private const float CriticalHealthSpawnSuppressionThreshold = 0.15f;
        private const float CriticalOxygenSpawnSuppressionThreshold = 0.15f;
        private const uint SurvivalCriticalFlagHealth = 1u << 0;
        private const uint SurvivalCriticalFlagOxygen = 1u << 1;
        private const float SpawnClusterRadiusSq = 15f * 15f;
        private const float MinSpawnRadius = 50f;
        private const float MaxSpawnRadius = 150f;
        private const float DespawnKeepDistanceSq = 25f * 25f;
        private const float HeadlessDespawnDistanceSq = 400f * 400f;
        private const float PredictiveSpawnLeadMeters = 200f;
        private const float StationaryVelocitySq = 0.25f;
        private const float ForwardFrustumDotReject = 0.9f;
        private const float DespairModeBudgetCap = 20f;
        private const float ThreatStressRadiusSq = 50f * 50f;
        private const float InvThreatStressRadiusSq = 1f / ThreatStressRadiusSq;
        private const float InvMaxDepthStressMeters = 1f / 4000f;
        private const float InvVelocityStressMetersPerSecond = 1f / 12f;
        private const float InvSafeIdleVelocityMetersPerSecond = 1f / 1.25f;
        private const float InvLowStressCreditThreshold = 1f / 0.35f;
        private const float SafeIdleStressDecayPerTick = 0.06f;
        private const float SelectionEpsilon = 0.0001f;
        private const int HunterSquadOverrideMinSimultaneous = 3;
        internal const int LegacySpawnSlotCount = 3;
        internal const int LegacyDespawnSlotCount = 3;
        internal const int MaxSpawnRequestsPerTick = 16;
        internal const int MaxDespawnRequestsPerTick = 16;
        private const uint HunterSquadHuntingFlankStateBits = (1u << 0) | (1u << 2);

        public EncounterDirectorState CurrentState;
        [NoAlias] public NativeArray<EncounterDirectorState> WriteState;
        [NoAlias, ReadOnly] public NativeArray<EncounterEnemyToken> ActiveEnemies;
        [NoAlias, ReadOnly] public NativeArray<float4> FrustumPlanes;
        [NoAlias, ReadOnly] public NativeArray<float3> CandidateDirections;
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
        // SAFETY_JUSTIFICATION_PARAGRAPH_1: Unity's parallel-for safety guard cannot infer that this director is a
        // single-lane job. The guard sees writes to request indices unrelated to Execute(index), but Schedule(1, 1)
        // creates exactly one writer and Execute returns immediately for any index other than zero.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2: Manual legacy fields were rejected because they capped the director at
        // three spawns and left budget stranded. A managed list was rejected for zero-GC. A NativeQueue was rejected
        // because this job has one producer and a fixed maximum, so an indexed window is cheaper and easier to audit.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3: The invariant is: main thread clears this buffer before scheduling,
        // the scheduled job owns indices [0, MaxSpawnRequestsPerTick), and main thread reads only after the job
        // completed through DispatcherJobSwap. Future multi-lane scheduling must partition this buffer first.
        [NoAlias, NativeDisableParallelForRestriction]
        public NativeArray<EncounterSpawnRequest> SpawnRequests;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1: Same single-lane director invariant as SpawnRequests. The safety system
        // cannot prove that Execute(0) is the sole writer when despawn request indices are compacted independently
        // from the parallel-for index, so this field needs the same explicit ownership proof.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2: Keeping only three legacy integer fields was rejected because dense
        // headless scenes could need multiple cold ticks to free entities beyond 400m. A dynamic container was
        // rejected because the output count is bounded and gameplay tick paths must not allocate or grow storage.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3: The invariant is: main thread clears this fixed int window before
        // scheduling, the job writes each entity id at most once in [0, MaxDespawnRequestsPerTick), and completed
        // output is consumed once on the main thread before the next schedule.
        [NoAlias, NativeDisableParallelForRestriction]
        public NativeArray<int> DespawnRequests;
        [NoAlias]
        public NativeArray<EncounterJobOutput> Output;

        public void Execute(int index)
        {
            if (index != 0)
                return;

            EncounterDirectorState state = CurrentState;
            EncounterJobOutput output = default;
            float playerHealth01 = SanitizeNormalized01(PlayerHealthNormalized);
            float playerOxygen01 = SanitizeNormalized01(PlayerOxygenNormalized);
            float playerInternalStress01 = SanitizeNormalized01(PlayerInternalStress);
            float acousticThreat01 = SanitizeNormalized01(AcousticThreatLevel);
            float avgFrameTimeMs = SanitizeNonNegativeFinite(AvgFrameTimeMs);
            float playerDepthMeters = SanitizeNonNegativeFinite(PlayerPosition.w);
            float playerSpeedMetersPerSecond = SanitizeNonNegativeFinite(PlayerVelocity.w);

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
            float3 frustumRejectExtents = new float3(FrustumRejectPadding);

            for (int i = 0; i < ActiveEnemies.Length; i++)
            {
                EncounterEnemyToken token = ActiveEnemies[i];
                if (token.EntityId == 0)
                    continue;

                float3 toThreat = token.Position - playerPosition;
                float distSq = math.lengthsq(toThreat);
                if (distSq < nearestThreatDistanceSq)
                    nearestThreatDistanceSq = distSq;

                if (distSq > HeadlessDespawnDistanceSq)
                {
                    if (TryWriteDespawnSlot(ref output, token.EntityId))
                    {
                        state.TokenBudget = math.clamp(state.TokenBudget + token.TokenCost * 0.5f, 0f, MaxTokenBudget);
                        continue;
                    }
                }

                bool insideOrIntersectingFrustum = TestPlanesAABB(token.Position, frustumRejectExtents);
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

                if (avgFrameTimeMs <= LoadShedThresholdMs)
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
                ? math.saturate(1f - nearestThreatDistanceSq * InvThreatStressRadiusSq)
                : 0f;
            float depthStress = math.saturate(playerDepthMeters * InvMaxDepthStressMeters) * 0.4f;
            float velocityStress = math.saturate(playerSpeedMetersPerSecond * InvVelocityStressMetersPerSecond) * 0.15f;
            float healthStress = 1f - playerHealth01;
            float oxygenStress = 1f - playerOxygen01;
            float acousticStress = acousticThreat01;
            float rawStress = 0.35f * healthStress +
                              0.25f * oxygenStress +
                              0.25f * proximityStress +
                              0.10f * depthStress +
                              0.05f * velocityStress;
            rawStress = math.saturate(math.max(rawStress, acousticStress));
            float safeIdleRecovery = math.saturate(math.min(
                math.min(playerHealth01, playerOxygen01),
                math.min(
                    1f - proximityStress,
                    math.min(
                        1f - math.saturate(playerSpeedMetersPerSecond * InvSafeIdleVelocityMetersPerSecond),
                        1f - math.max(acousticStress, playerInternalStress01)))));
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
            if (avgFrameTimeMs > EmergencyThresholdMs)
            {
                budgetFlags |= EncounterBudgetFlags.LoadSheddingActive | EncounterBudgetFlags.SpawnSuspended | EncounterBudgetFlags.EmergencyRecall;
                state.RecoveryTimer = 0f;
            }
            else if (avgFrameTimeMs > LoadShedThresholdMs)
            {
                budgetFlags |= EncounterBudgetFlags.LoadSheddingActive | EncounterBudgetFlags.SpawnSuspended;
                budgetFlags &= ~EncounterBudgetFlags.EmergencyRecall;
                state.RecoveryTimer = 0f;
            }
            else if (avgFrameTimeMs <= RecoveryThresholdMs)
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

            bool healthCriticalSuppressed = playerHealth01 <= CriticalHealthSpawnSuppressionThreshold;
            bool oxygenCriticalSuppressed = playerOxygen01 <= CriticalOxygenSpawnSuppressionThreshold;
            bool survivalCriticalSuppressed = healthCriticalSuppressed || oxygenCriticalSuppressed;
            state.SurvivalCriticalFlags = ResolveSurvivalCriticalFlags(playerHealth01, playerOxygen01);
            state.SurvivalCriticalSeverityPermille = ResolveSurvivalCriticalSeverityPermille(playerHealth01, playerOxygen01);
            if (survivalCriticalSuppressed)
                budgetFlags |= EncounterBudgetFlags.DespairModeActive;
            else
                budgetFlags &= ~EncounterBudgetFlags.DespairModeActive;

            state.BudgetFlags = (int)budgetFlags;

            if (((EncounterBudgetFlags)state.BudgetFlags & EncounterBudgetFlags.RegenBlocked) != 0)
            {
                state.TokenRegenRate = 0f;
            }
            else if (survivalCriticalSuppressed)
            {
                state.TokenRegenRate = 0f;
                state.TokenBudget = math.min(state.TokenBudget, DespairModeBudgetCap);
            }
            else
            {
                float lowStressCredit01 = math.saturate(1f - (state.StressLevel * InvLowStressCreditThreshold));
                float lowStressRegen = lowStressCredit01 > 0f ? math.lerp(4f, 10f, lowStressCredit01) : 0f;
                float buildupRegen = (EncounterPhase)state.ActivePhase == EncounterPhase.BuildUp
                    ? math.lerp(5f, 14f, math.saturate(state.IntensityLevel + state.StressLevel * 0.5f))
                    : 0f;
                float relaxRegen = (EncounterPhase)state.ActivePhase == EncounterPhase.Relax ? 8f : 0f;
                state.TokenRegenRate = math.max(math.max(relaxRegen, lowStressRegen), buildupRegen);
            }

            state.TokenBudget = math.clamp(state.TokenBudget + state.TokenRegenRate, 0f, MaxTokenBudget);

            if (avgFrameTimeMs > LoadShedThresholdMs)
            {
                int shedCount = avgFrameTimeMs > EmergencyThresholdMs ? 3 : 1;
                if (bestEntity0 != 0 && shedCount > 0)
                {
                    if (TryWriteDespawnSlot(ref output, bestEntity0))
                    {
                        state.TokenBudget = math.clamp(state.TokenBudget + bestCost0 * 0.5f, 0f, MaxTokenBudget);
                        activeEnemyCount--;
                        DecrementThreatClassCount(ref threatClassCounts, bestEntity0, ActiveEnemies);
                    }
                }

                if (bestEntity1 != 0 && shedCount > 1)
                {
                    if (TryWriteDespawnSlot(ref output, bestEntity1))
                    {
                        state.TokenBudget = math.clamp(state.TokenBudget + bestCost1 * 0.5f, 0f, MaxTokenBudget);
                        activeEnemyCount--;
                        DecrementThreatClassCount(ref threatClassCounts, bestEntity1, ActiveEnemies);
                    }
                }

                if (bestEntity2 != 0 && shedCount > 2)
                {
                    if (TryWriteDespawnSlot(ref output, bestEntity2))
                    {
                        state.TokenBudget = math.clamp(state.TokenBudget + bestCost2 * 0.5f, 0f, MaxTokenBudget);
                        activeEnemyCount--;
                        DecrementThreatClassCount(ref threatClassCounts, bestEntity2, ActiveEnemies);
                    }
                }
            }

            if (survivalCriticalSuppressed)
                state.TokenBudget = math.min(state.TokenBudget, DespairModeBudgetCap);

            bool forceSpawn = !survivalCriticalSuppressed && ForcedThreatCount > 0 && ForcedThreatClass >= 0;
            bool spawnCadenceOpen = forceSpawn || !survivalCriticalSuppressed || ((((int)state.PacingPhaseTimer) & 0x3) == 0);
            if ((forceSpawn || (EncounterPhase)state.ActivePhase != EncounterPhase.Relax) &&
                ((EncounterBudgetFlags)state.BudgetFlags & (EncounterBudgetFlags.LoadSheddingActive | EncounterBudgetFlags.SpawnSuspended)) == 0 &&
                activeEnemyCount < 32 &&
                spawnCadenceOpen)
            {
                int maxRequestSlots = math.min(MaxSpawnRequestsPerTick, SpawnRequests.IsCreated ? SpawnRequests.Length : LegacySpawnSlotCount);
                int maxForcedRequests = forceSpawn ? math.min(ForcedThreatCount, maxRequestSlots) : maxRequestSlots;
                for (int spawnIndex = 0; spawnIndex < maxForcedRequests && activeEnemyCount < 32; spawnIndex++)
                {
                    EncounterThreatClass threatClass;
                    bool resolvedThreatClass = forceSpawn
                        ? TryResolveForcedThreatClass(ForcedThreatClass, threatClassCounts, ThreatAuthoring, out threatClass)
                        : TryResolveDesiredThreatClass(state.IntensityLevel, state.TokenBudget, survivalCriticalSuppressed, threatClassCounts, ThreatAuthoring, out threatClass);
                    if (!resolvedThreatClass)
                        break;

                    float spawnCost = ResolveTokenCost(threatClass, ThreatAuthoring);
                    int maxSimultaneous = ResolveMaxSimultaneous(threatClass, ThreatAuthoring);
                    if (forceSpawn && threatClass == EncounterThreatClass.Stalker)
                        maxSimultaneous = math.max(maxSimultaneous, HunterSquadOverrideMinSimultaneous);

                    int availableThreatSlots = math.max(0, maxSimultaneous - ResolveThreatClassCount(threatClass, threatClassCounts));
                    int availableEnemySlots = math.max(0, 32 - activeEnemyCount);
                    if (availableThreatSlots <= 0 || availableEnemySlots <= 0)
                        break;

                    if (!forceSpawn && spawnCost > SelectionEpsilon && state.TokenBudget + SelectionEpsilon < spawnCost)
                        break;

                    if (!TryResolveSpawnCandidate(playerPosition, PlayerVelocity.xyz, playerForward, forceSpawn, output.SpawnRequestCount, out float3 spawnPosition))
                        break;

                    uint spawnSequence = state.SpawnSequence + 1u;
                    uint spawnSeed = EncounterDirector.BuildDeterministicSeed(
                        playerPosition,
                        unchecked((int)spawnSequence),
                        state.ActivePhase,
                        activeEnemyCount + spawnIndex);
                    uint squadBits = forceSpawn && threatClass == EncounterThreatClass.Stalker
                        ? HunterSquadHuntingFlankStateBits
                        : 0u;
                    WriteSpawnSlot(ref output, output.SpawnRequestCount, threatClass, spawnPosition, spawnSeed, squadBits);
                    output.SpawnRequestCount++;
                    output.SpawnThreatClass = (int)threatClass;
                    if (output.SpawnRequestCount == 1)
                        output.SpawnSquadStateBits = squadBits;
                    state.SpawnSequence = spawnSequence;
                    if (!forceSpawn)
                        state.TokenBudget = math.clamp(state.TokenBudget - spawnCost, 0f, MaxTokenBudget);
                    else
                        output.ForcedSpawnConsumed++;

                    activeEnemyCount++;
                    IncrementThreatClassCount(ref threatClassCounts, threatClass);

                    if (!forceSpawn && state.TokenBudget + SelectionEpsilon < ResolveCheapestAllowedCost(state.IntensityLevel, survivalCriticalSuppressed, threatClassCounts, ThreatAuthoring))
                    {
                        break;
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

        private static uint ResolveSurvivalCriticalFlags(float health01, float oxygen01)
        {
            uint flags = 0u;
            if (health01 <= CriticalHealthSpawnSuppressionThreshold)
                flags |= SurvivalCriticalFlagHealth;
            if (oxygen01 <= CriticalOxygenSpawnSuppressionThreshold)
                flags |= SurvivalCriticalFlagOxygen;
            return flags;
        }

        private static uint ResolveSurvivalCriticalSeverityPermille(float health01, float oxygen01)
        {
            float healthSeverity = CriticalHealthSpawnSuppressionThreshold > 0f
                ? math.saturate((CriticalHealthSpawnSuppressionThreshold - health01) / CriticalHealthSpawnSuppressionThreshold)
                : 0f;
            float oxygenSeverity = CriticalOxygenSpawnSuppressionThreshold > 0f
                ? math.saturate((CriticalOxygenSpawnSuppressionThreshold - oxygen01) / CriticalOxygenSpawnSuppressionThreshold)
                : 0f;
            return unchecked((uint)math.round(math.max(healthSeverity, oxygenSeverity) * 1000f));
        }

        private static float SanitizeNormalized01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizeNonNegativeFinite(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
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
            return math.saturate(numerator * math.rcp(math.max(denominator, 0.0001f)));
        }

        private bool TryResolveSpawnCandidate(
            float3 playerPosition,
            float3 playerVelocity,
            float3 playerForward,
            bool preferFarEdge,
            int reservedCount,
            out float3 spawnPosition)
        {
            spawnPosition = float3.zero;
            float bestScore = float.MinValue;
            bool found = false;
            float velocitySq = math.lengthsq(playerVelocity);
            bool usePredictiveLead = velocitySq > StationaryVelocitySq;
            float3 velocityDirection = usePredictiveLead
                ? playerVelocity * math.rsqrt(velocitySq)
                : playerForward;
            float3 spawnAnchor = usePredictiveLead
                ? playerPosition + velocityDirection * PredictiveSpawnLeadMeters
                : playerPosition;
            float3 frustumRejectExtents = new float3(FrustumRejectPadding);

            int directionCount = math.min(CandidateCount, CandidateDirections.Length);
            float invDirectionDenominator = directionCount > 1
                ? math.rcp((float)(directionCount - 1))
                : 0f;
            for (int i = 0; i < directionCount; i++)
            {
                float normalizedIndex = i * invDirectionDenominator;
                if (preferFarEdge && normalizedIndex < 0.65f)
                    continue;

                float radius = preferFarEdge
                    ? MaxSpawnRadius
                    : math.lerp(MinSpawnRadius, MaxSpawnRadius, normalizedIndex);
                float3 candidate = spawnAnchor + CandidateDirections[i] * radius;
                if (usePredictiveLead && math.dot(candidate - playerPosition, velocityDirection) < 0f)
                    continue;

                if (candidate.y > SurfaceWorldY - 2f)
                    continue;

                float3 candidateOffset = candidate - playerPosition;
                float candidateDistSq = math.lengthsq(candidateOffset);
                float3 toCandidate = NormalizeSafe(candidateOffset, playerForward);
                if (candidateDistSq <= HeadlessDespawnDistanceSq && math.dot(playerForward, toCandidate) > ForwardFrustumDotReject)
                    continue;

                if (TestPlanesAABB(candidate, frustumRejectExtents))
                    continue;

                if (!HasEnemyClearance(candidate))
                    continue;

                if (!HasReservedSpawnClearance(candidate, reservedCount))
                    continue;

                float score = candidateDistSq + 500f * (1f - math.dot(playerForward, toCandidate));
                if (score <= bestScore)
                    continue;

                bestScore = score;
                spawnPosition = candidate;
                found = true;
            }

            return found;
        }

        private void WriteSpawnSlot(
            ref EncounterJobOutput output,
            int index,
            EncounterThreatClass threatClass,
            float3 position,
            uint seed,
            uint squadStateBits)
        {
            if (SpawnRequests.IsCreated && index >= 0 && index < SpawnRequests.Length)
            {
                SpawnRequests[index] = new EncounterSpawnRequest
                {
                    ThreatClass = (int)threatClass,
                    Position = position,
                    VariantSeed = seed,
                    SquadStateBits = squadStateBits
                };
            }

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

        private bool TryWriteDespawnSlot(ref EncounterJobOutput output, int entityId)
        {
            if (entityId == 0)
                return false;

            int maxSlots = math.min(MaxDespawnRequestsPerTick, DespawnRequests.IsCreated ? DespawnRequests.Length : LegacyDespawnSlotCount);
            if (output.DespawnRequestCount >= maxSlots)
                return false;

            int existingCount = math.min(output.DespawnRequestCount, maxSlots);
            if (DespawnRequests.IsCreated)
            {
                for (int i = 0; i < existingCount; i++)
                {
                    if (DespawnRequests[i] == entityId)
                        return false;
                }
            }
            else if (output.DespawnEntityId0 == entityId ||
                     output.DespawnEntityId1 == entityId ||
                     output.DespawnEntityId2 == entityId)
            {
                return false;
            }

            if (DespawnRequests.IsCreated)
                DespawnRequests[output.DespawnRequestCount] = entityId;

            if (output.DespawnRequestCount == 0)
                output.DespawnEntityId0 = entityId;
            else if (output.DespawnRequestCount == 1)
                output.DespawnEntityId1 = entityId;
            else if (output.DespawnRequestCount == 2)
                output.DespawnEntityId2 = entityId;

            output.DespawnRequestCount++;
            return true;
        }

        private static bool TryResolveForcedThreatClass(
            int forcedThreatClass,
            int4 threatClassCounts,
            EncounterThreatAuthoringSnapshot authoring,
            out EncounterThreatClass threatClass)
        {
            threatClass = (EncounterThreatClass)forcedThreatClass;
            if (forcedThreatClass < (int)EncounterThreatClass.Drone ||
                forcedThreatClass > (int)EncounterThreatClass.Leviathan)
            {
                return false;
            }

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

        private bool HasReservedSpawnClearance(float3 candidate, int reservedCount)
        {
            int count = math.min(reservedCount, SpawnRequests.IsCreated ? SpawnRequests.Length : 0);
            for (int i = 0; i < count; i++)
            {
                EncounterSpawnRequest request = SpawnRequests[i];
                if (request.ThreatClass < 0)
                    continue;

                if (math.lengthsq(candidate - request.Position) < SpawnClusterRadiusSq)
                    return false;
            }

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
            bool survivalCriticalSuppressed,
            int4 threatClassCounts,
            EncounterThreatAuthoringSnapshot authoring,
            out EncounterThreatClass threatClass)
        {
            if (CanSpawnThreatClass(EncounterThreatClass.Leviathan, intensityLevel, tokenBudget, survivalCriticalSuppressed, threatClassCounts, authoring))
            {
                threatClass = EncounterThreatClass.Leviathan;
                return true;
            }

            if (CanSpawnThreatClass(EncounterThreatClass.Stalker, intensityLevel, tokenBudget, survivalCriticalSuppressed, threatClassCounts, authoring))
            {
                threatClass = EncounterThreatClass.Stalker;
                return true;
            }

            if (CanSpawnThreatClass(EncounterThreatClass.Swarm, intensityLevel, tokenBudget, survivalCriticalSuppressed, threatClassCounts, authoring))
            {
                threatClass = EncounterThreatClass.Swarm;
                return true;
            }

            if (CanSpawnThreatClass(EncounterThreatClass.Drone, intensityLevel, tokenBudget, survivalCriticalSuppressed, threatClassCounts, authoring))
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
            bool survivalCriticalSuppressed,
            int4 threatClassCounts,
            EncounterThreatAuthoringSnapshot authoring)
        {
            if (survivalCriticalSuppressed && !AllowsSurvivalCriticalSpawn(threatClass, authoring))
                return false;

            if (intensityLevel + SelectionEpsilon < ResolveMinimumIntensity(threatClass, authoring))
                return false;

            if (tokenBudget + SelectionEpsilon < ResolveTokenCost(threatClass, authoring))
                return false;

            return ResolveThreatClassCount(threatClass, threatClassCounts) < ResolveMaxSimultaneous(threatClass, authoring);
        }

        private static float ResolveCheapestAllowedCost(
            float intensityLevel,
            bool survivalCriticalSuppressed,
            int4 threatClassCounts,
            EncounterThreatAuthoringSnapshot authoring)
        {
            float cheapest = float.MaxValue;
            if (CanSpawnThreatClass(EncounterThreatClass.Drone, intensityLevel, float.MaxValue, survivalCriticalSuppressed, threatClassCounts, authoring))
                cheapest = math.min(cheapest, ResolveTokenCost(EncounterThreatClass.Drone, authoring));
            if (CanSpawnThreatClass(EncounterThreatClass.Swarm, intensityLevel, float.MaxValue, survivalCriticalSuppressed, threatClassCounts, authoring))
                cheapest = math.min(cheapest, ResolveTokenCost(EncounterThreatClass.Swarm, authoring));
            if (CanSpawnThreatClass(EncounterThreatClass.Stalker, intensityLevel, float.MaxValue, survivalCriticalSuppressed, threatClassCounts, authoring))
                cheapest = math.min(cheapest, ResolveTokenCost(EncounterThreatClass.Stalker, authoring));
            if (CanSpawnThreatClass(EncounterThreatClass.Leviathan, intensityLevel, float.MaxValue, survivalCriticalSuppressed, threatClassCounts, authoring))
                cheapest = math.min(cheapest, ResolveTokenCost(EncounterThreatClass.Leviathan, authoring));

            return cheapest;
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

        private static bool AllowsSurvivalCriticalSpawn(EncounterThreatClass threatClass, EncounterThreatAuthoringSnapshot authoring)
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
            float normalizedTime = duration > 0.0001f ? math.saturate(timer * math.rcp(duration)) : 0f;
            switch (phase)
            {
                case EncounterPhase.BuildUp:
                    float buildSine = math.saturate(MathLodApproximation.ApproxSinBhaskara(1.57079637f * normalizedTime));
                    return buildSine * math.sqrt(buildSine);
                case EncounterPhase.Peak:
                    return 1f - 0.1f * MathLodApproximation.ApproxSinBhaskara(6.28318531f * normalizedTime);
                case EncounterPhase.Decay:
                    float decayCos = math.saturate(MathLodApproximation.ApproxCosBhaskara(1.57079637f * normalizedTime));
                    return math.saturate(decayCos * (1.55f - 0.55f * decayCos));
                default:
                    return 0.05f + 0.05f * MathLodApproximation.ApproxSinBhaskara(3.14159265f * normalizedTime);
            }
        }

        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private static float Triangle01(float value)
        {
            float t = math.frac(value);
            return 1f - math.abs((t * 2f) - 1f);
        }

        private static float TriangleSigned(float value)
        {
            return (Triangle01(value) * 2f) - 1f;
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
