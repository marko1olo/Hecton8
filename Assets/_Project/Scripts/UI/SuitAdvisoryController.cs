using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Gameplay;
using TMPro;
using UnityEngine;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Suit Advisory Controller")]
    public sealed class SuitAdvisoryController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HectonSurvivalSystem survival;
        [SerializeField] private HUDNotification hudNotification;

        [Header("Thresholds")]
        [SerializeField, Range(0.05f, 0.8f)] private float oxygenWarningThreshold = 0.35f;
        [SerializeField, Range(0.05f, 0.5f)] private float oxygenCriticalThreshold = 0.15f;
        [SerializeField, Range(0.05f, 0.8f)] private float energyWarningThreshold = 0.25f;
        [SerializeField, Range(0.05f, 0.8f)] private float integrityWarningThreshold = 0.35f;
        [SerializeField, Range(0.05f, 0.5f)] private float integrityCriticalThreshold = 0.18f;
        [SerializeField, Range(0.05f, 1f)] private float coldWarningThreshold = 0.28f;
        [SerializeField, Range(0.05f, 1f)] private float coldCriticalThreshold = 0.62f;
        [SerializeField, Range(0.05f, 1f)] private float heatWarningThreshold = 0.28f;
        [SerializeField, Range(0.05f, 1f)] private float heatCriticalThreshold = 0.62f;
        [SerializeField] private float safeDepthWarningMargin = 20f;
        [SerializeField] private float safeDepthCriticalMargin = 6f;
        [SerializeField] private float resetHysteresis = 0.06f;

        [Header("Audio")]
        [SerializeField] private AudioClip warningClip;
        [SerializeField] private AudioClip criticalClip;
        [SerializeField, Range(0f, 1f)] private float uiVolume = 0.45f;

        private bool _oxygenWarned;
        private bool _oxygenCritical;
        private bool _energyWarned;
        private bool _integrityWarned;
        private bool _integrityCritical;
        private bool _depthWarned;
        private bool _depthCritical;
        private bool _coldWarned;
        private bool _coldCritical;
        private bool _heatWarned;
        private bool _heatCritical;
        private bool _bleedingWarned;
        private bool _fractureWarned;
        private bool _deathTriggered;

        private const string MsgOxygenWarning = "OXYGEN RESERVES LOW";
        private const string MsgOxygenCritical = "CRITICAL OXYGEN";
        private const string MsgEnergyWarning = "SUIT POWER LOW";
        private const string MsgIntegrityWarning = "SUIT INTEGRITY DEGRADED";
        private const string MsgIntegrityCritical = "SUIT INTEGRITY CRITICAL";
        private const string MsgDeath = "SUIT FAILURE";
        private const string MsgDeathOxygen = "FATALITY: OXYGEN DEPLETED";
        private const string MsgDeathPressure = "FATALITY: PRESSURE COLLAPSE";
        private const string MsgDeathThermal = "FATALITY: THERMAL FAILURE";
        private const string MsgDeathRadiation = "FATALITY: RADIATION EXPOSURE";
        private const string MsgDeathStarvation = "FATALITY: STARVATION";
        private const string MsgDeathDehydration = "FATALITY: DEHYDRATION";
        private const string MsgDeathIntegrity = "FATALITY: STRUCTURAL FAILURE";
        private const string MsgBaseBreach = "BASE BREACH DETECTED";
        private const string MsgBleeding = "BLEEDING DETECTED // HULL LOSS ACTIVE";
        private const string MsgFracture = "FRACTURE DETECTED // SWIM THRUST DEGRADED";
        private const string MsgColdWarning = "THERMAL LOAD RISING // HEATING DRAW ACTIVE";
        private const string MsgColdCritical = "EXTREME COLD // SUIT HEAT FAILING";
        private const string MsgHeatWarning = "HEAT LOAD RISING // HYDRATION LOSS ACTIVE";
        private const string MsgHeatCritical = "THERMAL OVERLOAD // BODY HEAT UNSAFE";

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            EvaluateAll();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void ResolveReferences()
        {
            if (survival == null)
            {
                survival = GetComponent<HectonSurvivalSystem>();

                if (survival == null &&
                    SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                    playerTransform != null)
                {
                    survival = playerTransform.GetComponent<HectonSurvivalSystem>();
                }
            }

            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);
        }

        private void Subscribe()
        {
            BaseIntegrityEvents.OnModuleBreached += HandleModuleBreached;
            BaseIntegrityEvents.OnModuleEmergency += HandleModuleEmergency;
            BaseIntegrityEvents.OnModuleAirQualityWarning += HandleModuleAirQualityWarning;

            if (survival == null)
                return;

            survival.OnOxygenChanged += HandleOxygenChanged;
            survival.OnEnergyChanged += HandleEnergyChanged;
            survival.OnIntegrityChanged += HandleIntegrityChanged;
            survival.OnDepthChanged += HandleDepthChanged;
            survival.OnTemperatureChanged += HandleTemperatureChanged;
            survival.ThermalStateChanged += HandleThermalStateChanged;
            survival.OnDeath += HandleDeath;
            survival.InjuryStateChanged += HandleInjuryStateChanged;
        }

        private void Unsubscribe()
        {
            if (survival != null)
            {
                survival.OnOxygenChanged -= HandleOxygenChanged;
                survival.OnEnergyChanged -= HandleEnergyChanged;
                survival.OnIntegrityChanged -= HandleIntegrityChanged;
                survival.OnDepthChanged -= HandleDepthChanged;
                survival.OnTemperatureChanged -= HandleTemperatureChanged;
                survival.ThermalStateChanged -= HandleThermalStateChanged;
                survival.OnDeath -= HandleDeath;
                survival.InjuryStateChanged -= HandleInjuryStateChanged;
            }

            BaseIntegrityEvents.OnModuleBreached -= HandleModuleBreached;
            BaseIntegrityEvents.OnModuleEmergency -= HandleModuleEmergency;
            BaseIntegrityEvents.OnModuleAirQualityWarning -= HandleModuleAirQualityWarning;
        }

        private void EvaluateAll()
        {
            if (survival == null)
                return;

            HandleOxygenChanged(survival.Oxygen);
            HandleEnergyChanged(survival.Energy);
            HandleIntegrityChanged(survival.Integrity);
            HandleDepthChanged(survival.Depth);
            HandleTemperatureChanged(survival.EnvironmentTemperature);
            HandleInjuryStateChanged();
        }

        private void HandleOxygenChanged(float _)
        {
            if (survival == null || survival.Stats == null)
                return;

            float normalized = survival.OxygenNormalized;

            if (!_oxygenCritical && normalized <= oxygenCriticalThreshold)
            {
                _oxygenCritical = true;
                NotifyCritical(MsgOxygenCritical);
            }
            else if (!_oxygenWarned && normalized <= oxygenWarningThreshold)
            {
                _oxygenWarned = true;
                NotifyWarning(MsgOxygenWarning);
            }

            if (normalized > oxygenWarningThreshold + resetHysteresis)
            {
                _oxygenWarned = false;
                _oxygenCritical = false;
            }
            else if (normalized > oxygenCriticalThreshold + resetHysteresis)
            {
                _oxygenCritical = false;
            }
        }

        private void HandleEnergyChanged(float _)
        {
            if (survival == null || survival.Stats == null)
                return;

            float normalized = survival.EnergyNormalized;
            if (!_energyWarned && normalized <= energyWarningThreshold)
            {
                _energyWarned = true;
                NotifyWarning(MsgEnergyWarning);
            }

            if (normalized > energyWarningThreshold + resetHysteresis)
                _energyWarned = false;
        }

        private void HandleIntegrityChanged(float _)
        {
            if (survival == null || survival.Stats == null)
                return;

            float normalized = survival.IntegrityNormalized;

            if (!_integrityCritical && normalized <= integrityCriticalThreshold)
            {
                _integrityCritical = true;
                NotifyCritical(MsgIntegrityCritical);
            }
            else if (!_integrityWarned && normalized <= integrityWarningThreshold)
            {
                _integrityWarned = true;
                NotifyWarning(MsgIntegrityWarning);
            }

            if (normalized > integrityWarningThreshold + resetHysteresis)
            {
                _integrityWarned = false;
                _integrityCritical = false;
            }
            else if (normalized > integrityCriticalThreshold + resetHysteresis)
            {
                _integrityCritical = false;
            }
        }

        private void HandleDepthChanged(float depth)
        {
            if (survival == null || survival.Stats == null)
                return;

            float remaining = survival.SafeDepthMarginMeters;

            if (!_depthCritical && remaining <= safeDepthCriticalMargin)
            {
                _depthCritical = true;
                NotifyCritical(BuildDepthCriticalMessage());
            }
            else if (!_depthWarned && remaining <= safeDepthWarningMargin)
            {
                _depthWarned = true;
                NotifyWarning(BuildDepthWarningMessage(remaining));
            }

            if (remaining > safeDepthWarningMargin + 5f)
            {
                _depthWarned = false;
                _depthCritical = false;
            }
            else if (remaining > safeDepthCriticalMargin + 3f)
            {
                _depthCritical = false;
            }
        }

        private void HandleTemperatureChanged(float _)
        {
            if (survival == null)
                return;

            EvaluateColdStress();
            EvaluateHeatStress();
        }

        private void HandleThermalStateChanged()
        {
            if (survival == null)
                return;

            EvaluateColdStress();
            EvaluateHeatStress();
        }

        private void HandleInjuryStateChanged()
        {
            if (survival == null)
                return;

            if (survival.IsBleeding)
            {
                if (!_bleedingWarned)
                {
                    _bleedingWarned = true;
                    NotifyCritical(BuildBleedingMessage());
                }
            }
            else
            {
                _bleedingWarned = false;
            }

            if (survival.HasFracture)
            {
                if (!_fractureWarned)
                {
                    _fractureWarned = true;
                    NotifyWarning(BuildFractureMessage());
                }
            }
            else
            {
                _fractureWarned = false;
            }
        }

        private void HandleDeath()
        {
            if (_deathTriggered)
                return;

            _deathTriggered = true;
            NotifyCritical(ResolveDeathMessage());

            if (survival != null && survival.TryGetLastDeathRecord(out SurvivalDeathRecord record))
            {
                NotifyWarning(BuildDeathAdvice(record.Cause));
                NotifyInfo(BuildDeathSummary(record));
            }
        }

        private void HandleModuleBreached()
        {
            NotifyCritical(MsgBaseBreach);
        }

        private void HandleModuleEmergency(BaseModuleFailureMode failureMode, float integrity)
        {
            NotifyWarning(BuildBaseEmergencyMessage(failureMode, integrity));
        }

        private void HandleModuleAirQualityWarning(float airQualityNormalized)
        {
            if (airQualityNormalized <= 0.12f)
            {
                NotifyCritical(BuildAirQualityMessage(airQualityNormalized));
                return;
            }

            NotifyWarning(BuildAirQualityMessage(airQualityNormalized));
        }

        private void NotifyWarning(string message)
        {
            hudNotification?.ShowWarning(message);
            PlayUiClip(warningClip);
        }

        private void NotifyCritical(string message)
        {
            hudNotification?.ShowCritical(message);
            PlayUiClip(criticalClip != null ? criticalClip : warningClip);
        }

        private void NotifyInfo(string message)
        {
            hudNotification?.ShowInfo(message);
        }

        private void PlayUiClip(AudioClip clip)
        {
            if (clip == null)
                return;

            Hecton8.Core.IAudioService audio = Hecton8.Core.GlobalRegistry.Audio;
            if (audio != null)
                audio.PlayStatic2D(clip, uiVolume);
        }

        private string ResolveDeathMessage()
        {
            if (survival == null)
                return MsgDeath;

            switch (survival.LastDeathCause)
            {
                case SurvivalDeathCause.OxygenDepletion:
                    return MsgDeathOxygen;
                case SurvivalDeathCause.PressureCollapse:
                    return MsgDeathPressure;
                case SurvivalDeathCause.ThermalFailure:
                    return MsgDeathThermal;
                case SurvivalDeathCause.RadiationExposure:
                    return MsgDeathRadiation;
                case SurvivalDeathCause.Starvation:
                    return MsgDeathStarvation;
                case SurvivalDeathCause.Dehydration:
                    return MsgDeathDehydration;
                case SurvivalDeathCause.IntegrityFailure:
                    return MsgDeathIntegrity;
                default:
                    return MsgDeath;
            }
        }

        private string BuildDeathAdvice(SurvivalDeathCause cause)
        {
            if (survival == null)
                return "SURVIVAL ADVICE: REBUILD YOUR SAFETY MARGIN BEFORE THE NEXT PUSH";

            return $"SURVIVAL ADVICE: {survival.GetDeathAdvice(cause).ToUpperInvariant()}";
        }

        private static string BuildDeathSummary(SurvivalDeathRecord record)
        {
            int totalSeconds = Mathf.Max(0, Mathf.RoundToInt((float)record.LifeDurationSeconds));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            return string.Format(
                "LAST RUN {0:00}:{1:00} // PEAK {2:0}M // O2 LOW {3:0}% // PWR LOW {4:0}% // HULL LOW {5:0}%",
                minutes,
                seconds,
                record.PeakDepthMeters,
                record.LowestOxygenNormalized * 100f,
                record.LowestEnergyNormalized * 100f,
                record.LowestIntegrityNormalized * 100f);
        }

        private static string BuildBaseEmergencyMessage(BaseModuleFailureMode failureMode, float integrity)
        {
            int integrityPercent = Mathf.Clamp(Mathf.RoundToInt(integrity * 100f), 0, 100);

            switch (failureMode)
            {
                case BaseModuleFailureMode.OxygenLeak:
                    return $"BASE OXYGEN LEAK // SHELTER UNSAFE // HULL {integrityPercent}%";
                case BaseModuleFailureMode.Fire:
                    return $"BASE FIRE // EVACUATE OR REPAIR // HULL {integrityPercent}%";
                case BaseModuleFailureMode.ShortCircuit:
                    return $"BASE SHORT CIRCUIT // POWER OFFLINE // HULL {integrityPercent}%";
                default:
                    return $"BASE EMERGENCY // HULL {integrityPercent}%";
            }
        }

        private static string BuildAirQualityMessage(float airQualityNormalized)
        {
            int reservePercent = Mathf.Clamp(Mathf.RoundToInt(airQualityNormalized * 100f), 0, 100);
            return $"BASE AIR QUALITY LOW // SCRUBBERS {reservePercent}%";
        }

        private void EvaluateColdStress()
        {
            float severity = survival != null ? survival.ColdStressSeverity01 : 0f;

            if (!_coldCritical && severity >= coldCriticalThreshold)
            {
                _coldCritical = true;
                NotifyCritical(BuildColdStressMessage(true));
            }
            else if (!_coldWarned && severity >= coldWarningThreshold)
            {
                _coldWarned = true;
                NotifyWarning(BuildColdStressMessage(false));
            }

            if (severity <= Mathf.Max(0f, coldWarningThreshold - resetHysteresis))
            {
                _coldWarned = false;
                _coldCritical = false;
            }
            else if (severity <= Mathf.Max(0f, coldCriticalThreshold - resetHysteresis))
            {
                _coldCritical = false;
            }
        }

        private void EvaluateHeatStress()
        {
            float severity = survival != null ? survival.HeatStressSeverity01 : 0f;

            if (!_heatCritical && severity >= heatCriticalThreshold)
            {
                _heatCritical = true;
                NotifyCritical(BuildHeatStressMessage(true));
            }
            else if (!_heatWarned && severity >= heatWarningThreshold)
            {
                _heatWarned = true;
                NotifyWarning(BuildHeatStressMessage(false));
            }

            if (severity <= Mathf.Max(0f, heatWarningThreshold - resetHysteresis))
            {
                _heatWarned = false;
                _heatCritical = false;
            }
            else if (severity <= Mathf.Max(0f, heatCriticalThreshold - resetHysteresis))
            {
                _heatCritical = false;
            }
        }

        private string BuildBleedingMessage()
        {
            if (survival == null)
                return MsgBleeding;

            return string.Format(
                "{0} // {1:0.0}/S",
                MsgBleeding,
                survival.BleedingSeverity01 * 1.8f);
        }

        private string BuildFractureMessage()
        {
            if (survival == null)
                return MsgFracture;

            int mobilityPercent = Mathf.Clamp(
                Mathf.RoundToInt((1f - survival.FracturePenalty01) * 100f),
                0,
                100);
            return $"{MsgFracture} // MOBILITY {mobilityPercent}%";
        }

        private string BuildColdStressMessage(bool critical)
        {
            if (survival == null)
                return critical ? MsgColdCritical : MsgColdWarning;

            string prefix = critical ? MsgColdCritical : MsgColdWarning;
            return string.Format(
                "{0} // {1:0}C // PWR {2:0}%",
                prefix,
                survival.EnvironmentTemperature,
                survival.EnergyPercent);
        }

        private string BuildHeatStressMessage(bool critical)
        {
            if (survival == null)
                return critical ? MsgHeatCritical : MsgHeatWarning;

            string prefix = critical ? MsgHeatCritical : MsgHeatWarning;
            return string.Format(
                "{0} // {1:0}C // HYD {2:0}%",
                prefix,
                survival.EnvironmentTemperature,
                survival.ThirstPercent);
        }

        private string BuildDepthWarningMessage(float safeDepthMarginMeters)
        {
            float displayedMargin = Mathf.Max(0f, safeDepthMarginMeters);
            return string.Format(
                "SAFE DEPTH WINDOW {0:0}M // SUIT RATING {1:0}M",
                displayedMargin,
                survival != null && survival.Stats != null ? survival.Stats.SafeDepth : 0f);
        }

        private string BuildDepthCriticalMessage()
        {
            if (survival == null)
                return "PRESSURE DAMAGE ACTIVE";

            return string.Format(
                "PRESSURE DAMAGE ACTIVE // +{0:0}M // HULL {1:0.0}/S",
                survival.OverpressureMeters,
                survival.PressureDamagePerSecond);
        }
    }
}
