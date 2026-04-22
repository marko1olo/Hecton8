// ============================================================================
// HECTON-8 â€” HectonPlayerMovement.cs  v7.0
// Rigidbody-based hybrid player movement â€” FULL IMMERSION BUILD
//
// v7.0 ADDITIONS:
//   â€¢ Depth calculation + feeding to CameraJuiceInput
//   â€¢ Depth-based swim slowdown (pressure resistance)
//   â€¢ Collision camera shake via OnCollisionEnter
//   â€¢ Splash / submerge events exposed as pollable properties
//   â€¢ FOV offset applied from CameraJuiceProcessor
//   â€¢ Visual pitch inertia fed through juice processor
//   â€¢ Exhale event exposed
//   â€¢ New diagnostic fields for depth, FOV, splash, exhale
//
// v6.3 PRESERVED:
//   â€¢ Crest dynamic height, smoothed immersion, single GroundCheck
//   â€¢ Surface lock, graduated gravity, ground snap, mode detection
//   â€¢ Zero-rotation Rigidbody, zero-jitter camera
// ============================================================================

using Hecton8.Core;
using Hecton8.Audio;
using Hecton8.Environment;
using Hecton8.Interaction;
using Hecton8.Physics;
using Hecton8.UI;
using Hecton8.Input;
using Hecton8.Meta;
using Hecton8.Tools;
using Hecton8.Visor;
using Hecton8.World;
using Hecton.Localization;
using NASAPunk.Visor;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HectonPlayerMovement : MonoBehaviour, ITickable, IFixedTickable
    {
        private const float GroundCheckSkin = 0.02f;
        private float _runtimeSwimSpeedMultiplier = 1f;
        private float _runtimeInjurySwimSpeedMultiplier = 1f;
        private const float TwoPi = 6.28318530718f;
        private const string DefaultWaterEntrySplashClipPath = "Assets/_Project/Audio/Movement/dive_splash.wav";
        private const int CrestBodySampleCount = 5;
        private const int CrestSampleCenter = 0;
        private const int CrestSampleHead = 1;
        private const int CrestSampleFeet = 2;
        private const int CrestSampleLeft = 3;
        private const int CrestSampleRight = 4;
        private static readonly string[] _locomotionModeLabels =
        {
            "DryGroundWalk",
            "DryInteriorWalk",
            "ShallowWadeWalk",
            "SurfaceSwim",
            "UnderwaterSwim",
            "ExosuitLocomotion"
        }; // COLD ALLOC: string[6] â€” editor diagnostics labels â€” owner: HectonPlayerMovement

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” REFERENCES
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ References â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] private SuitData currentSuitData;
        [SerializeField] private ControlScheme controlScheme;
        [SerializeField] private bool leanIntoTurn = true;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” WATER CONFIGURATION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Water Configuration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Fallback water surface Y when Crest is unavailable.")]
        [SerializeField] private float waterSurfaceY = 4900f;

        [SerializeField] private float playerHeight = 1.8f;

        [SerializeField, Range(0.3f, 0.95f)]
        [Tooltip("Immersion ratio above which player switches from walking to swimming.")]
        private float swimTransitionThreshold = 0.7f;

        [Header("â”€â”€ Surface Swim Realism â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Depth band near the waterline treated as surface swim instead of deep 3D swim.")]
        [SerializeField, Range(0.1f, 2.5f)] private float surfaceSwimDepthBand = 0.85f;
        [Tooltip("How strongly forward swim is flattened near the surface. 1 = strongly planar.")]
        [SerializeField, Range(0f, 1f)] private float surfaceForwardPitchSuppression = 0.85f;
        [Tooltip("Forward swim force multiplier while surface swimming.")]
        [SerializeField, Range(0.1f, 1f)] private float surfaceForwardForceMultiplier = 0.82f;
        [Tooltip("Strafe swim force multiplier while surface swimming.")]
        [SerializeField, Range(0.1f, 1f)] private float surfaceStrafeForceMultiplier = 0.72f;
        [Tooltip("Vertical swim force multiplier while surface swimming.")]
        [SerializeField, Range(0.1f, 1f)] private float surfaceVerticalForceMultiplier = 0.4f;
        [Tooltip("Extra drag applied while surface swimming.")]
        [SerializeField, Range(1f, 3f)] private float surfaceDragMultiplier = 1.35f;
        [Tooltip("Max speed multiplier while surface swimming.")]
        [SerializeField, Range(0.2f, 1f)] private float surfaceMaxSpeedMultiplier = 0.72f;
        [Tooltip("Depth window where upward surface escape is strongly damped.")]
        [SerializeField, Range(0.02f, 0.6f)] private float surfaceAscendReleaseDepth = 0.18f;
        [Tooltip("Damping applied to upward velocity at the top of the water.")]
        [SerializeField, Range(0f, 20f)] private float surfaceAscendVelocityDamping = 5f;
        [Tooltip("Minimum camera look-down angle below the horizon that counts as deliberate surface dive intent. Runtime never allows values below 30 degrees.")]
        [SerializeField, Range(0f, 85f)] private float surfaceDivePitchCommit = 30f;
        [Tooltip("Minimum forward input that counts as deliberate surface dive intent.")]
        [SerializeField, Range(0f, 1f)] private float surfaceDiveForwardCommit = 0.35f;
        [Tooltip("How long deliberate dive input must stay committed before SurfaceSwim releases into full underwater motion.")]
        [SerializeField, Range(0f, 0.35f)] private float surfaceDiveCommitHoldTime = 0.12f;
        [Tooltip("How long a committed surface dive keeps the player out of surface-lock locomotion.")]
        [SerializeField, Range(0.04f, 0.5f)] private float surfaceDiveAssistDuration = 0.18f;
        [Tooltip("Extra downward swim-force multiplier applied while breaking through the surface into a dive.")]
        [SerializeField, Range(0f, 3f)] private float surfaceDiveAssistForceMultiplier = 1.15f;
        [Tooltip("World-space Y offset applied to the player root while SurfaceSwim sticks to the sampled wave height.")]
        [SerializeField, Range(-2f, 0.5f)] private float surfaceStickOffset = -0.62f;
        [Tooltip("How quickly surface snap blends in when entering SurfaceSwim.")]
        [SerializeField, Range(1f, 30f)] private float surfaceSnapEngageSpeed = 18f;
        [Tooltip("How quickly surface snap blends out when leaving SurfaceSwim.")]
        [SerializeField, Range(1f, 30f)] private float surfaceSnapReleaseSpeed = 12f;
        [Tooltip("How quickly the snapped player root follows Crest wave height while surface swimming.")]
        [SerializeField, Range(1f, 40f)] private float surfaceWaveFollowSharpness = 20f;
        [Tooltip("How deep below the surface the head must be before a deliberate dive unlocks free 3D swim.")]
        [SerializeField, Range(0.05f, 1.5f)] private float surfaceDiveBreakDepth = 0.28f;
        [Tooltip("How close the head must get to the surface to snap back into SurfaceSwim while ascending.")]
        [SerializeField, Range(0f, 0.25f)] private float surfaceHeadReattachDepth = 0.05f;
        [Tooltip("Upward velocity above which crossing the waterline keeps SurfaceSwim reattach disabled so dolphin breaches can clear the surface.")]
        [SerializeField, Range(0.5f, 12f)] private float surfaceBreachReleaseVelocity = 3.25f;
        [Tooltip("How long a fast upward breach keeps SurfaceSwim reattach disabled.")]
        [SerializeField, Range(0.05f, 0.6f)] private float surfaceBreachLockDuration = 0.24f;
        [Tooltip("Extra damping applied against downward velocity while breaking through the surface.")]
        [SerializeField, Range(0f, 20f)] private float surfaceDiveResistanceDamping = 4.5f;
        [Tooltip("How much Crest's sampled vertical surface velocity influences the player while surface-sticking.")]
        [SerializeField, Range(0f, 1f)] private float surfaceWaveVelocityInfluence = 0.75f;
        [Tooltip("Feet depth below the water surface at which grounded shoreline contact can hand off from swimming to walking.")]
        [SerializeField, Range(0.05f, 2f)] private float shoreWalkFootDepth = 1.05f;
        [Tooltip("Feet-to-bottom clearance where shoreline buoyancy has fully recovered from shallow-bottom interference.")]
        [SerializeField, Range(0.1f, 3f)] private float shoreBuoyancyRecoveryClearance = 1.35f;
        [Tooltip("How quickly shoreline buoyancy fades out near bottom contact and recovers in deeper water.")]
        [SerializeField, Range(1f, 30f)] private float shoreBuoyancyBlendSharpness = 10f;
        [Tooltip("Smoothed shoreline buoyancy blend below which shallow grounded contact hands control back to walking.")]
        [SerializeField, Range(0f, 1f)] private float shoreWalkHandoffBuoyancyThreshold = 0.42f;
        [Tooltip("Minimum downward speed required to trigger the heavy water-entry damping burst.")]
        [SerializeField, Range(0f, 20f)] private float waterEntryImpactMinSpeed = 5f;
        [Tooltip("Peak extra linear damping applied after an air-to-water impact.")]
        [SerializeField, Range(0f, 30f)] private float waterEntryImpactDamping = 14f;
        [Tooltip("How long the heavy water-entry damping burst lasts after an air-to-water impact.")]
        [SerializeField, Range(0.1f, 1.25f)] private float waterEntryImpactDuration = 0.7f;
        [Tooltip("Positive FOV kick applied on hard water entry before the rebound compression.")]
        [SerializeField, Range(0f, 15f)] private float waterEntryImpactFovExpand = 4.5f;
        [Tooltip("Negative FOV rebound applied after the initial water-entry expansion.")]
        [SerializeField, Range(0f, 12f)] private float waterEntryImpactFovCompress = 2.1f;
        [Tooltip("3D splash clip for fast downward surface entries.")]
        [SerializeField] private AudioClip waterEntrySplashClip;
        [Tooltip("3D splash clip for fast upward surface breaches. Falls back to entry clip when null.")]
        [SerializeField] private AudioClip waterExitSplashClip;
        [Tooltip("Minimum vertical speed required before a surface-pierce splash one-shot is played.")]
        [SerializeField, Range(0f, 20f)] private float surfacePierceSplashMinSpeed = 3.5f;
        [Tooltip("Vertical speed where surface-pierce splash volume reaches maximum.")]
        [SerializeField, Range(0.5f, 25f)] private float surfacePierceSplashMaxSpeed = 10f;
        [Tooltip("Minimum 3D splash volume at the playback threshold.")]
        [SerializeField, Range(0f, 1f)] private float surfacePierceSplashMinVolume = 0.45f;
        [Tooltip("Maximum 3D splash volume for the fastest surface-pierce events.")]
        [SerializeField, Range(0f, 1f)] private float surfacePierceSplashMaxVolume = 1f;
        [Tooltip("How quickly controller-level wet-lens intensity decays back toward dry after a pulse.")]
        [SerializeField, Range(0.25f, 10f)] private float wetLensSignalRecoverySpeed = 1.6f;
        [Tooltip("Storm intensity threshold before crest-over-camera can emit a wet-lens pulse.")]
        [SerializeField, Range(0f, 1f)] private float wetLensStormIntensityThreshold = 0.4f;
        [Tooltip("How far the water surface must overtake the camera before a storm crest counts as a wet-lens hit.")]
        [SerializeField, Range(0f, 0.25f)] private float wetLensWaveCoverDepth = 0.035f;
        [Tooltip("Cooldown between automatic storm-driven wet-lens pulses so rough water does not spam consumers.")]
        [SerializeField, Range(0.05f, 1f)] private float wetLensStormPulseCooldown = 0.18f;
        [Tooltip("Base wet-lens pulse intensity emitted when storm swell overtakes the camera at the surface.")]
        [SerializeField, Range(0f, 1f)] private float wetLensStormPulseIntensity = 0.24f;
        [Tooltip("Base wet-lens pulse intensity emitted on fast upward dolphin breaches.")]
        [SerializeField, Range(0f, 1f)] private float wetLensBreachPulseIntensity = 0.82f;

        [Header("── Surf Zone Extremes ───────────────────────")]
        [Tooltip("Storm intensity threshold before shoreline backwash can resolve into a real undertow pull.")]
        [SerializeField, Range(0f, 1f)] private float shoreUndertowStormThreshold = 0.32f;
        [Tooltip("Depth below the surface where shoreline undertow is fully faded out.")]
        [SerializeField, Range(0.1f, 4f)] private float shoreUndertowMaxDepth = 1.7f;
        [Tooltip("Retreating water speed along the beach downslope where undertow begins to pull the player seaward.")]
        [SerializeField, Range(0.05f, 4f)] private float shoreUndertowRetreatVelocityStart = 0.35f;
        [Tooltip("Retreating water speed along the beach downslope where undertow reaches full force.")]
        [SerializeField, Range(0.1f, 6f)] private float shoreUndertowRetreatVelocityMax = 2.1f;
        [Tooltip("Base mass-scaled undertow force applied when storm backwash drags the player off the shoreline.")]
        [SerializeField, Range(0f, 320f)] private float shoreUndertowForce = 115f;
        [Tooltip("How much undertow force scales up while the player is still partly buoyant and fighting the shore handoff.")]
        [SerializeField, Range(1f, 3f)] private float shoreUndertowSurfaceBoost = 1.3f;
        [Tooltip("Feet-depth threshold below which shoreline undertow is suppressed so knee-deep water does not unrealistically drag the player offshore.")]
        [SerializeField, Range(0.05f, 1.5f)] private float shoreUndertowMinFeetDepth = 0.45f;
        [Tooltip("Feet-depth where shoreline undertow reaches full authored strength after the knee-deep suppression band.")]
        [SerializeField, Range(0.1f, 2f)] private float shoreUndertowFullFeetDepth = 1f;
        [Tooltip("Delta-velocity threshold where a hard transport crash or breach landing becomes a wipeout.")]
        [SerializeField, Range(1f, 30f)] private float wipeoutImpactDeltaVelocityThreshold = 9.5f;
        [Tooltip("Delta-velocity where wipeout severity reaches authored maximum.")]
        [SerializeField, Range(2f, 40f)] private float wipeoutImpactDeltaVelocityMax = 21f;
        [Tooltip("How long control stays disabled after a hard wipeout impact.")]
        [SerializeField, Range(0.5f, 3f)] private float wipeoutDuration = 1.7f;
        [Tooltip("Extra damping applied while the player is recovering from a wipeout so the crash does not instantly re-stabilize.")]
        [SerializeField, Range(0f, 20f)] private float wipeoutRecoveryDrag = 4.8f;
        [Tooltip("Impulse fired away from the impact normal when a wipeout starts.")]
        [SerializeField, Range(0f, 20f)] private float wipeoutReboundImpulse = 6.5f;
        [Tooltip("Additional transport damage multiplier applied when a wipeout crash happens on an active scooter or mount.")]
        [SerializeField, Range(1f, 4f)] private float wipeoutTransportDamageScale = 1.55f;
        [Tooltip("Chance that a hard wipeout breaks one installed suit upgrade module and disables its runtime bonuses.")]
        [SerializeField, Range(0f, 1f)] private float wipeoutSuitUpgradeBreakChance = 0.2f;
        [Tooltip("How long a fast breach exit keeps solid-land collision eligible for wipeout logic.")]
        [SerializeField, Range(0.1f, 2f)] private float wipeoutBreachLandingGraceTime = 1.15f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” CREST OCEAN INTEGRATION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Crest Ocean Integration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Enable dynamic water height from Crest Ocean waves.")]
        [SerializeField] private bool useCrestOceanHeight = true;
        [Tooltip("Optional explicit ocean-kinematics provider. Scene scan prefers the active Crest adapter when no provider is assigned.")]
        [FormerlySerializedAs("crestOceanRenderer")]
        [SerializeField] private MonoBehaviour oceanKinematicsProvider;
        [Tooltip("How much Crest flow velocity is converted into passive player drift velocity.")]
        [SerializeField, Range(0f, 2f)] private float crestFlowVelocityScale = 0.35f;
        [Tooltip("How strongly Crest flow is applied as a drift force to the rigidbody.")]
        [SerializeField, Range(0f, 8f)] private float crestFlowForceResponsiveness = 1.6f;
        [Tooltip("How strongly active swim input opposing Crest flow suppresses passive drift.")]
        [SerializeField, Range(0f, 1f)] private float crestFlowOppositionReduction = 0.82f;
        [Tooltip("How strongly cross-current swim input suppresses passive Crest drift.")]
        [SerializeField, Range(0f, 1f)] private float crestFlowCrossCurrentReduction = 0.45f;
        [Tooltip("Fail-safe floor for Crest drift influence while the player is actively swimming.")]
        [SerializeField, Range(0f, 1f)] private float crestFlowInputMinimumScale = 0.18f;
        [Tooltip("Blend speed for Crest drift attenuation when the player starts or stops fighting the current.")]
        [SerializeField, Range(0.5f, 20f)] private float crestFlowInputBlendSpeed = 8f;
        [Tooltip("Extra Crest drift response applied while surface swimming with no movement input so idle swimmers do not read as anchored to world space.")]
        [SerializeField, Range(1f, 4f)] private float crestFlowSurfaceIdleBoost = 1.45f;
        [Tooltip("Planar input magnitude below which the player is treated as idle for surface Crest drift.")]
        [SerializeField, Range(0f, 0.35f)] private float crestFlowIdleInputThreshold = 0.08f;
        [Tooltip("Minimum Crest wavelength used for player-body water sampling. 0 keeps full available detail.")]
        [SerializeField, Range(0f, 4f)] private float crestBodySampleMinLength = 0.4f;
        [Tooltip("Forward sample distance used for batched Crest body queries around the player.")]
        [SerializeField, Range(0.15f, 2f)] private float crestBodyForwardSampleDistance = 0.62f;
        [Tooltip("Lateral sample distance used for batched Crest body queries around the player.")]
        [SerializeField, Range(0.1f, 1.25f)] private float crestBodyLateralSampleDistance = 0.34f;
        [Tooltip("How quickly sampled wave pitch and roll settle onto the player swim presentation root.")]
        [SerializeField, Range(1f, 30f)] private float surfaceWaveAlignmentSharpness = 10f;
        [Tooltip("Maximum local pitch contributed by Crest multi-point surface alignment.")]
        [SerializeField, Range(0f, 30f)] private float surfaceWaveMaxPitch = 14f;
        [Tooltip("Maximum local roll contributed by Crest multi-point surface alignment.")]
        [SerializeField, Range(0f, 30f)] private float surfaceWaveMaxRoll = 18f;
        [Tooltip("Maximum depth below the surface where storm turbulence still meaningfully shoves the swimmer.")]
        [SerializeField, Range(1f, 15f)] private float underwaterTurbulenceMaxDepth = 10f;
        [Tooltip("Wave-height span across the player footprint where storm turbulence begins.")]
        [SerializeField, Range(0.05f, 3f)] private float underwaterTurbulenceHeightStart = 0.35f;
        [Tooltip("Wave-height span across the player footprint where storm turbulence reaches full strength.")]
        [SerializeField, Range(0.1f, 4f)] private float underwaterTurbulenceHeightMax = 1.35f;
        [Tooltip("Horizontal Crest displacement magnitude where storm turbulence begins.")]
        [SerializeField, Range(0.05f, 3f)] private float underwaterTurbulenceDisplacementStart = 0.25f;
        [Tooltip("Horizontal Crest displacement magnitude where storm turbulence reaches full strength.")]
        [SerializeField, Range(0.1f, 4f)] private float underwaterTurbulenceDisplacementMax = 1.2f;
        [Tooltip("Horizontal Crest water speed where storm turbulence reaches full strength.")]
        [SerializeField, Range(0.1f, 8f)] private float underwaterTurbulenceVelocityMax = 3.2f;
        [Tooltip("Base lateral surf-zone turbulence force applied near the surface during heavy swell.")]
        [SerializeField, Range(0f, 300f)] private float underwaterTurbulenceForce = 95f;
        [Tooltip("Base vertical shove applied by storm turbulence under breaking waves.")]
        [SerializeField, Range(0f, 220f)] private float underwaterTurbulenceVerticalForce = 58f;
        [Tooltip("Oscillation frequency used to keep storm turbulence alive instead of static.")]
        [SerializeField, Range(0.1f, 6f)] private float underwaterTurbulenceFrequency = 1.55f;
        [Tooltip("Maximum additive pitch sent to swim presentation while surf turbulence throws the player around.")]
        [SerializeField, Range(0f, 18f)] private float underwaterTurbulencePitch = 5.5f;
        [Tooltip("Maximum additive roll sent to swim presentation while surf turbulence throws the player around.")]
        [SerializeField, Range(0f, 20f)] private float underwaterTurbulenceRoll = 8.5f;
        [Tooltip("How quickly turbulence pose settles toward the currently sampled storm target.")]
        [SerializeField, Range(1f, 30f)] private float underwaterTurbulencePoseSharpness = 9f;
        [Tooltip("Bottom clearance where surf-zone turbulence stops receiving shallow-bottom amplification.")]
        [SerializeField, Range(0.1f, 8f)] private float underwaterTurbulenceBottomInfluenceDepth = 2.6f;
        [Tooltip("Maximum extra turbulence multiplier applied when storm water is forced through shallow bottom clearance.")]
        [SerializeField, Range(1f, 4f)] private float underwaterTurbulenceBottomBoost = 1.65f;
        [Tooltip("Normalized turbulence level where underwater disorientation visuals begin to ramp in.")]
        [SerializeField, Range(0f, 1f)] private float underwaterStressSignalThreshold = 0.28f;
        [Tooltip("How quickly the underwater disorientation signal converges toward the current turbulence target.")]
        [SerializeField, Range(1f, 30f)] private float underwaterStressSignalBlendSharpness = 8f;
        [Tooltip("Depth below the surface where near-surface transport cavitation begins to fade out.")]
        [SerializeField, Range(0.1f, 2f)] private float transportCavitationStartDepth = 1f;
        [Tooltip("Depth below the surface where near-surface transport cavitation is fully recovered.")]
        [SerializeField, Range(0.2f, 3f)] private float transportCavitationRecoveryDepth = 1.5f;
        [Tooltip("Forward acceleration where near-surface transport cavitation starts reducing thrust efficiency.")]
        [SerializeField, Range(0f, 20f)] private float transportCavitationAccelerationStart = 2.5f;
        [Tooltip("Forward acceleration where near-surface transport cavitation reaches full authored loss.")]
        [SerializeField, Range(0.1f, 30f)] private float transportCavitationAccelerationMax = 11f;
        [Tooltip("Minimum propulsion efficiency retained during severe near-surface cavitation.")]
        [SerializeField, Range(0.05f, 1f)] private float transportCavitationMinEfficiency = 0.42f;
        [Tooltip("How quickly near-surface transport cavitation converges toward the current efficiency target.")]
        [SerializeField, Range(1f, 30f)] private float transportCavitationBlendSharpness = 10f;
        [Header("Dynamic Collision Deformation")]
        [Tooltip("How quickly the physical capsule converges toward the current wave-driven tuck target.")]
        [SerializeField, Range(1f, 30f)] private float dynamicCollisionDeformationBlendSharpness = 11f;
        [Tooltip("Downhill wave slope where dynamic capsule tuck reaches full authored strength.")]
        [SerializeField, Range(0.01f, 1f)] private float dynamicCollisionTuckSlopeForFull = 0.32f;
        [Tooltip("Immersion depth below the surface where wave-driven collision tuck reaches full influence.")]
        [SerializeField, Range(0.05f, 2f)] private float dynamicCollisionImmersionDepthForFull = 0.52f;
        [Tooltip("Minimum capsule height scale while the swimmer fully tucks on a steep descending wave face.")]
        [SerializeField, Range(0.3f, 1f)] private float dynamicCollisionMinHeightScale = 0.58f;
        [Tooltip("Maximum capsule radius scale while the swimmer fully tucks on a steep descending wave face.")]
        [SerializeField, Range(1f, 2f)] private float dynamicCollisionMaxRadiusScale = 1.32f;
        [Tooltip("Center offset applied while the swimmer tucks so the collider collapses downward toward a compact ball.")]
        [SerializeField, Range(-0.5f, 0.5f)] private float dynamicCollisionCenterYOffset = -0.14f;
        [Header("Active Trauma Collision")]
        [Tooltip("How long active-trauma collision inflation stays armed before the collider starts recovering.")]
        [SerializeField, Range(0.02f, 1f)] private float physicalTraumaCollisionHoldTime = 0.24f;
        [Tooltip("How quickly active-trauma collision recovery settles back toward the baseline capsule.")]
        [SerializeField, Range(1f, 30f)] private float physicalTraumaCollisionRecoverySharpness = 9f;
        [Tooltip("Additional radius scale applied while active trauma keeps the body in a defensive tucked collision state.")]
        [SerializeField, Range(1f, 2f)] private float physicalTraumaCollisionRadiusScale = 1.18f;
        [Tooltip("Additional height scale applied while active trauma compresses the body away from nearby geometry.")]
        [SerializeField, Range(0.3f, 1f)] private float physicalTraumaCollisionHeightScale = 0.76f;
        [Tooltip("Additional downward center offset applied during active trauma so the collision capsule protects the bent torso.")]
        [SerializeField, Range(-0.5f, 0.2f)] private float physicalTraumaCollisionCenterYOffset = -0.1f;
        [Header("Abyssal Currents")]
        [Tooltip("Depth where abyssal downdrafts start arming below otherwise stable underwater swim.")]
        [SerializeField, Range(50f, 500f)] private float abyssalCurrentStartDepth = 100f;
        [Tooltip("Depth where abyssal downdrafts reach full authored strength.")]
        [SerializeField, Range(60f, 800f)] private float abyssalCurrentFullDepth = 220f;
        [Tooltip("Longest downtime between abyssal downdraft pulses near the arming depth.")]
        [SerializeField, Range(0.5f, 12f)] private float abyssalDowndraftIntervalMax = 4.8f;
        [Tooltip("Shortest downtime between abyssal downdraft pulses in the deepest armed water.")]
        [SerializeField, Range(0.15f, 8f)] private float abyssalDowndraftIntervalMin = 1.65f;
        [Tooltip("Minimum downward velocity-change applied by an abyssal downdraft pulse.")]
        [SerializeField, Range(0.1f, 8f)] private float abyssalDowndraftVelocityChangeMin = 1.65f;
        [Tooltip("Maximum downward velocity-change applied by an abyssal downdraft pulse.")]
        [SerializeField, Range(0.2f, 14f)] private float abyssalDowndraftVelocityChangeMax = 4.9f;
        [Tooltip("How much sampled biome/current flow can tilt an abyssal downdraft away from pure vertical.")]
        [SerializeField, Range(0f, 1f)] private float abyssalDowndraftHorizontalBias = 0.3f;
        [Tooltip("How long a downdraft pulse keeps draining energy while the player fights upward against it.")]
        [SerializeField, Range(0.1f, 2f)] private float abyssalDowndraftAftershockDuration = 0.85f;
        [Tooltip("Energy drained per second while the player actively resists an active abyssal downdraft.")]
        [SerializeField, Range(0f, 20f)] private float abyssalDowndraftCounterEnergyDrain = 5.2f;
        [Tooltip("Minimum noisy-flow delta treated as crossing a turbulent abyssal-current seam.")]
        [SerializeField, Range(0.05f, 8f)] private float abyssalFlowNoiseBoundaryThreshold = 1.1f;
        [Tooltip("VelocityChange applied to the rider when the scooter crosses a turbulent abyssal-current seam.")]
        [SerializeField, Range(0f, 6f)] private float abyssalFlowNoiseBoundaryVelocityChange = 0.9f;
        [Tooltip("Shortest time between abyssal turbulence seam hits so noisy currents read violent without degenerating into audio-camera spam.")]
        [SerializeField, Range(0.02f, 1f)] private float abyssalFlowNoiseBoundaryCooldown = 0.18f;
        [Tooltip("Signed camera-roll impulse fired when abyssal-current turbulence slams the rider across a noisy seam.")]
        [SerializeField, Range(0f, 12f)] private float abyssalFlowNoiseBoundaryRollImpulse = 2.8f;
        [Tooltip("Velocity-change torque injected into active transport control when abyssal turbulence snaps across a noisy seam.")]
        [SerializeField, Range(0f, 8f)] private float abyssalTransportTurbulenceTorqueVelocityChange = 0.7f;
        [Tooltip("Maximum temporary pitch deviation injected into transport thrust by abyssal turbulence seam hits.")]
        [SerializeField, Range(0f, 12f)] private float abyssalTransportTurbulencePitchDegrees = 2.8f;
        [Tooltip("Maximum temporary yaw deviation injected into transport thrust by abyssal turbulence seam hits.")]
        [SerializeField, Range(0f, 16f)] private float abyssalTransportTurbulenceYawDegrees = 5.5f;
        [Tooltip("How quickly abyssal turbulence steering offsets decay back to neutral once the seam hit passes.")]
        [SerializeField, Range(1f, 20f)] private float abyssalTransportTurbulenceRecoverySharpness = 8f;
        [Header("Crush Depth")]
        [Tooltip("Depth where hull stress starts accumulating from abyssal pressure and rapid depth changes.")]
        [SerializeField, Range(500f, 3000f)] private float crushDepthStart = 1000f;
        [Tooltip("Depth where hull stress reaches full authored strength before fatal overload.")]
        [SerializeField, Range(700f, 5000f)] private float crushDepthFullDepth = 1450f;
        [Tooltip("Vertical speed where rapid depth change contributes full extra hull stress.")]
        [SerializeField, Range(0.5f, 30f)] private float crushDepthRateForFullStress = 9f;
        [Tooltip("How quickly hull stress chases the current depth/rate target.")]
        [SerializeField, Range(0.5f, 20f)] private float crushDepthStressBlendSharpness = 3.2f;
        [Tooltip("Additional swim drag applied at full hull stress. This sells pressure as viscous resistance without corrupting rigidbody mass.")]
        [SerializeField, Range(1f, 4f)] private float crushDepthDragMultiplier = 1.55f;
        [Tooltip("How much active transport yaw responsiveness is suppressed at full hull stress.")]
        [SerializeField, Range(0f, 0.9f)] private float crushDepthTurnSuppression = 0.58f;
        [Tooltip("Hull stress threshold where camera micro-vibration starts.")]
        [SerializeField, Range(0f, 1f)] private float crushDepthShakeThreshold = 0.32f;
        [Tooltip("Hull stress threshold where metal-groan one-shots start.")]
        [SerializeField, Range(0f, 1f)] private float crushDepthGroanThreshold = 0.48f;
        [Tooltip("Longest interval between hull groans near the start of dangerous stress.")]
        [SerializeField, Range(0.2f, 10f)] private float crushDepthGroanIntervalMax = 4.2f;
        [Tooltip("Shortest interval between hull groans under extreme stress.")]
        [SerializeField, Range(0.05f, 5f)] private float crushDepthGroanIntervalMin = 1.25f;
        [Tooltip("Hull stress threshold that triggers fatal implosion wipeout.")]
        [SerializeField, Range(0.5f, 1f)] private float crushDepthImplosionThreshold = 0.985f;
        [Tooltip("Optional 2D groan one-shot played while the suit or scooter hull is under extreme compression.")]
        [SerializeField] private AudioClip crushDepthGroanClip;
        [Tooltip("Optional 2D implosion one-shot played when fatal crush depth is crossed.")]
        [SerializeField] private AudioClip crushDepthImplosionClip;
        [Tooltip("Delay between fatal pressure lock-on and the actual implosion wipeout. This is the pre-death glitch window.")]
        [SerializeField, Range(0.25f, 3f)] private float fatalPressureSequenceDuration = 1.5f;
        [Tooltip("Slowest cadence between visor glitch pulses during the fatal pressure loop.")]
        [SerializeField, Range(0.02f, 0.5f)] private float fatalPressureGlitchPulseIntervalMax = 0.28f;
        [Tooltip("Fastest cadence between visor glitch pulses right before implosion.")]
        [SerializeField, Range(0.02f, 0.5f)] private float fatalPressureGlitchPulseIntervalMin = 0.07f;
        [Tooltip("Shortest visor glitch pulse duration during the fatal pressure loop.")]
        [SerializeField, Range(0.02f, 0.5f)] private float fatalPressureGlitchDurationMin = 0.08f;
        [Tooltip("Longest visor glitch pulse duration right before implosion.")]
        [SerializeField, Range(0.02f, 0.75f)] private float fatalPressureGlitchDurationMax = 0.26f;
        [Tooltip("Gameplay FOV floor reached near the end of the fatal pressure squeeze.")]
        [SerializeField, Range(15f, 25f)] private float fatalPressureMinFov = 18f;
        [Tooltip("Mouse-look sensitivity floor reached near the end of the fatal pressure squeeze.")]
        [SerializeField, Range(0f, 0.35f)] private float fatalPressureLookSensitivityFloor = 0.08f;
        [Tooltip("Initial yaw freedom around the locked neck pose when the fatal pressure sequence begins.")]
        [SerializeField, Range(5f, 90f)] private float fatalPressureYawFreedomStart = 42f;
        [Tooltip("Final yaw freedom right before the implosion fires.")]
        [SerializeField, Range(1f, 25f)] private float fatalPressureYawFreedomEnd = 8f;
        [Tooltip("Initial pitch freedom around the locked neck pose when the fatal pressure sequence begins.")]
        [SerializeField, Range(5f, 60f)] private float fatalPressurePitchFreedomStart = 26f;
        [Tooltip("Final pitch freedom right before the implosion fires.")]
        [SerializeField, Range(1f, 20f)] private float fatalPressurePitchFreedomEnd = 5f;
        [Header("Thermal Updrafts")]
        [Tooltip("Minimum sampled upward current speed treated as a black-smoker thermal updraft.")]
        [SerializeField, Range(0f, 10f)] private float thermalUpdraftSpeedThreshold = 1.8f;
        [Tooltip("Upward current speed where thermal updraft reaches full authored shove.")]
        [SerializeField, Range(0.1f, 20f)] private float thermalUpdraftSpeedMax = 6.4f;
        [Tooltip("VelocityChange per second applied by a full-strength thermal updraft. Multiplied by fixed delta time before injection.")]
        [SerializeField, Range(0f, 20f)] private float thermalUpdraftVelocityChangePerSecond = 8.5f;
        [Tooltip("Minimum depth where authored upward currents are allowed to behave like abyssal thermal vents.")]
        [SerializeField, Range(0f, 5000f)] private float thermalUpdraftStartDepth = 120f;
        [Tooltip("Normalized thermal-updraft intensity that is violent enough to force an active-trauma body bend.")]
        [SerializeField, Range(0f, 1f)] private float thermalUpdraftTraumaThreshold = 0.62f;
        [Tooltip("Minimum delay between repeated thermal-updraft trauma hits so continuous vents do not spam the blend state.")]
        [SerializeField, Range(0.05f, 2f)] private float thermalUpdraftTraumaCooldown = 0.45f;
        [Header("Active Sonar")]
        [Tooltip("Cooldown between controller-owned active sonar pings.")]
        [SerializeField, Range(0.1f, 10f)] private float activeSonarPingCooldown = 2.35f;
        [Tooltip("Radius used by the controller-owned active sonar ping.")]
        [SerializeField, Range(25f, 400f)] private float activeSonarPingRadius = 200f;
        [Tooltip("How long shader/VFX consumers should keep the active sonar reveal alive after a ping.")]
        [SerializeField, Range(0.5f, 5f)] private float activeSonarRevealDuration = 2.4f;
        [Header("Vegetation Density Drag")]
        [Tooltip("Optional direct cartographer bridge ref used for per-position vegetation density queries. When absent, the controller falls back to the bridge's player-scoped global density handoff.")]
        [SerializeField] private HectonMapMagicVegetationBridge vegetationDensityBridge;
        [Tooltip("Minimum sargassum density before stem viscosity begins increasing Rigidbody linear damping.")]
        [SerializeField, Range(0f, 1f)] private float vegetationDensityDragThreshold = 0.55f;
        [Tooltip("Extra Rigidbody linear damping applied at peak dense-sargassum density.")]
        [SerializeField, Range(0f, 8f)] private float vegetationDensityLinearDampingMax = 3.2f;
        [Tooltip("How quickly dense-vegetation damping blends toward and away from the sampled density target.")]
        [SerializeField, Range(0.5f, 20f)] private float vegetationDensityLinearDampingBlendSharpness = 5.5f;
        [Header("Bailout State")]
        [Tooltip("Transport impact speed where wipeout escalates into a forced bailout.")]
        [SerializeField, Range(5f, 40f)] private float wipeoutBailoutSpeedThreshold = 25f;
        [Tooltip("Normalized transport integrity threshold below which active transport is treated as critically failed and triggers bailout.")]
        [SerializeField, Range(0f, 1f)] private float wipeoutBailoutCriticalIntegrityThreshold = 0.08f;
        [Tooltip("Additional horizontal bailout impulse fired when the controller ejects the player from an active scooter.")]
        [SerializeField, Range(0f, 20f)] private float wipeoutBailoutImpulse = 7.5f;
        [Tooltip("Additional upward bailout impulse fired when the controller ejects the player from an active scooter.")]
        [SerializeField, Range(0f, 15f)] private float wipeoutBailoutUpwardImpulse = 3.4f;
        [Tooltip("How long bailout disorientation keeps the visor optics smeared after emergency ejection.")]
        [SerializeField, Range(0.1f, 3f)] private float wipeoutBailoutDisorientationDuration = 1.1f;
        [Tooltip("Signed camera-roll impulse fired when the player is violently ejected from a transport.")]
        [SerializeField, Range(0f, 20f)] private float wipeoutBailoutRollImpulse = 8.5f;
        [Tooltip("Visor distortion strength applied during the bailout disorientation window. This sells blur without inventing a second post stack.")]
        [SerializeField, Range(0f, 1f)] private float wipeoutBailoutVisorDistortion = 0.72f;
        [Tooltip("How quickly bailout visor distortion decays back to a clean image.")]
        [SerializeField, Range(0.1f, 12f)] private float wipeoutBailoutVisorRecovery = 4.2f;

        [Header("â”€â”€ Environmental Drag Integration â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("How long an external ApplyEnvironmentalDrag request stays active without refresh before recovering to baseline.")]
        [SerializeField, Range(0f, 0.35f)] private float externalEnvironmentalDragHoldTime = 0.12f;
        [Tooltip("How quickly external environmental drag blends toward the requested multiplier and recovers after release.")]
        [SerializeField, Range(1f, 24f)] private float externalEnvironmentalDragBlendSpeed = 9f;
        [Header("Parasite Latch Physics")]
        [Tooltip("How long parasite COM and harvester pull stay active without refresh before recovering to neutral.")]
        [SerializeField, Range(0f, 0.35f)] private float parasiteLatchInfluenceHoldTime = 0.14f;
        [Tooltip("How quickly parasite COM and harvester pull blend toward the latest async GPU readback sample.")]
        [SerializeField, Range(1f, 24f)] private float parasiteLatchInfluenceBlendSpeed = 8.5f;
        [Tooltip("Base force applied from the parasite center of mass when the swarm is latched to one side of the active hull.")]
        [SerializeField, Range(0f, 80f)] private float parasiteCenterOfMassForce = 18f;
        [Tooltip("Additional force applied toward the nearest DeadZone massive-structure anchor once the hive enters harvester mode.")]
        [SerializeField, Range(0f, 120f)] private float parasiteHarvesterPullForce = 42f;
        [Tooltip("Latched parasite count treated as full parasite force on the active transport hull.")]
        [SerializeField, Range(1f, 64f)] private float parasiteLatchCountForFullForce = 18f;
        [Header("Sargassum Entanglement")]
        [Tooltip("Spring force applied when the player or scooter gets snared inside dense sargassum and loses momentum.")]
        [SerializeField, Range(0f, 40f)] private float sargassumEntanglementSpring = 11.5f;
        [Tooltip("Velocity damping applied along the snare spring so the player feels stem tension instead of a raw teleport pull.")]
        [SerializeField, Range(0f, 20f)] private float sargassumEntanglementDamping = 4.8f;
        [Tooltip("How much the snare spring is allowed to pull vertically. Lower values keep the trap mostly planar and avoid ugly bobbing.")]
        [SerializeField, Range(0f, 1f)] private float sargassumEntanglementVerticalInfluence = 0.18f;
        [Tooltip("Body-mass reference used to keep sargassum snaring stable across light and heavy suit profiles.")]
        [SerializeField, Range(40f, 500f)] private float sargassumEntanglementMassReference = 80f;
        [Tooltip("Maximum extra environmental drag requested by full sargassum entanglement while swimming without transport.")]
        [SerializeField, Range(0f, 3f)] private float sargassumEntanglementSwimEnvironmentalDrag = 0.45f;
        [Tooltip("Maximum extra environmental drag requested by full sargassum entanglement while a transport propeller is active.")]
        [SerializeField, Range(0f, 4f)] private float sargassumEntanglementTransportEnvironmentalDrag = 1.15f;
        [Tooltip("How much active transport propulsion can shave off spring tension so the player can still build escape momentum.")]
        [SerializeField, Range(0f, 1f)] private float sargassumEntanglementEscapeRelief = 0.48f;
        [Tooltip("Base suit-energy drain per second applied while the player is actively fighting dense sargassum entanglement.")]
        [SerializeField, Range(0f, 10f)] private float sargassumEscapeEnergyDrainPerSecond = 0.85f;
        [Tooltip("Multiplier applied to escape-drain while entanglement is active. Default 3x per design request.")]
        [SerializeField, Range(1f, 6f)] private float sargassumEntanglementEscapeEnergyMultiplier = 3f;
        [Tooltip("Minimum combined movement or propulsion intent required before escape-drain is applied.")]
        [SerializeField, Range(0f, 1f)] private float sargassumEscapeInputThreshold = 0.2f;
        [Tooltip("Optional 3D one-shot played when the player actively strains against a sargassum snare. Falls back to the underwater impact clip when null.")]
        [SerializeField] private AudioClip sargassumEntanglementStrainClip;
        [Tooltip("Cooldown between strain one-shots so continuous struggle reads as stem tension instead of audio spam.")]
        [SerializeField, Range(0.05f, 1f)] private float sargassumEntanglementAudioCooldown = 0.24f;
        [Tooltip("Minimum normalized escape intent required before entanglement strain emits camera/audio feedback.")]
        [SerializeField, Range(0f, 1f)] private float sargassumEntanglementStrainThreshold = 0.22f;
        [Tooltip("Scales the normalized entanglement strain before it is forwarded into the camera-shake path.")]
        [SerializeField, Range(0f, 2f)] private float sargassumEntanglementCameraShakeScale = 0.9f;
        [Tooltip("Strain level where sargassum escape escalates into hard stress. At and above this point the controller amplifies shake and resource drain.")]
        [SerializeField, Range(0f, 1f)] private float sargassumHighStrainThreshold = 0.5f;
        [Tooltip("Extra shake multiplier applied while entanglement strain stays above the hard-stress threshold.")]
        [SerializeField, Range(1f, 4f)] private float sargassumHighStrainShakeBoost = 1.75f;
        [Tooltip("Extra energy-drain multiplier applied while entanglement strain stays above the hard-stress threshold.")]
        [SerializeField, Range(1f, 6f)] private float sargassumHighStrainEnergyMultiplier = 3f;
        [Tooltip("How long high-strain stress persists without a fresh strain pulse before relaxing back to baseline.")]
        [SerializeField, Range(0f, 0.5f)] private float sargassumHighStrainHoldTime = 0.18f;
        [Header("Abyssal Cable Entanglement")]
        [Tooltip("Spring force applied when the player or scooter gets snared inside abyssal bio-cables.")]
        [SerializeField, Range(0f, 80f)] private float abyssalCableEntanglementSpring = 28f;
        [Tooltip("Velocity damping applied along the bio-cable snare direction.")]
        [SerializeField, Range(0f, 30f)] private float abyssalCableEntanglementDamping = 9.5f;
        [Tooltip("How much the cable snare is allowed to pull vertically.")]
        [SerializeField, Range(0f, 1f)] private float abyssalCableEntanglementVerticalInfluence = 0.12f;
        [Tooltip("Maximum extra environmental drag requested by full abyssal cable tension while swimming without transport.")]
        [SerializeField, Range(0f, 5f)] private float abyssalCableEntanglementSwimEnvironmentalDrag = 1.25f;
        [Tooltip("Maximum extra environmental drag requested by full abyssal cable tension while active transport is engaged.")]
        [SerializeField, Range(0f, 8f)] private float abyssalCableEntanglementTransportEnvironmentalDrag = 2.85f;
        [Tooltip("Base suit-energy drain per second applied while the player fights an active abyssal cable snare.")]
        [SerializeField, Range(0f, 20f)] private float abyssalCableEscapeEnergyDrainPerSecond = 6.2f;
        [Tooltip("Multiplier applied to cable escape drain while the snare remains mostly uncut.")]
        [SerializeField, Range(1f, 8f)] private float abyssalCableEscapeEnergyMultiplier = 4.5f;
        [Tooltip("Minimum cable cut progress required before propulsion starts buying real relief against the snare.")]
        [SerializeField, Range(0f, 1f)] private float abyssalCableCutReleaseThreshold = 0.68f;
        [Tooltip("Maximum propulsion relief unlocked once the cable knot is substantially severed.")]
        [SerializeField, Range(0f, 1f)] private float abyssalCablePropulsionReliefAtFullCut = 0.72f;
        [Header("Sargassum Buoyancy Support")]
        [Tooltip("Global sargassum density threshold where a floating mat starts to support the player's body weight.")]
        [SerializeField, Range(0f, 1f)] private float sargassumMatBuoyancyDensityThreshold = 0.8f;
        [Tooltip("Depth below the water surface where dense floating-mat support has fully faded out.")]
        [SerializeField, Range(0.1f, 3f)] private float sargassumMatBuoyancyMaxDepth = 1.65f;
        [Tooltip("How quickly dense floating-mat support blends in and out.")]
        [SerializeField, Range(1f, 24f)] private float sargassumMatBuoyancyBlendSharpness = 9f;
        [Tooltip("Extra upward support applied in multiples of body weight while the player lies on a dense sargassum mat.")]
        [SerializeField, Range(0f, 2.5f)] private float sargassumMatBuoyancyForceScale = 0.85f;
        [Tooltip("Extra surface-lock authority granted by a dense sargassum mat.")]
        [SerializeField, Range(1f, 3f)] private float sargassumMatSurfaceLockBoost = 1.4f;
        [Tooltip("Additional lift applied to the surface-lock target while a dense mat is carrying the player.")]
        [SerializeField, Range(0f, 0.75f)] private float sargassumMatSurfaceLiftOffset = 0.16f;

        [Header("Surface Recovery Feedback")]
        [Tooltip("Optional 2D helmet-breath one-shot played when the player breaks back into open air after staying underwater for a while.")]
        [SerializeField] private AudioClip surfaceGaspClip;
        [Tooltip("Minimum continuous head-submerged time required before surfacing can trigger the greedy gasp one-shot.")]
        [SerializeField, Range(0f, 12f)] private float surfaceGaspMinUnderwaterTime = 2.4f;
        [Tooltip("Cooldown between gasp triggers so wave chop cannot spam breath recovery feedback.")]
        [SerializeField, Range(0f, 5f)] private float surfaceGaspCooldown = 1.2f;
        [Tooltip("Head depth below Crest required before the controller treats the player as fully submerged for gasp timing.")]
        [SerializeField, Range(0f, 0.35f)] private float surfaceGaspHeadEnterDepth = 0.04f;
        [Tooltip("Head depth below Crest where the gasp submerge latch finally releases back to open air.")]
        [SerializeField, Range(0f, 0.2f)] private float surfaceGaspHeadExitDepth = 0.01f;
        [Tooltip("Helmet-breath playback volume for the greedy gasp recovery one-shot.")]
        [SerializeField, Range(0f, 1f)] private float surfaceGaspVolume = 0.82f;
        [Tooltip("Short FOV expansion applied with the gasp so surfacing after pressure feels physical instead of cosmetic.")]
        [SerializeField, Range(0f, 12f)] private float surfaceGaspFovExpand = 2.6f;
        [Tooltip("Follow-up FOV compression applied after the gasp expansion settles.")]
        [SerializeField, Range(0f, 8f)] private float surfaceGaspFovCompress = 0.85f;
        [Tooltip("Duration of the gasp FOV kick.")]
        [SerializeField, Range(0.05f, 1f)] private float surfaceGaspFovDuration = 0.34f;
        [Tooltip("Optional splash-ring particle system restarted when a recent dolphin breach slams back into the water.")]
        [SerializeField] private ParticleSystem breachSplashRingParticles;
        [Tooltip("Minimum normalized re-entry intensity before the breach splash ring is allowed to fire.")]
        [SerializeField, Range(0f, 1f)] private float breachSplashRingMinIntensity = 0.18f;

        [Header("Sargassum Bed Recovery")]
        [Tooltip("Field density threshold where a sargassum mat becomes dense enough to function as a floating resting bed.")]
        [SerializeField, Range(0f, 1f)] private float sargassumRestDensityThreshold = 0.9f;
        [Tooltip("Maximum player speed allowed before dense-mat resting recovery is suppressed.")]
        [SerializeField, Range(0.05f, 3f)] private float sargassumRestMaxSpeed = 0.4f;
        [Tooltip("Maximum input intent allowed before dense-mat resting recovery is suppressed.")]
        [SerializeField, Range(0f, 1f)] private float sargassumRestMaxInputIntent = 0.18f;
        [Tooltip("Head depth below Crest where resting recovery is fully suppressed because the player is no longer breathing above the floating mat.")]
        [SerializeField, Range(0f, 0.5f)] private float sargassumRestMaxHeadDepth = 0.03f;
        [Tooltip("How quickly dense-mat resting recovery blends in and out.")]
        [SerializeField, Range(1f, 24f)] private float sargassumRestBlendSharpness = 6f;
        [Tooltip("Additional oxygen refill per second granted while the player lies still on a dense sargassum mat at the surface.")]
        [SerializeField, Range(0f, 25f)] private float sargassumRestOxygenRestorePerSecond = 8f;
        [Tooltip("Additional energy refill per second granted while the player lies still on a dense sargassum mat at the surface.")]
        [SerializeField, Range(0f, 10f)] private float sargassumRestEnergyRestorePerSecond = 1.35f;

        [Header("Impact Feedback")]
        [Tooltip("Optional particle burst emitted when a hard underwater wipeout happens.")]
        [SerializeField] private ParticleSystem wipeoutBubbleParticles;
        [Tooltip("Optional particle burst emitted when a violent dolphin breach tears through the surface.")]
        [SerializeField] private ParticleSystem breachBubbleParticles;
        [Tooltip("Optional muffled underwater impact clip used by wipeouts and violent breaches.")]
        [SerializeField] private AudioClip underwaterImpactClip;
        [Tooltip("Minimum normalized intensity before a bubble impact burst is allowed to emit.")]
        [SerializeField, Range(0f, 1f)] private float impactBubbleMinIntensity = 0.18f;
        [Tooltip("Minimum number of particles emitted by an impact bubble burst.")]
        [SerializeField, Range(0, 64)] private int impactBubbleMinCount = 10;
        [Tooltip("Maximum number of particles emitted by an impact bubble burst.")]
        [SerializeField, Range(0, 128)] private int impactBubbleMaxCount = 32;
        [Tooltip("Minimum volume used by the muffled underwater impact one-shot.")]
        [SerializeField, Range(0f, 1f)] private float underwaterImpactMinVolume = 0.42f;
        [Tooltip("Maximum volume used by the muffled underwater impact one-shot.")]
        [SerializeField, Range(0f, 1f)] private float underwaterImpactMaxVolume = 0.88f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” GRADUATED GRAVITY
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Graduated Gravity â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField, Range(1f, 3f)]
        private float gravityFadeRate = 1.4f;

        [SerializeField, Range(1f, 5f)]
        private float snapFadeRate = 2.5f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” MOUSE LOOK
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Mouse Look â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float pitchMin = -85f;
        [SerializeField] private float pitchMax = 85f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” SWIM VERTICAL DEFAULTS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Control Scheme â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        
        [Header("â”€â”€ Input System â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]

        [Header("â”€â”€ Swim Vertical (fallback ÐµÑÐ»Ð¸ Ð½ÐµÑ‚ ControlScheme) â”€â”€")]





        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” GROUND DETECTION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Ground Detection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private float groundCheckRadius = 0.3f;
        [SerializeField] private float groundCheckDistance = 0.4f;
        [SerializeField, Range(5f, 89f)] private float maxGroundAngle = 60f;
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Range(1f, 2f)] private float slopeStabilityFactor = 1.1f;
        [SerializeField, Range(0f, 20f)] private float groundSnapForce = 8f;
        [SerializeField, Range(0f, 0.3f)] private float jumpBufferTime = 0.12f;
        [SerializeField, Range(0f, 0.3f)] private float dryGroundGraceTime = 0.12f;
        [SerializeField, Range(0f, 0.3f)] private float shoreGroundGraceTime = 0.14f;
        [SerializeField, Range(0f, 0.6f)] private float stepAssistHeight = 0.3f;
        [SerializeField, Range(0.05f, 0.8f)] private float stepAssistForwardDistance = 0.28f;
        [SerializeField, Range(0f, 0.2f)] private float stepAssistClearance = 0.04f;
        [SerializeField, Range(0f, 0.2f)] private float stepAssistCooldownTime = 0.06f;
        [SerializeField, Range(0f, 0.6f)] private float jumpHeadClearanceDistance = 0.18f;
        [SerializeField, Range(0.02f, 0.3f)] private float surfaceBreachDepthWindow = 0.12f;
        [SerializeField, Range(0.3f, 0.95f)] private float surfaceBreachMinImmersion = 0.45f;
        [SerializeField, Range(0.05f, 1f)] private float dryAirControlMultiplier = 0.4f;
        [SerializeField, Range(0f, 1f)] private float dryAirDampingMultiplier = 0.18f;
        [Tooltip("Dry-interior grounded walk force multiplier for heavy NASA-punk suit movement inside airlocks and wreck corridors.")]
        [SerializeField, Range(0.4f, 2f)] private float dryInteriorWalkForceMultiplier = 0.82f;
        [Tooltip("Dry-interior grounded walk speed multiplier for heavy NASA-punk suit movement inside airlocks and wreck corridors.")]
        [SerializeField, Range(0.3f, 1.5f)] private float dryInteriorWalkSpeedMultiplier = 0.76f;
        [Tooltip("How far to either side each dry-interior foot probe samples metal-floor support.")]
        [SerializeField, Range(0.05f, 0.6f)] private float dryInteriorFootProbeLateralOffset = 0.16f;
        [Tooltip("Forward offset applied to each dry-interior foot probe from body center.")]
        [SerializeField, Range(-0.25f, 0.5f)] private float dryInteriorFootProbeForwardOffset = 0.05f;
        [Tooltip("Height above the capsule bottom used when casting dry-interior foot-support rays.")]
        [SerializeField, Range(0.05f, 0.8f)] private float dryInteriorFootProbeHeight = 0.24f;
        [Tooltip("Maximum distance used by each dry-interior foot-support raycast.")]
        [SerializeField, Range(0.1f, 1.6f)] private float dryInteriorFootProbeDistance = 0.7f;

        [Header("Exosuit Locomotion")]
        [Tooltip("Extra grounded walk force multiplier while an exosuit transport owns locomotion.")]
        [SerializeField, Range(0.5f, 4f)] private float exosuitWalkForceMultiplier = 1.4f;
        [Tooltip("Ground-speed multiplier while an exosuit transport owns locomotion.")]
        [SerializeField, Range(0.5f, 3f)] private float exosuitWalkSpeedMultiplier = 0.82f;
        [Tooltip("Upward force applied by exosuit jump jets while the pilot commands vertical launch.")]
        [SerializeField, Range(0f, 120f)] private float exosuitJumpJetForce = 42f;
        [Tooltip("Immediate upward kick applied when jump jets ignite from the seabed.")]
        [SerializeField, Range(0f, 12f)] private float exosuitJumpJetLaunchImpulse = 3.2f;
        [Tooltip("Suit energy drained per second while exosuit jump jets are firing.")]
        [SerializeField, Range(0f, 40f)] private float exosuitJumpJetEnergyDrainPerSecond = 8.5f;
        [Tooltip("Multiplier applied against mounted transport energy drain when exosuit jump jets ignite.")]
        [SerializeField, Range(1f, 10f)] private float exosuitJumpJetScooterDrainMultiplier = 5f;
        [Tooltip("Normalized heat accumulated per second while exosuit jump jets are firing.")]
        [SerializeField, Range(0f, 4f)] private float exosuitJumpJetHeatPerSecond = 0.85f;
        [Tooltip("Normalized heat removed per second while exosuit jump jets are idle.")]
        [SerializeField, Range(0f, 4f)] private float exosuitJumpJetCoolRate = 0.55f;
        [Tooltip("Heat level below which overheated jump jets recover and can fire again.")]
        [SerializeField, Range(0f, 1f)] private float exosuitJumpJetRecoverThreshold = 0.32f;
        [Tooltip("Extra gravity scale applied while an exosuit transport owns locomotion underwater.")]
        [SerializeField, Range(1f, 3f)] private float exosuitNegativeBuoyancyScale = 1.2f;
        [Tooltip("How far to either side each exosuit foot probe samples slope support.")]
        [SerializeField, Range(0.1f, 1.5f)] private float exosuitFootProbeLateralOffset = 0.34f;
        [Tooltip("Forward offset applied to each exosuit foot probe from body center.")]
        [SerializeField, Range(-0.5f, 1f)] private float exosuitFootProbeForwardOffset = 0.12f;
        [Tooltip("Height above the capsule bottom used when casting exosuit foot-support rays.")]
        [SerializeField, Range(0.05f, 1.5f)] private float exosuitFootProbeHeight = 0.55f;
        [Tooltip("Maximum distance used by each exosuit foot-support raycast.")]
        [SerializeField, Range(0.1f, 3f)] private float exosuitFootProbeDistance = 1.35f;
        [Tooltip("Minimum normal Y still accepted as footing while the exosuit grips steep cave slopes.")]
        [SerializeField, Range(0.05f, 0.8f)] private float exosuitMinGroundNormalY = 0.18f;
        [Tooltip("How quickly dual-foot slope probes overwrite the default ground normal while the exosuit is planted.")]
        [SerializeField, Range(1f, 40f)] private float exosuitFootSlopeBlendSharpness = 22f;
        [Tooltip("Additional slope-hold force multiplier applied while the exosuit is grounded.")]
        [SerializeField, Range(1f, 4f)] private float exosuitSlopeStickForceMultiplier = 2.25f;
        [Tooltip("Additional snap-force multiplier applied while the exosuit is grounded to keep it glued to cave slopes.")]
        [SerializeField, Range(1f, 4f)] private float exosuitGroundSnapForceMultiplier = 1.85f;
        [Tooltip("Collision speed threshold where exosuit rock landings trigger the heavy impact response.")]
        [SerializeField, Range(0f, 30f)] private float exosuitImpactShakeSpeedThreshold = 7.5f;
        [Tooltip("Collision shake scale applied on heavy exosuit rock impacts.")]
        [SerializeField, Range(1f, 4f)] private float exosuitImpactShakeScale = 2.1f;
        [Tooltip("One-shot disturbed-silt injection fired by heavy exosuit rock impacts.")]
        [SerializeField, Range(0f, 2f)] private float exosuitImpactSiltBurstScale = 0.95f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” FOV
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ FOV â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Base FOV of the camera. FOV compression applies relative to this.")]
        [SerializeField] private float baseFov = 70f;
        [Tooltip("How much underwater body-yaw responsiveness remains while dragging the heaviest heavy-carry object.")]
        [SerializeField, Range(0.1f, 1f)] private float maxHeavyCarryBodyYawSpringMultiplier = 0.58f;
        [Header("Heavy Tow Response")]
        [Tooltip("How much the camera pitches upward while a heavy tow line is loading the player from behind.")]
        [SerializeField, Range(0f, 20f)] private float heavyTowCameraPitchDegrees = 5.5f;
        [Tooltip("How much the camera rolls toward a laterally drifting tow payload.")]
        [SerializeField, Range(0f, 20f)] private float heavyTowCameraRollDegrees = 8.5f;
        [Tooltip("How far the camera shifts backward at peak tow load.")]
        [SerializeField, Range(0f, 0.4f)] private float heavyTowCameraBackwardOffset = 0.09f;
        [Tooltip("How far the camera shifts sideways toward the payload at peak tow load.")]
        [SerializeField, Range(0f, 0.25f)] private float heavyTowCameraSideOffset = 0.055f;
        [Tooltip("How quickly heavy-tow COM and camera response converge.")]
        [SerializeField, Range(1f, 24f)] private float heavyTowResponseBlendSharpness = 7f;
        [Tooltip("Rearward center-of-mass shift applied while the tow line is loaded.")]
        [SerializeField, Range(0f, 0.6f)] private float heavyTowCenterOfMassRearShift = 0.22f;
        [Tooltip("Lateral center-of-mass shift applied toward the towed mass.")]
        [SerializeField, Range(0f, 0.35f)] private float heavyTowCenterOfMassLateralShift = 0.14f;
        [Tooltip("Downward center-of-mass sink applied while the tow line is loaded.")]
        [SerializeField, Range(0f, 0.25f)] private float heavyTowCenterOfMassDownShift = 0.05f;
        [Header("Cutting Tension Physics")]
        [Tooltip("Maximum slack allowed between the player and the current cut anchor before virtual spring tension starts loading.")]
        [SerializeField, Range(0.2f, 3f)] private float cuttingTensionRestLength = 1.1f;
        [Tooltip("Hooke spring strength applied while the cutter anchor is loaded.")]
        [SerializeField, Range(0f, 120f)] private float cuttingTensionSpring = 24f;
        [Tooltip("Velocity damping applied along the cutter spring axis so the player does not oscillate forever while pulling metal free.")]
        [SerializeField, Range(0f, 40f)] private float cuttingTensionDamping = 8f;
        [Tooltip("Maximum force the virtual cutter spring is allowed to inject into the player body.")]
        [SerializeField, Range(0f, 120f)] private float cuttingTensionMaxForce = 34f;
        [Tooltip("How long a cutter anchor request is kept alive without a fresh tool update before the spring fully releases.")]
        [SerializeField, Range(0f, 0.25f)] private float cuttingTensionHoldTime = 0.08f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” DIAGNOSTICS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Diagnostics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private bool _debugIsWalking;
        [SerializeField] private string _debugLocomotionMode;
        [SerializeField] private bool _debugIsGrounded;
        [SerializeField] private float _debugImmersionRatio;
        [SerializeField] private float _debugSmoothedImmersion;
        [SerializeField] private float _debugGravityScale;
        [SerializeField] private float _debugSnapScale;
        [SerializeField] private float _debugBodyYaw;
        [SerializeField] private float _debugCameraYaw;
        [SerializeField] private float _debugCurrentRoll;
#pragma warning disable CS0414
        [SerializeField] private bool _debugStepEvent;
#pragma warning restore CS0414
        [SerializeField] private string _debugSuitName;
        [SerializeField] private float _debugSpeed;
        [SerializeField] private float _debugDynamicWaterY;
        [SerializeField] private bool _debugCrestAvailable;
        [SerializeField] private bool _debugCrestSampling;
        [SerializeField] private float _debugDepth;
        [SerializeField] private float _debugFovOffset;
        [SerializeField] private bool _debugSplashThisFrame;
        [SerializeField] private bool _debugExhaleThisFrame;
        [SerializeField] private bool _debugIsSubmerged;
        [SerializeField] private bool _debugHasSwimPresentationController;
        [SerializeField] private int _debugLastSwimPresentationDriveFrame = -1;
#pragma warning disable CS0414
        [SerializeField] private bool _debugHeavyCarryActive;
        [SerializeField] private float _debugHeavyCarryForceMultiplier = 1f;
        [SerializeField] private float _debugHeavyCarrySpeedMultiplier = 1f;
        [SerializeField] private float _debugSargassumSpeedMultiplier = 1f;
        [SerializeField] private float _debugSargassumDragMultiplier = 1f;
        [SerializeField] private bool _debugSargassumEntangled;
        [SerializeField] private float _debugSargassumEntanglement01;
        [SerializeField] private float _debugSargassumEntanglementDragRequest = 1f;
        [SerializeField] private float _debugSargassumFieldDensity01;
        [SerializeField] private float _debugSargassumMatBuoyancy01;
        [SerializeField] private float _debugExternalEnvironmentalDragMultiplier = 1f;
        [SerializeField] private float _debugExternalEnvironmentalSpeedMultiplier = 1f;
        [SerializeField] private float _debugExternalEnvironmentalThrustMultiplier = 1f;
        [SerializeField] private int _debugParasiteLatchedCount;
        [SerializeField] private Vector3 _debugParasiteCenterOfMassLS;
        [SerializeField] private Vector3 _debugParasiteHarvesterPullWS;
        [SerializeField] private float _debugSurfaceWavePitch;
        [SerializeField] private float _debugSurfaceWaveRoll;
        [SerializeField] private float _debugStormIntensity01;
        [SerializeField] private float _debugWaveHeightSpan;
        [SerializeField] private float _debugTransportCavitationEfficiency = 1f;
        [SerializeField] private float _debugShoreBuoyancyBlend = 1f;
        [SerializeField] private float _debugBottomClearance = -1f;
        [SerializeField] private float _debugWetLensIntensity;
        [SerializeField] private float _debugWaveSlopeForward;
        [SerializeField] private float _debugWaveSlopeLateral;
        [SerializeField] private float _debugUndertowIntensity;
        [SerializeField] private float _debugWipeoutTimer;
        [SerializeField] private float _debugDynamicCollisionTuck;
        [SerializeField] private float _debugAbyssalCurrentIntensity;
        [SerializeField] private bool _debugHeavyTowActive;
        [SerializeField] private float _debugHeavyTowTension01;
        [SerializeField] private float _debugHeavyTowStress01;
        [SerializeField] private float _debugHeavyTowDragMultiplier = 1f;
        [SerializeField] private float _debugHeavyTowSignedLateralPull;
        [SerializeField] private float _debugHeavyTowBackwardPull;
#pragma warning restore CS0414

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  CACHED REFERENCES
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private Rigidbody _rb;
        private BuoyancyObject _buoyancy;
        private CapsuleCollider _capsuleCollider;
        private Transform _cachedTransform;
        private Camera _cameraComponent;
        private InputManager _inputManager;
        private InputManager _subscribedInputManager;
        private PlayerToolManager _playerToolManager;
        private PlayerTransportCoordinator _playerTransportCoordinator;
        private PlayerSwimPresentationController _swimPresentationController;
        private PhysicalInteractionHandler _physicalInteractionHandler;
        private HeavyTowWinch _heavyTowWinch;
        private SargassumMovementInfluence _sargassumMovementInfluence;
        private HectonSurvivalSystem _survivalSystem;
        private HectonUnderwaterVisuals _underwaterVisuals;
        private bool _resolvedPhysicalInteractionHandler;
        private bool _resolvedHeavyTowWinch;
        private bool _resolvedUnderwaterVisuals;
        private int _instanceId;
        private float _nextSargassumEntanglementAudioTime = float.NegativeInfinity;
        private float _basePlayerHeight;
        private float _baseCapsuleHeight;
        private float _baseCapsuleRadius;
        private Vector3 _baseCenterOfMass;
        private Vector3 _baseCapsuleCenter;
        private float _appliedCollisionHeightScale = 1f;
        private float _appliedCollisionRadiusScale = 1f;
        private float _appliedCollisionCenterYOffset;
        private float _requestedTransportCollisionHeightScale = 1f;
        private float _requestedTransportCollisionRadiusScale = 1f;
        private float _requestedTransportCollisionCenterYOffset;
        private float _dynamicCollisionTuck01;
        private float _physicalTraumaCollisionWeight;
        private float _physicalTraumaCollisionHoldTimer;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  CREST OCEAN â€” runtime state
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private IHectonOceanKinematics _oceanKinematics;
        private bool _crestAvailable;
        private float _dynamicWaterSurfaceY;
        private Vector3 _dynamicWaterSurfaceNormal = Vector3.up;
        private Vector3 _dynamicWaterSurfaceVelocity;
        private Vector3 _dynamicWaterFlowVelocity;
        private Vector3 _dynamicWaterDisplacement;
        private Vector3 _dynamicAverageWaterVelocity;
        private Vector3 _dynamicAverageWaterDisplacement;
        private float _fallbackWaterSurfaceY;
        private float _dynamicWaveHeightSpan;
        private float _dynamicStormIntensity;
        private bool _crestSamplingSucceeded;
        private bool _crestFlowSamplingSucceeded;
        private float _nextOceanKinematicsResolveTime = float.NegativeInfinity;
        private readonly Vector3[] _crestQueryPoints = new Vector3[CrestBodySampleCount]; // COLD ALLOC: Vector3[5] â€” batched Crest body-query points (center/head/feet/left/right) â€” owner: HectonPlayerMovement
        private readonly float[] _crestQueryHeights = new float[CrestBodySampleCount]; // COLD ALLOC: float[5] â€” batched Crest sampled water heights â€” owner: HectonPlayerMovement
        private readonly Vector3[] _crestQueryNormals = new Vector3[CrestBodySampleCount]; // COLD ALLOC: Vector3[5] â€” batched Crest sampled normals â€” owner: HectonPlayerMovement
        private readonly Vector3[] _crestQueryVelocities = new Vector3[CrestBodySampleCount]; // COLD ALLOC: Vector3[5] â€” batched Crest sampled water velocities â€” owner: HectonPlayerMovement
        private readonly Vector3[] _crestQueryDisplacements = new Vector3[CrestBodySampleCount]; // COLD ALLOC: Vector3[5] â€” batched Crest sampled displacements â€” owner: HectonPlayerMovement
        private readonly Vector3[] _crestQueryFlows = new Vector3[CrestBodySampleCount]; // COLD ALLOC: Vector3[5] â€” batched Crest flow samples â€” owner: HectonPlayerMovement
        private readonly System.Collections.Generic.List<GameObject> _sceneRootBuffer =
            new System.Collections.Generic.List<GameObject>(16); // COLD ALLOC: List<GameObject>(16) â€” reusable root scan buffer for delayed ocean-kinematics owner recovery â€” owner: HectonPlayerMovement
        private readonly System.Collections.Generic.List<MonoBehaviour> _sceneOceanProviderBuffer =
            new System.Collections.Generic.List<MonoBehaviour>(32); // COLD ALLOC: List<MonoBehaviour>(32) â€” reusable scene component scan buffer for ocean-kinematics provider recovery â€” owner: HectonPlayerMovement

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  CAMERA JUICE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        // COLD ALLOC: List<VisorHUDController>(4) — reused fatal-pressure visor glitch target list — owner: HectonPlayerMovement
        private static readonly List<VisorHUDController> s_fatalPressureGlitchControllers = new List<VisorHUDController>(4);
        private CameraJuiceProcessor _juiceProcessor;
        private CameraJuiceInput _juiceInput;
        private CameraJuiceOutput _juiceOutput;
        private Vector3 _cameraBaseLocalPos;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INPUT STATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private float _inputH;
        private float _inputV;
        private float _inputVertical;
        private float _mouseXDelta;

        private float _cameraYaw;
        private float _cameraPitch;

        private bool _inputCleared;
        private bool _jumpRequested;
        private bool _isSprinting;
        private float _jumpBufferTimer;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  BODY YAW (decoupled from camera)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private float _bodyYaw;
        private float _bodyYawVelocity;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  MODE STATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private bool _isWalking;
        private bool _isGrounded;
        private bool _wasGroundedLastFrame;
        private float _dryGroundGraceTimer;
        private float _shoreGroundGraceTimer;
        private float _stepAssistCooldownTimer;
        private float _currentFixedDeltaTime = 0.02f;
        private float _waterImmersionRatio;
        private float _smoothedImmersionRatio;
        private float _currentLinearDamping;
        private float _gravityScale;
        private float _snapScale;
        private float _currentDepth;  // v7.0: meters below water surface
        private bool _isSurfaceSwimming;
        private PlayerLocomotionMode _currentLocomotionMode = PlayerLocomotionMode.DryGroundWalk;
        private float _surfaceBreachLockTimer;
        private float _surfaceDiveCommitTimer;
        private float _surfaceDiveAssistTimer;
        private float _surfaceLockBlend;
        private float _surfaceLockTargetY;
        private float _waterEntryImpactTimer;
        private float _waterEntryImpactStrength;
        private float _shoreBuoyancyBlend = 1f;
        private float _bottomClearance = float.PositiveInfinity;
        private Vector3 _bottomNormal = Vector3.up;
        private float _exosuitJumpJetHeat01;
        private bool _exosuitJumpJetsOverheated;
        private Vector3 _exosuitFootingNormal = Vector3.up;
        private bool _exosuitFootingValid;
        private float _wetLensSignalIntensity;
        private float _wetLensPulseCooldownTimer;
        private float _underwaterStressSignalIntensity;
        private float _crestFlowInputAttenuation = 1f;
        private float _externalEnvironmentalDragRequestedMultiplier = 1f;
        private float _externalEnvironmentalDragCurrentMultiplier = 1f;
        private float _externalEnvironmentalDragHoldTimer;
        private bool _externalEnvironmentalDragRequestedThisStep;
        private Vector3 _cuttingTensionAnchorRequestedWS = Vector3.zero;
        private Vector3 _cuttingTensionAnchorCurrentWS = Vector3.zero;
        private Vector3 _cuttingTensionAnchorNormalRequestedWS = Vector3.up;
        private Vector3 _cuttingTensionAnchorNormalCurrentWS = Vector3.up;
        private float _cuttingTensionHoldTimer;
        private float _cuttingTensionCurrentForce;
        private bool _cuttingTensionRequestedThisStep;
        private float _vegetationDensityLinearDamping;
        private float _sargassumFieldDensity01;
        private float _sargassumMatBuoyancyBlend;
        private float _sargassumHighStrainIntensity;
        private float _sargassumHighStrainTimer;
        private AbyssalThermalManager.ThermalFlowSample _abyssalThermalFlowSample;
        private Vector3 _abyssalThermalFlowVelocityWS = Vector3.zero;
        private Quaternion _surfaceWavePoseRotation = Quaternion.identity;
        private Quaternion _underwaterTurbulencePoseRotation = Quaternion.identity;
        private float _transportCavitationEfficiency = 1f;
        private float _heavyTowCameraPitchOffset;
        private float _heavyTowCameraRollOffset;
        private Vector3 _heavyTowCameraLocalOffset;
        private Vector3 _heavyTowCenterOfMassOffset;
        private float _previousTransportForwardVelocity;
        private Vector2 _dynamicWaveLocalSlope = Vector2.zero;
        private Vector3 _dynamicWaveLongitudinalGradient = Vector3.zero;
        private Vector3 _dynamicWaveLateralGradient = Vector3.zero;
        private Vector3 _undertowVector = Vector3.zero;
        private float _undertowIntensity;
        private float _wipeoutTimer;
        private float _wipeoutSeverity;
        private float _transportBailoutCooldownTimer;
        private float _recentBreachExitTimer;
        private float _abyssalDowndraftCooldownTimer;
        private float _abyssalDowndraftActiveTimer;
        private float _abyssalDowndraftIntensity;
        private Vector3 _abyssalDowndraftVelocityChange = Vector3.zero;
        private float _abyssalFlowNoiseBoundaryCooldownTimer;
        private Vector3 _previousAbyssalNoisyFlow = Vector3.zero;
        private float _abyssalTransportTurbulencePitchOffset;
        private float _abyssalTransportTurbulenceYawOffset;
        private float _hullStressIntensity;
        private float _hullStressGroanCooldownTimer;
        private float _hullStressHudCorruptionRefreshTimer;
        private float _externalHullStressRequestedIntensity;
        private bool _externalHullStressRequestedThisStep;
        private float _fatalPressureSequenceTimer;
        private float _fatalPressureSequenceGlitchPulseTimer;
        private float _fatalPressureSequenceIntensity;
        private float _fatalPressureLookYawAnchor;
        private float _fatalPressureLookPitchAnchor;
        private float _activeSonarPingCooldownTimer;
        private float _thermalUpdraftIntensity;
        private Vector3 _thermalUpdraftVelocityChange = Vector3.zero;
        private Vector3 _externalThermalUpdraftVelocityChange = Vector3.zero;
        private bool _externalThermalUpdraftRequestedThisStep;
        private int _parasiteLatchedRequestedCount;
        private int _parasiteLatchedCurrentCount;
        private Vector3 _parasiteCenterOfMassRequestedLS = Vector3.zero;
        private Vector3 _parasiteCenterOfMassCurrentLS = Vector3.zero;
        private Vector3 _parasiteHarvesterPullRequestedWS = Vector3.zero;
        private Vector3 _parasiteHarvesterPullCurrentWS = Vector3.zero;
        private bool _parasiteLatchRequestedThisStep;
        private float _parasiteLatchHoldTimer;
        private float _thermalUpdraftTraumaCooldownTimer;
        private float _surfaceGaspUnderwaterTimer;
        private float _surfaceGaspCooldownTimer;
        private float _sargassumRestRecoveryBlend;
        private bool _surfaceGaspSubmergedLatch;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  AMBIENT CURRENT
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private float _currentTimer;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SPEED TRACKING
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private float _prevSpeed;
        private float _prevYawForMomentum;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  REGISTRATION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private bool _registeredTick;
        private bool _registeredFixedTick;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  CACHED MATH
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private Vector3 _moveDirection;
        private Vector3 _forceVector;
        private Vector3 _velocity;
        private Quaternion _cameraWorldRotation;

        private RaycastHit _groundHit;
        private Vector3 _groundCheckOrigin;
        private Vector3 _cachedGravity;
        private float _cachedGravityMagnitude;
        private Vector3 _smoothedGroundNormal;
        private float _minGroundNormalY;
        private readonly RaycastHit[] _groundHitBuffer = new RaycastHit[8]; // COLD ALLOC: reused walkable-ground filter buffer for slope/wall separation

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  EVENTS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public event System.Action OnFootstep;

        /// <summary>Fired when a splash is detected. Float = intensity 0-1.</summary>
        public event System.Action<float> OnWaterSplash;

        /// <summary>Fired when head crosses submerge threshold. Bool = now submerged.</summary>
        public event System.Action<bool> OnSubmergeChange;

        /// <summary>Fired on each exhale cycle underwater. For bubble VFX / audio.</summary>
        public event System.Action OnExhale;
        /// <summary>Fired when the controller decides the visor/camera should receive a wet-lens pulse. Float = intensity 0-1.</summary>
        public event System.Action<float> OnWetLensPulse;
        /// <summary>Fired when active transport control is ripped away and the player is forcefully bailed out. Args: severity, world impulse.</summary>
        public event System.Action<float, Vector3> OnTransportBailout;
        /// <summary>Fired while the fatal pressure loop ramps toward implosion. Float = normalized sequence intensity 0-1.</summary>
        public event System.Action<float> OnFatalPressureSequence;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  CONSTANTS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private const float DEG_TO_RAD = 0.01745329f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  EFFECTIVE WATER SURFACE â€” Crest or fallback
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private float EffectiveWaterSurfaceY => (_crestAvailable && useCrestOceanHeight)
            ? _dynamicWaterSurfaceY
            : _fallbackWaterSurfaceY;

        private Vector3 EffectiveWaterSurfaceNormal => (_crestAvailable && useCrestOceanHeight)
            ? _dynamicWaterSurfaceNormal
            : Vector3.up;

        private Vector3 EffectiveWaterSurfaceVelocity => (_crestAvailable && useCrestOceanHeight)
            ? _dynamicWaterSurfaceVelocity
            : Vector3.zero;

        private Vector3 EffectiveWaterFlowVelocity => (_crestFlowSamplingSucceeded && useCrestOceanHeight)
            ? _dynamicWaterFlowVelocity
            : Vector3.zero;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void SetSuit(SuitData newSuit)
        {
            if (newSuit == null) { Debug.LogWarning("[HectonPlayerMovement] null suit.", this); return; }
            currentSuitData = newSuit;
            ApplySuitToRigidbody();
            EnsureJuiceProcessor();
            _juiceProcessor.Initialize(leanIntoTurn);
            UpdateSuitDiagnostics();
        }

        /// <summary>
        /// Applies a runtime-only multiplier to underwater thrust and swim-speed ceilings.
        /// </summary>
        /// <param name="multiplier">Runtime swim-speed multiplier.</param>
        public void SetRuntimeSwimSpeedMultiplier(float multiplier)
        {
            _runtimeSwimSpeedMultiplier = Mathf.Clamp(multiplier, 0.5f, 3f);
        }

        /// <summary>
        /// Applies a body-state penalty without overwriting external runtime swim buffs.
        /// </summary>
        internal void SetRuntimeInjurySwimSpeedMultiplier(float multiplier)
        {
            _runtimeInjurySwimSpeedMultiplier = Mathf.Clamp(multiplier, 0.35f, 1f);
        }

        public SuitData CurrentSuit => currentSuitData;
        public float WaterImmersionRatio => _waterImmersionRatio;
        public bool IsGrounded => _isGrounded && _isWalking;
        public bool IsWalking => _isWalking;
        /// <summary>Resolved locomotion mode for movement, camera, audio, and VFX consumers.</summary>
        public PlayerLocomotionMode CurrentLocomotionMode => _currentLocomotionMode;
        public float CurrentRoll => _juiceProcessor != null ? _juiceProcessor.CurrentRoll : 0f;
        public float BodyYaw => _bodyYaw;
        public float CameraYaw => _cameraYaw;
        public float CurrentWaterSurfaceY => EffectiveWaterSurfaceY;
        public float CurrentDepth => _currentDepth;
        public bool IsPlayerSubmerged => _juiceProcessor != null && _juiceProcessor.IsSubmerged;
        public bool IsDraggingHeavyCargo => IsHeavyCarryActive();
        public float HeavyCarryLoad => ResolveHeavyCarryLoad01();
        public Quaternion CurrentSurfaceWavePoseLocalRotation => _surfaceWavePoseRotation;
        public Quaternion CurrentUnderwaterTurbulencePoseLocalRotation => _underwaterTurbulencePoseRotation;
        public float CurrentStormIntensity01 => _dynamicStormIntensity;
        public float CurrentShoreBuoyancyBlend01 => _shoreBuoyancyBlend;
        public float CurrentWetLensIntensity01 => _wetLensSignalIntensity;
        /// <summary>Normalized near-surface storm stress signal consumed by camera and post-processing layers.</summary>
        public float CurrentUnderwaterStressIntensity01 => _underwaterStressSignalIntensity;
        /// <summary>Normalized crush-depth stress from extreme pressure and rapid depth change.</summary>
        public float CurrentHullStress01 => _hullStressIntensity;
        /// <summary>Normalized fatal-pressure pre-implosion loop intensity. 0 when inactive, 1 just before implosion.</summary>
        public float CurrentFatalPressureSequence01 => _fatalPressureSequenceIntensity;
        /// <summary>Normalized thermal updraft intensity currently throwing the player upward.</summary>
        public float CurrentThermalUpdraftIntensity01 => _thermalUpdraftIntensity;
        /// <summary>True while the controller is in wipeout recovery and player movement input is suppressed.</summary>
        public bool IsInWipeoutState => _wipeoutTimer > 0f;
        internal float CurrentCuttingTensionForce => _cuttingTensionCurrentForce;
        internal float CurrentCuttingTensionNormalized => math.saturate(_cuttingTensionCurrentForce / math.max(0.01f, cuttingTensionMaxForce));

        /// <summary>
        /// Applies a sticky external drag request that smoothly suppresses swim thrust and swim max speed.
        /// Call this continuously while the player remains inside a dense medium. Pass 1 to release back toward neutral.
        /// </summary>
        /// <param name="dragMultiplier">1 = no extra drag. Values above 1 increase resistance.</param>
        public void ApplyEnvironmentalDrag(float dragMultiplier)
        {
            float clampedDragMultiplier = math.max(1f, dragMultiplier);
            if (clampedDragMultiplier > _externalEnvironmentalDragRequestedMultiplier)
                _externalEnvironmentalDragRequestedMultiplier = clampedDragMultiplier;

            _externalEnvironmentalDragRequestedThisStep = true;

            if (clampedDragMultiplier <= 1f)
            {
                _externalEnvironmentalDragHoldTimer = 0f;
                return;
            }

            _externalEnvironmentalDragHoldTimer = externalEnvironmentalDragHoldTime;
        }

        /// <summary>
        /// Arms the fixed-step cutter spring so locomotion can pull the body back toward the active cut anchor.
        /// LaserCutter owns target acquisition; movement owns actual force application.
        /// </summary>
        internal void ApplyCuttingTensionAnchor(Vector3 anchorPointWS, Vector3 anchorNormalWS)
        {
            _cuttingTensionAnchorRequestedWS = anchorPointWS;
            _cuttingTensionAnchorNormalRequestedWS = anchorNormalWS.sqrMagnitude > 0.0001f
                ? anchorNormalWS.normalized
                : Vector3.up;
            _cuttingTensionRequestedThisStep = true;
            _cuttingTensionHoldTimer = cuttingTensionHoldTime;
        }

        /// <summary>
        /// Explicitly clears the current cutter spring request when the tool stops cutting.
        /// </summary>
        internal void ClearCuttingTensionAnchor()
        {
            _cuttingTensionRequestedThisStep = false;
            _cuttingTensionHoldTimer = 0f;
            _cuttingTensionCurrentForce = 0f;
        }

        /// <summary>
        /// Temporarily releases authored swim posing so a physical hit can bend the body through the active-trauma presentation path.
        /// Also inflates the collision capsule for a short defensive window so the bent pose does not clip through nearby geometry.
        /// </summary>
        public void ApplyPhysicalTrauma(Vector3 impulse, float weight)
        {
            float clampedWeight = math.saturate(weight);
            if (clampedWeight <= 0f)
                return;

            if (_swimPresentationController == null)
                TryGetComponent(out _swimPresentationController);

            if (_swimPresentationController != null)
                _swimPresentationController.ApplyPhysicalTrauma(impulse, clampedWeight);

            if (clampedWeight > _physicalTraumaCollisionWeight)
                _physicalTraumaCollisionWeight = clampedWeight;

            _physicalTraumaCollisionHoldTimer = math.max(
                _physicalTraumaCollisionHoldTimer,
                physicalTraumaCollisionHoldTime * math.lerp(0.6f, 1f, clampedWeight));
        }

        /// <summary>
        /// Applies snap-release feedback when a heavy tow cable catastrophically fails.
        /// </summary>
        internal void ApplyTowCableSnapFeedback(Vector3 releasedVelocityChange, Vector3 traumaImpulse, float severity, float signedRoll)
        {
            float clampedSeverity = math.saturate(severity);
            if (clampedSeverity <= 0f)
                return;

            _rb.AddForce(releasedVelocityChange, ForceMode.VelocityChange);
            ApplyPhysicalTrauma(
                traumaImpulse.sqrMagnitude > 0.0001f
                    ? traumaImpulse
                    : -releasedVelocityChange * (_rb != null ? _rb.mass : 1f),
                math.lerp(0.18f, 0.55f, clampedSeverity));

            if (_juiceProcessor != null)
            {
                _juiceProcessor.RegisterEntanglementStrain(math.lerp(0.18f, 0.62f, clampedSeverity));
                _juiceProcessor.RegisterExternalRollImpulse(signedRoll * math.lerp(2.5f, 8f, clampedSeverity));
            }
        }

        /// <summary>
        /// Returns the current local wave slope derived from the batched Crest body samples.
        /// X = lateral slope (right side higher = positive). Y = longitudinal slope (head/forward side higher = positive).
        /// </summary>
        public Vector2 GetCurrentLocalWaveSlope()
        {
            return _dynamicWaveLocalSlope;
        }

        /// <summary>
        /// Fires a controller-owned active sonar ping through the existing visor sonar owner.
        /// This is an event-path action, not a per-frame scan.
        /// </summary>
        public bool TriggerActiveSonarPing()
        {
            if (_activeSonarPingCooldownTimer > 0f)
                return false;

            SpectrumSystem spectrumSystem = SpectrumSystem.Instance;
            if (spectrumSystem == null)
                return false;

            if (!spectrumSystem.TriggerActiveSonarPing(activeSonarPingRadius, activeSonarRevealDuration))
                return false;

            _activeSonarPingCooldownTimer = activeSonarPingCooldown;
            return true;
        }

        /// <summary>
        /// Accepts a one-step external thermal updraft velocity-change injection from future vent managers.
        /// Call every fixed step while the player remains inside the authored thermal plume.
        /// </summary>
        /// <param name="velocityChange">World-space upward velocity change to inject this fixed step.</param>
        public void ApplyExternalThermalUpdraft(Vector3 velocityChange)
        {
            if (velocityChange.y <= 0.0001f)
                return;

            if (!_externalThermalUpdraftRequestedThisStep ||
                velocityChange.sqrMagnitude > _externalThermalUpdraftVelocityChange.sqrMagnitude)
            {
                _externalThermalUpdraftVelocityChange = velocityChange;
            }

            _externalThermalUpdraftRequestedThisStep = true;
        }

        /// <summary>
        /// Accepts the latest parasite latch aggregate from the asynchronous GPU readback path.
        /// Center of mass is expected in player-local space so side-biased parasite clusters can pull the hull laterally.
        /// </summary>
        public void ApplyParasiteLatchInfluence(int latchedCount, Vector3 parasiteCenterOfMassLS, Vector3 harvesterPullWS)
        {
            int clampedCount = math.max(0, latchedCount);
            if (!_parasiteLatchRequestedThisStep || clampedCount > _parasiteLatchedRequestedCount)
                _parasiteLatchedRequestedCount = clampedCount;

            _parasiteCenterOfMassRequestedLS = parasiteCenterOfMassLS;
            _parasiteHarvesterPullRequestedWS = harvesterPullWS;
            _parasiteLatchRequestedThisStep = true;
            _parasiteLatchHoldTimer = parasiteLatchInfluenceHoldTime;
        }

        /// <summary>
        /// Accepts a one-step external hull-stress request from non-pressure hazards such as parasite swarms.
        /// Call continuously while the hazard remains attached to the active hull.
        /// </summary>
        /// <param name="normalizedStress">Requested normalized hull-stress intensity in the 0..1 range.</param>
        internal void RequestExternalHullStress(float normalizedStress)
        {
            float clampedStress = math.saturate(normalizedStress);
            if (clampedStress <= 0.0001f)
                return;

            if (!_externalHullStressRequestedThisStep || clampedStress > _externalHullStressRequestedIntensity)
                _externalHullStressRequestedIntensity = clampedStress;

            _externalHullStressRequestedThisStep = true;
        }

        /// <summary>
        /// Forces a transport bailout through the locomotion owner.
        /// Use this when an external damage or hazard system decides active transport control must be stripped immediately.
        /// </summary>
        /// <param name="worldImpulse">World-space bailout impulse. Zero falls back to the authored default ejection impulse.</param>
        /// <param name="severity">Normalized bailout severity 0-1.</param>
        public void ForceTransportBailout(Vector3 worldImpulse, float severity = 1f)
        {
            float clampedSeverity = math.saturate(severity);
            StartWipeout(
                math.max(clampedSeverity, 0.65f),
                math.max(_rb != null ? _rb.linearVelocity.magnitude : 0f, wipeoutBailoutSpeedThreshold),
                _cachedTransform != null ? _cachedTransform.position : Vector3.zero,
                Vector3.up,
                null,
                true,
                worldImpulse);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            _instanceId = GetHashCode();
            _cachedTransform = transform;

            _rb = GetComponent<Rigidbody>();
            TryGetComponent(out _capsuleCollider);
            TryGetComponent(out _buoyancy);
            TryGetComponent(out _swimPresentationController);
            TryGetComponent(out _physicalInteractionHandler);
            if (!TryGetComponent(out _heavyTowWinch))
            {
                _heavyTowWinch = gameObject.AddComponent<HeavyTowWinch>(); // COLD ALLOC: HeavyTowWinch[1] — player-owned salvage tow runtime for harpoon/winch towing — owner: HectonPlayerMovement
            }
            TryGetComponent(out _survivalSystem);
            if (!TryGetComponent(out _sargassumMovementInfluence))
            {
                _sargassumMovementInfluence = gameObject.AddComponent<SargassumMovementInfluence>(); // COLD ALLOC: SargassumMovementInfluence[1] â€” player-owned sticky drag receiver for sargassum obstacles â€” owner: HectonPlayerMovement
            }

            _resolvedPhysicalInteractionHandler = true;
            _resolvedHeavyTowWinch = true;
            CacheBaseCollisionProfile();
            ResolvePlayerToolManager();

            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.freezeRotation = true;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.useGravity = false;
            _baseCenterOfMass = _rb.centerOfMass;

            // Cache camera component for FOV manipulation
            if (playerCamera != null)
                _cameraComponent = playerCamera.GetComponent<Camera>();

            Vector3 euler = _cachedTransform.eulerAngles;
            _cameraYaw = euler.y;
            _bodyYaw = euler.y;
            _bodyYawVelocity = 0f;

            if (playerCamera != null)
            {
                float camX = playerCamera.localEulerAngles.x;
                _cameraPitch = camX > 180f ? camX - 360f : camX;
                _cameraPitch = -_cameraPitch;
                _cameraPitch = math.clamp(_cameraPitch, pitchMin, pitchMax);
                _cameraBaseLocalPos = playerCamera.localPosition;
            }

            if (_cameraComponent != null)
                baseFov = _cameraComponent.fieldOfView;

            _cachedGravity = UnityEngine.Physics.gravity;
            _cachedGravityMagnitude = _cachedGravity.magnitude;
            _smoothedGroundNormal = Vector3.up;
            RefreshGroundSlopeCache();
            EnsureJuiceProcessor();
            _juiceProcessor.Initialize(leanIntoTurn);

            // â”€â”€ Crest integration â”€â”€
            _fallbackWaterSurfaceY = ResolveFallbackWaterSurfaceY();
            _dynamicWaterSurfaceY = _fallbackWaterSurfaceY;
            _dynamicWaterSurfaceNormal = Vector3.up;
            _dynamicWaterSurfaceVelocity = Vector3.zero;
            _dynamicWaterFlowVelocity = Vector3.zero;
            _crestSamplingSucceeded = false;
            _crestFlowSamplingSucceeded = false;
            InitOceanKinematics();

            _waterImmersionRatio = ComputeImmersionRatio();
            _smoothedImmersionRatio = _waterImmersionRatio;
            _currentDepth = ComputeDepth();
            if (IsInDryInterior())
            {
                _waterImmersionRatio = 0f;
                _smoothedImmersionRatio = 0f;
                _currentDepth = 0f;
            }
            _isWalking = _waterImmersionRatio < swimTransitionThreshold;
            _isSurfaceSwimming = !_isWalking && !IsInDryInterior() && _currentDepth <= surfaceSwimDepthBand;
            _currentLocomotionMode = ResolveLocomotionMode(_smoothedImmersionRatio);
            _prevSpeed = 0f;
            _prevYawForMomentum = _cameraYaw;
            _currentTimer = 0f;
            _crestFlowInputAttenuation = 1f;
            _surfaceDiveCommitTimer = 0f;
            _surfaceLockBlend = _isSurfaceSwimming ? 1f : 0f;
            _surfaceLockTargetY = _isSurfaceSwimming
                ? EffectiveWaterSurfaceY + surfaceStickOffset
                : _rb.position.y;
            _shoreBuoyancyBlend = _isWalking ? 0f : 1f;
            _bottomClearance = float.PositiveInfinity;
            _bottomNormal = Vector3.up;
            _surfaceWavePoseRotation = Quaternion.identity;
            _underwaterTurbulencePoseRotation = Quaternion.identity;
            _transportCavitationEfficiency = 1f;
            _previousTransportForwardVelocity = 0f;
            _wetLensSignalIntensity = 0f;
            _wetLensPulseCooldownTimer = 0f;
            _underwaterStressSignalIntensity = 0f;
            _dynamicWaveLocalSlope = Vector2.zero;
            _dynamicWaveLongitudinalGradient = Vector3.zero;
            _dynamicWaveLateralGradient = Vector3.zero;
            _sargassumFieldDensity01 = 0f;
            _sargassumMatBuoyancyBlend = 0f;
            _sargassumHighStrainIntensity = 0f;
            _sargassumHighStrainTimer = 0f;
            _abyssalThermalFlowSample = default;
            _abyssalThermalFlowSample.DragMultiplier = 1f;
            _abyssalThermalFlowVelocityWS = Vector3.zero;
            _undertowVector = Vector3.zero;
            _undertowIntensity = 0f;
            _wipeoutTimer = 0f;
            _wipeoutSeverity = 0f;
            _transportBailoutCooldownTimer = 0f;
            _recentBreachExitTimer = 0f;
            _surfaceGaspUnderwaterTimer = 0f;
            _surfaceGaspCooldownTimer = 0f;
            _sargassumRestRecoveryBlend = 0f;
            _surfaceGaspSubmergedLatch = false;
            _externalEnvironmentalDragRequestedMultiplier = 1f;
            _externalEnvironmentalDragCurrentMultiplier = 1f;
            _externalEnvironmentalDragHoldTimer = 0f;
            _externalEnvironmentalDragRequestedThisStep = false;
            _cuttingTensionAnchorRequestedWS = Vector3.zero;
            _cuttingTensionAnchorCurrentWS = Vector3.zero;
            _cuttingTensionAnchorNormalRequestedWS = Vector3.up;
            _cuttingTensionAnchorNormalCurrentWS = Vector3.up;
            _cuttingTensionHoldTimer = 0f;
            _cuttingTensionCurrentForce = 0f;
            _cuttingTensionRequestedThisStep = false;
            _parasiteLatchedRequestedCount = 0;
            _parasiteLatchedCurrentCount = 0;
            _parasiteCenterOfMassRequestedLS = Vector3.zero;
            _parasiteCenterOfMassCurrentLS = Vector3.zero;
            _parasiteHarvesterPullRequestedWS = Vector3.zero;
            _parasiteHarvesterPullCurrentWS = Vector3.zero;
            _parasiteLatchRequestedThisStep = false;
            _parasiteLatchHoldTimer = 0f;
            _requestedTransportCollisionHeightScale = 1f;
            _requestedTransportCollisionRadiusScale = 1f;
            _requestedTransportCollisionCenterYOffset = 0f;
            _dynamicCollisionTuck01 = 0f;
            _physicalTraumaCollisionWeight = 0f;
            _physicalTraumaCollisionHoldTimer = 0f;
            ResetHeavyTowRuntimeResponse();
            _abyssalDowndraftCooldownTimer = 0f;
            _abyssalDowndraftActiveTimer = 0f;
            _abyssalDowndraftIntensity = 0f;
            _abyssalDowndraftVelocityChange = Vector3.zero;
            _abyssalFlowNoiseBoundaryCooldownTimer = 0f;
            _previousAbyssalNoisyFlow = Vector3.zero;
            _abyssalTransportTurbulencePitchOffset = 0f;
            _abyssalTransportTurbulenceYawOffset = 0f;
            _hullStressIntensity = 0f;
            _hullStressGroanCooldownTimer = 0f;
            _hullStressHudCorruptionRefreshTimer = 0f;
            _externalHullStressRequestedIntensity = 0f;
            _externalHullStressRequestedThisStep = false;
            _fatalPressureSequenceTimer = 0f;
            _fatalPressureSequenceGlitchPulseTimer = 0f;
            _fatalPressureSequenceIntensity = 0f;
            _activeSonarPingCooldownTimer = 0f;
            _thermalUpdraftIntensity = 0f;
            _thermalUpdraftVelocityChange = Vector3.zero;
            _externalThermalUpdraftVelocityChange = Vector3.zero;
            _externalThermalUpdraftRequestedThisStep = false;
            _thermalUpdraftTraumaCooldownTimer = 0f;
            _vegetationDensityLinearDamping = 0f;
            _debugSargassumEntanglementDragRequest = 1f;

            ApplySuitToRigidbody();

            _registeredTick = false;
            _registeredFixedTick = false;

            _inputManager = InputManager.Instance;
            UpdateSuitDiagnostics();
        }

        private void EnsureJuiceProcessor()
        {
            if (_juiceProcessor != null)
                return;

            _juiceProcessor = new CameraJuiceProcessor();
        }

        private void OnEnable() 
        { 
            SargassumGlobalDragManager.OnEntanglementStrain += HandleSargassumEntanglementStrain;
            SpectrumEvents.OnSonarPingSent += HandleSonarPingSent;
            ResolvePlayerToolManager();
            RefreshInputManagerBinding();
            TryRegisterToTickManager(); 
        }

        private void Start()
        {
            if (_registeredTick && _registeredFixedTick) return;
            TryRegisterToTickManager();
            if (!_registeredTick || !_registeredFixedTick)
                Debug.LogError("[HectonPlayerMovement] GameTickManager.Instance is null.", this);

            if (useCrestOceanHeight && !_crestAvailable)
                InitOceanKinematics();
        }

        private void OnDisable()
        {
            SargassumGlobalDragManager.OnEntanglementStrain -= HandleSargassumEntanglementStrain;
            SpectrumEvents.OnSonarPingSent -= HandleSonarPingSent;
            GameTickManager inst = GameTickManager.Instance;
            UnsubscribeFromInput();
            _externalEnvironmentalDragRequestedMultiplier = 1f;
            _externalEnvironmentalDragCurrentMultiplier = 1f;
            _externalEnvironmentalDragHoldTimer = 0f;
            _externalEnvironmentalDragRequestedThisStep = false;
            _cuttingTensionAnchorRequestedWS = Vector3.zero;
            _cuttingTensionAnchorCurrentWS = Vector3.zero;
            _cuttingTensionAnchorNormalRequestedWS = Vector3.up;
            _cuttingTensionAnchorNormalCurrentWS = Vector3.up;
            _cuttingTensionHoldTimer = 0f;
            _cuttingTensionCurrentForce = 0f;
            _cuttingTensionRequestedThisStep = false;
            _debugSargassumEntanglementDragRequest = 1f;
            _shoreBuoyancyBlend = 1f;
            _bottomClearance = float.PositiveInfinity;
            _bottomNormal = Vector3.up;
            _surfaceWavePoseRotation = Quaternion.identity;
            _underwaterTurbulencePoseRotation = Quaternion.identity;
            _transportCavitationEfficiency = 1f;
            _previousTransportForwardVelocity = 0f;
            _wetLensSignalIntensity = 0f;
            _wetLensPulseCooldownTimer = 0f;
            _underwaterStressSignalIntensity = 0f;
            _dynamicWaveLocalSlope = Vector2.zero;
            _dynamicWaveLongitudinalGradient = Vector3.zero;
            _dynamicWaveLateralGradient = Vector3.zero;
            _sargassumFieldDensity01 = 0f;
            _sargassumMatBuoyancyBlend = 0f;
            _sargassumHighStrainIntensity = 0f;
            _sargassumHighStrainTimer = 0f;
            _abyssalThermalFlowSample = default;
            _abyssalThermalFlowSample.DragMultiplier = 1f;
            _abyssalThermalFlowVelocityWS = Vector3.zero;
            _undertowVector = Vector3.zero;
            _undertowIntensity = 0f;
            _wipeoutTimer = 0f;
            _wipeoutSeverity = 0f;
            _transportBailoutCooldownTimer = 0f;
            _recentBreachExitTimer = 0f;
            _surfaceGaspUnderwaterTimer = 0f;
            _surfaceGaspCooldownTimer = 0f;
            _sargassumRestRecoveryBlend = 0f;
            _surfaceGaspSubmergedLatch = false;
            _requestedTransportCollisionHeightScale = 1f;
            _requestedTransportCollisionRadiusScale = 1f;
            _requestedTransportCollisionCenterYOffset = 0f;
            _dynamicCollisionTuck01 = 0f;
            _physicalTraumaCollisionWeight = 0f;
            _physicalTraumaCollisionHoldTimer = 0f;
            ResetHeavyTowRuntimeResponse();
            _abyssalDowndraftCooldownTimer = 0f;
            _abyssalDowndraftActiveTimer = 0f;
            _abyssalDowndraftIntensity = 0f;
            _abyssalDowndraftVelocityChange = Vector3.zero;
            _abyssalFlowNoiseBoundaryCooldownTimer = 0f;
            _previousAbyssalNoisyFlow = Vector3.zero;
            _abyssalTransportTurbulencePitchOffset = 0f;
            _abyssalTransportTurbulenceYawOffset = 0f;
            _hullStressIntensity = 0f;
            _hullStressGroanCooldownTimer = 0f;
            _hullStressHudCorruptionRefreshTimer = 0f;
            _fatalPressureSequenceTimer = 0f;
            _fatalPressureSequenceGlitchPulseTimer = 0f;
            _fatalPressureSequenceIntensity = 0f;
            _fatalPressureLookYawAnchor = _cameraYaw;
            _fatalPressureLookPitchAnchor = _cameraPitch;
            _activeSonarPingCooldownTimer = 0f;
            _thermalUpdraftIntensity = 0f;
            _thermalUpdraftVelocityChange = Vector3.zero;
            _externalThermalUpdraftVelocityChange = Vector3.zero;
            _externalThermalUpdraftRequestedThisStep = false;
            _thermalUpdraftTraumaCooldownTimer = 0f;
            _vegetationDensityLinearDamping = 0f;
            ApplyResolvedCollisionProfile(1f, 1f, 0f);

            if (inst == null) return;
            if (_registeredTick) { inst.Unregister((ITickable)this); _registeredTick = false; }
            if (_registeredFixedTick) { inst.Unregister((IFixedTickable)this); _registeredFixedTick = false; }
        }

        private void TryRegisterToTickManager()
        {
            GameTickManager inst = GameTickManager.Instance;
            if (inst == null) return;
            if (!_registeredTick) { inst.Register((ITickable)this); _registeredTick = true; }
            if (!_registeredFixedTick) { inst.Register((IFixedTickable)this); _registeredFixedTick = true; }
        }

        private void ResolvePlayerToolManager()
        {
            if (_playerToolManager != null)
                return;

            if (!TryGetComponent(out _playerToolManager))
                _playerToolManager = GetComponentInChildren<PlayerToolManager>(true);
        }

        private void ResolvePlayerTransportCoordinator()
        {
            if (_playerTransportCoordinator != null)
                return;

            TryGetComponent(out _playerTransportCoordinator);
        }

        private IPlayerTransportSource ResolveActiveTransportSource()
        {
            if (_playerToolManager == null)
                ResolvePlayerToolManager();

            return _playerToolManager != null && !_playerToolManager.IsSwapping
                ? _playerToolManager.CurrentToolTransportSource
                : null;
        }

        private float ResolveActiveTransportPropulsionForce()
        {
            ResolvePlayerTransportCoordinator();
            if (_playerTransportCoordinator != null)
                return _playerTransportCoordinator.ResolveTransportPropulsionForce();

            IPlayerTransportSource transportSource = ResolveActiveTransportSource();
            return transportSource != null
                ? math.max(0f, transportSource.GetTransportPropulsionForce())
                : 0f;
        }

        private float ResolveActiveTransportSpeedMultiplier()
        {
            ResolvePlayerTransportCoordinator();
            if (_playerTransportCoordinator != null)
                return _playerTransportCoordinator.ResolveTransportSpeedMultiplier();

            IPlayerTransportSource transportSource = ResolveActiveTransportSource();
            return transportSource != null
                ? math.max(0.01f, transportSource.GetTransportSpeedMultiplier())
                : 1f;
        }

        private float ResolveActiveTransportBoost01()
        {
            ResolvePlayerTransportCoordinator();
            if (_playerTransportCoordinator != null)
                return _playerTransportCoordinator.ResolveTransportBoost01();

            IPlayerTransportSource transportSource = ResolveActiveTransportSource();
            return transportSource != null
                ? math.saturate(transportSource.GetTransportBoost01())
                : 0f;
        }

        private float ResolveActiveTransportCameraMotionScale()
        {
            ResolvePlayerTransportCoordinator();
            if (_playerTransportCoordinator != null)
                return _playerTransportCoordinator.ResolveTransportCameraMotionScale();

            if (_playerToolManager == null)
                ResolvePlayerToolManager();

            if (_playerToolManager != null && !_playerToolManager.IsSwapping)
            {
                PlayerTransportFeelContract transportFeelContract = _playerToolManager.CurrentToolTransportFeelContract;
                if (transportFeelContract != null)
                    return math.saturate(transportFeelContract.CameraMotionScale);
            }

            return 1f;
        }

        private PlayerTransportPreset ResolveActiveTransportPreset()
        {
            ResolvePlayerTransportCoordinator();
            if (_playerTransportCoordinator != null)
            {
                PlayerTransportPreset transportPreset = _playerTransportCoordinator.ResolveTransportPreset();
                if (transportPreset != null)
                    return transportPreset;
            }

            if (_playerToolManager == null)
                ResolvePlayerToolManager();

            if (_playerToolManager != null && !_playerToolManager.IsSwapping)
            {
                PlayerTransportFeelContract transportFeelContract = _playerToolManager.CurrentToolTransportFeelContract;
                if (transportFeelContract != null)
                    return transportFeelContract.Preset;
            }

            return null;
        }

        private void CacheBaseCollisionProfile()
        {
            _basePlayerHeight = math.max(0.5f, playerHeight);

            if (_capsuleCollider != null)
            {
                _baseCapsuleHeight = math.max(0.5f, _capsuleCollider.height);
                _baseCapsuleRadius = math.max(0.01f, _capsuleCollider.radius);
                _baseCapsuleCenter = _capsuleCollider.center;
            }
            else
            {
                _baseCapsuleHeight = _basePlayerHeight;
                _baseCapsuleRadius = math.max(0.01f, groundCheckRadius);
                _baseCapsuleCenter = Vector3.zero;
            }

            _appliedCollisionHeightScale = 1f;
            _appliedCollisionRadiusScale = 1f;
            _appliedCollisionCenterYOffset = 0f;
            _requestedTransportCollisionHeightScale = 1f;
            _requestedTransportCollisionRadiusScale = 1f;
            _requestedTransportCollisionCenterYOffset = 0f;
            _dynamicCollisionTuck01 = 0f;
        }

        private void UpdateRequestedTransportCollisionProfile(PlayerTransportPreset transportPreset)
        {
            _requestedTransportCollisionRadiusScale = ResolveTransportCollisionRadiusScale(transportPreset);
            _requestedTransportCollisionHeightScale = ResolveTransportCollisionHeightScale(transportPreset);
            _requestedTransportCollisionCenterYOffset = ResolveTransportCollisionCenterYOffset(transportPreset);
        }

        private void ApplyResolvedCollisionProfile(float radiusScale, float heightScale, float centerYOffset)
        {
            if (math.abs(_appliedCollisionRadiusScale - radiusScale) <= 0.0001f &&
                math.abs(_appliedCollisionHeightScale - heightScale) <= 0.0001f &&
                math.abs(_appliedCollisionCenterYOffset - centerYOffset) <= 0.0001f)
                return;

            _appliedCollisionRadiusScale = radiusScale;
            _appliedCollisionHeightScale = heightScale;
            _appliedCollisionCenterYOffset = centerYOffset;
            playerHeight = math.max(0.5f, _basePlayerHeight * heightScale);

            if (_capsuleCollider == null)
                return;

            float scaledRadius = math.max(0.01f, _baseCapsuleRadius * radiusScale);
            float scaledHeight = math.max(scaledRadius * 2f + 0.01f, _baseCapsuleHeight * heightScale);
            Vector3 scaledCenter = _baseCapsuleCenter;
            scaledCenter.y += centerYOffset;

            _capsuleCollider.radius = scaledRadius;
            _capsuleCollider.height = scaledHeight;
            _capsuleCollider.center = scaledCenter;
        }

        private static float ResolveTransportCollisionRadiusScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0.5f, transportPreset.CollisionRadiusScale)
                : 1f;
        }

        private static float ResolveTransportCollisionHeightScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0.5f, transportPreset.CollisionHeightScale)
                : 1f;
        }

        private static float ResolveTransportCollisionCenterYOffset(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? transportPreset.CollisionCenterYOffset
                : 0f;
        }

        private static float ResolveTransportForwardPitchInfluence(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.saturate(transportPreset.ForwardPitchInfluence)
                : 1f;
        }

        private static float ResolveTransportStrafeInputScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0f, transportPreset.StrafeInputScale)
                : 1f;
        }

        private static float ResolveTransportVerticalInputScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0f, transportPreset.VerticalInputScale)
                : 1f;
        }

        private static float ResolveTransportReverseThrustScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0f, transportPreset.ReverseThrustScale)
                : 1f;
        }

        private static float ResolveTransportBodyYawResponsivenessScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0.1f, transportPreset.BodyYawResponsivenessScale)
                : 1f;
        }

        private float ResolveHullStressTurnResponsivenessScale(PlayerTransportPreset transportPreset)
        {
            if (transportPreset == null || _hullStressIntensity <= 0.0001f)
                return 1f;

            return math.lerp(1f, math.max(0.05f, 1f - crushDepthTurnSuppression), _hullStressIntensity);
        }

        private static float ResolveTransportSurfaceDiveAssistScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0f, transportPreset.SurfaceDiveAssistScale)
                : 1f;
        }

        private static float ResolveTransportAmbientCurrentInfluenceScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0f, transportPreset.AmbientCurrentInfluenceScale)
                : 1f;
        }

        private static float ResolveTransportSurfaceLockInfluenceScale(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0f, transportPreset.SurfaceLockInfluenceScale)
                : 1f;
        }

        private bool IsHeavyCarryActive()
        {
            if (!_resolvedPhysicalInteractionHandler)
            {
                TryGetComponent(out _physicalInteractionHandler);
                _resolvedPhysicalInteractionHandler = true;
            }

            return _physicalInteractionHandler != null && _physicalInteractionHandler.IsDraggingHeavyObject;
        }

        private float ResolveHeavyCarryForceMultiplier()
        {
            if (!IsHeavyCarryActive())
                return 1f;

            return _physicalInteractionHandler.ResolveHeavyCarryForceMultiplier();
        }

        private float ResolveHeavyCarrySpeedMultiplier()
        {
            if (!IsHeavyCarryActive())
                return 1f;

            return _physicalInteractionHandler.ResolveHeavyCarrySpeedMultiplier();
        }

        private float ResolveHeavyCarryLoad01()
        {
            if (!IsHeavyCarryActive())
                return 0f;

            return _physicalInteractionHandler.HeavyCarryLoad01;
        }

        private float ResolveHeavyCarryBodyYawSpringMultiplier()
        {
            float heavyCarryLoad = ResolveHeavyCarryLoad01();
            if (heavyCarryLoad <= 0f)
                return 1f;

            return math.lerp(1f, maxHeavyCarryBodyYawSpringMultiplier, heavyCarryLoad);
        }

        private bool IsHeavyTowActive()
        {
            if (!_resolvedHeavyTowWinch)
            {
                TryGetComponent(out _heavyTowWinch);
                _resolvedHeavyTowWinch = true;
            }

            return _heavyTowWinch != null && _heavyTowWinch.HasActiveTow;
        }

        private void UpdateHeavyTowRuntimeResponse(float fixedDeltaTime)
        {
            float targetPitchOffset = 0f;
            float targetRollOffset = 0f;
            Vector3 targetCameraOffset = Vector3.zero;
            Vector3 targetCenterOfMassOffset = Vector3.zero;

            if (IsHeavyTowActive())
            {
                float tension01 = _heavyTowWinch.CurrentTension01;
                float stress01 = _heavyTowWinch.CurrentStress01;
                float lateralPull = _heavyTowWinch.CurrentSignedLateralPull01;
                float backwardPull = _heavyTowWinch.CurrentBackwardPull01;
                float response01 = math.saturate(math.max(tension01, stress01 * 0.65f));

                targetPitchOffset = -backwardPull * heavyTowCameraPitchDegrees * response01;
                targetRollOffset = -lateralPull * heavyTowCameraRollDegrees * response01;
                targetCameraOffset.x = -lateralPull * heavyTowCameraSideOffset * response01;
                targetCameraOffset.z = -backwardPull * heavyTowCameraBackwardOffset * response01;

                targetCenterOfMassOffset.x = lateralPull * heavyTowCenterOfMassLateralShift * response01;
                targetCenterOfMassOffset.y = -heavyTowCenterOfMassDownShift * response01;
                targetCenterOfMassOffset.z = -backwardPull * heavyTowCenterOfMassRearShift * response01;
            }

            float blendT = 1f - math.exp(-math.max(heavyTowResponseBlendSharpness, 0.01f) * fixedDeltaTime);
            _heavyTowCameraPitchOffset = math.lerp(_heavyTowCameraPitchOffset, targetPitchOffset, blendT);
            _heavyTowCameraRollOffset = math.lerp(_heavyTowCameraRollOffset, targetRollOffset, blendT);
            _heavyTowCameraLocalOffset = Vector3.Lerp(_heavyTowCameraLocalOffset, targetCameraOffset, blendT);
            _heavyTowCenterOfMassOffset = Vector3.Lerp(_heavyTowCenterOfMassOffset, targetCenterOfMassOffset, blendT);

            if (_rb != null)
                _rb.centerOfMass = _baseCenterOfMass + _heavyTowCenterOfMassOffset;
        }

        private void ResetHeavyTowRuntimeResponse()
        {
            _heavyTowCameraPitchOffset = 0f;
            _heavyTowCameraRollOffset = 0f;
            _heavyTowCameraLocalOffset = Vector3.zero;
            _heavyTowCenterOfMassOffset = Vector3.zero;
            if (_rb != null)
                _rb.centerOfMass = _baseCenterOfMass;
        }

        private void AdvanceSargassumInfluence(float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            if (_sargassumMovementInfluence == null)
                return;

            SargassumGlobalDragManager dragManager = SargassumGlobalDragManager.Instance;
            if (dragManager != null)
            {
                Vector3 samplePosition = _cachedTransform.position;
                float sampleRadius = _capsuleCollider != null ? Mathf.Max(0.35f, _capsuleCollider.radius) : 0.5f;
                float sampleSpeed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
                bool hasFieldInfluence = dragManager.SampleDetailedInfluence(
                    samplePosition,
                    sampleRadius,
                    sampleSpeed,
                    out SargassumGlobalDragManager.SargassumFieldSample sample);
                _sargassumMovementInfluence.ApplyDetailedFieldInfluence(
                    hasFieldInfluence,
                    sample.SpeedMultiplier,
                    sample.DragMultiplier,
                    sample.Density01,
                    sample.AnchorWS,
                    sample.Entanglement01);
                _sargassumFieldDensity01 = hasFieldInfluence ? sample.Density01 : 0f;
            }
            else
            {
                _sargassumMovementInfluence.ApplyFieldInfluence(false, 1f, 1f, 0f);
                _sargassumFieldDensity01 = 0f;
            }

            _sargassumMovementInfluence.Advance(fixedDeltaTime);
            UpdateSargassumMatBuoyancyBlend(fixedDeltaTime);
            UpdateSargassumHighStrainState(fixedDeltaTime);
            ApplySargassumEnvironmentalDrag(fixedDeltaTime, transportPreset);
            ApplySargassumRestRecovery(fixedDeltaTime);
        }

        private void AdvanceAbyssalThermalInfluence(float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            _abyssalThermalFlowSample = default;
            _abyssalThermalFlowSample.DragMultiplier = 1f;
            _abyssalThermalFlowVelocityWS = Vector3.zero;

            if (_isWalking || IsInDryInterior())
                return;

            AbyssalThermalManager thermalManager = AbyssalThermalManager.Instance;
            if (thermalManager == null)
                return;

            Vector3 samplePosition = _cachedTransform.position;
            float sampleRadius = _capsuleCollider != null ? Mathf.Max(0.35f, _capsuleCollider.radius) : 0.5f;
            bool hasPlayerSample = thermalManager.SampleThermalFlow(samplePosition, sampleRadius, out AbyssalThermalManager.ThermalFlowSample sample);
            AdvanceHeavyTowCableSnare(thermalManager);
            if (!hasPlayerSample)
                return;

            _abyssalThermalFlowSample = sample;
            _abyssalThermalFlowVelocityWS = sample.FlowVelocityWS;

            if (sample.DragMultiplier > 1f)
                ApplyEnvironmentalDrag(sample.DragMultiplier);

            if (sample.IsCableZone)
                ApplyAbyssalCableEnvironmentalDrag(fixedDeltaTime, transportPreset, sample);

            if (sample.HasFlow && fixedDeltaTime > 0f)
                ApplyExternalThermalUpdraft(sample.FlowVelocityWS * fixedDeltaTime);
        }

        private void AdvanceHeavyTowCableSnare(AbyssalThermalManager thermalManager)
        {
            if (_heavyTowWinch == null || thermalManager == null)
                return;

            if (!_heavyTowWinch.TryGetTowPayloadSample(out Vector3 payloadPositionWS, out float payloadRadiusWS))
            {
                _heavyTowWinch.ApplyExternalCableSnare(Vector3.zero, 0f, 1f);
                return;
            }

            if (!thermalManager.SampleThermalFlow(payloadPositionWS, payloadRadiusWS, out AbyssalThermalManager.ThermalFlowSample payloadSample) ||
                !payloadSample.IsCableZone)
            {
                _heavyTowWinch.ApplyExternalCableSnare(Vector3.zero, 0f, 1f);
                return;
            }

            _heavyTowWinch.ApplyExternalCableSnare(
                payloadSample.CableAnchorWS,
                payloadSample.CableTension01,
                payloadSample.CableCutProgress01);
        }

        private float ResolveSargassumSpeedMultiplier()
        {
            return _sargassumMovementInfluence != null
                ? math.clamp(_sargassumMovementInfluence.SpeedMultiplier, 0.1f, 1f)
                : 1f;
        }

        private float ResolveSargassumDragMultiplier()
        {
            return _sargassumMovementInfluence != null
                ? math.max(1f, _sargassumMovementInfluence.DragMultiplier)
                : 1f;
        }

        private float ResolveActiveTransportPropulsionReference(PlayerTransportPreset transportPreset)
        {
            return transportPreset != null
                ? math.max(0.01f, transportPreset.PropulsionForceReference)
                : 0f;
        }

        private void ApplySargassumEnvironmentalDrag(float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            if (_sargassumMovementInfluence == null)
            {
                _debugSargassumEntanglementDragRequest = 1f;
                return;
            }

            float tension = math.saturate(_sargassumMovementInfluence.Entanglement01);
            if (tension <= 0.0001f)
            {
                ApplyEnvironmentalDrag(1f);
                _debugSargassumEntanglementDragRequest = 1f;
                return;
            }

            float massReference = math.max(1f, sargassumEntanglementMassReference);
            float massRatio = math.saturate((_rb.mass - massReference) / (massReference * 3f));
            float bodyMassScale = math.lerp(0.92f, 1.22f, massRatio);
            float propulsionReference = ResolveActiveTransportPropulsionReference(transportPreset);
            float propulsionForce = ResolveActiveTransportPropulsionForce();
            float propulsion01 = propulsionReference > 0f
                ? math.saturate(propulsionForce / propulsionReference)
                : 0f;
            float transportPresence01 = propulsionReference > 0f ? 1f : 0f;
            float maxExtraDrag = math.lerp(
                sargassumEntanglementSwimEnvironmentalDrag,
                sargassumEntanglementTransportEnvironmentalDrag,
                transportPresence01);
            float propulsionRelief = math.lerp(1f, 0.72f, propulsion01);
            float requestedDragMultiplier = 1f + maxExtraDrag * tension * bodyMassScale * propulsionRelief;
            ApplyEnvironmentalDrag(requestedDragMultiplier);
            ApplySargassumEscapeEnergyDrain(fixedDeltaTime, tension, propulsion01);
            _debugSargassumEntanglementDragRequest = requestedDragMultiplier;
        }

        private void ApplySargassumEscapeEnergyDrain(float fixedDeltaTime, float tension, float propulsion01)
        {
            if (_survivalSystem == null || fixedDeltaTime <= 0f)
                return;

            if (tension <= 0.0001f || sargassumEscapeEnergyDrainPerSecond <= 0f)
                return;

            float normalizedIntent = ResolveSargassumEscapeIntent01(propulsion01);
            if (normalizedIntent <= 0f)
                return;
            float drainAmount =
                sargassumEscapeEnergyDrainPerSecond *
                sargassumEntanglementEscapeEnergyMultiplier *
                math.lerp(1f, sargassumHighStrainEnergyMultiplier, _sargassumHighStrainIntensity) *
                tension *
                normalizedIntent *
                fixedDeltaTime;
            if (drainAmount <= 0.0001f)
                return;

            _survivalSystem.DrainEnergy(drainAmount);
        }

        private void ApplyAbyssalCableEnvironmentalDrag(float fixedDeltaTime, PlayerTransportPreset transportPreset, AbyssalThermalManager.ThermalFlowSample sample)
        {
            float tension = math.saturate(sample.CableTension01);
            if (tension <= 0.0001f)
                return;

            float massReference = math.max(1f, sargassumEntanglementMassReference);
            float massRatio = math.saturate((_rb.mass - massReference) / (massReference * 3f));
            float bodyMassScale = math.lerp(0.95f, 1.3f, massRatio);
            float propulsionReference = ResolveActiveTransportPropulsionReference(transportPreset);
            float propulsionForce = ResolveActiveTransportPropulsionForce();
            float propulsion01 = propulsionReference > 0f
                ? math.saturate(propulsionForce / propulsionReference)
                : 0f;
            float transportPresence01 = propulsionReference > 0f ? 1f : 0f;
            float maxExtraDrag = math.lerp(
                abyssalCableEntanglementSwimEnvironmentalDrag,
                abyssalCableEntanglementTransportEnvironmentalDrag,
                transportPresence01);
            float cutReleaseT = ResolveAbyssalCableCutRelease01(sample.CableCutProgress01);
            float propulsionRelief = math.lerp(1f, 1f - abyssalCablePropulsionReliefAtFullCut, propulsion01 * cutReleaseT);
            float suppression = math.max(0.25f, sample.CableEscapeSuppression01);
            float requestedDragMultiplier = 1f + maxExtraDrag * tension * suppression * bodyMassScale * propulsionRelief;
            ApplyEnvironmentalDrag(requestedDragMultiplier);

            if (_survivalSystem == null || fixedDeltaTime <= 0f || abyssalCableEscapeEnergyDrainPerSecond <= 0f)
                return;

            float normalizedIntent = ResolveSargassumEscapeIntent01(propulsion01);
            if (normalizedIntent <= 0f)
                return;

            float drainAmount =
                abyssalCableEscapeEnergyDrainPerSecond *
                abyssalCableEscapeEnergyMultiplier *
                tension *
                suppression *
                normalizedIntent *
                fixedDeltaTime;
            if (drainAmount <= 0.0001f)
                return;

            _survivalSystem.DrainEnergy(drainAmount);
        }

        private void UpdateSargassumHighStrainState(float fixedDeltaTime)
        {
            if (_sargassumHighStrainTimer > 0f)
            {
                _sargassumHighStrainTimer -= fixedDeltaTime;
                if (_sargassumHighStrainTimer < 0f)
                    _sargassumHighStrainTimer = 0f;
            }

            if (_sargassumHighStrainTimer <= 0f)
            {
                float fadeT = 1f - math.exp(-12f * fixedDeltaTime);
                _sargassumHighStrainIntensity = math.lerp(_sargassumHighStrainIntensity, 0f, fadeT);
                if (_sargassumHighStrainIntensity < 0.0001f)
                    _sargassumHighStrainIntensity = 0f;
            }
        }

        private void UpdateSargassumMatBuoyancyBlend(float fixedDeltaTime)
        {
            float targetBlend = 0f;
            if (!IsInDryInterior() && !_isWalking && _waterImmersionRatio > 0.05f)
            {
                float densityDenominator = math.max(1f - sargassumMatBuoyancyDensityThreshold, 0.0001f);
                float densityT = math.saturate((_sargassumFieldDensity01 - sargassumMatBuoyancyDensityThreshold) / densityDenominator);
                if (densityT > 0f)
                {
                    float depthT = 1f - math.saturate(_currentDepth / math.max(sargassumMatBuoyancyMaxDepth, 0.01f));
                    targetBlend = densityT * depthT;
                }
            }

            float blendT = 1f - math.exp(-math.max(sargassumMatBuoyancyBlendSharpness, 0.01f) * fixedDeltaTime);
            _sargassumMatBuoyancyBlend = math.lerp(_sargassumMatBuoyancyBlend, targetBlend, blendT);
        }

        private void ApplySargassumMatBuoyancySupport()
        {
            if (_sargassumMatBuoyancyBlend <= 0.001f || _isWalking || IsInDryInterior())
                return;

            float upwardVelocityAllowance = 1f - math.saturate(math.max(0f, _rb.linearVelocity.y) / math.max(surfaceBreachReleaseVelocity, 0.01f));
            if (upwardVelocityAllowance <= 0.001f)
                return;

            float buoyancyForce = _cachedGravityMagnitude * _rb.mass * sargassumMatBuoyancyForceScale * _sargassumMatBuoyancyBlend * upwardVelocityAllowance;
            if (buoyancyForce <= 0.001f)
                return;

            _forceVector.x = 0f;
            _forceVector.y = buoyancyForce;
            _forceVector.z = 0f;
            _rb.AddForce(_forceVector, ForceMode.Force);
        }

        private void ApplySargassumEntanglementForce(PlayerTransportPreset transportPreset)
        {
            if (_sargassumMovementInfluence == null)
                return;

            float tension = _sargassumMovementInfluence.Entanglement01;
            if (tension <= 0.0001f)
                return;

            Vector3 anchor = _sargassumMovementInfluence.EntanglementAnchorWS;
            Vector3 displacement = _cachedTransform.position - anchor;
            displacement.y *= sargassumEntanglementVerticalInfluence;
            float displacementMagnitude = displacement.magnitude;
            if (displacementMagnitude <= 0.0001f)
                return;

            Vector3 springDirection = displacement / displacementMagnitude;
            float velocityAlongSpring = Vector3.Dot(_rb.linearVelocity, springDirection);
            float massReference = math.max(1f, sargassumEntanglementMassReference);
            float massRatio = math.saturate((_rb.mass - massReference) / (massReference * 3f));
            float bodyMassScale = math.lerp(0.9f, 1.24f, massRatio);
            float propulsionReference = ResolveActiveTransportPropulsionReference(transportPreset);
            float propulsionForce = ResolveActiveTransportPropulsionForce();
            float propulsion01 = propulsionReference > 0f
                ? math.saturate(propulsionForce / propulsionReference)
                : 0f;
            float springRelief = math.lerp(1f, 1f - sargassumEntanglementEscapeRelief, propulsion01);
            float dampingRelief = math.lerp(1f, 0.82f, propulsion01);
            float springForceMagnitude = displacementMagnitude * sargassumEntanglementSpring * tension * bodyMassScale * springRelief;
            float dampingForceMagnitude = velocityAlongSpring * sargassumEntanglementDamping * tension * bodyMassScale * dampingRelief;
            float totalForceMagnitude = springForceMagnitude + dampingForceMagnitude;
            if (totalForceMagnitude <= 0f)
                return;

            _forceVector = -springDirection * totalForceMagnitude * _rb.mass;
            _rb.AddForce(_forceVector, ForceMode.Force);

            float escapeIntent01 = ResolveSargassumEscapeIntent01(propulsion01);
            float strain01 = math.saturate(tension * escapeIntent01 * math.max(0.35f, bodyMassScale * 0.72f));
            if (strain01 >= sargassumEntanglementStrainThreshold)
            {
                SargassumGlobalDragManager.RaiseEntanglementStrain(
                    new SargassumGlobalDragManager.EntanglementStrainSignal
                    {
                        SourceInstanceId = _instanceId,
                        PositionWS = _cachedTransform.position,
                        AnchorWS = anchor,
                        Tension01 = tension,
                        EscapeIntent01 = escapeIntent01,
                        Shake01 = math.saturate(strain01 * sargassumEntanglementCameraShakeScale)
                    });
            }
        }

        private void ApplyAbyssalCableEntanglementForce(PlayerTransportPreset transportPreset)
        {
            if (!_abyssalThermalFlowSample.IsCableZone)
                return;

            float tension = _abyssalThermalFlowSample.CableTension01;
            if (tension <= 0.0001f)
                return;

            Vector3 anchor = _abyssalThermalFlowSample.CableAnchorWS;
            Vector3 displacement = _cachedTransform.position - anchor;
            displacement.y *= abyssalCableEntanglementVerticalInfluence;
            float displacementMagnitude = displacement.magnitude;
            if (displacementMagnitude <= 0.0001f)
                return;

            Vector3 springDirection = displacement / displacementMagnitude;
            float velocityAlongSpring = Vector3.Dot(_rb.linearVelocity, springDirection);
            float propulsionReference = ResolveActiveTransportPropulsionReference(transportPreset);
            float propulsionForce = ResolveActiveTransportPropulsionForce();
            float propulsion01 = propulsionReference > 0f
                ? math.saturate(propulsionForce / propulsionReference)
                : 0f;
            float cutReleaseT = ResolveAbyssalCableCutRelease01(_abyssalThermalFlowSample.CableCutProgress01);
            float springRelief = math.lerp(1f, 1f - abyssalCablePropulsionReliefAtFullCut, propulsion01 * cutReleaseT);
            float dampingRelief = math.lerp(1f, 0.82f, propulsion01 * cutReleaseT);
            float suppression = math.max(0.25f, _abyssalThermalFlowSample.CableEscapeSuppression01);
            float springForceMagnitude = displacementMagnitude * abyssalCableEntanglementSpring * tension * suppression * springRelief;
            float dampingForceMagnitude = velocityAlongSpring * abyssalCableEntanglementDamping * tension * suppression * dampingRelief;
            float totalForceMagnitude = springForceMagnitude + dampingForceMagnitude;
            if (totalForceMagnitude <= 0f)
                return;

            _forceVector = -springDirection * totalForceMagnitude * _rb.mass;
            _rb.AddForce(_forceVector, ForceMode.Force);
        }

        private float ResolveAbyssalCableCutRelease01(float cableCutProgress01)
        {
            if (cableCutProgress01 <= abyssalCableCutReleaseThreshold)
                return 0f;

            return math.saturate(
                (cableCutProgress01 - abyssalCableCutReleaseThreshold) /
                math.max(1f - abyssalCableCutReleaseThreshold, 0.0001f));
        }

        private float ResolveSargassumEscapeIntent01(float propulsion01)
        {
            float planarInputMagnitude = math.saturate(math.sqrt(_inputH * _inputH + _inputV * _inputV));
            float verticalInputMagnitude = math.abs(_inputVertical);
            float inputIntent = math.max(planarInputMagnitude, verticalInputMagnitude * 0.75f);
            float escapeIntent = math.max(inputIntent, propulsion01);
            if (escapeIntent <= sargassumEscapeInputThreshold)
                return 0f;

            return math.saturate((escapeIntent - sargassumEscapeInputThreshold) / math.max(1f - sargassumEscapeInputThreshold, 0.0001f));
        }

        private void HandleSargassumEntanglementStrain(SargassumGlobalDragManager.EntanglementStrainSignal signal)
        {
            if (signal.SourceInstanceId != _instanceId)
                return;

            float shakeIntensity = signal.Shake01;
            if (shakeIntensity >= sargassumHighStrainThreshold)
            {
                float highStrainDenominator = math.max(1f - sargassumHighStrainThreshold, 0.0001f);
                float highStrainT = math.saturate((shakeIntensity - sargassumHighStrainThreshold) / highStrainDenominator);
                _sargassumHighStrainIntensity = math.max(_sargassumHighStrainIntensity, highStrainT);
                _sargassumHighStrainTimer = sargassumHighStrainHoldTime;
                shakeIntensity *= math.lerp(1f, sargassumHighStrainShakeBoost, highStrainT);
            }

            if (_juiceProcessor != null)
                _juiceProcessor.RegisterEntanglementStrain(math.saturate(shakeIntensity));

            TryPlaySargassumEntanglementAudio(signal);
        }

        private void HandleSonarPingSent(float intensity)
        {
            if (_juiceProcessor == null)
                return;

            _juiceProcessor.RegisterSonarPingImpulse(intensity);
        }

        private void TryPlaySargassumEntanglementAudio(SargassumGlobalDragManager.EntanglementStrainSignal signal)
        {
            if (signal.Shake01 <= 0.0001f || Time.time < _nextSargassumEntanglementAudioTime)
                return;

            AudioClip clip = sargassumEntanglementStrainClip != null
                ? sargassumEntanglementStrainClip
                : underwaterImpactClip;
            if (clip == null)
                return;

            if (!SpatialAudioManager.TryGetInstance(out SpatialAudioManager audioManager) || audioManager == null)
                return;

            float volume = math.lerp(0.12f, 0.42f, signal.Shake01);
            float pitch = math.lerp(0.72f, 0.94f, signal.EscapeIntent01);
            audioManager.PlayAtPoint(clip, signal.PositionWS, volume, pitch);
            _nextSargassumEntanglementAudioTime = Time.time + sargassumEntanglementAudioCooldown;
        }

        private void AdvanceExternalEnvironmentalDrag(float fixedDeltaTime)
        {
            if (_externalEnvironmentalDragRequestedThisStep)
            {
                if (_externalEnvironmentalDragRequestedMultiplier > 1f)
                    _externalEnvironmentalDragHoldTimer = externalEnvironmentalDragHoldTime;
            }
            else if (_externalEnvironmentalDragHoldTimer > 0f)
            {
                _externalEnvironmentalDragHoldTimer -= fixedDeltaTime;
                if (_externalEnvironmentalDragHoldTimer < 0f)
                    _externalEnvironmentalDragHoldTimer = 0f;
            }

            float targetDragMultiplier =
                _externalEnvironmentalDragRequestedThisStep || _externalEnvironmentalDragHoldTimer > 0f
                    ? math.max(1f, _externalEnvironmentalDragRequestedMultiplier)
                    : 1f;
            float blendT = 1f - math.exp(-math.max(externalEnvironmentalDragBlendSpeed, 0.01f) * fixedDeltaTime);
            _externalEnvironmentalDragCurrentMultiplier = math.lerp(_externalEnvironmentalDragCurrentMultiplier, targetDragMultiplier, blendT);

            _externalEnvironmentalDragRequestedMultiplier = 1f;
            _externalEnvironmentalDragRequestedThisStep = false;
        }

        private float ResolveExternalEnvironmentalDragMultiplier()
        {
            return math.max(1f, _externalEnvironmentalDragCurrentMultiplier);
        }

        private float ResolveExternalEnvironmentalSpeedMultiplier()
        {
            return math.rsqrt(ResolveExternalEnvironmentalDragMultiplier());
        }

        private float ResolveExternalEnvironmentalThrustMultiplier()
        {
            return math.rcp(ResolveExternalEnvironmentalDragMultiplier());
        }

        private void AdvanceCuttingTensionRequest(float fixedDeltaTime)
        {
            if (_cuttingTensionRequestedThisStep)
            {
                _cuttingTensionAnchorCurrentWS = _cuttingTensionAnchorRequestedWS;
                _cuttingTensionAnchorNormalCurrentWS = _cuttingTensionAnchorNormalRequestedWS;
            }
            else if (_cuttingTensionHoldTimer > 0f)
            {
                _cuttingTensionHoldTimer -= fixedDeltaTime;
                if (_cuttingTensionHoldTimer <= 0f)
                {
                    _cuttingTensionHoldTimer = 0f;
                    _cuttingTensionCurrentForce = 0f;
                }
            }
            else
            {
                _cuttingTensionCurrentForce = 0f;
            }

            _cuttingTensionRequestedThisStep = false;
        }

        private void ApplyCuttingTensionPhysics(float fixedDeltaTime)
        {
            AdvanceCuttingTensionRequest(fixedDeltaTime);
            if (_cuttingTensionHoldTimer <= 0f)
                return;

            Vector3 anchorOffset = _cuttingTensionAnchorCurrentWS - _cachedTransform.position;
            float anchorDistance = anchorOffset.magnitude;
            if (anchorDistance <= 0.0001f)
                return;

            float extension = anchorDistance - math.max(0.01f, cuttingTensionRestLength);
            if (extension <= 0f)
            {
                _cuttingTensionCurrentForce = 0f;
                return;
            }

            Vector3 springDirection = anchorOffset / anchorDistance;
            float velocityAlongSpring = Vector3.Dot(_rb.linearVelocity, springDirection);
            float forceMagnitude = extension * cuttingTensionSpring;
            forceMagnitude -= velocityAlongSpring * cuttingTensionDamping;
            forceMagnitude = math.clamp(forceMagnitude, 0f, cuttingTensionMaxForce);
            _cuttingTensionCurrentForce = forceMagnitude;
            if (forceMagnitude <= 0.0001f)
                return;

            _rb.AddForce(springDirection * forceMagnitude, ForceMode.Force);
            if (_juiceProcessor != null)
                _juiceProcessor.RegisterEntanglementStrain(math.saturate(forceMagnitude / math.max(0.01f, cuttingTensionMaxForce)) * 0.55f);
        }

        private void AdvanceParasiteLatchInfluence(float fixedDeltaTime)
        {
            if (_parasiteLatchRequestedThisStep)
            {
                if (_parasiteLatchedRequestedCount > 0)
                    _parasiteLatchHoldTimer = parasiteLatchInfluenceHoldTime;
            }
            else if (_parasiteLatchHoldTimer > 0f)
            {
                _parasiteLatchHoldTimer -= fixedDeltaTime;
                if (_parasiteLatchHoldTimer < 0f)
                    _parasiteLatchHoldTimer = 0f;
            }

            bool keepInfluenceAlive = _parasiteLatchRequestedThisStep || _parasiteLatchHoldTimer > 0f;
            float targetCount = keepInfluenceAlive ? _parasiteLatchedRequestedCount : 0f;
            Vector3 targetCenterOfMass = keepInfluenceAlive ? _parasiteCenterOfMassRequestedLS : Vector3.zero;
            Vector3 targetHarvesterPull = keepInfluenceAlive ? _parasiteHarvesterPullRequestedWS : Vector3.zero;
            float blendT = 1f - math.exp(-math.max(parasiteLatchInfluenceBlendSpeed, 0.01f) * fixedDeltaTime);

            _parasiteLatchedCurrentCount = Mathf.RoundToInt(math.lerp(_parasiteLatchedCurrentCount, targetCount, blendT));
            _parasiteCenterOfMassCurrentLS = Vector3.Lerp(_parasiteCenterOfMassCurrentLS, targetCenterOfMass, blendT);
            _parasiteHarvesterPullCurrentWS = Vector3.Lerp(_parasiteHarvesterPullCurrentWS, targetHarvesterPull, blendT);

            _parasiteLatchedRequestedCount = 0;
            _parasiteCenterOfMassRequestedLS = Vector3.zero;
            _parasiteHarvesterPullRequestedWS = Vector3.zero;
            _parasiteLatchRequestedThisStep = false;

            _debugParasiteLatchedCount = _parasiteLatchedCurrentCount;
            _debugParasiteCenterOfMassLS = _parasiteCenterOfMassCurrentLS;
            _debugParasiteHarvesterPullWS = _parasiteHarvesterPullCurrentWS;
        }

        private void ApplyParasiteLatchForces(float fixedDeltaTime)
        {
            if (_parasiteLatchedCurrentCount <= 0 || _playerTransportCoordinator == null || !_playerTransportCoordinator.IsTransportActive())
                return;

            float latchForce01 = math.saturate(_parasiteLatchedCurrentCount / math.max(1f, parasiteLatchCountForFullForce));
            if (latchForce01 <= 0.0001f)
                return;

            Vector3 applicationPoint = _cachedTransform.TransformPoint(_parasiteCenterOfMassCurrentLS);
            Vector3 localCenter = _parasiteCenterOfMassCurrentLS;
            Vector3 localLateral = new Vector3(localCenter.x, 0f, localCenter.z);
            float localLateralMagnitude = localLateral.magnitude;
            Vector3 centerOfMassForce = Vector3.zero;
            if (localLateralMagnitude > 0.0001f)
            {
                Vector3 localBiasDirection = localLateral / localLateralMagnitude;
                float sideBias01 = math.saturate(localLateralMagnitude / math.max(0.25f, parasiteLatchCountForFullForce * 0.05f));
                centerOfMassForce = _cachedTransform.TransformDirection(localBiasDirection) * (parasiteCenterOfMassForce * latchForce01 * sideBias01);
            }

            Vector3 harvesterForce = Vector3.zero;
            if (_parasiteHarvesterPullCurrentWS.sqrMagnitude > 0.0001f)
                harvesterForce = _parasiteHarvesterPullCurrentWS.normalized * (parasiteHarvesterPullForce * latchForce01);

            Vector3 totalForce = centerOfMassForce + harvesterForce;
            if (totalForce.sqrMagnitude <= 0.0001f)
                return;

            _rb.AddForceAtPosition(totalForce, applicationPoint, ForceMode.Force);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  COLLISION â€” camera shake integration
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void OnCollisionEnter(Collision collision)
        {
            if (currentSuitData == null)
                return;

            float relSpeed = collision.relativeVelocity.magnitude;
            if (_juiceProcessor != null && currentSuitData.enableCollisionShake)
                _juiceProcessor.RegisterCollisionImpulse(relSpeed, currentSuitData);

            Vector3 hitPoint = _cachedTransform.position;
            Vector3 hitNormal = Vector3.up;
            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                hitPoint = contact.point;
                hitNormal = contact.normal;
            }

            ResolvePlayerTransportCoordinator();
            IPlayerTransportLifecycleOwner transportLifecycleOwner = null;
            if (_playerTransportCoordinator != null &&
                _playerTransportCoordinator.IsTransportActive())
            {
                _playerTransportCoordinator.TryResolveTransportLifecycleOwner(out transportLifecycleOwner);
            }

            if (transportLifecycleOwner != null)
                transportLifecycleOwner.ApplyTransportCollisionImpact(relSpeed, hitPoint, hitNormal);

            TryTriggerExosuitCollisionImpactFeedback(collision, relSpeed);
            TryStartWipeoutFromCollision(collision, relSpeed, hitPoint, hitNormal, transportLifecycleOwner);
        }

        private void TryTriggerExosuitCollisionImpactFeedback(Collision collision, float relativeSpeed)
        {
            if (_currentLocomotionMode != PlayerLocomotionMode.ExosuitLocomotion)
                return;

            if (collision == null || collision.collider == null || collision.collider.isTrigger)
                return;

            int collisionLayerMask = 1 << collision.collider.gameObject.layer;
            if ((groundLayers.value & collisionLayerMask) == 0)
                return;

            if (relativeSpeed < exosuitImpactShakeSpeedThreshold)
                return;

            if (_juiceProcessor != null && currentSuitData != null)
                _juiceProcessor.RegisterCollisionImpulse(relativeSpeed * exosuitImpactShakeScale, currentSuitData);

            ResolveUnderwaterVisuals();
            if (_underwaterVisuals != null && exosuitImpactSiltBurstScale > 0f)
            {
                float burst01 = math.saturate(relativeSpeed / math.max(exosuitImpactShakeSpeedThreshold * 2f, 0.01f));
                _underwaterVisuals.TriggerExternalBottomSiltBurst(burst01 * exosuitImpactSiltBurstScale);
            }
        }

        private void TryStartWipeoutFromCollision(
            Collision collision,
            float relativeSpeed,
            Vector3 hitPoint,
            Vector3 hitNormal,
            IPlayerTransportLifecycleOwner transportLifecycleOwner)
        {
            if (_wipeoutTimer > 0f)
                return;

            if (collision == null || collision.collider == null || collision.collider.isTrigger)
                return;

            int collisionLayerMask = 1 << collision.collider.gameObject.layer;
            if ((groundLayers.value & collisionLayerMask) == 0)
                return;

            bool transportImpact =
                ResolveActiveTransportPropulsionForce() > 0.01f ||
                (transportLifecycleOwner != null &&
                 _playerTransportCoordinator != null &&
                 _playerTransportCoordinator.IsTransportActive());
            bool breachRockImpact = _recentBreachExitTimer > 0f && _waterImmersionRatio <= 0.12f;
            if (!transportImpact && !breachRockImpact)
                return;

            if (relativeSpeed < wipeoutImpactDeltaVelocityThreshold)
                return;

            float severity = math.saturate(
                (relativeSpeed - wipeoutImpactDeltaVelocityThreshold) /
                math.max(wipeoutImpactDeltaVelocityMax - wipeoutImpactDeltaVelocityThreshold, 0.01f));
            if (severity <= 0f)
                return;

            bool requestTransportBailout = ShouldRequestTransportBailout(relativeSpeed, transportLifecycleOwner);
            StartWipeout(severity, relativeSpeed, hitPoint, hitNormal, transportLifecycleOwner, requestTransportBailout, Vector3.zero);
        }

        private void StartWipeout(
            float severity,
            float impactSpeed,
            Vector3 hitPoint,
            Vector3 hitNormal,
            IPlayerTransportLifecycleOwner transportLifecycleOwner,
            bool requestTransportBailout,
            Vector3 bailoutImpulse)
        {
            bool wasAlreadyInWipeout = _wipeoutTimer > 0f;
            _wipeoutSeverity = math.max(_wipeoutSeverity, severity);
            _wipeoutTimer = math.max(_wipeoutTimer, wipeoutDuration);
            _recentBreachExitTimer = 0f;
            _surfaceBreachLockTimer = 0f;
            _surfaceDiveAssistTimer = 0f;
            _surfaceDiveCommitTimer = 0f;
            _surfaceLockBlend = 0f;
            _jumpRequested = false;
            _jumpBufferTimer = 0f;

            if (_juiceProcessor != null && currentSuitData != null)
                _juiceProcessor.RegisterCollisionImpulse(impactSpeed * math.lerp(1.25f, 1.8f, severity), currentSuitData);

            if (transportLifecycleOwner != null)
                transportLifecycleOwner.ApplyTransportCollisionImpact(impactSpeed * wipeoutTransportDamageScale, hitPoint, hitNormal);

            if (!wasAlreadyInWipeout)
                TryBreakSuitUpgradeFromWipeout();
            EmitWipeoutImpactFeedback(severity);

            Vector3 reboundDirection = hitNormal + Vector3.up * 0.28f;
            if (reboundDirection.sqrMagnitude <= 0.0001f)
                reboundDirection = Vector3.up;
            else
                reboundDirection.Normalize();

            float reboundImpulse = wipeoutReboundImpulse * math.lerp(0.75f, 1.35f, severity);
            _forceVector = reboundDirection * reboundImpulse * _rb.mass;
            _rb.AddForce(_forceVector, ForceMode.Impulse);
            ApplyPhysicalTrauma(reboundDirection * reboundImpulse * _rb.mass, severity);

            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            _survivalSystem?.ReportPhysicalTrauma(impactSpeed, severity);

            if (requestTransportBailout)
                TriggerTransportBailout(severity, hitNormal, transportLifecycleOwner, bailoutImpulse);
        }

        private void TryBreakSuitUpgradeFromWipeout()
        {
            if (wipeoutSuitUpgradeBreakChance <= 0f)
                return;

            SuitUpgradeManager suitUpgradeManager = SuitUpgradeManager.Instance;
            if (suitUpgradeManager == null)
                return;

            suitUpgradeManager.TryBreakRandomInstalledUpgrade(wipeoutSuitUpgradeBreakChance, out _);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  CREST OCEAN HEIGHT SAMPLING
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void InitOceanKinematics()
        {
            _fallbackWaterSurfaceY = ResolveFallbackWaterSurfaceY();
            _dynamicWaterSurfaceY = _fallbackWaterSurfaceY;
            _dynamicWaterSurfaceNormal = Vector3.up;
            _dynamicWaterSurfaceVelocity = Vector3.zero;
            _dynamicWaterFlowVelocity = Vector3.zero;
            _dynamicWaterDisplacement = Vector3.zero;
            _dynamicAverageWaterVelocity = Vector3.zero;
            _dynamicAverageWaterDisplacement = Vector3.zero;
            _dynamicWaveHeightSpan = 0f;
            _dynamicStormIntensity = 0f;
            _crestAvailable = false;
            _crestFlowSamplingSucceeded = false;
            if (!useCrestOceanHeight)
            {
                UpdateCrestDiagnostics();
                return;
            }

            IHectonOceanKinematics oceanKinematics = ResolveOceanKinematics(forceSceneSearch: true);
            if (oceanKinematics != null && oceanKinematics.IsAvailable)
            {
                _crestAvailable = true;
                _dynamicWaterSurfaceY = ResolveOceanSeaLevel(oceanKinematics);
            }

            UpdateCrestDiagnostics();
        }

        private void UpdateOceanWaterHeight()
        {
            _fallbackWaterSurfaceY = ResolveFallbackWaterSurfaceY();

            if (!useCrestOceanHeight)
            {
                _crestAvailable = false;
                _crestSamplingSucceeded = false;
                ResetDynamicWaveSampling();
                _dynamicWaterSurfaceY = _fallbackWaterSurfaceY;
                _dynamicWaterFlowVelocity = Vector3.zero;
                _crestFlowSamplingSucceeded = false;
                UpdateCrestDiagnostics();
                return;
            }

            IHectonOceanKinematics oceanKinematics = ResolveOceanKinematics(forceSceneSearch: !_crestAvailable);
            if (oceanKinematics == null || !oceanKinematics.IsAvailable)
            {
                _crestAvailable = false;
                _crestSamplingSucceeded = false;
                ResetDynamicWaveSampling();
                _dynamicWaterSurfaceY = _fallbackWaterSurfaceY;
                _dynamicWaterFlowVelocity = Vector3.zero;
                _crestFlowSamplingSucceeded = false;
                UpdateCrestDiagnostics();
                return;
            }

            float forwardSampleDistance = ResolveCrestForwardSampleDistance();
            float lateralSampleDistance = ResolveCrestLateralSampleDistance();
            float minSpatialLength = ResolveCrestBodySampleMinLength(forwardSampleDistance, lateralSampleDistance);
            UpdateCrestQueryPoints(forwardSampleDistance, lateralSampleDistance);

            _crestAvailable = true;
            _crestSamplingSucceeded = oceanKinematics.GetWaterHeight(
                _crestQueryPoints,
                CrestBodySampleCount,
                minSpatialLength,
                _crestQueryHeights);

            if (_crestSamplingSucceeded)
            {
                bool waveQuerySucceeded = oceanKinematics.GetWaveNormal(
                    _crestQueryPoints,
                    CrestBodySampleCount,
                    minSpatialLength,
                    _crestQueryNormals,
                    _crestQueryVelocities,
                    _crestQueryDisplacements);
                if (!waveQuerySucceeded)
                {
                    for (int i = 0; i < CrestBodySampleCount; i++)
                    {
                        _crestQueryNormals[i] = Vector3.up;
                        _crestQueryVelocities[i] = Vector3.zero;
                        _crestQueryDisplacements[i] = Vector3.zero;
                    }
                }

                UpdateDynamicWaveSampling();
            }
            else
            {
                ResetDynamicWaveSampling();
                _dynamicWaterSurfaceY = ResolveOceanSeaLevel(oceanKinematics);
            }

            UpdateOceanFlowSampling(oceanKinematics, minSpatialLength);
            UpdateCrestDiagnostics();
        }

        private float ResolveFallbackWaterSurfaceY()
        {
            HectonFluidEngine fluidEngine = HectonFluidEngine.Instance;
            return fluidEngine != null ? fluidEngine.WaterLevel : waterSurfaceY;
        }

        private float ResolveOceanSeaLevel(IHectonOceanKinematics oceanKinematics)
        {
            if (oceanKinematics != null)
                return oceanKinematics.SeaLevel;

            return _fallbackWaterSurfaceY;
        }

        private void UpdateOceanFlowSampling(IHectonOceanKinematics oceanKinematics, float minSpatialLength)
        {
            if (oceanKinematics == null || !oceanKinematics.IsAvailable)
            {
                _dynamicWaterFlowVelocity = Vector3.zero;
                _crestFlowSamplingSucceeded = false;
                return;
            }

            _crestFlowSamplingSucceeded = oceanKinematics.GetSurfaceFlow(
                _crestQueryPoints,
                CrestBodySampleCount,
                minSpatialLength,
                _crestQueryFlows);
            if (_crestFlowSamplingSucceeded)
            {
                Vector3 averageFlow = Vector3.zero;
                for (int i = 0; i < CrestBodySampleCount; i++)
                    averageFlow += _crestQueryFlows[i];

                _dynamicWaterFlowVelocity = averageFlow / CrestBodySampleCount;
                _dynamicWaterFlowVelocity.y = 0f;
            }
            else
            {
                _dynamicWaterFlowVelocity = Vector3.zero;
            }
        }

        private void ResetDynamicWaveSampling()
        {
            _dynamicWaterSurfaceY = _fallbackWaterSurfaceY;
            _dynamicWaterSurfaceNormal = Vector3.up;
            _dynamicWaterSurfaceVelocity = Vector3.zero;
            _dynamicWaterDisplacement = Vector3.zero;
            _dynamicAverageWaterVelocity = Vector3.zero;
            _dynamicAverageWaterDisplacement = Vector3.zero;
            _dynamicWaveHeightSpan = 0f;
            _dynamicStormIntensity = 0f;
            _dynamicWaveLocalSlope = Vector2.zero;
            _dynamicWaveLongitudinalGradient = Vector3.zero;
            _dynamicWaveLateralGradient = Vector3.zero;
        }

        private float ResolveCrestForwardSampleDistance()
        {
            return math.max(crestBodyForwardSampleDistance, playerHeight * 0.26f);
        }

        private float ResolveCrestLateralSampleDistance()
        {
            float colliderRadius = _capsuleCollider != null ? _capsuleCollider.radius : groundCheckRadius;
            return math.max(crestBodyLateralSampleDistance, colliderRadius * 1.1f);
        }

        private float ResolveCrestBodySampleMinLength(float forwardDistance, float lateralDistance)
        {
            return math.max(crestBodySampleMinLength, math.max(forwardDistance, lateralDistance));
        }

        private void UpdateCrestQueryPoints(float forwardDistance, float lateralDistance)
        {
            Vector3 center = _rb.position;
            float yawRad = _bodyYaw * DEG_TO_RAD;
            float sinYaw = math.sin(yawRad);
            float cosYaw = math.cos(yawRad);
            Vector3 bodyForward = new Vector3(sinYaw, 0f, cosYaw);
            Vector3 bodyRight = new Vector3(cosYaw, 0f, -sinYaw);

            _crestQueryPoints[CrestSampleCenter] = center;
            _crestQueryPoints[CrestSampleHead] = center + bodyForward * forwardDistance;
            _crestQueryPoints[CrestSampleFeet] = center - bodyForward * forwardDistance;
            _crestQueryPoints[CrestSampleLeft] = center - bodyRight * lateralDistance;
            _crestQueryPoints[CrestSampleRight] = center + bodyRight * lateralDistance;
        }

        private void UpdateDynamicWaveSampling()
        {
            Vector3 headPoint = _crestQueryPoints[CrestSampleHead];
            headPoint.y = _crestQueryHeights[CrestSampleHead];
            Vector3 feetPoint = _crestQueryPoints[CrestSampleFeet];
            feetPoint.y = _crestQueryHeights[CrestSampleFeet];
            Vector3 leftPoint = _crestQueryPoints[CrestSampleLeft];
            leftPoint.y = _crestQueryHeights[CrestSampleLeft];
            Vector3 rightPoint = _crestQueryPoints[CrestSampleRight];
            rightPoint.y = _crestQueryHeights[CrestSampleRight];

            Vector3 longitudinalAxis = headPoint - feetPoint;
            Vector3 lateralAxis = rightPoint - leftPoint;
            Vector3 derivedNormal = Vector3.Cross(lateralAxis, longitudinalAxis);
            if (derivedNormal.y < 0f)
                derivedNormal = -derivedNormal;

            float longitudinalHorizontalDistance = math.sqrt(
                longitudinalAxis.x * longitudinalAxis.x +
                longitudinalAxis.z * longitudinalAxis.z);
            float lateralHorizontalDistance = math.sqrt(
                lateralAxis.x * lateralAxis.x +
                lateralAxis.z * lateralAxis.z);
            float longitudinalGradientValue = longitudinalAxis.y / math.max(longitudinalHorizontalDistance, 0.01f);
            float lateralGradientValue = lateralAxis.y / math.max(lateralHorizontalDistance, 0.01f);
            _dynamicWaveLongitudinalGradient = longitudinalHorizontalDistance > 0.01f
                ? new Vector3(longitudinalAxis.x, 0f, longitudinalAxis.z).normalized * longitudinalGradientValue
                : Vector3.zero;
            _dynamicWaveLateralGradient = lateralHorizontalDistance > 0.01f
                ? new Vector3(lateralAxis.x, 0f, lateralAxis.z).normalized * lateralGradientValue
                : Vector3.zero;
            _dynamicWaveLocalSlope.x = lateralGradientValue;
            _dynamicWaveLocalSlope.y = longitudinalGradientValue;

            if (derivedNormal.sqrMagnitude > 0.0001f)
            {
                derivedNormal.Normalize();
            }
            else
            {
                Vector3 sampledNormal = _crestQueryNormals[CrestSampleCenter];
                derivedNormal = sampledNormal.sqrMagnitude > 0.0001f
                    ? sampledNormal.normalized
                    : Vector3.up;
            }

            Vector3 averageVelocity = Vector3.zero;
            Vector3 averageDisplacement = Vector3.zero;
            for (int i = 0; i < CrestBodySampleCount; i++)
            {
                averageVelocity += _crestQueryVelocities[i];
                averageDisplacement += _crestQueryDisplacements[i];
            }

            averageVelocity /= CrestBodySampleCount;
            averageDisplacement /= CrestBodySampleCount;

            _dynamicWaterSurfaceY = _crestQueryHeights[CrestSampleCenter];
            _dynamicWaterSurfaceNormal = derivedNormal;
            _dynamicWaterSurfaceVelocity = _crestQueryVelocities[CrestSampleCenter];
            _dynamicWaterDisplacement = _crestQueryDisplacements[CrestSampleCenter];
            _dynamicAverageWaterVelocity = averageVelocity;
            _dynamicAverageWaterDisplacement = averageDisplacement;
            _dynamicWaveHeightSpan = math.max(
                math.abs(_crestQueryHeights[CrestSampleHead] - _crestQueryHeights[CrestSampleFeet]),
                math.abs(_crestQueryHeights[CrestSampleRight] - _crestQueryHeights[CrestSampleLeft]));

            float horizontalDisplacementMagnitude = math.sqrt(
                averageDisplacement.x * averageDisplacement.x +
                averageDisplacement.z * averageDisplacement.z);
            float horizontalVelocityMagnitude = math.sqrt(
                averageVelocity.x * averageVelocity.x +
                averageVelocity.z * averageVelocity.z);
            float heightStormT = math.saturate(
                (_dynamicWaveHeightSpan - underwaterTurbulenceHeightStart) /
                math.max(underwaterTurbulenceHeightMax - underwaterTurbulenceHeightStart, 0.01f));
            float displacementStormT = math.saturate(
                (horizontalDisplacementMagnitude - underwaterTurbulenceDisplacementStart) /
                math.max(underwaterTurbulenceDisplacementMax - underwaterTurbulenceDisplacementStart, 0.01f));
            float velocityStormT = math.saturate(horizontalVelocityMagnitude / math.max(underwaterTurbulenceVelocityMax, 0.01f));
            _dynamicStormIntensity = math.max(heightStormT, math.max(displacementStormT, velocityStormT));
        }

        private IHectonOceanKinematics ResolveOceanKinematics(bool forceSceneSearch)
        {
            if (oceanKinematicsProvider is IHectonOceanKinematics assignedProvider)
            {
                _oceanKinematics = assignedProvider;
                return _oceanKinematics;
            }

            _oceanKinematics = null;
            if (!forceSceneSearch || !Application.isPlaying)
                return null;

            float now = Time.unscaledTime;
            if (now < _nextOceanKinematicsResolveTime)
                return null;

            _nextOceanKinematicsResolveTime = now + 1f;

            _sceneRootBuffer.Clear();
            gameObject.scene.GetRootGameObjects(_sceneRootBuffer);
            int rootCount = _sceneRootBuffer.Count;
            IHectonOceanKinematics bestCandidate = null;
            MonoBehaviour bestBehaviour = null;
            int bestPriority = int.MinValue;
            for (int i = 0; i < rootCount; i++)
            {
                GameObject rootObject = _sceneRootBuffer[i];
                if (rootObject == null)
                    continue;

                _sceneOceanProviderBuffer.Clear();
                rootObject.GetComponentsInChildren(true, _sceneOceanProviderBuffer);
                for (int componentIndex = 0; componentIndex < _sceneOceanProviderBuffer.Count; componentIndex++)
                {
                    MonoBehaviour candidateBehaviour = _sceneOceanProviderBuffer[componentIndex];
                    if (!(candidateBehaviour is IHectonOceanKinematics candidate))
                        continue;

                    int priority = ResolveOceanKinematicsProviderPriority(candidateBehaviour);
                    if (priority <= bestPriority)
                        continue;

                    bestPriority = priority;
                    bestBehaviour = candidateBehaviour;
                    bestCandidate = candidate;
                }
            }

            if (bestCandidate == null)
                return null;

            oceanKinematicsProvider = bestBehaviour;
            _oceanKinematics = bestCandidate;
            return _oceanKinematics;
        }

        private static int ResolveOceanKinematicsProviderPriority(MonoBehaviour provider)
        {
            if (provider is Crest5KinematicsAdapter)
                return 200;

            if (provider is Crest4KinematicsAdapter)
                return 100;

            return 0;
        }

        private PlayerTransportOccupancyMode ResolveActiveTransportOccupancyMode()
        {
            ResolvePlayerTransportCoordinator();
            return _playerTransportCoordinator != null
                ? _playerTransportCoordinator.ResolveTransportOccupancyMode()
                : PlayerTransportOccupancyMode.Handheld;
        }

        private bool IsExosuitTransportActive()
        {
            if (ResolveActiveTransportPreset() == null)
                return false;

            return ResolveActiveTransportOccupancyMode() == PlayerTransportOccupancyMode.Exosuit;
        }

        private void ResolveUnderwaterVisuals()
        {
            if (_resolvedUnderwaterVisuals)
                return;

            _underwaterVisuals = GetComponentInChildren<HectonUnderwaterVisuals>(true);
            _resolvedUnderwaterVisuals = true;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INPUT SYSTEM INTEGRATION (Zero GC)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void SubscribeToInput()
        {
            if (_inputManager == null || _subscribedInputManager == _inputManager) return;

            _inputManager.OnJump += HandleJumpInput;
            _inputManager.OnSprint += HandleSprintStarted;
            _inputManager.OnSprintCanceled += HandleSprintCanceled;
            _subscribedInputManager = _inputManager;
        }

        private void UnsubscribeFromInput()
        {
            if (_subscribedInputManager == null) return;

            _subscribedInputManager.OnJump -= HandleJumpInput;
            _subscribedInputManager.OnSprint -= HandleSprintStarted;
            _subscribedInputManager.OnSprintCanceled -= HandleSprintCanceled;
            _subscribedInputManager = null;
        }

        private void RefreshInputManagerBinding()
        {
            InputManager currentManager = InputManager.Instance;
            if (ReferenceEquals(_inputManager, currentManager) &&
                ReferenceEquals(_subscribedInputManager, currentManager))
            {
                return;
            }

            UnsubscribeFromInput();
            _inputManager = currentManager;
            SubscribeToInput();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SPRINT EVENTS (for CameraJuiceSystem integration)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public event System.Action OnSprintStarted;
        public event System.Action OnSprintEnded;

        private void HandleJumpInput()
        {
            _jumpRequested = true;
            _jumpBufferTimer = jumpBufferTime;
        }

        private void HandleSprintStarted()
        {
            _isSprinting = true;
            OnSprintStarted?.Invoke();
        }

        private void HandleSprintCanceled()
        {
            _isSprinting = false;
            OnSprintEnded?.Invoke();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  Tick â€” INPUT + CAMERA (render framerate)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void Tick(float deltaTime)
        {
            SuitData suit = currentSuitData;
            if (suit == null) return;

            EnsureJuiceProcessor();

            RefreshInputManagerBinding();

            if (_swimPresentationController == null)
                TryGetComponent(out _swimPresentationController);

            _debugHasSwimPresentationController = _swimPresentationController != null;
            UpdateRequestedTransportCollisionProfile(ResolveActiveTransportPreset());
            if (_activeSonarPingCooldownTimer > 0f)
            {
                _activeSonarPingCooldownTimer -= deltaTime;
                if (_activeSonarPingCooldownTimer < 0f)
                    _activeSonarPingCooldownTimer = 0f;
            }

            if (IsGameplayInputBlockedByMenu())
            {
                _inputH = 0f; _inputV = 0f; _inputVertical = 0f; _mouseXDelta = 0f;
                _isSprinting = false;
                _inputCleared = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                BuildJuiceInput(deltaTime, suit);
                _juiceOutput = _juiceProcessor.Process(in _juiceInput, suit);
                ApplyCameraState();
                return;
            }

            if (_inputCleared || Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _inputCleared = false;
            }

            if (_inputManager != null && _inputManager.IsPlayerInputEnabled)
            {
                Vector2 lookDelta = _inputManager.LookInput;
                ApplyLookInput(lookDelta);

                Vector2 moveInput = _inputManager.MoveInput;
                _inputH = moveInput.x;
                _inputV = moveInput.y;
                _inputVertical = _isWalking ? 0f : ResolveVerticalInput();
                _isSprinting = _inputManager.IsSprinting;
            }
            else
            {
                Vector2 lookDelta = ReadLookFallback();
                ApplyLookInput(lookDelta);

                Vector2 moveInput = ReadMoveFallback();
                _inputH = moveInput.x;
                _inputV = moveInput.y;
                _inputVertical = _isWalking ? 0f : ReadVerticalFallbackKeys();
                _isSprinting = IsSprintFallbackHeld();
            }

            if (_wipeoutTimer > 0f || _fatalPressureSequenceTimer > 0f)
            {
                _mouseXDelta = 0f;
                _inputH = 0f;
                _inputV = 0f;
                _inputVertical = 0f;
                _isSprinting = false;
                _jumpRequested = false;
                _jumpBufferTimer = 0f;
            }

            _velocity = _rb.linearVelocity;
            float currentSpeed = math.sqrt(
                _velocity.x * _velocity.x +
                _velocity.y * _velocity.y +
                _velocity.z * _velocity.z);
            float yawDelta = _cameraYaw - _prevYawForMomentum;

            if (_swimPresentationController != null)
            {
                _swimPresentationController.SyncFromLocomotion(deltaTime, true);
                _debugLastSwimPresentationDriveFrame = Time.frameCount;
            }

            BuildJuiceInput(deltaTime, suit);
            _juiceInput.speedDelta = currentSpeed - _prevSpeed;
            _juiceInput.yawDelta = yawDelta;
            _juiceOutput = _juiceProcessor.Process(in _juiceInput, suit);

            _prevSpeed = currentSpeed;
            _prevYawForMomentum = _cameraYaw;

            if (_juiceOutput.stepEvent)
            {
                OnFootstep?.Invoke();
                UpdateStepDiagnostics();
            }

            if (_juiceProcessor.SplashThisFrame)
            {
                OnWaterSplash?.Invoke(_juiceProcessor.SplashIntensity);
            }

            if (_juiceProcessor.SubmergeChangedThisFrame)
            {
                OnSubmergeChange?.Invoke(_juiceProcessor.IsSubmerged);
            }

            if (_juiceProcessor.ExhaleThisFrame)
            {
                OnExhale?.Invoke();
            }

            ApplyCameraState();
            UpdateDiagnostics(currentSpeed);

        }

        private void BuildJuiceInput(float deltaTime, SuitData suit)
        {
            _velocity = _rb.linearVelocity;
            _juiceInput.isWalking = _isWalking;
            _juiceInput.locomotionMode = _currentLocomotionMode;
            _juiceInput.isGrounded = _isGrounded;
            _juiceInput.hasMovementInput = _inputH != 0f || _inputV != 0f || _inputVertical != 0f;
            _juiceInput.inputH = _inputH;
            _juiceInput.mouseXDelta = _mouseXDelta;
            _juiceInput.horizontalSpeed = math.sqrt(_velocity.x * _velocity.x + _velocity.z * _velocity.z);
            _juiceInput.verticalVelocity = _velocity.y;
            _juiceInput.wasGroundedLastFrame = _wasGroundedLastFrame;
            _juiceInput.deltaTime = deltaTime;
            _juiceInput.immersionRatio = _waterImmersionRatio;

            // v7.0 additions
            _juiceInput.depth = _currentDepth;
            _juiceInput.swimSpeed = math.sqrt(
                _velocity.x * _velocity.x +
                _velocity.y * _velocity.y +
                _velocity.z * _velocity.z);
            _juiceInput.cameraPitch = _cameraPitch;
            _juiceInput.swimVerticalInput = _inputVertical;
            _juiceInput.heavyCarryLoad = ResolveHeavyCarryLoad01();
            float wipeoutTransportControl = ResolveWipeoutTransportControl01();
            _juiceInput.transportBoost01 = ResolveActiveTransportBoost01() * wipeoutTransportControl;
            _juiceInput.transportCameraMotionScale = ResolveActiveTransportCameraMotionScale() * math.lerp(0.35f, 1f, wipeoutTransportControl);

            if (_swimPresentationController != null)
            {
                _juiceInput.swimPresentationMode = _swimPresentationController.CurrentMode;
                _juiceInput.swimStrokePhase = _swimPresentationController.CurrentStrokePhase;
                _juiceInput.swimPropulsionPulse = _swimPresentationController.CurrentPropulsionPulse;
                _juiceInput.swimStrokeImpulse = _swimPresentationController.CurrentStrokePowerImpulse;
                _juiceInput.swimGuideWeight = _swimPresentationController.CurrentGuideWeight;
            }
            else
            {
                _juiceInput.swimPresentationMode = PlayerSwimPresentationMode.None;
                _juiceInput.swimStrokePhase = 0f;
                _juiceInput.swimPropulsionPulse = 0f;
                _juiceInput.swimStrokeImpulse = 0f;
                _juiceInput.swimGuideWeight = 0f;
            }
        }

        private float ResolveVerticalInput()
        {
            float inputSystemVertical = _inputManager != null ? _inputManager.VerticalMovementInput : 0f;
            if (math.abs(inputSystemVertical) > 0.01f)
                return math.clamp(inputSystemVertical, -1f, 1f);

            return ReadVerticalFallbackKeys();
        }

        private float ReadVerticalFallbackKeys()
        {
            bool ascend =
                KeyHeld(controlScheme != null ? controlScheme.swimAscendPrimary : KeyCode.Space) ||
                KeyHeld(controlScheme != null ? controlScheme.swimAscendAlternate : KeyCode.None);

            bool descend =
                KeyHeld(controlScheme != null ? controlScheme.swimDescendPrimary : KeyCode.C) ||
                KeyHeld(controlScheme != null ? controlScheme.swimDescendAlternate : KeyCode.C) ||
                KeyHeld(controlScheme != null ? controlScheme.swimDescendLegacy : KeyCode.Q);

            if (ascend == descend)
                return 0f;

            return ascend ? 1f : -1f;
        }

        private static Vector2 ReadMoveFallback()
        {
            if (Keyboard.current == null)
                return Vector2.zero;

            float horizontal = 0f;
            float vertical = 0f;

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                horizontal -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                horizontal += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                vertical -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                vertical += 1f;

            return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
        }

        private static Vector2 ReadLookFallback()
        {
            return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        }

        private void ApplyLookInput(Vector2 lookDelta)
        {
            float squeeze01 = ResolveFatalPressureSqueeze01();
            float lookSensitivityScale = math.lerp(1f, fatalPressureLookSensitivityFloor, squeeze01);
            float scaledLookX = lookDelta.x * mouseSensitivity * lookSensitivityScale;
            float scaledLookY = lookDelta.y * mouseSensitivity * lookSensitivityScale;

            _mouseXDelta = lookDelta.x * lookSensitivityScale;
            _cameraYaw += scaledLookX;
            _cameraPitch -= scaledLookY;
            ApplyFatalPressureLookClamp(squeeze01);
        }

        private float ResolveFatalPressureSqueeze01()
        {
            if (_fatalPressureSequenceTimer <= 0f)
                return 0f;

            return math.saturate(_fatalPressureSequenceIntensity);
        }

        private void ApplyFatalPressureLookClamp(float squeeze01)
        {
            if (squeeze01 <= 0f)
            {
                _cameraPitch = math.clamp(_cameraPitch, pitchMin, pitchMax);
                return;
            }

            float yawFreedom = math.lerp(fatalPressureYawFreedomStart, fatalPressureYawFreedomEnd, squeeze01);
            float pitchFreedom = math.lerp(fatalPressurePitchFreedomStart, fatalPressurePitchFreedomEnd, squeeze01);
            _cameraYaw = math.clamp(_cameraYaw, _fatalPressureLookYawAnchor - yawFreedom, _fatalPressureLookYawAnchor + yawFreedom);
            _cameraPitch = math.clamp(
                _cameraPitch,
                math.max(pitchMin, _fatalPressureLookPitchAnchor - pitchFreedom),
                math.min(pitchMax, _fatalPressureLookPitchAnchor + pitchFreedom));
        }

        private static bool IsSprintFallbackHeld()
        {
            return KeyHeld(KeyCode.LeftControl) ||
                   KeyHeld(KeyCode.RightControl) ||
                   KeyHeld(KeyCode.LeftShift) ||
                   KeyHeld(KeyCode.RightShift);
        }

        private static bool KeyHeld(KeyCode key)
        {
            if (key == KeyCode.None || Keyboard.current == null)
                return false;

            return key switch
            {
                KeyCode.Space => Keyboard.current.spaceKey.isPressed,
                KeyCode.LeftControl => Keyboard.current.leftCtrlKey.isPressed,
                KeyCode.RightControl => Keyboard.current.rightCtrlKey.isPressed,
                KeyCode.C => Keyboard.current.cKey.isPressed,
                KeyCode.Q => Keyboard.current.qKey.isPressed,
                KeyCode.E => Keyboard.current.eKey.isPressed,
                KeyCode.LeftShift => Keyboard.current.leftShiftKey.isPressed,
                KeyCode.RightShift => Keyboard.current.rightShiftKey.isPressed,
                _ => false,
            };
        }

        private void ApplyCameraState()
        {
            if (playerCamera == null) return;

            // v7.0a: direct camera pitch â€” pitch inertia removed (caused reverse jerk)
            float sargassumPitchOffset = _sargassumMovementInfluence != null ? _sargassumMovementInfluence.CameraPitchOffset : 0f;
            float sargassumRollOffset = _sargassumMovementInfluence != null ? _sargassumMovementInfluence.CameraRollOffset : 0f;
            Vector3 sargassumLocalOffset = _sargassumMovementInfluence != null ? _sargassumMovementInfluence.CameraLocalOffset : Vector3.zero;
            float finalPitch = _cameraPitch + _juiceOutput.pitchOffset + sargassumPitchOffset + _heavyTowCameraPitchOffset;
            finalPitch = math.clamp(finalPitch, pitchMin - 5f, pitchMax + 5f);
            float finalRoll = _juiceOutput.rollOffset + sargassumRollOffset + _heavyTowCameraRollOffset;

            _cameraWorldRotation = Quaternion.Euler(finalPitch, _cameraYaw, finalRoll);
            playerCamera.rotation = _cameraWorldRotation;

            Vector3 finalPos;
            finalPos.x = _cameraBaseLocalPos.x + _juiceOutput.localPositionOffset.x + sargassumLocalOffset.x + _heavyTowCameraLocalOffset.x;
            finalPos.y = _cameraBaseLocalPos.y + _juiceOutput.localPositionOffset.y + sargassumLocalOffset.y + _heavyTowCameraLocalOffset.y;
            finalPos.z = _cameraBaseLocalPos.z + _juiceOutput.localPositionOffset.z + sargassumLocalOffset.z + _heavyTowCameraLocalOffset.z;
            playerCamera.localPosition = finalPos;

            // FOV compression
            if (_cameraComponent != null)
            {
                float targetFov = baseFov + _juiceOutput.fovOffset;
                float pressureSqueeze01 = ResolveFatalPressureSqueeze01();
                if (pressureSqueeze01 > 0f)
                    targetFov = math.lerp(targetFov, fatalPressureMinFov, pressureSqueeze01);
                _cameraComponent.fieldOfView = math.lerp(
                    _cameraComponent.fieldOfView, targetFov,
                    1f - math.exp(-8f * _juiceInput.deltaTime));
            }
        }

        private static bool IsGameplayInputBlockedByMenu()
        {
            return HectonFabricatorUI.IsMenuOpen || PlayerPDA.IsOpen || PauseMenuController.IsAnyOpen;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  FixedTick â€” PHYSICS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void FixedTick(float fixedDeltaTime)
        {
            SuitData suit = currentSuitData;
            if (suit == null) return;

            if (_transportBailoutCooldownTimer > 0f)
            {
                _transportBailoutCooldownTimer -= fixedDeltaTime;
                if (_transportBailoutCooldownTimer < 0f)
                    _transportBailoutCooldownTimer = 0f;
            }

            EnsureJuiceProcessor();
            _currentFixedDeltaTime = fixedDeltaTime;
            PlayerTransportPreset activeTransportPreset = ResolveActiveTransportPreset();
            float previousWaterImmersionRatio = _waterImmersionRatio;
            bool wasGroundedLastFixedTick = _isGrounded;
            float currentVerticalVelocity = _rb.linearVelocity.y;

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  1. FORCE-OVERRIDE: BuoyancyObject-proof
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            _rb.useGravity = false;

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  2. BODY YAW SPRING
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            if (_isWalking)
            {
                _bodyYaw = _cameraYaw;
                _bodyYawVelocity = 0f;
            }
            else
            {
                float bodyYawOmega = suit.bodyYawSpringOmega *
                    ResolveHeavyCarryBodyYawSpringMultiplier() *
                    ResolveTransportBodyYawResponsivenessScale(activeTransportPreset) *
                    ResolveHullStressTurnResponsivenessScale(activeTransportPreset);
                _bodyYaw = SpringDamp(_bodyYaw, _cameraYaw, ref _bodyYawVelocity,
                    bodyYawOmega, fixedDeltaTime);
            }

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  3. PRE-IMPACT VELOCITY TRACKING
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            _juiceProcessor.TrackVerticalVelocity(_rb.linearVelocity.y);
            _wasGroundedLastFrame = _isGrounded;

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  4. GROUND CHECK
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            GroundCheck();

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  5. CREST HEIGHT + WATER IMMERSION + DEPTH
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            UpdateOceanWaterHeight();
            _waterImmersionRatio = ComputeImmersionRatio();
            _currentDepth = ComputeDepth();
            UpdateBottomClearance();

            if (IsInDryInterior())
            {
                _waterImmersionRatio = 0f;
                _smoothedImmersionRatio = 0f;
                _currentDepth = 0f;
            }

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  6. SMOOTHED IMMERSION + GROUNDED OVERRIDE
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            if (_waterImmersionRatio > _smoothedImmersionRatio)
            {
                float enterT = 1f - math.exp(-12f * fixedDeltaTime);
                _smoothedImmersionRatio = math.lerp(_smoothedImmersionRatio, _waterImmersionRatio, enterT);
            }
            else
            {
                float exitT = 1f - math.exp(-3f * fixedDeltaTime);
                _smoothedImmersionRatio = math.lerp(_smoothedImmersionRatio, _waterImmersionRatio, exitT);
            }

            float physicsImmersion = _smoothedImmersionRatio;
            float feetDepth = GetFeetDepthBelowSurface(EffectiveWaterSurfaceY);
            bool hasImmediateShoreFooting = _isGrounded && feetDepth <= shoreWalkFootDepth;
            bool isShallowEnoughForShore = physicsImmersion < swimTransitionThreshold || hasImmediateShoreFooting;
            bool isDryLand = physicsImmersion <= 0.01f;
            if (_isGrounded && isDryLand)
            {
                _dryGroundGraceTimer = dryGroundGraceTime;
            }
            else if (_dryGroundGraceTimer > 0f)
            {
                _dryGroundGraceTimer -= fixedDeltaTime;
                if (_dryGroundGraceTimer < 0f)
                    _dryGroundGraceTimer = 0f;
            }

            if (_isGrounded && isShallowEnoughForShore)
            {
                _shoreGroundGraceTimer = shoreGroundGraceTime;
            }
            else if (_shoreGroundGraceTimer > 0f)
            {
                _shoreGroundGraceTimer -= fixedDeltaTime;
                if (_shoreGroundGraceTimer < 0f)
                    _shoreGroundGraceTimer = 0f;
            }

            if (_jumpBufferTimer > 0f)
            {
                _jumpBufferTimer -= fixedDeltaTime;
                if (_jumpBufferTimer <= 0f)
                {
                    _jumpBufferTimer = 0f;
                    _jumpRequested = false;
                }
            }

            if (_surfaceBreachLockTimer > 0f)
            {
                _surfaceBreachLockTimer -= fixedDeltaTime;
                if (_surfaceBreachLockTimer < 0f)
                    _surfaceBreachLockTimer = 0f;
            }

            if (_surfaceDiveAssistTimer > 0f)
            {
                _surfaceDiveAssistTimer -= fixedDeltaTime;
                if (_surfaceDiveAssistTimer < 0f)
                    _surfaceDiveAssistTimer = 0f;
            }

            if (_waterEntryImpactTimer > 0f)
            {
                _waterEntryImpactTimer -= fixedDeltaTime;
                if (_waterEntryImpactTimer <= 0f)
                {
                    _waterEntryImpactTimer = 0f;
                    _waterEntryImpactStrength = 0f;
                }
            }

            if (_stepAssistCooldownTimer > 0f)
            {
                _stepAssistCooldownTimer -= fixedDeltaTime;
                if (_stepAssistCooldownTimer < 0f)
                    _stepAssistCooldownTimer = 0f;
            }

            if (_recentBreachExitTimer > 0f)
            {
                _recentBreachExitTimer -= fixedDeltaTime;
                if (_recentBreachExitTimer < 0f)
                    _recentBreachExitTimer = 0f;
            }

            if (_surfaceGaspCooldownTimer > 0f)
            {
                _surfaceGaspCooldownTimer -= fixedDeltaTime;
                if (_surfaceGaspCooldownTimer < 0f)
                    _surfaceGaspCooldownTimer = 0f;
            }

            if (_fatalPressureSequenceTimer > 0f)
            {
                _fatalPressureSequenceTimer -= fixedDeltaTime;
                float duration = math.max(fatalPressureSequenceDuration, 0.01f);
                _fatalPressureSequenceIntensity = math.saturate(1f - (_fatalPressureSequenceTimer / duration));
                ApplyFatalPressureVisorCorruption(ResolveFatalPressureCorruptionIntensity(_fatalPressureSequenceIntensity));
                _fatalPressureSequenceGlitchPulseTimer -= fixedDeltaTime;
                if (_fatalPressureSequenceGlitchPulseTimer <= 0f)
                {
                    PulseFatalPressureGlitch(_fatalPressureSequenceIntensity);
                    _fatalPressureSequenceGlitchPulseTimer = math.lerp(
                        fatalPressureGlitchPulseIntervalMax,
                        fatalPressureGlitchPulseIntervalMin,
                        _fatalPressureSequenceIntensity);
                }

                if (_juiceProcessor != null)
                    _juiceProcessor.RegisterEntanglementStrain(math.lerp(0.22f, 0.8f, _fatalPressureSequenceIntensity));

                OnFatalPressureSequence?.Invoke(_fatalPressureSequenceIntensity);

                if (_fatalPressureSequenceTimer <= 0f)
                {
            _fatalPressureSequenceTimer = 0f;
            _fatalPressureSequenceGlitchPulseTimer = 0f;
            _fatalPressureSequenceIntensity = 0f;
            _fatalPressureLookYawAnchor = _cameraYaw;
            _fatalPressureLookPitchAnchor = _cameraPitch;
                    TriggerFatalPressureImplosion();
                }
            }

            if (_wipeoutTimer > 0f)
            {
                _wipeoutTimer -= fixedDeltaTime;
                if (_wipeoutTimer <= 0f)
                {
                    _wipeoutTimer = 0f;
                    _wipeoutSeverity = 0f;
                }
            }

            UpdateShoreBuoyancyBlend(fixedDeltaTime, physicsImmersion, feetDepth);
            UpdateVegetationDensityLinearDamping(fixedDeltaTime);

            bool hasDryGroundSupport = _isGrounded || (_dryGroundGraceTimer > 0f && isDryLand);
            bool hasShoreGroundSupport = hasImmediateShoreFooting || (_shoreGroundGraceTimer > 0f && isShallowEnoughForShore);
            bool groundedOnDryLand = hasDryGroundSupport && isDryLand;
            bool groundedOnShore = hasShoreGroundSupport && isShallowEnoughForShore;
            bool exosuitActive = IsExosuitTransportActive();
            RefreshSurfaceBreachLock(physicsImmersion);
            UpdateSurfaceDiveCommitTimer(fixedDeltaTime, activeTransportPreset);

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  7A. GRADUATED GRAVITY
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            if (exosuitActive || groundedOnShore)
            {
                _gravityScale = exosuitActive ? exosuitNegativeBuoyancyScale : 1f;
            }
            else
            {
                _gravityScale = 1f - math.saturate(physicsImmersion * gravityFadeRate);
            }

            if (_gravityScale > 0.001f)
            {
                float mass = _rb.mass;
                _forceVector.x = _cachedGravity.x * mass * _gravityScale;
                _forceVector.y = _cachedGravity.y * mass * _gravityScale;
                _forceVector.z = _cachedGravity.z * mass * _gravityScale;
                _rb.AddForce(_forceVector, ForceMode.Force);
            }

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  7B. GROUND SNAP SCALE
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            if ((exosuitActive && _isGrounded) || groundedOnShore)
            {
                _snapScale = 1f;
            }
            else
            {
                _snapScale = 1f - math.saturate(physicsImmersion * snapFadeRate);
            }

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  8. MODE DETECTION
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            bool shouldWalk = ShouldUseLandLocomotion(physicsImmersion, hasShoreGroundSupport, hasImmediateShoreFooting);
            bool shouldStartSurfaceDiveAssist =
                !shouldWalk &&
                !IsInDryInterior() &&
                physicsImmersion >= surfaceBreachMinImmersion &&
                GetHeadDepthBelowSurface(EffectiveWaterSurfaceY) <= surfaceDiveBreakDepth &&
                HasCommittedSurfaceDive(activeTransportPreset);

            if (shouldWalk)
            {
                _surfaceDiveAssistTimer = 0f;
            }
            else if (shouldStartSurfaceDiveAssist && _surfaceDiveAssistTimer <= 0f)
            {
                _surfaceDiveAssistTimer = surfaceDiveAssistDuration;
            }

            if (shouldWalk != _isWalking)
            {
                _isWalking = shouldWalk;
                ApplyModePhysics(suit);
                UpdateModeDiagnostics();
            }

            _isSurfaceSwimming = !exosuitActive && !_isWalking && ResolveSurfaceSwimState(physicsImmersion, activeTransportPreset);
            _currentLocomotionMode = ResolveLocomotionMode(physicsImmersion);
            UpdateSurfaceLockState(fixedDeltaTime);
            UpdateWaterPresentationPose(fixedDeltaTime);
            UpdateDynamicCollisionProfile(fixedDeltaTime);
            UpdateHeavyTowRuntimeResponse(fixedDeltaTime);
            UpdateWetLensSignal(fixedDeltaTime);
            UpdateHeadSurfaceRecovery(fixedDeltaTime);
            UpdateTransportCriticalBailout();
            UpdateModeDiagnostics();
            TryStartWaterEntryImpact(previousWaterImmersionRatio, wasGroundedLastFixedTick, currentVerticalVelocity);
            TryPlaySurfacePierceSplashAudio(previousWaterImmersionRatio, currentVerticalVelocity);

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  9. DAMPING TRANSITION
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            SmoothDampingTransition(fixedDeltaTime, suit);

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  10. JUMP
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            if (_jumpRequested)
            {
                if (!exosuitActive && (groundedOnDryLand || groundedOnShore) && _jumpBufferTimer > 0f)
                {
                    if (TryApplyJumpImpulse(suit.jumpImpulse))
                    {
                        ConsumeJumpRequest();
                        _dryGroundGraceTimer = 0f;
                        _shoreGroundGraceTimer = 0f;
                        _surfaceBreachLockTimer = 0f;
                    }
                }
            }

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  11. MOVEMENT + FORCES
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            if (_isWalking)
            {
                bool hasLandInput = _inputH != 0f || _inputV != 0f;
                ApplyEnvironmentalDrag(1f);
                AdvanceExternalEnvironmentalDrag(fixedDeltaTime);
                AdvanceParasiteLatchInfluence(fixedDeltaTime);
                _sargassumFieldDensity01 = 0f;
                UpdateSargassumMatBuoyancyBlend(fixedDeltaTime);
                WalkPhysics(suit, fixedDeltaTime);
                ApplyCuttingTensionPhysics(fixedDeltaTime);
                ApplyExosuitJumpJets(fixedDeltaTime);
                if (!exosuitActive)
                    CoolExosuitJumpJets(fixedDeltaTime);

                if (hasLandInput)
                    TryApplyStepAssist(groundedOnDryLand, groundedOnShore);

                if (_isGrounded && _snapScale > 0.001f)
                    ApplyGroundStability(_snapScale);
            }
            else
            {
                CoolExosuitJumpJets(fixedDeltaTime);
                AdvanceCuttingTensionRequest(fixedDeltaTime);
                _currentTimer += fixedDeltaTime;
                if (_currentTimer > 100000f) _currentTimer -= 100000f;
                AdvanceSargassumInfluence(fixedDeltaTime, activeTransportPreset);
                AdvanceAbyssalThermalInfluence(fixedDeltaTime, activeTransportPreset);
                AdvanceExternalEnvironmentalDrag(fixedDeltaTime);
                AdvanceParasiteLatchInfluence(fixedDeltaTime);
                SwimPhysics(suit, fixedDeltaTime, activeTransportPreset);

                if (_surfaceLockBlend > 0.001f)
                    ApplySurfaceLock(suit, activeTransportPreset);

                if (_waterImmersionRatio > 0.3f)
                    ApplyAmbientCurrent(suit, fixedDeltaTime, activeTransportPreset);

                ApplyUnderwaterTurbulence(fixedDeltaTime, activeTransportPreset);
                ApplyAbyssalCurrents(fixedDeltaTime, activeTransportPreset);
                ApplyThermalUpdrafts(fixedDeltaTime, activeTransportPreset);
                ApplyParasiteLatchForces(fixedDeltaTime);
                UpdateHullStress(fixedDeltaTime, activeTransportPreset);
            }

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  12. VELOCITY CLAMP
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            if (_isWalking)
            {
                _abyssalThermalFlowSample = default;
                _abyssalThermalFlowSample.DragMultiplier = 1f;
                _abyssalThermalFlowVelocityWS = Vector3.zero;
                _thermalUpdraftIntensity = 0f;
                _thermalUpdraftVelocityChange = Vector3.zero;
                _externalThermalUpdraftVelocityChange = Vector3.zero;
                _externalThermalUpdraftRequestedThisStep = false;
                UpdateHullStress(fixedDeltaTime, activeTransportPreset);
            }

            if (_waterImmersionRatio > 0.02f)
                ApplyShoreUndertow(physicsImmersion, activeTransportPreset);
            else
            {
                _undertowVector = Vector3.zero;
                _undertowIntensity = 0f;
            }

            ApplyWipeoutRecoveryForces(fixedDeltaTime);
            ClampVelocity(suit);
            UpdateGroundDiagnostics();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  WATER IMMERSION + DEPTH
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private float ComputeImmersionRatio()
        {
            float surfaceY = EffectiveWaterSurfaceY;
            float feetY = GetBodyBottomY();
            float headY = GetBodyTopY();

            if (feetY >= surfaceY) return 0f;
            if (headY <= surfaceY) return 1f;

            return math.clamp((surfaceY - feetY) / playerHeight, 0f, 1f);
        }

        /// <summary>
        /// Depth in meters below water surface. 0 = at surface. Positive = deeper.
        /// Returns 0 if above water.
        /// </summary>
        private float ComputeDepth()
        {
            float surfaceY = EffectiveWaterSurfaceY;
            float eyeY = GetBodyEyeY();
            float depth = surfaceY - eyeY;
            return depth > 0f ? depth : 0f;
        }

        private bool IsInDryInterior()
        {
            return _buoyancy != null && _buoyancy.IsInDryZone;
        }

        private bool ResolveSurfaceSwimState(float physicsImmersion, PlayerTransportPreset transportPreset)
        {
            if (_isWalking || IsInDryInterior())
                return false;

            float surfaceY = EffectiveWaterSurfaceY;
            float headSurfaceOffset = GetHeadSurfaceOffset(surfaceY);
            float headDepth = headSurfaceOffset > 0f ? headSurfaceOffset : 0f;
            bool headTouchingSurface = headSurfaceOffset >= 0f && headSurfaceOffset <= surfaceHeadReattachDepth;
            bool deliberateDive = HasCommittedSurfaceDive(transportPreset);
            bool insideSurfaceBand =
                physicsImmersion >= surfaceBreachMinImmersion &&
                (_currentDepth <= surfaceSwimDepthBand || headDepth <= surfaceDiveBreakDepth);

            if (_surfaceBreachLockTimer > 0f)
                return false;

            if (headTouchingSurface)
                return true;

            if (_surfaceDiveAssistTimer > 0f && !_isSurfaceSwimming)
                return false;

            if (_isSurfaceSwimming)
            {
                if (deliberateDive && headDepth >= surfaceDiveBreakDepth)
                    return false;

                return insideSurfaceBand;
            }

            if (!insideSurfaceBand)
                return false;

            return !deliberateDive;
        }

        private PlayerLocomotionMode ResolveLocomotionMode(float physicsImmersion)
        {
            if (IsInDryInterior())
                return PlayerLocomotionMode.DryInteriorWalk;

            if (IsExosuitTransportActive())
                return PlayerLocomotionMode.ExosuitLocomotion;

            if (_isWalking)
            {
                if (physicsImmersion > 0.01f)
                    return PlayerLocomotionMode.ShallowWadeWalk;

                return PlayerLocomotionMode.DryGroundWalk;
            }

            return _isSurfaceSwimming
                ? PlayerLocomotionMode.SurfaceSwim
                : PlayerLocomotionMode.UnderwaterSwim;
        }

        private bool HasSurfaceDiveIntent(PlayerTransportPreset transportPreset)
        {
            if (ResolveTransportSurfaceDiveAssistScale(transportPreset) <= 0f)
                return false;

            float requiredDiveAngle = math.max(surfaceDivePitchCommit, 30f);
            if (ResolveCameraLookDownAngle() < requiredDiveAngle)
                return false;

            return _inputV > surfaceDiveForwardCommit;
        }

        private bool HasCommittedSurfaceDive(PlayerTransportPreset transportPreset)
        {
            if (!HasSurfaceDiveIntent(transportPreset))
                return false;

            if (surfaceDiveCommitHoldTime <= 0f)
                return true;

            return _surfaceDiveCommitTimer >= surfaceDiveCommitHoldTime;
        }

        private void UpdateSurfaceDiveCommitTimer(float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            bool canCommitDive =
                !_isWalking &&
                !IsInDryInterior() &&
                _waterImmersionRatio > 0.01f;

            bool hasDiveIntent = canCommitDive && HasSurfaceDiveIntent(transportPreset);
            if (hasDiveIntent)
            {
                if (surfaceDiveCommitHoldTime <= 0f)
                {
                    _surfaceDiveCommitTimer = 0f;
                    return;
                }

                _surfaceDiveCommitTimer += fixedDeltaTime;
                if (_surfaceDiveCommitTimer > surfaceDiveCommitHoldTime)
                    _surfaceDiveCommitTimer = surfaceDiveCommitHoldTime;
                return;
            }

            if (_surfaceDiveCommitTimer <= 0f)
                return;

            _surfaceDiveCommitTimer -= fixedDeltaTime * 2f;
            if (_surfaceDiveCommitTimer < 0f)
                _surfaceDiveCommitTimer = 0f;
        }

        private void UpdateSurfaceLockState(float fixedDeltaTime)
        {
            if (_surfaceBreachLockTimer > 0f)
            {
                _surfaceLockBlend = 0f;
                _surfaceLockTargetY = _rb.position.y;
                return;
            }

            float targetBlend = _isSurfaceSwimming ? _shoreBuoyancyBlend : 0f;
            float blendSpeed = targetBlend > _surfaceLockBlend
                ? surfaceSnapEngageSpeed
                : surfaceSnapReleaseSpeed;
            float blendT = 1f - math.exp(-math.max(blendSpeed, 0.01f) * fixedDeltaTime);
            _surfaceLockBlend = math.lerp(_surfaceLockBlend, targetBlend, blendT);

            if (_isSurfaceSwimming)
            {
                float targetRootY = EffectiveWaterSurfaceY + surfaceStickOffset;
                float shorelineTargetRootY = math.lerp(_rb.position.y, targetRootY, _shoreBuoyancyBlend);
                float followT = 1f - math.exp(-math.max(surfaceWaveFollowSharpness, 0.01f) * fixedDeltaTime);
                _surfaceLockTargetY = math.lerp(_surfaceLockTargetY, shorelineTargetRootY, followT);
                return;
            }

            if (_surfaceLockBlend <= 0.001f)
            {
                _surfaceLockBlend = 0f;
                _surfaceLockTargetY = _rb.position.y;
            }
        }

        private void UpdateWaterPresentationPose(float fixedDeltaTime)
        {
            Quaternion targetSurfacePose = Quaternion.identity;
            if (_isSurfaceSwimming && _crestSamplingSucceeded && _surfaceLockBlend > 0.001f)
            {
                float yawRad = _bodyYaw * DEG_TO_RAD;
                Vector3 bodyForward = new Vector3(math.sin(yawRad), 0f, math.cos(yawRad));
                Vector3 desiredForward = Vector3.ProjectOnPlane(bodyForward, EffectiveWaterSurfaceNormal);
                if (desiredForward.sqrMagnitude <= 0.0001f)
                    desiredForward = bodyForward;
                else
                    desiredForward.Normalize();

                Quaternion yawRotation = Quaternion.Euler(0f, _bodyYaw, 0f);
                Quaternion waveRotation = Quaternion.LookRotation(desiredForward, EffectiveWaterSurfaceNormal);
                Quaternion localDelta = Quaternion.Inverse(yawRotation) * waveRotation;
                Vector3 localEuler = localDelta.eulerAngles;
                float pitch = math.clamp(NormalizeSignedAngle(localEuler.x), -surfaceWaveMaxPitch, surfaceWaveMaxPitch) * _shoreBuoyancyBlend;
                float roll = math.clamp(NormalizeSignedAngle(localEuler.z), -surfaceWaveMaxRoll, surfaceWaveMaxRoll) * _shoreBuoyancyBlend;
                targetSurfacePose = Quaternion.Euler(pitch, 0f, roll);
            }

            float surfaceBlendT = 1f - math.exp(-math.max(surfaceWaveAlignmentSharpness, 0.01f) * fixedDeltaTime);
            _surfaceWavePoseRotation = Quaternion.Slerp(_surfaceWavePoseRotation, targetSurfacePose, surfaceBlendT);
        }

        private void UpdateDynamicCollisionProfile(float fixedDeltaTime)
        {
            float targetTuck = 0f;
            if (_isSurfaceSwimming && _crestSamplingSucceeded && _surfaceLockBlend > 0.001f)
            {
                float downhillSlopeT = math.saturate(-_dynamicWaveLocalSlope.y / math.max(dynamicCollisionTuckSlopeForFull, 0.0001f));
                float descentPosePitch = -NormalizeSignedAngle(_surfaceWavePoseRotation.eulerAngles.x);
                float descentPoseT = math.saturate(descentPosePitch / math.max(surfaceWaveMaxPitch, 0.01f));
                float immersionDepthT = math.saturate(_currentDepth / math.max(dynamicCollisionImmersionDepthForFull, 0.01f));
                targetTuck = math.max(downhillSlopeT, descentPoseT) * immersionDepthT * _surfaceLockBlend * _shoreBuoyancyBlend;
            }

            if (_physicalTraumaCollisionHoldTimer > 0f)
            {
                _physicalTraumaCollisionHoldTimer -= fixedDeltaTime;
                if (_physicalTraumaCollisionHoldTimer < 0f)
                    _physicalTraumaCollisionHoldTimer = 0f;
            }

            float blendT = 1f - math.exp(-math.max(dynamicCollisionDeformationBlendSharpness, 0.01f) * fixedDeltaTime);
            _dynamicCollisionTuck01 = math.lerp(_dynamicCollisionTuck01, targetTuck, blendT);

            float traumaCollisionTarget = _physicalTraumaCollisionHoldTimer > 0f ? _physicalTraumaCollisionWeight : 0f;
            float traumaBlendT = 1f - math.exp(-math.max(physicalTraumaCollisionRecoverySharpness, 0.01f) * fixedDeltaTime);
            _physicalTraumaCollisionWeight = math.lerp(_physicalTraumaCollisionWeight, traumaCollisionTarget, traumaBlendT);

            float waveCollisionHeightScale = _requestedTransportCollisionHeightScale * math.lerp(1f, dynamicCollisionMinHeightScale, _dynamicCollisionTuck01);
            float waveCollisionRadiusScale = _requestedTransportCollisionRadiusScale * math.lerp(1f, dynamicCollisionMaxRadiusScale, _dynamicCollisionTuck01);
            float waveCollisionCenterYOffset = _requestedTransportCollisionCenterYOffset + math.lerp(0f, dynamicCollisionCenterYOffset, _dynamicCollisionTuck01);
            float traumaCollisionHeightScale = math.lerp(1f, physicalTraumaCollisionHeightScale, _physicalTraumaCollisionWeight);
            float traumaCollisionRadiusScale = math.lerp(1f, physicalTraumaCollisionRadiusScale, _physicalTraumaCollisionWeight);
            float traumaCollisionCenterYOffset = math.lerp(0f, physicalTraumaCollisionCenterYOffset, _physicalTraumaCollisionWeight);
            float collisionHeightScale = waveCollisionHeightScale * traumaCollisionHeightScale;
            float collisionRadiusScale = waveCollisionRadiusScale * traumaCollisionRadiusScale;
            float collisionCenterYOffset = waveCollisionCenterYOffset + traumaCollisionCenterYOffset;
            ApplyResolvedCollisionProfile(collisionRadiusScale, collisionHeightScale, collisionCenterYOffset);
        }

        private void ApplyUnderwaterTurbulence(float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            Quaternion targetTurbulencePose = Quaternion.identity;
            if (_isWalking || _isSurfaceSwimming || IsInDryInterior() || !_crestSamplingSucceeded)
            {
                UpdateUnderwaterStressSignal(0f, fixedDeltaTime);
                float fadeT = 1f - math.exp(-math.max(underwaterTurbulencePoseSharpness, 0.01f) * fixedDeltaTime);
                _underwaterTurbulencePoseRotation = Quaternion.Slerp(_underwaterTurbulencePoseRotation, targetTurbulencePose, fadeT);
                return;
            }

            if (_currentDepth <= 0f || _currentDepth > underwaterTurbulenceMaxDepth)
            {
                UpdateUnderwaterStressSignal(0f, fixedDeltaTime);
                float fadeT = 1f - math.exp(-math.max(underwaterTurbulencePoseSharpness, 0.01f) * fixedDeltaTime);
                _underwaterTurbulencePoseRotation = Quaternion.Slerp(_underwaterTurbulencePoseRotation, targetTurbulencePose, fadeT);
                return;
            }

            float environmentalInfluenceScale = ResolveTransportAmbientCurrentInfluenceScale(transportPreset);
            float depthFade = 1f - math.saturate(_currentDepth / math.max(underwaterTurbulenceMaxDepth, 0.01f));
            float bottomBoost = 1f;
            if (!float.IsPositiveInfinity(_bottomClearance))
            {
                float bottomT = 1f - math.saturate(_bottomClearance / math.max(underwaterTurbulenceBottomInfluenceDepth, 0.01f));
                bottomBoost = math.lerp(1f, underwaterTurbulenceBottomBoost, bottomT);
            }

            float turbulenceIntensity = _dynamicStormIntensity * depthFade * environmentalInfluenceScale * bottomBoost;
            float stressDenominator = math.max(1f - underwaterStressSignalThreshold, 0.0001f);
            float stressTarget = math.saturate((turbulenceIntensity - underwaterStressSignalThreshold) / stressDenominator);
            UpdateUnderwaterStressSignal(stressTarget, fixedDeltaTime);
            if (turbulenceIntensity > 0.001f)
            {
                Vector3 horizontalWaveVelocity = _dynamicAverageWaterVelocity;
                horizontalWaveVelocity.y = 0f;
                Vector3 horizontalDisplacement = _dynamicAverageWaterDisplacement;
                horizontalDisplacement.y = 0f;
                Vector3 horizontalWaveVector = horizontalWaveVelocity + horizontalDisplacement * underwaterTurbulenceFrequency + EffectiveWaterFlowVelocity * 0.55f;
                if (horizontalWaveVector.sqrMagnitude <= 0.0001f)
                {
                    float yawRad = _bodyYaw * DEG_TO_RAD;
                    horizontalWaveVector.x = math.sin(yawRad);
                    horizontalWaveVector.z = math.cos(yawRad);
                }
                else
                {
                    horizontalWaveVector.Normalize();
                }

                Vector3 crossWave = new Vector3(-horizontalWaveVector.z, 0f, horizontalWaveVector.x);
                float turbulencePhase = _currentTimer * underwaterTurbulenceFrequency;
                float lateralOscillation = math.sin(turbulencePhase * TwoPi + _dynamicAverageWaterDisplacement.x * 1.65f + _dynamicAverageWaterVelocity.z * 0.3f);
                float verticalOscillation = math.cos(turbulencePhase * TwoPi * 1.37f + _dynamicAverageWaterDisplacement.z * 1.1f - _dynamicAverageWaterVelocity.x * 0.45f);
                float undertowOscillation = math.sin(turbulencePhase * TwoPi * 0.53f + _dynamicWaveHeightSpan * 1.6f);

                float lateralForce = underwaterTurbulenceForce * turbulenceIntensity * _rb.mass;
                float verticalForce = underwaterTurbulenceVerticalForce * turbulenceIntensity * _rb.mass;
                _forceVector.x = (crossWave.x * lateralOscillation - horizontalWaveVector.x * undertowOscillation * 0.65f) * lateralForce;
                _forceVector.y = (-math.abs(undertowOscillation) * 0.55f + verticalOscillation * 0.45f) * verticalForce;
                _forceVector.z = (crossWave.z * lateralOscillation - horizontalWaveVector.z * undertowOscillation * 0.65f) * lateralForce;
                _rb.AddForce(_forceVector, ForceMode.Force);

                float targetPitch = math.clamp(
                    (-undertowOscillation * underwaterTurbulencePitch) + verticalOscillation * underwaterTurbulencePitch * 0.35f,
                    -underwaterTurbulencePitch,
                    underwaterTurbulencePitch) * turbulenceIntensity;
                float targetRoll = math.clamp(
                    lateralOscillation * underwaterTurbulenceRoll,
                    -underwaterTurbulenceRoll,
                    underwaterTurbulenceRoll) * turbulenceIntensity;
                targetTurbulencePose = Quaternion.Euler(targetPitch, 0f, targetRoll);
            }

            float turbulenceBlendT = 1f - math.exp(-math.max(underwaterTurbulencePoseSharpness, 0.01f) * fixedDeltaTime);
            _underwaterTurbulencePoseRotation = Quaternion.Slerp(_underwaterTurbulencePoseRotation, targetTurbulencePose, turbulenceBlendT);
        }

        private void ApplyAbyssalCurrents(float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            AdvanceAbyssalTransportTurbulenceSteering(fixedDeltaTime);

            if (_abyssalDowndraftCooldownTimer > 0f)
            {
                _abyssalDowndraftCooldownTimer -= fixedDeltaTime;
                if (_abyssalDowndraftCooldownTimer < 0f)
                    _abyssalDowndraftCooldownTimer = 0f;
            }

            if (_abyssalDowndraftActiveTimer > 0f)
            {
                _abyssalDowndraftActiveTimer -= fixedDeltaTime;
                if (_abyssalDowndraftActiveTimer < 0f)
                    _abyssalDowndraftActiveTimer = 0f;
            }

            if (_abyssalFlowNoiseBoundaryCooldownTimer > 0f)
            {
                _abyssalFlowNoiseBoundaryCooldownTimer -= fixedDeltaTime;
                if (_abyssalFlowNoiseBoundaryCooldownTimer < 0f)
                    _abyssalFlowNoiseBoundaryCooldownTimer = 0f;
            }

            if (_isWalking || _isSurfaceSwimming || IsInDryInterior() || _currentDepth < abyssalCurrentStartDepth)
            {
                _abyssalDowndraftIntensity = 0f;
                _abyssalDowndraftVelocityChange = Vector3.zero;
                _previousAbyssalNoisyFlow = Vector3.zero;
                return;
            }

            float depthT = math.saturate(
                (_currentDepth - abyssalCurrentStartDepth) /
                math.max(abyssalCurrentFullDepth - abyssalCurrentStartDepth, 0.01f));
            if (depthT <= 0f)
            {
                _abyssalDowndraftIntensity = 0f;
                _abyssalDowndraftVelocityChange = Vector3.zero;
                _previousAbyssalNoisyFlow = Vector3.zero;
                return;
            }

            Vector3 abyssalNoisyFlow = ResolveAbyssalAmbientFlowWithNoise();
            ApplyAbyssalFlowBoundaryTurbulence(abyssalNoisyFlow, depthT, transportPreset);

            if (_abyssalDowndraftCooldownTimer <= 0f)
            {
                Vector3 downdraftDirection = ResolveAbyssalDowndraftDirection(abyssalNoisyFlow);
                float velocityChangeMagnitude = math.lerp(
                    abyssalDowndraftVelocityChangeMin,
                    abyssalDowndraftVelocityChangeMax,
                    depthT);
                float transportInfluence = ResolveTransportAmbientCurrentInfluenceScale(transportPreset);
                _abyssalDowndraftVelocityChange = downdraftDirection * velocityChangeMagnitude * transportInfluence;
                _abyssalDowndraftIntensity = depthT;
                _abyssalDowndraftActiveTimer = abyssalDowndraftAftershockDuration;
                _rb.AddForce(_abyssalDowndraftVelocityChange, ForceMode.VelocityChange);
                _abyssalDowndraftCooldownTimer = ResolveAbyssalDowndraftInterval(depthT);
            }

            if (_abyssalDowndraftActiveTimer <= 0f || _survivalSystem == null)
                return;

            float upwardLookT = math.saturate((-_cameraPitch - 10f) / 30f);
            float counterIntent = math.max(math.saturate(_inputVertical), upwardLookT * math.saturate(_inputV));
            float transportCounter = math.saturate(ResolveActiveTransportBoost01()) * 0.65f;
            float drainIntent = math.max(counterIntent, transportCounter);
            if (drainIntent <= 0.001f)
                return;

            _survivalSystem.DrainEnergy(abyssalDowndraftCounterEnergyDrain * _abyssalDowndraftIntensity * drainIntent * fixedDeltaTime);
        }

        private void ApplyThermalUpdrafts(float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            if (_thermalUpdraftTraumaCooldownTimer > 0f)
            {
                _thermalUpdraftTraumaCooldownTimer -= fixedDeltaTime;
                if (_thermalUpdraftTraumaCooldownTimer < 0f)
                    _thermalUpdraftTraumaCooldownTimer = 0f;
            }

            Vector3 totalVelocityChange = Vector3.zero;
            float strongestIntensity = 0f;

            if (!_isWalking && !IsInDryInterior() && _currentDepth >= thermalUpdraftStartDepth)
            {
                Vector3 sampledCurrent = Hecton8.Physics.CurrentVolume.SampleAt(_rb.position);
                if (sampledCurrent.y > thermalUpdraftSpeedThreshold)
                {
                    float currentIntensity = math.saturate(
                        (sampledCurrent.y - thermalUpdraftSpeedThreshold) /
                        math.max(thermalUpdraftSpeedMax - thermalUpdraftSpeedThreshold, 0.01f));
                    float transportInfluence = ResolveTransportAmbientCurrentInfluenceScale(transportPreset);
                    float verticalChange = thermalUpdraftVelocityChangePerSecond * currentIntensity * transportInfluence * fixedDeltaTime;
                    totalVelocityChange.y += verticalChange;
                    strongestIntensity = math.max(strongestIntensity, currentIntensity);
                }
            }

            if (_externalThermalUpdraftRequestedThisStep && _externalThermalUpdraftVelocityChange.y > 0.0001f)
            {
                totalVelocityChange += _externalThermalUpdraftVelocityChange;
                strongestIntensity = math.max(
                    strongestIntensity,
                    math.saturate(_externalThermalUpdraftVelocityChange.magnitude / math.max(thermalUpdraftVelocityChangePerSecond, 0.01f)));
            }

            _thermalUpdraftVelocityChange = totalVelocityChange;
            _thermalUpdraftIntensity = strongestIntensity;

            if (totalVelocityChange.sqrMagnitude > 0.0001f)
            {
                _rb.AddForce(totalVelocityChange, ForceMode.VelocityChange);
                if (strongestIntensity >= thermalUpdraftTraumaThreshold && _thermalUpdraftTraumaCooldownTimer <= 0f)
                {
                    ApplyPhysicalTrauma(totalVelocityChange * _rb.mass, strongestIntensity);
                    _thermalUpdraftTraumaCooldownTimer = thermalUpdraftTraumaCooldown;
                }
            }

            _externalThermalUpdraftVelocityChange = Vector3.zero;
            _externalThermalUpdraftRequestedThisStep = false;
        }

        private void UpdateHullStress(float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            if (_hullStressHudCorruptionRefreshTimer > 0f)
            {
                _hullStressHudCorruptionRefreshTimer -= fixedDeltaTime;
                if (_hullStressHudCorruptionRefreshTimer < 0f)
                    _hullStressHudCorruptionRefreshTimer = 0f;
            }

            if (_hullStressGroanCooldownTimer > 0f)
            {
                _hullStressGroanCooldownTimer -= fixedDeltaTime;
                if (_hullStressGroanCooldownTimer < 0f)
                    _hullStressGroanCooldownTimer = 0f;
            }

            float targetStress = 0f;
            if (!IsInDryInterior() && !_isWalking && _currentDepth > crushDepthStart)
            {
                float depthT = math.saturate(
                    (_currentDepth - crushDepthStart) /
                    math.max(crushDepthFullDepth - crushDepthStart, 0.01f));
                float depthRateT = math.saturate(math.abs(_rb.linearVelocity.y) / math.max(crushDepthRateForFullStress, 0.01f));
                float transportProtection = transportPreset != null
                    ? math.max(0.1f, transportPreset.PressureDamageScale)
                    : 1f;
                targetStress = math.saturate((depthT * 0.72f + depthRateT * 0.28f) * transportProtection);
            }

            if (_externalHullStressRequestedThisStep)
            {
                targetStress = math.max(targetStress, _externalHullStressRequestedIntensity);
                _externalHullStressRequestedIntensity = 0f;
                _externalHullStressRequestedThisStep = false;
            }

            float blendT = 1f - math.exp(-math.max(crushDepthStressBlendSharpness, 0.01f) * fixedDeltaTime);
            _hullStressIntensity = math.lerp(_hullStressIntensity, targetStress, blendT);

            if (_hullStressIntensity > crushDepthShakeThreshold && _juiceProcessor != null)
            {
                float normalizedShake = math.saturate(
                    (_hullStressIntensity - crushDepthShakeThreshold) /
                    math.max(1f - crushDepthShakeThreshold, 0.01f));
                _juiceProcessor.RegisterEntanglementStrain(normalizedShake * 0.55f);
            }

            TryPlayCrushDepthGroan();
            RefreshFatalPressureHudCorruptionIfNeeded();

            if (_hullStressIntensity < crushDepthImplosionThreshold || _wipeoutTimer > 0f || _fatalPressureSequenceTimer > 0f)
                return;
            StartFatalPressureSequence();
        }

        private void RefreshFatalPressureHudCorruptionIfNeeded()
        {
            if (_hullStressIntensity <= 0.9f || _hullStressHudCorruptionRefreshTimer > 0f)
                return;

            LocalizationManager localization = LocalizationManager.Instance;
            if (localization == null)
                return;

            localization.RefreshHullStressHudCorruptionVisuals();
            _hullStressHudCorruptionRefreshTimer = 0.5f;
        }

        private void TryPlayCrushDepthGroan()
        {
            if (crushDepthGroanClip == null || _hullStressIntensity < crushDepthGroanThreshold || _hullStressGroanCooldownTimer > 0f)
                return;

            SpatialAudioManager audioManager = SpatialAudioManager.Instance;
            if (audioManager == null)
                return;

            float groanT = math.saturate(
                (_hullStressIntensity - crushDepthGroanThreshold) /
                math.max(1f - crushDepthGroanThreshold, 0.01f));
            float volume = math.lerp(0.4f, 0.9f, groanT);
            audioManager.PlayStatic2D(crushDepthGroanClip, volume, audioManager.InterfaceGroup);
            _hullStressGroanCooldownTimer = math.lerp(crushDepthGroanIntervalMax, crushDepthGroanIntervalMin, groanT);
        }

        private void UpdateUnderwaterStressSignal(float targetIntensity, float fixedDeltaTime)
        {
            float blendT = 1f - math.exp(-math.max(underwaterStressSignalBlendSharpness, 0.01f) * fixedDeltaTime);
            _underwaterStressSignalIntensity = math.lerp(_underwaterStressSignalIntensity, math.saturate(targetIntensity), blendT);
        }

        private void ApplyShoreUndertow(float physicsImmersion, PlayerTransportPreset transportPreset)
        {
            _undertowVector = Vector3.zero;
            _undertowIntensity = 0f;

            if (IsInDryInterior() || physicsImmersion <= 0.02f)
                return;

            if (_dynamicStormIntensity <= shoreUndertowStormThreshold)
                return;

            if (float.IsPositiveInfinity(_bottomClearance))
                return;

            Vector3 downSlopeDirection = Vector3.ProjectOnPlane(Vector3.down, _bottomNormal);
            float downSlopeSqrMagnitude = downSlopeDirection.sqrMagnitude;
            if (downSlopeSqrMagnitude <= 0.0001f)
                return;

            downSlopeDirection *= math.rsqrt(downSlopeSqrMagnitude);

            Vector3 retreatVelocity = _dynamicAverageWaterVelocity + EffectiveWaterFlowVelocity * 0.8f + _dynamicAverageWaterDisplacement * underwaterTurbulenceFrequency;
            float retreatSpeed = Vector3.Dot(retreatVelocity, downSlopeDirection);
            float retreatT = math.saturate(
                (retreatSpeed - shoreUndertowRetreatVelocityStart) /
                math.max(shoreUndertowRetreatVelocityMax - shoreUndertowRetreatVelocityStart, 0.01f));
            if (retreatT <= 0f)
                return;

            float stormT = math.saturate(
                (_dynamicStormIntensity - shoreUndertowStormThreshold) /
                math.max(1f - shoreUndertowStormThreshold, 0.01f));
            float shallowT = 1f - math.saturate(_currentDepth / math.max(shoreUndertowMaxDepth, 0.01f));
            float shorelineT = 1f - _shoreBuoyancyBlend;
            float bottomT = 1f - math.saturate(_bottomClearance / math.max(shoreBuoyancyRecoveryClearance, 0.01f));
            float feetDepth = GetFeetDepthBelowSurface(EffectiveWaterSurfaceY);
            float kneeDepthT = math.saturate(
                (feetDepth - shoreUndertowMinFeetDepth) /
                math.max(shoreUndertowFullFeetDepth - shoreUndertowMinFeetDepth, 0.01f));
            float transportInfluence = ResolveTransportAmbientCurrentInfluenceScale(transportPreset);
            float surfaceBoost = _isSurfaceSwimming ? shoreUndertowSurfaceBoost : 1f;
            float undertowIntensity = stormT * retreatT * shallowT * math.max(shorelineT, bottomT) * kneeDepthT * transportInfluence * surfaceBoost;
            if (undertowIntensity <= 0.001f)
                return;

            float undertowForce = shoreUndertowForce * undertowIntensity * _rb.mass;
            _undertowVector = downSlopeDirection * undertowForce;
            _undertowIntensity = undertowIntensity;
            _rb.AddForce(_undertowVector, ForceMode.Force);
        }

        private bool ShouldRequestTransportBailout(float impactSpeed, IPlayerTransportLifecycleOwner transportLifecycleOwner)
        {
            if (transportLifecycleOwner == null || _transportBailoutCooldownTimer > 0f)
                return false;

            if (impactSpeed >= wipeoutBailoutSpeedThreshold)
                return true;

            return transportLifecycleOwner.IsTransportBroken ||
                   transportLifecycleOwner.TransportIntegrityNormalized <= wipeoutBailoutCriticalIntegrityThreshold;
        }

        private void UpdateTransportCriticalBailout()
        {
            if (_wipeoutTimer > 0f || _fatalPressureSequenceTimer > 0f || _transportBailoutCooldownTimer > 0f)
                return;

            ResolvePlayerTransportCoordinator();
            if (_playerTransportCoordinator == null || !_playerTransportCoordinator.IsTransportActive())
                return;

            if (!_playerTransportCoordinator.TryResolveTransportLifecycleOwner(out IPlayerTransportLifecycleOwner transportLifecycleOwner) ||
                transportLifecycleOwner == null)
                return;

            if (!transportLifecycleOwner.IsTransportBroken &&
                transportLifecycleOwner.TransportIntegrityNormalized > wipeoutBailoutCriticalIntegrityThreshold)
                return;

            StartWipeout(
                1f,
                math.max(_rb.linearVelocity.magnitude, wipeoutBailoutSpeedThreshold),
                _cachedTransform.position,
                Vector3.up,
                transportLifecycleOwner,
                true,
                Vector3.zero);
        }

        private Vector3 ResolveTransportBailoutImpulse(Vector3 hitNormal, float severity)
        {
            Vector3 planarVelocity = _rb.linearVelocity;
            planarVelocity.y = 0f;
            Vector3 lateralDirection = planarVelocity.sqrMagnitude > 0.0001f
                ? -planarVelocity.normalized
                : Vector3.ProjectOnPlane(hitNormal, Vector3.up);

            if (lateralDirection.sqrMagnitude <= 0.0001f)
            {
                float yawRad = _bodyYaw * DEG_TO_RAD;
                lateralDirection.x = -math.sin(yawRad);
                lateralDirection.y = 0f;
                lateralDirection.z = -math.cos(yawRad);
            }
            else
            {
                lateralDirection.Normalize();
            }

            Vector3 bailoutImpulse = lateralDirection * (wipeoutBailoutImpulse * math.lerp(0.85f, 1.35f, severity));
            bailoutImpulse.y += wipeoutBailoutUpwardImpulse * math.lerp(0.75f, 1.2f, severity);
            return bailoutImpulse;
        }

        private void TriggerTransportBailout(
            float severity,
            Vector3 hitNormal,
            IPlayerTransportLifecycleOwner transportLifecycleOwner,
            Vector3 requestedBailoutImpulse)
        {
            Vector3 bailoutImpulse = requestedBailoutImpulse.sqrMagnitude > 0.0001f
                ? requestedBailoutImpulse
                : ResolveTransportBailoutImpulse(hitNormal, severity);

            ResolvePlayerToolManager();
            if (_playerToolManager != null &&
                _playerToolManager.CurrentTool != null &&
                transportLifecycleOwner != null &&
                ReferenceEquals(_playerToolManager.CurrentTool, transportLifecycleOwner))
            {
                if (transportLifecycleOwner is MantaScooter mantaScooter)
                    mantaScooter.TrySpawnEmergencyBailoutWreck(_rb.linearVelocity, bailoutImpulse, severity);

                _playerToolManager.Holster();
            }

            if (transportLifecycleOwner is MountablePlayerTransport mountableTransport)
                mountableTransport.TriggerEmergencyBailoutDrift(_rb.linearVelocity, severity);

            _transportBailoutCooldownTimer = wipeoutDuration + 0.35f;
            _rb.AddForce(bailoutImpulse, ForceMode.Impulse);
            TriggerBailoutDisorientation(severity, bailoutImpulse);
            OnTransportBailout?.Invoke(severity, bailoutImpulse);
        }

        private Vector3 ResolveAbyssalAmbientFlowWithNoise()
        {
            Vector3 worldPosition = _rb.position;
            Vector3 horizontalBias = Hecton8.Physics.CurrentVolume.SampleAt(worldPosition) + EffectiveWaterFlowVelocity;
            Unity.Mathematics.float3 phantomCurrent = CurrentManager.SampleCurrent(
                worldPosition,
                _currentTimer + _instanceId * 0.0131f,
                0.0042f,
                0.085f,
                1f,
                0.25f);
            horizontalBias.x += phantomCurrent.x;
            horizontalBias.z += phantomCurrent.z;
            if (vegetationDensityBridge != null)
                horizontalBias = vegetationDensityBridge.ApplyAbyssalFlowNoise(horizontalBias, worldPosition);

            return horizontalBias;
        }

        private void ApplyAbyssalFlowBoundaryTurbulence(Vector3 noisyFlow, float depthT, PlayerTransportPreset transportPreset)
        {
            Vector3 flowDelta = noisyFlow - _previousAbyssalNoisyFlow;
            _previousAbyssalNoisyFlow = noisyFlow;

            if (_abyssalFlowNoiseBoundaryCooldownTimer > 0f)
                return;

            float deltaMagnitude = flowDelta.magnitude;
            if (deltaMagnitude <= abyssalFlowNoiseBoundaryThreshold)
                return;

            float transportInfluence = ResolveTransportAmbientCurrentInfluenceScale(transportPreset);
            float boundaryT = math.saturate(
                (deltaMagnitude - abyssalFlowNoiseBoundaryThreshold) /
                math.max(abyssalFlowNoiseBoundaryThreshold, 0.01f));
            Vector3 joltDirection = flowDelta / math.max(deltaMagnitude, 0.0001f);
            Vector3 velocityChange = joltDirection * abyssalFlowNoiseBoundaryVelocityChange * boundaryT * depthT * transportInfluence;
            if (velocityChange.sqrMagnitude <= 0.0001f)
                return;

            _rb.AddForce(velocityChange, ForceMode.VelocityChange);
            ApplyAbyssalTransportTurbulenceTorque(joltDirection, boundaryT, depthT, transportPreset);
            if (_juiceProcessor != null)
            {
                _juiceProcessor.RegisterCollisionImpulse(
                    velocityChange.magnitude * 8f,
                    currentSuitData);

                float rollSign = math.sign(Vector3.Dot(joltDirection, _cachedTransform.right));
                if (rollSign == 0f)
                    rollSign = 1f;

                _juiceProcessor.RegisterExternalRollImpulse(rollSign * abyssalFlowNoiseBoundaryRollImpulse * boundaryT);
            }

            _abyssalFlowNoiseBoundaryCooldownTimer = abyssalFlowNoiseBoundaryCooldown;
        }

        private void AdvanceAbyssalTransportTurbulenceSteering(float fixedDeltaTime)
        {
            float recoverySharpness = math.max(abyssalTransportTurbulenceRecoverySharpness, 0.01f);
            float blendT = 1f - math.exp(-recoverySharpness * fixedDeltaTime);
            _abyssalTransportTurbulencePitchOffset = math.lerp(_abyssalTransportTurbulencePitchOffset, 0f, blendT);
            _abyssalTransportTurbulenceYawOffset = math.lerp(_abyssalTransportTurbulenceYawOffset, 0f, blendT);
        }

        private void ApplyAbyssalTransportTurbulenceTorque(
            Vector3 joltDirection,
            float boundaryT,
            float depthT,
            PlayerTransportPreset transportPreset)
        {
            if (ResolveActiveTransportPropulsionReference(transportPreset) <= 0.01f)
                return;

            float transportInfluence = ResolveTransportAmbientCurrentInfluenceScale(transportPreset);
            float turbulenceT = boundaryT * depthT * transportInfluence;
            if (turbulenceT <= 0.0001f)
                return;

            Vector3 torqueAxis = Vector3.Cross(_cachedTransform.forward, joltDirection);
            if (torqueAxis.sqrMagnitude <= 0.0001f)
                torqueAxis = Vector3.Cross(_cachedTransform.up, joltDirection);
            if (torqueAxis.sqrMagnitude > 0.0001f)
            {
                torqueAxis.Normalize();
                _rb.AddTorque(
                    torqueAxis * abyssalTransportTurbulenceTorqueVelocityChange * turbulenceT,
                    ForceMode.VelocityChange);
            }

            Vector3 localJolt = _cachedTransform.InverseTransformDirection(joltDirection);
            _abyssalTransportTurbulencePitchOffset = math.clamp(
                _abyssalTransportTurbulencePitchOffset - localJolt.y * abyssalTransportTurbulencePitchDegrees * turbulenceT,
                -abyssalTransportTurbulencePitchDegrees,
                abyssalTransportTurbulencePitchDegrees);
            _abyssalTransportTurbulenceYawOffset = math.clamp(
                _abyssalTransportTurbulenceYawOffset + localJolt.x * abyssalTransportTurbulenceYawDegrees * turbulenceT,
                -abyssalTransportTurbulenceYawDegrees,
                abyssalTransportTurbulenceYawDegrees);
        }

        private Vector3 ResolveAbyssalDowndraftDirection(Vector3 noisyFlow)
        {
            Vector3 horizontalBias = noisyFlow;
            horizontalBias.y = 0f;

            if (horizontalBias.sqrMagnitude <= 0.0001f || abyssalDowndraftHorizontalBias <= 0f)
                return Vector3.down;

            horizontalBias.Normalize();
            Vector3 downdraftDirection = Vector3.down + horizontalBias * abyssalDowndraftHorizontalBias;
            return downdraftDirection.sqrMagnitude > 0.0001f
                ? downdraftDirection.normalized
                : Vector3.down;
        }

        private float ResolveAbyssalDowndraftInterval(float depthT)
        {
            float baseInterval = math.lerp(abyssalDowndraftIntervalMax, abyssalDowndraftIntervalMin, depthT);
            float phase = math.frac(_currentTimer * 0.173f + _instanceId * 0.00017f);
            return math.lerp(baseInterval * 0.82f, baseInterval * 1.18f, phase);
        }

        private void TriggerBailoutDisorientation(float severity, Vector3 bailoutImpulse)
        {
            if (_juiceProcessor != null)
            {
                float rollSign = math.sign(Vector3.Dot(bailoutImpulse, _cachedTransform.right));
                if (rollSign == 0f)
                    rollSign = 1f;

                _juiceProcessor.RegisterExternalRollImpulse(
                    rollSign * wipeoutBailoutRollImpulse * math.lerp(0.7f, 1f, severity));
            }

            VisorHUDController.CopyActiveControllersTo(s_fatalPressureGlitchControllers);
            float distortionIntensity = wipeoutBailoutVisorDistortion * math.lerp(0.72f, 1f, severity);
            for (int i = 0; i < s_fatalPressureGlitchControllers.Count; i++)
            {
                VisorHUDController controller = s_fatalPressureGlitchControllers[i];
                if (controller != null)
                {
                    controller.TriggerEnvironmentalDistortion(
                        distortionIntensity,
                        wipeoutBailoutDisorientationDuration,
                        wipeoutBailoutVisorRecovery);
                }
            }

            s_fatalPressureGlitchControllers.Clear();
        }

        private float ResolveWipeoutTransportControl01()
        {
            if (_wipeoutTimer <= 0f && _fatalPressureSequenceTimer <= 0f)
                return 1f;

            return 0f;
        }

        private void StartFatalPressureSequence()
        {
            if (_fatalPressureSequenceTimer > 0f)
                return;

            _fatalPressureSequenceTimer = math.max(0.01f, fatalPressureSequenceDuration);
            _fatalPressureSequenceGlitchPulseTimer = 0f;
            _fatalPressureSequenceIntensity = 0.01f;
            _fatalPressureLookYawAnchor = _cameraYaw;
            _fatalPressureLookPitchAnchor = _cameraPitch;
            _jumpRequested = false;
            _jumpBufferTimer = 0f;

            float corruptionIntensity = ResolveFatalPressureCorruptionIntensity(_fatalPressureSequenceIntensity);
            ApplyFatalPressureVisorCorruption(corruptionIntensity);
            PushFatalPressureCorruptionWarning(corruptionIntensity);
        }

        private float ResolveFatalPressureCorruptionIntensity(float sequenceIntensity)
        {
            LocalizationManager localization = LocalizationManager.Instance;
            float localizationIntensity = localization != null
                ? localization.GetHullStressCorruptionIntensity()
                : 0f;
            return math.saturate(math.max(localizationIntensity, sequenceIntensity));
        }

        private void ApplyFatalPressureVisorCorruption(float corruptionIntensity)
        {
            float clampedIntensity = math.saturate(corruptionIntensity);
            if (clampedIntensity <= 0f)
                return;

            VisorHUDController.CopyActiveControllersTo(s_fatalPressureGlitchControllers);
            float holdDuration = math.lerp(0.08f, fatalPressureGlitchDurationMax, clampedIntensity);
            float recoverySpeed = math.lerp(6f, 1.4f, clampedIntensity);
            for (int i = 0; i < s_fatalPressureGlitchControllers.Count; i++)
            {
                VisorHUDController controller = s_fatalPressureGlitchControllers[i];
                if (controller != null)
                    controller.TriggerEnvironmentalDistortion(clampedIntensity, holdDuration, recoverySpeed);
            }

            s_fatalPressureGlitchControllers.Clear();
        }

        private void PushFatalPressureCorruptionWarning(float corruptionIntensity)
        {
            LocalizationManager localization = LocalizationManager.Instance;
            string message = localization != null
                ? localization.GetOrFallback(localization.CurrentLanguage, LocalizationKeys.HUD_STATUS_PRESSURE_LIMIT_EXCEEDED, "PRESSURE LIMIT EXCEEDED")
                : "PRESSURE LIMIT EXCEEDED";
            if (localization != null && corruptionIntensity > 0f)
                message = localization.CorruptExpandedText(message, corruptionIntensity);

            NotificationEvents.PushCritical(message);
        }

        private void TriggerFatalPressureImplosion()
        {
            IPlayerTransportLifecycleOwner transportLifecycleOwner = null;
            ResolvePlayerTransportCoordinator();
            if (_playerTransportCoordinator != null && _playerTransportCoordinator.IsTransportActive())
                _playerTransportCoordinator.TryResolveTransportLifecycleOwner(out transportLifecycleOwner);

            if (crushDepthImplosionClip != null)
            {
                SpatialAudioManager audioManager = SpatialAudioManager.Instance;
                if (audioManager != null)
                    audioManager.PlayStatic2D(crushDepthImplosionClip, 0.95f, audioManager.InterfaceGroup);
            }

            StartWipeout(
                1f,
                math.max(_rb.linearVelocity.magnitude, wipeoutBailoutSpeedThreshold),
                _cachedTransform.position,
                Vector3.up,
                transportLifecycleOwner,
                transportLifecycleOwner != null,
                Vector3.zero);
        }

        private void PulseFatalPressureGlitch(float intensity)
        {
            VisorHUDController.CopyActiveControllersTo(s_fatalPressureGlitchControllers);
            float glitchDuration = math.lerp(fatalPressureGlitchDurationMin, fatalPressureGlitchDurationMax, math.saturate(intensity));
            for (int i = 0; i < s_fatalPressureGlitchControllers.Count; i++)
            {
                VisorHUDController controller = s_fatalPressureGlitchControllers[i];
                if (controller != null)
                    controller.GlitchPulse(glitchDuration);
            }

            s_fatalPressureGlitchControllers.Clear();
        }

        private void UpdateVegetationDensityLinearDamping(float fixedDeltaTime)
        {
            float targetDamping = 0f;
            if (!IsInDryInterior())
            {
                float density = 0f;
                bool isSargassum = false;
                if (vegetationDensityBridge != null)
                {
                    HectonMapMagicVegetationBridge.VegetationDensitySample sample = vegetationDensityBridge.GetVegetationDensity(_rb.position);
                    density = sample.Density;
                    isSargassum = sample.AcousticType == HectonMapMagicVegetationBridge.VegetationAcousticType.SargassumBubbles;
                }
                else if (HectonMapMagicVegetationBridge.GlobalVegetationAcousticType == HectonMapMagicVegetationBridge.VegetationAcousticType.SargassumBubbles)
                {
                    density = HectonMapMagicVegetationBridge.GlobalVegetationAudioDensity;
                    isSargassum = true;
                }

                if (isSargassum && density > vegetationDensityDragThreshold)
                {
                    float densityT = math.saturate(
                        (density - vegetationDensityDragThreshold) /
                        math.max(1f - vegetationDensityDragThreshold, 0.01f));
                    targetDamping = vegetationDensityLinearDampingMax * densityT;
                }
            }

            float blendT = 1f - math.exp(-math.max(vegetationDensityLinearDampingBlendSharpness, 0.01f) * fixedDeltaTime);
            _vegetationDensityLinearDamping = math.lerp(_vegetationDensityLinearDamping, targetDamping, blendT);
        }

        private void ApplyWipeoutRecoveryForces(float fixedDeltaTime)
        {
            if (_wipeoutTimer <= 0f)
                return;

            _velocity = _rb.linearVelocity;
            float speed = math.sqrt(
                _velocity.x * _velocity.x +
                _velocity.y * _velocity.y +
                _velocity.z * _velocity.z);
            if (speed <= 0.01f)
                return;

            float dampingScale = wipeoutRecoveryDrag * math.lerp(0.75f, 1.25f, _wipeoutSeverity);
            float dampingForceMagnitude = dampingScale * speed * _rb.mass;
            float maxDampingForce = speed * _rb.mass * 0.9f / math.max(fixedDeltaTime, 0.0001f);
            if (dampingForceMagnitude > maxDampingForce)
                dampingForceMagnitude = maxDampingForce;

            float invSpeed = 1f / speed;
            _forceVector.x = -_velocity.x * invSpeed * dampingForceMagnitude;
            _forceVector.y = -_velocity.y * invSpeed * dampingForceMagnitude;
            _forceVector.z = -_velocity.z * invSpeed * dampingForceMagnitude;
            _rb.AddForce(_forceVector, ForceMode.Force);
        }

        private float ResolveTransportCavitationEfficiency(
            float fixedDeltaTime,
            bool hasTransportPropulsion,
            float forwardVelocity,
            float transportBoost01)
        {
            float targetEfficiency = 1f;
            if (hasTransportPropulsion && _currentDepth < transportCavitationRecoveryDepth)
            {
                float forwardAcceleration = (forwardVelocity - _previousTransportForwardVelocity) / math.max(fixedDeltaTime, 0.0001f);
                float accelerationT = math.saturate(
                    (forwardAcceleration - transportCavitationAccelerationStart) /
                    math.max(transportCavitationAccelerationMax - transportCavitationAccelerationStart, 0.01f));
                float depthT = 1f - math.saturate(
                    (_currentDepth - transportCavitationStartDepth) /
                    math.max(transportCavitationRecoveryDepth - transportCavitationStartDepth, 0.01f));
                float demandT = math.max(transportBoost01, math.saturate(_inputV));
                float lossT = depthT * accelerationT * demandT;
                targetEfficiency = math.lerp(1f, transportCavitationMinEfficiency, lossT);
            }

            float blendT = 1f - math.exp(-math.max(transportCavitationBlendSharpness, 0.01f) * fixedDeltaTime);
            _transportCavitationEfficiency = math.lerp(_transportCavitationEfficiency, targetEfficiency, blendT);
            _previousTransportForwardVelocity = forwardVelocity;
            return _transportCavitationEfficiency;
        }

        private void UpdateWetLensSignal(float fixedDeltaTime)
        {
            if (_wetLensPulseCooldownTimer > 0f)
            {
                _wetLensPulseCooldownTimer -= fixedDeltaTime;
                if (_wetLensPulseCooldownTimer < 0f)
                    _wetLensPulseCooldownTimer = 0f;
            }

            float recoveryT = 1f - math.exp(-math.max(wetLensSignalRecoverySpeed, 0.01f) * fixedDeltaTime);
            _wetLensSignalIntensity = math.lerp(_wetLensSignalIntensity, 0f, recoveryT);

            if (IsInDryInterior() || !_isSurfaceSwimming)
                return;

            float cameraY = playerCamera != null ? playerCamera.position.y : GetBodyEyeY();
            float coverDepth = EffectiveWaterSurfaceY - cameraY;
            if (coverDepth <= wetLensWaveCoverDepth || _dynamicStormIntensity < wetLensStormIntensityThreshold)
                return;

            float stormRange = math.max(1f - wetLensStormIntensityThreshold, 0.01f);
            float stormT = math.saturate((_dynamicStormIntensity - wetLensStormIntensityThreshold) / stormRange);
            float coverT = math.saturate((coverDepth - wetLensWaveCoverDepth) / math.max(wetLensWaveCoverDepth, 0.01f));
            EmitWetLensPulse(wetLensStormPulseIntensity * math.max(stormT, coverT), wetLensStormPulseCooldown);
        }

        private void EmitWetLensPulse(float intensity, float cooldown)
        {
            float clampedIntensity = math.saturate(intensity);
            if (clampedIntensity <= 0f)
                return;

            if (_wetLensPulseCooldownTimer > 0f && clampedIntensity <= _wetLensSignalIntensity)
                return;

            if (_wetLensSignalIntensity < clampedIntensity)
                _wetLensSignalIntensity = clampedIntensity;

            if (_wetLensPulseCooldownTimer < cooldown)
                _wetLensPulseCooldownTimer = cooldown;

            OnWetLensPulse?.Invoke(clampedIntensity);
        }

        private static float NormalizeSignedAngle(float angle)
        {
            angle = math.fmod(angle + 180f, 360f);
            if (angle < 0f)
                angle += 360f;

            return angle - 180f;
        }

        private float ResolveCameraLookDownAngle()
        {
            if (playerCamera != null)
            {
                float downwardComponent = math.clamp(-playerCamera.forward.y, 0f, 1f);
                return math.degrees(math.asin(downwardComponent));
            }

            return math.max(0f, _cameraPitch);
        }

        private void RefreshSurfaceBreachLock(float physicsImmersion)
        {
            if (IsInDryInterior() || _isGrounded || physicsImmersion <= 0.01f)
                return;

            if (_rb.linearVelocity.y < surfaceBreachReleaseVelocity)
                return;

            float headSurfaceOffset = GetHeadSurfaceOffset(EffectiveWaterSurfaceY);
            if (headSurfaceOffset < -surfaceBreachDepthWindow || headSurfaceOffset > surfaceBreachDepthWindow)
                return;

            if (_surfaceBreachLockTimer < surfaceBreachLockDuration)
                _surfaceBreachLockTimer = surfaceBreachLockDuration;
        }

        private void TryStartWaterEntryImpact(float previousWaterImmersionRatio, bool wasGroundedLastFixedTick, float entryVerticalVelocity)
        {
            if (IsInDryInterior() || wasGroundedLastFixedTick || previousWaterImmersionRatio > 0.01f || _waterImmersionRatio <= 0.01f)
                return;

            if (_surfaceBreachLockTimer > 0f && entryVerticalVelocity < 0f)
                _surfaceBreachLockTimer = 0f;

            if (_currentLocomotionMode != PlayerLocomotionMode.SurfaceSwim &&
                _currentLocomotionMode != PlayerLocomotionMode.UnderwaterSwim)
                return;

            float downwardEntrySpeed = math.max(0f, -entryVerticalVelocity);
            if (downwardEntrySpeed < waterEntryImpactMinSpeed)
                return;

            float impactRange = math.max(waterEntryImpactMinSpeed, 0.01f);
            float impactT = math.saturate((downwardEntrySpeed - waterEntryImpactMinSpeed) / impactRange);
            float impactDamping = math.lerp(waterEntryImpactDamping * 0.45f, waterEntryImpactDamping, impactT);

            if (_waterEntryImpactTimer < waterEntryImpactDuration)
                _waterEntryImpactTimer = waterEntryImpactDuration;

            if (_waterEntryImpactStrength < impactDamping)
                _waterEntryImpactStrength = impactDamping;

            float impactFovScale = math.lerp(0.35f, 1f, impactT);
            _juiceProcessor.RegisterWaterEntryFovImpulse(
                waterEntryImpactFovExpand * impactFovScale,
                waterEntryImpactFovCompress * impactFovScale,
                waterEntryImpactDuration);

            if (_recentBreachExitTimer > 0f)
                EmitBreachSplashRing(impactT);
        }

        private void TryPlaySurfacePierceSplashAudio(float previousWaterImmersionRatio, float surfacePierceVerticalVelocity)
        {
            if (IsInDryInterior())
                return;

            bool enteredWater = previousWaterImmersionRatio <= 0.01f && _waterImmersionRatio > 0.01f;
            bool exitedWater = previousWaterImmersionRatio > 0.01f && _currentDepth <= 0f && surfacePierceVerticalVelocity > 0f;
            if (!enteredWater && !exitedWater)
                return;

            float verticalSpeed = math.abs(surfacePierceVerticalVelocity);
            if (verticalSpeed < surfacePierceSplashMinSpeed)
                return;

            if (exitedWater)
                _recentBreachExitTimer = math.max(_recentBreachExitTimer, wipeoutBreachLandingGraceTime);

            float clampedMaxSpeed = math.max(surfacePierceSplashMaxSpeed, surfacePierceSplashMinSpeed + 0.01f);
            float speedT = math.saturate((verticalSpeed - surfacePierceSplashMinSpeed) / (clampedMaxSpeed - surfacePierceSplashMinSpeed));
            if (exitedWater)
                EmitBreachImpactFeedback(math.lerp(0.45f, 1f, speedT));

            if (!SpatialAudioManager.TryGetInstance(out SpatialAudioManager audioManager) || audioManager == null)
                return;

            AudioClip clip = enteredWater
                ? waterEntrySplashClip
                : (waterExitSplashClip != null ? waterExitSplashClip : waterEntrySplashClip);
            if (clip == null)
                return;

            float volume = math.lerp(surfacePierceSplashMinVolume, surfacePierceSplashMaxVolume, speedT);
            float pitch = enteredWater
                ? math.lerp(1.02f, 0.94f, speedT)
                : math.lerp(1.08f, 1.16f, speedT);

            audioManager.PlayAtPoint(clip, _cachedTransform.position, volume, pitch);

            if (exitedWater)
            {
                EmitWetLensPulse(math.max(wetLensBreachPulseIntensity, volume), wetLensStormPulseCooldown);
            }
        }

        private void EmitWipeoutImpactFeedback(float severity)
        {
            if (_waterImmersionRatio <= 0.01f && _currentDepth <= 0f)
                return;

            EmitImpactBubbleBurst(wipeoutBubbleParticles, severity);
            PlayUnderwaterImpactOneShot(severity);
        }

        private void EmitBreachImpactFeedback(float intensity)
        {
            EmitImpactBubbleBurst(breachBubbleParticles, intensity);
            PlayUnderwaterImpactOneShot(intensity * 0.88f);
        }

        private void EmitImpactBubbleBurst(ParticleSystem bubbleParticles, float intensity)
        {
            if (bubbleParticles == null)
                return;

            float clampedIntensity = math.saturate(intensity);
            if (clampedIntensity < impactBubbleMinIntensity)
                return;

            int bubbleCount = Mathf.RoundToInt(math.lerp(impactBubbleMinCount, impactBubbleMaxCount, clampedIntensity));
            if (bubbleCount <= 0)
                return;

            bubbleParticles.Emit(bubbleCount);
        }

        private void PlayUnderwaterImpactOneShot(float intensity)
        {
            if (underwaterImpactClip == null)
                return;

            if (!SpatialAudioManager.TryGetInstance(out SpatialAudioManager audioManager) || audioManager == null)
                return;

            float clampedIntensity = math.saturate(intensity);
            float volume = math.lerp(underwaterImpactMinVolume, underwaterImpactMaxVolume, clampedIntensity);
            float pitch = math.lerp(0.94f, 0.78f, clampedIntensity);
            audioManager.PlayAtPoint(underwaterImpactClip, _cachedTransform.position, volume, pitch);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SUIT APPLICATION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void UpdateHeadSurfaceRecovery(float fixedDeltaTime)
        {
            if (IsInDryInterior())
            {
                _surfaceGaspUnderwaterTimer = 0f;
                _surfaceGaspSubmergedLatch = false;
                return;
            }

            float headDepth = GetHeadDepthBelowSurface(EffectiveWaterSurfaceY);
            bool headSubmerged =
                headDepth >= surfaceGaspHeadEnterDepth ||
                (_surfaceGaspSubmergedLatch && headDepth > surfaceGaspHeadExitDepth);

            if (headSubmerged)
            {
                _surfaceGaspSubmergedLatch = true;
                _surfaceGaspUnderwaterTimer += fixedDeltaTime;
                return;
            }

            if (_surfaceGaspSubmergedLatch &&
                _surfaceGaspUnderwaterTimer >= surfaceGaspMinUnderwaterTime &&
                _surfaceGaspCooldownTimer <= 0f)
            {
                EmitSurfaceGasp();
                _surfaceGaspCooldownTimer = surfaceGaspCooldown;
            }

            _surfaceGaspUnderwaterTimer = 0f;
            _surfaceGaspSubmergedLatch = false;
        }

        private void EmitSurfaceGasp()
        {
            if (surfaceGaspClip != null &&
                SpatialAudioManager.TryGetInstance(out SpatialAudioManager audioManager) &&
                audioManager != null)
            {
                audioManager.PlayStatic2D(surfaceGaspClip, surfaceGaspVolume, audioManager.InterfaceGroup);
            }

            if (_juiceProcessor != null)
            {
                _juiceProcessor.RegisterWaterEntryFovImpulse(
                    surfaceGaspFovExpand,
                    surfaceGaspFovCompress,
                    surfaceGaspFovDuration);
            }
        }

        private void EmitBreachSplashRing(float intensity)
        {
            if (breachSplashRingParticles == null || intensity < breachSplashRingMinIntensity)
                return;

            Transform ringTransform = breachSplashRingParticles.transform;
            Vector3 ringPosition = _cachedTransform.position;
            ringPosition.y = EffectiveWaterSurfaceY;
            ringTransform.position = ringPosition;
            ringTransform.rotation = Quaternion.identity;
            breachSplashRingParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            breachSplashRingParticles.Play(true);
        }

        private void ApplySuitToRigidbody()
        {
            if (currentSuitData == null) return;
            _rb.mass = currentSuitData.mass;
            _rb.useGravity = false;

            if (_isWalking)
            {
                _currentLinearDamping = IsInDryInterior() ? 0f : currentSuitData.walkDrag;
                _rb.linearDamping = _currentLinearDamping;
            }
            else
            {
                _currentLinearDamping = 0f;
                _rb.linearDamping = 0f;
            }
        }

        private void ApplyModePhysics(SuitData suit)
        {
            if (!_isWalking || IsInDryInterior())
            {
                _rb.linearDamping = 0f;
                _currentLinearDamping = 0f;
            }
        }

        private void SmoothDampingTransition(float fixedDeltaTime, SuitData suit)
        {
            float targetDamping;
            if (_isWalking)
            {
                if (IsInDryInterior())
                {
                    targetDamping = 0f;
                }
                else
                {
                    float wadeFactor = 1f + _waterImmersionRatio * suit.wadeSlowdownFactor;
                    targetDamping = suit.walkDrag * wadeFactor;

                    if (IsDryLandAirborne())
                        targetDamping *= dryAirDampingMultiplier;
                }
            }
            else
            {
                targetDamping = 0f;
            }

            bool waterEntryImpactActive = _waterEntryImpactTimer > 0f && _waterEntryImpactStrength > 0f;
            if (waterEntryImpactActive)
            {
                float normalizedImpactTime = math.saturate(_waterEntryImpactTimer / math.max(waterEntryImpactDuration, 0.01f));
                float impactReleaseT = normalizedImpactTime * normalizedImpactTime * (3f - 2f * normalizedImpactTime);
                targetDamping += _waterEntryImpactStrength * impactReleaseT;
            }

            float dampingTransitionSpeed = suit.dampingTransitionSpeed;
            if (waterEntryImpactActive)
            {
                bool impactRampUp = targetDamping > _currentLinearDamping;
                dampingTransitionSpeed = math.max(dampingTransitionSpeed, impactRampUp ? 30f : 12f);
            }

            targetDamping += _vegetationDensityLinearDamping;

            if (math.abs(_currentLinearDamping - targetDamping) > 0.01f)
            {
                float t = 1f - math.exp(-dampingTransitionSpeed * fixedDeltaTime);
                _currentLinearDamping = math.lerp(_currentLinearDamping, targetDamping, t);
                _rb.linearDamping = _currentLinearDamping;
            }
            else if (_currentLinearDamping != targetDamping)
            {
                _currentLinearDamping = targetDamping;
                _rb.linearDamping = targetDamping;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  GROUND DETECTION + SMOOTHED NORMAL
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void GroundCheck()
        {
            GroundCheck(_currentFixedDeltaTime);
        }

        private void GroundCheck(float fixedDeltaTime)
        {
            Vector3 rbPos = _rb.position;
            float bodyBottomY = GetBodyBottomY();
            bool exosuitActive = IsExosuitTransportActive();
            bool dryInteriorActive = IsInDryInterior();
            float requiredGroundNormalY = exosuitActive
                ? math.min(_minGroundNormalY, exosuitMinGroundNormalY)
                : _minGroundNormalY;
            _groundCheckOrigin.x = rbPos.x;
            _groundCheckOrigin.y = bodyBottomY + groundCheckRadius + GroundCheckSkin;
            _groundCheckOrigin.z = rbPos.z;

            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                _groundCheckOrigin,
                groundCheckRadius,
                Vector3.down,
                _groundHitBuffer,
                groundCheckDistance + GroundCheckSkin,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            _isGrounded = false;
            float bestDistance = float.MaxValue;
            float bestNormalY = requiredGroundNormalY;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _groundHitBuffer[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null)
                    continue;

                if (hitCollider.attachedRigidbody == _rb)
                    continue;

                float normalY = hit.normal.y;
                if (normalY < requiredGroundNormalY)
                    continue;

                if (!_isGrounded || hit.distance < bestDistance || (math.abs(hit.distance - bestDistance) <= 0.001f && normalY > bestNormalY))
                {
                    _groundHit = hit;
                    bestDistance = hit.distance;
                    bestNormalY = normalY;
                    _isGrounded = true;
                }
            }

            Vector3 resolvedGroundNormal = _groundHit.normal;
            _exosuitFootingValid = false;
            _exosuitFootingNormal = Vector3.up;
            if (exosuitActive &&
                TryResolveExosuitFootSlope(rbPos, bodyBottomY, requiredGroundNormalY, out RaycastHit exosuitSupportHit, out Vector3 exosuitSupportNormal))
            {
                resolvedGroundNormal = exosuitSupportNormal;
                _exosuitFootingNormal = exosuitSupportNormal;
                _exosuitFootingValid = true;

                if (!_isGrounded ||
                    exosuitSupportHit.distance < bestDistance ||
                    (math.abs(exosuitSupportHit.distance - bestDistance) <= 0.001f && exosuitSupportNormal.y > bestNormalY))
                {
                    _groundHit = exosuitSupportHit;
                    bestDistance = exosuitSupportHit.distance;
                    bestNormalY = exosuitSupportNormal.y;
                    _isGrounded = true;
                }
            }
            else if (dryInteriorActive &&
                TryResolveDryInteriorFootSlope(rbPos, bodyBottomY, requiredGroundNormalY, out RaycastHit dryInteriorSupportHit, out Vector3 dryInteriorSupportNormal))
            {
                resolvedGroundNormal = dryInteriorSupportNormal;
                if (!_isGrounded ||
                    dryInteriorSupportHit.distance < bestDistance ||
                    (math.abs(dryInteriorSupportHit.distance - bestDistance) <= 0.001f && dryInteriorSupportNormal.y > bestNormalY))
                {
                    _groundHit = dryInteriorSupportHit;
                    bestDistance = dryInteriorSupportHit.distance;
                    bestNormalY = dryInteriorSupportNormal.y;
                    _isGrounded = true;
                }
            }

            if (_isGrounded)
            {
                float blendSharpness = exosuitActive && _exosuitFootingValid ? exosuitFootSlopeBlendSharpness : 15f;
                float normalT = 1f - math.exp(-math.max(0.01f, blendSharpness) * fixedDeltaTime);
                _smoothedGroundNormal = Vector3.Slerp(_smoothedGroundNormal, resolvedGroundNormal, normalT);

                float sqrMag = _smoothedGroundNormal.sqrMagnitude;
                if (sqrMag > 0.001f && math.abs(sqrMag - 1f) > 0.001f)
                {
                    _smoothedGroundNormal = _smoothedGroundNormal.normalized;
                }
            }
            else
            {
                _groundHit = default;
                float resetT = 1f - math.exp(-5f * fixedDeltaTime);
                _smoothedGroundNormal = Vector3.Slerp(_smoothedGroundNormal, Vector3.up, resetT);
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  GROUND STABILITY
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ApplyGroundStability(float scale)
        {
            if (scale <= 0.001f) return;

            float mass = _rb.mass;
            bool exosuitActive = _currentLocomotionMode == PlayerLocomotionMode.ExosuitLocomotion;
            float slopeStickMultiplier = exosuitActive ? exosuitSlopeStickForceMultiplier : 1f;
            float snapForceMultiplier = exosuitActive ? exosuitGroundSnapForceMultiplier : 1f;
            float gravityAlongNormal = Vector3.Dot(_cachedGravity, _smoothedGroundNormal);
            _forceVector.x = (_smoothedGroundNormal.x * gravityAlongNormal) - _cachedGravity.x;
            _forceVector.y = (_smoothedGroundNormal.y * gravityAlongNormal) - _cachedGravity.y;
            _forceVector.z = (_smoothedGroundNormal.z * gravityAlongNormal) - _cachedGravity.z;

            float tangentSqr = _forceVector.x * _forceVector.x + _forceVector.y * _forceVector.y + _forceVector.z * _forceVector.z;
            if (tangentSqr > 0.000001f)
            {
                float slopeHoldForce = mass * _gravityScale * scale * slopeStickMultiplier;
                _forceVector.x *= slopeHoldForce;
                _forceVector.y *= slopeHoldForce;
                _forceVector.z *= slopeHoldForce;
                _rb.AddForce(_forceVector, ForceMode.Force);
            }

            float gravityIntoGround = Vector3.Dot(-_cachedGravity, _smoothedGroundNormal);
            if (gravityIntoGround > 0f)
            {
                float supportForce = gravityIntoGround * mass * slopeStabilityFactor * scale * slopeStickMultiplier;
                _forceVector.x = _smoothedGroundNormal.x * supportForce;
                _forceVector.y = _smoothedGroundNormal.y * supportForce;
                _forceVector.z = _smoothedGroundNormal.z * supportForce;
                _rb.AddForce(_forceVector, ForceMode.Force);
            }

            if (groundSnapForce > 0f)
            {
                float snapForce = groundSnapForce * mass * scale * snapForceMultiplier;
                _forceVector.x = -_smoothedGroundNormal.x * snapForce;
                _forceVector.y = -_smoothedGroundNormal.y * snapForce;
                _forceVector.z = -_smoothedGroundNormal.z * snapForce;
                _rb.AddForce(_forceVector, ForceMode.Force);
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SURFACE LOCK
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ApplySurfaceLock(SuitData suit, PlayerTransportPreset transportPreset)
        {
            if (suit.surfaceLockStrength <= 0f) return;
            if (IsInDryInterior()) return;
            if (_isGrounded) return;
            if (_shoreGroundGraceTimer > 0f && _smoothedImmersionRatio < swimTransitionThreshold) return;
            float surfaceLockInfluenceScale = ResolveTransportSurfaceLockInfluenceScale(transportPreset);
            float diveCommitT = 0f;
            if (surfaceDiveCommitHoldTime > 0f)
                diveCommitT = math.saturate(_surfaceDiveCommitTimer / surfaceDiveCommitHoldTime);
            else if (HasSurfaceDiveIntent(transportPreset))
                diveCommitT = 1f;

            float diveIntentScale = HasCommittedSurfaceDive(transportPreset)
                ? 0.18f
                : math.lerp(1f, 0.72f, diveCommitT);
            float sargassumSupportScale = math.lerp(1f, sargassumMatSurfaceLockBoost, _sargassumMatBuoyancyBlend);
            float effectiveLockScale = _shoreBuoyancyBlend * surfaceLockInfluenceScale * _surfaceLockBlend * diveIntentScale * sargassumSupportScale;
            if (effectiveLockScale <= 0.001f) return;

            float targetSurfaceLockY = _surfaceLockTargetY + sargassumMatSurfaceLiftOffset * _sargassumMatBuoyancyBlend;
            float positionError = _rb.position.y - targetSurfaceLockY;
            float effectiveSurfaceLockRange = math.max(suit.surfaceLockRange, math.abs(surfaceStickOffset) + surfaceBreachDepthWindow);
            positionError = math.clamp(positionError, -effectiveSurfaceLockRange, effectiveSurfaceLockRange);

            float targetSurfaceVelocityY = _isSurfaceSwimming
                ? EffectiveWaterSurfaceVelocity.y * surfaceWaveVelocityInfluence
                : 0f;
            float velocityError = _rb.linearVelocity.y - targetSurfaceVelocityY;
            float springForce = -positionError * suit.surfaceLockStrength * effectiveLockScale;
            float dampForce = -velocityError * suit.surfaceLockDamping * effectiveLockScale;
            float totalForce = (springForce + dampForce) * _rb.mass;

            _forceVector.x = 0f;
            _forceVector.y = totalForce;
            _forceVector.z = 0f;
            _rb.AddForce(_forceVector, ForceMode.Force);
        }

        private float GetBodyBottomY()
        {
            if (_capsuleCollider != null)
                return _capsuleCollider.bounds.min.y;

            return _rb.position.y - playerHeight * 0.5f;
        }

        private float GetBodyTopY()
        {
            if (_capsuleCollider != null)
                return _capsuleCollider.bounds.max.y;

            return _rb.position.y + playerHeight * 0.5f;
        }

        private float GetBodyEyeY()
        {
            return math.lerp(GetBodyBottomY(), GetBodyTopY(), 0.85f);
        }

        private float GetHeadSurfaceOffset(float surfaceY)
        {
            return surfaceY - GetBodyTopY();
        }

        private float GetHeadDepthBelowSurface(float surfaceY)
        {
            float depth = GetHeadSurfaceOffset(surfaceY);
            return depth > 0f ? depth : 0f;
        }

        private float GetFeetDepthBelowSurface(float surfaceY)
        {
            float depth = surfaceY - GetBodyBottomY();
            return depth > 0f ? depth : 0f;
        }

        private void UpdateBottomClearance()
        {
            if (IsInDryInterior())
            {
                _bottomClearance = float.PositiveInfinity;
                _bottomNormal = Vector3.up;
                return;
            }

            if (_isGrounded)
            {
                _bottomClearance = 0f;
                _bottomNormal = _smoothedGroundNormal.sqrMagnitude > 0.0001f ? _smoothedGroundNormal.normalized : Vector3.up;
                return;
            }

            float maxSampleDistance = math.max(
                groundCheckDistance,
                math.max(shoreBuoyancyRecoveryClearance, underwaterTurbulenceBottomInfluenceDepth)) + playerHeight;
            if (maxSampleDistance <= 0f)
            {
                _bottomClearance = float.PositiveInfinity;
                _bottomNormal = Vector3.up;
                return;
            }

            float radius = math.max(groundCheckRadius * 0.85f, 0.05f);
            Vector3 origin = _rb.position;
            origin.y = GetBodyBottomY() + radius + GroundCheckSkin;
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                origin,
                radius,
                Vector3.down,
                _groundHitBuffer,
                maxSampleDistance + GroundCheckSkin,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            float bestClearance = float.PositiveInfinity;
            Vector3 bestNormal = Vector3.up;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _groundHitBuffer[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null)
                    continue;

                if (hitCollider.attachedRigidbody == _rb)
                    continue;

                if (hit.normal.y < _minGroundNormalY)
                    continue;

                float clearance = hit.distance - GroundCheckSkin;
                if (clearance < 0f)
                    clearance = 0f;

                if (clearance < bestClearance)
                {
                    bestClearance = clearance;
                    bestNormal = hit.normal;
                }
            }

            _bottomClearance = bestClearance;
            _bottomNormal = bestNormal;
        }

        private void UpdateShoreBuoyancyBlend(float fixedDeltaTime, float physicsImmersion, float feetDepth)
        {
            float targetBlend = 1f;
            if (IsInDryInterior() || physicsImmersion <= 0.01f)
            {
                targetBlend = 0f;
            }
            else if (feetDepth <= shoreBuoyancyRecoveryClearance)
            {
                float depthBlend = math.saturate(
                    (feetDepth - shoreWalkFootDepth) /
                    math.max(shoreBuoyancyRecoveryClearance - shoreWalkFootDepth, 0.01f));
                if (!float.IsPositiveInfinity(_bottomClearance))
                {
                    float clearanceBlend = math.saturate(_bottomClearance / math.max(shoreBuoyancyRecoveryClearance, 0.01f));
                    targetBlend = math.min(depthBlend, clearanceBlend);
                }
                else
                {
                    targetBlend = depthBlend;
                }
            }

            float blendT = 1f - math.exp(-math.max(shoreBuoyancyBlendSharpness, 0.01f) * fixedDeltaTime);
            _shoreBuoyancyBlend = math.lerp(_shoreBuoyancyBlend, targetBlend, blendT);
        }

        private bool TryGetCapsuleCastGeometry(float inset, out Vector3 point1, out Vector3 point2, out float radius)
        {
            if (_capsuleCollider != null)
            {
                Bounds bounds = _capsuleCollider.bounds;
                float extentsX = bounds.extents.x;
                float extentsZ = bounds.extents.z;
                radius = math.max(0.01f, math.min(extentsX, extentsZ) - inset);
                float segmentHalf = math.max(0f, bounds.extents.y - radius - inset);
                Vector3 center = bounds.center;
                point1 = center + Vector3.up * segmentHalf;
                point2 = center - Vector3.up * segmentHalf;
                return true;
            }

            radius = math.max(groundCheckRadius - inset, 0.01f);
            float halfHeight = math.max(playerHeight * 0.5f - radius - inset, 0f);
            Vector3 centerFallback = _rb.position;
            point1 = centerFallback + Vector3.up * halfHeight;
            point2 = centerFallback - Vector3.up * halfHeight;
            return true;
        }

        private void RefreshGroundSlopeCache()
        {
            _minGroundNormalY = math.cos(maxGroundAngle * DEG_TO_RAD);
        }

        private void ConsumeJumpRequest()
        {
            _jumpRequested = false;
            _jumpBufferTimer = 0f;
        }

        private void ApplyExosuitJumpJets(float fixedDeltaTime)
        {
            if (!IsExosuitTransportActive())
                return;

            if (_rb == null || fixedDeltaTime <= 0f)
                return;

            float jumpIntent = ResolveExosuitJumpJetIntent();
            if (jumpIntent <= 0.0001f)
            {
                CoolExosuitJumpJets(fixedDeltaTime);
                return;
            }

            if (!HasJumpHeadClearance())
            {
                ConsumeJumpRequest();
                CoolExosuitJumpJets(fixedDeltaTime);
                return;
            }

            if (_survivalSystem != null && _survivalSystem.Energy <= 0.01f)
            {
                ConsumeJumpRequest();
                CoolExosuitJumpJets(fixedDeltaTime);
                return;
            }

            if (_exosuitJumpJetsOverheated)
            {
                CoolExosuitJumpJets(fixedDeltaTime);
                ConsumeJumpRequest();
                return;
            }

            if (_isGrounded && _jumpRequested && exosuitJumpJetLaunchImpulse > 0f)
            {
                _rb.AddForce(Vector3.up * exosuitJumpJetLaunchImpulse, ForceMode.VelocityChange);
                _isGrounded = false;
                _wasGroundedLastFrame = false;
                _dryGroundGraceTimer = 0f;
                _shoreGroundGraceTimer = 0f;
            }

            float thrustForce = exosuitJumpJetForce * jumpIntent;
            if (thrustForce > 0.001f)
            {
                _rb.AddForce(Vector3.up * thrustForce, ForceMode.Force);

                float energyDrainPerSecond = ResolveExosuitJumpJetEnergyDrainPerSecond();
                if (_survivalSystem != null && energyDrainPerSecond > 0f)
                    _survivalSystem.DrainEnergy(energyDrainPerSecond * jumpIntent * fixedDeltaTime);

                if (exosuitJumpJetHeatPerSecond > 0f)
                {
                    _exosuitJumpJetHeat01 = math.saturate(_exosuitJumpJetHeat01 + exosuitJumpJetHeatPerSecond * jumpIntent * fixedDeltaTime);
                    if (_exosuitJumpJetHeat01 >= 0.999f)
                        _exosuitJumpJetsOverheated = true;
                }
            }

            if (_subscribedInputManager == null || !_subscribedInputManager.IsJumping)
                ConsumeJumpRequest();
        }

        private void CoolExosuitJumpJets(float fixedDeltaTime)
        {
            if (_exosuitJumpJetHeat01 <= 0f)
                return;

            _exosuitJumpJetHeat01 = math.max(0f, _exosuitJumpJetHeat01 - exosuitJumpJetCoolRate * fixedDeltaTime);
            if (_exosuitJumpJetsOverheated && _exosuitJumpJetHeat01 <= exosuitJumpJetRecoverThreshold)
                _exosuitJumpJetsOverheated = false;
        }

        private float ResolveExosuitJumpJetEnergyDrainPerSecond()
        {
            PlayerTransportPreset activeTransportPreset = ResolveActiveTransportPreset();
            if (activeTransportPreset != null && activeTransportPreset.EnergyDrainPerSecond > 0f)
            {
                return activeTransportPreset.EnergyDrainPerSecond * math.max(1f, exosuitJumpJetScooterDrainMultiplier);
            }

            return math.max(0f, exosuitJumpJetEnergyDrainPerSecond);
        }

        private float ResolveExosuitJumpJetIntent()
        {
            if (_subscribedInputManager != null && _subscribedInputManager.IsJumping)
                return 1f;

            return _jumpRequested ? 1f : 0f;
        }

        private bool TryResolveDryInteriorFootSlope(
            Vector3 rbPosition,
            float bodyBottomY,
            float minimumNormalY,
            out RaycastHit supportHit,
            out Vector3 supportNormal)
        {
            supportHit = default;
            supportNormal = Vector3.up;

            float lateralOffset = math.max(0.01f, dryInteriorFootProbeLateralOffset);
            float probeDistance = math.max(0.05f, dryInteriorFootProbeDistance);
            float probeOriginY = bodyBottomY + math.max(0.01f, dryInteriorFootProbeHeight);
            float yawRad = _bodyYaw * DEG_TO_RAD;
            Vector3 bodyForward = new Vector3(math.sin(yawRad), 0f, math.cos(yawRad));
            Vector3 bodyRight = new Vector3(bodyForward.z, 0f, -bodyForward.x);
            Vector3 center = new Vector3(rbPosition.x, probeOriginY, rbPosition.z);
            Vector3 forwardOffset = bodyForward * dryInteriorFootProbeForwardOffset;

            bool leftValid = TrySampleDryInteriorFootSupport(
                center + forwardOffset - bodyRight * lateralOffset,
                probeDistance,
                minimumNormalY,
                out RaycastHit leftHit);
            bool rightValid = TrySampleDryInteriorFootSupport(
                center + forwardOffset + bodyRight * lateralOffset,
                probeDistance,
                minimumNormalY,
                out RaycastHit rightHit);
            if (!leftValid && !rightValid)
                return false;

            if (leftValid && rightValid)
            {
                Vector3 combinedNormal = leftHit.normal + rightHit.normal;
                supportNormal = combinedNormal.sqrMagnitude > 0.0001f
                    ? combinedNormal.normalized
                    : Vector3.up;
                supportHit = leftHit.distance <= rightHit.distance ? leftHit : rightHit;
                return true;
            }

            supportHit = leftValid ? leftHit : rightHit;
            supportNormal = supportHit.normal;
            return true;
        }

        private bool TrySampleDryInteriorFootSupport(
            Vector3 origin,
            float distance,
            float minimumNormalY,
            out RaycastHit supportHit)
        {
            supportHit = default;
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                _groundHitBuffer,
                distance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _groundHitBuffer[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.attachedRigidbody == _rb)
                    continue;

                if (hit.normal.y < minimumNormalY)
                    continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    supportHit = hit;
                }
            }

            return bestDistance < float.MaxValue;
        }

        private bool TryResolveExosuitFootSlope(
            Vector3 rbPosition,
            float bodyBottomY,
            float minimumNormalY,
            out RaycastHit supportHit,
            out Vector3 supportNormal)
        {
            supportHit = default;
            supportNormal = Vector3.up;

            float lateralOffset = math.max(0.01f, exosuitFootProbeLateralOffset);
            float probeDistance = math.max(0.05f, exosuitFootProbeDistance);
            float probeOriginY = bodyBottomY + math.max(0.01f, exosuitFootProbeHeight);
            float yawRad = _bodyYaw * DEG_TO_RAD;
            Vector3 bodyForward = new Vector3(math.sin(yawRad), 0f, math.cos(yawRad));
            Vector3 bodyRight = new Vector3(bodyForward.z, 0f, -bodyForward.x);
            Vector3 center = new Vector3(rbPosition.x, probeOriginY, rbPosition.z);
            Vector3 forwardOffset = bodyForward * exosuitFootProbeForwardOffset;

            bool leftValid = TrySampleExosuitFootSupport(
                center + forwardOffset - bodyRight * lateralOffset,
                probeDistance,
                minimumNormalY,
                out RaycastHit leftHit);
            bool rightValid = TrySampleExosuitFootSupport(
                center + forwardOffset + bodyRight * lateralOffset,
                probeDistance,
                minimumNormalY,
                out RaycastHit rightHit);
            if (!leftValid && !rightValid)
                return false;

            if (leftValid && rightValid)
            {
                Vector3 combinedNormal = leftHit.normal + rightHit.normal;
                supportNormal = combinedNormal.sqrMagnitude > 0.0001f
                    ? combinedNormal.normalized
                    : Vector3.up;
                supportHit = leftHit.distance <= rightHit.distance ? leftHit : rightHit;
                return true;
            }

            supportHit = leftValid ? leftHit : rightHit;
            supportNormal = supportHit.normal;
            return true;
        }

        private bool TrySampleExosuitFootSupport(
            Vector3 origin,
            float distance,
            float minimumNormalY,
            out RaycastHit supportHit)
        {
            supportHit = default;
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                _groundHitBuffer,
                distance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _groundHitBuffer[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.attachedRigidbody == _rb)
                    continue;

                if (hit.normal.y < minimumNormalY)
                    continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    supportHit = hit;
                }
            }

            return bestDistance < float.MaxValue;
        }

        private bool TryApplyJumpImpulse(float impulse)
        {
            if (impulse <= 0f)
                return false;

            if (!HasJumpHeadClearance())
                return false;

            _velocity = _rb.linearVelocity;
            if (_velocity.y < 0f)
            {
                _velocity.y = 0f;
                _rb.linearVelocity = _velocity;
            }

            _isGrounded = false;
            _wasGroundedLastFrame = false;
            _snapScale = 0f;
            _dryGroundGraceTimer = 0f;
            _shoreGroundGraceTimer = 0f;

            if (_juiceProcessor != null)
                _juiceProcessor.RegisterLandJumpLaunch();

            _rb.AddForce(Vector3.up * impulse, ForceMode.VelocityChange);
            return true;
        }

        private bool HasJumpHeadClearance()
        {
            if (jumpHeadClearanceDistance <= 0f)
                return true;

            if (!TryGetCapsuleCastGeometry(0.02f, out Vector3 point1, out Vector3 point2, out float radius))
                return true;

            int hitCount = UnityEngine.Physics.CapsuleCastNonAlloc(
                point1,
                point2,
                radius,
                Vector3.up,
                _groundHitBuffer,
                jumpHeadClearanceDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = _groundHitBuffer[i].collider;
                if (hitCollider == null)
                    continue;

                if (hitCollider.attachedRigidbody == _rb)
                    continue;

                return false;
            }

            return true;
        }

        private bool TryApplyStepAssist(bool groundedOnDryLand, bool groundedOnShore)
        {
            if (stepAssistHeight <= 0f || stepAssistForwardDistance <= 0f)
                return false;

            if (_stepAssistCooldownTimer > 0f)
                return false;

            if (!(groundedOnDryLand || groundedOnShore || _isGrounded))
                return false;

            if (_rb.linearVelocity.y > 0.5f)
                return false;

            float dirX = _moveDirection.x;
            float dirZ = _moveDirection.z;
            float planarSqr = dirX * dirX + dirZ * dirZ;
            if (planarSqr <= 0.0001f)
                return false;

            float invPlanarMag = 1f / math.sqrt(planarSqr);
            dirX *= invPlanarMag;
            dirZ *= invPlanarMag;

            float probeRadius = math.max(groundCheckRadius * 0.85f, 0.05f);
            float currentBottomY = GetBodyBottomY();

            _groundCheckOrigin.x = _rb.position.x;
            _groundCheckOrigin.y = currentBottomY + probeRadius + GroundCheckSkin;
            _groundCheckOrigin.z = _rb.position.z;

            if (!TryFindStepObstacle(_groundCheckOrigin, probeRadius, dirX, dirZ, out RaycastHit obstacleHit))
                return false;

            float forwardDistance = math.min(stepAssistForwardDistance, math.max(obstacleHit.distance + stepAssistClearance, probeRadius));

            Vector3 raisedOrigin = _groundCheckOrigin;
            raisedOrigin.y += stepAssistHeight;

            if (HasForwardBlockAtHeight(raisedOrigin, probeRadius, dirX, dirZ, forwardDistance))
                return false;

            Vector3 landingOrigin;
            landingOrigin.x = raisedOrigin.x + dirX * forwardDistance;
            landingOrigin.y = raisedOrigin.y;
            landingOrigin.z = raisedOrigin.z + dirZ * forwardDistance;

            if (!TryFindStepLanding(landingOrigin, probeRadius, out RaycastHit landingHit))
                return false;

            float landedCenterY = landingOrigin.y - landingHit.distance;
            float targetBottomY = landedCenterY - probeRadius;
            float stepDeltaY = targetBottomY - currentBottomY;
            if (stepDeltaY <= GroundCheckSkin || stepDeltaY > stepAssistHeight + GroundCheckSkin)
                return false;

            Vector3 newPosition = _rb.position;
            newPosition.x += dirX * forwardDistance;
            newPosition.y += stepDeltaY;
            newPosition.z += dirZ * forwardDistance;
            _rb.position = newPosition;

            _velocity = _rb.linearVelocity;
            if (_velocity.y < 0f)
            {
                _velocity.y = 0f;
                _rb.linearVelocity = _velocity;
            }

            _stepAssistCooldownTimer = stepAssistCooldownTime;
            _dryGroundGraceTimer = dryGroundGraceTime;
            if (groundedOnShore)
                _shoreGroundGraceTimer = shoreGroundGraceTime;

            GroundCheck();
            _waterImmersionRatio = ComputeImmersionRatio();
            _currentDepth = ComputeDepth();
            return true;
        }

        private bool TryFindStepObstacle(Vector3 origin, float radius, float dirX, float dirZ, out RaycastHit obstacleHit)
        {
            obstacleHit = default;
            Vector3 direction = new Vector3(dirX, 0f, dirZ);
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                origin,
                radius,
                direction,
                _groundHitBuffer,
                stepAssistForwardDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _groundHitBuffer[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null)
                    continue;

                if (hitCollider.attachedRigidbody == _rb)
                    continue;

                if (hit.distance <= 0.001f)
                    continue;

                if (hit.normal.y >= _minGroundNormalY)
                    continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    obstacleHit = hit;
                }
            }

            return bestDistance < float.MaxValue;
        }

        private bool HasForwardBlockAtHeight(Vector3 origin, float radius, float dirX, float dirZ, float distance)
        {
            Vector3 direction = new Vector3(dirX, 0f, dirZ);
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                origin,
                radius,
                direction,
                _groundHitBuffer,
                distance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = _groundHitBuffer[i].collider;
                if (hitCollider == null)
                    continue;

                if (hitCollider.attachedRigidbody == _rb)
                    continue;

                return true;
            }

            return false;
        }

        private bool TryFindStepLanding(Vector3 origin, float radius, out RaycastHit landingHit)
        {
            landingHit = default;
            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(
                origin,
                radius,
                Vector3.down,
                _groundHitBuffer,
                stepAssistHeight + radius + GroundCheckSkin,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            float bestNormalY = _minGroundNormalY;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _groundHitBuffer[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null)
                    continue;

                if (hitCollider.attachedRigidbody == _rb)
                    continue;

                float normalY = hit.normal.y;
                if (normalY < _minGroundNormalY)
                    continue;

                if (hit.distance < bestDistance || (math.abs(hit.distance - bestDistance) <= 0.001f && normalY > bestNormalY))
                {
                    bestDistance = hit.distance;
                    bestNormalY = normalY;
                    landingHit = hit;
                }
            }

            return bestDistance < float.MaxValue;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SWIM PHYSICS â€” with depth pressure resistance
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void SwimPhysics(SuitData suit, float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            _velocity = _rb.linearVelocity;
            float speed = math.sqrt(
                _velocity.x * _velocity.x +
                _velocity.y * _velocity.y +
                _velocity.z * _velocity.z);
            bool isSurfaceSwim = _isSurfaceSwimming;
            bool hasSurfaceDiveIntent = isSurfaceSwim && HasCommittedSurfaceDive(transportPreset);
            float shoreSwimBlend = isSurfaceSwim ? _shoreBuoyancyBlend : 1f;

            // â”€â”€ Depth-based drag increase (v7.0) â”€â”€
            float depthDragAdd = 0f;
            if (_currentDepth > suit.depthSwimSlowdownStart && suit.depthDragIncreaseMax > 0f)
            {
                float depthT = math.saturate(
                    (_currentDepth - suit.depthSwimSlowdownStart) /
                    math.max(suit.depthSwimSlowdownEnd - suit.depthSwimSlowdownStart, 0.01f));
                depthDragAdd = depthT * suit.depthDragIncreaseMax;
            }

            float effectiveDragCoeff = suit.swimDragCoefficient + depthDragAdd;
            if (isSurfaceSwim)
                effectiveDragCoeff *= surfaceDragMultiplier;

            float sargassumSpeedMultiplier = ResolveSargassumSpeedMultiplier();
            float sargassumDragMultiplier = ResolveSargassumDragMultiplier();
            float externalEnvironmentalDragMultiplier = ResolveExternalEnvironmentalDragMultiplier();
            float externalEnvironmentalThrustMultiplier = ResolveExternalEnvironmentalThrustMultiplier();
            effectiveDragCoeff *= sargassumDragMultiplier;
            effectiveDragCoeff *= externalEnvironmentalDragMultiplier;
            effectiveDragCoeff *= math.lerp(1f, crushDepthDragMultiplier, _hullStressIntensity);

            // â”€â”€ Quadratic drag â”€â”€
            if (speed > 0.01f)
            {
                float dragMagnitude = effectiveDragCoeff * speed * speed;
                float maxDrag = speed * _rb.mass * 0.9f / fixedDeltaTime;
                if (dragMagnitude > maxDrag) dragMagnitude = maxDrag;

                float invSpeed = 1f / speed;
                _forceVector.x = -_velocity.x * invSpeed * dragMagnitude;
                _forceVector.y = -_velocity.y * invSpeed * dragMagnitude;
                _forceVector.z = -_velocity.z * invSpeed * dragMagnitude;
                _rb.AddForce(_forceVector, ForceMode.Force);
            }

            // â”€â”€ Swim thrust â”€â”€
            float rawTransportPropulsionForce =
                ResolveActiveTransportPropulsionForce() *
                sargassumSpeedMultiplier *
                externalEnvironmentalThrustMultiplier *
                ResolveWipeoutTransportControl01();
            bool hasInput = _inputH != 0f || _inputV != 0f || _inputVertical != 0f;
            bool surfaceDiveAssistActive = _surfaceDiveAssistTimer > 0f;
            if (!hasInput && rawTransportPropulsionForce <= 0f && !surfaceDiveAssistActive)
                return;

            // â”€â”€ Depth-based swim force reduction (v7.0) â”€â”€
            float depthSlowdown = 1f;
            if (_currentDepth > suit.depthSwimSlowdownStart && suit.depthSwimSlowdownMax > 0f)
            {
                float slowT = math.saturate(
                    (_currentDepth - suit.depthSwimSlowdownStart) /
                    math.max(suit.depthSwimSlowdownEnd - suit.depthSwimSlowdownStart, 0.01f));
                depthSlowdown = 1f - slowT * suit.depthSwimSlowdownMax;
            }

            bool heavyCarryActive = IsHeavyCarryActive();
            float sprintMult = _isSprinting && !heavyCarryActive ? suit.sprintMultiplier : 1f;
            float runtimeSwimSpeedScale = _runtimeSwimSpeedMultiplier * _runtimeInjurySwimSpeedMultiplier;
            float effectiveSwimForce = suit.swimForce * depthSlowdown * sprintMult * runtimeSwimSpeedScale;
            float effectiveVerticalForce = suit.swimVerticalForce * depthSlowdown * sprintMult * runtimeSwimSpeedScale;
            float heavyCarryForceMultiplier = ResolveHeavyCarryForceMultiplier();
            effectiveSwimForce *= heavyCarryForceMultiplier;
            effectiveVerticalForce *= heavyCarryForceMultiplier;
            effectiveSwimForce *= externalEnvironmentalThrustMultiplier;
            effectiveVerticalForce *= math.lerp(1f, externalEnvironmentalThrustMultiplier, 0.7f);
            effectiveSwimForce *= sargassumSpeedMultiplier;
            effectiveVerticalForce *= math.lerp(1f, sargassumSpeedMultiplier, 0.55f);
            effectiveSwimForce *= shoreSwimBlend;
            effectiveVerticalForce *= math.lerp(0.45f, 1f, shoreSwimBlend);
            float transportForwardPitchInfluence = ResolveTransportForwardPitchInfluence(transportPreset);
            float transportStrafeInputScale = ResolveTransportStrafeInputScale(transportPreset);
            float transportVerticalInputScale = ResolveTransportVerticalInputScale(transportPreset);
            float transportReverseThrustScale = ResolveTransportReverseThrustScale(transportPreset);
            float transportSurfaceDiveAssistScale = ResolveTransportSurfaceDiveAssistScale(transportPreset);
            float hullStressTurnScale = ResolveHullStressTurnResponsivenessScale(transportPreset);
            transportStrafeInputScale *= hullStressTurnScale;

            float bodyYawRad = _bodyYaw * DEG_TO_RAD;
            float pitchRad = _cameraPitch * DEG_TO_RAD;

            float sinBodyYaw = math.sin(bodyYawRad);
            float cosBodyYaw = math.cos(bodyYawRad);
            float sinPitch = math.sin(pitchRad);
            float cosPitch = math.cos(pitchRad);
            float fwdX;
            float fwdY;
            float fwdZ;
            float rightX;
            float rightZ;

            if (isSurfaceSwim && !hasSurfaceDiveIntent)
            {
                Vector3 bodyForward = new Vector3(sinBodyYaw, 0f, cosBodyYaw);
                Vector3 bodyRight = new Vector3(cosBodyYaw, 0f, -sinBodyYaw);
                Vector3 surfaceNormal = EffectiveWaterSurfaceNormal;
                Vector3 surfaceForward = Vector3.ProjectOnPlane(bodyForward, surfaceNormal);
                Vector3 surfaceRight = Vector3.ProjectOnPlane(bodyRight, surfaceNormal);

                if (surfaceForward.sqrMagnitude <= 0.0001f)
                    surfaceForward = bodyForward;
                else
                    surfaceForward.Normalize();

                if (surfaceRight.sqrMagnitude <= 0.0001f)
                    surfaceRight = bodyRight;
                else
                    surfaceRight.Normalize();

                fwdX = surfaceForward.x;
                fwdY = surfaceForward.y;
                fwdZ = surfaceForward.z;
                rightX = surfaceRight.x;
                rightZ = surfaceRight.z;
            }
            else
            {
                float surfaceDepthT = isSurfaceSwim
                    ? math.saturate(_currentDepth / math.max(surfaceSwimDepthBand, 0.01f))
                    : 1f;
                float surfacePitchBlend = isSurfaceSwim
                    ? math.lerp(1f - surfaceForwardPitchSuppression, 1f, surfaceDepthT)
                    : 1f;
                surfacePitchBlend *= transportForwardPitchInfluence;

                float fwdPlanarScale = math.lerp(1f, cosPitch, surfacePitchBlend);
                fwdX = sinBodyYaw * fwdPlanarScale;
                fwdY = -sinPitch * transportForwardPitchInfluence;
                fwdZ = cosBodyYaw * fwdPlanarScale;
                rightX = cosBodyYaw;
                rightZ = -sinBodyYaw;
            }

            float forwardScale = isSurfaceSwim ? surfaceForwardForceMultiplier : 1f;
            float strafeScale = (isSurfaceSwim ? surfaceStrafeForceMultiplier : 1f) * transportStrafeInputScale;
            float forwardInput = _inputV;
            if (forwardInput < 0f)
                forwardInput *= transportReverseThrustScale;

            float forwardVelocity = _velocity.x * fwdX + _velocity.y * fwdY + _velocity.z * fwdZ;
            float transportPropulsionForce = rawTransportPropulsionForce;
            if (transportPropulsionForce > 0f)
            {
                transportPropulsionForce *= shoreSwimBlend;
                float cavitationEfficiency = ResolveTransportCavitationEfficiency(
                    fixedDeltaTime,
                    true,
                    forwardVelocity,
                    ResolveActiveTransportBoost01());
                transportPropulsionForce *= cavitationEfficiency;
            }
            else
            {
                ResolveTransportCavitationEfficiency(fixedDeltaTime, false, forwardVelocity, 0f);
            }

            float dirX = fwdX * (forwardInput * forwardScale) + rightX * (_inputH * strafeScale);
            float dirY = fwdY * (forwardInput * forwardScale);
            float dirZ = fwdZ * (forwardInput * forwardScale) + rightZ * (_inputH * strafeScale);

            float sqrMag = dirX * dirX + dirY * dirY + dirZ * dirZ;
            if (sqrMag > 1.0001f)
            {
                float invMag = 1f / math.sqrt(sqrMag);
                dirX *= invMag; dirY *= invMag; dirZ *= invMag;
            }

            float verticalInput = _inputVertical;
            if (isSurfaceSwim && verticalInput > 0f)
            {
                float ascendGate = math.saturate(_currentDepth / math.max(surfaceAscendReleaseDepth, 0.01f));
                verticalInput *= ascendGate;
            }
            verticalInput *= transportVerticalInputScale;

            _forceVector.x = dirX * effectiveSwimForce;
            _forceVector.y = dirY * effectiveSwimForce;
            _forceVector.z = dirZ * effectiveSwimForce;
            _forceVector.y += verticalInput * effectiveVerticalForce * (isSurfaceSwim ? surfaceVerticalForceMultiplier : 1f);

            if (surfaceDiveAssistActive)
            {
                float diveAssistT = math.saturate(_surfaceDiveAssistTimer / math.max(surfaceDiveAssistDuration, 0.01f));
                _forceVector.y -= effectiveVerticalForce * surfaceDiveAssistForceMultiplier * transportSurfaceDiveAssistScale * diveAssistT;
            }

            if (isSurfaceSwim && hasSurfaceDiveIntent && surfaceDiveResistanceDamping > 0f && _velocity.y < 0f)
            {
                float headDepth = GetHeadDepthBelowSurface(EffectiveWaterSurfaceY);
                float surfaceResistanceT = 1f - math.saturate(headDepth / math.max(surfaceDiveBreakDepth, 0.01f));
                if (surfaceResistanceT > 0f)
                {
                    _forceVector.y -= _velocity.y * _rb.mass * surfaceDiveResistanceDamping * surfaceResistanceT;
                }
            }

            if (transportPropulsionForce > 0f)
            {
                Vector3 transportPropulsionDirection = new Vector3(_forceVector.x, _forceVector.y, _forceVector.z);
                if (transportPropulsionDirection.sqrMagnitude <= 0.0001f)
                    transportPropulsionDirection = new Vector3(fwdX, fwdY, fwdZ);
                else
                    transportPropulsionDirection.Normalize();

                if (ResolveActiveTransportSource() is MantaScooter mantaScooter &&
                    mantaScooter.TryGetHullStressMisfireDeviation(out Vector2 misfireDeviationDegrees))
                {
                    transportPropulsionDirection = Quaternion.Euler(misfireDeviationDegrees.x, misfireDeviationDegrees.y, 0f) * transportPropulsionDirection;
                }

                if (math.abs(_abyssalTransportTurbulencePitchOffset) > 0.001f ||
                    math.abs(_abyssalTransportTurbulenceYawOffset) > 0.001f)
                {
                    transportPropulsionDirection =
                        Quaternion.Euler(_abyssalTransportTurbulencePitchOffset, _abyssalTransportTurbulenceYawOffset, 0f) *
                        transportPropulsionDirection;
                }

                _forceVector.x += transportPropulsionDirection.x * transportPropulsionForce;
                _forceVector.y += transportPropulsionDirection.y * transportPropulsionForce;
                _forceVector.z += transportPropulsionDirection.z * transportPropulsionForce;
            }

            _rb.AddForce(_forceVector, ForceMode.Force);
            ApplySargassumEntanglementForce(transportPreset);
            ApplyAbyssalCableEntanglementForce(transportPreset);
            ApplySargassumMatBuoyancySupport();

            if (isSurfaceSwim && surfaceAscendVelocityDamping > 0f && _velocity.y > 0f)
            {
                if (_velocity.y >= surfaceBreachReleaseVelocity)
                    return;

                float upwardDampingT = 1f - math.saturate(_currentDepth / math.max(surfaceAscendReleaseDepth, 0.01f));
                if (upwardDampingT > 0f)
                {
                    _forceVector.x = 0f;
                    _forceVector.y = -_velocity.y * _rb.mass * surfaceAscendVelocityDamping * upwardDampingT;
                    _forceVector.z = 0f;
                    _rb.AddForce(_forceVector, ForceMode.Force);
                }
            }
        }

        private void ApplySargassumRestRecovery(float fixedDeltaTime)
        {
            float targetBlend = 0f;
            if (_survivalSystem != null &&
                fixedDeltaTime > 0f &&
                !IsInDryInterior() &&
                _isSurfaceSwimming &&
                _wipeoutTimer <= 0f &&
                _sargassumFieldDensity01 > sargassumRestDensityThreshold &&
                _sargassumMatBuoyancyBlend > 0.05f)
            {
                float densityRange = math.max(1f - sargassumRestDensityThreshold, 0.0001f);
                float densityT = math.saturate((_sargassumFieldDensity01 - sargassumRestDensityThreshold) / densityRange);
                float speed = _rb != null ? _rb.linearVelocity.magnitude : 0f;
                float stillnessT = 1f - math.saturate(speed / math.max(sargassumRestMaxSpeed, 0.01f));
                float inputIntent = math.max(math.sqrt(_inputH * _inputH + _inputV * _inputV), math.abs(_inputVertical));
                float inputCalmT = 1f - math.saturate(inputIntent / math.max(sargassumRestMaxInputIntent, 0.01f));
                float headDepth = GetHeadDepthBelowSurface(EffectiveWaterSurfaceY);
                float breathingT = 1f - math.saturate(headDepth / math.max(sargassumRestMaxHeadDepth, 0.0001f));
                targetBlend = densityT * stillnessT * inputCalmT * breathingT * _sargassumMatBuoyancyBlend;
            }

            float blendT = 1f - math.exp(-math.max(sargassumRestBlendSharpness, 0.01f) * fixedDeltaTime);
            _sargassumRestRecoveryBlend = math.lerp(_sargassumRestRecoveryBlend, targetBlend, blendT);
            if (_sargassumRestRecoveryBlend <= 0.001f)
            {
                _sargassumRestRecoveryBlend = 0f;
                return;
            }

            if (sargassumRestOxygenRestorePerSecond > 0f)
                _survivalSystem.RefillOxygen(sargassumRestOxygenRestorePerSecond * _sargassumRestRecoveryBlend * fixedDeltaTime);

            if (sargassumRestEnergyRestorePerSecond > 0f)
                _survivalSystem.RechargeEnergy(sargassumRestEnergyRestorePerSecond * _sargassumRestRecoveryBlend * fixedDeltaTime);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  WALK PHYSICS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void WalkPhysics(SuitData suit, float fixedDeltaTime)
        {
            if (_inputH == 0f && _inputV == 0f) return;

            bool exosuitActive = _currentLocomotionMode == PlayerLocomotionMode.ExosuitLocomotion;
            bool dryInteriorActive = _currentLocomotionMode == PlayerLocomotionMode.DryInteriorWalk;

            float yawRad = _bodyYaw * DEG_TO_RAD;
            float sinYaw = math.sin(yawRad);
            float cosYaw = math.cos(yawRad);

            _moveDirection.x = sinYaw * _inputV + cosYaw * _inputH;
            _moveDirection.y = 0f;
            _moveDirection.z = cosYaw * _inputV - sinYaw * _inputH;

            float sqrMag = _moveDirection.x * _moveDirection.x + _moveDirection.z * _moveDirection.z;
            if (sqrMag > 1.0001f)
            {
                float invMag = 1f / math.sqrt(sqrMag);
                _moveDirection.x *= invMag;
                _moveDirection.z *= invMag;
            }

            if (_isGrounded)
            {
                _moveDirection = Vector3.ProjectOnPlane(_moveDirection, _smoothedGroundNormal);
                float projSqr = _moveDirection.sqrMagnitude;
                if (projSqr > 0.0001f)
                {
                    float invMag = 1f / math.sqrt(projSqr);
                    _moveDirection.x *= invMag;
                    _moveDirection.y *= invMag;
                    _moveDirection.z *= invMag;
                }
            }

            float wadeMultiplier = exosuitActive ? 1f : 1f - _waterImmersionRatio * suit.wadeSlowdownFactor;
            wadeMultiplier = math.max(wadeMultiplier, 0.2f);
            float sprintMult = CanUseLandSprint() ? suit.sprintMultiplier : 1f;
            float force =
                suit.walkForce *
                wadeMultiplier *
                sprintMult *
                ResolveHeavyCarryForceMultiplier() *
                ResolveExternalEnvironmentalThrustMultiplier();

            if (exosuitActive)
                force *= exosuitWalkForceMultiplier;
            else if (dryInteriorActive)
                force *= dryInteriorWalkForceMultiplier;

            if (IsDryLandAirborne())
                force *= dryAirControlMultiplier;

            _forceVector.x = _moveDirection.x * force;
            _forceVector.y = _moveDirection.y * force;
            _forceVector.z = _moveDirection.z * force;
            _rb.AddForce(_forceVector, ForceMode.Force);
        }

        private bool ShouldUseLandLocomotion(float physicsImmersion, bool hasShoreGroundSupport, bool hasImmediateShoreFooting)
        {
            if (IsInDryInterior())
                return true;

            if (IsExosuitTransportActive())
                return true;

            if (physicsImmersion <= 0.01f)
                return true;

            if (hasImmediateShoreFooting && _shoreBuoyancyBlend <= shoreWalkHandoffBuoyancyThreshold)
                return true;

            if (physicsImmersion >= swimTransitionThreshold)
                return false;

            return hasShoreGroundSupport && _shoreBuoyancyBlend <= shoreWalkHandoffBuoyancyThreshold;
        }

        private bool IsDryLandAirborne()
        {
            if (!_isWalking || _isGrounded)
                return false;

            if (_dryGroundGraceTimer > 0f)
                return false;

            if (_shoreGroundGraceTimer > 0f)
                return false;

            return _waterImmersionRatio <= 0.01f;
        }

        private bool CanUseLandSprint()
        {
            if (!_isSprinting || !_isWalking)
                return false;

            if (IsHeavyCarryActive())
                return false;

            if (_isGrounded)
                return true;

            if (_dryGroundGraceTimer > 0f)
                return true;

            return _shoreGroundGraceTimer > 0f;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  AMBIENT CURRENT
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ApplyAmbientCurrent(SuitData suit, float fixedDeltaTime, PlayerTransportPreset transportPreset)
        {
            float currentInfluenceScale = ResolveTransportAmbientCurrentInfluenceScale(transportPreset);
            if (currentInfluenceScale <= 0f) return;

            Vector3 currentPosition = _cachedTransform.position;
            float shoreCurrentScale = _isSurfaceSwimming
                ? math.lerp(0.15f, 1f, _shoreBuoyancyBlend)
                : 1f;

            float ambientCurrentX = 0f;
            float ambientCurrentY = 0f;
            float ambientCurrentZ = 0f;
            if (suit.ambientCurrentStrength > 0f)
            {
                float strength = suit.ambientCurrentStrength * _waterImmersionRatio * currentInfluenceScale * shoreCurrentScale;
                Unity.Mathematics.float3 phantom = CurrentManager.SampleCurrent(
                    new Unity.Mathematics.float3(currentPosition.x, currentPosition.y, currentPosition.z),
                    _currentTimer,
                    0.018f,
                    0.12f,
                    strength,
                    0.2f);
                Vector3 localVolumeCurrent = Hecton8.Physics.CurrentVolume.SampleAt(currentPosition) * (_waterImmersionRatio * currentInfluenceScale * shoreCurrentScale);
                ambientCurrentX = phantom.x + localVolumeCurrent.x;
                ambientCurrentY = phantom.y + localVolumeCurrent.y;
                ambientCurrentZ = phantom.z + localVolumeCurrent.z;
            }

            if (_abyssalThermalFlowVelocityWS.sqrMagnitude > 0.0001f)
            {
                float thermalCurrentScale = _waterImmersionRatio * currentInfluenceScale * shoreCurrentScale;
                ambientCurrentX += _abyssalThermalFlowVelocityWS.x * thermalCurrentScale;
                ambientCurrentY += _abyssalThermalFlowVelocityWS.y * thermalCurrentScale;
                ambientCurrentZ += _abyssalThermalFlowVelocityWS.z * thermalCurrentScale;
            }

            float crestFlowForceX = 0f;
            float crestFlowForceZ = 0f;
            if (_crestFlowSamplingSucceeded && crestFlowVelocityScale > 0f && crestFlowForceResponsiveness > 0f)
            {
                float inputDriftScale = ResolveCrestFlowInputAttenuation(fixedDeltaTime);
                float modeDriftScale = _isSurfaceSwimming ? _shoreBuoyancyBlend * shoreCurrentScale : 0.8f;
                float planarInputMagnitude = math.sqrt(_inputH * _inputH + _inputV * _inputV);
                bool surfaceIdleDrift = _isSurfaceSwimming && planarInputMagnitude <= crestFlowIdleInputThreshold && math.abs(_inputVertical) <= crestFlowIdleInputThreshold;
                if (surfaceIdleDrift)
                    modeDriftScale *= crestFlowSurfaceIdleBoost;
                float crestForceScale =
                    crestFlowVelocityScale *
                    crestFlowForceResponsiveness *
                    currentInfluenceScale *
                    inputDriftScale *
                    modeDriftScale;
                crestFlowForceX = EffectiveWaterFlowVelocity.x * _rb.mass * crestForceScale;
                crestFlowForceZ = EffectiveWaterFlowVelocity.z * _rb.mass * crestForceScale;
            }

            _forceVector.x = ambientCurrentX + crestFlowForceX;
            _forceVector.y = ambientCurrentY;
            _forceVector.z = ambientCurrentZ + crestFlowForceZ;
            _rb.AddForce(_forceVector, ForceMode.Force);
        }

        private float ResolveCrestFlowInputAttenuation(float fixedDeltaTime)
        {
            float desiredScale = 1f;
            float planarInputMagnitude = math.sqrt(_inputH * _inputH + _inputV * _inputV);
            float verticalInputMagnitude = math.abs(_inputVertical);
            Vector3 flowVelocity = EffectiveWaterFlowVelocity;
            float flowSqrMagnitude = flowVelocity.x * flowVelocity.x + flowVelocity.z * flowVelocity.z;

            if (flowSqrMagnitude > 0.0001f)
            {
                if (planarInputMagnitude > 0.001f)
                {
                    float yawRad = _bodyYaw * DEG_TO_RAD;
                    float sinYaw = math.sin(yawRad);
                    float cosYaw = math.cos(yawRad);

                    float desiredX = sinYaw * _inputV + cosYaw * _inputH;
                    float desiredZ = cosYaw * _inputV - sinYaw * _inputH;
                    float desiredSqrMagnitude = desiredX * desiredX + desiredZ * desiredZ;

                    if (desiredSqrMagnitude > 0.0001f)
                    {
                        float desiredInvMagnitude = 1f / math.sqrt(desiredSqrMagnitude);
                        desiredX *= desiredInvMagnitude;
                        desiredZ *= desiredInvMagnitude;

                        float flowInvMagnitude = 1f / math.sqrt(flowSqrMagnitude);
                        float flowDirX = flowVelocity.x * flowInvMagnitude;
                        float flowDirZ = flowVelocity.z * flowInvMagnitude;
                        float alignment = math.clamp(desiredX * flowDirX + desiredZ * flowDirZ, -1f, 1f);
                        float opposingFactor = math.saturate(-alignment);
                        float neutralFactor = 1f - math.abs(alignment);
                        float inputT = math.saturate(planarInputMagnitude);
                        float directionalScale = 1f - (crestFlowOppositionReduction * opposingFactor + crestFlowCrossCurrentReduction * neutralFactor) * inputT;
                        desiredScale = math.clamp(directionalScale, crestFlowInputMinimumScale, 1f);
                    }
                }

                if (verticalInputMagnitude > 0.001f)
                {
                    float verticalScale = 1f - crestFlowCrossCurrentReduction * math.saturate(verticalInputMagnitude);
                    if (verticalScale < desiredScale)
                        desiredScale = math.clamp(verticalScale, crestFlowInputMinimumScale, 1f);
                }
            }

            float blendT = 1f - math.exp(-crestFlowInputBlendSpeed * fixedDeltaTime);
            _crestFlowInputAttenuation = math.lerp(_crestFlowInputAttenuation, desiredScale, blendT);
            return _crestFlowInputAttenuation;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  VELOCITY CLAMP
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ClampVelocity(SuitData suit)
        {
            _velocity = _rb.linearVelocity;

            if (_isWalking)
            {
                bool exosuitActive = _currentLocomotionMode == PlayerLocomotionMode.ExosuitLocomotion;
                bool dryInteriorActive = _currentLocomotionMode == PlayerLocomotionMode.DryInteriorWalk;
                float maxSpd = suit.maxWalkSpeed;
                float wadeMultiplier = exosuitActive ? 1f : 1f - _waterImmersionRatio * suit.wadeSlowdownFactor;
                maxSpd *= math.max(wadeMultiplier, 0.2f);
                if (CanUseLandSprint()) maxSpd *= suit.sprintMultiplier;
                maxSpd *= ResolveHeavyCarrySpeedMultiplier();
                maxSpd *= ResolveExternalEnvironmentalSpeedMultiplier();
                if (exosuitActive)
                    maxSpd *= exosuitWalkSpeedMultiplier;
                else if (dryInteriorActive)
                    maxSpd *= dryInteriorWalkSpeedMultiplier;

                if (maxSpd > 0f)
                {
                    if (_isGrounded)
                    {
                        Vector3 planarVelocity = Vector3.ProjectOnPlane(_velocity, _smoothedGroundNormal);
                        float planarSqr = planarVelocity.sqrMagnitude;
                        float maxSqr = maxSpd * maxSpd;
                        if (planarSqr > maxSqr)
                        {
                            float scale = maxSpd / math.sqrt(planarSqr);
                            Vector3 normalVelocity = _velocity - planarVelocity;
                            planarVelocity.x *= scale;
                            planarVelocity.y *= scale;
                            planarVelocity.z *= scale;
                            _rb.linearVelocity = planarVelocity + normalVelocity;
                        }
                    }
                    else
                    {
                        float xzSqr = _velocity.x * _velocity.x + _velocity.z * _velocity.z;
                        float maxSqr = maxSpd * maxSpd;
                        if (xzSqr > maxSqr)
                        {
                            float scale = maxSpd / math.sqrt(xzSqr);
                            _velocity.x *= scale; _velocity.z *= scale;
                            _rb.linearVelocity = _velocity;
                        }
                    }
                }
            }
            else
            {
                float maxSpd = suit.maxSwimSpeed * (_runtimeSwimSpeedMultiplier * _runtimeInjurySwimSpeedMultiplier);
                if (_isSurfaceSwimming)
                {
                    maxSpd *= surfaceMaxSpeedMultiplier;
                    maxSpd *= math.lerp(0.45f, 1f, _shoreBuoyancyBlend);
                }
                if (_isSprinting && !IsHeavyCarryActive()) maxSpd *= suit.sprintMultiplier;
                maxSpd *= ResolveHeavyCarrySpeedMultiplier();
                maxSpd *= ResolveSargassumSpeedMultiplier();
                maxSpd *= ResolveExternalEnvironmentalSpeedMultiplier();
                maxSpd *= _transportCavitationEfficiency;
                maxSpd *= ResolveActiveTransportSpeedMultiplier() * ResolveWipeoutTransportControl01();
                if (maxSpd > 0f)
                {
                    float fullSqr = _velocity.x * _velocity.x + _velocity.y * _velocity.y + _velocity.z * _velocity.z;
                    float maxSqr = maxSpd * maxSpd;
                    if (fullSqr > maxSqr)
                    {
                        float scale = maxSpd / math.sqrt(fullSqr);
                        _velocity.x *= scale; _velocity.y *= scale; _velocity.z *= scale;
                        _rb.linearVelocity = _velocity;
                    }
                }
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SPRING UTILITY
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private static float SpringDamp(float current, float target, ref float velocity, float omega, float dt)
        {
            float n1 = velocity - (current - target) * (omega * omega * dt);
            float n2 = 1f + omega * dt;
            velocity = n1 / (n2 * n2);
            return current + velocity * dt;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  DIAGNOSTICS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateModeDiagnostics()
        {
            _debugIsWalking = _isWalking;
            int modeIndex = (int)_currentLocomotionMode;
            _debugLocomotionMode = (uint)modeIndex < (uint)_locomotionModeLabels.Length
                ? _locomotionModeLabels[modeIndex]
                : "Unknown";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateGroundDiagnostics() { _debugIsGrounded = _isGrounded; }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateStepDiagnostics() { _debugStepEvent = true; }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateSuitDiagnostics()
        {
            _debugSuitName = currentSuitData != null ? currentSuitData.name : "NONE";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateCrestDiagnostics()
        {
            _debugCrestAvailable = _crestAvailable;
            _debugDynamicWaterY = _dynamicWaterSurfaceY;
            _debugCrestSampling = _crestSamplingSucceeded;
            Vector3 surfaceWaveEuler = _surfaceWavePoseRotation.eulerAngles;
            _debugSurfaceWavePitch = NormalizeSignedAngle(surfaceWaveEuler.x);
            _debugSurfaceWaveRoll = NormalizeSignedAngle(surfaceWaveEuler.z);
            _debugStormIntensity01 = _dynamicStormIntensity;
            _debugWaveHeightSpan = _dynamicWaveHeightSpan;
            _debugTransportCavitationEfficiency = _transportCavitationEfficiency;
            _debugShoreBuoyancyBlend = _shoreBuoyancyBlend;
            _debugBottomClearance = float.IsPositiveInfinity(_bottomClearance) ? -1f : _bottomClearance;
            _debugWetLensIntensity = _wetLensSignalIntensity;
            _debugWaveSlopeForward = _dynamicWaveLocalSlope.y;
            _debugWaveSlopeLateral = _dynamicWaveLocalSlope.x;
            _debugUndertowIntensity = _undertowIntensity;
            _debugWipeoutTimer = _wipeoutTimer;
            _debugDynamicCollisionTuck = _dynamicCollisionTuck01;
            _debugAbyssalCurrentIntensity = _abyssalDowndraftIntensity;
            _debugHeavyTowActive = IsHeavyTowActive();
            _debugHeavyTowTension01 = IsHeavyTowActive() ? _heavyTowWinch.CurrentTension01 : 0f;
            _debugHeavyTowStress01 = IsHeavyTowActive() ? _heavyTowWinch.CurrentStress01 : 0f;
            _debugHeavyTowDragMultiplier = IsHeavyTowActive() ? _heavyTowWinch.CurrentTowDragMultiplier : 1f;
            _debugHeavyTowSignedLateralPull = IsHeavyTowActive() ? _heavyTowWinch.CurrentSignedLateralPull01 : 0f;
            _debugHeavyTowBackwardPull = IsHeavyTowActive() ? _heavyTowWinch.CurrentBackwardPull01 : 0f;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics(float speed)
        {
            _debugCurrentRoll = _juiceProcessor != null ? _juiceProcessor.CurrentRoll : 0f;
            _debugImmersionRatio = _waterImmersionRatio;
            _debugSmoothedImmersion = _smoothedImmersionRatio;
            _debugGravityScale = _gravityScale;
            _debugSnapScale = _snapScale;
            _debugBodyYaw = _bodyYaw;
            _debugCameraYaw = _cameraYaw;
            _debugSpeed = speed;
            _debugDynamicWaterY = EffectiveWaterSurfaceY;
            _debugCrestAvailable = _crestAvailable;
            _debugCrestSampling = _crestSamplingSucceeded;
            _debugDepth = _currentDepth;
            _debugFovOffset = _juiceOutput.fovOffset;
            _debugSplashThisFrame = _juiceProcessor != null && _juiceProcessor.SplashThisFrame;
            _debugExhaleThisFrame = _juiceProcessor != null && _juiceProcessor.ExhaleThisFrame;
            _debugIsSubmerged = _juiceProcessor != null && _juiceProcessor.IsSubmerged;
            _debugHeavyCarryActive = IsHeavyCarryActive();
            _debugHeavyCarryForceMultiplier = ResolveHeavyCarryForceMultiplier();
            _debugHeavyCarrySpeedMultiplier = ResolveHeavyCarrySpeedMultiplier();
            _debugSargassumSpeedMultiplier = ResolveSargassumSpeedMultiplier();
            _debugSargassumDragMultiplier = ResolveSargassumDragMultiplier();
            _debugSargassumEntangled = _sargassumMovementInfluence != null && _sargassumMovementInfluence.Entanglement01 > 0.01f;
            _debugSargassumEntanglement01 = _sargassumMovementInfluence != null ? _sargassumMovementInfluence.Entanglement01 : 0f;
            _debugSargassumFieldDensity01 = _sargassumFieldDensity01;
            _debugSargassumMatBuoyancy01 = _sargassumMatBuoyancyBlend;
            if (_sargassumMovementInfluence == null)
                _debugSargassumEntanglementDragRequest = 1f;
            _debugExternalEnvironmentalDragMultiplier = ResolveExternalEnvironmentalDragMultiplier();
            _debugExternalEnvironmentalSpeedMultiplier = ResolveExternalEnvironmentalSpeedMultiplier();
            _debugExternalEnvironmentalThrustMultiplier = ResolveExternalEnvironmentalThrustMultiplier();
            Vector3 surfaceWaveEuler = _surfaceWavePoseRotation.eulerAngles;
            _debugSurfaceWavePitch = NormalizeSignedAngle(surfaceWaveEuler.x);
            _debugSurfaceWaveRoll = NormalizeSignedAngle(surfaceWaveEuler.z);
            _debugStormIntensity01 = _dynamicStormIntensity;
            _debugWaveHeightSpan = _dynamicWaveHeightSpan;
            _debugTransportCavitationEfficiency = _transportCavitationEfficiency;
            _debugShoreBuoyancyBlend = _shoreBuoyancyBlend;
            _debugBottomClearance = float.IsPositiveInfinity(_bottomClearance) ? -1f : _bottomClearance;
            _debugWetLensIntensity = _wetLensSignalIntensity;
            _debugWaveSlopeForward = _dynamicWaveLocalSlope.y;
            _debugWaveSlopeLateral = _dynamicWaveLocalSlope.x;
            _debugUndertowIntensity = _undertowIntensity;
            _debugWipeoutTimer = _wipeoutTimer;
            _debugDynamicCollisionTuck = _dynamicCollisionTuck01;
            _debugAbyssalCurrentIntensity = _abyssalDowndraftIntensity;
            _debugHeavyTowActive = IsHeavyTowActive();
            _debugHeavyTowTension01 = IsHeavyTowActive() ? _heavyTowWinch.CurrentTension01 : 0f;
            _debugHeavyTowStress01 = IsHeavyTowActive() ? _heavyTowWinch.CurrentStress01 : 0f;
            _debugHeavyTowDragMultiplier = IsHeavyTowActive() ? _heavyTowWinch.CurrentTowDragMultiplier : 1f;
            _debugHeavyTowSignedLateralPull = IsHeavyTowActive() ? _heavyTowWinch.CurrentSignedLateralPull01 : 0f;
            _debugHeavyTowBackwardPull = IsHeavyTowActive() ? _heavyTowWinch.CurrentBackwardPull01 : 0f;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  EDITOR
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (mouseSensitivity < 0.01f) mouseSensitivity = 0.01f;
            if (groundCheckRadius < 0.01f) groundCheckRadius = 0.01f;
            if (groundCheckDistance < 0.01f) groundCheckDistance = 0.01f;
            if (dryGroundGraceTime < 0f) dryGroundGraceTime = 0f;
            if (dryGroundGraceTime > 0.3f) dryGroundGraceTime = 0.3f;
            if (maxGroundAngle < 5f) maxGroundAngle = 5f;
            if (maxGroundAngle > 89f) maxGroundAngle = 89f;
            if (pitchMin < -89.9f) pitchMin = -89.9f;
            if (pitchMax > 89.9f) pitchMax = 89.9f;
            if (pitchMin > pitchMax) pitchMin = pitchMax;
            if (playerHeight < 0.5f) playerHeight = 0.5f;
            if (baseFov < 30f) baseFov = 30f;
            if (baseFov > 120f) baseFov = 120f;
            if (exosuitJumpJetScooterDrainMultiplier < 1f) exosuitJumpJetScooterDrainMultiplier = 1f;
            if (exosuitJumpJetScooterDrainMultiplier > 10f) exosuitJumpJetScooterDrainMultiplier = 10f;
            if (exosuitNegativeBuoyancyScale < 1f) exosuitNegativeBuoyancyScale = 1f;
            if (exosuitNegativeBuoyancyScale > 3f) exosuitNegativeBuoyancyScale = 3f;
            if (exosuitFootProbeLateralOffset < 0.1f) exosuitFootProbeLateralOffset = 0.1f;
            if (exosuitFootProbeLateralOffset > 1.5f) exosuitFootProbeLateralOffset = 1.5f;
            if (exosuitFootProbeForwardOffset < -0.5f) exosuitFootProbeForwardOffset = -0.5f;
            if (exosuitFootProbeForwardOffset > 1f) exosuitFootProbeForwardOffset = 1f;
            if (exosuitFootProbeHeight < 0.05f) exosuitFootProbeHeight = 0.05f;
            if (exosuitFootProbeHeight > 1.5f) exosuitFootProbeHeight = 1.5f;
            if (exosuitFootProbeDistance < 0.1f) exosuitFootProbeDistance = 0.1f;
            if (exosuitFootProbeDistance > 3f) exosuitFootProbeDistance = 3f;
            if (exosuitMinGroundNormalY < 0.05f) exosuitMinGroundNormalY = 0.05f;
            if (exosuitMinGroundNormalY > 0.8f) exosuitMinGroundNormalY = 0.8f;
            if (exosuitFootSlopeBlendSharpness < 1f) exosuitFootSlopeBlendSharpness = 1f;
            if (exosuitFootSlopeBlendSharpness > 40f) exosuitFootSlopeBlendSharpness = 40f;
            if (exosuitSlopeStickForceMultiplier < 1f) exosuitSlopeStickForceMultiplier = 1f;
            if (exosuitSlopeStickForceMultiplier > 4f) exosuitSlopeStickForceMultiplier = 4f;
            if (exosuitGroundSnapForceMultiplier < 1f) exosuitGroundSnapForceMultiplier = 1f;
            if (exosuitGroundSnapForceMultiplier > 4f) exosuitGroundSnapForceMultiplier = 4f;
            if (dryInteriorWalkForceMultiplier < 0.1f) dryInteriorWalkForceMultiplier = 0.1f;
            if (dryInteriorWalkForceMultiplier > 2f) dryInteriorWalkForceMultiplier = 2f;
            if (dryInteriorWalkSpeedMultiplier < 0.1f) dryInteriorWalkSpeedMultiplier = 0.1f;
            if (dryInteriorWalkSpeedMultiplier > 2f) dryInteriorWalkSpeedMultiplier = 2f;
            if (dryInteriorFootProbeLateralOffset < 0.05f) dryInteriorFootProbeLateralOffset = 0.05f;
            if (dryInteriorFootProbeLateralOffset > 1f) dryInteriorFootProbeLateralOffset = 1f;
            if (dryInteriorFootProbeForwardOffset < -0.25f) dryInteriorFootProbeForwardOffset = -0.25f;
            if (dryInteriorFootProbeForwardOffset > 0.5f) dryInteriorFootProbeForwardOffset = 0.5f;
            if (dryInteriorFootProbeHeight < 0.05f) dryInteriorFootProbeHeight = 0.05f;
            if (dryInteriorFootProbeHeight > 1f) dryInteriorFootProbeHeight = 1f;
            if (dryInteriorFootProbeDistance < 0.1f) dryInteriorFootProbeDistance = 0.1f;
            if (dryInteriorFootProbeDistance > 2f) dryInteriorFootProbeDistance = 2f;
            if (cuttingTensionRestLength < 0.2f) cuttingTensionRestLength = 0.2f;
            if (cuttingTensionRestLength > 3f) cuttingTensionRestLength = 3f;
            if (cuttingTensionSpring < 0f) cuttingTensionSpring = 0f;
            if (cuttingTensionSpring > 120f) cuttingTensionSpring = 120f;
            if (cuttingTensionDamping < 0f) cuttingTensionDamping = 0f;
            if (cuttingTensionDamping > 40f) cuttingTensionDamping = 40f;
            if (cuttingTensionMaxForce < 0f) cuttingTensionMaxForce = 0f;
            if (cuttingTensionMaxForce > 120f) cuttingTensionMaxForce = 120f;
            if (cuttingTensionHoldTime < 0f) cuttingTensionHoldTime = 0f;
            if (cuttingTensionHoldTime > 0.25f) cuttingTensionHoldTime = 0.25f;
            if (fatalPressureMinFov < 15f) fatalPressureMinFov = 15f;
            if (fatalPressureMinFov > 25f) fatalPressureMinFov = 25f;
            if (wipeoutSuitUpgradeBreakChance < 0f) wipeoutSuitUpgradeBreakChance = 0f;
            if (wipeoutSuitUpgradeBreakChance > 1f) wipeoutSuitUpgradeBreakChance = 1f;
            if (fatalPressureLookSensitivityFloor < 0f) fatalPressureLookSensitivityFloor = 0f;
            if (fatalPressureLookSensitivityFloor > 0.35f) fatalPressureLookSensitivityFloor = 0.35f;
            if (fatalPressureYawFreedomStart < 5f) fatalPressureYawFreedomStart = 5f;
            if (fatalPressureYawFreedomEnd < 1f) fatalPressureYawFreedomEnd = 1f;
            if (fatalPressureYawFreedomStart < fatalPressureYawFreedomEnd) fatalPressureYawFreedomStart = fatalPressureYawFreedomEnd;
            if (fatalPressurePitchFreedomStart < 5f) fatalPressurePitchFreedomStart = 5f;
            if (fatalPressurePitchFreedomEnd < 1f) fatalPressurePitchFreedomEnd = 1f;
            if (fatalPressurePitchFreedomStart < fatalPressurePitchFreedomEnd) fatalPressurePitchFreedomStart = fatalPressurePitchFreedomEnd;
            if (surfaceSwimDepthBand < 0.1f) surfaceSwimDepthBand = 0.1f;
            if (surfaceAscendReleaseDepth < 0.02f) surfaceAscendReleaseDepth = 0.02f;
            if (surfaceDivePitchCommit < 0f) surfaceDivePitchCommit = 0f;
            if (surfaceDivePitchCommit > 85f) surfaceDivePitchCommit = 85f;
            if (surfaceDiveForwardCommit < 0f) surfaceDiveForwardCommit = 0f;
            if (surfaceDiveForwardCommit > 1f) surfaceDiveForwardCommit = 1f;
            if (surfaceDiveCommitHoldTime < 0f) surfaceDiveCommitHoldTime = 0f;
            if (surfaceDiveCommitHoldTime > 0.35f) surfaceDiveCommitHoldTime = 0.35f;
            if (surfaceDiveAssistDuration < 0.04f) surfaceDiveAssistDuration = 0.04f;
            if (surfaceDiveAssistForceMultiplier < 0f) surfaceDiveAssistForceMultiplier = 0f;
            if (surfaceSnapEngageSpeed < 1f) surfaceSnapEngageSpeed = 1f;
            if (surfaceSnapReleaseSpeed < 1f) surfaceSnapReleaseSpeed = 1f;
            if (surfaceWaveFollowSharpness < 1f) surfaceWaveFollowSharpness = 1f;
            if (surfaceDiveBreakDepth < 0.05f) surfaceDiveBreakDepth = 0.05f;
            if (surfaceHeadReattachDepth < 0f) surfaceHeadReattachDepth = 0f;
            if (surfaceHeadReattachDepth > surfaceDiveBreakDepth) surfaceHeadReattachDepth = surfaceDiveBreakDepth;
            if (surfaceBreachReleaseVelocity < 0.5f) surfaceBreachReleaseVelocity = 0.5f;
            if (surfaceBreachLockDuration < 0.05f) surfaceBreachLockDuration = 0.05f;
            if (surfaceDiveResistanceDamping < 0f) surfaceDiveResistanceDamping = 0f;
            if (surfaceWaveVelocityInfluence < 0f) surfaceWaveVelocityInfluence = 0f;
            if (surfaceWaveVelocityInfluence > 1f) surfaceWaveVelocityInfluence = 1f;
            if (shoreWalkFootDepth < 0.05f) shoreWalkFootDepth = 0.05f;
            if (shoreBuoyancyRecoveryClearance < shoreWalkFootDepth) shoreBuoyancyRecoveryClearance = shoreWalkFootDepth;
            if (shoreBuoyancyBlendSharpness < 1f) shoreBuoyancyBlendSharpness = 1f;
            if (shoreWalkHandoffBuoyancyThreshold < 0f) shoreWalkHandoffBuoyancyThreshold = 0f;
            if (shoreWalkHandoffBuoyancyThreshold > 1f) shoreWalkHandoffBuoyancyThreshold = 1f;
            if (waterEntryImpactMinSpeed < 0f) waterEntryImpactMinSpeed = 0f;
            if (waterEntryImpactDamping < 0f) waterEntryImpactDamping = 0f;
            if (waterEntryImpactDuration < 0.1f) waterEntryImpactDuration = 0.1f;
            if (waterEntryImpactFovExpand < 0f) waterEntryImpactFovExpand = 0f;
            if (waterEntryImpactFovCompress < 0f) waterEntryImpactFovCompress = 0f;
            if (surfacePierceSplashMinSpeed < 0f) surfacePierceSplashMinSpeed = 0f;
            if (surfacePierceSplashMaxSpeed < surfacePierceSplashMinSpeed)
                surfacePierceSplashMaxSpeed = surfacePierceSplashMinSpeed;
            if (surfacePierceSplashMinVolume < 0f) surfacePierceSplashMinVolume = 0f;
            if (surfacePierceSplashMinVolume > 1f) surfacePierceSplashMinVolume = 1f;
            if (surfacePierceSplashMaxVolume < 0f) surfacePierceSplashMaxVolume = 0f;
            if (surfacePierceSplashMaxVolume > 1f) surfacePierceSplashMaxVolume = 1f;
            if (surfacePierceSplashMinVolume > surfacePierceSplashMaxVolume)
                surfacePierceSplashMinVolume = surfacePierceSplashMaxVolume;
            if (wetLensSignalRecoverySpeed < 0.25f) wetLensSignalRecoverySpeed = 0.25f;
            if (wetLensStormIntensityThreshold < 0f) wetLensStormIntensityThreshold = 0f;
            if (wetLensStormIntensityThreshold > 1f) wetLensStormIntensityThreshold = 1f;
            if (wetLensWaveCoverDepth < 0f) wetLensWaveCoverDepth = 0f;
            if (wetLensWaveCoverDepth > 0.25f) wetLensWaveCoverDepth = 0.25f;
            if (wetLensStormPulseCooldown < 0.05f) wetLensStormPulseCooldown = 0.05f;
            if (wetLensStormPulseCooldown > 1f) wetLensStormPulseCooldown = 1f;
            if (wetLensStormPulseIntensity < 0f) wetLensStormPulseIntensity = 0f;
            if (wetLensStormPulseIntensity > 1f) wetLensStormPulseIntensity = 1f;
            if (wetLensBreachPulseIntensity < 0f) wetLensBreachPulseIntensity = 0f;
            if (wetLensBreachPulseIntensity > 1f) wetLensBreachPulseIntensity = 1f;
            if (shoreUndertowStormThreshold < 0f) shoreUndertowStormThreshold = 0f;
            if (shoreUndertowStormThreshold > 1f) shoreUndertowStormThreshold = 1f;
            if (shoreUndertowMaxDepth < 0.1f) shoreUndertowMaxDepth = 0.1f;
            if (shoreUndertowRetreatVelocityStart < 0.05f) shoreUndertowRetreatVelocityStart = 0.05f;
            if (shoreUndertowRetreatVelocityMax < shoreUndertowRetreatVelocityStart)
                shoreUndertowRetreatVelocityMax = shoreUndertowRetreatVelocityStart;
            if (shoreUndertowForce < 0f) shoreUndertowForce = 0f;
            if (shoreUndertowSurfaceBoost < 1f) shoreUndertowSurfaceBoost = 1f;
            if (shoreUndertowMinFeetDepth < 0.05f) shoreUndertowMinFeetDepth = 0.05f;
            if (shoreUndertowFullFeetDepth < shoreUndertowMinFeetDepth)
                shoreUndertowFullFeetDepth = shoreUndertowMinFeetDepth;
            if (wipeoutImpactDeltaVelocityThreshold < 1f) wipeoutImpactDeltaVelocityThreshold = 1f;
            if (wipeoutImpactDeltaVelocityMax < wipeoutImpactDeltaVelocityThreshold + 0.01f)
                wipeoutImpactDeltaVelocityMax = wipeoutImpactDeltaVelocityThreshold + 0.01f;
            if (wipeoutDuration < 0.5f) wipeoutDuration = 0.5f;
            if (wipeoutRecoveryDrag < 0f) wipeoutRecoveryDrag = 0f;
            if (wipeoutReboundImpulse < 0f) wipeoutReboundImpulse = 0f;
            if (wipeoutTransportDamageScale < 1f) wipeoutTransportDamageScale = 1f;
            if (wipeoutBreachLandingGraceTime < 0.1f) wipeoutBreachLandingGraceTime = 0.1f;
            if (surfaceBreachDepthWindow < SurfaceStateUtility.ExitUnderwaterDepth)
                surfaceBreachDepthWindow = SurfaceStateUtility.ExitUnderwaterDepth;
            if (surfaceBreachMinImmersion >= 0.98f)
                surfaceBreachMinImmersion = 0.97f;
            if (crestFlowVelocityScale < 0f) crestFlowVelocityScale = 0f;
            if (crestFlowForceResponsiveness < 0f) crestFlowForceResponsiveness = 0f;
            if (crestFlowOppositionReduction < 0f) crestFlowOppositionReduction = 0f;
            if (crestFlowOppositionReduction > 1f) crestFlowOppositionReduction = 1f;
            if (crestFlowCrossCurrentReduction < 0f) crestFlowCrossCurrentReduction = 0f;
            if (crestFlowCrossCurrentReduction > 1f) crestFlowCrossCurrentReduction = 1f;
            if (crestFlowInputMinimumScale < 0f) crestFlowInputMinimumScale = 0f;
            if (crestFlowInputMinimumScale > 1f) crestFlowInputMinimumScale = 1f;
            if (crestFlowInputBlendSpeed < 0.5f) crestFlowInputBlendSpeed = 0.5f;
            if (crestFlowSurfaceIdleBoost < 1f) crestFlowSurfaceIdleBoost = 1f;
            if (crestFlowIdleInputThreshold < 0f) crestFlowIdleInputThreshold = 0f;
            if (crestFlowIdleInputThreshold > 0.35f) crestFlowIdleInputThreshold = 0.35f;
            if (crestBodySampleMinLength < 0f) crestBodySampleMinLength = 0f;
            if (crestBodyForwardSampleDistance < 0.15f) crestBodyForwardSampleDistance = 0.15f;
            if (crestBodyLateralSampleDistance < 0.1f) crestBodyLateralSampleDistance = 0.1f;
            if (surfaceWaveAlignmentSharpness < 1f) surfaceWaveAlignmentSharpness = 1f;
            if (surfaceWaveMaxPitch < 0f) surfaceWaveMaxPitch = 0f;
            if (surfaceWaveMaxRoll < 0f) surfaceWaveMaxRoll = 0f;
            if (underwaterTurbulenceMaxDepth < 1f) underwaterTurbulenceMaxDepth = 1f;
            if (underwaterTurbulenceHeightStart < 0.05f) underwaterTurbulenceHeightStart = 0.05f;
            if (underwaterTurbulenceHeightMax < underwaterTurbulenceHeightStart)
                underwaterTurbulenceHeightMax = underwaterTurbulenceHeightStart;
            if (underwaterTurbulenceDisplacementStart < 0.05f) underwaterTurbulenceDisplacementStart = 0.05f;
            if (underwaterTurbulenceDisplacementMax < underwaterTurbulenceDisplacementStart)
                underwaterTurbulenceDisplacementMax = underwaterTurbulenceDisplacementStart;
            if (underwaterTurbulenceVelocityMax < 0.1f) underwaterTurbulenceVelocityMax = 0.1f;
            if (underwaterTurbulenceForce < 0f) underwaterTurbulenceForce = 0f;
            if (underwaterTurbulenceVerticalForce < 0f) underwaterTurbulenceVerticalForce = 0f;
            if (underwaterTurbulenceFrequency < 0.1f) underwaterTurbulenceFrequency = 0.1f;
            if (underwaterTurbulencePitch < 0f) underwaterTurbulencePitch = 0f;
            if (underwaterTurbulenceRoll < 0f) underwaterTurbulenceRoll = 0f;
            if (underwaterTurbulencePoseSharpness < 1f) underwaterTurbulencePoseSharpness = 1f;
            if (underwaterTurbulenceBottomInfluenceDepth < 0.1f) underwaterTurbulenceBottomInfluenceDepth = 0.1f;
            if (underwaterTurbulenceBottomBoost < 1f) underwaterTurbulenceBottomBoost = 1f;
            if (underwaterStressSignalThreshold < 0f) underwaterStressSignalThreshold = 0f;
            if (underwaterStressSignalThreshold > 1f) underwaterStressSignalThreshold = 1f;
            if (underwaterStressSignalBlendSharpness < 1f) underwaterStressSignalBlendSharpness = 1f;
            if (abyssalTransportTurbulenceTorqueVelocityChange < 0f) abyssalTransportTurbulenceTorqueVelocityChange = 0f;
            if (abyssalTransportTurbulencePitchDegrees < 0f) abyssalTransportTurbulencePitchDegrees = 0f;
            if (abyssalTransportTurbulenceYawDegrees < 0f) abyssalTransportTurbulenceYawDegrees = 0f;
            if (abyssalTransportTurbulenceRecoverySharpness < 1f) abyssalTransportTurbulenceRecoverySharpness = 1f;
            if (transportCavitationStartDepth < 0.1f) transportCavitationStartDepth = 0.1f;
            if (transportCavitationRecoveryDepth < transportCavitationStartDepth)
                transportCavitationRecoveryDepth = transportCavitationStartDepth;
            if (transportCavitationAccelerationStart < 0f) transportCavitationAccelerationStart = 0f;
            if (transportCavitationAccelerationMax < transportCavitationAccelerationStart + 0.01f)
                transportCavitationAccelerationMax = transportCavitationAccelerationStart + 0.01f;
            if (transportCavitationMinEfficiency < 0.05f) transportCavitationMinEfficiency = 0.05f;
            if (transportCavitationMinEfficiency > 1f) transportCavitationMinEfficiency = 1f;
            if (transportCavitationBlendSharpness < 1f) transportCavitationBlendSharpness = 1f;
            if (externalEnvironmentalDragHoldTime < 0f) externalEnvironmentalDragHoldTime = 0f;
            if (externalEnvironmentalDragHoldTime > 0.35f) externalEnvironmentalDragHoldTime = 0.35f;
            if (externalEnvironmentalDragBlendSpeed < 1f) externalEnvironmentalDragBlendSpeed = 1f;
            if (parasiteLatchInfluenceHoldTime < 0f) parasiteLatchInfluenceHoldTime = 0f;
            if (parasiteLatchInfluenceHoldTime > 0.35f) parasiteLatchInfluenceHoldTime = 0.35f;
            if (parasiteLatchInfluenceBlendSpeed < 1f) parasiteLatchInfluenceBlendSpeed = 1f;
            if (parasiteCenterOfMassForce < 0f) parasiteCenterOfMassForce = 0f;
            if (parasiteCenterOfMassForce > 80f) parasiteCenterOfMassForce = 80f;
            if (parasiteHarvesterPullForce < 0f) parasiteHarvesterPullForce = 0f;
            if (parasiteHarvesterPullForce > 120f) parasiteHarvesterPullForce = 120f;
            if (parasiteLatchCountForFullForce < 1f) parasiteLatchCountForFullForce = 1f;
            if (parasiteLatchCountForFullForce > 64f) parasiteLatchCountForFullForce = 64f;
            if (sargassumEntanglementMassReference < 40f) sargassumEntanglementMassReference = 40f;
            if (sargassumEntanglementMassReference > 500f) sargassumEntanglementMassReference = 500f;
            if (sargassumEntanglementSwimEnvironmentalDrag < 0f) sargassumEntanglementSwimEnvironmentalDrag = 0f;
            if (sargassumEntanglementSwimEnvironmentalDrag > 3f) sargassumEntanglementSwimEnvironmentalDrag = 3f;
            if (sargassumEntanglementTransportEnvironmentalDrag < 0f) sargassumEntanglementTransportEnvironmentalDrag = 0f;
            if (sargassumEntanglementTransportEnvironmentalDrag > 4f) sargassumEntanglementTransportEnvironmentalDrag = 4f;
            if (sargassumEntanglementEscapeRelief < 0f) sargassumEntanglementEscapeRelief = 0f;
            if (sargassumEntanglementEscapeRelief > 1f) sargassumEntanglementEscapeRelief = 1f;
            if (sargassumEscapeEnergyDrainPerSecond < 0f) sargassumEscapeEnergyDrainPerSecond = 0f;
            if (sargassumEscapeEnergyDrainPerSecond > 10f) sargassumEscapeEnergyDrainPerSecond = 10f;
            if (sargassumEntanglementEscapeEnergyMultiplier < 1f) sargassumEntanglementEscapeEnergyMultiplier = 1f;
            if (sargassumEntanglementEscapeEnergyMultiplier > 6f) sargassumEntanglementEscapeEnergyMultiplier = 6f;
            if (sargassumEscapeInputThreshold < 0f) sargassumEscapeInputThreshold = 0f;
            if (sargassumEscapeInputThreshold > 1f) sargassumEscapeInputThreshold = 1f;
            if (sargassumHighStrainThreshold < 0f) sargassumHighStrainThreshold = 0f;
            if (sargassumHighStrainThreshold > 1f) sargassumHighStrainThreshold = 1f;
            if (sargassumHighStrainShakeBoost < 1f) sargassumHighStrainShakeBoost = 1f;
            if (sargassumHighStrainShakeBoost > 4f) sargassumHighStrainShakeBoost = 4f;
            if (sargassumHighStrainEnergyMultiplier < 1f) sargassumHighStrainEnergyMultiplier = 1f;
            if (sargassumHighStrainEnergyMultiplier > 6f) sargassumHighStrainEnergyMultiplier = 6f;
            if (sargassumHighStrainHoldTime < 0f) sargassumHighStrainHoldTime = 0f;
            if (sargassumHighStrainHoldTime > 0.5f) sargassumHighStrainHoldTime = 0.5f;
            if (abyssalCableEntanglementSpring < 0f) abyssalCableEntanglementSpring = 0f;
            if (abyssalCableEntanglementSpring > 80f) abyssalCableEntanglementSpring = 80f;
            if (abyssalCableEntanglementDamping < 0f) abyssalCableEntanglementDamping = 0f;
            if (abyssalCableEntanglementDamping > 30f) abyssalCableEntanglementDamping = 30f;
            if (abyssalCableEntanglementVerticalInfluence < 0f) abyssalCableEntanglementVerticalInfluence = 0f;
            if (abyssalCableEntanglementVerticalInfluence > 1f) abyssalCableEntanglementVerticalInfluence = 1f;
            if (abyssalCableEntanglementSwimEnvironmentalDrag < 0f) abyssalCableEntanglementSwimEnvironmentalDrag = 0f;
            if (abyssalCableEntanglementSwimEnvironmentalDrag > 5f) abyssalCableEntanglementSwimEnvironmentalDrag = 5f;
            if (abyssalCableEntanglementTransportEnvironmentalDrag < 0f) abyssalCableEntanglementTransportEnvironmentalDrag = 0f;
            if (abyssalCableEntanglementTransportEnvironmentalDrag > 8f) abyssalCableEntanglementTransportEnvironmentalDrag = 8f;
            if (abyssalCableEscapeEnergyDrainPerSecond < 0f) abyssalCableEscapeEnergyDrainPerSecond = 0f;
            if (abyssalCableEscapeEnergyDrainPerSecond > 20f) abyssalCableEscapeEnergyDrainPerSecond = 20f;
            if (abyssalCableEscapeEnergyMultiplier < 1f) abyssalCableEscapeEnergyMultiplier = 1f;
            if (abyssalCableEscapeEnergyMultiplier > 8f) abyssalCableEscapeEnergyMultiplier = 8f;
            if (abyssalCableCutReleaseThreshold < 0f) abyssalCableCutReleaseThreshold = 0f;
            if (abyssalCableCutReleaseThreshold > 1f) abyssalCableCutReleaseThreshold = 1f;
            if (abyssalCablePropulsionReliefAtFullCut < 0f) abyssalCablePropulsionReliefAtFullCut = 0f;
            if (abyssalCablePropulsionReliefAtFullCut > 1f) abyssalCablePropulsionReliefAtFullCut = 1f;
            if (sargassumMatBuoyancyDensityThreshold < 0f) sargassumMatBuoyancyDensityThreshold = 0f;
            if (sargassumMatBuoyancyDensityThreshold > 1f) sargassumMatBuoyancyDensityThreshold = 1f;
            if (sargassumMatBuoyancyMaxDepth < 0.1f) sargassumMatBuoyancyMaxDepth = 0.1f;
            if (sargassumMatBuoyancyBlendSharpness < 1f) sargassumMatBuoyancyBlendSharpness = 1f;
            if (sargassumMatBuoyancyForceScale < 0f) sargassumMatBuoyancyForceScale = 0f;
            if (sargassumMatBuoyancyForceScale > 2.5f) sargassumMatBuoyancyForceScale = 2.5f;
            if (sargassumMatSurfaceLockBoost < 1f) sargassumMatSurfaceLockBoost = 1f;
            if (sargassumMatSurfaceLockBoost > 3f) sargassumMatSurfaceLockBoost = 3f;
            if (sargassumMatSurfaceLiftOffset < 0f) sargassumMatSurfaceLiftOffset = 0f;
            if (sargassumMatSurfaceLiftOffset > 0.75f) sargassumMatSurfaceLiftOffset = 0.75f;
            if (impactBubbleMinIntensity < 0f) impactBubbleMinIntensity = 0f;
            if (impactBubbleMinIntensity > 1f) impactBubbleMinIntensity = 1f;
            if (impactBubbleMinCount < 0) impactBubbleMinCount = 0;
            if (impactBubbleMaxCount < impactBubbleMinCount) impactBubbleMaxCount = impactBubbleMinCount;
            if (underwaterImpactMinVolume < 0f) underwaterImpactMinVolume = 0f;
            if (underwaterImpactMinVolume > 1f) underwaterImpactMinVolume = 1f;
            if (underwaterImpactMaxVolume < 0f) underwaterImpactMaxVolume = 0f;
            if (underwaterImpactMaxVolume > 1f) underwaterImpactMaxVolume = 1f;
            if (underwaterImpactMinVolume > underwaterImpactMaxVolume) underwaterImpactMinVolume = underwaterImpactMaxVolume;
            if (maxHeavyCarryBodyYawSpringMultiplier < 0.1f) maxHeavyCarryBodyYawSpringMultiplier = 0.1f;
            if (maxHeavyCarryBodyYawSpringMultiplier > 1f) maxHeavyCarryBodyYawSpringMultiplier = 1f;
            if (heavyTowCameraPitchDegrees < 0f) heavyTowCameraPitchDegrees = 0f;
            if (heavyTowCameraRollDegrees < 0f) heavyTowCameraRollDegrees = 0f;
            if (heavyTowCameraBackwardOffset < 0f) heavyTowCameraBackwardOffset = 0f;
            if (heavyTowCameraSideOffset < 0f) heavyTowCameraSideOffset = 0f;
            if (heavyTowResponseBlendSharpness < 1f) heavyTowResponseBlendSharpness = 1f;
            if (heavyTowCenterOfMassRearShift < 0f) heavyTowCenterOfMassRearShift = 0f;
            if (heavyTowCenterOfMassLateralShift < 0f) heavyTowCenterOfMassLateralShift = 0f;
            if (heavyTowCenterOfMassDownShift < 0f) heavyTowCenterOfMassDownShift = 0f;

            TryAssignEditorAuthoringDefaults();

            RefreshGroundSlopeCache();
            CacheBaseCollisionProfile();
        }

        private void TryAssignEditorAuthoringDefaults()
        {
            if (waterEntrySplashClip == null)
                waterEntrySplashClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultWaterEntrySplashClipPath);

            if (waterExitSplashClip == null)
                waterExitSplashClip = waterEntrySplashClip;
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            Vector3 bodyPos = transform.position;
            float bodyBottomY = GetBodyBottomY();
            Vector3 origin = new Vector3(bodyPos.x, bodyBottomY + groundCheckRadius + GroundCheckSkin, bodyPos.z);
            Vector3 castEnd = origin + Vector3.down * (groundCheckDistance + GroundCheckSkin);

            // Water level
            float effectiveY = EffectiveWaterSurfaceY;
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            Vector3 waterCenter = transform.position;
            waterCenter.y = effectiveY;
            Gizmos.DrawWireCube(waterCenter, new Vector3(6f, 0.02f, 6f));

            // Immersion indicator
            if (_waterImmersionRatio > 0.01f)
            {
                Gizmos.color = new Color(0f, 0.3f, 1f, 0.5f);
                float immersedHeight = playerHeight * _waterImmersionRatio;
                Vector3 immCenter = transform.position;
                immCenter.y += immersedHeight * 0.5f;
                Gizmos.DrawWireCube(immCenter, new Vector3(0.5f, immersedHeight, 0.5f));
            }

            if (_isGrounded)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
                Gizmos.DrawWireSphere(_groundHit.point, groundCheckRadius);
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(_groundHit.point, _groundHit.point + _groundHit.normal * 1.5f);

                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(_groundHit.point,
                    _groundHit.point + _smoothedGroundNormal * 1.2f);
            }
            else
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                Gizmos.DrawWireSphere(castEnd, groundCheckRadius);
            }

            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawLine(origin, castEnd);

            // Body vs camera yaw
            if (!_isWalking)
            {
                Vector3 pos = transform.position + Vector3.up * 1.5f;
                float camR = _cameraYaw * DEG_TO_RAD;
                float bodR = _bodyYaw * DEG_TO_RAD;
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pos, pos + new Vector3(math.sin(camR), 0f, math.cos(camR)) * 2f);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(pos, pos + new Vector3(math.sin(bodR), 0f, math.cos(bodR)) * 1.5f);
            }

            // Depth indicator
            if (_currentDepth > 0.5f)
            {
                Gizmos.color = new Color(0f, 0f, 0.8f, 0.4f);
                Vector3 depthStart = transform.position;
                depthStart.y = effectiveY;
                Gizmos.DrawLine(depthStart, transform.position);
            }
        }
#endif
    }
}
