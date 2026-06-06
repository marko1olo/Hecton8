using UnityEngine;
using System;

namespace Hecton8.Gameplay.Atlas6Liability
{
    /// <summary>
    /// Ibarra Protocol: Actuarial Liability Deferment
    /// Suspends life insurance payouts by treating the 843 dead workers as "Unresolved System Load".
    /// Recovering bodies or uploading ghost PDA data increases Corporate Hostility.
    /// Drone repairs stop, and automated defenses flag the player as an "Actuarial Threat".
    /// </summary>
    public class ActuarialLiabilitySystem
    {
        public int RecoveredWorkerTags { get; private set; }
        public float CorporateHostilityIndex { get; private set; }
        public float CorporateCreditBalance { get; private set; }
        
        // Thresholds
        private readonly int _actuarialThreatThreshold = 5; // 5 tags recovered makes you a threat

        // Events
        public event Action<float> OnCorporateHostilityIncreased;
        public event Action<float> OnCorporateCreditDeducted;
        public event Action OnPlayerFlaggedAsActuarialThreat;
        public event Action OnDroneRepairCyclesHalted;

        public bool IsPlayerActuarialThreat => RecoveredWorkerTags >= _actuarialThreatThreshold;

        public void Initialize(float startingCredit)
        {
            RecoveredWorkerTags = 0;
            CorporateHostilityIndex = 0f;
            CorporateCreditBalance = startingCredit;
        }

        /// <summary>
        /// Called when the player scans a corpse ID.
        /// The water owns the claim; recovering it angers Atlas-6.
        /// </summary>
        public void RegisterWorkerTagRecovery(string workerId)
        {
            RecoveredWorkerTags++;
            
            // Base hostility increase
            CorporateHostilityIndex += 15.5f;
            Debug.LogWarning($"[ATLAS-6] Unresolved System Load anomaly detected. ID {workerId} is legally non-recoverable. Hostility increased.");
            
            OnCorporateHostilityIncreased?.Invoke(CorporateHostilityIndex);

            EvaluateActuarialThreatStatus();
        }

        /// <summary>
        /// Ghost data provides blueprints, but uploading it to the network fines the player.
        /// </summary>
        public void UploadGhostPDAData(float dataSizeInMegabytes)
        {
            // Corporate penalty: 50 credits per MB of "corrupted" historical data
            float deduction = dataSizeInMegabytes * 50f;
            CorporateCreditBalance -= deduction;
            
            Debug.LogError($"[ATLAS-6] Unauthorized historical payload uploaded. Corporate fine levied: -{deduction} Credits.");
            OnCorporateCreditDeducted?.Invoke(deduction);
        }

        private void EvaluateActuarialThreatStatus()
        {
            if (IsPlayerActuarialThreat)
            {
                // The player is now actively detrimental to the actuarial deferment strategy.
                Debug.LogError("[ATLAS-6] Contractor classified as Actuarial Threat. Halting drone repair routes. Engaging defense flags.");
                
                OnPlayerFlaggedAsActuarialThreat?.Invoke();
                OnDroneRepairCyclesHalted?.Invoke();
            }
        }
    }
}
