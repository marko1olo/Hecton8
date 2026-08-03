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
//   • ISlowTickable — plavnoe izmenenie lokalnyh proxy-light reaktsiy.
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
    public sealed class HectonBiolumController : MonoBehaviour, ISlowTickable, ILateFrameTickable, IAtlasSignalEventListener, IDepthZoneEventListener, ISonarPulseEventListener, IEclipseGameplayEventListener, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
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
        private bool _lateFrameRegistered;
        private bool _runtimeRegistered;
        private bool _hotSwapRegistered;
        private bool _localProxyLightsDirty;
        private DepthZoneProfile _activeDepthZone;
        private float _activeZoneBiolumIntensity;
        private IPlayerRuntimeContext _playerRuntimeContext;

        public ServiceHeartbeatState HeartbeatState => _runtimeRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady => _runtimeRegistered;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            CachePlayerRuntimeContext(GlobalRegistry.Player, null);
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            if (!TryRegisterRuntime())
                return;

            TryRegisterHotSwapListener();
            TryRegister();

            CachePlayerRuntimeContext(GlobalRegistry.Player, null);

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
            _activeDepthZone = null;
            _activeZoneBiolumIntensity = 0f;
            _localProxyLightsDirty = true;
        }

        public void LateFrameTick()
        {
            // L19 hop2 LIVE: batch peel LateFrameTick - biolum GPU/visual sync hang headless after VERBSWEEP.
            if (UnityEngine.Application.isBatchMode)
                return;

            if (!_localProxyLightsDirty)
                return;

            _localProxyLightsDirty = false;
            ApplyLocalProxyLights();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterRuntime();
            TryUnregisterHotSwapListener();

            EclipseGameplayEvents.Unregister(this);
            AtlasSignalEvents.Unregister(this);
            DepthZoneEvents.Unregister(this);
            SpectrumEvents.UnregisterSonarPulseListener(this);

            _targetEclipseMultiplier = 1f;
            _currentEclipseMultiplier = 1f;
            _atlasPulseBurst = 0f;
            _sonarPulseBurst = 0f;
            _activeDepthZone = null;
            _activeZoneBiolumIntensity = 0f;
            RestoreLocalProxyLightBaselines();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterRuntime();
            TryUnregisterHotSwapListener();
            EclipseGameplayEvents.Unregister(this);
            AtlasSignalEvents.Unregister(this);
            DepthZoneEvents.Unregister(this);
            SpectrumEvents.UnregisterSonarPulseListener(this);
            _activeDepthZone = null;
            _activeZoneBiolumIntensity = 0f;
            RestoreLocalProxyLightBaselines();
        }

        public void OnServiceShutdown()
        {
            TryUnregister();
            TryUnregisterRuntime();
            TryUnregisterHotSwapListener();
            EclipseGameplayEvents.Unregister(this);
            AtlasSignalEvents.Unregister(this);
            DepthZoneEvents.Unregister(this);
            SpectrumEvents.UnregisterSonarPulseListener(this);
            RestoreLocalProxyLightBaselines();
            _playerRuntimeContext = null;
            _atlasPulseBurst = 0f;
            _sonarPulseBurst = 0f;
            _targetEclipseMultiplier = 1f;
            _currentEclipseMultiplier = 1f;
            _activeDepthZone = null;
            _activeZoneBiolumIntensity = 0f;
            _localProxyLightBaseIntensities = null;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            // L19 hop2 LIVE: batch peel SlowTick - biolum slow path hang headless after VERBSWEEP.
            if (UnityEngine.Application.isBatchMode)
                return;

            const float dt = 0.5f;

            // Vychislyaem tselevuyu intensivnost
            float depth = ResolveCurrentDepthMeters();
            float transitionDepth = deepTransitionDepth > 1f ? deepTransitionDepth : 1f;
            float depthFactor = depth >= transitionDepth ? 1f : depth / transitionDepth;
            if (depthFactor < 0f)
                depthFactor = 0f;

            float target = baseIntensity + (deepIntensity - baseIntensity) * depthFactor;
            target = math.max(target, _activeZoneBiolumIntensity);

            _currentEclipseMultiplier = MoveTowardsFast(
                _currentEclipseMultiplier,
                _targetEclipseMultiplier,
                eclipseMultiplierSmoothRate * dt);

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

            QueueLocalProxyLightPresentation();
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
                if (proxyLight == null)
                {
                    _localProxyLightBaseIntensities[i] = 0f;
                    continue;
                }

                proxyLight.shadows = LightShadows.None;
                _localProxyLightBaseIntensities[i] = proxyLight.intensity;
            }
        }

        private void ApplyLocalProxyLights()
        {
            int count = localProxyLights != null ? localProxyLights.Length : 0;
            if (count <= 0 || _localProxyLightBaseIntensities == null)
                return;

            float multiplier = ResolveLocalProxyLightMultiplier();
            int limit = Mathf.Min(count, _localProxyLightBaseIntensities.Length);
            for (int i = 0; i < limit; i++)
            {
                Light proxyLight = localProxyLights[i];
                if (proxyLight == null)
                    continue;

                proxyLight.intensity = _localProxyLightBaseIntensities[i] * multiplier;
            }
        }

        private void RestoreLocalProxyLightBaselines()
        {
            int count = localProxyLights != null ? localProxyLights.Length : 0;
            if (count <= 0 || _localProxyLightBaseIntensities == null)
                return;

            int limit = Mathf.Min(count, _localProxyLightBaseIntensities.Length);
            for (int i = 0; i < limit; i++)
            {
                Light proxyLight = localProxyLights[i];
                if (proxyLight != null)
                    proxyLight.intensity = _localProxyLightBaseIntensities[i];
            }
        }

        private float ResolveLocalProxyLightMultiplier()
        {
            float glowReaction = math.max(0f, _currentIntensity + _atlasPulseBurst + _sonarPulseBurst);
            float eclipse = math.max(0f, _currentEclipseMultiplier);
            return (1f + glowReaction) * eclipse;
        }

        private void QueueLocalProxyLightPresentation()
        {
            _localProxyLightsDirty = true;
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
                _currentEclipseMultiplier = clampedMultiplier;
                QueueLocalProxyLightPresentation();
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
            QueueLocalProxyLightPresentation();
        }

        private void HandleSonarPulse(float radius)
        {
            float normalizedRadius = Mathf.Clamp01(radius / Mathf.Max(1f, sonarReferenceRadius));
            if (normalizedRadius <= 0f)
            {
                return;
            }

            _sonarPulseBurst = Mathf.Max(_sonarPulseBurst, sonarPulseBoost * normalizedRadius);
            QueueLocalProxyLightPresentation();
        }

        void ISonarPulseEventListener.OnSonarPulse(float radius)
        {
            HandleSonarPulse(radius);
        }

        private void HandleDepthZoneEntered(DepthZoneProfile zone)
        {
            if (zone == null)
                return;

            _activeDepthZone = zone;
            _activeZoneBiolumIntensity = math.saturate(zone.ambience.biolumIntensity);
            _targetIntensity = math.max(_targetIntensity, _activeZoneBiolumIntensity);
            QueueLocalProxyLightPresentation();
        }

        private void HandleDepthZoneExited(DepthZoneProfile zone)
        {
            if (zone != null && !ReferenceEquals(zone, _activeDepthZone))
                return;

            _activeDepthZone = null;
            _activeZoneBiolumIntensity = 0f;
            QueueLocalProxyLightPresentation();
        }

        private void CachePlayerRuntimeContext(
            IPlayerRuntimeContext currentPlayerContext,
            IPlayerRuntimeContext previousPlayerContext)
        {
            if (previousPlayerContext != null &&
                ReferenceEquals(survivalSystem, previousPlayerContext.SurvivalSystem))
            {
                survivalSystem = null;
            }

            _playerRuntimeContext = currentPlayerContext;
            HectonSurvivalSystem contextSurvival = currentPlayerContext != null
                ? currentPlayerContext.SurvivalSystem
                : null;

            if (contextSurvival != null)
                survivalSystem = contextSurvival;
        }

        private float ResolveCurrentDepthMeters()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                return math.max(0f, movementState.DepthMeters);
            }

            if (playerContext != null)
                return 0f;

            HectonSurvivalSystem currentSurvival = survivalSystem;
            if (currentSurvival != null && math.isfinite(currentSurvival.Depth))
                return math.max(0f, currentSurvival.Depth);

            return 0f;
        }

        void IDepthZoneEventListener.OnDepthZoneEntered(DepthZoneProfile zone)
        {
            HandleDepthZoneEntered(zone);
        }

        void IDepthZoneEventListener.OnDepthZoneExited(DepthZoneProfile zone)
        {
            HandleDepthZoneExited(zone);
        }

        private void TryRegister()
        {
            if ((_registered && _lateFrameRegistered) || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registered)
                _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private bool TryRegisterRuntime()
        {
            if (_runtimeRegistered)
                return true;

            if (!Application.isPlaying)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterBiolumControllerRuntime(this);
            _runtimeRegistered = ReferenceEquals(GlobalRegistry.BiolumController, this);
            return _runtimeRegistered;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            HectonBiolumController registered = GlobalRegistry.BiolumController;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsBiolumControllerRuntimeUsable(registered))
            {
                Destroy(gameObject);
                return true;
            }

            GlobalRegistry.UnregisterBiolumControllerRuntime(registered);
            return false;
        }

        private static bool IsBiolumControllerRuntimeUsable(HectonBiolumController controller)
        {
            return controller != null && controller._runtimeRegistered && controller.isActiveAndEnabled;
        }

        private void TryUnregister()
        {
            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            if (_registered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registered = false;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                CachePlayerRuntimeContext(
                    currentService as IPlayerRuntimeContext,
                    previousService as IPlayerRuntimeContext);
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregister();
            if (currentService != null && isActiveAndEnabled)
                TryRegister();
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
