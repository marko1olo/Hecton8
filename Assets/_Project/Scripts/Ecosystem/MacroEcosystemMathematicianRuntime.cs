using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using EcosystemSectorDTO = Hecton8.Core.Contracts.EcosystemSectorDTO;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Ecosystem
{
    /// <summary>
    /// SHINOBU_300 data-only macro ecosystem solver. It owns no GameObjects and never calls Unity physics.
    /// </summary>
    public unsafe sealed partial class MacroEcosystemMathematicianRuntime : IFrostTickable, IGlobalRegistryHotSwapListener, IDisposable
    {
        private const int GridWidth = 100;
        private const int GridHeight = 100;
        private const int SectorCapacity = GridWidth * GridHeight;
        private const int IndexCapacity = 32768;
        private const int TelemetryCapacity = 300;
        private const int CounterCapacity = 16;
        private const int BiomeSpecCapacity = 256;
        private const int CsvScratchBytes = 32768;
        private const int JobBatchSize = 64;
        private const float SectorSizeMeters = 1000f;
        private const float FrostDeltaSeconds = 5f;
        private const string CsvFileName = "macro_ecosystem_coefficients.csv";
        private const string LegacyCsvFileName = "biome_ecosystem_specs.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_300.bin";
        private const ulong DumpMagic = 0x4D4143524F45434FUL; // MACROECO
        private const uint RouteHash = 0x53483136u; // SH16
        private const uint PostSimulationSystemHash = RouteHash ^ 0x50534D30u; // PSM0
        private const uint JobPinSectorFront = 1u << 0;
        private const uint JobPinSectorBack = 1u << 1;
        private const uint JobPinRemainders = 1u << 2;
        private const uint JobPinSectorCoords = 1u << 3;
        private const uint JobPinBiomeSpecs = 1u << 4;
        private const uint JobPinTuning = 1u << 5;
        private const uint JobPinCounters = 1u << 6;
        private const uint JobPinFaultFlags = 1u << 7;
        private const uint JobPinTelemetryRing = 1u << 8;
#if UNITY_EDITOR
        private static readonly ulong BiomeSpecImportMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuMacroEcosystemBiomeSpecs);
        private static readonly ulong BiomeSpecImportCounterMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuMacroEcosystemCounters);
        private static readonly byte[] s_biomeCsvImportScratch = new byte[CsvScratchBytes];
        private static readonly BiomeEcosystemSpecDTO[] s_biomeSpecImportScratch = new BiomeEcosystemSpecDTO[BiomeSpecCapacity];
        private static int s_biomeCsvImportScratchBusy;
#endif

        private static MacroEcosystemMathematicianRuntime s_runtime;

        private VaultGenerationHandle<EcosystemSectorDTO> _frontHandle;
        private VaultGenerationHandle<EcosystemSectorDTO> _backHandle;
        private VaultGenerationHandle<EcosystemSectorRemainderDTO> _remainderHandle;
        private VaultGenerationHandle<EcosystemSectorCoordDTO> _coordHandle;
        private VaultGenerationHandle<EcosystemSectorIndexEntryDTO> _indexEntryHandle;
        private VaultGenerationHandle<BiomeEcosystemSpecDTO> _biomeSpecHandle;
        private VaultGenerationHandle<MacroEcosystemTuningDTO> _tuningHandle;
        private VaultGenerationHandle<MacroEcosystemCounterDTO> _counterHandle;
        private VaultGenerationHandle<MacroEcosystemTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<uint> _faultFlagHandle;

        private IDataVault _vault;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private JobHandle _activeJobHandle;
        private long _scheduleTicks;
        private long _csvTimestampTicks;
        private uint _simulationTick;
        private int _telemetryCursor;
        private int _lastTelemetrySlot;
        private int _lastDiffusionSteps;
        private bool _initialized;
        private bool _registeredFrost;
        private bool _registeredPostSimulation;
        private bool _registeredHotSwap;
        private bool _jobScheduled;
        private bool _jobPinsHeld;
        private bool _dumpedFault;
        private IDataVault _jobPinVault;
        private uint _jobPinMask;

        private MacroEcosystemMathematicianRuntime()
        {
        }

        /// <summary>
        /// Ensures the headless runtime exists and is registered to FrostTick.
        /// </summary>
        public static MacroEcosystemMathematicianRuntime EnsureRuntime()
        {
            MacroEcosystemMathematicianRuntime runtime = s_runtime;
            if (runtime == null)
            {
                runtime = new MacroEcosystemMathematicianRuntime();
                s_runtime = runtime;
            }

            runtime.Activate();
            return runtime;
        }

        /// <summary>
        /// Legacy same-domain bridge; callers should prefer the double3 AUP overload.
        /// The float3 value is treated as runtime-local meters and converted through the current floating origin.
        /// </summary>
        public static bool TryGetBiomassAvailability(float3 runtimePosition, out float preyBiomass01, out float predatorBiomass01, out float carryingCapacity01)
        {
            preyBiomass01 = 0f;
            predatorBiomass01 = 0f;
            carryingCapacity01 = 0f;
            return TryResolveRuntimePositionAup(runtimePosition, out double3 absoluteUniversePosition) &&
                   TryGetBiomassAvailability(absoluteUniversePosition, out preyBiomass01, out predatorBiomass01, out carryingCapacity01);
        }

        /// <summary>
        /// Resolves local macro biomass from a 64-bit Absolute Universe Position.
        /// </summary>
        public static bool TryGetBiomassAvailability(double3 absoluteUniversePosition, out float preyBiomass01, out float predatorBiomass01, out float carryingCapacity01)
        {
            preyBiomass01 = 0f;
            predatorBiomass01 = 0f;
            carryingCapacity01 = 0f;

            MacroEcosystemMathematicianRuntime runtime = s_runtime;
            if (runtime == null || !runtime._initialized || !math.all(math.isfinite(absoluteUniversePosition)))
                return false;

            ResolveSectorCoordFromAup(absoluteUniversePosition, out long sectorX, out long sectorY, out long sectorZ);
            return runtime.TryGetSectorBiomass(sectorX, sectorY, sectorZ, out preyBiomass01, out predatorBiomass01, out carryingCapacity01);
        }

        /// <summary>
        /// Legacy same-domain bridge; callers should prefer the double3 AUP overload.
        /// The float3 value is treated as runtime-local meters and converted through the current floating origin.
        /// </summary>
        public static bool TryGetSectorSpawnWeights(float3 runtimePosition, out float predatorWeight01, out float rareResourceWeight01)
        {
            predatorWeight01 = 0f;
            rareResourceWeight01 = 0f;
            return TryResolveRuntimePositionAup(runtimePosition, out double3 absoluteUniversePosition) &&
                   TryGetSectorSpawnWeights(absoluteUniversePosition, out predatorWeight01, out rareResourceWeight01);
        }

        /// <summary>
        /// Resolves predator and rare-resource spawn weights from a 64-bit Absolute Universe Position.
        /// </summary>
        public static bool TryGetSectorSpawnWeights(double3 absoluteUniversePosition, out float predatorWeight01, out float rareResourceWeight01)
        {
            predatorWeight01 = 0f;
            rareResourceWeight01 = 0f;

            MacroEcosystemMathematicianRuntime runtime = s_runtime;
            if (runtime == null || !runtime._initialized || !math.all(math.isfinite(absoluteUniversePosition)))
                return false;

            ResolveSectorCoordFromAup(absoluteUniversePosition, out long sectorX, out long sectorY, out long sectorZ);
            return runtime.TryGetSectorSpawnWeights(sectorX, sectorY, sectorZ, out predatorWeight01, out rareResourceWeight01);
        }

        private static bool TryResolveRuntimePositionAup(float3 runtimePosition, out double3 absoluteUniversePosition)
        {
            absoluteUniversePosition = default;
            if (!math.all(math.isfinite(runtimePosition)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            double3 origin = originAup.ToAbsoluteDouble3();
            double3 local = new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            absoluteUniversePosition = origin + local;
            return math.all(math.isfinite(absoluteUniversePosition));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubsystem()
        {
            if (s_runtime != null)
                s_runtime.Dispose();

            s_runtime = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            EnsureRuntime();
        }

        /// <inheritdoc />
        public void FrostTick()
        {
            if (_jobScheduled || !HasVaultStateReady())
                return;

#if UNITY_EDITOR
            TryLoadBiomeSpecsCsv();
#endif
            IDataVault vault = _vault;
            if (vault == null || !TryLockJobBuffers(vault))
                return;

            bool keepJobPins = false;
            try
            {
                if (!TryOpenVaultBuffer(vault, ref _frontHandle, BufferID.ShinobuMacroEcosystemSectorFront, SectorCapacity, out NativeArray<EcosystemSectorDTO> front) ||
                    !TryOpenVaultBuffer(vault, ref _backHandle, BufferID.ShinobuMacroEcosystemSectorBack, SectorCapacity, out NativeArray<EcosystemSectorDTO> back) ||
                    !TryOpenVaultBuffer(vault, ref _remainderHandle, BufferID.ShinobuMacroEcosystemRemainders, SectorCapacity, out NativeArray<EcosystemSectorRemainderDTO> remainders) ||
                    !TryOpenVaultBuffer(vault, ref _coordHandle, BufferID.ShinobuMacroEcosystemSectorCoords, SectorCapacity, out NativeArray<EcosystemSectorCoordDTO> coords) ||
                    !TryOpenVaultBuffer(vault, ref _biomeSpecHandle, BufferID.ShinobuMacroEcosystemBiomeSpecs, 1, out NativeArray<BiomeEcosystemSpecDTO> biomeSpecs) ||
                    !TryOpenVaultBuffer(vault, ref _tuningHandle, BufferID.ShinobuMacroEcosystemTuning, 1, out NativeArray<MacroEcosystemTuningDTO> tuningArray) ||
                    !TryOpenVaultBuffer(vault, ref _counterHandle, BufferID.ShinobuMacroEcosystemCounters, CounterCapacity, out NativeArray<MacroEcosystemCounterDTO> counters) ||
                    !TryOpenVaultBuffer(vault, ref _telemetryHandle, BufferID.ShinobuMacroEcosystemTelemetryRing, TelemetryCapacity, out NativeArray<MacroEcosystemTelemetryEntry> telemetry) ||
                    !TryOpenVaultBuffer(vault, ref _faultFlagHandle, BufferID.ShinobuMacroEcosystemFaultFlags, SectorCapacity, out NativeArray<uint> faultFlags))
                {
                    return;
                }

                int sectorCount = math.min(SectorCapacity, front.Length);
                sectorCount = math.min(sectorCount, back.Length);
                sectorCount = math.min(sectorCount, remainders.Length);
                sectorCount = math.min(sectorCount, coords.Length);
                sectorCount = math.min(sectorCount, faultFlags.Length);
                if (sectorCount <= 0)
                    return;

                MacroEcosystemTuningDTO tuning = MacroEcosystemTuningDTO.Sanitize(tuningArray[0]);
                tuning.GlobalQualityWeight = ResolveGlobalQualityWeight();
                tuning.FrostDeltaSeconds = FrostDeltaSeconds;
                tuning.Flags |= MacroEcosystemMath.TuningFlagSnapshotWriteInFlight;
                tuning.StateHash = MacroEcosystemMath.Mix32(tuning.StateHash, math.asuint(tuning.GlobalQualityWeight));
                tuningArray[0] = tuning;

                int diffusionSteps = MacroEcosystemMath.ResolveDiffusionSteps(tuning.GlobalQualityWeight);
                int integrationSubsteps = MacroEcosystemMath.ResolveIntegrationSubsteps(tuning.GlobalQualityWeight);
                float qualityFlowWeight = MacroEcosystemMath.ResolveQualityFlowWeight(tuning.GlobalQualityWeight);
                _lastDiffusionSteps = diffusionSteps;
                int telemetrySlot = _telemetryCursor % telemetry.Length;
                _lastTelemetrySlot = telemetrySlot;

                EcosystemSectorDTO* frontPtr = (EcosystemSectorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(front);
                EcosystemSectorDTO* backPtr = (EcosystemSectorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(back);
                EcosystemSectorRemainderDTO* remainderPtr = (EcosystemSectorRemainderDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(remainders);
                EcosystemSectorCoordDTO* coordPtr = (EcosystemSectorCoordDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(coords);
                BiomeEcosystemSpecDTO* biomeSpecPtr = (BiomeEcosystemSpecDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(biomeSpecs);
                uint* faultFlagPtr = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr(faultFlags);

                var populationJob = new EcosystemPopulationJob
                {
                    Front = frontPtr,
                    Back = backPtr,
                    Remainders = remainderPtr,
                    Coords = coordPtr,
                    BiomeSpecs = biomeSpecPtr,
                    FaultFlags = faultFlagPtr,
                    Tuning = tuning,
                    IntegrationSubsteps = integrationSubsteps,
                    SectorCount = sectorCount,
                    BiomeSpecCapacity = biomeSpecs.Length
                };

                JobHandle handle = populationJob.Schedule(populationJob.SectorCount, JobBatchSize);
                EcosystemSectorDTO* source = backPtr;
                EcosystemSectorDTO* destination = frontPtr;
                for (int step = 0; step < diffusionSteps; step++)
                {
                    var diffusionJob = new BiomassDiffusionJob
                    {
                        Source = source,
                        Destination = destination,
                        Remainders = remainderPtr,
                        Coords = coordPtr,
                        BiomeSpecs = biomeSpecPtr,
                        SectorCount = populationJob.SectorCount,
                        BiomeSpecCapacity = biomeSpecs.Length,
                        Width = GridWidth,
                        Height = GridHeight,
                        SectorSizeMeters = SectorSizeMeters,
                        MigrationRate = tuning.MigrationRate,
                        QualityFlowWeight = qualityFlowWeight,
                        CarryingCapacityPrey = tuning.CarryingCapacityPrey,
                        CarryingCapacityPredator = tuning.CarryingCapacityPredator,
                        TemperatureOptimum = tuning.TemperatureOptimum,
                        TemperatureHalfRange = tuning.TemperatureHalfRange
                    };
                    handle = diffusionJob.Schedule(populationJob.SectorCount, JobBatchSize, handle);
                    EcosystemSectorDTO* swap = source;
                    source = destination;
                    destination = swap;
                }

                if (source != frontPtr)
                {
                    var copyJob = new CopySectorBufferJob
                    {
                        Source = source,
                        Destination = frontPtr,
                        SectorCount = populationJob.SectorCount
                    };
                    handle = copyJob.Schedule(populationJob.SectorCount, JobBatchSize, handle);
                }

                var telemetryJob = new EcosystemTelemetryReductionJob
                {
                    Sectors = frontPtr,
                    Remainders = remainderPtr,
                    Telemetry = telemetry,
                    Counters = counters,
                    FaultFlags = faultFlagPtr,
                    SectorCount = populationJob.SectorCount,
                    TelemetryIndex = telemetrySlot,
                    FrameIndex = _simulationTick++,
                    DiffusionSteps = unchecked((uint)diffusionSteps),
                    IntegrationSubsteps = unchecked((uint)integrationSubsteps),
                    GlobalQualityWeight = tuning.GlobalQualityWeight
                };
                _activeJobHandle = telemetryJob.Schedule(handle);
                _scheduleTicks = Stopwatch.GetTimestamp();
                _jobScheduled = true;
                _telemetryCursor++;
                keepJobPins = true;
            }
            finally
            {
                if (!keepJobPins)
                    UnlockJobBuffers();
            }
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    RebindDataVaultForLifecycle(currentService as IDataVault, previousService as IDataVault);
                    if (_vault != null)
                        EnsureVaultState();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    UnregisterDispatcherRoutes();
                    if (currentService != null)
                        TryRegister();
                    break;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            CompleteScheduledJobForTeardown();
            UnlockJobBuffers();
            TryUnregister();
            ReleaseOwnedVaultHandles(_vault);
            ResetVaultHandles();
            _vault = null;
            _initialized = false;
        }

        private void Activate()
        {
            if (!Application.isPlaying)
                return;

            TryRegister();
            TryBindDataVaultCold();
            EnsureVaultState();
        }

        private void TryBindDataVaultCold()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || ReferenceEquals(_vault, vault))
                return;

            RebindDataVaultForLifecycle(vault, null);
        }

        private void RebindDataVaultForLifecycle(IDataVault nextVault, IDataVault releaseVaultFallback)
        {
            if (ReferenceEquals(_vault, nextVault))
                return;

            IDataVault releaseVault = _vault ?? releaseVaultFallback;
            CompleteScheduledJobForVaultSwapBarrier();
            UnlockJobBuffers();
            ReleaseOwnedVaultHandles(releaseVault);
            ResetVaultHandles();
            _vault = nextVault;
            _initialized = false;
            _dumpedFault = false;
            _telemetryCursor = 0;
            _lastTelemetrySlot = 0;
            _simulationTick = 0u;
            _csvTimestampTicks = 0L;
        }

        private bool EnsureVaultState()
        {
            MacroEcosystemLayoutManifest.VerifyColdBoot();

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (!OpenOrAcquireVaultBuffer(vault, ref _frontHandle, BufferID.ShinobuMacroEcosystemSectorFront, SectorCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquireVaultBuffer(vault, ref _backHandle, BufferID.ShinobuMacroEcosystemSectorBack, SectorCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquireVaultBuffer(vault, ref _remainderHandle, BufferID.ShinobuMacroEcosystemRemainders, SectorCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquireVaultBuffer(vault, ref _coordHandle, BufferID.ShinobuMacroEcosystemSectorCoords, SectorCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquireVaultBuffer(vault, ref _indexEntryHandle, BufferID.ShinobuMacroEcosystemIndexEntries, IndexCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquireVaultBuffer(vault, ref _biomeSpecHandle, BufferID.ShinobuMacroEcosystemBiomeSpecs, BiomeSpecCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquireVaultBuffer(vault, ref _tuningHandle, BufferID.ShinobuMacroEcosystemTuning, 1, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquireVaultBuffer(vault, ref _counterHandle, BufferID.ShinobuMacroEcosystemCounters, CounterCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquireVaultBuffer(vault, ref _telemetryHandle, BufferID.ShinobuMacroEcosystemTelemetryRing, TelemetryCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquireVaultBuffer(vault, ref _csvScratchHandle, BufferID.ShinobuMacroEcosystemCsvScratch, CsvScratchBytes, NativeArrayOptions.UninitializedMemory, out _) ||
                !OpenOrAcquireVaultBuffer(vault, ref _faultFlagHandle, BufferID.ShinobuMacroEcosystemFaultFlags, SectorCapacity, NativeArrayOptions.UninitializedMemory, out _))
            {
                return false;
            }

            if (!_initialized)
                GenerateEmergencyMockEcosystem(vault);

            return _initialized;
        }

        private bool HasVaultStateReady()
        {
            return _initialized &&
                   _vault != null &&
                   IsMatchingVaultHandle(in _frontHandle, BufferID.ShinobuMacroEcosystemSectorFront) &&
                   IsMatchingVaultHandle(in _backHandle, BufferID.ShinobuMacroEcosystemSectorBack) &&
                   IsMatchingVaultHandle(in _remainderHandle, BufferID.ShinobuMacroEcosystemRemainders) &&
                   IsMatchingVaultHandle(in _coordHandle, BufferID.ShinobuMacroEcosystemSectorCoords) &&
                   IsMatchingVaultHandle(in _indexEntryHandle, BufferID.ShinobuMacroEcosystemIndexEntries) &&
                   IsMatchingVaultHandle(in _biomeSpecHandle, BufferID.ShinobuMacroEcosystemBiomeSpecs) &&
                   IsMatchingVaultHandle(in _tuningHandle, BufferID.ShinobuMacroEcosystemTuning) &&
                   IsMatchingVaultHandle(in _counterHandle, BufferID.ShinobuMacroEcosystemCounters) &&
                   IsMatchingVaultHandle(in _telemetryHandle, BufferID.ShinobuMacroEcosystemTelemetryRing) &&
                   IsMatchingVaultHandle(in _csvScratchHandle, BufferID.ShinobuMacroEcosystemCsvScratch) &&
                   IsMatchingVaultHandle(in _faultFlagHandle, BufferID.ShinobuMacroEcosystemFaultFlags);
        }

        private void GenerateEmergencyMockEcosystem(IDataVault vault)
        {
            if (!TryOpenVaultBuffer(vault, ref _frontHandle, BufferID.ShinobuMacroEcosystemSectorFront, SectorCapacity, out NativeArray<EcosystemSectorDTO> front) ||
                !TryOpenVaultBuffer(vault, ref _backHandle, BufferID.ShinobuMacroEcosystemSectorBack, SectorCapacity, out NativeArray<EcosystemSectorDTO> back) ||
                !TryOpenVaultBuffer(vault, ref _remainderHandle, BufferID.ShinobuMacroEcosystemRemainders, SectorCapacity, out NativeArray<EcosystemSectorRemainderDTO> remainders) ||
                !TryOpenVaultBuffer(vault, ref _coordHandle, BufferID.ShinobuMacroEcosystemSectorCoords, SectorCapacity, out NativeArray<EcosystemSectorCoordDTO> coords) ||
                !TryOpenVaultBuffer(vault, ref _indexEntryHandle, BufferID.ShinobuMacroEcosystemIndexEntries, IndexCapacity, out NativeArray<EcosystemSectorIndexEntryDTO> indexEntries) ||
                !TryOpenVaultBuffer(vault, ref _biomeSpecHandle, BufferID.ShinobuMacroEcosystemBiomeSpecs, 1, out NativeArray<BiomeEcosystemSpecDTO> biomeSpecs) ||
                !TryOpenVaultBuffer(vault, ref _tuningHandle, BufferID.ShinobuMacroEcosystemTuning, 1, out NativeArray<MacroEcosystemTuningDTO> tuning) ||
                !TryOpenVaultBuffer(vault, ref _counterHandle, BufferID.ShinobuMacroEcosystemCounters, CounterCapacity, out NativeArray<MacroEcosystemCounterDTO> counters) ||
                !TryOpenVaultBuffer(vault, ref _telemetryHandle, BufferID.ShinobuMacroEcosystemTelemetryRing, TelemetryCapacity, out NativeArray<MacroEcosystemTelemetryEntry> telemetry) ||
                !TryOpenVaultBuffer(vault, ref _faultFlagHandle, BufferID.ShinobuMacroEcosystemFaultFlags, SectorCapacity, out NativeArray<uint> faultFlags))
            {
                return;
            }

            MacroEcosystemTuningDTO defaultTuning = MacroEcosystemTuningDTO.CreateDefault();
            defaultTuning.GlobalQualityWeight = ResolveGlobalQualityWeight();
            tuning[0] = defaultTuning;

            var clearJob = new ClearMacroEcosystemVaultTablesJob
            {
                IndexEntries = indexEntries,
                BiomeSpecs = biomeSpecs,
                Counters = counters,
                Telemetry = telemetry,
                FaultFlags = faultFlags
            };

            var mockJob = new GenerateEmergencyMockEcosystemJob
            {
                Front = (EcosystemSectorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(front),
                Back = (EcosystemSectorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(back),
                Remainders = (EcosystemSectorRemainderDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(remainders),
                Coords = (EcosystemSectorCoordDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(coords),
                FaultFlags = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr(faultFlags),
                Width = GridWidth,
                Height = GridHeight,
                OriginX = -(GridWidth / 2),
                OriginZ = -(GridHeight / 2)
            };

            var indexJob = new BuildSectorIndexJob
            {
                Sectors = (EcosystemSectorDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(front),
                IndexEntries = (EcosystemSectorIndexEntryDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(indexEntries),
                SectorCount = SectorCapacity,
                IndexCapacity = IndexCapacity
            };

            // COLD SYNC JOB: first boot writes every uninitialized Vault sector before any reader can observe it.
            JobHandle clearHandle = clearJob.Schedule();
            JobHandle mockHandle = mockJob.Schedule(SectorCapacity, JobBatchSize, clearHandle);
            JobHandle indexHandle = indexJob.Schedule(mockHandle);
            ForceCompleteColdBootstrapInPostSimulationWindow(ref indexHandle);
            _initialized = true;
        }

        private bool TryGetSectorBiomass(long sectorX, long sectorY, long sectorZ, out float preyBiomass01, out float predatorBiomass01, out float carryingCapacity01)
        {
            preyBiomass01 = 0f;
            predatorBiomass01 = 0f;
            carryingCapacity01 = 0f;
            IDataVault vault = _vault;
            if (vault == null)
                return false;
            if (_jobScheduled)
                return false;

            if (!TryOpenVaultBuffer(vault, ref _tuningHandle, BufferID.ShinobuMacroEcosystemTuning, 1, out NativeArray<MacroEcosystemTuningDTO> tuning))
                return false;

            MacroEcosystemTuningDTO tuneBefore = tuning[0];
            if (!IsMacroEcosystemSnapshotReadable(tuneBefore))
                return false;

            ulong hash = MacroEcosystemMath.ComputeSectorHash(sectorX, sectorY, sectorZ);
            if (!TryResolveSectorIndex(vault, hash, out int index))
                return false;

            if (!TryOpenVaultBuffer(vault, ref _frontHandle, BufferID.ShinobuMacroEcosystemSectorFront, SectorCapacity, out NativeArray<EcosystemSectorDTO> front) ||
                (uint)index >= (uint)front.Length)
                return false;

            EcosystemSectorDTO sector = front[index];
            MacroEcosystemTuningDTO tuneAfter = tuning[0];
            if (!IsMacroEcosystemSnapshotReadable(tuneAfter) ||
                !MacroEcosystemTuningMatchesForDirectRead(tuneBefore, tuneAfter))
            {
                return false;
            }

            MacroEcosystemTuningDTO t = MacroEcosystemTuningDTO.Sanitize(tuneBefore);
            float fallbackCapacity = math.max(1f, t.CarryingCapacityPrey + t.CarryingCapacityPredator);
            float sectorCapacity = math.max(1f, math.select(fallbackCapacity, sector.CarryingCapacity, math.isfinite(sector.CarryingCapacity) & sector.CarryingCapacity > 0f));
            preyBiomass01 = math.saturate(sector.PreyBiomass * math.rcp(sectorCapacity));
            predatorBiomass01 = math.saturate(sector.PredatorBiomass * math.rcp(sectorCapacity));
            MacroEcosystemTuningDTO fallback = MacroEcosystemTuningDTO.CreateDefault();
            carryingCapacity01 = math.saturate(sectorCapacity * math.rcp(fallback.CarryingCapacityPrey + fallback.CarryingCapacityPredator));
            return true;
        }

        private bool TryResolveSectorIndex(IDataVault vault, ulong sectorHash, out int sectorIndex)
        {
            sectorIndex = -1;
            if (!TryOpenVaultBuffer(vault, ref _indexEntryHandle, BufferID.ShinobuMacroEcosystemIndexEntries, IndexCapacity, out NativeArray<EcosystemSectorIndexEntryDTO> entries) ||
                sectorHash == 0UL)
                return false;

            int slot = MacroEcosystemMath.ResolveOpenAddressSlot(sectorHash, entries.Length);
            for (int probe = 0; probe < entries.Length; probe++)
            {
                EcosystemSectorIndexEntryDTO entry = entries[slot];
                if (entry.Occupied == 0u)
                    return false;

                if (entry.SectorHash == sectorHash)
                {
                    sectorIndex = entry.Slot;
                    return sectorIndex >= 0 && sectorIndex < SectorCapacity;
                }

                slot++;
                if (slot == entries.Length)
                    slot = 0;
            }

            return false;
        }

        private bool TryGetSectorSpawnWeights(long sectorX, long sectorY, long sectorZ, out float predatorWeight01, out float rareResourceWeight01)
        {
            predatorWeight01 = 0f;
            rareResourceWeight01 = 0f;
            IDataVault vault = _vault;
            if (vault == null)
                return false;
            if (_jobScheduled)
                return false;

            if (!TryOpenVaultBuffer(vault, ref _tuningHandle, BufferID.ShinobuMacroEcosystemTuning, 1, out NativeArray<MacroEcosystemTuningDTO> tuning))
                return false;

            MacroEcosystemTuningDTO tuneBefore = tuning[0];
            if (!IsMacroEcosystemSnapshotReadable(tuneBefore))
                return false;

            ulong hash = MacroEcosystemMath.ComputeSectorHash(sectorX, sectorY, sectorZ);
            if (!TryResolveSectorIndex(vault, hash, out int index))
                return false;

            if (!TryOpenVaultBuffer(vault, ref _frontHandle, BufferID.ShinobuMacroEcosystemSectorFront, SectorCapacity, out NativeArray<EcosystemSectorDTO> front) ||
                (uint)index >= (uint)front.Length)
                return false;

            EcosystemSectorDTO sector = front[index];
            MacroEcosystemTuningDTO tuneAfter = tuning[0];
            if (!IsMacroEcosystemSnapshotReadable(tuneAfter) ||
                !MacroEcosystemTuningMatchesForDirectRead(tuneBefore, tuneAfter))
            {
                return false;
            }

            MacroEcosystemTuningDTO t = MacroEcosystemTuningDTO.Sanitize(tuneBefore);
            float fallbackCapacity = math.max(1f, t.CarryingCapacityPrey + t.CarryingCapacityPredator);
            float sectorCapacity = math.max(1f, math.select(fallbackCapacity, sector.CarryingCapacity, math.isfinite(sector.CarryingCapacity) & sector.CarryingCapacity > 0f));
            float predatorMass = sector.PredatorBiomass * math.rcp(sectorCapacity);
            float floraStarvation = 1f - math.saturate(sector.FloraBiomass * math.rcp(sectorCapacity));
            predatorWeight01 = math.saturate(predatorMass);
            rareResourceWeight01 = math.saturate(floraStarvation * 0.45f + predatorWeight01 * 0.35f);
            return true;
        }

        private static bool IsMacroEcosystemSnapshotReadable(MacroEcosystemTuningDTO tune)
        {
            return (tune.Flags & MacroEcosystemMath.TuningFlagSnapshotWriteInFlight) == 0u;
        }

        private static bool MacroEcosystemTuningMatchesForDirectRead(
            MacroEcosystemTuningDTO before,
            MacroEcosystemTuningDTO after)
        {
            return before.Flags == after.Flags &&
                   before.StateHash == after.StateHash &&
                   before.CarryingCapacityPrey == after.CarryingCapacityPrey &&
                   before.CarryingCapacityPredator == after.CarryingCapacityPredator &&
                   before.TemperatureOptimum == after.TemperatureOptimum &&
                   before.TemperatureHalfRange == after.TemperatureHalfRange;
        }

        private void TryRegister()
        {
            if (!_registeredHotSwap)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredPostSimulation)
            {
                if (_postSimulationPhase == null)
                    _postSimulationPhase = new PostSimulationPhaseSystem(this);

                _registeredPostSimulation = GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase);
            }

            if (!_registeredPostSimulation)
                return;

            if (!_registeredFrost)
                _registeredFrost = GlobalRegistry.TryRegisterFrostTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            UnregisterDispatcherRoutes();

            if (_registeredHotSwap)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }
        }

        private void UnregisterDispatcherRoutes()
        {
            if (_registeredFrost)
            {
                GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Environment);
                _registeredFrost = false;
            }

            if (_registeredPostSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
                _registeredPostSimulation = false;
            }
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            if (_jobPinsHeld)
                return true;
            if (vault == null)
                return false;

            _jobPinVault = vault;
            try
            {
                if (!TryLockJobBuffer(vault, BufferID.ShinobuMacroEcosystemSectorFront, JobPinSectorFront) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuMacroEcosystemSectorBack, JobPinSectorBack) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuMacroEcosystemRemainders, JobPinRemainders) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuMacroEcosystemSectorCoords, JobPinSectorCoords) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuMacroEcosystemBiomeSpecs, JobPinBiomeSpecs) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuMacroEcosystemTuning, JobPinTuning) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuMacroEcosystemCounters, JobPinCounters) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuMacroEcosystemFaultFlags, JobPinFaultFlags) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuMacroEcosystemTelemetryRing, JobPinTelemetryRing))
                    return false;

                _jobPinsHeld = true;
                return true;
            }
            finally
            {
                if (!_jobPinsHeld)
                    UnlockJobBuffers();
            }
        }

        private void UnlockJobBuffers()
        {
            if (!_jobPinsHeld && _jobPinMask == 0u)
                return;

            IDataVault vault = _jobPinVault;
            uint pinMask = _jobPinMask;
            _jobPinVault = null;
            _jobPinMask = 0u;
            _jobPinsHeld = false;
            if (vault == null)
                return;

            TryUnlockJobBuffer(vault, pinMask, JobPinTelemetryRing, BufferID.ShinobuMacroEcosystemTelemetryRing);
            TryUnlockJobBuffer(vault, pinMask, JobPinFaultFlags, BufferID.ShinobuMacroEcosystemFaultFlags);
            TryUnlockJobBuffer(vault, pinMask, JobPinCounters, BufferID.ShinobuMacroEcosystemCounters);
            TryUnlockJobBuffer(vault, pinMask, JobPinTuning, BufferID.ShinobuMacroEcosystemTuning);
            TryUnlockJobBuffer(vault, pinMask, JobPinBiomeSpecs, BufferID.ShinobuMacroEcosystemBiomeSpecs);
            TryUnlockJobBuffer(vault, pinMask, JobPinSectorCoords, BufferID.ShinobuMacroEcosystemSectorCoords);
            TryUnlockJobBuffer(vault, pinMask, JobPinRemainders, BufferID.ShinobuMacroEcosystemRemainders);
            TryUnlockJobBuffer(vault, pinMask, JobPinSectorBack, BufferID.ShinobuMacroEcosystemSectorBack);
            TryUnlockJobBuffer(vault, pinMask, JobPinSectorFront, BufferID.ShinobuMacroEcosystemSectorFront);
        }

        private bool TryLockJobBuffer(IDataVault vault, BufferID bufferId, uint pinBit)
        {
            if ((_jobPinMask & pinBit) != 0u)
                return true;

            if (vault == null || !vault.TryLockBuffer(bufferId, SystemID.AIEcology))
                return false;

            _jobPinMask |= pinBit;
            return true;
        }

        private static void TryUnlockJobBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, SystemID.AIEcology);
        }

        private void TryFinalizeScheduledJobNoWait()
        {
            if (!_jobScheduled)
                return;

            if (!_activeJobHandle.IsCompleted)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _activeJobHandle))
                return;

            FinishCompletedScheduledJob();
        }

        private void CompleteScheduledJobForTeardown()
        {
            // [BLOCKING_SYNC_POINT] Teardown cannot release Vault locks while the owner job may still write.
            CompleteScheduledJobForTeardownOrVaultSwapBarrierBlocking();
        }

        private void CompleteScheduledJobForVaultSwapBarrier()
        {
            // [BLOCKING_SYNC_POINT] DataVault replacement cannot swap handles while the old writer job is active.
            CompleteScheduledJobForTeardownOrVaultSwapBarrierBlocking();
        }

        private void CompleteScheduledJobForTeardownOrVaultSwapBarrierBlocking()
        {
            if (!_jobScheduled)
                return;

            if (!ForceCompleteScheduledJobInPostSimulationWindow())
                return;

            FinishCompletedScheduledJob();
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            TryFinalizeScheduledJobNoWait();
        }

        private void FinishCompletedScheduledJob()
        {
            long now = Stopwatch.GetTimestamp();
            float micros = ResolveElapsedMicroseconds(_scheduleTicks, now);

            try
            {
                IDataVault vault = _vault;
                if (vault != null)
                {
                    ClearSnapshotWriteInFlight(vault);
                    PatchCompletedTelemetry(vault, micros);
                }
            }
            finally
            {
                _jobScheduled = false;
                UnlockJobBuffers();
            }
        }

        private bool ForceCompleteScheduledJobInPostSimulationWindow()
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                return DispatcherJobFence.TryComplete(ref _activeJobHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private static bool ForceCompleteColdBootstrapInPostSimulationWindow(ref JobHandle handle)
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                return DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private static float ResolveElapsedMicroseconds(long startTicks, long finishTicks)
        {
            if (startTicks <= 0L ||
                finishTicks < startTicks ||
                Stopwatch.Frequency <= 0)
            {
                return 0f;
            }

            double elapsed = (finishTicks - startTicks) * 1000000.0d / Stopwatch.Frequency;
            if (elapsed <= 0.0d)
                return 0f;
            return elapsed > float.MaxValue ? float.MaxValue : (float)elapsed;
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

        private void ClearSnapshotWriteInFlight(IDataVault vault)
        {
            if (!TryOpenVaultBuffer(vault, ref _tuningHandle, BufferID.ShinobuMacroEcosystemTuning, 1, out NativeArray<MacroEcosystemTuningDTO> tuning))
                return;

            MacroEcosystemTuningDTO value = tuning[0];
            value.Flags &= ~MacroEcosystemMath.TuningFlagSnapshotWriteInFlight;
            tuning[0] = value;
        }

        private void PatchCompletedTelemetry(IDataVault vault, float solverMicros)
        {
            if (!TryOpenVaultBuffer(vault, ref _telemetryHandle, BufferID.ShinobuMacroEcosystemTelemetryRing, TelemetryCapacity, out NativeArray<MacroEcosystemTelemetryEntry> telemetry) ||
                (uint)_lastTelemetrySlot >= (uint)telemetry.Length)
                return;

            MacroEcosystemTelemetryEntry entry = telemetry[_lastTelemetrySlot];
            bool validTiming = math.isfinite(solverMicros) && solverMicros >= 0f;
            entry.SolverMicroseconds = math.select(0f, solverMicros, validTiming);
            entry.DiffusionSteps = unchecked((uint)_lastDiffusionSteps);
            entry.TimingMode = MacroEcosystemTelemetryEntry.TimingModeScheduleToFinalize;
            entry.TimingSourceHash = MacroEcosystemTelemetryEntry.TimingSourceStopwatchDispatcherFence;
            entry.Flags |= MacroEcosystemTelemetryEntry.FlagTimingScheduleToFinalize;
            if (!validTiming)
                entry.Flags |= MacroEcosystemTelemetryEntry.FlagInvalidMath;
            if (entry.SolverMicroseconds > 2000f)
                entry.Flags |= MacroEcosystemTelemetryEntry.FlagSolveOverBudget;
            telemetry[_lastTelemetrySlot] = entry;

            bool fault = (entry.Flags & (MacroEcosystemTelemetryEntry.FlagInvalidMath | MacroEcosystemTelemetryEntry.FlagSolveOverBudget)) != 0u;
            if (fault && !_dumpedFault)
            {
                _dumpedFault = true;
                DumpTelemetry(vault);
            }
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly MacroEcosystemMathematicianRuntime _owner;

            public PostSimulationPhaseSystem(MacroEcosystemMathematicianRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => PostSimulationSystemHash;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PostSimulation;

            public byte GetBucketId() => byte.MaxValue;

            public int GetDependencyCount() => 0;

            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public void PreSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                _owner?.PostSimulationTick(in timing);
            }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
            }
        }

        private unsafe void DumpTelemetry(IDataVault vault)
        {
            if (!TryOpenVaultBuffer(vault, ref _telemetryHandle, BufferID.ShinobuMacroEcosystemTelemetryRing, TelemetryCapacity, out NativeArray<MacroEcosystemTelemetryEntry> telemetry))
                return;

            NativeArray<byte> payload = default;
            try
            {
                int stride = UnsafeUtility.SizeOf<MacroEcosystemTelemetryEntry>();
                int telemetryBytes = telemetry.Length * stride;
                int byteCount = 24 + telemetryBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(MacroEcosystemMathematicianRuntime),
                    "macroEcosystemTelemetryDumpPayload");
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                Span<byte> bytes = new Span<byte>(target, byteCount);
                WriteUInt64(bytes.Slice(0, 8), DumpMagic);
                WriteUInt32(bytes.Slice(8, 4), unchecked((uint)TelemetryCapacity));
                WriteUInt32(bytes.Slice(12, 4), unchecked((uint)stride));
                WriteUInt32(bytes.Slice(16, 4), unchecked((uint)_telemetryCursor));
                WriteUInt32(bytes.Slice(20, 4), RouteHash);
                if (telemetryBytes > 0)
                {
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    UnsafeUtility.MemCpy(target + 24, ptr, telemetryBytes);
                }

                if (!NativeFaultDumpWriter.TryWriteAll(DumpRelativePath, payload, byteCount))
                    GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)RouteHash));
            }
            catch (IOException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)RouteHash));
            }
            catch (UnauthorizedAccessException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)RouteHash));
            }
            catch (ArgumentException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)RouteHash));
            }
            catch (NotSupportedException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)RouteHash));
            }
            catch (InvalidOperationException)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)RouteHash));
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(MacroEcosystemMathematicianRuntime),
                    "macroEcosystemTelemetryDumpPayload");
            }
        }

#if UNITY_EDITOR
        private unsafe void TryLoadBiomeSpecsCsv()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return;

            string path = BuildCsvPath();
            if (path == null || path.Length == 0 || !File.Exists(path))
                return;

            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
            if (lastWriteUtc.Ticks == _csvTimestampTicks)
                return;

            if (System.Threading.Interlocked.CompareExchange(ref s_biomeCsvImportScratchBusy, 1, 0) != 0)
                return;

            try
            {
                int bytesRead = ReadCsvBytesCold(path, s_biomeCsvImportScratch, CsvScratchBytes);
                if (bytesRead <= 0)
                    return;

                int parsed = MacroEcosystemCsvParser.ParseBiomeSpecs(
                    new ReadOnlySpan<byte>(s_biomeCsvImportScratch, 0, bytesRead),
                    s_biomeSpecImportScratch);

                if (!TryCommitBiomeSpecs(vault) || !TryCommitBiomeSpecImportCounter(vault, parsed))
                    return;

                _csvTimestampTicks = lastWriteUtc.Ticks;
            }
            catch (IOException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x42494353u, RouteHash, 0f);
            }
            catch (UnauthorizedAccessException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x42494353u, RouteHash, 0f);
            }
            catch (ArgumentException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x42494353u, RouteHash, 0f);
            }
            catch (NotSupportedException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x42494353u, RouteHash, 0f);
            }
            catch (InvalidOperationException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x42494353u, RouteHash, 0f);
            }
            finally
            {
                System.Threading.Volatile.Write(ref s_biomeCsvImportScratchBusy, 0);
            }
        }

        private static int ReadCsvBytesCold(string path, byte[] scratch, int maxBytes)
        {
            if (scratch == null || scratch.Length <= 0 || maxBytes <= 0)
                return 0;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long boundedLength = stream.Length < maxBytes ? stream.Length : maxBytes;
                int byteCount = boundedLength > scratch.Length ? scratch.Length : (int)boundedLength;
                return byteCount > 0 ? stream.Read(scratch, 0, byteCount) : 0;
            }
        }

        private bool TryCommitBiomeSpecs(IDataVault vault)
        {
            if (vault == null || !vault.TryAcquireMutationGuard(BiomeSpecImportMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenVaultBuffer(
                        vault,
                        ref _biomeSpecHandle,
                        BufferID.ShinobuMacroEcosystemBiomeSpecs,
                        BiomeSpecCapacity,
                        out NativeArray<BiomeEcosystemSpecDTO> specs) ||
                    !specs.IsCreated ||
                    specs.Length <= 0)
                {
                    return false;
                }

                int copyCount = math.min(s_biomeSpecImportScratch.Length, specs.Length);
                int byteCount = copyCount * UnsafeUtility.SizeOf<BiomeEcosystemSpecDTO>();
                void* destination = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(specs);
                fixed (BiomeEcosystemSpecDTO* source = s_biomeSpecImportScratch)
                {
                    UnsafeUtility.MemCpy(destination, source, byteCount);
                }

                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(BiomeSpecImportMutationGuardMask);
            }
        }

        private bool TryCommitBiomeSpecImportCounter(IDataVault vault, int parsed)
        {
            if (vault == null || !vault.TryAcquireMutationGuard(BiomeSpecImportCounterMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenVaultBuffer(
                        vault,
                        ref _counterHandle,
                        BufferID.ShinobuMacroEcosystemCounters,
                        CounterCapacity,
                        out NativeArray<MacroEcosystemCounterDTO> counters) ||
                    !counters.IsCreated ||
                    counters.Length <= 6)
                {
                    return false;
                }

                counters[6] = MacroEcosystemCounterDTO.FromValue(parsed);
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(BiomeSpecImportCounterMutationGuardMask);
            }
        }

        private static string BuildCsvPath()
        {
            string dataPath = Application.dataPath;
            string first = Path.Combine(dataPath, "_Project", "Data", CsvFileName);
            if (File.Exists(first))
                return first;

            string legacyFirst = Path.Combine(dataPath, "_Project", "Data", LegacyCsvFileName);
            if (File.Exists(legacyFirst))
                return legacyFirst;

            DirectoryInfo root = Directory.GetParent(dataPath);
            if (root == null)
                return first;

            string rootFirst = Path.Combine(root.FullName, "Data", CsvFileName);
            if (File.Exists(rootFirst))
                return rootFirst;

            string legacyRoot = Path.Combine(root.FullName, "Data", LegacyCsvFileName);
            return File.Exists(legacyRoot) ? legacyRoot : rootFirst;
        }
#endif

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, weight, math.isfinite(weight)));
        }

        private static void ResolveSectorCoordFromAup(double3 absoluteUniversePosition, out long sectorX, out long sectorY, out long sectorZ)
        {
            const double invSectorSize = 1.0 / SectorSizeMeters;
            sectorX = (long)math.floor(absoluteUniversePosition.x * invSectorSize);
            // Macro biomass is a horizontal regional layer; depth effects are presentation/profile scalars, not sector identity.
            sectorY = 0L;
            sectorZ = (long)math.floor(absoluteUniversePosition.z * invSectorSize);
        }

        private static bool OpenOrAcquireVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryOpenVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null)
            {
                buffer = default;
                return false;
            }

            ReleaseOwnedVaultHandle(vault, ref handle);
            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.AIEcology,
                options);
            if (TryOpenVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            handle = default;
            buffer = default;
            return false;
        }

        private static bool TryOpenVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsMatchingVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsMatchingVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.Generation != 0u;
        }

        private static void ResetVaultHandles()
        {
            MacroEcosystemMathematicianRuntime runtime = s_runtime;
            if (runtime == null)
                return;

            runtime._frontHandle = default;
            runtime._backHandle = default;
            runtime._remainderHandle = default;
            runtime._coordHandle = default;
            runtime._indexEntryHandle = default;
            runtime._biomeSpecHandle = default;
            runtime._tuningHandle = default;
            runtime._counterHandle = default;
            runtime._telemetryHandle = default;
            runtime._csvScratchHandle = default;
            runtime._faultFlagHandle = default;
        }

        private void ReleaseOwnedVaultHandles(IDataVault vault)
        {
            ReleaseOwnedVaultHandle(vault, ref _frontHandle);
            ReleaseOwnedVaultHandle(vault, ref _backHandle);
            ReleaseOwnedVaultHandle(vault, ref _remainderHandle);
            ReleaseOwnedVaultHandle(vault, ref _coordHandle);
            ReleaseOwnedVaultHandle(vault, ref _indexEntryHandle);
            ReleaseOwnedVaultHandle(vault, ref _biomeSpecHandle);
            ReleaseOwnedVaultHandle(vault, ref _tuningHandle);
            ReleaseOwnedVaultHandle(vault, ref _counterHandle);
            ReleaseOwnedVaultHandle(vault, ref _telemetryHandle);
            ReleaseOwnedVaultHandle(vault, ref _csvScratchHandle);
            ReleaseOwnedVaultHandle(vault, ref _faultFlagHandle);
        }

        private static void ReleaseOwnedVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null &&
                handle.BufferID != 0u &&
                handle.Generation != 0u &&
                handle.SystemID == (uint)SystemID.AIEcology)
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private static void WriteUInt32(Span<byte> target, uint value)
        {
            target[0] = (byte)value;
            target[1] = (byte)(value >> 8);
            target[2] = (byte)(value >> 16);
            target[3] = (byte)(value >> 24);
        }

        private static void WriteUInt64(Span<byte> target, ulong value)
        {
            WriteUInt32(target, unchecked((uint)value));
            WriteUInt32(target.Slice(4, 4), unchecked((uint)(value >> 32)));
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct EcosystemSectorCoordDTO
    {
        [FieldOffset(0)] public long SectorX;
        [FieldOffset(8)] public long SectorY;
        [FieldOffset(16)] public long SectorZ;
        [FieldOffset(24)] public uint BiomeHash;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct EcosystemSectorRemainderDTO
    {
        [FieldOffset(0)] public float PreyFraction;
        [FieldOffset(4)] public float PredatorFraction;
        [FieldOffset(8)] public uint LastDiffusionTransfers;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct EcosystemSectorIndexEntryDTO
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public int Slot;
        [FieldOffset(12)] public uint Occupied;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BiomeEcosystemSpecDTO
    {
        [FieldOffset(0)] public uint BiomeHash;
        [FieldOffset(4)] public float CarryingCapacityPrey;
        [FieldOffset(8)] public float CarryingCapacityPredator;
        [FieldOffset(12)] public float MigrationResistance;
        [FieldOffset(16)] public float TemperatureOptimum;
        [FieldOffset(20)] public float ToxinPenalty;
        [FieldOffset(24)] public float BaseBirthRate;
        [FieldOffset(28)] public float PredationRate;
        [FieldOffset(32)] public float PredatorConversionRate;
        [FieldOffset(36)] public float PredatorStarvationRate;
        [FieldOffset(40)] private uint _pad0;
        [FieldOffset(44)] private uint _pad1;
        [FieldOffset(48)] private uint _pad2;
        [FieldOffset(52)] private uint _pad3;
        [FieldOffset(56)] private uint _pad4;
        [FieldOffset(60)] private uint _pad5;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MacroEcosystemTuningDTO
    {
        [FieldOffset(0)] public float BaseBirthRate;
        [FieldOffset(4)] public float PredationRate;
        [FieldOffset(8)] public float PredatorConversionRate;
        [FieldOffset(12)] public float PredatorStarvationRate;
        [FieldOffset(16)] public float CarryingCapacityPrey;
        [FieldOffset(20)] public float CarryingCapacityPredator;
        [FieldOffset(24)] public float MigrationRate;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public float FrostDeltaSeconds;
        [FieldOffset(36)] public float TemperatureOptimum;
        [FieldOffset(40)] public float TemperatureHalfRange;
        [FieldOffset(44)] public float ToxicityBirthSuppression;
        [FieldOffset(48)] public float ToxicityDeathBoost;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint Reserved;

        public static MacroEcosystemTuningDTO CreateDefault()
        {
            return new MacroEcosystemTuningDTO
            {
                BaseBirthRate = 0.034f,
                PredationRate = 0.0000015f,
                PredatorConversionRate = 0.00000055f,
                PredatorStarvationRate = 0.020f,
                CarryingCapacityPrey = 60000f,
                CarryingCapacityPredator = 12000f,
                MigrationRate = 0.045f,
                GlobalQualityWeight = 1f,
                FrostDeltaSeconds = 5f,
                TemperatureOptimum = 8f,
                TemperatureHalfRange = 18f,
                ToxicityBirthSuppression = 0.92f,
                ToxicityDeathBoost = 2.4f,
                Flags = 1u,
                StateHash = 0x4D45434Fu,
                Reserved = 0u
            };
        }

        public static MacroEcosystemTuningDTO Sanitize(MacroEcosystemTuningDTO value)
        {
            MacroEcosystemTuningDTO fallback = CreateDefault();
            value.BaseBirthRate = PositiveOr(value.BaseBirthRate, fallback.BaseBirthRate);
            value.PredationRate = PositiveOr(value.PredationRate, fallback.PredationRate);
            value.PredatorConversionRate = PositiveOr(value.PredatorConversionRate, fallback.PredatorConversionRate);
            value.PredatorStarvationRate = PositiveOr(value.PredatorStarvationRate, fallback.PredatorStarvationRate);
            value.CarryingCapacityPrey = PositiveOr(value.CarryingCapacityPrey, fallback.CarryingCapacityPrey);
            value.CarryingCapacityPredator = PositiveOr(value.CarryingCapacityPredator, fallback.CarryingCapacityPredator);
            value.MigrationRate = math.clamp(PositiveOr(value.MigrationRate, fallback.MigrationRate), 0.001f, 0.25f);
            value.GlobalQualityWeight = math.saturate(math.select(fallback.GlobalQualityWeight, value.GlobalQualityWeight, math.isfinite(value.GlobalQualityWeight)));
            value.FrostDeltaSeconds = PositiveOr(value.FrostDeltaSeconds, fallback.FrostDeltaSeconds);
            value.TemperatureOptimum = math.select(fallback.TemperatureOptimum, value.TemperatureOptimum, math.isfinite(value.TemperatureOptimum));
            value.TemperatureHalfRange = PositiveOr(value.TemperatureHalfRange, fallback.TemperatureHalfRange);
            value.ToxicityBirthSuppression = math.saturate(math.select(fallback.ToxicityBirthSuppression, value.ToxicityBirthSuppression, math.isfinite(value.ToxicityBirthSuppression)));
            value.ToxicityDeathBoost = PositiveOr(value.ToxicityDeathBoost, fallback.ToxicityDeathBoost);
            return value;
        }

        private static float PositiveOr(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MacroEcosystemTelemetryEntry
    {
        public const uint FlagInvalidMath = 1u << 0;
        public const uint FlagPopulationExplosion = 1u << 1;
        public const uint FlagSolveOverBudget = 1u << 2;
        public const uint FlagTimingScheduleToFinalize = 1u << 3;
        public const uint TimingModeUnspecified = 0u;
        public const uint TimingModeScheduleToFinalize = 1u;
        public const uint TimingSourceStopwatchDispatcherFence = 0x53574643u; // SWFC

        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public float TotalFloraBiomass;
        [FieldOffset(12)] public float TotalPreyBiomass;
        [FieldOffset(16)] public float TotalPredatorBiomass;
        [FieldOffset(20)] public uint DiffusionTransfers;
        [FieldOffset(24)] public float MaxPredatorDensity;
        [FieldOffset(28)] public float SolverMicroseconds;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public uint DiffusionSteps;
        [FieldOffset(40)] public uint IntegrationSubsteps;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public float TotalCarryingCapacity;
        [FieldOffset(52)] public uint DominantPredatorSectorHash;
        [FieldOffset(56)] public uint TimingMode;
        [FieldOffset(60)] public uint TimingSourceHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MacroEcosystemCounterDTO
    {
        [FieldOffset(0)] public int Value;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public ulong Reserved0;
        [FieldOffset(16)] public ulong Reserved1;
        [FieldOffset(24)] public ulong Reserved2;
        [FieldOffset(32)] public ulong Reserved3;
        [FieldOffset(40)] public ulong Reserved4;
        [FieldOffset(48)] public ulong Reserved5;
        [FieldOffset(56)] public ulong Reserved6;

        public static MacroEcosystemCounterDTO FromValue(int value)
        {
            return new MacroEcosystemCounterDTO
            {
                Value = value,
                Flags = 0u,
                Reserved0 = 0UL,
                Reserved1 = 0UL,
                Reserved2 = 0UL,
                Reserved3 = 0UL,
                Reserved4 = 0UL,
                Reserved5 = 0UL,
                Reserved6 = 0UL
            };
        }
    }

    internal static class MacroEcosystemMath
    {
        public const uint SectorFaultInvalidMath = 1u << 0;
        public const uint TuningFlagSnapshotWriteInFlight = MacroEcosystemVaultContract.TuningFlagSnapshotWriteInFlight;
        public const uint BiomeAbyssalPlain = 0x2B38B429u;
        public const uint BiomeThermalVent = 0x6F90CB76u;
        public const uint BiomeKelpTrench = 0x64E62B68u;
        public const uint BiomeReactorRuin = 0x213B297Cu;
        public const uint BiomeBrineLake = 0x2F4F2039u;
        public const uint DominantFlora = 1u << 0;
        public const uint DominantPrey = 1u << 1;
        public const uint DominantPredator = 1u << 2;
        private const int FloraDensityShift = 8;
        private const int PreyDensityShift = 16;
        private const int PredatorDensityShift = 24;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ComputeSectorHash(long sectorX, long sectorY, long sectorZ)
        {
            ulong hash = 14695981039346656037UL;
            hash = MixLong(hash, sectorX);
            hash = MixLong(hash, sectorY);
            hash = MixLong(hash, sectorZ);
            return hash == 0UL ? 1UL : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashLowerAscii(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash = (hash ^ b) * 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Mix32(uint hash, uint value)
        {
            hash ^= value + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            return hash == 0u ? 1u : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint SelectMockBiomeHash(uint noise)
        {
            uint bucket = noise % 5u;
            return bucket == 0u
                ? BiomeAbyssalPlain
                : bucket == 1u
                    ? BiomeThermalVent
                    : bucket == 2u
                        ? BiomeKelpTrench
                        : bucket == 3u
                            ? BiomeReactorRuin
                            : BiomeBrineLake;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveDiffusionSteps(float globalQualityWeight)
        {
            float curve = ResolveQualityCurve(globalQualityWeight);
            return math.clamp((int)math.lerp(1f, 5.999f, curve), 1, 5);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveIntegrationSubsteps(float globalQualityWeight)
        {
            float curve = ResolveQualityCurve(globalQualityWeight);
            return math.clamp((int)math.lerp(1f, 6.999f, curve), 1, 6);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveQualityFlowWeight(float globalQualityWeight)
        {
            float curve = ResolveQualityCurve(globalQualityWeight);
            return math.lerp(0.25f, 1f, curve);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveQualityCurve(float globalQualityWeight)
        {
            float q = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            float thermalBand = math.saturate((q - 0.2f) * math.rcp(0.8f));
            float polynomial = thermalBand * thermalBand * (3f - 2f * thermalBand);
            return polynomial;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveOpenAddressSlot(ulong hash, int capacity)
        {
            capacity = math.max(1, capacity);
            uint mixed = (uint)(hash ^ (hash >> 32));
            mixed ^= mixed >> 16;
            mixed *= 0x7FEB352Du;
            mixed ^= mixed >> 15;
            mixed *= 0x846CA68Bu;
            mixed ^= mixed >> 16;
            return (capacity & (capacity - 1)) == 0
                ? (int)(mixed & (uint)(capacity - 1))
                : (int)(mixed % (uint)capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TryResolveBiomeSpec(
            BiomeEcosystemSpecDTO* specs,
            int capacity,
            uint biomeHash,
            out BiomeEcosystemSpecDTO spec)
        {
            spec = default;
            if (specs == null || capacity <= 0 || biomeHash == 0u)
                return false;

            int slot = ResolveOpenAddressSlot(biomeHash, capacity);
            for (int probe = 0; probe < capacity; probe++)
            {
                BiomeEcosystemSpecDTO current = specs[slot];
                if (current.BiomeHash == 0u)
                    return false;
                if (current.BiomeHash == biomeHash)
                {
                    spec = current;
                    return true;
                }

                slot++;
                if (slot == capacity)
                    slot = 0;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveTemperatureSuitability(float temperature, float optimum, float halfRange)
        {
            float safeTemp = math.select(optimum, temperature, math.isfinite(temperature));
            float safeRange = math.max(0.001f, math.select(1f, halfRange, math.isfinite(halfRange)));
            return math.saturate(1f - math.abs(safeTemp - optimum) * math.rcp(safeRange));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint QuantizeBiomass(float value)
        {
            if (!math.isfinite(value) || value <= 0f)
                return 0u;
            if (value >= 4294967040f)
                return uint.MaxValue;
            return (uint)math.floor(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeBiomass(float value, float capacity)
        {
            float safeCapacity = math.max(0.0001f, math.select(1f, capacity, math.isfinite(capacity)));
            float safeValue = math.select(0f, value, math.isfinite(value));
            return math.clamp(safeValue, 0f, safeCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PackDominantSpeciesMask(float flora, float prey, float predator, float carryingCapacity)
        {
            float safeCapacity = math.max(0.0001f, math.select(1f, carryingCapacity, math.isfinite(carryingCapacity)));
            float flora01 = math.saturate(flora * math.rcp(safeCapacity));
            float prey01 = math.saturate(prey * math.rcp(safeCapacity));
            float predator01 = math.saturate(predator * math.rcp(safeCapacity));
            uint dominant = flora >= prey && flora >= predator
                ? DominantFlora
                : prey >= predator ? DominantPrey : DominantPredator;
            return dominant |
                   ((uint)math.round(flora01 * 255f) << FloraDensityShift) |
                   ((uint)math.round(prey01 * 255f) << PreyDensityShift) |
                   ((uint)math.round(predator01 * 255f) << PredatorDensityShift);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DecodePreyDensity01(uint mask)
        {
            return ((mask >> PreyDensityShift) & 0xFFu) * math.rcp(255f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DecodePredatorDensity01(uint mask)
        {
            return ((mask >> PredatorDensityShift) & 0xFFu) * math.rcp(255f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong MixLong(ulong hash, long value)
        {
            ulong v = unchecked((ulong)value);
            for (int shift = 0; shift < 64; shift += 8)
                hash = (hash ^ ((v >> shift) & 0xFFUL)) * 1099511628211UL;
            return hash;
        }
    }

    internal static class MacroEcosystemLayoutManifest
    {
        private static bool _verified;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void VerifyEditor()
        {
            VerifyColdBoot();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _verified = false;
        }

        internal static void VerifyColdBoot()
        {
            if (_verified)
                return;

            AssertSize<EcosystemSectorDTO>(64);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.SectorHash), 0);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.FloraBiomass), 8);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.PreyBiomass), 12);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.PredatorBiomass), 16);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.CarryingCapacity), 20);
            AssertOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.DominantSpeciesMask), 24);
            AssertOffset<EcosystemSectorDTO>("_pad0", 28);
            AssertOffset<EcosystemSectorDTO>("_pad8", 60);
            AssertSize<MacroEcosystemSectorVaultRecord>(64);
            AssertOffset<MacroEcosystemSectorVaultRecord>(nameof(MacroEcosystemSectorVaultRecord.SectorHash), 0);
            AssertOffset<MacroEcosystemSectorVaultRecord>(nameof(MacroEcosystemSectorVaultRecord.FloraBiomass), 8);
            AssertOffset<MacroEcosystemSectorVaultRecord>(nameof(MacroEcosystemSectorVaultRecord.PreyBiomass), 12);
            AssertOffset<MacroEcosystemSectorVaultRecord>(nameof(MacroEcosystemSectorVaultRecord.PredatorBiomass), 16);
            AssertOffset<MacroEcosystemSectorVaultRecord>(nameof(MacroEcosystemSectorVaultRecord.CarryingCapacity), 20);
            AssertOffset<MacroEcosystemSectorVaultRecord>(nameof(MacroEcosystemSectorVaultRecord.DominantSpeciesMask), 24);
            AssertOffset<MacroEcosystemSectorVaultRecord>("_pad0", 28);
            AssertOffset<MacroEcosystemSectorVaultRecord>("_pad8", 60);
            AssertSize<EcosystemSectorCoordDTO>(32);
            AssertOffset<EcosystemSectorCoordDTO>(nameof(EcosystemSectorCoordDTO.SectorX), 0);
            AssertOffset<EcosystemSectorCoordDTO>(nameof(EcosystemSectorCoordDTO.SectorY), 8);
            AssertOffset<EcosystemSectorCoordDTO>(nameof(EcosystemSectorCoordDTO.SectorZ), 16);
            AssertOffset<EcosystemSectorCoordDTO>(nameof(EcosystemSectorCoordDTO.BiomeHash), 24);
            AssertOffset<EcosystemSectorCoordDTO>(nameof(EcosystemSectorCoordDTO.Flags), 28);
            AssertSize<EcosystemSectorRemainderDTO>(16);
            AssertOffset<EcosystemSectorRemainderDTO>(nameof(EcosystemSectorRemainderDTO.PreyFraction), 0);
            AssertOffset<EcosystemSectorRemainderDTO>(nameof(EcosystemSectorRemainderDTO.PredatorFraction), 4);
            AssertOffset<EcosystemSectorRemainderDTO>(nameof(EcosystemSectorRemainderDTO.LastDiffusionTransfers), 8);
            AssertOffset<EcosystemSectorRemainderDTO>(nameof(EcosystemSectorRemainderDTO.Flags), 12);
            AssertSize<EcosystemSectorIndexEntryDTO>(16);
            AssertOffset<EcosystemSectorIndexEntryDTO>(nameof(EcosystemSectorIndexEntryDTO.SectorHash), 0);
            AssertOffset<EcosystemSectorIndexEntryDTO>(nameof(EcosystemSectorIndexEntryDTO.Slot), 8);
            AssertOffset<EcosystemSectorIndexEntryDTO>(nameof(EcosystemSectorIndexEntryDTO.Occupied), 12);
            AssertSize<MacroEcosystemSectorIndexRecord>(16);
            AssertOffset<MacroEcosystemSectorIndexRecord>(nameof(MacroEcosystemSectorIndexRecord.SectorHash), 0);
            AssertOffset<MacroEcosystemSectorIndexRecord>(nameof(MacroEcosystemSectorIndexRecord.Slot), 8);
            AssertOffset<MacroEcosystemSectorIndexRecord>(nameof(MacroEcosystemSectorIndexRecord.Occupied), 12);
            AssertSize<BiomeEcosystemSpecDTO>(64);
            AssertOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.BiomeHash), 0);
            AssertOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.CarryingCapacityPrey), 4);
            AssertOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.CarryingCapacityPredator), 8);
            AssertOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.MigrationResistance), 12);
            AssertOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.TemperatureOptimum), 16);
            AssertOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.ToxinPenalty), 20);
            AssertOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.BaseBirthRate), 24);
            AssertOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.PredationRate), 28);
            AssertOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.PredatorConversionRate), 32);
            AssertOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.PredatorStarvationRate), 36);
            AssertSize<MacroEcosystemTuningDTO>(64);
            AssertOffset<MacroEcosystemTuningDTO>(nameof(MacroEcosystemTuningDTO.BaseBirthRate), 0);
            AssertOffset<MacroEcosystemTuningDTO>(nameof(MacroEcosystemTuningDTO.MigrationRate), 24);
            AssertOffset<MacroEcosystemTuningDTO>(nameof(MacroEcosystemTuningDTO.GlobalQualityWeight), 28);
            AssertOffset<MacroEcosystemTuningDTO>(nameof(MacroEcosystemTuningDTO.Flags), 52);
            AssertOffset<MacroEcosystemTuningDTO>(nameof(MacroEcosystemTuningDTO.StateHash), 56);
            AssertOffset<MacroEcosystemTuningDTO>(nameof(MacroEcosystemTuningDTO.Reserved), 60);
            AssertSize<MacroEcosystemTuningVaultRecord>(64);
            AssertOffset<MacroEcosystemTuningVaultRecord>(nameof(MacroEcosystemTuningVaultRecord.BaseBirthRate), 0);
            AssertOffset<MacroEcosystemTuningVaultRecord>(nameof(MacroEcosystemTuningVaultRecord.MigrationRate), 24);
            AssertOffset<MacroEcosystemTuningVaultRecord>(nameof(MacroEcosystemTuningVaultRecord.GlobalQualityWeight), 28);
            AssertOffset<MacroEcosystemTuningVaultRecord>(nameof(MacroEcosystemTuningVaultRecord.Flags), 52);
            AssertOffset<MacroEcosystemTuningVaultRecord>(nameof(MacroEcosystemTuningVaultRecord.StateHash), 56);
            AssertOffset<MacroEcosystemTuningVaultRecord>(nameof(MacroEcosystemTuningVaultRecord.Reserved), 60);
            AssertSize<MacroEcosystemTelemetryEntry>(64);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.FrameIndex), 0);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.TotalFloraBiomass), 8);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.TotalPreyBiomass), 12);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.TotalPredatorBiomass), 16);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.DiffusionTransfers), 20);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.MaxPredatorDensity), 24);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.SolverMicroseconds), 28);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.Flags), 44);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.TotalCarryingCapacity), 48);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.TimingMode), 56);
            AssertOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.TimingSourceHash), 60);
            AssertSize<MacroEcosystemCounterDTO>(64);
            AssertOffset<MacroEcosystemCounterDTO>(nameof(MacroEcosystemCounterDTO.Value), 0);
            AssertOffset<MacroEcosystemCounterDTO>(nameof(MacroEcosystemCounterDTO.Flags), 4);
            AssertOffset<MacroEcosystemCounterDTO>(nameof(MacroEcosystemCounterDTO.Reserved0), 8);
            AssertOffset<MacroEcosystemCounterDTO>(nameof(MacroEcosystemCounterDTO.Reserved6), 56);
            _verified = true;
        }

        private static void AssertSize<T>(int expected) where T : unmanaged
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed != expected)
                throw new CriticalBootException("[MacroEcosystemLayout] Size mismatch.");
        }

        private static void AssertOffset<T>(string fieldName, int expected) where T : unmanaged
        {
            int observed = (int)Marshal.OffsetOf<T>(fieldName);
            if (observed != expected)
                throw new CriticalBootException("[MacroEcosystemLayout] Offset mismatch.");
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct ClearMacroEcosystemVaultTablesJob : IJob
    {
        [NoAlias] public NativeArray<EcosystemSectorIndexEntryDTO> IndexEntries;
        [NoAlias] public NativeArray<BiomeEcosystemSpecDTO> BiomeSpecs;
        [NoAlias] public NativeArray<MacroEcosystemCounterDTO> Counters;
        [NoAlias] public NativeArray<MacroEcosystemTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<uint> FaultFlags;

        public void Execute()
        {
            for (int i = 0; i < IndexEntries.Length; i++)
                IndexEntries[i] = default;
            for (int i = 0; i < BiomeSpecs.Length; i++)
                BiomeSpecs[i] = default;
            SeedDefaultBiomeSpecs(BiomeSpecs);
            for (int i = 0; i < Counters.Length; i++)
                Counters[i] = default;
            for (int i = 0; i < Telemetry.Length; i++)
                Telemetry[i] = default;
            for (int i = 0; i < FaultFlags.Length; i++)
                FaultFlags[i] = 0u;
            if (Counters.Length > 0)
                Counters[0] = MacroEcosystemCounterDTO.FromValue(1);
        }

        private static void SeedDefaultBiomeSpecs(NativeArray<BiomeEcosystemSpecDTO> specs)
        {
            InsertBiomeSpec(specs, new BiomeEcosystemSpecDTO
            {
                BiomeHash = MacroEcosystemMath.BiomeAbyssalPlain,
                CarryingCapacityPrey = 56000f,
                CarryingCapacityPredator = 9000f,
                MigrationResistance = 0.22f,
                TemperatureOptimum = 5f,
                ToxinPenalty = 0.35f
            });
            InsertBiomeSpec(specs, new BiomeEcosystemSpecDTO
            {
                BiomeHash = MacroEcosystemMath.BiomeThermalVent,
                CarryingCapacityPrey = 32000f,
                CarryingCapacityPredator = 14000f,
                MigrationResistance = 0.38f,
                TemperatureOptimum = 18f,
                ToxinPenalty = 0.62f
            });
            InsertBiomeSpec(specs, new BiomeEcosystemSpecDTO
            {
                BiomeHash = MacroEcosystemMath.BiomeKelpTrench,
                CarryingCapacityPrey = 72000f,
                CarryingCapacityPredator = 7000f,
                MigrationResistance = 0.18f,
                TemperatureOptimum = 9f,
                ToxinPenalty = 0.18f
            });
            InsertBiomeSpec(specs, new BiomeEcosystemSpecDTO
            {
                BiomeHash = MacroEcosystemMath.BiomeReactorRuin,
                CarryingCapacityPrey = 12000f,
                CarryingCapacityPredator = 3000f,
                MigrationResistance = 0.64f,
                TemperatureOptimum = 7f,
                ToxinPenalty = 0.95f
            });
            InsertBiomeSpec(specs, new BiomeEcosystemSpecDTO
            {
                BiomeHash = MacroEcosystemMath.BiomeBrineLake,
                CarryingCapacityPrey = 26000f,
                CarryingCapacityPredator = 5000f,
                MigrationResistance = 0.52f,
                TemperatureOptimum = 3f,
                ToxinPenalty = 0.78f
            });
        }

        private static void InsertBiomeSpec(NativeArray<BiomeEcosystemSpecDTO> specs, BiomeEcosystemSpecDTO spec)
        {
            if (specs.Length <= 0 || spec.BiomeHash == 0u)
                return;

            int slot = MacroEcosystemMath.ResolveOpenAddressSlot(spec.BiomeHash, specs.Length);
            for (int probe = 0; probe < specs.Length; probe++)
            {
                BiomeEcosystemSpecDTO existing = specs[slot];
                if (existing.BiomeHash == 0u || existing.BiomeHash == spec.BiomeHash)
                {
                    specs[slot] = spec;
                    return;
                }

                slot++;
                if (slot == specs.Length)
                    slot = 0;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct GenerateEmergencyMockEcosystemJob : IJobParallelFor
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Front;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Back;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorRemainderDTO* Remainders;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorCoordDTO* Coords;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public uint* FaultFlags;
        public int Width;
        public int Height;
        public long OriginX;
        public long OriginZ;

        public void Execute(int index)
        {
            int x = index % Width;
            int z = index / Width;
            long sectorX = OriginX + x;
            long sectorY = 0L;
            long sectorZ = OriginZ + z;
            ulong hash = MacroEcosystemMath.ComputeSectorHash(sectorX, sectorY, sectorZ);
            uint noise = (uint)(hash ^ (hash >> 32));
            noise = noise * 747796405u + 2891336453u;
            float n0 = (noise & 1023u) * math.rcp(1023f);
            float n1 = ((noise >> 10) & 255u) * math.rcp(255f);
            float n2 = ((noise >> 18) & 4095u) * math.rcp(4095f);
            float carryingCapacity = math.lerp(18000f, 96000f, n0);
            float flora = carryingCapacity * math.lerp(0.12f, 1.35f, n1);
            float prey = carryingCapacity * math.lerp(0.02f, 0.72f, n2);
            float predator = carryingCapacity * math.lerp(0.001f, 0.18f, 1f - n1);
            uint instabilityBucket = noise & 3u;
            flora = math.select(flora, carryingCapacity * 1.9f, instabilityBucket == 0u);
            prey = math.select(prey, carryingCapacity * 0.04f, instabilityBucket == 1u);
            predator = math.select(predator, 0f, instabilityBucket == 0u);
            predator = math.select(predator, carryingCapacity * 0.44f, instabilityBucket == 1u);
            flora = math.clamp(flora, 0f, carryingCapacity);
            prey = math.clamp(prey, 0f, carryingCapacity);
            predator = math.clamp(predator, 0f, carryingCapacity);
            EcosystemSectorDTO sector = new EcosystemSectorDTO
            {
                SectorHash = hash,
                FloraBiomass = flora,
                PreyBiomass = prey,
                PredatorBiomass = predator,
                CarryingCapacity = carryingCapacity,
                DominantSpeciesMask = MacroEcosystemMath.PackDominantSpeciesMask(flora, prey, predator, carryingCapacity)
            };

            Front[index] = sector;
            Back[index] = sector;
            Remainders[index] = default;
            FaultFlags[index] = 0u;
            Coords[index] = new EcosystemSectorCoordDTO
            {
                SectorX = sectorX,
                SectorY = sectorY,
                SectorZ = sectorZ,
                BiomeHash = MacroEcosystemMath.SelectMockBiomeHash(noise),
                Flags = 1u
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct BuildSectorIndexJob : IJob
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Sectors;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorIndexEntryDTO* IndexEntries;
        public int SectorCount;
        public int IndexCapacity;

        public void Execute()
        {
            for (int i = 0; i < SectorCount; i++)
            {
                ulong hash = Sectors[i].SectorHash;
                int slot = MacroEcosystemMath.ResolveOpenAddressSlot(hash, IndexCapacity);
                for (int probe = 0; probe < IndexCapacity; probe++)
                {
                    EcosystemSectorIndexEntryDTO existing = IndexEntries[slot];
                    if (existing.Occupied == 0u || existing.SectorHash == hash)
                    {
                        IndexEntries[slot] = new EcosystemSectorIndexEntryDTO
                        {
                            SectorHash = hash,
                            Slot = i,
                            Occupied = 1u
                        };
                        break;
                    }

                    slot++;
                    if (slot == IndexCapacity)
                        slot = 0;
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct EcosystemPopulationJob : IJobParallelFor
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Front;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Back;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorRemainderDTO* Remainders;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorCoordDTO* Coords;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public BiomeEcosystemSpecDTO* BiomeSpecs;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public uint* FaultFlags;
        public MacroEcosystemTuningDTO Tuning;
        public int IntegrationSubsteps;
        public int SectorCount;
        public int BiomeSpecCapacity;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)SectorCount)
                return;

            ref readonly EcosystemSectorDTO src = ref UnsafeUtility.AsRef<EcosystemSectorDTO>(Front + index);
            ref EcosystemSectorDTO dst = ref UnsafeUtility.AsRef<EcosystemSectorDTO>(Back + index);
            ref EcosystemSectorRemainderDTO rem = ref UnsafeUtility.AsRef<EcosystemSectorRemainderDTO>(Remainders + index);
            EcosystemSectorCoordDTO coord = Coords[index];
            bool hasSpec = MacroEcosystemMath.TryResolveBiomeSpec(BiomeSpecs, BiomeSpecCapacity, coord.BiomeHash, out BiomeEcosystemSpecDTO spec);
            float preyCapacity = hasSpec ? math.max(1f, spec.CarryingCapacityPrey) : math.max(1f, Tuning.CarryingCapacityPrey);
            float predatorCapacity = hasSpec ? math.max(1f, spec.CarryingCapacityPredator) : math.max(1f, Tuning.CarryingCapacityPredator);
            float carryingCapacity = math.max(1f, math.select(preyCapacity + predatorCapacity, src.CarryingCapacity, math.isfinite(src.CarryingCapacity) & src.CarryingCapacity > 0f));
            float toxinPenalty = hasSpec ? math.saturate(spec.ToxinPenalty) : 1f;

            float flora = MacroEcosystemMath.SanitizeBiomass(src.FloraBiomass, carryingCapacity);
            float prey = MacroEcosystemMath.SanitizeBiomass(src.PreyBiomass + rem.PreyFraction, carryingCapacity);
            float predator = MacroEcosystemMath.SanitizeBiomass(src.PredatorBiomass + rem.PredatorFraction, carryingCapacity);
            int substeps = math.clamp(IntegrationSubsteps, 1, 8);
            float dt = math.max(0.001f, Tuning.FrostDeltaSeconds) * math.rcp(substeps);
            float alphaRate = math.select(Tuning.BaseBirthRate, spec.BaseBirthRate, hasSpec & math.isfinite(spec.BaseBirthRate) & spec.BaseBirthRate > 0f);
            float betaRate = math.select(Tuning.PredationRate, spec.PredationRate, hasSpec & math.isfinite(spec.PredationRate) & spec.PredationRate > 0f);
            float deltaRate = math.select(Tuning.PredatorConversionRate, spec.PredatorConversionRate, hasSpec & math.isfinite(spec.PredatorConversionRate) & spec.PredatorConversionRate > 0f);
            float gammaRate = math.select(Tuning.PredatorStarvationRate, spec.PredatorStarvationRate, hasSpec & math.isfinite(spec.PredatorStarvationRate) & spec.PredatorStarvationRate > 0f);
            float toxicitySuppression = math.saturate(1f - toxinPenalty * Tuning.ToxicityBirthSuppression * 0.25f);
            float alphaBase = math.max(0.0001f, alphaRate) * toxicitySuppression;
            float beta = math.max(0.0000000001f, betaRate);
            float delta = math.max(0.0000000001f, deltaRate);
            float gamma = math.max(0.0001f, gammaRate) * (1f + toxinPenalty * Tuning.ToxicityDeathBoost * 0.15f);
            float floraGrowth = math.max(0.0001f, alphaRate * 1.65f) * math.max(0.15f, 1f - toxinPenalty * 0.2f);

            for (int step = 0; step < substeps; step++)
            {
                float safeCapacity = math.max(0.0001f, carryingCapacity);
                float flora01 = math.saturate(flora * math.rcp(safeCapacity));
                float safeFlora = math.max(0.0001f, flora);
                float safePrey = math.max(0.0001f, prey);
                float safePredator = math.max(0.0001f, predator);
                float alpha = alphaBase * math.max(0.05f, flora01);
                float interaction = safePrey * safePredator;
                float floraNext = flora + ((floraGrowth * safeFlora * (1f - flora01)) - (alpha * safePrey * 0.18f)) * dt;
                float preyNext = prey + ((alpha * safePrey - beta * interaction) * dt);
                float predatorNext = predator + ((delta * interaction - gamma * safePredator) * dt);
                flora = math.clamp(floraNext, 0f, carryingCapacity);
                prey = math.clamp(preyNext, 0f, math.min(carryingCapacity, preyCapacity));
                predator = math.clamp(predatorNext, 0f, math.min(carryingCapacity, predatorCapacity));
            }

            bool valid = math.isfinite(flora) & math.isfinite(prey) & math.isfinite(predator);
            if (!valid)
            {
                flora = math.min(src.FloraBiomass, carryingCapacity);
                prey = math.min(src.PreyBiomass, preyCapacity);
                predator = math.min(src.PredatorBiomass, predatorCapacity);
            }
            FaultFlags[index] = valid ? 0u : MacroEcosystemMath.SectorFaultInvalidMath;

            rem.PreyFraction = 0f;
            rem.PredatorFraction = 0f;
            rem.LastDiffusionTransfers = 0u;
            rem.Flags = valid ? 0u : MacroEcosystemMath.SectorFaultInvalidMath;
            dst = src;
            dst.FloraBiomass = flora;
            dst.PreyBiomass = prey;
            dst.PredatorBiomass = predator;
            dst.CarryingCapacity = carryingCapacity;
            dst.DominantSpeciesMask = MacroEcosystemMath.PackDominantSpeciesMask(flora, prey, predator, carryingCapacity);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct BiomassDiffusionJob : IJobParallelFor
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Source;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Destination;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorRemainderDTO* Remainders;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorCoordDTO* Coords;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public BiomeEcosystemSpecDTO* BiomeSpecs;
        public int SectorCount;
        public int BiomeSpecCapacity;
        public int Width;
        public int Height;
        public float SectorSizeMeters;
        public float MigrationRate;
        public float QualityFlowWeight;
        public float CarryingCapacityPrey;
        public float CarryingCapacityPredator;
        public float TemperatureOptimum;
        public float TemperatureHalfRange;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)SectorCount)
                return;

            int x = index % Width;
            int z = index / Width;
            EcosystemSectorDTO center = Source[index];
            EcosystemSectorCoordDTO centerCoord = Coords[index];
            float floraDelta = 0f;
            float preyDelta = 0f;
            float predatorDelta = 0f;
            uint transferCount = 0u;
            AccumulateNeighbor(x > 0 ? index - 1 : -1, center, centerCoord, ref floraDelta, ref preyDelta, ref predatorDelta, ref transferCount);
            AccumulateNeighbor(x < Width - 1 ? index + 1 : -1, center, centerCoord, ref floraDelta, ref preyDelta, ref predatorDelta, ref transferCount);
            AccumulateNeighbor(z > 0 ? index - Width : -1, center, centerCoord, ref floraDelta, ref preyDelta, ref predatorDelta, ref transferCount);
            AccumulateNeighbor(z < Height - 1 ? index + Width : -1, center, centerCoord, ref floraDelta, ref preyDelta, ref predatorDelta, ref transferCount);

            ref EcosystemSectorRemainderDTO rem = ref UnsafeUtility.AsRef<EcosystemSectorRemainderDTO>(Remainders + index);
            float carrying = math.max(1f, math.select(CarryingCapacityPrey + CarryingCapacityPredator, center.CarryingCapacity, math.isfinite(center.CarryingCapacity) & center.CarryingCapacity > 0f));
            float floraNext = math.clamp(center.FloraBiomass + floraDelta, 0f, carrying);
            float preyNext = math.clamp(center.PreyBiomass + preyDelta, 0f, carrying);
            float predatorNext = math.clamp(center.PredatorBiomass + predatorDelta, 0f, carrying);
            bool valid = math.isfinite(floraNext) & math.isfinite(preyNext) & math.isfinite(predatorNext);
            if (!valid)
            {
                floraNext = MacroEcosystemMath.SanitizeBiomass(center.FloraBiomass, carrying);
                preyNext = MacroEcosystemMath.SanitizeBiomass(center.PreyBiomass, carrying);
                predatorNext = MacroEcosystemMath.SanitizeBiomass(center.PredatorBiomass, carrying);
            }
            rem.LastDiffusionTransfers = transferCount;
            rem.Flags = valid ? 0u : MacroEcosystemMath.SectorFaultInvalidMath;

            EcosystemSectorDTO output = center;
            output.FloraBiomass = floraNext;
            output.PreyBiomass = preyNext;
            output.PredatorBiomass = predatorNext;
            output.CarryingCapacity = carrying;
            output.DominantSpeciesMask = MacroEcosystemMath.PackDominantSpeciesMask(floraNext, preyNext, predatorNext, carrying);
            Destination[index] = output;
        }

        private void AccumulateNeighbor(
            int neighborIndex,
            EcosystemSectorDTO center,
            EcosystemSectorCoordDTO centerCoord,
            ref float floraDelta,
            ref float preyDelta,
            ref float predatorDelta,
            ref uint transferCount)
        {
            if ((uint)neighborIndex >= (uint)SectorCount)
                return;

            EcosystemSectorDTO neighbor = Source[neighborIndex];
            EcosystemSectorCoordDTO neighborCoord = Coords[neighborIndex];
            double3 centerAup = new double3((double)centerCoord.SectorX, (double)centerCoord.SectorY, (double)centerCoord.SectorZ) * (double)SectorSizeMeters;
            double3 neighborAup = new double3((double)neighborCoord.SectorX, (double)neighborCoord.SectorY, (double)neighborCoord.SectorZ) * (double)SectorSizeMeters;
            float3 delta = AupPrecisionMath.LocalDeltaFloat3(neighborAup, centerAup, float3.zero);
            float invDistance = math.rsqrt(math.max(1f, math.lengthsq(delta)));
            bool hasCenterSpec = MacroEcosystemMath.TryResolveBiomeSpec(BiomeSpecs, BiomeSpecCapacity, centerCoord.BiomeHash, out BiomeEcosystemSpecDTO centerSpec);
            bool hasNeighborSpec = MacroEcosystemMath.TryResolveBiomeSpec(BiomeSpecs, BiomeSpecCapacity, neighborCoord.BiomeHash, out BiomeEcosystemSpecDTO neighborSpec);
            float resistance = math.saturate(((hasCenterSpec ? centerSpec.MigrationResistance : 0f) + (hasNeighborSpec ? neighborSpec.MigrationResistance : 0f)) * 0.5f);
            float qualityFlow = math.clamp(math.select(0.25f, QualityFlowWeight, math.isfinite(QualityFlowWeight)), 0.25f, 1f);
            float pairWeight = MigrationRate * qualityFlow * (1f - resistance) * math.saturate(invDistance * SectorSizeMeters);
            float centerCapacity = math.max(1f, math.select(CarryingCapacityPrey + CarryingCapacityPredator, center.CarryingCapacity, math.isfinite(center.CarryingCapacity) & center.CarryingCapacity > 0f));
            float neighborCapacity = math.max(1f, math.select(CarryingCapacityPrey + CarryingCapacityPredator, neighbor.CarryingCapacity, math.isfinite(neighbor.CarryingCapacity) & neighbor.CarryingCapacity > 0f));
            float centerFlora01 = math.saturate(center.FloraBiomass * math.rcp(centerCapacity));
            float neighborFlora01 = math.saturate(neighbor.FloraBiomass * math.rcp(neighborCapacity));
            float centerPrey01 = math.saturate(center.PreyBiomass * math.rcp(centerCapacity));
            float neighborPrey01 = math.saturate(neighbor.PreyBiomass * math.rcp(neighborCapacity));
            float centerPred01 = math.saturate(center.PredatorBiomass * math.rcp(centerCapacity));
            float neighborPred01 = math.saturate(neighbor.PredatorBiomass * math.rcp(neighborCapacity));
            float averagePrey = (center.PreyBiomass + neighbor.PreyBiomass) * 0.5f;
            float averagePredator = (center.PredatorBiomass + neighbor.PredatorBiomass) * 0.5f;
            floraDelta += (neighbor.FloraBiomass - center.FloraBiomass) * pairWeight * 0.08f;
            preyDelta += ((neighbor.PreyBiomass - center.PreyBiomass) * 0.35f + ((neighborFlora01 - centerFlora01) - (neighborPred01 - centerPred01) * 0.45f) * averagePrey) * pairWeight;
            predatorDelta += ((neighbor.PredatorBiomass - center.PredatorBiomass) * 0.25f + ((neighborPrey01 - centerPrey01) - (neighborPred01 - centerPred01) * 0.25f) * averagePredator) * pairWeight * 0.65f;
            transferCount += pairWeight > 0.000001f ? 1u : 0u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct CopySectorBufferJob : IJobParallelFor
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Source;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Destination;
        public int SectorCount;

        public void Execute(int index)
        {
            if ((uint)index < (uint)SectorCount)
                Destination[index] = Source[index];
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct EcosystemTelemetryReductionJob : IJob
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorDTO* Sectors;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public EcosystemSectorRemainderDTO* Remainders;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public uint* FaultFlags;
        [NoAlias] public NativeArray<MacroEcosystemTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<MacroEcosystemCounterDTO> Counters;
        public int SectorCount;
        public int TelemetryIndex;
        public uint FrameIndex;
        public uint DiffusionSteps;
        public uint IntegrationSubsteps;
        public float GlobalQualityWeight;

        public void Execute()
        {
            float flora = 0f;
            float prey = 0f;
            float predator = 0f;
            float carrying = 0f;
            uint sterile = 0u;
            uint transferCount = 0u;
            uint flags = 0u;
            uint dominantPredatorSectorHash = 0u;
            float maxPredatorDensity = 0f;
            uint hash = 2166136261u;
            for (int i = 0; i < SectorCount; i++)
            {
                EcosystemSectorDTO sector = Sectors[i];
                EcosystemSectorRemainderDTO rem = Remainders[i];
                float sectorCapacity = math.max(0.0001f, math.select(1f, sector.CarryingCapacity, math.isfinite(sector.CarryingCapacity)));
                bool finiteSector = math.isfinite(sector.FloraBiomass) & math.isfinite(sector.PreyBiomass) & math.isfinite(sector.PredatorBiomass) & math.isfinite(sector.CarryingCapacity);
                float safeFlora = math.select(0f, sector.FloraBiomass, math.isfinite(sector.FloraBiomass));
                float safePrey = math.select(0f, sector.PreyBiomass, math.isfinite(sector.PreyBiomass));
                float safePredator = math.select(0f, sector.PredatorBiomass, math.isfinite(sector.PredatorBiomass));
                flora += safeFlora;
                prey += safePrey;
                predator += safePredator;
                carrying += sectorCapacity;
                transferCount += rem.LastDiffusionTransfers;
                float predatorDensity = math.saturate(safePredator * math.rcp(sectorCapacity));
                bool strongerPredator = predatorDensity > maxPredatorDensity;
                maxPredatorDensity = math.select(maxPredatorDensity, predatorDensity, strongerPredator);
                dominantPredatorSectorHash = math.select(dominantPredatorSectorHash, (uint)sector.SectorHash, strongerPredator);
                float sectorMass = safeFlora + safePrey + safePredator;
                sterile += sectorMass <= 0.0001f ? 1u : 0u;
                flags |= finiteSector
                    ? 0u
                    : MacroEcosystemTelemetryEntry.FlagInvalidMath;
                flags |= (FaultFlags[i] & MacroEcosystemMath.SectorFaultInvalidMath) != 0u
                    ? MacroEcosystemTelemetryEntry.FlagInvalidMath
                    : 0u;
                flags |= (rem.Flags & MacroEcosystemMath.SectorFaultInvalidMath) != 0u
                    ? MacroEcosystemTelemetryEntry.FlagInvalidMath
                    : 0u;
                flags |= sector.PreyBiomass > sectorCapacity * 4f || sector.PredatorBiomass > sectorCapacity * 2f || sector.FloraBiomass > sectorCapacity * 4f
                    ? MacroEcosystemTelemetryEntry.FlagPopulationExplosion
                    : 0u;
                hash = MacroEcosystemMath.Mix32(hash, (uint)sector.SectorHash);
                hash = MacroEcosystemMath.Mix32(hash, math.asuint(safeFlora));
                hash = MacroEcosystemMath.Mix32(hash, math.asuint(safePrey));
                hash = MacroEcosystemMath.Mix32(hash, math.asuint(safePredator));
            }

            if ((uint)TelemetryIndex < (uint)Telemetry.Length)
            {
                Telemetry[TelemetryIndex] = new MacroEcosystemTelemetryEntry
                {
                    FrameIndex = FrameIndex,
                    StateHash = hash,
                    TotalFloraBiomass = math.select(0f, flora, math.isfinite(flora)),
                    TotalPreyBiomass = math.select(0f, prey, math.isfinite(prey)),
                    TotalPredatorBiomass = math.select(0f, predator, math.isfinite(predator)),
                    DiffusionTransfers = transferCount,
                    MaxPredatorDensity = maxPredatorDensity,
                    SolverMicroseconds = 0f,
                    GlobalQualityWeight = math.saturate(GlobalQualityWeight),
                    DiffusionSteps = DiffusionSteps,
                    IntegrationSubsteps = IntegrationSubsteps,
                    Flags = flags,
                    TotalCarryingCapacity = math.select(0f, carrying, math.isfinite(carrying)),
                    DominantPredatorSectorHash = dominantPredatorSectorHash,
                    TimingMode = MacroEcosystemTelemetryEntry.TimingModeUnspecified,
                    TimingSourceHash = 0u
                };
            }

            if (Counters.IsCreated)
            {
                if (Counters.Length > 1) Counters[1] = MacroEcosystemCounterDTO.FromValue(SectorCount);
                if (Counters.Length > 2) Counters[2] = MacroEcosystemCounterDTO.FromValue(transferCount > int.MaxValue ? int.MaxValue : (int)transferCount);
                if (Counters.Length > 3) Counters[3] = MacroEcosystemCounterDTO.FromValue(sterile > int.MaxValue ? int.MaxValue : (int)sterile);
                if (Counters.Length > 4) Counters[4] = MacroEcosystemCounterDTO.FromValue((int)DiffusionSteps);
                if (Counters.Length > 5) Counters[5] = MacroEcosystemCounterDTO.FromValue((int)flags);
            }
        }
    }

    #if UNITY_EDITOR
    internal static unsafe class MacroEcosystemCsvParser
    {
        internal static int ParseBiomeSpecs(
            ReadOnlySpan<byte> csv,
            NativeArray<BiomeEcosystemSpecDTO> specs)
        {
            if (!specs.IsCreated || specs.Length <= 0)
                return 0;

            return ParseBiomeSpecs(
                csv,
                new Span<BiomeEcosystemSpecDTO>(NativeArrayUnsafeUtility.GetUnsafePtr(specs), specs.Length));
        }

        internal static int ParseBiomeSpecs(
            ReadOnlySpan<byte> csv,
            Span<BiomeEcosystemSpecDTO> specs)
        {
            int parsed = 0;
            int cursor = 0;
            int row = 0;
            for (int i = 0; i < specs.Length; i++)
                specs[i] = default;

            while (cursor < csv.Length && parsed < specs.Length)
            {
                int lineStart = cursor;
                while (cursor < csv.Length && csv[cursor] != (byte)'\n' && csv[cursor] != (byte)'\r')
                    cursor++;
                int lineEnd = cursor;
                while (cursor < csv.Length && (csv[cursor] == (byte)'\n' || csv[cursor] == (byte)'\r'))
                    cursor++;

                ReadOnlySpan<byte> line = csv.Slice(lineStart, lineEnd - lineStart);
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                row++;
                if (row == 1 && StartsWithAscii(line, "biome"))
                    continue;

                if (TryParseBiomeLine(line, out BiomeEcosystemSpecDTO spec))
                {
                    parsed += TryInsertBiomeSpec(specs, spec) ? 1 : 0;
                }
            }

            return parsed;
        }

        private static bool TryInsertBiomeSpec(Span<BiomeEcosystemSpecDTO> specs, BiomeEcosystemSpecDTO spec)
        {
            if (specs.Length <= 0 || spec.BiomeHash == 0u)
                return false;

            int slot = MacroEcosystemMath.ResolveOpenAddressSlot(spec.BiomeHash, specs.Length);
            for (int probe = 0; probe < specs.Length; probe++)
            {
                BiomeEcosystemSpecDTO existing = specs[slot];
                if (existing.BiomeHash == 0u || existing.BiomeHash == spec.BiomeHash)
                {
                    specs[slot] = spec;
                    return true;
                }

                slot++;
                if (slot == specs.Length)
                    slot = 0;
            }

            return false;
        }

        private static bool TryParseBiomeLine(ReadOnlySpan<byte> line, out BiomeEcosystemSpecDTO spec)
        {
            spec = default;
            ReadOnlySpan<byte> biome = ReadField(line, 0);
            if (biome.Length == 0)
                return false;

            if (!TryParseFloat(ReadField(line, 1), out float preyCapacity) ||
                !TryParseFloat(ReadField(line, 2), out float predatorCapacity) ||
                !TryParseFloat(ReadField(line, 3), out float migrationResistance) ||
                !TryParseFloat(ReadField(line, 4), out float temperatureOptimum) ||
                !TryParseFloat(ReadField(line, 5), out float toxinPenalty))
            {
                return false;
            }

            spec = new BiomeEcosystemSpecDTO
            {
                BiomeHash = MacroEcosystemMath.HashLowerAscii(biome),
                CarryingCapacityPrey = math.max(1f, preyCapacity),
                CarryingCapacityPredator = math.max(1f, predatorCapacity),
                MigrationResistance = math.saturate(migrationResistance),
                TemperatureOptimum = math.select(8f, temperatureOptimum, math.isfinite(temperatureOptimum)),
                ToxinPenalty = math.saturate(toxinPenalty),
                BaseBirthRate = ReadOptionalPositiveFloat(line, 6),
                PredationRate = ReadOptionalPositiveFloat(line, 7),
                PredatorConversionRate = ReadOptionalPositiveFloat(line, 8),
                PredatorStarvationRate = ReadOptionalPositiveFloat(line, 9)
            };
            return true;
        }

        private static float ReadOptionalPositiveFloat(ReadOnlySpan<byte> line, int fieldIndex)
        {
            ReadOnlySpan<byte> field = ReadField(line, fieldIndex);
            if (field.Length == 0 || !TryParseFloat(field, out float value))
                return 0f;
            return math.isfinite(value) && value > 0f ? value : 0f;
        }

        private static ReadOnlySpan<byte> ReadField(ReadOnlySpan<byte> line, int fieldIndex)
        {
            int current = 0;
            int start = 0;
            for (int i = 0; i <= line.Length; i++)
            {
                if (i != line.Length && line[i] != (byte)',')
                    continue;

                if (current == fieldIndex)
                    return Trim(line.Slice(start, i - start));

                current++;
                start = i + 1;
            }

            return ReadOnlySpan<byte>.Empty;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length;
            while (start < end && value[start] <= 32)
                start++;
            while (end > start && value[end - 1] <= 32)
                end--;
            return value.Slice(start, end - start);
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> value, out float result)
        {
            result = 0f;
            value = Trim(value);
            if (value.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (value[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (value[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
            {
                hasDigit = true;
                integer = integer * 10f + (value[index] - (byte)'0');
                index++;
            }

            float fraction = 0f;
            float scale = 0.1f;
            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
                {
                    hasDigit = true;
                    fraction += (value[index] - (byte)'0') * scale;
                    scale *= 0.1f;
                    index++;
                }
            }

            result = (integer + fraction) * sign;
            return hasDigit && math.isfinite(result);
        }

        private static bool StartsWithAscii(ReadOnlySpan<byte> value, string ascii)
        {
            if (value.Length < ascii.Length)
                return false;

            for (int i = 0; i < ascii.Length; i++)
            {
                byte b = value[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                if (b != (byte)ascii[i])
                    return false;
            }

            return true;
        }
    }
    #endif
}
