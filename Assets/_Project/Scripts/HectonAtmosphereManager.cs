// ══════════════════════════════════════════════════════════════════
// HectonAtmosphereManager.cs  v4.2
// Орбитальная модель солнца + время суток + затмения + _SunDirection
//
// ═══════════════════════════════════════════════════════════════
// v4.2 CHANGES:
// ═══════════════════════════════════════════════════════════════
//
//   [TASK #1] EDITOR SCENE VIEW SUPPORT:
//     Added [ExecuteAlways] attribute.
//     Added Update() with #if UNITY_EDITOR guard:
//       If !Application.isPlaying, calls Tick() with safe deltaTime.
//       This ensures ProfileSunIntensity and ComputedHorizonFade
//       are computed when flying the Scene View camera in Edit Mode.
//     Protected singleton, event subscriptions, and GameTickManager
//       registration from running in Edit Mode.
//
// ═══════════════════════════════════════════════════════════════
// v4.1 FIXES (preserved):
// ═══════════════════════════════════════════════════════════════
//
//   [FIX] sunLight.intensity NO LONGER WRITTEN by AtmosphereManager.
//     ComputeSunValues() stores results in _computedSunIntensity
//     and _computedHorizonFade. sunLight.intensity is NOT touched.
//     UnderwaterVisuals is SOLE AUTHORITY for sunLight.intensity.
//
// ОТВЕТСТВЕННОСТИ (v4.2 — CLARIFIED):
//   • Цикл дня/ночи (вращение Directional Light)
//   • Расчёт TimeOfDay, SunAngle, SunElevation
//   • Детекция EnvironmentState (DAY/NIGHT/ECLIPSE/UNDERWATER)
//   • Передача _SunDirection в глобальные шейдеры
//   • Расчёт ProfileSunIntensity = profile × transition
//   • Расчёт ComputedHorizonFade = smoothstep по SunElevation
//   • Уведомление через OnStateChanged
//   • Biome atmosphere overrides (profile switching)
//   • Edit Mode: Tick via Update() for Scene View preview
//
// НЕ ДЕЛАЕТ (v4.2 — EXPLICIT):
//   ✗ sunLight.intensity — NEVER WRITES (UnderwaterVisuals is authority)
//   ✗ RenderSettings.fog / fogColor / fogDensity
//   ✗ RenderSettings.ambientLight
//   ✗ Camera.clearFlags / backgroundColor
//   ✗ Depth-based fog
//
// КООРДИНАЦИЯ (v4.2):
//   AtmosphereManager → ProfileSunIntensity (data only, no light write)
//                     → ComputedHorizonFade (data only)
//   UnderwaterVisuals → reads both → sunLight.intensity = profile × horizon × depth
//   CelestialEngine   → reads sunLight.intensity → *= occlusion
// ══════════════════════════════════════════════════════════════════

using System;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

namespace Hecton8.Atmosphere
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Atmosphere Manager")]
    [ExecuteAlways] // v4.2: Scene View support in Edit Mode
    public class HectonAtmosphereManager : MonoBehaviour, ITickable
    {
        #region ══════════ AtmosphereSnapshot ══════════

        private struct AtmosphereSnapshot
        {
            public float  skyExposure;
            public float  sunIntensity;

            public static AtmosphereSnapshot Default => new AtmosphereSnapshot
            {
                skyExposure  = 1f,
                sunIntensity = 1f
            };

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static AtmosphereSnapshot FromProfile(AtmosphereProfile p)
            {
                return new AtmosphereSnapshot
                {
                    skyExposure  = p.skyExposure,
                    sunIntensity = p.sunIntensity
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
                    sunIntensity = math.lerp(from.sunIntensity, to.sunIntensity, t)
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
                    _instance = FindFirstObjectByType<HectonAtmosphereManager>();
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

        // ═══ v4.1: Computed values (read by other systems) ═══

        private float _computedHorizonFade;
        private float _computedSunIntensity;

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
        /// Raw profile sun intensity AFTER transition interpolation,
        /// BEFORE horizon fade and depth absorption.
        /// </summary>
        public float ProfileSunIntensity => _currentValues.sunIntensity;

        /// <summary>
        /// Horizon fade factor [0..1].
        /// 0 = sun below horizon (night), 1 = sun fully above (day).
        /// </summary>
        public float ComputedHorizonFade => _computedHorizonFade;

        /// <summary>
        /// Computed sun intensity = profile × horizonFade.
        /// NOT written to sunLight.intensity.
        /// </summary>
        public float CurrentSunIntensity => _computedSunIntensity;

        #endregion

        #region ══════════ Lifecycle ══════════

        private void Awake()
        {
            // v4.2: Singleton only in Play Mode to avoid Edit Mode conflicts
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
                // Edit Mode: set instance for property access but don't destroy duplicates
                _instance = this;
            }

            _registeredToTickManager = false;

            InitializeCycleTimer();
            InitializeAtmosphereValues();
        }

        private void OnEnable()
        {
            // v4.2: Only register tick manager and events in Play Mode
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

            // v4.2: Only clear events in Play Mode
            if (Application.isPlaying)
                OnStateChanged = null;
        }

        // ═══════════════════════════════════════════════════════════
        // v4.2 TASK #1: EDIT MODE UPDATE
        // ═══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        /// <summary>
        /// v4.2: In Edit Mode, Unity calls Update() thanks to [ExecuteAlways].
        /// We use this to drive Tick() so that ProfileSunIntensity,
        /// ComputedHorizonFade, sun rotation, and _SunDirection shader global
        /// are all computed when flying the Scene View camera.
        ///
        /// In Play Mode, Tick() is driven by GameTickManager — this does nothing.
        /// </summary>
        private void Update()
        {
            if (Application.isPlaying) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) dt = 0.016f; // safety fallback for edit mode

            Tick(dt);
        }
#endif

#if UNITY_EDITOR
        private void OnValidate()
        {
            _cycleDuration = math.max(_cycleDuration, 1f);

            if (_sunLight == null)
            {
                var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
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

            // FIX: вычислить позицию солнца и derived values немедленно,
            // чтобы первое чтение ProfileSunIntensity другими скриптами
            // получило корректные данные до первого Tick().
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

            Shader.SetGlobalVector(
                _shaderID_SunDirection,
                new Vector4(sunForward.x, sunForward.y, sunForward.z, 0f));
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

            // v4.2: Only fire events in Play Mode
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

            AtmosphereProfile biomeProfile = null;

            if (_biomeOverrides != null)
            {
                int count = _biomeOverrides.Length;
                for (int i = 0; i < count; i++)
                {
                    if (_biomeOverrides[i].biomeID == biomeID)
                    {
                        biomeProfile = _biomeOverrides[i].profile;
                        break;
                    }
                }
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