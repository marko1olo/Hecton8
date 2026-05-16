// ============================================================================
// HECTON-8 - HeavyTowWinch.cs
// Player-owned heavy tow facade backed by TetherManager/TetherInstance.
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton8.Physics;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Player-owned heavy tow facade that binds a managed tether runtime to a salvage payload.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Heavy Tow Winch")]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(TetherManager))]
    public sealed class HeavyTowWinch : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Optional tow origin. Falls back to the player camera and then the local transform.")]
        [SerializeField] private Transform towAnchor;

        [Tooltip("Optional explicit locomotion owner used for drag and snap feedback.")]
        [SerializeField] private HectonPlayerMovement playerMovement;

        [Header("Tow Envelope")]
        [Tooltip("Minimum rigidbody mass allowed for heavy towing.")]
        [SerializeField, Range(50f, 1000f)] private float minTowMass = 50f;

        [Tooltip("Maximum rigidbody mass allowed for heavy towing.")]
        [SerializeField, Range(50f, 2000f)] private float maxTowMass = 1000f;

        [Tooltip("Maximum initial distance where a tow line can still lock.")]
        [SerializeField, Range(1f, 80f)] private float maxAttachDistance = 42f;

        [Tooltip("Slack preserved when the tow line first attaches so the cable does not start fully rigid.")]
        [SerializeField, Range(0f, 3f)] private float initialCableSlack = 0.85f;

        [Tooltip("Hard fail-safe distance where the tow line is considered lost even before stress reaches snap.")]
        [SerializeField, Range(2f, 120f)] private float maxTowBreakDistance = 58f;

        [Header("Cable Tension")]
        [Tooltip("Hooke spring coefficient applied once the cable goes taut.")]
        [SerializeField, Range(0f, 200f)] private float cableSpring = 48f;

#pragma warning disable CS0414
        [Tooltip("Legacy serialized damping field retained for scene compatibility. Runtime damping is now derived from reduced mass and critical damping.")]
        [SerializeField] private float cableDamping = 11f;

        [Tooltip("Legacy serialized field retained for scene compatibility. Runtime tether length is now solved against the actual path.")]
        [SerializeField, Range(0f, 1f)] private float cableVerticalInfluence = 0.35f;

        [Tooltip("Absolute acceleration cap for the tether spring solver.")]
        [SerializeField, Range(50f, 20000f)] private float maxCableForce = 5200f;

        [Tooltip("Cable extension treated as full tension for drag, COM shift, and camera response.")]
        [SerializeField, Range(0.1f, 10f)] private float fullTensionExtension = 3.8f;

        [Header("Cable Bending")]
        [Tooltip("Obstacle mask used when probing whether the tow cable should bend around geometry.")]
        [SerializeField] private LayerMask cableBendObstructionMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Tooltip("How far the stored bend point is pushed off the blocking surface normal to avoid self-intersection.")]
        [SerializeField, Range(0.01f, 1f)] private float cableBendSurfaceOffset = 0.12f;

        [Tooltip("Inset applied to both cable endpoints before the bend raycast starts so the tow line does not instantly hit the rider or payload colliders.")]
        [SerializeField, Range(0.005f, 0.5f)] private float cableBendEndpointInset = 0.08f;

        [Header("Payload Current Drag")]
        [Tooltip("Baseline environmental flow coupling applied to the payload body.")]
        [SerializeField, Range(0f, 10f)] private float payloadCurrentStrength = 1.15f;

        [Tooltip("Extra force multiplier when the sampled current hits the payload from the side.")]
        [SerializeField, Range(0f, 8f)] private float payloadSideCurrentBoost = 1.75f;

        [Tooltip("How much vertical current is preserved while drifting a payload.")]
        [SerializeField, Range(0f, 1f)] private float payloadCurrentVerticalFactor = 0.18f;

        [Tooltip("Noise scale used for the global phantom current sample on the payload.")]
        [SerializeField, Range(0.001f, 0.2f)] private float payloadCurrentNoiseScale = 0.018f;

        [Tooltip("Time scale used for the global phantom current sample on the payload.")]
        [SerializeField, Range(0.01f, 1f)] private float payloadCurrentTimeScale = 0.12f;

        [Tooltip("Linear damping applied against payload velocity while current drags it sideways.")]
        [SerializeField, Range(0f, 20f)] private float payloadCurrentDamping = 1.9f;

        [Tooltip("Absolute cap on current acceleration applied to the payload.")]
        [SerializeField, Range(50f, 20000f)] private float maxPayloadCurrentForce = 2800f;

        [Tooltip("Angular damping applied every fixed step so wedged cargo does not helicopter.")]
        [SerializeField, Range(0f, 20f)] private float payloadAngularDamping = 3.2f;

        [Tooltip("Maximum angular speed allowed on the towed payload.")]
        [SerializeField, Range(0.1f, 30f)] private float maxPayloadAngularSpeed = 3.6f;

        [Header("Tow Drag")]
        [Tooltip("Exponent used when converting tow load into player drag.")]
        [SerializeField, Range(0.1f, 8f)] private float towDragExponent = 2.1f;

        [Tooltip("Maximum extra drag authored by a fully loaded tow line.")]
        [SerializeField, Range(0f, 10f)] private float maxTowEnvironmentalDrag = 3.8f;

        [Header("Cable Snap")]
        [Tooltip("Legacy serialized field retained for scene compatibility. Stress now accumulates from peak tether tension instead of relative-speed spikes.")]
        [SerializeField, Range(0f, 20f)] private float stressVelocityDeltaStart = 3.5f;

        [Tooltip("Legacy serialized field retained for scene compatibility. Stress now accumulates from peak tether tension instead of relative-speed spikes.")]
        [SerializeField, Range(0.1f, 30f)] private float stressVelocityDeltaMax = 12.5f;

        [Tooltip("Legacy serialized field retained for scene compatibility. Runtime uses tension-duration stress accumulation.")]
        [SerializeField, Range(0f, 4f)] private float tetherStressBuildRate = 0.42f;

        [Tooltip("Legacy serialized field retained for scene compatibility. Runtime uses tension-duration stress recovery.")]
        [SerializeField, Range(0f, 4f)] private float tetherStressRecoveryRate = 0.28f;

        [Tooltip("Legacy serialized field retained for scene compatibility. Hard limits now come from the non-elastic 110% constraint.")]
        [SerializeField, Range(0f, 20f)] private float tetherSnapExtensionThreshold = 4.25f;
#pragma warning restore CS0414

        [Tooltip("Time peak tension must remain above threshold before the tow line snaps.")]
        [SerializeField, Range(0.1f, 5f)] private float tetherSnapHoldDuration = 1.5f;

        [Tooltip("Minimum forward velocity change applied to the player when the cable snaps.")]
        [SerializeField, Range(0f, 20f)] private float snapReleaseVelocityChangeMin = 5.4f;

        [Tooltip("Maximum forward velocity change applied to the player when the cable snaps.")]
        [SerializeField, Range(0f, 20f)] private float snapReleaseVelocityChangeMax = 10.5f;

        [Tooltip("Camera/body trauma impulse magnitude applied on snap recoil.")]
        [SerializeField, Range(0f, 40f)] private float snapRecoilImpulse = 8.5f;

        [Tooltip("Minimum reverse velocity change applied to the payload when the cable snaps.")]
        [SerializeField, Range(0f, 20f)] private float snapPayloadVelocityChangeMin = 4.8f;

        [Tooltip("Maximum reverse velocity change applied to the payload when the cable snaps.")]
        [SerializeField, Range(0f, 24f)] private float snapPayloadVelocityChangeMax = 9.2f;

        [Tooltip("Minimum angular velocity change injected into the payload when the cable snaps.")]
        [SerializeField, Range(0f, 30f)] private float snapPayloadTorqueVelocityChangeMin = 7.5f;

        [Tooltip("Maximum angular velocity change injected into the payload when the cable snaps.")]
        [SerializeField, Range(0f, 40f)] private float snapPayloadTorqueVelocityChangeMax = 16f;

        [Header("Bio-Cable Snare")]
        [Tooltip("How much bio-cable grip on the towed payload accelerates tether stress buildup.")]
        [SerializeField, Range(0f, 4f)] private float bioCableStressBuildMultiplier = 1.2f;

        [Tooltip("Force applied toward the gripping bio-cable anchor while the payload is snagged.")]
        [SerializeField, Range(0f, 8000f)] private float bioCablePayloadPullForce = 1600f;

        [Tooltip("How long a payload cable snare stays armed without refresh before recovering.")]
        [SerializeField, Range(0f, 0.5f)] private float bioCableHoldTime = 0.12f;

        [Tooltip("How quickly payload cable snare influence blends toward the newest sample.")]
        [SerializeField, Range(1f, 24f)] private float bioCableBlendSharpness = 8f;

        [Header("Diagnostics")]
#pragma warning disable CS0414
        [SerializeField] private bool _debugTowActive;
        [SerializeField] private string _debugTowTarget;
        [SerializeField] private float _debugTowMass;
        [SerializeField] private float _debugTension01;
        [SerializeField] private float _debugStress01;
        [SerializeField] private float _debugTowDragMultiplier = 1f;
#pragma warning restore CS0414

        private Transform _cachedTransform;
        private Rigidbody _playerRigidbody;
        private HectonPlayerMotor _playerMotor;
        private TetherManager _tetherManager;
        private TetherInstance _activeTether;
        private string _payloadName;
        private string _payloadNameUpper;
        private float _payloadMass;
        private Transform _overrideTowAnchor;
        private Rigidbody _overrideTowBody;

        /// <summary>True while a valid heavy tow target is currently attached.</summary>
        public bool HasActiveTow => _activeTether != null && _activeTether.IsActive;

        /// <summary>Current normalized cable tension.</summary>
        public float CurrentTension01 => _activeTether != null ? _activeTether.CurrentTension01 : 0f;

        /// <summary>Current normalized accumulated stress.</summary>
        public float CurrentStress01 => _activeTether != null ? _activeTether.CurrentStress01 : 0f;

        /// <summary>Current drag multiplier forwarded into locomotion.</summary>
        public float CurrentTowDragMultiplier => _activeTether != null ? _activeTether.CurrentTowDragMultiplier : 1f;

        /// <summary>Signed lateral pull in player-local space.</summary>
        public float CurrentSignedLateralPull01 => _activeTether != null ? _activeTether.CurrentSignedLateralPull01 : 0f;

        /// <summary>Backward pull amount in player-local space.</summary>
        public float CurrentBackwardPull01 => _activeTether != null ? _activeTether.CurrentBackwardPull01 : 0f;

        /// <summary>Uppercase cached payload name for UI/reporting.</summary>
        public string CurrentTargetNameUpper => _payloadNameUpper;

        /// <summary>Runtime winch target length. External input can reel this value in or out.</summary>
        public float TargetLength { get; private set; }

        internal Transform CachedTransform => _cachedTransform != null ? _cachedTransform : transform;
        internal Transform ActiveTowAnchorTransform => _overrideTowAnchor != null ? _overrideTowAnchor : (towAnchor != null ? towAnchor : CachedTransform);
        internal Vector3 PlayerRight => ActiveTowAnchorTransform.right;
        internal Vector3 PlayerForward => ActiveTowAnchorTransform.forward;
        internal Vector3 PlayerUp => ActiveTowAnchorTransform.up;
        internal bool ShouldSuppressTow => playerMovement != null && playerMovement.IsDraggingHeavyCargo;

        private void Awake()
        {
            _cachedTransform = transform;
            if (!TryGetComponent(out _playerRigidbody))
                _playerRigidbody = GetComponent<Rigidbody>();

            if (playerMovement == null)
                TryGetComponent(out playerMovement);

            if (!TryGetComponent(out _playerMotor))
                TryGetComponent(out _playerMotor);

            if (!TryGetComponent(out _tetherManager))
                _tetherManager = GetComponent<TetherManager>();

            if (towAnchor == null)
            {
                Camera playerCamera = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Camera>(transform);
                if (playerCamera != null)
                    towAnchor = playerCamera.transform;
            }
        }

        private void OnDisable()
        {
            ReleaseTow(false);
        }

        /// <summary>
        /// Attempts to bind the winch to a heavy rigidbody payload.
        /// </summary>
        internal bool TryAttach(Rigidbody payloadBody, Collider payloadCollider, float initialDistance)
        {
            if (!IsTowCandidate(payloadBody) || payloadCollider == null || ShouldSuppressTow)
                return false;

            if (_tetherManager == null)
                return false;

            if (!TetherSignals.PublishFire(
                    _tetherManager,
                    this,
                    _playerMotor,
                    _playerRigidbody,
                    payloadBody,
                    payloadCollider,
                    initialDistance,
                    _tetherManager.CurrentFixedFrameIndex))
            {
                return false;
            }

            return _tetherManager.ExecuteFireRequest(
                this,
                _playerMotor,
                _playerRigidbody,
                payloadBody,
                payloadCollider,
                initialDistance);
        }

        internal bool CanTowMass(float mass)
        {
            return mass >= minTowMass && mass <= maxTowMass;
        }

        internal bool TryGetTowPayloadSample(out Vector3 payloadPositionWS, out float payloadRadiusWS)
        {
            if (_activeTether != null)
                return _activeTether.TryGetPayloadSample(out payloadPositionWS, out payloadRadiusWS);

            payloadPositionWS = Vector3.zero;
            payloadRadiusWS = 0f;
            return false;
        }

        internal void ApplyExternalCableSnare(Vector3 anchorWS, float tension01, float cutProgress01)
        {
            if (_activeTether == null)
                return;

            _activeTether.QueueExternalCableSnare(anchorWS, tension01, cutProgress01);
        }

        /// <summary>
        /// Releases the active tow line if one exists.
        /// </summary>
        internal bool ReleaseTow(bool snapped)
        {
            if (_activeTether == null)
            {
                ApplyTowLoad(1f);
                ResetRuntimeState();
                return false;
            }

            TetherInstance active = _activeTether;
            _activeTether = null;
            if (_tetherManager != null)
                _tetherManager.DetachTether(active, snapped, false);

            ApplyTowLoad(1f);
            ResetRuntimeState();
            if (snapped)
                _debugTowTarget = "SNAPPED";

            UpdateDiagnostics();
            return true;
        }

        internal void OnTetherDetached(TetherInstance instance, bool snapped)
        {
            if (!ReferenceEquals(instance, _activeTether))
                return;

            _activeTether = null;
            ApplyTowLoad(1f);
            ResetRuntimeState();
            if (snapped)
                _debugTowTarget = "SNAPPED";

            UpdateDiagnostics();
        }

        internal bool CompleteSignalAttach(TetherInstance instance, Rigidbody payloadBody)
        {
            if (instance == null || payloadBody == null)
                return false;

            _overrideTowAnchor = null;
            _overrideTowBody = null;
            _activeTether = instance;
            CachePayloadIdentity(payloadBody);
            UpdateDiagnostics();
            return true;
        }

        internal Vector3 ResolveTowAnchorPosition()
        {
            return ActiveTowAnchorTransform.position;
        }

        internal bool IsTowPayloadValid(Rigidbody payloadBody, Collider payloadCollider)
        {
            return payloadBody != null &&
                   payloadCollider != null &&
                   payloadBody.gameObject.activeInHierarchy &&
                   !payloadBody.isKinematic &&
                   payloadBody.mass >= minTowMass &&
                   payloadBody.mass <= maxTowMass;
        }

        internal float ResolveTowSpringStiffness() => math.max(0f, cableSpring);
        internal float ResolveTowOverDampingMultiplier() => 1.2f;
        internal float ResolveTowRestLength(float initialDistance)
        {
            TargetLength = math.clamp(
                math.max(1.25f, initialDistance - initialCableSlack),
                1.25f,
                maxAttachDistance);
            return TargetLength;
        }

        public void SetTargetLength(float targetLength)
        {
            if (!math.isfinite(targetLength))
                return;

            TargetLength = math.clamp(targetLength, 1.25f, maxAttachDistance);
        }

        public void AdjustTargetLength(float deltaMeters)
        {
            if (!math.isfinite(deltaMeters))
                return;

            SetTargetLength(TargetLength + deltaMeters);
        }

        internal float ResolveMaxTowBreakDistance() => math.max(1.25f, maxTowBreakDistance);
        internal float ResolveMaxCableAcceleration() => math.max(1f, maxCableForce);
        internal float ResolveFullTensionExtension() => math.max(0.1f, fullTensionExtension);
        internal int ResolveMaxBendPoints() => 4;
        internal float ResolveBendPointClearanceRadius() => math.max(0.3f, cableBendSurfaceOffset);
        internal LayerMask ResolveCableBendObstructionMask() => cableBendObstructionMask;
        internal float ResolveBendSurfaceOffset() => math.max(0.01f, cableBendSurfaceOffset);
        internal float ResolveBendEndpointInset() => math.max(0.005f, cableBendEndpointInset);
        internal int ResolveVisualSegmentCount() => 16;
        internal float ResolveVisualSegmentSmoothSpeed() => 12f;
        internal float ResolvePayloadCurrentStrength() => math.max(0f, payloadCurrentStrength);
        internal float ResolvePayloadSideCurrentBoost() => math.max(0f, payloadSideCurrentBoost);
        internal float ResolvePayloadCurrentVerticalFactor() => math.clamp(payloadCurrentVerticalFactor, 0f, 1f);
        internal float ResolvePayloadCurrentNoiseScale() => math.max(0f, payloadCurrentNoiseScale);
        internal float ResolvePayloadCurrentTimeScale() => math.max(0f, payloadCurrentTimeScale);
        internal float ResolvePayloadCurrentDamping() => math.max(0f, payloadCurrentDamping);
        internal float ResolveMaxPayloadCurrentForce() => math.max(1f, maxPayloadCurrentForce);
        internal float ResolvePayloadAngularDamping() => math.max(0f, payloadAngularDamping);
        internal float ResolveMaxPayloadAngularSpeed() => math.max(0.1f, maxPayloadAngularSpeed);
        internal float ResolveBioCableStressBuildMultiplier() => math.max(0f, bioCableStressBuildMultiplier);
        internal float ResolveBioCablePayloadPullForce() => math.max(0f, bioCablePayloadPullForce);
        internal float ResolveBioCableHoldTime() => math.clamp(bioCableHoldTime, 0f, 0.5f);
        internal float ResolveBioCableBlendSharpness() => math.max(1f, bioCableBlendSharpness);
        internal float ResolvePayloadMass01(float payloadMass)
        {
            float massRange = math.max(maxTowMass - minTowMass, 0.01f);
            return math.saturate((payloadMass - minTowMass) / massRange);
        }

        internal float ResolveTowDragMultiplier(float load01)
        {
            if (load01 <= 0.0001f || maxTowEnvironmentalDrag <= 0f)
                return 1f;

            float exponent = math.max(0.1f, towDragExponent);
            float loadedRise = FastTowDragRise(load01 * exponent);
            float fullRise = math.max(FastTowDragRise(exponent), 0.0001f);
            return 1f + (loadedRise / fullRise) * maxTowEnvironmentalDrag;
        }

        private static float FastTowDragRise(float x)
        {
            float clamped = math.max(0f, x);
            float x2 = clamped * clamped;
            float fakeExp = 1f + clamped + (0.48f * x2) + (0.235f * x2 * clamped);
            return math.max(0f, fakeExp - 1f);
        }

        internal float ResolveSnapTensionThreshold() => math.max(1f, maxCableForce);
        internal float ResolveSnapStressDuration() => math.max(0.1f, tetherSnapHoldDuration);

        internal void ApplyTowLoad(float towDragMultiplier)
        {
            if (playerMovement == null)
                return;

            playerMovement.ApplyEnvironmentalDrag(IsTowBoundToPlayer() ? towDragMultiplier : 1f);
        }

        internal bool TryResolveSharedTransportPlatform(
            Transform payloadTransform,
            Collider payloadCollider,
            out ITransportPlatform platform,
            out Matrix4x4 worldToLocalMatrix,
            out Matrix4x4 localToWorldMatrix)
        {
            platform = null;
            worldToLocalMatrix = Matrix4x4.identity;
            localToWorldMatrix = Matrix4x4.identity;
            if (playerMovement == null || payloadTransform == null || !playerMovement.TryGetActiveTransportPlatform(out platform))
                return false;

            Transform platformTransform = platform.PlatformTransform;
            if (platformTransform == null)
            {
                platform = null;
                return false;
            }

            bool payloadInsidePlatform =
                ReferenceEquals(payloadTransform, platformTransform) ||
                payloadTransform.IsChildOf(platformTransform) ||
                (payloadCollider != null && (
                    ReferenceEquals(payloadCollider.transform, platformTransform) ||
                    payloadCollider.transform.IsChildOf(platformTransform)));
            if (!payloadInsidePlatform)
            {
                platform = null;
                return false;
            }

            worldToLocalMatrix = platformTransform.worldToLocalMatrix;
            localToWorldMatrix = platformTransform.localToWorldMatrix;
            return true;
        }

        internal bool TryTransferTowToTransport(Rigidbody transportBody, Transform transportAnchor)
        {
            if (_activeTether == null || transportBody == null || transportAnchor == null)
                return false;

            if (!_activeTether.TryGetPayloadBody(out Rigidbody payloadBody) || payloadBody == null)
                return false;

            float exosuitMass = math.max(transportBody.mass, 0.0001f);
            float wreckMass = math.max(payloadBody.mass, 0.0001f);
            Vector3 exosuitVelocity = transportBody.linearVelocity;
            Vector3 wreckVelocity = payloadBody.linearVelocity;
            Vector3 targetVelocity = ((exosuitMass * exosuitVelocity) + (wreckMass * wreckVelocity)) /
                                     math.max(exosuitMass + wreckMass, 0.0001f);
            Vector3 velocityChange = targetVelocity - exosuitVelocity;

            _overrideTowAnchor = transportAnchor;
            _overrideTowBody = transportBody;
            _activeTether.RetargetAnchorEndpoint(null, transportBody);
            transportBody.WakeUp();
            if (velocityChange.sqrMagnitude > 0.000001f)
                PhysicsForceRouter.QueueForce(transportBody, velocityChange, ForceMode.VelocityChange);

            ApplyTowLoad(1f);
            UpdateDiagnostics();
            return true;
        }

        internal void HandleTetherSnap(
            Vector3 playerSegmentDirection,
            Vector3 payloadSegmentDirection,
            float snapSeverity,
            bool suppressPlayerFeedback,
            Rigidbody payloadBody,
            Collider payloadCollider)
        {
            if (suppressPlayerFeedback)
                return;

            float clampedSeverity = math.saturate(math.max(snapSeverity, 0.01f));
            Vector3 playerForward = PlayerForward;
            Vector3 playerRight = PlayerRight;
            Vector3 playerUp = PlayerUp;
            Rigidbody activeTowBody = ResolveActiveTowBody();
            float activeTowMass = activeTowBody != null ? activeTowBody.mass : 1f;
            Vector3 releasedVelocityChange = playerForward * math.lerp(
                snapReleaseVelocityChangeMin,
                snapReleaseVelocityChangeMax,
                clampedSeverity);
            Vector3 snapTraumaImpulse = -playerSegmentDirection * (
                snapRecoilImpulse *
                math.lerp(0.65f, 1.2f, clampedSeverity) *
                activeTowMass);
            float signedRoll = math.clamp(math.dot(ToFloat3(playerSegmentDirection), ToFloat3(playerRight)), -1f, 1f);
            ApplyPayloadSnapResponse(payloadBody, payloadCollider, payloadSegmentDirection, playerUp, playerRight, clampedSeverity);

            if (IsTowBoundToPlayer() && playerMovement != null)
            {
                playerMovement.ApplyTowCableSnapFeedback(releasedVelocityChange, snapTraumaImpulse, clampedSeverity, signedRoll);
            }
            else if (IsTowBoundToPlayer() && _playerMotor != null)
            {
                _playerMotor.ApplyVelocityChange(releasedVelocityChange);
            }
            else
            {
                if (activeTowBody != null)
                    PhysicsForceRouter.QueueForce(activeTowBody, releasedVelocityChange, ForceMode.VelocityChange);
            }
        }

        private void ApplyPayloadSnapResponse(
            Rigidbody payloadBody,
            Collider payloadCollider,
            Vector3 payloadSegmentDirection,
            Vector3 playerUp,
            Vector3 playerRight,
            float snapSeverity)
        {
            if (payloadBody == null)
                return;

            Vector3 payloadVelocityChange = -payloadSegmentDirection * math.lerp(
                snapPayloadVelocityChangeMin,
                snapPayloadVelocityChangeMax,
                snapSeverity);
            PhysicsForceRouter.QueueForce(payloadBody, payloadVelocityChange, ForceMode.VelocityChange);

            Vector3 torqueAxis = Vector3.Cross(payloadSegmentDirection, playerUp);
            float torqueAxisSq = torqueAxis.sqrMagnitude;
            if (torqueAxisSq <= 0.0001f || !math.all(math.isfinite(ToFloat3(torqueAxis))))
                torqueAxis = playerRight;
            else
                torqueAxis *= math.rsqrt(torqueAxisSq);

            Vector3 payloadTorqueVelocityChange = torqueAxis * math.lerp(
                snapPayloadTorqueVelocityChangeMin,
                snapPayloadTorqueVelocityChangeMax,
                snapSeverity);
            PhysicsForceRouter.QueueTorque(payloadBody, payloadTorqueVelocityChange, ForceMode.VelocityChange);

            if (payloadBody.TryGetComponent(out ITowSnapReceiver snapReceiver))
            {
                snapReceiver.HandleTowCableSnap(
                    new TowSnapEventData(
                        payloadBody,
                        payloadSegmentDirection,
                        payloadVelocityChange,
                        payloadTorqueVelocityChange,
                        snapSeverity));
                return;
            }

            if (payloadCollider != null && payloadCollider.TryGetComponent(out snapReceiver))
            {
                snapReceiver.HandleTowCableSnap(
                    new TowSnapEventData(
                        payloadBody,
                        payloadSegmentDirection,
                        payloadVelocityChange,
                        payloadTorqueVelocityChange,
                        snapSeverity));
            }
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private void CachePayloadIdentity(Rigidbody payloadBody)
        {
            _payloadMass = payloadBody != null ? payloadBody.mass : 0f;
            _payloadName = payloadBody != null ? payloadBody.gameObject.name : null;
            _payloadNameUpper = string.IsNullOrWhiteSpace(_payloadName) ? "CARGO" : _payloadName.ToUpperInvariant();
        }

        private bool IsTowCandidate(Rigidbody payloadBody)
        {
            if (payloadBody == null || payloadBody == _playerRigidbody || payloadBody.isKinematic)
                return false;

            float mass = payloadBody.mass;
            return mass >= minTowMass && mass <= maxTowMass;
        }

        private void ResetRuntimeState()
        {
            _payloadName = null;
            _payloadNameUpper = null;
            _payloadMass = 0f;
            TargetLength = 0f;
            _overrideTowAnchor = null;
            _overrideTowBody = null;
        }

        private Rigidbody ResolveActiveTowBody()
        {
            return _overrideTowBody != null ? _overrideTowBody : _playerRigidbody;
        }

        private bool IsTowBoundToPlayer()
        {
            return _overrideTowBody == null || ReferenceEquals(_overrideTowBody, _playerRigidbody);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugTowActive = HasActiveTow;
            _debugTowTarget = _payloadNameUpper;
            _debugTowMass = _payloadMass;
            _debugTension01 = CurrentTension01;
            _debugStress01 = CurrentStress01;
            _debugTowDragMultiplier = CurrentTowDragMultiplier;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (maxTowMass < minTowMass)
                maxTowMass = minTowMass;
            if (maxAttachDistance < 1f)
                maxAttachDistance = 1f;
            if (maxTowBreakDistance < maxAttachDistance)
                maxTowBreakDistance = maxAttachDistance;
            if (fullTensionExtension < 0.1f)
                fullTensionExtension = 0.1f;
            if (tetherSnapHoldDuration < 0.1f)
                tetherSnapHoldDuration = 0.1f;
            if (maxCableForce < 50f)
                maxCableForce = 50f;
            if (maxPayloadCurrentForce < 50f)
                maxPayloadCurrentForce = 50f;
            if (maxPayloadAngularSpeed < 0.1f)
                maxPayloadAngularSpeed = 0.1f;
            if (snapPayloadVelocityChangeMin < 0f)
                snapPayloadVelocityChangeMin = 0f;
            if (snapPayloadVelocityChangeMax < snapPayloadVelocityChangeMin)
                snapPayloadVelocityChangeMax = snapPayloadVelocityChangeMin;
            if (snapPayloadTorqueVelocityChangeMin < 0f)
                snapPayloadTorqueVelocityChangeMin = 0f;
            if (snapPayloadTorqueVelocityChangeMax < snapPayloadTorqueVelocityChangeMin)
                snapPayloadTorqueVelocityChangeMax = snapPayloadTorqueVelocityChangeMin;
            if (bioCableStressBuildMultiplier < 0f)
                bioCableStressBuildMultiplier = 0f;
            if (bioCablePayloadPullForce < 0f)
                bioCablePayloadPullForce = 0f;
            if (bioCableHoldTime < 0f)
                bioCableHoldTime = 0f;
            if (bioCableHoldTime > 0.5f)
                bioCableHoldTime = 0.5f;
            if (bioCableBlendSharpness < 1f)
                bioCableBlendSharpness = 1f;
        }
#endif
    }
}
