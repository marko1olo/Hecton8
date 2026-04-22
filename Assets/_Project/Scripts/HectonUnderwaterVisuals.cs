// ============================================================================
// HECTON-8 â€” HectonUnderwaterVisuals.cs  v5.1
// Ð•Ð”Ð˜ÐÐžÐ›Ð˜Ð§ÐÐ«Ð™ Ð”Ð˜Ð Ð•ÐšÐ¢ÐžÐ  Ð¡Ð Ð•Ð”Ð«: Ñ‚ÑƒÐ¼Ð°Ð½, ÑÐ²ÐµÑ‚, Ñ†Ð²ÐµÑ‚Ð°, ÐºÐ°Ð¼ÐµÑ€Ð°, Ñ€Ð°ÑÑÐµÐ¸Ð²Ð°Ð½Ð¸Ðµ ÑÐ¾Ð»Ð½Ñ†Ð°.
//
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// v5.1 CHANGES â€” RACE CONDITION FIX:
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
//
//   [FIX] EXECUTION ORDER:
//     DefaultExecutionOrder(-4000) ensures this script's OnEnable
//     fires AFTER AtmosphereManager(-6000) but BEFORE CelestialEngine(-3000).
//     Registration order in GameTickManager = tick order.
//
//     CHAIN (every frame, deterministic):
//       1. AtmosphereManager.Tick() â†’ fresh ProfileSunIntensity, ComputedHorizonFade
//       2. UnderwaterVisuals.Tick() â†’ sunLight.intensity = profile Ã— horizon Ã— depth
//       3. CelestialEngine.Tick()   â†’ sunLight.intensity *= eclipseVisibility
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
//     (profile Ã— horizon) so CelestialEngine can multiply by eclipse.
//     Guard changed: only skip if BOTH baseSun AND horizon are zero
//     (prevents writing 0 when AtmosphereManager hasn't initialized).
//
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// v5.0 PRESERVED:
//   âœ“ Global depth curve (AnimationCurve)
//   âœ“ Fog density derived from lightFactor
//   âœ“ Sun scattering / color fade
//   âœ“ Biome color interpolation
//   âœ“ Camera background = fog color
//   âœ“ EnforceFogState render callback
//   âœ“ Zero GC in Tick
//   âœ“ [ExecuteAlways] for Editor preview
//
// ÐšÐžÐžÐ Ð”Ð˜ÐÐÐ¦Ð˜Ð¯ Ð—ÐÐŸÐ˜Ð¡Ð˜ sunLight.intensity (v5.1):
//   AtmosphereManager(-6000) â†’ ProfileSunIntensity, ComputedHorizonFade (data)
//   UnderwaterVisuals(-4000) â†’ sunLight.intensity = profile Ã— horizon Ã— lightCurve
//   CelestialEngine(-3000)   â†’ sunLight.intensity *= (1 - eclipseOcclusion)
// ============================================================================

using Hecton8.Core;
using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Celestial;
using Hecton8.Gameplay;
using Hecton8.VFX;
using Hecton8.World;
using NASAPunk.Visor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;
using VLB;
using CrestUnderwaterRenderer = global::Crest.UnderwaterRenderer;
using CrestOceanRenderer = global::Crest.OceanRenderer;

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
        internal static HectonUnderwaterVisuals ActiveRuntimeInstance { get; private set; }

        private const float RuntimeCameraResolveRetryInterval = 1f;
        private const float EditorCameraResolveRetryInterval = 0.25f;
        private const float VisualEnterUnderwaterDepth = 0.08f;
        private const float VisualExitUnderwaterDepth = 0.03f;
        private const float VisualForcedUnderwaterDepth = 0.18f;
        private const float VisualCameraDepthOverrideThreshold = 0.15f;
        private const float MaxSurfaceFogBlendDepth = 3f;
        private const float MaxSceneViewUnderwaterFogDensityScale = 0.24f;
        private const float UnderwaterFogDensityFloorNearSurface = 0.012f;
        private const float UnderwaterFogDensityFloorAtDepth = 0.0048f;
        private const float UnderwaterFogDensityFloorDepth = 8f;
        private const float DaylightReadableDepth = 460f;
        private const float DaylightReadableLightFloor = 0.27f;
        private const float DaylightReadableExtinctionReduction = 0.38f;
        private const float FogBlackoutStartDepthDay = 340f;
        private const float FogBlackoutStartDepthNight = 90f;
        private const float FogBlackBlendIntensity = 0.68f;
        private const float UnderwaterFarHazeStartDepth = 1.5f;
        private const float UnderwaterFarHazeFullDepth = 14f;
        private const float UnderwaterFarHazeDensityBoost = 0.00075f;
        private const float UnderwaterBaselineDistanceHaze = 0.00045f;
        private const float UnderwaterMediumFogColorBlend = 0.54f;
        private const float UnderwaterDepthColumnHazeFullDepth = 36f;
        private const float UnderwaterDepthColumnHazeDensityBoost = 0.00055f;
        private const float UnderwaterBiomeFogInfluenceShallow = 0.18f;
        private const float UnderwaterBiomeFogInfluenceDeep = 0.34f;
        private const float UnderwaterBiomeFogInfluenceDepth = 90f;
        private const float UnderwaterSunlitTintDepth = 32f;
        private const float UnderwaterSunlitTintStrength = 0.38f;
        private const float UnderwaterSunlitAmbientDepth = 42f;
        private const float UnderwaterSunlitAmbientStrength = 0.24f;
        private const float GameplayReadableBeerLambertExtinctionBias = 0.72f;
        private const float UnderwaterShallowColumnColorDepth = 56f;
        private const float UnderwaterShallowColumnColorStrength = 0.12f;
        private const float UnderwaterDaylightSeaTintDepth = 96f;
        private const float UnderwaterDaylightSeaTintStrength = 0.34f;
        private const float UnderwaterDaylightAmbientTintStrength = 0.18f;
        private const float UnderwaterClearWaterMotesStrength = 0.2f;
        private const string UnderwaterSuspendedMotesChildName = "Underwater_SuspendedMotes";
        private const string UnderwaterExhaleBubblesChildName = "Underwater_ExhaleBubbles";
        private const string UnderwaterShallowSunBeamChildName = "Underwater_ShallowSunBeam";
        private static readonly Color UnderwaterDaylightSeaTintShallow = new Color(0.118f, 0.402f, 0.424f, 1f);
        private static readonly Color UnderwaterDaylightSeaTintMid = new Color(0.026f, 0.156f, 0.238f, 1f);
        private static readonly int _SargassumCanopyShadowParamsId = Shader.PropertyToID("_SargassumCanopyShadowParams");
        private static readonly int _SargassumCanopyLightingParamsId = Shader.PropertyToID("_SargassumCanopyLightingParams");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” REFERENCES
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â•â•â• REFERENCES â•â•â•")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] private Light sunLight;
        [SerializeField] private LensFlareComponentSRP sunFlare;
        [SerializeField] private Transform sunVisualTransform;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private DepthZoneDirector depthZoneDirector;
        [SerializeField] private LandingImpactVFX transitionCameraVfx;
        [SerializeField] private VisorHUDController transitionVisorController;
        [Tooltip("Near-camera suspended particulate system parented under the runtime main camera.")]
        [SerializeField] private ParticleSystem underwaterSuspendedMotes;
        [Tooltip("Burst-only exhale bubble system parented under the runtime main camera.")]
        [SerializeField] private ParticleSystem underwaterExhaleBubbles;
        [Tooltip("Optional shallow-water god ray beam parented under the runtime main camera.")]
        [SerializeField] private VolumetricLightBeamHD shallowSunBeam;
        [Tooltip("Attached light used only to drive the VLB beam. Keep cullingMask = 0 to avoid lighting the world.")]
        [SerializeField] private Light shallowSunBeamLight;

        [Header("â•â•â• SARGASSUM CANOPY â•â•â•")]
        [Tooltip("Allows underwater visuals to react to global sargassum density when the player dives under floating mats.")]
        [SerializeField] private bool enableSargassumCanopyLighting = true;
        [Tooltip("World-space radius used by the local canopy shadow blob pushed into first-party underwater terrain shaders.")]
        [SerializeField, UnityEngine.Range(4f, 48f)] private float sargassumCanopyShadowRadius = 24f;
        [Tooltip("How strongly floating sargassum density suppresses underwater light when the player is under a canopy.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float sargassumCanopyLightOcclusionStrength = 0.62f;
        [Tooltip("Extra fog density added under dense canopy coverage.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float sargassumCanopyFogBoost = 0.28f;
        [Tooltip("Ambient-light suppression under a dense canopy.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float sargassumCanopyAmbientOcclusionStrength = 0.48f;
        [Tooltip("How strongly the canopy suppresses shallow sun beams unless a local Voronoi window is open.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float sargassumCanopyBeamOcclusionStrength = 0.72f;
        [Tooltip("How strongly Voronoi canopy windows re-open shallow god rays.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float sargassumCanopyBeamWindowBoost = 0.94f;
        [Tooltip("How strongly the shallow god ray shifts laterally toward the current canopy-window anchor so the beam tracks drifting sargassum openings.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float sargassumCanopyBeamAnchorTracking = 0.42f;
        [Tooltip("Maximum local beam offset applied when tracking a drifting canopy window.")]
        [SerializeField, UnityEngine.Range(0f, 6f)] private float sargassumCanopyBeamAnchorMaxOffset = 2.4f;

        [Header("â•â•â• ATMOSPHERE MANAGER â•â•â•")]
        [SerializeField] private HectonAtmosphereManager atmosphereManager;

        [Header("â•â•â• CREST MATERIAL â•â•â•")]
        [SerializeField] private Material oceanUnderwaterMaterial;

        [Header("â•â•â• SKY MATERIAL â•â•â•")]
        [SerializeField] private Material skyMaterial;

        [Header("â•â•â• BIOME PALETTE â•â•â•")]
        [SerializeField] private HectonOceanPalette biomePalette;

        [Header("â•â•â• VERTICAL RUNTIME â•â•â•")]
        [SerializeField] private BiomeMatrixDirector biomeMatrixDirector;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” GLOBAL DEPTH CURVE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â•â•â• GLOBAL LIGHT CURVE â•â•â•")]
        [Tooltip("Ð“Ð›ÐÐ’ÐÐÐ¯ ÐšÐ Ð˜Ð’ÐÐ¯ Ð—ÐÐ¢Ð•ÐœÐÐ•ÐÐ˜Ð¯.\n" +
                 "X = Ð³Ð»ÑƒÐ±Ð¸Ð½Ð° (Ð¼), Y = Ð¼Ð½Ð¾Ð¶Ð¸Ñ‚ÐµÐ»ÑŒ ÑÐ²ÐµÑ‚Ð° [0..1].")]
        [SerializeField] private AnimationCurve globalLightCurve = new AnimationCurve(
            new Keyframe(0f,    1.0f,  0f, 0f),
            new Keyframe(300f,  0.8f,  0f, 0f),
            new Keyframe(700f,  0.1f,  0f, 0f),
            new Keyframe(1000f, 0.0f,  0f, 0f)
        );

        [Header("â•â•â• FOG DENSITY RANGE â•â•â•")]
        [Header("Beer-Lambert Depth Attenuation")]
        [Tooltip("Uses Crest depth-fog coefficients as Beer-Lambert extinction instead of the legacy authored depth curve.")]
        [SerializeField] private bool useBeerLambertDepthAttenuation = true;
        [Tooltip("Keeps the upper water column readable before full extinction ramps in.")]
        [SerializeField, UnityEngine.Range(0f, 80f)] private float beerLambertSurfaceClarityDepth = 35f;
        [Tooltip("Global multiplier on extinction derived from Crest _DepthFogDensity.")]
        [SerializeField, UnityEngine.Range(0.1f, 4f)] private float beerLambertExtinctionScale = 1f;
        [Tooltip("Treat deep water as effectively black once transmittance falls below this threshold.")]
        [SerializeField, UnityEngine.Range(0f, 0.05f)] private float beerLambertBlackoutThreshold = 0.0025f;
        [Tooltip("Depth gate for the deep-black clamp so the upper column stays readable.")]
        [SerializeField, UnityEngine.Range(100f, 1000f)] private float beerLambertBlackoutDepth = 450f;

        [Header("Fog Density Range")]
        [UnityEngine.Range(0.0001f, 0.05f)]
        [SerializeField] private float minFogDensity = 0.002f;

        [UnityEngine.Range(0.01f, 0.5f)]
        [SerializeField] private float maxFogDensity = 0.08f;

        [Header("Editor Scene View Preview")]
        [Tooltip("Scales Unity fog density for Scene View underwater preview so the editor does not stack full fog on top of Crest underwater rendering.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float sceneViewUnderwaterFogDensityScale = 0.35f;

        [Header("Horizon Weld")]
        [Tooltip("Blends underwater fog back toward the sky-owned horizon color near the surface to avoid a hard seam.")]
        [SerializeField, UnityEngine.Range(0.5f, 40f)] private float surfaceFogBlendDepth = 16f;

        [Header("Surface Ocean Horizon Merge")]
        [Tooltip("How much the distant/grazing ocean is pulled toward the fog-owned horizon veil. Raise this when the waterline still reads as a hard cut.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float surfaceOceanHorizonFogBlend = 0.78f;
        [Tooltip("How much the ocean base color is lifted toward the same atmospheric veil. Lower than grazing on purpose so near water keeps its body color.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float surfaceOceanBaseFogBlend = 0.24f;
        [Tooltip("How much the sun-facing horizon water is allowed to inherit the sky sun-scatter tint instead of staying neutral fog.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float surfaceOceanSunScatterBlend = 0.2f;
        [Tooltip("Extra luminance lift for grazing water near the horizon veil. Raise this when the horizon is still a dark strip under a bright sky.")]
        [SerializeField, UnityEngine.Range(0f, 2f)] private float surfaceOceanHorizonLuminanceLift = 0.7f;
        [Tooltip("How strongly distant ocean merge prefers sky/haze tint over neutral fog. Raise this when the far water keeps collapsing into gray instead of inheriting the horizon air color.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float surfaceOceanHorizonSkyBias = 0.72f;
        [Tooltip("Preserves sky/haze color in the distant ocean merge after fog neutralization. Raise this when the horizon line softens but the far water still looks dead and gray.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float surfaceOceanHorizonColorPreserve = 0.28f;
        [Tooltip("Controls how strongly Crest's procedural sky base is glued to the fog/haze state instead of the authored fallback color.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float crestSkyBaseFogLink = 0.88f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” CONFIGURATION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â•â•â• WATER LEVEL â•â•â•")]
        [SerializeField] private float waterLevelFallback = 4900f;

        [Header("â•â•â• DEEP CELESTIAL CULL â•â•â•")]
        [Tooltip("Suppresses SpaceCamera celestial rendering below this depth to cut deep-water render overhead without touching shallow water.")]
        [SerializeField] private float deepCelestialCullDepth = 1000f;
        [Tooltip("Keeps SpaceCamera celestial rendering suppressed until the player climbs clearly out of the deep-water threshold instead of thrashing at one boundary.")]
        [SerializeField, UnityEngine.Range(0f, 300f)] private float deepCelestialCullDepthHysteresis = 120f;
        [Tooltip("Allows weak hardware to suppress the extra celestial camera earlier once dynamic resolution has already fallen and the player is no longer in shallow water.")]
        [SerializeField] private bool enableAdaptiveSpaceCameraCull = true;
        [Tooltip("Do not suppress the celestial camera from perf pressure in shallow water. This preserves near-surface sky readability.")]
        [SerializeField, UnityEngine.Range(0f, 1000f)] private float adaptiveSpaceCameraCullMinDepth = 350f;
        [Tooltip("Render-scale threshold that triggers earlier SpaceCamera suppression on weak hardware.")]
        [SerializeField, UnityEngine.Range(0.5f, 1f)] private float adaptiveSpaceCameraCullRenderScale = 0.76f;
        [Tooltip("Render-scale threshold required before the SpaceCamera is restored after adaptive suppression.")]
        [SerializeField, UnityEngine.Range(0.5f, 1f)] private float adaptiveSpaceCameraRestoreRenderScale = 0.9f;

        [Header("â•â•â• SUN VISUAL â•â•â•")]
        [UnityEngine.Range(0.0001f, 0.05f)]
        [SerializeField] private float sunVisualDisableThreshold = 0.005f;

        [Header("â•â•â• SUN SCATTERING â•â•â•")]
        [SerializeField] private float baseSunSize = 0.002f;
        [SerializeField] private float underwaterSunSizeMax = 0.15f;
        [SerializeField] private float baseSunEdgeSoftness = 0.001f;
        [SerializeField] private float underwaterSunSoftnessMax = 0.5f;
        [SerializeField, UnityEngine.Range(0.5f, 20f)] private float sunStateBrightenSpeed = 4.5f;
        [SerializeField, UnityEngine.Range(0.5f, 20f)] private float sunStateDarkenSpeed = 8f;

        [Header("â•â•â• TRANSITION â•â•â•")]
        [UnityEngine.Range(0.05f, 2.0f)]
        [SerializeField] private float biomeTransitionSpeed = 0.2f;
        [SerializeField] private float slowTickInterval = 0.5f;

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â SUBMERGE IMPULSE Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [SerializeField, UnityEngine.Range(0f, 0.6f)] private float submergeDarkenStrength = 0.2f;
        [SerializeField, UnityEngine.Range(0f, 2f)] private float submergeFogBoost = 0.45f;
        [SerializeField, UnityEngine.Range(0.05f, 1f)] private float submergeImpulseDuration = 0.32f;
        [SerializeField, UnityEngine.Range(0.1f, 2f)] private float submergeImpulseDepthWindow = 0.9f;

        [Header("── Thermocline Transition ───")]
        [Tooltip("Optional subtle one-shot used when the player punches through a sharp water-layer boundary.")]
        [SerializeField] private AudioClip thermoclineTransitionClip;
        [SerializeField, UnityEngine.Range(0f, 1f)] private float thermoclineMinTriggerIntensity = 0.18f;
        [SerializeField, UnityEngine.Range(0.25f, 20f)] private float thermoclineTemperatureDeltaForFullEffect = 6f;
        [SerializeField, UnityEngine.Range(0.02f, 0.5f)] private float thermoclineFogDeltaForFullEffect = 0.11f;
        [SerializeField, UnityEngine.Range(0.05f, 1.5f)] private float thermoclineColorDeltaForFullEffect = 0.32f;
        [SerializeField, UnityEngine.Range(0f, 1f)] private float thermoclineAudioVolume = 0.26f;
        [SerializeField, UnityEngine.Range(0f, 1f)] private float thermoclineVisorDistortionHoldDuration = 0.08f;
        [SerializeField, UnityEngine.Range(0.25f, 8f)] private float thermoclineVisorDistortionRecoverySpeed = 5.8f;
        [SerializeField, UnityEngine.Range(0.1f, 2f)] private float thermoclineMinRepeatInterval = 0.45f;

        [Header("Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â SHALLOW CAUSTICS Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â")]
        [SerializeField] private bool enableShallowCaustics = true;
        [SerializeField, UnityEngine.Range(0.1f, 4f)] private float causticsStrengthScale = 1f;
        [SerializeField, UnityEngine.Range(0.05f, 2f)] private float causticsFadeInDepth = 0.3f;
        [SerializeField, UnityEngine.Range(1f, 40f)] private float causticsFadeOutDepth = 18f;
        [SerializeField, UnityEngine.Range(0f, 1f)] private float causticsMinLightFactor = 0.18f;

        [Header("â”€â”€â”€ UNDERWATER MOTES â”€â”€â”€")]
        [Tooltip("Enables camera-local suspended particulate while underwater.")]
        [SerializeField] private bool enableSuspendedMotes = true;
        [Tooltip("Base emission rate at clean shallow water.")]
        [SerializeField, UnityEngine.Range(0f, 64f)] private float suspendedMotesMinEmission = 6f;
        [Tooltip("Emission ceiling at deeper or dirtier water.")]
        [SerializeField, UnityEngine.Range(0f, 128f)] private float suspendedMotesMaxEmission = 24f;
        [Tooltip("Depth at which the particle field reaches full density.")]
        [SerializeField, UnityEngine.Range(0.25f, 40f)] private float suspendedMotesFullEmissionDepth = 10f;
        [Tooltip("Extra emission injected during the first moment of submerge.")]
        [SerializeField, UnityEngine.Range(0f, 32f)] private float suspendedMotesSubmergeBoost = 10f;
        [Tooltip("How strongly biome turbidity raises particulate density.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float suspendedMotesTurbidityWeight = 0.65f;

        [Header("â”€â”€â”€ BOTTOM SILT â”€â”€â”€")]
        [Tooltip("Boosts near-camera particulate when the player moves close to the seafloor.")]
        [SerializeField] private bool enableBottomSiltBoost = true;
        [Tooltip("Maximum interval between bottom probes while underwater.")]
        [SerializeField, UnityEngine.Range(0.05f, 1f)] private float bottomSiltProbeInterval = 0.18f;
        [Tooltip("No extra disturbed silt above this seafloor distance.")]
        [SerializeField, UnityEngine.Range(0.25f, 12f)] private float bottomSiltActivationDistance = 3.5f;
        [Tooltip("Full disturbed-silt response when this close to the seafloor.")]
        [SerializeField, UnityEngine.Range(0.1f, 4f)] private float bottomSiltFullDistance = 1.2f;
        [Tooltip("Minimum player speed before disturbed silt appears.")]
        [SerializeField, UnityEngine.Range(0f, 6f)] private float bottomSiltMinSpeed = 0.85f;
        [Tooltip("Player speed at which disturbed silt reaches full intensity.")]
        [SerializeField, UnityEngine.Range(0.25f, 12f)] private float bottomSiltFullSpeed = 3.4f;
        [Tooltip("Maximum extra emission injected into the existing suspended motes field near the seabed.")]
        [SerializeField, UnityEngine.Range(0f, 48f)] private float bottomSiltEmissionBoost = 14f;
        [Tooltip("How quickly one-shot external bottom-silt bursts decay back into the normal seabed response.")]
        [SerializeField, UnityEngine.Range(0.5f, 12f)] private float bottomSiltBurstRecoverySpeed = 4.5f;

        [Header("â”€â”€â”€ EXHALE BUBBLES â”€â”€â”€")]
        [Tooltip("Emits a short bubble burst on each underwater exhale event from the player movement owner.")]
        [SerializeField] private bool enableExhaleBubbles = true;
        [Tooltip("Minimum burst count in clean shallow water.")]
        [SerializeField, UnityEngine.Range(0, 32)] private int exhaleBubbleMinBurstCount = 7;
        [Tooltip("Burst count ceiling in deeper or murkier water.")]
        [SerializeField, UnityEngine.Range(1, 48)] private int exhaleBubbleMaxBurstCount = 14;
        [Tooltip("Depth at which exhale bubbles reach their full burst density.")]
        [SerializeField, UnityEngine.Range(0.5f, 40f)] private float exhaleBubbleFullDepth = 14f;
        [Tooltip("How strongly turbidity contributes to burst density.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float exhaleBubbleTurbidityWeight = 0.35f;
        [Tooltip("Protects against duplicate exhale events landing in the same short window.")]
        [SerializeField, UnityEngine.Range(0.05f, 1f)] private float exhaleBubbleMinInterval = 0.28f;

        [Header("â”€â”€â”€ SHALLOW SUN BEAM â”€â”€â”€")]
        [Tooltip("Enables the near-camera shallow-water sunshaft proxy.")]
        [SerializeField] private bool enableShallowSunBeam = true;
        [Tooltip("Beam fades in over this first shallow depth range.")]
        [SerializeField, UnityEngine.Range(0.05f, 4f)] private float shallowSunBeamFadeInDepth = 0.75f;
        [Tooltip("Beam is fully faded out by this depth.")]
        [SerializeField, UnityEngine.Range(2f, 40f)] private float shallowSunBeamFadeOutDepth = 16f;
        [Tooltip("Minimum underwater light factor required before the shaft appears.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float shallowSunBeamMinLightFactor = 0.32f;
        [Tooltip("Maximum light intensity pushed into the proxy beam light.")]
        [SerializeField, UnityEngine.Range(0f, 4f)] private float shallowSunBeamMaxLightIntensity = 0.55f;

        [Header("â”€â”€â”€ ECOLOGY RESPONSE â”€â”€â”€")]
        [Tooltip("How strongly fauna mood can thicken underwater suspended particulates.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float ecologySuspendedMotesWeight = 0.28f;
        [Tooltip("How strongly fauna mood can increase exhale bubble burst density.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float ecologyBubbleWeight = 0.18f;
        [Tooltip("How strongly calm/lively fauna space keeps shallow beams readable before deeper fade takes over.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float ecologySunBeamWeight = 0.16f;
        [Header("Adaptive Budget Response")]
        [Tooltip("Scales underwater near-camera dressing from DynamicResolutionScaler render scale so weak devices shed expensive dressing before the frame collapses.")]
        [SerializeField] private bool enableAdaptiveBudgetResponse = true;
        [Tooltip("Render scale at which adaptive dressing reaches its minimum authored budget response.")]
        [SerializeField, UnityEngine.Range(0.5f, 1f)] private float adaptiveBudgetFloorRenderScale = 0.7f;
        [Tooltip("Minimum suspended motes density allowed at the adaptive budget floor.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float adaptiveMotesBudgetFloor = 0.55f;
        [Tooltip("Minimum exhale bubble density allowed at the adaptive budget floor.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float adaptiveBubbleBudgetFloor = 0.6f;
        [Tooltip("Minimum shallow sun-beam intensity allowed at the adaptive budget floor.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float adaptiveBeamBudgetFloor = 0.7f;
        [Tooltip("Minimum shallow caustics intensity allowed at the adaptive budget floor.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float adaptiveCausticsBudgetFloor = 0.72f;
        [Tooltip("Maximum bottom-silt probe interval multiplier at the adaptive budget floor. Higher values reduce probe cadence on weak frames.")]
        [SerializeField, UnityEngine.Range(1f, 4f)] private float adaptiveBottomSiltProbeIntervalMultiplier = 1.8f;

        [Header("Soundscape Tier Response")]
        // Depth-band response stays inside the underwater visual owner instead of a fake global audio owner.
        [Tooltip("Applies authored soundscape depth tiers to underwater fog, ambient, beam, and caustics so each depth band reads like a different water mass.")]
        [SerializeField] private bool enableSoundscapeTierResponse = true;
        [Tooltip("Ambient tint injected only in thermal tier so the abyss stops reading as flat blue-black.")]
        [SerializeField] private Color thermalTierTintColor = new Color(0.22f, 0.1f, 0.03f, 1f);
        [Tooltip("Thermal tier tint blend amount applied to fog and ambient colors.")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float thermalTierTintBlend = 0.28f;
        [Tooltip("Fog density multiplier in twilight tier.")]
        [SerializeField, UnityEngine.Range(0.5f, 2f)] private float twilightTierFogScale = 1.08f;
        [Tooltip("Fog density multiplier in darkness tier.")]
        [SerializeField, UnityEngine.Range(0.5f, 2f)] private float darknessTierFogScale = 1.18f;
        [Tooltip("Fog density multiplier in abyss tier.")]
        [SerializeField, UnityEngine.Range(0.5f, 2f)] private float abyssTierFogScale = 1.32f;
        [Tooltip("Fog density multiplier in deep abyss tier.")]
        [SerializeField, UnityEngine.Range(0.5f, 2f)] private float deepAbyssTierFogScale = 1.48f;
        [Tooltip("Fog density multiplier in thermal tier.")]
        [SerializeField, UnityEngine.Range(0.5f, 2f)] private float thermalTierFogScale = 1.3f;
        [Tooltip("Ambient intensity multiplier in twilight tier.")]
        [SerializeField, UnityEngine.Range(0.25f, 1.5f)] private float twilightTierAmbientScale = 0.94f;
        [Tooltip("Ambient intensity multiplier in darkness tier.")]
        [SerializeField, UnityEngine.Range(0.25f, 1.5f)] private float darknessTierAmbientScale = 0.82f;
        [Tooltip("Ambient intensity multiplier in abyss tier.")]
        [SerializeField, UnityEngine.Range(0.25f, 1.5f)] private float abyssTierAmbientScale = 0.72f;
        [Tooltip("Ambient intensity multiplier in deep abyss tier.")]
        [SerializeField, UnityEngine.Range(0.25f, 1.5f)] private float deepAbyssTierAmbientScale = 0.62f;
        [Tooltip("Ambient intensity multiplier in thermal tier.")]
        [SerializeField, UnityEngine.Range(0.25f, 1.5f)] private float thermalTierAmbientScale = 0.78f;
        [Tooltip("Shallow sun beam intensity multiplier in twilight tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float twilightTierBeamScale = 0.88f;
        [Tooltip("Shallow sun beam intensity multiplier in darkness tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float darknessTierBeamScale = 0.6f;
        [Tooltip("Shallow sun beam intensity multiplier in abyss tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float abyssTierBeamScale = 0.32f;
        [Tooltip("Shallow sun beam intensity multiplier in deep abyss tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float deepAbyssTierBeamScale = 0.14f;
        [Tooltip("Shallow sun beam intensity multiplier in thermal tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float thermalTierBeamScale = 0.16f;
        [Tooltip("Caustics intensity multiplier in twilight tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float twilightTierCausticsScale = 0.92f;
        [Tooltip("Caustics intensity multiplier in darkness tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float darknessTierCausticsScale = 0.58f;
        [Tooltip("Caustics intensity multiplier in abyss tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float abyssTierCausticsScale = 0.28f;
        [Tooltip("Caustics intensity multiplier in deep abyss tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float deepAbyssTierCausticsScale = 0.1f;
        [Tooltip("Caustics intensity multiplier in thermal tier.")]
        [SerializeField, UnityEngine.Range(0f, 1.5f)] private float thermalTierCausticsScale = 0.14f;

        [Header("â•â•â• SURFACE DEFAULTS â•â•â•")]
        [ColorUsage(false)]
        [SerializeField] private Color surfaceFogColor = new Color(0.7f, 0.75f, 0.8f, 1f);
        [SerializeField] private float surfaceFogDensity = 0.001f;
        [SerializeField] private bool enableSurfaceFog = false;
        [ColorUsage(false)]
        [SerializeField] private Color surfaceAmbientColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("â•â•â• UNDERWATER AMBIENT â•â•â•")]
        [ColorUsage(false)]
        [SerializeField] private Color underwaterAmbientColor = new Color(0.02f, 0.04f, 0.06f, 1f);

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  DIAGNOSTICS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â•â•â• DIAGNOSTICS â•â•â•")]
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
        [SerializeField] private float _debugCausticsStrength;
        [SerializeField] private float _debugSuspendedMotesEmission;
        [SerializeField] private float _debugBottomDistance;
        [SerializeField] private float _debugBottomSiltBoost;
        [SerializeField] private int   _debugExhaleBubbleBurstCount;
        [SerializeField] private float _debugShallowSunBeamIntensity;
        [SerializeField] private string _debugFaunaMood = "None";
        [SerializeField] private string _debugFaunaAmbience = "None";
        [SerializeField] private float _debugEcologyMotesMultiplier = 1f;
        [SerializeField] private float _debugEcologyBubbleMultiplier = 1f;
        [SerializeField] private float _debugEcologyBeamMultiplier = 1f;
        [SerializeField] private float _debugAdaptiveRenderScale = 1f;
        [SerializeField] private float _debugAdaptiveBudgetNormalized = 1f;
        [SerializeField] private float _debugAdaptiveMotesScale = 1f;
        [SerializeField] private float _debugAdaptiveBubbleScale = 1f;
        [SerializeField] private float _debugAdaptiveBeamScale = 1f;
        [SerializeField] private float _debugAdaptiveCausticsScale = 1f;
        [SerializeField] private float _debugAdaptiveBottomProbeScale = 1f;
        [SerializeField] private string _debugSoundscapeTier = "Shallow";
        [SerializeField] private float _debugSoundscapeFogScale = 1f;
        [SerializeField] private float _debugSoundscapeAmbientScale = 1f;
        [SerializeField] private float _debugSoundscapeBeamScale = 1f;
        [SerializeField] private float _debugSoundscapeCausticsScale = 1f;
        [SerializeField] private bool  _debugPhysicsEngineFound;
        [SerializeField] private bool  _debugAtmoManagerFound;
        [SerializeField] private bool  _debugPlayerMovementFound;
        [SerializeField] private string _debugPlayerMovementSource = "Unresolved";
        [SerializeField] private bool  _debugSunVisualActive;
        [SerializeField] private float _debugSunScatter;
        [SerializeField] private bool  _debugEditorDriven;
#pragma warning restore CS0414

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SHADER PROPERTY IDs
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private static readonly int _ID_ScatterColourBase =
            Shader.PropertyToID("_ScatterColourBase");
        private static readonly int _ID_ScatterColourShallow =
            Shader.PropertyToID("_ScatterColourShallow");
        private static readonly int _ID_DepthFogDensity =
            Shader.PropertyToID("_DepthFogDensity");
        private static readonly int _ID_Diffuse =
            Shader.PropertyToID("_Diffuse");
        private static readonly int _ID_DiffuseGrazing =
            Shader.PropertyToID("_DiffuseGrazing");
        private static readonly int _ID_DiffuseShadow =
            Shader.PropertyToID("_DiffuseShadow");
        private static readonly int _ID_SubSurfaceColour =
            Shader.PropertyToID("_SubSurfaceColour");
        private static readonly int _ID_SubSurfaceShallowCol =
            Shader.PropertyToID("_SubSurfaceShallowCol");
        private static readonly int _ID_SubSurfaceShallowColShadow =
            Shader.PropertyToID("_SubSurfaceShallowColShadow");
        private static readonly int _ID_SubSurfaceBase =
            Shader.PropertyToID("_SubSurfaceBase");
        private static readonly int _ID_SubSurfaceSun =
            Shader.PropertyToID("_SubSurfaceSun");
        private static readonly int _ID_SubSurfaceSunFallOff =
            Shader.PropertyToID("_SubSurfaceSunFallOff");
        private static readonly int _ID_SubSurfaceDepthMax =
            Shader.PropertyToID("_SubSurfaceDepthMax");
        private static readonly int _ID_SubSurfaceDepthPower =
            Shader.PropertyToID("_SubSurfaceDepthPower");
        private static readonly int _ID_SubSurfaceScattering =
            Shader.PropertyToID("_SubSurfaceScattering");
        private static readonly int _ID_SubSurfaceShallowColour =
            Shader.PropertyToID("_SubSurfaceShallowColour");
        private static readonly int _ID_Underwater =
            Shader.PropertyToID("_Underwater");
        private static readonly int _ID_CullMode =
            Shader.PropertyToID("_CullMode");
        private static readonly int _ID_Caustics =
            Shader.PropertyToID("_Caustics");
        private static readonly int _ID_CausticsStrength =
            Shader.PropertyToID("_CausticsStrength");
        private static readonly int _ID_SunSize =
            Shader.PropertyToID("_SunSize");
        private static readonly int _ID_SunEdgeSoftness =
            Shader.PropertyToID("_SunEdgeSoftness");
        private static readonly int _ID_SunDiscColor =
            Shader.PropertyToID("_SunDiscColor");
        private static readonly int _ID_SunScatterColor =
            Shader.PropertyToID("_SunScatterColor");
        private static readonly int _ID_SkyColorZenith =
            Shader.PropertyToID("_SkyColorZenith");
        private static readonly int _ID_SkyColorHorizon =
            Shader.PropertyToID("_SkyColorHorizon");
        private static readonly int _ID_SkyBase =
            Shader.PropertyToID("_SkyBase");
        private static readonly int _ID_SkyTowardsSun =
            Shader.PropertyToID("_SkyTowardsSun");
        private static readonly int _ID_SkyAwayFromSun =
            Shader.PropertyToID("_SkyAwayFromSun");
        private static readonly int _ID_SkyDirectionality =
            Shader.PropertyToID("_SkyDirectionality");
        private static readonly int _ID_ProceduralSky =
            Shader.PropertyToID("_ProceduralSky");

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  CONSTANTS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private static readonly Color MIN_AMBIENT = new Color(0.01f, 0.02f, 0.03f, 1f);
        private const string ProceduralSkyKeyword = "_PROCEDURALSKY_ON";
        private const string UnderwaterKeyword = "_UNDERWATER_ON";
        private const float SurfaceScatterLuminanceFloor = 0.24f;
        private const float UnderwaterScatterLuminanceFloor = 0.06f;
        private const float SharedOceanUnderwaterScatterLuminanceFloor = 0.48f;
        private const float CrestSkyDirectionality = 0.78f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  RUNTIME STATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private Hecton8.Physics.HectonFluidEngine _physicsEngine;
        private bool _physicsEngineCached;

        private HectonAtmosphereManager _cachedAtmoManager;
        private bool _atmoManagerCached;
        private HectonPlayerMovement _playerMovement;
        private HectonPlayerMovement _subscribedPlayerMovement;
        private Rigidbody _playerRigidbody;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private HectonBiomeProfile _matrixRuntimeVisualProfile;
        private WorldProceduralFaunaMood _currentFaunaMood;
        private string _currentFaunaAmbienceSummary;
        private float _ecologySuspendedMotesMultiplier = 1f;
        private float _ecologyBubbleMultiplier = 1f;
        private float _ecologySunBeamMultiplier = 1f;
        private float _adaptiveBudgetNormalized = 1f;
        private float _adaptiveMotesScale = 1f;
        private float _adaptiveBubbleScale = 1f;
        private float _adaptiveBeamScale = 1f;
        private float _adaptiveCausticsScale = 1f;
        private float _adaptiveBottomSiltProbeIntervalScale = 1f;
        private SoundscapeTier _currentSoundscapeTier = SoundscapeTier.Shallow;
        private float _soundscapeFogDensityScale = 1f;
        private float _soundscapeAmbientScale = 1f;
        private float _soundscapeBeamScale = 1f;
        private float _soundscapeCausticsScale = 1f;
        private float _soundscapeThermalTintBlend = 0f;

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
        private Color _cachedUnderwaterFogColor = new Color(0.06f, 0.18f, 0.28f, 1f);

        private float _baseFlareIntensity;
        private bool  _baseValuesCaptured;

        private Color _baseSunDiscColor;
        private Color _baseSunScatterColor;
        private bool  _baseSkyColorsCaptured;
        private bool  _surfaceWeatherOverrideActive;
        private Color _surfaceWeatherFogColor;
        private float _surfaceWeatherFogDensity;
        private Color _surfaceWeatherAmbientColor;
        private float _surfaceWeatherSunMultiplier = 1f;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _wasUnderwater;
        private DepthZoneProfile _lastDepthZoneProfile;
        private float _submergeImpulseTimer;
        private float _nextThermoclineAllowedTime = float.NegativeInfinity;
        private float _cachedVisualDepth;
        private float _cachedLightFactor;
        private float _cachedCausticsStrength;
        private float _smoothedSunLightFactor = -1f;
        private float _smoothedSunIntensity = -1f;
        private float _cachedSuspendedMotesEmission = -1f;
        private float _cachedBottomDistance = float.PositiveInfinity;
        private float _cachedBottomSiltBoost;
        private float _externalBottomSiltBurstBoost;
        private float _cachedShallowSunBeamLightIntensity = -1f;
        private bool _cachedVisualIsUnderwater;
        private bool _underwaterSuspendedMotesPlaying;
        private bool _shallowSunBeamActive;
        private bool _editorGameplayMainCameraSuppressed;
        private bool _editorGameplaySpaceCameraSuppressed;
        private bool _sunVisualWasDisabled;
        private bool _editorCrestSuppressed;
        private bool _spaceCameraSuppressed;
        private bool _spaceCameraMaskCaptured;
        private float _nextBottomSiltProbeTime = float.NegativeInfinity;
        private float _nextExhaleBubbleAllowedTime = float.NegativeInfinity;
        private float _nextRuntimePlayerCameraResolveTime = float.NegativeInfinity;
        private float _nextRuntimeMainCameraResolveTime = float.NegativeInfinity;
        private float _nextRuntimeReferenceWarningTime = float.NegativeInfinity;
        private float _nextEditorCameraResolveTime = float.NegativeInfinity;
        private const int RuntimeCameraBufferSize = 8;
        private static readonly Camera[] _runtimeCameraBuffer = new Camera[RuntimeCameraBufferSize]; // COLD ALLOC: Camera[8] â€” reusable runtime main-camera resolve buffer to avoid hierarchy array allocations â€” owner: HectonUnderwaterVisuals
        private Camera _gameplayMainCamera;
        private Camera _spaceCamera;
        private Camera _capturedCompositionMainCamera;
        private Camera _capturedCompositionSpaceCamera;
        private CrestOceanRenderer _oceanRenderer;
        private Transform _shallowSunBeamTransform;
        private Vector3 _shallowSunBeamBaseLocalPosition;
        private CrestUnderwaterRenderer _mainCameraUnderwaterRenderer;
        private CrestUnderwaterRenderer _spaceCameraUnderwaterRenderer;
        private bool _cameraCompositionDefaultsCaptured;
        private bool _runtimeCameraStackFallbackActive;
        private int _spaceCameraOriginalCullingMask;
        private int _mainCameraOriginalCullingMask;
        private float _mainCameraOriginalDepth;
        private float _spaceCameraOriginalDepth;
        private CameraClearFlags _mainCameraOriginalClearFlags;
        private CameraRenderType _mainCameraOriginalRenderType;
        private CameraRenderType _spaceCameraOriginalRenderType;
        private const int CelestialLayerIndex = 15;
        private const int _CelestialLayerMask = 1 << CelestialLayerIndex;
#if UNITY_EDITOR
        private CrestUnderwaterRenderer _editorCrestUnderwaterRenderer;
        private CrestUnderwaterRenderer _editorSceneViewUnderwaterRenderer;
        private bool _editorCrestUnderwaterRendererWasEnabled;
        private Camera _editorGameplaySpaceCamera;
        private bool _editorGameplayMainCameraWasEnabled;
        private bool _editorGameplaySpaceCameraWasEnabled;
#endif

        private float _editorSlowTickAccum;
        private readonly RaycastHit[] _bottomSiltProbeHits = new RaycastHit[4]; // COLD ALLOC: RaycastHit[4] â€” reused seafloor probe buffer for underwater bottom-silt gating â€” owner: HectonUnderwaterVisuals

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void OnEnable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
#endif

            if (Application.isPlaying)
            {
                ActiveRuntimeInstance = this;
                _debugEditorDriven = false;
                if (mainCamera != null && !IsRuntimeMainCamera(mainCamera))
                    mainCamera = null;
            }

            ResolvePlayerCamera();
            ResolveMainCamera();
            ResolveTransitionVisorController();
            ResolveUnderwaterParticles();
            ResolveUnderwaterExhaleBubbles();
            ResolveShallowSunBeam();
            ResolveSpaceCamera();
            ValidateReferences();
            CachePhysicsEngine();
            CacheAtmosphereManager();
            CaptureBaseValues();
            CaptureSkyBaseColors();
            InitializeCurrentValues();
            EnsureCrestUnderwaterPassOwnership();
            ApplyCrestMaterial();

            RenderPipelineManager.beginCameraRendering += EnforceFogState;

            if (Application.isPlaying)
            {
                _debugEditorDriven = false;
                EnsureRuntimeVisualOwners();
                EnsureGameplayCameraStackEnabled();
                MapMagicBridge.OnBiomeChanged += HandleBiomeChanged;
                BiomeMatrixDirector.OnMatrixBiomeChanged += HandleMatrixBiomeChanged;
                SoundscapeEvents.OnTierChanged += HandleSoundscapeTierChanged;
                ResolveBiomeMatrixDirector();
                ApplyCurrentMatrixVisualOverride();
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
            _lastDepthZoneProfile = null;
            _nextThermoclineAllowedTime = float.NegativeInfinity;
            _sunVisualWasDisabled = false;
        }

        private void EnsureGameplayCameraStackEnabled()
        {
            if (mainCamera != null && !mainCamera.enabled)
                mainCamera.enabled = true;

            Camera spaceCamera = ResolveValidCameraReference(ref _spaceCamera);
            if (spaceCamera != null && !spaceCamera.enabled)
                spaceCamera.enabled = true;

            ApplyGameplayCameraCompositionMode();
            EnsureCrestUnderwaterPassOwnership();
        }

        private void ApplyGameplayCameraCompositionMode()
        {
            if (!Application.isPlaying || mainCamera == null)
                return;

            ResolveSpaceCamera();
            Camera spaceCamera = ResolveValidCameraReference(ref _spaceCamera);
            if (spaceCamera == null)
                return;

            if (!mainCamera.TryGetComponent(out UniversalAdditionalCameraData mainCameraData) ||
                mainCameraData == null ||
                !spaceCamera.TryGetComponent(out UniversalAdditionalCameraData spaceCameraData) ||
                spaceCameraData == null)
            {
                return;
            }

            CaptureGameplayCameraCompositionDefaults(mainCameraData, spaceCameraData, spaceCamera);

            if (SupportsGameplayCameraStacking(mainCameraData, spaceCameraData))
            {
                RestoreGameplayCameraCompositionDefaults(mainCameraData, spaceCameraData, spaceCamera);
                return;
            }

            ApplyGameplayCameraCompositionFallback(mainCameraData, spaceCameraData, spaceCamera);
        }

        private void CaptureGameplayCameraCompositionDefaults(
            UniversalAdditionalCameraData mainCameraData,
            UniversalAdditionalCameraData spaceCameraData,
            Camera spaceCamera)
        {
            if (_cameraCompositionDefaultsCaptured &&
                ReferenceEquals(_capturedCompositionMainCamera, mainCamera) &&
                ReferenceEquals(_capturedCompositionSpaceCamera, spaceCamera))
            {
                return;
            }

            _capturedCompositionMainCamera = mainCamera;
            _capturedCompositionSpaceCamera = spaceCamera;
            _mainCameraOriginalDepth = mainCamera.depth;
            _spaceCameraOriginalDepth = spaceCamera.depth;
            _mainCameraOriginalClearFlags = mainCamera.clearFlags;
            _mainCameraOriginalRenderType = mainCameraData.renderType;
            _spaceCameraOriginalRenderType = spaceCameraData.renderType;
            _mainCameraOriginalCullingMask = mainCamera.cullingMask;
            _cameraCompositionDefaultsCaptured = true;
        }

        private static bool SupportsGameplayCameraStacking(
            UniversalAdditionalCameraData mainCameraData,
            UniversalAdditionalCameraData spaceCameraData)
        {
            ScriptableRenderer mainRenderer = mainCameraData.scriptableRenderer;
            ScriptableRenderer spaceRenderer = spaceCameraData.scriptableRenderer;

            return mainRenderer != null &&
                   spaceRenderer != null &&
                   mainRenderer.SupportsCameraStackingType(CameraRenderType.Overlay) &&
                   spaceRenderer.SupportsCameraStackingType(CameraRenderType.Base);
        }

        private void ApplyGameplayCameraCompositionFallback(
            UniversalAdditionalCameraData mainCameraData,
            UniversalAdditionalCameraData spaceCameraData,
            Camera spaceCamera)
        {
            if (spaceCameraData.renderType != CameraRenderType.Base)
                spaceCameraData.renderType = CameraRenderType.Base;

            if (mainCameraData.renderType != CameraRenderType.Base)
                mainCameraData.renderType = CameraRenderType.Base;

            float fallbackSpaceDepth = _cameraCompositionDefaultsCaptured ? _spaceCameraOriginalDepth : spaceCamera.depth;
            float fallbackMainDepth = math.max(
                _cameraCompositionDefaultsCaptured ? _mainCameraOriginalDepth : mainCamera.depth,
                fallbackSpaceDepth + 1f);

            if (!Mathf.Approximately(spaceCamera.depth, fallbackSpaceDepth))
                spaceCamera.depth = fallbackSpaceDepth;

            if (!Mathf.Approximately(mainCamera.depth, fallbackMainDepth))
                mainCamera.depth = fallbackMainDepth;

            EnsureFallbackMainCameraCelestialVisibility(mainCamera);
            _runtimeCameraStackFallbackActive = true;
            ApplyRuntimeMainCameraClearFlags(CameraClearFlags.Depth);
        }

        private void RestoreGameplayCameraCompositionDefaults(
            UniversalAdditionalCameraData mainCameraData,
            UniversalAdditionalCameraData spaceCameraData,
            Camera spaceCamera)
        {
            if (!_cameraCompositionDefaultsCaptured)
                return;

            if (spaceCameraData.renderType != _spaceCameraOriginalRenderType)
                spaceCameraData.renderType = _spaceCameraOriginalRenderType;

            if (mainCameraData.renderType != _mainCameraOriginalRenderType)
                mainCameraData.renderType = _mainCameraOriginalRenderType;

            if (!Mathf.Approximately(spaceCamera.depth, _spaceCameraOriginalDepth))
                spaceCamera.depth = _spaceCameraOriginalDepth;

            if (!Mathf.Approximately(mainCamera.depth, _mainCameraOriginalDepth))
                mainCamera.depth = _mainCameraOriginalDepth;

            if (_runtimeCameraStackFallbackActive || mainCamera.clearFlags != _mainCameraOriginalClearFlags)
                mainCamera.clearFlags = _mainCameraOriginalClearFlags;

            if (_cameraCompositionDefaultsCaptured &&
                mainCamera.cullingMask != _mainCameraOriginalCullingMask)
            {
                mainCamera.cullingMask = _mainCameraOriginalCullingMask;
            }

            if (spaceCameraData.renderType == CameraRenderType.Base &&
                mainCameraData.renderType == CameraRenderType.Overlay)
            {
                var stack = spaceCameraData.cameraStack;
                if (stack != null && !stack.Contains(mainCamera))
                    stack.Add(mainCamera);
            }

            _runtimeCameraStackFallbackActive = false;
        }

        private static void EnsureFallbackMainCameraCelestialVisibility(Camera mainCamera)
        {
            if (mainCamera == null || _CelestialLayerMask == 0)
                return;

            int fallbackMask = mainCamera.cullingMask | _CelestialLayerMask;
            if (mainCamera.cullingMask != fallbackMask)
                mainCamera.cullingMask = fallbackMask;
        }

        private void ApplyRuntimeMainCameraClearFlags(CameraClearFlags desiredFlags)
        {
            if (mainCamera == null)
                return;

            CameraClearFlags appliedFlags = _runtimeCameraStackFallbackActive
                ? CameraClearFlags.Depth
                : desiredFlags;

            if (mainCamera.clearFlags != appliedFlags)
                mainCamera.clearFlags = appliedFlags;
        }

        private void Start()
        {
            if (!Application.isPlaying) return;

#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
#endif
            _debugEditorDriven = false;
            EnsureRuntimeVisualOwners();
            EnsureGameplayCameraStackEnabled();

            if (!_registeredTick || !_registeredSlowTick)
                TryRegisterTickManagers();

            if (!_physicsEngineCached)
                CachePhysicsEngine();

            if (!_atmoManagerCached)
                CacheAtmosphereManager();

            if (biomeMatrixDirector == null)
                ResolveBiomeMatrixDirector();

            ApplyCurrentMatrixVisualOverride();
            EnsureRuntimeVisualOwners();
            EnsureCrestUnderwaterPassOwnership();
            ApplyCrestMaterial();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= EnforceFogState;

            if (Application.isPlaying)
            {
                if (ActiveRuntimeInstance == this)
                    ActiveRuntimeInstance = null;

                MapMagicBridge.OnBiomeChanged -= HandleBiomeChanged;
                BiomeMatrixDirector.OnMatrixBiomeChanged -= HandleMatrixBiomeChanged;
                SoundscapeEvents.OnTierChanged -= HandleSoundscapeTierChanged;

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

#if UNITY_EDITOR
            ResumeEditorWaterRendering();
#endif
            _lastDepthZoneProfile = null;
            _nextThermoclineAllowedTime = float.NegativeInfinity;
            _cachedBottomDistance = float.PositiveInfinity;
            _cachedBottomSiltBoost = 0f;
            _nextBottomSiltProbeTime = float.NegativeInfinity;
            _nextExhaleBubbleAllowedTime = float.NegativeInfinity;
            UnsubscribePlayerMovement(_subscribedPlayerMovement);
            DisableUnderwaterSuspendedMotes(true);
            DisableUnderwaterExhaleBubbles(true);
            DisableShallowSunBeam(true);
            RestoreBaseValues();
            RestoreSunVisual();
            RestoreSpaceCameraDefaults();
            RestoreCameraDefaults();
            RestoreSkyMaterialDefaults();
            Shader.SetGlobalVector(_SargassumCanopyShadowParamsId, Vector4.zero);
            Shader.SetGlobalVector(_SargassumCanopyLightingParamsId, new Vector4(0f, 0f, 1f, 0f));
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  EDITOR UPDATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (this == null) return;

            if (Application.isPlaying)
            {
                ResumeEditorWaterRendering();
                return;
            }

            if (!IsEditorPreviewActive())
            {
                SuspendEditorWaterRendering();
                return;
            }

            ResolveEditorCamera();

            if (ShouldSuppressEditorGameplayCrest())
            {
                SuspendEditorWaterRendering();
            }
            else
            {
                ResumeEditorWaterRendering();
            }

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

        private static bool IsEditorPreviewActive()
        {
            return !EditorApplication.isCompiling &&
                   !EditorApplication.isUpdating;
        }

        private void ResolveEditorCamera()
        {
            if (Application.isPlaying) return;

            var sv = SceneView.lastActiveSceneView;
            Camera sceneViewCamera = sv != null ? sv.camera : null;
            ResolveGameplayMainCameraForEditor();

            Camera authoredGameplayCamera = IsRuntimeMainCamera(_gameplayMainCamera)
                ? _gameplayMainCamera
                : null;
            if (authoredGameplayCamera == null && IsRuntimeMainCamera(mainCamera))
            {
                authoredGameplayCamera = mainCamera;
            }
            else if (authoredGameplayCamera == null && playerCamera != null)
            {
                Camera playerOwnedCamera = playerCamera.GetComponent<Camera>();
                if (IsRuntimeMainCamera(playerOwnedCamera))
                    authoredGameplayCamera = playerOwnedCamera;
            }

            if (sceneViewCamera != null)
            {
                if (mainCamera != sceneViewCamera)
                    mainCamera = sceneViewCamera;
                if (!ReferenceEquals(playerCamera, sceneViewCamera.transform))
                    playerCamera = sceneViewCamera.transform;
                _nextEditorCameraResolveTime = float.NegativeInfinity;
                return;
            }

            if (authoredGameplayCamera != null)
            {
                if (!ReferenceEquals(mainCamera, authoredGameplayCamera))
                    mainCamera = authoredGameplayCamera;
                if (!ReferenceEquals(playerCamera, authoredGameplayCamera.transform))
                    playerCamera = authoredGameplayCamera.transform;

                _nextEditorCameraResolveTime = float.NegativeInfinity;
                return;
            }

            if (Time.realtimeSinceStartup < _nextEditorCameraResolveTime)
                return;

            _nextEditorCameraResolveTime = Time.realtimeSinceStartup + EditorCameraResolveRetryInterval;

            if (mainCamera != null)
            {
                if (playerCamera == null)
                    playerCamera = mainCamera.transform;
                return;
            }

            ResolveGameplayMainCameraForEditor();
            if (_gameplayMainCamera == null)
                return;

            mainCamera = _gameplayMainCamera;
            playerCamera = _gameplayMainCamera.transform;
        }

        private bool ShouldSuppressEditorGameplayCrest()
        {
            if (Application.isPlaying)
                return false;

            ResolveGameplayMainCameraForEditor();
            if (_gameplayMainCamera == null)
                return false;

            if (!ReferenceEquals(mainCamera, _gameplayMainCamera))
                return false;

            float cameraDepth = math.max(
                0f,
                ResolveWaterLevel() - _gameplayMainCamera.transform.position.y);
            return cameraDepth < VisualForcedUnderwaterDepth;
        }

        private void SuspendEditorWaterRendering()
        {
            _debugEditorDriven = false;

            if (_editorCrestSuppressed)
                return;

            ResolveGameplayMainCameraForEditor();
            if (_gameplayMainCamera == null)
                return;

            if (!_gameplayMainCamera.TryGetComponent(out _editorCrestUnderwaterRenderer) ||
                _editorCrestUnderwaterRenderer == null)
            {
                _editorCrestUnderwaterRenderer = null;
            }

            if (_editorCrestUnderwaterRenderer != null)
            {
                _editorCrestUnderwaterRendererWasEnabled = _editorCrestUnderwaterRenderer.enabled;
                if (_editorCrestUnderwaterRendererWasEnabled)
                    _editorCrestUnderwaterRenderer.enabled = false;

                _editorCrestSuppressed = _editorCrestUnderwaterRendererWasEnabled;
            }

            // Do not suppress gameplay cameras in edit mode. The Game view must
            // remain usable for live preview and tooling.
            _editorGameplayMainCameraWasEnabled = false;
            _editorGameplayMainCameraSuppressed = false;
            _editorGameplaySpaceCameraWasEnabled = false;
            _editorGameplaySpaceCameraSuppressed = false;
        }

        private void ResumeEditorWaterRendering()
        {
            if (_editorGameplaySpaceCameraSuppressed &&
                IsCameraReferenceValid(_editorGameplaySpaceCamera) &&
                _editorGameplaySpaceCameraWasEnabled &&
                !_editorGameplaySpaceCamera.enabled)
            {
                _editorGameplaySpaceCamera.enabled = true;
            }

            _editorGameplaySpaceCameraSuppressed = false;
            _editorGameplaySpaceCameraWasEnabled = false;

            if (_editorGameplayMainCameraSuppressed &&
                _gameplayMainCamera != null &&
                _editorGameplayMainCameraWasEnabled &&
                !_gameplayMainCamera.enabled)
            {
                _gameplayMainCamera.enabled = true;
            }

            _editorGameplayMainCameraSuppressed = false;
            _editorGameplayMainCameraWasEnabled = false;

            if (!_editorCrestSuppressed)
                return;

            if (_editorCrestUnderwaterRenderer != null &&
                _editorCrestUnderwaterRendererWasEnabled &&
                !_editorCrestUnderwaterRenderer.enabled)
            {
                _editorCrestUnderwaterRenderer.enabled = true;
            }

            _editorCrestSuppressed = false;
            _editorCrestUnderwaterRendererWasEnabled = false;
        }
#endif

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ITickable.Tick â€” PER-FRAME
        //
        //  v5.1: By the time this runs, AtmosphereManager has already
        //  computed fresh ProfileSunIntensity and ComputedHorizonFade.
        //  We read those values and combine with depth factor.
        //  CelestialEngine will run AFTER us and multiply by eclipse.
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void Tick(float deltaTime)
        {
            EnsureRuntimeVisualOwners();
            DecayExternalBottomSiltBurst(deltaTime);

            if (playerCamera == null)
            {
                ResolvePlayerCamera();
                if (playerCamera == null) return;
            }

            float depth = ResolveCurrentDepth();
            bool isUnderwater = ResolveUnderwaterVisualState(depth);

            ApplySpaceCameraDepthState(depth, isUnderwater);

            UpdateDepthDiagnostics(depth, isUnderwater);
            UpdateSubmergeImpulse(deltaTime);
            RefreshAdaptiveBudgetResponse();
            _cachedVisualDepth = depth;
            _cachedVisualIsUnderwater = isUnderwater;
            UpdateTransportCockpitOverlay();
            TryHandleThermoclineTransition(isUnderwater);

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  ABOVE WATER
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            if (!isUnderwater)
            {
                if (_wasUnderwater)
                {
                    TriggerSurfaceBreakImpulse();
                    _cachedLightFactor = 1f;
                    _cachedCausticsStrength = 0f;
                    ApplySurfaceDefaults();
                    _wasUnderwater = false;
                }

                UpdateUnderwaterSuspendedMotes(depth, 1f, 0f, false);
                DisableUnderwaterExhaleBubbles(true);
                UpdateShallowSunBeam(depth, 1f, false, Vector3.zero, 1f, 0f);
                ApplySargassumCanopyShaderGlobals(default);

                // â”€â”€ v5.1: Write sunLight.intensity EVERY FRAME above water â”€â”€
                // This is the "base" value that CelestialEngine will multiply
                // by eclipse visibility in its Tick() (which runs after ours).
                //
                // profile Ã— horizon gives the correct sunset/sunrise dimming.
                // CelestialEngine then applies: intensity *= (1 - eclipseOcclusion)
                //
                // Guard: skip only if AtmosphereManager hasn't computed yet
                // (both values would be at their defaults = 1.0, which is fine).
                if (sunLight != null)
                {
                    float targetSunIntensity;
                    if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState surfaceState))
                    {
                        targetSunIntensity = surfaceState.DirectionalLightIntensity;
                        UpdateSurfaceLightDiagnostics(
                            surfaceState.DirectionalLightIntensity,
                            1f,
                            targetSunIntensity);
                    }
                    else
                    {
                        float baseSun = ResolveProfileSunIntensity();
                        float horizon = ResolveHorizonFade();
                        targetSunIntensity = baseSun * horizon * ResolveSurfaceSunMultiplier();

                        UpdateSurfaceLightDiagnostics(
                            baseSun,
                            horizon,
                            targetSunIntensity);
                    }

                    float smoothedLightFactor = SmoothSunState(targetSunIntensity, 1f, deltaTime);
                    _cachedLightFactor = smoothedLightFactor;
                    ApplySunVisualState(smoothedLightFactor);
                    ApplySunScattering(smoothedLightFactor);
                    ApplySunColorFade(smoothedLightFactor);
                }

                return;
            }

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  ENTERING WATER
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            if (!_wasUnderwater)
            {
                RenderSettings.fog     = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                TriggerSubmergeImpulse();
                _wasUnderwater = true;
            }

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  UNDERWATER â€” DEPTH-DRIVEN
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            SargassumGlobalDragManager.SargassumFieldSample sargassumCanopySample = ResolveSargassumCanopySample();
            float canopyOcclusion01 = sargassumCanopySample.Occlusion01;
            float canopyWindow01 = sargassumCanopySample.Window01;
            float lightFactor = ResolveDepthLightFactor(depth);
            float submergeImpulse = EvaluateSubmergeImpulse(depth);
            lightFactor *= 1f - (submergeDarkenStrength * submergeImpulse);
            lightFactor *= 1f - (canopyOcclusion01 * sargassumCanopyLightOcclusionStrength);
            _cachedLightFactor = lightFactor;
            _cachedCausticsStrength = ResolveCausticsStrength(depth, lightFactor, isUnderwater);
            UpdateUnderwaterSuspendedMotes(depth, lightFactor, submergeImpulse, true);
            UpdateShallowSunBeam(depth, lightFactor, true, sargassumCanopySample.AnchorWS, canopyWindow01, canopyOcclusion01);
            ApplySargassumCanopyShaderGlobals(sargassumCanopySample);

            // â”€â”€ Sun intensity = profile Ã— horizon Ã— depthCurve â”€â”€
            float baseSunIntensity = ResolveProfileSunIntensity();
            float horizonFade = ResolveHorizonFade();
            float finalSunIntensity = baseSunIntensity * horizonFade * lightFactor;

            float smoothedUnderwaterLightFactor = SmoothSunState(finalSunIntensity, lightFactor, deltaTime);
            float appliedSunIntensity = _smoothedSunIntensity;
            ApplySunVisualState(smoothedUnderwaterLightFactor);
            ApplySunScattering(smoothedUnderwaterLightFactor);
            ApplySunColorFade(smoothedUnderwaterLightFactor);
            ApplyUnderwaterFog(lightFactor, depth, submergeImpulse, canopyOcclusion01);
            ApplyUnderwaterAmbient(canopyOcclusion01);
            ApplyUnderwaterCamera();

            UpdateLightDiagnostics(lightFactor, baseSunIntensity, horizonFade, appliedSunIntensity);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ISlowTickable.SlowTick â€” 2Hz
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void SlowTick()
        {
            if (Application.isPlaying)
            {
                ResolvePlayerCamera();
                ResolveMainCamera();
                ResolveSunVisualTransform();
                WarnIfRuntimeReferencesStillMissing();
            }

            if (playerCamera == null) return;

            RefreshTargetsFromCurrentProfile();
            RefreshSoundscapeTierResponse(false);

            float lerpT = math.saturate(biomeTransitionSpeed * slowTickInterval);
            InterpolateBiomeParameters(lerpT);

            ApplyCrestMaterial();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ATMOSPHERE MANAGER INTEGRATION
        //
        //  v5.1: ResolveHorizonFade now reads the PRECOMPUTED value
        //  from AtmosphereManager instead of recalculating.
        //  This ensures ONE source of truth for the horizon curve.
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private float ResolveProfileSunIntensity()
        {
            if (!_atmoManagerCached)
                CacheAtmosphereManager();

            if (_cachedAtmoManager != null)
            {
                float profileSunIntensity = _cachedAtmoManager.ProfileSunIntensity;
                if (profileSunIntensity > 0.0001f)
                    return profileSunIntensity;

                float currentSunIntensity = _cachedAtmoManager.CurrentSunIntensity;
                if (currentSunIntensity > 0.0001f)
                    return currentSunIntensity;
            }

            // Fallback: no atmosphere manager = sun at full intensity
            if (sunLight != null && sunLight.intensity > 0.0001f)
                return sunLight.intensity;

            return 1f;
        }

        /// <summary>
        /// v5.1 FIX: Reads AtmosphereManager.ComputedHorizonFade directly.
        ///
        /// OLD (v5.0): Recalculated from SunElevation with its own fadeAngle.
        ///   Problem: different fadeAngle â†’ different curve â†’ desync with
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

        private float ResolveDepthLightFactor(float depth)
        {
            if (depth <= 0f)
                return 1f;

            if (!useBeerLambertDepthAttenuation)
                return math.saturate(globalLightCurve.Evaluate(depth));

            float effectiveDepth = math.max(0f, depth - beerLambertSurfaceClarityDepth);
            if (effectiveDepth <= 0f)
                return 1f;

            float daylightVisibility = ResolveSurfaceDaylightVisibility();
            float extinction = ResolveBeerLambertExtinction();
            if (daylightVisibility > 0.001f)
            {
                float readableBandFade = 1f - math.saturate(
                    (depth - DaylightReadableDepth) /
                    math.max(1f, beerLambertBlackoutDepth - DaylightReadableDepth));
                extinction *= Mathf.Lerp(
                    1f,
                    DaylightReadableExtinctionReduction,
                    daylightVisibility * readableBandFade);
            }

            float transmittance = math.exp(-extinction * effectiveDepth);
            if (daylightVisibility > 0.001f)
            {
                float readabilityDepthT = math.saturate(
                    (depth - beerLambertSurfaceClarityDepth) /
                    math.max(1f, DaylightReadableDepth - beerLambertSurfaceClarityDepth));
                float readabilityBlackoutFade = 1f - math.saturate(
                    (depth - DaylightReadableDepth) /
                    math.max(1f, beerLambertBlackoutDepth - DaylightReadableDepth));
                float readabilityFloor = Mathf.Lerp(
                    DaylightReadableLightFloor,
                    DaylightReadableLightFloor * 0.72f,
                    readabilityDepthT);
                readabilityFloor *= daylightVisibility * readabilityBlackoutFade;
                transmittance = math.max(transmittance, readabilityFloor);
            }

            if (depth >= beerLambertBlackoutDepth &&
                transmittance <= beerLambertBlackoutThreshold)
            {
                return 0f;
            }

            return math.saturate(transmittance);
        }

        private float ResolveBeerLambertExtinction()
        {
            Vector3 depthFogDensity = _currentDepthFogDensity;
            float luminance =
                (depthFogDensity.x * 0.2126f) +
                (depthFogDensity.y * 0.7152f) +
                (depthFogDensity.z * 0.0722f);

            float extinction = luminance * math.max(0.1f, beerLambertExtinctionScale) * GameplayReadableBeerLambertExtinctionBias;
            extinction *= math.max(0.5f, _currentTurbidity);

            return math.max(0.0001f, extinction);
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SUN INTENSITY
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private float SmoothSunState(float targetIntensity, float targetLightFactor, float deltaTime)
        {
            if (_smoothedSunIntensity < 0f)
                _smoothedSunIntensity = targetIntensity;

            if (_smoothedSunLightFactor < 0f)
                _smoothedSunLightFactor = targetLightFactor;

            float brightenT = 1f - math.exp(-sunStateBrightenSpeed * deltaTime);
            float darkenT = 1f - math.exp(-sunStateDarkenSpeed * deltaTime);

            float intensityT = targetIntensity >= _smoothedSunIntensity ? brightenT : darkenT;
            float lightFactorT = targetLightFactor >= _smoothedSunLightFactor ? brightenT : darkenT;

            _smoothedSunIntensity = math.lerp(_smoothedSunIntensity, targetIntensity, intensityT);
            _smoothedSunLightFactor = math.lerp(_smoothedSunLightFactor, targetLightFactor, lightFactorT);

            ApplySunIntensityImmediate(_smoothedSunIntensity, _smoothedSunLightFactor);
            return _smoothedSunLightFactor;
        }

        private float ApplySunIntensityImmediate(float finalIntensity, float lightFactor)
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

            return finalIntensity;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SUN VISUAL DISC
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ApplySunVisualState(float lightFactor)
        {
            if (sunVisualTransform == null) return;

            if (_cachedAtmoManager != null)
            {
                HideSunVisualAboveWater();
                return;
            }

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
            if (_cachedAtmoManager != null)
            {
                HideSunVisualAboveWater();
                return;
            }

            if (sunVisualTransform != null && _sunVisualWasDisabled)
            {
                sunVisualTransform.gameObject.SetActive(true);
                _sunVisualWasDisabled = false;
            }
        }

        private void HideSunVisualAboveWater()
        {
            if (sunVisualTransform == null) return;

            if (sunVisualTransform.gameObject.activeSelf)
                sunVisualTransform.gameObject.SetActive(false);

            _sunVisualWasDisabled = true;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SUN SCATTERING
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SUN DISC / SCATTER COLOR FADE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  FOG
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private Color ResolveSurfaceFogColor()
        {
            if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState state))
            {
                Color stateFogColor = state.FogColor;
                stateFogColor.a = 1f;
                return stateFogColor;
            }

            Color skyHorizonColor = Shader.GetGlobalColor(_ID_SkyColorHorizon);
            if (skyHorizonColor.maxColorComponent > 0.0001f)
            {
                skyHorizonColor.a = 1f;
                return skyHorizonColor;
            }

            return _surfaceWeatherOverrideActive
                ? _surfaceWeatherFogColor
                : surfaceFogColor;
        }

        private float ResolveSurfaceFogDensity()
        {
            if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState state))
                return Mathf.Max(0.0001f, state.FogDensity);

            return _surfaceWeatherOverrideActive
                ? _surfaceWeatherFogDensity
                : surfaceFogDensity;
        }

        private Color ResolveSurfaceAmbientColor()
        {
            if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState state))
            {
                Color surfaceAmbient = Color.Lerp(state.AmbientEquatorColor, state.AmbientSkyColor, 0.35f);
                surfaceAmbient.a = 1f;
                return surfaceAmbient;
            }

            if (_surfaceWeatherOverrideActive)
                return _surfaceWeatherAmbientColor;

            Color skyAmbient = RenderSettings.ambientSkyColor;
            if (skyAmbient.maxColorComponent <= 0.0001f)
                skyAmbient = ResolveSurfaceSkyZenithColor();

            if (skyAmbient.maxColorComponent <= 0.0001f)
                return surfaceAmbientColor;

            Color blendedAmbient = Color.Lerp(surfaceAmbientColor, skyAmbient, 0.72f);
            blendedAmbient.a = 1f;
            return blendedAmbient;
        }

        private float ResolveSurfaceSunMultiplier()
        {
            return _surfaceWeatherOverrideActive
                ? _surfaceWeatherSunMultiplier
                : 1f;
        }

        private void ApplyUnderwaterFog(float lightFactor, float currentDepth, float submergeImpulse, float canopyOcclusion01)
        {
            Color fogColor = ResolveUnderwaterFogColor(lightFactor, currentDepth);
            if (canopyOcclusion01 > 0.0001f)
            {
                float canopyColorBlend = canopyOcclusion01 * 0.55f;
                fogColor = Color.Lerp(fogColor, fogColor * 0.54f, canopyColorBlend);
                fogColor.a = 1f;
            }
            float effectiveSurfaceFogBlendDepth = math.min(surfaceFogBlendDepth, MaxSurfaceFogBlendDepth);
            float surfaceBlend = 1f - math.saturate(
                currentDepth / math.max(0.01f, effectiveSurfaceFogBlendDepth));
            surfaceBlend *= surfaceBlend;
            if (surfaceBlend > 0f)
            {
                fogColor = Color.Lerp(fogColor, ResolveSurfaceFogColor(), surfaceBlend);
                fogColor.a = 1f;
            }

            _cachedUnderwaterFogColor = fogColor;
            RenderSettings.fogColor = fogColor;

            float baseDensity = Mathf.Lerp(maxFogDensity, minFogDensity, lightFactor);
            float targetDensity = baseDensity * _currentTurbidity;
            targetDensity *= _soundscapeFogDensityScale;
            targetDensity *= 1f + (submergeFogBoost * submergeImpulse);
            targetDensity *= 1f + (canopyOcclusion01 * sargassumCanopyFogBoost);
            float shallowDensityFloor = Mathf.Lerp(
                UnderwaterFogDensityFloorNearSurface,
                UnderwaterFogDensityFloorAtDepth,
                math.saturate(currentDepth / UnderwaterFogDensityFloorDepth));
            targetDensity = math.max(targetDensity, shallowDensityFloor);
            targetDensity += UnderwaterBaselineDistanceHaze * math.max(0.85f, _currentTurbidity);
            float farHazeBlend = math.saturate(
                (currentDepth - UnderwaterFarHazeStartDepth) /
                math.max(0.01f, UnderwaterFarHazeFullDepth - UnderwaterFarHazeStartDepth));
            targetDensity += UnderwaterFarHazeDensityBoost * _currentTurbidity * farHazeBlend;
            float depthColumnHazeBlend = math.saturate(currentDepth / UnderwaterDepthColumnHazeFullDepth);
            targetDensity += UnderwaterDepthColumnHazeDensityBoost *
                Mathf.Lerp(0.75f, 1f, ResolveSurfaceDaylightVisibility()) *
                _currentTurbidity *
                depthColumnHazeBlend;

            float smoothSubmerge = math.saturate(currentDepth / 0.5f);
            float surfDensity = enableSurfaceFog ? surfaceFogDensity : 0.0001f;
            if (_surfaceWeatherOverrideActive)
                surfDensity = ResolveSurfaceFogDensity();

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

            if (Application.isPlaying)
            {
                _debugEditorDriven = false;

                if (!_registeredTick || !_registeredSlowTick)
                    TryRegisterTickManagers();

                if (mainCamera == null ||
                    !IsRuntimeMainCamera(mainCamera) ||
                    _mainCameraUnderwaterRenderer == null)
                {
                    EnsureRuntimeVisualOwners();
                    EnsureGameplayCameraStackEnabled();
                }
            }

            bool renderUnderwater = ShouldRenderUnderwaterFogForCamera(cam);

            if (renderUnderwater)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = _cachedUnderwaterFogColor;
                RenderSettings.fogDensity = ResolvePerCameraUnderwaterFogDensity(cam);
            }
            else if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState state))
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = state.FogColor;
                RenderSettings.fogDensity = state.FogDensity;
            }
            else
            {
                if (enableSurfaceFog)
                {
                    RenderSettings.fog = true;
                    RenderSettings.fogColor = ResolveSurfaceFogColor();
                    RenderSettings.fogDensity = ResolveSurfaceFogDensity();
                }
                else
                {
                    RenderSettings.fog = false;
                }
            }
        }

        private float ResolvePerCameraUnderwaterFogDensity(Camera cam)
        {
            if (cam == null)
                return _cachedFogDensity;

            if (cam.cameraType != CameraType.SceneView || Application.isPlaying)
                return _cachedFogDensity;

            float surfaceDensity = enableSurfaceFog ? surfaceFogDensity : 0.0001f;
            if (_surfaceWeatherOverrideActive)
                surfaceDensity = ResolveSurfaceFogDensity();

            float sceneViewDensityScale = math.min(sceneViewUnderwaterFogDensityScale, MaxSceneViewUnderwaterFogDensityScale);
            return Mathf.Lerp(surfaceDensity, _cachedFogDensity, sceneViewDensityScale);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  AMBIENT / CAMERA
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ApplyUnderwaterAmbient(float canopyOcclusion01)
        {
            Color effectiveAmbient = ResolveUnderwaterAmbientColor();
            if (canopyOcclusion01 > 0.0001f)
            {
                float canopyDarken = 1f - (canopyOcclusion01 * sargassumCanopyAmbientOcclusionStrength);
                effectiveAmbient.r *= canopyDarken;
                effectiveAmbient.g *= canopyDarken;
                effectiveAmbient.b *= canopyDarken;
            }
            Color ambient;
            ambient.r = math.max(effectiveAmbient.r, MIN_AMBIENT.r);
            ambient.g = math.max(effectiveAmbient.g, MIN_AMBIENT.g);
            ambient.b = math.max(effectiveAmbient.b, MIN_AMBIENT.b);
            ambient.a = 1f;

            if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState surfaceState) &&
                surfaceState.IsValid)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;

                float depthBlend = math.saturate(_cachedVisualDepth / UnderwaterDaylightSeaTintDepth);
                Color skyAmbient = MaxColorRgb(
                    Color.Lerp(surfaceState.AmbientSkyColor, ambient, Mathf.Lerp(0.18f, 0.34f, depthBlend)),
                    ScaleColorRgb(ambient, 0.78f));
                Color equatorAmbient = MaxColorRgb(
                    Color.Lerp(surfaceState.AmbientEquatorColor, ambient, Mathf.Lerp(0.26f, 0.5f, depthBlend)),
                    ambient);
                Color groundAmbient = MaxColorRgb(
                    Color.Lerp(surfaceState.AmbientGroundColor, ambient, Mathf.Lerp(0.34f, 0.62f, depthBlend)),
                    ScaleColorRgb(ambient, 0.88f));

                skyAmbient.a = 1f;
                equatorAmbient.a = 1f;
                groundAmbient.a = 1f;

                RenderSettings.ambientSkyColor = skyAmbient;
                RenderSettings.ambientEquatorColor = equatorAmbient;
                RenderSettings.ambientGroundColor = groundAmbient;
                RenderSettings.ambientIntensity = Mathf.Max(surfaceState.AmbientIntensity, 0.55f);
                return;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
        }

        private void ApplyUnderwaterCamera()
        {
            if (mainCamera == null) return;
            mainCamera.backgroundColor = _cachedUnderwaterFogColor;
            CameraClearFlags underwaterClearFlags =
                _mainCameraUnderwaterRenderer != null && _mainCameraUnderwaterRenderer.IsActive
                    ? CameraClearFlags.Skybox
                    : CameraClearFlags.SolidColor;
            ApplyRuntimeMainCameraClearFlags(underwaterClearFlags);
        }

        private SargassumGlobalDragManager.SargassumFieldSample ResolveSargassumCanopySample()
        {
            SargassumGlobalDragManager.SargassumFieldSample sample = default;
            sample.Window01 = 1f;
            if (!enableSargassumCanopyLighting)
                return sample;

            SargassumGlobalDragManager dragManager = SargassumGlobalDragManager.Instance;
            if (dragManager == null)
                return sample;

            Transform sampleTransform = playerCamera != null ? playerCamera : mainCamera != null ? mainCamera.transform : null;
            if (sampleTransform == null)
                return sample;

            dragManager.SampleDetailedInfluence(sampleTransform.position, 1.15f, 0f, out sample);
            return sample;
        }

        private void ApplySargassumCanopyShaderGlobals(SargassumGlobalDragManager.SargassumFieldSample sample)
        {
            if (!enableSargassumCanopyLighting || !sample.HasInfluence)
            {
                Shader.SetGlobalVector(_SargassumCanopyShadowParamsId, Vector4.zero);
                Shader.SetGlobalVector(_SargassumCanopyLightingParamsId, new Vector4(0f, 0f, 1f, 0f));
                return;
            }

            float inverseRadius = 1f / math.max(0.01f, sargassumCanopyShadowRadius);
            Shader.SetGlobalVector(
                _SargassumCanopyShadowParamsId,
                new Vector4(
                    sample.AnchorWS.x,
                    sample.AnchorWS.z,
                    inverseRadius,
                    sample.Occlusion01));
            Shader.SetGlobalVector(
                _SargassumCanopyLightingParamsId,
                new Vector4(
                    sample.Density01,
                    sample.Occlusion01,
                    sample.Window01,
                    1f));
        }

        private Color ResolveUnderwaterAmbientColor()
        {
            Color ambientColor = _currentAmbientColor;
            ambientColor.r *= _soundscapeAmbientScale;
            ambientColor.g *= _soundscapeAmbientScale;
            ambientColor.b *= _soundscapeAmbientScale;

            float currentDepth = ResolveCurrentDepth();
            float daylightVisibility = ResolveSurfaceDaylightVisibility();
            float sunlitAmbientBlend =
                daylightVisibility *
                (1f - math.saturate(currentDepth / UnderwaterSunlitAmbientDepth));
            if (sunlitAmbientBlend > 0.0001f)
            {
                Color skylitWaterAmbient = Color.Lerp(
                    _currentScatterShallow,
                    ResolveSurfaceSkyZenithColor(),
                    0.18f);
                skylitWaterAmbient.a = 1f;
                ambientColor = Color.Lerp(
                    ambientColor,
                    skylitWaterAmbient,
                    sunlitAmbientBlend * UnderwaterSunlitAmbientStrength);
            }

            float daylightSeaAmbientBlend = daylightVisibility *
                (1f - math.saturate(currentDepth / UnderwaterDaylightSeaTintDepth));
            if (daylightSeaAmbientBlend > 0.0001f)
            {
                float shallowSeaBlend = 1f - math.saturate(currentDepth / UnderwaterSunlitAmbientDepth);
                Color daylightSeaAmbient = Color.Lerp(
                    UnderwaterDaylightSeaTintMid,
                    UnderwaterDaylightSeaTintShallow,
                    shallowSeaBlend);
                daylightSeaAmbient = MaxColorRgb(
                    daylightSeaAmbient,
                    ScaleColorRgb(_currentScatterShallow, 0.72f));
                ambientColor = Color.Lerp(
                    ambientColor,
                    daylightSeaAmbient,
                    daylightSeaAmbientBlend * UnderwaterDaylightAmbientTintStrength);
            }

            if (_soundscapeThermalTintBlend > 0.001f)
                ambientColor = Color.Lerp(ambientColor, thermalTierTintColor, _soundscapeThermalTintBlend);

            ambientColor.a = 1f;
            return ambientColor;
        }

        private Color ResolveUnderwaterFogColor(float lightFactor, float currentDepth)
        {
            Color fogColor = ResolveBaseUnderwaterFogColor();
            float daylightVisibility = ResolveSurfaceDaylightVisibility();
            float shallowColumnBlend = daylightVisibility *
                (1f - math.saturate(currentDepth / UnderwaterShallowColumnColorDepth));
            if (shallowColumnBlend > 0.0001f)
            {
                Color shallowColumnColor = Color.Lerp(
                    _currentScatterShallow,
                    ResolveSurfaceSkyZenithColor(),
                    0.18f);
                shallowColumnColor.a = 1f;
                fogColor = Color.Lerp(
                    fogColor,
                    shallowColumnColor,
                    shallowColumnBlend * UnderwaterShallowColumnColorStrength);
            }

            float fogBlackoutStartDepth = Mathf.Lerp(
                FogBlackoutStartDepthNight,
                FogBlackoutStartDepthDay,
                daylightVisibility);

            float blackoutRange = math.max(
                1f,
                beerLambertBlackoutDepth - fogBlackoutStartDepth);
            float depthBlackBlend = math.saturate(
                (currentDepth - fogBlackoutStartDepth) / blackoutRange);
            depthBlackBlend *= depthBlackBlend;

            float extinctionBlend =
                math.saturate(1f - lightFactor) *
                math.saturate(
                    (currentDepth - beerLambertSurfaceClarityDepth) /
                    math.max(1f, beerLambertBlackoutDepth - beerLambertSurfaceClarityDepth));
            extinctionBlend *= Mathf.Lerp(1f, 0.45f, daylightVisibility);

            float deepBlackBlend = math.max(depthBlackBlend, extinctionBlend);
            if (deepBlackBlend <= 0.0001f)
            {
                fogColor.a = 1f;
                return fogColor;
            }

            Color abyssColor = new Color(0.004f, 0.008f, 0.016f, 1f);
            fogColor = Color.Lerp(fogColor, abyssColor, deepBlackBlend * FogBlackBlendIntensity);
            fogColor.a = 1f;
            return fogColor;
        }

        private float ResolveSurfaceDaylightVisibility()
        {
            float directSunFactor = Mathf.Clamp01(ResolveProfileSunIntensity() * ResolveHorizonFade());
            Color zenithColor = ResolveSurfaceSkyZenithColor();
            Color horizonColor = ResolveSurfaceFogColor();
            Color daylightColor = Color.Lerp(zenithColor, horizonColor, 0.25f);
            float skyFactor = ResolvePerceivedLuminance(daylightColor);
            return Mathf.Clamp01(Mathf.Max(directSunFactor, skyFactor * 0.82f));
        }

        private Color ResolveBaseUnderwaterFogColor()
        {
            float currentDepth = ResolveCurrentDepth();
            float daylightVisibility = ResolveSurfaceDaylightVisibility();
            Color waterMediumColor = Color.Lerp(_currentScatterBase, _currentScatterShallow, 0.62f);
            waterMediumColor.a = 1f;
            Color fogColor = Color.Lerp(_currentFogColor, waterMediumColor, UnderwaterMediumFogColorBlend);

            float biomeInfluence = Mathf.Lerp(
                UnderwaterBiomeFogInfluenceShallow,
                UnderwaterBiomeFogInfluenceDeep,
                math.saturate(currentDepth / UnderwaterBiomeFogInfluenceDepth));
            fogColor = Color.Lerp(waterMediumColor, fogColor, biomeInfluence);

            float daylightSeaTintBlend = daylightVisibility *
                (1f - math.saturate(currentDepth / UnderwaterDaylightSeaTintDepth));
            if (daylightSeaTintBlend > 0.0001f)
            {
                float shallowSeaBlend = 1f - math.saturate(currentDepth / UnderwaterSunlitTintDepth);
                Color daylightSeaTint = Color.Lerp(
                    UnderwaterDaylightSeaTintMid,
                    UnderwaterDaylightSeaTintShallow,
                    shallowSeaBlend);
                daylightSeaTint = MaxColorRgb(
                    daylightSeaTint,
                    ScaleColorRgb(_currentScatterShallow, 0.84f));
                fogColor = Color.Lerp(
                    fogColor,
                    daylightSeaTint,
                    daylightSeaTintBlend * UnderwaterDaylightSeaTintStrength);
            }

            float sunlitShallowBlend =
                daylightVisibility *
                (1f - math.saturate(currentDepth / UnderwaterSunlitTintDepth));
            if (sunlitShallowBlend > 0.0001f)
            {
                Color sunlitWaterColor = Color.Lerp(
                    _currentScatterShallow,
                    ResolveSurfaceSkyZenithColor(),
                    0.16f);
                sunlitWaterColor.a = 1f;
                fogColor = Color.Lerp(
                    fogColor,
                    sunlitWaterColor,
                    sunlitShallowBlend * UnderwaterSunlitTintStrength);
            }

            if (_soundscapeThermalTintBlend > 0.001f)
                fogColor = Color.Lerp(fogColor, thermalTierTintColor, _soundscapeThermalTintBlend);

            fogColor.a = 1f;
            return fogColor;
        }

        private bool ShouldRenderUnderwaterFogForCamera(Camera camera)
        {
            float cameraDepth = ResolveVisualDepthForCamera(camera);
            if (cameraDepth <= VisualExitUnderwaterDepth)
                return false;

            if (Application.isPlaying &&
                !_wasUnderwater &&
                cameraDepth < VisualForcedUnderwaterDepth)
            {
                return false;
            }

            return ResolveUnderwaterVisualStateForCameraDepth(cameraDepth, cameraDepth);
        }

        private Color ResolveSurfaceSkyZenithColor()
        {
            if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState state) &&
                state.IsValid)
            {
                Color stateZenith = state.SkyZenithColor;
                stateZenith.a = 1f;
                return stateZenith;
            }

            Color zenithColor = Shader.GetGlobalColor(_ID_SkyColorZenith);
            if (zenithColor.maxColorComponent <= 0.0001f)
                zenithColor = RenderSettings.ambientSkyColor;

            if (zenithColor.maxColorComponent <= 0.0001f)
                zenithColor = surfaceFogColor;

            zenithColor.a = 1f;
            return zenithColor;
        }

        private Color ResolveSurfaceHorizonVeilColor()
        {
            if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState state) &&
                state.IsValid)
            {
                Color veilColor = Color.Lerp(
                    state.FogColor,
                    state.HorizonHazeColor,
                    Mathf.Clamp01(0.35f + (state.HorizonHazeIntensity * 0.85f)));
                veilColor = Color.Lerp(
                    veilColor,
                    state.AmbientEquatorColor,
                    0.18f);
                veilColor.a = 1f;
                return veilColor;
            }

            return ResolveSurfaceFogColor();
        }

        private Color ResolveSurfaceOceanHorizonMergeColor()
        {
            if (HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState state) &&
                state.IsValid)
            {
                Color skyDrivenColor = Color.Lerp(
                    state.HorizonHazeColor,
                    state.SkyHorizonColor,
                    surfaceOceanHorizonSkyBias);
                Color ambientLiftColor = Color.Lerp(
                    state.AmbientEquatorColor,
                    state.AmbientSkyColor,
                    0.24f);
                Color fogAnchoredColor = Color.Lerp(
                    state.FogColor,
                    skyDrivenColor,
                    surfaceOceanHorizonSkyBias);
                Color mergeColor = Color.Lerp(fogAnchoredColor, ambientLiftColor, 0.16f);
                mergeColor = Color.Lerp(mergeColor, skyDrivenColor, surfaceOceanHorizonColorPreserve);

                float targetLuminance = Mathf.Max(
                    ResolvePerceivedLuminance(skyDrivenColor),
                    ResolvePerceivedLuminance(state.HorizonHazeColor),
                    ResolvePerceivedLuminance(ambientLiftColor) * 0.92f);
                mergeColor = LiftColorTowardsLuminance(
                    mergeColor,
                    targetLuminance,
                    0.18f + (surfaceOceanHorizonColorPreserve * 0.2f));
                mergeColor.a = 1f;
                return mergeColor;
            }

            return ResolveSurfaceHorizonVeilColor();
        }

        private Color ResolveCrestSkyTowardsSunColor()
        {
            Color sunScatterColor = skyMaterial != null && skyMaterial.HasProperty(_ID_SunScatterColor)
                ? skyMaterial.GetColor(_ID_SunScatterColor)
                : _baseSunScatterColor;

            if (sunScatterColor.maxColorComponent <= 0.0001f)
                sunScatterColor = ResolveSurfaceFogColor();

            sunScatterColor.a = 1f;
            return sunScatterColor;
        }

        private float ResolveSurfaceMaterialLightFactor()
        {
            float directSunFactor = Mathf.Clamp01(ResolveProfileSunIntensity() * ResolveHorizonFade());
            float skyFactor = ResolvePerceivedLuminance(ResolveSurfaceFogColor());
            return Mathf.Clamp01(Mathf.Max(directSunFactor, skyFactor));
        }

        private void ApplyCrestSkyBinding(Material targetMaterial)
        {
            if (targetMaterial == null)
                return;

            if (targetMaterial.HasProperty(_ID_ProceduralSky))
                targetMaterial.SetFloat(_ID_ProceduralSky, 1f);

            targetMaterial.EnableKeyword(ProceduralSkyKeyword);

            Color horizonVeilColor = ResolveSurfaceHorizonVeilColor();
            Color oceanHorizonMergeColor = ResolveSurfaceOceanHorizonMergeColor();
            Color skyBase = ResolveSafeSkyBindingColor(
                Color.Lerp(ResolveSurfaceFogColor(), oceanHorizonMergeColor, crestSkyBaseFogLink),
                targetMaterial,
                _ID_SkyBase,
                horizonVeilColor);
            Color skyAwayFromSun = ResolveSafeSkyBindingColor(
                ResolveSurfaceSkyZenithColor(),
                targetMaterial,
                _ID_SkyAwayFromSun,
                skyBase);
            Color skyTowardsSun = ResolveSafeSkyBindingColor(
                ResolveCrestSkyTowardsSunColor(),
                targetMaterial,
                _ID_SkyTowardsSun,
                Color.Lerp(skyBase, skyAwayFromSun, 0.35f));

            SetMaterialColorIfPresent(targetMaterial, _ID_SkyBase, skyBase);
            SetMaterialColorIfPresent(targetMaterial, _ID_SkyAwayFromSun, skyAwayFromSun);
            SetMaterialColorIfPresent(targetMaterial, _ID_SkyTowardsSun, skyTowardsSun);
            SetMaterialFloatIfPresent(targetMaterial, _ID_SkyDirectionality, CrestSkyDirectionality);
        }

        private static float ResolvePerceivedLuminance(Color color)
        {
            float luminance =
                (color.r * 0.2126f) +
                (color.g * 0.7152f) +
                (color.b * 0.0722f);
            return Mathf.Clamp01(luminance);
        }

        private static Color LiftColorTowardsLuminance(Color color, float targetLuminance, float blend)
        {
            float currentLuminance = ResolvePerceivedLuminance(color);
            float clampedBlend = Mathf.Clamp01(blend);

            if (currentLuminance <= 0.0001f)
            {
                Color fallbackLift = Color.Lerp(
                    color,
                    new Color(targetLuminance, targetLuminance, targetLuminance, 1f),
                    clampedBlend);
                fallbackLift.a = 1f;
                return fallbackLift;
            }

            float targetScale = Mathf.Max(0f, targetLuminance / currentLuminance);
            Color lifted = ScaleColorRgb(color, Mathf.Lerp(1f, targetScale, clampedBlend));
            lifted.a = 1f;
            return lifted;
        }

        private bool IsCrestUnderwaterRequiredForMaterial(Material targetMaterial)
        {
            if (targetMaterial == null)
                return false;

            Material oceanMaterial = CrestOceanRenderer.Instance != null
                ? CrestOceanRenderer.Instance.OceanMaterial
                : null;

            if (!ReferenceEquals(targetMaterial, oceanMaterial))
                return false;

            if (_mainCameraUnderwaterRenderer != null)
                return true;

            if (CrestUnderwaterRenderer.Instance != null)
                return true;

            return false;
        }

        private static Color ScaleColorRgb(Color color, float multiplier)
        {
            Color scaled = color;
            scaled.r *= multiplier;
            scaled.g *= multiplier;
            scaled.b *= multiplier;
            scaled.a = 1f;
            return scaled;
        }

        private static Color MaxColorRgb(Color left, Color right)
        {
            return new Color(
                Mathf.Max(left.r, right.r),
                Mathf.Max(left.g, right.g),
                Mathf.Max(left.b, right.b),
                1f);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SURFACE DEFAULTS
        //
        //  v5.1: ApplySurfaceDefaults writes sunLight.intensity
        //  as profile Ã— horizon. CelestialEngine will multiply
        //  by eclipse visibility afterward (runs later in tick order).
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ApplySurfaceDefaults()
        {
            bool hasSurfaceAtmosphereState = HectonCelestialEngine.TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState surfaceState);

            // â”€â”€ Sun intensity: base for CelestialEngine to multiply â”€â”€
            if (sunLight != null)
            {
                if (hasSurfaceAtmosphereState)
                {
                    sunLight.intensity = surfaceState.DirectionalLightIntensity;
                }
                else
                {
                    float baseSun = ResolveProfileSunIntensity();
                    float horizon = ResolveHorizonFade();
                    sunLight.intensity = baseSun * horizon * ResolveSurfaceSunMultiplier();
                }
            }

            if (_baseValuesCaptured && sunFlare != null)
            {
                sunFlare.intensity = _baseFlareIntensity;
                if (!sunFlare.enabled)
                    sunFlare.enabled = true;
            }

            HideSunVisualAboveWater();

            if (hasSurfaceAtmosphereState)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = surfaceState.FogColor;
                RenderSettings.fogDensity = surfaceState.FogDensity;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = surfaceState.AmbientSkyColor;
                RenderSettings.ambientEquatorColor = surfaceState.AmbientEquatorColor;
                RenderSettings.ambientGroundColor = surfaceState.AmbientGroundColor;
                RenderSettings.ambientIntensity = surfaceState.AmbientIntensity;
            }
            else if (enableSurfaceFog)
            {
                RenderSettings.fog        = true;
                RenderSettings.fogMode    = FogMode.ExponentialSquared;
                RenderSettings.fogColor   = ResolveSurfaceFogColor();
                RenderSettings.fogDensity = ResolveSurfaceFogDensity();
                RenderSettings.ambientMode  = AmbientMode.Flat;
                RenderSettings.ambientLight = ResolveSurfaceAmbientColor();
            }
            else
            {
                RenderSettings.fog = false;
                RenderSettings.ambientMode  = AmbientMode.Flat;
                RenderSettings.ambientLight = ResolveSurfaceAmbientColor();
            }

            ApplyRuntimeMainCameraClearFlags(CameraClearFlags.Skybox);

            if (biomePalette != null)
            {
                HectonBiomeProfile surfProfile = biomePalette.SurfaceProfile;
                if (surfProfile != null)
                {
                    _currentScatterBase     = surfProfile.scatterColorBase;
                    _currentScatterShallow  = surfProfile.scatterColorShallow;
                    _currentDepthFogDensity = surfProfile.depthFogDensity;
                    _cachedVisualDepth = 0f;
                    _cachedVisualIsUnderwater = false;
                    _cachedCausticsStrength = 0f;
                    ApplyCrestMaterial();
                }
            }
        }

        private void RestoreCameraDefaults()
        {
            ApplyRuntimeMainCameraClearFlags(CameraClearFlags.Skybox);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  BIOME INTERPOLATION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void RefreshTargetsFromCurrentProfile()
        {
            HectonBiomeProfile matrixOverride = ResolveActiveMatrixRuntimeVisualProfile();
            if (matrixOverride != null)
            {
                _targetScatterBase     = matrixOverride.scatterColorBase;
                _targetScatterShallow  = matrixOverride.scatterColorShallow;
                _targetDepthFogDensity = matrixOverride.depthFogDensity;
                _targetFogColor        = matrixOverride.fogColor;
                _targetTurbidity       = matrixOverride.turbidityMultiplier;
                _targetAmbientColor    = underwaterAmbientColor;
                return;
            }

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
            ApplyCrestMaterial(oceanUnderwaterMaterial, true);

            Material oceanMaterial = CrestOceanRenderer.Instance != null
                ? CrestOceanRenderer.Instance.OceanMaterial
                : null;

            if (oceanMaterial != null &&
                !ReferenceEquals(oceanMaterial, oceanUnderwaterMaterial))
            {
                ApplyCrestMaterial(oceanMaterial, false);
            }
        }

        private void ApplyCrestMaterial(Material targetMaterial, bool underwaterMaterial)
        {
            if (targetMaterial == null)
                return;

            bool crestUnderwaterRequired = underwaterMaterial || IsCrestUnderwaterRequiredForMaterial(targetMaterial);
            bool sharedOceanFeedsUnderwater = crestUnderwaterRequired && !underwaterMaterial;
            Material scatterSourceMaterial = sharedOceanFeedsUnderwater && oceanUnderwaterMaterial != null
                ? oceanUnderwaterMaterial
                : targetMaterial;
            float materialLightFactor = underwaterMaterial
                ? Mathf.Clamp01(_cachedLightFactor)
                : ResolveSurfaceMaterialLightFactor();
            float scatterLuminanceFloor;
            if (underwaterMaterial)
            {
                scatterLuminanceFloor = UnderwaterScatterLuminanceFloor;
            }
            else if (sharedOceanFeedsUnderwater)
            {
                scatterLuminanceFloor = SharedOceanUnderwaterScatterLuminanceFloor;
            }
            else
            {
                scatterLuminanceFloor = SurfaceScatterLuminanceFloor;
            }
            float scatterIntensity = Mathf.Lerp(scatterLuminanceFloor, 1f, materialLightFactor);
            Color horizonVeilColor = ResolveSurfaceHorizonVeilColor();
            Color oceanHorizonMergeColor = ResolveSurfaceOceanHorizonMergeColor();
            Color zenithSkyColor = ResolveSurfaceSkyZenithColor();
            Color sunSkyColor = ResolveCrestSkyTowardsSunColor();
            Color sourceScatterBase = ReadMaterialColorOrDefault(
                scatterSourceMaterial,
                _ID_Diffuse,
                new Color(0f, 0.03f, 0.07f, 1f));
            Color sourceScatterShallow = ReadMaterialColorOrDefault(
                scatterSourceMaterial,
                _ID_SubSurfaceShallowCol,
                new Color(0f, 0.15f, 0.12f, 1f));

            Color scatterBase = ResolveSafeCrestColor(
                _currentScatterBase,
                sourceScatterBase);
            Color scatterShallow = ResolveSafeCrestColor(
                _currentScatterShallow,
                sourceScatterShallow);

            if (sharedOceanFeedsUnderwater)
            {
                Color underwaterFogColor = ResolveBaseUnderwaterFogColor();
                Color sharedBaseSeed = Color.Lerp(sourceScatterBase, underwaterFogColor, 0.72f);
                Color sharedShallowSeed = Color.Lerp(
                    sourceScatterShallow,
                    Color.Lerp(underwaterFogColor, oceanHorizonMergeColor, 0.58f),
                    0.78f);
                sharedBaseSeed.a = 1f;
                sharedShallowSeed.a = 1f;
                scatterBase = MaxColorRgb(scatterBase, sharedBaseSeed);
                scatterShallow = MaxColorRgb(scatterShallow, sharedShallowSeed);
            }

            scatterBase = ScaleColorRgb(scatterBase, scatterIntensity);
            scatterShallow = ScaleColorRgb(scatterShallow, Mathf.Lerp(scatterLuminanceFloor * 1.15f, 1f, materialLightFactor));

            if (!underwaterMaterial)
            {
                Color surfaceBaseFloor;
                Color surfaceShallowFloor;

                if (sharedOceanFeedsUnderwater)
                {
                    Color underwaterFogColor = ResolveBaseUnderwaterFogColor();
                    surfaceBaseFloor = ScaleColorRgb(
                        Color.Lerp(underwaterFogColor, zenithSkyColor, 0.22f),
                        0.44f + (materialLightFactor * 0.16f));
                    surfaceShallowFloor = ScaleColorRgb(
                        Color.Lerp(underwaterFogColor, oceanHorizonMergeColor, 0.58f),
                        0.58f + (materialLightFactor * 0.18f));
                }
                else
                {
                    Color surfaceBaseHorizonTarget = Color.Lerp(
                        horizonVeilColor,
                        oceanHorizonMergeColor,
                        0.28f);
                    Color surfaceBaseSeed = Color.Lerp(
                        zenithSkyColor,
                        surfaceBaseHorizonTarget,
                        surfaceOceanBaseFogBlend);
                    surfaceBaseFloor = ScaleColorRgb(
                        surfaceBaseSeed,
                        0.12f + (materialLightFactor * 0.16f));

                    Color surfaceShallowSeed = Color.Lerp(
                        oceanHorizonMergeColor,
                        sunSkyColor,
                        surfaceOceanSunScatterBlend);
                    surfaceShallowSeed = Color.Lerp(
                        surfaceShallowSeed,
                        Color.Lerp(oceanHorizonMergeColor, horizonVeilColor, 0.18f),
                        surfaceOceanHorizonFogBlend);
                    surfaceShallowFloor = ScaleColorRgb(
                        surfaceShallowSeed,
                        (0.24f + (materialLightFactor * 0.24f)) *
                        (1f + surfaceOceanHorizonLuminanceLift));
                }

                scatterBase = MaxColorRgb(scatterBase, surfaceBaseFloor);
                scatterShallow = MaxColorRgb(scatterShallow, surfaceShallowFloor);
            }

            Color diffuseShadowFallback = sharedOceanFeedsUnderwater
                ? Color.Lerp(scatterBase, Color.black, 0.22f)
                : Color.Lerp(scatterBase, Color.black, 0.45f);
            Color shallowShadowFallback = sharedOceanFeedsUnderwater
                ? Color.Lerp(scatterShallow, scatterBase, 0.18f)
                : Color.Lerp(scatterShallow, scatterBase, 0.35f);
            Color diffuseShadow = ReadMaterialColorOrDefault(
                scatterSourceMaterial,
                _ID_DiffuseShadow,
                diffuseShadowFallback);
            Color shallowShadow = ReadMaterialColorOrDefault(
                scatterSourceMaterial,
                _ID_SubSurfaceShallowColShadow,
                shallowShadowFallback);
            if (sharedOceanFeedsUnderwater)
            {
                diffuseShadow = MaxColorRgb(diffuseShadow, diffuseShadowFallback);
                shallowShadow = MaxColorRgb(shallowShadow, shallowShadowFallback);
            }
            Vector3 depthFogDensity = ResolveSafeDepthFogDensity(targetMaterial);
            float subSurfaceBaseIntensity = ReadMaterialFloatOrDefault(
                scatterSourceMaterial,
                _ID_SubSurfaceBase,
                sharedOceanFeedsUnderwater ? 0f : 0.33f);
            float subSurfaceSunIntensity = ReadMaterialFloatOrDefault(
                scatterSourceMaterial,
                _ID_SubSurfaceSun,
                sharedOceanFeedsUnderwater ? 0.22f : 1.13f);
            float subSurfaceSunFalloff = ReadMaterialFloatOrDefault(
                scatterSourceMaterial,
                _ID_SubSurfaceSunFallOff,
                sharedOceanFeedsUnderwater ? 5.26f : 7.11f);

            if (!underwaterMaterial && !sharedOceanFeedsUnderwater)
            {
                float horizonSubsurfaceBias = Mathf.Clamp01(
                    (surfaceOceanHorizonFogBlend * 0.65f) +
                    (surfaceOceanSunScatterBlend * 0.35f));
                subSurfaceBaseIntensity = Mathf.Max(
                    subSurfaceBaseIntensity,
                    Mathf.Lerp(0.38f, 0.72f, horizonSubsurfaceBias));
                subSurfaceSunIntensity = Mathf.Max(
                    subSurfaceSunIntensity,
                    Mathf.Lerp(1.25f, 1.95f, horizonSubsurfaceBias));
                subSurfaceSunFalloff = Mathf.Min(
                    subSurfaceSunFalloff,
                    Mathf.Lerp(6.2f, 4.8f, horizonSubsurfaceBias));
            }

            if (sharedOceanFeedsUnderwater)
            {
                Vector3 underwaterDensityFloor = ResolveFallbackDepthFogDensity(oceanUnderwaterMaterial);
                float densityFloorScale = _cachedVisualIsUnderwater ? 1f : 0.68f;
                depthFogDensity = new Vector3(
                    Mathf.Max(depthFogDensity.x, underwaterDensityFloor.x * densityFloorScale),
                    Mathf.Max(depthFogDensity.y, underwaterDensityFloor.y * densityFloorScale),
                    Mathf.Max(depthFogDensity.z, underwaterDensityFloor.z * densityFloorScale));
            }
            ApplyCrestSkyBinding(targetMaterial);

            SetMaterialColorIfPresent(targetMaterial, _ID_ScatterColourBase, scatterBase);
            SetMaterialColorIfPresent(targetMaterial, _ID_Diffuse, scatterBase);
            SetMaterialColorIfPresent(targetMaterial, _ID_DiffuseGrazing, scatterShallow);
            SetMaterialColorIfPresent(targetMaterial, _ID_DiffuseShadow, diffuseShadow);
            SetMaterialColorIfPresent(targetMaterial, _ID_SubSurfaceColour, scatterShallow);
            SetMaterialColorIfPresent(targetMaterial, _ID_ScatterColourShallow, scatterShallow);
            SetMaterialColorIfPresent(targetMaterial, _ID_SubSurfaceShallowCol, scatterShallow);
            SetMaterialColorIfPresent(targetMaterial, _ID_SubSurfaceShallowColShadow, shallowShadow);
            SetMaterialVectorIfPresent(
                targetMaterial,
                _ID_DepthFogDensity,
                new Vector4(
                    depthFogDensity.x,
                    depthFogDensity.y,
                    depthFogDensity.z,
                    0f));
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_Caustics,
                _cachedCausticsStrength > 0.001f ? 1f : 0f);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_CausticsStrength,
                _cachedCausticsStrength);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_SubSurfaceScattering,
                1f);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_SubSurfaceBase,
                subSurfaceBaseIntensity);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_SubSurfaceSun,
                subSurfaceSunIntensity);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_SubSurfaceSunFallOff,
                subSurfaceSunFalloff);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_SubSurfaceShallowColour,
                1f);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_Underwater,
                crestUnderwaterRequired ? 1f : 0f);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_CullMode,
                crestUnderwaterRequired
                    ? (float)UnityEngine.Rendering.CullMode.Off
                    : (float)UnityEngine.Rendering.CullMode.Back);

            if (crestUnderwaterRequired)
                targetMaterial.EnableKeyword(UnderwaterKeyword);
            else
                targetMaterial.DisableKeyword(UnderwaterKeyword);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  BIOME EVENT
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void HandleBiomeChanged(int biomeIndex)
        {
            _targetBiomeIndex = biomeIndex;
            if (_matrixRuntimeVisualProfile != null)
            {
#if UNITY_EDITOR
                _debugTargetBiome = _targetBiomeIndex;
#endif
                return;
            }

            ApplyBiomePaletteTarget(biomeIndex);
        }

        private void HandleMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            HectonBiomeProfile nextOverride = profile != null ? profile.runtimeVisualProfile : null;
            ApplyEcologyContext(profile);
            if (_matrixRuntimeVisualProfile == nextOverride)
                return;

            _matrixRuntimeVisualProfile = nextOverride;
            if (_matrixRuntimeVisualProfile != null)
            {
                SetTargetFromProfile(_matrixRuntimeVisualProfile);
                return;
            }

            ApplyBiomePaletteTarget(_targetBiomeIndex);
        }

        private void HandleSoundscapeTierChanged(SoundscapeTier oldTier, SoundscapeTier newTier)
        {
            ApplySoundscapeTierResponse(newTier);
        }

        private void RefreshSoundscapeTierResponse(bool force)
        {
            SoundscapeTier tier = SoundscapeSystem.Instance != null
                ? SoundscapeSystem.Instance.CurrentTier
                : SoundscapeTier.Shallow;

            ApplySoundscapeTierResponse(tier);
        }

        private void ApplySoundscapeTierResponse(SoundscapeTier tier)
        {
            _currentSoundscapeTier = tier;
            _soundscapeFogDensityScale = 1f;
            _soundscapeAmbientScale = 1f;
            _soundscapeBeamScale = 1f;
            _soundscapeCausticsScale = 1f;
            _soundscapeThermalTintBlend = 0f;

            if (!enableSoundscapeTierResponse)
            {
                UpdateSoundscapeDiagnostics();
                return;
            }

            switch (tier)
            {
                case SoundscapeTier.Twilight:
                    _soundscapeFogDensityScale = twilightTierFogScale;
                    _soundscapeAmbientScale = twilightTierAmbientScale;
                    _soundscapeBeamScale = twilightTierBeamScale;
                    _soundscapeCausticsScale = twilightTierCausticsScale;
                    break;

                case SoundscapeTier.Darkness:
                    _soundscapeFogDensityScale = darknessTierFogScale;
                    _soundscapeAmbientScale = darknessTierAmbientScale;
                    _soundscapeBeamScale = darknessTierBeamScale;
                    _soundscapeCausticsScale = darknessTierCausticsScale;
                    break;

                case SoundscapeTier.Abyss:
                    _soundscapeFogDensityScale = abyssTierFogScale;
                    _soundscapeAmbientScale = abyssTierAmbientScale;
                    _soundscapeBeamScale = abyssTierBeamScale;
                    _soundscapeCausticsScale = abyssTierCausticsScale;
                    break;

                case SoundscapeTier.DeepAbyss:
                    _soundscapeFogDensityScale = deepAbyssTierFogScale;
                    _soundscapeAmbientScale = deepAbyssTierAmbientScale;
                    _soundscapeBeamScale = deepAbyssTierBeamScale;
                    _soundscapeCausticsScale = deepAbyssTierCausticsScale;
                    break;

                case SoundscapeTier.Thermal:
                    _soundscapeFogDensityScale = thermalTierFogScale;
                    _soundscapeAmbientScale = thermalTierAmbientScale;
                    _soundscapeBeamScale = thermalTierBeamScale;
                    _soundscapeCausticsScale = thermalTierCausticsScale;
                    _soundscapeThermalTintBlend = thermalTierTintBlend;
                    break;

                case SoundscapeTier.Surface:
                case SoundscapeTier.Shallow:
                default:
                    break;
            }

            UpdateSoundscapeDiagnostics();
        }

        private void ApplyBiomePaletteTarget(int biomeIndex)
        {
            if (biomePalette == null) return;

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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public float CurrentDepth
        {
            get
            {
                return ResolveCurrentDepth();
            }
        }

        public float CurrentLightFactor
        {
            get
            {
                float d = CurrentDepth;
                if (d <= 0f) return 1f;
                return ResolveDepthLightFactor(d);
            }
        }

        internal void SetSurfaceWeatherOverride(
            Color fogColor,
            float fogDensity,
            Color ambientColor,
            float sunMultiplier)
        {
            _surfaceWeatherOverrideActive = true;
            _surfaceWeatherFogColor = fogColor;
            _surfaceWeatherFogDensity = Mathf.Max(0f, fogDensity);
            _surfaceWeatherAmbientColor = ambientColor;
            _surfaceWeatherSunMultiplier = Mathf.Max(0f, sunMultiplier);
        }

        internal void ClearSurfaceWeatherOverride()
        {
            _surfaceWeatherOverrideActive = false;
            _surfaceWeatherSunMultiplier = 1f;
        }

        public bool IsUnderwater
        {
            get
            {
                if (Application.isPlaying)
                    return _wasUnderwater;

                return ResolveCurrentDepth() > 0f;
            }
        }

        public float CurrentTurbidity => _currentTurbidity;
        public int TargetBiomeIndex => _targetBiomeIndex;
        public float TransitionProgress => _transitionProgress;
        internal float DebugAdaptiveMotesScale => _debugAdaptiveMotesScale;
        internal float DebugAdaptiveBubbleScale => _debugAdaptiveBubbleScale;
        internal float DebugAdaptiveBeamScale => _debugAdaptiveBeamScale;
        internal float DebugSuspendedMotesEmission => _debugSuspendedMotesEmission;
        internal int DebugExhaleBubbleBurstCount => _debugExhaleBubbleBurstCount;

        public void SetTargetBiome(int biomeIndex) => HandleBiomeChanged(biomeIndex);
        public void SetPlayerCamera(Transform camera) => playerCamera = camera;
        public void SetWaterLevelFallback(float y) => waterLevelFallback = y;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” INIT
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ResolvePlayerCamera()
        {
            if (Application.isPlaying)
            {
                if (_playerMovement == null && playerCamera != null)
                    TryCachePlayerMovementFromTransformHierarchy(playerCamera, "PlayerCameraHierarchy");

                if (_playerMovement == null && mainCamera != null)
                    TryCachePlayerMovementFromTransformHierarchy(mainCamera.transform, "MainCameraHierarchy");

                if (_playerMovement == null &&
                    SceneBootstrap.TryGetCurrentPlayerTransform(out Transform cachedPlayerTransform))
                {
                    TryCachePlayerMovementFromTransformHierarchy(cachedPlayerTransform, "SceneBootstrap");
                    CachePlayerMovement(cachedPlayerTransform);
                }

                if (playerCamera != null) return;

                if (Time.unscaledTime < _nextRuntimePlayerCameraResolveTime)
                    return;

                _nextRuntimePlayerCameraResolveTime = Time.unscaledTime + RuntimeCameraResolveRetryInterval;
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                {
                    CachePlayerMovement(playerTransform);

                    Camera playerOwnedCamera = ResolveRuntimeMainCamera(playerTransform);
                    if (playerOwnedCamera != null)
                    {
                        playerCamera = playerOwnedCamera.transform;
                        mainCamera = playerOwnedCamera;
                        return;
                    }

                    playerCamera = playerTransform;
                    return;
                }

            }
#if UNITY_EDITOR
            else
            {
                if (playerCamera != null) return;
                ResolveEditorCamera();
            }
#endif
        }

        private void ResolveMainCamera()
        {
            if (mainCamera != null &&
                (!Application.isPlaying || IsRuntimeMainCamera(mainCamera)))
            {
                return;
            }

            mainCamera = null;
            if (Application.isPlaying)
            {
                if (playerCamera != null)
                {
                    Camera playerOwnedCamera = playerCamera.GetComponent<Camera>();
                    if (IsRuntimeMainCamera(playerOwnedCamera))
                    {
                        mainCamera = playerOwnedCamera;
                        return;
                    }
                }

                if (Time.unscaledTime < _nextRuntimeMainCameraResolveTime)
                    return;

                _nextRuntimeMainCameraResolveTime = Time.unscaledTime + RuntimeCameraResolveRetryInterval;
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                {
                    Camera playerOwnedCamera = ResolveRuntimeMainCamera(playerTransform);
                    if (playerOwnedCamera != null)
                    {
                        mainCamera = playerOwnedCamera;
                        playerCamera = playerOwnedCamera.transform;
                        return;
                    }
                }

                mainCamera = ResolveRuntimeMainCamera();
                if (mainCamera != null)
                {
                    playerCamera = mainCamera.transform;
                    return;
                }

                if (TryGetComponent(out Camera localCamera) && IsRuntimeMainCamera(localCamera))
                {
                    mainCamera = localCamera;
                    return;
                }

                Transform root = transform.root;
                if (root != null)
                {
                    Transform rootMainCameraTransform = root.Find("Main Camera");
                    if (rootMainCameraTransform != null)
                    {
                        Camera rootMainCamera = rootMainCameraTransform.GetComponent<Camera>();
                        if (IsRuntimeMainCamera(rootMainCamera))
                        {
                            mainCamera = rootMainCamera;
                            return;
                        }
                    }
                }

                Camera parentCamera = GetComponentInParent<Camera>();
                if (IsRuntimeMainCamera(parentCamera))
                    mainCamera = parentCamera;

            }
#if UNITY_EDITOR
            else
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv != null) mainCamera = sv.camera;
            }
#endif

            if (Application.isPlaying)
                EnsureCrestUnderwaterPassOwnership();
        }

        private void ResolveGameplayMainCameraForEditor()
        {
            if (Application.isPlaying)
                return;

            if (_gameplayMainCamera != null)
                return;

            if (playerCamera != null)
            {
                Camera playerOwnedCamera = playerCamera.GetComponent<Camera>();
                if (playerOwnedCamera != null && playerOwnedCamera.GetComponent<CrestUnderwaterRenderer>() != null)
                {
                    _gameplayMainCamera = playerOwnedCamera;
                    return;
                }
            }

            if (mainCamera != null && mainCamera.GetComponent<CrestUnderwaterRenderer>() != null)
            {
                _gameplayMainCamera = mainCamera;
                return;
            }

            Transform root = transform.root;
            if (root == null)
                return;

            Transform mainCameraTransform = root.Find("Main Camera");
            if (mainCameraTransform == null && root.parent != null)
                mainCameraTransform = root.parent.Find("Main Camera");

            if (mainCameraTransform != null)
                _gameplayMainCamera = mainCameraTransform.GetComponent<Camera>();
        }

        private void ResolveGameplaySpaceCameraForEditor()
        {
            if (Application.isPlaying)
                return;

            if (ResolveValidCameraReference(ref _editorGameplaySpaceCamera) != null)
                return;

            ResolveGameplayMainCameraForEditor();
            if (_gameplayMainCamera == null)
                return;

            Transform spaceCameraTransform = _gameplayMainCamera.transform.Find("SpaceCamera");
            if (spaceCameraTransform == null)
                return;

            _editorGameplaySpaceCamera = spaceCameraTransform.GetComponent<Camera>();
            ResolveValidCameraReference(ref _editorGameplaySpaceCamera);
        }

        private void ResolveSpaceCamera()
        {
            if (IsCameraReferenceValid(_spaceCamera))
                return;

            _spaceCamera = null;

            Transform spaceCameraTransform = null;
            if (playerCamera != null)
                spaceCameraTransform = playerCamera.Find("SpaceCamera");

            if (spaceCameraTransform == null)
            {
                if (mainCamera == null)
                    return;

                spaceCameraTransform = mainCamera.transform.Find("SpaceCamera");
                if (spaceCameraTransform == null && mainCamera.transform.parent != null)
                    spaceCameraTransform = mainCamera.transform.parent.Find("SpaceCamera");
            }

            if (spaceCameraTransform == null)
                return;

            Camera resolvedSpaceCamera = spaceCameraTransform.GetComponent<Camera>();
            if (!IsCameraReferenceValid(resolvedSpaceCamera))
            {
                _spaceCamera = null;
                return;
            }

            _spaceCamera = resolvedSpaceCamera;
            if (_spaceCameraMaskCaptured)
                return;

            _spaceCameraOriginalCullingMask = resolvedSpaceCamera.cullingMask;
            _spaceCameraMaskCaptured = true;

            if (Application.isPlaying)
                EnsureCrestUnderwaterPassOwnership();
        }

        private void EnsureCrestUnderwaterPassOwnership()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EnsureEditorCrestUnderwaterPassOwnership();
                return;
            }
#endif

            if (mainCamera == null)
                return;

            if (!mainCamera.TryGetComponent(out _mainCameraUnderwaterRenderer) ||
                _mainCameraUnderwaterRenderer == null)
            {
                // COLD ALLOC: UnderwaterRenderer[1] â€” restore Crest underwater pass on the gameplay camera when authoring data lost it â€” owner: HectonUnderwaterVisuals
                _mainCameraUnderwaterRenderer =
                    mainCamera.gameObject.AddComponent<CrestUnderwaterRenderer>();
            }

            if (!_mainCameraUnderwaterRenderer.enabled)
                _mainCameraUnderwaterRenderer.enabled = true;

            _mainCameraUnderwaterRenderer._copyOceanMaterialParamsEachFrame = true;

            ResolveSpaceCamera();
            PurgeSecondaryUnderwaterRenderers();
            EnsureCrestOceanCameraOwnership();
        }

#if UNITY_EDITOR
        private void EnsureEditorCrestUnderwaterPassOwnership()
        {
            EnsureEditorGameplayCameraUnderwaterRenderer();

            SceneView sceneView = SceneView.lastActiveSceneView;
            Camera sceneViewCamera = sceneView != null ? sceneView.camera : null;
            if (sceneViewCamera == null)
                return;

            if (!sceneViewCamera.TryGetComponent(out _editorSceneViewUnderwaterRenderer) ||
                _editorSceneViewUnderwaterRenderer == null)
            {
                // COLD ALLOC: UnderwaterRenderer[1] â€” restore Crest underwater pass on SceneView camera for editor preview ownership â€” owner: HectonUnderwaterVisuals
                _editorSceneViewUnderwaterRenderer =
                    sceneViewCamera.gameObject.AddComponent<CrestUnderwaterRenderer>();
            }

            CrestUnderwaterRenderer template = ResolveEditorUnderwaterRendererTemplate();
            CopyUnderwaterRendererSettings(template, _editorSceneViewUnderwaterRenderer);
            _editorSceneViewUnderwaterRenderer._copyOceanMaterialParamsEachFrame = true;
            if (!_editorSceneViewUnderwaterRenderer.enabled)
                _editorSceneViewUnderwaterRenderer.enabled = true;

            if (ReferenceEquals(mainCamera, sceneViewCamera))
                _mainCameraUnderwaterRenderer = _editorSceneViewUnderwaterRenderer;
        }

        private void EnsureEditorGameplayCameraUnderwaterRenderer()
        {
            ResolveGameplayMainCameraForEditor();
            if (_gameplayMainCamera == null)
                return;

            if (!_gameplayMainCamera.TryGetComponent(out _editorCrestUnderwaterRenderer) ||
                _editorCrestUnderwaterRenderer == null)
            {
                // COLD ALLOC: UnderwaterRenderer[1] â€” restore Crest underwater pass on gameplay main camera for editor GameView ownership â€” owner: HectonUnderwaterVisuals
                _editorCrestUnderwaterRenderer =
                    _gameplayMainCamera.gameObject.AddComponent<CrestUnderwaterRenderer>();
            }

            _editorCrestUnderwaterRenderer._copyOceanMaterialParamsEachFrame = true;
        }

        private CrestUnderwaterRenderer ResolveEditorUnderwaterRendererTemplate()
        {
            if (_editorCrestUnderwaterRenderer != null)
                return _editorCrestUnderwaterRenderer;

            if (_gameplayMainCamera != null &&
                _gameplayMainCamera.TryGetComponent(out CrestUnderwaterRenderer gameplayRenderer) &&
                gameplayRenderer != null)
            {
                _editorCrestUnderwaterRenderer = gameplayRenderer;
                return gameplayRenderer;
            }

            return _mainCameraUnderwaterRenderer;
        }
#endif

        private void EnsureCrestOceanCameraOwnership()
        {
            if (!Application.isPlaying)
                return;

            if (mainCamera == null || !IsRuntimeMainCamera(mainCamera))
                ResolveMainCamera();

            if (mainCamera == null)
                return;

            _oceanRenderer = CrestOceanRenderer.Instance;
            if (_oceanRenderer == null)
                return;

            Transform mainCameraTransform = mainCamera.transform;
            _oceanRenderer.ViewCamera = mainCamera;
            _oceanRenderer.Viewpoint = mainCameraTransform;
        }

        private void EnsureRuntimeVisualOwners()
        {
            if (!Application.isPlaying)
                return;

            if (playerCamera == null || _playerMovement == null)
                ResolvePlayerCamera();

            if (mainCamera == null || !IsRuntimeMainCamera(mainCamera))
                ResolveMainCamera();

            if (sunVisualTransform == null)
                ResolveSunVisualTransform();

            if (underwaterSuspendedMotes == null)
                ResolveUnderwaterParticles();

            if (underwaterExhaleBubbles == null)
                ResolveUnderwaterExhaleBubbles();

            if (shallowSunBeam == null || shallowSunBeamLight == null || _shallowSunBeamTransform == null)
                ResolveShallowSunBeam();

            if (!IsCameraReferenceValid(_spaceCamera))
                ResolveSpaceCamera();

            if (depthZoneDirector == null)
                depthZoneDirector = DepthZoneDirector.Instance;

            EnsureCrestOceanCameraOwnership();

            if (_mainCameraUnderwaterRenderer == null)
                EnsureCrestUnderwaterPassOwnership();
        }

        private static bool IsRuntimeMainCamera(Camera camera)
        {
            return camera != null &&
                   camera.cameraType != CameraType.SceneView &&
                   camera.CompareTag("MainCamera");
        }

        private static Camera ResolveRuntimeMainCamera()
        {
            int cameraCount = Camera.GetAllCameras(_runtimeCameraBuffer);
            for (int i = 0; i < cameraCount; i++)
            {
                Camera candidate = _runtimeCameraBuffer[i];
                if (IsRuntimeMainCamera(candidate) &&
                    candidate.enabled &&
                    candidate.gameObject.activeInHierarchy)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Camera ResolveRuntimeMainCamera(Transform playerTransform)
        {
            if (playerTransform == null)
                return null;

            int cameraCount = Camera.GetAllCameras(_runtimeCameraBuffer);
            for (int i = 0; i < cameraCount; i++)
            {
                Camera candidate = _runtimeCameraBuffer[i];
                if (!IsRuntimeMainCamera(candidate) ||
                    !candidate.enabled ||
                    !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Transform candidateTransform = candidate.transform;
                if (ReferenceEquals(candidateTransform, playerTransform) ||
                    candidateTransform.IsChildOf(playerTransform))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void PurgeSecondaryUnderwaterRenderers()
        {
            int cameraCount = Camera.GetAllCameras(_runtimeCameraBuffer);
            for (int i = 0; i < cameraCount; i++)
            {
                Camera candidate = _runtimeCameraBuffer[i];
                if (candidate == null)
                    continue;

                if (!candidate.TryGetComponent(out CrestUnderwaterRenderer candidateRenderer) ||
                    candidateRenderer == null)
                {
                    continue;
                }

                if (ReferenceEquals(candidate, mainCamera))
                {
                    _mainCameraUnderwaterRenderer = candidateRenderer;
                    continue;
                }

                if (ReferenceEquals(candidate, _spaceCamera))
                    _spaceCameraUnderwaterRenderer = candidateRenderer;

                Destroy(candidateRenderer);
            }
        }

        private static void CopyUnderwaterRendererSettings(
            CrestUnderwaterRenderer source,
            CrestUnderwaterRenderer target)
        {
            if (source == null || target == null || ReferenceEquals(source, target))
                return;

            target._mode = source._mode;
            target._depthFogDensityFactor = source._depthFogDensityFactor;
            target._volumeGeometry = source._volumeGeometry;
            target._invertCulling = source._invertCulling;
            target._enableShaderAPI = source._enableShaderAPI;
            target._copyOceanMaterialParamsEachFrame = source._copyOceanMaterialParamsEachFrame;
            target._farPlaneMultiplier = source._farPlaneMultiplier;
        }

        private static void SetMaterialColorIfPresent(Material targetMaterial, int propertyId, Color value)
        {
            if (targetMaterial != null && targetMaterial.HasProperty(propertyId))
                targetMaterial.SetColor(propertyId, value);
        }

        private static void SetMaterialVectorIfPresent(Material targetMaterial, int propertyId, Vector4 value)
        {
            if (targetMaterial != null && targetMaterial.HasProperty(propertyId))
                targetMaterial.SetVector(propertyId, value);
        }

        private static void SetMaterialFloatIfPresent(Material targetMaterial, int propertyId, float value)
        {
            if (targetMaterial != null && targetMaterial.HasProperty(propertyId))
                targetMaterial.SetFloat(propertyId, value);
        }

        private static float ReadMaterialFloatOrDefault(Material material, int propertyId, float fallback)
        {
            return material != null && material.HasProperty(propertyId)
                ? material.GetFloat(propertyId)
                : fallback;
        }

        private static Color ReadMaterialColorOrDefault(Material material, int propertyId, Color fallback)
        {
            return material != null && material.HasProperty(propertyId)
                ? material.GetColor(propertyId)
                : fallback;
        }

        private Color ResolveSafeSkyBindingColor(
            Color preferred,
            Material targetMaterial,
            int propertyId,
            Color fallback)
        {
            Color resolved = preferred;
            if (IsNearlyBlack(resolved))
                resolved = ReadMaterialColorOrDefault(targetMaterial, propertyId, fallback);

            if (IsNearlyBlack(resolved))
                resolved = fallback;

            resolved.a = 1f;
            return resolved;
        }

        private static Vector3 ReadMaterialVector3OrDefault(Material material, int propertyId, Vector3 fallback)
        {
            if (material == null || !material.HasProperty(propertyId))
                return fallback;

            Vector4 value = material.GetVector(propertyId);
            return new Vector3(value.x, value.y, value.z);
        }

        private static bool IsNearlyBlack(Color color)
        {
            return color.r <= 0.0001f &&
                   color.g <= 0.0001f &&
                   color.b <= 0.0001f;
        }

        private Color ResolveSafeCrestColor(Color preferred, Color fallback)
        {
            Color resolved = IsNearlyBlack(preferred) ? fallback : preferred;
            if (IsNearlyBlack(resolved))
                resolved = fallback;

            resolved.a = 1f;
            return resolved;
        }

        private Vector3 ResolveSafeDepthFogDensity(Material targetMaterial)
        {
            Vector3 fallback = ResolveFallbackDepthFogDensity(targetMaterial);
            Vector3 source = _currentDepthFogDensity;

            return new Vector3(
                Mathf.Clamp(source.x > 0f ? source.x : fallback.x, minFogDensity, maxFogDensity),
                Mathf.Clamp(source.y > 0f ? source.y : fallback.y, minFogDensity, maxFogDensity),
                Mathf.Clamp(source.z > 0f ? source.z : fallback.z, minFogDensity, maxFogDensity));
        }

        private Vector3 ResolveFallbackDepthFogDensity(Material preferredMaterial)
        {
            Vector3 fallback = new Vector3(0.0125f, 0.009f, 0.01f);
            fallback = ReadMaterialVector3OrDefault(preferredMaterial, _ID_DepthFogDensity, fallback);
            fallback = ReadMaterialVector3OrDefault(oceanUnderwaterMaterial, _ID_DepthFogDensity, fallback);

            Material oceanMaterial = CrestOceanRenderer.Instance != null
                ? CrestOceanRenderer.Instance.OceanMaterial
                : null;
            fallback = ReadMaterialVector3OrDefault(oceanMaterial, _ID_DepthFogDensity, fallback);

            return new Vector3(
                Mathf.Clamp(fallback.x, minFogDensity, maxFogDensity),
                Mathf.Clamp(fallback.y, minFogDensity, maxFogDensity),
                Mathf.Clamp(fallback.z, minFogDensity, maxFogDensity));
        }

        private Color ResolveFallbackFogColor()
        {
            Color fallback = new Color(0.0567818f, 0.28103185f, 0.41509432f, 1f);
            fallback = ReadMaterialColorOrDefault(oceanUnderwaterMaterial, _ID_ScatterColourShallow, fallback);
            return ResolveSafeCrestColor(fallback, new Color(0f, 0.15f, 0.12f, 1f));
        }

        private void TryHandleThermoclineTransition(bool isUnderwater)
        {
            if (!Application.isPlaying)
                return;

            if (depthZoneDirector == null)
                depthZoneDirector = DepthZoneDirector.Instance;

            DepthZoneProfile currentZone = depthZoneDirector != null ? depthZoneDirector.CurrentZone : null;
            if (!isUnderwater || !_wasUnderwater)
            {
                _lastDepthZoneProfile = currentZone;
                return;
            }

            if (currentZone == _lastDepthZoneProfile)
                return;

            DepthZoneProfile previousZone = _lastDepthZoneProfile;
            _lastDepthZoneProfile = currentZone;

            if (previousZone == null || currentZone == null)
                return;

            if (Time.unscaledTime < _nextThermoclineAllowedTime)
                return;

            float intensity = ResolveThermoclineTransitionIntensity(previousZone, currentZone);
            if (intensity < thermoclineMinTriggerIntensity)
                return;

            ResolveTransitionCameraVfx();
            if (transitionCameraVfx != null)
                transitionCameraVfx.TriggerThermoclineImpulse(intensity);

            ResolveTransitionVisorController();
            if (transitionVisorController != null)
            {
                transitionVisorController.TriggerEnvironmentalDistortion(
                    intensity,
                    thermoclineVisorDistortionHoldDuration,
                    thermoclineVisorDistortionRecoverySpeed);
            }

            if (thermoclineTransitionClip != null && SpatialAudioManager.Instance != null)
                SpatialAudioManager.Instance.PlayStatic2D(thermoclineTransitionClip, thermoclineAudioVolume * intensity);

            _nextThermoclineAllowedTime = Time.unscaledTime + thermoclineMinRepeatInterval;
        }

        private float ResolveThermoclineTransitionIntensity(DepthZoneProfile previousZone, DepthZoneProfile currentZone)
        {
            DepthZoneAmbience previousAmbience = previousZone.ambience;
            DepthZoneAmbience currentAmbience = currentZone.ambience;

            float temperatureDelta = Mathf.Abs(previousAmbience.waterTemperature - currentAmbience.waterTemperature);
            float fogDelta = Mathf.Abs(previousAmbience.fogDensity - currentAmbience.fogDensity);
            Vector3 previousColor = new Vector3(previousAmbience.waterColor.r, previousAmbience.waterColor.g, previousAmbience.waterColor.b);
            Vector3 currentColor = new Vector3(currentAmbience.waterColor.r, currentAmbience.waterColor.g, currentAmbience.waterColor.b);
            float colorDelta = Vector3.Distance(previousColor, currentColor);

            float normalizedTemperature = temperatureDelta / Mathf.Max(0.01f, thermoclineTemperatureDeltaForFullEffect);
            float normalizedFog = fogDelta / Mathf.Max(0.01f, thermoclineFogDeltaForFullEffect);
            float normalizedColor = colorDelta / Mathf.Max(0.01f, thermoclineColorDeltaForFullEffect);
            float structuralBonus = previousZone.isThermal != currentZone.isThermal ? 0.24f : 0f;
            structuralBonus += Mathf.Abs(previousZone.dangerLevel - currentZone.dangerLevel) * 0.12f;

            return Mathf.Clamp01(Mathf.Max(normalizedTemperature, Mathf.Max(normalizedFog, normalizedColor)) + structuralBonus);
        }

        private void TriggerSubmergeImpulse()
        {
            ResolveTransitionCameraVfx();
            if (transitionCameraVfx != null)
                transitionCameraVfx.TriggerSubmergeImpulse();

            ResolveTransitionVisorController();
            if (transitionVisorController != null)
                transitionVisorController.TriggerSubmergeRunoff();

            _submergeImpulseTimer = submergeImpulseDuration;
        }

        private void TriggerSurfaceBreakImpulse()
        {
            ResolveTransitionCameraVfx();
            if (transitionCameraVfx != null)
                transitionCameraVfx.TriggerSurfaceBreakImpulse();

            ResolveTransitionVisorController();
            if (transitionVisorController != null)
                transitionVisorController.TriggerSurfaceBreakRunoff();
        }

        private void ResolveTransitionCameraVfx()
        {
            if (transitionCameraVfx != null)
                return;

            if (mainCamera == null)
                ResolveMainCamera();

            if (mainCamera != null)
                mainCamera.TryGetComponent(out transitionCameraVfx);
        }

        private void ResolveTransitionVisorController()
        {
            if (transitionVisorController != null)
                return;

            if (mainCamera == null)
                ResolveMainCamera();

            if (mainCamera == null)
                return;

            Transform playerRoot = mainCamera.transform.parent;
            if (playerRoot == null)
                return;

            Transform visorTransform = playerRoot.Find("Suit_Visor");
            if (visorTransform != null)
                visorTransform.TryGetComponent(out transitionVisorController);
        }

        private void ResolveUnderwaterParticles()
        {
            if (underwaterSuspendedMotes != null)
                return;

            if (mainCamera == null)
                ResolveMainCamera();

            if (mainCamera == null)
                return;

            Transform motesTransform = mainCamera.transform.Find(UnderwaterSuspendedMotesChildName);
            if (motesTransform != null)
                motesTransform.TryGetComponent(out underwaterSuspendedMotes);
        }

        private void ResolveUnderwaterExhaleBubbles()
        {
            if (underwaterExhaleBubbles != null)
                return;

            if (mainCamera == null)
                ResolveMainCamera();

            if (mainCamera == null)
                return;

            Transform exhaleTransform = mainCamera.transform.Find(UnderwaterExhaleBubblesChildName);
            if (exhaleTransform != null)
                exhaleTransform.TryGetComponent(out underwaterExhaleBubbles);
        }

        private void ResolveShallowSunBeam()
        {
            if (shallowSunBeam != null &&
                shallowSunBeamLight != null &&
                _shallowSunBeamTransform != null)
            {
                return;
            }

            if (mainCamera == null)
                ResolveMainCamera();

            if (mainCamera == null)
                return;

            Transform beamTransform = mainCamera.transform.Find(UnderwaterShallowSunBeamChildName);
            if (beamTransform == null)
                return;

            _shallowSunBeamTransform = beamTransform;
            _shallowSunBeamBaseLocalPosition = beamTransform.localPosition;
            if (shallowSunBeam == null)
                beamTransform.TryGetComponent(out shallowSunBeam);
            if (shallowSunBeamLight == null)
                beamTransform.TryGetComponent(out shallowSunBeamLight);
        }

        private void ResolveSunVisualTransform()
        {
            if (sunVisualTransform != null)
                return;

            if (sunLight == null)
                sunLight = RenderSettings.sun;

            if (sunLight == null)
                return;

            Transform resolvedSunVisual = sunLight.transform.Find("Sun_Body");
            if (resolvedSunVisual != null)
                sunVisualTransform = resolvedSunVisual;
        }

        private void UpdateSubmergeImpulse(float deltaTime)
        {
            if (_submergeImpulseTimer <= 0f)
                return;

            _submergeImpulseTimer -= deltaTime;
            if (_submergeImpulseTimer < 0f)
                _submergeImpulseTimer = 0f;
        }

        private float EvaluateSubmergeImpulse(float depth)
        {
            if (_submergeImpulseTimer <= 0f || submergeImpulseDuration <= 0.0001f)
                return 0f;

            float timeFade = _submergeImpulseTimer / submergeImpulseDuration;
            float depthFade = 1f - math.saturate(depth / math.max(0.01f, submergeImpulseDepthWindow));
            return timeFade * depthFade;
        }

        private void RefreshAdaptiveBudgetResponse()
        {
            if (!enableAdaptiveBudgetResponse)
            {
                ApplyAdaptiveBudgetResponse(1f, 1f);
                return;
            }

            DynamicResolutionScaler scaler = DynamicResolutionScaler.Instance;
            if (scaler == null || !scaler.Enabled)
            {
                ApplyAdaptiveBudgetResponse(1f, 1f);
                return;
            }

            float renderScale = math.saturate(scaler.CurrentRenderScale);
            float normalized = math.saturate(
                (renderScale - adaptiveBudgetFloorRenderScale) /
                math.max(0.0001f, 1f - adaptiveBudgetFloorRenderScale));

            ApplyAdaptiveBudgetResponse(renderScale, normalized);
        }

        private void ApplyAdaptiveBudgetResponse(float renderScale, float normalized)
        {
            _adaptiveBudgetNormalized = normalized;
            _adaptiveMotesScale = math.lerp(adaptiveMotesBudgetFloor, 1f, normalized);
            _adaptiveBubbleScale = math.lerp(adaptiveBubbleBudgetFloor, 1f, normalized);
            _adaptiveBeamScale = math.lerp(adaptiveBeamBudgetFloor, 1f, normalized);
            _adaptiveCausticsScale = math.lerp(adaptiveCausticsBudgetFloor, 1f, normalized);
            _adaptiveBottomSiltProbeIntervalScale = math.lerp(adaptiveBottomSiltProbeIntervalMultiplier, 1f, normalized);

#if UNITY_EDITOR
            _debugAdaptiveRenderScale = renderScale;
            _debugAdaptiveBudgetNormalized = normalized;
            _debugAdaptiveMotesScale = _adaptiveMotesScale;
            _debugAdaptiveBubbleScale = _adaptiveBubbleScale;
            _debugAdaptiveBeamScale = _adaptiveBeamScale;
            _debugAdaptiveCausticsScale = _adaptiveCausticsScale;
            _debugAdaptiveBottomProbeScale = _adaptiveBottomSiltProbeIntervalScale;
#endif
        }

        private void UpdateUnderwaterSuspendedMotes(
            float depth,
            float lightFactor,
            float submergeImpulse,
            bool isUnderwater)
        {
            if (underwaterSuspendedMotes == null)
                ResolveUnderwaterParticles();

            if (underwaterSuspendedMotes == null)
                return;

            float targetEmission = 0f;
            bool shouldPlay = false;

            if (enableSuspendedMotes && isUnderwater)
            {
                float transportExposureScale = ResolveTransportHelmetExposureScale();
                float depthFactor = math.saturate(
                    depth / math.max(0.01f, suspendedMotesFullEmissionDepth));
                float turbidityFactor = math.saturate(
                    (_currentTurbidity - 0.5f) * suspendedMotesTurbidityWeight);
                float darknessFactor = 1f - lightFactor;
                float daylightVisibility = ResolveSurfaceDaylightVisibility();
                float clearWaterPresence = daylightVisibility * (1f - turbidityFactor);
                float densityFactor = math.saturate(
                    depthFactor * 0.45f +
                    turbidityFactor * 0.35f +
                    darknessFactor * 0.2f +
                    clearWaterPresence * UnderwaterClearWaterMotesStrength);

                targetEmission = math.lerp(
                    suspendedMotesMinEmission,
                    suspendedMotesMaxEmission,
                    densityFactor);
                targetEmission *= transportExposureScale;
                targetEmission *= _ecologySuspendedMotesMultiplier;
                targetEmission *= _adaptiveMotesScale;
                targetEmission += ResolveBottomSiltEmissionBoost(isUnderwater);
                targetEmission += submergeImpulse * suspendedMotesSubmergeBoost * transportExposureScale;
                shouldPlay = targetEmission > 0.01f;
            }
            else
            {
                ResolveBottomSiltEmissionBoost(false);
            }

            if (math.abs(targetEmission - _cachedSuspendedMotesEmission) > 0.05f)
            {
                ParticleSystem.EmissionModule emission = underwaterSuspendedMotes.emission;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(targetEmission);
                _cachedSuspendedMotesEmission = targetEmission;
            }

            if (shouldPlay)
            {
                if (!_underwaterSuspendedMotesPlaying)
                {
                    underwaterSuspendedMotes.Play(true);
                    _underwaterSuspendedMotesPlaying = true;
                }
            }
            else
            {
                DisableUnderwaterSuspendedMotes(true);
            }

#if UNITY_EDITOR
            _debugSuspendedMotesEmission = targetEmission;
#endif
        }

        private void DisableUnderwaterSuspendedMotes(bool clearParticles)
        {
            if (underwaterSuspendedMotes == null)
                return;

            if (_cachedSuspendedMotesEmission != 0f)
            {
                ParticleSystem.EmissionModule emission = underwaterSuspendedMotes.emission;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
                _cachedSuspendedMotesEmission = 0f;
            }

            if (_underwaterSuspendedMotesPlaying)
            {
                underwaterSuspendedMotes.Stop(
                    true,
                    clearParticles
                        ? ParticleSystemStopBehavior.StopEmittingAndClear
                        : ParticleSystemStopBehavior.StopEmitting);
                _underwaterSuspendedMotesPlaying = false;
            }

            _cachedBottomSiltBoost = 0f;

#if UNITY_EDITOR
            _debugSuspendedMotesEmission = 0f;
            _debugBottomDistance = 0f;
            _debugBottomSiltBoost = 0f;
#endif
        }

        private void DisableUnderwaterExhaleBubbles(bool clearParticles)
        {
            if (underwaterExhaleBubbles == null)
                return;

            if (underwaterExhaleBubbles.isPlaying || underwaterExhaleBubbles.particleCount > 0)
            {
                underwaterExhaleBubbles.Stop(
                    true,
                    clearParticles
                        ? ParticleSystemStopBehavior.StopEmittingAndClear
                        : ParticleSystemStopBehavior.StopEmitting);
            }

#if UNITY_EDITOR
            _debugExhaleBubbleBurstCount = 0;
#endif
        }

        private void HandlePlayerExhale()
        {
            if (!enableExhaleBubbles || !_cachedVisualIsUnderwater)
                return;

            if (ResolveTransportHelmetExposureScale() <= 0.001f)
                return;

            if (Time.unscaledTime < _nextExhaleBubbleAllowedTime)
                return;

            if (underwaterExhaleBubbles == null)
                ResolveUnderwaterExhaleBubbles();

            if (underwaterExhaleBubbles == null)
                return;

            int burstCount = ResolveExhaleBubbleBurstCount();
            if (burstCount <= 0)
                return;

            _nextExhaleBubbleAllowedTime = Time.unscaledTime + exhaleBubbleMinInterval;

            underwaterExhaleBubbles.Play(true);
            underwaterExhaleBubbles.Emit(burstCount);

#if UNITY_EDITOR
            _debugExhaleBubbleBurstCount = burstCount;
#endif
        }

        private int ResolveExhaleBubbleBurstCount()
        {
            int minBurst = math.max(0, exhaleBubbleMinBurstCount);
            int maxBurst = math.max(minBurst, exhaleBubbleMaxBurstCount);
            if (maxBurst <= 0)
                return 0;

            float depthFactor = math.saturate(
                _cachedVisualDepth / math.max(0.01f, exhaleBubbleFullDepth));
            float turbidityFactor = math.saturate((_currentTurbidity - 0.5f) * exhaleBubbleTurbidityWeight);
            float burstFactor = math.saturate(depthFactor * 0.65f + turbidityFactor * 0.35f);
            float transportExposureScale = ResolveTransportHelmetExposureScale();
            int burstCount = (int)math.round(
                math.lerp(minBurst, maxBurst, burstFactor) *
                _ecologyBubbleMultiplier *
                _adaptiveBubbleScale *
                transportExposureScale);
            return math.max(0, burstCount);
        }

        private float ResolveBottomSiltEmissionBoost(bool isUnderwater)
        {
            if (!enableBottomSiltBoost || !isUnderwater)
            {
                _cachedBottomSiltBoost = 0f;
                _cachedBottomDistance = float.PositiveInfinity;
#if UNITY_EDITOR
                _debugBottomDistance = 0f;
                _debugBottomSiltBoost = 0f;
#endif
                return 0f;
            }

            if (playerCamera == null)
                ResolvePlayerCamera();

            if (playerCamera == null)
            {
                _cachedBottomSiltBoost = 0f;
                _cachedBottomDistance = float.PositiveInfinity;
#if UNITY_EDITOR
                _debugBottomDistance = 0f;
                _debugBottomSiltBoost = 0f;
#endif
                return 0f;
            }

            RefreshBottomSiltProbe(playerCamera.position);

            if (_playerRigidbody == null)
            {
                Transform playerRoot = playerCamera.parent;
                if (playerRoot != null)
                    playerRoot.TryGetComponent(out _playerRigidbody);
            }

            float playerSpeed = _playerRigidbody != null ? _playerRigidbody.linearVelocity.magnitude : 0f;
            float distanceFactor = 1f - math.saturate(
                (_cachedBottomDistance - bottomSiltFullDistance) /
                math.max(0.01f, bottomSiltActivationDistance - bottomSiltFullDistance));
            float speedFactor = math.saturate(
                (playerSpeed - bottomSiltMinSpeed) /
                math.max(0.01f, bottomSiltFullSpeed - bottomSiltMinSpeed));

            float boost = bottomSiltEmissionBoost * distanceFactor * speedFactor * _adaptiveMotesScale + _externalBottomSiltBurstBoost;
            _cachedBottomSiltBoost = boost;

#if UNITY_EDITOR
            _debugBottomDistance = float.IsPositiveInfinity(_cachedBottomDistance) ? 0f : _cachedBottomDistance;
            _debugBottomSiltBoost = boost;
#endif
            return boost;
        }

        private void RefreshBottomSiltProbe(Vector3 probePosition)
        {
            if (Time.unscaledTime < _nextBottomSiltProbeTime)
                return;

            _nextBottomSiltProbeTime = Time.unscaledTime + math.max(0.05f, bottomSiltProbeInterval * _adaptiveBottomSiltProbeIntervalScale);
            _cachedBottomDistance = ResolveBottomSiltDistance(probePosition);
        }

        private float ResolveBottomSiltDistance(Vector3 probePosition)
        {
            MapMagicBridge bridge = MapMagicBridge.Instance;
            if (bridge != null && bridge.TryGetHeight(probePosition.x, probePosition.z, out float seafloorHeight))
                return math.max(0f, probePosition.y - seafloorHeight);

            float waterSurface = bridge != null ? bridge.WaterSurfaceLevel : ResolveWaterLevel();
            float rayOriginY = math.max(waterSurface + 1000f, probePosition.y + 1000f);
            Vector3 rayOrigin = new Vector3(probePosition.x, rayOriginY, probePosition.z);
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                rayOrigin,
                Vector3.down,
                _bottomSiltProbeHits,
                40000f,
                ~0,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.PositiveInfinity;
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit hit = _bottomSiltProbeHits[hitIndex];
                if (ShouldIgnoreBottomSiltHit(hit))
                    continue;

                float hitDistance = math.max(0f, probePosition.y - hit.point.y);
                if (hitDistance < bestDistance)
                    bestDistance = hitDistance;
            }

            return bestDistance;
        }

        internal void TriggerExternalBottomSiltBurst(float intensity01)
        {
            float clampedIntensity = math.saturate(intensity01);
            if (clampedIntensity <= 0f)
                return;

            float requestedBoost = bottomSiltEmissionBoost * clampedIntensity;
            if (requestedBoost > _externalBottomSiltBurstBoost)
                _externalBottomSiltBurstBoost = requestedBoost;
        }

        private void DecayExternalBottomSiltBurst(float deltaTime)
        {
            if (_externalBottomSiltBurstBoost <= 0f || deltaTime <= 0f)
                return;

            _externalBottomSiltBurstBoost = math.max(
                0f,
                _externalBottomSiltBurstBoost - bottomSiltBurstRecoverySpeed * bottomSiltEmissionBoost * deltaTime);
        }

        private bool ShouldIgnoreBottomSiltHit(RaycastHit hit)
        {
            Collider hitCollider = hit.collider;
            if (hitCollider == null)
                return true;

            Transform playerRoot = _playerRigidbody != null
                ? _playerRigidbody.transform
                : (playerCamera != null ? playerCamera.parent : null);
            Transform hitTransform = hitCollider.transform;
            if (playerRoot != null &&
                (hitTransform == playerRoot || hitTransform.IsChildOf(playerRoot)))
            {
                return true;
            }

            Rigidbody hitBody = hit.rigidbody;
            if (hitBody == null)
                return false;

            if (_playerRigidbody != null && hitBody == _playerRigidbody)
                return true;

            if (playerRoot == null)
                return false;

            Transform hitBodyTransform = hitBody.transform;
            return hitBodyTransform == playerRoot || hitBodyTransform.IsChildOf(playerRoot);
        }

        private void UpdateShallowSunBeam(float depth, float lightFactor, bool isUnderwater, Vector3 canopyAnchorWS, float canopyWindow01, float canopyOcclusion01)
        {
            if (shallowSunBeam == null || shallowSunBeamLight == null || _shallowSunBeamTransform == null)
                ResolveShallowSunBeam();

            if (shallowSunBeam == null || shallowSunBeamLight == null || _shallowSunBeamTransform == null)
                return;

            float targetIntensity = 0f;
            bool shouldActivate = false;

            if (enableShallowSunBeam && isUnderwater)
            {
                float fadeIn = math.saturate(depth / math.max(0.01f, shallowSunBeamFadeInDepth));
                float fadeOut = 1f - math.saturate(
                    (depth - shallowSunBeamFadeInDepth) /
                    math.max(0.01f, shallowSunBeamFadeOutDepth - shallowSunBeamFadeInDepth));
                float lightFade = math.saturate(
                    (lightFactor - shallowSunBeamMinLightFactor) /
                    math.max(0.0001f, 1f - shallowSunBeamMinLightFactor));
                float beamFactor = fadeIn * fadeOut * lightFade * ResolveHorizonFade();
                if (enableSargassumCanopyLighting)
                {
                    float canopyWindowFactor = Mathf.Lerp(
                        1f - sargassumCanopyBeamOcclusionStrength,
                        1f,
                        canopyWindow01 * sargassumCanopyBeamWindowBoost);
                    beamFactor *= canopyWindowFactor;
                    beamFactor *= 1f - (canopyOcclusion01 * sargassumCanopyBeamOcclusionStrength);
                }
                targetIntensity = shallowSunBeamMaxLightIntensity * beamFactor * _ecologySunBeamMultiplier * _adaptiveBeamScale * _soundscapeBeamScale;
                shouldActivate = targetIntensity > 0.001f;
            }

            if (shouldActivate)
            {
                GameObject beamObject = _shallowSunBeamTransform.gameObject;
                if (!_shallowSunBeamActive && !beamObject.activeSelf)
                    beamObject.SetActive(true);

                if (sunLight != null)
                {
                    Vector3 beamDirection = sunLight.transform.forward;
                    if (beamDirection.sqrMagnitude > 0.0001f)
                    {
                        beamDirection = beamDirection.normalized;
                        Vector3 beamUp = math.abs(Vector3.Dot(beamDirection, Vector3.up)) > 0.98f
                            ? (mainCamera != null ? mainCamera.transform.right : Vector3.right)
                            : Vector3.up;
                        _shallowSunBeamTransform.rotation = Quaternion.LookRotation(beamDirection, beamUp);
                    }
                }

                Vector3 beamLocalPosition = _shallowSunBeamBaseLocalPosition;
                if (enableSargassumCanopyLighting && mainCamera != null && canopyOcclusion01 > 0.0001f)
                {
                    Vector3 canopyDeltaWS = canopyAnchorWS - mainCamera.transform.position;
                    canopyDeltaWS.y = 0f;
                    float maxOffset = math.max(0.01f, sargassumCanopyBeamAnchorMaxOffset);
                    canopyDeltaWS = Vector3.ClampMagnitude(canopyDeltaWS, maxOffset);
                    Vector3 canopyDeltaLS = mainCamera.transform.InverseTransformVector(canopyDeltaWS);
                    canopyDeltaLS.z = 0f;
                    beamLocalPosition += canopyDeltaLS * (sargassumCanopyBeamAnchorTracking * canopyWindow01);
                }

                if ((_shallowSunBeamTransform.localPosition - beamLocalPosition).sqrMagnitude > 0.000001f)
                    _shallowSunBeamTransform.localPosition = beamLocalPosition;

                if (math.abs(targetIntensity - _cachedShallowSunBeamLightIntensity) > 0.01f)
                {
                    shallowSunBeamLight.intensity = targetIntensity;
                    _cachedShallowSunBeamLightIntensity = targetIntensity;
                }

                _shallowSunBeamActive = true;
            }
            else
            {
                DisableShallowSunBeam(true);
            }

#if UNITY_EDITOR
            _debugShallowSunBeamIntensity = targetIntensity;
#endif
        }

        private void DisableShallowSunBeam(bool setInactive)
        {
            if (shallowSunBeamLight != null && _cachedShallowSunBeamLightIntensity != 0f)
            {
                shallowSunBeamLight.intensity = 0f;
                _cachedShallowSunBeamLightIntensity = 0f;
            }

            if (_shallowSunBeamTransform != null && _shallowSunBeamActive && setInactive)
                _shallowSunBeamTransform.gameObject.SetActive(false);

            _shallowSunBeamActive = false;

#if UNITY_EDITOR
            _debugShallowSunBeamIntensity = 0f;
#endif
        }

        private float ResolveCausticsStrength(float depth, float lightFactor, bool isUnderwater)
        {
            if (!enableShallowCaustics || !isUnderwater)
            {
#if UNITY_EDITOR
                _debugCausticsStrength = 0f;
#endif
                return 0f;
            }

            float fadeIn = math.saturate(depth / math.max(0.01f, causticsFadeInDepth));
            float fadeOut = 1f - math.saturate(
                (depth - causticsFadeInDepth) /
                math.max(0.01f, causticsFadeOutDepth - causticsFadeInDepth));
            float lightFade = math.saturate(
                (lightFactor - causticsMinLightFactor) /
                math.max(0.0001f, 1f - causticsMinLightFactor));

            float strength = causticsStrengthScale * fadeIn * fadeOut * lightFade * _adaptiveCausticsScale * _soundscapeCausticsScale;

#if UNITY_EDITOR
            _debugCausticsStrength = strength;
#endif
            return strength;
        }

        private void ApplyEcologyContext(HectonBiomeMatrixProfile profile)
        {
            _currentFaunaMood = profile != null ? profile.faunaMood : WorldProceduralFaunaMood.None;

            HectonFaunaFamilyProfile faunaFamily = profile != null &&
                                                  profile.familyProfile != null
                ? profile.familyProfile.faunaFamilyProfile
                : null;

            _currentFaunaAmbienceSummary = faunaFamily != null ? faunaFamily.ambienceSummary : null;

            switch (_currentFaunaMood)
            {
                case WorldProceduralFaunaMood.Calm:
                    _ecologySuspendedMotesMultiplier = 0.92f;
                    _ecologyBubbleMultiplier = 0.94f;
                    _ecologySunBeamMultiplier = 1f + ecologySunBeamWeight;
                    break;

                case WorldProceduralFaunaMood.Lively:
                    _ecologySuspendedMotesMultiplier = 1f + ecologySuspendedMotesWeight;
                    _ecologyBubbleMultiplier = 1f + ecologyBubbleWeight;
                    _ecologySunBeamMultiplier = 1f + ecologySunBeamWeight * 0.4f;
                    break;

                case WorldProceduralFaunaMood.Mixed:
                    _ecologySuspendedMotesMultiplier = 1f + ecologySuspendedMotesWeight * 0.55f;
                    _ecologyBubbleMultiplier = 1f + ecologyBubbleWeight * 0.45f;
                    _ecologySunBeamMultiplier = 1f;
                    break;

                case WorldProceduralFaunaMood.Hostile:
                    _ecologySuspendedMotesMultiplier = 1f + ecologySuspendedMotesWeight * 0.75f;
                    _ecologyBubbleMultiplier = 1f + ecologyBubbleWeight * 0.7f;
                    _ecologySunBeamMultiplier = 1f - ecologySunBeamWeight;
                    break;

                default:
                    _ecologySuspendedMotesMultiplier = 1f;
                    _ecologyBubbleMultiplier = 1f;
                    _ecologySunBeamMultiplier = 1f;
                    break;
            }

#if UNITY_EDITOR
            _debugFaunaMood = _currentFaunaMood.ToString();
            _debugFaunaAmbience = string.IsNullOrWhiteSpace(_currentFaunaAmbienceSummary)
                ? "None"
                : _currentFaunaAmbienceSummary;
            _debugEcologyMotesMultiplier = _ecologySuspendedMotesMultiplier;
            _debugEcologyBubbleMultiplier = _ecologyBubbleMultiplier;
            _debugEcologyBeamMultiplier = _ecologySunBeamMultiplier;
#endif
        }

        private void ValidateReferences()
        {
            if (Application.isPlaying)
            {
                ResolvePlayerCamera();
                ResolveMainCamera();
                ResolveSunVisualTransform();
            }

            if (biomePalette == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] biomePalette not assigned.", this);
            if (oceanUnderwaterMaterial == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] oceanUnderwaterMaterial not assigned.", this);
            if (skyMaterial == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] skyMaterial not assigned.", this);
            if (globalLightCurve == null || globalLightCurve.length == 0)
                Debug.LogError("[HectonUnderwaterVisuals] globalLightCurve is empty!", this);
        }

        private void WarnIfRuntimeReferencesStillMissing()
        {
            if (!Application.isPlaying)
                return;

            if (Time.unscaledTime < _nextRuntimeReferenceWarningTime)
                return;

            _nextRuntimeReferenceWarningTime = Time.unscaledTime + 5f;

            if (playerCamera == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] playerCamera still unresolved after runtime retry.", this);

            if (mainCamera == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] mainCamera still unresolved after runtime retry.", this);

            if (sunVisualTransform == null)
                Debug.LogWarning("[HectonUnderwaterVisuals] sunVisualTransform still unresolved after runtime retry.", this);
        }

        private void ResolveBiomeMatrixDirector()
        {
            if (biomeMatrixDirector != null)
                return;

            if (Application.isPlaying)
            {
                WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
                return;
            }

#if UNITY_EDITOR
            biomeMatrixDirector = FindAnyObjectByType<BiomeMatrixDirector>(FindObjectsInactive.Include);
#endif
        }

        private void ApplyCurrentMatrixVisualOverride()
        {
            if (biomeMatrixDirector == null)
                return;

            HandleMatrixBiomeChanged(biomeMatrixDirector.CurrentProfile);
        }

        private HectonBiomeProfile ResolveActiveMatrixRuntimeVisualProfile()
        {
            if (_matrixRuntimeVisualProfile != null)
                return _matrixRuntimeVisualProfile;

            if (biomeMatrixDirector == null || biomeMatrixDirector.CurrentProfile == null)
                return null;

            return biomeMatrixDirector.CurrentProfile.runtimeVisualProfile;
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

        private void ApplySpaceCameraDepthState(float depth, bool isUnderwater)
        {
            if (!Application.isPlaying)
                return;

            Camera validatedMainCamera = ResolveValidCameraReference(ref mainCamera);
            ResolveSpaceCamera();
            Camera spaceCamera = ResolveValidCameraReference(ref _spaceCamera);
            bool canFallbackToMainCameraMask = _runtimeCameraStackFallbackActive && validatedMainCamera != null;
            if (spaceCamera == null && !canFallbackToMainCameraMask)
                return;

            bool shouldSuppress = ShouldSuppressSpaceCamera(depth, isUnderwater);
            if (_spaceCameraSuppressed == shouldSuppress)
                return;

            if (!_spaceCameraMaskCaptured && spaceCamera != null)
            {
                _spaceCameraOriginalCullingMask = spaceCamera.cullingMask;
                _spaceCameraMaskCaptured = true;
            }

            if (_runtimeCameraStackFallbackActive && validatedMainCamera != null)
            {
                int visibleMask = _mainCameraOriginalCullingMask | _CelestialLayerMask;
                int hiddenMask = visibleMask & ~_CelestialLayerMask;
                int targetMask = shouldSuppress ? hiddenMask : visibleMask;
                if (validatedMainCamera.cullingMask != targetMask)
                    validatedMainCamera.cullingMask = targetMask;
            }

            if (spaceCamera != null)
                spaceCamera.cullingMask = shouldSuppress ? 0 : _spaceCameraOriginalCullingMask;

            _spaceCameraSuppressed = shouldSuppress;
        }

        private bool ShouldSuppressSpaceCamera(float depth, bool isUnderwater)
        {
            if (!isUnderwater)
                return false;

            float renderScale = 1f;
            DynamicResolutionScaler scaler = DynamicResolutionScaler.Instance;
            if (scaler != null)
                renderScale = math.saturate(scaler.CurrentRenderScale);

            float depthReleaseThreshold = math.max(0f, deepCelestialCullDepth - deepCelestialCullDepthHysteresis);
            float adaptiveDepthReleaseThreshold = math.max(0f, adaptiveSpaceCameraCullMinDepth - deepCelestialCullDepthHysteresis);

            if (_spaceCameraSuppressed)
            {
                bool keepDepthSuppressed = depth >= depthReleaseThreshold;
                bool keepPerfSuppressed =
                    enableAdaptiveSpaceCameraCull &&
                    depth >= adaptiveDepthReleaseThreshold &&
                    renderScale <= adaptiveSpaceCameraRestoreRenderScale;
                return keepDepthSuppressed || keepPerfSuppressed;
            }

            bool suppressByDepth = depth >= deepCelestialCullDepth;
            bool suppressByPerf =
                enableAdaptiveSpaceCameraCull &&
                depth >= adaptiveSpaceCameraCullMinDepth &&
                renderScale <= adaptiveSpaceCameraCullRenderScale;
            return suppressByDepth || suppressByPerf;
        }

        private void RestoreSpaceCameraDefaults()
        {
            if (mainCamera != null && _cameraCompositionDefaultsCaptured)
                mainCamera.cullingMask = _mainCameraOriginalCullingMask;

            Camera spaceCamera = ResolveValidCameraReference(ref _spaceCamera);
            if (spaceCamera == null || !_spaceCameraMaskCaptured)
            {
                _spaceCameraSuppressed = false;
                return;
            }

            spaceCamera.cullingMask = _spaceCameraOriginalCullingMask;
            _spaceCameraSuppressed = false;
        }

        private static bool IsCameraReferenceValid(Camera camera)
        {
            if (ReferenceEquals(camera, null))
                return false;

            try
            {
                return camera != null;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (UnassignedReferenceException)
            {
                return false;
            }
        }

        private static Camera ResolveValidCameraReference(ref Camera camera)
        {
            if (IsCameraReferenceValid(camera))
                return camera;

            camera = null;
            return null;
        }

        private float ResolveWaterLevel()
        {
            if (!_physicsEngineCached)
                CachePhysicsEngine();
            if (_physicsEngine != null)
                return _physicsEngine.WaterLevel;
            return waterLevelFallback;
        }

        private float ResolveCurrentDepth()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return ResolveActiveVisualCameraDepth();
#endif

            float cameraDepth = ResolveActiveVisualCameraDepth();

            if (_playerMovement != null)
            {
                float movementDepth = _playerMovement.CurrentDepth;
                if (cameraDepth > movementDepth + VisualCameraDepthOverrideThreshold)
                    return cameraDepth;

                return movementDepth;
            }

            return cameraDepth;
        }

        private bool ResolveUnderwaterVisualState(float depth)
        {
            return ResolveUnderwaterVisualStateForCameraDepth(depth, ResolveActiveVisualCameraDepth());
        }

        private bool ResolveUnderwaterVisualStateForCameraDepth(float depth, float cameraDepth)
        {
            if (cameraDepth <= 0f)
                cameraDepth = depth;
            float visualDepth = math.max(depth, cameraDepth);

            if (visualDepth <= VisualExitUnderwaterDepth)
                return false;

            bool depthDrivenUnderwater = SurfaceStateUtility.ResolveUnderwaterFromDepth(
                visualDepth,
                _wasUnderwater,
                VisualEnterUnderwaterDepth,
                VisualExitUnderwaterDepth);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return depthDrivenUnderwater;
            }
#endif

            if (_playerMovement != null)
            {
                if (visualDepth >= VisualForcedUnderwaterDepth)
                    return true;

                switch (_playerMovement.CurrentLocomotionMode)
                {
                    case PlayerLocomotionMode.UnderwaterSwim:
                        return true;

                    case PlayerLocomotionMode.ExosuitLocomotion:
                        return _playerMovement.CurrentDepth > 0.01f || _playerMovement.IsPlayerSubmerged || depthDrivenUnderwater;

                    case PlayerLocomotionMode.SurfaceSwim:
                        return _playerMovement.IsPlayerSubmerged || depthDrivenUnderwater;

                    default:
                        return depthDrivenUnderwater;
                }
            }

            return depthDrivenUnderwater;
        }

        private float ResolveActiveVisualCameraDepth()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                SceneView sceneView = SceneView.lastActiveSceneView;
                Camera sceneViewCamera = sceneView != null ? sceneView.camera : null;
                if (sceneViewCamera != null)
                    return math.max(0f, ResolveWaterLevel() - sceneViewCamera.transform.position.y);

                Camera editorPreviewCamera = null;
                if (mainCamera != null && !IsRuntimeMainCamera(mainCamera))
                {
                    editorPreviewCamera = mainCamera;
                }
                else if (playerCamera != null)
                {
                    Camera playerOwnedCamera = playerCamera.GetComponent<Camera>();
                    if (playerOwnedCamera != null && !IsRuntimeMainCamera(playerOwnedCamera))
                        editorPreviewCamera = playerOwnedCamera;
                }

                if (editorPreviewCamera != null)
                    return math.max(0f, ResolveWaterLevel() - editorPreviewCamera.transform.position.y);
            }
#endif

            if (playerCamera != null)
                return math.max(0f, ResolveWaterLevel() - playerCamera.position.y);

            return 0f;
        }

        private float ResolveVisualDepthForCamera(Camera camera)
        {
            if (camera == null)
                return ResolveActiveVisualCameraDepth();

            return math.max(0f, ResolveWaterLevel() - camera.transform.position.y);
        }

        private void CachePlayerMovement(Transform playerTransform)
        {
            HectonPlayerMovement nextPlayerMovement = null;
            Rigidbody nextPlayerRigidbody = null;
            PlayerTransportCoordinator nextPlayerTransportCoordinator = null;

            if (playerTransform != null)
            {
                playerTransform.TryGetComponent(out nextPlayerMovement);
                playerTransform.TryGetComponent(out nextPlayerRigidbody);
                playerTransform.TryGetComponent(out nextPlayerTransportCoordinator);
            }

            if (!ReferenceEquals(_subscribedPlayerMovement, nextPlayerMovement))
            {
                UnsubscribePlayerMovement(_subscribedPlayerMovement);
                SubscribePlayerMovement(nextPlayerMovement);
            }

            _playerMovement = nextPlayerMovement;
            _playerRigidbody = nextPlayerRigidbody;
            _playerTransportCoordinator = nextPlayerTransportCoordinator;
            _debugPlayerMovementFound = _playerMovement != null;
            if (_playerMovement == null)
                _debugPlayerMovementSource = "Unresolved";
        }

        private float ResolveTransportHelmetExposureScale()
        {
            if (_playerTransportCoordinator == null)
                return 1f;

            PlayerTransportFeelContract transportFeelContract = _playerTransportCoordinator.ResolveTransportFeelContract();
            if (transportFeelContract == null)
                return 1f;

            switch (transportFeelContract.OccupancyMode)
            {
                case PlayerTransportOccupancyMode.EnclosedCabin:
                    return 0f;

                case PlayerTransportOccupancyMode.Exosuit:
                    return math.min(0.5f, math.saturate(transportFeelContract.SwimPresentationScale));

                default:
                    return 1f;
            }
        }

        private void UpdateTransportCockpitOverlay()
        {
            if (transitionCameraVfx == null)
                return;

            if (_playerTransportCoordinator == null)
            {
                transitionCameraVfx.SetTransportCockpitOverlay(0f, 1f, 0.32f, 0f);
                return;
            }

            PlayerTransportPreset transportPreset = _playerTransportCoordinator.ResolveTransportPreset();
            if (transportPreset == null)
            {
                transitionCameraVfx.SetTransportCockpitOverlay(0f, 1f, 0.32f, 0f);
                return;
            }

            switch (_playerTransportCoordinator.ResolveTransportOccupancyMode())
            {
                case PlayerTransportOccupancyMode.Exosuit:
                    transitionCameraVfx.SetTransportCockpitOverlay(
                        transportPreset.CockpitVignetteIntensity > 0f ? transportPreset.CockpitVignetteIntensity : 0.12f,
                        transportPreset.CockpitVignetteRoundness > 0f ? transportPreset.CockpitVignetteRoundness : 0.38f,
                        transportPreset.CockpitVignetteSmoothness > 0f ? transportPreset.CockpitVignetteSmoothness : 0.52f,
                        transportPreset.CockpitChromaticAberration);
                    return;

                case PlayerTransportOccupancyMode.EnclosedCabin:
                    transitionCameraVfx.SetTransportCockpitOverlay(
                        transportPreset.CockpitVignetteIntensity > 0f ? transportPreset.CockpitVignetteIntensity : 0.24f,
                        transportPreset.CockpitVignetteRoundness > 0f ? transportPreset.CockpitVignetteRoundness : 0.96f,
                        transportPreset.CockpitVignetteSmoothness > 0f ? transportPreset.CockpitVignetteSmoothness : 0.44f,
                        transportPreset.CockpitChromaticAberration > 0f ? transportPreset.CockpitChromaticAberration : 0.04f);
                    return;

                default:
                    transitionCameraVfx.SetTransportCockpitOverlay(0f, 1f, 0.32f, 0f);
                    return;
            }
        }

        private bool TryCachePlayerMovementFromTransformHierarchy(Transform anchor, string sourceLabel)
        {
            Transform current = anchor;
            while (current != null)
            {
                if (current.TryGetComponent(out HectonPlayerMovement movement))
                {
                    CachePlayerMovement(current);
                    if (movement != null)
                        _debugPlayerMovementSource = sourceLabel;
                    return movement != null;
                }

                current = current.parent;
            }

            return false;
        }

        private void SubscribePlayerMovement(HectonPlayerMovement movement)
        {
            if (movement == null)
                return;

            movement.OnExhale += HandlePlayerExhale;
            _subscribedPlayerMovement = movement;
        }

        private void UnsubscribePlayerMovement(HectonPlayerMovement movement)
        {
            if (movement == null)
                return;

            movement.OnExhale -= HandlePlayerExhale;
            if (ReferenceEquals(_subscribedPlayerMovement, movement))
                _subscribedPlayerMovement = null;
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
                _currentScatterBase     = ResolveSafeCrestColor(
                    ReadMaterialColorOrDefault(oceanUnderwaterMaterial, _ID_Diffuse, new Color(0f, 0.03f, 0.07f, 1f)),
                    new Color(0f, 0.03f, 0.07f, 1f));
                _currentScatterShallow  = ResolveSafeCrestColor(
                    ReadMaterialColorOrDefault(oceanUnderwaterMaterial, _ID_SubSurfaceShallowCol, new Color(0f, 0.15f, 0.12f, 1f)),
                    new Color(0f, 0.15f, 0.12f, 1f));
                _currentDepthFogDensity = ResolveFallbackDepthFogDensity(oceanUnderwaterMaterial);
                _currentFogColor        = ResolveFallbackFogColor();
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  UTILITY
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateSoundscapeDiagnostics()
        {
            _debugSoundscapeTier = _currentSoundscapeTier.ToString();
            _debugSoundscapeFogScale = _soundscapeFogDensityScale;
            _debugSoundscapeAmbientScale = _soundscapeAmbientScale;
            _debugSoundscapeBeamScale = _soundscapeBeamScale;
            _debugSoundscapeCausticsScale = _soundscapeCausticsScale;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  GIZMOS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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
                float lf = ResolveDepthLightFactor(depth);

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

            float maxTime = useBeerLambertDepthAttenuation
                ? Mathf.Max(
                    beerLambertBlackoutDepth,
                    globalLightCurve[globalLightCurve.length - 1].time)
                : globalLightCurve[globalLightCurve.length - 1].time;
            const float threshold = 0.005f;
            const int samples = 100;
            float step = maxTime / samples;

            for (int i = 1; i <= samples; i++)
            {
                float t = i * step;
                float v = ResolveDepthLightFactor(t);
                if (v <= threshold)
                    return t;
            }

            return maxTime;
        }
#endif
    }
}
