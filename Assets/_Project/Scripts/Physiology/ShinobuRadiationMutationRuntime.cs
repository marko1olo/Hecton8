using System;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Physiology;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Physiology
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physiology/Radiation Mutation Link")]
    public sealed unsafe class ShinobuRadiationMutationRuntime : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const SystemID OwnerSystem = SystemID.GameplayPlayer;
        private const string CsvRelativePath = "biological_mutation_profiles.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_324.bin";

        [Header("Emergency Mock")]
        [SerializeField] private bool enableEmergencyMockRadiation;
        [SerializeField, Min(1f)] private float mockPeakDoseRad = ShinobuRadiationMutationConstants.DefaultMockPeakDoseRad;

        [Header("Presentation")]
        [SerializeField] private bool emitToxicBloodVfx = true;

        private VaultGenerationHandle<MutationStateDTO> _mutationStateHandle;
        private VaultGenerationHandle<RadiationMutationTuningDTO> _tuningHandle;
        private VaultGenerationHandle<RadiationMutationTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<RadiationMutationProfileDTO> _profilesHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<float> _mockDoseHandle;
        private VaultGenerationHandle<RadiationStateDTO> _radiationStateHandle;
        private VaultGenerationHandle<MetabolicStateDTO> _metabolicStateHandle;

        private IDataVault _dataVault;
        private IPlayerRuntimeContext _playerContext;
        private PreSimulationPhaseSystem _preSimulationPhase;
        private VisualSyncPhaseSystem _visualSyncPhase;
        private string _csvPath;
        private string _dumpPath;
        private long _csvLastWriteTicks;
        private uint _frameCounter;
        private int _telemetryCursor;
        private float _previousDoseRad;
        private uint _lastVfxFrame;
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
            if (vault == null || !HasRuntimeVaultState())
                return;

            uint frame = ++_frameCounter;
            long start = Stopwatch.GetTimestamp();
            if (RunEvaluation(vault, frame, ResolveGlobalQualityWeight()))
            {
                float microseconds = ResolveElapsedMicroseconds(start, Stopwatch.GetTimestamp());
                PatchLatestTelemetry(vault, microseconds);
            }

#if UNITY_EDITOR
            TryLoadCsvProfilesCold(vault);
#endif
        }

        public void LateFrameTick()
        {
            if (!_registeredVisualSync)
                RunVisualSyncFrame();
        }

        private void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !_defaultsInitialized)
                return;

            RunMetabolicBridge(vault);
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            RunVisualSyncFrame();
        }

        private void RunVisualSyncFrame()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !_defaultsInitialized)
                return;

            PublishVisualScalars(vault);
            EmitToxicBloodVfxIfNeeded(vault);
            TryDumpAutopsyIfFaulted(vault);
        }

        public bool TryGetMutationState(out MutationStateDTO state)
        {
            state = default;
            return TryReadCachedBuffer(in _mutationStateHandle, ShinobuRadiationMutationConstants.MutationStateBuffer, ShinobuRadiationMutationConstants.DefaultEntityCapacity, out NativeArray<MutationStateDTO> rows) &&
                   rows.Length > 0 &&
                   TryReadFirst(rows, out state);
        }

        public bool TryGetTuning(out RadiationMutationTuningDTO tuning)
        {
            tuning = default;
            if (!TryReadCachedBuffer(in _tuningHandle, ShinobuRadiationMutationConstants.MutationTuningBuffer, 1, out NativeArray<RadiationMutationTuningDTO> rows) ||
                rows.Length <= 0)
            {
                return false;
            }

            tuning = ShinobuRadiationMutationJobMath.SanitizeTuning(rows[0]);
            return true;
        }

        public bool SetEditorTuning(RadiationMutationTuningDTO tuning)
        {
            if (!TryResolveOwnBuffer(ref _tuningHandle, ShinobuRadiationMutationConstants.MutationTuningBuffer, 1, out NativeArray<RadiationMutationTuningDTO> rows) ||
                rows.Length <= 0)
            {
                return false;
            }

            void* tuningPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rows);
            ref RadiationMutationTuningDTO target = ref UnsafeUtility.AsRef<RadiationMutationTuningDTO>(tuningPtr);
            target = ShinobuRadiationMutationJobMath.SanitizeTuning(tuning);
            return true;
        }

        public bool TryGetLatestTelemetry(out RadiationMutationTelemetryEntry entry)
        {
            entry = default;
            if (!TryReadCachedBuffer(in _telemetryHandle, ShinobuRadiationMutationConstants.MutationTelemetryBuffer, ShinobuRadiationMutationConstants.TelemetryFrameCount, out NativeArray<RadiationMutationTelemetryEntry> rows) ||
                rows.Length <= 0)
            {
                return false;
            }

            int index = (_telemetryCursor + rows.Length - 1) % rows.Length;
            entry = rows[index];
            return true;
        }

#if UNITY_EDITOR
        public int CopyTelemetrySeriesForEditor(float[] doseSeries, float[] severitySeries)
        {
            if (doseSeries == null ||
                severitySeries == null ||
                doseSeries.Length <= 0 ||
                severitySeries.Length <= 0 ||
                !TryReadCachedBuffer(in _telemetryHandle, ShinobuRadiationMutationConstants.MutationTelemetryBuffer, ShinobuRadiationMutationConstants.TelemetryFrameCount, out NativeArray<RadiationMutationTelemetryEntry> rows) ||
                rows.Length <= 0)
            {
                return 0;
            }

            int count = math.min(math.min(doseSeries.Length, severitySeries.Length), rows.Length);
            int start = (_telemetryCursor + rows.Length - count) % rows.Length;
            for (int i = 0; i < count; i++)
            {
                int index = start + i;
                if (index >= rows.Length)
                    index -= rows.Length;
                RadiationMutationTelemetryEntry entry = rows[index];
                doseSeries[i] = math.saturate(entry.AttenuatedDoseRad * math.rcp(math.max(1f, ShinobuRadiationMutationConstants.DefaultFatalDoseRad)));
                severitySeries[i] = math.saturate(entry.MutationSeverity01);
            }

            return count;
        }
#endif

        public bool InjectMockDose(float doseRad)
        {
            if (!TryResolveOwnBuffer(ref _mockDoseHandle, ShinobuRadiationMutationConstants.MutationMockDoseBuffer, ShinobuRadiationMutationConstants.DefaultEntityCapacity, out NativeArray<float> mockDose) ||
                mockDose.Length <= 0)
            {
                return false;
            }

            void* mockPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mockDose);
            ref float target = ref UnsafeUtility.AsRef<float>(mockPtr);
            target = math.max(0f, ShinobuRadiationMutationJobMath.SanitizeFinite(doseRad, 0f));
            return true;
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
            if (!ShinobuRadiationMutationLayoutGuards.ValidateMutationLayouts())
                return false;

            bool created =
                OpenOrAcquireOwnBuffer(ref _mutationStateHandle, ShinobuRadiationMutationConstants.MutationStateBuffer, ShinobuRadiationMutationConstants.DefaultEntityCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireOwnBuffer(ref _tuningHandle, ShinobuRadiationMutationConstants.MutationTuningBuffer, 1, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireOwnBuffer(ref _telemetryHandle, ShinobuRadiationMutationConstants.MutationTelemetryBuffer, ShinobuRadiationMutationConstants.TelemetryFrameCount, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireOwnBuffer(ref _profilesHandle, ShinobuRadiationMutationConstants.MutationProfileBuffer, ShinobuRadiationMutationConstants.ProfileCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireOwnBuffer(ref _csvScratchHandle, ShinobuRadiationMutationConstants.MutationCsvScratchBuffer, ShinobuRadiationMutationConstants.CsvMaxBytes, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquireOwnBuffer(ref _mockDoseHandle, ShinobuRadiationMutationConstants.MutationMockDoseBuffer, ShinobuRadiationMutationConstants.DefaultEntityCapacity, NativeArrayOptions.UninitializedMemory, out _);
            if (!created || !HandlesReady())
                return false;

            InitializeDefaults(vault);
            return true;
        }

        private bool HasRuntimeVaultState()
        {
            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   _defaultsInitialized &&
                   HandlesReady();
        }

        private bool HandlesReady()
        {
            return TryResolveOwnBuffer(ref _mutationStateHandle, ShinobuRadiationMutationConstants.MutationStateBuffer, ShinobuRadiationMutationConstants.DefaultEntityCapacity, out _) &&
                   TryResolveOwnBuffer(ref _tuningHandle, ShinobuRadiationMutationConstants.MutationTuningBuffer, 1, out _) &&
                   TryResolveOwnBuffer(ref _telemetryHandle, ShinobuRadiationMutationConstants.MutationTelemetryBuffer, ShinobuRadiationMutationConstants.TelemetryFrameCount, out _) &&
                   TryResolveOwnBuffer(ref _profilesHandle, ShinobuRadiationMutationConstants.MutationProfileBuffer, ShinobuRadiationMutationConstants.ProfileCapacity, out _) &&
                   TryResolveOwnBuffer(ref _csvScratchHandle, ShinobuRadiationMutationConstants.MutationCsvScratchBuffer, ShinobuRadiationMutationConstants.CsvMaxBytes, out _) &&
                   TryResolveOwnBuffer(ref _mockDoseHandle, ShinobuRadiationMutationConstants.MutationMockDoseBuffer, ShinobuRadiationMutationConstants.DefaultEntityCapacity, out _);
        }

        private void InitializeDefaults(IDataVault vault)
        {
            if (_defaultsInitialized)
                return;

            TryResolveOwnBuffer(ref _mutationStateHandle, ShinobuRadiationMutationConstants.MutationStateBuffer, ShinobuRadiationMutationConstants.DefaultEntityCapacity, out NativeArray<MutationStateDTO> states);
            TryResolveOwnBuffer(ref _telemetryHandle, ShinobuRadiationMutationConstants.MutationTelemetryBuffer, ShinobuRadiationMutationConstants.TelemetryFrameCount, out NativeArray<RadiationMutationTelemetryEntry> telemetry);
            TryResolveOwnBuffer(ref _mockDoseHandle, ShinobuRadiationMutationConstants.MutationMockDoseBuffer, ShinobuRadiationMutationConstants.DefaultEntityCapacity, out NativeArray<float> mockDose);
            int initCount = math.max(math.max(states.IsCreated ? states.Length : 0, telemetry.IsCreated ? telemetry.Length : 0), mockDose.IsCreated ? mockDose.Length : 0);
            for (int i = 0; i < initCount; i++)
            {
                if (states.IsCreated && i < states.Length)
                    states[i] = default;
                if (telemetry.IsCreated && i < telemetry.Length)
                    telemetry[i] = default;
                if (mockDose.IsCreated && i < mockDose.Length)
                    mockDose[i] = 0f;
            }

            if (TryResolveOwnBuffer(ref _tuningHandle, ShinobuRadiationMutationConstants.MutationTuningBuffer, 1, out NativeArray<RadiationMutationTuningDTO> tuning) &&
                tuning.Length > 0)
            {
                RadiationMutationTuningDTO row = ShinobuRadiationMutationJobMath.BuildDefaultTuning();
                row.MockPeakDoseRad = math.max(row.SafeDoseRad, mockPeakDoseRad);
                tuning[0] = row;
            }

            if (TryResolveOwnBuffer(ref _profilesHandle, ShinobuRadiationMutationConstants.MutationProfileBuffer, ShinobuRadiationMutationConstants.ProfileCapacity, out NativeArray<RadiationMutationProfileDTO> profiles) &&
                profiles.Length > 0)
            {
                profiles[0] = BuildDefaultProfile(0x44454654u);
                for (int i = 1; i < profiles.Length; i++)
                    profiles[i] = default;
            }

            _defaultsInitialized = true;
            TryLoadCsvProfilesCold(vault);
        }

        private bool RunEvaluation(IDataVault vault, uint frame, float quality)
        {
            if (!TryResolveOwnBuffer(ref _mutationStateHandle, ShinobuRadiationMutationConstants.MutationStateBuffer, ShinobuRadiationMutationConstants.DefaultEntityCapacity, out NativeArray<MutationStateDTO> mutationStates) ||
                !TryResolveOwnBuffer(ref _tuningHandle, ShinobuRadiationMutationConstants.MutationTuningBuffer, 1, out NativeArray<RadiationMutationTuningDTO> tuningRows) ||
                !TryResolveOwnBuffer(ref _telemetryHandle, ShinobuRadiationMutationConstants.MutationTelemetryBuffer, ShinobuRadiationMutationConstants.TelemetryFrameCount, out NativeArray<RadiationMutationTelemetryEntry> telemetry) ||
                !TryResolveOwnBuffer(ref _mockDoseHandle, ShinobuRadiationMutationConstants.MutationMockDoseBuffer, ShinobuRadiationMutationConstants.DefaultEntityCapacity, out NativeArray<float> mockDose))
            {
                return false;
            }

            if (mutationStates.Length <= 0 ||
                tuningRows.Length <= 0 ||
                telemetry.Length <= 0 ||
                mockDose.Length <= 0)
            {
                return false;
            }

            bool hasRadiation = TryBindExistingSnapshot(ref _radiationStateHandle, BufferID.Shinobu274RadiationStates, ShinobuRadiationMutationConstants.DefaultEntityCapacity, out NativeArray<RadiationStateDTO> radiationStates);
            if (!hasRadiation && !enableEmergencyMockRadiation)
                return false;

            bool mutationLocked = false;
            bool tuningLocked = false;
            bool telemetryLocked = false;
            bool mockLocked = false;
            try
            {
                if (!vault.TryLockBuffer(ShinobuRadiationMutationConstants.MutationStateBuffer, OwnerSystem))
                    return false;
                mutationLocked = true;
                if (!vault.TryLockBuffer(ShinobuRadiationMutationConstants.MutationTuningBuffer, OwnerSystem))
                    return false;
                tuningLocked = true;
                if (!vault.TryLockBuffer(ShinobuRadiationMutationConstants.MutationTelemetryBuffer, OwnerSystem))
                    return false;
                telemetryLocked = true;
                if (!vault.TryLockBuffer(ShinobuRadiationMutationConstants.MutationMockDoseBuffer, OwnerSystem))
                    return false;
                mockLocked = true;
                RadiationMutationTuningDTO tuning = ShinobuRadiationMutationJobMath.SanitizeTuning(tuningRows[0]);
                tuning.GlobalQualityWeight = quality;
                tuning.MockPeakDoseRad = math.max(tuning.SafeDoseRad, mockPeakDoseRad);
                tuningRows[0] = tuning;

                int activeCount = math.min(ShinobuRadiationMutationConstants.DefaultEntityCapacity, mutationStates.Length);
                if (enableEmergencyMockRadiation)
                {
                    float mockDoseRad = RadiationMutationKernel.GenerateMockDose(ResolveMockTimeSeconds(frame), in tuning);
                    for (int i = 0; i < activeCount && i < mockDose.Length; i++)
                        mockDose[i] = mockDoseRad;
                }

                int telemetryIndex = _telemetryCursor % telemetry.Length;
                if (telemetryIndex < 0)
                    telemetryIndex += telemetry.Length;
                for (int i = 0; i < activeCount; i++)
                {
                    RadiationStateDTO radiation = hasRadiation && i < radiationStates.Length ? radiationStates[i] : default;
                    float mockDoseRad = enableEmergencyMockRadiation && i < mockDose.Length ? mockDose[i] : 0f;
                    MutationStateDTO mutation = mutationStates[i];
                    RadiationMutationKernel.EvaluateRow(
                        i,
                        in radiation,
                        (byte)(hasRadiation ? 1 : 0),
                        mockDoseRad,
                        (byte)(enableEmergencyMockRadiation ? 1 : 0),
                        in tuning,
                        _previousDoseRad,
                        ShinobuMetabolismConstants.NominalSlowTickSeconds,
                        quality,
                        frame,
                        telemetryIndex,
                        ref mutation,
                        out RadiationMutationTelemetryEntry entry);
                    mutationStates[i] = mutation;
                    if (i == 0)
                        telemetry[telemetryIndex] = entry;
                }

                _previousDoseRad = telemetry[telemetryIndex].AttenuatedDoseRad;
                _telemetryCursor++;
                if (_telemetryCursor >= ShinobuRadiationMutationConstants.TelemetryFrameCount)
                    _telemetryCursor %= ShinobuRadiationMutationConstants.TelemetryFrameCount;
                return true;
            }
            finally
            {
                if (mockLocked)
                    vault.TryUnlockBuffer(ShinobuRadiationMutationConstants.MutationMockDoseBuffer, OwnerSystem);
                if (telemetryLocked)
                    vault.TryUnlockBuffer(ShinobuRadiationMutationConstants.MutationTelemetryBuffer, OwnerSystem);
                if (tuningLocked)
                    vault.TryUnlockBuffer(ShinobuRadiationMutationConstants.MutationTuningBuffer, OwnerSystem);
                if (mutationLocked)
                    vault.TryUnlockBuffer(ShinobuRadiationMutationConstants.MutationStateBuffer, OwnerSystem);
            }
        }

        private bool RunMetabolicBridge(IDataVault vault)
        {
            if (!TryResolveOwnBuffer(ref _mutationStateHandle, ShinobuRadiationMutationConstants.MutationStateBuffer, ShinobuRadiationMutationConstants.DefaultEntityCapacity, out NativeArray<MutationStateDTO> mutationStates) ||
                !TryResolveOwnBuffer(ref _tuningHandle, ShinobuRadiationMutationConstants.MutationTuningBuffer, 1, out NativeArray<RadiationMutationTuningDTO> tuningRows) ||
                !TryResolveExistingBuffer(ref _metabolicStateHandle, BufferID.ShinobuMetabolismStates, ShinobuRadiationMutationConstants.DefaultEntityCapacity, out NativeArray<MetabolicStateDTO> metabolicStates) ||
                mutationStates.Length <= 0 ||
                metabolicStates.Length <= 0 ||
                tuningRows.Length <= 0)
            {
                return false;
            }

            bool mutationLocked = false;
            bool metabolicLocked = false;
            bool metabolicGuard = false;
            try
            {
                if (!vault.TryAcquireMutationGuard(ShinobuMetabolismVaultContract.MetabolismStateMutationGuardMask))
                    return false;
                metabolicGuard = true;
                if (!vault.TryLockBuffer(ShinobuRadiationMutationConstants.MutationStateBuffer, OwnerSystem))
                    return false;
                mutationLocked = true;
                if (!vault.TryLockBuffer(BufferID.ShinobuMetabolismStates, OwnerSystem))
                    return false;
                metabolicLocked = true;

                RadiationMutationTuningDTO tuning = ShinobuRadiationMutationJobMath.SanitizeTuning(tuningRows[0]);
                int count = math.min(ShinobuRadiationMutationConstants.DefaultEntityCapacity, math.min(mutationStates.Length, metabolicStates.Length));
                for (int i = 0; i < count; i++)
                {
                    MutationStateDTO mutation = mutationStates[i];
                    MetabolicStateDTO metabolic = metabolicStates[i];
                    RadiationMutationKernel.ApplyMetabolicBridge(ref mutation, ref metabolic, in tuning);
                    mutationStates[i] = mutation;
                    metabolicStates[i] = metabolic;
                }

                return true;
            }
            finally
            {
                if (metabolicLocked)
                    vault.TryUnlockBuffer(BufferID.ShinobuMetabolismStates, OwnerSystem);
                if (mutationLocked)
                    vault.TryUnlockBuffer(ShinobuRadiationMutationConstants.MutationStateBuffer, OwnerSystem);
                if (metabolicGuard)
                    vault.ReleaseMutationGuard(ShinobuMetabolismVaultContract.MetabolismStateMutationGuardMask);
            }
        }

        private void PatchLatestTelemetry(IDataVault vault, float elapsedMicroseconds)
        {
            if (!TryResolveOwnBuffer(ref _telemetryHandle, ShinobuRadiationMutationConstants.MutationTelemetryBuffer, ShinobuRadiationMutationConstants.TelemetryFrameCount, out NativeArray<RadiationMutationTelemetryEntry> telemetry))
                return;

            bool telemetryLocked = false;
            try
            {
                if (!vault.TryLockBuffer(ShinobuRadiationMutationConstants.MutationTelemetryBuffer, OwnerSystem))
                    return;
                telemetryLocked = true;
                int latest = (_telemetryCursor + telemetry.Length - 1) % telemetry.Length;
                RadiationMutationTelemetryEntry entry = telemetry[latest];
                RadiationMutationKernel.PatchTelemetry(ref entry, elapsedMicroseconds);
                telemetry[latest] = entry;
            }
            finally
            {
                if (telemetryLocked)
                    vault.TryUnlockBuffer(ShinobuRadiationMutationConstants.MutationTelemetryBuffer, OwnerSystem);
            }
        }

        private void PublishVisualScalars(IDataVault vault)
        {
            if (!TryReadCachedBuffer(in _mutationStateHandle, ShinobuRadiationMutationConstants.MutationStateBuffer, ShinobuRadiationMutationConstants.DefaultEntityCapacity, out NativeArray<MutationStateDTO> states) ||
                states.Length <= 0 ||
                !TryReadCachedBuffer(in _tuningHandle, ShinobuRadiationMutationConstants.MutationTuningBuffer, 1, out NativeArray<RadiationMutationTuningDTO> tuningRows) ||
                tuningRows.Length <= 0)
            {
                return;
            }

            MutationStateDTO state = states[0];
            RadiationMutationTuningDTO tuning = ShinobuRadiationMutationJobMath.SanitizeTuning(tuningRows[0]);
            float quality = ResolveGlobalQualityWeight();
            float severity = math.saturate(state.MutationSeverity01);
            float highCostWeight = ShinobuRadiationMutationJobMath.Smooth01((quality - 0.5f) * 2f);
            float pulse = 1f + highCostWeight * tuning.ShaderPulseStrength * (0.5f + 0.5f * MathLodApproximation.ApproxSinBhaskara(_frameCounter * 0.173f));
            HectonShaderGlobalDataVaultBridge.PublishRadiationMutation(new Vector4(
                math.saturate(severity * pulse),
                math.saturate(state.MaxStaminaPenalty),
                math.saturate(state.HealingSuppression01),
                quality));
        }

        private void EmitToxicBloodVfxIfNeeded(IDataVault vault)
        {
            if (!emitToxicBloodVfx ||
                !TryGetMutationState(out MutationStateDTO state) ||
                !TryGetTuning(out RadiationMutationTuningDTO tuning))
            {
                return;
            }

            float severity = math.saturate(state.MutationSeverity01);
            if (severity < tuning.ToxicBloodThreshold01)
                return;

            float quality = ResolveGlobalQualityWeight();
            uint cadence = (uint)math.round(math.lerp(96f, 24f, quality));
            uint frame = _frameCounter;
            uint safeCadence = cadence == 0u ? 1u : cadence;
            if (frame - _lastVfxFrame < safeCadence)
                return;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition aup))
                return;

            _lastVfxFrame = frame;
            DebrisSpawnSignal signal = new DebrisSpawnSignal
            {
                PositionAup = aup,
                SpeciesHash = ShinobuRadiationMutationConstants.ToxicBloodSpeciesHash,
                SourceEntityId = ShinobuRadiationMutationConstants.SourceHash,
                Intensity01 = math.saturate((severity - tuning.ToxicBloodThreshold01) * math.rcp(math.max(0.0001f, 1f - tuning.ToxicBloodThreshold01))),
                DebrisKind = DebrisSpawnSignal.DebrisKindOrganicScrap,
                Flags = DebrisSpawnSignal.FlagComputeShard,
                Quantity = (ushort)math.clamp(1 + (int)math.round(quality * 3f), 1, 4)
            };
            SignalBus<DebrisSpawnSignal>.TryPush(in signal);
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition aup)
        {
            aup = default;
            IPlayerRuntimeContext player = _playerContext;
            if (player != null && player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) && snapshot.Aup.IsFinite())
            {
                aup = snapshot.Aup;
                return true;
            }

            return false;
        }

        private void TryDumpAutopsyIfFaulted(IDataVault vault)
        {
            if (_autopsyDumped ||
                !TryResolveOwnBuffer(ref _telemetryHandle, ShinobuRadiationMutationConstants.MutationTelemetryBuffer, ShinobuRadiationMutationConstants.TelemetryFrameCount, out NativeArray<RadiationMutationTelemetryEntry> telemetry) ||
                telemetry.Length <= 0)
            {
                return;
            }

            int latestIndex = (_telemetryCursor + telemetry.Length - 1) % telemetry.Length;
            RadiationMutationTelemetryEntry entry = telemetry[latestIndex];
            bool faulted = (entry.Flags & (RadiationMutationFlags.NonFiniteSanitized | RadiationMutationFlags.OverBudget)) != 0u ||
                           !math.isfinite(entry.MutationSeverity01) ||
                           !math.isfinite(entry.MaxStaminaPenalty) ||
                           !math.isfinite(entry.HealingSuppression01);
            if (!faulted)
                return;

            _autopsyDumped = true;
            DumpAutopsyReport(telemetry);
        }

        private void DumpAutopsyReport(NativeArray<RadiationMutationTelemetryEntry> telemetry)
        {
            try
            {
                string directory = Path.GetDirectoryName(_dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[32];
                    WriteUInt64LittleEndian(header.Slice(0, 8), ShinobuRadiationMutationConstants.DumpMagic);
                    WriteUInt32LittleEndian(header.Slice(8, 4), ShinobuRadiationMutationConstants.DumpVersion);
                    WriteUInt32LittleEndian(header.Slice(12, 4), (uint)telemetry.Length);
                    WriteUInt32LittleEndian(header.Slice(16, 4), (uint)UnsafeUtility.SizeOf<RadiationMutationTelemetryEntry>());
                    WriteUInt32LittleEndian(header.Slice(20, 4), unchecked((uint)_telemetryCursor));
                    WriteUInt32LittleEndian(header.Slice(24, 4), ShinobuRadiationMutationConstants.SourceHash);
                    WriteUInt32LittleEndian(header.Slice(28, 4), _frameCounter);
                    stream.Write(header);

                    void* telemetryPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    int byteCount = telemetry.Length * UnsafeUtility.SizeOf<RadiationMutationTelemetryEntry>();
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

            if (!TryResolveOwnBuffer(ref _profilesHandle, ShinobuRadiationMutationConstants.MutationProfileBuffer, ShinobuRadiationMutationConstants.ProfileCapacity, out NativeArray<RadiationMutationProfileDTO> profiles) ||
                !TryResolveOwnBuffer(ref _tuningHandle, ShinobuRadiationMutationConstants.MutationTuningBuffer, 1, out NativeArray<RadiationMutationTuningDTO> tuning) ||
                !TryResolveOwnBuffer(ref _csvScratchHandle, ShinobuRadiationMutationConstants.MutationCsvScratchBuffer, ShinobuRadiationMutationConstants.CsvMaxBytes, out NativeArray<byte> scratch))
            {
                return false;
            }

            bool profilesLocked = false;
            bool tuningLocked = false;
            bool scratchLocked = false;
            try
            {
                if (!vault.TryLockBuffer(ShinobuRadiationMutationConstants.MutationProfileBuffer, OwnerSystem))
                    return false;
                profilesLocked = true;
                if (!vault.TryLockBuffer(ShinobuRadiationMutationConstants.MutationTuningBuffer, OwnerSystem))
                    return false;
                tuningLocked = true;
                if (!vault.TryLockBuffer(ShinobuRadiationMutationConstants.MutationCsvScratchBuffer, OwnerSystem))
                    return false;
                scratchLocked = true;

                byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                int count = 0;
                using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long fileBytes = stream.Length;
                    if (fileBytes <= 0L)
                        return false;

                    int targetBytes = (int)math.min(fileBytes, scratch.Length);
                    Span<byte> scratchSpan = new Span<byte>(scratchPtr, targetBytes);
                    while (count < targetBytes)
                    {
                        int read = stream.Read(scratchSpan.Slice(count));
                        if (read <= 0)
                            break;
                        count += read;
                    }
                }

                if (count <= 0)
                    return false;

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
                    vault.TryUnlockBuffer(ShinobuRadiationMutationConstants.MutationCsvScratchBuffer, OwnerSystem);
                if (tuningLocked)
                    vault.TryUnlockBuffer(ShinobuRadiationMutationConstants.MutationTuningBuffer, OwnerSystem);
                if (profilesLocked)
                    vault.TryUnlockBuffer(ShinobuRadiationMutationConstants.MutationProfileBuffer, OwnerSystem);
            }

            return false;
#endif
        }

#if UNITY_EDITOR
        private static bool ParseProfilesCsv(ReadOnlySpan<byte> bytes, NativeArray<RadiationMutationProfileDTO> profiles, NativeArray<RadiationMutationTuningDTO> tuningRows)
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
                if (TryParseProfileLine(line, out RadiationMutationProfileDTO profile))
                {
                    profiles[profileCount++] = profile;
                    if (profileCount == 1 && tuningRows.IsCreated && tuningRows.Length > 0)
                    {
                        RadiationMutationTuningDTO tuning = ShinobuRadiationMutationJobMath.SanitizeTuning(tuningRows[0]);
                        tuning.SafeDoseRad = profile.SafeDoseRad;
                        tuning.FatalDoseRad = profile.FatalDoseRad;
                        tuning.MaxStaminaPenaltyPercent = profile.MaxStaminaPenaltyPercent;
                        tuning.HealingDecayPerSecond = profile.HealingDecayPerSecond;
                        tuning.ToxicBloodThreshold01 = profile.ToxicBloodThreshold01;
                        tuning.Flags |= RadiationMutationFlags.CsvProfile;
                        tuningRows[0] = ShinobuRadiationMutationJobMath.SanitizeTuning(tuning);
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

        private static bool TryParseProfileLine(ReadOnlySpan<byte> line, out RadiationMutationProfileDTO profile)
        {
            profile = default;
            if (line.Length == 0 || line[0] == (byte)'#')
                return false;

            int cursor = 0;
            ReadOnlySpan<byte> nameCell = ReadCell(line, ref cursor);
            if (nameCell.Length == 0 || IsHeaderCell(nameCell))
                return false;

            if (!TryParseAsciiFloat(ReadCell(line, ref cursor), out float safeDose) ||
                !TryParseAsciiFloat(ReadCell(line, ref cursor), out float fatalDose) ||
                !TryParseAsciiFloat(ReadCell(line, ref cursor), out float staminaPenalty) ||
                !TryParseAsciiFloat(ReadCell(line, ref cursor), out float healingDecay) ||
                !TryParseAsciiFloat(ReadCell(line, ref cursor), out float toxicBloodThreshold))
            {
                return false;
            }

            profile.ProfileHash = HashLowerAscii(nameCell);
            profile.SafeDoseRad = safeDose;
            profile.FatalDoseRad = math.max(safeDose + 1f, fatalDose);
            profile.MaxStaminaPenaltyPercent = staminaPenalty;
            profile.HealingDecayPerSecond = healingDecay;
            profile.ToxicBloodThreshold01 = toxicBloodThreshold;
            profile.Flags = RadiationMutationFlags.CsvProfile;
            return true;
        }

        private static RadiationMutationProfileDTO BuildDefaultProfile(uint hash)
        {
            RadiationMutationTuningDTO tuning = ShinobuRadiationMutationJobMath.BuildDefaultTuning();
            return new RadiationMutationProfileDTO
            {
                ProfileHash = hash,
                SafeDoseRad = tuning.SafeDoseRad,
                FatalDoseRad = tuning.FatalDoseRad,
                MaxStaminaPenaltyPercent = tuning.MaxStaminaPenaltyPercent,
                HealingDecayPerSecond = tuning.HealingDecayPerSecond,
                ToxicBloodThreshold01 = tuning.ToxicBloodThreshold01
            };
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

            if (!vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private bool TryBindExistingSnapshot<T>(
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

            if (vault.TryReadHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            handle = default;
            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle))
            {
                buffer = default;
                return false;
            }

            if (!vault.TryReadHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
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
            return frame * ShinobuMetabolismConstants.NominalSlowTickSeconds;
        }

        private static float ResolveElapsedMicroseconds(long startTimestamp, long endTimestamp)
        {
            long rawDelta = endTimestamp - startTimestamp;
            long delta = rawDelta > 0L ? rawDelta : 0L;
            double microseconds = delta * 1000000.0 / Stopwatch.Frequency;
            return math.isfinite(microseconds) ? (float)math.min(microseconds, float.MaxValue) : 0f;
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
            return hash != 0u ? hash : ShinobuRadiationMutationConstants.SourceHash;
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
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
                // COLD ALLOC: dispatcher PRE_SIMULATION stamina bridge adapter; no gameplay state is stored here.
                _preSimulationPhase = new PreSimulationPhaseSystem(this);
            }

            if (_visualSyncPhase == null)
            {
                // COLD ALLOC: dispatcher VISUAL_SYNC shader/VFX adapter; presentation truth remains in Vault/shader scalar.
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
            _mutationStateHandle = default;
            _tuningHandle = default;
            _telemetryHandle = default;
            _profilesHandle = default;
            _csvScratchHandle = default;
            _mockDoseHandle = default;
            _radiationStateHandle = default;
            _metabolicStateHandle = default;
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
            private readonly ShinobuRadiationMutationRuntime _owner;

            public PreSimulationPhaseSystem(ShinobuRadiationMutationRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() { return 0x53333450u; }
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
            private readonly ShinobuRadiationMutationRuntime _owner;

            public VisualSyncPhaseSystem(ShinobuRadiationMutationRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() { return 0x53333456u; }
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
