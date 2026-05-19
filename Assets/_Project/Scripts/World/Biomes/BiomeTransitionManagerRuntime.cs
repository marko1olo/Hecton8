using System;
using System.IO;
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
    public sealed class BiomeTransitionManagerRuntime : MonoBehaviour, IFastTickable, ILateFrameTickable, IOriginShiftListener
    {
        internal static BiomeTransitionManagerRuntime ActiveRuntimeInstance;

        private const string CsvRelativePath = "Assets/_Project/Data/World/biome_atmosphere_rules.csv";
        private const string BlackBoxDumpPath = "Docs/AgentLogs/Dump_BIOME_MANAGER.bin";
        private const uint RuntimeContextHash = 0x42313232u;
        private const uint NonFiniteStateHash = 0x424E414Eu;
        private const uint SelfAuditLayoutFault = 1u << 0;
        private const uint SelfAuditSnapshotMissing = 1u << 1;
        private const uint SelfAuditWeightFault = 1u << 2;
        private const uint SelfAuditBlendCountFault = 1u << 3;

        private static readonly int H8FogColorId = Shader.PropertyToID("_H8FogColor");
        private static readonly int H8FogDensityId = Shader.PropertyToID("_H8FogDensity");
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
        private VaultBufferHandle<BiomeStateDTO> _statesHandle;
        private VaultBufferHandle<BiomeCenterDTO> _centersHandle;
        private VaultBufferHandle<BiomeInfluenceDTO> _influenceHandle;
        private VaultBufferHandle<CurrentAtmosphereDTO> _currentAtmosphereHandle;
        private VaultBufferHandle<BiomeBlendMaskDTO> _blendMaskHandle;
        private VaultBufferHandle<float4> _shaderPayloadHandle;
        private VaultBufferHandle<BiomeAcousticStageDTO> _acousticStageHandle;
        private VaultBufferHandle<BiomeTransitionTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<BiomeTransitionCounterDTO> _countersHandle;
        private VaultBufferHandle<BiomeTransitionTuningDTO> _tuningHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;
        private VaultBufferHandle<AbsoluteUniversePositionBlit128> _mockCameraAupHandle;

        private JobHandle _pipelineHandle;
        private JobHandle _seedHandle;
        private bool _pipelineScheduled;
        private bool _seedScheduled;
        private bool _seedCsvAttempted;
        private bool _seedFallbackScheduled;
        private bool _tuningInitialized;
        private bool _registeredFastTick;
        private bool _registeredLateFrame;
        private bool _originShiftRegistered;
        private bool _vaultReady;
        private bool _seededBiomeData;
        private uint _lastOriginShiftSequence;
        private uint _simulationFrameCounter;
        private float _cadenceAccumulator;
        private AbsoluteUniversePositionBlit128 _lastPlayerAup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        private void Awake()
        {
            if (!TryClaimActiveRuntime())
                return;

            EnsureVaultBuffers();
        }

        private void OnEnable()
        {
            if (!TryClaimActiveRuntime())
                return;

            EnsureVaultBuffers();
            TryRegisterTickables();
            TryRegisterOriginShift();
        }

        private void Start()
        {
            EnsureVaultBuffers();
            TrySeedBiomeData();
        }

        private void OnDisable()
        {
            TryUnregisterTickables();
            TryUnregisterOriginShift();
            CompletePipelineForShutdown();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void OnDestroy()
        {
            TryUnregisterTickables();
            TryUnregisterOriginShift();
            CompletePipelineForShutdown();

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        public void FastTick(float deltaTime)
        {
            if (!Application.isPlaying || _pipelineScheduled)
                return;

            EnsureVaultBuffers();
            if (!_vaultReady || !TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            TrySeedBiomeData();
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
            float quality = ResolveQualityWeight(in activeTuning);
            _cadenceAccumulator += math.max(0f, deltaTime);
            float cadenceSeconds = ResolveCadenceSeconds(in activeTuning, quality);
            if (_cadenceAccumulator < cadenceSeconds)
                return;

            _cadenceAccumulator = math.max(0f, _cadenceAccumulator - cadenceSeconds);
            AbsoluteUniversePositionBlit128 playerBlit = playerAup.ToAlignedBlit();
            bool useMockTraversal = forceMockTraversal || activeTuning.MockTraversalEnabled > 0.5f;
            JobHandle inputDependency = default;
            if (useMockTraversal)
            {
                inputDependency = ScheduleMockTraversal(mockCameraAup, centers, counters);
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
                inputDependency);
        }

        public void LateFrameTick()
        {
            if (!_pipelineScheduled || !_pipelineHandle.IsCompleted)
                return;

            _pipelineHandle.Complete();
            _pipelineScheduled = false;

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
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _lastOriginShiftSequence = shiftData.Sequence;
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

        private void EnsureVaultBuffers()
        {
            if (_vaultReady && _vault != null)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null && GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
                vault = latest;
            if (vault == null)
                return;

            if (!ReferenceEquals(_vault, vault))
                _tuningInitialized = false;

            _vault = vault;
            _statesHandle = vault.GetBufferHandle<BiomeStateDTO>(
                BufferID.BiomeTransitionStates,
                BiomeTransitionConstants.MaxActiveBiomes,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            _centersHandle = vault.GetBufferHandle<BiomeCenterDTO>(
                BufferID.BiomeTransitionCenters,
                BiomeTransitionConstants.MaxActiveBiomes,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            _influenceHandle = vault.GetBufferHandle<BiomeInfluenceDTO>(
                BufferID.BiomeTransitionInfluences,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            _currentAtmosphereHandle = vault.GetBufferHandle<CurrentAtmosphereDTO>(
                BufferID.BiomeTransitionCurrentAtmosphere,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            _blendMaskHandle = vault.GetBufferHandle<BiomeBlendMaskDTO>(
                BufferID.BiomeTransitionBlendMask,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            _shaderPayloadHandle = vault.GetBufferHandle<float4>(
                BufferID.BiomeTransitionShaderPayload,
                BiomeTransitionConstants.ShaderPayloadFloat4Count,
                SystemID.GraphicsScalability,
                NativeArrayOptions.UninitializedMemory);
            _acousticStageHandle = vault.GetBufferHandle<BiomeAcousticStageDTO>(
                BufferID.BiomeTransitionAcousticStage,
                1,
                SystemID.Audio,
                NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.GetBufferHandle<BiomeTransitionTelemetryEntry>(
                BufferID.BiomeTransitionTelemetryRing,
                BiomeTransitionConstants.TelemetryCapacity,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            _countersHandle = vault.GetBufferHandle<BiomeTransitionCounterDTO>(
                BufferID.BiomeTransitionCounters,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.GetBufferHandle<BiomeTransitionTuningDTO>(
                BufferID.BiomeTransitionTuning,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = vault.GetBufferHandle<byte>(
                BufferID.BiomeTransitionCsvScratch,
                BiomeTransitionConstants.CsvScratchBytes,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);
            _mockCameraAupHandle = vault.GetBufferHandle<AbsoluteUniversePositionBlit128>(
                BufferID.BiomeTransitionMockCameraAup,
                1,
                SystemID.WorldStreaming,
                NativeArrayOptions.UninitializedMemory);

            _vaultReady = _statesHandle.IsCreated &&
                          _centersHandle.IsCreated &&
                          _influenceHandle.IsCreated &&
                          _currentAtmosphereHandle.IsCreated &&
                          _blendMaskHandle.IsCreated &&
                          _shaderPayloadHandle.IsCreated &&
                          _acousticStageHandle.IsCreated &&
                          _telemetryHandle.IsCreated &&
                          _countersHandle.IsCreated &&
                          _tuningHandle.IsCreated &&
                          _csvScratchHandle.IsCreated &&
                          _mockCameraAupHandle.IsCreated;
            if (_vaultReady)
            {
                NativeQueue<BiomeChangedSignal>.ParallelWriter unusedWriter = SignalBus<BiomeChangedSignal>.ParallelWriter;
                _ = unusedWriter;
            }

            EnsureTuningDefaultNoRead();
        }

        private void EnsureTuningDefaultNoRead()
        {
            if (_tuningInitialized || !_vaultReady || _vault == null || !_tuningHandle.IsCreated)
                return;

            NativeArray<BiomeTransitionTuningDTO> tuning = _tuningHandle.Resolve(_vault);
            if (!tuning.IsCreated || tuning.Length == 0)
                return;

            tuning[0] = CreateDefaultTuning();
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

            IDataVault vault = _vault ?? GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            _vault = vault;
            states = _statesHandle.Resolve(vault);
            centers = _centersHandle.Resolve(vault);
            influence = _influenceHandle.Resolve(vault);
            currentAtmosphere = _currentAtmosphereHandle.Resolve(vault);
            blendMask = _blendMaskHandle.Resolve(vault);
            shaderPayload = _shaderPayloadHandle.Resolve(vault);
            acousticStage = _acousticStageHandle.Resolve(vault);
            telemetry = _telemetryHandle.Resolve(vault);
            counters = _countersHandle.Resolve(vault);
            tuning = _tuningHandle.Resolve(vault);
            mockCameraAup = _mockCameraAupHandle.Resolve(vault);
            return states.IsCreated &&
                   centers.IsCreated &&
                   influence.IsCreated &&
                   currentAtmosphere.IsCreated &&
                   blendMask.IsCreated &&
                   shaderPayload.IsCreated &&
                   acousticStage.IsCreated &&
                   telemetry.IsCreated &&
                   counters.IsCreated &&
                   tuning.IsCreated &&
                   mockCameraAup.IsCreated;
        }

        private void TrySeedBiomeData()
        {
            if (_seededBiomeData || !_vaultReady)
                return;

            if (_seedScheduled && !_seedHandle.IsCompleted)
                return;

            if (_seedScheduled)
            {
                _seedHandle.Complete();
                _seedScheduled = false;
                if (TryReadCounters(out BiomeTransitionCounterDTO seededCounters) && seededCounters.ActiveBiomeCount > 0)
                {
                    _seededBiomeData = true;
                    return;
                }
            }

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
                    out NativeArray<BiomeTransitionTuningDTO> tuning,
                    out _))
            {
                return;
            }

            EnsureTuningDefaultNoRead();

            if (!_seedCsvAttempted && TryScheduleCsvRules(states, centers, counters, out _seedHandle))
            {
                _seedCsvAttempted = true;
                _seedFallbackScheduled = false;
                _seedScheduled = true;
                return;
            }

            if (!_seedFallbackScheduled && TryScheduleEmergencyMockBiomes(states, centers, counters, tuning, out _seedHandle))
            {
                _seedFallbackScheduled = true;
                _seedScheduled = true;
            }
        }

        private bool TryScheduleCsvRules(
            NativeArray<BiomeStateDTO> states,
            NativeArray<BiomeCenterDTO> centers,
            NativeArray<BiomeTransitionCounterDTO> counters,
            out JobHandle handle)
        {
            handle = default;
            if (!_csvScratchHandle.IsCreated || _vault == null)
                return false;

            string fullPath = Path.Combine(ProjectRoot(), CsvRelativePath);
            if (!File.Exists(fullPath))
                return false;

            NativeArray<byte> scratch = _csvScratchHandle.Resolve(_vault);
            if (!scratch.IsCreated || scratch.Length == 0)
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

        private bool TryScheduleEmergencyMockBiomes(
            NativeArray<BiomeStateDTO> states,
            NativeArray<BiomeCenterDTO> centers,
            NativeArray<BiomeTransitionCounterDTO> counters,
            NativeArray<BiomeTransitionTuningDTO> tuning,
            out JobHandle handle)
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
                Tuning = tuning,
                OriginAup = origin
            };
            handle = mockJob.Schedule();
            return true;
        }

        private JobHandle ScheduleMockTraversal(
            NativeArray<AbsoluteUniversePositionBlit128> mockCameraAup,
            NativeArray<BiomeCenterDTO> centers,
            NativeArray<BiomeTransitionCounterDTO> counters)
        {
            int active = counters.IsCreated && counters.Length > 0
                ? math.clamp(counters[0].ActiveBiomeCount, 1, centers.Length)
                : centers.Length;
            double3 start = centers.IsCreated && active > 0 ? centers[0].CenterAup : ResolveMockOriginAup();
            double3 end = centers.IsCreated && active > 1 ? centers[active - 1].CenterAup : start + new double3(3000d, 0d, 0d);
            mockTraversalPhase01 += 1f / 600f;
            if (mockTraversalPhase01 >= 1f)
                mockTraversalPhase01 -= math.floor(mockTraversalPhase01);

            var mockJob = new MockCameraTraversalJob
            {
                OutputAup = mockCameraAup,
                StartAup = start,
                EndAup = end,
                Phase01 = mockTraversalPhase01
            };
            return mockJob.Schedule();
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
            JobHandle inputDependency)
        {
            uint frame = ResolveSimulationFrame();
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

            JobHandle evaluateHandle = evaluateJob.Schedule(inputDependency);
            JobHandle blendHandle = blendJob.Schedule(evaluateHandle);
            JobHandle publishHandle = publishJob.Schedule(blendHandle);
            JobHandle acousticHandle = acousticJob.Schedule(blendHandle);
            JobHandle combined = JobHandle.CombineDependencies(publishHandle, acousticHandle);
            _pipelineHandle = telemetryJob.Schedule(combined);
            _pipelineScheduled = true;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            IPlayerRuntimeContext player = GlobalRegistry.Player;
            if (player != null && player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                playerAup = snapshot.Aup;
                return true;
            }

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (playerTransform == null)
                return false;

            playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerTransform.position);
            return true;
        }

        private float ResolveQualityWeight(in BiomeTransitionTuningDTO tuning)
        {
            float overrideWeight = tuning.HardwareQualityOverride;
            if (math.isfinite(overrideWeight) && overrideWeight >= 0f)
                return math.saturate(overrideWeight);

            float global = HomeostasisBrain.GlobalQualityWeight;
            return BiomeTransitionMath.Sanitize01(global);
        }

        private static float ResolveCadenceSeconds(in BiomeTransitionTuningDTO tuning, float quality)
        {
            float lowHz = math.max(1f, math.select(5f, tuning.LowCadenceHz, math.isfinite(tuning.LowCadenceHz)));
            float ultraHz = math.max(lowHz, math.select(60f, tuning.UltraCadenceHz, math.isfinite(tuning.UltraCadenceHz)));
            float hz = math.lerp(lowHz, ultraHz, BiomeTransitionMath.Smooth01(quality));
            return math.rcp(math.max(1f, hz));
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
            if (_vault == null || !_countersHandle.IsCreated)
                return false;

            NativeArray<BiomeTransitionCounterDTO> counterArray = _countersHandle.Resolve(_vault);
            if (!counterArray.IsCreated || counterArray.Length == 0)
                return false;

            counters = counterArray[0];
            return true;
        }

        private void PublishShaderPayloadToUnityGlobals()
        {
            if (_vault == null || !_shaderPayloadHandle.IsCreated)
                return;

            NativeArray<float4> payload = _shaderPayloadHandle.Resolve(_vault);
            if (!payload.IsCreated || payload.Length < 6)
                return;

            float4 fog = SanitizePayload(payload[0], new float4(0.006f, 0.014f, 0.022f, 1f));
            float4 absorption = SanitizePayload(payload[1], new float4(0.18f, 0.21f, 0.28f, 0.85f));
            float4 audio = SanitizePayload(payload[2], new float4(0.65f, 1f, 1f, 4f));
            float4 weights = SanitizePayload(payload[3], new float4(1f, 0f, 0f, 0f));
            float4 hashes = payload[4];
            float4 dither = SanitizePayload(payload[5], new float4(1f, 1f, 1f, 1f));
            float fogDensity = math.max(0f, absorption.w * 0.04f);

            Shader.SetGlobalVector(BiomeTransitionFogColorId, ToVector4(fog));
            Shader.SetGlobalVector(BiomeTransitionAbsorptionId, ToVector4(absorption));
            Shader.SetGlobalVector(BiomeTransitionAudioId, ToVector4(audio));
            Shader.SetGlobalVector(BiomeTransitionWeightsId, ToVector4(weights));
            Shader.SetGlobalVector(BiomeTransitionHashesId, ToVector4(hashes));
            Shader.SetGlobalVector(BiomeTransitionDitherId, ToVector4(dither));
            Shader.SetGlobalVector(H8FogColorId, ToVector4(new float4(fog.xyz, fogDensity)));
            Shader.SetGlobalFloat(H8FogDensityId, fogDensity);
            Shader.SetGlobalVector(H8ExtinctionCoefficientsId, ToVector4(absorption));
        }

        private static float4 SanitizePayload(float4 value, float4 fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        private static Vector4 ToVector4(float4 value)
        {
            return new Vector4(value.x, value.y, value.z, value.w);
        }

        private void CompletePipelineForShutdown()
        {
            if (_seedScheduled)
            {
                _seedHandle.Complete();
                _seedScheduled = false;
            }

            if (!_pipelineScheduled)
                return;

            _pipelineHandle.Complete();
            _pipelineScheduled = false;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[BiomeTransitionManagerRuntime] CSV load failed: " + exception.Message, this);
#endif
                return 0;
            }
        }

        private void DumpBlackBox()
        {
            if (_vault == null || !_telemetryHandle.IsCreated)
                return;

            NativeArray<BiomeTransitionTelemetryEntry> telemetry = _telemetryHandle.Resolve(_vault);
            NativeArray<BiomeTransitionCounterDTO> counters = _countersHandle.Resolve(_vault);
            if (!telemetry.IsCreated || telemetry.Length == 0)
                return;

            int cursor = counters.IsCreated && counters.Length > 0 ? counters[0].TelemetryCursor : 0;
            TryDumpTelemetry(telemetry, cursor, ProjectRoot(), BlackBoxDumpPath);
            GlobalTelemetryBus.PublishPerformanceWarning(NonFiniteStateHash, RuntimeContextHash, 1f);
        }

        private void OnDrawGizmos()
        {
            bool tuningDrawEnabled = TryReadTuning(out BiomeTransitionTuningDTO tuning) && tuning.DebugDrawEnabled > 0.5f;
            if (!drawGizmos && !tuningDrawEnabled)
                return;

            IDataVault vault = _vault ?? GlobalRegistry.DataVault;
            if (vault == null ||
                !_centersHandle.IsCreated ||
                !_blendMaskHandle.IsCreated ||
                !_countersHandle.IsCreated)
            {
                return;
            }

            NativeArray<BiomeCenterDTO> centers = _centersHandle.Resolve(vault);
            NativeArray<BiomeBlendMaskDTO> blendMask = _blendMaskHandle.Resolve(vault);
            NativeArray<BiomeTransitionCounterDTO> counters = _countersHandle.Resolve(vault);
            if (!centers.IsCreated || !counters.IsCreated || counters.Length == 0)
                return;

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
                DrawContributionLines(in blendMask[0], centers, count);
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

        public static bool TryReloadCsvFromEditor()
        {
            if (ActiveRuntimeInstance == null)
                return false;

            if (ActiveRuntimeInstance._seedScheduled)
                return false;

            ActiveRuntimeInstance._seededBiomeData = false;
            ActiveRuntimeInstance._seedCsvAttempted = false;
            ActiveRuntimeInstance._seedFallbackScheduled = false;
            ActiveRuntimeInstance.EnsureVaultBuffers();
            ActiveRuntimeInstance.TrySeedBiomeData();
            return true;
        }

        public static bool TryReadSnapshot(out CurrentAtmosphereDTO atmosphere, out BiomeBlendMaskDTO mask, out BiomeTransitionCounterDTO counters)
        {
            atmosphere = default;
            mask = default;
            counters = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetBufferHandle(BufferID.BiomeTransitionCurrentAtmosphere, out VaultBufferHandle<CurrentAtmosphereDTO> atmosphereHandle) ||
                !vault.TryGetBufferHandle(BufferID.BiomeTransitionBlendMask, out VaultBufferHandle<BiomeBlendMaskDTO> maskHandle) ||
                !vault.TryGetBufferHandle(BufferID.BiomeTransitionCounters, out VaultBufferHandle<BiomeTransitionCounterDTO> countersHandle))
            {
                return false;
            }

            NativeArray<CurrentAtmosphereDTO> atmosphereArray = atmosphereHandle.Resolve(vault);
            NativeArray<BiomeBlendMaskDTO> maskArray = maskHandle.Resolve(vault);
            NativeArray<BiomeTransitionCounterDTO> counterArray = countersHandle.Resolve(vault);
            if (!atmosphereArray.IsCreated || atmosphereArray.Length == 0 ||
                !maskArray.IsCreated || maskArray.Length == 0 ||
                !counterArray.IsCreated || counterArray.Length == 0)
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
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetBufferHandle(BufferID.BiomeTransitionTuning, out VaultBufferHandle<BiomeTransitionTuningDTO> tuningHandle))
            {
                return false;
            }

            NativeArray<BiomeTransitionTuningDTO> tuningArray = tuningHandle.Resolve(vault);
            if (!tuningArray.IsCreated || tuningArray.Length == 0)
                return false;

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
            float resolvedSum = math.max(maskSum, atmosphereSum);
            weightSumError = math.abs(resolvedSum - 1f);
            if (!math.isfinite(weightSumError) || weightSumError > 0.001f)
                faultFlags |= SelfAuditWeightFault;

            if (counters.LastBlendCount < 1 || counters.LastBlendCount > BiomeTransitionConstants.MaxBlendBiomes)
                faultFlags |= SelfAuditBlendCountFault;

            return faultFlags == 0u;
        }

        public static bool TryWriteTuning(in BiomeTransitionTuningDTO tuning)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetBufferHandle(BufferID.BiomeTransitionTuning, out VaultBufferHandle<BiomeTransitionTuningDTO> tuningHandle))
            {
                return false;
            }

            NativeArray<BiomeTransitionTuningDTO> tuningArray = tuningHandle.Resolve(vault);
            if (!tuningArray.IsCreated || tuningArray.Length == 0)
                return false;

            tuningArray[0] = tuning;
            if (ActiveRuntimeInstance != null)
                ActiveRuntimeInstance._tuningInitialized = true;
            return true;
        }

        public static bool TryDumpBlackBoxFromEditor()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetBufferHandle(BufferID.BiomeTransitionTelemetryRing, out VaultBufferHandle<BiomeTransitionTelemetryEntry> telemetryHandle) ||
                !vault.TryGetBufferHandle(BufferID.BiomeTransitionCounters, out VaultBufferHandle<BiomeTransitionCounterDTO> countersHandle))
            {
                return false;
            }

            NativeArray<BiomeTransitionTelemetryEntry> telemetry = telemetryHandle.Resolve(vault);
            NativeArray<BiomeTransitionCounterDTO> counters = countersHandle.Resolve(vault);
            if (!telemetry.IsCreated || telemetry.Length == 0)
                return false;

            int cursor = counters.IsCreated && counters.Length > 0 ? counters[0].TelemetryCursor : 0;
            return TryDumpTelemetry(telemetry, cursor, ProjectRoot(), BlackBoxDumpPath);
        }

        private static bool TryDumpTelemetry(
            NativeArray<BiomeTransitionTelemetryEntry> telemetry,
            int cursor,
            string root,
            string relativePath)
        {
            try
            {
                string fullPath = Path.Combine(root, relativePath);
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                int count = telemetry.Length;
                int start = math.clamp(cursor, 0, math.max(0, count - 1));
                for (int i = 0; i < count; i++)
                {
                    int index = start + i;
                    if (index >= count)
                        index -= count;

                    BiomeTransitionTelemetryEntry entry = telemetry[index];
                    writer.Write(entry.PlayerAup.GridX);
                    writer.Write(entry.PlayerAup.GridY);
                    writer.Write(entry.PlayerAup.GridZ);
                    writer.Write(entry.PlayerAup.LocalX);
                    writer.Write(entry.PlayerAup.LocalY);
                    writer.Write(entry.PlayerAup.LocalZ);
                    writer.Write(entry.DominantBiomeHash);
                    writer.Write(entry.BlendedBiomeCount);
                    writer.Write(entry.CpuMicroseconds);
                    writer.Write(entry.StateHash);
                }

                return true;
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[BiomeTransitionManagerRuntime] Black-box dump failed: " + exception.Message);
#endif
                return false;
            }
        }
    }
}
