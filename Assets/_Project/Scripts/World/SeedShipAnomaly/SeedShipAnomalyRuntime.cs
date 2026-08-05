using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.World.SeedShipAnomaly
{
    [DisallowMultipleComponent]
    public sealed unsafe class SeedShipAnomalyRuntime : MonoBehaviour, IColdTickable, IUpdatable, ILateFrameTickable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private int _signalPushDropCount;
        private const SystemID OwnerSystem = SystemID.EndgameAnomaly;
#if UNITY_EDITOR
        private const int CsvMaxBytes = 8192;
#endif
        private const int DumpScratchBytes = 32 + SeedShipAnomalyConstants.TelemetryFrameCount * 64;
        private const int JobBatchSize = 64;
        private const float ComputeBudgetMs = 0.1f;
        private const float RadiationExportSlowTickSeconds = 0.1f;
        private const float RadiationDosePerSecondScale = 2.5f;
#if UNITY_EDITOR
        private const string CsvRelativePath = "anomaly_profiles.csv";
#endif
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SEED_SHIP_ANOMALY.bin";
        private const string LegacyEmissionFile = "seed_ship_emission_rates.h8bin";
        private const string LegacyGlitchFile = "glitch_zones_007.bin";
        private const ulong DumpMagic = 0x5345454453484950UL; // SEEDSHIP
        private const uint DumpVersion = 1u;

        private const uint JobPinField = 1u << 0;
        private const uint JobPinTuning = 1u << 1;
        private const uint JobPinGlobals = 1u << 2;
        private const uint JobPinGlitchCommand = 1u << 3;
        private const uint JobPinMockHudSignals = 1u << 4;
        private const uint JobPinMockLeviathans = 1u << 5;
        private const uint JobPinMockAupRebase = 1u << 6;
        private const uint JobPinThermoSource = 1u << 7;
        private const uint JobPinTelemetryRing = 1u << 8;
        private static readonly ulong CsvApplyMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuSeedShipAnomalyTuning) |
            MutationGuardBit(BufferID.ShinobuSeedShipAnomalyField) |
            MutationGuardBit(BufferID.ShinobuSeedShipAnomalyCsvOverrides);

        private static readonly uint _MaxCorruptionRadiusHash = HashLowerAsciiString("max_corruption_radius");
        private static readonly uint _GravityInversionStrengthHash = HashLowerAsciiString("gravity_inversion_strength");
        private static readonly uint _PulseFrequencyHash = HashLowerAsciiString("pulse_frequency");
        private static readonly uint _GlitchIntensityHash = HashLowerAsciiString("glitch_intensity");
        private static readonly uint _HeatEmissionHash = HashLowerAsciiString("heat_emission");
        private static readonly uint _RadiationEmissionHash = HashLowerAsciiString("radiation_emission");
        private static readonly uint _RadarJamIntensityHash = HashLowerAsciiString("radar_jam_intensity");
        private static readonly uint _BabelScrambleStrengthHash = HashLowerAsciiString("babel_scramble_strength");
        private static readonly uint _GlobalQualityWeightHash = HashLowerAsciiString("global_quality_weight");
        private static readonly System.Threading.WaitCallback TelemetryDumpWorkerCallback = RunTelemetryDumpWorker;

        [Header("Seed Ship AUP")]
        [SerializeField] private double seedShipAupX;
        [SerializeField] private double seedShipAupY = SeedShipAnomalyConstants.DefaultSeedShipDepthMeters;
        [SerializeField] private double seedShipAupZ;

        [Header("Runtime Capacity")]
        [SerializeField, Min(1)] private int mockLeviathanCapacity = SeedShipAnomalyConstants.DefaultMockLeviathanCapacity;
        [SerializeField, Range(0f, 1f)] private float defaultGlobalQualityWeight = 1f;

        private VaultGenerationHandle<AnomalyFieldDTO> _fieldHandle;
        private VaultGenerationHandle<AnomalyTuningDTO> _tuningHandle;
        private VaultGenerationHandle<AnomalyGlobalScalarsDTO> _globalsHandle;
        private VaultGenerationHandle<GlitchCommandDTO> _glitchHandle;
        private VaultGenerationHandle<MockHudSignal> _hudHandle;
        private VaultGenerationHandle<MockLeviathanState> _leviathanHandle;
        private VaultGenerationHandle<MockAupRebaseSignal> _rebaseHandle;
        private VaultGenerationHandle<AnomalyThermoSourceDTO> _thermoHandle;
        private VaultGenerationHandle<AnomalyTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<AnomalyCsvOverrideDTO> _csvOverrideHandle;
        private VaultGenerationHandle<ScalabilityStateDTO> _scalabilityHandle;

        private IDataVault _dataVault;
        private IPlayerRuntimeContext _playerContext;
        private JobHandle _activeJobHandle;
        private string _projectRoot;
#if UNITY_EDITOR
        private string _csvPath;
#endif
        private string _dumpPath;
        private readonly byte[] _telemetryDumpScratch = new byte[DumpScratchBytes];
#if UNITY_EDITOR
        private long _csvLastWriteTicks;
#endif
        private long _jobStartTimestamp;
        private int _telemetryCursor;
        private int _scheduledEntityBudget;
        private float _localTimeSeconds;
        private float _healingSecondsRemaining;
        private uint _simulationFrameCounter;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredSlowTick;
        private bool _registeredColdTick;
        private bool _registeredHotSwap;
        private bool _vaultRepairRequested;
        private bool _radiationExportRequested;
        private bool _radiationSourceActive;
        private bool _jobScheduled;
        private bool _jobLocksHeld;
        private IDataVault _jobPinVault;
        private uint _jobPinMask;
        private bool _defaultsInitialized;
        private bool _dumpedBudgetBreach;
        private int _telemetryDumpInFlight;
        private int _telemetryDumpByteCount;
        private bool _legacyReconComplete;
        private uint _legacyReconFlags;

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void Awake()
        {
            mockLeviathanCapacity = math.max(1, mockLeviathanCapacity);
            _projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
#if UNITY_EDITOR
            _csvPath = Path.GetFullPath(Path.Combine(_projectRoot, CsvRelativePath));
#endif
            _dumpPath = DumpRelativePath;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            EnsureSignalLanesReady();
            TryRegisterHotSwapListener();
            TryRegisterColdTick();
            RebindColdServices();
            PrepareRuntimeStateCold();
            if (HasVaultStateReady())
                TryRegisterTicks();
        }

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            RebindColdServices();
            TryRegisterColdTick();
            PrepareRuntimeStateCold();
            if (HasVaultStateReady())
                TryRegisterTicks();
        }

        private void OnDisable()
        {
            CompleteFrameJobForTeardown();
            TryUnregisterTicks();
            TryUnregisterHotSwapListener();
            UnlockJobBuffers();
            ReleaseSeedShipVaultHandles(_dataVault);
            ClearCachedHandles();
            _defaultsInitialized = false;
            _legacyReconComplete = false;
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                CompleteFrameJobForTeardown();
                UnlockJobBuffers();
                ReleaseSeedShipVaultHandles(_dataVault ?? previousService as IDataVault);
                _dataVault = currentService as IDataVault;
                ClearCachedHandles();
                _defaultsInitialized = false;
                _legacyReconComplete = false;
                _vaultRepairRequested = true;
                TryRegisterColdTick();
                PrepareRuntimeStateCold();
                if (HasVaultStateReady())
                    TryRegisterTicks();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                _playerContext = currentService as IPlayerRuntimeContext;
        }

        public void Tick(float deltaTime)
        {
            if (_jobScheduled)
                return;

            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultStateReady())
            {
                _vaultRepairRequested = true;
                return;
            }

            float dt = math.clamp(deltaTime, 0.0001f, 0.05f);
            _localTimeSeconds += dt;
            _simulationFrameCounter++;
            ConsumeHackSignals(dt);

            if (!TryLockJobBuffers(vault))
                return;

            bool keepJobPins = false;
            try
            {
                if (!TryResolveBuffers(
                        vault,
                        out NativeArray<AnomalyFieldDTO> field,
                        out NativeArray<AnomalyTuningDTO> tuningArray,
                        out NativeArray<AnomalyGlobalScalarsDTO> globals,
                        out NativeArray<GlitchCommandDTO> glitch,
                        out NativeArray<MockHudSignal> hud,
                        out NativeArray<MockLeviathanState> leviathans,
                        out NativeArray<MockAupRebaseSignal> rebase,
                        out NativeArray<AnomalyThermoSourceDTO> thermo,
                        out NativeArray<AnomalyTelemetryEntry> telemetry))
                {
                    return;
                }

                AnomalyTuningDTO tuning = SeedShipAnomalyMath.SanitizeTuning(tuningArray[0]);
                tuning.GlobalQualityWeight = ResolveGlobalQualityWeight(vault, tuning.GlobalQualityWeight);
                tuningArray[0] = tuning;

                int entityBudget = SeedShipAnomalyMath.ResolveEntityBudget(
                    leviathans.Length,
                    tuning.GlobalQualityWeight,
                    globals[0].Corruption01,
                    tuning.MinEntityBudget,
                    tuning.MaxEntityBudget);
                _scheduledEntityBudget = entityBudget;

                double3 playerAup = ResolvePlayerAup();
                uint frame = _simulationFrameCounter;
                uint sectorHash = SeedShipAnomalyMath.HashAupSector(playerAup);
                _jobStartTimestamp = Stopwatch.GetTimestamp();

                JobHandle handle = new SeedShipMockAupRebaseJob
                {
                    RebaseSignals = rebase,
                    Frame = frame,
                    Seed = SeedShipAnomalyConstants.SourceHash,
                    SectorHash = sectorHash,
                    Chance01 = tuning.MockRebaseChance01
                }.Schedule();

                handle = new SeedShipAnomalyFieldJob
                {
                    Field = field,
                    Tuning = tuningArray,
                    Globals = globals,
                    GlitchCommands = glitch,
                    HudSignals = hud,
                    ThermoSources = thermo,
                    RebaseSignals = rebase,
                    Telemetry = telemetry,
                    RadarJamWriter = SignalBus<RadarJamSignal>.ParallelWriter,
                    RadarJamWriterBudget = SignalBus<RadarJamSignal>.ParallelWriterBudget,
                    PlayerAUP = playerAup,
                    DeltaSeconds = dt,
                    TimeSeconds = _localTimeSeconds,
                    HackHealingSeconds = _healingSecondsRemaining,
                    TelemetryCursor = _telemetryCursor,
                    EntityBudget = entityBudget,
                    Frame = frame,
                    EmitRadarSignal = 1
                }.Schedule(handle);

                if (entityBudget > 0)
                {
                    handle = new SeedShipLeviathanFrenzyJob
                    {
                        Field = field,
                        Tuning = tuningArray,
                        Leviathans = leviathans,
                        Frame = frame
                    }.Schedule(entityBudget, JobBatchSize, handle);
                }

                _activeJobHandle = handle;
                _jobScheduled = true;
                H8Memory.RegisterActiveJob(OwnerSystem, _activeJobHandle);
                keepJobPins = true;
            }
            finally
            {
                if (!keepJobPins)
                    UnlockJobBuffers();
            }
        }

        public void LateFrameTick()
        {
            TryFinalizeFrameJobNoWait();
        }

        public void SlowTick()
        {
            _radiationExportRequested = true;
            IDataVault vault = _dataVault;
            if (vault == null || _jobScheduled || !HasVaultStateReady())
            {
                _vaultRepairRequested = true;
                return;
            }

#if UNITY_EDITOR
            MonitorCsvOverrides(vault);
#endif
        }

        public void ColdTick()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
                return;

            if (!_vaultRepairRequested && HasVaultStateReady())
                return;

            PrepareRuntimeStateCold();
            if (HasVaultStateReady())
                TryRegisterTicks();
        }

        public ref AnomalyFieldDTO GetAnomalyFieldRef()
        {
            IDataVault vault = _dataVault;
            if (!TryReadSeedShipVaultBuffer(vault, in _fieldHandle, BufferID.ShinobuSeedShipAnomalyField, 1, out NativeArray<AnomalyFieldDTO> field))
                FatalMemoryException.ThrowStaleVaultHandle();

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(field);
            return ref UnsafeUtility.ArrayElementAsRef<AnomalyFieldDTO>(ptr, 0);
        }

        public bool TryGetField(out AnomalyFieldDTO field)
        {
            field = default;
            IDataVault vault = _dataVault;
            if (!TryReadSeedShipVaultBuffer(vault, in _fieldHandle, BufferID.ShinobuSeedShipAnomalyField, 1, out NativeArray<AnomalyFieldDTO> array))
                return false;

            field = array[0];
            return true;
        }

        public bool TryGetGlobals(out AnomalyGlobalScalarsDTO globals)
        {
            globals = default;
            IDataVault vault = _dataVault;
            if (!TryReadSeedShipVaultBuffer(vault, in _globalsHandle, BufferID.ShinobuSeedShipAnomalyGlobals, 1, out NativeArray<AnomalyGlobalScalarsDTO> array))
                return false;

            globals = array[0];
            return true;
        }

        public bool TryGetTuning(out AnomalyTuningDTO tuning)
        {
            tuning = default;
            IDataVault vault = _dataVault;
            if (!TryReadSeedShipVaultBuffer(vault, in _tuningHandle, BufferID.ShinobuSeedShipAnomalyTuning, 1, out NativeArray<AnomalyTuningDTO> array))
                return false;

            tuning = array[0];
            return true;
        }

        public bool SetEditorTuning(AnomalyTuningDTO tuning)
        {
            IDataVault vault = _dataVault;
            if (!EnsureVaultState() ||
                !TryResolveSeedShipVaultBuffer(vault, ref _tuningHandle, BufferID.ShinobuSeedShipAnomalyTuning, 1, out NativeArray<AnomalyTuningDTO> array))
            {
                return false;
            }

            array[0] = SeedShipAnomalyMath.SanitizeTuning(tuning);
            return true;
        }

        public bool SetEditorField(AnomalyFieldDTO field)
        {
            IDataVault vault = _dataVault;
            if (!EnsureVaultState() ||
                !TryResolveSeedShipVaultBuffer(vault, ref _fieldHandle, BufferID.ShinobuSeedShipAnomalyField, 1, out NativeArray<AnomalyFieldDTO> array))
            {
                return false;
            }

            field.Radius = math.max(0f, field.Radius);
            field.CorruptionLevel = math.saturate(field.CorruptionLevel);
            field.GlitchHash = field.GlitchHash != 0u ? field.GlitchHash : SeedShipAnomalyConstants.GlitchHash;
            array[0] = field;
            return true;
        }

        /// <summary>
        /// Publishes a core-hack result onto the <see cref="CoreHackedSignal"/> lane.
        /// </summary>
        /// <remarks>
        /// The two <c>UnityEngine.Assertions.Assert.IsTrue</c> calls that used to open this method THREW - nothing
        /// under Assets sets <c>Assert.raiseExceptions = false</c>. This is a public entry point on a component
        /// registered as IColdTickable, IUpdatable, ILateFrameTickable AND ISlowTickable, so a throw here would
        /// have escaped into whatever endgame caller invoked it rather than being contained.
        ///
        /// The two arguments are NOT the same class of problem, so they are not treated the same way:
        ///
        /// - <c>validity01</c> had a reachable fallback already inline: the payload assignment below sanitized it
        ///   with <c>math.saturate</c>, so the assert only converted an already-handled out-of-range value into a
        ///   crash. It is now clamped and reported instead. Non-finite input is also handled, which
        ///   <c>math.saturate</c> alone does not do reliably - NaN would have propagated into the DTO.
        /// - <c>codeHash == 0</c> has NO safe continuation. Zero is the sentinel for "no code" across the hash
        ///   lanes, so pushing it would put a permanently unmatchable CoreHackedSignal into the bus and corrupt
        ///   the endgame hack state. That check is kept as a hard reject with a loud error and an early return,
        ///   per the non-throwing-guard rule.
        ///
        /// Both reports use literal strings through the Conditional-stripped H8Debug facade, so there is no
        /// allocation and no per-call string work; they are intentionally NOT latched, because repeated caller
        /// misuse of a public API must stay visible.
        /// </remarks>
        public void InjectCoreHack(uint codeHash, float validity01)
        {
            AssertValidCoreHackCodeHash(codeHash);
            if (codeHash == 0u)
            {
                return;
            }

            float sanitizedValidity01 = math.isfinite(validity01) ? math.saturate(validity01) : 0f;
            if (sanitizedValidity01 != validity01)
                LogClampedCoreHackValidity();

            SignalBus<CoreHackedSignal>.TryPushTracked(new CoreHackedSignal
            {
                Frame = _simulationFrameCounter,
                SourceHash = SeedShipAnomalyConstants.SourceHash,
                CodeHash = codeHash,
                Validity01 = sanitizedValidity01,
                Flags = 1
            }, ref _signalPushDropCount);
        }

        /// <summary>
        /// Reports a rejected zero <c>codeHash</c>. Literal message, so no allocation on any cadence.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void AssertValidCoreHackCodeHash(uint codeHash)
        {
            if (codeHash == 0u)
            {
                throw new System.ArgumentException("SeedShipAnomalyRuntime.InjectCoreHack: codeHash was 0, which is the 'no code' sentinel across the hash lanes. The CoreHackedSignal was REJECTED rather than published, because a zero code hash can never be matched by any consumer and would leave the endgame hack state permanently ambiguous. Pass the real authored code hash of the hacked core.");
            }
        }

        /// <summary>
        /// Reports a clamped or non-finite <c>validity01</c>. Literal message, so no allocation on any cadence.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogClampedCoreHackValidity()
        {
            Hecton8.Core.H8Debug.LogWarning("SeedShipAnomalyRuntime.InjectCoreHack: validity01 was outside the 0..1 contract or non-finite. It has been clamped (non-finite becomes 0) and the CoreHackedSignal was still published, matching the math.saturate this method already applied. Fix the caller to pass a normalized 0..1 validity.");
        }

        private void RebindColdServices()
        {
            IDataVault currentVault = GlobalRegistry.DataVault;
            if (_dataVault != null && !ReferenceEquals(_dataVault, currentVault))
            {
                CompleteFrameJobForTeardown();
                UnlockJobBuffers();
                ReleaseSeedShipVaultHandles(_dataVault);
                ClearCachedHandles();
                _defaultsInitialized = false;
                _legacyReconComplete = false;
            }

            _dataVault = currentVault;
            _playerContext = GlobalRegistry.Player;
        }

        private bool EnsureVaultState()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            mockLeviathanCapacity = math.max(1, mockLeviathanCapacity);
            if (HandlesReady(vault))
                return true;

            if (!EnsureSeedShipVaultBuffer(vault, ref _fieldHandle, BufferID.ShinobuSeedShipAnomalyField, 1, NativeArrayOptions.UninitializedMemory, out _) ||
                !EnsureSeedShipVaultBuffer(vault, ref _tuningHandle, BufferID.ShinobuSeedShipAnomalyTuning, 1, NativeArrayOptions.UninitializedMemory, out _) ||
                !EnsureSeedShipVaultBuffer(vault, ref _globalsHandle, BufferID.ShinobuSeedShipAnomalyGlobals, 1, NativeArrayOptions.UninitializedMemory, out _) ||
                !EnsureSeedShipVaultBuffer(vault, ref _glitchHandle, BufferID.ShinobuSeedShipAnomalyGlitchCommand, 1, NativeArrayOptions.UninitializedMemory, out _) ||
                !EnsureSeedShipVaultBuffer(vault, ref _hudHandle, BufferID.ShinobuSeedShipAnomalyMockHudSignals, 1, NativeArrayOptions.UninitializedMemory, out _) ||
                !EnsureSeedShipVaultBuffer(vault, ref _leviathanHandle, BufferID.ShinobuSeedShipAnomalyMockLeviathans, mockLeviathanCapacity, NativeArrayOptions.UninitializedMemory, out _) ||
                !EnsureSeedShipVaultBuffer(vault, ref _rebaseHandle, BufferID.ShinobuSeedShipAnomalyMockAupRebase, 1, NativeArrayOptions.UninitializedMemory, out _) ||
                !EnsureSeedShipVaultBuffer(vault, ref _thermoHandle, BufferID.ShinobuSeedShipAnomalyThermoSource, 1, NativeArrayOptions.UninitializedMemory, out _) ||
                !EnsureSeedShipVaultBuffer(vault, ref _telemetryHandle, BufferID.ShinobuSeedShipAnomalyTelemetryRing, SeedShipAnomalyConstants.TelemetryFrameCount, NativeArrayOptions.UninitializedMemory, out _) ||
                !EnsureSeedShipVaultBuffer(vault, ref _csvOverrideHandle, BufferID.ShinobuSeedShipAnomalyCsvOverrides, SeedShipAnomalyConstants.CsvOverrideCapacity, NativeArrayOptions.UninitializedMemory, out _))
            {
                return false;
            }

            InitializeDefaults(vault);
            return true;
        }

        private void PrepareRuntimeStateCold()
        {
            _vaultRepairRequested = !EnsureVaultState();
        }

        private bool HasVaultStateReady()
        {
            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   _defaultsInitialized &&
                   HandlesReady(vault);
        }

        private bool HandlesReady(IDataVault vault)
        {
            return HasSeedShipVaultBuffer(vault, in _fieldHandle, BufferID.ShinobuSeedShipAnomalyField, 1) &&
                   HasSeedShipVaultBuffer(vault, in _tuningHandle, BufferID.ShinobuSeedShipAnomalyTuning, 1) &&
                   HasSeedShipVaultBuffer(vault, in _globalsHandle, BufferID.ShinobuSeedShipAnomalyGlobals, 1) &&
                   HasSeedShipVaultBuffer(vault, in _glitchHandle, BufferID.ShinobuSeedShipAnomalyGlitchCommand, 1) &&
                   HasSeedShipVaultBuffer(vault, in _hudHandle, BufferID.ShinobuSeedShipAnomalyMockHudSignals, 1) &&
                   HasSeedShipVaultBuffer(vault, in _leviathanHandle, BufferID.ShinobuSeedShipAnomalyMockLeviathans, mockLeviathanCapacity) &&
                   HasSeedShipVaultBuffer(vault, in _rebaseHandle, BufferID.ShinobuSeedShipAnomalyMockAupRebase, 1) &&
                   HasSeedShipVaultBuffer(vault, in _thermoHandle, BufferID.ShinobuSeedShipAnomalyThermoSource, 1) &&
                   HasSeedShipVaultBuffer(vault, in _telemetryHandle, BufferID.ShinobuSeedShipAnomalyTelemetryRing, SeedShipAnomalyConstants.TelemetryFrameCount) &&
                   HasSeedShipVaultBuffer(vault, in _csvOverrideHandle, BufferID.ShinobuSeedShipAnomalyCsvOverrides, SeedShipAnomalyConstants.CsvOverrideCapacity);
        }

        private void InitializeDefaults(IDataVault vault)
        {
            if (_defaultsInitialized)
                return;

            if (!TryResolveBuffers(
                    vault,
                    out NativeArray<AnomalyFieldDTO> field,
                    out NativeArray<AnomalyTuningDTO> tuning,
                    out NativeArray<AnomalyGlobalScalarsDTO> globals,
                    out NativeArray<GlitchCommandDTO> glitch,
                    out NativeArray<MockHudSignal> hud,
                    out NativeArray<MockLeviathanState> leviathans,
                    out NativeArray<MockAupRebaseSignal> rebase,
                    out NativeArray<AnomalyThermoSourceDTO> thermo,
                    out NativeArray<AnomalyTelemetryEntry> telemetry))
            {
                return;
            }

            if (!TryResolveSeedShipVaultBuffer(
                    vault,
                    ref _csvOverrideHandle,
                    BufferID.ShinobuSeedShipAnomalyCsvOverrides,
                    SeedShipAnomalyConstants.CsvOverrideCapacity,
                    out NativeArray<AnomalyCsvOverrideDTO> csvOverrides))
            {
                return;
            }

            GenerateEmergencyMockAnomalies(field, tuning, globals, glitch, hud, leviathans, rebase, thermo, telemetry, csvOverrides);
            TryLoadLegacyAnomalyTables(vault, field, tuning);
            _defaultsInitialized = true;
        }

        private void GenerateEmergencyMockAnomalies(
            NativeArray<AnomalyFieldDTO> field,
            NativeArray<AnomalyTuningDTO> tuning,
            NativeArray<AnomalyGlobalScalarsDTO> globals,
            NativeArray<GlitchCommandDTO> glitch,
            NativeArray<MockHudSignal> hud,
            NativeArray<MockLeviathanState> leviathans,
            NativeArray<MockAupRebaseSignal> rebase,
            NativeArray<AnomalyThermoSourceDTO> thermo,
            NativeArray<AnomalyTelemetryEntry> telemetry,
            NativeArray<AnomalyCsvOverrideDTO> csvOverrides)
        {
            double3 epicenter = new double3(seedShipAupX, seedShipAupY, seedShipAupZ);
            AnomalyFieldDTO fieldDto = new AnomalyFieldDTO
            {
                EpicenterAUP = epicenter,
                Radius = SeedShipAnomalyConstants.DefaultRadiusMeters,
                CorruptionLevel = 0f,
                GlitchHash = SeedShipAnomalyConstants.GlitchHash,
                _pad0 = 0u,
                _pad1 = 0UL
            };

            AnomalyTuningDTO tuningDto = SeedShipAnomalyMath.SanitizeTuning(new AnomalyTuningDTO
            {
                MaxCorruptionRadius = SeedShipAnomalyConstants.DefaultRadiusMeters,
                GravityInversionStrength = 1f,
                PulseFrequency = 1.7f,
                GlitchIntensity = 0.85f,
                HeatEmission = 0.9f,
                RadiationEmission = 0.7f,
                RadarJamIntensity = 0.8f,
                BabelScrambleStrength = 0.65f,
                GlobalQualityWeight = math.saturate(defaultGlobalQualityWeight),
                MinEntityBudget = 0,
                MaxEntityBudget = mockLeviathanCapacity,
                ShaderNoiseStrength = 0.75f,
                HealingRateScalar = 1f,
                MockRebaseChance01 = 0.015f
            });

            field[0] = fieldDto;
            tuning[0] = tuningDto;
            globals[0] = new AnomalyGlobalScalarsDTO
            {
                GravityY = 9.80665f,
                GlobalQualityWeight = tuningDto.GlobalQualityWeight,
                RadiusMeters = fieldDto.Radius
            };
            glitch[0] = new GlitchCommandDTO { GlyphHash = SeedShipAnomalyConstants.GlitchHash };
            hud[0] = default;
            rebase[0] = default;
            thermo[0] = new AnomalyThermoSourceDTO { EpicenterAUP = epicenter, RadiusMeters = fieldDto.Radius };

            for (int i = 0; i < telemetry.Length; i++)
                telemetry[i] = default;
            for (int i = 0; i < csvOverrides.Length; i++)
                csvOverrides[i] = default;

            Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(0xA48D1E55u);
            for (int i = 0; i < leviathans.Length; i++)
            {
                float angle = random.NextFloat(0f, math.PI * 2f);
                float radius = random.NextFloat(250f, 250f + fieldDto.Radius);
                float y = random.NextFloat(-850f, 850f);
                MathLodApproximation.ApproxSinCosBhaskara(angle, out float sin, out float cos);
                double3 offset = new double3(cos * radius, y, sin * radius);
                leviathans[i] = new MockLeviathanState
                {
                    AUP = epicenter + offset,
                    AggressionWeight = 0.1f,
                    LightAversion = 1f,
                    EntityId = unchecked((uint)(0x4C455600u + i)),
                    LastDistanceMeters = radius
                };
            }
        }

        private bool TryLoadLegacyAnomalyTables(IDataVault vault, NativeArray<AnomalyFieldDTO> field, NativeArray<AnomalyTuningDTO> tuning)
        {
            if (_legacyReconComplete)
                return (_legacyReconFlags & 1u) != 0u;

            _legacyReconComplete = true;
            try
            {
                if (TryFindLegacyFile(LegacyEmissionFile, out string emissionPath) &&
                    TryReadLegacyEmission(emissionPath, out float radius, out float heat, out float radiation))
                {
                    AnomalyFieldDTO currentField = field[0];
                    currentField.Radius = math.clamp(radius, 1f, 12000f);
                    field[0] = currentField;

                    AnomalyTuningDTO currentTuning = tuning[0];
                    currentTuning.MaxCorruptionRadius = currentField.Radius;
                    currentTuning.HeatEmission = math.saturate(heat);
                    currentTuning.RadiationEmission = math.saturate(radiation);
                    tuning[0] = SeedShipAnomalyMath.SanitizeTuning(currentTuning);
                    _legacyReconFlags |= 1u;
                }

                if (TryFindLegacyFile(LegacyGlitchFile, out string glitchPath) &&
                    TryReadLegacyGlitch(glitchPath, out uint glitchHash, out float intensity))
                {
                    AnomalyFieldDTO currentField = field[0];
                    currentField.GlitchHash = glitchHash != 0u ? glitchHash : SeedShipAnomalyConstants.GlitchHash;
                    field[0] = currentField;

                    AnomalyTuningDTO currentTuning = tuning[0];
                    currentTuning.GlitchIntensity = math.saturate(intensity);
                    tuning[0] = SeedShipAnomalyMath.SanitizeTuning(currentTuning);
                    _legacyReconFlags |= 2u;
                }
            }
            catch (Exception)
            {
                _legacyReconFlags |= 4u;
            }

            return (_legacyReconFlags & 1u) != 0u;
        }

        private bool TryFindLegacyFile(string fileName, out string path)
        {
            path = null;
            if (TryFindLegacyFileInRoot(Path.Combine(_projectRoot, "Docs", "Archive"), fileName, out path))
                return true;

            string streamingRoot = Application.streamingAssetsPath;
            return !string.IsNullOrEmpty(streamingRoot) && TryFindLegacyFileInRoot(streamingRoot, fileName, out path);
        }

        private static bool TryFindLegacyFileInRoot(string root, string fileName, out string path)
        {
            path = null;
            try
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    return false;

                string[] files = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                if (files.Length == 0)
                    return false;

                path = files[0];
                return true;
            }
            catch (Exception)
            {
                path = null;
                return false;
            }
        }

        private static bool TryReadLegacyEmission(string path, out float radius, out float heat, out float radiation)
        {
            radius = SeedShipAnomalyConstants.DefaultRadiusMeters;
            heat = 0.9f;
            radiation = 0.7f;
            Span<byte> scratch = stackalloc byte[16];

            try
            {
                int read = ReadColdBytes(path, scratch);
                if (read < 12)
                    return false;

                radius = ReadFloatLittleEndian(scratch, 0);
                heat = ReadFloatLittleEndian(scratch, 4);
                radiation = ReadFloatLittleEndian(scratch, 8);
                return math.isfinite(radius) && math.isfinite(heat) && math.isfinite(radiation);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryReadLegacyGlitch(string path, out uint glitchHash, out float intensity)
        {
            glitchHash = SeedShipAnomalyConstants.GlitchHash;
            intensity = 0.85f;
            Span<byte> scratch = stackalloc byte[16];

            try
            {
                int read = ReadColdBytes(path, scratch);
                if (read < 8)
                    return false;

                glitchHash = ReadUInt32LittleEndian(scratch, 0);
                intensity = ReadFloatLittleEndian(scratch, 4);
                return math.isfinite(intensity);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static int ReadColdBytes(string path, Span<byte> scratch)
        {
            if (scratch.Length == 0)
                return 0;

            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return stream.Read(scratch);
        }

        private bool TryResolveBuffers(
            IDataVault vault,
            out NativeArray<AnomalyFieldDTO> field,
            out NativeArray<AnomalyTuningDTO> tuning,
            out NativeArray<AnomalyGlobalScalarsDTO> globals,
            out NativeArray<GlitchCommandDTO> glitch,
            out NativeArray<MockHudSignal> hud,
            out NativeArray<MockLeviathanState> leviathans,
            out NativeArray<MockAupRebaseSignal> rebase,
            out NativeArray<AnomalyThermoSourceDTO> thermo,
            out NativeArray<AnomalyTelemetryEntry> telemetry)
        {
            field = default;
            tuning = default;
            globals = default;
            glitch = default;
            hud = default;
            leviathans = default;
            rebase = default;
            thermo = default;
            telemetry = default;

            return TryResolveSeedShipVaultBuffer(vault, ref _fieldHandle, BufferID.ShinobuSeedShipAnomalyField, 1, out field) &&
                   TryResolveSeedShipVaultBuffer(vault, ref _tuningHandle, BufferID.ShinobuSeedShipAnomalyTuning, 1, out tuning) &&
                   TryResolveSeedShipVaultBuffer(vault, ref _globalsHandle, BufferID.ShinobuSeedShipAnomalyGlobals, 1, out globals) &&
                   TryResolveSeedShipVaultBuffer(vault, ref _glitchHandle, BufferID.ShinobuSeedShipAnomalyGlitchCommand, 1, out glitch) &&
                   TryResolveSeedShipVaultBuffer(vault, ref _hudHandle, BufferID.ShinobuSeedShipAnomalyMockHudSignals, 1, out hud) &&
                   TryResolveSeedShipVaultBuffer(vault, ref _leviathanHandle, BufferID.ShinobuSeedShipAnomalyMockLeviathans, mockLeviathanCapacity, out leviathans) &&
                   TryResolveSeedShipVaultBuffer(vault, ref _rebaseHandle, BufferID.ShinobuSeedShipAnomalyMockAupRebase, 1, out rebase) &&
                   TryResolveSeedShipVaultBuffer(vault, ref _thermoHandle, BufferID.ShinobuSeedShipAnomalyThermoSource, 1, out thermo) &&
                   TryResolveSeedShipVaultBuffer(vault, ref _telemetryHandle, BufferID.ShinobuSeedShipAnomalyTelemetryRing, SeedShipAnomalyConstants.TelemetryFrameCount, out telemetry);
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            if (_jobLocksHeld)
                return true;

            if (vault == null)
                return false;

            _jobPinVault = vault;
            try
            {
                if (!TryLockJobBuffer(vault, BufferID.ShinobuSeedShipAnomalyField, JobPinField) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuSeedShipAnomalyTuning, JobPinTuning) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuSeedShipAnomalyGlobals, JobPinGlobals) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuSeedShipAnomalyGlitchCommand, JobPinGlitchCommand) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuSeedShipAnomalyMockHudSignals, JobPinMockHudSignals) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuSeedShipAnomalyMockLeviathans, JobPinMockLeviathans) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuSeedShipAnomalyMockAupRebase, JobPinMockAupRebase) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuSeedShipAnomalyThermoSource, JobPinThermoSource) ||
                    !TryLockJobBuffer(vault, BufferID.ShinobuSeedShipAnomalyTelemetryRing, JobPinTelemetryRing))
                    return false;

                _jobLocksHeld = true;
                return true;
            }
            finally
            {
                if (!_jobLocksHeld)
                    UnlockJobBuffers();
            }
        }

        private void UnlockJobBuffers()
        {
            IDataVault vault = _jobPinVault;
            uint pinMask = _jobPinMask;
            _jobPinVault = null;
            _jobPinMask = 0u;
            _jobLocksHeld = false;

            if (vault == null || pinMask == 0u)
                return;

            TryUnlockJobBuffer(vault, pinMask, JobPinTelemetryRing, BufferID.ShinobuSeedShipAnomalyTelemetryRing);
            TryUnlockJobBuffer(vault, pinMask, JobPinThermoSource, BufferID.ShinobuSeedShipAnomalyThermoSource);
            TryUnlockJobBuffer(vault, pinMask, JobPinMockAupRebase, BufferID.ShinobuSeedShipAnomalyMockAupRebase);
            TryUnlockJobBuffer(vault, pinMask, JobPinMockLeviathans, BufferID.ShinobuSeedShipAnomalyMockLeviathans);
            TryUnlockJobBuffer(vault, pinMask, JobPinMockHudSignals, BufferID.ShinobuSeedShipAnomalyMockHudSignals);
            TryUnlockJobBuffer(vault, pinMask, JobPinGlitchCommand, BufferID.ShinobuSeedShipAnomalyGlitchCommand);
            TryUnlockJobBuffer(vault, pinMask, JobPinGlobals, BufferID.ShinobuSeedShipAnomalyGlobals);
            TryUnlockJobBuffer(vault, pinMask, JobPinTuning, BufferID.ShinobuSeedShipAnomalyTuning);
            TryUnlockJobBuffer(vault, pinMask, JobPinField, BufferID.ShinobuSeedShipAnomalyField);
        }

        private bool TryLockJobBuffer(IDataVault vault, BufferID bufferId, uint pinBit)
        {
            if ((_jobPinMask & pinBit) != 0u)
                return true;

            if (vault == null || !vault.TryLockBuffer(bufferId, OwnerSystem))
                return false;

            _jobPinMask |= pinBit;
            return true;
        }

        private static void TryUnlockJobBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, OwnerSystem);
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

            if (!CompleteFrameJobForTeardownInPostSimulationWindow())
                return;

            FinishFrameJobCompletion();
        }

        private bool CompleteFrameJobForTeardownInPostSimulationWindow()
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

        private void FinishFrameJobCompletion()
        {
            float elapsedMs = (float)((Stopwatch.GetTimestamp() - _jobStartTimestamp) * 1000.0 / Stopwatch.Frequency);
            IDataVault vault = _dataVault;
            bool hasSnapshot = false;
            bool shouldDump = false;
            uint diagnosticFlags = 0u;
            AnomalyFieldDTO fieldSnapshot = default;
            AnomalyGlobalScalarsDTO scalarSnapshot = default;
            MockHudSignal hudSnapshot = default;
            MockAupRebaseSignal rebaseSnapshot = default;
            AnomalyThermoSourceDTO thermoSnapshot = default;
            try
            {
                if (vault != null &&
                    TryResolveBuffers(
                        vault,
                        out NativeArray<AnomalyFieldDTO> field,
                        out _,
                        out NativeArray<AnomalyGlobalScalarsDTO> globals,
                        out _,
                        out NativeArray<MockHudSignal> hud,
                        out _,
                        out NativeArray<MockAupRebaseSignal> rebase,
                        out NativeArray<AnomalyThermoSourceDTO> thermo,
                        out NativeArray<AnomalyTelemetryEntry> telemetry))
                {
                    AnomalyGlobalScalarsDTO scalar = globals[0];
                    diagnosticFlags = scalar.Flags;
                    if (elapsedMs > ComputeBudgetMs)
                        diagnosticFlags |= SeedShipAnomalyFlags.BudgetExceeded;
                    scalar.AnomalyComputeTimeMs = 0f;
                    globals[0] = scalar;

                    if (telemetry.IsCreated && telemetry.Length > 0)
                    {
                        int cursor = math.clamp(_telemetryCursor, 0, telemetry.Length - 1);
                        AnomalyTelemetryEntry entry = telemetry[cursor];
                        entry.AnomalyComputeTimeMs = elapsedMs;
                        if (elapsedMs > ComputeBudgetMs)
                            entry.Flags |= SeedShipAnomalyFlags.BudgetExceeded;
                        telemetry[cursor] = entry;
                    }

                    fieldSnapshot = field[0];
                    scalarSnapshot = scalar;
                    hudSnapshot = hud[0];
                    rebaseSnapshot = rebase[0];
                    thermoSnapshot = thermo[0];
                    shouldDump = (diagnosticFlags & (SeedShipAnomalyFlags.BudgetExceeded | SeedShipAnomalyFlags.NonFinite)) != 0u;
                    hasSnapshot = true;
                }
            }
            finally
            {
                _telemetryCursor = (_telemetryCursor + 1) % SeedShipAnomalyConstants.TelemetryFrameCount;
                _jobScheduled = false;
                UnlockJobBuffers();
            }

            if (!hasSnapshot)
                return;

            PublishLateFrameSignals(in fieldSnapshot, in scalarSnapshot, in hudSnapshot, in rebaseSnapshot, in thermoSnapshot);
            SeedShipAnomalyShaderBridge.Publish(vault, in fieldSnapshot, in scalarSnapshot);

            if (shouldDump)
                TryDumpTelemetry(vault, diagnosticFlags);
        }

        private void PublishLateFrameSignals(
            in AnomalyFieldDTO field,
            in AnomalyGlobalScalarsDTO globals,
            in MockHudSignal hud,
            in MockAupRebaseSignal rebase,
            in AnomalyThermoSourceDTO thermo)
        {
            AbsoluteUniversePosition epicenter = AbsoluteUniversePosition.FromAbsolutePosition(field.EpicenterAUP);
            SignalBus<MockHudSignal>.TryPushTracked(in hud, ref _signalPushDropCount);
            if (rebase.Flags != 0u && rebase.ShiftFrameId == globals.LastRebaseFrame)
                SignalBus<MockAupRebaseSignal>.TryPushTracked(in rebase, ref _signalPushDropCount);

            SignalBus<AnomalyProximitySignal>.TryPushTracked(new AnomalyProximitySignal
            {
                SourceAup = epicenter,
                Proximity01 = globals.Corruption01,
                Interference01 = globals.RadarJam01,
                Frame = globals.Frame,
                SourceHash = SeedShipAnomalyConstants.SourceHash,
                Flags = (byte)(globals.Corruption01 > 0.001f ? 1 : 0)
            }, ref _signalPushDropCount);

            bool publishRadiationThisSlowTick = _radiationExportRequested;
            if (publishRadiationThisSlowTick)
                _radiationExportRequested = false;

            if (globals.Corruption01 > 0.001f)
            {
                SignalBus<SystemGlitchSignal>.TryPushTracked(new SystemGlitchSignal
                {
                    Frame = globals.Frame,
                    SourceId = SeedShipAnomalyConstants.SourceHash,
                    LocalHash = field.GlitchHash,
                    ExpectedHash = SeedShipAnomalyConstants.GlitchHash,
                    Intensity01 = globals.ShaderCorruption01,
                    DurationSeconds = 0.25f,
                    Reason = 48,
                    Flags = 1
                }, ref _signalPushDropCount);

                SignalBus<TelemetryAnomalySignal>.TryPushTracked(new TelemetryAnomalySignal
                {
                    SystemHash = SeedShipAnomalyConstants.SourceHash,
                    AnomalyHash = field.GlitchHash,
                    Scalar = globals.Corruption01,
                    Frame = globals.Frame,
                    Severity = (byte)math.clamp((int)math.round(globals.Corruption01 * 255f), 0, 255),
                    Flags = (byte)(globals.Flags & 0xFFu)
                }, ref _signalPushDropCount);

                if (publishRadiationThisSlowTick)
                {
                    float radiation01 = SaturateFinite01(globals.Radiation01);
                    float sourceIntensity01 = SaturateFinite01(thermo.Radiation01);
                    float sourceRadiusMeters = PositiveFiniteOrZero(thermo.RadiusMeters);
                    if (sourceIntensity01 > 0.0001f && sourceRadiusMeters > 0f)
                    {
                        SignalBus<RadiationSourceSignal>.TryPushTracked(new RadiationSourceSignal
                        {
                            PositionAup = epicenter,
                            Intensity = sourceIntensity01,
                            RadiusMeters = sourceRadiusMeters,
                            SourceId = unchecked((int)SeedShipAnomalyConstants.SourceHash),
                            Operation = RadiationSourceSignal.OperationUpsert,
                            Flags = 1
                        }, ref _signalPushDropCount);
                        _radiationSourceActive = true;
                    }
                    else if (_radiationSourceActive)
                    {
                        PublishRadiationSourceRemove();
                    }

                    if (radiation01 > 0.0001f)
                    {
                        SignalBus<RadiationDoseSignal>.TryPushTracked(new RadiationDoseSignal
                        {
                            PositionAup = epicenter,
                            Dose = radiation01 * RadiationDosePerSecondScale * RadiationExportSlowTickSeconds,
                            Intensity01 = radiation01,
                            SourceId = SeedShipAnomalyConstants.SourceHash,
                            DoseKind = 48,
                            Flags = 1
                        }, ref _signalPushDropCount);
                    }
                }
            }
            else if (publishRadiationThisSlowTick && _radiationSourceActive)
            {
                PublishRadiationSourceRemove();
            }
        }

        private void PublishRadiationSourceRemove()
        {
            SignalBus<RadiationSourceSignal>.TryPushTracked(new RadiationSourceSignal
            {
                SourceId = unchecked((int)SeedShipAnomalyConstants.SourceHash),
                Operation = RadiationSourceSignal.OperationRemove,
                Flags = 1
            }, ref _signalPushDropCount);
            _radiationSourceActive = false;
        }

        private static float SaturateFinite01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float PositiveFiniteOrZero(float value)
        {
            return math.isfinite(value) && value > 0f ? value : 0f;
        }

        private void TryDumpTelemetry(IDataVault vault, uint reasonFlags)
        {
            if (_dumpedBudgetBreach || vault == null)
                return;

            if (System.Threading.Interlocked.CompareExchange(ref _telemetryDumpInFlight, 1, 0) != 0)
            {
                _dumpedBudgetBreach = true;
                return;
            }

            bool queued = false;
            if (!TryReadOnlySeedShipVaultBuffer(
                    vault,
                    ref _telemetryHandle,
                    BufferID.ShinobuSeedShipAnomalyTelemetryRing,
                    SeedShipAnomalyConstants.TelemetryFrameCount,
                    out NativeArray<AnomalyTelemetryEntry>.ReadOnly telemetry) ||
                !telemetry.IsCreated ||
                telemetry.Length == 0)
            {
                System.Threading.Volatile.Write(ref _telemetryDumpInFlight, 0);
                return;
            }

            try
            {
                _dumpedBudgetBreach = true;
                _telemetryDumpByteCount = PackTelemetryDumpBytes(telemetry, reasonFlags, _telemetryCursor, _telemetryDumpScratch);
                queued = System.Threading.ThreadPool.QueueUserWorkItem(TelemetryDumpWorkerCallback, this);
            }
            catch (Exception)
            {
                _dumpedBudgetBreach = true;
            }
            finally
            {
                if (!queued)
                    System.Threading.Volatile.Write(ref _telemetryDumpInFlight, 0);
            }
        }

        private static int PackTelemetryDumpBytes(
            NativeArray<AnomalyTelemetryEntry>.ReadOnly telemetry,
            uint reasonFlags,
            int telemetryCursor,
            byte[] scratchBytes)
        {
            Span<byte> scratch = scratchBytes;
            scratch.Clear();
            BinaryPrimitives.WriteUInt64LittleEndian(scratch.Slice(0, 8), DumpMagic);
            BinaryPrimitives.WriteUInt32LittleEndian(scratch.Slice(8, 4), DumpVersion);
            BinaryPrimitives.WriteUInt32LittleEndian(scratch.Slice(12, 4), reasonFlags);
            BinaryPrimitives.WriteInt32LittleEndian(scratch.Slice(16, 4), telemetry.Length);
            BinaryPrimitives.WriteInt32LittleEndian(scratch.Slice(20, 4), telemetryCursor);

            int offset = 32;
            for (int i = 0; i < telemetry.Length && offset + 64 <= scratch.Length; i++)
            {
                AnomalyTelemetryEntry entry = telemetry[i];
                WriteFloatLittleEndian(scratch.Slice(offset, 4), entry.CurrentCorruptionLevel);
                BinaryPrimitives.WriteInt32LittleEndian(scratch.Slice(offset + 4, 4), entry.EntitiesAffected);
                WriteFloatLittleEndian(scratch.Slice(offset + 8, 4), entry.AnomalyComputeTimeMs);
                WriteFloatLittleEndian(scratch.Slice(offset + 12, 4), entry.GravityY);
                WriteFloatLittleEndian(scratch.Slice(offset + 16, 4), entry.RadarJam01);
                WriteFloatLittleEndian(scratch.Slice(offset + 20, 4), entry.HeatSource01);
                WriteFloatLittleEndian(scratch.Slice(offset + 24, 4), entry.GlobalQualityWeight);
                BinaryPrimitives.WriteUInt32LittleEndian(scratch.Slice(offset + 28, 4), entry.Frame);
                BinaryPrimitives.WriteUInt32LittleEndian(scratch.Slice(offset + 32, 4), entry.Flags);
                BinaryPrimitives.WriteUInt32LittleEndian(scratch.Slice(offset + 36, 4), entry.StateHash);
                WriteDoubleLittleEndian(scratch.Slice(offset + 40, 8), entry.EpicenterAUP.x);
                WriteDoubleLittleEndian(scratch.Slice(offset + 48, 8), entry.EpicenterAUP.y);
                WriteDoubleLittleEndian(scratch.Slice(offset + 56, 8), entry.EpicenterAUP.z);
                offset += 64;
            }

            return math.min(offset, scratch.Length);
        }

        private static void RunTelemetryDumpWorker(object state)
        {
            if (state is SeedShipAnomalyRuntime runtime)
                runtime.WriteQueuedTelemetryDump();
        }

        private void WriteQueuedTelemetryDump()
        {
            try
            {
                if (!WriteColdDumpBytes(_dumpPath, _telemetryDumpScratch, _telemetryDumpByteCount))
                    _dumpedBudgetBreach = true;
            }
            catch (Exception)
            {
                _dumpedBudgetBreach = true;
            }
            finally
            {
                System.Threading.Volatile.Write(ref _telemetryDumpInFlight, 0);
            }
        }

        private static unsafe bool WriteColdDumpBytes(string path, byte[] bytes, int byteCount)
        {
            if (string.IsNullOrWhiteSpace(path) || bytes == null || byteCount <= 0)
                return false;

            int safeByteCount = math.min(byteCount, bytes.Length);
            if (safeByteCount <= 0)
                return false;

            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                safeByteCount,
                nameof(SeedShipAnomalyRuntime),
                "seedShipAnomalyTelemetryDumpPayload");
            try
            {
                void* destination = NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                fixed (byte* source = bytes)
                {
                    UnsafeUtility.MemCpy(destination, source, safeByteCount);
                }

                return NativeFaultDumpWriter.TryWriteAll(path, payload, safeByteCount);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(SeedShipAnomalyRuntime),
                    "seedShipAnomalyTelemetryDumpPayload");
            }
        }

        private void ConsumeHackSignals(float dt)
        {
            ReadOnlySpan<CoreHackedSignal> signals = SignalBus<CoreHackedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                CoreHackedSignal signal = signals[i];
                if (signal.CodeHash == SeedShipAnomalyConstants.CoreHackAcceptedHash &&
                    signal.Validity01 >= 0.999f)
                {
                    _healingSecondsRemaining = 10f;
                }
            }

            if (_healingSecondsRemaining > 0f)
                _healingSecondsRemaining = math.max(0f, _healingSecondsRemaining - dt);
        }

        private double3 ResolvePlayerAup()
        {
            IPlayerRuntimeContext context = _playerContext;
            if (context != null &&
                context.IsInitialized &&
                context.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                return snapshot.Aup.ToAbsoluteDouble3();
            }

            return new double3(seedShipAupX, seedShipAupY, seedShipAupZ);
        }

        private float ResolveGlobalQualityWeight(IDataVault vault, float fallback)
        {
            if (TryResolveBorrowedScalabilityState(vault, out NativeArray<ScalabilityStateDTO>.ReadOnly state) &&
                math.isfinite(state[0].GlobalQualityWeight))
            {
                return math.saturate(state[0].GlobalQualityWeight);
            }

            return math.saturate(math.isfinite(fallback) ? fallback : defaultGlobalQualityWeight);
        }

#if UNITY_EDITOR
        private void MonitorCsvOverrides(IDataVault vault)
        {
            try
            {
                if (string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath))
                    return;

                long ticks = File.GetLastWriteTimeUtc(_csvPath).Ticks;
                if (ticks == _csvLastWriteTicks)
                    return;

                _csvLastWriteTicks = ticks;
                Span<byte> scratch = stackalloc byte[CsvMaxBytes];
                int read = ReadColdBytes(_csvPath, scratch);
                if (read <= 0)
                    return;

                Span<AnomalyCsvOverrideDTO> overrides = stackalloc AnomalyCsvOverrideDTO[SeedShipAnomalyConstants.CsvOverrideCapacity];
                if (!TryBuildCsvOverrides(
                        vault,
                        scratch.Slice(0, read),
                        overrides,
                        out AnomalyTuningDTO tuning,
                        out AnomalyFieldDTO field,
                        out int overrideCount))
                {
                    return;
                }

                TryCommitCsvOverrides(vault, in tuning, in field, overrides, overrideCount);
            }
            catch (Exception)
            {
                _csvLastWriteTicks = 0L;
            }
        }

        private bool TryBuildCsvOverrides(
            IDataVault vault,
            ReadOnlySpan<byte> bytes,
            Span<AnomalyCsvOverrideDTO> stagedOverrides,
            out AnomalyTuningDTO tuning,
            out AnomalyFieldDTO field,
            out int overrideCount)
        {
            tuning = default;
            field = default;
            overrideCount = 0;
            if (!TryReadOnlySeedShipVaultBuffer(vault, ref _tuningHandle, BufferID.ShinobuSeedShipAnomalyTuning, 1, out NativeArray<AnomalyTuningDTO>.ReadOnly tuningArray) ||
                !TryReadOnlySeedShipVaultBuffer(vault, ref _fieldHandle, BufferID.ShinobuSeedShipAnomalyField, 1, out NativeArray<AnomalyFieldDTO>.ReadOnly fieldArray))
            {
                return false;
            }

            tuning = tuningArray[0];
            field = fieldArray[0];
            int lineStart = 0;
            uint frame = _simulationFrameCounter;

            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != (byte)'\n')
                    continue;

                ReadOnlySpan<byte> line = TrimAscii(bytes.Slice(lineStart, i - lineStart));
                lineStart = i + 1;
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                int comma = IndexOf(line, (byte)',');
                if (comma <= 0)
                    comma = IndexOf(line, (byte)'=');
                if (comma <= 0)
                    continue;

                ReadOnlySpan<byte> key = TrimAscii(line.Slice(0, comma));
                ReadOnlySpan<byte> valueSpan = TrimAscii(line.Slice(comma + 1));
                if (!TryParseAsciiFloat(valueSpan, out float value))
                    continue;

                uint keyHash = HashLowerAscii(key);
                ApplyCsvOverride(keyHash, value, ref tuning, ref field);
                if (overrideCount < stagedOverrides.Length)
                {
                    AnomalyCsvOverrideDTO row = default;
                    row.KeyHash = keyHash;
                    row.Value = value;
                    row.Frame = frame;
                    row.Flags = 1u;
                    stagedOverrides[overrideCount++] = row;
                }
            }

            tuning = SeedShipAnomalyMath.SanitizeTuning(tuning);
            field.Radius = math.max(0f, field.Radius);
            field.CorruptionLevel = math.saturate(field.CorruptionLevel);
            return true;
        }

        private bool TryCommitCsvOverrides(
            IDataVault vault,
            in AnomalyTuningDTO tuning,
            in AnomalyFieldDTO field,
            ReadOnlySpan<AnomalyCsvOverrideDTO> stagedOverrides,
            int overrideCount)
        {
            if (vault == null || !vault.TryAcquireMutationGuard(CsvApplyMutationGuardMask))
                return false;

            try
            {
                if (!TryResolveSeedShipVaultBuffer(vault, ref _tuningHandle, BufferID.ShinobuSeedShipAnomalyTuning, 1, out NativeArray<AnomalyTuningDTO> tuningArray) ||
                    !TryResolveSeedShipVaultBuffer(vault, ref _fieldHandle, BufferID.ShinobuSeedShipAnomalyField, 1, out NativeArray<AnomalyFieldDTO> fieldArray) ||
                    !TryResolveSeedShipVaultBuffer(vault, ref _csvOverrideHandle, BufferID.ShinobuSeedShipAnomalyCsvOverrides, SeedShipAnomalyConstants.CsvOverrideCapacity, out NativeArray<AnomalyCsvOverrideDTO> overrides))
                {
                    return false;
                }

                tuningArray[0] = tuning;
                fieldArray[0] = field;
                int safeOverrideCount = math.min(math.max(0, overrideCount), math.min(stagedOverrides.Length, overrides.Length));
                for (int i = 0; i < safeOverrideCount; i++)
                    overrides[i] = stagedOverrides[i];
                for (int i = safeOverrideCount; i < overrides.Length; i++)
                    overrides[i] = default;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(CsvApplyMutationGuardMask);
            }
        }

        private static void ApplyCsvOverride(uint keyHash, float value, ref AnomalyTuningDTO tuning, ref AnomalyFieldDTO field)
        {
            if (keyHash == _MaxCorruptionRadiusHash)
            {
                tuning.MaxCorruptionRadius = value;
                field.Radius = value;
            }
            else if (keyHash == _GravityInversionStrengthHash)
            {
                tuning.GravityInversionStrength = value;
            }
            else if (keyHash == _PulseFrequencyHash)
            {
                tuning.PulseFrequency = value;
            }
            else if (keyHash == _GlitchIntensityHash)
            {
                tuning.GlitchIntensity = value;
            }
            else if (keyHash == _HeatEmissionHash)
            {
                tuning.HeatEmission = value;
            }
            else if (keyHash == _RadiationEmissionHash)
            {
                tuning.RadiationEmission = value;
            }
            else if (keyHash == _RadarJamIntensityHash)
            {
                tuning.RadarJamIntensity = value;
            }
            else if (keyHash == _BabelScrambleStrengthHash)
            {
                tuning.BabelScrambleStrength = value;
            }
            else if (keyHash == _GlobalQualityWeightHash)
            {
                tuning.GlobalQualityWeight = value;
            }
        }
#endif

        private void TryRegisterTicks()
        {
            TryRegisterColdTick();

            if (!_registeredUpdate)
                _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);

            if (!_registeredUpdate || !_registeredLateFrame || !_registeredSlowTick)
            {
                if (_registeredUpdate)
                {
                    GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                    _registeredUpdate = false;
                }

                if (_registeredLateFrame)
                {
                    GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                    _registeredLateFrame = false;
                }

                if (_registeredSlowTick)
                {
                    GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                    _registeredSlowTick = false;
                }
            }
        }

        private void TryUnregisterTicks()
        {
            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredColdTick = false;
            }

            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdate = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }
        }

        private void TryRegisterColdTick()
        {
            if (!_registeredColdTick)
                _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private bool EnsureSeedShipVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (TryResolveSeedShipVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystem, options);
            return TryResolveSeedShipVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private bool TryResolveSeedShipVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (IsSeedShipVaultHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                !IsSeedShipVaultHandle(in handle, bufferId) ||
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

        private static bool HasSeedShipVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsSeedShipVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryReadSeedShipVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                requiredLength <= 0 ||
                !IsSeedShipVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private bool TryReadOnlySeedShipVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (IsSeedShipVaultHandle(in handle, bufferId) &&
                vault.TryReadOnlyHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                !IsSeedShipVaultHandle(in handle, bufferId) ||
                !vault.TryReadOnlyHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                handle = default;
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsSeedShipVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u;
        }

        private bool TryResolveBorrowedScalabilityState(IDataVault vault, out NativeArray<ScalabilityStateDTO>.ReadOnly state)
        {
            state = default;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (IsBorrowedScalabilityHandle(in _scalabilityHandle) &&
                vault.TryReadOnlyHandle(in _scalabilityHandle, out state) &&
                state.IsCreated &&
                state.Length > 0)
            {
                return true;
            }

            if (!vault.TryGetGenerationHandle<ScalabilityStateDTO>(BufferID.ShinobuScalabilityState, out _scalabilityHandle) ||
                !IsBorrowedScalabilityHandle(in _scalabilityHandle) ||
                !vault.TryReadOnlyHandle(in _scalabilityHandle, out state) ||
                !state.IsCreated ||
                state.Length <= 0)
            {
                _scalabilityHandle = default;
                state = default;
                return false;
            }

            return true;
        }

        private static bool IsBorrowedScalabilityHandle(
            in VaultGenerationHandle<ScalabilityStateDTO> handle)
        {
            return handle.BufferID == unchecked((uint)(int)BufferID.ShinobuScalabilityState) &&
                   handle.SystemID == (uint)SystemID.GraphicsScalability &&
                   handle.Generation != 0u;
        }

        private void ReleaseSeedShipVaultHandles(IDataVault vault)
        {
            ReleaseSeedShipVaultHandle(vault, ref _fieldHandle);
            ReleaseSeedShipVaultHandle(vault, ref _tuningHandle);
            ReleaseSeedShipVaultHandle(vault, ref _globalsHandle);
            ReleaseSeedShipVaultHandle(vault, ref _glitchHandle);
            ReleaseSeedShipVaultHandle(vault, ref _hudHandle);
            ReleaseSeedShipVaultHandle(vault, ref _leviathanHandle);
            ReleaseSeedShipVaultHandle(vault, ref _rebaseHandle);
            ReleaseSeedShipVaultHandle(vault, ref _thermoHandle);
            ReleaseSeedShipVaultHandle(vault, ref _telemetryHandle);
            ReleaseSeedShipVaultHandle(vault, ref _csvOverrideHandle);
        }

        private static void ReleaseSeedShipVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null &&
                handle.SystemID == (uint)OwnerSystem &&
                handle.BufferID != 0u &&
                handle.Generation != 0u)
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private void ClearCachedHandles()
        {
            _fieldHandle = default;
            _tuningHandle = default;
            _globalsHandle = default;
            _glitchHandle = default;
            _hudHandle = default;
            _leviathanHandle = default;
            _rebaseHandle = default;
            _thermoHandle = default;
            _telemetryHandle = default;
            _csvOverrideHandle = default;
            _scalabilityHandle = default;
        }

        private static void EnsureSignalLanesReady()
        {
            SignalBus<RadarJamSignal>.Configure(8, maxFrameSignals: 16, lowTierFrameSignals: 4, laneHash: SeedShipAnomalyConstants.RadarJamLaneHash);
            SignalBus<RadarJamSignal>.EnsureInitialized();
            SignalBus<CoreHackedSignal>.Configure(4, maxFrameSignals: 8, lowTierFrameSignals: 2, laneHash: SeedShipAnomalyConstants.CoreHackLaneHash);
            SignalBus<CoreHackedSignal>.EnsureInitialized();
            SignalBus<MockHudSignal>.Configure(4, maxFrameSignals: 8, lowTierFrameSignals: 2, laneHash: SeedShipAnomalyConstants.MockHudLaneHash);
            SignalBus<MockHudSignal>.EnsureInitialized();
            SignalBus<MockAupRebaseSignal>.Configure(4, maxFrameSignals: 8, lowTierFrameSignals: 2, laneHash: SeedShipAnomalyConstants.MockAupRebaseLaneHash);
            SignalBus<MockAupRebaseSignal>.EnsureInitialized();
            SignalBus<AnomalyProximitySignal>.EnsureInitialized();
            SignalBus<SystemGlitchSignal>.EnsureInitialized();
            SignalBus<TelemetryAnomalySignal>.EnsureInitialized();
            SignalBus<RadiationSourceSignal>.EnsureInitialized();
            SignalBus<RadiationDoseSignal>.EnsureInitialized();
        }

#if UNITY_EDITOR
        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> bytes)
        {
            int start = 0;
            int end = bytes.Length - 1;
            while (start <= end && IsCsvSpace(bytes[start]))
                start++;
            while (end >= start && (IsCsvSpace(bytes[end]) || bytes[end] == (byte)'\r'))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : bytes.Slice(start, end - start + 1);
        }

        private static int IndexOf(ReadOnlySpan<byte> bytes, byte target)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == target)
                    return i;
            }

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsCsvSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static bool TryParseAsciiFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            int cursor = 0;
            while (cursor < bytes.Length && IsCsvSpace(bytes[cursor]))
                cursor++;

            float sign = 1f;
            if (cursor < bytes.Length && bytes[cursor] == (byte)'-')
            {
                sign = -1f;
                cursor++;
            }
            else if (cursor < bytes.Length && bytes[cursor] == (byte)'+')
            {
                cursor++;
            }

            float whole = 0f;
            bool hasDigit = false;
            while (cursor < bytes.Length && bytes[cursor] >= (byte)'0' && bytes[cursor] <= (byte)'9')
            {
                hasDigit = true;
                whole = whole * 10f + (bytes[cursor] - (byte)'0');
                cursor++;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (cursor < bytes.Length && bytes[cursor] == (byte)'.')
            {
                cursor++;
                while (cursor < bytes.Length && bytes[cursor] >= (byte)'0' && bytes[cursor] <= (byte)'9')
                {
                    hasDigit = true;
                    fraction = fraction * 10f + (bytes[cursor] - (byte)'0');
                    divisor *= 10f;
                    cursor++;
                }
            }

            if (!hasDigit)
                return false;

            value = sign * (whole + fraction * math.rcp(math.max(1f, divisor)));
            return math.isfinite(value);
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte value = bytes[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                hash ^= value;
                hash *= 16777619u;
            }

            return hash;
        }
#endif

        private static uint HashLowerAsciiString(string value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash;
        }

        private static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> bytes, int offset)
        {
            return (uint)(bytes[offset] |
                          (bytes[offset + 1] << 8) |
                          (bytes[offset + 2] << 16) |
                          (bytes[offset + 3] << 24));
        }

        private static float ReadFloatLittleEndian(ReadOnlySpan<byte> bytes, int offset)
        {
            uint raw = ReadUInt32LittleEndian(bytes, offset);
            return math.asfloat(raw);
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, math.asuint(value));
        }

        private static void WriteDoubleLittleEndian(Span<byte> destination, double value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(destination, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
        }
    }
}
