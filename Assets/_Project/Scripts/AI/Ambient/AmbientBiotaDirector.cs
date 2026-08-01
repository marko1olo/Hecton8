using System;
using System.Runtime.InteropServices;
using System.Threading;
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
        private const string BlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_1403_AMBIENT_BIOTA.bin";
        private const string BlackBoxDumpPayloadLabel = "ambientBiotaTelemetryDumpPayload";
        private const int BucketMask = 15;
        private const int BlackBoxFrameCount = 300;
        private const int MacroHydrationCounterCount = 4;
        private const int MacroVisualBoidsPerBiomassUnit = 64;
        private const int MaxDebrisSignalsPerLateFrame = 16;
        private const int BiomeChangedSignalLaneCapacity = 64;
        private const int EntitySpawnSignalLaneCapacity = 128;
        private const int DebrisSpawnSignalLaneCapacity = 128;
        private const int CapacityResizeGranularity = 256;
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
        private const ushort TelemetryFlagPendingDebris = 1 << 3;
        private const uint AmbientStateHeadlightReactiveFlag = AmbientBiotaState.FlagHeadlightReactive;
        private static readonly AmbientBiotaTelemetryEntry[] s_blackBoxDumpSnapshot =
            new AmbientBiotaTelemetryEntry[BlackBoxFrameCount]; // COLD ALLOC: fixed ambient blackbox dump snapshot, owner: AMBIENT_BIOTA_DIRECTOR
        private static int s_blackBoxDumpCursor;
        private static int s_blackBoxDumpCount;
        private static int s_blackBoxDumpInFlight;
        private static uint s_blackBoxDumpHash;
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
        private static readonly ulong MacroMutationGuardMask =
            AmbientBiotaMutationGuardBit(BufferID.BiotaAUPs) |
            AmbientBiotaMutationGuardBit(BufferID.BiotaVelocities) |
            AmbientBiotaMutationGuardBit(BufferID.BiotaStates) |
            AmbientBiotaMutationGuardBit(BufferID.BiotaMacroHydrationCounters);
        private static readonly ulong TelemetryMutationGuardMask =
            AmbientBiotaMutationGuardBit(BufferID.BiotaTelemetryRing) |
            AmbientBiotaMutationGuardBit(BufferID.BiotaTelemetryCursor);
        private const uint BiotaJobPinAups = 1u << 0;
        private const uint BiotaJobPinVelocities = 1u << 1;
        private const uint BiotaJobPinStates = 1u << 2;
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
        [SerializeField, Tooltip("REQUIRED for the indirect draw. There is no runtime fallback quad: TryResolveDrawMesh returns this field verbatim and UploadIndirectArgs needs submesh 0 index data, so an empty field means zero pixels.")] private Mesh biotaQuadMesh;
        [SerializeField, Tooltip("REQUIRED for the indirect draw. Material whose shader is Hecton8/Ambient/BiotaIndirect (Hecton_AmbientBiotaIndirect.shader). No runtime fallback exists and none is possible in a player build - see EnsureIndirectPresentationAssetsOrReport.")] private Material biotaMaterial;
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
        private MaterialPropertyBlock _biotaDrawProperties; // COLD ALLOC: MaterialPropertyBlock[1] - ambient biota indirect draw payload - owner: AMBIENT_BIOTA_DIRECTOR
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
        private int _gpuVisibleInstanceCount;
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
        private GraphicsBuffer _publishedBiotaInstanceBuffer;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private Vector4 _publishedBiotaFlowVector;
        private Vector4 _publishedBiotaOriginWs;
        private int _publishedBiotaCapacity = -1;
        private int _publishedBiotaActiveCount = -1;
        private uint _publishedBiotaBiomeHash;
        private float _publishedBiotaQualityWeight = -1f;
        private float _publishedBiotaSystemStress = -1f;
        private float _publishedBiotaOverkill = -1f;
        private float _publishedBiotaVisualTime = -1f;
        private bool _jobPending;
        private bool _jobBuffersPinned;
        private IDataVault _jobBufferPinVault;
        private uint _jobBufferPinMask;
        private int[] _macroSlotScratch;
        private int[] _macroSlotMarkScratch;
        private AbsoluteUniversePosition[] _macroAupScratch;
        private float4[] _macroVelocityScratch;
        private AmbientBiotaState[] _macroStateScratch;
        private int _macroSlotMarkGeneration;
        private bool _pendingDebrisDrainActive;
        private bool _blackBoxDumped;
        private bool _gpuPayloadDirty = true;
        private bool _biotaDrawMaterialPublished;
        private bool _serviceRegistered;
        private bool _tickRegistered;
        private bool _slowTickRegistered;
        private bool _lateFrameRegistered;
        private bool _hotSwapListenerRegistered;
        private bool _presentationAssetGapReported;

        public bool IsInitialized => _capacity > 0 &&
                                     IsOwnedVaultHandle(in _biotaAupHandle, BufferID.BiotaAUPs) &&
                                     IsOwnedVaultHandle(in _biotaVelocityHandle, BufferID.BiotaVelocities) &&
                                     IsOwnedVaultHandle(in _biotaStateHandle, BufferID.BiotaStates);

        public int TickCount => _tickCount;

        public int Capacity => _capacity;

        public int ActiveBiotaCount => _activeBiotaCount;

        public float CullRatePerSecond => _cullRatePerSecond;

        public NativeArray<AbsoluteUniversePosition>.ReadOnly BiotaAups =>
            !_jobPending && TryResolveBiotaAupBuffer(out NativeArray<AbsoluteUniversePosition> aups) ? aups.AsReadOnly() : default;

        public NativeArray<float4>.ReadOnly BiotaVelocities =>
            !_jobPending && TryResolveBiotaVelocityBuffer(out NativeArray<float4> velocities) ? velocities.AsReadOnly() : default;

        public NativeArray<AmbientBiotaState>.ReadOnly BiotaStates =>
            !_jobPending && TryResolveBiotaStateBuffer(out NativeArray<AmbientBiotaState> states) ? states.AsReadOnly() : default;

        // AmbientBiotaDirector is the sole IAmbientBiotaService owner and lives in
        // Hecton8.AI.Ambient (autoReferenced: false). No other assembly can AddComponent it, and
        // no scene/prefab GUID hit exists for 560a1d1e41eb4e9e81bc73402e4c7807. Live consumers
        // cache the permanent null: EcosystemDirector.cs:1917, Creature.cs:1226/7320,
        // WorldChunkResidencyManager.cs:2576. Self-bootstrap via RuntimeInitialize is the only
        // path that can construct this owner without breaking the asmdef fence.
        private const string RuntimeRootName = "__HECTON_AMBIENT_BIOTA_RUNTIME";

        /// <summary>
        /// Cold-path resolve-or-create for the ambient biota owner. Idempotent.
        /// </summary>
        public static AmbientBiotaDirector EnsureRuntimeInstance()
        {
            AmbientBiotaDirector existing = FindFirstObjectByType<AmbientBiotaDirector>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (!existing.gameObject.activeSelf)
                    existing.gameObject.SetActive(true);
                if (!existing.enabled)
                    existing.enabled = true;
                return existing;
            }

            if (GlobalRegistry.AmbientBiota is AmbientBiotaDirector registered && registered != null)
                return registered;

            GameObject root = GameObject.Find(RuntimeRootName);
            if (root == null)
                root = new GameObject(RuntimeRootName); // COLD ALLOC: GameObject[1] - ambient biota runtime root - owner: AmbientBiotaDirector

            root.hideFlags = HideFlags.None;
            if (!root.activeSelf)
                root.SetActive(true);

            if (!root.TryGetComponent(out AmbientBiotaDirector director))
                director = root.AddComponent<AmbientBiotaDirector>();

            return director;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstanceOnLoad()
        {
            // Edit-mode domain reloads and batch validators must not spawn a play-mode owner.
            if (!Application.isPlaying)
                return;

            EnsureRuntimeInstance();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            EnsureBiotaDrawPropertiesCold();
            CacheDependencies();
            RefreshQualityPolicy();
            EnsureVaultBuffers();
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
            ReleaseMacroScratch();
            ReleaseVaultHandles(_vault);
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

            if (_jobPending)
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
            if (!TryPinBiotaJobBuffers())
                return;

            bool scheduled = false;
            try
            {
                if (!TryResolveBiotaBuffers(out NativeArray<AbsoluteUniversePosition> aups, out NativeArray<float4> velocities, out NativeArray<AmbientBiotaState> states))
                    return;

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
                scheduled = true;
                _frameIndex++;
            }
            finally
            {
                if (!scheduled)
                    ReleaseBiotaJobBufferPins();
            }
        }

        public void SlowTick()
        {
            RefreshQualityPolicy();

            if (_jobPending)
                return;

            if (!HasVaultBuffersReadyNoGrow())
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
            if (!TryPinBiotaJobBuffers())
                return;

            bool scheduled = false;
            try
            {
                if (!TryResolveBiotaBuffers(out NativeArray<AbsoluteUniversePosition> aups, out NativeArray<float4> velocities, out NativeArray<AmbientBiotaState> states))
                    return;

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
                scheduled = true;
                _frameIndex++;
            }
            finally
            {
                if (!scheduled)
                    ReleaseBiotaJobBufferPins();
            }
        }

        public void LateFrameTick()
        {
            bool completedJob = _jobPending && TryFinalizeActiveJobNoWait();
            try
            {
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
            finally
            {
                if (completedJob)
                    ReleaseBiotaJobBufferPins();
            }
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
            if (!TryResolveBiotaBuffers(out _, out _, out NativeArray<AmbientBiotaState> stageStates))
                return false;

            int safeCapacity = math.min(_capacity, stageStates.Length);
            if (!HasMacroScratchCapacity(safeCapacity))
                return false;

            int safeSwarmCount = math.min(swarmCount, swarms.Length);
            float radiusMeters = math.max(8f, radiusMetersQ);
            float macroQualityWeight01 = ResolveMacroVisualQualityWeight01(in centerAup, qualityByte, systemStress01);
            byte spawnQualityByte = EncodeMacroVisualQualitySignalByte(macroQualityWeight01);
            float macroSurvivalPressure01 = math.max(
                1f - SmoothStep01(macroQualityWeight01),
                SmoothStep01(math.saturate((systemStress01 - 0.62f) * math.rcp(0.38f))));
            uint frameIndex = _frameIndex;
            int requestedCount;
            int invalidCount;
            int stagedCount = StageMacroHydrationScratch(
                stageStates,
                swarms,
                safeCapacity,
                safeSwarmCount,
                in centerAup,
                radiusMeters,
                frameIndex,
                baseSpeciesId,
                lifetimeSeconds,
                macroQualityWeight01,
                macroSurvivalPressure01,
                systemStress01,
                out requestedCount,
                out invalidCount);
            if (stagedCount <= 0)
                return false;

            if (!TryAcquireAmbientMutationGuard(MacroMutationGuardMask, out IDataVault guardVault))
                return false;

            int committedCount = 0;
            try
            {
                if (!TryResolveBiotaBuffers(out NativeArray<AbsoluteUniversePosition> aups, out NativeArray<float4> velocities, out NativeArray<AmbientBiotaState> states) ||
                    !TryResolveMacroCounters(out NativeArray<int> counters))
                {
                    return false;
                }

                committedCount = CommitMacroHydrationScratch(
                    aups,
                    velocities,
                    states,
                    counters,
                    stagedCount,
                    requestedCount,
                    invalidCount);
            }
            finally
            {
                ReleaseAmbientMutationGuard(guardVault, MacroMutationGuardMask);
            }

            _frameIndex = frameIndex + 1u;
            spawnedBoidCount = committedCount;
            if (spawnedBoidCount <= 0)
                return false;

            if (TryResolveBiotaStateBuffer(out NativeArray<AmbientBiotaState> recountStates))
                RecountActiveBiota(recountStates);

            _gpuPayloadDirty = true;
            EntitySpawnSignal spawnSignal = new EntitySpawnSignal
            {
                PositionAup = centerAup,
                SourceHash = MacroHydrationSeedSalt,
                SpawnedCount = (ushort)math.clamp(spawnedBoidCount, 0, ushort.MaxValue),
                RequestedCount = (ushort)math.clamp(requestedCount, 0, ushort.MaxValue),
                EntityKind = EntitySpawnSignal.KindEcology,
                QualityWeightQ8 = spawnQualityByte,
                Flags = EntitySpawnSignal.FlagEcology,
                SurvivalPressureQ8 = EncodeMacroSurvivalPressureSignalByte(macroSurvivalPressure01),
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

            if (!TryResolveBiotaBuffers(out NativeArray<AbsoluteUniversePosition> stageAups, out _, out NativeArray<AmbientBiotaState> stageStates))
                return false;

            int safeCapacity = math.min(_capacity, math.min(stageAups.Length, stageStates.Length));
            if (!HasMacroScratchCapacity(safeCapacity))
                return false;

            float radiusMeters = math.max(8f, radiusMetersQ);
            double radiusSq = (double)radiusMeters * radiusMeters;
            int stagedCount = StageMacroDehydrationSlots(stageAups, stageStates, safeCapacity, in centerAup, radiusSq);
            if (stagedCount <= 0)
                return false;

            if (!TryAcquireAmbientMutationGuard(MacroMutationGuardMask, out IDataVault guardVault))
                return false;

            int committedCount = 0;
            try
            {
                if (!TryResolveBiotaBuffers(out NativeArray<AbsoluteUniversePosition> aups, out NativeArray<float4> velocities, out NativeArray<AmbientBiotaState> states) ||
                    !TryResolveMacroCounters(out NativeArray<int> counters))
                {
                    return false;
                }

                committedCount = CommitMacroDehydrationSlots(
                    aups,
                    velocities,
                    states,
                    counters,
                    stagedCount,
                    in centerAup,
                    radiusSq);
            }
            finally
            {
                ReleaseAmbientMutationGuard(guardVault, MacroMutationGuardMask);
            }

            releasedBoidCount = committedCount;
            if (releasedBoidCount <= 0)
                return false;

            biomassValue = math.saturate(releasedBoidCount * math.rcp((float)MacroVisualBoidsPerBiomassUnit));
            if (TryResolveBiotaStateBuffer(out NativeArray<AmbientBiotaState> recountStates))
                RecountActiveBiota(recountStates);
            _gpuPayloadDirty = true;
            return biomassValue > 0f;
        }

        private int StageMacroHydrationScratch(
            NativeArray<AmbientBiotaState> states,
            NativeArray<MacroSwarm> swarms,
            int safeCapacity,
            int safeSwarmCount,
            in AbsoluteUniversePosition centerAup,
            float radiusMeters,
            uint frameIndex,
            ushort baseSpeciesId,
            float lifetimeSeconds,
            float macroQualityWeight01,
            float macroSurvivalPressure01,
            float systemStress01,
            out int requested,
            out int invalid)
        {
            requested = 0;
            invalid = 0;
            if (!states.IsCreated ||
                !swarms.IsCreated ||
                safeCapacity <= 0 ||
                safeSwarmCount <= 0)
            {
                return 0;
            }

            float radius = math.max(8f, radiusMeters);
            float survivalPressure01 = math.saturate(math.select(1f, macroSurvivalPressure01, math.isfinite(macroSurvivalPressure01)));
            float qualityWeight01 = math.saturate(math.select(1f, macroQualityWeight01, math.isfinite(macroQualityWeight01)));
            float visualBudget01 = math.saturate(SmoothStep01(qualityWeight01) * (1f - survivalPressure01 * 0.75f));
            float visualScale = math.lerp(1f, 0.5f, SmoothStep01(math.saturate((systemStress01 - MacroHydrationStressCullThreshold01) * math.rcp(0.3f))));
            int staged = 0;
            int searchStart = 0;
            int stageGeneration = BeginMacroSlotStageGeneration(safeCapacity);

            for (int swarmIndex = 0; swarmIndex < safeSwarmCount && staged < safeCapacity; swarmIndex++)
            {
                MacroSwarm swarm = swarms[swarmIndex];
                if (!IsValidMacroSwarmForHydration(in swarm))
                {
                    invalid++;
                    continue;
                }

                int swarmBudget = math.clamp(
                    (int)math.ceil(math.saturate(swarm.BiomassValue) * MacroVisualBoidsPerBiomassUnit * visualScale),
                    1,
                    MacroVisualBoidsPerBiomassUnit);
                requested += swarmBudget;

                for (int spawnedForSwarm = 0; spawnedForSwarm < swarmBudget && staged < safeCapacity; spawnedForSwarm++)
                {
                    int slot = FindInactiveMacroStagingSlot(states, safeCapacity, ref searchStart, stageGeneration);
                    if (slot < 0)
                        break;

                    uint hash = Hash32(MacroHydrationSeedSalt ^ _currentBiomeHash ^ swarm.HashId ^ ((uint)slot * 747796405u) ^ ((uint)spawnedForSwarm * 2891336453u) ^ frameIndex);
                    double3 offset = ResolveMacroHydrationSpawnOffset(hash, radius, visualBudget01);
                    if (!IsFinite(offset))
                    {
                        invalid++;
                        continue;
                    }

                    AbsoluteUniversePosition aup = OffsetAup(in centerAup, offset);
                    if (!IsFiniteAup(in aup))
                    {
                        invalid++;
                        continue;
                    }

                    float3 velocity = ResolveMacroHydrationSpawnVelocity(hash, visualBudget01);
                    _macroSlotMarkScratch[slot] = stageGeneration;
                    _macroSlotScratch[staged] = slot;
                    _macroAupScratch[staged] = aup;
                    _macroVelocityScratch[staged] = new float4(velocity, ((hash >> 8) & 255u) * (1f / 255f));
                    _macroStateScratch[staged] = new AmbientBiotaState
                    {
                        StateFlags = AmbientBiotaState.FlagActive |
                                     AmbientBiotaState.FlagMacroHydrated,
                        StableHash = hash,
                        SpeciesId = (ushort)(baseSpeciesId + 8 + (Hash32(hash ^ _currentBiomeHash) & 7u)),
                        BucketId = (ushort)(hash & BucketMask),
                        AgeSeconds = 0f,
                        LifetimeSeconds = math.max(1f, lifetimeSeconds * math.lerp(0.8f, 1.6f, ((hash >> 16) & 255u) * (1f / 255f))),
                        ScaleMeters = math.lerp(0.08f, math.lerp(0.26f, 0.42f, visualBudget01), ((hash >> 24) & 255u) * (1f / 255f)),
                        Emission01 = math.saturate(ResolveBiomeEmissionBias(_currentBiomeHash, hash) + swarm.BiomassValue * 0.6f),
                        Reserved = 0u
                    };
                    staged++;
                }
            }

            return staged;
        }

        private int CommitMacroHydrationScratch(
            NativeArray<AbsoluteUniversePosition> aups,
            NativeArray<float4> velocities,
            NativeArray<AmbientBiotaState> states,
            NativeArray<int> counters,
            int stagedCount,
            int requested,
            int invalid)
        {
            ClearMacroCounters(counters);
            int safeCapacity = math.min(_capacity, math.min(aups.Length, math.min(velocities.Length, states.Length)));
            int safeCount = math.min(stagedCount, math.min(safeCapacity, _macroSlotScratch.Length));
            int spawned = 0;
            int rejected = 0;
            for (int i = 0; i < safeCount; i++)
            {
                int slot = _macroSlotScratch[i];
                if ((uint)slot >= (uint)safeCapacity)
                {
                    rejected++;
                    continue;
                }

                AmbientBiotaState current = states[slot];
                if ((current.StateFlags & AmbientBiotaState.FlagActive) != 0u)
                {
                    rejected++;
                    continue;
                }

                aups[slot] = _macroAupScratch[i];
                velocities[slot] = _macroVelocityScratch[i];
                states[slot] = _macroStateScratch[i];
                spawned++;
            }

            if (counters.IsCreated && counters.Length >= MacroHydrationCounterCount)
            {
                counters[0] = spawned;
                counters[1] = requested;
                counters[2] = math.max(0, requested - spawned);
                counters[3] = invalid + rejected;
            }

            return spawned;
        }

        private int StageMacroDehydrationSlots(
            NativeArray<AbsoluteUniversePosition> aups,
            NativeArray<AmbientBiotaState> states,
            int safeCapacity,
            in AbsoluteUniversePosition centerAup,
            double radiusSq)
        {
            if (!aups.IsCreated || !states.IsCreated || safeCapacity <= 0)
                return 0;

            int staged = 0;
            for (int i = 0; i < safeCapacity && staged < _macroSlotScratch.Length; i++)
            {
                AmbientBiotaState state = states[i];
                if ((state.StateFlags & (AmbientBiotaState.FlagActive | AmbientBiotaState.FlagMacroHydrated)) !=
                    (AmbientBiotaState.FlagActive | AmbientBiotaState.FlagMacroHydrated))
                {
                    continue;
                }

                AbsoluteUniversePosition sampleAup = aups[i];
                double3 delta = DeltaMeters(in sampleAup, in centerAup);
                double distSq = math.dot(delta, delta);
                if (!math.isfinite(distSq) || distSq > radiusSq)
                    continue;

                _macroSlotScratch[staged++] = i;
            }

            return staged;
        }

        private int CommitMacroDehydrationSlots(
            NativeArray<AbsoluteUniversePosition> aups,
            NativeArray<float4> velocities,
            NativeArray<AmbientBiotaState> states,
            NativeArray<int> counters,
            int stagedCount,
            in AbsoluteUniversePosition centerAup,
            double radiusSq)
        {
            ClearMacroCounters(counters);
            int safeCapacity = math.min(_capacity, math.min(aups.Length, math.min(velocities.Length, states.Length)));
            int safeCount = math.min(stagedCount, math.min(safeCapacity, _macroSlotScratch.Length));
            int released = 0;
            uint hash = 2166136261u;
            for (int i = 0; i < safeCount; i++)
            {
                int slot = _macroSlotScratch[i];
                if ((uint)slot >= (uint)safeCapacity)
                    continue;

                AmbientBiotaState state = states[slot];
                if ((state.StateFlags & (AmbientBiotaState.FlagActive | AmbientBiotaState.FlagMacroHydrated)) !=
                    (AmbientBiotaState.FlagActive | AmbientBiotaState.FlagMacroHydrated))
                {
                    continue;
                }

                AbsoluteUniversePosition sampleAup = aups[slot];
                double3 delta = DeltaMeters(in sampleAup, in centerAup);
                double distSq = math.dot(delta, delta);
                if (!math.isfinite(distSq) || distSq > radiusSq)
                    continue;

                hash = Hash32(hash ^ state.StableHash);
                states[slot] = default;
                aups[slot] = default;
                velocities[slot] = default;
                released++;
            }

            if (counters.IsCreated && counters.Length >= MacroHydrationCounterCount)
            {
                counters[0] = released;
                counters[1] = unchecked((int)hash);
            }

            return released;
        }

        private int FindInactiveMacroStagingSlot(
            NativeArray<AmbientBiotaState> states,
            int safeCapacity,
            ref int searchStart,
            int stageGeneration)
        {
            for (int scanned = 0; scanned < safeCapacity; scanned++)
            {
                int index = searchStart + scanned;
                if (index >= safeCapacity)
                    index -= safeCapacity;

                AmbientBiotaState state = states[index];
                if ((state.StateFlags & AmbientBiotaState.FlagActive) != 0u ||
                    _macroSlotMarkScratch[index] == stageGeneration)
                {
                    continue;
                }

                searchStart = index + 1;
                if (searchStart >= safeCapacity)
                    searchStart = 0;
                return index;
            }

            return -1;
        }

        private int BeginMacroSlotStageGeneration(int safeCapacity)
        {
            _macroSlotMarkGeneration++;
            if (_macroSlotMarkGeneration != 0)
                return _macroSlotMarkGeneration;

            int length = math.min(safeCapacity, _macroSlotMarkScratch.Length);
            for (int i = 0; i < length; i++)
                _macroSlotMarkScratch[i] = 0;

            _macroSlotMarkGeneration = 1;
            return _macroSlotMarkGeneration;
        }

        private static bool IsValidMacroSwarmForHydration(in MacroSwarm swarm)
        {
            return swarm.HashId != 0u &&
                   math.isfinite(swarm.BiomassValue) &&
                   math.isfinite(swarm.Speed) &&
                   math.all(math.isfinite(swarm.CurrentSectorAup)) &&
                   swarm.BiomassValue > 0.0001f;
        }

        private static double3 ResolveMacroHydrationSpawnOffset(uint hash, float radius, float visualBudget01)
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

        private static float3 ResolveMacroHydrationSpawnVelocity(uint hash, float visualBudget01)
        {
            float scalar = math.lerp(0.08f, 0.18f, visualBudget01);
            return new float3(
                (((hash >> 3) & 255u) * (1f / 255f) - 0.5f) * scalar,
                (((hash >> 11) & 255u) * (1f / 255f)) * scalar,
                (((hash >> 19) & 255u) * (1f / 255f) - 0.5f) * scalar);
        }

        private void CacheDependencies()
        {
            RefreshRegistryDependencies();
        }

        private void RefreshRegistryDependencies()
        {
            RebindDataVaultForLifecycle(GlobalRegistry.DataVault);

            if (_ecosystem == null || !_ecosystem.IsInitialized)
                _ecosystem = GlobalRegistry.EcosystemDirector;

            if (_bucketer == null || !_bucketer.IsInitialized)
                _bucketer = GlobalRegistry.SimulationBucketer;

            if (_abyssalFlowReadModel == null)
                _abyssalFlowReadModel = GlobalRegistry.AbyssalFlowVolume;

            if (_playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;
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
                    RebindDataVaultForLifecycle(currentService is IDataVault currentVault ? currentVault : null);
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

                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
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
            if (!IsOwnedVaultHandle(in _biotaAupHandle, BufferID.BiotaAUPs) || capacityChanged)
            {
                _biotaAupHandle = ClaimVaultBuffer<AbsoluteUniversePosition>(
                    vault,
                    BufferID.BiotaAUPs,
                    desiredCapacity,
                    NativeArrayOptions.ClearMemory);
            }

            if (!IsOwnedVaultHandle(in _biotaVelocityHandle, BufferID.BiotaVelocities) || capacityChanged)
            {
                _biotaVelocityHandle = ClaimVaultBuffer<float4>(
                    vault,
                    BufferID.BiotaVelocities,
                    desiredCapacity,
                    NativeArrayOptions.ClearMemory);
            }

            if (!IsOwnedVaultHandle(in _biotaStateHandle, BufferID.BiotaStates) || capacityChanged)
            {
                _biotaStateHandle = ClaimVaultBuffer<AmbientBiotaState>(
                    vault,
                    BufferID.BiotaStates,
                    desiredCapacity,
                    NativeArrayOptions.ClearMemory);
            }

            if (!IsOwnedVaultHandle(in _macroHydrationCounterHandle, BufferID.BiotaMacroHydrationCounters))
            {
                _macroHydrationCounterHandle = ClaimVaultBuffer<int>(
                    vault,
                    BufferID.BiotaMacroHydrationCounters,
                    MacroHydrationCounterCount,
                    NativeArrayOptions.ClearMemory);
            }

            if (!IsOwnedVaultHandle(in _telemetryRingHandle, BufferID.BiotaTelemetryRing))
            {
                _telemetryRingHandle = ClaimVaultBuffer<AmbientBiotaTelemetryEntry>(
                    vault,
                    BufferID.BiotaTelemetryRing,
                    BlackBoxFrameCount,
                    NativeArrayOptions.ClearMemory);
            }

            if (!IsOwnedVaultHandle(in _telemetryCursorHandle, BufferID.BiotaTelemetryCursor))
            {
                _telemetryCursorHandle = ClaimVaultBuffer<int>(
                    vault,
                    BufferID.BiotaTelemetryCursor,
                    1,
                    NativeArrayOptions.ClearMemory);
            }

            bool ready = IsOwnedVaultHandle(in _biotaAupHandle, BufferID.BiotaAUPs) &&
                         IsOwnedVaultHandle(in _biotaVelocityHandle, BufferID.BiotaVelocities) &&
                         IsOwnedVaultHandle(in _biotaStateHandle, BufferID.BiotaStates) &&
                         IsOwnedVaultHandle(in _macroHydrationCounterHandle, BufferID.BiotaMacroHydrationCounters) &&
                         IsOwnedVaultHandle(in _telemetryRingHandle, BufferID.BiotaTelemetryRing) &&
                         IsOwnedVaultHandle(in _telemetryCursorHandle, BufferID.BiotaTelemetryCursor) &&
                         TryResolveBiotaBuffers(desiredCapacity, out _, out _, out _) &&
                         TryResolveMacroCounters(out _) &&
                         TryResolveTelemetryBuffers(out _, out _);
            if (!ready)
                return false;

            if (!EnsureMacroScratchCapacity(desiredCapacity))
                return false;

            if (capacityChanged)
            {
                ResetCapacityDependentRuntimeState();
            }

            _capacity = desiredCapacity;
            if (enableIndirectDraw && EnsureIndirectPresentationAssetsOrReport())
                TryEnsureGraphicsResourcesCold(_capacity);
            return true;
        }

        /// <summary>
        /// Cold gate in front of the indirect-draw GPU allocation. Returns true only when the draw could
        /// actually reach the rasteriser, and reports the exact authoring gap once per component otherwise.
        /// <para>
        /// Without this gate <c>enableIndirectDraw</c> alone allocated the render payload:
        /// <see cref="EnsureGraphicsResources"/> creates two structured buffers of
        /// <c>capacity * sizeof(AmbientBiotaGpuInstance)</c> (64 B per instance - three float4 plus four
        /// uint) plus two indirect-args buffers. At <c>precisionCapacity = 8192</c> that is 1 MB of VRAM
        /// held for the whole session. <see cref="RenderIndirectBiota"/> then returns at its
        /// <c>material == null</c> / <c>!TryResolveDrawMesh</c> guard on every single
        /// <see cref="LateFrameTick"/>, so not one byte of it is ever sampled.
        /// </para>
        /// <para>
        /// Both fields are <c>[SerializeField]</c> and neither has a runtime fallback, so this is decided
        /// at enable time and cannot be repaired by a runtime-created host: a code-only owner has no
        /// inspector to receive them, <c>Resources.Load</c> is forbidden by project law, and
        /// <c>Shader.Find</c> cannot recover the shader in a player build because
        /// Hecton_AmbientBiotaIndirect.shader (guid 6f2d4d4b1f134e5cb3e9a7d01a5219c8) is referenced by no
        /// material anywhere in Assets/ and is absent from m_AlwaysIncludedShaders in
        /// ProjectSettings/GraphicsSettings.asset, which means it is stripped from the build. The owner
        /// therefore has to be authored - into 02_HECTON_WORLD or onto a prefab - with both fields wired.
        /// </para>
        /// <para>
        /// The report is what makes the failure loud instead of silent. The draw guard is a bare
        /// <c>return</c> on a hot tick, so an owner authored with an unassigned material renders nothing
        /// and says nothing while <see cref="IsInitialized"/> and <see cref="ActiveBiotaCount"/> both
        /// report a healthy system. <c>H8Debug.LogError</c> carries
        /// <c>[Conditional("UNITY_EDITOR")]</c> + <c>[Conditional("DEVELOPMENT_BUILD")]</c> on the
        /// message+context overload (H8Debug.cs:74-80), so the call and its literal argument are stripped
        /// from release IL - no release-build cost and no string allocation on any path.
        /// </para>
        /// </summary>
        /// <returns>True when both indirect presentation assets are assigned.</returns>
        private bool EnsureIndirectPresentationAssetsOrReport()
        {
            bool hasMaterial = biotaMaterial != null;
            bool hasMesh = TryResolveDrawMesh(out _);
            if (hasMaterial && hasMesh)
            {
                _presentationAssetGapReported = false;
                return true;
            }

            if (_presentationAssetGapReported)
                return false;

            _presentationAssetGapReported = true;
            if (!hasMaterial && !hasMesh)
            {
                Hecton8.Core.H8Debug.LogError(
                    "[AmbientBiotaDirector] Indirect draw enabled with no biotaMaterial and no biotaQuadMesh. Ambient biota will simulate and stay invisible. Author a Material on shader 'Hecton8/Ambient/BiotaIndirect' and a quad Mesh, assign both on this component, and keep the shader reachable from the build.",
                    this);
            }
            else if (!hasMaterial)
            {
                Hecton8.Core.H8Debug.LogError(
                    "[AmbientBiotaDirector] Indirect draw enabled with no biotaMaterial. Ambient biota will simulate and stay invisible. Author a Material on shader 'Hecton8/Ambient/BiotaIndirect' and assign it to biotaMaterial.",
                    this);
            }
            else
            {
                Hecton8.Core.H8Debug.LogError(
                    "[AmbientBiotaDirector] Indirect draw enabled with no biotaQuadMesh. Ambient biota will simulate and stay invisible. Assign a quad Mesh with submesh 0 index data to biotaQuadMesh.",
                    this);
            }

            return false;
        }

        private void RebindDataVaultForLifecycle(IDataVault currentVault)
        {
            if (ReferenceEquals(_vault, currentVault))
                return;

            CompleteActiveJobForTeardown();
            if (!_jobPending)
                ReleaseBiotaJobBufferPins();
            ReleaseVaultHandles(_vault);
            ClearVaultHandles();
            _vault = currentVault;
            _capacity = 0;
            ResetCapacityDependentRuntimeState();
        }

        private bool HasVaultBuffersReadyNoGrow()
        {
            return _vault != null &&
                   _capacity > 0 &&
                   IsOwnedVaultHandle(in _biotaAupHandle, BufferID.BiotaAUPs) &&
                   IsOwnedVaultHandle(in _biotaVelocityHandle, BufferID.BiotaVelocities) &&
                   IsOwnedVaultHandle(in _biotaStateHandle, BufferID.BiotaStates) &&
                   IsOwnedVaultHandle(in _macroHydrationCounterHandle, BufferID.BiotaMacroHydrationCounters) &&
                   IsOwnedVaultHandle(in _telemetryRingHandle, BufferID.BiotaTelemetryRing) &&
                   IsOwnedVaultHandle(in _telemetryCursorHandle, BufferID.BiotaTelemetryCursor) &&
                   TryResolveBiotaBuffers(_capacity, out _, out _, out _) &&
                   TryResolveMacroCounters(out _) &&
                   TryResolveTelemetryBuffers(out _, out _);
        }

        private static bool IsOwnedVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)SystemID.AmbientBiota;
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
                return vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> existing) &&
                       IsOwnedVaultHandle(in existing, bufferId)
                    ? existing
                    : default;
            }

            return vault.EnsureGenerationHandle<T>(bufferId, length, SystemID.AmbientBiota, options);
        }

        private bool TryOpenVaultView<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _vault;
            return vault != null &&
                   IsOwnedVaultHandle(in handle, expectedBufferId) &&
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

        private bool EnsureMacroScratchCapacity(int desiredCapacity)
        {
            if (desiredCapacity <= 0)
                return false;

            if (_macroSlotScratch != null &&
                _macroSlotMarkScratch != null &&
                _macroAupScratch != null &&
                _macroVelocityScratch != null &&
                _macroStateScratch != null &&
                _macroSlotScratch.Length >= desiredCapacity &&
                _macroSlotMarkScratch.Length >= desiredCapacity &&
                _macroAupScratch.Length >= desiredCapacity &&
                _macroVelocityScratch.Length >= desiredCapacity &&
                _macroStateScratch.Length >= desiredCapacity)
            {
                return true;
            }

            _macroSlotScratch = new int[desiredCapacity]; // COLD ALLOC: owner-local macro staging slots, no per-call GC, owner: AMBIENT_BIOTA_DIRECTOR
            _macroSlotMarkScratch = new int[desiredCapacity]; // COLD ALLOC: generation marks prevent duplicate slot staging, owner: AMBIENT_BIOTA_DIRECTOR
            _macroAupScratch = new AbsoluteUniversePosition[desiredCapacity]; // COLD ALLOC: staged AUP writes outside Vault guard, owner: AMBIENT_BIOTA_DIRECTOR
            _macroVelocityScratch = new float4[desiredCapacity]; // COLD ALLOC: staged velocity writes outside Vault guard, owner: AMBIENT_BIOTA_DIRECTOR
            _macroStateScratch = new AmbientBiotaState[desiredCapacity]; // COLD ALLOC: staged state writes outside Vault guard, owner: AMBIENT_BIOTA_DIRECTOR
            _macroSlotMarkGeneration = 0;
            return true;
        }

        private bool HasMacroScratchCapacity(int requiredCapacity)
        {
            return requiredCapacity > 0 &&
                   _macroSlotScratch != null &&
                   _macroSlotMarkScratch != null &&
                   _macroAupScratch != null &&
                   _macroVelocityScratch != null &&
                   _macroStateScratch != null &&
                   _macroSlotScratch.Length >= requiredCapacity &&
                   _macroSlotMarkScratch.Length >= requiredCapacity &&
                   _macroAupScratch.Length >= requiredCapacity &&
                   _macroVelocityScratch.Length >= requiredCapacity &&
                   _macroStateScratch.Length >= requiredCapacity;
        }

        private void ReleaseMacroScratch()
        {
            _macroSlotScratch = null;
            _macroSlotMarkScratch = null;
            _macroAupScratch = null;
            _macroVelocityScratch = null;
            _macroStateScratch = null;
            _macroSlotMarkGeneration = 0;
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
            return TryOpenVaultView(in _biotaAupHandle, BufferID.BiotaAUPs, requiredCapacity, out aups);
        }

        private bool TryResolveBiotaVelocityBuffer(out NativeArray<float4> velocities)
        {
            return TryResolveBiotaVelocityBuffer(_capacity, out velocities);
        }

        private bool TryResolveBiotaVelocityBuffer(int requiredCapacity, out NativeArray<float4> velocities)
        {
            return TryOpenVaultView(in _biotaVelocityHandle, BufferID.BiotaVelocities, requiredCapacity, out velocities);
        }

        private bool TryResolveBiotaStateBuffer(out NativeArray<AmbientBiotaState> states)
        {
            return TryResolveBiotaStateBuffer(_capacity, out states);
        }

        private bool TryResolveBiotaStateBuffer(int requiredCapacity, out NativeArray<AmbientBiotaState> states)
        {
            return TryOpenVaultView(in _biotaStateHandle, BufferID.BiotaStates, requiredCapacity, out states);
        }

        private bool TryResolveMacroCounters(out NativeArray<int> counters)
        {
            return TryOpenVaultView(in _macroHydrationCounterHandle, BufferID.BiotaMacroHydrationCounters, MacroHydrationCounterCount, out counters);
        }

        private bool TryResolveTelemetryBuffers(
            out NativeArray<AmbientBiotaTelemetryEntry> telemetryRing,
            out NativeArray<int> telemetryCursor)
        {
            telemetryRing = default;
            telemetryCursor = default;
            return TryOpenVaultView(in _telemetryRingHandle, BufferID.BiotaTelemetryRing, BlackBoxFrameCount, out telemetryRing) &&
                   TryOpenVaultView(in _telemetryCursorHandle, BufferID.BiotaTelemetryCursor, 1, out telemetryCursor);
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

        private bool TryPinBiotaJobBuffers()
        {
            if (_jobBuffersPinned)
                return false;

            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            bool success = false;
            try
            {
                _jobBufferPinVault = vault;
                if (!TryLockBiotaJobBuffer(vault, BufferID.BiotaAUPs, BiotaJobPinAups) ||
                    !TryLockBiotaJobBuffer(vault, BufferID.BiotaVelocities, BiotaJobPinVelocities) ||
                    !TryLockBiotaJobBuffer(vault, BufferID.BiotaStates, BiotaJobPinStates) ||
                    !TryResolveBiotaBuffers(_capacity, out _, out _, out _))
                {
                    return false;
                }

                _jobBuffersPinned = true;
                success = true;
                return true;
            }
            finally
            {
                if (!success)
                    ReleaseBiotaJobBufferPins();
            }
        }

        private void ReleaseBiotaJobBufferPins()
        {
            uint pinMask = _jobBufferPinMask;
            IDataVault vault = _jobBufferPinVault;
            _jobBuffersPinned = false;
            _jobBufferPinVault = null;
            _jobBufferPinMask = 0u;
            if (vault == null || pinMask == 0u)
                return;

            TryUnlockBiotaJobBuffer(vault, pinMask, BiotaJobPinStates, BufferID.BiotaStates);
            TryUnlockBiotaJobBuffer(vault, pinMask, BiotaJobPinVelocities, BufferID.BiotaVelocities);
            TryUnlockBiotaJobBuffer(vault, pinMask, BiotaJobPinAups, BufferID.BiotaAUPs);
        }

        private bool TryLockBiotaJobBuffer(IDataVault vault, BufferID bufferId, uint pinBit)
        {
            if ((_jobBufferPinMask & pinBit) != 0u)
                return true;

            if (vault == null ||
                (_jobBufferPinVault != null && !ReferenceEquals(_jobBufferPinVault, vault)) ||
                !vault.TryLockBuffer(bufferId, SystemID.AmbientBiota))
            {
                return false;
            }

            _jobBufferPinVault = vault;
            _jobBufferPinMask |= pinBit;
            return true;
        }

        private static void TryUnlockBiotaJobBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, SystemID.AmbientBiota);
        }

        private bool TryAcquireAmbientMutationGuard(ulong mask, out IDataVault guardVault)
        {
            guardVault = null;
            IDataVault vault = _vault;
            if (vault == null ||
                mask == 0UL ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(mask))
            {
                return false;
            }

            guardVault = vault;
            return true;
        }

        private static void ReleaseAmbientMutationGuard(IDataVault guardVault, ulong mask)
        {
            guardVault?.ReleaseMutationGuard(mask);
        }

        private static ulong AmbientBiotaMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _biotaAupHandle, BufferID.BiotaAUPs);
            ReleaseVaultHandle(vault, ref _biotaVelocityHandle, BufferID.BiotaVelocities);
            ReleaseVaultHandle(vault, ref _biotaStateHandle, BufferID.BiotaStates);
            ReleaseVaultHandle(vault, ref _macroHydrationCounterHandle, BufferID.BiotaMacroHydrationCounters);
            ReleaseVaultHandle(vault, ref _telemetryRingHandle, BufferID.BiotaTelemetryRing);
            ReleaseVaultHandle(vault, ref _telemetryCursorHandle, BufferID.BiotaTelemetryCursor);
        }

        private static void ReleaseVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID expectedBufferId)
            where T : struct
        {
            if (vault != null &&
                IsOwnedVaultHandle(in handle, expectedBufferId))
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
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
            int requestedCapacity = math.clamp((int)math.round(requested), 128, 32768);
            int quantizedCapacity = ((requestedCapacity + (CapacityResizeGranularity >> 1)) / CapacityResizeGranularity) * CapacityResizeGranularity;
            return math.clamp(quantizedCapacity, 128, 32768);
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
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null ||
                !playerContext.TryGetPlayerPoseSnapshot(out pose))
            {
                pose = default;
                return false;
            }

            if ((pose.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !math.all(math.isfinite(pose.RuntimePosition)) ||
                !IsFiniteAup(in pose.Aup))
            {
                pose = default;
                return false;
            }

            pose.Forward = SanitizeForward(pose.Forward, new float3(0f, 0f, 1f));
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
            if (material == null ||
                !TryResolveDrawMesh(out Mesh mesh) ||
                !HasGraphicsResources(_capacity))
            {
                return;
            }

            int requestedDrawCount = math.min(_activeBiotaCount, _capacity);
            if ((_gpuPayloadDirty || payloadDirty) && !UploadGpuPayload(aups, velocities, states, _capacity, requestedDrawCount))
                return;

            int drawCount = math.clamp(_gpuVisibleInstanceCount, 0, requestedDrawCount);
            if (drawCount <= 0)
                return;

            if (!TryResolveGpuReadBuffer(out GraphicsBuffer instanceBuffer) ||
                !UploadIndirectArgs(mesh, drawCount, out GraphicsBuffer indirectArgsBuffer))
            {
                return;
            }

            if (_biotaDrawProperties == null)
                return;

            PublishBiotaDrawProperties(_biotaDrawProperties, instanceBuffer, drawCount);

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
                motionVectorMode = MotionVectorGenerationMode.Object,
                matProps = _biotaDrawProperties
            };
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, mesh, indirectArgsBuffer, 1, 0);
        }

        private bool EnsureGraphicsResources(int capacity)
        {
            if (capacity <= 0)
                return false;

            if (HasGraphicsResources(capacity))
                return true;

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

        private bool TryEnsureGraphicsResourcesCold(int capacity)
        {
            try
            {
                return EnsureGraphicsResources(capacity);
            }
            catch (InvalidOperationException)
            {
                ReleaseGraphicsResources();
                return false;
            }
            catch (ArgumentException)
            {
                ReleaseGraphicsResources();
                return false;
            }
            catch (UnityException)
            {
                ReleaseGraphicsResources();
                return false;
            }
        }

        private bool HasGraphicsResources(int capacity)
        {
            return capacity > 0 &&
                   _gpuBufferCapacity >= capacity &&
                   AreGraphicsBuffersValid(capacity) &&
                   AreIndirectArgsBuffersValid();
        }

        private void PublishBiotaDrawProperties(
            MaterialPropertyBlock properties,
            GraphicsBuffer instanceBuffer,
            int drawCount)
        {
            if (!_biotaDrawMaterialPublished || !ReferenceEquals(_publishedBiotaInstanceBuffer, instanceBuffer))
            {
                properties.SetBuffer(BiotaInstancesShaderId, instanceBuffer);
                _publishedBiotaInstanceBuffer = instanceBuffer;
            }

            if (!_biotaDrawMaterialPublished || _publishedBiotaCapacity != _capacity)
            {
                properties.SetInt(BiotaCapacityShaderId, _capacity);
                _publishedBiotaCapacity = _capacity;
            }

            if (!_biotaDrawMaterialPublished || _publishedBiotaActiveCount != drawCount)
            {
                properties.SetInt(BiotaActiveCountShaderId, drawCount);
                _publishedBiotaActiveCount = drawCount;
            }

            if (!_biotaDrawMaterialPublished || _publishedBiotaBiomeHash != _currentBiomeHash)
            {
                properties.SetFloat(BiotaBiomeHashShaderId, (float)_currentBiomeHash);
                _publishedBiotaBiomeHash = _currentBiomeHash;
            }

            if (!_biotaDrawMaterialPublished || _publishedBiotaQualityWeight != _cachedQualityWeight01)
            {
                properties.SetFloat(BiotaQualityWeightShaderId, _cachedQualityWeight01);
                _publishedBiotaQualityWeight = _cachedQualityWeight01;
            }

            if (!_biotaDrawMaterialPublished || _publishedBiotaSystemStress != _cachedSystemStress01)
            {
                properties.SetFloat(BiotaSystemStressShaderId, _cachedSystemStress01);
                _publishedBiotaSystemStress = _cachedSystemStress01;
            }

            Vector4 flowVector = new Vector4(_flowVector.x, _flowVector.y, _flowVector.z, 0f);
            if (!_biotaDrawMaterialPublished || !SameVector4(in _publishedBiotaFlowVector, in flowVector))
            {
                properties.SetVector(BiotaFlowVectorShaderId, flowVector);
                _publishedBiotaFlowVector = flowVector;
            }

            if (!_biotaDrawMaterialPublished || _publishedBiotaOverkill != _visualOverkillWeight01)
            {
                properties.SetFloat(BiotaOverkillShaderId, _visualOverkillWeight01);
                _publishedBiotaOverkill = _visualOverkillWeight01;
            }

            if (!_biotaDrawMaterialPublished || _publishedBiotaVisualTime != _telemetryClockSeconds)
            {
                properties.SetFloat(BiotaVisualTimeShaderId, _telemetryClockSeconds);
                _publishedBiotaVisualTime = _telemetryClockSeconds;
            }

            Vector4 originWs = new Vector4(_lastPlayerRuntimePosition.x, _lastPlayerRuntimePosition.y, _lastPlayerRuntimePosition.z, 1f);
            if (!_biotaDrawMaterialPublished || !SameVector4(in _publishedBiotaOriginWs, in originWs))
            {
                properties.SetVector(BiotaOriginWsShaderId, originWs);
                _publishedBiotaOriginWs = originWs;
            }

            _biotaDrawMaterialPublished = true;
        }

        private static bool SameVector4(in Vector4 left, in Vector4 right)
        {
            return left.x == right.x &&
                   left.y == right.y &&
                   left.z == right.z &&
                   left.w == right.w;
        }

        private void ResetBiotaDrawMaterialCache()
        {
            _publishedBiotaInstanceBuffer = null;
            _publishedBiotaCapacity = -1;
            _publishedBiotaActiveCount = -1;
            _publishedBiotaBiomeHash = 0u;
            _publishedBiotaQualityWeight = -1f;
            _publishedBiotaSystemStress = -1f;
            _publishedBiotaOverkill = -1f;
            _publishedBiotaVisualTime = -1f;
            _publishedBiotaFlowVector = default;
            _publishedBiotaOriginWs = default;
            _biotaDrawMaterialPublished = false;
            if (_biotaDrawProperties != null)
                _biotaDrawProperties.Clear();
        }

        private void EnsureBiotaDrawPropertiesCold()
        {
            if (_biotaDrawProperties != null)
                return;

            _biotaDrawProperties = new MaterialPropertyBlock();
        }

        private static GraphicsBuffer CreateIndirectArgsBuffer()
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
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
            int capacity,
            int targetActiveCount)
        {
            int writeIndex = 1 - _gpuBufferIndex;
            if (!TryResolveGpuBuffer(writeIndex, out GraphicsBuffer instanceBuffer))
                return false;

            bool uploaded = UploadPackedGpuInstances(instanceBuffer, aups, velocities, states, capacity, targetActiveCount, out int visibleCount);
            if (!uploaded)
                return false;

            _gpuBufferIndex = writeIndex;
            _gpuVisibleInstanceCount = visibleCount;
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
                GraphicsBuffer.IndirectDrawIndexedArgs indirectArgs = default;
                indirectArgs.indexCountPerInstance = indexCount;
                indirectArgs.instanceCount = (uint)capacity;
                indirectArgs.startIndex = startIndex;
                indirectArgs.baseVertexIndex = baseVertexIndex;
                indirectArgs.startInstance = 0u;
                argsWrite[0] = indirectArgs;
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
            int capacity,
            int targetActiveCount,
            out int visibleCount)
        {
            visibleCount = 0;
            int sourceLength = math.min(aups.IsCreated ? aups.Length : 0, math.min(velocities.IsCreated ? velocities.Length : 0, states.IsCreated ? states.Length : 0));
            int safeCount = ResolveSafeWriteCount(destination, sourceLength, capacity, UnsafeUtility.SizeOf<AmbientBiotaGpuInstance>());
            if (safeCount <= 0)
                return false;

            int targetCount = math.clamp(targetActiveCount, 0, safeCount);
            if (targetCount <= 0)
                return true;

            var mapped = destination.LockBufferForWrite<AmbientBiotaGpuInstance>(0, targetCount);
            try
            {
                int writeIndex = 0;
                for (int i = 0; i < sourceLength && writeIndex < targetCount; i++)
                {
                    AbsoluteUniversePosition aup = aups[i];
                    AmbientBiotaState state = states[i];
                    if (!TryBuildGpuInstance(in aup, velocities[i], in state, in _lastPlayerAup, out AmbientBiotaGpuInstance instance))
                        continue;

                    mapped[writeIndex] = instance;
                    writeIndex++;
                }

                visibleCount = writeIndex;
            }
            finally
            {
                destination.UnlockBufferAfterWrite<AmbientBiotaGpuInstance>(targetCount);
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

        private static bool TryBuildGpuInstance(
            in AbsoluteUniversePosition aup,
            float4 velocity,
            in AmbientBiotaState state,
            in AbsoluteUniversePosition centerAup,
            out AmbientBiotaGpuInstance instance)
        {
            instance = default;
            if ((state.StateFlags & AmbientBiotaState.FlagActive) == 0u)
                return false;

            double3 deltaMeters = DeltaMeters(in aup, in centerAup);
            bool deltaFinite = TryResolveFiniteLocalDelta(deltaMeters, 10000.0d, out float3 localMeters);
            bool velocityFinite = math.all(math.isfinite(velocity));
            bool stateFinite = math.isfinite(state.AgeSeconds) &&
                               math.isfinite(state.LifetimeSeconds) &&
                               math.isfinite(state.ScaleMeters) &&
                               math.isfinite(state.Emission01);
            bool active = deltaFinite &&
                          velocityFinite &&
                          stateFinite &&
                          state.ScaleMeters > 0f;
            if (!active)
                return false;

            float4 safeVelocity = velocityFinite ? velocity : float4.zero;
            float safeLifetime = math.max(0.001f, math.select(1f, state.LifetimeSeconds, math.isfinite(state.LifetimeSeconds)));
            float safeAge = math.select(0f, state.AgeSeconds, math.isfinite(state.AgeSeconds));
            float age01 = math.saturate(safeAge * math.rcp(safeLifetime));
            float hash01 = (state.StableHash & 0xFFFFu) * (1f / 65535f);

            instance = new AmbientBiotaGpuInstance
            {
                PositionScale = new float4(localMeters, math.max(0.001f, state.ScaleMeters)),
                VelocityEmission = new float4(safeVelocity.x, safeVelocity.y, safeVelocity.z, math.saturate(state.Emission01)),
                VisualParams = new float4(age01, safeAge, hash01, 1f),
                StateFlags = state.StateFlags,
                StableHash = state.StableHash,
                SpeciesBucket = ((uint)state.BucketId << 16) | (uint)state.SpeciesId,
                Reserved = state.Reserved
            };
            return true;
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
            return mesh != null;
        }

        private static void EnsureSignalLanesReady()
        {
            SignalBus<BiomeChangedSignal>.EnsureInitialized();
            SignalBus<EntitySpawnSignal>.EnsureInitialized();
            SignalBus<DebrisSpawnSignal>.EnsureInitialized();
        }

        private void ReleaseGraphicsResources()
        {
            ReleaseBuffer(ref _gpuInstanceBufferA);
            ReleaseBuffer(ref _gpuInstanceBufferB);
            ReleaseBuffer(ref _indirectArgsBufferA);
            ReleaseBuffer(ref _indirectArgsBufferB);
            _gpuBufferCapacity = 0;
            _gpuBufferIndex = 0;
            _gpuVisibleInstanceCount = 0;
            _indirectArgsBufferIndex = 0;
            _gpuPayloadDirty = true;
            _indirectArgsMesh = null;
            _indirectArgsCapacity = -1;
            ResetBiotaDrawMaterialCache();

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

            try
            {
                DispatcherJobFence.BeginPostSimulationSwapWindow();
                try
                {
                    if (!DispatcherJobFence.TryComplete(ref _activeJobHandle, forceComplete: true))
                        return;
                }
                finally
                {
                    DispatcherJobFence.EndPostSimulationSwapWindow();
                }

                _jobPending = false;
            }
            finally
            {
                if (!_jobPending)
                    ReleaseBiotaJobBufferPins();
            }
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
            if (_pendingDebrisDrainActive)
                flags = (ushort)(flags | TelemetryFlagPendingDebris);
            _lastTelemetryFlags = flags;
        }

        private void WriteTelemetryHeartbeat()
        {
            if (!TryAcquireAmbientMutationGuard(TelemetryMutationGuardMask, out IDataVault guardVault))
                return;

            bool queueBlackBoxDump = false;
            try
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
                    queueBlackBoxDump = TryStageBlackBoxDump(ring, index);
                }
            }
            catch (System.Exception e)
            {
                Hecton8.Core.H8Debug.LogError($"[AmbientBiotaDirector] Telemetry heartbeat failed: {e}");
            }
            finally
            {
                ReleaseAmbientMutationGuard(guardVault, TelemetryMutationGuardMask);
            }

            if (queueBlackBoxDump)
                _blackBoxDumped = QueueStagedBlackBoxDump();
        }

        private static bool TryStageBlackBoxDump(NativeArray<AmbientBiotaTelemetryEntry> ring, int cursor)
        {
            if (!ring.IsCreated ||
                Interlocked.CompareExchange(ref s_blackBoxDumpInFlight, 1, 0) != 0)
            {
                return false;
            }

            int count = math.min(ring.Length, BlackBoxFrameCount);
            for (int i = 0; i < count; i++)
                s_blackBoxDumpSnapshot[i] = ring[i];

            s_blackBoxDumpCursor = cursor;
            s_blackBoxDumpCount = count;
            return true;
        }

        private static bool QueueStagedBlackBoxDump()
        {
            try
            {
                return TryWriteBlackBoxSnapshotCold();
            }
            finally
            {
                Interlocked.Exchange(ref s_blackBoxDumpInFlight, 0);
            }
        }

        private static unsafe bool TryWriteBlackBoxSnapshotCold()
        {
            if (s_blackBoxDumpCount <= 0 || s_blackBoxDumpCount > BlackBoxFrameCount)
                return false;

            const int headerBytes = 24;
            uint hash = 2166136261u ^ DirectorSourceHash ^ (uint)s_blackBoxDumpCount ^ (uint)s_blackBoxDumpCursor;
            int entryBytes = UnsafeUtility.SizeOf<AmbientBiotaTelemetryEntry>();
            int byteCount = headerBytes + (entryBytes * s_blackBoxDumpCount);
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(AmbientBiotaDirector),
                    BlackBoxDumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                for (int i = 0; i < s_blackBoxDumpCount; i++)
                {
                    AmbientBiotaTelemetryEntry entry = s_blackBoxDumpSnapshot[i];
                    byte* source = (byte*)UnsafeUtility.AddressOf(ref entry);
                    byte* row = destination + headerBytes + (i * entryBytes);
                    UnsafeUtility.MemCpy(row, source, entryBytes);
                    for (int byteIndex = 0; byteIndex < entryBytes; byteIndex++)
                        hash = (hash ^ row[byteIndex]) * 16777619u;
                }

                uint nonZeroHash = hash == 0u ? 2166136261u : hash;
                WriteUInt32Le(destination, 0, DirectorSourceHash);
                WriteUInt32Le(destination, 4, 1u);
                WriteUInt32Le(destination, 8, (uint)s_blackBoxDumpCount);
                WriteUInt32Le(destination, 12, (uint)math.max(0, s_blackBoxDumpCursor));
                WriteUInt32Le(destination, 16, (uint)entryBytes);
                WriteUInt32Le(destination, 20, nonZeroHash);

                if (!NativeFaultDumpWriter.TryWriteAll(BlackBoxDumpRelativePath, payload, byteCount))
                    return false;

                s_blackBoxDumpHash = nonZeroHash;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(AmbientBiotaDirector),
                    BlackBoxDumpPayloadLabel);
            }
        }

        private static unsafe void WriteUInt32Le(byte* target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct AmbientBiotaGpuInstance
        {
            [FieldOffset(0)] public float4 PositionScale;
            [FieldOffset(16)] public float4 VelocityEmission;
            [FieldOffset(32)] public float4 VisualParams;
            [FieldOffset(48)] public uint StateFlags;
            [FieldOffset(52)] public uint StableHash;
            [FieldOffset(56)] public uint SpeciesBucket;
            [FieldOffset(60)] public uint Reserved;
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
                        StateFlags = AmbientBiotaState.FlagActive,
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
                bool active = (state.StateFlags & AmbientBiotaState.FlagActive) != 0u;
                bool bucketActive = ((int)state.BucketId & BucketMask) == (ActiveBucket & BucketMask);
                bool validDeltaTime = math.isfinite(DeltaTime) & DeltaTime > 0f;
                if (!active || !bucketActive || !validDeltaTime)
                    return;

                AbsoluteUniversePosition originalAup = Aups[index];
                float4 originalPackedVelocity = Velocities[index];
                bool shouldSimulate = true;
                float safeDeltaTime = DeltaTime;
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
                float3 relative = ResolveFiniteLocalDeltaOrZero(deltaFromPlayer, 10000.0d, ref faultSanitized);
                float3 outward = SafeNormalize(relative, float3.zero, ref faultSanitized);
                float3 playerForward = SafeNormalize(PlayerForward, new float3(0f, 0f, 1f), ref faultSanitized);
                float frontDot = math.dot(outward, playerForward);
                bool frontDotFinite = math.isfinite(frontDot);
                float avoidanceWeight01 = math.select(0f, visualOverkill01, frontDotFinite & (frontDot > HeadlightConeDot));
                bool shouldAvoidLight = shouldSimulate & (avoidanceWeight01 > 0.0001f);
                faultSanitized |= shouldSimulate & (visualOverkill01 > 0.0001f) & !frontDotFinite;
                targetVelocity += math.select(float3.zero, outward * AvoidanceMetersPerSecond * avoidanceWeight01, shouldAvoidLight);
                uint stateFlagsWithoutReactive = state.StateFlags & ~AmbientStateHeadlightReactiveFlag;
                state.StateFlags = math.select(
                    stateFlagsWithoutReactive,
                    stateFlagsWithoutReactive | AmbientStateHeadlightReactiveFlag,
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

        private static bool TryResolveFiniteLocalDelta(double3 deltaMeters, double maxAbsMeters, out float3 localMeters)
        {
            localMeters = default;
            bool finite = IsFinite(deltaMeters) &&
                          math.isfinite(maxAbsMeters) &&
                          maxAbsMeters > 0.0d &&
                          math.abs(deltaMeters.x) <= maxAbsMeters &&
                          math.abs(deltaMeters.y) <= maxAbsMeters &&
                          math.abs(deltaMeters.z) <= maxAbsMeters;
            if (!finite)
                return false;

            localMeters = new float3((float)deltaMeters.x, (float)deltaMeters.y, (float)deltaMeters.z);
            return math.all(math.isfinite(localMeters));
        }

        private static float3 ResolveFiniteLocalDeltaOrZero(double3 deltaMeters, double maxAbsMeters, ref bool faultSanitized)
        {
            if (TryResolveFiniteLocalDelta(deltaMeters, maxAbsMeters, out float3 localMeters))
                return localMeters;

            faultSanitized = true;
            return float3.zero;
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

        private static float ResolveMacroVisualQualityWeight01(in AbsoluteUniversePosition centerAup, byte qualityByte, float systemStress01)
        {
            if (!IsFiniteAup(in centerAup) || !math.isfinite(systemStress01))
                return 0f;

            float quality01 = qualityByte * (1f / 255f);
            float stressPressure01 = SmoothStep01(math.saturate((systemStress01 - 0.62f) * math.rcp(0.38f)));
            return math.saturate(quality01 * (1f - stressPressure01));
        }

        private static byte EncodeMacroVisualQualitySignalByte(float qualityWeight01)
        {
            return (byte)math.clamp((int)math.round(math.saturate(qualityWeight01) * 255f), 0, 255);
        }

        private static byte EncodeMacroSurvivalPressureSignalByte(float survivalPressure01)
        {
            return (byte)math.clamp((int)math.round(math.saturate(survivalPressure01) * 255f), 0, 255);
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
