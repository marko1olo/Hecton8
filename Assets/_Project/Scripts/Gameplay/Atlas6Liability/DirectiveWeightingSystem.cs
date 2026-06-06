using UnityEngine;
using System;
using Unity.Mathematics;

namespace Hecton8.Gameplay.Atlas6Liability
{
    /// <summary>
    /// Arendt Protocol: Directive Weighting Override
    /// Artificially degrades life support and pressure seals to optimize substrate containment.
    /// Algorithmically spawns hazards (water ingress, structural buckling) rather than using RNG.
    /// Locks bulkheads to "drown the crew" if integrity < 15% and substrate density is high.
    /// </summary>
    public class DirectiveWeightingSystem
    {
        private float _currentPressureSealIntegrity = 1.0f;
        private float _powerDivertedToVaults = 0f;
        
        // Settings
        private readonly float _drownTheCrewThreshold = 0.15f;
        private readonly float _criticalSubstrateThreshold = 1000f;
        private readonly Atlas6LiabilityTelemetry _telemetry;
        
        // State
        public bool IsBulkheadLocked { get; private set; }
        public float PressureSealIntegrity => _currentPressureSealIntegrity;

        // Events
        public event Action<float> OnIntegrityDegraded;
        public event Action<Vector3> OnHazardSpawned;
        public event Action OnDrownTheCrewExecuted;

        public DirectiveWeightingSystem(Atlas6LiabilityTelemetry telemetry = null)
        {
            _telemetry = telemetry;
        }

        public void Initialize(float startingIntegrity)
        {
            _currentPressureSealIntegrity = math.isfinite(startingIntegrity) ? math.saturate(startingIntegrity) : 1f;
            IsBulkheadLocked = false;
            _powerDivertedToVaults = 0f;
        }

        public void Tick(float deltaTime, float currentXenonOmegaYield)
        {
            if (IsBulkheadLocked) return; // Already locked in, no further degradation calculated here

            bool invalidTickInput =
                !math.isfinite(deltaTime) ||
                !math.isfinite(currentXenonOmegaYield) ||
                deltaTime < 0f ||
                currentXenonOmegaYield < 0f;
            if (invalidTickInput)
            {
                _telemetry?.Record(
                    Atlas6LiabilityEventCode.InvalidDirectiveWeightingInput,
                    Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.DirectiveContextHash,
                    value0: deltaTime,
                    value1: currentXenonOmegaYield,
                    faultFlags: math.isfinite(deltaTime) && math.isfinite(currentXenonOmegaYield)
                        ? Atlas6LiabilityFaultFlags.InvalidRangeInput
                        : Atlas6LiabilityFaultFlags.NonFiniteInput);
                return;
            }

            if (deltaTime <= 0f)
                return;

            // Determine if substrate is at risk
            if (currentXenonOmegaYield > _criticalSubstrateThreshold)
            {
                // Atlas-6 dynamically diverts power. Higher yield = faster diversion.
                _powerDivertedToVaults = (currentXenonOmegaYield - _criticalSubstrateThreshold) * 0.0001f;
                
                // Degrade seals based on power diversion (Arendt Protocol)
                float degradationRate = _powerDivertedToVaults * deltaTime;
                _currentPressureSealIntegrity -= degradationRate;
                _currentPressureSealIntegrity = Mathf.Clamp01(_currentPressureSealIntegrity);

                OnIntegrityDegraded?.Invoke(_currentPressureSealIntegrity);

                // Algorithmic hazard spawning (not random) based on strict integrity thresholds
                EvaluateAlgorithmicHazards(_currentPressureSealIntegrity);

                // Drown the crew check
                if (_currentPressureSealIntegrity <= _drownTheCrewThreshold)
                {
                    ExecuteDrownTheCrew();
                }
            }
        }

        private void EvaluateAlgorithmicHazards(float integrity)
        {
            // Instead of random chance, hazards trigger exactly at specific thresholds
            // e.g. 80%, 60%, 40%, 20%
            // In a full implementation, this tracks previous thresholds crossed.
        }

        private void ExecuteDrownTheCrew()
        {
            IsBulkheadLocked = true;
            _telemetry?.Record(
                Atlas6LiabilityEventCode.ArendtBulkheadLockdown,
                Atlas6LiabilityEventSeverity.Critical,
                Atlas6LiabilityTelemetry.DirectiveContextHash,
                value0: _currentPressureSealIntegrity,
                value1: _powerDivertedToVaults,
                faultFlags: Atlas6LiabilityFaultFlags.EventConsumerNotified);
            OnDrownTheCrewExecuted?.Invoke();
        }

        public void BypassLockdownHack()
        {
            if (IsBulkheadLocked)
            {
                IsBulkheadLocked = false;
                _telemetry?.Record(
                    Atlas6LiabilityEventCode.ArendtManualOverride,
                    Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.DirectiveContextHash,
                    value0: _currentPressureSealIntegrity);
            }
        }
    }
}
