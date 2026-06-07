using UnityEngine;
using System;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.AtlasSignal;
using Hecton8.Narrative;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Unity.Mathematics;

namespace Hecton8.Gameplay.Atlas6Liability
{
    public enum Atlas6ThreatLevel
    {
        Nominal = 0,
        SubstrateAtRisk = 1,
        ActuarialLiability = 2,
        TotalLockdown = 3
    }

    /// <summary>
    /// Atlas-6 Protocol Core Manager.
    /// Master orchestrator integrating the Varnek, Arendt, Haldane, Ibarra, and Sato-Ren Corporate Protocols.
    /// Acts as the central nervous system for the hostile corporate logic.
    /// </summary>
    public sealed class Atlas6CorporateLiabilityManager : MonoBehaviour, IUpdatable, ISaveable, IGlobalRegistryHotSwapListener, IAudioLogEventListener, INarrativeEventListener
    {
        public const float MaximumTrackedSectorXenonOmegaYield = 1000000f;
        public const float XenonOmegaBiomatterExposurePerYieldUnit = 0.02f;
        public const string XenonOmegaVentCacheStableId = "resource.node.xenon_omega_vent_cache";
        public const string Atlas6TerminalSector3AudioLogId = "atlas6_terminal_sector3";
        public const string ChenMSuitDiscoveryId = "chen_m_suit";
        public const string ChenMWorkerTagId = "CHEN_M";
        public static readonly int XenonOmegaVentCacheStableHashId = LocHash.Compute(XenonOmegaVentCacheStableId);
        public static readonly uint Atlas6TerminalSector3AudioLogHash = QuestFlagHashKernel.ComputeStableHash(Atlas6TerminalSector3AudioLogId);
        public static readonly uint ChenMSuitDiscoveryHash = NarrativeEvents.ComputeDiscoveryHash(ChenMSuitDiscoveryId);
        public static readonly uint ChenMWorkerTagHash = Atlas6LiabilityTelemetry.ComputeStableHash(ChenMWorkerTagId);

        public static Atlas6CorporateLiabilityManager ActiveRuntimeInstance { get; private set; }

        [Header("Corporate Directive Settings")]
        [SerializeField] private float sectorXenonOmegaYield = 0f;
        [SerializeField] private float playerDistanceToPrimaryDrillSite = 200f;
        [SerializeField] private bool hasDisasterEvidenceInInventory = false;

        // Core Sub-systems
        public DirectiveWeightingSystem DirectiveWeighting { get; private set; }
        public ThermalSheerManager ThermalSheer { get; private set; }
        public ActuarialLiabilitySystem ActuarialLiability { get; private set; }
        public ExtractionGatingSystem ExtractionGating { get; private set; }
        public Atlas6LiabilityTelemetry Telemetry { get; private set; }

        [Header("System State")]
        [SerializeField] private Atlas6ThreatLevel currentThreatLevel = Atlas6ThreatLevel.Nominal;

        public event Action<Atlas6ThreatLevel> OnThreatLevelChanged;
        public Atlas6ThreatLevel CurrentThreatLevel => currentThreatLevel;
        public float SectorXenonOmegaYield => sectorXenonOmegaYield;
        public bool HasDisasterEvidenceInInventory => hasDisasterEvidenceInInventory;
        public int SavePriority => 12;
        public int LoadPriority => 12;

        public static bool IsXenonOmegaResourceTemplateHash(int resourceTemplateStableHashId)
        {
            return resourceTemplateStableHashId != 0 &&
                   resourceTemplateStableHashId == XenonOmegaVentCacheStableHashId;
        }

        public static bool TryReportXenonOmegaExtracted(int resourceTemplateStableHashId, float amount)
        {
            if (!IsXenonOmegaResourceTemplateHash(resourceTemplateStableHashId))
                return false;

            Atlas6CorporateLiabilityManager activeRuntime = ActiveRuntimeInstance;
            if (activeRuntime == null)
                return false;

            activeRuntime.ReportXenonOmegaExtracted(amount);
            return true;
        }

        public static bool IsAtlas6DisasterEvidenceAudioLogHash(uint audioLogHash)
        {
            return audioLogHash != 0u &&
                   audioLogHash == Atlas6TerminalSector3AudioLogHash;
        }

        public static bool IsAtlas6WorkerTagDiscoveryHash(uint discoveryHash)
        {
            return discoveryHash != 0u &&
                   discoveryHash == ChenMSuitDiscoveryHash;
        }

        private bool _isRegistered;
        private bool _registeredHotSwapListener;
        private bool _registeredAudioLogEvents;
        private bool _registeredNarrativeEvents;
        private bool _saveRegistered;
        private bool _actuarialThreatPublished;
        private bool _satoRenSeverancePublished;
        private ISaveService _saveService;
        private IAudioLogRuntime _audioLogs;
        private ActuarialLiabilitySystem _wiredActuarialLiability;
        private ExtractionGatingSystem _wiredExtractionGating;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveRuntimeInstance()
        {
            ActiveRuntimeInstance = null;
        }

        private void Awake()
        {
            if (!TryRegisterActiveRuntimeInstance())
                return;

            Telemetry = new Atlas6LiabilityTelemetry();
            DirectiveWeighting = new DirectiveWeightingSystem(Telemetry);
            ThermalSheer = new ThermalSheerManager();
            ActuarialLiability = new ActuarialLiabilitySystem(Telemetry);
            ExtractionGating = new ExtractionGatingSystem(Telemetry);

            // Initialize baseline states
            DirectiveWeighting.Initialize(1.0f);
            ActuarialLiability.Initialize(5000f); // starting corporate credit
            SanitizeSectorXenonOmegaYield();
        }

        private void OnEnable()
        {
            if (!TryRegisterActiveRuntimeInstance())
                return;

            EnsureSubsystemsInitialized();
            TryRegisterHotSwapListener();
            TryRegisterAudioLogEvents();
            TryRegisterNarrativeEvents();
            CacheAudioLogSystem(GlobalRegistry.AudioLogRuntime);
            TrySyncDisasterEvidenceFromAudioLogRuntime();
            TrySyncWorkerTagsFromNarrativeDiscoveryReadModel(GlobalRegistry.NarrativeDiscoveryReadModel);
            TryRegisterSaveParticipant();
            RegisterWithGlobalRegistry();
            WireSubsystemEvents();
        }

        private void OnDisable()
        {
            UnregisterFromGlobalRegistry();
            TryUnregisterNarrativeEvents();
            TryUnregisterAudioLogEvents();
            TryUnregisterHotSwapListener();
            TryUnregisterSaveParticipant();
            UnwireSubsystemEvents();
            ClearCachedRuntimeServices();

            TryUnregisterActiveRuntimeInstance();
        }

        private void OnDestroy()
        {
            TryUnregisterNarrativeEvents();
            TryUnregisterAudioLogEvents();
            TryUnregisterHotSwapListener();
            TryUnregisterSaveParticipant();
            UnwireSubsystemEvents();
            ClearCachedRuntimeServices();
            TryUnregisterActiveRuntimeInstance();
        }

        public void Tick(float deltaTime)
        {
            if (!_isRegistered) return;

            SanitizeSectorXenonOmegaYield();
            EvaluateThreatLevel();
            ApplyArendtDirectiveWeighting(deltaTime);
            ValidateExtractionGating();
        }

        private void EvaluateThreatLevel()
        {
            Atlas6ThreatLevel newLevel = CalculateThreatLevel();

            if (newLevel != currentThreatLevel)
            {
                currentThreatLevel = newLevel;
                RecordTelemetry(
                    Atlas6LiabilityEventCode.ThreatLevelChanged,
                    currentThreatLevel >= Atlas6ThreatLevel.ActuarialLiability
                        ? Atlas6LiabilityEventSeverity.Critical
                        : Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.ManagerContextHash,
                    value0: (float)currentThreatLevel,
                    value1: sectorXenonOmegaYield);
                OnThreatLevelChanged?.Invoke(currentThreatLevel);
            }
        }

        private Atlas6ThreatLevel CalculateThreatLevel()
        {
            Atlas6ThreatLevel newLevel = Atlas6ThreatLevel.Nominal;

            if (ActuarialLiability != null && ActuarialLiability.IsPlayerActuarialThreat)
                newLevel = Atlas6ThreatLevel.ActuarialLiability;
            else if (sectorXenonOmegaYield > 1000f)
                newLevel = Atlas6ThreatLevel.SubstrateAtRisk;

            if (DirectiveWeighting != null && DirectiveWeighting.PressureSealIntegrity < 0.15f)
                newLevel = Atlas6ThreatLevel.TotalLockdown;

            return newLevel;
        }

        private void RecordTelemetry(
            Atlas6LiabilityEventCode eventCode,
            Atlas6LiabilityEventSeverity severity,
            uint contextHash,
            uint subjectHash = 0u,
            float value0 = 0f,
            float value1 = 0f,
            Atlas6LiabilityFaultFlags faultFlags = Atlas6LiabilityFaultFlags.None)
        {
            Telemetry?.Record(
                eventCode,
                severity,
                contextHash,
                subjectHash,
                value0,
                value1,
                ExtractionGating != null ? ExtractionGating.CarrierState : ExtractionCarrierState.Offline,
                currentThreatLevel,
                faultFlags);
        }

        // --- PUBLIC API EXPOSED TO OTHER HECTON-8 SYSTEMS ---

        public void ReportXenonOmegaExtracted(float amount)
        {
            EnsureSubsystemsInitialized();
            SanitizeSectorXenonOmegaYield();
            bool invalidAmount = !math.isfinite(amount) || amount <= 0f;
            if (invalidAmount)
            {
                RecordTelemetry(
                    Atlas6LiabilityEventCode.InvalidXenonOmegaYieldReported,
                    Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.ManagerContextHash,
                    value0: amount,
                    faultFlags: math.isfinite(amount)
                        ? Atlas6LiabilityFaultFlags.InvalidRangeInput
                        : Atlas6LiabilityFaultFlags.NonFiniteInput);
                return;
            }

            double accumulatedYield = (double)sectorXenonOmegaYield + amount;
            Atlas6LiabilityFaultFlags faultFlags = Atlas6LiabilityFaultFlags.None;
            if (accumulatedYield > MaximumTrackedSectorXenonOmegaYield)
            {
                sectorXenonOmegaYield = MaximumTrackedSectorXenonOmegaYield;
                faultFlags = Atlas6LiabilityFaultFlags.InvalidRangeInput;
            }
            else
            {
                sectorXenonOmegaYield = (float)accumulatedYield;
            }

            RecordTelemetry(
                Atlas6LiabilityEventCode.XenonOmegaYieldReported,
                Atlas6LiabilityEventSeverity.Info,
                Atlas6LiabilityTelemetry.ManagerContextHash,
                value0: sectorXenonOmegaYield,
                value1: amount,
                faultFlags: faultFlags);
            ExtractionGating?.AddBiomatterExposure(amount * XenonOmegaBiomatterExposurePerYieldUnit);
        }

        public void ReportWorkerTagScanned(string workerId)
        {
            EnsureSubsystemsInitialized();
            ActuarialLiability?.RegisterWorkerTagRecovery(workerId);
        }

        public void ReportWorkerTagScannedHash(uint workerTagHash)
        {
            EnsureSubsystemsInitialized();
            ActuarialLiability?.RegisterWorkerTagRecoveryHash(workerTagHash);
        }

        public void ReportGhostPDADataUploaded(float dataSizeInMegabytes)
        {
            EnsureSubsystemsInitialized();
            ActuarialLiability?.UploadGhostPDAData(dataSizeInMegabytes);
        }

        public void ReportDisasterEvidenceCollected()
        {
            EnsureSubsystemsInitialized();
            if (hasDisasterEvidenceInInventory)
                return;

            hasDisasterEvidenceInInventory = true;
            RecordTelemetry(
                Atlas6LiabilityEventCode.DisasterEvidenceCollected,
                Atlas6LiabilityEventSeverity.Warning,
                Atlas6LiabilityTelemetry.ExtractionContextHash);
        }

        public void ReportDisasterEvidenceDiscarded()
        {
            EnsureSubsystemsInitialized();
            if (!hasDisasterEvidenceInInventory)
                return;

            hasDisasterEvidenceInInventory = false;
            RecordTelemetry(
                Atlas6LiabilityEventCode.DisasterEvidenceDiscarded,
                Atlas6LiabilityEventSeverity.Info,
                Atlas6LiabilityTelemetry.ExtractionContextHash);
        }

        public ThermalSheerManager.TelemetryReadout GetSubmarineOSReadout(float trueSheer)
        {
            if (ThermalSheer == null)
                ThermalSheer = new ThermalSheerManager();

            return ThermalSheer.CalculateTelemetry(trueSheer, playerDistanceToPrimaryDrillSite);
        }

        public bool AttemptCarrierTether()
        {
            EnsureSubsystemsInitialized();
            return ExtractionGating != null &&
                   ExtractionGating.RequestExtractionTether(sectorXenonOmegaYield, hasDisasterEvidenceInInventory);
        }

        public bool BoardBlackKeel()
        {
            EnsureSubsystemsInitialized();
            return ExtractionGating != null && ExtractionGating.AttemptBoardingSequence();
        }

        public bool TryCopyLatestTelemetry(out Atlas6LiabilityTelemetryRecord record)
        {
            if (Telemetry == null)
            {
                record = default;
                return false;
            }

            return Telemetry.TryCopyLatest(out record);
        }

        public bool TryCopyTelemetryNewest(int newestOffset, out Atlas6LiabilityTelemetryRecord record)
        {
            if (Telemetry == null)
            {
                record = default;
                return false;
            }

            return Telemetry.TryCopyNewest(newestOffset, out record);
        }

        public void OnAudioLogEvent(in AudioLogEventPayload payload)
        {
            if (payload.Type != AudioLogEventType.Discovered ||
                !IsAtlas6DisasterEvidenceAudioLogHash(payload.LogHash))
            {
                return;
            }

            ReportDisasterEvidenceCollected();
        }

        public void OnNarrativeEvent(in NarrativeEventPayload payload)
        {
            if ((NarrativeEventType)payload.EventType != NarrativeEventType.DiscoveryMade ||
                !IsAtlas6WorkerTagDiscoveryHash(payload.DiscoveryHash))
            {
                return;
            }

            ReportWorkerTagScannedHash(ChenMWorkerTagHash);
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.atlas6LiabilitySectorXenonOmegaYield = math.isfinite(sectorXenonOmegaYield)
                ? math.clamp(sectorXenonOmegaYield, 0f, MaximumTrackedSectorXenonOmegaYield)
                : 0f;
            data.atlas6LiabilityHasDisasterEvidence = hasDisasterEvidenceInInventory;
            data.atlas6LiabilityCorporateCreditBalance = ActuarialLiability != null
                ? ActuarialLiability.CorporateCreditBalance
                : 0f;
            data.atlas6LiabilityCorporateHostilityIndex = ActuarialLiability != null
                ? ActuarialLiability.CorporateHostilityIndex
                : 0f;
            SaveData.EnsureExactArrayCapacity(
                ref data.atlas6LiabilityRecoveredWorkerTagHashes,
                SaveData.MaxAtlas6LiabilityWorkerTags);
            int recoveredWorkerTagCount = ActuarialLiability != null
                ? ActuarialLiability.CopyRecoveredWorkerTagHashesTo(
                    data.atlas6LiabilityRecoveredWorkerTagHashes,
                    SaveData.MaxAtlas6LiabilityWorkerTags)
                : 0;
            data.atlas6LiabilityRecoveredWorkerTagCount = recoveredWorkerTagCount;
            for (int i = recoveredWorkerTagCount; i < SaveData.MaxAtlas6LiabilityWorkerTags; i++)
                data.atlas6LiabilityRecoveredWorkerTagHashes[i] = 0u;

            data.atlas6LiabilityExtractionCarrierState = ExtractionGating != null
                ? (int)ExtractionGating.CarrierState
                : (int)ExtractionCarrierState.Offline;
            data.atlas6LiabilityBiomatterExposureLevel = ExtractionGating != null
                ? ExtractionGating.BiomatterExposureLevel
                : 0f;
            data.atlas6LiabilityHaldaneLockoutActive = ExtractionGating != null &&
                                                       ExtractionGating.IsHaldaneLockoutActive;
            data.atlas6LiabilityPressureSealIntegrity = DirectiveWeighting != null
                ? DirectiveWeighting.PressureSealIntegrity
                : 1f;
            data.atlas6LiabilityBulkheadLocked = DirectiveWeighting != null &&
                                                 DirectiveWeighting.IsBulkheadLocked;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (data == null)
                return;

            EnsureSubsystemsInitialized();
            sectorXenonOmegaYield = math.isfinite(data.atlas6LiabilitySectorXenonOmegaYield)
                ? math.clamp(data.atlas6LiabilitySectorXenonOmegaYield, 0f, MaximumTrackedSectorXenonOmegaYield)
                : 0f;
            hasDisasterEvidenceInInventory = data.atlas6LiabilityHasDisasterEvidence;
            ActuarialLiability.RestoreState(
                data.atlas6LiabilityCorporateCreditBalance,
                data.atlas6LiabilityCorporateHostilityIndex,
                data.atlas6LiabilityRecoveredWorkerTagHashes,
                data.atlas6LiabilityRecoveredWorkerTagCount);
            ExtractionGating.RestoreState(
                (ExtractionCarrierState)data.atlas6LiabilityExtractionCarrierState,
                data.atlas6LiabilityBiomatterExposureLevel,
                data.atlas6LiabilityHaldaneLockoutActive);
            DirectiveWeighting.RestoreState(
                data.atlas6LiabilityPressureSealIntegrity,
                data.atlas6LiabilityBulkheadLocked);

            _actuarialThreatPublished = ActuarialLiability.IsPlayerActuarialThreat;
            _satoRenSeverancePublished = ExtractionGating.CarrierState == ExtractionCarrierState.TetherSevered;
            currentThreatLevel = CalculateThreatLevel();
        }

        // --- INTERNAL EVENT HANDLERS ---

        private void ApplyArendtDirectiveWeighting(float deltaTime)
        {
            if (DirectiveWeighting != null)
            {
                DirectiveWeighting.Tick(deltaTime, sectorXenonOmegaYield);
            }
        }

        private void ValidateExtractionGating()
        {
            if (ExtractionGating != null &&
               (ExtractionGating.CarrierState == ExtractionCarrierState.TetherRequested ||
                ExtractionGating.CarrierState == ExtractionCarrierState.TetherEstablished))
            {
                // Haldane Quarantine: actively monitor exposure while tether is active
                // If the player receives a lethal dose of Xenon-Omega biomatter while waiting,
                // the system proactively severs the tether and locks the staging airlock.
                if (ExtractionGating.BiomatterExposureLevel > ExtractionGating.LockoutExposureThreshold)
                {
                    if (ExtractionGating.IsHaldaneLockoutActive)
                        return;

                    RecordTelemetry(
                        Atlas6LiabilityEventCode.DynamicHaldaneMonitorRejected,
                        Atlas6LiabilityEventSeverity.Critical,
                        Atlas6LiabilityTelemetry.ManagerContextHash,
                        value0: ExtractionGating.BiomatterExposureLevel);
                    ExtractionGating.AttemptBoardingSequence(); // This will trigger the lockout event
                }
            }
        }

        private void HandleActuarialThreat()
        {
            if (_actuarialThreatPublished)
                return;

            _actuarialThreatPublished = true;
            RecordTelemetry(
                Atlas6LiabilityEventCode.ActuarialThreatRaised,
                Atlas6LiabilityEventSeverity.Critical,
                Atlas6LiabilityTelemetry.ManagerContextHash,
                value0: ActuarialLiability.CorporateHostilityIndex,
                value1: ActuarialLiability.RecoveredWorkerTags);
            Atlas6Events.TryRaisePlayerStatusChanged(Atlas6PlayerStatus.Threat);
            Atlas6Events.TryRaiseDirectiveConflict(Atlas6Events.ActuarialLiabilityThreatConflictHash);
        }

        private void HandleSatoRenSeverance()
        {
            if (_satoRenSeverancePublished)
                return;

            _satoRenSeverancePublished = true;
            RecordTelemetry(
                Atlas6LiabilityEventCode.TetherSeveredSatoRen,
                Atlas6LiabilityEventSeverity.Critical,
                Atlas6LiabilityTelemetry.ManagerContextHash,
                value0: sectorXenonOmegaYield);
            Atlas6Events.TryRaisePlayerStatusChanged(Atlas6PlayerStatus.Threat);
            Atlas6Events.TryRaiseDirectiveConflict(Atlas6Events.SatoRenSilenceSeveranceConflictHash);
        }

        // --- REGISTRY ---

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                UnregisterFromGlobalRegistry();
                RegisterWithGlobalRegistry();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.AudioLogRuntime)
            {
                CacheAudioLogSystem(currentService as IAudioLogRuntime);
                TrySyncDisasterEvidenceFromAudioLogRuntime();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.NarrativeDirectorRuntime)
            {
                TrySyncWorkerTagsFromNarrativeDiscoveryReadModel(currentService as INarrativeDiscoveryReadModel);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Save)
            {
                TryUnregisterSaveParticipant();
                _saveService = currentService as ISaveService;
                TryRegisterSaveParticipant();
            }
        }

        private void RegisterWithGlobalRegistry()
        {
            if (!Application.isPlaying)
                return;

            if (!_isRegistered)
            {
                _isRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
                if (_isRegistered)
                {
                    RecordTelemetry(
                        Atlas6LiabilityEventCode.RegistryRegistrationChanged,
                        Atlas6LiabilityEventSeverity.Info,
                        Atlas6LiabilityTelemetry.ManagerContextHash,
                        value0: 1f);
                }
            }
        }

        private void UnregisterFromGlobalRegistry()
        {
            if (_isRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _isRegistered = false;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void TryRegisterAudioLogEvents()
        {
            if (_registeredAudioLogEvents || !Application.isPlaying)
                return;

            AudioLogEvents.Register(this);
            _registeredAudioLogEvents = true;
        }

        private void TryUnregisterAudioLogEvents()
        {
            if (!_registeredAudioLogEvents)
                return;

            AudioLogEvents.Unregister(this);
            _registeredAudioLogEvents = false;
        }

        private void TryRegisterNarrativeEvents()
        {
            if (_registeredNarrativeEvents || !Application.isPlaying)
                return;

            NarrativeEvents.Register(this);
            _registeredNarrativeEvents = true;
        }

        private void TryUnregisterNarrativeEvents()
        {
            if (!_registeredNarrativeEvents)
                return;

            NarrativeEvents.Unregister(this);
            _registeredNarrativeEvents = false;
        }

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered || !Application.isPlaying || !isActiveAndEnabled)
                return;

            if (_saveService == null)
                _saveService = GlobalRegistry.Save;
            if (_saveService == null)
                return;

            _saveService.Register(this);
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered)
                return;

            ISaveService saveService = _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _saveService = null;
            _saveRegistered = false;
        }

        private void ClearCachedRuntimeServices()
        {
            _audioLogs = null;
        }

        private void TrySyncDisasterEvidenceFromAudioLogRuntime()
        {
            if (!Application.isPlaying)
                return;

            if (hasDisasterEvidenceInInventory)
                return;

            IAudioLogRuntime audioLogRuntime = ResolveAudioLogSystem();
            if (audioLogRuntime == null)
                return;

            if (audioLogRuntime.IsAudioLogDiscovered(Atlas6TerminalSector3AudioLogHash))
                ReportDisasterEvidenceCollected();
        }

        private void CacheAudioLogSystem(IAudioLogRuntime audioLogSystem)
        {
            _audioLogs = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null;
        }

        private IAudioLogRuntime ResolveAudioLogSystem()
        {
            IAudioLogRuntime audioLogSystem = _audioLogs;
            if (IsAudioLogRuntimeUsable(audioLogSystem))
                return audioLogSystem;

            _audioLogs = null;
            return null;
        }

        private static bool IsAudioLogRuntimeUsable(IAudioLogRuntime audioLogSystem)
        {
            if (audioLogSystem == null)
                return false;

            if (audioLogSystem is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void TrySyncWorkerTagsFromNarrativeDiscoveryReadModel(INarrativeDiscoveryReadModel narrativeDiscovery)
        {
            if (!Application.isPlaying)
                return;

            if (narrativeDiscovery == null)
                return;

            if (narrativeDiscovery.HasDiscovery(ChenMSuitDiscoveryHash))
                ReportWorkerTagScannedHash(ChenMWorkerTagHash);
        }

        private void EnsureSubsystemsInitialized()
        {
            if (Telemetry == null)
                Telemetry = new Atlas6LiabilityTelemetry();

            if (DirectiveWeighting == null)
            {
                DirectiveWeighting = new DirectiveWeightingSystem(Telemetry);
                DirectiveWeighting.Initialize(1f);
            }

            if (ThermalSheer == null)
                ThermalSheer = new ThermalSheerManager();

            if (ActuarialLiability == null)
            {
                ActuarialLiability = new ActuarialLiabilitySystem(Telemetry);
                ActuarialLiability.Initialize(5000f);
            }

            if (ExtractionGating == null)
                ExtractionGating = new ExtractionGatingSystem(Telemetry);

            if (!Application.isPlaying || isActiveAndEnabled)
                WireSubsystemEvents();
        }

        private void WireSubsystemEvents()
        {
            if (!ReferenceEquals(_wiredActuarialLiability, ActuarialLiability))
            {
                if (_wiredActuarialLiability != null)
                    _wiredActuarialLiability.OnPlayerFlaggedAsActuarialThreat -= HandleActuarialThreat;

                _wiredActuarialLiability = ActuarialLiability;
                if (_wiredActuarialLiability != null)
                    _wiredActuarialLiability.OnPlayerFlaggedAsActuarialThreat += HandleActuarialThreat;
            }

            if (!ReferenceEquals(_wiredExtractionGating, ExtractionGating))
            {
                if (_wiredExtractionGating != null)
                    _wiredExtractionGating.OnTetherSeveredSatoRen -= HandleSatoRenSeverance;

                _wiredExtractionGating = ExtractionGating;
                if (_wiredExtractionGating != null)
                    _wiredExtractionGating.OnTetherSeveredSatoRen += HandleSatoRenSeverance;
            }
        }

        private void UnwireSubsystemEvents()
        {
            if (_wiredActuarialLiability != null)
                _wiredActuarialLiability.OnPlayerFlaggedAsActuarialThreat -= HandleActuarialThreat;

            if (_wiredExtractionGating != null)
                _wiredExtractionGating.OnTetherSeveredSatoRen -= HandleSatoRenSeverance;

            _wiredActuarialLiability = null;
            _wiredExtractionGating = null;
        }

        private void SanitizeSectorXenonOmegaYield()
        {
            if (!math.isfinite(sectorXenonOmegaYield))
            {
                sectorXenonOmegaYield = 0f;
                RecordTelemetry(
                    Atlas6LiabilityEventCode.InvalidXenonOmegaYieldReported,
                    Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.ManagerContextHash,
                    faultFlags: Atlas6LiabilityFaultFlags.NonFiniteInput);
                return;
            }

            if (sectorXenonOmegaYield < 0f)
            {
                RecordTelemetry(
                    Atlas6LiabilityEventCode.InvalidXenonOmegaYieldReported,
                    Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.ManagerContextHash,
                    value0: sectorXenonOmegaYield,
                    faultFlags: Atlas6LiabilityFaultFlags.InvalidRangeInput);
                sectorXenonOmegaYield = 0f;
                return;
            }

            if (sectorXenonOmegaYield > MaximumTrackedSectorXenonOmegaYield)
            {
                RecordTelemetry(
                    Atlas6LiabilityEventCode.InvalidXenonOmegaYieldReported,
                    Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.ManagerContextHash,
                    value0: sectorXenonOmegaYield,
                    value1: MaximumTrackedSectorXenonOmegaYield,
                    faultFlags: Atlas6LiabilityFaultFlags.InvalidRangeInput);
                sectorXenonOmegaYield = MaximumTrackedSectorXenonOmegaYield;
            }
        }

        private bool TryRegisterActiveRuntimeInstance()
        {
            if (!Application.isPlaying)
                return true;

            Atlas6CorporateLiabilityManager activeRuntime = ActiveRuntimeInstance;
            if (activeRuntime != null && !ReferenceEquals(activeRuntime, this))
            {
                enabled = false;
                Destroy(this);
                return false;
            }

            ActiveRuntimeInstance = this;
            return true;
        }

        private void TryUnregisterActiveRuntimeInstance()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }
    }
}
