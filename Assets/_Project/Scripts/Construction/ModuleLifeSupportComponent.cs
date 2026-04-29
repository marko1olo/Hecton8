using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.Construction
{
    internal struct ModuleLifeSupportSignals
    {
        public bool AirQualityWarningRaised;
        public bool AirReserveDepletedRaised;
        public bool Co2CriticalRaised;
    }

    /// <summary>
    /// Runtime life-support state extracted from BaseModule. Keeps breathable reserve, stale-air behavior,
    /// and CO2 accumulation localized without changing BaseModule save ownership.
    /// </summary>
    [System.Serializable]
    internal sealed class ModuleLifeSupportComponent
    {
        private const float ToxicCo2ThresholdNormalized = 0.75f;

        private float _oxygenRefillRate;
        private float _breathableReserveCapacity;
        private float _breathableReserve;
        private float _airRecycleRate;
        private float _occupiedAirDrainRate;
        private float _staleAirThreshold;
        private float _staleAirMinRefillScale;
        private float _staleAirSuitDrainRate;
        private float _co2Capacity;
        private float _co2Level;
        private float _co2GenerationRate;
        private float _co2CriticalThreshold;
        private bool _airReserveWarningLatched;
        private bool _airReserveDepletedLatched;
        private bool _co2CriticalLatched;

        public float AirReserveNormalized => _breathableReserveCapacity > 0.01f ? Mathf.Clamp01(_breathableReserve / _breathableReserveCapacity) : 1f;
        public bool IsAirQualityLow => AirReserveNormalized <= _staleAirThreshold;
        public float Co2Normalized => _co2Capacity > 0.01f ? Mathf.Clamp01(_co2Level / _co2Capacity) : 0f;
        public bool IsCo2Critical => _co2Level >= _co2CriticalThreshold;
        public bool IsCo2Toxic => Co2Normalized >= ToxicCo2ThresholdNormalized;
        public float ToxicHazardIntensity => Mathf.InverseLerp(ToxicCo2ThresholdNormalized, 1f, Co2Normalized);
        public float BreathableReserve => _breathableReserve;
        public float BreathableReserveCapacity => _breathableReserveCapacity;
        public float Co2Level => _co2Level;
        public float Co2Capacity => _co2Capacity;

        public void Configure(
            float oxygenRefillRate,
            float breathableReserveCapacity,
            float breathableReserve,
            float airRecycleRate,
            float occupiedAirDrainRate,
            float staleAirThreshold,
            float staleAirMinRefillScale,
            float staleAirSuitDrainRate,
            float co2Capacity,
            float co2Level,
            float co2GenerationRate,
            float co2CriticalThreshold)
        {
            _oxygenRefillRate = Mathf.Max(0f, oxygenRefillRate);
            _breathableReserveCapacity = Mathf.Max(1f, breathableReserveCapacity);
            _breathableReserve = breathableReserve;
            _airRecycleRate = Mathf.Max(0f, airRecycleRate);
            _occupiedAirDrainRate = Mathf.Max(0f, occupiedAirDrainRate);
            _staleAirThreshold = Mathf.Clamp(staleAirThreshold, 0.05f, 0.8f);
            _staleAirMinRefillScale = Mathf.Clamp01(staleAirMinRefillScale);
            _staleAirSuitDrainRate = Mathf.Max(0f, staleAirSuitDrainRate);
            _co2Capacity = Mathf.Max(1f, co2Capacity);
            _co2Level = Mathf.Max(0f, co2Level);
            _co2GenerationRate = Mathf.Max(0f, co2GenerationRate);
            _co2CriticalThreshold = Mathf.Clamp(co2CriticalThreshold, 0.05f, _co2Capacity);
            InitializeCold();
        }

        public void InitializeCold()
        {
            if (_breathableReserve <= 0f)
                _breathableReserve = _breathableReserveCapacity;

            _breathableReserve = Mathf.Clamp(_breathableReserve, 0f, _breathableReserveCapacity);
            _co2Level = Mathf.Clamp(_co2Level, 0f, _co2Capacity);
            _airReserveWarningLatched = IsAirQualityLow;
            _airReserveDepletedLatched = _breathableReserve <= 0f;
            _co2CriticalLatched = IsCo2Critical;
        }

        public void RestoreState(float airReserveNormalized, float co2Normalized)
        {
            _breathableReserve = Mathf.Clamp01(airReserveNormalized) * _breathableReserveCapacity;
            _co2Level = Mathf.Clamp01(co2Normalized) * _co2Capacity;
            _airReserveWarningLatched = IsAirQualityLow;
            _airReserveDepletedLatched = _breathableReserve <= 0f;
            _co2CriticalLatched = IsCo2Critical;
        }

        public void ResetForDespawn()
        {
            _breathableReserve = _breathableReserveCapacity;
            _co2Level = 0f;
            _airReserveWarningLatched = false;
            _airReserveDepletedLatched = false;
            _co2CriticalLatched = false;
        }

        public void ApplyCascadeFailureEffects(
            HectonSurvivalSystem trackedPlayerSurvival,
            BaseModuleFailureMode failureMode,
            float oxygenLeakDrainRate,
            float fireSuitDamageRate,
            float fireSuitEnergyDrainRate,
            float dt)
        {
            if (trackedPlayerSurvival == null)
                return;

            switch (failureMode)
            {
                case BaseModuleFailureMode.OxygenLeak:
                    if (oxygenLeakDrainRate > 0f)
                        trackedPlayerSurvival.DrainOxygen(oxygenLeakDrainRate * dt);
                    break;
                case BaseModuleFailureMode.Fire:
                    if (fireSuitDamageRate > 0f)
                        trackedPlayerSurvival.TakeDamage(fireSuitDamageRate * dt);
                    if (fireSuitEnergyDrainRate > 0f)
                        trackedPlayerSurvival.DrainEnergy(fireSuitEnergyDrainRate * dt);
                    break;
            }
        }

        public ModuleLifeSupportSignals Tick(
            float dt,
            bool dryCompartment,
            bool hasOperationalPower,
            HectonSurvivalSystem trackedPlayerSurvival)
        {
            ModuleLifeSupportSignals signals = default;

            if (dryCompartment &&
                hasOperationalPower &&
                !IsCo2Critical &&
                _airRecycleRate > 0f &&
                _breathableReserve < _breathableReserveCapacity)
            {
                _breathableReserve += _airRecycleRate * dt;
                if (_breathableReserve > _breathableReserveCapacity)
                    _breathableReserve = _breathableReserveCapacity;
            }

            if (!dryCompartment)
            {
                if (_co2GenerationRate > 0f)
                    AccumulateCo2(_co2GenerationRate * dt);
            }
            else if (!hasOperationalPower && _airRecycleRate > 0f)
            {
                AccumulateCo2(_airRecycleRate * dt);
            }

            if (trackedPlayerSurvival != null && dryCompartment)
            {
                if (_occupiedAirDrainRate > 0f)
                {
                    _breathableReserve -= _occupiedAirDrainRate * dt;
                    if (_breathableReserve < 0f)
                        _breathableReserve = 0f;
                }

                float co2AccumulationRate = 0f;
                if (_co2GenerationRate > 0f)
                    co2AccumulationRate += _co2GenerationRate;

                if (co2AccumulationRate > 0f)
                    AccumulateCo2(co2AccumulationRate * dt);

                if (_breathableReserve > 0f && !IsCo2Critical)
                {
                    float refillScale = ResolveAirRefillScale();
                    if (refillScale > 0f && _oxygenRefillRate > 0f)
                        trackedPlayerSurvival.RefillOxygen(_oxygenRefillRate * refillScale * dt);
                }
                else if (_staleAirSuitDrainRate > 0f)
                {
                    trackedPlayerSurvival.DrainOxygen(_staleAirSuitDrainRate * dt);
                }
            }

            if (IsAirQualityLow && !_airReserveWarningLatched)
            {
                _airReserveWarningLatched = true;
                signals.AirQualityWarningRaised = true;
            }
            else if (!IsAirQualityLow && _airReserveWarningLatched && AirReserveNormalized > _staleAirThreshold + 0.15f)
            {
                _airReserveWarningLatched = false;
            }

            bool depleted = _breathableReserve <= 0f;
            if (depleted && !_airReserveDepletedLatched)
            {
                _airReserveDepletedLatched = true;
                signals.AirReserveDepletedRaised = true;
            }
            else if (!depleted && _airReserveDepletedLatched && AirReserveNormalized > 0.2f)
            {
                _airReserveDepletedLatched = false;
            }

            if (IsCo2Critical && !_co2CriticalLatched)
            {
                _co2CriticalLatched = true;
                signals.Co2CriticalRaised = true;
            }
            else if (!IsCo2Critical && _co2CriticalLatched && Co2Normalized < 0.8f)
            {
                _co2CriticalLatched = false;
            }

            return signals;
        }

        public void ScrubCo2(float amount)
        {
            if (amount <= 0f)
                return;

            _co2Level -= amount;
            if (_co2Level < 0f)
                _co2Level = 0f;

            _breathableReserve += amount;
            if (_breathableReserve > _breathableReserveCapacity)
                _breathableReserve = _breathableReserveCapacity;
        }

        public void ApplyFloodExposure(float normalizedFloodDelta, float co2Amplifier)
        {
            if (normalizedFloodDelta <= 0f)
                return;

            float floodDelta = Mathf.Max(0f, normalizedFloodDelta);
            _breathableReserve -= _breathableReserveCapacity * floodDelta;
            if (_breathableReserve < 0f)
                _breathableReserve = 0f;

            _co2Level += _co2Capacity * floodDelta * Mathf.Max(0f, co2Amplifier);
            if (_co2Level > _co2Capacity)
                _co2Level = _co2Capacity;
        }

        public void CollapseBreathableReserve()
        {
            _breathableReserve = 0f;
            _co2Level = Mathf.Max(_co2Level, _co2CriticalThreshold);
            if (_co2Level > _co2Capacity)
                _co2Level = _co2Capacity;

            _airReserveWarningLatched = true;
            _airReserveDepletedLatched = true;
            _co2CriticalLatched = IsCo2Critical;
        }

        private void AccumulateCo2(float amount)
        {
            if (amount <= 0f)
                return;

            _co2Level += amount;
            if (_co2Level > _co2Capacity)
                _co2Level = _co2Capacity;
        }

        public string BuildAirReserveSummary()
        {
            return string.Format(
                "Breathable reserve down to {0:0}% inside the dry shelter loop. Scrubber support is no longer keeping pace with occupancy.",
                AirReserveNormalized * 100f);
        }

        public string BuildCo2CriticalSummary()
        {
            return string.Format(
                "CO2 saturation reached {0:0}% of scrubber capacity. Mechanical circulation is no longer restoring breathable air without botanical conversion.",
                Co2Normalized * 100f);
        }

        private float ResolveAirRefillScale()
        {
            float airQuality = AirReserveNormalized;
            if (airQuality >= _staleAirThreshold)
                return 1f;

            if (airQuality <= 0f || _staleAirThreshold <= 0.01f)
                return _staleAirMinRefillScale;

            return Mathf.Lerp(_staleAirMinRefillScale, 1f, airQuality / _staleAirThreshold);
        }
    }
}
