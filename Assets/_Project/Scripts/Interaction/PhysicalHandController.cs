// ============================================================================
// HECTON-8 - PhysicalHandController.cs
// Heavy-object articulation hand proxy with zero-GC direct finger-pose solve.
// ============================================================================

namespace Hecton8.Interaction
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Core.Memory;
    using Hecton8.Gameplay;
    using Hecton8.Tools;
    using Hecton8.World;
    using Unity.Collections;
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

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct FingerRayDefinition
    {
        [FieldOffset(0)] public float3 LocalKnuckleOffset;
        [FieldOffset(12)] public float3 LocalFingerDirection;
        [FieldOffset(24)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct FingerRayRuntime
    {
        [FieldOffset(0)] public float3 Origin;
        [FieldOffset(12)] public float3 Direction;
        [FieldOffset(24)] private ulong _pad0;
    }

    /// <summary>
    /// Owns the articulation-backed heavy-grab proxy and zero-GC finger-pose solve.
    /// Driven explicitly from <see cref="PhysicalInteractionHandler"/> inside FixedTick.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/Physical Hand Controller")]
    public sealed class PhysicalHandController : MonoBehaviour, IPhysicalHandIkTargetSink, IGlobalRegistryHotSwapListener
    {
        private struct SomaticIKSolver
        {
            public double3 RuntimeOriginAup;
            public double3 ShoulderAup;
            public double3 TargetAup;
            public quaternion TargetRotation;
            public float UpperArmLengthMeters;
            public float LowerArmLengthMeters;
            public float GlobalQualityWeight;
            public double3 ResolvedAup;
            public float4x4 HandMatrix;
            public byte Success;

            public void Execute()
            {
                float3 shoulder = ToRuntimeLocal(ShoulderAup, RuntimeOriginAup);
                float3 target = ToRuntimeLocal(TargetAup, RuntimeOriginAup);
                float upper = math.max(0.05f, UpperArmLengthMeters);
                float lower = math.max(0.05f, LowerArmLengthMeters);
                int iterations = ResolveSomaticIterations(GlobalQualityWeight);
                SolveTwoBoneFabrik(shoulder, target, upper, lower, iterations, out float3 hand);
                double3 resolved = RuntimeOriginAup + new double3(hand.x, hand.y, hand.z);
                bool valid = math.all(math.isfinite(resolved));
                quaternion rotation = math.all(math.isfinite(TargetRotation.value))
                    ? TargetRotation
                    : quaternion.identity;
                float4x4 matrix = float4x4.TRS(hand, rotation, new float3(1f));
                ResolvedAup = resolved;
                HandMatrix = matrix;
                Success = valid ? (byte)1 : (byte)0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static int ResolveSomaticIterations(float qualityWeight)
            {
                float q = math.saturate(math.select(1f, qualityWeight, math.isfinite(qualityWeight)));
                float curved = q * q * (3f - (2f * q));
                return math.clamp((int)math.round(math.lerp(2f, 4f, curved)), 2, 4);
            }

            internal static void SolveTwoBoneFabrik(
                float3 shoulder,
                float3 target,
                float upper,
                float lower,
                int iterations,
                out float3 hand)
            {
                float3 toTarget = target - shoulder;
                float targetDistanceSq = math.lengthsq(toTarget);
                float totalLength = upper + lower;
                if (targetDistanceSq >= totalLength * totalLength)
                {
                    float3 direction = Normalize(toTarget, new float3(0f, 0f, 1f));
                    hand = shoulder + (direction * totalLength);
                    return;
                }

                float3 pole = ResolvePoleDirection(toTarget);
                float3 elbow = shoulder + (pole * upper);
                hand = target;
                int safeIterations = math.clamp(iterations, 1, 4);
                for (int i = 0; i < safeIterations; i++)
                {
                    hand = target;
                    elbow = hand + (Normalize(elbow - hand, -pole) * lower);
                    elbow = shoulder + (Normalize(elbow - shoulder, pole) * upper);
                    hand = elbow + (Normalize(hand - elbow, Normalize(toTarget, new float3(0f, 0f, 1f))) * lower);

                    if (math.lengthsq(hand - target) <= 0.000001f)
                        break;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static float3 ToRuntimeLocal(double3 value, double3 origin)
            {
                double3 delta = value - origin;
                float3 result = new float3((float)delta.x, (float)delta.y, (float)delta.z);
                return math.all(math.isfinite(result)) ? result : float3.zero;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static float3 ResolvePoleDirection(float3 toTarget)
            {
                float3 forward = Normalize(toTarget, new float3(0f, 0f, 1f));
                float3 up = math.abs(forward.y) > 0.92f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
                float3 right = Normalize(math.cross(up, forward), new float3(1f, 0f, 0f));
                return Normalize(math.cross(forward, right), up);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static float3 Normalize(float3 value, float3 fallback)
            {
                float lengthSq = math.lengthsq(value);
                return lengthSq > 0.000001f && math.isfinite(lengthSq)
                    ? value * math.rsqrt(lengthSq)
                    : fallback;
            }
        }

        private struct FingerPoseFabrikSolver
        {
            public FingerRayDefinition[] Definitions;
            public FingerRayRuntime[] Runtime;
            public FingerPoseData[] Poses;
            public int Count;
            public float3 HandPosition;
            public quaternion HandRotation;
            public float3 TargetPosition;
            public float CastLength;
            public float GlobalQualityWeight;

            public void Execute()
            {
                if (Definitions == null || Runtime == null || Poses == null)
                    return;

                int capacity = math.min(math.min(Definitions.Length, Runtime.Length), Poses.Length);
                int count = math.clamp(Count, 0, capacity);
                float safeCastLength = math.isfinite(CastLength)
                    ? math.max(CastLength, MinimumDeltaTime)
                    : MinimumDeltaTime;
                quaternion handRotation = math.all(math.isfinite(HandRotation.value))
                    ? HandRotation
                    : quaternion.identity;
                int iterations = ResolveFingerIterations(GlobalQualityWeight);

                for (int index = 0; index < count; index++)
                {
                    FingerRayDefinition definition = Definitions[index];
                    float3 localKnuckleOffset = math.all(math.isfinite(definition.LocalKnuckleOffset))
                        ? definition.LocalKnuckleOffset
                        : float3.zero;
                    float3 localFingerDirection = Normalize(definition.LocalFingerDirection, new float3(0f, 0f, 1f));
                    float3 origin = HandPosition + math.rotate(handRotation, localKnuckleOffset);
                    if (!math.all(math.isfinite(origin)))
                        origin = HandPosition;

                    float3 fallbackDirection = Normalize(math.rotate(handRotation, localFingerDirection), new float3(0f, 0f, 1f));
                    float3 direction = Normalize(TargetPosition - origin, fallbackDirection);
                    if (!math.all(math.isfinite(direction)))
                        direction = fallbackDirection;

                    float targetDistanceSq = math.lengthsq(TargetPosition - origin);
                    float targetDistance = targetDistanceSq > 0.000001f && math.isfinite(targetDistanceSq)
                        ? targetDistanceSq * math.rsqrt(targetDistanceSq)
                        : 0f;
                    float bendAngle = math.saturate(1f - (targetDistance / safeCastLength));
                    float solveDistance = math.lerp(
                        safeCastLength,
                        math.max(safeCastLength * 0.35f, MinimumDeltaTime),
                        bendAngle);
                    float3 target = origin + direction * solveDistance;
                    float3 tip = SolveFingerFabrik(origin, target, fallbackDirection, solveDistance, iterations, out float3 normal);
                    Runtime[index] = new FingerRayRuntime
                    {
                        Origin = origin,
                        Direction = direction
                    };
                    Poses[index] = new FingerPoseData
                    {
                        BendAngle = bendAngle,
                        TipPosition = tip,
                        TipNormal = normal
                    };
                }
            }

            private static float3 SolveFingerFabrik(
                float3 root,
                float3 target,
                float3 fallbackDirection,
                float reach,
                int iterations,
                out float3 normal)
            {
                float segment = reach * 0.33333334f;
                float3 direction = Normalize(target - root, fallbackDirection);
                float3 joint0 = root + direction * segment;
                float3 joint1 = root + direction * (segment + segment);
                float3 tip = target;
                int safeIterations = math.clamp(iterations, 1, 4);

                for (int i = 0; i < safeIterations; i++)
                {
                    tip = target;
                    joint1 = tip + Normalize(joint1 - tip, -direction) * segment;
                    joint0 = joint1 + Normalize(joint0 - joint1, -direction) * segment;
                    joint0 = root + Normalize(joint0 - root, direction) * segment;
                    joint1 = joint0 + Normalize(joint1 - joint0, direction) * segment;
                    tip = joint1 + Normalize(tip - joint1, direction) * segment;
                }

                normal = -Normalize(tip - joint1, direction);
                return tip;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int ResolveFingerIterations(float qualityWeight)
            {
                float q = math.saturate(math.select(1f, qualityWeight, math.isfinite(qualityWeight)));
                float curved = q * q * (3f - (2f * q));
                return math.clamp((int)math.round(math.lerp(2f, 4f, curved)), 2, 4);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float3 Normalize(float3 value, float3 fallback)
            {
                float lengthSq = math.lengthsq(value);
                return lengthSq > 0.000001f && math.isfinite(lengthSq)
                    ? value * math.rsqrt(lengthSq)
                    : fallback;
            }
        }

        private static int s_x001PhysicalHandControllerSignalPushDropCount;
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
        private const int FingerPoseMinIntervalFrames = 1;
        private const int FingerPoseMaxIntervalFrames = 6;
        private const float MaxSupportedGrabMass = 500f;
        private const float MinimumDeltaTime = 0.0001f;
        private const float MaximumSafeDeltaTime = 0.02f;
        private const float RadiansPerDegree = 0.0174532925f;
        private const int SuitOverlapCapacity = 8;
        private const int SuitOverlapStaleFrameLimit = 4;
        private const int KinematicBridgeColdRetryIntervalFrames = 30;
        private const ulong KinematicBridgeMutationGuardMask = VRInteractionKinematicBridgeConstants.MutationGuardMask;
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
        private const float BallastLeverMaxDegrees = 90f;
        private const float BallastLeverHapticDeadband01 = 0.03f;
        private const float DegreesPerRadian = 57.29578f;
        private const byte LeftMotorMask = 0b0001;
        private const byte RightMotorMask = 0b0010;
        private const byte BothMotorMask = LeftMotorMask | RightMotorMask;
        private const byte HandContactHapticPriority = 2;
        private const byte CriticalHapticPriority = 3;
        private const byte CriticalHapticBlendMode = ToolHapticsRuntime.BlendModeMax;
        private const uint SomaticBallastLeverSourceHash = 0x53424c56u; // SBLV
        private const uint SomaticMaintenanceSourceHash = 0x534d4149u; // SMAI
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const string InvalidMotionResetMessage = "[PhysicalHandController] NaN/Inf detected. Motion reset.";
#endif
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

        [Tooltip("Uses the SHINOBU_271 kinematic SDF bridge instead of ArticulationBody/Rigidbody hand proxies.")]
        [SerializeField] private bool useKinematicSdfHandBridge = true;

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
        private uint _handFixedFrameIndex;
        private uint _lastSuitDamageFrame = uint.MaxValue;
        private bool _suitOverlapSaturationLogged;
        private FingerPoseData[] _fingerPoses;
        private FingerRayDefinition[] _fingerRayDefinitions;
        private FingerRayRuntime[] _fingerRayRuntime;
        private VRInteractionSocketDTO[] _kinematicSocketSnapshot;
        private Collider[] _suitOverlapResults;
        private int[] _suitOverlapStaleFrames;
        private Quaternion[] _baseFingerLocalRotations;
        private string _cachedGrabbedBodyName;
        private Collider _activeBodyCollider;
        private Transform _suitHandTransform;
        private Rigidbody _suitHandBody;
        private SphereCollider _suitHandCollider;
        private PhysicalHandSuitCollisionShellProxy _suitShellProxy;
        private Transform _cachedInteractionProbeColliderSource;
        private Collider _cachedInteractionProbeCollider;
        private IPhysicsService _physicsService;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private ISubmarineRuntimeContext _submarineRuntimeContext;
        private SubmarineCoreDirector _submarineCoreDirector;
        private InputDispatcher _inputDispatcher;
        private IDataVault _kinematicBridgeVault;
        private Hecton8.Core.Contracts.IVoxelSonarSdfReadModel _kinematicSdfReadModel;
        private Hecton8.Core.Contracts.IVoxelSonarSdfReadLeaseModel _kinematicSdfReadLeaseModel;
        private Vector3 _harvestSnapPosition;
        private float3 _lastXRIdleGripPosition;
        private float3 _lastKinematicSurfaceNormal = new float3(0f, 1f, 0f);
        private Quaternion _harvestSnapRotation = Quaternion.identity;
        private Quaternion _previousPlatformInverseRotation = Quaternion.identity;
        private float _harvestSnapTimer;
        private float _harvestSnapDuration;
        private float _lastKinematicPenetration;
        private float _lastSubmittedBallastRatio = -1f;
        private bool _harvestSnapActive;
        private bool _registeredHotSwapListener;
        private bool _hasPlatformFrame;
        private int _terminalSnapSourceId;
        private Matrix4x4 _previousPlatformWorldToLocal = Matrix4x4.identity;
        private uint _lastKinematicVelocitySignalFrame = uint.MaxValue;
        private uint _lastKinematicFaultFrame = uint.MaxValue;
        private int _lastKinematicBridgeCacheAttempt = -KinematicBridgeColdRetryIntervalFrames;
        private int _kinematicBridgeCacheAttempt;
        private uint _kinematicBridgeFrameIndex;
        private uint _lastKinematicSocketId;
        private bool _kinematicBridgeReady;

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

        /// <summary>True while transient hand poses need owner fixed-step advancement.</summary>
        internal bool RequiresFixedTick => _harvestSnapActive;

        /// <summary>True while deferred finger jobs need a dispatcher-owned late-frame pass.</summary>
        internal bool RequiresLateFrameTick => _fingerPoseScheduled;

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
            if (!enabled)
            {
                DisableSuitCollisionShell(forceClear: true);
                return;
            }

            if (useKinematicSdfHandBridge)
            {
                DisableSuitCollisionShell();
                return;
            }

            EnsureSuitCollisionShell();
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
            Transform attachment = ReadRightHandAttachment();
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
            _terminalSnapSourceId = 0;
            return true;
        }

        /// <summary>
        /// Starts a short physical terminal pose latch consumed by the existing hand snap path.
        /// </summary>
        public bool TryBeginPoseSnap(Vector3 worldPosition, Quaternion worldRotation, float durationSeconds, int sourceId)
        {
            if (IsGrabbing || !IsFinite(worldPosition) || !IsFinite(worldRotation))
                return false;

            _harvestSnapPosition = worldPosition;
            _harvestSnapRotation = worldRotation;
            _harvestSnapDuration = ResolveHarvestSnapDuration(durationSeconds);
            _harvestSnapTimer = _harvestSnapDuration;
            _harvestSnapActive = true;
            _terminalSnapSourceId = sourceId;
            return true;
        }

        public void SetTerminalHandTarget(in PhysicalHandIkTarget target)
        {
            TryBeginPoseSnap(target.WorldPosition, target.WorldRotation, target.HoldSeconds, target.SourceId);
        }

        public void ClearTerminalHandTarget(int sourceId)
        {
            if (_terminalSnapSourceId == 0 || _terminalSnapSourceId != sourceId)
                return;

            CancelHarvestSnap();
            _terminalSnapSourceId = 0;
        }

        /// <summary>
        /// Cancels the transient flora pick latch without changing grab state.
        /// </summary>
        public void CancelHarvestSnap()
        {
            _harvestSnapActive = false;
            _harvestSnapTimer = 0f;
            _harvestSnapDuration = 0f;
            _terminalSnapSourceId = 0;
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

            Transform attachment = ReadRightHandAttachment();
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
            if (_runtimeGripPoint != null)
                _runtimeGripPoint.position = _virtualHandPosition;

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
                    QueueBodyVelocityTarget(_activeBody, IsFinite(clampedVelocity) ? clampedVelocity : Vector3.zero);
                    _physicsService?.QueueAngularVelocitySet(_activeBody, Vector3.zero, wake: false);
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
            if (dt <= 0f)
            {
                if (!_fingerPoseScheduled)
                    _lastFingerPoseDeltaTime = 0f;
                return;
            }

            _lastFingerPoseDeltaTime = dt;
            _handFixedFrameIndex++;
            AdvanceHandHapticCooldowns(dt);
            ApplyPlatformRelativeCarry(ref controllerPosition, ref controllerRotation);
            AdvanceHarvestSnap(dt);
            if (ShouldBypassXRHandKinematicUpdate())
            {
                DecayWallRecoilOffset(dt);
                ApplyOpenHandPose(dt);
                return;
            }

            ApplyHarvestSnapPose(ref controllerPosition, ref controllerRotation);

            if (useKinematicSdfHandBridge && IsFinite(controllerPosition) && IsFinite(controllerRotation))
                StepKinematicSdfBridge(ref controllerPosition, controllerRotation, dt);
            else if (IsFinite(controllerPosition) && IsFinite(controllerRotation))
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
                if (ShouldScheduleFingerPoseBatch())
                    ScheduleFingerPoseBatch();

                FinalizeControllerPoseState(controllerPosition, controllerRotation);
            }
        }

        public bool TrySubmitBallastLeverAngle(float leverAngleDegrees, uint sourceHash = SomaticBallastLeverSourceHash)
        {
            float safeAngle = math.isfinite(leverAngleDegrees) ? math.clamp(leverAngleDegrees, 0f, BallastLeverMaxDegrees) : 0f;
            float ratio = math.saturate(safeAngle * math.rcp(BallastLeverMaxDegrees));
            SubmarineCoreDirector core = _submarineCoreDirector;
            if (core == null)
                return false;

            bool accepted = core.TrySubmitBallastLeverAngle(safeAngle, sourceHash);
            if (accepted && math.abs(ratio - _lastSubmittedBallastRatio) >= BallastLeverHapticDeadband01)
            {
                _lastSubmittedBallastRatio = ratio;
                PublishSomaticHaptic(ratio, 0.06f, 0.28f, HapticRequest.ChannelGearScrape, HapticRequest.FlagMicroVibration, sourceHash);
            }

            return accepted;
        }

        public bool TryRecordVesselMaintenanceAction(uint panelBitIndex, uint sourceHash = SomaticMaintenanceSourceHash)
        {
            SubmarineCoreDirector core = _submarineCoreDirector;
            if (core == null)
                return false;

            bool accepted = core.TryRecordVesselMaintenanceAction(panelBitIndex, sourceHash);
            if (accepted)
                PublishSomaticHaptic(0.35f, 0.04f, 0.55f, HapticRequest.ChannelLightThud, HapticRequest.FlagLightThud, sourceHash);

            return accepted;
        }

        private void PublishSomaticHaptic(float intensity01, float durationSeconds, float frequency01, byte channel, byte flags, uint sourceHash)
        {
            HapticRequest request = default;
            request.Intensity01 = math.saturate(math.select(0f, intensity01, math.isfinite(intensity01)));
            request.DurationSeconds = math.max(0f, math.select(0f, durationSeconds, math.isfinite(durationSeconds)));
            request.Frequency01 = math.saturate(math.select(0f, frequency01, math.isfinite(frequency01)));
            request.SourceHash = sourceHash != 0u ? sourceHash : SomaticBallastLeverSourceHash;
            request.Frame = _handFixedFrameIndex;
            request.Channel = channel;
            request.Flags = flags;
            SignalBus<HapticRequest>.TryPushTracked(in request, ref s_x001PhysicalHandControllerSignalPushDropCount);
        }

        internal void LateFrameTick()
        {
            CompleteScheduledFingerPose(_lastFingerPoseDeltaTime);
        }

        private bool ShouldBypassXRHandKinematicUpdate()
        {
            if (!HectonXRRuntimeState.IsXRActive)
                return false;

            if (IsGrabbing || _harvestSnapActive || enableSuitCollisionShell || _suitContactActive)
            {
                _hasXRIdleGripPoseSample = false;
                return false;
            }

            InputDispatcher dispatcher = _inputDispatcher;
            if (dispatcher == null)
            {
                _hasXRIdleGripPoseSample = false;
                return true;
            }

            if (!TryResolveXRControllerIndex(handSide, out byte controllerIndex))
            {
                _hasXRIdleGripPoseSample = false;
                return true;
            }

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

            if (state.HasActiveInput())
            {
                _hasXRIdleGripPoseSample = false;
                return false;
            }

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
            CachePhysicsServiceCold();
            CachePlayerRuntimeContextCold();
            CacheSubmarineRuntimeContextCold();
            CacheInputDispatcherCold();
            CacheSwimBlockoutRigCold();
            CacheKinematicBridgeCold(true);
            EnsureRuntimeProxy();
            CacheInteractionProbeColliderCold();
            CacheOpposingHandAttachmentCold();
            AllocatePersistentBuffersCold();
            ResolveFingerSegments();
            if (enableSuitCollisionShell)
                EnsureSuitCollisionShell();
            SyncDebugState();
        }

        private void OnEnable()
        {
            CachePhysicsServiceCold();
            CachePlayerRuntimeContextCold();
            CacheSubmarineRuntimeContextCold();
            CacheInputDispatcherCold();
            TryRegisterHotSwapListener();
            CacheKinematicBridgeCold(true);
            CacheInteractionProbeColliderCold();
            AllocatePersistentBuffersCold();
            if (enableSuitCollisionShell)
                EnsureSuitCollisionShell();
        }

        private void OnDisable()
        {
            CancelHarvestSnap();
            _cachedInteractionProbeColliderSource = null;
            _cachedInteractionProbeCollider = null;
            _playerRuntimeContext = null;
            _submarineRuntimeContext = null;
            _submarineCoreDirector = null;
            _hasPlatformFrame = false;
            _hasXRIdleGripPoseSample = false;
            _kinematicBridgeVault = null;
            _kinematicBridgeReady = false;
            DisableSuitCollisionShell();
            if (IsGrabbing)
                EndGrab(PhysicalHandGrabEndReason.Disabled);

            TryUnregisterHotSwapListener();
            DisposePersistentBuffers();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            DisposePersistentBuffers();
            DisableSuitCollisionShell();
            if (_suitShellProxy != null)
                _suitShellProxy.Shutdown();

            if (_suitHandTransform != null)
                Destroy(_suitHandTransform.gameObject);

            _suitHandTransform = null;
            _suitHandBody = null;
            _suitHandCollider = null;
            _suitShellProxy = null;
            _suitCollisionShellCreated = false;
            _suitOverlapResults = null;
            _suitOverlapSaturationLogged = false;

            if (_runtimeGripPoint != null && (_runtimeRoot == null || _runtimeGripPoint != _runtimeRoot))
                Destroy(_runtimeGripPoint.gameObject);

            if (_runtimeRoot != null)
                Destroy(_runtimeRoot.gameObject);

            _runtimeRoot = null;
            _runtimeGripPoint = null;
            _runtimeHandBody = null;
            _runtimeProxyCreated = false;
            _physicsService = null;
            _playerRuntimeContext = null;
            _submarineRuntimeContext = null;
            _submarineCoreDirector = null;
            _inputDispatcher = null;
            _kinematicBridgeVault = null;
            _kinematicSdfReadModel = null;
            _kinematicSdfReadLeaseModel = null;
            _kinematicBridgeReady = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Physics)
            {
                _physicsService = currentService as IPhysicsService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Submarine)
            {
                _submarineRuntimeContext = currentService as ISubmarineRuntimeContext;
                _submarineCoreDirector = _submarineRuntimeContext as SubmarineCoreDirector;
                _hasPlatformFrame = false;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Input)
            {
                _inputDispatcher = currentService as InputDispatcher;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                _kinematicBridgeVault = currentService as IDataVault;
                _kinematicBridgeReady = false;
                _lastKinematicBridgeCacheAttempt = _kinematicBridgeCacheAttempt - KinematicBridgeColdRetryIntervalFrames;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.VoxelEngineRuntime)
            {
                _kinematicSdfReadModel = currentService as Hecton8.Core.Contracts.IVoxelSonarSdfReadModel;
                _kinematicSdfReadLeaseModel = currentService as Hecton8.Core.Contracts.IVoxelSonarSdfReadLeaseModel;
            }
        }

        private void CachePhysicsServiceCold()
        {
            _physicsService = GlobalRegistry.Physics;
        }

        private void CachePlayerRuntimeContextCold()
        {
            _playerRuntimeContext = GlobalRegistry.Player;
        }

        private void CacheSubmarineRuntimeContextCold()
        {
            _submarineRuntimeContext = GlobalRegistry.Submarine;
            _submarineCoreDirector = _submarineRuntimeContext as SubmarineCoreDirector;
            _hasPlatformFrame = false;
        }

        private void CacheInputDispatcherCold()
        {
            _inputDispatcher = GlobalRegistry.RegisteredInput as InputDispatcher;
            if (_inputDispatcher == null)
                InputDispatcher.TryResolveActiveRuntime(ref _inputDispatcher);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void EnsureRuntimeProxy()
        {
            if (_runtimeProxyCreated)
                return;

            // COLD ALLOC: GameObject[1] — persistent articulation root for physical hand velocity drive — owner: PhysicalHandController
            GameObject rootObject = new GameObject("[PhysicalHandRuntimeRoot]");
            rootObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
            _runtimeRoot = rootObject.transform;
            Transform initialAttachment = CacheRightHandAttachmentCold();
            _runtimeRoot.position = initialAttachment != null ? initialAttachment.position : _cachedTransform.position;
            _runtimeRoot.rotation = Quaternion.identity;
            if (!useKinematicSdfHandBridge)
            {
                ArticulationBody runtimeRootBody = rootObject.AddComponent<ArticulationBody>();
                runtimeRootBody.immovable = true;
                runtimeRootBody.useGravity = false;
                runtimeRootBody.linearDamping = 0f;
                runtimeRootBody.angularDamping = 0f;
            }

            // COLD ALLOC: GameObject[1] — persistent articulation joint proxy for physical hand velocity drive — owner: PhysicalHandController
            GameObject handObject = new GameObject(useKinematicSdfHandBridge ? "[VRKinematicHandRuntimeTarget]" : "[PhysicalHandRuntimeJoint]");
            handObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
            Transform handTransform = handObject.transform;
            handTransform.position = _runtimeRoot.position;
            handTransform.rotation = Quaternion.identity;

            if (useKinematicSdfHandBridge)
            {
                _runtimeHandBody = null;
                _runtimeGripPoint = handTransform;
                _runtimeProxyCreated = true;
                return;
            }

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

            if (useKinematicSdfHandBridge)
            {
                DisableSuitCollisionShell();
                return;
            }

            if (_suitOverlapResults == null || _suitOverlapResults.Length != SuitOverlapCapacity)
            {
                // COLD ALLOC: Collider[8] - zero-GC physical hand suit contact overlap buffer - owner: PhysicalHandController
                _suitOverlapResults = new Collider[SuitOverlapCapacity];
            }

            if (_suitOverlapStaleFrames == null || _suitOverlapStaleFrames.Length != SuitOverlapCapacity)
            {
                // COLD ALLOC: int[8] - stale-frame counters for physical hand suit contact candidates - owner: PhysicalHandController
                _suitOverlapStaleFrames = new int[SuitOverlapCapacity];
            }

            if (_suitCollisionShellCreated)
            {
                if (_suitHandCollider != null)
                {
                    _suitHandCollider.isTrigger = true;
                    _suitHandCollider.radius = ResolveSuitCollisionProbeRadius();
                    _suitHandCollider.enabled = enableSuitCollisionShell;
                }

                if (_suitShellProxy != null)
                    _suitShellProxy.Initialize(this);

                return;
            }

            // COLD ALLOC: GameObject[1] - optional VR physical hand suit trigger shell - owner: PhysicalHandController
            GameObject shellObject = new GameObject("[PhysicalHandSuitCollisionShell]");
            shellObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor;
            if (!TryResolveSuitCollisionShellLayer(out int shellLayer))
            {
                enableSuitCollisionShell = false;
                Destroy(shellObject);
                DisableSuitCollisionShell(forceClear: true);
                return;
            }

            shellObject.layer = shellLayer;

            _suitHandTransform = shellObject.transform;
            Transform reference = _runtimeGripPoint != null ? _runtimeGripPoint : _runtimeRoot;
            _suitHandTransform.position = reference != null ? reference.position : _cachedTransform.position;
            _suitHandTransform.rotation = reference != null ? reference.rotation : _cachedTransform.rotation;

            _suitHandBody = shellObject.AddComponent<Rigidbody>();
            _suitHandBody.isKinematic = true;
            _suitHandBody.useGravity = false;
            _suitHandBody.detectCollisions = true;
            _suitHandBody.interpolation = RigidbodyInterpolation.Interpolate;
            _suitHandBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _suitHandBody.maxDepenetrationVelocity = 3f;

            _suitHandCollider = shellObject.AddComponent<SphereCollider>();
            _suitHandCollider.isTrigger = true;
            _suitHandCollider.radius = ResolveSuitCollisionProbeRadius();
            _suitHandCollider.enabled = enableSuitCollisionShell;

            _suitShellProxy = shellObject.AddComponent<PhysicalHandSuitCollisionShellProxy>();
            _suitShellProxy.Initialize(this);

            _suitCollisionShellCreated = true;
        }

        private void DisableSuitCollisionShell(bool forceClear = false)
        {
            bool disabledShell = false;
            if (_suitHandCollider != null && _suitHandCollider.enabled)
            {
                _suitHandCollider.enabled = false;
                disabledShell = true;
            }

            if (!forceClear && !_suitContactActive && !disabledShell && !_suitOverlapSaturationLogged)
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
            {
                _suitOverlapResults[i] = null;
                if (_suitOverlapStaleFrames != null && i < _suitOverlapStaleFrames.Length)
                    _suitOverlapStaleFrames[i] = 0;
            }
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
                _suitHandCollider.isTrigger = true;
                _suitHandCollider.radius = radius;
                _suitHandCollider.enabled = true;
            }

            float radiusSq = radius * radius;
            float strongestPenetration = 0f;
            Vector3 strongestNormal = Vector3.zero;
            int safeCount = _suitOverlapResults.Length;
            for (int i = 0; i < safeCount; i++)
            {
                Collider collider = _suitOverlapResults[i];
                if (!IsSuitCollisionCandidate(collider))
                {
                    _suitOverlapResults[i] = null;
                    if (_suitOverlapStaleFrames != null && i < _suitOverlapStaleFrames.Length)
                        _suitOverlapStaleFrames[i] = 0;
                    continue;
                }

                if (!TryResolveApproxColliderShellContact(
                        collider,
                        controllerPosition,
                        radius,
                        radiusSq,
                        out float penetration,
                        out Vector3 normal))
                {
                    if (_suitOverlapStaleFrames != null &&
                        i < _suitOverlapStaleFrames.Length &&
                        ++_suitOverlapStaleFrames[i] > SuitOverlapStaleFrameLimit)
                    {
                        _suitOverlapResults[i] = null;
                        _suitOverlapStaleFrames[i] = 0;
                    }

                    continue;
                }

                if (_suitOverlapStaleFrames != null && i < _suitOverlapStaleFrames.Length)
                    _suitOverlapStaleFrames[i] = 0;

                if (penetration <= strongestPenetration)
                    continue;

                strongestPenetration = penetration;
                strongestNormal = normal;
            }

            _suitContactActive = strongestPenetration > 0f;
            if (_suitContactActive)
            {
                _handWallRecoilOffset = strongestNormal * math.min(HandWallRecoilMaxOffset, strongestPenetration * HandWallRecoilScale);
                TryEnqueueSuitCollisionHaptic(strongestPenetration);
            }
        }

        internal void RegisterSuitShellCandidate(Collider collider)
        {
            if (_suitOverlapResults == null || !IsSuitCollisionCandidate(collider))
                return;

            int firstEmpty = -1;
            for (int i = 0; i < _suitOverlapResults.Length; i++)
            {
                Collider existing = _suitOverlapResults[i];
                if (ReferenceEquals(existing, collider))
                {
                    if (_suitOverlapStaleFrames != null && i < _suitOverlapStaleFrames.Length)
                        _suitOverlapStaleFrames[i] = 0;

                    return;
                }

                if (existing == null && firstEmpty < 0)
                    firstEmpty = i;
            }

            if (firstEmpty >= 0)
            {
                _suitOverlapResults[firstEmpty] = collider;
                if (_suitOverlapStaleFrames != null && firstEmpty < _suitOverlapStaleFrames.Length)
                    _suitOverlapStaleFrames[firstEmpty] = 0;
                return;
            }

            _suitOverlapSaturationLogged = true;
        }

        internal void UnregisterSuitShellCandidate(Collider collider)
        {
            if (_suitOverlapResults == null || collider == null)
                return;

            for (int i = 0; i < _suitOverlapResults.Length; i++)
            {
                if (ReferenceEquals(_suitOverlapResults[i], collider))
                {
                    _suitOverlapResults[i] = null;
                    if (_suitOverlapStaleFrames != null && i < _suitOverlapStaleFrames.Length)
                        _suitOverlapStaleFrames[i] = 0;
                }
            }
        }

        internal void ClearSuitShellCandidatesFromProxy()
        {
            ClearSuitOverlapResults();
        }

        private bool IsSuitCollisionCandidate(Collider collider)
        {
            if (collider == null ||
                ReferenceEquals(collider, _suitHandCollider) ||
                !collider.enabled ||
                collider.isTrigger)
            {
                return false;
            }

            GameObject colliderObject = collider.gameObject;
            if (colliderObject == null)
                return false;

            int layer = colliderObject.layer;
            return layer >= 0 &&
                   layer < 32 &&
                   (suitCollisionMask.value & (1 << layer)) != 0;
        }

        private bool TryResolveSuitCollisionShellLayer(out int resolvedLayer)
        {
            resolvedLayer = HectonLayerMasks.Player;
            int mask = suitCollisionMask.value;
            int currentLayer = gameObject.layer;
            int bestLayer = IsValidLayer(currentLayer) ? currentLayer : HectonLayerMasks.Player;
            int bestScore = CountLayerMatrixContacts(bestLayer, mask);
            TryPreferSuitCollisionShellLayer(HectonLayerMasks.Player, mask, ref bestLayer, ref bestScore);
            TryPreferSuitCollisionShellLayer(HectonLayerMasks.FirstPersonTools, mask, ref bestLayer, ref bestScore);
            TryPreferSuitCollisionShellLayer(HectonLayerMasks.Interactable, mask, ref bestLayer, ref bestScore);
            TryPreferSuitCollisionShellLayer(HectonLayerMasks.Default, mask, ref bestLayer, ref bestScore);
            if (bestScore <= 0)
                return false;

            resolvedLayer = bestLayer;
            return true;
        }

        private static void TryPreferSuitCollisionShellLayer(int layer, int targetMask, ref int bestLayer, ref int bestScore)
        {
            int score = CountLayerMatrixContacts(layer, targetMask);
            if (score > bestScore)
            {
                bestScore = score;
                bestLayer = layer;
            }
        }

        private static int CountLayerMatrixContacts(int shellLayer, int targetMask)
        {
            if (!IsValidLayer(shellLayer) || targetMask == 0)
                return 0;

            int score = 0;
            for (int targetLayer = 0; targetLayer < 32; targetLayer++)
            {
                if ((targetMask & (1 << targetLayer)) == 0)
                    continue;

                if (!UnityEngine.Physics.GetIgnoreLayerCollision(shellLayer, targetLayer))
                    score++;
            }

            return score;
        }

        private static bool IsValidLayer(int layer)
        {
            return layer >= 0 && layer < 32;
        }

        private void TryEnqueueSuitCollisionHaptic(float penetrationMeters)
        {
            if (!math.isfinite(penetrationMeters) || penetrationMeters <= 0f)
                return;

            float crushThreshold = ResolveSuitCrushPenetrationThreshold();
            float pressure01 = math.saturate(penetrationMeters / math.max(crushThreshold, MinimumDeltaTime));
            float scale01 = ResolveSuitCollisionHapticScale(pressure01);
            if (scale01 <= 0.0001f)
                return;

            byte motorMask = ResolveHandMotorMask(handSide);
            if (penetrationMeters >= crushThreshold)
            {
                if (_handDamageHapticCooldownTimer > 0f)
                    return;

                if (ToolHapticsRuntime.TryEnqueueCommand(
                        math.saturate(0.32f + (scale01 * 0.38f)),
                        math.saturate(0.48f + (scale01 * 0.42f)),
                        0.085f,
                        7.5f,
                        CriticalHapticPriority,
                        motorMask,
                        CriticalHapticBlendMode))
                {
                    _handDamageHapticCooldownTimer = HandDamageHapticCooldownSeconds;
                    _handContactHapticCooldownTimer = math.max(_handContactHapticCooldownTimer, HandContactHapticCooldownSeconds);
                }

                return;
            }

            if (_handContactHapticCooldownTimer > 0f)
                return;

            if (ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                    math.saturate(0.05f + (scale01 * 0.18f)),
                    math.saturate(0.14f + (scale01 * 0.36f)),
                    0.035f,
                    48f + (scale01 * 10f),
                    HandContactHapticPriority,
                    motorMask))
            {
                _handContactHapticCooldownTimer = HandContactHapticCooldownSeconds;
            }
        }

        private void CacheKinematicBridgeCold(bool force = false)
        {
            if (!useKinematicSdfHandBridge)
                return;

            int attempt = force ? _kinematicBridgeCacheAttempt : ++_kinematicBridgeCacheAttempt;
            if (!force && attempt - _lastKinematicBridgeCacheAttempt < KinematicBridgeColdRetryIntervalFrames)
                return;

            _lastKinematicBridgeCacheAttempt = attempt;
            if (force || _kinematicBridgeVault == null)
                _kinematicBridgeVault = GlobalRegistry.DataVault;
            if (_kinematicSdfReadModel == null)
            {
                _kinematicSdfReadModel = GlobalRegistry.VoxelSonarSdf;
                _kinematicSdfReadLeaseModel = _kinematicSdfReadModel as Hecton8.Core.Contracts.IVoxelSonarSdfReadLeaseModel;
            }
            else if (_kinematicSdfReadLeaseModel == null)
            {
                _kinematicSdfReadLeaseModel = _kinematicSdfReadModel as Hecton8.Core.Contracts.IVoxelSonarSdfReadLeaseModel;
            }

            _kinematicBridgeReady = VRInteractionKinematicBridgeVault.EnsureBuffers(
                _kinematicBridgeVault,
                out _);
        }

        private bool TryResolveKinematicBridgeViews(out VRInteractionKinematicBridgeViews views)
        {
            if (!_kinematicBridgeReady)
                RefreshKinematicBridgeExisting(out views);
            else
                _kinematicBridgeReady = VRInteractionKinematicBridgeVault.TryResolveExisting(
                    _kinematicBridgeVault,
                    out views);

            if (!_kinematicBridgeReady || !views.IsValid())
                RefreshKinematicBridgeExisting(out views);

            return _kinematicBridgeReady && views.IsValid();
        }

        private void RefreshKinematicBridgeExisting(out VRInteractionKinematicBridgeViews views)
        {
            _kinematicBridgeReady = VRInteractionKinematicBridgeVault.TryResolveExisting(
                _kinematicBridgeVault,
                out views);
        }

        private void StepKinematicSdfBridge(ref Vector3 controllerPosition, Quaternion controllerRotation, float dt)
        {
            EnsureRuntimeProxy();

            double3 runtimeOriginAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!VRInteractionKinematicBridgeMath.IsFinite(runtimeOriginAup))
            {
                MarkKinematicBridgeFault();
                return;
            }

            int handIndex = handSide == PhysicalHandSide.Left
                ? VRInteractionKinematicBridgeConstants.LeftHandIndex
                : VRInteractionKinematicBridgeConstants.RightHandIndex;
            if (!TrySnapshotKinematicBridgeForSolve(
                    handIndex,
                    out VRInteractionTuningDTO tuning,
                    out VRHandStateDTO previous,
                    out int socketCount))
            {
                UpdateKinematicRuntimeTarget(controllerPosition, controllerRotation);
                return;
            }

            long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();

            RefreshKinematicTuning(ref tuning, controllerPosition, runtimeOriginAup);

            NativeArray<byte>.ReadOnly encodedSdf = default;
            Hecton8.Core.Contracts.VoxelSonarSdfReadLease sdfReadLease = default;
            bool sdfReadLeaseLocked = false;
            if (!TryBindNearestSdf(
                    controllerPosition,
                    runtimeOriginAup,
                    ref tuning,
                    out encodedSdf,
                    out sdfReadLease,
                    out sdfReadLeaseLocked))
            {
                tuning.SdfDimensions = int3.zero;
            }

            VRControllerMatrixDTO controllerInput = default;
            VRHandStateDTO state = default;
            int iterations = 0;
            bool faultAfterSolve = false;
            try
            {
                controllerInput = BuildKinematicControllerMatrix(
                    handIndex,
                    controllerPosition,
                    controllerRotation,
                    runtimeOriginAup,
                    in tuning);
                if (!VRInteractionKinematicBridgeMath.TryIngestControllerMatrix(in controllerInput, handIndex, out state))
                {
                    faultAfterSolve = true;
                }
                else
                {
                    if (!VRInteractionKinematicBridgeMath.IsFinite(previous.ResolvedHandAUP))
                        previous.ResolvedHandAUP = state.RawControllerAUP;

                    state = VRInteractionKinematicBridgeMath.ResolveHand(
                        state,
                        previous,
                        encodedSdf,
                        _kinematicSocketSnapshot,
                        socketCount,
                        in tuning,
                        handIndex,
                        dt,
                        out _lastKinematicPenetration,
                        out _lastKinematicSurfaceNormal,
                        out _lastKinematicSocketId,
                        out iterations);
                    _suitContactActive = (state.InteractionFlags & VRInteractionKinematicBridgeConstants.StateFlagSdfResolved) != 0u &&
                                         _lastKinematicPenetration > 0f;
                    faultAfterSolve = (state.InteractionFlags & VRInteractionKinematicBridgeConstants.StateFlagNonFinite) != 0u;
                }
            }
            finally
            {
                ReleaseKinematicSdfReadLease(in sdfReadLease, ref sdfReadLeaseLocked);
            }

            if (faultAfterSolve)
            {
                MarkKinematicBridgeFault();
                return;
            }

            uint elapsedMicros = ResolveElapsedMicros(startTicks);
            VRHandStateDTO writeState = state;
            float4x4 handMatrix = BuildKinematicHandMatrixValue(state.ResolvedHandAUP, controllerRotation, runtimeOriginAup);
            if (TryResolveSomaticHandMatrix(
                    state.ResolvedHandAUP,
                    controllerRotation,
                    runtimeOriginAup,
                    in tuning,
                    out double3 somaticResolvedAup,
                    out float4x4 somaticMatrix))
            {
                writeState.ResolvedHandAUP = somaticResolvedAup;
                handMatrix = somaticMatrix;
            }

            VRInteractionTelemetryEntry telemetryEntry = BuildKinematicBridgeTelemetryEntry(
                handIndex,
                in writeState,
                in tuning,
                elapsedMicros,
                (uint)iterations,
                out int telemetryBaseSlot,
                out int telemetrySlot);
            if (!TryWriteKinematicBridgeSolve(
                    handIndex,
                    in tuning,
                    in controllerInput,
                    in writeState,
                    in handMatrix,
                    in telemetryEntry,
                    telemetryBaseSlot,
                    telemetrySlot))
            {
                UpdateKinematicRuntimeTarget(controllerPosition, controllerRotation);
                return;
            }

            if (VRInteractionKinematicBridgeMath.TryResolveRuntimePosition(writeState.ResolvedHandAUP, runtimeOriginAup, out Vector3 resolvedRuntimePosition))
                controllerPosition = resolvedRuntimePosition;

            TryPublishKinematicVelocitySignal(in tuning, in writeState);
            UpdateKinematicRuntimeTarget(controllerPosition, controllerRotation);
            SyncDebugState();
        }

        private bool TrySnapshotKinematicBridgeForSolve(
            int handIndex,
            out VRInteractionTuningDTO tuning,
            out VRHandStateDTO previous,
            out int socketCount)
        {
            tuning = default;
            previous = default;
            socketCount = 0;
            if (_kinematicSocketSnapshot == null)
                return false;

            IDataVault mutationVault = _kinematicBridgeVault;
            if (mutationVault == null ||
                mutationVault.IsCompactionFenceActive ||
                !mutationVault.TryAcquireMutationGuard(KinematicBridgeMutationGuardMask))
                return false;

            try
            {
                if (mutationVault.IsCompactionFenceActive)
                    return false;

                if (!TryResolveKinematicBridgeViews(out VRInteractionKinematicBridgeViews views) ||
                    !views.IsValid() ||
                    (uint)handIndex >= (uint)views.HandStates.Length ||
                    views.Tuning.Length == 0)
                {
                    return false;
                }

                tuning = views.Tuning[0];
                previous = views.HandStates[handIndex];
                socketCount = CopyKinematicSocketSnapshot(views.Sockets);
                return true;
            }
            finally
            {
                mutationVault.ReleaseMutationGuard(KinematicBridgeMutationGuardMask);
            }
        }

        private int CopyKinematicSocketSnapshot(NativeArray<VRInteractionSocketDTO> sockets)
        {
            if (_kinematicSocketSnapshot == null || !sockets.IsCreated)
                return 0;

            int limit = math.min(sockets.Length, _kinematicSocketSnapshot.Length);
            int activeCount = 0;
            for (int i = 0; i < limit; i++)
            {
                VRInteractionSocketDTO socket = sockets[i];
                _kinematicSocketSnapshot[i] = socket;
                if ((socket.Flags & VRInteractionKinematicBridgeConstants.SocketFlagActive) != 0u)
                    activeCount = i + 1;
            }

            for (int i = limit; i < _kinematicSocketSnapshot.Length; i++)
                _kinematicSocketSnapshot[i] = default;

            return activeCount;
        }

        private bool TryWriteKinematicBridgeSolve(
            int handIndex,
            in VRInteractionTuningDTO tuning,
            in VRControllerMatrixDTO controllerInput,
            in VRHandStateDTO writeState,
            in float4x4 handMatrix,
            in VRInteractionTelemetryEntry telemetryEntry,
            int telemetryBaseSlot,
            int telemetrySlot)
        {
            IDataVault mutationVault = _kinematicBridgeVault;
            if (mutationVault == null ||
                mutationVault.IsCompactionFenceActive ||
                !mutationVault.TryAcquireMutationGuard(KinematicBridgeMutationGuardMask))
                return false;

            try
            {
                if (mutationVault.IsCompactionFenceActive)
                    return false;

                if (!TryResolveKinematicBridgeViews(out VRInteractionKinematicBridgeViews views) ||
                    !views.IsValid() ||
                    (uint)handIndex >= (uint)views.HandStates.Length ||
                    (uint)handIndex >= (uint)views.PreviousHandStates.Length ||
                    (uint)handIndex >= (uint)views.ControllerMatrices.Length ||
                    (uint)handIndex >= (uint)views.HandMatrices.Length)
                {
                    return false;
                }

                views.Tuning[0] = tuning;
                views.ControllerMatrices[handIndex] = controllerInput;
                views.HandStates[handIndex] = writeState;
                views.PreviousHandStates[handIndex] = writeState;
                views.HandMatrices[handIndex] = handMatrix;
                if (views.TelemetryRing.IsCreated &&
                    (uint)telemetrySlot < (uint)views.TelemetryRing.Length)
                {
                    views.TelemetryRing[telemetrySlot] = telemetryEntry;
                }

                if (views.TelemetryCursor.IsCreated &&
                    views.TelemetryCursor.Length > 0)
                {
                    views.TelemetryCursor[0] = telemetryBaseSlot;
                }

                return true;
            }
            finally
            {
                mutationVault.ReleaseMutationGuard(KinematicBridgeMutationGuardMask);
            }
        }

        private void RefreshKinematicTuning(ref VRInteractionTuningDTO tuning, Vector3 controllerPosition, double3 runtimeOriginAup)
        {
            Vector3 fallbackRootRuntimePosition = _cachedTransform != null ? _cachedTransform.position : controllerPosition;
            double3 rootAup = ResolvePlayerRootAup(runtimeOriginAup, fallbackRootRuntimePosition);

            float side = handSide == PhysicalHandSide.Left ? -1f : 1f;
            double3 shoulderAup = rootAup + new double3(side * 0.18d, 1.38d, 0.08d);
            tuning.PlayerRootAUP = rootAup;
            tuning.ShoulderAUP = shoulderAup;
            tuning.HandRadiusMeters = ResolveSuitCollisionProbeRadius();
            tuning.MaxArmLengthMeters = math.max(0.05f, tuning.MaxArmLengthMeters > 0f ? tuning.MaxArmLengthMeters : VRInteractionKinematicBridgeConstants.DefaultMaxArmLengthMeters);
            tuning.SnapRadiusScale = math.max(0.05f, tuning.SnapRadiusScale > 0f ? tuning.SnapRadiusScale : 1f);
            tuning.VelocitySignalThreshold = math.max(0.1f, tuning.VelocitySignalThreshold > 0f ? tuning.VelocitySignalThreshold : VRInteractionKinematicBridgeConstants.DefaultVelocitySignalThreshold);
            tuning.GlobalQualityWeight = ResolveGlobalQualityWeight01();
            tuning.FrameIndex = ++_kinematicBridgeFrameIndex;
            tuning.Flags |=
                VRInteractionKinematicBridgeConstants.TuningFlagInitialized |
                VRInteractionKinematicBridgeConstants.TuningFlagSdfEnabled |
                VRInteractionKinematicBridgeConstants.TuningFlagSocketSnapEnabled |
                VRInteractionKinematicBridgeConstants.TuningFlagVelocitySignalEnabled;
        }

        private double3 ResolvePlayerRootAup(double3 runtimeOriginAup, Vector3 fallbackRuntimePosition)
        {
            IPlayerRuntimeContext runtimeContext = _playerRuntimeContext;
            if (runtimeContext != null &&
                runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                snapshot.Aup.IsFinite())
            {
                double3 snapshotAup = snapshot.Aup.ToAbsoluteDouble3();
                if (VRInteractionKinematicBridgeMath.IsFinite(snapshotAup))
                    return snapshotAup;
            }

            if (VRInteractionKinematicBridgeMath.TryResolveRuntimeAup(fallbackRuntimePosition, runtimeOriginAup, out double3 fallbackAup))
                return fallbackAup;

            return runtimeOriginAup;
        }

        private bool TryBindNearestSdf(
            Vector3 controllerPosition,
            double3 runtimeOriginAup,
            ref VRInteractionTuningDTO tuning,
            out NativeArray<byte>.ReadOnly encodedSdf,
            out Hecton8.Core.Contracts.VoxelSonarSdfReadLease lease,
            out bool leaseLocked)
        {
            encodedSdf = default;
            lease = default;
            leaseLocked = false;
            Hecton8.Core.Contracts.IVoxelSonarSdfReadLeaseModel readModel = _kinematicSdfReadLeaseModel;
            if (readModel == null)
                return false;

            float3 runtimeOrigin = new float3(controllerPosition.x, controllerPosition.y, controllerPosition.z);
            if (!readModel.TryAcquireNearestSonarSdfReadLease(
                    runtimeOrigin,
                    out encodedSdf,
                    out int3 gridDimensions,
                    out float3 volumeOrigin,
                    out float3 cellSize,
                    out float sdfRange,
                    out lease) ||
                !encodedSdf.IsCreated ||
                gridDimensions.x <= 1 ||
                gridDimensions.y <= 1 ||
                gridDimensions.z <= 1 ||
                !math.all(math.isfinite(volumeOrigin)) ||
                !math.all(math.isfinite(cellSize)) ||
                !math.isfinite(sdfRange) ||
                sdfRange <= 0f)
            {
                if (lease.IsValid)
                {
                    leaseLocked = true;
                    ReleaseKinematicSdfReadLease(in lease, ref leaseLocked);
                }

                encodedSdf = default;
                lease = default;
                return false;
            }

            leaseLocked = true;
            tuning.SdfOriginAUP = runtimeOriginAup + new double3(volumeOrigin.x, volumeOrigin.y, volumeOrigin.z);
            tuning.SdfCellSize = math.max(cellSize, new float3(0.0001f));
            tuning.SdfDimensions = gridDimensions;
            tuning.SdfRangeMeters = sdfRange;
            return true;
        }

        private void ReleaseKinematicSdfReadLease(
            in Hecton8.Core.Contracts.VoxelSonarSdfReadLease lease,
            ref bool leaseLocked)
        {
            if (!leaseLocked)
                return;

            Hecton8.Core.Contracts.IVoxelSonarSdfReadLeaseModel readModel = _kinematicSdfReadLeaseModel;
            if (readModel != null && lease.IsValid)
                readModel.ReleaseNearestSonarSdfReadLease(in lease);

            leaseLocked = false;
        }

        private static VRControllerMatrixDTO BuildKinematicControllerMatrix(
            int handIndex,
            Vector3 controllerPosition,
            Quaternion controllerRotation,
            double3 runtimeOriginAup,
            in VRInteractionTuningDTO tuning)
        {
            float3 runtimePosition = new float3(controllerPosition.x, controllerPosition.y, controllerPosition.z);
            quaternion rotation = new quaternion(controllerRotation.x, controllerRotation.y, controllerRotation.z, controllerRotation.w);
            if (!math.all(math.isfinite(runtimePosition)))
                runtimePosition = float3.zero;
            if (!math.all(math.isfinite(rotation.value)))
                rotation = quaternion.identity;

            double3 rootRuntimeDelta = tuning.PlayerRootAUP - runtimeOriginAup;
            float3 rootRuntimePosition = new float3((float)rootRuntimeDelta.x, (float)rootRuntimeDelta.y, (float)rootRuntimeDelta.z);
            if (!math.all(math.isfinite(rootRuntimePosition)))
                rootRuntimePosition = float3.zero;

            float3 controllerRootLocal = runtimePosition - rootRuntimePosition;
            if (!math.all(math.isfinite(controllerRootLocal)))
                controllerRootLocal = float3.zero;

            double3 shoulderDelta = tuning.ShoulderAUP - tuning.PlayerRootAUP;
            float3 shoulderRuntimeOffset = new float3((float)shoulderDelta.x, (float)shoulderDelta.y, (float)shoulderDelta.z);
            if (!math.all(math.isfinite(shoulderRuntimeOffset)))
                shoulderRuntimeOffset = float3.zero;

            VRControllerMatrixDTO dto = default;
            dto.ControllerLocalToWorld = float4x4.TRS(controllerRootLocal, rotation, new float3(1f));
            dto.PlayerRootAUP = tuning.PlayerRootAUP;
            dto.ShoulderRuntimeOffset = shoulderRuntimeOffset;
            dto.Grip01 = 1f;
            dto.Flags =
                VRInteractionKinematicBridgeConstants.StateFlagValid |
                VRInteractionKinematicBridgeConstants.StateFlagTracked |
                VRInteractionKinematicBridgeConstants.StateFlagNoPhysicsProxy;
            dto.FrameIndex = tuning.FrameIndex;
            dto.HandIndex = (byte)handIndex;
            dto.IsTracked = 1;
            return dto;
        }

        private static bool TryResolveSomaticHandMatrix(
            double3 resolvedAup,
            Quaternion rotation,
            double3 runtimeOriginAup,
            in VRInteractionTuningDTO tuning,
            out double3 somaticResolvedAup,
            out float4x4 handMatrix)
        {
            somaticResolvedAup = resolvedAup;
            handMatrix = BuildKinematicHandMatrixValue(resolvedAup, rotation, runtimeOriginAup);
            if (!VRInteractionKinematicBridgeMath.IsFinite(resolvedAup))
                return false;

            quaternion q = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
            if (!math.all(math.isfinite(q.value)))
                q = quaternion.identity;

            SomaticIKSolver solver = new SomaticIKSolver
            {
                RuntimeOriginAup = runtimeOriginAup,
                ShoulderAup = tuning.ShoulderAUP,
                TargetAup = resolvedAup,
                TargetRotation = q,
                UpperArmLengthMeters = tuning.MaxArmLengthMeters * 0.52f,
                LowerArmLengthMeters = tuning.MaxArmLengthMeters * 0.48f,
                GlobalQualityWeight = tuning.GlobalQualityWeight
            };
            solver.Execute();

            if (solver.Success == 0 || !VRInteractionKinematicBridgeMath.IsFinite(solver.ResolvedAup))
                return false;

            somaticResolvedAup = solver.ResolvedAup;
            handMatrix = solver.HandMatrix;
            return true;
        }

        private static float4x4 BuildKinematicHandMatrixValue(double3 resolvedAup, Quaternion rotation, double3 runtimeOriginAup)
        {
            double3 delta = resolvedAup - runtimeOriginAup;
            float3 local = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            if (!math.all(math.isfinite(local)))
                local = float3.zero;

            quaternion q = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
            if (!math.all(math.isfinite(q.value)))
                q = quaternion.identity;

            return float4x4.TRS(local, q, new float3(1f));
        }

        private VRInteractionTelemetryEntry BuildKinematicBridgeTelemetryEntry(
            int handIndex,
            in VRHandStateDTO state,
            in VRInteractionTuningDTO tuning,
            uint elapsedMicros,
            uint iterations,
            out int telemetryBaseSlot,
            out int telemetrySlot)
        {
            uint frame = _kinematicBridgeFrameIndex;
            telemetryBaseSlot = (int)(frame % VRInteractionKinematicBridgeConstants.TelemetryFrameCapacity) * VRInteractionKinematicBridgeConstants.HandCount;
            telemetrySlot = telemetryBaseSlot + handIndex;
            VRInteractionTelemetryEntry entry = default;
            entry.FrameIndex = frame;
            entry.StateHash = VRInteractionKinematicBridgeMath.HashState(in state, (uint)handIndex);
            entry.Flags = state.InteractionFlags;
            if (elapsedMicros > 100u)
                entry.Flags |= VRInteractionKinematicBridgeConstants.TelemetryFlagBudgetExceeded;
            if (VRInteractionKinematicBridgeMath.ResolveQualityIterationHint(tuning.GlobalQualityWeight) < (int)iterations)
                entry.Flags |= VRInteractionKinematicBridgeConstants.TelemetryFlagQualityScaled;
            entry.CpuTimeMicros = elapsedMicros;
            entry.RawControllerAUP = state.RawControllerAUP;
            entry.ResolvedHandAUP = state.ResolvedHandAUP;
            entry.Velocity = state.Velocity;
            entry.MaxPenetrationMeters = _lastKinematicPenetration;
            entry.SurfaceNormal = _lastKinematicSurfaceNormal;
            entry.SocketId = _lastKinematicSocketId;
            entry.SolverIterations = iterations;
            entry.HandIndex = (uint)handIndex;
            entry.Marker = VRInteractionKinematicBridgeConstants.TelemetryMarker;
            return entry;
        }

        private void TryPublishKinematicVelocitySignal(in VRInteractionTuningDTO tuning, in VRHandStateDTO state)
        {
            if ((state.InteractionFlags & VRInteractionKinematicBridgeConstants.StateFlagVelocitySignal) == 0u)
                return;

            uint frame = _kinematicBridgeFrameIndex;
            if (_lastKinematicVelocitySignalFrame == frame)
                return;

            float speedSq = math.lengthsq(state.Velocity);
            if (!math.isfinite(speedSq) || speedSq <= 0.000001f)
                return;

            float invSpeed = math.rsqrt(speedSq);
            float speed = speedSq * invSpeed;
            float threshold = math.max(0.0001f, tuning.VelocitySignalThreshold);
            float kinetic01 = math.saturate(speed * math.rcp(threshold));
            CombatDamageSignal signal = default;
            signal.ImpactAup = state.ResolvedHandAUP;
            signal.Direction = state.Velocity * invSpeed;
            signal.Magnitude = speed;
            signal.DamageType = 0x56524844u; // VRHD
            signal.TargetHash = _lastKinematicSocketId;
            signal.SourceHash = 0x53483237u; // SH27
            signal.Frame = frame;
            signal.SourceId = (ushort)handSide;
            signal.Channel = 1;
            signal.Flags = CombatDamageSignal.DirectRuntimeFlag | CombatDamageSignal.VisualOnlyFlag;
            signal.IntegrityDelta = (byte)math.clamp((int)math.round(kinetic01 * 255f), 1, 255);
            SignalBus<CombatDamageSignal>.TryPushTracked(in signal, ref s_x001PhysicalHandControllerSignalPushDropCount);
            _lastKinematicVelocitySignalFrame = frame;
        }

        private void UpdateKinematicRuntimeTarget(Vector3 position, Quaternion rotation)
        {
            if (_runtimeRoot != null && !IsGrabbing)
            {
                _runtimeRoot.position = position;
                _runtimeRoot.rotation = Quaternion.identity;
            }

            if (_runtimeGripPoint != null)
            {
                _runtimeGripPoint.position = position;
                _runtimeGripPoint.rotation = rotation;
            }
        }

        private void MarkKinematicBridgeFault()
        {
            uint frame = _handFixedFrameIndex;
            if (_lastKinematicFaultFrame == frame)
                return;

            _lastKinematicFaultFrame = frame;
        }

        private static uint ResolveElapsedMicros(long startTicks)
        {
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
            if (elapsed <= 0L)
                return 0u;

            double micros = (elapsed * 1000000.0d) / System.Diagnostics.Stopwatch.Frequency;
            if (double.IsNaN(micros) || double.IsInfinity(micros) || micros <= 0d)
                return 0u;

            return micros >= uint.MaxValue ? uint.MaxValue : (uint)micros;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float weight = SignalBusRegistry.GlobalQualityWeight01;
            return math.saturate(math.select(1f, weight, math.isfinite(weight)));
        }

        private bool ShouldScheduleFingerPoseBatch()
        {
            int interval = ResolveFingerPoseIntervalFrames(ResolveGlobalQualityWeight01());
            return interval <= FingerPoseMinIntervalFrames ||
                   (_handFixedFrameIndex % (uint)interval) == 0u;
        }

        private static int ResolveFingerPoseIntervalFrames(float globalQualityWeight)
        {
            float q = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            float shaped = q * q * (3f - 2f * q);
            float interval = math.lerp(FingerPoseMaxIntervalFrames, FingerPoseMinIntervalFrames, shaped);
            return math.max(FingerPoseMinIntervalFrames, (int)math.round(interval));
        }

        private void AllocatePersistentBuffersCold()
        {
            if (_kinematicSocketSnapshot == null)
            {
                _kinematicSocketSnapshot = new VRInteractionSocketDTO[VRInteractionKinematicBridgeConstants.SocketCapacity]; // COLD ALLOC: VRInteractionSocketDTO[128] - kinematic socket snapshot outside vault mutation guard - owner: PhysicalHandController
            }

            if (HasFingerPoseBuffers())
                return;

            _fingerPoses = new FingerPoseData[FingerCount]; // COLD ALLOC: FingerPoseData[5] - finger pose value results - owner: PhysicalHandController
            _fingerRayDefinitions = new FingerRayDefinition[FingerCount]; // COLD ALLOC: FingerRayDefinition[5] - local finger ray definitions - owner: PhysicalHandController
            _fingerRayRuntime = new FingerRayRuntime[FingerCount]; // COLD ALLOC: FingerRayRuntime[5] - world-space finger ray runtime values - owner: PhysicalHandController

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

        private bool HasFingerPoseBuffers()
        {
            return _fingerPoses != null &&
                   _fingerPoses.Length >= FingerCount &&
                   _fingerRayDefinitions != null &&
                   _fingerRayDefinitions.Length >= FingerCount &&
                   _fingerRayRuntime != null &&
                   _fingerRayRuntime.Length >= FingerCount;
        }

        private void DisposePersistentBuffers()
        {
            _kinematicSocketSnapshot = null;

            _fingerPoses = null;
            _fingerRayDefinitions = null;
            _fingerRayRuntime = null;
            _fingerPoseScheduled = false;
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

        private Transform ReadRightHandAttachment()
        {
            return _rightHandAttachmentResolved ? _resolvedRightHandAttachment : null;
        }

        private Transform CacheRightHandAttachmentCold()
        {
            if (_rightHandAttachmentResolved)
                return _resolvedRightHandAttachment;

            if (rightHandAttachmentOverride != null)
            {
                _resolvedRightHandAttachment = rightHandAttachmentOverride;
                _rightHandAttachmentResolved = true;
                return _resolvedRightHandAttachment;
            }

            CacheSwimBlockoutRigCold();

            if (swimBlockoutRig != null)
                _resolvedRightHandAttachment = swimBlockoutRig.RightHandAttachment;

            _rightHandAttachmentResolved = true;
            return _resolvedRightHandAttachment;
        }

        private Transform ReadOpposingHandAttachment()
        {
            return _opposingHandAttachmentResolved ? _resolvedOpposingHandAttachment : null;
        }

        private Transform CacheOpposingHandAttachmentCold()
        {
            if (_opposingHandAttachmentResolved)
                return _resolvedOpposingHandAttachment;

            CacheSwimBlockoutRigCold();

            if (swimBlockoutRig != null)
            {
                _resolvedOpposingHandAttachment = handSide == PhysicalHandSide.Left
                    ? swimBlockoutRig.RightHandAttachment
                    : swimBlockoutRig.LeftHandAttachment;
            }

            _opposingHandAttachmentResolved = true;
            return _resolvedOpposingHandAttachment;
        }

        private PlayerSwimBlockoutRig CacheSwimBlockoutRigCold()
        {
            if (swimBlockoutRig != null)
                return swimBlockoutRig;

            Transform root = _cachedTransform != null ? _cachedTransform : transform;
            swimBlockoutRig = ComponentReferenceUtility.ResolveOwnedComponent<PlayerSwimBlockoutRig>(root);
            return swimBlockoutRig;
        }

        private void CacheInteractionProbeColliderCold()
        {
            Transform attachment = CacheRightHandAttachmentCold();
            if (attachment == null)
            {
                _cachedInteractionProbeColliderSource = null;
                _cachedInteractionProbeCollider = null;
                return;
            }

            if (ReferenceEquals(_cachedInteractionProbeColliderSource, attachment))
                return;

            _cachedInteractionProbeColliderSource = attachment;
            _cachedInteractionProbeCollider = null;
            attachment.TryGetComponent(out _cachedInteractionProbeCollider);
        }

        private void CompleteScheduledFingerPose(float dt)
        {
            if (!_fingerPoseScheduled)
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
            if (fingerSegments == null || _baseFingerLocalRotations == null || _fingerPoses == null)
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

            if (_runtimeGripPoint != null)
                _runtimeGripPoint.position = _virtualHandPosition;
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
                _physicsService?.QueueForce(body, deltaVelocity, ForceMode.VelocityChange);

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
                _physicsService?.QueueTorque(body, deltaAngularVelocity, ForceMode.VelocityChange);

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
                _physicsService?.QueueTorque(body, deltaAngularVelocity, ForceMode.VelocityChange);
        }

        private void UpdateImplicitTwoHandStabilizer(Rigidbody body)
        {
            if (!_requiresTwoHandStabilization || body == null)
                return;

            Transform opposingHand = ReadOpposingHandAttachment();
            if (opposingHand == null || !IsFinite(opposingHand.position))
            {
                _twoHandStabilized = false;
                _hasSecondHandStabilizerPose = false;
                return;
            }

            Bounds bodyBounds = ResolveActiveBodyBounds(body);
            float maxExtent = math.max(MinimumBoundsSpan, math.cmax((float3)bodyBounds.extents));
            float stabilizerRadius = (maxExtent * 2f) + math.max(0.08f, suitCollisionProbeRadius);
            Vector3 stabilizerDelta = opposingHand.position - bodyBounds.center;
            bool withinStabilizerRange = stabilizerDelta.sqrMagnitude <= stabilizerRadius * stabilizerRadius;
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
                _physicsService?.QueueTorque(body, deltaAngularVelocity, ForceMode.VelocityChange);
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

            if (!HasFingerPoseBuffers())
                return;

            Vector3 targetPosition = ResolveFingerPoseTargetPoint(_activeBody, _activeBodyCollider, _runtimeGripPoint.position);

            SolveFingerPoseValues(
                _runtimeGripPoint.position,
                _runtimeGripPoint.rotation,
                targetPosition,
                FingerCastLength);
            _fingerPoseScheduled = true;
        }

        private void SolveFingerPoseValues(Vector3 handPosition, Quaternion handRotation, Vector3 targetPosition, float castLength)
        {
            if (_fingerRayDefinitions == null || _fingerRayRuntime == null || _fingerPoses == null)
                return;

            FingerPoseFabrikSolver solver = new FingerPoseFabrikSolver
            {
                Definitions = _fingerRayDefinitions,
                Runtime = _fingerRayRuntime,
                Poses = _fingerPoses,
                Count = FingerCount,
                HandPosition = (float3)handPosition,
                HandRotation = ToMathematicsQuaternion(handRotation),
                TargetPosition = (float3)targetPosition,
                CastLength = castLength,
                GlobalQualityWeight = ResolveGlobalQualityWeight01()
            };
            solver.Execute();
        }

        private static Vector3 ResolveFingerPoseTargetPoint(Rigidbody body, Collider activeCollider, Vector3 gripPosition)
        {
            if (activeCollider != null &&
                TryResolveApproxColliderSurfacePoint(activeCollider, gripPosition, out Vector3 surfacePoint, out _, out _))
            {
                return surfacePoint;
            }

            if (body != null && IsFinite(body.worldCenterOfMass))
                return body.worldCenterOfMass;

            return IsFinite(gripPosition) ? gripPosition : Vector3.zero;
        }

        private static bool TryResolveApproxColliderShellContact(
            Collider collider,
            Vector3 samplePosition,
            float radius,
            float radiusSq,
            out float penetration,
            out Vector3 normal)
        {
            penetration = 0f;
            normal = Vector3.zero;
            if (collider == null ||
                !collider.enabled ||
                !IsFinite(samplePosition) ||
                radius <= 0f ||
                radiusSq <= 0f)
            {
                return false;
            }

            if (!TryResolveApproxColliderSurfacePoint(
                    collider,
                    samplePosition,
                    out Vector3 surfacePoint,
                    out normal,
                    out bool sampleInside))
            {
                return false;
            }

            if (sampleInside)
            {
                penetration = radius;
                return IsFinite(normal);
            }

            float3 delta = (float3)(samplePosition - surfacePoint);
            float distanceSq = math.lengthsq(delta);
            if (!math.isfinite(distanceSq) || distanceSq > radiusSq)
                return false;

            float distance = distanceSq > 0.000001f
                ? distanceSq * math.rsqrt(distanceSq)
                : 0f;
            penetration = radius - distance;
            if (penetration <= 0f || !math.isfinite(penetration))
                return false;

            if (distance > 0.000001f)
            {
                normal = (Vector3)(delta * math.rsqrt(distanceSq));
                return IsFinite(normal);
            }

            normal = IsFinite(normal) ? normal : Vector3.up;
            return true;
        }

        private static bool TryResolveApproxColliderSurfacePoint(
            Collider collider,
            Vector3 samplePosition,
            out Vector3 surfacePoint,
            out Vector3 surfaceNormal,
            out bool sampleInside)
        {
            surfacePoint = Vector3.zero;
            surfaceNormal = Vector3.up;
            sampleInside = false;
            if (collider == null || !collider.enabled || !IsFinite(samplePosition))
                return false;

            if (collider is BoxCollider boxCollider &&
                TryResolveApproxBoxSurfacePoint(boxCollider, samplePosition, out surfacePoint, out surfaceNormal, out sampleInside))
            {
                return true;
            }

            if (collider is SphereCollider sphereCollider &&
                TryResolveApproxSphereSurfacePoint(sphereCollider, samplePosition, out surfacePoint, out surfaceNormal, out sampleInside))
            {
                return true;
            }

            if (collider is CapsuleCollider capsuleCollider &&
                TryResolveApproxCapsuleSurfacePoint(capsuleCollider, samplePosition, out surfacePoint, out surfaceNormal, out sampleInside))
            {
                return true;
            }

            return TryResolveApproxBoundsSurfacePoint(collider, samplePosition, out surfacePoint, out surfaceNormal, out sampleInside);
        }

        private static bool TryResolveApproxBoxSurfacePoint(
            BoxCollider boxCollider,
            Vector3 samplePosition,
            out Vector3 surfacePoint,
            out Vector3 surfaceNormal,
            out bool sampleInside)
        {
            surfacePoint = Vector3.zero;
            surfaceNormal = Vector3.up;
            sampleInside = false;
            Transform colliderTransform = boxCollider.transform;
            if (colliderTransform == null || !IsFinite(samplePosition))
                return false;

            Vector3 localPosition = colliderTransform.InverseTransformPoint(samplePosition);
            if (!IsFinite(localPosition))
                return false;

            float3 center = (float3)boxCollider.center;
            float3 extents = math.max((float3)boxCollider.size * 0.5f, new float3(MinimumBoundsSpan * 0.5f));
            float3 localDelta = (float3)localPosition - center;
            float3 min = center - extents;
            float3 max = center + extents;
            float3 closest = math.clamp((float3)localPosition, min, max);
            float3 delta = (float3)localPosition - closest;
            float distanceSq = math.lengthsq(delta);
            if (!math.isfinite(distanceSq))
                return false;

            if (distanceSq > 0.000001f)
            {
                surfacePoint = colliderTransform.TransformPoint((Vector3)closest);
                surfaceNormal = NormalizeVectorApproxNoSqrt(colliderTransform.TransformDirection((Vector3)(delta * math.rsqrt(distanceSq))), Vector3.up);
                return IsFinite(surfacePoint) && IsFinite(surfaceNormal);
            }

            sampleInside = true;
            float3 axisPenetration = math.max(extents - math.abs(localDelta), new float3(MinimumDeltaTime));
            float3 normal = ResolveDominantAxisNormal(localDelta, axisPenetration);
            float3 point = center + new float3(
                normal.x != 0f ? normal.x * extents.x : math.clamp(localDelta.x, -extents.x, extents.x),
                normal.y != 0f ? normal.y * extents.y : math.clamp(localDelta.y, -extents.y, extents.y),
                normal.z != 0f ? normal.z * extents.z : math.clamp(localDelta.z, -extents.z, extents.z));

            surfacePoint = colliderTransform.TransformPoint((Vector3)point);
            surfaceNormal = NormalizeVectorApproxNoSqrt(colliderTransform.TransformDirection((Vector3)normal), Vector3.up);
            return IsFinite(surfacePoint) && IsFinite(surfaceNormal);
        }

        private static bool TryResolveApproxSphereSurfacePoint(
            SphereCollider sphereCollider,
            Vector3 samplePosition,
            out Vector3 surfacePoint,
            out Vector3 surfaceNormal,
            out bool sampleInside)
        {
            surfacePoint = Vector3.zero;
            surfaceNormal = Vector3.up;
            sampleInside = false;
            Transform colliderTransform = sphereCollider.transform;
            if (colliderTransform == null || !IsFinite(samplePosition))
                return false;

            Vector3 center = colliderTransform.TransformPoint(sphereCollider.center);
            if (!IsFinite(center))
                return false;

            float radius = math.max(MinimumBoundsSpan * 0.5f, sphereCollider.radius * ResolveMaxAbsScale(colliderTransform.lossyScale));
            Vector3 delta = samplePosition - center;
            float distanceSq = delta.sqrMagnitude;
            if (!math.isfinite(distanceSq))
                return false;

            surfaceNormal = distanceSq > 0.000001f
                ? (Vector3)((float3)delta * math.rsqrt(distanceSq))
                : Vector3.up;
            sampleInside = distanceSq <= radius * radius;
            surfacePoint = center + (surfaceNormal * radius);
            return IsFinite(surfacePoint) && IsFinite(surfaceNormal);
        }

        private static bool TryResolveApproxCapsuleSurfacePoint(
            CapsuleCollider capsuleCollider,
            Vector3 samplePosition,
            out Vector3 surfacePoint,
            out Vector3 surfaceNormal,
            out bool sampleInside)
        {
            surfacePoint = Vector3.zero;
            surfaceNormal = Vector3.up;
            sampleInside = false;
            Transform colliderTransform = capsuleCollider.transform;
            if (colliderTransform == null || !IsFinite(samplePosition))
                return false;

            Vector3 center = colliderTransform.TransformPoint(capsuleCollider.center);
            Vector3 axis = ResolveCapsuleWorldAxis(capsuleCollider, colliderTransform);
            if (!IsFinite(center) || !IsFinite(axis) || axis.sqrMagnitude <= 0.000001f)
                return false;

            Vector3 scale = colliderTransform.lossyScale;
            float axisScale = ResolveCapsuleAxisScale(capsuleCollider.direction, scale);
            float radiusScale = ResolveCapsuleRadiusScale(capsuleCollider.direction, scale);
            float radius = math.max(MinimumBoundsSpan * 0.5f, capsuleCollider.radius * radiusScale);
            float height = math.max(radius * 2f, capsuleCollider.height * math.max(axisScale, MinimumDeltaTime));
            float halfSegment = math.max(0f, (height * 0.5f) - radius);
            Vector3 segmentA = center - axis * halfSegment;
            Vector3 segmentB = center + axis * halfSegment;
            Vector3 segment = segmentB - segmentA;
            float segmentLengthSq = segment.sqrMagnitude;
            float t = segmentLengthSq > 0.000001f
                ? math.saturate(Vector3.Dot(samplePosition - segmentA, segment) / segmentLengthSq)
                : 0.5f;
            Vector3 closestAxisPoint = segmentA + segment * t;
            Vector3 radial = samplePosition - closestAxisPoint;
            float radialDistanceSq = radial.sqrMagnitude;
            if (!math.isfinite(radialDistanceSq))
                return false;

            surfaceNormal = radialDistanceSq > 0.000001f
                ? (Vector3)((float3)radial * math.rsqrt(radialDistanceSq))
                : ResolveCapsuleFallbackNormal(axis);
            sampleInside = radialDistanceSq <= radius * radius;
            surfacePoint = closestAxisPoint + surfaceNormal * radius;
            return IsFinite(surfacePoint) && IsFinite(surfaceNormal);
        }

        private static bool TryResolveApproxBoundsSurfacePoint(
            Collider collider,
            Vector3 samplePosition,
            out Vector3 surfacePoint,
            out Vector3 surfaceNormal,
            out bool sampleInside)
        {
            surfacePoint = Vector3.zero;
            surfaceNormal = Vector3.up;
            sampleInside = false;
            if (!TryResolveApproxColliderBounds(collider, out Bounds bounds) || !IsFinite(samplePosition))
                return false;

            float3 sample = (float3)samplePosition;
            float3 center = (float3)bounds.center;
            float3 extents = math.max((float3)bounds.extents, new float3(MinimumBoundsSpan * 0.5f));
            float3 min = center - extents;
            float3 max = center + extents;
            float3 closest = math.clamp(sample, min, max);
            float3 delta = sample - closest;
            float distanceSq = math.lengthsq(delta);
            if (!math.isfinite(distanceSq))
                return false;

            if (distanceSq > 0.000001f)
            {
                surfacePoint = (Vector3)closest;
                surfaceNormal = (Vector3)(delta * math.rsqrt(distanceSq));
                return IsFinite(surfacePoint) && IsFinite(surfaceNormal);
            }

            sampleInside = true;
            float3 localDelta = sample - center;
            float3 axisPenetration = math.max(extents - math.abs(localDelta), new float3(MinimumDeltaTime));
            float3 normal = ResolveDominantAxisNormal(localDelta, axisPenetration);
            float3 point = center + new float3(
                normal.x != 0f ? normal.x * extents.x : math.clamp(localDelta.x, -extents.x, extents.x),
                normal.y != 0f ? normal.y * extents.y : math.clamp(localDelta.y, -extents.y, extents.y),
                normal.z != 0f ? normal.z * extents.z : math.clamp(localDelta.z, -extents.z, extents.z));

            surfacePoint = (Vector3)point;
            surfaceNormal = (Vector3)normal;
            return IsFinite(surfacePoint) && IsFinite(surfaceNormal);
        }

        private static Vector3 ResolveCapsuleWorldAxis(CapsuleCollider capsuleCollider, Transform colliderTransform)
        {
            Vector3 localAxis = capsuleCollider.direction == 0
                ? Vector3.right
                : (capsuleCollider.direction == 2 ? Vector3.forward : Vector3.up);
            return NormalizeVectorApproxNoSqrt(colliderTransform.TransformDirection(localAxis), Vector3.up);
        }

        private static float ResolveCapsuleAxisScale(int direction, Vector3 scale)
        {
            float3 absScale = math.abs((float3)scale);
            if (direction == 0)
                return math.max(absScale.x, MinimumDeltaTime);
            if (direction == 2)
                return math.max(absScale.z, MinimumDeltaTime);
            return math.max(absScale.y, MinimumDeltaTime);
        }

        private static float ResolveCapsuleRadiusScale(int direction, Vector3 scale)
        {
            float3 absScale = math.abs((float3)scale);
            if (direction == 0)
                return math.max(math.max(absScale.y, absScale.z), MinimumDeltaTime);
            if (direction == 2)
                return math.max(math.max(absScale.x, absScale.y), MinimumDeltaTime);
            return math.max(math.max(absScale.x, absScale.z), MinimumDeltaTime);
        }

        private static float ResolveMaxAbsScale(Vector3 scale)
        {
            float3 absScale = math.abs((float3)scale);
            return math.max(math.cmax(absScale), MinimumDeltaTime);
        }

        private static Vector3 ResolveCapsuleFallbackNormal(Vector3 axis)
        {
            Vector3 candidate = Vector3.Cross(axis, Vector3.up);
            if (candidate.sqrMagnitude <= 0.000001f)
                candidate = Vector3.Cross(axis, Vector3.right);
            return NormalizeVectorApproxNoSqrt(candidate, Vector3.up);
        }

        private static bool TryResolveApproxColliderBounds(Collider collider, out Bounds bounds)
        {
            bounds = default;
            if (collider == null || !collider.enabled)
                return false;

            bounds = collider.bounds;
            if (!IsFinite(bounds.center) || !IsFinite(bounds.extents))
                return false;

            float3 extents = (float3)bounds.extents;
            return math.all(math.isfinite(extents)) && math.cmax(extents) > 0.000001f;
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
            if (!math.isfinite(value) || value <= 0f)
                return 0f;

            return math.clamp(value, MinimumDeltaTime, MaximumSafeDeltaTime);
        }

        private static bool TryResolveXRControllerIndex(PhysicalHandSide side, out byte controllerIndex)
        {
            if (side == PhysicalHandSide.Left)
            {
                controllerIndex = 0;
                return true;
            }

            if (side == PhysicalHandSide.Right)
            {
                controllerIndex = 1;
                return true;
            }

            controllerIndex = 0;
            return false;
        }

        private static byte ResolveHandMotorMask(PhysicalHandSide side)
        {
            if (side == PhysicalHandSide.Left)
                return LeftMotorMask;

            if (side == PhysicalHandSide.Right)
                return RightMotorMask;

            return BothMotorMask;
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
                {
                    _runtimeGripPoint.position = controllerPosition;
                    _runtimeGripPoint.rotation = controllerRotation;
                }
            }
        }

        private void ApplyPlatformRelativeCarry(ref Vector3 controllerPosition, ref Quaternion controllerRotation)
        {
            ISubmarineRuntimeContext context = _submarineRuntimeContext;
            Transform platform = context != null && context.IsTransportPlatformActive
                ? context.PlatformTransform
                : null;
            if (platform == null || !IsFinite(controllerPosition) || !IsFinite(controllerRotation))
            {
                _hasPlatformFrame = false;
                return;
            }

            Matrix4x4 currentLocalToWorld = platform.localToWorldMatrix;
            Matrix4x4 currentWorldToLocal = platform.worldToLocalMatrix;
            Quaternion currentRotation = platform.rotation;
            if (!IsFinite(currentRotation))
            {
                _hasPlatformFrame = false;
                return;
            }

            if (!_hasPlatformFrame)
            {
                _previousPlatformWorldToLocal = currentWorldToLocal;
                _previousPlatformInverseRotation = ConjugateUnitQuaternion(currentRotation);
                _hasPlatformFrame = true;
                return;
            }

            Vector3 previousPlatformLocal = _previousPlatformWorldToLocal.MultiplyPoint3x4(controllerPosition);
            Vector3 carriedPosition = currentLocalToWorld.MultiplyPoint3x4(previousPlatformLocal);
            Quaternion rotationDelta = currentRotation * _previousPlatformInverseRotation;
            Quaternion carriedRotation = rotationDelta * controllerRotation;
            _previousPlatformWorldToLocal = currentWorldToLocal;
            _previousPlatformInverseRotation = ConjugateUnitQuaternion(currentRotation);

            if (!IsFinite(carriedPosition) || !IsFinite(carriedRotation))
                return;

            double3 runtimeOriginAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            double3 carriedAup = runtimeOriginAup + new double3(carriedPosition.x, carriedPosition.y, carriedPosition.z);
            double3 localDelta = carriedAup - runtimeOriginAup;
            Vector3 localized = new Vector3((float)localDelta.x, (float)localDelta.y, (float)localDelta.z);
            if (!IsFinite(localized))
                return;

            controllerPosition = localized;
            controllerRotation = carriedRotation;
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

            double3 delta = new double3(
                (double)a.x - b.x,
                (double)a.y - b.y,
                (double)a.z - b.z);
            double distanceSq = math.lengthsq(delta);
            return math.isfinite((float)distanceSq)
                ? math.min((float)distanceSq, float.MaxValue)
                : float.MaxValue;
        }

        private static AbsoluteUniversePosition ResolveSuitContactAup(Vector3 contactPoint, Vector3 controllerPosition)
        {
            if (TryResolveXrCachedHeadAup(controllerPosition, out AbsoluteUniversePosition controllerAup))
                return OffsetAupLocal(in controllerAup, contactPoint - controllerPosition);

            return TryResolveRuntimeAup(contactPoint, out AbsoluteUniversePosition contactAup)
                ? contactAup
                : default;
        }

        private static bool TryResolveXrCachedHeadAup(Vector3 runtimePosition, out AbsoluteUniversePosition headAup)
        {
            if (HectonXRRuntimeState.TryResolveCachedHeadAupFields(
                    runtimePosition,
                    out long gridX,
                    out long gridY,
                    out long gridZ,
                    out float localX,
                    out float localY,
                    out float localZ))
            {
                headAup = new AbsoluteUniversePosition
                {
                    GridX = gridX,
                    GridY = gridY,
                    GridZ = gridZ,
                    LocalX = localX,
                    LocalY = localY,
                    LocalZ = localZ
                };
                return true;
            }

            headAup = default;
            return false;
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromAbsolutePosition(HectonFloatingOrigin.CurrentTotalOffsetDouble);
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
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

        private static Quaternion ConjugateUnitQuaternion(Quaternion value)
        {
            return new Quaternion(-value.x, -value.y, -value.z, value.w);
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

        private float ResolveSuitCollisionHapticScale(float pressure01)
        {
            float depth01 = 0f;
            float integrity01 = 1f;
            float pressureSeverity01 = 0f;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null)
            {
                if (playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState))
                    depth01 = math.saturate(movementState.DepthMeters / HapticDepthReferenceMeters);

                if (playerContext.TryGetSurvivalRuntimeState(out PlayerSurvivalRuntimeState survivalState))
                {
                    integrity01 = math.saturate(survivalState.IntegrityNormalized);
                    pressureSeverity01 = math.saturate(survivalState.PressureExposureSeverity01);
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
                QueueBodyVelocityTarget(body, Vector3.zero);
                _physicsService?.QueueAngularVelocitySet(body, Vector3.zero, wake: false);
            }

            _virtualHandVelocity = Vector3.zero;
            _virtualHandTargetVelocity = Vector3.zero;
            _currentSeparationSq = 0f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError(InvalidMotionResetMessage);
#endif
        }

        private void QueueBodyVelocityTarget(Rigidbody body, Vector3 targetVelocity)
        {
            if (body == null || body.isKinematic)
                return;

            Vector3 currentVelocity = IsFinite(body.linearVelocity) ? body.linearVelocity : Vector3.zero;
            Vector3 safeTargetVelocity = IsFinite(targetVelocity) ? targetVelocity : Vector3.zero;
            if ((safeTargetVelocity - currentVelocity).sqrMagnitude > 0.0000001f)
                _physicsService?.QueueLinearVelocitySet(body, safeTargetVelocity);
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct FingerPoseData
        {
            [FieldOffset(0)] public float3 TipPosition;
            [FieldOffset(12)] public float3 TipNormal;
            [FieldOffset(24)] public float BendAngle;
            [FieldOffset(28)] private uint _pad0;
        }
    }

    internal sealed class PhysicalHandSuitCollisionShellProxy : MonoBehaviour
    {
        private PhysicalHandController _owner;

        internal void Initialize(PhysicalHandController owner)
        {
            _owner = owner;
        }

        private void OnTriggerEnter(Collider other)
        {
            _owner?.RegisterSuitShellCandidate(other);
        }

        private void OnTriggerStay(Collider other)
        {
            _owner?.RegisterSuitShellCandidate(other);
        }

        private void OnTriggerExit(Collider other)
        {
            _owner?.UnregisterSuitShellCandidate(other);
        }

        private void OnDisable()
        {
            Shutdown();
        }

        internal void Shutdown()
        {
            _owner?.ClearSuitShellCandidatesFromProxy();
            _owner = null;
        }
    }
}
