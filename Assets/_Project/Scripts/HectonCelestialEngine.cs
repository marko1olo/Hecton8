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
using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Environment;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Hecton8.Atmosphere;
using Hecton8.World;
using CrestOceanRenderer = global::Crest.OceanRenderer;
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
        private static AtmosphericLightingState _currentAtmosphericLightingState = AtmosphericLightingState.Default;
        private static bool _hasAtmosphericLightingState;

        /// <summary>
        /// Returns the latest surface-atmosphere snapshot authored by the celestial pipeline.
        /// Consumers use this instead of re-deriving surface fog, ambient, or light tint.
        /// </summary>
        public static bool TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState state)
        {
            state = _currentAtmosphericLightingState;
            return _hasAtmosphericLightingState && state.IsValid;
        }
        // ─────────────────────────────────────────────
        // КОНФИГУРАЦИЯ
        // ─────────────────────────────────────────────

        [Header("═══ REFERENCES ═══")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Transform aegirTransform;
        [SerializeField] private ObserverRelativeCelestialBody aegirObserverRelativeBody;
        [SerializeField] private Renderer aegirRenderer;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private HectonAtmosphereManager _atmosphereManager;

        [Header("═══ SKY MATERIAL ═══")]
        [SerializeField] private Material _skyMaterial;
        [SerializeField] private float _cloudSpeed = 0.01f;

        [Header("═══ SKY COLOR PROFILES ═══")]
        [HideInInspector]
        [SerializeField] private SkyColorProfile _dayProfile = new SkyColorProfile
        {
            zenithColor  = new Color(0.05f, 0.08f, 0.25f, 1f),
            horizonColor = new Color(0.4f, 0.35f, 0.5f, 1f),
            nadirColor   = new Color(0.02f, 0.03f, 0.08f, 1f)
        };

        [HideInInspector]
        [SerializeField] private SkyColorProfile _sunsetProfile = new SkyColorProfile
        {
            zenithColor  = new Color(0.24f, 0.16f, 0.38f, 1f),
            horizonColor = new Color(1.18f, 0.58f, 0.28f, 1f),
            nadirColor   = new Color(0.06f, 0.04f, 0.10f, 1f)
        };

        [HideInInspector]
        [SerializeField] private SkyColorProfile _nightProfile = new SkyColorProfile
        {
            zenithColor  = new Color(0.01f, 0.005f, 0.03f, 1f),
            horizonColor = new Color(0.08f, 0.05f, 0.12f, 1f),
            nadirColor   = new Color(0.005f, 0.003f, 0.01f, 1f)
        };

        [Header("═══ HORIZON RESPONSE ═══")]
        [HideInInspector]
        [Tooltip("Compresses horizon luminance so the sky and gas giant dissolve into the same atmospheric band instead of reading as separate layers.")]
        [SerializeField, Range(0.25f, 1f)] private float _horizonBrightnessScale = 0.72f;
        [HideInInspector]
        [Tooltip("Pulls the horizon tint back toward the zenith hue to avoid a chalk-white band near the waterline.")]
        [SerializeField, Range(0f, 1f)] private float _horizonZenithBlend = 0.3f;

        [Header("═══ CELESTIAL ATMOSPHERE VEIL ═══")]
        [Tooltip("Thickens the horizon veil. Raise this first when the bottom of Aegir or the moons still reads as a hard cutout at the sea line.")]
        [SerializeField, Range(0f, 4f)] private float horizonDensity = 1.35f;
        [Tooltip("Clears the zenith portion of the atmosphere so the gas giant belts and moon detail stay readable overhead.")]
        [SerializeField, Range(0f, 1f)] private float zenithTransparency = 0.78f;
        [Tooltip("Controls how long the horizon veil holds before relaxing toward the zenith. Higher values keep the sky-color dissolve lower for longer.")]
        [SerializeField, Range(0.35f, 4f)] private float atmosphereBlendPower = 1.4f;

        [Header("═══ SURFACE HAZE TUNING ═══")]
        [Tooltip("Master multiplier for above-water distance haze. Raise this first when coastlines and sea stay too sharp at long range.")]
        [SerializeField, Range(0.5f, 3f)] private float _surfaceFogDensityMultiplier = 1.35f;
        [Tooltip("Extra strength for the sky-material horizon haze band. Raise this if the air still feels too clean after fog density is correct.")]
        [SerializeField, Range(0.25f, 2.5f)] private float _surfaceSkyHazeIntensityMultiplier = 1.2f;
        [Tooltip("How much sky hue is allowed to leak into above-water haze. Lower keeps the fog neutral; higher pushes stylized blue-violet air.")]
        [SerializeField, Range(0f, 1f)] private float _surfaceHazeSkyTintInfluence = 0.12f;
        [Tooltip("0 = fog inherits the atmosphere color directly. 1 = fully use the manual fog color below. This is the explicit color override for surface fog.")]
        [SerializeField, Range(0f, 1f)] private float _surfaceFogManualColorBlend = 0f;
        [Tooltip("Manual surface fog color override. Keep blend at 0 to stay fully linked to atmosphere, raise blend when art direction needs a deliberate fog tint shift.")]
        [SerializeField, ColorUsage(false, true)] private Color _surfaceFogManualColor = new Color(0.66f, 0.71f, 0.77f, 1f);
        [Tooltip("How strongly the sky horizon tint pushes the final fog color. Raise this when the waterline still reads as a hard cut instead of dissolving into the air.")]
        [SerializeField, Range(0f, 1f)] private float _surfaceFogSkyColorInfluence = 0.32f;
        [Tooltip("How much ambient scene color lifts the final fog color. Raise this when the fog still feels disconnected from the world lighting.")]
        [SerializeField, Range(0f, 1f)] private float _surfaceFogAmbientColorInfluence = 0.22f;
        [Tooltip("Widens the haze band away from the exact horizon line. Raise this when haze controls feel like they only affect a razor-thin strip.")]
        [SerializeField, Range(0.5f, 2.5f)] private float _surfaceHazeHorizonSpread = 1.35f;
        [Tooltip("Strength of the low mist shelf that sits directly on the waterline. Raise this when the far ocean still reads as a cut plane instead of dissolving into air.")]
        [SerializeField, Range(0f, 2f)] private float _surfaceHorizonMistShelfIntensity = 1f;
        [Tooltip("Vertical reach of the horizon mist shelf above the waterline. Raise this when the dissolve stays too thin and only touches the exact seam.")]
        [SerializeField, Range(0.04f, 0.32f)] private float _surfaceHorizonMistShelfHeight = 0.16f;
        [Tooltip("Softness of the horizon mist shelf transition. Raise this when the shelf still reads as a stripe instead of a gradual atmospheric mass.")]
        [SerializeField, Range(0.02f, 0.24f)] private float _surfaceHorizonMistShelfSoftness = 0.1f;

        [Space(10)]
        [Header("═══ AEGIR ATMOSPHERE COMPOSITE ═══")]
        [Tooltip("Base transmittance multiplier pushed into Aegir. Lower values keep more cloud-band body color visible through haze; higher values let the sky occlude the disc earlier.")]
        [SerializeField, Range(0f, 1.5f)] private float _atmosphereTransmittanceWeight = 0.92f;
        [Tooltip("Base in-scattering multiplier pushed into Aegir. Raise this when you want more sky glow near the horizon; lower it when Aegir starts reading as a flat fog disk.")]
        [SerializeField, Range(0f, 2f)] private float _atmosphereInscatterWeight = 0.78f;
        [Tooltip("Reduces how much shared sky transmittance is pushed into moon materials. Lower values keep moon albedo readable against bright daytime haze.")]
        [SerializeField, Range(0f, 1.5f)] private float _moonAtmosphereTransmittanceMultiplier = 0.78f;
        [Tooltip("Reduces how much shared sky in-scatter is pushed into moon materials. Lower values stop small moons from dissolving into a foggy point.")]
        [SerializeField, Range(0f, 2f)] private float _moonAtmosphereInscatterMultiplier = 0.42f;

        [Space(8)]
        [Tooltip("Day profile modulation from Horizon (0.0) to Zenith (1.0). This multiplies the live skybox color instead of replacing it.")]
        [SerializeField] private Gradient dayAtmosphere = CreateDefaultDayAtmosphereGradient();
        [Tooltip("Sunset profile modulation from Horizon (0.0) to Zenith (1.0). Keep this near neutral if you want the LUT to stay tightly glued to the authored skybox colors.")]
        [SerializeField] private Gradient sunsetAtmosphere = CreateDefaultSunsetAtmosphereGradient();
        [Tooltip("Night profile modulation from Horizon (0.0) to Zenith (1.0). Use this to control how much sky color survives on shadow sides at night.")]
        [SerializeField] private Gradient nightAtmosphere = CreateDefaultNightAtmosphereGradient();
        [Tooltip("Day profile transmittance curve from Horizon (0.0) to Zenith (1.0). Higher values near the first key make the horizon denser.")]
        [SerializeField] private AnimationCurve dayAtmosphereDensity = CreateDefaultDayAtmosphereDensityCurve();
        [Tooltip("Sunset profile transmittance curve from Horizon (0.0) to Zenith (1.0). Raise the first key to thicken dusk at the horizon.")]
        [SerializeField] private AnimationCurve sunsetAtmosphereDensity = CreateDefaultSunsetAtmosphereDensityCurve();
        [Tooltip("Night profile transmittance curve from Horizon (0.0) to Zenith (1.0). Use this to keep night silhouettes dissolved into the sky instead of becoming black cutouts.")]
        [SerializeField] private AnimationCurve nightAtmosphereDensity = CreateDefaultNightAtmosphereDensityCurve();
        [Tooltip("Day profile density multiplier applied before the LUT alpha is converted to transmittance.")]
        [SerializeField, Range(0f, 2f)] private float dayAtmosphereDensityScale = 1f;
        [Tooltip("Sunset profile density multiplier. Raise this to make dusk burn thicker around the horizon.")]
        [SerializeField, Range(0f, 2f)] private float sunsetAtmosphereDensityScale = 0.52f;
        [Tooltip("Night profile density multiplier. Lower values keep giant and moon detail visible against space at night.")]
        [SerializeField, Range(0f, 2f)] private float nightAtmosphereDensityScale = 0.24f;
        [Tooltip("Day profile in-scattering exposure. Raise this to make daytime haze brighter without changing transmittance.")]
        [SerializeField, Range(0f, 4f)] private float dayAtmosphereExposure = 1.02f;
        [Tooltip("Sunset profile in-scattering exposure. Raise this for stronger HDR burn at dusk and dawn.")]
        [SerializeField, Range(0f, 4f)] private float sunsetAtmosphereExposure = 0.58f;
        [Tooltip("Night profile in-scattering exposure. Lower values let celestial bodies fade into darkness instead of glowing.")]
        [SerializeField, Range(0f, 4f)] private float nightAtmosphereExposure = 0.001f;
        [Tooltip("Half-width in sun elevation degrees for the sunset profile window around the horizon.")]
        [SerializeField, Range(1f, 35f)] private float sunsetAtmosphereBandDegrees = 14f;
        [Tooltip("Transition depth in degrees below twilight end before the night profile reaches full weight.")]
        [SerializeField, Range(1f, 35f)] private float nightAtmosphereTransitionDegrees = 12f;
        [Tooltip("Only rebakes the atmosphere LUT when sun elevation moves by at least this many degrees, preventing pointless runtime texture uploads.")]
        [SerializeField, Range(0.05f, 5f)] private float atmosphereLutRebuildSunAngleThreshold = 0.35f;
        [HideInInspector]
        [SerializeField] private int _visualDefaultsVersion;

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

        [Header("═══ DEEP VRAM GATE ═══")]
        [Tooltip("Below this depth, celestial textures are detached from runtime materials to reduce deep-water VRAM residency. Asset imports are not modified.")]
        [SerializeField, Min(0f)] private float deepTextureUnloadDepth = 1000f;
        [Tooltip("Keeps celestial texture residency reduced until the player climbs clearly out of the deep-water threshold instead of thrashing at one boundary.")]
        [SerializeField, Min(0f)] private float deepTextureDepthHysteresis = 120f;
        [Tooltip("Allows weak hardware to drop heavy celestial textures earlier when dynamic resolution has already collapsed and the player is no longer in shallow water.")]
        [SerializeField] private bool enableAdaptiveDeepTextureResidency = true;
        [Tooltip("Do not reduce celestial texture residency from perf pressure in shallow water. This keeps near-surface sky readability intact.")]
        [SerializeField, Min(0f)] private float adaptiveDeepTextureMinDepth = 350f;
        [Tooltip("Render-scale threshold that triggers early celestial texture detachment under perf pressure.")]
        [SerializeField, Range(0.5f, 1f)] private float adaptiveDeepTextureUnloadRenderScale = 0.76f;
        [Tooltip("Render-scale threshold required before celestial textures are restored after a perf-pressure reduction.")]
        [SerializeField, Range(0.5f, 1f)] private float adaptiveDeepTextureRestoreRenderScale = 0.9f;

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
        private MaterialPropertyBlock _moonMPB;
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
        private Color _lastAppliedSkyZenith;
        private Color _lastAppliedSkyHorizon;
        private Color _lastAppliedSkyNadir;

        private float _sunOcclusionFactor;
        private float _smoothedOcclusionFactor;
        private float _baseSunIntensity;
        private bool _baseSunIntensityCaptured;
        private Color _baseSunColor = Color.white;
        private bool _baseSunColorCaptured;
        private float _baseFlareIntensity;
        private float _baseFlareScale;
        private bool _baseFlareValuesCaptured;

        private float3 _resolvedSunDirection;
        private Color _resolvedSkyZenith;
        private Color _resolvedSkyHorizon;
        private Color _resolvedSkyNadir;
        private Texture2D _celestialAtmosphereLutTexture;
        private float _currentAtmosphereExposure = 1f;
        private float _lastAtmosphereBakeSunElevation = float.PositiveInfinity;
        private float _lastAtmosphereBakeDayWeight = -1f;
        private float _lastAtmosphereBakeSunsetWeight = -1f;
        private float _lastAtmosphereBakeNightWeight = -1f;
        private Color _lastAtmosphereBakeSkyZenith;
        private Color _lastAtmosphereBakeSkyHorizon;
        private Color _lastAtmosphereBakeSkyNadir;

        private bool _eclipseRadiusCalculated;

        private Renderer _cachedSunDiscRenderer;
        private bool _sunDiscRendererCached;

        private float _cachedAegirRadius;
        private Material _aegirSharedMaterial;
        private CrestOceanRenderer _crestOceanRenderer;
        private bool _deepTextureResidencyReduced;
        private float _currentDepthMeters;
        private float _currentAdaptiveRenderScale = 1f;

        private Texture _skyHighCloudTexDefault;
        private Texture _skyMainCloudAtlasDefault;
        private Texture _skyMainCloudTexDefault;
        private Texture _skyStarTexDefault;
        private Texture _daySkyboxMainTexDefault;
        private Texture _daySkyboxEmissionTexDefault;
        private Texture _nightSkyboxMainTexDefault;
        private Texture _nightSkyboxEmissionTexDefault;
        private Texture _blendedDayCubemapDefault;
        private Texture _blendedNightCubemapDefault;
        private Texture _aegirMainTexDefault;
        private Texture _aegirDetailTexDefault;
        private Texture _aegirEmissionMapDefault;
        private Texture _aegirCelestialOcclusionTexDefault;

        private float _defaultCloudDensityThreshold;
        private float _defaultCloudSoftness;
        private float _defaultCloudSpeedMultiplier;
        private Vector4 _defaultWindDirection;
        private Color _defaultCloudLitColor;
        private Color _defaultCloudShadowColor;
        private Color _defaultSunsetCloudColor;
        private Color _defaultNightCloudColor;
        private Color _defaultSunDiscColor;
        private Color _defaultSunScatterColor;
        private bool _cachedSkyWeatherDefaults;

        private bool _surfaceWeatherOverrideActive;
        private float _surfaceWeatherCloudDensityThreshold;
        private float _surfaceWeatherCloudSoftness;
        private float _surfaceWeatherCloudSpeedMultiplier = 1f;
        private Vector4 _surfaceWeatherWindDirection = new Vector4(1f, 0f, 0f, 0f);
        private float _surfaceWeatherStarVisibilityMultiplier = 1f;
        private float _surfaceWeatherStormEmissionMultiplier = 1f;
        private float _surfaceWeatherSkyLuminanceMultiplier = 1f;
        private float _surfaceWeatherSunDiscMultiplier = 1f;
        private float _surfaceWeatherSunScatterMultiplier = 1f;
        private Color _surfaceWeatherCloudLitColor = Color.white;
        private Color _surfaceWeatherCloudShadowColor = Color.white;
        private Color _surfaceWeatherSunsetCloudColor = Color.white;
        private Color _surfaceWeatherNightCloudColor = Color.white;
        private bool _surfaceWeatherFogOverrideActive;
        private Color _surfaceWeatherFogColor = Color.white;
        private float _surfaceWeatherFogDensity;
        private Color _surfaceWeatherAmbientColor = Color.white;
        private float _surfaceWeatherSunMultiplier = 1f;
        private AtmosphericLightingState _surfaceAtmosphericLightingState = AtmosphericLightingState.Default;
        private const int CelestialBodyCacheCapacity = 8;
        private const float AtmosphereWeightBlendThreshold = 0.01f;
        private const int CelestialAtmosphereLutResolution = 512;
        private const int BestVisualDefaultsVersion = 5;
        private const float NightAtmosphereInscatterFloor = 0.001f;
        private bool _editorPreviewDirty;
        private readonly List<ObserverRelativeCelestialBody> _observerBodyCache = new List<ObserverRelativeCelestialBody>(CelestialBodyCacheCapacity); // COLD ALLOC: List<ObserverRelativeCelestialBody>[8] - cold observer-body cache for moon renderer discovery - owner: HectonCelestialEngine
        private readonly List<Renderer> _moonRenderers = new List<Renderer>(CelestialBodyCacheCapacity); // COLD ALLOC: List<Renderer>[8] - cached moon renderers for shared atmosphere overrides - owner: HectonCelestialEngine
        private readonly Color[] _celestialAtmosphereLutPixels = new Color[CelestialAtmosphereLutResolution]; // COLD ALLOC: Color[512] — celestial atmosphere LUT bake buffer — owner: HectonCelestialEngine

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
        private static readonly int _ID_CelestialAtmosphereLut = Shader.PropertyToID("_CelestialAtmosphereLUT");
        private static readonly int _ID_CelestialAtmosphereLutReady = Shader.PropertyToID("_CelestialAtmosphereLUTReady");
        private static readonly int _ID_AtmosphereExposure = Shader.PropertyToID("_AtmosphereExposure");
        private static readonly int _ID_CelestialHorizonDensity = Shader.PropertyToID("_CelestialHorizonDensity");
        private static readonly int _ID_CelestialZenithTransparency = Shader.PropertyToID("_CelestialZenithTransparency");
        private static readonly int _ID_CelestialAtmosphereBlendPower = Shader.PropertyToID("_CelestialAtmosphereBlendPower");
        private static readonly int _ID_AtmosphereTransmittanceWeight = Shader.PropertyToID("_AtmosphereTransmittanceWeight");
        private static readonly int _ID_AtmosphereInscatterWeight = Shader.PropertyToID("_AtmosphereInscatterWeight");
        private static readonly int _ID_GameTime           = Shader.PropertyToID("_GameTime");
        private static readonly int _ID_WindDirection      = Shader.PropertyToID("_WindDirection");
        private static readonly int _ID_NightBlend         = Shader.PropertyToID("_NightBlend");
        private static readonly int _ID_SunElevation       = Shader.PropertyToID("_SunElevation");
        private static readonly int _ID_EclipseOcclusion   = Shader.PropertyToID("_EclipseOcclusion");
        private static readonly int _ID_CloudDensityThreshold = Shader.PropertyToID("_CloudDensityThreshold");
        private static readonly int _ID_CloudSoftness = Shader.PropertyToID("_CloudSoftness");
        private static readonly int _ID_CloudSpeedMult = Shader.PropertyToID("_CloudSpeedMult");
        private static readonly int _ID_CloudColorLit = Shader.PropertyToID("_CloudColorLit");
        private static readonly int _ID_CloudColorShadow = Shader.PropertyToID("_CloudColorShadow");
        private static readonly int _ID_SunsetHorizonColor = Shader.PropertyToID("_SunsetHorizonColor");
        private static readonly int _ID_SunsetCloudColor = Shader.PropertyToID("_SunsetCloudColor");
        private static readonly int _ID_NightCloudColor = Shader.PropertyToID("_NightCloudColor");
        private static readonly int _ID_AegirGlowIntensity = Shader.PropertyToID("_AegirGlowIntensity");
        private static readonly int _ID_SunDiscColor = Shader.PropertyToID("_SunDiscColor");
        private static readonly int _ID_SunScatterColor = Shader.PropertyToID("_SunScatterColor");
        private static readonly int _ID_SkyLuminanceMultiplier = Shader.PropertyToID("_SkyLuminanceMultiplier");
        private static readonly int _ID_HazeIntensity = Shader.PropertyToID("_HazeIntensity");
        private static readonly int _ID_HazeFalloff = Shader.PropertyToID("_HazeFalloff");
        private static readonly int _ID_HazeColor = Shader.PropertyToID("_HazeColor");
        private static readonly int _ID_HazeSunTintStrength = Shader.PropertyToID("_HazeSunTintStrength");
        private static readonly int _ID_HorizonMistShelfIntensity = Shader.PropertyToID("_HorizonMistShelfIntensity");
        private static readonly int _ID_HorizonMistShelfHeight = Shader.PropertyToID("_HorizonMistShelfHeight");
        private static readonly int _ID_HorizonMistShelfSoftness = Shader.PropertyToID("_HorizonMistShelfSoftness");
        private static readonly int _ID_HighCloudTex       = Shader.PropertyToID("_HighCloudTex");
        private static readonly int _ID_MainCloudAtlas     = Shader.PropertyToID("_MainCloudAtlas");
        private static readonly int _ID_MainCloudTex       = Shader.PropertyToID("_MainCloudTex");
        private static readonly int _ID_StarTex            = Shader.PropertyToID("_StarTex");
        private static readonly int _ID_MainTex            = Shader.PropertyToID("_MainTex");
        private static readonly int _ID_EmissionMap        = Shader.PropertyToID("_EmissionMap");
        private static readonly int _ID_DayCubemap         = Shader.PropertyToID("_DayCubemap");
        private static readonly int _ID_NightCubemap       = Shader.PropertyToID("_NightCubemap");
        private static readonly int _ID_DetailTex          = Shader.PropertyToID("_DetailTex");
        private static readonly int _ID_CelestialOcclusionTex = Shader.PropertyToID("_CelestialOcclusionTex");

        private static Gradient CreateDefaultDayAtmosphereGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.04f, 1.01f, 0.97f), 0f),
                    new GradientColorKey(new Color(0.98f, 1.0f, 1.02f), 0.38f),
                    new GradientColorKey(new Color(0.9f, 0.95f, 1.0f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.45f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateDefaultSunsetAtmosphereGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.12f, 0.98f, 0.92f), 0f),
                    new GradientColorKey(new Color(1.04f, 0.88f, 0.8f), 0.26f),
                    new GradientColorKey(new Color(0.8f, 0.82f, 0.9f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.35f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateDefaultNightAtmosphereGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.32f, 0.36f, 0.44f), 0f),
                    new GradientColorKey(new Color(0.15f, 0.19f, 0.26f), 0.42f),
                    new GradientColorKey(new Color(0.05f, 0.08f, 0.12f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.4f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static AnimationCurve CreateDefaultDayAtmosphereDensityCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.56f, 0f, -1.1f),
                new Keyframe(0.18f, 0.34f, -0.72f, -0.38f),
                new Keyframe(0.56f, 0.09f, -0.18f, -0.08f),
                new Keyframe(1f, 0f, 0f, 0f));
        }

        private static AnimationCurve CreateDefaultSunsetAtmosphereDensityCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.82f, 0f, -1.0f),
                new Keyframe(0.18f, 0.48f, -0.64f, -0.34f),
                new Keyframe(0.56f, 0.14f, -0.18f, -0.08f),
                new Keyframe(1f, 0.02f, 0f, 0f));
        }

        private static AnimationCurve CreateDefaultNightAtmosphereDensityCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.12f, 0f, -0.3f),
                new Keyframe(0.24f, 0.05f, -0.12f, -0.06f),
                new Keyframe(0.66f, 0.012f, -0.02f, -0.01f),
                new Keyframe(1f, 0f, 0f, 0f));
        }

        // ─────────────────────────────────────────────
        // LIFECYCLE
        // ─────────────────────────────────────────────

        private void Awake()
        {
            EnsureCelestialAtmosphereLutReady();
        }

        private void OnEnable()
        {
            ValidateReferences();
            EnsureCelestialAtmosphereLutReady();
            InitializeMaterialPropertyBlocks();
            _moonMPB ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - moon shader state bridge - owner: HectonCelestialEngine
            InitializePlanetShineLight();
            CacheCelestialTextureDefaults();
            CacheMoonRenderers();

            _accumulatedOrbitalAngle = sunStartAngle;
            _currentBacklitFactor = 0f;
            _smoothedOcclusionFactor = 0f;
            _sunOcclusionFactor = 0f;
            _baseSunIntensityCaptured = false;
            _baseSunColorCaptured = false;
            _baseFlareValuesCaptured = false;
            _eclipseRadiusCalculated = false;
            _sunDiscRendererCached = false;

            _rotationAccumulator = 0.0;
            _rotationTimer = 0f;
            _gameTime = 0f;

            _previousBlendForColors = -1f;
            _lastAppliedSkyZenith = default;
            _lastAppliedSkyHorizon = default;
            _lastAppliedSkyNadir = default;
            _editorPreviewDirty = true;

            if (sunLight != null)
            {
                _baseSunIntensity = sunLight.intensity;
                _baseSunIntensityCaptured = true;
                _baseSunColor = sunLight.color;
                _baseSunColorCaptured = true;
            }

            CaptureBaseFlareValues();
            SyncCrestPrimaryLight();

            if (blendedSkyboxMaterial != null)
                RenderSettings.skybox = blendedSkyboxMaterial;

            if (Application.isPlaying)
            {
                BiomeMatrixDirector.OnDepthTierChanged -= HandleDepthTierChanged;
                BiomeMatrixDirector.OnDepthTierChanged += HandleDepthTierChanged;
                GameTickManager tickManager = GameTickManager.Instance;
                if (tickManager != null)
                    tickManager.Register((ITickable)this);

                BiomeMatrixDirector director = BiomeMatrixDirector.ActiveRuntimeInstance;
                if (director != null)
                {
                    _currentDepthMeters = Mathf.Max(0f, director.CurrentDepthMeters);
                    UpdateDeepTextureResidencyState();
                }
                else
                {
                    _currentDepthMeters = 0f;
                    RestoreCelestialTextureDefaults();
                }
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
            _editorPreviewDirty = false;
            _surfaceAtmosphericLightingState = AtmosphericLightingState.Default;
            _currentAtmosphericLightingState = AtmosphericLightingState.Default;
            _hasAtmosphericLightingState = false;

            if (Application.isPlaying)
                BiomeMatrixDirector.OnDepthTierChanged -= HandleDepthTierChanged;

            ReleaseCelestialAtmosphereLut();
            RestoreCelestialTextureDefaults();
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
            ReleaseCelestialAtmosphereLut();
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

            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                return;

            bool sunMoved = TryConsumeEditorSunTransformChange();
            if (!_editorPreviewDirty && !sunMoved)
                return;

            Tick(0f);
            _editorPreviewDirty = false;
            SceneView.RepaintAll();
        }

        private void OnValidate()
        {
            EnsureCelestialAtmosphereLutReady();
            CacheMoonRenderers();
            _editorPreviewDirty = true;

            if (Application.isPlaying)
                return;

            InitializeMaterialPropertyBlocks();
            Tick(0f);

#if UNITY_EDITOR
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
#endif
        }

        private bool TryConsumeEditorSunTransformChange()
        {
            if (sunLight == null)
                return false;

            if (_atmosphereManager != null)
            {
                bool sunSynced = _atmosphereManager.SyncEditorPreviewFromSunTransform();
                bool previewDirty = _atmosphereManager.ConsumeEditorPreviewDirty();
                return sunSynced || previewDirty;
            }

            Transform sunTransform = sunLight.transform;
            if (sunTransform == null || !sunTransform.hasChanged)
                return false;

            SyncEditorOrbitFromSunTransform();
            sunTransform.hasChanged = false;
            return true;
        }

        private void SyncEditorOrbitFromSunTransform()
        {
            float3 orbitAxis = math.normalizesafe((float3)sunOrbitAxis, new float3(1f, 0f, 0f));
            Vector3 worldOrbitAxis = new Vector3(orbitAxis.x, orbitAxis.y, orbitAxis.z);
            Vector3 referenceForward = Vector3.ProjectOnPlane(Vector3.forward, worldOrbitAxis);
            Vector3 currentForward = Vector3.ProjectOnPlane(sunLight.transform.forward, worldOrbitAxis);

            if (referenceForward.sqrMagnitude <= 0.0001f ||
                currentForward.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            _accumulatedOrbitalAngle = Vector3.SignedAngle(
                referenceForward,
                currentForward,
                worldOrbitAxis);

            if (_accumulatedOrbitalAngle < 0f)
                _accumulatedOrbitalAngle += 360f;
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

            _gameTime += deltaTime * Mathf.Max(0f, _cloudSpeed * ResolveCloudSpeedMultiplier());

            if (!_eclipseRadiusCalculated && aegirTransform != null && playerTransform != null)
            {
                CalculateEclipseAngularRadius();
                _eclipseRadiusCalculated = true;
            }

            _cachedAegirRadius = ComputeAegirWorldRadius();

            UpdateSunPosition(deltaTime);
            ResolveSunDirection();
            SyncCrestPrimaryLight();
            UpdateSunVisualPosition();

            float sunElevation = CalculateSunElevation();
            _currentSunAngle = sunElevation;

            CalculateEclipseBacklight();
            DetectEclipse();
            UpdateSunOcclusion(deltaTime);

            UpdateSkyboxBlend(sunElevation);
            UpdateStarIntensity(sunElevation);
            ResolveSkyColors(out _resolvedSkyZenith, out _resolvedSkyHorizon, out _resolvedSkyNadir);
            UpdateDynamicCelestialAtmosphere(sunElevation, false);
            UpdateGlobalShaderData();
            PushSkyToRenderSettings();

            UpdateSkyMaterial();

            UpdateAegirMaterial();
            UpdateMoonMaterialOverrides();
            UpdatePlanetShine();
            UpdateDeepTextureResidencyState();

            // v5.1: ApplySunOcclusion is the LAST intensity writer.
            // It MULTIPLIES whatever UnderwaterVisuals wrote.
            ApplySunOcclusion();

            OnSunAngleChanged?.Invoke(_currentSunAngle);
        }

        /// <summary>
        /// Applies the tuned first-run celestial atmosphere defaults used to preserve gas giant detail across day and night.
        /// </summary>
        [ContextMenu("Apply Best Visual Defaults")]
        public void ApplyBestVisualDefaults()
        {
            ApplyBestVisualDefaultsInternal();
            EnsureCelestialAtmosphereLutReady();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// Forces an immediate celestial atmosphere LUT rebuild from the current script-owned sky authoring without waiting for sun movement.
        /// </summary>
        [ContextMenu("Manual Re-bake LUT")]
        public void ManualRebakeLut()
        {
            InvalidateCelestialAtmosphereLutCache();
            EnsureCelestialAtmosphereLutReady();
            UpdateGlobalShaderData();
            UpdateSkyMaterial();
            UpdateAegirMaterial();
            UpdateMoonMaterialOverrides();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorUtility.SetDirty(this);
#endif
        }

        // ─────────────────────────────────────────────
        // INITIALIZATION
        // ─────────────────────────────────────────────

        private void ValidateReferences()
        {
            if (sunLight == null && RenderSettings.sun != null)
                sunLight = RenderSettings.sun;

            if (sunLight == null)
                Debug.LogError("[HectonCelestialEngine] Sun Light is not assigned!", this);

            if (aegirTransform == null)
                Debug.LogError("[HectonCelestialEngine] Aegir Transform is not assigned!", this);
            else if (aegirObserverRelativeBody == null)
                aegirTransform.TryGetComponent(out aegirObserverRelativeBody);

            if (playerTransform == null)
            {
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform currentPlayer) && currentPlayer != null)
                {
                    playerTransform = currentPlayer;
                    Debug.LogWarning("[HectonCelestialEngine] Player not assigned, using SceneBootstrap player transform.");
                }
            }

            if (_skyMaterial == null)
                Debug.LogWarning("[HectonCelestialEngine] Sky Material is not assigned!", this);
        }

        private void SyncCrestPrimaryLight()
        {
            if (sunLight == null)
                return;

            if (!ReferenceEquals(RenderSettings.sun, sunLight))
                RenderSettings.sun = sunLight;

            if (_crestOceanRenderer == null)
                _crestOceanRenderer = CrestOceanRenderer.Instance;

            if (_crestOceanRenderer == null)
                return;

            if (!ReferenceEquals(_crestOceanRenderer._primaryLight, sunLight))
                _crestOceanRenderer._primaryLight = sunLight;
        }

        private void EnsureCelestialAtmosphereLutReady()
        {
            EnsureCelestialAtmosphereAuthoring();
            EnsureCelestialAtmosphereTexture();
            float sunElevation = GetCurrentSunElevationForAtmosphere();
            _currentSunAngle = sunElevation;
            UpdateSkyboxBlend(sunElevation);
            ResolveSkyColors(out _resolvedSkyZenith, out _resolvedSkyHorizon, out _resolvedSkyNadir);
            UpdateDynamicCelestialAtmosphere(sunElevation, true);
        }

        private void InvalidateCelestialAtmosphereLutCache()
        {
            _lastAtmosphereBakeSunElevation = float.PositiveInfinity;
            _lastAtmosphereBakeDayWeight = -1f;
            _lastAtmosphereBakeSunsetWeight = -1f;
            _lastAtmosphereBakeNightWeight = -1f;
            _lastAtmosphereBakeSkyZenith = default;
            _lastAtmosphereBakeSkyHorizon = default;
            _lastAtmosphereBakeSkyNadir = default;
        }

        private void EnsureCelestialAtmosphereAuthoring()
        {
            EnsureBestVisualDefaults();

            if (dayAtmosphere == null)
                dayAtmosphere = CreateDefaultDayAtmosphereGradient();

            if (sunsetAtmosphere == null)
                sunsetAtmosphere = CreateDefaultSunsetAtmosphereGradient();

            if (nightAtmosphere == null)
                nightAtmosphere = CreateDefaultNightAtmosphereGradient();

            if (dayAtmosphereDensity == null || dayAtmosphereDensity.length == 0)
                dayAtmosphereDensity = CreateDefaultDayAtmosphereDensityCurve();

            if (sunsetAtmosphereDensity == null || sunsetAtmosphereDensity.length == 0)
                sunsetAtmosphereDensity = CreateDefaultSunsetAtmosphereDensityCurve();

            if (nightAtmosphereDensity == null || nightAtmosphereDensity.length == 0)
                nightAtmosphereDensity = CreateDefaultNightAtmosphereDensityCurve();
        }

        private void EnsureBestVisualDefaults()
        {
            if (_visualDefaultsVersion >= BestVisualDefaultsVersion)
                return;

            ApplyBestVisualDefaultsInternal();
        }

        private void ApplyBestVisualDefaultsInternal()
        {
            if (_visualDefaultsVersion < 3)
            {
                horizonDensity = 1.1f;
                zenithTransparency = 0.84f;
                atmosphereBlendPower = 1.65f;
                _surfaceFogDensityMultiplier = 1.35f;
                _surfaceSkyHazeIntensityMultiplier = 1.2f;
                _surfaceHazeSkyTintInfluence = 0.12f;
                _atmosphereTransmittanceWeight = 0.92f;
                _atmosphereInscatterWeight = 0.78f;
                _moonAtmosphereTransmittanceMultiplier = 0.78f;
                _moonAtmosphereInscatterMultiplier = 0.42f;

                _horizonBrightnessScale = 0.7f;
                _horizonZenithBlend = 0.22f;

                dayAtmosphere = CreateDefaultDayAtmosphereGradient();
                sunsetAtmosphere = CreateDefaultSunsetAtmosphereGradient();
                nightAtmosphere = CreateDefaultNightAtmosphereGradient();

                dayAtmosphereDensity = CreateDefaultDayAtmosphereDensityCurve();
                sunsetAtmosphereDensity = CreateDefaultSunsetAtmosphereDensityCurve();
                nightAtmosphereDensity = CreateDefaultNightAtmosphereDensityCurve();

                dayAtmosphereDensityScale = 1f;
                sunsetAtmosphereDensityScale = 0.78f;
                nightAtmosphereDensityScale = 0.16f;

                dayAtmosphereExposure = 1.02f;
                sunsetAtmosphereExposure = 0.58f;
                nightAtmosphereExposure = NightAtmosphereInscatterFloor;

                sunsetAtmosphereBandDegrees = 16f;
                nightAtmosphereTransitionDegrees = 10f;
                atmosphereLutRebuildSunAngleThreshold = 0.35f;
            }

            if (_visualDefaultsVersion < 4)
            {
                _surfaceFogManualColorBlend = 0f;
                _surfaceFogManualColor = new Color(0.66f, 0.71f, 0.77f, 1f);
                _surfaceFogSkyColorInfluence = 0.32f;
                _surfaceFogAmbientColorInfluence = 0.22f;
                _surfaceHazeHorizonSpread = 1.35f;
            }

            if (_visualDefaultsVersion < 5)
            {
                _surfaceHorizonMistShelfIntensity = 1f;
                _surfaceHorizonMistShelfHeight = 0.16f;
                _surfaceHorizonMistShelfSoftness = 0.1f;
            }

            _visualDefaultsVersion = BestVisualDefaultsVersion;
        }

        private void EnsureCelestialAtmosphereTexture()
        {
            if (_celestialAtmosphereLutTexture != null)
                return;

            TextureFormat textureFormat = SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf)
                ? TextureFormat.RGBAHalf
                : TextureFormat.RGBAFloat;

            _celestialAtmosphereLutTexture = new Texture2D(
                CelestialAtmosphereLutResolution,
                1,
                textureFormat,
                false,
                true)
            {
                name = "HectonCelestialAtmosphereLUT",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private void UpdateDynamicCelestialAtmosphere(float sunElevation, bool forceRebuild)
        {
            EnsureCelestialAtmosphereAuthoring();
            EnsureCelestialAtmosphereTexture();

            EvaluateCelestialAtmosphereProfileWeights(
                sunElevation,
                out float dayWeight,
                out float sunsetWeight,
                out float nightWeight);

            float blendedExposure =
                dayWeight * dayAtmosphereExposure +
                sunsetWeight * sunsetAtmosphereExposure +
                nightWeight * nightAtmosphereExposure;

            bool sunMovedEnough = float.IsPositiveInfinity(_lastAtmosphereBakeSunElevation) ||
                                  Mathf.Abs(Mathf.DeltaAngle(_lastAtmosphereBakeSunElevation, sunElevation)) >= atmosphereLutRebuildSunAngleThreshold;

            bool profileShifted = Mathf.Abs(dayWeight - _lastAtmosphereBakeDayWeight) >= 0.01f ||
                                  Mathf.Abs(sunsetWeight - _lastAtmosphereBakeSunsetWeight) >= 0.01f ||
                                  Mathf.Abs(nightWeight - _lastAtmosphereBakeNightWeight) >= 0.01f;

            bool skyShifted = HasMeaningfulColorShift(_resolvedSkyZenith, _lastAtmosphereBakeSkyZenith) ||
                              HasMeaningfulColorShift(_resolvedSkyHorizon, _lastAtmosphereBakeSkyHorizon) ||
                              HasMeaningfulColorShift(_resolvedSkyNadir, _lastAtmosphereBakeSkyNadir);

            _currentAtmosphereExposure = Mathf.Max(0f, blendedExposure);

            if (!forceRebuild && !sunMovedEnough && !profileShifted && !skyShifted)
                return;

            RebuildCelestialAtmosphereLut(dayWeight, sunsetWeight, nightWeight);
            _lastAtmosphereBakeSunElevation = sunElevation;
            _lastAtmosphereBakeDayWeight = dayWeight;
            _lastAtmosphereBakeSunsetWeight = sunsetWeight;
            _lastAtmosphereBakeNightWeight = nightWeight;
            _lastAtmosphereBakeSkyZenith = _resolvedSkyZenith;
            _lastAtmosphereBakeSkyHorizon = _resolvedSkyHorizon;
            _lastAtmosphereBakeSkyNadir = _resolvedSkyNadir;
        }

        private void RebuildCelestialAtmosphereLut(float dayWeight, float sunsetWeight, float nightWeight)
        {
            if (_celestialAtmosphereLutTexture == null)
                return;

            for (int i = 0; i < CelestialAtmosphereLutResolution; i++)
            {
                float t = CelestialAtmosphereLutResolution > 1
                    ? (float)i / (CelestialAtmosphereLutResolution - 1)
                    : 0f;

                float sample01 = EvaluateAtmosphereBlend01(t);
                Color profileColor = EvaluateAtmosphereGradientColor(
                    sample01,
                    dayWeight,
                    sunsetWeight,
                    nightWeight);

                Color skyGradient = EvaluateSkySourceGradientColor(sample01);
                Color lutColor = MultiplyRgb(skyGradient, profileColor);
                lutColor.a = EvaluateAtmosphereTransmittance(
                    sample01,
                    dayWeight,
                    sunsetWeight,
                    nightWeight);
                _celestialAtmosphereLutPixels[i] = lutColor;
            }

            _celestialAtmosphereLutTexture.SetPixels(_celestialAtmosphereLutPixels, 0);
            _celestialAtmosphereLutTexture.Apply(false, false);
            PublishCelestialAtmosphereLut();
        }

        private Color EvaluateAtmosphereGradientColor(
            float t,
            float dayWeight,
            float sunsetWeight,
            float nightWeight)
        {
            Color color =
                dayAtmosphere.Evaluate(t) * dayWeight +
                sunsetAtmosphere.Evaluate(t) * sunsetWeight +
                nightAtmosphere.Evaluate(t) * nightWeight;

            color.a = 1f;
            return color;
        }

        private float EvaluateAtmosphereTransmittance(
            float t,
            float dayWeight,
            float sunsetWeight,
            float nightWeight)
        {
            float density =
                dayAtmosphereDensity.Evaluate(t) * dayWeight * dayAtmosphereDensityScale +
                sunsetAtmosphereDensity.Evaluate(t) * sunsetWeight * sunsetAtmosphereDensityScale +
                nightAtmosphereDensity.Evaluate(t) * nightWeight * nightAtmosphereDensityScale;

            float zenithDensityScale = Mathf.Lerp(1f, 0.05f, zenithTransparency);
            float altitudeDensityScale = Mathf.Lerp(
                Mathf.Max(0f, horizonDensity),
                zenithDensityScale,
                t);
            density *= altitudeDensityScale;

            return 1f - Mathf.Clamp01(density);
        }

        private float EvaluateAtmosphereBlend01(float t)
        {
            float clamped = Mathf.Clamp01(t);
            return Mathf.Pow(clamped, Mathf.Max(0.01f, atmosphereBlendPower));
        }

        private Color EvaluateSkySourceGradientColor(float t)
        {
            Color color = Color.Lerp(_resolvedSkyHorizon, _resolvedSkyZenith, t);
            color.a = 1f;
            return color;
        }

        private static Color MultiplyRgb(Color lhs, Color rhs)
        {
            return new Color(
                lhs.r * rhs.r,
                lhs.g * rhs.g,
                lhs.b * rhs.b,
                1f);
        }

        private void EvaluateCelestialAtmosphereProfileWeights(
            float sunElevation,
            out float dayWeight,
            out float sunsetWeight,
            out float nightWeight)
        {
            float safeSunsetBand = Mathf.Max(1f, sunsetAtmosphereBandDegrees);
            float safeNightTransition = Mathf.Max(1f, nightAtmosphereTransitionDegrees);
            float safeTwilightStart = Mathf.Max(0.01f, twilightStartAngle);

            float sunsetWindowT = 1f - Mathf.Clamp01(Mathf.Abs(sunElevation) / safeSunsetBand);
            float twilightWeight = Mathf.SmoothStep(0f, 1f, sunsetWindowT);

            float dayBase = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(sunElevation / safeTwilightStart));

            float nightBase = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01((twilightEndAngle - sunElevation) / safeNightTransition));

            dayWeight = dayBase * (1f - twilightWeight);
            sunsetWeight = twilightWeight * (1f - nightBase * 0.85f);
            nightWeight = nightBase * (1f - twilightWeight);

            float totalWeight = dayWeight + sunsetWeight + nightWeight;
            if (totalWeight <= 0.0001f)
            {
                if (sunElevation > twilightStartAngle)
                {
                    dayWeight = 1f;
                    sunsetWeight = 0f;
                    nightWeight = 0f;
                }
                else if (sunElevation < twilightEndAngle)
                {
                    dayWeight = 0f;
                    sunsetWeight = 0f;
                    nightWeight = 1f;
                }
                else
                {
                    dayWeight = 0f;
                    sunsetWeight = 1f;
                    nightWeight = 0f;
                }
                return;
            }

            float invTotalWeight = 1f / totalWeight;
            dayWeight *= invTotalWeight;
            sunsetWeight *= invTotalWeight;
            nightWeight *= invTotalWeight;
        }

        private float GetCurrentSunElevationForAtmosphere()
        {
            float3 sunDirection = _resolvedSunDirection;
            if (math.lengthsq(sunDirection) <= 0.0001f && sunLight != null)
                sunDirection = -(float3)sunLight.transform.forward;

            float sinElevation = math.dot(math.normalizesafe(sunDirection, new float3(0f, 1f, 0f)), new float3(0f, 1f, 0f));
            return math.degrees(math.asin(math.clamp(sinElevation, -1f, 1f)));
        }

        private static bool HasMeaningfulColorShift(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) >= 0.01f ||
                   Mathf.Abs(a.g - b.g) >= 0.01f ||
                   Mathf.Abs(a.b - b.b) >= 0.01f;
        }

        private static float ComputePerceivedLuminance(Color color)
        {
            return color.r * 0.2126f +
                   color.g * 0.7152f +
                   color.b * 0.0722f;
        }

        private static Color DesaturateColor(Color color, float amount)
        {
            float luminance = ComputePerceivedLuminance(color);
            Color grayscale = new Color(luminance, luminance, luminance, 1f);
            Color result = Color.Lerp(color, grayscale, Mathf.Clamp01(amount));
            result.a = 1f;
            return result;
        }

        private static Color LiftColorTowardsLuminance(Color color, float targetLuminance, float maxWhiteBlend)
        {
            float currentLuminance = ComputePerceivedLuminance(color);
            if (currentLuminance >= targetLuminance)
            {
                color.a = 1f;
                return color;
            }

            float safeTarget = Mathf.Max(targetLuminance, 0.0001f);
            float liftFactor = Mathf.Clamp01((safeTarget - currentLuminance) / safeTarget);
            Color lifted = Color.Lerp(color, Color.white, liftFactor * Mathf.Clamp01(maxWhiteBlend));
            lifted.a = 1f;
            return lifted;
        }

        private static Color NormalizeColorToMax(Color color)
        {
            float maxComponent = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            if (maxComponent <= 0.0001f)
                return Color.white;

            Color normalized = new Color(
                color.r / maxComponent,
                color.g / maxComponent,
                color.b / maxComponent,
                1f);
            return normalized;
        }

        private static bool HasUsableSurfaceColor(Color color)
        {
            return ComputePerceivedLuminance(color) > 0.001f;
        }

        private float ResolveSurfaceSkyExposure()
        {
            if (_atmosphereManager != null)
            {
                float exposure = _atmosphereManager.CurrentSkyExposure;
                if (exposure > 0.001f)
                    return exposure;
            }

            float zenithLuminance = ComputePerceivedLuminance(_resolvedSkyZenith);
            float horizonLuminance = ComputePerceivedLuminance(_resolvedSkyHorizon);
            float nadirLuminance = ComputePerceivedLuminance(_resolvedSkyNadir);
            float blendedLuminance = Mathf.Max(
                0.16f,
                zenithLuminance * 0.42f +
                horizonLuminance * 0.46f +
                nadirLuminance * 0.12f);
            return Mathf.Clamp(blendedLuminance * 1.85f, 0.28f, 1.8f);
        }

        private void PublishCelestialAtmosphereLut()
        {
            if (_celestialAtmosphereLutTexture != null)
            {
                Shader.SetGlobalTexture(_ID_CelestialAtmosphereLut, _celestialAtmosphereLutTexture);
                Shader.SetGlobalFloat(_ID_CelestialAtmosphereLutReady, 1f);
                Shader.SetGlobalFloat(_ID_AtmosphereExposure, _currentAtmosphereExposure);
                Shader.SetGlobalFloat(_ID_CelestialHorizonDensity, horizonDensity);
                Shader.SetGlobalFloat(_ID_CelestialZenithTransparency, zenithTransparency);
                Shader.SetGlobalFloat(_ID_CelestialAtmosphereBlendPower, atmosphereBlendPower);
                PushSkyToRenderSettings();
            }
        }

        private void ReleaseCelestialAtmosphereLut()
        {
            if (_celestialAtmosphereLutTexture == null)
                return;

            Shader.SetGlobalTexture(_ID_CelestialAtmosphereLut, Texture2D.blackTexture);
            Shader.SetGlobalFloat(_ID_CelestialAtmosphereLutReady, 0f);
            Shader.SetGlobalFloat(_ID_AtmosphereExposure, 0f);
            Shader.SetGlobalFloat(_ID_CelestialHorizonDensity, 0f);
            Shader.SetGlobalFloat(_ID_CelestialZenithTransparency, 0f);
            Shader.SetGlobalFloat(_ID_CelestialAtmosphereBlendPower, 1f);

            if (Application.isPlaying)
                Destroy(_celestialAtmosphereLutTexture);
            else
                DestroyImmediate(_celestialAtmosphereLutTexture);

            _celestialAtmosphereLutTexture = null;
            _lastAtmosphereBakeSunElevation = float.PositiveInfinity;
            _lastAtmosphereBakeDayWeight = -1f;
            _lastAtmosphereBakeSunsetWeight = -1f;
            _lastAtmosphereBakeNightWeight = -1f;
            _lastAtmosphereBakeSkyZenith = default;
            _lastAtmosphereBakeSkyHorizon = default;
            _lastAtmosphereBakeSkyNadir = default;
        }

        private void PushSkyToRenderSettings()
        {
            AtmosphericLightingState state = BuildSurfaceAtmosphericLightingState();
            ApplySurfaceAtmosphericLightingState(state);
        }

        private AtmosphericLightingState BuildSurfaceAtmosphericLightingState()
        {
            EvaluateCelestialAtmosphereProfileWeights(
                _currentSunAngle,
                out float dayWeight,
                out float sunsetWeight,
                out float nightWeight);

            Color skyHorizonColor = _resolvedSkyHorizon;
            skyHorizonColor.a = 1f;
            Color skyZenithColor = _resolvedSkyZenith;
            skyZenithColor.a = 1f;
            Color skyNadirColor = _resolvedSkyNadir;
            skyNadirColor.a = 1f;

            Color skyFogAnchor = Color.Lerp(skyHorizonColor, skyZenithColor, 0.18f);
            skyFogAnchor = Color.Lerp(skyFogAnchor, skyNadirColor, 0.08f);

            Color atmosphereFogColor = _atmosphereManager != null
                ? _atmosphereManager.CurrentFogColor
                : skyFogAnchor;
            if (!HasUsableSurfaceColor(atmosphereFogColor))
                atmosphereFogColor = skyFogAnchor;
            atmosphereFogColor.a = 1f;

            float horizonTransmittance = EvaluateAtmosphereTransmittance(
                0f,
                dayWeight,
                sunsetWeight,
                nightWeight);
            float horizonHaze = 1f - Mathf.Clamp01(horizonTransmittance);
            float lowSunFactor = 1f - Mathf.Clamp01((_currentSunAngle + 8f) / 88f);
            float hazeResponse = Mathf.Clamp01(horizonHaze * 0.72f + lowSunFactor * 0.28f);
            Color ambientBaseColor = ResolveSurfaceAmbientBaseColor();
            Color horizonSkyTint = Color.Lerp(skyHorizonColor, skyZenithColor, 0.14f);
            horizonSkyTint = Color.Lerp(horizonSkyTint, skyNadirColor, 0.05f);
            if (_celestialAtmosphereLutPixels.Length > 0)
            {
                horizonSkyTint = Color.Lerp(horizonSkyTint, _celestialAtmosphereLutPixels[0], 0.14f);
                horizonTransmittance = Mathf.Clamp01(_celestialAtmosphereLutPixels[0].a);
                horizonHaze = 1f - Mathf.Clamp01(horizonTransmittance);
                hazeResponse = Mathf.Clamp01(horizonHaze * 0.72f + lowSunFactor * 0.28f);
            }
            horizonSkyTint.a = 1f;

            float baseFogDensity = ResolveSurfaceBaseFogDensity();
            float dayVisibility = Mathf.Clamp01((_currentSunAngle + 2f) / 64f);
            float middayFogReduction = Mathf.Lerp(1.08f, 0.82f, dayVisibility);
            float fogDensity = Mathf.Max(
                0.0001f,
                baseFogDensity *
                Mathf.Lerp(0.82f, 1.28f, hazeResponse) *
                middayFogReduction *
                Mathf.Max(0.25f, _surfaceFogDensityMultiplier));

            Color fogOwnerColor = _surfaceWeatherFogOverrideActive
                ? _surfaceWeatherFogColor
                : Color.Lerp(
                    atmosphereFogColor,
                    _surfaceFogManualColor,
                    Mathf.Clamp01(_surfaceFogManualColorBlend));
            fogOwnerColor.a = 1f;

            float skyTintWeight =
                Mathf.Lerp(0.06f, 0.18f, hazeResponse) * Mathf.Clamp01(_surfaceFogSkyColorInfluence) +
                Mathf.Lerp(0.02f, 0.18f, hazeResponse) * _surfaceHazeSkyTintInfluence;
            skyTintWeight = Mathf.Lerp(skyTintWeight, skyTintWeight * 1.22f, sunsetWeight);
            skyTintWeight = Mathf.Lerp(skyTintWeight, skyTintWeight * 0.82f, nightWeight);
            skyTintWeight = Mathf.Clamp01(skyTintWeight);

            float ambientWeight = Mathf.Lerp(0.08f, 0.24f, 1f - dayVisibility) *
                                  Mathf.Clamp01(_surfaceFogAmbientColorInfluence);

            Color horizonFogColor = Color.Lerp(fogOwnerColor, horizonSkyTint, skyTintWeight);
            horizonFogColor = Color.Lerp(horizonFogColor, ambientBaseColor, ambientWeight);
            float atmosphereRestoreWeight =
                (1f - Mathf.Clamp01(_surfaceFogManualColorBlend)) *
                Mathf.Lerp(0.18f, 0.36f, hazeResponse);
            horizonFogColor = Color.Lerp(horizonFogColor, atmosphereFogColor, atmosphereRestoreWeight);
            float fogTargetLuminance = Mathf.Max(
                ComputePerceivedLuminance(fogOwnerColor) * Mathf.Lerp(1f, 0.88f, hazeResponse),
                ComputePerceivedLuminance(horizonSkyTint),
                ComputePerceivedLuminance(skyZenithColor) * Mathf.Lerp(0.42f, 0.58f, dayVisibility));
            horizonFogColor = LiftColorTowardsLuminance(
                horizonFogColor,
                fogTargetLuminance,
                Mathf.Lerp(0.22f, 0.38f, dayVisibility));
            horizonFogColor = DesaturateColor(
                horizonFogColor,
                Mathf.Lerp(0.14f, 0.22f, dayWeight) + hazeResponse * 0.04f);
            horizonFogColor.a = 1f;

            float hazeSpread = Mathf.Max(0.5f, _surfaceHazeHorizonSpread);
            float hazeIntensity = Mathf.Lerp(0.12f, 0.34f, hazeResponse) *
                                  Mathf.Max(0.25f, _surfaceSkyHazeIntensityMultiplier);
            hazeIntensity *= Mathf.Lerp(1f, 1f + (hazeSpread - 1f) * 0.35f, hazeResponse);
            hazeIntensity = Mathf.Lerp(hazeIntensity, hazeIntensity * 1.18f, sunsetWeight);
            hazeIntensity = Mathf.Lerp(hazeIntensity, hazeIntensity * 0.42f, nightWeight);

            float hazeFalloff = Mathf.Lerp(6.1f, 3.8f, hazeResponse) / hazeSpread;
            hazeFalloff = Mathf.Lerp(hazeFalloff, hazeFalloff * 0.9f, sunsetWeight);
            hazeFalloff = Mathf.Lerp(hazeFalloff, hazeFalloff * 1.15f, nightWeight);
            hazeFalloff = Mathf.Clamp(hazeFalloff, 1.35f, 8f);

            Color hazeColor = Color.Lerp(
                horizonFogColor,
                horizonSkyTint,
                Mathf.Clamp01(0.08f + _surfaceHazeSkyTintInfluence * 0.38f));
            hazeColor = Color.Lerp(hazeColor, fogOwnerColor, 0.38f);
            hazeColor = Color.Lerp(hazeColor, ResolveScriptSunsetHorizonColor(), sunsetWeight * 0.16f);
            hazeColor = LiftColorTowardsLuminance(
                hazeColor,
                fogTargetLuminance,
                Mathf.Lerp(0.24f, 0.42f, dayVisibility));
            hazeColor = DesaturateColor(hazeColor, Mathf.Lerp(0.18f, 0.28f, dayWeight));
            hazeColor.a = 1f;

            float hazeSunTintStrength = Mathf.Lerp(0.1f, 0.3f, hazeResponse);
            hazeSunTintStrength = Mathf.Lerp(hazeSunTintStrength, hazeSunTintStrength * 1.25f, sunsetWeight);
            hazeSunTintStrength = Mathf.Lerp(hazeSunTintStrength, hazeSunTintStrength * 0.6f, nightWeight);

            float mistShelfIntensity = Mathf.Lerp(0.22f, 0.56f, hazeResponse) *
                                       Mathf.Clamp(_surfaceHorizonMistShelfIntensity, 0f, 2f);
            mistShelfIntensity *= Mathf.Lerp(1f, 1f + (hazeSpread - 1f) * 0.24f, hazeResponse);
            mistShelfIntensity = Mathf.Lerp(mistShelfIntensity, mistShelfIntensity * 1.12f, sunsetWeight);
            mistShelfIntensity = Mathf.Lerp(mistShelfIntensity, mistShelfIntensity * 0.34f, nightWeight);
            mistShelfIntensity = Mathf.Clamp(mistShelfIntensity, 0f, 2f);

            float mistShelfHeight = _surfaceHorizonMistShelfHeight *
                                    Mathf.Lerp(0.92f, 1.18f, hazeResponse);
            mistShelfHeight = Mathf.Clamp(mistShelfHeight, 0.04f, 0.32f);

            float mistShelfSoftness = _surfaceHorizonMistShelfSoftness *
                                      Mathf.Lerp(0.9f, 1.12f, hazeResponse);
            mistShelfSoftness = Mathf.Clamp(mistShelfSoftness, 0.02f, 0.24f);

            Color ambientSkyColor = Color.Lerp(ambientBaseColor, skyZenithColor, 0.7f);
            Color ambientEquatorColor = Color.Lerp(ambientBaseColor, horizonFogColor, 0.62f);
            Color ambientGroundColor = Color.Lerp(skyNadirColor, ambientEquatorColor, 0.46f);
            ambientSkyColor.a = 1f;
            ambientEquatorColor.a = 1f;
            ambientGroundColor.a = 1f;

            float exposureBase = ResolveSurfaceSkyExposure();
            float skyLuminanceMultiplier = Mathf.Max(0.35f, ResolveSkyLuminanceMultiplier());
            float horizonBrightness = Mathf.Max(
                ComputePerceivedLuminance(skyHorizonColor),
                ComputePerceivedLuminance(horizonFogColor));
            float ambientBrightnessLift = Mathf.Lerp(0.82f, 1.26f, dayVisibility);
            ambientBrightnessLift *= Mathf.Lerp(0.86f, 1.08f, Mathf.Clamp01(horizonBrightness * 1.35f));
            float ambientIntensity = Mathf.Clamp(
                Mathf.Max(0.24f, exposureBase) *
                skyLuminanceMultiplier *
                ambientBrightnessLift *
                Mathf.Lerp(0.9f, 1.08f, 1f - nightWeight * 0.2f),
                0.24f,
                2.4f);
            float sunIntensityMultiplier = ResolveSurfaceSunMultiplier();
            float directionalLightIntensity = ResolveSurfaceDirectionalLightIntensity(sunIntensityMultiplier);

            return new AtmosphericLightingState
            {
                IsValid = true,
                SunElevationDegrees = _currentSunAngle,
                SkyExposure = Mathf.Max(0f, exposureBase),
                FogDensity = fogDensity,
                AmbientIntensity = ambientIntensity,
                SunIntensityMultiplier = sunIntensityMultiplier,
                DirectionalLightIntensity = directionalLightIntensity,
                HorizonHazeIntensity = hazeIntensity,
                HorizonHazeFalloff = hazeFalloff,
                HorizonHazeSunTintStrength = hazeSunTintStrength,
                HorizonMistShelfIntensity = mistShelfIntensity,
                HorizonMistShelfHeight = mistShelfHeight,
                HorizonMistShelfSoftness = mistShelfSoftness,
                SkyZenithColor = _resolvedSkyZenith,
                SkyHorizonColor = _resolvedSkyHorizon,
                SkyNadirColor = _resolvedSkyNadir,
                FogColor = horizonFogColor,
                HorizonHazeColor = hazeColor,
                AmbientSkyColor = ambientSkyColor,
                AmbientEquatorColor = ambientEquatorColor,
                AmbientGroundColor = ambientGroundColor,
                DirectionalLightColor = ResolveSurfaceSunLightColor(horizonFogColor, sunsetWeight, horizonHaze)
            };
        }

        private void ApplySurfaceAtmosphericLightingState(AtmosphericLightingState state)
        {
            _surfaceAtmosphericLightingState = state;
            _currentAtmosphericLightingState = state;
            _hasAtmosphericLightingState = state.IsValid;

            if (!state.IsValid)
                return;

            if (!RenderSettings.fog || RenderSettings.fogMode != FogMode.ExponentialSquared)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
            }

            if (HasMeaningfulColorShift(state.FogColor, RenderSettings.fogColor))
                RenderSettings.fogColor = state.FogColor;

            if (Mathf.Abs(RenderSettings.fogDensity - state.FogDensity) >= 0.0001f)
                RenderSettings.fogDensity = state.FogDensity;

            if (RenderSettings.ambientMode != AmbientMode.Trilight)
                RenderSettings.ambientMode = AmbientMode.Trilight;

            if (HasMeaningfulColorShift(state.AmbientSkyColor, RenderSettings.ambientSkyColor))
                RenderSettings.ambientSkyColor = state.AmbientSkyColor;

            if (HasMeaningfulColorShift(state.AmbientEquatorColor, RenderSettings.ambientEquatorColor))
                RenderSettings.ambientEquatorColor = state.AmbientEquatorColor;

            if (HasMeaningfulColorShift(state.AmbientGroundColor, RenderSettings.ambientGroundColor))
                RenderSettings.ambientGroundColor = state.AmbientGroundColor;

            if (Mathf.Abs(RenderSettings.ambientIntensity - state.AmbientIntensity) >= 0.0001f)
                RenderSettings.ambientIntensity = state.AmbientIntensity;

            if (sunLight == null)
                return;

            bool allowSurfaceDirectionalLight =
                HectonUnderwaterVisuals.ActiveRuntimeInstance == null ||
                !HectonUnderwaterVisuals.ActiveRuntimeInstance.IsUnderwater;

            if (!allowSurfaceDirectionalLight)
                return;

            if (_baseSunColorCaptured && HasMeaningfulColorShift(state.DirectionalLightColor, sunLight.color))
                sunLight.color = state.DirectionalLightColor;

            if (Mathf.Abs(sunLight.intensity - state.DirectionalLightIntensity) >= 0.0001f)
                sunLight.intensity = state.DirectionalLightIntensity;
        }

        private float ResolveSurfaceBaseFogDensity()
        {
            if (_surfaceWeatherFogOverrideActive)
                return Mathf.Max(0.0001f, _surfaceWeatherFogDensity);

            if (_atmosphereManager != null)
            {
                float fogDensity = _atmosphereManager.CurrentFogDensity;
                if (fogDensity > 0.0001f)
                    return fogDensity;
            }

            float densityFromHaze = Mathf.Lerp(0.0011f, 0.0036f, Mathf.Clamp01(horizonDensity / 4f));
            return densityFromHaze;
        }

        private Color ResolveSurfaceAmbientBaseColor()
        {
            Color skyAmbientAnchor = Color.Lerp(_resolvedSkyZenith, _resolvedSkyHorizon, 0.34f);
            skyAmbientAnchor = Color.Lerp(skyAmbientAnchor, _resolvedSkyNadir, 0.12f);

            Color atmosphereAmbient = _atmosphereManager != null
                ? _atmosphereManager.CurrentAmbientColor
                : skyAmbientAnchor;
            if (!HasUsableSurfaceColor(atmosphereAmbient))
                atmosphereAmbient = skyAmbientAnchor;

            Color ambientBase = Color.Lerp(skyAmbientAnchor, atmosphereAmbient, 0.55f);
            if (_surfaceWeatherFogOverrideActive)
                ambientBase = Color.Lerp(ambientBase, _surfaceWeatherAmbientColor, 0.24f);
            ambientBase.a = 1f;
            return ambientBase;
        }

        private float ResolveSurfaceSunMultiplier()
        {
            return _surfaceWeatherFogOverrideActive
                ? Mathf.Max(0f, _surfaceWeatherSunMultiplier)
                : 1f;
        }

        private float ResolveFallbackSurfaceSunIntensity()
        {
            EvaluateCelestialAtmosphereProfileWeights(
                _currentSunAngle,
                out float dayWeight,
                out float sunsetWeight,
                out float nightWeight);

            float dayIntensity = _baseSunIntensityCaptured && _baseSunIntensity > 0.0001f
                ? _baseSunIntensity
                : 1.55f;
            float sunsetIntensity = Mathf.Max(0.32f, dayIntensity * 0.72f);
            float nightIntensity = 0.08f;

            return Mathf.Max(
                0f,
                dayWeight * dayIntensity +
                sunsetWeight * sunsetIntensity +
                nightWeight * nightIntensity);
        }

        private float ResolveSurfaceDirectionalLightIntensity(float sunMultiplier)
        {
            if (_atmosphereManager != null)
            {
                float computedIntensity = Mathf.Max(
                    _atmosphereManager.CurrentSunIntensity,
                    _atmosphereManager.ProfileSunIntensity * _atmosphereManager.ComputedHorizonFade);
                if (computedIntensity <= 0.0001f)
                    computedIntensity = ResolveFallbackSurfaceSunIntensity();
                if (computedIntensity <= 0.0001f)
                    computedIntensity = 1f;
                return Mathf.Max(
                    0f,
                    computedIntensity *
                    sunMultiplier);
            }

            if (sunLight != null)
                return Mathf.Max(0f, sunLight.intensity * sunMultiplier);

            return sunMultiplier;
        }

        private Color ResolveSurfaceSunLightColor(Color horizonFogColor, float sunsetWeight, float horizonHaze)
        {
            Color baseSunColor = _baseSunColorCaptured ? NormalizeColorToMax(_baseSunColor) : Color.white;
            Color daylightColor = Color.Lerp(Color.white, baseSunColor, 0.22f);
            Color sunsetTint = ResolveScriptSunsetHorizonColor();
            Color skyLightTint = Color.Lerp(_resolvedSkyHorizon, horizonFogColor, 0.3f);
            Color sunColor = Color.Lerp(
                daylightColor,
                sunsetTint,
                Mathf.Clamp01(sunsetWeight * 0.85f));
            sunColor = Color.Lerp(
                sunColor,
                skyLightTint,
                Mathf.Clamp01(horizonHaze * 0.08f));
            sunColor.a = 1f;
            return sunColor;
        }

        private void InitializeMaterialPropertyBlocks()
        {
            _aegirMPB = new MaterialPropertyBlock();   // COLD ALLOC: MaterialPropertyBlock[1] — gas giant shader state bridge — owner: HectonCelestialEngine
            _sunDiscMPB = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — sun-disc shader state bridge — owner: HectonCelestialEngine
        }

        private void CacheMoonRenderers()
        {
            _observerBodyCache.Clear();
            _moonRenderers.Clear();

            Transform searchRoot = aegirTransform != null
                ? aegirTransform.root
                : transform.root;
            if (searchRoot == null)
                return;

            searchRoot.GetComponentsInChildren(true, _observerBodyCache);
            for (int i = 0; i < _observerBodyCache.Count; i++)
            {
                ObserverRelativeCelestialBody body = _observerBodyCache[i];
                if (body == null || body == aegirObserverRelativeBody)
                    continue;

                if (!body.TryGetComponent(out Renderer renderer) || renderer == null || renderer == aegirRenderer)
                    continue;

                Material sharedMaterial = renderer.sharedMaterial;
                if (sharedMaterial == null || sharedMaterial.shader == null)
                    continue;

                if (!sharedMaterial.HasProperty(_ID_AtmosphereTransmittanceWeight) ||
                    !sharedMaterial.HasProperty(_ID_AtmosphereInscatterWeight))
                {
                    continue;
                }

                if (!string.Equals(sharedMaterial.shader.name, "HECTON/Celestial/Hecton_CelestialMoon", StringComparison.Ordinal))
                    continue;

                _moonRenderers.Add(renderer);
            }
        }

        private void CacheCelestialTextureDefaults()
        {
            _aegirSharedMaterial = aegirRenderer != null ? aegirRenderer.sharedMaterial : null;

            _skyHighCloudTexDefault = GetMaterialTexture(_skyMaterial, _ID_HighCloudTex);
            _skyMainCloudAtlasDefault = GetMaterialTexture(_skyMaterial, _ID_MainCloudAtlas);
            _skyMainCloudTexDefault = GetMaterialTexture(_skyMaterial, _ID_MainCloudTex);
            _skyStarTexDefault = GetMaterialTexture(_skyMaterial, _ID_StarTex);

            _daySkyboxMainTexDefault = GetMaterialTexture(daySkybox, _ID_MainTex);
            _daySkyboxEmissionTexDefault = GetMaterialTexture(daySkybox, _ID_EmissionMap);
            _nightSkyboxMainTexDefault = GetMaterialTexture(nightSkybox, _ID_MainTex);
            _nightSkyboxEmissionTexDefault = GetMaterialTexture(nightSkybox, _ID_EmissionMap);

            _blendedDayCubemapDefault = GetMaterialTexture(blendedSkyboxMaterial, _ID_DayCubemap);
            _blendedNightCubemapDefault = GetMaterialTexture(blendedSkyboxMaterial, _ID_NightCubemap);

            _aegirMainTexDefault = GetMaterialTexture(_aegirSharedMaterial, _ID_MainTex);
            _aegirDetailTexDefault = GetMaterialTexture(_aegirSharedMaterial, _ID_DetailTex);
            _aegirEmissionMapDefault = GetMaterialTexture(_aegirSharedMaterial, _ID_EmissionMap);
            _aegirCelestialOcclusionTexDefault = GetMaterialTexture(_aegirSharedMaterial, _ID_CelestialOcclusionTex);

            CacheSkyWeatherDefaults();
        }

        private void CacheSkyWeatherDefaults()
        {
            if (_skyMaterial == null || _cachedSkyWeatherDefaults)
                return;

            _defaultCloudDensityThreshold = GetMaterialFloat(_skyMaterial, _ID_CloudDensityThreshold, 0.2f);
            _defaultCloudSoftness = GetMaterialFloat(_skyMaterial, _ID_CloudSoftness, 0.28f);
            _defaultCloudSpeedMultiplier = GetMaterialFloat(_skyMaterial, _ID_CloudSpeedMult, 0.3f);
            _defaultWindDirection = GetMaterialVector(_skyMaterial, _ID_WindDirection, new Vector4(1f, 0.2f, 0f, 0f));
            _defaultCloudLitColor = GetMaterialColor(_skyMaterial, _ID_CloudColorLit, Color.white);
            _defaultCloudShadowColor = GetMaterialColor(_skyMaterial, _ID_CloudColorShadow, Color.white);
            _defaultSunsetCloudColor = GetMaterialColor(_skyMaterial, _ID_SunsetCloudColor, Color.white);
            _defaultNightCloudColor = GetMaterialColor(_skyMaterial, _ID_NightCloudColor, Color.white);
            _defaultSunDiscColor = GetMaterialColor(_skyMaterial, _ID_SunDiscColor, Color.white);
            _defaultSunScatterColor = GetMaterialColor(_skyMaterial, _ID_SunScatterColor, Color.white);
            _cachedSkyWeatherDefaults = true;
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

            _eclipseAngularRadius = GetAegirAngularRadiusDegrees();
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

        private bool TryResolveAegirSkyDirection(out float3 direction)
        {
            if (aegirObserverRelativeBody != null)
            {
                Vector3 currentDirection = aegirObserverRelativeBody.CurrentDirection;
                if (currentDirection.sqrMagnitude > 0.0001f)
                {
                    direction = math.normalize((float3)currentDirection);
                    return true;
                }
            }

            if (aegirTransform != null)
            {
                Vector3 localDirection = aegirTransform.localPosition;
                if (localDirection.sqrMagnitude > 0.0001f)
                {
                    direction = math.normalize((float3)localDirection);
                    return true;
                }
            }

            if (aegirTransform != null && playerTransform != null)
            {
                float3 playerPos = (float3)playerTransform.position;
                float3 aegirPos = (float3)aegirTransform.position;
                direction = math.normalizesafe(aegirPos - playerPos);
                if (math.lengthsq(direction) > 0.0001f)
                    return true;
            }

            direction = float3.zero;
            return false;
        }

        private float GetAegirAngularRadiusDegrees()
        {
            if (aegirObserverRelativeBody != null)
                return math.max(aegirObserverRelativeBody.AngularDiameterDegrees * 0.5f, 0.01f);

            if (aegirTransform != null && playerTransform != null)
            {
                float radius = GetAegirWorldRadius();
                float distance = math.max(
                    math.length((float3)aegirTransform.position - (float3)playerTransform.position),
                    0.01f);
                return math.degrees(math.atan2(radius, distance));
            }

            return math.max(_eclipseAngularRadius, 0.01f);
        }

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
            if (_atmosphereManager != null)
            {
                if (sunVisualTransform != null && sunVisualTransform.gameObject.activeSelf)
                    sunVisualTransform.gameObject.SetActive(false);
                return;
            }

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

        private void HandleDepthTierChanged(int depthTier, float depthMeters)
        {
            _currentDepthMeters = Mathf.Max(0f, depthMeters);
            UpdateDeepTextureResidencyState();
        }

        private void UpdateDeepTextureResidencyState()
        {
            float depthMeters = Mathf.Max(0f, _currentDepthMeters);
            DynamicResolutionScaler scaler = DynamicResolutionScaler.Instance;
            _currentAdaptiveRenderScale = scaler != null
                ? Mathf.Clamp01(scaler.CurrentRenderScale)
                : 1f;

            bool shouldReduceResidency = ShouldReduceDeepTextureResidency(depthMeters, _currentAdaptiveRenderScale);
            if (shouldReduceResidency == _deepTextureResidencyReduced)
                return;

            if (shouldReduceResidency)
                DetachDeepCelestialTextures();
            else
                RestoreCelestialTextureDefaults();
        }

        private bool ShouldReduceDeepTextureResidency(float depthMeters, float renderScale)
        {
            float depthReleaseThreshold = Mathf.Max(0f, deepTextureUnloadDepth - deepTextureDepthHysteresis);
            float adaptiveDepthReleaseThreshold = Mathf.Max(0f, adaptiveDeepTextureMinDepth - deepTextureDepthHysteresis);

            if (_deepTextureResidencyReduced)
            {
                bool keepDepthReduced = depthMeters >= depthReleaseThreshold;
                bool keepPerfReduced =
                    enableAdaptiveDeepTextureResidency &&
                    depthMeters >= adaptiveDepthReleaseThreshold &&
                    renderScale <= adaptiveDeepTextureRestoreRenderScale;
                return keepDepthReduced || keepPerfReduced;
            }

            bool reduceByDepth = depthMeters >= deepTextureUnloadDepth;
            bool reduceByPerfPressure =
                enableAdaptiveDeepTextureResidency &&
                depthMeters >= adaptiveDeepTextureMinDepth &&
                renderScale <= adaptiveDeepTextureUnloadRenderScale;

            return reduceByDepth || reduceByPerfPressure;
        }

        private void DetachDeepCelestialTextures()
        {
            SetMaterialTexture(_skyMaterial, _ID_HighCloudTex, null);
            SetMaterialTexture(_skyMaterial, _ID_MainCloudAtlas, null);
            SetMaterialTexture(_skyMaterial, _ID_MainCloudTex, null);
            SetMaterialTexture(_skyMaterial, _ID_StarTex, null);

            SetMaterialTexture(daySkybox, _ID_MainTex, null);
            SetMaterialTexture(daySkybox, _ID_EmissionMap, null);
            SetMaterialTexture(nightSkybox, _ID_MainTex, null);
            SetMaterialTexture(nightSkybox, _ID_EmissionMap, null);

            SetMaterialTexture(blendedSkyboxMaterial, _ID_DayCubemap, null);
            SetMaterialTexture(blendedSkyboxMaterial, _ID_NightCubemap, null);

            SetMaterialTexture(_aegirSharedMaterial, _ID_MainTex, null);
            SetMaterialTexture(_aegirSharedMaterial, _ID_DetailTex, null);
            SetMaterialTexture(_aegirSharedMaterial, _ID_EmissionMap, null);
            SetMaterialTexture(_aegirSharedMaterial, _ID_CelestialOcclusionTex, null);

            _deepTextureResidencyReduced = true;
        }

        private void RestoreCelestialTextureDefaults()
        {
            SetMaterialTexture(_skyMaterial, _ID_HighCloudTex, _skyHighCloudTexDefault);
            SetMaterialTexture(_skyMaterial, _ID_MainCloudAtlas, _skyMainCloudAtlasDefault);
            SetMaterialTexture(_skyMaterial, _ID_MainCloudTex, _skyMainCloudTexDefault);
            SetMaterialTexture(_skyMaterial, _ID_StarTex, _skyStarTexDefault);

            SetMaterialTexture(daySkybox, _ID_MainTex, _daySkyboxMainTexDefault);
            SetMaterialTexture(daySkybox, _ID_EmissionMap, _daySkyboxEmissionTexDefault);
            SetMaterialTexture(nightSkybox, _ID_MainTex, _nightSkyboxMainTexDefault);
            SetMaterialTexture(nightSkybox, _ID_EmissionMap, _nightSkyboxEmissionTexDefault);

            SetMaterialTexture(blendedSkyboxMaterial, _ID_DayCubemap, _blendedDayCubemapDefault);
            SetMaterialTexture(blendedSkyboxMaterial, _ID_NightCubemap, _blendedNightCubemapDefault);

            SetMaterialTexture(_aegirSharedMaterial, _ID_MainTex, _aegirMainTexDefault);
            SetMaterialTexture(_aegirSharedMaterial, _ID_DetailTex, _aegirDetailTexDefault);
            SetMaterialTexture(_aegirSharedMaterial, _ID_EmissionMap, _aegirEmissionMapDefault);
            SetMaterialTexture(_aegirSharedMaterial, _ID_CelestialOcclusionTex, _aegirCelestialOcclusionTexDefault);

            _deepTextureResidencyReduced = false;
        }

        private static Texture GetMaterialTexture(Material material, int propertyId)
        {
            if (material == null || !material.HasProperty(propertyId))
                return null;

            return material.GetTexture(propertyId);
        }

        private static void SetMaterialTexture(Material material, int propertyId, Texture texture)
        {
            if (material == null || !material.HasProperty(propertyId))
                return;

            material.SetTexture(propertyId, texture);
        }

        private static float GetMaterialFloat(Material material, int propertyId, float fallback)
        {
            if (material == null || !material.HasProperty(propertyId))
                return fallback;

            return material.GetFloat(propertyId);
        }

        private static Vector4 GetMaterialVector(Material material, int propertyId, Vector4 fallback)
        {
            if (material == null || !material.HasProperty(propertyId))
                return fallback;

            return material.GetVector(propertyId);
        }

        private static Color GetMaterialColor(Material material, int propertyId, Color fallback)
        {
            if (material == null || !material.HasProperty(propertyId))
                return fallback;

            return material.GetColor(propertyId);
        }

        private float ResolveCloudSpeedMultiplier()
        {
            if (!_surfaceWeatherOverrideActive)
                return 1f;

            return Mathf.Max(0f, _surfaceWeatherCloudSpeedMultiplier);
        }

        private float ResolveStarVisibilityMultiplier()
        {
            if (!_surfaceWeatherOverrideActive)
                return 1f;

            return Mathf.Clamp01(_surfaceWeatherStarVisibilityMultiplier);
        }

        private float ResolveStormEmissionMultiplier()
        {
            if (!_surfaceWeatherOverrideActive)
                return 1f;

            return Mathf.Max(0f, _surfaceWeatherStormEmissionMultiplier);
        }

        private void ApplySurfaceWeatherSkyProperties(Material targetMaterial)
        {
            CacheSkyWeatherDefaults();

            if (!_cachedSkyWeatherDefaults || targetMaterial == null)
                return;

            targetMaterial.SetFloat(_ID_SkyLuminanceMultiplier, ResolveSkyLuminanceMultiplier());

            if (!_surfaceWeatherOverrideActive)
            {
                targetMaterial.SetFloat(_ID_CloudDensityThreshold, _defaultCloudDensityThreshold);
                targetMaterial.SetFloat(_ID_CloudSoftness, _defaultCloudSoftness);
                targetMaterial.SetFloat(_ID_CloudSpeedMult, _defaultCloudSpeedMultiplier);
                targetMaterial.SetVector(_ID_WindDirection, _defaultWindDirection);
                targetMaterial.SetColor(_ID_CloudColorLit, _defaultCloudLitColor);
                targetMaterial.SetColor(_ID_CloudColorShadow, _defaultCloudShadowColor);
                targetMaterial.SetColor(_ID_SunsetCloudColor, ResolveScriptSunsetCloudColor());
                targetMaterial.SetColor(_ID_NightCloudColor, ResolveScriptNightCloudColor());
                targetMaterial.SetColor(_ID_SunDiscColor, _defaultSunDiscColor);
                targetMaterial.SetColor(_ID_SunScatterColor, _defaultSunScatterColor);
                return;
            }

            targetMaterial.SetFloat(_ID_CloudDensityThreshold, _surfaceWeatherCloudDensityThreshold);
            targetMaterial.SetFloat(_ID_CloudSoftness, _surfaceWeatherCloudSoftness);
            targetMaterial.SetFloat(_ID_CloudSpeedMult, _defaultCloudSpeedMultiplier * ResolveCloudSpeedMultiplier());
            targetMaterial.SetVector(_ID_WindDirection, _surfaceWeatherWindDirection);
            targetMaterial.SetColor(_ID_CloudColorLit, _surfaceWeatherCloudLitColor);
            targetMaterial.SetColor(_ID_CloudColorShadow, _surfaceWeatherCloudShadowColor);
            targetMaterial.SetColor(_ID_SunsetCloudColor, _surfaceWeatherSunsetCloudColor);
            targetMaterial.SetColor(_ID_NightCloudColor, _surfaceWeatherNightCloudColor);
            targetMaterial.SetColor(_ID_SunDiscColor, _defaultSunDiscColor * Mathf.Max(0f, _surfaceWeatherSunDiscMultiplier));
            targetMaterial.SetColor(_ID_SunScatterColor, _defaultSunScatterColor * Mathf.Max(0f, _surfaceWeatherSunScatterMultiplier));
        }

        private Color ResolveScriptSunsetCloudColor()
        {
            Color sunsetAtmosphereColor = sunsetAtmosphere != null
                ? sunsetAtmosphere.Evaluate(0f)
                : Color.white;
            Color sunsetCloudColor = MultiplyRgb(_sunsetProfile.horizonColor, sunsetAtmosphereColor);
            sunsetCloudColor.a = 1f;
            return sunsetCloudColor;
        }

        private Color ResolveScriptNightCloudColor()
        {
            Color nightAtmosphereColor = nightAtmosphere != null
                ? nightAtmosphere.Evaluate(0f)
                : Color.white;
            Color nightCloudColor = MultiplyRgb(_nightProfile.horizonColor, nightAtmosphereColor);
            nightCloudColor.a = 1f;
            return nightCloudColor;
        }

        private Color ResolveScriptSunsetHorizonColor()
        {
            Color sunsetAtmosphereColor = sunsetAtmosphere != null
                ? sunsetAtmosphere.Evaluate(0f)
                : Color.white;
            Color sunsetHorizonColor = MultiplyRgb(_sunsetProfile.horizonColor, sunsetAtmosphereColor);
            sunsetHorizonColor.a = 1f;
            return sunsetHorizonColor;
        }

        private float ResolveScriptAegirNightGlow()
        {
            return Mathf.Max(0f, _currentBacklitFactor * _atmosphereInscatterWeight);
        }

        private void ApplySurfaceWeatherSkyLuminance(ref Color zenith, ref Color horizon, ref Color nadir)
        {
            float multiplier = ResolveSkyLuminanceMultiplier();
            zenith *= multiplier;
            horizon *= multiplier;
            nadir *= multiplier;
        }

        private float ResolveSkyLuminanceMultiplier()
        {
            if (!_surfaceWeatherOverrideActive)
                return 1f;

            return Mathf.Max(0f, _surfaceWeatherSkyLuminanceMultiplier);
        }

        // ─────────────────────────────────────────────
        // SKY MATERIAL UPDATE
        // ─────────────────────────────────────────────

        private void UpdateSkyMaterial()
        {
            if (_skyMaterial == null)
                return;

            float sunElevationNormalized = math.clamp(_currentSunAngle / 90f, -1f, 1f);
            float3 fromSun = -_resolvedSunDirection;
            Vector4 sunDirection = new Vector4(fromSun.x, fromSun.y, fromSun.z, 0f);
            Vector4 aegirDirection = Vector4.zero;
            if (TryResolveAegirSkyDirection(out float3 toAegir))
            {
                aegirDirection = new Vector4(toAegir.x, toAegir.y, toAegir.z, 0f);
            }

            ApplySkyMaterialProperties(_skyMaterial, sunElevationNormalized, sunDirection, aegirDirection);
            _previousBlendForColors = _currentBlend;
            _lastAppliedSkyZenith = _resolvedSkyZenith;
            _lastAppliedSkyHorizon = _resolvedSkyHorizon;
            _lastAppliedSkyNadir = _resolvedSkyNadir;
        }

        private void ApplySkyMaterialProperties(
            Material targetMaterial,
            float sunElevationNormalized,
            Vector4 sunDirection,
            Vector4 aegirDirection)
        {
            if (targetMaterial == null)
                return;

            targetMaterial.SetFloat(_ID_GameTime, _gameTime);
            targetMaterial.SetFloat(_ID_NightBlend, _currentBlend);
            targetMaterial.SetFloat(_ID_StarIntensity, _currentStarIntensity);
            targetMaterial.SetFloat(_ID_SunElevation, sunElevationNormalized);
            targetMaterial.SetFloat(_ID_EclipseOcclusion, _smoothedOcclusionFactor);
            targetMaterial.SetFloat(_ID_AtmosphereTransmittanceWeight, _atmosphereTransmittanceWeight);
            targetMaterial.SetFloat(_ID_AtmosphereInscatterWeight, _atmosphereInscatterWeight);
            targetMaterial.SetVector(_ID_SunDirection, sunDirection);
            targetMaterial.SetVector(_ID_AegirDirection, aegirDirection);
            targetMaterial.SetColor(_ID_SkyColorZenith, _resolvedSkyZenith);
            targetMaterial.SetColor(_ID_SkyColorHorizon, _resolvedSkyHorizon);
            targetMaterial.SetColor(_ID_SkyColorNadir, _resolvedSkyNadir);
            targetMaterial.SetColor(_ID_SunsetHorizonColor, ResolveScriptSunsetHorizonColor());
            targetMaterial.SetFloat(_ID_AegirGlowIntensity, ResolveScriptAegirNightGlow());
            ApplySkyMaterialHazeProperties(targetMaterial);
            ApplySurfaceWeatherSkyProperties(targetMaterial);
        }

        private void ApplySkyMaterialHazeProperties(Material targetMaterial)
        {
            AtmosphericLightingState state = _surfaceAtmosphericLightingState.IsValid
                ? _surfaceAtmosphericLightingState
                : BuildSurfaceAtmosphericLightingState();

            targetMaterial.SetFloat(_ID_HazeIntensity, state.HorizonHazeIntensity);
            targetMaterial.SetFloat(_ID_HazeFalloff, state.HorizonHazeFalloff);
            targetMaterial.SetColor(_ID_HazeColor, state.HorizonHazeColor);
            targetMaterial.SetFloat(_ID_HazeSunTintStrength, state.HorizonHazeSunTintStrength);
            targetMaterial.SetFloat(_ID_HorizonMistShelfIntensity, state.HorizonMistShelfIntensity);
            targetMaterial.SetFloat(_ID_HorizonMistShelfHeight, state.HorizonMistShelfHeight);
            targetMaterial.SetFloat(_ID_HorizonMistShelfSoftness, state.HorizonMistShelfSoftness);
        }

        private void ResolveSkyColors(out Color zenith, out Color horizon, out Color nadir)
        {
            EvaluateCelestialAtmosphereProfileWeights(
                _currentSunAngle,
                out float dayWeight,
                out float sunsetWeight,
                out float nightWeight);

            zenith = ResolveSkyProfileColor(
                _dayProfile.zenithColor,
                _sunsetProfile.zenithColor,
                _nightProfile.zenithColor,
                dayWeight,
                sunsetWeight,
                nightWeight);
            horizon = ResolveSkyProfileColor(
                _dayProfile.horizonColor,
                _sunsetProfile.horizonColor,
                _nightProfile.horizonColor,
                dayWeight,
                sunsetWeight,
                nightWeight);
            nadir = ResolveSkyProfileColor(
                _dayProfile.nadirColor,
                _sunsetProfile.nadirColor,
                _nightProfile.nadirColor,
                dayWeight,
                sunsetWeight,
                nightWeight);

            if (_smoothedOcclusionFactor > 0f)
            {
                float eclipseNight = Mathf.Clamp01(_smoothedOcclusionFactor);
                zenith = Color.Lerp(zenith, _nightProfile.zenithColor, eclipseNight);
                horizon = Color.Lerp(horizon, _nightProfile.horizonColor, eclipseNight);
                nadir = Color.Lerp(nadir, _nightProfile.nadirColor, eclipseNight);
            }

            horizon = CompressHorizonColor(horizon, zenith, dayWeight, sunsetWeight, nightWeight);
            ApplySurfaceWeatherSkyColorInfluence(ref zenith, ref horizon, ref nadir, dayWeight, sunsetWeight, nightWeight);
            ApplySurfaceWeatherSkyLuminance(ref zenith, ref horizon, ref nadir);
        }

        private void ApplySurfaceWeatherSkyColorInfluence(
            ref Color zenith,
            ref Color horizon,
            ref Color nadir,
            float dayWeight,
            float sunsetWeight,
            float nightWeight)
        {
            if (!_surfaceWeatherOverrideActive)
                return;

            Color weatherHorizonAnchor = Color.Lerp(_surfaceWeatherFogColor, _surfaceWeatherAmbientColor, 0.35f);
            Color weatherZenithAnchor = Color.Lerp(_surfaceWeatherAmbientColor, weatherHorizonAnchor, 0.22f);
            float horizonBlend = dayWeight * 0.24f + sunsetWeight * 0.14f;
            float zenithBlend = dayWeight * 0.08f + sunsetWeight * 0.04f;
            float nadirBlend = dayWeight * 0.05f + nightWeight * 0.02f;

            horizon = Color.Lerp(horizon, weatherHorizonAnchor, Mathf.Clamp01(horizonBlend));
            horizon = DesaturateColor(horizon, Mathf.Lerp(0.18f, 0.08f, sunsetWeight));
            zenith = Color.Lerp(zenith, weatherZenithAnchor, Mathf.Clamp01(zenithBlend));
            nadir = Color.Lerp(nadir, weatherHorizonAnchor, Mathf.Clamp01(nadirBlend * 0.35f));

            horizon.a = 1f;
            zenith.a = 1f;
            nadir.a = 1f;
        }

        private static Color ResolveSkyProfileColor(
            Color dayColor,
            Color sunsetColor,
            Color nightColor,
            float dayWeight,
            float sunsetWeight,
            float nightWeight)
        {
            Color blendedColor =
                dayColor * dayWeight +
                sunsetColor * sunsetWeight +
                nightColor * nightWeight;
            blendedColor.a = 1f;
            return blendedColor;
        }

        private Color CompressHorizonColor(Color horizon, Color zenith, float dayWeight, float sunsetWeight, float nightWeight)
        {
            float effectiveZenithBlend =
                dayWeight * (_horizonZenithBlend * 0.35f) +
                sunsetWeight * (_horizonZenithBlend * 0.7f) +
                nightWeight * _horizonZenithBlend;
            float effectiveBrightnessScale =
                dayWeight * Mathf.Lerp(1f, _horizonBrightnessScale, 0.28f) +
                sunsetWeight * Mathf.Lerp(1f, _horizonBrightnessScale, 0.5f) +
                nightWeight * _horizonBrightnessScale;

            Color compressed = Color.Lerp(horizon, zenith, effectiveZenithBlend);
            compressed *= effectiveBrightnessScale;
            float targetLuminance =
                ComputePerceivedLuminance(horizon) *
                (dayWeight * 0.92f + sunsetWeight * 0.82f + nightWeight * 0.68f);
            compressed = LiftColorTowardsLuminance(
                compressed,
                targetLuminance,
                0.34f);
            compressed = DesaturateColor(
                compressed,
                dayWeight * 0.26f + nightWeight * 0.04f);
            compressed.a = horizon.a;
            return compressed;
        }

        // ─────────────────────────────────────────────
        // SUN OCCLUSION
        // ─────────────────────────────────────────────

        private void UpdateSunOcclusion(float dt)
        {
            if (_isEclipseActive)
            {
                _sunOcclusionFactor = 1.0f;
#if UNITY_EDITOR
                if (!Application.isPlaying && dt <= 0f)
                {
                    _smoothedOcclusionFactor = 1.0f;
                    return;
                }
#endif
                _smoothedOcclusionFactor = math.lerp(
                    _smoothedOcclusionFactor, 1.0f, math.saturate(flareFadeSpeed * dt));
                return;
            }

            if (!TryResolveAegirSkyDirection(out float3 toAegir))
            {
                _sunOcclusionFactor = 0f;
#if UNITY_EDITOR
                if (!Application.isPlaying && dt <= 0f)
                {
                    _smoothedOcclusionFactor = 0f;
                    return;
                }
#endif
                _smoothedOcclusionFactor = math.lerp(
                    _smoothedOcclusionFactor, 0f, math.saturate(flareFadeSpeed * dt));
                return;
            }

            float3 toSun     = _resolvedSunDirection;

            float dotSunAegir = math.dot(toSun, toAegir);
            float angularSeparationDeg = math.degrees(
                math.acos(math.clamp(dotSunAegir, -1f, 1f)));

            float dynamicAngularRadius = GetAegirAngularRadiusDegrees();

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

#if UNITY_EDITOR
            if (!Application.isPlaying && dt <= 0f)
            {
                _smoothedOcclusionFactor = _sunOcclusionFactor;
                return;
            }
#endif
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
            bool skyOwnsPrimarySunDisc = _atmosphereManager != null;

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

                if (!skyOwnsPrimarySunDisc
                    && sunVisualTransform.gameObject.activeSelf != shouldBeActive)
                {
                    sunVisualTransform.gameObject.SetActive(shouldBeActive);
                }

                if (shouldBeActive && sunVisualTransform.gameObject.activeSelf)
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

        private void HideSunVisualDisc()
        {
            if (sunVisualTransform != null && sunVisualTransform.gameObject.activeSelf)
                sunVisualTransform.gameObject.SetActive(false);
        }

        private void RestoreSunDefaults()
        {
            if (sunLight != null && _baseSunIntensityCaptured && _atmosphereManager == null)
                sunLight.intensity = _baseSunIntensity;

            if (sunLight != null && _baseSunColorCaptured)
                sunLight.color = _baseSunColor;

            if (_sunLensFlare != null && _baseFlareValuesCaptured)
            {
                _sunLensFlare.intensity = _baseFlareIntensity;
                _sunLensFlare.scale = _baseFlareScale;
                if (!_sunLensFlare.enabled)
                    _sunLensFlare.enabled = true;
            }

            if (_atmosphereManager != null)
            {
                HideSunVisualDisc();
            }
            else if (sunVisualTransform != null && !sunVisualTransform.gameObject.activeSelf)
            {
                sunVisualTransform.gameObject.SetActive(true);
            }
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
            _currentStarIntensity *= ResolveStarVisibilityMultiplier();

            if (blendedSkyboxMaterial != null)
                blendedSkyboxMaterial.SetFloat(_ID_StarIntensity, _currentStarIntensity);
        }

        // ─────────────────────────────────────────────
        // GLOBAL SHADER DATA
        // ─────────────────────────────────────────────

        private void UpdateGlobalShaderData()
        {
            if (_atmosphereManager == null)
            {
                float3 fromSun = -_resolvedSunDirection;
                Shader.SetGlobalVector(_ID_SunDirection,
                    new Vector4(fromSun.x, fromSun.y, fromSun.z, 0f));
            }

            Vector4 aegirDirection = Vector4.zero;
            if (TryResolveAegirSkyDirection(out float3 toAegir))
                aegirDirection = new Vector4(toAegir.x, toAegir.y, toAegir.z, 0f);

            Shader.SetGlobalVector(_ID_AegirDirection, aegirDirection);
            Shader.SetGlobalColor(_ID_SkyColorZenith, _resolvedSkyZenith);
            Shader.SetGlobalColor(_ID_SkyColorHorizon, _resolvedSkyHorizon);
            Shader.SetGlobalColor(_ID_SkyColorNadir, _resolvedSkyNadir);
            Shader.SetGlobalFloat(_ID_NightBlend, _currentBlend);
            Shader.SetGlobalFloat(_ID_EclipseOcclusion, _smoothedOcclusionFactor);
            Shader.SetGlobalFloat(_ID_AtmosphereTransmittanceWeight, _atmosphereTransmittanceWeight);
            Shader.SetGlobalFloat(_ID_AtmosphereInscatterWeight, _atmosphereInscatterWeight);
            Shader.SetGlobalFloat(_ID_CelestialAtmosphereLutReady, _celestialAtmosphereLutTexture != null ? 1f : 0f);
            Shader.SetGlobalFloat(_ID_AtmosphereExposure, _currentAtmosphereExposure);
            Shader.SetGlobalFloat(_ID_CelestialHorizonDensity, horizonDensity);
            Shader.SetGlobalFloat(_ID_CelestialZenithTransparency, zenithTransparency);
            Shader.SetGlobalFloat(_ID_CelestialAtmosphereBlendPower, atmosphereBlendPower);
            Shader.SetGlobalFloat(_ID_GameTime, _gameTime);
            PublishCelestialAtmosphereLut();
        }

        // ─────────────────────────────────────────────
        // ECLIPSE BACKLIGHT
        // ─────────────────────────────────────────────

        private void CalculateEclipseBacklight()
        {
            _currentBacklitFactor = 0f;

            if (!TryResolveAegirSkyDirection(out float3 playerToGiant))
                return;

            float3 playerToSun = _resolvedSunDirection;

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

            if (TryResolveAegirSkyDirection(out float3 playerToAegir))
            {
                float3 aegirToPlayer = -playerToAegir;
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
            _aegirMPB.SetFloat(_ID_StormEmission, stormEmissionIntensity * ResolveStormEmissionMultiplier());
            _aegirMPB.SetFloat(_ID_SunBacklitFactor, _currentBacklitFactor);
            _aegirMPB.SetFloat(_ID_GlobalRotation, _rotationPhase);
            _aegirMPB.SetFloat(_ID_GameTime, _gameTime);
            _aegirMPB.SetFloat(_ID_NightBlend, _currentBlend);
            _aegirMPB.SetFloat(_ID_AtmosphereTransmittanceWeight, _atmosphereTransmittanceWeight);
            _aegirMPB.SetFloat(_ID_AtmosphereInscatterWeight, _atmosphereInscatterWeight);

            _aegirMPB.SetColor(_ID_SkyColorZenith, _resolvedSkyZenith);
            _aegirMPB.SetColor(_ID_SkyColorHorizon, _resolvedSkyHorizon);
            _aegirMPB.SetColor(_ID_SkyColorNadir, _resolvedSkyNadir);

            if (_skyMaterial != null && _skyMaterial.HasProperty(_ID_WindDirection))
                _aegirMPB.SetVector(_ID_WindDirection, _skyMaterial.GetVector(_ID_WindDirection));

            aegirRenderer.SetPropertyBlock(_aegirMPB);

            OnPlanetPhaseChanged?.Invoke(_currentPhase);
        }

        // ─────────────────────────────────────────────
        // PLANET-SHINE
        // ─────────────────────────────────────────────

        private void UpdateMoonMaterialOverrides()
        {
            if (_moonMPB == null)
                return;

            for (int i = 0; i < _moonRenderers.Count; i++)
            {
                Renderer moonRenderer = _moonRenderers[i];
                if (moonRenderer == null)
                    continue;

                moonRenderer.GetPropertyBlock(_moonMPB);
                _moonMPB.SetFloat(
                    _ID_AtmosphereTransmittanceWeight,
                    _atmosphereTransmittanceWeight * _moonAtmosphereTransmittanceMultiplier);
                _moonMPB.SetFloat(
                    _ID_AtmosphereInscatterWeight,
                    _atmosphereInscatterWeight * _moonAtmosphereInscatterMultiplier);
                moonRenderer.SetPropertyBlock(_moonMPB);
            }
        }

        private void UpdatePlanetShine()
        {
            if (_planetShineLight == null || !TryResolveAegirSkyDirection(out float3 playerToAegir))
                return;

            float3 aegirToPlayer = -playerToAegir;
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
            if (!TryResolveAegirSkyDirection(out float3 toAegir))
                return;

            float3 toSun = _resolvedSunDirection;

            float dotSunAegir = math.dot(toSun, toAegir);
            float angleDeg = math.degrees(
                math.acos(math.clamp(dotSunAegir, -1f, 1f)));

            float dynamicAngularRadius = GetAegirAngularRadiusDegrees();

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

        internal void SetSurfaceWeatherOverride(
            float cloudDensityThreshold,
            float cloudSoftness,
            float cloudSpeedMultiplier,
            Vector2 windDirection,
            Color fogColor,
            float fogDensity,
            Color ambientColor,
            float sunMultiplier,
            float starVisibilityMultiplier,
            float stormEmissionMultiplier,
            float skyLuminanceMultiplier,
            float sunDiscMultiplier,
            float sunScatterMultiplier,
            Color cloudLitColor,
            Color cloudShadowColor,
            Color sunsetCloudColor,
            Color nightCloudColor)
        {
            CacheSkyWeatherDefaults();

            Vector2 resolvedDirection = windDirection.sqrMagnitude > 0.0001f
                ? windDirection.normalized
                : new Vector2(_defaultWindDirection.x, _defaultWindDirection.y).normalized;

            _surfaceWeatherOverrideActive = true;
            _surfaceWeatherFogOverrideActive = true;
            _surfaceWeatherCloudDensityThreshold = Mathf.Clamp01(cloudDensityThreshold);
            _surfaceWeatherCloudSoftness = Mathf.Clamp(cloudSoftness, 0.01f, 0.5f);
            _surfaceWeatherCloudSpeedMultiplier = Mathf.Max(0f, cloudSpeedMultiplier);
            _surfaceWeatherWindDirection = new Vector4(resolvedDirection.x, resolvedDirection.y, 0f, 0f);
            _surfaceWeatherFogColor = fogColor;
            _surfaceWeatherFogColor.a = 1f;
            _surfaceWeatherFogDensity = Mathf.Max(0.0001f, fogDensity);
            _surfaceWeatherAmbientColor = ambientColor;
            _surfaceWeatherAmbientColor.a = 1f;
            _surfaceWeatherSunMultiplier = Mathf.Max(0f, sunMultiplier);
            _surfaceWeatherStarVisibilityMultiplier = Mathf.Clamp01(starVisibilityMultiplier);
            _surfaceWeatherStormEmissionMultiplier = Mathf.Max(0f, stormEmissionMultiplier);
            _surfaceWeatherSkyLuminanceMultiplier = Mathf.Max(0f, skyLuminanceMultiplier);
            _surfaceWeatherSunDiscMultiplier = Mathf.Max(0f, sunDiscMultiplier);
            _surfaceWeatherSunScatterMultiplier = Mathf.Max(0f, sunScatterMultiplier);
            _surfaceWeatherCloudLitColor = cloudLitColor;
            _surfaceWeatherCloudShadowColor = cloudShadowColor;
            _surfaceWeatherSunsetCloudColor = sunsetCloudColor;
            _surfaceWeatherNightCloudColor = nightCloudColor;
        }

        internal void ClearSurfaceWeatherOverride()
        {
            _surfaceWeatherOverrideActive = false;
            _surfaceWeatherFogOverrideActive = false;
            _surfaceWeatherCloudSpeedMultiplier = 1f;
            _surfaceWeatherStarVisibilityMultiplier = 1f;
            _surfaceWeatherStormEmissionMultiplier = 1f;
            _surfaceWeatherSkyLuminanceMultiplier = 1f;
            _surfaceWeatherSunDiscMultiplier = 1f;
            _surfaceWeatherSunScatterMultiplier = 1f;
            _surfaceWeatherSunMultiplier = 1f;
        }

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
