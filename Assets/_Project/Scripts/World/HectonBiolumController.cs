// ============================================================================
// HECTON-8 — HectonBiolumController.cs
// Upravlenie globalnoy biolyuminestsentsiey.
//
// LOR:
//   • Biolyuminestsentsiya usilivaetsya vo vremya zatmeniya (lor1).
//   • Reagiruet na puls signala Atlas-6 (lor3 Blok Z).
//   • Na glubine 500m+ — postoyannaya biolyuminestsentsiya (lor2).
//   • Kristallicheskie derevya svetyatsya ot davleniya (lor2 Razdel 7).
//
// ARHITEKTURA:
//   • Publikuet _BiolumIntensity, _BiolumPulseTime v globalnye sheydery.
//   • ISlowTickable — plavnoe izmenenie intensivnosti.
//   • Slushaet EclipseGameplayEvents i AtlasSignalEvents.
// ============================================================================

using Hecton8.AtlasSignal;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Visor;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-85)]
    public sealed class HectonBiolumController : MonoBehaviour, ISlowTickable, IAtlasSignalEventListener, IDepthZoneEventListener, ISonarPulseEventListener, IEclipseGameplayEventListener, IServiceHeartbeat, IServiceShutdown
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Base Intensity ──────────────────────────")]
        [Tooltip("Bazovaya intensivnost biolyuminestsentsii.")]
        [SerializeField, Range(0f, 1f)] private float baseIntensity = 0.15f;

        [Tooltip("Intensivnost na glubine > 500m.")]
        [SerializeField, Range(0f, 1f)] private float deepIntensity = 0.45f;

        [Tooltip("Glubina perehoda k deep intensity (metry).")]
        [SerializeField] private float deepTransitionDepth = 500f;

        [Header("── Eclipse Boost ────────────────────────────")]
        [Tooltip("Mnozhitel vo vremya zatmeniya.")]
        [SerializeField, Range(1f, 5f)] private float eclipseMultiplier = 2f;
        [SerializeField, Min(0.01f)] private float eclipseMultiplierSmoothRate = 1.25f;

        [Header("── Signal Pulse ────────────────────────────")]
        [Tooltip("Dopolnitelnaya intensivnost pri pulse signala Atlas-6.")]
        [SerializeField, Range(0f, 0.5f)] private float signalPulseBoost = 0.2f;

        [Tooltip("Skorost zatuhaniya pulsa.")]
        [SerializeField] private float pulseDecayRate = 0.5f;

        [Header("── Sonar Communication ────────────────────")]
        [Tooltip("Dopolnitelnaya intensivnost otklika biolyuma na aktivnyy sonar pulse igroka.")]
        [SerializeField, Range(0f, 0.35f)] private float sonarPulseBoost = 0.12f;

        [Tooltip("Normalizuyuschiy radius sonar pulse dlya rascheta biolyuminestsentnogo otklika.")]
        [SerializeField] private float sonarReferenceRadius = 100f;

        [Header("── References ──────────────────────────────")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;
        [SerializeField] private Light[] localProxyLights;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

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
        private bool _runtimeRegistered;

        // _BiolumIntensity is a vector global owned by HectonBiolumManager.
        private static readonly int _ShaderLegacyBiolumIntensity = Shader.PropertyToID("_HectonLegacyBiolumIntensity");
        private static readonly int _ShaderBiolumPulseTime  = Shader.PropertyToID("_BiolumPulseTime");

        public ServiceHeartbeatState HeartbeatState => _runtimeRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady => _runtimeRegistered;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            HectonBiolumController registered = GlobalRegistry.BiolumController;
            if (registered != null && registered != this) { Destroy(gameObject); return; }
        }

        private void OnEnable()
        {
            if (!TryRegisterRuntime())
                return;

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
            TryUnregisterRuntime();

            EclipseGameplayEvents.Unregister(this);
            AtlasSignalEvents.Unregister(this);
            DepthZoneEvents.Unregister(this);
            SpectrumEvents.UnregisterSonarPulseListener(this);

            _targetEclipseMultiplier = 1f;
            _currentEclipseMultiplier = 1f;
            ApplyLocalProxyLights();
            Shader.SetGlobalFloat(_ShaderLegacyBiolumIntensity, 0f);
            Shader.SetGlobalFloat(_ShaderBiolumPulseTime, 0f);
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterRuntime();
            EclipseGameplayEvents.Unregister(this);
            AtlasSignalEvents.Unregister(this);
            DepthZoneEvents.Unregister(this);
            SpectrumEvents.UnregisterSonarPulseListener(this);

        }

        public void OnServiceShutdown()
        {
            TryUnregister();
            TryUnregisterRuntime();
            EclipseGameplayEvents.Unregister(this);
            AtlasSignalEvents.Unregister(this);
            DepthZoneEvents.Unregister(this);
            SpectrumEvents.UnregisterSonarPulseListener(this);
            _localProxyLightBaseIntensities = null;
            _atlasPulseBurst = 0f;
            _sonarPulseBurst = 0f;
            _targetEclipseMultiplier = 1f;
            _currentEclipseMultiplier = 1f;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (survivalSystem == null && !ResolveSurvivalSystem())
                return;

            const float dt = 0.5f;

            // Vychislyaem tselevuyu intensivnost
            float depth = survivalSystem != null ? survivalSystem.Depth : 0f;
            float transitionDepth = deepTransitionDepth > 1f ? deepTransitionDepth : 1f;
            float depthFactor = depth >= transitionDepth ? 1f : depth / transitionDepth;
            if (depthFactor < 0f)
                depthFactor = 0f;

            float target = baseIntensity + (deepIntensity - baseIntensity) * depthFactor;

            _currentEclipseMultiplier = MoveTowardsFast(
                _currentEclipseMultiplier,
                _targetEclipseMultiplier,
                eclipseMultiplierSmoothRate * dt);

            target *= _currentEclipseMultiplier;

            _targetIntensity = target;

            // Plavnoe izmenenie + pulse burst
            _currentIntensity = MoveTowardsFast(_currentIntensity, _targetIntensity, 0.05f * dt / 0.5f);

            if (_atlasPulseBurst > 0f)
            {
                _atlasPulseBurst = math.max(0f, _atlasPulseBurst - pulseDecayRate * dt);
            }

            if (_sonarPulseBurst > 0f)
            {
                _sonarPulseBurst = math.max(0f, _sonarPulseBurst - pulseDecayRate * dt);
            }

            ApplyShader();
            ApplyLocalProxyLights();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private static float MoveTowardsFast(float current, float target, float maxDelta)
        {
            float delta = target - current;
            float safeDelta = math.max(0f, maxDelta);
            if (math.abs(delta) <= safeDelta)
                return target;

            return current + (math.sign(delta) * safeDelta);
        }

        private void ApplyShader()
        {
            Shader.SetGlobalFloat(_ShaderLegacyBiolumIntensity, _currentIntensity + _atlasPulseBurst + _sonarPulseBurst);
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
            if (!active)
                _targetEclipseMultiplier = 1f;
        }

        private void HandleEclipseBiolumMultiplier(float multiplier)
        {
            float localMax = Mathf.Max(1f, eclipseMultiplier * 3f);
            float clampedMultiplier = Mathf.Clamp(multiplier, 1f, localMax);
            _targetEclipseMultiplier = clampedMultiplier;
            if (clampedMultiplier >= Mathf.Max(1f, eclipseMultiplier) - 0.001f)
            {
                float previousMultiplier = Mathf.Max(0.001f, _currentEclipseMultiplier);
                float baseIntensityWithoutEclipse = _currentIntensity / previousMultiplier;
                _currentEclipseMultiplier = clampedMultiplier;
                _currentIntensity = Mathf.Max(_currentIntensity, baseIntensityWithoutEclipse * clampedMultiplier);
                _targetIntensity = Mathf.Max(_targetIntensity, baseIntensityWithoutEclipse * clampedMultiplier);
                ApplyShader();
                ApplyLocalProxyLights();
            }
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

        private bool TryRegisterRuntime()
        {
            if (_runtimeRegistered)
                return true;

            if (!Application.isPlaying)
                return false;

            HectonBiolumController registered = GlobalRegistry.BiolumController;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterBiolumControllerRuntime(this);
            _runtimeRegistered = ReferenceEquals(GlobalRegistry.BiolumController, this);
            return _runtimeRegistered;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registered = false;
        }

        private void TryUnregisterRuntime()
        {
            if (!_runtimeRegistered)
                return;

            GlobalRegistry.UnregisterBiolumControllerRuntime(this);
            _runtimeRegistered = false;
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
