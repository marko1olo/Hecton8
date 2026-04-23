using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hecton8.Visor
{
    /// <summary>
    /// Applies critical-state pulse feedback through a dedicated runtime volume and heartbeat cues.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStressVFX : MonoBehaviour, ITickable
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
        [Tooltip("Peak vignette intensity injected by the stress pulse.")]
        [SerializeField, Range(0f, 0.8f)] private float maxVignetteIntensity = 0.42f;

        [Tooltip("Peak chromatic aberration intensity injected by the stress pulse.")]
        [SerializeField, Range(0f, 1f)] private float maxChromaticIntensity = 0.36f;

        [Tooltip("Extra vignette smoothness applied at high stress.")]
        [SerializeField, Range(0f, 1f)] private float maxVignetteSmoothness = 0.86f;

        [Tooltip("Priority applied to the runtime stress volume so it layers above baseline presentation.")]
        [SerializeField] private float runtimeVolumePriority = 95f;

        [Tooltip("Maximum contrast loss applied during visor fogging.")]
        [SerializeField, Range(0f, 100f)] private float fogContrastLoss = 28f;

        [Tooltip("Maximum saturation loss applied during visor fogging.")]
        [SerializeField, Range(0f, 100f)] private float fogSaturationLoss = 22f;

        [Tooltip("Oxygen threshold below which visor fogging starts to bloom.")]
        [SerializeField, Range(0.01f, 1f)] private float oxygenFogThreshold = 0.24f;

        [Tooltip("Temperature delta in Celsius that counts as a visor-shocking thermal transition.")]
        [SerializeField, Range(1f, 60f)] private float thermalShockDeltaThreshold = 11f;

        [Tooltip("Resolved environment temperature below which frost begins to creep over the visor rim.")]
        [SerializeField] private float frostStartTemperatureCelsius = -6f;

        [Tooltip("Resolved environment temperature at which frost reaches full edge coverage.")]
        [SerializeField] private float frostMaxTemperatureCelsius = -30f;

        [Tooltip("Cold tint used for frost edging on the visor.")]
        [SerializeField] private Color frostTint = new Color(0.72f, 0.9f, 1f, 1f);

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

        private bool _registered;
        private HectonSurvivalSystem _survivalSystem;
        private HectonPlayerMovement _playerMovement;
        private Volume _runtimeVolume;
        private VolumeProfile _runtimeProfile;
        private Vignette _runtimeVignette;
        private ChromaticAberration _runtimeChromatic;
        private ColorAdjustments _runtimeColorAdjustments;
        private float _pulsePhase;
        private float _heartbeatTimer;
        private float _lastEnvironmentTemperature = 20f;
        private bool _hasEnvironmentTemperatureSample;

        private void Awake()
        {
            TryResolveDependencies();
            EnsureRuntimeVolume();
            _heartbeatTimer = heartbeatIntervalMaxSeconds;
        }

        private void OnEnable()
        {
            TryRegisterTickHandler();
        }

        private void OnDisable()
        {
            TryUnregisterTickHandler();
            ResetRuntimeEffects();
            _heartbeatTimer = heartbeatIntervalMaxSeconds;
            _hasEnvironmentTemperatureSample = false;
        }

        private void OnDestroy()
        {
            TryUnregisterTickHandler();

            if (_runtimeProfile != null)
                Destroy(_runtimeProfile);
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

            float stress01 = ResolveStress01();
            _debugStress01 = stress01;
            float fog01 = ResolveFogging01();
            float frost01 = ResolveFrost01();

            if (stress01 <= 0.001f && fog01 <= 0.001f && frost01 <= 0.001f)
            {
                _pulsePhase = 0f;
                _heartbeatTimer = heartbeatIntervalMaxSeconds;
                ApplyStressPulse(0f, 0f, fog01, frost01);
                return;
            }

            float heartbeatInterval = Mathf.Lerp(heartbeatIntervalMaxSeconds, heartbeatIntervalMinSeconds, stress01);
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
                PlayHeartbeat(stress01);
                _heartbeatTimer = heartbeatInterval;
            }

            _debugPulse01 = beat01;
            ApplyStressPulse(stress01, beat01, fog01, frost01);
        }

        private void TryResolveDependencies()
        {
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            if (_playerMovement == null)
                TryGetComponent(out _playerMovement);

            if ((_survivalSystem == null || _playerMovement == null) &&
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (_survivalSystem == null)
                    playerTransform.TryGetComponent(out _survivalSystem);

                if (_playerMovement == null)
                    playerTransform.TryGetComponent(out _playerMovement);
            }
        }

        private void EnsureRuntimeVolume()
        {
            if (_runtimeVolume != null)
                return;

            GameObject runtimeObject = new GameObject("__PlayerStressVFXVolume"); // COLD ALLOC: one runtime child volume for player stress pulse - owner: PlayerStressVFX
            runtimeObject.transform.SetParent(transform, false);
            _runtimeVolume = runtimeObject.AddComponent<Volume>();
            _runtimeVolume.isGlobal = true;
            _runtimeVolume.priority = runtimeVolumePriority;
            _runtimeVolume.weight = 1f;

            _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>(); // COLD ALLOC: runtime-only volume profile so authored scene profiles remain immutable - owner: PlayerStressVFX
            _runtimeVolume.profile = _runtimeProfile;

            _runtimeVignette = _runtimeProfile.Add<Vignette>(true);
            _runtimeVignette.active = true;
            _runtimeVignette.intensity.overrideState = true;
            _runtimeVignette.smoothness.overrideState = true;
            _runtimeVignette.rounded.overrideState = true;
            _runtimeVignette.intensity.value = 0f;
            _runtimeVignette.smoothness.value = 0.6f;
            _runtimeVignette.rounded.value = true;

            _runtimeChromatic = _runtimeProfile.Add<ChromaticAberration>(true);
            _runtimeChromatic.active = true;
            _runtimeChromatic.intensity.overrideState = true;
            _runtimeChromatic.intensity.value = 0f;

            _runtimeColorAdjustments = _runtimeProfile.Add<ColorAdjustments>(true);
            _runtimeColorAdjustments.active = true;
            _runtimeColorAdjustments.contrast.overrideState = true;
            _runtimeColorAdjustments.saturation.overrideState = true;
            _runtimeColorAdjustments.colorFilter.overrideState = true;
            _runtimeColorAdjustments.contrast.value = 0f;
            _runtimeColorAdjustments.saturation.value = 0f;
            _runtimeColorAdjustments.colorFilter.value = Color.white;
        }

        private void TryRegisterTickHandler()
        {
            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null || _registered)
                return;

            tickManager.Register((ITickable)this);
            _registered = true;
        }

        private void TryUnregisterTickHandler()
        {
            if (!_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister((ITickable)this);

            _registered = false;
        }

        private float ResolveStress01()
        {
            float oxygenNormalized = _survivalSystem != null ? Mathf.Clamp01(_survivalSystem.OxygenNormalized) : 1f;
            float integrityNormalized = _survivalSystem != null ? Mathf.Clamp01(_survivalSystem.IntegrityNormalized) : 1f;
            float fatalPressure01 = _playerMovement != null ? Mathf.Clamp01(_playerMovement.CurrentFatalPressureSequence01) : 0f;
            float oxygenStress01 = Mathf.Clamp01(Mathf.InverseLerp(oxygenCriticalThreshold, 0.05f, oxygenNormalized));
            float integrityStress01 = Mathf.Clamp01(Mathf.InverseLerp(integrityCriticalThreshold, 0.08f, integrityNormalized));
            float stress01 = Mathf.Clamp01(Mathf.Max(oxygenStress01, integrityStress01, fatalPressure01));

            _debugOxygenNormalized = oxygenNormalized;
            _debugIntegrityNormalized = integrityNormalized;
            _debugFatalPressure01 = fatalPressure01;
            return stress01;
        }

        private void ApplyStressPulse(float stress01, float beat01, float fog01, float frost01)
        {
            if (_runtimeVignette == null || _runtimeChromatic == null || _runtimeColorAdjustments == null)
                return;

            float pulse = stress01 * (0.35f + beat01 * 0.65f);
            float combinedVignette = Mathf.Clamp01(pulse + frost01 * 0.58f + fog01 * 0.18f);
            _runtimeVignette.intensity.value = maxVignetteIntensity * combinedVignette;
            _runtimeVignette.smoothness.value = Mathf.Lerp(0.55f, maxVignetteSmoothness, Mathf.Max(pulse, frost01));
            _runtimeVignette.color.overrideState = true;
            _runtimeVignette.color.value = Color.Lerp(Color.black, frostTint, frost01);
            _runtimeChromatic.intensity.value = maxChromaticIntensity * Mathf.Clamp01(pulse + fog01 * 0.22f);
            _runtimeColorAdjustments.contrast.value = -fogContrastLoss * fog01;
            _runtimeColorAdjustments.saturation.value = -fogSaturationLoss * fog01;
            _runtimeColorAdjustments.colorFilter.value = Color.Lerp(Color.white, frostTint, frost01 * 0.45f + fog01 * 0.18f);
        }

        private void ResetRuntimeEffects()
        {
            if (_runtimeVignette != null)
            {
                _runtimeVignette.intensity.value = 0f;
                _runtimeVignette.smoothness.value = 0.6f;
                _runtimeVignette.color.overrideState = true;
                _runtimeVignette.color.value = Color.black;
            }

            if (_runtimeChromatic != null)
                _runtimeChromatic.intensity.value = 0f;

            if (_runtimeColorAdjustments != null)
            {
                _runtimeColorAdjustments.contrast.value = 0f;
                _runtimeColorAdjustments.saturation.value = 0f;
                _runtimeColorAdjustments.colorFilter.value = Color.white;
            }

            _debugFog01 = 0f;
            _debugFrost01 = 0f;
            _debugTemperatureShock01 = 0f;
            _debugPulse01 = 0f;
        }

        private void PlayHeartbeat(float stress01)
        {
            if (heartbeatClip == null || !SpatialAudioManager.TryGetInstance(out SpatialAudioManager audioManager))
                return;

            float volume = Mathf.Lerp(heartbeatVolumeMin, heartbeatVolumeMax, stress01);
            audioManager.PlayStatic2D(heartbeatClip, volume, audioManager.InterfaceGroup);
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
            _debugFog01 = Mathf.Clamp01(Mathf.Max(oxygenFog01, oxygenGraceFog01, thermalShock01, _survivalSystem.RapidAscentRisk01 * 0.4f));
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
