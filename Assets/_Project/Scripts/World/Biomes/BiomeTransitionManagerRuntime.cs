using System;
using System.IO;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World.Biomes
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4315)]
    public sealed class BiomeTransitionManagerRuntime : MonoBehaviour, IFastTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        internal static BiomeTransitionManagerRuntime ActiveRuntimeInstance;

        private const string CsvRelativePath = "Assets/_Project/Data/World/biome_atmosphere_rules.csv";
        private const string BlackBoxDumpPath = "Docs/AgentLogs/Dump_BIOME_MANAGER.bin";
        private const uint RuntimeContextHash = 0x42313232u;
        private const uint NonFiniteStateHash = 0x424E414Eu;
        private const float QualityHysteresisBand = 0.015f;
        private const float QualityDowngradeStepPerFrame = 1f / 60f;
        private const float QualityUpgradeStepPerFrame = 1f / 180f;
        private const uint SelfAuditLayoutFault = 1u << 0;
        private const uint SelfAuditSnapshotMissing = 1u << 1;
        private const uint SelfAuditWeightFault = 1u << 2;
        private const uint SelfAuditBlendCountFault = 1u << 3;
        private const SystemID OwnerSystem = SystemID.WorldStreaming;
        private const uint MockTraversalPeriodFrames = 600u;

        private static readonly int BiomeLightingParametersCBufferId = Shader.PropertyToID("H8BiomeLightingParameters");
        private static readonly int H8FogColorId = Shader.PropertyToID("_H8FogColor");
        private static readonly int H8FogDensityId = Shader.PropertyToID("_H8FogDensity");
        private static readonly int H8GlobalQualityWeightId = Shader.PropertyToID("_H8GlobalQualityWeight");
        private static readonly int H8ExtinctionCoefficientsId = Shader.PropertyToID("_H8ExtinctionCoefficients");
        private static readonly int BiomeTransitionFogColorId = Shader.PropertyToID("_H8BiomeTransitionFogColor");
        private static readonly int BiomeTransitionAbsorptionId = Shader.PropertyToID("_H8BiomeTransitionAbsorption");
        private static readonly int BiomeTransitionAudioId = Shader.PropertyToID("_H8BiomeTransitionAudio");
        private static readonly int BiomeTransitionWeightsId = Shader.PropertyToID("_H8BiomeTransitionWeights");
        private static readonly int BiomeTransitionHashesId = Shader.PropertyToID("_H8BiomeTransitionHashes");
        private static readonly int BiomeTransitionDitherId = Shader.PropertyToID("_H8BiomeTransitionDither");

        [Header("AUP Source")]
        [SerializeField] private Transform playerTransform;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos;
        [SerializeField] private bool forceMockTraversal;
        [SerializeField] private float mockTraversalPhase01;
        [SerializeField] private uint _debugDominantBiomeHash;
        [SerializeField] private int _debugBlendCount;
        [SerializeField] private float _debugQualityWeight;
        [SerializeField] private float _debugWeightSum;

        private IDataVault _vault;
        private IPlayerRuntimeContext _playerContext;
        private VaultGenerationHandle<BiomeStateDTO> _statesHandle;
        private VaultGenerationHandle<BiomeCenterDTO> _centersHandle;
        private VaultGenerationHandle<BiomeInfluenceDTO> _influenceHandle;
        private VaultGenerationHandle<CurrentAtmosphereDTO> _currentAtmosphereHandle;
        private VaultGenerationHandle<BiomeBlendMaskDTO> _blendMaskHandle;
        private VaultGenerationHandle<float4> _shaderPayloadHandle;
        private VaultGenerationHandle<BiomeAcousticStageDTO> _acousticStageHandle;
        private VaultGenerationHandle<BiomeTransitionTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<BiomeTransitionCounterDTO> _countersHandle;
        private VaultGenerationHandle<BiomeTransitionTuningDTO> _tuningHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<AbsoluteUniversePositionBlit128> _mockCameraAupHandle;

        private GraphicsBuffer _biomeLightingBufferA;
        private GraphicsBuffer _biomeLightingBufferB;
        private GraphicsBuffer _activeBiomeLightingBuffer;
        private JobHandle _pipelineHandle;
        private JobHandle _seedHandle;
        private long _pipelineScheduleTicks;
        private int _biomeLightingWriteIndex;
        private uint _lastBiomeLightingParametersHash;
        private uint _lastShaderGlobalPayloadHash;
        private bool _pipelineScheduled;
        private bool _pendingShaderPayloadUpload;
        private bool _hasUploadedBiomeLightingParameters;
        private bool _hasUploadedShaderGlobalPayload;
        private bool _seedScheduled;
        private bool _seedCsvAttempted;
        private bool _seedFallbackScheduled;
        private bool _tuningInitialized;
        private bool _registeredFastTick;
        private bool _registeredLateFrame;
        private bool _originShiftRegistered;
        private bool _registeredHotSwapListener;
        private bool _vaultReady;
        private bool _seededBiomeData;
        private bool _coldSupportsSetConstantBuffer;
        private uint _lastOriginShiftSequence;
        private uint _simulationFrameCounter;
        private uint _lastScheduledFrame;
        private uint _qualityFilterFrame;
        private float _filteredQualityWeight;
        private bool _qualityFilterInitialized;
        private AbsoluteUniversePositionBlit128 _lastPlayerAup;
        private readonly BiomeTransitionTelemetryEntry[] _blackBoxDumpSnapshot = new BiomeTransitionTelemetryEntry[BiomeTransitionConstants.TelemetryCapacity];
        private int _blackBoxDumpSnapshotCount;
        private int _blackBoxDumpInFlight;
        private string _blackBoxDumpRootCold;

        private static readonly WaitCallback s_blackBoxDumpWorker = WriteBlackBoxDumpWorker;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        private void Awake()
        {
            if (!TryClaimActiveRuntime())
                return;

            CacheGraphicsCapabilitiesCold();
            EnsureShaderPayloadBuffersCold();
            ResolveColdDependencies();
            EnsureVaultBuffers();
        }

        private void OnEnable()
        {
            if (!TryClaimActiveRuntime())
                return;

            CacheGraphicsCapabilitiesCold();
            EnsureShaderPayloadBuffersCold();
            TryRegisterHotSwapListener();
            ResolveColdDependencies();
            EnsureVaultBuffers();
            TryRegisterTickables();
            TryRegisterOriginShift();
        }

        private void Start()
        {
            ResolveColdDependencies();
            EnsureVaultBuffers();
            TryRegisterTickables();
            TryRegisterOriginShift();
            TrySeedBiomeData();
        }

        private void OnDisable()
        {
            TryUnregisterTickables();
            TryUnregisterOriginShift();
            TryUnregisterHotSwapListener();
            ClearVaultBinding();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void OnDestroy()
        {
            TryUnregisterTickables();
            TryUnregisterOriginShift();
            TryUnregisterHotSwapListener();
            ClearVaultBinding();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        public void FastTick(float deltaTime)
        {
            if (!Application.isPlaying)
                return;

            TryFinalizeCompletedPipeline();
            if (_pipelineScheduled)
                return;

            if (!_vaultReady)
                return;

            TryFinalizeSeedBiomeData();
            if (!_seededBiomeData)
                return;

            if (!TryResolveRuntimeBuffers(
                    out NativeArray<BiomeStateDTO> states,
                    out NativeArray<BiomeCenterDTO> centers,
                    out NativeArray<BiomeInfluenceDTO> influence,
                    out NativeArray<CurrentAtmosphereDTO> currentAtmosphere,
                    out NativeArray<BiomeBlendMaskDTO> blendMask,
                    out NativeArray<float4> shaderPayload,
                    out NativeArray<BiomeAcousticStageDTO> acousticStage,
                    out NativeArray<BiomeTransitionTelemetryEntry> telemetry,
                    out NativeArray<BiomeTransitionCounterDTO> counters,
                    out NativeArray<BiomeTransitionTuningDTO> tuning,
                    out NativeArray<AbsoluteUniversePositionBlit128> mockCameraAup))
            {
                return;
            }

            BiomeTransitionTuningDTO activeTuning = tuning.Length > 0 ? tuning[0] : CreateDefaultTuning();
            float rawQuality = ResolveQualityWeight(in activeTuning);
            uint frame = ResolveSimulationFrame();
            float quality = ResolveFilteredQualityWeight(rawQuality, frame);
            int cadenceFrameStep = ResolveCadenceFrameStep(in activeTuning, quality);
            bool useMockTraversal = forceMockTraversal || activeTuning.MockTraversalEnabled > 0.5f;
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                if (!useMockTraversal)
                    return;

                playerAup = AbsoluteUniversePosition.FromAbsolutePosition(double3.zero);
            }

            AbsoluteUniversePositionBlit128 playerBlit = playerAup.ToAlignedBlit();
            if (_pendingShaderPayloadUpload)
            {
                RecordCadenceSkippedTelemetry(
                    telemetry,
                    counters,
                    blendMask,
                    mockCameraAup,
                    centers,
                    in playerBlit,
                    useMockTraversal,
                    frame);
                return;
            }

            if (_lastScheduledFrame != 0u && unchecked(frame - _lastScheduledFrame) < (uint)cadenceFrameStep)
            {
                RecordCadenceSkippedTelemetry(
                    telemetry,
                    counters,
                    blendMask,
                    mockCameraAup,
                    centers,
                    in playerBlit,
                    useMockTraversal,
                    frame);
                return;
            }

            _lastScheduledFrame = frame;
            JobHandle inputDependency = default;
            if (useMockTraversal)
            {
                inputDependency = ScheduleMockTraversal(mockCameraAup, centers, counters, frame);
            }

            _lastPlayerAup = playerBlit;
            SchedulePipeline(
                states,
                centers,
                influence,
                currentAtmosphere,
                blendMask,
                shaderPayload,
                acousticStage,
                telemetry,
                counters,
                mockCameraAup,
                playerBlit,
                in activeTuning,
                quality,
                useMockTraversal,
                frame,
                inputDependency);
        }

        private void RecordCadenceSkippedTelemetry(
            NativeArray<BiomeTransitionTelemetryEntry> telemetry,
            NativeArray<BiomeTransitionCounterDTO> counters,
            NativeArray<BiomeBlendMaskDTO> blendMask,
            NativeArray<AbsoluteUniversePositionBlit128> mockCameraAup,
            NativeArray<BiomeCenterDTO> centers,
            in AbsoluteUniversePositionBlit128 playerAup,
            bool useMockPlayerAup,
            uint frame)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0 || !counters.IsCreated || counters.Length == 0)
                return;

            BiomeTransitionCounterDTO counter = counters[0];
            int cursor = math.clamp(counter.TelemetryCursor, 0, telemetry.Length - 1);
            BiomeBlendMaskDTO mask = blendMask.IsCreated && blendMask.Length > 0 ? blendMask[0] : default;
            uint stateHash = BiomeTransitionMath.HashState(in mask, frame);
            AbsoluteUniversePositionBlit128 resolvedPlayerAup = playerAup;
            if (useMockPlayerAup)
            {
                resolvedPlayerAup = ResolveMockTraversalBlit(centers, counters, frame);
                if (mockCameraAup.IsCreated && mockCameraAup.Length > 0)
                    mockCameraAup[0] = resolvedPlayerAup;
            }

            uint dominantBiomeHash = counter.CurrentDominantBiomeHash != 0u
                ? counter.CurrentDominantBiomeHash
                : mask.DominantBiomeHash;

            BiomeTransitionTelemetryEntry entry;
            entry.PlayerAup = BiomeTransitionMath.ToAup(in resolvedPlayerAup);
            entry.DominantBiomeHash = dominantBiomeHash;
            entry.BlendedBiomeCount = math.clamp(counter.LastBlendCount, 1, BiomeTransitionConstants.MaxBlendBiomes);
            entry.CpuMicroseconds = 0f;
            entry.StateHash = stateHash;
            telemetry[cursor] = entry;

            counter.TelemetryCursor = cursor + 1 >= telemetry.Length ? 0 : cursor + 1;
            counter.LastFrameIndex = frame;
            counter.LastCpuMicroseconds = 0f;
            counter.LastStateHash = stateHash;
            counter.LastFlags |= BiomeTransitionConstants.FlagCadenceReused;
            counters[0] = counter;
        }

        public void LateFrameTick()
        {
            TryFinalizeCompletedPipeline();
            if (!_pendingShaderPayloadUpload)
                return;

            if (TryReadCounters(out BiomeTransitionCounterDTO counters))
            {
                _debugDominantBiomeHash = counters.CurrentDominantBiomeHash;
                _debugBlendCount = counters.LastBlendCount;
                _debugQualityWeight = counters.LastQualityWeight;
                _debugWeightSum = counters.LastWeightSum;
                PublishShaderPayloadToUnityGlobals();
                if ((counters.LastFlags & BiomeTransitionConstants.FlagNonFiniteOutput) != 0u)
                    DumpBlackBox();
            }

            _pendingShaderPayloadUpload = false;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _lastOriginShiftSequence = shiftData.Sequence;
        }

        public void OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                if (currentService is IDataVault vault)
                {
                    BindVault(vault);
                    TrySeedBiomeData();
                }
                else
                {
                    ClearVaultBinding();
                }

                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Player)
                return;

            _playerContext = currentService as IPlayerRuntimeContext;
            if (_playerContext != null && _playerContext.PlayerTransform != null)
                playerTransform = _playerContext.PlayerTransform;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                if (currentService is IDataVault vault)
                    BindVault(vault);
                else
                    ClearVaultBinding();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player && currentService == null)
                _playerContext = null;
        }

        private bool TryClaimActiveRuntime()
        {
            if (!Application.isPlaying)
                return true;

            if (ActiveRuntimeInstance == null)
            {
                ActiveRuntimeInstance = this;
                return true;
            }

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                return true;

            enabled = false;
            return false;
        }

        private void TryRegisterTickables()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredFastTick)
                _registeredFastTick = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterTickables()
        {
            if (_registeredFastTick)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Environment);
                _registeredFastTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }
        }

        private void TryRegisterOriginShift()
        {
            if (_originShiftRegistered || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _originShiftRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterOriginShift()
        {
            if (!_originShiftRegistered)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _originShiftRegistered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void ResolveColdDependencies()
        {
            if (!Application.isPlaying)
                return;

            TryEnsureBlackBoxDumpRootCold();

            if (_vault == null || !_vaultReady)
            {
                IDataVault vault = GlobalRegistry.DataVault;
                if (vault != null)
                    BindVault(vault);
            }

            if (_playerContext == null)
                _playerContext = GlobalRegistry.Player;

            if (_playerContext != null && _playerContext.PlayerTransform != null)
            {
                playerTransform = _playerContext.PlayerTransform;
                return;
            }

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
        }

        private void EnsureVaultBuffers()
        {
            if (_vaultReady && _vault != null)
                return;

            IDataVault vault = _vault;
            if (vault == null)
                return;

            BindVault(vault);
        }

        private void BindVault(IDataVault vault)
        {
            if (vault == null)
                return;

            if (_vaultReady && ReferenceEquals(_vault, vault))
                return;

            if (!ReferenceEquals(_vault, vault))
            {
                if (_vault != null)
                {
                    CompletePipelineForShutdown();
                    ReleaseBiomeVaultHandles(_vault);
                }

                ReleaseShaderPayloadBuffers();
                _tuningInitialized = false;
                _seededBiomeData = false;
                _seedScheduled = false;
                _seedCsvAttempted = false;
                _seedFallbackScheduled = false;
                _vaultReady = false;
                _qualityFilterInitialized = false;
                _qualityFilterFrame = 0u;
                _filteredQualityWeight = 0f;
            }

            _vault = vault;
            _vaultReady =
                EnsureBiomeVaultBuffer(vault, ref _statesHandle, BufferID.BiomeTransitionStates, OwnerSystem, BiomeTransitionConstants.MaxActiveBiomes, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureBiomeVaultBuffer(vault, ref _centersHandle, BufferID.BiomeTransitionCenters, OwnerSystem, BiomeTransitionConstants.MaxActiveBiomes, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureBiomeVaultBuffer(vault, ref _influenceHandle, BufferID.BiomeTransitionInfluences, OwnerSystem, 1, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureBiomeVaultBuffer(vault, ref _currentAtmosphereHandle, BufferID.BiomeTransitionCurrentAtmosphere, OwnerSystem, 1, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureBiomeVaultBuffer(vault, ref _blendMaskHandle, BufferID.BiomeTransitionBlendMask, OwnerSystem, 1, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureBiomeVaultBuffer(vault, ref _shaderPayloadHandle, BufferID.BiomeTransitionShaderPayload, SystemID.GraphicsScalability, BiomeTransitionConstants.ShaderPayloadFloat4Count, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureBiomeVaultBuffer(vault, ref _acousticStageHandle, BufferID.BiomeTransitionAcousticStage, SystemID.Audio, 1, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureBiomeVaultBuffer(vault, ref _telemetryHandle, BufferID.BiomeTransitionTelemetryRing, OwnerSystem, BiomeTransitionConstants.TelemetryCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureBiomeVaultBuffer(vault, ref _countersHandle, BufferID.BiomeTransitionCounters, OwnerSystem, 1, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureBiomeVaultBuffer(vault, ref _tuningHandle, BufferID.BiomeTransitionTuning, OwnerSystem, 1, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureBiomeVaultBuffer(vault, ref _csvScratchHandle, BufferID.BiomeTransitionCsvScratch, OwnerSystem, BiomeTransitionConstants.CsvScratchBytes, NativeArrayOptions.UninitializedMemory, out _) &&
                EnsureBiomeVaultBuffer(vault, ref _mockCameraAupHandle, BufferID.BiomeTransitionMockCameraAup, OwnerSystem, 1, NativeArrayOptions.UninitializedMemory, out _);
            if (!_vaultReady)
            {
                ReleaseBiomeVaultHandles(vault);
                _vault = null;
                return;
            }

            if (_vaultReady)
            {
                SignalBus<BiomeChangedSignal>.EnsureInitialized();
            }

            EnsureTuningDefaultNoRead();
        }

        private void ClearVaultBinding()
        {
            CompletePipelineForShutdown();
            _pendingShaderPayloadUpload = false;
            IDataVault vault = _vault;
            ReleaseBiomeVaultHandles(vault);
            ReleaseShaderPayloadBuffers();
            _vault = null;
            _vaultReady = false;
            _seedScheduled = false;
            _seededBiomeData = false;
            _seedCsvAttempted = false;
            _seedFallbackScheduled = false;
            _tuningInitialized = false;
        }

        private void EnsureTuningDefaultNoRead()
        {
            if (_tuningInitialized || !_vaultReady || _vault == null)
                return;

            BiomeTransitionTuningDTO tuning = CreateDefaultTuning();
            if (!TryWriteSingleBiomeVaultValue(
                    _vault,
                    in _tuningHandle,
                    BufferID.BiomeTransitionTuning,
                    OwnerSystem,
                    in tuning))
            {
                return;
            }

            _tuningInitialized = true;
        }

        private bool TryResolveRuntimeBuffers(
            out NativeArray<BiomeStateDTO> states,
            out NativeArray<BiomeCenterDTO> centers,
            out NativeArray<BiomeInfluenceDTO> influence,
            out NativeArray<CurrentAtmosphereDTO> currentAtmosphere,
            out NativeArray<BiomeBlendMaskDTO> blendMask,
            out NativeArray<float4> shaderPayload,
            out NativeArray<BiomeAcousticStageDTO> acousticStage,
            out NativeArray<BiomeTransitionTelemetryEntry> telemetry,
            out NativeArray<BiomeTransitionCounterDTO> counters,
            out NativeArray<BiomeTransitionTuningDTO> tuning,
            out NativeArray<AbsoluteUniversePositionBlit128> mockCameraAup)
        {
            states = default;
            centers = default;
            influence = default;
            currentAtmosphere = default;
            blendMask = default;
            shaderPayload = default;
            acousticStage = default;
            telemetry = default;
            counters = default;
            tuning = default;
            mockCameraAup = default;

            IDataVault vault = _vault;
            if (vault == null || !_vaultReady)
                return false;

            return TryResolveBiomeVaultBuffer(vault, ref _statesHandle, BufferID.BiomeTransitionStates, OwnerSystem, BiomeTransitionConstants.MaxActiveBiomes, out states) &&
                   TryResolveBiomeVaultBuffer(vault, ref _centersHandle, BufferID.BiomeTransitionCenters, OwnerSystem, BiomeTransitionConstants.MaxActiveBiomes, out centers) &&
                   TryResolveBiomeVaultBuffer(vault, ref _influenceHandle, BufferID.BiomeTransitionInfluences, OwnerSystem, 1, out influence) &&
                   TryResolveBiomeVaultBuffer(vault, ref _currentAtmosphereHandle, BufferID.BiomeTransitionCurrentAtmosphere, OwnerSystem, 1, out currentAtmosphere) &&
                   TryResolveBiomeVaultBuffer(vault, ref _blendMaskHandle, BufferID.BiomeTransitionBlendMask, OwnerSystem, 1, out blendMask) &&
                   TryResolveBiomeVaultBuffer(vault, ref _shaderPayloadHandle, BufferID.BiomeTransitionShaderPayload, SystemID.GraphicsScalability, BiomeTransitionConstants.ShaderPayloadFloat4Count, out shaderPayload) &&
                   TryResolveBiomeVaultBuffer(vault, ref _acousticStageHandle, BufferID.BiomeTransitionAcousticStage, SystemID.Audio, 1, out acousticStage) &&
                   TryResolveBiomeVaultBuffer(vault, ref _telemetryHandle, BufferID.BiomeTransitionTelemetryRing, OwnerSystem, BiomeTransitionConstants.TelemetryCapacity, out telemetry) &&
                   TryResolveBiomeVaultBuffer(vault, ref _countersHandle, BufferID.BiomeTransitionCounters, OwnerSystem, 1, out counters) &&
                   TryResolveBiomeVaultBuffer(vault, ref _tuningHandle, BufferID.BiomeTransitionTuning, OwnerSystem, 1, out tuning) &&
                   TryResolveBiomeVaultBuffer(vault, ref _mockCameraAupHandle, BufferID.BiomeTransitionMockCameraAup, OwnerSystem, 1, out mockCameraAup);
        }

        private void TrySeedBiomeData()
        {
            if (_seededBiomeData || !_vaultReady)
                return;

            TryFinalizeSeedBiomeData();
            if (_seededBiomeData || _seedScheduled)
                return;

            if (!TryResolveRuntimeBuffers(
                    out NativeArray<BiomeStateDTO> states,
                    out NativeArray<BiomeCenterDTO> centers,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out NativeArray<BiomeTransitionCounterDTO> counters,
                    out _,
                    out _))
            {
                return;
            }

            EnsureTuningDefaultNoRead();

#if UNITY_EDITOR
            if (!_seedCsvAttempted && TryScheduleCsvRules(states, centers, counters, out JobHandle csvHandle))
            {
                _seedCsvAttempted = true;
                if (TryScheduleEmergencyMockBiomes(
                        states,
                        centers,
                        counters,
                        out _seedHandle,
                        csvHandle,
                        onlyWhenCounterEmpty: true))
                {
                    _seedFallbackScheduled = true;
                }
                else
                {
                    _seedHandle = csvHandle;
                    _seedFallbackScheduled = false;
                }

                _seedScheduled = true;
                H8Memory.RegisterActiveJob(OwnerSystem, _seedHandle);
                return;
            }
#endif

            if (!_seedFallbackScheduled && TryScheduleEmergencyMockBiomes(states, centers, counters, out _seedHandle))
            {
                _seedFallbackScheduled = true;
                _seedScheduled = true;
                H8Memory.RegisterActiveJob(OwnerSystem, _seedHandle);
            }
        }

        private void TryFinalizeSeedBiomeData()
        {
            if (_seededBiomeData || !_seedScheduled || !_seedHandle.IsCompleted)
                return;

            if (!DispatcherJobSwap.TryFinalizeCompleted(ref _seedHandle))
                return;

            _seedScheduled = false;
            if (TryReadCounters(out BiomeTransitionCounterDTO seededCounters) && seededCounters.ActiveBiomeCount > 0)
            {
                _seededBiomeData = true;
                return;
            }

            _seedFallbackScheduled = false;
        }

#if UNITY_EDITOR
        private bool TryScheduleCsvRules(
            NativeArray<BiomeStateDTO> states,
            NativeArray<BiomeCenterDTO> centers,
            NativeArray<BiomeTransitionCounterDTO> counters,
            out JobHandle handle)
        {
            handle = default;
            if (_vault == null)
                return false;

            string fullPath = Path.Combine(ProjectRoot(), CsvRelativePath);
            if (!File.Exists(fullPath))
                return false;

            if (!TryResolveBiomeVaultBuffer(_vault, ref _csvScratchHandle, BufferID.BiomeTransitionCsvScratch, OwnerSystem, BiomeTransitionConstants.CsvScratchBytes, out NativeArray<byte> scratch))
                return false;

            int bytesRead = ReadFileIntoNativeScratch(fullPath, scratch);
            if (bytesRead <= 0)
                return false;

            var parseJob = new BiomeAtmosphereCsvIngestJob
            {
                CsvBytes = scratch,
                States = states,
                Centers = centers,
                Counters = counters,
                ByteLength = bytesRead
            };
            handle = parseJob.Schedule();
            return true;
        }
#endif

        private bool TryScheduleEmergencyMockBiomes(
            NativeArray<BiomeStateDTO> states,
            NativeArray<BiomeCenterDTO> centers,
            NativeArray<BiomeTransitionCounterDTO> counters,
            out JobHandle handle,
            JobHandle dependency = default,
            bool onlyWhenCounterEmpty = false)
        {
            handle = default;
            if (!states.IsCreated || !centers.IsCreated || states.Length < 4 || centers.Length < 4)
                return false;

            double3 origin = ResolveMockOriginAup();
            var mockJob = new BuildEmergencyMockBiomesJob
            {
                States = states,
                Centers = centers,
                Counters = counters,
                OriginAup = origin,
                OnlyWhenCounterEmpty = onlyWhenCounterEmpty ? (byte)1 : (byte)0
            };
            handle = mockJob.Schedule(dependency);
            return true;
        }

        private JobHandle ScheduleMockTraversal(
            NativeArray<AbsoluteUniversePositionBlit128> mockCameraAup,
            NativeArray<BiomeCenterDTO> centers,
            NativeArray<BiomeTransitionCounterDTO> counters,
            uint frame)
        {
            ResolveMockTraversalEndpoints(centers, counters, out double3 start, out double3 end);
            float phase = ResolveMockTraversalPhase(frame);
            mockTraversalPhase01 = phase;

            var mockJob = new MockCameraTraversalJob
            {
                OutputAup = mockCameraAup,
                StartAup = start,
                EndAup = end,
                Phase01 = phase
            };
            return mockJob.Schedule();
        }

        private AbsoluteUniversePositionBlit128 ResolveMockTraversalBlit(
            NativeArray<BiomeCenterDTO> centers,
            NativeArray<BiomeTransitionCounterDTO> counters,
            uint frame)
        {
            ResolveMockTraversalEndpoints(centers, counters, out double3 start, out double3 end);
            float phase = ResolveMockTraversalPhase(frame);
            mockTraversalPhase01 = phase;
            float t = BiomeTransitionMath.Smooth01(phase - math.floor(phase));
            return BiomeTransitionMath.ToBlit(math.lerp(start, end, (double)t));
        }

        private void ResolveMockTraversalEndpoints(
            NativeArray<BiomeCenterDTO> centers,
            NativeArray<BiomeTransitionCounterDTO> counters,
            out double3 start,
            out double3 end)
        {
            int active = centers.IsCreated ? centers.Length : 0;
            if (active > 0 && counters.IsCreated && counters.Length > 0)
                active = math.clamp(counters[0].ActiveBiomeCount, 1, active);

            start = centers.IsCreated && active > 0 ? centers[0].CenterAup : ResolveMockOriginAup();
            end = centers.IsCreated && active > 1 ? centers[active - 1].CenterAup : start + new double3(3000d, 0d, 0d);
        }

        private static float ResolveMockTraversalPhase(uint frame)
        {
            return (frame % MockTraversalPeriodFrames) * math.rcp((float)MockTraversalPeriodFrames);
        }

        private void SchedulePipeline(
            NativeArray<BiomeStateDTO> states,
            NativeArray<BiomeCenterDTO> centers,
            NativeArray<BiomeInfluenceDTO> influence,
            NativeArray<CurrentAtmosphereDTO> currentAtmosphere,
            NativeArray<BiomeBlendMaskDTO> blendMask,
            NativeArray<float4> shaderPayload,
            NativeArray<BiomeAcousticStageDTO> acousticStage,
            NativeArray<BiomeTransitionTelemetryEntry> telemetry,
            NativeArray<BiomeTransitionCounterDTO> counters,
            NativeArray<AbsoluteUniversePositionBlit128> mockCameraAup,
            AbsoluteUniversePositionBlit128 playerAup,
            in BiomeTransitionTuningDTO tuning,
            float quality,
            bool useMockTraversal,
            uint frame,
            JobHandle inputDependency)
        {
            int activeCount = counters.IsCreated && counters.Length > 0 ? math.max(1, counters[0].ActiveBiomeCount) : 1;
            float cpuMicroseconds = EstimateCpuMicroseconds(activeCount, quality);
            var evaluateJob = new EvaluateBiomeProximityJob
            {
                Centers = centers,
                States = states,
                MockPlayerAup = mockCameraAup,
                Influence = influence,
                Counters = counters,
                BiomeChangedWriter = SignalBus<BiomeChangedSignal>.ParallelWriter,
                BiomeChangedWriterBudget = SignalBus<BiomeChangedSignal>.ParallelWriterBudget,
                PlayerAup = playerAup,
                GlobalQualityWeight = quality,
                RadiusScale = math.max(0.0001f, tuning.RadiusScale),
                MaxCenterScanScale = math.saturate(math.select(1f, tuning.MaxCenterScanScale, math.isfinite(tuning.MaxCenterScanScale))),
                FrameIndex = frame,
                UseMockPlayerAup = useMockTraversal ? (byte)1 : (byte)0
            };

            var blendJob = new BlendAtmosphereJob
            {
                States = states,
                Influence = influence,
                CurrentAtmosphere = currentAtmosphere,
                BlendMask = blendMask,
                Counters = counters,
                GlobalQualityWeight = quality,
                DitherStrength = tuning.DitherStrength,
                FrameIndex = frame
            };

            var publishJob = new PublishAtmosphereDataJob
            {
                CurrentAtmosphere = currentAtmosphere,
                BlendMask = blendMask,
                ShaderPayload = shaderPayload
            };

            var acousticJob = new StageAcousticParametersJob
            {
                CurrentAtmosphere = currentAtmosphere,
                BlendMask = blendMask,
                AcousticStage = acousticStage,
                Counters = counters,
                FrameIndex = frame
            };

            var telemetryJob = new RecordBiomeTransitionTelemetryJob
            {
                BlendMask = blendMask,
                MockPlayerAup = mockCameraAup,
                TelemetryRing = telemetry,
                Counters = counters,
                PlayerAup = playerAup,
                CpuMicroseconds = cpuMicroseconds,
                UseMockPlayerAup = useMockTraversal ? (byte)1 : (byte)0
            };

            _pipelineScheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            JobHandle evaluateHandle = evaluateJob.Schedule(inputDependency);
            JobHandle blendHandle = blendJob.Schedule(evaluateHandle);
            JobHandle publishHandle = publishJob.Schedule(blendHandle);
            JobHandle acousticHandle = acousticJob.Schedule(blendHandle);
            JobHandle combined = JobHandle.CombineDependencies(publishHandle, acousticHandle);
            _pipelineHandle = telemetryJob.Schedule(combined);
            _pipelineScheduled = true;
            H8Memory.RegisterActiveJob(OwnerSystem, _pipelineHandle);
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            IPlayerRuntimeContext player = _playerContext;
            if (player != null && player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                playerAup = snapshot.Aup;
                return true;
            }

            return false;
        }

        private float ResolveQualityWeight(in BiomeTransitionTuningDTO tuning)
        {
            float overrideWeight = tuning.HardwareQualityOverride;
            if (math.isfinite(overrideWeight) && overrideWeight >= 0f)
                return math.saturate(overrideWeight);

            float global = HomeostasisBrain.GlobalQualityWeight;
            return BiomeTransitionMath.Sanitize01(global);
        }

        private float ResolveFilteredQualityWeight(float targetQuality, uint frame)
        {
            targetQuality = BiomeTransitionMath.Sanitize01(targetQuality);
            if (!_qualityFilterInitialized)
            {
                _filteredQualityWeight = targetQuality;
                _qualityFilterFrame = frame;
                _qualityFilterInitialized = true;
                return _filteredQualityWeight;
            }

            if (frame < _qualityFilterFrame)
            {
                _filteredQualityWeight = targetQuality;
                _qualityFilterFrame = frame;
                return _filteredQualityWeight;
            }

            uint deltaFrames = frame - _qualityFilterFrame;
            if (deltaFrames == 0u)
                deltaFrames = 1u;
            if (deltaFrames > 180u)
                deltaFrames = 180u;

            _qualityFilterFrame = frame;
            float delta = targetQuality - _filteredQualityWeight;
            float absDelta = math.abs(delta);
            if (absDelta <= QualityHysteresisBand)
                return _filteredQualityWeight;

            float direction = math.select(-1f, 1f, delta > 0f);
            float rate = delta < 0f ? QualityDowngradeStepPerFrame : QualityUpgradeStepPerFrame;
            float allowedStep = rate * deltaFrames;
            float hysteresisAdjustedDelta = delta - (direction * QualityHysteresisBand);
            _filteredQualityWeight = math.saturate(_filteredQualityWeight + math.clamp(hysteresisAdjustedDelta, -allowedStep, allowedStep));
            return _filteredQualityWeight;
        }

        private static int ResolveCadenceFrameStep(in BiomeTransitionTuningDTO tuning, float quality)
        {
            float lowHz = math.max(1f, math.select(5f, tuning.LowCadenceHz, math.isfinite(tuning.LowCadenceHz)));
            float ultraHz = math.max(lowHz, math.select(60f, tuning.UltraCadenceHz, math.isfinite(tuning.UltraCadenceHz)));
            float hz = math.lerp(lowHz, ultraHz, BiomeTransitionMath.Smooth01(quality));
            float frames = 60f * math.rcp(math.max(1f, hz));
            return math.clamp((int)math.round(frames), 1, 60);
        }

        private static float EstimateCpuMicroseconds(int activeBiomeCount, float quality)
        {
            float q = BiomeTransitionMath.Sanitize01(quality);
            float scanScale = math.lerp(0.22f, 1f, BiomeTransitionMath.Smooth01(q));
            float blendCount = math.lerp(1f, 4f, BiomeTransitionMath.Smooth01(q));
            return math.max(0.05f, activeBiomeCount * scanScale * 0.075f + blendCount * 0.11f);
        }

        private uint ResolveSimulationFrame()
        {
            if (SystemDispatcher.TryGetExecutionPipelineXRaySnapshot(null, null, out DispatcherStateDTO dispatcherState) &&
                dispatcherState.CurrentFrame != 0u)
            {
                _simulationFrameCounter = dispatcherState.CurrentFrame;
                return dispatcherState.CurrentFrame;
            }

            _simulationFrameCounter++;
            return _simulationFrameCounter == 0u ? 1u : _simulationFrameCounter;
        }

        private static BiomeTransitionTuningDTO CreateDefaultTuning()
        {
            return new BiomeTransitionTuningDTO
            {
                RadiusScale = 1f,
                HardwareQualityOverride = -1f,
                LowCadenceHz = 5f,
                UltraCadenceHz = 60f,
                DitherStrength = 1f,
                DebugDrawEnabled = 0f,
                MockTraversalEnabled = 0f,
                MaxCenterScanScale = 1f
            };
        }

        private double3 ResolveMockOriginAup()
        {
            if (TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return playerAup.ToAbsoluteDouble3();

            return double3.zero;
        }

        private bool TryReadCounters(out BiomeTransitionCounterDTO counters)
        {
            counters = default;
            if (_vault == null ||
                !TryReadBiomeVaultBuffer(_vault, in _countersHandle, BufferID.BiomeTransitionCounters, OwnerSystem, 1, out NativeArray<BiomeTransitionCounterDTO>.ReadOnly counterArray))
            {
                return false;
            }

            counters = counterArray[0];
            return true;
        }

        private bool TryReadCachedTuning(out BiomeTransitionTuningDTO tuning)
        {
            tuning = default;
            if (_vault == null ||
                !TryReadBiomeVaultBuffer(_vault, in _tuningHandle, BufferID.BiomeTransitionTuning, OwnerSystem, 1, out NativeArray<BiomeTransitionTuningDTO>.ReadOnly tuningArray))
            {
                return false;
            }

            tuning = tuningArray[0];
            return true;
        }

        private void PublishShaderPayloadToUnityGlobals()
        {
            if (_vault == null ||
                !TryResolveBiomeVaultBuffer(_vault, ref _shaderPayloadHandle, BufferID.BiomeTransitionShaderPayload, SystemID.GraphicsScalability, BiomeTransitionConstants.ShaderPayloadFloat4Count, out NativeArray<float4> payload))
            {
                return;
            }

            TryUploadBiomeLightingParametersFromPayload(payload);

            float4 fog = SanitizePayload(payload[0], new float4(0.006f, 0.014f, 0.022f, 1f));
            float4 absorption = SanitizePayload(payload[1], new float4(0.18f, 0.21f, 0.28f, 0.85f));
            float4 audio = SanitizePayload(payload[2], new float4(0.65f, 1f, 1f, 4f));
            float4 weights = SanitizePayload(payload[3], new float4(1f, 0f, 0f, 0f));
            float4 hashes = payload[4];
            float4 dither = SanitizePayload(payload[5], new float4(1f, 1f, 1f, 1f));
            float fogDensity = math.max(0f, absorption.w * 0.04f);
            float qualityWeight = math.saturate(dither.w);
            uint globalPayloadHash = HashShaderGlobalPayload(
                fog,
                absorption,
                audio,
                weights,
                hashes,
                dither,
                fogDensity,
                qualityWeight);
            if (_hasUploadedShaderGlobalPayload &&
                globalPayloadHash == _lastShaderGlobalPayloadHash)
            {
                return;
            }

            Shader.SetGlobalVector(BiomeTransitionFogColorId, ToVector4(fog));
            Shader.SetGlobalVector(BiomeTransitionAbsorptionId, ToVector4(absorption));
            Shader.SetGlobalVector(BiomeTransitionAudioId, ToVector4(audio));
            Shader.SetGlobalVector(BiomeTransitionWeightsId, ToVector4(weights));
            Shader.SetGlobalVector(BiomeTransitionHashesId, ToVector4(hashes));
            Shader.SetGlobalVector(BiomeTransitionDitherId, ToVector4(dither));
            Shader.SetGlobalVector(H8FogColorId, ToVector4(new float4(fog.xyz, fogDensity)));
            Shader.SetGlobalFloat(H8FogDensityId, fogDensity);
            Shader.SetGlobalFloat(H8GlobalQualityWeightId, qualityWeight);
            Shader.SetGlobalVector(H8ExtinctionCoefficientsId, ToVector4(absorption));
            _lastShaderGlobalPayloadHash = globalPayloadHash;
            _hasUploadedShaderGlobalPayload = true;
        }

        private void TryUploadBiomeLightingParametersFromPayload(NativeArray<float4> payload)
        {
            if (!_coldSupportsSetConstantBuffer)
            {
                ReleaseShaderPayloadBuffers();
                return;
            }

            if (!AreShaderPayloadBuffersReady())
                return;

            TryUploadBiomeLightingParametersCBuffer(payload);
        }

        private unsafe void TryUploadBiomeLightingParametersCBuffer(NativeArray<float4> payload)
        {
            BiomeLightingParametersDTO compactPayload = ResolveBiomeLightingParameters(payload);
            uint payloadHash = HashBiomeLightingParameters(in compactPayload);
            if (_hasUploadedBiomeLightingParameters &&
                payloadHash == _lastBiomeLightingParametersHash &&
                _activeBiomeLightingBuffer != null &&
                _activeBiomeLightingBuffer.IsValid())
            {
                return;
            }

            GraphicsBuffer writeBuffer = ResolveNextBiomeLightingParametersBuffer();
            NativeArray<BiomeLightingParametersDTO> mapped =
                writeBuffer.LockBufferForWrite<BiomeLightingParametersDTO>(0, 1);
            try
            {
                void* destination = mapped.GetUnsafePtr();
                void* source = &compactPayload;
                UnsafeUtility.MemCpy(
                    destination,
                    source,
                    BiomeTransitionConstants.BiomeLightingParametersStrideBytes);
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<BiomeLightingParametersDTO>(1);
            }

            _activeBiomeLightingBuffer = writeBuffer;
            Shader.SetGlobalConstantBuffer(
                BiomeLightingParametersCBufferId,
                _activeBiomeLightingBuffer,
                0,
                BiomeTransitionConstants.BiomeLightingParametersStrideBytes);
            _lastBiomeLightingParametersHash = payloadHash;
            _hasUploadedBiomeLightingParameters = true;
        }

        private void EnsureShaderPayloadBuffersCold()
        {
            if (_coldSupportsSetConstantBuffer)
                EnsureShaderPayloadBuffers();
            else
                ReleaseShaderPayloadBuffers();
        }

        private bool EnsureShaderPayloadBuffers()
        {
            if (AreShaderPayloadBuffersReady())
            {
                return true;
            }

            ReleaseShaderPayloadBuffers();
            _biomeLightingWriteIndex = 0;
            // COLD ALLOC: GraphicsBuffer[2 x 64B] - compact biome lighting CBuffer ping-pong - owner: SHINOBU_122.
            _biomeLightingBufferA = new GraphicsBuffer(
                GraphicsBuffer.Target.Constant,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                BiomeTransitionConstants.BiomeLightingParametersStrideBytes);
            _biomeLightingBufferB = new GraphicsBuffer(
                GraphicsBuffer.Target.Constant,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                BiomeTransitionConstants.BiomeLightingParametersStrideBytes);

            bool valid =
                _biomeLightingBufferA.IsValid() &&
                _biomeLightingBufferB.IsValid();
            if (!valid)
                ReleaseShaderPayloadBuffers();
            return valid;
        }

        private bool AreShaderPayloadBuffersReady()
        {
            return _biomeLightingBufferA != null &&
                   _biomeLightingBufferA.IsValid() &&
                   _biomeLightingBufferB != null &&
                   _biomeLightingBufferB.IsValid();
        }

        private GraphicsBuffer ResolveNextBiomeLightingParametersBuffer()
        {
            _biomeLightingWriteIndex ^= 1;
            return _biomeLightingWriteIndex == 0 ? _biomeLightingBufferA : _biomeLightingBufferB;
        }

        private void ReleaseShaderPayloadBuffers()
        {
            _biomeLightingBufferA?.Release();
            _biomeLightingBufferB?.Release();
            _biomeLightingBufferA = null;
            _biomeLightingBufferB = null;
            _activeBiomeLightingBuffer = null;
            _lastBiomeLightingParametersHash = 0u;
            _lastShaderGlobalPayloadHash = 0u;
            _hasUploadedBiomeLightingParameters = false;
            _hasUploadedShaderGlobalPayload = false;
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _coldSupportsSetConstantBuffer = SystemInfo.supportsSetConstantBuffer;
        }

        private static float4 SanitizePayload(float4 value, float4 fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        private static Vector4 ToVector4(float4 value)
        {
            return new Vector4(value.x, value.y, value.z, value.w);
        }

        private static BiomeLightingParametersDTO ResolveBiomeLightingParameters(NativeArray<float4> payload)
        {
            float4 fog = SanitizePayload(payload[0], new float4(0.006f, 0.014f, 0.022f, 1f));
            float4 absorption = SanitizePayload(payload[1], new float4(0.18f, 0.21f, 0.28f, 0.85f));
            float4 audio = SanitizePayload(payload[2], new float4(0.65f, 1f, 1f, 4f));
            float4 weights = SanitizePayload(payload[3], new float4(1f, 0f, 0f, 0f));
            float4 dither = SanitizePayload(payload[5], new float4(1f, 1f, 1f, 1f));
            float fogDensity = math.max(0f, absorption.w * 0.04f);
            float blendFactor = math.saturate(1f - weights.x);
            float qualityWeight = math.saturate(dither.w);
            float lightShaftIntensity = math.saturate(audio.x * (0.5f + qualityWeight * 0.5f));
            float4 resolvedFog = new float4(fog.x, fog.y, fog.z, 1f);

            return new BiomeLightingParametersDTO
            {
                PrimaryFogColor = resolvedFog,
                SecondaryFogColor = resolvedFog,
                FogDensity = fogDensity,
                BlendFactor = blendFactor,
                LightShaftIntensity = lightShaftIntensity,
                _pad0 = qualityWeight,
                _pad1 = 1f,
                _pad2 = 0f,
                _pad3 = 0f,
                _pad4 = 0f
            };
        }

        private static uint HashBiomeLightingParameters(in BiomeLightingParametersDTO value)
        {
            uint hash = 2166136261u;
            hash = HashFloat4(hash, value.PrimaryFogColor);
            hash = HashFloat4(hash, value.SecondaryFogColor);
            hash = HashFloat(hash, value.FogDensity);
            hash = HashFloat(hash, value.BlendFactor);
            hash = HashFloat(hash, value.LightShaftIntensity);
            hash = HashFloat(hash, value._pad0);
            hash = HashFloat(hash, value._pad1);
            return hash == 0u ? 1u : hash;
        }

        private static uint HashShaderGlobalPayload(
            float4 fog,
            float4 absorption,
            float4 audio,
            float4 weights,
            float4 hashes,
            float4 dither,
            float fogDensity,
            float qualityWeight)
        {
            uint hash = 2166136261u;
            hash = HashFloat4(hash, fog);
            hash = HashFloat4(hash, absorption);
            hash = HashFloat4(hash, audio);
            hash = HashFloat4(hash, weights);
            hash = HashFloat4(hash, hashes);
            hash = HashFloat4(hash, dither);
            hash = HashFloat(hash, fogDensity);
            hash = HashFloat(hash, qualityWeight);
            return hash == 0u ? 1u : hash;
        }

        private static uint HashFloat4(uint hash, float4 value)
        {
            hash = HashFloat(hash, value.x);
            hash = HashFloat(hash, value.y);
            hash = HashFloat(hash, value.z);
            return HashFloat(hash, value.w);
        }

        private static uint HashFloat(uint hash, float value)
        {
            hash ^= math.asuint(value);
            return hash * 16777619u;
        }

        private void CompletePipelineForShutdown()
        {
            if (_seedScheduled)
            {
                DispatcherJobSwap.BeginPostSimulationSwapWindow();
                try
                {
                    DispatcherJobSwap.TryComplete(ref _seedHandle, forceComplete: true);
                }
                finally
                {
                    DispatcherJobSwap.EndPostSimulationSwapWindow();
                }

                _seedScheduled = false;
            }

            if (!_pipelineScheduled)
            {
                _pendingShaderPayloadUpload = false;
                _pipelineScheduleTicks = 0L;
                return;
            }

            DispatcherJobSwap.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobSwap.TryComplete(ref _pipelineHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobSwap.EndPostSimulationSwapWindow();
            }

            _pipelineScheduled = false;
            _pendingShaderPayloadUpload = false;
            _pipelineScheduleTicks = 0L;
        }

        private bool TryFinalizeCompletedPipeline()
        {
            if (!_pipelineScheduled || !_pipelineHandle.IsCompleted)
                return false;

            if (!DispatcherJobSwap.TryFinalizeCompleted(ref _pipelineHandle))
                return false;

            _pipelineScheduled = false;
            PatchCompletedPipelineTiming(ResolvePipelineElapsedMicroseconds());
            _pendingShaderPayloadUpload = true;
            return true;
        }

        private float ResolvePipelineElapsedMicroseconds()
        {
            if (_pipelineScheduleTicks <= 0L)
                return 0f;

            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _pipelineScheduleTicks;
            _pipelineScheduleTicks = 0L;
            if (elapsedTicks <= 0L)
                return 0f;

            double microseconds = elapsedTicks * 1000000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (double.IsNaN(microseconds) || double.IsInfinity(microseconds) || microseconds <= 0d)
                return 0f;

            return (float)System.Math.Min(microseconds, 1000000d);
        }

        private void PatchCompletedPipelineTiming(float cpuMicroseconds)
        {
            if (cpuMicroseconds <= 0f ||
                _vault == null)
            {
                return;
            }

            if (!TryResolveBiomeVaultBuffer(_vault, ref _telemetryHandle, BufferID.BiomeTransitionTelemetryRing, OwnerSystem, BiomeTransitionConstants.TelemetryCapacity, out NativeArray<BiomeTransitionTelemetryEntry> telemetry) ||
                !TryResolveBiomeVaultBuffer(_vault, ref _countersHandle, BufferID.BiomeTransitionCounters, OwnerSystem, 1, out NativeArray<BiomeTransitionCounterDTO> counters))
            {
                return;
            }

            BiomeTransitionCounterDTO counter = counters[0];
            int cursor = math.clamp(counter.TelemetryCursor, 0, telemetry.Length - 1);
            int latestIndex = cursor == 0 ? telemetry.Length - 1 : cursor - 1;
            BiomeTransitionTelemetryEntry entry = telemetry[latestIndex];
            entry.CpuMicroseconds = cpuMicroseconds;
            telemetry[latestIndex] = entry;

            counter.LastCpuMicroseconds = cpuMicroseconds;
            counters[0] = counter;
        }

        private unsafe int ReadFileIntoNativeScratch(string fullPath, NativeArray<byte> scratch)
        {
            try
            {
                using FileStream stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                int maxBytes = (int)math.min(stream.Length, (long)scratch.Length);
                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                int total = 0;
                while (total < maxBytes)
                {
                    int read = stream.Read(new Span<byte>(ptr + total, maxBytes - total));
                    if (read <= 0)
                        break;

                    total += read;
                }

                return total;
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogWarning("[BiomeTransitionManagerRuntime] CSV load failed: " + exception.Message, this);
#endif
                return 0;
            }
        }

        private void DumpBlackBox()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return;

            if (Volatile.Read(ref _blackBoxDumpInFlight) != 0)
                return;

            if (_blackBoxDumpRootCold == null || _blackBoxDumpRootCold.Length == 0)
                return;

            if (!TryReadBiomeVaultBuffer(vault, in _telemetryHandle, BufferID.BiomeTransitionTelemetryRing, OwnerSystem, BiomeTransitionConstants.TelemetryCapacity, out NativeArray<BiomeTransitionTelemetryEntry>.ReadOnly telemetry))
                return;

            int cursor = TryReadBiomeVaultBuffer(vault, in _countersHandle, BufferID.BiomeTransitionCounters, OwnerSystem, 1, out NativeArray<BiomeTransitionCounterDTO>.ReadOnly counters)
                ? counters[0].TelemetryCursor
                : 0;

            Volatile.Write(ref _blackBoxDumpInFlight, 1);
            if (!TryStageBlackBoxDumpSnapshot(telemetry, cursor))
            {
                Volatile.Write(ref _blackBoxDumpInFlight, 0);
                return;
            }

            bool queued = false;
            try
            {
                queued = ThreadPool.QueueUserWorkItem(s_blackBoxDumpWorker, this);
            }
            catch (NotSupportedException)
            {
                queued = false;
            }

            if (!queued)
                Volatile.Write(ref _blackBoxDumpInFlight, 0);

            GlobalTelemetryBus.PublishPerformanceWarning(NonFiniteStateHash, RuntimeContextHash, 1f);
        }

        private bool TryStageBlackBoxDumpSnapshot(
            NativeArray<BiomeTransitionTelemetryEntry>.ReadOnly telemetry,
            int cursor)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            BiomeTransitionTelemetryEntry[] snapshot = _blackBoxDumpSnapshot;
            if (snapshot == null || snapshot.Length < BiomeTransitionConstants.TelemetryCapacity)
                return false;

            int count = math.min(telemetry.Length, snapshot.Length);
            int start = math.clamp(cursor, 0, math.max(0, count - 1));
            for (int i = 0; i < count; i++)
            {
                int index = start + i;
                if (index >= count)
                    index -= count;

                snapshot[i] = telemetry[index];
            }

            _blackBoxDumpSnapshotCount = count;
            return true;
        }

        private static void WriteBlackBoxDumpWorker(object state)
        {
            BiomeTransitionManagerRuntime runtime = state as BiomeTransitionManagerRuntime;
            if (runtime == null)
                return;

            try
            {
                runtime.TryWriteBlackBoxSnapshotCold();
            }
            finally
            {
                Volatile.Write(ref runtime._blackBoxDumpInFlight, 0);
            }
        }

        private unsafe bool TryWriteBlackBoxSnapshotCold()
        {
            int count = _blackBoxDumpSnapshotCount;
            if (count <= 0)
                return false;

            string root = _blackBoxDumpRootCold;
            if (root == null || root.Length == 0)
                return false;

            try
            {
                string fullPath = Path.Combine(root, BlackBoxDumpPath);
                int rowBytes = BiomeTransitionConstants.TelemetryStrideBytes;
                int totalBytes = count * rowBytes;
                const string PayloadLabel = "biomeTransitionTelemetryDumpPayload";
                NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(BiomeTransitionManagerRuntime),
                    PayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                try
                {
                    Span<byte> bytes = new Span<byte>(payload.GetUnsafePtr(), totalBytes);
                    int writeOffset = 0;
                    for (int i = 0; i < count; i++)
                    {
                        Span<byte> record = bytes.Slice(writeOffset, rowBytes);
                        record.Clear();
                        WriteTelemetryRecordLittleEndian(record, in _blackBoxDumpSnapshot[i]);
                        writeOffset += rowBytes;
                    }

                    return NativeFaultDumpWriter.TryWriteAll(fullPath, payload, totalBytes);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(BiomeTransitionManagerRuntime),
                        PayloadLabel);
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        private void OnDrawGizmos()
        {
            bool tuningDrawEnabled = TryReadCachedTuning(out BiomeTransitionTuningDTO tuning) && tuning.DebugDrawEnabled > 0.5f;
            if (!drawGizmos && !tuningDrawEnabled)
                return;

            IDataVault vault = _vault;
            if (vault == null)
                return;

            if (!TryResolveBiomeVaultBuffer(vault, ref _centersHandle, BufferID.BiomeTransitionCenters, OwnerSystem, BiomeTransitionConstants.MaxActiveBiomes, out NativeArray<BiomeCenterDTO> centers) ||
                !TryResolveBiomeVaultBuffer(vault, ref _blendMaskHandle, BufferID.BiomeTransitionBlendMask, OwnerSystem, 1, out NativeArray<BiomeBlendMaskDTO> blendMask) ||
                !TryResolveBiomeVaultBuffer(vault, ref _countersHandle, BufferID.BiomeTransitionCounters, OwnerSystem, 1, out NativeArray<BiomeTransitionCounterDTO> counters))
            {
                return;
            }

            int count = math.clamp(counters[0].ActiveBiomeCount, 0, centers.Length);
            for (int i = 0; i < count; i++)
            {
                BiomeCenterDTO center = centers[i];
                if (center.BiomeHash == 0u)
                    continue;

                Vector3 runtime = (Vector3)AbsoluteUniversePosition.FromAbsolutePosition(center.CenterAup).ToRuntimeFloat3();
                Gizmos.color = ColorForHash(center.BiomeHash, 0.18f);
                Gizmos.DrawWireSphere(runtime, math.max(0.1f, center.OuterRadiusMeters));
                Gizmos.color = ColorForHash(center.BiomeHash, 0.55f);
                Gizmos.DrawWireSphere(runtime, math.max(0.1f, center.InnerRadiusMeters));
            }

            if (blendMask.IsCreated && blendMask.Length > 0)
            {
                BiomeBlendMaskDTO mask = blendMask[0];
                DrawContributionLines(in mask, centers, count);
            }
        }

        private void DrawContributionLines(in BiomeBlendMaskDTO mask, NativeArray<BiomeCenterDTO> centers, int count)
        {
            Vector3 origin = playerTransform != null ? playerTransform.position : Vector3.zero;
            DrawLineToHash(mask.BiomeHashes.x, mask.Weights.x, origin, centers, count);
            DrawLineToHash(mask.BiomeHashes.y, mask.Weights.y, origin, centers, count);
            DrawLineToHash(mask.BiomeHashes.z, mask.Weights.z, origin, centers, count);
            DrawLineToHash(mask.BiomeHashes.w, mask.Weights.w, origin, centers, count);
        }

        private static void DrawLineToHash(uint hash, float weight, Vector3 origin, NativeArray<BiomeCenterDTO> centers, int count)
        {
            if (hash == 0u || weight <= 0.0001f)
                return;

            for (int i = 0; i < count; i++)
            {
                if (centers[i].BiomeHash != hash)
                    continue;

                Gizmos.color = ColorForHash(hash, math.saturate(weight));
                Vector3 runtime = (Vector3)AbsoluteUniversePosition.FromAbsolutePosition(centers[i].CenterAup).ToRuntimeFloat3();
                Gizmos.DrawLine(origin, runtime);
                return;
            }
        }

        private static Color ColorForHash(uint hash, float alpha)
        {
            float r = ((hash >> 0) & 255u) * (1f / 255f);
            float g = ((hash >> 8) & 255u) * (1f / 255f);
            float b = ((hash >> 16) & 255u) * (1f / 255f);
            return new Color(math.max(0.1f, r), math.max(0.1f, g), math.max(0.1f, b), math.saturate(alpha));
        }

        private static string ProjectRoot()
        {
            return Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
        }

        private bool TryEnsureBlackBoxDumpRootCold()
        {
            try
            {
                _blackBoxDumpRootCold = ProjectRoot();
                return _blackBoxDumpRootCold != null && _blackBoxDumpRootCold.Length > 0;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        public static bool TryReloadCsvFromEditor()
        {
            if (ActiveRuntimeInstance == null)
                return false;

            if (ActiveRuntimeInstance._seedScheduled)
                return false;

            ActiveRuntimeInstance._seededBiomeData = false;
            ActiveRuntimeInstance._seedCsvAttempted = false;
            ActiveRuntimeInstance._seedFallbackScheduled = false;
            ActiveRuntimeInstance.ResolveColdDependencies();
            ActiveRuntimeInstance.EnsureVaultBuffers();
            ActiveRuntimeInstance.TrySeedBiomeData();
            return true;
        }

        public static bool TryReadSnapshot(out CurrentAtmosphereDTO atmosphere, out BiomeBlendMaskDTO mask, out BiomeTransitionCounterDTO counters)
        {
            atmosphere = default;
            mask = default;
            counters = default;
            BiomeTransitionManagerRuntime active = ActiveRuntimeInstance;
            if (active == null ||
                !active._vaultReady ||
                active._vault == null ||
                !TryReadBiomeVaultBuffer(active._vault, in active._currentAtmosphereHandle, BufferID.BiomeTransitionCurrentAtmosphere, OwnerSystem, 1, out NativeArray<CurrentAtmosphereDTO>.ReadOnly atmosphereArray) ||
                !TryReadBiomeVaultBuffer(active._vault, in active._blendMaskHandle, BufferID.BiomeTransitionBlendMask, OwnerSystem, 1, out NativeArray<BiomeBlendMaskDTO>.ReadOnly maskArray) ||
                !TryReadBiomeVaultBuffer(active._vault, in active._countersHandle, BufferID.BiomeTransitionCounters, OwnerSystem, 1, out NativeArray<BiomeTransitionCounterDTO>.ReadOnly counterArray))
            {
                return false;
            }

            if (counterArray[0].LastFrameIndex == 0u)
                return false;

            atmosphere = atmosphereArray[0];
            mask = maskArray[0];
            counters = counterArray[0];
            return true;
        }

        public static bool TryReadTuning(out BiomeTransitionTuningDTO tuning)
        {
            tuning = default;
            BiomeTransitionManagerRuntime active = ActiveRuntimeInstance;
            if (active == null ||
                !active._vaultReady ||
                active._vault == null ||
                !TryReadBiomeVaultBuffer(active._vault, in active._tuningHandle, BufferID.BiomeTransitionTuning, OwnerSystem, 1, out NativeArray<BiomeTransitionTuningDTO>.ReadOnly tuningArray))
            {
                return false;
            }

            tuning = tuningArray[0];
            return true;
        }

        public static bool TryRunSelfAudit(out uint faultFlags, out float weightSumError)
        {
            faultFlags = 0u;
            weightSumError = 1f;
            if (!BiomeTransitionNativeLayout.Validate())
                faultFlags |= SelfAuditLayoutFault;

            if (!TryReadSnapshot(
                    out CurrentAtmosphereDTO atmosphere,
                    out BiomeBlendMaskDTO mask,
                    out BiomeTransitionCounterDTO counters))
            {
                faultFlags |= SelfAuditSnapshotMissing;
                return false;
            }

            float maskSum = math.csum(mask.Weights);
            float atmosphereSum = math.csum(atmosphere.NormalizedWeights);
            float maskError = math.abs(maskSum - 1f);
            float atmosphereError = math.abs(atmosphereSum - 1f);
            weightSumError = math.max(maskError, atmosphereError);
            if (!math.isfinite(maskSum) ||
                !math.isfinite(atmosphereSum) ||
                !math.isfinite(weightSumError) ||
                weightSumError > 0.001f)
            {
                faultFlags |= SelfAuditWeightFault;
            }

            if (counters.LastBlendCount < 1 || counters.LastBlendCount > BiomeTransitionConstants.MaxBlendBiomes)
                faultFlags |= SelfAuditBlendCountFault;

            return faultFlags == 0u;
        }

        public static bool TryWriteTuning(in BiomeTransitionTuningDTO tuning)
        {
            BiomeTransitionManagerRuntime active = ActiveRuntimeInstance;
            if (active == null || active._vault == null || !active._vaultReady)
                return false;

            if (!TryWriteSingleBiomeVaultValue(
                    active._vault,
                    in active._tuningHandle,
                    BufferID.BiomeTransitionTuning,
                    OwnerSystem,
                    in tuning))
            {
                return false;
            }

            active._tuningInitialized = true;
            return true;
        }

        public static bool TryDumpBlackBoxFromEditor()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryReadExistingBiomeVaultBuffer(vault, BufferID.BiomeTransitionTelemetryRing, OwnerSystem, BiomeTransitionConstants.TelemetryCapacity, out NativeArray<BiomeTransitionTelemetryEntry>.ReadOnly telemetry))
                return false;

            int cursor = TryReadExistingBiomeVaultBuffer(vault, BufferID.BiomeTransitionCounters, OwnerSystem, 1, out NativeArray<BiomeTransitionCounterDTO>.ReadOnly counters)
                ? counters[0].TelemetryCursor
                : 0;
            return TryDumpTelemetry(telemetry, cursor, ProjectRoot(), BlackBoxDumpPath);
        }

        private void ReleaseBiomeVaultHandles(IDataVault vault)
        {
            ReleaseBiomeVaultHandle(vault, ref _statesHandle, BufferID.BiomeTransitionStates, OwnerSystem);
            ReleaseBiomeVaultHandle(vault, ref _centersHandle, BufferID.BiomeTransitionCenters, OwnerSystem);
            ReleaseBiomeVaultHandle(vault, ref _influenceHandle, BufferID.BiomeTransitionInfluences, OwnerSystem);
            ReleaseBiomeVaultHandle(vault, ref _currentAtmosphereHandle, BufferID.BiomeTransitionCurrentAtmosphere, OwnerSystem);
            ReleaseBiomeVaultHandle(vault, ref _blendMaskHandle, BufferID.BiomeTransitionBlendMask, OwnerSystem);
            ReleaseBiomeVaultHandle(vault, ref _shaderPayloadHandle, BufferID.BiomeTransitionShaderPayload, SystemID.GraphicsScalability);
            ReleaseBiomeVaultHandle(vault, ref _acousticStageHandle, BufferID.BiomeTransitionAcousticStage, SystemID.Audio);
            ReleaseBiomeVaultHandle(vault, ref _telemetryHandle, BufferID.BiomeTransitionTelemetryRing, OwnerSystem);
            ReleaseBiomeVaultHandle(vault, ref _countersHandle, BufferID.BiomeTransitionCounters, OwnerSystem);
            ReleaseBiomeVaultHandle(vault, ref _tuningHandle, BufferID.BiomeTransitionTuning, OwnerSystem);
            ReleaseBiomeVaultHandle(vault, ref _csvScratchHandle, BufferID.BiomeTransitionCsvScratch, OwnerSystem);
            ReleaseBiomeVaultHandle(vault, ref _mockCameraAupHandle, BufferID.BiomeTransitionMockCameraAup, OwnerSystem);
        }

        private static bool EnsureBiomeVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID owner,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (TryResolveBiomeVaultBuffer(vault, ref handle, bufferId, owner, requiredLength, out buffer))
                return true;

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, owner, options);
            return TryResolveBiomeVaultBuffer(vault, ref handle, bufferId, owner, requiredLength, out buffer);
        }

        private static bool TryResolveBiomeVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID owner,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (IsBiomeVaultHandle(in handle, bufferId, owner) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                !IsBiomeVaultHandle(in handle, bufferId, owner) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                handle = default;
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryReadBiomeVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID owner,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsBiomeVaultHandle(in handle, bufferId, owner) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryWriteSingleBiomeVaultValue<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID owner,
            in T value) where T : struct
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsBiomeVaultHandle(in handle, bufferId, owner) ||
                !vault.TryAcquireWriteLock(in handle, owner, out NativeArray<T> buffer) ||
                !buffer.IsCreated ||
                buffer.Length == 0)
            {
                return false;
            }

            try
            {
                buffer[0] = value;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, owner);
            }
        }

        private static bool TryReadExistingBiomeVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            SystemID owner,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                !IsBiomeVaultHandle(in handle, bufferId, owner) ||
                !vault.TryReadOnlyHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsBiomeVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID owner) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)owner &&
                   handle.Generation != 0u;
        }

        private static void ReleaseBiomeVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID owner) where T : struct
        {
            if (vault != null && IsBiomeVaultHandle(in handle, bufferId, owner))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static unsafe bool TryDumpTelemetry(
            NativeArray<BiomeTransitionTelemetryEntry>.ReadOnly telemetry,
            int cursor,
            string root,
            string relativePath)
        {
            try
            {
                string fullPath = Path.Combine(root, relativePath);
                int count = telemetry.Length;
                int rowBytes = BiomeTransitionConstants.TelemetryStrideBytes;
                int totalBytes = count * rowBytes;
                const string PayloadLabel = "biomeTransitionEditorDumpPayload";
                NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(BiomeTransitionManagerRuntime),
                    PayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                try
                {
                    Span<byte> bytes = new Span<byte>(payload.GetUnsafePtr(), totalBytes);
                    int start = math.clamp(cursor, 0, math.max(0, count - 1));
                    int writeOffset = 0;
                    for (int i = 0; i < count; i++)
                    {
                        int index = start + i;
                        if (index >= count)
                            index -= count;

                        BiomeTransitionTelemetryEntry entry = telemetry[index];
                        Span<byte> record = bytes.Slice(writeOffset, rowBytes);
                        record.Clear();
                        WriteTelemetryRecordLittleEndian(record, in entry);
                        writeOffset += rowBytes;
                    }

                    return NativeFaultDumpWriter.TryWriteAll(fullPath, payload, totalBytes);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(BiomeTransitionManagerRuntime),
                        PayloadLabel);
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogError("[BiomeTransitionManagerRuntime] Black-box dump failed: " + exception.Message);
#endif
                return false;
            }
        }

        private static void WriteTelemetryRecordLittleEndian(Span<byte> dst, in BiomeTransitionTelemetryEntry entry)
        {
            WriteInt64LittleEndian(dst, 0, entry.PlayerAup.GridX);
            WriteInt64LittleEndian(dst, 8, entry.PlayerAup.GridY);
            WriteInt64LittleEndian(dst, 16, entry.PlayerAup.GridZ);
            WriteFloatLittleEndian(dst, 24, entry.PlayerAup.LocalX);
            WriteFloatLittleEndian(dst, 28, entry.PlayerAup.LocalY);
            WriteFloatLittleEndian(dst, 32, entry.PlayerAup.LocalZ);
            WriteUInt32LittleEndian(dst, 48, entry.DominantBiomeHash);
            WriteInt32LittleEndian(dst, 52, entry.BlendedBiomeCount);
            WriteFloatLittleEndian(dst, 56, entry.CpuMicroseconds);
            WriteUInt32LittleEndian(dst, 60, entry.StateHash);
        }

        private static void WriteFloatLittleEndian(Span<byte> dst, int offset, float value)
        {
            WriteUInt32LittleEndian(dst, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(Span<byte> dst, int offset, int value)
        {
            WriteUInt32LittleEndian(dst, offset, (uint)value);
        }

        private static void WriteInt64LittleEndian(Span<byte> dst, int offset, long value)
        {
            WriteUInt64LittleEndian(dst, offset, (ulong)value);
        }

        private static void WriteUInt32LittleEndian(Span<byte> dst, int offset, uint value)
        {
            dst[offset] = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
            dst[offset + 2] = (byte)(value >> 16);
            dst[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64LittleEndian(Span<byte> dst, int offset, ulong value)
        {
            dst[offset] = (byte)value;
            dst[offset + 1] = (byte)(value >> 8);
            dst[offset + 2] = (byte)(value >> 16);
            dst[offset + 3] = (byte)(value >> 24);
            dst[offset + 4] = (byte)(value >> 32);
            dst[offset + 5] = (byte)(value >> 40);
            dst[offset + 6] = (byte)(value >> 48);
            dst[offset + 7] = (byte)(value >> 56);
        }
    }
}
