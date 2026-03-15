// ============================================================================
// HECTON-8 — HectonUnderwaterVisuals.cs  v3.0
// ЕДИНОЛИЧНЫЙ ДИРЕКТОР СРЕДЫ: туман, свет, цвета, камера.
//
// ОТВЕТСТВЕННОСТИ:
//   • RenderSettings.fog / fogColor / fogDensity (SOLE AUTHORITY)
//   • RenderSettings.ambientLight (SOLE AUTHORITY)
//   • Camera.clearFlags / backgroundColor (SOLE AUTHORITY)
//   • Crest material: _ScatterColourBase, _ScatterColourShallow, _DepthFogDensity
//   • sunLight.intensity modulation by depth (Beer-Lambert)
//   • sunFlare.intensity modulation by depth
//   • sunVisualTransform activation/deactivation by depth
//
// ИНТЕГРАЦИЯ С AtmosphereManager:
//   Reads HectonAtmosphereManager.Instance.CurrentSunIntensity
//   as the "sky-side" sun intensity (profile × horizonFade).
//   Multiplies it by Beer-Lambert depth attenuation.
//   Final sunLight.intensity = atmosphereIntensity × exp(-depth × totalK)
//
// LIGHT MODEL:
//   totalK = _globalExtinctionK × _currentExtinctionMultiplier
//   depthFactor = exp(-depth × totalK)
//   sunLight.intensity = atmosphereSunIntensity × depthFactor
//
// FOG MODEL:
//   Underwater: fog = true, density = lerp(shallow, profile, depth/range)
//   Surface:    fog = false (or surface defaults)
//
// SAFETY:
//   • No writes to Material/RenderSettings outside Play Mode
//   • No ExecuteAlways, no Update
//   • SlowTick (2 Hz) only entry point
//   • Zero GC in hot path
// ============================================================================

using Hecton8.Core;
using Hecton8.Atmosphere;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

namespace Hecton8.Environment
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4000)]
    public sealed class HectonUnderwaterVisuals : MonoBehaviour, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REFERENCES
        // ══════════════════════════════════════════════════════════

        [Header("═══ REFERENCES ═══")]
        [Tooltip("Camera Transform игрока. Определяет глубину.")]
        [SerializeField] private Transform playerCamera;

        [Tooltip("Directional Light (солнце).\n" +
                 "Intensity = AtmosphereManager × Beer-Lambert(depth).")]
        [SerializeField] private Light sunLight;

        [Tooltip("SRP Lens Flare на солнечном свете.")]
        [SerializeField] private LensFlareComponentSRP sunFlare;

        [Tooltip("Transform визуального диска солнца.\n" +
                 "SetActive(false) на глубине.")]
        [SerializeField] private Transform sunVisualTransform;

        [Tooltip("Основная камера. clearFlags переключается под водой.")]
        [SerializeField] private Camera mainCamera;

        [Header("═══ ATMOSPHERE MANAGER ═══")]
        [Tooltip("Ссылка на HectonAtmosphereManager.\n" +
                 "Если не назначена — ищется через Instance.\n" +
                 "Используется для чтения CurrentSunIntensity.")]
        [SerializeField] private HectonAtmosphereManager atmosphereManager;

        [Header("═══ CREST MATERIAL ═══")]
        [Tooltip("Материал подводной части Crest Ocean.")]
        [SerializeField] private Material oceanUnderwaterMaterial;

        [Header("═══ BIOME PALETTE ═══")]
        [Tooltip("Палитра биомов (HectonOceanPalette).")]
        [SerializeField] private HectonOceanPalette biomePalette;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CONFIGURATION
        // ══════════════════════════════════════════════════════════

        [Header("═══ WATER LEVEL ═══")]
        [Tooltip("Fallback уровень воды если физический\n" +
                 "HectonFluidEngine.Instance недоступен.")]
        [SerializeField] private float waterLevelFallback = 4900f;

        [Header("═══ VERTICAL STRATIFICATION ═══")]
        [Tooltip("Глобальный коэффициент экстинкции (Beer-Lambert).\n" +
                 "totalK = globalK × biome.extinctionMultiplier.\n" +
                 "0.009 → мрак на ~500м (multiplier=1).")]
        [Range(0.001f, 0.05f)]
        [SerializeField] private float _globalExtinctionK = 0.009f;

        [Tooltip("Справочная глубина мрака (метры). Для fog depth scaling.")]
        [SerializeField] private float maxSunlightDepth = 500f;

        [Tooltip("Порог lightFactor для деактивации солнечного диска.\n" +
                 "Гистерезис: ON при threshold × 2.")]
        [Range(0.0001f, 0.01f)]
        [SerializeField] private float sunVisualDisableThreshold = 0.001f;

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

        [Tooltip("Ambient цвет для поверхности.\n" +
                 "Единоличный контроль RenderSettings.ambientLight.")]
        [ColorUsage(false)]
        [SerializeField] private Color surfaceAmbientColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("═══ UNDERWATER CAMERA ═══")]
        [Tooltip("Цвет фона камеры под водой (Camera.backgroundColor).\n" +
                 "Используется когда clearFlags = SolidColor.")]
        [ColorUsage(false)]
        [SerializeField] private Color underwaterCameraColor = new Color(0f, 0.03f, 0.07f, 1f);

        [Header("═══ UNDERWATER AMBIENT ═══")]
        [Tooltip("Ambient цвет под водой (RenderSettings.ambientLight).")]
        [ColorUsage(false)]
        [SerializeField] private Color underwaterAmbientColor = new Color(0.02f, 0.04f, 0.06f, 1f);

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [Header("═══ DIAGNOSTICS ═══")]
#pragma warning disable CS0414
        [SerializeField] private float _debugDepth;
        [SerializeField] private float _debugLightFactor;
        [SerializeField] private float _debugTotalK;
        [SerializeField] private float _debugCurrentMultiplier;
        [SerializeField] private float _debugAtmoSunIntensity;
        [SerializeField] private float _debugFinalSunIntensity;
        [SerializeField] private int   _debugTargetBiome;
        [SerializeField] private float _debugTransitionProgress;
        [SerializeField] private bool  _debugIsUnderwater;
        [SerializeField] private bool  _debugPhysicsEngineFound;
        [SerializeField] private bool  _debugAtmoManagerFound;
        [SerializeField] private bool  _debugSunVisualActive;
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

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Hecton8.Physics.HectonFluidEngine _physicsEngine;
        private bool _physicsEngineCached;

        private HectonAtmosphereManager _cachedAtmoManager;
        private bool _atmoManagerCached;

        private int _targetBiomeIndex;

        // Current interpolated
        private Color   _currentScatterBase;
        private Color   _currentScatterShallow;
        private Vector3 _currentDepthFogDensity;
        private Color   _currentFogColor;
        private float   _currentFogDensity;
        private float   _currentExtinctionMultiplier;
        private Color   _currentAmbientColor;
        private Color   _currentCameraColor;

        // Target
        private Color   _targetScatterBase;
        private Color   _targetScatterShallow;
        private Vector3 _targetDepthFogDensity;
        private Color   _targetFogColor;
        private float   _targetFogDensity;
        private float   _targetExtinctionMultiplier;
        private Color   _targetAmbientColor;
        private Color   _targetCameraColor;

        private float _transitionProgress;

        // Base values (for surface restore)
        private float _baseFlareIntensity;
        private bool  _baseValuesCaptured;

        private bool _registeredToTickManager;
        private bool _wasUnderwater;
        private bool _sunVisualWasDisabled;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            ResolvePlayerCamera();
            ResolveMainCamera();
            ValidateReferences();
            CachePhysicsEngine();
            CacheAtmosphereManager();
            CaptureBaseValues();
            InitializeCurrentValues();

            MapMagicBridge.OnBiomeChanged += HandleBiomeChanged;

            TryRegisterTickManager();
            _wasUnderwater = false;
            _sunVisualWasDisabled = false;
        }

        private void Start()
        {
            if (!Application.isPlaying) return;

            if (!_registeredToTickManager)
                TryRegisterTickManager();

            if (!_physicsEngineCached)
                CachePhysicsEngine();

            if (!_atmoManagerCached)
                CacheAtmosphereManager();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;

            MapMagicBridge.OnBiomeChanged -= HandleBiomeChanged;

            if (_registeredToTickManager)
            {
                GameTickManager tickManager = GameTickManager.Instance;
                if (tickManager != null)
                {
                    tickManager.Unregister((ISlowTickable)this);
                    _registeredToTickManager = false;
                }
            }

            RestoreBaseValues();
            RestoreSunVisual();
            RestoreCameraDefaults();
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable — SOLE ENTRY POINT (2 Hz)
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (!Application.isPlaying) return;
            if (playerCamera == null) return;

            // ══ 1. WATER LEVEL ══
            float waterLevel = ResolveWaterLevel();

            // ══ 2. DEPTH ══
            float cameraY = playerCamera.position.y;
            float depth = math.max(0f, waterLevel - cameraY);
            bool isUnderwater = cameraY < waterLevel;

            UpdateDiagnostics(depth, isUnderwater);

            // ══ 3. ABOVE WATER ══
            if (!isUnderwater)
            {
                ApplySurfaceDefaults();
                _wasUnderwater = false;
                return;
            }

            // ══ 4. ENTERING WATER ══
            if (!_wasUnderwater)
            {
                RenderSettings.fog     = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                _wasUnderwater = true;
            }

            // ══ 5. BEER-LAMBERT ══
            float totalK = _globalExtinctionK * _currentExtinctionMultiplier;
            float depthFactor = math.exp(-depth * totalK);

            if (depthFactor < 0.0001f)
                depthFactor = 0f;

            // ══ 6. SUN INTENSITY = atmosphere × depthFactor ══
            float atmosphereIntensity = ResolveAtmosphereSunIntensity();
            float finalSunIntensity = atmosphereIntensity * depthFactor;

            ApplySunIntensity(finalSunIntensity, depthFactor);
            ApplySunVisualState(depthFactor);

            // ══ 7. BIOME INTERPOLATION ══
            float lerpT = math.saturate(biomeTransitionSpeed * slowTickInterval);
            InterpolateBiomeParameters(lerpT);

            // ══ 8. APPLY ALL ══
            ApplyCrestMaterial();
            ApplyUnderwaterFog(depth);
            ApplyUnderwaterAmbient();
            ApplyUnderwaterCamera();

            UpdateExtinctionDiagnostics(totalK, atmosphereIntensity, finalSunIntensity);
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
            _physicsEngine = Hecton8.Physics.HectonFluidEngine.Instance;
            _physicsEngineCached = _physicsEngine != null;
#if UNITY_EDITOR
            _debugPhysicsEngineFound = _physicsEngineCached;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  ATMOSPHERE MANAGER INTEGRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Reads current sun intensity from AtmosphereManager.
        /// This is profile × horizonFade (no depth absorption).
        /// We multiply by Beer-Lambert depthFactor.
        /// Falls back to sunLight.intensity if manager unavailable.
        /// </summary>
        private float ResolveAtmosphereSunIntensity()
        {
            if (!_atmoManagerCached)
                CacheAtmosphereManager();

            if (_cachedAtmoManager != null)
                return _cachedAtmoManager.CurrentSunIntensity;

            // Fallback: read current light intensity directly
            if (sunLight != null)
                return sunLight.intensity;

            return 1f;
        }

        private void CacheAtmosphereManager()
        {
            _cachedAtmoManager = atmosphereManager;

            if (_cachedAtmoManager == null)
                _cachedAtmoManager = HectonAtmosphereManager.Instance;

            _atmoManagerCached = _cachedAtmoManager != null;

#if UNITY_EDITOR
            _debugAtmoManagerFound = _atmoManagerCached;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  SUN INTENSITY (depth-attenuated)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Sets sunLight.intensity and sunFlare.intensity.
        /// finalIntensity already includes atmosphere × depthFactor.
        /// </summary>
        private void ApplySunIntensity(float finalIntensity, float depthFactor)
        {
            if (sunLight != null)
                sunLight.intensity = finalIntensity;

            if (sunFlare != null)
            {
                sunFlare.intensity = _baseFlareIntensity * depthFactor;

                bool shouldEnable = depthFactor > sunVisualDisableThreshold;
                if (sunFlare.enabled != shouldEnable)
                    sunFlare.enabled = shouldEnable;
            }

#if UNITY_EDITOR
            _debugLightFactor = depthFactor;
#endif
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
        //  BIOME INTERPOLATION
        // ══════════════════════════════════════════════════════════

        private void InterpolateBiomeParameters(float lerpT)
        {
            _currentScatterBase     = Color.Lerp(
                _currentScatterBase, _targetScatterBase, lerpT);

            _currentScatterShallow  = Color.Lerp(
                _currentScatterShallow, _targetScatterShallow, lerpT);

            _currentDepthFogDensity = Vector3.Lerp(
                _currentDepthFogDensity, _targetDepthFogDensity, lerpT);

            _currentFogColor = Color.Lerp(
                _currentFogColor, _targetFogColor, lerpT);

            float densityDelta = math.abs(_targetFogDensity - _currentFogDensity);
            _currentFogDensity = Mathf.MoveTowards(
                _currentFogDensity, _targetFogDensity,
                densityDelta * lerpT + 0.0001f);

            _currentExtinctionMultiplier = Mathf.Lerp(
                _currentExtinctionMultiplier, _targetExtinctionMultiplier, lerpT);

            _currentAmbientColor = Color.Lerp(
                _currentAmbientColor, _targetAmbientColor, lerpT);

            _currentCameraColor = Color.Lerp(
                _currentCameraColor, _targetCameraColor, lerpT);

            float dist = ColorDistanceManhattan(
                _currentScatterBase, _targetScatterBase);
            _transitionProgress = 1f - math.saturate(dist * 10f);

#if UNITY_EDITOR
            _debugTransitionProgress = _transitionProgress;
            _debugCurrentMultiplier  = _currentExtinctionMultiplier;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  CREST MATERIAL
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
        //  URP FOG (SOLE AUTHORITY)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Underwater fog from biome profile.
        /// Density scales with depth for gradual murk increase.
        /// Uses profile's ComputedFogDensity as maximum at maxSunlightDepth.
        /// </summary>
        private void ApplyUnderwaterFog(float depth)
        {
            RenderSettings.fogColor = _currentFogColor;

            float depthMultiplier = 1f + 2f * math.saturate(
                depth / math.max(maxSunlightDepth, 1f));

            RenderSettings.fogDensity = _currentFogDensity * depthMultiplier;
        }

        // ══════════════════════════════════════════════════════════
        //  AMBIENT LIGHT (SOLE AUTHORITY)
        // ══════════════════════════════════════════════════════════

        private void ApplyUnderwaterAmbient()
        {
            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = _currentAmbientColor;
        }

        // ══════════════════════════════════════════════════════════
        //  CAMERA (SOLE AUTHORITY)
        // ══════════════════════════════════════════════════════════

        private void ApplyUnderwaterCamera()
        {
            if (mainCamera == null) return;

            mainCamera.clearFlags      = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = _currentCameraColor;
        }

        // ══════════════════════════════════════════════════════════
        //  SURFACE DEFAULTS (instant reset)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Instant reset to above-water state.
        /// SOLE AUTHORITY over all render settings.
        /// </summary>
        private void ApplySurfaceDefaults()
        {
            // ── Sun: let AtmosphereManager control intensity ──
            // We do NOT write sunLight.intensity here.
            // AtmosphereManager handles it via profile × horizonFade.

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

            HectonBiomeProfile profile = biomePalette.GetProfile(biomeIndex);
            if (profile == null) return;

            SetTargetFromProfile(profile);

#if UNITY_EDITOR
            _debugTargetBiome = biomeIndex;
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
                float totalK = _globalExtinctionK * _currentExtinctionMultiplier;
                return math.exp(-d * totalK);
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

        public float GlobalExtinctionK => _globalExtinctionK;
        public float CurrentExtinctionMultiplier => _currentExtinctionMultiplier;
        public int TargetBiomeIndex => _targetBiomeIndex;
        public float TransitionProgress => _transitionProgress;

        public void SetTargetBiome(int biomeIndex)
            => HandleBiomeChanged(biomeIndex);

        public void SetPlayerCamera(Transform camera)
            => playerCamera = camera;

        public void SetWaterLevelFallback(float y)
            => waterLevelFallback = y;

        public void SetGlobalExtinctionK(float k)
            => _globalExtinctionK = math.max(0.001f, k);

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — INIT
        // ══════════════════════════════════════════════════════════

        private void ResolvePlayerCamera()
        {
            if (playerCamera != null) return;
            Camera cam = Camera.main;
            if (cam != null) playerCamera = cam.transform;
        }

        private void ResolveMainCamera()
        {
            if (mainCamera != null) return;
            mainCamera = Camera.main;
        }

        private void ValidateReferences()
        {
            if (playerCamera == null)
                Debug.LogError("[HectonUnderwaterVisuals] playerCamera not found!", this);
            if (biomePalette == null)
                Debug.LogError("[HectonUnderwaterVisuals] biomePalette not assigned!", this);
            if (oceanUnderwaterMaterial == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] oceanUnderwaterMaterial not assigned.", this);
            if (sunVisualTransform == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] sunVisualTransform not assigned.", this);
            if (mainCamera == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] mainCamera not assigned.", this);
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

            // Do NOT restore sunLight.intensity — AtmosphereManager owns it.

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
            }
            else
            {
                _currentScatterBase          = new Color(0f, 0.03f, 0.07f, 1f);
                _currentScatterShallow       = new Color(0f, 0.15f, 0.12f, 1f);
                _currentDepthFogDensity      = new Vector3(0.5f, 0.25f, 0.15f);
                _currentFogColor             = new Color(0f, 0.05f, 0.1f, 1f);
                _currentFogDensity           = 0.04f;
                _currentExtinctionMultiplier = 1.0f;
                _currentAmbientColor         = underwaterAmbientColor;
                _currentCameraColor          = underwaterCameraColor;

                _targetScatterBase           = _currentScatterBase;
                _targetScatterShallow        = _currentScatterShallow;
                _targetDepthFogDensity       = _currentDepthFogDensity;
                _targetFogColor              = _currentFogColor;
                _targetFogDensity            = _currentFogDensity;
                _targetExtinctionMultiplier  = 1.0f;
                _targetAmbientColor          = _currentAmbientColor;
                _targetCameraColor           = _currentCameraColor;
            }

            _transitionProgress = 1f;
            _targetBiomeIndex = 0;
        }

        private void SetCurrentFromProfile(HectonBiomeProfile p)
        {
            _currentScatterBase          = p.scatterColorBase;
            _currentScatterShallow       = p.scatterColorShallow;
            _currentDepthFogDensity      = p.depthFogDensity;
            _currentFogColor             = p.fogColor;
            _currentFogDensity           = p.ComputedFogDensity;
            _currentExtinctionMultiplier = p.extinctionMultiplier;
            _currentAmbientColor         = underwaterAmbientColor;
            _currentCameraColor          = underwaterCameraColor;
        }

        private void SetTargetFromProfile(HectonBiomeProfile p)
        {
            _targetScatterBase          = p.scatterColorBase;
            _targetScatterShallow       = p.scatterColorShallow;
            _targetDepthFogDensity      = p.depthFogDensity;
            _targetFogColor             = p.fogColor;
            _targetFogDensity           = p.ComputedFogDensity;
            _targetExtinctionMultiplier = p.extinctionMultiplier;
            _targetAmbientColor         = underwaterAmbientColor;
            _targetCameraColor          = underwaterCameraColor;
            _transitionProgress         = 0f;
        }

        private void TryRegisterTickManager()
        {
            if (_registeredToTickManager) return;
            GameTickManager tm = GameTickManager.Instance;
            if (tm != null)
            {
                tm.Register((ISlowTickable)this);
                _registeredToTickManager = true;
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
        private void UpdateDiagnostics(float depth, bool underwater)
        {
            _debugDepth = depth;
            _debugIsUnderwater = underwater;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateExtinctionDiagnostics(
            float totalK, float atmoIntensity, float finalIntensity)
        {
            _debugTotalK = totalK;
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
                var sv = UnityEditor.SceneView.lastActiveSceneView;
                if (sv != null && sv.camera != null)
                    cam = sv.camera.transform;
            }
            if (cam == null) return;

            float waterLevel = waterLevelFallback;
            Vector3 camPos = cam.position;
            float depth = Mathf.Max(0f, waterLevel - camPos.y);

            // Water surface
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.12f);
            Gizmos.DrawCube(
                new Vector3(camPos.x, waterLevel, camPos.z),
                new Vector3(80f, 0.05f, 80f));

            // Depth line
            if (depth > 0f)
            {
                float t = Mathf.Clamp01(depth / maxSunlightDepth);
                Gizmos.color = Color.Lerp(Color.cyan, Color.black, t);
                Gizmos.DrawLine(
                    new Vector3(camPos.x, waterLevel, camPos.z), camPos);

                float maxSunY = waterLevel - maxSunlightDepth;
                Gizmos.color = new Color(1f, 0.15f, 0f, 0.25f);
                Gizmos.DrawCube(
                    new Vector3(camPos.x, maxSunY, camPos.z),
                    new Vector3(40f, 0.05f, 40f));
            }

            // Light factor
            float mult = 1.0f;
            if (biomePalette != null && biomePalette.Count > 0)
            {
                var prof = biomePalette.GetProfile(0);
                if (prof != null) mult = prof.extinctionMultiplier;
            }

            float totalK = _globalExtinctionK * mult;
            float lf = depth <= 0f ? 1f : Mathf.Exp(-depth * totalK);
            Gizmos.color = Color.Lerp(Color.black, new Color(1f, 0.95f, 0.8f), lf);
            Gizmos.DrawWireSphere(camPos, 2.5f);

            // Sun OFF depth
            if (totalK > 0.0001f)
            {
                float sunOffDepth = -Mathf.Log(sunVisualDisableThreshold) / totalK;
                float sunOffY = waterLevel - sunOffDepth;
                Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
                Gizmos.DrawCube(
                    new Vector3(camPos.x, sunOffY, camPos.z),
                    new Vector3(30f, 0.05f, 30f));
            }

            UnityEditor.Handles.Label(
                camPos + Vector3.up * 3f,
                $"Depth: {depth:F0}m  Light: {lf:P0}  K: {totalK:F4}");
        }
#endif
    }
}