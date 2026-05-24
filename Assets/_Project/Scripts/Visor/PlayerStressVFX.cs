using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Visor
{
    /// <summary>
    /// Applies critical-state pulse feedback through shader globals and heartbeat cues.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStressVFX : MonoBehaviour, ITickable, IPlayerSignalEventListener, IGlobalRegistryHotSwapListener
    {
        [Header("── Audio ──────────────────")]
        [Tooltip("Helmet heartbeat cue played while the player approaches death.")]
        [SerializeField] private AudioClip heartbeatClip;

        [Tooltip("Quietest heartbeat volume at low stress.")]
        [SerializeField, Range(0f, 1f)] private float heartbeatVolumeMin = 0.08f;

        [Tooltip("Maximum heartbeat volume at peak stress.")]
        [SerializeField, Range(0f, 1f)] private float heartbeatVolumeMax = 0.34f;

        [Tooltip("Longest heartbeat interval at low stress.")]
        [SerializeField, Min(0.05f)] private float heartbeatIntervalMaxSeconds = 1.2f;

        [Tooltip("Shortest heartbeat interval at peak stress.")]
        [SerializeField, Min(0.05f)] private float heartbeatIntervalMinSeconds = 0.36f;

        [Header("── Post FX ──────────────────")]
        [Tooltip("Peak shader-only edge vignette fake driven by the stress pulse.")]
        [SerializeField, Range(0f, 1f)] private float shaderVignetteMaximum = 0.42f;

        [Tooltip("Decay speed for one-shot HUD shader stress pulses raised by save/load integrity warnings.")]
        [SerializeField, Range(0.1f, 8f)] private float traumaChromaticPulseDecayPerSecond = 2.6f;

        [Tooltip("Peak shader-only condensation fake injected into SuitVisor.")]
        [SerializeField, Range(0f, 1f)] private float shaderFogCondensationMaximum = 0.35f;

        [Tooltip("Peak shader-only frost fake injected into SuitVisor.")]
        [SerializeField, Range(0f, 1f)] private float shaderFrostMaximum = 0.5f;

        [Tooltip("Oxygen threshold below which visor fogging starts to bloom.")]
        [SerializeField, Range(0.01f, 1f)] private float oxygenFogThreshold = 0.24f;

        [Tooltip("Temperature delta in Celsius that counts as a visor-shocking thermal transition.")]
        [SerializeField, Range(1f, 60f)] private float thermalShockDeltaThreshold = 11f;

        [Tooltip("Resolved environment temperature below which frost begins to creep over the visor rim.")]
        [SerializeField] private float frostStartTemperatureCelsius = -6f;

        [Tooltip("Resolved environment temperature at which frost reaches full edge coverage.")]
        [SerializeField] private float frostMaxTemperatureCelsius = -30f;

        [Header("── Thresholds ──────────────────")]
        [Tooltip("Oxygen threshold below which the pulse starts ramping aggressively.")]
        [SerializeField, Range(0.01f, 1f)] private float oxygenCriticalThreshold = 0.30f;

        [Tooltip("Integrity threshold below which the pulse starts ramping aggressively.")]
        [SerializeField, Range(0.01f, 1f)] private float integrityCriticalThreshold = 0.35f;

        [Header("── Diagnostics ──────────────────")]
        [SerializeField] private float _debugStress01;
        [SerializeField] private float _debugPulse01;
        [SerializeField] private float _debugOxygenNormalized = 1f;
        [SerializeField] private float _debugIntegrityNormalized = 1f;
        [SerializeField] private float _debugFatalPressure01;
        [SerializeField] private float _debugFog01;
        [SerializeField] private float _debugFrost01;
        [SerializeField] private float _debugTemperatureShock01;

        private const float PulseTwoPi = Mathf.PI * 2f;
        private const float PulseInvTwoPi = 1f / PulseTwoPi;
        private const float DependencyResolveRetryIntervalSeconds = 0.5f;
        private static readonly int PlayerStress01Id = Shader.PropertyToID("_PlayerStress01");
        private static readonly int HectonHudStressChromaticAberrationId = Shader.PropertyToID("_HectonHudStressChromaticAberration");
        private static readonly int HectonHudStressVignetteId = Shader.PropertyToID("_HectonHudStressVignette");
        private static readonly int HectonHudFogFrostId = Shader.PropertyToID("_HectonHudFogFrost");

        private bool _registered;
        private bool _hotSwapRegistered;
        private HectonSurvivalSystem _survivalSystem;
        private HectonPlayerMovement _playerMovement;
        private HectonPlayerHealth _playerHealth;
        private IAudioService _cachedAudioService;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private float _pulsePhase;
        private float _heartbeatTimer;
        private float _lastEnvironmentTemperature = 20f;
        private bool _hasEnvironmentTemperatureSample;
        private float _interactionStress01;
        private float _interactionVolume01 = 1f;
        private float _interactionPitchScale = 1f;
        private float _interactionFrequency01;
        private float _psychoMetricsStress01;
        private float _traumaPulse01;
        private float _dependencyResolveRetryRemaining;
        private int _lastPlayerStressSignalSequence;
        private bool _hasInteractionSignal;
        private bool _hasAppliedStressGlobals;
        private float _appliedPlayerStress01;
        private float _appliedHudStressChromaticAberration;
        private float _appliedHudStressVignette;
        private Vector4 _appliedHudFogFrost;

        private const float ShaderUniformEpsilon = 0.0005f;

        private void Awake()
        {
            CacheRegistryServicesCold();
            TryResolveDependencies(force: true);
            _heartbeatTimer = heartbeatIntervalMaxSeconds;
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            PlayerSignalEvents.Register(this);
            TryRegisterTickHandler();
        }

        private void OnDisable()
        {
            PlayerSignalEvents.Unregister(this);
            TryUnregisterHotSwapListener();
            TryUnregisterTickHandler();
            ResetRuntimeEffects();
            _heartbeatTimer = heartbeatIntervalMaxSeconds;
            _hasEnvironmentTemperatureSample = false;
            _interactionStress01 = 0f;
            _interactionVolume01 = 1f;
            _interactionPitchScale = 1f;
            _interactionFrequency01 = 0f;
            _psychoMetricsStress01 = 0f;
            _traumaPulse01 = 0f;
            _dependencyResolveRetryRemaining = 0f;
            _lastPlayerStressSignalSequence = 0;
            _hasInteractionSignal = false;
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterTickHandler();
            ResetRuntimeEffects();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && isActiveAndEnabled)
            {
                TryRegisterTickHandler();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Audio)
            {
                _cachedAudioService = currentService as IAudioService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                _cachedPlayerContext = currentService as IPlayerRuntimeContext;
        }

        /// <summary>
        /// Advances the stress pulse and heartbeat cadence.
        /// </summary>
        /// <param name="deltaTime">Tick delta supplied by <see cref="GameTickManager"/>.</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || !math.isfinite(deltaTime))
                return;

            TryResolveDependencies(deltaTime);

            _traumaPulse01 = SanitizeUnit(_traumaPulse01);
            _interactionStress01 = SanitizeUnit(_interactionStress01);
            _interactionFrequency01 = SanitizeUnit(_interactionFrequency01);
            if (!math.isfinite(_pulsePhase))
                _pulsePhase = 0f;

            if (_traumaPulse01 > 0f)
                _traumaPulse01 = math.max(0f, _traumaPulse01 - deltaTime * math.max(0.1f, traumaChromaticPulseDecayPerSecond));

            float stress01 = math.max(SanitizeUnit(ResolveStress01()), _traumaPulse01);
            float audioStress01 = _hasInteractionSignal ? math.max(stress01, _interactionStress01) : stress01;
            float fog01 = ResolveFogging01();
            float frost01 = ResolveFrost01();

            if (audioStress01 <= 0.001f && fog01 <= 0.001f && frost01 <= 0.001f)
            {
                _pulsePhase = 0f;
                _heartbeatTimer = heartbeatIntervalMaxSeconds;
                ApplyDebugPulseState(stress01, 0f);
                ApplyStressPulse(0f, 0f, fog01, frost01);
                return;
            }

            float heartbeatBlend01 = _hasInteractionSignal
                ? math.saturate(_interactionFrequency01)
                : audioStress01;
            float heartbeatInterval = math.lerp(heartbeatIntervalMaxSeconds, heartbeatIntervalMinSeconds, heartbeatBlend01);
            float pulseFrequency = 1f / math.max(0.05f, heartbeatInterval);
            _pulsePhase += deltaTime * pulseFrequency * PulseTwoPi;
            if (_pulsePhase > PulseTwoPi)
                _pulsePhase -= PulseTwoPi;

            float beat01 = EvaluateCheapHeartbeatPulse01(_pulsePhase);

            _heartbeatTimer -= deltaTime;
            if (_heartbeatTimer <= 0f)
            {
                PlayHeartbeat(audioStress01);
                _heartbeatTimer = heartbeatInterval;
            }

            ApplyDebugPulseState(stress01, beat01);
            ApplyStressPulse(stress01, beat01, fog01, frost01);
        }

        private static float EvaluateCheapHeartbeatPulse01(float phaseRadians)
        {
            float phase01 = math.frac(phaseRadians * PulseInvTwoPi);
            float triangle = 1f - math.abs(phase01 * 2f - 1f);
            float pulse = triangle * triangle;
            return pulse * pulse;
        }

        private static float FastInverseLerp01(float from, float to, float value)
        {
            if (!math.isfinite(from) || !math.isfinite(to) || !math.isfinite(value))
                return 0f;

            float range = to - from;
            if (math.abs(range) <= 0.00001f)
                return 0f;

            return math.saturate((value - from) / range);
        }

        private static float SanitizeUnit(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        void IPlayerSignalEventListener.OnTraumaHudSignal(in TraumaHudSignal signal)
        {
            _traumaPulse01 = math.max(SanitizeUnit(_traumaPulse01), SanitizeUnit(signal.GlitchIntensity));
        }

        void IPlayerSignalEventListener.OnInteractionSignal(in PlayerInteractionStressSignal signal)
        {
            HandleInteractionSignal(in signal);
        }

        void IPlayerSignalEventListener.OnToolDepletedSignal(in ToolDepletedSignal signal)
        {
        }

        private void HandleInteractionSignal(in PlayerInteractionStressSignal signal)
        {
            _interactionStress01 = SanitizeUnit(signal.Stress01);
            _interactionVolume01 = SanitizeUnit(signal.Volume01);
            _interactionPitchScale = math.isfinite(signal.PitchScale) ? math.clamp(signal.PitchScale, 0.1f, 3f) : 1f;
            _interactionFrequency01 = SanitizeUnit(signal.Frequency01);
            _hasInteractionSignal = true;
        }

        private void TryResolveDependencies(float deltaTime = 0f, bool force = false)
        {
            if (!force && _survivalSystem != null && _playerMovement != null && _playerHealth != null)
                return;

            if (!force)
            {
                if (_dependencyResolveRetryRemaining > 0f)
                {
                    _dependencyResolveRetryRemaining = math.max(0f, _dependencyResolveRetryRemaining - deltaTime);
                    if (_dependencyResolveRetryRemaining > 0f)
                        return;
                }

                _dependencyResolveRetryRemaining = DependencyResolveRetryIntervalSeconds;
            }

            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            if (_playerMovement == null)
                TryGetComponent(out _playerMovement);

            if (_playerHealth == null)
                TryGetComponent(out _playerHealth);

            if ((_survivalSystem == null || _playerMovement == null || _playerHealth == null) &&
                GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (_survivalSystem == null)
                    playerTransform.TryGetComponent(out _survivalSystem);

                if (_playerMovement == null)
                    playerTransform.TryGetComponent(out _playerMovement);

                if (_playerHealth == null)
                    playerTransform.TryGetComponent(out _playerHealth);
            }
        }

        private void TryRegisterTickHandler()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void TryUnregisterTickHandler()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);

            _registered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedAudioService = GlobalRegistry.Audio;
            _cachedPlayerContext = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;
        }

        private float ResolveStress01()
        {
            if (GlobalSignals.TryGetLatestPlayerStressSignal(out PlayerStressSignal stressSignal, out int sequence) &&
                sequence != _lastPlayerStressSignalSequence)
            {
                _lastPlayerStressSignalSequence = sequence;
                _psychoMetricsStress01 = SanitizeUnit(stressSignal.Stress01);
            }

            float oxygenNormalized = _survivalSystem != null ? SanitizeUnit(_survivalSystem.OxygenNormalized) : 1f;
            float integrityNormalized = _survivalSystem != null ? SanitizeUnit(_survivalSystem.IntegrityNormalized) : 1f;
            float fatalPressure01 = _playerMovement != null ? SanitizeUnit(_playerMovement.CurrentFatalPressureSequence01) : 0f;
            float healthStress01 = _playerHealth != null ? SanitizeUnit(_playerHealth.Stress) : 0f;
            float oxygenStress01 = FastInverseLerp01(oxygenCriticalThreshold, 0.05f, oxygenNormalized);
            float integrityStress01 = FastInverseLerp01(integrityCriticalThreshold, 0.08f, integrityNormalized);
            float stress01 = math.saturate(math.max(
                _psychoMetricsStress01,
                math.max(healthStress01, math.max(oxygenStress01, math.max(integrityStress01, fatalPressure01)))));

            ApplyDebugVitalsState(oxygenNormalized, integrityNormalized, fatalPressure01);
            return stress01;
        }

        private void ApplyStressPulse(float stress01, float beat01, float fog01, float frost01)
        {
            float playerStress01 = SanitizeUnit(stress01);
            float safeBeat01 = SanitizeUnit(beat01);
            float safeFog01 = SanitizeUnit(fog01);
            float safeFrost01 = SanitizeUnit(frost01);
            float pulse = playerStress01 * (0.35f + safeBeat01 * 0.65f);
            float hudStressChroma = math.saturate(playerStress01 + safeFog01 * 0.18f);
            float shaderVignette = math.saturate((pulse + safeFrost01 * 0.58f + safeFog01 * 0.18f) * SanitizeUnit(shaderVignetteMaximum));
            float shaderFog = math.saturate(safeFog01 * SanitizeUnit(shaderFogCondensationMaximum));
            float shaderFrost = math.saturate(safeFrost01 * SanitizeUnit(shaderFrostMaximum));
            Vector4 fogFrost;
            fogFrost.x = shaderFog;
            fogFrost.y = shaderFrost;
            fogFrost.z = safeFog01;
            fogFrost.w = safeFrost01;

            ApplyStressGlobals(playerStress01, hudStressChroma, shaderVignette, fogFrost, force: false);
        }

        private void ResetRuntimeEffects()
        {
            ClearDebugState();
            ApplyStressGlobals(0f, 0f, 0f, Vector4.zero, force: true);
        }

        private void ApplyStressGlobals(
            float playerStress01,
            float hudStressChroma,
            float shaderVignette,
            Vector4 fogFrost,
            bool force)
        {
            bool hasCachedGlobals = _hasAppliedStressGlobals;
            if (force || !hasCachedGlobals || ShouldApply(_appliedPlayerStress01, playerStress01))
            {
                Shader.SetGlobalFloat(PlayerStress01Id, playerStress01);
                _appliedPlayerStress01 = playerStress01;
            }

            if (force || !hasCachedGlobals || ShouldApply(_appliedHudStressChromaticAberration, hudStressChroma))
            {
                Shader.SetGlobalFloat(HectonHudStressChromaticAberrationId, hudStressChroma);
                _appliedHudStressChromaticAberration = hudStressChroma;
            }

            if (force || !hasCachedGlobals || ShouldApply(_appliedHudStressVignette, shaderVignette))
            {
                Shader.SetGlobalFloat(HectonHudStressVignetteId, shaderVignette);
                _appliedHudStressVignette = shaderVignette;
            }

            if (force || !hasCachedGlobals || ShouldApply(_appliedHudFogFrost, fogFrost))
            {
                Shader.SetGlobalVector(HectonHudFogFrostId, fogFrost);
                _appliedHudFogFrost = fogFrost;
            }

            _hasAppliedStressGlobals = true;
        }

        private static bool ShouldApply(float current, float next)
        {
            return math.abs(current - next) > ShaderUniformEpsilon;
        }

        private static bool ShouldApply(Vector4 current, Vector4 next)
        {
            Vector4 delta = current - next;
            return math.abs(delta.x) > ShaderUniformEpsilon ||
                math.abs(delta.y) > ShaderUniformEpsilon ||
                math.abs(delta.z) > ShaderUniformEpsilon ||
                math.abs(delta.w) > ShaderUniformEpsilon;
        }

        private void PlayHeartbeat(float stress01)
        {
            IAudioService audioManager = _cachedAudioService;
            if (heartbeatClip == null || audioManager == null)
                return;

            float signalVolume = _hasInteractionSignal ? _interactionVolume01 : 1f;
            float signalPitch = _hasInteractionSignal ? _interactionPitchScale : 1f;
            float volume = math.lerp(heartbeatVolumeMin, heartbeatVolumeMax, stress01) * signalVolume;
            audioManager.PlayAtPoint(heartbeatClip, ResolveHeartbeatAudioPosition(), volume, signalPitch, audioManager.InterfaceGroup);
        }

        private Vector3 ResolveHeartbeatAudioPosition()
        {
            HectonPlayerMovement movement = _playerMovement;
            if (movement == null)
            {
                IPlayerRuntimeContext playerContext = _cachedPlayerContext;
                movement = playerContext != null ? playerContext.PlayerMovement : null;
            }

            if (movement != null)
                return (Vector3)movement.CurrentAup.ToRuntimeFloat3();

            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
            {
                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                    return (Vector3)movementState.PredictedAup.ToRuntimeFloat3();
            }

            return Vector3.zero;
        }

        private float ResolveFogging01()
        {
            if (_survivalSystem == null)
            {
                ApplyDebugFogState(0f, 0f);
                return 0f;
            }

            float oxygenNormalized = SanitizeUnit(_survivalSystem.OxygenNormalized);
            float oxygenFog01 = FastInverseLerp01(oxygenFogThreshold, 0.04f, oxygenNormalized);
            float oxygenGraceFog01 = SanitizeUnit(_survivalSystem.OxygenGraceVisionBlur01);
            float nitrogenFog01 = SanitizeUnit(_survivalSystem.NitrogenNarcosisVisionBlur01);
            float rawTemperature = _survivalSystem.EnvironmentTemperature;
            float temperature = math.isfinite(rawTemperature) ? rawTemperature : _lastEnvironmentTemperature;
            float thermalShock01 = 0f;

            if (_hasEnvironmentTemperatureSample)
            {
                float delta = math.abs(temperature - _lastEnvironmentTemperature);
                thermalShock01 = math.saturate(
                    (delta - thermalShockDeltaThreshold) /
                    math.max(0.01f, thermalShockDeltaThreshold));
            }

            _lastEnvironmentTemperature = temperature;
            _hasEnvironmentTemperatureSample = true;
            float rapidAscentFog01 = SanitizeUnit(_survivalSystem.RapidAscentRisk01) * 0.4f;
            float fog01 = math.saturate(math.max(
                math.max(oxygenFog01, oxygenGraceFog01),
                math.max(nitrogenFog01, math.max(thermalShock01, rapidAscentFog01))));
            ApplyDebugFogState(fog01, thermalShock01);
            return fog01;
        }

        private float ResolveFrost01()
        {
            if (_survivalSystem == null)
            {
                ApplyDebugFrostState(0f);
                return 0f;
            }

            float rawTemperature = _survivalSystem.EnvironmentTemperature;
            float temperature = math.isfinite(rawTemperature) ? rawTemperature : frostStartTemperatureCelsius;
            float temperature01 = FastInverseLerp01(frostStartTemperatureCelsius, frostMaxTemperatureCelsius, temperature);
            float frost01 = math.saturate(math.max(temperature01, SanitizeUnit(_survivalSystem.ColdStressSeverity01)));
            ApplyDebugFrostState(frost01);
            return frost01;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ApplyDebugPulseState(float stress01, float beat01)
        {
            _debugStress01 = stress01;
            _debugPulse01 = beat01;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ApplyDebugVitalsState(float oxygenNormalized, float integrityNormalized, float fatalPressure01)
        {
            _debugOxygenNormalized = oxygenNormalized;
            _debugIntegrityNormalized = integrityNormalized;
            _debugFatalPressure01 = fatalPressure01;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ApplyDebugFogState(float fog01, float thermalShock01)
        {
            _debugFog01 = fog01;
            _debugTemperatureShock01 = thermalShock01;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ApplyDebugFrostState(float frost01)
        {
            _debugFrost01 = frost01;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ClearDebugState()
        {
            _debugFog01 = 0f;
            _debugFrost01 = 0f;
            _debugTemperatureShock01 = 0f;
            _debugPulse01 = 0f;
        }
    }
}


