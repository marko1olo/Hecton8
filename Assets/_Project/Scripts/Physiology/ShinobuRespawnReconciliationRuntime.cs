using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Physiology;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Determinism;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Physiology
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physiology/Respawn Reconciliation Runtime")]
    public sealed unsafe class ShinobuRespawnReconciliationRuntime : MonoBehaviour, IGlobalRegistryHotSwapListener
    {
        private static int s_x001ShinobuRespawnReconciliationRuntimeSignalPushDropCount;
        private const SystemID OwnerSystem = SystemID.GameplayPlayer;
        private const uint SystemHash = ShinobuRespawnConstants.SourceHash;
        private const uint DefaultPlayerHash = 0x504C5952u; // PLYR
        private const ulong DumpMagic = 0x5253504E53524745ul; // RSPNSRGE
        private const uint DumpVersion = 1u;
        private const string CsvRelativePath = "respawn_penalty_rules.csv";
        private const string MedicalBayCsvRelativePath = "medical_bay_profiles.csv";
        private const string LegacyMedicalBayCsvRelativePath = "respawn_medical_bays.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_329.bin";
        private const string LegacyDumpRelativePath = "Docs/AgentLogs/Dump_RECONCILIATION_SURGEON.bin";

        private static readonly double s_ticksToMicroseconds = 1000000.0 / Stopwatch.Frequency;
        private static readonly ulong JobMutationGuardMask =
            MutationGuardBit(ShinobuRespawnConstants.RespawnStateBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.RespawnRequestBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.MedicalBayRespawnPointsBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.RespawnFadeBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.RespawnTelemetryRingBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.RespawnTelemetryCursorBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.RespawnTuningBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.RespawnPenaltyRulesBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.RespawnPenaltyRuleCountBuffer) |
            MutationGuardBit(BufferID.ShinobuPhysiologyVitals) |
            MutationGuardBit(BufferID.ShinobuDecompressionStates) |
            MutationGuardBit(BufferID.ShinobuTissueCompartments) |
            MutationGuardBit(BufferID.ShinobuPhysiologyScalars) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismStatesBuffer) |
            MutationGuardBit(ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer) |
            MutationGuardBit(BufferID.PlayerKinematicState);
        private static readonly ulong DefaultsMutationGuardMask =
            MutationGuardBit(ShinobuRespawnConstants.RespawnTuningBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.RespawnFadeBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.RespawnStateBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.RespawnRequestBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.RespawnTelemetryCursorBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.RespawnPenaltyRuleCountBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.MedicalBayRespawnPointsBuffer);
        private static readonly ulong RequestMutationGuardMask =
            MutationGuardBit(ShinobuRespawnConstants.RespawnRequestBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.RespawnStateBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.RespawnFadeBuffer);
        private static readonly ulong TuningMutationGuardMask =
            MutationGuardBit(ShinobuRespawnConstants.RespawnTuningBuffer);
        private static readonly ulong TelemetryMutationGuardMask =
            MutationGuardBit(ShinobuRespawnConstants.RespawnTelemetryRingBuffer) |
            MutationGuardBit(ShinobuRespawnConstants.RespawnTelemetryCursorBuffer);
#if UNITY_EDITOR
        private static readonly ulong MedicalBayCsvMutationGuardMask =
            MutationGuardBit(ShinobuRespawnConstants.MedicalBayRespawnPointsBuffer);
        private static readonly ulong PenaltyRuleCsvMutationGuardMask =
            MutationGuardBit(ShinobuRespawnConstants.RespawnPenaltyRulesBuffer);
        private static readonly ulong PenaltyRuleCountCsvMutationGuardMask =
            MutationGuardBit(ShinobuRespawnConstants.RespawnPenaltyRuleCountBuffer);
        private static readonly byte[] s_csvImportScratch = new byte[ShinobuRespawnConstants.CsvScratchBytes];
        private static readonly MedicalBayDTO[] s_medicalBayImportScratch = new MedicalBayDTO[ShinobuRespawnConstants.MockMedicalBayCapacity];
        private static readonly InventoryDeathPenaltyRuleDTO[] s_penaltyRuleImportScratch = new InventoryDeathPenaltyRuleDTO[ShinobuRespawnConstants.PenaltyRuleCapacity];
        private static int s_csvImportScratchBusy;
#endif
        private static ShinobuRespawnReconciliationRuntime s_active;

        private VaultGenerationHandle<RespawnStateDTO> _stateHandle;
        private VaultGenerationHandle<RespawnRequestDTO> _requestHandle;
        private VaultGenerationHandle<MedicalBayDTO> _medicalBayHandle;
        private VaultGenerationHandle<RespawnFadeDTO> _fadeHandle;
        private VaultGenerationHandle<RespawnTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<RespawnTelemetryCursor64> _telemetryCursorHandle;
        private VaultGenerationHandle<RespawnTuningDTO> _tuningHandle;
        private VaultGenerationHandle<InventoryDeathPenaltyRuleDTO> _penaltyRulesHandle;
        private VaultGenerationHandle<int> _penaltyRuleCountHandle;
        private VaultGenerationHandle<PhysiologyDTO> _vitalsHandle;
        private VaultGenerationHandle<DecompressionStateDTO> _decompressionHandle;
        private VaultGenerationHandle<TissueCompartmentDTO> _tissueHandle;
        private VaultGenerationHandle<PhysiologyScalarsDTO> _scalarHandle;
        private VaultGenerationHandle<MetabolicStateDTO> _metabolismHandle;
        private VaultGenerationHandle<GasPhysiologyStateDTO> _gasStateHandle;
        private VaultGenerationHandle<LockstepPlayerKinematicState> _playerKinematicHandle;

        private IDataVault _dataVault;
        private JobHandle _activeHandle;
        private PreSimulationPhaseSystem _preSimulationPhase;
        private SimulationPhaseSystem _simulationPhase;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private VisualSyncPhaseSystem _visualSyncPhase;
        private string _csvPath;
        private string _medicalBayCsvPath;
        private string _legacyMedicalBayCsvPath;
        private string _dumpPath;
        private string _legacyDumpPath;
        private uint _lastRequestSequence;
        private uint _lastRequestPlayerHash;
        private uint _lastCommittedTransformSequence;
        private uint _activeLoadOperationId;
        private SaveStatusSignal _completedLoadPendingRespawnCancel;
        private uint _mockLethalSequence;
        private uint _lastFrame;
        private int _lastInventoryPenaltyResultSnapshotGeneration;
        private float _lastQualityWeight = 1f;
        private float _lastScheduleMicroseconds;
        private bool _registeredHotSwap;
        private bool _registeredPreSimulation;
        private bool _registeredSimulation;
        private bool _registeredPostSimulation;
        private bool _registeredVisualSync;
        private bool _defaultsInitialized;
        private bool _medicalBayCsvInitialized;
        private bool _penaltyCsvInitialized;
        private bool _jobScheduled;
        private bool _jobBuffersLocked;
        private bool _dumpedFault;
        private bool _loadOperationInProgress;
        private bool _loadCompletionRespawnCancelPending;
        private bool _respawnCommitSuppressedForLoad;
        private bool _respawnDearLieVisualActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_active = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSceneRuntime()
        {
            if (!Application.isPlaying || s_active != null)
                return;

            GameObject host = new GameObject("SHINOBU_329_RespawnReconciliation"); // COLD ALLOC: GameObject[1] - dispatcher host for Vault-only respawn reconciliation - owner: SHINOBU_329
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<ShinobuRespawnReconciliationRuntime>();
        }

        private void Awake()
        {
            if (s_active != null && !ReferenceEquals(s_active, this))
            {
                enabled = false;
                return;
            }

            s_active = this;
            string root = BuildProjectRootPathCold();
            _csvPath = Path.GetFullPath(Path.Combine(root, CsvRelativePath));
            _medicalBayCsvPath = Path.GetFullPath(Path.Combine(root, MedicalBayCsvRelativePath));
            _legacyMedicalBayCsvPath = Path.GetFullPath(Path.Combine(root, LegacyMedicalBayCsvRelativePath));
            _dumpPath = DumpRelativePath;
            _legacyDumpPath = LegacyDumpRelativePath;

            // COLD ALLOC: IDispatcherSystem[4] - phase adapters registered into the dispatcher graph - owner: SHINOBU_329
            _preSimulationPhase = new PreSimulationPhaseSystem(this);
            _simulationPhase = new SimulationPhaseSystem(this);
            _postSimulationPhase = new PostSimulationPhaseSystem(this);
            _visualSyncPhase = new VisualSyncPhaseSystem(this);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (s_active != null && !ReferenceEquals(s_active, this))
            {
                enabled = false;
                return;
            }

            s_active = this;
            ConfigureSignalLanes();
            TryRegisterHotSwap();
            _dataVault = BindVaultCold();
            if (EnsureVaultState(_dataVault) && HydrateColdDefaultsAndPenaltyRules())
            {
                RegisterDispatcherPhases();
            }
        }

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            _dataVault = BindVaultCold();
            if (EnsureVaultState(_dataVault) && HydrateColdDefaultsAndPenaltyRules())
            {
                RegisterDispatcherPhases();
            }
        }

        private void OnDisable()
        {
            CompleteActiveJobForTeardown();
            ClearRespawnDearLieVisualIfNeeded();
            UnregisterDispatcherPhases();
            TryUnregisterHotSwap();
            ReleaseOwnedVaultDescriptors(_dataVault);
            ClearCachedHandles();
            if (ReferenceEquals(s_active, this))
                s_active = null;
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            CompleteActiveJobForTeardown();
            ClearRespawnDearLieVisualIfNeeded();
            UnregisterDispatcherPhases();
            IDataVault previousVault = previousService as IDataVault;
            if (previousVault == null)
                previousVault = _dataVault;
            ReleaseOwnedVaultDescriptors(previousVault);
            _dataVault = currentService as IDataVault;
            ClearCachedHandles();
            _defaultsInitialized = false;
            _dumpedFault = false;
            if (_dataVault != null && EnsureVaultState(_dataVault) && HydrateColdDefaultsAndPenaltyRules())
            {
                RegisterDispatcherPhases();
            }
        }

        private void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            IDataVault vault = _dataVault;
            if (!HasHotVaultState(vault))
                return;

            if (timing.FrameId != 0u)
                _lastFrame = timing.FrameId;

            if (ConsumeLoadStatusSignals(vault))
                return;

            if (_jobScheduled)
            {
                if (!_activeHandle.IsCompleted)
                    return;

                if (!TryFinalizeActiveJobNoWait())
                    return;
            }

            _lastQualityWeight = ResolveQualityWeight();
            ReadOnlySpan<PlayerRespawnSignal> snapshot = SignalBus<PlayerRespawnSignal>.GetFrameSnapshot();
            if (snapshot.Length <= 0)
                return;

            for (int i = 0; i < snapshot.Length; i++)
            {
                PlayerRespawnSignal signal = snapshot[i];
                if (!IsAdmissibleRequestSignal(in signal))
                    continue;

                if (signal.Sequence == _lastRequestSequence)
                    continue;

                if (WriteRequestFromSignal(vault, in signal))
                    return;

                if ((signal.Flags & PlayerRespawnSignalFlags.InvalidDeathAup) != 0u ||
                    !math.all(math.isfinite(signal.DeathAUP)))
                {
                    TryWriteRejectedDeathTelemetry(vault, in signal);
                    _lastRequestSequence = signal.Sequence;
                    _lastRequestPlayerHash = 0u;
                }
            }
        }

        private bool ConsumeLoadStatusSignals(IDataVault vault)
        {
            ReadOnlySpan<SaveStatusSignal> snapshot = SignalBus<SaveStatusSignal>.GetFrameSnapshot();
            SaveStatusSignal completedLoad = default;
            bool loadCompleted = false;
            bool loadFailedOrRejected = false;
            for (int i = 0; i < snapshot.Length; i++)
            {
                SaveStatusSignal status = snapshot[i];
                if ((status.Flags & SaveStatusSignal.LoadOperationFlag) == 0)
                    continue;

                if (status.State == SaveStatusSignal.InProgress)
                {
                    _loadOperationInProgress = true;
                    _activeLoadOperationId = status.OperationId;
                    continue;
                }

                if (status.State == SaveStatusSignal.Completed ||
                    status.State == SaveStatusSignal.Failed ||
                    status.State == SaveStatusSignal.Rejected)
                {
                    if (_activeLoadOperationId == 0u || status.OperationId == _activeLoadOperationId)
                    {
                        _loadOperationInProgress = false;
                        _activeLoadOperationId = 0u;
                        if (status.State == SaveStatusSignal.Completed)
                        {
                            completedLoad = status;
                            loadCompleted = true;
                        }
                        else
                        {
                            loadFailedOrRejected = true;
                        }
                    }
                }
            }

            if (loadCompleted)
            {
                if (completedLoad.Frame == 0u)
                    completedLoad.Frame = _lastFrame;
                QueueOrApplyCompletedLoadRespawnCancel(vault, in completedLoad);
                return true;
            }

            if (loadFailedOrRejected)
                TryReleaseSuppressedRespawnCommitAfterLoadFailure();

            if (_loadOperationInProgress)
                return true;

            return false;
        }

        private void QueueOrApplyCompletedLoadRespawnCancel(IDataVault vault, in SaveStatusSignal completedLoad)
        {
            _completedLoadPendingRespawnCancel = completedLoad;
            if (_jobScheduled)
            {
                _loadCompletionRespawnCancelPending = true;
                return;
            }

            TryFlushCompletedLoadRespawnCancel(vault);
        }

        private bool TryFlushCompletedLoadRespawnCancel(IDataVault vault)
        {
            SaveStatusSignal completedLoad = _completedLoadPendingRespawnCancel;
            if (completedLoad.State != SaveStatusSignal.Completed && !_loadCompletionRespawnCancelPending)
                return false;

            if (completedLoad.Frame == 0u)
                completedLoad.Frame = _lastFrame;

            bool canceled = TryCancelPendingRespawnForLoad(vault, in completedLoad);
            _completedLoadPendingRespawnCancel = default;
            _loadCompletionRespawnCancelPending = false;
            _respawnCommitSuppressedForLoad = false;
            return canceled;
        }

        private bool TryReleaseSuppressedRespawnCommitAfterLoadFailure()
        {
            if (!_respawnCommitSuppressedForLoad)
                return false;

            _respawnCommitSuppressedForLoad = false;
            return TryTransformCommittedRespawnSignal();
        }

        private bool TryCancelPendingRespawnForLoad(IDataVault vault, in SaveStatusSignal status)
        {
            if (!HasHotVaultState(vault))
                return false;

            NativeArray<RespawnRequestDTO> requestArray = ResolveVaultBuffer(vault, in _requestHandle);
            NativeArray<RespawnStateDTO> stateArray = ResolveVaultBuffer(vault, in _stateHandle);
            NativeArray<RespawnFadeDTO> fadeArray = ResolveVaultBuffer(vault, in _fadeHandle);
            if (!HasRequiredLength(requestArray, 1) ||
                !HasRequiredLength(stateArray, 1) ||
                !HasRequiredLength(fadeArray, 1))
            {
                return false;
            }

            RespawnRequestDTO previousRequest = requestArray[0];
            RespawnStateDTO previousState = stateArray[0];
            RespawnFadeDTO previousFade = fadeArray[0];
            if ((previousRequest.Flags & ShinobuRespawnFlags.PendingRequest) == 0u &&
                (previousState.Flags & ShinobuRespawnFlags.RespawnActive) == 0u &&
                previousFade.DeathFadeIntensity <= 0.0001f)
            {
                return false;
            }

            if (!vault.TryAcquireMutationGuard(RequestMutationGuardMask))
                return false;

            bool cleared = false;
            try
            {
                RespawnFadeDTO clearedFade = default;
                clearedFade.GlobalQualityWeight = ResolveQualityWeight();
                clearedFade.Frame = status.Frame != 0u ? status.Frame : _lastFrame;
                requestArray[0] = default;
                stateArray[0] = default;
                fadeArray[0] = clearedFade;
                _lastRequestSequence = 0u;
                _lastRequestPlayerHash = 0u;
                _lastCommittedTransformSequence = 0u;
                cleared = true;
            }
            finally
            {
                vault.ReleaseMutationGuard(RequestMutationGuardMask);
            }

            if (!cleared)
                return false;

            TryWriteLoadCanceledRespawnTelemetry(vault, in previousRequest, in previousState, in previousFade, in status);
            ClearRespawnDearLieVisualIfNeeded();
            return true;
        }

        private JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
        {
            IDataVault vault = _dataVault;
            if (!HasHotVaultState(vault))
                return dependsOn;

            if (_jobScheduled)
            {
                if (!_activeHandle.IsCompleted)
                    return JobHandle.CombineDependencies(dependsOn, _activeHandle);

                if (!TryFinalizeActiveJobNoWait())
                    return JobHandle.CombineDependencies(dependsOn, _activeHandle);
            }

            if (_loadOperationInProgress)
                return dependsOn;

            if (!HasPendingRespawnWork(vault))
                return dependsOn;

            if (!TryResolveJobPointers(vault, out JobPointers pointers))
                return dependsOn;

            if (!TryLockJobBuffers(vault))
                return dependsOn;

            bool keepJobGuard = false;
            try
            {
                if ((pointers.Request->Flags & ShinobuRespawnFlags.PendingRequest) == 0u &&
                    (pointers.State->Flags & ShinobuRespawnFlags.RespawnActive) == 0u &&
                    pointers.Fade->DeathFadeIntensity <= 0.0001f)
                {
                    return dependsOn;
                }

                _lastFrame = context.Frame;
                _lastQualityWeight = ResolveQualityWeight();
                long start = Stopwatch.GetTimestamp();

                FindNearestMedicalBayJob nearestJob = default;
                nearestJob.RespawnState = pointers.State;
                nearestJob.RespawnRequest = pointers.Request;
                nearestJob.MedicalBays = pointers.MedicalBays;
                nearestJob.Tuning = pointers.Tuning;
                nearestJob.MedicalBayCount = pointers.MedicalBayCount;
                JobHandle nearestHandle = nearestJob.Schedule(dependsOn);

                ResetPlayerPhysiologyJob resetJob = default;
                resetJob.RespawnState = pointers.State;
                resetJob.RespawnRequest = pointers.Request;
                resetJob.MedicalBays = pointers.MedicalBays;
                resetJob.RespawnFade = pointers.Fade;
                resetJob.TelemetryRing = pointers.Telemetry;
                resetJob.TelemetryCursor = pointers.TelemetryCursor;
                resetJob.Tuning = pointers.Tuning;
                resetJob.PenaltyRules = pointers.PenaltyRules;
                resetJob.PenaltyRuleCount = pointers.PenaltyRuleCount;
                resetJob.Vitals = pointers.Vitals;
                resetJob.Decompression = pointers.Decompression;
                resetJob.Tissues = pointers.Tissues;
                resetJob.Scalars = pointers.Scalars;
                resetJob.Metabolism = pointers.Metabolism;
                resetJob.GasState = pointers.GasState;
                resetJob.PlayerKinematic = pointers.PlayerKinematic;
                resetJob.InventoryCommands = SignalBus<InventoryCommandSignal>.ParallelWriter;
                resetJob.InventoryCommandsBudget = SignalBus<InventoryCommandSignal>.ParallelWriterBudget;
                resetJob.InventoryDeathAupSignals = SignalBus<InventoryRespawnDeathAupSignal>.ParallelWriter;
                resetJob.InventoryDeathAupSignalsBudget = SignalBus<InventoryRespawnDeathAupSignal>.ParallelWriterBudget;
                resetJob.MedicalBayCount = pointers.MedicalBayCount;
                resetJob.TissueCount = pointers.TissueCount;
                resetJob.PenaltyCapacity = pointers.PenaltyCapacity;
                resetJob.Frame = context.Frame;
                resetJob.GlobalQualityWeight = _lastQualityWeight;
                resetJob.ScheduleMicroseconds = _lastScheduleMicroseconds;
                JobHandle resetHandle = resetJob.Schedule(nearestHandle);

                float dt = ResolveSimulationDelta(in timing);
                UpdateRespawnFadeJob fadeJob = default;
                fadeJob.RespawnState = pointers.State;
                fadeJob.RespawnFade = pointers.Fade;
                fadeJob.Tuning = pointers.Tuning;
                fadeJob.DeltaSeconds = dt;
                fadeJob.GlobalQualityWeight = _lastQualityWeight;
                fadeJob.Frame = context.Frame;
                JobHandle fadeHandle = fadeJob.Schedule(resetHandle);

                _activeHandle = fadeHandle;
                _jobScheduled = true;
                _lastScheduleMicroseconds = (float)((Stopwatch.GetTimestamp() - start) * s_ticksToMicroseconds);
                H8Memory.RegisterActiveJob(OwnerSystem, _activeHandle);
                keepJobGuard = true;
                return _activeHandle;
            }
            finally
            {
                if (!keepJobGuard)
                    UnlockJobBuffers();
            }
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            TryFinalizeActiveJobNoWait();
            if (_jobScheduled)
                return;

            ConsumeInventoryPenaltyResultSignals();
            TryDumpFaultedTelemetry();
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (_jobScheduled)
            {
                if (!_activeHandle.IsCompleted)
                    return;

                if (!TryFinalizeActiveJobNoWait())
                    return;
            }

            IDataVault vault = _dataVault;
            if (!HasHotVaultState(vault))
                return;

            NativeArray<RespawnFadeDTO> fadeArray = ResolveVaultBuffer(vault, in _fadeHandle);
            if (!HasRequiredLength(fadeArray, 1))
                return;

            RespawnFadeDTO fade = fadeArray[0];
            Vector4 payload = default;
            payload.x = math.saturate(fade.DeathFadeIntensity);
            payload.y = math.saturate(fade.ChromaticAberration01);
            payload.z = math.saturate(fade.FilmGrain01);
            payload.w = math.saturate(fade.GlobalQualityWeight);
            bool visualActive = payload.x > 0.0001f || (fade.Flags & ShinobuRespawnFlags.RespawnActive) != 0u;
            if (!visualActive)
            {
                if (!_respawnDearLieVisualActive)
                    return;

                payload.x = 0f;
                payload.y = 0f;
                payload.z = 0f;
            }

            HectonShaderGlobalDataVaultBridge.PublishRespawnDearLie(vault, payload);
            _respawnDearLieVisualActive = visualActive;
        }

        private bool WriteRequestFromSignal(IDataVault vault, in PlayerRespawnSignal signal)
        {
            if (!IsAdmissibleRequestSignal(in signal) ||
                !math.all(math.isfinite(signal.DeathAUP)))
                return false;

            NativeArray<RespawnRequestDTO> requestArray = ResolveVaultBuffer(vault, in _requestHandle);
            NativeArray<RespawnStateDTO> stateArray = ResolveVaultBuffer(vault, in _stateHandle);
            NativeArray<RespawnFadeDTO> fadeArray = ResolveVaultBuffer(vault, in _fadeHandle);
            if (!HasRequiredLength(requestArray, 1) ||
                !HasRequiredLength(stateArray, 1) ||
                !HasRequiredLength(fadeArray, 1))
            {
                return false;
            }

            if (!TryBuildDeathSequenceFade(vault, signal.Frame, out RespawnFadeDTO fade))
                return false;

            uint playerHash = signal.PlayerHash != 0u ? signal.PlayerHash : DefaultPlayerHash;
            bool invalidDeathAup = (signal.Flags & PlayerRespawnSignalFlags.InvalidDeathAup) != 0u;
            uint invalidRouteFlags = invalidDeathAup
                ? ShinobuRespawnFlags.NanDetected | ShinobuRespawnFlags.InvalidTargetAup
                : 0u;
            RespawnRequestDTO request = default;
            request.DeathAUP = signal.DeathAUP;
            request.PlayerHash = playerHash;
            request.DamageHash = signal.DamageHash;
            request.Frame = signal.Frame;
            request.Sequence = signal.Sequence;
            request.Flags = ShinobuRespawnFlags.PendingRequest |
                            ShinobuRespawnFlags.DeathSequenceBlackoutPrimed |
                            invalidRouteFlags;
            request.MedicalBayHashID = 0u;

            RespawnStateDTO state = default;
            state.TargetAUP = math.all(math.isfinite(signal.RespawnAUP)) ? signal.RespawnAUP : DefaultFallbackAup();
            state.MedicalBayHashID = signal.MedicalBayHashID;
            state.Flags = ShinobuRespawnFlags.RespawnActive |
                          ShinobuRespawnFlags.PendingRequest |
                          ShinobuRespawnFlags.DeathSequenceBlackoutPrimed |
                          invalidRouteFlags;

            if (!vault.TryAcquireMutationGuard(RequestMutationGuardMask))
                return false;

            try
            {
                fadeArray[0] = fade;
                requestArray[0] = request;
                stateArray[0] = state;
                _lastRequestSequence = signal.Sequence;
                _lastRequestPlayerHash = playerHash;
                _lastCommittedTransformSequence = 0u;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(RequestMutationGuardMask);
            }
        }

        private bool TryBuildDeathSequenceFade(IDataVault vault, uint frame, out RespawnFadeDTO fade)
        {
            fade = default;
            NativeArray<RespawnTuningDTO> tuningArray = ResolveVaultBuffer(vault, in _tuningHandle);
            if (!HasRequiredLength(tuningArray, 1))
                return false;

            float quality = ResolveQualityWeight();
            RespawnTuningDTO tuning = SanitizeTuning(tuningArray[0]);
            float fadeRate = math.lerp(
                math.max(0.0001f, tuning.HighQualityFadeRate),
                math.max(0.0001f, tuning.LowQualityFadeRate),
                1f - quality);
            float detailGate = Smooth01(math.saturate((quality - 0.18f) * 1.6129032f));
            fade.DeathFadeIntensity = 1f;
            fade.FadeRate = fadeRate;
            fade.ChromaticAberration01 = math.saturate(math.lerp(0f, 0.85f, detailGate));
            fade.FilmGrain01 = math.saturate(math.lerp(0.25f, 1f, quality));
            fade.GlobalQualityWeight = quality;
            fade.Frame = frame;
            fade.Flags = ShinobuRespawnFlags.RespawnActive |
                         ShinobuRespawnFlags.PendingRequest |
                         ShinobuRespawnFlags.DeathSequenceBlackoutPrimed;
            return true;
        }

        private bool TryWriteRejectedDeathTelemetry(IDataVault vault, in PlayerRespawnSignal signal)
        {
            if (vault == null ||
                !IsVaultDescriptorCreated(in _telemetryHandle) ||
                !IsVaultDescriptorCreated(in _telemetryCursorHandle))
            {
                return false;
            }

            NativeArray<RespawnTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _telemetryHandle);
            NativeArray<RespawnTelemetryCursor64> cursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            if (!HasRequiredLength(telemetry, ShinobuRespawnConstants.TelemetryFrameCount) ||
                !HasRequiredLength(cursor, 1))
            {
                return false;
            }

            uint flags = ShinobuRespawnFlags.NanDetected | ShinobuRespawnFlags.InvalidTargetAup;
            RespawnTelemetryEntry entry = default;
            entry.DeathAUP = SanitizeAup(signal.DeathAUP);
            entry.RespawnAUP = DefaultFallbackAup();
            entry.CauseHash = signal.DamageHash;
            entry.Frame = signal.Frame;
            entry.ReconcileMicroseconds = 0f;
            entry.Flags = flags;

            if (!vault.TryAcquireMutationGuard(TelemetryMutationGuardMask))
                return false;

            try
            {
                RespawnTelemetryCursor64 telemetryCursor = cursor[0];
                int index = telemetryCursor.Cursor % ShinobuRespawnConstants.TelemetryFrameCount;
                if (index < 0)
                    index += ShinobuRespawnConstants.TelemetryFrameCount;

                telemetry[index] = entry;
                telemetryCursor.Cursor = (index + 1) % ShinobuRespawnConstants.TelemetryFrameCount;
                telemetryCursor.Flags = flags;
                cursor[0] = telemetryCursor;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(TelemetryMutationGuardMask);
            }
        }

        private bool TryWriteLoadCanceledRespawnTelemetry(
            IDataVault vault,
            in RespawnRequestDTO request,
            in RespawnStateDTO state,
            in RespawnFadeDTO fade,
            in SaveStatusSignal status)
        {
            if (vault == null ||
                !IsVaultDescriptorCreated(in _telemetryHandle) ||
                !IsVaultDescriptorCreated(in _telemetryCursorHandle))
            {
                return false;
            }

            NativeArray<RespawnTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _telemetryHandle);
            NativeArray<RespawnTelemetryCursor64> cursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            if (!HasRequiredLength(telemetry, ShinobuRespawnConstants.TelemetryFrameCount) ||
                !HasRequiredLength(cursor, 1))
            {
                return false;
            }

            uint routeFlags = request.Flags | state.Flags | fade.Flags | ShinobuRespawnFlags.CanceledByLoad;
            RespawnTelemetryEntry entry = default;
            entry.DeathAUP = SanitizeAup(request.DeathAUP);
            entry.RespawnAUP = SanitizeAup(state.TargetAUP);
            entry.CauseHash = request.DamageHash != 0u ? request.DamageHash : status.OperationId;
            entry.Frame = status.Frame != 0u ? status.Frame : _lastFrame;
            entry.ReconcileMicroseconds = 0f;
            entry.Flags = routeFlags;

            if (!vault.TryAcquireMutationGuard(TelemetryMutationGuardMask))
                return false;

            try
            {
                RespawnTelemetryCursor64 telemetryCursor = cursor[0];
                int index = telemetryCursor.Cursor % ShinobuRespawnConstants.TelemetryFrameCount;
                if (index < 0)
                    index += ShinobuRespawnConstants.TelemetryFrameCount;

                telemetry[index] = entry;
                telemetryCursor.Cursor = (index + 1) % ShinobuRespawnConstants.TelemetryFrameCount;
                telemetryCursor.Flags = routeFlags & (ShinobuRespawnFlags.NanDetected | ShinobuRespawnFlags.InvalidTargetAup | ShinobuRespawnFlags.CanceledByLoad);
                cursor[0] = telemetryCursor;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(TelemetryMutationGuardMask);
            }
        }

        private bool EnsureVaultState()
        {
            IDataVault vault = BindVaultCold();
            return vault != null && EnsureVaultState(vault);
        }

        private bool EnsureVaultState(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (AreVaultHandlesCreated())
            {
                if (AreVaultHandlesResolvable(vault))
                    return true;

                ClearCachedHandles();
            }

            if (!ShinobuRespawnLayoutGuards.ValidateRespawnLayouts())
                return false;

            bool created =
                TryAcquireOwnedVaultDescriptor(vault, ShinobuRespawnConstants.RespawnStateBuffer, 1, NativeArrayOptions.UninitializedMemory, out _stateHandle) &&
                TryAcquireOwnedVaultDescriptor(vault, ShinobuRespawnConstants.RespawnRequestBuffer, 1, NativeArrayOptions.UninitializedMemory, out _requestHandle) &&
                TryAcquireOwnedVaultDescriptor(vault, ShinobuRespawnConstants.MedicalBayRespawnPointsBuffer, ShinobuRespawnConstants.MockMedicalBayCapacity, NativeArrayOptions.UninitializedMemory, out _medicalBayHandle) &&
                TryAcquireOwnedVaultDescriptor(vault, ShinobuRespawnConstants.RespawnFadeBuffer, 1, NativeArrayOptions.UninitializedMemory, out _fadeHandle) &&
                TryAcquireOwnedVaultDescriptor(vault, ShinobuRespawnConstants.RespawnTelemetryRingBuffer, ShinobuRespawnConstants.TelemetryFrameCount, NativeArrayOptions.UninitializedMemory, out _telemetryHandle) &&
                TryAcquireOwnedVaultDescriptor(vault, ShinobuRespawnConstants.RespawnTelemetryCursorBuffer, 1, NativeArrayOptions.UninitializedMemory, out _telemetryCursorHandle) &&
                TryAcquireOwnedVaultDescriptor(vault, ShinobuRespawnConstants.RespawnTuningBuffer, 1, NativeArrayOptions.UninitializedMemory, out _tuningHandle) &&
                TryAcquireOwnedVaultDescriptor(vault, ShinobuRespawnConstants.RespawnPenaltyRulesBuffer, ShinobuRespawnConstants.PenaltyRuleCapacity, NativeArrayOptions.UninitializedMemory, out _penaltyRulesHandle) &&
                TryAcquireOwnedVaultDescriptor(vault, ShinobuRespawnConstants.RespawnPenaltyRuleCountBuffer, 1, NativeArrayOptions.UninitializedMemory, out _penaltyRuleCountHandle) &&
                TryGetExistingVaultDescriptor(vault, BufferID.ShinobuPhysiologyVitals, OwnerSystem, 1, out _vitalsHandle) &&
                TryGetExistingVaultDescriptor(vault, BufferID.ShinobuDecompressionStates, OwnerSystem, 1, out _decompressionHandle) &&
                TryGetExistingVaultDescriptor(vault, BufferID.ShinobuTissueCompartments, OwnerSystem, 1, out _tissueHandle) &&
                TryGetExistingVaultDescriptor(vault, BufferID.ShinobuPhysiologyScalars, OwnerSystem, 1, out _scalarHandle) &&
                TryGetExistingVaultDescriptor(vault, ShinobuMetabolismConstants.MetabolismStatesBuffer, OwnerSystem, 1, out _metabolismHandle) &&
                TryGetExistingVaultDescriptor(vault, ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer, OwnerSystem, 1, out _gasStateHandle) &&
                TryGetExistingVaultDescriptor(vault, BufferID.PlayerKinematicState, OwnerSystem, 1, out _playerKinematicHandle);
            if (!created)
            {
                ReleaseOwnedVaultDescriptors(vault);
                ClearCachedHandles();
            }

            return created;
        }

        private bool AreVaultHandlesCreated()
        {
            return AreOwnedVaultHandlesCreated() &&
                   IsVaultDescriptorOwnedBy(in _vitalsHandle, OwnerSystem) &&
                   IsVaultDescriptorOwnedBy(in _decompressionHandle, OwnerSystem) &&
                   IsVaultDescriptorOwnedBy(in _tissueHandle, OwnerSystem) &&
                   IsVaultDescriptorOwnedBy(in _scalarHandle, OwnerSystem) &&
                   IsVaultDescriptorOwnedBy(in _metabolismHandle, OwnerSystem) &&
                   IsVaultDescriptorOwnedBy(in _gasStateHandle, OwnerSystem) &&
                   IsVaultDescriptorOwnedBy(in _playerKinematicHandle, OwnerSystem);
        }

        private bool AreOwnedVaultHandlesCreated()
        {
            return IsOwnedVaultDescriptor(in _stateHandle) &&
                   IsOwnedVaultDescriptor(in _requestHandle) &&
                   IsOwnedVaultDescriptor(in _medicalBayHandle) &&
                   IsOwnedVaultDescriptor(in _fadeHandle) &&
                   IsOwnedVaultDescriptor(in _telemetryHandle) &&
                   IsOwnedVaultDescriptor(in _telemetryCursorHandle) &&
                   IsOwnedVaultDescriptor(in _tuningHandle) &&
                   IsOwnedVaultDescriptor(in _penaltyRulesHandle) &&
                   IsOwnedVaultDescriptor(in _penaltyRuleCountHandle);
        }

        private bool AreVaultHandlesResolvable(IDataVault vault)
        {
            return AreOwnedVaultHandlesResolvable(vault) &&
                   IsVaultDescriptorResolvableByOwner(vault, in _vitalsHandle, OwnerSystem, 1) &&
                   IsVaultDescriptorResolvableByOwner(vault, in _decompressionHandle, OwnerSystem, 1) &&
                   IsVaultDescriptorResolvableByOwner(vault, in _tissueHandle, OwnerSystem, 1) &&
                   IsVaultDescriptorResolvableByOwner(vault, in _scalarHandle, OwnerSystem, 1) &&
                   IsVaultDescriptorResolvableByOwner(vault, in _metabolismHandle, OwnerSystem, 1) &&
                   IsVaultDescriptorResolvableByOwner(vault, in _gasStateHandle, OwnerSystem, 1) &&
                   IsVaultDescriptorResolvableByOwner(vault, in _playerKinematicHandle, OwnerSystem, 1);
        }

        private bool AreOwnedVaultHandlesResolvable(IDataVault vault)
        {
            return IsOwnedVaultDescriptorResolvable(vault, in _stateHandle, 1) &&
                   IsOwnedVaultDescriptorResolvable(vault, in _requestHandle, 1) &&
                   IsOwnedVaultDescriptorResolvable(vault, in _medicalBayHandle, ShinobuRespawnConstants.MockMedicalBayCapacity) &&
                   IsOwnedVaultDescriptorResolvable(vault, in _fadeHandle, 1) &&
                   IsOwnedVaultDescriptorResolvable(vault, in _telemetryHandle, ShinobuRespawnConstants.TelemetryFrameCount) &&
                   IsOwnedVaultDescriptorResolvable(vault, in _telemetryCursorHandle, 1) &&
                   IsOwnedVaultDescriptorResolvable(vault, in _tuningHandle, 1) &&
                   IsOwnedVaultDescriptorResolvable(vault, in _penaltyRulesHandle, ShinobuRespawnConstants.PenaltyRuleCapacity) &&
                   IsOwnedVaultDescriptorResolvable(vault, in _penaltyRuleCountHandle, 1);
        }

        private bool HasHotVaultState(IDataVault vault)
        {
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   AreVaultHandlesCreated() &&
                   AreVaultGenerationsCurrent(vault);
        }

        private bool AreVaultGenerationsCurrent(IDataVault vault)
        {
            return AreOwnedVaultGenerationsCurrent(vault) &&
                   IsVaultGenerationCurrentByOwner(vault, in _vitalsHandle, OwnerSystem) &&
                   IsVaultGenerationCurrentByOwner(vault, in _decompressionHandle, OwnerSystem) &&
                   IsVaultGenerationCurrentByOwner(vault, in _tissueHandle, OwnerSystem) &&
                   IsVaultGenerationCurrentByOwner(vault, in _scalarHandle, OwnerSystem) &&
                   IsVaultGenerationCurrentByOwner(vault, in _metabolismHandle, OwnerSystem) &&
                   IsVaultGenerationCurrentByOwner(vault, in _gasStateHandle, OwnerSystem) &&
                   IsVaultGenerationCurrentByOwner(vault, in _playerKinematicHandle, OwnerSystem);
        }

        private bool AreOwnedVaultGenerationsCurrent(IDataVault vault)
        {
            return IsOwnedVaultGenerationCurrent(vault, in _stateHandle) &&
                   IsOwnedVaultGenerationCurrent(vault, in _requestHandle) &&
                   IsOwnedVaultGenerationCurrent(vault, in _medicalBayHandle) &&
                   IsOwnedVaultGenerationCurrent(vault, in _fadeHandle) &&
                   IsOwnedVaultGenerationCurrent(vault, in _telemetryHandle) &&
                   IsOwnedVaultGenerationCurrent(vault, in _telemetryCursorHandle) &&
                   IsOwnedVaultGenerationCurrent(vault, in _tuningHandle) &&
                   IsOwnedVaultGenerationCurrent(vault, in _penaltyRulesHandle) &&
                   IsOwnedVaultGenerationCurrent(vault, in _penaltyRuleCountHandle);
        }

        private static bool IsVaultDescriptorCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static bool IsOwnedVaultDescriptor<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return IsVaultDescriptorOwnedBy(in handle, OwnerSystem);
        }

        private static bool IsVaultDescriptorOwnedBy<T>(in VaultGenerationHandle<T> handle, SystemID owner) where T : struct
        {
            return IsVaultDescriptorCreated(in handle) && handle.SystemID == (uint)owner;
        }

        private static bool IsVaultGenerationCurrent<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            return IsVaultDescriptorCreated(in handle) &&
                   TryResolveVaultBuffer(vault, in handle, out NativeArray<T> buffer) &&
                   buffer.IsCreated;
        }

        private static bool IsOwnedVaultGenerationCurrent<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            return IsOwnedVaultDescriptor(in handle) && IsVaultGenerationCurrent(vault, in handle);
        }

        private static bool IsVaultGenerationCurrentByOwner<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            SystemID owner) where T : struct
        {
            return IsVaultDescriptorOwnedBy(in handle, owner) &&
                   IsVaultGenerationCurrent(vault, in handle);
        }

        private static bool IsVaultDescriptorResolvable<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength) where T : struct
        {
            return TryResolveVaultBuffer(vault, in handle, out NativeArray<T> buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsOwnedVaultDescriptorResolvable<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength) where T : struct
        {
            return IsOwnedVaultDescriptor(in handle) &&
                   IsVaultDescriptorResolvable(vault, in handle, requiredLength);
        }

        private static bool IsVaultDescriptorResolvableByOwner<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            SystemID owner,
            int requiredLength) where T : struct
        {
            return IsVaultDescriptorOwnedBy(in handle, owner) &&
                   IsVaultDescriptorResolvable(vault, in handle, requiredLength);
        }

        private static bool HasRequiredLength<T>(NativeArray<T> buffer, int requiredLength) where T : struct
        {
            return buffer.IsCreated && buffer.Length >= requiredLength;
        }

        private static bool TryAcquireOwnedVaultDescriptor<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out VaultGenerationHandle<T> handle) where T : struct
        {
            handle = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (vault.TryGetGenerationHandle<T>(bufferId, out handle))
            {
                if (!IsOwnedVaultDescriptor(in handle))
                {
                    handle = default;
                    return false;
                }

                if (TryResolveVaultBuffer(vault, in handle, out NativeArray<T> existing) &&
                    existing.IsCreated &&
                    existing.Length >= requiredLength)
                {
                    return true;
                }
            }

            handle = default;
            if (vault.IsAllocationLocked)
                return false;

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystem, options);
            return IsOwnedVaultDescriptor(in handle) &&
                   TryResolveVaultBuffer(vault, in handle, out NativeArray<T> created) &&
                   created.IsCreated &&
                   created.Length >= requiredLength;
        }

        private static bool TryGetExistingVaultDescriptor<T>(
            IDataVault vault,
            BufferID bufferId,
            SystemID expectedOwner,
            int requiredLength,
            out VaultGenerationHandle<T> handle) where T : struct
        {
            handle = default;
            if (vault == null || requiredLength <= 0)
                return false;

            return vault.TryGetGenerationHandle<T>(bufferId, out handle) &&
                   IsVaultDescriptorOwnedBy(in handle, expectedOwner) &&
                   TryResolveVaultBuffer(vault, in handle, out NativeArray<T> existing) &&
                   existing.IsCreated &&
                   existing.Length >= requiredLength;
        }

        private static bool TryResolveVaultBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   IsVaultDescriptorCreated(in handle) &&
                   vault.TryResolveHandle(in handle, out buffer);
        }

        private static NativeArray<T> ResolveVaultBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            return TryResolveVaultBuffer(vault, in handle, out NativeArray<T> buffer) ? buffer : default;
        }

        private void ReleaseOwnedVaultDescriptors(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseVaultDescriptor(vault, in _stateHandle);
            ReleaseVaultDescriptor(vault, in _requestHandle);
            ReleaseVaultDescriptor(vault, in _medicalBayHandle);
            ReleaseVaultDescriptor(vault, in _fadeHandle);
            ReleaseVaultDescriptor(vault, in _telemetryHandle);
            ReleaseVaultDescriptor(vault, in _telemetryCursorHandle);
            ReleaseVaultDescriptor(vault, in _tuningHandle);
            ReleaseVaultDescriptor(vault, in _penaltyRulesHandle);
            ReleaseVaultDescriptor(vault, in _penaltyRuleCountHandle);
        }

        private static void ReleaseVaultDescriptor<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            if (IsOwnedVaultDescriptor(in handle))
                vault.ReleaseBuffer(in handle);
        }

        private void InitializeDefaultVaultContents()
        {
            if (_defaultsInitialized)
                return;

            IDataVault vault = _dataVault;
            if (!HasHotVaultState(vault))
                return;

            NativeArray<RespawnTuningDTO> tuning = ResolveVaultBuffer(vault, in _tuningHandle);
            NativeArray<RespawnFadeDTO> fade = ResolveVaultBuffer(vault, in _fadeHandle);
            NativeArray<RespawnStateDTO> state = ResolveVaultBuffer(vault, in _stateHandle);
            NativeArray<RespawnRequestDTO> request = ResolveVaultBuffer(vault, in _requestHandle);
            NativeArray<RespawnTelemetryCursor64> cursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            NativeArray<int> count = ResolveVaultBuffer(vault, in _penaltyRuleCountHandle);
            NativeArray<MedicalBayDTO> bays = ResolveVaultBuffer(vault, in _medicalBayHandle);
            if (!HasRequiredLength(tuning, 1) ||
                !HasRequiredLength(fade, 1) ||
                !HasRequiredLength(state, 1) ||
                !HasRequiredLength(request, 1) ||
                !HasRequiredLength(cursor, 1) ||
                !HasRequiredLength(count, 1) ||
                !HasRequiredLength(bays, ShinobuRespawnConstants.MockMedicalBayCapacity))
            {
                return;
            }

            RespawnTuningDTO defaultTuning = CreateDefaultTuning();
            RespawnFadeDTO defaultFade = default;
            defaultFade.GlobalQualityWeight = ResolveQualityWeight();
            MedicalBayDTO* defaultBays = stackalloc MedicalBayDTO[ShinobuRespawnConstants.MockMedicalBayCapacity];
            for (int index = 0; index < ShinobuRespawnConstants.MockMedicalBayCapacity; index++)
                defaultBays[index] = CreateMockRespawnPoint(index, ShinobuRespawnConstants.MockMedicalBayCapacity, defaultTuning.FallbackLifepodAUP);

            if (!vault.TryAcquireMutationGuard(DefaultsMutationGuardMask))
                return;

            try
            {
                tuning[0] = defaultTuning;
                fade[0] = defaultFade;
                state[0] = default;
                request[0] = default;
                cursor[0] = default;
                count[0] = 0;
                UnsafeUtility.MemCpy(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(bays),
                    defaultBays,
                    ShinobuRespawnConstants.MockMedicalBayCapacity * UnsafeUtility.SizeOf<MedicalBayDTO>());

                _defaultsInitialized = true;
            }
            finally
            {
                vault.ReleaseMutationGuard(DefaultsMutationGuardMask);
            }
        }

        private bool HydrateColdDefaultsAndPenaltyRules()
        {
            if (!_defaultsInitialized)
                InitializeDefaultVaultContents();

            if (!_defaultsInitialized)
                return false;

#if UNITY_EDITOR
            if (_defaultsInitialized && !_medicalBayCsvInitialized)
            {
                _medicalBayCsvInitialized = TryLoadMedicalBayCsv();
            }

            if (_defaultsInitialized && !_penaltyCsvInitialized)
            {
                _penaltyCsvInitialized = TryLoadPenaltyCsv();
            }
#endif

            return true;
        }

        private bool TryResolveJobPointers(IDataVault vault, out JobPointers pointers)
        {
            pointers = default;
            NativeArray<RespawnStateDTO> state = ResolveVaultBuffer(vault, in _stateHandle);
            NativeArray<RespawnRequestDTO> request = ResolveVaultBuffer(vault, in _requestHandle);
            NativeArray<MedicalBayDTO> bays = ResolveVaultBuffer(vault, in _medicalBayHandle);
            NativeArray<RespawnFadeDTO> fade = ResolveVaultBuffer(vault, in _fadeHandle);
            NativeArray<RespawnTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _telemetryHandle);
            NativeArray<RespawnTelemetryCursor64> cursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            NativeArray<RespawnTuningDTO> tuning = ResolveVaultBuffer(vault, in _tuningHandle);
            NativeArray<InventoryDeathPenaltyRuleDTO> penalty = ResolveVaultBuffer(vault, in _penaltyRulesHandle);
            NativeArray<int> penaltyCount = ResolveVaultBuffer(vault, in _penaltyRuleCountHandle);
            NativeArray<PhysiologyDTO> vitals = ResolveVaultBuffer(vault, in _vitalsHandle);
            NativeArray<DecompressionStateDTO> decompression = ResolveVaultBuffer(vault, in _decompressionHandle);
            NativeArray<TissueCompartmentDTO> tissues = ResolveVaultBuffer(vault, in _tissueHandle);
            NativeArray<PhysiologyScalarsDTO> scalars = ResolveVaultBuffer(vault, in _scalarHandle);
            NativeArray<MetabolicStateDTO> metabolism = ResolveVaultBuffer(vault, in _metabolismHandle);
            NativeArray<GasPhysiologyStateDTO> gasState = ResolveVaultBuffer(vault, in _gasStateHandle);
            NativeArray<LockstepPlayerKinematicState> kinematic = ResolveVaultBuffer(vault, in _playerKinematicHandle);

            if (!HasRequiredLength(state, 1) ||
                !HasRequiredLength(request, 1) ||
                !HasRequiredLength(bays, ShinobuRespawnConstants.MockMedicalBayCapacity) ||
                !HasRequiredLength(fade, 1) ||
                !HasRequiredLength(telemetry, ShinobuRespawnConstants.TelemetryFrameCount) ||
                !HasRequiredLength(cursor, 1) ||
                !HasRequiredLength(tuning, 1) ||
                !HasRequiredLength(penalty, ShinobuRespawnConstants.PenaltyRuleCapacity) ||
                !HasRequiredLength(penaltyCount, 1) ||
                !HasRequiredLength(vitals, 1) ||
                !HasRequiredLength(decompression, 1) ||
                !HasRequiredLength(tissues, 1) ||
                !HasRequiredLength(scalars, 1) ||
                !HasRequiredLength(metabolism, 1) ||
                !HasRequiredLength(gasState, 1) ||
                !HasRequiredLength(kinematic, 1))
            {
                return false;
            }

            pointers.State = (RespawnStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state);
            pointers.Request = (RespawnRequestDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(request);
            pointers.MedicalBays = (MedicalBayDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(bays);
            pointers.Fade = (RespawnFadeDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(fade);
            pointers.Telemetry = (RespawnTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(telemetry);
            pointers.TelemetryCursor = (RespawnTelemetryCursor64*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(cursor);
            pointers.Tuning = (RespawnTuningDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuning);
            pointers.PenaltyRules = (InventoryDeathPenaltyRuleDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(penalty);
            pointers.PenaltyRuleCount = (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(penaltyCount);
            pointers.Vitals = (PhysiologyDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(vitals);
            pointers.Decompression = (DecompressionStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(decompression);
            pointers.Tissues = (TissueCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tissues);
            pointers.Scalars = (PhysiologyScalarsDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scalars);
            pointers.Metabolism = (MetabolicStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(metabolism);
            pointers.GasState = (GasPhysiologyStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(gasState);
            pointers.PlayerKinematic = (LockstepPlayerKinematicState*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(kinematic);
            pointers.MedicalBayCount = bays.Length;
            pointers.TissueCount = tissues.Length;
            pointers.PenaltyCapacity = penalty.Length;
            return true;
        }

        private bool HasPendingRespawnWork(IDataVault vault)
        {
            NativeArray<RespawnRequestDTO> request = ResolveVaultBuffer(vault, in _requestHandle);
            NativeArray<RespawnStateDTO> state = ResolveVaultBuffer(vault, in _stateHandle);
            NativeArray<RespawnFadeDTO> fade = ResolveVaultBuffer(vault, in _fadeHandle);
            if (!HasRequiredLength(request, 1) ||
                !HasRequiredLength(state, 1) ||
                !HasRequiredLength(fade, 1))
            {
                return false;
            }

            return (request[0].Flags & ShinobuRespawnFlags.PendingRequest) != 0u ||
                   (state[0].Flags & ShinobuRespawnFlags.RespawnActive) != 0u ||
                   fade[0].DeathFadeIntensity > 0.0001f;
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            if (vault == null || _jobBuffersLocked)
                return false;

            if (!vault.TryAcquireMutationGuard(JobMutationGuardMask))
                return false;

            _jobBuffersLocked = true;
            return true;
        }

        private void UnlockJobBuffers()
        {
            if (!_jobBuffersLocked)
                return;

            IDataVault vault = _dataVault;
            if (vault != null)
                vault.ReleaseMutationGuard(JobMutationGuardMask);
            _jobBuffersLocked = false;
        }

#if UNITY_EDITOR
        private bool TryLoadMedicalBayCsv()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsVaultDescriptorCreated(in _medicalBayHandle))
            {
                return false;
            }

            string medicalBayPath = File.Exists(_medicalBayCsvPath) ? _medicalBayCsvPath : _legacyMedicalBayCsvPath;
            if (!File.Exists(medicalBayPath))
                return true;

            if (Interlocked.CompareExchange(ref s_csvImportScratchBusy, 1, 0) != 0)
                return false;

            try
            {
                int bytesRead = ReadCsvBytesCold(medicalBayPath, s_csvImportScratch, ShinobuRespawnConstants.CsvScratchBytes);
                if (bytesRead <= 0)
                    return false;

                int parsed = ParseMedicalBayCsv(
                    new ReadOnlySpan<byte>(s_csvImportScratch, 0, bytesRead),
                    s_medicalBayImportScratch);
                return parsed > 0 && TryCommitMedicalBayCsv(vault, parsed);
            }
            finally
            {
                Volatile.Write(ref s_csvImportScratchBusy, 0);
            }
        }

        private static int ParseMedicalBayCsv(ReadOnlySpan<byte> bytes, NativeArray<MedicalBayDTO> bays)
        {
            if (!bays.IsCreated || bays.Length <= 0)
                return 0;

            return ParseMedicalBayCsv(
                bytes,
                new Span<MedicalBayDTO>(NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(bays), bays.Length));
        }

        private static int ParseMedicalBayCsv(ReadOnlySpan<byte> bytes, Span<MedicalBayDTO> bays)
        {
            int cursor = 0;
            int written = 0;
            while (cursor < bytes.Length && written < bays.Length)
            {
                SkipLineNoise(bytes, ref cursor);
                if (cursor >= bytes.Length)
                    break;

                ReadOnlySpan<byte> hashToken = ReadCsvToken(bytes, ref cursor);
                if (cursor < bytes.Length && bytes[cursor] == (byte)',')
                    cursor++;
                if (IsMedicalBayHeaderToken(hashToken))
                {
                    SkipCsvLine(bytes, ref cursor);
                    continue;
                }

                uint baseHash = TryParseHashToken(hashToken, out uint parsedHash) ? parsedHash : HashToken(hashToken);
                if (!ReadDoubleToken(bytes, ref cursor, out double x) ||
                    !ReadDoubleToken(bytes, ref cursor, out double y) ||
                    !ReadDoubleToken(bytes, ref cursor, out double z))
                {
                    SkipCsvLine(bytes, ref cursor);
                    continue;
                }

                byte active = 1;
                byte powered = 1;
                byte priority = 0;
                if (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                {
                    active = ReadBoolToken(bytes, ref cursor);
                }

                if (cursor < bytes.Length && bytes[cursor] == (byte)',')
                {
                    cursor++;
                    powered = ReadBoolToken(bytes, ref cursor);
                }

                if (cursor < bytes.Length && bytes[cursor] == (byte)',')
                {
                    cursor++;
                    priority = ReadByteToken(bytes, ref cursor);
                }

                SkipCsvLine(bytes, ref cursor);
                double3 aup = new double3(x, y, z);
                if (baseHash == 0u || !math.all(math.isfinite(aup)))
                    continue;

                MedicalBayDTO bay = default;
                bay.BayAUP = aup;
                bay.AssociatedBaseHash = baseHash;
                bay.Flags = 0u;
                if (active != 0)
                    bay.Flags |= ShinobuRespawnFlags.MedicalBayActive;
                if (powered != 0)
                    bay.Flags |= ShinobuRespawnFlags.MedicalBayPowered;
                bay.Flags |= ((uint)priority << ShinobuRespawnConstants.MedicalBayPriorityShift) &
                             ShinobuRespawnConstants.MedicalBayPriorityMask;
                bays[written++] = bay;
            }

            return written;
        }

        private bool TryCommitMedicalBayCsv(IDataVault vault, int parsed)
        {
            if (vault == null || parsed <= 0)
                return false;

            NativeArray<MedicalBayDTO> bays = ResolveVaultBuffer(vault, in _medicalBayHandle);
            if (!HasRequiredLength(bays, 1))
                return false;

            int copyCount = math.min(parsed, bays.Length);
            if (copyCount <= 0)
                return false;

            int rowSize = UnsafeUtility.SizeOf<MedicalBayDTO>();
            int copyBytes = copyCount * rowSize;
            int clearBytes = (bays.Length - copyCount) * rowSize;
            if (!vault.TryAcquireMutationGuard(MedicalBayCsvMutationGuardMask))
            {
                return false;
            }

            try
            {
                fixed (MedicalBayDTO* source = s_medicalBayImportScratch)
                {
                    byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(bays);
                    UnsafeUtility.MemCpy(
                        destination,
                        source,
                        copyBytes);
                    if (clearBytes > 0)
                        UnsafeUtility.MemClear(destination + copyBytes, clearBytes);
                }

                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(MedicalBayCsvMutationGuardMask);
            }
        }
#endif

        private void ConsumeInventoryPenaltyResultSignals()
        {
            int snapshotGeneration = SignalBus<InventoryRespawnPenaltyResultSignal>.SnapshotGeneration;
            if (snapshotGeneration == _lastInventoryPenaltyResultSnapshotGeneration)
                return;

            _lastInventoryPenaltyResultSnapshotGeneration = snapshotGeneration;
            ReadOnlySpan<InventoryRespawnPenaltyResultSignal> signals = SignalBus<InventoryRespawnPenaltyResultSignal>.GetFrameSnapshot();
            if (signals.Length == 0)
                return;

            for (int i = 0; i < signals.Length; i++)
            {
                InventoryRespawnPenaltyResultSignal signal = signals[i];
                if (signal.Sequence == 0u ||
                    signal.Sequence != _lastRequestSequence ||
                    (_lastRequestPlayerHash != 0u &&
                     signal.InventoryHash != 0u &&
                     signal.InventoryHash != _lastRequestPlayerHash))
                {
                    continue;
                }

                TryWriteDroppedItemTelemetry(signal.DroppedCount);
            }
        }

        private bool TryWriteDroppedItemTelemetry(uint droppedCount)
        {
            IDataVault vault = _dataVault;
            if (!HasHotVaultState(vault) || _jobScheduled)
                return false;

            NativeArray<RespawnTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _telemetryHandle);
            NativeArray<RespawnTelemetryCursor64> cursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            if (!HasRequiredLength(telemetry, ShinobuRespawnConstants.TelemetryFrameCount) ||
                !HasRequiredLength(cursor, 1))
            {
                return false;
            }

            uint encodedCount = (math.min(droppedCount, 255u)) << ShinobuRespawnConstants.TelemetryDroppedItemShift;
            if (!vault.TryAcquireMutationGuard(TelemetryMutationGuardMask))
                return false;

            try
            {
                int index = cursor[0].Cursor - 1;
                if (index < 0)
                    index += telemetry.Length;

                if ((uint)index >= (uint)telemetry.Length)
                    return false;

                RespawnTelemetryEntry entry = telemetry[index];
                entry.Flags = (entry.Flags & ~(ShinobuRespawnConstants.TelemetryDroppedItemMask | ShinobuRespawnFlags.PenaltyApplied)) | encodedCount;
                if (droppedCount > 0u)
                    entry.Flags |= ShinobuRespawnFlags.PenaltyApplied;
                telemetry[index] = entry;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(TelemetryMutationGuardMask);
            }
        }

#if UNITY_EDITOR
        private bool TryLoadPenaltyCsv()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsVaultDescriptorCreated(in _penaltyRulesHandle) ||
                !IsVaultDescriptorCreated(in _penaltyRuleCountHandle))
                return false;

            if (!File.Exists(_csvPath))
            {
                TryCommitPenaltyRuleCount(vault, 0);
                return false;
            }

            if (Interlocked.CompareExchange(ref s_csvImportScratchBusy, 1, 0) != 0)
                return false;

            try
            {
                int bytesRead = ReadCsvBytesCold(_csvPath, s_csvImportScratch, ShinobuRespawnConstants.CsvScratchBytes);
                int parsed = bytesRead > 0
                    ? ParsePenaltyCsv(new ReadOnlySpan<byte>(s_csvImportScratch, 0, bytesRead), s_penaltyRuleImportScratch)
                    : 0;
                if (parsed <= 0)
                {
                    TryCommitPenaltyRuleCount(vault, 0);
                    return false;
                }

                return TryCommitPenaltyRuleCount(vault, 0) &&
                       TryCommitPenaltyRules(vault, parsed) &&
                       TryCommitPenaltyRuleCount(vault, parsed);
            }
            finally
            {
                Volatile.Write(ref s_csvImportScratchBusy, 0);
            }
        }

        private static int ParsePenaltyCsv(ReadOnlySpan<byte> bytes, NativeArray<InventoryDeathPenaltyRuleDTO> rules)
        {
            if (!rules.IsCreated || rules.Length <= 0)
                return 0;

            return ParsePenaltyCsv(
                bytes,
                new Span<InventoryDeathPenaltyRuleDTO>(NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rules), rules.Length));
        }

        private static int ParsePenaltyCsv(ReadOnlySpan<byte> bytes, Span<InventoryDeathPenaltyRuleDTO> rules)
        {
            int cursor = 0;
            int written = 0;
            while (cursor < bytes.Length && written < rules.Length)
            {
                SkipLineNoise(bytes, ref cursor);
                if (cursor >= bytes.Length)
                    break;

                int nameStart = cursor;
                while (cursor < bytes.Length && bytes[cursor] != (byte)',' && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                    cursor++;

                ReadOnlySpan<byte> item = Trim(bytes.Slice(nameStart, cursor - nameStart));
                uint itemHash = TryParseHashToken(item, out uint parsedHash) ? parsedHash : HashToken(item);
                byte drop = 0;
                byte retain = 0;
                if (cursor < bytes.Length && bytes[cursor] == (byte)',')
                {
                    cursor++;
                    drop = ReadBoolToken(bytes, ref cursor);
                }

                if (cursor < bytes.Length && bytes[cursor] == (byte)',')
                {
                    cursor++;
                    retain = ReadBoolToken(bytes, ref cursor);
                }

                while (cursor < bytes.Length && bytes[cursor] != (byte)'\n')
                    cursor++;

                if (cursor < bytes.Length)
                    cursor++;

                if (itemHash == 0u || IsHeaderToken(item))
                    continue;

                InventoryDeathPenaltyRuleDTO rule = default;
                rule.ItemHash = itemHash;
                rule.DropOnDeath = drop;
                rule.RetainIfEquipped = retain;
                rule.Flags = drop != 0 ? ShinobuRespawnFlags.PenaltyApplied : 0u;
                rules[written++] = rule;
            }

            return written;
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

        private bool TryCommitPenaltyRules(IDataVault vault, int parsed)
        {
            if (vault == null || parsed <= 0)
                return false;

            NativeArray<InventoryDeathPenaltyRuleDTO> rules = ResolveVaultBuffer(vault, in _penaltyRulesHandle);
            if (!HasRequiredLength(rules, 1))
                return false;

            int copyCount = math.min(parsed, rules.Length);
            if (copyCount <= 0)
                return false;

            int rowSize = UnsafeUtility.SizeOf<InventoryDeathPenaltyRuleDTO>();
            int copyBytes = copyCount * rowSize;
            int clearBytes = (rules.Length - copyCount) * rowSize;
            if (!vault.TryAcquireMutationGuard(PenaltyRuleCsvMutationGuardMask))
            {
                return false;
            }

            try
            {
                fixed (InventoryDeathPenaltyRuleDTO* source = s_penaltyRuleImportScratch)
                {
                    byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rules);
                    UnsafeUtility.MemCpy(
                        destination,
                        source,
                        copyBytes);
                    if (clearBytes > 0)
                        UnsafeUtility.MemClear(destination + copyBytes, clearBytes);
                }

                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(PenaltyRuleCsvMutationGuardMask);
            }
        }

        private bool TryCommitPenaltyRuleCount(IDataVault vault, int parsed)
        {
            if (vault == null)
                return false;

            NativeArray<int> count = ResolveVaultBuffer(vault, in _penaltyRuleCountHandle);
            if (!HasRequiredLength(count, 1))
                return false;

            int safeCount = math.max(0, parsed);
            if (!vault.TryAcquireMutationGuard(PenaltyRuleCountCsvMutationGuardMask))
                return false;

            try
            {
                count[0] = safeCount;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(PenaltyRuleCountCsvMutationGuardMask);
            }
        }
#endif

        private void TryDumpFaultedTelemetry()
        {
            if (_dumpedFault)
                return;

            if (_jobScheduled)
                return;

            IDataVault vault = _dataVault;
            if (!HasHotVaultState(vault))
                return;

            NativeArray<RespawnTelemetryCursor64> cursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            const uint DumpFlags = ShinobuRespawnFlags.NanDetected | ShinobuRespawnFlags.InvalidTargetAup;
            if (!HasRequiredLength(cursor, 1) || (cursor[0].Flags & DumpFlags) == 0u)
                return;

            bool dumpedPrimary = TryDumpTelemetry(_dumpPath, cursor[0].Flags);
            bool dumpedLegacy = TryDumpTelemetry(_legacyDumpPath, cursor[0].Flags);
            _dumpedFault = dumpedPrimary || dumpedLegacy;
        }

        private bool TryDumpTelemetry(string path, uint reasonFlags)
        {
            if (_jobScheduled)
                return false;

            IDataVault vault = _dataVault;
            if (!HasHotVaultState(vault))
                return false;

            NativeArray<RespawnTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _telemetryHandle);
            NativeArray<RespawnTelemetryCursor64> cursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            if (!HasRequiredLength(telemetry, ShinobuRespawnConstants.TelemetryFrameCount) ||
                !HasRequiredLength(cursor, 1))
            {
                return false;
            }

            NativeArray<byte> payload = default;
            try
            {
                const int HeaderBytes = 24;
                int stride = ShinobuRespawnConstants.RespawnTelemetryEntrySizeBytes;
                int totalBytes = HeaderBytes + ShinobuRespawnConstants.TelemetryFrameCount * stride;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(ShinobuRespawnReconciliationRuntime),
                    "shinobuRespawnReconciliationDumpPayload");
                byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);

                Span<byte> header = new Span<byte>(payloadPtr, HeaderBytes);
                BinaryPrimitives.WriteUInt64LittleEndian(header.Slice(0, 8), DumpMagic);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(8, 4), DumpVersion);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(12, 4), ShinobuRespawnConstants.TelemetryFrameCount);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(16, 4), cursor[0].Cursor);
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(20, 4), reasonFlags);

                int offset = HeaderBytes;
                for (int i = 0; i < ShinobuRespawnConstants.TelemetryFrameCount; i++)
                {
                    RespawnTelemetryEntry entry = telemetry[i];
                    UnsafeUtility.MemCpy(payloadPtr + offset, &entry, stride);
                    offset += stride;
                }

                return NativeFaultDumpWriter.TryWriteAll(path, payload, totalBytes);
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
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(ShinobuRespawnReconciliationRuntime),
                    "shinobuRespawnReconciliationDumpPayload");
            }
        }

        public static bool TryReadEditorState(out RespawnFadeDTO fade, out RespawnTuningDTO tuning)
        {
            fade = default;
            tuning = default;
            ShinobuRespawnReconciliationRuntime runtime = s_active;
            if (runtime == null)
                return false;

            IDataVault vault = runtime._dataVault;
            if (!runtime.HasHotVaultState(vault) || runtime._jobScheduled)
                return false;

            NativeArray<RespawnFadeDTO> fadeArray = ResolveVaultBuffer(vault, in runtime._fadeHandle);
            NativeArray<RespawnTuningDTO> tuningArray = ResolveVaultBuffer(vault, in runtime._tuningHandle);
            if (!HasRequiredLength(fadeArray, 1) || !HasRequiredLength(tuningArray, 1))
                return false;

            fade = fadeArray[0];
            tuning = tuningArray[0];
            return true;
        }

        public static bool TryReadEditorTelemetry(out RespawnTelemetryEntry latest, out int cursorIndex)
        {
            latest = default;
            cursorIndex = 0;
            ShinobuRespawnReconciliationRuntime runtime = s_active;
            if (runtime == null)
                return false;

            IDataVault vault = runtime._dataVault;
            if (!runtime.HasHotVaultState(vault) || runtime._jobScheduled)
                return false;

            NativeArray<RespawnTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in runtime._telemetryHandle);
            NativeArray<RespawnTelemetryCursor64> cursor = ResolveVaultBuffer(vault, in runtime._telemetryCursorHandle);
            if (!HasRequiredLength(telemetry, ShinobuRespawnConstants.TelemetryFrameCount) ||
                !HasRequiredLength(cursor, 1))
            {
                return false;
            }

            cursorIndex = cursor[0].Cursor;
            int latestIndex = cursorIndex - 1;
            if (latestIndex < 0)
                latestIndex += telemetry.Length;

            if ((uint)latestIndex >= (uint)telemetry.Length)
                return false;

            latest = telemetry[latestIndex];
            return latest.Frame != 0u || latest.Flags != 0u;
        }

        public static bool TryWriteEditorTuning(in RespawnTuningDTO tuning)
        {
            ShinobuRespawnReconciliationRuntime runtime = s_active;
            if (runtime == null || !runtime.EnsureVaultState() || !runtime.FinalizeCompletedEditorFenceForMutation())
                return false;

            IDataVault vault = runtime._dataVault;
            NativeArray<RespawnTuningDTO> tuningArray = ResolveVaultBuffer(vault, in runtime._tuningHandle);
            if (!HasRequiredLength(tuningArray, 1))
                return false;

            RespawnTuningDTO sanitized = SanitizeTuning(tuning);
            sanitized.Flags |= ShinobuRespawnFlags.ManualTuning;
            if (!vault.TryAcquireMutationGuard(TuningMutationGuardMask))
                return false;

            try
            {
                tuningArray[0] = sanitized;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(TuningMutationGuardMask);
            }
        }

#if UNITY_EDITOR
        public static bool TryReloadPenaltyCsvFromEditor()
        {
            ShinobuRespawnReconciliationRuntime runtime = s_active;
            if (runtime == null ||
                !runtime.EnsureVaultState() ||
                !runtime.FinalizeCompletedEditorFenceForMutation())
                return false;

            bool loaded = runtime.TryLoadPenaltyCsv();
            runtime._penaltyCsvInitialized = loaded;
            return loaded;
        }

        public static bool TryReloadMedicalBayCsvFromEditor()
        {
            ShinobuRespawnReconciliationRuntime runtime = s_active;
            if (runtime == null ||
                !runtime.EnsureVaultState() ||
                !runtime.FinalizeCompletedEditorFenceForMutation())
                return false;

            bool loaded = runtime.TryLoadMedicalBayCsv();
            runtime._medicalBayCsvInitialized = loaded;
            return loaded;
        }
#endif

        public static bool TryDumpBlackBoxForEditor()
        {
            ShinobuRespawnReconciliationRuntime runtime = s_active;
            return runtime != null &&
                   runtime.EnsureVaultState() &&
                   runtime.FinalizeCompletedEditorFenceForMutation() &&
                   runtime.TryDumpTelemetry(runtime._dumpPath, 0u) &&
                   runtime.TryDumpTelemetry(runtime._legacyDumpPath, 0u);
        }

        public static bool TryInjectMockLethalDamageFromEditor()
        {
            ShinobuRespawnReconciliationRuntime runtime = s_active;
            if (runtime == null ||
                !runtime.EnsureVaultState() ||
                !runtime.FinalizeCompletedEditorFenceForMutation())
                return false;

            IDataVault vault = runtime._dataVault;
            NativeArray<MedicalBayDTO> bays = ResolveVaultBuffer(vault, in runtime._medicalBayHandle);
            NativeArray<RespawnTuningDTO> tuning = ResolveVaultBuffer(vault, in runtime._tuningHandle);
            if (!HasRequiredLength(bays, ShinobuRespawnConstants.MockMedicalBayCapacity) ||
                !HasRequiredLength(tuning, 1))
            {
                return false;
            }

            ConfigureSignalLanes();
            uint sequence = ++runtime._mockLethalSequence;
            if (sequence == 0u)
                sequence = ++runtime._mockLethalSequence;

            GenerateMockLethalDamageJob job = default;
            job.MedicalBays = bays;
            job.RespawnSignals = SignalBus<PlayerRespawnSignal>.ParallelWriter;
            job.RespawnSignalsBudget = SignalBus<PlayerRespawnSignal>.ParallelWriterBudget;
            job.DeathAUP = SanitizeAup(tuning[0].FallbackLifepodAUP) + new double3(37d, -11d, -29d);
            job.FallbackLifepodAUP = tuning[0].FallbackLifepodAUP;
            job.Frame = TimeSliceScheduler.CurrentFrameId;
            job.Sequence = sequence;
            job.PlayerHash = DefaultPlayerHash;
            job.DamageHash = 0x4D4F434Bu; // MOCK
            job.Intensity01 = 1f;
            job.Execute();

            PlayerFatalPressureSignal fatal = default;
            fatal.SourceId = ShinobuRespawnConstants.SourceHash;
            fatal.Frame = job.Frame;
            fatal.Intensity01 = math.saturate(math.isfinite(job.Intensity01) ? job.Intensity01 : 1f);
            fatal.Flags = 1;
            SignalBus<PlayerFatalPressureSignal>.TryPushTracked(in fatal, ref s_x001ShinobuRespawnReconciliationRuntimeSignalPushDropCount);
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !IsVaultDescriptorCreated(in _medicalBayHandle))
                return;

            NativeArray<MedicalBayDTO> bays = ResolveVaultBuffer(vault, in _medicalBayHandle);
            if (!HasRequiredLength(bays, 1))
                return;

            TryDrawLastRespawnRouteGizmo(vault);
            for (int i = 0; i < bays.Length; i++)
            {
                MedicalBayDTO bay = bays[i];
                if (bay.AssociatedBaseHash == 0u || !math.all(math.isfinite(bay.BayAUP)))
                    continue;

                Color color = ValidateMedicalBay(in bay) ? Color.green : Color.red;
                Handles.color = color;
                Gizmos.color = color;
                Vector3 center = HectonFloatingOrigin.ToRuntimePosition(bay.BayAUP);
                float radius = 1.5f;
                Handles.DrawWireDisc(center, Vector3.up, radius);
                Handles.DrawWireDisc(center + Vector3.up * 2f, Vector3.up, radius);
                Vector3 xOffset = default;
                xOffset.x = radius;
                Vector3 zOffset = default;
                zOffset.z = radius;
                Vector3 yOffset = default;
                yOffset.y = 2f;
                Handles.DrawLine(center + xOffset, center + xOffset + yOffset);
                Handles.DrawLine(center - xOffset, center - xOffset + yOffset);
                Handles.DrawLine(center + zOffset, center + zOffset + yOffset);
                Handles.DrawLine(center - zOffset, center - zOffset + yOffset);
            }
        }

        private void TryDrawLastRespawnRouteGizmo(IDataVault vault)
        {
            NativeArray<RespawnRequestDTO> requestArray = ResolveVaultBuffer(vault, in _requestHandle);
            NativeArray<RespawnStateDTO> stateArray = ResolveVaultBuffer(vault, in _stateHandle);
            if (!HasRequiredLength(requestArray, 1) || !HasRequiredLength(stateArray, 1))
                return;

            RespawnRequestDTO request = requestArray[0];
            RespawnStateDTO state = stateArray[0];
            if (request.Sequence == 0u ||
                !math.all(math.isfinite(request.DeathAUP)) ||
                !math.all(math.isfinite(state.TargetAUP)))
            {
                return;
            }

            Handles.color = Color.yellow;
            Handles.DrawLine(
                HectonFloatingOrigin.ToRuntimePosition(request.DeathAUP),
                HectonFloatingOrigin.ToRuntimePosition(state.TargetAUP));
        }
#endif

        private bool TryFinalizeActiveJobNoWait()
        {
            if (!_jobScheduled)
                return true;

            if (!_activeHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _activeHandle))
                return false;

            bool suppressTransform = _loadOperationInProgress || _loadCompletionRespawnCancelPending;
            try
            {
                if (suppressTransform)
                {
                    _respawnCommitSuppressedForLoad = true;
                }
                else
                {
                    TryTransformCommittedRespawnSignal();
                }
            }
            finally
            {
                _jobScheduled = false;
                UnlockJobBuffers();
            }

            if (_loadCompletionRespawnCancelPending)
                TryFlushCompletedLoadRespawnCancel(_dataVault);

            return true;
        }

        private void CompleteActiveJobForTeardown()
        {
            if (!_jobScheduled)
                return;

            bool completed = false;
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                if (!DispatcherJobFence.TryComplete(ref _activeHandle, forceComplete: true))
                    return;
                completed = true;
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }

            if (!completed)
                return;

            bool suppressTransform = _loadOperationInProgress || _loadCompletionRespawnCancelPending;
            try
            {
                if (suppressTransform)
                    _respawnCommitSuppressedForLoad = true;
                else
                    TryTransformCommittedRespawnSignal();
            }
            finally
            {
                _jobScheduled = false;
                UnlockJobBuffers();
            }

            if (_loadCompletionRespawnCancelPending)
                TryFlushCompletedLoadRespawnCancel(_dataVault);
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private bool TryTransformCommittedRespawnSignal()
        {
            if (_loadOperationInProgress || _loadCompletionRespawnCancelPending)
                return false;

            IDataVault vault = _dataVault;
            if (!HasHotVaultState(vault))
                return false;

            NativeArray<RespawnRequestDTO> requestArray = ResolveVaultBuffer(vault, in _requestHandle);
            NativeArray<RespawnStateDTO> stateArray = ResolveVaultBuffer(vault, in _stateHandle);
            if (!HasRequiredLength(requestArray, 1) || !HasRequiredLength(stateArray, 1))
                return false;

            RespawnRequestDTO request = requestArray[0];
            RespawnStateDTO state = stateArray[0];
            if (request.Sequence == 0u ||
                request.Sequence == _lastCommittedTransformSequence ||
                (request.Flags & ShinobuRespawnFlags.Committed) == 0u ||
                (state.Flags & ShinobuRespawnFlags.Committed) == 0u ||
                !math.all(math.isfinite(request.DeathAUP)) ||
                !math.all(math.isfinite(state.TargetAUP)))
            {
                return false;
            }

            uint routeFlags = request.Flags | state.Flags;
            RespawnSignalResolvedTargetTransformer transformer = default;
            transformer.Sequence = request.Sequence;
            transformer.RespawnAUP = state.TargetAUP;
            transformer.MedicalBayHashID = state.MedicalBayHashID;
            transformer.Flags = PlayerRespawnSignalFlags.Requested |
                                PlayerRespawnSignalFlags.Committed |
                                PlayerRespawnSignalFlags.SuspendCollision |
                                TranslateSignalFlags(routeFlags);
            transformer.SuspendCollisionFrames = 1;
            SignalBus<PlayerRespawnSignal>.TransformSnapshot(transformer);
            _lastCommittedTransformSequence = request.Sequence;
            return true;
        }

        private bool FinalizeCompletedEditorFenceForMutation()
        {
            TryFinalizeActiveJobNoWait();
            return !_jobScheduled;
        }

        private void ClearRespawnDearLieVisualIfNeeded()
        {
            if (!_respawnDearLieVisualActive)
                return;

            Vector4 payload = default;
            HectonShaderGlobalDataVaultBridge.PublishRespawnDearLie(_dataVault, payload);
            _respawnDearLieVisualActive = false;
        }

        private void RegisterDispatcherPhases()
        {
            if (!_registeredPreSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_preSimulationPhase))
                _registeredPreSimulation = true;
            if (!_registeredSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_simulationPhase))
                _registeredSimulation = true;
            if (!_registeredPostSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase))
                _registeredPostSimulation = true;
            if (!_registeredVisualSync && GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncPhase))
                _registeredVisualSync = true;
        }

        private void UnregisterDispatcherPhases()
        {
            if (_registeredPreSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_preSimulationPhase);
                _registeredPreSimulation = false;
            }

            if (_registeredSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_simulationPhase);
                _registeredSimulation = false;
            }

            if (_registeredPostSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
                _registeredPostSimulation = false;
            }

            if (_registeredVisualSync)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_visualSyncPhase);
                _registeredVisualSync = false;
            }
        }

        private void TryRegisterHotSwap()
        {
            if (_registeredHotSwap)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private IDataVault BindVaultCold()
        {
            if (_dataVault != null)
                return _dataVault;

            _dataVault = GlobalRegistry.DataVault;
            return _dataVault;
        }

        private void ClearCachedHandles()
        {
            _stateHandle = default;
            _requestHandle = default;
            _medicalBayHandle = default;
            _fadeHandle = default;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _tuningHandle = default;
            _penaltyRulesHandle = default;
            _penaltyRuleCountHandle = default;
            _vitalsHandle = default;
            _decompressionHandle = default;
            _tissueHandle = default;
            _scalarHandle = default;
            _metabolismHandle = default;
            _gasStateHandle = default;
            _playerKinematicHandle = default;
            _defaultsInitialized = false;
            _medicalBayCsvInitialized = false;
            _penaltyCsvInitialized = false;
            _respawnDearLieVisualActive = false;
            _lastRequestSequence = 0u;
            _lastRequestPlayerHash = 0u;
            _lastCommittedTransformSequence = 0u;
            _activeLoadOperationId = 0u;
            _completedLoadPendingRespawnCancel = default;
            _loadOperationInProgress = false;
            _loadCompletionRespawnCancelPending = false;
            _respawnCommitSuppressedForLoad = false;
        }

        private static void ConfigureSignalLanes()
        {
            SignalBus<PlayerFatalPressureSignal>.EnsureInitialized();
            SignalBus<PlayerRespawnSignal>.Configure(
                PlayerRespawnSignal.ExpectedCapacity,
                maxFrameSignals: PlayerRespawnSignal.MaxFrameSignals,
                lowTierFrameSignals: PlayerRespawnSignal.LowTierFrameSignals,
                laneHash: PlayerRespawnSignal.LaneHash);
            SignalBus<PlayerRespawnSignal>.EnsureInitialized();
            SignalBus<InventoryCommandSignal>.EnsureInitialized();
            SignalBus<InventoryRespawnDeathAupSignal>.Configure(
                InventoryRespawnDeathAupSignal.ExpectedCapacity,
                maxFrameSignals: InventoryRespawnDeathAupSignal.MaxFrameSignals,
                lowTierFrameSignals: InventoryRespawnDeathAupSignal.LowTierFrameSignals,
                laneHash: InventoryRespawnDeathAupSignal.LaneHash);
            SignalBus<InventoryRespawnDeathAupSignal>.EnsureInitialized();
            SignalBus<InventoryRespawnPenaltyResultSignal>.Configure(
                InventoryRespawnPenaltyResultSignal.ExpectedCapacity,
                maxFrameSignals: InventoryRespawnPenaltyResultSignal.MaxFrameSignals,
                lowTierFrameSignals: InventoryRespawnPenaltyResultSignal.LowTierFrameSignals,
                laneHash: InventoryRespawnPenaltyResultSignal.LaneHash);
            SignalBus<InventoryRespawnPenaltyResultSignal>.EnsureInitialized();
        }

        private static RespawnTuningDTO CreateDefaultTuning()
        {
            RespawnTuningDTO tuning = default;
            tuning.FallbackLifepodAUP = DefaultFallbackAup();
            tuning.HighQualityFadeRate = 0.5f;
            tuning.LowQualityFadeRate = 2f;
            tuning.PenaltyMultiplier = 1f;
            tuning.ValidationClearanceMeters = 1.5f;
            tuning.RespawnInvulnerabilitySeconds = 1.5f;
            tuning.MedicalBaySearchRadiusMeters = 5000f;
            tuning.Flags = ShinobuRespawnFlags.MockMedicalBay;
            tuning.Version = 1u;
            return tuning;
        }

        private static bool ValidateMedicalBay(in MedicalBayDTO bay)
        {
            if (bay.AssociatedBaseHash == 0u)
                return false;

            if (!math.all(math.isfinite(bay.BayAUP)))
                return false;

            const uint requiredFlags = ShinobuRespawnFlags.MedicalBayActive | ShinobuRespawnFlags.MedicalBayPowered;
            return (bay.Flags & requiredFlags) == requiredFlags;
        }

        private static MedicalBayDTO CreateMockRespawnPoint(int index, int count, double3 fallbackAup)
        {
            float ring = 9f + ((index & 3) * 2.25f);
            float angle = ((index & 7) * 0.78539816339f) + 0.39269908169f;
            MathLodApproximation.ApproxSinCosBhaskara(angle, out float angleSin, out float angleCos);
            double3 offset = default;
            offset.x = angleCos * ring;
            offset.y = 1.5f + ((index & 1) * 0.5f);
            offset.z = angleSin * ring;
            MedicalBayDTO bay = default;
            bay.BayAUP = SanitizeAup(fallbackAup + offset);
            bay.AssociatedBaseHash = HashMockMedicalBay(index);
            bay.Flags = ShinobuRespawnFlags.MockMedicalBay |
                        ShinobuRespawnFlags.MedicalBayActive |
                        ShinobuRespawnFlags.MedicalBayPowered;
            bay.Flags |= ((uint)(count - index - 1) << ShinobuRespawnConstants.MedicalBayPriorityShift) &
                         ShinobuRespawnConstants.MedicalBayPriorityMask;
            return bay;
        }

        private static uint HashMockMedicalBay(int index)
        {
            unchecked
            {
                const uint Salt = 0x4D454442u;
                uint hash = 2166136261u;
                hash = (hash ^ (uint)index) * 16777619u;
                hash = (hash ^ Salt) * 16777619u;
                return hash == 0u ? Salt : hash;
            }
        }

        private static float ResolveQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static float ResolveSimulationDelta(in DispatcherTimingDTO timing)
        {
            float dt = timing.FixedDelta > 0f ? timing.FixedDelta : timing.FrameDelta;
            return math.clamp(math.isfinite(dt) ? dt : 1f / 60f, 0.0001f, 0.1f);
        }

        private static double3 SanitizeAup(double3 aup)
        {
            return math.all(math.isfinite(aup)) ? aup : DefaultFallbackAup();
        }

        private static double3 DefaultFallbackAup()
        {
            double3 fallback = default;
            fallback.y = -18d;
            return fallback;
        }

        private static RespawnTuningDTO SanitizeTuning(RespawnTuningDTO tuning)
        {
            tuning.FallbackLifepodAUP = SanitizeAup(tuning.FallbackLifepodAUP);
            tuning.HighQualityFadeRate = math.clamp(math.isfinite(tuning.HighQualityFadeRate) ? tuning.HighQualityFadeRate : 0.5f, 0.0001f, 16f);
            tuning.LowQualityFadeRate = math.clamp(math.isfinite(tuning.LowQualityFadeRate) ? tuning.LowQualityFadeRate : 2f, 0.0001f, 16f);
            tuning.PenaltyMultiplier = math.saturate(math.isfinite(tuning.PenaltyMultiplier) ? tuning.PenaltyMultiplier : 1f);
            tuning.ValidationClearanceMeters = math.clamp(math.isfinite(tuning.ValidationClearanceMeters) ? tuning.ValidationClearanceMeters : 1.5f, 0.25f, 16f);
            tuning.RespawnInvulnerabilitySeconds = math.clamp(math.isfinite(tuning.RespawnInvulnerabilitySeconds) ? tuning.RespawnInvulnerabilitySeconds : 1.5f, 0f, 60f);
            tuning.MedicalBaySearchRadiusMeters = math.clamp(math.isfinite(tuning.MedicalBaySearchRadiusMeters) ? tuning.MedicalBaySearchRadiusMeters : 5000f, 1f, 50000f);
            return tuning;
        }

        private static float3 AupDeltaToFloat3(double3 delta)
        {
            if (!math.all(math.isfinite(delta)))
                return float3.zero;

            double clamp = SafeAupClampMeters();
            float3 result = default;
            result.x = (float)math.clamp(delta.x, -clamp, clamp);
            result.y = (float)math.clamp(delta.y, -clamp, clamp);
            result.z = (float)math.clamp(delta.z, -clamp, clamp);
            return result;
        }

        private static double SafeAupClampMeters()
        {
            return math.max(HectonPhysicsContract.AupMaxDistanceReturnMeters, 0.0001d);
        }

        private static uint TranslateSignalFlags(uint flags)
        {
            uint translated = 0u;
            if ((flags & ShinobuRespawnFlags.MockMedicalBay) != 0u) translated |= PlayerRespawnSignalFlags.MockMedicalBay;
            if ((flags & ShinobuRespawnFlags.FallbackLifepod) != 0u) translated |= PlayerRespawnSignalFlags.FallbackLifepod;
            if ((flags & ShinobuRespawnFlags.InvalidTargetAup) != 0u) translated |= PlayerRespawnSignalFlags.InvalidTargetAup;
            if ((flags & ShinobuRespawnFlags.PenaltyApplied) != 0u) translated |= PlayerRespawnSignalFlags.PenaltyApplied;
            if ((flags & ShinobuRespawnFlags.NanDetected) != 0u) translated |= PlayerRespawnSignalFlags.InvalidDeathAup;
            return translated;
        }

        private static bool IsAdmissibleRequestSignal(in PlayerRespawnSignal signal)
        {
            return signal.Phase == PlayerRespawnSignalPhase.Request &&
                   signal.Sequence != 0u &&
                   (signal.Flags & PlayerRespawnSignalFlags.Requested) != 0u &&
                   (signal.Flags & PlayerRespawnSignalFlags.Committed) == 0u;
        }

        private struct RespawnSignalResolvedTargetTransformer : ISignalSnapshotTransformer<PlayerRespawnSignal>
        {
            public double3 RespawnAUP;
            public uint Sequence;
            public uint MedicalBayHashID;
            public uint Flags;
            public byte SuspendCollisionFrames;

            public void Transform(ref PlayerRespawnSignal signal)
            {
                if (signal.Sequence != Sequence)
                    return;

                signal.RespawnAUP = math.all(math.isfinite(RespawnAUP)) ? RespawnAUP : DefaultFallbackAup();
                signal.MedicalBayHashID = MedicalBayHashID;
                signal.Flags |= Flags;
                signal.Phase = PlayerRespawnSignalPhase.Committed;
                byte frames = signal.SuspendCollisionFrames;
                if (frames < SuspendCollisionFrames)
                    frames = SuspendCollisionFrames;
                if (frames > PlayerRespawnSignal.MaxSuspendCollisionFrames)
                    frames = PlayerRespawnSignal.MaxSuspendCollisionFrames;
                signal.SuspendCollisionFrames = frames;
            }
        }

#if UNITY_EDITOR
        private static void SkipLineNoise(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            while (cursor < bytes.Length)
            {
                byte value = bytes[cursor];
                if (value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n')
                {
                    cursor++;
                    continue;
                }

                if (value == (byte)'#')
                {
                    while (cursor < bytes.Length && bytes[cursor] != (byte)'\n')
                        cursor++;
                    continue;
                }

                break;
            }
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> bytes)
        {
            int start = 0;
            int end = bytes.Length;
            while (start < end && (bytes[start] == (byte)' ' || bytes[start] == (byte)'\t'))
                start++;
            while (end > start && (bytes[end - 1] == (byte)' ' || bytes[end - 1] == (byte)'\t'))
                end--;
            return bytes.Slice(start, end - start);
        }

        private static ReadOnlySpan<byte> ReadCsvToken(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            int start = cursor;
            while (cursor < bytes.Length &&
                   bytes[cursor] != (byte)',' &&
                   bytes[cursor] != (byte)'\n' &&
                   bytes[cursor] != (byte)'\r')
            {
                cursor++;
            }

            ReadOnlySpan<byte> token = Trim(bytes.Slice(start, cursor - start));
            return token;
        }

        private static bool ReadDoubleToken(ReadOnlySpan<byte> bytes, ref int cursor, out double value)
        {
            value = 0d;
            ReadOnlySpan<byte> token = ReadCsvToken(bytes, ref cursor);
            if (token.Length == 0)
                return false;

            int index = 0;
            double sign = 1d;
            if (token[index] == (byte)'-')
            {
                sign = -1d;
                index++;
            }
            else if (token[index] == (byte)'+')
            {
                index++;
            }

            double whole = 0d;
            bool hasDigit = false;
            while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
            {
                whole = (whole * 10d) + (token[index] - (byte)'0');
                index++;
                hasDigit = true;
            }

            double fraction = 0d;
            double divisor = 1d;
            if (index < token.Length && token[index] == (byte)'.')
            {
                index++;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    fraction = (fraction * 10d) + (token[index] - (byte)'0');
                    divisor *= 10d;
                    index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit)
                return false;

            double result = sign * (whole + (fraction / math.max(divisor, 1d)));
            if (index < token.Length && (token[index] == (byte)'e' || token[index] == (byte)'E'))
            {
                index++;
                int exponentSign = 1;
                if (index < token.Length && token[index] == (byte)'-')
                {
                    exponentSign = -1;
                    index++;
                }
                else if (index < token.Length && token[index] == (byte)'+')
                {
                    index++;
                }

                int exponent = 0;
                bool hasExponent = false;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    exponent = (exponent * 10) + (token[index] - (byte)'0');
                    index++;
                    hasExponent = true;
                }

                if (!hasExponent)
                    return false;

                result *= Pow10Signed(exponent * exponentSign);
            }

            if (index != token.Length || !math.isfinite(result))
                return false;

            if (cursor < bytes.Length && bytes[cursor] == (byte)',')
                cursor++;
            value = result;
            return true;
        }

        private static double Pow10Signed(int exponent)
        {
            int count = math.abs(exponent);
            double value = 1d;
            for (int i = 0; i < count; i++)
                value *= 10d;

            return exponent < 0 ? 1d / value : value;
        }

        private static void SkipCsvLine(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            while (cursor < bytes.Length && bytes[cursor] != (byte)'\n')
                cursor++;
            if (cursor < bytes.Length)
                cursor++;
        }

        private static byte ReadBoolToken(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            while (cursor < bytes.Length && (bytes[cursor] == (byte)' ' || bytes[cursor] == (byte)'\t'))
                cursor++;

            byte result = 0;
            if (cursor < bytes.Length)
            {
                byte value = bytes[cursor];
                result = (byte)(value == (byte)'1' || value == (byte)'t' || value == (byte)'T' || value == (byte)'y' || value == (byte)'Y' || value == (byte)'d' || value == (byte)'D' ? 1 : 0);
            }

            while (cursor < bytes.Length && bytes[cursor] != (byte)',' && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                cursor++;
            return result;
        }

        private static byte ReadByteToken(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            while (cursor < bytes.Length && (bytes[cursor] == (byte)' ' || bytes[cursor] == (byte)'\t'))
                cursor++;

            uint value = 0u;
            bool hasDigit = false;
            while (cursor < bytes.Length && bytes[cursor] >= (byte)'0' && bytes[cursor] <= (byte)'9')
            {
                value = math.min(255u, (value * 10u) + (uint)(bytes[cursor] - (byte)'0'));
                cursor++;
                hasDigit = true;
            }

            while (cursor < bytes.Length && bytes[cursor] != (byte)',' && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                cursor++;
            return hasDigit ? (byte)value : (byte)0;
        }

        private static uint HashToken(ReadOnlySpan<byte> token)
        {
            return Hecton.Localization.LocHash.ComputeUtf8AsUtf16(token);
        }

        private static bool TryParseHashToken(ReadOnlySpan<byte> token, out uint hash)
        {
            hash = 0u;
            if (token.Length == 0)
                return false;

            int cursor = 0;
            bool hex = token.Length > 2 && token[0] == (byte)'0' && (token[1] == (byte)'x' || token[1] == (byte)'X');
            if (hex)
                cursor = 2;

            if (cursor >= token.Length)
                return false;

            uint value = 0u;
            for (; cursor < token.Length; cursor++)
            {
                byte c = token[cursor];
                uint digit;
                if (c >= (byte)'0' && c <= (byte)'9')
                {
                    digit = (uint)(c - (byte)'0');
                }
                else if (hex && c >= (byte)'a' && c <= (byte)'f')
                {
                    digit = (uint)(10 + c - (byte)'a');
                }
                else if (hex && c >= (byte)'A' && c <= (byte)'F')
                {
                    digit = (uint)(10 + c - (byte)'A');
                }
                else
                {
                    return false;
                }

                value = hex ? ((value << 4) | digit) : ((value * 10u) + digit);
            }

            hash = value;
            return hash != 0u;
        }

        private static bool IsHeaderToken(ReadOnlySpan<byte> token)
        {
            return EqualsAsciiIgnoreCase(token, "item") ||
                   EqualsAsciiIgnoreCase(token, "itemhash") ||
                   EqualsAsciiIgnoreCase(token, "item_hash") ||
                   EqualsAsciiIgnoreCase(token, "persistentid") ||
                   EqualsAsciiIgnoreCase(token, "persistent_id");
        }

        private static bool IsMedicalBayHeaderToken(ReadOnlySpan<byte> token)
        {
            return EqualsAsciiIgnoreCase(token, "base") ||
                   EqualsAsciiIgnoreCase(token, "basehash") ||
                   EqualsAsciiIgnoreCase(token, "base_hash") ||
                   EqualsAsciiIgnoreCase(token, "associatedbasehash") ||
                   EqualsAsciiIgnoreCase(token, "associated_base_hash");
        }

        private static bool EqualsAsciiIgnoreCase(ReadOnlySpan<byte> token, string expected)
        {
            if (token.Length != expected.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                byte actual = token[i];
                if (actual >= (byte)'A' && actual <= (byte)'Z')
                    actual = (byte)(actual + 32);

                char expectedChar = expected[i];
                if (expectedChar >= 'A' && expectedChar <= 'Z')
                    expectedChar = (char)(expectedChar + 32);

                if (actual != (byte)expectedChar)
                    return false;
            }

            return true;
        }
#endif

        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private static string BuildProjectRootPathCold()
        {
            string dataPath = Application.dataPath;
            return string.IsNullOrEmpty(dataPath) ? "." : Path.GetFullPath(Path.Combine(dataPath, ".."));
        }

        private ref struct JobPointers
        {
            public RespawnStateDTO* State;
            public RespawnRequestDTO* Request;
            public MedicalBayDTO* MedicalBays;
            public RespawnFadeDTO* Fade;
            public RespawnTelemetryEntry* Telemetry;
            public RespawnTelemetryCursor64* TelemetryCursor;
            public RespawnTuningDTO* Tuning;
            public InventoryDeathPenaltyRuleDTO* PenaltyRules;
            public int* PenaltyRuleCount;
            public PhysiologyDTO* Vitals;
            public DecompressionStateDTO* Decompression;
            public TissueCompartmentDTO* Tissues;
            public PhysiologyScalarsDTO* Scalars;
            public MetabolicStateDTO* Metabolism;
            public GasPhysiologyStateDTO* GasState;
            public LockstepPlayerKinematicState* PlayerKinematic;
            public int MedicalBayCount;
            public int TissueCount;
            public int PenaltyCapacity;
        }

        private sealed class PreSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly ShinobuRespawnReconciliationRuntime _owner;
            public PreSimulationPhaseSystem(ShinobuRespawnReconciliationRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x53315550u; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.PreSimulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { _owner.PreSimulationTick(in timing); }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class SimulationPhaseSystem : IDispatcherSystem
        {
            private readonly ShinobuRespawnReconciliationRuntime _owner;
            public SimulationPhaseSystem(ShinobuRespawnReconciliationRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x53315553u; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.Simulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return _owner.ScheduleSimulation(in timing, in context, dependsOn); }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly ShinobuRespawnReconciliationRuntime _owner;
            public PostSimulationPhaseSystem(ShinobuRespawnReconciliationRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x5331554Fu; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.PostSimulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { _owner.PostSimulationTick(in timing); }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class VisualSyncPhaseSystem : IDispatcherSystem
        {
            private readonly ShinobuRespawnReconciliationRuntime _owner;
            public VisualSyncPhaseSystem(ShinobuRespawnReconciliationRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x53315556u; }
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
