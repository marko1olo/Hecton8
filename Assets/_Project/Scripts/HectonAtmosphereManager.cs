// ══════════════════════════════════════════════════════════════════
// HectonAtmosphereManager.cs
// Центральная система управления атмосферой экзолуны Гектон
// Unity 6 | Universal Render Pipeline
//
// Ответственность:
//   • Цикл дня/ночи (вращение Directional Light по наклонной орбите)
//   • Автоматическое определение состояния среды
//   • Плавная интерполяция параметров атмосферы
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
// ══════════════════════════════════════════════════════════════════

using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;

[DisallowMultipleComponent]
[AddComponentMenu("Hecton/Atmosphere Manager")]
public class HectonAtmosphereManager : MonoBehaviour
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
        /// Color.Lerp для цветов, math.lerp для скаляров.
        /// Передача по in-ref — без копирования структур.
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
    /// Подписчики получают новый EnvironmentState.
    /// <example>
    /// <code>
    /// HectonAtmosphereManager.OnStateChanged += state =>
    /// {
    ///     if (state == EnvironmentState.ECLIPSE)
    ///         PlayEclipseMusic();
    /// };
    /// </code>
    /// </example>
    /// </summary>
    public static event Action<EnvironmentState> OnStateChanged;

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
    [Header("═══ URP Volume (Опционально) ═══")]
    [Tooltip("Глобальный URP Volume для управления экспозицией неба.\nДобавьте ColorAdjustments в профиль Volume.\nЕсли не назначен — skyExposure доступна через свойство CurrentSkyExposure")]
    [SerializeField] private Volume _globalVolume;

    #endregion

    #region ══════════ Приватное состояние (кэш) ══════════

    // ── Состояние среды ──
    private EnvironmentState _currentState = EnvironmentState.SURFACE_DAY;

    // ── Цикл времени ──
    private float _cycleTimer;             // Текущее время в цикле (секунды)
    private float _sunAngleDegrees;        // Текущий угол солнца (0–360°)

    // ── Высота солнца над горизонтом ──
    // dot(sunForward, Vector3.up): >0 = солнце светит вверх (ниже горизонта для Directional),
    // <0 = солнце светит вниз (выше горизонта).
    // Для Directional Light: forward направлен ОТ солнца К сцене,
    // поэтому dot < 0 означает «солнце выше горизонта».
    // Мы храним _sunElevationDot = dot(-sunForward, up) = высота солнца.
    private float _sunElevationDot;

    // ── Затмение ──
    private bool  _eclipseActive;
    private float _eclipseRemainingTime;

    // ── Подводное состояние (внешний флаг) ──
    private bool _underwaterExternalFlag;

    // ── Интерполяция атмосферы ──
    private AtmosphereSnapshot _transitionOrigin;   // Снимок «откуда» переходим
    private AtmosphereSnapshot _currentValues;       // Текущие применяемые значения
    private float              _transitionProgress;  // 0→1 прогресс перехода

    // ── Кэш URP Volume ──
    private ColorAdjustments   _cachedColorAdjustments;
    private VolumeProfile      _runtimeVolumeProfile;

    #endregion

    #region ══════════ Публичные свойства (только чтение) ══════════

    /// <summary>Текущее состояние окружающей среды.</summary>
    public EnvironmentState CurrentState => _currentState;

    /// <summary>Нормализованное время суток (0–1).</summary>
    public float TimeOfDay => _cycleTimer / _cycleDuration;

    /// <summary>Текущий угол солнца (0–360°).</summary>
    public float SunAngle => _sunAngleDegrees;

    /// <summary>
    /// Высота солнца над горизонтом как dot(-sunForward, up).
    /// +1 = зенит, 0 = горизонт, -1 = надир.
    /// </summary>
    public float SunElevation => _sunElevationDot;

    /// <summary>Текущая экспозиция неба (для внешних систем без Volume).</summary>
    public float CurrentSkyExposure => _currentValues.skyExposure;

    /// <summary>Активно ли Великое Затмение.</summary>
    public bool IsEclipseActive => _eclipseActive;

    /// <summary>Оставшееся время затмения (секунды).</summary>
    public float EclipseRemainingTime => _eclipseRemainingTime;

    /// <summary>Длительность полного цикла дня/ночи (секунды).</summary>
    public float CycleDuration => _cycleDuration;

    /// <summary>Наклон орбиты солнца (градусы).</summary>
    public float OrbitalInclination => _orbitalInclination;

    #endregion

    #region ══════════ Жизненный цикл Unity ══════════

    private void Awake()
    {
        // ── Синглтон ──
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning(
                $"[HectonAtmosphere] Дубликат менеджера на '{gameObject.name}' — уничтожен.",
                gameObject);
            Destroy(this);
            return;
        }
        _instance = this;

        // ── Инициализация подсистем ──
        InitializeCycleTimer();
        InitializeAtmosphereValues();
        InitializeVolumeCache();
    }

    private void OnDestroy()
    {
        if (_instance != this) return;

        _instance = null;

        // Очищаем статическое событие для предотвращения утечек при смене сцен
        OnStateChanged = null;

        // Уничтожаем runtime-копию Volume-профиля
        if (_runtimeVolumeProfile != null)
        {
            DestroyImmediate(_runtimeVolumeProfile);
            _runtimeVolumeProfile = null;
        }
    }

    /// <summary>
    /// Главный цикл обновления. Вызывается каждый кадр.
    /// Гарантия: НОЛЬ аллокаций в куче.
    /// </summary>
    private void Update()
    {
        // 1. Время и солнце
        AdvanceCycleTimer();
        RotateSun();

        // 2. Затмение
        TickEclipseTimer();

        // 3. Определение целевого состояния
        EnvironmentState resolved = ResolveState();
        ProcessStateTransition(resolved);

        // 4. Интерполяция и применение
        InterpolateAtmosphere();
        ApplyToRenderSettings();
        ApplyToVolume();
    }

#if UNITY_EDITOR
    /// <summary>Валидация параметров в редакторе.</summary>
    private void OnValidate()
    {
        _cycleDuration = math.max(_cycleDuration, 1f);

        // Попытка найти Directional Light, если не назначен
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

    /// <summary>Визуализация уровня воды и орбиты солнца в Scene View.</summary>
    private void OnDrawGizmosSelected()
    {
        // ── Водная поверхность ──
        Gizmos.color = new Color(0.1f, 0.4f, 0.9f, 0.25f);
        Vector3 center = new Vector3(
            transform.position.x,
            _waterSurfaceY,
            transform.position.z);
        Gizmos.DrawCube(center, new Vector3(200f, 0.05f, 200f));

        Gizmos.color = new Color(0.1f, 0.4f, 0.9f, 0.8f);
        Gizmos.DrawWireCube(center, new Vector3(200f, 0.05f, 200f));

        // ── Орбита солнца (визуализация наклонённой окружности) ──
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
            // Точка на единичной окружности в плоскости XY (локальная орбитальная плоскость)
            float3 localPoint = new float3(
                math.cos(angle) * orbitRadius,
                math.sin(angle) * orbitRadius,
                0f
            );
            // Трансформируем в мировое пространство через орбитальный фрейм
            float3 worldPoint = math.mul(orbitFrame, localPoint);
            Vector3 wp = transform.position + (Vector3)worldPoint;

            if (i > 0)
                Gizmos.DrawLine(prevPoint, wp);

            prevPoint = wp;
        }

        // Отметка текущего положения солнца
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

    #region ══════════ Инициализация ══════════

    /// <summary>Устанавливает начальное время цикла из нормализованного значения.</summary>
    private void InitializeCycleTimer()
    {
        _cycleTimer = _initialTimeOfDay * _cycleDuration;
    }

    /// <summary>Считывает начальные значения атмосферы из стартового профиля.</summary>
    private void InitializeAtmosphereValues()
    {
        AtmosphereProfile profile = ResolveProfile(_currentState);

        _currentValues = profile != null
            ? AtmosphereSnapshot.FromProfile(profile)
            : AtmosphereSnapshot.Default;

        _transitionOrigin  = _currentValues;
        _transitionProgress = 1f; // Переход «завершён» — мы уже на месте
    }

    /// <summary>
    /// Кэширует ссылку на ColorAdjustments из URP Volume.
    /// Создаёт runtime-копию профиля, чтобы не портить ассет.
    /// </summary>
    private void InitializeVolumeCache()
    {
        if (_globalVolume == null || _globalVolume.profile == null) return;

        // Создаём runtime-копию профиля — модификации не затронут ассет на диске
        _runtimeVolumeProfile  = Instantiate(_globalVolume.profile);
        _globalVolume.profile  = _runtimeVolumeProfile;

        // Кэшируем ссылку на ColorAdjustments (если есть в профиле)
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

    /// <summary>
    /// Продвигает таймер цикла. math.fmod обеспечивает бесшовное зацикливание.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AdvanceCycleTimer()
    {
        _cycleTimer += Time.deltaTime;
        _cycleTimer  = math.fmod(_cycleTimer, _cycleDuration);
    }

    /// <summary>
    /// Вращает Directional Light (солнце) по наклонной орбите в полном 3D.
    ///
    /// Орбитальная модель (три последовательных вращения):
    ///
    ///   1. DAILY ROTATION (θ) — угол продвижения по орбите.
    ///      Нормализованное время суток → θ = timeOfDay × 360°.
    ///      Вращение вокруг оси X в локальной орбитальной плоскости.
    ///      0° = восход (горизонт, восток), 90° = зенит, 180° = закат, 270° = надир.
    ///
    ///   2. ORBITAL INCLINATION (i) — наклон плоскости орбиты.
    ///      Вращение вокруг оси Z на угол i.
    ///      При i=0° орбита экваториальная (солнце проходит через зенит).
    ///      При i=23.5° орбита наклонена как у Земли — солнце никогда не
    ///      достигает зенита, освещая газовый гигант сбоку (фаза «серпа»).
    ///      При i=90° солнце ходит строго по горизонту.
    ///
    ///   3. SUN AZIMUTH (ψ) — ориентация всей орбитальной плоскости.
    ///      Вращение вокруг мировой оси Y на угол ψ.
    ///      Определяет, в какую сторону «смотрит» линия восход-закат.
    ///
    /// Итоговый кватернион: Q = Rᵧ(ψ) × R_z(i) × Rₓ(θ)
    ///
    /// После вычисления ориентации:
    ///   - Обновляется _sunElevationDot = dot(-sunForward, Vector3.up)
    ///     (+1 = зенит, 0 = горизонт, -1 = надир)
    ///   - Обновляется глобальный шейдер-вектор _SunDirection
    /// </summary>
    private void RotateSun()
    {
        if (_sunLight == null) return;

        // ── 1. Нормализованное время → угол по орбите ──
        float normalized = _cycleTimer / _cycleDuration;
        _sunAngleDegrees = normalized * 360f;

        // ── 2. Углы в радианах ──
        float dailyRad       = math.radians(_sunAngleDegrees);
        float inclinationRad = math.radians(_orbitalInclination);
        float azimuthRad     = math.radians(_sunOrbitalYAngle);

        // ── 3. Составной кватернион: Azimuth × Inclination × DailyRotation ──
        //
        // Порядок умножения (справа налево):
        //   - Сначала вращаем вокруг X (суточное движение по орбите)
        //   - Затем наклоняем орбитальную плоскость вокруг Z
        //   - Затем разворачиваем всю конструкцию вокруг Y (азимут)
        //
        quaternion qDaily       = quaternion.RotateX(dailyRad);
        quaternion qInclination = quaternion.RotateZ(inclinationRad);
        quaternion qAzimuth     = quaternion.RotateY(azimuthRad);

        quaternion finalRotation = math.mul(qAzimuth, math.mul(qInclination, qDaily));

        _sunLight.transform.rotation = finalRotation;

        // ── 4. Вычисляем высоту солнца над горизонтом ──
        //
        // Для Directional Light: transform.forward указывает ОТ солнца К сцене.
        // Направление НА солнце = -forward.
        // dot(-forward, up) > 0 → солнце выше горизонта
        // dot(-forward, up) < 0 → солнце ниже горизонта
        //
        float3 sunForward = math.mul(finalRotation, new float3(0f, 0f, 1f));
        _sunElevationDot = math.dot(-sunForward, new float3(0f, 1f, 0f));

        // ── 5. Глобальный шейдер-вектор для Gas Giant и других эффектов ──
        Shader.SetGlobalVector("_SunDirection", (Vector4)(Vector3)(float3)sunForward);
    }

    #endregion

    #region ══════════ Затмение (таймер) ══════════

    /// <summary>Тикает таймер затмения и выключает его по истечении.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TickEclipseTimer()
    {
        if (!_eclipseActive) return;

        _eclipseRemainingTime -= Time.deltaTime;

        if (_eclipseRemainingTime <= 0f)
        {
            _eclipseRemainingTime = 0f;
            _eclipseActive        = false;
        }
    }

    #endregion

    #region ══════════ Машина состояний ══════════

    /// <summary>
    /// Определяет целевое состояние по приоритету:
    ///   1. ECLIPSE       (наивысший — космическое событие)
    ///   2. UNDERWATER    (игрок под водой)
    ///   3. SURFACE_NIGHT (солнце ниже порога)
    ///   4. SURFACE_DAY   (по умолчанию)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private EnvironmentState ResolveState()
    {
        if (_eclipseActive)   return EnvironmentState.ECLIPSE;
        if (EvaluateUnderwater()) return EnvironmentState.UNDERWATER;

        return EvaluateDaytime()
            ? EnvironmentState.SURFACE_DAY
            : EnvironmentState.SURFACE_NIGHT;
    }

    /// <summary>
    /// Проверяет, под водой ли игрок.
    /// Два источника: внешний флаг (SetUnderwater) и автоматическая проверка Y.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool EvaluateUnderwater()
    {
        if (_underwaterExternalFlag)
            return true;

        if (_useAutoUnderwaterDetection && _playerTransform != null)
            return _playerTransform.position.y < _waterSurfaceY;

        return false;
    }

    /// <summary>
    /// Определяет, является ли текущее время — днём.
    /// Использует _sunElevationDot (высота солнца над горизонтом).
    /// 
    /// День = солнце выше порогового угла от горизонта.
    /// _sunElevationDot > sin(_nightThresholdAngle) → день.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool EvaluateDaytime()
    {
        float thresholdSin = math.sin(math.radians(_nightThresholdAngle));
        return _sunElevationDot > thresholdSin;
    }

    /// <summary>
    /// Обрабатывает смену состояния.
    /// При переходе: сохраняет снимок текущих значений, сбрасывает прогресс,
    /// уведомляет подписчиков через OnStateChanged.
    /// </summary>
    private void ProcessStateTransition(EnvironmentState newState)
    {
        if (newState == _currentState) return;

        // Снимок текущих значений — отправная точка нового перехода
        _transitionOrigin  = _currentValues;
        _transitionProgress = 0f;

        EnvironmentState previous = _currentState;
        _currentState = newState;

        // Событие для внешних систем
        OnStateChanged?.Invoke(_currentState);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"[HectonAtmosphere] {previous} → {_currentState} " +
            $"(солнце: {_sunAngleDegrees:F1}°, высота: {_sunElevationDot:F3}, время: {TimeOfDay:P0})");
#endif
    }

    #endregion

    #region ══════════ Интерполяция атмосферы ══════════

    /// <summary>
    /// Плавно интерполирует все параметры атмосферы от снимка к целевому профилю.
    /// 
    /// Алгоритм:
    ///   1. _transitionProgress нарастает от 0 до 1 с заданной скоростью
    ///   2. Mathf.SmoothStep даёт ease-in/ease-out кривую
    ///   3. AtmosphereSnapshot.Lerp интерполирует все параметры разом
    /// 
    /// При прерывании перехода (например, быстрый нырок → выход):
    ///   - Текущие значения становятся новым снимком
    ///   - SmoothStep стартует заново от текущей позиции
    ///   - Визуально — плавное перенаправление без рывков
    /// </summary>
    private void InterpolateAtmosphere()
    {
        // Переход уже завершён — ничего не считаем
        if (_transitionProgress >= 1f) return;

        // Продвижение прогресса
        _transitionProgress = math.saturate(
            _transitionProgress + Time.deltaTime * _transitionSpeed);

        // SmoothStep: ease-in / ease-out (S-кривая)
        float smoothT = Mathf.SmoothStep(0f, 1f, _transitionProgress);

        // Целевой профиль
        AtmosphereProfile target = ResolveProfile(_currentState);
        if (target == null) return;

        // Целевые значения (стековая структура)
        AtmosphereSnapshot targetSnap = AtmosphereSnapshot.FromProfile(target);

        // Интерполяция всех параметров одним вызовом
        _currentValues = AtmosphereSnapshot.Lerp(
            in _transitionOrigin,
            in targetSnap,
            smoothT);
    }

    #endregion

    #region ══════════ Применение к системам рендеринга ══════════

    /// <summary>
    /// Применяет интерполированные значения к RenderSettings и Directional Light.
    /// Вызывается каждый кадр — без аллокаций.
    ///
    /// Логика затухания солнца у горизонта:
    ///   - _sunElevationDot > fadeThreshold → полная интенсивность (из профиля)
    ///   - _sunElevationDot ∈ [0, fadeThreshold] → плавное затухание (smoothstep)
    ///   - _sunElevationDot ≤ 0 → интенсивность = 0 (солнце ниже горизонта)
    ///
    /// Это обеспечивает физически корректный плавный закат/восход без резких
    /// переключений интенсивности.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyToRenderSettings()
    {
        // ── Туман ──
        RenderSettings.fog        = true;
        RenderSettings.fogMode    = FogMode.ExponentialSquared;
        RenderSettings.fogColor   = _currentValues.fogColor;
        RenderSettings.fogDensity = _currentValues.fogDensity;

        // ── Окружающее освещение ──
        RenderSettings.ambientMode  = AmbientMode.Flat;
        RenderSettings.ambientLight = _currentValues.ambientColor;

        // ── Солнце: плавное затухание у горизонта ──
        if (_sunLight != null)
        {
            // Порог затухания: sin(_sunHorizonFadeAngle)
            // При elevation > fadeThreshold → множитель = 1
            // При elevation ∈ [0, fadeThreshold] → множитель ∈ [0, 1] (smoothstep)
            // При elevation ≤ 0 → множитель = 0
            float fadeThreshold = math.sin(math.radians(_sunHorizonFadeAngle));

            float horizonFactor;
            if (_sunElevationDot <= 0f)
            {
                // Солнце ниже горизонта — полное гашение
                horizonFactor = 0f;
            }
            else if (_sunElevationDot >= fadeThreshold)
            {
                // Солнце выше зоны затухания — полная яркость
                horizonFactor = 1f;
            }
            else
            {
                // Зона плавного затухания (smoothstep для естественности)
                float t = _sunElevationDot / fadeThreshold;
                horizonFactor = t * t * (3f - 2f * t); // smoothstep(0,1,t)
            }

            _sunLight.intensity = _currentValues.sunIntensity * horizonFactor;
        }
    }

    /// <summary>
    /// Применяет skyExposure к URP Volume через кэшированный ColorAdjustments.
    /// Если Volume не назначен или в нём нет ColorAdjustments — метод ничего не делает.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyToVolume()
    {
        if (_cachedColorAdjustments == null) return;

        _cachedColorAdjustments.postExposure.overrideState = true;
        _cachedColorAdjustments.postExposure.value         = _currentValues.skyExposure;
    }

    #endregion

    #region ══════════ Выбор профиля ══════════

    /// <summary>
    /// Возвращает AtmosphereProfile для указанного состояния.
    /// Fallback: при отсутствии профиля возвращает дневной.
    /// </summary>
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

    /// <summary>
    /// Запускает Великое Затмение на указанную длительность.
    /// Солнце гаснет, небо темнеет, туман становится плотным и синим
    /// (в соответствии с _profileEclipse).
    /// 
    /// <example>
    /// <code>
    /// // Затмение на 2 минуты
    /// HectonAtmosphereManager.Instance.TriggerEclipse(120f);
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="duration">Длительность затмения в секундах (> 0).</param>
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

    /// <summary>Досрочно завершает Великое Затмение.</summary>
    public void EndEclipse()
    {
        _eclipseActive        = false;
        _eclipseRemainingTime = 0f;
    }

    /// <summary>
    /// Устанавливает флаг погружения извне.
    /// Имеет приоритет над автоматической проверкой Y-координаты.
    /// Используйте для триггеров воды, катсцен и т.п.
    /// </summary>
    /// <param name="isUnderwater">true = игрок под водой.</param>
    public void SetUnderwater(bool isUnderwater)
    {
        _underwaterExternalFlag = isUnderwater;
    }

    /// <summary>
    /// Устанавливает время суток напрямую.
    /// </summary>
    /// <param name="normalized">0–1 (0=восход, 0.25=полдень, 0.5=закат, 0.75=полночь).</param>
    public void SetTimeOfDay(float normalized)
    {
        _cycleTimer = math.saturate(normalized) * _cycleDuration;
    }

    /// <summary>Устанавливает Y-координату поверхности воды в мировом пространстве.</summary>
    public void SetWaterSurfaceLevel(float worldY)
    {
        _waterSurfaceY = worldY;
    }

    /// <summary>Назначает Transform игрока для автоматического определения погружения.</summary>
    public void SetPlayerTransform(Transform player)
    {
        _playerTransform = player;
    }

    /// <summary>Изменяет длительность цикла дня/ночи в рантайме.</summary>
    public void SetCycleDuration(float seconds)
    {
        _cycleDuration = math.max(seconds, 1f);
    }

    /// <summary>Изменяет скорость перехода между состояниями.</summary>
    public void SetTransitionSpeed(float speed)
    {
        _transitionSpeed = math.clamp(speed, 0.1f, 10f);
    }

    /// <summary>Изменяет наклон орбиты солнца в рантайме (0–90°).</summary>
    public void SetOrbitalInclination(float degrees)
    {
        _orbitalInclination = math.clamp(degrees, 0f, 90f);
    }

    #endregion
}