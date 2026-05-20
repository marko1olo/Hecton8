using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts;
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
        private const SystemID OwnerSystem = SystemID.GameplayPlayer;
        private const uint SystemHash = ShinobuRespawnConstants.SourceHash;
        private const uint DefaultPlayerHash = 0x504C5952u; // PLYR
        private const ulong DumpMagic = 0x5253504E53524745ul; // RSPNSRGE
        private const uint DumpVersion = 1u;
        private const string CsvRelativePath = "respawn_penalty_rules.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_155.bin";
        private const string LegacyDumpRelativePath = "Docs/AgentLogs/Dump_RECONCILIATION_SURGEON.bin";

        private static readonly double s_ticksToMicroseconds = 1000000.0 / Stopwatch.Frequency;
        private static ShinobuRespawnReconciliationRuntime s_active;

        private VaultGenerationHandle<RespawnStateDTO> _stateHandle;
        private VaultGenerationHandle<RespawnRequestDTO> _requestHandle;
        private VaultGenerationHandle<MedicalBayRespawnPointDTO> _medicalBayHandle;
        private VaultGenerationHandle<RespawnFadeDTO> _fadeHandle;
        private VaultGenerationHandle<RespawnTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<RespawnTelemetryCursor64> _telemetryCursorHandle;
        private VaultGenerationHandle<RespawnTuningDTO> _tuningHandle;
        private VaultGenerationHandle<InventoryDeathPenaltyRuleDTO> _penaltyRulesHandle;
        private VaultGenerationHandle<int> _penaltyRuleCountHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<PhysiologyDTO> _vitalsHandle;
        private VaultGenerationHandle<DecompressionStateDTO> _decompressionHandle;
        private VaultGenerationHandle<TissueCompartmentDTO> _tissueHandle;
        private VaultGenerationHandle<PhysiologyScalarsDTO> _scalarHandle;
        private VaultGenerationHandle<MetabolicStateDTO> _metabolismHandle;
        private VaultGenerationHandle<LockstepPlayerKinematicState> _playerKinematicHandle;

        private IDataVault _dataVault;
        private JobHandle _activeHandle;
        private PreSimulationPhaseSystem _preSimulationPhase;
        private SimulationPhaseSystem _simulationPhase;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private VisualSyncPhaseSystem _visualSyncPhase;
        private string _csvPath;
        private string _dumpPath;
        private string _legacyDumpPath;
        private uint _lastRequestSequence;
        private uint _lastFrame;
        private float _lastQualityWeight = 1f;
        private float _lastScheduleMicroseconds;
        private bool _registeredHotSwap;
        private bool _registeredPreSimulation;
        private bool _registeredSimulation;
        private bool _registeredPostSimulation;
        private bool _registeredVisualSync;
        private bool _defaultsInitialized;
        private bool _jobScheduled;
        private bool _dumpedFault;
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

            GameObject host = new GameObject("SHINOBU_155_RespawnReconciliation"); // COLD ALLOC: GameObject[1] - dispatcher host for Vault-only respawn reconciliation - owner: SHINOBU_155
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
            string root = ResolveProjectRoot();
            _csvPath = Path.GetFullPath(Path.Combine(root, CsvRelativePath));
            _dumpPath = Path.GetFullPath(Path.Combine(root, DumpRelativePath));
            _legacyDumpPath = Path.GetFullPath(Path.Combine(root, LegacyDumpRelativePath));

            // COLD ALLOC: IDispatcherSystem[4] - phase adapters registered into the dispatcher graph - owner: SHINOBU_155
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
            _dataVault = ResolveVaultCold();
            if (EnsureVaultState(_dataVault))
            {
                InitializeDefaultVaultContents();
                TryLoadPenaltyCsv();
            }

            RegisterDispatcherPhases();
        }

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            _dataVault = ResolveVaultCold();
            if (EnsureVaultState(_dataVault))
                RegisterDispatcherPhases();
        }

        private void OnDisable()
        {
            CompleteActiveJobIfReady(forceComplete: true);
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

            CompleteActiveJobIfReady(forceComplete: true);
            ClearRespawnDearLieVisualIfNeeded();
            IDataVault previousVault = previousService as IDataVault;
            if (previousVault == null)
                previousVault = _dataVault;
            ReleaseOwnedVaultDescriptors(previousVault);
            _dataVault = currentService as IDataVault;
            ClearCachedHandles();
            _defaultsInitialized = false;
            _dumpedFault = false;
            if (_dataVault != null && EnsureVaultState(_dataVault))
                InitializeDefaultVaultContents();
        }

        private void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            IDataVault vault = _dataVault;
            if (!HasHotVaultState(vault))
                return;

            if (_jobScheduled)
            {
                if (!_activeHandle.IsCompleted)
                    return;

                CompleteActiveJobIfReady(forceComplete: false);
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
            }
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

                CompleteActiveJobIfReady(forceComplete: false);
            }

            if (!TryResolveJobPointers(vault, out JobPointers pointers))
                return dependsOn;

            if ((pointers.Request->Flags & ShinobuRespawnFlags.PendingRequest) == 0u &&
                (pointers.State->Flags & ShinobuRespawnFlags.RespawnActive) == 0u &&
                pointers.Fade->DeathFadeIntensity <= 0.0001f)
            {
                return dependsOn;
            }

            _lastFrame = context.Frame;
            _lastQualityWeight = ResolveQualityWeight();
            long start = Stopwatch.GetTimestamp();

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
            resetJob.PlayerKinematic = pointers.PlayerKinematic;
            resetJob.InventoryCommands = SignalBus<InventoryCommandSignal>.ParallelWriter;
            resetJob.MedicalBayCount = pointers.MedicalBayCount;
            resetJob.TissueCount = pointers.TissueCount;
            resetJob.PenaltyCapacity = pointers.PenaltyCapacity;
            resetJob.Frame = context.Frame;
            resetJob.GlobalQualityWeight = _lastQualityWeight;
            resetJob.ScheduleMicroseconds = _lastScheduleMicroseconds;
            JobHandle resetHandle = resetJob.Schedule(dependsOn);

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
            return _activeHandle;
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            CompleteActiveJobIfReady(forceComplete: false);
            if (_jobScheduled)
                return;

            TryDumpFaultedTelemetry();
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (_jobScheduled)
            {
                if (!_activeHandle.IsCompleted)
                    return;

                CompleteActiveJobIfReady(forceComplete: false);
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
                (signal.Flags & PlayerRespawnSignalFlags.InvalidDeathAup) != 0u ||
                !math.all(math.isfinite(signal.DeathAUP)))
                return false;

            NativeArray<RespawnRequestDTO> requestArray = ResolveVaultBuffer(vault, in _requestHandle);
            NativeArray<RespawnStateDTO> stateArray = ResolveVaultBuffer(vault, in _stateHandle);
            if (!HasRequiredLength(requestArray, 1) || !HasRequiredLength(stateArray, 1))
                return false;

            double3 target = ResolveNearestMedicalBayAup(vault, signal.DeathAUP, out uint bayHash, out uint flags);
            uint playerHash = signal.PlayerHash != 0u ? signal.PlayerHash : DefaultPlayerHash;
            RespawnRequestDTO request = default;
            request.DeathAUP = signal.DeathAUP;
            request.PlayerHash = playerHash;
            request.DamageHash = signal.DamageHash;
            request.Frame = signal.Frame;
            request.Sequence = signal.Sequence;
            request.Flags = ShinobuRespawnFlags.PendingRequest | (flags & (ShinobuRespawnFlags.MockMedicalBay | ShinobuRespawnFlags.FallbackLifepod | ShinobuRespawnFlags.InvalidTargetAup));
            request.MedicalBayHashID = bayHash;
            requestArray[0] = request;

            RespawnStateDTO state = stateArray[0];
            state.TargetAUP = target;
            state.MedicalBayHashID = bayHash;
            state.Flags = ShinobuRespawnFlags.RespawnActive | ShinobuRespawnFlags.PendingRequest | request.Flags;
            stateArray[0] = state;
            _lastRequestSequence = signal.Sequence;

            RespawnSignalResolvedTargetTransformer transformer = default;
            transformer.Sequence = signal.Sequence;
            transformer.RespawnAUP = target;
            transformer.MedicalBayHashID = bayHash;
            transformer.Flags = PlayerRespawnSignalFlags.Requested |
                                PlayerRespawnSignalFlags.Committed |
                                PlayerRespawnSignalFlags.SuspendCollision |
                                TranslateSignalFlags(flags);
            transformer.SuspendCollisionFrames = 1;
            SignalBus<PlayerRespawnSignal>.TransformSnapshot(transformer);
            return true;
        }

        private double3 ResolveNearestMedicalBayAup(IDataVault vault, double3 deathAup, out uint bayHash, out uint flags)
        {
            flags = 0u;
            bayHash = 0u;
            NativeArray<RespawnTuningDTO> tuningArray = ResolveVaultBuffer(vault, in _tuningHandle);
            RespawnTuningDTO tuning = tuningArray.IsCreated && tuningArray.Length > 0 ? SanitizeTuning(tuningArray[0]) : CreateDefaultTuning();
            double3 fallback = SanitizeAup(tuning.FallbackLifepodAUP);
            double3 target = fallback;
            uint rejectedCandidateFlags = 0u;
            uint selectedCandidateFlags = 0u;
            double bestSq = double.MaxValue;
            double radius = math.max((double)tuning.MedicalBaySearchRadiusMeters, 0.0001d);
            double maxSearchSq = radius * radius;
            NativeArray<MedicalBayRespawnPointDTO> bays = ResolveVaultBuffer(vault, in _medicalBayHandle);
            if (bays.IsCreated)
            {
                for (int i = 0; i < bays.Length; i++)
                {
                    MedicalBayRespawnPointDTO bay = bays[i];
                    if (!math.all(math.isfinite(bay.BayAUP)))
                    {
                        rejectedCandidateFlags |= ShinobuRespawnFlags.InvalidTargetAup;
                        continue;
                    }

                    if (!ValidateMedicalBay(in bay, tuning.ValidationClearanceMeters))
                    {
                        rejectedCandidateFlags |= ShinobuRespawnFlags.InvalidTargetAup;
                        continue;
                    }

                    double3 delta = bay.BayAUP - deathAup;
                    if (!math.all(math.isfinite(delta)))
                    {
                        rejectedCandidateFlags |= ShinobuRespawnFlags.InvalidTargetAup;
                        continue;
                    }

                    float distanceSq = math.lengthsq(AupDeltaToFloat3(delta));
                    if (!math.isfinite(distanceSq))
                    {
                        rejectedCandidateFlags |= ShinobuRespawnFlags.InvalidTargetAup;
                        continue;
                    }

                    if ((double)distanceSq > maxSearchSq)
                    {
                        rejectedCandidateFlags |= ShinobuRespawnFlags.InvalidTargetAup;
                        continue;
                    }

                    if (distanceSq >= bestSq)
                        continue;

                    bestSq = distanceSq;
                    target = bay.BayAUP;
                    bayHash = bay.MedicalBayHashID;
                    selectedCandidateFlags = bay.Flags & ShinobuRespawnFlags.MockMedicalBay;
                }
            }

            if (bayHash == 0u || !math.all(math.isfinite(target)))
            {
                target = fallback;
                flags |= rejectedCandidateFlags | ShinobuRespawnFlags.FallbackLifepod;
            }
            else
            {
                flags |= selectedCandidateFlags;
            }

            return target;
        }

        private bool EnsureVaultState()
        {
            IDataVault vault = ResolveVaultCold();
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
                TryAcquireOwnedVaultDescriptor(vault, ShinobuRespawnConstants.RespawnCsvScratchBuffer, ShinobuRespawnConstants.CsvScratchBytes, NativeArrayOptions.UninitializedMemory, out _csvScratchHandle) &&
                TryGetExistingVaultDescriptor(vault, BufferID.ShinobuPhysiologyVitals, 1, out _vitalsHandle) &&
                TryGetExistingVaultDescriptor(vault, BufferID.ShinobuDecompressionStates, 1, out _decompressionHandle) &&
                TryGetExistingVaultDescriptor(vault, BufferID.ShinobuTissueCompartments, 1, out _tissueHandle) &&
                TryGetExistingVaultDescriptor(vault, BufferID.ShinobuPhysiologyScalars, 1, out _scalarHandle) &&
                TryGetExistingVaultDescriptor(vault, ShinobuMetabolismConstants.MetabolismStatesBuffer, 1, out _metabolismHandle) &&
                TryGetExistingVaultDescriptor(vault, BufferID.PlayerKinematicState, 1, out _playerKinematicHandle);
            if (!created)
            {
                ReleaseOwnedVaultDescriptors(vault);
                ClearCachedHandles();
            }

            return created;
        }

        private bool AreVaultHandlesCreated()
        {
            return IsVaultDescriptorCreated(in _stateHandle) &&
                   IsVaultDescriptorCreated(in _requestHandle) &&
                   IsVaultDescriptorCreated(in _medicalBayHandle) &&
                   IsVaultDescriptorCreated(in _fadeHandle) &&
                   IsVaultDescriptorCreated(in _telemetryHandle) &&
                   IsVaultDescriptorCreated(in _telemetryCursorHandle) &&
                   IsVaultDescriptorCreated(in _tuningHandle) &&
                   IsVaultDescriptorCreated(in _penaltyRulesHandle) &&
                   IsVaultDescriptorCreated(in _penaltyRuleCountHandle) &&
                   IsVaultDescriptorCreated(in _csvScratchHandle) &&
                   IsVaultDescriptorCreated(in _vitalsHandle) &&
                   IsVaultDescriptorCreated(in _decompressionHandle) &&
                   IsVaultDescriptorCreated(in _tissueHandle) &&
                   IsVaultDescriptorCreated(in _scalarHandle) &&
                   IsVaultDescriptorCreated(in _metabolismHandle) &&
                   IsVaultDescriptorCreated(in _playerKinematicHandle);
        }

        private bool AreVaultHandlesResolvable(IDataVault vault)
        {
            return IsVaultDescriptorResolvable(vault, in _stateHandle, 1) &&
                   IsVaultDescriptorResolvable(vault, in _requestHandle, 1) &&
                   IsVaultDescriptorResolvable(vault, in _medicalBayHandle, ShinobuRespawnConstants.MockMedicalBayCapacity) &&
                   IsVaultDescriptorResolvable(vault, in _fadeHandle, 1) &&
                   IsVaultDescriptorResolvable(vault, in _telemetryHandle, ShinobuRespawnConstants.TelemetryFrameCount) &&
                   IsVaultDescriptorResolvable(vault, in _telemetryCursorHandle, 1) &&
                   IsVaultDescriptorResolvable(vault, in _tuningHandle, 1) &&
                   IsVaultDescriptorResolvable(vault, in _penaltyRulesHandle, ShinobuRespawnConstants.PenaltyRuleCapacity) &&
                   IsVaultDescriptorResolvable(vault, in _penaltyRuleCountHandle, 1) &&
                   IsVaultDescriptorResolvable(vault, in _csvScratchHandle, ShinobuRespawnConstants.CsvScratchBytes) &&
                   IsVaultDescriptorResolvable(vault, in _vitalsHandle, 1) &&
                   IsVaultDescriptorResolvable(vault, in _decompressionHandle, 1) &&
                   IsVaultDescriptorResolvable(vault, in _tissueHandle, 1) &&
                   IsVaultDescriptorResolvable(vault, in _scalarHandle, 1) &&
                   IsVaultDescriptorResolvable(vault, in _metabolismHandle, 1) &&
                   IsVaultDescriptorResolvable(vault, in _playerKinematicHandle, 1);
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
            return IsVaultGenerationCurrent(vault, in _stateHandle) &&
                   IsVaultGenerationCurrent(vault, in _requestHandle) &&
                   IsVaultGenerationCurrent(vault, in _medicalBayHandle) &&
                   IsVaultGenerationCurrent(vault, in _fadeHandle) &&
                   IsVaultGenerationCurrent(vault, in _telemetryHandle) &&
                   IsVaultGenerationCurrent(vault, in _telemetryCursorHandle) &&
                   IsVaultGenerationCurrent(vault, in _tuningHandle) &&
                   IsVaultGenerationCurrent(vault, in _penaltyRulesHandle) &&
                   IsVaultGenerationCurrent(vault, in _penaltyRuleCountHandle) &&
                   IsVaultGenerationCurrent(vault, in _csvScratchHandle) &&
                   IsVaultGenerationCurrent(vault, in _vitalsHandle) &&
                   IsVaultGenerationCurrent(vault, in _decompressionHandle) &&
                   IsVaultGenerationCurrent(vault, in _tissueHandle) &&
                   IsVaultGenerationCurrent(vault, in _scalarHandle) &&
                   IsVaultGenerationCurrent(vault, in _metabolismHandle) &&
                   IsVaultGenerationCurrent(vault, in _playerKinematicHandle);
        }

        private static bool IsVaultDescriptorCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static bool IsVaultGenerationCurrent<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            BufferID bufferId = unchecked((BufferID)(int)handle.BufferID);
            return IsVaultDescriptorCreated(in handle) &&
                   vault.TryGetBufferGeneration(bufferId, out uint generation) &&
                   generation == handle.Generation;
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

            if (vault.TryGetGenerationHandle<T>(bufferId, out handle) &&
                TryResolveVaultBuffer(vault, in handle, out NativeArray<T> existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return true;
            }

            handle = default;
            if (vault.IsAllocationLocked)
                return false;

            handle = vault.GetGenerationHandle<T>(bufferId, requiredLength, OwnerSystem, options);
            return TryResolveVaultBuffer(vault, in handle, out NativeArray<T> created) &&
                   created.IsCreated &&
                   created.Length >= requiredLength;
        }

        private static bool TryGetExistingVaultDescriptor<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out VaultGenerationHandle<T> handle) where T : struct
        {
            handle = default;
            if (vault == null || requiredLength <= 0)
                return false;

            return vault.TryGetGenerationHandle<T>(bufferId, out handle) &&
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
            ReleaseVaultDescriptor(vault, in _csvScratchHandle);
        }

        private static void ReleaseVaultDescriptor<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            if (IsVaultDescriptorCreated(in handle))
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
            NativeArray<MedicalBayRespawnPointDTO> bays = ResolveVaultBuffer(vault, in _medicalBayHandle);
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
            tuning[0] = defaultTuning;
            RespawnFadeDTO defaultFade = default;
            defaultFade.GlobalQualityWeight = ResolveQualityWeight();
            fade[0] = defaultFade;
            state[0] = default;
            request[0] = default;
            cursor[0] = default;
            count[0] = 0;

            GenerateMockRespawnPointsJob mockJob = default;
            mockJob.MedicalBays = bays;
            mockJob.FallbackLifepodAUP = defaultTuning.FallbackLifepodAUP;
            mockJob.ValidationClearanceMeters = defaultTuning.ValidationClearanceMeters;
            for (int i = 0; i < bays.Length; i++)
                mockJob.Execute(i);

            _defaultsInitialized = true;
        }

        private bool TryResolveJobPointers(IDataVault vault, out JobPointers pointers)
        {
            pointers = default;
            NativeArray<RespawnStateDTO> state = ResolveVaultBuffer(vault, in _stateHandle);
            NativeArray<RespawnRequestDTO> request = ResolveVaultBuffer(vault, in _requestHandle);
            NativeArray<MedicalBayRespawnPointDTO> bays = ResolveVaultBuffer(vault, in _medicalBayHandle);
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
                !HasRequiredLength(kinematic, 1))
            {
                return false;
            }

            pointers.State = (RespawnStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(state);
            pointers.Request = (RespawnRequestDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(request);
            pointers.MedicalBays = (MedicalBayRespawnPointDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(bays);
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
            pointers.PlayerKinematic = (LockstepPlayerKinematicState*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(kinematic);
            pointers.MedicalBayCount = bays.Length;
            pointers.TissueCount = tissues.Length;
            pointers.PenaltyCapacity = penalty.Length;
            return true;
        }

        private bool TryLoadPenaltyCsv()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsVaultDescriptorCreated(in _csvScratchHandle) ||
                !IsVaultDescriptorCreated(in _penaltyRulesHandle) ||
                !IsVaultDescriptorCreated(in _penaltyRuleCountHandle))
                return false;

            NativeArray<byte> scratch = ResolveVaultBuffer(vault, in _csvScratchHandle);
            NativeArray<InventoryDeathPenaltyRuleDTO> rules = ResolveVaultBuffer(vault, in _penaltyRulesHandle);
            NativeArray<int> count = ResolveVaultBuffer(vault, in _penaltyRuleCountHandle);
            if (!HasRequiredLength(scratch, ShinobuRespawnConstants.CsvScratchBytes) ||
                !HasRequiredLength(rules, ShinobuRespawnConstants.PenaltyRuleCapacity) ||
                !HasRequiredLength(count, 1))
            {
                return false;
            }

            count[0] = 0;
            if (!File.Exists(_csvPath))
                return false;

            using FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
            Span<byte> scratchSpan = new Span<byte>(scratchPtr, scratch.Length);
            int read = stream.Read(scratchSpan);
            int parsed = ParsePenaltyCsv(scratchSpan.Slice(0, read), rules);
            count[0] = parsed;
            return parsed > 0;
        }

        private static int ParsePenaltyCsv(ReadOnlySpan<byte> bytes, NativeArray<InventoryDeathPenaltyRuleDTO> rules)
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

            TryDumpTelemetry(_dumpPath, cursor[0].Flags);
            TryDumpTelemetry(_legacyDumpPath, cursor[0].Flags);
            _dumpedFault = true;
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

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(DumpMagic);
            writer.Write(DumpVersion);
            writer.Write(ShinobuRespawnConstants.TelemetryFrameCount);
            writer.Write(cursor[0].Cursor);
            writer.Write(reasonFlags);
            for (int i = 0; i < ShinobuRespawnConstants.TelemetryFrameCount; i++)
            {
                RespawnTelemetryEntry entry = telemetry[i];
                writer.Write(entry.DeathAUP.x);
                writer.Write(entry.DeathAUP.y);
                writer.Write(entry.DeathAUP.z);
                writer.Write(entry.RespawnAUP.x);
                writer.Write(entry.RespawnAUP.y);
                writer.Write(entry.RespawnAUP.z);
                writer.Write(entry.CauseHash);
                writer.Write(entry.Frame);
                writer.Write(entry.ReconcileMicroseconds);
                writer.Write(entry.Flags);
            }

            return true;
        }

        public static bool TryReadEditorState(out RespawnFadeDTO fade, out RespawnTuningDTO tuning)
        {
            fade = default;
            tuning = default;
            ShinobuRespawnReconciliationRuntime runtime = s_active;
            if (runtime == null || !runtime.EnsureVaultState() || !runtime.TryPrepareEditorVaultAccess())
                return false;

            IDataVault vault = runtime.ResolveVaultCold();
            NativeArray<RespawnFadeDTO> fadeArray = ResolveVaultBuffer(vault, in runtime._fadeHandle);
            NativeArray<RespawnTuningDTO> tuningArray = ResolveVaultBuffer(vault, in runtime._tuningHandle);
            if (!HasRequiredLength(fadeArray, 1) || !HasRequiredLength(tuningArray, 1))
                return false;

            fade = fadeArray[0];
            tuning = tuningArray[0];
            return true;
        }

        public static bool TryWriteEditorTuning(in RespawnTuningDTO tuning)
        {
            ShinobuRespawnReconciliationRuntime runtime = s_active;
            if (runtime == null || !runtime.EnsureVaultState() || !runtime.TryPrepareEditorVaultAccess())
                return false;

            IDataVault vault = runtime.ResolveVaultCold();
            NativeArray<RespawnTuningDTO> tuningArray = ResolveVaultBuffer(vault, in runtime._tuningHandle);
            if (!HasRequiredLength(tuningArray, 1))
                return false;

            RespawnTuningDTO sanitized = SanitizeTuning(tuning);
            sanitized.Flags |= ShinobuRespawnFlags.ManualTuning;
            tuningArray[0] = sanitized;
            return true;
        }

        public static bool TryReloadPenaltyCsvFromEditor()
        {
            ShinobuRespawnReconciliationRuntime runtime = s_active;
            return runtime != null &&
                   runtime.EnsureVaultState() &&
                   runtime.TryPrepareEditorVaultAccess() &&
                   runtime.TryLoadPenaltyCsv();
        }

        public static bool TryDumpBlackBoxForEditor()
        {
            ShinobuRespawnReconciliationRuntime runtime = s_active;
            return runtime != null &&
                   runtime.EnsureVaultState() &&
                   runtime.TryPrepareEditorVaultAccess() &&
                   runtime.TryDumpTelemetry(runtime._dumpPath, 0u) &&
                   runtime.TryDumpTelemetry(runtime._legacyDumpPath, 0u);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            IDataVault vault = _dataVault != null ? _dataVault : ResolveVaultCold();
            if (vault == null || !IsVaultDescriptorCreated(in _medicalBayHandle))
                return;

            NativeArray<MedicalBayRespawnPointDTO> bays = ResolveVaultBuffer(vault, in _medicalBayHandle);
            if (!HasRequiredLength(bays, 1))
                return;

            Handles.color = Color.green;
            Gizmos.color = Color.green;
            for (int i = 0; i < bays.Length; i++)
            {
                MedicalBayRespawnPointDTO bay = bays[i];
                if (bay.MedicalBayHashID == 0u || !math.all(math.isfinite(bay.BayAUP)))
                    continue;

                Vector3 center = HectonFloatingOrigin.ToRuntimePosition(bay.BayAUP);
                float radius = math.max(0.5f, bay.ClearanceMeters);
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
#endif

        private void CompleteActiveJobIfReady(bool forceComplete)
        {
            if (!_jobScheduled)
                return;

            if (!forceComplete && !_activeHandle.IsCompleted)
                return;

            if (forceComplete)
            {
                DispatcherJobFence.TryComplete(ref _activeHandle, forceComplete: true);
            }
            else if (!DispatcherJobFence.TryFinalizeCompleted(ref _activeHandle))
            {
                return;
            }

            _jobScheduled = false;
        }

        private bool TryPrepareEditorVaultAccess()
        {
            CompleteActiveJobIfReady(forceComplete: false);
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

            GlobalRegistry.UnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private IDataVault ResolveVaultCold()
        {
            if (_dataVault != null)
                return _dataVault;

            _dataVault = GlobalRegistry.DataVault;
            if (_dataVault == null && GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latestVault))
                _dataVault = latestVault;
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
            _csvScratchHandle = default;
            _vitalsHandle = default;
            _decompressionHandle = default;
            _tissueHandle = default;
            _scalarHandle = default;
            _metabolismHandle = default;
            _playerKinematicHandle = default;
            _defaultsInitialized = false;
            _respawnDearLieVisualActive = false;
        }

        private static void ConfigureSignalLanes()
        {
            SignalBus<PlayerRespawnSignal>.Configure(
                PlayerRespawnSignal.ExpectedCapacity,
                maxFrameSignals: PlayerRespawnSignal.MaxFrameSignals,
                lowTierFrameSignals: PlayerRespawnSignal.LowTierFrameSignals,
                laneHash: PlayerRespawnSignal.LaneHash);
            SignalBus<PlayerRespawnSignal>.EnsureInitialized();
            SignalBus<InventoryCommandSignal>.EnsureInitialized();
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

        private static bool ValidateMedicalBay(in MedicalBayRespawnPointDTO bay, float clearanceMeters)
        {
            if (bay.MedicalBayHashID == 0u)
                return false;

            double3 delta = bay.BayAUP - bay.NearestTerrainAUP;
            if (!math.all(math.isfinite(delta)))
                return false;

            float3 local = AupDeltaToFloat3(delta);
            float distanceSq = math.lengthsq(local);
            float clearance = math.max(math.max(clearanceMeters, bay.ClearanceMeters), 0.25f);
            return math.isfinite(distanceSq) && distanceSq >= clearance * clearance;
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
            return math.max(HectonPhysicsContract.AupSectorSizeMetersDouble, 0.0001d);
        }

        private static uint TranslateSignalFlags(uint flags)
        {
            uint translated = 0u;
            if ((flags & ShinobuRespawnFlags.MockMedicalBay) != 0u) translated |= PlayerRespawnSignalFlags.MockMedicalBay;
            if ((flags & ShinobuRespawnFlags.FallbackLifepod) != 0u) translated |= PlayerRespawnSignalFlags.FallbackLifepod;
            if ((flags & ShinobuRespawnFlags.InvalidTargetAup) != 0u) translated |= PlayerRespawnSignalFlags.InvalidTargetAup;
            if ((flags & ShinobuRespawnFlags.PenaltyApplied) != 0u) translated |= PlayerRespawnSignalFlags.PenaltyApplied;
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
                if (signal.Sequence != Sequence ||
                    (signal.Flags & PlayerRespawnSignalFlags.InvalidDeathAup) != 0u ||
                    !math.all(math.isfinite(signal.DeathAUP)))
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

        private static string ResolveProjectRoot()
        {
            string dataPath = Application.dataPath;
            return string.IsNullOrEmpty(dataPath) ? "." : Path.GetFullPath(Path.Combine(dataPath, ".."));
        }

        private struct JobPointers
        {
            public RespawnStateDTO* State;
            public RespawnRequestDTO* Request;
            public MedicalBayRespawnPointDTO* MedicalBays;
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
