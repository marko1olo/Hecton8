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
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.AI.Ecosystem
{
    /// <summary>
    /// Data-only SHINOBU swarm and biomass balancer. Fish are vault rows, not scene objects.
    /// </summary>
    public sealed class ShinobuEcosystemBalancer : ITickable, IColdTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IDisposable
    {
        private const int DefaultEntityCapacity = 5000;
        private const int DefaultSectorCapacity = 256;
        private const int TelemetryCapacity = 300;
        private const int CounterCapacity = 24;
        private const int DebugCellCapacity = 256;
        private const int SpatialHashBucketCapacity = 32768;
        private const int SpatialHashBucketMask = SpatialHashBucketCapacity - 1;
        private const int FrameJobBatchSize = 64;
        private const int MaxNeighborSamples = 48;
        private const int MaxSpatialHashChainSteps = 64;
        private const int CsvMaxBytes = 8192;
        private const int LegacyProfileReadBytes = 64;
        private const float DefaultCellSizeMeters = 10f;
        private const float DefaultSectorSizeMeters = 64f;
        private const float DefaultNeighborRadiusMeters = 12f;
        private const float DefaultDehydrateDistanceMeters = 200f;
        private const float DefaultRehydrateDistanceMeters = 160f;
        private const float DefaultObstacleProbeMeters = 2f;
        private const float DefaultBoidSpeedMetersPerSecond = 5.5f;
        private const double AupCellSizeMetersDouble = HectonPhysicsContract.AupSectorSizeMetersDouble;
        private const string LegacyFaunaCapsFile = "fauna_population_caps.h8bin";
        private const string LegacyBoidProfileFile = "boid_behavior_profiles.bin";
        private const string CsvRelativePath = "ecosystem_balance.csv";
        private const string CsvPrecomputedRelativePath = "Data/Precomputed/ecosystem_balance.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_ECOSYSTEM.bin";
        private const string DumpH8RelativePath = "Docs/AgentLogs/Dump_ECOSYSTEM.h8dump";
        private const ulong DumpMagic = 0x45434F535348494EUL; // ECOSSHIN
        private const int DumpVersion = 2;
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

        private VaultBufferHandle<AmbientEntityDTO> _entityHandle;
        private VaultBufferHandle<AmbientEntityAupDTO> _aupHandle;
        private VaultBufferHandle<AmbientEntityDTO> _entitySnapshotHandle;
        private VaultBufferHandle<AmbientEntityAupDTO> _aupSnapshotHandle;
        private VaultBufferHandle<EcosystemSectorDTO> _sectorHandle;
        private VaultBufferHandle<ShinobuEcosystemTuning> _tuningHandle;
        private VaultBufferHandle<int> _counterHandle;
        private VaultBufferHandle<ShinobuTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<ShinobuSpatialHashDebugCell> _debugCellHandle;
        private VaultBufferHandle<float4x4> _renderMatrixHandle;
        private VaultBufferHandle<float4> _renderCustomDataHandle;
        private VaultBufferHandle<int> _spatialHashBucketHeadHandle;
        private VaultBufferHandle<int> _spatialHashNextHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;
        private VaultBufferHandle<byte> _legacyScratchHandle;

        private IDataVault _dataVault;
        private JobHandle _activeJobHandle;
        private AbsoluteUniversePosition _cameraAup;
        private float3 _cameraLocalPosition;
        private float3 _cameraForward = new float3(0f, 0f, 1f);
        private long _csvTimestampTicks;
        private long _scheduleTicks;
        private int _telemetryCursor;
        private int _coldTickIndex;
        private float _lastFlockingMs;
        private bool _registeredTick;
        private bool _registeredColdTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _jobScheduled;
        private bool _jobLocksHeld;
        private bool _dumpedFault;
        private uint _runtimeFlags;

        private ShinobuEcosystemBalancer()
        {
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

        private void Activate()
        {
            if (!Application.isPlaying)
                return;

            SignalBus<MockPredatorSignal>.EnsureInitialized();
            TryRegisterHotSwapListener();
            if (EnsureVaultState())
                TryRegisterTicks();
        }

        public void Dispose()
        {
            CompleteFrameJob(forceComplete: true);
            TryUnregisterTicks();
            TryUnregisterHotSwapListener();
            UnlockJobBuffers();
            ClearCachedState();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            CompleteFrameJob(forceComplete: true);
            UnlockJobBuffers();
            _dataVault = currentService as IDataVault;
            ResetVaultHandles();
            _telemetryCursor = 0;
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
                    out NativeArray<AmbientEntityDTO> entitySnapshot,
                    out NativeArray<AmbientEntityAupDTO> aupSnapshot,
                    out NativeArray<EcosystemSectorDTO> sectors,
                    out NativeArray<ShinobuEcosystemTuning> tuningArray,
                    out NativeArray<int> counters,
                    out NativeArray<ShinobuTelemetryEntry> telemetry,
                    out NativeArray<ShinobuSpatialHashDebugCell> debugCells,
                    out NativeArray<float4x4> matrices,
                    out NativeArray<float4> customData,
                    out NativeArray<int> spatialHashBucketHeads,
                    out NativeArray<int> spatialHashNext))
            {
                return;
            }

            int count = entityCapacity;
            count = math.min(count, entities.Length);
            count = math.min(count, aups.Length);
            count = math.min(count, entitySnapshot.Length);
            count = math.min(count, aupSnapshot.Length);
            count = math.min(count, spatialHashNext.Length);
            count = math.min(count, matrices.Length);
            count = math.min(count, customData.Length);
            if (count <= 0)
                return;

            if (!TryLockJobBuffers(vault))
                return;

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
                var buildJob = new LocalShiftAndSpatialHashJob
                {
                    Entities = entities,
                    Aups = aups,
                    EntitySnapshot = entitySnapshot,
                    AupSnapshot = aupSnapshot,
                    CenterAup = _cameraAup,
                    CellSizeMeters = math.max(0.25f, spatialCellSizeMeters),
                    SectorSizeMeters = math.max(1f, sectorSizeMeters),
                    SystemStress01 = ResolveSystemStress01(),
                    Count = count
                };

                JobHandle handle = buildJob.Schedule(count, FrameJobBatchSize);
                var hashJob = new BuildVaultSpatialHashJob
                {
                    AupSnapshot = aupSnapshot,
                    BucketHeads = spatialHashBucketHeads,
                    BucketNext = spatialHashNext,
                    Counters = counters,
                    MaxChainSteps = MaxSpatialHashChainSteps,
                    Count = count
                };
                handle = hashJob.Schedule(handle);
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
                        MaxChainSteps = MaxSpatialHashChainSteps
                    };
                    handle = debugJob.Schedule(handle);
                }

                var solveJob = new BurstBoidBehaviorSolverJob
                {
                    Entities = entities,
                    Aups = aups,
                    EntitySnapshot = entitySnapshot,
                    AupSnapshot = aupSnapshot,
                    BucketHeads = spatialHashBucketHeads,
                    BucketNext = spatialHashNext,
                    CenterAup = _cameraAup,
                    CameraForward = SafeNormalize(_cameraForward, new float3(0f, 0f, 1f)),
                    TerrainSampler = MockTerrainSampler.Default,
                    Predator = predator,
                    Tuning = tuning,
                    DeltaSeconds = math.clamp(deltaTime, 0.001f, 0.05f),
                    CellSizeMeters = math.max(0.25f, spatialCellSizeMeters),
                    SectorSizeMeters = math.max(1f, sectorSizeMeters),
                    NeighborRadiusMeters = math.max(1f, neighborRadiusMeters),
                    ObstacleProbeMeters = math.max(0.1f, obstacleProbeMeters),
                    MaxNeighborSamplesPerBoid = MaxNeighborSamples,
                    MaxSpatialHashChainSteps = MaxSpatialHashChainSteps,
                    Count = count
                };
                handle = solveJob.Schedule(count, FrameJobBatchSize, handle);

                var renderJob = new BuildShinobuRenderPayloadJob
                {
                    Entities = entities,
                    Aups = aups,
                    Matrices = matrices,
                    CustomData = customData,
                    Count = count
                };
                handle = renderJob.Schedule(count, FrameJobBatchSize, handle);

                var countJob = new CountTelemetryCountersJob
                {
                    Aups = aups,
                    Sectors = sectors,
                    Counters = counters,
                    Count = count,
                    SectorCount = math.min(sectorCapacity, sectors.Length)
                };
                handle = countJob.Schedule(handle);

                _activeJobHandle = handle;
                _scheduleTicks = Stopwatch.GetTimestamp();
                H8Memory.RegisterActiveJob(SystemID.AIEcology, _activeJobHandle);
                _jobScheduled = true;
                _jobLocksHeld = true;
            }
            catch (Exception)
            {
                UnlockJobBuffers();
                throw;
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
            CompleteFrameJob(forceComplete: false);
        }

        internal static ref AmbientEntityDTO GetAmbientEntityRef(
            IDataVault vault,
            ref VaultBufferHandle<AmbientEntityDTO> handle,
            int index)
        {
            return ref handle.GetElementAsRef(vault, index);
        }

        private bool EnsureVaultState()
        {
            ShinobuEcosystemLayoutManifest.VerifyColdBoot();

            IDataVault vault = ResolveDataVault();
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

            _entityHandle = vault.GetBufferHandle<AmbientEntityDTO>(
                BufferID.ShinobuAmbientEntities,
                entityCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _aupHandle = vault.GetBufferHandle<AmbientEntityAupDTO>(
                BufferID.ShinobuAmbientAups,
                entityCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _entitySnapshotHandle = vault.GetBufferHandle<AmbientEntityDTO>(
                BufferID.ShinobuAmbientEntitySnapshot,
                entityCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            _aupSnapshotHandle = vault.GetBufferHandle<AmbientEntityAupDTO>(
                BufferID.ShinobuAmbientAupSnapshot,
                entityCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            _sectorHandle = vault.GetBufferHandle<EcosystemSectorDTO>(
                BufferID.ShinobuEcosystemSectors,
                sectorCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _tuningHandle = vault.GetBufferHandle<ShinobuEcosystemTuning>(
                BufferID.ShinobuEcosystemTuning,
                1,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _counterHandle = vault.GetBufferHandle<int>(
                BufferID.ShinobuEcosystemCounters,
                CounterCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _telemetryHandle = vault.GetBufferHandle<ShinobuTelemetryEntry>(
                BufferID.ShinobuEcosystemTelemetryRing,
                TelemetryCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _debugCellHandle = vault.GetBufferHandle<ShinobuSpatialHashDebugCell>(
                BufferID.ShinobuSpatialHashDebugCells,
                DebugCellCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _renderMatrixHandle = vault.GetBufferHandle<float4x4>(
                BufferID.ShinobuRenderMatrices,
                entityCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _renderCustomDataHandle = vault.GetBufferHandle<float4>(
                BufferID.ShinobuRenderCustomData,
                entityCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _spatialHashBucketHeadHandle = vault.GetBufferHandle<int>(
                BufferID.ShinobuSpatialHashBucketHeads,
                SpatialHashBucketCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            _spatialHashNextHandle = vault.GetBufferHandle<int>(
                BufferID.ShinobuSpatialHashNext,
                entityCapacity + sectorCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = vault.GetBufferHandle<byte>(
                BufferID.ShinobuEcosystemCsvScratch,
                CsvMaxBytes,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            _legacyScratchHandle = vault.GetBufferHandle<byte>(
                BufferID.ShinobuEcosystemLegacyScratch,
                LegacyProfileReadBytes,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);

            bool ready = _entityHandle.IsCreated &&
                         _aupHandle.IsCreated &&
                         _entitySnapshotHandle.IsCreated &&
                         _aupSnapshotHandle.IsCreated &&
                         _sectorHandle.IsCreated &&
                         _tuningHandle.IsCreated &&
                         _counterHandle.IsCreated &&
                         _telemetryHandle.IsCreated &&
                         _debugCellHandle.IsCreated &&
                         _renderMatrixHandle.IsCreated &&
                         _renderCustomDataHandle.IsCreated &&
                         _spatialHashBucketHeadHandle.IsCreated &&
                         _spatialHashNextHandle.IsCreated &&
                         _csvScratchHandle.IsCreated &&
                         _legacyScratchHandle.IsCreated;
            if (!ready)
                return false;

            EnsureProfilesLoaded(vault);
            EnsureInitialPopulation(vault);
            return true;
        }

        private IDataVault ResolveDataVault()
        {
            if (_dataVault != null)
                return _dataVault;

            _dataVault = GlobalRegistry.DataVault;
            return _dataVault;
        }

        private bool TryResolveBuffers(
            IDataVault vault,
            out NativeArray<AmbientEntityDTO> entities,
            out NativeArray<AmbientEntityAupDTO> aups,
            out NativeArray<AmbientEntityDTO> entitySnapshot,
            out NativeArray<AmbientEntityAupDTO> aupSnapshot,
            out NativeArray<EcosystemSectorDTO> sectors,
            out NativeArray<ShinobuEcosystemTuning> tuning,
            out NativeArray<int> counters,
            out NativeArray<ShinobuTelemetryEntry> telemetry,
            out NativeArray<ShinobuSpatialHashDebugCell> debugCells,
            out NativeArray<float4x4> matrices,
            out NativeArray<float4> customData,
            out NativeArray<int> spatialHashBucketHeads,
            out NativeArray<int> spatialHashNext)
        {
            entities = _entityHandle.Resolve(vault);
            aups = _aupHandle.Resolve(vault);
            entitySnapshot = _entitySnapshotHandle.Resolve(vault);
            aupSnapshot = _aupSnapshotHandle.Resolve(vault);
            sectors = _sectorHandle.Resolve(vault);
            tuning = _tuningHandle.Resolve(vault);
            counters = _counterHandle.Resolve(vault);
            telemetry = _telemetryHandle.Resolve(vault);
            debugCells = _debugCellHandle.Resolve(vault);
            matrices = _renderMatrixHandle.Resolve(vault);
            customData = _renderCustomDataHandle.Resolve(vault);
            spatialHashBucketHeads = _spatialHashBucketHeadHandle.Resolve(vault);
            spatialHashNext = _spatialHashNextHandle.Resolve(vault);
            return entities.IsCreated &&
                   aups.IsCreated &&
                   entitySnapshot.IsCreated &&
                   aupSnapshot.IsCreated &&
                   sectors.IsCreated &&
                   tuning.IsCreated &&
                   counters.IsCreated &&
                   telemetry.IsCreated &&
                   debugCells.IsCreated &&
                   matrices.IsCreated &&
                   customData.IsCreated &&
                   spatialHashBucketHeads.IsCreated &&
                   spatialHashNext.IsCreated;
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
            out NativeArray<float4x4> matrices,
            out NativeArray<float4> customData)
        {
            return TryResolveBuffers(
                vault,
                out entities,
                out aups,
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
                out _);
        }

        private void EnsureProfilesLoaded(IDataVault vault)
        {
            NativeArray<int> counters = _counterHandle.Resolve(vault);
            NativeArray<ShinobuEcosystemTuning> tuning = _tuningHandle.Resolve(vault);
            if (!counters.IsCreated || !tuning.IsCreated || tuning.Length <= 0)
                return;

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

                NativeArray<byte> scratch = _legacyScratchHandle.Resolve(vault);
                if (!scratch.IsCreated || scratch.Length < LegacyProfileReadBytes)
                    return false;

                int bytesRead = ReadFileIntoNativeScratch(profilePath, scratch, LegacyProfileReadBytes, FileShare.Read);

                if (bytesRead < 24)
                    return false;

                ShinobuEcosystemTuning profile = ShinobuEcosystemTuning.Default;
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

            ShinobuEcosystemTuning profile = ShinobuEcosystemTuning.Default;
            profile.Flags = TuningFlagEmergencyMock;
            tuning[0] = profile;
            _runtimeFlags |= TuningFlagEmergencyMock;
            _runtimeFlags &= ~TuningFlagLegacyBinary;
        }

        private void EnsureInitialPopulation(IDataVault vault)
        {
            NativeArray<int> counters = _counterHandle.Resolve(vault);
            if (!counters.IsCreated || counters.Length <= CounterInitialized || counters[CounterInitialized] != 0)
                return;

            NativeArray<AmbientEntityDTO> entities = _entityHandle.Resolve(vault);
            NativeArray<AmbientEntityAupDTO> aups = _aupHandle.Resolve(vault);
            NativeArray<EcosystemSectorDTO> sectors = _sectorHandle.Resolve(vault);
            if (!entities.IsCreated || !aups.IsCreated || !sectors.IsCreated)
                return;

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
                float3 local = new float3(math.cos(angle) * radius, y, math.sin(angle) * radius);
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
            try
            {
                string path = ResolveCsvPath();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return;

                DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
                if (lastWriteUtc.Ticks == _csvTimestampTicks)
                    return;

                NativeArray<byte> scratch = _csvScratchHandle.Resolve(vault);
                if (!scratch.IsCreated || scratch.Length < CsvMaxBytes)
                    return;

                int bytesRead = ReadFileIntoNativeScratch(path, scratch, CsvMaxBytes, FileShare.ReadWrite);

                if (bytesRead <= 0)
                    return;

                NativeArray<ShinobuEcosystemTuning> tuning = _tuningHandle.Resolve(vault);
                NativeArray<int> counters = _counterHandle.Resolve(vault);
                if (!tuning.IsCreated || tuning.Length <= 0)
                    return;

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

        private void RunMacroBiomassPass(IDataVault vault)
        {
            if (!TryResolveBuffers(
                    vault,
                    out NativeArray<AmbientEntityDTO> entities,
                    out NativeArray<AmbientEntityAupDTO> aups,
                    out _,
                    out _,
                    out NativeArray<EcosystemSectorDTO> sectors,
                    out NativeArray<ShinobuEcosystemTuning> tuningArray,
                    out NativeArray<int> counters,
                    out NativeArray<ShinobuTelemetryEntry> telemetry,
                    out NativeArray<ShinobuSpatialHashDebugCell> debugCells,
                    out NativeArray<float4x4> matrices,
                    out NativeArray<float4> customData,
                    out NativeArray<int> spatialHashBucketHeads,
                    out NativeArray<int> spatialHashNext))
            {
                return;
            }

            if (!TryLockJobBuffers(vault))
                return;

            try
            {
                ShinobuEcosystemTuning tuning = ShinobuEcosystemTuning.Sanitize(tuningArray[0]);
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
                    EntityCount = math.min(entityCapacity, math.min(entities.Length, aups.Length)),
                    SectorCount = math.min(sectorCapacity, sectors.Length),
                    SectorSizeMeters = math.max(1f, sectorSizeMeters),
                    DehydrationDistanceSq = dehydrationDistanceMeters * dehydrationDistanceMeters,
                    RehydrationDistanceSq = rehydrationDistanceMeters * rehydrationDistanceMeters,
                    ApplyLotka = (_coldTickIndex % 60) == 0 ? 1 : 0,
                    Frame = unchecked((uint)Mathf.Max(0, Time.frameCount))
                };
                job.Run();
            }
            finally
            {
                UnlockJobBuffers();
            }
        }

        private void CompleteFrameJob(bool forceComplete)
        {
            if (!_jobScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _activeJobHandle, forceComplete))
                return;

            _jobScheduled = false;
            long completeTicks = Stopwatch.GetTimestamp();
            long elapsedTicks = completeTicks >= _scheduleTicks ? completeTicks - _scheduleTicks : 0L;
            _lastFlockingMs = Stopwatch.Frequency > 0
                ? (float)(elapsedTicks * 1000.0 / Stopwatch.Frequency)
                : 0f;

            IDataVault vault = _dataVault;
            if (vault != null)
                WriteTelemetryAndFaultDump(vault);

            UnlockJobBuffers();
        }

        private void WriteTelemetryAndFaultDump(IDataVault vault)
        {
            NativeArray<ShinobuTelemetryEntry> telemetry = _telemetryHandle.Resolve(vault);
            NativeArray<int> counters = _counterHandle.Resolve(vault);
            if (!telemetry.IsCreated || telemetry.Length <= 0 || !counters.IsCreated)
                return;

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

            telemetry[index] = new ShinobuTelemetryEntry
            {
                Frame = unchecked((uint)Mathf.Max(0, Time.frameCount)),
                StateHash = stateHash,
                ActiveBoidCount = active,
                HydratedBoidCount = hydrated,
                DehydratedSectorCount = dehydrated,
                SkippedBoidCount = skipped,
                AverageFlockingTimeMs = math.max(0f, _lastFlockingMs),
                SystemStress01 = ResolveSystemStress01(),
                Flags = _runtimeFlags | (invalidMath != 0 ? EntityFlagInvalidMath : 0u) | (overflow != 0 ? 0x80000000u : 0u),
                Reserved0 = ReadCounter(counters, CounterReproduced),
                Reserved1 = ReadCounter(counters, CounterTombstoned),
                Reserved2 = ReadCounter(counters, CounterDebugCellCount),
                Reserved3 = ReadCounter(counters, CounterCsvLoaded),
                Reserved4 = ReadCounter(counters, CounterProfileLoaded),
                Reserved5 = 0
            };

            if ((invalidMath != 0 || overflow != 0) && !_dumpedFault)
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
            if (!vault.TryLockBuffer(BufferID.ShinobuAmbientEntitySnapshot, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
            lockedCount++;
            if (!vault.TryLockBuffer(BufferID.ShinobuAmbientAupSnapshot, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, lockedCount); return false; }
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
                UnlockLockedJobBuffers(vault, 13);
            _jobLocksHeld = false;
        }

        private static void UnlockLockedJobBuffers(IDataVault vault, int lockedCount)
        {
            if (lockedCount >= 13) vault.TryUnlockBuffer(BufferID.ShinobuSpatialHashNext, SystemID.AIEcology);
            if (lockedCount >= 12) vault.TryUnlockBuffer(BufferID.ShinobuSpatialHashBucketHeads, SystemID.AIEcology);
            if (lockedCount >= 11) vault.TryUnlockBuffer(BufferID.ShinobuRenderCustomData, SystemID.AIEcology);
            if (lockedCount >= 10) vault.TryUnlockBuffer(BufferID.ShinobuRenderMatrices, SystemID.AIEcology);
            if (lockedCount >= 9) vault.TryUnlockBuffer(BufferID.ShinobuSpatialHashDebugCells, SystemID.AIEcology);
            if (lockedCount >= 8) vault.TryUnlockBuffer(BufferID.ShinobuEcosystemTelemetryRing, SystemID.AIEcology);
            if (lockedCount >= 7) vault.TryUnlockBuffer(BufferID.ShinobuEcosystemCounters, SystemID.AIEcology);
            if (lockedCount >= 6) vault.TryUnlockBuffer(BufferID.ShinobuEcosystemTuning, SystemID.AIEcology);
            if (lockedCount >= 5) vault.TryUnlockBuffer(BufferID.ShinobuEcosystemSectors, SystemID.AIEcology);
            if (lockedCount >= 4) vault.TryUnlockBuffer(BufferID.ShinobuAmbientAupSnapshot, SystemID.AIEcology);
            if (lockedCount >= 3) vault.TryUnlockBuffer(BufferID.ShinobuAmbientEntitySnapshot, SystemID.AIEcology);
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
            _entityHandle = default;
            _aupHandle = default;
            _entitySnapshotHandle = default;
            _aupSnapshotHandle = default;
            _sectorHandle = default;
            _tuningHandle = default;
            _counterHandle = default;
            _telemetryHandle = default;
            _debugCellHandle = default;
            _renderMatrixHandle = default;
            _renderCustomDataHandle = default;
            _spatialHashBucketHeadHandle = default;
            _spatialHashNextHandle = default;
            _csvScratchHandle = default;
            _legacyScratchHandle = default;
        }

        private void ClearCachedState()
        {
            _dataVault = null;
            ResetVaultHandles();
            _telemetryCursor = 0;
            _coldTickIndex = 0;
            _lastFlockingMs = 0f;
            _scheduleTicks = 0L;
            _csvTimestampTicks = 0L;
            _runtimeFlags = 0u;
            _dumpedFault = false;
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

        private static string ResolveCsvPath()
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
                    writer.Write(entry.AverageFlockingTimeMs);
                    writer.Write(entry.SystemStress01);
                    writer.Write(entry.Flags);
                    writer.Write(entry.Reserved0);
                    writer.Write(entry.Reserved1);
                    writer.Write(entry.Reserved2);
                    writer.Write(entry.Reserved3);
                    writer.Write(entry.Reserved4);
                    writer.Write(entry.Reserved5);
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
            return math.isfinite(lenSq) && lenSq > 0.000001f ? value * math.rsqrt(lenSq) : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint NextLcg(uint state)
        {
            return unchecked((state * 1664525u) + 1013904223u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash32(uint value)
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

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct AmbientEntityDTO
    {
        public float3 Position;
        public float3 Velocity;
        public uint SpeciesHash;
        public float Biomass;
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

        public static ShinobuEcosystemTuning Default => new ShinobuEcosystemTuning
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

        public static ShinobuEcosystemTuning Sanitize(ShinobuEcosystemTuning value)
        {
            ShinobuEcosystemTuning fallback = Default;
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
        [FieldOffset(24)] public float AverageFlockingTimeMs;
        [FieldOffset(28)] public float SystemStress01;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public int Reserved0;
        [FieldOffset(40)] public int Reserved1;
        [FieldOffset(44)] public int Reserved2;
        [FieldOffset(48)] public int Reserved3;
        [FieldOffset(52)] public int Reserved4;
        [FieldOffset(56)] public int Reserved5;
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

    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public partial struct MockTerrainSampler
    {
        public float3 SphereA;
        public float3 SphereB;
        public float3 SphereC;
        public float PlaneY;
        public float SphereRadius;
        public float Reserved;

        public static MockTerrainSampler Default => new MockTerrainSampler
        {
            SphereA = new float3(24f, -6f, 10f),
            SphereB = new float3(-32f, -4f, 18f),
            SphereC = new float3(8f, -10f, -42f),
            PlaneY = -18f,
            SphereRadius = 4f,
            Reserved = 0f
        };

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
            float len = math.sqrt(math.max(0.000001f, lenSq));
            return new MockTerrainSample
            {
                Distance = len - radius,
                Normal = delta * math.rsqrt(math.max(0.000001f, lenSq))
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

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct LocalShiftAndSpatialHashJob : IJobParallelFor
    {
        public NativeArray<AmbientEntityDTO> Entities;
        public NativeArray<AmbientEntityAupDTO> Aups;
        public NativeArray<AmbientEntityDTO> EntitySnapshot;
        public NativeArray<AmbientEntityAupDTO> AupSnapshot;
        public AbsoluteUniversePosition CenterAup;
        public float CellSizeMeters;
        public float SectorSizeMeters;
        public float SystemStress01;
        public int Count;

        public void Execute(int index)
        {
            if (index >= Count)
                return;

            AmbientEntityAupDTO meta = Aups[index];
            uint flags = meta.Flags;
            if ((flags & ShinobuEcosystemBalancer.EntityFlagActive) == 0u ||
                (flags & ShinobuEcosystemBalancer.EntityFlagHydrated) == 0u)
            {
                meta.Flags = flags | ShinobuEcosystemBalancer.EntityFlagFree;
                Aups[index] = meta;
                EntitySnapshot[index] = default;
                AupSnapshot[index] = meta;
                return;
            }

            flags &= ~ShinobuEcosystemBalancer.EntityFlagSkipUpdate;
            AmbientEntityDTO entity = Entities[index];
            float3 local = ShinobuEcosystemBalancer.AupToLocal(in meta.PositionAup, in CenterAup);
            if (!math.all(math.isfinite(local)))
            {
                meta.Flags = flags | ShinobuEcosystemBalancer.EntityFlagInvalidMath;
                Aups[index] = meta;
                EntitySnapshot[index] = entity;
                AupSnapshot[index] = meta;
                return;
            }

            int3 sectorCoord = ShinobuEcosystemBalancer.ResolveSectorCoord(in meta.PositionAup, SectorSizeMeters);
            int cellHash = ShinobuEcosystemBalancer.ResolveSpatialCellHash(local, CellSizeMeters);
            if (SystemStress01 > 0.82f && (index & 1) != 0)
                flags |= ShinobuEcosystemBalancer.EntityFlagSkipUpdate;

            entity.Position = local;
            meta.SectorHash = ShinobuEcosystemBalancer.ResolveSectorHash(sectorCoord);
            meta.SpatialCellHash = cellHash;
            meta.Flags = flags;
            Entities[index] = entity;
            Aups[index] = meta;
            EntitySnapshot[index] = entity;
            AupSnapshot[index] = meta;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildVaultSpatialHashJob : IJob
    {
        [ReadOnly] public NativeArray<AmbientEntityAupDTO> AupSnapshot;
        public NativeArray<int> BucketHeads;
        public NativeArray<int> BucketNext;
        [NativeDisableParallelForRestriction] public NativeArray<int> Counters;
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

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BurstBoidBehaviorSolverJob : IJobParallelFor
    {
        public NativeArray<AmbientEntityDTO> Entities;
        public NativeArray<AmbientEntityAupDTO> Aups;
        [ReadOnly] public NativeArray<AmbientEntityDTO> EntitySnapshot;
        [ReadOnly] public NativeArray<AmbientEntityAupDTO> AupSnapshot;
        [ReadOnly] public NativeArray<int> BucketHeads;
        [ReadOnly] public NativeArray<int> BucketNext;
        public AbsoluteUniversePosition CenterAup;
        public float3 CameraForward;
        public MockTerrainSampler TerrainSampler;
        public MockPredatorRuntime Predator;
        public ShinobuEcosystemTuning Tuning;
        public float DeltaSeconds;
        public float CellSizeMeters;
        public float SectorSizeMeters;
        public float NeighborRadiusMeters;
        public float ObstacleProbeMeters;
        public int MaxNeighborSamplesPerBoid;
        public int MaxSpatialHashChainSteps;
        public int Count;

        public void Execute(int index)
        {
            if (index >= Count)
                return;

            AmbientEntityAupDTO meta = AupSnapshot[index];
            uint flags = meta.Flags;
            if ((flags & ShinobuEcosystemBalancer.EntityFlagActive) == 0u ||
                (flags & ShinobuEcosystemBalancer.EntityFlagHydrated) == 0u ||
                (flags & ShinobuEcosystemBalancer.EntityFlagSkipUpdate) != 0u)
            {
                return;
            }

            AmbientEntityDTO entity = EntitySnapshot[index];
            float3 position = entity.Position;
            float3 velocity = entity.Velocity;
            bool invalid = !math.all(math.isfinite(position)) || !math.all(math.isfinite(velocity));
            if (invalid)
            {
                meta.Flags = flags | ShinobuEcosystemBalancer.EntityFlagInvalidMath;
                Aups[index] = meta;
                return;
            }

            float3 forward = ShinobuEcosystemBalancer.SafeNormalize(velocity, CameraForward);
            float cameraDot = math.dot(ShinobuEcosystemBalancer.SafeNormalize(position, CameraForward), CameraForward);
            bool visibleCone = cameraDot > -0.15f;
            float3 acceleration = new float3(0f);
            if (visibleCone)
            {
                QueryNeighbors(index, position, velocity, ref acceleration);
            }

            if (Predator.Valid != 0)
            {
                float3 predatorDelta = position - Predator.PositionLocal;
                float distSq = math.lengthsq(predatorDelta);
                float radius = math.max(1f, Predator.RadiusMeters);
                if (meta.SectorHash == Predator.SectorHash || distSq < radius * radius)
                {
                    float3 away = ShinobuEcosystemBalancer.SafeNormalize(predatorDelta, -forward);
                    float proximity = math.saturate(1f - (math.sqrt(math.max(0.0001f, distSq)) / radius));
                    acceleration += away * (Tuning.PredatorAvoidanceWeight * math.max(0.25f, Predator.Intensity01) * (1f + proximity));
                }
            }

            MockTerrainSample terrain = TerrainSampler.SampleSdf(position);
            if (terrain.Distance < ObstacleProbeMeters)
            {
                float push = math.saturate((ObstacleProbeMeters - terrain.Distance) / math.max(0.001f, ObstacleProbeMeters));
                acceleration += ShinobuEcosystemBalancer.SafeNormalize(terrain.Normal, new float3(0f, 1f, 0f)) * (push * 6f);
            }

            entity.Biomass = math.max(0.05f, entity.Biomass + (MockFloraSpawner.SampleFloraMass(position, entity.SpeciesHash) * Tuning.FeedRate * DeltaSeconds));
            velocity += acceleration * DeltaSeconds;
            float speed = math.length(velocity);
            float maxSpeed = math.max(0.5f, Tuning.MaxSpeedMetersPerSecond);
            if (!math.isfinite(speed) || speed <= 0.0001f)
                velocity = forward * maxSpeed;
            else
                velocity = velocity * (math.min(speed, maxSpeed) / speed);

            position += velocity * DeltaSeconds;
            if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(velocity)))
            {
                meta.Flags = flags | ShinobuEcosystemBalancer.EntityFlagInvalidMath;
                Aups[index] = meta;
                return;
            }

            entity.Position = position;
            entity.Velocity = velocity;
            meta.PositionAup = ShinobuEcosystemBalancer.OffsetAup(in CenterAup, position);
            meta.SpatialCellHash = ShinobuEcosystemBalancer.ResolveSpatialCellHash(position, CellSizeMeters);
            meta.SectorHash = ShinobuEcosystemBalancer.ResolveSectorHash(ShinobuEcosystemBalancer.ResolveSectorCoord(in meta.PositionAup, SectorSizeMeters));
            Entities[index] = entity;
            Aups[index] = meta;
        }

        private void QueryNeighbors(int index, float3 position, float3 velocity, ref float3 acceleration)
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
                        int otherIndex = BucketHeads[bucket];
                        int guard = 0;
                        while (otherIndex >= 0 && otherIndex < Count && guard < maxChainSteps)
                        {
                            guard++;
                            int nextIndex = BucketNext[otherIndex];
                            if (otherIndex != index)
                            {
                                AmbientEntityAupDTO otherMeta = AupSnapshot[otherIndex];
                                uint otherFlags = otherMeta.Flags;
                                if (otherMeta.SpatialCellHash == hash &&
                                    (otherFlags & ShinobuEcosystemBalancer.EntityFlagActive) != 0u &&
                                    (otherFlags & ShinobuEcosystemBalancer.EntityFlagHydrated) != 0u)
                                {
                                    AmbientEntityDTO other = EntitySnapshot[otherIndex];
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

            float inv = 1f / neighborCount;
            float3 desiredAlignment = ShinobuEcosystemBalancer.SafeNormalize(alignment * inv, velocity);
            float3 center = cohesion * inv;
            float3 desiredCohesion = ShinobuEcosystemBalancer.SafeNormalize(center - position, new float3(0f));
            acceleration += separation * Tuning.SeparationWeight;
            acceleration += desiredAlignment * Tuning.AlignmentWeight;
            acceleration += desiredCohesion * Tuning.CohesionWeight;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildShinobuRenderPayloadJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AmbientEntityDTO> Entities;
        [ReadOnly] public NativeArray<AmbientEntityAupDTO> Aups;
        public NativeArray<float4x4> Matrices;
        public NativeArray<float4> CustomData;
        public int Count;

        public void Execute(int index)
        {
            if (index >= Count)
                return;

            AmbientEntityAupDTO meta = Aups[index];
            AmbientEntityDTO entity = Entities[index];
            uint flags = meta.Flags;
            if ((flags & ShinobuEcosystemBalancer.EntityFlagActive) == 0u ||
                (flags & ShinobuEcosystemBalancer.EntityFlagHydrated) == 0u ||
                (flags & ShinobuEcosystemBalancer.EntityFlagInvalidMath) != 0u ||
                !math.all(math.isfinite(entity.Position)) ||
                !math.all(math.isfinite(entity.Velocity)) ||
                !math.isfinite(entity.Biomass))
            {
                Matrices[index] = default;
                CustomData[index] = default;
                return;
            }

            float3 forward = ShinobuEcosystemBalancer.SafeNormalize(entity.Velocity, new float3(0f, 0f, 1f));
            float scale = math.clamp(0.18f + entity.Biomass * 0.06f, 0.12f, 0.6f);
            float4x4 matrix = float4x4.TRS(entity.Position, quaternion.LookRotationSafe(forward, new float3(0f, 1f, 0f)), new float3(scale));
            if (!math.all(math.isfinite(matrix.c0)) ||
                !math.all(math.isfinite(matrix.c1)) ||
                !math.all(math.isfinite(matrix.c2)) ||
                !math.all(math.isfinite(matrix.c3)))
            {
                Matrices[index] = default;
                CustomData[index] = default;
                return;
            }

            Matrices[index] = matrix;
            float speciesLane = entity.SpeciesHash == 0x4341524Eu ? 1f : 0f;
            CustomData[index] = new float4(speciesLane, entity.Biomass, (flags & ShinobuEcosystemBalancer.EntityFlagSkipUpdate) != 0u ? 1f : 0f, 1f);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildHashDebugCellsJob : IJob
    {
        [ReadOnly] public NativeArray<AmbientEntityDTO> Entities;
        [ReadOnly] public NativeArray<AmbientEntityAupDTO> Aups;
        [ReadOnly] public NativeArray<int> BucketHeads;
        [ReadOnly] public NativeArray<int> BucketNext;
        public NativeArray<ShinobuSpatialHashDebugCell> DebugCells;
        [NativeDisableParallelForRestriction] public NativeArray<int> Counters;
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

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct CountTelemetryCountersJob : IJob
    {
        [ReadOnly] public NativeArray<AmbientEntityAupDTO> Aups;
        [ReadOnly] public NativeArray<EcosystemSectorDTO> Sectors;
        [NativeDisableParallelForRestriction] public NativeArray<int> Counters;
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
                active += (flags & ShinobuEcosystemBalancer.EntityFlagActive) != 0u ? 1 : 0;
                hydrated += (flags & ShinobuEcosystemBalancer.EntityFlagHydrated) != 0u ? 1 : 0;
                free += (flags & ShinobuEcosystemBalancer.EntityFlagFree) != 0u ? 1 : 0;
                skipped += (flags & ShinobuEcosystemBalancer.EntityFlagSkipUpdate) != 0u ? 1 : 0;
                invalid += (flags & ShinobuEcosystemBalancer.EntityFlagInvalidMath) != 0u ? 1 : 0;
            }

            int dehydratedSectors = 0;
            for (int i = 0; i < SectorCount; i++)
                dehydratedSectors += (Sectors[i].Flags & ShinobuEcosystemBalancer.SectorFlagDehydrated) != 0u ? 1 : 0;

            if (Counters.Length > 1) Counters[1] = active;
            if (Counters.Length > 2) Counters[2] = hydrated;
            if (Counters.Length > 3) Counters[3] = free;
            if (Counters.Length > 4) Counters[4] = dehydratedSectors;
            if (Counters.Length > 5) Counters[5] = skipped;
            if (Counters.Length > 6) Counters[6] = invalid;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct LotkaVolterraMacroJob : IJob
    {
        public NativeArray<AmbientEntityDTO> Entities;
        public NativeArray<AmbientEntityAupDTO> Aups;
        public NativeArray<EcosystemSectorDTO> Sectors;
        public NativeArray<int> SectorBucketHeads;
        public NativeArray<int> SectorEntityLinks;
        [NativeDisableParallelForRestriction] public NativeArray<int> Counters;
        public AbsoluteUniversePosition CenterAup;
        public ShinobuEcosystemTuning Tuning;
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
                int sectorSlot = ResolveOrCreateSector(sectorCoord, ref sectorCursor, hashBucketCount);
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
                int spawnCount = math.clamp((int)math.floor(totalMass), 0, 64);
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

        private int ResolveOrCreateSector(int3 coord, ref int freeSectorCursor, int hashBucketCount)
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
