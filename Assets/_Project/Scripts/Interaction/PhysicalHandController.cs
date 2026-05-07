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

    [StructLayout(LayoutKind.Sequential)]
    internal struct FingerRayDefinition
    {
        public float3 LocalKnuckleOffset;
        public float3 LocalFingerDirection;
    }

    [StructLayout(LayoutKind.Sequential)]
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
        private const float TwoHandAngularDampingSharpness = 12f;
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
        private const float HandContactHapticCooldownSeconds = 0.035f;
        private const float HandDamageHapticCooldownSeconds = 0.12f;
        private const byte LeftMotorMask = 0b0001;
        private const byte RightMotorMask = 0b0010;
        private const byte CriticalHapticPriority = 3;
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
        private static readonly float3 DefaultThumbDirection = math.normalize(new float3(-0.35f, -0.05f, 0.93f));
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
        private float _lastFingerPoseDeltaTime = MinimumDeltaTime;
        private float _nextHandContactHapticTime;
        private float _nextHandDamageHapticTime;
        private int _lastSuitDamageFrame = -1;
        private JobHandle _fingerPoseHandle;

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
        /// Resolves the current physical hand probe used by collider-driven diegetic controls.
        /// </summary>
        /// <param name="position">World-space hand probe position.</param>
        /// <param name="rotation">World-space hand probe rotation.</param>
        /// <returns>True when a valid authored or runtime hand probe exists.</returns>
        public bool TryGetInteractionProbePose(out Vector3 position, out Quaternion rotation)
        {
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

            if (body.mass > MaxSupportedGrabMass)
                return false;

            if (IsGrabbing)
                EndGrab(PhysicalHandGrabEndReason.ManualRelease);

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
            _virtualHandMass = ResolveVirtualHandMass(body.mass);
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
            _requiresTwoHandStabilization = requireTwoHandsForHeavyMass && body.mass > HeavyTwoHandMassThreshold;
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
            float dt = math.clamp(fixedDeltaTime, MinimumDeltaTime, MaximumSafeDeltaTime);
            _lastFingerPoseDeltaTime = dt;
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
                EmergencyResetGrabbedBodyMotion(_activeBody, "StepFixed.ControllerPose");
                BreakGrip(PhysicalHandGrabEndReason.InvalidTarget);
                ApplyOpenHandPose(dt);
                return;
            }

            EnsureRuntimeProxy();
            ResolveFingerSegments();

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

        private void Awake()
        {
            _cachedTransform = transform;
            EnsureRuntimeProxy();
            AllocatePersistentBuffers();
            ResolveFingerSegments();
            if (enableSuitCollisionShell)
                EnsureSuitCollisionShell();
            SyncDebugState();
        }

        private void OnDisable()
        {
            if (IsGrabbing)
                EndGrab(PhysicalHandGrabEndReason.Disabled);
        }

        private void OnDestroy()
        {
            DisposePersistentBuffers();
            if (_suitHandTransform != null)
                Destroy(_suitHandTransform.gameObject);
        }

        private void EnsureRuntimeProxy()
        {
            if (_runtimeProxyCreated)
                return;

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
                    _suitHandCollider.radius = math.max(0.001f, suitCollisionProbeRadius);
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
            _suitHandCollider.radius = math.max(0.001f, suitCollisionProbeRadius);
            _suitHandCollider.enabled = enableSuitCollisionShell;

            _suitCollisionShellCreated = true;
        }

        private void StepSuitCollisionShell(Vector3 controllerPosition, Quaternion controllerRotation, float dt)
        {
            if (!enableSuitCollisionShell)
            {
                if (_suitHandCollider != null && _suitHandCollider.enabled)
                    _suitHandCollider.enabled = false;

                _suitContactActive = false;
                return;
            }

            if (!_suitCollisionShellCreated)
            {
                _suitContactActive = false;
                return;
            }

            if (_suitOverlapResults == null || _suitOverlapResults.Length == 0 || suitCollisionMask.value == 0)
            {
                _suitContactActive = false;
                return;
            }

            float radius = math.max(0.001f, suitCollisionProbeRadius);
            if (_suitHandBody != null)
            {
                _suitHandBody.MovePosition(controllerPosition);
                _suitHandBody.MoveRotation(controllerRotation);
            }
            else if (_suitHandTransform != null)
            {
                _suitHandTransform.SetPositionAndRotation(controllerPosition, controllerRotation);
            }
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
                float3 deltaFromCenter = (float3)(controllerPosition - hitBounds.center);
                float3 axisPenetration = (float3)hitBounds.extents + new float3(radius) - math.abs(deltaFromCenter);
                float penetration = math.cmin(axisPenetration);
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

            float crushThreshold = math.max(0.001f, suitCrushPenetrationThreshold);
            float pressure01 = math.saturate(maxPenetration / crushThreshold);
            float hapticScale = ResolveSuitCollisionHapticScale(pressure01);
            float now = Time.time;
            if (routeHandCollisionHaptics && now >= _nextHandContactHapticTime)
            {
                byte motorMask = handSide == PhysicalHandSide.Left ? LeftMotorMask : RightMotorMask;
                float lowIntensity = handSide == PhysicalHandSide.Left ? hapticScale : hapticScale * 0.45f;
                float highIntensity = handSide == PhysicalHandSide.Right ? hapticScale : hapticScale * 0.45f;
                ToolHapticsRuntime.EnqueueCommand(
                    lowIntensity,
                    highIntensity,
                    0.08f,
                    7.5f,
                    CriticalHapticPriority,
                    motorMask,
                    2);
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
                _nextHandContactHapticTime = now + HandContactHapticCooldownSeconds;
            }

            int frame = Time.frameCount;
            if (pressure01 < 1f ||
                frame == _lastSuitDamageFrame ||
                now < _nextHandDamageHapticTime)
            {
                return;
            }

            _lastSuitDamageFrame = frame;
            _nextHandDamageHapticTime = now + HandDamageHapticCooldownSeconds;
            AbsoluteUniversePosition contactAup = AbsoluteUniversePosition.FromRuntimePosition(contactPoint);
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
            bool deferDispose = _fingerPoseScheduled;
            JobHandle disposeDependency = _fingerPoseHandle;

            DisposeNativeArray(ref _fingerCommands, deferDispose, disposeDependency);
            DisposeNativeArray(ref _fingerHits, deferDispose, disposeDependency);
            DisposeNativeArray(ref _fingerPoses, deferDispose, disposeDependency);
            DisposeNativeArray(ref _fingerRayDefinitions, deferDispose, disposeDependency);
            DisposeNativeArray(ref _fingerRayRuntime, deferDispose, disposeDependency);

            _fingerPoseScheduled = false;
            _fingerPoseHandle = default;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, bool deferDispose, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            if (deferDispose)
                array.Dispose(dependency);
            else
                array.Dispose();

            array = default;
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

            if (swimBlockoutRig == null)
                swimBlockoutRig = GetComponentInChildren<PlayerSwimBlockoutRig>(true);

            if (swimBlockoutRig != null)
                _resolvedRightHandAttachment = swimBlockoutRig.RightHandAttachment;

            _rightHandAttachmentResolved = true;
            return _resolvedRightHandAttachment;
        }

        private Transform ResolveOpposingHandAttachment()
        {
            if (_opposingHandAttachmentResolved)
                return _resolvedOpposingHandAttachment;

            if (swimBlockoutRig == null)
                swimBlockoutRig = GetComponentInChildren<PlayerSwimBlockoutRig>(true);

            if (swimBlockoutRig != null)
            {
                _resolvedOpposingHandAttachment = handSide == PhysicalHandSide.Left
                    ? swimBlockoutRig.RightHandAttachment
                    : swimBlockoutRig.LeftHandAttachment;
            }

            _opposingHandAttachmentResolved = true;
            return _resolvedOpposingHandAttachment;
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

                segment.localRotation = Quaternion.Slerp(segment.localRotation, _baseFingerLocalRotations[i], blendT);
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

            Quaternion targetRotation = _baseFingerLocalRotations[segmentIndex] * Quaternion.AngleAxis(-targetCurlDegrees, Vector3.right);
            segment.localRotation = Quaternion.Slerp(segment.localRotation, targetRotation, blendT);
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
            Vector3 targetEuler = NormalizeEulerDegrees(localTargetRotation.eulerAngles);
            Vector3 angularVelocityDegrees = Vector3.zero;
            if (_hasPreviousControllerPose)
            {
                Quaternion delta = controllerRotation * Quaternion.Inverse(_previousControllerRotation);
                Vector3 axis;
                float angleDegrees;
                delta.ToAngleAxis(out angleDegrees, out axis);
                if (!IsFinite(axis) || axis.sqrMagnitude < 0.000001f)
                {
                    axis = Vector3.up;
                    angleDegrees = 0f;
                }

                if (angleDegrees > 180f)
                    angleDegrees -= 360f;

                angularVelocityDegrees = (Vector3)(math.normalizesafe((float3)axis, new float3(0f, 1f, 0f)) * (angleDegrees / dt));
            }

            ArticulationDrive xDrive = _runtimeHandBody.xDrive;
            ArticulationDrive yDrive = _runtimeHandBody.yDrive;
            ArticulationDrive zDrive = _runtimeHandBody.zDrive;
            xDrive.target = targetEuler.x;
            xDrive.targetVelocity = angularVelocityDegrees.x;
            yDrive.target = targetEuler.y;
            yDrive.targetVelocity = angularVelocityDegrees.y;
            zDrive.target = targetEuler.z;
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
                EmergencyResetGrabbedBodyMotion(body, "SolveGrabbedBody.Input");
                BreakGrip(PhysicalHandGrabEndReason.InvalidTarget);
                return;
            }

            Vector3 bodyPosition = body.worldCenterOfMass;
            if (!IsFinite(bodyPosition))
            {
                EmergencyResetGrabbedBodyMotion(body, "SolveGrabbedBody.CenterOfMass");
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
            deltaRotation.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (!IsFinite(axis) || axis.sqrMagnitude < 0.000001f)
            {
                axis = Vector3.up;
                angleDegrees = 0f;
            }

            if (angleDegrees > 180f)
                angleDegrees -= 360f;

            float angleRadians = angleDegrees * RadiansPerDegree;
            Vector3 angularError = (Vector3)(math.normalizesafe((float3)axis, new float3(0f, 1f, 0f)) * angleRadians);
            Vector3 targetAngularVelocity = ResolveTargetAngularVelocityRadians(targetBodyRotation, dt);
            _previousTargetLocalRotation = targetBodyRotation;
            if (!IsFinite(targetAngularVelocity))
            {
                EmergencyResetGrabbedBodyMotion(body, "SolveGrabbedBody.TargetAngularVelocity");
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
                ApplyTwoHandAngularVelocityDamping(body, handPosition, dt);
                return;
            }

            Vector3 gravityDirection = UnityEngine.Physics.gravity;
            if (gravityDirection.sqrMagnitude < 0.000001f)
                gravityDirection = Vector3.down;

            Vector3 lever = handPosition - body.worldCenterOfMass;
            if (lever.sqrMagnitude < 0.000001f)
                lever = body.transform.right;

            float3 torqueAxis = math.cross(
                math.normalizesafe((float3)lever, (float3)body.transform.right),
                math.normalizesafe((float3)gravityDirection, new float3(0f, -1f, 0f)));
            torqueAxis = math.normalizesafe(torqueAxis, (float3)body.transform.forward);

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

        private void ApplyTwoHandAngularVelocityDamping(Rigidbody body, Vector3 primaryHandPosition, float dt)
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

            float overSpan01 = math.saturate((handSpanSq - boundsSpanSq) / math.max(boundsSpanSq, MinimumDeltaTime));
            float damping = math.saturate(overSpan01 * TwoHandAngularDampingSharpness * math.max(dt, MinimumDeltaTime));
            Vector3 dampedAngularVelocity = (Vector3)math.lerp((float3)body.angularVelocity, float3.zero, damping);
            if (IsFinite(dampedAngularVelocity))
                body.angularVelocity = dampedAngularVelocity;
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
                combinedOffset = math.normalizesafe(combinedOffset, float3.zero) * HandWallRecoilMaxOffset;

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
            delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (!IsFinite(axis) || axis.sqrMagnitude < 0.000001f)
                return Vector3.zero;

            if (angleDegrees > 180f)
                angleDegrees -= 360f;

            return (Vector3)(math.normalizesafe((float3)axis, new float3(0f, 1f, 0f)) * ((angleDegrees * RadiansPerDegree) / dt));
        }

        private void ScheduleFingerPoseBatch()
        {
            if (_fingerPoseScheduled)
                return;

            if (_runtimeGripPoint == null || _activeBody == null)
                return;

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
            if (objectMass <= HandMaxCarryMass)
                return 0f;

            float heavyMassSpan = math.max(MaxSupportedGrabMass - HandMaxCarryMass, 1f);
            float massRatio = math.saturate((objectMass - HandMaxCarryMass) / heavyMassSpan);
            return math.lerp(HeavyObjectMinimumVirtualMass, VirtualHandMaxMass, massRatio);
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

        private void FinalizeControllerPoseState(Vector3 controllerPosition, Quaternion controllerRotation)
        {
            _previousControllerPosition = controllerPosition;
            _previousControllerRotation = controllerRotation;
            if (!IsGrabbing)
                _previousTargetLocalRotation = controllerRotation;
            _hasPreviousControllerPose = true;
        }

        private static Vector3 NormalizeEulerDegrees(Vector3 euler)
        {
            euler.x = NormalizeSingleAngle(euler.x);
            euler.y = NormalizeSingleAngle(euler.y);
            euler.z = NormalizeSingleAngle(euler.z);
            return euler;
        }

        private static float NormalizeSingleAngle(float angle)
        {
            if (angle > 180f)
                angle -= 360f;
            return angle;
        }

        private static Vector3 ClampMagnitude(Vector3 value, float maxMagnitude)
        {
            float sqrMagnitude = value.sqrMagnitude;
            float maxMagnitudeSq = maxMagnitude * maxMagnitude;
            if (sqrMagnitude <= maxMagnitudeSq || sqrMagnitude < 0.0000001f)
                return value;

            return (Vector3)(math.normalizesafe((float3)value, float3.zero) * maxMagnitude);
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
            AbsoluteUniversePosition aupA = AbsoluteUniversePosition.FromRuntimePosition(a);
            AbsoluteUniversePosition aupB = AbsoluteUniversePosition.FromRuntimePosition(b);
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in aupA, in aupB);
            return math.isfinite((float)distanceSq)
                ? math.min((float)distanceSq, float.MaxValue)
                : float.MaxValue;
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
            quaternion offsetQ = math.normalize(math.mul(math.inverse(handQ), bodyQ));
            return ToUnityQuaternion(offsetQ);
        }

        private static quaternion ToMathematicsQuaternion(Quaternion value)
        {
            return new quaternion(value.x, value.y, value.z, value.w);
        }

        private static Quaternion ToUnityQuaternion(quaternion value)
        {
            return new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);
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

        private void EmergencyResetGrabbedBodyMotion(Rigidbody body, string context)
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
                float3 localFingerDirection = math.normalizesafe(definition.LocalFingerDirection, new float3(0f, 0f, 1f));
                float3 origin = HandPosition + math.rotate(HandRotation, localKnuckleOffset);
                if (!math.all(math.isfinite(origin)))
                    origin = HandPosition;

                float3 fallbackDirection = math.rotate(HandRotation, localFingerDirection);
                fallbackDirection = math.normalizesafe(fallbackDirection, new float3(0f, 0f, 1f));
                float3 targetDirection = math.normalizesafe(TargetPosition - origin, fallbackDirection);
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
                float3 direction = math.normalizesafe(rayRuntime.Direction, new float3(0f, 0f, 1f));
                float3 hitPoint = hit.point;
                bool hasHit = hit.distance > 0f && !math.any(math.isnan(hitPoint));

                FingerPoseData pose = default;
                if (hasHit)
                {
                    float bend = 1f - math.saturate(hit.distance / math.max(CastLength, MinimumDeltaTime));
                    float3 normal = hit.normal;
                    if (math.lengthsq(normal) < 0.000001f)
                        normal = -direction;

                    pose.BendAngle = bend;
                    pose.TipPosition = hitPoint;
                    pose.TipNormal = normal;
                }
                else
                {
                    pose.BendAngle = 1f;
                    pose.TipPosition = origin + direction * CastLength;
                    pose.TipNormal = -direction;
                }

                Output[index] = pose;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FingerPoseData
        {
            public float3 TipPosition;
            public float3 TipNormal;
            public float BendAngle;
        }
    }
}
