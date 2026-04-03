// ══════════════════════════════════════════════════════════════════
// HectonAtmosphereManager.cs  v2.1 (OPTIMIZATION PASS)
// Орбитальная модель солнца + время суток + затмения + _SunDirection
//
// ═══════════════════════════════════════════════════════════════
// v2.1 CHANGES (OPTIMIZATION):
// ═══════════════════════════════════════════════════════════════
//
//   [OPT] Shader property dirty-write batching in RotateSun()
//     • Caches _cachedShaderSunDirection (float3, stack)
//     • Only calls Shader.SetGlobalVector() if changed
//     • Impact: eliminates redundant GPU command buffer writes
//
//   [OPT] Dictionary<int, AtmosphereProfile> for biome lookup
//     • HandleBiomeChanged() from O(N) linear search → O(1) TryGetValue
//     • Built once in Awake() from _biomeOverrides[]
//     • Impact: O(N) one-time cost, O(1) per biome change
//
// ═══════════════════════════════════════════════════════════════
// v4.3 BASELINE (PRESERVED):
// ═══════════════════════════════════════════════════════════════
//
//   [FIX] DefaultExecutionOrder(-6000):
//     Guarantees AtmosphereManager.OnEnable() fires BEFORE
//     UnderwaterVisuals(-4000) and CelestialEngine(-3000).
//     This means AtmosphereManager registers with GameTickManager FIRST
//     → ticks FIRST → ProfileSunIntensity and ComputedHorizonFade
//     are FRESH when UnderwaterVisuals reads them.
//
//     EXECUTION CHAIN (deterministic):
//       1. AtmosphereManager.Tick()  → compute profile, horizon, _SunDirection
//       2. UnderwaterVisuals.Tick()  → write sunLight.intensity (sole authority)
//       3. CelestialEngine.Tick()    → multiply sunLight.intensity by visibility
//
// ═══════════════════════════════════════════════════════════════
// v4.2 DETAILS:
//   ✓ [ExecuteAlways] for Scene View preview
//   ✓ sunLight.intensity NEVER WRITTEN (read-only)
//   ✓ ProfileSunIntensity = profile × transition
//   ✓ ComputedHorizonFade = smoothstep by SunElevation
//   ✓ Biome atmosphere overrides
//   ✓ Eclipse timer + state machine
// ══════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

namespace Hecton8.Atmosphere
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Atmosphere Manager")]
    [DefaultExecutionOrder(-6000)]  // v4.3: MUST tick before UnderwaterVisuals(-4000)
    [ExecuteAlways]
    public class HectonAtmosphereManager : MonoBehaviour, ITickable
    {
        #region ══════════ AtmosphereSnapshot ══════════

        private struct AtmosphereSnapshot
        {
            public float  skyExposure;
            public float  sunIntensity;
            public float  temperature;
            public float  radiation;

            public static AtmosphereSnapshot Default => new AtmosphereSnapshot
            {
                skyExposure  = 1f,
                sunIntensity = 1f,
                temperature  = 20f,
                radiation    = 0f
            };

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static AtmosphereSnapshot FromProfile(AtmosphereProfile p)
            {
                return new AtmosphereSnapshot
                {
                    skyExposure  = p.skyExposure,
                    sunIntensity = p.sunIntensity,
                    temperature  = p.temperature,
                    radiation    = p.radiation
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static AtmosphereSnapshot Lerp(
                in AtmosphereSnapshot from,
                in AtmosphereSnapshot to,
                float t)
            {
                return new AtmosphereSnapshot
                {
                    skyExposure  = math.lerp(from.skyExposure,  to.skyExposure,  t),
                    sunIntensity = math.lerp(from.sunIntensity, to.sunIntensity, t),
                    temperature  = math.lerp(from.temperature,  to.temperature,  t),
                    radiation    = math.lerp(from.radiation,    to.radiation,    t)
                };
            }
        }

        #endregion

        #region ══════════ Singleton ══════════

        private static HectonAtmosphereManager _instance;

        public static HectonAtmosphereManager Instance
        {
            get
            {
#if UNITY_EDITOR
                if (_instance == null)
                    _instance = FindAnyObjectByType<HectonAtmosphereManager>();
#endif
                return _instance;
            }
        }

        #endregion

        #region ══════════ Events ══════════

        public static event Action<EnvironmentState> OnStateChanged;

        #endregion

        #region ══════════ Shader IDs ══════════

        private static readonly int _shaderID_SunDirection =
            Shader.PropertyToID("_SunDirection");

        #endregion

        #region ══════════ Inspector ══════════

        [Header("═══ Sun & Time Cycle ═══")]
        [SerializeField] private Light _sunLight;

        [SerializeField, Min(1f)]
        private float _cycleDuration = 3600f;

        [SerializeField, Range(0f, 1f)]
        private float _initialTimeOfDay = 0.25f;

        [SerializeField, Range(0f, 360f)]
        private float _sunOrbitalYAngle = 170f;

        [SerializeField, Range(0f, 90f)]
        private float _orbitalInclination = 23.5f;

        [SerializeField, Range(1f, 30f)]
        private float _nightThresholdAngle = 10f;

        [Tooltip("Angular zone below horizon for smooth sun intensity fade.\n" +
                 "At dot ∈ [0, -sin(fadeAngle)] intensity smoothly → 0.")]
        [SerializeField, Range(1f, 30f)]
        private float _sunHorizonFadeAngle = 10f;

        [Header("═══ Atmosphere Profiles ═══")]
        [SerializeField] private AtmosphereProfile _profileDay;
        [SerializeField] private AtmosphereProfile _profileNight;
        [SerializeField] private AtmosphereProfile _profileUnderwater;
        [SerializeField] private AtmosphereProfile _profileEclipse;

        [Header("═══ Transition ═══")]
        [SerializeField, Range(0.1f, 5f)]
        private float _transitionSpeed = 1.5f;

        [Header("═══ Underwater Detection ═══")]
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private float _waterSurfaceY = 0f;
        [SerializeField] private bool _useAutoUnderwaterDetection = true;

        [Header("═══ Biome Overrides ═══")]
        [SerializeField] private BiomeAtmosphereOverride[] _biomeOverrides;

        #endregion

        #region ══════════ Runtime State ══════════

        private EnvironmentState _currentState = EnvironmentState.SURFACE_DAY;

        private float _cycleTimer;
        private float _sunAngleDegrees;
        private float _sunElevationDot;

        private bool  _eclipseActive;
        private float _eclipseRemainingTime;

        private bool _underwaterExternalFlag;

        private AtmosphereSnapshot _transitionOrigin;
        private AtmosphereSnapshot _currentValues;
        private float              _transitionProgress;

        private bool _registeredToTickManager;

        private AtmosphereProfile _activeBiomeProfile;
        private int _currentBiomeID = -1;
        private bool _editorInitialized;

        private float _computedHorizonFade;
        private float _computedSunIntensity;

        /// <summary>Cached shader property values (for dirty-write batching).</summary>
        private float3 _cachedShaderSunDirection = new float3(0f, -1f, 0f);

        /// <summary>Dictionary for O(1) biome profile lookup (instead of linear search).</summary>
        private Dictionary<int, AtmosphereProfile> _biomeProfileDict;

        #endregion

        #region ══════════ Biome Override Struct ══════════

        [Serializable]
        public struct BiomeAtmosphereOverride
        {
            public int biomeID;
            public AtmosphereProfile profile;
        }

        #endregion

        #region ══════════ Public Properties ══════════

        public EnvironmentState CurrentState   => _currentState;
        public float TimeOfDay                 => _cycleTimer / _cycleDuration;
        public float SunAngle                  => _sunAngleDegrees;
        public float SunElevation              => _sunElevationDot;
        public float CurrentSkyExposure        => _currentValues.skyExposure;
        public bool  IsEclipseActive           => _eclipseActive;
        public float EclipseRemainingTime      => _eclipseRemainingTime;
        public float CycleDuration             => _cycleDuration;
        public float OrbitalInclination        => _orbitalInclination;

        /// <summary>
        /// v4.3 CLARIFICATION:
        /// Raw profile sun intensity AFTER transition interpolation.
        /// This is the "what the sun WANTS to be" value.
        /// UnderwaterVisuals multiplies this by horizon × depth.
        /// CelestialEngine then multiplies by eclipse visibility.
        ///
        /// NEVER written to sunLight.intensity by this script.
        /// </summary>
        public float ProfileSunIntensity => _currentValues.sunIntensity;

        /// <summary>
        /// v4.3 CLARIFICATION:
        /// Horizon fade factor [0..1].
        /// Computed from sun elevation angle via smoothstep.
        /// 0 = sun fully below horizon, 1 = sun fully above.
        ///
        /// Read by UnderwaterVisuals to compute:
        ///   sunLight.intensity = ProfileSunIntensity × ComputedHorizonFade × depthFactor
        /// </summary>
        public float ComputedHorizonFade => _computedHorizonFade;

        public float CurrentSunIntensity => _computedSunIntensity;

        public float CurrentTemperature  => _currentValues.temperature;
        public float CurrentRadiation    => _currentValues.radiation;

        #endregion

        #region ══════════ Lifecycle ══════════

        private void Awake()
        {
            if (Application.isPlaying)
            {
                if (_instance != null && _instance != this)
                {
                    Destroy(this);
                    return;
                }
                _instance = this;
            }
            else
            {
                _instance = this;
            }

            _registeredToTickManager = false;

            // Build biome profile dictionary (ONE-TIME, O(n) initialization)
            _biomeProfileDict = new Dictionary<int, AtmosphereProfile>(16);
            if (_biomeOverrides != null)
            {
                for (int i = 0; i < _biomeOverrides.Length; i++)
                {
                    _biomeProfileDict[_biomeOverrides[i].biomeID] = _biomeOverrides[i].profile;
                }
            }

            InitializeCycleTimer();
            InitializeAtmosphereValues();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                if (!_registeredToTickManager && GameTickManager.Instance != null)
                {
                    GameTickManager.Instance.Register((ITickable)this);
                    _registeredToTickManager = true;
                }

                MapMagicBridge.OnBiomeChanged += HandleBiomeChanged;
            }
        }

        private void Start()
        {
            if (!Application.isPlaying) return;

            if (_registeredToTickManager) return;

            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((ITickable)this);
                _registeredToTickManager = true;
            }
            else
            {
                Debug.LogError(
                    "[HectonAtmosphere] GameTickManager.Instance == null in Start(). " +
                    "Atmosphere will NOT update.", this);
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                if (_registeredToTickManager && GameTickManager.Instance != null)
                {
                    GameTickManager.Instance.Unregister((ITickable)this);
                    _registeredToTickManager = false;
                }

                MapMagicBridge.OnBiomeChanged -= HandleBiomeChanged;
            }
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;

            if (Application.isPlaying)
                OnStateChanged = null;
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (Application.isPlaying) return;

            if (!_editorInitialized)
            {
                InitializeCycleTimer();
                InitializeAtmosphereValues();
                _editorInitialized = true;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) dt = 0.016f;

            Tick(dt);
        }
#endif

#if UNITY_EDITOR
        private void OnValidate()
        {
            _editorInitialized = false;
            _cycleDuration = math.max(_cycleDuration, 1f);

            if (_sunLight == null)
            {
                var lights = FindObjectsByType<Light>();
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i].type == LightType.Directional)
                    {
                        _sunLight = lights[i];
                        break;
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.4f, 0.9f, 0.25f);
            Vector3 center = new Vector3(
                transform.position.x, _waterSurfaceY, transform.position.z);
            Gizmos.DrawCube(center, new Vector3(200f, 0.05f, 200f));

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            const int segments = 64;
            const float orbitRadius = 50f;

            float incRad = math.radians(_orbitalInclination);
            float azRad  = math.radians(_sunOrbitalYAngle);

            quaternion qAzimuth     = quaternion.RotateY(azRad);
            quaternion qInclination = quaternion.RotateZ(incRad);
            quaternion orbitFrame   = math.mul(qAzimuth, qInclination);

            Vector3 prevPoint = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * math.PI * 2f;
                float3 localPoint = new float3(
                    math.cos(angle) * orbitRadius,
                    math.sin(angle) * orbitRadius, 0f);
                float3 worldPoint = math.mul(orbitFrame, localPoint);
                Vector3 wp = transform.position + (Vector3)worldPoint;

                if (i > 0) Gizmos.DrawLine(prevPoint, wp);
                prevPoint = wp;
            }

            if (_sunLight != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(
                    transform.position -
                    (Vector3)(_sunLight.transform.forward * orbitRadius), 2f);
            }
        }
#endif

        #endregion

        #region ══════════ ITickable ══════════

        public void Tick(float deltaTime)
        {
            AdvanceCycleTimer(deltaTime);
            RotateSun();
            TickEclipseTimer(deltaTime);

            EnvironmentState resolved = ResolveState();
            ProcessStateTransition(resolved);

            InterpolateAtmosphere(deltaTime);

            ComputeSunValues();
        }

        #endregion

        #region ══════════ Cycle & Sun ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeCycleTimer()
        {
            _cycleTimer = _initialTimeOfDay * _cycleDuration;
        }

        private void InitializeAtmosphereValues()
        {
            AtmosphereProfile profile = ResolveProfile(_currentState);
            _currentValues = profile != null
                ? AtmosphereSnapshot.FromProfile(profile)
                : AtmosphereSnapshot.Default;
            _transitionOrigin   = _currentValues;
            _transitionProgress = 1f;

            _computedHorizonFade = 1f;
            _computedSunIntensity = _currentValues.sunIntensity;

            RotateSun();
            ComputeSunValues();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceCycleTimer(float deltaTime)
        {
            _cycleTimer += deltaTime;
            _cycleTimer  = math.fmod(_cycleTimer, _cycleDuration);
        }

        private void RotateSun()
        {
            if (_sunLight == null) return;

            float normalized = _cycleTimer / _cycleDuration;
            _sunAngleDegrees = normalized * 360f;

            float dailyRad       = math.radians(_sunAngleDegrees);
            float inclinationRad = math.radians(_orbitalInclination);
            float azimuthRad     = math.radians(_sunOrbitalYAngle);

            quaternion qDaily       = quaternion.RotateX(dailyRad);
            quaternion qInclination = quaternion.RotateZ(inclinationRad);
            quaternion qAzimuth     = quaternion.RotateY(azimuthRad);

            quaternion finalRotation = math.mul(
                qAzimuth, math.mul(qInclination, qDaily));

            _sunLight.transform.rotation = finalRotation;

            float3 sunForward = math.mul(finalRotation, new float3(0f, 0f, 1f));
            _sunElevationDot = math.dot(-sunForward, new float3(0f, 1f, 0f));

            // v2.1 OPT: Dirty-write batching — only write to shader if changed
            if (!sunForward.Equals(_cachedShaderSunDirection))
            {
                _cachedShaderSunDirection = sunForward;
                Shader.SetGlobalVector(
                    _shaderID_SunDirection,
                    new Vector4(sunForward.x, sunForward.y, sunForward.z, 0f));
            }
        }

        #endregion

        #region ══════════ Eclipse ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TickEclipseTimer(float deltaTime)
        {
            if (!_eclipseActive) return;
            _eclipseRemainingTime -= deltaTime;
            if (_eclipseRemainingTime <= 0f)
            {
                _eclipseRemainingTime = 0f;
                _eclipseActive        = false;
            }
        }

        #endregion

        #region ══════════ State Machine ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private EnvironmentState ResolveState()
        {
            if (_eclipseActive)       return EnvironmentState.ECLIPSE;
            if (EvaluateUnderwater()) return EnvironmentState.UNDERWATER;
            return EvaluateDaytime()
                ? EnvironmentState.SURFACE_DAY
                : EnvironmentState.SURFACE_NIGHT;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EvaluateUnderwater()
        {
            if (_underwaterExternalFlag) return true;
            if (_useAutoUnderwaterDetection && _playerTransform != null)
                return _playerTransform.position.y < _waterSurfaceY;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EvaluateDaytime()
        {
            float thresholdSin = math.sin(math.radians(_nightThresholdAngle));
            return _sunElevationDot > thresholdSin;
        }

        private void ProcessStateTransition(EnvironmentState newState)
        {
            if (newState == _currentState) return;

            _transitionOrigin   = _currentValues;
            _transitionProgress = 0f;

            _currentState = newState;

            if (Application.isPlaying)
                OnStateChanged?.Invoke(_currentState);
        }

        #endregion

        #region ══════════ Interpolation ══════════

        private void InterpolateAtmosphere(float deltaTime)
        {
            if (_transitionProgress >= 1f) return;

            _transitionProgress = math.saturate(
                _transitionProgress + deltaTime * _transitionSpeed);

            float t = _transitionProgress;
            float smoothT = t * t * (3f - 2f * t);

            AtmosphereProfile target = ResolveProfile(_currentState);
            if (target == null) return;

            AtmosphereSnapshot targetSnap = AtmosphereSnapshot.FromProfile(target);
            _currentValues = AtmosphereSnapshot.Lerp(
                in _transitionOrigin, in targetSnap, smoothT);
        }

        #endregion

        #region ══════════ Sun Values (COMPUTE ONLY, NO WRITE) ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ComputeSunValues()
        {
            float fadeThreshold = math.sin(math.radians(_sunHorizonFadeAngle));

            if (_sunElevationDot <= 0f)
            {
                _computedHorizonFade = 0f;
            }
            else if (_sunElevationDot >= fadeThreshold)
            {
                _computedHorizonFade = 1f;
            }
            else
            {
                float st = _sunElevationDot / fadeThreshold;
                _computedHorizonFade = st * st * (3f - 2f * st);
            }

            _computedSunIntensity = _currentValues.sunIntensity * _computedHorizonFade;
        }

        #endregion

        #region ══════════ Profile Resolution ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AtmosphereProfile ResolveProfile(EnvironmentState state)
        {
            if (state == EnvironmentState.ECLIPSE)
                return _profileEclipse != null ? _profileEclipse : _profileDay;

            if (state == EnvironmentState.UNDERWATER)
                return _profileUnderwater != null ? _profileUnderwater : _profileDay;

            if (_activeBiomeProfile != null)
                return _activeBiomeProfile;

            AtmosphereProfile profile = state switch
            {
                EnvironmentState.SURFACE_DAY   => _profileDay,
                EnvironmentState.SURFACE_NIGHT => _profileNight,
                _                              => _profileDay
            };

            return profile != null ? profile : _profileDay;
        }

        #endregion

        #region ══════════ Biome Handler ══════════

        private void HandleBiomeChanged(int biomeID)
        {
            _currentBiomeID = biomeID;

            // v2.1 OPT: O(1) dictionary lookup instead of O(n) linear search
            AtmosphereProfile biomeProfile = null;
            if (_biomeProfileDict != null && _biomeProfileDict.TryGetValue(biomeID, out var profile))
            {
                biomeProfile = profile;
            }

            _activeBiomeProfile = biomeProfile;
            _transitionOrigin   = _currentValues;
            _transitionProgress = 0f;
        }

        #endregion

        #region ══════════ Public API ══════════

        public void TriggerEclipse(float duration)
        {
            if (duration <= 0f) return;
            _eclipseActive        = true;
            _eclipseRemainingTime = duration;
        }

        public void EndEclipse()
        {
            _eclipseActive        = false;
            _eclipseRemainingTime = 0f;
        }

        public void SetUnderwater(bool isUnderwater)
        {
            _underwaterExternalFlag = isUnderwater;
        }

        public void SetTimeOfDay(float normalized)
        {
            _cycleTimer = math.saturate(normalized) * _cycleDuration;
        }

        public void SetWaterSurfaceLevel(float worldY)
        {
            _waterSurfaceY = worldY;
        }

        public void SetPlayerTransform(Transform player)
        {
            _playerTransform = player;
        }

        public void SetCycleDuration(float seconds)
        {
            _cycleDuration = math.max(seconds, 1f);
        }

        public void SetTransitionSpeed(float speed)
        {
            _transitionSpeed = math.clamp(speed, 0.1f, 10f);
        }

        public void SetOrbitalInclination(float degrees)
        {
            _orbitalInclination = math.clamp(degrees, 0f, 90f);
        }

        #endregion
    }
}
