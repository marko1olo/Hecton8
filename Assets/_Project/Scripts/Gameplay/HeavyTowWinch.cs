// ============================================================================
// HECTON-8 - HeavyTowWinch.cs
// Player-owned heavy towing runtime for harpoon/winch salvage handling.
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton8.Core;
    using Hecton8.Physics;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Player-owned heavy tow runtime that binds a cable to large rigidbody salvage.
    /// Uses explicit spring/damping forces instead of a ConfigurableJoint so towing remains deterministic.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Heavy Tow Winch")]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HeavyTowWinch : MonoBehaviour, IFixedTickable
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

        [Tooltip("Velocity damping applied along the cable axis.")]
        [SerializeField, Range(0f, 80f)] private float cableDamping = 11f;

        [Tooltip("How much vertical separation contributes to tow tension.")]
        [SerializeField, Range(0f, 1f)] private float cableVerticalInfluence = 0.35f;

        [Tooltip("Absolute force cap for the manual cable solver.")]
        [SerializeField, Range(50f, 20000f)] private float maxCableForce = 5200f;

        [Tooltip("Cable extension treated as full tension for drag, COM shift, and camera response.")]
        [SerializeField, Range(0.1f, 10f)] private float fullTensionExtension = 3.8f;

        [Header("Cable Bending")]
        [Tooltip("Obstacle mask used when probing whether the tow cable should bend around geometry.")]
        [SerializeField] private LayerMask cableBendObstructionMask = ~0;

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

        [Tooltip("Absolute cap on current force applied to the payload.")]
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
        [Tooltip("Relative speed delta where tether stress starts accumulating.")]
        [SerializeField, Range(0f, 20f)] private float stressVelocityDeltaStart = 3.5f;

        [Tooltip("Relative speed delta treated as full snap stress.")]
        [SerializeField, Range(0.1f, 30f)] private float stressVelocityDeltaMax = 12.5f;

        [Tooltip("How quickly stress accumulates while the line is loaded and the speed delta keeps climbing.")]
        [SerializeField, Range(0f, 4f)] private float tetherStressBuildRate = 0.42f;

        [Tooltip("How quickly accumulated stress bleeds off when the line relaxes.")]
        [SerializeField, Range(0f, 4f)] private float tetherStressRecoveryRate = 0.28f;

        [Tooltip("Cable extension above rest length that must stay exceeded continuously before the tow line hard-snaps from over-tension.")]
        [SerializeField, Range(0f, 20f)] private float tetherSnapExtensionThreshold = 4.25f;

        [Tooltip("Continuous time the cable may remain above the critical extension threshold before snapping.")]
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
        private bool _registeredFixedTick;
        private Rigidbody _payloadBody;
        private Collider _payloadCollider;
        private string _payloadName;
        private string _payloadNameUpper;
        private float _payloadMass;
        private float _payloadMass01;
        private float _cableRestLength;
        private float _tension01;
        private float _stress01;
        private float _towDragMultiplier = 1f;
        private float _signedLateralPull01;
        private float _backwardPull01;
        private float _payloadDrift01;
        private float _criticalSnapHoldTimer;
        private bool _hasCableBendPoint;
        private Vector3 _cableBendPointWS;
        private Vector3 _cableBendNormalWS = Vector3.up;
        private Vector3 _bioCableRequestedAnchorWS;
        private Vector3 _bioCableCurrentAnchorWS;
        private float _bioCableRequestedTension01;
        private float _bioCableCurrentTension01;
        private float _bioCableRequestedCutProgress01 = 1f;
        private float _bioCableCurrentCutProgress01 = 1f;
        private float _bioCableHoldTimer;
        private bool _bioCableRequestedThisStep;
        private readonly RaycastHit[] _cableBendHitBuffer = new RaycastHit[8]; // COLD ALLOC: RaycastHit[8] — tow cable bend probe results — owner: HeavyTowWinch

        /// <summary>
        /// True while a valid heavy tow target is currently attached.
        /// </summary>
        public bool HasActiveTow => IsTowValid();

        /// <summary>
        /// Current normalized cable tension.
        /// </summary>
        public float CurrentTension01 => _tension01;

        /// <summary>
        /// Current normalized accumulated stress.
        /// </summary>
        public float CurrentStress01 => _stress01;

        /// <summary>
        /// Current drag multiplier forwarded into locomotion.
        /// </summary>
        public float CurrentTowDragMultiplier => _towDragMultiplier;

        /// <summary>
        /// Signed lateral pull in player-local space.
        /// </summary>
        public float CurrentSignedLateralPull01 => _signedLateralPull01;

        /// <summary>
        /// Backward pull amount in player-local space.
        /// </summary>
        public float CurrentBackwardPull01 => _backwardPull01;

        /// <summary>
        /// Uppercase cached payload name for UI/reporting.
        /// </summary>
        public string CurrentTargetNameUpper => _payloadNameUpper;

        private void Awake()
        {
            _cachedTransform = transform;
            if (!TryGetComponent(out _playerRigidbody))
                _playerRigidbody = GetComponent<Rigidbody>();

            if (playerMovement == null)
                TryGetComponent(out playerMovement);

            if (towAnchor == null)
            {
                Camera playerCamera = GetComponentInChildren<Camera>(true);
                if (playerCamera != null)
                    towAnchor = playerCamera.transform;
            }
        }

        private void OnEnable()
        {
            if (!_registeredFixedTick && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((IFixedTickable)this);
                _registeredFixedTick = true;
            }
        }

        private void OnDisable()
        {
            ReleaseTow(false);

            if (_registeredFixedTick && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister((IFixedTickable)this);
                _registeredFixedTick = false;
            }
        }

        /// <summary>
        /// Attempts to bind the winch to a heavy rigidbody payload.
        /// </summary>
        internal bool TryAttach(Rigidbody payloadBody, Collider payloadCollider, float initialDistance)
        {
            if (!IsTowCandidate(payloadBody))
                return false;

            if (playerMovement != null && playerMovement.IsDraggingHeavyCargo)
                return false;

            _payloadBody = payloadBody;
            _payloadCollider = payloadCollider;
            _payloadMass = payloadBody.mass;
            float massRange = math.max(maxTowMass - minTowMass, 0.01f);
            _payloadMass01 = math.saturate((_payloadMass - minTowMass) / massRange);
            _payloadName = payloadBody.gameObject.name;
            _payloadNameUpper = string.IsNullOrWhiteSpace(_payloadName) ? "CARGO" : _payloadName.ToUpperInvariant();
            _cableRestLength = math.clamp(
                math.max(1.25f, initialDistance - initialCableSlack),
                1.25f,
                maxAttachDistance);
            _tension01 = 0f;
            _stress01 = 0f;
            _towDragMultiplier = 1f;
            _signedLateralPull01 = 0f;
            _backwardPull01 = 0f;
            _payloadDrift01 = 0f;
            UpdateDiagnostics();
            return true;
        }

        internal bool CanTowMass(float mass)
        {
            return mass >= minTowMass && mass <= maxTowMass;
        }

        internal bool TryGetTowPayloadSample(out Vector3 payloadPositionWS, out float payloadRadiusWS)
        {
            payloadPositionWS = Vector3.zero;
            payloadRadiusWS = 0f;
            if (!IsTowValid())
                return false;

            payloadPositionWS = _payloadBody.worldCenterOfMass;
            if (_payloadCollider != null)
            {
                Bounds bounds = _payloadCollider.bounds;
                payloadRadiusWS = Mathf.Max(0.35f, Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)));
            }
            else
            {
                payloadRadiusWS = 0.75f;
            }

            return true;
        }

        internal void ApplyExternalCableSnare(Vector3 anchorWS, float tension01, float cutProgress01)
        {
            _bioCableRequestedThisStep = true;
            _bioCableRequestedAnchorWS = anchorWS;
            _bioCableRequestedTension01 = math.saturate(tension01);
            _bioCableRequestedCutProgress01 = math.saturate(cutProgress01);
            if (_bioCableRequestedTension01 > 0f)
                _bioCableHoldTimer = bioCableHoldTime;
        }

        /// <summary>
        /// Releases the active tow line if one exists.
        /// </summary>
        internal bool ReleaseTow(bool snapped)
        {
            if (_payloadBody == null && _payloadCollider == null)
            {
                ResetRuntimeState();
                return false;
            }

            _payloadBody = null;
            _payloadCollider = null;
            _payloadName = null;
            _payloadNameUpper = null;
            _payloadMass = 0f;
            _payloadMass01 = 0f;
            _cableRestLength = 0f;
            ResetRuntimeState();
            if (snapped)
                _debugTowTarget = "SNAPPED";
            return true;
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (!IsTowValid())
            {
                if (_payloadBody != null || _payloadCollider != null)
                    ReleaseTow(false);
                ResetRuntimeState();
                return;
            }

            if (_playerRigidbody == null)
                return;

            if (playerMovement != null && playerMovement.IsDraggingHeavyCargo)
            {
                ReleaseTow(false);
                return;
            }

            Vector3 anchorPosition = ResolveTowAnchorPosition();
            Vector3 payloadPosition = _payloadBody.worldCenterOfMass;
            AdvanceExternalCableSnare(fixedDeltaTime);
            Vector3 payloadCurrentForce = ComputePayloadCurrentForce(anchorPosition, payloadPosition);
            ApplyPayloadCurrentForce(payloadCurrentForce, fixedDeltaTime);
            SolveCable(anchorPosition, payloadPosition, fixedDeltaTime);
            UpdateDiagnostics();
        }

        private Vector3 ResolveTowAnchorPosition()
        {
            if (towAnchor != null)
                return towAnchor.position;

            return _cachedTransform != null ? _cachedTransform.position : transform.position;
        }

        private Vector3 ComputePayloadCurrentForce(Vector3 anchorPosition, Vector3 payloadPosition)
        {
            float time = Time.time;
            float3 phantomCurrentSample = CurrentManager.SampleCurrent(
                new float3(payloadPosition.x, payloadPosition.y, payloadPosition.z),
                time,
                payloadCurrentNoiseScale,
                payloadCurrentTimeScale,
                payloadCurrentStrength,
                payloadCurrentVerticalFactor);
            Vector3 phantomCurrent = new Vector3(phantomCurrentSample.x, phantomCurrentSample.y, phantomCurrentSample.z);
            Vector3 authoredCurrent = CurrentVolume.SampleAt(payloadPosition);
            Vector3 environmentCurrent = phantomCurrent + authoredCurrent;
            environmentCurrent.y *= payloadCurrentVerticalFactor;

            Vector3 currentDelta = environmentCurrent - _payloadBody.linearVelocity;
            Vector3 toPayload = payloadPosition - anchorPosition;
            Vector3 playerRight = _cachedTransform != null ? _cachedTransform.right : transform.right;
            float sideExposure = 0f;
            if (currentDelta.sqrMagnitude > 0.0001f)
                sideExposure = math.abs(Vector3.Dot(currentDelta.normalized, playerRight));

            float currentScale = math.lerp(0.55f, 1f, _payloadMass01);
            currentScale *= math.lerp(1f, payloadSideCurrentBoost, sideExposure);
            Vector3 currentForce = currentDelta * (payloadCurrentDamping * currentScale);
            float currentForceMagnitude = currentForce.magnitude;
            if (currentForceMagnitude > maxPayloadCurrentForce)
                currentForce *= maxPayloadCurrentForce / math.max(currentForceMagnitude, 0.0001f);

            float driftMagnitude = currentDelta.magnitude;
            _payloadDrift01 = math.saturate(driftMagnitude / math.max(stressVelocityDeltaMax, 0.01f));
            return currentForce;
        }

        private void ApplyPayloadCurrentForce(Vector3 payloadCurrentForce, float fixedDeltaTime)
        {
            if (payloadCurrentForce.sqrMagnitude > 0.0001f)
                ApplyClampedAcceleration(_payloadBody, payloadCurrentForce, maxPayloadCurrentForce);

            ApplyExternalCableSnareForce(fixedDeltaTime);

            if (payloadAngularDamping > 0f)
            {
                Vector3 angularVelocity = _payloadBody.angularVelocity;
                float angularBlend = 1f / (1f + payloadAngularDamping * fixedDeltaTime);
                angularVelocity.x *= angularBlend;
                angularVelocity.y *= angularBlend;
                angularVelocity.z *= angularBlend;

                float angularSpeed = angularVelocity.magnitude;
                if (angularSpeed > maxPayloadAngularSpeed)
                    angularVelocity *= maxPayloadAngularSpeed / math.max(angularSpeed, 0.0001f);

                _payloadBody.angularVelocity = angularVelocity;
            }
        }

        private void SolveCable(Vector3 anchorPosition, Vector3 payloadPosition, float fixedDeltaTime)
        {
            if (!ResolveCablePath(
                    anchorPosition,
                    payloadPosition,
                    out Vector3 playerSegmentDirection,
                    out Vector3 payloadSegmentDirection,
                    out float pathLength))
            {
                ResetRuntimeLoads();
                return;
            }

            if (pathLength > maxTowBreakDistance)
            {
                ReleaseTow(false);
                return;
            }
            Vector3 payloadVelocity = _payloadBody.linearVelocity;
            Vector3 playerVelocity = _playerRigidbody.linearVelocity;
            float verticalContribution = math.abs(payloadPosition.y - anchorPosition.y);
            float effectivePathLength = math.max(
                0f,
                pathLength - verticalContribution + (verticalContribution * math.clamp(cableVerticalInfluence, 0f, 1f)));
            float extension = math.max(0f, effectivePathLength - _cableRestLength);
            float cableLengthRate =
                Vector3.Dot(playerVelocity, playerSegmentDirection) -
                Vector3.Dot(payloadVelocity, payloadSegmentDirection);
            float cableForceMagnitude = extension * cableSpring + cableLengthRate * cableDamping;
            if (cableForceMagnitude < 0f)
                cableForceMagnitude = 0f;
            if (cableForceMagnitude > maxCableForce)
                cableForceMagnitude = maxCableForce;

            float fullExtension = math.max(fullTensionExtension, 0.01f);
            _tension01 = math.saturate(extension / fullExtension);
            UpdateTowDirectionResponse(playerSegmentDirection);

            if (cableForceMagnitude > 0f)
            {
                ApplyClampedAcceleration(_playerRigidbody, playerSegmentDirection * cableForceMagnitude, maxCableForce);
                ApplyClampedAcceleration(_payloadBody, payloadSegmentDirection * cableForceMagnitude, maxCableForce);
            }

            float load01 = math.saturate(math.max(_tension01, _payloadDrift01 * 0.72f) * math.lerp(0.45f, 1f, _payloadMass01));
            _towDragMultiplier = ResolveTowDragMultiplier(load01);
            if (playerMovement != null)
                playerMovement.ApplyEnvironmentalDrag(_towDragMultiplier);

            UpdateStress(cableLengthRate, extension, load01, fixedDeltaTime, playerSegmentDirection, payloadSegmentDirection);
        }

        private bool ResolveCablePath(
            Vector3 anchorPosition,
            Vector3 payloadPosition,
            out Vector3 playerSegmentDirection,
            out Vector3 payloadSegmentDirection,
            out float pathLength)
        {
            playerSegmentDirection = Vector3.zero;
            payloadSegmentDirection = Vector3.zero;
            pathLength = 0f;

            Vector3 directLine = payloadPosition - anchorPosition;
            float directDistance = directLine.magnitude;
            if (directDistance <= 0.0001f)
            {
                _hasCableBendPoint = false;
                _cableBendPointWS = Vector3.zero;
                _cableBendNormalWS = Vector3.up;
                return false;
            }

            Vector3 directDirection = directLine / directDistance;
            if (TryResolveCableBendPoint(payloadPosition, anchorPosition, directDirection, directDistance, out Vector3 bendPoint, out Vector3 bendNormal))
            {
                Vector3 payloadSegment = bendPoint - payloadPosition;
                Vector3 playerSegment = bendPoint - anchorPosition;
                float payloadSegmentLength = payloadSegment.magnitude;
                float playerSegmentLength = playerSegment.magnitude;
                if (payloadSegmentLength > 0.0001f && playerSegmentLength > 0.0001f)
                {
                    payloadSegmentDirection = payloadSegment / payloadSegmentLength;
                    playerSegmentDirection = playerSegment / playerSegmentLength;
                    pathLength = payloadSegmentLength + playerSegmentLength;
                    _hasCableBendPoint = true;
                    _cableBendPointWS = bendPoint;
                    _cableBendNormalWS = bendNormal;
                    return true;
                }
            }

            playerSegmentDirection = directDirection;
            payloadSegmentDirection = -directDirection;
            pathLength = directDistance;
            _hasCableBendPoint = false;
            _cableBendPointWS = Vector3.zero;
            _cableBendNormalWS = Vector3.up;
            return true;
        }

        private bool TryResolveCableBendPoint(
            Vector3 payloadPosition,
            Vector3 anchorPosition,
            Vector3 directDirection,
            float directDistance,
            out Vector3 bendPoint,
            out Vector3 bendNormal)
        {
            bendPoint = Vector3.zero;
            bendNormal = Vector3.up;

            float endpointInset = math.clamp(cableBendEndpointInset, 0.005f, directDistance * 0.45f);
            float castDistance = directDistance - endpointInset * 2f;
            if (castDistance <= 0.0001f)
                return false;

            Vector3 rayOrigin = payloadPosition - directDirection * endpointInset;
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                rayOrigin,
                -directDirection,
                _cableBendHitBuffer,
                castDistance,
                cableBendObstructionMask,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
                return false;

            float closestDistance = float.PositiveInfinity;
            RaycastHit bestHit = default;
            bool foundHit = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = _cableBendHitBuffer[i];
                Collider collider = candidate.collider;
                if (collider == null)
                    continue;

                if (ReferenceEquals(collider, _payloadCollider))
                    continue;

                Rigidbody attachedBody = collider.attachedRigidbody;
                if (attachedBody == _payloadBody || attachedBody == _playerRigidbody)
                    continue;

                if (candidate.distance < closestDistance)
                {
                    closestDistance = candidate.distance;
                    bestHit = candidate;
                    foundHit = true;
                }
            }

            if (!foundHit)
                return false;

            bendPoint = bestHit.point + bestHit.normal * math.max(0.01f, cableBendSurfaceOffset);
            bendNormal = bestHit.normal.sqrMagnitude > 0.0001f ? bestHit.normal.normalized : Vector3.up;
            return true;
        }

        private void AdvanceExternalCableSnare(float fixedDeltaTime)
        {
            if (_bioCableRequestedThisStep)
            {
                if (_bioCableRequestedTension01 > 0f)
                    _bioCableHoldTimer = bioCableHoldTime;
            }
            else if (_bioCableHoldTimer > 0f)
            {
                _bioCableHoldTimer -= fixedDeltaTime;
                if (_bioCableHoldTimer < 0f)
                    _bioCableHoldTimer = 0f;
            }

            bool keepAlive = _bioCableRequestedThisStep || _bioCableHoldTimer > 0f;
            float targetTension = keepAlive ? _bioCableRequestedTension01 : 0f;
            float targetCutProgress = keepAlive ? _bioCableRequestedCutProgress01 : 1f;
            Vector3 targetAnchor = keepAlive ? _bioCableRequestedAnchorWS : Vector3.zero;
            float blendT = 1f - math.exp(-math.max(1f, bioCableBlendSharpness) * fixedDeltaTime);

            _bioCableCurrentTension01 = math.lerp(_bioCableCurrentTension01, targetTension, blendT);
            _bioCableCurrentCutProgress01 = math.lerp(_bioCableCurrentCutProgress01, targetCutProgress, blendT);
            _bioCableCurrentAnchorWS = Vector3.Lerp(_bioCableCurrentAnchorWS, targetAnchor, blendT);

            _bioCableRequestedTension01 = 0f;
            _bioCableRequestedCutProgress01 = 1f;
            _bioCableRequestedAnchorWS = Vector3.zero;
            _bioCableRequestedThisStep = false;
        }

        private void ApplyExternalCableSnareForce(float fixedDeltaTime)
        {
            if (_payloadBody == null || _bioCableCurrentTension01 <= 0.0001f)
                return;

            float cutSuppression = 1f - math.saturate(_bioCableCurrentCutProgress01);
            float effectiveTension = _bioCableCurrentTension01 * cutSuppression;
            if (effectiveTension <= 0.0001f)
                return;

            Vector3 toAnchor = _bioCableCurrentAnchorWS - _payloadBody.worldCenterOfMass;
            if (toAnchor.sqrMagnitude > 0.0001f)
            {
                Vector3 snareForce = toAnchor.normalized * (bioCablePayloadPullForce * effectiveTension);
                ApplyClampedAcceleration(_payloadBody, snareForce, bioCablePayloadPullForce);
            }

            _stress01 = math.saturate(_stress01 + effectiveTension * bioCableStressBuildMultiplier * fixedDeltaTime);
        }

        private void UpdateTowDirectionResponse(Vector3 lineDirection)
        {
            Vector3 playerRight = _cachedTransform != null ? _cachedTransform.right : transform.right;
            Vector3 playerForward = _cachedTransform != null ? _cachedTransform.forward : transform.forward;
            _signedLateralPull01 = math.clamp(Vector3.Dot(lineDirection, playerRight), -1f, 1f);
            _backwardPull01 = math.saturate(-Vector3.Dot(lineDirection, playerForward));
        }

        private static void ApplyClampedAcceleration(Rigidbody targetBody, Vector3 acceleration, float maxAcceleration)
        {
            if (targetBody == null || maxAcceleration <= 0f)
                return;

            float3 acceleration3 = new float3(acceleration.x, acceleration.y, acceleration.z);
            if (!math.all(math.isfinite(acceleration3)))
                return;

            float sqrMagnitude = math.lengthsq(acceleration3);
            if (sqrMagnitude <= 0.000001f)
                return;

            float maxAccelerationSq = maxAcceleration * maxAcceleration;
            if (sqrMagnitude > maxAccelerationSq)
            {
                acceleration3 *= maxAcceleration / math.sqrt(sqrMagnitude);
            }

            targetBody.AddForce(new Vector3(acceleration3.x, acceleration3.y, acceleration3.z), ForceMode.Acceleration);
        }

        private float ResolveTowDragMultiplier(float load01)
        {
            if (load01 <= 0.0001f || maxTowEnvironmentalDrag <= 0f)
                return 1f;

            float exponent = math.max(0.1f, towDragExponent);
            float normalizedExp = (math.exp(load01 * exponent) - 1f) / math.max(math.exp(exponent) - 1f, 0.0001f);
            return 1f + normalizedExp * maxTowEnvironmentalDrag;
        }

        private void UpdateStress(
            float cableLengthRate,
            float extension,
            float load01,
            float fixedDeltaTime,
            Vector3 playerSegmentDirection,
            Vector3 payloadSegmentDirection)
        {
            float speedDelta = math.abs(cableLengthRate);
            float speedT = 0f;
            if (speedDelta > stressVelocityDeltaStart)
            {
                speedT = math.saturate(
                    (speedDelta - stressVelocityDeltaStart) /
                    math.max(stressVelocityDeltaMax - stressVelocityDeltaStart, 0.01f));
            }

            if (speedT > 0f && load01 > 0.0001f)
            {
                _stress01 += speedT * load01 * tetherStressBuildRate * fixedDeltaTime;
            }
            else if (_stress01 > 0f)
            {
                _stress01 -= tetherStressRecoveryRate * fixedDeltaTime;
            }

            _stress01 = math.saturate(_stress01);
            if (extension > tetherSnapExtensionThreshold && load01 > 0.0001f)
            {
                _criticalSnapHoldTimer += fixedDeltaTime;
            }
            else
            {
                _criticalSnapHoldTimer = 0f;
            }

            bool overextensionSnap = _criticalSnapHoldTimer >= tetherSnapHoldDuration;
            if (_stress01 < 1f && !overextensionSnap)
                return;

            float holdSeverity = tetherSnapHoldDuration > 0.0001f
                ? math.saturate(_criticalSnapHoldTimer / tetherSnapHoldDuration)
                : 1f;
            float snapSeverity = math.saturate(math.max(math.max(load01, speedT), holdSeverity));
            Vector3 forward = _cachedTransform != null ? _cachedTransform.forward : transform.forward;
            Vector3 releasedVelocityChange = forward * math.lerp(
                snapReleaseVelocityChangeMin,
                snapReleaseVelocityChangeMax,
                snapSeverity);
            Vector3 snapTraumaImpulse = -playerSegmentDirection * (
                snapRecoilImpulse *
                math.lerp(0.65f, 1.2f, snapSeverity) *
                _playerRigidbody.mass);
            float signedRoll = math.clamp(Vector3.Dot(playerSegmentDirection, _cachedTransform.right), -1f, 1f);
            ApplyPayloadSnapResponse(payloadSegmentDirection, snapSeverity);
            if (playerMovement != null)
            {
                playerMovement.ApplyTowCableSnapFeedback(releasedVelocityChange, snapTraumaImpulse, snapSeverity, signedRoll);
            }
            else
            {
                _playerRigidbody.AddForce(releasedVelocityChange, ForceMode.VelocityChange);
            }

            ReleaseTow(true);
        }

        private void ApplyPayloadSnapResponse(Vector3 payloadSegmentDirection, float snapSeverity)
        {
            if (_payloadBody == null)
                return;

            Vector3 payloadVelocityChange = -payloadSegmentDirection * math.lerp(
                snapPayloadVelocityChangeMin,
                snapPayloadVelocityChangeMax,
                snapSeverity);
            _payloadBody.AddForce(payloadVelocityChange, ForceMode.VelocityChange);

            Vector3 upAxis = _cachedTransform != null ? _cachedTransform.up : transform.up;
            Vector3 torqueAxis = Vector3.Cross(payloadSegmentDirection, upAxis);
            if (torqueAxis.sqrMagnitude <= 0.0001f)
                torqueAxis = _cachedTransform != null ? _cachedTransform.right : transform.right;
            else
                torqueAxis.Normalize();

            float torqueMagnitude = math.lerp(
                snapPayloadTorqueVelocityChangeMin,
                snapPayloadTorqueVelocityChangeMax,
                snapSeverity);
            Vector3 payloadTorqueVelocityChange = torqueAxis * torqueMagnitude;
            _payloadBody.AddTorque(payloadTorqueVelocityChange, ForceMode.VelocityChange);

            if (_payloadBody.TryGetComponent(out ITowSnapReceiver snapReceiver))
            {
                snapReceiver.HandleTowCableSnap(
                    new TowSnapEventData(
                        _payloadBody,
                        payloadSegmentDirection,
                        payloadVelocityChange,
                        payloadTorqueVelocityChange,
                        snapSeverity));
            }
            else if (_payloadCollider != null && _payloadCollider.TryGetComponent(out snapReceiver))
            {
                snapReceiver.HandleTowCableSnap(
                    new TowSnapEventData(
                        _payloadBody,
                        payloadSegmentDirection,
                        payloadVelocityChange,
                        payloadTorqueVelocityChange,
                        snapSeverity));
            }
        }

        private bool IsTowCandidate(Rigidbody payloadBody)
        {
            if (payloadBody == null ||
                payloadBody == _playerRigidbody ||
                payloadBody.isKinematic)
            {
                return false;
            }

            float mass = payloadBody.mass;
            return mass >= minTowMass &&
                   mass <= maxTowMass;
        }

        private bool IsTowValid()
        {
            return _payloadBody != null &&
                   _payloadCollider != null &&
                   _payloadBody.gameObject.activeInHierarchy &&
                   !_payloadBody.isKinematic &&
                   _payloadBody.mass >= minTowMass &&
                   _payloadBody.mass <= maxTowMass;
        }

        private void ResetRuntimeLoads()
        {
            _tension01 = 0f;
            _towDragMultiplier = 1f;
            _signedLateralPull01 = 0f;
            _backwardPull01 = 0f;
            _payloadDrift01 = 0f;
        }

        private void ResetRuntimeState()
        {
            _tension01 = 0f;
            _stress01 = 0f;
            _towDragMultiplier = 1f;
            _signedLateralPull01 = 0f;
            _backwardPull01 = 0f;
            _payloadDrift01 = 0f;
            _bioCableRequestedAnchorWS = Vector3.zero;
            _bioCableCurrentAnchorWS = Vector3.zero;
            _bioCableRequestedTension01 = 0f;
            _bioCableCurrentTension01 = 0f;
            _bioCableRequestedCutProgress01 = 1f;
            _bioCableCurrentCutProgress01 = 1f;
            _bioCableHoldTimer = 0f;
            _criticalSnapHoldTimer = 0f;
            _bioCableRequestedThisStep = false;
            UpdateDiagnostics();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugTowActive = IsTowValid();
            _debugTowTarget = _payloadNameUpper;
            _debugTowMass = _payloadMass;
            _debugTension01 = _tension01;
            _debugStress01 = _stress01;
            _debugTowDragMultiplier = _towDragMultiplier;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_hasCableBendPoint || _payloadBody == null)
                return;

            Vector3 anchorPosition = ResolveTowAnchorPosition();
            Vector3 payloadPosition = _payloadBody.worldCenterOfMass;
            Gizmos.color = new Color(0.2f, 0.95f, 0.9f, 0.85f);
            Gizmos.DrawLine(anchorPosition, _cableBendPointWS);
            Gizmos.DrawLine(_cableBendPointWS, payloadPosition);
            Gizmos.DrawWireSphere(_cableBendPointWS, 0.08f);
            Gizmos.color = new Color(1f, 0.65f, 0.2f, 0.85f);
            Gizmos.DrawLine(_cableBendPointWS, _cableBendPointWS + (_cableBendNormalWS * 0.35f));
        }

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
            if (stressVelocityDeltaMax < stressVelocityDeltaStart + 0.01f)
                stressVelocityDeltaMax = stressVelocityDeltaStart + 0.01f;
            if (maxCableForce < 50f)
                maxCableForce = 50f;
            if (maxPayloadCurrentForce < 50f)
                maxPayloadCurrentForce = 50f;
            if (maxPayloadAngularSpeed < 0.1f)
                maxPayloadAngularSpeed = 0.1f;
            if (tetherSnapHoldDuration < 0.1f)
                tetherSnapHoldDuration = 0.1f;
            if (tetherSnapExtensionThreshold < 0f)
                tetherSnapExtensionThreshold = 0f;
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
