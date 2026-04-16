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
using Hecton8.Bootstrap;
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
        private const float RuntimeCameraResolveRetryInterval = 1f;
        private const float EditorCameraResolveRetryInterval = 0.25f;
        private const float VisualEnterUnderwaterDepth = 0.01f;
        private const float VisualExitUnderwaterDepth = 0.005f;
        private const string UnderwaterSuspendedMotesChildName = "Underwater_SuspendedMotes";
        private const string UnderwaterExhaleBubblesChildName = "Underwater_ExhaleBubbles";
        private const string UnderwaterShallowSunBeamChildName = "Underwater_ShallowSunBeam";

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REFERENCES
        // ══════════════════════════════════════════════════════════

        [Header("═══ REFERENCES ═══")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] private Light sunLight;
        [SerializeField] private LensFlareComponentSRP sunFlare;
        [SerializeField] private Transform sunVisualTransform;
        [SerializeField] private Camera mainCamera;
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

        [Header("═══ ATMOSPHERE MANAGER ═══")]
        [SerializeField] private HectonAtmosphereManager atmosphereManager;

        [Header("═══ CREST MATERIAL ═══")]
        [SerializeField] private Material oceanUnderwaterMaterial;

        [Header("═══ SKY MATERIAL ═══")]
        [SerializeField] private Material skyMaterial;

        [Header("═══ BIOME PALETTE ═══")]
        [SerializeField] private HectonOceanPalette biomePalette;

        [Header("═══ VERTICAL RUNTIME ═══")]
        [SerializeField] private BiomeMatrixDirector biomeMatrixDirector;

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
        [UnityEngine.Range(0.0001f, 0.05f)]
        [SerializeField] private float minFogDensity = 0.002f;

        [UnityEngine.Range(0.01f, 0.5f)]
        [SerializeField] private float maxFogDensity = 0.08f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CONFIGURATION
        // ══════════════════════════════════════════════════════════

        [Header("═══ WATER LEVEL ═══")]
        [SerializeField] private float waterLevelFallback = 4900f;

        [Header("═══ DEEP CELESTIAL CULL ═══")]
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

        [Header("═══ SUN VISUAL ═══")]
        [UnityEngine.Range(0.0001f, 0.05f)]
        [SerializeField] private float sunVisualDisableThreshold = 0.005f;

        [Header("═══ SUN SCATTERING ═══")]
        [SerializeField] private float baseSunSize = 0.002f;
        [SerializeField] private float underwaterSunSizeMax = 0.15f;
        [SerializeField] private float baseSunEdgeSoftness = 0.001f;
        [SerializeField] private float underwaterSunSoftnessMax = 0.5f;

        [Header("═══ TRANSITION ═══")]
        [UnityEngine.Range(0.05f, 2.0f)]
        [SerializeField] private float biomeTransitionSpeed = 0.2f;
        [SerializeField] private float slowTickInterval = 0.5f;

        [Header("â•â•â• SUBMERGE IMPULSE â•â•â•")]
        [SerializeField, UnityEngine.Range(0f, 0.6f)] private float submergeDarkenStrength = 0.2f;
        [SerializeField, UnityEngine.Range(0f, 2f)] private float submergeFogBoost = 0.45f;
        [SerializeField, UnityEngine.Range(0.05f, 1f)] private float submergeImpulseDuration = 0.32f;
        [SerializeField, UnityEngine.Range(0.1f, 2f)] private float submergeImpulseDepthWindow = 0.9f;

        [Header("â•â•â• SHALLOW CAUSTICS â•â•â•")]
        [SerializeField] private bool enableShallowCaustics = true;
        [SerializeField, UnityEngine.Range(0.1f, 4f)] private float causticsStrengthScale = 1f;
        [SerializeField, UnityEngine.Range(0.05f, 2f)] private float causticsFadeInDepth = 0.3f;
        [SerializeField, UnityEngine.Range(1f, 40f)] private float causticsFadeOutDepth = 18f;
        [SerializeField, UnityEngine.Range(0f, 1f)] private float causticsMinLightFactor = 0.18f;

        [Header("─── UNDERWATER MOTES ───")]
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

        [Header("─── BOTTOM SILT ───")]
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

        [Header("─── EXHALE BUBBLES ───")]
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

        [Header("─── SHALLOW SUN BEAM ───")]
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

        [Header("─── ECOLOGY RESPONSE ───")]
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
        private static readonly int _ID_Diffuse =
            Shader.PropertyToID("_Diffuse");
        private static readonly int _ID_DiffuseGrazing =
            Shader.PropertyToID("_DiffuseGrazing");
        private static readonly int _ID_SubSurfaceShallowCol =
            Shader.PropertyToID("_SubSurfaceShallowCol");
        private static readonly int _ID_DepthFogDensity =
            Shader.PropertyToID("_DepthFogDensity");
        private static readonly int _ID_SunSize =
            Shader.PropertyToID("_SunSize");
        private static readonly int _ID_SunEdgeSoftness =
            Shader.PropertyToID("_SunEdgeSoftness");
        private static readonly int _ID_Caustics =
            Shader.PropertyToID("_Caustics");
        private static readonly int _ID_CausticsStrength =
            Shader.PropertyToID("_CausticsStrength");
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
        private HectonPlayerMovement _playerMovement;
        private HectonPlayerMovement _subscribedPlayerMovement;
        private Rigidbody _playerRigidbody;
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
        private float _submergeImpulseTimer;
        private float _cachedVisualDepth;
        private float _cachedLightFactor;
        private float _cachedCausticsStrength;
        private float _cachedSuspendedMotesEmission = -1f;
        private float _cachedBottomDistance = float.PositiveInfinity;
        private float _cachedBottomSiltBoost;
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
        private Camera _gameplayMainCamera;
        private Camera _spaceCamera;
        private Camera _capturedCompositionMainCamera;
        private Camera _capturedCompositionSpaceCamera;
        private CrestOceanRenderer _oceanRenderer;
        private Transform _shallowSunBeamTransform;
        private CrestUnderwaterRenderer _mainCameraUnderwaterRenderer;
        private CrestUnderwaterRenderer _spaceCameraUnderwaterRenderer;
        private Material _lastRuntimeOceanMaterial;
        private bool _pendingRuntimeCrestMaterialSync;
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
        private bool _editorCrestUnderwaterRendererWasEnabled;
        private Camera _editorGameplaySpaceCamera;
        private bool _editorGameplayMainCameraWasEnabled;
        private bool _editorGameplaySpaceCameraWasEnabled;
#endif

        private float _editorSlowTickAccum;
        private readonly RaycastHit[] _bottomSiltProbeHits = new RaycastHit[4]; // COLD ALLOC: RaycastHit[4] — reused seafloor probe buffer for underwater bottom-silt gating — owner: HectonUnderwaterVisuals

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
#endif

            if (Application.isPlaying)
            {
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
            _pendingRuntimeCrestMaterialSync = true;
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
            _sunVisualWasDisabled = false;
        }

        private void EnsureGameplayCameraStackEnabled()
        {
            if (mainCamera != null && !mainCamera.enabled)
                mainCamera.enabled = true;

            if (_spaceCamera != null && !_spaceCamera.enabled)
                _spaceCamera.enabled = true;

            ApplyGameplayCameraCompositionMode();
            EnsureCrestUnderwaterPassOwnership();
        }

        private void ApplyGameplayCameraCompositionMode()
        {
            if (!Application.isPlaying || mainCamera == null)
                return;

            ResolveSpaceCamera();
            if (_spaceCamera == null)
                return;

            if (!mainCamera.TryGetComponent(out UniversalAdditionalCameraData mainCameraData) ||
                mainCameraData == null ||
                !_spaceCamera.TryGetComponent(out UniversalAdditionalCameraData spaceCameraData) ||
                spaceCameraData == null)
            {
                return;
            }

            CaptureGameplayCameraCompositionDefaults(mainCameraData, spaceCameraData);

            if (SupportsGameplayCameraStacking(mainCameraData, spaceCameraData))
            {
                RestoreGameplayCameraCompositionDefaults(mainCameraData, spaceCameraData);
                return;
            }

            ApplyGameplayCameraCompositionFallback(mainCameraData, spaceCameraData);
        }

        private void CaptureGameplayCameraCompositionDefaults(
            UniversalAdditionalCameraData mainCameraData,
            UniversalAdditionalCameraData spaceCameraData)
        {
            if (_cameraCompositionDefaultsCaptured &&
                ReferenceEquals(_capturedCompositionMainCamera, mainCamera) &&
                ReferenceEquals(_capturedCompositionSpaceCamera, _spaceCamera))
            {
                return;
            }

            _capturedCompositionMainCamera = mainCamera;
            _capturedCompositionSpaceCamera = _spaceCamera;
            _mainCameraOriginalDepth = mainCamera.depth;
            _spaceCameraOriginalDepth = _spaceCamera.depth;
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
            UniversalAdditionalCameraData spaceCameraData)
        {
            if (spaceCameraData.renderType != CameraRenderType.Base)
                spaceCameraData.renderType = CameraRenderType.Base;

            if (mainCameraData.renderType != CameraRenderType.Base)
                mainCameraData.renderType = CameraRenderType.Base;

            float fallbackSpaceDepth = _cameraCompositionDefaultsCaptured ? _spaceCameraOriginalDepth : _spaceCamera.depth;
            float fallbackMainDepth = math.max(
                _cameraCompositionDefaultsCaptured ? _mainCameraOriginalDepth : mainCamera.depth,
                fallbackSpaceDepth + 1f);

            if (!Mathf.Approximately(_spaceCamera.depth, fallbackSpaceDepth))
                _spaceCamera.depth = fallbackSpaceDepth;

            if (!Mathf.Approximately(mainCamera.depth, fallbackMainDepth))
                mainCamera.depth = fallbackMainDepth;

            EnsureFallbackMainCameraCelestialVisibility(mainCamera);
            _runtimeCameraStackFallbackActive = true;
            ApplyRuntimeMainCameraClearFlags(CameraClearFlags.Depth);
        }

        private void RestoreGameplayCameraCompositionDefaults(
            UniversalAdditionalCameraData mainCameraData,
            UniversalAdditionalCameraData spaceCameraData)
        {
            if (!_cameraCompositionDefaultsCaptured)
                return;

            if (spaceCameraData.renderType != _spaceCameraOriginalRenderType)
                spaceCameraData.renderType = _spaceCameraOriginalRenderType;

            if (mainCameraData.renderType != _mainCameraOriginalRenderType)
                mainCameraData.renderType = _mainCameraOriginalRenderType;

            if (!Mathf.Approximately(_spaceCamera.depth, _spaceCameraOriginalDepth))
                _spaceCamera.depth = _spaceCameraOriginalDepth;

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
            _pendingRuntimeCrestMaterialSync = true;
            _lastRuntimeOceanMaterial = null;
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
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR UPDATE
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (Application.isPlaying) return;
            if (this == null) return;

            if (!IsEditorPreviewActive())
            {
                SuspendEditorWaterRendering();
                return;
            }

            ResumeEditorWaterRendering();

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

        private static bool IsEditorPreviewActive()
        {
            return !EditorApplication.isCompiling &&
                   !EditorApplication.isUpdating;
        }

        private void ResolveEditorCamera()
        {
            if (Application.isPlaying) return;

            Camera authoredGameplayCamera = null;
            if (IsRuntimeMainCamera(mainCamera))
            {
                authoredGameplayCamera = mainCamera;
            }
            else if (playerCamera != null)
            {
                Camera playerOwnedCamera = playerCamera.GetComponent<Camera>();
                if (IsRuntimeMainCamera(playerOwnedCamera))
                    authoredGameplayCamera = playerOwnedCamera;
            }

            // Preserve scene source-of-truth when the underwater owner is already
            // wired to the gameplay camera. SceneView fallback is only for unwired preview.
            if (authoredGameplayCamera != null)
            {
                if (!ReferenceEquals(mainCamera, authoredGameplayCamera))
                    mainCamera = authoredGameplayCamera;
                if (playerCamera == null || !ReferenceEquals(playerCamera, authoredGameplayCamera.transform))
                    playerCamera = authoredGameplayCamera.transform;

                _nextEditorCameraResolveTime = float.NegativeInfinity;
                return;
            }

            var sv = SceneView.lastActiveSceneView;
            if (sv != null && sv.camera != null)
            {
                Camera sceneViewCamera = sv.camera;
                if (mainCamera != sceneViewCamera)
                    mainCamera = sceneViewCamera;
                if (playerCamera == null || playerCamera == mainCamera.transform)
                    playerCamera = sceneViewCamera.transform;
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
                _editorGameplaySpaceCamera != null &&
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
            EnsureRuntimeVisualOwners();
            TryApplyRuntimeCrestMaterialSync();

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

            // ══════════════════════════════════════════════
            //  ABOVE WATER
            // ══════════════════════════════════════════════

            if (!isUnderwater)
            {
                if (_wasUnderwater)
                {
                    TriggerSurfaceBreakImpulse();
                    _cachedLightFactor = 1f;
                    _cachedCausticsStrength = 0f;
                    ApplySurfaceDefaults();
                    RestoreSkyMaterialDefaults();
                    _wasUnderwater = false;
                }

                UpdateUnderwaterSuspendedMotes(depth, 1f, 0f, false);
                DisableUnderwaterExhaleBubbles(true);
                UpdateShallowSunBeam(depth, 1f, false);

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
                    sunLight.intensity = baseSun * horizon * ResolveSurfaceSunMultiplier();

                    UpdateSurfaceLightDiagnostics(
                        baseSun,
                        horizon,
                        baseSun * horizon * ResolveSurfaceSunMultiplier());
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
                TriggerSubmergeImpulse();
                _wasUnderwater = true;
            }

            // ══════════════════════════════════════════════
            //  UNDERWATER — DEPTH-DRIVEN
            // ══════════════════════════════════════════════

            float lightFactor = math.saturate(globalLightCurve.Evaluate(depth));
            float submergeImpulse = EvaluateSubmergeImpulse(depth);
            lightFactor *= 1f - (submergeDarkenStrength * submergeImpulse);
            _cachedLightFactor = lightFactor;
            _cachedCausticsStrength = ResolveCausticsStrength(depth, lightFactor, isUnderwater);
            UpdateUnderwaterSuspendedMotes(depth, lightFactor, submergeImpulse, true);
            UpdateShallowSunBeam(depth, lightFactor, true);

            // ── Sun intensity = profile × horizon × depthCurve ──
            float baseSunIntensity = ResolveProfileSunIntensity();
            float horizonFade = ResolveHorizonFade();
            float finalSunIntensity = baseSunIntensity * horizonFade * lightFactor;

            ApplySunIntensity(finalSunIntensity, lightFactor);
            ApplySunVisualState(lightFactor);
            ApplySunScattering(lightFactor);
            ApplySunColorFade(lightFactor);
            ApplyUnderwaterFog(lightFactor, depth, submergeImpulse);
            ApplyUnderwaterAmbient();
            ApplyUnderwaterCamera();

            UpdateLightDiagnostics(lightFactor, baseSunIntensity, horizonFade, finalSunIntensity);
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable.SlowTick — 2Hz
        // ══════════════════════════════════════════════════════════

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
                _cachedAtmoManager = HectonAtmosphereManager.Instance;

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

        private Color ResolveSurfaceFogColor()
        {
            return _surfaceWeatherOverrideActive
                ? _surfaceWeatherFogColor
                : surfaceFogColor;
        }

        private float ResolveSurfaceFogDensity()
        {
            return _surfaceWeatherOverrideActive
                ? _surfaceWeatherFogDensity
                : surfaceFogDensity;
        }

        private Color ResolveSurfaceAmbientColor()
        {
            return _surfaceWeatherOverrideActive
                ? _surfaceWeatherAmbientColor
                : surfaceAmbientColor;
        }

        private float ResolveSurfaceSunMultiplier()
        {
            return _surfaceWeatherOverrideActive
                ? _surfaceWeatherSunMultiplier
                : 1f;
        }

        private void ApplyUnderwaterFog(float lightFactor, float currentDepth, float submergeImpulse)
        {
            Color fogColor = ResolveUnderwaterFogColor();
            RenderSettings.fogColor = fogColor;

            float baseDensity = Mathf.Lerp(maxFogDensity, minFogDensity, lightFactor);
            float targetDensity = baseDensity * _currentTurbidity;
            targetDensity *= _soundscapeFogDensityScale;
            targetDensity *= 1f + (submergeFogBoost * submergeImpulse);

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
                    _spaceCamera == null ||
                    _spaceCameraUnderwaterRenderer == null)
                {
                    EnsureRuntimeVisualOwners();
                    EnsureGameplayCameraStackEnabled();
                }
            }

            if (_wasUnderwater)
            {
                RenderSettings.fog = true;
                RenderSettings.fogColor = ResolveUnderwaterFogColor();
                RenderSettings.fogDensity = _cachedFogDensity;
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

        // ══════════════════════════════════════════════════════════
        //  AMBIENT / CAMERA
        // ══════════════════════════════════════════════════════════

        private void ApplyUnderwaterAmbient()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;

            Color effectiveAmbient = ResolveUnderwaterAmbientColor();
            Color ambient;
            ambient.r = math.max(effectiveAmbient.r, MIN_AMBIENT.r);
            ambient.g = math.max(effectiveAmbient.g, MIN_AMBIENT.g);
            ambient.b = math.max(effectiveAmbient.b, MIN_AMBIENT.b);
            ambient.a = 1f;

            RenderSettings.ambientLight = ambient;
        }

        private void ApplyUnderwaterCamera()
        {
            if (mainCamera == null) return;
            mainCamera.backgroundColor = ResolveUnderwaterFogColor();
            ApplyRuntimeMainCameraClearFlags(CameraClearFlags.SolidColor);
        }

        private Color ResolveUnderwaterAmbientColor()
        {
            Color ambientColor = _currentAmbientColor;
            ambientColor.r *= _soundscapeAmbientScale;
            ambientColor.g *= _soundscapeAmbientScale;
            ambientColor.b *= _soundscapeAmbientScale;

            if (_soundscapeThermalTintBlend > 0.001f)
                ambientColor = Color.Lerp(ambientColor, thermalTierTintColor, _soundscapeThermalTintBlend);

            ambientColor.a = 1f;
            return ambientColor;
        }

        private Color ResolveUnderwaterFogColor()
        {
            if (_soundscapeThermalTintBlend <= 0.001f)
                return _currentFogColor;

            Color fogColor = Color.Lerp(_currentFogColor, thermalTierTintColor, _soundscapeThermalTintBlend);
            fogColor.a = 1f;
            return fogColor;
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
                sunLight.intensity = baseSun * horizon * ResolveSurfaceSunMultiplier();
            }

            if (_baseValuesCaptured && sunFlare != null)
            {
                sunFlare.intensity = _baseFlareIntensity;
                if (!sunFlare.enabled)
                    sunFlare.enabled = true;
            }

            HideSunVisualAboveWater();

            if (enableSurfaceFog)
            {
                RenderSettings.fog        = true;
                RenderSettings.fogMode    = FogMode.ExponentialSquared;
                RenderSettings.fogColor   = ResolveSurfaceFogColor();
                RenderSettings.fogDensity = ResolveSurfaceFogDensity();
            }
            else
            {
                RenderSettings.fog = false;
            }

            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = ResolveSurfaceAmbientColor();

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
            ApplyCrestMaterial(oceanUnderwaterMaterial);

            Material runtimeOceanMaterial = CrestOceanRenderer.Instance != null
                ? CrestOceanRenderer.Instance.OceanMaterial
                : null;

            if (runtimeOceanMaterial != null &&
                !ReferenceEquals(runtimeOceanMaterial, oceanUnderwaterMaterial))
            {
                ApplyCrestMaterial(runtimeOceanMaterial);
            }

            _lastRuntimeOceanMaterial = runtimeOceanMaterial;
            _pendingRuntimeCrestMaterialSync = false;
        }

        private void ApplyCrestMaterial(Material targetMaterial)
        {
            if (targetMaterial == null)
                return;

            SetMaterialColorIfPresent(targetMaterial, _ID_ScatterColourBase, _currentScatterBase);
            SetMaterialColorIfPresent(targetMaterial, _ID_Diffuse, _currentScatterBase);
            SetMaterialColorIfPresent(targetMaterial, _ID_DiffuseGrazing, _currentScatterBase);
            SetMaterialColorIfPresent(targetMaterial, _ID_ScatterColourShallow, _currentScatterShallow);
            SetMaterialColorIfPresent(targetMaterial, _ID_SubSurfaceShallowCol, _currentScatterShallow);
            SetMaterialVectorIfPresent(
                targetMaterial,
                _ID_DepthFogDensity,
                new Vector4(
                    _currentDepthFogDensity.x,
                    _currentDepthFogDensity.y,
                    _currentDepthFogDensity.z,
                    0f));
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_Caustics,
                _cachedCausticsStrength > 0.001f ? 1f : 0f);
            SetMaterialFloatIfPresent(
                targetMaterial,
                _ID_CausticsStrength,
                _cachedCausticsStrength);
        }

        // ══════════════════════════════════════════════════════════
        //  BIOME EVENT
        // ══════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

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
                return math.saturate(globalLightCurve.Evaluate(d));
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
                if (Time.unscaledTime < _nextRuntimePlayerCameraResolveTime)
                    return;

                _nextRuntimePlayerCameraResolveTime = Time.unscaledTime + RuntimeCameraResolveRetryInterval;
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                {
                    CachePlayerMovement(playerTransform);

                    Camera playerOwnedCamera = playerTransform.GetComponentInChildren<Camera>(true);
                    if (playerOwnedCamera != null)
                    {
                        playerCamera = playerOwnedCamera.transform;
                        return;
                    }

                    playerCamera = playerTransform;
                    return;
                }

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
                    mainCamera = playerTransform.GetComponentInChildren<Camera>(true);
                    if (mainCamera != null)
                        return;
                }

                if (TryGetComponent(out Camera localCamera) && IsRuntimeMainCamera(localCamera))
                {
                    mainCamera = localCamera;
                    return;
                }

                Camera childCamera = GetComponentInChildren<Camera>(true);
                if (IsRuntimeMainCamera(childCamera))
                {
                    mainCamera = childCamera;
                    return;
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

            if (_editorGameplaySpaceCamera != null)
                return;

            ResolveGameplayMainCameraForEditor();
            if (_gameplayMainCamera == null)
                return;

            Transform spaceCameraTransform = _gameplayMainCamera.transform.Find("SpaceCamera");
            if (spaceCameraTransform == null)
                return;

            _editorGameplaySpaceCamera = spaceCameraTransform.GetComponent<Camera>();
        }

        private void ResolveSpaceCamera()
        {
            if (_spaceCamera != null)
                return;

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

            _spaceCamera = spaceCameraTransform.GetComponent<Camera>();
            if (_spaceCamera == null || _spaceCameraMaskCaptured)
                return;

            _spaceCameraOriginalCullingMask = _spaceCamera.cullingMask;
            _spaceCameraMaskCaptured = true;

            if (Application.isPlaying)
                EnsureCrestUnderwaterPassOwnership();
        }

        private void EnsureCrestUnderwaterPassOwnership()
        {
            if (!Application.isPlaying || mainCamera == null)
                return;

            if (!mainCamera.TryGetComponent(out _mainCameraUnderwaterRenderer) ||
                _mainCameraUnderwaterRenderer == null)
            {
                _mainCameraUnderwaterRenderer = null;
                return;
            }

            ResolveSpaceCamera();
            if (_spaceCamera == null)
                return;

            if (!_spaceCamera.TryGetComponent(out _spaceCameraUnderwaterRenderer) ||
                _spaceCameraUnderwaterRenderer == null)
            {
                // COLD ALLOC: UnderwaterRenderer[1] — ensure Crest fullscreen underwater pass is present on the base camera stack owner — owner: HectonUnderwaterVisuals
                _spaceCameraUnderwaterRenderer =
                    _spaceCamera.gameObject.AddComponent<CrestUnderwaterRenderer>();
            }

            CopyUnderwaterRendererSettings(
                _mainCameraUnderwaterRenderer,
                _spaceCameraUnderwaterRenderer);

            EnsureCrestOceanCameraOwnership();
        }

        private void EnsureCrestOceanCameraOwnership()
        {
            if (!Application.isPlaying || _spaceCamera == null)
                return;

            _oceanRenderer = CrestOceanRenderer.Instance;
            if (_oceanRenderer == null)
                return;

            if (!ReferenceEquals(_oceanRenderer.ViewCamera, _spaceCamera))
                _oceanRenderer.ViewCamera = _spaceCamera;

            Transform spaceCameraTransform = _spaceCamera.transform;
            if (!ReferenceEquals(_oceanRenderer.Viewpoint, spaceCameraTransform))
                _oceanRenderer.Viewpoint = spaceCameraTransform;
        }

        private void EnsureRuntimeVisualOwners()
        {
            if (!Application.isPlaying)
                return;

            if (playerCamera == null)
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

            if (_spaceCamera == null)
                ResolveSpaceCamera();

            if (_spaceCamera != null)
                EnsureCrestOceanCameraOwnership();

            if (_spaceCameraUnderwaterRenderer == null || _mainCameraUnderwaterRenderer == null)
                EnsureCrestUnderwaterPassOwnership();
        }

        private void TryApplyRuntimeCrestMaterialSync()
        {
            if (!Application.isPlaying)
                return;

            CrestOceanRenderer oceanRenderer = CrestOceanRenderer.Instance;
            Material runtimeOceanMaterial = oceanRenderer != null ? oceanRenderer.OceanMaterial : null;
            if (runtimeOceanMaterial == null)
                return;

            bool densityLooksStale =
                oceanRenderer != null &&
                oceanRenderer.UnderwaterDepthFogDensity.sqrMagnitude <= 0.000001f &&
                _currentDepthFogDensity.sqrMagnitude > 0.000001f;

            if (!_pendingRuntimeCrestMaterialSync &&
                !densityLooksStale &&
                ReferenceEquals(runtimeOceanMaterial, _lastRuntimeOceanMaterial))
            {
                return;
            }

            ApplyCrestMaterial();
        }

        private static bool IsRuntimeMainCamera(Camera camera)
        {
            return camera != null &&
                   camera.cameraType != CameraType.SceneView &&
                   camera.CompareTag("MainCamera");
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
                float depthFactor = math.saturate(
                    depth / math.max(0.01f, suspendedMotesFullEmissionDepth));
                float turbidityFactor = math.saturate(
                    (_currentTurbidity - 0.5f) * suspendedMotesTurbidityWeight);
                float darknessFactor = 1f - lightFactor;
                float densityFactor = math.saturate(
                    depthFactor * 0.45f +
                    turbidityFactor * 0.35f +
                    darknessFactor * 0.2f);

                targetEmission = math.lerp(
                    suspendedMotesMinEmission,
                    suspendedMotesMaxEmission,
                    densityFactor);
                targetEmission *= _ecologySuspendedMotesMultiplier;
                targetEmission *= _adaptiveMotesScale;
                targetEmission += ResolveBottomSiltEmissionBoost(isUnderwater);
                targetEmission += submergeImpulse * suspendedMotesSubmergeBoost;
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
            int burstCount = (int)math.round(math.lerp(minBurst, maxBurst, burstFactor) * _ecologyBubbleMultiplier * _adaptiveBubbleScale);
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

            float boost = bottomSiltEmissionBoost * distanceFactor * speedFactor * _adaptiveMotesScale;
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

        private void UpdateShallowSunBeam(float depth, float lightFactor, bool isUnderwater)
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
            if (!Application.isPlaying)
                return;

            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
        }

        private void ApplyCurrentMatrixVisualOverride()
        {
            if (!Application.isPlaying)
                return;

            if (biomeMatrixDirector == null)
                return;

            HandleMatrixBiomeChanged(biomeMatrixDirector.CurrentProfile);
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
            ResolveSpaceCamera();
            if (_spaceCamera == null && mainCamera == null)
                return;

            bool shouldSuppress = ShouldSuppressSpaceCamera(depth, isUnderwater);
            if (_spaceCameraSuppressed == shouldSuppress)
                return;

            if (!_spaceCameraMaskCaptured)
            {
                _spaceCameraOriginalCullingMask = _spaceCamera.cullingMask;
                _spaceCameraMaskCaptured = true;
            }

            if (_runtimeCameraStackFallbackActive && mainCamera != null)
            {
                int visibleMask = _mainCameraOriginalCullingMask | _CelestialLayerMask;
                int hiddenMask = visibleMask & ~_CelestialLayerMask;
                int targetMask = shouldSuppress ? hiddenMask : visibleMask;
                if (mainCamera.cullingMask != targetMask)
                    mainCamera.cullingMask = targetMask;
            }

            if (_spaceCamera != null)
                _spaceCamera.cullingMask = shouldSuppress ? 0 : _spaceCameraOriginalCullingMask;

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

            if (_spaceCamera == null || !_spaceCameraMaskCaptured)
            {
                _spaceCameraSuppressed = false;
                return;
            }

            _spaceCamera.cullingMask = _spaceCameraOriginalCullingMask;
            _spaceCameraSuppressed = false;
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
            if (_playerMovement != null)
                return _playerMovement.CurrentDepth;

            if (playerCamera != null)
                return math.max(0f, ResolveWaterLevel() - playerCamera.position.y);

            return 0f;
        }

        private bool ResolveUnderwaterVisualState(float depth)
        {
            if (_playerMovement != null)
            {
                switch (_playerMovement.CurrentLocomotionMode)
                {
                    case PlayerLocomotionMode.UnderwaterSwim:
                        return true;

                    case PlayerLocomotionMode.SurfaceSwim:
                        return _playerMovement.IsPlayerSubmerged;

                    default:
                        return false;
                }
            }

            return SurfaceStateUtility.ResolveUnderwaterFromDepth(
                depth,
                _wasUnderwater,
                VisualEnterUnderwaterDepth,
                VisualExitUnderwaterDepth);
        }

        private void CachePlayerMovement(Transform playerTransform)
        {
            HectonPlayerMovement nextPlayerMovement = null;
            Rigidbody nextPlayerRigidbody = null;

            if (playerTransform != null)
            {
                playerTransform.TryGetComponent(out nextPlayerMovement);
                playerTransform.TryGetComponent(out nextPlayerRigidbody);
            }

            if (!ReferenceEquals(_subscribedPlayerMovement, nextPlayerMovement))
            {
                UnsubscribePlayerMovement(_subscribedPlayerMovement);
                SubscribePlayerMovement(nextPlayerMovement);
            }

            _playerMovement = nextPlayerMovement;
            _playerRigidbody = nextPlayerRigidbody;
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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateSoundscapeDiagnostics()
        {
            _debugSoundscapeTier = _currentSoundscapeTier.ToString();
            _debugSoundscapeFogScale = _soundscapeFogDensityScale;
            _debugSoundscapeAmbientScale = _soundscapeAmbientScale;
            _debugSoundscapeBeamScale = _soundscapeBeamScale;
            _debugSoundscapeCausticsScale = _soundscapeCausticsScale;
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
