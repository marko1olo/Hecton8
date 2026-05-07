using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Visor
{
    /// <summary>
    /// Applies critical-state pulse feedback through shader globals and heartbeat cues.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStressVFX : MonoBehaviour, ITickable, IPlayerSignalEventListener
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
        private const float DependencyResolveRetryIntervalSeconds = 0.5f;
        private static readonly int PlayerStress01Id = Shader.PropertyToID("_PlayerStress01");
        private static readonly int HectonHudStressChromaticAberrationId = Shader.PropertyToID("_HectonHudStressChromaticAberration");
        private static readonly int HectonHudStressVignetteId = Shader.PropertyToID("_HectonHudStressVignette");
        private static readonly int HectonHudFogFrostId = Shader.PropertyToID("_HectonHudFogFrost");

        private bool _registered;
        private HectonSurvivalSystem _survivalSystem;
        private HectonPlayerMovement _playerMovement;
        private HectonPlayerHealth _playerHealth;
        private float _pulsePhase;
        private float _heartbeatTimer;
        private float _lastEnvironmentTemperature = 20f;
        private bool _hasEnvironmentTemperatureSample;
        private float _interactionStress01;
        private float _interactionVolume01 = 1f;
        private float _interactionPitchScale = 1f;
        private float _interactionFrequency01;
        private float _traumaPulse01;
        private float _nextDependencyResolveTime;
        private bool _hasInteractionSignal;

        private void Awake()
        {
            TryResolveDependencies(force: true);
            _heartbeatTimer = heartbeatIntervalMaxSeconds;
        }

        private void OnEnable()
        {
            PlayerSignalEvents.Register(this);
            TryRegisterTickHandler();
        }

        private void OnDisable()
        {
            PlayerSignalEvents.Unregister(this);
            TryUnregisterTickHandler();
            ResetRuntimeEffects();
            _heartbeatTimer = heartbeatIntervalMaxSeconds;
            _hasEnvironmentTemperatureSample = false;
            _interactionStress01 = 0f;
            _interactionVolume01 = 1f;
            _interactionPitchScale = 1f;
            _interactionFrequency01 = 0f;
            _traumaPulse01 = 0f;
            _hasInteractionSignal = false;
        }

        private void OnDestroy()
        {
            TryUnregisterTickHandler();
            ResetRuntimeEffects();
        }

        /// <summary>
        /// Advances the stress pulse and heartbeat cadence.
        /// </summary>
        /// <param name="deltaTime">Tick delta supplied by <see cref="GameTickManager"/>.</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            TryResolveDependencies();

            if (_traumaPulse01 > 0f)
                _traumaPulse01 = Mathf.Max(0f, _traumaPulse01 - deltaTime * Mathf.Max(0.1f, traumaChromaticPulseDecayPerSecond));

            float stress01 = Mathf.Max(ResolveStress01(), _traumaPulse01);
            float audioStress01 = _hasInteractionSignal ? Mathf.Max(stress01, _interactionStress01) : stress01;
            _debugStress01 = stress01;
            float fog01 = ResolveFogging01();
            float frost01 = ResolveFrost01();

            if (audioStress01 <= 0.001f && fog01 <= 0.001f && frost01 <= 0.001f)
            {
                _pulsePhase = 0f;
                _heartbeatTimer = heartbeatIntervalMaxSeconds;
                ApplyStressPulse(0f, 0f, fog01, frost01);
                return;
            }

            float heartbeatBlend01 = _hasInteractionSignal
                ? Mathf.Clamp01(_interactionFrequency01)
                : audioStress01;
            float heartbeatInterval = math.lerp(heartbeatIntervalMaxSeconds, heartbeatIntervalMinSeconds, heartbeatBlend01);
            float pulseFrequency = 1f / Mathf.Max(0.05f, heartbeatInterval);
            _pulsePhase += deltaTime * pulseFrequency * PulseTwoPi;
            if (_pulsePhase > PulseTwoPi)
                _pulsePhase -= PulseTwoPi;

            float beat01 = Mathf.Sin(_pulsePhase) * 0.5f + 0.5f;
            beat01 *= beat01;
            beat01 *= beat01;

            _heartbeatTimer -= deltaTime;
            if (_heartbeatTimer <= 0f)
            {
                PlayHeartbeat(audioStress01);
                _heartbeatTimer = heartbeatInterval;
            }

            _debugPulse01 = beat01;
            ApplyStressPulse(stress01, beat01, fog01, frost01);
        }

        void IPlayerSignalEventListener.OnTraumaHudSignal(in TraumaHudSignal signal)
        {
            _traumaPulse01 = Mathf.Max(_traumaPulse01, Mathf.Clamp01(signal.GlitchIntensity));
        }

        void IPlayerSignalEventListener.OnInteractionSignal(in InteractionSignal signal)
        {
            HandleInteractionSignal(in signal);
        }

        void IPlayerSignalEventListener.OnToolDepletedSignal(in ToolDepletedSignal signal)
        {
        }

        private void HandleInteractionSignal(in InteractionSignal signal)
        {
            _interactionStress01 = Mathf.Clamp01(signal.Stress01);
            _interactionVolume01 = Mathf.Clamp01(signal.Volume01);
            _interactionPitchScale = Mathf.Clamp(signal.PitchScale, 0.1f, 3f);
            _interactionFrequency01 = Mathf.Clamp01(signal.Frequency01);
            _hasInteractionSignal = true;
        }

        private void TryResolveDependencies(bool force = false)
        {
            if (!force && _survivalSystem != null && _playerMovement != null && _playerHealth != null)
                return;

            if (!force)
            {
                float now = Time.unscaledTime;
                if (now < _nextDependencyResolveTime)
                    return;

                _nextDependencyResolveTime = now + DependencyResolveRetryIntervalSeconds;
            }

            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            if (_playerMovement == null)
                TryGetComponent(out _playerMovement);

            if (_playerHealth == null)
                TryGetComponent(out _playerHealth);

            if ((_survivalSystem == null || _playerMovement == null || _playerHealth == null) &&
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
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

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregisterTickHandler()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);

            _registered = false;
        }

        private float ResolveStress01()
        {
            float oxygenNormalized = _survivalSystem != null ? Mathf.Clamp01(_survivalSystem.OxygenNormalized) : 1f;
            float integrityNormalized = _survivalSystem != null ? Mathf.Clamp01(_survivalSystem.IntegrityNormalized) : 1f;
            float fatalPressure01 = _playerMovement != null ? Mathf.Clamp01(_playerMovement.CurrentFatalPressureSequence01) : 0f;
            float healthStress01 = _playerHealth != null ? Mathf.Clamp01(_playerHealth.Stress) : 0f;
            float oxygenStress01 = Mathf.Clamp01(Mathf.InverseLerp(oxygenCriticalThreshold, 0.05f, oxygenNormalized));
            float integrityStress01 = Mathf.Clamp01(Mathf.InverseLerp(integrityCriticalThreshold, 0.08f, integrityNormalized));
            float stress01 = Mathf.Clamp01(Mathf.Max(healthStress01, Mathf.Max(oxygenStress01, Mathf.Max(integrityStress01, fatalPressure01))));

            _debugOxygenNormalized = oxygenNormalized;
            _debugIntegrityNormalized = integrityNormalized;
            _debugFatalPressure01 = fatalPressure01;
            return stress01;
        }

        private void ApplyStressPulse(float stress01, float beat01, float fog01, float frost01)
        {
            float pulse = stress01 * (0.35f + beat01 * 0.65f);
            float hudStressChroma = Mathf.Clamp01(stress01 + fog01 * 0.18f);
            float shaderVignette = Mathf.Clamp01((pulse + frost01 * 0.58f + fog01 * 0.18f) * Mathf.Clamp01(shaderVignetteMaximum));
            float shaderFog = Mathf.Clamp01(fog01 * Mathf.Clamp01(shaderFogCondensationMaximum));
            float shaderFrost = Mathf.Clamp01(frost01 * Mathf.Clamp01(shaderFrostMaximum));

            Shader.SetGlobalFloat(PlayerStress01Id, Mathf.Clamp01(stress01));
            Shader.SetGlobalFloat(HectonHudStressChromaticAberrationId, hudStressChroma);
            Shader.SetGlobalFloat(HectonHudStressVignetteId, shaderVignette);
            Shader.SetGlobalVector(HectonHudFogFrostId, new Vector4(shaderFog, shaderFrost, fog01, frost01));
        }

        private void ResetRuntimeEffects()
        {
            _debugFog01 = 0f;
            _debugFrost01 = 0f;
            _debugTemperatureShock01 = 0f;
            _debugPulse01 = 0f;
            Shader.SetGlobalFloat(PlayerStress01Id, 0f);
            Shader.SetGlobalFloat(HectonHudStressChromaticAberrationId, 0f);
            Shader.SetGlobalFloat(HectonHudStressVignetteId, 0f);
            Shader.SetGlobalVector(HectonHudFogFrostId, Vector4.zero);
        }

        private void PlayHeartbeat(float stress01)
        {
            Hecton8.Core.IAudioService audioManager = Hecton8.Core.GlobalRegistry.Audio;
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
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                movement = playerContext != null ? playerContext.PlayerMovement : null;
            }

            if (movement != null)
                return (Vector3)movement.CurrentAup.ToRuntimeFloat3();

            return transform.position;
        }

        private float ResolveFogging01()
        {
            if (_survivalSystem == null)
            {
                _debugFog01 = 0f;
                _debugTemperatureShock01 = 0f;
                return 0f;
            }

            float oxygenFog01 = Mathf.Clamp01(Mathf.InverseLerp(oxygenFogThreshold, 0.04f, _survivalSystem.OxygenNormalized));
            float oxygenGraceFog01 = Mathf.Clamp01(_survivalSystem.OxygenGraceVisionBlur01);
            float nitrogenFog01 = Mathf.Clamp01(_survivalSystem.NitrogenNarcosisVisionBlur01);
            float temperature = _survivalSystem.EnvironmentTemperature;
            float thermalShock01 = 0f;

            if (_hasEnvironmentTemperatureSample)
            {
                float delta = Mathf.Abs(temperature - _lastEnvironmentTemperature);
                thermalShock01 = Mathf.Clamp01(
                    (delta - thermalShockDeltaThreshold) /
                    Mathf.Max(0.01f, thermalShockDeltaThreshold));
            }

            _lastEnvironmentTemperature = temperature;
            _hasEnvironmentTemperatureSample = true;
            _debugTemperatureShock01 = thermalShock01;
            _debugFog01 = Mathf.Clamp01(Mathf.Max(oxygenFog01, oxygenGraceFog01, nitrogenFog01, thermalShock01, _survivalSystem.RapidAscentRisk01 * 0.4f));
            return _debugFog01;
        }

        private float ResolveFrost01()
        {
            if (_survivalSystem == null)
            {
                _debugFrost01 = 0f;
                return 0f;
            }

            float temperature01 = Mathf.Clamp01(
                Mathf.InverseLerp(frostStartTemperatureCelsius, frostMaxTemperatureCelsius, _survivalSystem.EnvironmentTemperature));
            _debugFrost01 = Mathf.Clamp01(Mathf.Max(temperature01, _survivalSystem.ColdStressSeverity01));
            return _debugFrost01;
        }
    }
}


