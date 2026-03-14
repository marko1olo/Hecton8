// ============================================================================
// HECTON-8 — HectonCelestialEngine.cs
// Небесная механика экзолуны Гектон: газовый гигант Аэгир,
// затмения, planet-shine, окклюзия солнца, управление шейдером неба.
//
// ОТВЕТСТВЕННОСТИ:
//   1. Рендер газового гиганта (MaterialPropertyBlock, precision-safe rotation).
//   2. Детекция затмений (angular occlusion + hysteresis).
//   3. Eclipse backlight (Fresnel glow при затмении).
//   4. Planet-Shine (отражённый свет от Аэгира).
//   5. Sun occlusion (плавное скрытие LensFlare за планетой).
//   6. Skybox blend (день/ночь с плавным переходом).
//   7. Позиционирование солнечного диска (visual billboard).
//   8. Sky shader integration (cloud rotation, sun/aegir direction,
//      day/night color profiles via material properties).
//
// АРХИТЕКТУРА (v4.0):
//   • ITickable — интеграция с GameTickManager. Нативный Update() только
//     для [ExecuteAlways] в Editor (не в Play Mode).
//   • Прямые типизированные ссылки: HectonAtmosphereManager, LensFlareComponentSRP.
//     Reflection полностью удалена — zero boxing, zero GC.
//   • Precision-safe rotation: double аккумулятор → float через mod → frac() в шейдере.
//   • Все MaterialPropertyBlock кэшированы — zero аллокаций в hot path.
//   • Sky material: передаёт _GameTime (постоянно растущее время) вместо
//     _cloudRotation. Шейдер сам вычисляет offset = _GameTime * speed,
//     а frac() гарантирует бесшовность. Никаких скачков при сбросе таймера.
//   • Day/night color lerp: пропускается если _currentBlend не изменился
//     (epsilon guard) — zero redundant SetColor на MX350.
//   • _NightBlend передаётся в шейдер неба для управления видимостью звёзд.
//
// КООРДИНАЦИЯ С HectonAtmosphereManager:
//   • Если AtmosphereManager назначен: ОН управляет Directional Light и _SunDirection.
//     CelestialEngine только ЧИТАЕТ данные (SunAngle, light.transform.forward).
//     Sun intensity: CelestialEngine МОДУЛИРУЕТ intensity из AtmosphereManager окклюзией,
//     а не перезаписывает своим base значением. Race condition устранена.
//   • Если AtmosphereManager НЕ назначен: CelestialEngine использует внутреннюю
//     орбитальную модель для самостоятельного управления солнцем.
//
// SKY MATERIAL PIPELINE (v4.0):
//   • _GameTime: continuously increasing time accumulator (float).
//     The sky shader computes UV offset as _GameTime * _speed internally.
//     frac() inside the shader prevents any precision issues.
//     REPLACES the old _GlobalRotation / _cloudRotation approach that
//     caused visible "jerks" when the accumulator wrapped at 1.0.
//   • _NightBlend: day/night blend factor [0=day, 1=night].
//     Used by the sky shader to control star visibility.
//     Stars are only visible at night (multiplied by _NightBlend).
//   • _SunDirection: written to sky material AND global (for other shaders).
//     Convention: direction FROM sun (sunLight.transform.forward).
//   • _AegirDirection: normalized vector from camera TOWARD Aegir.
//     Written to sky material only (not global — only sky shader uses it).
//   • _SkyColorZenith/_SkyColorHorizon/_SkyColorNadir: lerped between
//     day and night profiles based on _currentBlend. Updated ONLY when
//     blend value actually changes (epsilon = 0.001).
//
// ZERO GC:
//   • Все MaterialPropertyBlock предаллоцированы (включая sun disc).
//   • Нет reflection GetValue/SetValue (boxing устранён).
//   • Unity.Mathematics для SIMD-оптимизированных вычислений.
//   • События (Action) — zero GC при Invoke (delegate, не closure).
//   • Sky material color updates skipped when blend unchanged — zero SetColor.
// ============================================================================

using System;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Hecton8.Atmosphere;

namespace Hecton8.Celestial
{
    // ─────────────────────────────────────────────
    // SKY COLOR PROFILE
    //
    // HDR color triplet for zenith/horizon/nadir.
    // Used for day and night sky configurations.
    // Lerped by CelestialEngine based on sun elevation.
    // ─────────────────────────────────────────────

    [Serializable]
    public struct SkyColorProfile
    {
        [ColorUsage(false, true)]
        [Tooltip("Sky color at zenith (directly overhead)")]
        public Color zenithColor;

        [ColorUsage(false, true)]
        [Tooltip("Sky color at horizon (eye level)")]
        public Color horizonColor;

        [ColorUsage(false, true)]
        [Tooltip("Sky color at nadir (directly below)")]
        public Color nadirColor;

        /// <summary>
        /// Returns a profile with all colors set to the given defaults.
        /// Used for serialization fallback when profile is uninitialized.
        /// </summary>
        public static SkyColorProfile Default(
            Color zenith, Color horizon, Color nadir)
        {
            return new SkyColorProfile
            {
                zenithColor  = zenith,
                horizonColor = horizon,
                nadirColor   = nadir
            };
        }
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class HectonCelestialEngine : MonoBehaviour, ITickable
    {
        // ─────────────────────────────────────────────
        // КОНФИГУРАЦИЯ
        // ─────────────────────────────────────────────

        [Header("═══ REFERENCES ═══")]
        [Tooltip("Directional Light, представляющий звезду (Солнце)")]
        [SerializeField] private Light sunLight;

        [Tooltip("Transform сферы газового гиганта Аэгир")]
        [SerializeField] private Transform aegirTransform;

        [Tooltip("Renderer сферы газового гиганта")]
        [SerializeField] private Renderer aegirRenderer;

        [Tooltip("Transform игрока (камеры на экзолуне)")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("Ссылка на HectonAtmosphereManager для синхронизации угла Солнца.\n"
               + "Если назначен — CelestialEngine НЕ вращает Directional Light и НЕ пишет _SunDirection.\n"
               + "Если не назначен — используется внутренняя орбитальная модель.")]
        [SerializeField] private HectonAtmosphereManager _atmosphereManager;

        [Header("═══ SKY MATERIAL ═══")]
        [Tooltip("Material шейдера HECTON/Sky/Hecton_AlienSky_Master.\n"
               + "CelestialEngine передаёт сюда _GameTime, направления светил,\n"
               + "и цвета дня/ночи.")]
        [SerializeField] private Material _skyMaterial;

        [Tooltip("Скорость вращения облаков. Передаётся в шейдер как _CloudSpeed.\n"
               + "Шейдер вычисляет offset = _GameTime * _CloudSpeed.")]
        [SerializeField] private float _cloudSpeed = 0.01f;

        [Header("═══ SKY COLOR PROFILES ═══")]
        [Tooltip("Цвета неба в дневное время")]
        [SerializeField] private SkyColorProfile _dayProfile = new SkyColorProfile
        {
            zenithColor  = new Color(0.05f, 0.08f, 0.25f, 1f),
            horizonColor = new Color(0.4f, 0.35f, 0.5f, 1f),
            nadirColor   = new Color(0.02f, 0.03f, 0.08f, 1f)
        };

        [Tooltip("Цвета неба в ночное время")]
        [SerializeField] private SkyColorProfile _nightProfile = new SkyColorProfile
        {
            zenithColor  = new Color(0.01f, 0.005f, 0.03f, 1f),
            horizonColor = new Color(0.08f, 0.05f, 0.12f, 1f),
            nadirColor   = new Color(0.005f, 0.003f, 0.01f, 1f)
        };

        [Header("═══ SUN OCCLUSION ═══")]
        [Tooltip("LensFlareComponentSRP на Directional Light солнца")]
        [SerializeField] private LensFlareComponentSRP _sunLensFlare;

        [Tooltip("Distance at which the sun visual disc is placed along the light's reverse forward vector")]
        [SerializeField] private float sunDistance = 100000f;

        [Tooltip("Optional visual sun disc transform (billboard/quad). Positioned at sunDistance.")]
        [SerializeField] private Transform sunVisualTransform;

        [Tooltip("Angular margin (degrees) outside Aegir disc where flare begins to fade")]
        [SerializeField] private float flareFadeMarginDegrees = 2.0f;

        [Tooltip("Speed of flare fade lerp (higher = faster response)")]
        [SerializeField] private float flareFadeSpeed = 5.0f;

        [Header("═══ SKYBOX ═══")]
        [SerializeField] private Material daySkybox;
        [SerializeField] private Material nightSkybox;

        [Tooltip("Материал скайбокса, поддерживающий _Blend и _StarIntensity")]
        [SerializeField] private Material blendedSkyboxMaterial;

        [Header("═══ ORBITAL PARAMETERS ═══")]
        [Tooltip("Полный цикл день/ночь в секундах (только в standalone режиме без AtmosphereManager)")]
        [SerializeField] private float orbitalPeriod = 3600f;

        [Tooltip("Ось вращения Солнца вокруг сцены (нормализуется). Только в standalone режиме.")]
        [SerializeField] private Vector3 sunOrbitAxis = Vector3.right;

        [Tooltip("Начальный угол Солнца в градусах")]
        [SerializeField] private float sunStartAngle;

        [Header("═══ ECLIPSE DETECTION ═══")]
        [Tooltip("Угловой радиус Аэгира для детекции затмения (градусы). 0 = авторасчёт")]
        [SerializeField] private float eclipseAngularRadiusOverride;

        [Tooltip("Допуск совпадения направлений для гистерезиса (градусы)")]
        [SerializeField] private float eclipseHysteresisMargin = 0.5f;

        [Header("═══ ECLIPSE BACKLIGHT ═══")]
        [Tooltip("Порог dot-product для начала эффекта подсветки (мягкий край)")]
        [SerializeField] private float backlitAlignmentSoftStart = 0.97f;

        [Tooltip("Порог dot-product для полной подсветки (жёсткий центр затмения)")]
        [SerializeField] private float backlitAlignmentFullStart = 0.995f;

        [Tooltip("Множитель backlit-фактора, отправляемого в шейдер")]
        [SerializeField] private float backlitFactorMultiplier = 1.0f;

        [Header("═══ PLANET-SHINE ═══")]
        [Tooltip("Интенсивность отражённого света при полной фазе")]
        [SerializeField] private float planetShineMaxIntensity = 0.35f;

        [Tooltip("Цвет Planet-Shine")]
        [SerializeField] private Color planetShineColor = Color.HSVToRGB(0.75f, 0.2f, 0.9f);

        [Tooltip("Порог фазы ниже которого Planet-Shine гасится")]
        [SerializeField] private float planetShineNewMoonThreshold = 0.1f;

        [Header("═══ SHADER PARAMETERS ═══")]
        [SerializeField] private float equatorialRotationSpeed = 0.02f;
        [SerializeField] private float polarRotationMultiplier = 0.4f;
        [SerializeField] private float backlitIntensity = 0.08f;
        [SerializeField] private float stormEmissionIntensity = 1.0f;

        [Header("═══ TRANSITION CURVES ═══")]
        [SerializeField] private float twilightStartAngle = 5f;
        [SerializeField] private float twilightEndAngle = -5f;

        // ─────────────────────────────────────────────
        // СОБЫТИЯ
        // ─────────────────────────────────────────────

        public static event Action OnEclipseStart;
        public static event Action OnEclipseEnd;
        public static event Action<float> OnSunAngleChanged;
        public static event Action<float> OnPlanetPhaseChanged;

        // ─────────────────────────────────────────────
        // RUNTIME STATE (zero-alloc)
        // ─────────────────────────────────────────────

        private Light _planetShineLight;
        private GameObject _planetShineLightGO;

        /// <summary>MaterialPropertyBlock для газового гиганта. Кэширован — zero GC.</summary>
        private MaterialPropertyBlock _aegirMPB;

        /// <summary>MaterialPropertyBlock для солнечного диска. Кэширован — zero GC.</summary>
        private MaterialPropertyBlock _sunDiscMPB;

        private float _currentSunAngle;
        private float _currentBlend;
        private float _currentStarIntensity;
        private float _currentPhase;
        private bool _isEclipseActive;
        private float _eclipseAngularRadius;
        private float _accumulatedOrbitalAngle;
        private float _currentBacklitFactor;

        // ═══ PRECISION-SAFE ROTATION TIMER (Aegir planet) ═══
        private double _rotationAccumulator;
        private float _rotationTimer;

        // ═══ GAME TIME ACCUMULATOR (sky shader) ═══ NEW ═══
        // Continuously increasing time. Never wraps. Never resets.
        // The sky shader uses frac(_GameTime * speed) internally,
        // which guarantees seamless UV scrolling with zero "jerks".
        // Replaces the old _cloudRotation approach that caused
        // visible snapping when the accumulator wrapped at 1.0.
        private float _gameTime;

        // ═══ SKY COLOR BLEND TRACKING ═══
        // Tracks previous blend value to skip redundant SetColor calls.
        // On MX350, every saved Material.SetColor matters.
        private float _previousBlendForColors;
        private const float COLOR_BLEND_EPSILON = 0.001f;

        // Sun occlusion state
        private float _sunOcclusionFactor;
        private float _smoothedOcclusionFactor;
        private float _baseSunIntensity;
        private bool _baseSunIntensityCaptured;
        private float _baseFlareIntensity;
        private float _baseFlareScale;
        private bool _baseFlareValuesCaptured;

        // Cached sun direction (world-space, pointing TOWARD the sun)
        private float3 _resolvedSunDirection;

        // Lazy initialization flag for eclipse radius
        private bool _eclipseRadiusCalculated;

        // Cached sun disc renderer (looked up once)
        private Renderer _cachedSunDiscRenderer;
        private bool _sunDiscRendererCached;

        // ═══ Per-frame cached Aegir radius ═══
        private float _cachedAegirRadius;

        // ═══ Shader property IDs — кэшируем один раз ═══
        //
        // NAMING CONVENTION:
        //   _ID_PropertyName for shader property IDs to distinguish from
        //   runtime state variables that may share similar names.
        //   Exception: legacy IDs kept as-is for compatibility.
        //
        // ISOLATION:
        //   _GlobalRotation is used by Aegir (via MPB) ONLY now.
        //   Sky material uses _GameTime instead.
        //   Zero conflict by design.

        private static readonly int _ID_SunDirection       = Shader.PropertyToID("_SunDirection");
        private static readonly int _ID_BacklitIntensity   = Shader.PropertyToID("_BacklitIntensity");
        private static readonly int _ID_EquatorialSpeed    = Shader.PropertyToID("_EquatorialSpeed");
        private static readonly int _ID_PolarMultiplier    = Shader.PropertyToID("_PolarMultiplier");
        private static readonly int _ID_PlanetPhase        = Shader.PropertyToID("_PlanetPhase");
        private static readonly int _ID_StormEmission      = Shader.PropertyToID("_StormEmission");
        private static readonly int _ID_Blend              = Shader.PropertyToID("_Blend");
        private static readonly int _ID_StarIntensity      = Shader.PropertyToID("_StarIntensity");
        private static readonly int _ID_FresnelSunDir      = Shader.PropertyToID("_FresnelSunDir");
        private static readonly int _ID_SunBacklitFactor   = Shader.PropertyToID("_SunBacklitFactor");
        private static readonly int _ID_GlobalRotation     = Shader.PropertyToID("_GlobalRotation");
        private static readonly int _ID_OcclusionFactor    = Shader.PropertyToID("_OcclusionFactor");
        private static readonly int _ID_EmissionColor      = Shader.PropertyToID("_EmissionColor");
        private static readonly int _ID_AegirDirection     = Shader.PropertyToID("_AegirDirection");
        private static readonly int _ID_SkyColorZenith     = Shader.PropertyToID("_SkyColorZenith");
        private static readonly int _ID_SkyColorHorizon    = Shader.PropertyToID("_SkyColorHorizon");
        private static readonly int _ID_SkyColorNadir      = Shader.PropertyToID("_SkyColorNadir");

        // ═══ NEW SHADER PROPERTY IDs ═══
        private static readonly int _ID_GameTime           = Shader.PropertyToID("_GameTime");
        private static readonly int _ID_NightBlend         = Shader.PropertyToID("_NightBlend");

        // ─────────────────────────────────────────────
        // LIFECYCLE
        // ─────────────────────────────────────────────

        private void OnEnable()
        {
            ValidateReferences();
            InitializeMaterialPropertyBlocks();
            InitializePlanetShineLight();

            _accumulatedOrbitalAngle = sunStartAngle;
            _currentBacklitFactor = 0f;
            _smoothedOcclusionFactor = 0f;
            _sunOcclusionFactor = 0f;
            _baseSunIntensityCaptured = false;
            _baseFlareValuesCaptured = false;
            _eclipseRadiusCalculated = false;
            _sunDiscRendererCached = false;

            _rotationAccumulator = 0.0;
            _rotationTimer = 0f;

            // ═══ MODIFIED ═══
            // Initialize game time accumulator. Starts at 0, grows forever.
            // No wrapping, no resetting. The shader handles frac() internally.
            _gameTime = 0f;

            // Force initial color update on first frame
            _previousBlendForColors = -1f;

            // Capture base sun intensity (only used in standalone mode)
            if (sunLight != null && _atmosphereManager == null)
            {
                _baseSunIntensity = sunLight.intensity;
                _baseSunIntensityCaptured = true;
            }

            // Capture base flare values
            CaptureBaseFlareValues();

            if (blendedSkyboxMaterial != null)
            {
                RenderSettings.skybox = blendedSkyboxMaterial;
            }

            // ── ITickable registration (Play Mode only) ──
            if (Application.isPlaying)
            {
                GameTickManager tickManager = GameTickManager.Instance;
                if (tickManager != null)
                {
                    tickManager.Register((ITickable)this);
                }
            }
        }

        private void OnDisable()
        {
            RestoreSunDefaults();
            CleanupPlanetShineLight();

            // ── ITickable unregistration ──
            if (Application.isPlaying)
            {
                GameTickManager tickManager = GameTickManager.Instance;
                if (tickManager != null)
                {
                    tickManager.Unregister((ITickable)this);
                }
            }
        }

        private void OnDestroy()
        {
            // ── Очистка статических событий для предотвращения утечек ──
            OnEclipseStart = null;
            OnEclipseEnd = null;
            OnSunAngleChanged = null;
            OnPlanetPhaseChanged = null;
        }

        /// <summary>
        /// Editor-only Update for [ExecuteAlways] preview.
        /// In Play Mode, GameTickManager calls Tick() instead.
        /// </summary>
#if UNITY_EDITOR
        private void Update()
        {
            if (!Application.isPlaying)
            {
                float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
                Tick(dt);
            }
        }
#endif

        // ─────────────────────────────────────────────
        // ITickable — MAIN LOOP
        // ─────────────────────────────────────────────

        /// <summary>
        /// Main update loop. Called by GameTickManager every frame (Play Mode)
        /// or by editor-only Update (Edit Mode preview).
        /// Zero GC in hot path.
        /// </summary>
        public void Tick(float deltaTime)
        {
            // ═══ PRECISION-SAFE ROTATION TIMER (Aegir planet) ═══
            // This timer is for the gas giant's own rotation (MaterialPropertyBlock).
            // Completely independent from the sky shader's _GameTime.
            _rotationAccumulator += (double)deltaTime;
            if (_rotationAccumulator > 10000.0)
                _rotationAccumulator -= 10000.0;
            _rotationTimer = (float)_rotationAccumulator;

            // ═══ GAME TIME ACCUMULATOR (sky shader) ═══ MODIFIED ═══
            // Simply accumulate time. Never wrap. Never reset.
            // The sky shader computes: offset = frac(_GameTime * speed)
            // This guarantees zero "jerks" — no discontinuity ever.
            // Float precision is fine here because the shader uses frac()
            // which discards the integer part. Even at _gameTime = 100000,
            // frac(100000.0 * 0.01) = frac(1000.0) = 0.0 — perfectly precise.
            _gameTime += deltaTime;

            // ═══ LAZY ECLIPSE RADIUS (one-time after all refs valid) ═══
            if (!_eclipseRadiusCalculated && aegirTransform != null && playerTransform != null)
            {
                CalculateEclipseAngularRadius();
                _eclipseRadiusCalculated = true;
            }

            // ═══ CACHE AEGIR RADIUS ONCE PER FRAME ═══
            _cachedAegirRadius = ComputeAegirWorldRadius();

            UpdateSunPosition(deltaTime);
            ResolveSunDirection();
            UpdateSunVisualPosition();

            float sunElevation = CalculateSunElevation();
            _currentSunAngle = sunElevation;

            UpdateSkyboxBlend(sunElevation);
            UpdateStarIntensity(sunElevation);
            UpdateGlobalShaderData();

            // ═══ SKY MATERIAL UPDATE ═══
            // After ResolveSunDirection and UpdateSkyboxBlend (needs _currentBlend).
            // Before eclipse/occlusion (doesn't depend on them).
            UpdateSkyMaterial();

            CalculateEclipseBacklight();
            UpdateAegirMaterial();
            UpdatePlanetShine();
            DetectEclipse();

            UpdateSunOcclusion(deltaTime);
            ApplySunOcclusion();

            OnSunAngleChanged?.Invoke(_currentSunAngle);
        }

        // ─────────────────────────────────────────────
        // INITIALIZATION
        // ─────────────────────────────────────────────

        private void ValidateReferences()
        {
            if (sunLight == null)
                Debug.LogError("[HectonCelestialEngine] Sun Light is not assigned!", this);

            if (aegirTransform == null)
                Debug.LogError("[HectonCelestialEngine] Aegir Transform is not assigned!", this);

            if (playerTransform == null)
            {
                // Camera.main uses FindObjectWithTag internally — acceptable in OnEnable, not hot path
                var cam = Camera.main;
                if (cam != null)
                {
                    playerTransform = cam.transform;
                    Debug.LogWarning("[HectonCelestialEngine] Player not assigned, using Main Camera.");
                }
            }

            if (_skyMaterial == null)
            {
                Debug.LogWarning(
                    "[HectonCelestialEngine] Sky Material is not assigned! " +
                    "Cloud rotation, stars and sky colors will not be updated.", this);
            }
        }

        private void InitializeMaterialPropertyBlocks()
        {
            _aegirMPB = new MaterialPropertyBlock();
            _sunDiscMPB = new MaterialPropertyBlock();
        }

        private void InitializePlanetShineLight()
        {
            const string lightName = "AegirSecondaryLight_PlanetShine";

            var existing = transform.Find(lightName);
            if (existing != null)
            {
                _planetShineLightGO = existing.gameObject;
                _planetShineLight = _planetShineLightGO.GetComponent<Light>();
            }
            else
            {
                _planetShineLightGO = new GameObject(lightName);
                _planetShineLightGO.transform.SetParent(transform, false);
                _planetShineLightGO.hideFlags = HideFlags.DontSave;
                _planetShineLight = _planetShineLightGO.AddComponent<Light>();
            }

            _planetShineLight.type = LightType.Directional;
            _planetShineLight.color = planetShineColor;
            _planetShineLight.intensity = 0f;
            _planetShineLight.shadows = LightShadows.None;
            _planetShineLight.renderMode = LightRenderMode.Auto;
            _planetShineLight.cullingMask = ~LayerMask.GetMask("Celestial");
        }

        private void CleanupPlanetShineLight()
        {
            if (_planetShineLightGO != null)
            {
                if (Application.isPlaying)
                    Destroy(_planetShineLightGO);
                else
                    DestroyImmediate(_planetShineLightGO);
            }
        }

        /// <summary>
        /// Captures base LensFlare values for occlusion modulation.
        /// Direct property access — no reflection, no boxing.
        /// </summary>
        private void CaptureBaseFlareValues()
        {
            if (_baseFlareValuesCaptured) return;
            if (_sunLensFlare == null) return;

            _baseFlareIntensity = _sunLensFlare.intensity;
            _baseFlareScale = _sunLensFlare.scale;
            _baseFlareValuesCaptured = true;
        }

        private void CalculateEclipseAngularRadius()
        {
            if (eclipseAngularRadiusOverride > 0f)
            {
                _eclipseAngularRadius = eclipseAngularRadiusOverride;
                return;
            }

            if (aegirTransform != null && playerTransform != null)
            {
                float radius = _cachedAegirRadius;
                float distance = math.max(
                    math.length((float3)aegirTransform.position - (float3)playerTransform.position),
                    0.01f
                );
                _eclipseAngularRadius = math.degrees(math.atan2(radius, distance));
            }
            else
            {
                _eclipseAngularRadius = 5f;
            }
        }

        /// <summary>
        /// Computes Aegir world radius. Called ONCE per frame, result cached in _cachedAegirRadius.
        /// </summary>
        private float ComputeAegirWorldRadius()
        {
            if (aegirRenderer != null)
            {
                float3 extents = (float3)aegirRenderer.bounds.extents;
                return math.cmax(extents);
            }
            if (aegirTransform != null)
            {
                float3 scale = (float3)aegirTransform.lossyScale;
                return math.cmax(scale) * 0.5f;
            }
            return 1f;
        }

        /// <summary>
        /// Returns cached Aegir radius. Use in hot path instead of ComputeAegirWorldRadius().
        /// </summary>
        private float GetAegirWorldRadius()
        {
            return _cachedAegirRadius;
        }

        // ─────────────────────────────────────────────
        // SUN DIRECTION RESOLUTION
        // ─────────────────────────────────────────────

        /// <summary>
        /// Resolves the true 3D sun direction from the Directional Light transform.
        /// The light is rotated by AtmosphereManager (if present) or by our internal orbit.
        /// Either way, light.transform.forward is always the authoritative sun direction.
        ///
        /// For Directional Light: forward points FROM sun TO scene.
        /// We negate to get direction TOWARD the sun.
        /// No reflection. No boxing. One transform read.
        /// </summary>
        private void ResolveSunDirection()
        {
            if (sunLight != null)
            {
                _resolvedSunDirection = -(float3)sunLight.transform.forward;
            }
        }

        // ─────────────────────────────────────────────
        // SUN ORBITAL LOGIC
        // ─────────────────────────────────────────────

        /// <summary>
        /// Updates sun position. Two modes:
        ///
        /// WITH AtmosphereManager:
        ///   Reads SunAngle for internal tracking only.
        ///   Does NOT rotate the light (AtmosphereManager is authority).
        ///   Eliminates race condition on light rotation.
        ///
        /// WITHOUT AtmosphereManager (standalone):
        ///   Uses internal orbit model to rotate the Directional Light.
        /// </summary>
        private void UpdateSunPosition(float dt)
        {
            if (sunLight == null) return;

            // ── AtmosphereManager mode: read angle, don't rotate light ──
            if (_atmosphereManager != null)
            {
                _accumulatedOrbitalAngle = _atmosphereManager.SunAngle;
                return;
            }

            // ── Standalone mode: internal orbit ──
            UpdateInternalOrbit(dt);

            float3 axis = math.normalizesafe((float3)sunOrbitAxis, new float3(1, 0, 0));
            quaternion rotation = quaternion.AxisAngle(axis, math.radians(_accumulatedOrbitalAngle));
            float3 sunForward = math.mul(rotation, new float3(0, 0, 1));

            sunLight.transform.rotation = Quaternion.LookRotation((Vector3)sunForward);
        }

        private void UpdateInternalOrbit(float dt)
        {
            float degreesPerSecond = 360f / math.max(orbitalPeriod, 1f);
            _accumulatedOrbitalAngle += degreesPerSecond * dt;
            _accumulatedOrbitalAngle %= 360f;
        }

        private void UpdateSunVisualPosition()
        {
            if (sunVisualTransform == null || sunLight == null) return;

            Vector3 towardSun = -sunLight.transform.forward;
            Vector3 cameraPos = playerTransform != null ? playerTransform.position : Vector3.zero;

            sunVisualTransform.position = cameraPos + towardSun * sunDistance;

            if (playerTransform != null)
            {
                sunVisualTransform.LookAt(playerTransform.position, Vector3.up);
            }
        }

        private float CalculateSunElevation()
        {
            float3 toSun = _resolvedSunDirection;
            float sinElevation = math.dot(toSun, new float3(0, 1, 0));
            return math.degrees(math.asin(math.clamp(sinElevation, -1f, 1f)));
        }

        // ─────────────────────────────────────────────
        // SKY MATERIAL UPDATE
        // ─────────────────────────────────────────────

        /// <summary>
        /// Updates the sky shader material with:
        ///   1. _GameTime — continuously increasing time (float).
        ///      The shader computes UV offset as frac(_GameTime * speed).
        ///      This REPLACES the old _GlobalRotation approach.
        ///      No wrapping in C# = no "jerks" = seamless scrolling.
        ///   2. _NightBlend — day/night factor [0=day, 1=night].
        ///      Used by the shader to fade in stars at night.
        ///   3. Sun direction (_SunDirection) — direction FROM sun.
        ///   4. Aegir direction (_AegirDirection) — normalized from camera TO Aegir.
        ///   5. Sky colors — lerped between day/night profiles by _currentBlend.
        ///      Colors are ONLY updated when _currentBlend actually changes
        ///      (epsilon guard). On MX350, skipping 3x SetColor per frame
        ///      when the sun isn't moving saves measurable GPU command overhead.
        ///
        /// Called from Tick() after ResolveSunDirection() and UpdateSkyboxBlend().
        /// Zero GC: no allocations, no boxing, no string lookups (PropertyToID cached).
        /// </summary>
        private void UpdateSkyMaterial()
        {
            if (_skyMaterial == null) return;

            // ═══ 1. Game Time ═══ MODIFIED ═══
            // Continuously growing time. The shader uses frac() internally.
            // This completely eliminates the "jerk" artifact that occurred
            // when the old _cloudRotation wrapped at 1.0.
            _skyMaterial.SetFloat(_ID_GameTime, _gameTime);

            // ═══ 2. Night Blend ═══ NEW ═══
            // Tells the sky shader how "nighttime" it is.
            // Stars multiply their brightness by this value.
            // 0.0 = full day (stars invisible), 1.0 = full night (stars visible).
            _skyMaterial.SetFloat(_ID_NightBlend, _currentBlend);

            // ═══ 3. Sun Direction ═══
            // Convention: direction FROM sun (sunLight.transform.forward).
            // Same convention as UpdateGlobalShaderData and AtmosphereManager.
            // _resolvedSunDirection points TOWARD sun, so we negate.
            float3 fromSun = -_resolvedSunDirection;
            _skyMaterial.SetVector(_ID_SunDirection,
                new Vector4(fromSun.x, fromSun.y, fromSun.z, 0f));

            // ═══ 4. Aegir Direction ═══
            // Normalized vector from camera TOWARD Aegir.
            // The sky shader uses this for the Aegir halo effect.
            if (aegirTransform != null && playerTransform != null)
            {
                float3 playerPos = (float3)playerTransform.position;
                float3 aegirPos  = (float3)aegirTransform.position;
                float3 toAegir   = math.normalizesafe(aegirPos - playerPos);

                _skyMaterial.SetVector(_ID_AegirDirection,
                    new Vector4(toAegir.x, toAegir.y, toAegir.z, 0f));
            }

            // ═══ 5. Sky Colors (day/night lerp) ═══
            // Only update when _currentBlend has actually changed.
            // This saves 3x Material.SetColor calls per frame when
            // the sun is stationary (paused, slow cycle, etc).
            float blendDelta = math.abs(_currentBlend - _previousBlendForColors);

            if (blendDelta > COLOR_BLEND_EPSILON)
            {
                _previousBlendForColors = _currentBlend;

                Color zenith  = Color.Lerp(
                    _dayProfile.zenithColor,
                    _nightProfile.zenithColor,
                    _currentBlend);

                Color horizon = Color.Lerp(
                    _dayProfile.horizonColor,
                    _nightProfile.horizonColor,
                    _currentBlend);

                Color nadir   = Color.Lerp(
                    _dayProfile.nadirColor,
                    _nightProfile.nadirColor,
                    _currentBlend);

                _skyMaterial.SetColor(_ID_SkyColorZenith,  zenith);
                _skyMaterial.SetColor(_ID_SkyColorHorizon, horizon);
                _skyMaterial.SetColor(_ID_SkyColorNadir,   nadir);
            }
        }

        // ─────────────────────────────────────────────
        // SUN OCCLUSION
        // ─────────────────────────────────────────────

        private void UpdateSunOcclusion(float dt)
        {
            if (_isEclipseActive)
            {
                _sunOcclusionFactor = 1.0f;
                _smoothedOcclusionFactor = math.lerp(
                    _smoothedOcclusionFactor, 1.0f, math.saturate(flareFadeSpeed * dt));
                return;
            }

            if (aegirTransform == null || playerTransform == null)
            {
                _sunOcclusionFactor = 0f;
                _smoothedOcclusionFactor = math.lerp(
                    _smoothedOcclusionFactor, 0f, math.saturate(flareFadeSpeed * dt));
                return;
            }

            float3 playerPos = (float3)playerTransform.position;
            float3 aegirPos  = (float3)aegirTransform.position;
            float3 toSun     = _resolvedSunDirection;
            float3 toAegir   = math.normalizesafe(aegirPos - playerPos);

            float dotSunAegir = math.dot(toSun, toAegir);
            float angularSeparationDeg = math.degrees(
                math.acos(math.clamp(dotSunAegir, -1f, 1f)));

            float radius = GetAegirWorldRadius();
            float dist = math.max(math.length(aegirPos - playerPos), 0.01f);
            float dynamicAngularRadius = math.degrees(math.atan2(radius, dist));

            float innerEdge = dynamicAngularRadius;
            float outerEdge = dynamicAngularRadius + math.max(flareFadeMarginDegrees, 0.01f);

            if (angularSeparationDeg <= innerEdge)
            {
                _sunOcclusionFactor = 1.0f;
            }
            else if (angularSeparationDeg < outerEdge)
            {
                float t = (outerEdge - angularSeparationDeg) / (outerEdge - innerEdge);
                _sunOcclusionFactor = SmoothStep01(t);
            }
            else
            {
                _sunOcclusionFactor = 0f;
            }

            _smoothedOcclusionFactor = math.lerp(
                _smoothedOcclusionFactor,
                _sunOcclusionFactor,
                math.saturate(flareFadeSpeed * dt));

            if (_smoothedOcclusionFactor < 0.001f) _smoothedOcclusionFactor = 0f;
            if (_smoothedOcclusionFactor > 0.999f) _smoothedOcclusionFactor = 1f;
        }

        /// <summary>
        /// Applies occlusion to sun light, lens flare, and sun disc.
        /// Direct typed access — no reflection, no boxing, zero GC.
        /// Sun disc uses cached MaterialPropertyBlock (_sunDiscMPB).
        ///
        /// INTENSITY COORDINATION:
        ///   WITH AtmosphereManager: reads current sunLight.intensity (already set by AtmosphereManager)
        ///   and MODULATES it by occlusion visibility. Does NOT overwrite with _baseSunIntensity.
        ///   WITHOUT AtmosphereManager: uses captured _baseSunIntensity as base.
        /// </summary>
        private void ApplySunOcclusion()
        {
            float visibility = 1.0f - _smoothedOcclusionFactor;

            // ── Sun Light Intensity ──
            if (sunLight != null)
            {
                if (_atmosphereManager != null)
                {
                    // AtmosphereManager already set intensity (with horizon fade).
                    // We MODULATE it by occlusion — multiplicative, not overwrite.
                    // Read current value (set by AtmosphereManager this frame), multiply by visibility.
                    sunLight.intensity *= visibility;
                }
                else if (_baseSunIntensityCaptured)
                {
                    // Standalone mode: use our captured base intensity
                    sunLight.intensity = _baseSunIntensity * visibility;
                }
            }

            // ── Lens Flare (direct typed access — zero boxing) ──
            if (_sunLensFlare != null && _baseFlareValuesCaptured)
            {
                _sunLensFlare.intensity = _baseFlareIntensity * visibility;
                _sunLensFlare.scale = _baseFlareScale * visibility;

                bool shouldBeEnabled = visibility > 0.001f;
                if (_sunLensFlare.enabled != shouldBeEnabled)
                {
                    _sunLensFlare.enabled = shouldBeEnabled;
                }
            }

            // ── Sun Visual Disc (cached MPB — zero GC) ──
            if (sunVisualTransform != null)
            {
                bool shouldBeActive = visibility > 0.001f;
                if (sunVisualTransform.gameObject.activeSelf != shouldBeActive)
                {
                    sunVisualTransform.gameObject.SetActive(shouldBeActive);
                }

                if (shouldBeActive)
                {
                    Renderer sunRenderer = GetCachedSunDiscRenderer();
                    if (sunRenderer != null)
                    {
                        sunRenderer.GetPropertyBlock(_sunDiscMPB);
                        _sunDiscMPB.SetFloat(_ID_OcclusionFactor, visibility);
                        _sunDiscMPB.SetColor(_ID_EmissionColor, Color.white * visibility);
                        sunRenderer.SetPropertyBlock(_sunDiscMPB);
                    }
                }
            }
        }

        /// <summary>
        /// Lazy-cached Renderer lookup for sun disc. One GetComponent, ever.
        /// </summary>
        private Renderer GetCachedSunDiscRenderer()
        {
            if (!_sunDiscRendererCached && sunVisualTransform != null)
            {
                sunVisualTransform.TryGetComponent(out _cachedSunDiscRenderer);
                _sunDiscRendererCached = true;
            }
            return _cachedSunDiscRenderer;
        }

        private void RestoreSunDefaults()
        {
            if (sunLight != null && _baseSunIntensityCaptured && _atmosphereManager == null)
            {
                sunLight.intensity = _baseSunIntensity;
            }

            if (_sunLensFlare != null && _baseFlareValuesCaptured)
            {
                _sunLensFlare.intensity = _baseFlareIntensity;
                _sunLensFlare.scale = _baseFlareScale;

                if (!_sunLensFlare.enabled)
                    _sunLensFlare.enabled = true;
            }

            if (sunVisualTransform != null && !sunVisualTransform.gameObject.activeSelf)
            {
                sunVisualTransform.gameObject.SetActive(true);
            }
        }

        // ─────────────────────────────────────────────
        // SKYBOX BLEND
        // ─────────────────────────────────────────────

        private void UpdateSkyboxBlend(float sunElevation)
        {
            if (blendedSkyboxMaterial == null) return;

            float range = twilightStartAngle - twilightEndAngle;
            if (range < 0.001f) range = 10f;

            _currentBlend = math.saturate((twilightStartAngle - sunElevation) / range);
            _currentBlend = SmoothStep01(_currentBlend);

            blendedSkyboxMaterial.SetFloat(_ID_Blend, _currentBlend);
        }

        private void UpdateStarIntensity(float sunElevation)
        {
            if (blendedSkyboxMaterial == null) return;

            float range = twilightStartAngle - twilightEndAngle;
            if (range < 0.001f) range = 10f;

            _currentStarIntensity = math.saturate((twilightStartAngle - sunElevation) / range);
            _currentStarIntensity = SmoothStep01(_currentStarIntensity);

            blendedSkyboxMaterial.SetFloat(_ID_StarIntensity, _currentStarIntensity);
        }

        // ─────────────────────────────────────────────
        // GLOBAL SHADER DATA
        // ─────────────────────────────────────────────

        /// <summary>
        /// Writes _SunDirection to global shader ONLY in standalone mode.
        /// When AtmosphereManager is present, IT is the authority for _SunDirection.
        /// This eliminates the race condition where two scripts write to the same global.
        ///
        /// NOTE: The sky material also receives _SunDirection via UpdateSkyMaterial()
        /// (Material.SetVector). This global write is for OTHER shaders (terrain, water, etc.)
        /// that read _SunDirection as a global property.
        ///
        /// Convention: _SunDirection = sunLight.transform.forward (direction FROM sun, same as AtmosphereManager).
        /// </summary>
        private void UpdateGlobalShaderData()
        {
            // AtmosphereManager handles _SunDirection — avoid double-write
            if (_atmosphereManager != null) return;

            // _resolvedSunDirection points TOWARD the sun (-forward).
            // Negate to match convention: _SunDirection = forward (FROM sun).
            float3 fromSun = -_resolvedSunDirection;
            Shader.SetGlobalVector(_ID_SunDirection, new Vector4(fromSun.x, fromSun.y, fromSun.z, 0f));
        }

        // ─────────────────────────────────────────────
        // ECLIPSE BACKLIGHT
        // ─────────────────────────────────────────────

        private void CalculateEclipseBacklight()
        {
            _currentBacklitFactor = 0f;

            if (aegirTransform == null || playerTransform == null) return;

            float3 playerPos = (float3)playerTransform.position;
            float3 aegirPos  = (float3)aegirTransform.position;

            float3 playerToSun = _resolvedSunDirection;
            float3 playerToGiant = math.normalizesafe(aegirPos - playerPos);

            float alignment = math.dot(playerToSun, playerToGiant);

            if (alignment > backlitAlignmentSoftStart)
            {
                float range = math.max(backlitAlignmentFullStart - backlitAlignmentSoftStart, 0.001f);
                float t = math.saturate((alignment - backlitAlignmentSoftStart) / range);
                t = SmoothStep01(t);

                _currentBacklitFactor = math.saturate(t * backlitFactorMultiplier);
            }
        }

        // ─────────────────────────────────────────────
        // AEGIR MATERIAL
        // ─────────────────────────────────────────────

        private void UpdateAegirMaterial()
        {
            if (aegirRenderer == null) return;

            aegirRenderer.GetPropertyBlock(_aegirMPB);

            float3 toSun = _resolvedSunDirection;

            if (aegirTransform != null && playerTransform != null)
            {
                float3 aegirToPlayer = math.normalizesafe(
                    (float3)playerTransform.position - (float3)aegirTransform.position);
                _currentPhase = math.dot(toSun, aegirToPlayer);
            }
            else
            {
                _currentPhase = math.dot(toSun, new float3(0, 0, 1));
            }

            _aegirMPB.SetVector(_ID_FresnelSunDir, new Vector4(toSun.x, toSun.y, toSun.z, 0));
            _aegirMPB.SetFloat(_ID_BacklitIntensity, backlitIntensity);
            _aegirMPB.SetFloat(_ID_EquatorialSpeed, equatorialRotationSpeed);
            _aegirMPB.SetFloat(_ID_PolarMultiplier, polarRotationMultiplier);
            _aegirMPB.SetFloat(_ID_PlanetPhase, _currentPhase);
            _aegirMPB.SetFloat(_ID_StormEmission, stormEmissionIntensity);
            _aegirMPB.SetFloat(_ID_SunBacklitFactor, _currentBacklitFactor);
            _aegirMPB.SetFloat(_ID_GlobalRotation, _rotationTimer);

            aegirRenderer.SetPropertyBlock(_aegirMPB);

            OnPlanetPhaseChanged?.Invoke(_currentPhase);
        }

        // ─────────────────────────────────────────────
        // PLANET-SHINE
        // ─────────────────────────────────────────────

        private void UpdatePlanetShine()
        {
            if (_planetShineLight == null || aegirTransform == null || playerTransform == null)
                return;

            float3 aegirPos   = (float3)aegirTransform.position;
            float3 playerPos  = (float3)playerTransform.position;

            float3 aegirToPlayer = math.normalizesafe(playerPos - aegirPos);
            float3 aegirToSun = _resolvedSunDirection;

            float rawPhase = math.dot(aegirToSun, aegirToPlayer);

            float phaseFactor = math.saturate(
                (rawPhase - planetShineNewMoonThreshold) /
                math.max(1f - planetShineNewMoonThreshold, 0.01f));
            phaseFactor = phaseFactor * phaseFactor;

            float eclipseDim = 1f - _currentBacklitFactor;
            float intensity = phaseFactor * eclipseDim * planetShineMaxIntensity;

            _planetShineLight.transform.rotation = Quaternion.LookRotation(
                (Vector3)(-aegirToPlayer));

            _planetShineLight.intensity = intensity;
            _planetShineLight.color = planetShineColor;
        }

        // ─────────────────────────────────────────────
        // ECLIPSE DETECTION
        // ─────────────────────────────────────────────

        private void DetectEclipse()
        {
            if (aegirTransform == null || playerTransform == null)
                return;

            float3 playerPos = (float3)playerTransform.position;
            float3 aegirPos  = (float3)aegirTransform.position;

            float3 toSun = _resolvedSunDirection;
            float3 toAegir = math.normalizesafe(aegirPos - playerPos);

            float dotSunAegir = math.dot(toSun, toAegir);
            float angleDeg = math.degrees(math.acos(math.clamp(dotSunAegir, -1f, 1f)));

            float radius = GetAegirWorldRadius();
            float dist = math.max(math.length(aegirPos - playerPos), 0.01f);
            float dynamicAngularRadius = math.degrees(math.atan2(radius, dist));

            float enterThreshold = dynamicAngularRadius;
            float exitThreshold  = dynamicAngularRadius + eclipseHysteresisMargin;

            bool sunOccluded = angleDeg < enterThreshold;

            if (sunOccluded && !_isEclipseActive)
            {
                _isEclipseActive = true;
                OnEclipseStart?.Invoke();
            }
            else if (!sunOccluded && _isEclipseActive && angleDeg > exitThreshold)
            {
                _isEclipseActive = false;
                OnEclipseEnd?.Invoke();
            }
        }

        // ─────────────────────────────────────────────
        // UTILITY
        // ─────────────────────────────────────────────

        private static float SmoothStep01(float t)
        {
            t = math.saturate(t);
            return t * t * (3f - 2f * t);
        }

        // ─────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────

        public float SunElevation => _currentSunAngle;
        public float DayNightBlend => _currentBlend;
        public float PlanetPhase => _currentPhase;
        public bool IsEclipseActive => _isEclipseActive;
        public float EclipseBacklitFactor => _currentBacklitFactor;
        public float StarIntensity => _currentStarIntensity;
        public Vector3 ResolvedSunDirection => (Vector3)_resolvedSunDirection;
        public float SunOcclusionFactor => _smoothedOcclusionFactor;
        public float RotationTimer => _rotationTimer;

        // ═══ MODIFIED ═══
        // Replaced CloudRotation with GameTime.
        // GameTime is the continuously growing time accumulator
        // passed to the sky shader as _GameTime.
        public float GameTime => _gameTime;

        public void SetOrbitalAngle(float angleDegrees)
        {
            _accumulatedOrbitalAngle = angleDegrees % 360f;
        }

        // ─────────────────────────────────────────────
        // EDITOR GIZMOS
        // ─────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (aegirTransform == null || playerTransform == null) return;

            float3 aegirPos  = (float3)aegirTransform.position;
            float3 playerPos = (float3)playerTransform.position;

            // Line Aegir -> Player
            Gizmos.color = planetShineColor;
            Gizmos.DrawLine((Vector3)aegirPos, (Vector3)playerPos);

            // Eclipse cone + sun ray
            Gizmos.color = _isEclipseActive ? Color.red : Color.yellow;
            float3 toSun = _resolvedSunDirection;
            Gizmos.DrawRay((Vector3)playerPos, (Vector3)(toSun * 50f));

            float3 toAegir = math.normalizesafe(aegirPos - playerPos);
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.5f);
            Gizmos.DrawRay((Vector3)playerPos,
                (Vector3)(toAegir * math.length(aegirPos - playerPos)));

            // Backlit factor
            float gizmoRadius = GetAegirWorldRadius();
            if (_currentBacklitFactor > 0.01f)
            {
                Gizmos.color = new Color(1f, 0.8f, 0.2f, _currentBacklitFactor);
                Gizmos.DrawWireSphere((Vector3)aegirPos, gizmoRadius * 1.05f);
            }

            // Angular radius
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere((Vector3)aegirPos, gizmoRadius);

            // Sun occlusion
            if (_smoothedOcclusionFactor > 0.01f)
            {
                Gizmos.color = new Color(1f, 0.2f, 0f, _smoothedOcclusionFactor * 0.6f);
                Gizmos.DrawWireSphere((Vector3)aegirPos, gizmoRadius * 1.02f);
            }

            // Sun visual
            if (sunVisualTransform != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(sunVisualTransform.position, 500f);
            }

            // Aegir direction indicator (for sky shader debug)
            if (_skyMaterial != null)
            {
                Gizmos.color = new Color(0.6f, 0.5f, 0.8f, 0.7f);
                Gizmos.DrawRay((Vector3)playerPos,
                    (Vector3)(math.normalizesafe(aegirPos - playerPos) * 30f));
            }
        }
#endif
    }
}