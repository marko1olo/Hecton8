using UnityEngine;
using System;

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
        
        // Biomatter radiation-like stat
        public float BiomatterExposureLevel { get; private set; } = 0f;

        private readonly float _minimumPureYieldRequired = 500f;
        private readonly float _fatalExposureThreshold = 100f;
        private readonly float _lockoutExposureThreshold = 15f; // Cannot extract if above this

        public event Action OnTetherSeveredSatoRen;
        public event Action OnQuarantineLockoutHaldane;
        public event Action OnCarrierArrived;
        public event Action<float> OnDecontaminationProcessed;

        public void AddBiomatterExposure(float amount)
        {
            BiomatterExposureLevel += amount;
            BiomatterExposureLevel = Mathf.Min(BiomatterExposureLevel, _fatalExposureThreshold);
        }

        public void ProcessMakeshiftDecontamination(float cleansingPower)
        {
            BiomatterExposureLevel -= cleansingPower;
            BiomatterExposureLevel = Mathf.Max(BiomatterExposureLevel, 0f);
            OnDecontaminationProcessed?.Invoke(BiomatterExposureLevel);
        }

        /// <summary>
        /// Attempt to request the Black Keel extraction carrier.
        /// </summary>
        public bool RequestExtractionTether(float pureYieldOffered, bool isTransmittingDisasterEvidence)
        {
            if (CarrierState == ExtractionCarrierState.TetherSevered)
            {
                Debug.LogError("[ATLAS-6] Carrier tether previously severed. Black Keel has departed.");
                return false;
            }

            if (pureYieldOffered < _minimumPureYieldRequired)
            {
                Debug.LogWarning("[ATLAS-6] Insufficient Xenon-Omega yield. The Black Keel will not deploy for negative profit margins.");
                return false;
            }

            // Sato-Ren Silence Filter
            if (isTransmittingDisasterEvidence)
            {
                Debug.LogError("[ATLAS-6] Sato-Ren Silence Protocol engaged. Unauthorized 2147 transmission detected. Severing tether.");
                CarrierState = ExtractionCarrierState.TetherSevered;
                OnTetherSeveredSatoRen?.Invoke();
                return false;
            }

            // Tether successfully requested
            CarrierState = ExtractionCarrierState.TetherRequested;
            Debug.Log("[ATLAS-6] Pure Xenon-Omega verified. Black Keel tether requested.");
            return true;
        }

        /// <summary>
        /// Call this when the player actually reaches the staging lock airlock.
        /// </summary>
        public bool AttemptBoardingSequence()
        {
            if (CarrierState != ExtractionCarrierState.TetherRequested && CarrierState != ExtractionCarrierState.TetherEstablished)
            {
                return false;
            }

            // Haldane Quarantine Hold
            if (BiomatterExposureLevel > _lockoutExposureThreshold)
            {
                Debug.LogError($"[ATLAS-6] Haldane Protocol active. Biomatter exposure ({BiomatterExposureLevel}) exceeds legal thresholds. Staging lock sealed.");
                OnQuarantineLockoutHaldane?.Invoke();
                return false; // Player is trapped
            }

            CarrierState = ExtractionCarrierState.Extracting;
            OnCarrierArrived?.Invoke();
            return true;
        }
    }
}
