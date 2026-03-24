// ============================================================================
// HECTON-8 — SuitData.cs  v7.0
// Data-driven suit configuration — full immersion parameter set.
//
// v7.0 ADDITIONS:
//   • Swim bob parameters (stroke rhythm)
//   • Underwater pitch inertia omega
//   • Collision camera shake parameters
//   • Splash detection thresholds
//   • Depth sway / roll / slowdown multipliers
//   • FOV depth compression parameters
//   • Exhale rhythm parameters
// ============================================================================

using UnityEngine;

namespace Hecton8.Gameplay
{
    [CreateAssetMenu(fileName = "NewSuitData", menuName = "Hecton8/Suit Data", order = 100)]
    public sealed class SuitData : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════
        //  PHYSICS — CORE
        // ══════════════════════════════════════════════════════════

        [Header("── Physics: Core ─────────────────────────────")]
        [Range(30f, 1000f)]
        [Tooltip("Rigidbody mass (kg). Light: 80. Heavy: 300-500.")]
        public float mass = 80f;

        // ══════════════════════════════════════════════════════════
        //  PHYSICS — SWIMMING
        // ══════════════════════════════════════════════════════════

        [Header("── Physics: Swimming ─────────────────────────")]
        [Tooltip("Swim thrust force (Newtons).")]
        public float swimForce = 800f;

        [Tooltip("Vertical thrust (Space/Ctrl/Q/E).")]
        public float swimVerticalForce = 400f;

        [Tooltip("Maximum 3D swim speed (m/s).")]
        public float maxSwimSpeed = 9f;

        [Tooltip("LEGACY — kept for serialization. Not used in v6.1+.")]
        [HideInInspector] public float swimDrag = 2f;

        [Tooltip("LEGACY — replaced by swimDragCoefficient.")]
        [HideInInspector] public float swimBrakeForce = 8f;

        [Tooltip("Quadratic drag coefficient. F = C * v². " +
                 "Higher = heavier water. Light: 1.5-2.5. Heavy: 3-5.")]
        [Range(0.5f, 10f)]
        public float swimDragCoefficient = 2.5f;

        // ══════════════════════════════════════════════════════════
        //  PHYSICS — WALKING
        // ══════════════════════════════════════════════════════════

        [Header("── Physics: Walking ──────────────────────────")]
        public float walkForce = 1200f;
        public float maxWalkSpeed = 7f;
        public float walkDrag = 5f;
        public float jumpImpulse = 5f;

        [Tooltip("How much shallow water slows walking. 0.6 = 60% slower at full wading.")]
        [Range(0f, 0.9f)]
        public float wadeSlowdownFactor = 0.6f;

        // ══════════════════════════════════════════════════════════
        //  BODY YAW SPRING
        // ══════════════════════════════════════════════════════════

        [Header("── Body Yaw Spring (Swimming) ────────────────")]
        [Tooltip("How fast body yaw chases camera yaw underwater. " +
                 "Higher = faster follow. 8-12 = responsive. 4-6 = sluggish.")]
        [Range(2f, 20f)]
        public float bodyYawSpringOmega = 10f;

        // ══════════════════════════════════════════════════════════
        //  SURFACE SWIMMING
        // ══════════════════════════════════════════════════════════

        [Header("── Surface Swimming (Subnautica-style) ───────")]
        [Tooltip("Spring strength keeping player at water surface. " +
                 "0 = no lock (free bobbing). 8-15 = gentle. 20+ = strong.")]
        [Range(0f, 50f)]
        public float surfaceLockStrength = 12f;

        [Tooltip("Damping to prevent oscillation at surface.")]
        [Range(0f, 20f)]
        public float surfaceLockDamping = 6f;

        [Tooltip("Range (meters) from surface where lock activates.")]
        [Range(0.1f, 3f)]
        public float surfaceLockRange = 1.5f;

        // ══════════════════════════════════════════════════════════
        //  HEAD BOB
        // ══════════════════════════════════════════════════════════

        [Header("── Head Bob (Walking) ────────────────────────")]
        [Range(0.5f, 4f)] public float bobFrequency = 2f;
        [Range(0f, 0.2f)] public float bobVerticalAmplitude = 0.04f;
        [Range(0f, 0.15f)] public float bobHorizontalAmplitude = 0.025f;
        [Range(1f, 20f)] public float bobTransitionSpeed = 8f;

        // ══════════════════════════════════════════════════════════
        //  SWIM BOB — stroke rhythm while swimming
        // ══════════════════════════════════════════════════════════

        [Header("── Swim Bob (Stroke Rhythm) ──────────────────")]
        [Tooltip("Enable rhythmic stroke bobbing while swimming.")]
        public bool enableSwimBob = true;

        [Tooltip("Stroke frequency (Hz). 0.8-1.2 = natural stroke pace.")]
        [Range(0.3f, 3f)]
        public float swimBobFrequency = 0.9f;

        [Tooltip("Vertical amplitude per stroke (meters). 0.03-0.06 = subtle.")]
        [Range(0f, 0.15f)]
        public float swimBobVerticalAmplitude = 0.04f;

        [Tooltip("Forward/backward amplitude per stroke (meters).")]
        [Range(0f, 0.08f)]
        public float swimBobForwardAmplitude = 0.015f;

        [Tooltip("Slight roll per stroke cycle (degrees). Simulates alternating arm pull.")]
        [Range(0f, 3f)]
        public float swimBobRollAmplitude = 0.8f;

        [Tooltip("How fast swim bob intensity ramps up/down.")]
        [Range(1f, 15f)]
        public float swimBobTransitionSpeed = 4f;

        // ══════════════════════════════════════════════════════════
        //  CAMERA ROLL
        // ══════════════════════════════════════════════════════════

        [Header("── Camera Roll (Swimming) ────────────────────")]
        [Tooltip("Max roll from strafe (degrees). Subnautica-like: 2-4.")]
        [Range(0f, 10f)]
        public float strafeRollAngle = 3f;

        [Tooltip("Max roll from mouse turn (degrees). Keep subtle: 1-2.")]
        [Range(0f, 6f)]
        public float mouseRollAngle = 1.5f;

        [Tooltip("Mouse X to roll sensitivity.")]
        [Range(0.01f, 2f)]
        public float mouseRollSensitivity = 0.15f;

        [Tooltip("LEGACY — replaced by rollSpringOmega.")]
        [HideInInspector] public float rollAttackSpeed = 8f;
        [HideInInspector] public float rollDecaySpeed = 4f;

        [Tooltip("Spring omega for roll. 6-8 = smooth. 10+ = snappy.")]
        [Range(2f, 20f)]
        public float rollSpringOmega = 7f;

        // ══════════════════════════════════════════════════════════
        //  UNDERWATER PITCH INERTIA
        // ══════════════════════════════════════════════════════════

        [Header("── Underwater Pitch Inertia ──────────────────")]
        [Tooltip("Enable helmet/head pitch inertia underwater. " +
                 "On land pitch is instant. Underwater it lags through spring.")]
        public bool enableUnderwaterPitchInertia = true;

        [Tooltip("Spring omega for visual pitch chasing actual pitch. " +
                 "15-20 = fast but perceptible. 25+ = nearly instant.")]
        [Range(5f, 40f)]
        public float underwaterPitchSpringOmega = 18f;

        // ══════════════════════════════════════════════════════════
        //  COLLISION CAMERA SHAKE
        // ══════════════════════════════════════════════════════════

        [Header("── Collision Camera Shake ────────────────────")]
        [Tooltip("Enable camera shake on collision impact.")]
        public bool enableCollisionShake = true;

        [Tooltip("Minimum collision relative velocity to trigger shake (m/s).")]
        [Range(0.5f, 10f)]
        public float collisionShakeThreshold = 2f;

        [Tooltip("Collision velocity at which shake reaches maximum.")]
        [Range(3f, 30f)]
        public float collisionShakeMaxVelocity = 12f;

        [Tooltip("Maximum shake displacement (meters). 0.03-0.06 = punchy.")]
        [Range(0f, 0.15f)]
        public float collisionShakeMaxAmplitude = 0.05f;

        [Tooltip("Maximum shake pitch impulse (degrees).")]
        [Range(0f, 5f)]
        public float collisionShakeMaxPitch = 2f;

        [Tooltip("Spring omega for shake recovery. Higher = faster settle.")]
        [Range(4f, 25f)]
        public float collisionShakeRecoveryOmega = 10f;

        // ══════════════════════════════════════════════════════════
        //  SPLASH DETECTION
        // ══════════════════════════════════════════════════════════

        [Header("── Splash Detection ──────────────────────────")]
        [Tooltip("Minimum immersion change rate per second to trigger splash.")]
        [Range(0.1f, 5f)]
        public float splashImmersionRateThreshold = 0.8f;

        [Tooltip("Minimum vertical speed for splash intensity scaling (m/s).")]
        [Range(0.5f, 5f)]
        public float splashMinVerticalSpeed = 1.5f;

        [Tooltip("Camera dip on splash (meters). Simulates impact with surface.")]
        [Range(0f, 0.1f)]
        public float splashCameraDip = 0.03f;

        [Tooltip("Immersion ratio threshold for head submerge detection.")]
        [Range(0.7f, 0.98f)]
        public float submergeThreshold = 0.85f;

        // ══════════════════════════════════════════════════════════
        //  DEPTH EFFECTS
        // ══════════════════════════════════════════════════════════

        [Header("── Depth Effects ─────────────────────────────")]

        [Tooltip("Depth (meters below surface) where sway multiplier starts increasing.")]
        [Range(0f, 100f)]
        public float depthSwayStart = 10f;

        [Tooltip("Depth where sway multiplier reaches maximum.")]
        [Range(10f, 300f)]
        public float depthSwayEnd = 80f;

        [Tooltip("Maximum sway amplitude multiplier at depth. 1.0 = no change. 1.5 = 50% more.")]
        [Range(1f, 3f)]
        public float depthSwayMultiplierMax = 1.6f;

        [Space(5)]
        [Tooltip("Maximum roll amplitude multiplier at depth.")]
        [Range(1f, 2.5f)]
        public float depthRollMultiplierMax = 1.4f;

        [Space(5)]
        [Tooltip("Depth where swim slowdown starts (meters).")]
        [Range(0f, 100f)]
        public float depthSwimSlowdownStart = 20f;

        [Tooltip("Depth where swim slowdown reaches maximum.")]
        [Range(20f, 500f)]
        public float depthSwimSlowdownEnd = 150f;

        [Tooltip("Maximum swim force reduction at depth. 0.3 = 30% slower.")]
        [Range(0f, 0.5f)]
        public float depthSwimSlowdownMax = 0.2f;

        [Tooltip("Maximum additional drag coefficient at depth.")]
        [Range(0f, 3f)]
        public float depthDragIncreaseMax = 0.8f;

        // ══════════════════════════════════════════════════════════
        //  FOV DEPTH COMPRESSION
        // ══════════════════════════════════════════════════════════

        [Header("── FOV Depth Compression ─────────────────────")]
        [Tooltip("Enable FOV narrowing with depth (pressure claustrophobia).")]
        public bool enableDepthFovCompression = true;

        [Tooltip("Depth where FOV compression starts (meters).")]
        [Range(0f, 50f)]
        public float depthFovCompressionStart = 5f;

        [Tooltip("Depth where FOV compression reaches maximum.")]
        [Range(10f, 300f)]
        public float depthFovCompressionEnd = 100f;

        [Tooltip("Maximum FOV reduction at depth (degrees). 5-8 = subtle. 10+ = dramatic.")]
        [Range(0f, 20f)]
        public float depthFovCompressionMax = 6f;

        // ══════════════════════════════════════════════════════════
        //  EXHALE RHYTHM
        // ══════════════════════════════════════════════════════════

        [Header("── Exhale Rhythm (Underwater Breathing) ──────")]
        [Tooltip("Enable periodic exhale micro-dip and event.")]
        public bool enableExhaleRhythm = true;

        [Tooltip("Base interval between exhales (seconds).")]
        [Range(2f, 10f)]
        public float exhaleIntervalBase = 4.5f;

        [Tooltip("Random variation on exhale interval (± seconds).")]
        [Range(0f, 3f)]
        public float exhaleIntervalVariation = 1f;

        [Tooltip("Camera Y dip during exhale (meters). Very subtle.")]
        [Range(0f, 0.04f)]
        public float exhaleDipAmplitude = 0.012f;

        [Tooltip("Camera pitch pulse during exhale (degrees).")]
        [Range(0f, 2f)]
        public float exhalePitchAmplitude = 0.4f;

        [Tooltip("Duration of exhale dip/pulse (seconds).")]
        [Range(0.1f, 1f)]
        public float exhaleDuration = 0.4f;

        // ══════════════════════════════════════════════════════════
        //  LANDING IMPACT
        // ══════════════════════════════════════════════════════════

        [Header("── Landing Impact ────────────────────────────")]
        [Range(0.5f, 20f)] public float impactVelocityThreshold = 5f;
        [Range(0f, 0.5f)] public float impactDipMaxDistance = 0.15f;
        [Range(2f, 30f)] public float impactVelocityMax = 12f;
        [Range(1f, 15f)] public float impactRecoverySpeed = 6f;

        // ══════════════════════════════════════════════════════════
        //  IDLE SWAY
        // ══════════════════════════════════════════════════════════

        [Header("── Idle Sway (Underwater Breathing) ──────────")]
        public bool enableIdleSway = true;
        [Range(0.05f, 1f)] public float idleSwayFrequencyY = 0.25f;
        [Range(0.05f, 1f)] public float idleSwayFrequencyX = 0.17f;
        [Range(0.05f, 0.5f)] public float idleSwayFrequencyRoll = 0.11f;
        [Range(0f, 0.05f)] public float idleSwayAmplitudeY = 0.01f;
        [Range(0f, 0.05f)] public float idleSwayAmplitudeX = 0.007f;
        [Range(0f, 3f)] public float idleSwayAmplitudeRoll = 0.5f;
        [Range(0.5f, 10f)] public float idleSwayTransitionSpeed = 2f;

        // ══════════════════════════════════════════════════════════
        //  MOMENTUM EFFECTS
        // ══════════════════════════════════════════════════════════

        [Header("── Momentum Effects (Swimming) ───────────────")]
        [Range(0f, 3f)] public float accelerationPitchFactor = 0.8f;
        [Range(2f, 15f)] public float momentumPitchSpringOmega = 6f;

        // ══════════════════════════════════════════════════════════
        //  AMBIENT CURRENT
        // ══════════════════════════════════════════════════════════

        [Header("── Ambient Current (Swimming) ────────────────")]
        [Tooltip("Subtle drift force (Newtons). 0.1-0.3 = gentle.")]
        [Range(0f, 1f)]
        public float ambientCurrentStrength = 0.15f;

        // ══════════════════════════════════════════════════════════
        //  MODE TRANSITION
        // ══════════════════════════════════════════════════════════

        [Header("── Mode Transition ──────────────────────────")]
        [Range(1f, 20f)] public float dampingTransitionSpeed = 6f;
    }
}