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
    /// Data-only flora/fauna symbiosis lane. The truth is scalar chemistry in Vault memory; rendering is a later lie.
    /// </summary>
    public sealed class ShinobuFloraFaunaSymbiosisSolver : IColdTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IDisposable
    {
        public const uint TuningFlagEmergencyMock = 1u << 0;
        public const uint TuningFlagLegacyBinary = 1u << 1;
        public const uint TuningFlagCsvOverride = 1u << 2;
        public const uint TuningFlagEditorGizmos = 1u << 3;
        public const uint TuningFlagAnomalyMirror = 1u << 4;

        internal const uint FloraFlagActive = 1u << 0;
        internal const uint FloraFlagToxic = 1u << 1;
        internal const uint FloraFlagCamouflage = 1u << 2;
        internal const uint FloraFlagOxygen = 1u << 3;
        internal const uint FloraFlagSpore = 1u << 4;
        internal const uint FloraFlagGlow = 1u << 5;
        internal const uint FloraFlagBlighted = 1u << 6;

        internal const uint FaunaFlagActive = 1u << 0;
        internal const uint FaunaFlagCamouflaged = 1u << 16;
        internal const uint FaunaFlagCarryingPollen = 1u << 17;
        internal const uint FaunaFlagToxemia = 1u << 18;

        internal const uint LinkFlagCompatible = 1u << 0;
        internal const uint LinkFlagToxicImmune = 1u << 1;

        private const int DefaultFloraCapacity = 50000;
        private const int DefaultMockFishCapacity = 128;
        private const int DefaultAmbientFishCapacity = 5000;
        private const int LinkCapacity = 128;
        private const int ExchangeCapacity = 8192;
        private const int TelemetryCapacity = 300;
        private const int ScannerVfxCapacity = 128;
        private const int OxygenEmitterCapacity = 256;
        private const int AdherenceCapacity = 64;
        private const int SeedCapacity = 128;
        private const int AcousticTapCapacity = 64;
        private const int SpatialBucketCapacity = 65536;
        private const int SpatialBucketMask = SpatialBucketCapacity - 1;
        private const int CsvMaxBytes = 8192;
        private const int LegacyScratchBytes = 512;
        private const int MaxSpatialHashChainSteps = 64;
        private const int MaxNeighborSamples = 48;
        private const float DefaultCellSizeMeters = 8f;
        private const float DefaultSectorSizeMeters = 64f;
        private const float DefaultSimulationTickDelta = 1f;
        private const double AupCellSizeMetersDouble = HectonPhysicsContract.AupSectorSizeMetersDouble;
        private const string CsvRelativePath = "symbiosis_links.csv";
        private const string CsvPrecomputedRelativePath = "Data/Precomputed/symbiosis_links.csv";
        private const string LegacyLinksFile = "symbiosis_chemical_links.h8bin";
        private const uint LegacyLinksMagicLittleEndian = 0x4C323653u; // S62L
        private const uint LegacyLinksMagicBigEndian = 0x42323653u; // S62B
        private const int LegacyLinksHeaderBytes = 16;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_62.bin";
        private const string DumpSymbiosisRelativePath = "Docs/AgentLogs/Dump_SYMBIOSIS.bin";
        private const ulong DumpMagic = 0x5348594D42493632UL; // SHYMBI62
        private const int DumpVersion = 1;
        private const uint SourceHash = 0x53363253u; // S62S
        internal const uint FaunaHashHerbivore = 0x48455242u; // HERB
        internal const uint FaunaHashCarnivore = 0x4341524Eu; // CARN
        internal const uint FloraHashHealingKelp = 0x484B454Cu; // HKEL
        internal const uint FloraHashToxicAnemone = 0x54414E45u; // TANE
        internal const uint FloraHashOxygenKelp = 0x4F584B45u; // OXKE
        internal const uint FloraHashGlowMoss = 0x474D4F53u; // GMOS
        internal const uint FloraHashSporeCoral = 0x5350434Fu; // SPCO

        private static ShinobuFloraFaunaSymbiosisSolver s_runtime;

        private VaultBufferHandle<SymbiosisFloraDTO> _floraHandle;
        private VaultBufferHandle<SymbiosisFloraAupDTO> _floraAupHandle;
        private VaultBufferHandle<SymbiosisChemicalLinkDTO> _linkHandle;
        private VaultBufferHandle<SymbiosisExchangeDTO> _exchangeHandle;
        private VaultBufferHandle<SymbiosisTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<SymbiosisCounterDTO> _counterHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;
        private VaultBufferHandle<ScannerVfxDTO> _scannerVfxHandle;
        private VaultBufferHandle<SymbiosisOxygenEmitterDTO> _oxygenEmitterHandle;
        private VaultBufferHandle<AdherenceDTO> _adherenceHandle;
        private VaultBufferHandle<FloraSeedDTO> _seedHandle;
        private VaultBufferHandle<SymbiosisAcousticTapDTO> _acousticTapHandle;
        private VaultBufferHandle<SymbiosisTuningDTO> _tuningHandle;
        private VaultBufferHandle<int> _floraBucketHeadHandle;
        private VaultBufferHandle<int> _floraBucketNextHandle;
        private VaultBufferHandle<MockBoidArray> _mockBoidHandle;
        private VaultBufferHandle<byte> _legacyScratchHandle;
        private VaultBufferHandle<MockFishSymbiosisDTO> _mockFishHandle;
        private VaultBufferHandle<AmbientEntityDTO> _ambientEntityHandle;
        private VaultBufferHandle<AmbientEntityAupDTO> _ambientAupHandle;
        private VaultBufferHandle<SymbiosisAnomalyFieldMirror> _anomalyFieldHandle;

        private IDataVault _dataVault;
        private JobHandle _activeJobHandle;
        private AbsoluteUniversePosition _centerAup;
        private AbsoluteUniversePosition _lastSubmarineAup;
        private AbsoluteUniversePosition _submarineAup;
        private long _csvTimestampTicks;
        private long _scheduleTicks;
        private int _telemetryCursor;
        private int _ambientFishCapacity = DefaultAmbientFishCapacity;
        private uint _simulationFrameCounter;
        private float _submarineIdleSeconds;
        private float _lastSolverMs;
        private bool _registeredColdTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _jobScheduled;
        private bool _jobLocksHeld;
        private bool _dumpedFault;
        private bool _hasSubmarineAup;
        private uint _runtimeFlags;

        private ShinobuFloraFaunaSymbiosisSolver()
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

        public static ShinobuFloraFaunaSymbiosisSolver EnsureRuntimeService()
        {
            ShinobuFloraFaunaSymbiosisSolver runtime = s_runtime;
            if (runtime == null)
            {
                runtime = new ShinobuFloraFaunaSymbiosisSolver();
                s_runtime = runtime;
            }

            runtime.Activate();
            return runtime;
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
            _simulationFrameCounter = 0u;
            _dumpedFault = false;

            if (_dataVault == null || !EnsureVaultState())
            {
                TryUnregisterTicks();
                return;
            }

            TryRegisterTicks();
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

            RefreshAupSignals();
            RefreshSubmarineIdleState();
            MonitorCsvOverrides(vault);
            RefreshQualityWeight(vault);

            if (!TryResolveJobBuffers(
                    vault,
                    out NativeArray<SymbiosisFloraDTO> flora,
                    out NativeArray<SymbiosisFloraAupDTO> floraAups,
                    out NativeArray<SymbiosisChemicalLinkDTO> links,
                    out NativeArray<SymbiosisExchangeDTO> exchanges,
                    out NativeArray<SymbiosisTelemetryEntry> telemetry,
                    out NativeArray<SymbiosisCounterDTO> counters,
                    out NativeArray<ScannerVfxDTO> scannerVfx,
                    out NativeArray<SymbiosisOxygenEmitterDTO> oxygenEmitters,
                    out NativeArray<AdherenceDTO> adherence,
                    out NativeArray<FloraSeedDTO> seeds,
                    out NativeArray<SymbiosisAcousticTapDTO> acousticTaps,
                    out NativeArray<SymbiosisTuningDTO> tuning,
                    out NativeArray<int> floraBucketHeads,
                    out NativeArray<int> floraBucketNext,
                    out NativeArray<MockBoidArray> mockBoids,
                    out NativeArray<MockFishSymbiosisDTO> mockFish,
                    out NativeArray<AmbientEntityDTO> ambientEntities,
                    out NativeArray<AmbientEntityAupDTO> ambientAups,
                    out NativeArray<SymbiosisAnomalyFieldMirror> anomalyField))
            {
                return;
            }

            if (!TryLockJobBuffers(vault))
                return;

            try
            {
                int floraCount = math.min(DefaultFloraCapacity, math.min(flora.Length, floraAups.Length));
                int mockFishCount = math.min(DefaultMockFishCapacity, mockFish.Length);
                int ambientCount = math.min(_ambientFishCapacity, math.min(ambientEntities.Length, ambientAups.Length));
                float quality = ResolveGlobalQualityWeight(vault);
                uint frame = AdvanceSimulationFrame(counters);
                uint seed = ResolveFrameSectorSeed(in _centerAup, frame);

                JobHandle handle = default;
                if (counters.Length > 0 && counters[0].Initialized == 0)
                {
                    var hydrateJob = new GenerateEmergencyMockSymbiosisJob
                    {
                        Flora = flora,
                        FloraAups = floraAups,
                        Links = links,
                        Tuning = tuning,
                        Counters = counters,
                        MockBoids = mockBoids,
                        MockFish = mockFish,
                        CenterAup = _centerAup,
                        GlobalQualityWeight = quality,
                        FloraCount = floraCount,
                        MockFishCount = mockFishCount,
                        LinkCount = math.min(LinkCapacity, links.Length),
                        Seed = seed
                    };
                    handle = hydrateJob.Schedule(handle);
                    _runtimeFlags |= TuningFlagEmergencyMock;
                }

                var hashJob = new BuildSymbiosisFloraSpatialHashJob
                {
                    Flora = flora,
                    FloraAups = floraAups,
                    BucketHeads = floraBucketHeads,
                    BucketNext = floraBucketNext,
                    CenterAup = _centerAup,
                    CellSizeMeters = DefaultCellSizeMeters,
                    Count = floraCount
                };
                handle = hashJob.Schedule(handle);

                var solveJob = new SymbiosisExchangeKernelJob
                {
                    Flora = flora,
                    FloraAups = floraAups,
                    Links = links,
                    Exchanges = exchanges,
                    Counters = counters,
                    ScannerVfx = scannerVfx,
                    OxygenEmitters = oxygenEmitters,
                    Adherence = adherence,
                    Seeds = seeds,
                    AcousticTaps = acousticTaps,
                    Tuning = tuning,
                    BucketHeads = floraBucketHeads,
                    BucketNext = floraBucketNext,
                    MockBoids = mockBoids,
                    MockFish = mockFish,
                    AmbientEntities = ambientEntities,
                    AmbientAups = ambientAups,
                    AnomalyField = anomalyField,
                    CenterAup = _centerAup,
                    SubmarineAup = _submarineAup,
                    SubmarineIdleSeconds = _submarineIdleSeconds,
                    Frame = frame,
                    CellSizeMeters = DefaultCellSizeMeters,
                    SectorSizeMeters = DefaultSectorSizeMeters,
                    SimulationTickDelta = DefaultSimulationTickDelta,
                    FloraCount = floraCount,
                    AmbientFishCount = ambientCount,
                    MockFishCount = mockFishCount,
                    MaxNeighborSamplesBase = MaxNeighborSamples,
                    MaxSpatialHashChainSteps = MaxSpatialHashChainSteps
                };
                handle = solveJob.Schedule(handle);

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

        public void LateFrameTick()
        {
            CompleteFrameJob(forceComplete: false);
        }

        internal static ref SymbiosisFloraDTO GetFloraRef(
            IDataVault vault,
            ref VaultBufferHandle<SymbiosisFloraDTO> handle,
            int index)
        {
            return ref handle.GetElementAsRef(vault, index);
        }

        private void Activate()
        {
            if (!Application.isPlaying)
                return;

            SignalBus<AcousticPingSignal>.EnsureInitialized();
            SignalBus<SubmarineLightsChangedSignal>.EnsureInitialized();
            TryRegisterHotSwapListener();
            if (EnsureVaultState())
                TryRegisterTicks();
        }

        private bool EnsureVaultState()
        {
            SymbiosisLayoutManifest.VerifyColdBoot();

            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return false;

            _floraHandle = vault.GetBufferHandle<SymbiosisFloraDTO>(
                BufferID.ShinobuSymbiosisFlora,
                DefaultFloraCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _floraAupHandle = vault.GetBufferHandle<SymbiosisFloraAupDTO>(
                BufferID.ShinobuSymbiosisFloraAups,
                DefaultFloraCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _linkHandle = vault.GetBufferHandle<SymbiosisChemicalLinkDTO>(
                BufferID.ShinobuSymbiosisLinks,
                LinkCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _exchangeHandle = vault.GetBufferHandle<SymbiosisExchangeDTO>(
                BufferID.ShinobuSymbiosisExchanges,
                ExchangeCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.GetBufferHandle<SymbiosisTelemetryEntry>(
                BufferID.ShinobuSymbiosisTelemetryRing,
                TelemetryCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _counterHandle = vault.GetBufferHandle<SymbiosisCounterDTO>(
                BufferID.ShinobuSymbiosisCounters,
                1,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _csvScratchHandle = vault.GetBufferHandle<byte>(
                BufferID.ShinobuSymbiosisCsvScratch,
                CsvMaxBytes,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            _scannerVfxHandle = vault.GetBufferHandle<ScannerVfxDTO>(
                BufferID.ShinobuSymbiosisScannerVfx,
                ScannerVfxCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            _oxygenEmitterHandle = vault.GetBufferHandle<SymbiosisOxygenEmitterDTO>(
                BufferID.ShinobuSymbiosisOxygenEmitters,
                OxygenEmitterCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            _adherenceHandle = vault.GetBufferHandle<AdherenceDTO>(
                BufferID.ShinobuSymbiosisAdherence,
                AdherenceCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            _seedHandle = vault.GetBufferHandle<FloraSeedDTO>(
                BufferID.ShinobuSymbiosisSeeds,
                SeedCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            _acousticTapHandle = vault.GetBufferHandle<SymbiosisAcousticTapDTO>(
                BufferID.ShinobuSymbiosisAcousticTaps,
                AcousticTapCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.GetBufferHandle<SymbiosisTuningDTO>(
                BufferID.ShinobuSymbiosisTuning,
                1,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _floraBucketHeadHandle = vault.GetBufferHandle<int>(
                BufferID.ShinobuSymbiosisFloraHashBucketHeads,
                SpatialBucketCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            _floraBucketNextHandle = vault.GetBufferHandle<int>(
                BufferID.ShinobuSymbiosisFloraHashNext,
                DefaultFloraCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            _mockBoidHandle = vault.GetBufferHandle<MockBoidArray>(
                BufferID.ShinobuSymbiosisMockBoids,
                1,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _legacyScratchHandle = vault.GetBufferHandle<byte>(
                BufferID.ShinobuSymbiosisLegacyScratch,
                LegacyScratchBytes,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            _mockFishHandle = vault.GetBufferHandle<MockFishSymbiosisDTO>(
                BufferID.ShinobuSymbiosisMockFish,
                DefaultMockFishCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);

            if (!vault.TryGetBufferHandle(BufferID.ShinobuAmbientEntities, out _ambientEntityHandle))
            {
                _ambientEntityHandle = vault.GetBufferHandle<AmbientEntityDTO>(
                    BufferID.ShinobuAmbientEntities,
                    _ambientFishCapacity,
                    SystemID.AIEcology,
                    NativeArrayOptions.ClearMemory);
            }

            if (!vault.TryGetBufferHandle(BufferID.ShinobuAmbientAups, out _ambientAupHandle))
            {
                _ambientAupHandle = vault.GetBufferHandle<AmbientEntityAupDTO>(
                    BufferID.ShinobuAmbientAups,
                    _ambientFishCapacity,
                    SystemID.AIEcology,
                    NativeArrayOptions.ClearMemory);
            }

            if (!vault.TryGetBufferHandle(BufferID.ShinobuSeedShipAnomalyField, out _anomalyFieldHandle))
                _anomalyFieldHandle = default;

            bool ready = _floraHandle.IsCreated &&
                         _floraAupHandle.IsCreated &&
                         _linkHandle.IsCreated &&
                         _exchangeHandle.IsCreated &&
                         _telemetryHandle.IsCreated &&
                         _counterHandle.IsCreated &&
                         _csvScratchHandle.IsCreated &&
                         _scannerVfxHandle.IsCreated &&
                         _oxygenEmitterHandle.IsCreated &&
                         _adherenceHandle.IsCreated &&
                         _seedHandle.IsCreated &&
                         _acousticTapHandle.IsCreated &&
                         _tuningHandle.IsCreated &&
                         _floraBucketHeadHandle.IsCreated &&
                         _floraBucketNextHandle.IsCreated &&
                         _mockBoidHandle.IsCreated &&
                         _legacyScratchHandle.IsCreated &&
                         _mockFishHandle.IsCreated &&
                         _ambientEntityHandle.IsCreated &&
                         _ambientAupHandle.IsCreated;
            if (!ready)
                return false;

            TryLoadLegacyLinksIntoVault(vault);
            return true;
        }

        private IDataVault ResolveDataVault()
        {
            if (_dataVault != null)
                return _dataVault;

            _dataVault = GlobalRegistry.DataVault;
            return _dataVault;
        }

        private bool TryResolveJobBuffers(
            IDataVault vault,
            out NativeArray<SymbiosisFloraDTO> flora,
            out NativeArray<SymbiosisFloraAupDTO> floraAups,
            out NativeArray<SymbiosisChemicalLinkDTO> links,
            out NativeArray<SymbiosisExchangeDTO> exchanges,
            out NativeArray<SymbiosisTelemetryEntry> telemetry,
            out NativeArray<SymbiosisCounterDTO> counters,
            out NativeArray<ScannerVfxDTO> scannerVfx,
            out NativeArray<SymbiosisOxygenEmitterDTO> oxygenEmitters,
            out NativeArray<AdherenceDTO> adherence,
            out NativeArray<FloraSeedDTO> seeds,
            out NativeArray<SymbiosisAcousticTapDTO> acousticTaps,
            out NativeArray<SymbiosisTuningDTO> tuning,
            out NativeArray<int> floraBucketHeads,
            out NativeArray<int> floraBucketNext,
            out NativeArray<MockBoidArray> mockBoids,
            out NativeArray<MockFishSymbiosisDTO> mockFish,
            out NativeArray<AmbientEntityDTO> ambientEntities,
            out NativeArray<AmbientEntityAupDTO> ambientAups,
            out NativeArray<SymbiosisAnomalyFieldMirror> anomalyField)
        {
            flora = _floraHandle.Resolve(vault);
            floraAups = _floraAupHandle.Resolve(vault);
            links = _linkHandle.Resolve(vault);
            exchanges = _exchangeHandle.Resolve(vault);
            telemetry = _telemetryHandle.Resolve(vault);
            counters = _counterHandle.Resolve(vault);
            scannerVfx = _scannerVfxHandle.Resolve(vault);
            oxygenEmitters = _oxygenEmitterHandle.Resolve(vault);
            adherence = _adherenceHandle.Resolve(vault);
            seeds = _seedHandle.Resolve(vault);
            acousticTaps = _acousticTapHandle.Resolve(vault);
            tuning = _tuningHandle.Resolve(vault);
            floraBucketHeads = _floraBucketHeadHandle.Resolve(vault);
            floraBucketNext = _floraBucketNextHandle.Resolve(vault);
            mockBoids = _mockBoidHandle.Resolve(vault);
            mockFish = _mockFishHandle.Resolve(vault);
            ambientEntities = _ambientEntityHandle.Resolve(vault);
            ambientAups = _ambientAupHandle.Resolve(vault);
            anomalyField = _anomalyFieldHandle.IsCreated ? _anomalyFieldHandle.Resolve(vault) : default;
            return flora.IsCreated &&
                   floraAups.IsCreated &&
                   links.IsCreated &&
                   exchanges.IsCreated &&
                   telemetry.IsCreated &&
                   counters.IsCreated &&
                   scannerVfx.IsCreated &&
                   oxygenEmitters.IsCreated &&
                   adherence.IsCreated &&
                   seeds.IsCreated &&
                   acousticTaps.IsCreated &&
                   tuning.IsCreated &&
                   floraBucketHeads.IsCreated &&
                   floraBucketNext.IsCreated &&
                   mockBoids.IsCreated &&
                   mockFish.IsCreated &&
                   ambientEntities.IsCreated &&
                   ambientAups.IsCreated;
        }

        private void RefreshAupSignals()
        {
            ReadOnlySpan<PlayerStateSignal> playerSignals = SignalBus<PlayerStateSignal>.GetFrameSnapshot();
            for (int i = 0; i < playerSignals.Length; i++)
            {
                PlayerStateSignal signal = playerSignals[i];
                if ((signal.Flags & PlayerStateSignal.FlagActive) != 0 && IsFiniteAup(in signal.PositionAup))
                    _centerAup = signal.PositionAup;
            }

            ReadOnlySpan<CameraPositionSignal> cameraSignals = SignalBus<CameraPositionSignal>.GetFrameSnapshot();
            for (int i = 0; i < cameraSignals.Length; i++)
            {
                CameraPositionSignal signal = cameraSignals[i];
                if ((signal.Flags & 1) != 0 && math.all(math.isfinite(signal.Position)))
                    return;
            }
        }

        private void RefreshSubmarineIdleState()
        {
            bool found = false;
            AbsoluteUniversePosition latest = _submarineAup;
            ReadOnlySpan<SubmarineLightsChangedSignal> signals = SignalBus<SubmarineLightsChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                SubmarineLightsChangedSignal signal = signals[i];
                if (signal.Operation == SubmarineLightsChangedSignalOperations.Remove ||
                    !IsFiniteAup(in signal.PositionAup))
                {
                    continue;
                }

                latest = signal.PositionAup;
                found = true;
            }

            if (!found)
            {
                _submarineIdleSeconds = math.max(0f, _submarineIdleSeconds - 1f);
                return;
            }

            if (_hasSubmarineAup)
            {
                float3 delta = AupToLocal(in latest, in _lastSubmarineAup);
                float movedSq = math.lengthsq(delta);
                _submarineIdleSeconds = movedSq < 0.25f ? math.min(120f, _submarineIdleSeconds + 1f) : 0f;
            }
            else
            {
                _submarineIdleSeconds = 0f;
                _hasSubmarineAup = true;
            }

            _lastSubmarineAup = latest;
            _submarineAup = latest;
        }

        private void RefreshQualityWeight(IDataVault vault)
        {
            NativeArray<SymbiosisTuningDTO> tuning = _tuningHandle.Resolve(vault);
            if (!tuning.IsCreated || tuning.Length <= 0)
                return;

            SymbiosisTuningDTO dto = SymbiosisTuningDTO.Sanitize(tuning[0]);
            dto.GlobalQualityWeight = ResolveGlobalQualityWeight(vault);
            dto.SimulationTickDelta = DefaultSimulationTickDelta;
            tuning[0] = dto;
        }

        private float ResolveGlobalQualityWeight(IDataVault vault)
        {
            if (vault != null &&
                vault.TryGetBufferHandle(BufferID.ShinobuScalabilityState, out VaultBufferHandle<ScalabilityStateDTO> handle) &&
                handle.IsCreated)
            {
                NativeArray<ScalabilityStateDTO> state = handle.Resolve(vault);
                if (state.IsCreated && state.Length > 0 && math.isfinite(state[0].GlobalQualityWeight))
                    return math.saturate(state[0].GlobalQualityWeight);
            }

            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (math.isfinite(weight))
                return math.saturate(weight);

            byte tier = ScalabilityTierProfiles.Normalize(GlobalRegistry.ScalabilityTierProfileByte);
            return math.saturate(tier * (1f / 3f));
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
                if (!scratch.IsCreated)
                    return;

                int bytesRead = ReadFileIntoNativeScratch(path, scratch, CsvMaxBytes, FileShare.ReadWrite);
                if (bytesRead <= 0)
                    return;

                NativeArray<SymbiosisTuningDTO> tuning = _tuningHandle.Resolve(vault);
                NativeArray<SymbiosisChemicalLinkDTO> links = _linkHandle.Resolve(vault);
                NativeArray<SymbiosisCounterDTO> counters = _counterHandle.Resolve(vault);
                if (!tuning.IsCreated || !links.IsCreated || tuning.Length <= 0)
                    return;

                SymbiosisTuningDTO profile = SymbiosisTuningDTO.Sanitize(tuning[0]);
                ParseCsvOverrides(scratch, bytesRead, ref profile, links);
                profile.Flags |= TuningFlagCsvOverride;
                tuning[0] = SymbiosisTuningDTO.Sanitize(profile);
                if (counters.IsCreated && counters.Length > 0)
                {
                    SymbiosisCounterDTO counter = counters[0];
                    counter.CsvLoaded++;
                    counters[0] = counter;
                }

                _runtimeFlags |= TuningFlagCsvOverride;
                _csvTimestampTicks = lastWriteUtc.Ticks;
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x53364356u, SourceHash, 0f);
            }
        }

        private bool TryLoadLegacyLinksIntoVault(IDataVault vault)
        {
            NativeArray<SymbiosisCounterDTO> counters = _counterHandle.Resolve(vault);
            if (!counters.IsCreated || counters.Length <= 0 || (counters[0].Flags & TuningFlagLegacyBinary) != 0u)
                return false;

            try
            {
                string path = ResolveLegacyPath();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return false;

                NativeArray<byte> scratch = _legacyScratchHandle.Resolve(vault);
                NativeArray<SymbiosisChemicalLinkDTO> links = _linkHandle.Resolve(vault);
                NativeArray<SymbiosisTuningDTO> tuning = _tuningHandle.Resolve(vault);
                if (!scratch.IsCreated || !links.IsCreated || !tuning.IsCreated || tuning.Length <= 0)
                    return false;

                int bytesRead = ReadFileIntoNativeScratch(path, scratch, LegacyScratchBytes, FileShare.Read);
                if (bytesRead < UnsafeUtility.SizeOf<SymbiosisChemicalLinkDTO>())
                    return false;

                int stride = UnsafeUtility.SizeOf<SymbiosisChemicalLinkDTO>();
                ResolveLegacyLinkEncoding(scratch, bytesRead, out bool bigEndian, out int payloadOffset);
                int payloadBytes = math.max(0, bytesRead - payloadOffset);
                int count = math.min(links.Length, payloadBytes / stride);
                for (int i = 0; i < count; i++)
                {
                    int offset = payloadOffset + (i * stride);
                    links[i] = new SymbiosisChemicalLinkDTO
                    {
                        FloraHash = ReadUInt32(scratch, offset, bigEndian),
                        FaunaHash = ReadUInt32(scratch, offset + 4, bigEndian),
                        ChemicalTransferRate = ReadFloat32(scratch, offset + 8, bigEndian, 0.01f),
                        Flags = ReadUInt32(scratch, offset + 12, bigEndian)
                    };
                }

                SymbiosisTuningDTO profile = SymbiosisTuningDTO.Sanitize(tuning[0]);
                profile.ActiveLinkCount = count;
                profile.Flags |= TuningFlagLegacyBinary;
                tuning[0] = profile;

                SymbiosisCounterDTO counter = counters[0];
                counter.Flags |= TuningFlagLegacyBinary;
                counters[0] = counter;
                _runtimeFlags |= TuningFlagLegacyBinary;
                return true;
            }
            catch (Exception)
            {
                return false;
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
            _lastSolverMs = Stopwatch.Frequency > 0
                ? (float)(elapsedTicks * 1000.0 / Stopwatch.Frequency)
                : 0f;

            IDataVault vault = _dataVault;
            if (vault != null)
            {
                WriteTelemetryAndFaultDump(vault);
                PublishAcousticTaps(vault);
            }

            UnlockJobBuffers();
        }

        private uint AdvanceSimulationFrame(NativeArray<SymbiosisCounterDTO> counters)
        {
            uint previous = _simulationFrameCounter;
            if (counters.IsCreated && counters.Length > 0)
            {
                uint vaultFrame = counters[0].Frame;
                if (vaultFrame != 0u)
                    previous = vaultFrame;
            }

            uint next = unchecked(previous + 1u);
            if (next == 0u)
                next = 1u;

            _simulationFrameCounter = next;
            return next;
        }

        private void WriteTelemetryAndFaultDump(IDataVault vault)
        {
            NativeArray<SymbiosisTelemetryEntry> telemetry = _telemetryHandle.Resolve(vault);
            NativeArray<SymbiosisCounterDTO> counters = _counterHandle.Resolve(vault);
            if (!telemetry.IsCreated || telemetry.Length <= 0 || !counters.IsCreated || counters.Length <= 0)
                return;

            SymbiosisCounterDTO counter = counters[0];
            int cursor = _telemetryCursor;
            int index = cursor % telemetry.Length;
            _telemetryCursor = cursor + 1;

            uint stateHash = MixTelemetryHash(counter.ActiveExchanges, counter.BiomassTransferredMilli, counter.InvalidMath, counter.OverflowCount);
            uint frame = counter.Frame != 0u ? counter.Frame : _simulationFrameCounter;
            telemetry[index] = new SymbiosisTelemetryEntry
            {
                Frame = frame,
                StateHash = stateHash,
                ActiveExchanges = counter.ActiveExchanges,
                BiomassTransferred = counter.BiomassTransferredMilli * 0.001f,
                SolverComputeTimeMs = math.max(0f, _lastSolverMs),
                OxygenEmitterCount = counter.OxygenEmitterCount,
                ToxemiaCount = counter.ToxemiaCount,
                CamouflageCount = counter.CamouflageCount,
                SeedCount = counter.SeedCount,
                AdherenceCount = counter.AdherenceCount,
                AcousticTapCount = counter.AcousticTapCount,
                Flags = _runtimeFlags | counter.Flags,
                InvalidMathCount = counter.InvalidMath,
                OverflowCount = counter.OverflowCount
            };

            if ((counter.InvalidMath != 0 || counter.OverflowCount != 0) && !_dumpedFault)
            {
                _dumpedFault = true;
                DumpBlackBox(telemetry, _telemetryCursor);
            }
        }

        private void PublishAcousticTaps(IDataVault vault)
        {
            NativeArray<SymbiosisCounterDTO> counters = _counterHandle.Resolve(vault);
            NativeArray<SymbiosisAcousticTapDTO> taps = _acousticTapHandle.Resolve(vault);
            if (!counters.IsCreated || counters.Length <= 0 || !taps.IsCreated)
                return;

            int count = math.min(counters[0].AcousticTapCount, taps.Length);
            for (int i = 0; i < count; i++)
            {
                SymbiosisAcousticTapDTO tap = taps[i];
                if ((tap.Flags & 1u) == 0u || !IsFiniteAup(in tap.PositionAup))
                    continue;

                AbsoluteUniversePosition tapAup = tap.PositionAup.ToAup();
                AcousticPingSignal signal = new AcousticPingSignal
                {
                    PositionAup = tapAup,
                    RadiusMeters = math.max(1f, tap.RadiusMeters),
                    Intensity01 = math.saturate(tap.Magnitude01),
                    SourceId = tap.SourceHash,
                    Channel = AcousticPingSignal.ChannelJawSnap,
                    Flags = AcousticPingSignal.FlagJawSnap
                };
                SignalBus<AcousticPingSignal>.TryPush(in signal);
            }
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            if (vault == null || _jobLocksHeld)
                return false;

            int locked = 0;
            if (!vault.TryLockBuffer(BufferID.ShinobuSymbiosisFlora, SystemID.AIEcology)) return false;
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSymbiosisFloraAups, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSymbiosisLinks, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSymbiosisExchanges, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSymbiosisTelemetryRing, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSymbiosisCounters, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSymbiosisScannerVfx, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSymbiosisOxygenEmitters, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSymbiosisAdherence, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSymbiosisSeeds, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSymbiosisAcousticTaps, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSymbiosisTuning, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSymbiosisFloraHashBucketHeads, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSymbiosisFloraHashNext, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSymbiosisMockBoids, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuSymbiosisMockFish, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuAmbientEntities, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuAmbientAups, SystemID.AIEcology)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;

            _jobLocksHeld = true;
            return true;
        }

        private void UnlockJobBuffers()
        {
            if (!_jobLocksHeld)
                return;

            IDataVault vault = _dataVault;
            if (vault != null)
                UnlockLockedJobBuffers(vault, 18);
            _jobLocksHeld = false;
        }

        private static void UnlockLockedJobBuffers(IDataVault vault, int locked)
        {
            if (locked >= 18) vault.TryUnlockBuffer(BufferID.ShinobuAmbientAups, SystemID.AIEcology);
            if (locked >= 17) vault.TryUnlockBuffer(BufferID.ShinobuAmbientEntities, SystemID.AIEcology);
            if (locked >= 16) vault.TryUnlockBuffer(BufferID.ShinobuSymbiosisMockFish, SystemID.AIEcology);
            if (locked >= 15) vault.TryUnlockBuffer(BufferID.ShinobuSymbiosisMockBoids, SystemID.AIEcology);
            if (locked >= 14) vault.TryUnlockBuffer(BufferID.ShinobuSymbiosisFloraHashNext, SystemID.AIEcology);
            if (locked >= 13) vault.TryUnlockBuffer(BufferID.ShinobuSymbiosisFloraHashBucketHeads, SystemID.AIEcology);
            if (locked >= 12) vault.TryUnlockBuffer(BufferID.ShinobuSymbiosisTuning, SystemID.AIEcology);
            if (locked >= 11) vault.TryUnlockBuffer(BufferID.ShinobuSymbiosisAcousticTaps, SystemID.AIEcology);
            if (locked >= 10) vault.TryUnlockBuffer(BufferID.ShinobuSymbiosisSeeds, SystemID.AIEcology);
            if (locked >= 9) vault.TryUnlockBuffer(BufferID.ShinobuSymbiosisAdherence, SystemID.AIEcology);
            if (locked >= 8) vault.TryUnlockBuffer(BufferID.ShinobuSymbiosisOxygenEmitters, SystemID.AIEcology);
            if (locked >= 7) vault.TryUnlockBuffer(BufferID.ShinobuSymbiosisScannerVfx, SystemID.AIEcology);
            if (locked >= 6) vault.TryUnlockBuffer(BufferID.ShinobuSymbiosisCounters, SystemID.AIEcology);
            if (locked >= 5) vault.TryUnlockBuffer(BufferID.ShinobuSymbiosisTelemetryRing, SystemID.AIEcology);
            if (locked >= 4) vault.TryUnlockBuffer(BufferID.ShinobuSymbiosisExchanges, SystemID.AIEcology);
            if (locked >= 3) vault.TryUnlockBuffer(BufferID.ShinobuSymbiosisLinks, SystemID.AIEcology);
            if (locked >= 2) vault.TryUnlockBuffer(BufferID.ShinobuSymbiosisFloraAups, SystemID.AIEcology);
            if (locked >= 1) vault.TryUnlockBuffer(BufferID.ShinobuSymbiosisFlora, SystemID.AIEcology);
        }

        private void TryRegisterTicks()
        {
            if (!_registeredColdTick)
                _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);

            if (!_registeredColdTick || !_registeredLateFrame)
                TryUnregisterTicks();
        }

        private void TryUnregisterTicks()
        {
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
            _floraHandle = default;
            _floraAupHandle = default;
            _linkHandle = default;
            _exchangeHandle = default;
            _telemetryHandle = default;
            _counterHandle = default;
            _csvScratchHandle = default;
            _scannerVfxHandle = default;
            _oxygenEmitterHandle = default;
            _adherenceHandle = default;
            _seedHandle = default;
            _acousticTapHandle = default;
            _tuningHandle = default;
            _floraBucketHeadHandle = default;
            _floraBucketNextHandle = default;
            _mockBoidHandle = default;
            _legacyScratchHandle = default;
            _mockFishHandle = default;
            _ambientEntityHandle = default;
            _ambientAupHandle = default;
            _anomalyFieldHandle = default;
        }

        private void ClearCachedState()
        {
            _dataVault = null;
            ResetVaultHandles();
            _csvTimestampTicks = 0L;
            _scheduleTicks = 0L;
            _telemetryCursor = 0;
            _simulationFrameCounter = 0u;
            _submarineIdleSeconds = 0f;
            _lastSolverMs = 0f;
            _dumpedFault = false;
            _hasSubmarineAup = false;
            _runtimeFlags = 0u;
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

        private static string ResolveCsvPath()
        {
            string root = ResolveProjectRoot();
            string precomputed = Path.Combine(root, CsvPrecomputedRelativePath);
            if (File.Exists(precomputed))
                return precomputed;

            return Path.Combine(root, CsvRelativePath);
        }

        private static string ResolveLegacyPath()
        {
            string root = ResolveProjectRoot();
            return Path.Combine(root, "Docs", "Archive", LegacyLinksFile);
        }

        private static string ResolveProjectRoot()
        {
            string assetsPath = Application.dataPath;
            DirectoryInfo parent = Directory.GetParent(assetsPath);
            return parent != null ? parent.FullName : assetsPath;
        }

        private static void DumpBlackBox(NativeArray<SymbiosisTelemetryEntry> telemetry, int cursor)
        {
            try
            {
                string root = ResolveProjectRoot();
                WriteBlackBoxFile(Path.Combine(root, DumpRelativePath), telemetry, cursor);
                WriteBlackBoxFile(Path.Combine(root, DumpSymbiosisRelativePath), telemetry, cursor);
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x53364450u, SourceHash, 0f);
            }
        }

        private static void WriteBlackBoxFile(string path, NativeArray<SymbiosisTelemetryEntry> telemetry, int cursor)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                int capacity = telemetry.Length;
                int dumpCount = math.min(capacity, math.max(0, cursor));
                int start = cursor < capacity ? 0 : cursor % capacity;
                writer.Write(DumpMagic);
                writer.Write(DumpVersion);
                writer.Write(capacity);
                writer.Write(dumpCount);
                writer.Write(cursor);
                writer.Write(start);
                writer.Write(UnsafeUtility.SizeOf<SymbiosisTelemetryEntry>());
                for (int offset = 0; offset < dumpCount; offset++)
                {
                    SymbiosisTelemetryEntry entry = telemetry[(start + offset) % capacity];
                    writer.Write(entry.Frame);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.ActiveExchanges);
                    writer.Write(entry.BiomassTransferred);
                    writer.Write(entry.SolverComputeTimeMs);
                    writer.Write(entry.OxygenEmitterCount);
                    writer.Write(entry.ToxemiaCount);
                    writer.Write(entry.CamouflageCount);
                    writer.Write(entry.SeedCount);
                    writer.Write(entry.AdherenceCount);
                    writer.Write(entry.AcousticTapCount);
                    writer.Write(entry.Flags);
                    writer.Write(entry.InvalidMathCount);
                    writer.Write(entry.OverflowCount);
                    writer.Write(entry.Pad0);
                    writer.Write(entry.Pad1);
                }
            }
        }

        private static void ParseCsvOverrides(
            NativeArray<byte> bytes,
            int length,
            ref SymbiosisTuningDTO tuning,
            NativeArray<SymbiosisChemicalLinkDTO> links)
        {
            if (!bytes.IsCreated)
                return;

            length = math.min(length, bytes.Length);
            int cursor = 0;
            int linkCursor = 0;
            while (cursor < length)
            {
                int keyStart = cursor;
                while (cursor < length && bytes[cursor] != (byte)',' && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                    cursor++;

                int keyEnd = cursor;
                uint keyHash = HashAsciiLower(bytes, keyStart, keyEnd);
                if (cursor >= length || bytes[cursor] != (byte)',')
                {
                    cursor = SkipLine(bytes, cursor, length);
                    continue;
                }

                cursor++;
                if (keyHash == 0x0DDB0669u)
                {
                    if (linkCursor < links.Length)
                    {
                        SymbiosisChemicalLinkDTO link = default;
                        cursor = ParseCsvUInt(bytes, cursor, length, out link.FloraHash);
                        cursor = ParseCsvUInt(bytes, cursor, length, out link.FaunaHash);
                        cursor = ParseCsvFloat(bytes, cursor, length, out link.ChemicalTransferRate);
                        cursor = ParseCsvUInt(bytes, cursor, length, out link.Flags);
                        links[linkCursor] = link;
                        linkCursor++;
                        tuning.ActiveLinkCount = math.max(tuning.ActiveLinkCount, linkCursor);
                    }

                    cursor = SkipLine(bytes, cursor, length);
                    continue;
                }

                float value;
                cursor = ParseCsvFloat(bytes, cursor, length, out value);
                if (math.isfinite(value))
                    ApplyCsvScalar(keyHash, value, ref tuning);

                cursor = SkipLine(bytes, cursor, length);
            }
        }

        private static void ApplyCsvScalar(uint keyHash, float value, ref SymbiosisTuningDTO tuning)
        {
            switch (keyHash)
            {
                case 0x7C0B181Eu:
                    tuning.FeedingRate = value;
                    break;
                case 0xB401DB20u:
                    tuning.ToxinPotency = value;
                    break;
                case 0xBDCCB218u:
                    tuning.CamouflageRadius = value;
                    break;
                case 0xC3C3B65Eu:
                    tuning.ParasiteGrowthSpeed = value;
                    break;
                case 0x17D802B1u:
                    tuning.OxygenRateScale = value;
                    break;
                case 0xB00FB719u:
                    tuning.GlobalQualityWeight = value;
                    break;
                case 0x4BA0F3B7u:
                    tuning.MacroThreshold = value;
                    break;
                case 0xDD7C761Fu:
                    tuning.SeedShipToxicBoost = value;
                    break;
                case 0x309EFED2u:
                    tuning.AcousticThreshold = value;
                    break;
                case 0xF2275492u:
                    tuning.FeedingRadius = value;
                    break;
            }
        }

        private static int ParseCsvFloat(NativeArray<byte> bytes, int cursor, int length, out float value)
        {
            value = 0f;
            while (cursor < length && (bytes[cursor] == (byte)' ' || bytes[cursor] == (byte)'\t'))
                cursor++;

            int sign = 1;
            if (cursor < length && bytes[cursor] == (byte)'-')
            {
                sign = -1;
                cursor++;
            }

            double result = 0.0d;
            bool found = false;
            while (cursor < length && bytes[cursor] >= (byte)'0' && bytes[cursor] <= (byte)'9')
            {
                found = true;
                result = (result * 10.0d) + (bytes[cursor] - (byte)'0');
                cursor++;
            }

            if (cursor < length && bytes[cursor] == (byte)'.')
            {
                cursor++;
                double factor = 0.1d;
                while (cursor < length && bytes[cursor] >= (byte)'0' && bytes[cursor] <= (byte)'9')
                {
                    found = true;
                    result += (bytes[cursor] - (byte)'0') * factor;
                    factor *= 0.1d;
                    cursor++;
                }
            }

            value = found ? (float)(result * sign) : 0f;
            while (cursor < length && bytes[cursor] != (byte)',' && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                cursor++;
            if (cursor < length && bytes[cursor] == (byte)',')
                cursor++;
            return cursor;
        }

        private static int ParseCsvUInt(NativeArray<byte> bytes, int cursor, int length, out uint value)
        {
            value = 0u;
            while (cursor < length && (bytes[cursor] == (byte)' ' || bytes[cursor] == (byte)'\t'))
                cursor++;

            bool hex = cursor + 1 < length && bytes[cursor] == (byte)'0' && (bytes[cursor + 1] == (byte)'x' || bytes[cursor + 1] == (byte)'X');
            if (hex)
                cursor += 2;

            while (cursor < length)
            {
                byte b = bytes[cursor];
                uint digit;
                if (b >= (byte)'0' && b <= (byte)'9')
                    digit = (uint)(b - (byte)'0');
                else if (hex && b >= (byte)'a' && b <= (byte)'f')
                    digit = (uint)(10 + b - (byte)'a');
                else if (hex && b >= (byte)'A' && b <= (byte)'F')
                    digit = (uint)(10 + b - (byte)'A');
                else
                    break;

                value = hex ? ((value << 4) | digit) : ((value * 10u) + digit);
                cursor++;
            }

            while (cursor < length && bytes[cursor] != (byte)',' && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                cursor++;
            if (cursor < length && bytes[cursor] == (byte)',')
                cursor++;
            return cursor;
        }

        private static int SkipLine(NativeArray<byte> bytes, int cursor, int length)
        {
            while (cursor < length && bytes[cursor] != (byte)'\n')
                cursor++;
            return cursor < length ? cursor + 1 : cursor;
        }

        private static uint HashAsciiLower(NativeArray<byte> bytes, int start, int end)
        {
            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte b = bytes[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash = (hash ^ b) * 16777619u;
            }

            return hash;
        }

        private static void ResolveLegacyLinkEncoding(NativeArray<byte> bytes, int bytesRead, out bool bigEndian, out int payloadOffset)
        {
            bigEndian = false;
            payloadOffset = 0;
            if (!bytes.IsCreated || bytesRead < LegacyLinksHeaderBytes)
                return;

            uint marker = ReadUInt32(bytes, 0, false);
            if (marker == LegacyLinksMagicLittleEndian)
            {
                payloadOffset = LegacyLinksHeaderBytes;
                return;
            }

            if (marker == LegacyLinksMagicBigEndian)
            {
                bigEndian = true;
                payloadOffset = LegacyLinksHeaderBytes;
            }
        }

        private static uint ReadUInt32(NativeArray<byte> bytes, int offset, bool bigEndian)
        {
            if (!bytes.IsCreated || offset < 0 || offset > bytes.Length - 4)
                return 0u;

            uint raw = (uint)(bytes[offset] |
                              (bytes[offset + 1] << 8) |
                              (bytes[offset + 2] << 16) |
                              (bytes[offset + 3] << 24));
            return bigEndian ? math.reversebytes(raw) : raw;
        }

        private static float ReadFloat32(NativeArray<byte> bytes, int offset, bool bigEndian, float fallback)
        {
            uint raw = ReadUInt32(bytes, offset, bigEndian);
            float value = math.asfloat(raw);
            return math.isfinite(value) ? value : fallback;
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
        internal static float3 AupToLocal(in SymbiosisAup48 position, in AbsoluteUniversePosition center)
        {
            double3 delta = new double3(
                (((double)position.GridX - center.GridX) * AupCellSizeMetersDouble) + (position.LocalX - center.LocalX),
                (((double)position.GridY - center.GridY) * AupCellSizeMetersDouble) + (position.LocalY - center.LocalY),
                (((double)position.GridZ - center.GridZ) * AupCellSizeMetersDouble) + (position.LocalZ - center.LocalZ));
            return math.all(math.isfinite(delta)) ? (float3)delta : new float3(0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 AupToLocal(in SymbiosisAup48 position, in SymbiosisAup48 center)
        {
            double3 delta = new double3(
                (((double)position.GridX - center.GridX) * AupCellSizeMetersDouble) + (position.LocalX - center.LocalX),
                (((double)position.GridY - center.GridY) * AupCellSizeMetersDouble) + (position.LocalY - center.LocalY),
                (((double)position.GridZ - center.GridZ) * AupCellSizeMetersDouble) + (position.LocalZ - center.LocalZ));
            return math.all(math.isfinite(delta)) ? (float3)delta : new float3(0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 AupToLocal(in AbsoluteUniversePosition position, in SymbiosisAup48 center)
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
        internal static double3 ToAbsoluteDouble3(in SymbiosisAup48 aup)
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
        internal static bool IsFiniteAup(in SymbiosisAup48 aup)
        {
            return math.isfinite(aup.LocalX) &&
                   math.isfinite(aup.LocalY) &&
                   math.isfinite(aup.LocalZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int3 ResolveSpatialCell(float3 localPosition, float cellSize)
        {
            return (int3)math.floor(localPosition / math.max(0.25f, cellSize));
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
        internal static int ResolveBucket(int3 cell)
        {
            return ResolveSpatialCellHash(cell) & SpatialBucketMask;
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
        internal static int3 ResolveSectorCoord(in SymbiosisAup48 aup, float sectorSize)
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
        internal static uint ResolveFrameSectorSeed(in AbsoluteUniversePosition centerAup, uint frame)
        {
            uint sectorHash = ResolveSectorHash(ResolveSectorCoord(in centerAup, DefaultSectorSizeMeters));
            return MixHash(sectorHash ^ (frame * 0x9E3779B9u) ^ 0x5336324Du);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint MixHash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }

        private static uint MixTelemetryHash(int active, int biomassMilli, int invalid, int overflow)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)active) * 16777619u;
            hash = (hash ^ (uint)biomassMilli) * 16777619u;
            hash = (hash ^ (uint)invalid) * 16777619u;
            hash = (hash ^ (uint)overflow) * 16777619u;
            return hash != 0u ? hash : 1u;
        }
    }

    public static class SymbiosisLayoutManifest
    {
        private static bool _verified;

        public static void VerifyColdBoot()
        {
            if (_verified)
                return;

            AssertSize<SymbiosisExchangeDTO>(16);
            AssertSize<SymbiosisChemicalLinkDTO>(16);
            AssertSize<SymbiosisAup48>(48);
            AssertSize<SymbiosisFloraDTO>(48);
            AssertSize<SymbiosisFloraAupDTO>(64);
            AssertSize<SymbiosisTuningDTO>(64);
            AssertSize<SymbiosisCounterDTO>(64);
            AssertSize<SymbiosisTelemetryEntry>(64);
            AssertSize<MockBoidArray>(16);
            AssertSize<MockFishSymbiosisDTO>(64);
            AssertSize<ScannerVfxDTO>(32);
            AssertSize<SymbiosisOxygenEmitterDTO>(32);
            AssertSize<AdherenceDTO>(32);
            AssertSize<FloraSeedDTO>(32);
            AssertSize<SymbiosisAcousticTapDTO>(64);
            AssertOffset<SymbiosisExchangeDTO>(nameof(SymbiosisExchangeDTO.FloraHash), 0);
            AssertOffset<SymbiosisExchangeDTO>(nameof(SymbiosisExchangeDTO.FaunaHash), 4);
            AssertOffset<SymbiosisExchangeDTO>(nameof(SymbiosisExchangeDTO.ChemicalTransferRate), 8);
            AssertOffset<SymbiosisExchangeDTO>(nameof(SymbiosisExchangeDTO._pad0), 12);
            AssertOffset<SymbiosisAup48>(nameof(SymbiosisAup48.GridX), 0);
            AssertOffset<SymbiosisAup48>(nameof(SymbiosisAup48.GridY), 8);
            AssertOffset<SymbiosisAup48>(nameof(SymbiosisAup48.GridZ), 16);
            AssertOffset<SymbiosisAup48>(nameof(SymbiosisAup48.LocalX), 24);
            AssertOffset<SymbiosisAup48>(nameof(SymbiosisAup48.LocalY), 28);
            AssertOffset<SymbiosisAup48>(nameof(SymbiosisAup48.LocalZ), 32);
            AssertOffset<SymbiosisCounterDTO>(nameof(SymbiosisCounterDTO._pad0), 56);
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
            throw new CriticalBootException("[SymbiosisLayoutManifest] Layout mismatch " + label + " expected=" + expected + " observed=" + observed);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct SymbiosisExchangeDTO
    {
        [FieldOffset(0)] public uint FloraHash;
        [FieldOffset(4)] public uint FaunaHash;
        [FieldOffset(8)] public float ChemicalTransferRate;
        [FieldOffset(12)] public float _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct SymbiosisChemicalLinkDTO
    {
        [FieldOffset(0)] public uint FloraHash;
        [FieldOffset(4)] public uint FaunaHash;
        [FieldOffset(8)] public float ChemicalTransferRate;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct SymbiosisAup48
    {
        [FieldOffset(0)] public long GridX;
        [FieldOffset(8)] public long GridY;
        [FieldOffset(16)] public long GridZ;
        [FieldOffset(24)] public float LocalX;
        [FieldOffset(28)] public float LocalY;
        [FieldOffset(32)] public float LocalZ;
        [FieldOffset(36)] public uint _pad0;
        [FieldOffset(40)] public ulong _pad1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SymbiosisAup48 FromAup(in AbsoluteUniversePosition aup)
        {
            return new SymbiosisAup48
            {
                GridX = aup.GridX,
                GridY = aup.GridY,
                GridZ = aup.GridZ,
                LocalX = aup.LocalX,
                LocalY = aup.LocalY,
                LocalZ = aup.LocalZ,
                _pad0 = 0u,
                _pad1 = 0UL
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AbsoluteUniversePosition ToAup()
        {
            return new AbsoluteUniversePosition
            {
                GridX = GridX,
                GridY = GridY,
                GridZ = GridZ,
                LocalX = LocalX,
                LocalY = LocalY,
                LocalZ = LocalZ
            };
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct SymbiosisFloraDTO
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float Biomass;
        [FieldOffset(16)] public uint FloraHash;
        [FieldOffset(20)] public uint ChemicalMask;
        [FieldOffset(24)] public float OxygenRate;
        [FieldOffset(28)] public float ToxicPotency;
        [FieldOffset(32)] public float CamouflageRadius;
        [FieldOffset(36)] public float FeedingRadius;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SymbiosisFloraAupDTO
    {
        [FieldOffset(0)] public SymbiosisAup48 PositionAup;
        [FieldOffset(48)] public uint FloraHash;
        [FieldOffset(52)] public uint SectorHash;
        [FieldOffset(56)] public int SpatialCellHash;
        [FieldOffset(60)] public uint StableSeed;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SymbiosisTuningDTO
    {
        [FieldOffset(0)] public float FeedingRate;
        [FieldOffset(4)] public float ToxinPotency;
        [FieldOffset(8)] public float CamouflageRadius;
        [FieldOffset(12)] public float ParasiteGrowthSpeed;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float MacroThreshold;
        [FieldOffset(24)] public float OxygenRateScale;
        [FieldOffset(28)] public float SeedShipToxicBoost;
        [FieldOffset(32)] public float AcousticThreshold;
        [FieldOffset(36)] public float FeedingRadius;
        [FieldOffset(40)] public float SimulationTickDelta;
        [FieldOffset(44)] public float CorruptionLevel;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public int ActiveFloraCount;
        [FieldOffset(56)] public int ActiveLinkCount;
        [FieldOffset(60)] public uint _pad0;

        public static SymbiosisTuningDTO Default()
        {
            return new SymbiosisTuningDTO
            {
                FeedingRate = 0.04f,
                ToxinPotency = 0.65f,
                CamouflageRadius = 2.0f,
                ParasiteGrowthSpeed = 0.0167f,
                GlobalQualityWeight = 1.0f,
                MacroThreshold = 0.3f,
                OxygenRateScale = 0.08f,
                SeedShipToxicBoost = 2.5f,
                AcousticThreshold = 7.0f,
                FeedingRadius = 5.0f,
                SimulationTickDelta = 1.0f,
                CorruptionLevel = 0.0f,
                Flags = ShinobuFloraFaunaSymbiosisSolver.TuningFlagEmergencyMock,
                ActiveFloraCount = 0,
                ActiveLinkCount = 0,
                _pad0 = 0u
            };
        }

        public static SymbiosisTuningDTO Sanitize(SymbiosisTuningDTO value)
        {
            SymbiosisTuningDTO fallback = Default();
            value.FeedingRate = SanitizePositive(value.FeedingRate, fallback.FeedingRate);
            value.ToxinPotency = SanitizePositive(value.ToxinPotency, fallback.ToxinPotency);
            value.CamouflageRadius = SanitizePositive(value.CamouflageRadius, fallback.CamouflageRadius);
            value.ParasiteGrowthSpeed = SanitizePositive(value.ParasiteGrowthSpeed, fallback.ParasiteGrowthSpeed);
            value.GlobalQualityWeight = math.saturate(math.isfinite(value.GlobalQualityWeight) ? value.GlobalQualityWeight : fallback.GlobalQualityWeight);
            value.MacroThreshold = math.clamp(SanitizePositive(value.MacroThreshold, fallback.MacroThreshold), 0.05f, 0.95f);
            value.OxygenRateScale = SanitizePositive(value.OxygenRateScale, fallback.OxygenRateScale);
            value.SeedShipToxicBoost = SanitizePositive(value.SeedShipToxicBoost, fallback.SeedShipToxicBoost);
            value.AcousticThreshold = SanitizePositive(value.AcousticThreshold, fallback.AcousticThreshold);
            value.FeedingRadius = SanitizePositive(value.FeedingRadius, fallback.FeedingRadius);
            value.SimulationTickDelta = SanitizePositive(value.SimulationTickDelta, fallback.SimulationTickDelta);
            value.CorruptionLevel = math.saturate(math.isfinite(value.CorruptionLevel) ? value.CorruptionLevel : 0f);
            value.ActiveFloraCount = math.max(0, value.ActiveFloraCount);
            value.ActiveLinkCount = math.max(0, value.ActiveLinkCount);
            return value;
        }

        private static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SymbiosisCounterDTO
    {
        [FieldOffset(0)] public int ActiveExchanges;
        [FieldOffset(4)] public int BiomassTransferredMilli;
        [FieldOffset(8)] public int ToxemiaCount;
        [FieldOffset(12)] public int CamouflageCount;
        [FieldOffset(16)] public int OxygenEmitterCount;
        [FieldOffset(20)] public int SeedCount;
        [FieldOffset(24)] public int AdherenceCount;
        [FieldOffset(28)] public int AcousticTapCount;
        [FieldOffset(32)] public int InvalidMath;
        [FieldOffset(36)] public int OverflowCount;
        [FieldOffset(40)] public int CsvLoaded;
        [FieldOffset(44)] public int Initialized;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SymbiosisTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public int ActiveExchanges;
        [FieldOffset(12)] public float BiomassTransferred;
        [FieldOffset(16)] public float SolverComputeTimeMs;
        [FieldOffset(20)] public int OxygenEmitterCount;
        [FieldOffset(24)] public int ToxemiaCount;
        [FieldOffset(28)] public int CamouflageCount;
        [FieldOffset(32)] public int SeedCount;
        [FieldOffset(36)] public int AdherenceCount;
        [FieldOffset(40)] public int AcousticTapCount;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public int InvalidMathCount;
        [FieldOffset(52)] public int OverflowCount;
        [FieldOffset(56)] public uint Pad0;
        [FieldOffset(60)] public uint Pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct MockBoidArray
    {
        [FieldOffset(0)] public int StartIndex;
        [FieldOffset(4)] public int Count;
        [FieldOffset(8)] public uint StableSeed;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MockFishSymbiosisDTO
    {
        [FieldOffset(0)] public SymbiosisAup48 PositionAup;
        [FieldOffset(48)] public float Biomass;
        [FieldOffset(52)] public uint SpeciesHash;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint StableSeed;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ScannerVfxDTO
    {
        [FieldOffset(0)] public float3 HitLocal;
        [FieldOffset(12)] public float HitDistance;
        [FieldOffset(16)] public float ScanProgress;
        [FieldOffset(20)] public uint TargetHash;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public float BeamScore;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SymbiosisOxygenEmitterDTO
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float Oxygen01;
        [FieldOffset(16)] public uint SectorHash;
        [FieldOffset(20)] public float RadiusMeters;
        [FieldOffset(24)] public uint FloraHash;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AdherenceDTO
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float Growth01;
        [FieldOffset(16)] public uint HostHash;
        [FieldOffset(20)] public uint FloraHash;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Frame;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FloraSeedDTO
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float Viability01;
        [FieldOffset(16)] public uint FloraHash;
        [FieldOffset(20)] public uint CarrierHash;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SymbiosisAcousticTapDTO
    {
        [FieldOffset(0)] public SymbiosisAup48 PositionAup;
        [FieldOffset(48)] public float Magnitude01;
        [FieldOffset(52)] public float RadiusMeters;
        [FieldOffset(56)] public uint SourceHash;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct SymbiosisAnomalyFieldMirror
    {
        [FieldOffset(0)] public double3 EpicenterAUP;
        [FieldOffset(24)] public float Radius;
        [FieldOffset(28)] public float CorruptionLevel;
        [FieldOffset(32)] public uint GlitchHash;
        [FieldOffset(36)] public uint _pad0;
        [FieldOffset(40)] public ulong _pad1;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateEmergencyMockSymbiosisJob : IJob
    {
        [NoAlias] public NativeArray<SymbiosisFloraDTO> Flora;
        [NoAlias] public NativeArray<SymbiosisFloraAupDTO> FloraAups;
        [NoAlias] public NativeArray<SymbiosisChemicalLinkDTO> Links;
        [NoAlias] public NativeArray<SymbiosisTuningDTO> Tuning;
        [NoAlias] public NativeArray<SymbiosisCounterDTO> Counters;
        [NoAlias] public NativeArray<MockBoidArray> MockBoids;
        [NoAlias] public NativeArray<MockFishSymbiosisDTO> MockFish;
        public AbsoluteUniversePosition CenterAup;
        public float GlobalQualityWeight;
        public int FloraCount;
        public int MockFishCount;
        public int LinkCount;
        public uint Seed;

        public void Execute()
        {
            int floraLimit = math.min(FloraCount, math.min(Flora.Length, FloraAups.Length));
            int fishLimit = math.min(MockFishCount, MockFish.Length);
            int linkLimit = math.min(LinkCount, Links.Length);

            SymbiosisTuningDTO tuning = SymbiosisTuningDTO.Default();
            tuning.GlobalQualityWeight = math.saturate(GlobalQualityWeight);
            tuning.ActiveFloraCount = floraLimit;
            tuning.ActiveLinkCount = math.min(5, linkLimit);
            if (Tuning.IsCreated && Tuning.Length > 0)
                Tuning[0] = tuning;

            Unity.Mathematics.Random rng = new Unity.Mathematics.Random(Seed != 0u ? Seed : 1u);
            for (int i = 0; i < floraLimit; i++)
            {
                int lane = i % 5;
                float ring = 10f + ((i & 31) * 1.7f);
                float angle = (i * 2.3999631f) + rng.NextFloat(-0.08f, 0.08f);
                float3 local = new float3(math.cos(angle) * ring, -6f + ((i % 7) * 0.75f), math.sin(angle) * ring);
                uint floraHash = ResolveFloraHash(lane);
                uint flags = ShinobuFloraFaunaSymbiosisSolver.FloraFlagActive | ResolveFloraFlags(lane);
                float feedingRadius = lane == 1 ? 4.0f : 5.0f;
                AbsoluteUniversePosition aup = ShinobuFloraFaunaSymbiosisSolver.OffsetAup(in CenterAup, local);
                int3 sectorCoord = ShinobuFloraFaunaSymbiosisSolver.ResolveSectorCoord(in aup, 64f);
                Flora[i] = new SymbiosisFloraDTO
                {
                    LocalPosition = local,
                    Biomass = 4f + (lane * 0.7f),
                    FloraHash = floraHash,
                    ChemicalMask = 0u,
                    OxygenRate = lane == 2 ? 1.0f : 0.25f,
                    ToxicPotency = lane == 1 ? 1.0f : 0.05f,
                    CamouflageRadius = lane == 0 ? 2.0f : 0.5f,
                    FeedingRadius = feedingRadius,
                    Flags = flags,
                    _pad0 = 0u
                };
                FloraAups[i] = new SymbiosisFloraAupDTO
                {
                    PositionAup = SymbiosisAup48.FromAup(in aup),
                    FloraHash = floraHash,
                    SectorHash = ShinobuFloraFaunaSymbiosisSolver.ResolveSectorHash(sectorCoord),
                    SpatialCellHash = 0,
                    StableSeed = ShinobuFloraFaunaSymbiosisSolver.MixHash(Seed ^ (uint)i ^ 0x464C4F52u)
                };
            }

            for (int i = floraLimit; i < Flora.Length; i++)
                Flora[i] = default;
            for (int i = floraLimit; i < FloraAups.Length; i++)
                FloraAups[i] = default;

            WriteDefaultLinks(linkLimit);

            if (MockBoids.IsCreated && MockBoids.Length > 0)
            {
                MockBoids[0] = new MockBoidArray
                {
                    StartIndex = 0,
                    Count = fishLimit,
                    StableSeed = Seed != 0u ? Seed : 1u,
                    Flags = 1u
                };
            }

            for (int i = 0; i < fishLimit; i++)
            {
                int targetFlora = floraLimit > 0 ? i % floraLimit : 0;
                AbsoluteUniversePosition baseAup = targetFlora < FloraAups.Length ? FloraAups[targetFlora].PositionAup.ToAup() : CenterAup;
                float3 offset = new float3(((i & 3) - 1.5f) * 0.75f, 0.2f, (((i >> 2) & 3) - 1.5f) * 0.75f);
                uint species = (i % 6) == 0
                    ? ShinobuFloraFaunaSymbiosisSolver.FaunaHashCarnivore
                    : ShinobuFloraFaunaSymbiosisSolver.FaunaHashHerbivore;
                AbsoluteUniversePosition fishAup = ShinobuFloraFaunaSymbiosisSolver.OffsetAup(in baseAup, offset);
                MockFish[i] = new MockFishSymbiosisDTO
                {
                    PositionAup = SymbiosisAup48.FromAup(in fishAup),
                    Biomass = species == ShinobuFloraFaunaSymbiosisSolver.FaunaHashCarnivore ? 3.5f : 1.0f,
                    SpeciesHash = species,
                    Flags = ShinobuFloraFaunaSymbiosisSolver.FaunaFlagActive,
                    StableSeed = ShinobuFloraFaunaSymbiosisSolver.MixHash(Seed ^ (uint)i ^ 0x4D464953u)
                };
            }

            for (int i = fishLimit; i < MockFish.Length; i++)
                MockFish[i] = default;

            if (Counters.IsCreated && Counters.Length > 0)
            {
                SymbiosisCounterDTO counter = default;
                counter.Initialized = 1;
                counter.Flags = ShinobuFloraFaunaSymbiosisSolver.TuningFlagEmergencyMock;
                Counters[0] = counter;
            }
        }

        private void WriteDefaultLinks(int linkLimit)
        {
            for (int i = 0; i < Links.Length; i++)
                Links[i] = default;

            if (linkLimit > 0) Links[0] = Link(ShinobuFloraFaunaSymbiosisSolver.FloraHashHealingKelp, ShinobuFloraFaunaSymbiosisSolver.FaunaHashHerbivore, 1.00f);
            if (linkLimit > 1) Links[1] = Link(ShinobuFloraFaunaSymbiosisSolver.FloraHashOxygenKelp, ShinobuFloraFaunaSymbiosisSolver.FaunaHashHerbivore, 0.75f);
            if (linkLimit > 2) Links[2] = Link(ShinobuFloraFaunaSymbiosisSolver.FloraHashGlowMoss, ShinobuFloraFaunaSymbiosisSolver.FaunaHashHerbivore, 0.60f);
            if (linkLimit > 3) Links[3] = Link(ShinobuFloraFaunaSymbiosisSolver.FloraHashSporeCoral, ShinobuFloraFaunaSymbiosisSolver.FaunaHashHerbivore, 0.35f);
            if (linkLimit > 4) Links[4] = Link(ShinobuFloraFaunaSymbiosisSolver.FloraHashToxicAnemone, ShinobuFloraFaunaSymbiosisSolver.FaunaHashCarnivore, 0.10f);
        }

        private static SymbiosisChemicalLinkDTO Link(uint floraHash, uint faunaHash, float rate)
        {
            return new SymbiosisChemicalLinkDTO
            {
                FloraHash = floraHash,
                FaunaHash = faunaHash,
                ChemicalTransferRate = rate,
                Flags = ShinobuFloraFaunaSymbiosisSolver.LinkFlagCompatible
            };
        }

        private static uint ResolveFloraHash(int lane)
        {
            switch (lane)
            {
                case 0: return ShinobuFloraFaunaSymbiosisSolver.FloraHashHealingKelp;
                case 1: return ShinobuFloraFaunaSymbiosisSolver.FloraHashToxicAnemone;
                case 2: return ShinobuFloraFaunaSymbiosisSolver.FloraHashOxygenKelp;
                case 3: return ShinobuFloraFaunaSymbiosisSolver.FloraHashGlowMoss;
                default: return ShinobuFloraFaunaSymbiosisSolver.FloraHashSporeCoral;
            }
        }

        private static uint ResolveFloraFlags(int lane)
        {
            switch (lane)
            {
                case 0: return ShinobuFloraFaunaSymbiosisSolver.FloraFlagCamouflage | ShinobuFloraFaunaSymbiosisSolver.FloraFlagOxygen;
                case 1: return ShinobuFloraFaunaSymbiosisSolver.FloraFlagToxic;
                case 2: return ShinobuFloraFaunaSymbiosisSolver.FloraFlagOxygen;
                case 3: return ShinobuFloraFaunaSymbiosisSolver.FloraFlagGlow | ShinobuFloraFaunaSymbiosisSolver.FloraFlagOxygen;
                default: return ShinobuFloraFaunaSymbiosisSolver.FloraFlagSpore | ShinobuFloraFaunaSymbiosisSolver.FloraFlagToxic;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildSymbiosisFloraSpatialHashJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<SymbiosisFloraDTO> Flora;
        [NoAlias] public NativeArray<SymbiosisFloraAupDTO> FloraAups;
        [NoAlias] public NativeArray<int> BucketHeads;
        [NoAlias] public NativeArray<int> BucketNext;
        public AbsoluteUniversePosition CenterAup;
        public float CellSizeMeters;
        public int Count;

        public void Execute()
        {
            for (int i = 0; i < BucketHeads.Length; i++)
                BucketHeads[i] = -1;

            int count = math.min(Count, math.min(Flora.Length, math.min(FloraAups.Length, BucketNext.Length)));
            for (int i = 0; i < count; i++)
            {
                SymbiosisFloraDTO flora = Flora[i];
                if ((flora.Flags & ShinobuFloraFaunaSymbiosisSolver.FloraFlagActive) == 0u)
                {
                    BucketNext[i] = -1;
                    continue;
                }

                SymbiosisFloraAupDTO aup = FloraAups[i];
                float3 local = ShinobuFloraFaunaSymbiosisSolver.AupToLocal(in aup.PositionAup, in CenterAup);
                int3 cell = ShinobuFloraFaunaSymbiosisSolver.ResolveSpatialCell(local, CellSizeMeters);
                int bucket = ShinobuFloraFaunaSymbiosisSolver.ResolveBucket(cell);
                aup.SpatialCellHash = ShinobuFloraFaunaSymbiosisSolver.ResolveSpatialCellHash(cell);
                FloraAups[i] = aup;
                BucketNext[i] = BucketHeads[bucket];
                BucketHeads[bucket] = i;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct SymbiosisExchangeKernelJob : IJob
    {
        [NoAlias] public NativeArray<SymbiosisFloraDTO> Flora;
        [ReadOnly, NoAlias] public NativeArray<SymbiosisFloraAupDTO> FloraAups;
        [ReadOnly, NoAlias] public NativeArray<SymbiosisChemicalLinkDTO> Links;
        [NoAlias] public NativeArray<SymbiosisExchangeDTO> Exchanges;
        [NoAlias] public NativeArray<SymbiosisCounterDTO> Counters;
        [NoAlias] public NativeArray<ScannerVfxDTO> ScannerVfx;
        [NoAlias] public NativeArray<SymbiosisOxygenEmitterDTO> OxygenEmitters;
        [NoAlias] public NativeArray<AdherenceDTO> Adherence;
        [NoAlias] public NativeArray<FloraSeedDTO> Seeds;
        [NoAlias] public NativeArray<SymbiosisAcousticTapDTO> AcousticTaps;
        [ReadOnly, NoAlias] public NativeArray<SymbiosisTuningDTO> Tuning;
        [ReadOnly, NoAlias] public NativeArray<int> BucketHeads;
        [ReadOnly, NoAlias] public NativeArray<int> BucketNext;
        [ReadOnly, NoAlias] public NativeArray<MockBoidArray> MockBoids;
        [NoAlias] public NativeArray<MockFishSymbiosisDTO> MockFish;
        [ReadOnly, NoAlias] public NativeArray<AmbientEntityDTO> AmbientEntities;
        [ReadOnly, NoAlias] public NativeArray<AmbientEntityAupDTO> AmbientAups;
        [ReadOnly, NoAlias] public NativeArray<SymbiosisAnomalyFieldMirror> AnomalyField;
        public AbsoluteUniversePosition CenterAup;
        public AbsoluteUniversePosition SubmarineAup;
        public float SubmarineIdleSeconds;
        public uint Frame;
        public float CellSizeMeters;
        public float SectorSizeMeters;
        public float SimulationTickDelta;
        public int FloraCount;
        public int AmbientFishCount;
        public int MockFishCount;
        public int MaxNeighborSamplesBase;
        public int MaxSpatialHashChainSteps;

        public void Execute()
        {
            if (!Counters.IsCreated || Counters.Length <= 0 || !Tuning.IsCreated || Tuning.Length <= 0)
                return;

            SymbiosisTuningDTO tuning = SymbiosisTuningDTO.Sanitize(Tuning[0]);
            float quality = math.saturate(tuning.GlobalQualityWeight);
            float qualityCurve = quality * quality * (3f - (2f * quality));
            int floraCount = math.min(FloraCount, math.min(Flora.Length, FloraAups.Length));
            int linkCount = math.min(tuning.ActiveLinkCount > 0 ? tuning.ActiveLinkCount : Links.Length, Links.Length);
            SymbiosisCounterDTO counter = default;
            counter.Initialized = 1;
            counter.Frame = Frame;
            counter.Flags = tuning.Flags;

            AggregateOxygenAndSpores(ref counter, tuning, qualityCurve, floraCount);

            float macroGate = math.step(quality, tuning.MacroThreshold);
            if (macroGate > 0.5f)
                ApplyMacroAverage(ref counter, tuning, qualityCurve, floraCount);
            else
                ApplyMicroExchange(ref counter, tuning, qualityCurve, floraCount, linkCount);

            Counters[0] = counter;
        }

        private void AggregateOxygenAndSpores(ref SymbiosisCounterDTO counter, SymbiosisTuningDTO tuning, float qualityCurve, int floraCount)
        {
            int floraStride = math.max(1, (int)math.round(math.lerp(8f, 1f, qualityCurve)));
            for (int i = 0; i < floraCount; i += floraStride)
            {
                SymbiosisFloraDTO flora = Flora[i];
                if ((flora.Flags & ShinobuFloraFaunaSymbiosisSolver.FloraFlagActive) == 0u)
                    continue;

                SymbiosisFloraAupDTO aup = FloraAups[i];
                float corruption = ResolveCorruption01(in aup.PositionAup);
                if (corruption > 0.001f)
                {
                    flora.Flags |= ShinobuFloraFaunaSymbiosisSolver.FloraFlagBlighted | ShinobuFloraFaunaSymbiosisSolver.FloraFlagToxic;
                    flora.ToxicPotency = math.max(flora.ToxicPotency, corruption * tuning.SeedShipToxicBoost);
                    flora.ChemicalMask = 0x7B2CBFu;
                    Flora[i] = flora;
                    counter.Flags |= ShinobuFloraFaunaSymbiosisSolver.TuningFlagAnomalyMirror;
                }

                if ((flora.Flags & ShinobuFloraFaunaSymbiosisSolver.FloraFlagOxygen) != 0u && flora.Biomass > 0f)
                    WriteOxygenEmitter(ref counter, flora, aup, tuning, qualityCurve);

                if ((flora.Flags & ShinobuFloraFaunaSymbiosisSolver.FloraFlagSpore) != 0u && SubmarineIdleSeconds >= 60f)
                    WriteAdherenceIfSubmarineInSporeZone(ref counter, flora, aup, tuning);
            }
        }

        private void ApplyMacroAverage(ref SymbiosisCounterDTO counter, SymbiosisTuningDTO tuning, float qualityCurve, int floraCount)
        {
            float totalBiomass = 0f;
            int active = 0;
            for (int i = 0; i < floraCount; i++)
            {
                SymbiosisFloraDTO flora = Flora[i];
                if ((flora.Flags & ShinobuFloraFaunaSymbiosisSolver.FloraFlagActive) == 0u)
                    continue;
                totalBiomass += math.max(0f, flora.Biomass);
                active++;
            }

            float avg = totalBiomass / math.max(1, active);
            float macroRate = avg * tuning.FeedingRate * SimulationTickDelta * math.lerp(0.02f, 0.18f, qualityCurve);
            int stride = math.max(1, (int)math.round(math.lerp(16f, 2f, qualityCurve)));
            ProcessMacroMockFish(ref counter, macroRate, stride);

            float floraLoss = macroRate * 0.015f;
            for (int i = 0; i < floraCount; i += stride)
            {
                SymbiosisFloraDTO flora = Flora[i];
                if ((flora.Flags & ShinobuFloraFaunaSymbiosisSolver.FloraFlagActive) == 0u)
                    continue;
                flora.Biomass = math.max(0.01f, flora.Biomass - floraLoss);
                Flora[i] = flora;
            }
        }

        private void ProcessMacroMockFish(ref SymbiosisCounterDTO counter, float macroRate, int stride)
        {
            int count = ResolveMockCount();
            for (int i = 0; i < count; i += stride)
            {
                MockFishSymbiosisDTO fish = MockFish[i];
                if ((fish.Flags & ShinobuFloraFaunaSymbiosisSolver.FaunaFlagActive) == 0u)
                    continue;

                float transfer = math.max(0f, macroRate);
                fish.Biomass += transfer;
                MockFish[i] = fish;
                WriteExchange(ref counter, 0x4D414352u, fish.StableSeed, transfer);
            }
        }

        private void ApplyMicroExchange(ref SymbiosisCounterDTO counter, SymbiosisTuningDTO tuning, float qualityCurve, int floraCount, int linkCount)
        {
            int maxSamples = math.max(3, (int)math.round(math.lerp(4f, MaxNeighborSamplesBase, qualityCurve)));
            int mockCount = ResolveMockCount();
            for (int i = 0; i < mockCount; i++)
            {
                MockFishSymbiosisDTO fish = MockFish[i];
                if ((fish.Flags & ShinobuFloraFaunaSymbiosisSolver.FaunaFlagActive) == 0u)
                    continue;

                AbsoluteUniversePosition fishAup = fish.PositionAup.ToAup();
                ProcessOneFish(ref counter, ref fishAup, ref fish.Biomass, ref fish.Flags, fish.SpeciesHash, fish.StableSeed, tuning, floraCount, linkCount, maxSamples, true);
                fish.PositionAup = SymbiosisAup48.FromAup(in fishAup);
                MockFish[i] = fish;
            }

            int ambientStride = math.max(1, (int)math.round(math.lerp(10f, 1f, qualityCurve)));
            int ambientCount = math.min(AmbientFishCount, math.min(AmbientEntities.Length, AmbientAups.Length));
            for (int i = 0; i < ambientCount; i += ambientStride)
            {
                AmbientEntityAupDTO meta = AmbientAups[i];
                if ((meta.Flags & ShinobuEcosystemBalancer.EntityFlagActive) == 0u)
                    continue;

                AmbientEntityDTO entity = AmbientEntities[i];
                AbsoluteUniversePosition fishAup = meta.PositionAup;
                float biomass = entity.Biomass;
                uint flags = meta.Flags;
                uint stableHash = meta.StableSeed != 0u ? meta.StableSeed : ShinobuFloraFaunaSymbiosisSolver.MixHash((uint)i ^ entity.SpeciesHash);
                ProcessOneFish(ref counter, ref fishAup, ref biomass, ref flags, entity.SpeciesHash, stableHash, tuning, floraCount, linkCount, maxSamples, false);
            }
        }

        private void ProcessOneFish(
            ref SymbiosisCounterDTO counter,
            ref AbsoluteUniversePosition fishAup,
            ref float fishBiomass,
            ref uint fishFlags,
            uint faunaHash,
            uint faunaStableHash,
            SymbiosisTuningDTO tuning,
            int floraCount,
            int linkCount,
            int maxSamples,
            bool mutateFish)
        {
            float3 fishLocal = ShinobuFloraFaunaSymbiosisSolver.AupToLocal(in fishAup, in CenterAup);
            if (!math.all(math.isfinite(fishLocal)))
            {
                counter.InvalidMath++;
                return;
            }

            int3 baseCell = ShinobuFloraFaunaSymbiosisSolver.ResolveSpatialCell(fishLocal, CellSizeMeters);
            int sampled = 0;
            int bestIndex = -1;
            float bestDistSq = float.MaxValue;
            float bestRate = 0f;
            for (int z = -1; z <= 1; z++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        int bucket = ShinobuFloraFaunaSymbiosisSolver.ResolveBucket(baseCell + new int3(x, y, z));
                        int floraIndex = BucketHeads[bucket];
                        int chain = 0;
                        while (floraIndex >= 0 && floraIndex < floraCount && chain < MaxSpatialHashChainSteps && sampled < maxSamples)
                        {
                            sampled++;
                            SymbiosisFloraDTO flora = Flora[floraIndex];
                            SymbiosisFloraAupDTO floraAup = FloraAups[floraIndex];
                            float3 delta = ShinobuFloraFaunaSymbiosisSolver.AupToLocal(in floraAup.PositionAup, in fishAup);
                            float distSq = math.lengthsq(delta);
                            if (!math.isfinite(distSq))
                            {
                                counter.InvalidMath++;
                                floraIndex = BucketNext[floraIndex];
                                chain++;
                                continue;
                            }

                            float corruption = ResolveCorruption01(in floraAup.PositionAup);
                            float toxin = flora.ToxicPotency * tuning.ToxinPotency * (1f + (corruption * tuning.SeedShipToxicBoost));
                            if ((flora.Flags & ShinobuFloraFaunaSymbiosisSolver.FloraFlagToxic) != 0u && distSq <= 16f * 16f)
                            {
                                fishFlags |= ShinobuFloraFaunaSymbiosisSolver.FaunaFlagToxemia;
                                counter.ToxemiaCount++;
                                WriteScannerVfx(ref counter, flora, delta, toxin);
                            }

                            float camoRadius = math.max(tuning.CamouflageRadius, flora.CamouflageRadius);
                            if ((flora.Flags & ShinobuFloraFaunaSymbiosisSolver.FloraFlagCamouflage) != 0u && distSq <= camoRadius * camoRadius)
                            {
                                fishFlags |= ShinobuFloraFaunaSymbiosisSolver.FaunaFlagCamouflaged;
                                counter.CamouflageCount++;
                            }

                            float linkRate = ResolveLinkRate(flora.FloraHash, faunaHash, linkCount);
                            float feedRadius = math.max(0.25f, math.max(tuning.FeedingRadius, flora.FeedingRadius));
                            if (linkRate > 0f && distSq <= feedRadius * feedRadius && distSq < bestDistSq && flora.Biomass > 0f)
                            {
                                bestDistSq = distSq;
                                bestIndex = floraIndex;
                                bestRate = linkRate;
                            }

                            floraIndex = BucketNext[floraIndex];
                            chain++;
                        }
                    }
                }
            }

            if (bestIndex < 0)
                return;

            SymbiosisFloraDTO best = Flora[bestIndex];
            float radius = math.max(0.25f, math.max(tuning.FeedingRadius, best.FeedingRadius));
            float atten = 1f - math.saturate(math.sqrt(math.max(0f, bestDistSq)) / radius);
            float transfer = math.min(best.Biomass, tuning.FeedingRate * bestRate * SimulationTickDelta * math.max(0.05f, atten));
            if (!math.isfinite(transfer) || transfer <= 0f)
                return;

            best.Biomass = math.max(0f, best.Biomass - transfer);
            Flora[bestIndex] = best;
            if (mutateFish)
                fishBiomass += transfer;

            if ((best.Flags & ShinobuFloraFaunaSymbiosisSolver.FloraFlagGlow) != 0u)
            {
                fishFlags |= ShinobuFloraFaunaSymbiosisSolver.FaunaFlagCarryingPollen;
                WriteSeed(ref counter, best, bestIndex, faunaStableHash);
            }

            WriteExchange(ref counter, best.FloraHash, faunaStableHash, transfer);
            if (sampled >= tuning.AcousticThreshold)
                WriteAcousticTap(ref counter, fishAup, sampled);
        }

        private int ResolveMockCount()
        {
            if (!MockBoids.IsCreated || MockBoids.Length <= 0)
                return math.min(MockFishCount, MockFish.Length);

            MockBoidArray array = MockBoids[0];
            return math.min(math.max(0, array.Count), math.min(MockFishCount, MockFish.Length));
        }

        private float ResolveLinkRate(uint floraHash, uint faunaHash, int linkCount)
        {
            for (int i = 0; i < linkCount; i++)
            {
                SymbiosisChemicalLinkDTO link = Links[i];
                if ((link.Flags & ShinobuFloraFaunaSymbiosisSolver.LinkFlagCompatible) == 0u)
                    continue;

                if (link.FloraHash == floraHash && link.FaunaHash == faunaHash && math.isfinite(link.ChemicalTransferRate))
                    return math.max(0f, link.ChemicalTransferRate);
            }

            return 0f;
        }

        private float ResolveCorruption01(in AbsoluteUniversePosition floraAup)
        {
            if (!AnomalyField.IsCreated || AnomalyField.Length <= 0)
                return 0f;

            SymbiosisAnomalyFieldMirror field = AnomalyField[0];
            if (!math.isfinite(field.Radius) || field.Radius <= 0f || !math.isfinite(field.CorruptionLevel))
                return 0f;

            AbsoluteUniversePosition anomalyAup = ShinobuFloraFaunaSymbiosisSolver.FromAbsoluteDouble3(field.EpicenterAUP);
            float3 delta = ShinobuFloraFaunaSymbiosisSolver.AupToLocal(in floraAup, in anomalyAup);
            float dist = math.sqrt(math.max(0f, math.lengthsq(delta)));
            return math.saturate((1f - (dist / math.max(1f, field.Radius))) * field.CorruptionLevel);
        }

        private float ResolveCorruption01(in SymbiosisAup48 floraAup)
        {
            if (!AnomalyField.IsCreated || AnomalyField.Length <= 0)
                return 0f;

            SymbiosisAnomalyFieldMirror field = AnomalyField[0];
            if (!math.isfinite(field.Radius) || field.Radius <= 0f || !math.isfinite(field.CorruptionLevel))
                return 0f;

            AbsoluteUniversePosition anomalyAup = ShinobuFloraFaunaSymbiosisSolver.FromAbsoluteDouble3(field.EpicenterAUP);
            float3 delta = ShinobuFloraFaunaSymbiosisSolver.AupToLocal(in floraAup, in anomalyAup);
            float dist = math.sqrt(math.max(0f, math.lengthsq(delta)));
            return math.saturate((1f - (dist / math.max(1f, field.Radius))) * field.CorruptionLevel);
        }

        private void WriteExchange(ref SymbiosisCounterDTO counter, uint floraHash, uint faunaHash, float transfer)
        {
            int index = counter.ActiveExchanges;
            if (index >= Exchanges.Length)
            {
                counter.OverflowCount++;
                return;
            }

            Exchanges[index] = new SymbiosisExchangeDTO
            {
                FloraHash = floraHash,
                FaunaHash = faunaHash,
                ChemicalTransferRate = transfer,
                _pad0 = 0f
            };
            counter.ActiveExchanges = index + 1;
            counter.BiomassTransferredMilli += (int)math.round(math.max(0f, transfer) * 1000f);
        }

        private void WriteScannerVfx(ref SymbiosisCounterDTO counter, SymbiosisFloraDTO flora, float3 delta, float toxin)
        {
            int index = counter.ToxemiaCount - 1;
            if (index < 0 || index >= ScannerVfx.Length)
            {
                counter.OverflowCount++;
                return;
            }

            ScannerVfx[index] = new ScannerVfxDTO
            {
                HitLocal = flora.LocalPosition,
                HitDistance = math.sqrt(math.max(0f, math.lengthsq(delta))),
                ScanProgress = math.saturate(toxin),
                TargetHash = flora.FloraHash,
                Flags = 1u,
                BeamScore = math.saturate(toxin)
            };
        }

        private void WriteOxygenEmitter(ref SymbiosisCounterDTO counter, SymbiosisFloraDTO flora, SymbiosisFloraAupDTO aup, SymbiosisTuningDTO tuning, float qualityCurve)
        {
            uint sectorHash = aup.SectorHash != 0u
                ? aup.SectorHash
                : ShinobuFloraFaunaSymbiosisSolver.ResolveSectorHash(ShinobuFloraFaunaSymbiosisSolver.ResolveSectorCoord(in aup.PositionAup, SectorSizeMeters));
            float oxygen = flora.Biomass * flora.OxygenRate * tuning.OxygenRateScale * math.lerp(0.25f, 1f, qualityCurve);
            for (int i = 0; i < counter.OxygenEmitterCount && i < OxygenEmitters.Length; i++)
            {
                SymbiosisOxygenEmitterDTO existing = OxygenEmitters[i];
                if (existing.SectorHash != sectorHash)
                    continue;

                existing.Oxygen01 = math.saturate(existing.Oxygen01 + oxygen);
                OxygenEmitters[i] = existing;
                return;
            }

            int index = counter.OxygenEmitterCount;
            if (index >= OxygenEmitters.Length)
            {
                counter.OverflowCount++;
                return;
            }

            OxygenEmitters[index] = new SymbiosisOxygenEmitterDTO
            {
                LocalPosition = flora.LocalPosition,
                Oxygen01 = math.saturate(oxygen),
                SectorHash = sectorHash,
                RadiusMeters = 24f + (flora.Biomass * 2f),
                FloraHash = flora.FloraHash,
                Flags = 1u
            };
            counter.OxygenEmitterCount = index + 1;
        }

        private void WriteAdherenceIfSubmarineInSporeZone(ref SymbiosisCounterDTO counter, SymbiosisFloraDTO flora, SymbiosisFloraAupDTO aup, SymbiosisTuningDTO tuning)
        {
            float3 delta = ShinobuFloraFaunaSymbiosisSolver.AupToLocal(in aup.PositionAup, in SubmarineAup);
            if (math.lengthsq(delta) > 24f * 24f)
                return;

            int index = counter.AdherenceCount;
            if (index >= Adherence.Length)
            {
                counter.OverflowCount++;
                return;
            }

            Adherence[index] = new AdherenceDTO
            {
                LocalPosition = flora.LocalPosition,
                Growth01 = math.saturate((SubmarineIdleSeconds - 60f) * tuning.ParasiteGrowthSpeed),
                HostHash = 0x5355424Du,
                FloraHash = flora.FloraHash,
                Flags = 1u,
                Frame = Frame
            };
            counter.AdherenceCount = index + 1;
        }

        private void WriteSeed(ref SymbiosisCounterDTO counter, SymbiosisFloraDTO flora, int floraIndex, uint carrierHash)
        {
            int index = counter.SeedCount;
            if (index >= Seeds.Length)
            {
                counter.OverflowCount++;
                return;
            }

            uint hash = ShinobuFloraFaunaSymbiosisSolver.MixHash((uint)floraIndex ^ carrierHash ^ Frame);
            float2 offset = new float2(((hash & 255u) - 128) * (1f / 64f), (((hash >> 8) & 255u) - 128) * (1f / 64f));
            Seeds[index] = new FloraSeedDTO
            {
                LocalPosition = flora.LocalPosition + new float3(offset.x, 0f, offset.y),
                Viability01 = 0.75f,
                FloraHash = flora.FloraHash,
                CarrierHash = carrierHash,
                Frame = Frame,
                Flags = 1u
            };
            counter.SeedCount = index + 1;
        }

        private void WriteAcousticTap(ref SymbiosisCounterDTO counter, AbsoluteUniversePosition fishAup, int sampled)
        {
            int index = counter.AcousticTapCount;
            if (index >= AcousticTaps.Length)
            {
                counter.OverflowCount++;
                return;
            }

            AcousticTaps[index] = new SymbiosisAcousticTapDTO
            {
                PositionAup = SymbiosisAup48.FromAup(in fishAup),
                Magnitude01 = math.saturate(sampled * (1f / 32f)),
                RadiusMeters = 18f + sampled,
                SourceHash = 0x53485250u,
                Flags = 1u
            };
            counter.AcousticTapCount = index + 1;
        }
    }
}
