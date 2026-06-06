using UnityEngine;
using System;
using Hecton8.Core;
using Hecton8.AtlasSignal;
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
    public sealed class Atlas6CorporateLiabilityManager : MonoBehaviour, IUpdatable, IGlobalRegistryHotSwapListener
    {
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

        private bool _isRegistered;
        private bool _registeredHotSwapListener;
        private bool _actuarialThreatPublished;
        private bool _satoRenSeverancePublished;

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
        }

        private void OnEnable()
        {
            if (!TryRegisterActiveRuntimeInstance())
                return;

            TryRegisterHotSwapListener();
            RegisterWithGlobalRegistry();
            
            // Wire up internal events
            if (ActuarialLiability != null) ActuarialLiability.OnPlayerFlaggedAsActuarialThreat += HandleActuarialThreat;
            if (ExtractionGating != null) ExtractionGating.OnTetherSeveredSatoRen += HandleSatoRenSeverance;
        }

        private void OnDisable()
        {
            UnregisterFromGlobalRegistry();
            TryUnregisterHotSwapListener();
            
            if (ActuarialLiability != null) ActuarialLiability.OnPlayerFlaggedAsActuarialThreat -= HandleActuarialThreat;
            if (ExtractionGating != null) ExtractionGating.OnTetherSeveredSatoRen -= HandleSatoRenSeverance;

            TryUnregisterActiveRuntimeInstance();
        }

        public void Tick(float deltaTime)
        {
            if (!_isRegistered) return;

            EvaluateThreatLevel();
            ApplyArendtDirectiveWeighting(deltaTime);
            ValidateExtractionGating();
        }

        private void EvaluateThreatLevel()
        {
            Atlas6ThreatLevel newLevel = Atlas6ThreatLevel.Nominal;

            if (ActuarialLiability.IsPlayerActuarialThreat)
                newLevel = Atlas6ThreatLevel.ActuarialLiability;
            else if (sectorXenonOmegaYield > 1000f)
                newLevel = Atlas6ThreatLevel.SubstrateAtRisk;

            if (DirectiveWeighting.PressureSealIntegrity < 0.15f)
                newLevel = Atlas6ThreatLevel.TotalLockdown;

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

            sectorXenonOmegaYield += amount;
            RecordTelemetry(
                Atlas6LiabilityEventCode.XenonOmegaYieldReported,
                Atlas6LiabilityEventSeverity.Info,
                Atlas6LiabilityTelemetry.ManagerContextHash,
                value0: sectorXenonOmegaYield,
                value1: amount);
        }

        public void ReportWorkerTagScanned(string workerId)
        {
            string safeWorkerId = string.IsNullOrWhiteSpace(workerId) ? "UNREADABLE" : workerId;
            ActuarialLiability.RegisterWorkerTagRecovery(safeWorkerId);
        }

        public void ReportGhostPDADataUploaded(float dataSizeInMegabytes)
        {
            ActuarialLiability.UploadGhostPDAData(dataSizeInMegabytes);
        }

        public void ReportDisasterEvidenceCollected()
        {
            hasDisasterEvidenceInInventory = true;
            RecordTelemetry(
                Atlas6LiabilityEventCode.DisasterEvidenceCollected,
                Atlas6LiabilityEventSeverity.Warning,
                Atlas6LiabilityTelemetry.ExtractionContextHash);
        }

        public void ReportDisasterEvidenceDiscarded()
        {
            hasDisasterEvidenceInInventory = false;
            RecordTelemetry(
                Atlas6LiabilityEventCode.DisasterEvidenceDiscarded,
                Atlas6LiabilityEventSeverity.Info,
                Atlas6LiabilityTelemetry.ExtractionContextHash);
        }

        public ThermalSheerManager.TelemetryReadout GetSubmarineOSReadout(float trueSheer)
        {
            return ThermalSheer.CalculateTelemetry(trueSheer, playerDistanceToPrimaryDrillSite);
        }

        public bool AttemptCarrierTether()
        {
            return ExtractionGating.RequestExtractionTether(sectorXenonOmegaYield, hasDisasterEvidenceInInventory);
        }

        public bool BoardBlackKeel()
        {
            return ExtractionGating.AttemptBoardingSequence();
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
            }
        }

        private void RegisterWithGlobalRegistry()
        {
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
