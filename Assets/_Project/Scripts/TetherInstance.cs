using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    internal enum TetherLifecycleState : byte
    {
        Alive = 0,
        Released = 1,
        Snapped = 2
    }

    /// <summary>
    /// Per-tether runtime state and solver.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TetherInstance : MonoBehaviour
    {
        private const int MaxSupportedBendPoints = 4;
        private const int MaxSegments = MaxSupportedBendPoints + 1;
        private const int MaxAnchors = MaxSegments + 1;
        private const int BendRecheckCooldownFrames = 3;
        private const float MinDistance = 0.0001f;
        private const float MinVectorMagnitudeSq = 0.000001f;
        private const float TowCableOverDampingMinimum = 1.2f;
        private const float NonElasticLimitRatio = 1.10f;
        private const int MinVisualSegmentCount = 8;
        private const int MaxVisualSegmentCount = 24;
        private const float VisualSagScale = 0.05f;

        // COLD ALLOC: Vector3[4] — virtual bend point corner cache for this tether instance — owner: TetherInstance
        private readonly Vector3[] _bendPoints = new Vector3[MaxSupportedBendPoints];
        // COLD ALLOC: Vector3[4] — per-bend normal cache for debug/render stabilization — owner: TetherInstance
        private readonly Vector3[] _bendNormals = new Vector3[MaxSupportedBendPoints];
        // COLD ALLOC: Vector3[6] — authoritative physics anchor chain snapshot (player, bends, payload) — owner: TetherInstance
        private readonly Vector3[] _anchorPositions = new Vector3[MaxAnchors];
        // COLD ALLOC: Vector3[6] — anchor velocities aligned to _anchorPositions — owner: TetherInstance
        private readonly Vector3[] _anchorVelocities = new Vector3[MaxAnchors];
        // COLD ALLOC: Vector3[6] — solver-space anchor positions (world or platform-local) used by the constraint solver — owner: TetherInstance
        private readonly Vector3[] _solverAnchorPositions = new Vector3[MaxAnchors];
        // COLD ALLOC: Vector3[6] — solver-space anchor velocities aligned to _solverAnchorPositions — owner: TetherInstance
        private readonly Vector3[] _solverAnchorVelocities = new Vector3[MaxAnchors];
        // COLD ALLOC: float[5] — per-segment rest-length distribution across bends — owner: TetherInstance
        private readonly float[] _segmentRestLengths = new float[MaxSegments];
        // COLD ALLOC: float[5] — per-segment runtime lengths used by solver and visual sampling — owner: TetherInstance
        private readonly float[] _segmentLengths = new float[MaxSegments];
        // COLD ALLOC: RaycastHit[16] — bend detection hit buffer for this tether instance — owner: TetherInstance
        private readonly RaycastHit[] _bendHitBuffer = new RaycastHit[16];
        // COLD ALLOC: RaycastHit[8] — anti-slice integrity validation buffer for this tether instance — owner: TetherInstance
        private readonly RaycastHit[] _integrityHitBuffer = new RaycastHit[8];
        // COLD ALLOC: HectonVoxelVolume[4] — dynamic voxel owners for active bend points — owner: TetherInstance
        private readonly HectonVoxelVolume[] _bendVolumes = new HectonVoxelVolume[MaxSupportedBendPoints];
        // COLD ALLOC: int[4] — cached runtime stamps for bend-volume invalidation — owner: TetherInstance
        private readonly int[] _bendVolumeRuntimeStamps = new int[MaxSupportedBendPoints];

        private HeavyTowWinch _owner;
        private HectonPlayerMotor _playerMotor;
        private Rigidbody _playerRigidbody;
        private Rigidbody _payloadBody;
        private Collider _payloadCollider;
        private TetherClass _tetherClass;
        private float _restLength;
        private float _currentLength;
        private float _reducedMass;
        private float _dampingCoefficient;
        private float _payloadMass;
        private float _payloadMass01;
        private float _springStiffness;
        private float _maxTowBreakDistance;
        private float _maxCableAcceleration;
        private float _fullTensionExtension;
        private int _maxBendPoints;
        private float _bendPointClearanceRadius;
        private LayerMask _bendObstructionMask;
        private float _bendSurfaceOffset;
        private float _bendEndpointInset;
        private int _visualSegmentCount;
        private float _visualSegmentSmoothSpeed;
        private float _payloadCurrentStrength;
        private float _payloadSideCurrentBoost;
        private float _payloadCurrentVerticalFactor;
        private float _payloadCurrentNoiseScale;
        private float _payloadCurrentTimeScale;
        private float _payloadCurrentDamping;
        private float _maxPayloadCurrentForce;
        private float _payloadAngularDamping;
        private float _maxPayloadAngularSpeed;
        private float _bioCableStressBuildMultiplier;
        private float _bioCablePayloadPullForce;
        private float _bioCableHoldTime;
        private float _bioCableBlendSharpness;
        private int _bendPointCount;
        private bool _segmentRestLengthsDirty;
        private bool _losBlocked;
        private bool _isActive;
        private int _losCheckCooldownFrames;
        private float _stressTimer;
        private float _tension01;
        private float _stress01;
        private float _towDragMultiplier = 1f;
        private float _signedLateralPull01;
        private float _backwardPull01;
        private float _payloadDrift01;
        private int _slicingSegmentIndex = -1;
        private int _slicingConsecutiveFrames;
        private Vector3 _bioCableRequestedAnchorWS;
        private Vector3 _bioCableCurrentAnchorWS;
        private float _bioCableRequestedTension01;
        private float _bioCableCurrentTension01;
        private float _bioCableRequestedCutProgress01 = 1f;
        private float _bioCableCurrentCutProgress01 = 1f;
        private float _bioCableHoldTimer;
        private bool _bioCableRequestedThisStep;
        private Bounds _visualBounds;
        private ITransportPlatform _solverPlatform;
        private Transform _solverPlatformTransform;
        private Matrix4x4 _solverWorldToLocalMatrix = Matrix4x4.identity;
        private Matrix4x4 _solverLocalToWorldMatrix = Matrix4x4.identity;
        private bool _solveInPlatformLocalSpace;
        private bool _kinematicAnchorCompensationEnabled;

        /// <summary>Active owner facade that exposes tether state to the rest of gameplay.</summary>
        public HeavyTowWinch Owner => _owner;

        /// <summary>True while the tether is attached and ready to simulate.</summary>
        public bool IsActive => _isActive;

        /// <summary>Current normalized cable tension.</summary>
        public float CurrentTension01 => _tension01;

        /// <summary>Current normalized accumulated stress timer.</summary>
        public float CurrentStress01 => _stress01;

        /// <summary>Current drag multiplier applied to the player locomotion owner.</summary>
        public float CurrentTowDragMultiplier => _towDragMultiplier;

        /// <summary>Signed lateral pull against the player's right axis.</summary>
        public float CurrentSignedLateralPull01 => _signedLateralPull01;

        /// <summary>Backward pull amount against the player's forward axis.</summary>
        public float CurrentBackwardPull01 => _backwardPull01;

        private NativeArray<float3> _visualSegmentPositions;

        /// <summary>GPU source buffer consumed by the procedural line-strip draw.</summary>
        public GraphicsBuffer VisualSegmentBuffer { get; private set; }

        /// <summary>Current number of visual points owned by the line-strip buffer.</summary>
        public int VisualPointCount => _isActive && _visualSegmentPositions.IsCreated ? _visualSegmentPositions.Length : 0;

        /// <summary>Whether the visual staging and render buffers are ready for use.</summary>
        public bool IsVisualReady => _isActive && _visualSegmentPositions.IsCreated && VisualSegmentBuffer != null && _visualSegmentPositions.Length > 1;

        /// <summary>CPU staging buffer used by the LateUpdate visual upload path.</summary>
        public NativeArray<float3> VisualSegmentPositions => _visualSegmentPositions;

        /// <summary>Active payload rigidbody resolved by this tether.</summary>
        internal Rigidbody PayloadBody => _payloadBody;

        /// <summary>
        /// Configures the tether against a player/payload pair.
        /// </summary>
        public void Configure(
            HeavyTowWinch owner,
            HectonPlayerMotor playerMotor,
            Rigidbody playerRigidbody,
            Rigidbody payloadBody,
            Collider payloadCollider,
            float initialDistance)
        {
            _owner = owner;
            _playerMotor = playerMotor;
            _playerRigidbody = playerRigidbody;
            _payloadBody = payloadBody;
            _payloadCollider = payloadCollider;
            _tetherClass = TetherClass.TowCable;
            _springStiffness = owner != null ? owner.ResolveTowSpringStiffness() : 0f;
            _maxTowBreakDistance = owner != null ? owner.ResolveMaxTowBreakDistance() : 0f;
            _maxCableAcceleration = owner != null ? owner.ResolveMaxCableAcceleration() : 0f;
            _fullTensionExtension = owner != null ? owner.ResolveFullTensionExtension() : 1f;
            _maxBendPoints = owner != null ? owner.ResolveMaxBendPoints() : 0;
            _bendPointClearanceRadius = owner != null ? owner.ResolveBendPointClearanceRadius() : 0.3f;
            _bendObstructionMask = owner != null ? owner.ResolveCableBendObstructionMask() : ~0;
            _bendSurfaceOffset = owner != null ? owner.ResolveBendSurfaceOffset() : 0.12f;
            _bendEndpointInset = owner != null ? owner.ResolveBendEndpointInset() : 0.08f;
            _visualSegmentCount = owner != null ? owner.ResolveVisualSegmentCount() : 16;
            _visualSegmentSmoothSpeed = owner != null ? owner.ResolveVisualSegmentSmoothSpeed() : 12f;
            _payloadCurrentStrength = owner != null ? owner.ResolvePayloadCurrentStrength() : 0f;
            _payloadSideCurrentBoost = owner != null ? owner.ResolvePayloadSideCurrentBoost() : 1f;
            _payloadCurrentVerticalFactor = owner != null ? owner.ResolvePayloadCurrentVerticalFactor() : 0f;
            _payloadCurrentNoiseScale = owner != null ? owner.ResolvePayloadCurrentNoiseScale() : 0f;
            _payloadCurrentTimeScale = owner != null ? owner.ResolvePayloadCurrentTimeScale() : 0f;
            _payloadCurrentDamping = owner != null ? owner.ResolvePayloadCurrentDamping() : 0f;
            _maxPayloadCurrentForce = owner != null ? owner.ResolveMaxPayloadCurrentForce() : 0f;
            _payloadAngularDamping = owner != null ? owner.ResolvePayloadAngularDamping() : 0f;
            _maxPayloadAngularSpeed = owner != null ? owner.ResolveMaxPayloadAngularSpeed() : 0f;
            _bioCableStressBuildMultiplier = owner != null ? owner.ResolveBioCableStressBuildMultiplier() : 0f;
            _bioCablePayloadPullForce = owner != null ? owner.ResolveBioCablePayloadPullForce() : 0f;
            _bioCableHoldTime = owner != null ? owner.ResolveBioCableHoldTime() : 0f;
            _bioCableBlendSharpness = owner != null ? owner.ResolveBioCableBlendSharpness() : 1f;
            _restLength = owner != null ? owner.ResolveTowRestLength(initialDistance) : math.max(1f, initialDistance);
            _visualSegmentCount = Mathf.Clamp(_visualSegmentCount, MinVisualSegmentCount, MaxVisualSegmentCount);
            _visualSegmentSmoothSpeed = math.max(1f, _visualSegmentSmoothSpeed);
            _payloadMass = _payloadBody != null ? _payloadBody.mass : 0f;
            _payloadMass01 = owner != null ? owner.ResolvePayloadMass01(_payloadMass) : 0f;
            _segmentRestLengthsDirty = true;
            _bendPointCount = 0;
            _losBlocked = false;
            _losCheckCooldownFrames = 0;
            _stressTimer = 0f;
            _tension01 = 0f;
            _stress01 = 0f;
            _towDragMultiplier = 1f;
            _signedLateralPull01 = 0f;
            _backwardPull01 = 0f;
            _payloadDrift01 = 0f;
            _slicingSegmentIndex = -1;
            _slicingConsecutiveFrames = 0;
            _bioCableRequestedAnchorWS = Vector3.zero;
            _bioCableCurrentAnchorWS = Vector3.zero;
            _bioCableRequestedTension01 = 0f;
            _bioCableCurrentTension01 = 0f;
            _bioCableRequestedCutProgress01 = 1f;
            _bioCableCurrentCutProgress01 = 1f;
            _bioCableHoldTimer = 0f;
            _bioCableRequestedThisStep = false;
            _solverPlatform = null;
            _solverPlatformTransform = null;
            _solverWorldToLocalMatrix = Matrix4x4.identity;
            _solverLocalToWorldMatrix = Matrix4x4.identity;
            _solveInPlatformLocalSpace = false;
            _kinematicAnchorCompensationEnabled = false;
            ClearBendMetadata(0);
            EnsureVisualBuffers(_visualSegmentCount);
            GlobalPhysicsStateManager.RegisterTetherConnection(this, _playerRigidbody, _payloadBody);
            RefreshKinematicAnchorCompensationState(forceRecalculateDamping: true);
            RecalculateDampingCoefficient();
            _isActive = true;
            _visualBounds = new Bounds(
                owner != null ? owner.ResolveTowAnchorPosition() : Vector3.zero,
                Vector3.one);
        }

        /// <summary>
        /// Queues an external cable-snare force sample for the next fixed-step solve.
        /// </summary>
        public void QueueExternalCableSnare(Vector3 anchorWS, float tension01, float cutProgress01)
        {
            _bioCableRequestedThisStep = true;
            _bioCableRequestedAnchorWS = anchorWS;
            _bioCableRequestedTension01 = math.saturate(tension01);
            _bioCableRequestedCutProgress01 = math.saturate(cutProgress01);
            if (_bioCableRequestedTension01 > 0f)
                _bioCableHoldTimer = _bioCableHoldTime;
        }

        /// <summary>
        /// Returns the current payload sample consumed by abyssal cable-zone logic.
        /// </summary>
        public bool TryGetPayloadSample(out Vector3 payloadPositionWS, out float payloadRadiusWS)
        {
            payloadPositionWS = Vector3.zero;
            payloadRadiusWS = 0f;
            if (!_isActive || _payloadBody == null)
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

        /// <summary>
        /// Executes the fixed-step tether solver.
        /// </summary>
        internal TetherLifecycleState Simulate(float fixedDeltaTime, int activeTetherCount, int maxVisualizedTethers)
        {
            if (!_isActive || _owner == null || _payloadBody == null || _playerRigidbody == null)
                return TetherLifecycleState.Released;

            if (_owner.ShouldSuppressTow || !_owner.IsTowPayloadValid(_payloadBody, _payloadCollider))
                return TetherLifecycleState.Released;

            if (fixedDeltaTime <= 0f)
                return TetherLifecycleState.Alive;

            Vector3 anchorPosition = _owner.ResolveTowAnchorPosition();
            Vector3 payloadPosition = _payloadBody.worldCenterOfMass;
            if (!IsFinite(anchorPosition) || !IsFinite(payloadPosition))
                return TetherLifecycleState.Released;

            if (!Mathf.Approximately(_payloadMass, _payloadBody.mass))
            {
                _payloadMass = _payloadBody.mass;
                _payloadMass01 = _owner.ResolvePayloadMass01(_payloadMass);
                RecalculateDampingCoefficient();
            }

            RefreshKinematicAnchorCompensationState(forceRecalculateDamping: false);

            ResolveSolverReferenceFrame();
            AdvanceExternalCableSnare(fixedDeltaTime);
            Vector3 payloadCurrentForce = ComputePayloadCurrentForce(anchorPosition, payloadPosition);
            ApplyPayloadCurrentForce(payloadCurrentForce, fixedDeltaTime);

            bool allowBendPoints = activeTetherCount <= maxVisualizedTethers && _maxBendPoints > 0;
            UpdateLineOfSight(anchorPosition, payloadPosition, allowBendPoints);

            int anchorCount = BuildAnchorChain(anchorPosition, _payloadBody.worldCenterOfMass);
            if (anchorCount < 2)
            {
                ResetRuntimeLoads();
                _owner.ApplyTowLoad(1f);
                return TetherLifecycleState.Alive;
            }

            if (_currentLength > _maxTowBreakDistance)
                return TetherLifecycleState.Released;

            ApplyHardConstraint(anchorCount);
            anchorCount = BuildAnchorChain(anchorPosition, _payloadBody.worldCenterOfMass);
            if (anchorCount < 2)
            {
                ResetRuntimeLoads();
                _owner.ApplyTowLoad(1f);
                return TetherLifecycleState.Alive;
            }

            float peakTension = ApplySpringForces(anchorCount);
            float bioCablePeakTension = ApplyExternalCableSnareForce();
            if (bioCablePeakTension > peakTension)
                peakTension = bioCablePeakTension;

            UpdateTowDirectionResponse();
            UpdateTowDrag();

            if (UpdateStressAndSnap(peakTension, fixedDeltaTime))
                return TetherLifecycleState.Snapped;

            if (ValidateCableIntegrity(anchorCount, allowBendPoints))
                return TetherLifecycleState.Snapped;

            return TetherLifecycleState.Alive;
        }

        /// <summary>
        /// Updates the visual staging buffer and uploads it to the GPU render buffer.
        /// </summary>
        public void UpdateVisuals(float deltaTime)
        {
            if (!_isActive || !_visualSegmentPositions.IsCreated || VisualSegmentBuffer == null)
                return;

            Vector3 anchorPosition = _owner != null ? _owner.ResolveTowAnchorPosition() : Vector3.zero;
            Vector3 payloadPosition = _payloadBody != null ? _payloadBody.worldCenterOfMass : anchorPosition;
            ResolveSolverReferenceFrame();
            int anchorCount = BuildAnchorChain(anchorPosition, payloadPosition);
            if (anchorCount < 2)
                return;

            float safeDeltaTime = math.max(deltaTime, 0f);
            float blendT = 1f - math.exp(-_visualSegmentSmoothSpeed * safeDeltaTime);
            float pathLength = math.max(_currentLength, MinDistance);
            float step = _visualSegmentPositions.Length > 1
                ? pathLength / (_visualSegmentPositions.Length - 1)
                : pathLength;

            Vector3 minBounds = anchorPosition;
            Vector3 maxBounds = anchorPosition;
            for (int i = 0; i < _visualSegmentPositions.Length; i++)
            {
                float travel = step * i;
                Vector3 targetPoint = SamplePathPoint(anchorCount, travel);
                Vector3 currentPoint = new Vector3(
                    _visualSegmentPositions[i].x,
                    _visualSegmentPositions[i].y,
                    _visualSegmentPositions[i].z);
                Vector3 blendedPoint = i == 0 || i == _visualSegmentPositions.Length - 1
                    ? targetPoint
                    : Vector3.Lerp(currentPoint, targetPoint, blendT);
                _visualSegmentPositions[i] = new float3(blendedPoint.x, blendedPoint.y, blendedPoint.z);
                minBounds = Vector3.Min(minBounds, blendedPoint);
                maxBounds = Vector3.Max(maxBounds, blendedPoint);
            }

            _visualBounds.SetMinMax(minBounds, maxBounds);
            VisualSegmentBuffer.SetData(_visualSegmentPositions);
        }

        /// <summary>
        /// Returns the current visual draw bounds with extra padding.
        /// </summary>
        public Bounds GetVisualBounds(float padding)
        {
            Bounds bounds = _visualBounds;
            bounds.Expand(math.max(0f, padding) * 2f);
            return bounds;
        }

        /// <summary>
        /// Clears state but preserves pooled buffers for reuse.
        /// </summary>
        public void Deactivate()
        {
            GlobalPhysicsStateManager.UnregisterTetherConnection(this);
            _isActive = false;
            _owner = null;
            _playerMotor = null;
            _playerRigidbody = null;
            _payloadBody = null;
            _payloadCollider = null;
            _payloadMass = 0f;
            _payloadMass01 = 0f;
            _restLength = 0f;
            _currentLength = 0f;
            _reducedMass = 0f;
            _dampingCoefficient = 0f;
            _springStiffness = 0f;
            _maxTowBreakDistance = 0f;
            _maxCableAcceleration = 0f;
            _fullTensionExtension = 1f;
            _maxBendPoints = 0;
            _bendPointClearanceRadius = 0f;
            _bendObstructionMask = 0;
            _bendSurfaceOffset = 0f;
            _bendEndpointInset = 0f;
            _visualSegmentCount = 0;
            _visualSegmentSmoothSpeed = 0f;
            _payloadCurrentStrength = 0f;
            _payloadSideCurrentBoost = 0f;
            _payloadCurrentVerticalFactor = 0f;
            _payloadCurrentNoiseScale = 0f;
            _payloadCurrentTimeScale = 0f;
            _payloadCurrentDamping = 0f;
            _maxPayloadCurrentForce = 0f;
            _payloadAngularDamping = 0f;
            _maxPayloadAngularSpeed = 0f;
            _bioCableStressBuildMultiplier = 0f;
            _bioCablePayloadPullForce = 0f;
            _bioCableHoldTime = 0f;
            _bioCableBlendSharpness = 1f;
            _bendPointCount = 0;
            _segmentRestLengthsDirty = false;
            _losBlocked = false;
            _losCheckCooldownFrames = 0;
            _stressTimer = 0f;
            _tension01 = 0f;
            _stress01 = 0f;
            _towDragMultiplier = 1f;
            _signedLateralPull01 = 0f;
            _backwardPull01 = 0f;
            _payloadDrift01 = 0f;
            _slicingSegmentIndex = -1;
            _slicingConsecutiveFrames = 0;
            _bioCableRequestedAnchorWS = Vector3.zero;
            _bioCableCurrentAnchorWS = Vector3.zero;
            _bioCableRequestedTension01 = 0f;
            _bioCableCurrentTension01 = 0f;
            _bioCableRequestedCutProgress01 = 1f;
            _bioCableCurrentCutProgress01 = 1f;
            _bioCableHoldTimer = 0f;
            _bioCableRequestedThisStep = false;
            _visualBounds = new Bounds(Vector3.zero, Vector3.one);
            _solverPlatform = null;
            _solverPlatformTransform = null;
            _solverWorldToLocalMatrix = Matrix4x4.identity;
            _solverLocalToWorldMatrix = Matrix4x4.identity;
            _solveInPlatformLocalSpace = false;
            _kinematicAnchorCompensationEnabled = false;
            ClearBendMetadata(0);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Releases persistent native and GPU resources.
        /// </summary>
        public void DisposeRuntimeResources()
        {
            if (VisualSegmentBuffer != null)
            {
                VisualSegmentBuffer.Release();
                VisualSegmentBuffer = null;
            }

            if (_visualSegmentPositions.IsCreated)
                _visualSegmentPositions.Dispose();
        }

        private void OnDestroy()
        {
            DisposeRuntimeResources();
        }

        private void EnsureVisualBuffers(int pointCount)
        {
            if (pointCount < 2)
                pointCount = 2;

            if (_visualSegmentPositions.IsCreated && _visualSegmentPositions.Length != pointCount)
            {
                _visualSegmentPositions.Dispose();
                _visualSegmentPositions = default;
            }

            if (!_visualSegmentPositions.IsCreated)
            {
                // COLD ALLOC: NativeArray<float3>[pointCount] — persistent visual staging path for tether line rendering — owner: TetherInstance
                _visualSegmentPositions = new NativeArray<float3>(pointCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (VisualSegmentBuffer != null && VisualSegmentBuffer.count != pointCount)
            {
                VisualSegmentBuffer.Release();
                VisualSegmentBuffer = null;
            }

            if (VisualSegmentBuffer == null)
            {
                // COLD ALLOC: GraphicsBuffer[pointCount] — persistent GPU line-strip source for tether visuals — owner: TetherInstance
                VisualSegmentBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pointCount, sizeof(float) * 3);
            }
        }

        private void RecalculateDampingCoefficient()
        {
            float playerMass = _playerRigidbody != null ? math.max(_playerRigidbody.mass, 0.0001f) : 1f;
            float payloadMass = _payloadBody == null || _payloadBody.isKinematic || _kinematicAnchorCompensationEnabled
                ? float.PositiveInfinity
                : math.max(_payloadBody.mass, 0.0001f);

            if (float.IsInfinity(payloadMass))
            {
                _reducedMass = playerMass;
            }
            else
            {
                _reducedMass = (playerMass * payloadMass) / math.max(playerMass + payloadMass, 0.0001f);
            }

            float requestedMultiplier = _owner != null ? _owner.ResolveTowOverDampingMultiplier() : 1f;
            float overDampingMultiplier = _tetherClass == TetherClass.TowCable
                ? math.max(TowCableOverDampingMinimum, requestedMultiplier)
                : math.max(1f, requestedMultiplier);
            float criticalDamping = 2f * math.sqrt(math.max(_springStiffness, 0f) * math.max(_reducedMass, 0f));
            _dampingCoefficient = criticalDamping * overDampingMultiplier;
        }

        private void AdvanceExternalCableSnare(float fixedDeltaTime)
        {
            if (_bioCableRequestedThisStep)
            {
                if (_bioCableRequestedTension01 > 0f)
                    _bioCableHoldTimer = _bioCableHoldTime;
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
            float blendT = 1f - math.exp(-math.max(1f, _bioCableBlendSharpness) * fixedDeltaTime);

            _bioCableCurrentTension01 = math.lerp(_bioCableCurrentTension01, targetTension, blendT);
            _bioCableCurrentCutProgress01 = math.lerp(_bioCableCurrentCutProgress01, targetCutProgress, blendT);
            _bioCableCurrentAnchorWS = Vector3.Lerp(_bioCableCurrentAnchorWS, targetAnchor, blendT);

            _bioCableRequestedTension01 = 0f;
            _bioCableRequestedCutProgress01 = 1f;
            _bioCableRequestedAnchorWS = Vector3.zero;
            _bioCableRequestedThisStep = false;
        }

        private Vector3 ComputePayloadCurrentForce(Vector3 anchorPosition, Vector3 payloadPosition)
        {
            if (_payloadBody == null)
                return Vector3.zero;

            float time = Time.time;
            float3 phantomCurrentSample = CurrentManager.SampleCurrent(
                new float3(payloadPosition.x, payloadPosition.y, payloadPosition.z),
                time,
                _payloadCurrentNoiseScale,
                _payloadCurrentTimeScale,
                _payloadCurrentStrength,
                _payloadCurrentVerticalFactor);
            Vector3 phantomCurrent = new Vector3(phantomCurrentSample.x, phantomCurrentSample.y, phantomCurrentSample.z);
            Vector3 authoredCurrent = CurrentVolume.SampleAt(payloadPosition);
            Vector3 environmentCurrent = phantomCurrent + authoredCurrent;
            environmentCurrent.y *= _payloadCurrentVerticalFactor;

            Vector3 currentDelta = environmentCurrent - _payloadBody.linearVelocity;
            Vector3 playerRight = _owner != null ? _owner.PlayerRight : Vector3.right;
            float sideExposure = 0f;
            if (currentDelta.sqrMagnitude > MinVectorMagnitudeSq)
                sideExposure = math.abs(Vector3.Dot(currentDelta.normalized, playerRight));

            float currentScale = math.lerp(0.55f, 1f, _payloadMass01);
            currentScale *= math.lerp(1f, _payloadSideCurrentBoost, sideExposure);
            Vector3 currentForce = currentDelta * (_payloadCurrentDamping * currentScale);
            float magnitude = currentForce.magnitude;
            if (magnitude > _maxPayloadCurrentForce)
                currentForce *= _maxPayloadCurrentForce / math.max(magnitude, MinDistance);

            _payloadDrift01 = math.saturate(currentDelta.magnitude / math.max(1f, _maxCableAcceleration));
            return currentForce;
        }

        private void ApplyPayloadCurrentForce(Vector3 payloadCurrentForce, float fixedDeltaTime)
        {
            if (_payloadBody == null)
                return;

            if (payloadCurrentForce.sqrMagnitude > MinVectorMagnitudeSq)
                ApplyClampedAcceleration(_payloadBody, payloadCurrentForce, _maxPayloadCurrentForce);

            if (_payloadAngularDamping > 0f)
            {
                Vector3 angularVelocity = _payloadBody.angularVelocity;
                float angularBlend = 1f / (1f + _payloadAngularDamping * fixedDeltaTime);
                angularVelocity *= angularBlend;
                float angularSpeed = angularVelocity.magnitude;
                if (angularSpeed > _maxPayloadAngularSpeed)
                    angularVelocity *= _maxPayloadAngularSpeed / math.max(angularSpeed, MinDistance);

                _payloadBody.angularVelocity = angularVelocity;
            }
        }

        private float ApplyExternalCableSnareForce()
        {
            if (_payloadBody == null || _bioCableCurrentTension01 <= MinDistance)
                return 0f;

            float cutSuppression = 1f - math.saturate(_bioCableCurrentCutProgress01);
            float effectiveTension = _bioCableCurrentTension01 * cutSuppression;
            if (effectiveTension <= MinDistance)
                return 0f;

            Vector3 toAnchor = _bioCableCurrentAnchorWS - _payloadBody.worldCenterOfMass;
            if (toAnchor.sqrMagnitude > MinVectorMagnitudeSq)
            {
                Vector3 snareForce = toAnchor.normalized * (_bioCablePayloadPullForce * effectiveTension);
                ApplyClampedAcceleration(_payloadBody, snareForce, _bioCablePayloadPullForce);
            }

            return _bioCablePayloadPullForce * effectiveTension * math.max(1f, _bioCableStressBuildMultiplier);
        }

        private void UpdateLineOfSight(Vector3 anchorPosition, Vector3 payloadPosition, bool allowBendPoints)
        {
            if (InvalidateBendPointsForDynamicVoxelChange())
                _losCheckCooldownFrames = 0;

            if (!allowBendPoints)
            {
                if (_bendPointCount > 0)
                    _segmentRestLengthsDirty = true;
                _bendPointCount = 0;
                _losBlocked = false;
                _losCheckCooldownFrames = BendRecheckCooldownFrames;
                ClearBendMetadata(0);
                return;
            }

            _losCheckCooldownFrames--;
            if (_losCheckCooldownFrames > 0)
                return;

            bool directBlocked = TryFindClosestObstacle(anchorPosition, payloadPosition, out RaycastHit firstHit);
            if (!directBlocked)
            {
                if (_bendPointCount > 0)
                    _segmentRestLengthsDirty = true;
                _bendPointCount = 0;
                _losBlocked = false;
                _losCheckCooldownFrames = BendRecheckCooldownFrames;
                ClearBendMetadata(0);
                return;
            }

            RecalculateBendPoints(anchorPosition, payloadPosition, firstHit);
            _losBlocked = _bendPointCount > 0;
            _losCheckCooldownFrames = BendRecheckCooldownFrames;
        }

        private void RecalculateBendPoints(Vector3 anchorPosition, Vector3 payloadPosition, RaycastHit firstHit)
        {
            int previousCount = _bendPointCount;
            _bendPointCount = 0;
            Vector3 origin = anchorPosition;
            Vector3 target = payloadPosition;
            RaycastHit initialHit = firstHit;

            for (int bendIndex = 0; bendIndex < _maxBendPoints && bendIndex < MaxSupportedBendPoints; bendIndex++)
            {
                RaycastHit hit;
                if (bendIndex == 0)
                {
                    hit = initialHit;
                    if (hit.collider == null)
                        break;
                }
                else if (!TryFindClosestObstacle(origin, target, out hit))
                {
                    break;
                }

                if (!TryResolveBendCorner(
                        hit,
                        target - origin,
                        out Vector3 bendPoint,
                        out Vector3 bendNormal,
                        out HectonVoxelVolume bendVolume,
                        out int bendRuntimeStamp))
                    break;

                if (_bendPointCount > 0)
                {
                    float minSpacingSq = _bendPointClearanceRadius * _bendPointClearanceRadius;
                    if ((bendPoint - _bendPoints[_bendPointCount - 1]).sqrMagnitude <= minSpacingSq)
                        break;
                }

                _bendPoints[_bendPointCount] = bendPoint;
                _bendNormals[_bendPointCount] = bendNormal;
                _bendVolumes[_bendPointCount] = bendVolume;
                _bendVolumeRuntimeStamps[_bendPointCount] = bendRuntimeStamp;
                _bendPointCount++;
                origin = bendPoint + (target - origin).normalized * _bendPointClearanceRadius;
            }

            if (previousCount != _bendPointCount)
                _segmentRestLengthsDirty = true;

            ClearBendMetadata(_bendPointCount);
        }

        private bool TryResolveBendCorner(
            RaycastHit hit,
            Vector3 lineDirection,
            out Vector3 bendPoint,
            out Vector3 bendNormal,
            out HectonVoxelVolume bendVolume,
            out int bendRuntimeStamp)
        {
            bendPoint = Vector3.zero;
            bendNormal = Vector3.up;
            bendVolume = null;
            bendRuntimeStamp = 0;

            if (hit.collider == null)
                return false;

            bendNormal = hit.normal.sqrMagnitude > MinVectorMagnitudeSq ? hit.normal.normalized : Vector3.up;
            HectonVoxelVolume voxelVolume = null;
            if (!hit.collider.TryGetComponent(out voxelVolume))
                voxelVolume = hit.collider.GetComponentInParent<HectonVoxelVolume>();

            if (voxelVolume != null && voxelVolume.TryResolveNearestVoxelCorner(hit.point, bendNormal, out Vector3 cornerWorld))
            {
                bendPoint = cornerWorld + bendNormal * math.max(0.01f, _bendSurfaceOffset);
                bendVolume = voxelVolume;
                bendRuntimeStamp = voxelVolume.RuntimeStamp;
                return IsFinite(bendPoint);
            }

            Vector3 tangent = Vector3.ProjectOnPlane(lineDirection, bendNormal);
            if (tangent.sqrMagnitude > MinVectorMagnitudeSq)
            {
                tangent.Normalize();
            }
            else
            {
                tangent = Vector3.Cross(bendNormal, Vector3.up);
                if (tangent.sqrMagnitude <= MinVectorMagnitudeSq)
                    tangent = Vector3.Cross(bendNormal, Vector3.right);
                tangent.Normalize();
            }

            bendPoint = hit.point + bendNormal * math.max(0.01f, _bendSurfaceOffset) + tangent * math.max(0.01f, _bendPointClearanceRadius);
            return IsFinite(bendPoint);
        }

        private bool TryFindClosestObstacle(Vector3 start, Vector3 end, out RaycastHit bestHit)
        {
            bestHit = default;
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance <= MinDistance)
                return false;

            Vector3 direction = delta / distance;
            float endpointInset = math.clamp(_bendEndpointInset, 0.005f, distance * 0.45f);
            float castDistance = distance - endpointInset * 2f;
            if (castDistance <= MinDistance)
                return false;

            Vector3 origin = start + direction * endpointInset;
            int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                origin,
                direction,
                _bendHitBuffer,
                castDistance,
                _bendObstructionMask,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
                return false;

            float closestDistance = float.PositiveInfinity;
            bool foundHit = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = _bendHitBuffer[i];
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

            return foundHit;
        }

        private int BuildAnchorChain(Vector3 anchorPosition, Vector3 payloadPosition)
        {
            _anchorPositions[0] = anchorPosition;
            _anchorVelocities[0] = _playerRigidbody != null ? _playerRigidbody.linearVelocity : Vector3.zero;
            int anchorCount = 1;

            for (int i = 0; i < _bendPointCount; i++)
            {
                _anchorPositions[anchorCount] = _bendPoints[i];
                _anchorVelocities[anchorCount] = Vector3.zero;
                anchorCount++;
            }

            _anchorPositions[anchorCount] = payloadPosition;
            _anchorVelocities[anchorCount] = _payloadBody != null ? _payloadBody.linearVelocity : Vector3.zero;
            anchorCount++;

            PopulateSolverAnchors(anchorCount);

            float totalLength = 0f;
            int segmentCount = anchorCount - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                float segmentLength = Vector3.Distance(_solverAnchorPositions[i], _solverAnchorPositions[i + 1]);
                _segmentLengths[i] = segmentLength;
                totalLength += segmentLength;
            }

            _currentLength = totalLength;
            if (_segmentRestLengthsDirty)
                RecalculateSegmentRestLengths(segmentCount, totalLength);

            return anchorCount;
        }

        private void RecalculateSegmentRestLengths(int segmentCount, float totalLength)
        {
            if (segmentCount <= 0)
                return;

            if (totalLength <= MinDistance)
            {
                float uniformLength = _restLength / segmentCount;
                for (int i = 0; i < segmentCount; i++)
                    _segmentRestLengths[i] = uniformLength;
                _segmentRestLengthsDirty = false;
                return;
            }

            for (int i = 0; i < segmentCount; i++)
            {
                float fraction = _segmentLengths[i] / totalLength;
                _segmentRestLengths[i] = _restLength * fraction;
            }

            _segmentRestLengthsDirty = false;
        }

        private void ApplyHardConstraint(int anchorCount)
        {
            int segmentCount = anchorCount - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                Vector3 start = _anchorPositions[i];
                Vector3 end = _anchorPositions[i + 1];
                Vector3 solverStart = _solverAnchorPositions[i];
                Vector3 solverEnd = _solverAnchorPositions[i + 1];
                Vector3 delta = solverEnd - solverStart;
                float currentDistance = delta.magnitude;
                if (currentDistance <= MinDistance)
                    continue;

                float hardLimit = _segmentRestLengths[i] * NonElasticLimitRatio;
                float overExtension = currentDistance - hardLimit;
                if (overExtension <= 0f)
                    continue;

                Vector3 solverDirection = delta / currentDistance;
                Vector3 worldDirection = ResolveSolverDirectionToWorld(solverDirection);
                bool startDynamic = i == 0;
                bool endDynamic = i == segmentCount - 1 && !_kinematicAnchorCompensationEnabled;
                float startInvMass = startDynamic && _playerRigidbody != null
                    ? 1f / math.max(_playerRigidbody.mass, 0.0001f)
                    : 0f;
                float endInvMass = endDynamic && _payloadBody != null && !_payloadBody.isKinematic
                    ? 1f / math.max(_payloadBody.mass, 0.0001f)
                    : 0f;
                float totalInvMass = startInvMass + endInvMass;
                if (totalInvMass <= 0f)
                    continue;

                if (startDynamic)
                {
                    Vector3 correction = worldDirection * (overExtension * (startInvMass / totalInvMass));
                    MovePlayerPosition(_playerRigidbody.position + correction);
                }

                if (endDynamic)
                {
                    Vector3 correction = worldDirection * (-overExtension * (endInvMass / totalInvMass));
                    _payloadBody.MovePosition(_payloadBody.position + correction);
                }

                Vector3 relativeVelocity = _solverAnchorVelocities[i + 1] - _solverAnchorVelocities[i];
                float relVelAlongCable = Vector3.Dot(relativeVelocity, solverDirection);
                if (relVelAlongCable <= 0f)
                    continue;

                Vector3 velocityCorrection = worldDirection * relVelAlongCable;
                if (startDynamic)
                    ApplyPlayerVelocityChange(velocityCorrection * (startInvMass / totalInvMass));
                if (endDynamic)
                    PhysicsForceRouter.QueueForce(_payloadBody, -velocityCorrection * (endInvMass / totalInvMass), ForceMode.VelocityChange);
            }
        }

        private float ApplySpringForces(int anchorCount)
        {
            int segmentCount = anchorCount - 1;
            Vector3 playerAcceleration = Vector3.zero;
            Vector3 payloadAcceleration = Vector3.zero;
            float peakTension = 0f;

            for (int i = 0; i < segmentCount; i++)
            {
                Vector3 solverStart = _solverAnchorPositions[i];
                Vector3 solverEnd = _solverAnchorPositions[i + 1];
                Vector3 delta = solverEnd - solverStart;
                float currentDistance = delta.magnitude;
                if (currentDistance <= MinDistance)
                    continue;

                Vector3 solverDirection = delta / currentDistance;
                float extension = currentDistance - _segmentRestLengths[i];
                if (extension <= 0f)
                    continue;

                Vector3 relativeVelocity = _solverAnchorVelocities[i + 1] - _solverAnchorVelocities[i];
                float relVelAlongCable = Vector3.Dot(relativeVelocity, solverDirection);
                float springForce = _springStiffness * extension;
                float dampingForce = _dampingCoefficient * relVelAlongCable;
                float tension = springForce + dampingForce;
                if (tension <= 0f)
                    continue;

                if (tension > peakTension)
                    peakTension = tension;

                Vector3 worldDirection = ResolveSolverDirectionToWorld(solverDirection);
                if (i == 0)
                    playerAcceleration += worldDirection * tension;

                if (i == segmentCount - 1 && !_kinematicAnchorCompensationEnabled)
                    payloadAcceleration += -worldDirection * tension;
            }

            ApplyPlayerAcceleration(playerAcceleration);
            ApplyClampedAcceleration(_payloadBody, payloadAcceleration, _maxCableAcceleration);

            float extensionTotal = math.max(0f, _currentLength - _restLength);
            _tension01 = math.saturate(extensionTotal / math.max(_fullTensionExtension, 0.01f));
            return peakTension;
        }

        private void UpdateTowDirectionResponse()
        {
            if (_owner == null)
                return;

            Vector3 lineDirection;
            if (_bendPointCount > 0)
            {
                Vector3 toFirstBend = _bendPoints[0] - _owner.ResolveTowAnchorPosition();
                lineDirection = toFirstBend.sqrMagnitude > MinVectorMagnitudeSq ? toFirstBend.normalized : Vector3.zero;
            }
            else if (_payloadBody != null)
            {
                Vector3 direct = _payloadBody.worldCenterOfMass - _owner.ResolveTowAnchorPosition();
                lineDirection = direct.sqrMagnitude > MinVectorMagnitudeSq ? direct.normalized : Vector3.zero;
            }
            else
            {
                lineDirection = Vector3.zero;
            }

            _signedLateralPull01 = math.clamp(Vector3.Dot(lineDirection, _owner.PlayerRight), -1f, 1f);
            _backwardPull01 = math.saturate(-Vector3.Dot(lineDirection, _owner.PlayerForward));
        }

        private void UpdateTowDrag()
        {
            if (_owner == null)
                return;

            float load01 = math.saturate(math.max(_tension01, _payloadDrift01 * 0.72f) * math.lerp(0.45f, 1f, _payloadMass01));
            _towDragMultiplier = _owner.ResolveTowDragMultiplier(load01);
            _owner.ApplyTowLoad(_towDragMultiplier);
        }

        private bool UpdateStressAndSnap(float peakTension, float fixedDeltaTime)
        {
            float snapThreshold = math.max(1f, _owner != null ? _owner.ResolveSnapTensionThreshold() : 1f);
            float snapDuration = math.max(0.1f, _owner != null ? _owner.ResolveSnapStressDuration() : 0.1f);

            if (peakTension > snapThreshold)
            {
                _stressTimer += fixedDeltaTime;
            }
            else
            {
                _stressTimer = math.max(0f, _stressTimer - (fixedDeltaTime * 0.5f));
            }

            _stress01 = math.saturate(_stressTimer / snapDuration);
            if (_stressTimer < snapDuration)
                return false;

            Vector3 playerSegmentDirection = _bendPointCount > 0
                ? (_bendPoints[0] - _owner.ResolveTowAnchorPosition()).normalized
                : (_payloadBody.worldCenterOfMass - _owner.ResolveTowAnchorPosition()).normalized;
            Vector3 payloadSegmentDirection = _bendPointCount > 0
                ? (_bendPoints[_bendPointCount - 1] - _payloadBody.worldCenterOfMass).normalized
                : (_owner.ResolveTowAnchorPosition() - _payloadBody.worldCenterOfMass).normalized;
            float snapSeverity = math.saturate(peakTension / snapThreshold);
            _owner.HandleTetherSnap(playerSegmentDirection, payloadSegmentDirection, snapSeverity, false, _payloadBody, _payloadCollider);
            return true;
        }

        private bool ValidateCableIntegrity(int anchorCount, bool allowBendPoints)
        {
            if (!allowBendPoints || anchorCount < 2)
            {
                _slicingSegmentIndex = -1;
                _slicingConsecutiveFrames = 0;
                return false;
            }

            int segmentCount = anchorCount - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                Vector3 start = _anchorPositions[i];
                Vector3 end = _anchorPositions[i + 1];
                Vector3 delta = end - start;
                float distance = delta.magnitude;
                if (distance <= MinDistance)
                    continue;

                float segmentInset = math.min(math.max(0.01f, _bendEndpointInset), distance * 0.25f);
                float castDistance = distance - segmentInset * 2f;
                if (castDistance <= MinDistance)
                    continue;

                Vector3 direction = delta / distance;
                int hits = UnityEngine.Physics.RaycastNonAlloc(
                    start + direction * segmentInset,
                    direction,
                    _integrityHitBuffer,
                    castDistance,
                    _bendObstructionMask,
                    QueryTriggerInteraction.Ignore);
                if (hits <= 0)
                    continue;

                bool foundBlockingHit = false;
                for (int hitIndex = 0; hitIndex < hits; hitIndex++)
                {
                    RaycastHit candidate = _integrityHitBuffer[hitIndex];
                    Collider collider = candidate.collider;
                    if (collider == null)
                        continue;

                    if (ReferenceEquals(collider, _payloadCollider))
                        continue;

                    Rigidbody attachedBody = collider.attachedRigidbody;
                    if (attachedBody == _payloadBody || attachedBody == _playerRigidbody)
                        continue;

                    foundBlockingHit = true;
                    break;
                }

                if (!foundBlockingHit)
                    continue;

                _losCheckCooldownFrames = 0;
                if (_slicingSegmentIndex == i)
                {
                    _slicingConsecutiveFrames++;
                }
                else
                {
                    _slicingSegmentIndex = i;
                    _slicingConsecutiveFrames = 1;
                }

                if (_slicingConsecutiveFrames < 3)
                    return false;

                Vector3 playerSegmentDirection = _bendPointCount > 0
                    ? (_bendPoints[0] - _owner.ResolveTowAnchorPosition()).normalized
                    : (_payloadBody.worldCenterOfMass - _owner.ResolveTowAnchorPosition()).normalized;
                Vector3 payloadSegmentDirection = _bendPointCount > 0
                    ? (_bendPoints[_bendPointCount - 1] - _payloadBody.worldCenterOfMass).normalized
                    : (_owner.ResolveTowAnchorPosition() - _payloadBody.worldCenterOfMass).normalized;
                _owner.HandleTetherSnap(playerSegmentDirection, payloadSegmentDirection, 0f, true, _payloadBody, _payloadCollider);
                return true;
            }

            _slicingSegmentIndex = -1;
            _slicingConsecutiveFrames = 0;
            return false;
        }

        private Vector3 SamplePathPoint(int anchorCount, float travelDistance)
        {
            int segmentCount = anchorCount - 1;
            if (segmentCount <= 0)
                return _owner != null ? _owner.ResolveTowAnchorPosition() : Vector3.zero;

            float remaining = math.clamp(travelDistance, 0f, _currentLength);
            for (int i = 0; i < segmentCount; i++)
            {
                float segmentLength = math.max(_segmentLengths[i], MinDistance);
                if (remaining > segmentLength && i < segmentCount - 1)
                {
                    remaining -= segmentLength;
                    continue;
                }

                float segmentT = math.saturate(remaining / segmentLength);
                Vector3 start = _anchorPositions[i];
                Vector3 end = _anchorPositions[i + 1];
                Vector3 basePoint = Vector3.Lerp(start, end, segmentT);
                float sag = segmentLength * VisualSagScale;
                float sagWeight = 4f * segmentT * (1f - segmentT);
                Vector3 sagOffset = Vector3.down * (sag * sagWeight);
                return basePoint + sagOffset;
            }

            return _anchorPositions[anchorCount - 1];
        }

        private void ResetRuntimeLoads()
        {
            _tension01 = 0f;
            _stress01 = 0f;
            _towDragMultiplier = 1f;
            _signedLateralPull01 = 0f;
            _backwardPull01 = 0f;
            _payloadDrift01 = 0f;
        }

        private void MovePlayerPosition(Vector3 position)
        {
            if (_playerMotor != null)
            {
                _playerMotor.MovePosition(position);
                return;
            }

            if (_playerRigidbody != null)
                _playerRigidbody.MovePosition(position);
        }

        private void ApplyPlayerAcceleration(Vector3 acceleration)
        {
            if (_playerMotor != null)
            {
                _playerMotor.ApplyAcceleration(ClampVector(acceleration, _maxCableAcceleration));
                return;
            }

            ApplyClampedAcceleration(_playerRigidbody, acceleration, _maxCableAcceleration);
        }

        private void ApplyPlayerVelocityChange(Vector3 velocityChange)
        {
            if (_playerMotor != null)
            {
                _playerMotor.ApplyVelocityChange(velocityChange);
                return;
            }

            if (_playerRigidbody != null && velocityChange.sqrMagnitude > MinVectorMagnitudeSq)
                PhysicsForceRouter.QueueForce(_playerRigidbody, velocityChange, ForceMode.VelocityChange);
        }

        private static void ApplyClampedAcceleration(Rigidbody body, Vector3 acceleration, float maxAcceleration)
        {
            if (body == null)
                return;

            Vector3 clamped = ClampVector(acceleration, maxAcceleration);
            if (clamped.sqrMagnitude <= MinVectorMagnitudeSq)
                return;

            PhysicsForceRouter.QueueForce(body, clamped, ForceMode.Acceleration);
        }

        internal void RebaseManagedRuntimeState(Vector3 shiftOffset)
        {
            if (!_isActive || shiftOffset.sqrMagnitude <= MinVectorMagnitudeSq)
                return;

            for (int i = 0; i < _bendPointCount; i++)
                _bendPoints[i] -= shiftOffset;

            for (int i = 0; i < _anchorPositions.Length; i++)
                _anchorPositions[i] -= shiftOffset;

            if (!_solveInPlatformLocalSpace)
            {
                for (int i = 0; i < _solverAnchorPositions.Length; i++)
                    _solverAnchorPositions[i] -= shiftOffset;
            }

            _bioCableRequestedAnchorWS -= shiftOffset;
            _bioCableCurrentAnchorWS -= shiftOffset;
            _visualBounds.SetMinMax(_visualBounds.min - shiftOffset, _visualBounds.max - shiftOffset);
        }

        internal void CommitVisualRebaseUpload()
        {
            if (_visualSegmentPositions.IsCreated && VisualSegmentBuffer != null)
                VisualSegmentBuffer.SetData(_visualSegmentPositions);
        }

        internal void RetargetAnchorEndpoint(HectonPlayerMotor playerMotor, Rigidbody anchorBody)
        {
            if (anchorBody == null)
                return;

            _playerMotor = playerMotor;
            _playerRigidbody = anchorBody;
            GlobalPhysicsStateManager.RegisterTetherConnection(this, _playerRigidbody, _payloadBody);
            RefreshKinematicAnchorCompensationState(forceRecalculateDamping: true);
            RecalculateDampingCoefficient();
        }

        internal bool TryGetPayloadBody(out Rigidbody payloadBody)
        {
            payloadBody = _payloadBody;
            return payloadBody != null;
        }

        private void ResolveSolverReferenceFrame()
        {
            _solverPlatform = null;
            _solverPlatformTransform = null;
            _solverWorldToLocalMatrix = Matrix4x4.identity;
            _solverLocalToWorldMatrix = Matrix4x4.identity;
            _solveInPlatformLocalSpace = false;

            if (_owner == null || _payloadBody == null)
                return;

            if (!_owner.TryResolveSharedTransportPlatform(
                    _payloadBody.transform,
                    _payloadCollider,
                    out ITransportPlatform platform,
                    out Matrix4x4 worldToLocalMatrix,
                    out Matrix4x4 localToWorldMatrix))
                return;

            _solverPlatform = platform;
            _solverPlatformTransform = platform.PlatformTransform;
            _solverWorldToLocalMatrix = worldToLocalMatrix;
            _solverLocalToWorldMatrix = localToWorldMatrix;
            _solveInPlatformLocalSpace = true;
        }

        private void PopulateSolverAnchors(int anchorCount)
        {
            if (_solveInPlatformLocalSpace && _solverPlatform != null && _solverPlatformTransform != null)
            {
                for (int i = 0; i < anchorCount; i++)
                {
                    Vector3 worldAnchor = _anchorPositions[i];
                    _solverAnchorPositions[i] = _solverWorldToLocalMatrix.MultiplyPoint3x4(worldAnchor);

                    if (i == 0 || i == anchorCount - 1)
                    {
                        Vector3 platformVelocity = _solverPlatform.GetPlatformPointVelocity(worldAnchor);
                        Vector3 relativeVelocity = _anchorVelocities[i] - platformVelocity;
                        _solverAnchorVelocities[i] = _solverWorldToLocalMatrix.MultiplyVector(relativeVelocity);
                    }
                    else
                    {
                        _solverAnchorVelocities[i] = Vector3.zero;
                    }
                }

                return;
            }

            for (int i = 0; i < anchorCount; i++)
            {
                _solverAnchorPositions[i] = _anchorPositions[i];
                _solverAnchorVelocities[i] = _anchorVelocities[i];
            }
        }

        private Vector3 ResolveSolverDirectionToWorld(Vector3 solverDirection)
        {
            if (!_solveInPlatformLocalSpace)
                return solverDirection;

            Vector3 worldDirection = _solverLocalToWorldMatrix.MultiplyVector(solverDirection);
            if (worldDirection.sqrMagnitude <= MinVectorMagnitudeSq)
                return Vector3.zero;

            return worldDirection.normalized;
        }

        private bool InvalidateBendPointsForDynamicVoxelChange()
        {
            for (int i = 0; i < _bendPointCount; i++)
            {
                HectonVoxelVolume bendVolume = _bendVolumes[i];
                if (bendVolume != null && bendVolume.MatchesRuntimeStamp(_bendVolumeRuntimeStamps[i]))
                    continue;

                if (bendVolume == null && _bendVolumeRuntimeStamps[i] == 0)
                    continue;

                _bendPointCount = 0;
                _losBlocked = false;
                _segmentRestLengthsDirty = true;
                ClearBendMetadata(0);
                return true;
            }

            return false;
        }

        private void ClearBendMetadata(int startIndex)
        {
            for (int i = math.max(0, startIndex); i < MaxSupportedBendPoints; i++)
            {
                _bendVolumes[i] = null;
                _bendVolumeRuntimeStamps[i] = 0;
            }
        }

        private static Vector3 ClampVector(Vector3 value, float maxMagnitude)
        {
            if (!IsFinite(value))
                return Vector3.zero;

            float sqrMagnitude = value.sqrMagnitude;
            if (sqrMagnitude <= MinVectorMagnitudeSq || maxMagnitude <= 0f)
                return sqrMagnitude <= MinVectorMagnitudeSq ? Vector3.zero : value;

            float maxMagnitudeSq = maxMagnitude * maxMagnitude;
            if (sqrMagnitude <= maxMagnitudeSq)
                return value;

            return value.normalized * maxMagnitude;
        }

        private static bool IsFinite(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3));
        }

        private void RefreshKinematicAnchorCompensationState(bool forceRecalculateDamping)
        {
            bool nextState = GlobalPhysicsStateManager.IsKinematicAnchorCompensationEnabled(this, PhysicsConnectionKind.Tether);
            if (!forceRecalculateDamping && nextState == _kinematicAnchorCompensationEnabled)
                return;

            _kinematicAnchorCompensationEnabled = nextState;
            if (_playerRigidbody != null && _payloadBody != null)
                RecalculateDampingCoefficient();
        }
    }
}
