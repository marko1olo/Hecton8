using UnityEngine;
using System;
using Unity.Mathematics;

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
        private bool _actuarialThreatRaised;
        private readonly Atlas6LiabilityTelemetry _telemetry;
        
        // Thresholds
        private readonly int _actuarialThreatThreshold = 5; // 5 tags recovered makes you a threat

        // Events
        public event Action<float> OnCorporateHostilityIncreased;
        public event Action<float> OnCorporateCreditDeducted;
        public event Action OnPlayerFlaggedAsActuarialThreat;
        public event Action OnDroneRepairCyclesHalted;

        public bool IsPlayerActuarialThreat => RecoveredWorkerTags >= _actuarialThreatThreshold;

        public ActuarialLiabilitySystem(Atlas6LiabilityTelemetry telemetry = null)
        {
            _telemetry = telemetry;
        }

        public void Initialize(float startingCredit)
        {
            RecoveredWorkerTags = 0;
            CorporateHostilityIndex = 0f;
            CorporateCreditBalance = math.isfinite(startingCredit) ? math.max(0f, startingCredit) : 0f;
            _actuarialThreatRaised = false;
        }

        /// <summary>
        /// Called when the player scans a corpse ID.
        /// The water owns the claim; recovering it angers Atlas-6.
        /// </summary>
        public void RegisterWorkerTagRecovery(string workerId)
        {
            if (string.IsNullOrWhiteSpace(workerId))
                workerId = "UNREADABLE";

            RecoveredWorkerTags++;
            
            // Base hostility increase
            CorporateHostilityIndex += 15.5f;
            _telemetry?.Record(
                Atlas6LiabilityEventCode.WorkerTagRecovered,
                Atlas6LiabilityEventSeverity.Warning,
                Atlas6LiabilityTelemetry.ActuarialContextHash,
                subjectHash: Atlas6LiabilityTelemetry.ComputeStableHash(workerId),
                value0: RecoveredWorkerTags,
                value1: CorporateHostilityIndex);
            
            OnCorporateHostilityIncreased?.Invoke(CorporateHostilityIndex);

            EvaluateActuarialThreatStatus();
        }

        /// <summary>
        /// Ghost data provides blueprints, but uploading it to the network fines the player.
        /// </summary>
        public void UploadGhostPDAData(float dataSizeInMegabytes)
        {
            bool invalidDataSize = !math.isfinite(dataSizeInMegabytes) || dataSizeInMegabytes <= 0f;
            if (invalidDataSize)
            {
                _telemetry?.Record(
                    Atlas6LiabilityEventCode.InvalidGhostPDADataReported,
                    Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.ActuarialContextHash,
                    value0: dataSizeInMegabytes,
                    value1: CorporateCreditBalance,
                    faultFlags: math.isfinite(dataSizeInMegabytes)
                        ? Atlas6LiabilityFaultFlags.InvalidRangeInput
                        : Atlas6LiabilityFaultFlags.NonFiniteInput);
                return;
            }

            // Corporate penalty: 50 credits per MB of "corrupted" historical data
            float deduction = dataSizeInMegabytes * 50f;
            CorporateCreditBalance -= deduction;
            
            _telemetry?.Record(
                Atlas6LiabilityEventCode.CorporateCreditDeducted,
                Atlas6LiabilityEventSeverity.Critical,
                Atlas6LiabilityTelemetry.ActuarialContextHash,
                value0: deduction,
                value1: CorporateCreditBalance);
            OnCorporateCreditDeducted?.Invoke(deduction);
        }

        private void EvaluateActuarialThreatStatus()
        {
            if (!IsPlayerActuarialThreat || _actuarialThreatRaised)
                return;

            _actuarialThreatRaised = true;

            _telemetry?.Record(
                Atlas6LiabilityEventCode.ActuarialThreatRaised,
                Atlas6LiabilityEventSeverity.Critical,
                Atlas6LiabilityTelemetry.ActuarialContextHash,
                value0: RecoveredWorkerTags,
                value1: CorporateHostilityIndex,
                faultFlags: Atlas6LiabilityFaultFlags.EventConsumerNotified);

            OnPlayerFlaggedAsActuarialThreat?.Invoke();
            OnDroneRepairCyclesHalted?.Invoke();
        }
    }
}
