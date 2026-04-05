// ============================================================================
// HECTON-8 — HectonCelestialEngine.cs  v5.1
// Небесная механика: газовый гигант, затмения, planet-shine, окклюзия, небо.
//
// ═══════════════════════════════════════════════════════════════
// v5.1 CHANGES — RACE CONDITION FIX + SKY OCCLUSION:
// ═══════════════════════════════════════════════════════════════
//
//   [FIX] DefaultExecutionOrder(-3000):
//     Runs AFTER AtmosphereManager(-6000) and UnderwaterVisuals(-4000).
//     By the time ApplySunOcclusion() executes, sunLight.intensity
//     already contains: ProfileSunIntensity × horizonFade × depthFactor
//     (written by UnderwaterVisuals).
//
//   [FIX] ApplySunOcclusion() — MULTIPLY, NOT OVERWRITE:
//     OLD (v4.x, RACE CONDITION):
//       sunLight.intensity = ProfileSunIntensity × horizonFade × visibility
//       ← LOST depth factor! Underwater sun was too bright.
//       ← Sunset flicker: UnderwaterVisuals wrote correct value,
//          then CelestialEngine overwrote without depth.
//
//     NEW (v5.1, CORRECT):
//       sunLight.intensity *= visibility
//       ← Preserves ALL previous factors (profile × horizon × depth).
//       ← Eclipse just dims whatever is already there.
//       ← Zero knowledge of depth system required.
//
//   [FIX] _EclipseOcclusion → sky shader:
//     Already existed in v4.0 but now documented as critical path.
//     Shader must multiply sun glow by (1 - _EclipseOcclusion).
//     Without this, skybox sun disc stays bright during eclipse.
//
//   [FIX] UpdateSkyboxBlend — eclipse triggers night sky:
//     _currentBlend = max(timeBlend, _smoothedOcclusionFactor)
//     During eclipse, sky darkens to night profile even if it's noon.
//     Stars appear. Horizon dims. Looks correct.
//
// ═══════════════════════════════════════════════════════════════
// EXECUTION CHAIN (deterministic via DefaultExecutionOrder):
//   1. AtmosphereManager(-6000).Tick() → ProfileSunIntensity, HorizonFade
//   2. UnderwaterVisuals(-4000).Tick() → sunLight.intensity = P × H × depth
//   3. CelestialEngine(-3000).Tick()   → sunLight.intensity *= visibility
//                                       → sky shader receives _EclipseOcclusion
//
// PRESERVED FROM v4.x:
//   ✓ Gas giant rendering (MaterialPropertyBlock)
//   ✓ Eclipse detection (angular occlusion + hysteresis)
//   ✓ Eclipse backlight (Fresnel)
//   ✓ Planet-shine
//   ✓ Lens flare occlusion
//   ✓ Skybox day/night blend
//   ✓ Sun visual disc positioning
//   ✓ _GameTime for sky shader (seamless cloud scrolling)
//   ✓ _NightBlend, _SunElevation for sky shader
//   ✓ Zero GC in hot path
// ============================================================================

using System;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Hecton8.Atmosphere;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Celestial
{
    [Serializable]
    public struct SkyColorProfile
    {
        [ColorUsage(false, true)]
        [Tooltip("Sky color at zenith")]
        public Color zenithColor;

        [ColorUsage(false, true)]
        [Tooltip("Sky color at horizon")]
        public Color horizonColor;

        [ColorUsage(false, true)]
        [Tooltip("Sky color at nadir")]
        public Color nadirColor;

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
    [DefaultExecutionOrder(-3000)]  // v5.1: MUST tick AFTER UnderwaterVisuals(-4000)
    public class HectonCelestialEngine : MonoBehaviour, ITickable
    {
        // ─────────────────────────────────────────────
        // КОНФИГУРАЦИЯ
        // ─────────────────────────────────────────────

        [Header("═══ REFERENCES ═══")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Transform aegirTransform;
        [SerializeField] private Renderer aegirRenderer;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private HectonAtmosphereManager _atmosphereManager;

        [Header("═══ SKY MATERIAL ═══")]
        [SerializeField] private Material _skyMaterial;
        [SerializeField] private Material _skyOverlayMaterial;
        [SerializeField] private float _cloudSpeed = 0.01f;

        [Header("═══ SKY COLOR PROFILES ═══")]
        [SerializeField] private SkyColorProfile _dayProfile = new SkyColorProfile
        {
            zenithColor  = new Color(0.05f, 0.08f, 0.25f, 1f),
            horizonColor = new Color(0.4f, 0.35f, 0.5f, 1f),
            nadirColor   = new Color(0.02f, 0.03f, 0.08f, 1f)
        };

        [SerializeField] private SkyColorProfile _nightProfile = new SkyColorProfile
        {
            zenithColor  = new Color(0.01f, 0.005f, 0.03f, 1f),
            horizonColor = new Color(0.08f, 0.05f, 0.12f, 1f),
            nadirColor   = new Color(0.005f, 0.003f, 0.01f, 1f)
        };

        [Header("═══ SUN OCCLUSION ═══")]
        [SerializeField] private LensFlareComponentSRP _sunLensFlare;
        [SerializeField] private float sunDistance = 100000f;
        [SerializeField] private Transform sunVisualTransform;
        [SerializeField] private float flareFadeMarginDegrees = 2.0f;
        [SerializeField] private float flareFadeSpeed = 5.0f;

        [Header("═══ SKYBOX ═══")]
        [SerializeField] private Material daySkybox;
        [SerializeField] private Material nightSkybox;
        [SerializeField] private Material blendedSkyboxMaterial;

        [Header("═══ ORBITAL PARAMETERS ═══")]
        [SerializeField] private float orbitalPeriod = 3600f;
        [SerializeField] private Vector3 sunOrbitAxis = Vector3.right;
        [SerializeField] private float sunStartAngle;

        [Header("═══ ECLIPSE DETECTION ═══")]
        [SerializeField] private float eclipseAngularRadiusOverride;
        [SerializeField] private float eclipseHysteresisMargin = 0.5f;

        [Header("═══ ECLIPSE BACKLIGHT ═══")]
        [SerializeField] private float backlitAlignmentSoftStart = 0.97f;
        [SerializeField] private float backlitAlignmentFullStart = 0.995f;
        [SerializeField] private float backlitFactorMultiplier = 1.0f;

        [Header("═══ PLANET-SHINE ═══")]
        [SerializeField] private float planetShineMaxIntensity = 0.35f;
        [SerializeField] private Color planetShineColor = Color.HSVToRGB(0.75f, 0.2f, 0.9f);
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
        // RUNTIME STATE
        // ─────────────────────────────────────────────

        private Light _planetShineLight;
        private GameObject _planetShineLightGO;

        private MaterialPropertyBlock _aegirMPB;
        private MaterialPropertyBlock _sunDiscMPB;

        private float _currentSunAngle;
        private float _currentBlend;
        private float _currentStarIntensity;
        private float _currentPhase;
        private bool _isEclipseActive;
        private float _eclipseAngularRadius;
        private float _accumulatedOrbitalAngle;
        private float _currentBacklitFactor;

        private double _rotationAccumulator;
        private float _rotationTimer;
        private float _rotationPhase;
        private float _gameTime;

        private float _previousBlendForColors;
        private const float COLOR_BLEND_EPSILON = 0.001f;

        private float _sunOcclusionFactor;
        private float _smoothedOcclusionFactor;
        private float _baseSunIntensity;
        private bool _baseSunIntensityCaptured;
        private float _baseFlareIntensity;
        private float _baseFlareScale;
        private bool _baseFlareValuesCaptured;

        private float3 _resolvedSunDirection;

        private bool _eclipseRadiusCalculated;

        private Renderer _cachedSunDiscRenderer;
        private bool _sunDiscRendererCached;

        private float _cachedAegirRadius;

        // ─────────────────────────────────────────────
        // SHADER PROPERTY IDs
        // ─────────────────────────────────────────────

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
        private static readonly int _ID_GameTime           = Shader.PropertyToID("_GameTime");
        private static readonly int _ID_NightBlend         = Shader.PropertyToID("_NightBlend");
        private static readonly int _ID_SunElevation       = Shader.PropertyToID("_SunElevation");
        private static readonly int _ID_EclipseOcclusion   = Shader.PropertyToID("_EclipseOcclusion");
        private static readonly int _ID_OverlayDiscInnerDot = Shader.PropertyToID("_OverlayDiscInnerDot");
        private static readonly int _ID_OverlayDiscOuterDot = Shader.PropertyToID("_OverlayDiscOuterDot");

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
            _gameTime = 0f;

            _previousBlendForColors = -1f;

            // v5.1: Base sun intensity only for standalone mode (no AtmosphereManager)
            if (sunLight != null && _atmosphereManager == null)
            {
                _baseSunIntensity = sunLight.intensity;
                _baseSunIntensityCaptured = true;
            }

            CaptureBaseFlareValues();

            if (blendedSkyboxMaterial != null)
                RenderSettings.skybox = blendedSkyboxMaterial;

            if (Application.isPlaying)
            {
                GameTickManager tickManager = GameTickManager.Instance;
                if (tickManager != null)
                    tickManager.Register((ITickable)this);
            }
#if UNITY_EDITOR
            else
            {
                EditorApplication.update -= EditorTick;
                EditorApplication.update += EditorTick;
            }
#endif
        }

        private void OnDisable()
        {
            RestoreSunDefaults();
            CleanupPlanetShineLight();

            if (Application.isPlaying)
            {
                GameTickManager tickManager = GameTickManager.Instance;
                if (tickManager != null)
                    tickManager.Unregister((ITickable)this);
            }
#if UNITY_EDITOR
            else
            {
                EditorApplication.update -= EditorTick;
            }
#endif
        }

        private void OnDestroy()
        {
            OnEclipseStart = null;
            OnEclipseEnd = null;
            OnSunAngleChanged = null;
            OnPlanetPhaseChanged = null;
        }

#if UNITY_EDITOR
        private void EditorTick()
        {
            if (Application.isPlaying || this == null)
                return;

            float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
            Tick(dt);
        }
#endif

        // ─────────────────────────────────────────────
        // ITickable — MAIN LOOP
        // ─────────────────────────────────────────────

        public void Tick(float deltaTime)
        {
            _rotationAccumulator += (double)deltaTime;
            if (_rotationAccumulator > 10000.0)
                _rotationAccumulator -= 10000.0;
            _rotationTimer = (float)_rotationAccumulator;
            _rotationPhase = (float)(_rotationAccumulator % 1.0);

            _gameTime += deltaTime * Mathf.Max(0f, _cloudSpeed);

            if (!_eclipseRadiusCalculated && aegirTransform != null && playerTransform != null)
            {
                CalculateEclipseAngularRadius();
                _eclipseRadiusCalculated = true;
            }

            _cachedAegirRadius = ComputeAegirWorldRadius();

            UpdateSunPosition(deltaTime);
            ResolveSunDirection();
            UpdateSunVisualPosition();

            float sunElevation = CalculateSunElevation();
            _currentSunAngle = sunElevation;

            CalculateEclipseBacklight();
            DetectEclipse();
            UpdateSunOcclusion(deltaTime);

            UpdateSkyboxBlend(sunElevation);
            UpdateStarIntensity(sunElevation);
            UpdateGlobalShaderData();

            UpdateSkyMaterial();

            UpdateAegirMaterial();
            UpdatePlanetShine();

            // v5.1: ApplySunOcclusion is the LAST intensity writer.
            // It MULTIPLIES whatever UnderwaterVisuals wrote.
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
                var cam = Camera.main;
                if (cam != null)
                {
                    playerTransform = cam.transform;
                    Debug.LogWarning("[HectonCelestialEngine] Player not assigned, using Main Camera.");
                }
            }

            if (_skyMaterial == null)
                Debug.LogWarning("[HectonCelestialEngine] Sky Material is not assigned!", this);
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
                    0.01f);
                _eclipseAngularRadius = math.degrees(math.atan2(radius, distance));
            }
            else
            {
                _eclipseAngularRadius = 5f;
            }
        }

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

        private float GetAegirWorldRadius() => _cachedAegirRadius;

        // ─────────────────────────────────────────────
        // SUN DIRECTION RESOLUTION
        // ─────────────────────────────────────────────

        private void ResolveSunDirection()
        {
            if (sunLight != null)
                _resolvedSunDirection = -(float3)sunLight.transform.forward;
        }

        // ─────────────────────────────────────────────
        // SUN ORBITAL LOGIC
        // ─────────────────────────────────────────────

        private void UpdateSunPosition(float dt)
        {
            if (sunLight == null) return;

            if (_atmosphereManager != null)
            {
                _accumulatedOrbitalAngle = _atmosphereManager.SunAngle;
                return;
            }

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
            Vector3 observerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

            sunVisualTransform.position = observerPos + towardSun * sunDistance;

            if (playerTransform != null)
                OrientSunVisualTowardObserver(observerPos);
        }

        private void OrientSunVisualTowardObserver(Vector3 observerPos)
        {
            Vector3 toObserver = observerPos - sunVisualTransform.position;
            float distanceSqr = toObserver.sqrMagnitude;
            if (distanceSqr <= 0.0001f)
                return;

            Vector3 forward = toObserver / Mathf.Sqrt(distanceSqr);
            Vector3 referenceUp = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f
                ? Vector3.right
                : Vector3.up;

            Vector3 right = Vector3.Cross(referenceUp, forward);
            float rightSqr = right.sqrMagnitude;
            if (rightSqr <= 0.0001f)
            {
                referenceUp = Vector3.forward;
                right = Vector3.Cross(referenceUp, forward);
                rightSqr = right.sqrMagnitude;
                if (rightSqr <= 0.0001f)
                    return;
            }

            right /= Mathf.Sqrt(rightSqr);
            Vector3 stableUp = Vector3.Cross(forward, right);
            sunVisualTransform.rotation = Quaternion.LookRotation(forward, stableUp);
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

        private void UpdateSkyMaterial()
        {
            if (_skyMaterial == null && _skyOverlayMaterial == null)
                return;

            float sunElevationNormalized = math.clamp(_currentSunAngle / 90f, -1f, 1f);
            float3 fromSun = -_resolvedSunDirection;
            Vector4 sunDirection = new Vector4(fromSun.x, fromSun.y, fromSun.z, 0f);
            Vector4 aegirDirection = Vector4.zero;
            float overlayDiscInnerDot = 0.995f;
            float overlayDiscOuterDot = 0.985f;

            if (aegirTransform != null && playerTransform != null)
            {
                float3 playerPos = (float3)playerTransform.position;
                float3 aegirPos  = (float3)aegirTransform.position;
                float3 toAegir   = math.normalizesafe(aegirPos - playerPos);
                aegirDirection = new Vector4(toAegir.x, toAegir.y, toAegir.z, 0f);

                float radius = GetAegirWorldRadius();
                float dist = math.max(math.length(aegirPos - playerPos), 0.01f);
                float angularRadius = math.atan2(radius, dist);
                overlayDiscInnerDot = math.clamp(math.cos(angularRadius * 0.92f), -1f, 1f);
                overlayDiscOuterDot = math.clamp(math.cos(angularRadius * 1.12f), -1f, 1f);
            }

            ApplySkyMaterialProperties(_skyMaterial, sunElevationNormalized, sunDirection, aegirDirection, overlayDiscInnerDot, overlayDiscOuterDot);
            if (_skyOverlayMaterial != null && _skyOverlayMaterial != _skyMaterial)
                ApplySkyMaterialProperties(_skyOverlayMaterial, sunElevationNormalized, sunDirection, aegirDirection, overlayDiscInnerDot, overlayDiscOuterDot);

            float blendDelta = math.abs(_currentBlend - _previousBlendForColors);

            if (blendDelta > COLOR_BLEND_EPSILON)
            {
                _previousBlendForColors = _currentBlend;

                Color zenith  = Color.Lerp(
                    _dayProfile.zenithColor, _nightProfile.zenithColor, _currentBlend);
                Color horizon = Color.Lerp(
                    _dayProfile.horizonColor, _nightProfile.horizonColor, _currentBlend);
                Color nadir   = Color.Lerp(
                    _dayProfile.nadirColor, _nightProfile.nadirColor, _currentBlend);

                if (_skyMaterial != null)
                {
                    _skyMaterial.SetColor(_ID_SkyColorZenith,  zenith);
                    _skyMaterial.SetColor(_ID_SkyColorHorizon, horizon);
                    _skyMaterial.SetColor(_ID_SkyColorNadir,   nadir);
                }

                if (_skyOverlayMaterial != null && _skyOverlayMaterial != _skyMaterial)
                {
                    _skyOverlayMaterial.SetColor(_ID_SkyColorZenith,  zenith);
                    _skyOverlayMaterial.SetColor(_ID_SkyColorHorizon, horizon);
                    _skyOverlayMaterial.SetColor(_ID_SkyColorNadir,   nadir);
                }
            }
        }

        private void ApplySkyMaterialProperties(
            Material targetMaterial,
            float sunElevationNormalized,
            Vector4 sunDirection,
            Vector4 aegirDirection,
            float overlayDiscInnerDot,
            float overlayDiscOuterDot)
        {
            if (targetMaterial == null)
                return;

            targetMaterial.SetFloat(_ID_GameTime, _gameTime);
            targetMaterial.SetFloat(_ID_NightBlend, _currentBlend);
            targetMaterial.SetFloat(_ID_StarIntensity, _currentStarIntensity);
            targetMaterial.SetFloat(_ID_SunElevation, sunElevationNormalized);
            targetMaterial.SetFloat(_ID_EclipseOcclusion, _smoothedOcclusionFactor);
            targetMaterial.SetVector(_ID_SunDirection, sunDirection);
            targetMaterial.SetVector(_ID_AegirDirection, aegirDirection);
            targetMaterial.SetFloat(_ID_OverlayDiscInnerDot, overlayDiscInnerDot);
            targetMaterial.SetFloat(_ID_OverlayDiscOuterDot, overlayDiscOuterDot);
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
        /// v5.1 FIX: MULTIPLY, NOT OVERWRITE.
        ///
        /// By this point in the tick chain:
        ///   - AtmosphereManager has computed ProfileSunIntensity + HorizonFade
        ///   - UnderwaterVisuals has written:
        ///       sunLight.intensity = profile × horizon × depthFactor
        ///
        /// We simply multiply by eclipse visibility:
        ///   sunLight.intensity *= (1 - occlusionFactor)
        ///
        /// This preserves ALL previous factors. Eclipse just dims
        /// whatever is already there. Zero knowledge of depth system.
        ///
        /// STANDALONE MODE (no AtmosphereManager):
        ///   If nothing else has written sunLight.intensity this frame,
        ///   we use our captured _baseSunIntensity as the starting point.
        ///   This handles the case where CelestialEngine runs alone.
        ///
        /// EDGE CASE — visibility ≈ 1 (no eclipse):
        ///   Skip the multiply entirely. This avoids unnecessary
        ///   float imprecision that could cause 0.9999 × 1.0 = 0.9999
        ///   drift over thousands of frames.
        /// </summary>
        private void ApplySunOcclusion()
        {
            float visibility = 1.0f - _smoothedOcclusionFactor;

            // ── Sun Light Intensity ──
            if (sunLight != null)
            {
                if (_atmosphereManager != null)
                {
                    // v5.1: MULTIPLY the existing intensity.
                    // UnderwaterVisuals already wrote: profile × horizon × depth.
                    // We just dim by eclipse.
                    if (visibility < 0.999f)
                    {
                        sunLight.intensity *= visibility;
                    }
                    // else: visibility ≈ 1, no eclipse active, skip multiply
                }
                else if (_baseSunIntensityCaptured)
                {
                    // Standalone: no UnderwaterVisuals pipeline.
                    // We are the sole intensity controller.
                    sunLight.intensity = _baseSunIntensity * visibility;
                }
            }

            // ── Lens Flare ──
            if (_sunLensFlare != null && _baseFlareValuesCaptured)
            {
                _sunLensFlare.intensity = _baseFlareIntensity * visibility;
                _sunLensFlare.scale = _baseFlareScale * visibility;

                bool shouldBeEnabled = visibility > 0.001f;
                if (_sunLensFlare.enabled != shouldBeEnabled)
                    _sunLensFlare.enabled = shouldBeEnabled;
            }

            // ── Sun Visual Disc ──
            if (sunVisualTransform != null)
            {
                bool shouldBeActive = visibility > 0.001f;
                if (sunVisualTransform.gameObject.activeSelf != shouldBeActive)
                    sunVisualTransform.gameObject.SetActive(shouldBeActive);

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
                sunLight.intensity = _baseSunIntensity;

            if (_sunLensFlare != null && _baseFlareValuesCaptured)
            {
                _sunLensFlare.intensity = _baseFlareIntensity;
                _sunLensFlare.scale = _baseFlareScale;
                if (!_sunLensFlare.enabled)
                    _sunLensFlare.enabled = true;
            }

            if (sunVisualTransform != null && !sunVisualTransform.gameObject.activeSelf)
                sunVisualTransform.gameObject.SetActive(true);
        }

        // ─────────────────────────────────────────────
        // SKYBOX BLEND
        // ─────────────────────────────────────────────

        private void UpdateSkyboxBlend(float sunElevation)
        {
            float range = twilightStartAngle - twilightEndAngle;
            if (range < 0.001f) range = 10f;

            float timeBlend = math.saturate((twilightStartAngle - sunElevation) / range);
            timeBlend = SmoothStep01(timeBlend);

            // v5.1: Eclipse also triggers night sky.
            _currentBlend = math.max(timeBlend, _smoothedOcclusionFactor);

            if (blendedSkyboxMaterial != null)
                blendedSkyboxMaterial.SetFloat(_ID_Blend, _currentBlend);
        }

        private void UpdateStarIntensity(float sunElevation)
        {
            float range = twilightStartAngle - twilightEndAngle;
            if (range < 0.001f) range = 10f;

            float timeStars = math.saturate((twilightStartAngle - sunElevation) / range);
            timeStars = SmoothStep01(timeStars);

            _currentStarIntensity = math.max(timeStars, _smoothedOcclusionFactor);

            if (blendedSkyboxMaterial != null)
                blendedSkyboxMaterial.SetFloat(_ID_StarIntensity, _currentStarIntensity);
        }

        // ─────────────────────────────────────────────
        // GLOBAL SHADER DATA
        // ─────────────────────────────────────────────

        private void UpdateGlobalShaderData()
        {
            if (_atmosphereManager != null) return;

            float3 fromSun = -_resolvedSunDirection;
            Shader.SetGlobalVector(_ID_SunDirection,
                new Vector4(fromSun.x, fromSun.y, fromSun.z, 0f));
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
                float range = math.max(
                    backlitAlignmentFullStart - backlitAlignmentSoftStart, 0.001f);
                float t = math.saturate(
                    (alignment - backlitAlignmentSoftStart) / range);
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
            _aegirMPB.SetFloat(_ID_GlobalRotation, _rotationPhase);

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
            float angleDeg = math.degrees(
                math.acos(math.clamp(dotSunAegir, -1f, 1f)));

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

            Gizmos.color = planetShineColor;
            Gizmos.DrawLine((Vector3)aegirPos, (Vector3)playerPos);

            Gizmos.color = _isEclipseActive ? Color.red : Color.yellow;
            float3 toSun = _resolvedSunDirection;
            Gizmos.DrawRay((Vector3)playerPos, (Vector3)(toSun * 50f));

            float3 toAegir = math.normalizesafe(aegirPos - playerPos);
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.5f);
            Gizmos.DrawRay((Vector3)playerPos,
                (Vector3)(toAegir * math.length(aegirPos - playerPos)));

            float gizmoRadius = GetAegirWorldRadius();
            if (_currentBacklitFactor > 0.01f)
            {
                Gizmos.color = new Color(1f, 0.8f, 0.2f, _currentBacklitFactor);
                Gizmos.DrawWireSphere((Vector3)aegirPos, gizmoRadius * 1.05f);
            }

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere((Vector3)aegirPos, gizmoRadius);

            if (_smoothedOcclusionFactor > 0.01f)
            {
                Gizmos.color = new Color(1f, 0.2f, 0f, _smoothedOcclusionFactor * 0.6f);
                Gizmos.DrawWireSphere((Vector3)aegirPos, gizmoRadius * 1.02f);
            }

            if (sunVisualTransform != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(sunVisualTransform.position, 500f);
            }

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
