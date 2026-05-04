// ============================================================================
// HECTON-8 — HectonBiolumController.cs
// Управление глобальной биолюминесценцией.
//
// ЛОР:
//   • Биолюминесценция усиливается во время затмения (лор1).
//   • Реагирует на пульс сигнала Атлас-6 (лор3 Блок З).
//   • На глубине 500м+ — постоянная биолюминесценция (лор2).
//   • Кристаллические деревья светятся от давления (лор2 Раздел 7).
//
// АРХИТЕКТУРА:
//   • Публикует _BiolumIntensity, _BiolumPulseTime в глобальные шейдеры.
//   • ISlowTickable — плавное изменение интенсивности.
//   • Слушает EclipseGameplayEvents и AtlasSignalEvents.
// ============================================================================

using Hecton8.AtlasSignal;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Visor;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-85)]
    public sealed class HectonBiolumController : MonoBehaviour, ISlowTickable, IAtlasSignalEventListener, IDepthZoneEventListener, ISonarPulseEventListener, IEclipseGameplayEventListener
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Base Intensity ──────────────────────────")]
        [Tooltip("Базовая интенсивность биолюминесценции.")]
        [SerializeField, Range(0f, 1f)] private float baseIntensity = 0.15f;

        [Tooltip("Интенсивность на глубине > 500м.")]
        [SerializeField, Range(0f, 1f)] private float deepIntensity = 0.45f;

        [Tooltip("Глубина перехода к deep intensity (метры).")]
        [SerializeField] private float deepTransitionDepth = 500f;

        [Header("── Eclipse Boost ────────────────────────────")]
        [Tooltip("Множитель во время затмения.")]
        [SerializeField, Range(1f, 3f)] private float eclipseMultiplier = 1.5f;
        [SerializeField, Min(0.01f)] private float eclipseMultiplierSmoothRate = 1.25f;

        [Header("── Signal Pulse ────────────────────────────")]
        [Tooltip("Дополнительная интенсивность при пульсе сигнала Атлас-6.")]
        [SerializeField, Range(0f, 0.5f)] private float signalPulseBoost = 0.2f;

        [Tooltip("Скорость затухания пульса.")]
        [SerializeField] private float pulseDecayRate = 0.5f;

        [Header("── Sonar Communication ────────────────────")]
        [Tooltip("Дополнительная интенсивность отклика биолюма на активный sonar pulse игрока.")]
        [SerializeField, Range(0f, 0.35f)] private float sonarPulseBoost = 0.12f;

        [Tooltip("Нормализующий радиус sonar pulse для расчета биолюминесцентного отклика.")]
        [SerializeField] private float sonarReferenceRadius = 100f;

        [Header("── References ──────────────────────────────")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;
        [SerializeField] private Light[] localProxyLights;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static HectonBiolumController Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private float _currentIntensity;
        private float _targetIntensity;
        private float _atlasPulseBurst;
        private float _sonarPulseBurst;
        private float _currentEclipseMultiplier = 1f;
        private float _targetEclipseMultiplier = 1f;
        private float[] _localProxyLightBaseIntensities;
        private bool  _eclipseActive;
        private bool  _registered;

        private static readonly int _ShaderBiolumIntensity  = Shader.PropertyToID("_BiolumIntensity");
        private static readonly int _ShaderBiolumPulseTime  = Shader.PropertyToID("_BiolumPulseTime");

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            TryRegister();

            ResolveSurvivalSystem();

            EclipseGameplayEvents.Register(this);
            AtlasSignalEvents.Register(this);
            DepthZoneEvents.Register(this);
            SpectrumEvents.RegisterSonarPulseListener(this);

            CacheLocalProxyLightBaselines();
            _currentIntensity = baseIntensity;
            _currentEclipseMultiplier = 1f;
            _targetEclipseMultiplier = 1f;
            _atlasPulseBurst = 0f;
            _sonarPulseBurst = 0f;
            ApplyShader();
            ApplyLocalProxyLights();
        }

        private void OnDisable()
        {
            TryUnregister();

            EclipseGameplayEvents.Unregister(this);
            AtlasSignalEvents.Unregister(this);
            DepthZoneEvents.Unregister(this);
            SpectrumEvents.UnregisterSonarPulseListener(this);

            _targetEclipseMultiplier = 1f;
            _currentEclipseMultiplier = 1f;
            ApplyLocalProxyLights();
            Shader.SetGlobalFloat(_ShaderBiolumIntensity, baseIntensity);
            Shader.SetGlobalFloat(_ShaderBiolumPulseTime, 0f);
        }

        private void OnDestroy()
        {
            TryUnregister();
            EclipseGameplayEvents.Unregister(this);
            SpectrumEvents.UnregisterSonarPulseListener(this);

            if (Instance == this)
                Instance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (survivalSystem == null && !ResolveSurvivalSystem())
                return;

            const float dt = 0.5f;

            // Вычисляем целевую интенсивность
            float depth = survivalSystem != null ? survivalSystem.Depth : 0f;
            float depthFactor = depth >= deepTransitionDepth ? 1f :
                depth / Mathf.Max(1f, deepTransitionDepth);

            float target = Mathf.Lerp(baseIntensity, deepIntensity, depthFactor);

            _currentEclipseMultiplier = Mathf.MoveTowards(
                _currentEclipseMultiplier,
                _targetEclipseMultiplier,
                eclipseMultiplierSmoothRate * dt);

            target *= _currentEclipseMultiplier;

            _targetIntensity = target;

            // Плавное изменение + pulse burst
            _currentIntensity = Mathf.MoveTowards(_currentIntensity, _targetIntensity, 0.05f * dt / 0.5f);

            if (_atlasPulseBurst > 0f)
            {
                _atlasPulseBurst = Mathf.Max(0f, _atlasPulseBurst - pulseDecayRate * dt);
            }

            if (_sonarPulseBurst > 0f)
            {
                _sonarPulseBurst = Mathf.Max(0f, _sonarPulseBurst - pulseDecayRate * dt);
            }

            ApplyShader();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void ApplyShader()
        {
            Shader.SetGlobalFloat(_ShaderBiolumIntensity, _currentIntensity + _atlasPulseBurst + _sonarPulseBurst);
        }

        private void CacheLocalProxyLightBaselines()
        {
            int count = localProxyLights != null ? localProxyLights.Length : 0;
            if (count <= 0)
                return;

            if (_localProxyLightBaseIntensities == null || _localProxyLightBaseIntensities.Length != count)
                _localProxyLightBaseIntensities = new float[count]; // COLD ALLOC: float[count] - authored local biolum proxy light baseline cache - owner: HectonBiolumController

            for (int i = 0; i < count; i++)
            {
                Light proxyLight = localProxyLights[i];
                _localProxyLightBaseIntensities[i] = proxyLight != null ? proxyLight.intensity : 0f;
            }
        }

        private void ApplyLocalProxyLights()
        {
            int count = localProxyLights != null ? localProxyLights.Length : 0;
            if (count <= 0 || _localProxyLightBaseIntensities == null)
                return;

            int limit = Mathf.Min(count, _localProxyLightBaseIntensities.Length);
            for (int i = 0; i < limit; i++)
            {
                Light proxyLight = localProxyLights[i];
                if (proxyLight == null)
                    continue;

                proxyLight.intensity = _localProxyLightBaseIntensities[i] * _currentEclipseMultiplier;
            }
        }

        private void HandleEclipsePhase(bool active)
        {
            _eclipseActive = active;
            _targetEclipseMultiplier = active ? Mathf.Max(1f, eclipseMultiplier) : 1f;
        }

        private void HandleEclipseBiolumMultiplier(float multiplier)
        {
            float localMax = Mathf.Max(1f, eclipseMultiplier);
            _targetEclipseMultiplier = Mathf.Clamp(multiplier, 1f, localMax);
        }

        void IEclipseGameplayEventListener.OnEclipseGameplayPhaseChanged(bool active)
        {
            HandleEclipsePhase(active);
        }

        void IEclipseGameplayEventListener.OnNightPredatorsRising(float intensity)
        {
        }

        void IEclipseGameplayEventListener.OnEclipseTemperatureDelta(float delta)
        {
        }

        void IEclipseGameplayEventListener.OnEclipseBiolumMultiplierChanged(float multiplier)
        {
            HandleEclipseBiolumMultiplier(multiplier);
        }

        public void OnAtlasSignalEvent(in AtlasSignalEventPayload payload)
        {
            if ((AtlasSignalEventType)payload.EventType == AtlasSignalEventType.Pulse)
                HandleSignalPulse(payload.SignalStrength);
        }

        private void HandleSignalPulse(float intensity)
        {
            _atlasPulseBurst = Mathf.Max(_atlasPulseBurst, signalPulseBoost * intensity);
            Shader.SetGlobalFloat(_ShaderBiolumPulseTime, Time.time);
            ApplyShader();
            ApplyLocalProxyLights();
        }

        private void HandleSonarPulse(float radius)
        {
            float normalizedRadius = Mathf.Clamp01(radius / Mathf.Max(1f, sonarReferenceRadius));
            if (normalizedRadius <= 0f)
            {
                return;
            }

            _sonarPulseBurst = Mathf.Max(_sonarPulseBurst, sonarPulseBoost * normalizedRadius);
            Shader.SetGlobalFloat(_ShaderBiolumPulseTime, Time.time);
            ApplyShader();
        }

        void ISonarPulseEventListener.OnSonarPulse(float radius)
        {
            HandleSonarPulse(radius);
        }

        private void HandleDepthZoneEntered(DepthZoneProfile zone)
        {
            if (zone == null) return;
            // Zone-specific biolum intensity from profile
            float zoneBiolum = zone.ambience.biolumIntensity;
            if (zoneBiolum > 0.01f)
            {
                // Blend zone biolum with current depth-based intensity
                _targetIntensity = Mathf.Max(_targetIntensity, zoneBiolum);
            }
        }

        private bool ResolveSurvivalSystem()
        {
            if (survivalSystem != null)
                return true;

            if (!BootstrapState.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            return playerTransform.TryGetComponent(out survivalSystem);
        }

        void IDepthZoneEventListener.OnDepthZoneEntered(DepthZoneProfile zone)
        {
            HandleDepthZoneEntered(zone);
        }

        void IDepthZoneEventListener.OnDepthZoneExited(DepthZoneProfile zone)
        {
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registered = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            deepTransitionDepth = Mathf.Max(1f, deepTransitionDepth);
            pulseDecayRate = Mathf.Max(0.01f, pulseDecayRate);
            sonarReferenceRadius = Mathf.Max(1f, sonarReferenceRadius);
            eclipseMultiplierSmoothRate = Mathf.Max(0.01f, eclipseMultiplierSmoothRate);
        }
#endif
    }
}
