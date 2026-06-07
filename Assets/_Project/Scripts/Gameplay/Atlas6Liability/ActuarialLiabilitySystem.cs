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
        private const int RecoveredWorkerTagHashCapacity = 1024;
        private static readonly uint UnreadableWorkerTagHash = Atlas6LiabilityTelemetry.ComputeStableHash("UNREADABLE");

        public int RecoveredWorkerTags { get; private set; }
        public float CorporateHostilityIndex { get; private set; }
        public float CorporateCreditBalance { get; private set; }
        private bool _actuarialThreatRaised;
        private readonly Atlas6LiabilityTelemetry _telemetry;
        private readonly uint[] _recoveredWorkerTagHashes;
        private int _recoveredWorkerTagHashCount;

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
            _recoveredWorkerTagHashes = new uint[RecoveredWorkerTagHashCapacity]; // COLD ALLOC: uint[1024] - recovered worker tag dedupe table, covers Atlas-6 casualty count - owner: ActuarialLiabilitySystem
        }

        public void Initialize(float startingCredit)
        {
            RecoveredWorkerTags = 0;
            CorporateHostilityIndex = 0f;
            CorporateCreditBalance = math.isfinite(startingCredit) ? math.max(0f, startingCredit) : 0f;
            _actuarialThreatRaised = false;
            Array.Clear(_recoveredWorkerTagHashes, 0, _recoveredWorkerTagHashCount);
            _recoveredWorkerTagHashCount = 0;
        }

        /// <summary>
        /// Called when the player scans a corpse ID.
        /// The water owns the claim; recovering it angers Atlas-6.
        /// </summary>
        public bool RegisterWorkerTagRecovery(string workerId)
        {
            if (string.IsNullOrWhiteSpace(workerId))
                return RegisterWorkerTagRecoveryHash(UnreadableWorkerTagHash);

            return RegisterWorkerTagRecoveryHash(Atlas6LiabilityTelemetry.ComputeStableHash(workerId));
        }

        public bool RegisterWorkerTagRecoveryHash(uint workerTagHash)
        {
            if (workerTagHash == 0u)
                workerTagHash = UnreadableWorkerTagHash;

            if (HasRecoveredWorkerTag(workerTagHash))
                return false;

            if (_recoveredWorkerTagHashCount >= _recoveredWorkerTagHashes.Length)
            {
                _telemetry?.Record(
                    Atlas6LiabilityEventCode.WorkerTagRecovered,
                    Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.ActuarialContextHash,
                    subjectHash: workerTagHash,
                    value0: RecoveredWorkerTags,
                    value1: CorporateHostilityIndex,
                    faultFlags: Atlas6LiabilityFaultFlags.InvalidRangeInput);
                return false;
            }

            _recoveredWorkerTagHashes[_recoveredWorkerTagHashCount++] = workerTagHash;
            RecoveredWorkerTags++;

            // Base hostility increase
            CorporateHostilityIndex += 15.5f;
            _telemetry?.Record(
                Atlas6LiabilityEventCode.WorkerTagRecovered,
                Atlas6LiabilityEventSeverity.Warning,
                Atlas6LiabilityTelemetry.ActuarialContextHash,
                subjectHash: workerTagHash,
                value0: RecoveredWorkerTags,
                value1: CorporateHostilityIndex);

            OnCorporateHostilityIncreased?.Invoke(CorporateHostilityIndex);

            EvaluateActuarialThreatStatus();
            return true;
        }

        public int CopyRecoveredWorkerTagHashesTo(uint[] destination, int maxCount)
        {
            if (destination == null || maxCount <= 0)
                return 0;

            int safeCount = math.min(
                _recoveredWorkerTagHashCount,
                math.min(maxCount, destination.Length));
            for (int i = 0; i < safeCount; i++)
                destination[i] = _recoveredWorkerTagHashes[i];

            return safeCount;
        }

        public void RestoreState(
            float corporateCreditBalance,
            float corporateHostilityIndex,
            uint[] recoveredWorkerTagHashes,
            int recoveredWorkerTagCount)
        {
            CorporateCreditBalance = math.isfinite(corporateCreditBalance)
                ? math.max(0f, corporateCreditBalance)
                : 0f;
            CorporateHostilityIndex = math.isfinite(corporateHostilityIndex)
                ? math.max(0f, corporateHostilityIndex)
                : 0f;
            RecoveredWorkerTags = 0;
            Array.Clear(_recoveredWorkerTagHashes, 0, _recoveredWorkerTagHashCount);
            _recoveredWorkerTagHashCount = 0;

            int sourceLength = recoveredWorkerTagHashes != null ? recoveredWorkerTagHashes.Length : 0;
            int safeCount = math.clamp(
                recoveredWorkerTagCount,
                0,
                math.min(sourceLength, _recoveredWorkerTagHashes.Length));
            for (int i = 0; i < safeCount; i++)
            {
                uint workerTagHash = recoveredWorkerTagHashes[i];
                if (workerTagHash == 0u || HasRecoveredWorkerTag(workerTagHash))
                    continue;

                _recoveredWorkerTagHashes[_recoveredWorkerTagHashCount++] = workerTagHash;
                RecoveredWorkerTags++;
            }

            _actuarialThreatRaised = IsPlayerActuarialThreat;
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
            if (!math.isfinite(deduction))
            {
                _telemetry?.Record(
                    Atlas6LiabilityEventCode.InvalidGhostPDADataReported,
                    Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.ActuarialContextHash,
                    value0: dataSizeInMegabytes,
                    value1: CorporateCreditBalance,
                    faultFlags: Atlas6LiabilityFaultFlags.InvalidRangeInput);
                return;
            }

            float safeBalance = math.isfinite(CorporateCreditBalance)
                ? math.max(0f, CorporateCreditBalance)
                : 0f;
            float appliedDeduction = math.min(safeBalance, deduction);
            CorporateCreditBalance = math.max(0f, safeBalance - appliedDeduction);

            _telemetry?.Record(
                Atlas6LiabilityEventCode.CorporateCreditDeducted,
                Atlas6LiabilityEventSeverity.Critical,
                Atlas6LiabilityTelemetry.ActuarialContextHash,
                value0: appliedDeduction,
                value1: CorporateCreditBalance);
            OnCorporateCreditDeducted?.Invoke(appliedDeduction);
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

        private bool HasRecoveredWorkerTag(uint workerTagHash)
        {
            for (int i = 0; i < _recoveredWorkerTagHashCount; i++)
            {
                if (_recoveredWorkerTagHashes[i] == workerTagHash)
                    return true;
            }

            return false;
        }
    }
}
