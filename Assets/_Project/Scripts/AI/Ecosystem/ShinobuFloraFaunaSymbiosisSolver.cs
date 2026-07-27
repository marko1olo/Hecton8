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
        private static int s_x001ShinobuFloraFaunaSymbiosisSolverSignalPushDropCount;
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
#if UNITY_EDITOR
        private const int CsvMaxBytes = 8192;
#endif
        private const int LegacyScratchBytes = 512;
        private const int MaxSpatialHashChainSteps = 64;
        private const int MaxNeighborSamples = 48;
        private const float DefaultCellSizeMeters = 8f;
        private const float DefaultSectorSizeMeters = 64f;
        private const float DefaultSimulationTickDelta = 1f;
        private const float AuthoritativeQualityWeight = 1f;
        private const double AupCellSizeMetersDouble = HectonPhysicsContract.AupSectorSizeMetersDouble;
#if UNITY_EDITOR
        private const string CsvRelativePath = "symbiosis_links.csv";
        private const string CsvPrecomputedRelativePath = "Data/Precomputed/symbiosis_links.csv";
#endif
        private const string LegacyLinksFile = "symbiosis_chemical_links.h8bin";
        private const uint LegacyLinksMagicLittleEndian = 0x4C323653u; // S62L
        private const uint LegacyLinksMagicBigEndian = 0x42323653u; // S62B
        private const int LegacyLinksHeaderBytes = 16;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_13AI.bin";
        private const string DumpPayloadLabel = "shinobuSymbiosisTelemetryDumpPayload";
        private const ulong DumpMagic = 0x5348594D42493632UL; // SHYMBI62
        private const int DumpVersion = 1;
        private const int DumpHeaderBytes = 32;
        private const uint SourceHash = 0x53363253u; // S62S
        internal const uint FaunaHashHerbivore = 0x48455242u; // HERB
        internal const uint FaunaHashCarnivore = 0x4341524Eu; // CARN
        internal const uint FloraHashHealingKelp = 0x484B454Cu; // HKEL
        internal const uint FloraHashToxicAnemone = 0x54414E45u; // TANE
        internal const uint FloraHashOxygenKelp = 0x4F584B45u; // OXKE
        internal const uint FloraHashGlowMoss = 0x474D4F53u; // GMOS
        internal const uint FloraHashSporeCoral = 0x5350434Fu; // SPCO

        private static readonly ulong SymbiosisTelemetryMutationGuardMask =
            SymbiosisMutationGuardBit(BufferID.ShinobuSymbiosisTelemetryRing);

        private static ShinobuFloraFaunaSymbiosisSolver s_runtime;

        // Reusable arrays for pure logic integration to avoid GC allocation
        private static readonly float[] s_symbiosisPopulations = new float[2];
        private static readonly float[,] s_symbiosisInteraction = new float[2, 2];


        private VaultGenerationHandle<SymbiosisFloraDTO> _floraHandle;
        private VaultGenerationHandle<SymbiosisFloraAupDTO> _floraAupHandle;
        private VaultGenerationHandle<SymbiosisChemicalLinkDTO> _linkHandle;
        private VaultGenerationHandle<SymbiosisExchangeDTO> _exchangeHandle;
        private VaultGenerationHandle<SymbiosisTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<SymbiosisCounterDTO> _counterHandle;
        private VaultGenerationHandle<ScannerVfxDTO> _scannerVfxHandle;
        private VaultGenerationHandle<SymbiosisOxygenEmitterDTO> _oxygenEmitterHandle;
        private VaultGenerationHandle<AdherenceDTO> _adherenceHandle;
        private VaultGenerationHandle<FloraSeedDTO> _seedHandle;
        private VaultGenerationHandle<SymbiosisAcousticTapDTO> _acousticTapHandle;
        private VaultGenerationHandle<SymbiosisTuningDTO> _tuningHandle;
        private VaultGenerationHandle<int> _floraBucketHeadHandle;
        private VaultGenerationHandle<int> _floraBucketNextHandle;
        private VaultGenerationHandle<MockBoidArray> _mockBoidHandle;
        private VaultGenerationHandle<MockFishSymbiosisDTO> _mockFishHandle;
        private VaultGenerationHandle<AmbientEntityDTO> _ambientEntityHandle;
        private VaultGenerationHandle<AmbientEntityAupDTO> _ambientAupHandle;
        private VaultGenerationHandle<SymbiosisAnomalyFieldMirror> _anomalyFieldHandle;
        private bool _ownsAmbientEntityHandle;
        private bool _ownsAmbientAupHandle;
        private NativeArray<SymbiosisTelemetryEntry> _telemetryMirror;
        private NativeArray<SymbiosisAcousticTapDTO> _acousticTapPublishScratch;
        private NativeArray<AmbientEntityDTO> _ambientEntityJobSnapshot;
        private NativeArray<AmbientEntityAupDTO> _ambientAupJobSnapshot;
        private NativeArray<SymbiosisFloraDTO> _floraJobBuffer;
        private NativeArray<SymbiosisFloraAupDTO> _floraAupJobBuffer;
        private NativeArray<SymbiosisChemicalLinkDTO> _linkJobBuffer;
        private NativeArray<SymbiosisExchangeDTO> _exchangeJobBuffer;
        private NativeArray<SymbiosisCounterDTO> _counterJobBuffer;
        private NativeArray<ScannerVfxDTO> _scannerVfxJobBuffer;
        private NativeArray<SymbiosisOxygenEmitterDTO> _oxygenEmitterJobBuffer;
        private NativeArray<AdherenceDTO> _adherenceJobBuffer;
        private NativeArray<FloraSeedDTO> _seedJobBuffer;
        private NativeArray<SymbiosisAcousticTapDTO> _acousticTapJobBuffer;
        private NativeArray<int> _floraBucketHeadJobBuffer;
        private NativeArray<int> _floraBucketNextJobBuffer;
        private NativeArray<MockBoidArray> _mockBoidJobBuffer;
        private NativeArray<MockFishSymbiosisDTO> _mockFishJobBuffer;
        private NativeArray<SymbiosisAnomalyFieldMirror> _anomalyFieldJobSnapshot;

        private IDataVault _dataVault;
        private JobHandle _activeJobHandle;
        private AbsoluteUniversePosition _centerAup;
        private AbsoluteUniversePosition _lastSubmarineAup;
        private AbsoluteUniversePosition _submarineAup;
#if UNITY_EDITOR
        private long _csvTimestampTicks;
        private byte[] _symbiosisCsvManagedScratch;
        private byte[] _symbiosisLegacyManagedScratch;
        private SymbiosisChemicalLinkDTO[] _symbiosisLinkManagedScratch;
#endif
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
        private bool _dumpedFault;
        private bool _hasSubmarineAup;
        private bool _vaultStateReady;
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
            CompleteFrameJobForTeardown();
            TryUnregisterTicks();
            TryUnregisterHotSwapListener();
            ReleaseVaultStateForLifecycle();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            CompleteFrameJobForTeardown();
            RebindDataVaultForLifecycle(currentService is IDataVault currentVault ? currentVault : null);

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

            if (!_vaultStateReady || !AreVaultHandlesReady())
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            RefreshAupSignals();
            RefreshSubmarineIdleState();
#if UNITY_EDITOR
            MonitorCsvOverrides(vault);
#endif
            if (!TryRefreshAuthorityTuning(vault, out SymbiosisTuningDTO activeTuning))
                return;

            if (vault.IsCompactionFenceActive)
                return;

            if (!TrySnapshotJobBuffersFromVault(vault))
                return;

            JobHandle scheduledHandle = default;
            bool scheduledWork = false;
            try
            {
                if (!TryBindJobBuffers(
                    out NativeArray<SymbiosisFloraDTO> flora,
                    out NativeArray<SymbiosisFloraAupDTO> floraAups,
                    out NativeArray<SymbiosisChemicalLinkDTO> links,
                    out NativeArray<SymbiosisExchangeDTO> exchanges,
                    out NativeArray<SymbiosisCounterDTO> counters,
                    out NativeArray<ScannerVfxDTO> scannerVfx,
                    out NativeArray<SymbiosisOxygenEmitterDTO> oxygenEmitters,
                    out NativeArray<AdherenceDTO> adherence,
                    out NativeArray<FloraSeedDTO> seeds,
                    out NativeArray<SymbiosisAcousticTapDTO> acousticTaps,
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

                int floraCount = math.min(DefaultFloraCapacity, math.min(flora.Length, floraAups.Length));
                int mockFishCount = math.min(DefaultMockFishCapacity, mockFish.Length);
                int ambientCount = math.min(_ambientFishCapacity, math.min(ambientEntities.Length, ambientAups.Length));
                float quality = math.saturate(activeTuning.GlobalQualityWeight);
                uint frame = AdvanceSimulationFrame(counters);
                uint seed = ResolveFrameSectorSeed(in _centerAup, frame);
                activeTuning.ActiveFloraCount = math.max(activeTuning.ActiveFloraCount, floraCount);
                activeTuning.ActiveLinkCount = math.max(activeTuning.ActiveLinkCount, math.min(5, links.Length));
                float microExchangeWeight = ResolveMicroExchangeWeight(quality, activeTuning.MacroThreshold);
                bool runMicroExchangeFrame = ResolveDitheredFrameGate(seed ^ 0x4D455843u, microExchangeWeight);

                JobHandle handle = default;
                if (counters.Length > 0 && counters[0].Initialized == 0)
                {
                    GenerateEmergencyMockSymbiosisJob hydrateJob = default;
                    hydrateJob.Flora = flora;
                    hydrateJob.FloraAups = floraAups;
                    hydrateJob.Links = links;
                    hydrateJob.Counters = counters;
                    hydrateJob.MockBoids = mockBoids;
                    hydrateJob.MockFish = mockFish;
                    hydrateJob.CenterAup = _centerAup;
                    hydrateJob.GlobalQualityWeight = quality;
                    hydrateJob.FloraCount = floraCount;
                    hydrateJob.MockFishCount = mockFishCount;
                    hydrateJob.LinkCount = math.min(LinkCapacity, links.Length);
                    hydrateJob.Seed = seed;
                    handle = hydrateJob.Schedule(handle);
                    scheduledHandle = handle;
                    scheduledWork = true;
                    _runtimeFlags |= TuningFlagEmergencyMock;
                }

                if (runMicroExchangeFrame)
                {
                    BuildSymbiosisFloraSpatialHashJob hashJob = default;
                    hashJob.Flora = flora;
                    hashJob.FloraAups = floraAups;
                    hashJob.BucketHeads = floraBucketHeads;
                    hashJob.BucketNext = floraBucketNext;
                    hashJob.CenterAup = _centerAup;
                    hashJob.CellSizeMeters = DefaultCellSizeMeters;
                    hashJob.Count = floraCount;
                    handle = hashJob.Schedule(handle);
                    scheduledHandle = handle;
                    scheduledWork = true;
                }

                SymbiosisExchangeKernelJob solveJob = default;
                solveJob.Flora = flora;
                solveJob.FloraAups = floraAups;
                solveJob.Links = links;
                solveJob.Exchanges = exchanges;
                solveJob.Counters = counters;
                solveJob.ScannerVfx = scannerVfx;
                solveJob.OxygenEmitters = oxygenEmitters;
                solveJob.Adherence = adherence;
                solveJob.Seeds = seeds;
                solveJob.AcousticTaps = acousticTaps;
                solveJob.Tuning = activeTuning;
                solveJob.BucketHeads = floraBucketHeads;
                solveJob.BucketNext = floraBucketNext;
                solveJob.MockBoids = mockBoids;
                solveJob.MockFish = mockFish;
                solveJob.AmbientEntities = ambientEntities;
                solveJob.AmbientAups = ambientAups;
                solveJob.AnomalyField = anomalyField;
                solveJob.CenterAup = _centerAup;
                solveJob.SubmarineAup = _submarineAup;
                solveJob.SubmarineIdleSeconds = _submarineIdleSeconds;
                solveJob.Frame = frame;
                solveJob.CellSizeMeters = DefaultCellSizeMeters;
                solveJob.SectorSizeMeters = DefaultSectorSizeMeters;
                solveJob.SimulationTickDelta = DefaultSimulationTickDelta;
                solveJob.FloraCount = floraCount;
                solveJob.AmbientFishCount = ambientCount;
                solveJob.MockFishCount = mockFishCount;
                solveJob.MaxNeighborSamplesBase = MaxNeighborSamples;
                solveJob.MaxSpatialHashChainSteps = MaxSpatialHashChainSteps;
                solveJob.MicroExchangeThisFrame = math.select(0, 1, runMicroExchangeFrame);
                handle = solveJob.Schedule(handle);
                scheduledHandle = handle;
                scheduledWork = true;

                _activeJobHandle = handle;
                _scheduleTicks = Stopwatch.GetTimestamp();
                H8Memory.RegisterActiveJob(SystemID.AIEcology, _activeJobHandle);
                _jobScheduled = true;
            }
            catch (InvalidOperationException)
            {
                if (scheduledWork)
                {
                    _activeJobHandle = scheduledHandle;
                    H8Memory.RegisterActiveJob(SystemID.AIEcology, _activeJobHandle);
                    _jobScheduled = true;
                }

                GlobalTelemetryBus.PublishPerformanceWarning(0x53364A53u, SourceHash, 0f);
            }
            catch (ArgumentException)
            {
                if (scheduledWork)
                {
                    _activeJobHandle = scheduledHandle;
                    H8Memory.RegisterActiveJob(SystemID.AIEcology, _activeJobHandle);
                    _jobScheduled = true;
                }

                GlobalTelemetryBus.PublishPerformanceWarning(0x53364A53u, SourceHash, 0f);
            }
        }

        public void LateFrameTick()
        {
            TryFinalizeFrameJobNoWait();
        }

        private static bool IsOwnedVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)SystemID.AIEcology;
        }

        private static bool IsVaultHandleForBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.Generation != 0u;
        }

        private static VaultGenerationHandle<T> ClaimGenerationHandle<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null)
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

        private static VaultGenerationHandle<T> BorrowGenerationHandle<T>(
            IDataVault vault,
            BufferID bufferId) where T : struct
        {
            if (vault == null || !vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle))
                return default;

            return IsVaultHandleForBuffer(in handle, bufferId) ? handle : default;
        }

        private static bool TryResolveOwnedVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   IsOwnedVaultHandle(in handle, expectedBufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool TryResolveVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   IsVaultHandleForBuffer(in handle, expectedBufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool TryAcquireSymbiosisMutationGuard(IDataVault vault, BufferID bufferId)
        {
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryAcquireMutationGuard(SymbiosisMutationGuardBit(bufferId));
        }

        private static void ReleaseSymbiosisMutationGuard(IDataVault vault, BufferID bufferId)
        {
            vault?.ReleaseMutationGuard(SymbiosisMutationGuardBit(bufferId));
        }

        private static ulong SymbiosisMutationGuardBit(BufferID bufferId)
        {
            int bitIndex = unchecked((int)((uint)(int)bufferId & 31u));
            return 1UL << bitIndex;
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
            IDataVault currentVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, currentVault))
                RebindDataVaultForLifecycle(currentVault);

            if (_vaultStateReady && _dataVault != null && AreVaultHandlesReady())
                return true;

            IDataVault vault = AcquireDataVaultCold();
            if (vault == null)
                return false;

            _floraHandle = ClaimGenerationHandle<SymbiosisFloraDTO>(
                vault,
                BufferID.ShinobuSymbiosisFlora,
                DefaultFloraCapacity,
                NativeArrayOptions.ClearMemory);
            _floraAupHandle = ClaimGenerationHandle<SymbiosisFloraAupDTO>(
                vault,
                BufferID.ShinobuSymbiosisFloraAups,
                DefaultFloraCapacity,
                NativeArrayOptions.ClearMemory);
            _linkHandle = ClaimGenerationHandle<SymbiosisChemicalLinkDTO>(
                vault,
                BufferID.ShinobuSymbiosisLinks,
                LinkCapacity,
                NativeArrayOptions.ClearMemory);
            _exchangeHandle = ClaimGenerationHandle<SymbiosisExchangeDTO>(
                vault,
                BufferID.ShinobuSymbiosisExchanges,
                ExchangeCapacity,
                NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = ClaimGenerationHandle<SymbiosisTelemetryEntry>(
                vault,
                BufferID.ShinobuSymbiosisTelemetryRing,
                TelemetryCapacity,
                NativeArrayOptions.ClearMemory);
            _counterHandle = ClaimGenerationHandle<SymbiosisCounterDTO>(
                vault,
                BufferID.ShinobuSymbiosisCounters,
                1,
                NativeArrayOptions.ClearMemory);
            _scannerVfxHandle = ClaimGenerationHandle<ScannerVfxDTO>(
                vault,
                BufferID.ShinobuSymbiosisScannerVfx,
                ScannerVfxCapacity,
                NativeArrayOptions.UninitializedMemory);
            _oxygenEmitterHandle = ClaimGenerationHandle<SymbiosisOxygenEmitterDTO>(
                vault,
                BufferID.ShinobuSymbiosisOxygenEmitters,
                OxygenEmitterCapacity,
                NativeArrayOptions.UninitializedMemory);
            _adherenceHandle = ClaimGenerationHandle<AdherenceDTO>(
                vault,
                BufferID.ShinobuSymbiosisAdherence,
                AdherenceCapacity,
                NativeArrayOptions.UninitializedMemory);
            _seedHandle = ClaimGenerationHandle<FloraSeedDTO>(
                vault,
                BufferID.ShinobuSymbiosisSeeds,
                SeedCapacity,
                NativeArrayOptions.UninitializedMemory);
            _acousticTapHandle = ClaimGenerationHandle<SymbiosisAcousticTapDTO>(
                vault,
                BufferID.ShinobuSymbiosisAcousticTaps,
                AcousticTapCapacity,
                NativeArrayOptions.UninitializedMemory);
            _tuningHandle = ClaimGenerationHandle<SymbiosisTuningDTO>(
                vault,
                BufferID.ShinobuSymbiosisTuning,
                1,
                NativeArrayOptions.ClearMemory);
            _floraBucketHeadHandle = ClaimGenerationHandle<int>(
                vault,
                BufferID.ShinobuSymbiosisFloraHashBucketHeads,
                SpatialBucketCapacity,
                NativeArrayOptions.UninitializedMemory);
            _floraBucketNextHandle = ClaimGenerationHandle<int>(
                vault,
                BufferID.ShinobuSymbiosisFloraHashNext,
                DefaultFloraCapacity,
                NativeArrayOptions.UninitializedMemory);
            _mockBoidHandle = ClaimGenerationHandle<MockBoidArray>(
                vault,
                BufferID.ShinobuSymbiosisMockBoids,
                1,
                NativeArrayOptions.ClearMemory);
            _mockFishHandle = ClaimGenerationHandle<MockFishSymbiosisDTO>(
                vault,
                BufferID.ShinobuSymbiosisMockFish,
                DefaultMockFishCapacity,
                NativeArrayOptions.ClearMemory);

            _ambientEntityHandle = BorrowGenerationHandle<AmbientEntityDTO>(vault, BufferID.ShinobuAmbientEntities);
            _ownsAmbientEntityHandle = false;
            if (!IsVaultHandleForBuffer(in _ambientEntityHandle, BufferID.ShinobuAmbientEntities))
            {
                _ambientEntityHandle = ClaimGenerationHandle<AmbientEntityDTO>(
                    vault,
                    BufferID.ShinobuAmbientEntities,
                    _ambientFishCapacity,
                    NativeArrayOptions.ClearMemory);
                _ownsAmbientEntityHandle = IsOwnedVaultHandle(in _ambientEntityHandle, BufferID.ShinobuAmbientEntities);
            }

            _ambientAupHandle = BorrowGenerationHandle<AmbientEntityAupDTO>(vault, BufferID.ShinobuAmbientAups);
            _ownsAmbientAupHandle = false;
            if (!IsVaultHandleForBuffer(in _ambientAupHandle, BufferID.ShinobuAmbientAups))
            {
                _ambientAupHandle = ClaimGenerationHandle<AmbientEntityAupDTO>(
                    vault,
                    BufferID.ShinobuAmbientAups,
                    _ambientFishCapacity,
                    NativeArrayOptions.ClearMemory);
                _ownsAmbientAupHandle = IsOwnedVaultHandle(in _ambientAupHandle, BufferID.ShinobuAmbientAups);
            }

            _anomalyFieldHandle = BorrowGenerationHandle<SymbiosisAnomalyFieldMirror>(vault, BufferID.ShinobuSeedShipAnomalyField);

            bool ready = AreVaultHandlesReady();
            if (ready)
                ready = EnsureLocalBuffersCold();
            if (!ready)
                return false;

            // Authoring-time only, and guarded at the call site rather than opened up: the legacy
            // loader reads <repositoryRoot>/Docs/Archive/, a path that cannot exist in a shipped
            // player. A player build has nothing to migrate, so skipping it is the correct behaviour
            // and not a lost feature. The links it would import are already baked into the vault.
#if UNITY_EDITOR
            TryLoadLegacyLinksIntoVault(vault);
#endif
            _vaultStateReady = true;
            return true;
        }

        private IDataVault AcquireDataVaultCold()
        {
            IDataVault currentVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, currentVault))
                RebindDataVaultForLifecycle(currentVault);

            return _dataVault;
        }

        private bool EnsureLocalBuffersCold()
        {
            try
            {
                if (_telemetryMirror.IsCreated &&
                    _telemetryMirror.Length == TelemetryCapacity &&
                    _acousticTapPublishScratch.IsCreated &&
                    _acousticTapPublishScratch.Length == AcousticTapCapacity &&
                    _ambientEntityJobSnapshot.IsCreated &&
                    _ambientEntityJobSnapshot.Length == _ambientFishCapacity &&
                    _ambientAupJobSnapshot.IsCreated &&
                    _ambientAupJobSnapshot.Length == _ambientFishCapacity &&
                    _floraJobBuffer.IsCreated &&
                    _floraJobBuffer.Length == DefaultFloraCapacity &&
                    _floraAupJobBuffer.IsCreated &&
                    _floraAupJobBuffer.Length == DefaultFloraCapacity &&
                    _linkJobBuffer.IsCreated &&
                    _linkJobBuffer.Length == LinkCapacity &&
                    _exchangeJobBuffer.IsCreated &&
                    _exchangeJobBuffer.Length == ExchangeCapacity &&
                    _counterJobBuffer.IsCreated &&
                    _counterJobBuffer.Length == 1 &&
                    _scannerVfxJobBuffer.IsCreated &&
                    _scannerVfxJobBuffer.Length == ScannerVfxCapacity &&
                    _oxygenEmitterJobBuffer.IsCreated &&
                    _oxygenEmitterJobBuffer.Length == OxygenEmitterCapacity &&
                    _adherenceJobBuffer.IsCreated &&
                    _adherenceJobBuffer.Length == AdherenceCapacity &&
                    _seedJobBuffer.IsCreated &&
                    _seedJobBuffer.Length == SeedCapacity &&
                    _acousticTapJobBuffer.IsCreated &&
                    _acousticTapJobBuffer.Length == AcousticTapCapacity &&
                    _floraBucketHeadJobBuffer.IsCreated &&
                    _floraBucketHeadJobBuffer.Length == SpatialBucketCapacity &&
                    _floraBucketNextJobBuffer.IsCreated &&
                    _floraBucketNextJobBuffer.Length == DefaultFloraCapacity &&
                    _mockBoidJobBuffer.IsCreated &&
                    _mockBoidJobBuffer.Length == 1 &&
                    _mockFishJobBuffer.IsCreated &&
                    _mockFishJobBuffer.Length == DefaultMockFishCapacity &&
                    _anomalyFieldJobSnapshot.IsCreated &&
                    _anomalyFieldJobSnapshot.Length == 1)
                {
                    return true;
                }

                DisposeLocalBuffersCold();
                EnsureNativeJobArray(ref _telemetryMirror, TelemetryCapacity, nameof(_telemetryMirror));
                EnsureNativeJobArray(ref _acousticTapPublishScratch, AcousticTapCapacity, nameof(_acousticTapPublishScratch));
                EnsureNativeJobArray(ref _ambientEntityJobSnapshot, _ambientFishCapacity, nameof(_ambientEntityJobSnapshot));
                EnsureNativeJobArray(ref _ambientAupJobSnapshot, _ambientFishCapacity, nameof(_ambientAupJobSnapshot));
                EnsureNativeJobArray(ref _floraJobBuffer, DefaultFloraCapacity, nameof(_floraJobBuffer));
                EnsureNativeJobArray(ref _floraAupJobBuffer, DefaultFloraCapacity, nameof(_floraAupJobBuffer));
                EnsureNativeJobArray(ref _linkJobBuffer, LinkCapacity, nameof(_linkJobBuffer));
                EnsureNativeJobArray(ref _exchangeJobBuffer, ExchangeCapacity, nameof(_exchangeJobBuffer));
                EnsureNativeJobArray(ref _counterJobBuffer, 1, nameof(_counterJobBuffer));
                EnsureNativeJobArray(ref _scannerVfxJobBuffer, ScannerVfxCapacity, nameof(_scannerVfxJobBuffer));
                EnsureNativeJobArray(ref _oxygenEmitterJobBuffer, OxygenEmitterCapacity, nameof(_oxygenEmitterJobBuffer));
                EnsureNativeJobArray(ref _adherenceJobBuffer, AdherenceCapacity, nameof(_adherenceJobBuffer));
                EnsureNativeJobArray(ref _seedJobBuffer, SeedCapacity, nameof(_seedJobBuffer));
                EnsureNativeJobArray(ref _acousticTapJobBuffer, AcousticTapCapacity, nameof(_acousticTapJobBuffer));
                EnsureNativeJobArray(ref _floraBucketHeadJobBuffer, SpatialBucketCapacity, nameof(_floraBucketHeadJobBuffer));
                EnsureNativeJobArray(ref _floraBucketNextJobBuffer, DefaultFloraCapacity, nameof(_floraBucketNextJobBuffer));
                EnsureNativeJobArray(ref _mockBoidJobBuffer, 1, nameof(_mockBoidJobBuffer));
                EnsureNativeJobArray(ref _mockFishJobBuffer, DefaultMockFishCapacity, nameof(_mockFishJobBuffer));
                EnsureNativeJobArray(ref _anomalyFieldJobSnapshot, 1, nameof(_anomalyFieldJobSnapshot));
                return true;
            }
            catch (ArgumentException)
            {
                DisposeLocalBuffersCold();
                GlobalTelemetryBus.PublishPerformanceWarning(0x53364D41u, SourceHash, 0f);
                return false;
            }
            catch (InvalidOperationException)
            {
                DisposeLocalBuffersCold();
                GlobalTelemetryBus.PublishPerformanceWarning(0x53364D49u, SourceHash, 0f);
                return false;
            }
            catch (OutOfMemoryException)
            {
                DisposeLocalBuffersCold();
                GlobalTelemetryBus.PublishPerformanceWarning(0x53364D4Fu, SourceHash, 0f);
                return false;
            }
        }

        private static void EnsureNativeJobArray<T>(ref NativeArray<T> array, int length, string label)
            where T : struct
        {
            if (array.IsCreated && array.Length == length)
                return;

            DisposeNativeJobArray(ref array);
            array = H8Memory.Allocate<T>(length, SystemID.AIEcology, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            if (!array.IsCreated)
                throw new InvalidOperationException($"{nameof(ShinobuFloraFaunaSymbiosisSolver)} native allocation failed for {label}.");
        }

        private void DisposeLocalBuffersCold()
        {
            DisposeNativeJobArray(ref _anomalyFieldJobSnapshot);
            DisposeNativeJobArray(ref _mockFishJobBuffer);
            DisposeNativeJobArray(ref _mockBoidJobBuffer);
            DisposeNativeJobArray(ref _floraBucketNextJobBuffer);
            DisposeNativeJobArray(ref _floraBucketHeadJobBuffer);
            DisposeNativeJobArray(ref _acousticTapJobBuffer);
            DisposeNativeJobArray(ref _seedJobBuffer);
            DisposeNativeJobArray(ref _adherenceJobBuffer);
            DisposeNativeJobArray(ref _oxygenEmitterJobBuffer);
            DisposeNativeJobArray(ref _scannerVfxJobBuffer);
            DisposeNativeJobArray(ref _counterJobBuffer);
            DisposeNativeJobArray(ref _exchangeJobBuffer);
            DisposeNativeJobArray(ref _linkJobBuffer);
            DisposeNativeJobArray(ref _floraAupJobBuffer);
            DisposeNativeJobArray(ref _floraJobBuffer);
            DisposeNativeJobArray(ref _ambientAupJobSnapshot);
            DisposeNativeJobArray(ref _ambientEntityJobSnapshot);
            DisposeNativeJobArray(ref _acousticTapPublishScratch);
            DisposeNativeJobArray(ref _telemetryMirror);
        }

        private static void DisposeNativeJobArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            H8Memory.Release(ref array, SystemID.AIEcology);
        }

        private bool AreVaultHandlesReady()
        {
            return IsOwnedVaultHandle(in _floraHandle, BufferID.ShinobuSymbiosisFlora) &&
                   IsOwnedVaultHandle(in _floraAupHandle, BufferID.ShinobuSymbiosisFloraAups) &&
                   IsOwnedVaultHandle(in _linkHandle, BufferID.ShinobuSymbiosisLinks) &&
                   IsOwnedVaultHandle(in _exchangeHandle, BufferID.ShinobuSymbiosisExchanges) &&
                   IsOwnedVaultHandle(in _telemetryHandle, BufferID.ShinobuSymbiosisTelemetryRing) &&
                   IsOwnedVaultHandle(in _counterHandle, BufferID.ShinobuSymbiosisCounters) &&
                   IsOwnedVaultHandle(in _scannerVfxHandle, BufferID.ShinobuSymbiosisScannerVfx) &&
                   IsOwnedVaultHandle(in _oxygenEmitterHandle, BufferID.ShinobuSymbiosisOxygenEmitters) &&
                   IsOwnedVaultHandle(in _adherenceHandle, BufferID.ShinobuSymbiosisAdherence) &&
                   IsOwnedVaultHandle(in _seedHandle, BufferID.ShinobuSymbiosisSeeds) &&
                   IsOwnedVaultHandle(in _acousticTapHandle, BufferID.ShinobuSymbiosisAcousticTaps) &&
                   IsOwnedVaultHandle(in _tuningHandle, BufferID.ShinobuSymbiosisTuning) &&
                   IsOwnedVaultHandle(in _floraBucketHeadHandle, BufferID.ShinobuSymbiosisFloraHashBucketHeads) &&
                   IsOwnedVaultHandle(in _floraBucketNextHandle, BufferID.ShinobuSymbiosisFloraHashNext) &&
                   IsOwnedVaultHandle(in _mockBoidHandle, BufferID.ShinobuSymbiosisMockBoids) &&
                   IsOwnedVaultHandle(in _mockFishHandle, BufferID.ShinobuSymbiosisMockFish) &&
                   IsVaultHandleForBuffer(in _ambientEntityHandle, BufferID.ShinobuAmbientEntities) &&
                   IsVaultHandleForBuffer(in _ambientAupHandle, BufferID.ShinobuAmbientAups);
        }

        private bool TryBindJobBuffers(
            out NativeArray<SymbiosisFloraDTO> flora,
            out NativeArray<SymbiosisFloraAupDTO> floraAups,
            out NativeArray<SymbiosisChemicalLinkDTO> links,
            out NativeArray<SymbiosisExchangeDTO> exchanges,
            out NativeArray<SymbiosisCounterDTO> counters,
            out NativeArray<ScannerVfxDTO> scannerVfx,
            out NativeArray<SymbiosisOxygenEmitterDTO> oxygenEmitters,
            out NativeArray<AdherenceDTO> adherence,
            out NativeArray<FloraSeedDTO> seeds,
            out NativeArray<SymbiosisAcousticTapDTO> acousticTaps,
            out NativeArray<int> floraBucketHeads,
            out NativeArray<int> floraBucketNext,
            out NativeArray<MockBoidArray> mockBoids,
            out NativeArray<MockFishSymbiosisDTO> mockFish,
            out NativeArray<AmbientEntityDTO> ambientEntities,
            out NativeArray<AmbientEntityAupDTO> ambientAups,
            out NativeArray<SymbiosisAnomalyFieldMirror> anomalyField)
        {
            flora = default;
            floraAups = default;
            links = default;
            exchanges = default;
            counters = default;
            scannerVfx = default;
            oxygenEmitters = default;
            adherence = default;
            seeds = default;
            acousticTaps = default;
            floraBucketHeads = default;
            floraBucketNext = default;
            mockBoids = default;
            mockFish = default;
            ambientEntities = default;
            ambientAups = default;
            anomalyField = default;

            bool ready =
                _floraJobBuffer.IsCreated &&
                _floraAupJobBuffer.IsCreated &&
                _linkJobBuffer.IsCreated &&
                _exchangeJobBuffer.IsCreated &&
                _counterJobBuffer.IsCreated &&
                _scannerVfxJobBuffer.IsCreated &&
                _oxygenEmitterJobBuffer.IsCreated &&
                _adherenceJobBuffer.IsCreated &&
                _seedJobBuffer.IsCreated &&
                _acousticTapJobBuffer.IsCreated &&
                _floraBucketHeadJobBuffer.IsCreated &&
                _floraBucketNextJobBuffer.IsCreated &&
                _mockBoidJobBuffer.IsCreated &&
                _mockFishJobBuffer.IsCreated &&
                _ambientEntityJobSnapshot.IsCreated &&
                _ambientAupJobSnapshot.IsCreated &&
                _anomalyFieldJobSnapshot.IsCreated;

            if (ready)
            {
                flora = _floraJobBuffer;
                floraAups = _floraAupJobBuffer;
                links = _linkJobBuffer;
                exchanges = _exchangeJobBuffer;
                counters = _counterJobBuffer;
                scannerVfx = _scannerVfxJobBuffer;
                oxygenEmitters = _oxygenEmitterJobBuffer;
                adherence = _adherenceJobBuffer;
                seeds = _seedJobBuffer;
                acousticTaps = _acousticTapJobBuffer;
                floraBucketHeads = _floraBucketHeadJobBuffer;
                floraBucketNext = _floraBucketNextJobBuffer;
                mockBoids = _mockBoidJobBuffer;
                mockFish = _mockFishJobBuffer;
                ambientEntities = _ambientEntityJobSnapshot;
                ambientAups = _ambientAupJobSnapshot;
                anomalyField = _anomalyFieldJobSnapshot;
            }

            return ready;
        }

        private bool TrySnapshotJobBuffersFromVault(IDataVault vault)
        {
            if (vault == null)
                return false;

            return ClearJobOutputBuffers() &&
                   TryCopyVaultBufferToSnapshot(
                       vault,
                       in _floraHandle,
                       BufferID.ShinobuSymbiosisFlora,
                       _floraJobBuffer) &&
                   TryCopyVaultBufferToSnapshot(
                       vault,
                       in _floraAupHandle,
                       BufferID.ShinobuSymbiosisFloraAups,
                       _floraAupJobBuffer) &&
                   TryCopyVaultBufferToSnapshot(
                       vault,
                       in _linkHandle,
                       BufferID.ShinobuSymbiosisLinks,
                       _linkJobBuffer) &&
                   TryCopyVaultBufferToSnapshot(
                       vault,
                       in _counterHandle,
                       BufferID.ShinobuSymbiosisCounters,
                       _counterJobBuffer) &&
                   TryCopyVaultBufferToSnapshot(
                       vault,
                       in _floraBucketHeadHandle,
                       BufferID.ShinobuSymbiosisFloraHashBucketHeads,
                       _floraBucketHeadJobBuffer) &&
                   TryCopyVaultBufferToSnapshot(
                       vault,
                       in _floraBucketNextHandle,
                       BufferID.ShinobuSymbiosisFloraHashNext,
                       _floraBucketNextJobBuffer) &&
                   TryCopyVaultBufferToSnapshot(
                       vault,
                       in _mockBoidHandle,
                       BufferID.ShinobuSymbiosisMockBoids,
                       _mockBoidJobBuffer) &&
                   TryCopyVaultBufferToSnapshot(
                       vault,
                       in _mockFishHandle,
                       BufferID.ShinobuSymbiosisMockFish,
                       _mockFishJobBuffer) &&
                   TryCopyVaultBufferToSnapshot(
                        vault,
                        in _ambientEntityHandle,
                        BufferID.ShinobuAmbientEntities,
                        _ambientEntityJobSnapshot) &&
                   TryCopyVaultBufferToSnapshot(
                       vault,
                        in _ambientAupHandle,
                        BufferID.ShinobuAmbientAups,
                        _ambientAupJobSnapshot) &&
                   TrySnapshotAnomalyField(vault);
        }

        private bool ClearJobOutputBuffers()
        {
            return ClearNativeJobArray(_exchangeJobBuffer) &&
                   ClearNativeJobArray(_scannerVfxJobBuffer) &&
                   ClearNativeJobArray(_oxygenEmitterJobBuffer) &&
                   ClearNativeJobArray(_adherenceJobBuffer) &&
                   ClearNativeJobArray(_seedJobBuffer) &&
                   ClearNativeJobArray(_acousticTapJobBuffer);
        }

        private static unsafe bool ClearNativeJobArray<T>(NativeArray<T> array)
            where T : unmanaged
        {
            if (!array.IsCreated)
                return false;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            UnsafeUtility.MemClear(ptr, (long)UnsafeUtility.SizeOf<T>() * array.Length);

            return true;
        }

        private bool TrySnapshotAnomalyField(IDataVault vault)
        {
            if (!_anomalyFieldJobSnapshot.IsCreated || _anomalyFieldJobSnapshot.Length <= 0)
                return false;

            _anomalyFieldJobSnapshot[0] = default;
            if (!IsVaultHandleForBuffer(in _anomalyFieldHandle, BufferID.ShinobuSeedShipAnomalyField))
                return true;

            return TryCopyVaultBufferToSnapshot(
                vault,
                in _anomalyFieldHandle,
                BufferID.ShinobuSeedShipAnomalyField,
                _anomalyFieldJobSnapshot);
        }

        private bool TryPublishJobBuffersToVault(IDataVault vault)
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            bool publishedData =
                TryPublishSnapshotToVault(vault, in _floraHandle, BufferID.ShinobuSymbiosisFlora, _floraJobBuffer) &&
                TryPublishSnapshotToVault(vault, in _floraAupHandle, BufferID.ShinobuSymbiosisFloraAups, _floraAupJobBuffer) &&
                TryPublishSnapshotToVault(vault, in _linkHandle, BufferID.ShinobuSymbiosisLinks, _linkJobBuffer) &&
                TryPublishSnapshotToVault(vault, in _exchangeHandle, BufferID.ShinobuSymbiosisExchanges, _exchangeJobBuffer) &&
                TryPublishSnapshotToVault(vault, in _scannerVfxHandle, BufferID.ShinobuSymbiosisScannerVfx, _scannerVfxJobBuffer) &&
                TryPublishSnapshotToVault(vault, in _oxygenEmitterHandle, BufferID.ShinobuSymbiosisOxygenEmitters, _oxygenEmitterJobBuffer) &&
                TryPublishSnapshotToVault(vault, in _adherenceHandle, BufferID.ShinobuSymbiosisAdherence, _adherenceJobBuffer) &&
                TryPublishSnapshotToVault(vault, in _seedHandle, BufferID.ShinobuSymbiosisSeeds, _seedJobBuffer) &&
                TryPublishSnapshotToVault(vault, in _acousticTapHandle, BufferID.ShinobuSymbiosisAcousticTaps, _acousticTapJobBuffer) &&
                TryPublishSnapshotToVault(vault, in _floraBucketHeadHandle, BufferID.ShinobuSymbiosisFloraHashBucketHeads, _floraBucketHeadJobBuffer) &&
                TryPublishSnapshotToVault(vault, in _floraBucketNextHandle, BufferID.ShinobuSymbiosisFloraHashNext, _floraBucketNextJobBuffer) &&
                TryPublishSnapshotToVault(vault, in _mockBoidHandle, BufferID.ShinobuSymbiosisMockBoids, _mockBoidJobBuffer) &&
                TryPublishSnapshotToVault(vault, in _mockFishHandle, BufferID.ShinobuSymbiosisMockFish, _mockFishJobBuffer);

            return publishedData &&
                   TryPublishSnapshotToVault(vault, in _counterHandle, BufferID.ShinobuSymbiosisCounters, _counterJobBuffer);
        }

        private static unsafe bool TryPublishSnapshotToVault<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            NativeArray<T> snapshot) where T : unmanaged
        {
            if (vault == null || !snapshot.IsCreated || vault.IsCompactionFenceActive)
                return false;

            if (!TryAcquireSymbiosisMutationGuard(vault, bufferId))
                return false;

            try
            {
                if (!TryResolveOwnedVaultBuffer(vault, in handle, bufferId, out NativeArray<T> target))
                    return false;

                int safeCount = math.min(snapshot.Length, target.Length);
                int stride = UnsafeUtility.SizeOf<T>();
                byte* targetPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(target);
                if (safeCount > 0)
                {
                    void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(snapshot);
                    UnsafeUtility.MemCpy(targetPtr, sourcePtr, (long)stride * safeCount);
                }

                int tailCount = target.Length - safeCount;
                if (tailCount > 0)
                    UnsafeUtility.MemClear(targetPtr + ((long)stride * safeCount), (long)stride * tailCount);

                return true;
            }
            finally
            {
                ReleaseSymbiosisMutationGuard(vault, bufferId);
            }
        }

        private static unsafe bool TryCopyVaultBufferToSnapshot<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            NativeArray<T> snapshot) where T : unmanaged
        {
            if (vault == null || !snapshot.IsCreated || vault.IsCompactionFenceActive)
                return false;

            if (!TryAcquireSymbiosisMutationGuard(vault, bufferId))
                return false;

            try
            {
                if (!TryResolveVaultBuffer(vault, in handle, bufferId, out NativeArray<T> source))
                    return false;

                int safeCount = math.min(source.Length, snapshot.Length);
                int stride = UnsafeUtility.SizeOf<T>();
                byte* snapshotPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(snapshot);
                if (safeCount > 0)
                {
                    void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                    UnsafeUtility.MemCpy(snapshotPtr, sourcePtr, (long)stride * safeCount);
                }

                int tailCount = snapshot.Length - safeCount;
                if (tailCount > 0)
                    UnsafeUtility.MemClear(snapshotPtr + ((long)stride * safeCount), (long)stride * tailCount);

                return true;
            }
            finally
            {
                ReleaseSymbiosisMutationGuard(vault, bufferId);
            }
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

        private bool TryRefreshAuthorityTuning(IDataVault vault, out SymbiosisTuningDTO dto)
        {
            dto = SymbiosisTuningDTO.Default();
            float quality = ResolveSymbiosisQualityWeight();
            SymbiosisTuningDTO raw = default;
            if (vault == null ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (!TryResolveOwnedVaultBuffer(vault, in _tuningHandle, BufferID.ShinobuSymbiosisTuning, out NativeArray<SymbiosisTuningDTO> tuning) ||
                !tuning.IsCreated ||
                tuning.Length <= 0)
            {
                return false;
            }

            raw = tuning[0];
            dto = SymbiosisTuningDTO.Sanitize(raw);
            dto.GlobalQualityWeight = quality;
            dto.SimulationTickDelta = DefaultSimulationTickDelta;
            if (dto.ActiveFloraCount <= 0)
                dto.ActiveFloraCount = DefaultFloraCapacity;
            if (dto.ActiveLinkCount <= 0)
                dto.ActiveLinkCount = math.min(5, LinkCapacity);
            return TryWriteSymbiosisTuning(vault, dto);
        }

        private static float ResolveSymbiosisQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AuthoritativeQualityWeight);

            return MathLodApproximation.SaturateFinite(HomeostasisBrain.GlobalQualityWeight, AuthoritativeQualityWeight);
        }

#if UNITY_EDITOR
        private void MonitorCsvOverrides(IDataVault vault)
        {
            try
            {
                string path = BuildCsvPath();
                if (path == null || path.Length == 0 || !File.Exists(path))
                    return;

                DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
                if (lastWriteUtc.Ticks == _csvTimestampTicks)
                    return;

                byte[] scratch = EnsureEditorByteScratch(ref _symbiosisCsvManagedScratch, CsvMaxBytes);
                int bytesRead = LoadFileIntoManagedScratch(path, scratch, CsvMaxBytes, FileShare.ReadWrite);
                if (bytesRead <= 0)
                    return;

                if (!TryReadSymbiosisTuning(vault, out SymbiosisTuningDTO profile))
                    return;

                SymbiosisChemicalLinkDTO[] linkScratch = EnsureEditorLinkScratch(ref _symbiosisLinkManagedScratch, LinkCapacity);
                ParseCsvOverrides(new ReadOnlySpan<byte>(scratch, 0, bytesRead), ref profile, linkScratch, out int linkCount);
                profile.Flags |= TuningFlagCsvOverride;
                if (!TryWriteSymbiosisLinks(vault, linkScratch, linkCount))
                    return;
                if (!TryWriteSymbiosisTuning(vault, SymbiosisTuningDTO.Sanitize(profile)))
                    return;
                TryIncrementSymbiosisCsvCounter(vault);

                _runtimeFlags |= TuningFlagCsvOverride;
                _csvTimestampTicks = lastWriteUtc.Ticks;
            }
            catch (IOException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x53364356u, SourceHash, 0f);
            }
            catch (UnauthorizedAccessException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x53364356u, SourceHash, 0f);
            }
            catch (ArgumentException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x53364356u, SourceHash, 0f);
            }
            catch (NotSupportedException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x53364356u, SourceHash, 0f);
            }
            catch (InvalidOperationException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x53364356u, SourceHash, 0f);
            }
        }

        private bool TryLoadLegacyLinksIntoVault(IDataVault vault)
        {
            if (!TryReadSymbiosisCounter(vault, out SymbiosisCounterDTO counter))
                return false;

            if ((counter.Flags & TuningFlagLegacyBinary) != 0u)
                return false;

            try
            {
                string path = BuildLegacyPath();
                if (path == null || path.Length == 0 || !File.Exists(path))
                    return false;

                byte[] scratch = EnsureEditorByteScratch(ref _symbiosisLegacyManagedScratch, LegacyScratchBytes);
                int bytesRead = LoadFileIntoManagedScratch(path, scratch, LegacyScratchBytes, FileShare.Read);
                if (bytesRead < UnsafeUtility.SizeOf<SymbiosisChemicalLinkDTO>())
                    return false;

                int stride = UnsafeUtility.SizeOf<SymbiosisChemicalLinkDTO>();
                ReadOnlySpan<byte> payload = new ReadOnlySpan<byte>(scratch, 0, bytesRead);
                ResolveLegacyLinkEncoding(payload, out bool bigEndian, out int payloadOffset);
                int payloadBytes = math.max(0, bytesRead - payloadOffset);
                int count = math.min(LinkCapacity, payloadBytes / stride);
                SymbiosisChemicalLinkDTO[] linkScratch = EnsureEditorLinkScratch(ref _symbiosisLinkManagedScratch, LinkCapacity);
                for (int i = 0; i < count; i++)
                {
                    int offset = payloadOffset + (i * stride);
                    linkScratch[i] = new SymbiosisChemicalLinkDTO
                    {
                        FloraHash = ReadUInt32(payload, offset, bigEndian),
                        FaunaHash = ReadUInt32(payload, offset + 4, bigEndian),
                        ChemicalTransferRate = ReadFloat32(payload, offset + 8, bigEndian, 0.01f),
                        Flags = ReadUInt32(payload, offset + 12, bigEndian)
                    };
                }

                if (!TryWriteSymbiosisLinks(vault, linkScratch, count))
                    return false;
                if (!TryReadSymbiosisTuning(vault, out SymbiosisTuningDTO profile))
                    return false;

                profile.ActiveLinkCount = count;
                profile.Flags |= TuningFlagLegacyBinary;
                if (!TryWriteSymbiosisTuning(vault, profile))
                    return false;

                counter.Flags |= TuningFlagLegacyBinary;
                if (!TryWriteSymbiosisCounter(vault, counter))
                    return false;
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

        private static byte[] EnsureEditorByteScratch(ref byte[] scratch, int minimumLength)
        {
            int length = math.max(1, minimumLength);
            if (scratch == null || scratch.Length < length)
                scratch = new byte[length];
            return scratch;
        }

        private static SymbiosisChemicalLinkDTO[] EnsureEditorLinkScratch(
            ref SymbiosisChemicalLinkDTO[] scratch,
            int minimumLength)
        {
            int length = math.max(1, minimumLength);
            if (scratch == null || scratch.Length < length)
                scratch = new SymbiosisChemicalLinkDTO[length];
            return scratch;
        }

        private static int LoadFileIntoManagedScratch(
            string path,
            byte[] scratch,
            int maxBytes,
            FileShare share)
        {
            if (path == null || scratch == null || maxBytes <= 0)
                return 0;

            int limit = math.min(maxBytes, scratch.Length);
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, share, 4096, FileOptions.SequentialScan))
            {
                int read = stream.Read(scratch, 0, limit);
                return math.max(0, math.min(read, limit));
            }
        }

        private bool TryReadSymbiosisTuning(IDataVault vault, out SymbiosisTuningDTO dto)
        {
            dto = SymbiosisTuningDTO.Default();
            SymbiosisTuningDTO raw = default;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (!TryAcquireSymbiosisMutationGuard(vault, BufferID.ShinobuSymbiosisTuning))
            {
                return false;
            }

            try
            {
                if (!TryResolveOwnedVaultBuffer(vault, in _tuningHandle, BufferID.ShinobuSymbiosisTuning, out NativeArray<SymbiosisTuningDTO> tuning) ||
                    !tuning.IsCreated ||
                    tuning.Length <= 0)
                {
                    return false;
                }

                raw = tuning[0];
                dto = SymbiosisTuningDTO.Sanitize(raw);
                return true;
            }
            finally
            {
                ReleaseSymbiosisMutationGuard(vault, BufferID.ShinobuSymbiosisTuning);
            }
        }
#endif

        private bool TryWriteSymbiosisTuning(IDataVault vault, SymbiosisTuningDTO dto)
        {
            SymbiosisTuningDTO sanitized = SymbiosisTuningDTO.Sanitize(dto);
            if (!TryAcquireSymbiosisMutationGuard(vault, BufferID.ShinobuSymbiosisTuning))
            {
                return false;
            }

            try
            {
                if (!TryResolveOwnedVaultBuffer(vault, in _tuningHandle, BufferID.ShinobuSymbiosisTuning, out NativeArray<SymbiosisTuningDTO> tuning) ||
                    !tuning.IsCreated ||
                    tuning.Length <= 0)
                {
                    return false;
                }

                tuning[0] = sanitized;
                return true;
            }
            finally
            {
                ReleaseSymbiosisMutationGuard(vault, BufferID.ShinobuSymbiosisTuning);
            }
        }

        private unsafe bool TryWriteSymbiosisLinks(
            IDataVault vault,
            SymbiosisChemicalLinkDTO[] source,
            int count)
        {
            if (source == null ||
                vault == null)
            {
                return false;
            }

            int requestedCount = math.min(math.max(0, count), source.Length);
            if (requestedCount <= 0)
                return true;

            if (!TryAcquireSymbiosisMutationGuard(vault, BufferID.ShinobuSymbiosisLinks))
            {
                return false;
            }

            try
            {
                if (!TryResolveOwnedVaultBuffer(vault, in _linkHandle, BufferID.ShinobuSymbiosisLinks, out NativeArray<SymbiosisChemicalLinkDTO> links) ||
                    !links.IsCreated)
                {
                    return false;
                }

                int writeCount = math.min(requestedCount, links.Length);
                if (writeCount <= 0)
                    return true;

                void* targetPtr = NativeArrayUnsafeUtility.GetUnsafePtr(links);
                fixed (SymbiosisChemicalLinkDTO* sourcePtr = source)
                {
                    UnsafeUtility.MemCpy(targetPtr, sourcePtr, (long)writeCount * UnsafeUtility.SizeOf<SymbiosisChemicalLinkDTO>());
                }

                return true;
            }
            finally
            {
                ReleaseSymbiosisMutationGuard(vault, BufferID.ShinobuSymbiosisLinks);
            }
        }

        private bool TryReadSymbiosisCounter(IDataVault vault, out SymbiosisCounterDTO counter)
        {
            counter = default;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (!TryAcquireSymbiosisMutationGuard(vault, BufferID.ShinobuSymbiosisCounters))
            {
                return false;
            }

            try
            {
                if (!TryResolveOwnedVaultBuffer(vault, in _counterHandle, BufferID.ShinobuSymbiosisCounters, out NativeArray<SymbiosisCounterDTO> counters) ||
                    !counters.IsCreated ||
                    counters.Length <= 0)
                {
                    return false;
                }

                counter = counters[0];
                return true;
            }
            finally
            {
                ReleaseSymbiosisMutationGuard(vault, BufferID.ShinobuSymbiosisCounters);
            }
        }

        private bool TryWriteSymbiosisCounter(IDataVault vault, SymbiosisCounterDTO counter)
        {
            if (!TryAcquireSymbiosisMutationGuard(vault, BufferID.ShinobuSymbiosisCounters))
            {
                return false;
            }

            try
            {
                if (!TryResolveOwnedVaultBuffer(vault, in _counterHandle, BufferID.ShinobuSymbiosisCounters, out NativeArray<SymbiosisCounterDTO> counters) ||
                    !counters.IsCreated ||
                    counters.Length <= 0)
                {
                    return false;
                }

                counters[0] = counter;
                return true;
            }
            finally
            {
                ReleaseSymbiosisMutationGuard(vault, BufferID.ShinobuSymbiosisCounters);
            }
        }

        private void TryIncrementSymbiosisCsvCounter(IDataVault vault)
        {
            if (!TryReadSymbiosisCounter(vault, out SymbiosisCounterDTO counter))
                return;

            counter.CsvLoaded++;
            TryWriteSymbiosisCounter(vault, counter);
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

            FinishFrameJobCompletion();
        }

        private void FinishFrameJobCompletion()
        {
            _jobScheduled = false;
            long completeTicks = Stopwatch.GetTimestamp();
            long elapsedTicks = completeTicks >= _scheduleTicks ? completeTicks - _scheduleTicks : 0L;
            _lastSolverMs = Stopwatch.Frequency > 0
                ? (float)(elapsedTicks * 1000.0 / Stopwatch.Frequency)
                : 0f;

            IDataVault vault = _dataVault;
            bool hasTelemetry = false;
            SymbiosisTelemetryEntry telemetryEntry = default;
            bool telemetryFault = false;
            int acousticTapCount = 0;

            if (vault != null)
            {
                hasTelemetry = TryBuildTelemetryEntry(out telemetryEntry, out telemetryFault);
                acousticTapCount = BuildAcousticTapPublishScratch();
            }

            if (vault == null || !TryPublishJobBuffersToVault(vault))
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x53365046u, SourceHash, 0f);
                return;
            }

            PublishAcousticTapsFromScratch(acousticTapCount);
            if (hasTelemetry)
                WriteTelemetryAndFaultDump(vault, in telemetryEntry, telemetryFault);
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

        private bool TryBuildTelemetryEntry(
            out SymbiosisTelemetryEntry entry,
            out bool shouldDump)
        {
            entry = default;
            shouldDump = false;
            NativeArray<SymbiosisCounterDTO> counters = _counterJobBuffer;
            if (!counters.IsCreated || counters.Length <= 0)
                return false;

            SymbiosisCounterDTO counter = counters[0];
            uint stateHash = MixTelemetryHash(counter.ActiveExchanges, counter.BiomassTransferredMilli, counter.InvalidMath, counter.OverflowCount);
            uint frame = counter.Frame != 0u ? counter.Frame : _simulationFrameCounter;
            entry.Frame = frame;
            entry.StateHash = stateHash;
            entry.ActiveExchanges = counter.ActiveExchanges;
            entry.BiomassTransferred = counter.BiomassTransferredMilli * 0.001f;
            entry.SolverComputeTimeMs = math.max(0f, _lastSolverMs);
            entry.OxygenEmitterCount = counter.OxygenEmitterCount;
            entry.ToxemiaCount = counter.ToxemiaCount;
            entry.CamouflageCount = counter.CamouflageCount;
            entry.SeedCount = counter.SeedCount;
            entry.AdherenceCount = counter.AdherenceCount;
            entry.AcousticTapCount = counter.AcousticTapCount;
            entry.Flags = _runtimeFlags | counter.Flags;
            entry.InvalidMathCount = counter.InvalidMath;
            entry.OverflowCount = counter.OverflowCount;
            shouldDump = counter.InvalidMath != 0 || counter.OverflowCount != 0;
            return true;
        }

        private void WriteTelemetryAndFaultDump(
            IDataVault vault,
            in SymbiosisTelemetryEntry entry,
            bool shouldDump)
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(SymbiosisTelemetryMutationGuardMask))
            {
                return;
            }

            bool dumpAfterRelease = false;
            int dumpCursor = 0;
            try
            {
                if (!TryResolveOwnedVaultBuffer(vault, in _telemetryHandle, BufferID.ShinobuSymbiosisTelemetryRing, out NativeArray<SymbiosisTelemetryEntry> telemetry) ||
                    !telemetry.IsCreated ||
                    telemetry.Length <= 0)
                {
                    return;
                }

                int cursor = _telemetryCursor;
                if (cursor < 0 || cursor >= int.MaxValue - telemetry.Length)
                    cursor = 0;

                int index = cursor % telemetry.Length;
                int nextCursor = cursor + 1;
                _telemetryCursor = nextCursor;
                telemetry[index] = entry;
                if (_telemetryMirror.IsCreated && _telemetryMirror.Length == telemetry.Length)
                    _telemetryMirror[index] = entry;

                if (shouldDump && !_dumpedFault)
                {
                    _dumpedFault = true;
                    dumpAfterRelease = true;
                    dumpCursor = nextCursor;
                }
            }
            finally
            {
                vault.ReleaseMutationGuard(SymbiosisTelemetryMutationGuardMask);
            }

            if (dumpAfterRelease && _telemetryMirror.IsCreated)
                DumpBlackBox(_telemetryMirror, dumpCursor);
        }

        private int BuildAcousticTapPublishScratch()
        {
            NativeArray<SymbiosisCounterDTO> counters = _counterJobBuffer;
            NativeArray<SymbiosisAcousticTapDTO> taps = _acousticTapJobBuffer;
            if (!counters.IsCreated ||
                counters.Length <= 0 ||
                !taps.IsCreated ||
                !_acousticTapPublishScratch.IsCreated)
            {
                return 0;
            }

            int count = math.min(counters[0].AcousticTapCount, math.min(taps.Length, _acousticTapPublishScratch.Length));
            for (int i = 0; i < count; i++)
                _acousticTapPublishScratch[i] = taps[i];
            return count;
        }

        private void PublishAcousticTapsFromScratch(int count)
        {
            if (!_acousticTapPublishScratch.IsCreated)
                return;

            int safeCount = math.min(math.max(0, count), _acousticTapPublishScratch.Length);
            for (int i = 0; i < safeCount; i++)
            {
                SymbiosisAcousticTapDTO tap = _acousticTapPublishScratch[i];
                if ((tap.Flags & 1u) == 0u || !IsFiniteAup(in tap.PositionAup))
                    continue;

                AbsoluteUniversePosition tapAup = tap.PositionAup.ToAup();
                AcousticPingSignal signal = default;
                signal.PositionAup = tapAup;
                signal.RadiusMeters = math.max(1f, tap.RadiusMeters);
                signal.Intensity01 = math.saturate(tap.Magnitude01);
                signal.SourceId = tap.SourceHash;
                signal.Channel = AcousticPingSignal.ChannelJawSnap;
                signal.Flags = AcousticPingSignal.FlagJawSnap;
                SignalBus<AcousticPingSignal>.TryPushTracked(in signal, ref s_x001ShinobuFloraFaunaSymbiosisSolverSignalPushDropCount);
            }
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
            _vaultStateReady = false;
            _floraHandle = default;
            _floraAupHandle = default;
            _linkHandle = default;
            _exchangeHandle = default;
            _telemetryHandle = default;
            _counterHandle = default;
            _scannerVfxHandle = default;
            _oxygenEmitterHandle = default;
            _adherenceHandle = default;
            _seedHandle = default;
            _acousticTapHandle = default;
            _tuningHandle = default;
            _floraBucketHeadHandle = default;
            _floraBucketNextHandle = default;
            _mockBoidHandle = default;
            _mockFishHandle = default;
            _ambientEntityHandle = default;
            _ambientAupHandle = default;
            _anomalyFieldHandle = default;
            _ownsAmbientEntityHandle = false;
            _ownsAmbientAupHandle = false;
        }

        private void RebindDataVaultForLifecycle(IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            ReleaseVaultStateForLifecycle();
            _dataVault = nextVault;
        }

        private void ReleaseVaultStateForLifecycle()
        {
            CompleteFrameJobForTeardown();
            ReleaseOwnedVaultHandles(_dataVault);
            DisposeLocalBuffersCold();
            ClearCachedState();
        }

        private void ReleaseOwnedVaultHandles(IDataVault vault)
        {
            ReleaseOwnedVaultHandle(vault, ref _floraHandle, BufferID.ShinobuSymbiosisFlora);
            ReleaseOwnedVaultHandle(vault, ref _floraAupHandle, BufferID.ShinobuSymbiosisFloraAups);
            ReleaseOwnedVaultHandle(vault, ref _linkHandle, BufferID.ShinobuSymbiosisLinks);
            ReleaseOwnedVaultHandle(vault, ref _exchangeHandle, BufferID.ShinobuSymbiosisExchanges);
            ReleaseOwnedVaultHandle(vault, ref _telemetryHandle, BufferID.ShinobuSymbiosisTelemetryRing);
            ReleaseOwnedVaultHandle(vault, ref _counterHandle, BufferID.ShinobuSymbiosisCounters);
            ReleaseOwnedVaultHandle(vault, ref _scannerVfxHandle, BufferID.ShinobuSymbiosisScannerVfx);
            ReleaseOwnedVaultHandle(vault, ref _oxygenEmitterHandle, BufferID.ShinobuSymbiosisOxygenEmitters);
            ReleaseOwnedVaultHandle(vault, ref _adherenceHandle, BufferID.ShinobuSymbiosisAdherence);
            ReleaseOwnedVaultHandle(vault, ref _seedHandle, BufferID.ShinobuSymbiosisSeeds);
            ReleaseOwnedVaultHandle(vault, ref _acousticTapHandle, BufferID.ShinobuSymbiosisAcousticTaps);
            ReleaseOwnedVaultHandle(vault, ref _tuningHandle, BufferID.ShinobuSymbiosisTuning);
            ReleaseOwnedVaultHandle(vault, ref _floraBucketHeadHandle, BufferID.ShinobuSymbiosisFloraHashBucketHeads);
            ReleaseOwnedVaultHandle(vault, ref _floraBucketNextHandle, BufferID.ShinobuSymbiosisFloraHashNext);
            ReleaseOwnedVaultHandle(vault, ref _mockBoidHandle, BufferID.ShinobuSymbiosisMockBoids);
            ReleaseOwnedVaultHandle(vault, ref _mockFishHandle, BufferID.ShinobuSymbiosisMockFish);

            if (_ownsAmbientEntityHandle)
                ReleaseOwnedVaultHandle(vault, ref _ambientEntityHandle, BufferID.ShinobuAmbientEntities);
            else
                _ambientEntityHandle = default;

            if (_ownsAmbientAupHandle)
                ReleaseOwnedVaultHandle(vault, ref _ambientAupHandle, BufferID.ShinobuAmbientAups);
            else
                _ambientAupHandle = default;

            _anomalyFieldHandle = default;
            _ownsAmbientEntityHandle = false;
            _ownsAmbientAupHandle = false;
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

        private void ClearCachedState()
        {
            _dataVault = null;
            ResetVaultHandles();
#if UNITY_EDITOR
            _csvTimestampTicks = 0L;
            _symbiosisCsvManagedScratch = null;
            _symbiosisLegacyManagedScratch = null;
            _symbiosisLinkManagedScratch = null;
#endif
            _scheduleTicks = 0L;
            _telemetryCursor = 0;
            _simulationFrameCounter = 0u;
            _submarineIdleSeconds = 0f;
            _lastSolverMs = 0f;
            _dumpedFault = false;
            _hasSubmarineAup = false;
            _runtimeFlags = 0u;
        }

        // Editor-only: both paths are built from the parent of Application.dataPath, i.e. the
        // repository root. BuildLegacyPath resolves <root>/Docs/Archive/..., which does not exist in
        // a shipped player, so the CSV override and legacy-migration routes are authoring-time only.
#if UNITY_EDITOR
        private static string BuildCsvPath()
        {
            string root = BuildProjectRootForIo();
            string precomputed = Path.Combine(root, CsvPrecomputedRelativePath);
            if (File.Exists(precomputed))
                return precomputed;

            return Path.Combine(root, CsvRelativePath);
        }

        private static string BuildLegacyPath()
        {
            string root = BuildProjectRootForIo();
            return Path.Combine(root, "Docs", "Archive", LegacyLinksFile);
        }

        private static string BuildProjectRootForIo()
        {
            string assetsPath = Application.dataPath;
            DirectoryInfo parent = Directory.GetParent(assetsPath);
            return parent != null ? parent.FullName : assetsPath;
        }
#endif

        private static void DumpBlackBox(NativeArray<SymbiosisTelemetryEntry> telemetry, int cursor)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x53364450u, SourceHash, 0f);
                return;
            }

            WriteBlackBoxFile(DumpRelativePath, telemetry, cursor);
        }

        private static unsafe void WriteBlackBoxFile(string path, NativeArray<SymbiosisTelemetryEntry> telemetry, int cursor)
        {
            int entrySize = UnsafeUtility.SizeOf<SymbiosisTelemetryEntry>();
            if (string.IsNullOrEmpty(path) ||
                entrySize != 64 ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0)
            {
                return;
            }

            int capacity = telemetry.Length;
            int written = math.max(0, cursor);
            int count = math.min(math.min(capacity, TelemetryCapacity), written);
            if (count <= 0)
                count = math.min(capacity, TelemetryCapacity);

            int start = written < capacity ? 0 : cursor % capacity;
            if (start < 0)
                start = 0;

            int byteCount = DumpHeaderBytes + (count * entrySize);
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(ShinobuFloraFaunaSymbiosisSolver),
                    DumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                WriteUInt64LittleEndian(target, 0, DumpMagic);
                WriteInt32LittleEndian(target, 8, DumpVersion);
                WriteInt32LittleEndian(target, 12, capacity);
                WriteInt32LittleEndian(target, 16, count);
                WriteInt32LittleEndian(target, 20, cursor);
                WriteInt32LittleEndian(target, 24, start);
                WriteInt32LittleEndian(target, 28, entrySize);

                int offset = DumpHeaderBytes;
                for (int i = 0; i < count; i++)
                {
                    int slot = start + i;
                    if (slot >= capacity)
                        slot -= capacity;

                    SymbiosisTelemetryEntry entry = telemetry[slot];
                    UnsafeUtility.MemCpy(target + offset, &entry, entrySize);
                    offset += entrySize;
                }

                NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(ShinobuFloraFaunaSymbiosisSolver),
                    DumpPayloadLabel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WriteInt32LittleEndian(byte* destination, int offset, int value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WriteUInt64LittleEndian(byte* destination, int offset, ulong value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
            WriteUInt32LittleEndian(destination, offset + 4, unchecked((uint)(value >> 32)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WriteUInt32LittleEndian(byte* destination, int offset, uint value)
        {
            destination[offset] = unchecked((byte)value);
            destination[offset + 1] = unchecked((byte)(value >> 8));
            destination[offset + 2] = unchecked((byte)(value >> 16));
            destination[offset + 3] = unchecked((byte)(value >> 24));
        }

        private static void ParseCsvOverrides(
            ReadOnlySpan<byte> bytes,
            ref SymbiosisTuningDTO tuning,
            SymbiosisChemicalLinkDTO[] links,
            out int linkCount)
        {
            linkCount = 0;
            if (links == null)
                return;

            int length = bytes.Length;
            int cursor = 0;
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
                    if (linkCount < links.Length)
                    {
                        SymbiosisChemicalLinkDTO link = default;
                        cursor = ParseCsvUInt(bytes, cursor, length, out link.FloraHash);
                        cursor = ParseCsvUInt(bytes, cursor, length, out link.FaunaHash);
                        cursor = ParseCsvFloat(bytes, cursor, length, out link.ChemicalTransferRate);
                        cursor = ParseCsvUInt(bytes, cursor, length, out link.Flags);
                        links[linkCount] = link;
                        linkCount++;
                        tuning.ActiveLinkCount = math.max(tuning.ActiveLinkCount, linkCount);
                    }

                    cursor = SkipLine(bytes, cursor, length);
                    continue;
                }

                cursor = ParseCsvFloat(bytes, cursor, length, out float value);
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

        private static int ParseCsvFloat(ReadOnlySpan<byte> bytes, int cursor, int length, out float value)
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

        private static int ParseCsvUInt(ReadOnlySpan<byte> bytes, int cursor, int length, out uint value)
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

        private static int SkipLine(ReadOnlySpan<byte> bytes, int cursor, int length)
        {
            while (cursor < length && bytes[cursor] != (byte)'\n')
                cursor++;
            return cursor < length ? cursor + 1 : cursor;
        }

        private static uint HashAsciiLower(ReadOnlySpan<byte> bytes, int start, int end)
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

        private static void ResolveLegacyLinkEncoding(ReadOnlySpan<byte> bytes, out bool bigEndian, out int payloadOffset)
        {
            bigEndian = false;
            payloadOffset = 0;
            if (bytes.Length < LegacyLinksHeaderBytes)
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

        private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset, bool bigEndian)
        {
            if (offset < 0 || offset > bytes.Length - 4)
                return 0u;

            uint raw = (uint)(bytes[offset] |
                              (bytes[offset + 1] << 8) |
                              (bytes[offset + 2] << 16) |
                              (bytes[offset + 3] << 24));
            return bigEndian ? ReverseUInt32(raw) : raw;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReverseUInt32(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }

        private static float ReadFloat32(ReadOnlySpan<byte> bytes, int offset, bool bigEndian, float fallback)
        {
            uint raw = ReadUInt32(bytes, offset, bigEndian);
            float value = math.asfloat(raw);
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 AupToLocal(in AbsoluteUniversePosition position, in AbsoluteUniversePosition center)
        {
            double3 delta = math.double3(
                (((double)position.GridX - center.GridX) * AupCellSizeMetersDouble) + (position.LocalX - center.LocalX),
                (((double)position.GridY - center.GridY) * AupCellSizeMetersDouble) + (position.LocalY - center.LocalY),
                (((double)position.GridZ - center.GridZ) * AupCellSizeMetersDouble) + (position.LocalZ - center.LocalZ));
            return ToFiniteLocalFloat3(delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 AupToLocal(in SymbiosisAup48 position, in AbsoluteUniversePosition center)
        {
            double3 delta = math.double3(
                (((double)position.GridX - center.GridX) * AupCellSizeMetersDouble) + (position.LocalX - center.LocalX),
                (((double)position.GridY - center.GridY) * AupCellSizeMetersDouble) + (position.LocalY - center.LocalY),
                (((double)position.GridZ - center.GridZ) * AupCellSizeMetersDouble) + (position.LocalZ - center.LocalZ));
            return ToFiniteLocalFloat3(delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 AupToLocal(in SymbiosisAup48 position, in SymbiosisAup48 center)
        {
            double3 delta = math.double3(
                (((double)position.GridX - center.GridX) * AupCellSizeMetersDouble) + (position.LocalX - center.LocalX),
                (((double)position.GridY - center.GridY) * AupCellSizeMetersDouble) + (position.LocalY - center.LocalY),
                (((double)position.GridZ - center.GridZ) * AupCellSizeMetersDouble) + (position.LocalZ - center.LocalZ));
            return ToFiniteLocalFloat3(delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 AupToLocal(in AbsoluteUniversePosition position, in SymbiosisAup48 center)
        {
            double3 delta = math.double3(
                (((double)position.GridX - center.GridX) * AupCellSizeMetersDouble) + (position.LocalX - center.LocalX),
                (((double)position.GridY - center.GridY) * AupCellSizeMetersDouble) + (position.LocalY - center.LocalY),
                (((double)position.GridZ - center.GridZ) * AupCellSizeMetersDouble) + (position.LocalZ - center.LocalZ));
            return ToFiniteLocalFloat3(delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ToFiniteLocalFloat3(double3 delta)
        {
            if (!math.all(math.isfinite(delta)))
                return float3.zero;

            float3 local = (float3)delta;
            return math.all(math.isfinite(local)) ? local : float3.zero;
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
            return math.double3(
                (aup.GridX * AupCellSizeMetersDouble) + aup.LocalX,
                (aup.GridY * AupCellSizeMetersDouble) + aup.LocalY,
                (aup.GridZ * AupCellSizeMetersDouble) + aup.LocalZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double3 ToAbsoluteDouble3(in SymbiosisAup48 aup)
        {
            return math.double3(
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
            AbsoluteUniversePosition result = default;
            result.GridX = gridX;
            result.GridY = gridY;
            result.GridZ = gridZ;
            result.LocalX = (float)(absolute.x - (gridX * AupCellSizeMetersDouble));
            result.LocalY = (float)(absolute.y - (gridY * AupCellSizeMetersDouble));
            result.LocalZ = (float)(absolute.z - (gridZ * AupCellSizeMetersDouble));
            return result;
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
            return math.int3(
                (int)math.floor(absolute.x * inv),
                (int)math.floor(absolute.y * inv),
                (int)math.floor(absolute.z * inv));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int3 ResolveSectorCoord(in SymbiosisAup48 aup, float sectorSize)
        {
            double3 absolute = ToAbsoluteDouble3(in aup);
            double inv = 1.0d / math.max(1.0d, sectorSize);
            return math.int3(
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ResolveMicroExchangeWeight(float quality, float macroThreshold)
        {
            float threshold = math.clamp(macroThreshold, 0.05f, 0.95f);
            float width = math.max(1f - threshold, 0.0001f);
            float t = math.saturate((math.saturate(quality) - threshold) * math.rcp(width));
            return t * t * (3f - (2f * t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool ResolveDitheredFrameGate(uint seed, float weight)
        {
            uint hash = MixHash(seed);
            const float invHashBucketCount = 5.9604645e-8f;
            float sample = (hash & 0x00FFFFFFu) * invHashBucketCount;
            return sample < math.saturate(weight);
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
        private const string LayoutSizeMismatchMessage = "[SymbiosisLayoutManifest] Size mismatch";
        private const string LayoutOffsetMismatchMessage = "[SymbiosisLayoutManifest] Offset mismatch";

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
            AssertSize<SymbiosisAnomalyFieldMirror>(48);
            AssertOffsets();
            _verified = true;
        }

        private static void AssertOffsets()
        {
            AssertOffset<SymbiosisExchangeDTO>(nameof(SymbiosisExchangeDTO.FloraHash), 0);
            AssertOffset<SymbiosisExchangeDTO>(nameof(SymbiosisExchangeDTO.FaunaHash), 4);
            AssertOffset<SymbiosisExchangeDTO>(nameof(SymbiosisExchangeDTO.ChemicalTransferRate), 8);
            AssertOffset<SymbiosisExchangeDTO>(nameof(SymbiosisExchangeDTO._pad0), 12);
            AssertOffset<SymbiosisChemicalLinkDTO>(nameof(SymbiosisChemicalLinkDTO.FloraHash), 0);
            AssertOffset<SymbiosisChemicalLinkDTO>(nameof(SymbiosisChemicalLinkDTO.FaunaHash), 4);
            AssertOffset<SymbiosisChemicalLinkDTO>(nameof(SymbiosisChemicalLinkDTO.ChemicalTransferRate), 8);
            AssertOffset<SymbiosisChemicalLinkDTO>(nameof(SymbiosisChemicalLinkDTO.Flags), 12);
            AssertOffset<SymbiosisAup48>(nameof(SymbiosisAup48.GridX), 0);
            AssertOffset<SymbiosisAup48>(nameof(SymbiosisAup48.GridY), 8);
            AssertOffset<SymbiosisAup48>(nameof(SymbiosisAup48.GridZ), 16);
            AssertOffset<SymbiosisAup48>(nameof(SymbiosisAup48.LocalX), 24);
            AssertOffset<SymbiosisAup48>(nameof(SymbiosisAup48.LocalY), 28);
            AssertOffset<SymbiosisAup48>(nameof(SymbiosisAup48.LocalZ), 32);
            AssertOffset<SymbiosisAup48>(nameof(SymbiosisAup48._pad0), 36);
            AssertOffset<SymbiosisAup48>(nameof(SymbiosisAup48._pad1), 40);
            AssertOffset<SymbiosisFloraDTO>(nameof(SymbiosisFloraDTO.LocalPosition), 0);
            AssertOffset<SymbiosisFloraDTO>(nameof(SymbiosisFloraDTO.Biomass), 12);
            AssertOffset<SymbiosisFloraDTO>(nameof(SymbiosisFloraDTO.FloraHash), 16);
            AssertOffset<SymbiosisFloraDTO>(nameof(SymbiosisFloraDTO.ChemicalMask), 20);
            AssertOffset<SymbiosisFloraDTO>(nameof(SymbiosisFloraDTO.OxygenRate), 24);
            AssertOffset<SymbiosisFloraDTO>(nameof(SymbiosisFloraDTO.ToxicPotency), 28);
            AssertOffset<SymbiosisFloraDTO>(nameof(SymbiosisFloraDTO.CamouflageRadius), 32);
            AssertOffset<SymbiosisFloraDTO>(nameof(SymbiosisFloraDTO.FeedingRadius), 36);
            AssertOffset<SymbiosisFloraDTO>(nameof(SymbiosisFloraDTO.Flags), 40);
            AssertOffset<SymbiosisFloraDTO>(nameof(SymbiosisFloraDTO._pad0), 44);
            AssertOffset<SymbiosisFloraAupDTO>(nameof(SymbiosisFloraAupDTO.PositionAup), 0);
            AssertOffset<SymbiosisFloraAupDTO>(nameof(SymbiosisFloraAupDTO.FloraHash), 48);
            AssertOffset<SymbiosisFloraAupDTO>(nameof(SymbiosisFloraAupDTO.SectorHash), 52);
            AssertOffset<SymbiosisFloraAupDTO>(nameof(SymbiosisFloraAupDTO.SpatialCellHash), 56);
            AssertOffset<SymbiosisFloraAupDTO>(nameof(SymbiosisFloraAupDTO.StableSeed), 60);
            AssertOffset<SymbiosisTuningDTO>(nameof(SymbiosisTuningDTO.FeedingRate), 0);
            AssertOffset<SymbiosisTuningDTO>(nameof(SymbiosisTuningDTO.ToxinPotency), 4);
            AssertOffset<SymbiosisTuningDTO>(nameof(SymbiosisTuningDTO.CamouflageRadius), 8);
            AssertOffset<SymbiosisTuningDTO>(nameof(SymbiosisTuningDTO.ParasiteGrowthSpeed), 12);
            AssertOffset<SymbiosisTuningDTO>(nameof(SymbiosisTuningDTO.GlobalQualityWeight), 16);
            AssertOffset<SymbiosisTuningDTO>(nameof(SymbiosisTuningDTO.MacroThreshold), 20);
            AssertOffset<SymbiosisTuningDTO>(nameof(SymbiosisTuningDTO.OxygenRateScale), 24);
            AssertOffset<SymbiosisTuningDTO>(nameof(SymbiosisTuningDTO.SeedShipToxicBoost), 28);
            AssertOffset<SymbiosisTuningDTO>(nameof(SymbiosisTuningDTO.AcousticThreshold), 32);
            AssertOffset<SymbiosisTuningDTO>(nameof(SymbiosisTuningDTO.FeedingRadius), 36);
            AssertOffset<SymbiosisTuningDTO>(nameof(SymbiosisTuningDTO.SimulationTickDelta), 40);
            AssertOffset<SymbiosisTuningDTO>(nameof(SymbiosisTuningDTO.CorruptionLevel), 44);
            AssertOffset<SymbiosisTuningDTO>(nameof(SymbiosisTuningDTO.Flags), 48);
            AssertOffset<SymbiosisTuningDTO>(nameof(SymbiosisTuningDTO.ActiveFloraCount), 52);
            AssertOffset<SymbiosisTuningDTO>(nameof(SymbiosisTuningDTO.ActiveLinkCount), 56);
            AssertOffset<SymbiosisTuningDTO>(nameof(SymbiosisTuningDTO._pad0), 60);
            AssertOffset<SymbiosisCounterDTO>(nameof(SymbiosisCounterDTO.ActiveExchanges), 0);
            AssertOffset<SymbiosisCounterDTO>(nameof(SymbiosisCounterDTO.BiomassTransferredMilli), 4);
            AssertOffset<SymbiosisCounterDTO>(nameof(SymbiosisCounterDTO.ToxemiaCount), 8);
            AssertOffset<SymbiosisCounterDTO>(nameof(SymbiosisCounterDTO.CamouflageCount), 12);
            AssertOffset<SymbiosisCounterDTO>(nameof(SymbiosisCounterDTO.OxygenEmitterCount), 16);
            AssertOffset<SymbiosisCounterDTO>(nameof(SymbiosisCounterDTO.SeedCount), 20);
            AssertOffset<SymbiosisCounterDTO>(nameof(SymbiosisCounterDTO.AdherenceCount), 24);
            AssertOffset<SymbiosisCounterDTO>(nameof(SymbiosisCounterDTO.AcousticTapCount), 28);
            AssertOffset<SymbiosisCounterDTO>(nameof(SymbiosisCounterDTO.InvalidMath), 32);
            AssertOffset<SymbiosisCounterDTO>(nameof(SymbiosisCounterDTO.OverflowCount), 36);
            AssertOffset<SymbiosisCounterDTO>(nameof(SymbiosisCounterDTO.CsvLoaded), 40);
            AssertOffset<SymbiosisCounterDTO>(nameof(SymbiosisCounterDTO.Initialized), 44);
            AssertOffset<SymbiosisCounterDTO>(nameof(SymbiosisCounterDTO.Frame), 48);
            AssertOffset<SymbiosisCounterDTO>(nameof(SymbiosisCounterDTO.Flags), 52);
            AssertOffset<SymbiosisCounterDTO>(nameof(SymbiosisCounterDTO._pad0), 56);
            AssertOffset<SymbiosisTelemetryEntry>(nameof(SymbiosisTelemetryEntry.Frame), 0);
            AssertOffset<SymbiosisTelemetryEntry>(nameof(SymbiosisTelemetryEntry.StateHash), 4);
            AssertOffset<SymbiosisTelemetryEntry>(nameof(SymbiosisTelemetryEntry.ActiveExchanges), 8);
            AssertOffset<SymbiosisTelemetryEntry>(nameof(SymbiosisTelemetryEntry.BiomassTransferred), 12);
            AssertOffset<SymbiosisTelemetryEntry>(nameof(SymbiosisTelemetryEntry.SolverComputeTimeMs), 16);
            AssertOffset<SymbiosisTelemetryEntry>(nameof(SymbiosisTelemetryEntry.OxygenEmitterCount), 20);
            AssertOffset<SymbiosisTelemetryEntry>(nameof(SymbiosisTelemetryEntry.ToxemiaCount), 24);
            AssertOffset<SymbiosisTelemetryEntry>(nameof(SymbiosisTelemetryEntry.CamouflageCount), 28);
            AssertOffset<SymbiosisTelemetryEntry>(nameof(SymbiosisTelemetryEntry.SeedCount), 32);
            AssertOffset<SymbiosisTelemetryEntry>(nameof(SymbiosisTelemetryEntry.AdherenceCount), 36);
            AssertOffset<SymbiosisTelemetryEntry>(nameof(SymbiosisTelemetryEntry.AcousticTapCount), 40);
            AssertOffset<SymbiosisTelemetryEntry>(nameof(SymbiosisTelemetryEntry.Flags), 44);
            AssertOffset<SymbiosisTelemetryEntry>(nameof(SymbiosisTelemetryEntry.InvalidMathCount), 48);
            AssertOffset<SymbiosisTelemetryEntry>(nameof(SymbiosisTelemetryEntry.OverflowCount), 52);
            AssertOffset<SymbiosisTelemetryEntry>(nameof(SymbiosisTelemetryEntry.Pad0), 56);
            AssertOffset<SymbiosisTelemetryEntry>(nameof(SymbiosisTelemetryEntry.Pad1), 60);
            AssertOffset<MockBoidArray>(nameof(MockBoidArray.StartIndex), 0);
            AssertOffset<MockBoidArray>(nameof(MockBoidArray.Count), 4);
            AssertOffset<MockBoidArray>(nameof(MockBoidArray.StableSeed), 8);
            AssertOffset<MockBoidArray>(nameof(MockBoidArray.Flags), 12);
            AssertOffset<MockFishSymbiosisDTO>(nameof(MockFishSymbiosisDTO.PositionAup), 0);
            AssertOffset<MockFishSymbiosisDTO>(nameof(MockFishSymbiosisDTO.Biomass), 48);
            AssertOffset<MockFishSymbiosisDTO>(nameof(MockFishSymbiosisDTO.SpeciesHash), 52);
            AssertOffset<MockFishSymbiosisDTO>(nameof(MockFishSymbiosisDTO.Flags), 56);
            AssertOffset<MockFishSymbiosisDTO>(nameof(MockFishSymbiosisDTO.StableSeed), 60);
            AssertOffset<ScannerVfxDTO>(nameof(ScannerVfxDTO.HitLocal), 0);
            AssertOffset<ScannerVfxDTO>(nameof(ScannerVfxDTO.HitDistance), 12);
            AssertOffset<ScannerVfxDTO>(nameof(ScannerVfxDTO.ScanProgress), 16);
            AssertOffset<ScannerVfxDTO>(nameof(ScannerVfxDTO.TargetHash), 20);
            AssertOffset<ScannerVfxDTO>(nameof(ScannerVfxDTO.Flags), 24);
            AssertOffset<ScannerVfxDTO>(nameof(ScannerVfxDTO.BeamScore), 28);
            AssertOffset<SymbiosisOxygenEmitterDTO>(nameof(SymbiosisOxygenEmitterDTO.LocalPosition), 0);
            AssertOffset<SymbiosisOxygenEmitterDTO>(nameof(SymbiosisOxygenEmitterDTO.Oxygen01), 12);
            AssertOffset<SymbiosisOxygenEmitterDTO>(nameof(SymbiosisOxygenEmitterDTO.SectorHash), 16);
            AssertOffset<SymbiosisOxygenEmitterDTO>(nameof(SymbiosisOxygenEmitterDTO.RadiusMeters), 20);
            AssertOffset<SymbiosisOxygenEmitterDTO>(nameof(SymbiosisOxygenEmitterDTO.FloraHash), 24);
            AssertOffset<SymbiosisOxygenEmitterDTO>(nameof(SymbiosisOxygenEmitterDTO.Flags), 28);
            AssertOffset<AdherenceDTO>(nameof(AdherenceDTO.LocalPosition), 0);
            AssertOffset<AdherenceDTO>(nameof(AdherenceDTO.Growth01), 12);
            AssertOffset<AdherenceDTO>(nameof(AdherenceDTO.HostHash), 16);
            AssertOffset<AdherenceDTO>(nameof(AdherenceDTO.FloraHash), 20);
            AssertOffset<AdherenceDTO>(nameof(AdherenceDTO.Flags), 24);
            AssertOffset<AdherenceDTO>(nameof(AdherenceDTO.Frame), 28);
            AssertOffset<FloraSeedDTO>(nameof(FloraSeedDTO.LocalPosition), 0);
            AssertOffset<FloraSeedDTO>(nameof(FloraSeedDTO.Viability01), 12);
            AssertOffset<FloraSeedDTO>(nameof(FloraSeedDTO.FloraHash), 16);
            AssertOffset<FloraSeedDTO>(nameof(FloraSeedDTO.CarrierHash), 20);
            AssertOffset<FloraSeedDTO>(nameof(FloraSeedDTO.Frame), 24);
            AssertOffset<FloraSeedDTO>(nameof(FloraSeedDTO.Flags), 28);
            AssertOffset<SymbiosisAcousticTapDTO>(nameof(SymbiosisAcousticTapDTO.PositionAup), 0);
            AssertOffset<SymbiosisAcousticTapDTO>(nameof(SymbiosisAcousticTapDTO.Magnitude01), 48);
            AssertOffset<SymbiosisAcousticTapDTO>(nameof(SymbiosisAcousticTapDTO.RadiusMeters), 52);
            AssertOffset<SymbiosisAcousticTapDTO>(nameof(SymbiosisAcousticTapDTO.SourceHash), 56);
            AssertOffset<SymbiosisAcousticTapDTO>(nameof(SymbiosisAcousticTapDTO.Flags), 60);
            AssertOffset<SymbiosisAnomalyFieldMirror>(nameof(SymbiosisAnomalyFieldMirror.EpicenterAUP), 0);
            AssertOffset<SymbiosisAnomalyFieldMirror>(nameof(SymbiosisAnomalyFieldMirror.Radius), 24);
            AssertOffset<SymbiosisAnomalyFieldMirror>(nameof(SymbiosisAnomalyFieldMirror.CorruptionLevel), 28);
            AssertOffset<SymbiosisAnomalyFieldMirror>(nameof(SymbiosisAnomalyFieldMirror.GlitchHash), 32);
            AssertOffset<SymbiosisAnomalyFieldMirror>(nameof(SymbiosisAnomalyFieldMirror._pad0), 36);
            AssertOffset<SymbiosisAnomalyFieldMirror>(nameof(SymbiosisAnomalyFieldMirror._pad1), 40);
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
            SymbiosisAup48 result = default;
            result.GridX = aup.GridX;
            result.GridY = aup.GridY;
            result.GridZ = aup.GridZ;
            result.LocalX = aup.LocalX;
            result.LocalY = aup.LocalY;
            result.LocalZ = aup.LocalZ;
            result._pad0 = 0u;
            result._pad1 = 0UL;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AbsoluteUniversePosition ToAup()
        {
            AbsoluteUniversePosition result = default;
            result.GridX = GridX;
            result.GridY = GridY;
            result.GridZ = GridZ;
            result.LocalX = LocalX;
            result.LocalY = LocalY;
            result.LocalZ = LocalZ;
            return result;
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

    internal static class SymbiosisSimdMath
    {
        private const float Pi = 3.14159265358979323846f;
        private const float TwoPi = 6.28318530717958647692f;
        private const float HalfPi = 1.57079632679489661923f;
        private const float InvTwoPi = 0.15915494309189533577f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LengthFromSq(float lengthSq)
        {
            float finiteSq = math.select(0f, lengthSq, math.isfinite(lengthSq) & lengthSq > 0f);
            return finiteSq * math.rsqrt(math.max(finiteSq, 0.0001f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SinPolynomial7(float angle)
        {
            float x = angle - TwoPi * math.floor((angle + Pi) * InvTwoPi);
            x = math.select(x, Pi - x, x > HalfPi);
            x = math.select(x, -Pi - x, x < -HalfPi);
            float x2 = x * x;
            return x * (1f + x2 * (-0.16666667f + x2 * (0.008333331f + x2 * -0.000198409f)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosPolynomial7(float angle)
        {
            return SinPolynomial7(angle + HalfPi);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct GenerateEmergencyMockSymbiosisJob : IJob
    {
        [WriteOnly, NoAlias] public NativeArray<SymbiosisFloraDTO> Flora;
        [NoAlias] public NativeArray<SymbiosisFloraAupDTO> FloraAups;
        [WriteOnly, NoAlias] public NativeArray<SymbiosisChemicalLinkDTO> Links;
        [WriteOnly, NoAlias] public NativeArray<SymbiosisCounterDTO> Counters;
        [WriteOnly, NoAlias] public NativeArray<MockBoidArray> MockBoids;
        [WriteOnly, NoAlias] public NativeArray<MockFishSymbiosisDTO> MockFish;
        public AbsoluteUniversePosition CenterAup;
        public float GlobalQualityWeight;
        public int FloraCount;
        public int MockFishCount;
        public int LinkCount;
        public uint Seed;

        public void Execute()
        {
            int floraLimit = math.max(0, math.min(FloraCount, math.min(Flora.Length, FloraAups.Length)));
            int fishLimit = math.max(0, math.min(MockFishCount, MockFish.Length));
            int linkLimit = math.max(0, math.min(LinkCount, Links.Length));
            AbsoluteUniversePosition centerAup = ShinobuFloraFaunaSymbiosisSolver.IsFiniteAup(in CenterAup)
                ? CenterAup
                : default;

            SymbiosisTuningDTO tuning = SymbiosisTuningDTO.Default();
            tuning.GlobalQualityWeight = math.saturate(GlobalQualityWeight);
            tuning.ActiveFloraCount = floraLimit;
            tuning.ActiveLinkCount = math.min(5, linkLimit);

            Unity.Mathematics.Random rng = Unity.Mathematics.Random.CreateFromIndex(Seed != 0u ? Seed : 1u);
            for (int i = 0; i < floraLimit; i++)
            {
                int lane = i % 5;
                float ring = 10f + ((i & 31) * 1.7f);
                float angle = (i * 2.3999631f) + rng.NextFloat(-0.08f, 0.08f);
                float3 local = math.float3(
                    SymbiosisSimdMath.CosPolynomial7(angle) * ring,
                    -6f + ((i % 7) * 0.75f),
                    SymbiosisSimdMath.SinPolynomial7(angle) * ring);
                uint floraHash = ResolveFloraHash(lane);
                uint flags = ShinobuFloraFaunaSymbiosisSolver.FloraFlagActive | ResolveFloraFlags(lane);
                float feedingRadius = lane == 1 ? 4.0f : 5.0f;
                AbsoluteUniversePosition aup = ShinobuFloraFaunaSymbiosisSolver.OffsetAup(in centerAup, local);
                int3 sectorCoord = ShinobuFloraFaunaSymbiosisSolver.ResolveSectorCoord(in aup, 64f);
                SymbiosisFloraDTO floraDto = default;
                floraDto.LocalPosition = local;
                floraDto.Biomass = 4f + (lane * 0.7f);
                floraDto.FloraHash = floraHash;
                floraDto.ChemicalMask = 0u;
                floraDto.OxygenRate = lane == 2 ? 1.0f : 0.25f;
                floraDto.ToxicPotency = lane == 1 ? 1.0f : 0.05f;
                floraDto.CamouflageRadius = lane == 0 ? 2.0f : 0.5f;
                floraDto.FeedingRadius = feedingRadius;
                floraDto.Flags = flags;
                floraDto._pad0 = 0u;
                Flora[i] = floraDto;

                SymbiosisFloraAupDTO floraAupDto = default;
                floraAupDto.PositionAup = SymbiosisAup48.FromAup(in aup);
                floraAupDto.FloraHash = floraHash;
                floraAupDto.SectorHash = ShinobuFloraFaunaSymbiosisSolver.ResolveSectorHash(sectorCoord);
                floraAupDto.SpatialCellHash = 0;
                floraAupDto.StableSeed = ShinobuFloraFaunaSymbiosisSolver.MixHash(Seed ^ (uint)i ^ 0x464C4F52u);
                FloraAups[i] = floraAupDto;
            }

            MemClearTail(Flora, floraLimit);
            MemClearTail(FloraAups, floraLimit);

            WriteDefaultLinks(linkLimit);

            MemClearArray(MockBoids);
            if (MockBoids.IsCreated && MockBoids.Length > 0)
            {
                MockBoidArray mockBoids = default;
                mockBoids.StartIndex = 0;
                mockBoids.Count = fishLimit;
                mockBoids.StableSeed = Seed != 0u ? Seed : 1u;
                mockBoids.Flags = 1u;
                MockBoids[0] = mockBoids;
            }

            for (int i = 0; i < fishLimit; i++)
            {
                int targetFlora = floraLimit > 0 ? i % floraLimit : 0;
                AbsoluteUniversePosition baseAup = targetFlora < FloraAups.Length ? FloraAups[targetFlora].PositionAup.ToAup() : centerAup;
                float3 offset = math.float3(((i & 3) - 1.5f) * 0.75f, 0.2f, (((i >> 2) & 3) - 1.5f) * 0.75f);
                uint species = (i % 6) == 0
                    ? ShinobuFloraFaunaSymbiosisSolver.FaunaHashCarnivore
                    : ShinobuFloraFaunaSymbiosisSolver.FaunaHashHerbivore;
                AbsoluteUniversePosition fishAup = ShinobuFloraFaunaSymbiosisSolver.OffsetAup(in baseAup, offset);
                MockFishSymbiosisDTO mockFish = default;
                mockFish.PositionAup = SymbiosisAup48.FromAup(in fishAup);
                mockFish.Biomass = species == ShinobuFloraFaunaSymbiosisSolver.FaunaHashCarnivore ? 3.5f : 1.0f;
                mockFish.SpeciesHash = species;
                mockFish.Flags = ShinobuFloraFaunaSymbiosisSolver.FaunaFlagActive;
                mockFish.StableSeed = ShinobuFloraFaunaSymbiosisSolver.MixHash(Seed ^ (uint)i ^ 0x4D464953u);
                MockFish[i] = mockFish;
            }

            MemClearTail(MockFish, fishLimit);

            MemClearArray(Counters);
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
            MemClearArray(Links);

            if (linkLimit > 0) Links[0] = Link(ShinobuFloraFaunaSymbiosisSolver.FloraHashHealingKelp, ShinobuFloraFaunaSymbiosisSolver.FaunaHashHerbivore, 1.00f);
            if (linkLimit > 1) Links[1] = Link(ShinobuFloraFaunaSymbiosisSolver.FloraHashOxygenKelp, ShinobuFloraFaunaSymbiosisSolver.FaunaHashHerbivore, 0.75f);
            if (linkLimit > 2) Links[2] = Link(ShinobuFloraFaunaSymbiosisSolver.FloraHashGlowMoss, ShinobuFloraFaunaSymbiosisSolver.FaunaHashHerbivore, 0.60f);
            if (linkLimit > 3) Links[3] = Link(ShinobuFloraFaunaSymbiosisSolver.FloraHashSporeCoral, ShinobuFloraFaunaSymbiosisSolver.FaunaHashHerbivore, 0.35f);
            if (linkLimit > 4) Links[4] = Link(ShinobuFloraFaunaSymbiosisSolver.FloraHashToxicAnemone, ShinobuFloraFaunaSymbiosisSolver.FaunaHashCarnivore, 0.10f);
        }

        private static SymbiosisChemicalLinkDTO Link(uint floraHash, uint faunaHash, float rate)
        {
            SymbiosisChemicalLinkDTO dto = default;
            dto.FloraHash = floraHash;
            dto.FaunaHash = faunaHash;
            dto.ChemicalTransferRate = rate;
            dto.Flags = ShinobuFloraFaunaSymbiosisSolver.LinkFlagCompatible;
            return dto;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MemClearTail<T>(NativeArray<T> array, int activeCount)
            where T : unmanaged
        {
            if (!array.IsCreated)
                return;

            int start = math.clamp(activeCount, 0, array.Length);
            int clearLength = array.Length - start;
            if (clearLength <= 0)
                return;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(array);
            int stride = UnsafeUtility.SizeOf<T>();
            UnsafeUtility.MemClear(ptr + ((long)stride * start), (long)stride * clearLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MemClearArray<T>(NativeArray<T> array)
            where T : unmanaged
        {
            if (!array.IsCreated || array.Length <= 0)
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            int stride = UnsafeUtility.SizeOf<T>();
            UnsafeUtility.MemClear(ptr, (long)stride * array.Length);
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
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
        public SymbiosisTuningDTO Tuning;
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
        public int MicroExchangeThisFrame;

        public void Execute()
        {
            if (!Counters.IsCreated || Counters.Length <= 0)
                return;

            SymbiosisTuningDTO tuning = SymbiosisTuningDTO.Sanitize(Tuning);
            float q = math.saturate(tuning.GlobalQualityWeight);
            float qualityCurve = q * q * (3f - 2f * q);
            int floraCount = math.min(FloraCount, math.min(Flora.Length, FloraAups.Length));
            int linkCount = math.min(tuning.ActiveLinkCount > 0 ? tuning.ActiveLinkCount : Links.Length, Links.Length);
            SymbiosisCounterDTO counter = default;
            counter.Initialized = 1;
            counter.Frame = Frame;
            counter.Flags = tuning.Flags;

            AggregateOxygenAndSpores(ref counter, tuning, qualityCurve, floraCount);

            if (MicroExchangeThisFrame != 0)
                ApplyMicroExchange(ref counter, tuning, qualityCurve, floraCount, linkCount);
            else
                ApplyMacroAverage(ref counter, tuning, qualityCurve, floraCount);

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
            int floraStride = math.max(1, (int)math.round(math.lerp(16f, 1f, qualityCurve)));
            for (int i = 0; i < floraCount; i += floraStride)
            {
                SymbiosisFloraDTO flora = Flora[i];
                if ((flora.Flags & ShinobuFloraFaunaSymbiosisSolver.FloraFlagActive) == 0u)
                    continue;
                totalBiomass += math.max(0f, flora.Biomass);
                active++;
            }

            float avg = totalBiomass / math.max(1, active);
            const float truthCurve = 1f;
            float macroRate = avg * tuning.FeedingRate * SimulationTickDelta * math.lerp(0.02f, 0.18f, truthCurve);
            int stride = math.max(1, (int)math.round(math.lerp(16f, 2f, qualityCurve)));
            ProcessMacroMockFish(ref counter, macroRate, stride);

            float floraLoss = macroRate * 0.015f;
            for (int i = 0; i < floraCount; i += floraStride)
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
                        int bucket = ShinobuFloraFaunaSymbiosisSolver.ResolveBucket(baseCell + math.int3(x, y, z));
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
            float radiusSq = math.max(0.0001f, radius * radius);
            float atten = 1f - math.saturate(bestDistSq / radiusSq);
            float benefit0 = tuning.FeedingRate * bestRate * SimulationTickDelta * math.max(0.05f, atten);
            float transfer = math.min(best.Biomass, benefit0);
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
            float distSq = math.lengthsq(delta);
            float radiusSq = math.max(1f, field.Radius * field.Radius);
            return math.isfinite(distSq)
                ? math.saturate((1f - (distSq / radiusSq)) * field.CorruptionLevel)
                : 0f;
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
            float distSq = math.lengthsq(delta);
            float radiusSq = math.max(1f, field.Radius * field.Radius);
            return math.isfinite(distSq)
                ? math.saturate((1f - (distSq / radiusSq)) * field.CorruptionLevel)
                : 0f;
        }

        private void WriteExchange(ref SymbiosisCounterDTO counter, uint floraHash, uint faunaHash, float transfer)
        {
            int index = counter.ActiveExchanges;
            if (index >= Exchanges.Length)
            {
                counter.OverflowCount++;
                return;
            }

            SymbiosisExchangeDTO exchange = default;
            exchange.FloraHash = floraHash;
            exchange.FaunaHash = faunaHash;
            exchange.ChemicalTransferRate = transfer;
            exchange._pad0 = 0f;
            Exchanges[index] = exchange;
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

            ScannerVfxDTO scanner = default;
            scanner.HitLocal = flora.LocalPosition;
            scanner.HitDistance = SymbiosisSimdMath.LengthFromSq(math.lengthsq(delta));
            scanner.ScanProgress = math.saturate(toxin);
            scanner.TargetHash = flora.FloraHash;
            scanner.Flags = 1u;
            scanner.BeamScore = math.saturate(toxin);
            ScannerVfx[index] = scanner;
        }

        private void WriteOxygenEmitter(ref SymbiosisCounterDTO counter, SymbiosisFloraDTO flora, SymbiosisFloraAupDTO aup, SymbiosisTuningDTO tuning, float qualityCurve)
        {
            uint sectorHash = aup.SectorHash != 0u
                ? aup.SectorHash
                : ShinobuFloraFaunaSymbiosisSolver.ResolveSectorHash(ShinobuFloraFaunaSymbiosisSolver.ResolveSectorCoord(in aup.PositionAup, SectorSizeMeters));
            const float truthCurve = 1f;
            float oxygen = flora.Biomass * flora.OxygenRate * tuning.OxygenRateScale * math.lerp(0.25f, 1f, truthCurve);
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

            SymbiosisOxygenEmitterDTO emitter = default;
            emitter.LocalPosition = flora.LocalPosition;
            emitter.Oxygen01 = math.saturate(oxygen);
            emitter.SectorHash = sectorHash;
            emitter.RadiusMeters = 24f + (flora.Biomass * 2f);
            emitter.FloraHash = flora.FloraHash;
            emitter.Flags = 1u;
            OxygenEmitters[index] = emitter;
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

            AdherenceDTO adherence = default;
            adherence.LocalPosition = flora.LocalPosition;
            adherence.Growth01 = math.saturate((SubmarineIdleSeconds - 60f) * tuning.ParasiteGrowthSpeed);
            adherence.HostHash = 0x5355424Du;
            adherence.FloraHash = flora.FloraHash;
            adherence.Flags = 1u;
            adherence.Frame = Frame;
            Adherence[index] = adherence;
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
            float2 offset = math.float2(((hash & 255u) - 128) * (1f / 64f), (((hash >> 8) & 255u) - 128) * (1f / 64f));
            FloraSeedDTO seed = default;
            seed.LocalPosition = flora.LocalPosition + math.float3(offset.x, 0f, offset.y);
            seed.Viability01 = 0.75f;
            seed.FloraHash = flora.FloraHash;
            seed.CarrierHash = carrierHash;
            seed.Frame = Frame;
            seed.Flags = 1u;
            Seeds[index] = seed;
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

            SymbiosisAcousticTapDTO tap = default;
            tap.PositionAup = SymbiosisAup48.FromAup(in fishAup);
            tap.Magnitude01 = math.saturate(sampled * (1f / 32f));
            tap.RadiusMeters = 18f + sampled;
            tap.SourceHash = 0x53485250u;
            tap.Flags = 1u;
            AcousticTaps[index] = tap;
            counter.AcousticTapCount = index + 1;
        }
    }
}
