using System;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Physiology;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Determinism;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Physiology
{
    // [Preserve] because the only construction site is GameBootstrapper's reflection ensure:
    // Hecton8.Physiology references Hecton8.Core, so a direct bootstrap call would form an assembly
    // cycle. No assembly references Hecton8.Physiology and its asmdef sets autoReferenced=false, so
    // without this attribute the managed stripper can drop the type that Type.GetType must resolve.
    [Preserve]
    [DisallowMultipleComponent]
    public sealed unsafe class ShinobuSuitIntegrityRuntime : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const SystemID OwnerSystem = SystemID.GameplayPlayer;
        private const string RuntimeRootName = "[ShinobuSuitIntegrityRuntime]";
        private const string CsvRelativePath = "suit_pressure_profiles.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_323.bin";
        private const ulong DumpMagic = 0x5333323350524553UL; // S323PRES
        private const uint DumpVersion = 1u;
        private const float SlowTickNominalSeconds = 0.1f;
        private const double DefaultSeaLevelAupY = 14.02d;
        private static readonly ulong JobMutationGuardMask =
            MutationGuardBit(ShinobuSuitIntegrityConstants.StateBuffer) |
            MutationGuardBit(ShinobuSuitIntegrityConstants.ProfileBuffer) |
            MutationGuardBit(ShinobuSuitIntegrityConstants.TuningBuffer) |
            MutationGuardBit(ShinobuSuitIntegrityConstants.TelemetryBuffer) |
            MutationGuardBit(ShinobuSuitIntegrityConstants.VisualBuffer) |
            MutationGuardBit(ShinobuSuitIntegrityConstants.MockAupBuffer);
        private static readonly ulong DefaultsMutationGuardMask =
            MutationGuardBit(ShinobuSuitIntegrityConstants.StateBuffer) |
            MutationGuardBit(ShinobuSuitIntegrityConstants.ProfileBuffer) |
            MutationGuardBit(ShinobuSuitIntegrityConstants.TuningBuffer);
        private static readonly ulong MockAupMutationGuardMask =
            MutationGuardBit(ShinobuSuitIntegrityConstants.MockAupBuffer);
        private static readonly ulong ProfileCsvMutationGuardMask =
            MutationGuardBit(ShinobuSuitIntegrityConstants.ProfileBuffer);
        private static readonly uint _HeaderSuitNameHash = HashLowerAsciiString("suit_name");
        private static readonly uint _HeaderNameHash = HashLowerAsciiString("name");
        // COLD ALLOC: SuitPressureProfileDTO[ProfileCapacity] - default profile commit scratch - owner: ShinobuSuitIntegrityRuntime
        private static readonly SuitPressureProfileDTO[] s_defaultProfileScratch = new SuitPressureProfileDTO[ShinobuSuitIntegrityConstants.ProfileCapacity];
        // COLD ALLOC: SuitHydrostaticMockAupDTO[MockPressureSampleCount] - mock AUP commit scratch - owner: ShinobuSuitIntegrityRuntime
        private static readonly SuitHydrostaticMockAupDTO[] s_mockAupScratchCold = new SuitHydrostaticMockAupDTO[ShinobuSuitIntegrityConstants.MockPressureSampleCount];
        private static int s_mockAupScratchBusy;
#if UNITY_EDITOR
        // COLD ALLOC: byte[CsvMaxBytes] - editor CSV import scratch - owner: ShinobuSuitIntegrityRuntime
        private static readonly byte[] s_profileCsvScratchCold = new byte[ShinobuSuitIntegrityConstants.CsvMaxBytes];
        // COLD ALLOC: SuitPressureProfileDTO[ProfileCapacity] - editor CSV profile import scratch - owner: ShinobuSuitIntegrityRuntime
        private static readonly SuitPressureProfileDTO[] s_profileImportScratch = new SuitPressureProfileDTO[ShinobuSuitIntegrityConstants.ProfileCapacity];
        private static int s_profileCsvScratchBusy;
#endif

        [Header("Runtime Capacity")]
        [SerializeField, Min(1)] private int entityCapacity = ShinobuSuitIntegrityConstants.DefaultEntityCapacity;

        [Header("AUP Pressure")]
        [Tooltip("Sea-level Y in AUP meters. Depth is seaLevelAup.y - playerAup.y in double precision.")]
        [SerializeField] private double seaLevelAupY = DefaultSeaLevelAupY;

        [Tooltip("Use the 0m..8000m synthetic AUP pressure profile when no player AUP is available.")]
        [SerializeField] private bool enableEmergencyMockPressureProfile = true;

        private VaultGenerationHandle<SuitIntegrityDTO> _integrityHandle;
        private VaultGenerationHandle<SuitPressureProfileDTO> _profileHandle;
        private VaultGenerationHandle<SuitIntegrityTuningDTO> _tuningHandle;
        private VaultGenerationHandle<SuitIntegrityTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<SuitIntegrityVisualDTO> _visualHandle;
        private VaultGenerationHandle<SuitHydrostaticMockAupDTO> _mockAupHandle;
        private VaultGenerationHandle<LockstepPlayerKinematicState> _playerKinematicStateHandle;
        private VaultGenerationHandle<MetabolicStateDTO> _metabolismStateHandle;

        private IDataVault _dataVault;
        private IPlayerRuntimeContext _playerContext;
        private IHectonOceanKinematicsService _oceanKinematics;
        private ITickDispatcher _tickDispatcher;
        private JobHandle _activeJobHandle;
        private AbsoluteUniversePosition _lastPlayerAup;
        private double3 _lastPlayerAupDouble;
        private string _csvPath;
        private string _dumpPath;
        private double _lastDispatcherTimeSeconds = -1d;
        private long _csvLastWriteTicks;
        private long _jobScheduleTimestamp;
        private int _telemetryCursor;
        private int _scheduledCount;
        private uint _frameCounter;
        private uint _metabolicDamageTargetHash;
        private uint _kinematicDamageTargetHash;
        private uint _coldDamageTargetHash;
        private float _simulationAccumulator;
        private float _lastTickInterval = 1f;
        private bool _registeredSlow;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _jobScheduled;
        private bool _jobGuardHeld;
        private bool _defaultsInitialized;
        private bool _autopsyDumped;
        private bool _playerAupValid;
        private IDataVault _jobGuardVault;

        /// <summary>
        /// Resolve-or-create the sole suit pressure-damage authority.
        /// Script GUID eb0be93afdca59f4389d297e159727d2 has ZERO live scene/prefab hits: a byte-level
        /// scan of all 5174 project scene/prefab/asset files (33 of them binary-serialized) finds the
        /// GUID only in its own .meta, and the binary 02_HECTON_WORLD type tree carries no
        /// ShinobuSuitIntegrityRuntime entry. No authored instance exists anywhere. The old pressure
        /// lane in HectonSurvivalSystem is a documented no-op stub that hands off to this type, and
        /// this type registers its SlowTick/LateFrameTick lanes in OnEnable, which only runs on an
        /// instance that already exists. Without this construction site nothing ever schedules
        /// EvaluateHydrostaticPressureJob or CalculateStructuralYieldJob, so depth costs the suit no
        /// integrity and barotrauma implosion damage can never reach the player.
        /// Idempotent: an authored or already-constructed instance wins and no duplicate is built.
        /// </summary>
        public static ShinobuSuitIntegrityRuntime EnsureRuntimeInstance()
        {
            ShinobuSuitIntegrityRuntime existing = FindFirstObjectByType<ShinobuSuitIntegrityRuntime>(FindObjectsInactive.Include);
            if (existing != null)
            {
                // Re-activate a buried instance rather than stacking a second one. This is not cosmetic:
                // TryRegisterHotSwapListener and TryRegisterTicks run from OnEnable, and OnEnable never
                // fires while the GameObject is inactive in hierarchy, so a found-but-disabled owner
                // leaves the pressure lane unscheduled exactly as an absent one would. Start() re-runs
                // the same rebind, but it is equally gated on the object being active.
                if (!existing.gameObject.activeSelf)
                    existing.gameObject.SetActive(true);
                if (!existing.enabled)
                    existing.enabled = true;
                return existing;
            }

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: no authored instance is reachable in any scene. Every
            // dependency this type needs is resolved cold in OnEnable/Start through GlobalRegistry
            // (DataVault, Player, OceanKinematics, TickDispatcher) and re-resolved on hot swap through
            // OnGlobalRegistryServiceReplaced, so a runtime-created owner drives the full pressure lane.
            GameObject runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: GameObject[1] - bootstrap-owned suit pressure authority root - owner: ShinobuSuitIntegrityRuntime
            ShinobuSuitIntegrityRuntime runtime = runtimeRoot.AddComponent<ShinobuSuitIntegrityRuntime>();

            // Disarm the emergency mock ramp on the bootstrap-built owner, because the bootstrap
            // construction site runs from GameBootstrapper.PublishPlayerRuntimeReference, which
            // DisablePlayer calls before Step 3 world generation and Step 7 player spawn. This owner is
            // therefore alive for the whole loading screen with no player AUP bound, and the class has
            // no player-existence guard: SlowTick would set useMock, EvaluateHydrostaticPressureJob
            // would replace the player position with the synthetic 0..MockMaxDepthMeters ramp (8000 m
            // by SanitizeTuning default), and at 8000 m the 61 ATM standard profile sees ~12x
            // overpressure - integrity reaches CatastrophicIntegrity01 and EnqueueImplosionDamage fires
            // a 9999-magnitude CombatDamageSignal at the canonical player hash while the player is
            // still suspended behind the Kinematic Arrest Gate. With the ramp disarmed the no-player
            // branch is provably inert instead: player and sea level collapse onto the same AUP Y,
            // ResolveDepthMetersFromAup returns 0, pressure is SurfacePressureAtm, overpressure is 0,
            // and no damage, groan or implosion is emitted until a real player AUP binds through
            // PlayerRuntimeContextService. The serialized default stays true so an authored instance a
            // designer places for pressure-profile tuning keeps the diagnostic ramp.
            // Assigning after AddComponent is safe: neither Awake nor OnEnable reads this field, and
            // the first read is in SlowTick, which cannot run before the dispatcher's next slow phase.
            runtime.enableEmergencyMockPressureProfile = false;
            return runtime;
        }

        private void Awake()
        {
            entityCapacity = math.max(1, entityCapacity);
            _csvPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", CsvRelativePath));
            _dumpPath = DumpRelativePath;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            SignalBus<CombatDamageSignal>.EnsureInitialized();
            SignalBus<MovementAcousticSignal>.EnsureInitialized();
            TryRegisterHotSwapListener();
            RebindColdServices();
            if (EnsureVaultState())
                TryRegisterTicks();
        }

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            RebindColdServices();
            if (EnsureVaultState())
                TryRegisterTicks();
        }

        private void OnDisable()
        {
            CompleteFrameJobForTeardown();
            TryUnregisterTicks();
            TryUnregisterHotSwapListener();
            UnlockJobBuffers();
            ReleaseVaultHandles();
            ClearCachedHandles();
            _oceanKinematics = null;
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                CompleteFrameJobForTeardown();
                UnlockJobBuffers();
                ReleaseVaultHandles();
                _dataVault = currentService as IDataVault;
                ClearCachedHandles();
                _defaultsInitialized = false;
                _autopsyDumped = false;
                ClearTargetHashCache();
                RefreshPlayerCombatTargetHashCold(_playerContext);
                if (_dataVault != null && EnsureVaultState())
                    TryRegisterTicks();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerContext = currentService as IPlayerRuntimeContext;
                ClearTargetHashCache();
                _playerAupValid = false;
                RefreshPlayerCombatTargetHashCold(_playerContext);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.OceanKinematics)
            {
                _oceanKinematics = currentService as IHectonOceanKinematicsService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _tickDispatcher = currentService as ITickDispatcher;
                _lastDispatcherTimeSeconds = -1d;
                TryUnregisterTicks();
                if (currentService != null && isActiveAndEnabled)
                    TryRegisterTicks();
            }
        }

        public void SlowTick()
        {
            if (_jobScheduled)
                return;

            IDataVault vault = _dataVault;
            if (vault == null || !_defaultsInitialized || !HandlesReady())
                return;

            SuitIntegrityTuningDTO tuning = ReadSanitizedTuning(vault);
            float quality = ResolveGlobalQualityWeight();
            float slowDeltaSeconds = ResolveSlowTickDeltaSeconds();
            if (slowDeltaSeconds <= 0f)
                return;

            tuning.GlobalQualityWeight = quality;
            _lastTickInterval = ShinobuSuitIntegrityJobMath.ResolveTickInterval(quality);
            _simulationAccumulator = math.min(_simulationAccumulator + slowDeltaSeconds, 2f);
            if (_simulationAccumulator < _lastTickInterval)
                return;

            float dt = math.clamp(_simulationAccumulator, SlowTickNominalSeconds, 1.25f);
            uint frame = ++_frameCounter;
            RefreshPlayerAup();
            uint playerTargetHash = ResolvePlayerDamageTargetHash();
            bool hasPlayerAup = _playerAupValid;
            bool useMock = enableEmergencyMockPressureProfile && !hasPlayerAup;
            if (!SignalBus<CombatDamageSignal>.HasNativeStorage ||
                !SignalBus<MovementAcousticSignal>.HasNativeStorage)
            {
                return;
            }

            if (!TryResolveBuffers(
                    vault,
                    out NativeArray<SuitIntegrityDTO> integrity,
                    out NativeArray<SuitPressureProfileDTO> profiles,
                    out NativeArray<SuitIntegrityTuningDTO> tuningArray,
                    out NativeArray<SuitIntegrityTelemetryEntry> telemetry,
                    out NativeArray<SuitIntegrityVisualDTO> visuals,
                    out NativeArray<SuitHydrostaticMockAupDTO> mockAups))
            {
                return;
            }

            int count = math.min(entityCapacity, integrity.Length);
            count = math.min(count, visuals.Length);
            if (count <= 0)
                return;

            int profileCount = math.min(ShinobuSuitIntegrityConstants.ProfileCapacity, profiles.Length);
            if (!TryLockJobBuffers(vault))
                return;

            bool keepJobGuard = false;
            try
            {
                tuningArray[0] = tuning;
                double resolvedSeaLevelAupY = ResolveRuntimeSeaLevelAupY();
                double3 playerDouble = hasPlayerAup ? _lastPlayerAupDouble : new double3(0d, resolvedSeaLevelAupY, 0d);
                AbsoluteUniversePosition playerAup = hasPlayerAup ? _lastPlayerAup : AbsoluteUniversePosition.FromAbsolutePosition(playerDouble);
                double3 seaLevelAup = new double3(playerDouble.x, resolvedSeaLevelAupY, playerDouble.z);
                long scheduleTimestamp = Stopwatch.GetTimestamp();

                JobHandle handle = new EvaluateHydrostaticPressureJob
                {
                    Integrity = integrity,
                    MockAups = mockAups,
                    PlayerAup = playerAup,
                    PlayerAupOverride = playerDouble,
                    SeaLevelAup = seaLevelAup,
                    Tuning = tuning,
                    Frame = frame,
                    Count = count,
                    UseMockAup = useMock ? (byte)1 : (byte)0,
                    UsePlayerAupOverride = !hasPlayerAup && !useMock ? (byte)1 : (byte)0
                }.Schedule(count, ShinobuSuitIntegrityConstants.FrameJobBatchSize);

                handle = new CalculateStructuralYieldJob
                {
                    Integrity = integrity,
                    Visuals = visuals,
                    Telemetry = telemetry,
                    Profiles = profiles,
                    DamageWriter = SignalBus<CombatDamageSignal>.ParallelWriter,
                    DamageWriterBudget = SignalBus<CombatDamageSignal>.ParallelWriterBudget,
                    AcousticWriter = SignalBus<MovementAcousticSignal>.ParallelWriter,
                    AcousticWriterBudget = SignalBus<MovementAcousticSignal>.ParallelWriterBudget,
                    PlayerAup = playerAup,
                    PlayerImpactAup = playerDouble,
                    Tuning = tuning,
                    PlayerTargetHash = playerTargetHash,
                    Frame = frame,
                    Count = count,
                    ProfileCount = profileCount,
                    TelemetryCursor = _telemetryCursor,
                    DeltaSeconds = dt,
                    TickIntervalSeconds = _lastTickInterval
                }.Schedule(count, ShinobuSuitIntegrityConstants.FrameJobBatchSize, handle);

                _activeJobHandle = handle;
                _scheduledCount = count;
                _jobScheduleTimestamp = scheduleTimestamp;
                _simulationAccumulator = 0f;
                _jobScheduled = true;
                H8Memory.RegisterActiveJob(OwnerSystem, _activeJobHandle);
                keepJobGuard = true;
            }
            finally
            {
                if (!keepJobGuard)
                    UnlockJobBuffers();
            }
        }

        public void LateFrameTick()
        {
            TryFinalizeFrameJobNoWait();
        }

        public bool TryGetIntegrity(int entityIndex, out SuitIntegrityDTO integrity)
        {
            integrity = default;
            if (_jobScheduled)
                return false;

            NativeArray<SuitIntegrityDTO> states = ReadVaultArray(ref _integrityHandle, ShinobuSuitIntegrityConstants.StateBuffer, entityCapacity);
            if (!states.IsCreated || (uint)entityIndex >= (uint)states.Length)
                return false;

            integrity = states[entityIndex];
            return true;
        }

        public bool TryGetVisual(int entityIndex, out SuitIntegrityVisualDTO visual)
        {
            visual = default;
            if (_jobScheduled)
                return false;

            NativeArray<SuitIntegrityVisualDTO> visuals = ReadVaultArray(ref _visualHandle, ShinobuSuitIntegrityConstants.VisualBuffer, entityCapacity);
            if (!visuals.IsCreated || (uint)entityIndex >= (uint)visuals.Length)
                return false;

            visual = visuals[entityIndex];
            return true;
        }

        public bool TryGetLatestTelemetry(out SuitIntegrityTelemetryEntry entry)
        {
            entry = default;
            if (_jobScheduled)
                return false;

            NativeArray<SuitIntegrityTelemetryEntry> telemetry = ReadVaultArray(ref _telemetryHandle, ShinobuSuitIntegrityConstants.TelemetryBuffer, ShinobuSuitIntegrityConstants.TelemetryFrameCount);
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            int index = (_telemetryCursor + telemetry.Length - 1) % telemetry.Length;
            entry = telemetry[index];
            return entry.Frame != 0u;
        }

        public bool TryGetTuning(out SuitIntegrityTuningDTO tuning)
        {
            tuning = default;
            if (_jobScheduled)
                return false;

            NativeArray<SuitIntegrityTuningDTO> tuningArray = ReadVaultArray(ref _tuningHandle, ShinobuSuitIntegrityConstants.TuningBuffer, 1);
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                return false;

            tuning = ShinobuSuitIntegrityJobMath.SanitizeTuning(tuningArray[0]);
            return true;
        }

        public void SetEditorTuning(SuitIntegrityTuningDTO tuning)
        {
            if (_jobScheduled)
                return;

            NativeArray<SuitIntegrityTuningDTO> tuningArray = OpenVaultArray(ref _tuningHandle, ShinobuSuitIntegrityConstants.TuningBuffer, 1);
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                return;

            tuningArray[0] = ShinobuSuitIntegrityJobMath.SanitizeTuning(tuning);
            GenerateMockHydrostaticPressureData();
        }

        public bool SetEquippedSuitHash(int entityIndex, uint suitHash)
        {
            if (_jobScheduled || suitHash == 0u)
                return false;

            NativeArray<SuitIntegrityDTO> states = OpenVaultArray(ref _integrityHandle, ShinobuSuitIntegrityConstants.StateBuffer, entityCapacity);
            if (!states.IsCreated || (uint)entityIndex >= (uint)states.Length)
                return false;

            SuitIntegrityDTO state = states[entityIndex];
            state.EquippedSuitHash = suitHash;
            state.IntegrityFlags |= SuitIntegrityFlags.Initialized;
            states[entityIndex] = state;
            return true;
        }

        public bool GenerateMockHydrostaticPressureData()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            NativeArray<SuitIntegrityTuningDTO> tuningArray = ReadVaultArray(ref _tuningHandle, ShinobuSuitIntegrityConstants.TuningBuffer, 1);
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                return false;

            SuitIntegrityTuningDTO tuning = ShinobuSuitIntegrityJobMath.SanitizeTuning(tuningArray[0]);
            double3 seaLevel = new double3(0d, ResolveRuntimeSeaLevelAupY(), 0d);
            if (System.Threading.Interlocked.CompareExchange(ref s_mockAupScratchBusy, 1, 0) != 0)
                return false;

            try
            {
                int sampleCount = BuildMockHydrostaticPressureScratch(
                    s_mockAupScratchCold.AsSpan(),
                    seaLevel,
                    tuning.MockMaxDepthMeters,
                    tuning.MockDurationSeconds,
                    0u,
                    ShinobuSuitIntegrityConstants.MockPressureSampleCount);
                if (sampleCount <= 0)
                    return false;

                NativeArray<SuitHydrostaticMockAupDTO> mock = OpenVaultArray(ref _mockAupHandle, ShinobuSuitIntegrityConstants.MockAupBuffer, ShinobuSuitIntegrityConstants.MockPressureSampleCount);
                if (!mock.IsCreated)
                    return false;

                if (!vault.TryAcquireMutationGuard(MockAupMutationGuardMask))
                    return false;

                try
                {
                    CommitMockHydrostaticPressure(s_mockAupScratchCold.AsSpan(), sampleCount, mock);
                    return true;
                }
                finally
                {
                    vault.ReleaseMutationGuard(MockAupMutationGuardMask);
                }
            }
            finally
            {
                System.Threading.Volatile.Write(ref s_mockAupScratchBusy, 0);
            }
        }

        private void RebindColdServices()
        {
            _dataVault = GlobalRegistry.DataVault;
            _playerContext = GlobalRegistry.Player;
            _oceanKinematics = GlobalRegistry.OceanKinematics;
            _tickDispatcher = GlobalRegistry.TickDispatcher;
            _lastDispatcherTimeSeconds = -1d;
            ClearTargetHashCache();
            RefreshPlayerCombatTargetHashCold(_playerContext);
        }

        private bool EnsureVaultState()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            entityCapacity = math.max(1, entityCapacity);
            if (HandlesReady())
            {
                TryBindBorrowedStateHandles(vault);
                if (!_defaultsInitialized)
                    InitializeDefaults(vault);
                return true;
            }
            if (!ShinobuSuitIntegrityLayoutGuards.ValidateLayouts())
                return false;

            bool created =
                OpenOrAcquireVaultBuffer(ref _integrityHandle, ShinobuSuitIntegrityConstants.StateBuffer, entityCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireVaultBuffer(ref _profileHandle, ShinobuSuitIntegrityConstants.ProfileBuffer, ShinobuSuitIntegrityConstants.ProfileCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireVaultBuffer(ref _tuningHandle, ShinobuSuitIntegrityConstants.TuningBuffer, 1, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquireVaultBuffer(ref _telemetryHandle, ShinobuSuitIntegrityConstants.TelemetryBuffer, ShinobuSuitIntegrityConstants.TelemetryFrameCount, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquireVaultBuffer(ref _visualHandle, ShinobuSuitIntegrityConstants.VisualBuffer, entityCapacity, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquireVaultBuffer(ref _mockAupHandle, ShinobuSuitIntegrityConstants.MockAupBuffer, ShinobuSuitIntegrityConstants.MockPressureSampleCount, NativeArrayOptions.UninitializedMemory, out _);
            if (!created || !HandlesReady())
                return false;

            TryBindBorrowedStateHandles(vault);
            InitializeDefaults(vault);
            return true;
        }

        private bool HandlesReady()
        {
            return OpenVaultBuffer(ref _integrityHandle, ShinobuSuitIntegrityConstants.StateBuffer, entityCapacity, out _) &&
                   OpenVaultBuffer(ref _profileHandle, ShinobuSuitIntegrityConstants.ProfileBuffer, ShinobuSuitIntegrityConstants.ProfileCapacity, out _) &&
                   OpenVaultBuffer(ref _tuningHandle, ShinobuSuitIntegrityConstants.TuningBuffer, 1, out _) &&
                   OpenVaultBuffer(ref _telemetryHandle, ShinobuSuitIntegrityConstants.TelemetryBuffer, ShinobuSuitIntegrityConstants.TelemetryFrameCount, out _) &&
                   OpenVaultBuffer(ref _visualHandle, ShinobuSuitIntegrityConstants.VisualBuffer, entityCapacity, out _) &&
                   OpenVaultBuffer(ref _mockAupHandle, ShinobuSuitIntegrityConstants.MockAupBuffer, ShinobuSuitIntegrityConstants.MockPressureSampleCount, out _);
        }

        private void InitializeDefaults(IDataVault vault)
        {
            if (_defaultsInitialized)
                return;

            NativeArray<SuitIntegrityTuningDTO> tuningRead = ReadVaultArray(ref _tuningHandle, ShinobuSuitIntegrityConstants.TuningBuffer, 1);
            SuitIntegrityTuningDTO tuning = tuningRead.IsCreated && tuningRead.Length > 0
                ? ShinobuSuitIntegrityJobMath.SanitizeTuning(tuningRead[0])
                : ShinobuSuitIntegrityJobMath.SanitizeTuning(default);
            int defaultProfileCount = BuildDefaultProfiles(s_defaultProfileScratch.AsSpan());
            int stateCount = math.max(0, entityCapacity);
            SuitIntegrityDTO defaultState = new SuitIntegrityDTO
            {
                CurrentIntegrity01 = 1f,
                AppliedPressureATM = ShinobuSuitIntegrityConstants.SurfacePressureAtm,
                MicroFractureAccumulation = 0f,
                EquippedSuitHash = tuning.DefaultSuitHash != 0u ? tuning.DefaultSuitHash : ShinobuSuitIntegrityConstants.StandardSuitHash,
                IntegrityFlags = SuitIntegrityFlags.Initialized
            };

            if (!vault.TryAcquireMutationGuard(DefaultsMutationGuardMask))
                return;

            try
            {
                NativeArray<SuitIntegrityTuningDTO> tuningArray = OpenVaultArray(ref _tuningHandle, ShinobuSuitIntegrityConstants.TuningBuffer, 1);
                if (tuningArray.IsCreated && tuningArray.Length > 0)
                    tuningArray[0] = tuning;

                NativeArray<SuitPressureProfileDTO> profiles = OpenVaultArray(ref _profileHandle, ShinobuSuitIntegrityConstants.ProfileBuffer, ShinobuSuitIntegrityConstants.ProfileCapacity);
                if (profiles.IsCreated)
                    CommitDefaultProfiles(s_defaultProfileScratch.AsSpan(), defaultProfileCount, profiles);

                NativeArray<SuitIntegrityDTO> states = OpenVaultArray(ref _integrityHandle, ShinobuSuitIntegrityConstants.StateBuffer, entityCapacity);
                if (states.IsCreated)
                    CommitDefaultStates(defaultState, stateCount, states);
            }
            finally
            {
                vault.ReleaseMutationGuard(DefaultsMutationGuardMask);
            }

#if UNITY_EDITOR
            LoadCsvProfilesFromDisk(vault);
#endif
            GenerateMockHydrostaticPressureData();
            _defaultsInitialized = true;
        }

        private static int BuildDefaultProfiles(Span<SuitPressureProfileDTO> profiles)
        {
            int length = profiles.Length;
            if (length <= 0)
                return 0;

            profiles[0] = ShinobuSuitIntegrityJobMath.SanitizeProfile(new SuitPressureProfileDTO
            {
                SuitHash = ShinobuSuitIntegrityConstants.StandardSuitHash,
                MaxSafePressureATM = 61f,
                YieldConstant = 0.004f,
                CriticalFractureThreshold = 1f,
                FractureIntegrityDamageRate = 0.08f,
                VisualBucklingGain = 0.26f,
                GroanOverpressureThreshold = 0.06f,
                LowTierYieldScale = 0.65f,
                MiddleTierYieldScale = 0.85f,
                HighTierYieldScale = 1f,
                UltraTierYieldScale = 1.2f,
                ProfileIndex = 0u,
                Flags = SuitIntegrityFlags.Initialized
            }, ShinobuSuitIntegrityConstants.StandardSuitHash);

            int count = 1;
            if (length > 1)
            {
                profiles[1] = ShinobuSuitIntegrityJobMath.SanitizeProfile(new SuitPressureProfileDTO
                {
                    SuitHash = ShinobuSuitIntegrityConstants.ReinforcedSuitHash,
                    MaxSafePressureATM = 181f,
                    YieldConstant = 0.0025f,
                    CriticalFractureThreshold = 1.25f,
                    FractureIntegrityDamageRate = 0.055f,
                    VisualBucklingGain = 0.18f,
                    GroanOverpressureThreshold = 0.08f,
                    LowTierYieldScale = 0.7f,
                    MiddleTierYieldScale = 0.9f,
                    HighTierYieldScale = 1f,
                    UltraTierYieldScale = 1.18f,
                    ProfileIndex = 1u,
                    Flags = SuitIntegrityFlags.Initialized
                }, ShinobuSuitIntegrityConstants.ReinforcedSuitHash);
                count = 2;
            }

            if (length > 2)
            {
                profiles[2] = ShinobuSuitIntegrityJobMath.SanitizeProfile(new SuitPressureProfileDTO
                {
                    SuitHash = ShinobuSuitIntegrityConstants.ExosuitHash,
                    MaxSafePressureATM = 401f,
                    YieldConstant = 0.0014f,
                    CriticalFractureThreshold = 1.6f,
                    FractureIntegrityDamageRate = 0.04f,
                    VisualBucklingGain = 0.13f,
                    GroanOverpressureThreshold = 0.1f,
                    LowTierYieldScale = 0.75f,
                    MiddleTierYieldScale = 0.92f,
                    HighTierYieldScale = 1f,
                    UltraTierYieldScale = 1.15f,
                    ProfileIndex = 2u,
                    Flags = SuitIntegrityFlags.Initialized
                }, ShinobuSuitIntegrityConstants.ExosuitHash);
                count = 3;
            }

            if (length > 3)
            {
                profiles[3] = ShinobuSuitIntegrityJobMath.SanitizeProfile(new SuitPressureProfileDTO
                {
                    SuitHash = ShinobuSuitIntegrityConstants.SubmarineHullHash,
                    MaxSafePressureATM = 651f,
                    YieldConstant = 0.0009f,
                    CriticalFractureThreshold = 2.1f,
                    FractureIntegrityDamageRate = 0.03f,
                    VisualBucklingGain = 0.1f,
                    GroanOverpressureThreshold = 0.12f,
                    LowTierYieldScale = 0.8f,
                    MiddleTierYieldScale = 0.95f,
                    HighTierYieldScale = 1f,
                    UltraTierYieldScale = 1.12f,
                    ProfileIndex = 3u,
                    Flags = SuitIntegrityFlags.Initialized
                }, ShinobuSuitIntegrityConstants.SubmarineHullHash);
                count = 4;
            }

            return count;
        }

        private static void CommitDefaultProfiles(
            ReadOnlySpan<SuitPressureProfileDTO> defaultProfiles,
            int defaultProfileCount,
            NativeArray<SuitPressureProfileDTO> profiles)
        {
            int safeCount = math.min(math.min(defaultProfileCount, defaultProfiles.Length), profiles.Length);
            for (int i = 0; i < safeCount; i++)
                profiles[i] = defaultProfiles[i];
            for (int i = safeCount; i < profiles.Length; i++)
                profiles[i] = default;
        }

        private static void CommitDefaultStates(
            in SuitIntegrityDTO defaultState,
            int requestedCount,
            NativeArray<SuitIntegrityDTO> states)
        {
            int safeCount = math.min(math.max(0, requestedCount), states.Length);
            for (int i = 0; i < safeCount; i++)
                states[i] = defaultState;
        }

        private static int BuildMockHydrostaticPressureScratch(
            Span<SuitHydrostaticMockAupDTO> scratch,
            double3 seaLevelAup,
            float maxDepthMeters,
            float durationSeconds,
            uint frameBase,
            int requestedCount)
        {
            int count = math.min(math.max(0, requestedCount), scratch.Length);
            if (count <= 0)
                return 0;

            float denom = math.max(1f, count - 1);
            float maxDepth = math.max(0f, maxDepthMeters);
            float duration = math.max(0f, durationSeconds);
            for (int index = 0; index < count; index++)
            {
                float t = math.saturate(index * math.rcp(denom));
                float depth = maxDepth * t;
                double3 playerAup = seaLevelAup;
                playerAup.y -= depth;
                scratch[index] = new SuitHydrostaticMockAupDTO
                {
                    PlayerAup = playerAup,
                    SeaLevelAup = seaLevelAup,
                    TimeSeconds = duration * t,
                    DepthMeters = depth,
                    Frame = frameBase + (uint)index,
                    Flags = SuitIntegrityFlags.MockProfile
                };
            }

            return count;
        }

        private static double ResolveSeaLevelAupY(double candidateSeaLevelAupY)
        {
            return math.isfinite(candidateSeaLevelAupY) &&
                   math.abs(candidateSeaLevelAupY) > 0.0001d &&
                   math.abs(candidateSeaLevelAupY) <= 1000d
                ? candidateSeaLevelAupY
                : DefaultSeaLevelAupY;
        }

        private double ResolveRuntimeSeaLevelAupY()
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematics;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveSeaLevelAupY(oceanKinematics.SeaLevel, out double seaLevelAupY))
            {
                return seaLevelAupY;
            }

            return ResolveSeaLevelAupY(this.seaLevelAupY);
        }

        private static bool TryResolveSeaLevelAupY(float candidateSeaLevelY, out double seaLevelAupY)
        {
            if (math.isfinite(candidateSeaLevelY) &&
                math.abs(candidateSeaLevelY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                seaLevelAupY = candidateSeaLevelY;
                return true;
            }

            seaLevelAupY = DefaultSeaLevelAupY;
            return false;
        }

        private static void CommitMockHydrostaticPressure(
            ReadOnlySpan<SuitHydrostaticMockAupDTO> source,
            int sourceCount,
            NativeArray<SuitHydrostaticMockAupDTO> destination)
        {
            int safeCount = math.min(math.min(sourceCount, source.Length), destination.Length);
            for (int i = 0; i < safeCount; i++)
                destination[i] = source[i];
        }

        private SuitIntegrityTuningDTO ReadSanitizedTuning(IDataVault vault)
        {
            NativeArray<SuitIntegrityTuningDTO> tuningArray = OpenVaultArray(ref _tuningHandle, ShinobuSuitIntegrityConstants.TuningBuffer, 1);
            SuitIntegrityTuningDTO tuning = tuningArray.IsCreated && tuningArray.Length > 0
                ? tuningArray[0]
                : default;
            return ShinobuSuitIntegrityJobMath.SanitizeTuning(tuning);
        }

        private void RefreshPlayerAup()
        {
            _playerAupValid = false;
            TryRefreshPlayerTargetHashFromMetabolism();
            if (TryRefreshPlayerAupFromKinematicVault())
                return;

            IPlayerRuntimeContext player = _playerContext;
            if (player == null || !player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
                return;

            AbsoluteUniversePosition aup = snapshot.Aup;
            double3 playerAup = ShinobuSuitIntegrityJobMath.ToAbsoluteDouble3(in aup);
            if (!math.all(math.isfinite(playerAup)))
                return;

            _lastPlayerAup = aup;
            _lastPlayerAupDouble = playerAup;
            _playerAupValid = true;
        }

        private bool TryRefreshPlayerAupFromKinematicVault()
        {
            NativeArray<LockstepPlayerKinematicState> playerStates = BorrowVaultArray(
                ref _playerKinematicStateHandle,
                BufferID.PlayerKinematicState,
                SystemID.GameplayPlayer,
                1);
            if (!playerStates.IsCreated || playerStates.Length <= 0)
                return false;

            LockstepPlayerKinematicState state = playerStates[0];
            if (state.Frame == 0u || !math.all(math.isfinite(state.LocalPosition)))
                return false;

            AbsoluteUniversePosition aup = new AbsoluteUniversePosition
            {
                GridX = state.SectorX,
                GridY = state.SectorY,
                GridZ = state.SectorZ,
                LocalX = state.LocalPosition.x,
                LocalY = state.LocalPosition.y,
                LocalZ = state.LocalPosition.z
            };
            double3 playerAup = ShinobuSuitIntegrityJobMath.ToAbsoluteDouble3(in aup);
            if (!math.all(math.isfinite(playerAup)))
                return false;

            _lastPlayerAup = aup;
            _lastPlayerAupDouble = playerAup;
            _playerAupValid = true;
            if (state.StableId != 0u)
                _kinematicDamageTargetHash = state.StableId;
            return true;
        }

        private void TryRefreshPlayerTargetHashFromMetabolism()
        {
            NativeArray<MetabolicStateDTO> states = BorrowVaultArray(
                ref _metabolismStateHandle,
                ShinobuMetabolismConstants.MetabolismStatesBuffer,
                SystemID.GameplayPlayer,
                1);
            if (!states.IsCreated || states.Length <= 0)
                return;

            uint entityHash = states[0].EntityHashID;
            _metabolicDamageTargetHash = entityHash;
        }

        private uint ResolvePlayerDamageTargetHash()
        {
            if (_metabolicDamageTargetHash != 0u)
                return _metabolicDamageTargetHash;

            if (_kinematicDamageTargetHash != 0u)
                return _kinematicDamageTargetHash;

            return _coldDamageTargetHash != 0u
                ? _coldDamageTargetHash
                : ShinobuSuitIntegrityConstants.PlayerTargetHash;
        }

        private void RefreshPlayerCombatTargetHashCold(IPlayerRuntimeContext player)
        {
            GameObject playerObject = player != null ? player.PlayerObject : null;
            if (playerObject != null)
            {
                uint entityHash = unchecked((uint)EntityId.ToULong(playerObject.GetEntityId()));
                if (entityHash != 0u)
                    _coldDamageTargetHash = entityHash;
            }
        }

        private void ClearTargetHashCache()
        {
            _metabolicDamageTargetHash = 0u;
            _kinematicDamageTargetHash = 0u;
            _coldDamageTargetHash = 0u;
        }

        private void TryFinalizeFrameJobNoWait()
        {
            if (!_jobScheduled || !_activeJobHandle.IsCompleted)
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

        private void FinishFrameJobCompletion()
        {
            float elapsedMicroseconds = ResolveElapsedMicroseconds(_jobScheduleTimestamp, Stopwatch.GetTimestamp());
            int completedTelemetryCursor = _telemetryCursor;
            bool publishVisualSync = false;
            Vector4 visualSyncPayload = default;

            try
            {
                IDataVault vault = _dataVault;
                if (vault != null)
                {
                    PatchLatestTelemetryExecutionTime(elapsedMicroseconds);
                    publishVisualSync = TryCaptureVisualSyncScalars(out visualSyncPayload);
                }
            }
            finally
            {
                UnlockJobBuffers();
                _scheduledCount = 0;
                _jobScheduled = false;
            }

            if (publishVisualSync)
                PublishVisualSyncScalars(visualSyncPayload);

            TryDumpAutopsyIfFaulted(completedTelemetryCursor);
            _telemetryCursor++;
            if (_telemetryCursor >= ShinobuSuitIntegrityConstants.TelemetryFrameCount)
                _telemetryCursor %= ShinobuSuitIntegrityConstants.TelemetryFrameCount;
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

        private static float ResolveElapsedMicroseconds(long startTimestamp, long endTimestamp)
        {
            long rawDelta = endTimestamp - startTimestamp;
            long delta = rawDelta > 0L ? rawDelta : 0L;
            double microseconds = delta * 1000000.0 / Stopwatch.Frequency;
            return math.isfinite(microseconds) ? (float)math.min(microseconds, float.MaxValue) : 0f;
        }

        private void PatchLatestTelemetryExecutionTime(float elapsedMicroseconds)
        {
            NativeArray<SuitIntegrityTelemetryEntry> telemetry = OpenVaultArray(ref _telemetryHandle, ShinobuSuitIntegrityConstants.TelemetryBuffer, ShinobuSuitIntegrityConstants.TelemetryFrameCount);
            NativeArray<SuitIntegrityTuningDTO> tuningArray = OpenVaultArray(ref _tuningHandle, ShinobuSuitIntegrityConstants.TuningBuffer, 1);
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            int telemetryIndex = _telemetryCursor % telemetry.Length;
            SuitIntegrityTelemetryEntry entry = telemetry[telemetryIndex];
            entry.ExecutionMicroseconds = math.max(0f, ShinobuSuitIntegrityJobMath.SanitizeFinite(elapsedMicroseconds, 0f));
            float budget = ShinobuSuitIntegrityConstants.DefaultTickBudgetMicroseconds;
            if (tuningArray.IsCreated && tuningArray.Length > 0)
                budget = ShinobuSuitIntegrityJobMath.SanitizeTuning(tuningArray[0]).TickBudgetMicroseconds;
            if (entry.ExecutionMicroseconds > budget)
                entry.Flags |= SuitIntegrityFlags.OverBudget;
            telemetry[telemetryIndex] = entry;
        }

        private bool TryCaptureVisualSyncScalars(out Vector4 vector)
        {
            vector = default;
            NativeArray<SuitIntegrityVisualDTO> visuals = OpenVaultArray(ref _visualHandle, ShinobuSuitIntegrityConstants.VisualBuffer, entityCapacity);
            if (!visuals.IsCreated || visuals.Length <= 0)
                return false;

            SuitIntegrityVisualDTO visual = visuals[0];
            vector = new Vector4(
                math.saturate(visual.Buckling01),
                math.max(0f, visual.OverpressureScalar),
                math.saturate(1f - visual.CurrentIntegrity01),
                math.saturate(visual.GlobalQualityWeight));
            return true;
        }

        private static void PublishVisualSyncScalars(Vector4 vector)
        {
            HectonShaderGlobalDataVaultBridge.PublishSuitCrushDearLie(vector);
        }

        private void TryDumpAutopsyIfFaulted(int telemetryCursor)
        {
            if (_autopsyDumped)
                return;

            NativeArray<SuitIntegrityTelemetryEntry> telemetry = ReadVaultArray(ref _telemetryHandle, ShinobuSuitIntegrityConstants.TelemetryBuffer, ShinobuSuitIntegrityConstants.TelemetryFrameCount);
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            int latestIndex = telemetryCursor % telemetry.Length;
            SuitIntegrityTelemetryEntry entry = telemetry[latestIndex];
            bool faulted = (entry.Flags & (SuitIntegrityFlags.NonFinitePressure | SuitIntegrityFlags.OverBudget | SuitIntegrityFlags.Imploded)) != 0u;
            if (!faulted)
                return;

            _autopsyDumped = DumpAutopsyReport(telemetry);
        }

        private bool DumpAutopsyReport(NativeArray<SuitIntegrityTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated)
                return false;

            try
            {
                int byteCount = telemetry.Length * UnsafeUtility.SizeOf<SuitIntegrityTelemetryEntry>();
                int totalBytes = 32 + byteCount;
                NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(ShinobuSuitIntegrityRuntime),
                    "shinobuSuitIntegrityAutopsyPayload");
                try
                {
                    byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                    Span<byte> header = new Span<byte>(scratchPtr, 32);
                    WriteUInt64LittleEndian(header.Slice(0, 8), DumpMagic);
                    WriteUInt32LittleEndian(header.Slice(8, 4), DumpVersion);
                    WriteUInt32LittleEndian(header.Slice(12, 4), (uint)telemetry.Length);
                    WriteUInt32LittleEndian(header.Slice(16, 4), (uint)UnsafeUtility.SizeOf<SuitIntegrityTelemetryEntry>());
                    WriteUInt32LittleEndian(header.Slice(20, 4), unchecked((uint)_telemetryCursor));
                    WriteUInt32LittleEndian(header.Slice(24, 4), ShinobuSuitIntegrityConstants.SourceHash);
                    WriteUInt32LittleEndian(header.Slice(28, 4), _frameCounter);

                    void* telemetryPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    UnsafeUtility.MemCpy(scratchPtr + 32, telemetryPtr, byteCount);
                    return NativeFaultDumpWriter.TryWriteAll(_dumpPath, payload, totalBytes);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(ShinobuSuitIntegrityRuntime),
                        "shinobuSuitIntegrityAutopsyPayload");
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
        }

#if UNITY_EDITOR
        private void LoadCsvProfilesFromDisk(IDataVault vault)
        {
            if (string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath))
                return;

            DateTime stamp = File.GetLastWriteTimeUtc(_csvPath);
            if (stamp.Ticks == 0L || stamp.Ticks == _csvLastWriteTicks)
                return;

            if (System.Threading.Interlocked.CompareExchange(ref s_profileCsvScratchBusy, 1, 0) != 0)
                return;

            try
            {
                int read = ReadCsvBytesCold(_csvPath, s_profileCsvScratchCold, ShinobuSuitIntegrityConstants.CsvMaxBytes);
                if (read <= 0)
                    return;

                int profileCount = ParseSuitProfilesCsv(
                    s_profileCsvScratchCold.AsSpan(0, read),
                    s_profileImportScratch.AsSpan());
                if (profileCount <= 0)
                    return;

                if (vault == null)
                    return;

                NativeArray<SuitPressureProfileDTO> profiles = OpenVaultArray(ref _profileHandle, ShinobuSuitIntegrityConstants.ProfileBuffer, ShinobuSuitIntegrityConstants.ProfileCapacity);
                if (!profiles.IsCreated)
                    return;

                if (!vault.TryAcquireMutationGuard(ProfileCsvMutationGuardMask))
                    return;

                try
                {
                    CommitSuitProfilesCsv(s_profileImportScratch.AsSpan(), profileCount, profiles);
                    _csvLastWriteTicks = stamp.Ticks;
                }
                finally
                {
                    vault.ReleaseMutationGuard(ProfileCsvMutationGuardMask);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            finally
            {
                System.Threading.Volatile.Write(ref s_profileCsvScratchBusy, 0);
            }
        }

        private static int ReadCsvBytesCold(string path, byte[] scratch, int maxBytes)
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long boundedLength = stream.Length < maxBytes ? stream.Length : maxBytes;
            int byteCount = boundedLength > scratch.Length ? scratch.Length : (int)boundedLength;
            if (byteCount <= 0)
                return 0;

            int totalRead = 0;
            while (totalRead < byteCount)
            {
                int read = stream.Read(scratch, totalRead, byteCount - totalRead);
                if (read <= 0)
                    break;
                totalRead += read;
            }

            return totalRead;
        }

        private static int ParseSuitProfilesCsv(ReadOnlySpan<byte> bytes, Span<SuitPressureProfileDTO> profiles)
        {
            int cursor = 0;
            int profileIndex = 0;
            while (cursor < bytes.Length && profileIndex < profiles.Length)
            {
                int lineStart = cursor;
                while (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                    cursor++;

                int lineEnd = cursor;
                while (cursor < bytes.Length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                    cursor++;

                if (TryParseProfileLine(bytes.Slice(lineStart, lineEnd - lineStart), (uint)profileIndex, out SuitPressureProfileDTO profile))
                {
                    profiles[profileIndex] = profile;
                    profileIndex++;
                }
            }

            return profileIndex;
        }

        private static void CommitSuitProfilesCsv(
            ReadOnlySpan<SuitPressureProfileDTO> parsedProfiles,
            int parsedCount,
            NativeArray<SuitPressureProfileDTO> profiles)
        {
            int safeCount = math.min(math.min(parsedCount, parsedProfiles.Length), profiles.Length);
            for (int i = 0; i < safeCount; i++)
                profiles[i] = parsedProfiles[i];
        }
#endif

        private static bool TryParseProfileLine(ReadOnlySpan<byte> line, uint profileIndex, out SuitPressureProfileDTO profile)
        {
            profile = default;
            line = Trim(line);
            if (line.Length <= 0 || line[0] == (byte)'#')
                return false;

            int cursor = 0;
            ReadOnlySpan<byte> name = NextToken(line, ref cursor);
            uint nameHash = HashLowerAscii(name);
            if (nameHash == _HeaderSuitNameHash || nameHash == _HeaderNameHash || name.Length <= 0)
                return false;

            if (!TryParseAsciiFloat(NextToken(line, ref cursor), out float safePressureAtm))
                return false;

            TryParseAsciiFloat(NextToken(line, ref cursor), out float yieldConstant);
            TryParseAsciiFloat(NextToken(line, ref cursor), out float fractureThreshold);
            TryParseAsciiFloat(NextToken(line, ref cursor), out float damageRate);
            TryParseAsciiFloat(NextToken(line, ref cursor), out float visualGain);
            TryParseAsciiFloat(NextToken(line, ref cursor), out float groanThreshold);

            profile = ShinobuSuitIntegrityJobMath.SanitizeProfile(new SuitPressureProfileDTO
            {
                SuitHash = nameHash,
                MaxSafePressureATM = safePressureAtm,
                YieldConstant = yieldConstant,
                CriticalFractureThreshold = fractureThreshold,
                FractureIntegrityDamageRate = damageRate,
                VisualBucklingGain = visualGain,
                GroanOverpressureThreshold = groanThreshold,
                LowTierYieldScale = 0.65f,
                MiddleTierYieldScale = 0.85f,
                HighTierYieldScale = 1f,
                UltraTierYieldScale = 1.2f,
                ProfileIndex = profileIndex,
                Flags = SuitIntegrityFlags.CsvProfile
            }, nameHash);
            return true;
        }

        private static ReadOnlySpan<byte> NextToken(ReadOnlySpan<byte> line, ref int cursor)
        {
            if (cursor >= line.Length)
                return ReadOnlySpan<byte>.Empty;

            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;

            return Trim(line.Slice(start, end - start));
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start < value.Length && IsCsvSpace(value[start]))
                start++;
            while (end >= start && IsCsvSpace(value[end]))
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private bool TryResolveBuffers(
            IDataVault vault,
            out NativeArray<SuitIntegrityDTO> integrity,
            out NativeArray<SuitPressureProfileDTO> profiles,
            out NativeArray<SuitIntegrityTuningDTO> tuning,
            out NativeArray<SuitIntegrityTelemetryEntry> telemetry,
            out NativeArray<SuitIntegrityVisualDTO> visuals,
            out NativeArray<SuitHydrostaticMockAupDTO> mockAups)
        {
            integrity = OpenVaultArray(ref _integrityHandle, ShinobuSuitIntegrityConstants.StateBuffer, entityCapacity);
            profiles = OpenVaultArray(ref _profileHandle, ShinobuSuitIntegrityConstants.ProfileBuffer, ShinobuSuitIntegrityConstants.ProfileCapacity);
            tuning = OpenVaultArray(ref _tuningHandle, ShinobuSuitIntegrityConstants.TuningBuffer, 1);
            telemetry = OpenVaultArray(ref _telemetryHandle, ShinobuSuitIntegrityConstants.TelemetryBuffer, ShinobuSuitIntegrityConstants.TelemetryFrameCount);
            visuals = OpenVaultArray(ref _visualHandle, ShinobuSuitIntegrityConstants.VisualBuffer, entityCapacity);
            mockAups = OpenVaultArray(ref _mockAupHandle, ShinobuSuitIntegrityConstants.MockAupBuffer, ShinobuSuitIntegrityConstants.MockPressureSampleCount);
            return integrity.IsCreated &&
                   profiles.IsCreated &&
                   tuning.IsCreated &&
                   tuning.Length > 0 &&
                   telemetry.IsCreated &&
                   visuals.IsCreated &&
                   mockAups.IsCreated;
        }

        private bool OpenOrAcquireVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            IDataVault vault = _dataVault;
            if (OpenVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle))
                {
                    buffer = default;
                    return false;
                }

                return OpenVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
            }

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystem, options);
            return OpenVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private NativeArray<T> OpenVaultArray<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return OpenVaultBuffer(_dataVault, ref handle, bufferId, requiredLength, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private NativeArray<T> ReadVaultArray<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return ReadVaultBuffer(_dataVault, ref handle, bufferId, requiredLength, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private bool OpenVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            return OpenVaultBuffer(_dataVault, ref handle, bufferId, requiredLength, out buffer);
        }

        private static bool OpenVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool ReadVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsVaultHandle(in handle, bufferId) ||
                !vault.TryReadHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private void TryBindBorrowedStateHandles(IDataVault vault)
        {
            TryBindBorrowedVaultHandle(
                vault,
                BufferID.PlayerKinematicState,
                SystemID.GameplayPlayer,
                1,
                ref _playerKinematicStateHandle);
            TryBindBorrowedVaultHandle(
                vault,
                ShinobuMetabolismConstants.MetabolismStatesBuffer,
                SystemID.GameplayPlayer,
                1,
                ref _metabolismStateHandle);
        }

        private NativeArray<T> BorrowVaultArray<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID expectedOwner,
            int requiredLength) where T : struct
        {
            IDataVault vault = _dataVault;
            if (ReadBorrowedVaultBuffer(vault, in handle, bufferId, expectedOwner, requiredLength, out NativeArray<T> buffer))
                return buffer;

            handle = default;
            if (!TryBindBorrowedVaultHandle(vault, bufferId, expectedOwner, requiredLength, ref handle))
                return default;

            return ReadBorrowedVaultBuffer(vault, in handle, bufferId, expectedOwner, requiredLength, out buffer)
                ? buffer
                : default;
        }

        private static bool TryBindBorrowedVaultHandle<T>(
            IDataVault vault,
            BufferID bufferId,
            SystemID expectedOwner,
            int requiredLength,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (ReadBorrowedVaultBuffer(vault, in handle, bufferId, expectedOwner, requiredLength, out _))
                return true;

            handle = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (!vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> candidate))
                return false;

            if (!ReadBorrowedVaultBuffer(vault, in candidate, bufferId, expectedOwner, requiredLength, out _))
                return false;

            handle = candidate;
            return true;
        }

        private static bool ReadBorrowedVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID expectedOwner,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                handle.BufferID != (uint)bufferId ||
                handle.SystemID != (uint)expectedOwner ||
                handle.Generation == 0u ||
                !vault.TryReadHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u;
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            if (_jobGuardHeld || vault == null || !vault.TryAcquireMutationGuard(JobMutationGuardMask))
                return false;

            _jobGuardVault = vault;
            _jobGuardHeld = true;
            return true;
        }

        private void UnlockJobBuffers()
        {
            if (!_jobGuardHeld)
                return;

            IDataVault vault = _jobGuardVault;
            _jobGuardVault = null;
            _jobGuardHeld = false;
            if (vault != null)
                vault.ReleaseMutationGuard(JobMutationGuardMask);
        }

        private void TryRegisterTicks()
        {
            if (!_registeredSlow)
                _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);

            if (!_registeredSlow || !_registeredLateFrame)
                TryUnregisterTicks();
        }

        private void TryUnregisterTicks()
        {
            if (_registeredSlow)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                _registeredSlow = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
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

            if (GlobalRegistry.TryUnregisterHotSwapListener(this))
                _registeredHotSwap = false;
        }

        private void ClearCachedHandles()
        {
            _integrityHandle = default;
            _profileHandle = default;
            _tuningHandle = default;
            _telemetryHandle = default;
            _visualHandle = default;
            _mockAupHandle = default;
            _playerKinematicStateHandle = default;
            _metabolismStateHandle = default;
            _jobScheduled = false;
            _jobGuardHeld = false;
            _jobGuardVault = null;
            _scheduledCount = 0;
        }

        private void ReleaseVaultHandles()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            ReleaseVaultHandle(vault, ref _integrityHandle);
            ReleaseVaultHandle(vault, ref _profileHandle);
            ReleaseVaultHandle(vault, ref _tuningHandle);
            ReleaseVaultHandle(vault, ref _telemetryHandle);
            ReleaseVaultHandle(vault, ref _visualHandle);
            ReleaseVaultHandle(vault, ref _mockAupHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : 1f);
        }

        private float ResolveSlowTickDeltaSeconds()
        {
            ITickDispatcher dispatcher = _tickDispatcher;
            if (dispatcher != null && dispatcher.SimulationPaused)
                return 0f;

            if (dispatcher != null)
            {
                H8TimeSnapshot snapshot = dispatcher.TimeSnapshot;
                if (double.IsFinite(snapshot.Time))
                {
                    double delta = _lastDispatcherTimeSeconds >= 0d
                        ? snapshot.Time - _lastDispatcherTimeSeconds
                        : SlowTickNominalSeconds;
                    _lastDispatcherTimeSeconds = snapshot.Time;
                    if (double.IsFinite(delta) && delta > 0d)
                        return math.clamp((float)delta, 0.0001f, 2f);
                }
            }

            return SlowTickNominalSeconds;
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

        private static void WriteUInt64LittleEndian(Span<byte> span, ulong value)
        {
            span[0] = (byte)value;
            span[1] = (byte)(value >> 8);
            span[2] = (byte)(value >> 16);
            span[3] = (byte)(value >> 24);
            span[4] = (byte)(value >> 32);
            span[5] = (byte)(value >> 40);
            span[6] = (byte)(value >> 48);
            span[7] = (byte)(value >> 56);
        }

        private static void WriteUInt32LittleEndian(Span<byte> span, uint value)
        {
            span[0] = (byte)value;
            span[1] = (byte)(value >> 8);
            span[2] = (byte)(value >> 16);
            span[3] = (byte)(value >> 24);
        }
    }
}
