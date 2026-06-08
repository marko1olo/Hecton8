using System;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Suit Advisory Controller")]
    public sealed class SuitAdvisoryController : MonoBehaviour, IBaseIntegrityEventListener, ILateFrameTickable, IGlobalRegistryHotSwapListener
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
        private bool _hotSwapListenerRegistered;
        private uint _survivalSignalSourceId;
        private uint _lastSurvivalSignalSequence;
        private IAudioService _cachedAudioService;
        private IPlayerRuntimeContext _cachedPlayerContext;
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
            CacheAudioService(GlobalRegistry.Audio);
            CachePlayerRuntimeContext(GlobalRegistry.Player);
            ResolveReferences();
        }

        private void OnEnable()
        {
            CacheAudioService(GlobalRegistry.Audio);
            CachePlayerRuntimeContext(GlobalRegistry.Player);
            TryRegisterHotSwapListener();
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
            TryUnregisterHotSwapListener();
            _cachedPlayerContext = null;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            UnregisterSurvivalSignalPump();
            TryUnregisterHotSwapListener();
            BaseIntegrityEvents.AssertUnregistered(this, nameof(SuitAdvisoryController));
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Audio)
                CacheAudioService(currentService as IAudioService);
            else if (serviceSlot == GlobalRegistryServiceSlot.Player)
                CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
        }

        private void ResolveReferences()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (survival == null && playerContext != null && playerContext.IsInitialized)
                survival = playerContext.SurvivalSystem;

            if (survival == null)
            {
                TryGetComponent(out survival);

                if (survival == null &&
                    GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                    playerTransform != null)
                {
                    playerTransform.TryGetComponent(out survival);
                }
            }

            if (hudNotification == null)
                HUDNotification.TryGetActive(out hudNotification);
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext)
        {
            _cachedPlayerContext = playerContext != null && playerContext.IsInitialized ? playerContext : null;
            if (survival == null && _cachedPlayerContext != null)
                survival = _cachedPlayerContext.SurvivalSystem;
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
        public void OnBaseIntegrityEvent(in UiBaseIntegrityEventPayload payload)
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

        public void LateFrameTick()
        {
            if (survival == null)
                return;

            RefreshSurvivalSignalBinding();
            if (_survivalSignalSourceId == 0u)
                return;

            ReadOnlySpan<SurvivalVitalsChangedSignal> signals = SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly SurvivalVitalsChangedSignal signal = ref signals[i];
                if (signal.SourceId != _survivalSignalSourceId)
                    continue;

                if (signal.Sequence == 0u || signal.Sequence == _lastSurvivalSignalSequence)
                    continue;

                _lastSurvivalSignalSequence = signal.Sequence;
                ProcessSurvivalVitalsSignal(in signal);
            }
        }

        private void ProcessSurvivalVitalsSignal(in SurvivalVitalsChangedSignal signal)
        {
            uint flags = signal.Flags;
            if ((flags & SurvivalVitalsChangedSignalFlags.Oxygen) != 0u &&
                TryResolveFiniteUnit01(signal.Oxygen01, out float oxygen01))
            {
                HandleOxygenChanged(oxygen01);
            }

            if ((flags & SurvivalVitalsChangedSignalFlags.Energy) != 0u &&
                TryResolveFiniteUnit01(signal.Energy01, out float energy01))
            {
                HandleEnergyChanged(energy01);
            }

            if ((flags & SurvivalVitalsChangedSignalFlags.Integrity) != 0u &&
                TryResolveFiniteUnit01(signal.Integrity01, out float integrity01))
            {
                HandleIntegrityChanged(integrity01);
            }

            if ((flags & SurvivalVitalsChangedSignalFlags.Depth) != 0u)
                HandleDepthChanged(float.NaN);

            if ((flags & (SurvivalVitalsChangedSignalFlags.Temperature | SurvivalVitalsChangedSignalFlags.Thermal)) != 0u)
                HandleThermalStateChanged();

            if ((flags & SurvivalVitalsChangedSignalFlags.Injury) != 0u)
                HandleInjuryStateChanged();

            SynchronizeDeathState();
        }

        private static bool TryResolveFiniteUnit01(float value, out float safeValue)
        {
            if (!math.isfinite(value))
            {
                safeValue = 0f;
                return false;
            }

            safeValue = math.saturate(value);
            return true;
        }

        private void RefreshSurvivalSignalBinding()
        {
            uint sourceId = ResolveSurvivalSignalSourceId(survival);
            if (_survivalSignalSourceId == sourceId)
                return;

            _survivalSignalSourceId = sourceId;
            _lastSurvivalSignalSequence = 0u;
        }

        private static uint ResolveSurvivalSignalSourceId(HectonSurvivalSystem system)
        {
            return system != null
                ? RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(system.GetEntityId()))
                : 0u;
        }

        private void RegisterSurvivalSignalPump()
        {
            if (_registeredForSurvivalSignals || !Application.isPlaying)
                return;

            _registeredForSurvivalSignals = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void UnregisterSurvivalSignalPump()
        {
            if (!_registeredForSurvivalSignals)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _registeredForSurvivalSignals = false;
        }

        private void EvaluateAll()
        {
            if (survival == null || survival.Stats == null)
                return;

            HandleOxygenChanged(survival.OxygenNormalized);
            HandleEnergyChanged(survival.EnergyNormalized);
            HandleIntegrityChanged(survival.IntegrityNormalized);
            HandleDepthChanged(float.NaN);
            HandleTemperatureChanged(survival.EnvironmentTemperature);
            HandleInjuryStateChanged();
            SynchronizeDeathState();
        }

        private void HandleOxygenChanged(float normalized)
        {
            if (survival == null)
                return;

            normalized = Mathf.Clamp01(normalized);

            if (!_oxygenCritical && normalized <= oxygenCriticalThreshold)
            {
                _oxygenCritical = true;
                NotifyCritical(MsgOxygenCritical.AsSpan());
            }
            else if (!_oxygenWarned && normalized <= oxygenWarningThreshold)
            {
                _oxygenWarned = true;
                NotifyWarning(MsgOxygenWarning.AsSpan());
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

        private void HandleEnergyChanged(float normalized)
        {
            if (survival == null)
                return;

            normalized = Mathf.Clamp01(normalized);
            if (!_energyWarned && normalized <= energyWarningThreshold)
            {
                _energyWarned = true;
                NotifyWarning(MsgEnergyWarning.AsSpan());
            }

            if (normalized > energyWarningThreshold + resetHysteresis)
                _energyWarned = false;
        }

        private void HandleIntegrityChanged(float normalized)
        {
            if (survival == null)
                return;

            normalized = Mathf.Clamp01(normalized);

            if (!_integrityCritical && normalized <= integrityCriticalThreshold)
            {
                _integrityCritical = true;
                NotifyCritical(MsgIntegrityCritical.AsSpan());
            }
            else if (!_integrityWarned && normalized <= integrityWarningThreshold)
            {
                _integrityWarned = true;
                NotifyWarning(MsgIntegrityWarning.AsSpan());
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

            float remaining = ResolveSafeDepthMarginMeters(depth);

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

        private float ResolveSafeDepthMarginMeters(float fallbackDepthMeters)
        {
            if (survival == null || survival.Stats == null)
                return 0f;

            float safeDepthMeters = ResolveEffectiveSafeDepthMeters();
            float depthMeters = ResolveAdvisoryDepthMeters(fallbackDepthMeters);
            return safeDepthMeters - depthMeters;
        }

        private float ResolveEffectiveSafeDepthMeters()
        {
            if (survival == null || survival.Stats == null)
                return 0f;

            float survivalDepth = survival.Depth;
            float margin = survival.SafeDepthMarginMeters;
            if (math.isfinite(survivalDepth) && math.isfinite(margin))
                return math.max(0f, math.max(0f, survivalDepth) + margin);

            return math.max(0f, survival.Stats.SafeDepth);
        }

        private float ResolveAdvisoryDepthMeters(float fallbackDepthMeters)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                return math.max(0f, movementState.DepthMeters);
            }

            if (math.isfinite(fallbackDepthMeters))
                return math.max(0f, fallbackDepthMeters);

            return survival != null && math.isfinite(survival.Depth)
                ? math.max(0f, survival.Depth)
                : 0f;
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

        private void SynchronizeDeathState()
        {
            if (survival == null)
                return;

            if (survival.IsAlive)
            {
                _deathTriggered = false;
                return;
            }

            HandleDeath();
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
            NotifyCritical(MsgBaseBreach.AsSpan());
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

        private void NotifyWarning(ReadOnlySpan<char> message)
        {
            _advisoryMessageBuffer.Clear();
            if (_advisoryMessageBuffer.Append(message))
                hudNotification?.ShowWarning(in _advisoryMessageBuffer);
            PlayUiClip(warningClip);
        }

        private void NotifyWarning(in FixedCharBuffer messageBuffer)
        {
            if (messageBuffer.Length <= 0)
                return;

            hudNotification?.ShowWarning(in messageBuffer);
            PlayUiClip(warningClip);
        }

        private void NotifyCritical(ReadOnlySpan<char> message)
        {
            _advisoryMessageBuffer.Clear();
            if (_advisoryMessageBuffer.Append(message))
                hudNotification?.ShowCritical(in _advisoryMessageBuffer);
            PlayUiClip(criticalClip != null ? criticalClip : warningClip);
        }

        private void NotifyCritical(in FixedCharBuffer messageBuffer)
        {
            if (messageBuffer.Length <= 0)
                return;

            hudNotification?.ShowCritical(in messageBuffer);
            PlayUiClip(criticalClip != null ? criticalClip : warningClip);
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

            IAudioService audio = ResolveAudioService();
            if (audio != null)
                audio.PlayStatic2D(clip, uiVolume);
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _cachedAudioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _cachedAudioService = null;
            return null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private ReadOnlySpan<char> ResolveDeathMessage()
        {
            if (survival == null)
                return MsgDeath.AsSpan();

            switch (survival.LastDeathCause)
            {
                case SurvivalDeathCause.OxygenDepletion:
                    return MsgDeathOxygen.AsSpan();
                case SurvivalDeathCause.PressureCollapse:
                    return MsgDeathPressure.AsSpan();
                case SurvivalDeathCause.ThermalFailure:
                    return MsgDeathThermal.AsSpan();
                case SurvivalDeathCause.RadiationExposure:
                    return MsgDeathRadiation.AsSpan();
                case SurvivalDeathCause.Starvation:
                    return MsgDeathStarvation.AsSpan();
                case SurvivalDeathCause.Dehydration:
                    return MsgDeathDehydration.AsSpan();
                case SurvivalDeathCause.IntegrityFailure:
                    return MsgDeathIntegrity.AsSpan();
                default:
                    return MsgDeath.AsSpan();
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
                char c = value[i] == '_' ? ' ' : value[i];
                if (c >= 'a' && c <= 'z')
                    c = (char)(c - 32);

                scratch[0] = c;
                if (!buffer.Append(scratch))
                    return false;
            }

            return true;
        }
    }
}
