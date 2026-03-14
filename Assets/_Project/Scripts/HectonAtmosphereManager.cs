// ══════════════════════════════════════════════════════════════════
// HectonAtmosphereManager.cs
// Центральная система управления атмосферой экзолуны Гектон
// Unity 6 | Universal Render Pipeline | GameTickManager Integration
//
// ═══════════════════════════════════════════════════════════════
// REFACTORED v3 — SURFACE FOG FIX + DEPTH MATH.LERP + ZERO GC
// ═══════════════════════════════════════════════════════════════
//
// Ответственность:
//   • Цикл дня/ночи (вращение Directional Light по наклонной орбите)
//   • Автоматическое определение состояния среды
//   • Плавная интерполяция параметров атмосферы между PROFILE ASSETS
//   • Управление камерой под водой (clearFlags / backgroundColor)
//   • Уведомление внешних систем через событие OnStateChanged
//   • Передача направления солнца в глобальный шейдер-вектор _SunDirection
//   • Depth-based underwater fog: math.lerp между минимальной и профильной
//     плотностью на основе глубины погружения
//
// КЛЮЧЕВЫЕ ИЗМЕНЕНИЯ v3:
//   [FIX] RenderSettings.fog = false для ВСЕХ наземных состояний.
//         Устраняет голубую муть на поверхности после всплытия.
//   [FIX] Depth-based fog использует math.lerp вместо AnimationCurve
//         для предсказуемости и Zero GC гарантии (AnimationCurve.Evaluate
//         allocation-free, но math.lerp проще верифицировать).
//   [ADD] fogDensity и fogColor полноценно участвуют в интерполяции
//         через AtmosphereSnapshot и передаются в RenderSettings.
//   [DEL] ApplyToVolume() — полностью удалён. postExposure контролируется
//         ТОЛЬКО художниками через URP Volume Inspector.
//   [DEL] AnimationCurve underwaterVisibilityCurve — заменён на
//         shallowFogDensity/deepFogDensity + shallowDepthThreshold.
//
// DESIGN PRINCIPLE — SINGLE SOURCE OF TRUTH:
//   AtmosphereProfile .asset files are the SINGLE SOURCE OF TRUTH.
//   This manager ONLY lerps between profiles. It does NOT override
//   Volume postExposure. Fog parameters are driven by profiles +
//   depth modulation when submerged.
//
// UNDERWATER FOG MODEL:
//   density = math.lerp(shallowFogDensity, profile.fogDensity, depthFactor)
//   depthFactor = math.saturate(depth / shallowDepthThreshold)
//   Result: crystal-clear water near surface, murky at depth.
//   Profile fogDensity defines the MAXIMUM density at full depth.
//
// SURFACE FOG MODEL:
//   RenderSettings.fog = false for ALL surface states.
//   Sky, islands, and distant geometry are visible without blue haze.
//   Artistic fog (volumetric, height fog) is handled by separate
//   VFX systems or URP Volume overrides — not by RenderSettings.fog.
//
// Орбитальная модель солнца:
//   Солнце движется по наклонной орбите, определяемой тремя параметрами:
//     - Daily Rotation (timeOfDay → 0°–360° вокруг орбитальной оси)
//     - Orbital Inclination (наклон плоскости орбиты к мировой вертикали)
//     - Sun Azimuth (поворот всей орбитальной плоскости вокруг Y)
//
// Приоритет состояний:
//   ECLIPSE → UNDERWATER → SURFACE_NIGHT / SURFACE_DAY
//
// Подводный рендер:
//   При UNDERWATER состоянии менеджер переключает Camera.clearFlags
//   на SolidColor и устанавливает backgroundColor = fogColor из профиля.
//
// Zero-GC гарантия:
//   Метод Tick() не содержит аллокаций. Все вычисления — struct math
//   через Unity.Mathematics. Shader Property ID кэшированы статически.
//   Color интерполяция через покомпонентный math.lerp — без boxing.
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
using Unity.Mathematics;

// NOTE: MapMagicBridge.OnBiomeChanged is in Hecton8.Core namespace
// (same as this class), so no additional using needed.
// This comment is for documentation only.

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
        ///
        /// fogDensity: для UNDERWATER — максимальная плотность на глубине.
        ///             Реальная плотность модулируется через math.lerp по глубине.
        ///             Для наземных состояний — не применяется (fog = false).
        ///
        /// fogColor:   для UNDERWATER — цвет тумана и Camera.backgroundColor.
        ///             Для наземных — участвует в интерполяции, но не применяется.
        ///
        /// skyExposure: хранится для информационных целей (CurrentSkyExposure).
        ///              НЕ применяется к Volume — художник контролирует через Inspector.
        /// </summary>
        private struct AtmosphereSnapshot
        {
            public float4 fogColor;      // RGBA as float4 — zero boxing, math-friendly
            public float  fogDensity;
            public float  skyExposure;
            public float4 ambientColor;  // RGBA as float4
            public float  sunIntensity;

            /// <summary>Нейтральные значения по умолчанию.</summary>
            public static AtmosphereSnapshot Default => new AtmosphereSnapshot
            {
                fogColor     = new float4(0.7f, 0.7f, 0.8f, 1f),
                fogDensity   = 0.01f,
                skyExposure  = 1f,
                ambientColor = new float4(0.5f, 0.5f, 0.5f, 1f),
                sunIntensity = 1f
            };

            /// <summary>Создаёт снимок из ScriptableObject-профиля. Zero GC.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static AtmosphereSnapshot FromProfile(AtmosphereProfile p)
            {
                Color fc = p.fogColor;
                Color ac = p.ambientColor;

                return new AtmosphereSnapshot
                {
                    fogColor     = new float4(fc.r, fc.g, fc.b, fc.a),
                    fogDensity   = p.fogDensity,
                    skyExposure  = p.skyExposure,
                    ambientColor = new float4(ac.r, ac.g, ac.b, ac.a),
                    sunIntensity = p.sunIntensity
                };
            }

            /// <summary>
            /// Интерполяция между двумя снимками.
            /// Использует math.lerp для ВСЕХ полей — zero boxing, zero GC.
            /// Color.Lerp заменён на покомпонентный math.lerp через float4.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static AtmosphereSnapshot Lerp(
                in AtmosphereSnapshot from,
                in AtmosphereSnapshot to,
                float t)
            {
                return new AtmosphereSnapshot
                {
                    fogColor     = math.lerp(from.fogColor,     to.fogColor,     t),
                    fogDensity   = math.lerp(from.fogDensity,   to.fogDensity,   t),
                    skyExposure  = math.lerp(from.skyExposure,  to.skyExposure,  t),
                    ambientColor = math.lerp(from.ambientColor, to.ambientColor, t),
                    sunIntensity = math.lerp(from.sunIntensity, to.sunIntensity, t)
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

        private static readonly int _shaderID_SunDirection =
            Shader.PropertyToID("_SunDirection");

        #endregion

        #region ══════════ Сериализуемые настройки ══════════

        [Header("═══ Солнце и Цикл Времени ═══")]
        [Tooltip("Directional Light, играющий роль солнца экзолуны")]
        [SerializeField] private Light _sunLight;

        [Tooltip("Длительность полного цикла дня/ночи в секундах " +
                 "(3600 = 60 мин)")]
        [SerializeField, Min(1f)]
        private float _cycleDuration = 3600f;

        [Tooltip("Начальное нормализованное время суток.\n" +
                 "0 = восход, 0.25 = полдень, 0.5 = закат, 0.75 = полночь")]
        [SerializeField, Range(0f, 1f)]
        private float _initialTimeOfDay = 0.25f;

        [Tooltip("Азимут орбиты солнца (поворот по Y). " +
                 "Задаёт направление восхода/заката")]
        [SerializeField, Range(0f, 360f)]
        private float _sunOrbitalYAngle = 170f;

        [Tooltip("Наклон орбиты солнца относительно мировой вертикали (градусы).\n"
               + "0° = экваториальная орбита (солнце проходит через зенит).\n"
               + "23.5° = наклон как у Земли.\n"
               + "90° = полярная орбита (солнце ходит по горизонту).\n"
               + "Наклон обеспечивает боковое освещение газового гиганта " +
                 "(фаза «серпа»).")]
        [SerializeField, Range(0f, 90f)]
        private float _orbitalInclination = 23.5f;

        [Tooltip("Угол солнца от горизонта, при котором переключается " +
                 "день/ночь")]
        [SerializeField, Range(1f, 30f)]
        private float _nightThresholdAngle = 10f;

        [Tooltip("Угловая зона ниже горизонта для плавного затухания " +
                 "интенсивности солнца (градусы).\n"
               + "При dot(sunForward, up) ∈ [0, -sin(fadeAngle)] " +
                 "интенсивность плавно уходит в 0.")]
        [SerializeField, Range(1f, 30f)]
        private float _sunHorizonFadeAngle = 10f;

        [Space(10)]
        [Header("═══ Профили Атмосферы (Data-Driven) ═══")]
        [Tooltip("Профиль дневной поверхности")]
        [SerializeField] private AtmosphereProfile _profileDay;

        [Tooltip("Профиль ночной поверхности")]
        [SerializeField] private AtmosphereProfile _profileNight;

        [Tooltip("Профиль подводной среды.\n" +
                 "fogDensity в этом профиле = МАКСИМАЛЬНАЯ плотность на глубине.\n" +
                 "У поверхности плотность снижается до shallowFogDensity.")]
        [SerializeField] private AtmosphereProfile _profileUnderwater;

        [Tooltip("Профиль Великого Затмения")]
        [SerializeField] private AtmosphereProfile _profileEclipse;

        [Space(10)]
        [Header("═══ Скорость Переходов ═══")]
        [Tooltip("Скорость интерполяции между состояниями.\n" +
                 "1.0 = переход за 1 секунду, 0.5 = за 2 секунды")]
        [SerializeField, Range(0.1f, 5f)]
        private float _transitionSpeed = 1.5f;

        [Space(10)]
        [Header("═══ Подводная Среда ═══")]
        [Tooltip("Transform камеры/головы игрока для автоматической " +
                 "проверки погружения")]
        [SerializeField] private Transform _playerTransform;

        [Tooltip("Y-координата поверхности воды в мировом пространстве")]
        [SerializeField] private float _waterSurfaceY = 0f;

        [Tooltip("Включить автоматическое определение погружения " +
                 "по Y-координате игрока")]
        [SerializeField] private bool _useAutoUnderwaterDetection = true;

        [Space(10)]
        [Header("═══ Underwater Depth Fog (math.lerp) ═══")]
        [Tooltip("Минимальная плотность тумана у поверхности воды (глубина ≈ 0м).\n" +
                 "Чем ниже — тем прозрачнее вода сверху.\n" +
                 "Рекомендуемо: 0.001–0.005 для Subnautica-подобного ощущения.")]
        [SerializeField, Range(0.0001f, 0.05f)]
        private float _shallowFogDensity = 0.001f;

                [Tooltip("Глубина (в метрах), на которой плотность тумана " +
                 "достигает значения из профиля (profile.fogDensity).\n" +
                 "math.lerp(_shallowFogDensity, profile.fogDensity, " +
                 "depth / threshold).\n" +
                 "50м = прозрачная вода до 50м, затем нарастает мутность.")]
        [SerializeField, Range(10f, 500f)]
        private float _shallowDepthThreshold = 50f;

        [Space(5)]
        [Header("═══ Underwater Light Absorption ══════════════")]
        [Tooltip("Коэффициент экспоненциального поглощения солнечного света водой.\n" +
                 "intensity = profileValue × horizonFade × exp(-k × depth)\n\n" +
                 "Примеры:\n" +
                 "  0.002 = чистая тропическая вода (свет до ~500м)\n" +
                 "  0.005 = умеренная прозрачность (сумерки на ~200м, тьма ~500м)\n" +
                 "  0.010 = мутная вода (тьма на ~200м)\n" +
                 "  0.020 = болотная вода (тьма на ~100м)\n\n" +
                 "Применяется ТОЛЬКО в состоянии UNDERWATER.\n" +
                 "На поверхности интенсивность = profile × horizonFade (без поглощения).")]
        [SerializeField, Range(0.001f, 0.05f)]
        private float _lightAbsorptionCoeff = 0.005f;

        [Tooltip("Глубина (метры), на которой fog density достигает " +
                 "полного значения из профиля.\n" +
                 "Используется ВМЕСТО _shallowDepthThreshold для более " +
                 "плавного нарастания тумана на больших глубинах.\n" +
                 "300м = прозрачно у поверхности, густой туман к 300м.")]
        [SerializeField, Range(50f, 1000f)]
        private float _deepFogDepthRange = 300f;

        [Tooltip("Reference to the survival system that tracks player depth.\n" +
                 "Must implement IDepthProvider.\n" +
                 "If null, depth = (waterSurfaceY - playerTransform.y).")]
        [SerializeField] private MonoBehaviour _survivalSystemRef;

        [Space(10)]
        [Header("═══ Камера (Подводный рендер) ═══")]
        [Tooltip("Основная камера. Используется для переключения clearFlags " +
                 "при погружении.\n" +
                 "Под водой: SolidColor + backgroundColor = fogColor.\n" +
                 "На поверхности: Skybox.")]
        [SerializeField] private Camera mainCamera;

        [Space(10)]
        [Header("═══ Biome Atmosphere Overrides ═══════════════")]
        [Tooltip("Маппинг biome ID → AtmosphereProfile.\n" +
                 "Biome ID соответствует индексу splat layer из " +
                 "MapMagic Biomes Set ноды.\n\n" +
                 "Когда игрок входит в биом — атмосфера плавно " +
                 "переходит на указанный профиль.\n" +
                 "Биомы без оверрайда используют стандартные " +
                 "профили (Day/Night/Underwater/Eclipse).")]
        [SerializeField] private BiomeAtmosphereOverride[] _biomeOverrides;

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

        /// <summary>
        /// Cached depth value. Computed once per Tick when underwater.
        /// Avoids redundant position reads. Positive = deeper.
        /// </summary>
        private float _currentDepth;

        /// <summary>
        /// Interface for reading depth from survival system.
        /// Cached in Awake to avoid per-frame GetComponent/casting.
        /// If null, depth is computed from player Y position.
        /// </summary>
        private IDepthProvider _depthProvider;

        /// <summary>
        /// Tracks whether this component successfully registered
        /// with GameTickManager. Prevents double-register and
        /// orphan unregister.
        /// </summary>
        private bool _registeredToTickManager;

        /// <summary>
        /// Cached previous fog state to avoid redundant RenderSettings
        /// writes when fog is already disabled. RenderSettings property
        /// setters are cheap but not free — skip when unchanged.
        /// </summary>
        private bool _wasFogEnabled;

        /// <summary>
        /// Currently active biome override profile (or null if none).
        /// Set by HandleBiomeChanged. Used by ResolveProfile to
        /// override default state-based profile selection.
        /// </summary>
        private AtmosphereProfile _activeBiomeProfile;

        /// <summary>
        /// Current biome ID received from MapMagicBridge.
        /// -1 = no biome override active (use default profiles).
        /// </summary>
        private int _currentBiomeID = -1;

        #endregion

        #region ══════════ Depth Provider Interface ══════════

        /// <summary>
        /// Optional interface that HectonSurvivalSystem can implement
        /// to provide current player depth. If not available, depth is
        /// calculated as (waterSurfaceY - playerTransform.position.y).
        /// </summary>
        public interface IDepthProvider
        {
            /// <summary>
            /// Current depth in meters below water surface.
            /// Positive = deeper.
            /// </summary>
            float CurrentDepth { get; }
        }

        #endregion

        #region ══════════ Biome Atmosphere Overrides ══════════

        /// <summary>
        /// Maps a biome ID (from MapMagicBridge) to an AtmosphereProfile.
        /// When the player enters this biome, the atmosphere transitions
        /// to the specified profile using the existing transition system.
        ///
        /// Configure in Inspector: drag AtmosphereProfile assets for each
        /// biome index from the MapMagic Biomes Set node.
        ///
        /// Biomes without an override use the default state-based profile
        /// (Day/Night/Underwater/Eclipse).
        /// </summary>
        [Serializable]
        public struct BiomeAtmosphereOverride
        {
            [Tooltip("Biome index matching the terrain splat layer index " +
                     "from MapMagic Biomes Set node output.")]
            public int biomeID;

            [Tooltip("Atmosphere profile to use when this biome is active.\n" +
                     "Controls fog color, fog density, sun intensity, " +
                     "ambient color, sky exposure.\n" +
                     "Leave null to use default state-based profiles.")]
            public AtmosphereProfile profile;
        }

        #endregion

        #region ══════════ Публичные свойства (только чтение) ══════════

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
        /// Current depth below water surface in meters.
        /// Valid only when state == UNDERWATER. Zero otherwise.
        /// </summary>
        public float CurrentDepth              => _currentDepth;

        /// <summary>
        /// Current sun intensity as computed by AtmosphereManager
        /// (profile × horizon fade). CelestialEngine reads this to
        /// modulate by occlusion without race conditions.
        /// </summary>
        public float CurrentSunIntensity =>
            _sunLight != null ? _sunLight.intensity : 0f;

        #endregion

        #region ══════════ Жизненный цикл Unity ══════════

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning(
                    $"[HectonAtmosphere] Дубликат менеджера на " +
                    $"'{gameObject.name}' — уничтожен.",
                    gameObject);
                Destroy(this);
                return;
            }
            _instance = this;

            _registeredToTickManager = false;
            _wasFogEnabled           = RenderSettings.fog;

            InitializeCycleTimer();
            InitializeAtmosphereValues();
            InitializeDepthProvider();
        }

        // ================================================================
        // TICK REGISTRATION — Deferred two-phase pattern.
        //
        // Phase 1 (OnEnable): Attempt registration. If Instance is null
        //   (script execution order), silently skip — Start will retry.
        //
        // Phase 2 (Start): Guaranteed retry. All Awake() calls done.
        //   Debug.LogError ONLY if Instance still null.
        // ================================================================

        private void OnEnable()
        {
            if (!_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((ITickable)this);
                _registeredToTickManager = true;
            }

            // ── Subscribe to biome changes ──
            MapMagicBridge.OnBiomeChanged += HandleBiomeChanged;
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
                    "[HectonAtmosphere] GameTickManager.Instance == null " +
                    "даже в Start(). Атмосфера НЕ будет обновляться. " +
                    "Убедитесь, что GameTickManager присутствует в сцене " +
                    "и активен.",
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

            // ── Unsubscribe from biome changes ──
            MapMagicBridge.OnBiomeChanged -= HandleBiomeChanged;
        }

        private void OnDestroy()
        {
            if (_instance != this) return;

            _instance = null;
            OnStateChanged = null;
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

            // Validate depth fog parameters
            _shallowFogDensity     = math.clamp(_shallowFogDensity, 0.0001f, 0.05f);
            _shallowDepthThreshold = math.max(_shallowDepthThreshold, 1f);
            _lightAbsorptionCoeff  = math.clamp(_lightAbsorptionCoeff, 0.001f, 0.05f);
            _deepFogDepthRange     = math.max(_deepFogDepthRange, 10f);
        }

        private void OnDrawGizmosSelected()
        {
            // ── Water surface plane ──
            Gizmos.color = new Color(0.1f, 0.4f, 0.9f, 0.25f);
            Vector3 center = new Vector3(
                transform.position.x,
                _waterSurfaceY,
                transform.position.z);
            Gizmos.DrawCube(center, new Vector3(200f, 0.05f, 200f));

            Gizmos.color = new Color(0.1f, 0.4f, 0.9f, 0.8f);
            Gizmos.DrawWireCube(center, new Vector3(200f, 0.05f, 200f));

            // ── Shallow depth threshold plane ──
            Gizmos.color = new Color(0.0f, 0.2f, 0.6f, 0.15f);
            Vector3 shallowCenter = new Vector3(
                transform.position.x,
                _waterSurfaceY - _shallowDepthThreshold,
                transform.position.z);
            Gizmos.DrawCube(shallowCenter, new Vector3(180f, 0.05f, 180f));

            Gizmos.color = new Color(0.0f, 0.2f, 0.6f, 0.5f);
            Gizmos.DrawWireCube(shallowCenter, new Vector3(180f, 0.05f, 180f));

            // ── Sun orbital path ──
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            const int segments    = 64;
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
                    transform.position -
                    (Vector3)(_sunLight.transform.forward * orbitRadius),
                    2f);
            }
        }
#endif

        #endregion

        #region ══════════ ITickable — Главный цикл обновления ══════════

        /// <summary>
        /// Главный цикл обновления атмосферы.
        /// Вызывается GameTickManager каждый кадр.
        ///
        /// ZERO GC: все вычисления — struct math, никаких аллокаций.
        /// Порядок операций критичен для корректности:
        ///   1. Advance timer → 2. Rotate sun → 3. Eclipse tick →
        ///   4. Resolve state → 5. Interpolate → 6. Apply render →
        ///   7. Apply camera
        /// </summary>
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

        private void InitializeDepthProvider()
        {
            if (_survivalSystemRef != null)
            {
                _depthProvider = _survivalSystemRef as IDepthProvider;

                if (_depthProvider == null)
                {
                    Debug.LogWarning(
                        $"[HectonAtmosphere] Assigned survival system " +
                        $"'{_survivalSystemRef.name}' does not implement " +
                        "IDepthProvider. Depth will be calculated from " +
                        "player Y position.",
                        _survivalSystemRef);
                }
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

        /// <summary>
        /// Вращает Directional Light по наклонной орбите.
        /// Вычисляет elevation dot для определения дня/ночи.
        /// Передаёт направление солнца в глобальный шейдер-вектор.
        ///
        /// Zero GC: Unity.Mathematics quaternion — struct math.
        /// </summary>
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
            float thresholdSin = math.sin(
                math.radians(_nightThresholdAngle));
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
                $"(солнце: {_sunAngleDegrees:F1}°, " +
                $"высота: {_sunElevationDot:F3}, " +
                $"время: {TimeOfDay:P0})");
#endif
        }

        #endregion

        #region ══════════ Интерполяция атмосферы ══════════

        /// <summary>
        /// Плавная интерполяция параметров между профилями.
        /// Использует hermite smoothstep для естественного перехода.
        /// Zero GC: AtmosphereSnapshot.Lerp — struct math only.
        /// </summary>
        private void InterpolateAtmosphere(float deltaTime)
        {
            if (_transitionProgress >= 1f) return;

            _transitionProgress = math.saturate(
                _transitionProgress + deltaTime * _transitionSpeed);

            float t = _transitionProgress;
            float smoothT = t * t * (3f - 2f * t); // hermite smoothstep

            AtmosphereProfile target = ResolveProfile(_currentState);
            if (target == null) return;

            AtmosphereSnapshot targetSnap =
                AtmosphereSnapshot.FromProfile(target);

            _currentValues = AtmosphereSnapshot.Lerp(
                in _transitionOrigin,
                in targetSnap,
                smoothT);
        }

        #endregion

        #region ══════════ Применение к системам рендеринга ══════════

        /// <summary>
        /// Applies interpolated atmosphere values to Unity RenderSettings.
        ///
        /// FOG RULES (CRITICAL — fixes blue haze on surface):
        ///   UNDERWATER:     fog = true, density from depth curve, color from profile.
        ///   SURFACE_DAY:    fog = false — clear sky, visible islands.
        ///   SURFACE_NIGHT:  fog = false — starry sky, visible terrain.
        ///   ECLIPSE:        fog = false — dramatic sky, unobstructed view.
        ///
        /// DEPTH-BASED FOG (UNDERWATER only):
        ///   depthFactor = saturate(depth / shallowDepthThreshold)
        ///   fogDensity  = math.lerp(shallowFogDensity, profile.fogDensity, depthFactor)
        ///
        ///   At depth 0m:   density = shallowFogDensity (0.001) → crystal clear
        ///   At depth 50m+: density = profile.fogDensity → full murkiness
        ///
        /// NOT APPLIED (artist-controlled via Inspector):
        ///   - Volume postExposure
        ///   - Any Volume override parameters
        ///
        /// Zero GC: all math is struct-based via Unity.Mathematics.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyToRenderSettings()
        {
            // ── Ambient light (always from profile lerp) ──
            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = Float4ToColor(_currentValues.ambientColor);

            // ── Fog control: UNDERWATER = on, everything else = off ──
            if (_currentState == EnvironmentState.UNDERWATER)
            {
                // ── Enable fog (only write if state changed) ──
                if (!_wasFogEnabled)
                {
                    RenderSettings.fog     = true;
                    RenderSettings.fogMode = FogMode.ExponentialSquared;
                    _wasFogEnabled         = true;
                }

                // ── Compute current depth ──
                _currentDepth = ComputeDepth();

                // ══════════════════════════════════════════════════
                // DEPTH-BASED FOG DENSITY (dynamic range model)
                //
                // Uses _deepFogDepthRange instead of _shallowDepthThreshold
                // for smoother fog buildup across the full depth range.
                //
                // depthFactor ramps from 0 (surface) to 1 (deepFogDepthRange).
                // Beyond deepFogDepthRange, density stays at profile maximum.
                //
                // Model: density = lerp(shallow, profile, saturate(depth / range))
                //   0m:    density = 0.001 (crystal clear)
                //   150m:  density = 0.05  (haze visible)
                //   300m+: density = profile.fogDensity (full murk)
                // ══════════════════════════════════════════════════
                float depthFactor = math.saturate(
                    _currentDepth / _deepFogDepthRange);

                float density = math.lerp(
                    _shallowFogDensity,
                    _currentValues.fogDensity,
                    depthFactor);

                RenderSettings.fogDensity = density;

                // ── Fog color from interpolated profile ──
                RenderSettings.fogColor = Float4ToColor(_currentValues.fogColor);
            }
            else
            {
                // ══════════════════════════════════════════════════
                // SURFACE / ECLIPSE: FOG = FALSE
                //
                // CRITICAL FIX: Previously, the else branch did not
                // touch RenderSettings.fog, leaving it true after
                // surfacing. This caused the blue haze over islands.
                //
                // Now explicitly disabled for all non-underwater states.
                // Artistic fog (volumetric, height fog) is handled by
                // separate VFX systems or URP Volume overrides.
                // ══════════════════════════════════════════════════
                if (_wasFogEnabled)
                {
                    RenderSettings.fog = false;
                    _wasFogEnabled     = false;
                }

                // Must be set BEFORE sun intensity calculation
                // so lightAbsorption = exp(-k × 0) = 1.0 on surface.
                _currentDepth = 0f;
            }

            // ══════════════════════════════════════════════════════
            // SUN INTENSITY = profile × horizonFade × lightAbsorption
            //
            // Three multiplicative factors:
            //
            // 1. profileValue: base intensity from AtmosphereProfile
            //    (interpolated during state transitions).
            //
            // 2. horizonFactor: smoothstep fade near horizon (0..1).
            //    Prevents abrupt day/night transition.
            //    Applied in ALL states (surface + underwater).
            //
            // 3. lightAbsorption: exp(-k × depth) (0..1).
            //    Applied ONLY in UNDERWATER state.
            //    Models exponential light absorption by water:
            //      depth 0m:   factor = 1.0 (full sun)
            //      depth 200m: factor ≈ 0.37 (k=0.005) — sumerki
            //      depth 500m: factor ≈ 0.08 — near darkness
            //      depth 1000m: factor ≈ 0.007 — absolute abyss
            //    On surface: factor = 1.0 (no absorption).
            //
            // Result: smooth continuous darkening with depth.
            // AtmosphereManager remains AUTHORITY for sunLight.intensity.
            // CelestialEngine reads this and MODULATES by occlusion.
            //
            // Zero GC: math.exp, math.sin, math.radians — all struct math.
            // ══════════════════════════════════════════════════════
            if (_sunLight != null)
            {
                // ── Factor 1: Horizon fade (all states) ──
                float fadeThreshold = math.sin(
                    math.radians(_sunHorizonFadeAngle));

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

                // ── Factor 2: Light absorption (underwater only) ──
                // exp(-k × depth): Beer-Lambert law approximation.
                // _currentDepth is already computed above in the fog block.
                // For surface states, _currentDepth == 0, so exp(0) == 1.
                float lightAbsorption = math.exp(
                    -_lightAbsorptionCoeff * _currentDepth);

                _sunLight.intensity =
                    _currentValues.sunIntensity * horizonFactor * lightAbsorption;
            }
        }

        /// <summary>
        /// Computes current depth below water surface in meters.
        /// Uses IDepthProvider if available, otherwise calculates
        /// from player Y position. Result clamped to [0, ∞).
        /// Zero GC: no allocations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ComputeDepth()
        {
            if (_depthProvider != null)
            {
                return math.max(0f, _depthProvider.CurrentDepth);
            }

            if (_playerTransform != null)
            {
                return math.max(
                    0f,
                    _waterSurfaceY - _playerTransform.position.y);
            }

            return 0f;
        }

        /// <summary>
        /// Manages camera clear flags based on environment state.
        /// UNDERWATER: SolidColor with fogColor background (seamless blend).
        /// SURFACE:    Skybox (reveals sky and distant geometry).
        /// Zero GC: direct property assignment.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyCameraClearFlags()
        {
            if (mainCamera == null) return;

            if (_currentState == EnvironmentState.UNDERWATER)
            {
                mainCamera.clearFlags      = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor =
                    Float4ToColor(_currentValues.fogColor);
            }
            else
            {
                mainCamera.clearFlags = CameraClearFlags.Skybox;
            }
        }

        // NOTE: ApplyToVolume() — ПОЛНОСТЬЮ УДАЛЁН.
        // postExposure и прочие Volume overrides контролируются
        // ТОЛЬКО художниками через URP Volume Inspector.
        // Код НЕ должен перезаписывать работу художника.

        #endregion

        #region ══════════ Утилиты (Zero GC) ══════════

        /// <summary>
        /// Converts float4 (Unity.Mathematics) to Color (UnityEngine).
        /// Inline — no method call overhead at callsite.
        /// Zero allocation: both are value types.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Color Float4ToColor(float4 v)
        {
            return new Color(v.x, v.y, v.z, v.w);
        }

        #endregion

        #region ══════════ Выбор профиля ══════════

        /// <summary>
        /// Resolves the active AtmosphereProfile for the current state.
        ///
        /// Priority order (highest first):
        ///   1. ECLIPSE — always uses _profileEclipse (dramatic override).
        ///   2. UNDERWATER — always uses _profileUnderwater (fog/absorption).
        ///   3. BIOME OVERRIDE — if _activeBiomeProfile != null and state
        ///      is SURFACE_DAY or SURFACE_NIGHT, use biome profile.
        ///      This allows each biome to have unique fog color, density,
        ///      sun intensity, and ambient lighting.
        ///   4. DEFAULT — _profileDay or _profileNight based on sun position.
        ///
        /// WHY ECLIPSE AND UNDERWATER IGNORE BIOMES:
        ///   Eclipse is a dramatic global event — biome visuals are secondary.
        ///   Underwater has depth-based absorption/fog that must be consistent
        ///   regardless of surface biome (water physics don't change per biome).
        ///   Future: per-biome underwater profiles can be added via a separate
        ///   BiomeUnderwaterOverride array.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AtmosphereProfile ResolveProfile(EnvironmentState state)
        {
            // Priority 1: Eclipse overrides everything
            if (state == EnvironmentState.ECLIPSE)
            {
                return _profileEclipse != null
                    ? _profileEclipse
                    : _profileDay;
            }

            // Priority 2: Underwater has fixed profile (depth absorption)
            if (state == EnvironmentState.UNDERWATER)
            {
                return _profileUnderwater != null
                    ? _profileUnderwater
                    : _profileDay;
            }

            // Priority 3: Biome override (surface states only)
            if (_activeBiomeProfile != null)
            {
                return _activeBiomeProfile;
            }

            // Priority 4: Default state-based profile
            AtmosphereProfile profile = state switch
            {
                EnvironmentState.SURFACE_DAY  => _profileDay,
                EnvironmentState.SURFACE_NIGHT => _profileNight,
                _                              => _profileDay
            };

            return profile != null ? profile : _profileDay;
        }

        #endregion

        #region ══════════ Публичный API ══════════

        // ══════════════════════════════════════════════════════════
        //  BIOME ATMOSPHERE HANDLING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by MapMagicBridge.OnBiomeChanged event.
        ///
        /// Looks up the biome ID in _biomeOverrides array.
        /// If found: activates the biome profile → starts transition.
        /// If not found: clears biome override → reverts to default
        /// state-based profile (Day/Night/Underwater/Eclipse).
        ///
        /// The transition uses the existing _transitionSpeed system
        /// for smooth blending between atmosphere profiles.
        ///
        /// THREAD SAFETY: Called from main thread only (SlowTick).
        /// ZERO GC: Array iteration, no allocations.
        /// </summary>
        /// <param name="biomeID">Biome index from terrain splat layer.</param>
        private void HandleBiomeChanged(int biomeID)
        {
            _currentBiomeID = biomeID;

            // ── Search for matching override ──
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

            // ── Apply or clear override ──
            _activeBiomeProfile = biomeProfile;

            // ── Force transition to new profile ──
            // Snapshot current values as origin, reset progress.
            // InterpolateAtmosphere will lerp to the new target
            // using the resolved profile (which now includes biome).
            _transitionOrigin   = _currentValues;
            _transitionProgress = 0f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string profileName = biomeProfile != null
                ? biomeProfile.name
                : "default (state-based)";
            Debug.Log(
                $"[HectonAtmosphere] Biome changed → ID {biomeID}, " +
                $"profile: {profileName}");
#endif
        }

        /// <summary>
        /// Запускает Великое Затмение на указанную длительность.
        /// Переключает атмосферу в состояние ECLIPSE с плавным переходом.
        /// </summary>
        /// <param name="duration">Длительность в секундах. Должна быть > 0.</param>
        public void TriggerEclipse(float duration)
        {
            if (duration <= 0f)
            {
                Debug.LogWarning(
                    "[HectonAtmosphere] TriggerEclipse: " +
                    "длительность должна быть > 0.",
                    this);
                return;
            }

            _eclipseActive        = true;
            _eclipseRemainingTime = duration;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[HectonAtmosphere] ◐ Великое Затмение! " +
                $"Длительность: {duration:F1} сек.",
                this);
#endif
        }

        /// <summary>Принудительно завершает затмение.</summary>
        public void EndEclipse()
        {
            _eclipseActive        = false;
            _eclipseRemainingTime = 0f;
        }

        /// <summary>
        /// Устанавливает внешний флаг погружения.
        /// Используется триггерами воды (OnTriggerEnter/Exit).
        /// </summary>
        public void SetUnderwater(bool isUnderwater)
        {
            _underwaterExternalFlag = isUnderwater;
        }

        /// <summary>Устанавливает нормализованное время суток [0..1].</summary>
        public void SetTimeOfDay(float normalized)
        {
            _cycleTimer = math.saturate(normalized) * _cycleDuration;
        }

        /// <summary>Устанавливает Y-координату поверхности воды.</summary>
        public void SetWaterSurfaceLevel(float worldY)
        {
            _waterSurfaceY = worldY;
        }

        /// <summary>Назначает Transform игрока для авто-определения погружения.</summary>
        public void SetPlayerTransform(Transform player)
        {
            _playerTransform = player;
        }

        /// <summary>Назначает основную камеру.</summary>
        public void SetMainCamera(Camera camera)
        {
            mainCamera = camera;
        }

        /// <summary>Устанавливает длительность цикла дня/ночи в секундах.</summary>
        public void SetCycleDuration(float seconds)
        {
            _cycleDuration = math.max(seconds, 1f);
        }

        /// <summary>Устанавливает скорость интерполяции между состояниями.</summary>
        public void SetTransitionSpeed(float speed)
        {
            _transitionSpeed = math.clamp(speed, 0.1f, 10f);
        }

        /// <summary>Устанавливает наклон орбиты солнца (градусы).</summary>
        public void SetOrbitalInclination(float degrees)
        {
            _orbitalInclination = math.clamp(degrees, 0f, 90f);
        }

        /// <summary>
        /// Assigns a depth provider at runtime. Replaces Inspector reference.
        /// Pass null to revert to Y-position based calculation.
        /// </summary>
        public void SetDepthProvider(IDepthProvider provider)
        {
            _depthProvider = provider;
        }

        #endregion
    }
}