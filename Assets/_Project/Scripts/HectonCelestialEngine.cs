using System;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

[ExecuteAlways]
[DisallowMultipleComponent]
public class HectonCelestialEngine : MonoBehaviour
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

    [Tooltip("Ссылка на HectonAtmosphereManager для синхронизации угла Солнца")]
    [SerializeField] private MonoBehaviour atmosphereManagerRef;

    [Header("═══ SUN OCCLUSION ═══")]
    [Tooltip("LensFlareComponentSRP on the Sun directional light")]
    [SerializeField] private MonoBehaviour sunLensFlare; // LensFlareComponentSRP (type-agnostic for compilation safety)

    [Tooltip("Distance at which the sun visual disc is placed along the light's reverse forward vector")]
    public float sunDistance = 100000f;

    [Tooltip("Optional visual sun disc transform (billboard/quad). Positioned at sunDistance.")]
    [SerializeField] private Transform sunVisualTransform;

    [Tooltip("Angular margin (degrees) outside Aegir disc where flare begins to fade (terminator/penumbra zone)")]
    [SerializeField] private float flareFadeMarginDegrees = 2.0f;

    [Tooltip("Speed of flare fade lerp (higher = faster response)")]
    [SerializeField] private float flareFadeSpeed = 5.0f;

    [Header("═══ SKYBOX ═══")]
    [SerializeField] private Material daySkybox;
    [SerializeField] private Material nightSkybox;

    [Tooltip("Материал скайбокса, поддерживающий _Blend и _StarIntensity")]
    [SerializeField] private Material blendedSkyboxMaterial;

    [Header("═══ ORBITAL PARAMETERS ═══")]
    [Tooltip("Полный цикл день/ночь в секундах")]
    [SerializeField] private float orbitalPeriod = 3600f;

    [Tooltip("Ось вращения Солнца вокруг сцены (нормализуется)")]
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

    [Tooltip("Цвет Planet-Shine (HSV: H=0.75 S=0.2 V=0.9 → бледно-фиолетовый)")]
    [SerializeField] private Color planetShineColor = Color.HSVToRGB(0.75f, 0.2f, 0.9f);

    [Tooltip("Порог фазы ниже которого Planet-Shine гасится (New Moon dim)")]
    [SerializeField] private float planetShineNewMoonThreshold = 0.1f;

    [Header("═══ SHADER PARAMETERS ═══")]
    [Tooltip("Скорость вращения экваториальных облаков")]
    [SerializeField] private float equatorialRotationSpeed = 0.02f;

    [Tooltip("Скорость вращения полярных облаков (множитель)")]
    [SerializeField] private float polarRotationMultiplier = 0.4f;

    [Tooltip("Интенсивность Backlit на теневой стороне")]
    [SerializeField] private float backlitIntensity = 0.08f;

    [Tooltip("Интенсивность Emission (грозы)")]
    [SerializeField] private float stormEmissionIntensity = 1.0f;

    [Header("═══ TRANSITION CURVES ═══")]
    [Tooltip("Угол Солнца (градусы) при котором начинается переход в ночь")]
    [SerializeField] private float twilightStartAngle = 5f;

    [Tooltip("Угол Солнца (градусы) при котором ночь полная")]
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
    private MaterialPropertyBlock _aegirMPB;

    private float _currentSunAngle;
    private float _currentBlend;
    private float _currentStarIntensity;
    private float _currentPhase;
    private bool _isEclipseActive;
    private float _eclipseAngularRadius;
    private float _accumulatedOrbitalAngle;
    private float _currentBacklitFactor;

    // Sun occlusion state
    private float _sunOcclusionFactor;       // 0 = fully visible, 1 = fully occluded
    private float _smoothedOcclusionFactor;  // lerped version for smooth fading
    private float _baseSunIntensity;         // cached original sun light intensity
    private bool _baseSunIntensityCaptured;
    private float _baseFlareIntensity;       // cached original lens flare intensity
    private float _baseFlareScale;           // cached original lens flare scale
    private bool _baseFlareValuesCaptured;

    // LensFlareComponentSRP reflection cache (avoids hard dependency)
    private System.Reflection.PropertyInfo _flareIntensityProp;
    private System.Reflection.PropertyInfo _flareScaleProp;
    private System.Reflection.PropertyInfo _flareEnabledProp;
    private bool _hasFlareReflection;

    // Cached 3D sun direction from AtmosphereManager (world-space, pointing TOWARD the sun)
    private float3 _resolvedSunDirection;

    // Shader property IDs — кэшируем один раз
    private static readonly int _SunDirection       = Shader.PropertyToID("_SunDirection");
    private static readonly int _BacklitIntensity   = Shader.PropertyToID("_BacklitIntensity");
    private static readonly int _EquatorialSpeed    = Shader.PropertyToID("_EquatorialSpeed");
    private static readonly int _PolarMultiplier    = Shader.PropertyToID("_PolarMultiplier");
    private static readonly int _PlanetPhase        = Shader.PropertyToID("_PlanetPhase");
    private static readonly int _StormEmission      = Shader.PropertyToID("_StormEmission");
    private static readonly int _Blend              = Shader.PropertyToID("_Blend");
    private static readonly int _StarIntensity      = Shader.PropertyToID("_StarIntensity");
    private static readonly int _FresnelSunDir      = Shader.PropertyToID("_FresnelSunDir");
    private static readonly int _SunBacklitFactor   = Shader.PropertyToID("_SunBacklitFactor");

    // AtmosphereManager reflection cache
    private System.Reflection.PropertyInfo _sunAngleProperty;
    private System.Reflection.PropertyInfo _sunDirectionProperty;
    private System.Reflection.FieldInfo    _sunDirectionField;
    private bool _hasAtmosphereManager;
    private bool _hasAtmosphereSunDirection;

    // ─────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────

    private void OnEnable()
    {
        ValidateReferences();
        InitializeMaterialPropertyBlock();
        InitializePlanetShineLight();
        CacheAtmosphereManagerAccess();
        CacheLensFlareAccess();
        CalculateEclipseAngularRadius();

        _accumulatedOrbitalAngle = sunStartAngle;
        _currentBacklitFactor = 0f;
        _smoothedOcclusionFactor = 0f;
        _sunOcclusionFactor = 0f;
        _baseSunIntensityCaptured = false;
        _baseFlareValuesCaptured = false;

        // Capture base sun intensity
        if (sunLight != null)
        {
            _baseSunIntensity = sunLight.intensity;
            _baseSunIntensityCaptured = true;
        }

        if (blendedSkyboxMaterial != null)
        {
            RenderSettings.skybox = blendedSkyboxMaterial;
        }
    }

    private void OnDisable()
    {
        // Restore sun intensity and flare on disable
        RestoreSunDefaults();
        CleanupPlanetShineLight();
    }

    private void Update()
    {
        float dt = Application.isPlaying ? Time.deltaTime : 0.016f;

        UpdateSunPosition(dt);
        ResolveSunDirection();
        UpdateSunVisualPosition();

        float sunElevation = CalculateSunElevation();
        _currentSunAngle = sunElevation;

        UpdateSkyboxBlend(sunElevation);
        UpdateStarIntensity(sunElevation);
        UpdateGlobalShaderData();

        CalculateEclipseBacklight();
        UpdateAegirMaterial();
        UpdatePlanetShine();
        DetectEclipse();

        // Sun occlusion must run after DetectEclipse so _isEclipseActive is up-to-date
        UpdateSunOcclusion(dt);
        ApplySunOcclusion();

        OnSunAngleChanged?.Invoke(_currentSunAngle);
    }

    // ─────────────────────────────────────────────
    // INITIALIZATION
    // ─────────────────────────────────────────────

    private void ValidateReferences()
    {
        if (sunLight == null)
        {
            Debug.LogError("[HectonCelestialEngine] Sun Light is not assigned!");
        }
        if (aegirTransform == null)
        {
            Debug.LogError("[HectonCelestialEngine] Aegir Transform is not assigned!");
        }
        if (playerTransform == null)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                playerTransform = cam.transform;
                Debug.LogWarning("[HectonCelestialEngine] Player not assigned, using Main Camera.");
            }
        }
    }

    private void InitializeMaterialPropertyBlock()
    {
        _aegirMPB = new MaterialPropertyBlock();
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

    private void CacheAtmosphereManagerAccess()
    {
        _hasAtmosphereManager = false;
        _hasAtmosphereSunDirection = false;

        if (atmosphereManagerRef == null) return;

        var type = atmosphereManagerRef.GetType();

        // ── Cache SunAngle (float) property ──
        _sunAngleProperty = type.GetProperty("SunAngle")
                         ?? type.GetProperty("sunAngle")
                         ?? type.GetProperty("CurrentSunAngle");

        if (_sunAngleProperty != null && _sunAngleProperty.PropertyType == typeof(float))
        {
            _hasAtmosphereManager = true;
        }
        else
        {
            var field = type.GetField("sunAngle")
                     ?? type.GetField("SunAngle")
                     ?? type.GetField("currentSunAngle");
            if (field != null)
            {
                Debug.LogWarning(
                    "[HectonCelestialEngine] Found field but not property for SunAngle on AtmosphereManager. "
                  + "Using internal orbit for angle."
                );
            }
        }

        // ── Cache SunDirection (Vector3 / float3) property or field ──
        _sunDirectionProperty = type.GetProperty("SunDirection")
                             ?? type.GetProperty("sunDirection")
                             ?? type.GetProperty("SunDir");

        if (_sunDirectionProperty != null &&
           (_sunDirectionProperty.PropertyType == typeof(Vector3) ||
            _sunDirectionProperty.PropertyType == typeof(float3)))
        {
            _hasAtmosphereSunDirection = true;
        }
        else
        {
            _sunDirectionField = type.GetField("SunDirection")
                              ?? type.GetField("sunDirection")
                              ?? type.GetField("SunDir");

            if (_sunDirectionField != null &&
               (_sunDirectionField.FieldType == typeof(Vector3) ||
                _sunDirectionField.FieldType == typeof(float3)))
            {
                _hasAtmosphereSunDirection = true;
            }
        }

        if (_hasAtmosphereSunDirection)
        {
            Debug.Log("[HectonCelestialEngine] Successfully linked SunDirection from AtmosphereManager.");
        }
    }

    /// <summary>
    /// Caches reflection access to LensFlareComponentSRP properties.
    /// This avoids a hard compile-time dependency on the URP lens flare type.
    /// </summary>
    private void CacheLensFlareAccess()
    {
        _hasFlareReflection = false;

        if (sunLensFlare == null) return;

        var type = sunLensFlare.GetType();

        _flareIntensityProp = type.GetProperty("intensity")
                           ?? type.GetProperty("Intensity");

        _flareScaleProp = type.GetProperty("scale")
                       ?? type.GetProperty("Scale");

        _flareEnabledProp = type.GetProperty("enabled");

        if (_flareIntensityProp != null || _flareScaleProp != null)
        {
            _hasFlareReflection = true;

            // Capture base values
            if (!_baseFlareValuesCaptured)
            {
                _baseFlareIntensity = _flareIntensityProp != null
                    ? (float)_flareIntensityProp.GetValue(sunLensFlare)
                    : 1.0f;

                _baseFlareScale = _flareScaleProp != null
                    ? (float)_flareScaleProp.GetValue(sunLensFlare)
                    : 1.0f;

                _baseFlareValuesCaptured = true;
            }

            Debug.Log("[HectonCelestialEngine] Successfully linked LensFlareComponentSRP via reflection.");
        }
        else
        {
            Debug.LogWarning(
                "[HectonCelestialEngine] sunLensFlare assigned but could not find intensity/scale properties. "
              + $"Type: {type.FullName}"
            );
        }
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
            float radius = GetAegirWorldRadius();
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

    private float GetAegirWorldRadius()
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

    // ─────────────────────────────────────────────
    // SUN DIRECTION RESOLUTION
    // ─────────────────────────────────────────────

    /// <summary>
    /// Resolves the true 3D sun direction vector.
    /// Priority: AtmosphereManager.SunDirection → Directional Light forward.
    /// Result stored in _resolvedSunDirection (world-space, pointing TOWARD the sun).
    /// </summary>
    private void ResolveSunDirection()
    {
        bool resolved = false;

        // ── Try AtmosphereManager's SunDirection vector ──
        if (_hasAtmosphereSunDirection && atmosphereManagerRef != null)
        {
            try
            {
                Vector3 dir;
                if (_sunDirectionProperty != null)
                {
                    object val = _sunDirectionProperty.GetValue(atmosphereManagerRef);
                    dir = (val is float3 f3) ? (Vector3)f3 : (Vector3)val;
                }
                else // _sunDirectionField != null
                {
                    object val = _sunDirectionField.GetValue(atmosphereManagerRef);
                    dir = (val is float3 f3) ? (Vector3)f3 : (Vector3)val;
                }

                float sqrMag = math.lengthsq((float3)dir);
                if (sqrMag > 0.001f)
                {
                    float3 normalized = math.normalize((float3)dir);

                    if (sunLight != null)
                    {
                        float3 lightFwd = (float3)sunLight.transform.forward;
                        float alignment = math.dot(normalized, lightFwd);
                        if (alignment > 0.5f)
                        {
                            normalized = -normalized;
                        }
                    }

                    _resolvedSunDirection = normalized;
                    resolved = true;
                }
            }
            catch (Exception)
            {
                // Silently fall through to directional light fallback
            }
        }

        // ── Fallback: use Directional Light forward ──
        if (!resolved && sunLight != null)
        {
            _resolvedSunDirection = -((float3)sunLight.transform.forward);
        }
    }

    // ─────────────────────────────────────────────
    // SUN ORBITAL LOGIC
    // ─────────────────────────────────────────────

    private void UpdateSunPosition(float dt)
    {
        if (sunLight == null) return;

        if (_hasAtmosphereManager && atmosphereManagerRef != null)
        {
            try
            {
                float externalAngle = (float)_sunAngleProperty.GetValue(atmosphereManagerRef);
                _accumulatedOrbitalAngle = externalAngle;
            }
            catch (Exception)
            {
                UpdateInternalOrbit(dt);
            }
        }
        else
        {
            UpdateInternalOrbit(dt);
        }

        // Rotate Directional Light
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

    /// <summary>
    /// Positions the sun visual disc (if any) at sunDistance along the light's reverse forward vector.
    /// This ensures the visual sun is always "infinitely far" and sorts behind planetary geometry.
    /// </summary>
    private void UpdateSunVisualPosition()
    {
        if (sunVisualTransform == null) return;
        if (sunLight == null) return;

        // Place at sunDistance along the direction TOWARD the sun (reverse of light forward)
        Vector3 towardSun = -sunLight.transform.forward;
        Vector3 cameraPos = playerTransform != null ? playerTransform.position : Vector3.zero;

        sunVisualTransform.position = cameraPos + towardSun * sunDistance;

        // Face the camera
        if (playerTransform != null)
        {
            sunVisualTransform.LookAt(playerTransform.position, Vector3.up);
        }
    }

    private float CalculateSunElevation()
    {
        float3 toSun = _resolvedSunDirection;
        float3 up = new float3(0, 1, 0);
        float sinElevation = math.dot(toSun, up);
        return math.degrees(math.asin(math.clamp(sinElevation, -1f, 1f)));
    }

    // ─────────────────────────────────────────────
    // SUN OCCLUSION (Flare Culling + Smooth Fade)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Computes how much the sun is occluded by Aegir.
    /// Uses angular separation between Camera→Sun and Camera→Aegir vs the dynamic angular radius.
    /// Also forces full occlusion when eclipse is active.
    /// Result: _sunOcclusionFactor (0 = visible, 1 = fully behind planet).
    /// _smoothedOcclusionFactor is the temporally smoothed version used for visual fading.
    /// </summary>
    private void UpdateSunOcclusion(float dt)
    {
        // ── Eclipse override: when eclipse event is active, force full occlusion ──
        if (_isEclipseActive)
        {
            _sunOcclusionFactor = 1.0f;
            _smoothedOcclusionFactor = math.lerp(_smoothedOcclusionFactor, 1.0f, flareFadeSpeed * dt);
            _smoothedOcclusionFactor = math.clamp(_smoothedOcclusionFactor, 0f, 1f);
            return;
        }

        if (aegirTransform == null || playerTransform == null)
        {
            _sunOcclusionFactor = 0f;
            _smoothedOcclusionFactor = math.lerp(_smoothedOcclusionFactor, 0f, flareFadeSpeed * dt);
            _smoothedOcclusionFactor = math.clamp(_smoothedOcclusionFactor, 0f, 1f);
            return;
        }

        float3 playerPos = (float3)playerTransform.position;
        float3 aegirPos  = (float3)aegirTransform.position;

        // Direction from camera toward the Sun
        float3 toSun = _resolvedSunDirection;

        // Direction from camera toward Aegir
        float3 toAegir = math.normalizesafe(aegirPos - playerPos);

        // Angular separation between sun and Aegir center as seen from camera
        float dotSunAegir = math.dot(toSun, toAegir);
        float angularSeparationDeg = math.degrees(math.acos(math.clamp(dotSunAegir, -1f, 1f)));

        // Dynamic angular radius of Aegir from camera
        float radius = GetAegirWorldRadius();
        float dist = math.max(math.length(aegirPos - playerPos), 0.01f);
        float dynamicAngularRadius = math.degrees(math.atan2(radius, dist));

        // ── Occlusion zones ──
        // Inner: sun center is within Aegir disc → fully occluded
        // Fade zone: sun is within [dynamicAngularRadius, dynamicAngularRadius + flareFadeMarginDegrees]
        //            → partial occlusion (terminator/penumbra)
        // Outer: sun is clearly outside → no occlusion

        float innerEdge = dynamicAngularRadius;
        float outerEdge = dynamicAngularRadius + math.max(flareFadeMarginDegrees, 0.01f);

        if (angularSeparationDeg <= innerEdge)
        {
            // Sun center is behind Aegir disc
            _sunOcclusionFactor = 1.0f;
        }
        else if (angularSeparationDeg < outerEdge)
        {
            // Terminator zone: smooth fade
            float t = (outerEdge - angularSeparationDeg) / (outerEdge - innerEdge);
            t = SmoothStep01(t);
            _sunOcclusionFactor = t;
        }
        else
        {
            // Sun is clearly visible
            _sunOcclusionFactor = 0f;
        }

        // ── Additional check: is the sun actually BEHIND Aegir (not in front)? ──
        // If the sun direction and the Aegir direction are roughly opposite,
        // the planet is between us and the sun. If they diverge, no occlusion.
        // We already measure angular separation; if it's small, they align.
        // But we must also ensure Aegir is actually between camera and sun.
        // For a directional (infinite distance) sun, the sun is always "behind" Aegir
        // if toSun ≈ toAegir. The angular check above handles this correctly.

        // ── Temporal smoothing ──
        _smoothedOcclusionFactor = math.lerp(
            _smoothedOcclusionFactor,
            _sunOcclusionFactor,
            math.saturate(flareFadeSpeed * dt)
        );

        // Snap near extremes to avoid lingering tiny values
        if (_smoothedOcclusionFactor < 0.001f) _smoothedOcclusionFactor = 0f;
        if (_smoothedOcclusionFactor > 0.999f) _smoothedOcclusionFactor = 1f;
    }

    /// <summary>
    /// Applies the computed occlusion factor to sun light intensity and lens flare.
    /// </summary>
    private void ApplySunOcclusion()
    {
        float visibility = 1.0f - _smoothedOcclusionFactor; // 1 = fully visible, 0 = fully occluded

        // ── Sun Light Intensity ──
        if (sunLight != null && _baseSunIntensityCaptured)
        {
            sunLight.intensity = _baseSunIntensity * visibility;
        }

        // ── Lens Flare (via reflection to avoid compile-time URP dependency) ──
        if (_hasFlareReflection && sunLensFlare != null)
        {
            try
            {
                if (_flareIntensityProp != null)
                {
                    _flareIntensityProp.SetValue(sunLensFlare, _baseFlareIntensity * visibility);
                }

                if (_flareScaleProp != null)
                {
                    _flareScaleProp.SetValue(sunLensFlare, _baseFlareScale * visibility);
                }

                // Disable the component entirely when fully occluded to avoid any bleed
                if (_flareEnabledProp != null)
                {
                    bool shouldBeEnabled = visibility > 0.001f;
                    bool currentlyEnabled = (bool)_flareEnabledProp.GetValue(sunLensFlare);
                    if (currentlyEnabled != shouldBeEnabled)
                    {
                        _flareEnabledProp.SetValue(sunLensFlare, shouldBeEnabled);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HectonCelestialEngine] Failed to set lens flare properties: {e.Message}");
                _hasFlareReflection = false; // Stop trying if reflection fails
            }
        }

        // ── Sun Visual Disc ──
        if (sunVisualTransform != null)
        {
            // Scale down to zero when occluded, or disable renderer
            bool shouldBeActive = visibility > 0.001f;
            if (sunVisualTransform.gameObject.activeSelf != shouldBeActive)
            {
                sunVisualTransform.gameObject.SetActive(shouldBeActive);
            }

            if (shouldBeActive)
            {
                // Optionally modulate alpha/emission on the sun disc material
                var sunRenderer = sunVisualTransform.GetComponent<Renderer>();
                if (sunRenderer != null)
                {
                    // Use a property block so we don't mutate the shared material
                    var mpb = new MaterialPropertyBlock();
                    sunRenderer.GetPropertyBlock(mpb);
                    mpb.SetFloat("_OcclusionFactor", visibility);
                    mpb.SetColor("_EmissionColor", Color.white * visibility);
                    sunRenderer.SetPropertyBlock(mpb);
                }
            }
        }
    }

    /// <summary>
    /// Restores sun light and flare to their original values (called on disable).
    /// </summary>
    private void RestoreSunDefaults()
    {
        if (sunLight != null && _baseSunIntensityCaptured)
        {
            sunLight.intensity = _baseSunIntensity;
        }

        if (_hasFlareReflection && sunLensFlare != null)
        {
            try
            {
                if (_flareIntensityProp != null)
                    _flareIntensityProp.SetValue(sunLensFlare, _baseFlareIntensity);
                if (_flareScaleProp != null)
                    _flareScaleProp.SetValue(sunLensFlare, _baseFlareScale);
                if (_flareEnabledProp != null)
                    _flareEnabledProp.SetValue(sunLensFlare, true);
            }
            catch (Exception) { /* best-effort restore */ }
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

        _currentBlend = math.saturate(
            (twilightStartAngle - sunElevation) / range
        );
        _currentBlend = SmoothStep01(_currentBlend);

        blendedSkyboxMaterial.SetFloat(_Blend, _currentBlend);
    }

    private void UpdateStarIntensity(float sunElevation)
    {
        if (blendedSkyboxMaterial == null) return;

        float range = twilightStartAngle - twilightEndAngle;
        if (range < 0.001f) range = 10f;

        _currentStarIntensity = math.saturate(
            (twilightStartAngle - sunElevation) / range
        );
        _currentStarIntensity = SmoothStep01(_currentStarIntensity);

        blendedSkyboxMaterial.SetFloat(_StarIntensity, _currentStarIntensity);
    }

    // ─────────────────────────────────────────────
    // GLOBAL SHADER DATA
    // ─────────────────────────────────────────────

    private void UpdateGlobalShaderData()
    {
        float3 toSun = _resolvedSunDirection;
        Shader.SetGlobalVector(_SunDirection, new Vector4(-toSun.x, -toSun.y, -toSun.z, 0f));
    }

    // ─────────────────────────────────────────────
    // ECLIPSE BACKLIGHT
    // ─────────────────────────────────────────────

    /// <summary>
    /// Calculates the eclipse backlight factor by measuring alignment
    /// between Player→Sun and Player→Giant directions.
    /// </summary>
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
            float range = backlitAlignmentFullStart - backlitAlignmentSoftStart;
            range = math.max(range, 0.001f);

            float t = math.saturate((alignment - backlitAlignmentSoftStart) / range);
            t = SmoothStep01(t);

            _currentBacklitFactor = t * backlitFactorMultiplier;
            _currentBacklitFactor = math.saturate(_currentBacklitFactor);
        }
    }

    // ─────────────────────────────────────────────
    // AEGIR MATERIAL (via MaterialPropertyBlock)
    // ─────────────────────────────────────────────

    private void UpdateAegirMaterial()
    {
        if (aegirRenderer == null) return;

        aegirRenderer.GetPropertyBlock(_aegirMPB);

        float3 toSun = _resolvedSunDirection;

        // ── Phase calculation ──
        if (aegirTransform != null && playerTransform != null)
        {
            float3 aegirToPlayer = math.normalizesafe(
                (float3)playerTransform.position - (float3)aegirTransform.position
            );
            float3 aegirToSun = toSun;
            _currentPhase = math.dot(aegirToSun, aegirToPlayer);
        }
        else
        {
            _currentPhase = math.dot(toSun, new float3(0, 0, 1));
        }

        // ── Shader data ──
        _aegirMPB.SetVector(_FresnelSunDir, new Vector4(toSun.x, toSun.y, toSun.z, 0));
        _aegirMPB.SetFloat(_BacklitIntensity, backlitIntensity);
        _aegirMPB.SetFloat(_EquatorialSpeed, equatorialRotationSpeed);
        _aegirMPB.SetFloat(_PolarMultiplier, polarRotationMultiplier);
        _aegirMPB.SetFloat(_PlanetPhase, _currentPhase);
        _aegirMPB.SetFloat(_StormEmission, stormEmissionIntensity);
        _aegirMPB.SetFloat(_SunBacklitFactor, _currentBacklitFactor);

        aegirRenderer.SetPropertyBlock(_aegirMPB);

        OnPlanetPhaseChanged?.Invoke(_currentPhase);
    }

    // ─────────────────────────────────────────────
    // PLANET-SHINE (Reflected light from Aegir)
    // ─────────────────────────────────────────────

    private void UpdatePlanetShine()
    {
        if (_planetShineLight == null || aegirTransform == null ||
            playerTransform == null)
            return;

        float3 aegirPos   = (float3)aegirTransform.position;
        float3 playerPos  = (float3)playerTransform.position;

        float3 aegirToPlayer = math.normalizesafe(playerPos - aegirPos);
        float3 aegirToSun = _resolvedSunDirection;

        float rawPhase = math.dot(aegirToSun, aegirToPlayer);

        float phaseFactor = math.saturate(
            (rawPhase - planetShineNewMoonThreshold) /
            math.max(1f - planetShineNewMoonThreshold, 0.01f)
        );
        phaseFactor = phaseFactor * phaseFactor;

        float eclipseDim = 1f - _currentBacklitFactor;

        float intensity = phaseFactor * eclipseDim * planetShineMaxIntensity;

        _planetShineLight.transform.rotation = Quaternion.LookRotation(
            (Vector3)(-aegirToPlayer)
        );

        _planetShineLight.intensity = intensity;
        _planetShineLight.color = planetShineColor;
    }

    // ─────────────────────────────────────────────
    // ECLIPSE DETECTION (event-based)
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

    /// <summary>Текущий угол возвышения Солнца в градусах</summary>
    public float SunElevation => _currentSunAngle;

    /// <summary>Текущий blend день/ночь (0=день, 1=ночь)</summary>
    public float DayNightBlend => _currentBlend;

    /// <summary>Текущая фаза планеты (-1..1)</summary>
    public float PlanetPhase => _currentPhase;

    /// <summary>Активно ли затмение</summary>
    public bool IsEclipseActive => _isEclipseActive;

    /// <summary>Текущий backlit-фактор затмения (0..1)</summary>
    public float EclipseBacklitFactor => _currentBacklitFactor;

    /// <summary>Текущая интенсивность звёзд</summary>
    public float StarIntensity => _currentStarIntensity;

    /// <summary>Resolved sun direction (world-space, pointing TOWARD sun)</summary>
    public Vector3 ResolvedSunDirection => (Vector3)_resolvedSunDirection;

    /// <summary>Current sun occlusion factor (0 = visible, 1 = fully behind planet)</summary>
    public float SunOcclusionFactor => _smoothedOcclusionFactor;

    /// <summary>Устанавливает угол орбиты вручную (для cutscene и т.д.)</summary>
    public void SetOrbitalAngle(float angleDegrees)
    {
        _accumulatedOrbitalAngle = angleDegrees % 360f;
    }

    /// <summary>Устанавливает множитель скорости орбиты</summary>
    public void SetTimeScale(float scale)
    {
        // Для использования с TimeLapse эффектом
        // Применяется через модификацию orbitalPeriod
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

        // Line Aegir → Player
        Gizmos.color = planetShineColor;
        Gizmos.DrawLine((Vector3)aegirPos, (Vector3)playerPos);

        // Eclipse cone + sun ray
        Gizmos.color = _isEclipseActive ? Color.red : Color.yellow;
        float3 toSun = _resolvedSunDirection;
        Gizmos.DrawRay((Vector3)playerPos, (Vector3)(toSun * 50f));

        float3 toAegir = math.normalizesafe(aegirPos - playerPos);
        Gizmos.color = new Color(0.5f, 0f, 1f, 0.5f);
        Gizmos.DrawRay((Vector3)playerPos, (Vector3)(toAegir * math.length(aegirPos - playerPos)));

        // Backlit factor visualisation
        if (_currentBacklitFactor > 0.01f)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, _currentBacklitFactor);
            float r = GetAegirWorldRadius() * 1.05f;
            Gizmos.DrawWireSphere((Vector3)aegirPos, r);
        }

        // Angular radius
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        float gizmoRadius = GetAegirWorldRadius();
        Gizmos.DrawWireSphere((Vector3)aegirPos, gizmoRadius);

        // Sun occlusion debug: draw the fade margin zone
        if (_smoothedOcclusionFactor > 0.01f)
        {
            Gizmos.color = new Color(1f, 0.2f, 0f, _smoothedOcclusionFactor * 0.6f);
            float occlusionRadius = GetAegirWorldRadius() * 1.02f;
            Gizmos.DrawWireSphere((Vector3)aegirPos, occlusionRadius);
        }

        // Sun visual position indicator
        if (sunVisualTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(sunVisualTransform.position, 500f);
        }
    }
#endif
}