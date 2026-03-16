// ============================================================================
// HECTON-8 — HectonUnderwaterVisuals.cs  v5.0
// ЕДИНОЛИЧНЫЙ ДИРЕКТОР СРЕДЫ: туман, свет, цвета, камера, рассеивание солнца.
//
// ═══════════════════════════════════════════════════════════════
// v5.0 ARCHITECTURE — GLOBAL DEPTH CURVES:
// ═══════════════════════════════════════════════════════════════
//
//   CORE CHANGE:
//     Replaced Beer-Lambert (exp(-depth * K)) with a single
//     AnimationCurve (globalLightCurve) that maps depth → lightFactor.
//
//     Artist draws ONE curve in Inspector:
//       X axis = depth in meters (0..5000)
//       Y axis = light factor (1.0 = full sun, 0.0 = total darkness)
//
//     Example curve for "bright shallows, dark abyss":
//       (0m, 1.0) → (300m, 0.8) → (700m, 0.1) → (1000m, 0.0)
//
//     Fog density is DERIVED from lightFactor automatically:
//       fogDensity = lerp(minFogDensity, maxFogDensity, 1 - lightFactor)
//       × biome.turbidityMultiplier [0.5..2.0]
//
//     ONE curve controls EVERYTHING depth-dependent:
//       • Sun intensity
//       • Sun flare
//       • Sun visual disc on/off
//       • Sun scattering (size/softness)
//       • Sun disc/scatter color fade
//       • Fog density
//       • All synced — impossible to desync
//
//   BIOMES NOW CONTROL ONLY:
//     • Colors (fog, scatter, Crest)
//     • Turbidity multiplier (gentle fog density nudge)
//     • Biomes CANNOT break global light stratification
//
//   REMOVED:
//     ✗ _globalExtinctionK
//     ✗ maxSunlightDepth (replaced by curve shape)
//     ✗ extinctionMultiplier from biomes
//     ✗ visibilityDistance from biomes
//     ✗ ComputedFogDensity from biomes
//     ✗ Beer-Lambert formula
//     ✗ depthMultiplier formula in ApplyUnderwaterFog
//
// ═══════════════════════════════════════════════════════════════
// PRESERVED FROM v4.x:
// ═══════════════════════════════════════════════════════════════
//
//   ✓ Live-editing (SlowTick reads profile every tick)
//   ✓ Sun disc/scatter color fade (pow2 from lightFactor)
//   ✓ Camera background = fog color
//   ✓ Editor Scene View support ([ExecuteAlways])
//   ✓ Ambient clamp (MIN_AMBIENT)
//   ✓ Race condition fix (sole authority for sunLight.intensity)
//   ✓ Biome fallback (hardcoded defaults if no palette)
//   ✓ Zero GC in Tick (AnimationCurve.Evaluate is native, no alloc)
//
// ═══════════════════════════════════════════════════════════════
// КООРДИНАЦИЯ ЗАПИСИ sunLight.intensity:
//   AtmosphereManager → ProfileSunIntensity (data only, no write)
//   UnderwaterVisuals → sunLight.intensity = profile × horizon × lightCurve
//   CelestialEngine   → sunLight.intensity *= occlusion
// ============================================================================

using Hecton8.Core;
using Hecton8.Atmosphere;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Environment
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4000)]
    [ExecuteAlways]
    public sealed class HectonUnderwaterVisuals : MonoBehaviour, ITickable, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REFERENCES
        // ══════════════════════════════════════════════════════════

        [Header("═══ REFERENCES ═══")]
        [Tooltip("Camera Transform игрока. Определяет глубину.\n" +
                 "Если не назначена — используется Camera.main.\n" +
                 "В редакторе без Play Mode — SceneView камера.")]
        [SerializeField] private Transform playerCamera;

        [Tooltip("Directional Light (солнце).\n" +
                 "Intensity = ProfileSunIntensity × horizonFade × lightCurve(depth).")]
        [SerializeField] private Light sunLight;

        [Tooltip("SRP Lens Flare на солнечном свете.")]
        [SerializeField] private LensFlareComponentSRP sunFlare;

        [Tooltip("Transform визуального диска солнца.\n" +
                 "SetActive(false) когда lightFactor ≤ threshold.")]
        [SerializeField] private Transform sunVisualTransform;

        [Tooltip("Основная камера. clearFlags переключается под водой.")]
        [SerializeField] private Camera mainCamera;

        [Header("═══ ATMOSPHERE MANAGER ═══")]
        [Tooltip("Ссылка на HectonAtmosphereManager.\n" +
                 "Если не назначена — ищется через Instance.\n" +
                 "Используется для чтения ProfileSunIntensity и SunElevation.")]
        [SerializeField] private HectonAtmosphereManager atmosphereManager;

        [Header("═══ CREST MATERIAL ═══")]
        [Tooltip("Материал подводной части Crest Ocean.")]
        [SerializeField] private Material oceanUnderwaterMaterial;

        [Header("═══ SKY MATERIAL ═══")]
        [Tooltip("Материал шейдера неба (Hecton_AlienSky_Master).\n" +
                 "Для управления _SunSize, _SunEdgeSoftness,\n" +
                 "_SunDiscColor и _SunScatterColor при погружении.")]
        [SerializeField] private Material skyMaterial;

        [Header("═══ BIOME PALETTE ═══")]
        [Tooltip("Палитра биомов (HectonOceanPalette).")]
        [SerializeField] private HectonOceanPalette biomePalette;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — GLOBAL DEPTH CURVE (v5.0 CORE)
        // ══════════════════════════════════════════════════════════

        [Header("═══ GLOBAL LIGHT CURVE ═══")]
        [Tooltip("ГЛАВНАЯ КРИВАЯ ЗАТЕМНЕНИЯ.\n\n" +
                 "Ось X = глубина в метрах (0 ... 5000).\n" +
                 "Ось Y = множитель света (1.0 = полный день, 0.0 = мрак).\n\n" +
                 "Эта одна кривая управляет ВСЕМ:\n" +
                 "  • Яркость солнца\n" +
                 "  • Lens Flare\n" +
                 "  • Видимость диска солнца\n" +
                 "  • Рассеивание (размер/мягкость)\n" +
                 "  • Цвета солнца в шейдере неба\n" +
                 "  • Плотность тумана (через min/maxFogDensity)\n\n" +
                 "Пример: (0, 1.0) → (300, 0.8) → (700, 0.1) → (1000, 0.0)\n" +
                 "= светло до 300м, сумерки 300-700м, мрак после 700м.")]
        [SerializeField] private AnimationCurve globalLightCurve = new AnimationCurve(
            new Keyframe(0f,    1.0f,  0f, 0f),
            new Keyframe(300f,  0.8f,  0f, 0f),
            new Keyframe(700f,  0.1f,  0f, 0f),
            new Keyframe(1000f, 0.0f,  0f, 0f)
        );

        [Header("═══ FOG DENSITY RANGE ═══")]
        [Tooltip("Минимальная плотность тумана (на поверхности, lightFactor=1.0).\n" +
                 "Чем меньше — тем прозрачнее вода у поверхности.\n" +
                 "0.002 ≈ видимость ~500м.")]
        [Range(0.0001f, 0.05f)]
        [SerializeField] private float minFogDensity = 0.002f;

        [Tooltip("Максимальная плотность тумана (в полной тьме, lightFactor=0.0).\n" +
                 "Чем больше — тем гуще мрак на максимальной глубине.\n" +
                 "0.08 ≈ видимость ~12м.")]
        [Range(0.01f, 0.5f)]
        [SerializeField] private float maxFogDensity = 0.08f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CONFIGURATION
        // ══════════════════════════════════════════════════════════

        [Header("═══ WATER LEVEL ═══")]
        [Tooltip("Fallback уровень воды если физический\n" +
                 "HectonFluidEngine.Instance недоступен.")]
        [SerializeField] private float waterLevelFallback = 4900f;

        [Header("═══ SUN VISUAL ═══")]
        [Tooltip("Порог lightFactor для деактивации солнечного диска.\n" +
                 "Гистерезис: ON при threshold × 2.")]
        [Range(0.0001f, 0.05f)]
        [SerializeField] private float sunVisualDisableThreshold = 0.005f;

        [Header("═══ SUN SCATTERING ═══")]
        [Tooltip("Базовый размер солнечного диска в шейдере неба.\n" +
                 "Значение над водой. Маленький = точка.")]
        [SerializeField] private float baseSunSize = 0.002f;

        [Tooltip("Максимальный размер солнца под водой.\n" +
                 "Когда lightFactor < 1 — солнце растёт.\n" +
                 "0.15 = большое рассеянное пятно.")]
        [SerializeField] private float underwaterSunSizeMax = 0.15f;

        [Tooltip("Базовая мягкость края солнца (над водой).")]
        [SerializeField] private float baseSunEdgeSoftness = 0.001f;

        [Tooltip("Максимальная мягкость края солнца под водой.\n" +
                 "0.5 = очень размытое свечение.")]
        [SerializeField] private float underwaterSunSoftnessMax = 0.5f;

        [Header("═══ TRANSITION ═══")]
        [Range(0.05f, 2.0f)]
        [SerializeField] private float biomeTransitionSpeed = 0.2f;

        [SerializeField] private float slowTickInterval = 0.5f;

        [Header("═══ SURFACE DEFAULTS ═══")]
        [Tooltip("Цвет тумана над водой.")]
        [ColorUsage(false)]
        [SerializeField] private Color surfaceFogColor = new Color(0.7f, 0.75f, 0.8f, 1f);

        [SerializeField] private float surfaceFogDensity = 0.001f;

        [Tooltip("Включать fog над водой. false = чистое небо.")]
        [SerializeField] private bool enableSurfaceFog = false;

        [Tooltip("Ambient цвет для поверхности.")]
        [ColorUsage(false)]
        [SerializeField] private Color surfaceAmbientColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("═══ UNDERWATER AMBIENT ═══")]
        [Tooltip("Ambient цвет под водой (RenderSettings.ambientLight).\n" +
                 "Минимум (0.01, 0.02, 0.03) применяется автоматически.")]
        [ColorUsage(false)]
        [SerializeField] private Color underwaterAmbientColor = new Color(0.02f, 0.04f, 0.06f, 1f);

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [Header("═══ DIAGNOSTICS ═══")]
#pragma warning disable CS0414
        [SerializeField] private float _debugDepth;
        [SerializeField] private float _debugLightFactor;
        [SerializeField] private float _debugFogDensity;
        [SerializeField] private float _debugTurbidity;
        [SerializeField] private float _debugAtmoSunIntensity;
        [SerializeField] private float _debugFinalSunIntensity;
        [SerializeField] private int   _debugTargetBiome;
        [SerializeField] private float _debugTransitionProgress;
        [SerializeField] private bool  _debugIsUnderwater;
        [SerializeField] private bool  _debugPhysicsEngineFound;
        [SerializeField] private bool  _debugAtmoManagerFound;
        [SerializeField] private bool  _debugSunVisualActive;
        [SerializeField] private float _debugSunScatter;
        [SerializeField] private bool  _debugEditorDriven;
#pragma warning restore CS0414

        // ══════════════════════════════════════════════════════════
        //  SHADER PROPERTY IDs
        // ══════════════════════════════════════════════════════════

        private static readonly int _ID_ScatterColourBase =
            Shader.PropertyToID("_ScatterColourBase");

        private static readonly int _ID_ScatterColourShallow =
            Shader.PropertyToID("_ScatterColourShallow");

        private static readonly int _ID_DepthFogDensity =
            Shader.PropertyToID("_DepthFogDensity");

        private static readonly int _ID_SunSize =
            Shader.PropertyToID("_SunSize");

        private static readonly int _ID_SunEdgeSoftness =
            Shader.PropertyToID("_SunEdgeSoftness");

        private static readonly int _ID_SunDiscColor =
            Shader.PropertyToID("_SunDiscColor");

        private static readonly int _ID_SunScatterColor =
            Shader.PropertyToID("_SunScatterColor");

        // ══════════════════════════════════════════════════════════
        //  CONSTANTS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Minimum ambient color components. Terrain is ALWAYS visible.
        /// Even at 5000m depth, some bioluminescent glow illuminates geometry.
        /// </summary>
        private static readonly Color MIN_AMBIENT = new Color(0.01f, 0.02f, 0.03f, 1f);

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Hecton8.Physics.HectonFluidEngine _physicsEngine;
        private bool _physicsEngineCached;

        private HectonAtmosphereManager _cachedAtmoManager;
        private bool _atmoManagerCached;

        private int _targetBiomeIndex;

        // Current interpolated biome parameters (COLORS + TURBIDITY ONLY)
        private Color   _currentScatterBase;
        private Color   _currentScatterShallow;
        private Vector3 _currentDepthFogDensity;
        private Color   _currentFogColor;
        private float   _currentTurbidity;
        private Color   _currentAmbientColor;

        // Target biome parameters
        private Color   _targetScatterBase;
        private Color   _targetScatterShallow;
        private Vector3 _targetDepthFogDensity;
        private Color   _targetFogColor;
        private float   _targetTurbidity;
        private Color   _targetAmbientColor;

        private float _transitionProgress;

        // Base values (for surface restore)
        private float _baseFlareIntensity;
        private bool  _baseValuesCaptured;

        // Base sky sun colors
        private Color _baseSunDiscColor;
        private Color _baseSunScatterColor;
        private bool  _baseSkyColorsCaptured;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _wasUnderwater;
        private bool _sunVisualWasDisabled;

        private bool _biomeFallbackActive;

        // Editor mode timing
        private float _editorSlowTickAccum;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            ResolvePlayerCamera();
            ResolveMainCamera();
            ValidateReferences();
            CachePhysicsEngine();
            CacheAtmosphereManager();
            CaptureBaseValues();
            CaptureSkyBaseColors();
            InitializeCurrentValues();

            if (Application.isPlaying)
            {
                MapMagicBridge.OnBiomeChanged += HandleBiomeChanged;
                TryRegisterTickManagers();
            }
#if UNITY_EDITOR
            else
            {
                EditorApplication.update -= EditorUpdate;
                EditorApplication.update += EditorUpdate;
            }
#endif

            _wasUnderwater = false;
            _sunVisualWasDisabled = false;
        }

        private void Start()
        {
            if (!Application.isPlaying) return;

            if (!_registeredTick || !_registeredSlowTick)
                TryRegisterTickManagers();

            if (!_physicsEngineCached)
                CachePhysicsEngine();

            if (!_atmoManagerCached)
                CacheAtmosphereManager();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                MapMagicBridge.OnBiomeChanged -= HandleBiomeChanged;

                GameTickManager tickManager = GameTickManager.Instance;
                if (tickManager != null)
                {
                    if (_registeredTick)
                    {
                        tickManager.Unregister((ITickable)this);
                        _registeredTick = false;
                    }
                    if (_registeredSlowTick)
                    {
                        tickManager.Unregister((ISlowTickable)this);
                        _registeredSlowTick = false;
                    }
                }
            }
#if UNITY_EDITOR
            else
            {
                EditorApplication.update -= EditorUpdate;
            }
#endif

            RestoreBaseValues();
            RestoreSunVisual();
            RestoreCameraDefaults();
            RestoreSkyMaterialDefaults();
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR UPDATE (scene camera support in edit mode)
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (Application.isPlaying) return;
            if (this == null) return;

            ResolveEditorCamera();

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) dt = 0.016f;

            Tick(dt);

            _editorSlowTickAccum += dt;
            if (_editorSlowTickAccum >= slowTickInterval)
            {
                _editorSlowTickAccum -= slowTickInterval;
                SlowTick();
            }

            _debugEditorDriven = true;
        }

        private void ResolveEditorCamera()
        {
            if (Application.isPlaying) return;

            var sv = SceneView.lastActiveSceneView;
            if (sv != null && sv.camera != null)
            {
                playerCamera = sv.camera.transform;
                if (mainCamera == null)
                    mainCamera = sv.camera;
            }
        }
#endif

        // ══════════════════════════════════════════════════════════
        //  ITickable.Tick — PER-FRAME
        //
        //  v5.0: All depth logic driven by globalLightCurve.
        //  ONE curve → lightFactor → everything else derived.
        //  AnimationCurve.Evaluate is native call, zero GC.
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            if (playerCamera == null)
            {
                ResolvePlayerCamera();
                if (playerCamera == null) return;
            }

            // ══ 1. WATER LEVEL ══
            float waterLevel = ResolveWaterLevel();

            // ══ 2. DEPTH ══
            float cameraY = playerCamera.position.y;
            float depth = math.max(0f, waterLevel - cameraY);
            bool isUnderwater = cameraY < waterLevel;

            UpdateDepthDiagnostics(depth, isUnderwater);

            // ══ 3. ABOVE WATER — instant restore ══
            if (!isUnderwater)
            {
                if (_wasUnderwater)
                {
                    ApplySurfaceDefaults();
                    RestoreSkyMaterialDefaults();
                    _wasUnderwater = false;
                }
                return;
            }

            // ══ 4. ENTERING WATER — one-time setup ══
            if (!_wasUnderwater)
            {
                RenderSettings.fog     = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                _wasUnderwater = true;
            }

            // ══ 5. GLOBAL LIGHT CURVE (v5.0 CORE) ══
            // One curve rules everything. No Beer-Lambert.
            // Clamp to [0, 1] — curve editor might overshoot.
            float lightFactor = math.saturate(globalLightCurve.Evaluate(depth));

            // ══ 6. SUN INTENSITY ══
            float baseSunIntensity = ResolveProfileSunIntensity();
            float horizonFade = ResolveHorizonFade();
            float finalSunIntensity = baseSunIntensity * horizonFade * lightFactor;

            ApplySunIntensity(finalSunIntensity, lightFactor);
            ApplySunVisualState(lightFactor);

            // ══ 7. SUN SCATTERING (driven by lightFactor, not depth) ══
            ApplySunScattering(lightFactor);

            // ══ 8. SUN DISC/SCATTER COLOR FADE ══
            ApplySunColorFade(lightFactor);

            // ══ 9. FOG (derived from lightFactor + turbidity) ══
            ApplyUnderwaterFog(lightFactor);

            // ══ 10. AMBIENT LIGHT (per-frame, clamped) ══
            ApplyUnderwaterAmbient();

            // ══ 11. CAMERA (fog color) ══
            ApplyUnderwaterCamera();

            UpdateLightDiagnostics(lightFactor, baseSunIntensity * horizonFade, finalSunIntensity);
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable.SlowTick — 2Hz
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (playerCamera == null) return;

            // Live-editing — refresh targets from current profile
            RefreshTargetsFromCurrentProfile();

            // Biome interpolation (smooth color + turbidity transitions)
            float lerpT = math.saturate(biomeTransitionSpeed * slowTickInterval);
            InterpolateBiomeParameters(lerpT);

            // Crest material
            ApplyCrestMaterial();
        }

        // ══════════════════════════════════════════════════════════
        //  LIVE-EDITING — REFRESH TARGETS
        // ══════════════════════════════════════════════════════════

        private void RefreshTargetsFromCurrentProfile()
        {
            if (biomePalette == null) return;

            HectonBiomeProfile currentProf = biomePalette.GetProfile(_targetBiomeIndex);
            if (currentProf == null)
            {
                currentProf = biomePalette.GetProfile(0);
                if (currentProf == null) return;
            }

            // Update targets WITHOUT resetting _transitionProgress
            _targetScatterBase     = currentProf.scatterColorBase;
            _targetScatterShallow  = currentProf.scatterColorShallow;
            _targetDepthFogDensity = currentProf.depthFogDensity;
            _targetFogColor        = currentProf.fogColor;
            _targetTurbidity       = currentProf.turbidityMultiplier;
            _targetAmbientColor    = underwaterAmbientColor;
        }

        // ══════════════════════════════════════════════════════════
        //  WATER LEVEL
        // ══════════════════════════════════════════════════════════

        private float ResolveWaterLevel()
        {
            if (!_physicsEngineCached)
                CachePhysicsEngine();

            if (_physicsEngine != null)
                return _physicsEngine.WaterLevel;

            return waterLevelFallback;
        }

        private void CachePhysicsEngine()
        {
            if (!Application.isPlaying)
            {
                _physicsEngineCached = false;
#if UNITY_EDITOR
                _debugPhysicsEngineFound = false;
#endif
                return;
            }

            _physicsEngine = Hecton8.Physics.HectonFluidEngine.Instance;
            _physicsEngineCached = _physicsEngine != null;
#if UNITY_EDITOR
            _debugPhysicsEngineFound = _physicsEngineCached;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  ATMOSPHERE MANAGER INTEGRATION
        // ══════════════════════════════════════════════════════════

        private float ResolveProfileSunIntensity()
        {
            if (!_atmoManagerCached)
                CacheAtmosphereManager();

            if (_cachedAtmoManager != null)
                return _cachedAtmoManager.ProfileSunIntensity;

            return 1f;
        }

        private float ResolveHorizonFade()
        {
            if (_cachedAtmoManager == null)
                return 1f;

            float elevation = _cachedAtmoManager.SunElevation;

            const float fadeAngle = 10f;
            float fadeThreshold = math.sin(math.radians(fadeAngle));

            if (elevation <= 0f) return 0f;
            if (elevation >= fadeThreshold) return 1f;

            float st = elevation / fadeThreshold;
            return st * st * (3f - 2f * st);
        }

        private void CacheAtmosphereManager()
        {
            _cachedAtmoManager = atmosphereManager;

            if (_cachedAtmoManager == null)
            {
                if (Application.isPlaying)
                    _cachedAtmoManager = HectonAtmosphereManager.Instance;
#if UNITY_EDITOR
                else
                    _cachedAtmoManager = FindFirstObjectByType<HectonAtmosphereManager>();
#endif
            }

            _atmoManagerCached = _cachedAtmoManager != null;

#if UNITY_EDITOR
            _debugAtmoManagerFound = _atmoManagerCached;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  SUN INTENSITY — in Tick()
        // ══════════════════════════════════════════════════════════

        private void ApplySunIntensity(float finalIntensity, float lightFactor)
        {
            if (sunLight != null)
                sunLight.intensity = finalIntensity;

            if (sunFlare != null)
            {
                sunFlare.intensity = _baseFlareIntensity * lightFactor;

                bool shouldEnable = lightFactor > sunVisualDisableThreshold;
                if (sunFlare.enabled != shouldEnable)
                    sunFlare.enabled = shouldEnable;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SUN VISUAL DISC — in Tick()
        // ══════════════════════════════════════════════════════════

        private void ApplySunVisualState(float lightFactor)
        {
            if (sunVisualTransform == null) return;

            float disableAt = sunVisualDisableThreshold;
            float enableAt  = sunVisualDisableThreshold * 2f;

            if (!_sunVisualWasDisabled)
            {
                if (lightFactor < disableAt)
                {
                    sunVisualTransform.gameObject.SetActive(false);
                    _sunVisualWasDisabled = true;
                }
            }
            else
            {
                if (lightFactor > enableAt)
                {
                    sunVisualTransform.gameObject.SetActive(true);
                    _sunVisualWasDisabled = false;
                }
            }

#if UNITY_EDITOR
            _debugSunVisualActive = !_sunVisualWasDisabled;
#endif
        }

        private void RestoreSunVisual()
        {
            if (sunVisualTransform != null && _sunVisualWasDisabled)
            {
                sunVisualTransform.gameObject.SetActive(true);
                _sunVisualWasDisabled = false;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SUN SCATTERING — in Tick()
        //  v5.0: driven by lightFactor (1-lightFactor = scatter amount)
        //  instead of raw depth. Synchronized with the curve.
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// v5.0: Sun scattering is now driven by lightFactor from the global curve.
        /// scatterT = 1 - lightFactor:
        ///   lightFactor=1.0 → scatterT=0 → small sharp sun (surface)
        ///   lightFactor=0.5 → scatterT=0.5 → medium scattered glow
        ///   lightFactor=0.0 → scatterT=1.0 → max scatter (but sun is off anyway)
        ///
        /// This keeps scattering perfectly synced with the light curve.
        /// No separate scatterDepthMax parameter needed.
        /// </summary>
        private void ApplySunScattering(float lightFactor)
        {
            if (skyMaterial == null) return;

            // Invert: less light = more scatter
            float scatterT = math.saturate(1f - lightFactor);

            float sunSize = Mathf.Lerp(baseSunSize, underwaterSunSizeMax, scatterT);
            float sunSoftness = Mathf.Lerp(baseSunEdgeSoftness, underwaterSunSoftnessMax, scatterT);

            skyMaterial.SetFloat(_ID_SunSize, sunSize);
            skyMaterial.SetFloat(_ID_SunEdgeSoftness, sunSoftness);

#if UNITY_EDITOR
            _debugSunScatter = scatterT;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  SUN DISC / SCATTER COLOR FADE
        // ══════════════════════════════════════════════════════════

        private void CaptureSkyBaseColors()
        {
            if (_baseSkyColorsCaptured) return;
            if (skyMaterial == null) return;

            if (skyMaterial.HasColor(_ID_SunDiscColor))
                _baseSunDiscColor = skyMaterial.GetColor(_ID_SunDiscColor);
            else
                _baseSunDiscColor = Color.white;

            if (skyMaterial.HasColor(_ID_SunScatterColor))
                _baseSunScatterColor = skyMaterial.GetColor(_ID_SunScatterColor);
            else
                _baseSunScatterColor = Color.white;

            _baseSkyColorsCaptured = true;
        }

        /// <summary>
        /// Fades sun disc and scatter colors toward black.
        /// Uses pow(lightFactor, 2) for quadratic falloff — sun colors
        /// disappear faster than ambient, which looks natural.
        /// </summary>
        private void ApplySunColorFade(float lightFactor)
        {
            if (skyMaterial == null) return;
            if (!_baseSkyColorsCaptured) return;

            float colorFactor = lightFactor * lightFactor;

            Color fadedDisc;
            fadedDisc.r = _baseSunDiscColor.r * colorFactor;
            fadedDisc.g = _baseSunDiscColor.g * colorFactor;
            fadedDisc.b = _baseSunDiscColor.b * colorFactor;
            fadedDisc.a = _baseSunDiscColor.a;

            Color fadedScatter;
            fadedScatter.r = _baseSunScatterColor.r * colorFactor;
            fadedScatter.g = _baseSunScatterColor.g * colorFactor;
            fadedScatter.b = _baseSunScatterColor.b * colorFactor;
            fadedScatter.a = _baseSunScatterColor.a;

            skyMaterial.SetColor(_ID_SunDiscColor, fadedDisc);
            skyMaterial.SetColor(_ID_SunScatterColor, fadedScatter);
        }

        private void RestoreSkyMaterialDefaults()
        {
            if (skyMaterial == null) return;

            skyMaterial.SetFloat(_ID_SunSize, baseSunSize);
            skyMaterial.SetFloat(_ID_SunEdgeSoftness, baseSunEdgeSoftness);

            if (_baseSkyColorsCaptured)
            {
                skyMaterial.SetColor(_ID_SunDiscColor, _baseSunDiscColor);
                skyMaterial.SetColor(_ID_SunScatterColor, _baseSunScatterColor);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  BIOME INTERPOLATION — in SlowTick()
        //  v5.0: interpolates colors + turbidity. No extinction.
        // ══════════════════════════════════════════════════════════

        private void InterpolateBiomeParameters(float lerpT)
        {
            _currentScatterBase = Color.Lerp(
                _currentScatterBase, _targetScatterBase, lerpT);

            _currentScatterShallow = Color.Lerp(
                _currentScatterShallow, _targetScatterShallow, lerpT);

            _currentDepthFogDensity = Vector3.Lerp(
                _currentDepthFogDensity, _targetDepthFogDensity, lerpT);

            _currentFogColor = Color.Lerp(
                _currentFogColor, _targetFogColor, lerpT);

            _currentTurbidity = Mathf.Lerp(
                _currentTurbidity, _targetTurbidity, lerpT);

            _currentAmbientColor = Color.Lerp(
                _currentAmbientColor, _targetAmbientColor, lerpT);

            float dist = ColorDistanceManhattan(
                _currentScatterBase, _targetScatterBase);
            _transitionProgress = 1f - math.saturate(dist * 10f);

#if UNITY_EDITOR
            _debugTransitionProgress = _transitionProgress;
            _debugTurbidity          = _currentTurbidity;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  CREST MATERIAL — in SlowTick()
        // ══════════════════════════════════════════════════════════

        private void ApplyCrestMaterial()
        {
            if (oceanUnderwaterMaterial == null) return;

            oceanUnderwaterMaterial.SetColor(
                _ID_ScatterColourBase, _currentScatterBase);

            oceanUnderwaterMaterial.SetColor(
                _ID_ScatterColourShallow, _currentScatterShallow);

            oceanUnderwaterMaterial.SetVector(
                _ID_DepthFogDensity,
                new Vector4(
                    _currentDepthFogDensity.x,
                    _currentDepthFogDensity.y,
                    _currentDepthFogDensity.z,
                    0f));
        }

        // ══════════════════════════════════════════════════════════
        //  URP FOG — in Tick()
        //  v5.0: fog density derived from lightFactor + turbidity
        //  ONE formula, perfectly synced with light curve.
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// v5.0: Fog density is derived from lightFactor.
        ///
        /// fogDensity = lerp(minFogDensity, maxFogDensity, 1 - lightFactor)
        ///            × biome.turbidityMultiplier
        ///
        /// lightFactor=1.0 → minFogDensity (clear surface water)
        /// lightFactor=0.0 → maxFogDensity (impenetrable deep)
        /// turbidity=1.0   → no change
        /// turbidity=2.0   → 2x murkier (dirty biome)
        /// turbidity=0.5   → half as murky (crystal clear biome)
        ///
        /// Artist controls:
        ///   • Shape of transition: globalLightCurve
        ///   • Min/max fog range: minFogDensity, maxFogDensity
        ///   • Per-biome murkiness: turbidityMultiplier [0.5..2.0]
        /// </summary>
        private void ApplyUnderwaterFog(float lightFactor)
        {
            RenderSettings.fogColor = _currentFogColor;

            // Remap lightFactor to fog density range
            // lightFactor=1 → min fog, lightFactor=0 → max fog
            float baseDensity = Mathf.Lerp(maxFogDensity, minFogDensity, lightFactor);

            // Apply biome turbidity
            float finalDensity = baseDensity * _currentTurbidity;

            RenderSettings.fogDensity = finalDensity;

#if UNITY_EDITOR
            _debugFogDensity = finalDensity;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  AMBIENT LIGHT — in Tick() + CLAMP
        // ══════════════════════════════════════════════════════════

        private void ApplyUnderwaterAmbient()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;

            Color ambient;
            ambient.r = math.max(_currentAmbientColor.r, MIN_AMBIENT.r);
            ambient.g = math.max(_currentAmbientColor.g, MIN_AMBIENT.g);
            ambient.b = math.max(_currentAmbientColor.b, MIN_AMBIENT.b);
            ambient.a = 1f;

            RenderSettings.ambientLight = ambient;
        }

        // ══════════════════════════════════════════════════════════
        //  CAMERA — in Tick()
        //  Camera background = fog color for seamless dissolve
        // ══════════════════════════════════════════════════════════

        private void ApplyUnderwaterCamera()
        {
            if (mainCamera == null) return;

            mainCamera.clearFlags      = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = _currentFogColor;
        }

        // ══════════════════════════════════════════════════════════
        //  SURFACE DEFAULTS (instant reset) — in Tick()
        // ══════════════════════════════════════════════════════════

        private void ApplySurfaceDefaults()
        {
            // ── SUN INTENSITY (CRITICAL FIX) ──
            // UnderwaterVisuals — ЕДИНОЛИЧНЫЙ хозяин света.
            // Мы обязаны сами вернуть яркость солнца на поверхности.
            if (sunLight != null)
            {
                float baseSun = ResolveProfileSunIntensity();
                float horizon = ResolveHorizonFade();
                sunLight.intensity = baseSun * horizon;
            }

            // ── Flare restore ──
            if (_baseValuesCaptured && sunFlare != null)
            {
                sunFlare.intensity = _baseFlareIntensity;
                if (!sunFlare.enabled)
                    sunFlare.enabled = true;
            }

            // ── Sun visual ──
            RestoreSunVisual();

            // ── Fog (SOLE AUTHORITY) ──
            if (enableSurfaceFog)
            {
                RenderSettings.fog        = true;
                RenderSettings.fogMode    = FogMode.ExponentialSquared;
                RenderSettings.fogColor   = surfaceFogColor;
                RenderSettings.fogDensity = surfaceFogDensity;
            }
            else
            {
                RenderSettings.fog = false;
            }

            // ── Ambient (SOLE AUTHORITY) ──
            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = surfaceAmbientColor;

            // ── Camera (SOLE AUTHORITY) ──
            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.Skybox;
            }

            // ── Crest surface colors ──
            if (biomePalette != null)
            {
                HectonBiomeProfile surfProfile = biomePalette.SurfaceProfile;
                if (surfProfile != null)
                {
                    _currentScatterBase     = surfProfile.scatterColorBase;
                    _currentScatterShallow  = surfProfile.scatterColorShallow;
                    _currentDepthFogDensity = surfProfile.depthFogDensity;
                    ApplyCrestMaterial();
                }
            }
        }

        private void RestoreCameraDefaults()
        {
            if (mainCamera != null)
                mainCamera.clearFlags = CameraClearFlags.Skybox;
        }

        // ══════════════════════════════════════════════════════════
        //  BIOME EVENT
        // ══════════════════════════════════════════════════════════

        private void HandleBiomeChanged(int biomeIndex)
        {
            if (biomePalette == null) return;

            _targetBiomeIndex = biomeIndex;
            _biomeFallbackActive = false;

            HectonBiomeProfile profile = biomePalette.GetProfile(biomeIndex);
            if (profile == null)
            {
                profile = biomePalette.GetProfile(0);
                _targetBiomeIndex = 0;
            }

            if (profile == null) return;

            SetTargetFromProfile(profile);

#if UNITY_EDITOR
            _debugTargetBiome = _targetBiomeIndex;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Current depth in meters below water surface.
        /// </summary>
        public float CurrentDepth
        {
            get
            {
                if (playerCamera == null) return 0f;
                return math.max(0f, ResolveWaterLevel() - playerCamera.position.y);
            }
        }

        /// <summary>
        /// v5.0: Light factor from global curve at current depth.
        /// 1.0 = full daylight, 0.0 = total darkness.
        /// </summary>
        public float CurrentLightFactor
        {
            get
            {
                float d = CurrentDepth;
                if (d <= 0f) return 1f;
                return math.saturate(globalLightCurve.Evaluate(d));
            }
        }

        public bool IsUnderwater
        {
            get
            {
                if (playerCamera == null) return false;
                return playerCamera.position.y < ResolveWaterLevel();
            }
        }

        public float CurrentTurbidity => _currentTurbidity;
        public int TargetBiomeIndex => _targetBiomeIndex;
        public float TransitionProgress => _transitionProgress;

        public void SetTargetBiome(int biomeIndex)
            => HandleBiomeChanged(biomeIndex);

        public void SetPlayerCamera(Transform camera)
            => playerCamera = camera;

        public void SetWaterLevelFallback(float y)
            => waterLevelFallback = y;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — INIT
        // ══════════════════════════════════════════════════════════

        private void ResolvePlayerCamera()
        {
            if (playerCamera != null) return;

            if (Application.isPlaying)
            {
                Camera cam = Camera.main;
                if (cam != null) playerCamera = cam.transform;
            }
#if UNITY_EDITOR
            else
            {
                ResolveEditorCamera();
            }
#endif
        }

        private void ResolveMainCamera()
        {
            if (mainCamera != null) return;

            if (Application.isPlaying)
            {
                mainCamera = Camera.main;
            }
#if UNITY_EDITOR
            else
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null)
                    mainCamera = sv.camera;
            }
#endif
        }

        private void ValidateReferences()
        {
            if (playerCamera == null && Application.isPlaying)
                Debug.LogError("[HectonUnderwaterVisuals] playerCamera not found!", this);
            if (biomePalette == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] biomePalette not assigned. Using hardcoded defaults.", this);
            if (oceanUnderwaterMaterial == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] oceanUnderwaterMaterial not assigned.", this);
            if (sunVisualTransform == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] sunVisualTransform not assigned.", this);
            if (mainCamera == null && Application.isPlaying)
                Debug.LogWarning("[HectonUnderwaterVisuals] mainCamera not assigned.", this);
            if (skyMaterial == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] skyMaterial not assigned. Sun scattering/color fade disabled.", this);
            if (globalLightCurve == null || globalLightCurve.length == 0)
                Debug.LogError("[HectonUnderwaterVisuals] globalLightCurve is empty! Depth lighting will not work.", this);
        }

        private void CaptureBaseValues()
        {
            if (_baseValuesCaptured) return;

            if (sunFlare != null)
                _baseFlareIntensity = sunFlare.intensity;

            _baseValuesCaptured = true;
        }

        private void RestoreBaseValues()
        {
            if (!_baseValuesCaptured) return;

            if (sunFlare != null)
            {
                sunFlare.intensity = _baseFlareIntensity;
                if (!sunFlare.enabled)
                    sunFlare.enabled = true;
            }
        }

        private void InitializeCurrentValues()
        {
            HectonBiomeProfile initial = null;

            if (biomePalette != null)
            {
                initial = biomePalette.SurfaceProfile;
                if (initial == null && biomePalette.Count > 0)
                    initial = biomePalette.GetProfile(0);
            }

            if (initial != null)
            {
                SetCurrentFromProfile(initial);
                SetTargetFromProfile(initial);
                _biomeFallbackActive = false;
            }
            else
            {
                // Hardcoded fallback — ocean works without biome palette
                _currentScatterBase     = new Color(0f, 0.03f, 0.07f, 1f);
                _currentScatterShallow  = new Color(0f, 0.15f, 0.12f, 1f);
                _currentDepthFogDensity = new Vector3(0.5f, 0.25f, 0.15f);
                _currentFogColor        = new Color(0f, 0.05f, 0.1f, 1f);
                _currentTurbidity       = 1.0f;
                _currentAmbientColor    = underwaterAmbientColor;

                _targetScatterBase     = _currentScatterBase;
                _targetScatterShallow  = _currentScatterShallow;
                _targetDepthFogDensity = _currentDepthFogDensity;
                _targetFogColor        = _currentFogColor;
                _targetTurbidity       = 1.0f;
                _targetAmbientColor    = _currentAmbientColor;

                _biomeFallbackActive = true;

                Debug.LogWarning(
                    "[HectonUnderwaterVisuals] No biome palette/profiles found. " +
                    "Using hardcoded ocean defaults. Assign HectonOceanPalette for biome support.");
            }

            _transitionProgress = 1f;
            _targetBiomeIndex = 0;
        }

        private void SetCurrentFromProfile(HectonBiomeProfile p)
        {
            _currentScatterBase     = p.scatterColorBase;
            _currentScatterShallow  = p.scatterColorShallow;
            _currentDepthFogDensity = p.depthFogDensity;
            _currentFogColor        = p.fogColor;
            _currentTurbidity       = p.turbidityMultiplier;
            _currentAmbientColor    = underwaterAmbientColor;
        }

        private void SetTargetFromProfile(HectonBiomeProfile p)
        {
            _targetScatterBase     = p.scatterColorBase;
            _targetScatterShallow  = p.scatterColorShallow;
            _targetDepthFogDensity = p.depthFogDensity;
            _targetFogColor        = p.fogColor;
            _targetTurbidity       = p.turbidityMultiplier;
            _targetAmbientColor    = underwaterAmbientColor;
            _transitionProgress    = 0f;
        }

        private void TryRegisterTickManagers()
        {
            if (!Application.isPlaying) return;

            GameTickManager tm = GameTickManager.Instance;
            if (tm == null) return;

            if (!_registeredTick)
            {
                tm.Register((ITickable)this);
                _registeredTick = true;
            }

            if (!_registeredSlowTick)
            {
                tm.Register((ISlowTickable)this);
                _registeredSlowTick = true;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  UTILITY
        // ══════════════════════════════════════════════════════════

        private static float ColorDistanceManhattan(Color a, Color b)
        {
            return math.abs(a.r - b.r) +
                   math.abs(a.g - b.g) +
                   math.abs(a.b - b.b);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDepthDiagnostics(float depth, bool underwater)
        {
            _debugDepth = depth;
            _debugIsUnderwater = underwater;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateLightDiagnostics(
            float lightFactor, float atmoIntensity, float finalIntensity)
        {
            _debugLightFactor = lightFactor;
            _debugAtmoSunIntensity = atmoIntensity;
            _debugFinalSunIntensity = finalIntensity;
        }

        // ══════════════════════════════════════════════════════════
        //  GIZMOS
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform cam = playerCamera;
            if (cam == null)
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null && sv.camera != null)
                    cam = sv.camera.transform;
            }
            if (cam == null) return;

            float waterLevel = waterLevelFallback;
            Vector3 camPos = cam.position;
            float depth = Mathf.Max(0f, waterLevel - camPos.y);

            // Water surface plane
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.12f);
            Gizmos.DrawCube(
                new Vector3(camPos.x, waterLevel, camPos.z),
                new Vector3(80f, 0.05f, 80f));

            if (depth > 0f)
            {
                // Depth line colored by light factor
                float lf = globalLightCurve != null
                    ? Mathf.Clamp01(globalLightCurve.Evaluate(depth))
                    : 1f;

                Gizmos.color = Color.Lerp(Color.black, Color.cyan, lf);
                Gizmos.DrawLine(
                    new Vector3(camPos.x, waterLevel, camPos.z), camPos);

                // Find darkness depth from curve (where Y ≈ 0)
                float darknessDepth = FindCurveDarknessDepth();
                if (darknessDepth > 0f)
                {
                    float darknessY = waterLevel - darknessDepth;
                    Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
                    Gizmos.DrawCube(
                        new Vector3(camPos.x, darknessY, camPos.z),
                        new Vector3(40f, 0.05f, 40f));
                }

                // Light factor sphere
                Gizmos.color = Color.Lerp(Color.black, new Color(1f, 0.95f, 0.8f), lf);
                Gizmos.DrawWireSphere(camPos, 2.5f);

                float scatter = 1f - lf;
                Handles.Label(
                    camPos + Vector3.up * 3f,
                    $"Depth: {depth:F0}m  Light: {lf:P0}  Scatter: {scatter:P0}  Turbidity: {_currentTurbidity:F2}");
            }
            else
            {
                Handles.Label(
                    camPos + Vector3.up * 3f,
                    "Above water");
            }
        }

        /// <summary>
        /// Scans the light curve to find the depth where light ≈ 0.
        /// Used only for Gizmo visualization.
        /// </summary>
        private float FindCurveDarknessDepth()
        {
            if (globalLightCurve == null || globalLightCurve.length < 2)
                return 0f;

            // Get the last keyframe time as max search range
            float maxTime = globalLightCurve[globalLightCurve.length - 1].time;

            // Sample at intervals to find where value drops to ~0
            const float threshold = 0.005f;
            const int samples = 100;
            float step = maxTime / samples;

            for (int i = 1; i <= samples; i++)
            {
                float t = i * step;
                float v = globalLightCurve.Evaluate(t);
                if (v <= threshold)
                    return t;
            }

            return maxTime;
        }
#endif
    }
}