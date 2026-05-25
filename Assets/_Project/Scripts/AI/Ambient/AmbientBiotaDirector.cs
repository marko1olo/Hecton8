using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Hecton8.AI.Ambient
{
    [DisallowMultipleComponent]
    public sealed class AmbientBiotaDirector : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IAmbientBiotaService, IGlobalRegistryHotSwapListener
    {
        private static int s_x001AmbientBiotaDirectorSignalPushDropCount;
        private const float AupCellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersFloat;
        private const double AupCellSizeMetersDouble = HectonPhysicsContract.AupSectorSizeMetersDouble;
        private const float TwoPi = 6.28318530718f;
        private const float Pi = 3.14159265359f;
        private const float HalfPi = 1.57079632679f;
        private const float InvTwoPi = 0.15915494309f;
        private const uint BaseSeedSalt = 0x42494F54u; // BIOT
        private const uint MacroHydrationSeedSalt = 0x4D485944u; // MHYD
        private const uint DirectorSourceHash = 0x414D4249u; // AMBI
        private const int BucketMask = 15;
        private const int BlackBoxFrameCount = 300;
        private const int MacroHydrationCounterCount = 4;
        private const int MacroVisualBoidsPerBiomassUnit = 64;
        private const int MaxDebrisSignalsPerLateFrame = 16;
        private const int BiomeChangedSignalLaneCapacity = 64;
        private const int EntitySpawnSignalLaneCapacity = 128;
        private const int DebrisSpawnSignalLaneCapacity = 128;
        private const uint BiomeChangedSignalLaneHash = 0xBE8113A5u;
        private const uint EntitySpawnSignalLaneHash = 0x573BB0DDu;
        private const uint DebrisSpawnSignalLaneHash = 0x40D0075Du;
        private const float TelemetryDeltaTimeClampSeconds = 0.25f;
        private const float MacroHydrationStressCullThreshold01 = 0.7f;
        private const float DefaultFlowX = 0.08f;
        private const float DefaultFlowY = -0.01f;
        private const float DefaultFlowZ = 0.04f;
        private const float PrecisionHeadlightConeDot = 0.70f;
        private const float PrecisionAvoidanceMetersPerSecond = 0.42f;
        private const ushort TelemetryFlagFaultSanitized = 1 << 0;
        private const ushort TelemetryFlagSurvivalPressure = 1 << 1;
        private const ushort TelemetryFlagVisualOverkill = 1 << 2;
        private const ushort TelemetryFlagPendingDebris = 1 << 3;
        private const byte EntitySpawnMinimumQualityVisualFlag = 1 << 1;
        private const byte EntitySpawnVisualOverkillFlag = 1 << 3;
        private const uint AmbientStateMinimumQualityBillboardFlag = 1u << 1;
        private const uint AmbientStateVisualOverkillReactiveFlag = 1u << 4;
        private const string AgentDumpFileName = "Dump_AMBIENT_BIOTA_DIRECTOR.bin";
        private static readonly int BiotaInstancesShaderId = Shader.PropertyToID("_HectonBiotaInstances");
        private static readonly int BiotaCapacityShaderId = Shader.PropertyToID("_HectonBiotaCapacity");
        private static readonly int BiotaActiveCountShaderId = Shader.PropertyToID("_HectonBiotaActiveCount");
        private static readonly int BiotaBiomeHashShaderId = Shader.PropertyToID("_HectonBiotaBiomeHash");
        private static readonly int BiotaQualityWeightShaderId = Shader.PropertyToID("_HectonBiotaQualityProfile");
        private static readonly int BiotaSystemStressShaderId = Shader.PropertyToID("_HectonBiotaSystemStress01");
        private static readonly int BiotaFlowVectorShaderId = Shader.PropertyToID("_HectonBiotaFlowVector");
        private static readonly int BiotaOverkillShaderId = Shader.PropertyToID("_HectonBiotaOverkill01");
        private static readonly int BiotaVisualTimeShaderId = Shader.PropertyToID("_HectonBiotaVisualTime");
        private static readonly int BiotaOriginWsShaderId = Shader.PropertyToID("_HectonBiotaOriginWS");

        [Header("Biota Capacity")]
        [FormerlySerializedAs("lowTierCapacity")]
        [SerializeField, Tooltip("Minimum survival ambient biota slots requested from the GlobalDataVault."), Min(128)] private int survivalCapacity = 2048;
        [FormerlySerializedAs("highTierCapacity")]
        [SerializeField, Tooltip("Precision ambient biota slots requested from the GlobalDataVault before ultra overdraw."), Min(128)] private int precisionCapacity = 8192;
        [SerializeField, Tooltip("Maximum dead slots the Burst spawn job may reactivate per slow tick."), Min(1)] private int spawnBudgetPerSlowTick = 64;
        [SerializeField, Tooltip("Nominal AUP bubble radius in meters before stress and quality clamps."), Min(8f)] private float simulationRadiusMeters = 100f;
        [SerializeField, Tooltip("Biota lifetime in seconds before deterministic culling and organic debris signaling."), Min(1f)] private float lifetimeSeconds = 45f;
        [SerializeField, Tooltip("Base deterministic species id used to derive biome-biased ambient biota variants.")] private ushort baseSpeciesId = 16;

        [Header("Biota Presentation")]
        [SerializeField, Tooltip("Enables the Graphics.RenderMeshIndirect presentation path for active biota slots.")] private bool enableIndirectDraw = true;
        [SerializeField, Tooltip("Optional assigned quad mesh. If empty, a cold fallback quad is created on enable.")] private Mesh biotaQuadMesh;
        [SerializeField, Tooltip("Material using the Hecton ambient biota indirect shader and GPU instance buffer.")] private Material biotaMaterial;
        [SerializeField, Tooltip("Unity render layer used by the indirect biota draw.")] private int renderLayer;
        [SerializeField, Tooltip("Shadow mode for indirect ambient biota. Defaults off because sub-meter translucent biota should not cast dynamic shadows.")] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;

        private IDataVault _vault;
        private IEcosystemDirectorService _ecosystem;
        private ISimulationBucketer _bucketer;
        private IAbyssalFlowVolumeReadModel _abyssalFlowReadModel;
        private VaultGenerationHandle<AbsoluteUniversePosition> _biotaAupHandle;
        private VaultGenerationHandle<float4> _biotaVelocityHandle;
        private VaultGenerationHandle<AmbientBiotaState> _biotaStateHandle;
        private VaultGenerationHandle<int> _macroHydrationCounterHandle;
        private VaultGenerationHandle<AmbientBiotaTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private GraphicsBuffer _gpuInstanceBufferA;
        private GraphicsBuffer _gpuInstanceBufferB;
        private GraphicsBuffer _indirectArgsBufferA;
        private GraphicsBuffer _indirectArgsBufferB;
        private Mesh _runtimeFallbackMesh;
        private Mesh _indirectArgsMesh;
        private JobHandle _activeJobHandle;
        private AbsoluteUniversePosition _lastPlayerAup;
        private float3 _lastPlayerRuntimePosition;
        private float3 _lastPlayerForward = new float3(0f, 0f, 1f);
        private float3 _flowVector = new float3(DefaultFlowX, DefaultFlowY, DefaultFlowZ);
        private float _cachedQualityWeight01 = 1f;
        private float _visualOverkillWeight01;
        private float _cachedSystemStress01;
        private int _capacity;
        private int _activeBiotaCount;
        private int _previousActiveBiotaCount;
        private int _lastCulledCount;
        private int _tickCount;
        private float _cullRatePerSecond;
        private float _telemetryClockSeconds;
        private float _lastRecountClockSeconds;
        private uint _frameIndex;
        private uint _heartbeatFrameIndex;
        private uint _lastStateHash = 2166136261u;
        private uint _currentBiomeHash;
        private uint _previousBiomeHash;
        private ushort _lastTelemetryFlags;
        private int _gpuBufferCapacity;
        private int _gpuBufferIndex;
        private int _indirectArgsBufferIndex;
        private int _indirectArgsCapacity = -1;
        private bool _jobPending;
        private bool _pendingDebrisDrainActive;
        private bool _blackBoxDumped;
        private bool _gpuPayloadDirty = true;
        private bool _serviceRegistered;
        private bool _tickRegistered;
        private bool _slowTickRegistered;
        private bool _lateFrameRegistered;
        private bool _hotSwapListenerRegistered;

        public bool IsInitialized => _capacity > 0 &&
                                     IsHandleCreated(in _biotaAupHandle) &&
                                     IsHandleCreated(in _biotaVelocityHandle) &&
                                     IsHandleCreated(in _biotaStateHandle);

        public int TickCount => _tickCount;

        public int Capacity => _capacity;

        public int ActiveBiotaCount => _activeBiotaCount;

        public float CullRatePerSecond => _cullRatePerSecond;

        public NativeArray<AbsoluteUniversePosition>.ReadOnly BiotaAups =>
            TryResolveBiotaAupBuffer(out NativeArray<AbsoluteUniversePosition> aups) ? aups.AsReadOnly() : default;

        public NativeArray<float4>.ReadOnly BiotaVelocities =>
            TryResolveBiotaVelocityBuffer(out NativeArray<float4> velocities) ? velocities.AsReadOnly() : default;

        public NativeArray<AmbientBiotaState>.ReadOnly BiotaStates =>
            TryResolveBiotaStateBuffer(out NativeArray<AmbientBiotaState> states) ? states.AsReadOnly() : default;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            CacheDependencies();
            RefreshQualityPolicy();
            EnsureVaultBuffers();
            EnsureFallbackDrawMeshReady();
            EnsureSignalLanesReady();
            TryRegisterHotSwapListener();
            RegisterRuntime();
        }

        private void OnDisable()
        {
            CompleteActiveJobForTeardown();
            UnregisterRuntime();
            TryUnregisterHotSwapListener();
            ReleaseGraphicsResources();
            ClearVaultHandles();
            _vault = null;
            _ecosystem = null;
            _bucketer = null;
            _abyssalFlowReadModel = null;
            _capacity = 0;
            _activeBiotaCount = 0;
            _previousActiveBiotaCount = 0;
            _lastCulledCount = 0;
            _cullRatePerSecond = 0f;
            _telemetryClockSeconds = 0f;
            _lastRecountClockSeconds = 0f;
            _heartbeatFrameIndex = 0u;
            _cachedSystemStress01 = 0f;
            _pendingDebrisDrainActive = false;
        }

        public void Tick(float deltaTime)
        {
            _tickCount++;

            float safeDeltaTime = math.isfinite(deltaTime) && deltaTime > 0f
                ? math.min(deltaTime, 0.05f)
                : 0f;
            float telemetryDeltaTime = math.isfinite(deltaTime) && deltaTime > 0f
                ? math.min(deltaTime, TelemetryDeltaTimeClampSeconds)
                : 0f;
            if (!math.isfinite(_telemetryClockSeconds))
                _telemetryClockSeconds = 0f;
            _telemetryClockSeconds += telemetryDeltaTime;

            if (_jobPending || !TryResolveBiotaBuffers(out NativeArray<AbsoluteUniversePosition> aups, out NativeArray<float4> velocities, out NativeArray<AmbientBiotaState> states))
                return;

            if (safeDeltaTime <= 0f)
                return;

            if (!TryCapturePlayerPose(out PlayerRuntimePoseSnapshot pose))
                return;

            _lastPlayerAup = pose.Aup;
            _lastPlayerRuntimePosition = SanitizeRuntimePosition(pose.RuntimePosition, _lastPlayerRuntimePosition);
            _lastPlayerForward = SanitizeForward(pose.Forward, _lastPlayerForward);

            int activeBucket = ResolveActiveBucket();
            float radius = ResolveSimulationRadiusMeters();
            AmbientBiotaDriftJob driftJob = new AmbientBiotaDriftJob
            {
                Aups = aups,
                Velocities = velocities,
                States = states,
                CenterAup = _lastPlayerAup,
                PlayerForward = _lastPlayerForward,
                FlowVector = _flowVector,
                DeltaTime = safeDeltaTime,
                RadiusSq = (double)radius * radius,
                ActiveBucket = activeBucket,
                FrameIndex = _frameIndex,
                SurvivalPressure01 = ResolveSurvivalPressure01(),
                VisualOverkill01 = _visualOverkillWeight01,
                HeadlightConeDot = PrecisionHeadlightConeDot,
                AvoidanceMetersPerSecond = PrecisionAvoidanceMetersPerSecond
            };

            _activeJobHandle = driftJob.Schedule(_capacity, 64);
            _jobPending = true;
            _frameIndex++;
        }

        public void SlowTick()
        {
            RefreshQualityPolicy();

            if (_jobPending)
                return;

            EnsureVaultBuffers();

            if (!TryResolveBiotaBuffers(out NativeArray<AbsoluteUniversePosition> aups, out NativeArray<float4> velocities, out NativeArray<AmbientBiotaState> states))
                return;

            if (!TryCapturePlayerPose(out PlayerRuntimePoseSnapshot pose))
                return;

            RefreshBiomeSignalState();
            _lastPlayerAup = pose.Aup;
            _lastPlayerRuntimePosition = SanitizeRuntimePosition(pose.RuntimePosition, _lastPlayerRuntimePosition);
            _lastPlayerForward = SanitizeForward(pose.Forward, _lastPlayerForward);
            RefreshEcologyInputs(_lastPlayerRuntimePosition, out float preyBiomass01, out float carryingCapacity01);
            RefreshAbyssalFlow(_lastPlayerRuntimePosition);

            int targetActive = ResolveTargetActiveCount(preyBiomass01, carryingCapacity01);
            int spawnBudget = math.min(spawnBudgetPerSlowTick, math.max(0, targetActive - _activeBiotaCount));
            if (spawnBudget <= 0)
                return;

            float radius = ResolveSimulationRadiusMeters();
            AmbientBiotaSpawnJob spawnJob = new AmbientBiotaSpawnJob
            {
                Aups = aups,
                Velocities = velocities,
                States = states,
                CenterAup = _lastPlayerAup,
                PreyBiomass01 = preyBiomass01,
                CarryingCapacity01 = carryingCapacity01,
                RadiusMeters = radius,
                LifetimeSeconds = lifetimeSeconds,
                Capacity = _capacity,
                SpawnBudget = spawnBudget,
                BaseSpeciesId = baseSpeciesId,
                Seed = BaseSeedSalt,
                FrameIndex = _frameIndex,
                CurrentBiomeHash = _currentBiomeHash,
                SurvivalPressure01 = ResolveSurvivalPressure01(),
                VisualOverkill01 = _visualOverkillWeight01
            };

            _activeJobHandle = spawnJob.Schedule();
            _jobPending = true;
            _frameIndex++;
        }

        public void LateFrameTick()
        {
            bool completedJob = _jobPending && TryFinalizeActiveJobNoWait();
            if (_jobPending)
            {
                WriteTelemetryHeartbeat();
                return;
            }

            if (TryResolveBiotaBuffers(out NativeArray<AbsoluteUniversePosition> aups, out NativeArray<float4> velocities, out NativeArray<AmbientBiotaState> states))
            {
                bool debrisWasPending = _pendingDebrisDrainActive;
                if (completedJob || debrisWasPending)
                    PublishPendingDebrisSignals(aups, velocities, states);

                if (completedJob || debrisWasPending)
                    RecountActiveBiota(states);

                RenderIndirectBiota(aups, velocities, states, completedJob || debrisWasPending);
            }

            WriteTelemetryHeartbeat();
        }

        public bool TryHydrateMacroSwarms(
            in AbsoluteUniversePosition centerAup,
            ushort radiusMetersQ,
            NativeArray<MacroSwarm> swarms,
            int swarmCount,
            byte qualityByte,
            float systemStress01,
            out int spawnedBoidCount)
        {
            spawnedBoidCount = 0;
            if (!swarms.IsCreated || swarmCount <= 0)
                return false;

            if (_jobPending)
                return false;

            RefreshBiomeSignalState();
            if (!TryResolveBiotaBuffers(out NativeArray<AbsoluteUniversePosition> aups, out NativeArray<float4> velocities, out NativeArray<AmbientBiotaState> states) ||
                !TryResolveMacroCounters(out NativeArray<int> counters))
            {
                return false;
            }

            ClearMacroCounters(counters);
            int safeSwarmCount = math.min(swarmCount, swarms.Length);
            float radiusMeters = math.max(8f, radiusMetersQ);
            byte spawnQualityByte = ResolveMacroVisualQualityByte(in centerAup, qualityByte, systemStress01);
            float macroQualityWeight01 = math.saturate(spawnQualityByte * (1f / 3f));
            float macroSurvivalPressure01 = math.max(
                1f - SmoothStep01(macroQualityWeight01),
                SmoothStep01(math.saturate((systemStress01 - 0.62f) * math.rcp(0.38f))));
            AmbientBiotaMacroHydrationJob hydrationJob = new AmbientBiotaMacroHydrationJob
            {
                Aups = aups,
                Velocities = velocities,
                States = states,
                Swarms = swarms,
                Counters = counters,
                CenterAup = centerAup,
                RadiusMeters = radiusMeters,
                LifetimeSeconds = lifetimeSeconds,
                Capacity = _capacity,
                SwarmCount = safeSwarmCount,
                BaseSpeciesId = baseSpeciesId,
                Seed = MacroHydrationSeedSalt,
                FrameIndex = _frameIndex,
                CurrentBiomeHash = _currentBiomeHash,
                QualityWeight01 = macroQualityWeight01,
                SurvivalPressure01 = macroSurvivalPressure01,
                SystemStress01 = math.saturate(systemStress01)
            };

            hydrationJob.Execute();
            _frameIndex++;
            spawnedBoidCount = counters[0];
            if (spawnedBoidCount <= 0)
                return false;

            RecountActiveBiota(states);
            _gpuPayloadDirty = true;
            EntitySpawnSignal spawnSignal = new EntitySpawnSignal
            {
                PositionAup = centerAup,
                SourceHash = MacroHydrationSeedSalt,
                SpawnedCount = (ushort)math.clamp(spawnedBoidCount, 0, ushort.MaxValue),
                RequestedCount = (ushort)math.clamp(counters[1], 0, ushort.MaxValue),
                EntityKind = EntitySpawnSignal.KindEcology,
                QualityTier = spawnQualityByte,
                Flags = (byte)(EntitySpawnSignal.FlagEcology |
                               (macroSurvivalPressure01 >= 0.75f ? EntitySpawnMinimumQualityVisualFlag : 0) |
                               (macroQualityWeight01 >= 0.95f ? EntitySpawnVisualOverkillFlag : 0)),
                Frame = _frameIndex
            };
            SignalBus<EntitySpawnSignal>.TryPushTracked(in spawnSignal, ref s_x001AmbientBiotaDirectorSignalPushDropCount);
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

            if (_jobPending)
                return false;

            if (!TryResolveBiotaBuffers(out NativeArray<AbsoluteUniversePosition> aups, out NativeArray<float4> velocities, out NativeArray<AmbientBiotaState> states) ||
                !TryResolveMacroCounters(out NativeArray<int> counters))
            {
                return false;
            }

            ClearMacroCounters(counters);
            float radiusMeters = math.max(8f, radiusMetersQ);
            AmbientBiotaMacroDehydrationJob dehydrationJob = new AmbientBiotaMacroDehydrationJob
            {
                Aups = aups,
                Velocities = velocities,
                States = states,
                Counters = counters,
                CenterAup = centerAup,
                RadiusSq = (double)radiusMeters * radiusMeters,
                Capacity = _capacity
            };

            dehydrationJob.Execute();
            releasedBoidCount = counters[0];
            if (releasedBoidCount <= 0)
                return false;

            biomassValue = math.saturate(releasedBoidCount * math.rcp((float)MacroVisualBoidsPerBiomassUnit));
            RecountActiveBiota(states);
            _gpuPayloadDirty = true;
            return biomassValue > 0f;
        }

        private void CacheDependencies()
        {
            RefreshRegistryDependencies();
        }

        private void RefreshRegistryDependencies()
        {
            if (_vault == null)
            {
                _vault = GlobalRegistry.DataVault;
            }

            if (_ecosystem == null || !_ecosystem.IsInitialized)
                _ecosystem = GlobalRegistry.EcosystemDirector;

            if (_bucketer == null || !_bucketer.IsInitialized)
                _bucketer = GlobalRegistry.SimulationBucketer;

            if (_abyssalFlowReadModel == null)
                _abyssalFlowReadModel = GlobalRegistry.AbyssalFlowVolume;
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
            UnregisterDispatcherLanes();

            if (_serviceRegistered)
            {
                GlobalRegistry.UnregisterAmbientBiotaRuntime(this);
                _serviceRegistered = false;
            }
        }

        private void UnregisterDispatcherLanes()
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
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (currentService == null)
                    {
                        _tickRegistered = false;
                        _slowTickRegistered = false;
                        _lateFrameRegistered = false;
                        return;
                    }

                    if (isActiveAndEnabled)
                    {
                        UnregisterDispatcherLanes();
                        RegisterRuntime();
                    }
                    return;

                case GlobalRegistryServiceSlot.DataVault:
                    CompleteActiveJobForTeardown();
                    ClearVaultHandles();
                    _vault = currentService as IDataVault;
                    EnsureVaultBuffers();
                    _gpuPayloadDirty = true;
                    return;

                case GlobalRegistryServiceSlot.EcosystemDirector:
                    _ecosystem = currentService as IEcosystemDirectorService;
                    return;

                case GlobalRegistryServiceSlot.SimulationBucketerRuntime:
                    _bucketer = currentService as ISimulationBucketer;
                    return;

                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    _abyssalFlowReadModel = currentService as IAbyssalFlowVolumeReadModel;
                    return;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private bool EnsureVaultBuffers()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return false;

            int desiredCapacity = ResolveCapacity();
            bool capacityChanged = _capacity != desiredCapacity;
            if (!IsHandleCreated(in _biotaAupHandle) || capacityChanged)
            {
                _biotaAupHandle = ClaimVaultBuffer<AbsoluteUniversePosition>(
                    vault,
                    BufferID.BiotaAUPs,
                    desiredCapacity,
                    NativeArrayOptions.ClearMemory);
            }

            if (!IsHandleCreated(in _biotaVelocityHandle) || capacityChanged)
            {
                _biotaVelocityHandle = ClaimVaultBuffer<float4>(
                    vault,
                    BufferID.BiotaVelocities,
                    desiredCapacity,
                    NativeArrayOptions.ClearMemory);
            }

            if (!IsHandleCreated(in _biotaStateHandle) || capacityChanged)
            {
                _biotaStateHandle = ClaimVaultBuffer<AmbientBiotaState>(
                    vault,
                    BufferID.BiotaStates,
                    desiredCapacity,
                    NativeArrayOptions.ClearMemory);
            }

            if (!IsHandleCreated(in _macroHydrationCounterHandle))
            {
                _macroHydrationCounterHandle = ClaimVaultBuffer<int>(
                    vault,
                    BufferID.BiotaMacroHydrationCounters,
                    MacroHydrationCounterCount,
                    NativeArrayOptions.ClearMemory);
            }

            if (!IsHandleCreated(in _telemetryRingHandle))
            {
                _telemetryRingHandle = ClaimVaultBuffer<AmbientBiotaTelemetryEntry>(
                    vault,
                    BufferID.BiotaTelemetryRing,
                    BlackBoxFrameCount,
                    NativeArrayOptions.ClearMemory);
            }

            if (!IsHandleCreated(in _telemetryCursorHandle))
            {
                _telemetryCursorHandle = ClaimVaultBuffer<int>(
                    vault,
                    BufferID.BiotaTelemetryCursor,
                    1,
                    NativeArrayOptions.ClearMemory);
            }

            bool ready = IsHandleCreated(in _biotaAupHandle) &&
                         IsHandleCreated(in _biotaVelocityHandle) &&
                         IsHandleCreated(in _biotaStateHandle) &&
                         IsHandleCreated(in _macroHydrationCounterHandle) &&
                         IsHandleCreated(in _telemetryRingHandle) &&
                         IsHandleCreated(in _telemetryCursorHandle) &&
                         TryResolveBiotaBuffers(desiredCapacity, out _, out _, out _) &&
                         TryResolveMacroCounters(out _) &&
                         TryResolveTelemetryBuffers(out _, out _);
            if (!ready)
                return false;

            if (capacityChanged)
            {
                ResetCapacityDependentRuntimeState();
            }

            _capacity = desiredCapacity;
            return true;
        }

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u;
        }

        private static VaultGenerationHandle<T> ClaimVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int length,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null)
            {
                return default;
            }

            if (vault.IsAllocationLocked)
            {
                return vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> existing)
                    ? existing
                    : default;
            }

            return vault.EnsureGenerationHandle<T>(bufferId, length, SystemID.AmbientBiota, options);
        }

        private bool TryOpenVaultView<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _vault;
            return vault != null &&
                   handle.BufferID != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private void ResetCapacityDependentRuntimeState()
        {
            _activeBiotaCount = 0;
            _previousActiveBiotaCount = 0;
            _lastCulledCount = 0;
            _cullRatePerSecond = 0f;
            _lastRecountClockSeconds = 0f;
            _lastStateHash = 2166136261u;
            _pendingDebrisDrainActive = false;
            _gpuPayloadDirty = true;
            _indirectArgsCapacity = -1;
        }

        private bool TryResolveBiotaBuffers(
            out NativeArray<AbsoluteUniversePosition> aups,
            out NativeArray<float4> velocities,
            out NativeArray<AmbientBiotaState> states)
        {
            aups = default;
            velocities = default;
            states = default;
            return TryResolveBiotaBuffers(_capacity, out aups, out velocities, out states);
        }

        private bool TryResolveBiotaBuffers(
            int requiredCapacity,
            out NativeArray<AbsoluteUniversePosition> aups,
            out NativeArray<float4> velocities,
            out NativeArray<AmbientBiotaState> states)
        {
            aups = default;
            velocities = default;
            states = default;
            return TryResolveBiotaAupBuffer(requiredCapacity, out aups) &&
                   TryResolveBiotaVelocityBuffer(requiredCapacity, out velocities) &&
                   TryResolveBiotaStateBuffer(requiredCapacity, out states);
        }

        private bool TryResolveBiotaAupBuffer(out NativeArray<AbsoluteUniversePosition> aups)
        {
            return TryResolveBiotaAupBuffer(_capacity, out aups);
        }

        private bool TryResolveBiotaAupBuffer(int requiredCapacity, out NativeArray<AbsoluteUniversePosition> aups)
        {
            return TryOpenVaultView(in _biotaAupHandle, requiredCapacity, out aups);
        }

        private bool TryResolveBiotaVelocityBuffer(out NativeArray<float4> velocities)
        {
            return TryResolveBiotaVelocityBuffer(_capacity, out velocities);
        }

        private bool TryResolveBiotaVelocityBuffer(int requiredCapacity, out NativeArray<float4> velocities)
        {
            return TryOpenVaultView(in _biotaVelocityHandle, requiredCapacity, out velocities);
        }

        private bool TryResolveBiotaStateBuffer(out NativeArray<AmbientBiotaState> states)
        {
            return TryResolveBiotaStateBuffer(_capacity, out states);
        }

        private bool TryResolveBiotaStateBuffer(int requiredCapacity, out NativeArray<AmbientBiotaState> states)
        {
            return TryOpenVaultView(in _biotaStateHandle, requiredCapacity, out states);
        }

        private bool TryResolveMacroCounters(out NativeArray<int> counters)
        {
            return TryOpenVaultView(in _macroHydrationCounterHandle, MacroHydrationCounterCount, out counters);
        }

        private bool TryResolveTelemetryBuffers(
            out NativeArray<AmbientBiotaTelemetryEntry> telemetryRing,
            out NativeArray<int> telemetryCursor)
        {
            telemetryRing = default;
            telemetryCursor = default;
            return TryOpenVaultView(in _telemetryRingHandle, BlackBoxFrameCount, out telemetryRing) &&
                   TryOpenVaultView(in _telemetryCursorHandle, 1, out telemetryCursor);
        }

        private void ClearVaultHandles()
        {
            _biotaAupHandle = default;
            _biotaVelocityHandle = default;
            _biotaStateHandle = default;
            _macroHydrationCounterHandle = default;
            _telemetryRingHandle = default;
            _telemetryCursorHandle = default;
        }

        private void ClearMacroCounters(NativeArray<int> counters)
        {
            int length = math.min(counters.Length, MacroHydrationCounterCount);
            for (int i = 0; i < length; i++)
                counters[i] = 0;
        }

        private void RefreshBiomeSignalState()
        {
            ReadOnlySpan<BiomeChangedSignal> biomeSignals = SignalBus<BiomeChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < biomeSignals.Length; i++)
            {
                BiomeChangedSignal signal = biomeSignals[i];
                if (signal.CurrentBiomeHash == 0u)
                    continue;

                _previousBiomeHash = signal.PreviousBiomeHash;
                _currentBiomeHash = signal.CurrentBiomeHash;
            }
        }

        private int ResolveCapacity()
        {
            float qualityCurve = SmoothStep01(_cachedQualityWeight01);
            float ultraCurve = SmoothStep01(math.saturate((_cachedQualityWeight01 - 0.82f) * math.rcp(0.18f)));
            float requested = math.lerp(survivalCapacity, precisionCapacity, qualityCurve) + precisionCapacity * ultraCurve;
            return math.clamp((int)math.round(requested), 128, 32768);
        }

        private int ResolveTargetActiveCount(float preyBiomass01, float carryingCapacity01)
        {
            float biomass = math.saturate(math.min(preyBiomass01, carryingCapacity01));
            float scalar = math.lerp(0.35f, 0.92f, SmoothStep01(_cachedQualityWeight01));
            scalar *= math.lerp(1f, 0.45f, ResolveSurvivalPressure01());

            return math.clamp((int)math.round(_capacity * biomass * scalar), 0, _capacity);
        }

        private float ResolveSimulationRadiusMeters()
        {
            float radius = math.max(8f, simulationRadiusMeters);
            float survivalPressure01 = ResolveSurvivalPressure01();
            radius = math.lerp(radius, math.min(radius, 30f), survivalPressure01);
            radius = math.min(radius * math.lerp(1f, 1.35f, _visualOverkillWeight01 * (1f - survivalPressure01)), 220f);

            return radius;
        }

        private int ResolveActiveBucket()
        {
            if (_bucketer != null && _bucketer.IsInitialized)
                return _bucketer.ActiveSlowBucket & BucketMask;

            return (int)(_frameIndex & BucketMask);
        }

        private float ResolveSurvivalPressure01()
        {
            float qualityPressure01 = 1f - SmoothStep01(_cachedQualityWeight01);
            float stressPressure01 = SmoothStep01(math.saturate((_cachedSystemStress01 - 0.62f) * math.rcp(0.38f)));
            return math.saturate(math.max(qualityPressure01, stressPressure01));
        }

        private static float SmoothStep01(float value)
        {
            float saturated = math.saturate(value);
            return saturated * saturated * (3f - 2f * saturated);
        }

        private static float SinPolynomial7(float angle)
        {
            float x = angle - TwoPi * math.floor((angle + Pi) * InvTwoPi);
            x = math.select(x, Pi - x, x > HalfPi);
            x = math.select(x, -Pi - x, x < -HalfPi);
            float x2 = x * x;
            return x * (1f + x2 * (-0.16666667f + x2 * (0.008333331f + x2 * -0.000198409f)));
        }

        private static float CosPolynomial7(float angle)
        {
            return SinPolynomial7(angle + HalfPi);
        }

        private bool TryCapturePlayerPose(out PlayerRuntimePoseSnapshot pose)
        {
            if (!PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) ||
                runtimeContext == null ||
                !runtimeContext.IsBound)
            {
                pose = default;
                return false;
            }

            PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
            if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !math.all(math.isfinite(movementState.WorldPosition)) ||
                !IsFiniteAup(in movementState.PredictedAup))
            {
                pose = default;
                return false;
            }

            float3 fallbackForward = SanitizeForward(movementState.Forward, new float3(0f, 0f, 1f));
            float3 cameraForward = SanitizeForward(movementState.CameraForward, fallbackForward);
            pose = new PlayerRuntimePoseSnapshot(
                movementState.WorldPosition,
                cameraForward,
                movementState.PredictedAup,
                movementState.Flags);
            return true;
        }

        private void RefreshQualityPolicy()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            _cachedQualityWeight01 = math.saturate(math.select(_cachedQualityWeight01, quality, math.isfinite(quality)));
            float systemStress01 = SignalBusRegistry.SystemStress01;
            _cachedSystemStress01 = math.select(0f, math.saturate(systemStress01), math.isfinite(systemStress01));
            float overkillQuality01 = SmoothStep01(math.saturate((_cachedQualityWeight01 - 0.55f) * math.rcp(0.45f)));
            float stressSuppression01 = SmoothStep01(math.saturate((_cachedSystemStress01 - 0.35f) * math.rcp(0.65f)));
            _visualOverkillWeight01 = math.saturate(overkillQuality01 * (1f - stressSuppression01));
        }

        private void RefreshEcologyInputs(float3 runtimePosition, out float preyBiomass01, out float carryingCapacity01)
        {
            preyBiomass01 = 0.35f;
            carryingCapacity01 = 0.5f;
            IEcosystemDirectorService ecosystem = _ecosystem;
            if (ecosystem == null || !math.all(math.isfinite(runtimePosition)))
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
            if (!math.all(math.isfinite(runtimePosition)))
            {
                _flowVector = new float3(DefaultFlowX, DefaultFlowY, DefaultFlowZ);
                return;
            }

            IAbyssalFlowVolumeReadModel flowReadModel = _abyssalFlowReadModel;
            if (flowReadModel != null)
            {
                Vector3 position = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                if (flowReadModel.TrySampleAbyssalFlow(position, out Vector3 flow) &&
                    math.isfinite(flow.x) &&
                    math.isfinite(flow.y) &&
                    math.isfinite(flow.z))
                {
                    _flowVector = new float3(flow.x, flow.y, flow.z);
                    return;
                }
            }

            _flowVector = new float3(DefaultFlowX, DefaultFlowY, DefaultFlowZ);
        }

        private void RenderIndirectBiota(
            NativeArray<AbsoluteUniversePosition> aups,
            NativeArray<float4> velocities,
            NativeArray<AmbientBiotaState> states,
            bool payloadDirty)
        {
            if (!enableIndirectDraw || _capacity <= 0 || _activeBiotaCount <= 0)
                return;

            Material material = biotaMaterial;
            if (material == null || !TryResolveDrawMesh(out Mesh mesh) || !EnsureGraphicsResources(_capacity))
                return;

            if ((_gpuPayloadDirty || payloadDirty) && !UploadGpuPayload(aups, velocities, states, _capacity))
                return;

            if (!TryResolveGpuReadBuffer(out GraphicsBuffer instanceBuffer) ||
                !UploadIndirectArgs(mesh, _capacity, out GraphicsBuffer indirectArgsBuffer))
            {
                return;
            }

            material.SetBuffer(BiotaInstancesShaderId, instanceBuffer);
            material.SetInt(BiotaCapacityShaderId, _capacity);
            material.SetInt(BiotaActiveCountShaderId, _activeBiotaCount);
            material.SetFloat(BiotaBiomeHashShaderId, (float)_currentBiomeHash);
            material.SetFloat(BiotaQualityWeightShaderId, _cachedQualityWeight01);
            material.SetFloat(BiotaSystemStressShaderId, _cachedSystemStress01);
            material.SetVector(BiotaFlowVectorShaderId, new Vector4(_flowVector.x, _flowVector.y, _flowVector.z, 0f));
            material.SetFloat(BiotaOverkillShaderId, _visualOverkillWeight01);
            material.SetFloat(BiotaVisualTimeShaderId, _telemetryClockSeconds);
            material.SetVector(BiotaOriginWsShaderId, new Vector4(_lastPlayerRuntimePosition.x, _lastPlayerRuntimePosition.y, _lastPlayerRuntimePosition.z, 1f));

            float radius = ResolveSimulationRadiusMeters();
            Bounds drawBounds = new Bounds(
                new Vector3(_lastPlayerRuntimePosition.x, _lastPlayerRuntimePosition.y, _lastPlayerRuntimePosition.z),
                Vector3.one * radius * 2.5f);
            RenderParams renderParams = new RenderParams(material)
            {
                worldBounds = drawBounds,
                layer = renderLayer,
                shadowCastingMode = shadowCastingMode,
                receiveShadows = false,
                motionVectorMode = MotionVectorGenerationMode.Object
            };
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, mesh, indirectArgsBuffer, 1, 0);
        }

        private bool EnsureGraphicsResources(int capacity)
        {
            if (capacity <= 0)
                return false;

            if (_gpuBufferCapacity >= capacity && AreGraphicsBuffersValid(capacity))
                return AreIndirectArgsBuffersValid();

            ReleaseGraphicsResources();
            _gpuInstanceBufferA = CreateStructuredBuffer<AmbientBiotaGpuInstance>(capacity); // COLD ALLOC: GraphicsBuffer[BiotaInstances A] - double-buffered packed ambient biota render payload - owner: AMBIENT_BIOTA_DIRECTOR
            _gpuInstanceBufferB = CreateStructuredBuffer<AmbientBiotaGpuInstance>(capacity); // COLD ALLOC: GraphicsBuffer[BiotaInstances B] - double-buffered packed ambient biota render payload - owner: AMBIENT_BIOTA_DIRECTOR
            _indirectArgsBufferA = CreateIndirectArgsBuffer(); // COLD ALLOC: GraphicsBuffer[IndirectArgs A] - double-buffered locked ambient biota indirect draw args - owner: AMBIENT_BIOTA_DIRECTOR
            _indirectArgsBufferB = CreateIndirectArgsBuffer(); // COLD ALLOC: GraphicsBuffer[IndirectArgs B] - double-buffered locked ambient biota indirect draw args - owner: AMBIENT_BIOTA_DIRECTOR
            _gpuBufferCapacity = capacity;
            _gpuBufferIndex = 0;
            _indirectArgsBufferIndex = 0;
            _gpuPayloadDirty = true;
            _indirectArgsMesh = null;
            _indirectArgsCapacity = -1;
            return AreGraphicsBuffersValid(capacity) &&
                   AreIndirectArgsBuffersValid();
        }

        private static GraphicsBuffer CreateIndirectArgsBuffer()
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);
        }

        private bool AreGraphicsBuffersValid(int capacity)
        {
            return IsValidBuffer(_gpuInstanceBufferA, capacity) &&
                   IsValidBuffer(_gpuInstanceBufferB, capacity);
        }

        private static bool IsValidBuffer(GraphicsBuffer buffer, int capacity)
        {
            return buffer != null &&
                   buffer.IsValid() &&
                   buffer.count >= capacity;
        }

        private bool AreIndirectArgsBuffersValid()
        {
            return IsValidIndirectArgsBuffer(_indirectArgsBufferA) &&
                   IsValidIndirectArgsBuffer(_indirectArgsBufferB);
        }

        private static bool IsValidIndirectArgsBuffer(GraphicsBuffer buffer)
        {
            return buffer != null &&
                   buffer.IsValid() &&
                   buffer.count >= 1;
        }

        private bool UploadGpuPayload(
            NativeArray<AbsoluteUniversePosition> aups,
            NativeArray<float4> velocities,
            NativeArray<AmbientBiotaState> states,
            int count)
        {
            int writeIndex = 1 - _gpuBufferIndex;
            if (!TryResolveGpuBuffer(writeIndex, out GraphicsBuffer instanceBuffer))
                return false;

            bool uploaded = UploadPackedGpuInstances(instanceBuffer, aups, velocities, states, count);
            if (!uploaded)
                return false;

            _gpuBufferIndex = writeIndex;
            _gpuPayloadDirty = false;
            return true;
        }

        private bool TryResolveGpuReadBuffer(out GraphicsBuffer instanceBuffer)
        {
            return TryResolveGpuBuffer(_gpuBufferIndex, out instanceBuffer);
        }

        private bool TryResolveGpuBuffer(int index, out GraphicsBuffer instanceBuffer)
        {
            bool first = (index & 1) == 0;
            instanceBuffer = first ? _gpuInstanceBufferA : _gpuInstanceBufferB;
            return IsValidBuffer(instanceBuffer, _capacity);
        }

        private bool UploadIndirectArgs(Mesh mesh, int capacity, out GraphicsBuffer argsBuffer)
        {
            argsBuffer = null;
            if (mesh == null || capacity <= 0 || mesh.subMeshCount <= 0 || !AreIndirectArgsBuffersValid())
            {
                return false;
            }

            if (ReferenceEquals(_indirectArgsMesh, mesh) && _indirectArgsCapacity == capacity)
                return TryResolveIndirectArgsReadBuffer(out argsBuffer);

            uint indexCount = mesh.GetIndexCount(0);
            if (indexCount == 0u)
                return false;

            uint startIndex = mesh.GetIndexStart(0);
            uint baseVertexIndex = (uint)math.max(0, mesh.GetBaseVertex(0));
            int writeIndex = 1 - _indirectArgsBufferIndex;
            if (!TryResolveIndirectArgsBuffer(writeIndex, out GraphicsBuffer writeBuffer))
                return false;

            NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> argsWrite =
                writeBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            try
            {
                argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = indexCount,
                    instanceCount = (uint)capacity,
                    startIndex = startIndex,
                    baseVertexIndex = baseVertexIndex,
                    startInstance = 0u
                };
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
            }
            _indirectArgsBufferIndex = writeIndex;
            _indirectArgsMesh = mesh;
            _indirectArgsCapacity = capacity;
            argsBuffer = writeBuffer;
            return true;
        }

        private bool TryResolveIndirectArgsReadBuffer(out GraphicsBuffer argsBuffer)
        {
            return TryResolveIndirectArgsBuffer(_indirectArgsBufferIndex, out argsBuffer);
        }

        private bool TryResolveIndirectArgsBuffer(int index, out GraphicsBuffer argsBuffer)
        {
            argsBuffer = ((index & 1) == 0) ? _indirectArgsBufferA : _indirectArgsBufferB;
            return IsValidIndirectArgsBuffer(argsBuffer);
        }

        private bool UploadPackedGpuInstances(
            GraphicsBuffer destination,
            NativeArray<AbsoluteUniversePosition> aups,
            NativeArray<float4> velocities,
            NativeArray<AmbientBiotaState> states,
            int count)
        {
            int sourceLength = math.min(aups.IsCreated ? aups.Length : 0, math.min(velocities.IsCreated ? velocities.Length : 0, states.IsCreated ? states.Length : 0));
            int safeCount = ResolveSafeWriteCount(destination, sourceLength, count, UnsafeUtility.SizeOf<AmbientBiotaGpuInstance>());
            if (safeCount <= 0)
                return false;

            var mapped = destination.LockBufferForWrite<AmbientBiotaGpuInstance>(0, safeCount);
            try
            {
                for (int i = 0; i < safeCount; i++)
                {
                    AbsoluteUniversePosition aup = aups[i];
                    AmbientBiotaState state = states[i];
                    mapped[i] = BuildGpuInstance(in aup, velocities[i], in state, in _lastPlayerAup);
                }
            }
            finally
            {
                destination.UnlockBufferAfterWrite<AmbientBiotaGpuInstance>(safeCount);
            }
            return true;
        }

        private static int ResolveSafeWriteCount(GraphicsBuffer destination, int sourceLength, int requestedCount, int stride)
        {
            if (destination == null || requestedCount <= 0 || sourceLength <= 0 || destination.count <= 0)
                return 0;

            if (destination.stride != stride)
                return 0;

            return math.min(math.min(requestedCount, sourceLength), destination.count);
        }

        private static AmbientBiotaGpuInstance BuildGpuInstance(
            in AbsoluteUniversePosition aup,
            float4 velocity,
            in AmbientBiotaState state,
            in AbsoluteUniversePosition centerAup)
        {
            double3 deltaMeters = DeltaMeters(in aup, in centerAup);
            bool deltaFinite = IsFinite(deltaMeters) &&
                               math.abs(deltaMeters.x) <= 10000.0d &&
                               math.abs(deltaMeters.y) <= 10000.0d &&
                               math.abs(deltaMeters.z) <= 10000.0d;
            bool velocityFinite = math.all(math.isfinite(velocity));
            bool stateFinite = math.isfinite(state.AgeSeconds) &&
                               math.isfinite(state.LifetimeSeconds) &&
                               math.isfinite(state.ScaleMeters) &&
                               math.isfinite(state.Emission01);
            bool active = (state.StateFlags & AmbientBiotaState.FlagActive) != 0u &&
                          deltaFinite &&
                          velocityFinite &&
                          stateFinite &&
                          state.ScaleMeters > 0f;
            float3 localMeters = active
                ? new float3((float)deltaMeters.x, (float)deltaMeters.y, (float)deltaMeters.z)
                : float3.zero;
            float4 safeVelocity = velocityFinite ? velocity : float4.zero;
            float safeLifetime = math.max(0.001f, math.select(1f, state.LifetimeSeconds, math.isfinite(state.LifetimeSeconds)));
            float safeAge = math.select(0f, state.AgeSeconds, math.isfinite(state.AgeSeconds));
            float age01 = math.saturate(safeAge * math.rcp(safeLifetime));
            float hash01 = (state.StableHash & 0xFFFFu) * (1f / 65535f);
            uint activeFlags = active ? state.StateFlags : 0u;

            return new AmbientBiotaGpuInstance
            {
                PositionScale = new float4(localMeters, active ? math.max(0.001f, state.ScaleMeters) : 0f),
                VelocityEmission = new float4(safeVelocity.x, safeVelocity.y, safeVelocity.z, active ? math.saturate(state.Emission01) : 0f),
                StateFlags = activeFlags,
                StableHash = active ? state.StableHash : 0u,
                SpeciesBucket = active ? (((uint)state.BucketId << 16) | (uint)state.SpeciesId) : 0u,
                Reserved = active ? state.Reserved : 0u,
                VisualParams = new float4(age01, safeAge, hash01, active ? 1f : 0f)
            };
        }

        private static GraphicsBuffer CreateStructuredBuffer<T>(int capacity) where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                capacity,
                UnsafeUtility.SizeOf<T>());
        }

        private bool TryResolveDrawMesh(out Mesh mesh)
        {
            mesh = biotaQuadMesh;
            if (mesh != null)
                return true;

            mesh = _runtimeFallbackMesh;
            return mesh != null;
        }

        private void EnsureFallbackDrawMeshReady()
        {
            if (!enableIndirectDraw || biotaQuadMesh != null || _runtimeFallbackMesh != null)
                return;

            _runtimeFallbackMesh = CreateFallbackQuadMesh();
        }

        private static void EnsureSignalLanesReady()
        {
            SignalBus<BiomeChangedSignal>.EnsureInitialized();
            SignalBus<EntitySpawnSignal>.EnsureInitialized();
            SignalBus<DebrisSpawnSignal>.EnsureInitialized();
        }

        private static Mesh CreateFallbackQuadMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "H8_AmbientBiota_IndirectQuad"
            }; // COLD ALLOC: Mesh[1] - fallback ambient biota indirect quad - owner: AMBIENT_BIOTA_DIRECTOR
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f)
            }; // COLD ALLOC: Vector3[4] - fallback ambient quad vertices - owner: AMBIENT_BIOTA_DIRECTOR
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            }; // COLD ALLOC: Vector2[4] - fallback ambient quad UVs - owner: AMBIENT_BIOTA_DIRECTOR
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; // COLD ALLOC: int[6] - fallback ambient quad indices - owner: AMBIENT_BIOTA_DIRECTOR
            mesh.RecalculateBounds();
            return mesh;
        }

        private void ReleaseGraphicsResources()
        {
            ReleaseBuffer(ref _gpuInstanceBufferA);
            ReleaseBuffer(ref _gpuInstanceBufferB);
            ReleaseBuffer(ref _indirectArgsBufferA);
            ReleaseBuffer(ref _indirectArgsBufferB);
            _gpuBufferCapacity = 0;
            _gpuBufferIndex = 0;
            _indirectArgsBufferIndex = 0;
            _gpuPayloadDirty = true;
            _indirectArgsMesh = null;
            _indirectArgsCapacity = -1;

            if (_runtimeFallbackMesh != null)
            {
                Destroy(_runtimeFallbackMesh);
                _runtimeFallbackMesh = null;
            }
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private bool TryFinalizeActiveJobNoWait()
        {
            if (!_jobPending)
                return true;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _activeJobHandle))
                return false;

            _jobPending = false;
            return true;
        }

        private void CompleteActiveJobForTeardown()
        {
            if (!_jobPending)
                return;

            if (!DispatcherJobFence.TryComplete(ref _activeJobHandle, forceComplete: true))
                return;

            _jobPending = false;
        }

        private void PublishPendingDebrisSignals(
            NativeArray<AbsoluteUniversePosition> aups,
            NativeArray<float4> velocities,
            NativeArray<AmbientBiotaState> states)
        {
            int emitted = 0;
            int length = math.min(_capacity, math.min(aups.Length, math.min(velocities.Length, states.Length)));
            _pendingDebrisDrainActive = false;
            for (int i = 0; i < length; i++)
            {
                AmbientBiotaState state = states[i];
                if ((state.Reserved & AmbientBiotaState.ReservedDebrisPending) == 0u)
                    continue;

                if (emitted >= MaxDebrisSignalsPerLateFrame)
                {
                    _pendingDebrisDrainActive = true;
                    return;
                }

                DebrisSpawnSignal debrisSignal = new DebrisSpawnSignal
                {
                    PositionAup = aups[i],
                    SpeciesHash = state.StableHash != 0u ? state.StableHash : state.SpeciesId,
                    SourceEntityId = DirectorSourceHash,
                    Intensity01 = math.saturate(state.Emission01 + math.lerp(0.05f, 0.35f, _visualOverkillWeight01)),
                    DebrisKind = DebrisSpawnSignal.DebrisKindOrganicScrap,
                    Flags = DebrisSpawnSignal.FlagComputeShard,
                    Quantity = (ushort)math.clamp((int)math.round(math.lerp(2f, 6f, _visualOverkillWeight01)), 1, ushort.MaxValue)
                };
                SignalBus<DebrisSpawnSignal>.TryPushTracked(in debrisSignal, ref s_x001AmbientBiotaDirectorSignalPushDropCount);

                states[i] = default;
                aups[i] = default;
                velocities[i] = default;
                emitted++;
            }
        }

        private void RecountActiveBiota(NativeArray<AmbientBiotaState> states)
        {
            if (!states.IsCreated)
                return;

            int active = 0;
            int pendingDebris = 0;
            ushort flags = 0;
            uint hash = 2166136261u;
            int length = math.min(_capacity, states.Length);
            for (int i = 0; i < length; i++)
            {
                AmbientBiotaState state = states[i];
                if ((state.StateFlags & AmbientBiotaState.FlagActive) != 0u)
                {
                    active++;
                    hash = Hash32(hash ^ state.StableHash ^ state.StateFlags ^ ((uint)i * 16777619u));
                }

                if ((state.Reserved & AmbientBiotaState.ReservedDebrisPending) != 0u)
                    pendingDebris++;

                if ((state.Reserved & AmbientBiotaState.ReservedFaultSanitized) != 0u)
                    flags = (ushort)(flags | TelemetryFlagFaultSanitized);
            }

            int culled = math.max(0, _previousActiveBiotaCount - active);
            float now = _telemetryClockSeconds;
            float elapsed = math.select(
                1f,
                now - _lastRecountClockSeconds,
                math.isfinite(now) & math.isfinite(_lastRecountClockSeconds) & _lastRecountClockSeconds > 0f);
            elapsed = math.max(0.0001f, elapsed);
            _lastRecountClockSeconds = math.select(_lastRecountClockSeconds, now, math.isfinite(now));
            _cullRatePerSecond = culled * math.rcp(elapsed);
            _previousActiveBiotaCount = active;
            _activeBiotaCount = active;
            _lastCulledCount = culled;
            _lastStateHash = hash;
            _pendingDebrisDrainActive = _pendingDebrisDrainActive || pendingDebris > 0;
            if (ResolveSurvivalPressure01() >= 0.75f)
                flags = (ushort)(flags | TelemetryFlagSurvivalPressure);
            if (_visualOverkillWeight01 >= 0.6f)
                flags = (ushort)(flags | TelemetryFlagVisualOverkill);
            if (_pendingDebrisDrainActive)
                flags = (ushort)(flags | TelemetryFlagPendingDebris);
            _lastTelemetryFlags = flags;
        }

        private void WriteTelemetryHeartbeat()
        {
            if (!TryResolveTelemetryBuffers(out NativeArray<AmbientBiotaTelemetryEntry> ring, out NativeArray<int> cursor))
                return;

            int index = cursor[0];
            if ((uint)index >= (uint)ring.Length)
                index = 0;

            uint heartbeatFrameIndex = _heartbeatFrameIndex++;
            ring[index] = new AmbientBiotaTelemetryEntry
            {
                CenterAup = _lastPlayerAup,
                FrameIndex = heartbeatFrameIndex,
                StateHash = _lastStateHash,
                ActiveCount = (ushort)math.clamp(_activeBiotaCount, 0, ushort.MaxValue),
                CulledCount = (ushort)math.clamp(_lastCulledCount, 0, ushort.MaxValue),
                Capacity = (ushort)math.clamp(_capacity, 0, ushort.MaxValue),
                Flags = _lastTelemetryFlags
            };

            index++;
            if (index >= ring.Length)
                index = 0;
            cursor[0] = index;

            if ((_lastTelemetryFlags & TelemetryFlagFaultSanitized) != 0 && !_blackBoxDumped)
            {
                DumpBlackBox(ring, index);
                _blackBoxDumped = true;
            }
        }

        private static void DumpBlackBox(NativeArray<AmbientBiotaTelemetryEntry> ring, int cursor)
        {
            string dumpPath = ResolveAgentDumpPath();
            if (string.IsNullOrEmpty(dumpPath))
                return;

            try
            {
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(DirectorSourceHash);
                    writer.Write(ring.Length);
                    writer.Write(cursor);
                    for (int i = 0; i < ring.Length; i++)
                    {
                        AmbientBiotaTelemetryEntry entry = ring[i];
                        writer.Write(entry.CenterAup.GridX);
                        writer.Write(entry.CenterAup.GridY);
                        writer.Write(entry.CenterAup.GridZ);
                        writer.Write(entry.CenterAup.LocalX);
                        writer.Write(entry.CenterAup.LocalY);
                        writer.Write(entry.CenterAup.LocalZ);
                        writer.Write(entry.FrameIndex);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.ActiveCount);
                        writer.Write(entry.CulledCount);
                        writer.Write(entry.Capacity);
                        writer.Write(entry.Flags);
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string ResolveAgentDumpPath()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            if (string.IsNullOrEmpty(currentDirectory))
                return null;

            string projectRoot = Path.GetFileName(currentDirectory) == "Hecton8"
                ? currentDirectory
                : Path.Combine(currentDirectory, "Hecton8");
            return Path.Combine(projectRoot, "Docs", "AgentLogs", AgentDumpFileName);
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct AmbientBiotaGpuInstance
        {
            [FieldOffset(0)] public float4 PositionScale;
            [FieldOffset(16)] public float4 VelocityEmission;
            [FieldOffset(32)] public uint StateFlags;
            [FieldOffset(36)] public uint StableHash;
            [FieldOffset(40)] public uint SpeciesBucket;
            [FieldOffset(44)] public uint Reserved;
            [FieldOffset(48)] public float4 VisualParams;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct AmbientBiotaMacroHydrationJob : IJob
        {
            [NoAlias] public NativeArray<AbsoluteUniversePosition> Aups;
            [NoAlias] public NativeArray<float4> Velocities;
            [NoAlias] public NativeArray<AmbientBiotaState> States;
            [ReadOnly, NoAlias] public NativeArray<MacroSwarm> Swarms;
            [NoAlias] public NativeArray<int> Counters;
            public AbsoluteUniversePosition CenterAup;
            public float RadiusMeters;
            public float LifetimeSeconds;
            public int Capacity;
            public int SwarmCount;
            public ushort BaseSpeciesId;
            public uint Seed;
            public uint FrameIndex;
            public uint CurrentBiomeHash;
            public float QualityWeight01;
            public float SurvivalPressure01;
            public float SystemStress01;

            public void Execute()
            {
                int safeCapacity = math.min(Capacity, math.min(Aups.Length, math.min(Velocities.Length, States.Length)));
                int safeSwarmCount = math.min(SwarmCount, Swarms.Length);
                if (safeCapacity <= 0 || safeSwarmCount <= 0)
                    return;

                float radius = math.max(8f, RadiusMeters);
                float survivalPressure01 = math.saturate(math.select(1f, SurvivalPressure01, math.isfinite(SurvivalPressure01)));
                float qualityWeight01 = math.saturate(math.select(1f, QualityWeight01, math.isfinite(QualityWeight01)));
                float visualBudget01 = math.saturate(SmoothStep01(qualityWeight01) * (1f - survivalPressure01 * 0.75f));
                float visualScale = math.lerp(1f, 0.5f, SmoothStep01(math.saturate((SystemStress01 - MacroHydrationStressCullThreshold01) * math.rcp(0.3f))));
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

                        uint hash = Hash32(Seed ^ CurrentBiomeHash ^ swarm.HashId ^ ((uint)slot * 747796405u) ^ ((uint)spawnedForSwarm * 2891336453u) ^ FrameIndex);
                        double3 offset = ResolveSpawnOffset(hash, radius, visualBudget01);
                        if (!IsFinite(offset))
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

                        float3 velocity = ResolveSpawnVelocity(hash, visualBudget01);
                        Velocities[slot] = new float4(velocity, ((hash >> 8) & 255u) * (1f / 255f));
                        Aups[slot] = aup;
                        States[slot] = new AmbientBiotaState
                        {
                            StateFlags = AmbientBiotaState.FlagActive |
                                         AmbientBiotaState.FlagMacroHydrated |
                                         (survivalPressure01 >= 0.75f
                                             ? AmbientStateMinimumQualityBillboardFlag
                                             : 0u),
                            StableHash = hash,
                            SpeciesId = (ushort)(BaseSpeciesId + 8 + (Hash32(hash ^ CurrentBiomeHash) & 7u)),
                            BucketId = (ushort)(hash & BucketMask),
                            AgeSeconds = 0f,
                            LifetimeSeconds = math.max(1f, LifetimeSeconds * math.lerp(0.8f, 1.6f, ((hash >> 16) & 255u) * (1f / 255f))),
                            ScaleMeters = math.lerp(0.08f, math.lerp(0.26f, 0.42f, visualBudget01), ((hash >> 24) & 255u) * (1f / 255f)),
                            Emission01 = math.saturate(ResolveBiomeEmissionBias(CurrentBiomeHash, hash) + swarm.BiomassValue * 0.6f),
                            Reserved = 0u
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

            private static double3 ResolveSpawnOffset(uint hash, float radius, float visualBudget01)
            {
                float normA = (hash & 65535u) * (1f / 65535f);
                float normB = ((hash >> 10) & 1023u) * (1f / 1023f);
                float normC = ((hash >> 20) & 1023u) * (1f / 1023f);
                float angle = normA * TwoPi;
                float triangle = 1f - math.abs(normC * 2f - 1f);
                float radial = math.lerp(radius, math.lerp(radius * 0.18f, radius * 0.82f, normB), visualBudget01);
                float survivalVertical = (triangle - 0.5f) * 8f;
                float precisionVertical = math.lerp((normC - 0.5f) * 18f, -math.lerp(3f, 18f, normC), visualBudget01);
                float verticalBias = math.lerp(survivalVertical, precisionVertical, visualBudget01);
                return new double3(
                    CosPolynomial7(angle) * radial,
                    verticalBias,
                    SinPolynomial7(angle) * radial);
            }

            private static float3 ResolveSpawnVelocity(uint hash, float visualBudget01)
            {
                float scalar = math.lerp(0.08f, 0.18f, visualBudget01);
                return new float3(
                    (((hash >> 3) & 255u) * (1f / 255f) - 0.5f) * scalar,
                    (((hash >> 11) & 255u) * (1f / 255f)) * scalar,
                    (((hash >> 19) & 255u) * (1f / 255f) - 0.5f) * scalar);
            }

            private static float SmoothStep01(float value)
            {
                float saturated = math.saturate(value);
                return saturated * saturated * (3f - 2f * saturated);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct AmbientBiotaMacroDehydrationJob : IJob
        {
            [NoAlias] public NativeArray<AbsoluteUniversePosition> Aups;
            [NoAlias] public NativeArray<float4> Velocities;
            [NoAlias] public NativeArray<AmbientBiotaState> States;
            [NoAlias] public NativeArray<int> Counters;
            public AbsoluteUniversePosition CenterAup;
            public double RadiusSq;
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

                    AbsoluteUniversePosition sampleAup = Aups[i];
                    double3 delta = DeltaMeters(in sampleAup, in CenterAup);
                    double distSq = math.dot(delta, delta);
                    if (!math.isfinite(distSq) || distSq > RadiusSq)
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct AmbientBiotaSpawnJob : IJob
        {
            [NoAlias] public NativeArray<AbsoluteUniversePosition> Aups;
            [NoAlias] public NativeArray<float4> Velocities;
            [NoAlias] public NativeArray<AmbientBiotaState> States;
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
            public uint CurrentBiomeHash;
            public float SurvivalPressure01;
            public float VisualOverkill01;

            public void Execute()
            {
                if (SpawnBudget <= 0 || !math.isfinite(PreyBiomass01) || PreyBiomass01 <= 0.02f)
                    return;

                int safeCapacity = math.min(Capacity, math.min(Aups.Length, math.min(Velocities.Length, States.Length)));
                int activated = 0;
                uint biomassThreshold = (uint)math.round(math.saturate(PreyBiomass01 * CarryingCapacity01) * 1023f);
                float radius = math.max(8f, RadiusMeters);
                float survivalPressure01 = math.saturate(math.select(1f, SurvivalPressure01, math.isfinite(SurvivalPressure01)));
                float visualOverkill01 = math.saturate(math.select(0f, VisualOverkill01, math.isfinite(VisualOverkill01)));
                float visualBudget01 = math.saturate((1f - survivalPressure01) * (0.45f + visualOverkill01 * 0.55f));
                for (int i = 0; i < safeCapacity && activated < SpawnBudget; i++)
                {
                    AmbientBiotaState state = States[i];
                    if ((state.StateFlags & AmbientBiotaState.FlagActive) != 0u)
                        continue;

                    uint hash = Hash32(Seed ^ CurrentBiomeHash ^ ((uint)i * 747796405u) ^ (FrameIndex * 2891336453u));
                    if ((hash & 1023u) > biomassThreshold)
                        continue;

                    float normA = (hash & 65535u) * (1.0f / 65535.0f);
                    float normB = ((hash >> 10) & 1023u) * (1.0f / 1023.0f);
                    float normC = ((hash >> 20) & 1023u) * (1.0f / 1023.0f);
                    float angle = normA * TwoPi;
                    float radial = math.lerp(radius, math.lerp(radius * 0.35f, radius, normB), visualBudget01);
                    float survivalVertical = (1f - math.abs(normC * 2f - 1f) - 0.5f) * 10f;
                    float precisionVertical = (normC - 0.5f) * math.lerp(28f, 38f, visualOverkill01);
                    float vertical = math.lerp(survivalVertical, precisionVertical, visualBudget01);
                    double3 offset = new double3(
                        CosPolynomial7(angle) * radial,
                        vertical,
                        SinPolynomial7(angle) * radial);

                    if (!IsFinite(offset))
                        continue;

                    AbsoluteUniversePosition aup = OffsetAup(in CenterAup, offset);
                    if (!IsFiniteAup(in aup))
                        continue;

                    float speedScalar = math.lerp(0.08f, 0.14f, visualOverkill01);
                    float3 velocity = new float3(
                        (normB - 0.5f) * speedScalar,
                        (normC - 0.5f) * 0.03f,
                        (normA - 0.5f) * speedScalar);
                    if (!math.all(math.isfinite(velocity)))
                        velocity = float3.zero;

                    Aups[i] = aup;
                    Velocities[i] = new float4(velocity, normA);
                    States[i] = new AmbientBiotaState
                    {
                        StateFlags = AmbientBiotaState.FlagActive |
                                     (survivalPressure01 >= 0.75f ? AmbientStateMinimumQualityBillboardFlag : 0u),
                        StableHash = hash,
                        SpeciesId = (ushort)(BaseSpeciesId + (Hash32(hash ^ CurrentBiomeHash) & 7u)),
                        BucketId = (ushort)(hash & BucketMask),
                        AgeSeconds = 0f,
                        LifetimeSeconds = math.max(1f, LifetimeSeconds * math.lerp(0.75f, math.lerp(1.25f, 1.65f, visualOverkill01), normC)),
                        ScaleMeters = math.lerp(0.06f, math.lerp(0.28f, 0.42f, visualOverkill01), normB),
                        Emission01 = math.saturate(ResolveBiomeEmissionBias(CurrentBiomeHash, hash) + PreyBiomass01 * math.lerp(0.4f, 0.7f, visualOverkill01) + normA * 0.2f),
                        Reserved = 0u
                    };
                    activated++;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct AmbientBiotaDriftJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<AbsoluteUniversePosition> Aups;
            [NoAlias] public NativeArray<float4> Velocities;
            [NoAlias] public NativeArray<AmbientBiotaState> States;
            public AbsoluteUniversePosition CenterAup;
            public float3 PlayerForward;
            public float3 FlowVector;
            public float DeltaTime;
            public double RadiusSq;
            public int ActiveBucket;
            public uint FrameIndex;
            public float SurvivalPressure01;
            public float VisualOverkill01;
            public float HeadlightConeDot;
            public float AvoidanceMetersPerSecond;

            public void Execute(int index)
            {
                AmbientBiotaState originalState = States[index];
                AmbientBiotaState state = originalState;
                AbsoluteUniversePosition originalAup = Aups[index];
                float4 originalPackedVelocity = Velocities[index];
                bool active = (state.StateFlags & AmbientBiotaState.FlagActive) != 0u;
                bool bucketActive = ((int)state.BucketId & BucketMask) == (ActiveBucket & BucketMask);
                bool validDeltaTime = math.isfinite(DeltaTime) & DeltaTime > 0f;
                bool shouldSimulate = active & bucketActive & validDeltaTime;
                float safeDeltaTime = math.select(0f, DeltaTime, shouldSimulate);
                bool faultSanitized = false;
                float survivalPressure01 = math.saturate(math.select(1f, SurvivalPressure01, math.isfinite(SurvivalPressure01)));
                float visualOverkill01 = math.saturate(math.select(0f, VisualOverkill01, math.isfinite(VisualOverkill01)));
                float3 rawVelocity = originalPackedVelocity.xyz;
                bool velocityFinite = math.all(math.isfinite(rawVelocity));
                faultSanitized |= shouldSimulate & !velocityFinite;
                float3 velocity = math.select(float3.zero, rawVelocity, velocityFinite);

                uint hash = Hash32(state.StableHash ^ (FrameIndex * 2246822519u));
                float3 brownian = math.lerp(
                    ResolveHashBrownian(hash, visualOverkill01),
                    ResolveTriangleNoise(hash),
                    survivalPressure01);

                float3 rawTargetVelocity = FlowVector + brownian;
                bool targetVelocityFinite = math.all(math.isfinite(rawTargetVelocity));
                faultSanitized |= shouldSimulate & !targetVelocityFinite;
                float3 targetVelocity = math.select(brownian, rawTargetVelocity, targetVelocityFinite);

                double3 deltaFromPlayer = DeltaMeters(in originalAup, in CenterAup);
                float3 relative = new float3((float)deltaFromPlayer.x, (float)deltaFromPlayer.y, (float)deltaFromPlayer.z);
                float3 outward = SafeNormalize(relative, float3.zero, ref faultSanitized);
                float3 playerForward = SafeNormalize(PlayerForward, new float3(0f, 0f, 1f), ref faultSanitized);
                float frontDot = math.dot(outward, playerForward);
                bool frontDotFinite = math.isfinite(frontDot);
                float avoidanceWeight01 = math.select(0f, visualOverkill01, frontDotFinite & (frontDot > HeadlightConeDot));
                bool shouldAvoidLight = shouldSimulate & (avoidanceWeight01 > 0.0001f);
                faultSanitized |= shouldSimulate & (visualOverkill01 > 0.0001f) & !frontDotFinite;
                targetVelocity += math.select(float3.zero, outward * AvoidanceMetersPerSecond * avoidanceWeight01, shouldAvoidLight);
                uint stateFlagsWithoutReactive = state.StateFlags & ~AmbientStateVisualOverkillReactiveFlag;
                state.StateFlags = math.select(
                    stateFlagsWithoutReactive,
                    stateFlagsWithoutReactive | AmbientStateVisualOverkillReactiveFlag,
                    shouldAvoidLight);
                float relaxedEmission = math.saturate(state.Emission01 - safeDeltaTime * math.lerp(0.18f, 0.08f, survivalPressure01));
                float panicEmission = math.saturate(state.Emission01 + safeDeltaTime * 0.9f);
                state.Emission01 = math.select(relaxedEmission, panicEmission, shouldAvoidLight);

                float blend = math.saturate(safeDeltaTime * math.lerp(0.35f, 0.22f, survivalPressure01));
                velocity = math.lerp(velocity, targetVelocity, blend);
                float maxSpeed = math.lerp(math.lerp(0.42f, 0.95f, visualOverkill01), 0.18f, survivalPressure01);
                velocity = ClampMagnitude(velocity, maxSpeed, ref faultSanitized);
                double3 deltaMeters = new double3(velocity.x, velocity.y, velocity.z) * safeDeltaTime;
                bool deltaFinite = IsFinite(deltaMeters);
                faultSanitized |= shouldSimulate & !deltaFinite;
                deltaMeters = SelectDouble3(double3.zero, deltaMeters, deltaFinite);

                AbsoluteUniversePosition nextAup = OffsetAup(in originalAup, deltaMeters);
                bool nextAupFinite = IsFiniteAup(in nextAup);
                faultSanitized |= shouldSimulate & !nextAupFinite;
                nextAup = SelectAup(in CenterAup, in nextAup, nextAupFinite);

                double3 deltaFromCenter = DeltaMeters(in nextAup, in CenterAup);
                double distSq = math.dot(deltaFromCenter, deltaFromCenter);
                bool ageInvalid = !math.isfinite(state.AgeSeconds) | state.AgeSeconds < 0f;
                faultSanitized |= shouldSimulate & ageInvalid;
                state.AgeSeconds = math.select(state.AgeSeconds, 0f, ageInvalid);
                state.AgeSeconds += safeDeltaTime;
                bool distFinite = math.isfinite(distSq);
                bool expired = shouldSimulate & state.AgeSeconds >= math.max(0.1f, state.LifetimeSeconds);
                bool outside = shouldSimulate & (!distFinite | distSq > RadiusSq);
                bool retire = expired | outside;
                uint sanitizedFlag = math.select(0u, AmbientBiotaState.ReservedFaultSanitized, shouldSimulate & faultSanitized);

                state.StateFlags = math.select(state.StateFlags, 0u, retire);
                state.AgeSeconds = math.select(state.AgeSeconds, 0f, retire);
                state.Reserved |= math.select(0u, AmbientBiotaState.ReservedDebrisPending, retire) | sanitizedFlag;

                float4 simulatedVelocity = math.select(new float4(velocity, originalPackedVelocity.w), float4.zero, retire);
                Aups[index] = SelectAup(in originalAup, in nextAup, shouldSimulate);
                Velocities[index] = math.select(originalPackedVelocity, simulatedVelocity, shouldSimulate);
                States[index] = SelectState(in originalState, in state, shouldSimulate);
            }

            private static float3 ResolveTriangleNoise(uint hash)
            {
                float a = ((hash >> 0) & 255u) * (1f / 255f);
                float b = ((hash >> 8) & 255u) * (1f / 255f);
                float c = ((hash >> 16) & 255u) * (1f / 255f);
                return new float3(
                    (1f - math.abs(a * 2f - 1f) - 0.5f) * 0.08f,
                    (1f - math.abs(b * 2f - 1f) - 0.5f) * 0.018f,
                    (1f - math.abs(c * 2f - 1f) - 0.5f) * 0.08f);
            }

            private static float3 ResolveHashBrownian(uint hash, float visualOverkill01)
            {
                float scale = math.lerp(0.08f, 0.15f, visualOverkill01);
                float verticalScale = math.lerp(0.025f, 0.045f, visualOverkill01);
                return new float3(
                    (((hash >> 0) & 255u) * (1f / 255f) - 0.5f) * scale,
                    (((hash >> 8) & 255u) * (1f / 255f) - 0.5f) * verticalScale,
                    (((hash >> 16) & 255u) * (1f / 255f) - 0.5f) * scale);
            }

            private static float3 ClampMagnitude(float3 value, float maxLength, ref bool faultSanitized)
            {
                float lenSq = math.dot(value, value);
                bool finiteLen = math.isfinite(lenSq);
                bool validLen = finiteLen & lenSq > 0f;
                faultSanitized |= !finiteLen;
                float maxSq = maxLength * maxLength;
                bool tooLong = validLen & lenSq > maxSq;
                float invLength = math.rsqrt(math.max(lenSq, 0.000001f));
                bool invFinite = math.isfinite(invLength);
                faultSanitized |= tooLong & !invFinite;
                float3 validValue = math.select(float3.zero, value, validLen);
                float3 clampedValue = value * math.select(0f, invLength * maxLength, invFinite);
                return math.select(validValue, clampedValue, tooLong & invFinite);
            }

            public static float3 SafeNormalize(float3 value, float3 fallback, ref bool faultSanitized)
            {
                float lenSq = math.dot(value, value);
                bool finiteLen = math.isfinite(lenSq);
                bool validLen = finiteLen & lenSq >= 0.000001f;
                faultSanitized |= !finiteLen;
                float invLength = math.rsqrt(math.max(lenSq, 0.000001f));
                bool invFinite = math.isfinite(invLength);
                faultSanitized |= validLen & !invFinite;
                return math.select(fallback, value * invLength, validLen & invFinite);
            }
        }

        private static double3 SelectDouble3(double3 falseValue, double3 trueValue, bool condition)
        {
            return new double3(
                math.select(falseValue.x, trueValue.x, condition),
                math.select(falseValue.y, trueValue.y, condition),
                math.select(falseValue.z, trueValue.z, condition));
        }

        private static AbsoluteUniversePosition SelectAup(
            in AbsoluteUniversePosition falseValue,
            in AbsoluteUniversePosition trueValue,
            bool condition)
        {
            return new AbsoluteUniversePosition
            {
                GridX = math.select(falseValue.GridX, trueValue.GridX, condition),
                GridY = math.select(falseValue.GridY, trueValue.GridY, condition),
                GridZ = math.select(falseValue.GridZ, trueValue.GridZ, condition),
                LocalX = math.select(falseValue.LocalX, trueValue.LocalX, condition),
                LocalY = math.select(falseValue.LocalY, trueValue.LocalY, condition),
                LocalZ = math.select(falseValue.LocalZ, trueValue.LocalZ, condition)
            };
        }

        private static AmbientBiotaState SelectState(
            in AmbientBiotaState falseValue,
            in AmbientBiotaState trueValue,
            bool condition)
        {
            return new AmbientBiotaState
            {
                StateFlags = math.select(falseValue.StateFlags, trueValue.StateFlags, condition),
                StableHash = math.select(falseValue.StableHash, trueValue.StableHash, condition),
                SpeciesId = (ushort)math.select((uint)falseValue.SpeciesId, (uint)trueValue.SpeciesId, condition),
                BucketId = (ushort)math.select((uint)falseValue.BucketId, (uint)trueValue.BucketId, condition),
                AgeSeconds = math.select(falseValue.AgeSeconds, trueValue.AgeSeconds, condition),
                LifetimeSeconds = math.select(falseValue.LifetimeSeconds, trueValue.LifetimeSeconds, condition),
                ScaleMeters = math.select(falseValue.ScaleMeters, trueValue.ScaleMeters, condition),
                Emission01 = math.select(falseValue.Emission01, trueValue.Emission01, condition),
                Reserved = math.select(falseValue.Reserved, trueValue.Reserved, condition)
            };
        }

        private static AbsoluteUniversePosition OffsetAup(in AbsoluteUniversePosition origin, double3 deltaMeters)
        {
            double3 local = new double3(
                origin.LocalX + deltaMeters.x,
                origin.LocalY + deltaMeters.y,
                origin.LocalZ + deltaMeters.z);
            if (!IsFinite(local))
                return origin;

            double3 shiftDouble = math.floor(local * (1.0d / AupCellSizeMetersDouble));
            if (!IsFinite(shiftDouble) || !IsRepresentableGridShift(shiftDouble))
                return origin;

            long shiftX = (long)shiftDouble.x;
            long shiftY = (long)shiftDouble.y;
            long shiftZ = (long)shiftDouble.z;
            if (!CanAddGridOffset(origin.GridX, shiftX) ||
                !CanAddGridOffset(origin.GridY, shiftY) ||
                !CanAddGridOffset(origin.GridZ, shiftZ))
            {
                return origin;
            }

            double3 normalizedLocal = local - new double3(shiftX, shiftY, shiftZ) * AupCellSizeMetersDouble;
            if (!IsFinite(normalizedLocal))
                return origin;

            return new AbsoluteUniversePosition
            {
                GridX = origin.GridX + shiftX,
                GridY = origin.GridY + shiftY,
                GridZ = origin.GridZ + shiftZ,
                LocalX = (float)normalizedLocal.x,
                LocalY = (float)normalizedLocal.y,
                LocalZ = (float)normalizedLocal.z
            };
        }

        private static byte ResolveMacroVisualQualityByte(in AbsoluteUniversePosition centerAup, byte qualityByte, float systemStress01)
        {
            if (!IsFiniteAup(in centerAup) || !math.isfinite(systemStress01))
                return 0;

            byte clampedQuality = qualityByte <= 3 ? qualityByte : (byte)3;
            float quality01 = clampedQuality * (1f / 3f);
            float stressPressure01 = SmoothStep01(math.saturate((systemStress01 - 0.62f) * math.rcp(0.38f)));
            return (byte)math.clamp((int)math.round(math.saturate(quality01 * (1f - stressPressure01)) * 3f), 0, 3);
        }

        private static double3 DeltaMeters(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            return new double3(
                (((double)a.GridX - (double)b.GridX) * AupCellSizeMetersDouble) + (a.LocalX - b.LocalX),
                (((double)a.GridY - (double)b.GridY) * AupCellSizeMetersDouble) + (a.LocalY - b.LocalY),
                (((double)a.GridZ - (double)b.GridZ) * AupCellSizeMetersDouble) + (a.LocalZ - b.LocalZ));
        }

        private static bool IsFinite(double3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private static bool IsRepresentableGridShift(double3 shift)
        {
            return shift.x >= long.MinValue &&
                   shift.x <= long.MaxValue &&
                   shift.y >= long.MinValue &&
                   shift.y <= long.MaxValue &&
                   shift.z >= long.MinValue &&
                   shift.z <= long.MaxValue;
        }

        private static bool CanAddGridOffset(long value, long offset)
        {
            if (offset > 0L)
                return value <= long.MaxValue - offset;

            if (offset < 0L)
                return offset == long.MinValue
                    ? value >= 0L
                    : value >= long.MinValue - offset;

            return true;
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition aup)
        {
            return math.isfinite(aup.LocalX) &&
                   math.isfinite(aup.LocalY) &&
                   math.isfinite(aup.LocalZ);
        }

        private static float3 SanitizeForward(float3 forward, float3 fallback)
        {
            bool fault = false;
            float3 normalized = AmbientBiotaDriftJob.SafeNormalize(forward, fallback, ref fault);
            return math.all(math.isfinite(normalized)) ? normalized : new float3(0f, 0f, 1f);
        }

        private static float3 SanitizeRuntimePosition(float3 position, float3 fallback)
        {
            return math.all(math.isfinite(position)) ? position : fallback;
        }

        private static float ResolveBiomeEmissionBias(uint biomeHash, uint slotHash)
        {
            if (biomeHash == 0u)
                return 0.12f;

            uint mixed = Hash32(biomeHash ^ slotHash);
            float family01 = (mixed & 255u) * (1f / 255f);
            return (biomeHash & 1u) == 0u
                ? math.lerp(0.18f, 0.42f, family01)
                : math.lerp(0.04f, 0.18f, family01);
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
}
