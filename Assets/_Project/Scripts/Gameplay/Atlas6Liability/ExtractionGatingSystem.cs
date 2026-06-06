using UnityEngine;
using System;
using Unity.Mathematics;

namespace Hecton8.Gameplay.Atlas6Liability
{
    public enum ExtractionCarrierState
    {
        Offline,
        TetherRequested,
        TetherEstablished,
        TetherSevered,
        Extracting
    }

    /// <summary>
    /// Haldane & Sato-Ren Protocols: Extraction Gating & Silence Filter
    /// Extraction is blocked by Xenon-Omega Biomatter Exposure (Haldane Quarantine).
    /// If truth evidence is attached to the extraction request, the tether is severed (Sato-Ren Silence).
    /// Pure Xenon-Omega proof is required to even call the carrier.
    /// </summary>
    public class ExtractionGatingSystem
    {
        public ExtractionCarrierState CarrierState { get; private set; } = ExtractionCarrierState.Offline;
        public bool IsHaldaneLockoutActive { get; private set; }
        
        // Biomatter radiation-like stat
        public float BiomatterExposureLevel { get; private set; } = 0f;
        public float LockoutExposureThreshold => _lockoutExposureThreshold;

        private readonly float _minimumPureYieldRequired = 500f;
        private readonly float _fatalExposureThreshold = 100f;
        private readonly float _lockoutExposureThreshold = 15f; // Cannot extract if above this
        private readonly Atlas6LiabilityTelemetry _telemetry;
        private bool _haldaneLockoutRaised;

        public event Action OnTetherSeveredSatoRen;
        public event Action OnQuarantineLockoutHaldane;
        public event Action OnCarrierArrived;
        public event Action<float> OnDecontaminationProcessed;

        public ExtractionGatingSystem(Atlas6LiabilityTelemetry telemetry = null)
        {
            _telemetry = telemetry;
        }

        public void AddBiomatterExposure(float amount)
        {
            bool invalidExposureAmount = !math.isfinite(amount) || amount <= 0f;
            if (invalidExposureAmount)
            {
                _telemetry?.Record(
                    Atlas6LiabilityEventCode.InvalidBiomatterExposureReported,
                    Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.ExtractionContextHash,
                    value0: amount,
                    carrierState: CarrierState,
                    faultFlags: math.isfinite(amount)
                        ? Atlas6LiabilityFaultFlags.InvalidRangeInput
                        : Atlas6LiabilityFaultFlags.NonFiniteInput);
                return;
            }

            BiomatterExposureLevel = Mathf.Clamp(
                BiomatterExposureLevel + amount,
                0f,
                _fatalExposureThreshold);
        }

        public void ProcessMakeshiftDecontamination(float cleansingPower)
        {
            bool invalidCleansingPower = !math.isfinite(cleansingPower) || cleansingPower <= 0f;
            if (invalidCleansingPower)
            {
                _telemetry?.Record(
                    Atlas6LiabilityEventCode.InvalidDecontaminationReported,
                    Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.ExtractionContextHash,
                    value0: cleansingPower,
                    value1: BiomatterExposureLevel,
                    carrierState: CarrierState,
                    faultFlags: math.isfinite(cleansingPower)
                        ? Atlas6LiabilityFaultFlags.InvalidRangeInput
                        : Atlas6LiabilityFaultFlags.NonFiniteInput);
                return;
            }

            BiomatterExposureLevel = Mathf.Max(BiomatterExposureLevel - cleansingPower, 0f);
            if (BiomatterExposureLevel <= _lockoutExposureThreshold)
            {
                IsHaldaneLockoutActive = false;
                _haldaneLockoutRaised = false;
            }

            _telemetry?.Record(
                Atlas6LiabilityEventCode.DecontaminationProcessed,
                Atlas6LiabilityEventSeverity.Info,
                Atlas6LiabilityTelemetry.ExtractionContextHash,
                value0: BiomatterExposureLevel,
                carrierState: CarrierState);
            OnDecontaminationProcessed?.Invoke(BiomatterExposureLevel);
        }

        /// <summary>
        /// Attempt to request the Black Keel extraction carrier.
        /// </summary>
        public bool RequestExtractionTether(float pureYieldOffered, bool isTransmittingDisasterEvidence)
        {
            if (!math.isfinite(pureYieldOffered))
            {
                _telemetry?.Record(
                    Atlas6LiabilityEventCode.TetherDeniedInsufficientYield,
                    Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.ExtractionContextHash,
                    value0: pureYieldOffered,
                    value1: _minimumPureYieldRequired,
                    carrierState: CarrierState,
                    faultFlags: Atlas6LiabilityFaultFlags.NonFiniteInput);
                return false;
            }

            if (CarrierState == ExtractionCarrierState.TetherSevered)
            {
                _telemetry?.Record(
                    Atlas6LiabilityEventCode.TetherDeniedPreviouslySevered,
                    Atlas6LiabilityEventSeverity.Critical,
                    Atlas6LiabilityTelemetry.ExtractionContextHash,
                    value0: pureYieldOffered,
                    carrierState: CarrierState,
                    faultFlags: Atlas6LiabilityFaultFlags.CarrierStateRejected);
                return false;
            }

            if (pureYieldOffered < _minimumPureYieldRequired)
            {
                _telemetry?.Record(
                    Atlas6LiabilityEventCode.TetherDeniedInsufficientYield,
                    Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.ExtractionContextHash,
                    value0: pureYieldOffered,
                    value1: _minimumPureYieldRequired,
                    carrierState: CarrierState,
                    faultFlags: pureYieldOffered < 0f
                        ? Atlas6LiabilityFaultFlags.InvalidRangeInput
                        : Atlas6LiabilityFaultFlags.None);
                return false;
            }

            // Sato-Ren Silence Filter
            if (isTransmittingDisasterEvidence)
            {
                CarrierState = ExtractionCarrierState.TetherSevered;
                _telemetry?.Record(
                    Atlas6LiabilityEventCode.TetherSeveredSatoRen,
                    Atlas6LiabilityEventSeverity.Critical,
                    Atlas6LiabilityTelemetry.ExtractionContextHash,
                    value0: pureYieldOffered,
                    carrierState: CarrierState,
                    faultFlags: Atlas6LiabilityFaultFlags.EventConsumerNotified);
                OnTetherSeveredSatoRen?.Invoke();
                return false;
            }

            // Tether successfully requested
            CarrierState = ExtractionCarrierState.TetherRequested;
            _telemetry?.Record(
                Atlas6LiabilityEventCode.TetherRequested,
                Atlas6LiabilityEventSeverity.Info,
                Atlas6LiabilityTelemetry.ExtractionContextHash,
                value0: pureYieldOffered,
                carrierState: CarrierState);
            return true;
        }

        /// <summary>
        /// Call this when the player actually reaches the staging lock airlock.
        /// </summary>
        public bool AttemptBoardingSequence()
        {
            if (CarrierState != ExtractionCarrierState.TetherRequested && CarrierState != ExtractionCarrierState.TetherEstablished)
            {
                _telemetry?.Record(
                    Atlas6LiabilityEventCode.BoardingDeniedInvalidCarrierState,
                    Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.ExtractionContextHash,
                    carrierState: CarrierState,
                    faultFlags: Atlas6LiabilityFaultFlags.CarrierStateRejected);
                return false;
            }

            // Haldane Quarantine Hold
            if (BiomatterExposureLevel > _lockoutExposureThreshold)
            {
                return RaiseHaldaneLockout();
            }

            CarrierState = ExtractionCarrierState.Extracting;
            IsHaldaneLockoutActive = false;
            _haldaneLockoutRaised = false;
            _telemetry?.Record(
                Atlas6LiabilityEventCode.CarrierArrived,
                Atlas6LiabilityEventSeverity.Info,
                Atlas6LiabilityTelemetry.ExtractionContextHash,
                value0: BiomatterExposureLevel,
                carrierState: CarrierState);
            OnCarrierArrived?.Invoke();
            return true;
        }

        private bool RaiseHaldaneLockout()
        {
            IsHaldaneLockoutActive = true;
            if (_haldaneLockoutRaised)
            {
                _telemetry?.Record(
                    Atlas6LiabilityEventCode.HaldaneLockoutRaised,
                    Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.ExtractionContextHash,
                    value0: BiomatterExposureLevel,
                    carrierState: CarrierState,
                    faultFlags: Atlas6LiabilityFaultFlags.RepeatedFaultSuppressed);
                return false;
            }

            _haldaneLockoutRaised = true;
            _telemetry?.Record(
                Atlas6LiabilityEventCode.HaldaneLockoutRaised,
                Atlas6LiabilityEventSeverity.Critical,
                Atlas6LiabilityTelemetry.ExtractionContextHash,
                value0: BiomatterExposureLevel,
                value1: _lockoutExposureThreshold,
                carrierState: CarrierState,
                faultFlags: Atlas6LiabilityFaultFlags.EventConsumerNotified);
            OnQuarantineLockoutHaldane?.Invoke();
            return false; // Player is trapped
        }
    }
}
