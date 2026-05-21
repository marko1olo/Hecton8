using System;
using System.IO;
using System.Runtime.CompilerServices;
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
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.AI.Ecosystem
{
    /// <summary>
    /// Data-only SHINOBU swarm and biomass balancer. Fish are vault rows, not scene objects.
    /// </summary>
    public sealed class ShinobuEcosystemBalancer : ITickable, IColdTickable, ILateFrameTickable, IRenderable, IGlobalRegistryHotSwapListener, IDisposable
    {
        private const int DefaultEntityCapacity = 100000;
        private const int MinimumVisualBoidBudget = 1000;
        private const int DefaultSectorCapacity = 256;
        private const int TelemetryCapacity = 300;
        private const int CounterCapacity = 24;
        private const int DebugCellCapacity = 256;
        private const int SpatialHashBucketCapacity = 32768;
        private const int SpatialHashBucketMask = SpatialHashBucketCapacity - 1;
        private const int FrameJobBatchSize = 64;
        private const float TrigPi = 3.14159265358979323846f;
        private const float TrigTwoPi = 6.28318530717958647692f;
        private const float TrigHalfPi = 1.57079632679489661923f;
        private const float TrigInvTwoPi = 0.15915494309189533577f;
        private const int MaxNeighborSamples = 48;
        private const int MaxSpatialHashChainSteps = 64;
        private const int SwarmSpeciesProfileCapacity = 64;
        private const uint DefaultBoidVertexCountPerInstance = 3u;
        private const int CsvMaxBytes = 8192;
        private const int LegacyProfileReadBytes = 64;
        private const float DefaultCellSizeMeters = 10f;
        private const float DefaultSectorSizeMeters = 64f;
        private const float DefaultNeighborRadiusMeters = 12f;
        private const float DefaultDehydrateDistanceMeters = 200f;
        private const float DefaultRehydrateDistanceMeters = 160f;
        private const float DefaultObstacleProbeMeters = 2f;
        private const float DefaultBoidSpeedMetersPerSecond = 5.5f;
        private const float DefaultSimulationTickDeltaSeconds = 1f / 60f;
        private const float AuthoritativeQualityWeight = 1f;
        private const float TelemetryFaultThresholdMs = 1.5f;
        private const double AupCellSizeMetersDouble = HectonPhysicsContract.AupSectorSizeMetersDouble;
        private const byte ScheduledPipelineNone = 0;
        private const byte ScheduledPipelineFrame = 1;
        private const byte ScheduledPipelineMacro = 2;
        private const string LegacyFaunaCapsFile = "fauna_population_caps.h8bin";
        private const string LegacyBoidProfileFile = "boid_behavior_profiles.bin";
        private const string SwarmSpeciesCsvRelativePath = "swarm_species_profiles.csv";
        private const string SwarmSpeciesCsvPrecomputedRelativePath = "Data/Precomputed/swarm_species_profiles.csv";
        private const string CsvRelativePath = "ecosystem_balance.csv";
        private const string CsvPrecomputedRelativePath = "Data/Precomputed/ecosystem_balance.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_105.bin";
        private const string DumpH8RelativePath = "Docs/AgentLogs/Dump_SHINOBU_105.h8dump";
        private const ulong DumpMagic = 0x414259535357524DUL; // ABYSSWRM
        private const int DumpVersion = 3;
        private const uint SourceHash = 0x5348494Eu; // SHIN

        internal const uint EntityFlagActive = 1u << 0;
        internal const uint EntityFlagFree = 1u << 1;
        internal const uint EntityFlagHydrated = 1u << 2;
        internal const uint EntityFlagSkipUpdate = 1u << 3;
        internal const uint EntityFlagHerbivore = 1u << 4;
        internal const uint EntityFlagCarnivore = 1u << 5;
        internal const uint EntityFlagInvalidMath = 1u << 6;

        internal const uint SectorFlagValid = 1u << 0;
        internal const uint SectorFlagDehydrated = 1u << 1;

        public const uint TuningFlagEmergencyMock = 1u << 0;
        public const uint TuningFlagLegacyBinary = 1u << 1;
        public const uint TuningFlagCsvOverride = 1u << 2;
        public const uint TuningFlagEditorDebugGrid = 1u << 3;
        public const uint TuningFlagEditorDebugVectors = 1u << 4;
        public const uint TelemetryFlagMacroPass = 1u << 29;
        public const uint TelemetryFlagSolveOverBudget = 1u << 30;

        private const int CounterInitialized = 0;
        private const int CounterActive = 1;
        private const int CounterHydrated = 2;
        private const int CounterFree = 3;
        private const int CounterDehydratedSectors = 4;
        private const int CounterSkipped = 5;
        private const int CounterInvalidMath = 6;
        private const int CounterSpatialHashOverflow = 7;
        private const int CounterDebugCellCount = 8;
        private const int CounterReproduced = 9;
        private const int CounterTombstoned = 10;
        private const int CounterProfileLoaded = 11;
        private const int CounterCsvLoaded = 12;

        private static ShinobuEcosystemBalancer s_runtime;

        private int entityCapacity = DefaultEntityCapacity;
        private int sectorCapacity = DefaultSectorCapacity;
        private float spatialCellSizeMeters = DefaultCellSizeMeters;
        private float sectorSizeMeters = DefaultSectorSizeMeters;
        private float neighborRadiusMeters = DefaultNeighborRadiusMeters;
        private float dehydrationDistanceMeters = DefaultDehydrateDistanceMeters;
        private float rehydrationDistanceMeters = DefaultRehydrateDistanceMeters;
        private float obstacleProbeMeters = DefaultObstacleProbeMeters;

        private VaultGenerationHandle<AmbientEntityDTO> _entityHandle;
        private VaultGenerationHandle<AmbientEntityAupDTO> _aupHandle;
        private VaultGenerationHandle<BoidStateDTO> _boidStateHandle;
        private VaultGenerationHandle<AmbientEntityDTO> _entitySnapshotHandle;
        private VaultGenerationHandle<AmbientEntityAupDTO> _aupSnapshotHandle;
        private VaultGenerationHandle<BoidStateDTO> _boidStateSnapshotHandle;
        private VaultGenerationHandle<EcosystemSectorDTO> _sectorHandle;
        private VaultGenerationHandle<ShinobuEcosystemTuning> _tuningHandle;
        private VaultGenerationHandle<int> _counterHandle;
        private VaultGenerationHandle<ShinobuTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<ShinobuSpatialHashDebugCell> _debugCellHandle;
        private VaultGenerationHandle<BoidMatrixDTO> _renderMatrixHandle;
        private VaultGenerationHandle<float4> _renderCustomDataHandle;
        private VaultGenerationHandle<BoidIndirectArgsDTO> _indirectArgsHandle;
        private VaultGenerationHandle<int> _spatialHashBucketHeadHandle;
        private VaultGenerationHandle<int> _spatialHashNextHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<byte> _legacyScratchHandle;
        private VaultGenerationHandle<SwarmSpeciesProfileDTO> _swarmSpeciesProfileHandle;

        private IDataVault _dataVault;
        private readonly ShinobuBoidGpuUploadDispatcher _gpuUploadDispatcher;
        private JobHandle _activeJobHandle;
        private AbsoluteUniversePosition _cameraAup;
        private float3 _cameraLocalPosition;
        private float3 _cameraForward = new float3(0f, 0f, 1f);
        private long _csvTimestampTicks;
        private long _swarmSpeciesCsvTimestampTicks;
        private long _scheduleTicks;
        private int _telemetryCursor;
        private int _coldTickIndex;
        private uint _simulationFrameCounter;
        private float _lastFlockingMs;
        private float _lastSpatialHashMs;
        private float _lastMatrixUploadMs;
        private float _lastGlobalQualityWeight;
        private int _lastActiveBudget;
        private bool _registeredTick;
        private bool _registeredColdTick;
        private bool _registeredLateFrame;
        private bool _registeredRender;
        private bool _registeredHotSwap;
        private bool _jobScheduled;
        private bool _jobLocksHeld;
        private bool _vaultBuffersReady;
        private bool _dumpedFault;
        private bool _proceduralRenderEnabled;
        private byte _scheduledPipelineKind;
        private uint _runtimeFlags;
        private int _proceduralRenderLayer;
        private Material _proceduralRenderMaterial;
        private Bounds _proceduralRenderBounds;
        private ComputeShader _proceduralCullCompute;
        private Matrix4x4 _proceduralCullViewProjection = Matrix4x4.identity;
        private Matrix4x4 _proceduralCullViewMatrix = Matrix4x4.identity;
        private Vector4 _proceduralCullZBufferParams;
        private Vector4 _proceduralCullDepthTexelSize;
        private Texture _proceduralCullDepthPyramid;
        private bool _proceduralCullHasValidZBufferParams;
        private int _proceduralClearArgsKernel = -1;
        private int _proceduralCullKernel = -1;
        private int _proceduralCullDepthMipCount;
        private int _proceduralCullDensityStep = 1;
        private float _proceduralCullDepthBias = 0.04f;
        private float _proceduralCullBoundsRadius = 0.35f;

        private ShinobuEcosystemBalancer()
        {
            _gpuUploadDispatcher = new ShinobuBoidGpuUploadDispatcher();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            if (s_runtime != null)
                s_runtime.Dispose();

            s_runtime = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            EnsureRuntimeService();
        }

        public static ShinobuEcosystemBalancer EnsureRuntimeService()
        {
            ShinobuEcosystemBalancer runtime = s_runtime;
            if (runtime == null)
            {
                runtime = new ShinobuEcosystemBalancer();
                s_runtime = runtime;
            }

            runtime.Activate();
            return runtime;
        }

#if UNITY_EDITOR
        public void ForceDesignerDataReload()
        {
            if (!Application.isPlaying)
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            _csvTimestampTicks = 0L;
            _swarmSpeciesCsvTimestampTicks = 0L;
            MonitorCsvOverrides(vault);
        }
#endif

        private void Activate()
        {
            if (!Application.isPlaying)
                return;

            SignalBus<MockPredatorSignal>.EnsureInitialized();
            EnsureDataVaultCold();
            TryRegisterHotSwapListener();
            if (EnsureVaultState())
                TryRegisterTicks();
            if (_proceduralRenderMaterial != null)
                TryRegisterRender();
        }

        public void Dispose()
        {
            CompleteFrameJobForTeardown();
            TryUnregisterRender();
            TryUnregisterTicks();
            TryUnregisterHotSwapListener();
            UnlockJobBuffers();
            _gpuUploadDispatcher.Dispose();
            ClearCachedState();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.RenderDispatcher)
            {
                if (currentService != null)
                    TryRegisterRender();
                else
                    TryUnregisterRender();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            CompleteFrameJobForTeardown();
            UnlockJobBuffers();
            _dataVault = currentService as IDataVault;
            ResetVaultHandles();
            _telemetryCursor = 0;
            _simulationFrameCounter = 0u;
            _csvTimestampTicks = 0L;
            _swarmSpeciesCsvTimestampTicks = 0L;
            _dumpedFault = false;

            if (_dataVault == null || !EnsureVaultState())
            {
                TryUnregisterTicks();
                return;
            }

            TryRegisterTicks();
        }

        public void Tick(float deltaTime)
        {
            if (_jobScheduled)
                return;

            if (!EnsureVaultState())
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            RefreshCameraSignals();
            if (!TryResolveBuffers(
                    vault,
                    out NativeArray<AmbientEntityDTO> entities,
                    out NativeArray<AmbientEntityAupDTO> aups,
                    out NativeArray<BoidStateDTO> boidStates,
                    out NativeArray<AmbientEntityDTO> entitySnapshot,
                    out NativeArray<AmbientEntityAupDTO> aupSnapshot,
                    out NativeArray<BoidStateDTO> boidStateSnapshot,
                    out NativeArray<EcosystemSectorDTO> sectors,
                    out NativeArray<ShinobuEcosystemTuning> tuningArray,
                    out NativeArray<int> counters,
                    out NativeArray<ShinobuTelemetryEntry> telemetry,
                    out NativeArray<ShinobuSpatialHashDebugCell> debugCells,
                    out NativeArray<BoidMatrixDTO> matrices,
                    out NativeArray<float4> customData,
                    out NativeArray<BoidIndirectArgsDTO> indirectArgs,
                    out NativeArray<int> spatialHashBucketHeads,
                    out NativeArray<int> spatialHashNext))
            {
                return;
            }

            int count = entityCapacity;
            count = math.min(count, entities.Length);
            count = math.min(count, aups.Length);
            count = math.min(count, boidStates.Length);
            count = math.min(count, entitySnapshot.Length);
            count = math.min(count, aupSnapshot.Length);
            count = math.min(count, boidStateSnapshot.Length);
            count = math.min(count, spatialHashNext.Length);
            count = math.min(count, matrices.Length);
            count = math.min(count, customData.Length);
            float visualQualityWeight = ResolveGlobalQualityWeight01();
            const float authorityQualityWeight = AuthoritativeQualityWeight;
            float systemStress01 = ResolveSystemStress01();
            count = ResolveActiveEntityBudget(count, authorityQualityWeight);
            if (count <= 0)
                return;

            if (!TryLockJobBuffers(vault))
                return;

            JobHandle scheduledHandle = default;
            bool scheduledWork = false;
            try
            {
                for (int i = 0; i < CounterCapacity && i < counters.Length; i++)
                    counters[i] = 0;

                if (spatialHashBucketHeads.Length < SpatialHashBucketCapacity)
                {
                    counters[CounterSpatialHashOverflow] = 1;
                    UnlockJobBuffers();
                    return;
                }

                ShinobuEcosystemTuning tuning = ShinobuEcosystemTuning.Sanitize(tuningArray[0]);
                tuningArray[0] = tuning;

                MockPredatorRuntime predator = ResolvePredatorRuntime();
                bool debugGridEnabled = (tuning.Flags & TuningFlagEditorDebugGrid) != 0u && debugCells.IsCreated;
                int maxNeighborSamples = ResolveNeighborSampleBudget(MaxNeighborSamples, authorityQualityWeight);
                int maxChainSteps = ResolveSpatialHashChainBudget(MaxSpatialHashChainSteps, authorityQualityWeight);
                int updateStride = ResolveUpdateStride(authorityQualityWeight, systemStress01);
                float simulationDeltaSeconds = ResolveSimulationTickDelta(authorityQualityWeight);
                double3 cameraAbsolute = ToAbsoluteDouble3(in _cameraAup);
                uint frame = AdvanceSimulationFrame();
                var buildJob = new LocalShiftAndSpatialHashJob
                {
                    Entities = entities,
                    Aups = aups,
                    BoidStates = boidStates,
                    EntitySnapshot = entitySnapshot,
                    AupSnapshot = aupSnapshot,
                    BoidStateSnapshot = boidStateSnapshot,
                    CenterAup = _cameraAup,
                    CellSizeMeters = math.max(0.25f, spatialCellSizeMeters),
                    SectorSizeMeters = math.max(1f, sectorSizeMeters),
                    SystemStress01 = systemStress01,
                    GlobalQualityWeight = authorityQualityWeight,
                    UpdateStride = updateStride,
                    Frame = frame,
                    Count = count
                };

                _scheduleTicks = Stopwatch.GetTimestamp();
                JobHandle handle = buildJob.Schedule(count, FrameJobBatchSize);
                scheduledHandle = handle;
                scheduledWork = true;
                var hashJob = new BuildBoidSpatialHashJob
                {
                    AupSnapshot = aupSnapshot,
                    BucketHeads = spatialHashBucketHeads,
                    BucketNext = spatialHashNext,
                    Counters = counters,
                    MaxChainSteps = maxChainSteps,
                    Count = count
                };
                handle = hashJob.Schedule(handle);
                scheduledHandle = handle;
                if (debugGridEnabled)
                {
                    var debugJob = new BuildHashDebugCellsJob
                    {
                        Entities = entities,
                        Aups = aups,
                        BucketHeads = spatialHashBucketHeads,
                        BucketNext = spatialHashNext,
                        DebugCells = debugCells,
                        Counters = counters,
                        CellSizeMeters = math.max(0.25f, spatialCellSizeMeters),
                        Count = count,
                        Capacity = math.min(DebugCellCapacity, debugCells.Length),
                        MaxChainSteps = maxChainSteps
                    };
                    handle = debugJob.Schedule(handle);
                    scheduledHandle = handle;
                }

                var solveJob = new BoidFlockingJob
                {
                    Entities = entities,
                    Aups = aups,
                    BoidStates = boidStates,
                    EntitySnapshot = entitySnapshot,
                    AupSnapshot = aupSnapshot,
                    BoidStateSnapshot = boidStateSnapshot,
                    BucketHeads = spatialHashBucketHeads,
                    BucketNext = spatialHashNext,
                    CenterAup = _cameraAup,
                    CenterAbsolute = cameraAbsolute,
                    CameraForward = SafeNormalize(_cameraForward, new float3(0f, 0f, 1f)),
                    TerrainSampler = MockTerrainSampler.CreateDefault(),
                    Predator = predator,
                    Tuning = tuning,
                    DeltaSeconds = simulationDeltaSeconds,
                    GlobalQualityWeight = authorityQualityWeight,
                    CellSizeMeters = math.max(0.25f, spatialCellSizeMeters),
                    SectorSizeMeters = math.max(1f, sectorSizeMeters),
                    NeighborRadiusMeters = math.max(1f, neighborRadiusMeters),
                    ObstacleProbeMeters = math.max(0.1f, obstacleProbeMeters),
                    MaxNeighborSamplesPerBoid = maxNeighborSamples,
                    MaxSpatialHashChainSteps = maxChainSteps,
                    Count = count
                };
                handle = solveJob.Schedule(count, FrameJobBatchSize, handle);
                scheduledHandle = handle;

                var renderJob = new BuildShinobuRenderPayloadJob
                {
                    Entities = entities,
                    Aups = aups,
                    BoidStates = boidStates,
                    Matrices = matrices,
                    CustomData = customData,
                    CenterAbsolute = cameraAbsolute,
                    GlobalQualityWeight = visualQualityWeight,
                    Count = count
                };
                handle = renderJob.Schedule(count, FrameJobBatchSize, handle);
                scheduledHandle = handle;

                var countJob = new CountTelemetryCountersJob
                {
                    Aups = aups,
                    Sectors = sectors,
                    Counters = counters,
                    Count = count,
                    SectorCount = math.min(sectorCapacity, sectors.Length)
                };
                handle = countJob.Schedule(handle);
                scheduledHandle = handle;
                handle = WriteIndirectArgs(
                    indirectArgs,
                    DefaultBoidVertexCountPerInstance,
                    0u,
                    0u,
                    (uint)math.max(0, count),
                    handle);
                scheduledHandle = handle;

                _activeJobHandle = handle;
                _lastActiveBudget = count;
                _lastGlobalQualityWeight = visualQualityWeight;
                _lastSpatialHashMs = 0f;
                _lastMatrixUploadMs = 0f;
                _runtimeFlags &= ~TelemetryFlagMacroPass;
                _scheduledPipelineKind = ScheduledPipelineFrame;
                _jobScheduled = true;
                _jobLocksHeld = true;
                H8Memory.RegisterActiveJob(SystemID.AIEcology, _activeJobHandle);
            }
            catch (Exception)
            {
                if (scheduledWork)
                {
                    _activeJobHandle = scheduledHandle;
                    _lastActiveBudget = count;
                    _lastGlobalQualityWeight = visualQualityWeight;
                    _scheduledPipelineKind = ScheduledPipelineFrame;
                    _jobScheduled = true;
                    _jobLocksHeld = true;
                }
                else
                {
                    UnlockJobBuffers();
                }

                GlobalTelemetryBus.PublishPerformanceWarning(0x534A4F42u, SourceHash, 0f);
            }
        }

        public void ColdTick()
        {
            if (_jobScheduled)
                return;

            if (!EnsureVaultState())
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            RefreshCameraSignals();
            MonitorCsvOverrides(vault);
            RunMacroBiomassPass(vault);
            _coldTickIndex++;
        }

        public void LateFrameTick()
        {
            TryFinalizeFrameJobNoWait();
        }

        /// <summary>
        /// Binds a cold-authored material asset for render-dispatch submission of the uploaded swarm buffers.
        /// </summary>
        /// <param name="material">Material using `Hecton8/AbyssalSwarmProcedural`; null disables draw submission.</param>
        /// <param name="bounds">Camera-relative conservative draw bounds.</param>
        /// <param name="layer">Unity render layer index for the procedural draw.</param>
        public void BindProceduralRenderMaterial(Material material, Bounds bounds, int layer = 0)
        {
            _proceduralRenderMaterial = material;
            _proceduralRenderBounds = SanitizeRenderBounds(bounds);
            _proceduralRenderLayer = math.clamp(layer, 0, 31);
            _proceduralRenderEnabled = material != null;

            if (_proceduralRenderEnabled)
                TryRegisterRender();
            else
                TryUnregisterRender();
        }

        /// <summary>
        /// Binds caller-owned GPU culling resources; null compute disables the visibility pass.
        /// </summary>
        public void BindProceduralCullingResources(
            ComputeShader cullingCompute,
            Matrix4x4 viewProjection,
            Matrix4x4 viewMatrix,
            Vector4 zBufferParams,
            Texture depthPyramid,
            int depthPyramidMipCount,
            Vector4 depthPyramidTexelSize,
            float occlusionDepthBias = 0.04f,
            float boundsRadius = 0.35f,
            int densityStep = 1)
        {
            _proceduralCullCompute = cullingCompute;
            _proceduralCullViewProjection = viewProjection;
            _proceduralCullViewMatrix = viewMatrix;
            _proceduralCullHasValidZBufferParams = IsUsableZBufferParams(zBufferParams);
            _proceduralCullZBufferParams = _proceduralCullHasValidZBufferParams ? zBufferParams : new Vector4(0f, 1f, 0f, 1f);
            _proceduralCullDepthPyramid = depthPyramid;
            _proceduralCullDepthMipCount = SanitizeDepthPyramidMipCount(depthPyramid, depthPyramidMipCount);
            _proceduralCullDepthTexelSize = SanitizeDepthPyramidTexelSize(depthPyramid, depthPyramidTexelSize);
            _proceduralCullDepthBias = math.max(0.0001f, occlusionDepthBias);
            _proceduralCullBoundsRadius = math.max(0.001f, boundsRadius);
            _proceduralCullDensityStep = math.clamp(densityStep, 1, 8);

            if (cullingCompute == null)
            {
                _proceduralClearArgsKernel = -1;
                _proceduralCullKernel = -1;
                return;
            }

            _proceduralClearArgsKernel = cullingCompute.HasKernel(ShinobuSwarmGpuCullingParams.ClearKernelName)
                ? cullingCompute.FindKernel(ShinobuSwarmGpuCullingParams.ClearKernelName)
                : -1;
            _proceduralCullKernel = cullingCompute.HasKernel(ShinobuSwarmGpuCullingParams.CullKernelName)
                ? cullingCompute.FindKernel(ShinobuSwarmGpuCullingParams.CullKernelName)
                : -1;
        }

        /// <summary>
        /// Resolves the active double-buffered GPU resources after the latest completed upload.
        /// </summary>
        public bool TryGetUploadedSwarmBuffers(
            out GraphicsBuffer matrixBuffer,
            out GraphicsBuffer customDataBuffer,
            out GraphicsBuffer indirectArgsBuffer,
            out int activeCount)
        {
            return _gpuUploadDispatcher.TryGetActiveBuffers(
                out matrixBuffer,
                out customDataBuffer,
                out indirectArgsBuffer,
                out activeCount);
        }

        /// <summary>
        /// Issues the procedural indirect draw with a caller-owned material asset.
        /// </summary>
        public bool TryDrawUploadedSwarm(
            Material material,
            Bounds bounds,
            int layer = 0,
            ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off)
        {
            if (Application.isBatchMode || material == null)
                return false;

            return _gpuUploadDispatcher.TryDraw(
                material,
                SanitizeRenderBounds(bounds),
                ResolveGpuCullingParams(),
                MeshTopology.Triangles,
                math.clamp(layer, 0, 31),
                shadowCastingMode);
        }

        /// <summary>
        /// Submits the bound procedural swarm material through the render dispatcher.
        /// </summary>
        /// <param name="deltaTime">Scaled render delta supplied by the dispatcher; swarm simulation uses deterministic tick state instead.</param>
        public void Render(float deltaTime)
        {
            if (Application.isBatchMode || !_proceduralRenderEnabled || _proceduralRenderMaterial == null)
                return;

            _gpuUploadDispatcher.TryDraw(
                _proceduralRenderMaterial,
                _proceduralRenderBounds,
                ResolveGpuCullingParams(),
                MeshTopology.Triangles,
                _proceduralRenderLayer,
                ShadowCastingMode.Off);
        }

        private ShinobuSwarmGpuCullingParams ResolveGpuCullingParams()
        {
            float quality = math.saturate(_lastGlobalQualityWeight);
            int occlusionEnabled = _proceduralCullDepthPyramid != null &&
                                   _proceduralCullDepthMipCount > 0 &&
                                   _proceduralCullHasValidZBufferParams
                ? 1
                : 0;

            ShinobuSwarmGpuCullingParams culling;
            culling.CullingCompute = _proceduralCullCompute;
            culling.ClearArgsKernel = _proceduralClearArgsKernel;
            culling.CullKernel = _proceduralCullKernel;
            culling.ViewProjection = _proceduralCullViewProjection;
            culling.ViewMatrix = _proceduralCullViewMatrix;
            culling.ZBufferParams = _proceduralCullZBufferParams;
            culling.DepthPyramid = _proceduralCullDepthPyramid;
            culling.DepthPyramidTexelSize = _proceduralCullDepthTexelSize;
            culling.DepthPyramidMipCount = _proceduralCullDepthMipCount;
            culling.OcclusionEnabled = occlusionEnabled;
            culling.OcclusionDepthBias = _proceduralCullDepthBias;
            culling.BoundsRadius = _proceduralCullBoundsRadius;
            culling.QualityWeight = quality;
            culling.DensityStep = math.clamp(_proceduralCullDensityStep, 1, 8);
            return culling;
        }

        private static int SanitizeDepthPyramidMipCount(Texture depthPyramid, int requestedMipCount)
        {
            if (depthPyramid == null)
                return 0;

            int maxDimension = math.max(1, math.max(depthPyramid.width, depthPyramid.height));
            int maxMipCount = 1;
            while (maxDimension > 1 && maxMipCount < 16)
            {
                maxDimension >>= 1;
                maxMipCount++;
            }

            if (requestedMipCount <= 0)
                return 1;

            return math.clamp(requestedMipCount, 1, maxMipCount);
        }

        private static Vector4 SanitizeDepthPyramidTexelSize(Texture depthPyramid, Vector4 texelSize)
        {
            float textureWidth = depthPyramid != null ? math.max(1f, depthPyramid.width) : 1f;
            float textureHeight = depthPyramid != null ? math.max(1f, depthPyramid.height) : 1f;
            bool hasCallerWidth = math.isfinite(texelSize.z) && texelSize.z > 0f;
            bool hasCallerHeight = math.isfinite(texelSize.w) && texelSize.w > 0f;
            float width = hasCallerWidth ? texelSize.z : textureWidth;
            float height = hasCallerHeight ? texelSize.w : textureHeight;
            width = math.max(1f, width);
            height = math.max(1f, height);

            float expectedInvWidth = 1f / width;
            float expectedInvHeight = 1f / height;
            bool hasCallerInvWidth = math.isfinite(texelSize.x) && texelSize.x > 0f && math.abs((texelSize.x * width) - 1f) <= 0.05f;
            bool hasCallerInvHeight = math.isfinite(texelSize.y) && texelSize.y > 0f && math.abs((texelSize.y * height) - 1f) <= 0.05f;
            float invWidth = hasCallerWidth && hasCallerInvWidth ? texelSize.x : expectedInvWidth;
            float invHeight = hasCallerHeight && hasCallerInvHeight ? texelSize.y : expectedInvHeight;
            return new Vector4(invWidth, invHeight, width, height);
        }

        private static bool IsUsableZBufferParams(Vector4 zBufferParams)
        {
            return math.isfinite(zBufferParams.x) &&
                   math.isfinite(zBufferParams.y) &&
                   math.isfinite(zBufferParams.z) &&
                   math.isfinite(zBufferParams.w) &&
                   (math.abs(zBufferParams.z) + math.abs(zBufferParams.w)) > 0.0001f;
        }

        private static Bounds SanitizeRenderBounds(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            const float minRenderableExtentMeters = 0.001f;
            float fallbackExtentMeters = DefaultDehydrateDistanceMeters * 2f;
            if (!IsFinite(center))
                center = Vector3.zero;
            if (!IsFinite(size) ||
                size.x <= minRenderableExtentMeters ||
                size.y <= minRenderableExtentMeters ||
                size.z <= minRenderableExtentMeters)
            {
                size = Vector3.one * fallbackExtentMeters;
            }

            size.x = math.max(1f, size.x);
            size.y = math.max(1f, size.y);
            size.z = math.max(1f, size.z);
            return new Bounds(center, size);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsInfinity(value.z);
        }

        private bool EnsureVaultState()
        {
            ShinobuEcosystemLayoutManifest.VerifyColdBoot();

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            entityCapacity = math.max(1, entityCapacity);
            sectorCapacity = math.max(1, sectorCapacity);
            spatialCellSizeMeters = math.max(0.25f, spatialCellSizeMeters);
            sectorSizeMeters = math.max(1f, sectorSizeMeters);
            neighborRadiusMeters = math.max(1f, neighborRadiusMeters);
            dehydrationDistanceMeters = math.max(16f, dehydrationDistanceMeters);
            rehydrationDistanceMeters = math.min(dehydrationDistanceMeters - 1f, math.max(8f, rehydrationDistanceMeters));
            obstacleProbeMeters = math.max(0.1f, obstacleProbeMeters);

            if (_vaultBuffersReady)
                return true;

            _entityHandle = ClaimVaultHandle<AmbientEntityDTO>(
                vault,
                BufferID.ShinobuAmbientEntities,
                entityCapacity,
                NativeArrayOptions.UninitializedMemory);
            _aupHandle = ClaimVaultHandle<AmbientEntityAupDTO>(
                vault,
                BufferID.ShinobuAmbientAups,
                entityCapacity,
                NativeArrayOptions.UninitializedMemory);
            _boidStateHandle = ClaimVaultHandle<BoidStateDTO>(
                vault,
                BufferID.ShinobuBoidStates,
                entityCapacity,
                NativeArrayOptions.UninitializedMemory);
            _entitySnapshotHandle = ClaimVaultHandle<AmbientEntityDTO>(
                vault,
                BufferID.ShinobuAmbientEntitySnapshot,
                entityCapacity,
                NativeArrayOptions.UninitializedMemory);
            _aupSnapshotHandle = ClaimVaultHandle<AmbientEntityAupDTO>(
                vault,
                BufferID.ShinobuAmbientAupSnapshot,
                entityCapacity,
                NativeArrayOptions.UninitializedMemory);
            _boidStateSnapshotHandle = ClaimVaultHandle<BoidStateDTO>(
                vault,
                BufferID.ShinobuBoidStateSnapshot,
                entityCapacity,
                NativeArrayOptions.UninitializedMemory);
            _sectorHandle = ClaimVaultHandle<EcosystemSectorDTO>(
                vault,
                BufferID.ShinobuEcosystemSectors,
                sectorCapacity,
                NativeArrayOptions.ClearMemory);
            _tuningHandle = ClaimVaultHandle<ShinobuEcosystemTuning>(
                vault,
                BufferID.ShinobuEcosystemTuning,
                1,
                NativeArrayOptions.ClearMemory);
            _counterHandle = ClaimVaultHandle<int>(
                vault,
                BufferID.ShinobuEcosystemCounters,
                CounterCapacity,
                NativeArrayOptions.ClearMemory);
            _telemetryHandle = ClaimVaultHandle<ShinobuTelemetryEntry>(
                vault,
                BufferID.ShinobuEcosystemTelemetryRing,
                TelemetryCapacity,
                NativeArrayOptions.ClearMemory);
            _debugCellHandle = ClaimVaultHandle<ShinobuSpatialHashDebugCell>(
                vault,
                BufferID.ShinobuSpatialHashDebugCells,
                DebugCellCapacity,
                NativeArrayOptions.ClearMemory);
            _renderMatrixHandle = ClaimVaultHandle<BoidMatrixDTO>(
                vault,
                BufferID.ShinobuRenderMatrices,
                entityCapacity,
                NativeArrayOptions.UninitializedMemory);
            _renderCustomDataHandle = ClaimVaultHandle<float4>(
                vault,
                BufferID.ShinobuRenderCustomData,
                entityCapacity,
                NativeArrayOptions.UninitializedMemory);
            _indirectArgsHandle = ClaimVaultHandle<BoidIndirectArgsDTO>(
                vault,
                BufferID.ShinobuBoidIndirectArgs,
                1,
                NativeArrayOptions.UninitializedMemory);
            _spatialHashBucketHeadHandle = ClaimVaultHandle<int>(
                vault,
                BufferID.ShinobuSpatialHashBucketHeads,
                SpatialHashBucketCapacity,
                NativeArrayOptions.UninitializedMemory);
            _spatialHashNextHandle = ClaimVaultHandle<int>(
                vault,
                BufferID.ShinobuSpatialHashNext,
                entityCapacity + sectorCapacity,
                NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = ClaimVaultHandle<byte>(
                vault,
                BufferID.ShinobuEcosystemCsvScratch,
                CsvMaxBytes,
                NativeArrayOptions.UninitializedMemory);
            _legacyScratchHandle = ClaimVaultHandle<byte>(
                vault,
                BufferID.ShinobuEcosystemLegacyScratch,
                LegacyProfileReadBytes,
                NativeArrayOptions.UninitializedMemory);
            _swarmSpeciesProfileHandle = ClaimVaultHandle<SwarmSpeciesProfileDTO>(
                vault,
                BufferID.ShinobuSwarmSpeciesProfiles,
                SwarmSpeciesProfileCapacity,
                NativeArrayOptions.ClearMemory);

            bool ready = AreVaultHandlesCreated(vault);
            _vaultBuffersReady = ready;
            if (!ready)
                return false;

            EnsureGpuUploadCapacity();
            EnsureProfilesLoaded(vault);
            EnsureInitialPopulation(vault);
            return true;
        }

        private bool AreVaultHandlesCreated(IDataVault vault)
        {
            return TryOpenVaultView(vault, in _entityHandle, entityCapacity, out NativeArray<AmbientEntityDTO> _) &&
                   TryOpenVaultView(vault, in _aupHandle, entityCapacity, out NativeArray<AmbientEntityAupDTO> _) &&
                   TryOpenVaultView(vault, in _boidStateHandle, entityCapacity, out NativeArray<BoidStateDTO> _) &&
                   TryOpenVaultView(vault, in _entitySnapshotHandle, entityCapacity, out NativeArray<AmbientEntityDTO> _) &&
                   TryOpenVaultView(vault, in _aupSnapshotHandle, entityCapacity, out NativeArray<AmbientEntityAupDTO> _) &&
                   TryOpenVaultView(vault, in _boidStateSnapshotHandle, entityCapacity, out NativeArray<BoidStateDTO> _) &&
                   TryOpenVaultView(vault, in _sectorHandle, sectorCapacity, out NativeArray<EcosystemSectorDTO> _) &&
                   TryOpenVaultView(vault, in _tuningHandle, 1, out NativeArray<ShinobuEcosystemTuning> _) &&
                   TryOpenVaultView(vault, in _counterHandle, CounterCapacity, out NativeArray<int> _) &&
                   TryOpenVaultView(vault, in _telemetryHandle, TelemetryCapacity, out NativeArray<ShinobuTelemetryEntry> _) &&
                   TryOpenVaultView(vault, in _debugCellHandle, DebugCellCapacity, out NativeArray<ShinobuSpatialHashDebugCell> _) &&
                   TryOpenVaultView(vault, in _renderMatrixHandle, entityCapacity, out NativeArray<BoidMatrixDTO> _) &&
                   TryOpenVaultView(vault, in _renderCustomDataHandle, entityCapacity, out NativeArray<float4> _) &&
                   TryOpenVaultView(vault, in _indirectArgsHandle, 1, out NativeArray<BoidIndirectArgsDTO> _) &&
                   TryOpenVaultView(vault, in _spatialHashBucketHeadHandle, SpatialHashBucketCapacity, out NativeArray<int> _) &&
                   TryOpenVaultView(vault, in _spatialHashNextHandle, entityCapacity + sectorCapacity, out NativeArray<int> _) &&
                   TryOpenVaultView(vault, in _csvScratchHandle, CsvMaxBytes, out NativeArray<byte> _) &&
                   TryOpenVaultView(vault, in _legacyScratchHandle, LegacyProfileReadBytes, out NativeArray<byte> _) &&
                   TryOpenVaultView(vault, in _swarmSpeciesProfileHandle, SwarmSpeciesProfileCapacity, out NativeArray<SwarmSpeciesProfileDTO> _);
        }

        private void EnsureGpuUploadCapacity()
        {
            if (Application.isBatchMode)
                return;

            try
            {
                _gpuUploadDispatcher.EnsureGraphicsResources(entityCapacity);
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x47505543u, SourceHash, 0f);
            }
        }

        private IDataVault EnsureDataVaultCold()
        {
            if (_dataVault != null)
                return _dataVault;

            _dataVault = GlobalRegistry.DataVault;
            return _dataVault;
        }

        private static VaultGenerationHandle<T> ClaimVaultHandle<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null || requiredLength <= 0)
                return default;

            if (vault.IsAllocationLocked)
            {
                return vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> existing)
                    ? existing
                    : default;
            }

            return vault.GetGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.AIEcology,
                options);
        }

        private static bool TryOpenVaultView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                handle.BufferID == 0u ||
                handle.Generation == 0u ||
                requiredLength < 0 ||
                !vault.TryResolveHandle(in handle, out NativeArray<T> resolved) ||
                !resolved.IsCreated ||
                resolved.Length < requiredLength)
            {
                return false;
            }

            buffer = resolved;
            return true;
        }

        private bool TryResolveBuffers(
            IDataVault vault,
            out NativeArray<AmbientEntityDTO> entities,
            out NativeArray<AmbientEntityAupDTO> aups,
            out NativeArray<BoidStateDTO> boidStates,
            out NativeArray<AmbientEntityDTO> entitySnapshot,
            out NativeArray<AmbientEntityAupDTO> aupSnapshot,
            out NativeArray<BoidStateDTO> boidStateSnapshot,
            out NativeArray<EcosystemSectorDTO> sectors,
            out NativeArray<ShinobuEcosystemTuning> tuning,
            out NativeArray<int> counters,
            out NativeArray<ShinobuTelemetryEntry> telemetry,
            out NativeArray<ShinobuSpatialHashDebugCell> debugCells,
            out NativeArray<BoidMatrixDTO> matrices,
            out NativeArray<float4> customData,
            out NativeArray<BoidIndirectArgsDTO> indirectArgs,
            out NativeArray<int> spatialHashBucketHeads,
            out NativeArray<int> spatialHashNext)
        {
            entities = default;
            aups = default;
            boidStates = default;
            entitySnapshot = default;
            aupSnapshot = default;
            boidStateSnapshot = default;
            sectors = default;
            tuning = default;
            counters = default;
            telemetry = default;
            debugCells = default;
            matrices = default;
            customData = default;
            indirectArgs = default;
            spatialHashBucketHeads = default;
            spatialHashNext = default;

            return TryOpenVaultView(vault, in _entityHandle, entityCapacity, out entities) &&
                   TryOpenVaultView(vault, in _aupHandle, entityCapacity, out aups) &&
                   TryOpenVaultView(vault, in _boidStateHandle, entityCapacity, out boidStates) &&
                   TryOpenVaultView(vault, in _entitySnapshotHandle, entityCapacity, out entitySnapshot) &&
                   TryOpenVaultView(vault, in _aupSnapshotHandle, entityCapacity, out aupSnapshot) &&
                   TryOpenVaultView(vault, in _boidStateSnapshotHandle, entityCapacity, out boidStateSnapshot) &&
                   TryOpenVaultView(vault, in _sectorHandle, sectorCapacity, out sectors) &&
                   TryOpenVaultView(vault, in _tuningHandle, 1, out tuning) &&
                   TryOpenVaultView(vault, in _counterHandle, CounterCapacity, out counters) &&
                   TryOpenVaultView(vault, in _telemetryHandle, TelemetryCapacity, out telemetry) &&
                   TryOpenVaultView(vault, in _debugCellHandle, DebugCellCapacity, out debugCells) &&
                   TryOpenVaultView(vault, in _renderMatrixHandle, entityCapacity, out matrices) &&
                   TryOpenVaultView(vault, in _renderCustomDataHandle, entityCapacity, out customData) &&
                   TryOpenVaultView(vault, in _indirectArgsHandle, 1, out indirectArgs) &&
                   TryOpenVaultView(vault, in _spatialHashBucketHeadHandle, SpatialHashBucketCapacity, out spatialHashBucketHeads) &&
                   TryOpenVaultView(vault, in _spatialHashNextHandle, entityCapacity + sectorCapacity, out spatialHashNext);
        }

        private bool TryResolveBuffers(
            IDataVault vault,
            out NativeArray<AmbientEntityDTO> entities,
            out NativeArray<AmbientEntityAupDTO> aups,
            out NativeArray<EcosystemSectorDTO> sectors,
            out NativeArray<ShinobuEcosystemTuning> tuning,
            out NativeArray<int> counters,
            out NativeArray<ShinobuTelemetryEntry> telemetry,
            out NativeArray<ShinobuSpatialHashDebugCell> debugCells,
            out NativeArray<BoidMatrixDTO> matrices,
            out NativeArray<float4> customData)
        {
            return TryResolveBuffers(
                vault,
                out entities,
                out aups,
                out _,
                out _,
                out _,
                out _,
                out sectors,
                out tuning,
                out counters,
                out telemetry,
                out debugCells,
                out matrices,
                out customData,
                out _,
                out _,
                out _);
        }

        private void EnsureProfilesLoaded(IDataVault vault)
        {
            if (!TryOpenVaultView(vault, in _counterHandle, CounterCapacity, out NativeArray<int> counters) ||
                !TryOpenVaultView(vault, in _tuningHandle, 1, out NativeArray<ShinobuEcosystemTuning> tuning))
            {
                return;
            }

            if (counters.Length > CounterProfileLoaded && counters[CounterProfileLoaded] != 0)
                return;

            if (!TryLoadLegacyProfilesIntoVault(vault, tuning))
                GenerateEmergencyMockProfiles(tuning);

            if (counters.Length > CounterProfileLoaded)
                counters[CounterProfileLoaded] = 1;
        }

        private bool TryLoadLegacyProfilesIntoVault(IDataVault vault, NativeArray<ShinobuEcosystemTuning> tuning)
        {
            try
            {
                string profilePath = TryFindLegacyProfilePath();
                if (string.IsNullOrEmpty(profilePath) || !File.Exists(profilePath))
                    return false;

                if (!TryOpenVaultView(vault, in _legacyScratchHandle, LegacyProfileReadBytes, out NativeArray<byte> scratch))
                    return false;

                int bytesRead = ReadFileIntoNativeScratch(profilePath, scratch, LegacyProfileReadBytes, FileShare.Read);

                if (bytesRead < 24)
                    return false;

                ShinobuEcosystemTuning profile = ShinobuEcosystemTuning.CreateDefault();
                profile.SeparationWeight = ReadFloatLE(scratch, 0, profile.SeparationWeight);
                profile.AlignmentWeight = ReadFloatLE(scratch, 4, profile.AlignmentWeight);
                profile.CohesionWeight = ReadFloatLE(scratch, 8, profile.CohesionWeight);
                profile.PredatorAvoidanceWeight = ReadFloatLE(scratch, 12, profile.PredatorAvoidanceWeight);
                profile.HerbivoreBirthRate = ReadFloatLE(scratch, 16, profile.HerbivoreBirthRate);
                profile.CarnivoreDeathRate = ReadFloatLE(scratch, 20, profile.CarnivoreDeathRate);
                profile.Flags = TuningFlagLegacyBinary;
                tuning[0] = ShinobuEcosystemTuning.Sanitize(profile);
                _runtimeFlags &= ~TuningFlagEmergencyMock;
                _runtimeFlags |= TuningFlagLegacyBinary;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static unsafe int ReadFileIntoNativeScratch(string path, NativeArray<byte> scratch, int maxBytes, FileShare share)
        {
            if (!scratch.IsCreated || string.IsNullOrEmpty(path))
                return 0;

            int limit = math.min(math.max(0, maxBytes), scratch.Length);
            if (limit <= 0)
                return 0;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, share, math.max(1, limit), FileOptions.SequentialScan))
            {
                void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                return stream.Read(new Span<byte>(pointer, limit));
            }
        }

        private void GenerateEmergencyMockProfiles(NativeArray<ShinobuEcosystemTuning> tuning)
        {
            if (!tuning.IsCreated || tuning.Length <= 0)
                return;

            ShinobuEcosystemTuning profile = ShinobuEcosystemTuning.CreateDefault();
            profile.Flags = TuningFlagEmergencyMock;
            tuning[0] = profile;
            _runtimeFlags |= TuningFlagEmergencyMock;
            _runtimeFlags &= ~TuningFlagLegacyBinary;
        }

        private void EnsureInitialPopulation(IDataVault vault)
        {
            if (!TryOpenVaultView(vault, in _counterHandle, CounterCapacity, out NativeArray<int> counters) ||
                counters.Length <= CounterInitialized ||
                counters[CounterInitialized] != 0)
            {
                return;
            }

            if (!TryOpenVaultView(vault, in _entityHandle, entityCapacity, out NativeArray<AmbientEntityDTO> entities) ||
                !TryOpenVaultView(vault, in _aupHandle, entityCapacity, out NativeArray<AmbientEntityAupDTO> aups) ||
                !TryOpenVaultView(vault, in _sectorHandle, sectorCapacity, out NativeArray<EcosystemSectorDTO> sectors))
            {
                return;
            }

            int count = math.min(entityCapacity, math.min(entities.Length, aups.Length));
            uint seed = 0x53484E31u;
            AbsoluteUniversePosition center = _cameraAup;
            for (int i = 0; i < count; i++)
            {
                seed = NextLcg(seed);
                float angle = (seed & 65535u) * (math.PI * 2f / 65535f);
                seed = NextLcg(seed);
                float radius = 18f + ((seed & 1023u) * (90f / 1023f));
                seed = NextLcg(seed);
                float y = -8f + ((seed & 511u) * (16f / 511f));
                float3 local = new float3(CosPolynomial7(angle) * radius, y, SinPolynomial7(angle) * radius);
                uint speciesHash = (i % 5) == 0 ? 0x4341524Eu : 0x48455242u; // CARN/HERB
                float biomass = (speciesHash == 0x4341524Eu) ? 2.5f : 1f;
                uint flags = EntityFlagActive | EntityFlagHydrated |
                             ((speciesHash == 0x4341524Eu) ? EntityFlagCarnivore : EntityFlagHerbivore);
                AbsoluteUniversePosition aup = OffsetAup(in center, local);
                int3 sectorCoord = ResolveSectorCoord(in aup, sectorSizeMeters);
                uint sectorHash = ResolveSectorHash(sectorCoord);

                entities[i] = new AmbientEntityDTO
                {
                    Position = local,
                    Velocity = SafeNormalize(new float3(-local.z, 0f, local.x), new float3(0f, 0f, 1f)) * DefaultBoidSpeedMetersPerSecond,
                    SpeciesHash = speciesHash,
                    Biomass = biomass
                };
                aups[i] = new AmbientEntityAupDTO
                {
                    PositionAup = aup,
                    Flags = flags,
                    SectorHash = sectorHash,
                    SpatialCellHash = 0,
                    StableSeed = seed
                };
            }

            for (int i = count; i < aups.Length; i++)
            {
                entities[i] = default;
                aups[i] = new AmbientEntityAupDTO
                {
                    Flags = EntityFlagFree,
                    StableSeed = Hash32((uint)i ^ 0x46524545u)
                };
            }

            for (int i = 0; i < sectors.Length; i++)
                sectors[i] = default;

            counters[CounterInitialized] = 1;
        }

        private void MonitorCsvOverrides(IDataVault vault)
        {
            MonitorTuningCsvOverrides(vault);
            MonitorSwarmSpeciesProfiles(vault);
        }

        private void MonitorTuningCsvOverrides(IDataVault vault)
        {
            try
            {
                string path = ResolveTuningCsvPath();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return;

                DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
                if (lastWriteUtc.Ticks == _csvTimestampTicks)
                    return;

                if (!TryOpenVaultView(vault, in _csvScratchHandle, CsvMaxBytes, out NativeArray<byte> scratch))
                    return;

                int bytesRead = ReadFileIntoNativeScratch(path, scratch, CsvMaxBytes, FileShare.ReadWrite);

                if (bytesRead <= 0)
                    return;

                if (!TryOpenVaultView(vault, in _tuningHandle, 1, out NativeArray<ShinobuEcosystemTuning> tuning))
                    return;

                TryOpenVaultView(vault, in _counterHandle, CounterCapacity, out NativeArray<int> counters);
                ShinobuEcosystemTuning profile = tuning[0];
                ParseCsvOverrides(scratch, bytesRead, ref profile);
                profile.Flags |= TuningFlagCsvOverride;
                tuning[0] = ShinobuEcosystemTuning.Sanitize(profile);
                if (counters.IsCreated && counters.Length > CounterCsvLoaded)
                    counters[CounterCsvLoaded]++;

                _csvTimestampTicks = lastWriteUtc.Ticks;
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x43535646u, SourceHash, 0f);
            }
        }

        private void MonitorSwarmSpeciesProfiles(IDataVault vault)
        {
            try
            {
                string path = ResolveSwarmSpeciesCsvPath();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return;

                DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
                if (lastWriteUtc.Ticks == _swarmSpeciesCsvTimestampTicks)
                    return;

                if (!TryOpenVaultView(vault, in _csvScratchHandle, CsvMaxBytes, out NativeArray<byte> scratch) ||
                    !TryOpenVaultView(vault, in _swarmSpeciesProfileHandle, SwarmSpeciesProfileCapacity, out NativeArray<SwarmSpeciesProfileDTO> profiles))
                {
                    return;
                }

                int bytesRead = ReadFileIntoNativeScratch(path, scratch, CsvMaxBytes, FileShare.ReadWrite);
                if (bytesRead <= 0)
                    return;

                TryOpenVaultView(vault, in _counterHandle, CounterCapacity, out NativeArray<int> counters);
                int parsed = ParseSwarmSpeciesProfiles(scratch, bytesRead, profiles);
                if (counters.IsCreated && counters.Length > CounterProfileLoaded)
                    counters[CounterProfileLoaded] = math.max(counters[CounterProfileLoaded], parsed);

                _swarmSpeciesCsvTimestampTicks = lastWriteUtc.Ticks;
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x53504346u, SourceHash, 0f);
            }
        }

        private void RunMacroBiomassPass(IDataVault vault)
        {
            if (!TryResolveBuffers(
                    vault,
                    out NativeArray<AmbientEntityDTO> entities,
                    out NativeArray<AmbientEntityAupDTO> aups,
                    out _,
                    out _,
                    out _,
                    out _,
                    out NativeArray<EcosystemSectorDTO> sectors,
                    out NativeArray<ShinobuEcosystemTuning> tuningArray,
                    out NativeArray<int> counters,
                    out NativeArray<ShinobuTelemetryEntry> telemetry,
                    out NativeArray<ShinobuSpatialHashDebugCell> debugCells,
                    out NativeArray<BoidMatrixDTO> matrices,
                    out NativeArray<float4> customData,
                    out _,
                    out NativeArray<int> spatialHashBucketHeads,
                    out NativeArray<int> spatialHashNext))
            {
                return;
            }

            if (!TryLockJobBuffers(vault))
                return;

            JobHandle scheduledHandle = default;
            bool scheduledWork = false;
            try
            {
                ShinobuEcosystemTuning tuning = ShinobuEcosystemTuning.Sanitize(tuningArray[0]);
                float visualQualityWeight = ResolveGlobalQualityWeight01();
                var job = new LotkaVolterraMacroJob
                {
                    Entities = entities,
                    Aups = aups,
                    Sectors = sectors,
                    SectorBucketHeads = spatialHashBucketHeads,
                    SectorEntityLinks = spatialHashNext,
                    Counters = counters,
                    CenterAup = _cameraAup,
                    Tuning = tuning,
                    GlobalQualityWeight = AuthoritativeQualityWeight,
                    EntityCount = math.min(entityCapacity, math.min(entities.Length, aups.Length)),
                    SectorCount = math.min(sectorCapacity, sectors.Length),
                    SectorSizeMeters = math.max(1f, sectorSizeMeters),
                    DehydrationDistanceSq = dehydrationDistanceMeters * dehydrationDistanceMeters,
                    RehydrationDistanceSq = rehydrationDistanceMeters * rehydrationDistanceMeters,
                    ApplyLotka = (_coldTickIndex % 60) == 0 ? 1 : 0,
                    Frame = ResolveCurrentSimulationFrame()
                };

                _scheduleTicks = Stopwatch.GetTimestamp();
                _activeJobHandle = job.Schedule();
                scheduledHandle = _activeJobHandle;
                scheduledWork = true;
                _lastActiveBudget = job.EntityCount;
                _lastGlobalQualityWeight = visualQualityWeight;
                _lastSpatialHashMs = 0f;
                _lastMatrixUploadMs = 0f;
                _runtimeFlags |= TelemetryFlagMacroPass;
                _scheduledPipelineKind = ScheduledPipelineMacro;
                _jobScheduled = true;
                _jobLocksHeld = true;
                H8Memory.RegisterActiveJob(SystemID.AIEcology, _activeJobHandle);
            }
            catch (Exception)
            {
                if (scheduledWork)
                {
                    _activeJobHandle = scheduledHandle;
                    _scheduledPipelineKind = ScheduledPipelineMacro;
                    _jobScheduled = true;
                    _jobLocksHeld = true;
                }
                else
                {
                    UnlockJobBuffers();
                }

                GlobalTelemetryBus.PublishPerformanceWarning(0x534D4143u, SourceHash, 0f);
            }
        }

        private void TryFinalizeFrameJobNoWait()
        {
            if (!_jobScheduled)
                return;

            if (!_activeJobHandle.IsCompleted)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _activeJobHandle))
                return;

            FinishFrameJobCompletion();
        }

        private void CompleteFrameJobForTeardown()
        {
            if (!_jobScheduled)
                return;

            if (!DispatcherJobFence.TryComplete(ref _activeJobHandle, forceComplete: true))
                return;

            FinishFrameJobCompletion();
        }

        private void FinishFrameJobCompletion()
        {
            byte pipelineKind = _scheduledPipelineKind;
            _jobScheduled = false;
            _scheduledPipelineKind = ScheduledPipelineNone;
            long completeTicks = Stopwatch.GetTimestamp();
            long elapsedTicks = completeTicks >= _scheduleTicks ? completeTicks - _scheduleTicks : 0L;
            float elapsedMs = Stopwatch.Frequency > 0
                ? (float)(elapsedTicks * 1000.0 / Stopwatch.Frequency)
                : 0f;
            _lastFlockingMs = pipelineKind == ScheduledPipelineFrame ? elapsedMs : 0f;

            IDataVault vault = _dataVault;
            if (vault != null)
            {
                if (pipelineKind == ScheduledPipelineFrame)
                    UploadCompletedFrameToGpu(vault);

                WriteTelemetryAndFaultDump(vault);
            }

            UnlockJobBuffers();
        }

        private void UploadCompletedFrameToGpu(IDataVault vault)
        {
            _lastMatrixUploadMs = 0f;
            if (Application.isBatchMode)
                return;

            if (!TryOpenVaultView(vault, in _renderMatrixHandle, entityCapacity, out NativeArray<BoidMatrixDTO> matrices) ||
                !TryOpenVaultView(vault, in _renderCustomDataHandle, entityCapacity, out NativeArray<float4> customData) ||
                !TryOpenVaultView(vault, in _indirectArgsHandle, 1, out NativeArray<BoidIndirectArgsDTO> indirectArgs))
            {
                return;
            }

            long startTicks = Stopwatch.GetTimestamp();
            bool uploaded = false;
            try
            {
                uploaded = _gpuUploadDispatcher.UploadFromVault(matrices, customData, indirectArgs);
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x47505555u, SourceHash, 0f);
            }

            if (!uploaded || Stopwatch.Frequency <= 0)
                return;

            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            _lastMatrixUploadMs = elapsedTicks > 0L
                ? (float)(elapsedTicks * 1000.0 / Stopwatch.Frequency)
                : 0f;
        }

        private void WriteTelemetryAndFaultDump(IDataVault vault)
        {
            if (!TryOpenVaultView(vault, in _telemetryHandle, TelemetryCapacity, out NativeArray<ShinobuTelemetryEntry> telemetry) ||
                !TryOpenVaultView(vault, in _counterHandle, CounterCapacity, out NativeArray<int> counters))
            {
                return;
            }

            int cursor = _telemetryCursor;
            if (cursor < 0 || cursor >= int.MaxValue - telemetry.Length)
            {
                int wrapped = cursor % telemetry.Length;
                if (wrapped < 0)
                    wrapped += telemetry.Length;
                cursor = telemetry.Length + wrapped;
            }

            int index = cursor % telemetry.Length;
            int nextCursor = cursor + 1;
            _telemetryCursor = nextCursor;

            int active = ReadCounter(counters, CounterActive);
            int hydrated = ReadCounter(counters, CounterHydrated);
            int dehydrated = ReadCounter(counters, CounterDehydratedSectors);
            int skipped = ReadCounter(counters, CounterSkipped);
            int invalidMath = ReadCounter(counters, CounterInvalidMath);
            int overflow = ReadCounter(counters, CounterSpatialHashOverflow);
            uint stateHash = MixTelemetryHash(active, hydrated, dehydrated, skipped, invalidMath, overflow);
            bool solveOverBudget = _lastFlockingMs > TelemetryFaultThresholdMs;

            telemetry[index] = new ShinobuTelemetryEntry
            {
                Frame = ResolveCurrentSimulationFrame(),
                StateHash = stateHash,
                ActiveBoidCount = active,
                HydratedBoidCount = hydrated,
                DehydratedSectorCount = dehydrated,
                SkippedBoidCount = skipped,
                FlockingSolveTimeMs = math.max(0f, _lastFlockingMs),
                GlobalQualityWeight = _lastGlobalQualityWeight,
                Flags = _runtimeFlags |
                        (invalidMath != 0 ? EntityFlagInvalidMath : 0u) |
                        (overflow != 0 ? 0x80000000u : 0u) |
                        (solveOverBudget ? TelemetryFlagSolveOverBudget : 0u),
                SpatialHashTimeMs = math.max(0f, _lastSpatialHashMs),
                MatrixUploadTimeMs = math.max(0f, _lastMatrixUploadMs),
                ReproducedCount = ReadCounter(counters, CounterReproduced),
                TombstonedCount = ReadCounter(counters, CounterTombstoned),
                DebugCellCount = ReadCounter(counters, CounterDebugCellCount),
                CsvLoadedCount = (ushort)math.clamp(ReadCounter(counters, CounterCsvLoaded), 0, ushort.MaxValue),
                ProfileLoadedCount = (ushort)math.clamp(ReadCounter(counters, CounterProfileLoaded), 0, ushort.MaxValue)
            };

            if ((invalidMath != 0 || overflow != 0 || solveOverBudget) && !_dumpedFault)
            {
                _dumpedFault = true;
                DumpBlackBox(telemetry, nextCursor);
            }
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            if (vault == null || _jobLocksHeld)
                return false;

            int lockedCount = 0;
            if (!vault.TryLockBuffer(BufferID.ShinobuAmbientEntities, SystemID.AIEcology)) return false;
            lockedCount++;
            if (!vault.TryLockBuffer(BufferID.ShinobuAmbientAups, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(BufferID.ShinobuBoidStates, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(BufferID.ShinobuAmbientEntitySnapshot, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(BufferID.ShinobuAmbientAupSnapshot, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(BufferID.ShinobuBoidStateSnapshot, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(BufferID.ShinobuEcosystemSectors, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(BufferID.ShinobuEcosystemTuning, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(BufferID.ShinobuEcosystemCounters, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(BufferID.ShinobuEcosystemTelemetryRing, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSpatialHashDebugCells, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(BufferID.ShinobuRenderMatrices, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(BufferID.ShinobuRenderCustomData, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(BufferID.ShinobuBoidIndirectArgs, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSpatialHashBucketHeads, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSpatialHashNext, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
            lockedCount++;

            _jobLocksHeld = true;
            return true;
        }

        private void UnlockJobBuffers()
        {
            if (!_jobLocksHeld)
                return;

            IDataVault vault = _dataVault;
            if (vault != null)
                UnlockLockedJobBuffers(vault, 16);
            _jobLocksHeld = false;
        }

        private static void UnlockLockedJobBuffers(IDataVault vault, int lockedCount)
        {
            if (lockedCount >= 16) vault.TryUnlockBuffer(BufferID.ShinobuSpatialHashNext, SystemID.AIEcology);
            if (lockedCount >= 15) vault.TryUnlockBuffer(BufferID.ShinobuSpatialHashBucketHeads, SystemID.AIEcology);
            if (lockedCount >= 14) vault.TryUnlockBuffer(BufferID.ShinobuBoidIndirectArgs, SystemID.AIEcology);
            if (lockedCount >= 13) vault.TryUnlockBuffer(BufferID.ShinobuRenderCustomData, SystemID.AIEcology);
            if (lockedCount >= 12) vault.TryUnlockBuffer(BufferID.ShinobuRenderMatrices, SystemID.AIEcology);
            if (lockedCount >= 11) vault.TryUnlockBuffer(BufferID.ShinobuSpatialHashDebugCells, SystemID.AIEcology);
            if (lockedCount >= 10) vault.TryUnlockBuffer(BufferID.ShinobuEcosystemTelemetryRing, SystemID.AIEcology);
            if (lockedCount >= 9) vault.TryUnlockBuffer(BufferID.ShinobuEcosystemCounters, SystemID.AIEcology);
            if (lockedCount >= 8) vault.TryUnlockBuffer(BufferID.ShinobuEcosystemTuning, SystemID.AIEcology);
            if (lockedCount >= 7) vault.TryUnlockBuffer(BufferID.ShinobuEcosystemSectors, SystemID.AIEcology);
            if (lockedCount >= 6) vault.TryUnlockBuffer(BufferID.ShinobuBoidStateSnapshot, SystemID.AIEcology);
            if (lockedCount >= 5) vault.TryUnlockBuffer(BufferID.ShinobuAmbientAupSnapshot, SystemID.AIEcology);
            if (lockedCount >= 4) vault.TryUnlockBuffer(BufferID.ShinobuAmbientEntitySnapshot, SystemID.AIEcology);
            if (lockedCount >= 3) vault.TryUnlockBuffer(BufferID.ShinobuBoidStates, SystemID.AIEcology);
            if (lockedCount >= 2) vault.TryUnlockBuffer(BufferID.ShinobuAmbientAups, SystemID.AIEcology);
            if (lockedCount >= 1) vault.TryUnlockBuffer(BufferID.ShinobuAmbientEntities, SystemID.AIEcology);
        }

        private void TryRegisterTicks()
        {
            if (!_registeredTick)
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            if (!_registeredColdTick)
                _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);

            if (!_registeredTick || !_registeredColdTick || !_registeredLateFrame)
                TryUnregisterTicks();
        }

        private void TryUnregisterTicks()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredColdTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }
        }

        private void TryRegisterRender()
        {
            if (_registeredRender ||
                !_proceduralRenderEnabled ||
                _proceduralRenderMaterial == null ||
                Application.isBatchMode ||
                GlobalRegistry.RenderDispatcher == null)
            {
                return;
            }

            _registeredRender = GlobalRegistry.Renderables.TryRegister(this);
        }

        private void TryUnregisterRender()
        {
            if (!_registeredRender)
                return;

            GlobalRegistry.Renderables.TryUnregister(this);
            _registeredRender = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void ResetVaultHandles()
        {
            _vaultBuffersReady = false;
            _entityHandle = default;
            _aupHandle = default;
            _boidStateHandle = default;
            _entitySnapshotHandle = default;
            _aupSnapshotHandle = default;
            _boidStateSnapshotHandle = default;
            _sectorHandle = default;
            _tuningHandle = default;
            _counterHandle = default;
            _telemetryHandle = default;
            _debugCellHandle = default;
            _renderMatrixHandle = default;
            _renderCustomDataHandle = default;
            _indirectArgsHandle = default;
            _spatialHashBucketHeadHandle = default;
            _spatialHashNextHandle = default;
            _csvScratchHandle = default;
            _legacyScratchHandle = default;
            _swarmSpeciesProfileHandle = default;
        }

        private void ClearCachedState()
        {
            _dataVault = null;
            _vaultBuffersReady = false;
            ResetVaultHandles();
            _telemetryCursor = 0;
            _coldTickIndex = 0;
            _simulationFrameCounter = 0u;
            _lastFlockingMs = 0f;
            _lastSpatialHashMs = 0f;
            _lastMatrixUploadMs = 0f;
            _lastGlobalQualityWeight = 0f;
            _lastActiveBudget = 0;
            _scheduleTicks = 0L;
            _csvTimestampTicks = 0L;
            _swarmSpeciesCsvTimestampTicks = 0L;
            _runtimeFlags = 0u;
            _dumpedFault = false;
            _proceduralRenderEnabled = false;
            _proceduralRenderMaterial = null;
            _proceduralRenderBounds = default;
            _proceduralRenderLayer = 0;
        }

        private void RefreshCameraSignals()
        {
            ReadOnlySpan<PlayerStateSignal> playerSignals = SignalBus<PlayerStateSignal>.GetFrameSnapshot();
            for (int i = 0; i < playerSignals.Length; i++)
            {
                PlayerStateSignal signal = playerSignals[i];
                if ((signal.Flags & PlayerStateSignal.FlagActive) == 0)
                    continue;

                if (IsFiniteAup(in signal.PositionAup))
                    _cameraAup = signal.PositionAup;
            }

            ReadOnlySpan<CameraPositionSignal> cameraSignals = SignalBus<CameraPositionSignal>.GetFrameSnapshot();
            for (int i = 0; i < cameraSignals.Length; i++)
            {
                CameraPositionSignal signal = cameraSignals[i];
                if ((signal.Flags & 1) == 0)
                    continue;

                if (math.all(math.isfinite(signal.Position)))
                    _cameraLocalPosition = signal.Position;

                if (math.all(math.isfinite(signal.Forward)))
                    _cameraForward = SafeNormalize(signal.Forward, _cameraForward);
            }
        }

        private MockPredatorRuntime ResolvePredatorRuntime()
        {
            MockPredatorRuntime runtime = default;
            ReadOnlySpan<FaunaStateChangedSignal> faunaSignals = SignalBus<FaunaStateChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < faunaSignals.Length; i++)
            {
                FaunaStateChangedSignal signal = faunaSignals[i];
                if ((signal.Flags & FaunaStateChangedSignalFlags.StateActive) == 0 ||
                    signal.StateKind != FaunaStateChangedSignalKinds.Strike ||
                    !IsFiniteAup(in signal.PositionAup))
                {
                    continue;
                }

                runtime.PositionLocal = AupToLocal(in signal.PositionAup, in _cameraAup);
                runtime.RadiusMeters = 48f;
                runtime.Intensity01 = 1f;
                runtime.SectorHash = ResolveSectorHash(ResolveSectorCoord(in signal.PositionAup, sectorSizeMeters));
                runtime.Valid = 1;
            }

            ReadOnlySpan<MockPredatorSignal> signals = SignalBus<MockPredatorSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                MockPredatorSignal signal = signals[i];
                if (signal.RadiusMeters <= 0f || signal.Intensity01 <= 0f)
                    continue;

                runtime.PositionLocal = AupToLocal(in signal.PositionAup, in _cameraAup);
                runtime.RadiusMeters = math.max(1f, signal.RadiusMeters);
                runtime.Intensity01 = math.saturate(signal.Intensity01);
                runtime.SectorHash = signal.SectorHash != 0u
                    ? signal.SectorHash
                    : ResolveSectorHash(ResolveSectorCoord(in signal.PositionAup, sectorSizeMeters));
                runtime.Valid = 1;
            }

            return runtime;
        }

        private static float ResolveSystemStress01()
        {
            float stress = math.saturate(SignalBusRegistry.SystemStress01);
            ReadOnlySpan<SystemHealthIndexSignal> signals = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                SystemHealthIndexSignal signal = signals[i];
                float signalStress = math.saturate(signal.Pressure01);
                if (signal.State >= SystemHealthIndexSignal.StateCritical)
                    signalStress = math.max(signalStress, 1f);
                else if (signal.State >= SystemHealthIndexSignal.StateWarning)
                    signalStress = math.max(signalStress, 0.72f);
                if ((signal.Flags & SystemHealthIndexSignal.FlagAdrenaline) != 0)
                    signalStress = math.max(signalStress, 0.9f);
                stress = math.max(stress, signalStress);
            }

            return stress;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(weight) ? math.saturate(weight) : 0f;
        }

        private static int ResolveActiveEntityBudget(int capacity, float globalQualityWeight)
        {
            return math.max(0, capacity);
        }

        private static float ResolveActiveBudgetFraction(float quality01)
        {
            float q = math.saturate(quality01);
            float lowBand = math.lerp(0.01f, 0.05f, Smooth01(q * 10f));
            float highBand = math.lerp(0.05f, 1f, Smooth01(math.saturate((q - 0.1f) * 1.1111112f)));
            return math.lerp(lowBand, highBand, math.step(0.1f, q));
        }

        private static int ResolveNeighborSampleBudget(int maxSamples, float globalQualityWeight)
        {
            return math.max(1, maxSamples);
        }

        private static int ResolveSpatialHashChainBudget(int maxChainSteps, float globalQualityWeight)
        {
            return math.max(1, maxChainSteps);
        }

        private static int ResolveUpdateStride(float globalQualityWeight, float systemStress01)
        {
            return 1;
        }

        private uint AdvanceSimulationFrame()
        {
            uint next = _simulationFrameCounter + 1u;
            if (next == 0u)
                next = 1u;

            _simulationFrameCounter = next;
            return next;
        }

        private uint ResolveCurrentSimulationFrame()
        {
            return _simulationFrameCounter != 0u ? _simulationFrameCounter : 1u;
        }

        private static float ResolveSimulationTickDelta(float globalQualityWeight)
        {
            return DefaultSimulationTickDeltaSeconds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - (2f * x));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ResolveNeighborSolveWeight(float globalQualityWeight)
        {
            return 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 SampleEmergencyMockFlow(float3 localPosition, uint stableSeed, float globalQualityWeight)
        {
            float q = Smooth01(globalQualityWeight);
            float scale = math.lerp(0.0065f, 0.024f, q);
            float seedPhase = (stableSeed & 1023u) * 0.006135923f;
            float3 p = localPosition * scale;
            float x = TriangleSigned((p.y * 2.17f) + (p.z * 0.61f) + seedPhase);
            float y = TriangleSigned((p.z * 1.53f) - (p.x * 0.37f) + seedPhase * 0.5f) * math.lerp(0.08f, 0.34f, q);
            float z = TriangleSigned((p.x * 1.89f) + (p.y * 0.43f) - seedPhase);
            float3 coarse = new float3(x, y, z);
            float3 noiseP = (p * math.lerp(0.47f, 1.31f, q)) + new float3(seedPhase, seedPhase * 0.37f, seedPhase * 0.19f);
            float nx = ValueNoise3(noiseP + new float3(11.17f, 3.31f, 7.93f), stableSeed ^ 0x9E3779B9u);
            float ny = ValueNoise3(noiseP + new float3(5.41f, 13.73f, 2.19f), stableSeed ^ 0x85EBCA6Bu);
            float nz = ValueNoise3(noiseP + new float3(17.89f, 1.97f, 19.37f), stableSeed ^ 0xC2B2AE35u);
            float3 perlinStyle = new float3(nx, ny * math.lerp(0.1f, 0.48f, q), nz);
            float richBlend = Smooth01(math.saturate((q - 0.18f) * 1.2195122f));
            float3 flow = math.lerp(coarse, coarse + perlinStyle, richBlend);
            return SafeNormalize(flow, new float3(0f, 0f, 1f)) * math.lerp(0.25f, 2.35f, q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleSigned(float phase)
        {
            float triangle01 = 1f - math.abs((math.frac(phase * 0.15915494f + 0.25f) * 2f) - 1f);
            return (triangle01 * 2f) - 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ValueNoise3(float3 position, uint seed)
        {
            int3 cell = (int3)math.floor(position);
            float3 f = math.frac(position);
            float3 u = f * f * (3f - (2f * f));
            float x00 = math.lerp(SignedHash(cell + new int3(0, 0, 0), seed), SignedHash(cell + new int3(1, 0, 0), seed), u.x);
            float x10 = math.lerp(SignedHash(cell + new int3(0, 1, 0), seed), SignedHash(cell + new int3(1, 1, 0), seed), u.x);
            float x01 = math.lerp(SignedHash(cell + new int3(0, 0, 1), seed), SignedHash(cell + new int3(1, 0, 1), seed), u.x);
            float x11 = math.lerp(SignedHash(cell + new int3(0, 1, 1), seed), SignedHash(cell + new int3(1, 1, 1), seed), u.x);
            float y0 = math.lerp(x00, x10, u.y);
            float y1 = math.lerp(x01, x11, u.y);
            return math.lerp(y0, y1, u.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SignedHash(int3 cell, uint seed)
        {
            unchecked
            {
                uint h = seed ^ 0xA511E9B3u;
                h ^= (uint)cell.x * 0x9E3779B9u;
                h ^= (uint)cell.y * 0x85EBCA6Bu;
                h ^= (uint)cell.z * 0xC2B2AE35u;
                h = Hash32(h);
                return ((h & 0x00FFFFFFu) * (1f / 8388607.5f)) - 1f;
            }
        }

        public static unsafe bool TryUploadRenderMatricesToGpu(
            GraphicsBuffer destination,
            NativeArray<BoidMatrixDTO> source,
            int count)
        {
            if (destination == null || !source.IsCreated || count <= 0)
                return false;

            int safeCount = math.min(count, math.min(source.Length, destination.count));
            if (safeCount <= 0 || destination.stride != UnsafeUtility.SizeOf<BoidMatrixDTO>())
                return false;

            bool locked = false;
            try
            {
                NativeArray<BoidMatrixDTO> mapped = destination.LockBufferForWrite<BoidMatrixDTO>(0, safeCount);
                locked = true;
                void* dst = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                void* src = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(source);
                UnsafeUtility.MemCpy(dst, src, (long)safeCount * UnsafeUtility.SizeOf<BoidMatrixDTO>());
            }
            finally
            {
                if (locked)
                    destination.UnlockBufferAfterWrite<BoidMatrixDTO>(safeCount);
            }

            return true;
        }

        public static bool TryUploadIndirectDrawArgs(
            GraphicsBuffer destination,
            NativeArray<BoidIndirectArgsDTO> source)
        {
            if (destination == null || !source.IsCreated || source.Length <= 0)
                return false;
            if (destination.count < 1 || destination.stride != UnsafeUtility.SizeOf<BoidIndirectArgsDTO>())
                return false;

            BoidIndirectArgsDTO dto = source[0];
            if (dto.InstanceCount == 0u || dto.VertexCountPerInstance == 0u)
                return false;

            bool locked = false;
            try
            {
                NativeArray<BoidIndirectArgsDTO> mapped = destination.LockBufferForWrite<BoidIndirectArgsDTO>(0, 1);
                locked = true;
                mapped[0] = dto;
            }
            finally
            {
                if (locked)
                    destination.UnlockBufferAfterWrite<BoidIndirectArgsDTO>(1);
            }

            return true;
        }

        public static bool TryDrawProceduralIndirect(
            Material material,
            Bounds bounds,
            GraphicsBuffer indirectArgsBuffer,
            int layer)
        {
            if (material == null || indirectArgsBuffer == null)
                return false;

            Graphics.DrawProceduralIndirect(
                material,
                bounds,
                MeshTopology.Triangles,
                indirectArgsBuffer,
                0,
                null,
                null,
                ShadowCastingMode.Off,
                false,
                layer);
            return true;
        }

        internal static JobHandle GenerateEmergencyMockFlow(
            NativeArray<AbyssalFlowTensorDTO> flowTensors,
            float3 originLocal,
            float cellSizeMeters,
            float globalQualityWeight,
            uint sectorHash,
            JobHandle dependsOn)
        {
            if (!flowTensors.IsCreated || flowTensors.Length <= 0)
                return dependsOn;

            var job = new GenerateEmergencyMockFlowJob
            {
                FlowTensors = flowTensors,
                OriginLocal = originLocal,
                CellSizeMeters = math.max(1f, cellSizeMeters),
                GlobalQualityWeight = math.saturate(globalQualityWeight),
                SectorHash = sectorHash
            };
            return job.Schedule(flowTensors.Length, FrameJobBatchSize, dependsOn);
        }

        internal static JobHandle WriteIndirectArgs(
            NativeArray<BoidIndirectArgsDTO> indirectArgs,
            uint vertexCountPerInstance,
            uint startVertex,
            uint startInstance,
            uint activeBoidCount,
            JobHandle dependsOn)
        {
            if (!indirectArgs.IsCreated || indirectArgs.Length <= 0)
                return dependsOn;

            var job = new WriteBoidIndirectArgsJob
            {
                IndirectArgs = indirectArgs,
                VertexCountPerInstance = vertexCountPerInstance,
                StartVertex = startVertex,
                StartInstance = startInstance,
                ActiveBoidCount = activeBoidCount
            };
            return job.Schedule(dependsOn);
        }

        private string TryFindLegacyProfilePath()
        {
            string root = ResolveProjectRoot();
            string archivePath = Path.Combine(root, "Docs", "Archive");
            string streamingPath = Path.Combine(Application.dataPath, "StreamingAssets");
            string directStreamingPath = Path.Combine(root, "StreamingAssets");
            string path = FindFileRecursive(archivePath, LegacyBoidProfileFile);
            if (!string.IsNullOrEmpty(path))
                return path;
            path = FindFileRecursive(archivePath, LegacyFaunaCapsFile);
            if (!string.IsNullOrEmpty(path))
                return path;
            path = FindFileRecursive(streamingPath, LegacyBoidProfileFile);
            if (!string.IsNullOrEmpty(path))
                return path;
            path = FindFileRecursive(directStreamingPath, LegacyBoidProfileFile);
            if (!string.IsNullOrEmpty(path))
                return path;
            return null;
        }

        private static string FindFileRecursive(string root, string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    return null;

                string[] files = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                return files != null && files.Length > 0 ? files[0] : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ResolveSwarmSpeciesCsvPath()
        {
            string root = ResolveProjectRoot();
            string path = Path.Combine(root, SwarmSpeciesCsvPrecomputedRelativePath);
            if (File.Exists(path))
                return path;
            path = Path.Combine(root, SwarmSpeciesCsvRelativePath);
            return File.Exists(path) ? path : null;
        }

        private static string ResolveTuningCsvPath()
        {
            string root = ResolveProjectRoot();
            string path = Path.Combine(root, CsvPrecomputedRelativePath);
            if (File.Exists(path))
                return path;
            path = Path.Combine(root, CsvRelativePath);
            return File.Exists(path) ? path : null;
        }

        private static string ResolveProjectRoot()
        {
            string assetsPath = Application.dataPath;
            DirectoryInfo parent = Directory.GetParent(assetsPath);
            return parent != null ? parent.FullName : assetsPath;
        }

        private static void DumpBlackBox(NativeArray<ShinobuTelemetryEntry> telemetry, int cursor)
        {
            try
            {
                string root = ResolveProjectRoot();
                WriteBlackBoxFile(Path.Combine(root, DumpRelativePath), telemetry, cursor);
                WriteBlackBoxFile(Path.Combine(root, DumpH8RelativePath), telemetry, cursor);
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x444D5046u, SourceHash, 0f);
            }
        }

        private static void WriteBlackBoxFile(string path, NativeArray<ShinobuTelemetryEntry> telemetry, int cursor)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                int capacity = telemetry.Length;
                int written = math.max(0, cursor);
                int dumpCount = math.min(capacity, written);
                int start = written < capacity ? 0 : cursor % capacity;
                writer.Write(DumpMagic);
                writer.Write(DumpVersion);
                writer.Write(capacity);
                writer.Write(dumpCount);
                writer.Write(cursor);
                writer.Write(start);
                writer.Write(UnsafeUtility.SizeOf<ShinobuTelemetryEntry>());
                for (int offset = 0; offset < dumpCount; offset++)
                {
                    ShinobuTelemetryEntry entry = telemetry[(start + offset) % capacity];
                    writer.Write(entry.Frame);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.ActiveBoidCount);
                    writer.Write(entry.HydratedBoidCount);
                    writer.Write(entry.DehydratedSectorCount);
                    writer.Write(entry.SkippedBoidCount);
                    writer.Write(entry.FlockingSolveTimeMs);
                    writer.Write(entry.GlobalQualityWeight);
                    writer.Write(entry.Flags);
                    writer.Write(entry.SpatialHashTimeMs);
                    writer.Write(entry.MatrixUploadTimeMs);
                    writer.Write(entry.ReproducedCount);
                    writer.Write(entry.TombstonedCount);
                    writer.Write(entry.DebugCellCount);
                    writer.Write(entry.CsvLoadedCount);
                    writer.Write(entry.ProfileLoadedCount);
                    writer.Write(entry.Pad0);
                }
            }
        }

        private static void ParseCsvOverrides(NativeArray<byte> bytes, int length, ref ShinobuEcosystemTuning tuning)
        {
            if (!bytes.IsCreated)
                return;

            length = math.min(length, bytes.Length);
            int cursor = 0;
            while (cursor < length)
            {
                int keyStart = cursor;
                while (cursor < length && bytes[cursor] != (byte)',' && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                    cursor++;

                int keyEnd = cursor;
                if (cursor >= length || bytes[cursor] != (byte)',')
                {
                    cursor = SkipLine(bytes, cursor, length);
                    continue;
                }

                cursor++;
                int valueStart = cursor;
                while (cursor < length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                    cursor++;

                uint keyHash = HashAsciiKey(bytes, keyStart, keyEnd);
                if (TryParseFloatAscii(bytes, valueStart, cursor, out float value))
                    ApplyCsvValue(keyHash, value, ref tuning);

                cursor = SkipLine(bytes, cursor, length);
            }
        }

        public static int ParseSwarmSpeciesProfiles(
            NativeArray<byte> bytes,
            int length,
            NativeArray<SwarmSpeciesProfileDTO> profiles)
        {
            if (!bytes.IsCreated || !profiles.IsCreated || profiles.Length <= 0)
                return 0;

            length = math.min(length, bytes.Length);
            int cursor = 0;
            int writeIndex = 0;
            while (cursor < length && writeIndex < profiles.Length)
            {
                if (bytes[cursor] == (byte)'#' || bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r')
                {
                    cursor = SkipLine(bytes, cursor, length);
                    continue;
                }

                int biomassStart = cursor;
                int biomassEnd = ReadCsvCell(bytes, ref cursor, length);
                int meshStart = cursor;
                int meshEnd = ReadCsvCell(bytes, ref cursor, length);
                int materialStart = cursor;
                int materialEnd = ReadCsvCell(bytes, ref cursor, length);
                int speciesStart = cursor;
                int speciesEnd = ReadCsvCell(bytes, ref cursor, length);
                int scaleStart = cursor;
                int scaleEnd = ReadCsvCell(bytes, ref cursor, length);
                int speedStart = cursor;
                int speedEnd = ReadCsvCell(bytes, ref cursor, length);
                int fearStart = cursor;
                int fearEnd = ReadCsvCell(bytes, ref cursor, length);

                if (biomassEnd <= biomassStart || meshEnd <= meshStart || materialEnd <= materialStart)
                {
                    cursor = SkipLine(bytes, cursor, length);
                    continue;
                }

                bool parsedSpecies = TryParseFloatAscii(bytes, speciesStart, speciesEnd, out float speciesValue);
                bool parsedScale = TryParseFloatAscii(bytes, scaleStart, scaleEnd, out float scaleValue);
                bool parsedSpeed = TryParseFloatAscii(bytes, speedStart, speedEnd, out float speedValue);
                bool parsedFear = TryParseFloatAscii(bytes, fearStart, fearEnd, out float fearValue);
                if (!parsedSpecies && !parsedScale && !parsedSpeed && !parsedFear)
                {
                    cursor = SkipLine(bytes, cursor, length);
                    continue;
                }

                profiles[writeIndex++] = new SwarmSpeciesProfileDTO
                {
                    BiomassHash = HashAsciiKey(bytes, biomassStart, biomassEnd),
                    MeshHash = HashAsciiKey(bytes, meshStart, meshEnd),
                    MaterialHash = HashAsciiKey(bytes, materialStart, materialEnd),
                    SpeciesID = (ushort)math.clamp((int)speciesValue, 0, ushort.MaxValue),
                    Flags = 0,
                    Scale = math.isfinite(scaleValue) && scaleValue > 0f ? scaleValue : 1f,
                    Speed = math.isfinite(speedValue) && speedValue > 0f ? speedValue : DefaultBoidSpeedMetersPerSecond,
                    FearResponse = math.isfinite(fearValue) && fearValue >= 0f ? fearValue : 1f,
                    Pad0 = 0u
                };

                cursor = SkipLine(bytes, cursor, length);
            }

            for (int i = writeIndex; i < profiles.Length; i++)
                profiles[i] = default;

            return writeIndex;
        }

        private static int ReadCsvCell(NativeArray<byte> bytes, ref int cursor, int length)
        {
            while (cursor < length && (bytes[cursor] == (byte)' ' || bytes[cursor] == (byte)'\t'))
                cursor++;

            int start = cursor;
            while (cursor < length && bytes[cursor] != (byte)',' && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                cursor++;

            int end = cursor;
            while (end > start && (bytes[end - 1] == (byte)' ' || bytes[end - 1] == (byte)'\t'))
                end--;

            if (cursor < length && bytes[cursor] == (byte)',')
                cursor++;

            return end;
        }

        private static void ApplyCsvValue(uint keyHash, float value, ref ShinobuEcosystemTuning tuning)
        {
            switch (keyHash)
            {
                case 0x6D205EA6u: tuning.SeparationWeight = value; break; // separation_weight
                case 0x3E2D13CFu: tuning.AlignmentWeight = value; break; // alignment_weight
                case 0xA5D6A542u: tuning.CohesionWeight = value; break; // cohesion_weight
                case 0x9FBE9747u: tuning.PredatorAvoidanceWeight = value; break; // predator_avoidance
                case 0xD6DC88ACu: tuning.HerbivoreBirthRate = value; break; // herbivore_birth_rate
                case 0xC2F3944Bu: tuning.CarnivoreBirthRate = value; break; // carnivore_birth_rate
                case 0xE6748D99u: tuning.HerbivoreDeathRate = value; break; // herbivore_death_rate
                case 0x77F7341Eu: tuning.CarnivoreDeathRate = value; break; // carnivore_death_rate
                case 0x2D03238Cu: tuning.FloraGrowthRate = value; break; // flora_growth_rate
                case 0xA83CF84Cu: tuning.FeedRate = value; break; // feed_rate
            }
        }

        private static int SkipLine(NativeArray<byte> bytes, int cursor, int length)
        {
            while (cursor < length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                cursor++;
            while (cursor < length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                cursor++;
            return cursor;
        }

        private static uint HashAsciiKey(NativeArray<byte> bytes, int start, int end)
        {
            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte b = bytes[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                if (b == (byte)' ' || b == (byte)'\t')
                    continue;
                hash ^= b;
                hash *= 16777619u;
            }

            return hash;
        }

        private static bool TryParseFloatAscii(NativeArray<byte> bytes, int start, int end, out float value)
        {
            value = 0f;
            while (start < end && (bytes[start] == (byte)' ' || bytes[start] == (byte)'\t'))
                start++;

            int sign = 1;
            if (start < end && bytes[start] == (byte)'-')
            {
                sign = -1;
                start++;
            }

            double result = 0d;
            bool foundDigit = false;
            while (start < end && bytes[start] >= (byte)'0' && bytes[start] <= (byte)'9')
            {
                result = (result * 10d) + (bytes[start] - (byte)'0');
                start++;
                foundDigit = true;
            }

            if (start < end && bytes[start] == (byte)'.')
            {
                start++;
                double place = 0.1d;
                while (start < end && bytes[start] >= (byte)'0' && bytes[start] <= (byte)'9')
                {
                    result += (bytes[start] - (byte)'0') * place;
                    place *= 0.1d;
                    start++;
                    foundDigit = true;
                }
            }

            if (!foundDigit)
                return false;

            value = (float)(result * sign);
            return math.isfinite(value);
        }

        private static float ReadFloatLE(NativeArray<byte> bytes, int offset, float fallback)
        {
            if (!bytes.IsCreated || offset < 0 || offset > bytes.Length - 4)
                return fallback;

            uint raw = (uint)(bytes[offset] |
                              (bytes[offset + 1] << 8) |
                              (bytes[offset + 2] << 16) |
                              (bytes[offset + 3] << 24));
            float value = math.asfloat(raw);
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadCounter(NativeArray<int> counters, int index)
        {
            return counters.IsCreated && (uint)index < (uint)counters.Length ? counters[index] : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 AupToLocal(in AbsoluteUniversePosition position, in AbsoluteUniversePosition center)
        {
            double3 delta = new double3(
                (((double)position.GridX - center.GridX) * AupCellSizeMetersDouble) + (position.LocalX - center.LocalX),
                (((double)position.GridY - center.GridY) * AupCellSizeMetersDouble) + (position.LocalY - center.LocalY),
                (((double)position.GridZ - center.GridZ) * AupCellSizeMetersDouble) + (position.LocalZ - center.LocalZ));
            return math.all(math.isfinite(delta)) ? (float3)delta : new float3(0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static AbsoluteUniversePosition OffsetAup(in AbsoluteUniversePosition center, float3 localMeters)
        {
            double3 absolute = ToAbsoluteDouble3(in center) + (double3)localMeters;
            return FromAbsoluteDouble3(absolute);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double3 ToAbsoluteDouble3(in AbsoluteUniversePosition aup)
        {
            return new double3(
                (aup.GridX * AupCellSizeMetersDouble) + aup.LocalX,
                (aup.GridY * AupCellSizeMetersDouble) + aup.LocalY,
                (aup.GridZ * AupCellSizeMetersDouble) + aup.LocalZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort ResolveSpeciesId(uint speciesHash)
        {
            uint mixed = speciesHash ^ (speciesHash >> 16);
            return (ushort)(mixed & 0xFFFFu);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static BoidStateDTO BuildBoidState(
            in AbsoluteUniversePosition aup,
            uint speciesHash,
            int packIndex,
            float speed)
        {
            return new BoidStateDTO
            {
                AUP = ToAbsoluteDouble3(in aup),
                SpeciesID = ResolveSpeciesId(speciesHash),
                PackIndex = (ushort)(packIndex & 0xFFFF),
                Speed = math.max(0f, math.isfinite(speed) ? speed : 0f)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static AbsoluteUniversePosition FromAbsoluteDouble3(double3 absolute)
        {
            if (!math.all(math.isfinite(absolute)))
                return default;

            long gridX = (long)math.floor(absolute.x / AupCellSizeMetersDouble);
            long gridY = (long)math.floor(absolute.y / AupCellSizeMetersDouble);
            long gridZ = (long)math.floor(absolute.z / AupCellSizeMetersDouble);
            return new AbsoluteUniversePosition
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = (float)(absolute.x - (gridX * AupCellSizeMetersDouble)),
                LocalY = (float)(absolute.y - (gridY * AupCellSizeMetersDouble)),
                LocalZ = (float)(absolute.z - (gridZ * AupCellSizeMetersDouble))
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsFiniteAup(in AbsoluteUniversePosition aup)
        {
            return math.isfinite(aup.LocalX) &&
                   math.isfinite(aup.LocalY) &&
                   math.isfinite(aup.LocalZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int3 ResolveSectorCoord(in AbsoluteUniversePosition aup, float sectorSize)
        {
            double3 absolute = ToAbsoluteDouble3(in aup);
            double inv = 1.0d / math.max(1.0d, sectorSize);
            return new int3(
                (int)math.floor(absolute.x * inv),
                (int)math.floor(absolute.y * inv),
                (int)math.floor(absolute.z * inv));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ResolveSectorHash(int3 coord)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)coord.x) * 16777619u;
                hash = (hash ^ (uint)coord.y) * 16777619u;
                hash = (hash ^ (uint)coord.z) * 16777619u;
                return hash != 0u ? hash : 1u;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ResolveSpatialCellHash(float3 localPosition, float cellSize)
        {
            int3 cell = (int3)math.floor(localPosition / math.max(0.25f, cellSize));
            return ResolveSpatialCellHash(cell);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ResolveSpatialCellHash(int3 cell)
        {
            unchecked
            {
                return (cell.x * 73856093) ^ (cell.y * 19349663) ^ (cell.z * 83492791);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ResolveSpatialBucketIndex(int cellHash)
        {
            return (int)((uint)cellHash & SpatialHashBucketMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int3 ResolveSpatialCell(float3 localPosition, float cellSize)
        {
            return (int3)math.floor(localPosition / math.max(0.25f, cellSize));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            return math.isfinite(lenSq) && lenSq > 0.000001f ? value * math.rsqrt(math.max(lenSq, 0.000001f)) : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float SafeLength(float3 value)
        {
            float lenSq = math.lengthsq(value);
            return math.isfinite(lenSq) && lenSq > 0.000001f ? lenSq * math.rsqrt(math.max(lenSq, 0.000001f)) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float SinPolynomial7(float angle)
        {
            float x = angle - TrigTwoPi * math.floor((angle + TrigPi) * TrigInvTwoPi);
            x = math.select(x, TrigPi - x, x > TrigHalfPi);
            x = math.select(x, -TrigPi - x, x < -TrigHalfPi);
            float x2 = x * x;
            return x * (1f + x2 * (-0.16666667f + x2 * (0.008333331f + x2 * -0.000198409f)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float CosPolynomial7(float angle)
        {
            return SinPolynomial7(angle + TrigHalfPi);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint NextLcg(uint state)
        {
            return unchecked((state * 1664525u) + 1013904223u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint Hash32(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixTelemetryHash(int active, int hydrated, int dehydrated, int skipped, int invalidMath, int overflow)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)active) * 16777619u;
            hash = (hash ^ (uint)hydrated) * 16777619u;
            hash = (hash ^ (uint)dehydrated) * 16777619u;
            hash = (hash ^ (uint)skipped) * 16777619u;
            hash = (hash ^ (uint)invalidMath) * 16777619u;
            hash = (hash ^ (uint)overflow) * 16777619u;
            return hash;
        }
    }

    /// <summary>
    /// Cold render-graph payload for optional GPU visibility culling of the procedural swarm.
    /// </summary>
    public struct ShinobuSwarmGpuCullingParams
    {
        public const string ClearKernelName = "ClearAbyssalSwarmIndirectArgs";
        public const string CullKernelName = "CullAbyssalSwarm";

        public ComputeShader CullingCompute;
        public Matrix4x4 ViewProjection;
        public Matrix4x4 ViewMatrix;
        public Vector4 ZBufferParams;
        public Texture DepthPyramid;
        public Vector4 DepthPyramidTexelSize;
        public int ClearArgsKernel;
        public int CullKernel;
        public int DepthPyramidMipCount;
        public int OcclusionEnabled;
        public int DensityStep;
        public float OcclusionDepthBias;
        public float BoundsRadius;
        public float QualityWeight;
    }

    /// <summary>
    /// Cold-owned GPU bridge for SHINOBU boid matrices and procedural indirect arguments.
    /// </summary>
    public sealed class ShinobuBoidGpuUploadDispatcher : IDisposable
    {
        private static readonly int _ShinobuBoidMatricesId = Shader.PropertyToID("_H8ShinobuBoidMatrices");
        private static readonly int _ShinobuBoidCustomDataId = Shader.PropertyToID("_H8ShinobuBoidCustomData");
        private static readonly int _ShinobuBoidActiveCountId = Shader.PropertyToID("_H8ShinobuBoidActiveCount");
        private static readonly int _ShinobuBoidVisibleIndicesId = Shader.PropertyToID("_H8ShinobuBoidVisibleIndices");
        private static readonly int _ShinobuBoidUseVisibleIndicesId = Shader.PropertyToID("_H8ShinobuBoidUseVisibleIndices");
        private static readonly int _ShinobuBoidCulledIndirectArgsId = Shader.PropertyToID("_H8ShinobuBoidCulledIndirectArgs");
        private static readonly int _ShinobuSourceCountId = Shader.PropertyToID("_H8ShinobuSourceCount");
        private static readonly int _ShinobuViewProjectionId = Shader.PropertyToID("_H8ShinobuViewProjection");
        private static readonly int _ShinobuViewMatrixId = Shader.PropertyToID("_H8ShinobuViewMatrix");
        private static readonly int _ShinobuZBufferParamsId = Shader.PropertyToID("_H8ShinobuZBufferParams");
        private static readonly int _ShinobuDepthPyramidId = Shader.PropertyToID("_H8ShinobuDepthPyramid");
        private static readonly int _ShinobuDepthPyramidMipCountId = Shader.PropertyToID("_H8ShinobuDepthPyramidMipCount");
        private static readonly int _ShinobuDepthPyramidTexelSizeId = Shader.PropertyToID("_H8ShinobuDepthPyramidTexelSize");
        private static readonly int _ShinobuOcclusionEnabledId = Shader.PropertyToID("_H8ShinobuOcclusionEnabled");
        private static readonly int _ShinobuOcclusionDepthBiasId = Shader.PropertyToID("_H8ShinobuOcclusionDepthBias");
        private static readonly int _ShinobuBoundsRadiusId = Shader.PropertyToID("_H8ShinobuBoundsRadius");
        private static readonly int _ShinobuQualityWeightId = Shader.PropertyToID("_H8ShinobuQualityWeight");
        private static readonly int _ShinobuDensityStepId = Shader.PropertyToID("_H8ShinobuDensityStep");

        private GraphicsBuffer _matrixBufferA;
        private GraphicsBuffer _matrixBufferB;
        private GraphicsBuffer _customDataBufferA;
        private GraphicsBuffer _customDataBufferB;
        private GraphicsBuffer _argsBufferA;
        private GraphicsBuffer _argsBufferB;
        private GraphicsBuffer _visibleIndexBufferA;
        private GraphicsBuffer _visibleIndexBufferB;
        private GraphicsBuffer _culledArgsBufferA;
        private GraphicsBuffer _culledArgsBufferB;
        private int _capacity;
        private int _activeCount;
        private int _writeIndex;
        private int _activeIndex = -1;

        /// <summary>
        /// Allocates or validates double-buffered GPU resources for the requested boid capacity.
        /// </summary>
        public bool EnsureGraphicsResources(int requiredCapacity)
        {
            int capacity = math.max(1, requiredCapacity);
            if (_capacity >= capacity &&
                IsValid(_matrixBufferA, _capacity, UnsafeUtility.SizeOf<BoidMatrixDTO>()) &&
                IsValid(_matrixBufferB, _capacity, UnsafeUtility.SizeOf<BoidMatrixDTO>()) &&
                IsValid(_customDataBufferA, _capacity, UnsafeUtility.SizeOf<float4>()) &&
                IsValid(_customDataBufferB, _capacity, UnsafeUtility.SizeOf<float4>()) &&
                IsValid(_argsBufferA, 1, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>()) &&
                IsValid(_argsBufferB, 1, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>()) &&
                IsValid(_visibleIndexBufferA, _capacity, UnsafeUtility.SizeOf<uint>()) &&
                IsValid(_visibleIndexBufferB, _capacity, UnsafeUtility.SizeOf<uint>()) &&
                IsValidByteAddress(_culledArgsBufferA, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>()) &&
                IsValidByteAddress(_culledArgsBufferB, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>()))
            {
                return true;
            }

            ReleaseGraphicsResources();
            _capacity = NextPowerOfTwo(capacity);
            _matrixBufferA = CreateStructuredLockBuffer<BoidMatrixDTO>(_capacity); // COLD ALLOC: GraphicsBuffer[BoidMatrixDTO A] - double-buffered SHINOBU matrix upload - owner: SHINOBU_105
            _matrixBufferB = CreateStructuredLockBuffer<BoidMatrixDTO>(_capacity); // COLD ALLOC: GraphicsBuffer[BoidMatrixDTO B] - double-buffered SHINOBU matrix upload - owner: SHINOBU_105
            _customDataBufferA = CreateStructuredLockBuffer<float4>(_capacity); // COLD ALLOC: GraphicsBuffer[float4 custom A] - double-buffered SHINOBU shader scalar upload - owner: SHINOBU_105
            _customDataBufferB = CreateStructuredLockBuffer<float4>(_capacity); // COLD ALLOC: GraphicsBuffer[float4 custom B] - double-buffered SHINOBU shader scalar upload - owner: SHINOBU_105
            _argsBufferA = CreateIndirectArgsBuffer(); // COLD ALLOC: GraphicsBuffer[BoidIndirectArgsDTO A] - DrawProceduralIndirect args - owner: SHINOBU_105
            _argsBufferB = CreateIndirectArgsBuffer(); // COLD ALLOC: GraphicsBuffer[BoidIndirectArgsDTO B] - DrawProceduralIndirect args - owner: SHINOBU_105
            _visibleIndexBufferA = CreateStructuredGpuBuffer<uint>(_capacity); // COLD ALLOC: GraphicsBuffer[uint visible A] - GPU compacted SHINOBU visible indices - owner: SHINOBU_105
            _visibleIndexBufferB = CreateStructuredGpuBuffer<uint>(_capacity); // COLD ALLOC: GraphicsBuffer[uint visible B] - GPU compacted SHINOBU visible indices - owner: SHINOBU_105
            _culledArgsBufferA = CreateGpuWrittenIndirectArgsBuffer(); // COLD ALLOC: Raw indirect args A - GPU-written culled procedural draw args - owner: SHINOBU_105
            _culledArgsBufferB = CreateGpuWrittenIndirectArgsBuffer(); // COLD ALLOC: Raw indirect args B - GPU-written culled procedural draw args - owner: SHINOBU_105
            _writeIndex = 0;
            _activeIndex = -1;
            return IsValid(_matrixBufferA, _capacity, UnsafeUtility.SizeOf<BoidMatrixDTO>()) &&
                   IsValid(_matrixBufferB, _capacity, UnsafeUtility.SizeOf<BoidMatrixDTO>()) &&
                   IsValid(_customDataBufferA, _capacity, UnsafeUtility.SizeOf<float4>()) &&
                   IsValid(_customDataBufferB, _capacity, UnsafeUtility.SizeOf<float4>()) &&
                   IsValid(_argsBufferA, 1, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>()) &&
                   IsValid(_argsBufferB, 1, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>()) &&
                   IsValid(_visibleIndexBufferA, _capacity, UnsafeUtility.SizeOf<uint>()) &&
                   IsValid(_visibleIndexBufferB, _capacity, UnsafeUtility.SizeOf<uint>()) &&
                   IsValidByteAddress(_culledArgsBufferA, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>()) &&
                   IsValidByteAddress(_culledArgsBufferB, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>());
        }

        /// <summary>
        /// Copies Vault-produced matrices, scalar lanes, and indirect arguments into the inactive GPU buffer pair.
        /// </summary>
        public unsafe bool UploadFromVault(
            NativeArray<BoidMatrixDTO> matrices,
            NativeArray<float4> customData,
            NativeArray<BoidIndirectArgsDTO> indirectArgs)
        {
            if (!matrices.IsCreated ||
                !customData.IsCreated ||
                !indirectArgs.IsCreated ||
                indirectArgs.Length <= 0)
            {
                return false;
            }

            BoidIndirectArgsDTO args = indirectArgs[0];
            int requested = math.min((int)args.InstanceCount, math.min(matrices.Length, customData.Length));
            if (requested <= 0 || args.VertexCountPerInstance == 0u || !EnsureGraphicsResources(requested))
                return false;

            GraphicsBuffer matrixTarget = _writeIndex == 0 ? _matrixBufferA : _matrixBufferB;
            GraphicsBuffer customTarget = _writeIndex == 0 ? _customDataBufferA : _customDataBufferB;
            GraphicsBuffer argsTarget = _writeIndex == 0 ? _argsBufferA : _argsBufferB;
            GraphicsBuffer visibleTarget = _writeIndex == 0 ? _visibleIndexBufferA : _visibleIndexBufferB;
            int writeCount = math.clamp(requested, 0, math.min(_capacity, math.min(matrixTarget.count, customTarget.count)));
            if (writeCount <= 0)
                return false;

            bool matrixLocked = false;
            try
            {
                NativeArray<BoidMatrixDTO> mappedMatrices = matrixTarget.LockBufferForWrite<BoidMatrixDTO>(0, writeCount);
                matrixLocked = true;
                void* matrixDst = NativeArrayUnsafeUtility.GetUnsafePtr(mappedMatrices);
                void* matrixSrc = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(matrices);
                UnsafeUtility.MemCpy(matrixDst, matrixSrc, (long)writeCount * UnsafeUtility.SizeOf<BoidMatrixDTO>());
            }
            finally
            {
                if (matrixLocked)
                    matrixTarget.UnlockBufferAfterWrite<BoidMatrixDTO>(writeCount);
            }

            bool customLocked = false;
            try
            {
                NativeArray<float4> mappedCustom = customTarget.LockBufferForWrite<float4>(0, writeCount);
                customLocked = true;
                void* customDst = NativeArrayUnsafeUtility.GetUnsafePtr(mappedCustom);
                void* customSrc = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(customData);
                UnsafeUtility.MemCpy(customDst, customSrc, (long)writeCount * UnsafeUtility.SizeOf<float4>());
            }
            finally
            {
                if (customLocked)
                    customTarget.UnlockBufferAfterWrite<float4>(writeCount);
            }

            args.InstanceCount = (uint)writeCount;
            bool argsLocked = false;
            try
            {
                NativeArray<BoidIndirectArgsDTO> mappedArgs = argsTarget.LockBufferForWrite<BoidIndirectArgsDTO>(0, 1);
                argsLocked = true;
                mappedArgs[0] = args;
            }
            finally
            {
                if (argsLocked)
                    argsTarget.UnlockBufferAfterWrite<BoidIndirectArgsDTO>(1);
            }

            _activeIndex = _writeIndex;
            _activeCount = writeCount;
            _writeIndex ^= 1;
            PublishBuffers(matrixTarget, customTarget, visibleTarget, writeCount, false);
            return true;
        }

        /// <summary>
        /// Issues a single procedural indirect draw using the active buffer pair.
        /// </summary>
        public bool TryDraw(
            Material material,
            Bounds bounds,
            in ShinobuSwarmGpuCullingParams cullingParams,
            MeshTopology topology = MeshTopology.Triangles,
            int layer = 0,
            ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off)
        {
            if (material == null ||
                !TryGetActiveBuffers(out GraphicsBuffer matrixBuffer, out GraphicsBuffer customDataBuffer, out GraphicsBuffer argsBuffer, out int activeCount))
            {
                return false;
            }

            GraphicsBuffer visibleIndexBuffer = _activeIndex == 0 ? _visibleIndexBufferA : _visibleIndexBufferB;
            GraphicsBuffer culledArgsBuffer = _activeIndex == 0 ? _culledArgsBufferA : _culledArgsBufferB;
            bool gpuCulled = TryDispatchVisibilityCulling(
                cullingParams,
                matrixBuffer,
                visibleIndexBuffer,
                culledArgsBuffer,
                activeCount);
            int safeLayer = math.clamp(layer, 0, 31);
            PublishBuffers(matrixBuffer, customDataBuffer, visibleIndexBuffer, activeCount, gpuCulled);
            Graphics.DrawProceduralIndirect(
                material,
                bounds,
                topology,
                gpuCulled ? culledArgsBuffer : argsBuffer,
                0,
                null,
                null,
                shadowCastingMode,
                false,
                safeLayer);
            return true;
        }

        /// <summary>
        /// Resolves the active GPU buffers for external render-graph or material binding code.
        /// </summary>
        public bool TryGetActiveBuffers(
            out GraphicsBuffer matrixBuffer,
            out GraphicsBuffer customDataBuffer,
            out GraphicsBuffer argsBuffer,
            out int activeCount)
        {
            matrixBuffer = null;
            customDataBuffer = null;
            argsBuffer = null;
            activeCount = 0;
            if (_activeIndex < 0)
                return false;

            matrixBuffer = _activeIndex == 0 ? _matrixBufferA : _matrixBufferB;
            customDataBuffer = _activeIndex == 0 ? _customDataBufferA : _customDataBufferB;
            argsBuffer = _activeIndex == 0 ? _argsBufferA : _argsBufferB;
            activeCount = math.max(0, math.min(_activeCount, matrixBuffer != null ? matrixBuffer.count : 0));
            return IsValid(matrixBuffer, 1, UnsafeUtility.SizeOf<BoidMatrixDTO>()) &&
                   IsValid(customDataBuffer, 1, UnsafeUtility.SizeOf<float4>()) &&
                   IsValid(argsBuffer, 1, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>());
        }

        /// <summary>
        /// Releases all cold-owned GPU resources.
        /// </summary>
        public void Dispose()
        {
            ReleaseGraphicsResources();
        }

        private static void PublishBuffers(
            GraphicsBuffer matrixBuffer,
            GraphicsBuffer customDataBuffer,
            GraphicsBuffer visibleIndexBuffer,
            int activeCount,
            bool useVisibleIndices)
        {
            Shader.SetGlobalBuffer(_ShinobuBoidMatricesId, matrixBuffer);
            Shader.SetGlobalBuffer(_ShinobuBoidCustomDataId, customDataBuffer);
            if (visibleIndexBuffer != null)
                Shader.SetGlobalBuffer(_ShinobuBoidVisibleIndicesId, visibleIndexBuffer);
            Shader.SetGlobalInt(_ShinobuBoidActiveCountId, math.max(0, activeCount));
            Shader.SetGlobalInt(_ShinobuBoidUseVisibleIndicesId, useVisibleIndices ? 1 : 0);
        }

        private bool TryDispatchVisibilityCulling(
            in ShinobuSwarmGpuCullingParams cullingParams,
            GraphicsBuffer matrixBuffer,
            GraphicsBuffer visibleIndexBuffer,
            GraphicsBuffer culledArgsBuffer,
            int activeCount)
        {
            if (cullingParams.CullingCompute == null ||
                cullingParams.ClearArgsKernel < 0 ||
                cullingParams.CullKernel < 0 ||
                activeCount <= 0 ||
                !IsValid(matrixBuffer, 1, UnsafeUtility.SizeOf<BoidMatrixDTO>()) ||
                !IsValid(visibleIndexBuffer, 1, UnsafeUtility.SizeOf<uint>()) ||
                !IsValidByteAddress(culledArgsBuffer, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>()))
            {
                return false;
            }

            int sourceCount = math.clamp(activeCount, 0, math.min(_capacity, matrixBuffer.count));
            if (sourceCount <= 0)
                return false;

            ComputeShader compute = cullingParams.CullingCompute;
            compute.SetBuffer(cullingParams.ClearArgsKernel, _ShinobuBoidCulledIndirectArgsId, culledArgsBuffer);
            compute.Dispatch(cullingParams.ClearArgsKernel, 1, 1, 1);

            compute.SetInt(_ShinobuSourceCountId, sourceCount);
            compute.SetInt(_ShinobuDepthPyramidMipCountId, math.max(0, cullingParams.DepthPyramidMipCount));
            compute.SetInt(_ShinobuOcclusionEnabledId, cullingParams.OcclusionEnabled != 0 ? 1 : 0);
            compute.SetInt(_ShinobuDensityStepId, math.clamp(cullingParams.DensityStep, 1, 8));
            compute.SetFloat(_ShinobuOcclusionDepthBiasId, math.max(0.0001f, cullingParams.OcclusionDepthBias));
            compute.SetFloat(_ShinobuBoundsRadiusId, math.max(0.001f, cullingParams.BoundsRadius));
            compute.SetFloat(_ShinobuQualityWeightId, math.saturate(cullingParams.QualityWeight));
            compute.SetMatrix(_ShinobuViewProjectionId, cullingParams.ViewProjection);
            compute.SetMatrix(_ShinobuViewMatrixId, cullingParams.ViewMatrix);
            compute.SetVector(_ShinobuZBufferParamsId, cullingParams.ZBufferParams);
            compute.SetVector(_ShinobuDepthPyramidTexelSizeId, cullingParams.DepthPyramidTexelSize);
            compute.SetBuffer(cullingParams.CullKernel, _ShinobuBoidMatricesId, matrixBuffer);
            compute.SetBuffer(cullingParams.CullKernel, _ShinobuBoidVisibleIndicesId, visibleIndexBuffer);
            compute.SetBuffer(cullingParams.CullKernel, _ShinobuBoidCulledIndirectArgsId, culledArgsBuffer);
            compute.SetTexture(
                cullingParams.CullKernel,
                _ShinobuDepthPyramidId,
                cullingParams.DepthPyramid != null ? cullingParams.DepthPyramid : Texture2D.blackTexture);

            int groups = (sourceCount + 63) >> 6;
            compute.Dispatch(cullingParams.CullKernel, math.max(1, groups), 1, 1);
            return true;
        }

        private void ReleaseGraphicsResources()
        {
            ReleaseBuffer(ref _matrixBufferA);
            ReleaseBuffer(ref _matrixBufferB);
            ReleaseBuffer(ref _customDataBufferA);
            ReleaseBuffer(ref _customDataBufferB);
            ReleaseBuffer(ref _argsBufferA);
            ReleaseBuffer(ref _argsBufferB);
            ReleaseBuffer(ref _visibleIndexBufferA);
            ReleaseBuffer(ref _visibleIndexBufferB);
            ReleaseBuffer(ref _culledArgsBufferA);
            ReleaseBuffer(ref _culledArgsBufferB);
            _capacity = 0;
            _activeCount = 0;
            _writeIndex = 0;
            _activeIndex = -1;
        }

        private static GraphicsBuffer CreateStructuredLockBuffer<T>(int count) where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                math.max(1, count),
                UnsafeUtility.SizeOf<T>());
        }

        private static GraphicsBuffer CreateStructuredGpuBuffer<T>(int count) where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                math.max(1, count),
                UnsafeUtility.SizeOf<T>());
        }

        private static GraphicsBuffer CreateIndirectArgsBuffer()
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                UnsafeUtility.SizeOf<BoidIndirectArgsDTO>());
        }

        private static GraphicsBuffer CreateGpuWrittenIndirectArgsBuffer()
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                4,
                UnsafeUtility.SizeOf<uint>());
        }

        private static bool IsValid(GraphicsBuffer buffer, int count, int stride)
        {
            return buffer != null &&
                   buffer.IsValid() &&
                   buffer.count >= count &&
                   buffer.stride == stride;
        }

        private static bool IsValidByteAddress(GraphicsBuffer buffer, int minimumBytes)
        {
            return buffer != null &&
                   buffer.IsValid() &&
                   buffer.count * buffer.stride >= minimumBytes;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Dispose();
            buffer = null;
        }

        private static int NextPowerOfTwo(int value)
        {
            int v = math.max(1, value);
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return v;
        }
    }

    internal static class ShinobuEcosystemLayoutManifest
    {
        private static bool _verified;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            _verified = false;
        }

        internal static void VerifyColdBoot()
        {
            if (_verified)
                return;

            AssertSize<AmbientEntityDTO>(32);
            AssertSize<BoidStateDTO>(32);
            AssertSize<BoidTargetDTO>(32);
            AssertSize<BoidMatrixDTO>(64);
            AssertSize<BoidIndirectArgsDTO>(16);
            AssertSize<SwarmSpeciesProfileDTO>(32);
            AssertSize<AbyssalFlowTensorDTO>(64);
            AssertSize<AmbientEntityAupDTO>(64);
            AssertSize<EcosystemSectorDTO>(32);
            AssertSize<ShinobuEcosystemTuning>(64);
            AssertSize<ShinobuTelemetryEntry>(64);
            AssertSize<MockPredatorSignal>(64);
            AssertSize<MockPredatorRuntime>(32);
            AssertSize<MockTerrainSample>(16);
            AssertSize<MockTerrainSampler>(48);
            AssertSize<ShinobuSpatialHashDebugCell>(32);

            AssertOffset<AmbientEntityDTO>(nameof(AmbientEntityDTO.Position), 0);
            AssertOffset<AmbientEntityDTO>(nameof(AmbientEntityDTO.Velocity), 12);
            AssertOffset<AmbientEntityDTO>(nameof(AmbientEntityDTO.SpeciesHash), 24);
            AssertOffset<AmbientEntityDTO>(nameof(AmbientEntityDTO.Biomass), 28);
            AssertOffset<BoidStateDTO>(nameof(BoidStateDTO.AUP), 0);
            AssertOffset<BoidStateDTO>(nameof(BoidStateDTO.SpeciesID), 24);
            AssertOffset<BoidStateDTO>(nameof(BoidStateDTO.PackIndex), 26);
            AssertOffset<BoidStateDTO>(nameof(BoidStateDTO.Speed), 28);
            AssertOffset<BoidMatrixDTO>(nameof(BoidMatrixDTO.C0), 0);
            AssertOffset<BoidMatrixDTO>(nameof(BoidMatrixDTO.C1), 16);
            AssertOffset<BoidMatrixDTO>(nameof(BoidMatrixDTO.C2), 32);
            AssertOffset<BoidMatrixDTO>(nameof(BoidMatrixDTO.C3), 48);
            AssertOffset<BoidIndirectArgsDTO>(nameof(BoidIndirectArgsDTO.VertexCountPerInstance), 0);
            AssertOffset<BoidIndirectArgsDTO>(nameof(BoidIndirectArgsDTO.InstanceCount), 4);
            AssertOffset<BoidIndirectArgsDTO>(nameof(BoidIndirectArgsDTO.StartVertex), 8);
            AssertOffset<BoidIndirectArgsDTO>(nameof(BoidIndirectArgsDTO.StartInstance), 12);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.SectorHash), 0);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.HerbivoreMass), 4);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.CarnivoreMass), 8);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.FloraMass), 12);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.SectorX), 16);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.SectorY), 20);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.SectorZ), 24);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.Flags), 28);
            AssertOffset<ShinobuTelemetryEntry>(nameof(ShinobuTelemetryEntry.Pad0), 60);

            _verified = true;
        }

        private static void AssertSize<T>(int expected) where T : unmanaged
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed != expected)
                Fail(typeof(T).Name, expected, observed);
        }

        private static void AssertOffset<T>(string fieldName, int expected) where T : unmanaged
        {
            int observed = (int)Marshal.OffsetOf<T>(fieldName);
            if (observed != expected)
                Fail(typeof(T).Name + "." + fieldName, expected, observed);
        }

        private static void Fail(string label, int expected, int observed)
        {
            throw new CriticalBootException("[ShinobuEcosystemLayoutManifest] Layout mismatch " + label + " expected=" + expected + " observed=" + observed);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AmbientEntityDTO
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Velocity;
        [FieldOffset(24)] public uint SpeciesHash;
        [FieldOffset(28)] public float Biomass;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BoidStateDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public ushort SpeciesID;
        [FieldOffset(26)] public ushort PackIndex;
        [FieldOffset(28)] public float Speed;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BoidTargetDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public ushort SpeciesID;
        [FieldOffset(26)] public ushort Flags;
        [FieldOffset(28)] public float Weight01;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BoidMatrixDTO
    {
        [FieldOffset(0)] public float4 C0;
        [FieldOffset(16)] public float4 C1;
        [FieldOffset(32)] public float4 C2;
        [FieldOffset(48)] public float4 C3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BoidMatrixDTO FromFloat4x4(float4x4 matrix)
        {
            return new BoidMatrixDTO
            {
                C0 = matrix.c0,
                C1 = matrix.c1,
                C2 = matrix.c2,
                C3 = matrix.c3
            };
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct BoidIndirectArgsDTO
    {
        [FieldOffset(0)] public uint VertexCountPerInstance;
        [FieldOffset(4)] public uint InstanceCount;
        [FieldOffset(8)] public uint StartVertex;
        [FieldOffset(12)] public uint StartInstance;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SwarmSpeciesProfileDTO
    {
        [FieldOffset(0)] public uint BiomassHash;
        [FieldOffset(4)] public uint MeshHash;
        [FieldOffset(8)] public uint MaterialHash;
        [FieldOffset(12)] public ushort SpeciesID;
        [FieldOffset(14)] public ushort Flags;
        [FieldOffset(16)] public float Scale;
        [FieldOffset(20)] public float Speed;
        [FieldOffset(24)] public float FearResponse;
        [FieldOffset(28)] public uint Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AbyssalFlowTensorDTO
    {
        [FieldOffset(0)] public float4 AxisXAndStrength;
        [FieldOffset(16)] public float4 AxisYAndCurl;
        [FieldOffset(32)] public float4 AxisZAndTurbulence;
        [FieldOffset(48)] public float4 LocalOriginAndQuality;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AmbientEntityAupDTO
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint SectorHash;
        [FieldOffset(56)] public int SpatialCellHash;
        [FieldOffset(60)] public uint StableSeed;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct EcosystemSectorDTO
    {
        [FieldOffset(0)] public uint SectorHash;
        [FieldOffset(4)] public float HerbivoreMass;
        [FieldOffset(8)] public float CarnivoreMass;
        [FieldOffset(12)] public float FloraMass;
        [FieldOffset(16)] public int SectorX;
        [FieldOffset(20)] public int SectorY;
        [FieldOffset(24)] public int SectorZ;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ShinobuEcosystemTuning
    {
        [FieldOffset(0)] public float SeparationWeight;
        [FieldOffset(4)] public float AlignmentWeight;
        [FieldOffset(8)] public float CohesionWeight;
        [FieldOffset(12)] public float PredatorAvoidanceWeight;
        [FieldOffset(16)] public float HerbivoreBirthRate;
        [FieldOffset(20)] public float CarnivoreBirthRate;
        [FieldOffset(24)] public float HerbivoreDeathRate;
        [FieldOffset(28)] public float CarnivoreDeathRate;
        [FieldOffset(32)] public float FloraGrowthRate;
        [FieldOffset(36)] public float FeedRate;
        [FieldOffset(40)] public float BiomassReproductionThreshold;
        [FieldOffset(44)] public float MaxSpeedMetersPerSecond;
        [FieldOffset(48)] public float CarryingCapacity;
        [FieldOffset(52)] public float PredationRate;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Reserved;

        public static ShinobuEcosystemTuning CreateDefault()
        {
            return new ShinobuEcosystemTuning
            {
                SeparationWeight = 1.35f,
                AlignmentWeight = 0.45f,
                CohesionWeight = 0.38f,
                PredatorAvoidanceWeight = 8.0f,
                HerbivoreBirthRate = 0.11f,
                CarnivoreBirthRate = 0.035f,
                HerbivoreDeathRate = 0.018f,
                CarnivoreDeathRate = 0.026f,
                FloraGrowthRate = 0.22f,
                FeedRate = 0.018f,
                BiomassReproductionThreshold = 2.4f,
                MaxSpeedMetersPerSecond = 5.5f,
                CarryingCapacity = 9000f,
                PredationRate = 0.00012f,
                Flags = 0u,
                Reserved = 0u
            };
        }

        public static ShinobuEcosystemTuning Sanitize(ShinobuEcosystemTuning value)
        {
            ShinobuEcosystemTuning fallback = CreateDefault();
            value.SeparationWeight = SanitizePositive(value.SeparationWeight, fallback.SeparationWeight);
            value.AlignmentWeight = SanitizePositive(value.AlignmentWeight, fallback.AlignmentWeight);
            value.CohesionWeight = SanitizePositive(value.CohesionWeight, fallback.CohesionWeight);
            value.PredatorAvoidanceWeight = SanitizePositive(value.PredatorAvoidanceWeight, fallback.PredatorAvoidanceWeight);
            value.HerbivoreBirthRate = SanitizePositive(value.HerbivoreBirthRate, fallback.HerbivoreBirthRate);
            value.CarnivoreBirthRate = SanitizePositive(value.CarnivoreBirthRate, fallback.CarnivoreBirthRate);
            value.HerbivoreDeathRate = SanitizePositive(value.HerbivoreDeathRate, fallback.HerbivoreDeathRate);
            value.CarnivoreDeathRate = SanitizePositive(value.CarnivoreDeathRate, fallback.CarnivoreDeathRate);
            value.FloraGrowthRate = SanitizePositive(value.FloraGrowthRate, fallback.FloraGrowthRate);
            value.FeedRate = SanitizePositive(value.FeedRate, fallback.FeedRate);
            value.BiomassReproductionThreshold = SanitizePositive(value.BiomassReproductionThreshold, fallback.BiomassReproductionThreshold);
            value.MaxSpeedMetersPerSecond = SanitizePositive(value.MaxSpeedMetersPerSecond, fallback.MaxSpeedMetersPerSecond);
            value.CarryingCapacity = SanitizePositive(value.CarryingCapacity, fallback.CarryingCapacity);
            value.PredationRate = SanitizePositive(value.PredationRate, fallback.PredationRate);
            return value;
        }

        private static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ShinobuTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public int ActiveBoidCount;
        [FieldOffset(12)] public int HydratedBoidCount;
        [FieldOffset(16)] public int DehydratedSectorCount;
        [FieldOffset(20)] public int SkippedBoidCount;
        [FieldOffset(24)] public float FlockingSolveTimeMs;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public float SpatialHashTimeMs;
        [FieldOffset(40)] public float MatrixUploadTimeMs;
        [FieldOffset(44)] public int ReproducedCount;
        [FieldOffset(48)] public int TombstonedCount;
        [FieldOffset(52)] public int DebugCellCount;
        [FieldOffset(56)] public ushort CsvLoadedCount;
        [FieldOffset(58)] public ushort ProfileLoadedCount;
        [FieldOffset(60)] public uint Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ShinobuSpatialHashDebugCell
    {
        [FieldOffset(0)] public float3 CenterLocal;
        [FieldOffset(12)] public int CellHash;
        [FieldOffset(16)] public int Occupancy;
        [FieldOffset(20)] public float CellSizeMeters;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MockPredatorSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SectorHash;
        [FieldOffset(60)] public uint PredatorHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MockPredatorRuntime
    {
        [FieldOffset(0)] public float3 PositionLocal;
        [FieldOffset(12)] public float RadiusMeters;
        [FieldOffset(16)] public float Intensity01;
        [FieldOffset(20)] public uint SectorHash;
        [FieldOffset(24)] public int Valid;
        [FieldOffset(28)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MockTerrainSample
    {
        [FieldOffset(0)] public float3 Normal;
        [FieldOffset(12)] public float Distance;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public partial struct MockTerrainSampler
    {
        [FieldOffset(0)] public float3 SphereA;
        [FieldOffset(12)] public float3 SphereB;
        [FieldOffset(24)] public float3 SphereC;
        [FieldOffset(36)] public float PlaneY;
        [FieldOffset(40)] public float SphereRadius;
        [FieldOffset(44)] public float Reserved;

        public static MockTerrainSampler CreateDefault()
        {
            return new MockTerrainSampler
            {
                SphereA = new float3(24f, -6f, 10f),
                SphereB = new float3(-32f, -4f, 18f),
                SphereC = new float3(8f, -10f, -42f),
                PlaneY = -18f,
                SphereRadius = 4f,
                Reserved = 0f
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MockTerrainSample SampleSdf(float3 position)
        {
            MockTerrainSample plane = new MockTerrainSample
            {
                Distance = position.y - PlaneY,
                Normal = new float3(0f, 1f, 0f)
            };
            MockTerrainSample a = SphereSdf(position, SphereA, SphereRadius);
            MockTerrainSample b = SphereSdf(position, SphereB, SphereRadius * 1.35f);
            MockTerrainSample c = SphereSdf(position, SphereC, SphereRadius * 0.85f);
            MockTerrainSample best = plane.Distance < a.Distance ? plane : a;
            best = best.Distance < b.Distance ? best : b;
            best = best.Distance < c.Distance ? best : c;
            return best;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static MockTerrainSample SphereSdf(float3 position, float3 center, float radius)
        {
            float3 delta = position - center;
            float lenSq = math.lengthsq(delta);
            float safeLenSq = math.max(0.000001f, lenSq);
            float invLen = math.rsqrt(math.max(safeLenSq, 0.000001f));
            float len = safeLenSq * invLen;
            return new MockTerrainSample
            {
                Distance = len - radius,
                Normal = delta * invLen
            };
        }
    }

    public static class MockFloraSpawner
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SampleFloraMass(float3 position, uint speciesHash)
        {
            float wave = math.frac((position.x * 0.073f) + (position.z * 0.119f) + ((speciesHash & 255u) * 0.0039215686f));
            float triangle = 1f - math.abs((wave * 2f) - 1f);
            return math.saturate((triangle * 0.78f) + 0.05f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SampleSectorFlora(int3 sectorCoord)
        {
            uint hash = ShinobuEcosystemBalancer.ResolveSectorHash(sectorCoord);
            return 250f + ((hash & 1023u) * (650f / 1023f));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateEmergencyMockFlowJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<AbyssalFlowTensorDTO> FlowTensors;
        public float3 OriginLocal;
        public float CellSizeMeters;
        public float GlobalQualityWeight;
        public uint SectorHash;

        public void Execute(int index)
        {
            if (index >= FlowTensors.Length)
                return;

            int side = 16;
            int x = index & 15;
            int y = (index >> 4) & 15;
            int z = (index >> 8) & 15;
            float3 local = OriginLocal + ((new float3(x, y, z) - new float3(side * 0.5f)) * math.max(1f, CellSizeMeters));
            uint seed = ShinobuEcosystemBalancer.Hash32((uint)index ^ SectorHash ^ 0x4142464Cu);
            float3 flow = ShinobuEcosystemBalancer.SampleEmergencyMockFlow(local, seed, GlobalQualityWeight);
            float3 curlX = ShinobuEcosystemBalancer.SampleEmergencyMockFlow(local + new float3(CellSizeMeters, 0f, 0f), seed ^ 0x58415849u, GlobalQualityWeight) - flow;
            float3 curlY = ShinobuEcosystemBalancer.SampleEmergencyMockFlow(local + new float3(0f, CellSizeMeters, 0f), seed ^ 0x59415849u, GlobalQualityWeight) - flow;
            float3 curlZ = ShinobuEcosystemBalancer.SampleEmergencyMockFlow(local + new float3(0f, 0f, CellSizeMeters), seed ^ 0x5A415849u, GlobalQualityWeight) - flow;
            FlowTensors[index] = new AbyssalFlowTensorDTO
            {
                AxisXAndStrength = new float4(flow, ShinobuEcosystemBalancer.SafeLength(flow)),
                AxisYAndCurl = new float4(curlY, ShinobuEcosystemBalancer.SafeLength(curlX)),
                AxisZAndTurbulence = new float4(curlZ, ShinobuEcosystemBalancer.SafeLength(curlY + curlZ)),
                LocalOriginAndQuality = new float4(local, math.saturate(GlobalQualityWeight))
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct WriteBoidIndirectArgsJob : IJob
    {
        [NoAlias] public NativeArray<BoidIndirectArgsDTO> IndirectArgs;
        public uint VertexCountPerInstance;
        public uint StartVertex;
        public uint StartInstance;
        public uint ActiveBoidCount;

        public void Execute()
        {
            if (!IndirectArgs.IsCreated || IndirectArgs.Length <= 0)
                return;

            IndirectArgs[0] = new BoidIndirectArgsDTO
            {
                VertexCountPerInstance = VertexCountPerInstance,
                InstanceCount = ActiveBoidCount,
                StartVertex = StartVertex,
                StartInstance = StartInstance
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct LocalShiftAndSpatialHashJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<AmbientEntityDTO> Entities;
        [NoAlias] public NativeArray<AmbientEntityAupDTO> Aups;
        [NoAlias] public NativeArray<BoidStateDTO> BoidStates;
        [NoAlias] public NativeArray<AmbientEntityDTO> EntitySnapshot;
        [NoAlias] public NativeArray<AmbientEntityAupDTO> AupSnapshot;
        [NoAlias] public NativeArray<BoidStateDTO> BoidStateSnapshot;
        public AbsoluteUniversePosition CenterAup;
        public float CellSizeMeters;
        public float SectorSizeMeters;
        public float SystemStress01;
        public float GlobalQualityWeight;
        public int UpdateStride;
        public uint Frame;
        public int Count;

        public unsafe void Execute(int index)
        {
            if (index >= Count)
                return;

            AmbientEntityDTO* entities = (AmbientEntityDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Entities);
            AmbientEntityAupDTO* aups = (AmbientEntityAupDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Aups);
            BoidStateDTO* boidStates = (BoidStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(BoidStates);
            AmbientEntityDTO* entitySnapshots = (AmbientEntityDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(EntitySnapshot);
            AmbientEntityAupDTO* aupSnapshots = (AmbientEntityAupDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(AupSnapshot);
            BoidStateDTO* boidStateSnapshots = (BoidStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(BoidStateSnapshot);
            ref AmbientEntityDTO entity = ref UnsafeUtility.AsRef<AmbientEntityDTO>(entities + index);
            ref AmbientEntityAupDTO meta = ref UnsafeUtility.AsRef<AmbientEntityAupDTO>(aups + index);
            ref BoidStateDTO boidState = ref UnsafeUtility.AsRef<BoidStateDTO>(boidStates + index);
            ref AmbientEntityDTO entitySnapshot = ref UnsafeUtility.AsRef<AmbientEntityDTO>(entitySnapshots + index);
            ref AmbientEntityAupDTO aupSnapshot = ref UnsafeUtility.AsRef<AmbientEntityAupDTO>(aupSnapshots + index);
            ref BoidStateDTO boidStateSnapshot = ref UnsafeUtility.AsRef<BoidStateDTO>(boidStateSnapshots + index);
            uint flags = meta.Flags;
            if ((flags & ShinobuEcosystemBalancer.EntityFlagActive) == 0u ||
                (flags & ShinobuEcosystemBalancer.EntityFlagHydrated) == 0u)
            {
                meta.Flags = flags | ShinobuEcosystemBalancer.EntityFlagFree;
                boidState = default;
                entitySnapshot = default;
                aupSnapshot = meta;
                boidStateSnapshot = default;
                return;
            }

            flags &= ~ShinobuEcosystemBalancer.EntityFlagSkipUpdate;
            float3 local = ShinobuEcosystemBalancer.AupToLocal(in meta.PositionAup, in CenterAup);
            if (!math.all(math.isfinite(local)))
            {
                meta.Flags = flags | ShinobuEcosystemBalancer.EntityFlagInvalidMath;
                boidState = ShinobuEcosystemBalancer.BuildBoidState(in meta.PositionAup, entity.SpeciesHash, index, ShinobuEcosystemBalancer.SafeLength(entity.Velocity));
                entitySnapshot = entity;
                aupSnapshot = meta;
                boidStateSnapshot = boidState;
                return;
            }

            int3 sectorCoord = ShinobuEcosystemBalancer.ResolveSectorCoord(in meta.PositionAup, SectorSizeMeters);
            int cellHash = ShinobuEcosystemBalancer.ResolveSpatialCellHash(local, CellSizeMeters);
            int stride = math.max(1, UpdateStride);
            uint laneHash = ShinobuEcosystemBalancer.Hash32((uint)index ^ meta.StableSeed ^ Frame);
            if (stride > 1 && (laneHash % (uint)stride) != 0u)
                flags |= ShinobuEcosystemBalancer.EntityFlagSkipUpdate;

            entity.Position = local;
            meta.SectorHash = ShinobuEcosystemBalancer.ResolveSectorHash(sectorCoord);
            meta.SpatialCellHash = cellHash;
            meta.Flags = flags;
            boidState = ShinobuEcosystemBalancer.BuildBoidState(in meta.PositionAup, entity.SpeciesHash, index, ShinobuEcosystemBalancer.SafeLength(entity.Velocity));
            entitySnapshot = entity;
            aupSnapshot = meta;
            boidStateSnapshot = boidState;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildBoidSpatialHashJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<AmbientEntityAupDTO> AupSnapshot;
        [NoAlias] public NativeArray<int> BucketHeads;
        [NoAlias] public NativeArray<int> BucketNext;
        [NoAlias] public NativeArray<int> Counters;
        public int MaxChainSteps;
        public int Count;

        public void Execute()
        {
            for (int i = 0; i < BucketHeads.Length; i++)
                BucketHeads[i] = -1;

            int maxChainSteps = math.max(1, MaxChainSteps);
            int overflow = 0;
            for (int i = 0; i < Count; i++)
            {
                BucketNext[i] = -1;
                AmbientEntityAupDTO meta = AupSnapshot[i];
                uint flags = meta.Flags;
                if ((flags & ShinobuEcosystemBalancer.EntityFlagActive) == 0u ||
                    (flags & ShinobuEcosystemBalancer.EntityFlagHydrated) == 0u ||
                    (flags & ShinobuEcosystemBalancer.EntityFlagSkipUpdate) != 0u ||
                    (flags & ShinobuEcosystemBalancer.EntityFlagInvalidMath) != 0u)
                {
                    continue;
                }

                int bucket = ShinobuEcosystemBalancer.ResolveSpatialBucketIndex(meta.SpatialCellHash);
                int chainLength = 0;
                int cursor = BucketHeads[bucket];
                while (cursor >= 0 && cursor < Count && chainLength < maxChainSteps)
                {
                    chainLength++;
                    cursor = BucketNext[cursor];
                }

                if (chainLength >= maxChainSteps)
                {
                    overflow++;
                    continue;
                }

                BucketNext[i] = BucketHeads[bucket];
                BucketHeads[bucket] = i;
            }

            if (overflow != 0 && Counters.IsCreated && Counters.Length > 7)
                Counters[7] += overflow;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct BoidFlockingJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<AmbientEntityDTO> Entities;
        [NoAlias] public NativeArray<AmbientEntityAupDTO> Aups;
        [NoAlias] public NativeArray<BoidStateDTO> BoidStates;
        [ReadOnly, NoAlias] public NativeArray<AmbientEntityDTO> EntitySnapshot;
        [ReadOnly, NoAlias] public NativeArray<AmbientEntityAupDTO> AupSnapshot;
        [ReadOnly, NoAlias] public NativeArray<BoidStateDTO> BoidStateSnapshot;
        [ReadOnly, NoAlias] public NativeArray<int> BucketHeads;
        [ReadOnly, NoAlias] public NativeArray<int> BucketNext;
        public AbsoluteUniversePosition CenterAup;
        public double3 CenterAbsolute;
        public float3 CameraForward;
        public MockTerrainSampler TerrainSampler;
        public MockPredatorRuntime Predator;
        public ShinobuEcosystemTuning Tuning;
        public float DeltaSeconds;
        public float GlobalQualityWeight;
        public float CellSizeMeters;
        public float SectorSizeMeters;
        public float NeighborRadiusMeters;
        public float ObstacleProbeMeters;
        public int MaxNeighborSamplesPerBoid;
        public int MaxSpatialHashChainSteps;
        public int Count;

        public unsafe void Execute(int index)
        {
            if (index >= Count)
                return;

            AmbientEntityDTO* entities = (AmbientEntityDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Entities);
            AmbientEntityAupDTO* aups = (AmbientEntityAupDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Aups);
            BoidStateDTO* boidStates = (BoidStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(BoidStates);
            AmbientEntityDTO* entitySnapshots = (AmbientEntityDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(EntitySnapshot);
            AmbientEntityAupDTO* aupSnapshots = (AmbientEntityAupDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(AupSnapshot);
            BoidStateDTO* boidStateSnapshots = (BoidStateDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(BoidStateSnapshot);
            int* bucketHeads = (int*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(BucketHeads);
            int* bucketNext = (int*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(BucketNext);
            ref AmbientEntityDTO entityOut = ref UnsafeUtility.AsRef<AmbientEntityDTO>(entities + index);
            ref AmbientEntityAupDTO metaOut = ref UnsafeUtility.AsRef<AmbientEntityAupDTO>(aups + index);
            ref BoidStateDTO boidStateOut = ref UnsafeUtility.AsRef<BoidStateDTO>(boidStates + index);
            AmbientEntityAupDTO meta = UnsafeUtility.AsRef<AmbientEntityAupDTO>(aupSnapshots + index);
            uint flags = meta.Flags;
            if ((flags & ShinobuEcosystemBalancer.EntityFlagActive) == 0u ||
                (flags & ShinobuEcosystemBalancer.EntityFlagHydrated) == 0u ||
                (flags & ShinobuEcosystemBalancer.EntityFlagSkipUpdate) != 0u)
            {
                return;
            }

            AmbientEntityDTO entity = UnsafeUtility.AsRef<AmbientEntityDTO>(entitySnapshots + index);
            BoidStateDTO boidState = UnsafeUtility.AsRef<BoidStateDTO>(boidStateSnapshots + index);
            double3 localDelta = boidState.AUP - CenterAbsolute;
            float3 position = math.select(entity.Position, (float3)localDelta, math.all(math.isfinite(localDelta)));
            float3 velocity = entity.Velocity;
            bool invalid = !math.all(math.isfinite(position)) || !math.all(math.isfinite(velocity));
            if (invalid)
            {
                meta.Flags = flags | ShinobuEcosystemBalancer.EntityFlagInvalidMath;
                boidStateOut = boidState;
                metaOut = meta;
                return;
            }

            float3 forward = ShinobuEcosystemBalancer.SafeNormalize(velocity, CameraForward);
            float cameraDot = math.dot(ShinobuEcosystemBalancer.SafeNormalize(position, CameraForward), CameraForward);
            float visibleConeThreshold = math.lerp(0.62f, -0.15f, ShinobuEcosystemBalancer.Smooth01(GlobalQualityWeight));
            bool visibleCone = cameraDot > visibleConeThreshold;
            float3 acceleration = new float3(0f);
            float neighborSolve01 = ShinobuEcosystemBalancer.ResolveNeighborSolveWeight(GlobalQualityWeight);
            if (visibleCone && neighborSolve01 > 0.0001f)
            {
                QueryNeighbors(index, position, velocity, neighborSolve01, ref acceleration, entitySnapshots, aupSnapshots, bucketHeads, bucketNext);
            }

            float3 emergencyFlow = ShinobuEcosystemBalancer.SampleEmergencyMockFlow(position, meta.StableSeed, GlobalQualityWeight);
            acceleration += emergencyFlow * math.lerp(0.35f, 1.75f, ShinobuEcosystemBalancer.Smooth01(GlobalQualityWeight));

            if (Predator.Valid != 0)
            {
                float3 predatorDelta = position - Predator.PositionLocal;
                float distSq = math.lengthsq(predatorDelta);
                float radius = math.max(1f, Predator.RadiusMeters);
                float radiusSq = math.max(1f, radius * radius);
                if (meta.SectorHash == Predator.SectorHash || distSq < radiusSq)
                {
                    float3 away = ShinobuEcosystemBalancer.SafeNormalize(predatorDelta, -forward);
                    float proximity = math.saturate(1f - (distSq * math.rcp(radiusSq)));
                    acceleration += away * (Tuning.PredatorAvoidanceWeight * math.max(0.25f, Predator.Intensity01) * (1f + proximity));
                }
            }

            MockTerrainSample terrain = TerrainSampler.SampleSdf(position);
            if (terrain.Distance < ObstacleProbeMeters)
            {
                float push = math.saturate((ObstacleProbeMeters - terrain.Distance) / math.max(0.001f, ObstacleProbeMeters));
                float3 wallNormal = ShinobuEcosystemBalancer.SafeNormalize(terrain.Normal, new float3(0f, 1f, 0f));
                float3 vortexSwirl = ShinobuEcosystemBalancer.SafeNormalize(math.cross(new float3(0f, 1f, 0f), wallNormal), forward);
                float swirlWeight = math.lerp(0.35f, 1f, ShinobuEcosystemBalancer.Smooth01(GlobalQualityWeight));
                acceleration += (wallNormal * (push * 2.25f)) + (vortexSwirl * (push * math.lerp(2.5f, 8.5f, swirlWeight)));
            }

            entity.Biomass = math.max(0.05f, entity.Biomass + (MockFloraSpawner.SampleFloraMass(position, entity.SpeciesHash) * Tuning.FeedRate * DeltaSeconds));
            velocity += acceleration * DeltaSeconds;
            float maxSpeed = math.max(0.5f, Tuning.MaxSpeedMetersPerSecond);
            float rawSpeedSq = math.lengthsq(velocity);
            float speedSq = math.select(0f, rawSpeedSq, math.isfinite(rawSpeedSq));
            float maxSpeedSq = maxSpeed * maxSpeed;
            bool hasPositiveSpeed = speedSq > 0.00000001f;
            bool overMaxSpeed = speedSq > maxSpeedSq;
            float currentSpeed = speedSq * math.rsqrt(math.max(0.00000001f, speedSq));
            float3 clampedVelocity = velocity * (maxSpeed * math.rsqrt(math.max(0.0001f, speedSq)));
            float3 positiveVelocity = math.select(velocity, clampedVelocity, overMaxSpeed);
            float positiveSpeed = math.select(currentSpeed, maxSpeed, overMaxSpeed);
            bool positiveFinite = hasPositiveSpeed & math.all(math.isfinite(positiveVelocity));
            velocity = math.select(forward * maxSpeed, positiveVelocity, positiveFinite);
            float finalSpeed = math.select(maxSpeed, positiveSpeed, positiveFinite);

            position += velocity * DeltaSeconds;
            if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(velocity)))
            {
                meta.Flags = flags | ShinobuEcosystemBalancer.EntityFlagInvalidMath;
                boidStateOut = boidState;
                metaOut = meta;
                return;
            }

            entity.Position = position;
            entity.Velocity = velocity;
            meta.PositionAup = ShinobuEcosystemBalancer.OffsetAup(in CenterAup, position);
            meta.SpatialCellHash = ShinobuEcosystemBalancer.ResolveSpatialCellHash(position, CellSizeMeters);
            meta.SectorHash = ShinobuEcosystemBalancer.ResolveSectorHash(ShinobuEcosystemBalancer.ResolveSectorCoord(in meta.PositionAup, SectorSizeMeters));
            boidState.AUP = ShinobuEcosystemBalancer.ToAbsoluteDouble3(in meta.PositionAup);
            boidState.SpeciesID = ShinobuEcosystemBalancer.ResolveSpeciesId(entity.SpeciesHash);
            boidState.PackIndex = (ushort)(index & 0xFFFF);
            boidState.Speed = finalSpeed;
            entityOut = entity;
            metaOut = meta;
            boidStateOut = boidState;
        }

        private unsafe void QueryNeighbors(
            int index,
            float3 position,
            float3 velocity,
            float neighborSolve01,
            ref float3 acceleration,
            AmbientEntityDTO* entitySnapshots,
            AmbientEntityAupDTO* aupSnapshots,
            int* bucketHeads,
            int* bucketNext)
        {
            int3 baseCell = ShinobuEcosystemBalancer.ResolveSpatialCell(position, CellSizeMeters);
            float radiusSq = NeighborRadiusMeters * NeighborRadiusMeters;
            float3 separation = new float3(0f);
            float3 alignment = new float3(0f);
            float3 cohesion = new float3(0f);
            int neighborCount = 0;
            int sampleCount = 0;
            int maxChainSteps = math.max(1, MaxSpatialHashChainSteps);
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        int hash = ShinobuEcosystemBalancer.ResolveSpatialCellHash(baseCell + new int3(x, y, z));
                        int bucket = ShinobuEcosystemBalancer.ResolveSpatialBucketIndex(hash);
                        int otherIndex = bucketHeads[bucket];
                        int guard = 0;
                        while (otherIndex >= 0 && otherIndex < Count && guard < maxChainSteps)
                        {
                            guard++;
                            int nextIndex = bucketNext[otherIndex];
                            if (otherIndex != index)
                            {
                                AmbientEntityAupDTO otherMeta = UnsafeUtility.AsRef<AmbientEntityAupDTO>(aupSnapshots + otherIndex);
                                uint otherFlags = otherMeta.Flags;
                                if (otherMeta.SpatialCellHash == hash &&
                                    (otherFlags & ShinobuEcosystemBalancer.EntityFlagActive) != 0u &&
                                    (otherFlags & ShinobuEcosystemBalancer.EntityFlagHydrated) != 0u)
                                {
                                    AmbientEntityDTO other = UnsafeUtility.AsRef<AmbientEntityDTO>(entitySnapshots + otherIndex);
                                    float3 delta = position - other.Position;
                                    float distSq = math.lengthsq(delta);
                                    if (distSq > 0.0001f && distSq <= radiusSq)
                                    {
                                        float invDist = math.rsqrt(math.max(0.0001f, distSq));
                                        separation += delta * invDist * invDist;
                                        alignment += other.Velocity;
                                        cohesion += other.Position;
                                        neighborCount++;
                                        sampleCount++;
                                        if (sampleCount >= MaxNeighborSamplesPerBoid)
                                            break;
                                    }
                                }
                            }

                            otherIndex = nextIndex;
                        }

                        if (sampleCount >= MaxNeighborSamplesPerBoid)
                            break;
                    }

                    if (sampleCount >= MaxNeighborSamplesPerBoid)
                        break;
                }

                if (sampleCount >= MaxNeighborSamplesPerBoid)
                    break;
            }

            if (neighborCount <= 0)
                return;

            float inv = math.rcp(math.max(1, neighborCount));
            float3 desiredAlignment = ShinobuEcosystemBalancer.SafeNormalize(alignment * inv, velocity);
            float3 center = cohesion * inv;
            float3 desiredCohesion = ShinobuEcosystemBalancer.SafeNormalize(center - position, new float3(0f));
            float reynolds01 = ShinobuEcosystemBalancer.Smooth01(math.saturate((GlobalQualityWeight - 0.14f) * 1.1627907f));
            acceleration += separation * (Tuning.SeparationWeight * neighborSolve01);
            acceleration += desiredAlignment * (Tuning.AlignmentWeight * reynolds01 * neighborSolve01);
            acceleration += desiredCohesion * (Tuning.CohesionWeight * reynolds01 * neighborSolve01);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildShinobuRenderPayloadJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<AmbientEntityDTO> Entities;
        [ReadOnly, NoAlias] public NativeArray<AmbientEntityAupDTO> Aups;
        [ReadOnly, NoAlias] public NativeArray<BoidStateDTO> BoidStates;
        [NoAlias] public NativeArray<BoidMatrixDTO> Matrices;
        [NoAlias] public NativeArray<float4> CustomData;
        public double3 CenterAbsolute;
        public float GlobalQualityWeight;
        public int Count;

        public unsafe void Execute(int index)
        {
            if (index >= Count)
                return;

            AmbientEntityDTO* entities = (AmbientEntityDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Entities);
            AmbientEntityAupDTO* aups = (AmbientEntityAupDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Aups);
            BoidStateDTO* boidStates = (BoidStateDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(BoidStates);
            BoidMatrixDTO* matrices = (BoidMatrixDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Matrices);
            float4* customData = (float4*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(CustomData);
            ref AmbientEntityDTO entity = ref UnsafeUtility.AsRef<AmbientEntityDTO>(entities + index);
            ref AmbientEntityAupDTO meta = ref UnsafeUtility.AsRef<AmbientEntityAupDTO>(aups + index);
            ref BoidStateDTO boidState = ref UnsafeUtility.AsRef<BoidStateDTO>(boidStates + index);
            ref BoidMatrixDTO matrixOut = ref UnsafeUtility.AsRef<BoidMatrixDTO>(matrices + index);
            ref float4 customOut = ref UnsafeUtility.AsRef<float4>(customData + index);
            uint flags = meta.Flags;
            double3 localDelta = boidState.AUP - CenterAbsolute;
            float3 position = math.all(math.isfinite(localDelta)) ? (float3)localDelta : entity.Position;
            if ((flags & ShinobuEcosystemBalancer.EntityFlagActive) == 0u ||
                (flags & ShinobuEcosystemBalancer.EntityFlagHydrated) == 0u ||
                (flags & ShinobuEcosystemBalancer.EntityFlagInvalidMath) != 0u ||
                !math.all(math.isfinite(position)) ||
                !math.all(math.isfinite(entity.Velocity)) ||
                !math.isfinite(entity.Biomass))
            {
                matrixOut = default;
                customOut = default;
                return;
            }

            float3 forward = ShinobuEcosystemBalancer.SafeNormalize(entity.Velocity, new float3(0f, 0f, 1f));
            float scale = math.clamp(0.18f + entity.Biomass * 0.06f, 0.12f, 0.6f);
            float4x4 matrix = float4x4.TRS(position, quaternion.LookRotationSafe(forward, new float3(0f, 1f, 0f)), new float3(scale));
            if (!math.all(math.isfinite(matrix.c0)) ||
                !math.all(math.isfinite(matrix.c1)) ||
                !math.all(math.isfinite(matrix.c2)) ||
                !math.all(math.isfinite(matrix.c3)))
            {
                matrixOut = default;
                customOut = default;
                return;
            }

            matrixOut = BoidMatrixDTO.FromFloat4x4(matrix);
            float speciesLane = entity.SpeciesHash == 0x4341524Eu ? 1f : 0f;
            customOut = new float4(speciesLane, entity.Biomass, (flags & ShinobuEcosystemBalancer.EntityFlagSkipUpdate) != 0u ? 1f : 0f, GlobalQualityWeight);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildHashDebugCellsJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<AmbientEntityDTO> Entities;
        [ReadOnly, NoAlias] public NativeArray<AmbientEntityAupDTO> Aups;
        [ReadOnly, NoAlias] public NativeArray<int> BucketHeads;
        [ReadOnly, NoAlias] public NativeArray<int> BucketNext;
        [NoAlias] public NativeArray<ShinobuSpatialHashDebugCell> DebugCells;
        [NoAlias] public NativeArray<int> Counters;
        public float CellSizeMeters;
        public int Count;
        public int Capacity;
        public int MaxChainSteps;

        public void Execute()
        {
            int safeCapacity = math.min(Capacity, DebugCells.Length);
            for (int i = 0; i < safeCapacity; i++)
                DebugCells[i] = default;

            int debugCount = 0;
            int maxChainSteps = math.max(1, MaxChainSteps);
            int safeCount = math.min(Count, math.min(Entities.Length, math.min(Aups.Length, BucketNext.Length)));
            for (int bucket = 0; bucket < BucketHeads.Length && debugCount < safeCapacity; bucket++)
            {
                int entityIndex = BucketHeads[bucket];
                if (entityIndex < 0 || entityIndex >= safeCount)
                    continue;

                int occupancy = 0;
                int hash = 0;
                float3 firstPosition = default;
                int guard = 0;
                while (entityIndex >= 0 && entityIndex < safeCount && guard < maxChainSteps)
                {
                    guard++;
                    AmbientEntityAupDTO meta = Aups[entityIndex];
                    uint flags = meta.Flags;
                    if ((flags & ShinobuEcosystemBalancer.EntityFlagActive) != 0u &&
                        (flags & ShinobuEcosystemBalancer.EntityFlagHydrated) != 0u &&
                        (flags & ShinobuEcosystemBalancer.EntityFlagInvalidMath) == 0u)
                    {
                        if (occupancy == 0)
                        {
                            hash = meta.SpatialCellHash;
                            firstPosition = Entities[entityIndex].Position;
                        }

                        occupancy++;
                    }

                    entityIndex = BucketNext[entityIndex];
                }

                if (occupancy <= 0)
                    continue;

                int3 cell = ShinobuEcosystemBalancer.ResolveSpatialCell(firstPosition, CellSizeMeters);
                DebugCells[debugCount++] = new ShinobuSpatialHashDebugCell
                {
                    CenterLocal = ((float3)cell + new float3(0.5f)) * CellSizeMeters,
                    CellHash = hash,
                    Occupancy = occupancy,
                    CellSizeMeters = CellSizeMeters,
                    Flags = 1u
                };
            }

            if (Counters.IsCreated && Counters.Length > 8)
                Counters[8] = debugCount;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct CountTelemetryCountersJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<AmbientEntityAupDTO> Aups;
        [ReadOnly, NoAlias] public NativeArray<EcosystemSectorDTO> Sectors;
        [NoAlias] public NativeArray<int> Counters;
        public int Count;
        public int SectorCount;

        public void Execute()
        {
            int active = 0;
            int hydrated = 0;
            int free = 0;
            int skipped = 0;
            int invalid = 0;
            for (int i = 0; i < Count; i++)
            {
                uint flags = Aups[i].Flags;
                active += math.select(0, 1, (flags & ShinobuEcosystemBalancer.EntityFlagActive) != 0u);
                hydrated += math.select(0, 1, (flags & ShinobuEcosystemBalancer.EntityFlagHydrated) != 0u);
                free += math.select(0, 1, (flags & ShinobuEcosystemBalancer.EntityFlagFree) != 0u);
                skipped += math.select(0, 1, (flags & ShinobuEcosystemBalancer.EntityFlagSkipUpdate) != 0u);
                invalid += math.select(0, 1, (flags & ShinobuEcosystemBalancer.EntityFlagInvalidMath) != 0u);
            }

            int dehydratedSectors = 0;
            for (int i = 0; i < SectorCount; i++)
                dehydratedSectors += math.select(0, 1, (Sectors[i].Flags & ShinobuEcosystemBalancer.SectorFlagDehydrated) != 0u);

            if (Counters.Length > 1) Counters[1] = active;
            if (Counters.Length > 2) Counters[2] = hydrated;
            if (Counters.Length > 3) Counters[3] = free;
            if (Counters.Length > 4) Counters[4] = dehydratedSectors;
            if (Counters.Length > 5) Counters[5] = skipped;
            if (Counters.Length > 6) Counters[6] = invalid;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct LotkaVolterraMacroJob : IJob
    {
        [NoAlias] public NativeArray<AmbientEntityDTO> Entities;
        [NoAlias] public NativeArray<AmbientEntityAupDTO> Aups;
        [NoAlias] public NativeArray<EcosystemSectorDTO> Sectors;
        [NoAlias] public NativeArray<int> SectorBucketHeads;
        [NoAlias] public NativeArray<int> SectorEntityLinks;
        [NoAlias] public NativeArray<int> Counters;
        public AbsoluteUniversePosition CenterAup;
        public ShinobuEcosystemTuning Tuning;
        public float GlobalQualityWeight;
        public int EntityCount;
        public int SectorCount;
        public float SectorSizeMeters;
        public float DehydrationDistanceSq;
        public float RehydrationDistanceSq;
        public int ApplyLotka;
        public uint Frame;

        public void Execute()
        {
            EntityCount = math.max(0, math.min(EntityCount, math.min(Entities.Length, Aups.Length)));
            SectorCount = math.max(0, math.min(SectorCount, Sectors.Length));
            int sectorHeadBase = SectorBucketHeads.Length - SectorCount;
            int hashBucketCount = sectorHeadBase;
            int linkCount = SectorCount + EntityCount;
            if (SectorCount <= 0 ||
                EntityCount <= 0 ||
                hashBucketCount <= 0 ||
                linkCount > SectorEntityLinks.Length)
            {
                if (Counters.IsCreated && Counters.Length > 7)
                    Counters[7]++;
                return;
            }

            for (int i = 0; i < SectorBucketHeads.Length; i++)
                SectorBucketHeads[i] = -1;
            for (int i = 0; i < linkCount; i++)
                SectorEntityLinks[i] = -1;

            for (int i = 0; i < SectorCount; i++)
            {
                EcosystemSectorDTO sector = Sectors[i];
                if ((sector.Flags & ShinobuEcosystemBalancer.SectorFlagValid) == 0u)
                    continue;
                if ((sector.Flags & ShinobuEcosystemBalancer.SectorFlagDehydrated) == 0u)
                {
                    sector.HerbivoreMass = 0f;
                    sector.CarnivoreMass = 0f;
                    sector.FloraMass = MockFloraSpawner.SampleSectorFlora(new int3(sector.SectorX, sector.SectorY, sector.SectorZ));
                }
                Sectors[i] = sector;
                InsertSectorSlotIntoHash(i, in sector, hashBucketCount);
            }

            int freeCursor = 0;
            int sectorCursor = 0;
            int reproduced = 0;
            int tombstoned = 0;
            for (int i = 0; i < EntityCount; i++)
            {
                AmbientEntityAupDTO meta = Aups[i];
                if ((meta.Flags & ShinobuEcosystemBalancer.EntityFlagActive) == 0u ||
                    (meta.Flags & ShinobuEcosystemBalancer.EntityFlagHydrated) == 0u)
                {
                    continue;
                }

                AmbientEntityDTO entity = Entities[i];
                int3 sectorCoord = ShinobuEcosystemBalancer.ResolveSectorCoord(in meta.PositionAup, SectorSizeMeters);
                int sectorSlot = EnsureSectorSlot(sectorCoord, ref sectorCursor, hashBucketCount);
                if (sectorSlot >= 0)
                {
                    EcosystemSectorDTO sector = Sectors[sectorSlot];
                    if ((meta.Flags & ShinobuEcosystemBalancer.EntityFlagCarnivore) != 0u)
                        sector.CarnivoreMass += math.max(0f, entity.Biomass);
                    else
                        sector.HerbivoreMass += math.max(0f, entity.Biomass);
                    Sectors[sectorSlot] = sector;
                }

                float3 localToPlayer = ShinobuEcosystemBalancer.AupToLocal(in meta.PositionAup, in CenterAup);
                float distSq = math.lengthsq(localToPlayer);
                if (distSq > DehydrationDistanceSq)
                {
                    meta.Flags = (meta.Flags & ~(ShinobuEcosystemBalancer.EntityFlagActive | ShinobuEcosystemBalancer.EntityFlagHydrated | ShinobuEcosystemBalancer.EntityFlagSkipUpdate)) |
                                 ShinobuEcosystemBalancer.EntityFlagFree;
                    Aups[i] = meta;
                    tombstoned++;
                    if (sectorSlot >= 0)
                    {
                        EcosystemSectorDTO sector = Sectors[sectorSlot];
                        sector.Flags |= ShinobuEcosystemBalancer.SectorFlagDehydrated;
                        Sectors[sectorSlot] = sector;
                    }
                    continue;
                }

                if ((meta.Flags & ShinobuEcosystemBalancer.EntityFlagCarnivore) != 0u)
                    AppendCarnivoreToSectorChain(sectorSlot, i, sectorHeadBase);

                if (entity.Biomass > Tuning.BiomassReproductionThreshold)
                {
                    int freeSlot = FindNextFreeSlot(ref freeCursor);
                    if (freeSlot >= 0)
                    {
                        uint seed = NextLcg(meta.StableSeed ^ Frame);
                        float3 jitter = ResolveJitter(seed) * 3f;
                        AmbientEntityDTO child = entity;
                        child.Position += jitter;
                        child.Biomass = math.max(0.2f, entity.Biomass * 0.45f);
                        child.Velocity = ShinobuEcosystemBalancer.SafeNormalize(entity.Velocity + jitter, new float3(0f, 0f, 1f)) * math.max(0.5f, Tuning.MaxSpeedMetersPerSecond * 0.75f);
                        entity.Biomass *= 0.55f;
                        meta.StableSeed = seed;
                        Entities[i] = entity;
                        Aups[i] = meta;

                        AbsoluteUniversePosition childAup = ShinobuEcosystemBalancer.OffsetAup(in meta.PositionAup, jitter);
                        int3 childSectorCoord = ShinobuEcosystemBalancer.ResolveSectorCoord(in childAup, SectorSizeMeters);
                        Entities[freeSlot] = child;
                        Aups[freeSlot] = new AmbientEntityAupDTO
                        {
                            PositionAup = childAup,
                            Flags = (meta.Flags | ShinobuEcosystemBalancer.EntityFlagActive | ShinobuEcosystemBalancer.EntityFlagHydrated) &
                                    ~ShinobuEcosystemBalancer.EntityFlagFree,
                            SectorHash = ShinobuEcosystemBalancer.ResolveSectorHash(childSectorCoord),
                            SpatialCellHash = 0,
                            StableSeed = seed
                        };
                        reproduced++;
                    }
                }
            }

            if (ApplyLotka != 0)
                ApplyLotkaVolterra(ref tombstoned, sectorHeadBase);

            RehydrateNearSectors(ref freeCursor, ref reproduced);

            if (Counters.IsCreated)
            {
                if (Counters.Length > 9) Counters[9] += reproduced;
                if (Counters.Length > 10) Counters[10] += tombstoned;
            }
        }

        private void ApplyLotkaVolterra(ref int tombstoned, int sectorHeadBase)
        {
            for (int i = 0; i < SectorCount; i++)
            {
                EcosystemSectorDTO sector = Sectors[i];
                if ((sector.Flags & ShinobuEcosystemBalancer.SectorFlagValid) == 0u)
                    continue;

                float oldCarnivore = math.max(0f, sector.CarnivoreMass);
                float flora = math.max(0f, sector.FloraMass);
                float herb = math.max(0f, sector.HerbivoreMass);
                float carn = oldCarnivore;
                float carrying = math.max(1f, Tuning.CarryingCapacity);
                float dFlora = (Tuning.FloraGrowthRate * flora * (1f - flora / carrying)) - (Tuning.FeedRate * herb * flora * 0.001f);
                float dHerb = (Tuning.HerbivoreBirthRate * herb * flora / (flora + 1f)) -
                              (Tuning.PredationRate * herb * carn) -
                              (Tuning.HerbivoreDeathRate * herb);
                float dCarn = (Tuning.CarnivoreBirthRate * Tuning.PredationRate * herb * carn) -
                              (Tuning.CarnivoreDeathRate * carn);
                sector.FloraMass = math.clamp(flora + dFlora, 0f, carrying);
                sector.HerbivoreMass = math.max(0f, herb + dHerb);
                sector.CarnivoreMass = math.max(0f, carn + dCarn);
                Sectors[i] = sector;

                float starvationMass = math.max(0f, oldCarnivore - sector.CarnivoreMass);
                if (starvationMass > 0.5f)
                    TombstoneCarnivoresInSector(i, (int)math.ceil(starvationMass), ref tombstoned, sectorHeadBase);
            }
        }

        private void TombstoneCarnivoresInSector(int sectorSlot, int killBudget, ref int tombstoned, int sectorHeadBase)
        {
            if (sectorSlot < 0 || sectorSlot >= SectorCount)
                return;

            int headIndex = sectorHeadBase + sectorSlot;
            if (headIndex < 0 || headIndex >= SectorBucketHeads.Length)
                return;

            int entityIndex = SectorBucketHeads[headIndex];
            int guard = 0;
            while (entityIndex >= 0 && entityIndex < EntityCount && killBudget > 0 && guard < EntityCount)
            {
                guard++;
                int linkIndex = SectorCount + entityIndex;
                int next = linkIndex >= 0 && linkIndex < SectorEntityLinks.Length ? SectorEntityLinks[linkIndex] : -1;
                AmbientEntityAupDTO meta = Aups[entityIndex];
                if ((meta.Flags & ShinobuEcosystemBalancer.EntityFlagCarnivore) == 0u ||
                    (meta.Flags & ShinobuEcosystemBalancer.EntityFlagActive) == 0u)
                {
                    entityIndex = next;
                    continue;
                }

                meta.Flags = (meta.Flags & ~(ShinobuEcosystemBalancer.EntityFlagActive | ShinobuEcosystemBalancer.EntityFlagHydrated | ShinobuEcosystemBalancer.EntityFlagSkipUpdate)) |
                             ShinobuEcosystemBalancer.EntityFlagFree;
                Aups[entityIndex] = meta;
                killBudget--;
                tombstoned++;
                entityIndex = next;
            }
        }

        private void RehydrateNearSectors(ref int freeCursor, ref int reproduced)
        {
            for (int i = 0; i < SectorCount; i++)
            {
                EcosystemSectorDTO sector = Sectors[i];
                if ((sector.Flags & ShinobuEcosystemBalancer.SectorFlagDehydrated) == 0u)
                    continue;

                double3 centerAbs = SectorCenterAbsolute(in sector);
                AbsoluteUniversePosition sectorAup = ShinobuEcosystemBalancer.FromAbsoluteDouble3(centerAbs);
                float3 local = ShinobuEcosystemBalancer.AupToLocal(in sectorAup, in CenterAup);
                if (math.lengthsq(local) > RehydrationDistanceSq)
                    continue;

                float totalMass = math.max(0f, sector.HerbivoreMass + sector.CarnivoreMass);
                float density01 = ShinobuEcosystemBalancer.Smooth01(GlobalQualityWeight);
                int spawnCount = math.clamp((int)math.floor((totalMass * 0.01f) * math.lerp(0.25f, 1f, density01)), 0, 64);
                uint seed = sector.SectorHash ^ 0x52485944u;
                int spawnedThisSector = 0;
                for (int spawn = 0; spawn < spawnCount; spawn++)
                {
                    int slot = FindNextFreeSlot(ref freeCursor);
                    if (slot < 0)
                        break;

                    seed = NextLcg(seed);
                    float3 jitter = ResolveJitter(seed) * (SectorSizeMeters * 0.45f);
                    bool carnivore = spawn < (int)math.floor(sector.CarnivoreMass);
                    float biomass = carnivore ? 2.2f : 1f;
                    AbsoluteUniversePosition aup = ShinobuEcosystemBalancer.FromAbsoluteDouble3(centerAbs + (double3)jitter);
                    uint flags = ShinobuEcosystemBalancer.EntityFlagActive | ShinobuEcosystemBalancer.EntityFlagHydrated |
                                 (carnivore ? ShinobuEcosystemBalancer.EntityFlagCarnivore : ShinobuEcosystemBalancer.EntityFlagHerbivore);
                    Entities[slot] = new AmbientEntityDTO
                    {
                        Position = local + jitter,
                        Velocity = ShinobuEcosystemBalancer.SafeNormalize(jitter, new float3(0f, 0f, 1f)) * math.max(0.5f, Tuning.MaxSpeedMetersPerSecond),
                        SpeciesHash = carnivore ? 0x4341524Eu : 0x48455242u,
                        Biomass = biomass
                    };
                    Aups[slot] = new AmbientEntityAupDTO
                    {
                        PositionAup = aup,
                        Flags = flags,
                        SectorHash = sector.SectorHash,
                        SpatialCellHash = 0,
                        StableSeed = seed
                        };
                    reproduced++;
                    spawnedThisSector++;
                }

                if (spawnedThisSector >= spawnCount)
                    sector.Flags &= ~ShinobuEcosystemBalancer.SectorFlagDehydrated;
                Sectors[i] = sector;
            }
        }

        private void InsertSectorSlotIntoHash(int sectorSlot, in EcosystemSectorDTO sector, int hashBucketCount)
        {
            if (sectorSlot < 0 || sectorSlot >= SectorCount || hashBucketCount <= 0)
                return;

            int bucket = (int)(sector.SectorHash % (uint)hashBucketCount);
            SectorEntityLinks[sectorSlot] = SectorBucketHeads[bucket];
            SectorBucketHeads[bucket] = sectorSlot;
        }

        private void AppendCarnivoreToSectorChain(int sectorSlot, int entityIndex, int sectorHeadBase)
        {
            if (sectorSlot < 0 || sectorSlot >= SectorCount || entityIndex < 0 || entityIndex >= EntityCount)
                return;

            int headIndex = sectorHeadBase + sectorSlot;
            int linkIndex = SectorCount + entityIndex;
            if (headIndex < 0 || headIndex >= SectorBucketHeads.Length || linkIndex < 0 || linkIndex >= SectorEntityLinks.Length)
                return;

            SectorEntityLinks[linkIndex] = SectorBucketHeads[headIndex];
            SectorBucketHeads[headIndex] = entityIndex;
        }

        private int EnsureSectorSlot(int3 coord, ref int freeSectorCursor, int hashBucketCount)
        {
            uint hash = ShinobuEcosystemBalancer.ResolveSectorHash(coord);
            int bucket = hashBucketCount > 0 ? (int)(hash % (uint)hashBucketCount) : -1;
            int sectorSlot = bucket >= 0 ? SectorBucketHeads[bucket] : -1;
            int guard = 0;
            while (sectorSlot >= 0 && sectorSlot < SectorCount && guard < SectorCount)
            {
                guard++;
                EcosystemSectorDTO sector = Sectors[sectorSlot];
                if ((sector.Flags & ShinobuEcosystemBalancer.SectorFlagValid) != 0u &&
                    sector.SectorHash == hash &&
                    sector.SectorX == coord.x &&
                    sector.SectorY == coord.y &&
                    sector.SectorZ == coord.z)
                {
                    return sectorSlot;
                }

                sectorSlot = SectorEntityLinks[sectorSlot];
            }

            for (int i = math.max(0, freeSectorCursor); i < SectorCount; i++)
            {
                EcosystemSectorDTO sector = Sectors[i];
                if ((sector.Flags & ShinobuEcosystemBalancer.SectorFlagValid) != 0u)
                    continue;

                sector = new EcosystemSectorDTO
                {
                    SectorHash = hash,
                    HerbivoreMass = 0f,
                    CarnivoreMass = 0f,
                    FloraMass = MockFloraSpawner.SampleSectorFlora(coord),
                    SectorX = coord.x,
                    SectorY = coord.y,
                    SectorZ = coord.z,
                    Flags = ShinobuEcosystemBalancer.SectorFlagValid
                };
                Sectors[i] = sector;
                freeSectorCursor = i + 1;
                InsertSectorSlotIntoHash(i, in sector, hashBucketCount);
                return i;
            }

            freeSectorCursor = SectorCount;
            return -1;
        }

        private int FindNextFreeSlot(ref int cursor)
        {
            for (int i = math.max(0, cursor); i < EntityCount; i++)
            {
                AmbientEntityAupDTO meta = Aups[i];
                if ((meta.Flags & ShinobuEcosystemBalancer.EntityFlagActive) == 0u ||
                    (meta.Flags & ShinobuEcosystemBalancer.EntityFlagFree) != 0u)
                {
                    cursor = i + 1;
                    return i;
                }
            }

            cursor = EntityCount;
            return -1;
        }

        private double3 SectorCenterAbsolute(in EcosystemSectorDTO sector)
        {
            double size = math.max(1.0d, SectorSizeMeters);
            return new double3(
                (sector.SectorX + 0.5d) * size,
                (sector.SectorY + 0.5d) * size,
                (sector.SectorZ + 0.5d) * size);
        }

        private static uint NextLcg(uint state)
        {
            return unchecked((state * 1664525u) + 1013904223u);
        }

        private static float3 ResolveJitter(uint seed)
        {
            uint a = NextLcg(seed);
            uint b = NextLcg(a);
            uint c = NextLcg(b);
            return new float3(
                ((a & 1023u) * (2f / 1023f)) - 1f,
                ((b & 1023u) * (2f / 1023f)) - 1f,
                ((c & 1023u) * (2f / 1023f)) - 1f);
        }
    }
}
