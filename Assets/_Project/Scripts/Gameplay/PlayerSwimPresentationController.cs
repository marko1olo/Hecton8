using System;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Profile-driven first-person swim presentation owner.
    /// Resolves presentation mode, stroke cadence, propulsion pulse, and future viewmodel guide poses from locomotion truth.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Player Swim Presentation Controller")]
    public sealed class PlayerSwimPresentationController : MonoBehaviour, ILateFrameTickable, IColdTickable, IGlobalRegistryHotSwapListener
    {
        private const float Pi = 3.14159265359f;
        private const float TwoPi = 6.28318530718f;
        private const float InvTwoPi = 0.15915494f;
        private const float HalfPi = 1.57079632679f;
        private const float DegreesToRadians = 0.01745329252f;
        private const float UtilitySuitMassThreshold = 120f;
        private const float HeavySuitMassThreshold = 220f;
        private const string LeftGuideName = "Swim_LeftGuide";
        private const string RightGuideName = "Swim_RightGuide";
        private const int MaxGuideHierarchySearchDepth = 64;
        private const int MaxGuideHierarchySearchNodes = 512;
        private const int MaxGuideChildScanCount = 128;
        private const int ReferenceResolveRetryFrameInterval = 8;
        private const int HandObstacleWallContactMaxPhysicsFrameAge = 2;
        private const float HandObstaclePlaneEpsilon = 0.0001f;
        private static readonly int _WaveSlopeForwardShaderId = Shader.PropertyToID("_H8SwimWaveSlopeForward");
        private static readonly int _WaveSlopeLateralShaderId = Shader.PropertyToID("_H8SwimWaveSlopeLateral");
        private static readonly int _WaveSlopeXShaderId = Shader.PropertyToID("_H8SwimWaveSlopeX");
        private static readonly int _WaveSlopeZShaderId = Shader.PropertyToID("_H8SwimWaveSlopeZ");
        private static readonly int _WaveCrestReachShaderId = Shader.PropertyToID("_H8SwimWaveCrestReach");
        private static readonly int _WaveDescentTuckShaderId = Shader.PropertyToID("_H8SwimWaveDescentTuck");
        private static readonly int _WaveLeanWeightShaderId = Shader.PropertyToID("_H8SwimWaveLeanWeight");
        private static readonly int _ImmersionDepthShaderId = Shader.PropertyToID("_H8SwimImmersionDepth");
        private static readonly int _BreathingPhaseShaderId = Shader.PropertyToID("_BreathingPhase");
        private static readonly int _SwimVatSpeedScalarShaderId = Shader.PropertyToID("_HectonSwimVatSpeedScalar");

        private static readonly string[] s_modeLabels =
        {
            "None",
            "Dry",
            "ShallowWade",
            "SurfaceTread",
            "SurfaceStroke",
            "UnderwaterNeutral",
            "UnderwaterStroke",
            "UnderwaterGlide",
            "UnderwaterSprint"
        }; // COLD ALLOC: string[9] — editor diagnostics labels — owner: PlayerSwimPresentationController

        [System.Serializable]
        private struct SuitPresentationBinding
        {
            [Tooltip("Suit asset that should drive this swim presentation profile.")]
            public SuitData suit;

            [Tooltip("Presentation profile used when this suit is active.")]
            public SwimPresentationProfile profile;

            public SuitPresentationBinding(SuitData suit, SwimPresentationProfile profile)
            {
                this.suit = suit;
                this.profile = profile;
            }
        }

        [Header("-- References ----------------------------")]
        [Tooltip("Resolved movement owner. Required for locomotion truth.")]
        [SerializeField] private HectonPlayerMovement playerMovement;

        [Tooltip("Optional tool owner. When a held tool is active, swim-body presentation can be reduced to avoid rig fighting.")]
        [SerializeField] private PlayerToolManager playerToolManager;

        [Tooltip("Optional transport owner. Preferred over direct tool probing when available.")]
        [SerializeField] private PlayerTransportCoordinator playerTransportCoordinator;

        [Tooltip("Optional render-layer slave that visualizes the current swim presentation with near-camera blockout arms.")]
        [SerializeField] private PlayerSwimBlockoutRig swimBlockoutRig;

        [Tooltip("Procedural Burst/Vault owner that receives Crest-derived wave-slope parameters for spine leaning and IK.")]
        [SerializeField] private MonoBehaviour kineticMatrixRuntime;
        private IKineticCharacterPresentationSink _kineticMatrixSink;

        [Tooltip("Future swim viewmodel root driven by this controller.")]
        [SerializeField] private Transform viewModelRoot;

        [Tooltip("Optional left hand guide under the swim viewmodel root.")]
        [SerializeField] private Transform leftHandGuide;

        [Tooltip("Optional right hand guide under the swim viewmodel root.")]
        [SerializeField] private Transform rightHandGuide;

        [Header("-- Profiles ------------------------------")]
        [Tooltip("Optional data-owned profile library. Preferred over prefab-local suit binding authoring.")]
        [SerializeField] private SwimPresentationProfileLibrary profileLibrary;

        [Tooltip("Fallback profile for light / standard suits when no explicit binding exists.")]
        [SerializeField] private SwimPresentationProfile fallbackLightProfile;

        [Tooltip("Fallback profile for technical / utility suits when no explicit binding exists.")]
        [SerializeField] private SwimPresentationProfile fallbackUtilityProfile;

        [Tooltip("Fallback profile for heavy suits when no explicit binding exists.")]
        [SerializeField] private SwimPresentationProfile fallbackHeavyProfile;

        [Tooltip("Optional per-suit overrides. Keeps swim presentation authoring outside SuitData.")]
        [SerializeField] private SuitPresentationBinding[] suitBindings;

        [Header("-- Tuning --------------------------------")]
        [Tooltip("How quickly presentation intensity follows active movement.")]
        [SerializeField, Range(1f, 20f)] private float presentationBlendSpeed = 7f;

        [Tooltip("How strongly camera/body yaw disagreement feeds future viewmodel lag.")]
        [SerializeField, Range(0f, 1f)] private float bodyYawLagInfluence = 0.6f;

        [Tooltip("How strongly vertical speed adds lift/drop feel to the future viewmodel.")]
        [SerializeField, Range(0f, 0.08f)] private float verticalVelocityInfluence = 0.01f;

        [Tooltip("How strongly vertical swim velocity drives ascend / descend-specific hand posing.")]
        [SerializeField, Range(0f, 1.5f)] private float verticalPoseInfluence = 0.7f;

        [Tooltip("Forward reach clamp as a fraction of authored hand reach distance. Prevents hands from flying too far ahead of the camera.")]
        [SerializeField, Range(0.1f, 1f)] private float handForwardReachClamp = 0.55f;

        [Tooltip("Extra rear clamp as a fraction of authored pull distance. Keeps hands readable instead of collapsing fully into the visor.")]
        [SerializeField, Range(0.1f, 1.5f)] private float handRearReachClamp = 0.95f;

        [Tooltip("How much ascending pulls the hands back toward the torso instead of letting them spear forward.")]
        [SerializeField, Range(0f, 1f)] private float ascendPullbackBias = 0.36f;

        [Tooltip("How much descending lets the hands commit forward into the water column.")]
        [SerializeField, Range(0f, 1f)] private float descendReachBias = 0.22f;

        [Tooltip("How much swim-body presentation remains visible while a held tool is armed. 0 = fully suppressed, 1 = no suppression.")]
        [SerializeField, Range(0f, 1f)] private float equippedToolPresentationWeight = 0.2f;

        [Tooltip("How much root-level swim presentation remains visible while a held tool is armed.")]
        [SerializeField, Range(0f, 1f)] private float equippedToolRootPresentationWeight = 0.42f;

        [Tooltip("How much of the non-tool support hand remains visible while a held tool is armed.")]
        [SerializeField, Range(0f, 1f)] private float equippedToolSupportHandWeight = 0.68f;

        [Tooltip("Extra support-hand visibility while the equipped tool is actively being used.")]
        [SerializeField, Range(0f, 0.5f)] private float equippedToolActiveUseSupportBoost = 0.16f;

        [Tooltip("Which hand is considered owned by the active near-camera tool rig.")]
        [SerializeField] private PlayerToolSwimHandedness equippedToolHand = PlayerToolSwimHandedness.Right;

        [Header("-- Framing ------------------------------")]
        [Tooltip("How strongly surface swim pushes the hands outward toward the screen corners for horizon readability.")]
        [SerializeField, Range(0f, 0.1f)] private float surfaceFramingOutwardBias = 0.032f;

        [Tooltip("How strongly surface swim lowers the hands in frame so they do not crowd the horizon line.")]
        [SerializeField, Range(0f, 0.12f)] private float surfaceFramingDownBias = 0.036f;

        [Tooltip("How strongly surface swim pulls the hands slightly rearward to keep the whole arm visible.")]
        [SerializeField, Range(0f, 0.08f)] private float surfaceFramingRearBias = 0.018f;

        [Tooltip("How strongly an armed tool pushes the hands outward toward the screen corners.")]
        [SerializeField, Range(0f, 0.1f)] private float toolFramingOutwardBias = 0.026f;

        [Tooltip("How strongly an armed tool lowers the hands in frame to preserve view readability.")]
        [SerializeField, Range(0f, 0.12f)] private float toolFramingDownBias = 0.02f;

        [Tooltip("How strongly an armed tool pulls the hands slightly rearward to keep the body-connected silhouette visible.")]
        [SerializeField, Range(0f, 0.08f)] private float toolFramingRearBias = 0.012f;

        [Tooltip("Minimum extra center-clearance applied to hand guides when surface or tool framing is active.")]
        [SerializeField, Range(0f, 0.08f)] private float framingCenterClearance = 0.026f;

        [Tooltip("How much surface swim lowers the swim viewmodel root to expose more of the full arm silhouette.")]
        [SerializeField, Range(0f, 0.06f)] private float surfaceRootLowering = 0.014f;

        [Tooltip("How much surface swim pulls the swim viewmodel root rearward to reduce false forward reach.")]
        [SerializeField, Range(0f, 0.05f)] private float surfaceRootRearBias = 0.01f;

        [Tooltip("How much armed tools lower the swim viewmodel root for better first-person framing.")]
        [SerializeField, Range(0f, 0.06f)] private float toolRootLowering = 0.01f;

        [Tooltip("How much armed tools pull the swim viewmodel root rearward to reduce screen-center clutter.")]
        [SerializeField, Range(0f, 0.05f)] private float toolRootRearBias = 0.008f;

        [Header("-- Organic Motion ----------------------")]
        [Tooltip("Smooth time for overall presentation blend transitions.")]
        [SerializeField, Range(0.02f, 0.35f)] private float presentationBlendSmoothTime = 0.1f;

        [Tooltip("Smooth time for tool suppression, support-hand rebalance, and pose-bias transitions.")]
        [SerializeField, Range(0.02f, 0.35f)] private float toolTransitionSmoothTime = 0.09f;

        [Tooltip("Smooth time for steering correction and turn-lag response. Lower = snappier, higher = heavier.")]
        [SerializeField, Range(0.02f, 0.35f)] private float steeringResponseSmoothTime = 0.11f;

        [Tooltip("Breathing lift amplitude while treading at the surface.")]
        [SerializeField, Range(0f, 0.04f)] private float surfaceBreathAmplitude = 0.009f;

        [Tooltip("Breathing cadence while treading at the surface.")]
        [SerializeField, Range(0.05f, 1f)] private float surfaceBreathFrequency = 0.18f;

        [Tooltip("Wave-driven bob amplitude while idling at the surface.")]
        [SerializeField, Range(0f, 0.04f)] private float surfaceWaveBobAmplitude = 0.013f;

        [Tooltip("Wave-driven bob cadence while idling at the surface.")]
        [SerializeField, Range(0.05f, 1.2f)] private float surfaceWaveBobFrequency = 0.31f;

        [Tooltip("Pitch drift applied by surface breathing and swell.")]
        [SerializeField, Range(0f, 4f)] private float surfaceWavePitchAmplitude = 0.55f;

        [Tooltip("Roll drift applied by surface breathing and swell.")]
        [SerializeField, Range(0f, 4f)] private float surfaceWaveRollAmplitude = 0.8f;

        [Header("-- Stroke Shaping ----------------------")]
        [Tooltip("How sharply the pull half of the stroke peaks. Higher = more decisive catch and pull, lower = flatter paddle motion.")]
        [SerializeField, Range(0.6f, 3f)] private float strokePullSharpness = 1.35f;

        [Tooltip("How softly the recovery half of the stroke opens. Higher = calmer recovery, lower = more robotic return.")]
        [SerializeField, Range(0.6f, 3f)] private float strokeRecoverySharpness = 1.12f;

        [Tooltip("How strongly steering correction advances the leading hand and delays the bracing hand in normalized stroke phase.")]
        [SerializeField, Range(0f, 0.12f)] private float steeringStrokePhaseLead = 0.045f;

        [Tooltip("How strongly sudden camera-turn sway adds transient stroke lead and lag on top of steady steering correction.")]
        [SerializeField, Range(0f, 0.12f)] private float cameraTurnStrokePhaseLead = 0.03f;

        [Tooltip("How much vertical swim intent biases stroke timing so ascend and descend do not reuse the exact same cadence read.")]
        [SerializeField, Range(0f, 0.08f)] private float verticalStrokePhaseBias = 0.018f;

        [Tooltip("How much surface presentation suppresses aggressive phase-leading so the surface read stays calmer and more legible.")]
        [SerializeField, Range(0f, 1f)] private float surfaceStrokePhaseSuppression = 0.55f;

        [Header("-- Pose Follow-Through ------------------")]
        [Tooltip("Smooth time for final swim-root positional settling. This is final water drag lag, not locomotion smoothing.")]
        [SerializeField, Range(0.01f, 0.25f)] private float rootPosePositionSmoothTime = 0.05f;

        [Tooltip("Smooth time for final swim-root rotational settling. Higher = heavier suit follow-through.")]
        [SerializeField, Range(0.01f, 0.3f)] private float rootPoseRotationSmoothTime = 0.065f;

        [Tooltip("Smooth time for hand-guide positional settling.")]
        [SerializeField, Range(0.01f, 0.3f)] private float guidePosePositionSmoothTime = 0.06f;

        [Tooltip("Smooth time for hand-guide rotational settling.")]
        [SerializeField, Range(0.01f, 0.35f)] private float guidePoseRotationSmoothTime = 0.075f;

        [Tooltip("Lag multiplier while near the surface. Lower values preserve readability and reduce mushy horizon drag.")]
        [SerializeField, Range(0.5f, 1.25f)] private float surfacePoseLagMultiplier = 0.78f;

        [Tooltip("Lag multiplier during underwater sprint. Lower values keep high-speed control readable.")]
        [SerializeField, Range(0.5f, 1.25f)] private float sprintPoseLagMultiplier = 0.9f;

        [Tooltip("Lag multiplier applied to heavy industrial style profiles.")]
        [SerializeField, Range(0.75f, 1.5f)] private float heavyPoseLagMultiplier = 1.18f;

        [Tooltip("Lag multiplier applied to powered-assist profiles.")]
        [SerializeField, Range(0.5f, 1.25f)] private float poweredAssistPoseLagMultiplier = 0.88f;

        [Header("-- Directional Correction --------------")]
        [Tooltip("How strongly lateral move intent feeds asymmetric swim correction posing.")]
        [SerializeField, Range(0f, 1.5f)] private float strafeCorrectionInfluence = 0.8f;

        [Tooltip("How strongly body-to-camera yaw disagreement feeds asymmetric swim correction posing.")]
        [SerializeField, Range(0f, 1.5f)] private float turnCorrectionInfluence = 0.55f;

        [Tooltip("How strongly local lateral swim velocity contributes to asymmetric correction posing.")]
        [SerializeField, Range(0f, 1.5f)] private float lateralVelocityCorrectionInfluence = 0.42f;

        [Tooltip("How much surface swim suppresses aggressive asymmetric correction posing to preserve horizon readability.")]
        [SerializeField, Range(0f, 1f)] private float surfaceCorrectionSuppression = 0.48f;

        [Tooltip("How much the active correction hand reaches farther forward while the opposite hand braces closer to the torso.")]
        [SerializeField, Range(0f, 0.75f)] private float steeringReachBias = 0.34f;

        [Tooltip("How much the active correction hand sweeps outward while the opposite hand collapses inward.")]
        [SerializeField, Range(0f, 0.75f)] private float steeringOutwardBias = 0.26f;

        [Tooltip("How much correction posing changes hand height for subtle sculling and brace readability.")]
        [SerializeField, Range(0f, 0.75f)] private float steeringVerticalBias = 0.18f;

        [Tooltip("How much correction posing twists hand yaw away from perfect symmetry.")]
        [SerializeField, Range(0f, 0.75f)] private float steeringYawBias = 0.28f;

        [Tooltip("How much correction posing twists hand roll away from perfect symmetry.")]
        [SerializeField, Range(0f, 0.75f)] private float steeringRollBias = 0.22f;

        [Tooltip("How much correction intent shifts the swim root sideways for whole-body steering feel.")]
        [SerializeField, Range(0f, 0.06f)] private float steeringRootLateralBias = 0.012f;

        [Tooltip("How much correction intent yaws the swim root for body-leading steering feel.")]
        [SerializeField, Range(0f, 10f)] private float steeringRootYawBias = 2.6f;

        [Tooltip("How much correction intent rolls the swim root into steering effort.")]
        [SerializeField, Range(0f, 10f)] private float steeringRootRollBias = 2.1f;

        [Tooltip("How strongly sudden camera yaw velocity produces inertial hand/root sway. This is transient lag, not steady steering correction.")]
        [SerializeField, Range(0f, 1.5f)] private float cameraTurnSwayInfluence = 0.65f;

        [Tooltip("Maximum camera yaw speed considered for inertial swim sway, in degrees per second.")]
        [SerializeField, Range(90f, 1440f)] private float cameraTurnSwayMaxDegreesPerSecond = 720f;

        [Tooltip("How much surface swim suppresses aggressive camera-turn sway to keep the horizon readable.")]
        [SerializeField, Range(0f, 1f)] private float cameraTurnSwaySurfaceSuppression = 0.35f;

        [Tooltip("How strongly camera-turn sway feeds asymmetric hand correction posing.")]
        [SerializeField, Range(0f, 1.5f)] private float cameraTurnSwayHandInfluence = 0.48f;

        [Tooltip("How much sudden camera yaw adds extra root yaw lag beyond body-follow lag.")]
        [SerializeField, Range(0f, 10f)] private float cameraTurnSwayRootYawBias = 1.85f;

        [Tooltip("How much sudden camera yaw adds extra root roll lag beyond body-follow lag.")]
        [SerializeField, Range(0f, 10f)] private float cameraTurnSwayRootRollBias = 2.8f;

        [Header("-- Environment Reactivity -------------")]
        [Tooltip("Sphere radius used for hand obstacle probes. Keep this modest so tight caves feel reactive without causing jitter.")]
        [SerializeField, Range(0.02f, 0.18f)] private float handObstacleSphereRadius = 0.06f;

        [Tooltip("How far ahead each hand checks for obstacles before the pose is forced to tuck back toward the torso.")]
        [SerializeField, Range(0.05f, 0.5f)] private float handObstacleForwardLookahead = 0.22f;

        [Tooltip("How far outward each hand checks for side walls near the final pose.")]
        [SerializeField, Range(0.02f, 0.25f)] private float handObstacleSideLookahead = 0.11f;

        [Tooltip("How far above and below the final hand pose we check for nearby cave walls, ceilings, and seabed. This keeps hands from scraping geometry even when the stroke is mostly lateral.")]
        [SerializeField, Range(0.02f, 0.24f)] private float handObstacleVerticalLookahead = 0.08f;

        [Tooltip("How strongly multiple nearby obstacle probes stack into one blocked-hand pressure value. Higher values make narrow caves feel more claustrophobic instead of reacting only to the single closest hit.")]
        [SerializeField, Range(0f, 1f)] private float handObstacleMultiProbeBlend = 0.58f;

        [Tooltip("How far the hand is pulled back when a forward obstacle is detected.")]
        [SerializeField, Range(0f, 0.18f)] private float handObstacleRetractDistance = 0.085f;

        [Tooltip("Extra retreat toward the torso when the hand is boxed in by nearby geometry. This sells body volume in tight caves instead of letting the wrist hover inside the rock.")]
        [SerializeField, Range(0f, 0.12f)] private float handObstacleBodyRetractDistance = 0.028f;

        [Tooltip("How much the hand is pulled inward toward the torso when space gets tight.")]
        [SerializeField, Range(0f, 0.1f)] private float handObstacleInwardDistance = 0.03f;

        [Tooltip("How much the hand drops downward when bracing against nearby geometry.")]
        [SerializeField, Range(0f, 0.08f)] private float handObstacleDownwardDistance = 0.018f;

        [Tooltip("How much the hand lifts upward when nearby geometry reads more like the seabed, a wall edge, or other space that should push the arm up instead of down.")]
        [SerializeField, Range(0f, 0.1f)] private float handObstacleUpwardDistance = 0.024f;

        [Tooltip("Minimum upward escape bias applied when obstacle normals are mostly sideways. Keeps cave walls from forcing the hand straight down into the floor.")]
        [SerializeField, Range(0f, 1f)] private float handObstacleWallLiftBias = 0.28f;

        [Tooltip("Extra local yaw applied while the hand is tucking away from nearby geometry.")]
        [SerializeField, Range(0f, 25f)] private float handObstacleYawBias = 8f;

        [Tooltip("Extra local roll applied while the hand is tucking away from nearby geometry.")]
        [SerializeField, Range(0f, 25f)] private float handObstacleRollBias = 6f;

        [Tooltip("Smooth time for obstacle reaction so hands feel displaced by water and space, not teleported.")]
        [SerializeField, Range(0.01f, 0.2f)] private float handObstacleSmoothTime = 0.045f;

        [Header("-- Propulsion Feedback ----------------")]
        [Tooltip("Invoked when a strong swim pull reaches its propulsion peak. Bind bubbles, water distortion, or other pull FX here")]
        [SerializeField] private UnityEvent onStrokePowerPulse;

        [Tooltip("Minimum propulsion pulse needed before the swim owner emits a stroke power event.")]
        [SerializeField, Range(0.4f, 1f)] private float strokePowerPulseThreshold = 0.84f;

        [Tooltip("Minimum spacing between strong stroke events so one pull does not spam multiple pulses.")]
        [SerializeField, Range(0.03f, 0.3f)] private float strokePowerPulseCooldown = 0.09f;

        [Tooltip("How quickly the one-shot propulsion impulse decays after a peak stroke event. This drives camera kick and future VFX timing without spamming multiple peaks.")]
        [SerializeField, Range(0.03f, 0.3f)] private float strokePowerImpulseDecayTime = 0.1f;

        [Header("-- Tool Swim Dominance -----------------")]
        [Tooltip("How much extra phase-leading the free support hand gets while a tool owns the opposite hand.")]
        [SerializeField, Range(0f, 0.1f)] private float equippedToolSupportHandPhaseLeadBoost = 0.024f;

        [Tooltip("How strongly the tool-owning hand suppresses its phase-leading while a tool is equipped.")]
        [SerializeField, Range(0f, 1f)] private float equippedToolToolHandPhaseLeadSuppression = 0.55f;

        [Tooltip("How much extra motion amplitude the support hand gets while the opposite hand is busy with a tool.")]
        [SerializeField, Range(0f, 0.75f)] private float equippedToolSupportHandMotionBoost = 0.22f;

        [Tooltip("How much motion amplitude the tool-owning hand loses while it braces around an equipped tool.")]
        [SerializeField, Range(0f, 1f)] private float equippedToolToolHandMotionSuppression = 0.32f;

        [Tooltip("How strongly the tool-owning hand ignores obstacle-reactive swim posing while it protects an equipped device.")]
        [SerializeField, Range(0.15f, 1f)] private float equippedToolToolHandObstacleResponseScale = 0.58f;

        [Tooltip("How much more aggressively the free support hand reacts to nearby geometry while the opposite hand owns a tool.")]
        [SerializeField, Range(0f, 0.75f)] private float equippedToolSupportHandObstacleBoost = 0.18f;

        [Tooltip("How much additional lag is applied while a tool is equipped, selling heavier viscous turns in water.")]
        [SerializeField, Range(0.75f, 1.5f)] private float equippedToolPoseLagMultiplier = 1.08f;

        [Tooltip("How much extra lag the tool-owning hand gets while it braces around an equipped tool.")]
        [SerializeField, Range(0.75f, 1.5f)] private float equippedToolToolHandPoseLagMultiplier = 1.14f;

        [Tooltip("How much lag the free support hand gets while the opposite hand is occupied by a tool. Lower values keep the support stroke more assertive.")]
        [SerializeField, Range(0.75f, 1.25f)] private float equippedToolSupportHandPoseLagMultiplier = 0.92f;

        [Tooltip("How much sharp head turns amplify swim pose lag. Higher values make the arms visibly chase the camera with heavier suit inertia.")]
        [SerializeField, Range(1f, 1.75f)] private float cameraTurnPoseLagMultiplier = 1.22f;

        [Tooltip("How much nearby wall pressure amplifies hand lag. This makes blocked hands feel displaced by water and body mass instead of snapping instantly.")]
        [SerializeField, Range(1f, 1.6f)] private float obstaclePoseLagMultiplier = 1.18f;

        [Header("-- Locomotion Feel --------------------")]
        [Tooltip("Stroke cadence multiplier while dragging the heaviest heavy-carry load.")]
        [SerializeField, Range(0.25f, 1f)] private float heavyCarryCadenceMultiplier = 0.68f;

        [Tooltip("Propulsion pulse multiplier while dragging the heaviest heavy-carry load.")]
        [SerializeField, Range(0.2f, 1f)] private float heavyCarryPropulsionMultiplier = 0.56f;

        [Tooltip("Overall swim pose lag multiplier while dragging the heaviest heavy-carry load.")]
        [SerializeField, Range(1f, 1.75f)] private float heavyCarryPoseLagMultiplier = 1.24f;

        [Tooltip("How much heavy carry pulls the swim root rearward at full load.")]
        [SerializeField, Range(0f, 0.06f)] private float heavyCarryRootRearBias = 0.028f;

        [Tooltip("How much heavy carry lowers the swim root at full load.")]
        [SerializeField, Range(0f, 0.05f)] private float heavyCarryRootDownBias = 0.016f;

        [Tooltip("How much heavy carry pitches the swim root downward at full load.")]
        [SerializeField, Range(0f, 6f)] private float heavyCarryRootPitchBias = 1.4f;

        [Tooltip("How much heavy carry tucks the hands rearward at full load.")]
        [SerializeField, Range(0f, 0.08f)] private float heavyCarryGuideRearBias = 0.036f;

        [Tooltip("How much heavy carry lowers the hands at full load.")]
        [SerializeField, Range(0f, 0.05f)] private float heavyCarryGuideDownBias = 0.012f;

        [Tooltip("How much heavy carry suppresses hand stroke motion at full load.")]
        [SerializeField, Range(0.2f, 1f)] private float heavyCarryGuideMotionMultiplier = 0.72f;

        [Header("-- Transport Feel ---------------------")]
        [Tooltip("Propulsion-force reference used to normalize active Manta transport boost.")]
        [SerializeField, Range(100f, 1200f)] private float mantaTransportForceReference = 800f;

        [Tooltip("Minimum propulsion floor injected by active Manta transport.")]
        [SerializeField, Range(0f, 1f)] private float mantaTransportPropulsionFloor = 0.62f;

        [Tooltip("Stroke cadence multiplier while Manta transport is actively pulling the player.")]
        [SerializeField, Range(0.2f, 1f)] private float mantaTransportCadenceMultiplier = 0.74f;

        [Tooltip("Pose lag multiplier while Manta transport is actively pulling the player.")]
        [SerializeField, Range(0.5f, 1.2f)] private float mantaTransportPoseLagMultiplier = 0.9f;

        [Tooltip("How much active Manta transport restores root presentation visibility.")]
        [SerializeField, Range(0f, 1f)] private float mantaTransportRootPresentationWeight = 0.58f;

        [Tooltip("How much active Manta transport restores support-hand presentation visibility.")]
        [SerializeField, Range(0f, 1f)] private float mantaTransportSupportHandWeight = 0.84f;

        [Tooltip("How much active Manta transport pulls the swim root forward.")]
        [SerializeField, Range(0f, 0.06f)] private float mantaTransportRootForwardBias = 0.018f;

        [Tooltip("How much active Manta transport lets the hands commit forward instead of reading like a full stroke.")]
        [SerializeField, Range(0f, 0.08f)] private float mantaTransportGuideForwardBias = 0.028f;

        [Tooltip("How much a blocked hand loses stroke drive while it is crowding geometry. This keeps the arm from reading like a full power stroke while the cave is physically pushing it back.")]
        [SerializeField, Range(0f, 1f)] private float blockedHandStrokeSuppression = 0.42f;

        [Tooltip("How much the opposite hand compensates when one arm is blocked by nearby geometry. This sells one-handed recovery and stronger sculling in tight spaces.")]
        [SerializeField, Range(0f, 0.5f)] private float blockedSupportHandCompensationBoost = 0.18f;

        [Tooltip("How much nearby wall pressure suppresses phase-leading on the blocked hand.")]
        [SerializeField, Range(0f, 1f)] private float blockedHandPhaseLeadSuppression = 0.35f;

        [Tooltip("How much the freer hand is allowed to lead the stroke while the opposite hand is crowded by geometry.")]
        [SerializeField, Range(0f, 0.08f)] private float blockedSupportHandPhaseLeadBoost = 0.018f;

        [Tooltip("How strongly nearby obstacle pressure suppresses propulsion read. This keeps a blocked stroke from still feeling like a full-power camera kick.")]
        [SerializeField, Range(0f, 1f)] private float obstaclePropulsionSuppression = 0.38f;

        [Tooltip("How much the freer hand can recover propulsion when only one arm is crowded by geometry. This preserves one-handed sculling instead of collapsing all forward drive.")]
        [SerializeField, Range(0f, 0.5f)] private float obstaclePropulsionCompensation = 0.16f;

        [Header("-- Wave Animation Bridge --------------")]
        [Tooltip("How quickly Crest-derived wave slope signals blend into procedural matrix parameters.")]
        [SerializeField, Range(1f, 24f)] private float proceduralWaveSignalBlendSpeed = 9f;

        [Tooltip("Wave slope magnitude that maps to a full procedural response of 1.")]
        [SerializeField, Range(0.1f, 3f)] private float proceduralWaveSlopeNormalization = 1.15f;

        [Header("-- Active Trauma Blend ---------------")]
        [Tooltip("World-impulse magnitude treated as full authored trauma pose response.")]
        [SerializeField, Range(0.5f, 80f)] private float activeTraumaImpulseNormalization = 18f;

        [Tooltip("How quickly trauma blend weight spikes when a physical hit lands.")]
        [SerializeField, Range(1f, 30f)] private float activeTraumaBlendInSpeed = 14f;

        [Tooltip("How long the trauma pose holds before blending back to authored swim animation.")]
        [SerializeField, Range(0.02f, 1f)] private float activeTraumaHoldTime = 0.18f;

        [Tooltip("Smooth time used when trauma motion settles back toward normal swim posing.")]
        [SerializeField, Range(0.02f, 0.4f)] private float activeTraumaRecoverySmoothTime = 0.12f;

        [Tooltip("How much authored swim presentation is suppressed while trauma owns the pose.")]
        [SerializeField, Range(0f, 1f)] private float activeTraumaPresentationSuppression = 0.58f;

        [Tooltip("How much hand-guide visibility is suppressed while trauma owns the pose.")]
        [SerializeField, Range(0f, 1f)] private float activeTraumaGuideSuppression = 0.35f;

        [Tooltip("Maximum local root offset driven by trauma blend.")]
        [SerializeField, Range(0f, 0.2f)] private float activeTraumaRootOffsetDistance = 0.08f;

        [Tooltip("Maximum local root angular displacement driven by trauma blend.")]
        [SerializeField, Range(0f, 35f)] private float activeTraumaRootEulerDegrees = 14f;

        [Tooltip("Maximum local hand-guide offset driven by trauma blend.")]
        [SerializeField, Range(0f, 0.24f)] private float activeTraumaGuideOffsetDistance = 0.11f;

        [Tooltip("Maximum local hand-guide angular displacement driven by trauma blend.")]
        [SerializeField, Range(0f, 45f)] private float activeTraumaGuideEulerDegrees = 18f;

        [Header("-- Root Obstacle Response -------------")]
        [Tooltip("How much nearby wall pressure shifts the swim root sideways away from the blocked hand.")]
        [SerializeField, Range(0f, 0.05f)] private float obstacleRootLateralBias = 0.012f;

        [Tooltip("How much nearby obstacle pressure pulls the swim root slightly rearward in tight spaces.")]
        [SerializeField, Range(0f, 0.05f)] private float obstacleRootRearBias = 0.01f;

        [Tooltip("How much nearby obstacle pressure lifts the swim root when the hands are being forced upward by walls, seabed edges, or floor contact.")]
        [SerializeField, Range(0f, 0.04f)] private float obstacleRootUpwardBias = 0.008f;

        [Tooltip("How much tight-space pressure compresses the swim root in pitch. This is subtle torso hunch, not a camera shove.")]
        [SerializeField, Range(0f, 6f)] private float obstacleRootPitchCompression = 1.15f;

        [Tooltip("How much nearby obstacle pressure yaws the swim root away from the blocked side.")]
        [SerializeField, Range(0f, 6f)] private float obstacleRootYawBias = 1.35f;

        [Tooltip("How much nearby obstacle pressure rolls the swim root away from the blocked side.")]
        [SerializeField, Range(0f, 6f)] private float obstacleRootRollBias = 1.1f;

        [Tooltip("How quickly root obstacle pressure releases after the player clears nearby geometry. Lower values feel denser and more viscous.")]
        [SerializeField, Range(0.01f, 0.25f)] private float obstacleRootSmoothTime = 0.085f;

        [Header("-- Diagnostics ---------------------------")]
        [SerializeField] private string _debugMode = "None";
        [SerializeField] private float _debugStrokePhase;
        [SerializeField] private float _debugPropulsionPulse;
        [SerializeField] private float _debugSpeed;
        [SerializeField] private float _debugSteeringCorrection;
        [SerializeField] private float _debugCameraTurnSway;
        [SerializeField] private float _debugStrokePhaseLead;
        [SerializeField] private float _debugLeftObstacleWeight;
        [SerializeField] private float _debugRightObstacleWeight;
        [SerializeField] private float _debugStrokePowerImpulse;
        [SerializeField] private float _debugObstacleRootPressure;
        [SerializeField] private float _debugPropulsionObstruction;
        [SerializeField] private int _debugLastDrivenFrame = -1;
        [SerializeField] private string _debugProfile;
        [SerializeField] private string _debugProfileSource;

        private bool _registeredLateFrame;
        private bool _registeredColdReferenceRepair;
        private bool _hotSwapListenerRegistered;
        private bool _coldReferenceRepairRequested;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private SwimPresentationProfile _activeProfile;
        private PlayerSwimPresentationMode _currentMode;
        private float _presentationBlend;
        private float _presentationBlendVelocity;
        private float _strokePhase;
        private float _propulsionPulse;
        private float _idleTimer;
        private float _previousSpeed;
        private float _turnLagYawCurrent;
        private float _turnLagYawVelocity;
        private float _turnLagRollCurrent;
        private float _turnLagRollVelocity;
        private float _toolSuppressionWeight = 1f;
        private float _toolSuppressionWeightVelocity;
        private float _currentGuideWeight;
        private float _currentLeftGuideWeight;
        private float _currentRightGuideWeight;
        private float _equippedToolBlendCurrent;
        private float _equippedToolBlendVelocity;
        private float _activeToolUseBlendCurrent;
        private float _activeToolUseBlendVelocity;
        private Vector3 _rootToolPositionBiasCurrent;
        private Vector3 _rootToolPositionBiasVelocity;
        private Vector3 _rootToolEulerBiasCurrent;
        private Vector3 _rootToolEulerBiasVelocity;
        private Vector3 _leftGuideToolPositionBiasCurrent;
        private Vector3 _leftGuideToolPositionBiasVelocity;
        private Vector3 _leftGuideToolEulerBiasCurrent;
        private Vector3 _leftGuideToolEulerBiasVelocity;
        private Vector3 _rightGuideToolPositionBiasCurrent;
        private Vector3 _rightGuideToolPositionBiasVelocity;
        private Vector3 _rightGuideToolEulerBiasCurrent;
        private Vector3 _rightGuideToolEulerBiasVelocity;
        private float _leftGuideVisibilityWeight = 1f;
        private float _leftGuideVisibilityVelocity;
        private float _rightGuideVisibilityWeight = 1f;
        private float _rightGuideVisibilityVelocity;
        private Vector3 _currentLocalPosition;
        private Quaternion _currentLocalRotation = Quaternion.identity;
        private Vector3 _rootPosePositionVelocity;
        private Vector3 _rootPoseEulerCurrent;
        private Vector3 _rootPoseEulerVelocity;
        private Vector3 _leftGuideCurrentLocalPosition;
        private Quaternion _leftGuideCurrentLocalRotation = Quaternion.identity;
        private Vector3 _leftGuidePosePositionVelocity;
        private Vector3 _leftGuideEulerCurrent;
        private Vector3 _leftGuideEulerVelocity;
        private Vector3 _rightGuideCurrentLocalPosition;
        private Quaternion _rightGuideCurrentLocalRotation = Quaternion.identity;
        private Vector3 _rightGuidePosePositionVelocity;
        private Vector3 _rightGuideEulerCurrent;
        private Vector3 _rightGuideEulerVelocity;
        private float _directionalCorrectionCurrent;
        private float _directionalCorrectionVelocity;
        private float _cameraTurnSwayCurrent;
        private float _cameraTurnSwayVelocity;
        private float _debugStrokePhaseLeadCurrent;
        private float _leftObstacleWeightCurrent;
        private float _leftObstacleWeightVelocity;
        private float _leftObstacleSideBiasCurrent;
        private float _leftObstacleSideBiasVelocity;
        private float _rightObstacleWeightCurrent;
        private float _rightObstacleWeightVelocity;
        private float _rightObstacleSideBiasCurrent;
        private float _rightObstacleSideBiasVelocity;
        private float _waveSlopeForwardCurrent;
        private float _waveSlopeLateralCurrent;
        private float _waveCrestReachCurrent;
        private float _waveDescentTuckCurrent;
        private float _waveLeanWeightCurrent;
        private float _immersionDepthCurrent;
        private float _leftObstacleVerticalBiasCurrent;
        private float _leftObstacleVerticalBiasVelocity;
        private float _rightObstacleVerticalBiasCurrent;
        private float _rightObstacleVerticalBiasVelocity;
        private float _rootObstacleAverageCurrent;
        private float _rootObstacleAverageVelocity;
        private float _rootObstacleDifferenceCurrent;
        private float _rootObstacleDifferenceVelocity;
        private float _rootObstacleVerticalCurrent;
        private float _rootObstacleVerticalVelocity;
        private float _propulsionObstructionCurrent;
        private float _previousPropulsionPulse;
        private float _strokePowerPulseCooldownRemaining;
        private float _strokePowerImpulseCurrent;
        private float _strokePowerImpulseVelocity;
        private float _currentVerticalPoseBias;
        private float _heavyCarryLoadCurrent;
        private float _heavyCarryLoadVelocity;
        private float _transportBoostCurrent;
        private float _transportBoostVelocity;
        private float _physicalTraumaBlendCurrent;
        private float _physicalTraumaBlendVelocity;
        private float _physicalTraumaBlendTarget;
        private float _physicalTraumaHoldTimer;
        private Vector3 _physicalTraumaLocalImpulseCurrent;
        private Vector3 _physicalTraumaLocalImpulseTarget;
        private Vector3 _physicalTraumaLocalImpulseVelocity;
        private PlayerTransportFeelContract _transportFeelContractCurrent;
        private float _previousCameraYaw;
        private int _lastDrivenFrame = -1;
        private int _presentationFrameCounter;
        private int _nextReferenceResolveFrame = -1;
        private int _lastWaveSlopeForwardShaderByte = int.MinValue;
        private int _lastWaveSlopeLateralShaderByte = int.MinValue;
        private int _lastWaveSlopeXShaderByte = int.MinValue;
        private int _lastWaveSlopeZShaderByte = int.MinValue;
        private int _lastWaveCrestReachShaderByte = int.MinValue;
        private int _lastWaveDescentTuckShaderByte = int.MinValue;
        private int _lastWaveLeanWeightShaderByte = int.MinValue;
        private int _lastImmersionDepthShaderByte = int.MinValue;
        private int _lastBreathingPhaseShaderByte = int.MinValue;
        private int _lastSwimVatSpeedScalarByte = int.MinValue;
        private float _swimVatSpeedScalar;
        private float _pendingWaveSlopeForwardShaderValue;
        private float _pendingWaveSlopeLateralShaderValue;
        private float _pendingWaveSlopeXShaderValue;
        private float _pendingWaveSlopeZShaderValue;
        private float _pendingWaveCrestReachShaderValue;
        private float _pendingWaveDescentTuckShaderValue;
        private float _pendingWaveLeanWeightShaderValue;
        private float _pendingImmersionDepthShaderValue;
        private float _pendingBreathingPhaseShaderValue;
        private float _pendingSwimVatSpeedScalarShaderValue;
        private bool _waveSlopeForwardShaderDirty;
        private bool _waveSlopeLateralShaderDirty;
        private bool _waveSlopeXShaderDirty;
        private bool _waveSlopeZShaderDirty;
        private bool _waveCrestReachShaderDirty;
        private bool _waveDescentTuckShaderDirty;
        private bool _waveLeanWeightShaderDirty;
        private bool _immersionDepthShaderDirty;
        private bool _breathingPhaseShaderDirty;
        private bool _swimVatSpeedScalarShaderDirty;
        private bool _hasInitializedActiveBlend;
        private bool _cameraYawInitialized;
        private bool _poseStateInitialized;
        private IInputService _inputService;
        private readonly Transform[] _guideHierarchyCache = new Transform[MaxGuideHierarchySearchNodes]; // COLD ALLOC: Transform[512] — cached swim guide hierarchy snapshot — owner: PlayerSwimPresentationController
        private readonly Transform[] _guideDirectChildCache = new Transform[MaxGuideChildScanCount]; // COLD ALLOC: Transform[128] — cached swim guide direct-children snapshot — owner: PlayerSwimPresentationController
        private Transform _guideHierarchySource;
        private int _guideHierarchyCount;
        private Transform _guideDirectChildSource;
        private int _guideDirectChildCount;

        /// <summary>Current resolved swim presentation mode.</summary>
        public PlayerSwimPresentationMode CurrentMode => _currentMode;

        /// <summary>Current stroke phase in normalized 0..1 space.</summary>
        public float CurrentStrokePhase => _strokePhase;

        /// <summary>Current normalized propulsion pulse derived from the stroke cycle.</summary>
        public float CurrentPropulsionPulse => _propulsionPulse;

        /// <summary>Current one-shot propulsion impulse emitted at pull peaks.</summary>
        public float CurrentStrokePowerImpulse => _strokePowerImpulseCurrent;

        /// <summary>Invoked when a strong swim pull reaches its propulsion peak.</summary>
        public UnityEvent OnStrokePowerPulse => onStrokePowerPulse;

        /// <summary>Currently active presentation profile.</summary>
        public SwimPresentationProfile CurrentProfile => _activeProfile;

        /// <summary>Current swim viewmodel local position output.</summary>
        public Vector3 CurrentLocalPosition => _currentLocalPosition;

        /// <summary>Current swim viewmodel local rotation output.</summary>
        public Quaternion CurrentLocalRotation => _currentLocalRotation;

        /// <summary>Current left hand guide local position output.</summary>
        public Vector3 CurrentLeftGuideLocalPosition => _leftGuideCurrentLocalPosition;

        /// <summary>Current left hand guide local rotation output.</summary>
        public Quaternion CurrentLeftGuideLocalRotation => _leftGuideCurrentLocalRotation;

        /// <summary>Current right hand guide local position output.</summary>
        public Vector3 CurrentRightGuideLocalPosition => _rightGuideCurrentLocalPosition;

        /// <summary>Current right hand guide local rotation output.</summary>
        public Quaternion CurrentRightGuideLocalRotation => _rightGuideCurrentLocalRotation;

        /// <summary>Current normalized hand-guide presentation weight.</summary>
        public float CurrentGuideWeight => _currentGuideWeight;

        /// <summary>Current normalized left-hand-guide presentation weight.</summary>
        public float CurrentLeftGuideWeight => _currentLeftGuideWeight;

        /// <summary>Current normalized right-hand-guide presentation weight.</summary>
        public float CurrentRightGuideWeight => _currentRightGuideWeight;

        /// <summary>Current overall presentation blend after tool suppression.</summary>
        public float CurrentPresentationBlend => _presentationBlend * _toolSuppressionWeight;

        /// <summary>Current normalized vertical swim pose bias. Positive = ascend, negative = descend.</summary>
        public float CurrentVerticalPoseBias => _currentVerticalPoseBias;

        /// <summary>Current normalized physical-trauma blend suppressing authored swim posing.</summary>
        public float CurrentPhysicalTraumaBlend => _physicalTraumaBlendCurrent;

        /// <summary>Current normalized directional steering correction read.</summary>
        public float CurrentDirectionalCorrection => _directionalCorrectionCurrent;

        /// <summary>Current normalized camera-turn sway read.</summary>
        public float CurrentCameraTurnSway => _cameraTurnSwayCurrent;

        /// <summary>Current averaged obstacle pressure compressing the swim root.</summary>
        public float CurrentObstacleRootPressure => _rootObstacleAverageCurrent;

        /// <summary>Current signed left/right obstacle difference affecting the swim root.</summary>
        public float CurrentObstacleRootDifference => _rootObstacleDifferenceCurrent;

        /// <summary>Current normalized upward obstacle escape bias affecting the swim root.</summary>
        public float CurrentObstacleRootVerticalBias => _rootObstacleVerticalCurrent;

        /// <summary>Current normalized equipped-tool blend suppressing swim presentation.</summary>
        public float CurrentToolBlend => _equippedToolBlendCurrent;

        /// <summary>Current quantized swim-speed scalar exported to the third-person VAT shader lane.</summary>
        public float CurrentSwimVatSpeedScalar => _swimVatSpeedScalar;

        internal void SyncGpuVatSwimSpeedScalar(float speedScalar)
        {
            float clamped = math.saturate(speedScalar);
            int quantized = (int)math.round(clamped * 255f);
            if (quantized == _lastSwimVatSpeedScalarByte)
                return;

            _lastSwimVatSpeedScalarByte = quantized;
            _swimVatSpeedScalar = quantized * 0.00392156862f;
            QueueSwimShaderFloat(_SwimVatSpeedScalarShaderId, _swimVatSpeedScalar);
        }

        private void Awake()
        {
            AutoResolveReferences();
            EnsureKineticMatrixRuntimeCold();
            ResolveGuideReferences();
            InitializePoseStateFromCurrentTargets();

            if (playerMovement != null)
            {
                _previousCameraYaw = playerMovement.CameraYaw;
                _cameraYawInitialized = true;
            }
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            EnsureKineticMatrixRuntimeCold();
            TryRegister();
            TryRegisterColdReferenceRepair();
            TryRegisterHotSwapListener();
            RequestColdReferenceResolve(0);
        }

        private void Start()
        {
            TryRegister();
            TryRegisterColdReferenceRepair();
            ResolveGuideReferences();
            InitializePoseStateFromCurrentTargets();
        }

        private void OnDisable()
        {
            _physicalTraumaBlendCurrent = 0f;
            _physicalTraumaBlendVelocity = 0f;
            _physicalTraumaBlendTarget = 0f;
            _physicalTraumaHoldTimer = 0f;
            _physicalTraumaLocalImpulseCurrent = Vector3.zero;
            _physicalTraumaLocalImpulseTarget = Vector3.zero;
            _physicalTraumaLocalImpulseVelocity = Vector3.zero;
            _waveSlopeForwardCurrent = 0f;
            _waveSlopeLateralCurrent = 0f;
            _waveCrestReachCurrent = 0f;
            _waveDescentTuckCurrent = 0f;
            _waveLeanWeightCurrent = 0f;
            _immersionDepthCurrent = 0f;
            ResetProceduralWaveSignals();

            Shader.SetGlobalFloat(_BreathingPhaseShaderId, 0f);
            _lastBreathingPhaseShaderByte = int.MinValue;
            Shader.SetGlobalFloat(_SwimVatSpeedScalarShaderId, 0f);
            _lastSwimVatSpeedScalarByte = int.MinValue;
            _swimVatSpeedScalar = 0f;
            TryUnregisterHotSwapListener();
            TryUnregisterColdReferenceRepair();
            TryUnregister();
        }

        private void OnDestroy()
        {
            ResetProceduralWaveSignals();
            Shader.SetGlobalFloat(_BreathingPhaseShaderId, 0f);
            _lastBreathingPhaseShaderByte = int.MinValue;
            Shader.SetGlobalFloat(_SwimVatSpeedScalarShaderId, 0f);
            _lastSwimVatSpeedScalarByte = int.MinValue;
            _swimVatSpeedScalar = 0f;
            TryUnregisterHotSwapListener();
            TryUnregisterColdReferenceRepair();
            TryUnregister();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            AutoResolveReferences(allowSingletonAccess: false);
            ResolveGuideReferences();
        }
#endif

        public void LateFrameTick()
        {
            FlushQueuedSwimShaderFloats();
        }

        public void ColdTick()
        {
            if (!_coldReferenceRepairRequested)
                return;

            _coldReferenceRepairRequested = false;
            CacheRegistryServicesCold();
            AutoResolveReferences();
            EnsureKineticMatrixRuntimeCold();
            ResolveGuideReferences();
            InitializePoseStateFromCurrentTargets();

            if (playerMovement == null || viewModelRoot == null)
                _coldReferenceRepairRequested = true;
        }

        internal void ApplyPhysicalTrauma(Vector3 worldImpulse, float weight)
        {
            float clampedWeight = math.saturate(weight);
            if (clampedWeight <= 0f)
                return;

            Transform reference = viewModelRoot != null ? viewModelRoot : transform;
            Vector3 localImpulse = reference.InverseTransformDirection(worldImpulse);
            float normalization = math.max(0.5f, activeTraumaImpulseNormalization);
            Vector3 normalizedImpulse = ClampVectorBySqr(localImpulse / normalization, 1f);
            if (normalizedImpulse.sqrMagnitude <= 0.0001f)
                normalizedImpulse = reference.InverseTransformDirection(Vector3.down);

            _physicalTraumaLocalImpulseTarget = ClampVectorBySqr(
                _physicalTraumaLocalImpulseTarget + normalizedImpulse * clampedWeight,
                1f);
            _physicalTraumaLocalImpulseVelocity = Vector3.zero;
            _physicalTraumaBlendTarget = math.max(_physicalTraumaBlendTarget, clampedWeight);
            _physicalTraumaHoldTimer = math.max(_physicalTraumaHoldTimer, activeTraumaHoldTime * math.lerp(0.6f, 1f, clampedWeight));
            if (TryGetKineticMatrixSinkHot(out IKineticCharacterPresentationSink kineticSink))
                kineticSink.SubmitDamageImpulse(new float3(normalizedImpulse.x, normalizedImpulse.y, normalizedImpulse.z), clampedWeight);
        }

        /// <summary>Drives swim presentation from the authoritative locomotion owner. Safe to call from player movement each render tick.</summary>
        public void SyncFromLocomotion(float dt, bool forceFrame = false)
        {
            // L19 hop2 LIVE: PlayerSwimBlockoutRig.ApplyUpperArm → ResolveLookRotationNoTrig
            // ACCESS_VIOLATION under -batchmode. Hop probes only need locomotion intent.
            if (Application.isBatchMode)
                return;

            int frame = HectonArenaAllocator.CurrentFrameSequence;
            if (frame <= 0)
                frame = unchecked(++_presentationFrameCounter);
            if (!forceFrame && _lastDrivenFrame == frame)
                return;

            _lastDrivenFrame = frame;
#if UNITY_EDITOR
            _debugLastDrivenFrame = frame;
#endif

            if (dt <= 0f)
                return;

            if (playerMovement == null)
            {
                RequestColdReferenceResolve(frame);
                return;
            }

            if (viewModelRoot == null)
                RequestColdReferenceResolve(frame);

            SwimPresentationProfile profile = ResolveBoundProfile();
            if (profile == null)
                return;

            Vector3 velocity = TryResolveKccPresentationVelocity(out Vector3 kccVelocity)
                ? kccVelocity
                : ResolveMovementPresentationVelocity();
            float speed = ApproximateVectorMagnitude(velocity);
            float planarSpeed = ApproximatePlanarMagnitude(velocity.x, velocity.z);
            float speedDelta = speed - _previousSpeed;
            _currentVerticalPoseBias = math.clamp(velocity.y * 0.18f * verticalPoseInfluence, -1f, 1f);
            _previousSpeed = speed;

            UpdateLocomotionFeelState(dt);
            _currentMode = ResolveMode(profile, planarSpeed, speedDelta, dt);

            bool activeSwimPresentation =
                _currentMode == PlayerSwimPresentationMode.SurfaceTread ||
                _currentMode == PlayerSwimPresentationMode.SurfaceStroke ||
                _currentMode == PlayerSwimPresentationMode.UnderwaterNeutral ||
                _currentMode == PlayerSwimPresentationMode.UnderwaterStroke ||
                _currentMode == PlayerSwimPresentationMode.UnderwaterGlide ||
                _currentMode == PlayerSwimPresentationMode.UnderwaterSprint;

            UpdateToolSuppression(dt);
            float blendTarget = activeSwimPresentation ? 1f : 0f;
            if (activeSwimPresentation && !_hasInitializedActiveBlend)
            {
                _presentationBlend = 1f;
                _presentationBlendVelocity = 0f;
                _hasInitializedActiveBlend = true;
            }
            else if (!activeSwimPresentation)
            {
                _hasInitializedActiveBlend = false;
            }

            _presentationBlend = SmoothDampValue(
                _presentationBlend,
                blendTarget,
                ref _presentationBlendVelocity,
                presentationBlendSmoothTime,
                dt);

            UpdatePhysicalTrauma(dt);
            UpdateStrokeState(profile, planarSpeed, speedDelta, dt);
            UpdateStrokePowerPulse(activeSwimPresentation, dt);
            UpdateCameraTurnSway(dt);
            UpdateDirectionalCorrection(planarSpeed, velocity, dt);
            UpdateWaveAnimationBridge(activeSwimPresentation, dt);
            ApplyRootPose(profile, velocity, speedDelta, dt);
            ApplyGuidePoses(profile, planarSpeed, velocity, dt);
            if (swimBlockoutRig != null)
                swimBlockoutRig.SyncFromPresentation(dt, true);
            UpdateDiagnostics(profile, speed);
        }

        private void TryRegister()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregister()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrame = false;
            }
        }

        private void TryRegisterColdReferenceRepair()
        {
            if (_registeredColdReferenceRepair || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredColdReferenceRepair = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterColdReferenceRepair(bool clearPendingRequest = true)
        {
            if (!_registeredColdReferenceRepair)
            {
                if (clearPendingRequest)
                    _coldReferenceRepairRequested = false;
                return;
            }

            GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Player);
            _registeredColdReferenceRepair = false;
            if (clearPendingRequest)
                _coldReferenceRepairRequested = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregister();
                TryUnregisterColdReferenceRepair(clearPendingRequest: false);

                if (currentService != null && isActiveAndEnabled)
                {
                    TryRegister();
                    TryRegisterColdReferenceRepair();
                }

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Input)
            {
                _inputService = currentService as IInputService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                CachePlayerContextReferencesCold(currentService as IPlayerRuntimeContext, replaceExisting: true);
                RequestColdReferenceResolve(0);
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _inputService = GlobalRegistry.Input;
            CachePlayerContextReferencesCold(GlobalRegistry.Player);
        }

        private void CachePlayerContextReferencesCold(IPlayerRuntimeContext playerContext, bool replaceExisting = false)
        {
            if (playerContext == null || !playerContext.IsInitialized)
            {
                if (replaceExisting)
                {
                    _playerRuntimeContext = null;
                    playerMovement = null;
                    playerToolManager = null;
                    playerTransportCoordinator = null;
                }

                return;
            }

            _playerRuntimeContext = playerContext;

            if (replaceExisting || playerMovement == null)
                playerMovement = playerContext.PlayerMovement;

            if (replaceExisting || playerToolManager == null)
                playerToolManager = playerContext.ToolManager;

            if (replaceExisting || playerTransportCoordinator == null)
                playerTransportCoordinator = playerContext.PlayerTransportCoordinator;
        }

        private void RequestColdReferenceResolve(int frame)
        {
            if (frame > 0 && frame < _nextReferenceResolveFrame)
                return;

            _nextReferenceResolveFrame = frame + ReferenceResolveRetryFrameInterval;
            _coldReferenceRepairRequested = true;
        }

        private void AutoResolveReferences(bool allowSingletonAccess = true)
        {
            if (playerMovement == null)
                gameObject.TryGetComponent(out playerMovement);

            if (playerToolManager == null)
                gameObject.TryGetComponent(out playerToolManager);

            if (playerTransportCoordinator == null)
                gameObject.TryGetComponent(out playerTransportCoordinator);

            if (swimBlockoutRig == null)
                gameObject.TryGetComponent(out swimBlockoutRig);

            if (kineticMatrixRuntime == null || _kineticMatrixSink == null)
                TryResolveKineticMatrixSinkCold(out _);

            if (allowSingletonAccess && _inputService == null)
                CacheRegistryServicesCold();
        }

        private void EnsureKineticMatrixRuntimeCold()
        {
            if (TryGetKineticMatrixSinkHot(out _))
                return;

            TryResolveKineticMatrixSinkCold(out _);
        }

        private bool TryGetKineticMatrixSinkHot(out IKineticCharacterPresentationSink sink)
        {
            sink = _kineticMatrixSink;
            if (sink != null)
                return true;

            if (kineticMatrixRuntime is IKineticCharacterPresentationSink typedSink)
            {
                _kineticMatrixSink = typedSink;
                sink = typedSink;
                return true;
            }

            sink = null;
            return false;
        }

        private bool TryResolveKineticMatrixSinkCold(out IKineticCharacterPresentationSink sink)
        {
            if (TryGetKineticMatrixSinkHot(out sink))
                return true;

            if (gameObject.TryGetComponent(out IKineticCharacterPresentationSink resolvedSink))
            {
                _kineticMatrixSink = resolvedSink;
                kineticMatrixRuntime = resolvedSink as MonoBehaviour;
                sink = resolvedSink;
                return true;
            }

            sink = null;
            return false;
        }

        private void ResolveGuideReferences()
        {
            if (viewModelRoot == null && playerMovement != null)
            {
                Transform rootTransform = playerMovement.transform;
                if (rootTransform != null)
                    viewModelRoot = FindTransformByName(ResolveGuideHierarchy(rootTransform), "Swim_ViewmodelRoot");
            }

            if (viewModelRoot == null)
                return;

            if (leftHandGuide == null)
                leftHandGuide = FindChildByName(ResolveGuideDirectChildren(viewModelRoot), LeftGuideName);

            if (rightHandGuide == null)
                rightHandGuide = FindChildByName(ResolveGuideDirectChildren(viewModelRoot), RightGuideName);
        }

        private ReadOnlySpan<Transform> ResolveGuideHierarchy(Transform root)
        {
            if (_guideHierarchySource != root || _guideHierarchyCount == 0)
                RebuildGuideHierarchyCache(root);

            return new ReadOnlySpan<Transform>(_guideHierarchyCache, 0, _guideHierarchyCount);
        }

        private ReadOnlySpan<Transform> ResolveGuideDirectChildren(Transform root)
        {
            if (_guideDirectChildSource != root || _guideDirectChildCount == 0)
                RebuildGuideDirectChildCache(root);

            return new ReadOnlySpan<Transform>(_guideDirectChildCache, 0, _guideDirectChildCount);
        }

        private void RebuildGuideHierarchyCache(Transform root)
        {
            _guideHierarchySource = root;
            _guideHierarchyCount = 0;

            if (root == null)
                return;

            _guideHierarchyCache[0] = root;
            int writeCount = 1;
            for (int readIndex = 0; readIndex < writeCount; readIndex++)
            {
                Transform parent = _guideHierarchyCache[readIndex];
                int childCount = math.min(parent != null ? parent.childCount : 0, MaxGuideHierarchySearchNodes - writeCount);
                for (int childIndex = 0; childIndex < childCount; childIndex++)
                {
                    Transform child = parent.GetChild(childIndex);
                    if (child == null)
                        continue;

                    _guideHierarchyCache[writeCount++] = child;
                }
            }

            _guideHierarchyCount = writeCount;
        }

        private void RebuildGuideDirectChildCache(Transform root)
        {
            _guideDirectChildSource = root;
            _guideDirectChildCount = 0;

            if (root == null)
                return;

            int childCount = math.min(root.childCount, MaxGuideChildScanCount);
            for (int childIndex = 0; childIndex < childCount; childIndex++)
            {
                Transform child = root.GetChild(childIndex);
                if (child == null)
                    continue;

                _guideDirectChildCache[_guideDirectChildCount++] = child;
            }
        }

        private static Transform FindChildByName(ReadOnlySpan<Transform> children, string childName)
        {
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != null && child.name == childName)
                    return child;
            }

            return null;
        }

        private static Transform FindTransformByName(ReadOnlySpan<Transform> hierarchy, string transformName)
        {
            for (int i = 0; i < hierarchy.Length; i++)
            {
                Transform candidate = hierarchy[i];
                if (candidate != null && candidate.name == transformName)
                    return candidate;
            }

            return null;
        }

        private SwimPresentationProfile ResolveBoundProfile()
        {
            SuitData currentSuit = playerMovement.CurrentSuit;
            if (profileLibrary != null)
            {
                SwimPresentationProfile libraryProfile = profileLibrary.ResolveProfile(currentSuit);
                if (libraryProfile != null)
                {
                    _activeProfile = libraryProfile;
                    return _activeProfile;
                }
            }

            if (currentSuit != null && suitBindings != null)
            {
                for (int i = 0; i < suitBindings.Length; i++)
                {
                    if (ReferenceEquals(suitBindings[i].suit, currentSuit) &&
                        suitBindings[i].profile != null)
                    {
                        _activeProfile = suitBindings[i].profile;
                        return _activeProfile;
                    }
                }
            }

            if (currentSuit != null)
            {
                if (currentSuit.mass >= HeavySuitMassThreshold && fallbackHeavyProfile != null)
                {
                    _activeProfile = fallbackHeavyProfile;
                    return _activeProfile;
                }

                if (currentSuit.mass >= UtilitySuitMassThreshold && fallbackUtilityProfile != null)
                {
                    _activeProfile = fallbackUtilityProfile;
                    return _activeProfile;
                }
            }

            _activeProfile = fallbackLightProfile;
            return _activeProfile;
        }

        private PlayerSwimPresentationMode ResolveMode(
            SwimPresentationProfile profile,
            float planarSpeed,
            float speedDelta,
            float dt)
        {
            switch (playerMovement.CurrentLocomotionMode)
            {
                case PlayerLocomotionMode.ShallowWadeWalk:
                    return PlayerSwimPresentationMode.ShallowWade;

                case PlayerLocomotionMode.SurfaceSwim:
                    return planarSpeed < profile.SurfaceStrokeStartSpeed
                        ? PlayerSwimPresentationMode.SurfaceTread
                        : PlayerSwimPresentationMode.SurfaceStroke;

                case PlayerLocomotionMode.UnderwaterSwim:
                    if (_heavyCarryLoadCurrent < 0.05f &&
                        planarSpeed >= profile.UnderwaterSprintStartSpeed)
                        return PlayerSwimPresentationMode.UnderwaterSprint;

                    if (planarSpeed < profile.UnderwaterStrokeStartSpeed)
                        return PlayerSwimPresentationMode.UnderwaterNeutral;

                    float normalizedSpeedDelta = dt > 0f ? speedDelta / dt : 0f;
                    return normalizedSpeedDelta < -0.2f && planarSpeed > profile.UnderwaterStrokeStartSpeed * 1.1f
                        ? PlayerSwimPresentationMode.UnderwaterGlide
                        : PlayerSwimPresentationMode.UnderwaterStroke;

                default:
                    return PlayerSwimPresentationMode.Dry;
            }
        }

        private void UpdateStrokeState(
            SwimPresentationProfile profile,
            float planarSpeed,
            float speedDelta,
            float dt)
        {
            _idleTimer += dt;
            if (_idleTimer > 100000f)
                _idleTimer -= 100000f;

            float cadence = 0f;
            switch (_currentMode)
            {
                case PlayerSwimPresentationMode.SurfaceTread:
                    cadence = profile.SurfaceTreadCadence;
                    break;

                case PlayerSwimPresentationMode.SurfaceStroke:
                    cadence = profile.SurfaceStrokeCadence;
                    break;

                case PlayerSwimPresentationMode.UnderwaterNeutral:
                    cadence = profile.UnderwaterStrokeCadence * 0.45f;
                    break;

                case PlayerSwimPresentationMode.UnderwaterStroke:
                    cadence = profile.UnderwaterStrokeCadence;
                    break;

                case PlayerSwimPresentationMode.UnderwaterGlide:
                    cadence = profile.UnderwaterStrokeCadence * math.lerp(0.22f, 0.45f, 1f - profile.GlideBias);
                    break;

                case PlayerSwimPresentationMode.UnderwaterSprint:
                    cadence = profile.UnderwaterStrokeCadence * profile.SprintCadenceMultiplier;
                    break;
            }

            if (cadence > 0f)
            {
                cadence *= math.lerp(1f, heavyCarryCadenceMultiplier, _heavyCarryLoadCurrent);
                cadence *= math.lerp(1f, ResolveTransportCadenceMultiplier(), _transportBoostCurrent);
                float speedFactor = math.saturate(planarSpeed / math.max(0.01f, profile.UnderwaterSprintStartSpeed));
                cadence *= 1f + speedFactor * profile.SpeedCadenceInfluence;
                _strokePhase += cadence * dt;
                _strokePhase -= math.floor(_strokePhase);
            }
            else
            {
                _strokePhase = math.lerp(_strokePhase, 0f, ResolveDecayBlend(presentationBlendSpeed, dt));
            }

            float rawCycle = ApproximateSinCycle01(_strokePhase);
            float pullPulse = math.max(0f, rawCycle);
            float glidePulse = math.max(0f, -rawCycle) * profile.GlideBias;
            float accelerationBias = math.saturate(speedDelta * 0.2f);
            float propulsionPulse = math.saturate(pullPulse + accelerationBias - glidePulse * 0.35f);
            ApplyObstaclePropulsionDamping(ref propulsionPulse);
            propulsionPulse *= math.lerp(1f, heavyCarryPropulsionMultiplier, _heavyCarryLoadCurrent);
            if (_transportBoostCurrent > 0.0001f)
                propulsionPulse = math.max(propulsionPulse, _transportBoostCurrent * ResolveTransportPropulsionFloor());
            _propulsionPulse = propulsionPulse * _presentationBlend * _toolSuppressionWeight;
        }

        private void ApplyObstaclePropulsionDamping(ref float propulsionPulse)
        {
            float leftObstacle = _leftObstacleWeightCurrent * _currentLeftGuideWeight;
            float rightObstacle = _rightObstacleWeightCurrent * _currentRightGuideWeight;
            float obstacleAverage = (leftObstacle + rightObstacle) * 0.5f;
            float obstacleDifference = math.abs(leftObstacle - rightObstacle);
            float obstructionPenalty = math.max(0f, obstacleAverage - obstacleDifference * 0.32f);
            float suppressionScale = 1f - obstaclePropulsionSuppression * obstructionPenalty;
            float compensationScale = 1f + obstaclePropulsionCompensation * obstacleDifference * (1f - obstacleAverage * 0.5f);
            float finalScale = math.clamp(suppressionScale * compensationScale, 0.18f, 1f);
            _propulsionObstructionCurrent = 1f - finalScale;
            propulsionPulse *= finalScale;
        }

        private void UpdateStrokePowerPulse(bool activeSwimPresentation, float dt)
        {
            _strokePowerImpulseCurrent = SmoothDampValue(
                _strokePowerImpulseCurrent,
                0f,
                ref _strokePowerImpulseVelocity,
                strokePowerImpulseDecayTime,
                dt);

            if (_strokePowerPulseCooldownRemaining > 0f)
                _strokePowerPulseCooldownRemaining = math.max(0f, _strokePowerPulseCooldownRemaining - dt);

            bool canPulse =
                activeSwimPresentation &&
                (_currentMode == PlayerSwimPresentationMode.SurfaceStroke ||
                 _currentMode == PlayerSwimPresentationMode.UnderwaterStroke ||
                 _currentMode == PlayerSwimPresentationMode.UnderwaterSprint);

            if (canPulse &&
                _propulsionPulse >= strokePowerPulseThreshold &&
                _previousPropulsionPulse < strokePowerPulseThreshold &&
                _strokePowerPulseCooldownRemaining <= 0f)
            {
                onStrokePowerPulse?.Invoke();
                _strokePowerPulseCooldownRemaining = strokePowerPulseCooldown;
                _strokePowerImpulseCurrent = 1f;
                _strokePowerImpulseVelocity = 0f;
            }

            _previousPropulsionPulse = _propulsionPulse;
        }

        private void UpdateWaveAnimationBridge(bool activeSwimPresentation, float dt)
        {
            if (playerMovement == null)
                return;

            float targetWeight = 0f;
            float targetForward = 0f;
            float targetLateral = 0f;
            float targetCrestReach = 0f;
            float targetDescentTuck = 0f;
            float targetImmersionDepth = 0f;

            if (activeSwimPresentation && playerMovement.CurrentLocomotionMode == PlayerLocomotionMode.SurfaceSwim)
            {
                Vector2 waveSlope = playerMovement.GetCurrentLocalWaveSlope();
                float slopeNormalization = math.max(0.1f, proceduralWaveSlopeNormalization);
                float shorelineWeight = playerMovement.CurrentShoreBuoyancyBlend01;
                targetWeight = shorelineWeight;
                targetForward = math.clamp(waveSlope.y / slopeNormalization, -1f, 1f) * shorelineWeight;
                targetLateral = math.clamp(waveSlope.x / slopeNormalization, -1f, 1f) * shorelineWeight;
                targetCrestReach = math.max(0f, targetForward);
                targetDescentTuck = math.max(0f, -targetForward);
                targetImmersionDepth = ResolvePlayerDepthMeters() * shorelineWeight;
            }

            float traumaProceduralScale = 1f - _physicalTraumaBlendCurrent * activeTraumaPresentationSuppression;
            targetWeight *= traumaProceduralScale;
            targetForward *= traumaProceduralScale;
            targetLateral *= traumaProceduralScale;
            targetCrestReach *= traumaProceduralScale;
            targetDescentTuck *= traumaProceduralScale;
            targetImmersionDepth *= traumaProceduralScale;

            float blendT = ResolveDecayBlend(math.max(proceduralWaveSignalBlendSpeed, 0.01f), dt);
            _waveSlopeForwardCurrent = math.lerp(_waveSlopeForwardCurrent, targetForward, blendT);
            _waveSlopeLateralCurrent = math.lerp(_waveSlopeLateralCurrent, targetLateral, blendT);
            _waveCrestReachCurrent = math.lerp(_waveCrestReachCurrent, targetCrestReach, blendT);
            _waveDescentTuckCurrent = math.lerp(_waveDescentTuckCurrent, targetDescentTuck, blendT);
            _waveLeanWeightCurrent = math.lerp(_waveLeanWeightCurrent, targetWeight, blendT);
            _immersionDepthCurrent = math.lerp(_immersionDepthCurrent, targetImmersionDepth, blendT);

            PublishProceduralWaveSignals();
        }

        private float ResolvePlayerDepthMeters()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters))
            {
                return math.max(0f, movementState.DepthMeters);
            }

            if (playerContext != null)
                return 0f;

            HectonPlayerMovement movement = playerMovement;
            return movement != null && math.isfinite(movement.CurrentDepth)
                ? math.max(0f, movement.CurrentDepth)
                : 0f;
        }

        private void PublishProceduralWaveSignals()
        {
            PublishQuantizedShaderFloat(_WaveSlopeForwardShaderId, _waveSlopeForwardCurrent, ref _lastWaveSlopeForwardShaderByte, -1f, 1f);
            PublishQuantizedShaderFloat(_WaveSlopeLateralShaderId, _waveSlopeLateralCurrent, ref _lastWaveSlopeLateralShaderByte, -1f, 1f);
            PublishQuantizedShaderFloat(_WaveSlopeXShaderId, _waveSlopeLateralCurrent, ref _lastWaveSlopeXShaderByte, -1f, 1f);
            PublishQuantizedShaderFloat(_WaveSlopeZShaderId, _waveSlopeForwardCurrent, ref _lastWaveSlopeZShaderByte, -1f, 1f);
            PublishQuantizedShaderFloat(_WaveCrestReachShaderId, _waveCrestReachCurrent, ref _lastWaveCrestReachShaderByte, 0f, 1f);
            PublishQuantizedShaderFloat(_WaveDescentTuckShaderId, _waveDescentTuckCurrent, ref _lastWaveDescentTuckShaderByte, 0f, 1f);
            PublishQuantizedShaderFloat(_WaveLeanWeightShaderId, _waveLeanWeightCurrent, ref _lastWaveLeanWeightShaderByte, 0f, 1f);
            PublishQuantizedShaderFloat(_ImmersionDepthShaderId, _immersionDepthCurrent, ref _lastImmersionDepthShaderByte, 0f, 4f);

            if (TryGetKineticMatrixSinkHot(out IKineticCharacterPresentationSink kineticSink))
            {
                kineticSink.SubmitSwimPresentation(
                    _waveSlopeForwardCurrent,
                    _waveSlopeLateralCurrent,
                    _waveCrestReachCurrent,
                    _waveDescentTuckCurrent,
                    _waveLeanWeightCurrent,
                    _immersionDepthCurrent,
                    _lastBreathingPhaseShaderByte == int.MinValue ? 0f : _lastBreathingPhaseShaderByte * 0.00787401575f,
                    _equippedToolBlendCurrent);
            }
        }

        private void ResetProceduralWaveSignals()
        {
            Shader.SetGlobalFloat(_WaveSlopeForwardShaderId, 0f);
            Shader.SetGlobalFloat(_WaveSlopeLateralShaderId, 0f);
            Shader.SetGlobalFloat(_WaveSlopeXShaderId, 0f);
            Shader.SetGlobalFloat(_WaveSlopeZShaderId, 0f);
            Shader.SetGlobalFloat(_WaveCrestReachShaderId, 0f);
            Shader.SetGlobalFloat(_WaveDescentTuckShaderId, 0f);
            Shader.SetGlobalFloat(_WaveLeanWeightShaderId, 0f);
            Shader.SetGlobalFloat(_ImmersionDepthShaderId, 0f);
            _lastWaveSlopeForwardShaderByte = int.MinValue;
            _lastWaveSlopeLateralShaderByte = int.MinValue;
            _lastWaveSlopeXShaderByte = int.MinValue;
            _lastWaveSlopeZShaderByte = int.MinValue;
            _lastWaveCrestReachShaderByte = int.MinValue;
            _lastWaveDescentTuckShaderByte = int.MinValue;
            _lastWaveLeanWeightShaderByte = int.MinValue;
            _lastImmersionDepthShaderByte = int.MinValue;
        }

        private void PublishQuantizedShaderFloat(int shaderId, float value, ref int previousByte, float minValue, float maxValue)
        {
            float span = math.max(0.0001f, maxValue - minValue);
            float normalized = math.saturate((value - minValue) * math.rcp(span));
            int quantized = (int)math.round(normalized * 255f);
            if (quantized == previousByte)
                return;

            previousByte = quantized;
            QueueSwimShaderFloat(shaderId, minValue + quantized * 0.00392156862745f * span);
        }

        private void PublishBreathingPhase(float phase)
        {
            float clamped = math.clamp(phase, -1f, 1f);
            float roundingOffset = math.select(-0.5f, 0.5f, clamped >= 0f);
            int quantized = (int)(clamped * 127f + roundingOffset);
            if (quantized == _lastBreathingPhaseShaderByte)
                return;

            _lastBreathingPhaseShaderByte = quantized;
            float breathingPhase = quantized * 0.00787401575f;
            QueueSwimShaderFloat(_BreathingPhaseShaderId, breathingPhase);
            if (TryGetKineticMatrixSinkHot(out IKineticCharacterPresentationSink kineticSink))
            {
                kineticSink.SubmitSwimPresentation(
                    _waveSlopeForwardCurrent,
                    _waveSlopeLateralCurrent,
                    _waveCrestReachCurrent,
                    _waveDescentTuckCurrent,
                    _waveLeanWeightCurrent,
                    _immersionDepthCurrent,
                    breathingPhase,
                    _equippedToolBlendCurrent);
            }
        }

        private void QueueSwimShaderFloat(int shaderId, float value)
        {
            if (shaderId == _WaveSlopeForwardShaderId)
            {
                _pendingWaveSlopeForwardShaderValue = value;
                _waveSlopeForwardShaderDirty = true;
            }
            else if (shaderId == _WaveSlopeLateralShaderId)
            {
                _pendingWaveSlopeLateralShaderValue = value;
                _waveSlopeLateralShaderDirty = true;
            }
            else if (shaderId == _WaveSlopeXShaderId)
            {
                _pendingWaveSlopeXShaderValue = value;
                _waveSlopeXShaderDirty = true;
            }
            else if (shaderId == _WaveSlopeZShaderId)
            {
                _pendingWaveSlopeZShaderValue = value;
                _waveSlopeZShaderDirty = true;
            }
            else if (shaderId == _WaveCrestReachShaderId)
            {
                _pendingWaveCrestReachShaderValue = value;
                _waveCrestReachShaderDirty = true;
            }
            else if (shaderId == _WaveDescentTuckShaderId)
            {
                _pendingWaveDescentTuckShaderValue = value;
                _waveDescentTuckShaderDirty = true;
            }
            else if (shaderId == _WaveLeanWeightShaderId)
            {
                _pendingWaveLeanWeightShaderValue = value;
                _waveLeanWeightShaderDirty = true;
            }
            else if (shaderId == _ImmersionDepthShaderId)
            {
                _pendingImmersionDepthShaderValue = value;
                _immersionDepthShaderDirty = true;
            }
            else if (shaderId == _BreathingPhaseShaderId)
            {
                _pendingBreathingPhaseShaderValue = value;
                _breathingPhaseShaderDirty = true;
            }
            else if (shaderId == _SwimVatSpeedScalarShaderId)
            {
                _pendingSwimVatSpeedScalarShaderValue = value;
                _swimVatSpeedScalarShaderDirty = true;
            }
        }

        private void FlushQueuedSwimShaderFloats()
        {
            if (_waveSlopeForwardShaderDirty)
            {
                _waveSlopeForwardShaderDirty = false;
                Shader.SetGlobalFloat(_WaveSlopeForwardShaderId, _pendingWaveSlopeForwardShaderValue);
            }

            if (_waveSlopeLateralShaderDirty)
            {
                _waveSlopeLateralShaderDirty = false;
                Shader.SetGlobalFloat(_WaveSlopeLateralShaderId, _pendingWaveSlopeLateralShaderValue);
            }

            if (_waveSlopeXShaderDirty)
            {
                _waveSlopeXShaderDirty = false;
                Shader.SetGlobalFloat(_WaveSlopeXShaderId, _pendingWaveSlopeXShaderValue);
            }

            if (_waveSlopeZShaderDirty)
            {
                _waveSlopeZShaderDirty = false;
                Shader.SetGlobalFloat(_WaveSlopeZShaderId, _pendingWaveSlopeZShaderValue);
            }

            if (_waveCrestReachShaderDirty)
            {
                _waveCrestReachShaderDirty = false;
                Shader.SetGlobalFloat(_WaveCrestReachShaderId, _pendingWaveCrestReachShaderValue);
            }

            if (_waveDescentTuckShaderDirty)
            {
                _waveDescentTuckShaderDirty = false;
                Shader.SetGlobalFloat(_WaveDescentTuckShaderId, _pendingWaveDescentTuckShaderValue);
            }

            if (_waveLeanWeightShaderDirty)
            {
                _waveLeanWeightShaderDirty = false;
                Shader.SetGlobalFloat(_WaveLeanWeightShaderId, _pendingWaveLeanWeightShaderValue);
            }

            if (_immersionDepthShaderDirty)
            {
                _immersionDepthShaderDirty = false;
                Shader.SetGlobalFloat(_ImmersionDepthShaderId, _pendingImmersionDepthShaderValue);
            }

            if (_breathingPhaseShaderDirty)
            {
                _breathingPhaseShaderDirty = false;
                Shader.SetGlobalFloat(_BreathingPhaseShaderId, _pendingBreathingPhaseShaderValue);
            }

            if (_swimVatSpeedScalarShaderDirty)
            {
                _swimVatSpeedScalarShaderDirty = false;
                Shader.SetGlobalFloat(_SwimVatSpeedScalarShaderId, _pendingSwimVatSpeedScalarShaderValue);
            }
        }

        private void ApplyRootPose(
            SwimPresentationProfile profile,
            Vector3 velocity,
            float speedDelta,
            float dt)
        {
            float bodyLagDegrees = DeltaAngleDegreesNoMathf(playerMovement.BodyYaw, playerMovement.CameraYaw);
            float targetYawLag = bodyLagDegrees * bodyYawLagInfluence;
            float targetRollLag = -bodyLagDegrees * 0.12f;

            float turnSmoothTime = math.max(0.02f, steeringResponseSmoothTime / math.max(0.5f, profile.TurnLagResponse * 0.18f));
            _turnLagYawCurrent = SmoothDampValue(
                _turnLagYawCurrent,
                targetYawLag * profile.TurnLagYaw,
                ref _turnLagYawVelocity,
                turnSmoothTime,
                dt);
            _turnLagRollCurrent = SmoothDampValue(
                _turnLagRollCurrent,
                targetRollLag * profile.TurnLagRoll,
                ref _turnLagRollVelocity,
                turnSmoothTime,
                dt);

            float idleCycle = _idleTimer * profile.IdleDriftFrequency;
            float idleSin = ApproximateSinCycle01(idleCycle);
            float idleCos = ApproximateCosCycle01(idleCycle * 0.7f);
            float strokeSin = ApproximateSinCycle01(_strokePhase);
            float strokeCos = ApproximateCosCycle01(_strokePhase);
            float accelKick = math.clamp(speedDelta * profile.AccelerationKickAmplitude, -profile.AccelerationKickAmplitude, profile.AccelerationKickAmplitude);
            float traumaPresentationScale = 1f - _physicalTraumaBlendCurrent * activeTraumaPresentationSuppression;
            float presentationWeight = _presentationBlend * _toolSuppressionWeight * traumaPresentationScale;
            float surfaceFramingWeight = ResolveSurfaceFramingWeight();
            float toolFramingWeight = _equippedToolBlendCurrent;
            float surfaceIdleWeight = ResolveSurfaceIdleWeight();
            float surfaceBreathSin = ApproximateSinCycle01(_idleTimer * surfaceBreathFrequency);
            float breathingPhase01 = math.frac(_idleTimer * math.max(surfaceBreathFrequency, 0.0001f));
            float breathingPulse = ((math.abs(breathingPhase01 * 2f - 1f) * 2f) - 1f) * presentationWeight;
            PublishBreathingPhase(breathingPulse);
            float surfaceWaveCycle = _idleTimer * surfaceWaveBobFrequency;
            float surfaceWaveSin = ApproximateSinCycle01(surfaceWaveCycle + 0.1193662f);
            float surfaceWaveCos = ApproximateCosCycle01(surfaceWaveCycle * 0.82f);
            float leftObstacleWeight = _leftObstacleWeightCurrent * _currentLeftGuideWeight;
            float rightObstacleWeight = _rightObstacleWeightCurrent * _currentRightGuideWeight;
            float obstacleAverage = (leftObstacleWeight + rightObstacleWeight) * 0.5f;
            float obstacleDifference = rightObstacleWeight - leftObstacleWeight;
            float obstacleVertical = (_leftObstacleVerticalBiasCurrent * leftObstacleWeight + _rightObstacleVerticalBiasCurrent * rightObstacleWeight) * 0.5f;
            float rootObstacleSmooth = math.max(0.0001f, obstacleRootSmoothTime * ResolvePoseLagMultiplier(profile));
            _rootObstacleAverageCurrent = SmoothDampValue(
                _rootObstacleAverageCurrent,
                obstacleAverage,
                ref _rootObstacleAverageVelocity,
                rootObstacleSmooth,
                dt);
            _rootObstacleDifferenceCurrent = SmoothDampValue(
                _rootObstacleDifferenceCurrent,
                obstacleDifference,
                ref _rootObstacleDifferenceVelocity,
                rootObstacleSmooth,
                dt);
            _rootObstacleVerticalCurrent = SmoothDampValue(
                _rootObstacleVerticalCurrent,
                obstacleVertical,
                ref _rootObstacleVerticalVelocity,
                rootObstacleSmooth,
                dt);

            Vector3 localPosition = profile.BaseLocalPosition;
            localPosition.x += idleCos * profile.IdleDriftAmplitude * 0.75f * presentationWeight;
            localPosition.x += surfaceWaveCos * surfaceWaveBobAmplitude * 0.18f * surfaceIdleWeight * presentationWeight;
            localPosition.x += (_directionalCorrectionCurrent + _cameraTurnSwayCurrent * cameraTurnSwayHandInfluence) * steeringRootLateralBias * presentationWeight;
            localPosition.x -= _rootObstacleDifferenceCurrent * obstacleRootLateralBias * presentationWeight;
            localPosition.y += idleSin * profile.IdleDriftAmplitude * presentationWeight;
            localPosition.y += (surfaceBreathSin * surfaceBreathAmplitude + surfaceWaveSin * surfaceWaveBobAmplitude) * surfaceIdleWeight * presentationWeight;
            localPosition.y += strokeSin * profile.StrokeVerticalAmplitude * presentationWeight;
            localPosition.y -= math.saturate(math.abs(velocity.y)) * profile.InertialSinkAmplitude * presentationWeight;
            localPosition.y += velocity.y * verticalVelocityInfluence * presentationWeight;
            localPosition.y -= _heavyCarryLoadCurrent * heavyCarryRootDownBias * presentationWeight;
            localPosition.y += math.max(0f, _rootObstacleVerticalCurrent) * obstacleRootUpwardBias * presentationWeight;
            localPosition.y -= surfaceRootLowering * surfaceFramingWeight * presentationWeight;
            localPosition.y -= toolRootLowering * toolFramingWeight * presentationWeight;
            localPosition.z += strokeCos * profile.StrokeSurgeAmplitude * presentationWeight;
            localPosition.z += surfaceBreathSin * surfaceBreathAmplitude * 0.22f * surfaceIdleWeight * presentationWeight;
            localPosition.z += _transportBoostCurrent * ResolveTransportRootForwardBias() * presentationWeight;
            localPosition.z -= accelKick;
            localPosition.z -= _heavyCarryLoadCurrent * heavyCarryRootRearBias * presentationWeight;
            localPosition.z -= _rootObstacleAverageCurrent * obstacleRootRearBias * presentationWeight;
            localPosition.z -= surfaceRootRearBias * surfaceFramingWeight * presentationWeight;
            localPosition.z -= toolRootRearBias * toolFramingWeight * presentationWeight;
            localPosition += _rootToolPositionBiasCurrent * presentationWeight;

            Vector3 localEuler = profile.BaseLocalEuler;
            localEuler.x += strokeSin * profile.StrokePitchAmplitude * presentationWeight;
            localEuler.x += surfaceBreathSin * surfaceWavePitchAmplitude * surfaceIdleWeight * presentationWeight;
            localEuler.x += _heavyCarryLoadCurrent * heavyCarryRootPitchBias * presentationWeight;
            localEuler.x += _rootObstacleAverageCurrent * obstacleRootPitchCompression * presentationWeight;
            localEuler.y += _turnLagYawCurrent * presentationWeight;
            localEuler.y -= _directionalCorrectionCurrent * steeringRootYawBias * presentationWeight;
            localEuler.y -= _cameraTurnSwayCurrent * cameraTurnSwayRootYawBias * presentationWeight;
            localEuler.y -= _rootObstacleDifferenceCurrent * obstacleRootYawBias * presentationWeight;
            localEuler.z += strokeCos * profile.StrokeRollAmplitude * presentationWeight + _turnLagRollCurrent * presentationWeight;
            localEuler.z -= _directionalCorrectionCurrent * steeringRootRollBias * presentationWeight;
            localEuler.z += surfaceWaveSin * surfaceWaveRollAmplitude * surfaceIdleWeight * presentationWeight;
            localEuler.z -= _cameraTurnSwayCurrent * cameraTurnSwayRootRollBias * presentationWeight;
            localEuler.z -= _rootObstacleDifferenceCurrent * obstacleRootRollBias * presentationWeight;
            localEuler += _rootToolEulerBiasCurrent * presentationWeight;

            ApplyTraumaRootBias(ref localPosition, ref localEuler);

            float poseLagMultiplier = ResolvePoseLagMultiplier(profile);
            float rootPositionSmooth = math.max(0.0001f, rootPosePositionSmoothTime * poseLagMultiplier);
            float rootRotationSmooth = math.max(0.0001f, rootPoseRotationSmoothTime * poseLagMultiplier);
            Quaternion environmentalRotation = Quaternion.identity;
            if (playerMovement != null)
            {
                environmentalRotation =
                    playerMovement.CurrentSurfaceWavePoseLocalRotation *
                    playerMovement.CurrentUnderwaterTurbulencePoseLocalRotation;
            }

            if (!_poseStateInitialized)
            {
                _currentLocalPosition = localPosition;
                _rootPoseEulerCurrent = localEuler;
                _currentLocalRotation = ResolveEulerRotationNoTrig(_rootPoseEulerCurrent) * environmentalRotation;
            }
            else
            {
                _currentLocalPosition = SmoothDampVector(
                    _currentLocalPosition,
                    localPosition,
                    ref _rootPosePositionVelocity,
                    rootPositionSmooth,
                    dt);
                _rootPoseEulerCurrent = SmoothDampAngles(
                    _rootPoseEulerCurrent,
                    localEuler,
                    ref _rootPoseEulerVelocity,
                    rootRotationSmooth,
                    dt);
                _currentLocalRotation = ResolveEulerRotationNoTrig(_rootPoseEulerCurrent) * environmentalRotation;
            }

            if (viewModelRoot != null)
                viewModelRoot.SetLocalPositionAndRotation(_currentLocalPosition, _currentLocalRotation);
        }

        private void ApplyGuidePoses(
            SwimPresentationProfile profile,
            float planarSpeed,
            Vector3 velocity,
            float dt)
        {
            float traumaGuideScale = 1f - _physicalTraumaBlendCurrent * activeTraumaGuideSuppression;
            float presentationWeight = _presentationBlend * _toolSuppressionWeight * traumaGuideScale;
            float modeWeight;
            float reachScale;
            float pullScale;
            float outwardScale;
            float verticalScale;
            float yawScale;
            float rollScale;
            float pitchScale;
            float syncBias;

            ResolveGuideModeTuning(
                profile,
                planarSpeed,
                out modeWeight,
                out reachScale,
                out pullScale,
                out outwardScale,
                out verticalScale,
                out yawScale,
                out rollScale,
                out pitchScale,
                out syncBias);

            float guideWeight = math.saturate(presentationWeight * modeWeight);
            _currentGuideWeight = guideWeight;
            float leftGuideWeight = math.saturate(guideWeight * _leftGuideVisibilityWeight);
            float rightGuideWeight = math.saturate(guideWeight * _rightGuideVisibilityWeight);
            _currentLeftGuideWeight = leftGuideWeight;
            _currentRightGuideWeight = rightGuideWeight;

            ApplySingleGuidePose(
                leftHandGuide,
                profile,
                true,
                planarSpeed,
                velocity,
                dt,
                leftGuideWeight,
                reachScale,
                pullScale,
                outwardScale,
                verticalScale,
                yawScale,
                rollScale,
                pitchScale,
                syncBias);

            ApplySingleGuidePose(
                rightHandGuide,
                profile,
                false,
                planarSpeed,
                velocity,
                dt,
                rightGuideWeight,
                reachScale,
                pullScale,
                outwardScale,
                verticalScale,
                yawScale,
                rollScale,
                pitchScale,
                syncBias);
        }

        private void ResolveGuideModeTuning(
            SwimPresentationProfile profile,
            float planarSpeed,
            out float modeWeight,
            out float reachScale,
            out float pullScale,
            out float outwardScale,
            out float verticalScale,
            out float yawScale,
            out float rollScale,
            out float pitchScale,
            out float syncBias)
        {
            modeWeight = 0f;
            reachScale = 0f;
            pullScale = 0f;
            outwardScale = 0f;
            verticalScale = 0f;
            yawScale = 0f;
            rollScale = 0f;
            pitchScale = 0f;
            syncBias = 0f;

            switch (_currentMode)
            {
                case PlayerSwimPresentationMode.ShallowWade:
                    modeWeight = 0.18f;
                    reachScale = 0.12f;
                    pullScale = 0.08f;
                    outwardScale = 0.14f;
                    verticalScale = 0.1f;
                    yawScale = 0.1f;
                    rollScale = 0.08f;
                    pitchScale = 0.1f;
                    syncBias = 0.4f;
                    break;

                case PlayerSwimPresentationMode.SurfaceTread:
                    modeWeight = 0.55f;
                    reachScale = 0.18f;
                    pullScale = 0.16f;
                    outwardScale = 0.65f;
                    verticalScale = 0.34f;
                    yawScale = 0.72f;
                    rollScale = 0.32f;
                    pitchScale = 0.28f;
                    syncBias = 1f;
                    break;

                case PlayerSwimPresentationMode.SurfaceStroke:
                    modeWeight = 0.9f;
                    reachScale = 0.58f;
                    pullScale = 0.6f;
                    outwardScale = 0.82f;
                    verticalScale = 0.52f;
                    yawScale = 0.84f;
                    rollScale = 0.52f;
                    pitchScale = 0.62f;
                    syncBias = profile.SurfaceHandSync;
                    break;

                case PlayerSwimPresentationMode.UnderwaterNeutral:
                    modeWeight = 0.35f;
                    reachScale = 0.28f;
                    pullScale = 0.18f;
                    outwardScale = 0.3f;
                    verticalScale = 0.22f;
                    yawScale = 0.28f;
                    rollScale = 0.24f;
                    pitchScale = 0.24f;
                    syncBias = 0f;
                    break;

                case PlayerSwimPresentationMode.UnderwaterStroke:
                    modeWeight = math.saturate(planarSpeed / math.max(0.01f, profile.UnderwaterStrokeStartSpeed * 2f));
                    modeWeight = math.max(0.65f, modeWeight);
                    reachScale = 1f;
                    pullScale = 1f;
                    outwardScale = 1f;
                    verticalScale = 1f;
                    yawScale = 1f;
                    rollScale = 1f;
                    pitchScale = 1f;
                    syncBias = 0f;
                    break;

                case PlayerSwimPresentationMode.UnderwaterGlide:
                    modeWeight = 0.55f;
                    reachScale = 0.48f;
                    pullScale = 0.22f;
                    outwardScale = 0.38f;
                    verticalScale = 0.28f;
                    yawScale = 0.42f;
                    rollScale = 0.36f;
                    pitchScale = 0.34f;
                    syncBias = 0f;
                    break;

                case PlayerSwimPresentationMode.UnderwaterSprint:
                    modeWeight = 1.15f;
                    reachScale = 0.86f;
                    pullScale = 1.24f;
                    outwardScale = 0.74f;
                    verticalScale = 0.84f;
                    yawScale = 1.15f;
                    rollScale = 1.08f;
                    pitchScale = 1.1f;
                    syncBias = 0f;
                    break;
            }
        }

        private void ApplySingleGuidePose(
            Transform guide,
            SwimPresentationProfile profile,
            bool isLeft,
            float planarSpeed,
            Vector3 velocity,
            float dt,
            float guideWeight,
            float reachScale,
            float pullScale,
            float outwardScale,
            float verticalScale,
            float yawScale,
            float rollScale,
            float pitchScale,
            float syncBias)
        {
            Vector3 basePosition = isLeft ? profile.LeftGuideBaseLocalPosition : profile.RightGuideBaseLocalPosition;
            Vector3 baseEuler = isLeft ? profile.LeftGuideBaseLocalEuler : profile.RightGuideBaseLocalEuler;
            float sideSign = isLeft ? -1f : 1f;

            float alternatingOffset = isLeft ? 0f : 0.5f;
            float phaseOffset = math.lerp(alternatingOffset, 0f, syncBias);
            float combinedCorrection = _directionalCorrectionCurrent + _cameraTurnSwayCurrent * cameraTurnSwayHandInfluence;
            float correctionBias = -sideSign * combinedCorrection;
            float surfaceFramingWeight = ResolveSurfaceFramingWeight();
            float toolFramingWeight = _equippedToolBlendCurrent;
            float activeToolFramingWeight = _activeToolUseBlendCurrent;
            float obstaclePressure = isLeft ? _leftObstacleWeightCurrent : _rightObstacleWeightCurrent;
            float oppositeObstaclePressure = isLeft ? _rightObstacleWeightCurrent : _leftObstacleWeightCurrent;
            float compensationPressure = math.max(0f, oppositeObstaclePressure - obstaclePressure * 0.35f);
            bool toolHand =
                toolFramingWeight > 0.0001f &&
                ((isLeft && equippedToolHand == PlayerToolSwimHandedness.Left) ||
                 (!isLeft && equippedToolHand == PlayerToolSwimHandedness.Right));
            bool supportHand = toolFramingWeight > 0.0001f && !toolHand;
            float handPhaseLeadScale = 1f;
            if (toolHand)
            {
                handPhaseLeadScale -= equippedToolToolHandPhaseLeadSuppression * (toolFramingWeight + activeToolFramingWeight * 0.65f);
            }
            else if (supportHand)
            {
                handPhaseLeadScale += equippedToolSupportHandPhaseLeadBoost * (toolFramingWeight + activeToolFramingWeight);
            }

            if (obstaclePressure > 0.0001f)
                handPhaseLeadScale *= math.max(0.2f, 1f - blockedHandPhaseLeadSuppression * obstaclePressure);

            if (compensationPressure > 0.0001f)
                handPhaseLeadScale += blockedSupportHandPhaseLeadBoost * compensationPressure;

            float phaseLeadSuppression = 1f - surfaceFramingWeight * surfaceStrokePhaseSuppression;
            float phaseLead =
                correctionBias * steeringStrokePhaseLead +
                (-sideSign * _cameraTurnSwayCurrent) * cameraTurnStrokePhaseLead +
                math.clamp(velocity.y * verticalStrokePhaseBias, -verticalStrokePhaseBias, verticalStrokePhaseBias);
            phaseLead *= handPhaseLeadScale;
            float phase = _strokePhase + phaseOffset + phaseLead * guideWeight * phaseLeadSuppression;
            phase -= math.floor(phase);

            float strokeSin = ApproximateSinCycle01(phase);
            float strokeCos = ApproximateCosCycle01(phase);
            float pull = ShapeStrokeHalfWave(math.max(0f, strokeSin), strokePullSharpness);
            float recover = ShapeStrokeHalfWave(math.max(0f, -strokeSin), strokeRecoverySharpness);
            float sweep = strokeCos;
            float toolMotionScale = 1f;
            float strokePowerScale = 1f;
            if (toolHand)
            {
                float suppressionWeight = toolFramingWeight + activeToolFramingWeight * 0.7f;
                toolMotionScale = math.max(0.2f, 1f - equippedToolToolHandMotionSuppression * suppressionWeight);
                strokePowerScale = math.max(0.18f, 1f - equippedToolToolHandMotionSuppression * suppressionWeight);
            }
            else if (supportHand)
            {
                float supportBoostWeight = toolFramingWeight + activeToolFramingWeight;
                toolMotionScale += equippedToolSupportHandMotionBoost * supportBoostWeight;
                strokePowerScale += equippedToolSupportHandMotionBoost * supportBoostWeight;
            }

            if (obstaclePressure > 0.0001f)
            {
                float blockedScale = math.max(0.18f, 1f - blockedHandStrokeSuppression * obstaclePressure);
                toolMotionScale *= blockedScale;
                strokePowerScale *= blockedScale;
            }

            if (compensationPressure > 0.0001f)
            {
                float compensationBoost = 1f + blockedSupportHandCompensationBoost * compensationPressure;
                toolMotionScale *= compensationBoost;
                strokePowerScale *= compensationBoost;
            }

            if (_heavyCarryLoadCurrent > 0.0001f)
            {
                float heavyCarryMotionScale = math.lerp(1f, heavyCarryGuideMotionMultiplier, _heavyCarryLoadCurrent);
                toolMotionScale *= heavyCarryMotionScale;
                strokePowerScale *= heavyCarryMotionScale;
            }

            float sprintTuck = _currentMode == PlayerSwimPresentationMode.UnderwaterSprint
                ? profile.SprintHandTuckDistance
                : 0f;
            float speedBias = math.saturate(planarSpeed / math.max(0.01f, profile.UnderwaterSprintStartSpeed));
            float verticalBias = math.clamp(velocity.y * 0.02f, -0.03f, 0.03f);
            float verticalPose = _currentVerticalPoseBias;
            float ascendBias = math.max(0f, verticalPose);
            float descendBias = math.max(0f, -verticalPose);
            float framingOutwardBias = surfaceFramingWeight * surfaceFramingOutwardBias + toolFramingWeight * toolFramingOutwardBias;
            float framingDownBias = surfaceFramingWeight * surfaceFramingDownBias + toolFramingWeight * toolFramingDownBias + activeToolFramingWeight * toolFramingDownBias * 0.35f;
            float framingRearBias = surfaceFramingWeight * surfaceFramingRearBias + toolFramingWeight * toolFramingRearBias + activeToolFramingWeight * toolFramingRearBias * 0.4f;
            Vector3 toolPositionBias = isLeft ? _leftGuideToolPositionBiasCurrent : _rightGuideToolPositionBiasCurrent;
            Vector3 toolEulerBias = isLeft ? _leftGuideToolEulerBiasCurrent : _rightGuideToolEulerBiasCurrent;
            float strokeDrive = pull - recover * 0.35f;

            Vector3 localPosition = basePosition;
            localPosition.x += sideSign * (recover * profile.HandOutwardDistance * outwardScale * strokePowerScale);
            localPosition.x += sideSign * (sweep * profile.HandOutwardDistance * 0.35f * outwardScale * guideWeight * toolMotionScale);
            localPosition.x += sideSign * ascendBias * profile.HandOutwardDistance * 0.22f * guideWeight * toolMotionScale;
            localPosition.x -= sideSign * descendBias * profile.HandOutwardDistance * 0.18f * guideWeight * toolMotionScale;
            localPosition.x += sideSign * correctionBias * profile.HandOutwardDistance * steeringOutwardBias * guideWeight * toolMotionScale;
            localPosition.x += sideSign * framingOutwardBias * guideWeight;
            localPosition.y -= pull * profile.HandDownwardDistance * verticalScale * strokePowerScale;
            localPosition.y += recover * profile.HandRecoveryLift * verticalScale * strokePowerScale;
            localPosition.y += verticalBias * guideWeight;
            localPosition.y -= ascendBias * profile.HandDownwardDistance * 0.55f * guideWeight * toolMotionScale;
            localPosition.y += descendBias * profile.HandRecoveryLift * 0.24f * guideWeight * toolMotionScale;
            localPosition.y += correctionBias * profile.HandRecoveryLift * steeringVerticalBias * guideWeight * toolMotionScale;
            localPosition.y -= framingDownBias * guideWeight;
            localPosition.y -= _heavyCarryLoadCurrent * heavyCarryGuideDownBias * guideWeight;
            localPosition.z += recover * profile.HandReachDistance * reachScale * strokePowerScale;
            localPosition.z -= pull * profile.HandPullDistance * pullScale * strokePowerScale;
            localPosition.z -= sprintTuck * guideWeight;
            localPosition.z -= ascendBias * profile.HandPullDistance * ascendPullbackBias * guideWeight * toolMotionScale;
            localPosition.z += descendBias * profile.HandReachDistance * descendReachBias * guideWeight * toolMotionScale;
            localPosition.z += correctionBias * profile.HandReachDistance * steeringReachBias * guideWeight * toolMotionScale;
            localPosition.z += _transportBoostCurrent * ResolveTransportGuideForwardBias() * guideWeight;
            localPosition.z -= _heavyCarryLoadCurrent * heavyCarryGuideRearBias * guideWeight;
            localPosition.z -= framingRearBias * guideWeight;
            localPosition += toolPositionBias * guideWeight;

            float maxForward = basePosition.z + profile.HandReachDistance * handForwardReachClamp;
            float maxRear = basePosition.z - profile.HandPullDistance * handRearReachClamp - sprintTuck;
            localPosition.z = math.clamp(localPosition.z, maxRear, maxForward);
            float minAbsX = math.abs(basePosition.x) + framingCenterClearance * math.saturate(surfaceFramingWeight + toolFramingWeight * 0.85f);
            float localAbsX = math.abs(localPosition.x);
            if (localAbsX < minAbsX)
                localPosition.x = sideSign * minAbsX;

            Vector3 localEuler = baseEuler;
            localEuler.x += strokeDrive * profile.HandPitchAmplitude * pitchScale * strokePowerScale;
            localEuler.x -= ascendBias * profile.HandPitchAmplitude * 0.32f * guideWeight * toolMotionScale;
            localEuler.x += descendBias * profile.HandPitchAmplitude * 0.2f * guideWeight * toolMotionScale;
            localEuler.y += sideSign * (strokeDrive * profile.HandYawAmplitude * yawScale * strokePowerScale);
            localEuler.y += _turnLagYawCurrent * 0.18f * guideWeight;
            localEuler.y += sideSign * ascendBias * profile.HandYawAmplitude * 0.12f * guideWeight * toolMotionScale;
            localEuler.y -= sideSign * descendBias * profile.HandYawAmplitude * 0.08f * guideWeight * toolMotionScale;
            localEuler.y += sideSign * correctionBias * profile.HandYawAmplitude * steeringYawBias * guideWeight * toolMotionScale;
            localEuler.z += sideSign * (sweep * profile.HandRollAmplitude * rollScale * strokePowerScale);
            localEuler.z += _turnLagRollCurrent * 0.22f * guideWeight;
            localEuler.z += sideSign * ascendBias * profile.HandRollAmplitude * 0.12f * guideWeight * toolMotionScale;
            localEuler.z += sideSign * correctionBias * profile.HandRollAmplitude * steeringRollBias * guideWeight * toolMotionScale;
            localEuler.x += speedBias * profile.HandPitchAmplitude * 0.08f * guideWeight;
            localEuler += toolEulerBias * guideWeight;
            ApplyObstacleReactiveBias(
                guide,
                basePosition,
                ref localPosition,
                ref localEuler,
                isLeft,
                guideWeight,
                dt);
            ApplyTraumaGuideBias(ref localPosition, ref localEuler, isLeft, guideWeight);
            localPosition = basePosition + ((localPosition - basePosition) * guideWeight);
            Vector3 blendedEuler = baseEuler + ((localEuler - baseEuler) * guideWeight);
            float obstacleLagWeight = isLeft ? _leftObstacleWeightCurrent : _rightObstacleWeightCurrent;
            float poseLagMultiplier = ResolveGuidePoseLagMultiplier(profile, toolHand, supportHand, obstacleLagWeight);
            float guidePositionSmooth = math.max(0.0001f, guidePosePositionSmoothTime * poseLagMultiplier);
            float guideRotationSmooth = math.max(0.0001f, guidePoseRotationSmoothTime * poseLagMultiplier);
            Vector3 appliedLocalPosition;
            Quaternion appliedLocalRotation;

            if (isLeft)
            {
                if (!_poseStateInitialized)
                {
                    _leftGuideCurrentLocalPosition = localPosition;
                    _leftGuideEulerCurrent = blendedEuler;
                }
                else
                {
                    _leftGuideCurrentLocalPosition = SmoothDampVector(
                        _leftGuideCurrentLocalPosition,
                        localPosition,
                        ref _leftGuidePosePositionVelocity,
                        guidePositionSmooth,
                        dt);
                    _leftGuideEulerCurrent = SmoothDampAngles(
                        _leftGuideEulerCurrent,
                        blendedEuler,
                        ref _leftGuideEulerVelocity,
                        guideRotationSmooth,
                        dt);
                }

                _leftGuideCurrentLocalRotation = ResolveEulerRotationNoTrig(_leftGuideEulerCurrent);
                _debugStrokePhaseLeadCurrent = phaseLead;
                appliedLocalPosition = _leftGuideCurrentLocalPosition;
                appliedLocalRotation = _leftGuideCurrentLocalRotation;
            }
            else
            {
                if (!_poseStateInitialized)
                {
                    _rightGuideCurrentLocalPosition = localPosition;
                    _rightGuideEulerCurrent = blendedEuler;
                }
                else
                {
                    _rightGuideCurrentLocalPosition = SmoothDampVector(
                        _rightGuideCurrentLocalPosition,
                        localPosition,
                        ref _rightGuidePosePositionVelocity,
                        guidePositionSmooth,
                        dt);
                    _rightGuideEulerCurrent = SmoothDampAngles(
                        _rightGuideEulerCurrent,
                        blendedEuler,
                        ref _rightGuideEulerVelocity,
                        guideRotationSmooth,
                        dt);
                }

                _rightGuideCurrentLocalRotation = ResolveEulerRotationNoTrig(_rightGuideEulerCurrent);
                appliedLocalPosition = _rightGuideCurrentLocalPosition;
                appliedLocalRotation = _rightGuideCurrentLocalRotation;
            }

            if (guide != null)
                guide.SetLocalPositionAndRotation(appliedLocalPosition, appliedLocalRotation);
        }

        private void UpdatePhysicalTrauma(float dt)
        {
            if (_physicalTraumaHoldTimer > 0f)
            {
                _physicalTraumaHoldTimer = math.max(0f, _physicalTraumaHoldTimer - dt);
            }
            else
            {
                _physicalTraumaBlendTarget = 0f;
            }

            if (_physicalTraumaBlendTarget > _physicalTraumaBlendCurrent)
            {
                float blendT = ResolveDecayBlend(math.max(activeTraumaBlendInSpeed, 0.01f), dt);
                _physicalTraumaBlendCurrent = math.lerp(_physicalTraumaBlendCurrent, _physicalTraumaBlendTarget, blendT);
            }
            else
            {
                _physicalTraumaBlendCurrent = SmoothDampValue(
                    _physicalTraumaBlendCurrent,
                    _physicalTraumaBlendTarget,
                    ref _physicalTraumaBlendVelocity,
                    activeTraumaRecoverySmoothTime,
                    dt);
            }

            Vector3 targetImpulse = _physicalTraumaBlendTarget > 0f ? _physicalTraumaLocalImpulseTarget : Vector3.zero;
            _physicalTraumaLocalImpulseCurrent = SmoothDampVector(
                _physicalTraumaLocalImpulseCurrent,
                targetImpulse,
                ref _physicalTraumaLocalImpulseVelocity,
                activeTraumaRecoverySmoothTime,
                dt);

            if (_physicalTraumaBlendCurrent <= 0.001f && _physicalTraumaBlendTarget <= 0.001f)
            {
                _physicalTraumaBlendCurrent = 0f;
                _physicalTraumaBlendVelocity = 0f;
                _physicalTraumaLocalImpulseCurrent = Vector3.zero;
                _physicalTraumaLocalImpulseTarget = Vector3.zero;
                _physicalTraumaLocalImpulseVelocity = Vector3.zero;
            }
        }

        private void ApplyTraumaRootBias(ref Vector3 localPosition, ref Vector3 localEuler)
        {
            float traumaBlend = _physicalTraumaBlendCurrent;
            if (traumaBlend <= 0.001f)
                return;

            Vector3 traumaImpulse = _physicalTraumaLocalImpulseCurrent;
            localPosition.x += traumaImpulse.x * activeTraumaRootOffsetDistance * 0.8f * traumaBlend;
            localPosition.y += traumaImpulse.y * activeTraumaRootOffsetDistance * 0.65f * traumaBlend;
            localPosition.z += traumaImpulse.z * activeTraumaRootOffsetDistance * traumaBlend;
            localEuler.x += -traumaImpulse.y * activeTraumaRootEulerDegrees * traumaBlend;
            localEuler.y += traumaImpulse.x * activeTraumaRootEulerDegrees * 0.55f * traumaBlend;
            localEuler.z += (-traumaImpulse.x + traumaImpulse.z * 0.35f) * activeTraumaRootEulerDegrees * traumaBlend;
        }

        private void ApplyTraumaGuideBias(ref Vector3 localPosition, ref Vector3 localEuler, bool isLeft, float guideWeight)
        {
            float traumaBlend = _physicalTraumaBlendCurrent * guideWeight;
            if (traumaBlend <= 0.001f)
                return;

            Vector3 traumaImpulse = _physicalTraumaLocalImpulseCurrent;
            float sideSign = isLeft ? -1f : 1f;
            float sideBias = sideSign * (-traumaImpulse.x * 0.55f + traumaImpulse.z * 0.18f);
            localPosition.x += sideBias * activeTraumaGuideOffsetDistance * traumaBlend;
            localPosition.y += traumaImpulse.y * activeTraumaGuideOffsetDistance * 0.7f * traumaBlend;
            localPosition.z += traumaImpulse.z * activeTraumaGuideOffsetDistance * traumaBlend;
            localEuler.x += -traumaImpulse.y * activeTraumaGuideEulerDegrees * traumaBlend;
            localEuler.y += sideSign * traumaImpulse.x * activeTraumaGuideEulerDegrees * 0.8f * traumaBlend;
            localEuler.z += sideSign * (traumaImpulse.z + traumaImpulse.x * 0.4f) * activeTraumaGuideEulerDegrees * traumaBlend;
        }

        private void UpdateToolSuppression(float dt)
        {
            PlayerInputState inputState = _inputService != null && _inputService.IsPlayerInputEnabled
                ? _inputService.GetState()
                : default;

            bool toolEquipped = playerToolManager != null &&
                                playerToolManager.CurrentTool != null &&
                                !playerToolManager.IsSwapping;
            PlayerToolSwimContract toolSwimContract = toolEquipped
                ? playerToolManager.CurrentToolSwimContract
                : null;
            bool toolUsing = toolEquipped &&
                             (inputState.HasAction(PlayerInputAction.PrimaryFire) ||
                              inputState.HasAction(PlayerInputAction.SecondaryFire));

            PlayerToolSwimHandedness toolHand = toolSwimContract != null
                ? toolSwimContract.ToolHand
                : equippedToolHand;
            float targetWeight = toolEquipped
                ? (toolSwimContract != null ? toolSwimContract.SwimRootPresentationWeight : equippedToolRootPresentationWeight)
                : 1f;
            float targetSupportHandWeight = toolEquipped
                ? (toolSwimContract != null ? toolSwimContract.SwimSupportHandWeight : equippedToolSupportHandWeight)
                : 1f;
            float targetToolHandWeight = toolEquipped
                ? (toolUsing
                    ? math.min(toolSwimContract != null ? toolSwimContract.SwimToolHandWeight : equippedToolPresentationWeight, 0.08f)
                    : (toolSwimContract != null ? toolSwimContract.SwimToolHandWeight : equippedToolPresentationWeight))
                : 1f;

            if (toolUsing)
            {
                float supportBoost = toolSwimContract != null
                    ? toolSwimContract.ActiveUseSupportHandBoost
                    : equippedToolActiveUseSupportBoost;
                targetSupportHandWeight = math.saturate(targetSupportHandWeight + supportBoost);
            }

            if (_transportBoostCurrent > 0.0001f)
            {
                targetWeight = math.max(
                    targetWeight,
                    math.lerp(targetWeight, ResolveTransportRootPresentationWeight(), _transportBoostCurrent));
                targetSupportHandWeight = math.max(
                    targetSupportHandWeight,
                    math.lerp(targetSupportHandWeight, ResolveTransportSupportHandWeight(), _transportBoostCurrent));
            }

            float transportPresentationScale = ResolveTransportSwimPresentationScale();
            if (transportPresentationScale < 0.9999f)
            {
                targetWeight *= transportPresentationScale;
                targetSupportHandWeight *= transportPresentationScale;
                targetToolHandWeight *= transportPresentationScale;
            }

            _toolSuppressionWeight = SmoothDampValue(
                _toolSuppressionWeight,
                targetWeight,
                ref _toolSuppressionWeightVelocity,
                toolTransitionSmoothTime,
                dt);
            _equippedToolBlendCurrent = SmoothDampValue(
                _equippedToolBlendCurrent,
                toolEquipped ? 1f : 0f,
                ref _equippedToolBlendVelocity,
                toolTransitionSmoothTime,
                dt);
            _activeToolUseBlendCurrent = SmoothDampValue(
                _activeToolUseBlendCurrent,
                toolUsing ? 1f : 0f,
                ref _activeToolUseBlendVelocity,
                toolTransitionSmoothTime,
                dt);

            float leftTarget = toolHand == PlayerToolSwimHandedness.Left
                ? targetToolHandWeight
                : targetSupportHandWeight;
            float rightTarget = toolHand == PlayerToolSwimHandedness.Right
                ? targetToolHandWeight
                : targetSupportHandWeight;

            _leftGuideVisibilityWeight = SmoothDampValue(
                _leftGuideVisibilityWeight,
                leftTarget,
                ref _leftGuideVisibilityVelocity,
                toolTransitionSmoothTime,
                dt);
            _rightGuideVisibilityWeight = SmoothDampValue(
                _rightGuideVisibilityWeight,
                rightTarget,
                ref _rightGuideVisibilityVelocity,
                toolTransitionSmoothTime,
                dt);
            UpdateToolPoseBiases(toolSwimContract, toolUsing, toolHand, dt);
            PublishToolPoseToKinetic(toolEquipped, toolUsing, toolHand);
        }

        private void PublishToolPoseToKinetic(bool toolEquipped, bool toolUsing, PlayerToolSwimHandedness toolHand)
        {
            if (!toolEquipped || !TryGetKineticMatrixSinkHot(out IKineticCharacterPresentationSink kineticSink))
                return;

            bool rightHand = toolHand == PlayerToolSwimHandedness.Right;
            Vector3 positionBias = rightHand ? _rightGuideToolPositionBiasCurrent : _leftGuideToolPositionBiasCurrent;
            Vector3 eulerBias = rightHand ? _rightGuideToolEulerBiasCurrent : _leftGuideToolEulerBiasCurrent;
            float side = rightHand ? 1f : -1f;
            float3 localPosition = new float3(
                side * (0.22f + math.abs(positionBias.x)),
                1.06f + positionBias.y,
                0.42f + positionBias.z);
            float3 radians = math.radians(new float3(eulerBias.x, eulerBias.y + side * 4f, eulerBias.z));
            float activeWeight = math.saturate(_equippedToolBlendCurrent + (toolUsing ? _activeToolUseBlendCurrent * 0.35f : 0f));
            uint toolHash = ResolveKineticToolHash();
            kineticSink.SubmitToolPose(
                float4x4.TRS(localPosition, quaternion.EulerXYZ(radians), new float3(1f, 1f, 1f)),
                activeWeight,
                toolHash);
        }

        private uint ResolveKineticToolHash()
        {
            if (playerToolManager == null)
                return 0u;

            return playerToolManager.CurrentActiveToolHash;
        }

        private void UpdateCameraTurnSway(float dt)
        {
            if (!_cameraYawInitialized)
            {
                _previousCameraYaw = playerMovement.CameraYaw;
                _cameraYawInitialized = true;
            }

            float currentCameraYaw = playerMovement.CameraYaw;
            float yawDelta = DeltaAngleDegreesNoMathf(_previousCameraYaw, currentCameraYaw);
            _previousCameraYaw = currentCameraYaw;

            float yawVelocityNormalized = dt > 0f
                ? math.clamp(yawDelta / (dt * math.max(1f, cameraTurnSwayMaxDegreesPerSecond)), -1f, 1f)
                : 0f;
            float targetSway = yawVelocityNormalized * cameraTurnSwayInfluence;
            if (_currentMode == PlayerSwimPresentationMode.SurfaceTread ||
                _currentMode == PlayerSwimPresentationMode.SurfaceStroke)
            {
                targetSway *= 1f - cameraTurnSwaySurfaceSuppression;
            }

            _cameraTurnSwayCurrent = SmoothDampValue(
                _cameraTurnSwayCurrent,
                targetSway,
                ref _cameraTurnSwayVelocity,
                steeringResponseSmoothTime,
                dt);
        }

        private void UpdateDirectionalCorrection(
            float planarSpeed,
            Vector3 velocity,
            float dt)
        {
            PlayerInputState inputState = _inputService != null && _inputService.IsPlayerInputEnabled
                ? _inputService.GetState()
                : default;
            float targetCorrection = 0f;
            if (_currentMode != PlayerSwimPresentationMode.Dry &&
                _currentMode != PlayerSwimPresentationMode.ShallowWade)
            {
                float bodyYawRad = playerMovement.BodyYaw * DegreesToRadians;
                ApproximateSinCosFullNoTrig(bodyYawRad, out float sinBodyYaw, out float cosBodyYaw);
                float localLateralVelocity = velocity.x * cosBodyYaw + velocity.z * -sinBodyYaw;
                float lateralVelocityIntent = math.clamp(
                    localLateralVelocity / math.max(0.5f, _activeProfile.UnderwaterStrokeStartSpeed * 1.5f),
                    -1f,
                    1f);
                float inputStrafe = math.clamp(inputState.MoveDelta.x, -1f, 1f);
                float yawDisagreement = DeltaAngleDegreesNoMathf(playerMovement.BodyYaw, playerMovement.CameraYaw);
                float turnIntent = math.clamp(yawDisagreement / 45f, -1f, 1f);
                float swimSpeedWeight = math.saturate(planarSpeed / math.max(0.25f, _activeProfile.UnderwaterStrokeStartSpeed));

                targetCorrection =
                    inputStrafe * strafeCorrectionInfluence +
                    lateralVelocityIntent * lateralVelocityCorrectionInfluence +
                    turnIntent * turnCorrectionInfluence * swimSpeedWeight;

                if (_currentMode == PlayerSwimPresentationMode.SurfaceTread ||
                    _currentMode == PlayerSwimPresentationMode.SurfaceStroke)
                {
                    targetCorrection *= 1f - surfaceCorrectionSuppression;
                }
            }

            _directionalCorrectionCurrent = SmoothDampValue(
                _directionalCorrectionCurrent,
                math.clamp(targetCorrection, -1f, 1f),
                ref _directionalCorrectionVelocity,
                steeringResponseSmoothTime,
                dt);
        }

        private void ApplyObstacleReactiveBias(
            Transform guide,
            Vector3 basePosition,
            ref Vector3 localPosition,
            ref Vector3 localEuler,
            bool isLeft,
            float guideWeight,
            float dt)
        {
            if (guideWeight <= 0.0001f || viewModelRoot == null)
                return;

            Transform castSpace = guide != null && guide.parent != null
                ? guide.parent
                : viewModelRoot;
            if (castSpace == null)
                return;

            float sideSign = isLeft ? -1f : 1f;
            Vector3 outward = viewModelRoot.right * sideSign;
            Vector3 baseWorld = castSpace.TransformPoint(basePosition);
            Vector3 desiredWorld = castSpace.TransformPoint(localPosition);
            Vector3 toDesired = desiredWorld - baseWorld;
            float desiredDistanceSqr = toDesired.sqrMagnitude;
            if (desiredDistanceSqr <= 0.00000001f)
                return;

            float inverseDesiredDistance = math.rsqrt(desiredDistanceSqr);
            float desiredDistance = desiredDistanceSqr * inverseDesiredDistance;
            Vector3 forwardDirection = toDesired * inverseDesiredDistance;
            float accumulatedWeight = 0f;
            float accumulatedSideBias = 0f;
            float accumulatedVerticalBias = 0f;
            float strongestWeight = 0f;
            float obstacleResponseScale = ResolveObstacleResponseScale(isLeft);
            TryResolveObstacleProbe(
                baseWorld,
                forwardDirection,
                desiredDistance + handObstacleForwardLookahead,
                out float forwardWeight,
                out float forwardSideBias,
                out float forwardVerticalBias);
            AccumulateObstacleProbe(
                forwardWeight,
                forwardSideBias,
                forwardVerticalBias,
                1f,
                ref accumulatedWeight,
                ref accumulatedSideBias,
                ref accumulatedVerticalBias,
                ref strongestWeight);

            Vector3 sideOrigin = desiredWorld - outward * handObstacleSideLookahead;
            TryResolveObstacleProbe(
                sideOrigin,
                outward,
                handObstacleSideLookahead * 2f,
                out float sideWeight,
                out float sideBias,
                out float sideVerticalBias);
            AccumulateObstacleProbe(
                sideWeight,
                sideBias,
                sideVerticalBias,
                0.82f,
                ref accumulatedWeight,
                ref accumulatedSideBias,
                ref accumulatedVerticalBias,
                ref strongestWeight);

            if (handObstacleVerticalLookahead > 0.0001f)
            {
                Vector3 lowerProbeOrigin = desiredWorld + viewModelRoot.up * handObstacleVerticalLookahead;
                TryResolveObstacleProbe(
                    lowerProbeOrigin,
                    -viewModelRoot.up,
                    handObstacleVerticalLookahead * 2f,
                    out float lowerVerticalWeight,
                    out float lowerVerticalSideBias,
                    out float lowerVerticalVerticalBias);
                AccumulateObstacleProbe(
                    lowerVerticalWeight,
                    lowerVerticalSideBias,
                    lowerVerticalVerticalBias,
                    0.7f,
                    ref accumulatedWeight,
                    ref accumulatedSideBias,
                    ref accumulatedVerticalBias,
                    ref strongestWeight);

                Vector3 upperProbeOrigin = desiredWorld - viewModelRoot.up * handObstacleVerticalLookahead;
                TryResolveObstacleProbe(
                    upperProbeOrigin,
                    viewModelRoot.up,
                    handObstacleVerticalLookahead * 2f,
                    out float upperVerticalWeight,
                    out float upperVerticalSideBias,
                    out float upperVerticalVerticalBias);
                AccumulateObstacleProbe(
                    upperVerticalWeight,
                    upperVerticalSideBias,
                    upperVerticalVerticalBias,
                    0.7f,
                    ref accumulatedWeight,
                    ref accumulatedSideBias,
                    ref accumulatedVerticalBias,
                    ref strongestWeight);
            }

            float targetWeight = math.saturate(math.lerp(strongestWeight, accumulatedWeight, handObstacleMultiProbeBlend) * obstacleResponseScale);
            float targetSideBias = accumulatedWeight > 0.0001f
                ? math.clamp(accumulatedSideBias / accumulatedWeight, -1f, 1f)
                : 0f;
            float targetVerticalBias = accumulatedWeight > 0.0001f
                ? math.clamp(accumulatedVerticalBias / accumulatedWeight, -1f, 1f)
                : 0f;

            float obstacleWeightCurrent;
            float obstacleSideBiasCurrent;
            float obstacleVerticalBiasCurrent;
            if (isLeft)
            {
                _leftObstacleWeightCurrent = SmoothDampValue(
                    _leftObstacleWeightCurrent,
                    targetWeight,
                    ref _leftObstacleWeightVelocity,
                    handObstacleSmoothTime,
                    dt);
                _leftObstacleSideBiasCurrent = SmoothDampValue(
                    _leftObstacleSideBiasCurrent,
                    targetSideBias,
                    ref _leftObstacleSideBiasVelocity,
                    handObstacleSmoothTime,
                    dt);
                _leftObstacleVerticalBiasCurrent = SmoothDampValue(
                    _leftObstacleVerticalBiasCurrent,
                    targetVerticalBias,
                    ref _leftObstacleVerticalBiasVelocity,
                    handObstacleSmoothTime,
                    dt);
                obstacleWeightCurrent = _leftObstacleWeightCurrent;
                obstacleSideBiasCurrent = _leftObstacleSideBiasCurrent;
                obstacleVerticalBiasCurrent = _leftObstacleVerticalBiasCurrent;
            }
            else
            {
                _rightObstacleWeightCurrent = SmoothDampValue(
                    _rightObstacleWeightCurrent,
                    targetWeight,
                    ref _rightObstacleWeightVelocity,
                    handObstacleSmoothTime,
                    dt);
                _rightObstacleSideBiasCurrent = SmoothDampValue(
                    _rightObstacleSideBiasCurrent,
                    targetSideBias,
                    ref _rightObstacleSideBiasVelocity,
                    handObstacleSmoothTime,
                    dt);
                _rightObstacleVerticalBiasCurrent = SmoothDampValue(
                    _rightObstacleVerticalBiasCurrent,
                    targetVerticalBias,
                    ref _rightObstacleVerticalBiasVelocity,
                    handObstacleSmoothTime,
                    dt);
                obstacleWeightCurrent = _rightObstacleWeightCurrent;
                obstacleSideBiasCurrent = _rightObstacleSideBiasCurrent;
                obstacleVerticalBiasCurrent = _rightObstacleVerticalBiasCurrent;
            }

            localPosition.z -= handObstacleRetractDistance * obstacleWeightCurrent * guideWeight;
            localPosition.z -= handObstacleBodyRetractDistance * obstacleWeightCurrent * obstacleWeightCurrent * guideWeight;
            localPosition.x -= sideSign * handObstacleInwardDistance * obstacleWeightCurrent * guideWeight;
            localPosition.x += obstacleSideBiasCurrent * handObstacleInwardDistance * 0.75f * guideWeight;
            if (obstacleVerticalBiasCurrent >= 0f)
            {
                localPosition.y += handObstacleUpwardDistance * obstacleVerticalBiasCurrent * obstacleWeightCurrent * guideWeight;
            }
            else
            {
                localPosition.y -= handObstacleDownwardDistance * -obstacleVerticalBiasCurrent * obstacleWeightCurrent * guideWeight;
            }
            localEuler.y -= sideSign * handObstacleYawBias * obstacleWeightCurrent * guideWeight;
            localEuler.z -= sideSign * handObstacleRollBias * obstacleWeightCurrent * guideWeight;
        }

        private void TryResolveObstacleProbe(
            Vector3 origin,
            Vector3 direction,
            float distance,
            out float weight,
            out float sideBias,
            out float verticalBias)
        {
            weight = 0f;
            sideBias = 0f;
            verticalBias = 0f;
            if (distance <= HandObstaclePlaneEpsilon || playerMovement == null)
                return;

            if (!playerMovement.TryGetRecentPresentationWallContact(
                    HandObstacleWallContactMaxPhysicsFrameAge,
                    out Vector3 wallNormal,
                    out Vector3 wallPoint,
                    out float velocityReduction01))
            {
                return;
            }

            float directionSqr = direction.sqrMagnitude;
            if (directionSqr <= HandObstaclePlaneEpsilon * HandObstaclePlaneEpsilon)
                return;

            Vector3 safeDirection = direction * math.rsqrt(directionSqr);
            float approach = -Vector3.Dot(safeDirection, wallNormal);
            if (approach <= HandObstaclePlaneEpsilon)
                return;

            float signedDistance = Vector3.Dot(origin - wallPoint, wallNormal);
            float probeRadius = math.max(0.001f, handObstacleSphereRadius);
            float travel = math.max(0f, signedDistance - probeRadius) / approach;
            if (travel > distance + probeRadius)
                return;

            float fraction = math.saturate(travel / math.max(HandObstaclePlaneEpsilon, distance));
            float responseWeight = math.saturate(1f - fraction);
            responseWeight = math.max(responseWeight, math.saturate(velocityReduction01));
            if (responseWeight <= 0.0001f)
                return;

            weight = responseWeight;
            if (viewModelRoot != null)
            {
                sideBias = math.clamp(Vector3.Dot(viewModelRoot.right, wallNormal), -1f, 1f);
                verticalBias = math.clamp(Vector3.Dot(viewModelRoot.up, wallNormal), -1f, 1f);
                if (verticalBias >= -0.15f)
                    verticalBias = math.max(verticalBias, handObstacleWallLiftBias);
            }
        }

        private float ResolveObstacleResponseScale(bool isLeft)
        {
            float toolFramingWeight = _equippedToolBlendCurrent;
            if (toolFramingWeight <= 0.0001f)
                return 1f;

            bool toolHand =
                (isLeft && equippedToolHand == PlayerToolSwimHandedness.Left) ||
                (!isLeft && equippedToolHand == PlayerToolSwimHandedness.Right);
            if (toolHand)
            {
                float suppressionWeight = math.saturate(toolFramingWeight + _activeToolUseBlendCurrent * 0.7f);
                return math.lerp(1f, equippedToolToolHandObstacleResponseScale, suppressionWeight);
            }

            float supportWeight = math.saturate(toolFramingWeight + _activeToolUseBlendCurrent);
            return 1f + equippedToolSupportHandObstacleBoost * supportWeight;
        }

        private static void AccumulateObstacleProbe(
            float probeWeight,
            float probeSideBias,
            float probeVerticalBias,
            float influence,
            ref float accumulatedWeight,
            ref float accumulatedSideBias,
            ref float accumulatedVerticalBias,
            ref float strongestWeight)
        {
            if (probeWeight <= 0.0001f || influence <= 0.0001f)
                return;

            float weightedProbe = probeWeight * influence;
            accumulatedWeight += weightedProbe;
            accumulatedSideBias += probeSideBias * weightedProbe;
            accumulatedVerticalBias += probeVerticalBias * weightedProbe;
            if (weightedProbe > strongestWeight)
                strongestWeight = weightedProbe;
        }

        private void UpdateToolPoseBiases(
            PlayerToolSwimContract toolSwimContract,
            bool toolUsing,
            PlayerToolSwimHandedness toolHand,
            float dt)
        {
            Vector3 rootPositionTarget = Vector3.zero;
            Vector3 rootEulerTarget = Vector3.zero;
            Vector3 supportPositionTarget = Vector3.zero;
            Vector3 supportEulerTarget = Vector3.zero;
            Vector3 toolPositionTarget = Vector3.zero;
            Vector3 toolEulerTarget = Vector3.zero;

            if (toolSwimContract != null)
            {
                rootPositionTarget = toolSwimContract.SwimRootLocalPositionOffset;
                rootEulerTarget = toolSwimContract.SwimRootLocalEulerOffset;
                supportPositionTarget = toolSwimContract.SwimSupportHandLocalPositionOffset;
                supportEulerTarget = toolSwimContract.SwimSupportHandLocalEulerOffset;
                toolPositionTarget = toolSwimContract.SwimToolHandLocalPositionOffset;
                toolEulerTarget = toolSwimContract.SwimToolHandLocalEulerOffset;

                if (toolUsing)
                {
                    rootPositionTarget += toolSwimContract.ActiveUseRootLocalPositionOffset;
                    rootEulerTarget += toolSwimContract.ActiveUseRootLocalEulerOffset;
                    supportPositionTarget += toolSwimContract.ActiveUseSupportHandLocalPositionOffset;
                    supportEulerTarget += toolSwimContract.ActiveUseSupportHandLocalEulerOffset;
                }
            }

            _rootToolPositionBiasCurrent = SmoothDampVector(
                _rootToolPositionBiasCurrent,
                rootPositionTarget,
                ref _rootToolPositionBiasVelocity,
                toolTransitionSmoothTime,
                dt);
            _rootToolEulerBiasCurrent = SmoothDampVector(
                _rootToolEulerBiasCurrent,
                rootEulerTarget,
                ref _rootToolEulerBiasVelocity,
                toolTransitionSmoothTime,
                dt);

            if (toolHand == PlayerToolSwimHandedness.Left)
            {
                _leftGuideToolPositionBiasCurrent = SmoothDampVector(
                    _leftGuideToolPositionBiasCurrent,
                    toolPositionTarget,
                    ref _leftGuideToolPositionBiasVelocity,
                    toolTransitionSmoothTime,
                    dt);
                _leftGuideToolEulerBiasCurrent = SmoothDampVector(
                    _leftGuideToolEulerBiasCurrent,
                    toolEulerTarget,
                    ref _leftGuideToolEulerBiasVelocity,
                    toolTransitionSmoothTime,
                    dt);
                _rightGuideToolPositionBiasCurrent = SmoothDampVector(
                    _rightGuideToolPositionBiasCurrent,
                    supportPositionTarget,
                    ref _rightGuideToolPositionBiasVelocity,
                    toolTransitionSmoothTime,
                    dt);
                _rightGuideToolEulerBiasCurrent = SmoothDampVector(
                    _rightGuideToolEulerBiasCurrent,
                    supportEulerTarget,
                    ref _rightGuideToolEulerBiasVelocity,
                    toolTransitionSmoothTime,
                    dt);
            }
            else
            {
                _leftGuideToolPositionBiasCurrent = SmoothDampVector(
                    _leftGuideToolPositionBiasCurrent,
                    supportPositionTarget,
                    ref _leftGuideToolPositionBiasVelocity,
                    toolTransitionSmoothTime,
                    dt);
                _leftGuideToolEulerBiasCurrent = SmoothDampVector(
                    _leftGuideToolEulerBiasCurrent,
                    supportEulerTarget,
                    ref _leftGuideToolEulerBiasVelocity,
                    toolTransitionSmoothTime,
                    dt);
                _rightGuideToolPositionBiasCurrent = SmoothDampVector(
                    _rightGuideToolPositionBiasCurrent,
                    toolPositionTarget,
                    ref _rightGuideToolPositionBiasVelocity,
                    toolTransitionSmoothTime,
                    dt);
                _rightGuideToolEulerBiasCurrent = SmoothDampVector(
                    _rightGuideToolEulerBiasCurrent,
                    toolEulerTarget,
                    ref _rightGuideToolEulerBiasVelocity,
                    toolTransitionSmoothTime,
                    dt);
            }
        }

        private float ResolveSurfaceFramingWeight()
        {
            switch (_currentMode)
            {
                case PlayerSwimPresentationMode.ShallowWade:
                    return 0.2f;

                case PlayerSwimPresentationMode.SurfaceTread:
                    return 1f;

                case PlayerSwimPresentationMode.SurfaceStroke:
                    return 0.72f;

                default:
                    return 0f;
            }
        }

        private float ResolveSurfaceIdleWeight()
        {
            switch (_currentMode)
            {
                case PlayerSwimPresentationMode.SurfaceTread:
                    return 1f;

                case PlayerSwimPresentationMode.SurfaceStroke:
                    return 0.28f;

                default:
                    return 0f;
            }
        }

        private float ResolvePoseLagMultiplier(SwimPresentationProfile profile)
        {
            float multiplier = 1f;

            switch (_currentMode)
            {
                case PlayerSwimPresentationMode.SurfaceTread:
                case PlayerSwimPresentationMode.SurfaceStroke:
                    multiplier *= surfacePoseLagMultiplier;
                    break;

                case PlayerSwimPresentationMode.UnderwaterSprint:
                    multiplier *= sprintPoseLagMultiplier;
                    break;
            }

            if (profile != null)
            {
                switch (profile.AuthoredStrokeStyle)
                {
                    case SwimPresentationProfile.StrokeStyle.HeavyIndustrial:
                        multiplier *= heavyPoseLagMultiplier;
                        break;

                    case SwimPresentationProfile.StrokeStyle.PoweredAssist:
                        multiplier *= poweredAssistPoseLagMultiplier;
                        break;
                }
            }

            if (_equippedToolBlendCurrent > 0.0001f)
                multiplier *= math.lerp(1f, equippedToolPoseLagMultiplier, _equippedToolBlendCurrent);

            if (_heavyCarryLoadCurrent > 0.0001f)
                multiplier *= math.lerp(1f, heavyCarryPoseLagMultiplier, _heavyCarryLoadCurrent);

            if (_transportBoostCurrent > 0.0001f)
                multiplier *= math.lerp(1f, ResolveTransportPoseLagMultiplier(), _transportBoostCurrent);

            return math.max(0.0001f, multiplier);
        }

        private void UpdateLocomotionFeelState(float dt)
        {
            float targetHeavyCarryLoad = playerMovement != null && playerMovement.IsDraggingHeavyCargo
                ? playerMovement.HeavyCarryLoad
                : 0f;

            _transportFeelContractCurrent = ResolveTransportFeelContract();
            float targetTransportBoost = ResolveTransportBoost01();

            _heavyCarryLoadCurrent = SmoothDampValue(
                _heavyCarryLoadCurrent,
                targetHeavyCarryLoad,
                ref _heavyCarryLoadVelocity,
                toolTransitionSmoothTime,
                dt);
            _transportBoostCurrent = SmoothDampValue(
                _transportBoostCurrent,
                targetTransportBoost,
                ref _transportBoostVelocity,
                toolTransitionSmoothTime,
                dt);
        }

        private float ResolveTransportBoost01()
        {
            bool coordinatorOwnsTransport = playerTransportCoordinator != null && playerTransportCoordinator.HasActiveTransportSource();
            if (coordinatorOwnsTransport)
                return playerTransportCoordinator.ResolveTransportBoost01();

            if (playerToolManager == null || playerToolManager.IsSwapping)
                return 0f;

            IPlayerTransportSource transportSource = playerToolManager.CurrentToolTransportSource;
            if (transportSource == null)
                return 0f;

            float transportBoost = math.saturate(transportSource.GetTransportBoost01());
            if (transportBoost > 0f)
                return transportBoost;

            float reference = math.max(ResolveTransportPropulsionReference(), 0.01f);
            return math.saturate(transportSource.GetTransportPropulsionForce() / reference);
        }

        private PlayerTransportFeelContract ResolveTransportFeelContract()
        {
            bool coordinatorOwnsTransport = playerTransportCoordinator != null && playerTransportCoordinator.HasActiveTransportSource();
            if (coordinatorOwnsTransport)
                return playerTransportCoordinator.ResolveTransportFeelContract();

            if (playerToolManager == null || playerToolManager.IsSwapping)
                return null;

            return playerToolManager.CurrentToolTransportFeelContract;
        }

        private float ResolveTransportPropulsionReference()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.PropulsionForceReference
                : mantaTransportForceReference;
        }

        private float ResolveTransportPropulsionFloor()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.SwimPropulsionFloor
                : mantaTransportPropulsionFloor;
        }

        private float ResolveTransportCadenceMultiplier()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.SwimCadenceMultiplier
                : mantaTransportCadenceMultiplier;
        }

        private float ResolveTransportPoseLagMultiplier()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.SwimPoseLagMultiplier
                : mantaTransportPoseLagMultiplier;
        }

        private float ResolveTransportRootPresentationWeight()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.SwimRootPresentationWeight
                : mantaTransportRootPresentationWeight;
        }

        private float ResolveTransportSupportHandWeight()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.SwimSupportHandWeight
                : mantaTransportSupportHandWeight;
        }

        private float ResolveTransportRootForwardBias()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.SwimRootForwardBias
                : mantaTransportRootForwardBias;
        }

        private float ResolveTransportGuideForwardBias()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.SwimGuideForwardBias
                : mantaTransportGuideForwardBias;
        }

        private float ResolveTransportSwimPresentationScale()
        {
            return _transportFeelContractCurrent != null
                ? _transportFeelContractCurrent.SwimPresentationScale
                : 1f;
        }

        private float ResolveGuidePoseLagMultiplier(
            SwimPresentationProfile profile,
            bool toolHand,
            bool supportHand,
            float obstacleWeight)
        {
            float multiplier = ResolvePoseLagMultiplier(profile);
            float turnPressure = math.saturate(math.abs(_cameraTurnSwayCurrent));
            if (turnPressure > 0.0001f)
                multiplier *= math.lerp(1f, cameraTurnPoseLagMultiplier, turnPressure);

            if (toolHand)
            {
                multiplier *= math.lerp(1f, equippedToolToolHandPoseLagMultiplier, _equippedToolBlendCurrent);
            }
            else if (supportHand)
            {
                multiplier *= math.lerp(1f, equippedToolSupportHandPoseLagMultiplier, _equippedToolBlendCurrent);
            }

            if (obstacleWeight > 0.0001f)
                multiplier *= math.lerp(1f, obstaclePoseLagMultiplier, math.saturate(obstacleWeight));

            return math.max(0.0001f, multiplier);
        }

        private void InitializePoseStateFromCurrentTargets()
        {
            if (_poseStateInitialized)
                return;

            if (viewModelRoot != null)
            {
                _currentLocalPosition = viewModelRoot.localPosition;
                _currentLocalRotation = viewModelRoot.localRotation;
            }

            if (leftHandGuide != null)
            {
                _leftGuideCurrentLocalPosition = leftHandGuide.localPosition;
                _leftGuideCurrentLocalRotation = leftHandGuide.localRotation;
            }

            if (rightHandGuide != null)
            {
                _rightGuideCurrentLocalPosition = rightHandGuide.localPosition;
                _rightGuideCurrentLocalRotation = rightHandGuide.localRotation;
            }

            _rootPoseEulerCurrent = _currentLocalRotation.eulerAngles;
            _leftGuideEulerCurrent = _leftGuideCurrentLocalRotation.eulerAngles;
            _rightGuideEulerCurrent = _rightGuideCurrentLocalRotation.eulerAngles;
            _poseStateInitialized = true;
        }

        private static bool TryResolveKccPresentationVelocity(out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (!CoreDeterminismSignals.TryGetLatestKccVelocity(out KccVelocitySignal signal))
                return false;

            uint currentFrame = SystemDispatcher.CurrentFrameId;
            uint signalFrame = signal.Frame != 0u ? signal.Frame : signal.Sequence;
            if (currentFrame != 0u &&
                signalFrame != 0u &&
                (signalFrame > currentFrame || currentFrame - signalFrame > 2u))
            {
                return false;
            }

            float3 value = signal.Velocity;
            if (!math.all(math.isfinite(value)))
                return false;

            velocity = new Vector3(value.x, value.y, value.z);
            return true;
        }

        private Vector3 ResolveMovementPresentationVelocity()
        {
            if (playerMovement == null)
                return Vector3.zero;

            Vector3 velocity = playerMovement.CurrentWorldVelocity;
            return IsFinite(velocity) ? velocity : Vector3.zero;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static float ShapeStrokeHalfWave(float value, float sharpness)
        {
            float clamped = math.saturate(value);
            if (clamped <= 0f)
                return 0f;

            float squared = clamped * clamped;
            float cubed = squared * clamped;
            if (sharpness <= 1f)
            {
                float easeOut = clamped * (2f - clamped);
                return math.lerp(easeOut, clamped, math.saturate(sharpness));
            }

            if (sharpness <= 2f)
                return math.lerp(clamped, squared, math.saturate(sharpness - 1f));

            return math.lerp(squared, cubed, math.saturate(sharpness - 2f));
        }

        private static float ApproximateVectorMagnitude(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float max = math.max(ax, math.max(ay, az));
            float min = math.min(ax, math.min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + (0.375f * mid) + (0.125f * min);
        }

        private static float ApproximatePlanarMagnitude(float x, float z)
        {
            float ax = math.abs(x);
            float az = math.abs(z);
            float max = math.max(ax, az);
            float min = math.min(ax, az);
            return max + (0.375f * min);
        }

        private static Vector3 ClampVectorBySqr(Vector3 value, float maxMagnitude)
        {
            float maxMagnitudeSqr = maxMagnitude * maxMagnitude;
            float valueSqr = value.sqrMagnitude;
            if (valueSqr <= maxMagnitudeSqr || valueSqr <= 0.00000001f)
                return value;

            return value * (maxMagnitude * math.rsqrt(valueSqr));
        }

        private static float ResolveDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            return math.saturate(x / (1f + x));
        }

        private static float DeltaAngleDegreesNoMathf(float current, float target)
        {
            float delta = target - current;
            float wrapped = delta + 180f;
            wrapped -= 360f * math.floor(wrapped * 0.0027777778f);
            return wrapped - 180f;
        }

        private static float ApproximateSinCycle01(float cycle01)
        {
            ApproximateSinCosFullNoTrig(cycle01 * TwoPi, out float sin, out _);
            return sin;
        }

        private static float ApproximateCosCycle01(float cycle01)
        {
            ApproximateSinCosFullNoTrig(cycle01 * TwoPi, out _, out float cos);
            return cos;
        }

        private static Quaternion ResolveEulerRotationNoTrig(Vector3 eulerDegrees)
        {
            ApproximateSinCosFullNoTrig(eulerDegrees.x * DegreesToRadians * 0.5f, out float sx, out float cx);
            ApproximateSinCosFullNoTrig(eulerDegrees.y * DegreesToRadians * 0.5f, out float sy, out float cy);
            ApproximateSinCosFullNoTrig(eulerDegrees.z * DegreesToRadians * 0.5f, out float sz, out float cz);

            float4 pitch = new float4(sx, 0f, 0f, cx);
            float4 yaw = new float4(0f, sy, 0f, cy);
            float4 roll = new float4(0f, 0f, sz, cz);
            return ToQuaternion(NormalizeQuaternionNoSqrt(MulQuaternionNoSqrt(MulQuaternionNoSqrt(yaw, pitch), roll)));
        }

        private static void ApproximateSinCosFullNoTrig(float radians, out float sin, out float cos)
        {
            float x = radians - (TwoPi * FastNearestInt(radians * InvTwoPi));
            float cosSign = 1f;
            if (x > HalfPi)
            {
                x = Pi - x;
                cosSign = -1f;
            }
            else if (x < -HalfPi)
            {
                x = -Pi - x;
                cosSign = -1f;
            }

            float x2 = x * x;
            sin = x * (1f - (x2 * (0.16666667f - (x2 * 0.008333333f))));
            cos = cosSign * (1f - (x2 * (0.5f - (x2 * 0.041666667f))));
        }

        private static int FastNearestInt(float value)
        {
            return (int)(value + math.select(-0.5f, 0.5f, value >= 0f));
        }

        private static float4 MulQuaternionNoSqrt(float4 lhs, float4 rhs)
        {
            return new float4(
                lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y,
                lhs.w * rhs.y - lhs.x * rhs.z + lhs.y * rhs.w + lhs.z * rhs.x,
                lhs.w * rhs.z + lhs.x * rhs.y - lhs.y * rhs.x + lhs.z * rhs.w,
                lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z);
        }

        private static float4 NormalizeQuaternionNoSqrt(float4 value)
        {
            float lengthSq = math.max(math.dot(value, value), 0.000001f);
            return value * math.rsqrt(lengthSq);
        }

        private static Quaternion ToQuaternion(float4 value)
        {
            return new Quaternion(value.x, value.y, value.z, value.w);
        }

        private static Vector3 SmoothDampAngles(
            Vector3 current,
            Vector3 target,
            ref Vector3 velocity,
            float smoothTime,
            float dt)
        {
            float t = ResolveDecayBlend(1f / math.max(0.0001f, smoothTime), dt);
            Vector3 next = new Vector3(
                current.x + DeltaAngleDegreesNoMathf(current.x, target.x) * t,
                current.y + DeltaAngleDegreesNoMathf(current.y, target.y) * t,
                current.z + DeltaAngleDegreesNoMathf(current.z, target.z) * t);
            velocity = dt > 0.0001f ? (next - current) / dt : Vector3.zero;
            return next;
        }

        private static float SmoothDampValue(
            float current,
            float target,
            ref float velocity,
            float smoothTime,
            float dt)
        {
            float t = ResolveDecayBlend(1f / math.max(0.0001f, smoothTime), dt);
            float next = math.lerp(current, target, t);
            velocity = dt > 0.0001f ? (next - current) / dt : 0f;
            return next;
        }

        private static Vector3 SmoothDampVector(
            Vector3 current,
            Vector3 target,
            ref Vector3 velocity,
            float smoothTime,
            float dt)
        {
            float t = ResolveDecayBlend(1f / math.max(0.0001f, smoothTime), dt);
            Vector3 next = new Vector3(
                math.lerp(current.x, target.x, t),
                math.lerp(current.y, target.y, t),
                math.lerp(current.z, target.z, t));
            velocity = dt > 0.0001f ? (next - current) / dt : Vector3.zero;
            return next;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics(SwimPresentationProfile profile, float speed)
        {
            int modeIndex = (int)_currentMode;
            _debugMode = (uint)modeIndex < (uint)s_modeLabels.Length
                ? s_modeLabels[modeIndex]
                : "Unknown";
            _debugStrokePhase = _strokePhase;
            _debugPropulsionPulse = _propulsionPulse;
            _debugSpeed = speed;
            _debugSteeringCorrection = _directionalCorrectionCurrent;
            _debugCameraTurnSway = _cameraTurnSwayCurrent;
            _debugStrokePhaseLead = _debugStrokePhaseLeadCurrent;
            _debugLeftObstacleWeight = _leftObstacleWeightCurrent;
            _debugRightObstacleWeight = _rightObstacleWeightCurrent;
            _debugStrokePowerImpulse = _strokePowerImpulseCurrent;
            _debugObstacleRootPressure = math.max(_leftObstacleWeightCurrent, _rightObstacleWeightCurrent);
            _debugPropulsionObstruction = _propulsionObstructionCurrent;
            _debugProfile = profile != null ? profile.name : "None";
            _debugProfileSource = profileLibrary != null ? "ProfileLibrary" : "PrefabFallback";
        }
    }
}
