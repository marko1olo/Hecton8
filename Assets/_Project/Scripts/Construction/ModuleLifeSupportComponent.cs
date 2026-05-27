using System;
using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    internal struct ModuleLifeSupportSignals
    {
        public byte AirQualityWarningRaised;
        public byte AirReserveDepletedRaised;
        public byte Co2CriticalRaised;
        public byte Co2HypoxiaRaised;
    }

    /// <summary>
    /// Runtime life-support state extracted from BaseModule. Keeps breathable reserve, stale-air behavior,
    /// and CO2 accumulation localized without changing BaseModule save ownership.
    /// </summary>
    [System.Serializable]
    internal sealed class ModuleLifeSupportComponent
    {
        private const float ToxicCo2ThresholdNormalized = 0.75f;
        private const float HypoxiaCo2ThresholdNormalized = 0.80f;
        private const float MinimumLifeSupportCapacity = 1f;
        private const float MinimumRatioDenominator = 0.01f;
        private const float PressureCompressionMinScale = 0.1f;
        private const float PressureCompressionMaxScale = 1f;
        private const string AirReserveSummaryPrefix = "Breathable reserve down to ";
        private const string AirReserveSummarySuffix = "% inside the dry shelter loop. Scrubber support is no longer keeping pace with occupancy.";
        private const string Co2CriticalSummaryPrefix = "CO2 saturation reached ";
        private const string Co2CriticalSummarySuffix = "% of scrubber capacity. Mechanical circulation is no longer restoring breathable air without botanical conversion.";
        private const float FireSuitBurnStatusDurationSeconds = 0.35f;
        private const float FireSuitBurnMagnitudeScale = 0.25f;

        private float _oxygenRefillRate;
        private float _baseBreathableReserveCapacity;
        private float _breathableReserveCapacity;
        private float _breathableReserve;
        private float _airRecycleRate;
        private float _occupiedAirDrainRate;
        private float _staleAirThreshold;
        private float _staleAirMinRefillScale;
        private float _staleAirSuitDrainRate;
        private float _baseCo2Capacity;
        private float _co2Capacity;
        private float _co2Level;
        private float _co2GenerationRate;
        private float _co2CriticalThreshold;
        private float _baseCo2CriticalThreshold;
        private float _pressureCompressionVolumeScale = 1f;
        private bool _airReserveWarningLatched;
        private bool _airReserveDepletedLatched;
        private bool _co2CriticalLatched;
        private bool _co2HypoxiaLatched;

        public float AirReserveNormalized => _breathableReserveCapacity > MinimumRatioDenominator
            ? math.saturate(FiniteNonNegativeOrZero(_breathableReserve) / _breathableReserveCapacity)
            : 1f;
        public bool IsAirQualityLow => AirReserveNormalized <= _staleAirThreshold;
        public float Co2Normalized => _co2Capacity > MinimumRatioDenominator
            ? math.saturate(FiniteNonNegativeOrZero(_co2Level) / _co2Capacity)
            : 0f;
        public bool IsCo2Critical => _co2Level >= _co2CriticalThreshold;
        public bool IsCo2Toxic => Co2Normalized >= ToxicCo2ThresholdNormalized;
        public float ToxicHazardIntensity => math.saturate((Co2Normalized - ToxicCo2ThresholdNormalized) / (1f - ToxicCo2ThresholdNormalized));
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
            _oxygenRefillRate = FiniteNonNegativeOrZero(oxygenRefillRate);
            _baseBreathableReserveCapacity = math.max(MinimumLifeSupportCapacity, FiniteNonNegativeOrZero(breathableReserveCapacity));
            _breathableReserveCapacity = _baseBreathableReserveCapacity;
            _breathableReserve = math.isfinite(breathableReserve) ? breathableReserve : _breathableReserveCapacity;
            _airRecycleRate = FiniteNonNegativeOrZero(airRecycleRate);
            _occupiedAirDrainRate = FiniteNonNegativeOrZero(occupiedAirDrainRate);
            _staleAirThreshold = math.clamp(FiniteOr(staleAirThreshold, 0.2f), 0.05f, 0.8f);
            _staleAirMinRefillScale = math.saturate(FiniteNonNegativeOrZero(staleAirMinRefillScale));
            _staleAirSuitDrainRate = FiniteNonNegativeOrZero(staleAirSuitDrainRate);
            _baseCo2Capacity = math.max(MinimumLifeSupportCapacity, FiniteNonNegativeOrZero(co2Capacity));
            _co2Capacity = _baseCo2Capacity;
            _co2Level = FiniteNonNegativeOrZero(co2Level);
            _co2GenerationRate = FiniteNonNegativeOrZero(co2GenerationRate);
            _baseCo2CriticalThreshold = math.clamp(FiniteOr(co2CriticalThreshold, _baseCo2Capacity * 0.8f), 0.05f, _baseCo2Capacity);
            _co2CriticalThreshold = _baseCo2CriticalThreshold;
            _pressureCompressionVolumeScale = 1f;
            InitializeCold();
        }

        public void InitializeCold()
        {
            _breathableReserveCapacity = math.max(MinimumLifeSupportCapacity, FiniteNonNegativeOrZero(_breathableReserveCapacity));
            _co2Capacity = math.max(MinimumLifeSupportCapacity, FiniteNonNegativeOrZero(_co2Capacity));
            _co2CriticalThreshold = math.clamp(FiniteOr(_co2CriticalThreshold, _co2Capacity * HypoxiaCo2ThresholdNormalized), 0.05f, _co2Capacity);

            if (!math.isfinite(_breathableReserve) || _breathableReserve <= 0f)
                _breathableReserve = _breathableReserveCapacity;

            _breathableReserve = math.clamp(_breathableReserve, 0f, _breathableReserveCapacity);
            _co2Level = math.clamp(FiniteNonNegativeOrZero(_co2Level), 0f, _co2Capacity);
            _airReserveWarningLatched = IsAirQualityLow;
            _airReserveDepletedLatched = _breathableReserve <= 0f;
            _co2CriticalLatched = IsCo2Critical;
            _co2HypoxiaLatched = Co2Normalized >= HypoxiaCo2ThresholdNormalized;
        }

        public void RestoreState(float airReserveNormalized, float co2Normalized)
        {
            _breathableReserve = math.saturate(FiniteNonNegativeOrZero(airReserveNormalized)) * _breathableReserveCapacity;
            _co2Level = math.saturate(FiniteNonNegativeOrZero(co2Normalized)) * _co2Capacity;
            _airReserveWarningLatched = IsAirQualityLow;
            _airReserveDepletedLatched = _breathableReserve <= 0f;
            _co2CriticalLatched = IsCo2Critical;
            _co2HypoxiaLatched = Co2Normalized >= HypoxiaCo2ThresholdNormalized;
        }

        /// <summary>
        /// Scales breathable reserve and CO2 capacity by current pressure-compressed room volume.
        /// </summary>
        /// <param name="volumeScale">Normalized room volume scale in [0.1, 1.0].</param>
        public void ApplyPressureCompressionScale(float volumeScale)
        {
            float sanitizedScale = math.clamp(FiniteOr(volumeScale, PressureCompressionMaxScale), PressureCompressionMinScale, PressureCompressionMaxScale);
            if (math.abs(_pressureCompressionVolumeScale - sanitizedScale) <= 0.00001f)
                return;

            _pressureCompressionVolumeScale = sanitizedScale;
            _breathableReserveCapacity = math.max(MinimumLifeSupportCapacity, FiniteNonNegativeOrZero(_baseBreathableReserveCapacity) * sanitizedScale);
            _co2Capacity = math.max(MinimumLifeSupportCapacity, FiniteNonNegativeOrZero(_baseCo2Capacity) * sanitizedScale);
            _co2CriticalThreshold = math.clamp(FiniteNonNegativeOrZero(_baseCo2CriticalThreshold) * sanitizedScale, 0.05f, _co2Capacity);

            if (_breathableReserve > _breathableReserveCapacity)
                _breathableReserve = _breathableReserveCapacity;

            if (_co2Level > _co2Capacity)
                _co2Level = _co2Capacity;

            _airReserveWarningLatched = IsAirQualityLow;
            _airReserveDepletedLatched = _breathableReserve <= 0f;
            _co2CriticalLatched = IsCo2Critical;
            _co2HypoxiaLatched = Co2Normalized >= HypoxiaCo2ThresholdNormalized;
        }

        public void ResetForDespawn()
        {
            ApplyPressureCompressionScale(1f);
            _breathableReserve = _breathableReserveCapacity;
            _co2Level = 0f;
            _airReserveWarningLatched = false;
            _airReserveDepletedLatched = false;
            _co2CriticalLatched = false;
            _co2HypoxiaLatched = false;
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

            float safeDt = FiniteNonNegativeOrZero(dt);
            if (safeDt <= 0f)
                return;

            switch (failureMode)
            {
                case BaseModuleFailureMode.OxygenLeak:
                    if (oxygenLeakDrainRate > 0f)
                        trackedPlayerSurvival.DrainOxygen(FiniteNonNegativeOrZero(oxygenLeakDrainRate) * safeDt);
                    break;
                case BaseModuleFailureMode.Fire:
                    if (fireSuitDamageRate > 0f)
                        QueueFireSuitBurnStatus(trackedPlayerSurvival, FiniteNonNegativeOrZero(fireSuitDamageRate) * safeDt);
                    if (fireSuitEnergyDrainRate > 0f)
                        trackedPlayerSurvival.DrainEnergy(FiniteNonNegativeOrZero(fireSuitEnergyDrainRate) * safeDt);
                    break;
            }
        }

        private static void QueueFireSuitBurnStatus(HectonSurvivalSystem trackedPlayerSurvival, float damageAmount)
        {
            if (trackedPlayerSurvival == null)
                return;

            float safeDamage = FiniteNonNegativeOrZero(damageAmount);
            if (safeDamage <= 0f)
                return;

            int targetId = CaptureSurvivalCombatTargetId(trackedPlayerSurvival);
            if (targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))
                return;

            CombatDamageRuntime.TryQueueStatusEffect(
                targetId,
                CombatStatusBits.Burning64,
                FireSuitBurnStatusDurationSeconds,
                DamageSourceIds.EnvironmentHazard,
                math.saturate(safeDamage * FireSuitBurnMagnitudeScale));
        }

        private static int CaptureSurvivalCombatTargetId(HectonSurvivalSystem trackedPlayerSurvival)
        {
            if (trackedPlayerSurvival.TryGetComponent(out HectonPlayerHealth playerHealth))
                return CombatDamageRuntime.ResolveTargetId(playerHealth.gameObject);

            return CombatDamageRuntime.ResolveTargetId(trackedPlayerSurvival.gameObject);
        }

        public ModuleLifeSupportSignals Tick(
            float dt,
            bool dryCompartment,
            bool hasOperationalPower,
            float powerSupplyRatio,
            HectonSurvivalSystem trackedPlayerSurvival)
        {
            ModuleLifeSupportSignals signals = default;
            float safeDt = FiniteNonNegativeOrZero(dt);
            if (safeDt <= 0f)
                return signals;

            float sanitizedSupplyRatio = hasOperationalPower ? math.saturate(FiniteNonNegativeOrZero(powerSupplyRatio)) : 0f;

            if (dryCompartment &&
                hasOperationalPower &&
                !IsCo2Critical &&
                _airRecycleRate > 0f &&
                _breathableReserve < _breathableReserveCapacity &&
                sanitizedSupplyRatio > 0f)
            {
                _breathableReserve += _airRecycleRate * sanitizedSupplyRatio * safeDt;
                if (_breathableReserve > _breathableReserveCapacity)
                    _breathableReserve = _breathableReserveCapacity;
            }

            if (!dryCompartment)
            {
                if (_co2GenerationRate > 0f)
                    AccumulateCo2(_co2GenerationRate * safeDt);
            }
            else if (!hasOperationalPower && _airRecycleRate > 0f)
            {
                AccumulateCo2(_airRecycleRate * safeDt);
            }

            if (trackedPlayerSurvival != null && dryCompartment)
            {
                if (_occupiedAirDrainRate > 0f)
                {
                    _breathableReserve -= _occupiedAirDrainRate * safeDt;
                    if (_breathableReserve < 0f)
                        _breathableReserve = 0f;
                }

                float co2AccumulationRate = 0f;
                if (_co2GenerationRate > 0f)
                    co2AccumulationRate += _co2GenerationRate;

                if (co2AccumulationRate > 0f)
                    AccumulateCo2(co2AccumulationRate * safeDt);

                if (_breathableReserve > 0f && !IsCo2Critical)
                {
                    float refillScale = ResolveAirRefillScale();
                    if (refillScale > 0f && _oxygenRefillRate > 0f)
                        trackedPlayerSurvival.RefillOxygen(_oxygenRefillRate * refillScale * sanitizedSupplyRatio * safeDt);
                }
                else if (_staleAirSuitDrainRate > 0f)
                {
                    trackedPlayerSurvival.DrainOxygen(_staleAirSuitDrainRate * safeDt);
                }
            }

            if (IsAirQualityLow && !_airReserveWarningLatched)
            {
                _airReserveWarningLatched = true;
                signals.AirQualityWarningRaised = 1;
            }
            else if (!IsAirQualityLow && _airReserveWarningLatched && AirReserveNormalized > _staleAirThreshold + 0.15f)
            {
                _airReserveWarningLatched = false;
            }

            bool depleted = _breathableReserve <= 0f;
            if (depleted && !_airReserveDepletedLatched)
            {
                _airReserveDepletedLatched = true;
                signals.AirReserveDepletedRaised = 1;
            }
            else if (!depleted && _airReserveDepletedLatched && AirReserveNormalized > 0.2f)
            {
                _airReserveDepletedLatched = false;
            }

            if (IsCo2Critical && !_co2CriticalLatched)
            {
                _co2CriticalLatched = true;
                signals.Co2CriticalRaised = 1;
            }
            else if (!IsCo2Critical && _co2CriticalLatched && Co2Normalized < 0.8f)
            {
                _co2CriticalLatched = false;
            }

            bool hypoxia = Co2Normalized >= HypoxiaCo2ThresholdNormalized;
            if (hypoxia && !_co2HypoxiaLatched)
            {
                _co2HypoxiaLatched = true;
                signals.Co2HypoxiaRaised = 1;
            }
            else if (!hypoxia && _co2HypoxiaLatched && Co2Normalized < HypoxiaCo2ThresholdNormalized - 0.08f)
            {
                _co2HypoxiaLatched = false;
            }

            return signals;
        }

        public void ScrubCo2(float amount)
        {
            float safeAmount = FiniteNonNegativeOrZero(amount);
            if (safeAmount <= 0f)
                return;

            _co2Level -= safeAmount;
            if (_co2Level < 0f)
                _co2Level = 0f;

            _breathableReserve += safeAmount;
            if (_breathableReserve > _breathableReserveCapacity)
                _breathableReserve = _breathableReserveCapacity;
        }

        public void ApplyFloodExposure(float normalizedFloodDelta, float co2Amplifier)
        {
            float floodDelta = FiniteNonNegativeOrZero(normalizedFloodDelta);
            if (floodDelta <= 0f)
                return;

            _breathableReserve -= _breathableReserveCapacity * floodDelta;
            if (_breathableReserve < 0f)
                _breathableReserve = 0f;

            _co2Level += _co2Capacity * floodDelta * FiniteNonNegativeOrZero(co2Amplifier);
            if (_co2Level > _co2Capacity)
                _co2Level = _co2Capacity;
        }

        public void CollapseBreathableReserve()
        {
            _breathableReserve = 0f;
            _co2Level = math.max(FiniteNonNegativeOrZero(_co2Level), _co2CriticalThreshold);
            if (_co2Level > _co2Capacity)
                _co2Level = _co2Capacity;

            _airReserveWarningLatched = true;
            _airReserveDepletedLatched = true;
            _co2CriticalLatched = IsCo2Critical;
        }

        private void AccumulateCo2(float amount)
        {
            float safeAmount = FiniteNonNegativeOrZero(amount);
            if (safeAmount <= 0f)
                return;

            float normalizedLevel = _co2Capacity > MinimumRatioDenominator
                ? math.saturate((FiniteNonNegativeOrZero(_co2Level) + safeAmount) / _co2Capacity)
                : 0f;
            _co2Level = normalizedLevel * _co2Capacity;
        }

        public bool TryBuildAirReserveSummary(ref FixedCharBuffer buffer)
        {
            return TryBuildPercentSummary(
                ref buffer,
                AirReserveSummaryPrefix,
                math.clamp((int)math.round(AirReserveNormalized * 100f), 0, 999),
                AirReserveSummarySuffix);
        }

        public bool TryBuildCo2CriticalSummary(ref FixedCharBuffer buffer)
        {
            return TryBuildPercentSummary(
                ref buffer,
                Co2CriticalSummaryPrefix,
                math.clamp((int)math.round(Co2Normalized * 100f), 0, 999),
                Co2CriticalSummarySuffix);
        }

        private static bool TryBuildPercentSummary(ref FixedCharBuffer buffer, string prefix, int percent, string suffix)
        {
            return buffer.Append(prefix.AsSpan()) &&
                   buffer.AppendInt(percent) &&
                   buffer.Append(suffix.AsSpan());
        }

        private float ResolveAirRefillScale()
        {
            float airQuality = AirReserveNormalized;
            if (airQuality >= _staleAirThreshold)
                return 1f;

            if (airQuality <= 0f || _staleAirThreshold <= 0.01f)
                return _staleAirMinRefillScale;

            return math.lerp(_staleAirMinRefillScale, 1f, airQuality / _staleAirThreshold);
        }

        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static float FiniteNonNegativeOrZero(float value)
        {
            return math.isfinite(value) && value > 0f ? value : 0f;
        }
    }
}
