// ══════════════════════════════════════════════════════════════════
// HectonAtmosphereManager.cs
// Центральная система управления атмосферой экзолуны Гектон
// Unity 6 | Universal Render Pipeline | GameTickManager Integration
//
// Ответственность:
//   • Цикл дня/ночи (вращение Directional Light по наклонной орбите)
//   • Автоматическое определение состояния среды
//   • Плавная интерполяция параметров атмосферы
//   • Управление камерой под водой (clearFlags / backgroundColor)
//   • Уведомление внешних систем через событие OnStateChanged
//   • Интеграция с URP Volume (опционально)
//   • Передача направления солнца в глобальный шейдер-вектор _SunDirection
//
// Орбитальная модель солнца:
//   Солнце движется по наклонной орбите, определяемой тремя параметрами:
//     - Daily Rotation (timeOfDay → 0°–360° вокруг орбитальной оси)
//     - Orbital Inclination (наклон плоскости орбиты к мировой вертикали)
//     - Sun Azimuth (поворот всей орбитальной плоскости вокруг Y)
//   Это позволяет получить реалистичное освещение с «серповидными» фазами
//   газового гиганта и разнообразными углами восхода/заката.
//
// Приоритет состояний:
//   ECLIPSE → UNDERWATER → SURFACE_NIGHT / SURFACE_DAY
//
// Подводный рендер:
//   При UNDERWATER состоянии менеджер переключает Camera.clearFlags
//   на SolidColor и устанавливает backgroundColor = fogColor.
//   Переход плавный — backgroundColor интерполируется вместе с fogColor
//   через AtmosphereSnapshot и _transitionProgress.
//
// Zero-GC гарантия:
//   Метод Tick() не содержит аллокаций. Все Shader Property ID
//   кэшированы статически. Unity.Mathematics для всех расчётов.
//
// Интеграция с GameTickManager:
//   Реализует ITickable. Регистрация: OnEnable → попытка, Start → fallback.
//   Debug.LogError только если Instance == null даже в Start.
//   Метод Update() отсутствует.
//
// КООРДИНАЦИЯ С HectonCelestialEngine:
//   AtmosphereManager is the AUTHORITY for sunLight.intensity.
//   CelestialEngine reads the resulting intensity and MODULATES it
//   by occlusion factor — never overwrites with its own base value.
// ══════════════════════════════════════════════════════════════════

using System;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;

namespace Hecton8.Atmosphere
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Atmosphere Manager")]
    public class HectonAtmosphereManager : MonoBehaviour, ITickable
    {
        #region ══════════ Снимок атмосферы (struct — без аллокаций) ══════════

        /// <summary>
        /// Набор интерполируемых параметров атмосферы.
        /// Структура живёт на стеке — никаких аллокаций в куче.
        /// </summary>
        private struct AtmosphereSnapshot
        {
            public Color fogColor;
            public float fogDensity;
            public float skyExposure;
            public Color ambientColor;
            public float sunIntensity;

            /// <summary>Нейтральные значения по умолчанию.</summary>
            public static AtmosphereSnapshot Default => new AtmosphereSnapshot
            {
                fogColor     = new Color(0.7f, 0.7f, 0.8f, 1f),
                fogDensity   = 0.01f,
                skyExposure  = 1f,
                ambientColor = new Color(0.5f, 0.5f, 0.5f, 1f),
                sunIntensity = 1f
            };

            /// <summary>Создаёт снимок из ScriptableObject-профиля.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static AtmosphereSnapshot FromProfile(AtmosphereProfile p)
            {
                return new AtmosphereSnapshot
                {
                    fogColor     = p.fogColor,
                    fogDensity   = p.fogDensity,
                    skyExposure  = p.skyExposure,
                    ambientColor = p.ambientColor,
                    sunIntensity = p.sunIntensity
                };
            }

            /// <summary>
            /// Интерполяция между двумя снимками.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static AtmosphereSnapshot Lerp(
                in AtmosphereSnapshot from,
                in AtmosphereSnapshot to,
                float t)
            {
                return new AtmosphereSnapshot
                {
                    fogColor     = Color.Lerp(from.fogColor,     to.fogColor,     t),
                    fogDensity   = math.lerp(from.fogDensity,    to.fogDensity,   t),
                    skyExposure  = math.lerp(from.skyExposure,   to.skyExposure,  t),
                    ambientColor = Color.Lerp(from.ambientColor, to.ambientColor, t),
                    sunIntensity = math.lerp(from.sunIntensity,  to.sunIntensity, t)
                };
            }
        }

        #endregion

        #region ══════════ Синглтон ══════════

        private static HectonAtmosphereManager _instance;

        /// <summary>
        /// Единственный экземпляр менеджера атмосферы.
        /// В Editor-режиме выполняет поиск, если ссылка потеряна.
        /// </summary>
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

        #region ══════════ Глобальное событие ══════════

        /// <summary>
        /// Срабатывает при каждой смене состояния окружающей среды.
        /// </summary>
        public static event Action<EnvironmentState> OnStateChanged;

        #endregion

        #region ══════════ Кэшированные Shader Property ID ══════════

        private static readonly int _shaderID_SunDirection = Shader.PropertyToID("_SunDirection");

        #endregion

        #region ══════════ Сериализуемые настройки ══════════

        [Header("═══ Солнце и Цикл Времени ═══")]
        [Tooltip("Directional Light, играющий роль солнца экзолуны")]
        [SerializeField] private Light _sunLight;

        [Tooltip("Длительность полного цикла дня/ночи в секундах (3600 = 60 мин)")]
        [SerializeField, Min(1f)]
        private float _cycleDuration = 3600f;

        [Tooltip("Начальное нормализованное время суток.\n0 = восход, 0.25 = полдень, 0.5 = закат, 0.75 = полночь")]
        [SerializeField, Range(0f, 1f)]
        private float _initialTimeOfDay = 0.25f;

        [Tooltip("Азимут орбиты солнца (поворот по Y). Задаёт направление восхода/заката")]
        [SerializeField, Range(0f, 360f)]
        private float _sunOrbitalYAngle = 170f;

        [Tooltip("Наклон орбиты солнца относительно мировой вертикали (градусы).\n"
               + "0° = экваториальная орбита (солнце проходит через зенит).\n"
               + "23.5° = наклон как у Земли.\n"
               + "90° = полярная орбита (солнце ходит по горизонту).\n"
               + "Наклон обеспечивает боковое освещение газового гиганта (фаза «серпа»).")]
        [SerializeField, Range(0f, 90f)]
        private float _orbitalInclination = 23.5f;

        [Tooltip("Угол солнца от горизонта, при котором переключается день/ночь")]
        [SerializeField, Range(1f, 30f)]
        private float _nightThresholdAngle = 10f;

        [Tooltip("Угловая зона ниже горизонта для плавного затухания интенсивности солнца (градусы).\n"
               + "При dot(sunForward, up) ∈ [0, -sin(fadeAngle)] интенсивность плавно уходит в 0.")]
        [SerializeField, Range(1f, 30f)]
        private float _sunHorizonFadeAngle = 10f;

        [Space(10)]
        [Header("═══ Профили Атмосферы (Data-Driven) ═══")]
        [Tooltip("Профиль дневной поверхности")]
        [SerializeField] private AtmosphereProfile _profileDay;

        [Tooltip("Профиль ночной поверхности")]
        [SerializeField] private AtmosphereProfile _profileNight;

        [Tooltip("Профиль подводной среды")]
        [SerializeField] private AtmosphereProfile _profileUnderwater;

        [Tooltip("Профиль Великого Затмения")]
        [SerializeField] private AtmosphereProfile _profileEclipse;

        [Space(10)]
        [Header("═══ Скорость Переходов ═══")]
        [Tooltip("Скорость интерполяции между состояниями.\n1.0 = переход за 1 секунду, 0.5 = за 2 секунды")]
        [SerializeField, Range(0.1f, 5f)]
        private float _transitionSpeed = 1.5f;

        [Space(10)]
        [Header("═══ Подводная Среда ═══")]
        [Tooltip("Transform камеры/головы игрока для автоматической проверки погружения")]
        [SerializeField] private Transform _playerTransform;

        [Tooltip("Y-координата поверхности воды в мировом пространстве")]
        [SerializeField] private float _waterSurfaceY = 0f;

        [Tooltip("Включить автоматическое определение погружения по Y-координате игрока")]
        [SerializeField] private bool _useAutoUnderwaterDetection = true;

        [Space(10)]
        [Header("═══ Камера (Подводный рендер) ═══")]
        [Tooltip("Основная камера. Используется для переключения clearFlags при погружении.\n"
               + "Под водой: SolidColor + backgroundColor = fogColor.\n"
               + "На поверхности: Skybox.")]
        [SerializeField] private Camera mainCamera;

        [Space(10)]
        [Header("═══ URP Volume (Опционально) ═══")]
        [Tooltip("Глобальный URP Volume для управления экспозицией неба.\nДобавьте ColorAdjustments в профиль Volume.")]
        [SerializeField] private Volume _globalVolume;

        #endregion

        #region ══════════ Приватное состояние (кэш) ══════════

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

        private ColorAdjustments   _cachedColorAdjustments;
        private VolumeProfile      _runtimeVolumeProfile;

        /// <summary>
        /// Tracks whether this component successfully registered with GameTickManager.
        /// Prevents double-register (OnEnable + Start) and orphan unregister.
        /// </summary>
        private bool _registeredToTickManager;

        #endregion

        #region ══════════ Публичные свойства (только чтение) ══════════

        public EnvironmentState CurrentState => _currentState;
        public float TimeOfDay => _cycleTimer / _cycleDuration;
        public float SunAngle => _sunAngleDegrees;
        public float SunElevation => _sunElevationDot;
        public float CurrentSkyExposure => _currentValues.skyExposure;
        public bool IsEclipseActive => _eclipseActive;
        public float EclipseRemainingTime => _eclipseRemainingTime;
        public float CycleDuration => _cycleDuration;
        public float OrbitalInclination => _orbitalInclination;

        /// <summary>
        /// Current sun intensity as computed by AtmosphereManager (profile × horizon fade).
        /// CelestialEngine reads this to modulate by occlusion without race conditions.
        /// </summary>
        public float CurrentSunIntensity => _sunLight != null ? _sunLight.intensity : 0f;

        #endregion

        #region ══════════ Жизненный цикл Unity ══════════

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning(
                    $"[HectonAtmosphere] Дубликат менеджера на '{gameObject.name}' — уничтожен.",
                    gameObject);
                Destroy(this);
                return;
            }
            _instance = this;

            _registeredToTickManager = false;

            InitializeCycleTimer();
            InitializeAtmosphereValues();
            InitializeVolumeCache();
        }

        // ====================================================================
        // TICK REGISTRATION — Deferred two-phase pattern.
        //
        // Phase 1 (OnEnable): Attempt registration silently.
        //   GameTickManager.Instance may be null due to script execution order.
        //
        // Phase 2 (Start): Fallback registration.
        //   All Awake() calls completed. Debug.LogError only if still null.
        //
        // _registeredToTickManager flag ensures exactly-once registration.
        // ====================================================================

        private void OnEnable()
        {
            if (!_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((ITickable)this);
                _registeredToTickManager = true;
            }
        }

        private void Start()
        {
            if (_registeredToTickManager)
                return;

            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((ITickable)this);
                _registeredToTickManager = true;
            }
            else
            {
                Debug.LogError(
                    "[HectonAtmosphere] GameTickManager.Instance == null даже в Start(). " +
                    "Атмосфера НЕ будет обновляться. " +
                    "Убедитесь, что GameTickManager присутствует в сцене и активен.",
                    this);
            }
        }

        private void OnDisable()
        {
            if (_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister((ITickable)this);
                _registeredToTickManager = false;
            }
        }

        private void OnDestroy()
        {
            if (_instance != this) return;

            _instance = null;

            OnStateChanged = null;

            if (_runtimeVolumeProfile != null)
            {
                DestroyImmediate(_runtimeVolumeProfile);
                _runtimeVolumeProfile = null;
            }
        }

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

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.4f, 0.9f, 0.25f);
            Vector3 center = new Vector3(
                transform.position.x,
                _waterSurfaceY,
                transform.position.z);
            Gizmos.DrawCube(center, new Vector3(200f, 0.05f, 200f));

            Gizmos.color = new Color(0.1f, 0.4f, 0.9f, 0.8f);
            Gizmos.DrawWireCube(center, new Vector3(200f, 0.05f, 200f));

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
                    math.sin(angle) * orbitRadius,
                    0f
                );
                float3 worldPoint = math.mul(orbitFrame, localPoint);
                Vector3 wp = transform.position + (Vector3)worldPoint;

                if (i > 0)
                    Gizmos.DrawLine(prevPoint, wp);

                prevPoint = wp;
            }

            if (_sunLight != null)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 1f);
                Gizmos.DrawWireSphere(
                    transform.position - (Vector3)(_sunLight.transform.forward * orbitRadius),
                    2f);
            }
        }
#endif

        #endregion

        #region ══════════ ITickable — Главный цикл обновления ══════════

        public void Tick(float deltaTime)
        {
            AdvanceCycleTimer(deltaTime);
            RotateSun();

            TickEclipseTimer(deltaTime);

            EnvironmentState resolved = ResolveState();
            ProcessStateTransition(resolved);

            InterpolateAtmosphere(deltaTime);
            ApplyToRenderSettings();
            ApplyCameraClearFlags();
            ApplyToVolume();
        }

        #endregion

        #region ══════════ Инициализация ══════════

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
        }

        private void InitializeVolumeCache()
        {
            if (_globalVolume == null || _globalVolume.profile == null) return;

            _runtimeVolumeProfile  = Instantiate(_globalVolume.profile);
            _globalVolume.profile  = _runtimeVolumeProfile;

            _runtimeVolumeProfile.TryGet(out _cachedColorAdjustments);

            if (_cachedColorAdjustments == null)
            {
                Debug.LogWarning(
                    "[HectonAtmosphere] В Volume-профиле нет ColorAdjustments. " +
                    "skyExposure не будет применяться к Volume.",
                    _globalVolume);
            }
        }

        #endregion

        #region ══════════ Цикл времени и вращение солнца ══════════

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

            quaternion finalRotation = math.mul(qAzimuth, math.mul(qInclination, qDaily));

            _sunLight.transform.rotation = finalRotation;

            float3 sunForward = math.mul(finalRotation, new float3(0f, 0f, 1f));
            _sunElevationDot = math.dot(-sunForward, new float3(0f, 1f, 0f));

            Shader.SetGlobalVector(_shaderID_SunDirection,
                new Vector4(sunForward.x, sunForward.y, sunForward.z, 0f));
        }

        #endregion

        #region ══════════ Затмение (таймер) ══════════

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

        #region ══════════ Машина состояний ══════════

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
            if (_underwaterExternalFlag)
                return true;

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

            EnvironmentState previous = _currentState;
            _currentState = newState;

            OnStateChanged?.Invoke(_currentState);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[HectonAtmosphere] {previous} → {_currentState} " +
                $"(солнце: {_sunAngleDegrees:F1}°, высота: {_sunElevationDot:F3}, время: {TimeOfDay:P0})");
#endif
        }

        #endregion

        #region ══════════ Интерполяция атмосферы ══════════

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
                in _transitionOrigin,
                in targetSnap,
                smoothT);
        }

        #endregion

        #region ══════════ Применение к системам рендеринга ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyToRenderSettings()
        {
            RenderSettings.fog        = true;
            RenderSettings.fogMode    = FogMode.ExponentialSquared;
            RenderSettings.fogColor   = _currentValues.fogColor;
            RenderSettings.fogDensity = _currentValues.fogDensity;

            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = _currentValues.ambientColor;

            if (_sunLight != null)
            {
                float fadeThreshold = math.sin(math.radians(_sunHorizonFadeAngle));

                float horizonFactor;
                if (_sunElevationDot <= 0f)
                {
                    horizonFactor = 0f;
                }
                else if (_sunElevationDot >= fadeThreshold)
                {
                    horizonFactor = 1f;
                }
                else
                {
                    float st = _sunElevationDot / fadeThreshold;
                    horizonFactor = st * st * (3f - 2f * st);
                }

                _sunLight.intensity = _currentValues.sunIntensity * horizonFactor;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyCameraClearFlags()
        {
            if (mainCamera == null) return;

            if (_currentState == EnvironmentState.UNDERWATER)
            {
                mainCamera.clearFlags      = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = _currentValues.fogColor;
            }
            else
            {
                mainCamera.clearFlags = CameraClearFlags.Skybox;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyToVolume()
        {
            if (_cachedColorAdjustments == null) return;

            _cachedColorAdjustments.postExposure.overrideState = true;
            _cachedColorAdjustments.postExposure.value         = _currentValues.skyExposure;
        }

        #endregion

        #region ══════════ Выбор профиля ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AtmosphereProfile ResolveProfile(EnvironmentState state)
        {
            AtmosphereProfile profile = state switch
            {
                EnvironmentState.SURFACE_DAY   => _profileDay,
                EnvironmentState.SURFACE_NIGHT => _profileNight,
                EnvironmentState.UNDERWATER    => _profileUnderwater,
                EnvironmentState.ECLIPSE       => _profileEclipse,
                _                              => _profileDay
            };

            return profile != null ? profile : _profileDay;
        }

        #endregion

        #region ══════════ Публичный API ══════════

        public void TriggerEclipse(float duration)
        {
            if (duration <= 0f)
            {
                Debug.LogWarning(
                    "[HectonAtmosphere] TriggerEclipse: длительность должна быть > 0.",
                    this);
                return;
            }

            _eclipseActive        = true;
            _eclipseRemainingTime = duration;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[HectonAtmosphere] ◐ Великое Затмение! Длительность: {duration:F1} сек.",
                this);
#endif
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

        public void SetMainCamera(Camera camera)
        {
            mainCamera = camera;
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