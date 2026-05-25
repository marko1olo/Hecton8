using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.World;
using NASAPunk.Visor;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CelestialCataclysmSystem : MonoBehaviour, ISlowTickable, ILateFrameTickable, IRandomEventListener, IGlobalRegistryHotSwapListener
    {
        private const ushort SolarFlareSourceId = 0xA811;
        private const int MaxMeteorFogShadowCount = 4;

        [Header("Solar EMP Flare")]
        [SerializeField, Min(1f)] private float solarEmpRadiusMeters = 250000f;
        [SerializeField, Min(0.1f)] private float solarEmpDurationSeconds = 30f;
        [SerializeField, Range(0f, 1f)] private float solarEmpClaritySuppression01 = 1f;
        [SerializeField, Range(0f, 2f)] private float solarEmpVisorGlitchDurationSeconds = 1.2f;

        [Header("Lunar Resonance")]
        [SerializeField] private FloraRegrowthDirector floraRegrowthDirector;
        [SerializeField, Range(1f, 5f)] private float lunarResonanceGrowthMultiplier = 3f;
        [SerializeField, Min(0.5f)] private float lunarResonanceHoldSeconds = 12f;

        [Header("Meteor Fog Shadows")]
        [SerializeField, Min(1f)] private float meteorFogShadowRadiusMeters = 90f;
        [SerializeField, Range(0f, 1f)] private float meteorFogShadowStrength = 0.42f;
        [SerializeField, Min(1f)] private float meteorFogShadowDurationSeconds = 45f;

        // COLD ALLOC: Vector4[4] - global meteor fog shadow upload payload - owner: CelestialCataclysmSystem
        private readonly Vector4[] _meteorFogShadowPayload = new Vector4[MaxMeteorFogShadowCount];
        // COLD ALLOC: List<VisorHUDController>[4] - EMP visor pulse dispatch scratch - owner: CelestialCataclysmSystem
        private static readonly List<VisorHUDController> s_visorControllers = new List<VisorHUDController>(4);

        private bool _registered;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _reportedMissingFloraDirector;
        private bool _reportedMissingEmpVisorController;
        private bool _meteorFogShadowsDirty;
        private bool _solarEmpGlitchDirty;
        private float _pendingMeteorFogShadowIntensity01;
        private float _pendingSolarEmpGlitchIntensity01;
        private float _meteorFogShadowRemainingSeconds;
        private float _solarEmpGlitchRemainingSeconds;
        private float _solarEmpGlitchDurationSeconds;
        private float _solarEmpGlitchIntensity01;
        private HectonCelestialEngine _celestialEngine;

        private static readonly int _MeteorFogShadowPositionsId = Shader.PropertyToID("_MeteorFogShadowPositions");
        private static readonly int _MeteorFogShadowParamsId = Shader.PropertyToID("_MeteorFogShadowParams");
        private static readonly int _SolarEmpGlitchParamsId = Shader.PropertyToID("_SolarEmpGlitchParams");
        private static readonly uint _CataclysmContextHash = unchecked((uint)LocHash.Compute("CelestialCataclysmSystem"));
        private static readonly uint _FloraDirectorMissingWarningHash = unchecked((uint)LocHash.Compute("CelestialCataclysm.FloraDirectorMissing"));
        private static readonly uint _SolarEmpNoVisorControllerWarningHash = unchecked((uint)LocHash.Compute("CelestialCataclysm.SolarEmpNoVisorController"));

        private void OnEnable()
        {
            _celestialEngine = GlobalRegistry.CelestialEngine;
            TryRegisterHotSwapListener();
            TryRegister();
            RandomEventEvents.Register(this);
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
            RandomEventEvents.Unregister(this);
            _meteorFogShadowRemainingSeconds = 0f;
            _solarEmpGlitchRemainingSeconds = 0f;
            PublishMeteorFogShadowsImmediate(0f);
            PublishSolarEmpGlitchGlobalsImmediate(0f);
            s_visorControllers.Clear();
            TryUnregisterLateFrame();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
            RandomEventEvents.Unregister(this);
            TryUnregisterLateFrame();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (!isActiveAndEnabled)
                return;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (currentService == null)
                        return;

                    TryUnregister();
                    TryUnregisterLateFrame();
                    TryRegister();
                    if (_meteorFogShadowsDirty || _solarEmpGlitchDirty)
                        TryRegisterLateFrame();
                    break;
                case GlobalRegistryServiceSlot.CelestialEngineRuntime:
                    _celestialEngine = currentService as HectonCelestialEngine;
                    break;
            }
        }

        /// <summary>
        /// Advances low-frequency resonance and meteor-fog shadow state.
        /// </summary>
        public void SlowTick()
        {
            ApplyLunarResonanceIfActive();
            AdvanceMeteorFogShadows(0.5f);
            AdvanceSolarEmpGlitch(0.5f);
        }

        public void LateFrameTick()
        {
            FlushQueuedCataclysmVisuals();
            if (!_meteorFogShadowsDirty && !_solarEmpGlitchDirty)
                TryUnregisterLateFrame();
        }

        /// <summary>
        /// Applies cataclysm consequences for celestial random events.
        /// </summary>
        /// <param name="type">Random event type raised by the event system.</param>
        /// <param name="intensity">Normalized event intensity.</param>
        public void OnRandomEventStarted(RandomEventType type, float intensity)
        {
            switch (type)
            {
                case RandomEventType.MeteorShower:
                    _meteorFogShadowRemainingSeconds = Mathf.Max(_meteorFogShadowRemainingSeconds, meteorFogShadowDurationSeconds);
                    PublishMeteorFogShadows(Mathf.Clamp01(intensity));
                    break;
                case RandomEventType.SolarFlare:
                    PublishSolarEmpFlare(intensity);
                    break;
            }
        }

        /// <summary>
        /// Receives random-event completion notifications.
        /// </summary>
        /// <param name="type">Random event type that ended.</param>
        public void OnRandomEventEnded(RandomEventType type)
        {
            if (type != RandomEventType.SolarFlare)
                return;

            _solarEmpGlitchRemainingSeconds = 0f;
            _solarEmpGlitchDurationSeconds = 0f;
            _solarEmpGlitchIntensity01 = 0f;
            PublishSolarEmpGlitchGlobals(0f);
        }

        /// <summary>
        /// Unused seismic listener slot required by the random-event listener contract.
        /// </summary>
        /// <param name="payload">Incoming seismic shockwave payload.</param>
        public void OnSeismicShockwave(in SeismicShockwaveEvent payload)
        {
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void PublishSolarEmpFlare(float intensity)
        {
            Vector3 origin = ResolvePlayerPosition();
            PhysicsEventBus.TryNotifyElectromagneticPulse(new ElectromagneticPulseEvent(
                origin,
                Mathf.Max(1f, solarEmpRadiusMeters),
                Mathf.Max(0.1f, solarEmpDurationSeconds),
                Mathf.Clamp01(solarEmpClaritySuppression01 * Mathf.Max(0.1f, intensity)),
                (uint)DamageTypeMask.Emp,
                SolarFlareSourceId));
            TriggerSolarEmpVisualGlitch(intensity);
        }

        private void ApplyLunarResonanceIfActive()
        {
            HectonCelestialEngine celestialEngine = _celestialEngine;
            if (celestialEngine == null || !celestialEngine.IsLunarResonanceActive)
                return;

            FloraRegrowthDirector director = floraRegrowthDirector;
            if (director != null)
            {
                _reportedMissingFloraDirector = false;
                director.ApplyLunarResonance(Mathf.Max(1f, lunarResonanceGrowthMultiplier), lunarResonanceHoldSeconds);
                return;
            }

            PublishOnce(ref _reportedMissingFloraDirector, _FloraDirectorMissingWarningHash, lunarResonanceGrowthMultiplier);
        }

        private void AdvanceMeteorFogShadows(float deltaTime)
        {
            if (_meteorFogShadowRemainingSeconds <= 0f)
                return;

            _meteorFogShadowRemainingSeconds = math.max(0f, _meteorFogShadowRemainingSeconds - math.max(0f, deltaTime));
            float invMeteorDuration = math.rcp(math.max(0.1f, meteorFogShadowDurationSeconds));
            PublishMeteorFogShadows(math.saturate(_meteorFogShadowRemainingSeconds * invMeteorDuration));
        }

        private void PublishMeteorFogShadows(float intensity01)
        {
            _pendingMeteorFogShadowIntensity01 = math.saturate(intensity01);
            _meteorFogShadowsDirty = true;
            TryRegisterLateFrame();
        }

        private void PublishMeteorFogShadowsImmediate(float intensity01)
        {
            Vector3 playerPosition = ResolvePlayerPosition();
            float eventAge = Mathf.Max(0f, meteorFogShadowDurationSeconds - _meteorFogShadowRemainingSeconds);
            for (int i = 0; i < MaxMeteorFogShadowCount; i++)
            {
                float seed = (i + 1) * 37.13f;
                float angle = seed + eventAge * (0.19f + i * 0.041f);
                Vector3 offset = new Vector3(CinematicMath.FastCos(angle), 0f, CinematicMath.FastSin(angle)) * (120f + i * 55f);
                offset += Vector3.up * (70f + i * 18f);
                _meteorFogShadowPayload[i] = new Vector4(
                    playerPosition.x + offset.x,
                    playerPosition.y + offset.y,
                    playerPosition.z + offset.z,
                    Mathf.Max(1f, meteorFogShadowRadiusMeters));
            }

            Shader.SetGlobalVectorArray(_MeteorFogShadowPositionsId, _meteorFogShadowPayload);
            Shader.SetGlobalVector(
                _MeteorFogShadowParamsId,
                new Vector4(
                    intensity01 > 0.001f ? MaxMeteorFogShadowCount : 0,
                    Mathf.Clamp01(meteorFogShadowStrength * intensity01),
                    Mathf.Max(0f, eventAge),
                    0f));
        }

        private void TriggerSolarEmpVisualGlitch(float intensity)
        {
            float clampedIntensity = Mathf.Clamp01(intensity);
            if (clampedIntensity <= 0f || solarEmpVisorGlitchDurationSeconds <= 0f)
            {
                PublishSolarEmpGlitchGlobals(0f);
                return;
            }

            float duration = Mathf.Max(0.05f, solarEmpVisorGlitchDurationSeconds);
            _solarEmpGlitchDurationSeconds = duration;
            _solarEmpGlitchRemainingSeconds = duration;
            _solarEmpGlitchIntensity01 = clampedIntensity;
            PublishSolarEmpGlitchGlobals(clampedIntensity);

            VisorHUDController.CopyActiveControllersTo(s_visorControllers);
            if (s_visorControllers.Count == 0)
                PublishOnce(ref _reportedMissingEmpVisorController, _SolarEmpNoVisorControllerWarningHash, duration);
            else
                _reportedMissingEmpVisorController = false;

            for (int i = 0; i < s_visorControllers.Count; i++)
            {
                VisorHUDController controller = s_visorControllers[i];
                if (controller != null)
                    controller.GlitchPulse(duration);
            }

            s_visorControllers.Clear();
        }

        private void AdvanceSolarEmpGlitch(float deltaTime)
        {
            if (_solarEmpGlitchRemainingSeconds <= 0f)
                return;

            _solarEmpGlitchRemainingSeconds = Mathf.Max(0f, _solarEmpGlitchRemainingSeconds - Mathf.Max(0f, deltaTime));
            if (_solarEmpGlitchRemainingSeconds <= 0f)
            {
                _solarEmpGlitchDurationSeconds = 0f;
                _solarEmpGlitchIntensity01 = 0f;
                PublishSolarEmpGlitchGlobals(0f);
                return;
            }

            float remaining01 = Mathf.Clamp01(_solarEmpGlitchRemainingSeconds / Mathf.Max(0.05f, _solarEmpGlitchDurationSeconds));
            PublishSolarEmpGlitchGlobals(_solarEmpGlitchIntensity01 * remaining01);
        }

        private void PublishSolarEmpGlitchGlobals(float intensity01)
        {
            _pendingSolarEmpGlitchIntensity01 = Mathf.Clamp01(intensity01);
            _solarEmpGlitchDirty = true;
            TryRegisterLateFrame();
        }

        private void PublishSolarEmpGlitchGlobalsImmediate(float intensity01)
        {
            Shader.SetGlobalVector(
                _SolarEmpGlitchParamsId,
                new Vector4(
                    Mathf.Clamp01(intensity01),
                    Mathf.Max(0f, _solarEmpGlitchRemainingSeconds),
                    Time.unscaledTime,
                    Mathf.Clamp01(solarEmpClaritySuppression01)));
        }

        private void FlushQueuedCataclysmVisuals()
        {
            if (_meteorFogShadowsDirty)
            {
                _meteorFogShadowsDirty = false;
                PublishMeteorFogShadowsImmediate(_pendingMeteorFogShadowIntensity01);
            }

            if (_solarEmpGlitchDirty)
            {
                _solarEmpGlitchDirty = false;
                PublishSolarEmpGlitchGlobalsImmediate(_pendingSolarEmpGlitchIntensity01);
            }
        }

        private static void PublishPerformanceWarning(uint warningHash, float scalarValue)
        {
            GlobalTelemetryBus.PublishPerformanceWarning(warningHash, _CataclysmContextHash, scalarValue);
        }

        private static void PublishOnce(ref bool latch, uint warningHash, float scalarValue)
        {
            if (latch)
                return;

            latch = true;
            PublishPerformanceWarning(warningHash, scalarValue);
        }

        private Vector3 ResolvePlayerPosition()
        {
            return GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null
                ? playerTransform.position
                : transform.position;
        }
    }
}
