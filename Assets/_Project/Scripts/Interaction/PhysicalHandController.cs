// ============================================================================
// HECTON-8 — PhysicalHandController.cs
// Heavy-object articulation hand proxy with zero-GC finger spherecast batching.
// ============================================================================

namespace Hecton8.Interaction
{
    using Hecton8.Gameplay;
    using Hecton8.Physics;
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
        private const float VirtualSpringK = 300f;
        private const float VirtualHandLagMax = 0.4f;
        private const float GripWarnDistance = 0.4f;
        private const float GripBreakDistance = 0.8f;
        private const float MaxDeltaVelocity = 15f;
        private const float MaxDeltaAngularVelocity = 25f;
        private const float MaxObjectVelocity = 40f;
        private const float MaxObjectAngularVelocity = 50f;
        private const float VelocityLeadFactor = 0.04f;
        private const float LinearNaturalFrequency = 12f;
        private const float AngularNaturalFrequency = 10f;
        private const float FingerCastRadius = 0.012f;
        private const float FingerCastLength = 0.09f;
        private const float FingerInterpolationSpeed = 18f;
        private const float MaxSupportedGrabMass = 500f;
        private const float MinimumDeltaTime = 0.0001f;
        private const float MaximumSafeDeltaTime = 0.02f;
        private const float RadiansPerDegree = 0.0174532925f;

        private static readonly float3 DefaultThumbKnuckleOffset = new float3(-0.028f, -0.012f, 0.018f);
        private static readonly float3 DefaultIndexKnuckleOffset = new float3(-0.015f, -0.004f, 0.034f);
        private static readonly float3 DefaultMiddleKnuckleOffset = new float3(0f, -0.002f, 0.04f);
        private static readonly float3 DefaultRingKnuckleOffset = new float3(0.015f, -0.004f, 0.034f);
        private static readonly float3 DefaultLittleKnuckleOffset = new float3(0.03f, -0.008f, 0.026f);
        private static readonly float3 DefaultThumbDirection = math.normalize(new float3(-0.35f, -0.05f, 0.93f));
        private static readonly float3 DefaultFingerDirection = new float3(0f, 0f, 1f);
        [Header("── References ─────────────────────────")]
        [Tooltip("Optional authored swim blockout rig used to source a stable right-hand attachment.")]
        [SerializeField] private PlayerSwimBlockoutRig swimBlockoutRig;

        [Tooltip("Optional explicit right-hand transform used when the blockout rig is absent.")]
        [SerializeField] private Transform rightHandAttachmentOverride;

        [Tooltip("Optional finger segment transforms. Layout: thumb/index/middle/ring/little, proximal→distal.")]
        [SerializeField] private Transform[] fingerSegments;

        [Header("── Finger Solve ───────────────────────")]
        [Tooltip("Collision layers considered solid for finger spherecasts.")]
        [SerializeField] private LayerMask fingerCollisionMask = ~0;

        [Tooltip("Maximum curl angle applied to the proximal finger segment.")]
        [SerializeField, Range(10f, 100f)] private float proximalCurlDegrees = 56f;

        [Tooltip("Maximum curl angle applied to the intermediate finger segment.")]
        [SerializeField, Range(10f, 100f)] private float intermediateCurlDegrees = 68f;

        [Tooltip("Maximum curl angle applied to the distal finger segment.")]
        [SerializeField, Range(10f, 100f)] private float distalCurlDegrees = 42f;

        [Header("── Diagnostics ───────────────────────")]
#pragma warning disable CS0414
        [SerializeField] private bool _debugIsGrabbing;
        [SerializeField] private bool _debugDisconnectArmed;
        [SerializeField] private bool _debugGripBroken;
        [SerializeField] private float _debugSeparation;
        [SerializeField] private float _debugVirtualHandMass;
        [SerializeField] private string _debugGrabbedBodyName;
#pragma warning restore CS0414

        private HeavyCarryInteractable _activeInteractable;
        private Rigidbody _activeBody;
        private Transform _cachedTransform;
        private Transform _runtimeRoot;
        private Transform _runtimeGripPoint;
        private ArticulationBody _runtimeHandBody;
        private Quaternion _previousControllerRotation = Quaternion.identity;
        private Quaternion _previousTargetLocalRotation = Quaternion.identity;
        private Vector3 _previousControllerPosition;
        private Vector3 _virtualHandPosition;
        private Vector3 _virtualHandVelocity;
        private float _virtualHandMass;
        private float _cachedBodyDrag;
        private float _cachedBodyAngularDrag;
        private float _cachedBodyMaxAngularVelocity;
        private float _cachedBodyMaxLinearVelocity;
        private float _currentSeparation;
        private bool _isGrabbing;
        private bool _disconnectArmed;
        private bool _gripBroken;
        private bool _runtimeProxyCreated;
        private bool _fingerSegmentsResolved;
        private bool _hasPreviousControllerPose;
        private bool _fingerPoseScheduled;
        private JobHandle _fingerPoseHandle;

        private NativeArray<SpherecastCommand> _fingerCommands;
        private NativeArray<RaycastHit> _fingerHits;
        private NativeArray<FingerPoseData> _fingerPoses;
        private NativeArray<float3> _fingerOrigins;
        private NativeArray<float3> _fingerDirections;
        private NativeArray<float3> _localKnuckleOffsets;
        private NativeArray<float3> _localFingerDirections;
        private Quaternion[] _baseFingerLocalRotations;

        /// <summary>True while a heavy rigidbody is actively being held by the physical hand proxy.</summary>
        public bool IsGrabbing => _isGrabbing && _activeBody != null;

        /// <summary>True when the grip auto-broke because separation exceeded the hard disconnect threshold.</summary>
        public bool GripBroken => _gripBroken;

        /// <summary>Current world-space separation between the virtual hand target and the grabbed body center of mass.</summary>
        public float CurrentSeparation => _currentSeparation;

        /// <summary>Current virtual hand mass used to introduce heavy-object lag.</summary>
        public float CurrentVirtualHandMass => _virtualHandMass;

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
            _cachedBodyDrag = body.linearDamping;
            _cachedBodyAngularDrag = body.angularDamping;
            _cachedBodyMaxAngularVelocity = body.maxAngularVelocity;
            _cachedBodyMaxLinearVelocity = body.maxLinearVelocity;
            _virtualHandMass = ResolveVirtualHandMass(body.mass);
            _virtualHandPosition = body.worldCenterOfMass;
            _virtualHandVelocity = Vector3.zero;
            _previousControllerPosition = _virtualHandPosition;
            _previousControllerRotation = _runtimeGripPoint != null ? _runtimeGripPoint.rotation : Quaternion.identity;
            _previousTargetLocalRotation = Quaternion.identity;
            _currentSeparation = 0f;
            _disconnectArmed = false;
            _gripBroken = false;
            _isGrabbing = true;
            _hasPreviousControllerPose = false;

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
            _disconnectArmed = false;
            _isGrabbing = false;
            _virtualHandMass = 0f;
            _currentSeparation = 0f;
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
            CompleteScheduledFingerPose(dt);

            if (!IsGrabbing)
            {
                ApplyOpenHandPose(dt);
                return;
            }

            if (_activeBody == null || _activeBody.isKinematic)
            {
                BreakGrip(PhysicalHandGrabEndReason.InvalidTarget);
                ApplyOpenHandPose(dt);
                return;
            }

            EnsureRuntimeProxy();
            ResolveFingerSegments();

            UpdateVirtualHandPose(controllerPosition, controllerRotation, dt);
            UpdateArticulationTarget(controllerRotation, dt);
            SolveGrabbedBody(dt);

            if (IsGrabbing)
                ScheduleFingerPoseBatch();
        }

        private void Awake()
        {
            _cachedTransform = transform;
            EnsureRuntimeProxy();
            AllocatePersistentBuffers();
            ResolveFingerSegments();
            SyncDebugState();
        }

        private void OnDisable()
        {
            if (IsGrabbing)
                EndGrab(PhysicalHandGrabEndReason.Disabled);
        }

        private void OnDestroy()
        {
            if (_fingerCommands.IsCreated)
                _fingerCommands.Dispose(_fingerPoseHandle);
            if (_fingerHits.IsCreated)
                _fingerHits.Dispose(_fingerPoseHandle);
            if (_fingerPoses.IsCreated)
                _fingerPoses.Dispose(_fingerPoseHandle);
            if (_fingerOrigins.IsCreated)
                _fingerOrigins.Dispose(_fingerPoseHandle);
            if (_fingerDirections.IsCreated)
                _fingerDirections.Dispose(_fingerPoseHandle);
            if (_localKnuckleOffsets.IsCreated)
                _localKnuckleOffsets.Dispose(_fingerPoseHandle);
            if (_localFingerDirections.IsCreated)
                _localFingerDirections.Dispose(_fingerPoseHandle);
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

        private void AllocatePersistentBuffers()
        {
            if (_fingerCommands.IsCreated)
                return;

            // COLD ALLOC: NativeArray<SpherecastCommand>[5] — persistent finger spherecast commands — owner: PhysicalHandController
            _fingerCommands = new NativeArray<SpherecastCommand>(FingerCount, Allocator.Persistent);
            // COLD ALLOC: NativeArray<RaycastHit>[5] — persistent finger spherecast results — owner: PhysicalHandController
            _fingerHits = new NativeArray<RaycastHit>(FingerCount, Allocator.Persistent);
            // COLD ALLOC: NativeArray<FingerPoseData>[5] — persistent finger pose results — owner: PhysicalHandController
            _fingerPoses = new NativeArray<FingerPoseData>(FingerCount, Allocator.Persistent);
            // COLD ALLOC: NativeArray<float3>[5] — persistent finger origins — owner: PhysicalHandController
            _fingerOrigins = new NativeArray<float3>(FingerCount, Allocator.Persistent);
            // COLD ALLOC: NativeArray<float3>[5] — persistent finger directions — owner: PhysicalHandController
            _fingerDirections = new NativeArray<float3>(FingerCount, Allocator.Persistent);
            // COLD ALLOC: NativeArray<float3>[5] — persistent local knuckle offsets — owner: PhysicalHandController
            _localKnuckleOffsets = new NativeArray<float3>(FingerCount, Allocator.Persistent);
            // COLD ALLOC: NativeArray<float3>[5] — persistent local finger directions — owner: PhysicalHandController
            _localFingerDirections = new NativeArray<float3>(FingerCount, Allocator.Persistent);

            _localKnuckleOffsets[0] = DefaultThumbKnuckleOffset;
            _localKnuckleOffsets[1] = DefaultIndexKnuckleOffset;
            _localKnuckleOffsets[2] = DefaultMiddleKnuckleOffset;
            _localKnuckleOffsets[3] = DefaultRingKnuckleOffset;
            _localKnuckleOffsets[4] = DefaultLittleKnuckleOffset;
            _localFingerDirections[0] = DefaultThumbDirection;
            _localFingerDirections[1] = DefaultFingerDirection;
            _localFingerDirections[2] = DefaultFingerDirection;
            _localFingerDirections[3] = DefaultFingerDirection;
            _localFingerDirections[4] = DefaultFingerDirection;
        }

        private void ResolveFingerSegments()
        {
            if (_fingerSegmentsResolved)
                return;

            int segmentCount = fingerSegments != null ? fingerSegments.Length : 0;
            if (segmentCount > 0)
            {
                // COLD ALLOC: Quaternion[15] — cached authored finger local rotations — owner: PhysicalHandController
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
            if (rightHandAttachmentOverride != null)
                return rightHandAttachmentOverride;

            if (swimBlockoutRig == null)
                swimBlockoutRig = GetComponentInChildren<PlayerSwimBlockoutRig>(true);

            if (swimBlockoutRig != null)
                return swimBlockoutRig.RightHandAttachment;

            return null;
        }

        private void CompleteScheduledFingerPose(float dt)
        {
            if (!_fingerPoseScheduled)
                return;

            _fingerPoseHandle.Complete();
            _fingerPoseScheduled = false;
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

            Quaternion targetRotation = _baseFingerLocalRotations[segmentIndex] * Quaternion.Euler(-targetCurlDegrees, 0f, 0f);
            segment.localRotation = Quaternion.Slerp(segment.localRotation, targetRotation, blendT);
        }

        private void UpdateVirtualHandPose(Vector3 controllerPosition, Quaternion controllerRotation, float dt)
        {
            Vector3 controllerVelocity = Vector3.zero;
            if (_hasPreviousControllerPose)
                controllerVelocity = (controllerPosition - _previousControllerPosition) / dt;

            Vector3 effectiveControllerPosition = controllerPosition;
            if (_activeBody != null && _activeBody.mass > HandMaxCarryMass)
                effectiveControllerPosition += controllerVelocity * VelocityLeadFactor;

            if (_virtualHandMass > MinimumDeltaTime)
            {
                float damping = 2f * math.sqrt(math.max(_virtualHandMass * VirtualSpringK, MinimumDeltaTime)) * 0.9f;
                Vector3 springForce = (effectiveControllerPosition - _virtualHandPosition) * VirtualSpringK;
                Vector3 dampingForce = -_virtualHandVelocity * damping;
                Vector3 netAcceleration = (springForce + dampingForce) / math.max(_virtualHandMass, MinimumDeltaTime);
                _virtualHandVelocity += netAcceleration * dt;
                _virtualHandPosition += _virtualHandVelocity * dt;

                Vector3 lag = _virtualHandPosition - controllerPosition;
                lag.x = Mathf.Clamp(lag.x, -VirtualHandLagMax, VirtualHandLagMax);
                lag.y = Mathf.Clamp(lag.y, -VirtualHandLagMax, VirtualHandLagMax);
                lag.z = Mathf.Clamp(lag.z, -VirtualHandLagMax, VirtualHandLagMax);
                _virtualHandPosition = controllerPosition + lag;
            }
            else
            {
                _virtualHandPosition = effectiveControllerPosition;
                _virtualHandVelocity = controllerVelocity;
            }

            if (_runtimeRoot != null)
            {
                _runtimeRoot.position = _virtualHandPosition;
                _runtimeRoot.rotation = Quaternion.identity;
            }

            _previousControllerPosition = controllerPosition;
            _previousControllerRotation = controllerRotation;
            _hasPreviousControllerPose = true;
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

                angularVelocityDegrees = axis.normalized * (angleDegrees / dt);
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
            _previousTargetLocalRotation = localTargetRotation;
        }

        private void SolveGrabbedBody(float dt)
        {
            Rigidbody body = _activeBody;
            if (body == null)
                return;

            Vector3 handPosition = _runtimeGripPoint != null ? _runtimeGripPoint.position : _virtualHandPosition;
            Quaternion handRotation = _runtimeGripPoint != null ? _runtimeGripPoint.rotation : Quaternion.identity;
            Vector3 bodyPosition = body.worldCenterOfMass;
            Vector3 linearError = handPosition - bodyPosition;
            _currentSeparation = linearError.magnitude;

            if (_currentSeparation > GripBreakDistance)
            {
                BreakGrip(PhysicalHandGrabEndReason.GripBroken);
                return;
            }

            _disconnectArmed = _currentSeparation > GripWarnDistance;
            float gainMultiplier = 1f;
            if (_disconnectArmed)
                gainMultiplier = 1f - math.saturate((_currentSeparation - GripWarnDistance) / math.max(GripBreakDistance - GripWarnDistance, MinimumDeltaTime));

            Vector3 controllerVelocity = (handPosition - _previousControllerPosition) / dt;
            Vector3 velocityError = controllerVelocity - body.linearVelocity;
            float kp = LinearNaturalFrequency * LinearNaturalFrequency * gainMultiplier;
            float kd = 2f * LinearNaturalFrequency * gainMultiplier;
            Vector3 acceleration = (linearError * kp) + (velocityError * kd);

            if (!IsFinite(acceleration))
                acceleration = Vector3.zero;

            Vector3 deltaVelocity = ClampMagnitude(acceleration * dt, MaxDeltaVelocity * gainMultiplier);
            if (deltaVelocity.sqrMagnitude > 0.0000001f)
                PhysicsForceRouter.QueueForce(body, deltaVelocity, ForceMode.VelocityChange);

            Quaternion deltaRotation = handRotation * Quaternion.Inverse(body.rotation);
            deltaRotation.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (!IsFinite(axis) || axis.sqrMagnitude < 0.000001f)
            {
                axis = Vector3.up;
                angleDegrees = 0f;
            }

            if (angleDegrees > 180f)
                angleDegrees -= 360f;

            float angleRadians = angleDegrees * RadiansPerDegree;
            Vector3 angularError = axis.normalized * angleRadians;
            Vector3 targetAngularVelocity = ResolveTargetAngularVelocityRadians(handRotation, dt);
            Vector3 angularVelocityError = targetAngularVelocity - body.angularVelocity;
            float angularKp = AngularNaturalFrequency * AngularNaturalFrequency * gainMultiplier;
            float angularKd = 2f * AngularNaturalFrequency * gainMultiplier;
            Vector3 angularAcceleration = (angularError * angularKp) + (angularVelocityError * angularKd);

            if (!IsFinite(angularAcceleration))
                angularAcceleration = Vector3.zero;

            Vector3 deltaAngularVelocity = ClampMagnitude(angularAcceleration * dt, MaxDeltaAngularVelocity * gainMultiplier);
            if (deltaAngularVelocity.sqrMagnitude > 0.0000001f)
                PhysicsForceRouter.QueueTorque(body, deltaAngularVelocity, ForceMode.VelocityChange);

            SyncDebugState();
        }

        private Vector3 ResolveTargetAngularVelocityRadians(Quaternion targetRotation, float dt)
        {
            Quaternion delta = targetRotation * Quaternion.Inverse(_previousTargetLocalRotation);
            delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (!IsFinite(axis) || axis.sqrMagnitude < 0.000001f)
                return Vector3.zero;

            if (angleDegrees > 180f)
                angleDegrees -= 360f;

            return axis.normalized * ((angleDegrees * RadiansPerDegree) / dt);
        }

        private void ScheduleFingerPoseBatch()
        {
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
                LocalKnuckleOffsets = _localKnuckleOffsets,
                LocalFingerDirections = _localFingerDirections,
                Commands = _fingerCommands,
                Origins = _fingerOrigins,
                Directions = _fingerDirections
            };

            ProcessFingerHitsJob processJob = new ProcessFingerHitsJob
            {
                CastLength = FingerCastLength,
                Hits = _fingerHits,
                Origins = _fingerOrigins,
                Directions = _fingerDirections,
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
            float massRatio = math.saturate(objectMass / HandMaxCarryMass);
            return math.lerp(0f, VirtualHandMaxMass, massRatio);
        }

        private void SyncDebugState()
        {
            _debugIsGrabbing = IsGrabbing;
            _debugDisconnectArmed = _disconnectArmed;
            _debugGripBroken = _gripBroken;
            _debugSeparation = _currentSeparation;
            _debugVirtualHandMass = _virtualHandMass;
            _debugGrabbedBodyName = _activeBody != null ? _activeBody.name : null;
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

            return value.normalized * maxMagnitude;
        }

        private static Vector3 ClampPerAxis(Vector3 value, float axisLimit)
        {
            value.x = Mathf.Clamp(value.x, -axisLimit, axisLimit);
            value.y = Mathf.Clamp(value.y, -axisLimit, axisLimit);
            value.z = Mathf.Clamp(value.z, -axisLimit, axisLimit);
            return value;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
        }

        [BurstCompile]
        private struct BuildFingerSpherecastCommandsJob : IJobParallelFor
        {
            public float3 HandPosition;
            public quaternion HandRotation;
            public float3 TargetPosition;
            public float CastRadius;
            public float CastLength;
            public QueryParameters QueryParameters;

            [ReadOnly] public NativeArray<float3> LocalKnuckleOffsets;
            [ReadOnly] public NativeArray<float3> LocalFingerDirections;

            [WriteOnly] public NativeArray<SpherecastCommand> Commands;
            [WriteOnly] public NativeArray<float3> Origins;
            [WriteOnly] public NativeArray<float3> Directions;

            public void Execute(int index)
            {
                float3 origin = HandPosition + math.rotate(HandRotation, LocalKnuckleOffsets[index]);
                float3 fallbackDirection = math.rotate(HandRotation, LocalFingerDirections[index]);
                float3 targetDirection = math.normalizesafe(TargetPosition - origin, fallbackDirection);
                Origins[index] = origin;
                Directions[index] = targetDirection;
                Commands[index] = new SpherecastCommand(origin, CastRadius, targetDirection, QueryParameters, CastLength);
            }
        }

        [BurstCompile]
        private struct ProcessFingerHitsJob : IJobParallelFor
        {
            public float CastLength;

            [ReadOnly] public NativeArray<RaycastHit> Hits;
            [ReadOnly] public NativeArray<float3> Origins;
            [ReadOnly] public NativeArray<float3> Directions;

            [WriteOnly] public NativeArray<FingerPoseData> Output;

            public void Execute(int index)
            {
                RaycastHit hit = Hits[index];
                float3 origin = Origins[index];
                float3 direction = Directions[index];
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

        private struct FingerPoseData
        {
            public float3 TipPosition;
            public float3 TipNormal;
            public float BendAngle;
        }
    }
}
