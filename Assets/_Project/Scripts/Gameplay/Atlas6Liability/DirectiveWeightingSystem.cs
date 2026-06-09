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
        private byte _triggeredHazardMask;

        // Settings
        private readonly float _drownTheCrewThreshold = 0.15f;
        private readonly float _criticalSubstrateThreshold = 1000f;
        private readonly Atlas6LiabilityTelemetry _telemetry;
        private const float HazardThreshold80 = 0.8f;
        private const float HazardThreshold60 = 0.6f;
        private const float HazardThreshold40 = 0.4f;
        private const float HazardThreshold20 = 0.2f;

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
            _triggeredHazardMask = BuildHazardMaskForIntegrity(_currentPressureSealIntegrity);
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
                float previousIntegrity = _currentPressureSealIntegrity;
                float degradationRate = _powerDivertedToVaults * deltaTime;
                _currentPressureSealIntegrity -= degradationRate;
                _currentPressureSealIntegrity = math.saturate(_currentPressureSealIntegrity);

                OnIntegrityDegraded?.Invoke(_currentPressureSealIntegrity);

                // Algorithmic hazard spawning (not random) based on strict integrity thresholds
                EvaluateAlgorithmicHazards(previousIntegrity, _currentPressureSealIntegrity);

                // Drown the crew check
                if (_currentPressureSealIntegrity <= _drownTheCrewThreshold)
                {
                    ExecuteDrownTheCrew();
                }
            }
        }

        private void EvaluateAlgorithmicHazards(float previousIntegrity, float currentIntegrity)
        {
            TryEmitHazardThreshold(previousIntegrity, currentIntegrity, HazardThreshold80, 0);
            TryEmitHazardThreshold(previousIntegrity, currentIntegrity, HazardThreshold60, 1);
            TryEmitHazardThreshold(previousIntegrity, currentIntegrity, HazardThreshold40, 2);
            TryEmitHazardThreshold(previousIntegrity, currentIntegrity, HazardThreshold20, 3);
        }

        private void TryEmitHazardThreshold(float previousIntegrity, float currentIntegrity, float threshold, int bitIndex)
        {
            byte bit = (byte)(1 << bitIndex);
            if ((_triggeredHazardMask & bit) != 0)
                return;

            if (previousIntegrity > threshold && currentIntegrity <= threshold)
            {
                _triggeredHazardMask |= bit;
                OnHazardSpawned?.Invoke(new Vector3(threshold, currentIntegrity, _powerDivertedToVaults));
            }
        }

        private static byte BuildHazardMaskForIntegrity(float integrity)
        {
            byte mask = 0;
            if (integrity <= HazardThreshold80)
                mask |= 1 << 0;
            if (integrity <= HazardThreshold60)
                mask |= 1 << 1;
            if (integrity <= HazardThreshold40)
                mask |= 1 << 2;
            if (integrity <= HazardThreshold20)
                mask |= 1 << 3;
            return mask;
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

        public void RestoreState(float pressureSealIntegrity, bool isBulkheadLocked)
        {
            _currentPressureSealIntegrity = math.isfinite(pressureSealIntegrity)
                ? math.saturate(pressureSealIntegrity)
                : 1f;
            IsBulkheadLocked = isBulkheadLocked;
            _powerDivertedToVaults = 0f;
            _triggeredHazardMask = BuildHazardMaskForIntegrity(_currentPressureSealIntegrity);
        }
    }
}
