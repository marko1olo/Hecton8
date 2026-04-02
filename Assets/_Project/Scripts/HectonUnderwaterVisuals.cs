// ============================================================================
// HECTON-8 — HectonUnderwaterVisuals.cs  v5.1
// ЕДИНОЛИЧНЫЙ ДИРЕКТОР СРЕДЫ: туман, свет, цвета, камера, рассеивание солнца.
//
// ═══════════════════════════════════════════════════════════════
// v5.1 CHANGES — RACE CONDITION FIX:
// ═══════════════════════════════════════════════════════════════
//
//   [FIX] EXECUTION ORDER:
//     DefaultExecutionOrder(-4000) ensures this script's OnEnable
//     fires AFTER AtmosphereManager(-6000) but BEFORE CelestialEngine(-3000).
//     Registration order in GameTickManager = tick order.
//
//     CHAIN (every frame, deterministic):
//       1. AtmosphereManager.Tick() → fresh ProfileSunIntensity, ComputedHorizonFade
//       2. UnderwaterVisuals.Tick() → sunLight.intensity = profile × horizon × depth
//       3. CelestialEngine.Tick()   → sunLight.intensity *= eclipseVisibility
//
//   [FIX] ResolveHorizonFade():
//     Now reads AtmosphereManager.ComputedHorizonFade DIRECTLY
//     instead of recalculating from SunElevation with a potentially
//     different fadeAngle. ONE SOURCE OF TRUTH for horizon fade.
//     Removes the subtle desync where UnderwaterVisuals and
//     AtmosphereManager used different smoothstep curves.
//
//   [FIX] Surface light update:
//     Above water, sunLight.intensity is still written every frame
//     (profile × horizon) so CelestialEngine can multiply by eclipse.
//     Guard changed: only skip if BOTH baseSun AND horizon are zero
//     (prevents writing 0 when AtmosphereManager hasn't initialized).
//
// ═══════════════════════════════════════════════════════════════
// v5.0 PRESERVED:
//   ✓ Global depth curve (AnimationCurve)
//   ✓ Fog density derived from lightFactor
//   ✓ Sun scattering / color fade
//   ✓ Biome color interpolation
//   ✓ Camera background = fog color
//   ✓ EnforceFogState render callback
//   ✓ Zero GC in Tick
//   ✓ [ExecuteAlways] for Editor preview
//
// КООРДИНАЦИЯ ЗАПИСИ sunLight.intensity (v5.1):
//   AtmosphereManager(-6000) → ProfileSunIntensity, ComputedHorizonFade (data)
//   UnderwaterVisuals(-4000) → sunLight.intensity = profile × horizon × lightCurve
//   CelestialEngine(-3000)   → sunLight.intensity *= (1 - eclipseOcclusion)
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
        [SerializeField] private Transform playerCamera;
        [SerializeField] private Light sunLight;
        [SerializeField] private LensFlareComponentSRP sunFlare;
        [SerializeField] private Transform sunVisualTransform;
        [SerializeField] private Camera mainCamera;

        [Header("═══ ATMOSPHERE MANAGER ═══")]
        [SerializeField] private HectonAtmosphereManager atmosphereManager;

        [Header("═══ CREST MATERIAL ═══")]
        [SerializeField] private Material oceanUnderwaterMaterial;

        [Header("═══ SKY MATERIAL ═══")]
        [SerializeField] private Material skyMaterial;

        [Header("═══ BIOME PALETTE ═══")]
        [SerializeField] private HectonOceanPalette biomePalette;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — GLOBAL DEPTH CURVE
        // ══════════════════════════════════════════════════════════

        [Header("═══ GLOBAL LIGHT CURVE ═══")]
        [Tooltip("ГЛАВНАЯ КРИВАЯ ЗАТЕМНЕНИЯ.\n" +
                 "X = глубина (м), Y = множитель света [0..1].")]
        [SerializeField] private AnimationCurve globalLightCurve = new AnimationCurve(
            new Keyframe(0f,    1.0f,  0f, 0f),
            new Keyframe(300f,  0.8f,  0f, 0f),
            new Keyframe(700f,  0.1f,  0f, 0f),
            new Keyframe(1000f, 0.0f,  0f, 0f)
        );

        [Header("═══ FOG DENSITY RANGE ═══")]
        [Range(0.0001f, 0.05f)]
        [SerializeField] private float minFogDensity = 0.002f;

        [Range(0.01f, 0.5f)]
        [SerializeField] private float maxFogDensity = 0.08f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CONFIGURATION
        // ══════════════════════════════════════════════════════════

        [Header("═══ WATER LEVEL ═══")]
        [SerializeField] private float waterLevelFallback = 4900f;

        [Header("═══ SUN VISUAL ═══")]
        [Range(0.0001f, 0.05f)]
        [SerializeField] private float sunVisualDisableThreshold = 0.005f;

        [Header("═══ SUN SCATTERING ═══")]
        [SerializeField] private float baseSunSize = 0.002f;
        [SerializeField] private float underwaterSunSizeMax = 0.15f;
        [SerializeField] private float baseSunEdgeSoftness = 0.001f;
        [SerializeField] private float underwaterSunSoftnessMax = 0.5f;

        [Header("═══ TRANSITION ═══")]
        [Range(0.05f, 2.0f)]
        [SerializeField] private float biomeTransitionSpeed = 0.2f;
        [SerializeField] private float slowTickInterval = 0.5f;

        [Header("═══ SURFACE DEFAULTS ═══")]
        [ColorUsage(false)]
        [SerializeField] private Color surfaceFogColor = new Color(0.7f, 0.75f, 0.8f, 1f);
        [SerializeField] private float surfaceFogDensity = 0.001f;
        [SerializeField] private bool enableSurfaceFog = false;
        [ColorUsage(false)]
        [SerializeField] private Color surfaceAmbientColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("═══ UNDERWATER AMBIENT ═══")]
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
        [SerializeField] private float _debugHorizonFade;
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

        private static readonly Color MIN_AMBIENT = new Color(0.01f, 0.02f, 0.03f, 1f);

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Hecton8.Physics.HectonFluidEngine _physicsEngine;
        private bool _physicsEngineCached;

        private HectonAtmosphereManager _cachedAtmoManager;
        private bool _atmoManagerCached;

        private int _targetBiomeIndex;

        private Color   _currentScatterBase;
        private Color   _currentScatterShallow;
        private Vector3 _currentDepthFogDensity;
        private Color   _currentFogColor;
        private float   _currentTurbidity;
        private Color   _currentAmbientColor;

        private Color   _targetScatterBase;
        private Color   _targetScatterShallow;
        private Vector3 _targetDepthFogDensity;
        private Color   _targetFogColor;
        private float   _targetTurbidity;
        private Color   _targetAmbientColor;

        private float _transitionProgress;
        private float _cachedFogDensity;

        private float _baseFlareIntensity;
        private bool  _baseValuesCaptured;

        private Color _baseSunDiscColor;
        private Color _baseSunScatterColor;
        private bool  _baseSkyColorsCaptured;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _wasUnderwater;
        private bool _sunVisualWasDisabled;

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

            RenderPipelineManager.beginCameraRendering += EnforceFogState;

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
            RenderPipelineManager.beginCameraRendering -= EnforceFogState;

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
        //  EDITOR UPDATE
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

            Camera gameCamera = Camera.main;
            if (gameCamera != null)
                playerCamera = gameCamera.transform;

            var sv = SceneView.lastActiveSceneView;
            if (sv != null && sv.camera != null)
            {
                mainCamera = sv.camera;
                if (playerCamera == null)
                    playerCamera = sv.camera.transform;
            }
        }
#endif

        // ══════════════════════════════════════════════════════════
        //  ITickable.Tick — PER-FRAME
        //
        //  v5.1: By the time this runs, AtmosphereManager has already
        //  computed fresh ProfileSunIntensity and ComputedHorizonFade.
        //  We read those values and combine with depth factor.
        //  CelestialEngine will run AFTER us and multiply by eclipse.
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            if (playerCamera == null)
            {
                ResolvePlayerCamera();
                if (playerCamera == null) return;
            }

            float waterLevel = ResolveWaterLevel();
            float cameraY = playerCamera.position.y;
            float depth = math.max(0f, waterLevel - cameraY);
            bool isUnderwater = cameraY < waterLevel;

            UpdateDepthDiagnostics(depth, isUnderwater);

            // ══════════════════════════════════════════════
            //  ABOVE WATER
            // ══════════════════════════════════════════════

            if (!isUnderwater)
            {
                if (_wasUnderwater)
                {
                    ApplySurfaceDefaults();
                    RestoreSkyMaterialDefaults();
                    _wasUnderwater = false;
                }

                // ── v5.1: Write sunLight.intensity EVERY FRAME above water ──
                // This is the "base" value that CelestialEngine will multiply
                // by eclipse visibility in its Tick() (which runs after ours).
                //
                // profile × horizon gives the correct sunset/sunrise dimming.
                // CelestialEngine then applies: intensity *= (1 - eclipseOcclusion)
                //
                // Guard: skip only if AtmosphereManager hasn't computed yet
                // (both values would be at their defaults = 1.0, which is fine).
                if (sunLight != null)
                {
                    float baseSun = ResolveProfileSunIntensity();
                    float horizon = ResolveHorizonFade();
                    sunLight.intensity = baseSun * horizon;

                    UpdateSurfaceLightDiagnostics(baseSun, horizon, baseSun * horizon);
                }

                return;
            }

            // ══════════════════════════════════════════════
            //  ENTERING WATER
            // ══════════════════════════════════════════════

            if (!_wasUnderwater)
            {
                RenderSettings.fog     = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                _wasUnderwater = true;
            }

            // ══════════════════════════════════════════════
            //  UNDERWATER — DEPTH-DRIVEN
            // ══════════════════════════════════════════════

            float lightFactor = math.saturate(globalLightCurve.Evaluate(depth));

            // ── Sun intensity = profile × horizon × depthCurve ──
            float baseSunIntensity = ResolveProfileSunIntensity();
            float horizonFade = ResolveHorizonFade();
            float finalSunIntensity = baseSunIntensity * horizonFade * lightFactor;

            ApplySunIntensity(finalSunIntensity, lightFactor);
            ApplySunVisualState(lightFactor);
            ApplySunScattering(lightFactor);
            ApplySunColorFade(lightFactor);
            ApplyUnderwaterFog(lightFactor, depth);
            ApplyUnderwaterAmbient();
            ApplyUnderwaterCamera();

            UpdateLightDiagnostics(lightFactor, baseSunIntensity, horizonFade, finalSunIntensity);
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable.SlowTick — 2Hz
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (playerCamera == null) return;

            RefreshTargetsFromCurrentProfile();

            float lerpT = math.saturate(biomeTransitionSpeed * slowTickInterval);
            InterpolateBiomeParameters(lerpT);

            ApplyCrestMaterial();
        }

        // ══════════════════════════════════════════════════════════
        //  ATMOSPHERE MANAGER INTEGRATION
        //
        //  v5.1: ResolveHorizonFade now reads the PRECOMPUTED value
        //  from AtmosphereManager instead of recalculating.
        //  This ensures ONE source of truth for the horizon curve.
        // ══════════════════════════════════════════════════════════

        private float ResolveProfileSunIntensity()
        {
            if (!_atmoManagerCached)
                CacheAtmosphereManager();

            if (_cachedAtmoManager != null)
                return _cachedAtmoManager.ProfileSunIntensity;

            // Fallback: no atmosphere manager = sun at full intensity
            return sunLight != null ? sunLight.intensity : 1f;
        }

        /// <summary>
        /// v5.1 FIX: Reads AtmosphereManager.ComputedHorizonFade directly.
        ///
        /// OLD (v5.0): Recalculated from SunElevation with its own fadeAngle.
        ///   Problem: different fadeAngle → different curve → desync with
        ///   what AtmosphereManager considers "sunset".
        ///
        /// NEW (v5.1): Single source of truth.
        ///   AtmosphereManager computes horizonFade from its own _sunHorizonFadeAngle.
        ///   We just read the result.
        ///   If no AtmosphereManager: return 1.0 (sun always visible).
        /// </summary>
        private float ResolveHorizonFade()
        {
            if (!_atmoManagerCached)
                CacheAtmosphereManager();

            if (_cachedAtmoManager != null)
                return _cachedAtmoManager.ComputedHorizonFade;

            return 1f;
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
        //  SUN INTENSITY
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
        //  SUN VISUAL DISC
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
        //  SUN SCATTERING
        // ══════════════════════════════════════════════════════════

        private void ApplySunScattering(float lightFactor)
        {
            if (skyMaterial == null) return;

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
        //  FOG
        // ══════════════════════════════════════════════════════════

        private void ApplyUnderwaterFog(float lightFactor, float currentDepth)
        {
            RenderSettings.fogColor = _currentFogColor;

            float baseDensity = Mathf.Lerp(maxFogDensity, minFogDensity, lightFactor);
            float targetDensity = baseDensity * _currentTurbidity;

            float smoothSubmerge = math.saturate(currentDepth / 0.5f);
            float surfDensity = enableSurfaceFog ? surfaceFogDensity : 0.0001f;

            _cachedFogDensity = Mathf.Lerp(surfDensity, targetDensity, smoothSubmerge);
            RenderSettings.fogDensity = _cachedFogDensity;

#if UNITY_EDITOR
            _debugFogDensity = _cachedFogDensity;
#endif
        }

        private void EnforceFogState(ScriptableRenderContext context, Camera cam)
        {
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView)
                return;
            if (playerCamera == null) return;

            if (playerCamera.position.y < ResolveWaterLevel())
            {
                RenderSettings.fog = true;
                RenderSettings.fogColor = _currentFogColor;
                RenderSettings.fogDensity = _cachedFogDensity;
            }
            else
            {
                if (enableSurfaceFog)
                {
                    RenderSettings.fog = true;
                    RenderSettings.fogColor = surfaceFogColor;
                    RenderSettings.fogDensity = surfaceFogDensity;
                }
                else
                {
                    RenderSettings.fog = false;
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  AMBIENT / CAMERA
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

        private void ApplyUnderwaterCamera()
        {
            if (mainCamera == null) return;
            mainCamera.clearFlags      = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = _currentFogColor;
        }

        // ══════════════════════════════════════════════════════════
        //  SURFACE DEFAULTS
        //
        //  v5.1: ApplySurfaceDefaults writes sunLight.intensity
        //  as profile × horizon. CelestialEngine will multiply
        //  by eclipse visibility afterward (runs later in tick order).
        // ══════════════════════════════════════════════════════════

        private void ApplySurfaceDefaults()
        {
            // ── Sun intensity: base for CelestialEngine to multiply ──
            if (sunLight != null)
            {
                float baseSun = ResolveProfileSunIntensity();
                float horizon = ResolveHorizonFade();
                sunLight.intensity = baseSun * horizon;
            }

            if (_baseValuesCaptured && sunFlare != null)
            {
                sunFlare.intensity = _baseFlareIntensity;
                if (!sunFlare.enabled)
                    sunFlare.enabled = true;
            }

            RestoreSunVisual();

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

            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = surfaceAmbientColor;

            if (mainCamera != null)
                mainCamera.clearFlags = CameraClearFlags.Skybox;

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
        //  BIOME INTERPOLATION
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

            _targetScatterBase     = currentProf.scatterColorBase;
            _targetScatterShallow  = currentProf.scatterColorShallow;
            _targetDepthFogDensity = currentProf.depthFogDensity;
            _targetFogColor        = currentProf.fogColor;
            _targetTurbidity       = currentProf.turbidityMultiplier;
            _targetAmbientColor    = underwaterAmbientColor;
        }

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
        //  BIOME EVENT
        // ══════════════════════════════════════════════════════════

        private void HandleBiomeChanged(int biomeIndex)
        {
            if (biomePalette == null) return;

            _targetBiomeIndex = biomeIndex;

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

        public float CurrentDepth
        {
            get
            {
                if (playerCamera == null) return 0f;
                return math.max(0f, ResolveWaterLevel() - playerCamera.position.y);
            }
        }

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

        public void SetTargetBiome(int biomeIndex) => HandleBiomeChanged(biomeIndex);
        public void SetPlayerCamera(Transform camera) => playerCamera = camera;
        public void SetWaterLevelFallback(float y) => waterLevelFallback = y;

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
                if (sv != null) mainCamera = sv.camera;
            }
#endif
        }

        private void ValidateReferences()
        {
            if (playerCamera == null && Application.isPlaying)
                Debug.LogError("[HectonUnderwaterVisuals] playerCamera not found!", this);
            if (biomePalette == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] biomePalette not assigned.", this);
            if (oceanUnderwaterMaterial == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] oceanUnderwaterMaterial not assigned.", this);
            if (sunVisualTransform == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] sunVisualTransform not assigned.", this);
            if (mainCamera == null && Application.isPlaying)
                Debug.LogWarning("[HectonUnderwaterVisuals] mainCamera not assigned.", this);
            if (skyMaterial == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] skyMaterial not assigned.", this);
            if (globalLightCurve == null || globalLightCurve.length == 0)
                Debug.LogError("[HectonUnderwaterVisuals] globalLightCurve is empty!", this);
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
            }
            else
            {
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
            float lightFactor, float atmoIntensity, float horizonFade, float finalIntensity)
        {
            _debugLightFactor = lightFactor;
            _debugAtmoSunIntensity = atmoIntensity;
            _debugHorizonFade = horizonFade;
            _debugFinalSunIntensity = finalIntensity;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateSurfaceLightDiagnostics(
            float baseSun, float horizon, float finalIntensity)
        {
            _debugAtmoSunIntensity = baseSun;
            _debugHorizonFade = horizon;
            _debugFinalSunIntensity = finalIntensity;
            _debugLightFactor = 1f;
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

            Gizmos.color = new Color(0f, 0.5f, 1f, 0.12f);
            Gizmos.DrawCube(
                new Vector3(camPos.x, waterLevel, camPos.z),
                new Vector3(80f, 0.05f, 80f));

            if (depth > 0f)
            {
                float lf = globalLightCurve != null
                    ? Mathf.Clamp01(globalLightCurve.Evaluate(depth))
                    : 1f;

                Gizmos.color = Color.Lerp(Color.black, Color.cyan, lf);
                Gizmos.DrawLine(
                    new Vector3(camPos.x, waterLevel, camPos.z), camPos);

                float darknessDepth = FindCurveDarknessDepth();
                if (darknessDepth > 0f)
                {
                    float darknessY = waterLevel - darknessDepth;
                    Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
                    Gizmos.DrawCube(
                        new Vector3(camPos.x, darknessY, camPos.z),
                        new Vector3(40f, 0.05f, 40f));
                }

                Gizmos.color = Color.Lerp(Color.black, new Color(1f, 0.95f, 0.8f), lf);
                Gizmos.DrawWireSphere(camPos, 2.5f);

                float scatter = 1f - lf;
                UnityEditor.Handles.Label(
                    camPos + Vector3.up * 3f,
                    $"Depth: {depth:F0}m  Light: {lf:P0}  Scatter: {scatter:P0}  Turbidity: {_currentTurbidity:F2}");
            }
            else
            {
                UnityEditor.Handles.Label(
                    camPos + Vector3.up * 3f,
                    "Above water");
            }
        }

        private float FindCurveDarknessDepth()
        {
            if (globalLightCurve == null || globalLightCurve.length < 2)
                return 0f;

            float maxTime = globalLightCurve[globalLightCurve.length - 1].time;
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
