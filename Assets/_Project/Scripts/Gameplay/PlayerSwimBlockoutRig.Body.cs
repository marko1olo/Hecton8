using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public sealed partial class PlayerSwimBlockoutRig
    {
        private const string TorsoName = "Swim_Torso";
        private const string PelvisName = "Swim_Pelvis";
        private const string LeftThighName = "Swim_LeftThigh";
        private const string RightThighName = "Swim_RightThigh";
        private const string LeftCalfName = "Swim_LeftCalf";
        private const string RightCalfName = "Swim_RightCalf";
        private const string LeftFinName = "Swim_LeftFin";
        private const string RightFinName = "Swim_RightFin";
        private const string TorsoAttachmentName = "Swim_TorsoAttachment";
        private const string PelvisAttachmentName = "Swim_PelvisAttachment";
        private const string LeftThighAttachmentName = "Swim_LeftThighAttachment";
        private const string RightThighAttachmentName = "Swim_RightThighAttachment";
        private const string LeftCalfAttachmentName = "Swim_LeftCalfAttachment";
        private const string RightCalfAttachmentName = "Swim_RightCalfAttachment";
        private const string LeftFinAttachmentName = "Swim_LeftFinAttachment";
        private const string RightFinAttachmentName = "Swim_RightFinAttachment";
        private const int BodyModePoseStrideBytes = 96;

        [StructLayout(LayoutKind.Explicit, Size = BodyModePoseStrideBytes)]
        private struct BodyModePose
        {
            [FieldOffset(0)] public float BodyWeight;
            [FieldOffset(4)] public float TorsoPitch;
            [FieldOffset(8)] public float TorsoDrop;
            [FieldOffset(12)] public float TorsoForward;
            [FieldOffset(16)] public float PelvisPitch;
            [FieldOffset(20)] public float PelvisDrop;
            [FieldOffset(24)] public float LegSpread;
            [FieldOffset(28)] public float KneeTuck;
            [FieldOffset(32)] public float KickAmplitude;
            [FieldOffset(36)] public float KickLift;
            [FieldOffset(40)] public float KickForward;
            [FieldOffset(44)] public float KickBackward;
            [FieldOffset(48)] public float FinPitch;
            [FieldOffset(52)] public float FinSplay;
            [FieldOffset(56)] public float KickCadenceScale;
            [FieldOffset(60)] public float KickSync;
            [FieldOffset(64)] public float Streamline;
            [FieldOffset(68)] private uint _pad0;
            [FieldOffset(72)] private ulong _pad1;
            [FieldOffset(80)] private ulong _pad2;
            [FieldOffset(88)] private ulong _pad3;
        }

        [Header("── Full Body References ─────────────────")]
        [Tooltip("Optional explicit torso blockout transform.")]
        [SerializeField] private Transform torso;

        [Tooltip("Optional explicit pelvis blockout transform.")]
        [SerializeField] private Transform pelvis;

        [Tooltip("Optional explicit left thigh blockout transform.")]
        [SerializeField] private Transform leftThigh;

        [Tooltip("Optional explicit right thigh blockout transform.")]
        [SerializeField] private Transform rightThigh;

        [Tooltip("Optional explicit left calf blockout transform.")]
        [SerializeField] private Transform leftCalf;

        [Tooltip("Optional explicit right calf blockout transform.")]
        [SerializeField] private Transform rightCalf;

        [Tooltip("Optional explicit left fin blockout transform.")]
        [SerializeField] private Transform leftFin;

        [Tooltip("Optional explicit right fin blockout transform.")]
        [SerializeField] private Transform rightFin;

        [Tooltip("Optional explicit torso renderer.")]
        [SerializeField] private Renderer torsoRenderer;

        [Tooltip("Optional explicit pelvis renderer.")]
        [SerializeField] private Renderer pelvisRenderer;

        [Tooltip("Optional explicit left thigh renderer.")]
        [SerializeField] private Renderer leftThighRenderer;

        [Tooltip("Optional explicit right thigh renderer.")]
        [SerializeField] private Renderer rightThighRenderer;

        [Tooltip("Optional explicit left calf renderer.")]
        [SerializeField] private Renderer leftCalfRenderer;

        [Tooltip("Optional explicit right calf renderer.")]
        [SerializeField] private Renderer rightCalfRenderer;

        [Tooltip("Optional explicit left fin renderer.")]
        [SerializeField] private Renderer leftFinRenderer;

        [Tooltip("Optional explicit right fin renderer.")]
        [SerializeField] private Renderer rightFinRenderer;

        [Header("── Full Body Attachments ───────────────")]
        [Tooltip("Stable torso attachment for future authored art or shadow-proxy handoff.")]
        [SerializeField] private Transform torsoAttachment;

        [Tooltip("Stable pelvis attachment for future authored art or shadow-proxy handoff.")]
        [SerializeField] private Transform pelvisAttachment;

        [Tooltip("Stable left thigh attachment for future authored art or shadow-proxy handoff.")]
        [SerializeField] private Transform leftThighAttachment;

        [Tooltip("Stable right thigh attachment for future authored art or shadow-proxy handoff.")]
        [SerializeField] private Transform rightThighAttachment;

        [Tooltip("Stable left calf attachment for future authored art or shadow-proxy handoff.")]
        [SerializeField] private Transform leftCalfAttachment;

        [Tooltip("Stable right calf attachment for future authored art or shadow-proxy handoff.")]
        [SerializeField] private Transform rightCalfAttachment;

        [Tooltip("Stable left fin attachment for future authored art or shadow-proxy handoff.")]
        [SerializeField] private Transform leftFinAttachment;

        [Tooltip("Stable right fin attachment for future authored art or shadow-proxy handoff.")]
        [SerializeField] private Transform rightFinAttachment;

        [Header("── Full Body Visibility ────────────────")]
        [Tooltip("How quickly torso, pelvis, legs, and fins blend toward the active presentation mode.")]
        [SerializeField, Range(1f, 20f)] private float bodyVisibilityBlendSpeed = 7.5f;

        [Tooltip("How visible the body remains while fully dry so the player can still look down and read the lower silhouette.")]
        [SerializeField, Range(0f, 1f)] private float dryBodyVisibility = 0.46f;

        [Tooltip("How visible the body remains during shallow wade transitions.")]
        [SerializeField, Range(0f, 1f)] private float shallowBodyVisibility = 0.64f;

        [Tooltip("How visible the body remains during surface swim.")]
        [SerializeField, Range(0f, 1f)] private float surfaceBodyVisibility = 0.82f;

        [Tooltip("How visible the body remains during deep underwater swim.")]
        [SerializeField, Range(0f, 1f)] private float underwaterBodyVisibility = 0.96f;

        [Tooltip("Minimum lower-body visibility floor so feet and fins do not vanish first when the player looks down.")]
        [SerializeField, Range(0f, 0.5f)] private float lowerBodyVisibilityFloor = 0.24f;

        [Header("── Full Body Mass ──────────────────────")]
        [Tooltip("Extra torso thickness beyond the authored cube scale.")]
        [SerializeField, Range(0.8f, 2f)] private float torsoThicknessScale = 1.42f;

        [Tooltip("Extra pelvis thickness beyond the authored cube scale.")]
        [SerializeField, Range(0.8f, 2f)] private float pelvisThicknessScale = 1.28f;

        [Tooltip("Extra thigh/calf thickness beyond the authored cube scale.")]
        [SerializeField, Range(0.8f, 1.6f)] private float legThicknessScale = 1.06f;

        [Tooltip("Extra fin width/thickness beyond the authored cube scale.")]
        [SerializeField, Range(0.8f, 1.8f)] private float finThicknessScale = 1.2f;

        [Tooltip("Length multiplier for the procedural swim fins relative to their authored base scale.")]
        [SerializeField, Range(0.8f, 1.8f)] private float finLengthScale = 1f;

        [Tooltip("How strongly tools calm the torso and kick motion into a heavier brace.")]
        [SerializeField, Range(0f, 0.8f)] private float toolBodyStabilizeSuppression = 0.32f;

        [Header("── Full Body Pose ──────────────────────")]
        [Tooltip("How quickly the full body follows its procedural pose targets. Lower = heavier, higher = snappier.")]
        [SerializeField, Range(1f, 20f)] private float bodyPoseFollowSpeed = 8f;

        [Tooltip("Half-width of the hips relative to pelvis center.")]
        [SerializeField, Range(0.04f, 0.25f)] private float hipLateralOffset = 0.12f;

        [Tooltip("Hip drop relative to pelvis center.")]
        [SerializeField, Range(-0.2f, 0.1f)] private float hipVerticalOffset = -0.04f;

        [Tooltip("Hip forward bias relative to pelvis center.")]
        [SerializeField, Range(-0.1f, 0.15f)] private float hipForwardOffset = 0.025f;

        [Tooltip("How strongly torso yaw follows steady steering correction.")]
        [SerializeField, Range(0f, 12f)] private float torsoSteeringYawBias = 5.4f;

        [Tooltip("How strongly torso roll follows steady steering correction.")]
        [SerializeField, Range(0f, 12f)] private float torsoSteeringRollBias = 4.2f;

        [Tooltip("How strongly torso yaw lags behind sudden camera-turn sway.")]
        [SerializeField, Range(0f, 12f)] private float torsoTurnYawBias = 3.4f;

        [Tooltip("How strongly torso pitch compresses when the whole swim body is crowded by nearby geometry.")]
        [SerializeField, Range(0f, 12f)] private float torsoObstaclePitchBias = 6.4f;

        [Tooltip("How much the torso is pulled rearward when the player crowds geometry.")]
        [SerializeField, Range(0f, 0.08f)] private float torsoObstacleRearBias = 0.03f;

        [Tooltip("How much pelvis pitch counters the torso during active kicks.")]
        [SerializeField, Range(0f, 16f)] private float pelvisCounterPitchBias = 8f;

        [Tooltip("How strongly pelvis yaw follows steering correction.")]
        [SerializeField, Range(0f, 10f)] private float pelvisSteeringYawBias = 3.6f;

        [Tooltip("How much nearby geometry tucks the knees upward toward the torso.")]
        [SerializeField, Range(0f, 0.16f)] private float obstacleKneeTuck = 0.085f;

        [Tooltip("How much nearby geometry lifts the whole lower body upward out of the obstruction.")]
        [SerializeField, Range(0f, 0.14f)] private float obstacleLegLift = 0.052f;

        [Tooltip("How much nearby geometry retracts fins rearward instead of letting them push through walls.")]
        [SerializeField, Range(0f, 0.18f)] private float obstacleFinRearBias = 0.074f;

        [Tooltip("How much ascent tucks the knees and fins upward under the body.")]
        [SerializeField, Range(0f, 0.16f)] private float ascendKneeTuck = 0.068f;

        [Tooltip("How much descent extends the lower body rearward into a longer dive silhouette.")]
        [SerializeField, Range(0f, 0.2f)] private float descendLegExtend = 0.094f;

        [Tooltip("How much sprint streamlining extends the body rearward and calms lateral spread.")]
        [SerializeField, Range(0f, 0.16f)] private float sprintStreamlineBias = 0.082f;

        [Header("── Full Body Framing ───────────────────")]
        [Tooltip("Extra body visibility blended in when the camera is pitched down and the player is actively reading their own silhouette.")]
        [SerializeField, Range(0f, 0.35f)] private float lookDownBodyVisibilityBoost = 0.14f;

        [Tooltip("How much the torso is lowered in local camera space when the player looks downward.")]
        [SerializeField, Range(0f, 0.12f)] private float lookDownTorsoDrop = 0.032f;

        [Tooltip("How much the pelvis is lowered in local camera space when the player looks downward.")]
        [SerializeField, Range(0f, 0.16f)] private float lookDownPelvisDrop = 0.05f;

        [Tooltip("How much the lower body is extended forward when the player looks downward, keeping fins and legs readable instead of disappearing under the camera.")]
        [SerializeField, Range(0f, 0.18f)] private float lookDownLegForwardBias = 0.068f;

        [Tooltip("How much looking downward tightens leg spread so both legs stay readable in-frame instead of splitting too wide.")]
        [SerializeField, Range(0f, 0.8f)] private float lookDownLegSpreadTighten = 0.32f;

        [Tooltip("How much downward camera pitch tucks the knees to keep the silhouette compact and readable.")]
        [SerializeField, Range(0f, 0.12f)] private float lookDownKneeTuck = 0.042f;

        [Tooltip("How much fins are lifted when the player looks downward so the feet stay visible near the bottom of frame.")]
        [SerializeField, Range(0f, 0.12f)] private float lookDownFinLift = 0.038f;

#if UNITY_EDITOR
        [Header("── Full Body Diagnostics ───────────────")]
        [SerializeField] private float _debugBodyVisualWeight;
        [SerializeField] private float _debugLowerBodyWeight;
#endif

        /// <summary>Stable torso attachment for future authored art.</summary>
        public Transform TorsoAttachment => torsoAttachment != null ? torsoAttachment : torso;

        /// <summary>Stable pelvis attachment for future authored art.</summary>
        public Transform PelvisAttachment => pelvisAttachment != null ? pelvisAttachment : pelvis;

        /// <summary>Stable left thigh attachment for future authored art.</summary>
        public Transform LeftThighAttachment => leftThighAttachment != null ? leftThighAttachment : leftThigh;

        /// <summary>Stable right thigh attachment for future authored art.</summary>
        public Transform RightThighAttachment => rightThighAttachment != null ? rightThighAttachment : rightThigh;

        /// <summary>Stable left calf attachment for future authored art.</summary>
        public Transform LeftCalfAttachment => leftCalfAttachment != null ? leftCalfAttachment : leftCalf;

        /// <summary>Stable right calf attachment for future authored art.</summary>
        public Transform RightCalfAttachment => rightCalfAttachment != null ? rightCalfAttachment : rightCalf;

        /// <summary>Stable left fin attachment for future authored art.</summary>
        public Transform LeftFinAttachment => leftFinAttachment != null ? leftFinAttachment : leftFin;

        /// <summary>Stable right fin attachment for future authored art.</summary>
        public Transform RightFinAttachment => rightFinAttachment != null ? rightFinAttachment : rightFin;

        private float _bodyVisualWeight;
        private float _lowerBodyVisualWeight;
        private bool _hasInitializedBodyVisibleState;
        private Vector3 _torsoBaseLocalPosition;
        private Vector3 _pelvisBaseLocalPosition;
        private Vector3 _leftThighBaseLocalPosition;
        private Vector3 _rightThighBaseLocalPosition;
        private Vector3 _leftCalfBaseLocalPosition;
        private Vector3 _rightCalfBaseLocalPosition;
        private Vector3 _leftFinBaseLocalPosition;
        private Vector3 _rightFinBaseLocalPosition;
        private Vector3 _torsoBaseScale = Vector3.one;
        private Vector3 _pelvisBaseScale = Vector3.one;
        private Vector3 _leftThighBaseScale = Vector3.one;
        private Vector3 _rightThighBaseScale = Vector3.one;
        private Vector3 _leftCalfBaseScale = Vector3.one;
        private Vector3 _rightCalfBaseScale = Vector3.one;
        private Vector3 _leftFinBaseScale = Vector3.one;
        private Vector3 _rightFinBaseScale = Vector3.one;
        private bool _torsoVisible;
        private bool _pelvisVisible;
        private bool _leftThighVisible;
        private bool _rightThighVisible;
        private bool _leftCalfVisible;
        private bool _rightCalfVisible;
        private bool _leftFinVisible;
        private bool _rightFinVisible;
        private bool _torsoVisibleDirty;
        private bool _pelvisVisibleDirty;
        private bool _leftThighVisibleDirty;
        private bool _rightThighVisibleDirty;
        private bool _leftCalfVisibleDirty;
        private bool _rightCalfVisibleDirty;
        private bool _leftFinVisibleDirty;
        private bool _rightFinVisibleDirty;

        private void AutoResolveFullBodyReferences(Transform root)
        {
            if (torso == null)
                torso = FindTransformRecursive(root, TorsoName);

            if (pelvis == null)
                pelvis = FindTransformRecursive(root, PelvisName);

            if (leftThigh == null)
                leftThigh = FindTransformRecursive(root, LeftThighName);

            if (rightThigh == null)
                rightThigh = FindTransformRecursive(root, RightThighName);

            if (leftCalf == null)
                leftCalf = FindTransformRecursive(root, LeftCalfName);

            if (rightCalf == null)
                rightCalf = FindTransformRecursive(root, RightCalfName);

            if (leftFin == null)
                leftFin = FindTransformRecursive(root, LeftFinName);

            if (rightFin == null)
                rightFin = FindTransformRecursive(root, RightFinName);

            if (torsoAttachment == null)
                torsoAttachment = FindTransformRecursive(root, TorsoAttachmentName);

            if (pelvisAttachment == null)
                pelvisAttachment = FindTransformRecursive(root, PelvisAttachmentName);

            if (leftThighAttachment == null)
                leftThighAttachment = FindTransformRecursive(root, LeftThighAttachmentName);

            if (rightThighAttachment == null)
                rightThighAttachment = FindTransformRecursive(root, RightThighAttachmentName);

            if (leftCalfAttachment == null)
                leftCalfAttachment = FindTransformRecursive(root, LeftCalfAttachmentName);

            if (rightCalfAttachment == null)
                rightCalfAttachment = FindTransformRecursive(root, RightCalfAttachmentName);

            if (leftFinAttachment == null)
                leftFinAttachment = FindTransformRecursive(root, LeftFinAttachmentName);

            if (rightFinAttachment == null)
                rightFinAttachment = FindTransformRecursive(root, RightFinAttachmentName);

            if (torsoRenderer == null && torso != null)
                torso.TryGetComponent(out torsoRenderer);

            if (pelvisRenderer == null && pelvis != null)
                pelvis.TryGetComponent(out pelvisRenderer);

            if (leftThighRenderer == null && leftThigh != null)
                leftThigh.TryGetComponent(out leftThighRenderer);

            if (rightThighRenderer == null && rightThigh != null)
                rightThigh.TryGetComponent(out rightThighRenderer);

            if (leftCalfRenderer == null && leftCalf != null)
                leftCalf.TryGetComponent(out leftCalfRenderer);

            if (rightCalfRenderer == null && rightCalf != null)
                rightCalf.TryGetComponent(out rightCalfRenderer);

            if (leftFinRenderer == null && leftFin != null)
                leftFin.TryGetComponent(out leftFinRenderer);

            if (rightFinRenderer == null && rightFin != null)
                rightFin.TryGetComponent(out rightFinRenderer);
        }

        private void CacheFullBodyBaseScales()
        {
            if (torso != null)
            {
                _torsoBaseLocalPosition = torso.localPosition;
                _torsoBaseScale = torso.localScale;
            }

            if (pelvis != null)
            {
                _pelvisBaseLocalPosition = pelvis.localPosition;
                _pelvisBaseScale = pelvis.localScale;
            }

            if (leftThigh != null)
            {
                _leftThighBaseLocalPosition = leftThigh.localPosition;
                _leftThighBaseScale = leftThigh.localScale;
            }

            if (rightThigh != null)
            {
                _rightThighBaseLocalPosition = rightThigh.localPosition;
                _rightThighBaseScale = rightThigh.localScale;
            }

            if (leftCalf != null)
            {
                _leftCalfBaseLocalPosition = leftCalf.localPosition;
                _leftCalfBaseScale = leftCalf.localScale;
            }

            if (rightCalf != null)
            {
                _rightCalfBaseLocalPosition = rightCalf.localPosition;
                _rightCalfBaseScale = rightCalf.localScale;
            }

            if (leftFin != null)
            {
                _leftFinBaseLocalPosition = leftFin.localPosition;
                _leftFinBaseScale = leftFin.localScale;
            }

            if (rightFin != null)
            {
                _rightFinBaseLocalPosition = rightFin.localPosition;
                _rightFinBaseScale = rightFin.localScale;
            }
        }

        private bool AreFullBodyAttachmentsResolved()
        {
            return torsoAttachment != null &&
                   pelvisAttachment != null &&
                   leftThighAttachment != null &&
                   rightThighAttachment != null &&
                   leftCalfAttachment != null &&
                   rightCalfAttachment != null &&
                   leftFinAttachment != null &&
                   rightFinAttachment != null;
        }

        private void ForceSyncFullBodyAttachmentPoints()
        {
            ApplyAttachmentPose(torsoAttachment, torso);
            ApplyAttachmentPose(pelvisAttachment, pelvis);
            ApplyAttachmentPose(leftThighAttachment, leftThigh);
            ApplyAttachmentPose(rightThighAttachment, rightThigh);
            ApplyAttachmentPose(leftCalfAttachment, leftCalf);
            ApplyAttachmentPose(rightCalfAttachment, rightCalf);
            ApplyAttachmentPose(leftFinAttachment, leftFin);
            ApplyAttachmentPose(rightFinAttachment, rightFin);
        }

        private void ApplyFullBodyPose(
            PlayerSwimPresentationMode mode,
            SwimPresentationProfile profile,
            float suitScale,
            float sprintBoost,
            float verticalCompression,
            float dt)
        {
            if (torso == null || pelvis == null || leftThigh == null || rightThigh == null || leftCalf == null || rightCalf == null || leftFin == null || rightFin == null)
                return;

            BodyModePose pose = ResolveBodyModePose(mode);
            // Profile was accepted on this path and then ignored for every lower-body field.
            // Hands/shoulders already read CurrentProfile (suit scale, guide bases); legs/fins
            // must respond to the same authored cadence, kick mass and surface sync or a designer
            // retune of SwimPresentationProfile only moves the arms.
            ApplySwimProfileToBodyPose(ref pose, profile, mode);
            float lookDownWeight = ResolveLookDownWeight();
            float bodyTargetWeight = math.max(pose.BodyWeight, ResolveBodyTargetWeight(mode));
            float lowerBodyWeightScale = math.lerp(0.72f, 1f, math.saturate(lowerBodyVisibilityFloor * 2f));
            float lowerBodyTargetWeight = bodyTargetWeight * lowerBodyWeightScale;
            float lookDownVisibilityBoostWeight = lookDownWeight * lookDownBodyVisibilityBoost;
            bodyTargetWeight = math.saturate(bodyTargetWeight + (1f - bodyTargetWeight) * lookDownVisibilityBoostWeight);
            lowerBodyTargetWeight = math.saturate(lowerBodyTargetWeight + (1f - lowerBodyTargetWeight) * math.max(lookDownVisibilityBoostWeight, lookDownWeight * lowerBodyVisibilityFloor));
            if (!_hasInitializedBodyVisibleState && (bodyTargetWeight > 0f || lowerBodyTargetWeight > 0f))
            {
                _bodyVisualWeight = bodyTargetWeight;
                _lowerBodyVisualWeight = lowerBodyTargetWeight;
                _hasInitializedBodyVisibleState = true;
            }

            float visibilityT = ResolveDecayBlend(bodyVisibilityBlendSpeed, dt);
            _bodyVisualWeight = math.lerp(_bodyVisualWeight, bodyTargetWeight, visibilityT);
            _lowerBodyVisualWeight = math.lerp(_lowerBodyVisualWeight, lowerBodyTargetWeight, visibilityT);

            float poseT = ResolveDecayBlend(bodyPoseFollowSpeed, dt);
            float propulsion = swimPresentationController.CurrentPropulsionPulse;
            float strokeImpulse = swimPresentationController.CurrentStrokePowerImpulse;
            float strokePhase = swimPresentationController.CurrentStrokePhase;
            float steering = swimPresentationController.CurrentDirectionalCorrection;
            float turnSway = swimPresentationController.CurrentCameraTurnSway;
            float verticalPose = swimPresentationController.CurrentVerticalPoseBias;
            float obstaclePressure = swimPresentationController.CurrentObstacleRootPressure;
            float obstacleDifference = swimPresentationController.CurrentObstacleRootDifference;
            float obstacleVertical = swimPresentationController.CurrentObstacleRootVerticalBias;
            float toolBlend = swimPresentationController.CurrentToolBlend;
            float toolMotionScale = math.lerp(1f, 1f - toolBodyStabilizeSuppression, toolBlend);
            float bodyBulkScale = suitScale + sprintBoost * 0.45f;
            float lowerBodyBulkScale = suitScale + sprintBoost * 0.32f;
            float strokeCos = ApproximateCosCycle01(strokePhase);
            float surfaceWaveRoll = mode == PlayerSwimPresentationMode.SurfaceTread ? strokeCos * 1.8f : 0f;
            float streamline = pose.Streamline + (mode == PlayerSwimPresentationMode.UnderwaterSprint ? sprintStreamlineBias : 0f);

            Vector3 torsoTargetLocalPosition = _torsoBaseLocalPosition;
            torsoTargetLocalPosition.x -= steering * 0.022f * _bodyVisualWeight;
            torsoTargetLocalPosition.y -= pose.TorsoDrop;
            torsoTargetLocalPosition.y -= lookDownWeight * lookDownTorsoDrop;
            torsoTargetLocalPosition.y += math.max(0f, obstacleVertical) * obstacleLegLift * 0.25f;
            torsoTargetLocalPosition.z += pose.TorsoForward;
            torsoTargetLocalPosition.z -= obstaclePressure * torsoObstacleRearBias;
            torsoTargetLocalPosition.z -= streamline * 0.18f;
            torsoTargetLocalPosition.z += lookDownWeight * lookDownLegForwardBias * 0.16f;

            Vector3 torsoTargetLocalEuler = Vector3.zero;
            torsoTargetLocalEuler.x += pose.TorsoPitch;
            torsoTargetLocalEuler.x += propulsion * 4.5f * toolMotionScale;
            torsoTargetLocalEuler.x += math.max(0f, verticalPose) * 5.5f;
            torsoTargetLocalEuler.x -= math.max(0f, -verticalPose) * 3.5f;
            torsoTargetLocalEuler.x += obstaclePressure * torsoObstaclePitchBias;
            torsoTargetLocalEuler.x += strokeImpulse * 1.4f;
            torsoTargetLocalEuler.y -= steering * torsoSteeringYawBias;
            torsoTargetLocalEuler.y -= turnSway * torsoTurnYawBias;
            torsoTargetLocalEuler.y -= obstacleDifference * 3.6f;
            torsoTargetLocalEuler.z -= steering * torsoSteeringRollBias;
            torsoTargetLocalEuler.z -= turnSway * 3f;
            torsoTargetLocalEuler.z += surfaceWaveRoll;
            ApplyLocalBlockPart(
                torso,
                torsoRenderer,
                torsoTargetLocalPosition,
                torsoTargetLocalEuler,
                _torsoBaseScale,
                _bodyVisualWeight,
                bodyBulkScale * torsoThicknessScale,
                verticalCompression,
                poseT);

            Vector3 pelvisTargetLocalPosition = _pelvisBaseLocalPosition;
            pelvisTargetLocalPosition.x -= steering * 0.014f * _bodyVisualWeight;
            pelvisTargetLocalPosition.y -= pose.PelvisDrop;
            pelvisTargetLocalPosition.y -= lookDownWeight * lookDownPelvisDrop;
            pelvisTargetLocalPosition.y += math.max(0f, obstacleVertical) * obstacleLegLift * 0.48f;
            pelvisTargetLocalPosition.z -= streamline * 0.06f;
            pelvisTargetLocalPosition.z -= obstaclePressure * obstacleFinRearBias * 0.22f;
            pelvisTargetLocalPosition.z += lookDownWeight * lookDownLegForwardBias * 0.34f;

            Vector3 pelvisTargetLocalEuler = Vector3.zero;
            pelvisTargetLocalEuler.x += pose.PelvisPitch;
            pelvisTargetLocalEuler.x -= propulsion * pelvisCounterPitchBias * 0.35f * toolMotionScale;
            pelvisTargetLocalEuler.x -= strokeImpulse * 1.1f;
            pelvisTargetLocalEuler.x -= math.max(0f, verticalPose) * 2.8f;
            pelvisTargetLocalEuler.x += math.max(0f, -verticalPose) * 2.2f;
            pelvisTargetLocalEuler.y -= steering * pelvisSteeringYawBias;
            pelvisTargetLocalEuler.y -= obstacleDifference * 2.1f;
            pelvisTargetLocalEuler.z += steering * 1.8f;
            ApplyLocalBlockPart(
                pelvis,
                pelvisRenderer,
                pelvisTargetLocalPosition,
                pelvisTargetLocalEuler,
                _pelvisBaseScale,
                _bodyVisualWeight,
                bodyBulkScale * pelvisThicknessScale,
                verticalCompression,
                poseT);

            float kickPhase = strokePhase * pose.KickCadenceScale;
            float rightPhaseOffset = math.lerp(0.5f, 0f, pose.KickSync);
            float leftKick = ApproximateSinCycle01(kickPhase);
            float rightKick = ApproximateSinCycle01(kickPhase + rightPhaseOffset);
            ApplyLegPose(
                true,
                pose,
                pelvisTargetLocalPosition,
                leftKick,
                bodyBulkScale,
                lowerBodyBulkScale,
                toolMotionScale,
                verticalCompression,
                steering,
                turnSway,
                verticalPose,
                lookDownWeight,
                obstaclePressure,
                obstacleDifference,
                obstacleVertical,
                poseT);
            ApplyLegPose(
                false,
                pose,
                pelvisTargetLocalPosition,
                rightKick,
                bodyBulkScale,
                lowerBodyBulkScale,
                toolMotionScale,
                verticalCompression,
                steering,
                turnSway,
                verticalPose,
                lookDownWeight,
                obstaclePressure,
                obstacleDifference,
                obstacleVertical,
                poseT);

#if UNITY_EDITOR
            _debugBodyVisualWeight = _bodyVisualWeight;
            _debugLowerBodyWeight = _lowerBodyVisualWeight;
#endif
        }

        /// <summary>
        /// Scales the mode-local lower-body pose by the active <see cref="SwimPresentationProfile"/>.
        /// Mode tables stay the structural baseline; profile multiplies cadence, kick travel, fin pitch,
        /// surface kick sync and suit-family mass so legs/fins track the same asset the arms already use.
        /// </summary>
        private static void ApplySwimProfileToBodyPose(
            ref BodyModePose pose,
            SwimPresentationProfile profile,
            PlayerSwimPresentationMode mode)
        {
            if (profile == null)
                return;

            float cadenceReference;
            float cadenceAuthored;
            switch (mode)
            {
                case PlayerSwimPresentationMode.SurfaceTread:
                    cadenceReference = 0.6f;
                    cadenceAuthored = profile.SurfaceTreadCadence;
                    break;
                case PlayerSwimPresentationMode.SurfaceStroke:
                    cadenceReference = 1.05f;
                    cadenceAuthored = profile.SurfaceStrokeCadence;
                    break;
                case PlayerSwimPresentationMode.UnderwaterSprint:
                    cadenceReference = 0.95f * 1.35f;
                    cadenceAuthored = profile.UnderwaterStrokeCadence * profile.SprintCadenceMultiplier;
                    break;
                case PlayerSwimPresentationMode.Dry:
                case PlayerSwimPresentationMode.None:
                    cadenceReference = 1f;
                    cadenceAuthored = 1f;
                    break;
                default:
                    cadenceReference = 0.95f;
                    cadenceAuthored = profile.UnderwaterStrokeCadence;
                    break;
            }

            float cadenceScale = math.clamp(
                cadenceAuthored / math.max(0.0001f, cadenceReference),
                0.35f, 2.5f);
            pose.KickCadenceScale *= cadenceScale;

            const float kickTravelReference = 0.06f + 0.035f;
            float kickTravelAuthored = profile.StrokeSurgeAmplitude + profile.StrokeVerticalAmplitude;
            float kickTravelScale = math.clamp(kickTravelAuthored / kickTravelReference, 0.35f, 2.5f);
            pose.KickAmplitude *= kickTravelScale;
            pose.KickLift *= kickTravelScale;
            pose.KickForward *= kickTravelScale;
            pose.KickBackward *= kickTravelScale;

            if (mode == PlayerSwimPresentationMode.SurfaceTread ||
                mode == PlayerSwimPresentationMode.SurfaceStroke)
            {
                float handSync = math.saturate(profile.SurfaceHandSync);
                pose.KickSync = math.lerp(pose.KickSync, handSync, 0.65f);
            }

            float glideDelta = math.saturate(profile.GlideBias) - 0.45f;
            if (mode == PlayerSwimPresentationMode.UnderwaterGlide ||
                mode == PlayerSwimPresentationMode.UnderwaterNeutral)
            {
                pose.Streamline = math.max(0f, pose.Streamline + glideDelta * 0.12f);
            }

            pose.PelvisDrop = math.max(0f, pose.PelvisDrop + (profile.InertialSinkAmplitude - 0.012f));

            float finPitchScale = math.clamp(profile.StrokePitchAmplitude / 2.25f, 0.5f, 2f);
            pose.FinPitch *= finPitchScale;

            switch (profile.AuthoredStrokeStyle)
            {
                case SwimPresentationProfile.StrokeStyle.LightExpedition:
                    pose.KickCadenceScale *= 1.08f;
                    pose.KickAmplitude *= 0.94f;
                    break;
                case SwimPresentationProfile.StrokeStyle.HeavyIndustrial:
                    pose.KickCadenceScale *= 0.82f;
                    pose.KickAmplitude *= 1.12f;
                    pose.PelvisDrop += 0.008f;
                    break;
                case SwimPresentationProfile.StrokeStyle.PoweredAssist:
                    pose.KickCadenceScale *= 1.18f;
                    pose.KickAmplitude *= 1.06f;
                    pose.Streamline += 0.02f;
                    break;
            }
        }

        private BodyModePose ResolveBodyModePose(PlayerSwimPresentationMode mode)
        {
            BodyModePose pose = default;
            switch (mode)
            {
                case PlayerSwimPresentationMode.Dry:
                    pose.BodyWeight = dryBodyVisibility;
                    pose.TorsoPitch = 11f;
                    pose.LegSpread = 0.02f;
                    pose.KneeTuck = 0.015f;
                    pose.KickCadenceScale = 0.45f;
                    pose.KickSync = 1f;
                    pose.FinPitch = 4f;
                    pose.FinSplay = 3f;
                    break;

                case PlayerSwimPresentationMode.ShallowWade:
                    pose.BodyWeight = shallowBodyVisibility;
                    pose.TorsoPitch = 7f;
                    pose.TorsoDrop = 0.01f;
                    pose.PelvisDrop = 0.015f;
                    pose.LegSpread = 0.04f;
                    pose.KneeTuck = 0.035f;
                    pose.KickAmplitude = 0.026f;
                    pose.KickLift = 0.018f;
                    pose.KickForward = 0.022f;
                    pose.KickBackward = 0.012f;
                    pose.KickCadenceScale = 0.75f;
                    pose.KickSync = 0.82f;
                    pose.FinPitch = 6f;
                    pose.FinSplay = 4f;
                    break;

                case PlayerSwimPresentationMode.SurfaceTread:
                    pose.BodyWeight = surfaceBodyVisibility;
                    pose.TorsoPitch = 18f;
                    pose.TorsoDrop = 0.02f;
                    pose.PelvisDrop = 0.03f;
                    pose.LegSpread = 0.055f;
                    pose.KneeTuck = 0.09f;
                    pose.KickAmplitude = 0.04f;
                    pose.KickLift = 0.028f;
                    pose.KickForward = 0.038f;
                    pose.KickBackward = 0.02f;
                    pose.KickCadenceScale = 0.72f;
                    pose.KickSync = 0.25f;
                    pose.FinPitch = 12f;
                    pose.FinSplay = 8f;
                    break;

                case PlayerSwimPresentationMode.SurfaceStroke:
                    pose.BodyWeight = surfaceBodyVisibility;
                    pose.TorsoPitch = 6f;
                    pose.TorsoForward = 0.012f;
                    pose.PelvisPitch = -2f;
                    pose.LegSpread = 0.045f;
                    pose.KneeTuck = 0.04f;
                    pose.KickAmplitude = 0.055f;
                    pose.KickLift = 0.04f;
                    pose.KickForward = 0.055f;
                    pose.KickBackward = 0.032f;
                    pose.KickCadenceScale = 1f;
                    pose.KickSync = 0.08f;
                    pose.FinPitch = 15f;
                    pose.FinSplay = 9f;
                    pose.Streamline = 0.03f;
                    break;

                case PlayerSwimPresentationMode.UnderwaterNeutral:
                    pose.BodyWeight = underwaterBodyVisibility * 0.88f;
                    pose.TorsoPitch = 2f;
                    pose.LegSpread = 0.03f;
                    pose.KneeTuck = 0.02f;
                    pose.KickAmplitude = 0.024f;
                    pose.KickLift = 0.02f;
                    pose.KickForward = 0.028f;
                    pose.KickBackward = 0.016f;
                    pose.KickCadenceScale = 0.65f;
                    pose.KickSync = 0.05f;
                    pose.FinPitch = 12f;
                    pose.FinSplay = 7f;
                    pose.Streamline = 0.04f;
                    break;

                case PlayerSwimPresentationMode.UnderwaterStroke:
                    pose.BodyWeight = underwaterBodyVisibility * 0.94f;
                    pose.TorsoPitch = -2.5f;
                    pose.TorsoForward = 0.014f;
                    pose.PelvisPitch = -1.5f;
                    pose.LegSpread = 0.028f;
                    pose.KneeTuck = 0.024f;
                    pose.KickAmplitude = 0.066f;
                    pose.KickLift = 0.046f;
                    pose.KickForward = 0.07f;
                    pose.KickBackward = 0.043f;
                    pose.KickCadenceScale = 1.08f;
                    pose.KickSync = 0.035f;
                    pose.FinPitch = 18f;
                    pose.FinSplay = 8f;
                    pose.Streamline = 0.065f;
                    break;

                case PlayerSwimPresentationMode.UnderwaterGlide:
                    pose.BodyWeight = underwaterBodyVisibility * 0.9f;
                    pose.TorsoPitch = -1.5f;
                    pose.LegSpread = 0.02f;
                    pose.KneeTuck = 0.015f;
                    pose.KickAmplitude = 0.016f;
                    pose.KickLift = 0.012f;
                    pose.KickForward = 0.018f;
                    pose.KickBackward = 0.01f;
                    pose.KickCadenceScale = 0.42f;
                    pose.KickSync = 0.05f;
                    pose.FinPitch = 10f;
                    pose.FinSplay = 5f;
                    pose.Streamline = 0.08f;
                    break;

                case PlayerSwimPresentationMode.UnderwaterSprint:
                    pose.BodyWeight = underwaterBodyVisibility;
                    pose.TorsoPitch = -4f;
                    pose.TorsoForward = 0.018f;
                    pose.PelvisPitch = -3f;
                    pose.LegSpread = 0.028f;
                    pose.KneeTuck = 0.018f;
                    pose.KickAmplitude = 0.082f;
                    pose.KickLift = 0.056f;
                    pose.KickForward = 0.084f;
                    pose.KickBackward = 0.052f;
                    pose.KickCadenceScale = 1.45f;
                    pose.KickSync = 0.03f;
                    pose.FinPitch = 22f;
                    pose.FinSplay = 9f;
                    pose.Streamline = 0.12f;
                    break;

                default:
                    pose.BodyWeight = underwaterBodyVisibility;
                    pose.TorsoPitch = 0f;
                    pose.LegSpread = 0.035f;
                    pose.KneeTuck = 0.024f;
                    pose.KickAmplitude = 0.058f;
                    pose.KickLift = 0.042f;
                    pose.KickForward = 0.062f;
                    pose.KickBackward = 0.038f;
                    pose.KickCadenceScale = 1f;
                    pose.KickSync = 0.04f;
                    pose.FinPitch = 18f;
                    pose.FinSplay = 8f;
                    pose.Streamline = 0.055f;
                    break;
            }

            return pose;
        }

        private void ApplyLegPose(
            bool isLeft,
            BodyModePose pose,
            Vector3 pelvisLocalPosition,
            float kickWave,
            float bodyBulkScale,
            float lowerBodyBulkScale,
            float toolMotionScale,
            float verticalCompression,
            float steering,
            float turnSway,
            float verticalPose,
            float lookDownWeight,
            float obstaclePressure,
            float obstacleDifference,
            float obstacleVertical,
            float poseT)
        {
            Transform thigh = isLeft ? leftThigh : rightThigh;
            Transform calf = isLeft ? leftCalf : rightCalf;
            Transform fin = isLeft ? leftFin : rightFin;
            Renderer thighRenderer = isLeft ? leftThighRenderer : rightThighRenderer;
            Renderer calfRenderer = isLeft ? leftCalfRenderer : rightCalfRenderer;
            Renderer finRenderer = isLeft ? leftFinRenderer : rightFinRenderer;
            Vector3 thighBaseScale = isLeft ? _leftThighBaseScale : _rightThighBaseScale;
            Vector3 calfBaseScale = isLeft ? _leftCalfBaseScale : _rightCalfBaseScale;
            Vector3 finBaseScale = isLeft ? _leftFinBaseScale : _rightFinBaseScale;
            Vector3 thighMidBase = isLeft ? _leftThighBaseLocalPosition : _rightThighBaseLocalPosition;
            Vector3 calfMidBase = isLeft ? _leftCalfBaseLocalPosition : _rightCalfBaseLocalPosition;
            float sideSign = isLeft ? -1f : 1f;
            float legWeight = _lowerBodyVisualWeight;

            Vector3 baseHipLocal = _pelvisBaseLocalPosition + new Vector3(sideSign * hipLateralOffset, hipVerticalOffset, hipForwardOffset);
            Vector3 baseKneeLocal = thighMidBase * 2f - baseHipLocal;
            Vector3 baseAnkleLocal = calfMidBase * 2f - baseKneeLocal;

            Vector3 pelvisDelta = pelvisLocalPosition - _pelvisBaseLocalPosition;
            float kickDrive = math.max(0f, kickWave) * toolMotionScale;
            float kickRecover = math.max(0f, -kickWave) * toolMotionScale;
            float ascend = math.max(0f, verticalPose);
            float descend = math.max(0f, -verticalPose);
            float steeringBias = sideSign * steering;
            float obstacleSide = isLeft ? math.max(0f, obstacleDifference) : math.max(0f, -obstacleDifference);
            float streamline = pose.Streamline;
            float lookDownSpreadScale = math.max(0.2f, 1f - lookDownWeight * lookDownLegSpreadTighten);
            float legSpread = pose.LegSpread * lookDownSpreadScale;
            float lookDownForward = lookDownWeight * lookDownLegForwardBias;
            float lookDownTuck = lookDownWeight * lookDownKneeTuck;
            float lookDownFinRaise = lookDownWeight * lookDownFinLift;

            Vector3 hipLocal = baseHipLocal + pelvisDelta;
            hipLocal.x += sideSign * legSpread * 0.22f;
            hipLocal.z += lookDownForward * 0.18f;

            Vector3 kneeLocal = baseKneeLocal + pelvisDelta;
            kneeLocal.x += sideSign * (legSpread + steeringBias * 0.028f + kickWave * pose.KickAmplitude * 0.35f - obstacleSide * 0.018f);
            kneeLocal.y += pose.KneeTuck + lookDownTuck + obstaclePressure * obstacleKneeTuck + ascend * ascendKneeTuck + kickRecover * pose.KickLift * 0.55f;
            kneeLocal.y += math.max(0f, obstacleVertical) * obstacleLegLift * 0.3f;
            kneeLocal.z += streamline * 0.08f + lookDownForward * 0.45f + descend * descendLegExtend * 0.35f + kickRecover * pose.KickForward * 0.45f - kickDrive * pose.KickBackward * 0.18f;

            Vector3 ankleLocal = baseAnkleLocal + pelvisDelta;
            ankleLocal.x += sideSign * (legSpread * 0.72f + steeringBias * 0.018f + kickWave * pose.KickAmplitude * 0.18f - obstacleSide * 0.012f);
            ankleLocal.y += lookDownFinRaise + obstaclePressure * obstacleLegLift + ascend * ascendKneeTuck * 0.28f + kickWave * pose.KickLift;
            ankleLocal.y += math.max(0f, obstacleVertical) * obstacleLegLift * 0.42f;
            ankleLocal.z += streamline * 0.12f + lookDownForward + descend * descendLegExtend + kickRecover * pose.KickForward - kickDrive * pose.KickBackward;
            ankleLocal.z -= obstaclePressure * obstacleFinRearBias;

            float finPitch = pose.FinPitch + kickWave * 6.5f + descend * 7f - ascend * 5f + obstaclePressure * 5f;
            float finYaw = sideSign * (pose.FinSplay + steeringBias * 4.5f + obstacleDifference * 2.5f);
            float finRoll = sideSign * (kickWave * 5f + turnSway * 4f);
            Quaternion finRotation = ResolveEulerRotationNoTrig(new Vector3(finPitch, finYaw, finRoll));
            float finLength = (isLeft ? _leftFinBaseScale.z : _rightFinBaseScale.z) * finLengthScale * (1f + streamline * 0.85f);
            Vector3 finDirection = finRotation * Vector3.forward;
            Vector3 finTipLocal = ankleLocal + finDirection * finLength;
            float finWeight = math.max(legWeight, _bodyVisualWeight * lowerBodyVisibilityFloor);

            ApplyLocalSegment(
                thigh,
                thighRenderer,
                hipLocal,
                kneeLocal,
                thighBaseScale,
                legWeight,
                lowerBodyBulkScale * legThicknessScale,
                verticalCompression,
                poseT);
            ApplyLocalSegment(
                calf,
                calfRenderer,
                kneeLocal,
                ankleLocal,
                calfBaseScale,
                legWeight,
                lowerBodyBulkScale * legThicknessScale * 0.94f,
                verticalCompression,
                poseT);
            ApplyLocalSegment(
                fin,
                finRenderer,
                ankleLocal,
                finTipLocal,
                finBaseScale,
                finWeight,
                bodyBulkScale * finThicknessScale,
                verticalCompression,
                poseT);
        }

        private void ApplyLocalBlockPart(
            Transform part,
            Renderer partRenderer,
            Vector3 targetLocalPosition,
            Vector3 targetLocalEuler,
            Vector3 baseScale,
            float visibilityWeight,
            float thicknessScale,
            float verticalCompression,
            float poseT)
        {
            if (part == null)
                return;

            float visibility = math.saturate(visibilityWeight);
            Vector3 targetScale = baseScale;
            targetScale.x *= thicknessScale * visibility;
            targetScale.y *= thicknessScale * visibility * verticalCompression;
            targetScale.z *= thicknessScale;
            bool rendererVisible = showDebugCubes && visibility > rendererDisableThreshold;
            QueueRendererVisibility(partRenderer, rendererVisible);

            part.localPosition = ApproximateVectorLerp(part.localPosition, targetLocalPosition, poseT);
            Quaternion targetRotation = ResolveEulerRotationNoTrig(targetLocalEuler);
            part.localRotation = ApproximateNlerpNoSqrt(part.localRotation, targetRotation, poseT);
            part.localScale = targetScale;
        }

        private void ApplyLocalSegment(
            Transform segment,
            Renderer segmentRenderer,
            Vector3 startLocal,
            Vector3 endLocal,
            Vector3 baseScale,
            float visibilityWeight,
            float thicknessScale,
            float verticalCompression,
            float poseT)
        {
            if (segment == null)
                return;

            Vector3 direction = endLocal - startLocal;
            float lengthSq = direction.sqrMagnitude;
            float length;
            if (lengthSq <= 0.00000001f)
            {
                direction = Vector3.forward;
                length = 0.0001f;
            }
            else
            {
                float inverseLength = math.rsqrt(lengthSq);
                length = lengthSq * inverseLength;
                direction *= inverseLength;
            }

            Vector3 midpoint = (startLocal + endLocal) * 0.5f;
            Vector3 upAxis = math.abs(Vector3.Dot(direction, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            Quaternion targetRotation = ResolveLookRotationNoTrig(direction, upAxis);
            Vector3 targetScale = baseScale;
            targetScale.x *= thicknessScale * math.saturate(visibilityWeight);
            targetScale.y *= thicknessScale * math.saturate(visibilityWeight) * verticalCompression;
            targetScale.z = length;

            bool rendererVisible = showDebugCubes && visibilityWeight > rendererDisableThreshold;
            QueueRendererVisibility(segmentRenderer, rendererVisible);

            segment.localPosition = ApproximateVectorLerp(segment.localPosition, midpoint, poseT);
            segment.localRotation = ApproximateNlerpNoSqrt(segment.localRotation, targetRotation, poseT);
            segment.localScale = targetScale;
        }

        private bool TryQueueBodyRendererVisibility(Renderer renderer, bool visible)
        {
            if (renderer == null)
                return false;

            if (ReferenceEquals(renderer, torsoRenderer))
            {
                _torsoVisible = visible;
                _torsoVisibleDirty = true;
                return true;
            }

            if (ReferenceEquals(renderer, pelvisRenderer))
            {
                _pelvisVisible = visible;
                _pelvisVisibleDirty = true;
                return true;
            }

            if (ReferenceEquals(renderer, leftThighRenderer))
            {
                _leftThighVisible = visible;
                _leftThighVisibleDirty = true;
                return true;
            }

            if (ReferenceEquals(renderer, rightThighRenderer))
            {
                _rightThighVisible = visible;
                _rightThighVisibleDirty = true;
                return true;
            }

            if (ReferenceEquals(renderer, leftCalfRenderer))
            {
                _leftCalfVisible = visible;
                _leftCalfVisibleDirty = true;
                return true;
            }

            if (ReferenceEquals(renderer, rightCalfRenderer))
            {
                _rightCalfVisible = visible;
                _rightCalfVisibleDirty = true;
                return true;
            }

            if (ReferenceEquals(renderer, leftFinRenderer))
            {
                _leftFinVisible = visible;
                _leftFinVisibleDirty = true;
                return true;
            }

            if (ReferenceEquals(renderer, rightFinRenderer))
            {
                _rightFinVisible = visible;
                _rightFinVisibleDirty = true;
                return true;
            }

            return false;
        }

        private void FlushQueuedBodyRendererVisibility()
        {
            FlushRendererVisibility(torsoRenderer, ref _torsoVisibleDirty, _torsoVisible);
            FlushRendererVisibility(pelvisRenderer, ref _pelvisVisibleDirty, _pelvisVisible);
            FlushRendererVisibility(leftThighRenderer, ref _leftThighVisibleDirty, _leftThighVisible);
            FlushRendererVisibility(rightThighRenderer, ref _rightThighVisibleDirty, _rightThighVisible);
            FlushRendererVisibility(leftCalfRenderer, ref _leftCalfVisibleDirty, _leftCalfVisible);
            FlushRendererVisibility(rightCalfRenderer, ref _rightCalfVisibleDirty, _rightCalfVisible);
            FlushRendererVisibility(leftFinRenderer, ref _leftFinVisibleDirty, _leftFinVisible);
            FlushRendererVisibility(rightFinRenderer, ref _rightFinVisibleDirty, _rightFinVisible);
        }

        private float ResolveBodyTargetWeight(PlayerSwimPresentationMode mode)
        {
            switch (mode)
            {
                case PlayerSwimPresentationMode.Dry:
                    return dryBodyVisibility;

                case PlayerSwimPresentationMode.ShallowWade:
                    return shallowBodyVisibility;

                case PlayerSwimPresentationMode.SurfaceTread:
                case PlayerSwimPresentationMode.SurfaceStroke:
                    return surfaceBodyVisibility;

                case PlayerSwimPresentationMode.None:
                    return 0f;

                default:
                    return underwaterBodyVisibility;
            }
        }

        private float ResolveLookDownWeight()
        {
            if (viewmodelRoot == null)
                return 0f;

            Transform cameraRoot = viewmodelRoot.parent;
            if (cameraRoot == null)
                return 0f;

            return math.saturate(-cameraRoot.forward.y * 1.18f);
        }
    }
}
