using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Profile-driven first-person swim presentation owner.
    /// Resolves presentation mode, stroke cadence, propulsion pulse, and future viewmodel guide poses from locomotion truth.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Player Swim Presentation Controller")]
    public sealed class PlayerSwimPresentationController : MonoBehaviour, ITickable
    {
        private const float TwoPi = 6.28318530718f;
        private const float UtilitySuitMassThreshold = 120f;
        private const float HeavySuitMassThreshold = 220f;
        private const string LeftGuideName = "Swim_LeftGuide";
        private const string RightGuideName = "Swim_RightGuide";

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
        }

        [Header("-- References ----------------------------")]
        [Tooltip("Resolved movement owner. Required for locomotion truth.")]
        [SerializeField] private HectonPlayerMovement playerMovement;

        [Tooltip("Player rigidbody used for speed and acceleration sampling.")]
        [SerializeField] private Rigidbody playerRigidbody;

        [Tooltip("Optional tool owner. When a held tool is active, swim-body presentation can be reduced to avoid rig fighting.")]
        [SerializeField] private PlayerToolManager playerToolManager;

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

        [Header("-- Diagnostics ---------------------------")]
        [SerializeField] private string _debugMode = "None";
        [SerializeField] private float _debugStrokePhase;
        [SerializeField] private float _debugPropulsionPulse;
        [SerializeField] private float _debugSpeed;
        [SerializeField] private string _debugProfile;
        [SerializeField] private string _debugProfileSource;

        private bool _registered;
        private SwimPresentationProfile _activeProfile;
        private PlayerSwimPresentationMode _currentMode;
        private float _presentationBlend;
        private float _strokePhase;
        private float _propulsionPulse;
        private float _idleTimer;
        private float _previousSpeed;
        private float _turnLagYawCurrent;
        private float _turnLagRollCurrent;
        private float _toolSuppressionWeight = 1f;
        private float _currentGuideWeight;
        private float _currentLeftGuideWeight;
        private float _currentRightGuideWeight;
        private Vector3 _rootToolPositionBiasCurrent;
        private Vector3 _rootToolEulerBiasCurrent;
        private Vector3 _leftGuideToolPositionBiasCurrent;
        private Vector3 _leftGuideToolEulerBiasCurrent;
        private Vector3 _rightGuideToolPositionBiasCurrent;
        private Vector3 _rightGuideToolEulerBiasCurrent;
        private float _leftGuideVisibilityWeight = 1f;
        private float _rightGuideVisibilityWeight = 1f;
        private Vector3 _currentLocalPosition;
        private Quaternion _currentLocalRotation = Quaternion.identity;
        private Vector3 _leftGuideCurrentLocalPosition;
        private Quaternion _leftGuideCurrentLocalRotation = Quaternion.identity;
        private Vector3 _rightGuideCurrentLocalPosition;
        private Quaternion _rightGuideCurrentLocalRotation = Quaternion.identity;

        /// <summary>Current resolved swim presentation mode.</summary>
        public PlayerSwimPresentationMode CurrentMode => _currentMode;

        /// <summary>Current stroke phase in normalized 0..1 space.</summary>
        public float CurrentStrokePhase => _strokePhase;

        /// <summary>Current normalized propulsion pulse derived from the stroke cycle.</summary>
        public float CurrentPropulsionPulse => _propulsionPulse;

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

        private void Awake()
        {
            AutoResolveReferences();
            ResolveGuideReferences();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
            ResolveGuideReferences();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            AutoResolveReferences();
            ResolveGuideReferences();
        }
#endif

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (dt <= 0f)
                return;

            if (playerMovement == null || playerRigidbody == null)
            {
                AutoResolveReferences();
                if (playerMovement == null || playerRigidbody == null)
                    return;
            }

            if (viewModelRoot == null)
                ResolveGuideReferences();

            SwimPresentationProfile profile = ResolveBoundProfile();
            if (profile == null)
                return;

            Vector3 velocity = playerRigidbody.linearVelocity;
            float speed = math.length(velocity);
            float planarSpeed = math.sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
            float speedDelta = speed - _previousSpeed;
            _previousSpeed = speed;

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
            float blendT = 1f - math.exp(-presentationBlendSpeed * dt);
            _presentationBlend = math.lerp(_presentationBlend, blendTarget, blendT);

            UpdateStrokeState(profile, planarSpeed, speedDelta, dt);
            ApplyRootPose(profile, velocity, speedDelta, dt);
            ApplyGuidePoses(profile, planarSpeed, velocity);
            UpdateDiagnostics(profile, speed);
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager == null)
                return;

            gameTickManager.Register(this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager != null)
                gameTickManager.Unregister(this);

            _registered = false;
        }

        private void AutoResolveReferences()
        {
            if (playerMovement == null)
                gameObject.TryGetComponent(out playerMovement);

            if (playerRigidbody == null)
                gameObject.TryGetComponent(out playerRigidbody);

            if (playerToolManager == null)
                gameObject.TryGetComponent(out playerToolManager);
        }

        private void ResolveGuideReferences()
        {
            if (viewModelRoot == null && playerMovement != null)
            {
                Transform rootTransform = playerMovement.transform;
                if (rootTransform != null)
                    viewModelRoot = FindTransformRecursive(rootTransform, "Swim_ViewmodelRoot");
            }

            if (viewModelRoot == null)
                return;

            if (leftHandGuide == null)
                leftHandGuide = FindChildByName(viewModelRoot, LeftGuideName);

            if (rightHandGuide == null)
                rightHandGuide = FindChildByName(viewModelRoot, RightGuideName);
        }

        private static Transform FindChildByName(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            int childCount = parent.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == childName)
                    return child;
            }

            return null;
        }

        private static Transform FindTransformRecursive(Transform parent, string transformName)
        {
            if (parent == null)
                return null;

            if (parent.name == transformName)
                return parent;

            int childCount = parent.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform match = FindTransformRecursive(parent.GetChild(i), transformName);
                if (match != null)
                    return match;
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
                    if (planarSpeed >= profile.UnderwaterSprintStartSpeed)
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
                float speedFactor = math.saturate(planarSpeed / math.max(0.01f, profile.UnderwaterSprintStartSpeed));
                cadence *= 1f + speedFactor * profile.SpeedCadenceInfluence;
                _strokePhase += cadence * dt;
                _strokePhase -= math.floor(_strokePhase);
            }
            else
            {
                _strokePhase = math.lerp(_strokePhase, 0f, 1f - math.exp(-presentationBlendSpeed * dt));
            }

            float rawCycle = math.sin(_strokePhase * TwoPi);
            float pullPulse = math.max(0f, rawCycle);
            float glidePulse = math.max(0f, -rawCycle) * profile.GlideBias;
            float accelerationBias = math.saturate(speedDelta * 0.2f);
            _propulsionPulse = math.saturate(pullPulse + accelerationBias - glidePulse * 0.35f) * _presentationBlend * _toolSuppressionWeight;
        }

        private void ApplyRootPose(
            SwimPresentationProfile profile,
            Vector3 velocity,
            float speedDelta,
            float dt)
        {
            float bodyLagDegrees = Mathf.DeltaAngle(playerMovement.BodyYaw, playerMovement.CameraYaw);
            float targetYawLag = bodyLagDegrees * bodyYawLagInfluence;
            float targetRollLag = -bodyLagDegrees * 0.12f;

            float turnLerp = 1f - math.exp(-profile.TurnLagResponse * dt);
            _turnLagYawCurrent = math.lerp(_turnLagYawCurrent, targetYawLag * profile.TurnLagYaw, turnLerp);
            _turnLagRollCurrent = math.lerp(_turnLagRollCurrent, targetRollLag * profile.TurnLagRoll, turnLerp);

            float idleSin = math.sin(_idleTimer * profile.IdleDriftFrequency * TwoPi);
            float idleCos = math.cos(_idleTimer * profile.IdleDriftFrequency * TwoPi * 0.7f);
            float strokeSin = math.sin(_strokePhase * TwoPi);
            float strokeCos = math.cos(_strokePhase * TwoPi);
            float accelKick = math.clamp(speedDelta * profile.AccelerationKickAmplitude, -profile.AccelerationKickAmplitude, profile.AccelerationKickAmplitude);
            float presentationWeight = _presentationBlend * _toolSuppressionWeight;

            Vector3 localPosition = profile.BaseLocalPosition;
            localPosition.x += idleCos * profile.IdleDriftAmplitude * 0.75f * presentationWeight;
            localPosition.y += idleSin * profile.IdleDriftAmplitude * presentationWeight;
            localPosition.y += strokeSin * profile.StrokeVerticalAmplitude * presentationWeight;
            localPosition.y -= math.saturate(math.abs(velocity.y)) * profile.InertialSinkAmplitude * presentationWeight;
            localPosition.y += velocity.y * verticalVelocityInfluence * presentationWeight;
            localPosition.z += strokeCos * profile.StrokeSurgeAmplitude * presentationWeight;
            localPosition.z -= accelKick;
            localPosition += _rootToolPositionBiasCurrent * presentationWeight;

            Vector3 localEuler = profile.BaseLocalEuler;
            localEuler.x += strokeSin * profile.StrokePitchAmplitude * presentationWeight;
            localEuler.y += _turnLagYawCurrent * presentationWeight;
            localEuler.z += strokeCos * profile.StrokeRollAmplitude * presentationWeight + _turnLagRollCurrent * presentationWeight;
            localEuler += _rootToolEulerBiasCurrent * presentationWeight;

            _currentLocalPosition = localPosition;
            _currentLocalRotation = Quaternion.Euler(localEuler);

            if (viewModelRoot != null)
                viewModelRoot.SetLocalPositionAndRotation(_currentLocalPosition, _currentLocalRotation);
        }

        private void ApplyGuidePoses(
            SwimPresentationProfile profile,
            float planarSpeed,
            Vector3 velocity)
        {
            float presentationWeight = _presentationBlend * _toolSuppressionWeight;
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
            float phase = _strokePhase + phaseOffset;
            phase -= math.floor(phase);

            float cycle = phase * TwoPi;
            float strokeSin = math.sin(cycle);
            float strokeCos = math.cos(cycle);
            float pull = math.max(0f, strokeSin);
            float recover = math.max(0f, -strokeSin);
            float sweep = strokeCos;
            float sprintTuck = _currentMode == PlayerSwimPresentationMode.UnderwaterSprint
                ? profile.SprintHandTuckDistance
                : 0f;
            float speedBias = math.saturate(planarSpeed / math.max(0.01f, profile.UnderwaterSprintStartSpeed));
            float verticalBias = math.clamp(velocity.y * 0.02f, -0.03f, 0.03f);
            float verticalPose = math.clamp(velocity.y * 0.18f * verticalPoseInfluence, -1f, 1f);
            float ascendBias = math.max(0f, verticalPose);
            float descendBias = math.max(0f, -verticalPose);
            Vector3 toolPositionBias = isLeft ? _leftGuideToolPositionBiasCurrent : _rightGuideToolPositionBiasCurrent;
            Vector3 toolEulerBias = isLeft ? _leftGuideToolEulerBiasCurrent : _rightGuideToolEulerBiasCurrent;

            Vector3 localPosition = basePosition;
            localPosition.x += sideSign * (recover * profile.HandOutwardDistance * outwardScale);
            localPosition.x += sideSign * (sweep * profile.HandOutwardDistance * 0.35f * outwardScale * guideWeight);
            localPosition.x += sideSign * ascendBias * profile.HandOutwardDistance * 0.22f * guideWeight;
            localPosition.x -= sideSign * descendBias * profile.HandOutwardDistance * 0.18f * guideWeight;
            localPosition.y -= pull * profile.HandDownwardDistance * verticalScale;
            localPosition.y += recover * profile.HandRecoveryLift * verticalScale;
            localPosition.y += verticalBias * guideWeight;
            localPosition.y -= ascendBias * profile.HandDownwardDistance * 0.55f * guideWeight;
            localPosition.y += descendBias * profile.HandRecoveryLift * 0.24f * guideWeight;
            localPosition.z += recover * profile.HandReachDistance * reachScale;
            localPosition.z -= pull * profile.HandPullDistance * pullScale;
            localPosition.z -= sprintTuck * guideWeight;
            localPosition.z -= ascendBias * profile.HandPullDistance * ascendPullbackBias * guideWeight;
            localPosition.z += descendBias * profile.HandReachDistance * descendReachBias * guideWeight;
            localPosition += toolPositionBias * guideWeight;

            float maxForward = basePosition.z + profile.HandReachDistance * handForwardReachClamp;
            float maxRear = basePosition.z - profile.HandPullDistance * handRearReachClamp - sprintTuck;
            localPosition.z = math.clamp(localPosition.z, maxRear, maxForward);
            localPosition = Vector3.Lerp(basePosition, localPosition, guideWeight);

            Vector3 localEuler = baseEuler;
            localEuler.x += (pull - recover * 0.35f) * profile.HandPitchAmplitude * pitchScale;
            localEuler.x -= ascendBias * profile.HandPitchAmplitude * 0.32f * guideWeight;
            localEuler.x += descendBias * profile.HandPitchAmplitude * 0.2f * guideWeight;
            localEuler.y += sideSign * ((pull - recover * 0.25f) * profile.HandYawAmplitude * yawScale);
            localEuler.y += _turnLagYawCurrent * 0.18f * guideWeight;
            localEuler.y += sideSign * ascendBias * profile.HandYawAmplitude * 0.12f * guideWeight;
            localEuler.y -= sideSign * descendBias * profile.HandYawAmplitude * 0.08f * guideWeight;
            localEuler.z += sideSign * (sweep * profile.HandRollAmplitude * rollScale);
            localEuler.z += _turnLagRollCurrent * 0.22f * guideWeight;
            localEuler.z += sideSign * ascendBias * profile.HandRollAmplitude * 0.12f * guideWeight;
            localEuler.x += speedBias * profile.HandPitchAmplitude * 0.08f * guideWeight;
            localEuler += toolEulerBias * guideWeight;
            Quaternion localRotation = Quaternion.Euler(Vector3.Lerp(baseEuler, localEuler, guideWeight));

            if (isLeft)
            {
                _leftGuideCurrentLocalPosition = localPosition;
                _leftGuideCurrentLocalRotation = localRotation;
            }
            else
            {
                _rightGuideCurrentLocalPosition = localPosition;
                _rightGuideCurrentLocalRotation = localRotation;
            }

            if (guide != null)
                guide.SetLocalPositionAndRotation(localPosition, localRotation);
        }

        private void UpdateToolSuppression(float dt)
        {
            bool toolEquipped = playerToolManager != null &&
                                playerToolManager.CurrentTool != null &&
                                !playerToolManager.IsSwapping;
            PlayerToolSwimContract toolSwimContract = toolEquipped
                ? playerToolManager.CurrentToolSwimContract
                : null;
            bool toolUsing = toolEquipped &&
                             Hecton8.Input.InputManager.Instance != null &&
                             (Hecton8.Input.InputManager.Instance.IsPrimaryActionHeld ||
                              Hecton8.Input.InputManager.Instance.IsSecondaryActionHeld);

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

            float t = 1f - math.exp(-presentationBlendSpeed * dt);
            _toolSuppressionWeight = math.lerp(_toolSuppressionWeight, targetWeight, t);

            float leftTarget = toolHand == PlayerToolSwimHandedness.Left
                ? targetToolHandWeight
                : targetSupportHandWeight;
            float rightTarget = toolHand == PlayerToolSwimHandedness.Right
                ? targetToolHandWeight
                : targetSupportHandWeight;

            _leftGuideVisibilityWeight = math.lerp(_leftGuideVisibilityWeight, leftTarget, t);
            _rightGuideVisibilityWeight = math.lerp(_rightGuideVisibilityWeight, rightTarget, t);
            UpdateToolPoseBiases(toolSwimContract, toolUsing, toolHand, t);
        }

        private void UpdateToolPoseBiases(
            PlayerToolSwimContract toolSwimContract,
            bool toolUsing,
            PlayerToolSwimHandedness toolHand,
            float t)
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

            _rootToolPositionBiasCurrent = Vector3.Lerp(_rootToolPositionBiasCurrent, rootPositionTarget, t);
            _rootToolEulerBiasCurrent = Vector3.Lerp(_rootToolEulerBiasCurrent, rootEulerTarget, t);

            if (toolHand == PlayerToolSwimHandedness.Left)
            {
                _leftGuideToolPositionBiasCurrent = Vector3.Lerp(_leftGuideToolPositionBiasCurrent, toolPositionTarget, t);
                _leftGuideToolEulerBiasCurrent = Vector3.Lerp(_leftGuideToolEulerBiasCurrent, toolEulerTarget, t);
                _rightGuideToolPositionBiasCurrent = Vector3.Lerp(_rightGuideToolPositionBiasCurrent, supportPositionTarget, t);
                _rightGuideToolEulerBiasCurrent = Vector3.Lerp(_rightGuideToolEulerBiasCurrent, supportEulerTarget, t);
            }
            else
            {
                _leftGuideToolPositionBiasCurrent = Vector3.Lerp(_leftGuideToolPositionBiasCurrent, supportPositionTarget, t);
                _leftGuideToolEulerBiasCurrent = Vector3.Lerp(_leftGuideToolEulerBiasCurrent, supportEulerTarget, t);
                _rightGuideToolPositionBiasCurrent = Vector3.Lerp(_rightGuideToolPositionBiasCurrent, toolPositionTarget, t);
                _rightGuideToolEulerBiasCurrent = Vector3.Lerp(_rightGuideToolEulerBiasCurrent, toolEulerTarget, t);
            }
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
            _debugProfile = profile != null ? profile.name : "None";
            _debugProfileSource = profileLibrary != null ? "ProfileLibrary" : "PrefabFallback";
        }
    }
}
