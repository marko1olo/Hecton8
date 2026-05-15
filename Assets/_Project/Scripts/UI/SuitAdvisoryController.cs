using System;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Suit Advisory Controller")]
    public sealed class SuitAdvisoryController : MonoBehaviour, IBaseIntegrityEventListener, ILateFrameTickable
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
        private bool _registeredForSurvivalSignals;
        private uint _survivalSignalSourceId;
        private uint _lastSurvivalSignalSequence;
        private FixedCharBuffer _advisoryMessageBuffer = new FixedCharBuffer(192); // COLD ALLOC: char[192] - suit advisory notification staging buffer - owner: SuitAdvisoryController

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
            RefreshSurvivalSignalBinding();
            Subscribe();
            EvaluateAll();
            RegisterSurvivalSignalPump();
        }

        private void OnDisable()
        {
            Unsubscribe();
            UnregisterSurvivalSignalPump();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            UnregisterSurvivalSignalPump();
            BaseIntegrityEvents.AssertUnregistered(this, nameof(SuitAdvisoryController));
        }

        private void ResolveReferences()
        {
            if (survival == null)
            {
                survival = GetComponent<HectonSurvivalSystem>();

                if (survival == null &&
                    GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
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
            BaseIntegrityEvents.Register(this);
        }

        private void Unsubscribe()
        {
            BaseIntegrityEvents.Unregister(this);
        }

        /// <inheritdoc />
        public void OnBaseIntegrityEvent(in BaseIntegrityEventPayload payload)
        {
            switch ((BaseIntegrityEventType)payload.EventType)
            {
                case BaseIntegrityEventType.Breached:
                    HandleModuleBreached();
                    break;

                case BaseIntegrityEventType.Emergency:
                    HandleModuleEmergency((BaseModuleFailureMode)payload.FailureMode, payload.Value);
                    break;

                case BaseIntegrityEventType.AirQualityWarning:
                    HandleModuleAirQualityWarning(payload.Value);
                    break;
            }
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
                _advisoryMessageBuffer.Clear();
                AppendDepthCriticalMessage(ref _advisoryMessageBuffer);
                NotifyCritical(in _advisoryMessageBuffer);
            }
            else if (!_depthWarned && remaining <= safeDepthWarningMargin)
            {
                _depthWarned = true;
                _advisoryMessageBuffer.Clear();
                AppendDepthWarningMessage(ref _advisoryMessageBuffer, remaining);
                NotifyWarning(in _advisoryMessageBuffer);
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
                    _advisoryMessageBuffer.Clear();
                    AppendBleedingMessage(ref _advisoryMessageBuffer);
                    NotifyCritical(in _advisoryMessageBuffer);
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
                    _advisoryMessageBuffer.Clear();
                    AppendFractureMessage(ref _advisoryMessageBuffer);
                    NotifyWarning(in _advisoryMessageBuffer);
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
                _advisoryMessageBuffer.Clear();
                AppendDeathAdvice(ref _advisoryMessageBuffer, record.Cause);
                NotifyWarning(in _advisoryMessageBuffer);

                _advisoryMessageBuffer.Clear();
                AppendDeathSummary(ref _advisoryMessageBuffer, record);
                NotifyInfo(in _advisoryMessageBuffer);
            }
        }

        private void HandleModuleBreached()
        {
            NotifyCritical(MsgBaseBreach);
        }

        private void HandleModuleEmergency(BaseModuleFailureMode failureMode, float integrity)
        {
            _advisoryMessageBuffer.Clear();
            AppendBaseEmergencyMessage(ref _advisoryMessageBuffer, failureMode, integrity);
            NotifyWarning(in _advisoryMessageBuffer);
        }

        private void HandleModuleAirQualityWarning(float airQualityNormalized)
        {
            _advisoryMessageBuffer.Clear();
            AppendAirQualityMessage(ref _advisoryMessageBuffer, airQualityNormalized);

            if (airQualityNormalized <= 0.12f)
            {
                NotifyCritical(in _advisoryMessageBuffer);
                return;
            }

            NotifyWarning(in _advisoryMessageBuffer);
        }

        private void NotifyWarning(string message)
        {
            hudNotification?.ShowWarning(message);
            PlayUiClip(warningClip);
        }

        private void NotifyWarning(in FixedCharBuffer messageBuffer)
        {
            if (messageBuffer.Length <= 0)
                return;

            hudNotification?.ShowWarning(in messageBuffer);
            PlayUiClip(warningClip);
        }

        private void NotifyCritical(string message)
        {
            hudNotification?.ShowCritical(message);
            PlayUiClip(criticalClip != null ? criticalClip : warningClip);
        }

        private void NotifyCritical(in FixedCharBuffer messageBuffer)
        {
            if (messageBuffer.Length <= 0)
                return;

            hudNotification?.ShowCritical(in messageBuffer);
            PlayUiClip(criticalClip != null ? criticalClip : warningClip);
        }

        private void NotifyInfo(string message)
        {
            hudNotification?.ShowInfo(message);
        }

        private void NotifyInfo(in FixedCharBuffer messageBuffer)
        {
            if (messageBuffer.Length <= 0)
                return;

            hudNotification?.ShowInfo(in messageBuffer);
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

        private void AppendDeathAdvice(ref FixedCharBuffer buffer, SurvivalDeathCause cause)
        {
            if (survival == null)
            {
                AppendText(ref buffer, "SURVIVAL ADVICE: REBUILD YOUR SAFETY MARGIN BEFORE THE NEXT PUSH");
                return;
            }

            AppendText(ref buffer, "SURVIVAL ADVICE: ");
            AppendUpperInvariant(ref buffer, survival.GetDeathAdvice(cause));
        }

        private static void AppendDeathSummary(ref FixedCharBuffer buffer, SurvivalDeathRecord record)
        {
            int totalSeconds = Mathf.Max(0, Mathf.RoundToInt((float)record.LifeDurationSeconds));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            AppendText(ref buffer, "LAST RUN ");
            AppendTwoDigits(ref buffer, minutes);
            AppendText(ref buffer, ":");
            AppendTwoDigits(ref buffer, seconds);
            AppendText(ref buffer, " // PEAK ");
            AppendFloat(ref buffer, (float)record.PeakDepthMeters, 0);
            AppendText(ref buffer, "M // O2 LOW ");
            AppendFloat(ref buffer, record.LowestOxygenNormalized * 100f, 0);
            AppendText(ref buffer, "% // PWR LOW ");
            AppendFloat(ref buffer, record.LowestEnergyNormalized * 100f, 0);
            AppendText(ref buffer, "% // HULL LOW ");
            AppendFloat(ref buffer, record.LowestIntegrityNormalized * 100f, 0);
            AppendText(ref buffer, "%");
        }

        private static void AppendBaseEmergencyMessage(ref FixedCharBuffer buffer, BaseModuleFailureMode failureMode, float integrity)
        {
            int integrityPercent = Mathf.Clamp(Mathf.RoundToInt(integrity * 100f), 0, 100);

            switch (failureMode)
            {
                case BaseModuleFailureMode.OxygenLeak:
                    AppendText(ref buffer, "BASE OXYGEN LEAK // SHELTER UNSAFE // HULL ");
                    break;
                case BaseModuleFailureMode.Fire:
                    AppendText(ref buffer, "BASE FIRE // EVACUATE OR REPAIR // HULL ");
                    break;
                case BaseModuleFailureMode.ShortCircuit:
                    AppendText(ref buffer, "BASE SHORT CIRCUIT // POWER OFFLINE // HULL ");
                    break;
                default:
                    AppendText(ref buffer, "BASE EMERGENCY // HULL ");
                    break;
            }

            AppendInt(ref buffer, integrityPercent);
            AppendText(ref buffer, "%");
        }

        private static void AppendAirQualityMessage(ref FixedCharBuffer buffer, float airQualityNormalized)
        {
            int reservePercent = Mathf.Clamp(Mathf.RoundToInt(airQualityNormalized * 100f), 0, 100);
            AppendText(ref buffer, "BASE AIR QUALITY LOW // SCRUBBERS ");
            AppendInt(ref buffer, reservePercent);
            AppendText(ref buffer, "%");
        }

        private void EvaluateColdStress()
        {
            float severity = survival != null ? survival.ColdStressSeverity01 : 0f;

            if (!_coldCritical && severity >= coldCriticalThreshold)
            {
                _coldCritical = true;
                _advisoryMessageBuffer.Clear();
                AppendColdStressMessage(ref _advisoryMessageBuffer, true);
                NotifyCritical(in _advisoryMessageBuffer);
            }
            else if (!_coldWarned && severity >= coldWarningThreshold)
            {
                _coldWarned = true;
                _advisoryMessageBuffer.Clear();
                AppendColdStressMessage(ref _advisoryMessageBuffer, false);
                NotifyWarning(in _advisoryMessageBuffer);
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
                _advisoryMessageBuffer.Clear();
                AppendHeatStressMessage(ref _advisoryMessageBuffer, true);
                NotifyCritical(in _advisoryMessageBuffer);
            }
            else if (!_heatWarned && severity >= heatWarningThreshold)
            {
                _heatWarned = true;
                _advisoryMessageBuffer.Clear();
                AppendHeatStressMessage(ref _advisoryMessageBuffer, false);
                NotifyWarning(in _advisoryMessageBuffer);
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

        private void AppendBleedingMessage(ref FixedCharBuffer buffer)
        {
            if (survival == null)
            {
                AppendText(ref buffer, MsgBleeding);
                return;
            }

            AppendText(ref buffer, MsgBleeding);
            AppendText(ref buffer, " // ");
            AppendFloat(ref buffer, survival.BleedingSeverity01 * 1.8f, 1);
            AppendText(ref buffer, "/S");
        }

        private void AppendFractureMessage(ref FixedCharBuffer buffer)
        {
            if (survival == null)
            {
                AppendText(ref buffer, MsgFracture);
                return;
            }

            int mobilityPercent = Mathf.Clamp(
                Mathf.RoundToInt((1f - survival.FracturePenalty01) * 100f),
                0,
                100);
            AppendText(ref buffer, MsgFracture);
            AppendText(ref buffer, " // MOBILITY ");
            AppendInt(ref buffer, mobilityPercent);
            AppendText(ref buffer, "%");
        }

        private void AppendColdStressMessage(ref FixedCharBuffer buffer, bool critical)
        {
            if (survival == null)
            {
                AppendText(ref buffer, critical ? MsgColdCritical : MsgColdWarning);
                return;
            }

            string prefix = critical ? MsgColdCritical : MsgColdWarning;
            AppendText(ref buffer, prefix);
            AppendText(ref buffer, " // ");
            AppendFloat(ref buffer, survival.EnvironmentTemperature, 0);
            AppendText(ref buffer, "C // PWR ");
            AppendFloat(ref buffer, survival.EnergyPercent, 0);
            AppendText(ref buffer, "%");
        }

        private void AppendHeatStressMessage(ref FixedCharBuffer buffer, bool critical)
        {
            if (survival == null)
            {
                AppendText(ref buffer, critical ? MsgHeatCritical : MsgHeatWarning);
                return;
            }

            string prefix = critical ? MsgHeatCritical : MsgHeatWarning;
            AppendText(ref buffer, prefix);
            AppendText(ref buffer, " // ");
            AppendFloat(ref buffer, survival.EnvironmentTemperature, 0);
            AppendText(ref buffer, "C // HYD ");
            AppendFloat(ref buffer, survival.ThirstPercent, 0);
            AppendText(ref buffer, "%");
        }

        private void AppendDepthWarningMessage(ref FixedCharBuffer buffer, float safeDepthMarginMeters)
        {
            float displayedMargin = Mathf.Max(0f, safeDepthMarginMeters);
            AppendText(ref buffer, "SAFE DEPTH WINDOW ");
            AppendFloat(ref buffer, displayedMargin, 0);
            AppendText(ref buffer, "M // SUIT RATING ");
            AppendFloat(ref buffer, survival != null && survival.Stats != null ? survival.Stats.SafeDepth : 0f, 0);
            AppendText(ref buffer, "M");
        }

        private void AppendDepthCriticalMessage(ref FixedCharBuffer buffer)
        {
            if (survival == null)
            {
                AppendText(ref buffer, "PRESSURE DAMAGE ACTIVE");
                return;
            }

            AppendText(ref buffer, "PRESSURE DAMAGE ACTIVE // +");
            AppendFloat(ref buffer, survival.OverpressureMeters, 0);
            AppendText(ref buffer, "M // HULL ");
            AppendFloat(ref buffer, survival.PressureDamagePerSecond, 1);
            AppendText(ref buffer, "/S");
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value.AsSpan());
        }

        private static bool AppendInt(ref FixedCharBuffer buffer, int value)
        {
            return buffer.AppendInt(value);
        }

        private static bool AppendFloat(ref FixedCharBuffer buffer, float value, int decimals)
        {
            return buffer.AppendFloat(value, decimals);
        }

        private static bool AppendTwoDigits(ref FixedCharBuffer buffer, int value)
        {
            int safeValue = Mathf.Max(0, value);
            if (safeValue < 10 && !AppendText(ref buffer, "0"))
                return false;

            return AppendInt(ref buffer, safeValue);
        }

        private static bool AppendUpperInvariant(ref FixedCharBuffer buffer, string value)
        {
            if (string.IsNullOrEmpty(value))
                return true;

            Span<char> scratch = stackalloc char[1];
            for (int i = 0; i < value.Length; i++)
            {
                scratch[0] = value[i] == '_' ? ' ' : char.ToUpperInvariant(value[i]);
                if (!buffer.Append(scratch))
                    return false;
            }

            return true;
        }
    }
}
