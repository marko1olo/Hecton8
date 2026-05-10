// ============================================================================
// HECTON-8 - PhysicalHandController.cs
// Heavy-object articulation hand proxy with zero-GC finger spherecast batching.
// ============================================================================

namespace Hecton8.Interaction
{
    using System.Runtime.InteropServices;
    using Hecton8.Core;
    using Hecton8.Gameplay;
    using Hecton8.Physics;
    using Hecton8.Tools;
    using Hecton8.World;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;

    internal enum PhysicalHandGrabEndReason : byte
    {
        None = 0,
        ManualRelease = 1,
        GripBroken = 2,
        InvalidTarget = 3,
        Overweight = 4,
        Disabled = 5,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct FingerRayDefinition
    {
        public float3 LocalKnuckleOffset;
        public float3 LocalFingerDirection;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct FingerRayRuntime
    {
        public float3 Origin;
        public float3 Direction;
    }

    /// <summary>
    /// Owns the articulation-backed heavy-grab proxy and zero-GC finger-pose solve.
    /// Driven explicitly from <see cref="PhysicalInteractionHandler"/> inside FixedTick.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/Physical Hand Controller")]
    public sealed class PhysicalHandController : MonoBehaviour
    {
        private const int FingerCount = 5;
        private const int FingerSegmentsPerFinger = 3;
        private const int FingerSegmentCount = FingerCount * FingerSegmentsPerFinger;
        private const float HandMaxCarryMass = 50f;
        private const float VirtualHandMaxMass = 8f;
        private const float HeavyObjectMinimumVirtualMass = 1f;
        private const float VirtualSpringK = 300f;
        private const float VirtualSpringRootApprox = 17.320508f;
        private const float VirtualDampingScale = 1.8f;
        private const float VirtualHandMaxMassSqrtApprox = 2.828427f;
        private const float VirtualHandLagMax = 0.4f;
        private const float GripWarnDistance = 0.4f;
        private const float GripBreakDistance = 0.8f;
        private const float GripWarnDistanceSq = GripWarnDistance * GripWarnDistance;
        private const float GripBreakDistanceSq = GripBreakDistance * GripBreakDistance;
        private const float MaxDeltaVelocity = 15f;
        private const float MaxDeltaAngularVelocity = 25f;
        private const float MaxObjectVelocity = 40f;
        private const float MaxObjectAngularVelocity = 50f;
        private const float VelocityLeadFactor = 0.04f;
        private const float LinearNaturalFrequency = 12f;
        private const float AngularNaturalFrequency = 10f;
        private const float MinimumBoundsSpan = 0.05f;
        private const float HandWallRecoilMaxOffset = 0.18f;
        private const float HandWallRecoilScale = 2.0f;
        private const float HandWallRecoilDecay = 18f;
        private const float FingerCastRadius = 0.012f;
        private const float FingerCastLength = 0.09f;
        private const float FingerInterpolationSpeed = 18f;
        private const float MaxSupportedGrabMass = 500f;
        private const float MinimumDeltaTime = 0.0001f;
        private const float MaximumSafeDeltaTime = 0.02f;
        private const float RadiansPerDegree = 0.0174532925f;
        private const int SuitOverlapCapacity = 8;
        private const float HeavyTwoHandMassThreshold = 20f;
        private const float TwoHandReleaseAngularVelocity = 4.5f;
        private const float HapticDepthReferenceMeters = 1800f;
        private const float HandContactHapticCooldownSeconds = 0.05f;
        private const float HandDamageHapticCooldownSeconds = 0.12f;
        private const float MinimumSuitCollisionProbeRadius = 0.025f;
        private const float MaximumSuitCollisionProbeRadius = 0.18f;
        private const float MinimumSuitCrushPenetrationThreshold = 0.005f;
        private const float MaximumSuitCrushPenetrationThreshold = 0.08f;
        private const float HarvestSnapDefaultDurationSeconds = 0.18f;
        private const float HarvestSnapMaxDurationSeconds = 0.6f;
        private const float HarvestSnapSurfaceOffsetMeters = 0.035f;
        private const float XRIdleGripPoseDriftSq = 0.0004f;
        private const float DegreesPerRadian = 57.29578f;
        private const byte LeftMotorMask = 0b0001;
        private const byte RightMotorMask = 0b0010;
        private const byte HandContactHapticPriority = 2;
        private const byte CriticalHapticPriority = 3;
        private const byte CriticalHapticBlendMode = ToolHapticsRuntime.BlendModeMax;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const string InvalidMotionResetMessage = "[PhysicalHandController] NaN/Inf detected. Motion reset.";
#endif
        private const string NativeMemoryOwner = nameof(PhysicalHandController);
        private const NativeAllocationLifetime FingerNativeMemoryLifetime = NativeAllocationLifetime.Session;

        private static readonly float3 DefaultThumbKnuckleOffset = new float3(-0.028f, -0.012f, 0.018f);
        private static readonly float3 DefaultIndexKnuckleOffset = new float3(-0.015f, -0.004f, 0.034f);
        private static readonly float3 DefaultMiddleKnuckleOffset = new float3(0f, -0.002f, 0.04f);
        private static readonly float3 DefaultRingKnuckleOffset = new float3(0.015f, -0.004f, 0.034f);
        private static readonly float3 DefaultLittleKnuckleOffset = new float3(0.03f, -0.008f, 0.026f);
        private static readonly float3 DefaultThumbDirection = new float3(-0.352f, -0.050f, 0.935f);
        private static readonly float3 DefaultFingerDirection = new float3(0f, 0f, 1f);
        private static readonly float[] HapticPressureIntegrityLut =
        {
            1f, 0.93f, 0.84f, 0.73f, 0.62f, 0.51f, 0.39f, 0.28f
        }; // COLD ALLOC: float[8] - depth/integrity haptic attenuation LUT - owner: PhysicalHandController
        [Header("-- References -------------------------")]
        [Tooltip("Optional authored swim blockout rig used to source a stable right-hand attachment.")]
        [SerializeField] private PlayerSwimBlockoutRig swimBlockoutRig;

        [Tooltip("Optional explicit right-hand transform used when the blockout rig is absent.")]
        [SerializeField] private Transform rightHandAttachmentOverride;

        [Tooltip("Optional finger segment transforms. Layout: thumb/index/middle/ring/little, proximal?distal.")]
        [SerializeField] private Transform[] fingerSegments;

        [Header("-- Finger Solve -----------------------")]
        [Tooltip("Collision layers considered solid for finger spherecasts.")]
        [SerializeField] private LayerMask fingerCollisionMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Tooltip("Maximum curl angle applied to the proximal finger segment.")]
        [SerializeField, Range(10f, 100f)] private float proximalCurlDegrees = 56f;

        [Tooltip("Maximum curl angle applied to the intermediate finger segment.")]
        [SerializeField, Range(10f, 100f)] private float intermediateCurlDegrees = 68f;

        [Tooltip("Maximum curl angle applied to the distal finger segment.")]
        [SerializeField, Range(10f, 100f)] private float distalCurlDegrees = 42f;

        [Header("-- VR Somatic Safety ------------------")]
        [Tooltip("Physical hand side used for side-specific haptic routing and damage events.")]
        [SerializeField] private PhysicalHandSide handSide = PhysicalHandSide.Right;

        [Tooltip("Opt-in VR collision shell. Leave disabled for non-VR desktop rigs.")]
        [SerializeField] private bool enableSuitCollisionShell;

        [Tooltip("Layers considered crushing/scraping geometry for the physical hand shell.")]
        [SerializeField] private LayerMask suitCollisionMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Tooltip("Radius in meters for the continuous speculative hand collision shell.")]
        [SerializeField, Range(0.025f, 0.18f)] private float suitCollisionProbeRadius = 0.07f;

        [Tooltip("Penetration depth that escalates a hand contact into a suit damage event.")]
        [SerializeField, Range(0.005f, 0.08f)] private float suitCrushPenetrationThreshold = 0.025f;

        [Tooltip("Routes physical hand scraping/crush contacts into controller haptics.")]
        [SerializeField] private bool routeHandCollisionHaptics = true;

        [Tooltip("VR-only heavy mass rule. When enabled, objects over 20kg require a second stabilizing hand.")]
        [SerializeField] private bool requireTwoHandsForHeavyMass;

        [Header("-- Diagnostics -----------------------")]
#pragma warning disable CS0414
        [SerializeField] private bool _debugIsGrabbing;
        [SerializeField] private bool _debugDisconnectArmed;
        [SerializeField] private bool _debugGripBroken;
        [SerializeField] private float _debugSeparation;
        [SerializeField] private float _debugVirtualHandMass;
        [SerializeField] private string _debugGrabbedBodyName;
        [SerializeField] private bool _debugSuitContact;
        [SerializeField] private bool _debugRequiresTwoHands;
#pragma warning restore CS0414

        private HeavyCarryInteractable _activeInteractable;
        private Rigidbody _activeBody;
        private Transform _cachedTransform;
        private Transform _runtimeRoot;
        private Transform _runtimeGripPoint;
        private Transform _resolvedRightHandAttachment;
        private Transform _resolvedOpposingHandAttachment;
        private ArticulationBody _runtimeHandBody;
        private Quaternion _previousControllerRotation = Quaternion.identity;
        private Quaternion _previousTargetLocalRotation = Quaternion.identity;
        private Quaternion _grabBodyRotationOffset = Quaternion.identity;
        private Vector3 _previousControllerPosition;
        private Vector3 _virtualHandPosition;
        private Vector3 _virtualHandVelocity;
        private Vector3 _virtualHandTargetVelocity;
        private Vector3 _handWallRecoilOffset;
        private Vector3 _secondHandStabilizerPosition;
        private float _virtualHandMass;
        private float _cachedBodyDrag;
        private float _cachedBodyAngularDrag;
        private float _cachedBodyMaxAngularVelocity;
        private float _cachedBodyMaxLinearVelocity;
        private float _currentSeparationSq;
        private bool _isGrabbing;
        private bool _disconnectArmed;
        private bool _gripBroken;
        private bool _runtimeProxyCreated;
        private bool _rightHandAttachmentResolved;
        private bool _opposingHandAttachmentResolved;
        private bool _fingerSegmentsResolved;
        private bool _hasPreviousControllerPose;
        private bool _fingerPoseScheduled;
        private bool _suitCollisionShellCreated;
        private bool _twoHandStabilized = true;
        private bool _requiresTwoHandStabilization;
        private bool _hasSecondHandStabilizerPose;
        private bool _suitContactActive;
        private bool _hasXRIdleGripPoseSample;
        private float _lastFingerPoseDeltaTime = MinimumDeltaTime;
        private float _handContactHapticCooldownTimer;
        private float _handDamageHapticCooldownTimer;
        private int _lastSuitDamageFrame = -1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _suitOverlapSaturationLogged;
#endif
        private JobHandle _fingerPoseHandle;
        private JobHandle _fingerPoseDisposeHandle;

        private NativeArray<SpherecastCommand> _fingerCommands;
        private NativeArray<RaycastHit> _fingerHits;
        private NativeArray<FingerPoseData> _fingerPoses;
        private NativeArray<FingerRayDefinition> _fingerRayDefinitions;
        private NativeArray<FingerRayRuntime> _fingerRayRuntime;
        private Collider[] _suitOverlapResults;
        private Quaternion[] _baseFingerLocalRotations;
        private string _cachedGrabbedBodyName;
        private Collider _activeBodyCollider;
        private Transform _suitHandTransform;
        private Rigidbody _suitHandBody;
        private SphereCollider _suitHandCollider;
        private Transform _cachedInteractionProbeColliderSource;
        private Collider _cachedInteractionProbeCollider;
        private Vector3 _harvestSnapPosition;
        private float3 _lastXRIdleGripPosition;
        private Quaternion _harvestSnapRotation = Quaternion.identity;
        private float _harvestSnapTimer;
        private float _harvestSnapDuration;
        private bool _harvestSnapActive;

        /// <summary>True while a heavy rigidbody is actively being held by the physical hand proxy.</summary>
        public bool IsGrabbing => _isGrabbing && _activeBody != null;

        /// <summary>True when the grip auto-broke because separation exceeded the hard disconnect threshold.</summary>
        public bool GripBroken => _gripBroken;

        /// <summary>Current squared world-space separation between the virtual hand target and the grabbed body center of mass.</summary>
        public float CurrentSeparationSq => _currentSeparationSq;

        /// <summary>Current virtual hand mass used to introduce heavy-object lag.</summary>
        public float CurrentVirtualHandMass => _virtualHandMass;

        /// <summary>Authored side used as haptic fallback when the probe collider has no side tag/layer.</summary>
        public PhysicalHandSide HandSide => handSide;

        /// <summary>True while deferred finger jobs still need a dispatcher-owned late-frame completion pass.</summary>
        internal bool RequiresLateFrameTick => IsGrabbing || _fingerPoseScheduled;

        /// <summary>True when the grabbed mass exceeds the VR two-hand stabilization threshold.</summary>
        public bool RequiresTwoHandStabilization => _requiresTwoHandStabilization;

        /// <summary>
        /// Sets whether another physical hand is stabilizing the current heavy grab.
        /// </summary>
        public void SetTwoHandStabilized(bool stabilized)
        {
            _twoHandStabilized = stabilized || !_requiresTwoHandStabilization;
            _hasSecondHandStabilizerPose = false;
        }

        /// <summary>
        /// Sets the second hand pose used to damp angular velocity while stabilizing a heavy payload.
        /// </summary>
        public void SetTwoHandStabilizerPose(bool stabilized, Vector3 stabilizerWorldPosition)
        {
            _twoHandStabilized = stabilized || !_requiresTwoHandStabilization;
            _hasSecondHandStabilizerPose = stabilized && IsFinite(stabilizerWorldPosition);
            if (_hasSecondHandStabilizerPose)
                _secondHandStabilizerPosition = stabilizerWorldPosition;
        }

        /// <summary>
        /// Enables or disables the VR hand collision shell without affecting desktop carry logic.
        /// </summary>
        public void SetSuitCollisionShellEnabled(bool enabled)
        {
            enableSuitCollisionShell = enabled;
            if (!enabled && _suitHandCollider != null)
                _suitHandCollider.enabled = false;
        }

        /// <summary>
        /// Starts a short one-shot hand pose latch for a flora pick animation.
        /// </summary>
        public bool TryBeginHarvestSnap(in FloraHarvestInteractionPoint interactionPoint, float durationSeconds = HarvestSnapDefaultDurationSeconds)
        {
            if (IsGrabbing || !IsFinite(interactionPoint.RuntimePosition))
                return false;

            Vector3 normal = interactionPoint.SurfaceNormal;
            if (!IsFinite(normal) || normal.sqrMagnitude <= 0.000001f)
                normal = Vector3.up;
            else
                normal = NormalizeVectorApproxNoSqrt(normal, Vector3.up);

            Quaternion fallbackRotation = Quaternion.identity;
            Transform attachment = ResolveRightHandAttachment();
            if (attachment != null)
                fallbackRotation = attachment.rotation;
            else if (_runtimeGripPoint != null)
                fallbackRotation = _runtimeGripPoint.rotation;

            Quaternion snapRotation = ResolveHarvestSnapRotation(normal, fallbackRotation);
            if (!IsFinite(snapRotation))
                snapRotation = fallbackRotation;

            _harvestSnapPosition = interactionPoint.RuntimePosition + normal * HarvestSnapSurfaceOffsetMeters;
            _harvestSnapRotation = snapRotation;
            _harvestSnapDuration = ResolveHarvestSnapDuration(durationSeconds);
            _harvestSnapTimer = _harvestSnapDuration;
            _harvestSnapActive = true;
            return true;
        }

        /// <summary>
        /// Cancels the transient flora pick latch without changing grab state.
        /// </summary>
        public void CancelHarvestSnap()
        {
            _harvestSnapActive = false;
            _harvestSnapTimer = 0f;
            _harvestSnapDuration = 0f;
        }

        /// <summary>
        /// Returns the current flora pick target pose for animation layers that pull from this controller.
        /// </summary>
        public bool TryGetHarvestSnapPose(out Vector3 position, out Quaternion rotation, out float blend)
        {
            if (!_harvestSnapActive)
            {
                position = default;
                rotation = default;
                blend = 0f;
                return false;
            }

            position = _harvestSnapPosition;
            rotation = _harvestSnapRotation;
            blend = ResolveHarvestSnapBlend();
            return true;
        }

        /// <summary>
        /// Resolves the current physical hand probe used by collider-driven diegetic controls.
        /// </summary>
        /// <param name="position">World-space hand probe position.</param>
        /// <param name="rotation">World-space hand probe rotation.</param>
        /// <returns>True when a valid authored or runtime hand probe exists.</returns>
        public bool TryGetInteractionProbePose(out Vector3 position, out Quaternion rotation)
        {
            if (TryGetHarvestSnapPose(out position, out rotation, out float snapBlend) && snapBlend > 0.0001f)
                return true;

            Transform attachment = ResolveRightHandAttachment();
            if (attachment != null)
            {
                position = attachment.position;
                rotation = attachment.rotation;
                return true;
            }

            if (_runtimeGripPoint != null)
            {
                position = _runtimeGripPoint.position;
                rotation = _runtimeGripPoint.rotation;
                return true;
            }

            position = default;
            rotation = default;
            return false;
        }

        /// <summary>
        /// Resolves the collider that represents the physical hand probe for side-specific haptics.
        /// </summary>
        public bool TryGetInteractionProbeCollider(out Collider sourceCollider)
        {
            if (_suitHandCollider != null)
            {
                sourceCollider = _suitHandCollider;
                return true;
            }

            Transform attachment = ResolveRightHandAttachment();
            if (attachment == null)
            {
                sourceCollider = null;
                _cachedInteractionProbeColliderSource = null;
                _cachedInteractionProbeCollider = null;
                return false;
            }

            if (!ReferenceEquals(_cachedInteractionProbeColliderSource, attachment))
            {
                _cachedInteractionProbeColliderSource = attachment;
                attachment.TryGetComponent(out _cachedInteractionProbeCollider);
            }

            sourceCollider = _cachedInteractionProbeCollider;
            return sourceCollider != null;
        }

        /// <summary>
        /// Attempts to begin a heavy-object grab session.
        /// </summary>
        /// <param name="interactable">Owning interactable marker.</param>
        /// <param name="body">Target rigidbody.</param>
        /// <returns>True when the hand controller accepted the grab.</returns>
        public bool BeginGrab(HeavyCarryInteractable interactable, Rigidbody body)
        {
            if (interactable == null || body == null || body.isKinematic)
                return false;

            float bodyMass = body.mass;
            if (!math.isfinite(bodyMass) || bodyMass <= 0f || bodyMass > MaxSupportedGrabMass)
                return false;

            if (!IsFinite(body.worldCenterOfMass) || !IsFinite(body.rotation))
                return false;

            if (IsGrabbing)
                EndGrab(PhysicalHandGrabEndReason.ManualRelease);

            CancelHarvestSnap();
            EnsureRuntimeProxy();
            ResolveFingerSegments();

            _activeInteractable = interactable;
            _activeBody = body;
            _activeBodyCollider = null;
            body.TryGetComponent(out _activeBodyCollider);
            _cachedBodyDrag = body.linearDamping;
            _cachedBodyAngularDrag = body.angularDamping;
            _cachedBodyMaxAngularVelocity = body.maxAngularVelocity;
            _cachedBodyMaxLinearVelocity = body.maxLinearVelocity;
            _virtualHandMass = ResolveVirtualHandMass(bodyMass);
            _virtualHandPosition = body.worldCenterOfMass;
            _virtualHandVelocity = Vector3.zero;
            _virtualHandTargetVelocity = Vector3.zero;
            _previousControllerPosition = _virtualHandPosition;
            _previousControllerRotation = _runtimeGripPoint != null ? _runtimeGripPoint.rotation : Quaternion.identity;
            Quaternion handRotation = _runtimeGripPoint != null ? _runtimeGripPoint.rotation : Quaternion.identity;
            _grabBodyRotationOffset = ResolveGrabRotationOffset(handRotation, body.rotation);
            _previousTargetLocalRotation = body.rotation;
            _currentSeparationSq = 0f;
            _disconnectArmed = false;
            _gripBroken = false;
            _isGrabbing = true;
            _hasPreviousControllerPose = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _cachedGrabbedBodyName = body.name;
#endif
            _requiresTwoHandStabilization = requireTwoHandsForHeavyMass && bodyMass > HeavyTwoHandMassThreshold;
            _twoHandStabilized = !_requiresTwoHandStabilization;

            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.maxAngularVelocity = MaxObjectAngularVelocity;
            body.maxLinearVelocity = MaxObjectVelocity;

            if (_runtimeRoot != null)
                _runtimeRoot.position = _virtualHandPosition;

            interactable.SetDraggedState(true);
            SyncDebugState();
            return true;
        }

        /// <summary>
        /// Ends the current grab, restoring the grabbed rigidbody defaults.
        /// </summary>
        /// <param name="reason">Explicit release reason.</param>
        internal void EndGrab(PhysicalHandGrabEndReason reason)
        {
            if (_activeBody != null)
            {
                _activeBody.linearDamping = _cachedBodyDrag;
                _activeBody.angularDamping = _cachedBodyAngularDrag;
                _activeBody.maxAngularVelocity = _cachedBodyMaxAngularVelocity;
                _activeBody.maxLinearVelocity = _cachedBodyMaxLinearVelocity;

                if (reason == PhysicalHandGrabEndReason.GripBroken)
                {
                    Vector3 clampedVelocity = ClampPerAxis(_activeBody.linearVelocity, 8f);
                    _activeBody.linearVelocity = IsFinite(clampedVelocity) ? clampedVelocity : Vector3.zero;
                    _activeBody.angularVelocity = Vector3.zero;
                }
            }

            if (_activeInteractable != null)
                _activeInteractable.SetDraggedState(false);

            _activeInteractable = null;
            _activeBody = null;
            _activeBodyCollider = null;
            _grabBodyRotationOffset = Quaternion.identity;
            _disconnectArmed = false;
            _isGrabbing = false;
            _virtualHandMass = 0f;
            _virtualHandVelocity = Vector3.zero;
            _virtualHandTargetVelocity = Vector3.zero;
            _handWallRecoilOffset = Vector3.zero;
            _currentSeparationSq = 0f;
            _hasPreviousControllerPose = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _cachedGrabbedBodyName = null;
#endif
            _requiresTwoHandStabilization = false;
            _twoHandStabilized = true;
            _hasSecondHandStabilizerPose = false;
            SyncDebugState();
        }

        /// <summary>
        /// Executes the fixed-step hand solve. Must be called every physics tick by the owner.
        /// </summary>
        /// <param name="fixedDeltaTime">Authoritative fixed-step delta.</param>
        /// <param name="controllerPosition">Desired world-space hand target.</param>
        /// <param name="controllerRotation">Desired world-space hand rotation.</param>
        public void StepFixed(float fixedDeltaTime, Vector3 controllerPosition, Quaternion controllerRotation)
        {
            float dt = SanitizeFixedDeltaSeconds(fixedDeltaTime);
            _lastFingerPoseDeltaTime = dt;
            AdvanceHandHapticCooldowns(dt);
            AdvanceHarvestSnap(dt);
            if (ShouldBypassXRHandKinematicUpdate())
            {
                DecayWallRecoilOffset(dt);
                ApplyOpenHandPose(dt);
                return;
            }

            ApplyHarvestSnapPose(ref controllerPosition, ref controllerRotation);

            if (IsFinite(controllerPosition) && IsFinite(controllerRotation))
                StepSuitCollisionShell(controllerPosition, controllerRotation, dt);

            if (!IsGrabbing)
            {
                DecayWallRecoilOffset(dt);
                ApplyOpenHandPose(dt);
                return;
            }

            if (_activeBody == null || _activeBody.isKinematic)
            {
                BreakGrip(PhysicalHandGrabEndReason.InvalidTarget);
                ApplyOpenHandPose(dt);
                return;
            }

            if (!IsFinite(controllerPosition) || !IsFinite(controllerRotation))
            {
                EmergencyResetGrabbedBodyMotion(_activeBody);
                BreakGrip(PhysicalHandGrabEndReason.InvalidTarget);
                ApplyOpenHandPose(dt);
                return;
            }

            if (!_runtimeProxyCreated || _runtimeGripPoint == null)
            {
                BreakGrip(PhysicalHandGrabEndReason.InvalidTarget);
                ApplyOpenHandPose(dt);
                return;
            }

            UpdateVirtualHandPose(controllerPosition, dt);
            UpdateArticulationTarget(controllerRotation, dt);
            SolveGrabbedBody(dt);

            if (IsGrabbing)
            {
                ScheduleFingerPoseBatch();
                FinalizeControllerPoseState(controllerPosition, controllerRotation);
            }
        }

        internal void LateFrameTick()
        {
            CompleteScheduledFingerPose(_lastFingerPoseDeltaTime);
        }

        private bool ShouldBypassXRHandKinematicUpdate()
        {
            if (!HectonXRRuntimeState.IsXRActive)
                return false;

            InputDispatcher dispatcher = InputDispatcher.ActiveRuntimeInstance;
            if (dispatcher == null)
            {
                _hasXRIdleGripPoseSample = false;
                return true;
            }

            byte controllerIndex = handSide == PhysicalHandSide.Left ? (byte)0 : (byte)1;
            if (!dispatcher.TryGetXRInputState(controllerIndex, out XRInputState state))
            {
                _hasXRIdleGripPoseSample = false;
                return true;
            }

            if (state.IsTracked == 0)
            {
                _hasXRIdleGripPoseSample = false;
                return true;
            }

            if (state.HasActiveInput || IsGrabbing || _harvestSnapActive)
            {
                _hasXRIdleGripPoseSample = false;
                return false;
            }

            if (enableSuitCollisionShell || _suitContactActive)
                return false;

            float3 gripPosition = state.GripPositionWS;
            if (!math.all(math.isfinite(gripPosition)))
            {
                _hasXRIdleGripPoseSample = false;
                return true;
            }

            if (!_hasXRIdleGripPoseSample)
            {
                _lastXRIdleGripPosition = gripPosition;
                _hasXRIdleGripPoseSample = true;
                return false;
            }

            float driftSq = math.lengthsq(gripPosition - _lastXRIdleGripPosition);
            _lastXRIdleGripPosition = gripPosition;
            if (!math.isfinite(driftSq))
            {
                _hasXRIdleGripPoseSample = false;
                return false;
            }

            return driftSq <= XRIdleGripPoseDriftSq;
        }

        private void Awake()
        {
            _cachedTransform = transform;
            ResolveSwimBlockoutRig();
            EnsureRuntimeProxy();
            ResolveOpposingHandAttachment();
            if (HectonXRRuntimeState.IsXRActive)
                AllocatePersistentBuffers();
            ResolveFingerSegments();
            if (enableSuitCollisionShell)
                EnsureSuitCollisionShell();
            SyncDebugState();
        }

        private void OnEnable()
        {
            if (HectonXRRuntimeState.IsXRActive)
                AllocatePersistentBuffers();
            if (enableSuitCollisionShell)
                EnsureSuitCollisionShell();
        }

        private void OnDisable()
        {
            CancelHarvestSnap();
            _cachedInteractionProbeColliderSource = null;
            _cachedInteractionProbeCollider = null;
            _hasXRIdleGripPoseSample = false;
            DisableSuitCollisionShell();
            if (IsGrabbing)
                EndGrab(PhysicalHandGrabEndReason.Disabled);

            DisposePersistentBuffers();
        }

        private void OnDestroy()
        {
            DisposePersistentBuffers();
            DisableSuitCollisionShell();
            if (_suitHandTransform != null)
                Destroy(_suitHandTransform.gameObject);

            _suitHandTransform = null;
            _suitHandBody = null;
            _suitHandCollider = null;
            _suitCollisionShellCreated = false;
            _suitOverlapResults = null;
            _suitOverlapSaturationLogged = false;

            if (_runtimeRoot != null)
                Destroy(_runtimeRoot.gameObject);

            _runtimeRoot = null;
            _runtimeGripPoint = null;
            _runtimeHandBody = null;
            _runtimeProxyCreated = false;
        }

        private void EnsureRuntimeProxy()
        {
            if (_runtimeProxyCreated)
                return;

            // COLD ALLOC: GameObject[1] — persistent articulation root for physical hand velocity drive — owner: PhysicalHandController
            GameObject rootObject = new GameObject("[PhysicalHandRuntimeRoot]");
            rootObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
            _runtimeRoot = rootObject.transform;
            Transform initialAttachment = ResolveRightHandAttachment();
            _runtimeRoot.position = initialAttachment != null ? initialAttachment.position : _cachedTransform.position;
            _runtimeRoot.rotation = Quaternion.identity;
            ArticulationBody runtimeRootBody = rootObject.AddComponent<ArticulationBody>();
            runtimeRootBody.immovable = true;
            runtimeRootBody.useGravity = false;
            runtimeRootBody.linearDamping = 0f;
            runtimeRootBody.angularDamping = 0f;

            // COLD ALLOC: GameObject[1] — persistent articulation joint proxy for physical hand velocity drive — owner: PhysicalHandController
            GameObject handObject = new GameObject("[PhysicalHandRuntimeJoint]");
            handObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
            Transform handTransform = handObject.transform;
            handTransform.SetParent(_runtimeRoot, false);
            handTransform.localPosition = Vector3.zero;
            handTransform.localRotation = Quaternion.identity;

            _runtimeHandBody = handObject.AddComponent<ArticulationBody>();
            _runtimeHandBody.jointType = ArticulationJointType.SphericalJoint;
            _runtimeHandBody.useGravity = false;
            _runtimeHandBody.mass = 1f;
            _runtimeHandBody.linearDamping = 0f;
            _runtimeHandBody.angularDamping = 0f;
            _runtimeHandBody.matchAnchors = true;
            _runtimeHandBody.anchorPosition = Vector3.zero;
            _runtimeHandBody.parentAnchorPosition = Vector3.zero;
            _runtimeHandBody.anchorRotation = Quaternion.identity;
            _runtimeHandBody.parentAnchorRotation = Quaternion.identity;
            _runtimeHandBody.twistLock = ArticulationDofLock.LimitedMotion;
            _runtimeHandBody.swingYLock = ArticulationDofLock.LimitedMotion;
            _runtimeHandBody.swingZLock = ArticulationDofLock.LimitedMotion;
            _runtimeHandBody.maxAngularVelocity = MaxObjectAngularVelocity;

            ArticulationDrive baseDrive = default;
            baseDrive.lowerLimit = -180f;
            baseDrive.upperLimit = 180f;
            baseDrive.stiffness = 24000f;
            baseDrive.damping = 4200f;
            baseDrive.forceLimit = 15000f;
            baseDrive.target = 0f;
            baseDrive.targetVelocity = 0f;
            _runtimeHandBody.xDrive = baseDrive;
            _runtimeHandBody.yDrive = baseDrive;
            _runtimeHandBody.zDrive = baseDrive;

            _runtimeGripPoint = handTransform;
            _runtimeProxyCreated = true;
        }

        private void EnsureSuitCollisionShell()
        {
            EnsureRuntimeProxy();

            if (_suitOverlapResults == null || _suitOverlapResults.Length != SuitOverlapCapacity)
            {
                // COLD ALLOC: Collider[8] - zero-GC physical hand suit contact overlap buffer - owner: PhysicalHandController
                _suitOverlapResults = new Collider[SuitOverlapCapacity];
            }

            if (_suitCollisionShellCreated)
            {
                if (_suitHandCollider != null)
                {
                    _suitHandCollider.radius = ResolveSuitCollisionProbeRadius();
                    _suitHandCollider.enabled = enableSuitCollisionShell;
                }

                return;
            }

            // COLD ALLOC: GameObject[1] - optional VR physical hand suit trigger shell - owner: PhysicalHandController
            GameObject shellObject = new GameObject("[PhysicalHandSuitCollisionShell]");
            shellObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
            shellObject.layer = gameObject.layer;

            _suitHandTransform = shellObject.transform;
            Transform parent = _runtimeGripPoint != null ? _runtimeGripPoint : _runtimeRoot;
            _suitHandTransform.position = parent != null ? parent.position : _cachedTransform.position;
            _suitHandTransform.rotation = parent != null ? parent.rotation : _cachedTransform.rotation;

            _suitHandBody = shellObject.AddComponent<Rigidbody>();
            _suitHandBody.isKinematic = true;
            _suitHandBody.useGravity = false;
            _suitHandBody.detectCollisions = true;
            _suitHandBody.interpolation = RigidbodyInterpolation.Interpolate;
            _suitHandBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _suitHandBody.maxDepenetrationVelocity = 3f;

            _suitHandCollider = shellObject.AddComponent<SphereCollider>();
            _suitHandCollider.isTrigger = false;
            _suitHandCollider.radius = ResolveSuitCollisionProbeRadius();
            _suitHandCollider.enabled = enableSuitCollisionShell;

            _suitCollisionShellCreated = true;
        }

        private void DisableSuitCollisionShell()
        {
            bool disabledShell = false;
            if (_suitHandCollider != null && _suitHandCollider.enabled)
            {
                _suitHandCollider.enabled = false;
                disabledShell = true;
            }

            if (!_suitContactActive && !disabledShell && !_suitOverlapSaturationLogged)
                return;

            _suitContactActive = false;
            _suitOverlapSaturationLogged = false;
            ClearSuitOverlapResults();
        }

        private void ClearSuitOverlapResults()
        {
            if (_suitOverlapResults == null)
                return;

            for (int i = 0; i < _suitOverlapResults.Length; i++)
                _suitOverlapResults[i] = null;
        }

        private void AdvanceHandHapticCooldowns(float dt)
        {
            if (dt <= 0f)
                return;

            if (_handContactHapticCooldownTimer > 0f)
                _handContactHapticCooldownTimer = math.max(0f, _handContactHapticCooldownTimer - dt);
            if (_handDamageHapticCooldownTimer > 0f)
                _handDamageHapticCooldownTimer = math.max(0f, _handDamageHapticCooldownTimer - dt);
        }

        private void StepSuitCollisionShell(Vector3 controllerPosition, Quaternion controllerRotation, float dt)
        {
            if (!enableSuitCollisionShell)
            {
                DisableSuitCollisionShell();
                return;
            }

            if (!_suitCollisionShellCreated)
            {
                DisableSuitCollisionShell();
                return;
            }

            if (_suitOverlapResults == null || _suitOverlapResults.Length == 0 || suitCollisionMask.value == 0)
            {
                DisableSuitCollisionShell();
                return;
            }

            float radius = ResolveSuitCollisionProbeRadius();
            Transform shellTransform = _suitHandTransform;
            if (shellTransform == null && _suitHandBody != null)
                shellTransform = _suitHandBody.transform;
            if (shellTransform != null)
                shellTransform.SetPositionAndRotation(controllerPosition, controllerRotation);
            if (_suitHandCollider != null)
            {
                _suitHandCollider.radius = radius;
                _suitHandCollider.enabled = true;
            }

            int hitCount = global::UnityEngine.Physics.OverlapSphereNonAlloc(
                controllerPosition,
                radius,
                _suitOverlapResults,
                suitCollisionMask.value,
                QueryTriggerInteraction.Ignore);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (hitCount >= _suitOverlapResults.Length && !_suitOverlapSaturationLogged)
            {
                _suitOverlapSaturationLogged = true;
                Debug.LogWarning(
                    "[PhysicalHandController] Suit collision shell overlap buffer saturated. " +
                    "Increase SuitOverlapCapacity or narrow suitCollisionMask.", this);
            }
#endif

            float maxPenetration = 0f;
            Vector3 contactPoint = controllerPosition;
            Vector3 contactNormal = Vector3.up;
            float3 wallRecoilOffset = float3.zero;
            int sourceColliderInstanceId = 0;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _suitOverlapResults[i];
                _suitOverlapResults[i] = null;
                if (hit == null ||
                    !hit.enabled ||
                    ReferenceEquals(hit, _suitHandCollider) ||
                    (_activeBody != null && ReferenceEquals(hit.attachedRigidbody, _activeBody)))
                {
                    continue;
                }

                Bounds hitBounds = hit.bounds;
                if (!IsFinite(hitBounds.center) || !IsFinite(hitBounds.extents))
                    continue;

                float3 deltaFromCenter = (float3)(controllerPosition - hitBounds.center);
                float3 axisPenetration = (float3)hitBounds.extents + new float3(radius) - math.abs(deltaFromCenter);
                float penetration = math.cmin(axisPenetration);
                if (!math.isfinite(penetration) || penetration <= 0f)
                    continue;

                float3 normalAxis = ResolveDominantAxisNormal(deltaFromCenter, axisPenetration);
                Vector3 normal = (Vector3)normalAxis;
                Vector3 contact = controllerPosition - (Vector3)(normalAxis * radius);

                if (penetration <= maxPenetration)
                    continue;

                maxPenetration = penetration;
                contactPoint = contact;
                contactNormal = normal;
                wallRecoilOffset = normalAxis * math.min(HandWallRecoilMaxOffset, penetration * HandWallRecoilScale);
                sourceColliderInstanceId = unchecked((int)EntityId.ToULong(hit.GetEntityId()));
            }

            _suitContactActive = maxPenetration > 0f;
            if (!_suitContactActive)
                return;

            ApplyWallRecoilOffset((Vector3)wallRecoilOffset);

            float crushThreshold = ResolveSuitCrushPenetrationThreshold();
            float pressure01 = math.saturate(maxPenetration / crushThreshold);
            if (!math.isfinite(pressure01))
                return;

            float hapticScale = ResolveSuitCollisionHapticScale(pressure01);
            if (routeHandCollisionHaptics && _handContactHapticCooldownTimer <= 0f)
            {
                byte motorMask = handSide == PhysicalHandSide.Left ? LeftMotorMask : RightMotorMask;
                float lowIntensity = handSide == PhysicalHandSide.Left ? hapticScale : hapticScale * 0.45f;
                float highIntensity = handSide == PhysicalHandSide.Right ? hapticScale : hapticScale * 0.45f;
                ToolHapticsRuntime.EnqueueCommand(
                    lowIntensity,
                    highIntensity,
                    0.08f,
                    7.5f,
                    HandContactHapticPriority,
                    motorMask,
                    CriticalHapticBlendMode);
                PhysicsEventBus.NotifyAcousticImpulse(new AcousticImpulseEvent(
                    contactPoint,
                    contactNormal,
                    math.lerp(4f, 28f, pressure01),
                    math.saturate(0.12f + pressure01 * 0.38f),
                    math.lerp(1.35f, 2.1f, pressure01),
                    math.lerp(0.2f, 0.6f, pressure01),
                    sourceColliderInstanceId,
                    0,
                    AcousticImpulseFlags.PlayerCollision));
                _handContactHapticCooldownTimer = HandContactHapticCooldownSeconds;
            }

            int frame = Time.frameCount;
            if (pressure01 < 1f ||
                frame == _lastSuitDamageFrame ||
                _handDamageHapticCooldownTimer > 0f)
            {
                return;
            }

            _lastSuitDamageFrame = frame;
            _handDamageHapticCooldownTimer = HandDamageHapticCooldownSeconds;
            AbsoluteUniversePosition contactAup = ResolveSuitContactAup(contactPoint, controllerPosition);
            SuitDamageEvent damageEvent = new SuitDamageEvent(
                handSide,
                contactAup,
                contactNormal,
                pressure01,
                sourceColliderInstanceId,
                (uint)frame);
            SuitDamageEvents.Publish(in damageEvent);
            PhysicsEventBus.NotifyAcousticImpulse(new AcousticImpulseEvent(
                contactPoint,
                contactNormal,
                math.lerp(35f, 180f, pressure01),
                math.saturate(0.35f + pressure01 * 0.65f),
                0.75f,
                0.85f,
                sourceColliderInstanceId,
                0,
                AcousticImpulseFlags.PlayerCollision | AcousticImpulseFlags.Critical));
        }

        private void AllocatePersistentBuffers()
        {
            DispatcherJobSwap.TryFinalizeCompleted(ref _fingerPoseDisposeHandle);
            if (!_fingerPoseDisposeHandle.IsCompleted)
                return;

            if (_fingerCommands.IsCreated)
                return;

            // COLD ALLOC: NativeArray<SpherecastCommand>[5] - persistent finger spherecast commands - owner: PhysicalHandController
            _fingerCommands = new NativeArray<SpherecastCommand>(FingerCount, Allocator.Persistent);
            NativeMemorySentinel.RegisterNativeArray(_fingerCommands, NativeMemoryOwner, nameof(_fingerCommands), FingerNativeMemoryLifetime);
            // COLD ALLOC: NativeArray<RaycastHit>[5] - persistent finger spherecast results - owner: PhysicalHandController
            _fingerHits = new NativeArray<RaycastHit>(FingerCount, Allocator.Persistent);
            NativeMemorySentinel.RegisterNativeArray(_fingerHits, NativeMemoryOwner, nameof(_fingerHits), FingerNativeMemoryLifetime);
            // COLD ALLOC: NativeArray<FingerPoseData>[5] - persistent finger pose results - owner: PhysicalHandController
            _fingerPoses = new NativeArray<FingerPoseData>(FingerCount, Allocator.Persistent);
            NativeMemorySentinel.RegisterNativeArray(_fingerPoses, NativeMemoryOwner, nameof(_fingerPoses), FingerNativeMemoryLifetime);
            // COLD ALLOC: NativeArray<FingerRayDefinition>[5] - persistent local finger ray definitions - owner: PhysicalHandController
            _fingerRayDefinitions = new NativeArray<FingerRayDefinition>(FingerCount, Allocator.Persistent);
            NativeMemorySentinel.RegisterNativeArray(_fingerRayDefinitions, NativeMemoryOwner, nameof(_fingerRayDefinitions), FingerNativeMemoryLifetime);
            // COLD ALLOC: NativeArray<FingerRayRuntime>[5] - persistent world-space finger ray runtime data - owner: PhysicalHandController
            _fingerRayRuntime = new NativeArray<FingerRayRuntime>(FingerCount, Allocator.Persistent);
            NativeMemorySentinel.RegisterNativeArray(_fingerRayRuntime, NativeMemoryOwner, nameof(_fingerRayRuntime), FingerNativeMemoryLifetime);

            _fingerRayDefinitions[0] = new FingerRayDefinition
            {
                LocalKnuckleOffset = DefaultThumbKnuckleOffset,
                LocalFingerDirection = DefaultThumbDirection
            };
            _fingerRayDefinitions[1] = new FingerRayDefinition
            {
                LocalKnuckleOffset = DefaultIndexKnuckleOffset,
                LocalFingerDirection = DefaultFingerDirection
            };
            _fingerRayDefinitions[2] = new FingerRayDefinition
            {
                LocalKnuckleOffset = DefaultMiddleKnuckleOffset,
                LocalFingerDirection = DefaultFingerDirection
            };
            _fingerRayDefinitions[3] = new FingerRayDefinition
            {
                LocalKnuckleOffset = DefaultRingKnuckleOffset,
                LocalFingerDirection = DefaultFingerDirection
            };
            _fingerRayDefinitions[4] = new FingerRayDefinition
            {
                LocalKnuckleOffset = DefaultLittleKnuckleOffset,
                LocalFingerDirection = DefaultFingerDirection
            };
        }

        private void DisposePersistentBuffers()
        {
            DispatcherJobSwap.TryFinalizeCompleted(ref _fingerPoseDisposeHandle);
            bool hasPendingDispose = !_fingerPoseDisposeHandle.IsCompleted;
            JobHandle disposeHandle = hasPendingDispose
                ? JobHandle.CombineDependencies(_fingerPoseDisposeHandle, _fingerPoseHandle)
                : _fingerPoseHandle;
            bool scheduledDispose = false;

            DisposeNativeArray(ref _fingerCommands, ref disposeHandle, ref scheduledDispose);
            DisposeNativeArray(ref _fingerHits, ref disposeHandle, ref scheduledDispose);
            DisposeNativeArray(ref _fingerPoses, ref disposeHandle, ref scheduledDispose);
            DisposeNativeArray(ref _fingerRayDefinitions, ref disposeHandle, ref scheduledDispose);
            DisposeNativeArray(ref _fingerRayRuntime, ref disposeHandle, ref scheduledDispose);

            _fingerPoseScheduled = false;
            _fingerPoseHandle = default;
            if (!scheduledDispose)
                return;

            _fingerPoseDisposeHandle = disposeHandle;
            JobHandle.ScheduleBatchedJobs();
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, ref JobHandle disposeHandle, ref bool scheduledDispose) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            disposeHandle = array.Dispose(disposeHandle);
            array = default;
            scheduledDispose = true;
        }

        private void ResolveFingerSegments()
        {
            if (_fingerSegmentsResolved)
                return;

            int segmentCount = fingerSegments != null ? fingerSegments.Length : 0;
            if (segmentCount > 0)
            {
                // COLD ALLOC: Quaternion[15] - cached authored finger local rotations - owner: PhysicalHandController
                _baseFingerLocalRotations = new Quaternion[segmentCount];
                for (int i = 0; i < segmentCount; i++)
                {
                    Transform segment = fingerSegments[i];
                    _baseFingerLocalRotations[i] = segment != null ? segment.localRotation : Quaternion.identity;
                }
            }

            _fingerSegmentsResolved = true;
        }

        private Transform ResolveRightHandAttachment()
        {
            if (_rightHandAttachmentResolved)
                return _resolvedRightHandAttachment;

            if (rightHandAttachmentOverride != null)
            {
                _resolvedRightHandAttachment = rightHandAttachmentOverride;
                _rightHandAttachmentResolved = true;
                return _resolvedRightHandAttachment;
            }

            ResolveSwimBlockoutRig();

            if (swimBlockoutRig != null)
                _resolvedRightHandAttachment = swimBlockoutRig.RightHandAttachment;

            _rightHandAttachmentResolved = true;
            return _resolvedRightHandAttachment;
        }

        private Transform ResolveOpposingHandAttachment()
        {
            if (_opposingHandAttachmentResolved)
                return _resolvedOpposingHandAttachment;

            ResolveSwimBlockoutRig();

            if (swimBlockoutRig != null)
            {
                _resolvedOpposingHandAttachment = handSide == PhysicalHandSide.Left
                    ? swimBlockoutRig.RightHandAttachment
                    : swimBlockoutRig.LeftHandAttachment;
            }

            _opposingHandAttachmentResolved = true;
            return _resolvedOpposingHandAttachment;
        }

        private PlayerSwimBlockoutRig ResolveSwimBlockoutRig()
        {
            if (swimBlockoutRig != null)
                return swimBlockoutRig;

            Transform root = _cachedTransform != null ? _cachedTransform : transform;
            swimBlockoutRig = ComponentReferenceUtility.ResolveOwnedComponent<PlayerSwimBlockoutRig>(root);
            return swimBlockoutRig;
        }

        private void CompleteScheduledFingerPose(float dt)
        {
            if (!_fingerPoseScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _fingerPoseHandle, forceComplete: false))
                return;

            _fingerPoseScheduled = false;
            if (IsGrabbing)
                ApplyFingerPose(dt);
        }

        private void ApplyOpenHandPose(float dt)
        {
            if (fingerSegments == null || _baseFingerLocalRotations == null)
                return;

            float blendT = math.saturate(dt * FingerInterpolationSpeed);
            for (int i = 0; i < fingerSegments.Length; i++)
            {
                Transform segment = fingerSegments[i];
                if (segment == null)
                    continue;

                segment.localRotation = ApproximateNlerpNoSqrt(segment.localRotation, _baseFingerLocalRotations[i], blendT);
            }
        }

        private void ApplyFingerPose(float dt)
        {
            if (fingerSegments == null || _baseFingerLocalRotations == null)
                return;

            float blendT = math.saturate(dt * FingerInterpolationSpeed);
            for (int fingerIndex = 0; fingerIndex < FingerCount; fingerIndex++)
            {
                FingerPoseData pose = _fingerPoses[fingerIndex];
                float bendDegrees = pose.BendAngle;
                int baseIndex = fingerIndex * FingerSegmentsPerFinger;
                ApplyFingerSegment(baseIndex + 0, proximalCurlDegrees * bendDegrees, blendT);
                ApplyFingerSegment(baseIndex + 1, intermediateCurlDegrees * bendDegrees, blendT);
                ApplyFingerSegment(baseIndex + 2, distalCurlDegrees * bendDegrees, blendT);
            }
        }

        private void ApplyFingerSegment(int segmentIndex, float targetCurlDegrees, float blendT)
        {
            if (fingerSegments == null ||
                _baseFingerLocalRotations == null ||
                (uint)segmentIndex >= (uint)fingerSegments.Length ||
                (uint)segmentIndex >= (uint)_baseFingerLocalRotations.Length)
            {
                return;
            }

            Transform segment = fingerSegments[segmentIndex];
            if (segment == null)
                return;

            Quaternion targetRotation = _baseFingerLocalRotations[segmentIndex] * ApproximateLocalXRotationNoTrig(-targetCurlDegrees);
            segment.localRotation = ApproximateNlerpNoSqrt(segment.localRotation, targetRotation, blendT);
        }

        private void UpdateVirtualHandPose(Vector3 controllerPosition, float dt)
        {
            Vector3 controllerVelocity = Vector3.zero;
            if (_hasPreviousControllerPose)
                controllerVelocity = (controllerPosition - _previousControllerPosition) / dt;
            if (!IsFinite(controllerVelocity))
                controllerVelocity = Vector3.zero;

            Vector3 effectiveControllerPosition = controllerPosition;
            if (_handWallRecoilOffset.sqrMagnitude > 0.0000001f)
            {
                effectiveControllerPosition += _handWallRecoilOffset;
                DecayWallRecoilOffset(dt);
            }

            bool useVirtualMassLag = _activeBody != null && _activeBody.mass > HandMaxCarryMass && _virtualHandMass > MinimumDeltaTime;
            if (useVirtualMassLag)
                effectiveControllerPosition += controllerVelocity * VelocityLeadFactor;

            Vector3 previousVirtualHandPosition = _virtualHandPosition;
            if (useVirtualMassLag)
            {
                float damping = ResolveVirtualHandDamping(_virtualHandMass);
                Vector3 springForce = (effectiveControllerPosition - _virtualHandPosition) * VirtualSpringK;
                Vector3 dampingForce = -_virtualHandVelocity * damping;
                Vector3 netForce = springForce + dampingForce;
                if (!IsFinite(netForce))
                    netForce = Vector3.zero;

                Vector3 netAcceleration = netForce / math.max(_virtualHandMass, MinimumDeltaTime);
                if (!IsFinite(netAcceleration))
                    netAcceleration = Vector3.zero;

                _virtualHandVelocity += netAcceleration * dt;
                _virtualHandPosition += _virtualHandVelocity * dt;

                Vector3 lag = _virtualHandPosition - controllerPosition;
                float3 clampedLag = math.clamp((float3)lag, new float3(-VirtualHandLagMax), new float3(VirtualHandLagMax));
                _virtualHandPosition = controllerPosition + (Vector3)clampedLag;
                _virtualHandTargetVelocity = (_virtualHandPosition - previousVirtualHandPosition) / dt;
                _virtualHandVelocity = IsFinite(_virtualHandTargetVelocity) ? _virtualHandTargetVelocity : Vector3.zero;
            }
            else
            {
                _virtualHandPosition = effectiveControllerPosition;
                _virtualHandVelocity = controllerVelocity;
                _virtualHandTargetVelocity = controllerVelocity;
            }

            if (_runtimeRoot != null)
            {
                _runtimeRoot.position = _virtualHandPosition;
                _runtimeRoot.rotation = Quaternion.identity;
            }
        }

        private void UpdateArticulationTarget(Quaternion controllerRotation, float dt)
        {
            if (_runtimeHandBody == null)
                return;

            Quaternion localTargetRotation = controllerRotation;
            Vector3 targetReducedDegrees = ResolveApproxAngularVectorRadians(localTargetRotation) * DegreesPerRadian;
            Vector3 angularVelocityDegrees = Vector3.zero;
            if (_hasPreviousControllerPose)
            {
                Quaternion delta = controllerRotation * Quaternion.Inverse(_previousControllerRotation);
                Vector3 angularDeltaRadians = ResolveApproxAngularVectorRadians(delta);
                angularVelocityDegrees = angularDeltaRadians * (DegreesPerRadian / dt);
            }

            ArticulationDrive xDrive = _runtimeHandBody.xDrive;
            ArticulationDrive yDrive = _runtimeHandBody.yDrive;
            ArticulationDrive zDrive = _runtimeHandBody.zDrive;
            xDrive.target = targetReducedDegrees.x;
            xDrive.targetVelocity = angularVelocityDegrees.x;
            yDrive.target = targetReducedDegrees.y;
            yDrive.targetVelocity = angularVelocityDegrees.y;
            zDrive.target = targetReducedDegrees.z;
            zDrive.targetVelocity = angularVelocityDegrees.z;
            _runtimeHandBody.xDrive = xDrive;
            _runtimeHandBody.yDrive = yDrive;
            _runtimeHandBody.zDrive = zDrive;
        }

        private void SolveGrabbedBody(float dt)
        {
            Rigidbody body = _activeBody;
            if (body == null)
                return;

            Vector3 handPosition = _runtimeGripPoint != null ? _runtimeGripPoint.position : _virtualHandPosition;
            Quaternion handRotation = _runtimeGripPoint != null ? _runtimeGripPoint.rotation : Quaternion.identity;
            if (!IsFinite(handPosition) || !IsFinite(handRotation) || !IsFinite(body.rotation))
            {
                EmergencyResetGrabbedBodyMotion(body);
                BreakGrip(PhysicalHandGrabEndReason.InvalidTarget);
                return;
            }

            Vector3 bodyPosition = body.worldCenterOfMass;
            if (!IsFinite(bodyPosition))
            {
                EmergencyResetGrabbedBodyMotion(body);
                BreakGrip(PhysicalHandGrabEndReason.InvalidTarget);
                return;
            }

            Vector3 linearError = handPosition - bodyPosition;
            float separationSq = ResolveAupDistanceSqAsFloat(handPosition, bodyPosition);
            _currentSeparationSq = separationSq;

            if (separationSq > GripBreakDistanceSq)
            {
                BreakGrip(PhysicalHandGrabEndReason.GripBroken);
                return;
            }

            _disconnectArmed = separationSq > GripWarnDistanceSq;
            float gainMultiplier = 1f;
            if (_disconnectArmed)
                gainMultiplier = 1f - math.saturate((separationSq - GripWarnDistanceSq) / math.max(GripBreakDistanceSq - GripWarnDistanceSq, MinimumDeltaTime));

            Vector3 targetVelocity = IsFinite(_virtualHandTargetVelocity) ? _virtualHandTargetVelocity : Vector3.zero;
            Vector3 velocityError = targetVelocity - body.linearVelocity;
            float kp = LinearNaturalFrequency * LinearNaturalFrequency * gainMultiplier;
            float kd = 2f * LinearNaturalFrequency * gainMultiplier;
            Vector3 acceleration = (linearError * kp) + (velocityError * kd);

            if (!IsFinite(acceleration))
                acceleration = Vector3.zero;

            Vector3 deltaVelocity = ClampMagnitude(acceleration * dt, ResolveMaxDeltaVelocity(body, gainMultiplier));
            if (deltaVelocity.sqrMagnitude > 0.0000001f)
                PhysicsForceRouter.QueueForce(body, deltaVelocity, ForceMode.VelocityChange);

            Quaternion targetBodyRotation = ResolveTargetBodyRotation(handRotation);
            Quaternion deltaRotation = targetBodyRotation * Quaternion.Inverse(body.rotation);
            Vector3 angularError = ResolveApproxAngularVectorRadians(deltaRotation);
            Vector3 targetAngularVelocity = ResolveTargetAngularVelocityRadians(targetBodyRotation, dt);
            _previousTargetLocalRotation = targetBodyRotation;
            if (!IsFinite(targetAngularVelocity))
            {
                EmergencyResetGrabbedBodyMotion(body);
                BreakGrip(PhysicalHandGrabEndReason.InvalidTarget);
                return;
            }

            Vector3 angularVelocityError = targetAngularVelocity - body.angularVelocity;
            float angularKp = AngularNaturalFrequency * AngularNaturalFrequency * gainMultiplier;
            float angularKd = 2f * AngularNaturalFrequency * gainMultiplier;
            Vector3 angularAcceleration = (angularError * angularKp) + (angularVelocityError * angularKd);

            if (!IsFinite(angularAcceleration))
                angularAcceleration = Vector3.zero;

            Vector3 deltaAngularVelocity = ClampMagnitude(angularAcceleration * dt, MaxDeltaAngularVelocity * gainMultiplier);
            if (deltaAngularVelocity.sqrMagnitude > 0.0000001f)
                PhysicsForceRouter.QueueTorque(body, deltaAngularVelocity, ForceMode.VelocityChange);

            UpdateImplicitTwoHandStabilizer(body);
            ApplyTwoHandMassScaleTorque(body, handPosition, gainMultiplier, dt);
            SyncDebugState();
        }

        private void ApplyTwoHandMassScaleTorque(Rigidbody body, Vector3 handPosition, float gainMultiplier, float dt)
        {
            if (!_requiresTwoHandStabilization || body == null)
                return;

            if (_twoHandStabilized)
            {
                ApplyTwoHandAngularVelocityDamping(body, handPosition);
                return;
            }

            Vector3 gravityDirection = UnityEngine.Physics.gravity;
            if (gravityDirection.sqrMagnitude < 0.000001f)
                gravityDirection = Vector3.down;

            Quaternion bodyRotation = body.rotation;
            Vector3 bodyRight = bodyRotation * Vector3.right;
            if (!IsFinite(bodyRight) || bodyRight.sqrMagnitude <= 0.000001f)
                bodyRight = Vector3.right;

            Vector3 bodyForward = bodyRotation * Vector3.forward;
            if (!IsFinite(bodyForward) || bodyForward.sqrMagnitude <= 0.000001f)
                bodyForward = Vector3.forward;

            Vector3 lever = handPosition - body.worldCenterOfMass;
            if (lever.sqrMagnitude < 0.000001f)
                lever = bodyRight;

            float3 torqueAxis = math.cross(
                NormalizeVectorApproxNoSqrt((float3)lever, (float3)bodyRight),
                NormalizeVectorApproxNoSqrt((float3)gravityDirection, new float3(0f, -1f, 0f)));
            torqueAxis = NormalizeVectorApproxNoSqrt(torqueAxis, (float3)bodyForward);

            float load01 = math.saturate((body.mass - HeavyTwoHandMassThreshold) / math.max(MaxSupportedGrabMass - HeavyTwoHandMassThreshold, 1f));
            Vector3 deltaAngularVelocity = ClampMagnitude(
                (Vector3)(torqueAxis * (TwoHandReleaseAngularVelocity * load01 * math.max(gainMultiplier, 0.15f) * dt)),
                MaxDeltaAngularVelocity * 0.35f);

            if (deltaAngularVelocity.sqrMagnitude > 0.0000001f)
                PhysicsForceRouter.QueueTorque(body, deltaAngularVelocity, ForceMode.VelocityChange);
        }

        private void UpdateImplicitTwoHandStabilizer(Rigidbody body)
        {
            if (!_requiresTwoHandStabilization || body == null)
                return;

            Transform opposingHand = ResolveOpposingHandAttachment();
            if (opposingHand == null || !IsFinite(opposingHand.position))
            {
                _twoHandStabilized = false;
                _hasSecondHandStabilizerPose = false;
                return;
            }

            Bounds bodyBounds = ResolveActiveBodyBounds(body);
            float maxExtent = math.max(MinimumBoundsSpan, math.cmax((float3)bodyBounds.extents));
            float stabilizerRadius = (maxExtent * 2f) + math.max(0.08f, suitCollisionProbeRadius);
            AbsoluteUniversePosition handAup = AbsoluteUniversePosition.FromRuntimePosition(opposingHand.position);
            AbsoluteUniversePosition bodyAup = AbsoluteUniversePosition.FromRuntimePosition(bodyBounds.center);
            bool withinStabilizerRange = AbsoluteUniversePosition.DistanceSq(in handAup, in bodyAup) <= stabilizerRadius * stabilizerRadius;
            SetTwoHandStabilizerPose(withinStabilizerRange, opposingHand.position);
        }

        private void ApplyTwoHandAngularVelocityDamping(Rigidbody body, Vector3 primaryHandPosition)
        {
            if (!_hasSecondHandStabilizerPose || !IsFinite(primaryHandPosition) || !IsFinite(_secondHandStabilizerPosition))
                return;

            Bounds bodyBounds = ResolveActiveBodyBounds(body);
            float3 extents = math.max((float3)bodyBounds.extents, new float3(MinimumBoundsSpan * 0.5f));
            float boundsSpan = math.max(MinimumBoundsSpan, math.cmax(extents) * 2f);
            float boundsSpanSq = boundsSpan * boundsSpan;
            float handSpanSq = ResolveAupDistanceSqAsFloat(primaryHandPosition, _secondHandStabilizerPosition);
            if (handSpanSq <= boundsSpanSq)
                return;

            Vector3 angularVelocity = body.angularVelocity;
            if (!IsFinite(angularVelocity))
                return;

            Vector3 deltaAngularVelocity = ClampMagnitude(angularVelocity * -0.15f, MaxDeltaAngularVelocity * 0.35f);
            if (deltaAngularVelocity.sqrMagnitude > 0.0000001f && IsFinite(deltaAngularVelocity))
                PhysicsForceRouter.QueueTorque(body, deltaAngularVelocity, ForceMode.VelocityChange);
        }

        private Bounds ResolveActiveBodyBounds(Rigidbody body)
        {
            if (_activeBodyCollider != null && _activeBodyCollider.enabled)
                return _activeBodyCollider.bounds;

            return new Bounds(body.worldCenterOfMass, new Vector3(MinimumBoundsSpan, MinimumBoundsSpan, MinimumBoundsSpan));
        }

        private void ApplyWallRecoilOffset(Vector3 recoilOffset)
        {
            if (!IsFinite(recoilOffset))
                return;

            float3 combinedOffset = (float3)_handWallRecoilOffset + (float3)recoilOffset;
            float offsetSq = math.lengthsq(combinedOffset);
            float maxOffsetSq = HandWallRecoilMaxOffset * HandWallRecoilMaxOffset;
            if (offsetSq > maxOffsetSq && offsetSq > 0.0000001f)
                combinedOffset *= math.rcp(math.max(ApproximateMagnitudeNoSqrt(combinedOffset), MinimumDeltaTime)) * HandWallRecoilMaxOffset;

            _handWallRecoilOffset = (Vector3)combinedOffset;
        }

        private void DecayWallRecoilOffset(float dt)
        {
            if (_handWallRecoilOffset.sqrMagnitude <= 0.0000001f)
                return;

            _handWallRecoilOffset = (Vector3)math.lerp(
                (float3)_handWallRecoilOffset,
                float3.zero,
                math.saturate(dt * HandWallRecoilDecay));
        }

        private static float3 ResolveDominantAxisNormal(float3 deltaFromCenter, float3 axisPenetration)
        {
            if (axisPenetration.x <= axisPenetration.y && axisPenetration.x <= axisPenetration.z)
                return new float3(deltaFromCenter.x >= 0f ? 1f : -1f, 0f, 0f);
            if (axisPenetration.y <= axisPenetration.z)
                return new float3(0f, deltaFromCenter.y >= 0f ? 1f : -1f, 0f);
            return new float3(0f, 0f, deltaFromCenter.z >= 0f ? 1f : -1f);
        }

        private Vector3 ResolveTargetAngularVelocityRadians(Quaternion targetRotation, float dt)
        {
            if (!_hasPreviousControllerPose)
                return Vector3.zero;

            Quaternion delta = targetRotation * Quaternion.Inverse(_previousTargetLocalRotation);
            return ResolveApproxAngularVectorRadians(delta) / dt;
        }

        private void ScheduleFingerPoseBatch()
        {
            if (_fingerPoseScheduled)
                return;

            if (!HectonXRRuntimeState.IsXRActive || fingerSegments == null || fingerSegments.Length <= 0)
                return;

            if (_runtimeGripPoint == null || _activeBody == null)
                return;

            if (fingerCollisionMask.value == 0)
                return;

            if (!_fingerCommands.IsCreated ||
                !_fingerHits.IsCreated ||
                !_fingerPoses.IsCreated ||
                !_fingerRayDefinitions.IsCreated ||
                !_fingerRayRuntime.IsCreated)
            {
                AllocatePersistentBuffers();
                return;
            }

            QueryParameters queryParameters = new QueryParameters(
                fingerCollisionMask.value,
                false,
                QueryTriggerInteraction.Ignore,
                false);

            BuildFingerSpherecastCommandsJob buildJob = new BuildFingerSpherecastCommandsJob
            {
                HandPosition = _runtimeGripPoint.position,
                HandRotation = _runtimeGripPoint.rotation,
                TargetPosition = _activeBody.worldCenterOfMass,
                CastRadius = FingerCastRadius,
                CastLength = FingerCastLength,
                QueryParameters = queryParameters,
                RayDefinitions = _fingerRayDefinitions,
                Commands = _fingerCommands,
                RayRuntime = _fingerRayRuntime
            };

            ProcessFingerHitsJob processJob = new ProcessFingerHitsJob
            {
                CastLength = FingerCastLength,
                Hits = _fingerHits,
                RayRuntime = _fingerRayRuntime,
                Output = _fingerPoses
            };

            JobHandle buildHandle = buildJob.Schedule(FingerCount, 1);
            JobHandle castHandle = SpherecastCommand.ScheduleBatch(_fingerCommands, _fingerHits, 1, buildHandle);
            _fingerPoseHandle = processJob.Schedule(FingerCount, 1, castHandle);
            _fingerPoseScheduled = true;
        }

        private void BreakGrip(PhysicalHandGrabEndReason reason)
        {
            _gripBroken = reason == PhysicalHandGrabEndReason.GripBroken;
            EndGrab(reason);
        }

        private float ResolveVirtualHandMass(float objectMass)
        {
            if (!math.isfinite(objectMass) || objectMass <= HandMaxCarryMass)
                return 0f;

            float heavyMassSpan = math.max(MaxSupportedGrabMass - HandMaxCarryMass, 1f);
            float massRatio = math.saturate((math.min(objectMass, MaxSupportedGrabMass) - HandMaxCarryMass) / heavyMassSpan);
            return math.lerp(HeavyObjectMinimumVirtualMass, VirtualHandMaxMass, massRatio);
        }

        private static float ResolveHarvestSnapDuration(float value)
        {
            return math.isfinite(value)
                ? math.clamp(value, MinimumDeltaTime, HarvestSnapMaxDurationSeconds)
                : HarvestSnapDefaultDurationSeconds;
        }

        private float ResolveSuitCollisionProbeRadius()
        {
            return math.isfinite(suitCollisionProbeRadius)
                ? math.clamp(suitCollisionProbeRadius, MinimumSuitCollisionProbeRadius, MaximumSuitCollisionProbeRadius)
                : 0.07f;
        }

        private float ResolveSuitCrushPenetrationThreshold()
        {
            return math.isfinite(suitCrushPenetrationThreshold)
                ? math.clamp(suitCrushPenetrationThreshold, MinimumSuitCrushPenetrationThreshold, MaximumSuitCrushPenetrationThreshold)
                : 0.025f;
        }

        private static float SanitizeFixedDeltaSeconds(float value)
        {
            return math.isfinite(value)
                ? math.clamp(value, MinimumDeltaTime, MaximumSafeDeltaTime)
                : MinimumDeltaTime;
        }

        private static float ResolveVirtualHandDamping(float virtualMass)
        {
            float mass01 = math.saturate((virtualMass - HeavyObjectMinimumVirtualMass) / (VirtualHandMaxMass - HeavyObjectMinimumVirtualMass));
            float shaped = mass01 * (1.42f - 0.42f * mass01);
            float sqrtMassApprox = math.lerp(1f, VirtualHandMaxMassSqrtApprox, shaped);
            return VirtualDampingScale * VirtualSpringRootApprox * sqrtMassApprox;
        }

        private void SyncDebugState()
        {
            _debugIsGrabbing = IsGrabbing;
            _debugDisconnectArmed = _disconnectArmed;
            _debugGripBroken = _gripBroken;
            _debugSeparation = _currentSeparationSq;
            _debugVirtualHandMass = _virtualHandMass;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _debugGrabbedBodyName = _cachedGrabbedBodyName;
#endif
            _debugSuitContact = _suitContactActive;
            _debugRequiresTwoHands = _requiresTwoHandStabilization;
        }

        private void AdvanceHarvestSnap(float dt)
        {
            if (!_harvestSnapActive)
                return;

            _harvestSnapTimer = math.max(0f, _harvestSnapTimer - math.max(dt, 0f));
            if (_harvestSnapTimer <= 0f)
                CancelHarvestSnap();
        }

        private float ResolveHarvestSnapBlend()
        {
            if (!_harvestSnapActive || _harvestSnapDuration <= MinimumDeltaTime)
                return 0f;

            float remaining01 = math.saturate(_harvestSnapTimer / _harvestSnapDuration);
            return remaining01 * remaining01 * (3f - (2f * remaining01));
        }

        private void ApplyHarvestSnapPose(ref Vector3 controllerPosition, ref Quaternion controllerRotation)
        {
            if (!_harvestSnapActive)
                return;

            if (!IsFinite(_harvestSnapPosition) || !IsFinite(_harvestSnapRotation))
            {
                CancelHarvestSnap();
                return;
            }

            float blend = ResolveHarvestSnapBlend();
            if (blend <= 0.0001f)
                return;

            if (!IsFinite(controllerPosition))
                controllerPosition = _harvestSnapPosition;
            else
                controllerPosition = (Vector3)math.lerp((float3)controllerPosition, (float3)_harvestSnapPosition, blend);

            if (!IsFinite(controllerRotation))
                controllerRotation = _harvestSnapRotation;
            else
                controllerRotation = ApproximateNlerpNoSqrt(controllerRotation, _harvestSnapRotation, blend);

            if (!IsGrabbing && _runtimeRoot != null)
            {
                _runtimeRoot.position = controllerPosition;
                _runtimeRoot.rotation = Quaternion.identity;
                if (_runtimeGripPoint != null)
                    _runtimeGripPoint.rotation = controllerRotation;
            }
        }

        private void FinalizeControllerPoseState(Vector3 controllerPosition, Quaternion controllerRotation)
        {
            _previousControllerPosition = controllerPosition;
            _previousControllerRotation = controllerRotation;
            if (!IsGrabbing)
                _previousTargetLocalRotation = controllerRotation;
            _hasPreviousControllerPose = true;
        }

        private static Vector3 ClampMagnitude(Vector3 value, float maxMagnitude)
        {
            float sqrMagnitude = value.sqrMagnitude;
            float safeMaxMagnitude = math.max(0f, maxMagnitude);
            if (safeMaxMagnitude <= 0f || sqrMagnitude < 0.0000001f)
                return Vector3.zero;

            float maxMagnitudeSq = safeMaxMagnitude * safeMaxMagnitude;
            if (sqrMagnitude <= maxMagnitudeSq)
                return value;

            float approximateMagnitude = math.max(ApproximateMagnitudeNoSqrt(value), MinimumDeltaTime);
            float scale = math.clamp(safeMaxMagnitude * math.rcp(approximateMagnitude), 0f, 1f);
            return (Vector3)((float3)value * scale);
        }

        private static float ApproximateMagnitudeNoSqrt(Vector3 value)
        {
            return ApproximateMagnitudeNoSqrt((float3)value);
        }

        private static float ApproximateMagnitudeNoSqrt(float3 value)
        {
            float3 absValue = math.abs(value);
            float largest = math.cmax(absValue);
            float smallest = math.cmin(absValue);
            float middle = absValue.x + absValue.y + absValue.z - largest - smallest;
            return largest + (middle * 0.375f) + (smallest * 0.125f);
        }

        private static Vector3 ClampPerAxis(Vector3 value, float axisLimit)
        {
            return (Vector3)math.clamp((float3)value, new float3(-axisLimit), new float3(axisLimit));
        }

        private static float ResolveMaxDeltaVelocity(Rigidbody body, float gainMultiplier)
        {
            float depenetrationVelocity = UnityEngine.Physics.defaultMaxDepenetrationVelocity;
            if (body != null && math.isfinite(body.maxDepenetrationVelocity) && body.maxDepenetrationVelocity > 0f)
                depenetrationVelocity = math.min(depenetrationVelocity, body.maxDepenetrationVelocity);
            float safeDepenetrationVelocity = math.isfinite(depenetrationVelocity) && depenetrationVelocity > 0f
                ? depenetrationVelocity
                : MaxDeltaVelocity;
            return math.max(0f, math.min(MaxDeltaVelocity * math.max(0f, gainMultiplier), safeDepenetrationVelocity));
        }

        private static float ResolveAupDistanceSqAsFloat(Vector3 a, Vector3 b)
        {
            if (!IsFinite(a) || !IsFinite(b))
                return float.MaxValue;

            AbsoluteUniversePosition aupA = AbsoluteUniversePosition.FromRuntimePosition(a);
            AbsoluteUniversePosition aupB = AbsoluteUniversePosition.FromRuntimePosition(b);
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in aupA, in aupB);
            return math.isfinite((float)distanceSq)
                ? math.min((float)distanceSq, float.MaxValue)
                : float.MaxValue;
        }

        private static AbsoluteUniversePosition ResolveSuitContactAup(Vector3 contactPoint, Vector3 controllerPosition)
        {
            if (HectonXRRuntimeState.TryResolveCachedHeadAup(controllerPosition, out AbsoluteUniversePosition controllerAup))
                return OffsetAupLocal(in controllerAup, contactPoint - controllerPosition);

            return AbsoluteUniversePosition.FromRuntimePosition(contactPoint);
        }

        private static AbsoluteUniversePosition OffsetAupLocal(in AbsoluteUniversePosition anchorAup, Vector3 runtimeOffset)
        {
            AbsoluteUniversePosition result = anchorAup;
            result.LocalX += runtimeOffset.x;
            result.LocalY += runtimeOffset.y;
            result.LocalZ += runtimeOffset.z;
            NormalizeAupLocalAxis(ref result.GridX, ref result.LocalX);
            NormalizeAupLocalAxis(ref result.GridY, ref result.LocalY);
            NormalizeAupLocalAxis(ref result.GridZ, ref result.LocalZ);
            return result;
        }

        private static void NormalizeAupLocalAxis(ref long grid, ref float local)
        {
            const float cellSize = AbsoluteUniversePosition.CellSizeMeters;
            if (local >= 0f && local < cellSize)
                return;

            long gridDelta = (long)math.floor(local / cellSize);
            grid += gridDelta;
            local -= gridDelta * cellSize;
            if (local < 0f)
            {
                local += cellSize;
                grid--;
                return;
            }

            if (local >= cellSize)
            {
                local -= cellSize;
                grid++;
            }
        }

        private Quaternion ResolveTargetBodyRotation(Quaternion handRotation)
        {
            Quaternion targetBodyRotation = handRotation * _grabBodyRotationOffset;
            if (!IsFinite(targetBodyRotation))
            {
                _grabBodyRotationOffset = Quaternion.identity;
                targetBodyRotation = handRotation;
            }

            return targetBodyRotation;
        }

        private static Quaternion ResolveGrabRotationOffset(Quaternion handRotation, Quaternion bodyRotation)
        {
            if (!IsFinite(handRotation) || !IsFinite(bodyRotation))
                return Quaternion.identity;

            quaternion handQ = ToMathematicsQuaternion(handRotation);
            quaternion bodyQ = ToMathematicsQuaternion(bodyRotation);
            quaternion offsetQ = NormalizeQuaternionNoSqrt(math.mul(math.inverse(handQ), bodyQ));
            return ToUnityQuaternion(offsetQ);
        }

        private static Quaternion ResolveHarvestSnapRotation(Vector3 surfaceNormal, Quaternion fallbackRotation)
        {
            if (!IsFinite(surfaceNormal) || surfaceNormal.sqrMagnitude <= 0.000001f || !IsFinite(fallbackRotation))
                return fallbackRotation;

            Vector3 currentForward = fallbackRotation * Vector3.forward;
            Vector3 desiredForward = NormalizeVectorApproxNoSqrt(-surfaceNormal, Vector3.forward);
            Quaternion rotation = ResolveShortestArcNoTrig(currentForward, desiredForward) * fallbackRotation;
            return IsFinite(rotation) ? rotation : fallbackRotation;
        }

        private static quaternion ToMathematicsQuaternion(Quaternion value)
        {
            return new quaternion(value.x, value.y, value.z, value.w);
        }

        private static Quaternion ToUnityQuaternion(quaternion value)
        {
            return new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);
        }

        private static Quaternion ApproximateLocalXRotationNoTrig(float degrees)
        {
            ApproximateSinCosNoTrig(degrees * RadiansPerDegree * 0.5f, out float sinHalf, out float cosHalf);
            return new Quaternion(sinHalf, 0f, 0f, cosHalf);
        }

        private static Quaternion ApproximateNlerpNoSqrt(Quaternion from, Quaternion to, float t)
        {
            float4 fromValue = new float4(from.x, from.y, from.z, from.w);
            float4 toValue = new float4(to.x, to.y, to.z, to.w);
            toValue = math.select(toValue, -toValue, math.dot(fromValue, toValue) < 0.0f);

            float4 blended = math.lerp(fromValue, toValue, math.saturate(t));
            float lenSq = math.max(math.dot(blended, blended), 0.000001f);
            blended *= 1.5f - (0.5f * lenSq);
            return new Quaternion(blended.x, blended.y, blended.z, blended.w);
        }

        private static quaternion NormalizeQuaternionNoSqrt(quaternion value)
        {
            float4 v = value.value;
            v *= ApproximateInverseLengthNoSqrt(math.dot(v, v));
            return new quaternion(v);
        }

        private static float ApproximateInverseLengthNoSqrt(float lengthSq)
        {
            return math.rcp(0.5f + (0.5f * math.max(lengthSq, 0.000001f)));
        }

        private static Vector3 ResolveApproxAngularVectorRadians(Quaternion delta)
        {
            if (!IsFinite(delta))
                return Vector3.zero;

            float sign = delta.w < 0f ? -1f : 1f;
            float3 vector = new float3(delta.x * sign, delta.y * sign, delta.z * sign);
            float lenSq = math.lengthsq(vector);
            if (lenSq <= 0.0000001f || !math.isfinite(lenSq))
                return Vector3.zero;

            float lenSq2 = lenSq * lenSq;
            float scale = 2f * (1f + (lenSq * (0.16666667f + (0.075f * lenSq) + (0.044642857f * lenSq2))));
            float3 angularVector = vector * scale;
            return math.all(math.isfinite(angularVector)) ? (Vector3)angularVector : Vector3.zero;
        }

        private static Quaternion ResolveShortestArcNoTrig(Vector3 fromDirection, Vector3 toDirection)
        {
            float3 from = NormalizeVectorApproxNoSqrt((float3)fromDirection, new float3(0f, 0f, 1f));
            float3 to = NormalizeVectorApproxNoSqrt((float3)toDirection, new float3(0f, 0f, 1f));
            float dot = math.clamp(math.dot(from, to), -1f, 1f);
            float3 axis = math.cross(from, to);
            if (dot < -0.999f)
            {
                axis = math.cross(from, new float3(1f, 0f, 0f));
                if (math.lengthsq(axis) <= 0.000001f)
                    axis = math.cross(from, new float3(0f, 1f, 0f));

                axis = NormalizeVectorApproxNoSqrt(axis, new float3(0f, 1f, 0f));
                return new Quaternion(axis.x, axis.y, axis.z, 0f);
            }

            float4 value = new float4(axis.x, axis.y, axis.z, 1f + dot);
            value *= ApproximateInverseLengthNoSqrt(math.dot(value, value));
            return new Quaternion(value.x, value.y, value.z, value.w);
        }

        private static Vector3 NormalizeVectorApproxNoSqrt(Vector3 value, Vector3 fallback)
        {
            return (Vector3)NormalizeVectorApproxNoSqrt((float3)value, (float3)fallback);
        }

        private static float3 NormalizeVectorApproxNoSqrt(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            if (lenSq <= 0.000001f || !math.isfinite(lenSq))
                return fallback;

            return value * ApproximateInverseLengthNoSqrt(lenSq);
        }

        private static void ApproximateSinCosNoTrig(float x, out float sin, out float cos)
        {
            float clamped = math.clamp(x, -1.5707964f, 1.5707964f);
            float x2 = clamped * clamped;
            sin = clamped * (1f - (x2 * (0.16666667f - (x2 * 0.008333333f))));
            cos = 1f - (x2 * (0.5f - (x2 * 0.041666667f)));
        }

        private static bool IsFinite(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
        }

        private static bool IsFinite(Quaternion value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) || float.IsNaN(value.w) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z) || float.IsInfinity(value.w));
        }

        private static float ResolveSuitCollisionHapticScale(float pressure01)
        {
            float depth01 = 0f;
            float integrity01 = 1f;
            float pressureSeverity01 = 0f;
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
            {
                depth01 = math.saturate(runtimeContext.MovementState.DepthMeters / HapticDepthReferenceMeters);
                if ((runtimeContext.MovementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasSurvival) != 0u)
                {
                    integrity01 = math.saturate(runtimeContext.SurvivalState.IntegrityNormalized);
                    pressureSeverity01 = math.saturate(runtimeContext.SurvivalState.PressureExposureSeverity01);
                }
            }

            float numb01 = math.saturate(math.max(depth01, pressureSeverity01) * math.lerp(1.35f, 0.85f, integrity01));
            float sample = math.saturate(math.max(pressure01, numb01));
            float scaled = sample * (HapticPressureIntegrityLut.Length - 1);
            int index = math.clamp((int)math.floor(scaled), 0, HapticPressureIntegrityLut.Length - 1);
            int nextIndex = math.min(index + 1, HapticPressureIntegrityLut.Length - 1);
            float fraction = scaled - index;
            return math.saturate(pressure01 * math.lerp(HapticPressureIntegrityLut[index], HapticPressureIntegrityLut[nextIndex], fraction));
        }

        private void EmergencyResetGrabbedBodyMotion(Rigidbody body)
        {
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            _virtualHandVelocity = Vector3.zero;
            _virtualHandTargetVelocity = Vector3.zero;
            _currentSeparationSq = 0f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(InvalidMotionResetMessage);
#endif
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildFingerSpherecastCommandsJob : IJobParallelFor
        {
            public float3 HandPosition;
            public quaternion HandRotation;
            public float3 TargetPosition;
            public float CastRadius;
            public float CastLength;
            public QueryParameters QueryParameters;

            [ReadOnly] public NativeArray<FingerRayDefinition> RayDefinitions;

            [WriteOnly] public NativeArray<SpherecastCommand> Commands;
            [WriteOnly] public NativeArray<FingerRayRuntime> RayRuntime;

            public void Execute(int index)
            {
                FingerRayDefinition definition = RayDefinitions[index];
                float3 localKnuckleOffset = math.all(math.isfinite(definition.LocalKnuckleOffset))
                    ? definition.LocalKnuckleOffset
                    : float3.zero;
                float3 localFingerDirection = NormalizeVectorApproxNoSqrt(definition.LocalFingerDirection, new float3(0f, 0f, 1f));
                float3 origin = HandPosition + math.rotate(HandRotation, localKnuckleOffset);
                if (!math.all(math.isfinite(origin)))
                    origin = HandPosition;

                float3 fallbackDirection = math.rotate(HandRotation, localFingerDirection);
                fallbackDirection = NormalizeVectorApproxNoSqrt(fallbackDirection, new float3(0f, 0f, 1f));
                float3 targetDirection = NormalizeVectorApproxNoSqrt(TargetPosition - origin, fallbackDirection);
                if (!math.all(math.isfinite(targetDirection)))
                    targetDirection = fallbackDirection;

                RayRuntime[index] = new FingerRayRuntime
                {
                    Origin = origin,
                    Direction = targetDirection
                };
                Commands[index] = new SpherecastCommand(origin, CastRadius, targetDirection, QueryParameters, CastLength);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ProcessFingerHitsJob : IJobParallelFor
        {
            public float CastLength;

            [ReadOnly] public NativeArray<RaycastHit> Hits;
            [ReadOnly] public NativeArray<FingerRayRuntime> RayRuntime;

            [WriteOnly] public NativeArray<FingerPoseData> Output;

            public void Execute(int index)
            {
                RaycastHit hit = Hits[index];
                FingerRayRuntime rayRuntime = RayRuntime[index];
                float3 origin = math.all(math.isfinite(rayRuntime.Origin))
                    ? rayRuntime.Origin
                    : float3.zero;
                float3 direction = NormalizeVectorApproxNoSqrt(rayRuntime.Direction, new float3(0f, 0f, 1f));
                float safeCastLength = math.isfinite(CastLength)
                    ? math.max(CastLength, MinimumDeltaTime)
                    : MinimumDeltaTime;
                float3 hitPoint = hit.point;
                bool hasHit =
                    math.isfinite(hit.distance) &&
                    hit.distance > 0f &&
                    hit.distance <= safeCastLength &&
                    math.all(math.isfinite(hitPoint));

                FingerPoseData pose = default;
                if (hasHit)
                {
                    float bend = 1f - math.saturate(hit.distance / safeCastLength);
                    float3 normal = hit.normal;
                    if (math.lengthsq(normal) < 0.000001f)
                        normal = -direction;
                    else
                        normal = NormalizeVectorApproxNoSqrt(normal, -direction);

                    pose.BendAngle = bend;
                    pose.TipPosition = hitPoint;
                    pose.TipNormal = normal;
                }
                else
                {
                    pose.BendAngle = 1f;
                    pose.TipPosition = origin + direction * safeCastLength;
                    pose.TipNormal = -direction;
                }

                Output[index] = pose;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct FingerPoseData
        {
            public float3 TipPosition;
            public float3 TipNormal;
            public float BendAngle;
        }
    }
}
