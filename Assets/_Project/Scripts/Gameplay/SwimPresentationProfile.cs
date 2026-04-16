using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Authoring profile for first-person swim presentation cadence, mass feel, and guide motion.
    /// </summary>
    [CreateAssetMenu(fileName = "SwimPresentationProfile", menuName = "Hecton8/Swim Presentation Profile", order = 125)]
    public sealed class SwimPresentationProfile : ScriptableObject
    {
        /// <summary>
        /// High-level authored stroke family. This is a presentation tag, not gameplay logic.
        /// </summary>
        public enum StrokeStyle : byte
        {
            LightExpedition = 0,
            TechnicalUtility = 1,
            HeavyIndustrial = 2,
            PoweredAssist = 3
        }

        [Header("-- Identity ------------------------------")]
        [Tooltip("High-level visual swim family used for future animator/viewmodel authoring.")]
        [SerializeField] private StrokeStyle strokeStyle = StrokeStyle.LightExpedition;

        [Tooltip("Neutral local position for the swim viewmodel root.")]
        [SerializeField] private Vector3 baseLocalPosition = new Vector3(0f, -0.18f, 0.38f);

        [Tooltip("Neutral local euler rotation for the swim viewmodel root.")]
        [SerializeField] private Vector3 baseLocalEuler = new Vector3(6f, 0f, 0f);

        [Tooltip("Neutral local position for the left hand guide.")]
        [SerializeField] private Vector3 leftGuideBaseLocalPosition = new Vector3(-0.11f, -0.02f, 0.08f);

        [Tooltip("Neutral local euler rotation for the left hand guide.")]
        [SerializeField] private Vector3 leftGuideBaseLocalEuler = new Vector3(8f, -18f, -6f);

        [Tooltip("Neutral local position for the right hand guide.")]
        [SerializeField] private Vector3 rightGuideBaseLocalPosition = new Vector3(0.11f, -0.02f, 0.08f);

        [Tooltip("Neutral local euler rotation for the right hand guide.")]
        [SerializeField] private Vector3 rightGuideBaseLocalEuler = new Vector3(8f, 18f, 6f);

        [Header("-- Stroke Cadence -------------------------")]
        [Tooltip("Surface tread cadence in cycles per second.")]
        [SerializeField, Range(0.1f, 3f)] private float surfaceTreadCadence = 0.6f;

        [Tooltip("Normal surface stroke cadence in cycles per second.")]
        [SerializeField, Range(0.1f, 4f)] private float surfaceStrokeCadence = 1.05f;

        [Tooltip("Normal underwater stroke cadence in cycles per second.")]
        [SerializeField, Range(0.1f, 4f)] private float underwaterStrokeCadence = 0.95f;

        [Tooltip("Cadence multiplier applied during underwater sprint presentation.")]
        [SerializeField, Range(1f, 3f)] private float sprintCadenceMultiplier = 1.35f;

        [Tooltip("How strongly speed modulates cadence.")]
        [SerializeField, Range(0f, 1.5f)] private float speedCadenceInfluence = 0.45f;

        [Header("-- Root Motion ----------------------------")]
        [Tooltip("Forward surge magnitude per stroke.")]
        [SerializeField, Range(0f, 0.2f)] private float strokeSurgeAmplitude = 0.06f;

        [Tooltip("Vertical lift/drop magnitude per stroke.")]
        [SerializeField, Range(0f, 0.2f)] private float strokeVerticalAmplitude = 0.035f;

        [Tooltip("Roll applied across alternating pull phases.")]
        [SerializeField, Range(0f, 12f)] private float strokeRollAmplitude = 3.5f;

        [Tooltip("Pitch pulse during the propulsion phase.")]
        [SerializeField, Range(0f, 12f)] private float strokePitchAmplitude = 2.25f;

        [Tooltip("How much the profile visually lingers in glide after propulsion.")]
        [SerializeField, Range(0f, 1f)] private float glideBias = 0.45f;

        [Header("-- Hand Stroke Geometry -------------------")]
        [Tooltip("How far each hand reaches forward during recovery.")]
        [SerializeField, Range(0f, 0.25f)] private float handReachDistance = 0.12f;

        [Tooltip("How far each hand drives backward during the pull phase.")]
        [SerializeField, Range(0f, 0.25f)] private float handPullDistance = 0.15f;

        [Tooltip("Sideways sweep magnitude of the hands during the stroke.")]
        [SerializeField, Range(0f, 0.16f)] private float handOutwardDistance = 0.05f;

        [Tooltip("Downward drive of the hands during propulsion.")]
        [SerializeField, Range(0f, 0.14f)] private float handDownwardDistance = 0.04f;

        [Tooltip("Recovery lift applied while the hand comes back forward.")]
        [SerializeField, Range(0f, 0.14f)] private float handRecoveryLift = 0.03f;

        [Tooltip("Yaw twist applied to the hand guides during the stroke.")]
        [SerializeField, Range(0f, 50f)] private float handYawAmplitude = 18f;

        [Tooltip("Roll twist applied to the hand guides during the stroke.")]
        [SerializeField, Range(0f, 60f)] private float handRollAmplitude = 22f;

        [Tooltip("Pitch articulation applied to the hand guides during the stroke.")]
        [SerializeField, Range(0f, 60f)] private float handPitchAmplitude = 26f;

        [Tooltip("How much sprinting tucks the hands closer to the body.")]
        [SerializeField, Range(0f, 0.1f)] private float sprintHandTuckDistance = 0.028f;

        [Tooltip("How synchronized the hands become while swimming at the surface. 0 = alternating, 1 = fully synced.")]
        [SerializeField, Range(0f, 1f)] private float surfaceHandSync = 0.72f;

        [Header("-- Drift And Mass -------------------------")]
        [Tooltip("Idle drift displacement applied while floating or neutral underwater.")]
        [SerializeField, Range(0f, 0.08f)] private float idleDriftAmplitude = 0.018f;

        [Tooltip("Idle drift frequency in cycles per second.")]
        [SerializeField, Range(0.05f, 2f)] private float idleDriftFrequency = 0.32f;

        [Tooltip("How strongly camera yaw delta lags the swim viewmodel.")]
        [SerializeField, Range(0f, 30f)] private float turnLagYaw = 7.5f;

        [Tooltip("How strongly turn input induces a viewmodel roll lag.")]
        [SerializeField, Range(0f, 20f)] private float turnLagRoll = 6f;

        [Tooltip("Spring speed for turn lag recovery.")]
        [SerializeField, Range(1f, 30f)] private float turnLagResponse = 9f;

        [Tooltip("How much acceleration pulses the rig as mass feedback.")]
        [SerializeField, Range(0f, 0.08f)] private float accelerationKickAmplitude = 0.018f;

        [Tooltip("How much heavy suits visually sag downward under sustained motion.")]
        [SerializeField, Range(0f, 0.08f)] private float inertialSinkAmplitude = 0.012f;

        [Header("-- Thresholds -----------------------------")]
        [Tooltip("Surface speed below this uses tread presentation instead of full stroke.")]
        [SerializeField, Range(0f, 4f)] private float surfaceStrokeStartSpeed = 0.55f;

        [Tooltip("Underwater speed below this uses neutral float presentation.")]
        [SerializeField, Range(0f, 4f)] private float underwaterStrokeStartSpeed = 0.45f;

        [Tooltip("Underwater speed above this prefers sprint presentation.")]
        [SerializeField, Range(0f, 12f)] private float underwaterSprintStartSpeed = 5.2f;

        /// <summary>Authored stroke family.</summary>
        public StrokeStyle AuthoredStrokeStyle => strokeStyle;

        /// <summary>Neutral local position for the swim viewmodel root.</summary>
        public Vector3 BaseLocalPosition => baseLocalPosition;

        /// <summary>Neutral local euler rotation for the swim viewmodel root.</summary>
        public Vector3 BaseLocalEuler => baseLocalEuler;

        /// <summary>Neutral local position for the left hand guide.</summary>
        public Vector3 LeftGuideBaseLocalPosition => leftGuideBaseLocalPosition;

        /// <summary>Neutral local euler rotation for the left hand guide.</summary>
        public Vector3 LeftGuideBaseLocalEuler => leftGuideBaseLocalEuler;

        /// <summary>Neutral local position for the right hand guide.</summary>
        public Vector3 RightGuideBaseLocalPosition => rightGuideBaseLocalPosition;

        /// <summary>Neutral local euler rotation for the right hand guide.</summary>
        public Vector3 RightGuideBaseLocalEuler => rightGuideBaseLocalEuler;

        /// <summary>Surface tread cadence in cycles per second.</summary>
        public float SurfaceTreadCadence => surfaceTreadCadence;

        /// <summary>Normal surface stroke cadence in cycles per second.</summary>
        public float SurfaceStrokeCadence => surfaceStrokeCadence;

        /// <summary>Normal underwater stroke cadence in cycles per second.</summary>
        public float UnderwaterStrokeCadence => underwaterStrokeCadence;

        /// <summary>Cadence multiplier for underwater sprint presentation.</summary>
        public float SprintCadenceMultiplier => sprintCadenceMultiplier;

        /// <summary>Strength of speed-driven cadence modulation.</summary>
        public float SpeedCadenceInfluence => speedCadenceInfluence;

        /// <summary>Forward surge magnitude per stroke.</summary>
        public float StrokeSurgeAmplitude => strokeSurgeAmplitude;

        /// <summary>Vertical lift/drop magnitude per stroke.</summary>
        public float StrokeVerticalAmplitude => strokeVerticalAmplitude;

        /// <summary>Alternating roll magnitude per stroke.</summary>
        public float StrokeRollAmplitude => strokeRollAmplitude;

        /// <summary>Pitch pulse during propulsion.</summary>
        public float StrokePitchAmplitude => strokePitchAmplitude;

        /// <summary>Visual glide linger bias after propulsion.</summary>
        public float GlideBias => glideBias;

        /// <summary>Forward reach of the hands during recovery.</summary>
        public float HandReachDistance => handReachDistance;

        /// <summary>Backward pull distance of the hands during propulsion.</summary>
        public float HandPullDistance => handPullDistance;

        /// <summary>Sideways sweep distance of the hands.</summary>
        public float HandOutwardDistance => handOutwardDistance;

        /// <summary>Downward drive distance of the hands.</summary>
        public float HandDownwardDistance => handDownwardDistance;

        /// <summary>Upward recovery lift of the hands.</summary>
        public float HandRecoveryLift => handRecoveryLift;

        /// <summary>Yaw twist of the hand guides during the stroke.</summary>
        public float HandYawAmplitude => handYawAmplitude;

        /// <summary>Roll twist of the hand guides during the stroke.</summary>
        public float HandRollAmplitude => handRollAmplitude;

        /// <summary>Pitch articulation of the hand guides during the stroke.</summary>
        public float HandPitchAmplitude => handPitchAmplitude;

        /// <summary>Tuck distance applied to sprinting hands.</summary>
        public float SprintHandTuckDistance => sprintHandTuckDistance;

        /// <summary>How synchronized the hands become while swimming at the surface.</summary>
        public float SurfaceHandSync => surfaceHandSync;

        /// <summary>Idle drift displacement while floating.</summary>
        public float IdleDriftAmplitude => idleDriftAmplitude;

        /// <summary>Idle drift frequency in cycles per second.</summary>
        public float IdleDriftFrequency => idleDriftFrequency;

        /// <summary>Viewmodel yaw lag magnitude during turns.</summary>
        public float TurnLagYaw => turnLagYaw;

        /// <summary>Viewmodel roll lag magnitude during turns.</summary>
        public float TurnLagRoll => turnLagRoll;

        /// <summary>Spring speed for turn lag response.</summary>
        public float TurnLagResponse => turnLagResponse;

        /// <summary>Acceleration pulse magnitude for mass feedback.</summary>
        public float AccelerationKickAmplitude => accelerationKickAmplitude;

        /// <summary>Downward sag under sustained inertial load.</summary>
        public float InertialSinkAmplitude => inertialSinkAmplitude;

        /// <summary>Surface speed threshold for full stroke presentation.</summary>
        public float SurfaceStrokeStartSpeed => surfaceStrokeStartSpeed;

        /// <summary>Underwater speed threshold for active stroke presentation.</summary>
        public float UnderwaterStrokeStartSpeed => underwaterStrokeStartSpeed;

        /// <summary>Underwater speed threshold for sprint presentation.</summary>
        public float UnderwaterSprintStartSpeed => underwaterSprintStartSpeed;
    }
}
