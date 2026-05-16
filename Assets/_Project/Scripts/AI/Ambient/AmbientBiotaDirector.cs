using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Caves;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI.Ambient
{
    [DisallowMultipleComponent]
    public sealed class AmbientBiotaDirector : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IAmbientBiotaService
    {
        private const float AupCellSizeMeters = 5000.0f;
        private const float TwoPi = 6.28318530718f;
        private const uint BaseSeedSalt = 0x42494F54u; // BIOT
        private const uint MacroHydrationSeedSalt = 0x4D485944u; // MHYD
        private const int BucketMask = 15;
        private const int MacroHydrationCounterCount = 4;
        private const int MacroVisualBoidsPerBiomassUnit = 64;
        private const float MacroHydrationStressCullThreshold01 = 0.7f;
        private const float DefaultFlowX = 0.08f;
        private const float DefaultFlowY = -0.01f;
        private const float DefaultFlowZ = 0.04f;

        [SerializeField, Min(128)] private int lowTierCapacity = 2048;
        [SerializeField, Min(128)] private int highTierCapacity = 8192;
        [SerializeField, Min(1)] private int spawnBudgetPerSlowTick = 64;
        [SerializeField, Min(8f)] private float simulationRadiusMeters = 100f;
        [SerializeField, Min(1f)] private float lifetimeSeconds = 45f;
        [SerializeField] private ushort baseSpeciesId = 16;

        private IDataVault _vault;
        private IPlayerRuntimeContext _player;
        private IEcosystemDirectorService _ecosystem;
        private ISimulationBucketer _bucketer;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private NativeArray<AbsoluteUniversePosition> _biotaAups;
        private NativeArray<float4> _biotaVelocities;
        private NativeArray<AmbientBiotaState> _biotaStates;
        private NativeArray<int> _macroHydrationCounters;
        private JobHandle _activeJobHandle;
        private AbsoluteUniversePosition _lastPlayerAup;
        private float3 _lastPlayerRuntimePosition;
        private float3 _flowVector = new float3(DefaultFlowX, DefaultFlowY, DefaultFlowZ);
        private int _capacity;
        private int _activeBiotaCount;
        private int _previousActiveBiotaCount;
        private int _tickCount;
        private float _cullRatePerSecond;
        private uint _frameIndex;
        private bool _jobPending;
        private bool _serviceRegistered;
        private bool _tickRegistered;
        private bool _slowTickRegistered;
        private bool _lateFrameRegistered;

        public bool IsInitialized => _biotaAups.IsCreated &&
                                     _biotaVelocities.IsCreated &&
                                     _biotaStates.IsCreated &&
                                     _capacity > 0;

        public int TickCount => _tickCount;

        public int Capacity => _capacity;

        public int ActiveBiotaCount => _activeBiotaCount;

        public float CullRatePerSecond => _cullRatePerSecond;

        public NativeArray<AbsoluteUniversePosition>.ReadOnly BiotaAups =>
            _biotaAups.IsCreated ? _biotaAups.AsReadOnly() : default;

        public NativeArray<float4>.ReadOnly BiotaVelocities =>
            _biotaVelocities.IsCreated ? _biotaVelocities.AsReadOnly() : default;

        public NativeArray<AmbientBiotaState>.ReadOnly BiotaStates =>
            _biotaStates.IsCreated ? _biotaStates.AsReadOnly() : default;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            CacheDependencies();
            EnsureVaultBuffers();
            EnsureMacroHydrationCounters();
            RegisterRuntime();
        }

        private void OnDisable()
        {
            CompleteActiveJob();
            UnregisterRuntime();
            DisposeMacroHydrationCounters();
            _biotaAups = default;
            _biotaVelocities = default;
            _biotaStates = default;
            _vault = null;
            _player = null;
            _ecosystem = null;
            _bucketer = null;
            _vegetationBridge = null;
            _capacity = 0;
            _activeBiotaCount = 0;
            _previousActiveBiotaCount = 0;
            _cullRatePerSecond = 0f;
        }

        public void Tick(float deltaTime)
        {
            _tickCount++;

            if (!IsInitialized || _jobPending)
                return;

            float safeDeltaTime = math.isfinite(deltaTime) && deltaTime > 0f
                ? math.min(deltaTime, 0.05f)
                : 0f;
            if (safeDeltaTime <= 0f)
                return;

            if (!TryCapturePlayerPose(out PlayerRuntimePoseSnapshot pose))
                return;

            _lastPlayerAup = pose.Aup;
            _lastPlayerRuntimePosition = pose.RuntimePosition;
            int activeBucket = ResolveActiveBucket();
            float radius = ResolveSimulationRadiusMeters();

            AmbientBiotaDriftJob driftJob = new AmbientBiotaDriftJob
            {
                Aups = _biotaAups,
                Velocities = _biotaVelocities,
                States = _biotaStates,
                CenterAup = _lastPlayerAup,
                FlowVector = _flowVector,
                DeltaTime = safeDeltaTime,
                RadiusSq = radius * radius,
                ActiveBucket = activeBucket,
                FrameIndex = _frameIndex
            };

            _activeJobHandle = driftJob.Schedule(_capacity, 64);
            _jobPending = true;
            _frameIndex++;
        }

        public void SlowTick()
        {
            if (!IsInitialized || _jobPending)
                return;

            if (!TryCapturePlayerPose(out PlayerRuntimePoseSnapshot pose))
                return;

            _lastPlayerAup = pose.Aup;
            _lastPlayerRuntimePosition = pose.RuntimePosition;
            RefreshEcologyInputs(_lastPlayerRuntimePosition, out float preyBiomass01, out float carryingCapacity01);
            RefreshAbyssalFlow(_lastPlayerRuntimePosition);

            int targetActive = ResolveTargetActiveCount(preyBiomass01, carryingCapacity01);
            int spawnBudget = math.min(spawnBudgetPerSlowTick, math.max(0, targetActive - _activeBiotaCount));
            if (spawnBudget <= 0)
                return;

            AmbientBiotaSpawnJob spawnJob = new AmbientBiotaSpawnJob
            {
                Aups = _biotaAups,
                Velocities = _biotaVelocities,
                States = _biotaStates,
                CenterAup = _lastPlayerAup,
                PreyBiomass01 = preyBiomass01,
                CarryingCapacity01 = carryingCapacity01,
                RadiusMeters = ResolveSimulationRadiusMeters(),
                LifetimeSeconds = lifetimeSeconds,
                Capacity = _capacity,
                SpawnBudget = spawnBudget,
                BaseSpeciesId = baseSpeciesId,
                Seed = BaseSeedSalt,
                FrameIndex = _frameIndex
            };

            _activeJobHandle = spawnJob.Schedule();
            _jobPending = true;
            _frameIndex++;
        }

        public void LateFrameTick()
        {
            if (!_jobPending)
                return;

            CompleteActiveJob();
            RecountActiveBiota();
        }

        public bool TryHydrateMacroSwarms(
            in AbsoluteUniversePosition centerAup,
            ushort radiusMetersQ,
            NativeArray<MacroSwarm> swarms,
            int swarmCount,
            byte qualityTier,
            float systemStress01,
            out int spawnedBoidCount)
        {
            spawnedBoidCount = 0;
            if (!IsInitialized || !swarms.IsCreated || swarmCount <= 0)
                return false;

            CompleteActiveJob();
            EnsureMacroHydrationCounters();
            if (!_macroHydrationCounters.IsCreated)
                return false;

            ClearMacroCounters();
            int safeSwarmCount = math.min(swarmCount, swarms.Length);
            float radiusMeters = math.max(8f, radiusMetersQ);
            byte spawnQualityTier = ResolveSdfGuardedQualityTier(in centerAup, qualityTier);
            var hydrationJob = new AmbientBiotaMacroHydrationJob
            {
                Aups = _biotaAups,
                Velocities = _biotaVelocities,
                States = _biotaStates,
                Swarms = swarms,
                Counters = _macroHydrationCounters,
                CenterAup = centerAup,
                RadiusMeters = radiusMeters,
                LifetimeSeconds = lifetimeSeconds,
                Capacity = _capacity,
                SwarmCount = safeSwarmCount,
                BaseSpeciesId = baseSpeciesId,
                Seed = MacroHydrationSeedSalt,
                FrameIndex = _frameIndex,
                QualityTier = spawnQualityTier,
                SystemStress01 = math.saturate(systemStress01)
            };

            hydrationJob.Schedule().Complete();
            _frameIndex++;
            spawnedBoidCount = _macroHydrationCounters[0];
            if (spawnedBoidCount <= 0)
                return false;

            RecountActiveBiota();
            GlobalSignals.Publish(new EntitySpawnSignal
            {
                PositionAup = centerAup,
                SourceHash = MacroHydrationSeedSalt,
                SpawnedCount = (ushort)math.clamp(spawnedBoidCount, 0, ushort.MaxValue),
                RequestedCount = (ushort)math.clamp(_macroHydrationCounters[1], 0, ushort.MaxValue),
                EntityKind = EntitySpawnSignal.KindEcology,
                QualityTier = spawnQualityTier,
                Flags = (byte)(EntitySpawnSignal.FlagEcology |
                               (spawnQualityTier == 0 ? EntitySpawnSignal.FlagLowTierVisual : 0) |
                               (spawnQualityTier >= 2 ? EntitySpawnSignal.FlagSdfEmergence : 0)),
                Frame = unchecked((uint)Time.frameCount)
            });
            return true;
        }

        public bool TryPackMacroHydratedBiota(
            in AbsoluteUniversePosition centerAup,
            ushort radiusMetersQ,
            out int releasedBoidCount,
            out float biomassValue)
        {
            releasedBoidCount = 0;
            biomassValue = 0f;
            if (!IsInitialized)
                return false;

            CompleteActiveJob();
            EnsureMacroHydrationCounters();
            if (!_macroHydrationCounters.IsCreated)
                return false;

            ClearMacroCounters();
            float radiusMeters = math.max(8f, radiusMetersQ);
            var dehydrationJob = new AmbientBiotaMacroDehydrationJob
            {
                Aups = _biotaAups,
                Velocities = _biotaVelocities,
                States = _biotaStates,
                Counters = _macroHydrationCounters,
                CenterAup = centerAup,
                RadiusSq = radiusMeters * radiusMeters,
                Capacity = _capacity
            };

            dehydrationJob.Schedule().Complete();
            releasedBoidCount = _macroHydrationCounters[0];
            if (releasedBoidCount <= 0)
                return false;

            biomassValue = math.saturate(releasedBoidCount * math.rcp((float)MacroVisualBoidsPerBiomassUnit));
            RecountActiveBiota();
            return biomassValue > 0f;
        }

        private void CacheDependencies()
        {
            _vault = GlobalRegistry.DataVault;
            _player = GlobalRegistry.Player;
            _ecosystem = GlobalRegistry.EcosystemDirector;
            _bucketer = GlobalRegistry.SimulationBucketer;
            _vegetationBridge = GlobalRegistry.MapMagicVegetation;
        }

        private void RegisterRuntime()
        {
            if (!_serviceRegistered)
            {
                GlobalRegistry.RegisterAmbientBiotaRuntime(this);
                _serviceRegistered = ReferenceEquals(GlobalRegistry.AmbientBiota, this);
            }

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_tickRegistered)
                _tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);

            if (!_slowTickRegistered)
                _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);

            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void UnregisterRuntime()
        {
            if (_tickRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _tickRegistered = false;
            }

            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            if (_serviceRegistered)
            {
                GlobalRegistry.UnregisterAmbientBiotaRuntime(this);
                _serviceRegistered = false;
            }
        }

        private void EnsureVaultBuffers()
        {
            if (_vault == null)
                return;

            _capacity = ResolveCapacity();
            _biotaAups = _vault.GetBuffer<AbsoluteUniversePosition>(
                BufferID.BiotaAUPs,
                _capacity,
                SystemID.AmbientBiota,
                NativeArrayOptions.ClearMemory);
            _biotaVelocities = _vault.GetBuffer<float4>(
                BufferID.BiotaVelocities,
                _capacity,
                SystemID.AmbientBiota,
                NativeArrayOptions.ClearMemory);
            _biotaStates = _vault.GetBuffer<AmbientBiotaState>(
                BufferID.BiotaStates,
                _capacity,
                SystemID.AmbientBiota,
                NativeArrayOptions.ClearMemory);
        }

        private void EnsureMacroHydrationCounters()
        {
            if (_macroHydrationCounters.IsCreated)
                return;

            _macroHydrationCounters = H8Memory.Allocate<int>(
                MacroHydrationCounterCount,
                SystemID.AmbientBiota,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[4] - macro hydration/dehydration counters - owner: AmbientBiotaDirector
        }

        private void DisposeMacroHydrationCounters()
        {
            if (!_macroHydrationCounters.IsCreated)
                return;

            H8Memory.Release(ref _macroHydrationCounters, SystemID.AmbientBiota);
        }

        private void ClearMacroCounters()
        {
            for (int i = 0; i < _macroHydrationCounters.Length; i++)
                _macroHydrationCounters[i] = 0;
        }

        private int ResolveCapacity()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            int requested = tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350
                ? lowTierCapacity
                : highTierCapacity;
            return math.clamp(requested, 128, 32768);
        }

        private int ResolveTargetActiveCount(float preyBiomass01, float carryingCapacity01)
        {
            float biomass = math.saturate(math.min(preyBiomass01, carryingCapacity01));
            float lowTierScalar = GlobalRegistry.ScalabilityTierProfileByte == 0 ? 0.35f : 0.65f;
            return math.clamp((int)math.round(_capacity * biomass * lowTierScalar), 0, _capacity);
        }

        private float ResolveSimulationRadiusMeters()
        {
            float radius = math.max(8f, simulationRadiusMeters);
            if (GlobalSignals.SystemStress01 > 0.8f)
                radius = math.min(radius, 30f);

            return radius;
        }

        private int ResolveActiveBucket()
        {
            if (_bucketer != null && _bucketer.IsInitialized)
                return _bucketer.ActiveSlowBucket & BucketMask;

            return (int)(_frameIndex & BucketMask);
        }

        private bool TryCapturePlayerPose(out PlayerRuntimePoseSnapshot pose)
        {
            IPlayerRuntimeContext player = _player;
            if (player == null || !player.IsInitialized)
            {
                player = GlobalRegistry.Player;
                _player = player;
            }

            if (player != null && player.TryGetPlayerPoseSnapshot(out pose))
                return true;

            pose = default;
            return false;
        }

        private void RefreshEcologyInputs(float3 runtimePosition, out float preyBiomass01, out float carryingCapacity01)
        {
            preyBiomass01 = 0.35f;
            carryingCapacity01 = 0.5f;
            IEcosystemDirectorService ecosystem = _ecosystem;
            if (ecosystem == null || !ecosystem.IsInitialized)
            {
                ecosystem = GlobalRegistry.EcosystemDirector;
                _ecosystem = ecosystem;
            }

            if (ecosystem == null)
                return;

            Vector3 position = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (ecosystem.TryGetBiomassAvailability(position, out float prey, out _, out float capacity01))
            {
                preyBiomass01 = math.saturate(prey);
                carryingCapacity01 = math.saturate(capacity01);
            }
        }

        private void RefreshAbyssalFlow(float3 runtimePosition)
        {
            HectonMapMagicVegetationBridge bridge = _vegetationBridge;
            if (bridge == null)
            {
                bridge = GlobalRegistry.MapMagicVegetation;
                _vegetationBridge = bridge;
            }

            if (bridge != null)
            {
                Vector3 position = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                if (bridge.TrySampleAbyssalFlow(position, out Vector3 flow) &&
                    float.IsFinite(flow.x) &&
                    float.IsFinite(flow.y) &&
                    float.IsFinite(flow.z))
                {
                    _flowVector = new float3(flow.x, flow.y, flow.z);
                    return;
                }
            }

            _flowVector = new float3(DefaultFlowX, DefaultFlowY, DefaultFlowZ);
        }

        private void CompleteActiveJob()
        {
            if (!_jobPending)
                return;

            _activeJobHandle.Complete();
            _activeJobHandle = default;
            _jobPending = false;
        }

        private void RecountActiveBiota()
        {
            if (!_biotaStates.IsCreated)
                return;

            int active = 0;
            int length = math.min(_capacity, _biotaStates.Length);
            for (int i = 0; i < length; i++)
            {
                if ((_biotaStates[i].StateFlags & AmbientBiotaState.FlagActive) != 0u)
                    active++;
            }

            int culled = math.max(0, _previousActiveBiotaCount - active);
            _cullRatePerSecond = culled;
            _previousActiveBiotaCount = active;
            _activeBiotaCount = active;
        }

        [BurstCompile(FloatPrecision.Low, FloatMode.Fast, CompileSynchronously = false)]
        private struct AmbientBiotaMacroHydrationJob : IJob
        {
            public NativeArray<AbsoluteUniversePosition> Aups;
            public NativeArray<float4> Velocities;
            public NativeArray<AmbientBiotaState> States;
            [ReadOnly] public NativeArray<MacroSwarm> Swarms;
            public NativeArray<int> Counters;
            public AbsoluteUniversePosition CenterAup;
            public float RadiusMeters;
            public float LifetimeSeconds;
            public int Capacity;
            public int SwarmCount;
            public ushort BaseSpeciesId;
            public uint Seed;
            public uint FrameIndex;
            public byte QualityTier;
            public float SystemStress01;

            public void Execute()
            {
                int safeCapacity = math.min(Capacity, math.min(Aups.Length, math.min(Velocities.Length, States.Length)));
                int safeSwarmCount = math.min(SwarmCount, Swarms.Length);
                if (safeCapacity <= 0 || safeSwarmCount <= 0)
                    return;

                float radius = math.max(8f, RadiusMeters);
                float visualScale = SystemStress01 > MacroHydrationStressCullThreshold01 ? 0.5f : 1f;
                int spawned = 0;
                int requested = 0;
                int invalid = 0;
                int searchStart = 0;

                for (int swarmIndex = 0; swarmIndex < safeSwarmCount; swarmIndex++)
                {
                    MacroSwarm swarm = Swarms[swarmIndex];
                    if (!IsValidSwarm(in swarm))
                    {
                        invalid++;
                        continue;
                    }

                    int swarmBudget = math.clamp(
                        (int)math.ceil(math.saturate(swarm.BiomassValue) * MacroVisualBoidsPerBiomassUnit * visualScale),
                        1,
                        MacroVisualBoidsPerBiomassUnit);
                    requested += swarmBudget;

                    for (int spawnedForSwarm = 0; spawnedForSwarm < swarmBudget; spawnedForSwarm++)
                    {
                        int slot = FindInactiveSlot(safeCapacity, ref searchStart);
                        if (slot < 0)
                            break;

                        uint hash = Hash32(Seed ^ swarm.HashId ^ ((uint)slot * 747796405u) ^ ((uint)spawnedForSwarm * 2891336453u) ^ FrameIndex);
                        float3 offset = ResolveSpawnOffset(hash, radius, QualityTier);
                        if (!math.all(math.isfinite(offset)))
                        {
                            invalid++;
                            continue;
                        }

                        AbsoluteUniversePosition aup = OffsetAup(in CenterAup, offset);
                        if (!IsFiniteAup(in aup))
                        {
                            invalid++;
                            continue;
                        }

                        float3 velocity = ResolveSpawnVelocity(hash, QualityTier);
                        Velocities[slot] = new float4(velocity, ((hash >> 8) & 255u) * (1f / 255f));
                        Aups[slot] = aup;
                        States[slot] = new AmbientBiotaState
                        {
                            StateFlags = AmbientBiotaState.FlagActive |
                                         AmbientBiotaState.FlagMacroHydrated |
                                         (QualityTier == 0 || SystemStress01 > MacroHydrationStressCullThreshold01
                                             ? AmbientBiotaState.FlagLowTierBillboard
                                             : 0u) |
                                         (QualityTier >= 2 ? AmbientBiotaState.FlagSdfEmergence : 0u),
                            StableHash = hash,
                            SpeciesId = (ushort)(BaseSpeciesId + 8 + (hash & 7u)),
                            BucketId = (ushort)(hash & BucketMask),
                            AgeSeconds = 0f,
                            LifetimeSeconds = math.max(1f, LifetimeSeconds * math.lerp(0.8f, 1.6f, ((hash >> 16) & 255u) * (1f / 255f))),
                            ScaleMeters = math.lerp(0.08f, QualityTier >= 2 ? 0.42f : 0.26f, ((hash >> 24) & 255u) * (1f / 255f)),
                            Emission01 = math.saturate(0.2f + swarm.BiomassValue * 0.6f),
                            Reserved = swarm.HashId
                        };
                        spawned++;
                    }
                }

                if (Counters.IsCreated && Counters.Length >= MacroHydrationCounterCount)
                {
                    Counters[0] = spawned;
                    Counters[1] = requested;
                    Counters[2] = math.max(0, requested - spawned);
                    Counters[3] = invalid;
                }
            }

            private int FindInactiveSlot(int safeCapacity, ref int searchStart)
            {
                for (int scanned = 0; scanned < safeCapacity; scanned++)
                {
                    int index = searchStart + scanned;
                    if (index >= safeCapacity)
                        index -= safeCapacity;

                    AmbientBiotaState state = States[index];
                    if ((state.StateFlags & AmbientBiotaState.FlagActive) != 0u)
                        continue;

                    searchStart = index + 1;
                    if (searchStart >= safeCapacity)
                        searchStart = 0;
                    return index;
                }

                return -1;
            }

            private static bool IsValidSwarm(in MacroSwarm swarm)
            {
                return swarm.HashId != 0u &&
                       math.isfinite(swarm.BiomassValue) &&
                       math.isfinite(swarm.Speed) &&
                       math.all(math.isfinite(swarm.CurrentSectorAup)) &&
                       swarm.BiomassValue > 0.0001f;
            }

            private static float3 ResolveSpawnOffset(uint hash, float radius, byte qualityTier)
            {
                float normA = (hash & 65535u) * (1f / 65535f);
                float normB = ((hash >> 10) & 1023u) * (1f / 1023f);
                float normC = ((hash >> 20) & 1023u) * (1f / 1023f);
                float angle = normA * TwoPi;
                if (qualityTier == 0)
                {
                    return new float3(
                        math.cos(angle) * radius,
                        (normC - 0.5f) * 10f,
                        math.sin(angle) * radius);
                }

                float radial = math.lerp(radius * 0.18f, radius * 0.82f, normB);
                float sdfEmergenceBias = qualityTier >= 2 ? -math.lerp(3f, 18f, normC) : (normC - 0.5f) * 18f;
                return new float3(
                    math.cos(angle) * radial,
                    sdfEmergenceBias,
                    math.sin(angle) * radial);
            }

            private static float3 ResolveSpawnVelocity(uint hash, byte qualityTier)
            {
                float scalar = qualityTier >= 2 ? 0.18f : 0.08f;
                return new float3(
                    (((hash >> 3) & 255u) * (1f / 255f) - 0.5f) * scalar,
                    (((hash >> 11) & 255u) * (1f / 255f)) * scalar,
                    (((hash >> 19) & 255u) * (1f / 255f) - 0.5f) * scalar);
            }
        }

        [BurstCompile(FloatPrecision.Low, FloatMode.Fast, CompileSynchronously = false)]
        private struct AmbientBiotaMacroDehydrationJob : IJob
        {
            public NativeArray<AbsoluteUniversePosition> Aups;
            public NativeArray<float4> Velocities;
            public NativeArray<AmbientBiotaState> States;
            public NativeArray<int> Counters;
            public AbsoluteUniversePosition CenterAup;
            public float RadiusSq;
            public int Capacity;

            public void Execute()
            {
                int safeCapacity = math.min(Capacity, math.min(Aups.Length, math.min(Velocities.Length, States.Length)));
                int released = 0;
                uint hash = 2166136261u;
                for (int i = 0; i < safeCapacity; i++)
                {
                    AmbientBiotaState state = States[i];
                    if ((state.StateFlags & (AmbientBiotaState.FlagActive | AmbientBiotaState.FlagMacroHydrated)) !=
                        (AmbientBiotaState.FlagActive | AmbientBiotaState.FlagMacroHydrated))
                    {
                        continue;
                    }

                    double3 delta = DeltaMeters(in Aups[i], in CenterAup);
                    double distSq = math.dot(delta, delta);
                    if (!math.isfinite((float)distSq) || distSq > RadiusSq)
                        continue;

                    hash = Hash32(hash ^ state.StableHash);
                    States[i] = default;
                    Aups[i] = default;
                    Velocities[i] = default;
                    released++;
                }

                if (Counters.IsCreated && Counters.Length >= MacroHydrationCounterCount)
                {
                    Counters[0] = released;
                    Counters[1] = unchecked((int)hash);
                }
            }
        }

        [BurstCompile(FloatPrecision.Low, FloatMode.Fast, CompileSynchronously = false)]
        private struct AmbientBiotaSpawnJob : IJob
        {
            public NativeArray<AbsoluteUniversePosition> Aups;
            public NativeArray<float4> Velocities;
            public NativeArray<AmbientBiotaState> States;
            public AbsoluteUniversePosition CenterAup;
            public float PreyBiomass01;
            public float CarryingCapacity01;
            public float RadiusMeters;
            public float LifetimeSeconds;
            public int Capacity;
            public int SpawnBudget;
            public ushort BaseSpeciesId;
            public uint Seed;
            public uint FrameIndex;

            public void Execute()
            {
                if (SpawnBudget <= 0 || !math.isfinite(PreyBiomass01) || PreyBiomass01 <= 0.02f)
                    return;

                int safeCapacity = math.min(Capacity, math.min(Aups.Length, math.min(Velocities.Length, States.Length)));
                int activated = 0;
                uint biomassThreshold = (uint)math.round(math.saturate(PreyBiomass01 * CarryingCapacity01) * 1023f);
                for (int i = 0; i < safeCapacity && activated < SpawnBudget; i++)
                {
                    AmbientBiotaState state = States[i];
                    if ((state.StateFlags & AmbientBiotaState.FlagActive) != 0u)
                        continue;

                    uint hash = Hash32(Seed ^ ((uint)i * 747796405u) ^ (FrameIndex * 2891336453u));
                    if ((hash & 1023u) > biomassThreshold)
                        continue;

                    float normA = (hash & 65535u) * (1.0f / 65535.0f);
                    float normB = ((hash >> 10) & 1023u) * (1.0f / 1023.0f);
                    float normC = ((hash >> 20) & 1023u) * (1.0f / 1023.0f);
                    float angle = normA * TwoPi;
                    float radial = math.lerp(RadiusMeters * 0.35f, RadiusMeters, normB);
                    float3 offset = new float3(
                        math.cos(angle) * radial,
                        (normC - 0.5f) * 28f,
                        math.sin(angle) * radial);

                    if (!math.all(math.isfinite(offset)))
                        continue;

                    Aups[i] = OffsetAup(CenterAup, offset);
                    Velocities[i] = new float4(
                        (normB - 0.5f) * 0.08f,
                        (normC - 0.5f) * 0.03f,
                        (normA - 0.5f) * 0.08f,
                        normA);
                    States[i] = new AmbientBiotaState
                    {
                        StateFlags = AmbientBiotaState.FlagActive | AmbientBiotaState.FlagLowTierBillboard,
                        StableHash = hash,
                        SpeciesId = (ushort)(BaseSpeciesId + (hash & 3u)),
                        BucketId = (ushort)(hash & BucketMask),
                        AgeSeconds = 0f,
                        LifetimeSeconds = math.max(1f, LifetimeSeconds * math.lerp(0.75f, 1.25f, normC)),
                        ScaleMeters = math.lerp(0.06f, 0.28f, normB),
                        Emission01 = math.saturate(PreyBiomass01 * 0.4f + normA * 0.2f),
                        Reserved = 0u
                    };
                    activated++;
                }
            }
        }

        [BurstCompile(FloatPrecision.Low, FloatMode.Fast, CompileSynchronously = false)]
        private struct AmbientBiotaDriftJob : IJobParallelFor
        {
            public NativeArray<AbsoluteUniversePosition> Aups;
            public NativeArray<float4> Velocities;
            public NativeArray<AmbientBiotaState> States;
            public AbsoluteUniversePosition CenterAup;
            public float3 FlowVector;
            public float DeltaTime;
            public float RadiusSq;
            public int ActiveBucket;
            public uint FrameIndex;

            public void Execute(int index)
            {
                AmbientBiotaState state = States[index];
                if ((state.StateFlags & AmbientBiotaState.FlagActive) == 0u)
                    return;

                if (((int)state.BucketId & BucketMask) != (ActiveBucket & BucketMask))
                    return;

                if (!math.isfinite(DeltaTime) || DeltaTime <= 0f)
                    return;

                float4 packedVelocity = Velocities[index];
                float3 velocity = packedVelocity.xyz;
                if (!math.all(math.isfinite(velocity)))
                    velocity = float3.zero;

                uint hash = Hash32(state.StableHash ^ (FrameIndex * 2246822519u));
                float3 brownian = new float3(
                    (((hash >> 0) & 255u) * (1f / 255f) - 0.5f) * 0.08f,
                    (((hash >> 8) & 255u) * (1f / 255f) - 0.5f) * 0.025f,
                    (((hash >> 16) & 255u) * (1f / 255f) - 0.5f) * 0.08f);

                float3 targetVelocity = FlowVector + brownian;
                if (!math.all(math.isfinite(targetVelocity)))
                    targetVelocity = brownian;

                float blend = math.saturate(DeltaTime * 0.35f);
                velocity = math.lerp(velocity, targetVelocity, blend);
                float3 deltaMeters = velocity * DeltaTime;
                if (!math.all(math.isfinite(deltaMeters)))
                    deltaMeters = float3.zero;

                AbsoluteUniversePosition nextAup = OffsetAup(Aups[index], deltaMeters);
                double3 deltaFromCenter = DeltaMeters(nextAup, CenterAup);
                double distSq = math.dot(deltaFromCenter, deltaFromCenter);

                state.AgeSeconds += DeltaTime;
                bool expired = state.AgeSeconds >= state.LifetimeSeconds;
                bool outside = !math.isfinite((float)distSq) || distSq > RadiusSq;
                if (expired || outside)
                {
                    state.StateFlags = 0u;
                    state.AgeSeconds = 0f;
                    Velocities[index] = default;
                    States[index] = state;
                    return;
                }

                Aups[index] = nextAup;
                Velocities[index] = new float4(velocity, packedVelocity.w);
                States[index] = state;
            }
        }

        private static AbsoluteUniversePosition OffsetAup(in AbsoluteUniversePosition origin, float3 deltaMeters)
        {
            return new AbsoluteUniversePosition
            {
                GridX = origin.GridX,
                GridY = origin.GridY,
                GridZ = origin.GridZ,
                LocalX = origin.LocalX,
                LocalY = origin.LocalY,
                LocalZ = origin.LocalZ
            }.WithOffset(deltaMeters);
        }

        private static byte ResolveSdfGuardedQualityTier(in AbsoluteUniversePosition centerAup, byte qualityTier)
        {
            if (qualityTier < 2)
                return qualityTier;

            return PassesSdfCavityGuard(in centerAup) ? qualityTier : (byte)0;
        }

        private static bool PassesSdfCavityGuard(in AbsoluteUniversePosition centerAup)
        {
            float3 absolutePosition = new float3(
                (float)(centerAup.GridX * (double)AupCellSizeMeters + centerAup.LocalX),
                (float)(centerAup.GridY * (double)AupCellSizeMeters + centerAup.LocalY),
                (float)(centerAup.GridZ * (double)AupCellSizeMeters + centerAup.LocalZ));
            if (!math.all(math.isfinite(absolutePosition)))
                return false;

            if (!HectonVoxelVolume.GetSDFDensity(absolutePosition, out float sdfDensity) || !math.isfinite(sdfDensity))
                return false;

            return sdfDensity < 0f;
        }

        private static double3 DeltaMeters(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            return new double3(
                ((a.GridX - b.GridX) * (double)AupCellSizeMeters) + (a.LocalX - b.LocalX),
                ((a.GridY - b.GridY) * (double)AupCellSizeMeters) + (a.LocalY - b.LocalY),
                ((a.GridZ - b.GridZ) * (double)AupCellSizeMeters) + (a.LocalZ - b.LocalZ));
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition aup)
        {
            return math.isfinite(aup.LocalX) &&
                   math.isfinite(aup.LocalY) &&
                   math.isfinite(aup.LocalZ);
        }

        private static uint Hash32(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return value;
        }
    }

    internal static class AmbientBiotaAupExtensions
    {
        private const double CellSizeMeters = 5000.0d;

        public static AbsoluteUniversePosition WithOffset(this AbsoluteUniversePosition origin, float3 deltaMeters)
        {
            double localX = origin.LocalX + deltaMeters.x;
            double localY = origin.LocalY + deltaMeters.y;
            double localZ = origin.LocalZ + deltaMeters.z;

            long shiftX = (long)math.floor(localX / CellSizeMeters);
            long shiftY = (long)math.floor(localY / CellSizeMeters);
            long shiftZ = (long)math.floor(localZ / CellSizeMeters);

            localX -= shiftX * CellSizeMeters;
            localY -= shiftY * CellSizeMeters;
            localZ -= shiftZ * CellSizeMeters;

            return new AbsoluteUniversePosition
            {
                GridX = origin.GridX + shiftX,
                GridY = origin.GridY + shiftY,
                GridZ = origin.GridZ + shiftZ,
                LocalX = (float)localX,
                LocalY = (float)localY,
                LocalZ = (float)localZ
            };
        }
    }
}
