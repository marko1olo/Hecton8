using System;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Physiology
{
    [DisallowMultipleComponent]
    public sealed unsafe class ShinobuSensoryImpairmentRuntime : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const SystemID OwnerSystem = SystemID.GameplayPlayer;
        private const ulong InputMutationGuardMask = 1UL << 44;
        private const string CsvRelativePath = "sensory_impairment_profiles.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_322.bin";

        [Header("Emergency Mock")]
        [SerializeField] private bool enableEmergencyMockToxicity;
        [SerializeField, Min(1f)] private float mockMaxDepthMeters = ShinobuSensoryImpairmentConstants.DefaultMockMaxDepthMeters;

        private VaultGenerationHandle<SensoryImpairmentDTO> _impairmentHandle;
        private VaultGenerationHandle<SensoryImpairmentTuningDTO> _tuningHandle;
        private VaultGenerationHandle<SensoryImpairmentTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<SensoryImpairmentProfileDTO> _profilesHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<SensoryInputDriftDebugDTO> _driftDebugHandle;
        private VaultGenerationHandle<GasPhysiologyStateDTO> _gasStateHandle;
        private VaultGenerationHandle<MockEnvironmentVitalsSignal> _environmentHandle;
        private VaultGenerationHandle<InputStateDTO> _currentInputHandle;
        private VaultGenerationHandle<PredictedInputDTO> _predictedInputHandle;
        private VaultGenerationHandle<PredictedInputAupTargetDTO> _predictedAupTargetHandle;

        private IDataVault _dataVault;
        private IPlayerRuntimeContext _playerContext;
        private PreSimulationPhaseSystem _preSimulationPhase;
        private VisualSyncPhaseSystem _visualSyncPhase;
        private string _csvPath;
        private string _dumpPath;
        private long _csvLastWriteTicks;
        private uint _frameCounter;
        private int _telemetryCursor;
        private bool _registeredSlow;
        private bool _registeredLateFrame;
        private bool _registeredPreSimulation;
        private bool _registeredVisualSync;
        private bool _registeredHotSwap;
        private bool _defaultsInitialized;
        private bool _autopsyDumped;

        private void Awake()
        {
            _csvPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", CsvRelativePath));
            _dumpPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DumpRelativePath));
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            TryRegisterHotSwapListener();
            RebindColdServices();
            if (EnsureVaultState())
                TryRegisterRuntimeRoutes();
        }

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            RebindColdServices();
            if (EnsureVaultState())
                TryRegisterRuntimeRoutes();
        }

        private void OnDisable()
        {
            TryUnregisterRuntimeRoutes();
            TryUnregisterHotSwapListener();
            ClearCachedHandles();
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                _dataVault = currentService as IDataVault;
                ClearCachedHandles();
                _defaultsInitialized = false;
                _autopsyDumped = false;
                if (_dataVault != null && EnsureVaultState())
                    TryRegisterRuntimeRoutes();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                _playerContext = currentService as IPlayerRuntimeContext;
        }

        public void SlowTick()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !EnsureVaultState())
                return;

            if (RunEvaluation(vault, ResolveGlobalQualityWeight()))
                PatchLatestTelemetryGas(vault, -1f);

            TryLoadCsvProfilesCold(vault);
        }

        public void LateFrameTick()
        {
            RunVisualSyncFrame();
        }

        private void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            RunInputFrame(math.clamp(timing.FrameDelta, 0.0001f, 0.1f), ResolveFrameId(timing.FrameId));
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            RunVisualSyncFrame();
        }

        private void RunInputFrame(float deltaTime, uint frame)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !_defaultsInitialized)
                return;

            if (RunInputCorruption(vault, deltaTime, frame, ResolveGlobalQualityWeight(), out float corruptionMicroseconds))
                PatchLatestTelemetryGas(vault, corruptionMicroseconds);
        }

        private void RunVisualSyncFrame()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !_defaultsInitialized)
                return;

            PublishVisualScalars(vault);
            TryDumpAutopsyIfFaulted(vault);
        }

        public bool TryGetSensoryImpairment(out SensoryImpairmentDTO impairment)
        {
            impairment = default;
            return TryReadCachedBuffer(in _impairmentHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentBuffer, ShinobuSensoryImpairmentConstants.DefaultEntityCapacity, out NativeArray<SensoryImpairmentDTO> rows) &&
                   rows.Length > 0 &&
                   TryReadFirst(rows, out impairment);
        }

        public bool TryGetTuning(out SensoryImpairmentTuningDTO tuning)
        {
            tuning = default;
            if (!TryReadCachedBuffer(in _tuningHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentTuningBuffer, 1, out NativeArray<SensoryImpairmentTuningDTO> rows) ||
                rows.Length <= 0)
            {
                return false;
            }

            tuning = ShinobuSensoryImpairmentJobMath.SanitizeTuning(rows[0]);
            return true;
        }

        public bool SetEditorTuning(SensoryImpairmentTuningDTO tuning)
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!TryResolveOwnBuffer(ref _tuningHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentTuningBuffer, 1, out NativeArray<SensoryImpairmentTuningDTO> rows) ||
                rows.Length <= 0)
            {
                return false;
            }

            bool tuningLocked = false;
            try
            {
                if (!vault.TryLockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentTuningBuffer, OwnerSystem))
                    return false;
                tuningLocked = true;

                void* tuningPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rows);
                ref SensoryImpairmentTuningDTO target = ref UnsafeUtility.AsRef<SensoryImpairmentTuningDTO>(tuningPtr);
                target = ShinobuSensoryImpairmentJobMath.SanitizeTuning(tuning);
                return true;
            }
            finally
            {
                if (tuningLocked)
                    vault.TryUnlockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentTuningBuffer, OwnerSystem);
            }
        }

        public bool TryGetLatestTelemetry(out SensoryImpairmentTelemetryEntry entry)
        {
            entry = default;
            if (!TryReadCachedBuffer(in _telemetryHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentTelemetryBuffer, ShinobuSensoryImpairmentConstants.TelemetryFrameCount, out NativeArray<SensoryImpairmentTelemetryEntry> rows) ||
                rows.Length <= 0)
            {
                return false;
            }

            int index = (_telemetryCursor + rows.Length - 1) % rows.Length;
            entry = rows[index];
            return true;
        }

        public bool TryGetInputDriftDebug(out SensoryInputDriftDebugDTO debug)
        {
            debug = default;
            if (!TryReadCachedBuffer(in _driftDebugHandle, ShinobuSensoryImpairmentConstants.SensoryInputDriftDebugBuffer, 1, out NativeArray<SensoryInputDriftDebugDTO> rows) ||
                rows.Length <= 0)
            {
                return false;
            }

            debug = rows[0];
            return true;
        }

#if UNITY_EDITOR
        public int CopyTelemetrySeriesForEditor(float[] hypoxia, float[] narcosis)
        {
            if (hypoxia == null ||
                narcosis == null ||
                hypoxia.Length <= 0 ||
                narcosis.Length <= 0 ||
                !TryReadCachedBuffer(in _telemetryHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentTelemetryBuffer, ShinobuSensoryImpairmentConstants.TelemetryFrameCount, out NativeArray<SensoryImpairmentTelemetryEntry> rows) ||
                rows.Length <= 0)
            {
                return 0;
            }

            int count = math.min(math.min(hypoxia.Length, narcosis.Length), rows.Length);
            int start = (_telemetryCursor + rows.Length - count) % rows.Length;
            for (int i = 0; i < count; i++)
            {
                int index = start + i;
                if (index >= rows.Length)
                    index -= rows.Length;
                SensoryImpairmentTelemetryEntry entry = rows[index];
                hypoxia[i] = math.saturate(entry.HypoxiaVignette01);
                narcosis[i] = math.saturate(entry.NarcosisDrift01);
            }

            return count;
        }
#endif

        public bool InjectMockGas(float oxygenPartialPressureAtm, float nitrogenPartialPressureAtm, float carbonDioxidePartialPressureAtm)
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!TryResolveExistingBuffer(ref _gasStateHandle, ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer, 1, out NativeArray<GasPhysiologyStateDTO> gasStates) ||
                gasStates.Length <= 0)
            {
                return false;
            }

            bool gasLocked = false;
            try
            {
                if (!vault.TryLockBuffer(ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer, OwnerSystem))
                    return false;
                gasLocked = true;

                GasPhysiologyStateDTO gas = gasStates[0];
                gas.OxygenPartialPressure = math.max(0f, ShinobuSensoryImpairmentJobMath.SanitizeFinite(oxygenPartialPressureAtm, ShinobuPhysiologyConstants.SurfaceOxygenPartialPressureAtm));
                gas.NitrogenPartialPressure = math.max(0f, ShinobuSensoryImpairmentJobMath.SanitizeFinite(nitrogenPartialPressureAtm, ShinobuPhysiologyConstants.SurfaceNitrogenPartialPressureAtm));
                gas.CarbonDioxidePartialPressure = math.max(0f, ShinobuSensoryImpairmentJobMath.SanitizeFinite(carbonDioxidePartialPressureAtm, ShinobuPhysiologyConstants.CarbonDioxideFraction));
                gas.Flags |= ShinobuPhysiologyFlags.EmergencyMockCoefficients;
                gasStates[0] = gas;
                return true;
            }
            finally
            {
                if (gasLocked)
                    vault.TryUnlockBuffer(ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer, OwnerSystem);
            }
        }

        private void RebindColdServices()
        {
            _dataVault = GlobalRegistry.DataVault;
            _playerContext = GlobalRegistry.Player;
        }

        private bool EnsureVaultState()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (HandlesReady())
                return true;
            if (!ShinobuSensoryImpairmentLayoutGuards.ValidateSensoryLayouts())
                return false;

            bool created =
                OpenOrAcquireOwnBuffer(ref _impairmentHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentBuffer, ShinobuSensoryImpairmentConstants.DefaultEntityCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireOwnBuffer(ref _tuningHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentTuningBuffer, 1, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireOwnBuffer(ref _telemetryHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentTelemetryBuffer, ShinobuSensoryImpairmentConstants.TelemetryFrameCount, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireOwnBuffer(ref _profilesHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentProfilesBuffer, ShinobuSensoryImpairmentConstants.ProfileCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireOwnBuffer(ref _csvScratchHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentCsvScratchBuffer, ShinobuSensoryImpairmentConstants.CsvMaxBytes, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireOwnBuffer(ref _driftDebugHandle, ShinobuSensoryImpairmentConstants.SensoryInputDriftDebugBuffer, 1, NativeArrayOptions.UninitializedMemory, out _);
            if (!created || !HandlesReady())
                return false;

            InitializeDefaults(vault);
            return true;
        }

        private bool HandlesReady()
        {
            return TryResolveOwnBuffer(ref _impairmentHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentBuffer, ShinobuSensoryImpairmentConstants.DefaultEntityCapacity, out _) &&
                   TryResolveOwnBuffer(ref _tuningHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentTuningBuffer, 1, out _) &&
                   TryResolveOwnBuffer(ref _telemetryHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentTelemetryBuffer, ShinobuSensoryImpairmentConstants.TelemetryFrameCount, out _) &&
                   TryResolveOwnBuffer(ref _profilesHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentProfilesBuffer, ShinobuSensoryImpairmentConstants.ProfileCapacity, out _) &&
                   TryResolveOwnBuffer(ref _csvScratchHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentCsvScratchBuffer, ShinobuSensoryImpairmentConstants.CsvMaxBytes, out _) &&
                   TryResolveOwnBuffer(ref _driftDebugHandle, ShinobuSensoryImpairmentConstants.SensoryInputDriftDebugBuffer, 1, out _);
        }

        private void InitializeDefaults(IDataVault vault)
        {
            if (_defaultsInitialized)
                return;

            TryResolveOwnBuffer(ref _impairmentHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentBuffer, ShinobuSensoryImpairmentConstants.DefaultEntityCapacity, out NativeArray<SensoryImpairmentDTO> impairment);
            TryResolveOwnBuffer(ref _telemetryHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentTelemetryBuffer, ShinobuSensoryImpairmentConstants.TelemetryFrameCount, out NativeArray<SensoryImpairmentTelemetryEntry> telemetry);
            TryResolveOwnBuffer(ref _driftDebugHandle, ShinobuSensoryImpairmentConstants.SensoryInputDriftDebugBuffer, 1, out NativeArray<SensoryInputDriftDebugDTO> driftDebug);
            int initCount = math.max(impairment.IsCreated ? impairment.Length : 0, telemetry.IsCreated ? telemetry.Length : 0);
            initCount = math.max(initCount, driftDebug.IsCreated ? driftDebug.Length : 0);
            bool impairmentLocked = false;
            bool telemetryLocked = false;
            bool driftDebugLocked = false;
            bool tuningLocked = false;
            try
            {
                if (!vault.TryLockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentBuffer, OwnerSystem))
                    return;
                impairmentLocked = true;
                if (!vault.TryLockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentTelemetryBuffer, OwnerSystem))
                    return;
                telemetryLocked = true;
                if (!vault.TryLockBuffer(ShinobuSensoryImpairmentConstants.SensoryInputDriftDebugBuffer, OwnerSystem))
                    return;
                driftDebugLocked = true;
                if (!vault.TryLockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentTuningBuffer, OwnerSystem))
                    return;
                tuningLocked = true;

                if (initCount > 0)
                {
                    new InitSensoryImpairmentJob
                    {
                        Impairments = impairment,
                        Telemetry = telemetry,
                        DriftDebug = driftDebug,
                        Count = impairment.IsCreated ? impairment.Length : 0
                    }.Run(initCount);
                }

                if (TryResolveOwnBuffer(ref _tuningHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentTuningBuffer, 1, out NativeArray<SensoryImpairmentTuningDTO> tuning) &&
                    tuning.Length > 0)
                {
                    SensoryImpairmentTuningDTO row = ShinobuSensoryImpairmentJobMath.BuildDefaultTuning();
                    row.MockMaxDepthMeters = math.max(1f, mockMaxDepthMeters);
                    tuning[0] = row;
                }
            }
            finally
            {
                if (tuningLocked)
                    vault.TryUnlockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentTuningBuffer, OwnerSystem);
                if (driftDebugLocked)
                    vault.TryUnlockBuffer(ShinobuSensoryImpairmentConstants.SensoryInputDriftDebugBuffer, OwnerSystem);
                if (telemetryLocked)
                    vault.TryUnlockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentTelemetryBuffer, OwnerSystem);
                if (impairmentLocked)
                    vault.TryUnlockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentBuffer, OwnerSystem);
            }

            _defaultsInitialized = true;
            TryLoadCsvProfilesCold(vault);
        }

        private bool RunEvaluation(IDataVault vault, float quality)
        {
            if (!TryResolveOwnBuffer(ref _impairmentHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentBuffer, ShinobuSensoryImpairmentConstants.DefaultEntityCapacity, out NativeArray<SensoryImpairmentDTO> impairment) ||
                !TryResolveOwnBuffer(ref _tuningHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentTuningBuffer, 1, out NativeArray<SensoryImpairmentTuningDTO> tuning))
            {
                return false;
            }

            bool hasGas = TryResolveExistingBuffer(ref _gasStateHandle, ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer, 1, out NativeArray<GasPhysiologyStateDTO> gasStates);
            if (!hasGas && enableEmergencyMockToxicity)
                hasGas = OpenOrAcquireSharedBuffer(ref _gasStateHandle, ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer, 1, NativeArrayOptions.UninitializedMemory, out gasStates);
            if (!hasGas)
                return false;

            bool gasLocked = false;
            bool impairmentLocked = false;
            bool tuningLocked = false;
            bool environmentLocked = false;
            try
            {
                if (!vault.TryLockBuffer(ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer, OwnerSystem))
                    return false;
                gasLocked = true;
                if (!vault.TryLockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentBuffer, OwnerSystem))
                    return false;
                impairmentLocked = true;
                if (!vault.TryLockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentTuningBuffer, OwnerSystem))
                    return false;
                tuningLocked = true;

                SensoryImpairmentTuningDTO localTuning = ShinobuSensoryImpairmentJobMath.SanitizeTuning(tuning[0]);
                localTuning.GlobalQualityWeight = quality;
                localTuning.MockMaxDepthMeters = math.max(1f, mockMaxDepthMeters);
                tuning[0] = localTuning;

                if (enableEmergencyMockToxicity &&
                    (TryResolveExistingBuffer(ref _environmentHandle, BufferID.ShinobuEnvironmentVitals, 1, out NativeArray<MockEnvironmentVitalsSignal> environment) ||
                     OpenOrAcquireSharedBuffer(ref _environmentHandle, BufferID.ShinobuEnvironmentVitals, 1, NativeArrayOptions.UninitializedMemory, out environment)) &&
                    vault.TryLockBuffer(BufferID.ShinobuEnvironmentVitals, OwnerSystem))
                {
                    environmentLocked = true;
                    new GenerateMockToxicityDataJob
                    {
                        GasStates = gasStates,
                        Environment = environment,
                        Tuning = localTuning,
                        TimeSeconds = ResolveMockTimeSeconds(_frameCounter),
                        Frame = _frameCounter,
                        Count = 1
                    }.Run(1);
                }

                new EvaluateSensoryImpairmentJob
                {
                    GasStates = gasStates,
                    TuningArray = tuning,
                    Impairments = impairment,
                    GlobalQualityWeight = quality,
                    Count = 1
                }.Run(1);
                return true;
            }
            finally
            {
                if (environmentLocked)
                    vault.TryUnlockBuffer(BufferID.ShinobuEnvironmentVitals, OwnerSystem);
                if (tuningLocked)
                    vault.TryUnlockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentTuningBuffer, OwnerSystem);
                if (impairmentLocked)
                    vault.TryUnlockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentBuffer, OwnerSystem);
                if (gasLocked)
                    vault.TryUnlockBuffer(ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer, OwnerSystem);
            }
        }

        private bool RunInputCorruption(IDataVault vault, float deltaTime, uint frame, float quality, out float corruptionMicroseconds)
        {
            corruptionMicroseconds = 0f;
            if (!TryResolveOwnBuffer(ref _impairmentHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentBuffer, ShinobuSensoryImpairmentConstants.DefaultEntityCapacity, out NativeArray<SensoryImpairmentDTO> impairment) ||
                !TryResolveOwnBuffer(ref _tuningHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentTuningBuffer, 1, out NativeArray<SensoryImpairmentTuningDTO> tuning) ||
                !TryResolveOwnBuffer(ref _telemetryHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentTelemetryBuffer, ShinobuSensoryImpairmentConstants.TelemetryFrameCount, out NativeArray<SensoryImpairmentTelemetryEntry> telemetry) ||
                !TryResolveOwnBuffer(ref _driftDebugHandle, ShinobuSensoryImpairmentConstants.SensoryInputDriftDebugBuffer, 1, out NativeArray<SensoryInputDriftDebugDTO> driftDebug) ||
                !TryResolveExistingBuffer(ref _currentInputHandle, BufferID.ShinobuInputCurrentDto, 1, out NativeArray<InputStateDTO> currentInput) ||
                !TryResolveExistingBuffer(ref _predictedInputHandle, BufferID.ShinobuPredictedInputRing, 1, out NativeArray<PredictedInputDTO> predictedInputs))
            {
                return false;
            }

            TryResolveExistingBuffer(ref _predictedAupTargetHandle, BufferID.ShinobuPredictedInputAupTargets, 1, out NativeArray<PredictedInputAupTargetDTO> aupTargets);
            if (!vault.TryAcquireMutationGuard(InputMutationGuardMask))
                return false;

            bool currentLocked = false;
            bool predictedLocked = false;
            bool telemetryLocked = false;
            bool driftDebugLocked = false;
            try
            {
                if (!vault.TryLockBuffer(BufferID.ShinobuInputCurrentDto, OwnerSystem))
                    return false;
                currentLocked = true;
                if (!vault.TryLockBuffer(BufferID.ShinobuPredictedInputRing, OwnerSystem))
                    return false;
                predictedLocked = true;
                if (!vault.TryLockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentTelemetryBuffer, OwnerSystem))
                    return false;
                telemetryLocked = true;
                if (!vault.TryLockBuffer(ShinobuSensoryImpairmentConstants.SensoryInputDriftDebugBuffer, OwnerSystem))
                    return false;
                driftDebugLocked = true;

                double3 aupOrigin = ResolveAupOrigin();
                SensoryImpairmentTuningDTO localTuning = ShinobuSensoryImpairmentJobMath.SanitizeTuning(tuning[0]);
                long jobStart = Stopwatch.GetTimestamp();
                new CorruptPlayerInputJob
                {
                    CurrentInput = currentInput,
                    PredictedInputs = predictedInputs,
                    AupTargets = aupTargets,
                    Impairments = impairment,
                    Telemetry = telemetry,
                    DriftDebug = driftDebug,
                    Tuning = localTuning,
                    AupOrigin = aupOrigin,
                    TickNumber = frame,
                    Frame = frame,
                    TelemetryCursor = _telemetryCursor,
                    DeltaSeconds = deltaTime,
                    GlobalQualityWeight = quality
                }.Run(1);
                corruptionMicroseconds = ResolveElapsedMicroseconds(jobStart, Stopwatch.GetTimestamp());

                _telemetryCursor++;
                if (_telemetryCursor >= ShinobuSensoryImpairmentConstants.TelemetryFrameCount)
                    _telemetryCursor %= ShinobuSensoryImpairmentConstants.TelemetryFrameCount;
                return true;
            }
            finally
            {
                if (driftDebugLocked)
                    vault.TryUnlockBuffer(ShinobuSensoryImpairmentConstants.SensoryInputDriftDebugBuffer, OwnerSystem);
                if (telemetryLocked)
                    vault.TryUnlockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentTelemetryBuffer, OwnerSystem);
                if (predictedLocked)
                    vault.TryUnlockBuffer(BufferID.ShinobuPredictedInputRing, OwnerSystem);
                if (currentLocked)
                    vault.TryUnlockBuffer(BufferID.ShinobuInputCurrentDto, OwnerSystem);
                vault.ReleaseMutationGuard(InputMutationGuardMask);
            }
        }

        private void PatchLatestTelemetryGas(IDataVault vault, float elapsedMicroseconds)
        {
            if (!TryResolveOwnBuffer(ref _telemetryHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentTelemetryBuffer, ShinobuSensoryImpairmentConstants.TelemetryFrameCount, out NativeArray<SensoryImpairmentTelemetryEntry> telemetry))
            {
                return;
            }

            TryResolveExistingBuffer(ref _gasStateHandle, ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer, 1, out NativeArray<GasPhysiologyStateDTO> gasStates);
            TryResolveExistingBuffer(ref _environmentHandle, BufferID.ShinobuEnvironmentVitals, 1, out NativeArray<MockEnvironmentVitalsSignal> environment);
            bool telemetryLocked = false;
            try
            {
                if (!vault.TryLockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentTelemetryBuffer, OwnerSystem))
                    return;
                telemetryLocked = true;
                new PatchSensoryTelemetryGasJob
                {
                    GasStates = gasStates,
                    Environment = environment,
                    Telemetry = telemetry,
                    TelemetryCursor = (_telemetryCursor + telemetry.Length - 1) % telemetry.Length,
                    ExecutionMicroseconds = elapsedMicroseconds
                }.Run();
            }
            finally
            {
                if (telemetryLocked)
                    vault.TryUnlockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentTelemetryBuffer, OwnerSystem);
            }
        }

        private void PublishVisualScalars(IDataVault vault)
        {
            if (!TryResolveOwnBuffer(ref _impairmentHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentBuffer, ShinobuSensoryImpairmentConstants.DefaultEntityCapacity, out NativeArray<SensoryImpairmentDTO> impairment) ||
                impairment.Length <= 0)
            {
                return;
            }

            GasPhysiologyStateDTO gas = default;
            TryResolveExistingBuffer(ref _gasStateHandle, ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer, 1, out NativeArray<GasPhysiologyStateDTO> gasStates);
            if (gasStates.IsCreated && gasStates.Length > 0)
                gas = gasStates[0];

            SensoryImpairmentDTO row = impairment[0];
            float co2 = ShinobuPhysiologyJobMath.ResolveCarbonDioxideToxicity01(gas.CarbonDioxidePartialPressure);
            float quality = ResolveGlobalQualityWeight();
            HectonShaderGlobalDataVaultBridge.PublishPhysiologyGasToxicity(new Vector4(
                math.saturate(row.HypoxiaVignette01),
                math.saturate(gas.CnsToxicity01),
                math.saturate(co2),
                quality));
        }

        private void TryDumpAutopsyIfFaulted(IDataVault vault)
        {
            if (_autopsyDumped ||
                !TryResolveOwnBuffer(ref _telemetryHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentTelemetryBuffer, ShinobuSensoryImpairmentConstants.TelemetryFrameCount, out NativeArray<SensoryImpairmentTelemetryEntry> telemetry) ||
                telemetry.Length <= 0)
            {
                return;
            }

            int latestIndex = (_telemetryCursor + telemetry.Length - 1) % telemetry.Length;
            SensoryImpairmentTelemetryEntry entry = telemetry[latestIndex];
            bool faulted = (entry.Flags & (SensoryImpairmentFlags.NonFiniteSanitized | SensoryImpairmentFlags.OverBudget)) != 0u ||
                           !math.isfinite(entry.HypoxiaVignette01) ||
                           !math.isfinite(entry.NarcosisDrift01) ||
                           !math.isfinite(entry.InputLatencyMilliseconds);
            if (!faulted)
                return;

            _autopsyDumped = true;
            DumpAutopsyReport(telemetry);
        }

        private void DumpAutopsyReport(NativeArray<SensoryImpairmentTelemetryEntry> telemetry)
        {
            try
            {
                string directory = Path.GetDirectoryName(_dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[32];
                    WriteUInt64LittleEndian(header.Slice(0, 8), ShinobuSensoryImpairmentConstants.DumpMagic);
                    WriteUInt32LittleEndian(header.Slice(8, 4), ShinobuSensoryImpairmentConstants.DumpVersion);
                    WriteUInt32LittleEndian(header.Slice(12, 4), (uint)telemetry.Length);
                    WriteUInt32LittleEndian(header.Slice(16, 4), (uint)UnsafeUtility.SizeOf<SensoryImpairmentTelemetryEntry>());
                    WriteUInt32LittleEndian(header.Slice(20, 4), unchecked((uint)_telemetryCursor));
                    WriteUInt32LittleEndian(header.Slice(24, 4), ShinobuSensoryImpairmentConstants.SourceHash);
                    WriteUInt32LittleEndian(header.Slice(28, 4), _frameCounter);
                    stream.Write(header);

                    void* telemetryPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    int byteCount = telemetry.Length * UnsafeUtility.SizeOf<SensoryImpairmentTelemetryEntry>();
                    stream.Write(new ReadOnlySpan<byte>(telemetryPtr, byteCount));
                    stream.Flush();
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private bool TryLoadCsvProfilesCold(IDataVault vault)
        {
#if !UNITY_EDITOR
            return false;
#else
            if (string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath))
                return false;

            long writeTicks = File.GetLastWriteTimeUtc(_csvPath).Ticks;
            if (writeTicks == _csvLastWriteTicks)
                return false;

            if (!TryResolveOwnBuffer(ref _profilesHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentProfilesBuffer, ShinobuSensoryImpairmentConstants.ProfileCapacity, out NativeArray<SensoryImpairmentProfileDTO> profiles) ||
                !TryResolveOwnBuffer(ref _tuningHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentTuningBuffer, 1, out NativeArray<SensoryImpairmentTuningDTO> tuning) ||
                !TryResolveOwnBuffer(ref _csvScratchHandle, ShinobuSensoryImpairmentConstants.SensoryImpairmentCsvScratchBuffer, ShinobuSensoryImpairmentConstants.CsvMaxBytes, out NativeArray<byte> scratch))
            {
                return false;
            }

            bool profilesLocked = false;
            bool tuningLocked = false;
            bool scratchLocked = false;
            try
            {
                if (!vault.TryLockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentProfilesBuffer, OwnerSystem))
                    return false;
                profilesLocked = true;
                if (!vault.TryLockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentTuningBuffer, OwnerSystem))
                    return false;
                tuningLocked = true;
                if (!vault.TryLockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentCsvScratchBuffer, OwnerSystem))
                    return false;
                scratchLocked = true;

                byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                int count = 0;
                using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int targetBytes = (int)math.min(stream.Length, scratch.Length);
                    Span<byte> target = new Span<byte>(scratchPtr, targetBytes);
                    while (count < targetBytes)
                    {
                        int read = stream.Read(target.Slice(count));
                        if (read <= 0)
                            break;
                        count += read;
                    }
                }

                if (ParseProfilesCsv(new ReadOnlySpan<byte>(scratchPtr, count), profiles, tuning))
                {
                    _csvLastWriteTicks = writeTicks;
                    return true;
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
                if (scratchLocked)
                    vault.TryUnlockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentCsvScratchBuffer, OwnerSystem);
                if (tuningLocked)
                    vault.TryUnlockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentTuningBuffer, OwnerSystem);
                if (profilesLocked)
                    vault.TryUnlockBuffer(ShinobuSensoryImpairmentConstants.SensoryImpairmentProfilesBuffer, OwnerSystem);
            }

            return false;
#endif
        }

#if UNITY_EDITOR
        private static bool ParseProfilesCsv(ReadOnlySpan<byte> bytes, NativeArray<SensoryImpairmentProfileDTO> profiles, NativeArray<SensoryImpairmentTuningDTO> tuningRows)
        {
            if (!profiles.IsCreated || profiles.Length <= 0)
                return false;

            int lineStart = 0;
            int profileCount = 0;
            while (lineStart < bytes.Length && profileCount < profiles.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < bytes.Length && bytes[lineEnd] != (byte)'\n' && bytes[lineEnd] != (byte)'\r')
                    lineEnd++;

                ReadOnlySpan<byte> line = TrimAscii(bytes.Slice(lineStart, lineEnd - lineStart));
                if (TryParseProfileLine(line, out SensoryImpairmentProfileDTO profile))
                {
                    profiles[profileCount++] = profile;
                    if (profileCount == 1 && tuningRows.IsCreated && tuningRows.Length > 0)
                    {
                        SensoryImpairmentTuningDTO tuning = ShinobuSensoryImpairmentJobMath.SanitizeTuning(tuningRows[0]);
                        tuning.HypoxiaPartialPressureAtm = profile.HypoxiaPartialPressureAtm;
                        tuning.AnoxiaPartialPressureAtm = profile.AnoxiaPartialPressureAtm;
                        tuning.NarcosisStartAtm = profile.NarcosisStartAtm;
                        tuning.NarcosisFullAtm = profile.NarcosisFullAtm;
                        tuning.MaxInputLatencyMilliseconds = profile.MaxInputLatencyMilliseconds;
                        tuning.Flags |= SensoryImpairmentFlags.CsvProfile;
                        tuningRows[0] = ShinobuSensoryImpairmentJobMath.SanitizeTuning(tuning);
                    }
                }

                lineStart = lineEnd + 1;
                while (lineStart < bytes.Length && (bytes[lineStart] == (byte)'\n' || bytes[lineStart] == (byte)'\r'))
                    lineStart++;
            }

            for (int i = profileCount; i < profiles.Length; i++)
                profiles[i] = default;
            return profileCount > 0;
        }
#endif

        private static bool TryParseProfileLine(ReadOnlySpan<byte> line, out SensoryImpairmentProfileDTO profile)
        {
            profile = default;
            if (line.Length == 0 || line[0] == (byte)'#')
                return false;

            int cursor = 0;
            ReadOnlySpan<byte> nameCell = ReadCell(line, ref cursor);
            if (nameCell.Length == 0 || IsHeaderCell(nameCell))
                return false;

            if (!TryParseAsciiFloat(ReadCell(line, ref cursor), out float hypoxia) ||
                !TryParseAsciiFloat(ReadCell(line, ref cursor), out float anoxia) ||
                !TryParseAsciiFloat(ReadCell(line, ref cursor), out float narcStart) ||
                !TryParseAsciiFloat(ReadCell(line, ref cursor), out float narcFull) ||
                !TryParseAsciiFloat(ReadCell(line, ref cursor), out float latency))
            {
                return false;
            }

            profile.ProfileHash = HashLowerAscii(nameCell);
            profile.HypoxiaPartialPressureAtm = hypoxia;
            profile.AnoxiaPartialPressureAtm = anoxia;
            profile.NarcosisStartAtm = narcStart;
            profile.NarcosisFullAtm = narcFull;
            profile.MaxInputLatencyMilliseconds = latency;
            profile.Flags = SensoryImpairmentFlags.CsvProfile;
            return true;
        }

        private static ReadOnlySpan<byte> ReadCell(ReadOnlySpan<byte> line, ref int cursor)
        {
            if (cursor >= line.Length)
                return ReadOnlySpan<byte>.Empty;

            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;
            int end = cursor;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;
            return TrimAscii(line.Slice(start, end - start));
        }

        private static bool IsHeaderCell(ReadOnlySpan<byte> cell)
        {
            if (cell.Length <= 0)
                return true;
            byte first = ToLowerAscii(cell[0]);
            return first == (byte)'p' && cell.Length >= 7;
        }

        private static bool TryParseAsciiFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            bytes = TrimAscii(bytes);
            if (bytes.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (bytes[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (bytes[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool any = false;
            while (index < bytes.Length && bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9')
            {
                integer = integer * 10f + (bytes[index] - (byte)'0');
                index++;
                any = true;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < bytes.Length && bytes[index] == (byte)'.')
            {
                index++;
                while (index < bytes.Length && bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9')
                {
                    fraction = fraction * 10f + (bytes[index] - (byte)'0');
                    divisor *= 10f;
                    index++;
                    any = true;
                }
            }

            value = sign * (integer + fraction / divisor);
            return any && math.isfinite(value);
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte value = ToLowerAscii(bytes[i]);
                if (value <= (byte)' ')
                    continue;
                hash ^= value;
                hash *= 16777619u;
            }
            return hash != 0u ? hash : ShinobuSensoryImpairmentConstants.SourceHash;
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private bool OpenOrAcquireOwnBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            IDataVault vault = _dataVault;
            if (TryResolveOwnBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle<T>(bufferId, out handle))
                {
                    buffer = default;
                    return false;
                }

                return TryResolveOwnBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
            }

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystem, options);
            return TryResolveOwnBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private bool OpenOrAcquireSharedBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            IDataVault vault = _dataVault;
            if (TryResolveExistingBuffer(ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null || vault.IsAllocationLocked || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystem, options);
            return vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryResolveOwnBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            return TryResolveOwnBuffer(_dataVault, ref handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryResolveOwnBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                handle.BufferID != (uint)bufferId ||
                handle.SystemID != (uint)OwnerSystem ||
                handle.Generation == 0u ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private bool TryResolveExistingBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            IDataVault vault = _dataVault;
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if ((handle.BufferID != (uint)bufferId || handle.Generation == 0u) &&
                !vault.TryGetGenerationHandle<T>(bufferId, out handle))
            {
                handle = default;
                return false;
            }

            if ((!vault.TryResolveHandle(in handle, out buffer) ||
                 !buffer.IsCreated ||
                 buffer.Length < requiredLength) &&
                (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                 !vault.TryResolveHandle(in handle, out buffer) ||
                 !buffer.IsCreated ||
                 buffer.Length < requiredLength))
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private double3 ResolveAupOrigin()
        {
            IPlayerRuntimeContext player = _playerContext;
            if (player != null && player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                double3 origin = snapshot.Aup.ToAbsoluteDouble3();
                if (math.all(math.isfinite(origin)))
                    return origin;
            }

            return double3.zero;
        }

        private static bool TryReadFirst<T>(NativeArray<T> rows, out T value) where T : struct
        {
            value = default;
            if (!rows.IsCreated || rows.Length <= 0)
                return false;
            value = rows[0];
            return true;
        }

        private bool TryReadCachedBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int minimumLength,
            out NativeArray<T> rows) where T : struct
        {
            rows = default;
            IDataVault vault = _dataVault;
            if (vault == null || handle.BufferID != (uint)bufferId)
                return false;

            return vault.TryReadHandle(in handle, out rows) &&
                   rows.IsCreated &&
                   rows.Length >= minimumLength;
        }

        private float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : 1f);
        }

        private static float ResolveMockTimeSeconds(uint frame)
        {
            return frame * (1f / ShinobuSensoryImpairmentConstants.DefaultLatencyFrameRate);
        }

        private uint ResolveFrameId(uint dispatcherFrame)
        {
            if (dispatcherFrame != 0u)
            {
                _frameCounter = dispatcherFrame;
                return dispatcherFrame;
            }

            return ++_frameCounter;
        }

        private static float ResolveElapsedMicroseconds(long startTimestamp, long endTimestamp)
        {
            long rawDelta = endTimestamp - startTimestamp;
            long delta = rawDelta > 0L ? rawDelta : 0L;
            double microseconds = delta * 1000000.0 / Stopwatch.Frequency;
            return math.isfinite(microseconds) ? (float)math.min(microseconds, float.MaxValue) : 0f;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> bytes)
        {
            int start = 0;
            int end = bytes.Length;
            while (start < end && IsAsciiSpace(bytes[start]))
                start++;
            while (end > start && IsAsciiSpace(bytes[end - 1]))
                end--;
            return bytes.Slice(start, end - start);
        }

        private static bool IsAsciiSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\n' || value == (byte)'\r';
        }

        private void TryRegisterRuntimeRoutes()
        {
            EnsureDispatcherPhaseBridges();

            if (!_registeredPreSimulation && _preSimulationPhase != null)
                _registeredPreSimulation = GlobalRegistry.TryRegisterDispatcherSystem(_preSimulationPhase);
            if (!_registeredVisualSync && _visualSyncPhase != null)
                _registeredVisualSync = GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncPhase);
            if (!_registeredSlow)
                _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);

            if (_registeredVisualSync && _registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrame = false;
            }
            else if (!_registeredVisualSync && !_registeredLateFrame)
            {
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
            }
        }

        private void TryUnregisterRuntimeRoutes()
        {
            if (_registeredPreSimulation && _preSimulationPhase != null)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_preSimulationPhase);
                _registeredPreSimulation = false;
            }

            if (_registeredVisualSync && _visualSyncPhase != null)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_visualSyncPhase);
                _registeredVisualSync = false;
            }

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

        private void EnsureDispatcherPhaseBridges()
        {
            if (_preSimulationPhase == null)
            {
                // COLD ALLOC: PreSimulationPhaseSystem[1] - dispatcher PRE_SIMULATION input mutation bridge - owner: ShinobuSensoryImpairmentRuntime
                _preSimulationPhase = new PreSimulationPhaseSystem(this);
            }

            if (_visualSyncPhase == null)
            {
                // COLD ALLOC: VisualSyncPhaseSystem[1] - dispatcher VISUAL_SYNC shader scalar bridge - owner: ShinobuSensoryImpairmentRuntime
                _visualSyncPhase = new VisualSyncPhaseSystem(this);
            }
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

        private void ClearCachedHandles()
        {
            _impairmentHandle = default;
            _tuningHandle = default;
            _telemetryHandle = default;
            _profilesHandle = default;
            _csvScratchHandle = default;
            _driftDebugHandle = default;
            _gasStateHandle = default;
            _environmentHandle = default;
            _currentInputHandle = default;
            _predictedInputHandle = default;
            _predictedAupTargetHandle = default;
            _defaultsInitialized = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUInt32LittleEndian(Span<byte> destination, uint value)
        {
            destination[0] = (byte)value;
            destination[1] = (byte)(value >> 8);
            destination[2] = (byte)(value >> 16);
            destination[3] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUInt64LittleEndian(Span<byte> destination, ulong value)
        {
            WriteUInt32LittleEndian(destination.Slice(0, 4), (uint)value);
            WriteUInt32LittleEndian(destination.Slice(4, 4), (uint)(value >> 32));
        }

        private sealed class PreSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly ShinobuSensoryImpairmentRuntime _owner;

            public PreSimulationPhaseSystem(ShinobuSensoryImpairmentRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() { return 0x53333250u; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.PreSimulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { _owner.PreSimulationTick(in timing); }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class VisualSyncPhaseSystem : IDispatcherSystem
        {
            private readonly ShinobuSensoryImpairmentRuntime _owner;

            public VisualSyncPhaseSystem(ShinobuSensoryImpairmentRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() { return 0x53333256u; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.VisualSync; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { _owner.VisualSyncTick(in timing); }
        }
    }
}
