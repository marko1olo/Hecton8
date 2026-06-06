using UnityEngine;
using System;
using Hecton8.Core;

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
        [Header("Corporate Directive Settings")]
        [SerializeField] private float sectorXenonOmegaYield = 0f;
        [SerializeField] private float playerDistanceToPrimaryDrillSite = 200f;
        [SerializeField] private bool hasDisasterEvidenceInInventory = false;

        // Core Sub-systems
        public DirectiveWeightingSystem DirectiveWeighting { get; private set; }
        public ThermalSheerManager ThermalSheer { get; private set; }
        public ActuarialLiabilitySystem ActuarialLiability { get; private set; }
        public ExtractionGatingSystem ExtractionGating { get; private set; }

        [Header("System State")]
        [SerializeField] private Atlas6ThreatLevel currentThreatLevel = Atlas6ThreatLevel.Nominal;

        public event Action<Atlas6ThreatLevel> OnThreatLevelChanged;

        private bool _isRegistered;
        private bool _registeredHotSwapListener;

        private void Awake()
        {
            DirectiveWeighting = new DirectiveWeightingSystem();
            ThermalSheer = new ThermalSheerManager();
            ActuarialLiability = new ActuarialLiabilitySystem();
            ExtractionGating = new ExtractionGatingSystem();

            // Initialize baseline states
            DirectiveWeighting.Initialize(1.0f);
            ActuarialLiability.Initialize(5000f); // starting corporate credit
        }

        private void OnEnable()
        {
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
                OnThreatLevelChanged?.Invoke(currentThreatLevel);
            }
        }

        // --- PUBLIC API EXPOSED TO OTHER HECTON-8 SYSTEMS ---

        public void ReportXenonOmegaExtracted(float amount)
        {
            sectorXenonOmegaYield += amount;
        }

        public void ReportWorkerTagScanned(string workerId)
        {
            ActuarialLiability.RegisterWorkerTagRecovery(workerId);
        }

        public void ReportDisasterEvidenceCollected()
        {
            hasDisasterEvidenceInInventory = true;
            Debug.Log("[ATLAS-6] Covert scan complete. Contractor possesses unauthorized documentation.");
        }

        public void ReportDisasterEvidenceDiscarded()
        {
            hasDisasterEvidenceInInventory = false;
            Debug.Log("[ATLAS-6] Contractor compliance noted. Unauthorized documentation purged from inventory.");
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
                if (ExtractionGating.BiomatterExposureLevel > 15f)
                {
                    Debug.LogWarning("[ATLAS-6] Dynamic Haldane Quarantine check failed. Exposure exceeded limits during tether. Locking out.");
                    ExtractionGating.AttemptBoardingSequence(); // This will trigger the lockout event
                }
            }
        }

        private void HandleActuarialThreat()
        {
            Debug.Log("[ATLAS-6 Master] Actuarial threat threshold reached. Coordinating defense protocols.");
            // In a full game hook, this would signal drones, turrets, and nav meshes.
        }

        private void HandleSatoRenSeverance()
        {
            Debug.Log("[ATLAS-6 Master] Sato-Ren protocol executed. Disavowing contractor.");
            // In a full game hook, this would trigger lore events, audio logs, failing lights.
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
                    Debug.Log("[ATLAS-6] Liability Manager Hooked into Global Registry.");
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
    }
}
