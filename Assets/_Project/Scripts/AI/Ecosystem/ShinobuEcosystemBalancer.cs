using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
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
using MacroEcosystemSectorDTO = Hecton8.Core.Contracts.EcosystemSectorDTO;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.AI.Ecosystem
{
    /// <summary>
    /// Data-only SHINOBU swarm and biomass balancer. Fish are vault rows, not scene objects.
    /// </summary>
    public sealed partial class ShinobuEcosystemBalancer : ITickable, IColdTickable, ILateFrameTickable, IRenderable, IGlobalRegistryHotSwapListener, IDisposable
    {
        private const int DefaultEntityCapacity = 100000;
        private const int MinimumVisualBoidBudget = 1000;
        private const int DefaultSectorCapacity = 256;
        private const int TelemetryCapacity = 300;
        private const int FlockingTelemetryCapacity = 300;
        private const int FlockingThreatCapacity = 32;
        private const int FlockingCounterCapacity = 8;
        private const int CounterCapacity = 24;
        private const int DebugCellCapacity = 256;
        private const int SpatialHashBucketCapacity = 32768;
        private const int SpatialHashBucketMask = SpatialHashBucketCapacity - 1;
        private const int FrameJobBatchSize = 64;
        private const uint PortableMaxComputeThreadsPerGroup = 256u;
        private const float TrigPi = 3.14159265358979323846f;
        private const float TrigTwoPi = 6.28318530717958647692f;
        private const float TrigHalfPi = 1.57079632679489661923f;
        private const float TrigInvTwoPi = 0.15915494309189533577f;
        private const int MaxNeighborSamples = 48;
        private const int MaxSpatialHashChainSteps = 64;
        private const int SpatialGridBucketRangeCapacity = ShinobuSpatialGridConstants.BucketRangeCapacity;
        private const int SpatialGridProfileCapacity = ShinobuSpatialGridConstants.ProfileCapacity;
        private const int SpatialGridCsvMaxBytes = ShinobuSpatialGridConstants.CsvMaxBytes;
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
        private const float FlockingTelemetryFaultThresholdMs = 2.0f;
        private const double AupCellSizeMetersDouble = HectonPhysicsContract.AupSectorSizeMetersDouble;
        private const long AupSafeGridLimit = 1000000000000L;
        private const double AupSafeGridLimitDouble = 1000000000000.0d;
        private const float AupLocalLimitMeters = HectonPhysicsContract.AupSectorSizeMetersFloat + 1f;
        private const byte ScheduledPipelineNone = 0;
        private const byte ScheduledPipelineFrame = 1;
        private const byte ScheduledPipelineMacro = 2;
        private const string LegacyFaunaCapsFile = "fauna_population_caps.h8bin";
        private const string LegacyBoidProfileFile = "boid_behavior_profiles.bin";
        private const string SwarmSpeciesCsvRelativePath = "fauna_swarm_profiles.csv";
        private const string SwarmSpeciesCsvPrecomputedRelativePath = "Data/Precomputed/fauna_swarm_profiles.csv";
        private const string LegacySwarmSpeciesCsvRelativePath = "swarm_species_profiles.csv";
        private const string LegacySwarmSpeciesCsvPrecomputedRelativePath = "Data/Precomputed/swarm_species_profiles.csv";
        private const string SpatialGridCsvRelativePath = ShinobuSpatialGridConstants.ProfileCsvRelativePath;
        private const string SpatialGridCsvPrecomputedRelativePath = ShinobuSpatialGridConstants.ProfileCsvPrecomputedRelativePath;
        private const string CsvRelativePath = "ecosystem_balance.csv";
        private const string CsvPrecomputedRelativePath = "Data/Precomputed/ecosystem_balance.csv";
        private const uint SourceHash = 0x5348494Eu; // SHIN

        private static readonly ulong InitialPopulationMutationGuardMask =
            ShinobuMutationGuardBit(BufferID.ShinobuEcosystemCounters) |
            ShinobuMutationGuardBit(BufferID.ShinobuAmbientEntities) |
            ShinobuMutationGuardBit(BufferID.ShinobuAmbientAups) |
            ShinobuMutationGuardBit(BufferID.ShinobuBoidStates) |
            ShinobuMutationGuardBit(BufferID.ShinobuEcosystemSectors);
        private const uint JobPinEntities = 1u << 0;
        private const uint JobPinAups = 1u << 1;
        private const uint JobPinBoidStates = 1u << 2;
        private const uint JobPinEntitySnapshot = 1u << 3;
        private const uint JobPinAupSnapshot = 1u << 4;
        private const uint JobPinBoidStateSnapshot = 1u << 5;
        private const uint JobPinSectors = 1u << 6;
        private const uint JobPinCounters = 1u << 7;
        private const uint JobPinSpatialHashBucketHeads = 1u << 8;
        private const uint JobPinSpatialHashNext = 1u << 9;
        private const uint JobPinSpatialGridEntries = 1u << 10;
        private const uint JobPinSpatialGridSortScratch = 1u << 11;
        private const uint JobPinSpatialGridBucketRanges = 1u << 12;

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
        internal const int FlockingCounterNeighborSamples = 0;
        internal const int FlockingCounterEvaluatedBoids = 1;
        internal const int FlockingCounterPanicBoids = 2;
        internal const int FlockingCounterActiveThreats = 3;
        internal const int FlockingCounterMaxNeighbors = 4;
        internal const int FlockingCounterSpatialGridQueries = 5;

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
        private VaultGenerationHandle<EcosystemTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<FlockingThreatDTO> _flockingThreatHandle;
        private VaultGenerationHandle<int> _flockingThreatCountHandle;
        private VaultGenerationHandle<FlockingTelemetryEntry> _flockingTelemetryHandle;
        private VaultGenerationHandle<FlockingCounter64> _flockingCounterHandle;
        private VaultGenerationHandle<ShinobuSpatialHashDebugCell> _debugCellHandle;
        private VaultGenerationHandle<BoidMatrixDTO> _renderMatrixHandle;
        private VaultGenerationHandle<BoidCustomDataDTO> _renderCustomDataHandle;
        private VaultGenerationHandle<BoidIndirectArgsDTO> _indirectArgsHandle;
        private VaultGenerationHandle<int> _spatialHashBucketHeadHandle;
        private VaultGenerationHandle<int> _spatialHashNextHandle;
        private VaultGenerationHandle<SpatialGridEntryDTO> _spatialGridEntryHandle;
        private VaultGenerationHandle<SpatialGridEntryDTO> _spatialGridSortScratchHandle;
        private VaultGenerationHandle<SpatialGridBucketRangeDTO> _spatialGridBucketRangeHandle;
        private VaultGenerationHandle<SpatialGridTelemetryEntry> _spatialGridTelemetryHandle;
        private VaultGenerationHandle<int> _spatialGridTelemetryCursorHandle;
        private VaultGenerationHandle<SpatialGridTuningDTO> _spatialGridTuningHandle;
        private VaultGenerationHandle<SpatialGridProfileDTO> _spatialGridProfileHandle;
        private VaultGenerationHandle<byte> _spatialGridDumpSnapshotHandle;
        private VaultGenerationHandle<byte> _ecosystemDumpSnapshotHandle;
        private VaultGenerationHandle<SwarmSpeciesProfileDTO> _swarmSpeciesProfileHandle;

        private NativeArray<EcosystemTelemetryEntry> _ecosystemTelemetryMirror;
        private NativeArray<FlockingTelemetryEntry> _flockingTelemetryMirror;
        private NativeArray<SpatialGridTelemetryEntry> _spatialGridTelemetryMirror;
        private NativeArray<SpatialGridTelemetryEntry> _spatialGridTelemetryFrame;
        private NativeArray<FlockingThreatDTO> _flockingThreatJobSnapshot;
        private NativeArray<int> _flockingThreatCountJobSnapshot;
        private NativeArray<FlockingCounter64> _flockingCounterJobScratch;
        private NativeArray<BoidMatrixDTO> _renderMatrixJobScratch;
        private NativeArray<BoidCustomDataDTO> _renderCustomDataJobScratch;
        private NativeArray<AmbientEntityDTO> _entitySnapshotJobScratch;
        private NativeArray<AmbientEntityAupDTO> _aupSnapshotJobScratch;
        private NativeArray<BoidStateDTO> _boidStateSnapshotJobScratch;
        private NativeArray<int> _spatialHashBucketHeadJobScratch;
        private NativeArray<int> _spatialHashNextJobScratch;
        private NativeArray<SpatialGridEntryDTO> _spatialGridEntryJobScratch;
        private NativeArray<SpatialGridEntryDTO> _spatialGridSortJobScratch;
        private NativeArray<SpatialGridBucketRangeDTO> _spatialGridBucketRangeJobScratch;
        private NativeArray<ShinobuSpatialHashDebugCell> _debugCellJobScratch;
        private NativeArray<int> _debugCellCountJobScratch;

        private byte[] _ecosystemLegacyManagedScratch;
#if UNITY_EDITOR
        private byte[] _ecosystemCsvManagedScratch;
        private byte[] _spatialGridCsvManagedScratch;
        private SwarmSpeciesProfileDTO[] _swarmSpeciesManagedScratch;
        private SpatialGridProfileDTO[] _spatialGridProfileManagedScratch;
#endif

        private IDataVault _dataVault;
        private readonly ShinobuBoidGpuUploadDispatcher _gpuUploadDispatcher;
        private JobHandle _activeJobHandle;
        private AbsoluteUniversePosition _cameraAup;
        private float3 _cameraLocalPosition;
        private float3 _cameraForward = new float3(0f, 0f, 1f);
        private long _csvTimestampTicks;
        private long _swarmSpeciesCsvTimestampTicks;
        private long _spatialGridCsvTimestampTicks;
        private long _scheduleTicks;
        private int _telemetryCursor;
        private int _flockingTelemetryCursor;
        private int _spatialGridTelemetryMirrorCursor;
        private int _coldTickIndex;
        private uint _simulationFrameCounter;
        private uint _lastFlockingDispersalSignalFrame;
        private uint _spatialGridRangeEpoch = 0xA3010001u;
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
        private bool _jobBufferPinsHeld;
        private bool _vaultBuffersReady;
        private bool _dumpedFault;
        private bool _dumpedFlockingFault;
        private bool _dumpedSpatialGridFault;
        private bool _spatialGridTelemetryMirrorValid;
        private bool _debugCellPublishPending;
        private bool _proceduralRenderEnabled;
        private bool _supportsComputeShadersCold;
        private bool _proceduralCullKernelsResolved;
        private byte _scheduledPipelineKind;
        private uint _runtimeFlags;
        private uint _jobBufferPinMask;
        private int _proceduralRenderLayer;
        private IDataVault _jobBufferPinVault;
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
        private int _proceduralClearArgsThreadGroupSizeX;
        private int _proceduralCullThreadGroupSizeX;
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
            DisposeRuntimeForLifecycleTransition();
        }

        private static void DisposeRuntimeForLifecycleTransition()
        {
            ShinobuEcosystemBalancer runtime = s_runtime;
            s_runtime = null;
            if (runtime != null)
                runtime.Dispose();
            else
            {
                ShinobuEcosystemTelemetryForensics.ShutdownDumpWorker();
                ShinobuSpatialGridForensics.ShutdownDumpWorker();
            }
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorLifecycleTeardown()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= DisposeRuntimeForLifecycleTransition;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += DisposeRuntimeForLifecycleTransition;
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode ||
                change == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                DisposeRuntimeForLifecycleTransition();
            }
        }
#endif

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
            _spatialGridCsvTimestampTicks = 0L;
            MonitorCsvOverrides(vault);
        }
#endif

        private void Activate()
        {
            if (!Application.isPlaying)
                return;

            RefreshGraphicsCapabilitiesCold();
            SignalBus<MockPredatorSignal>.EnsureInitialized();
            EnsureDataVaultCold();
            TryRegisterHotSwapListener();
            if (EnsureVaultState())
            {
                TryEnsureEcosystemDumpWorker(_dataVault);
                TryEnsureSpatialGridDumpWorker(_dataVault);
                TryRegisterTicks();
            }
            if (_proceduralRenderMaterial != null)
                TryRegisterRender();
        }

        private void TryEnsureSpatialGridDumpWorker(IDataVault vault)
        {
            if (!ShinobuSpatialGridForensics.EnsureDumpWorker(
                    BuildProjectRootForIo(),
                    vault,
                    in _spatialGridDumpSnapshotHandle))
            {
                ShinobuSpatialGridForensics.RecordQueueFailure();
            }
        }

        private void TryEnsureEcosystemDumpWorker(IDataVault vault)
        {
            if (!ShinobuEcosystemTelemetryForensics.EnsureDumpWorker(
                    BuildProjectRootForIo(),
                    vault,
                    in _ecosystemDumpSnapshotHandle))
            {
                ShinobuEcosystemTelemetryForensics.RecordQueueFailure();
            }
        }

        public void Dispose()
        {
            CompleteFrameJobForTeardown();
            TryUnregisterRender();
            TryUnregisterTicks();
            TryUnregisterHotSwapListener();
            ShinobuEcosystemTelemetryForensics.ShutdownDumpWorker();
            ShinobuSpatialGridForensics.ShutdownDumpWorker();
            _gpuUploadDispatcher.Dispose();
            ReleaseVaultStateForLifecycle(clearRenderState: true);
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
            ShinobuEcosystemTelemetryForensics.ShutdownDumpWorker();
            ShinobuSpatialGridForensics.ShutdownDumpWorker();
            RebindDataVaultForLifecycle(currentService is IDataVault currentVault ? currentVault : null);

            if (_dataVault == null || !EnsureVaultState())
            {
                TryUnregisterTicks();
                return;
            }

            ClearSpatialGridRangeTable(_dataVault);
            TryEnsureEcosystemDumpWorker(_dataVault);
            TryEnsureSpatialGridDumpWorker(_dataVault);
            TryRegisterTicks();
        }

        public void Tick(float deltaTime)
        {
            if (_jobScheduled)
                return;

            if (!HasVaultStateReady())
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            RefreshCameraSignals();

            float visualQualityWeight = ResolveGlobalQualityWeight01();
            float spatialQualityWeight = visualQualityWeight;
            float systemStress01 = ResolveSystemStress01();
            if (!TryReadEcosystemTuning(vault, out ShinobuEcosystemTuning tuning))
                return;
            tuning = ShinobuEcosystemTuning.Sanitize(tuning);
            if (!TryReadSpatialGridTuning(vault, out SpatialGridTuningDTO spatialGridTuning))
                return;
            spatialGridTuning = ShinobuSpatialGridMath.Sanitize(spatialGridTuning);
            bool frameDebugGridRequested = (tuning.Flags & TuningFlagEditorDebugGrid) != 0u;

            if (vault.IsCompactionFenceActive)
                return;

            if (!TryPinFrameJobBuffers(vault))
                return;

            JobHandle scheduledHandle = default;
            bool scheduledWork = false;
            int count = 0;
            try
            {
                if (!TryResolveFrameJobBuffers(
                        vault,
                        out NativeArray<AmbientEntityDTO> entities,
                        out NativeArray<AmbientEntityAupDTO> aups,
                        out NativeArray<BoidStateDTO> boidStates,
                        out NativeArray<AmbientEntityDTO> entitySnapshot,
                        out NativeArray<AmbientEntityAupDTO> aupSnapshot,
                        out NativeArray<BoidStateDTO> boidStateSnapshot,
                        out NativeArray<int> counters,
                        out NativeArray<int> spatialHashBucketHeads,
                        out NativeArray<int> spatialHashNext))
                {
                    return;
                }

                if (!TryResolveFrameSpatialGridBuffers(
                        out NativeArray<SpatialGridEntryDTO> spatialGridEntries,
                        out NativeArray<SpatialGridEntryDTO> spatialGridScratch,
                        out NativeArray<SpatialGridBucketRangeDTO> spatialGridBucketRanges))
                {
                    return;
                }

                if (!TryResolveFrameFlockingBuffers(
                        out NativeArray<FlockingThreatDTO> flockingThreats,
                        out NativeArray<int> flockingThreatCount,
                        out NativeArray<FlockingCounter64> flockingCounters))
                {
                    return;
                }

                if (!_renderMatrixJobScratch.IsCreated || !_renderCustomDataJobScratch.IsCreated)
                    return;

                if (vault.IsCompactionFenceActive)
                    return;

                count = entityCapacity;
                count = math.min(count, entities.Length);
                count = math.min(count, aups.Length);
                count = math.min(count, boidStates.Length);
                count = math.min(count, entitySnapshot.Length);
                count = math.min(count, aupSnapshot.Length);
                count = math.min(count, boidStateSnapshot.Length);
                count = math.min(count, spatialHashNext.Length);
                count = math.min(count, spatialGridEntries.Length);
                count = math.min(count, spatialGridScratch.Length);
                count = math.min(count, _renderMatrixJobScratch.Length);
                count = math.min(count, _renderCustomDataJobScratch.Length);
                count = ResolveActiveEntityBudget(count);
                if (count <= 0)
                    return;

                int preservedDehydratedSectorCount = counters.Length > CounterDehydratedSectors
                    ? counters[CounterDehydratedSectors]
                    : 0;
                for (int i = 0; i < CounterCapacity && i < counters.Length; i++)
                    counters[i] = 0;
                if (counters.Length > CounterDehydratedSectors)
                    counters[CounterDehydratedSectors] = preservedDehydratedSectorCount;
                for (int i = 0; i < FlockingCounterCapacity && i < flockingCounters.Length; i++)
                    flockingCounters[i] = default;

                if (spatialHashBucketHeads.Length < SpatialHashBucketCapacity)
                {
                    counters[CounterSpatialHashOverflow] = 1;
                    return;
                }

                CaptureFlockingThreatSignals(flockingThreats, flockingThreatCount, flockingCounters, spatialQualityWeight);

                MockPredatorRuntime predator = ResolvePredatorRuntime();
                bool debugGridEnabled = frameDebugGridRequested &&
                                        _debugCellJobScratch.IsCreated &&
                                        _debugCellCountJobScratch.IsCreated &&
                                        _debugCellCountJobScratch.Length > 0;
                if (debugGridEnabled)
                    _debugCellCountJobScratch[0] = 0;
                float resolvedSpatialCellSize = ShinobuSpatialGridMath.ResolveCellSizeMeters(in spatialGridTuning, spatialQualityWeight, systemStress01);
                int maxNeighborSamples = math.min(ResolveNeighborSampleBudget(MaxNeighborSamples, spatialQualityWeight), ShinobuSpatialGridMath.ResolveMaxQueryResults(spatialGridTuning.MaxQueryResultsLimit, spatialQualityWeight));
                SetFlockingCounter(flockingCounters, FlockingCounterMaxNeighbors, maxNeighborSamples);
                int maxQueryCellProbeCount = ShinobuSpatialGridMath.ResolveProbeCount(spatialQualityWeight);
                int structuralRangeProbeCount = ShinobuSpatialGridMath.ResolveStructuralProbeCount(spatialGridBucketRanges.Length);
                int updateStride = ResolveUpdateStride(spatialQualityWeight, systemStress01);
                float simulationDeltaSeconds = ResolveSimulationTickDelta(spatialQualityWeight);
                double3 cameraAbsolute = ToAbsoluteDouble3(in _cameraAup);
                uint simulationFrame = AdvanceSimulationFrame();
                uint spatialGridFrame = ResolveSpatialGridRangeFrame(simulationFrame);
                if (_spatialGridTelemetryFrame.IsCreated)
                    _spatialGridTelemetryFrame[0] = default;
                LocalShiftAndSpatialHashJob buildJob = default;
                buildJob.Entities = entities;
                buildJob.Aups = aups;
                buildJob.BoidStates = boidStates;
                buildJob.EntitySnapshot = entitySnapshot;
                buildJob.AupSnapshot = aupSnapshot;
                buildJob.BoidStateSnapshot = boidStateSnapshot;
                buildJob.CenterAup = _cameraAup;
                buildJob.CellSizeMeters = resolvedSpatialCellSize;
                buildJob.SectorSizeMeters = math.max(1f, sectorSizeMeters);
                buildJob.SystemStress01 = systemStress01;
                buildJob.GlobalQualityWeight = spatialQualityWeight;
                buildJob.UpdateStride = updateStride;
                buildJob.Frame = simulationFrame;
                buildJob.Count = count;

                _scheduleTicks = Stopwatch.GetTimestamp();
                JobHandle handle = buildJob.Schedule(count, FrameJobBatchSize);
                scheduledHandle = handle;
                scheduledWork = true;
                QuantizeEntityCoordinatesJob quantizeJob = default;
                quantizeJob.AupSnapshot = aupSnapshot;
                quantizeJob.Entries = spatialGridEntries;
                quantizeJob.CellSizeMeters = resolvedSpatialCellSize;
                quantizeJob.HashMultiplierX = spatialGridTuning.HashMultiplierX;
                quantizeJob.HashMultiplierY = spatialGridTuning.HashMultiplierY;
                quantizeJob.HashMultiplierZ = spatialGridTuning.HashMultiplierZ;
                quantizeJob.Count = count;
                handle = quantizeJob.Schedule(count, FrameJobBatchSize, handle);
                scheduledHandle = handle;
                SortSpatialGridJob sortJob = default;
                sortJob.Entries = spatialGridEntries;
                sortJob.Scratch = spatialGridScratch;
                sortJob.Count = count;
                handle = sortJob.Schedule(handle);
                scheduledHandle = handle;
                BuildSpatialGridRangesJob rangeJob = default;
                rangeJob.Entries = spatialGridEntries;
                rangeJob.AupSnapshot = aupSnapshot;
                rangeJob.BucketRanges = spatialGridBucketRanges;
                rangeJob.Counters = counters;
                rangeJob.TelemetryOutput = _spatialGridTelemetryFrame;
                rangeJob.Frame = spatialGridFrame;
                rangeJob.CellSizeMeters = resolvedSpatialCellSize;
                rangeJob.GlobalQualityWeight = spatialQualityWeight;
                rangeJob.MaxProbeCount = structuralRangeProbeCount;
                rangeJob.MaxQueryResults = maxNeighborSamples;
                rangeJob.Count = count;
                rangeJob.CounterOverflowIndex = CounterSpatialHashOverflow;
                rangeJob.CounterInvalidIndex = CounterInvalidMath;
                handle = rangeJob.Schedule(handle);
                scheduledHandle = handle;
                if (debugGridEnabled)
                {
                    BuildSpatialGridDebugCellsJob debugJob = default;
                    debugJob.BucketRanges = spatialGridBucketRanges;
                    debugJob.Entries = spatialGridEntries;
                    debugJob.AupSnapshot = aupSnapshot;
                    debugJob.DebugCells = _debugCellJobScratch;
                    debugJob.DebugCellCount = _debugCellCountJobScratch;
                    debugJob.CenterAbsolute = cameraAbsolute;
                    debugJob.Frame = spatialGridFrame;
                    debugJob.CellSizeMeters = resolvedSpatialCellSize;
                    debugJob.Count = count;
                    debugJob.Capacity = math.min(DebugCellCapacity, _debugCellJobScratch.Length);
                    handle = debugJob.Schedule(handle);
                    scheduledHandle = handle;
                }

                BoidFlockingJob solveJob = default;
                solveJob.Entities = entities;
                solveJob.Aups = aups;
                solveJob.BoidStates = boidStates;
                solveJob.EntitySnapshot = entitySnapshot;
                solveJob.AupSnapshot = aupSnapshot;
                solveJob.BoidStateSnapshot = boidStateSnapshot;
                solveJob.SpatialGridEntries = spatialGridEntries;
                solveJob.SpatialGridBucketRanges = spatialGridBucketRanges;
                solveJob.Threats = flockingThreats;
                solveJob.ThreatCount = flockingThreatCount;
                solveJob.FlockingCounters = flockingCounters;
                solveJob.SpatialGridFrame = spatialGridFrame;
                solveJob.SpatialGridBucketRangeMask = SpatialGridBucketRangeCapacity - 1;
                solveJob.HashMultiplierX = spatialGridTuning.HashMultiplierX;
                solveJob.HashMultiplierY = spatialGridTuning.HashMultiplierY;
                solveJob.HashMultiplierZ = spatialGridTuning.HashMultiplierZ;
                solveJob.CenterAup = _cameraAup;
                solveJob.CenterAbsolute = cameraAbsolute;
                solveJob.CameraForward = SafeNormalize(_cameraForward, math.float3(0f, 0f, 1f));
                solveJob.TerrainSampler = MockTerrainSampler.CreateDefault();
                solveJob.Predator = predator;
                solveJob.Tuning = tuning;
                solveJob.DeltaSeconds = simulationDeltaSeconds;
                solveJob.GlobalQualityWeight = spatialQualityWeight;
                solveJob.CellSizeMeters = resolvedSpatialCellSize;
                solveJob.SectorSizeMeters = math.max(1f, sectorSizeMeters);
                solveJob.NeighborRadiusMeters = math.max(1f, neighborRadiusMeters);
                solveJob.ObstacleProbeMeters = math.max(0.1f, obstacleProbeMeters);
                solveJob.MaxNeighborSamplesPerBoid = maxNeighborSamples;
                solveJob.MaxSpatialGridProbeCount = maxQueryCellProbeCount;
                solveJob.Count = count;
                handle = solveJob.Schedule(count, FrameJobBatchSize, handle);
                scheduledHandle = handle;

                BuildShinobuRenderPayloadJob renderJob = default;
                renderJob.Entities = entities;
                renderJob.Aups = aups;
                renderJob.BoidStates = boidStates;
                renderJob.Matrices = _renderMatrixJobScratch;
                renderJob.CustomData = _renderCustomDataJobScratch;
                renderJob.CenterAbsolute = cameraAbsolute;
                renderJob.GlobalQualityWeight = visualQualityWeight;
                renderJob.Count = count;
                handle = renderJob.Schedule(count, FrameJobBatchSize, handle);
                scheduledHandle = handle;

                CountTelemetryCountersJob countJob = default;
                countJob.Aups = aups;
                countJob.Counters = counters;
                countJob.Count = count;
                handle = countJob.Schedule(handle);
                scheduledHandle = handle;

                _activeJobHandle = handle;
                _lastActiveBudget = count;
                _lastGlobalQualityWeight = visualQualityWeight;
                _lastSpatialHashMs = 0f;
                _lastMatrixUploadMs = 0f;
                _runtimeFlags &= ~TelemetryFlagMacroPass;
                _scheduledPipelineKind = ScheduledPipelineFrame;
                _debugCellPublishPending = debugGridEnabled;
                _jobScheduled = true;
                H8Memory.RegisterActiveJob(SystemID.AIEcology, _activeJobHandle);
            }
            catch (InvalidOperationException)
            {
                if (scheduledWork)
                {
                    _activeJobHandle = scheduledHandle;
                    _lastActiveBudget = count;
                    _lastGlobalQualityWeight = visualQualityWeight;
                    _scheduledPipelineKind = ScheduledPipelineFrame;
                    _jobScheduled = true;
                    H8Memory.RegisterActiveJob(SystemID.AIEcology, _activeJobHandle);
                }

                GlobalTelemetryBus.PublishPerformanceWarning(0x534A4F42u, SourceHash, 0f);
            }
            finally
            {
                if (!_jobScheduled)
                    ReleaseActiveJobBufferPins(vault);
            }
        }

        public void ColdTick()
        {
            if (_jobScheduled)
                return;

            if (!HasVaultStateReady())
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            RefreshCameraSignals();
            MonitorCsvOverrides(vault);
            if (!HasCanonicalMacroEcosystem(vault))
                RunMacroBiomassPass(vault);
            else
                _runtimeFlags &= ~TelemetryFlagMacroPass;
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
            bool computeChanged = !ReferenceEquals(_proceduralCullCompute, cullingCompute);
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

            if (cullingCompute == null || !_supportsComputeShadersCold)
            {
                _proceduralCullCompute = null;
                _proceduralClearArgsKernel = -1;
                _proceduralCullKernel = -1;
                _proceduralClearArgsThreadGroupSizeX = 0;
                _proceduralCullThreadGroupSizeX = 0;
                _proceduralCullKernelsResolved = false;
                return;
            }

            if (computeChanged || !_proceduralCullKernelsResolved)
            {
                _proceduralClearArgsKernel = ResolveSupportedKernel(cullingCompute, ShinobuSwarmGpuCullingParams.ClearKernelName, _supportsComputeShadersCold);
                _proceduralCullKernel = ResolveSupportedKernel(cullingCompute, ShinobuSwarmGpuCullingParams.CullKernelName, _supportsComputeShadersCold);
                _proceduralClearArgsThreadGroupSizeX = _proceduralClearArgsKernel >= 0
                    ? ResolveKernelThreadGroupSizeX(cullingCompute, _proceduralClearArgsKernel, _supportsComputeShadersCold)
                    : 0;
                _proceduralCullThreadGroupSizeX = _proceduralCullKernel >= 0
                    ? ResolveKernelThreadGroupSizeX(cullingCompute, _proceduralCullKernel, _supportsComputeShadersCold)
                    : 0;
                _proceduralCullKernelsResolved = true;
            }
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
            culling.ClearArgsThreadGroupSizeX = _proceduralClearArgsThreadGroupSizeX;
            culling.CullThreadGroupSizeX = _proceduralCullThreadGroupSizeX;
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

        private static int ResolveKernelThreadGroupSizeX(ComputeShader compute, int kernel, bool supportsComputeShaders)
        {
            if (compute == null || kernel < 0 || !supportsComputeShaders)
                return 0;

            uint sizeX;
            uint sizeY;
            uint sizeZ;
            try
            {
                if (!compute.IsSupported(kernel))
                    return 0;

                compute.GetKernelThreadGroupSizes(kernel, out sizeX, out sizeY, out sizeZ);
            }
            catch (System.ObjectDisposedException)
            {
                return 0;
            }
            catch (System.InvalidOperationException)
            {
                return 0;
            }
            catch (System.ArgumentException)
            {
                return 0;
            }
            catch (UnityEngine.MissingReferenceException)
            {
                return 0;
            }
            catch (UnityEngine.UnityException)
            {
                return 0;
            }
            if (sizeX == 0u || sizeY != 1u || sizeZ != 1u || sizeX > int.MaxValue)
                return 0;

            ulong totalThreads = sizeX * (ulong)sizeY * sizeZ;
            return totalThreads <= PortableMaxComputeThreadsPerGroup ? (int)sizeX : 0;
        }

        private static int ResolveSupportedKernel(ComputeShader compute, string kernelName, bool supportsComputeShaders)
        {
            if (compute == null || !supportsComputeShaders)
                return -1;

            try
            {
                if (!compute.HasKernel(kernelName))
                    return -1;

                int kernel = compute.FindKernel(kernelName);
                if (kernel < 0)
                    return -1;

                return compute.IsSupported(kernel) ? kernel : -1;
            }
            catch (System.ObjectDisposedException)
            {
                return -1;
            }
            catch (System.InvalidOperationException)
            {
                return -1;
            }
            catch (System.ArgumentException)
            {
                return -1;
            }
            catch (MissingReferenceException)
            {
                return -1;
            }
            catch (UnityException)
            {
                return -1;
            }
        }

        private void RefreshGraphicsCapabilitiesCold()
        {
            bool supportsCompute = SystemInfo.supportsComputeShaders;
            if (supportsCompute == _supportsComputeShadersCold)
                return;

            _supportsComputeShadersCold = supportsCompute;
            _proceduralCullKernelsResolved = false;
            if (supportsCompute)
                return;

            _proceduralCullCompute = null;
            _proceduralClearArgsKernel = -1;
            _proceduralCullKernel = -1;
            _proceduralClearArgsThreadGroupSizeX = 0;
            _proceduralCullThreadGroupSizeX = 0;
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
            IDataVault currentVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, currentVault))
                RebindDataVaultForLifecycle(currentVault);

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
            _telemetryHandle = ClaimVaultHandle<EcosystemTelemetryEntry>(
                vault,
                BufferID.ShinobuEcosystemTelemetryRing,
                TelemetryCapacity,
                NativeArrayOptions.ClearMemory);
            _flockingThreatHandle = ClaimVaultHandle<FlockingThreatDTO>(
                vault,
                BufferID.ShinobuFlockingThreats,
                FlockingThreatCapacity,
                NativeArrayOptions.UninitializedMemory);
            _flockingThreatCountHandle = ClaimVaultHandle<int>(
                vault,
                BufferID.ShinobuFlockingThreatCount,
                1,
                NativeArrayOptions.ClearMemory);
            _flockingTelemetryHandle = ClaimVaultHandle<FlockingTelemetryEntry>(
                vault,
                BufferID.ShinobuFlockingTelemetryRing,
                FlockingTelemetryCapacity,
                NativeArrayOptions.ClearMemory);
            _flockingCounterHandle = ClaimVaultHandle<FlockingCounter64>(
                vault,
                BufferID.ShinobuFlockingCounters64,
                FlockingCounterCapacity,
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
            _renderCustomDataHandle = ClaimVaultHandle<BoidCustomDataDTO>(
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
            _spatialGridEntryHandle = ClaimVaultHandle<SpatialGridEntryDTO>(
                vault,
                BufferID.ShinobuSpatialGridEntries,
                entityCapacity,
                NativeArrayOptions.UninitializedMemory);
            _spatialGridSortScratchHandle = ClaimVaultHandle<SpatialGridEntryDTO>(
                vault,
                BufferID.ShinobuSpatialGridSortScratch,
                entityCapacity,
                NativeArrayOptions.UninitializedMemory);
            _spatialGridBucketRangeHandle = ClaimVaultHandle<SpatialGridBucketRangeDTO>(
                vault,
                BufferID.ShinobuSpatialGridBucketRanges,
                SpatialGridBucketRangeCapacity,
                NativeArrayOptions.ClearMemory);
            _spatialGridTelemetryHandle = ClaimVaultHandle<SpatialGridTelemetryEntry>(
                vault,
                BufferID.ShinobuSpatialGridTelemetryRing,
                ShinobuSpatialGridConstants.TelemetryCapacity,
                NativeArrayOptions.ClearMemory);
            _spatialGridTelemetryCursorHandle = ClaimVaultHandle<int>(
                vault,
                BufferID.ShinobuSpatialGridTelemetryCursor,
                1,
                NativeArrayOptions.ClearMemory);
            _spatialGridTuningHandle = ClaimVaultHandle<SpatialGridTuningDTO>(
                vault,
                BufferID.ShinobuSpatialGridTuning,
                1,
                NativeArrayOptions.ClearMemory);
            _spatialGridProfileHandle = ClaimVaultHandle<SpatialGridProfileDTO>(
                vault,
                BufferID.ShinobuSpatialGridProfiles,
                SpatialGridProfileCapacity,
                NativeArrayOptions.ClearMemory);
            _spatialGridDumpSnapshotHandle = ClaimVaultHandle<byte>(
                vault,
                BufferID.ShinobuSpatialGridDumpSnapshot,
                ShinobuSpatialGridForensics.DumpSnapshotBytes,
                NativeArrayOptions.UninitializedMemory);
            _ecosystemDumpSnapshotHandle = ClaimVaultHandle<byte>(
                vault,
                BufferID.ShinobuEcosystemDumpSnapshot,
                ShinobuEcosystemTelemetryForensics.DumpSnapshotBytes,
                NativeArrayOptions.UninitializedMemory);
            _swarmSpeciesProfileHandle = ClaimVaultHandle<SwarmSpeciesProfileDTO>(
                vault,
                BufferID.ShinobuSwarmSpeciesProfiles,
                SwarmSpeciesProfileCapacity,
                NativeArrayOptions.ClearMemory);

            bool ready = AreVaultHandlesCreated(vault);
            if (ready)
                ready = EnsureTelemetryMirrorsCold();
            _vaultBuffersReady = ready;
            if (!ready)
                return false;

            EnsureGpuUploadCapacity();
            EnsureProfilesLoaded(vault);
            EnsureSpatialGridProfilesLoaded(vault);
            EnsureInitialPopulation(vault);
            return true;
        }

        private bool HasVaultStateReady()
        {
            IDataVault vault = _dataVault;
            return _vaultBuffersReady &&
                   vault != null &&
                   AreVaultHandlesCreated(vault);
        }

        private bool AreVaultHandlesCreated(IDataVault vault)
        {
            return TryOpenVaultView(vault, in _entityHandle, BufferID.ShinobuAmbientEntities, entityCapacity, out NativeArray<AmbientEntityDTO> _) &&
                   TryOpenVaultView(vault, in _aupHandle, BufferID.ShinobuAmbientAups, entityCapacity, out NativeArray<AmbientEntityAupDTO> _) &&
                   TryOpenVaultView(vault, in _boidStateHandle, BufferID.ShinobuBoidStates, entityCapacity, out NativeArray<BoidStateDTO> _) &&
                   TryOpenVaultView(vault, in _entitySnapshotHandle, BufferID.ShinobuAmbientEntitySnapshot, entityCapacity, out NativeArray<AmbientEntityDTO> _) &&
                   TryOpenVaultView(vault, in _aupSnapshotHandle, BufferID.ShinobuAmbientAupSnapshot, entityCapacity, out NativeArray<AmbientEntityAupDTO> _) &&
                   TryOpenVaultView(vault, in _boidStateSnapshotHandle, BufferID.ShinobuBoidStateSnapshot, entityCapacity, out NativeArray<BoidStateDTO> _) &&
                   TryOpenVaultView(vault, in _sectorHandle, BufferID.ShinobuEcosystemSectors, sectorCapacity, out NativeArray<EcosystemSectorDTO> _) &&
                   TryOpenVaultView(vault, in _tuningHandle, BufferID.ShinobuEcosystemTuning, 1, out NativeArray<ShinobuEcosystemTuning> _) &&
                   TryOpenVaultView(vault, in _counterHandle, BufferID.ShinobuEcosystemCounters, CounterCapacity, out NativeArray<int> _) &&
                   TryOpenVaultView(vault, in _telemetryHandle, BufferID.ShinobuEcosystemTelemetryRing, TelemetryCapacity, out NativeArray<EcosystemTelemetryEntry> _) &&
                   TryOpenVaultView(vault, in _flockingThreatHandle, BufferID.ShinobuFlockingThreats, FlockingThreatCapacity, out NativeArray<FlockingThreatDTO> _) &&
                   TryOpenVaultView(vault, in _flockingThreatCountHandle, BufferID.ShinobuFlockingThreatCount, 1, out NativeArray<int> _) &&
                   TryOpenVaultView(vault, in _flockingTelemetryHandle, BufferID.ShinobuFlockingTelemetryRing, FlockingTelemetryCapacity, out NativeArray<FlockingTelemetryEntry> _) &&
                   TryOpenVaultView(vault, in _flockingCounterHandle, BufferID.ShinobuFlockingCounters64, FlockingCounterCapacity, out NativeArray<FlockingCounter64> _) &&
                   TryOpenVaultView(vault, in _debugCellHandle, BufferID.ShinobuSpatialHashDebugCells, DebugCellCapacity, out NativeArray<ShinobuSpatialHashDebugCell> _) &&
                   TryOpenVaultView(vault, in _renderMatrixHandle, BufferID.ShinobuRenderMatrices, entityCapacity, out NativeArray<BoidMatrixDTO> _) &&
                   TryOpenVaultView(vault, in _renderCustomDataHandle, BufferID.ShinobuRenderCustomData, entityCapacity, out NativeArray<BoidCustomDataDTO> _) &&
                   TryOpenVaultView(vault, in _indirectArgsHandle, BufferID.ShinobuBoidIndirectArgs, 1, out NativeArray<BoidIndirectArgsDTO> _) &&
                   TryOpenVaultView(vault, in _spatialHashBucketHeadHandle, BufferID.ShinobuSpatialHashBucketHeads, SpatialHashBucketCapacity, out NativeArray<int> _) &&
                   TryOpenVaultView(vault, in _spatialHashNextHandle, BufferID.ShinobuSpatialHashNext, entityCapacity + sectorCapacity, out NativeArray<int> _) &&
                   TryOpenVaultView(vault, in _spatialGridEntryHandle, BufferID.ShinobuSpatialGridEntries, entityCapacity, out NativeArray<SpatialGridEntryDTO> _) &&
                   TryOpenVaultView(vault, in _spatialGridSortScratchHandle, BufferID.ShinobuSpatialGridSortScratch, entityCapacity, out NativeArray<SpatialGridEntryDTO> _) &&
                   TryOpenVaultView(vault, in _spatialGridBucketRangeHandle, BufferID.ShinobuSpatialGridBucketRanges, SpatialGridBucketRangeCapacity, out NativeArray<SpatialGridBucketRangeDTO> _) &&
                   TryOpenVaultView(vault, in _spatialGridTelemetryHandle, BufferID.ShinobuSpatialGridTelemetryRing, ShinobuSpatialGridConstants.TelemetryCapacity, out NativeArray<SpatialGridTelemetryEntry> _) &&
                   TryOpenVaultView(vault, in _spatialGridTelemetryCursorHandle, BufferID.ShinobuSpatialGridTelemetryCursor, 1, out NativeArray<int> _) &&
                   TryOpenVaultView(vault, in _spatialGridTuningHandle, BufferID.ShinobuSpatialGridTuning, 1, out NativeArray<SpatialGridTuningDTO> _) &&
                   TryOpenVaultView(vault, in _spatialGridProfileHandle, BufferID.ShinobuSpatialGridProfiles, SpatialGridProfileCapacity, out NativeArray<SpatialGridProfileDTO> _) &&
                   TryOpenVaultView(vault, in _spatialGridDumpSnapshotHandle, BufferID.ShinobuSpatialGridDumpSnapshot, ShinobuSpatialGridForensics.DumpSnapshotBytes, out NativeArray<byte> _) &&
                   TryOpenVaultView(vault, in _ecosystemDumpSnapshotHandle, BufferID.ShinobuEcosystemDumpSnapshot, ShinobuEcosystemTelemetryForensics.DumpSnapshotBytes, out NativeArray<byte> _) &&
                   TryOpenVaultView(vault, in _swarmSpeciesProfileHandle, BufferID.ShinobuSwarmSpeciesProfiles, SwarmSpeciesProfileCapacity, out NativeArray<SwarmSpeciesProfileDTO> _);
        }

        private void EnsureGpuUploadCapacity()
        {
            if (Application.isBatchMode)
                return;

            try
            {
                _gpuUploadDispatcher.EnsureGraphicsResources(entityCapacity);
            }
            catch (InvalidOperationException)
            {
                _gpuUploadDispatcher.Dispose();
                GlobalTelemetryBus.PublishPerformanceWarning(0x47505543u, SourceHash, 0f);
            }
            catch (ArgumentException)
            {
                _gpuUploadDispatcher.Dispose();
                GlobalTelemetryBus.PublishPerformanceWarning(0x47505543u, SourceHash, 0f);
            }
            catch (UnityException)
            {
                _gpuUploadDispatcher.Dispose();
                GlobalTelemetryBus.PublishPerformanceWarning(0x47505543u, SourceHash, 0f);
            }
        }

        private bool EnsureTelemetryMirrorsCold()
        {
            try
            {
                EnsureNativeMirrorArray(ref _ecosystemTelemetryMirror, TelemetryCapacity, nameof(_ecosystemTelemetryMirror));
                EnsureNativeMirrorArray(ref _flockingTelemetryMirror, FlockingTelemetryCapacity, nameof(_flockingTelemetryMirror));
                EnsureNativeMirrorArray(
                    ref _spatialGridTelemetryMirror,
                    ShinobuSpatialGridConstants.TelemetryCapacity,
                    nameof(_spatialGridTelemetryMirror));
                EnsureNativeMirrorArray(
                    ref _spatialGridTelemetryFrame,
                    1,
                    nameof(_spatialGridTelemetryFrame));
                EnsureNativeMirrorArray(
                    ref _flockingThreatJobSnapshot,
                    FlockingThreatCapacity,
                    nameof(_flockingThreatJobSnapshot));
                EnsureNativeMirrorArray(
                    ref _flockingThreatCountJobSnapshot,
                    1,
                    nameof(_flockingThreatCountJobSnapshot));
                EnsureNativeMirrorArray(
                    ref _flockingCounterJobScratch,
                    FlockingCounterCapacity,
                    nameof(_flockingCounterJobScratch));
                EnsureNativeMirrorArray(
                    ref _renderMatrixJobScratch,
                    entityCapacity,
                    nameof(_renderMatrixJobScratch));
                EnsureNativeMirrorArray(
                    ref _renderCustomDataJobScratch,
                    entityCapacity,
                    nameof(_renderCustomDataJobScratch));
                EnsureNativeMirrorArray(
                    ref _entitySnapshotJobScratch,
                    entityCapacity,
                    nameof(_entitySnapshotJobScratch));
                EnsureNativeMirrorArray(
                    ref _aupSnapshotJobScratch,
                    entityCapacity,
                    nameof(_aupSnapshotJobScratch));
                EnsureNativeMirrorArray(
                    ref _boidStateSnapshotJobScratch,
                    entityCapacity,
                    nameof(_boidStateSnapshotJobScratch));
                EnsureNativeMirrorArray(
                    ref _spatialHashBucketHeadJobScratch,
                    SpatialHashBucketCapacity,
                    nameof(_spatialHashBucketHeadJobScratch));
                EnsureNativeMirrorArray(
                    ref _spatialHashNextJobScratch,
                    entityCapacity + sectorCapacity,
                    nameof(_spatialHashNextJobScratch));
                EnsureNativeMirrorArray(
                    ref _spatialGridEntryJobScratch,
                    entityCapacity,
                    nameof(_spatialGridEntryJobScratch));
                EnsureNativeMirrorArray(
                    ref _spatialGridSortJobScratch,
                    entityCapacity,
                    nameof(_spatialGridSortJobScratch));
                EnsureNativeMirrorArray(
                    ref _spatialGridBucketRangeJobScratch,
                    SpatialGridBucketRangeCapacity,
                    nameof(_spatialGridBucketRangeJobScratch));
                EnsureNativeMirrorArray(
                    ref _debugCellJobScratch,
                    DebugCellCapacity,
                    nameof(_debugCellJobScratch));
                EnsureNativeMirrorArray(
                    ref _debugCellCountJobScratch,
                    1,
                    nameof(_debugCellCountJobScratch));
                return true;
            }
            catch (ArgumentException)
            {
                DisposeTelemetryMirrorsCold();
                GlobalTelemetryBus.PublishPerformanceWarning(0x544D5241u, SourceHash, 0f);
                return false;
            }
            catch (InvalidOperationException)
            {
                DisposeTelemetryMirrorsCold();
                GlobalTelemetryBus.PublishPerformanceWarning(0x544D5249u, SourceHash, 0f);
                return false;
            }
            catch (OutOfMemoryException)
            {
                DisposeTelemetryMirrorsCold();
                GlobalTelemetryBus.PublishPerformanceWarning(0x544D524Fu, SourceHash, 0f);
                return false;
            }
        }

        private static void EnsureNativeMirrorArray<T>(ref NativeArray<T> array, int length, string label)
            where T : struct
        {
            if (array.IsCreated && array.Length == length)
                return;

            DisposeNativeMirrorArray(ref array);
            array = H8Memory.Allocate<T>(length, SystemID.AIEcology, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            if (!array.IsCreated)
                throw new InvalidOperationException($"{nameof(ShinobuEcosystemBalancer)} native allocation failed for {label}.");
        }

        private void DisposeTelemetryMirrorsCold()
        {
            DisposeNativeMirrorArray(ref _debugCellCountJobScratch);
            DisposeNativeMirrorArray(ref _debugCellJobScratch);
            DisposeNativeMirrorArray(ref _spatialGridBucketRangeJobScratch);
            DisposeNativeMirrorArray(ref _spatialGridSortJobScratch);
            DisposeNativeMirrorArray(ref _spatialGridEntryJobScratch);
            DisposeNativeMirrorArray(ref _spatialHashNextJobScratch);
            DisposeNativeMirrorArray(ref _spatialHashBucketHeadJobScratch);
            DisposeNativeMirrorArray(ref _boidStateSnapshotJobScratch);
            DisposeNativeMirrorArray(ref _aupSnapshotJobScratch);
            DisposeNativeMirrorArray(ref _entitySnapshotJobScratch);
            DisposeNativeMirrorArray(ref _renderCustomDataJobScratch);
            DisposeNativeMirrorArray(ref _renderMatrixJobScratch);
            DisposeNativeMirrorArray(ref _flockingCounterJobScratch);
            DisposeNativeMirrorArray(ref _flockingThreatCountJobSnapshot);
            DisposeNativeMirrorArray(ref _flockingThreatJobSnapshot);
            DisposeNativeMirrorArray(ref _spatialGridTelemetryFrame);
            DisposeNativeMirrorArray(ref _spatialGridTelemetryMirror);
            DisposeNativeMirrorArray(ref _flockingTelemetryMirror);
            DisposeNativeMirrorArray(ref _ecosystemTelemetryMirror);
        }

        private static void DisposeNativeMirrorArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            H8Memory.Release(ref array, SystemID.AIEcology);
        }

        private IDataVault EnsureDataVaultCold()
        {
            IDataVault currentVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, currentVault))
                RebindDataVaultForLifecycle(currentVault);

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
                if (!vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> existing))
                    return default;

                return IsOwnedVaultHandle(in existing, bufferId) ? existing : default;
            }

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.AIEcology,
                options);
            return IsOwnedVaultHandle(in handle, bufferId) ? handle : default;
        }

        private static bool IsOwnedVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)SystemID.AIEcology;
        }

        private static bool TryOpenVaultView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsOwnedVaultHandle(in handle, expectedBufferId) ||
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

        private static byte[] EnsureEditorByteScratch(ref byte[] scratch, int minimumLength)
        {
            int length = math.max(1, minimumLength);
            if (scratch == null || scratch.Length < length)
                scratch = new byte[length];
            return scratch;
        }

        private static SwarmSpeciesProfileDTO[] EnsureSwarmSpeciesScratch(ref SwarmSpeciesProfileDTO[] scratch, int minimumLength)
        {
            int length = math.max(1, minimumLength);
            if (scratch == null || scratch.Length < length)
                scratch = new SwarmSpeciesProfileDTO[length];
            return scratch;
        }

        private static SpatialGridProfileDTO[] EnsureSpatialGridProfileScratch(ref SpatialGridProfileDTO[] scratch, int minimumLength)
        {
            int length = math.max(1, minimumLength);
            if (scratch == null || scratch.Length < length)
                scratch = new SpatialGridProfileDTO[length];
            return scratch;
        }

        private bool TryReadCounterValue(IDataVault vault, int counterIndex, out int value)
        {
            value = 0;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!TryOpenVaultView(vault, in _counterHandle, BufferID.ShinobuEcosystemCounters, CounterCapacity, out NativeArray<int> counters) ||
                (uint)counterIndex >= (uint)counters.Length)
            {
                return false;
            }

            value = counters[counterIndex];
            return true;
        }

        private bool TryWriteCounterValue(IDataVault vault, int counterIndex, int value)
        {
            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuEcosystemCounters))
                return false;

            try
            {
                if (!TryOpenVaultView(vault, in _counterHandle, BufferID.ShinobuEcosystemCounters, CounterCapacity, out NativeArray<int> counters) ||
                    (uint)counterIndex >= (uint)counters.Length)
                {
                    return false;
                }

                counters[counterIndex] = value;
                return true;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuEcosystemCounters);
            }
        }

        private bool TryMaxCounterValue(IDataVault vault, int counterIndex, int value)
        {
            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuEcosystemCounters))
                return false;

            try
            {
                if (!TryOpenVaultView(vault, in _counterHandle, BufferID.ShinobuEcosystemCounters, CounterCapacity, out NativeArray<int> counters) ||
                    (uint)counterIndex >= (uint)counters.Length)
                {
                    return false;
                }

                counters[counterIndex] = math.max(counters[counterIndex], value);
                return true;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuEcosystemCounters);
            }
        }

        private bool TryIncrementCounterValue(IDataVault vault, int counterIndex)
        {
            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuEcosystemCounters))
                return false;

            try
            {
                if (!TryOpenVaultView(vault, in _counterHandle, BufferID.ShinobuEcosystemCounters, CounterCapacity, out NativeArray<int> counters) ||
                    (uint)counterIndex >= (uint)counters.Length)
                {
                    return false;
                }

                counters[counterIndex]++;
                return true;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuEcosystemCounters);
            }
        }

        private bool TryReadEcosystemTuning(IDataVault vault, out ShinobuEcosystemTuning tuning)
        {
            tuning = default;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!TryOpenVaultView(vault, in _tuningHandle, BufferID.ShinobuEcosystemTuning, 1, out NativeArray<ShinobuEcosystemTuning> buffer) ||
                buffer.Length <= 0)
            {
                return false;
            }

            tuning = buffer[0];
            return true;
        }

        private bool TryWriteEcosystemTuning(IDataVault vault, ShinobuEcosystemTuning tuning)
        {
            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuEcosystemTuning))
                return false;

            try
            {
                if (!TryOpenVaultView(vault, in _tuningHandle, BufferID.ShinobuEcosystemTuning, 1, out NativeArray<ShinobuEcosystemTuning> buffer) ||
                    buffer.Length <= 0)
                {
                    return false;
                }

                buffer[0] = tuning;
                return true;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuEcosystemTuning);
            }
        }

        private bool TryWriteSwarmSpeciesProfiles(IDataVault vault, SwarmSpeciesProfileDTO[] staged, int parsedCount)
        {
            if (staged == null ||
                vault == null ||
                !TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuSwarmSpeciesProfiles))
            {
                return false;
            }

            try
            {
                if (!TryOpenVaultView(vault, in _swarmSpeciesProfileHandle, BufferID.ShinobuSwarmSpeciesProfiles, SwarmSpeciesProfileCapacity, out NativeArray<SwarmSpeciesProfileDTO> profiles))
                    return false;

                int copyCount = math.min(math.max(0, parsedCount), math.min(staged.Length, profiles.Length));
                for (int i = 0; i < copyCount; i++)
                    profiles[i] = staged[i];
                for (int i = copyCount; i < profiles.Length; i++)
                    profiles[i] = default;
                return true;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuSwarmSpeciesProfiles);
            }
        }

        private bool TryReadSpatialGridTuning(IDataVault vault, out SpatialGridTuningDTO tuning)
        {
            tuning = default;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!TryOpenVaultView(vault, in _spatialGridTuningHandle, BufferID.ShinobuSpatialGridTuning, 1, out NativeArray<SpatialGridTuningDTO> buffer) ||
                buffer.Length <= 0)
            {
                return false;
            }

            tuning = buffer[0];
            return true;
        }

        private bool TryWriteSpatialGridTuning(IDataVault vault, SpatialGridTuningDTO tuning)
        {
            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridTuning))
                return false;

            try
            {
                if (!TryOpenVaultView(vault, in _spatialGridTuningHandle, BufferID.ShinobuSpatialGridTuning, 1, out NativeArray<SpatialGridTuningDTO> buffer) ||
                    buffer.Length <= 0)
                {
                    return false;
                }

                buffer[0] = tuning;
                return true;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridTuning);
            }
        }

        private bool TryWriteDefaultSpatialGridProfileIfEmpty(IDataVault vault, SpatialGridProfileDTO fallback)
        {
            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridProfiles))
                return false;

            try
            {
                if (!TryOpenVaultView(vault, in _spatialGridProfileHandle, BufferID.ShinobuSpatialGridProfiles, SpatialGridProfileCapacity, out NativeArray<SpatialGridProfileDTO> profiles) ||
                    profiles.Length <= 0 ||
                    profiles[0].LayerHash != 0u)
                {
                    return false;
                }

                profiles[0] = fallback;
                return true;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridProfiles);
            }
        }

        private bool TryWriteSpatialGridProfiles(IDataVault vault, SpatialGridProfileDTO[] staged, int parsedCount)
        {
            if (staged == null ||
                vault == null ||
                !TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridProfiles))
            {
                return false;
            }

            try
            {
                if (!TryOpenVaultView(vault, in _spatialGridProfileHandle, BufferID.ShinobuSpatialGridProfiles, SpatialGridProfileCapacity, out NativeArray<SpatialGridProfileDTO> profiles))
                    return false;

                int copyCount = math.min(math.max(0, parsedCount), math.min(staged.Length, profiles.Length));
                for (int i = 0; i < copyCount; i++)
                    profiles[i] = staged[i];
                for (int i = copyCount; i < profiles.Length; i++)
                    profiles[i] = default;
                return true;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridProfiles);
            }
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
            out NativeArray<EcosystemTelemetryEntry> telemetry,
            out NativeArray<ShinobuSpatialHashDebugCell> debugCells,
            out NativeArray<BoidMatrixDTO> matrices,
            out NativeArray<BoidCustomDataDTO> customData,
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

            return TryOpenVaultView(vault, in _entityHandle, BufferID.ShinobuAmbientEntities, entityCapacity, out entities) &&
                   TryOpenVaultView(vault, in _aupHandle, BufferID.ShinobuAmbientAups, entityCapacity, out aups) &&
                   TryOpenVaultView(vault, in _boidStateHandle, BufferID.ShinobuBoidStates, entityCapacity, out boidStates) &&
                   TryOpenVaultView(vault, in _entitySnapshotHandle, BufferID.ShinobuAmbientEntitySnapshot, entityCapacity, out entitySnapshot) &&
                   TryOpenVaultView(vault, in _aupSnapshotHandle, BufferID.ShinobuAmbientAupSnapshot, entityCapacity, out aupSnapshot) &&
                   TryOpenVaultView(vault, in _boidStateSnapshotHandle, BufferID.ShinobuBoidStateSnapshot, entityCapacity, out boidStateSnapshot) &&
                   TryOpenVaultView(vault, in _sectorHandle, BufferID.ShinobuEcosystemSectors, sectorCapacity, out sectors) &&
                   TryOpenVaultView(vault, in _tuningHandle, BufferID.ShinobuEcosystemTuning, 1, out tuning) &&
                   TryOpenVaultView(vault, in _counterHandle, BufferID.ShinobuEcosystemCounters, CounterCapacity, out counters) &&
                   TryOpenVaultView(vault, in _telemetryHandle, BufferID.ShinobuEcosystemTelemetryRing, TelemetryCapacity, out telemetry) &&
                   TryOpenVaultView(vault, in _debugCellHandle, BufferID.ShinobuSpatialHashDebugCells, DebugCellCapacity, out debugCells) &&
                   TryOpenVaultView(vault, in _renderMatrixHandle, BufferID.ShinobuRenderMatrices, entityCapacity, out matrices) &&
                   TryOpenVaultView(vault, in _renderCustomDataHandle, BufferID.ShinobuRenderCustomData, entityCapacity, out customData) &&
                   TryOpenVaultView(vault, in _indirectArgsHandle, BufferID.ShinobuBoidIndirectArgs, 1, out indirectArgs) &&
                   TryOpenVaultView(vault, in _spatialHashBucketHeadHandle, BufferID.ShinobuSpatialHashBucketHeads, SpatialHashBucketCapacity, out spatialHashBucketHeads) &&
                   TryOpenVaultView(vault, in _spatialHashNextHandle, BufferID.ShinobuSpatialHashNext, entityCapacity + sectorCapacity, out spatialHashNext);
        }

        private bool TryResolveFrameJobBuffers(
            IDataVault vault,
            out NativeArray<AmbientEntityDTO> entities,
            out NativeArray<AmbientEntityAupDTO> aups,
            out NativeArray<BoidStateDTO> boidStates,
            out NativeArray<AmbientEntityDTO> entitySnapshot,
            out NativeArray<AmbientEntityAupDTO> aupSnapshot,
            out NativeArray<BoidStateDTO> boidStateSnapshot,
            out NativeArray<int> counters,
            out NativeArray<int> spatialHashBucketHeads,
            out NativeArray<int> spatialHashNext)
        {
            entities = default;
            aups = default;
            boidStates = default;
            entitySnapshot = default;
            aupSnapshot = default;
            boidStateSnapshot = default;
            counters = default;
            spatialHashBucketHeads = default;
            spatialHashNext = default;

            if (!TryOpenVaultView(vault, in _entityHandle, BufferID.ShinobuAmbientEntities, entityCapacity, out entities) ||
                !TryOpenVaultView(vault, in _aupHandle, BufferID.ShinobuAmbientAups, entityCapacity, out aups) ||
                !TryOpenVaultView(vault, in _boidStateHandle, BufferID.ShinobuBoidStates, entityCapacity, out boidStates) ||
                !TryOpenVaultView(vault, in _counterHandle, BufferID.ShinobuEcosystemCounters, CounterCapacity, out counters))
            {
                return false;
            }

            if (!_entitySnapshotJobScratch.IsCreated ||
                !_aupSnapshotJobScratch.IsCreated ||
                !_boidStateSnapshotJobScratch.IsCreated ||
                !_spatialHashBucketHeadJobScratch.IsCreated ||
                !_spatialHashNextJobScratch.IsCreated ||
                _entitySnapshotJobScratch.Length < entityCapacity ||
                _aupSnapshotJobScratch.Length < entityCapacity ||
                _boidStateSnapshotJobScratch.Length < entityCapacity ||
                _spatialHashBucketHeadJobScratch.Length < SpatialHashBucketCapacity ||
                _spatialHashNextJobScratch.Length < entityCapacity + sectorCapacity)
            {
                return false;
            }

            entitySnapshot = _entitySnapshotJobScratch;
            aupSnapshot = _aupSnapshotJobScratch;
            boidStateSnapshot = _boidStateSnapshotJobScratch;
            spatialHashBucketHeads = _spatialHashBucketHeadJobScratch;
            spatialHashNext = _spatialHashNextJobScratch;
            return true;
        }

        private bool TryResolveMacroJobBuffers(
            IDataVault vault,
            out NativeArray<AmbientEntityDTO> entities,
            out NativeArray<AmbientEntityAupDTO> aups,
            out NativeArray<EcosystemSectorDTO> sectors,
            out NativeArray<int> counters,
            out NativeArray<int> spatialHashBucketHeads,
            out NativeArray<int> spatialHashNext)
        {
            entities = default;
            aups = default;
            sectors = default;
            counters = default;
            spatialHashBucketHeads = default;
            spatialHashNext = default;

            if (!TryOpenVaultView(vault, in _entityHandle, BufferID.ShinobuAmbientEntities, entityCapacity, out entities) ||
                !TryOpenVaultView(vault, in _aupHandle, BufferID.ShinobuAmbientAups, entityCapacity, out aups) ||
                !TryOpenVaultView(vault, in _sectorHandle, BufferID.ShinobuEcosystemSectors, sectorCapacity, out sectors) ||
                !TryOpenVaultView(vault, in _counterHandle, BufferID.ShinobuEcosystemCounters, CounterCapacity, out counters))
            {
                return false;
            }

            if (!_spatialHashBucketHeadJobScratch.IsCreated ||
                !_spatialHashNextJobScratch.IsCreated ||
                _spatialHashBucketHeadJobScratch.Length < SpatialHashBucketCapacity ||
                _spatialHashNextJobScratch.Length < entityCapacity + sectorCapacity)
            {
                return false;
            }

            spatialHashBucketHeads = _spatialHashBucketHeadJobScratch;
            spatialHashNext = _spatialHashNextJobScratch;
            return true;
        }

        private bool TryResolveBuffers(
            IDataVault vault,
            out NativeArray<AmbientEntityDTO> entities,
            out NativeArray<AmbientEntityAupDTO> aups,
            out NativeArray<EcosystemSectorDTO> sectors,
            out NativeArray<ShinobuEcosystemTuning> tuning,
            out NativeArray<int> counters,
            out NativeArray<EcosystemTelemetryEntry> telemetry,
            out NativeArray<ShinobuSpatialHashDebugCell> debugCells,
            out NativeArray<BoidMatrixDTO> matrices,
            out NativeArray<BoidCustomDataDTO> customData)
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

        private bool TryResolveFrameSpatialGridBuffers(
            out NativeArray<SpatialGridEntryDTO> entries,
            out NativeArray<SpatialGridEntryDTO> sortScratch,
            out NativeArray<SpatialGridBucketRangeDTO> bucketRanges)
        {
            entries = default;
            sortScratch = default;
            bucketRanges = default;

            if (!_spatialGridEntryJobScratch.IsCreated ||
                _spatialGridEntryJobScratch.Length < entityCapacity ||
                !_spatialGridBucketRangeJobScratch.IsCreated ||
                _spatialGridBucketRangeJobScratch.Length < SpatialGridBucketRangeCapacity ||
                !_spatialGridSortJobScratch.IsCreated ||
                _spatialGridSortJobScratch.Length < entityCapacity)
            {
                return false;
            }

            entries = _spatialGridEntryJobScratch;
            sortScratch = _spatialGridSortJobScratch;
            bucketRanges = _spatialGridBucketRangeJobScratch;
            return true;
        }

        private bool TryResolveFrameFlockingBuffers(
            out NativeArray<FlockingThreatDTO> threats,
            out NativeArray<int> threatCount,
            out NativeArray<FlockingCounter64> counters)
        {
            threats = default;
            threatCount = default;
            counters = default;

            if (!_flockingThreatJobSnapshot.IsCreated ||
                !_flockingThreatCountJobSnapshot.IsCreated ||
                !_flockingCounterJobScratch.IsCreated)
            {
                return false;
            }

            threats = _flockingThreatJobSnapshot;
            threatCount = _flockingThreatCountJobSnapshot;
            counters = _flockingCounterJobScratch;
            return true;
        }

        private bool TryPinFrameJobBuffers(IDataVault vault)
        {
            if (vault == null || _jobBufferPinsHeld || vault.IsCompactionFenceActive)
                return false;

            bool pinned = false;
            try
            {
                _jobBufferPinVault = vault;
                if (!TryLockJobBuffer(vault, BufferID.ShinobuAmbientEntities, JobPinEntities) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuAmbientAups, JobPinAups) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuBoidStates, JobPinBoidStates) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuAmbientEntitySnapshot, JobPinEntitySnapshot) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuAmbientAupSnapshot, JobPinAupSnapshot) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuBoidStateSnapshot, JobPinBoidStateSnapshot) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuEcosystemCounters, JobPinCounters) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuSpatialHashBucketHeads, JobPinSpatialHashBucketHeads) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuSpatialHashNext, JobPinSpatialHashNext) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuSpatialGridEntries, JobPinSpatialGridEntries) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuSpatialGridSortScratch, JobPinSpatialGridSortScratch) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuSpatialGridBucketRanges, JobPinSpatialGridBucketRanges))
                {
                    return false;
                }

                _jobBufferPinsHeld = true;
                pinned = true;
                return true;
            }
            finally
            {
                if (!pinned)
                    ReleaseActiveJobBufferPins(vault);
            }
        }

        private bool TryPinMacroJobBuffers(IDataVault vault)
        {
            if (vault == null || _jobBufferPinsHeld || vault.IsCompactionFenceActive)
                return false;

            bool pinned = false;
            try
            {
                _jobBufferPinVault = vault;
                if (!TryLockJobBuffer(vault, BufferID.ShinobuAmbientEntities, JobPinEntities) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuAmbientAups, JobPinAups) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuEcosystemSectors, JobPinSectors) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuEcosystemCounters, JobPinCounters) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuSpatialHashBucketHeads, JobPinSpatialHashBucketHeads) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuSpatialHashNext, JobPinSpatialHashNext))
                {
                    return false;
                }

                _jobBufferPinsHeld = true;
                pinned = true;
                return true;
            }
            finally
            {
                if (!pinned)
                    ReleaseActiveJobBufferPins(vault);
            }
        }

        private void ReleaseActiveJobBufferPins(IDataVault vault)
        {
            IDataVault guardVault = _jobBufferPinVault ?? vault;
            uint pinMask = _jobBufferPinMask;
            _jobBufferPinsHeld = false;
            _jobBufferPinMask = 0u;
            _jobBufferPinVault = null;

            if (guardVault == null || pinMask == 0u)
                return;

            TryUnlockJobBuffer(guardVault, pinMask, JobPinSpatialGridBucketRanges, BufferID.ShinobuSpatialGridBucketRanges);
            TryUnlockJobBuffer(guardVault, pinMask, JobPinSpatialGridSortScratch, BufferID.ShinobuSpatialGridSortScratch);
            TryUnlockJobBuffer(guardVault, pinMask, JobPinSpatialGridEntries, BufferID.ShinobuSpatialGridEntries);
            TryUnlockJobBuffer(guardVault, pinMask, JobPinSpatialHashNext, BufferID.ShinobuSpatialHashNext);
            TryUnlockJobBuffer(guardVault, pinMask, JobPinSpatialHashBucketHeads, BufferID.ShinobuSpatialHashBucketHeads);
            TryUnlockJobBuffer(guardVault, pinMask, JobPinCounters, BufferID.ShinobuEcosystemCounters);
            TryUnlockJobBuffer(guardVault, pinMask, JobPinSectors, BufferID.ShinobuEcosystemSectors);
            TryUnlockJobBuffer(guardVault, pinMask, JobPinBoidStateSnapshot, BufferID.ShinobuBoidStateSnapshot);
            TryUnlockJobBuffer(guardVault, pinMask, JobPinAupSnapshot, BufferID.ShinobuAmbientAupSnapshot);
            TryUnlockJobBuffer(guardVault, pinMask, JobPinEntitySnapshot, BufferID.ShinobuAmbientEntitySnapshot);
            TryUnlockJobBuffer(guardVault, pinMask, JobPinBoidStates, BufferID.ShinobuBoidStates);
            TryUnlockJobBuffer(guardVault, pinMask, JobPinAups, BufferID.ShinobuAmbientAups);
            TryUnlockJobBuffer(guardVault, pinMask, JobPinEntities, BufferID.ShinobuAmbientEntities);
        }

        private bool TryLockJobBuffer(IDataVault vault, BufferID bufferId, uint pinBit)
        {
            if ((_jobBufferPinMask & pinBit) != 0u)
                return true;

            if (vault == null ||
                (_jobBufferPinVault != null && !ReferenceEquals(_jobBufferPinVault, vault)) ||
                !vault.TryLockBuffer(bufferId, SystemID.AIEcology))
            {
                return false;
            }

            _jobBufferPinVault = vault;
            _jobBufferPinMask |= pinBit;
            return true;
        }

        private static void TryUnlockJobBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, SystemID.AIEcology);
        }

        private static bool TryAcquireInitialPopulationMutationGuard(IDataVault vault)
        {
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryAcquireMutationGuard(InitialPopulationMutationGuardMask);
        }

        private static void ReleaseInitialPopulationMutationGuard(IDataVault vault)
        {
            vault?.ReleaseMutationGuard(InitialPopulationMutationGuardMask);
        }

        private static ulong ShinobuMutationGuardBit(BufferID bufferId)
        {
            int bitIndex = unchecked((int)((uint)(int)bufferId & 31u));
            return 1UL << bitIndex;
        }

        private static bool TryAcquireEcosystemMutationGuard(IDataVault vault, BufferID bufferId)
        {
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryAcquireMutationGuard(ShinobuMutationGuardBit(bufferId));
        }

        private static void ReleaseEcosystemMutationGuard(IDataVault vault, BufferID bufferId)
        {
            vault?.ReleaseMutationGuard(ShinobuMutationGuardBit(bufferId));
        }

        private void EnsureProfilesLoaded(IDataVault vault)
        {
            if (!TryReadCounterValue(vault, CounterProfileLoaded, out int profileLoaded) ||
                profileLoaded != 0)
                return;

            ShinobuEcosystemTuning profile;
            if (!TryLoadLegacyProfile(out profile))
                profile = CreateEmergencyMockProfile();

            if (TryWriteEcosystemTuning(vault, ShinobuEcosystemTuning.Sanitize(profile)))
                TryWriteCounterValue(vault, CounterProfileLoaded, 1);
        }

        private bool TryLoadLegacyProfile(out ShinobuEcosystemTuning profile)
        {
            profile = ShinobuEcosystemTuning.CreateDefault();
            try
            {
                string profilePath = TryFindLegacyProfilePath();
                if (profilePath == null || profilePath.Length == 0 || !File.Exists(profilePath))
                    return false;

                byte[] scratch = EnsureEditorByteScratch(ref _ecosystemLegacyManagedScratch, LegacyProfileReadBytes);
                int bytesRead = LoadFileIntoManagedScratch(profilePath, scratch, LegacyProfileReadBytes, FileShare.Read);

                if (bytesRead < 24)
                    return false;

                ReadOnlySpan<byte> data = scratch.AsSpan(0, bytesRead);
                profile.SeparationWeight = ReadFloatLE(data, 0, profile.SeparationWeight);
                profile.AlignmentWeight = ReadFloatLE(data, 4, profile.AlignmentWeight);
                profile.CohesionWeight = ReadFloatLE(data, 8, profile.CohesionWeight);
                profile.PredatorAvoidanceWeight = ReadFloatLE(data, 12, profile.PredatorAvoidanceWeight);
                profile.HerbivoreBirthRate = ReadFloatLE(data, 16, profile.HerbivoreBirthRate);
                profile.CarnivoreDeathRate = ReadFloatLE(data, 20, profile.CarnivoreDeathRate);
                profile.Flags = TuningFlagLegacyBinary;
                _runtimeFlags &= ~TuningFlagEmergencyMock;
                _runtimeFlags |= TuningFlagLegacyBinary;
                return true;
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
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static int LoadFileIntoManagedScratch(string path, byte[] scratch, int maxBytes, FileShare share)
        {
            if (scratch == null || path == null || path.Length == 0)
                return 0;

            int limit = math.min(math.max(0, maxBytes), scratch.Length);
            if (limit <= 0)
                return 0;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, share, math.max(1, limit), FileOptions.SequentialScan))
            {
                return stream.Read(scratch, 0, limit);
            }
        }

        private ShinobuEcosystemTuning CreateEmergencyMockProfile()
        {
            ShinobuEcosystemTuning profile = ShinobuEcosystemTuning.CreateDefault();
            profile.Flags = TuningFlagEmergencyMock;
            _runtimeFlags |= TuningFlagEmergencyMock;
            _runtimeFlags &= ~TuningFlagLegacyBinary;
            return profile;
        }

        private void EnsureInitialPopulation(IDataVault vault)
        {
            if (!TryAcquireInitialPopulationMutationGuard(vault))
                return;

            try
            {
                if (!TryOpenVaultView(vault, in _counterHandle, BufferID.ShinobuEcosystemCounters, CounterCapacity, out NativeArray<int> counters) ||
                    counters.Length <= CounterInitialized ||
                    counters[CounterInitialized] != 0)
                {
                    return;
                }

                if (!TryOpenVaultView(vault, in _entityHandle, BufferID.ShinobuAmbientEntities, entityCapacity, out NativeArray<AmbientEntityDTO> entities) ||
                    !TryOpenVaultView(vault, in _aupHandle, BufferID.ShinobuAmbientAups, entityCapacity, out NativeArray<AmbientEntityAupDTO> aups) ||
                    !TryOpenVaultView(vault, in _boidStateHandle, BufferID.ShinobuBoidStates, entityCapacity, out NativeArray<BoidStateDTO> boidStates) ||
                    !TryOpenVaultView(vault, in _sectorHandle, BufferID.ShinobuEcosystemSectors, sectorCapacity, out NativeArray<EcosystemSectorDTO> sectors))
                {
                    return;
                }

                int count = math.min(entityCapacity, math.min(entities.Length, math.min(aups.Length, boidStates.Length)));
                if (count <= 0)
                    return;

                GenerateMockBoidSwarmJob mockJob = default;
                mockJob.Entities = entities;
                mockJob.Aups = aups;
                mockJob.BoidStates = boidStates;
                mockJob.CenterAup = _cameraAup;
                mockJob.SectorSizeMeters = math.max(1f, sectorSizeMeters);
                mockJob.SpeedMetersPerSecond = DefaultBoidSpeedMetersPerSecond;
                mockJob.ActiveCount = count;
                mockJob.BaseSeed = 0x53484E31u;
                for (int i = 0; i < count; i++)
                    mockJob.Execute(i); // COLD_BOOTSTRAP_SYNC: DataVault raw views currently share one Unity safety domain, so deterministic seed rows are written by the owner phase.

                for (int i = 0; i < sectors.Length; i++)
                    sectors[i] = default;

                counters[CounterInitialized] = 1;
            }
            finally
            {
                ReleaseInitialPopulationMutationGuard(vault);
            }
        }

        private void EnsureSpatialGridProfilesLoaded(IDataVault vault)
        {
            if (!TryReadSpatialGridTuning(vault, out SpatialGridTuningDTO tuning))
                return;

            tuning = ShinobuSpatialGridMath.Sanitize(tuning);
            if (tuning.Flags == 0u)
            {
                tuning = ShinobuSpatialGridMath.CreateDefaultTuning();
                if (!TryWriteSpatialGridTuning(vault, tuning))
                    return;
            }

#if UNITY_EDITOR
            string path = BuildSpatialGridCsvPath();
            if (path == null || path.Length == 0 || !File.Exists(path))
            {
                SpatialGridProfileDTO fallback = new SpatialGridProfileDTO
                {
                    LayerHash = 0x4641554Eu,
                    BaseGridCellSize = tuning.BaseGridCellSize,
                    MinGridCellSize = tuning.MinGridCellSize,
                    MaxGridCellSize = tuning.MaxGridCellSize,
                    MaxQueryResultsLimit = tuning.MaxQueryResultsLimit,
                    MaxProbeCount = ShinobuSpatialGridMath.ResolveProbeCount(1f),
                    Flags = 1u
                };
                TryWriteDefaultSpatialGridProfileIfEmpty(vault, fallback);

                return;
            }

            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
            if (lastWriteUtc.Ticks == _spatialGridCsvTimestampTicks)
                return;

            byte[] scratch = EnsureEditorByteScratch(ref _spatialGridCsvManagedScratch, SpatialGridCsvMaxBytes);
            int bytesRead = LoadFileIntoManagedScratch(path, scratch, SpatialGridCsvMaxBytes, FileShare.ReadWrite);
            if (bytesRead <= 0)
                return;

            SpatialGridProfileDTO[] staged = EnsureSpatialGridProfileScratch(ref _spatialGridProfileManagedScratch, SpatialGridProfileCapacity);
            int parsed = SpatialGridProfileCsv.Parse(scratch.AsSpan(0, bytesRead), staged, out SpatialGridTuningDTO parsedTuning);
            if (parsed <= 0)
                return;

            if (!TryWriteSpatialGridProfiles(vault, staged, parsed))
                return;
            if (!TryWriteSpatialGridTuning(vault, ShinobuSpatialGridMath.Sanitize(parsedTuning)))
                return;

            _spatialGridCsvTimestampTicks = lastWriteUtc.Ticks;
#endif
        }

        private void MonitorCsvOverrides(IDataVault vault)
        {
#if UNITY_EDITOR
            MonitorTuningCsvOverrides(vault);
            MonitorSwarmSpeciesProfiles(vault);
            EnsureSpatialGridProfilesLoaded(vault);
#endif
        }

#if UNITY_EDITOR
        private void MonitorTuningCsvOverrides(IDataVault vault)
        {
            try
            {
                string path = BuildTuningCsvPath();
                if (path == null || path.Length == 0 || !File.Exists(path))
                    return;

                DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
                if (lastWriteUtc.Ticks == _csvTimestampTicks)
                    return;

                byte[] scratch = EnsureEditorByteScratch(ref _ecosystemCsvManagedScratch, CsvMaxBytes);
                int bytesRead = LoadFileIntoManagedScratch(path, scratch, CsvMaxBytes, FileShare.ReadWrite);
                if (bytesRead <= 0)
                    return;

                if (!TryReadEcosystemTuning(vault, out ShinobuEcosystemTuning profile))
                    return;

                ParseCsvOverrides(scratch.AsSpan(0, bytesRead), bytesRead, ref profile);
                profile.Flags |= TuningFlagCsvOverride;
                profile = ShinobuEcosystemTuning.Sanitize(profile);
                if (!TryWriteEcosystemTuning(vault, profile))
                    return;

                TryIncrementCounterValue(vault, CounterCsvLoaded);
                _csvTimestampTicks = lastWriteUtc.Ticks;
            }
            catch (IOException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x43535646u, SourceHash, 0f);
            }
            catch (UnauthorizedAccessException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x43535646u, SourceHash, 0f);
            }
            catch (ArgumentException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x43535646u, SourceHash, 0f);
            }
            catch (NotSupportedException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x43535646u, SourceHash, 0f);
            }
            catch (InvalidOperationException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x43535646u, SourceHash, 0f);
            }
        }

        private void MonitorSwarmSpeciesProfiles(IDataVault vault)
        {
            try
            {
                string path = BuildSwarmSpeciesCsvPath();
                if (path == null || path.Length == 0 || !File.Exists(path))
                    return;

                DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
                if (lastWriteUtc.Ticks == _swarmSpeciesCsvTimestampTicks)
                    return;

                byte[] scratch = EnsureEditorByteScratch(ref _ecosystemCsvManagedScratch, CsvMaxBytes);
                int bytesRead = LoadFileIntoManagedScratch(path, scratch, CsvMaxBytes, FileShare.ReadWrite);
                if (bytesRead <= 0)
                    return;

                SwarmSpeciesProfileDTO[] staged = EnsureSwarmSpeciesScratch(ref _swarmSpeciesManagedScratch, SwarmSpeciesProfileCapacity);
                int parsed = ParseSwarmSpeciesProfiles(scratch.AsSpan(0, bytesRead), bytesRead, staged);
                if (!TryWriteSwarmSpeciesProfiles(vault, staged, parsed))
                    return;

                TryMaxCounterValue(vault, CounterProfileLoaded, parsed);
                _swarmSpeciesCsvTimestampTicks = lastWriteUtc.Ticks;
            }
            catch (IOException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x53504346u, SourceHash, 0f);
            }
            catch (UnauthorizedAccessException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x53504346u, SourceHash, 0f);
            }
            catch (ArgumentException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x53504346u, SourceHash, 0f);
            }
            catch (NotSupportedException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x53504346u, SourceHash, 0f);
            }
            catch (InvalidOperationException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x53504346u, SourceHash, 0f);
            }
        }
#endif

        private void RunMacroBiomassPass(IDataVault vault)
        {
            if (HasCanonicalMacroEcosystem(vault))
            {
                _runtimeFlags &= ~TelemetryFlagMacroPass;
                return;
            }

            if (!TryReadEcosystemTuning(vault, out ShinobuEcosystemTuning tuning))
                return;
            tuning = ShinobuEcosystemTuning.Sanitize(tuning);
            float visualQualityWeight = ResolveGlobalQualityWeight01();

            if (vault.IsCompactionFenceActive)
                return;

            if (!TryPinMacroJobBuffers(vault))
                return;

            JobHandle scheduledHandle = default;
            bool scheduledWork = false;
            try
            {
                if (!TryResolveMacroJobBuffers(
                        vault,
                        out NativeArray<AmbientEntityDTO> entities,
                        out NativeArray<AmbientEntityAupDTO> aups,
                        out NativeArray<EcosystemSectorDTO> sectors,
                        out NativeArray<int> counters,
                        out NativeArray<int> spatialHashBucketHeads,
                        out NativeArray<int> spatialHashNext))
                {
                    return;
                }

                if (vault.IsCompactionFenceActive)
                    return;

                LotkaVolterraMacroJob job = default;
                job.Entities = entities;
                job.Aups = aups;
                job.Sectors = sectors;
                job.SectorBucketHeads = spatialHashBucketHeads;
                job.SectorEntityLinks = spatialHashNext;
                job.Counters = counters;
                job.CenterAup = _cameraAup;
                job.Tuning = tuning;
                job.GlobalQualityWeight = visualQualityWeight;
                job.EntityCount = math.min(entityCapacity, math.min(entities.Length, aups.Length));
                job.SectorCount = math.min(sectorCapacity, sectors.Length);
                job.SectorSizeMeters = math.max(1f, sectorSizeMeters);
                job.DehydrationDistanceSq = dehydrationDistanceMeters * dehydrationDistanceMeters;
                job.RehydrationDistanceSq = rehydrationDistanceMeters * rehydrationDistanceMeters;
                job.ApplyLotka = (_coldTickIndex % 60) == 0 ? 1 : 0;
                job.Frame = ResolveCurrentSimulationFrame();

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
                H8Memory.RegisterActiveJob(SystemID.AIEcology, _activeJobHandle);
            }
            catch (InvalidOperationException)
            {
                if (scheduledWork)
                {
                    _activeJobHandle = scheduledHandle;
                    _scheduledPipelineKind = ScheduledPipelineMacro;
                    _jobScheduled = true;
                    H8Memory.RegisterActiveJob(SystemID.AIEcology, _activeJobHandle);
                }

                GlobalTelemetryBus.PublishPerformanceWarning(0x534D4143u, SourceHash, 0f);
            }
            finally
            {
                if (!_jobScheduled)
                    ReleaseActiveJobBufferPins(vault);
            }
        }

        private static bool HasCanonicalMacroEcosystem(IDataVault vault)
        {
            return vault != null &&
                   vault.TryGetGenerationHandle(
                       BufferID.ShinobuMacroEcosystemSectorFront,
                       out VaultGenerationHandle<MacroEcosystemSectorDTO> handle) &&
                   handle.BufferID == (uint)BufferID.ShinobuMacroEcosystemSectorFront &&
                   handle.Generation != 0u;
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
            bool publishDebugCells = pipelineKind == ScheduledPipelineFrame && _debugCellPublishPending;
            _jobScheduled = false;
            _scheduledPipelineKind = ScheduledPipelineNone;
            long completeTicks = Stopwatch.GetTimestamp();
            long elapsedTicks = completeTicks >= _scheduleTicks ? completeTicks - _scheduleTicks : 0L;
            float elapsedMs = Stopwatch.Frequency > 0
                ? (float)(elapsedTicks * 1000.0 / Stopwatch.Frequency)
                : 0f;
            _lastFlockingMs = pipelineKind == ScheduledPipelineFrame ? elapsedMs : 0f;

            IDataVault vault = _dataVault;
            bool hasEcosystemTelemetry = false;
            EcosystemTelemetryEntry ecosystemTelemetry = default;
            bool ecosystemFault = false;
            bool hasFlockingTelemetry = false;
            FlockingTelemetryEntry flockingTelemetry = default;
            bool flockingFault = false;
            bool hasSpatialTelemetry = false;
            SpatialGridTelemetryEntry spatialTelemetry = default;
            bool spatialFault = false;
            try
            {
                if (vault == null)
                    return;

                if (pipelineKind == ScheduledPipelineFrame)
                {
                    bool hasFrameTelemetry = TryBuildFrameTelemetryEntries(
                        vault,
                        out ecosystemTelemetry,
                        out ecosystemFault,
                        out flockingTelemetry,
                        out flockingFault,
                        out hasFlockingTelemetry,
                        out spatialTelemetry,
                        out hasSpatialTelemetry,
                        out spatialFault);
                    if (hasFrameTelemetry)
                        hasEcosystemTelemetry = true;
                }

                if (pipelineKind == ScheduledPipelineMacro &&
                    TryBuildMacroTelemetryEntry(vault, out ecosystemTelemetry, out ecosystemFault))
                {
                    hasEcosystemTelemetry = true;
                }
            }
            finally
            {
                ReleaseActiveJobBufferPins(vault);
                if (!publishDebugCells || vault == null)
                    _debugCellPublishPending = false;
            }

            if (vault == null)
                return;

            if (pipelineKind == ScheduledPipelineFrame)
            {
                UploadCompletedFrameToGpu(_lastActiveBudget);
                if (hasEcosystemTelemetry)
                    ecosystemTelemetry.MatrixUploadTimeMs = math.max(0f, _lastMatrixUploadMs);
                if (hasFlockingTelemetry)
                    flockingTelemetry.MatrixUploadMicroseconds = math.max(0f, _lastMatrixUploadMs) * 1000f;
                WriteRenderPayloadAfterRelease(vault, _lastActiveBudget);
                WriteFlockingCountersAfterRelease(vault);
                if (publishDebugCells && !WriteDebugCellsAfterRelease(vault) && hasEcosystemTelemetry)
                    ecosystemTelemetry.DebugCellCount = 0;
                if (hasSpatialTelemetry && !WriteSpatialGridPayloadAfterRelease(vault, _lastActiveBudget))
                    hasSpatialTelemetry = false;
                _debugCellPublishPending = false;
            }

            if (hasEcosystemTelemetry)
                WriteEcosystemTelemetryAndFaultDump(vault, in ecosystemTelemetry, ecosystemFault);
            if (hasFlockingTelemetry)
            {
                TryPublishFlockingDispersalSignal(
                    flockingTelemetry.SimulatedBoidCount,
                    flockingTelemetry.ActiveThreatCount,
                    flockingTelemetry.PanicBoidCount,
                    flockingTelemetry.GlobalQualityWeight,
                    flockingTelemetry.Frame);
                WriteFlockingTelemetryAndFaultDump(vault, in flockingTelemetry, flockingFault);
            }

            if (hasSpatialTelemetry)
                WriteSpatialGridTelemetryAndFaultDump(vault, in spatialTelemetry, spatialFault);
        }

        private bool TryBuildMacroTelemetryEntry(
            IDataVault vault,
            out EcosystemTelemetryEntry entry,
            out bool shouldDump)
        {
            entry = default;
            shouldDump = false;
            if (!TryOpenVaultView(vault, in _counterHandle, BufferID.ShinobuEcosystemCounters, CounterCapacity, out NativeArray<int> counters))
                return false;

            int active = ReadCounter(counters, CounterActive);
            int hydrated = ReadCounter(counters, CounterHydrated);
            int dehydrated = ReadCounter(counters, CounterDehydratedSectors);
            int skipped = ReadCounter(counters, CounterSkipped);
            int invalidMath = ReadCounter(counters, CounterInvalidMath);
            int overflow = ReadCounter(counters, CounterSpatialHashOverflow);
            shouldDump = invalidMath != 0 || overflow != 0;
            entry = default;
            entry.Frame = ResolveCurrentSimulationFrame();
            entry.StateHash = MixTelemetryHash(active, hydrated, dehydrated, skipped, invalidMath, overflow);
            entry.ActiveBoidCount = active;
            entry.HydratedBoidCount = hydrated;
            entry.DehydratedSectorCount = dehydrated;
            entry.SkippedBoidCount = skipped;
            entry.FlockingSolveTimeMs = 0f;
            entry.GlobalQualityWeight = _lastGlobalQualityWeight;
            entry.Flags = _runtimeFlags |
                          (invalidMath != 0 ? EntityFlagInvalidMath : 0u) |
                          (overflow != 0 ? 0x80000000u : 0u);
            entry.SpatialHashTimeMs = math.max(0f, _lastSpatialHashMs);
            entry.MatrixUploadTimeMs = 0f;
            entry.ReproducedCount = ReadCounter(counters, CounterReproduced);
            entry.TombstonedCount = ReadCounter(counters, CounterTombstoned);
            entry.DebugCellCount = ReadCounter(counters, CounterDebugCellCount);
            entry.Pad0 = 0u;
            entry.CsvLoadedCount = (ushort)math.clamp(ReadCounter(counters, CounterCsvLoaded), 0, ushort.MaxValue);
            entry.ProfileLoadedCount = (ushort)math.clamp(ReadCounter(counters, CounterProfileLoaded), 0, ushort.MaxValue);
            return true;
        }

        private void WriteEcosystemTelemetryAndFaultDump(
            IDataVault vault,
            in EcosystemTelemetryEntry entry,
            bool shouldDump)
        {
            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuEcosystemTelemetryRing))
                return;

            bool dumpAfterRelease = false;
            int dumpCursor = 0;
            try
            {
                if (!TryOpenVaultView(vault, in _telemetryHandle, BufferID.ShinobuEcosystemTelemetryRing, TelemetryCapacity, out NativeArray<EcosystemTelemetryEntry> telemetry) ||
                    telemetry.Length <= 0)
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
                telemetry[index] = entry;
                if (_ecosystemTelemetryMirror.IsCreated && _ecosystemTelemetryMirror.Length == telemetry.Length)
                    _ecosystemTelemetryMirror[index] = entry;

                if (shouldDump && !_dumpedFault)
                {
                    _dumpedFault = true;
                    dumpAfterRelease = true;
                    dumpCursor = nextCursor;
                }
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuEcosystemTelemetryRing);
            }

            if (dumpAfterRelease)
                DumpBlackBoxFromMirror(dumpCursor);
        }

        private void UploadCompletedFrameToGpu(int activeBudget)
        {
            _lastMatrixUploadMs = 0f;
            if (Application.isBatchMode)
                return;

            if (!_renderMatrixJobScratch.IsCreated || !_renderCustomDataJobScratch.IsCreated)
                return;

            BoidIndirectArgsDTO indirectArgs = BuildBoidIndirectArgs((uint)math.max(0, activeBudget));
            long startTicks = Stopwatch.GetTimestamp();
            bool uploaded = false;
            try
            {
                uploaded = _gpuUploadDispatcher.UploadFromNative(_renderMatrixJobScratch, _renderCustomDataJobScratch, in indirectArgs);
            }
            catch (InvalidOperationException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x47505555u, SourceHash, 0f);
            }
            catch (ArgumentException)
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

        private bool WriteRenderPayloadAfterRelease(IDataVault vault, int activeBudget)
        {
            if (vault == null ||
                !_renderMatrixJobScratch.IsCreated ||
                !_renderCustomDataJobScratch.IsCreated)
            {
                return false;
            }

            int count = math.min(math.max(0, activeBudget), math.min(_renderMatrixJobScratch.Length, _renderCustomDataJobScratch.Length));
            if (!WriteFrameIndirectArgsAfterRelease(vault, 0))
                return false;

            if (count <= 0)
                return true;

            if (!TryWriteRenderMatricesAfterRelease(vault, count) ||
                !TryWriteRenderCustomDataAfterRelease(vault, count))
            {
                return false;
            }

            return WriteFrameIndirectArgsAfterRelease(vault, count);
        }

        private bool WriteSpatialGridPayloadAfterRelease(IDataVault vault, int activeBudget)
        {
            if (vault == null ||
                !_spatialGridEntryJobScratch.IsCreated ||
                !_spatialGridBucketRangeJobScratch.IsCreated)
            {
                return false;
            }

            int entryCount = math.min(math.max(0, activeBudget), _spatialGridEntryJobScratch.Length);
            if (!TryInvalidateSpatialGridBucketRangesAfterRelease(vault))
                return false;

            if (entryCount <= 0)
                return false;

            if (!TryWriteFrameSnapshotsAfterRelease(vault, entryCount))
                return false;

            if (!TryWriteSpatialGridEntriesAfterRelease(vault, entryCount))
                return false;

            return TryWriteSpatialGridBucketRangesAfterRelease(vault);
        }

        private bool TryWriteFrameSnapshotsAfterRelease(IDataVault vault, int count)
        {
            return TryWriteFrameSnapshotAfterRelease(vault, in _entitySnapshotHandle, BufferID.ShinobuAmbientEntitySnapshot, _entitySnapshotJobScratch, count) &&
                   TryWriteFrameSnapshotAfterRelease(vault, in _aupSnapshotHandle, BufferID.ShinobuAmbientAupSnapshot, _aupSnapshotJobScratch, count) &&
                   TryWriteFrameSnapshotAfterRelease(vault, in _boidStateSnapshotHandle, BufferID.ShinobuBoidStateSnapshot, _boidStateSnapshotJobScratch, count);
        }

        private unsafe bool TryWriteFrameSnapshotAfterRelease<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            NativeArray<T> source,
            int count)
            where T : struct
        {
            if (vault == null ||
                !source.IsCreated ||
                count <= 0 ||
                !TryAcquireEcosystemMutationGuard(vault, bufferId))
            {
                return false;
            }

            try
            {
                if (!TryOpenVaultView(vault, in handle, bufferId, source.Length, out NativeArray<T> destination) ||
                    !destination.IsCreated)
                {
                    return false;
                }

                int copyCount = math.min(math.min(count, destination.Length), source.Length);
                if (copyCount <= 0)
                    return false;

                void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(destination);
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                UnsafeUtility.MemCpy(dst, src, (long)copyCount * UnsafeUtility.SizeOf<T>());
                return true;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, bufferId);
            }
        }

        private unsafe bool TryInvalidateSpatialGridBucketRangesAfterRelease(IDataVault vault)
        {
            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridBucketRanges))
                return false;

            try
            {
                if (!TryOpenVaultView(vault, in _spatialGridBucketRangeHandle, BufferID.ShinobuSpatialGridBucketRanges, SpatialGridBucketRangeCapacity, out NativeArray<SpatialGridBucketRangeDTO> bucketRanges) ||
                    !bucketRanges.IsCreated ||
                    bucketRanges.Length <= 0)
                {
                    return false;
                }

                void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(bucketRanges);
                UnsafeUtility.MemClear(dst, (long)bucketRanges.Length * UnsafeUtility.SizeOf<SpatialGridBucketRangeDTO>());
                return true;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridBucketRanges);
            }
        }

        private unsafe bool TryWriteSpatialGridBucketRangesAfterRelease(IDataVault vault)
        {
            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridBucketRanges))
                return false;

            try
            {
                if (!TryOpenVaultView(vault, in _spatialGridBucketRangeHandle, BufferID.ShinobuSpatialGridBucketRanges, SpatialGridBucketRangeCapacity, out NativeArray<SpatialGridBucketRangeDTO> bucketRanges) ||
                    !_spatialGridBucketRangeJobScratch.IsCreated)
                {
                    return false;
                }

                int count = math.min(bucketRanges.Length, _spatialGridBucketRangeJobScratch.Length);
                if (count <= 0)
                    return false;

                void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(bucketRanges);
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_spatialGridBucketRangeJobScratch);
                UnsafeUtility.MemCpy(dst, src, (long)count * UnsafeUtility.SizeOf<SpatialGridBucketRangeDTO>());
                return true;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridBucketRanges);
            }
        }

        private unsafe bool TryWriteSpatialGridEntriesAfterRelease(IDataVault vault, int count)
        {
            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridEntries))
                return false;

            try
            {
                if (!TryOpenVaultView(vault, in _spatialGridEntryHandle, BufferID.ShinobuSpatialGridEntries, entityCapacity, out NativeArray<SpatialGridEntryDTO> entries) ||
                    !_spatialGridEntryJobScratch.IsCreated)
                {
                    return false;
                }

                int copyCount = math.min(math.min(count, entries.Length), _spatialGridEntryJobScratch.Length);
                if (copyCount <= 0)
                    return false;

                void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(entries);
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_spatialGridEntryJobScratch);
                UnsafeUtility.MemCpy(dst, src, (long)copyCount * UnsafeUtility.SizeOf<SpatialGridEntryDTO>());
                return true;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridEntries);
            }
        }

        private unsafe bool TryWriteRenderMatricesAfterRelease(IDataVault vault, int count)
        {
            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuRenderMatrices))
                return false;

            try
            {
                if (!TryOpenVaultView(vault, in _renderMatrixHandle, BufferID.ShinobuRenderMatrices, entityCapacity, out NativeArray<BoidMatrixDTO> matrices) ||
                    matrices.Length < count ||
                    !_renderMatrixJobScratch.IsCreated ||
                    _renderMatrixJobScratch.Length < count)
                {
                    return false;
                }

                void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(matrices);
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_renderMatrixJobScratch);
                UnsafeUtility.MemCpy(dst, src, (long)count * UnsafeUtility.SizeOf<BoidMatrixDTO>());
                return true;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuRenderMatrices);
            }
        }

        private unsafe bool TryWriteRenderCustomDataAfterRelease(IDataVault vault, int count)
        {
            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuRenderCustomData))
                return false;

            try
            {
                if (!TryOpenVaultView(vault, in _renderCustomDataHandle, BufferID.ShinobuRenderCustomData, entityCapacity, out NativeArray<BoidCustomDataDTO> customData) ||
                    customData.Length < count ||
                    !_renderCustomDataJobScratch.IsCreated ||
                    _renderCustomDataJobScratch.Length < count)
                {
                    return false;
                }

                void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(customData);
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_renderCustomDataJobScratch);
                UnsafeUtility.MemCpy(dst, src, (long)count * UnsafeUtility.SizeOf<BoidCustomDataDTO>());
                return true;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuRenderCustomData);
            }
        }

        private bool WriteFrameIndirectArgsAfterRelease(IDataVault vault, int activeBudget)
        {
            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuBoidIndirectArgs))
                return false;

            try
            {
                if (!TryOpenVaultView(vault, in _indirectArgsHandle, BufferID.ShinobuBoidIndirectArgs, 1, out NativeArray<BoidIndirectArgsDTO> indirectArgs) ||
                    indirectArgs.Length <= 0)
                {
                    return false;
                }

                indirectArgs[0] = BuildBoidIndirectArgs((uint)math.max(0, activeBudget));
                return true;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuBoidIndirectArgs);
            }
        }

        private static BoidIndirectArgsDTO BuildBoidIndirectArgs(uint activeBoidCount)
        {
            BoidIndirectArgsDTO args = default;
            args.VertexCountPerInstance = DefaultBoidVertexCountPerInstance;
            args.InstanceCount = activeBoidCount;
            args.StartVertex = 0u;
            args.StartInstance = 0u;
            return args;
        }

        private unsafe bool WriteDebugCellsAfterRelease(IDataVault vault)
        {
            int count = ReadDebugCellScratchCount();
            if (vault == null ||
                !_debugCellJobScratch.IsCreated)
            {
                return false;
            }

            if (!TryWriteCounterValue(vault, CounterDebugCellCount, 0))
                return false;

            if (count <= 0)
            {
                return true;
            }

            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuSpatialHashDebugCells))
                return false;

            int publishedCount = 0;
            try
            {
                if (!TryOpenVaultView(vault, in _debugCellHandle, BufferID.ShinobuSpatialHashDebugCells, DebugCellCapacity, out NativeArray<ShinobuSpatialHashDebugCell> cells))
                    return false;

                int copyCount = math.min(count, cells.Length);
                if (copyCount <= 0)
                    return false;

                int cellSize = UnsafeUtility.SizeOf<ShinobuSpatialHashDebugCell>();
                void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(cells);
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_debugCellJobScratch);
                UnsafeUtility.MemCpy(dst, src, (long)copyCount * cellSize);
                publishedCount = copyCount;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuSpatialHashDebugCells);
            }

            return TryWriteCounterValue(vault, CounterDebugCellCount, publishedCount);
        }

        private int ReadDebugCellScratchCount()
        {
            if (!_debugCellJobScratch.IsCreated ||
                !_debugCellCountJobScratch.IsCreated ||
                _debugCellCountJobScratch.Length <= 0)
            {
                return 0;
            }

            return math.clamp(_debugCellCountJobScratch[0], 0, math.min(DebugCellCapacity, _debugCellJobScratch.Length));
        }

        private bool TryBuildFrameTelemetryEntries(
            IDataVault vault,
            out EcosystemTelemetryEntry ecosystemEntry,
            out bool ecosystemFault,
            out FlockingTelemetryEntry flockingEntry,
            out bool flockingFault,
            out bool hasFlockingEntry,
            out SpatialGridTelemetryEntry spatialGridEntry,
            out bool hasSpatialGridEntry,
            out bool spatialFault)
        {
            ecosystemEntry = default;
            ecosystemFault = false;
            flockingEntry = default;
            flockingFault = false;
            hasFlockingEntry = false;
            spatialGridEntry = default;
            hasSpatialGridEntry = false;
            spatialFault = false;
            if (!TryOpenVaultView(vault, in _counterHandle, BufferID.ShinobuEcosystemCounters, CounterCapacity, out NativeArray<int> counters))
                return false;

            int active = ReadCounter(counters, CounterActive);
            int hydrated = ReadCounter(counters, CounterHydrated);
            int dehydrated = ReadCounter(counters, CounterDehydratedSectors);
            int skipped = ReadCounter(counters, CounterSkipped);
            int invalidMath = ReadCounter(counters, CounterInvalidMath);
            int overflow = ReadCounter(counters, CounterSpatialHashOverflow);
            uint stateHash = MixTelemetryHash(active, hydrated, dehydrated, skipped, invalidMath, overflow);
            bool solveOverBudget = _lastFlockingMs > TelemetryFaultThresholdMs;
            ecosystemFault = invalidMath != 0 || overflow != 0 || solveOverBudget;

            ecosystemEntry = default;
            ecosystemEntry.Frame = ResolveCurrentSimulationFrame();
            ecosystemEntry.StateHash = stateHash;
            ecosystemEntry.ActiveBoidCount = active;
            ecosystemEntry.HydratedBoidCount = hydrated;
            ecosystemEntry.DehydratedSectorCount = dehydrated;
            ecosystemEntry.SkippedBoidCount = skipped;
            ecosystemEntry.FlockingSolveTimeMs = math.max(0f, _lastFlockingMs);
            ecosystemEntry.GlobalQualityWeight = _lastGlobalQualityWeight;
            ecosystemEntry.Flags = _runtimeFlags |
                                   (invalidMath != 0 ? EntityFlagInvalidMath : 0u) |
                                   (overflow != 0 ? 0x80000000u : 0u) |
                                   (solveOverBudget ? TelemetryFlagSolveOverBudget : 0u);
            ecosystemEntry.SpatialHashTimeMs = math.max(0f, _lastSpatialHashMs);
            ecosystemEntry.MatrixUploadTimeMs = math.max(0f, _lastMatrixUploadMs);
            ecosystemEntry.ReproducedCount = ReadCounter(counters, CounterReproduced);
            ecosystemEntry.TombstonedCount = ReadCounter(counters, CounterTombstoned);
            ecosystemEntry.DebugCellCount = _debugCellPublishPending
                ? ReadDebugCellScratchCount()
                : ReadCounter(counters, CounterDebugCellCount);
            ecosystemEntry.Pad0 = 0u;
            ecosystemEntry.CsvLoadedCount = (ushort)math.clamp(ReadCounter(counters, CounterCsvLoaded), 0, ushort.MaxValue);
            ecosystemEntry.ProfileLoadedCount = (ushort)math.clamp(ReadCounter(counters, CounterProfileLoaded), 0, ushort.MaxValue);

            if (_spatialGridTelemetryFrame.IsCreated && _spatialGridTelemetryFrame.Length > 0)
            {
                spatialGridEntry = _spatialGridTelemetryFrame[0];
                uint expectedSpatialFrame = ResolveSpatialGridRangeFrame(ResolveCurrentSimulationFrame());
                if (spatialGridEntry.Frame == expectedSpatialFrame)
                {
                    hasSpatialGridEntry = true;
                    if (_flockingCounterJobScratch.IsCreated)
                    {
                        int queryCount = ReadFlockingCounter(_flockingCounterJobScratch, FlockingCounterSpatialGridQueries);
                        if (queryCount >= 0 && spatialGridEntry.QueryCount != queryCount)
                        {
                            spatialGridEntry.QueryCount = queryCount;
                            spatialGridEntry.Flags |= ShinobuSpatialGridConstants.TelemetryFlagQueryCountPatched;
                            spatialGridEntry.StateHash = ShinobuSpatialGridMath.MixStateHash(spatialGridEntry.StateHash, (uint)queryCount);
                        }
                    }

                    bool spatialGridFault = spatialGridEntry.OverflowCount != 0u || spatialGridEntry.InvalidInputCount != 0;
                    if (spatialGridFault)
                    {
                        spatialFault = true;
                        ecosystemFault = true;
                    }
                }
            }

            hasFlockingEntry = TryBuildFlockingTelemetryEntry(
                active,
                invalidMath,
                overflow,
                out flockingEntry,
                out flockingFault);
            return true;
        }

        private void WriteSpatialGridTelemetryAndFaultDump(
            IDataVault vault,
            in SpatialGridTelemetryEntry entry,
            bool shouldDump)
        {
            if (!TryAdvanceSpatialGridTelemetryCursor(vault, out int slotCursor, out int nextCursor))
            {
                return;
            }

            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridTelemetryRing))
                return;

            bool dumpAfterRelease = false;
            try
            {
                if (!TryOpenVaultView(vault, in _spatialGridTelemetryHandle, BufferID.ShinobuSpatialGridTelemetryRing, ShinobuSpatialGridConstants.TelemetryCapacity, out NativeArray<SpatialGridTelemetryEntry> telemetry) ||
                    telemetry.Length <= 0)
                {
                    return;
                }

                int index = slotCursor % telemetry.Length;
                SpatialGridTelemetryEntry storedEntry = entry;
                telemetry[index] = storedEntry;
                if (_spatialGridTelemetryMirror.IsCreated && _spatialGridTelemetryMirror.Length == telemetry.Length)
                {
                    _spatialGridTelemetryMirror[index] = storedEntry;
                    _spatialGridTelemetryMirrorCursor = nextCursor;
                    _spatialGridTelemetryMirrorValid = true;
                }

                if (shouldDump && !_dumpedSpatialGridFault)
                {
                    _dumpedSpatialGridFault = true;
                    dumpAfterRelease = true;
                }
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridTelemetryRing);
            }

            if (dumpAfterRelease &&
                (!_spatialGridTelemetryMirror.IsCreated ||
                 _spatialGridTelemetryMirror.Length <= 0 ||
                 !_spatialGridTelemetryMirrorValid ||
                 !ShinobuSpatialGridForensics.TryQueueTelemetryDump(
                     vault,
                     in _spatialGridDumpSnapshotHandle,
                     _spatialGridTelemetryMirror,
                     _spatialGridTelemetryMirrorCursor)))
            {
                ShinobuSpatialGridForensics.RecordQueueFailure();
            }
        }

        private bool TryAdvanceSpatialGridTelemetryCursor(
            IDataVault vault,
            out int slotCursor,
            out int nextCursor)
        {
            slotCursor = 0;
            nextCursor = 0;
            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridTelemetryCursor))
                return false;

            try
            {
                if (!TryOpenVaultView(vault, in _spatialGridTelemetryCursorHandle, BufferID.ShinobuSpatialGridTelemetryCursor, 1, out NativeArray<int> cursorBuffer) ||
                    cursorBuffer.Length <= 0)
                {
                    return false;
                }

                int cursor = cursorBuffer[0];
                if (cursor < 0 || cursor >= int.MaxValue - ShinobuSpatialGridConstants.TelemetryCapacity)
                    cursor = 0;

                slotCursor = cursor;
                nextCursor = cursor + 1;
                cursorBuffer[0] = nextCursor;
                return true;
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridTelemetryCursor);
            }
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
            _flockingThreatHandle = default;
            _flockingThreatCountHandle = default;
            _flockingTelemetryHandle = default;
            _flockingCounterHandle = default;
            _debugCellHandle = default;
            _renderMatrixHandle = default;
            _renderCustomDataHandle = default;
            _indirectArgsHandle = default;
            _spatialHashBucketHeadHandle = default;
            _spatialHashNextHandle = default;
            _spatialGridEntryHandle = default;
            _spatialGridSortScratchHandle = default;
            _spatialGridBucketRangeHandle = default;
            _spatialGridTelemetryHandle = default;
            _spatialGridTelemetryCursorHandle = default;
            _spatialGridTuningHandle = default;
            _spatialGridProfileHandle = default;
            _spatialGridDumpSnapshotHandle = default;
            _ecosystemDumpSnapshotHandle = default;
            _swarmSpeciesProfileHandle = default;
        }

        private void RebindDataVaultForLifecycle(IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            ReleaseVaultStateForLifecycle(clearRenderState: false);
            _dataVault = nextVault;
        }

        private void ReleaseVaultStateForLifecycle(bool clearRenderState)
        {
            CompleteFrameJobForTeardown();
            ShinobuEcosystemTelemetryForensics.ShutdownDumpWorker();
            ShinobuSpatialGridForensics.ShutdownDumpWorker();
            ReleaseOwnedVaultHandles(_dataVault);
            DisposeTelemetryMirrorsCold();
            ClearCachedState(clearRenderState);
        }

        private void ReleaseOwnedVaultHandles(IDataVault vault)
        {
            ReleaseOwnedVaultHandle(vault, ref _entityHandle, BufferID.ShinobuAmbientEntities);
            ReleaseOwnedVaultHandle(vault, ref _aupHandle, BufferID.ShinobuAmbientAups);
            ReleaseOwnedVaultHandle(vault, ref _boidStateHandle, BufferID.ShinobuBoidStates);
            ReleaseOwnedVaultHandle(vault, ref _entitySnapshotHandle, BufferID.ShinobuAmbientEntitySnapshot);
            ReleaseOwnedVaultHandle(vault, ref _aupSnapshotHandle, BufferID.ShinobuAmbientAupSnapshot);
            ReleaseOwnedVaultHandle(vault, ref _boidStateSnapshotHandle, BufferID.ShinobuBoidStateSnapshot);
            ReleaseOwnedVaultHandle(vault, ref _sectorHandle, BufferID.ShinobuEcosystemSectors);
            ReleaseOwnedVaultHandle(vault, ref _tuningHandle, BufferID.ShinobuEcosystemTuning);
            ReleaseOwnedVaultHandle(vault, ref _counterHandle, BufferID.ShinobuEcosystemCounters);
            ReleaseOwnedVaultHandle(vault, ref _telemetryHandle, BufferID.ShinobuEcosystemTelemetryRing);
            ReleaseOwnedVaultHandle(vault, ref _flockingThreatHandle, BufferID.ShinobuFlockingThreats);
            ReleaseOwnedVaultHandle(vault, ref _flockingThreatCountHandle, BufferID.ShinobuFlockingThreatCount);
            ReleaseOwnedVaultHandle(vault, ref _flockingTelemetryHandle, BufferID.ShinobuFlockingTelemetryRing);
            ReleaseOwnedVaultHandle(vault, ref _flockingCounterHandle, BufferID.ShinobuFlockingCounters64);
            ReleaseOwnedVaultHandle(vault, ref _debugCellHandle, BufferID.ShinobuSpatialHashDebugCells);
            ReleaseOwnedVaultHandle(vault, ref _renderMatrixHandle, BufferID.ShinobuRenderMatrices);
            ReleaseOwnedVaultHandle(vault, ref _renderCustomDataHandle, BufferID.ShinobuRenderCustomData);
            ReleaseOwnedVaultHandle(vault, ref _indirectArgsHandle, BufferID.ShinobuBoidIndirectArgs);
            ReleaseOwnedVaultHandle(vault, ref _spatialHashBucketHeadHandle, BufferID.ShinobuSpatialHashBucketHeads);
            ReleaseOwnedVaultHandle(vault, ref _spatialHashNextHandle, BufferID.ShinobuSpatialHashNext);
            ReleaseOwnedVaultHandle(vault, ref _spatialGridEntryHandle, BufferID.ShinobuSpatialGridEntries);
            ReleaseOwnedVaultHandle(vault, ref _spatialGridSortScratchHandle, BufferID.ShinobuSpatialGridSortScratch);
            ReleaseOwnedVaultHandle(vault, ref _spatialGridBucketRangeHandle, BufferID.ShinobuSpatialGridBucketRanges);
            ReleaseOwnedVaultHandle(vault, ref _spatialGridTelemetryHandle, BufferID.ShinobuSpatialGridTelemetryRing);
            ReleaseOwnedVaultHandle(vault, ref _spatialGridTelemetryCursorHandle, BufferID.ShinobuSpatialGridTelemetryCursor);
            ReleaseOwnedVaultHandle(vault, ref _spatialGridTuningHandle, BufferID.ShinobuSpatialGridTuning);
            ReleaseOwnedVaultHandle(vault, ref _spatialGridProfileHandle, BufferID.ShinobuSpatialGridProfiles);
            ReleaseOwnedVaultHandle(vault, ref _spatialGridDumpSnapshotHandle, BufferID.ShinobuSpatialGridDumpSnapshot);
            ReleaseOwnedVaultHandle(vault, ref _ecosystemDumpSnapshotHandle, BufferID.ShinobuEcosystemDumpSnapshot);
            ReleaseOwnedVaultHandle(vault, ref _swarmSpeciesProfileHandle, BufferID.ShinobuSwarmSpeciesProfiles);
        }

        private static void ReleaseOwnedVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID expectedBufferId)
            where T : struct
        {
            if (vault != null && IsOwnedVaultHandle(in handle, expectedBufferId))
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private void ClearCachedState(bool clearRenderState)
        {
            _dataVault = null;
            _vaultBuffersReady = false;
            ResetVaultHandles();
            _telemetryCursor = 0;
            _coldTickIndex = 0;
            _simulationFrameCounter = 0u;
            _lastFlockingDispersalSignalFrame = 0u;
            _spatialGridRangeEpoch = BumpSpatialGridRangeEpoch(_spatialGridRangeEpoch);
            _lastFlockingMs = 0f;
            _lastSpatialHashMs = 0f;
            _lastMatrixUploadMs = 0f;
            _lastGlobalQualityWeight = 0f;
            _lastActiveBudget = 0;
            _scheduleTicks = 0L;
            _flockingTelemetryCursor = 0;
            _spatialGridTelemetryMirrorCursor = 0;
            _csvTimestampTicks = 0L;
            _swarmSpeciesCsvTimestampTicks = 0L;
            _spatialGridCsvTimestampTicks = 0L;
            _runtimeFlags = 0u;
            _dumpedFault = false;
            _dumpedFlockingFault = false;
            _dumpedSpatialGridFault = false;
            _spatialGridTelemetryMirrorValid = false;
            _debugCellPublishPending = false;
            _ecosystemLegacyManagedScratch = null;
#if UNITY_EDITOR
            _ecosystemCsvManagedScratch = null;
            _spatialGridCsvManagedScratch = null;
            _swarmSpeciesManagedScratch = null;
            _spatialGridProfileManagedScratch = null;
#endif
            if (clearRenderState)
            {
                _proceduralRenderEnabled = false;
                _proceduralRenderMaterial = null;
                _proceduralRenderBounds = default;
                _proceduralRenderLayer = 0;
            }
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
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AuthoritativeQualityWeight);

            return MathLodApproximation.SaturateFinite(HomeostasisBrain.GlobalQualityWeight, AuthoritativeQualityWeight);
        }

        private static int ResolveActiveEntityBudget(int capacity)
        {
            return math.max(0, capacity);
        }

        private static int ResolveNeighborSampleBudget(int maxSamples, float globalQualityWeight)
        {
            int safeMax = math.max(1, maxSamples);
            return math.clamp((int)math.round(math.lerp(4f, 32f, math.saturate(globalQualityWeight))), 1, safeMax);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ResolveNeighborCellProbeBudget(float globalQualityWeight, int maxOpenAddressProbeCount)
        {
            float q = Smooth01(math.saturate(globalQualityWeight));
            int qualityTarget = (int)math.round(math.lerp(8f, 96f, q));
            int probeCap = math.clamp(math.max(1, maxOpenAddressProbeCount) * 4, 8, 128);
            return math.clamp(qualityTarget, 1, probeCap);
        }

        private static int ResolveSpatialHashChainBudget(int maxChainSteps, float globalQualityWeight)
        {
            int safeMax = math.max(1, maxChainSteps);
            return math.clamp((int)math.round(math.lerp(8f, safeMax, math.saturate(globalQualityWeight))), 1, safeMax);
        }

        private static int ResolveUpdateStride(float globalQualityWeight, float systemStress01)
        {
            float q = math.saturate(globalQualityWeight);
            float stress = Smooth01(math.saturate(systemStress01));
            float stride = math.lerp(8f, 1f, q) + math.lerp(0f, 4f, stress);
            return math.clamp((int)math.round(stride), 1, 12);
        }

        private uint AdvanceSimulationFrame()
        {
            uint next = _simulationFrameCounter + 1u;
            if (next == 0u)
            {
                next = 1u;
                _spatialGridRangeEpoch = BumpSpatialGridRangeEpoch(_spatialGridRangeEpoch);
            }

            _simulationFrameCounter = next;
            return next;
        }

        private uint ResolveSpatialGridRangeFrame(uint simulationFrame)
        {
            uint stamp = simulationFrame ^ _spatialGridRangeEpoch;
            return stamp != 0u ? stamp : 1u;
        }

        private static uint BumpSpatialGridRangeEpoch(uint current)
        {
            uint seed = current != 0u ? current : 0xA3010001u;
            uint next = ShinobuSpatialGridMath.MixStateHash(seed, 0x9E3779B9u);
            return next != 0u ? next : 0xA3010001u;
        }

        private unsafe void ClearSpatialGridRangeTable(IDataVault vault)
        {
            if (!TryAcquireEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridBucketRanges))
            {
                return;
            }

            try
            {
                if (!TryOpenVaultView(vault, in _spatialGridBucketRangeHandle, BufferID.ShinobuSpatialGridBucketRanges, SpatialGridBucketRangeCapacity, out NativeArray<SpatialGridBucketRangeDTO> bucketRanges) ||
                    !bucketRanges.IsCreated ||
                    bucketRanges.Length <= 0)
                {
                    return;
                }

                void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(bucketRanges);
                UnsafeUtility.MemClear(dst, (long)bucketRanges.Length * UnsafeUtility.SizeOf<SpatialGridBucketRangeDTO>());
            }
            finally
            {
                ReleaseEcosystemMutationGuard(vault, BufferID.ShinobuSpatialGridBucketRanges);
            }
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
            float q = Smooth01(math.saturate(globalQualityWeight));
            return math.lerp(0.15f, 1f, q);
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

            UnityEngine.Graphics.DrawProceduralIndirect(
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

        private string TryFindLegacyProfilePath()
        {
            string root = BuildProjectRootForIo();
            string archivePath = Path.Combine(root, "Docs", "Archive");
            string streamingPath = Path.Combine(Application.dataPath, "StreamingAssets");
            string directStreamingPath = Path.Combine(root, "StreamingAssets");
            string path = FindFileRecursive(archivePath, LegacyBoidProfileFile);
            if (path != null && path.Length != 0)
                return path;
            path = FindFileRecursive(archivePath, LegacyFaunaCapsFile);
            if (path != null && path.Length != 0)
                return path;
            path = FindFileRecursive(streamingPath, LegacyBoidProfileFile);
            if (path != null && path.Length != 0)
                return path;
            path = FindFileRecursive(directStreamingPath, LegacyBoidProfileFile);
            if (path != null && path.Length != 0)
                return path;
            return null;
        }

        private static string FindFileRecursive(string root, string fileName)
        {
            try
            {
                if (root == null || root.Length == 0 || !Directory.Exists(root))
                    return null;

                string[] files = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                return files != null && files.Length > 0 ? files[0] : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

#if UNITY_EDITOR
        private static string BuildSwarmSpeciesCsvPath()
        {
            string root = BuildProjectRootForIo();
            string path = Path.Combine(root, SwarmSpeciesCsvPrecomputedRelativePath);
            if (File.Exists(path))
                return path;
            path = Path.Combine(root, SwarmSpeciesCsvRelativePath);
            if (File.Exists(path))
                return path;
            path = Path.Combine(root, LegacySwarmSpeciesCsvPrecomputedRelativePath);
            if (File.Exists(path))
                return path;
            path = Path.Combine(root, LegacySwarmSpeciesCsvRelativePath);
            return File.Exists(path) ? path : null;
        }

        private static string BuildTuningCsvPath()
        {
            string root = BuildProjectRootForIo();
            string path = Path.Combine(root, CsvPrecomputedRelativePath);
            if (File.Exists(path))
                return path;
            path = Path.Combine(root, CsvRelativePath);
            return File.Exists(path) ? path : null;
        }

        private static string BuildSpatialGridCsvPath()
        {
            string root = BuildProjectRootForIo();
            string path = Path.Combine(root, SpatialGridCsvPrecomputedRelativePath);
            if (File.Exists(path))
                return path;
            path = Path.Combine(root, SpatialGridCsvRelativePath);
            return File.Exists(path) ? path : null;
        }
#endif

        private static string BuildProjectRootForIo()
        {
            string assetsPath = Application.dataPath;
            DirectoryInfo parent = Directory.GetParent(assetsPath);
            return parent != null ? parent.FullName : assetsPath;
        }

        private void DumpBlackBoxFromMirror(int cursor)
        {
            if (!_ecosystemTelemetryMirror.IsCreated)
            {
                ShinobuEcosystemTelemetryForensics.RecordQueueFailure();
                GlobalTelemetryBus.PublishPerformanceWarning(0x444D5046u, SourceHash, 0f);
                return;
            }

            DumpBlackBox(_ecosystemTelemetryMirror, cursor);
        }

        private void DumpBlackBox(NativeArray<EcosystemTelemetryEntry> telemetry, int cursor)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !ShinobuEcosystemTelemetryForensics.TryQueueTelemetryDump(
                    vault,
                    in _ecosystemDumpSnapshotHandle,
                    telemetry,
                    cursor))
            {
                ShinobuEcosystemTelemetryForensics.RecordQueueFailure();
                GlobalTelemetryBus.PublishPerformanceWarning(0x444D5046u, SourceHash, 0f);
            }
        }

#if UNITY_EDITOR
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

        private static void ParseCsvOverrides(ReadOnlySpan<byte> bytes, int length, ref ShinobuEcosystemTuning tuning)
        {
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
            ReadOnlySpan<byte> bytes,
            int length,
            Span<SwarmSpeciesProfileDTO> profiles)
        {
            if (profiles.Length <= 0)
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

        private static int ReadCsvCell(ReadOnlySpan<byte> bytes, ref int cursor, int length)
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

        private static int SkipLine(ReadOnlySpan<byte> bytes, int cursor, int length)
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

        private static uint HashAsciiKey(ReadOnlySpan<byte> bytes, int start, int end)
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

        private static bool TryParseFloatAscii(ReadOnlySpan<byte> bytes, int start, int end, out float value)
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
#endif

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

        private static float ReadFloatLE(ReadOnlySpan<byte> bytes, int offset, float fallback)
        {
            if (offset < 0 || offset > bytes.Length - 4)
                return fallback;

            float value = math.asfloat(BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4)));
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
            return ToFiniteLocalFloat3(delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 ToFiniteLocalFloat3(double3 delta)
        {
            return TryToFiniteLocalFloat3(delta, out float3 local) ? local : float3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryToFiniteLocalFloat3(double3 delta, out float3 local)
        {
            if (!math.all(math.isfinite(delta)))
            {
                local = float3.zero;
                return false;
            }

            local = (float3)delta;
            if (math.all(math.isfinite(local)))
                return true;

            local = float3.zero;
            return false;
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
                LocalPosition = default,
                Velocity = new float3(0f, 0f, math.max(0f, math.isfinite(speed) ? speed : 0f)),
                FlockHashID = ResolveFlockHashId(speciesHash, packIndex),
                PanicScalar = 0f
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static BoidStateDTO BuildBoidState(
            float3 localPosition,
            float3 velocity,
            uint speciesHash,
            int packIndex,
            float panicScalar)
        {
            return new BoidStateDTO
            {
                LocalPosition = math.all(math.isfinite(localPosition)) ? localPosition : default,
                Velocity = math.all(math.isfinite(velocity)) ? velocity : default,
                FlockHashID = ResolveFlockHashId(speciesHash, packIndex),
                PanicScalar = math.saturate(math.isfinite(panicScalar) ? panicScalar : 0f)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static AbsoluteUniversePosition FromAbsoluteDouble3(double3 absolute)
        {
            if (!math.all(math.isfinite(absolute)))
                return default;

            long gridX = QuantizeAupGridAxis(absolute.x);
            long gridY = QuantizeAupGridAxis(absolute.y);
            long gridZ = QuantizeAupGridAxis(absolute.z);
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
            return aup.GridX >= -AupSafeGridLimit &&
                   aup.GridX <= AupSafeGridLimit &&
                   aup.GridY >= -AupSafeGridLimit &&
                   aup.GridY <= AupSafeGridLimit &&
                   aup.GridZ >= -AupSafeGridLimit &&
                   aup.GridZ <= AupSafeGridLimit &&
                   math.isfinite(aup.LocalX) &&
                   math.isfinite(aup.LocalY) &&
                   math.isfinite(aup.LocalZ) &&
                   math.abs(aup.LocalX) <= AupLocalLimitMeters &&
                   math.abs(aup.LocalY) <= AupLocalLimitMeters &&
                   math.abs(aup.LocalZ) <= AupLocalLimitMeters;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long QuantizeAupGridAxis(double value)
        {
            if (!math.isfinite(value))
                return 0L;

            double scaled = math.floor(value / AupCellSizeMetersDouble);
            if (scaled < -AupSafeGridLimitDouble)
                return -AupSafeGridLimit;
            if (scaled > AupSafeGridLimitDouble)
                return AupSafeGridLimit;
            return (long)scaled;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FloorToIntClamped(double value)
        {
            if (!math.isfinite(value))
                return 0;

            double floored = math.floor(value);
            if (floored < int.MinValue)
                return int.MinValue;
            if (floored > int.MaxValue)
                return int.MaxValue;
            return (int)floored;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int3 ResolveSectorCoord(in AbsoluteUniversePosition aup, float sectorSize)
        {
            double3 absolute = ToAbsoluteDouble3(in aup);
            double inv = 1.0d / math.max(1.0d, sectorSize);
            return new int3(
                FloorToIntClamped(absolute.x * inv),
                FloorToIntClamped(absolute.y * inv),
                FloorToIntClamped(absolute.z * inv));
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
        internal static uint BuildFaunaAupSeed(
            in AbsoluteUniversePosition aup,
            uint worldSeed,
            uint speciesHash,
            uint rollIndex)
        {
            return Hecton8.Ecosystem.FaunaGenome64.BuildAupSeed(
                in aup,
                worldSeed,
                speciesHash,
                rollIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint BuildFaunaStableEntitySeed(uint stableSeed, uint speciesHash, uint salt)
        {
            return Hecton8.Ecosystem.FaunaGenome64.BuildStableEntitySeed(stableSeed, speciesHash, salt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong CompileFaunaGeneticMaskFromSeed(uint seed)
        {
            return Hecton8.Ecosystem.FaunaGenome64.CompileGeneticMaskFromSeed(seed);
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
    
        #region JulesLink_ExtinctionRiskIndexCalculator
        private static void JulesLink_ExtinctionRiskIndexCalculator() { _ = typeof(Hecton8.PureLogic.Systems.ExtinctionRiskIndexCalculator); }
        #endregion
}

    public static unsafe class ShinobuEcosystemTelemetryForensics
    {
        private const ulong DumpMagic = 0x414259535357524DUL;
        private const int DumpVersion = 4;
        private const int DumpHeaderBytes = 32;
        private const int DumpStateIdle = 0;
        private const int DumpStateSnapshotting = 1;
        private const int DumpStatePending = 2;
        private const int DumpStateWriting = 3;
        private const int DumpWorkerJoinMilliseconds = 500;
        private const int DumpWorkerPollMilliseconds = 100;
        private const int DumpFailureOwnerPath = 1;
        private const int DumpFailureH8Path = 2;
        private const int DumpFailureAgent1419Path = 4;
        private const int DumpFailureQueue = 8;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_105.bin";
        private const string DumpH8RelativePath = "Docs/AgentLogs/Dump_SHINOBU_105.h8dump";
        private const string Agent1419DumpRelativePath = "Docs/AgentLogs/Dump_1419_EcosystemSwarm.bin";

        public const int DumpSnapshotBytes = DumpHeaderBytes + (300 * 64);

        private static IDataVault s_dumpVault;
        private static VaultGenerationHandle<byte> s_dumpSnapshotHandle;
        private static Thread s_dumpWorker;
        private static AutoResetEvent s_dumpSignal;
        private static NativeArray<byte> s_snapshotBuffer;
#pragma warning disable CS0414
        private static string s_ownerDumpPath;
        private static string s_h8DumpPath;
        private static string s_agent1419DumpPath;
#pragma warning restore CS0414
        private static int s_dumpState;
        private static int s_stopRequested;
        private static int s_pendingByteCount;
        private static int s_lastDumpFailureFlags;
        private static int s_totalDumpWriteFailures;

        public static int LastDumpFailureFlags => Volatile.Read(ref s_lastDumpFailureFlags);

        public static int TotalDumpWriteFailures => Volatile.Read(ref s_totalDumpWriteFailures);

        public static void RecordQueueFailure()
        {
            AddDumpFailureFlags(DumpFailureQueue);
            Interlocked.Increment(ref s_totalDumpWriteFailures);
        }

        public static bool EnsureDumpWorker(
            string projectRoot,
            IDataVault vault,
            in VaultGenerationHandle<byte> snapshotHandle)
        {
            if (projectRoot == null ||
                projectRoot.Length == 0 ||
                vault == null ||
                !ValidateSnapshotHandle(in snapshotHandle))
            {
                return false;
            }

            try
            {
                if (s_dumpWorker != null && s_dumpWorker.IsAlive)
                {
                    ShutdownDumpWorker();
                    if (s_dumpWorker != null && s_dumpWorker.IsAlive)
                        return false;
                }

                s_dumpVault = vault;
                s_dumpSnapshotHandle = snapshotHandle;
                s_ownerDumpPath = null;
                s_h8DumpPath = null;
                s_agent1419DumpPath = null;
                EnsureSnapshotBuffer();

                Volatile.Write(ref s_stopRequested, 0);
                s_dumpSignal = null;
                s_dumpWorker = null;

                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (ThreadStateException)
            {
                return false;
            }
            catch (OutOfMemoryException)
            {
                return false;
            }
        }

        public static bool TryQueueTelemetryDump(
            IDataVault vault,
            in VaultGenerationHandle<byte> snapshotHandle,
            NativeArray<EcosystemTelemetryEntry> telemetry,
            int cursor)
        {
            if (!IsDumpWorkerPrepared(vault, in snapshotHandle))
                return false;

            if (vault == null ||
                !ValidateSnapshotHandle(in snapshotHandle) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0)
            {
                return false;
            }

            if (Volatile.Read(ref s_stopRequested) != 0)
                return false;

            if (Interlocked.CompareExchange(ref s_dumpState, DumpStateSnapshotting, DumpStateIdle) != DumpStateIdle)
                return false;

            if (!TryResolveSnapshotBuffer(out NativeArray<byte> snapshot))
            {
                Volatile.Write(ref s_dumpState, DumpStateIdle);
                return false;
            }

            int capacity = telemetry.Length;
            int written = math.max(0, cursor);
            int count = math.min(capacity, written);
            int start = written < capacity ? 0 : cursor % capacity;
            int entrySize = UnsafeUtility.SizeOf<EcosystemTelemetryEntry>();
            int byteCount = DumpHeaderBytes + (count * entrySize);
            if (entrySize != 64 ||
                byteCount < DumpHeaderBytes ||
                byteCount > DumpSnapshotBytes)
            {
                Volatile.Write(ref s_dumpState, DumpStateIdle);
                return false;
            }

            Span<byte> bytes = AsSpan(snapshot, DumpSnapshotBytes);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.Slice(0, 8), DumpMagic);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(8, 4), DumpVersion);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(12, 4), capacity);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(16, 4), count);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(20, 4), cursor);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(24, 4), start);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(28, 4), entrySize);

            int offset = DumpHeaderBytes;
            for (int i = 0; i < count; i++)
            {
                EcosystemTelemetryEntry entry = telemetry[(start + i) % capacity];
                ReadOnlySpan<EcosystemTelemetryEntry> entrySpan =
                    MemoryMarshal.CreateReadOnlySpan(ref entry, 1);
                MemoryMarshal.AsBytes(entrySpan).CopyTo(bytes.Slice(offset, entrySize));
                offset += entrySize;
            }

            if (byteCount < DumpSnapshotBytes)
                bytes.Slice(byteCount).Clear();

            Volatile.Write(ref s_pendingByteCount, byteCount);
            Thread.MemoryBarrier();
            Volatile.Write(ref s_lastDumpFailureFlags, 0);
            bool ownerWritten = TryWriteQueuedDumpFile(DumpRelativePath);
            bool h8Written = TryWriteQueuedDumpFile(DumpH8RelativePath);
            bool agent1419Written = TryWriteQueuedDumpFile(Agent1419DumpRelativePath);
            int failureFlags = ownerWritten ? 0 : DumpFailureOwnerPath;
            failureFlags |= h8Written ? 0 : DumpFailureH8Path;
            failureFlags |= agent1419Written ? 0 : DumpFailureAgent1419Path;
            AddDumpFailureFlags(failureFlags);
            if (failureFlags != 0)
                Interlocked.Increment(ref s_totalDumpWriteFailures);

            Volatile.Write(ref s_dumpState, DumpStateIdle);
            return ownerWritten || h8Written || agent1419Written;
        }

        public static void ShutdownDumpWorker()
        {
            Volatile.Write(ref s_stopRequested, 1);
            AutoResetEvent signal = s_dumpSignal;
            SignalDumpWorkerNoThrow(signal);

            Thread worker = s_dumpWorker;
            bool workerStopped = TryJoinDumpWorkerNoThrow(worker);

            if (!workerStopped)
                return;

            DrainPendingDump();
            s_dumpWorker = null;
            if (signal != null)
                DisposeDumpSignalNoThrow(signal);
            s_dumpSignal = null;
            s_dumpVault = null;
            s_dumpSnapshotHandle = default;
            H8Memory.Release(ref s_snapshotBuffer, SystemID.AIEcology);
            Volatile.Write(ref s_pendingByteCount, 0);
            Volatile.Write(ref s_dumpState, DumpStateIdle);
            Volatile.Write(ref s_stopRequested, 0);
        }

        private static bool SignalDumpWorkerNoThrow(AutoResetEvent signal)
        {
            if (signal == null)
                return false;

            try
            {
                signal.Set();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryJoinDumpWorkerNoThrow(Thread worker)
        {
            if (worker == null || !worker.IsAlive)
                return true;
            if (ReferenceEquals(Thread.CurrentThread, worker))
                return false;

            try
            {
                worker.Join(DumpWorkerJoinMilliseconds);
                return !worker.IsAlive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void DisposeDumpSignalNoThrow(AutoResetEvent signal)
        {
            if (signal == null)
                return;

            try
            {
                signal.Dispose();
            }
            catch (Exception)
            {
            }
        }

        private static void AddDumpFailureFlags(int flags)
        {
            if (flags == 0)
                return;

            int observed;
            int updated;
            do
            {
                observed = Volatile.Read(ref s_lastDumpFailureFlags);
                updated = observed | flags;
                if (updated == observed)
                    return;
            }
            while (Interlocked.CompareExchange(ref s_lastDumpFailureFlags, updated, observed) != observed);
        }

        private static void DumpWorkerLoop()
        {
            while (Volatile.Read(ref s_stopRequested) == 0)
            {
                AutoResetEvent signal = s_dumpSignal;
                if (signal == null)
                    return;

                try
                {
                    signal.WaitOne(DumpWorkerPollMilliseconds);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                DrainPendingDump();
            }

            DrainPendingDump();
        }

        private static void DrainPendingDump()
        {
            if (Interlocked.CompareExchange(ref s_dumpState, DumpStateWriting, DumpStatePending) != DumpStatePending)
                return;

            bool ownerWritten = TryWriteQueuedDumpFile(DumpRelativePath);
            bool h8Written = TryWriteQueuedDumpFile(DumpH8RelativePath);
            bool agent1419Written = TryWriteQueuedDumpFile(Agent1419DumpRelativePath);
            int failureFlags = ownerWritten ? 0 : DumpFailureOwnerPath;
            failureFlags |= h8Written ? 0 : DumpFailureH8Path;
            failureFlags |= agent1419Written ? 0 : DumpFailureAgent1419Path;
            AddDumpFailureFlags(failureFlags);
            if (failureFlags != 0)
                Interlocked.Increment(ref s_totalDumpWriteFailures);

            Volatile.Write(ref s_dumpState, DumpStateIdle);
        }

        private static bool TryWriteQueuedDumpFile(string path)
        {
            int byteCount = Volatile.Read(ref s_pendingByteCount);
            if (byteCount <= DumpHeaderBytes ||
                byteCount > DumpSnapshotBytes ||
                !s_snapshotBuffer.IsCreated ||
                s_snapshotBuffer.Length < byteCount)
            {
                return false;
            }

            return NativeFaultDumpWriter.TryWriteAll(path, s_snapshotBuffer, byteCount);
        }

        private static void EnsureSnapshotBuffer()
        {
            if (s_snapshotBuffer.IsCreated && s_snapshotBuffer.Length >= DumpSnapshotBytes)
                return;

            H8Memory.Release(ref s_snapshotBuffer, SystemID.AIEcology);
            if (s_snapshotBuffer.IsCreated)
                throw new InvalidOperationException($"{nameof(ShinobuEcosystemTelemetryForensics)} native release failed for {nameof(s_snapshotBuffer)}.");

            s_snapshotBuffer = H8Memory.Allocate<byte>(
                DumpSnapshotBytes,
                SystemID.AIEcology,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            if (!s_snapshotBuffer.IsCreated)
                throw new InvalidOperationException($"{nameof(ShinobuEcosystemTelemetryForensics)} native allocation failed for {nameof(s_snapshotBuffer)}.");
        }

        private static bool TryResolveSnapshotBuffer(out NativeArray<byte> snapshot)
        {
            snapshot = s_snapshotBuffer;
            return snapshot.IsCreated && snapshot.Length >= DumpSnapshotBytes;
        }

        private static bool IsDumpWorkerPrepared(
            IDataVault vault,
            in VaultGenerationHandle<byte> snapshotHandle)
        {
            return s_snapshotBuffer.IsCreated &&
                   s_snapshotBuffer.Length >= DumpSnapshotBytes &&
                   Volatile.Read(ref s_stopRequested) == 0 &&
                   s_dumpVault == vault &&
                   SameSnapshotHandle(in snapshotHandle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ValidateSnapshotHandle(in VaultGenerationHandle<byte> handle)
        {
            return handle.BufferID == (uint)BufferID.ShinobuEcosystemDumpSnapshot &&
                   handle.SystemID == (uint)SystemID.AIEcology &&
                   handle.Generation != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SameSnapshotHandle(in VaultGenerationHandle<byte> handle)
        {
            return s_dumpSnapshotHandle.BufferID == handle.BufferID &&
                   s_dumpSnapshotHandle.SystemID == handle.SystemID &&
                   s_dumpSnapshotHandle.Generation == handle.Generation &&
                   s_dumpSnapshotHandle.Flags == handle.Flags;
        }

        private static Span<byte> AsSpan(NativeArray<byte> buffer, int byteCount)
        {
            int safeCount = math.clamp(byteCount, 0, buffer.Length);
            return new Span<byte>(NativeArrayUnsafeUtility.GetUnsafePtr(buffer), safeCount);
        }

        private static ReadOnlySpan<byte> AsReadOnlySpan(NativeArray<byte> buffer, int byteCount)
        {
            int safeCount = math.clamp(byteCount, 0, buffer.Length);
            return new ReadOnlySpan<byte>(NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffer), safeCount);
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
        public int ClearArgsThreadGroupSizeX;
        public int CullThreadGroupSizeX;
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
        private GraphicsBuffer _publishedMatrixBuffer;
        private GraphicsBuffer _publishedCustomDataBuffer;
        private GraphicsBuffer _publishedVisibleIndexBuffer;
        private int _publishedActiveCount = -1;
        private bool _publishedUseVisibleIndices;

        /// <summary>
        /// Allocates or validates double-buffered GPU resources for the requested boid capacity.
        /// </summary>
        public bool EnsureGraphicsResources(int requiredCapacity)
        {
            int capacity = math.max(1, requiredCapacity);
            if (HasGraphicsResources(capacity))
            {
                return true;
            }

            ReleaseGraphicsResources();
            _capacity = NextPowerOfTwo(capacity);
            _matrixBufferA = CreateStructuredLockBuffer<BoidMatrixDTO>(_capacity); // COLD ALLOC: GraphicsBuffer[BoidMatrixDTO A] - double-buffered SHINOBU matrix upload - owner: SHINOBU_105
            _matrixBufferB = CreateStructuredLockBuffer<BoidMatrixDTO>(_capacity); // COLD ALLOC: GraphicsBuffer[BoidMatrixDTO B] - double-buffered SHINOBU matrix upload - owner: SHINOBU_105
            _customDataBufferA = CreateStructuredLockBuffer<BoidCustomDataDTO>(_capacity); // COLD ALLOC: GraphicsBuffer[BoidCustomDataDTO custom A] - double-buffered SHINOBU genetic scalar upload - owner: SHINOBU_105
            _customDataBufferB = CreateStructuredLockBuffer<BoidCustomDataDTO>(_capacity); // COLD ALLOC: GraphicsBuffer[BoidCustomDataDTO custom B] - double-buffered SHINOBU genetic scalar upload - owner: SHINOBU_105
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
                   IsValid(_customDataBufferA, _capacity, UnsafeUtility.SizeOf<BoidCustomDataDTO>()) &&
                   IsValid(_customDataBufferB, _capacity, UnsafeUtility.SizeOf<BoidCustomDataDTO>()) &&
                   IsValid(_argsBufferA, 1, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>()) &&
                   IsValid(_argsBufferB, 1, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>()) &&
                   IsValid(_visibleIndexBufferA, _capacity, UnsafeUtility.SizeOf<uint>()) &&
                   IsValid(_visibleIndexBufferB, _capacity, UnsafeUtility.SizeOf<uint>()) &&
                   IsValidByteAddress(_culledArgsBufferA, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>()) &&
                   IsValidByteAddress(_culledArgsBufferB, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>());
        }

        public bool HasGraphicsResources(int requiredCapacity)
        {
            int capacity = math.max(1, requiredCapacity);
            return _capacity >= capacity &&
                   IsValid(_matrixBufferA, _capacity, UnsafeUtility.SizeOf<BoidMatrixDTO>()) &&
                   IsValid(_matrixBufferB, _capacity, UnsafeUtility.SizeOf<BoidMatrixDTO>()) &&
                   IsValid(_customDataBufferA, _capacity, UnsafeUtility.SizeOf<BoidCustomDataDTO>()) &&
                   IsValid(_customDataBufferB, _capacity, UnsafeUtility.SizeOf<BoidCustomDataDTO>()) &&
                   IsValid(_argsBufferA, 1, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>()) &&
                   IsValid(_argsBufferB, 1, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>()) &&
                   IsValid(_visibleIndexBufferA, _capacity, UnsafeUtility.SizeOf<uint>()) &&
                   IsValid(_visibleIndexBufferB, _capacity, UnsafeUtility.SizeOf<uint>()) &&
                   IsValidByteAddress(_culledArgsBufferA, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>()) &&
                   IsValidByteAddress(_culledArgsBufferB, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>());
        }

        /// <summary>
        /// Copies native render matrices, scalar lanes, and indirect arguments into the inactive GPU buffer pair.
        /// </summary>
        public unsafe bool UploadFromNative(
            NativeArray<BoidMatrixDTO> matrices,
            NativeArray<BoidCustomDataDTO> customData,
            in BoidIndirectArgsDTO indirectArgs)
        {
            if (!matrices.IsCreated ||
                !customData.IsCreated)
            {
                return false;
            }

            BoidIndirectArgsDTO args = indirectArgs;
            int requested = math.min((int)args.InstanceCount, math.min(matrices.Length, customData.Length));
            if (requested <= 0 || args.VertexCountPerInstance == 0u || !HasGraphicsResources(requested))
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
                NativeArray<BoidCustomDataDTO> mappedCustom = customTarget.LockBufferForWrite<BoidCustomDataDTO>(0, writeCount);
                customLocked = true;
                void* customDst = NativeArrayUnsafeUtility.GetUnsafePtr(mappedCustom);
                void* customSrc = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(customData);
                UnsafeUtility.MemCpy(customDst, customSrc, (long)writeCount * UnsafeUtility.SizeOf<BoidCustomDataDTO>());
            }
            finally
            {
                if (customLocked)
                    customTarget.UnlockBufferAfterWrite<BoidCustomDataDTO>(writeCount);
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
            UnityEngine.Graphics.DrawProceduralIndirect(
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
                   IsValid(customDataBuffer, 1, UnsafeUtility.SizeOf<BoidCustomDataDTO>()) &&
                   IsValid(argsBuffer, 1, UnsafeUtility.SizeOf<BoidIndirectArgsDTO>());
        }

        /// <summary>
        /// Releases all cold-owned GPU resources.
        /// </summary>
        public void Dispose()
        {
            ReleaseGraphicsResources();
        }

        private void PublishBuffers(
            GraphicsBuffer matrixBuffer,
            GraphicsBuffer customDataBuffer,
            GraphicsBuffer visibleIndexBuffer,
            int activeCount,
            bool useVisibleIndices)
        {
            int safeActiveCount = math.max(0, activeCount);
            if (ReferenceEquals(_publishedMatrixBuffer, matrixBuffer) &&
                ReferenceEquals(_publishedCustomDataBuffer, customDataBuffer) &&
                ReferenceEquals(_publishedVisibleIndexBuffer, visibleIndexBuffer) &&
                _publishedActiveCount == safeActiveCount &&
                _publishedUseVisibleIndices == useVisibleIndices)
            {
                return;
            }

            Shader.SetGlobalBuffer(_ShinobuBoidMatricesId, matrixBuffer);
            Shader.SetGlobalBuffer(_ShinobuBoidCustomDataId, customDataBuffer);
            if (visibleIndexBuffer != null)
                Shader.SetGlobalBuffer(_ShinobuBoidVisibleIndicesId, visibleIndexBuffer);
            Shader.SetGlobalInt(_ShinobuBoidActiveCountId, safeActiveCount);
            Shader.SetGlobalInt(_ShinobuBoidUseVisibleIndicesId, useVisibleIndices ? 1 : 0);
            _publishedMatrixBuffer = matrixBuffer;
            _publishedCustomDataBuffer = customDataBuffer;
            _publishedVisibleIndexBuffer = visibleIndexBuffer;
            _publishedActiveCount = safeActiveCount;
            _publishedUseVisibleIndices = useVisibleIndices;
        }

        private bool TryDispatchVisibilityCulling(
            in ShinobuSwarmGpuCullingParams cullingParams,
            GraphicsBuffer matrixBuffer,
            GraphicsBuffer visibleIndexBuffer,
            GraphicsBuffer culledArgsBuffer,
            int activeCount)
        {
            if (!SupportsGpuWrittenIndirectArgs())
                return false;

            if (cullingParams.CullingCompute == null ||
                cullingParams.ClearArgsKernel < 0 ||
                cullingParams.CullKernel < 0 ||
                cullingParams.ClearArgsThreadGroupSizeX <= 0 ||
                cullingParams.CullThreadGroupSizeX <= 0 ||
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
            int clearGroups = CeilDividePositive(1, cullingParams.ClearArgsThreadGroupSizeX);
            if (clearGroups <= 0)
                return false;

            compute.SetBuffer(cullingParams.ClearArgsKernel, _ShinobuBoidCulledIndirectArgsId, culledArgsBuffer);
            compute.Dispatch(cullingParams.ClearArgsKernel, clearGroups, 1, 1);

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

            int groups = CeilDividePositive(sourceCount, cullingParams.CullThreadGroupSizeX);
            if (groups <= 0)
                return false;

            compute.Dispatch(cullingParams.CullKernel, groups, 1, 1);
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
            _publishedMatrixBuffer = null;
            _publishedCustomDataBuffer = null;
            _publishedVisibleIndexBuffer = null;
            _publishedActiveCount = -1;
            _publishedUseVisibleIndices = false;
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
                GraphicsBuffer.Target.IndirectArguments,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                UnsafeUtility.SizeOf<BoidIndirectArgsDTO>());
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

        private static bool SupportsGpuWrittenIndirectArgs()
        {
            // Unity 6 D3D rejects Raw | IndirectArguments buffers; keep the draw path on CPU-written args.
            return false;
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

        private static int CeilDividePositive(int value, int divisor)
        {
            const int MaxDispatchGroupsPerDimension = 65535;
            if (value <= 0 || divisor <= 0)
                return 0;

            long groups = ((long)value + divisor - 1L) / divisor;
            return groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
        }
    }

    internal static class ShinobuEcosystemLayoutManifest
    {
        private const string LayoutSizeMismatchMessage = "[ShinobuEcosystemLayoutManifest] Size mismatch";
        private const string LayoutOffsetMismatchMessage = "[ShinobuEcosystemLayoutManifest] Offset mismatch";

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
            AssertSize<BoidCustomDataDTO>(16);
            AssertSize<BoidIndirectArgsDTO>(16);
            AssertSize<SwarmSpeciesProfileDTO>(32);
            AssertSize<FlockingThreatDTO>(32);
            AssertSize<FlockingTelemetryEntry>(64);
            AssertSize<FlockingCounter64>(64);
            AssertSize<AbyssalFlowTensorDTO>(64);
            AssertSize<AmbientEntityAupDTO>(64);
            AssertSize<EcosystemSectorDTO>(32);
            AssertSize<ShinobuEcosystemTuning>(64);
            AssertSize<EcosystemTelemetryEntry>(64);
            AssertSize<MockPredatorSignal>(64);
            AssertSize<MockPredatorRuntime>(32);
            AssertSize<MockTerrainSample>(16);
            AssertSize<MockTerrainSampler>(48);
            AssertSize<ShinobuSpatialHashDebugCell>(32);
            AssertSize<SpatialGridEntryDTO>(16);
            AssertSize<SpatialGridBucketRangeDTO>(32);
            AssertSize<SpatialGridTelemetryEntry>(64);
            AssertSize<SpatialGridTuningDTO>(32);
            AssertSize<SpatialGridProfileDTO>(32);
            AssertSize<SpatialGridCell64>(24);
            AssertSize<SpatialHashQuery>(144);

            AssertOffset<AmbientEntityDTO>(nameof(AmbientEntityDTO.Position), 0);
            AssertOffset<AmbientEntityDTO>(nameof(AmbientEntityDTO.Velocity), 12);
            AssertOffset<AmbientEntityDTO>(nameof(AmbientEntityDTO.SpeciesHash), 24);
            AssertOffset<AmbientEntityDTO>(nameof(AmbientEntityDTO.Biomass), 28);
            AssertOffset<BoidStateDTO>(nameof(BoidStateDTO.LocalPosition), 0);
            AssertOffset<BoidStateDTO>(nameof(BoidStateDTO.Velocity), 12);
            AssertOffset<BoidStateDTO>(nameof(BoidStateDTO.FlockHashID), 24);
            AssertOffset<BoidStateDTO>(nameof(BoidStateDTO.PanicScalar), 28);
            AssertOffset<BoidTargetDTO>(nameof(BoidTargetDTO.AUP), 0);
            AssertOffset<BoidTargetDTO>(nameof(BoidTargetDTO.Weight01), 24);
            AssertOffset<BoidTargetDTO>(nameof(BoidTargetDTO.SpeciesID), 28);
            AssertOffset<BoidTargetDTO>(nameof(BoidTargetDTO.Flags), 30);
            AssertOffset<FlockingThreatDTO>(nameof(FlockingThreatDTO.LocalPosition), 0);
            AssertOffset<FlockingThreatDTO>(nameof(FlockingThreatDTO.RadiusMeters), 12);
            AssertOffset<FlockingThreatDTO>(nameof(FlockingThreatDTO.Intensity01), 16);
            AssertOffset<FlockingThreatDTO>(nameof(FlockingThreatDTO.SourceId), 20);
            AssertOffset<FlockingThreatDTO>(nameof(FlockingThreatDTO.TypeHash), 24);
            AssertOffset<FlockingThreatDTO>(nameof(FlockingThreatDTO.DirectionalBias), 28);
            AssertOffset<FlockingTelemetryEntry>(nameof(FlockingTelemetryEntry.Frame), 0);
            AssertOffset<FlockingTelemetryEntry>(nameof(FlockingTelemetryEntry.StateHash), 4);
            AssertOffset<FlockingTelemetryEntry>(nameof(FlockingTelemetryEntry.SimulatedBoidCount), 8);
            AssertOffset<FlockingTelemetryEntry>(nameof(FlockingTelemetryEntry.NeighborSamplesTotal), 12);
            AssertOffset<FlockingTelemetryEntry>(nameof(FlockingTelemetryEntry.AverageNeighbors), 16);
            AssertOffset<FlockingTelemetryEntry>(nameof(FlockingTelemetryEntry.ActiveThreatCount), 20);
            AssertOffset<FlockingTelemetryEntry>(nameof(FlockingTelemetryEntry.BurstExecutionMicroseconds), 24);
            AssertOffset<FlockingTelemetryEntry>(nameof(FlockingTelemetryEntry.GlobalQualityWeight), 28);
            AssertOffset<FlockingTelemetryEntry>(nameof(FlockingTelemetryEntry.Flags), 32);
            AssertOffset<FlockingTelemetryEntry>(nameof(FlockingTelemetryEntry.PanicBoidCount), 36);
            AssertOffset<FlockingTelemetryEntry>(nameof(FlockingTelemetryEntry.MaxNeighborsPerBoid), 40);
            AssertOffset<FlockingTelemetryEntry>(nameof(FlockingTelemetryEntry.SpatialHashOverflowCount), 44);
            AssertOffset<FlockingTelemetryEntry>(nameof(FlockingTelemetryEntry.InvalidMathCount), 48);
            AssertOffset<FlockingTelemetryEntry>(nameof(FlockingTelemetryEntry.SpatialHashMicroseconds), 52);
            AssertOffset<FlockingTelemetryEntry>(nameof(FlockingTelemetryEntry.MatrixUploadMicroseconds), 56);
            AssertOffset<FlockingTelemetryEntry>(nameof(FlockingTelemetryEntry.Pad0), 60);
            AssertOffset<FlockingCounter64>(nameof(FlockingCounter64.Value), 0);
            AssertOffset<FlockingCounter64>(nameof(FlockingCounter64.Pad0), 4);
            AssertOffset<FlockingCounter64>(nameof(FlockingCounter64.Pad1), 8);
            AssertOffset<FlockingCounter64>(nameof(FlockingCounter64.Pad2), 12);
            AssertOffset<FlockingCounter64>(nameof(FlockingCounter64.Pad3), 16);
            AssertOffset<FlockingCounter64>(nameof(FlockingCounter64.Pad4), 20);
            AssertOffset<FlockingCounter64>(nameof(FlockingCounter64.Pad5), 24);
            AssertOffset<FlockingCounter64>(nameof(FlockingCounter64.Pad6), 28);
            AssertOffset<FlockingCounter64>(nameof(FlockingCounter64.Pad7), 32);
            AssertOffset<FlockingCounter64>(nameof(FlockingCounter64.Pad8), 36);
            AssertOffset<FlockingCounter64>(nameof(FlockingCounter64.Pad9), 40);
            AssertOffset<FlockingCounter64>(nameof(FlockingCounter64.Pad10), 44);
            AssertOffset<FlockingCounter64>(nameof(FlockingCounter64.Pad11), 48);
            AssertOffset<FlockingCounter64>(nameof(FlockingCounter64.Pad12), 52);
            AssertOffset<FlockingCounter64>(nameof(FlockingCounter64.Pad13), 56);
            AssertOffset<FlockingCounter64>(nameof(FlockingCounter64.Pad14), 60);
            AssertOffset<BoidMatrixDTO>(nameof(BoidMatrixDTO.C0), 0);
            AssertOffset<BoidMatrixDTO>(nameof(BoidMatrixDTO.C1), 16);
            AssertOffset<BoidMatrixDTO>(nameof(BoidMatrixDTO.C2), 32);
            AssertOffset<BoidMatrixDTO>(nameof(BoidMatrixDTO.C3), 48);
            AssertOffset<BoidCustomDataDTO>(nameof(BoidCustomDataDTO.GeneticLow), 0);
            AssertOffset<BoidCustomDataDTO>(nameof(BoidCustomDataDTO.GeneticHigh), 4);
            AssertOffset<BoidCustomDataDTO>(nameof(BoidCustomDataDTO.PanicOrSkip), 8);
            AssertOffset<BoidCustomDataDTO>(nameof(BoidCustomDataDTO.QualityWeight), 12);
            AssertOffset<BoidIndirectArgsDTO>(nameof(BoidIndirectArgsDTO.VertexCountPerInstance), 0);
            AssertOffset<BoidIndirectArgsDTO>(nameof(BoidIndirectArgsDTO.InstanceCount), 4);
            AssertOffset<BoidIndirectArgsDTO>(nameof(BoidIndirectArgsDTO.StartVertex), 8);
            AssertOffset<BoidIndirectArgsDTO>(nameof(BoidIndirectArgsDTO.StartInstance), 12);
            AssertOffset<SwarmSpeciesProfileDTO>(nameof(SwarmSpeciesProfileDTO.BiomassHash), 0);
            AssertOffset<SwarmSpeciesProfileDTO>(nameof(SwarmSpeciesProfileDTO.MeshHash), 4);
            AssertOffset<SwarmSpeciesProfileDTO>(nameof(SwarmSpeciesProfileDTO.MaterialHash), 8);
            AssertOffset<SwarmSpeciesProfileDTO>(nameof(SwarmSpeciesProfileDTO.Scale), 12);
            AssertOffset<SwarmSpeciesProfileDTO>(nameof(SwarmSpeciesProfileDTO.Speed), 16);
            AssertOffset<SwarmSpeciesProfileDTO>(nameof(SwarmSpeciesProfileDTO.FearResponse), 20);
            AssertOffset<SwarmSpeciesProfileDTO>(nameof(SwarmSpeciesProfileDTO.Pad0), 24);
            AssertOffset<SwarmSpeciesProfileDTO>(nameof(SwarmSpeciesProfileDTO.SpeciesID), 28);
            AssertOffset<SwarmSpeciesProfileDTO>(nameof(SwarmSpeciesProfileDTO.Flags), 30);
            AssertOffset<AbyssalFlowTensorDTO>(nameof(AbyssalFlowTensorDTO.AxisXAndStrength), 0);
            AssertOffset<AbyssalFlowTensorDTO>(nameof(AbyssalFlowTensorDTO.AxisYAndCurl), 16);
            AssertOffset<AbyssalFlowTensorDTO>(nameof(AbyssalFlowTensorDTO.AxisZAndTurbulence), 32);
            AssertOffset<AbyssalFlowTensorDTO>(nameof(AbyssalFlowTensorDTO.LocalOriginAndQuality), 48);
            AssertOffset<AmbientEntityAupDTO>(nameof(AmbientEntityAupDTO.PositionAup), 0);
            AssertOffset<AmbientEntityAupDTO>(nameof(AmbientEntityAupDTO.Flags), 48);
            AssertOffset<AmbientEntityAupDTO>(nameof(AmbientEntityAupDTO.SectorHash), 52);
            AssertOffset<AmbientEntityAupDTO>(nameof(AmbientEntityAupDTO.SpatialCellHash), 56);
            AssertOffset<AmbientEntityAupDTO>(nameof(AmbientEntityAupDTO.StableSeed), 60);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.SectorHash), 0);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.HerbivoreMass), 4);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.CarnivoreMass), 8);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.FloraMass), 12);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.SectorX), 16);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.SectorY), 20);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.SectorZ), 24);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.Flags), 28);
            AssertOffset<ShinobuEcosystemTuning>(nameof(ShinobuEcosystemTuning.SeparationWeight), 0);
            AssertOffset<ShinobuEcosystemTuning>(nameof(ShinobuEcosystemTuning.AlignmentWeight), 4);
            AssertOffset<ShinobuEcosystemTuning>(nameof(ShinobuEcosystemTuning.CohesionWeight), 8);
            AssertOffset<ShinobuEcosystemTuning>(nameof(ShinobuEcosystemTuning.PredatorAvoidanceWeight), 12);
            AssertOffset<ShinobuEcosystemTuning>(nameof(ShinobuEcosystemTuning.HerbivoreBirthRate), 16);
            AssertOffset<ShinobuEcosystemTuning>(nameof(ShinobuEcosystemTuning.CarnivoreBirthRate), 20);
            AssertOffset<ShinobuEcosystemTuning>(nameof(ShinobuEcosystemTuning.HerbivoreDeathRate), 24);
            AssertOffset<ShinobuEcosystemTuning>(nameof(ShinobuEcosystemTuning.CarnivoreDeathRate), 28);
            AssertOffset<ShinobuEcosystemTuning>(nameof(ShinobuEcosystemTuning.FloraGrowthRate), 32);
            AssertOffset<ShinobuEcosystemTuning>(nameof(ShinobuEcosystemTuning.FeedRate), 36);
            AssertOffset<ShinobuEcosystemTuning>(nameof(ShinobuEcosystemTuning.BiomassReproductionThreshold), 40);
            AssertOffset<ShinobuEcosystemTuning>(nameof(ShinobuEcosystemTuning.MaxSpeedMetersPerSecond), 44);
            AssertOffset<ShinobuEcosystemTuning>(nameof(ShinobuEcosystemTuning.CarryingCapacity), 48);
            AssertOffset<ShinobuEcosystemTuning>(nameof(ShinobuEcosystemTuning.PredationRate), 52);
            AssertOffset<ShinobuEcosystemTuning>(nameof(ShinobuEcosystemTuning.Flags), 56);
            AssertOffset<ShinobuEcosystemTuning>(nameof(ShinobuEcosystemTuning.EvasionRadiusMeters), 60);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.Frame), 0);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.StateHash), 4);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.ActiveBoidCount), 8);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.HydratedBoidCount), 12);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.DehydratedSectorCount), 16);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.SkippedBoidCount), 20);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.FlockingSolveTimeMs), 24);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.GlobalQualityWeight), 28);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.Flags), 32);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.SpatialHashTimeMs), 36);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.MatrixUploadTimeMs), 40);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.ReproducedCount), 44);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.TombstonedCount), 48);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.DebugCellCount), 52);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.Pad0), 56);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.CsvLoadedCount), 60);
            AssertOffset<EcosystemTelemetryEntry>(nameof(EcosystemTelemetryEntry.ProfileLoadedCount), 62);
            AssertOffset<SpatialGridEntryDTO>(nameof(SpatialGridEntryDTO.EntityHashID), 0);
            AssertOffset<SpatialGridEntryDTO>(nameof(SpatialGridEntryDTO.EntityRowIndex), 0);
            AssertOffset<SpatialGridEntryDTO>(nameof(SpatialGridEntryDTO.CellHash), 4);
            AssertOffset<SpatialGridEntryDTO>(nameof(SpatialGridEntryDTO.LocalCellOffset), 8);
            AssertOffset<SpatialGridEntryDTO>(nameof(SpatialGridEntryDTO.CellFingerprint), 8);
            AssertOffset<SpatialGridBucketRangeDTO>(nameof(SpatialGridBucketRangeDTO.CellHash), 0);
            AssertOffset<SpatialGridBucketRangeDTO>(nameof(SpatialGridBucketRangeDTO.CellFingerprintX), 4);
            AssertOffset<SpatialGridBucketRangeDTO>(nameof(SpatialGridBucketRangeDTO.CellFingerprintY), 8);
            AssertOffset<SpatialGridBucketRangeDTO>(nameof(SpatialGridBucketRangeDTO.StartIndex), 12);
            AssertOffset<SpatialGridBucketRangeDTO>(nameof(SpatialGridBucketRangeDTO.Count), 16);
            AssertOffset<SpatialGridBucketRangeDTO>(nameof(SpatialGridBucketRangeDTO.Flags), 20);
            AssertOffset<SpatialGridBucketRangeDTO>(nameof(SpatialGridBucketRangeDTO.Pad0), 24);
            AssertOffset<SpatialGridBucketRangeDTO>(nameof(SpatialGridBucketRangeDTO.Pad1), 28);
            AssertOffset<SpatialGridTelemetryEntry>(nameof(SpatialGridTelemetryEntry.Frame), 0);
            AssertOffset<SpatialGridTelemetryEntry>(nameof(SpatialGridTelemetryEntry.EntityCount), 4);
            AssertOffset<SpatialGridTelemetryEntry>(nameof(SpatialGridTelemetryEntry.MaxBucketOccupancy), 8);
            AssertOffset<SpatialGridTelemetryEntry>(nameof(SpatialGridTelemetryEntry.QueryCount), 12);
            AssertOffset<SpatialGridTelemetryEntry>(nameof(SpatialGridTelemetryEntry.QuantizeMicroseconds), 16);
            AssertOffset<SpatialGridTelemetryEntry>(nameof(SpatialGridTelemetryEntry.SortMicroseconds), 20);
            AssertOffset<SpatialGridTelemetryEntry>(nameof(SpatialGridTelemetryEntry.GlobalQualityWeight), 24);
            AssertOffset<SpatialGridTelemetryEntry>(nameof(SpatialGridTelemetryEntry.CellSizeMeters), 28);
            AssertOffset<SpatialGridTelemetryEntry>(nameof(SpatialGridTelemetryEntry.OverflowCount), 32);
            AssertOffset<SpatialGridTelemetryEntry>(nameof(SpatialGridTelemetryEntry.Flags), 36);
            AssertOffset<SpatialGridTelemetryEntry>(nameof(SpatialGridTelemetryEntry.StateHash), 40);
            AssertOffset<SpatialGridTelemetryEntry>(nameof(SpatialGridTelemetryEntry.MaxProbeCount), 44);
            AssertOffset<SpatialGridTelemetryEntry>(nameof(SpatialGridTelemetryEntry.MaxQueryResults), 48);
            AssertOffset<SpatialGridTelemetryEntry>(nameof(SpatialGridTelemetryEntry.BucketRangeCount), 52);
            AssertOffset<SpatialGridTelemetryEntry>(nameof(SpatialGridTelemetryEntry.InvalidInputCount), 56);
            AssertOffset<SpatialGridTelemetryEntry>(nameof(SpatialGridTelemetryEntry.Pad1), 60);
            AssertOffset<SpatialGridTuningDTO>(nameof(SpatialGridTuningDTO.BaseGridCellSize), 0);
            AssertOffset<SpatialGridTuningDTO>(nameof(SpatialGridTuningDTO.MinGridCellSize), 4);
            AssertOffset<SpatialGridTuningDTO>(nameof(SpatialGridTuningDTO.MaxGridCellSize), 8);
            AssertOffset<SpatialGridTuningDTO>(nameof(SpatialGridTuningDTO.MaxQueryResultsLimit), 12);
            AssertOffset<SpatialGridTuningDTO>(nameof(SpatialGridTuningDTO.HashMultiplierX), 16);
            AssertOffset<SpatialGridTuningDTO>(nameof(SpatialGridTuningDTO.HashMultiplierY), 20);
            AssertOffset<SpatialGridTuningDTO>(nameof(SpatialGridTuningDTO.HashMultiplierZ), 24);
            AssertOffset<SpatialGridTuningDTO>(nameof(SpatialGridTuningDTO.Flags), 28);
            AssertOffset<SpatialGridProfileDTO>(nameof(SpatialGridProfileDTO.LayerHash), 0);
            AssertOffset<SpatialGridProfileDTO>(nameof(SpatialGridProfileDTO.BaseGridCellSize), 4);
            AssertOffset<SpatialGridProfileDTO>(nameof(SpatialGridProfileDTO.MinGridCellSize), 8);
            AssertOffset<SpatialGridProfileDTO>(nameof(SpatialGridProfileDTO.MaxGridCellSize), 12);
            AssertOffset<SpatialGridProfileDTO>(nameof(SpatialGridProfileDTO.MaxQueryResultsLimit), 16);
            AssertOffset<SpatialGridProfileDTO>(nameof(SpatialGridProfileDTO.MaxProbeCount), 20);
            AssertOffset<SpatialGridProfileDTO>(nameof(SpatialGridProfileDTO.Flags), 24);
            AssertOffset<SpatialGridProfileDTO>(nameof(SpatialGridProfileDTO.Pad0), 28);
            AssertOffset<SpatialGridCell64>(nameof(SpatialGridCell64.X), 0);
            AssertOffset<SpatialGridCell64>(nameof(SpatialGridCell64.Y), 8);
            AssertOffset<SpatialGridCell64>(nameof(SpatialGridCell64.Z), 16);
            AssertOffset<SpatialHashQuery>(nameof(SpatialHashQuery.CenterAbsolute), 0);
            AssertOffset<SpatialHashQuery>(nameof(SpatialHashQuery.EntriesHandle), 24);
            AssertOffset<SpatialHashQuery>(nameof(SpatialHashQuery.BucketRangesHandle), 40);
            AssertOffset<SpatialHashQuery>(nameof(SpatialHashQuery.AupSnapshotHandle), 56);
            AssertOffset<SpatialHashQuery>(nameof(SpatialHashQuery.TelemetryHandle), 72);
            AssertOffset<SpatialHashQuery>(nameof(SpatialHashQuery.TelemetryCursorHandle), 88);
            AssertOffset<SpatialHashQuery>(nameof(SpatialHashQuery.EntryCount), 104);
            AssertOffset<SpatialHashQuery>(nameof(SpatialHashQuery.BucketMask), 108);
            AssertOffset<SpatialHashQuery>(nameof(SpatialHashQuery.Frame), 112);
            AssertOffset<SpatialHashQuery>(nameof(SpatialHashQuery.CellSizeMeters), 116);
            AssertOffset<SpatialHashQuery>(nameof(SpatialHashQuery.HashMultiplierX), 120);
            AssertOffset<SpatialHashQuery>(nameof(SpatialHashQuery.HashMultiplierY), 124);
            AssertOffset<SpatialHashQuery>(nameof(SpatialHashQuery.HashMultiplierZ), 128);
            AssertOffset<SpatialHashQuery>(nameof(SpatialHashQuery.MaxResults), 132);
            AssertOffset<SpatialHashQuery>(nameof(SpatialHashQuery.MaxProbeCount), 136);
            AssertOffset<SpatialHashQuery>("_pad0", 140);

            _verified = true;
        }

        private static void AssertSize<T>(int expected) where T : unmanaged
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed != expected)
                Fail(LayoutSizeMismatchMessage);
        }

        private static void AssertOffset<T>(string fieldName, int expected) where T : unmanaged
        {
            int observed = (int)Marshal.OffsetOf<T>(fieldName);
            if (observed != expected)
                Fail(LayoutOffsetMismatchMessage);
        }

        private static void Fail(string message)
        {
            throw new CriticalBootException(message);
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
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float3 Velocity;
        [FieldOffset(24)] public uint FlockHashID;
        [FieldOffset(28)] public float PanicScalar;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BoidTargetDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public float Weight01;
        [FieldOffset(28)] public ushort SpeciesID;
        [FieldOffset(30)] public ushort Flags;
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
    public struct BoidCustomDataDTO
    {
        [FieldOffset(0)] public uint GeneticLow;
        [FieldOffset(4)] public uint GeneticHigh;
        [FieldOffset(8)] public float PanicOrSkip;
        [FieldOffset(12)] public float QualityWeight;
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
        [FieldOffset(12)] public float Scale;
        [FieldOffset(16)] public float Speed;
        [FieldOffset(20)] public float FearResponse;
        [FieldOffset(24)] public uint Pad0;
        [FieldOffset(28)] public ushort SpeciesID;
        [FieldOffset(30)] public ushort Flags;
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
        [FieldOffset(60)] public float EvasionRadiusMeters;

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
                EvasionRadiusMeters = 48f
            };
        }

        public static ShinobuEcosystemTuning Sanitize(ShinobuEcosystemTuning value)
        {
            ShinobuEcosystemTuning fallback = CreateDefault();
            value.SeparationWeight = SanitizePositive(value.SeparationWeight, fallback.SeparationWeight);
            value.AlignmentWeight = SanitizePositive(value.AlignmentWeight, fallback.AlignmentWeight);
            value.CohesionWeight = SanitizePositive(value.CohesionWeight, fallback.CohesionWeight);
            value.PredatorAvoidanceWeight = SanitizePositive(value.PredatorAvoidanceWeight, fallback.PredatorAvoidanceWeight);
            value.EvasionRadiusMeters = math.clamp(SanitizePositive(value.EvasionRadiusMeters, fallback.EvasionRadiusMeters), 4f, 160f);
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
    public struct EcosystemTelemetryEntry
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
        [FieldOffset(56)] public uint Pad0;
        [FieldOffset(60)] public ushort CsvLoadedCount;
        [FieldOffset(62)] public ushort ProfileLoadedCount;
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

    [BurstCompile(CompileSynchronously = false, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockBoidSwarmJob : IJobParallelFor
    {
        public NativeArray<AmbientEntityDTO> Entities;
        public NativeArray<AmbientEntityAupDTO> Aups;
        public NativeArray<BoidStateDTO> BoidStates;
        public AbsoluteUniversePosition CenterAup;
        public float SectorSizeMeters;
        public float SpeedMetersPerSecond;
        public int ActiveCount;
        public uint BaseSeed;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Entities.Length ||
                (uint)index >= (uint)Aups.Length ||
                (uint)index >= (uint)BoidStates.Length)
            {
                return;
            }

            if (index >= ActiveCount)
            {
                Entities[index] = default;
                Aups[index] = new AmbientEntityAupDTO
                {
                    Flags = ShinobuEcosystemBalancer.EntityFlagFree,
                    StableSeed = ShinobuEcosystemBalancer.Hash32((uint)index ^ 0x46524545u)
                };
                BoidStates[index] = default;
                return;
            }

            uint seed = ShinobuEcosystemBalancer.Hash32(BaseSeed ^ ((uint)index * 0x9E3779B9u));
            Unity.Mathematics.Random rng = Unity.Mathematics.Random.CreateFromIndex(seed == uint.MaxValue ? 0x53484E31u : seed);
            float angle = rng.NextFloat(0f, 6.28318530717958647692f);
            float shell01 = rng.NextFloat(0f, 1f);
            float radius = math.lerp(18f, 108f, shell01 * shell01);
            float y = rng.NextFloat(-8f, 8f);
            float3 local = new float3(
                ShinobuEcosystemBalancer.CosPolynomial7(angle) * radius,
                y,
                ShinobuEcosystemBalancer.SinPolynomial7(angle) * radius);
            bool carnivore = (index % 5) == 0;
            uint speciesHash = carnivore ? 0x4341524Eu : 0x48455242u;
            float biomass = carnivore ? 2.5f : 1f;
            float3 velocity = ShinobuEcosystemBalancer.SafeNormalize(
                new float3(-local.z, 0f, local.x),
                new float3(0f, 0f, 1f)) * math.max(0.1f, SpeedMetersPerSecond);
            AbsoluteUniversePosition aup = ShinobuEcosystemBalancer.OffsetAup(in CenterAup, local);
            int3 sectorCoord = ShinobuEcosystemBalancer.ResolveSectorCoord(in aup, math.max(1f, SectorSizeMeters));
            uint sectorHash = ShinobuEcosystemBalancer.ResolveSectorHash(sectorCoord);
            uint flags = ShinobuEcosystemBalancer.EntityFlagActive |
                         ShinobuEcosystemBalancer.EntityFlagHydrated |
                         (carnivore
                             ? ShinobuEcosystemBalancer.EntityFlagCarnivore
                             : ShinobuEcosystemBalancer.EntityFlagHerbivore);

            Entities[index] = new AmbientEntityDTO
            {
                Position = local,
                Velocity = velocity,
                SpeciesHash = speciesHash,
                Biomass = biomass
            };
            Aups[index] = new AmbientEntityAupDTO
            {
                PositionAup = aup,
                Flags = flags,
                SectorHash = sectorHash,
                SpatialCellHash = 0,
                StableSeed = Hecton8.Ecosystem.FaunaGenome64.BuildAupSeed(
                    in aup,
                    sectorHash ^ 0x306FAE31u,
                    speciesHash,
                    (uint)index)
            };
            BoidStates[index] = ShinobuEcosystemBalancer.BuildBoidState(local, velocity, speciesHash, index, 0f);
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
                boidState = ShinobuEcosystemBalancer.BuildBoidState(entity.Position, entity.Velocity, entity.SpeciesHash, index, boidState.PanicScalar);
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
            boidState = ShinobuEcosystemBalancer.BuildBoidState(local, entity.Velocity, entity.SpeciesHash, index, boidState.PanicScalar);
            entitySnapshot = entity;
            aupSnapshot = meta;
            boidStateSnapshot = boidState;
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
        [ReadOnly, NoAlias] public NativeArray<SpatialGridEntryDTO> SpatialGridEntries;
        [ReadOnly, NoAlias] public NativeArray<SpatialGridBucketRangeDTO> SpatialGridBucketRanges;
        [ReadOnly, NoAlias] public NativeArray<FlockingThreatDTO> Threats;
        [ReadOnly, NoAlias] public NativeArray<int> ThreatCount;

        // Safety: every worker may increment global flocking diagnostics, so Unity's per-index write
        // restriction is intentionally bypassed here. The writes are Interlocked.Add operations only.
        // Safety: each row is FlockingCounter64, an explicit 64-byte cache line, so evaluated/sample/panic
        // atomics cannot false-share with adjacent counters on i3/MX350-class or ARM64 cache hierarchies.
        // Safety: this buffer is locked by the owner before scheduling and is never aliased with entity,
        // snapshot, spatial grid, threat, render, or shared ecosystem counter buffers.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<FlockingCounter64> FlockingCounters;
        public uint SpatialGridFrame;
        public int SpatialGridBucketRangeMask;
        public uint HashMultiplierX;
        public uint HashMultiplierY;
        public uint HashMultiplierZ;
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
        public int MaxSpatialGridProbeCount;
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
            SpatialGridEntryDTO* spatialEntries = (SpatialGridEntryDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(SpatialGridEntries);
            SpatialGridBucketRangeDTO* spatialRanges = (SpatialGridBucketRangeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(SpatialGridBucketRanges);
            FlockingThreatDTO* threats = Threats.IsCreated ? (FlockingThreatDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Threats) : null;
            int activeThreatCount = 0;
            if (ThreatCount.IsCreated && ThreatCount.Length > 0)
                activeThreatCount = math.min(Threats.IsCreated ? Threats.Length : 0, math.max(0, ThreatCount[0]));
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
            float3 position = math.select(entity.Position, boidState.LocalPosition, math.all(math.isfinite(boidState.LocalPosition)));
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
            int neighborSamples = 0;
            if (visibleCone && neighborSolve01 > 0.0001f)
            {
                ShinobuEcosystemBalancer.AddFlockingCounterAtomic(FlockingCounters, ShinobuEcosystemBalancer.FlockingCounterSpatialGridQueries, 1);
                double3 absolutePosition = ShinobuEcosystemBalancer.ToAbsoluteDouble3(in meta.PositionAup);
                neighborSamples = QueryNeighbors(index, absolutePosition, position, velocity, neighborSolve01, ref acceleration, entitySnapshots, aupSnapshots, spatialEntries, spatialRanges);
            }

            ShinobuEcosystemBalancer.AddFlockingCounterAtomic(FlockingCounters, ShinobuEcosystemBalancer.FlockingCounterEvaluatedBoids, 1);
            ShinobuEcosystemBalancer.AddFlockingCounterAtomic(FlockingCounters, ShinobuEcosystemBalancer.FlockingCounterNeighborSamples, neighborSamples);

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

            float panicScalar = math.saturate(boidState.PanicScalar * math.max(0f, 1f - (DeltaSeconds * math.lerp(1.25f, 2.6f, ShinobuEcosystemBalancer.Smooth01(GlobalQualityWeight)))));
            if (threats != null && activeThreatCount > 0)
            {
                panicScalar = ShinobuEcosystemBalancer.ApplyFlockingThreats(
                    position,
                    forward,
                    panicScalar,
                    threats,
                    activeThreatCount,
                    Tuning.PredatorAvoidanceWeight,
                    Tuning.EvasionRadiusMeters,
                    GlobalQualityWeight,
                    ref acceleration);
            }

            if (panicScalar > 0.01f)
                ShinobuEcosystemBalancer.AddFlockingCounterAtomic(FlockingCounters, ShinobuEcosystemBalancer.FlockingCounterPanicBoids, 1);

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
            boidState.LocalPosition = position;
            boidState.Velocity = velocity;
            boidState.FlockHashID = ShinobuEcosystemBalancer.ResolveFlockHashId(entity.SpeciesHash, index);
            boidState.PanicScalar = panicScalar;
            entityOut = entity;
            metaOut = meta;
            boidStateOut = boidState;
        }

        private unsafe int QueryNeighbors(
            int index,
            double3 absolutePosition,
            float3 position,
            float3 velocity,
            float neighborSolve01,
            ref float3 acceleration,
            AmbientEntityDTO* entitySnapshots,
            AmbientEntityAupDTO* aupSnapshots,
            SpatialGridEntryDTO* spatialEntries,
            SpatialGridBucketRangeDTO* spatialRanges)
        {
            SpatialGridCell64 baseCell = ShinobuSpatialGridMath.QuantizeCell(absolutePosition, math.max(0.25f, CellSizeMeters));
            float3 separation = new float3(0f);
            float3 alignment = new float3(0f);
            float3 cohesion = new float3(0f);
            int neighborCount = 0;
            int sampleCount = 0;
            int entryScans = 0;
            int hardSampleLimit = math.max(1, MaxNeighborSamplesPerBoid);
            int cellProbeLimit = ShinobuEcosystemBalancer.ResolveNeighborCellProbeBudget(GlobalQualityWeight, MaxSpatialGridProbeCount);
            int cellProbes = 1;
            uint centerHash = ShinobuSpatialGridMath.HashCell(
                in baseCell,
                HashMultiplierX,
                HashMultiplierY,
                HashMultiplierZ);
            uint2 centerFingerprint = ShinobuSpatialGridMath.FingerprintCell(
                in baseCell,
                HashMultiplierX,
                HashMultiplierY,
                HashMultiplierZ);
            CollectNeighborCell(
                centerHash,
                centerFingerprint,
                index,
                position,
                ref separation,
                ref alignment,
                ref cohesion,
                ref neighborCount,
                ref sampleCount,
                ref entryScans,
                hardSampleLimit,
                entitySnapshots,
                aupSnapshots,
                spatialEntries,
                spatialRanges);

            int cellRadius = ShinobuSpatialGridMath.ResolveAdjacentCellRadius(NeighborRadiusMeters, CellSizeMeters, GlobalQualityWeight);
            int maxDistanceSq = cellRadius * cellRadius * 3;
            for (int distanceSq = 1; distanceSq <= maxDistanceSq && entryScans < hardSampleLimit && cellProbes < cellProbeLimit; distanceSq++)
            {
                for (int x = -cellRadius; x <= cellRadius && entryScans < hardSampleLimit && cellProbes < cellProbeLimit; x++)
                {
                    int xSq = x * x;
                    for (int y = -cellRadius; y <= cellRadius && entryScans < hardSampleLimit && cellProbes < cellProbeLimit; y++)
                    {
                        int xySq = xSq + (y * y);
                        for (int z = -cellRadius; z <= cellRadius && entryScans < hardSampleLimit && cellProbes < cellProbeLimit; z++)
                        {
                            if (xySq + (z * z) != distanceSq)
                                continue;

                            cellProbes++;
                            SpatialGridCell64 queryCell = new SpatialGridCell64
                            {
                                X = baseCell.X + x,
                                Y = baseCell.Y + y,
                                Z = baseCell.Z + z
                            };
                            uint2 fingerprint = ShinobuSpatialGridMath.FingerprintCell(
                                in queryCell,
                                HashMultiplierX,
                                HashMultiplierY,
                                HashMultiplierZ);
                            uint hash = ShinobuSpatialGridMath.HashCellFromFingerprint(fingerprint);
                            CollectNeighborCell(
                                hash,
                                fingerprint,
                                index,
                                position,
                                ref separation,
                                ref alignment,
                                ref cohesion,
                                ref neighborCount,
                                ref sampleCount,
                                ref entryScans,
                                hardSampleLimit,
                                entitySnapshots,
                                aupSnapshots,
                                spatialEntries,
                                spatialRanges);
                        }
                    }
                }
            }

            if (neighborCount <= 0)
                return entryScans;

            float inv = math.rcp(math.max(1, neighborCount));
            float3 desiredAlignment = ShinobuEcosystemBalancer.SafeNormalize(alignment * inv, velocity);
            float3 center = cohesion * inv;
            float3 desiredCohesion = ShinobuEcosystemBalancer.SafeNormalize(center - position, new float3(0f));
            float reynolds01 = ShinobuEcosystemBalancer.Smooth01(math.saturate((GlobalQualityWeight - 0.14f) * 1.1627907f));
            acceleration += separation * (Tuning.SeparationWeight * neighborSolve01);
            acceleration += desiredAlignment * (Tuning.AlignmentWeight * reynolds01 * neighborSolve01);
            acceleration += desiredCohesion * (Tuning.CohesionWeight * reynolds01 * neighborSolve01);
            return entryScans;
        }

        private unsafe void CollectNeighborCell(
            uint cellHash,
            uint2 cellFingerprint,
            int selfIndex,
            float3 position,
            ref float3 separation,
            ref float3 alignment,
            ref float3 cohesion,
            ref int neighborCount,
            ref int sampleCount,
            ref int entryScans,
            int hardSampleLimit,
            AmbientEntityDTO* entitySnapshots,
            AmbientEntityAupDTO* aupSnapshots,
            SpatialGridEntryDTO* spatialEntries,
            SpatialGridBucketRangeDTO* spatialRanges)
        {
            if (entryScans >= hardSampleLimit || sampleCount >= hardSampleLimit)
                return;

            if (!TryFindSpatialRange(cellHash, cellFingerprint, spatialRanges, out SpatialGridBucketRangeDTO range))
                return;

            int start = math.clamp(range.StartIndex, 0, Count);
            int available = Count - start;
            int rangeCount = math.min(math.max(0, range.Count), available);
            int end = start + rangeCount;
            int4 laneIndices = new int4(-1);
            int laneCount = 0;
            for (int entryIndex = start; entryIndex < end && entryScans < hardSampleLimit && sampleCount < hardSampleLimit; entryIndex++)
            {
                SpatialGridEntryDTO entry = spatialEntries[entryIndex];
                if (!ShinobuSpatialGridMath.CellFingerprintEquals(entry.CellFingerprint, cellFingerprint))
                    continue;

                entryScans++;
                int otherIndex = (int)entry.EntityRowIndex;
                if ((uint)otherIndex >= (uint)Count || otherIndex == selfIndex)
                    continue;

                AmbientEntityAupDTO otherMeta = UnsafeUtility.AsRef<AmbientEntityAupDTO>(aupSnapshots + otherIndex);
                uint otherFlags = otherMeta.Flags;
                if ((otherFlags & ShinobuEcosystemBalancer.EntityFlagActive) == 0u ||
                    (otherFlags & ShinobuEcosystemBalancer.EntityFlagHydrated) == 0u)
                {
                    continue;
                }

                if (laneCount == 0)
                    laneIndices.x = otherIndex;
                else if (laneCount == 1)
                    laneIndices.y = otherIndex;
                else if (laneCount == 2)
                    laneIndices.z = otherIndex;
                else
                    laneIndices.w = otherIndex;

                laneCount++;
                int remaining = hardSampleLimit - sampleCount;
                if (laneCount >= 4 || laneCount >= remaining)
                {
                    int batchCount = math.min(laneCount, remaining);
                    int accepted = ShinobuEcosystemBalancer.AccumulateNeighborBatch4(
                        position,
                        NeighborRadiusMeters,
                        batchCount,
                        laneIndices,
                        entitySnapshots,
                        ref separation,
                        ref alignment,
                        ref cohesion);
                    neighborCount += accepted;
                    sampleCount += batchCount;
                    laneIndices = new int4(-1);
                    laneCount = 0;
                }
            }

            if (laneCount > 0 && sampleCount < hardSampleLimit)
            {
                int batchCount = math.min(laneCount, hardSampleLimit - sampleCount);
                int accepted = ShinobuEcosystemBalancer.AccumulateNeighborBatch4(
                    position,
                    NeighborRadiusMeters,
                    batchCount,
                    laneIndices,
                    entitySnapshots,
                    ref separation,
                    ref alignment,
                    ref cohesion);
                neighborCount += accepted;
                sampleCount += batchCount;
            }
        }

        private unsafe bool TryFindSpatialRange(uint cellHash, uint2 cellFingerprint, SpatialGridBucketRangeDTO* spatialRanges, out SpatialGridBucketRangeDTO range)
        {
            range = default;
            if (cellHash == 0u || spatialRanges == null || SpatialGridBucketRanges.Length <= 0)
                return false;

            int mask = SpatialGridBucketRangeMask > 0 ? SpatialGridBucketRangeMask : SpatialGridBucketRanges.Length - 1;
            int maxProbe = ShinobuSpatialGridMath.ResolveStructuralProbeCount(SpatialGridBucketRanges.Length);
            for (int probe = 0; probe < maxProbe; probe++)
            {
                int slot = (int)((cellHash + (uint)probe) & (uint)mask);
                if ((uint)slot >= (uint)SpatialGridBucketRanges.Length)
                    return false;

                SpatialGridBucketRangeDTO candidate = spatialRanges[slot];
                if (candidate.Flags != SpatialGridFrame)
                    return false;
                if (candidate.CellHash == cellHash &&
                    ShinobuSpatialGridMath.CellFingerprintEquals(candidate.CellFingerprintX, candidate.CellFingerprintY, cellFingerprint))
                {
                    range = candidate;
                    return true;
                }
            }

            return false;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildShinobuRenderPayloadJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<AmbientEntityDTO> Entities;
        [ReadOnly, NoAlias] public NativeArray<AmbientEntityAupDTO> Aups;
        [ReadOnly, NoAlias] public NativeArray<BoidStateDTO> BoidStates;
        [NoAlias] public NativeArray<BoidMatrixDTO> Matrices;
        [NoAlias] public NativeArray<BoidCustomDataDTO> CustomData;
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
            BoidCustomDataDTO* customData = (BoidCustomDataDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(CustomData);
            ref AmbientEntityDTO entity = ref UnsafeUtility.AsRef<AmbientEntityDTO>(entities + index);
            ref AmbientEntityAupDTO meta = ref UnsafeUtility.AsRef<AmbientEntityAupDTO>(aups + index);
            ref BoidStateDTO boidState = ref UnsafeUtility.AsRef<BoidStateDTO>(boidStates + index);
            ref BoidMatrixDTO matrixOut = ref UnsafeUtility.AsRef<BoidMatrixDTO>(matrices + index);
            ref BoidCustomDataDTO customOut = ref UnsafeUtility.AsRef<BoidCustomDataDTO>(customData + index);
            uint flags = meta.Flags;
            float3 position = math.all(math.isfinite(boidState.LocalPosition)) ? boidState.LocalPosition : entity.Position;
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
            float panicOrSkip = math.max(boidState.PanicScalar, (flags & ShinobuEcosystemBalancer.EntityFlagSkipUpdate) != 0u ? 1f : 0f);
            uint geneticSeed = Hecton8.Ecosystem.FaunaGenome64.BuildStableEntitySeed(meta.StableSeed, entity.SpeciesHash, 0x306FAE31u);
            ulong geneticMask = Hecton8.Ecosystem.FaunaGenome64.CompileGeneticMaskFromSeed(geneticSeed);
            customOut = new BoidCustomDataDTO
            {
                GeneticLow = (uint)geneticMask,
                GeneticHigh = (uint)(geneticMask >> 32),
                PanicOrSkip = panicOrSkip,
                QualityWeight = GlobalQualityWeight
            };
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
                ShinobuSpatialHashDebugCell debugCell = default;
                debugCell.CenterLocal = ((float3)cell + math.float3(0.5f)) * CellSizeMeters;
                debugCell.CellHash = hash;
                debugCell.Occupancy = occupancy;
                debugCell.CellSizeMeters = CellSizeMeters;
                debugCell.Flags = 1u;
                DebugCells[debugCount++] = debugCell;
            }

            if (Counters.IsCreated && Counters.Length > 8)
                Counters[8] = debugCount;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct CountTelemetryCountersJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<AmbientEntityAupDTO> Aups;
        [NoAlias] public NativeArray<int> Counters;
        public int Count;

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

            if (Counters.Length > 1) Counters[1] = active;
            if (Counters.Length > 2) Counters[2] = hydrated;
            if (Counters.Length > 3) Counters[3] = free;
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
                    sector.FloraMass = MockFloraSpawner.SampleSectorFlora(math.int3(sector.SectorX, sector.SectorY, sector.SectorZ));
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
                        Unity.Mathematics.Random random = CreateDeterministicRandom(meta.StableSeed, meta.SectorHash, Frame);
                        float3 jitter = ResolveJitter(ref random) * 3f;
                        AmbientEntityDTO child = entity;
                        child.Position += jitter;
                        child.Biomass = math.max(0.2f, entity.Biomass * 0.45f);
                        child.Velocity = ShinobuEcosystemBalancer.SafeNormalize(entity.Velocity + jitter, math.float3(0f, 0f, 1f)) * math.max(0.5f, Tuning.MaxSpeedMetersPerSecond * 0.75f);
                        entity.Biomass *= 0.55f;
                        Entities[i] = entity;
                        Aups[i] = meta;

                        AbsoluteUniversePosition childAup = ShinobuEcosystemBalancer.OffsetAup(in meta.PositionAup, jitter);
                        int3 childSectorCoord = ShinobuEcosystemBalancer.ResolveSectorCoord(in childAup, SectorSizeMeters);
                        uint childSectorHash = ShinobuEcosystemBalancer.ResolveSectorHash(childSectorCoord);
                        Entities[freeSlot] = child;
                        AmbientEntityAupDTO childMeta = default;
                        childMeta.PositionAup = childAup;
                        childMeta.Flags = (meta.Flags | ShinobuEcosystemBalancer.EntityFlagActive | ShinobuEcosystemBalancer.EntityFlagHydrated) &
                                          ~ShinobuEcosystemBalancer.EntityFlagFree;
                        childMeta.SectorHash = childSectorHash;
                        childMeta.SpatialCellHash = 0;
                        childMeta.StableSeed = Hecton8.Ecosystem.FaunaGenome64.BuildAupSeed(
                            in childAup,
                            childSectorHash ^ 0x306FAE31u,
                            child.SpeciesHash,
                            Frame);
                        Aups[freeSlot] = childMeta;
                        reproduced++;
                    }
                }
            }

            if (ApplyLotka != 0)
                ApplyLotkaVolterra(ref tombstoned, sectorHeadBase);

            RehydrateNearSectors(ref freeCursor, ref reproduced);

            if (Counters.IsCreated)
            {
                if (Counters.Length > 4) Counters[4] = CountDehydratedSectors();
                if (Counters.Length > 9) Counters[9] += reproduced;
                if (Counters.Length > 10) Counters[10] += tombstoned;
            }
        }

        private int CountDehydratedSectors()
        {
            int dehydrated = 0;
            for (int i = 0; i < SectorCount; i++)
                dehydrated += math.select(0, 1, (Sectors[i].Flags & ShinobuEcosystemBalancer.SectorFlagDehydrated) != 0u);
            return dehydrated;
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
                Unity.Mathematics.Random random = CreateDeterministicRandom(sector.SectorHash, (uint)spawnCount, Frame ^ 0x52485944u);
                int spawnedThisSector = 0;
                for (int spawn = 0; spawn < spawnCount; spawn++)
                {
                    int slot = FindNextFreeSlot(ref freeCursor);
                    if (slot < 0)
                        break;

                    float3 jitter = ResolveJitter(ref random) * (SectorSizeMeters * 0.45f);
                    bool carnivore = spawn < (int)math.floor(sector.CarnivoreMass);
                    float biomass = carnivore ? 2.2f : 1f;
                    AbsoluteUniversePosition aup = ShinobuEcosystemBalancer.FromAbsoluteDouble3(centerAbs + (double3)jitter);
                    uint flags = ShinobuEcosystemBalancer.EntityFlagActive | ShinobuEcosystemBalancer.EntityFlagHydrated |
                                 (carnivore ? ShinobuEcosystemBalancer.EntityFlagCarnivore : ShinobuEcosystemBalancer.EntityFlagHerbivore);
                    uint speciesHash = carnivore ? 0x4341524Eu : 0x48455242u;
                    AmbientEntityDTO spawned = default;
                    spawned.Position = local + jitter;
                    spawned.Velocity = ShinobuEcosystemBalancer.SafeNormalize(jitter, math.float3(0f, 0f, 1f)) * math.max(0.5f, Tuning.MaxSpeedMetersPerSecond);
                    spawned.SpeciesHash = speciesHash;
                    spawned.Biomass = biomass;
                    Entities[slot] = spawned;

                    AmbientEntityAupDTO spawnedMeta = default;
                    spawnedMeta.PositionAup = aup;
                    spawnedMeta.Flags = flags;
                    spawnedMeta.SectorHash = sector.SectorHash;
                    spawnedMeta.SpatialCellHash = 0;
                    spawnedMeta.StableSeed = Hecton8.Ecosystem.FaunaGenome64.BuildAupSeed(
                        in aup,
                        sector.SectorHash ^ 0x306FAE31u,
                        speciesHash,
                        (uint)spawn);
                    Aups[slot] = spawnedMeta;
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

                sector = default;
                sector.SectorHash = hash;
                sector.HerbivoreMass = 0f;
                sector.CarnivoreMass = 0f;
                sector.FloraMass = MockFloraSpawner.SampleSectorFlora(coord);
                sector.SectorX = coord.x;
                sector.SectorY = coord.y;
                sector.SectorZ = coord.z;
                sector.Flags = ShinobuEcosystemBalancer.SectorFlagValid;
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
            return math.double3(
                (sector.SectorX + 0.5d) * size,
                (sector.SectorY + 0.5d) * size,
                (sector.SectorZ + 0.5d) * size);
        }

        private static Unity.Mathematics.Random CreateDeterministicRandom(uint stableSeed, uint sectorHash, uint frameOrSalt)
        {
            uint seed = ShinobuEcosystemBalancer.Hash32(stableSeed ^ (sectorHash * 0x9E3779B9u) ^ (frameOrSalt * 0x85EBCA6Bu));
            return Unity.Mathematics.Random.CreateFromIndex(seed == uint.MaxValue ? 0x306FAE31u : seed);
        }

        private static float3 ResolveJitter(ref Unity.Mathematics.Random random)
        {
            return math.float3(
                random.NextFloat(-1f, 1f),
                random.NextFloat(-1f, 1f),
                random.NextFloat(-1f, 1f));
        }
    }
}
