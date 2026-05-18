using System.IO;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
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
        private const int MinVisualSegmentCount = 8;
        private const int MaxVisualSegmentCount = 24;
        private const float VisualSagScale = 0.05f;
        private const int VerletLowIterationCount = 3;
        private const int VerletMidIterationCount = 5;
        private const int VerletHighIterationCount = 8;
        private const int VerletUltraIterationCount = 10;
        private const int VerletLowSegmentCount = 3;
        private const int VerletDefaultSegmentCount = 10;
        private const float VerletReelSpeedMetersPerSecond = 18f;
        private const float VerletMinSpooledSegmentLength = 0.08f;
        private const float VerletRockFriction01 = 0.58f;
        private const float VerletPlasticStretch01 = 0.18f;
        private const float VerletPlasticCreep01 = 0.035f;
        private const float VerletLowVelocityDamping = 0.965f;
        private const float VerletMidVelocityDamping = 0.975f;
        private const float VerletHighVelocityDamping = 0.985f;
        private const int VerletTelemetryCapacity = 300;
        private const int DataVaultMaxTetherSlots = 64;
        private const int DataVaultCableSegmentCount = 10;
        private const int DataVaultCablePointCount = DataVaultCableSegmentCount + 1;
        private const int DataVaultCablePointCapacity = DataVaultMaxTetherSlots * DataVaultCablePointCount;
        private const int DataVaultCableSegmentCapacity = DataVaultMaxTetherSlots * DataVaultCableSegmentCount;
        // Vault telemetry slab: 64 tethers * 300 frames * 64 bytes = 1,228,800 bytes.
        // This deliberately matches the 50-cable SHINOBU target plus pool headroom.
        private const int DataVaultTelemetryCapacity = DataVaultMaxTetherSlots * VerletTelemetryCapacity;
        private const int DataVaultTelemetryHeadCapacity = DataVaultMaxTetherSlots;
        private const int DataVaultScratchNodeCapacity = DataVaultMaxTetherSlots * DataVaultCablePointCount;
        private const int DataVaultScratchSegmentCapacity = DataVaultMaxTetherSlots * DataVaultCableSegmentCount;
        private const int DataVaultVisualAnchorCapacity = DataVaultMaxTetherSlots * MaxAnchors;
        private const int DataVaultVisualSegmentLengthCapacity = DataVaultMaxTetherSlots * MaxSegments;
        private const int DataVaultScratchScalarCapacity = DataVaultMaxTetherSlots;
        private const float VerletFloorY = -5000f;
        private const float VerletNodeRadius = 0.035f;
        private const float MaxCableVelocity = 24f;
        private const float LowTierTautLineVisualThreshold01 = 0.86f;
        private const float ReactiveVfxThreshold01 = 0.9f;
        private const float TensionCreakSafeMargin01 = 0.68f;
        private const int TensionCreakCooldownFrames = 12;
        private const int TowLoadLimitCommandCooldownFrames = 3;
        private const uint TetherCreakMaterialHash = 0x54455448u;
        private const uint TetherSnapImpactMaterialHash = 0x54534E50u;
        private const int DataVaultFlagPositions = 1 << 0;
        private const int DataVaultFlagPreviousPositions = 1 << 1;
        private const int DataVaultFlagVelocities = 1 << 2;
        private const int DataVaultFlagMasses = 1 << 3;
        private const int DataVaultFlagSegmentTensions = 1 << 4;
        private const int DataVaultFlagTelemetryRing = 1 << 5;
        private const int DataVaultFlagTelemetryHead = 1 << 6;
        private const int DataVaultFlagVisualSegmentPositions = 1 << 7;
        private const int DataVaultFlagVisualAnchorPositions = 1 << 8;
        private const int DataVaultFlagVisualSegmentLengths = 1 << 9;
        private const int DataVaultFlagVerletPositions = 1 << 10;
        private const int DataVaultFlagVerletPreviousPositions = 1 << 11;
        private const int DataVaultFlagVerletVelocities = 1 << 12;
        private const int DataVaultFlagVerletPinnedPositions = 1 << 13;
        private const int DataVaultFlagVerletPinnedMask = 1 << 14;
        private const int DataVaultFlagVerletSegmentRestLengths = 1 << 15;
        private const int DataVaultFlagVerletSegmentTensions = 1 << 16;
        private const int DataVaultFlagVerletCorrections = 1 << 17;
        private const int DataVaultFlagVerletCorrectionWeights = 1 << 18;
        private const int DataVaultFlagVerletSolverStats = 1 << 19;
        private const int DataVaultFlagVerletSolverFlags = 1 << 20;
        private const int DataVaultFlagVerletNodeFaultFlags = 1 << 21;
        private const int DataVaultFlagVisualGpuSplinePoints = 1 << 22;
        private const int DataVaultFlagVerletTensionForces = 1 << 23;
        private const int DataVaultFlagVerletTuning = 1 << 24;
        private const string TetherTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_VERLET_CABLES.bin";
        private const string TetherTelemetryH8DumpRelativePath = "Docs/AgentLogs/Dump_VERLET_CABLES.h8dump";
        private const ulong TetherTelemetryDumpMagic = 0x00384E4F54434548ul;
        private const int TetherTelemetryDumpEntrySize = 64;

        // COLD ALLOC: bool[64] — DataVault slot reservations for concurrent tether instances — owner: TetherInstance
        private static readonly bool[] _dataVaultSlotReservations = new bool[DataVaultMaxTetherSlots];

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
        // COLD ALLOC: HectonVoxelVolume[4] — dynamic voxel owners for active bend points — owner: TetherInstance
        private readonly HectonVoxelVolume[] _bendVolumes = new HectonVoxelVolume[MaxSupportedBendPoints];
        // COLD ALLOC: int[4] — cached runtime stamps for bend-volume invalidation — owner: TetherInstance
        private readonly int[] _bendVolumeRuntimeStamps = new int[MaxSupportedBendPoints];

        private TetherManager _manager;
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
        private float _primaryConstraintForceMagnitude;

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

        /// <summary>Cheap visual stress scalar for the procedural tether material.</summary>
        public float VisualStress01 => ResolveVisualStress01(_tension01, _stress01);

        private static float ResolveVisualStress01(float tension01, float stress01)
        {
            float tension = math.isfinite(tension01) ? math.saturate(tension01) : 0f;
            float stress = math.isfinite(stress01) ? math.saturate(stress01) : 0f;
            return math.saturate(math.max(tension, stress));
        }

        private NativeArray<float3> _visualSegmentPositions;
        private NativeArray<GpuCableSplinePointDTO> _visualSegmentGpuPoints;
        private NativeArray<float3> _visualAnchorPositions;
        private NativeArray<float> _visualSegmentLengths;
        private NativeArray<float3> _verletPositions;
        private NativeArray<float3> _verletPreviousPositions;
        private NativeArray<float3> _verletVelocities;
        private NativeArray<float3> _verletPinnedPositions;
        private NativeArray<byte> _verletPinnedMask;
        private NativeArray<float> _verletSegmentRestLengths;
        private NativeArray<float> _verletSegmentTensions;
        private NativeArray<float3> _verletCorrections;
        private NativeArray<float> _verletCorrectionWeights;
        private NativeArray<float> _verletSolverStats;
        private NativeArray<int> _verletSolverFlags;
        private NativeArray<byte> _verletNodeFaultFlags;
        private NativeArray<TetherVerletTelemetryEntry> _verletTelemetryRing;
        private NativeArray<int> _verletTelemetryHead;
        private NativeArray<CableTensionForceDTO> _verletTensionForces;
        private NativeArray<VerletCableTuningDTO> _verletTuning;
        private NativeArray<float3> _dataVaultCablePositions;
        private NativeArray<float3> _dataVaultCablePreviousPositions;
        private NativeArray<float3> _dataVaultCableVelocities;
        private NativeArray<float> _dataVaultCableMasses;
        private NativeArray<float> _dataVaultCableSegmentTensions;
        private VaultBufferHandle<float3> _visualSegmentPositionsHandle;
        private VaultBufferHandle<GpuCableSplinePointDTO> _visualSegmentGpuPointsHandle;
        private VaultBufferHandle<float3> _visualAnchorPositionsHandle;
        private VaultBufferHandle<float> _visualSegmentLengthsHandle;
        private VaultBufferHandle<float3> _verletPositionsHandle;
        private VaultBufferHandle<float3> _verletPreviousPositionsHandle;
        private VaultBufferHandle<float3> _verletVelocitiesHandle;
        private VaultBufferHandle<float3> _verletPinnedPositionsHandle;
        private VaultBufferHandle<byte> _verletPinnedMaskHandle;
        private VaultBufferHandle<float> _verletSegmentRestLengthsHandle;
        private VaultBufferHandle<float> _verletSegmentTensionsHandle;
        private VaultBufferHandle<float3> _verletCorrectionsHandle;
        private VaultBufferHandle<float> _verletCorrectionWeightsHandle;
        private VaultBufferHandle<float> _verletSolverStatsHandle;
        private VaultBufferHandle<int> _verletSolverFlagsHandle;
        private VaultBufferHandle<byte> _verletNodeFaultFlagsHandle;
        private VaultBufferHandle<TetherVerletTelemetryEntry> _verletTelemetryRingHandle;
        private VaultBufferHandle<int> _verletTelemetryHeadHandle;
        private VaultBufferHandle<CableTensionForceDTO> _verletTensionForcesHandle;
        private VaultBufferHandle<VerletCableTuningDTO> _verletTuningHandle;
        private VaultBufferHandle<float3> _dataVaultCablePositionsHandle;
        private VaultBufferHandle<float3> _dataVaultCablePreviousPositionsHandle;
        private VaultBufferHandle<float3> _dataVaultCableVelocitiesHandle;
        private VaultBufferHandle<float> _dataVaultCableMassesHandle;
        private VaultBufferHandle<float> _dataVaultCableSegmentTensionsHandle;
        private IDataVault _dataVault;
        private int _dataVaultSlot = -1;
        private int _dataVaultNativeStateMask;
        private uint _dataVaultResolvedGeneration;
        private bool _dataVaultCableStateReady;
        private bool _verletRuntimeInitialized;
        private int _verletNodeCount;
        private float3 _verletSolverOrigin;
        private bool _visualCulledThisFrame;
        private int _lastVerletIterationCount;
        private float _lastVerletPeakDelta;
        private int _lastTensionCreakFrame = -TensionCreakCooldownFrames;
        private int _lastTowLoadLimitCommandFrame = -TowLoadLimitCommandCooldownFrames;
        private int _currentSimulationFrameIndex;
        private bool _verletFaultDumpedThisActivation;
        private HectonQualityTier _qualityTier = HectonQualityTier.Unknown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticTetherSlotState()
        {
            for (int i = 0; i < _dataVaultSlotReservations.Length; i++)
                _dataVaultSlotReservations[i] = false;
        }

        private GraphicsBuffer _visualSegmentBufferA;
        private GraphicsBuffer _visualSegmentBufferB;
        private GraphicsBuffer _visualSegmentTensionBufferA;
        private GraphicsBuffer _visualSegmentTensionBufferB;
        private int _visualGpuBufferIndex;
        private GraphicsBuffer _visualDrawParamsBufferA;
        private GraphicsBuffer _visualDrawParamsBufferB;
        private int _visualDrawParamsGpuBufferIndex;

        /// <summary>GPU source buffer consumed by the procedural line-strip draw.</summary>
        public GraphicsBuffer VisualSegmentBuffer => _visualGpuBufferIndex == 0 ? _visualSegmentBufferA : _visualSegmentBufferB;

        /// <summary>GPU segment stress source consumed by the procedural tether shader.</summary>
        public GraphicsBuffer VisualSegmentTensionBuffer => _visualGpuBufferIndex == 0 ? _visualSegmentTensionBufferA : _visualSegmentTensionBufferB;

        /// <summary>GPU draw constants consumed by the procedural tether shader.</summary>
        public GraphicsBuffer VisualDrawParamsBuffer => _visualDrawParamsGpuBufferIndex == 0 ? _visualDrawParamsBufferA : _visualDrawParamsBufferB;

        /// <summary>Current number of visual points owned by the line-strip buffer.</summary>
        public int VisualPointCount => _isActive && _visualSegmentPositions.IsCreated ? _visualSegmentPositions.Length : (_verletPositions.IsCreated ? _verletPositions.Length : 0);

        /// <summary>Whether the visual staging and render buffers are ready for use.</summary>
        public bool IsVisualReady => _isActive && VisualSegmentBuffer != null && VisualSegmentTensionBuffer != null && VisualDrawParamsBuffer != null && VisualPointCount > 1;

        /// <summary>Whether the latest visual bounds were rejected by the manager frustum.</summary>
        internal bool IsVisualCulled => _visualCulledThisFrame;

        /// <summary>Active payload rigidbody resolved by this tether.</summary>
        internal Rigidbody PayloadBody => _payloadBody;

        /// <summary>Last peak tether tension sampled by the fixed-step solver.</summary>
        internal float CurrentPeakTension => _primaryConstraintForceMagnitude;

        /// <summary>
        /// Assigns the owning tether manager for pooled runtime instances.
        /// </summary>
        internal void InitializeManager(TetherManager manager)
        {
            _manager = manager;
        }

        /// <summary>
        /// Returns the mutable vault-backed visual staging buffer without a property copy.
        /// </summary>
        internal ref NativeArray<float3> GetVisualSegmentPositionsRef()
        {
            if (_visualSegmentPositions.IsCreated)
                return ref _visualSegmentPositions;

            return ref _verletPositions;
        }

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
            HectonQualityTier qualityTier = _manager != null
                ? _manager.CachedQualityTier
                : HectonQualityTier.Unknown;
            Configure(owner, playerMotor, playerRigidbody, payloadBody, payloadCollider, initialDistance, qualityTier);
        }

        internal void Configure(
            HeavyTowWinch owner,
            HectonPlayerMotor playerMotor,
            Rigidbody playerRigidbody,
            Rigidbody payloadBody,
            Collider payloadCollider,
            float initialDistance,
            HectonQualityTier qualityTier)
        {
            _qualityTier = TetherManager.SanitizeQualityTier(qualityTier);
            _owner = owner;
            _playerMotor = playerMotor;
            _playerRigidbody = playerRigidbody;
            _payloadBody = payloadBody;
            _payloadCollider = payloadCollider;
            _tetherClass = TetherClass.TowCable;
            _springStiffness = ResolveTowSpringStiffness();
            _maxTowBreakDistance = owner != null ? owner.ResolveMaxTowBreakDistance() : 0f;
            _maxCableAcceleration = owner != null ? owner.ResolveMaxCableAcceleration() : 0f;
            _fullTensionExtension = owner != null ? owner.ResolveFullTensionExtension() : 1f;
            _maxBendPoints = owner != null ? owner.ResolveMaxBendPoints() : 0;
            _bendPointClearanceRadius = owner != null ? owner.ResolveBendPointClearanceRadius() : 0.3f;
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
            _visualSegmentCount = ResolveVerletPointCount(_qualityTier);
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
            _lastTowLoadLimitCommandFrame = -TowLoadLimitCommandCooldownFrames;
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
            _verletFaultDumpedThisActivation = false;
            ClearBendMetadata(0);
            EnsureVisualBuffers(_visualSegmentCount);
            EnsureDataVaultCableState();
            InitializeVerletRuntime(
                owner != null ? owner.ResolveTowAnchorPosition() : Vector3.zero,
                _payloadBody != null ? _payloadBody.worldCenterOfMass : Vector3.zero);
            GlobalPhysicsStateManager.RegisterTetherConnection(this, _playerRigidbody, _payloadBody);
            RefreshKinematicAnchorCompensationState(forceRecalculateDamping: true);
            RecalculateDampingCoefficient();
            EnsurePrimaryConstraint(
                owner != null ? owner.ResolveTowAnchorPosition() : Vector3.zero,
                _payloadBody != null ? _payloadBody.worldCenterOfMass : Vector3.zero);
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
            _bioCableRequestedAnchorWS = IsFinite(anchorWS) ? anchorWS : _bioCableCurrentAnchorWS;
            _bioCableRequestedTension01 = math.isfinite(tension01) ? math.saturate(tension01) : 0f;
            _bioCableRequestedCutProgress01 = math.isfinite(cutProgress01) ? math.saturate(cutProgress01) : 1f;
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
            if (!IsFinite(payloadPositionWS))
            {
                payloadPositionWS = Vector3.zero;
                return false;
            }

            if (_payloadCollider != null)
            {
                Bounds bounds = _payloadCollider.bounds;
                Vector3 extents = bounds.extents;
                if (!IsFinite(extents))
                    payloadRadiusWS = 0.75f;
                else
                    payloadRadiusWS = Mathf.Max(0.35f, Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)));
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
        internal TetherLifecycleState Simulate(
            float fixedDeltaTime,
            float fixedStepClockSeconds,
            int fixedFrameIndex,
            int activeTetherCount,
            int maxVisualizedTethers,
            HectonQualityTier qualityTier)
        {
            if (!_isActive || _owner == null || _payloadBody == null || _playerRigidbody == null)
                return TetherLifecycleState.Released;

            if (_owner.ShouldSuppressTow || !_owner.IsTowPayloadValid(_payloadBody, _payloadCollider))
                return TetherLifecycleState.Released;

            _qualityTier = TetherManager.SanitizeQualityTier(qualityTier);
            _currentSimulationFrameIndex = fixedFrameIndex >= 0 ? fixedFrameIndex : 0;

            if (fixedDeltaTime <= 0f || !math.isfinite(fixedDeltaTime))
                return TetherLifecycleState.Alive;

            Vector3 anchorPosition = _owner.ResolveTowAnchorPosition();
            Vector3 payloadPosition = _payloadBody.worldCenterOfMass;
            if (!IsFinite(anchorPosition) || !IsFinite(payloadPosition))
            {
                DumpVerletTelemetryOnce((uint)TetherVerletFaultFlags.NonFiniteNode);
                return TetherLifecycleState.Released;
            }

            if (!Mathf.Approximately(_payloadMass, _payloadBody.mass))
            {
                _payloadMass = _payloadBody.mass;
                _payloadMass01 = _owner.ResolvePayloadMass01(_payloadMass);
                RecalculateDampingCoefficient();
            }

            RefreshKinematicAnchorCompensationState(forceRecalculateDamping: false);
            RefreshTargetLengthFromOwner();

            ResolveSolverReferenceFrame();
            AdvanceExternalCableSnare(fixedDeltaTime);
            Vector3 payloadCurrentForce = ComputePayloadCurrentForce(anchorPosition, payloadPosition, fixedStepClockSeconds);
            ApplyPayloadCurrentForce(payloadCurrentForce, fixedDeltaTime);

            int anchorCount = BuildAnchorChain(anchorPosition, _payloadBody.worldCenterOfMass);
            if (anchorCount < 2)
            {
                ResetRuntimeLoads();
                _owner.ApplyTowLoad(1f);
                return TetherLifecycleState.Alive;
            }

            if (_currentLength > _maxTowBreakDistance)
                return TetherLifecycleState.Released;

            float peakTension = RunVerletSolver(anchorPosition, payloadPosition, payloadCurrentForce, fixedDeltaTime);
            UpdateConstraintTelemetry();
            float bioCablePeakTension = ApplyExternalCableSnareForce();
            if (bioCablePeakTension > peakTension)
                peakTension = bioCablePeakTension;

            UpdateTowDirectionResponse();
            UpdateTowDrag();
            PublishTowLoadLimitIfNeeded(peakTension);

            if (UpdateStressAndSnap(peakTension, fixedDeltaTime))
                return TetherLifecycleState.Snapped;

            return TetherLifecycleState.Alive;
        }

        /// <summary>
        /// Updates the visual staging buffer and uploads it to the GPU render buffer.
        /// </summary>
        public void UpdateVisuals(float deltaTime)
        {
            UpdateVisuals(deltaTime, _qualityTier, null);
        }

        internal void UpdateVisuals(float deltaTime, HectonQualityTier qualityTier, Plane[] frustumPlanes)
        {
            _visualCulledThisFrame = false;
            if (!_isActive)
                return;

            _qualityTier = TetherManager.SanitizeQualityTier(qualityTier);
            if (VisualSegmentBuffer == null || VisualSegmentTensionBuffer == null || VisualDrawParamsBuffer == null)
                EnsureVisualBuffers(_verletNodeCount > 1 ? _verletNodeCount : ResolveVerletPointCount(_qualityTier));

            if (VisualSegmentBuffer == null || VisualSegmentTensionBuffer == null || VisualDrawParamsBuffer == null)
                return;

            EnsureDataVaultCableState(_verletNodeCount > 1 ? _verletNodeCount : ResolveVerletPointCount(_qualityTier));
            if (_verletPositions.IsCreated && _verletPositions.Length > 1)
            {
                UpdateVerletVisualUpload(_qualityTier, frustumPlanes);
                return;
            }

            if (!_visualSegmentPositions.IsCreated)
                return;

            Vector3 anchorPosition = _owner != null ? _owner.ResolveTowAnchorPosition() : Vector3.zero;
            Vector3 payloadPosition = _payloadBody != null ? _payloadBody.worldCenterOfMass : anchorPosition;
            ResolveSolverReferenceFrame();
            int anchorCount = BuildAnchorChain(anchorPosition, payloadPosition);
            if (anchorCount < 2)
                return;

            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(deltaTime, 0f) : 0f;
            float blendT = ResolveBlendFactor(_visualSegmentSmoothSpeed, safeDeltaTime);
            CopyVisualSolverState(anchorCount);
            BuildVisualCatenaryImmediate(
                anchorCount,
                _currentLength,
                blendT,
                VisualSagScale,
                _visualAnchorPositions,
                _visualSegmentLengths,
                _visualSegmentPositions);

            Vector3 minBounds = IsFinite(anchorPosition) ? anchorPosition : Vector3.zero;
            Vector3 maxBounds = minBounds;
            for (int i = 0; i < _visualSegmentPositions.Length; i++)
            {
                float3 blendedPoint = SanitizeFinite(_visualSegmentPositions[i]);
                _visualSegmentPositions[i] = blendedPoint;
                Vector3 blendedPointV3 = new Vector3(blendedPoint.x, blendedPoint.y, blendedPoint.z);
                minBounds = Vector3.Min(minBounds, blendedPointV3);
                maxBounds = Vector3.Max(maxBounds, blendedPointV3);
            }

            _visualBounds.SetMinMax(minBounds, maxBounds);
            if (ShouldUploadVisualBounds(frustumPlanes))
                UploadVisualGpuBuffers(includeTension: true);
            else
                _visualCulledThisFrame = true;
        }

        private static void BuildVisualCatenaryImmediate(
            int anchorCount,
            float currentLength,
            float blendT,
            float sagScale,
            NativeArray<float3> anchorPositions,
            NativeArray<float> segmentLengths,
            NativeArray<float3> visualSegmentPositions)
        {
            int pointCount = visualSegmentPositions.Length;
            if (anchorCount < 2 || pointCount <= 0)
                return;

            float safeCurrentLength = math.isfinite(currentLength) ? math.max(currentLength, MinDistance) : MinDistance;
            float safeSagScale = math.isfinite(sagScale) ? math.max(0f, sagScale) : 0f;
            float safeBlendT = math.isfinite(blendT) ? math.saturate(blendT) : 0f;
            float pathLength = safeCurrentLength;
            float step = pointCount > 1 ? pathLength * math.rcp(pointCount - 1) : pathLength;
            for (int index = 0; index < pointCount; index++)
            {
                float travelDistance = step * index;
                float3 targetPoint = SampleVisualPathPoint(anchorCount, travelDistance, anchorPositions, segmentLengths, safeSagScale);
                if (index == 0 || index == pointCount - 1)
                {
                    visualSegmentPositions[index] = SanitizeFinite(targetPoint);
                    continue;
                }

                float3 currentPoint = SanitizeFinite(visualSegmentPositions[index]);
                visualSegmentPositions[index] = SanitizeFinite(math.lerp(currentPoint, targetPoint, safeBlendT));
            }
        }

        private static float3 SampleVisualPathPoint(
            int anchorCount,
            float travelDistance,
            NativeArray<float3> anchorPositions,
            NativeArray<float> segmentLengths,
            float sagScale)
        {
            int segmentCount = anchorCount - 1;
            float remaining = math.isfinite(travelDistance) ? math.max(0f, travelDistance) : 0f;
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                float rawSegmentLength = segmentLengths[segmentIndex];
                float segmentLength = math.isfinite(rawSegmentLength) ? math.max(rawSegmentLength, MinDistance) : MinDistance;
                if (remaining > segmentLength && segmentIndex < segmentCount - 1)
                {
                    remaining -= segmentLength;
                    continue;
                }

                float segmentT = math.saturate(remaining * math.rcp(segmentLength));
                float3 start = SanitizeFinite(anchorPositions[segmentIndex]);
                float3 end = SanitizeFinite(anchorPositions[segmentIndex + 1]);
                float3 basePoint = math.lerp(start, end, segmentT);
                float sag = segmentLength * sagScale;
                float sagWeight = 4f * segmentT * (1f - segmentT);
                return SanitizeFinite(basePoint + new float3(0f, -(sag * sagWeight), 0f));
            }

            return SanitizeFinite(anchorPositions[anchorCount - 1]);
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
            ReleasePrimaryConstraint();
            DisposeDataVaultCableState();
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
            _verletRuntimeInitialized = false;
            _verletNodeCount = 0;
            _verletSolverOrigin = float3.zero;
            _lastVerletIterationCount = 0;
            _lastVerletPeakDelta = 0f;
            _lastTensionCreakFrame = -TensionCreakCooldownFrames;
            _lastTowLoadLimitCommandFrame = -TowLoadLimitCommandCooldownFrames;
            _verletFaultDumpedThisActivation = false;
            _qualityTier = HectonQualityTier.Unknown;
            ClearBendMetadata(0);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Releases persistent native and GPU resources.
        /// </summary>
        public void DisposeRuntimeResources()
        {
            ReleaseVisualBuffers();

            DisposeDataVaultCableState();
            _verletRuntimeInitialized = false;
            _verletNodeCount = 0;
            _verletSolverOrigin = float3.zero;
        }

        private void OnDestroy()
        {
            ReleasePrimaryConstraint();
            DisposeRuntimeResources();
        }

        private void EnsureVisualBuffers(int pointCount)
        {
            if (pointCount < 2)
                pointCount = 2;
            pointCount = math.min(pointCount, DataVaultCablePointCount);

            EnsureVerletBuffers(pointCount);
            if (!_visualSegmentPositions.IsCreated ||
                !_visualSegmentGpuPoints.IsCreated ||
                !_visualAnchorPositions.IsCreated ||
                !_visualSegmentLengths.IsCreated ||
                !_verletSegmentTensions.IsCreated)
                return;

            const int pointStride = VerletCableLayout.GpuSplinePointStrideBytes;
            bool positionBuffersInvalid =
                _visualSegmentBufferA == null ||
                _visualSegmentBufferB == null ||
                _visualSegmentBufferA.count != pointCount ||
                _visualSegmentBufferB.count != pointCount ||
                _visualSegmentBufferA.stride != pointStride ||
                _visualSegmentBufferB.stride != pointStride;
            if (positionBuffersInvalid)
            {
                ReleaseGraphicsBuffer(ref _visualSegmentBufferA);
                ReleaseGraphicsBuffer(ref _visualSegmentBufferB);
                _visualGpuBufferIndex = 0;
                // COLD ALLOC: GraphicsBuffer[pointCount * 2] — double-buffered GPU line-strip source for tether visuals — owner: TetherInstance
                EnsureVisualGraphicsBuffer(ref _visualSegmentBufferA, pointCount, pointStride);
                EnsureVisualGraphicsBuffer(ref _visualSegmentBufferB, pointCount, pointStride);
            }

            int visualSegmentCount = math.max(1, pointCount - 1);
            const int tensionStride = sizeof(float);
            bool tensionBuffersInvalid =
                _visualSegmentTensionBufferA == null ||
                _visualSegmentTensionBufferB == null ||
                _visualSegmentTensionBufferA.count != visualSegmentCount ||
                _visualSegmentTensionBufferB.count != visualSegmentCount ||
                _visualSegmentTensionBufferA.stride != tensionStride ||
                _visualSegmentTensionBufferB.stride != tensionStride;
            if (tensionBuffersInvalid)
            {
                ReleaseGraphicsBuffer(ref _visualSegmentTensionBufferA);
                ReleaseGraphicsBuffer(ref _visualSegmentTensionBufferB);
                _visualGpuBufferIndex = 0;
                // COLD ALLOC: GraphicsBuffer[visualSegmentCount * 2] — double-buffered per-segment stress source for tether shader — owner: TetherInstance
                EnsureVisualGraphicsBuffer(ref _visualSegmentTensionBufferA, visualSegmentCount, tensionStride);
                EnsureVisualGraphicsBuffer(ref _visualSegmentTensionBufferB, visualSegmentCount, tensionStride);
            }

            const int drawParamsStride = VerletCableLayout.GpuDrawParamsStrideBytes;
            bool drawParamsBuffersInvalid =
                _visualDrawParamsBufferA == null ||
                _visualDrawParamsBufferB == null ||
                _visualDrawParamsBufferA.count != 1 ||
                _visualDrawParamsBufferB.count != 1 ||
                _visualDrawParamsBufferA.stride != drawParamsStride ||
                _visualDrawParamsBufferB.stride != drawParamsStride;
            if (drawParamsBuffersInvalid)
            {
                ReleaseGraphicsBuffer(ref _visualDrawParamsBufferA);
                ReleaseGraphicsBuffer(ref _visualDrawParamsBufferB);
                _visualDrawParamsGpuBufferIndex = 0;
                // COLD ALLOC: GraphicsBuffer[2] - double-buffered 80-byte draw payload for tether shader constants - owner: TetherInstance
                EnsureVisualGraphicsBuffer(ref _visualDrawParamsBufferA, 1, drawParamsStride);
                EnsureVisualGraphicsBuffer(ref _visualDrawParamsBufferB, 1, drawParamsStride);
            }
        }

        private static void EnsureVisualGraphicsBuffer(ref GraphicsBuffer buffer, int count, int stride)
        {
            if (buffer != null && (buffer.count != count || buffer.stride != stride))
                ReleaseGraphicsBuffer(ref buffer);

            if (buffer != null)
                return;

            // COLD ALLOC: GraphicsBuffer[count] — tether GPU upload lane — owner: TetherInstance
            buffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                stride);
        }

        private void UploadVisualGpuBuffers(bool includeTension)
        {
            if (!_visualSegmentPositions.IsCreated || !_visualSegmentGpuPoints.IsCreated)
                return;

            int writeIndex = 1 - _visualGpuBufferIndex;
            GraphicsBuffer positionWriteBuffer = writeIndex == 0 ? _visualSegmentBufferA : _visualSegmentBufferB;
            if (positionWriteBuffer == null)
                return;

            int pointCount = math.min(_visualSegmentPositions.Length, _visualSegmentGpuPoints.Length);
            var copyJob = new TetherVisualGpuSplineCopyJob
            {
                Positions = _visualSegmentPositions,
                SegmentTensions = includeTension ? _verletSegmentTensions : default,
                GpuPoints = _visualSegmentGpuPoints,
                InvSnapTension = math.rcp(math.max(ResolveSnapTensionThreshold(), 1f))
            };
            copyJob.Run(pointCount);

            GraphicsBufferUploadUtility.UploadNativeArray(
                positionWriteBuffer,
                _visualSegmentGpuPoints,
                pointCount);

            if (includeTension && _verletSegmentTensions.IsCreated)
            {
                GraphicsBuffer tensionWriteBuffer = writeIndex == 0 ? _visualSegmentTensionBufferA : _visualSegmentTensionBufferB;
                if (tensionWriteBuffer != null)
                {
                    GraphicsBufferUploadUtility.UploadNativeArray(
                        tensionWriteBuffer,
                        _verletSegmentTensions,
                        _verletSegmentTensions.Length);
                }
            }

            _visualGpuBufferIndex = writeIndex;
        }

        internal bool UploadVisualDrawParams(
            Color tetherColor,
            Color tetherStressColor,
            float segmentStressScale,
            float radius,
            int pointCount,
            bool useIndirect,
            int visualTier,
            float crystalDensity,
            float siltIntensity,
            float visualClock)
        {
            int writeIndex = 1 - _visualDrawParamsGpuBufferIndex;
            GraphicsBuffer drawParamsWriteBuffer = writeIndex == 0 ? _visualDrawParamsBufferA : _visualDrawParamsBufferB;
            if (drawParamsWriteBuffer == null)
                return false;

            float safeSegmentStressScale = math.isfinite(segmentStressScale) ? math.max(segmentStressScale, 0f) : 0f;
            float safeRadius = math.isfinite(radius) ? math.max(radius, 0.001f) : 0.001f;
            float safeVisualClock = math.isfinite(visualClock) ? math.max(visualClock, 0f) : 0f;
            float safeCrystalDensity = math.isfinite(crystalDensity) ? math.saturate(crystalDensity) : 0f;
            float safeSiltIntensity = math.isfinite(siltIntensity) ? math.saturate(siltIntensity) : 0f;
            float safeVisualTier = math.max(0, visualTier);
            int safePointCount = math.max(0, pointCount);
            GpuCableDrawParamsDTO drawParams = new GpuCableDrawParamsDTO
            {
                Color = new float4(tetherColor.r, tetherColor.g, tetherColor.b, tetherColor.a),
                StressColor = new float4(tetherStressColor.r, tetherStressColor.g, tetherStressColor.b, tetherStressColor.a),
                Params0 = new float4(VisualStress01, safeSegmentStressScale, safePointCount, safeRadius),
                Params1 = new float4(useIndirect ? 1f : 0f, safeVisualTier, safeCrystalDensity, safeSiltIntensity),
                Params2 = new float4(safeVisualClock, 0f, 0f, 0f)
            };

            NativeArray<GpuCableDrawParamsDTO> mapped = drawParamsWriteBuffer.LockBufferForWrite<GpuCableDrawParamsDTO>(0, 1);
            mapped[0] = drawParams;
            drawParamsWriteBuffer.UnlockBufferAfterWrite<GpuCableDrawParamsDTO>(1);
            _visualDrawParamsGpuBufferIndex = writeIndex;
            return true;
        }

        private void ReleaseVisualBuffers()
        {
            ReleaseGraphicsBuffer(ref _visualSegmentBufferA);
            ReleaseGraphicsBuffer(ref _visualSegmentBufferB);
            ReleaseGraphicsBuffer(ref _visualSegmentTensionBufferA);
            ReleaseGraphicsBuffer(ref _visualSegmentTensionBufferB);
            ReleaseGraphicsBuffer(ref _visualDrawParamsBufferA);
            ReleaseGraphicsBuffer(ref _visualDrawParamsBufferB);
            _visualGpuBufferIndex = 0;
            _visualDrawParamsGpuBufferIndex = 0;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void EnsureVerletBuffers(int nodeCount)
        {
            if (nodeCount < 2)
                nodeCount = 2;
            nodeCount = math.min(nodeCount, DataVaultCablePointCount);
            EnsureDataVaultCableState(nodeCount);
            _verletNodeCount = _verletPositions.IsCreated ? _verletPositions.Length : 0;
        }

        private void InitializeVerletRuntime(Vector3 anchorPosition, Vector3 payloadPosition)
        {
            if (!_verletPositions.IsCreated || !_verletPreviousPositions.IsCreated || _verletPositions.Length < 2)
                return;

            float3 anchor = new float3(anchorPosition.x, anchorPosition.y, anchorPosition.z);
            float3 payload = new float3(payloadPosition.x, payloadPosition.y, payloadPosition.z);
            _verletSolverOrigin = SanitizeFinite(anchor);
            float3 localPayload = SanitizeFinite(payload - _verletSolverOrigin);
            int nodeCount = _verletPositions.Length;
            float invLast = math.rcp(math.max(1, nodeCount - 1));
            for (int i = 0; i < nodeCount; i++)
            {
                float t = i * invLast;
                float3 position = math.lerp(float3.zero, localPayload, t);
                _verletPositions[i] = position;
                _verletPreviousPositions[i] = position;
                if (_verletVelocities.IsCreated && i < _verletVelocities.Length)
                    _verletVelocities[i] = float3.zero;
                _verletPinnedPositions[i] = position;
                _verletPinnedMask[i] = (byte)((i == 0 || i == nodeCount - 1) ? 1 : 0);
                _verletNodeFaultFlags[i] = 0;
            }

            float segmentRestLength = math.max(_restLength, MinDistance) * invLast;
            for (int i = 0; i < _verletSegmentRestLengths.Length; i++)
            {
                _verletSegmentRestLengths[i] = segmentRestLength;
                _verletSegmentTensions[i] = 0f;
            }

            _verletSolverStats[0] = 0f;
            _verletSolverFlags[0] = TetherVerletFaultFlags.None;
            ClearDataVaultTelemetrySlot();
            int telemetryHeadIndex = ResolveTelemetryHeadIndex();
            if (_verletTelemetryHead.IsCreated && (uint)telemetryHeadIndex < (uint)_verletTelemetryHead.Length)
                _verletTelemetryHead[telemetryHeadIndex] = 0;
            _verletRuntimeInitialized = true;
        }

        private void RebaseVerletSolverOrigin(float3 nextOrigin)
        {
            if (!math.all(math.isfinite(nextOrigin)))
                return;

            if (!_verletRuntimeInitialized)
            {
                _verletSolverOrigin = nextOrigin;
                return;
            }

            float3 offset = _verletSolverOrigin - nextOrigin;
            _verletSolverOrigin = nextOrigin;
            if (math.lengthsq(offset) <= MinVectorMagnitudeSq)
                return;

            for (int i = 0; i < _verletPositions.Length; i++)
            {
                _verletPositions[i] = SanitizeFinite(_verletPositions[i] + offset);
                if (i < _verletPreviousPositions.Length)
                    _verletPreviousPositions[i] = SanitizeFinite(_verletPreviousPositions[i] + offset);
                if (i < _verletPinnedPositions.Length)
                    _verletPinnedPositions[i] = SanitizeFinite(_verletPinnedPositions[i] + offset);
            }
        }

        private void EnsureDataVaultCableState(int requestedNodeCount = 0)
        {
            int nodeCount = ResolveDataVaultNodeCount(requestedNodeCount);
            _dataVault = _manager != null ? _manager.CachedDataVault : _dataVault;
            IDataVault vault = _dataVault;
            if (_dataVaultCableStateReady &&
                _dataVaultSlot >= 0 &&
                vault != null &&
                _dataVaultResolvedGeneration == vault.VaultGenerationID &&
                AreDataVaultScratchSlicesReady(nodeCount))
            {
                return;
            }

            if (_dataVaultSlot < 0 && !TryAcquireDataVaultSlot())
                return;

            if (vault == null)
                return;

            int segmentCount = math.max(1, nodeCount - 1);
            int nodeOffset = _dataVaultSlot * DataVaultCablePointCount;
            int segmentOffset = _dataVaultSlot * DataVaultCableSegmentCount;
            int anchorOffset = _dataVaultSlot * MaxAnchors;
            int visualSegmentOffset = _dataVaultSlot * MaxSegments;
            int scalarOffset = _dataVaultSlot;
            bool positionsReady = EnsureDataVaultCableArray(
                ref _dataVaultCablePositions,
                ref _dataVaultCablePositionsHandle,
                BufferID.TetherCablePositions,
                DataVaultCablePointCapacity,
                nameof(_dataVaultCablePositions),
                DataVaultFlagPositions);
            bool previousReady = EnsureDataVaultCableArray(
                ref _dataVaultCablePreviousPositions,
                ref _dataVaultCablePreviousPositionsHandle,
                BufferID.TetherCablePreviousPositions,
                DataVaultCablePointCapacity,
                nameof(_dataVaultCablePreviousPositions),
                DataVaultFlagPreviousPositions);
            bool velocitiesReady = EnsureDataVaultCableArray(
                ref _dataVaultCableVelocities,
                ref _dataVaultCableVelocitiesHandle,
                BufferID.TetherCableVelocities,
                DataVaultCablePointCapacity,
                nameof(_dataVaultCableVelocities),
                DataVaultFlagVelocities);
            bool massesReady = EnsureDataVaultCableArray(
                ref _dataVaultCableMasses,
                ref _dataVaultCableMassesHandle,
                BufferID.TetherCableMasses,
                DataVaultCablePointCapacity,
                nameof(_dataVaultCableMasses),
                DataVaultFlagMasses);
            bool tensionReady = EnsureDataVaultCableArray(
                ref _dataVaultCableSegmentTensions,
                ref _dataVaultCableSegmentTensionsHandle,
                BufferID.TetherCableSegmentTensions,
                DataVaultCableSegmentCapacity,
                nameof(_dataVaultCableSegmentTensions),
                DataVaultFlagSegmentTensions);
            bool telemetryReady = EnsureDataVaultCableArray(
                ref _verletTelemetryRing,
                ref _verletTelemetryRingHandle,
                BufferID.TetherCableBlackBox,
                DataVaultTelemetryCapacity,
                nameof(_verletTelemetryRing),
                DataVaultFlagTelemetryRing);
            bool telemetryHeadReady = EnsureDataVaultCableArray(
                ref _verletTelemetryHead,
                ref _verletTelemetryHeadHandle,
                BufferID.TetherCableBlackBoxHead,
                DataVaultTelemetryHeadCapacity,
                nameof(_verletTelemetryHead),
                DataVaultFlagTelemetryHead);
            bool tensionForcesReady = EnsureDataVaultCableArray(
                ref _verletTensionForces,
                ref _verletTensionForcesHandle,
                BufferID.VerletCableTensionForces,
                DataVaultMaxTetherSlots,
                nameof(_verletTensionForces),
                DataVaultFlagVerletTensionForces);
            bool tuningReady = EnsureDataVaultCableArray(
                ref _verletTuning,
                ref _verletTuningHandle,
                BufferID.VerletCableTuning,
                1,
                nameof(_verletTuning),
                DataVaultFlagVerletTuning);
            if (tuningReady)
                EnsureVerletTuningDefaults();
            bool visualPositionsReady = EnsureDataVaultSliceArray(
                ref _visualSegmentPositions,
                ref _visualSegmentPositionsHandle,
                BufferID.TetherVisualSegmentPositions,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVisualSegmentPositions);
            bool visualGpuPointsReady = EnsureDataVaultSliceArray(
                ref _visualSegmentGpuPoints,
                ref _visualSegmentGpuPointsHandle,
                BufferID.VerletCableGpuSplinePoints,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVisualGpuSplinePoints);
            bool visualAnchorsReady = EnsureDataVaultSliceArray(
                ref _visualAnchorPositions,
                ref _visualAnchorPositionsHandle,
                BufferID.TetherVisualAnchorPositions,
                DataVaultVisualAnchorCapacity,
                anchorOffset,
                MaxAnchors,
                DataVaultFlagVisualAnchorPositions);
            bool visualLengthsReady = EnsureDataVaultSliceArray(
                ref _visualSegmentLengths,
                ref _visualSegmentLengthsHandle,
                BufferID.TetherVisualSegmentLengths,
                DataVaultVisualSegmentLengthCapacity,
                visualSegmentOffset,
                MaxSegments,
                DataVaultFlagVisualSegmentLengths);
            bool verletPositionsReady = EnsureDataVaultSliceArray(
                ref _verletPositions,
                ref _verletPositionsHandle,
                BufferID.TetherVerletPositions,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVerletPositions);
            bool verletPreviousReady = EnsureDataVaultSliceArray(
                ref _verletPreviousPositions,
                ref _verletPreviousPositionsHandle,
                BufferID.TetherVerletPreviousPositions,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVerletPreviousPositions);
            bool verletVelocitiesReady = EnsureDataVaultSliceArray(
                ref _verletVelocities,
                ref _verletVelocitiesHandle,
                BufferID.TetherVerletVelocities,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVerletVelocities);
            bool verletPinnedPositionsReady = EnsureDataVaultSliceArray(
                ref _verletPinnedPositions,
                ref _verletPinnedPositionsHandle,
                BufferID.TetherVerletPinnedPositions,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVerletPinnedPositions);
            bool verletPinnedMaskReady = EnsureDataVaultSliceArray(
                ref _verletPinnedMask,
                ref _verletPinnedMaskHandle,
                BufferID.TetherVerletPinnedMask,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVerletPinnedMask);
            bool verletRestLengthsReady = EnsureDataVaultSliceArray(
                ref _verletSegmentRestLengths,
                ref _verletSegmentRestLengthsHandle,
                BufferID.TetherVerletSegmentRestLengths,
                DataVaultScratchSegmentCapacity,
                segmentOffset,
                segmentCount,
                DataVaultFlagVerletSegmentRestLengths);
            bool verletTensionsReady = EnsureDataVaultSliceArray(
                ref _verletSegmentTensions,
                ref _verletSegmentTensionsHandle,
                BufferID.TetherVerletSegmentTensions,
                DataVaultScratchSegmentCapacity,
                segmentOffset,
                segmentCount,
                DataVaultFlagVerletSegmentTensions);
            bool verletCorrectionsReady = EnsureDataVaultSliceArray(
                ref _verletCorrections,
                ref _verletCorrectionsHandle,
                BufferID.TetherVerletCorrections,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVerletCorrections);
            bool verletCorrectionWeightsReady = EnsureDataVaultSliceArray(
                ref _verletCorrectionWeights,
                ref _verletCorrectionWeightsHandle,
                BufferID.TetherVerletCorrectionWeights,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVerletCorrectionWeights);
            bool solverStatsReady = EnsureDataVaultSliceArray(
                ref _verletSolverStats,
                ref _verletSolverStatsHandle,
                BufferID.TetherVerletSolverStats,
                DataVaultScratchScalarCapacity,
                scalarOffset,
                1,
                DataVaultFlagVerletSolverStats);
            bool solverFlagsReady = EnsureDataVaultSliceArray(
                ref _verletSolverFlags,
                ref _verletSolverFlagsHandle,
                BufferID.TetherVerletSolverFlags,
                DataVaultScratchScalarCapacity,
                scalarOffset,
                1,
                DataVaultFlagVerletSolverFlags);
            bool nodeFaultFlagsReady = EnsureDataVaultSliceArray(
                ref _verletNodeFaultFlags,
                ref _verletNodeFaultFlagsHandle,
                BufferID.TetherVerletNodeFaultFlags,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVerletNodeFaultFlags);

            _dataVaultCableStateReady = positionsReady &&
                                        previousReady &&
                                        velocitiesReady &&
                                        massesReady &&
                                        tensionReady &&
                                        telemetryReady &&
                                        telemetryHeadReady &&
                                        tensionForcesReady &&
                                        tuningReady &&
                                        visualPositionsReady &&
                                        visualGpuPointsReady &&
                                        visualAnchorsReady &&
                                        visualLengthsReady &&
                                        verletPositionsReady &&
                                        verletPreviousReady &&
                                        verletVelocitiesReady &&
                                        verletPinnedPositionsReady &&
                                        verletPinnedMaskReady &&
                                        verletRestLengthsReady &&
                                        verletTensionsReady &&
                                        verletCorrectionsReady &&
                                        verletCorrectionWeightsReady &&
                                        solverStatsReady &&
                                        solverFlagsReady &&
                                        nodeFaultFlagsReady;
            _dataVaultResolvedGeneration = _dataVaultCableStateReady ? vault.VaultGenerationID : 0u;
        }

        private int ResolveDataVaultNodeCount(int requestedNodeCount)
        {
            int nodeCount = requestedNodeCount > 0
                ? requestedNodeCount
                : (_verletNodeCount > 1 ? _verletNodeCount : ResolveVerletPointCount(_qualityTier));
            return math.clamp(nodeCount, 2, DataVaultCablePointCount);
        }

        private bool AreDataVaultScratchSlicesReady(int nodeCount)
        {
            int segmentCount = math.max(1, nodeCount - 1);
            return _visualSegmentPositions.IsCreated && _visualSegmentPositions.Length == nodeCount &&
                   _visualSegmentGpuPoints.IsCreated && _visualSegmentGpuPoints.Length == nodeCount &&
                   _visualAnchorPositions.IsCreated && _visualAnchorPositions.Length == MaxAnchors &&
                   _visualSegmentLengths.IsCreated && _visualSegmentLengths.Length == MaxSegments &&
                   _verletPositions.IsCreated && _verletPositions.Length == nodeCount &&
                   _verletPreviousPositions.IsCreated && _verletPreviousPositions.Length == nodeCount &&
                   _verletVelocities.IsCreated && _verletVelocities.Length == nodeCount &&
                   _verletPinnedPositions.IsCreated && _verletPinnedPositions.Length == nodeCount &&
                   _verletPinnedMask.IsCreated && _verletPinnedMask.Length == nodeCount &&
                   _verletSegmentRestLengths.IsCreated && _verletSegmentRestLengths.Length == segmentCount &&
                   _verletSegmentTensions.IsCreated && _verletSegmentTensions.Length == segmentCount &&
                   _verletCorrections.IsCreated && _verletCorrections.Length == nodeCount &&
                   _verletCorrectionWeights.IsCreated && _verletCorrectionWeights.Length == nodeCount &&
                   _verletSolverStats.IsCreated && _verletSolverStats.Length == 1 &&
                   _verletSolverFlags.IsCreated && _verletSolverFlags.Length == 1 &&
                   _verletTensionForces.IsCreated && _verletTensionForces.Length >= DataVaultMaxTetherSlots &&
                   _verletTuning.IsCreated && _verletTuning.Length >= 1 &&
                   _verletNodeFaultFlags.IsCreated && _verletNodeFaultFlags.Length == nodeCount;
        }

        private bool EnsureDataVaultCableArray<T>(
            ref NativeArray<T> array,
            ref VaultBufferHandle<T> handle,
            BufferID bufferId,
            int length,
            string label,
            int vaultFlag)
            where T : struct
        {
            if (length <= 0)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
            {
                ResetDataVaultCableView(ref array, ref handle, vaultFlag);
                return false;
            }

            if (!handle.IsCreated || handle.Length < length)
            {
                handle = vault.GetBufferHandle<T>(
                    bufferId,
                    length,
                    SystemID.Physics,
                    NativeArrayOptions.ClearMemory);
            }

            NativeArray<T> vaultArray = handle.Resolve(vault);
            if (vaultArray.IsCreated && vaultArray.Length >= length)
            {
                _dataVaultNativeStateMask |= vaultFlag;
                array = vaultArray;
                return true;
            }

            ResetDataVaultCableView(ref array, ref handle, vaultFlag);
            return false;
        }

        private bool EnsureDataVaultSliceArray<T>(
            ref NativeArray<T> array,
            ref VaultBufferHandle<T> handle,
            BufferID bufferId,
            int totalLength,
            int offset,
            int length,
            int vaultFlag)
            where T : struct
        {
            if (totalLength <= 0 || offset < 0 || length <= 0 || offset + length > totalLength)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
            {
                ResetDataVaultCableView(ref array, ref handle, vaultFlag);
                return false;
            }

            if (!handle.IsCreated || handle.Length < totalLength)
            {
                handle = vault.GetBufferHandle<T>(
                    bufferId,
                    totalLength,
                    SystemID.Physics,
                    NativeArrayOptions.ClearMemory);
            }

            NativeArray<T> vaultArray = handle.Resolve(vault);
            if (vaultArray.IsCreated && offset + length <= vaultArray.Length)
            {
                _dataVaultNativeStateMask |= vaultFlag;
                array = vaultArray.GetSubArray(offset, length);
                return true;
            }

            ResetDataVaultCableView(ref array, ref handle, vaultFlag);
            return false;
        }

        private void DisposeDataVaultCableState()
        {
            ClearDataVaultCableEntry();
            ClearDataVaultTelemetrySlot();
            DisposeDataVaultCableArray(ref _dataVaultCablePositions, ref _dataVaultCablePositionsHandle, DataVaultFlagPositions);
            DisposeDataVaultCableArray(ref _dataVaultCablePreviousPositions, ref _dataVaultCablePreviousPositionsHandle, DataVaultFlagPreviousPositions);
            DisposeDataVaultCableArray(ref _dataVaultCableVelocities, ref _dataVaultCableVelocitiesHandle, DataVaultFlagVelocities);
            DisposeDataVaultCableArray(ref _dataVaultCableMasses, ref _dataVaultCableMassesHandle, DataVaultFlagMasses);
            DisposeDataVaultCableArray(ref _dataVaultCableSegmentTensions, ref _dataVaultCableSegmentTensionsHandle, DataVaultFlagSegmentTensions);
            DisposeDataVaultCableArray(ref _verletTelemetryRing, ref _verletTelemetryRingHandle, DataVaultFlagTelemetryRing);
            DisposeDataVaultCableArray(ref _verletTelemetryHead, ref _verletTelemetryHeadHandle, DataVaultFlagTelemetryHead);
            DisposeDataVaultCableArray(ref _verletTensionForces, ref _verletTensionForcesHandle, DataVaultFlagVerletTensionForces);
            DisposeDataVaultCableArray(ref _verletTuning, ref _verletTuningHandle, DataVaultFlagVerletTuning);
            DisposeDataVaultCableArray(ref _visualSegmentPositions, ref _visualSegmentPositionsHandle, DataVaultFlagVisualSegmentPositions);
            DisposeDataVaultCableArray(ref _visualSegmentGpuPoints, ref _visualSegmentGpuPointsHandle, DataVaultFlagVisualGpuSplinePoints);
            DisposeDataVaultCableArray(ref _visualAnchorPositions, ref _visualAnchorPositionsHandle, DataVaultFlagVisualAnchorPositions);
            DisposeDataVaultCableArray(ref _visualSegmentLengths, ref _visualSegmentLengthsHandle, DataVaultFlagVisualSegmentLengths);
            DisposeDataVaultCableArray(ref _verletPositions, ref _verletPositionsHandle, DataVaultFlagVerletPositions);
            DisposeDataVaultCableArray(ref _verletPreviousPositions, ref _verletPreviousPositionsHandle, DataVaultFlagVerletPreviousPositions);
            DisposeDataVaultCableArray(ref _verletVelocities, ref _verletVelocitiesHandle, DataVaultFlagVerletVelocities);
            DisposeDataVaultCableArray(ref _verletPinnedPositions, ref _verletPinnedPositionsHandle, DataVaultFlagVerletPinnedPositions);
            DisposeDataVaultCableArray(ref _verletPinnedMask, ref _verletPinnedMaskHandle, DataVaultFlagVerletPinnedMask);
            DisposeDataVaultCableArray(ref _verletSegmentRestLengths, ref _verletSegmentRestLengthsHandle, DataVaultFlagVerletSegmentRestLengths);
            DisposeDataVaultCableArray(ref _verletSegmentTensions, ref _verletSegmentTensionsHandle, DataVaultFlagVerletSegmentTensions);
            DisposeDataVaultCableArray(ref _verletCorrections, ref _verletCorrectionsHandle, DataVaultFlagVerletCorrections);
            DisposeDataVaultCableArray(ref _verletCorrectionWeights, ref _verletCorrectionWeightsHandle, DataVaultFlagVerletCorrectionWeights);
            DisposeDataVaultCableArray(ref _verletSolverStats, ref _verletSolverStatsHandle, DataVaultFlagVerletSolverStats);
            DisposeDataVaultCableArray(ref _verletSolverFlags, ref _verletSolverFlagsHandle, DataVaultFlagVerletSolverFlags);
            DisposeDataVaultCableArray(ref _verletNodeFaultFlags, ref _verletNodeFaultFlagsHandle, DataVaultFlagVerletNodeFaultFlags);
            ReleaseDataVaultSlot();
            _dataVault = null;
            _dataVaultCableStateReady = false;
            _dataVaultResolvedGeneration = 0u;
        }

        private void DisposeDataVaultCableArray<T>(
            ref NativeArray<T> array,
            ref VaultBufferHandle<T> handle,
            int vaultFlag)
            where T : struct
        {
            handle = default;
            array = default;
            _dataVaultNativeStateMask &= ~vaultFlag;
        }

        private void ResetDataVaultCableView<T>(
            ref NativeArray<T> array,
            ref VaultBufferHandle<T> handle,
            int vaultFlag)
            where T : struct
        {
            handle = default;
            array = default;
            _dataVaultNativeStateMask &= ~vaultFlag;
            _dataVaultResolvedGeneration = 0u;
        }

        private void EnsureVerletTuningDefaults()
        {
            if (!_verletTuning.IsCreated || _verletTuning.Length == 0)
                return;

            VerletCableTuningDTO tuning = _verletTuning[0];
            bool uninitialized = math.lengthsq(tuning.Gravity) <= 0.000001f &&
                                 tuning.ConstraintIterations == 0 &&
                                 tuning.BreakForce <= 0f;
            if (!uninitialized)
                return;

            _verletTuning[0] = new VerletCableTuningDTO
            {
                Gravity = new float3(0f, -HectonPhysicsContract.GravityMetersPerSecondSquaredConst, 0f),
                FluidFriction = VerletMidVelocityDamping,
                ConstraintIterations = 0,
                StretchThreshold01 = VerletPlasticStretch01,
                BreakForce = 0f,
                RockFriction01 = VerletRockFriction01,
                ReelSpeedMetersPerSecond = VerletReelSpeedMetersPerSecond,
                Reserved0 = 0f,
                Reserved1 = 0f
            };
        }

        private VerletCableTuningDTO ResolveVerletTuning()
        {
            if (_verletTuning.IsCreated && _verletTuning.Length > 0)
                return _verletTuning[0];

            return new VerletCableTuningDTO
            {
                Gravity = new float3(0f, -HectonPhysicsContract.GravityMetersPerSecondSquaredConst, 0f),
                FluidFriction = 0f,
                ConstraintIterations = 0,
                StretchThreshold01 = VerletPlasticStretch01,
                BreakForce = 0f,
                RockFriction01 = VerletRockFriction01,
                ReelSpeedMetersPerSecond = VerletReelSpeedMetersPerSecond,
                Reserved0 = 0f,
                Reserved1 = 0f
            };
        }

        private bool TryAcquireDataVaultSlot()
        {
            for (int i = 0; i < _dataVaultSlotReservations.Length; i++)
            {
                if (_dataVaultSlotReservations[i])
                    continue;

                _dataVaultSlotReservations[i] = true;
                _dataVaultSlot = i;
                return true;
            }

            return false;
        }

        private void ReleaseDataVaultSlot()
        {
            if (_dataVaultSlot >= 0 && _dataVaultSlot < _dataVaultSlotReservations.Length)
                _dataVaultSlotReservations[_dataVaultSlot] = false;

            _dataVaultSlot = -1;
            _dataVaultCableStateReady = false;
        }

        private void PublishDataVaultCableState(float fixedDeltaTime, float peakTension)
        {
            if (!_dataVaultCableStateReady || _dataVaultSlot < 0)
                EnsureDataVaultCableState();

            if (!_dataVaultCableStateReady || _dataVaultSlot < 0)
                return;

            int pointOffset = _dataVaultSlot * DataVaultCablePointCount;
            int segmentOffset = _dataVaultSlot * DataVaultCableSegmentCount;
            if (pointOffset + DataVaultCablePointCount > _dataVaultCablePositions.Length ||
                pointOffset + DataVaultCablePointCount > _dataVaultCablePreviousPositions.Length ||
                pointOffset + DataVaultCablePointCount > _dataVaultCableVelocities.Length ||
                pointOffset + DataVaultCablePointCount > _dataVaultCableMasses.Length ||
                segmentOffset + DataVaultCableSegmentCount > _dataVaultCableSegmentTensions.Length)
            {
                return;
            }

            float invDt = math.rcp(math.max(fixedDeltaTime, 0.0001f));
            float playerMass = _playerRigidbody != null ? math.max(_playerRigidbody.mass, 0.0001f) : 1f;
            float payloadMass = _payloadBody != null ? math.max(_payloadBody.mass, 0.0001f) : 1f;
            float internalNodeMass = math.max(_reducedMass, 0.0001f);
            for (int i = 0; i < DataVaultCablePointCount; i++)
            {
                float3 localPosition = SampleCanonicalCablePoint(_verletPositions, i);
                float3 localPrevious = SampleCanonicalCablePoint(_verletPreviousPositions, i);
                float3 velocity = ClampCableVelocity((localPosition - localPrevious) * invDt, MaxCableVelocity);
                int targetIndex = pointOffset + i;
                _dataVaultCablePositions[targetIndex] = localPosition;
                _dataVaultCablePreviousPositions[targetIndex] = localPrevious;
                _dataVaultCableVelocities[targetIndex] = velocity;
                _dataVaultCableMasses[targetIndex] = i == 0
                    ? playerMass
                    : (i == DataVaultCablePointCount - 1 ? payloadMass : internalNodeMass);
            }

            for (int i = 0; i < DataVaultCableSegmentCount; i++)
            {
                float stretch = SampleCanonicalCableTension(i);
                _dataVaultCableSegmentTensions[segmentOffset + i] = math.max(0f, stretch * math.max(0f, _springStiffness));
            }

            if (_verletTensionForces.IsCreated && (uint)_dataVaultSlot < (uint)_verletTensionForces.Length)
            {
                float3 anchor = _verletSolverOrigin;
                float3 payload = _verletSolverOrigin + SampleCanonicalCablePoint(_verletPositions, DataVaultCablePointCount - 1);
                float3 delta = payload - anchor;
                float deltaLengthSq = math.lengthsq(delta);
                float3 direction = math.isfinite(deltaLengthSq) && deltaLengthSq > MinVectorMagnitudeSq
                    ? delta * math.rsqrt(deltaLengthSq)
                    : float3.zero;
                float safePeakTension = math.isfinite(peakTension) ? math.max(0f, peakTension) : 0f;
                _verletTensionForces[_dataVaultSlot] = new CableTensionForceDTO
                {
                    Force = direction * safePeakTension,
                    ApplicationPoint = anchor,
                    Tension = safePeakTension,
                    CableId = unchecked((int)EntityId.ToULong(GetEntityId()))
                };
            }

            if (!math.isfinite(peakTension))
                ClearDataVaultCableEntry();
        }

        private int ResolveTelemetryRingOffset()
        {
            return _dataVaultSlot >= 0 ? _dataVaultSlot * VerletTelemetryCapacity : 0;
        }

        private int ResolveTelemetryHeadIndex()
        {
            return _dataVaultSlot >= 0 ? _dataVaultSlot : 0;
        }

        private int ResolveTelemetryCapacity()
        {
            if (!_verletTelemetryRing.IsCreated)
                return 0;

            int offset = ResolveTelemetryRingOffset();
            if ((uint)offset >= (uint)_verletTelemetryRing.Length)
                return 0;

            return math.min(VerletTelemetryCapacity, _verletTelemetryRing.Length - offset);
        }

        private void ClearDataVaultTelemetrySlot()
        {
            if (!_verletTelemetryRing.IsCreated)
                return;

            int offset = ResolveTelemetryRingOffset();
            int capacity = ResolveTelemetryCapacity();
            for (int i = 0; i < capacity; i++)
                _verletTelemetryRing[offset + i] = default;
        }

        private void ClearDataVaultCableEntry()
        {
            if (_dataVaultSlot < 0 ||
                !_dataVaultCablePositions.IsCreated ||
                !_dataVaultCablePreviousPositions.IsCreated ||
                !_dataVaultCableVelocities.IsCreated ||
                !_dataVaultCableMasses.IsCreated ||
                !_dataVaultCableSegmentTensions.IsCreated)
            {
                return;
            }

            int pointOffset = _dataVaultSlot * DataVaultCablePointCount;
            int segmentOffset = _dataVaultSlot * DataVaultCableSegmentCount;
            if (pointOffset + DataVaultCablePointCount <= _dataVaultCablePositions.Length)
            {
                for (int i = 0; i < DataVaultCablePointCount; i++)
                {
                    int targetIndex = pointOffset + i;
                    _dataVaultCablePositions[targetIndex] = float3.zero;
                    _dataVaultCablePreviousPositions[targetIndex] = float3.zero;
                    _dataVaultCableVelocities[targetIndex] = float3.zero;
                    _dataVaultCableMasses[targetIndex] = 0f;
                }
            }

            if (segmentOffset + DataVaultCableSegmentCount <= _dataVaultCableSegmentTensions.Length)
            {
                for (int i = 0; i < DataVaultCableSegmentCount; i++)
                    _dataVaultCableSegmentTensions[segmentOffset + i] = 0f;
            }

            if (_verletTensionForces.IsCreated && (uint)_dataVaultSlot < (uint)_verletTensionForces.Length)
                _verletTensionForces[_dataVaultSlot] = default;
        }

        private float3 SampleCanonicalCablePoint(NativeArray<float3> source, int canonicalIndex)
        {
            if (!source.IsCreated || source.Length <= 0)
                return float3.zero;

            if (source.Length == 1)
                return SanitizeFinite(source[0]);

            float scaled = canonicalIndex * (source.Length - 1) * math.rcp(DataVaultCablePointCount - 1);
            int lower = math.clamp((int)math.floor(scaled), 0, source.Length - 1);
            int upper = math.min(lower + 1, source.Length - 1);
            float t = math.saturate(scaled - lower);
            return SanitizeFinite(math.lerp(source[lower], source[upper], t));
        }

        private float SampleCanonicalCableTension(int canonicalSegmentIndex)
        {
            if (!_verletSegmentTensions.IsCreated || _verletSegmentTensions.Length <= 0)
                return 0f;

            if (_verletSegmentTensions.Length == 1)
                return math.max(0f, _verletSegmentTensions[0]);

            float scaled = canonicalSegmentIndex * (_verletSegmentTensions.Length - 1) * math.rcp(DataVaultCableSegmentCount - 1);
            int lower = math.clamp((int)math.floor(scaled), 0, _verletSegmentTensions.Length - 1);
            int upper = math.min(lower + 1, _verletSegmentTensions.Length - 1);
            float t = math.saturate(scaled - lower);
            return math.max(0f, math.lerp(_verletSegmentTensions[lower], _verletSegmentTensions[upper], t));
        }

        private float RunVerletSolver(
            Vector3 anchorPosition,
            Vector3 payloadPosition,
            Vector3 payloadCurrentAcceleration,
            float fixedDeltaTime)
        {
            EnsureDataVaultCableState(_verletNodeCount > 1 ? _verletNodeCount : ResolveVerletPointCount(_qualityTier));

            if (!_verletRuntimeInitialized || !_verletPositions.IsCreated || _verletPositions.Length < 2)
                InitializeVerletRuntime(anchorPosition, payloadPosition);

            if (!_verletRuntimeInitialized)
            {
                SyncPrimaryConstraint(anchorPosition, payloadPosition);
                return ResolvePrimaryConstraintForceMagnitude();
            }

            float3 anchor = new float3(anchorPosition.x, anchorPosition.y, anchorPosition.z);
            float3 payload = new float3(payloadPosition.x, payloadPosition.y, payloadPosition.z);
            RebaseVerletSolverOrigin(SanitizeFinite(anchor));
            float3 payloadLocal = SanitizeFinite(payload - _verletSolverOrigin);
            int lastNodeIndex = _verletPositions.Length - 1;
            _verletPinnedPositions[0] = float3.zero;
            _verletPinnedPositions[lastNodeIndex] = payloadLocal;
            _verletPinnedMask[0] = 1;
            _verletPinnedMask[lastNodeIndex] = 1;

            VerletCableTuningDTO tuning = ResolveVerletTuning();
            ApplyVerletRestLengthTarget(math.max(_restLength, MinDistance), fixedDeltaTime, tuning.ReelSpeedMetersPerSecond);

            int iterationCount = ResolveVerletIterationCount(_qualityTier, tuning.ConstraintIterations);
            _lastVerletIterationCount = iterationCount;
            float dtSq = fixedDeltaTime * fixedDeltaTime;
            float3 defaultGravity = new float3(0f, -HectonPhysicsContract.GravityMetersPerSecondSquaredConst, 0f);
            float3 gravity = math.lengthsq(tuning.Gravity) > 0.000001f && math.all(math.isfinite(tuning.Gravity))
                ? tuning.Gravity
                : defaultGravity;
            float3 flowAcceleration = ToFloat3(ResolveVerletFlowAcceleration(payloadCurrentAcceleration));
            MockWorldSampler worldSampler = BuildVerletWorldSampler(payloadLocal, flowAcceleration);
            float velocityDamping = tuning.FluidFriction > 0f && math.isfinite(tuning.FluidFriction)
                ? math.saturate(tuning.FluidFriction)
                : ResolveVerletVelocityDamping(_qualityTier);
            var integrationJob = new TetherVerletIntegrationJob
            {
                Positions = _verletPositions,
                PreviousPositions = _verletPreviousPositions,
                Velocities = _verletVelocities,
                NodeFaultFlags = _verletNodeFaultFlags,
                PinnedPositions = _verletPinnedPositions,
                PinnedMask = _verletPinnedMask,
                Acceleration = gravity,
                DeltaTimeSq = dtSq,
                VelocityDamping = velocityDamping,
                MaxCableVelocity = MaxCableVelocity * fixedDeltaTime,
                FloorY = VerletFloorY,
                NodeRadius = VerletNodeRadius,
                WorldSampler = worldSampler,
                RockFriction01 = tuning.RockFriction01 > 0f && math.isfinite(tuning.RockFriction01)
                    ? math.saturate(tuning.RockFriction01)
                    : VerletRockFriction01,
                WorldSamplerEnabled = 1
            };

            integrationJob.Run(_verletPositions.Length);
            var constraintJob = new VerletCableSolverJob
            {
                Positions = _verletPositions,
                Corrections = _verletCorrections,
                CorrectionWeights = _verletCorrectionWeights,
                SegmentTensions = _verletSegmentTensions,
                SolverStats = _verletSolverStats,
                SolverFlags = _verletSolverFlags,
                SegmentRestLengths = _verletSegmentRestLengths,
                PinnedPositions = _verletPinnedPositions,
                PinnedMask = _verletPinnedMask,
                NodeFaultFlags = _verletNodeFaultFlags,
                NodeCount = _verletPositions.Length,
                IterationCount = iterationCount,
                FloorY = VerletFloorY,
                NodeRadius = VerletNodeRadius
            };
            constraintJob.Run();
            ApplyVerletPlasticDeformation(
                _verletSolverStats.IsCreated && _verletSolverStats.Length > 0 ? _verletSolverStats[0] : 0f,
                tuning.StretchThreshold01);
            float peakTension = _verletSolverStats.IsCreated && _verletSolverStats.Length > 0
                ? _verletSolverStats[0] * math.max(0f, _springStiffness)
                : 0f;
            var telemetryJob = new TetherVerletTelemetryJob
            {
                TelemetryRing = _verletTelemetryRing,
                TelemetryHead = _verletTelemetryHead,
                SolverStats = _verletSolverStats,
                SolverFlags = _verletSolverFlags,
                FrameIndex = unchecked((uint)_currentSimulationFrameIndex),
                NodeCount = _verletPositions.Length,
                IterationCount = iterationCount,
                PeakCableTension = peakTension,
                AnchorPosition = anchor,
                PayloadPosition = payload,
                Flags = 0u,
                TelemetryOffset = ResolveTelemetryRingOffset(),
                TelemetryCapacity = ResolveTelemetryCapacity(),
                TelemetryHeadOffset = ResolveTelemetryHeadIndex()
            };
            telemetryJob.Run();

            _lastVerletPeakDelta = _verletSolverStats.IsCreated && _verletSolverStats.Length > 0 ? _verletSolverStats[0] : 0f;
            if (_verletSolverFlags.IsCreated && _verletSolverFlags.Length > 0 && _verletSolverFlags[0] != TetherVerletFaultFlags.None)
                DumpVerletTelemetryOnce((uint)_verletSolverFlags[0]);
            _primaryConstraintForceMagnitude = peakTension;
            PublishTetherTensionSignal(anchorPosition, payloadPosition, peakTension);
            PublishDataVaultCableState(fixedDeltaTime, peakTension);
            ApplyVerletEndpointForces(anchorPosition, payloadPosition, peakTension);
            EmitTensionCreakIfNeeded(anchorPosition, payloadPosition, peakTension);
            return peakTension;
        }

        private void ApplyVerletRestLengthTarget(float targetCableLength, float fixedDeltaTime, float reelSpeedMetersPerSecond)
        {
            if (!_verletSegmentRestLengths.IsCreated || _verletSegmentRestLengths.Length == 0)
                return;

            int segmentCount = _verletSegmentRestLengths.Length;
            float targetSegmentRestLength = math.max(
                VerletMinSpooledSegmentLength,
                targetCableLength * math.rcp(math.max(1, segmentCount)));
            float safeDt = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;
            float safeReelSpeed = math.isfinite(reelSpeedMetersPerSecond) && reelSpeedMetersPerSecond > 0f
                ? reelSpeedMetersPerSecond
                : VerletReelSpeedMetersPerSecond;
            float maxSegmentDelta = safeReelSpeed * safeDt * math.rcp(math.max(1, segmentCount));
            if (maxSegmentDelta <= 0f || !math.isfinite(maxSegmentDelta))
                maxSegmentDelta = targetSegmentRestLength;

            for (int i = 0; i < segmentCount; i++)
            {
                float current = _verletSegmentRestLengths[i];
                if (!math.isfinite(current) || current <= 0f)
                {
                    _verletSegmentRestLengths[i] = targetSegmentRestLength;
                    continue;
                }

                float delta = targetSegmentRestLength - current;
                if (math.abs(delta) <= maxSegmentDelta)
                    _verletSegmentRestLengths[i] = targetSegmentRestLength;
                else
                    _verletSegmentRestLengths[i] = current + math.sign(delta) * maxSegmentDelta;
            }
        }

        private void ApplyVerletPlasticDeformation(float peakConstraintDelta, float stretchThreshold01)
        {
            if (!_verletSegmentRestLengths.IsCreated ||
                _verletSegmentRestLengths.Length == 0 ||
                !math.isfinite(peakConstraintDelta) ||
                peakConstraintDelta <= 0f)
            {
                return;
            }

            float restTotal = 0f;
            for (int i = 0; i < _verletSegmentRestLengths.Length; i++)
            {
                float restLength = _verletSegmentRestLengths[i];
                restTotal += math.isfinite(restLength) ? math.max(0f, restLength) : 0f;
            }

            float averageRest = restTotal * math.rcp(math.max(1, _verletSegmentRestLengths.Length));
            if (averageRest <= MinDistance)
                return;

            float stretch01 = peakConstraintDelta * math.rcp(averageRest);
            float threshold = math.isfinite(stretchThreshold01) && stretchThreshold01 > 0f
                ? stretchThreshold01
                : VerletPlasticStretch01;
            if (stretch01 <= threshold)
                return;

            float creepMeters = (stretch01 - threshold) * averageRest * VerletPlasticCreep01;
            creepMeters = math.min(creepMeters, averageRest * 0.025f);
            if (creepMeters <= 0f || !math.isfinite(creepMeters))
                return;

            for (int i = 0; i < _verletSegmentRestLengths.Length; i++)
                _verletSegmentRestLengths[i] = math.max(VerletMinSpooledSegmentLength, _verletSegmentRestLengths[i] + creepMeters);
        }

        private static MockWorldSampler BuildVerletWorldSampler(float3 payloadLocal, float3 flowAcceleration)
        {
            float lengthSq = math.lengthsq(payloadLocal);
            float obstacleRadius = math.isfinite(lengthSq) && lengthSq > 144f
                ? math.min(0.65f, math.sqrt(lengthSq) * 0.025f)
                : 0f;
            float3 side = ResolveCablePerpendicular(payloadLocal);
            return new MockWorldSampler
            {
                Sdf = new MockSDFSampler
                {
                    SphereCenter = payloadLocal * 0.52f + side * (obstacleRadius * 0.75f),
                    SphereRadius = obstacleRadius,
                    SecondarySphereCenter = payloadLocal * 0.74f - side * (obstacleRadius * 0.55f),
                    SecondarySphereRadius = obstacleRadius * 0.72f,
                    PlaneY = VerletFloorY,
                    Padding0 = 0f,
                    Padding1 = 0f,
                    Padding2 = 0f
                },
                FlowVelocity = flowAcceleration,
                FlowAccelerationScale = 1f
            };
        }

        private static float3 ResolveCablePerpendicular(float3 axis)
        {
            float3 safeAxis = math.lengthsq(axis) > MinVectorMagnitudeSq
                ? math.normalize(axis)
                : new float3(0f, 0f, 1f);
            float3 side = math.cross(safeAxis, new float3(0f, 1f, 0f));
            float sideLengthSq = math.lengthsq(side);
            if (!math.isfinite(sideLengthSq) || sideLengthSq <= MinVectorMagnitudeSq)
                side = math.cross(safeAxis, new float3(1f, 0f, 0f));

            sideLengthSq = math.lengthsq(side);
            return math.isfinite(sideLengthSq) && sideLengthSq > MinVectorMagnitudeSq
                ? side * math.rsqrt(sideLengthSq)
                : new float3(1f, 0f, 0f);
        }

        private void DumpVerletTelemetryOnce(uint reasonFlags)
        {
            if (_verletFaultDumpedThisActivation || !_verletTelemetryRing.IsCreated || !_verletTelemetryHead.IsCreated)
                return;

            int capacity = ResolveTelemetryCapacity();
            int telemetryOffset = ResolveTelemetryRingOffset();
            int telemetryHeadIndex = ResolveTelemetryHeadIndex();
            if (capacity <= 0 ||
                (uint)telemetryOffset >= (uint)_verletTelemetryRing.Length ||
                (uint)telemetryHeadIndex >= (uint)_verletTelemetryHead.Length)
            {
                return;
            }

            _verletFaultDumpedThisActivation = true;
            try
            {
                DirectoryInfo projectRootInfo = Directory.GetParent(Application.dataPath);
                if (projectRootInfo == null)
                    return;

                string legacyDumpPath = Path.Combine(
                    projectRootInfo.FullName,
                    TetherTelemetryDumpRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string h8DumpPath = Path.Combine(
                    projectRootInfo.FullName,
                    TetherTelemetryH8DumpRelativePath.Replace('/', Path.DirectorySeparatorChar));

                int head = _verletTelemetryHead[telemetryHeadIndex];
                if (head < 0 || head >= capacity)
                    head = 0;

                NativeArray<TetherVerletTelemetryEntry> telemetrySlice =
                    _verletTelemetryRing.GetSubArray(telemetryOffset, capacity);
                TetherBlackBoxDumpWriter.WritePrimaryAndLegacy(
                    h8DumpPath,
                    legacyDumpPath,
                    TetherTelemetryDumpMagic,
                    telemetrySlice,
                    head,
                    reasonFlags);
            }
            catch
            {
                // Fault-path export must never trigger a second gameplay failure.
            }
        }
        private Vector3 ResolveVerletFlowAcceleration(Vector3 payloadCurrentAcceleration)
        {
            Vector3 resolved = payloadCurrentAcceleration * 0.18f;
            HectonMapMagicVegetationBridge vegetationBridge = _manager != null ? _manager.CachedVegetationBridge : null;
            if (vegetationBridge != null && _payloadBody != null &&
                vegetationBridge.TrySampleAbyssalFlow(_payloadBody.worldCenterOfMass, out Vector3 vegetationFlow))
            {
                resolved += vegetationFlow * 0.16f;
            }
            else
            {
                HectonFluidEngine fluidEngine = _manager != null ? _manager.CachedFluidEngine : null;
                if (fluidEngine != null && _payloadBody != null &&
                fluidEngine.TrySampleModAbyssalFlow(_payloadBody.worldCenterOfMass, out float3 flowVector))
                {
                    resolved += new Vector3(flowVector.x, flowVector.y, flowVector.z) * 0.12f;
                }
                else
                {
                    IWeatherService weather = _manager != null ? _manager.CachedWeatherService : null;
                    if (weather != null && weather.IsInitialized)
                    {
                        WeatherRuntimeSnapshot snapshot = weather.GetRuntimeSnapshot();
                        float3 current = snapshot.CurrentMeta.GlobalBaseVector * math.max(0f, snapshot.CurrentMeta.GlobalScale);
                        resolved += new Vector3(current.x, current.y, current.z) * 0.08f;
                    }
                }
            }

            if (!IsFinite(resolved))
                return Vector3.zero;

            float maxAcceleration = math.max(0f, _maxPayloadCurrentForce) * 0.15f;
            return ClampVector(resolved, maxAcceleration);
        }

        private void EmitTensionCreakIfNeeded(Vector3 anchorPosition, Vector3 payloadPosition, float peakTension)
        {
            if (!math.isfinite(peakTension) || peakTension <= 0f)
                return;

            float snapThreshold = ResolveSnapTensionThreshold();
            float safeMargin = math.max(1f, snapThreshold * TensionCreakSafeMargin01);
            if (peakTension <= safeMargin)
                return;

            int frame = _currentSimulationFrameIndex;
            if (IsFrameCooldownActive(frame, _lastTensionCreakFrame, TensionCreakCooldownFrames))
                return;

            Vector3 safeAnchorPosition = IsFinite(anchorPosition) ? anchorPosition : Vector3.zero;
            Vector3 safePayloadPosition = IsFinite(payloadPosition) ? payloadPosition : safeAnchorPosition;
            Vector3 midpoint = (safeAnchorPosition + safePayloadPosition) * 0.5f;
            if (!IsFinite(midpoint))
                return;

            float intensity = math.saturate((peakTension - safeMargin) * math.rcp(math.max(1f, snapThreshold - safeMargin)));
            ImpactSignal signal = new ImpactSignal
            {
                PointAup = AbsoluteUniversePosition.FromRuntimePosition(midpoint),
                Force = peakTension,
                Intensity = intensity,
                MaterialHash = TetherCreakMaterialHash,
                WeightClass = 2,
                PrimaryMaterialId = 7,
                SecondaryMaterialId = 0,
                Flags = (byte)(peakTension >= snapThreshold * ReactiveVfxThreshold01 ? 3 : 1)
            };
            GlobalSignals.Publish(in signal);
            _lastTensionCreakFrame = frame;
        }

        private void PublishTetherTensionSignal(Vector3 anchorPosition, Vector3 payloadPosition, float peakTension)
        {
            if (peakTension <= 0f || !math.isfinite(peakTension))
                return;

            float snapThreshold = ResolveSnapTensionThreshold();
            Vector3 safeAnchorPosition = IsFinite(anchorPosition) ? anchorPosition : Vector3.zero;
            Vector3 safePayloadPosition = IsFinite(payloadPosition) ? payloadPosition : safeAnchorPosition;
            Vector3 direction = ResolveSafeDirection(safePayloadPosition - safeAnchorPosition, Vector3.zero);
            float reactiveVfx01 = math.saturate(
                (peakTension - snapThreshold * ReactiveVfxThreshold01) *
                math.rcp(math.max(1f, snapThreshold * (1f - ReactiveVfxThreshold01))));
            TetherTensionSignal signal = new TetherTensionSignal
            {
                AnchorAup = AbsoluteUniversePosition.FromRuntimePosition(safeAnchorPosition),
                PayloadAup = AbsoluteUniversePosition.FromRuntimePosition(safePayloadPosition),
                DirectionToPayload = ToFloat3(direction),
                TetherId = unchecked((uint)EntityId.ToULong(GetEntityId())),
                FrameIndex = unchecked((uint)_currentSimulationFrameIndex),
                TensionForce = peakTension,
                SnapThreshold = snapThreshold,
                Tension01 = math.saturate(peakTension * math.rcp(math.max(1f, snapThreshold))),
                ReactiveVfx01 = reactiveVfx01,
                NodeCount = (ushort)math.clamp(_verletNodeCount, 0, ushort.MaxValue),
                Flags = (byte)(reactiveVfx01 > 0f ? 1 : 0),
                Reserved = 0
            };
            TetherSignals.PublishTension(in signal);
        }

        private void PublishSnapImpactSignal(Vector3 snapPosition, float peakTension, float snapSeverity)
        {
            if (!IsFinite(snapPosition))
                snapPosition = IsFinite(transform.position) ? transform.position : Vector3.zero;

            float safePeakTension = math.isfinite(peakTension) ? math.max(0f, peakTension) : 0f;
            float safeSnapSeverity = math.isfinite(snapSeverity) ? math.saturate(snapSeverity) : 0f;

            ImpactSignal signal = new ImpactSignal
            {
                PointAup = AbsoluteUniversePosition.FromRuntimePosition(snapPosition),
                Force = safePeakTension,
                Intensity = safeSnapSeverity,
                MaterialHash = TetherSnapImpactMaterialHash,
                WeightClass = 2,
                PrimaryMaterialId = 7,
                SecondaryMaterialId = 0,
                Flags = 2
            };
            GlobalSignals.Publish(in signal);
        }

        private void ApplyVerletEndpointForces(Vector3 anchorPosition, Vector3 payloadPosition, float peakTension)
        {
            if (_payloadBody == null || _playerRigidbody == null || peakTension <= 0f || !math.isfinite(peakTension))
                return;

            Vector3 separation = payloadPosition - anchorPosition;
            float distanceSq = separation.sqrMagnitude;
            if (!math.isfinite(distanceSq) || distanceSq <= MinVectorMagnitudeSq)
                return;

            Vector3 direction = separation * math.rsqrt(math.max(distanceSq, MinVectorMagnitudeSq));
            float rawPlayerMass = _playerRigidbody != null ? _playerRigidbody.mass : 1f;
            float rawPayloadMass = _payloadBody != null ? _payloadBody.mass : 1f;
            float playerMass = math.isfinite(rawPlayerMass) ? math.max(rawPlayerMass, 0.0001f) : 1f;
            float payloadMass = math.isfinite(rawPayloadMass) ? math.max(rawPayloadMass, 0.0001f) : 1f;
            float massRatioScale = playerMass * math.rcp(math.max(playerMass + payloadMass, 0.0001f));
            float maxCableAcceleration = math.isfinite(_maxCableAcceleration) ? math.max(0f, _maxCableAcceleration) : 0f;
            float maxPayloadForce = maxCableAcceleration * payloadMass;
            float scaledForce = math.min(peakTension * massRatioScale, maxPayloadForce);
            if (scaledForce <= MinDistance || !math.isfinite(scaledForce))
                return;

            Vector3 payloadForce = -direction * scaledForce;
            PhysicsForceRouter.QueueForceAtPosition(
                _payloadBody,
                payloadForce,
                payloadPosition,
                ForceMode.Force);

            if (!_playerRigidbody.isKinematic)
            {
                Vector3 reaction = -payloadForce;
                PhysicsForceRouter.QueueForceAtPosition(
                    _playerRigidbody,
                    reaction,
                    anchorPosition,
                    ForceMode.Force);
            }
        }

        private void UpdateVerletVisualUpload(HectonQualityTier qualityTier, Plane[] frustumPlanes)
        {
            if (!_visualSegmentPositions.IsCreated || _visualSegmentPositions.Length != _verletPositions.Length)
                return;

            bool useStraightLineFake = ShouldUseLowTierTautLineVisualFake(qualityTier);
            float3 start = _verletPositions[0];
            float3 end = _verletPositions[_verletPositions.Length - 1];
            float invLast = math.rcp(math.max(1, _visualSegmentPositions.Length - 1));
            for (int i = 0; i < _visualSegmentPositions.Length; i++)
            {
                float3 localPoint = useStraightLineFake
                    ? math.lerp(start, end, i * invLast)
                    : _verletPositions[i];
                _visualSegmentPositions[i] = SanitizeFinite(localPoint + _verletSolverOrigin);
            }

            Vector3 minBounds = new Vector3(_visualSegmentPositions[0].x, _visualSegmentPositions[0].y, _visualSegmentPositions[0].z);
            Vector3 maxBounds = minBounds;
            for (int i = 1; i < _visualSegmentPositions.Length; i++)
            {
                float3 point = _visualSegmentPositions[i];
                Vector3 pointV3 = new Vector3(point.x, point.y, point.z);
                minBounds = Vector3.Min(minBounds, pointV3);
                maxBounds = Vector3.Max(maxBounds, pointV3);
            }

            _visualBounds.SetMinMax(minBounds, maxBounds);
            if (ShouldUploadVisualBounds(frustumPlanes))
                UploadVisualGpuBuffers(includeTension: true);
            else
                _visualCulledThisFrame = true;
        }

        private bool ShouldUploadVisualBounds(Plane[] frustumPlanes)
        {
            if (frustumPlanes == null || frustumPlanes.Length < 6)
                return true;

            return GeometryUtility.TestPlanesAABB(frustumPlanes, _visualBounds);
        }

        private bool ShouldUseLowTierTautLineVisualFake(HectonQualityTier qualityTier)
        {
            HectonQualityTier tier = TetherManager.SanitizeQualityTier(qualityTier);
            bool lowTier = tier == HectonQualityTier.Unknown ||
                           tier == HectonQualityTier.Low ||
                           tier == HectonQualityTier.Mx350;
            return lowTier && math.max(_tension01, _stress01) >= LowTierTautLineVisualThreshold01;
        }

        private static int ResolveVerletIterationCount(HectonQualityTier qualityTier, int tuningOverride)
        {
            if (tuningOverride > 0)
                return math.clamp(tuningOverride, VerletLowIterationCount, VerletUltraIterationCount);

            switch (TetherManager.SanitizeQualityTier(qualityTier))
            {
                case HectonQualityTier.Low:
                case HectonQualityTier.Mx350:
                case HectonQualityTier.Unknown:
                    return VerletLowIterationCount;
                case HectonQualityTier.Mid:
                    return VerletMidIterationCount;
                case HectonQualityTier.High:
                    return VerletHighIterationCount;
                case HectonQualityTier.Ultra:
                    return VerletUltraIterationCount;
                default:
                    return VerletLowIterationCount;
            }
        }

        private static int ResolveVerletPointCount(HectonQualityTier qualityTier)
        {
            return ResolveVerletSegmentCount(qualityTier) + 1;
        }

        private static int ResolveVerletSegmentCount(HectonQualityTier qualityTier)
        {
            switch (TetherManager.SanitizeQualityTier(qualityTier))
            {
                case HectonQualityTier.Low:
                case HectonQualityTier.Mx350:
                case HectonQualityTier.Unknown:
                    return VerletLowSegmentCount;
                case HectonQualityTier.Mid:
                case HectonQualityTier.High:
                case HectonQualityTier.Ultra:
                    return VerletDefaultSegmentCount;
                default:
                    return VerletLowSegmentCount;
            }
        }

        private static float ResolveVerletVelocityDamping(HectonQualityTier qualityTier)
        {
            switch (TetherManager.SanitizeQualityTier(qualityTier))
            {
                case HectonQualityTier.Low:
                case HectonQualityTier.Mx350:
                case HectonQualityTier.Unknown:
                    return VerletLowVelocityDamping;
                case HectonQualityTier.Mid:
                    return VerletMidVelocityDamping;
                case HectonQualityTier.High:
                case HectonQualityTier.Ultra:
                    return VerletHighVelocityDamping;
                default:
                    return VerletLowVelocityDamping;
            }
        }

        private void RecalculateDampingCoefficient()
        {
            float playerMass = _playerRigidbody != null ? math.max(_playerRigidbody.mass, 0.0001f) : 1f;
            float payloadMass = _payloadBody == null || _payloadBody.isKinematic || _kinematicAnchorCompensationEnabled
                ? float.PositiveInfinity
                : math.max(_payloadBody.mass, 0.0001f);

            _reducedMass = HectonContactJob.ResolveReducedMass(
                playerMass,
                payloadMass,
                float.IsInfinity(payloadMass));

            float requestedMultiplier = ResolveTowOverDampingMultiplier();
            float overDampingMultiplier = _tetherClass == TetherClass.TowCable
                ? math.max(TowCableOverDampingMinimum, requestedMultiplier)
                : math.max(1f, requestedMultiplier);
            _dampingCoefficient = HectonContactJob.ResolveCriticalDamping(
                _springStiffness,
                _reducedMass,
                overDampingMultiplier);
            RefreshPrimaryConstraintDrive();
        }

        private void AdvanceExternalCableSnare(float fixedDeltaTime)
        {
            float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;
            if (_bioCableRequestedThisStep)
            {
                if (_bioCableRequestedTension01 > 0f)
                    _bioCableHoldTimer = _bioCableHoldTime;
            }
            else if (_bioCableHoldTimer > 0f)
            {
                _bioCableHoldTimer -= safeFixedDeltaTime;
                if (_bioCableHoldTimer < 0f)
                    _bioCableHoldTimer = 0f;
            }

            bool keepAlive = _bioCableRequestedThisStep || _bioCableHoldTimer > 0f;
            float targetTension = keepAlive && math.isfinite(_bioCableRequestedTension01) ? math.saturate(_bioCableRequestedTension01) : 0f;
            float targetCutProgress = keepAlive && math.isfinite(_bioCableRequestedCutProgress01) ? math.saturate(_bioCableRequestedCutProgress01) : 1f;
            Vector3 targetAnchor = keepAlive && IsFinite(_bioCableRequestedAnchorWS) ? _bioCableRequestedAnchorWS : Vector3.zero;
            float blendSharpness = math.isfinite(_bioCableBlendSharpness) ? math.max(1f, _bioCableBlendSharpness) : 1f;
            float blendT = ResolveBlendFactor(blendSharpness, safeFixedDeltaTime);

            float currentTension = math.isfinite(_bioCableCurrentTension01) ? math.saturate(_bioCableCurrentTension01) : 0f;
            float currentCutProgress = math.isfinite(_bioCableCurrentCutProgress01) ? math.saturate(_bioCableCurrentCutProgress01) : 1f;
            Vector3 currentAnchor = IsFinite(_bioCableCurrentAnchorWS) ? _bioCableCurrentAnchorWS : targetAnchor;
            _bioCableCurrentTension01 = math.saturate(math.lerp(currentTension, targetTension, blendT));
            _bioCableCurrentCutProgress01 = math.saturate(math.lerp(currentCutProgress, targetCutProgress, blendT));
            Vector3 blendedAnchor = Vector3.Lerp(currentAnchor, targetAnchor, blendT);
            _bioCableCurrentAnchorWS = IsFinite(blendedAnchor) ? blendedAnchor : targetAnchor;

            _bioCableRequestedTension01 = 0f;
            _bioCableRequestedCutProgress01 = 1f;
            _bioCableRequestedAnchorWS = Vector3.zero;
            _bioCableRequestedThisStep = false;
        }

        private Vector3 ComputePayloadCurrentForce(Vector3 anchorPosition, Vector3 payloadPosition, float fixedStepClockSeconds)
        {
            if (_payloadBody == null)
                return Vector3.zero;

            Vector3 safePayloadPosition = IsFinite(payloadPosition) ? payloadPosition : (_payloadBody != null && IsFinite(_payloadBody.worldCenterOfMass) ? _payloadBody.worldCenterOfMass : Vector3.zero);
            float time = math.isfinite(fixedStepClockSeconds) ? fixedStepClockSeconds : 0f;
            float3 phantomCurrentSample = CurrentManager.SampleCurrent(
                new float3(safePayloadPosition.x, safePayloadPosition.y, safePayloadPosition.z),
                time,
                _payloadCurrentNoiseScale,
                _payloadCurrentTimeScale,
                _payloadCurrentStrength,
                _payloadCurrentVerticalFactor);
            Vector3 phantomCurrent = new Vector3(phantomCurrentSample.x, phantomCurrentSample.y, phantomCurrentSample.z);
            if (!IsFinite(phantomCurrent))
                phantomCurrent = Vector3.zero;

            Vector3 authoredCurrent = CurrentVolume.SampleAt(safePayloadPosition);
            if (!IsFinite(authoredCurrent))
                authoredCurrent = Vector3.zero;

            Vector3 environmentCurrent = phantomCurrent + authoredCurrent;
            float verticalFactor = math.isfinite(_payloadCurrentVerticalFactor) ? _payloadCurrentVerticalFactor : 0f;
            environmentCurrent.y *= verticalFactor;
            if (!IsFinite(environmentCurrent))
                environmentCurrent = Vector3.zero;

            Vector3 payloadVelocity = IsFinite(_payloadBody.linearVelocity) ? _payloadBody.linearVelocity : Vector3.zero;
            Vector3 currentDelta = environmentCurrent - payloadVelocity;
            float currentDeltaSq = currentDelta.sqrMagnitude;
            Vector3 playerRight = _owner != null ? _owner.PlayerRight : Vector3.right;
            float sideExposure = 0f;
            if (math.isfinite(currentDeltaSq) && currentDeltaSq > MinVectorMagnitudeSq)
                sideExposure = math.abs(Vector3.Dot(ResolveSafeDirection(currentDelta, Vector3.zero), playerRight));

            float safePayloadMass01 = math.isfinite(_payloadMass01) ? math.saturate(_payloadMass01) : 0f;
            float safeSideBoost = math.isfinite(_payloadSideCurrentBoost) ? math.max(0f, _payloadSideCurrentBoost) : 1f;
            float safeDamping = math.isfinite(_payloadCurrentDamping) ? math.max(0f, _payloadCurrentDamping) : 0f;
            float currentScale = math.lerp(0.55f, 1f, safePayloadMass01);
            currentScale *= math.lerp(1f, safeSideBoost, math.isfinite(sideExposure) ? math.saturate(sideExposure) : 0f);
            Vector3 currentForce = currentDelta * (safeDamping * currentScale);
            float maxPayloadCurrentForce = math.isfinite(_maxPayloadCurrentForce) ? math.max(0f, _maxPayloadCurrentForce) : 0f;
            float maxPayloadCurrentForceSq = maxPayloadCurrentForce * maxPayloadCurrentForce;
            float currentForceSq = currentForce.sqrMagnitude;
            if (!math.isfinite(currentForceSq))
                currentForce = Vector3.zero;
            else if (currentForceSq > maxPayloadCurrentForceSq)
                currentForce *= maxPayloadCurrentForce * math.rsqrt(math.max(currentForceSq, MinVectorMagnitudeSq));

            float maxCableAcceleration = math.isfinite(_maxCableAcceleration) ? math.max(1f, _maxCableAcceleration) : 1f;
            _payloadDrift01 = math.saturate(ResolveMagnitude(currentDeltaSq) * math.rcp(maxCableAcceleration));
            return currentForce;
        }

        private void ApplyPayloadCurrentForce(Vector3 payloadCurrentForce, float fixedDeltaTime)
        {
            if (_payloadBody == null)
                return;

            float payloadCurrentForceSq = payloadCurrentForce.sqrMagnitude;
            if (math.isfinite(payloadCurrentForceSq) && payloadCurrentForceSq > MinVectorMagnitudeSq)
                ApplyClampedAcceleration(_payloadBody, payloadCurrentForce, _maxPayloadCurrentForce);

            float payloadAngularDamping = math.isfinite(_payloadAngularDamping) ? math.max(0f, _payloadAngularDamping) : 0f;
            if (payloadAngularDamping > 0f)
            {
                Vector3 angularVelocity = _payloadBody.angularVelocity;
                if (!IsFinite(angularVelocity))
                    angularVelocity = Vector3.zero;

                float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;
                float angularBlend = math.rcp(1f + payloadAngularDamping * safeFixedDeltaTime);
                angularVelocity *= angularBlend;
                float maxPayloadAngularSpeed = math.isfinite(_maxPayloadAngularSpeed) ? math.max(0f, _maxPayloadAngularSpeed) : 0f;
                float maxPayloadAngularSpeedSq = maxPayloadAngularSpeed * maxPayloadAngularSpeed;
                float angularSpeedSq = angularVelocity.sqrMagnitude;
                if (!math.isfinite(angularSpeedSq))
                    angularVelocity = Vector3.zero;
                else if (angularSpeedSq > maxPayloadAngularSpeedSq)
                    angularVelocity *= maxPayloadAngularSpeed * math.rsqrt(math.max(angularSpeedSq, MinVectorMagnitudeSq));

                _payloadBody.angularVelocity = IsFinite(angularVelocity) ? angularVelocity : Vector3.zero;
            }
        }

        private float ApplyExternalCableSnareForce()
        {
            if (_payloadBody == null || !math.isfinite(_bioCableCurrentTension01) || _bioCableCurrentTension01 <= MinDistance)
                return 0f;

            float cutSuppression = 1f - (math.isfinite(_bioCableCurrentCutProgress01) ? math.saturate(_bioCableCurrentCutProgress01) : 1f);
            float effectiveTension = math.saturate(_bioCableCurrentTension01) * cutSuppression;
            if (effectiveTension <= MinDistance)
                return 0f;

            Vector3 payloadCenter = IsFinite(_payloadBody.worldCenterOfMass) ? _payloadBody.worldCenterOfMass : Vector3.zero;
            Vector3 safeBioAnchor = IsFinite(_bioCableCurrentAnchorWS) ? _bioCableCurrentAnchorWS : payloadCenter;
            Vector3 toAnchor = safeBioAnchor - payloadCenter;
            float toAnchorSq = toAnchor.sqrMagnitude;
            float safePullForce = math.isfinite(_bioCablePayloadPullForce) ? math.max(0f, _bioCablePayloadPullForce) : 0f;
            if (math.isfinite(toAnchorSq) && toAnchorSq > MinVectorMagnitudeSq && safePullForce > 0f)
            {
                Vector3 snareForce = ResolveSafeDirection(toAnchor, Vector3.zero) * (safePullForce * effectiveTension);
                ApplyClampedAcceleration(_payloadBody, snareForce, safePullForce);
            }

            float stressBuildMultiplier = math.isfinite(_bioCableStressBuildMultiplier) ? math.max(1f, _bioCableStressBuildMultiplier) : 1f;
            return safePullForce * effectiveTension * stressBuildMultiplier;
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

            bool directBlocked = TryFindClosestObstacle(
                anchorPosition,
                payloadPosition,
                out Vector3 firstHitPoint,
                out Vector3 firstHitNormal,
                out HectonVoxelVolume firstHitVolume,
                out int firstHitRuntimeStamp);
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

            RecalculateBendPoints(anchorPosition, payloadPosition, firstHitPoint, firstHitNormal, firstHitVolume, firstHitRuntimeStamp);
            _losBlocked = _bendPointCount > 0;
            _losCheckCooldownFrames = BendRecheckCooldownFrames;
        }

        private void RecalculateBendPoints(
            Vector3 anchorPosition,
            Vector3 payloadPosition,
            Vector3 firstHitPoint,
            Vector3 firstHitNormal,
            HectonVoxelVolume firstHitVolume,
            int firstHitRuntimeStamp)
        {
            int previousCount = _bendPointCount;
            _bendPointCount = 0;
            Vector3 origin = anchorPosition;
            Vector3 target = payloadPosition;
            Vector3 initialHitPoint = firstHitPoint;
            Vector3 initialHitNormal = firstHitNormal;
            HectonVoxelVolume initialHitVolume = firstHitVolume;
            int initialHitRuntimeStamp = firstHitRuntimeStamp;

            for (int bendIndex = 0; bendIndex < _maxBendPoints && bendIndex < MaxSupportedBendPoints; bendIndex++)
            {
                Vector3 hitPoint;
                Vector3 hitNormal;
                HectonVoxelVolume hitVolume;
                int hitRuntimeStamp;
                if (bendIndex == 0)
                {
                    hitPoint = initialHitPoint;
                    hitNormal = initialHitNormal;
                    hitVolume = initialHitVolume;
                    hitRuntimeStamp = initialHitRuntimeStamp;
                }
                else if (!TryFindClosestObstacle(origin, target, out hitPoint, out hitNormal, out hitVolume, out hitRuntimeStamp))
                {
                    break;
                }

                if (!TryResolveBendCorner(
                        hitPoint,
                        hitNormal,
                        hitVolume,
                        hitRuntimeStamp,
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
                origin = bendPoint + ResolveSafeDirection(target - origin, Vector3.zero) * _bendPointClearanceRadius;
            }

            if (previousCount != _bendPointCount)
                _segmentRestLengthsDirty = true;

            ClearBendMetadata(_bendPointCount);
        }

        private bool TryResolveBendCorner(
            Vector3 hitPoint,
            Vector3 hitNormal,
            HectonVoxelVolume hitVolume,
            int hitRuntimeStamp,
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

            if (!IsFinite(hitPoint))
                return false;

            bendNormal = ResolveSafeDirection(hitNormal, Vector3.up);
            if (hitVolume != null &&
                hitVolume.MatchesRuntimeStamp(hitRuntimeStamp) &&
                hitVolume.TryResolveNearestVoxelCorner(hitPoint, bendNormal, out Vector3 firstCornerWorld))
            {
                bendPoint = firstCornerWorld + bendNormal * math.max(0.01f, _bendSurfaceOffset);
                bendVolume = hitVolume;
                bendRuntimeStamp = hitVolume.RuntimeStamp;
                return IsFinite(bendPoint);
            }

            if (TryResolveCachedVoxelCorner(
                    hitPoint,
                    bendNormal,
                    out bendPoint,
                    out bendVolume,
                    out bendRuntimeStamp))
            {
                return true;
            }

            if (TryResolvePublishedVoxelCorner(
                    hitPoint,
                    lineDirection,
                    bendNormal,
                    out bendPoint,
                    out bendNormal,
                    out bendVolume,
                    out bendRuntimeStamp))
            {
                return true;
            }

            Vector3 tangent = Vector3.ProjectOnPlane(lineDirection, bendNormal);
            if (tangent.sqrMagnitude > MinVectorMagnitudeSq)
            {
                tangent = ResolveSafeDirection(tangent, Vector3.zero);
            }
            else
            {
                tangent = Vector3.Cross(bendNormal, Vector3.up);
                if (tangent.sqrMagnitude <= MinVectorMagnitudeSq)
                    tangent = Vector3.Cross(bendNormal, Vector3.right);
                tangent = ResolveSafeDirection(tangent, Vector3.right);
            }

            bendPoint = hitPoint + bendNormal * math.max(0.01f, _bendSurfaceOffset) + tangent * math.max(0.01f, _bendPointClearanceRadius);
            return IsFinite(bendPoint);
        }

        private bool TryResolveCachedVoxelCorner(
            Vector3 hitPoint,
            Vector3 hitNormal,
            out Vector3 bendPoint,
            out HectonVoxelVolume bendVolume,
            out int bendRuntimeStamp)
        {
            bendPoint = Vector3.zero;
            bendVolume = null;
            bendRuntimeStamp = 0;

            for (int i = 0; i < MaxSupportedBendPoints; i++)
            {
                HectonVoxelVolume cachedVolume = _bendVolumes[i];
                int cachedStamp = _bendVolumeRuntimeStamps[i];
                if (cachedVolume == null || !cachedVolume.MatchesRuntimeStamp(cachedStamp))
                    continue;

                if (!cachedVolume.TryResolveNearestVoxelCorner(hitPoint, hitNormal, out Vector3 cornerWorld))
                    continue;

                bendPoint = cornerWorld + hitNormal * math.max(0.01f, _bendSurfaceOffset);
                bendVolume = cachedVolume;
                bendRuntimeStamp = cachedVolume.RuntimeStamp;
                return IsFinite(bendPoint);
            }

            return false;
        }

        private bool TryResolvePublishedVoxelCorner(
            Vector3 hitPoint,
            Vector3 lineDirection,
            Vector3 fallbackNormal,
            out Vector3 bendPoint,
            out Vector3 bendNormal,
            out HectonVoxelVolume bendVolume,
            out int bendRuntimeStamp)
        {
            bendPoint = Vector3.zero;
            bendNormal = fallbackNormal;
            bendVolume = null;
            bendRuntimeStamp = 0;

            Vector3 rayDirection = ResolveSafeDirection(lineDirection, -fallbackNormal);
            if (rayDirection.sqrMagnitude <= MinVectorMagnitudeSq)
                rayDirection = ResolveSafeDirection(-fallbackNormal, Vector3.forward);

            float skin = math.max(0.02f, _bendSurfaceOffset + 0.02f);
            float maxDistance = math.max(0.35f, _bendPointClearanceRadius + skin * 2f);
            float stepMeters = math.clamp(_bendPointClearanceRadius * 0.25f, 0.05f, 0.2f);
            Vector3 rayOrigin = hitPoint - rayDirection * skin;
            if (!HectonVoxelVolume.TryRaymarchAnyPublishedSdf(
                    rayOrigin,
                    rayDirection,
                    maxDistance,
                    stepMeters,
                    out HectonVoxelVolume resolvedVolume,
                    out VoxelSdfRaycastHit sdfHit) ||
                resolvedVolume == null ||
                sdfHit.Hit == 0)
            {
                return false;
            }

            Vector3 resolvedNormal = ResolveSafeDirection(sdfHit.Normal, fallbackNormal);
            if (!resolvedVolume.TryResolveNearestVoxelCorner(sdfHit.Point, resolvedNormal, out Vector3 cornerWorld))
                return false;

            bendPoint = cornerWorld + resolvedNormal * math.max(0.01f, _bendSurfaceOffset);
            bendNormal = resolvedNormal;
            bendVolume = resolvedVolume;
            bendRuntimeStamp = resolvedVolume.RuntimeStamp;
            return IsFinite(bendPoint);
        }

        private bool TryFindClosestObstacle(
            Vector3 start,
            Vector3 end,
            out Vector3 hitPoint,
            out Vector3 hitNormal,
            out HectonVoxelVolume hitVolume,
            out int hitRuntimeStamp)
        {
            hitPoint = Vector3.zero;
            hitNormal = Vector3.up;
            hitVolume = null;
            hitRuntimeStamp = 0;
            Vector3 delta = end - start;
            float distanceSq = delta.sqrMagnitude;
            float minDistanceSq = MinDistance * MinDistance;
            if (!math.isfinite(distanceSq) || distanceSq <= minDistanceSq)
                return false;

            ResolveLengthAndInvLength(distanceSq, out float distance, out float invDistance);
            Vector3 direction = delta * invDistance;
            float endpointInset = math.clamp(_bendEndpointInset, 0.005f, distance * 0.45f);
            float castDistance = distance - endpointInset * 2f;
            if (castDistance <= MinDistance)
                return false;

            Vector3 origin = start + direction * endpointInset;
            float stepMeters = math.clamp(_bendPointClearanceRadius * 0.25f, 0.05f, 0.25f);
            if (!HectonVoxelVolume.TryRaymarchAnyPublishedSdf(
                    origin,
                    direction,
                    castDistance,
                    stepMeters,
                    out HectonVoxelVolume resolvedVolume,
                    out VoxelSdfRaycastHit sdfHit) ||
                resolvedVolume == null ||
                sdfHit.Hit == 0 ||
                !IsFinite(sdfHit.Point))
            {
                return false;
            }

            hitPoint = sdfHit.Point;
            hitNormal = ResolveSafeDirection(sdfHit.Normal, -direction);
            hitVolume = resolvedVolume;
            hitRuntimeStamp = resolvedVolume.RuntimeStamp;
            return true;
        }

        private int BuildAnchorChain(Vector3 anchorPosition, Vector3 payloadPosition)
        {
            Vector3 safeAnchorPosition = IsFinite(anchorPosition) ? anchorPosition : Vector3.zero;
            Vector3 safePayloadPosition = IsFinite(payloadPosition) ? payloadPosition : safeAnchorPosition;
            _anchorPositions[0] = safeAnchorPosition;
            _anchorVelocities[0] = _playerRigidbody != null && IsFinite(_playerRigidbody.linearVelocity) ? _playerRigidbody.linearVelocity : Vector3.zero;
            int anchorCount = 1;

            for (int i = 0; i < _bendPointCount; i++)
            {
                _anchorPositions[anchorCount] = IsFinite(_bendPoints[i]) ? _bendPoints[i] : _anchorPositions[anchorCount - 1];
                _anchorVelocities[anchorCount] = Vector3.zero;
                anchorCount++;
            }

            _anchorPositions[anchorCount] = safePayloadPosition;
            _anchorVelocities[anchorCount] = _payloadBody != null && IsFinite(_payloadBody.linearVelocity) ? _payloadBody.linearVelocity : Vector3.zero;
            anchorCount++;

            PopulateSolverAnchors(anchorCount);

            float totalLength = 0f;
            int segmentCount = anchorCount - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                float segmentLength = ResolveMagnitude((_solverAnchorPositions[i] - _solverAnchorPositions[i + 1]).sqrMagnitude);
                _segmentLengths[i] = segmentLength;
                totalLength += segmentLength;
            }

            _currentLength = math.isfinite(totalLength) ? math.max(0f, totalLength) : 0f;
            if (_segmentRestLengthsDirty)
                RecalculateSegmentRestLengths(segmentCount, _currentLength);

            return anchorCount;
        }

        private void RecalculateSegmentRestLengths(int segmentCount, float totalLength)
        {
            if (segmentCount <= 0)
                return;

            float safeRestLength = math.isfinite(_restLength) ? math.max(_restLength, MinDistance) : MinDistance;
            float safeTotalLength = math.isfinite(totalLength) ? math.max(0f, totalLength) : 0f;
            if (safeTotalLength <= MinDistance)
            {
                float uniformLength = safeRestLength * math.rcp(segmentCount);
                for (int i = 0; i < segmentCount; i++)
                    _segmentRestLengths[i] = uniformLength;
                _segmentRestLengthsDirty = false;
                return;
            }

            for (int i = 0; i < segmentCount; i++)
            {
                float segmentLength = math.isfinite(_segmentLengths[i]) ? math.max(0f, _segmentLengths[i]) : 0f;
                float fraction = segmentLength * math.rcp(safeTotalLength);
                _segmentRestLengths[i] = safeRestLength * fraction;
            }

            _segmentRestLengthsDirty = false;
        }

        private void UpdateTowDirectionResponse()
        {
            if (_owner == null)
                return;

            Vector3 lineDirection;
            if (_bendPointCount > 0)
            {
                Vector3 toFirstBend = _bendPoints[0] - _owner.ResolveTowAnchorPosition();
                lineDirection = ResolveSafeDirection(toFirstBend, Vector3.zero);
            }
            else if (_payloadBody != null)
            {
                Vector3 direct = _payloadBody.worldCenterOfMass - _owner.ResolveTowAnchorPosition();
                lineDirection = ResolveSafeDirection(direct, Vector3.zero);
            }
            else
            {
                lineDirection = Vector3.zero;
            }

            float lateralPull = Vector3.Dot(lineDirection, _owner.PlayerRight);
            float backwardPull = -Vector3.Dot(lineDirection, _owner.PlayerForward);
            _signedLateralPull01 = math.isfinite(lateralPull) ? math.clamp(lateralPull, -1f, 1f) : 0f;
            _backwardPull01 = math.isfinite(backwardPull) ? math.saturate(backwardPull) : 0f;
        }

        private void UpdateTowDrag()
        {
            if (_owner == null)
                return;

            float safeTension01 = math.isfinite(_tension01) ? math.saturate(_tension01) : 0f;
            float safePayloadDrift01 = math.isfinite(_payloadDrift01) ? math.saturate(_payloadDrift01) : 0f;
            float safePayloadMass01 = math.isfinite(_payloadMass01) ? math.saturate(_payloadMass01) : 0f;
            float load01 = math.saturate(math.max(safeTension01, safePayloadDrift01 * 0.72f) * math.lerp(0.45f, 1f, safePayloadMass01));
            _towDragMultiplier = _owner.ResolveTowDragMultiplier(load01);
            _owner.ApplyTowLoad(_towDragMultiplier);
        }

        private void UpdateConstraintTelemetry()
        {
            float safeCurrentLength = math.isfinite(_currentLength) ? math.max(0f, _currentLength) : 0f;
            float safeRestLength = math.isfinite(_restLength) ? math.max(0f, _restLength) : 0f;
            float safeFullTensionExtension = math.isfinite(_fullTensionExtension) ? math.max(_fullTensionExtension, 0.01f) : 0.01f;
            float extensionTotal = math.max(0f, safeCurrentLength - safeRestLength);
            _tension01 = math.saturate(extensionTotal * math.rcp(safeFullTensionExtension));
        }

        private bool UpdateStressAndSnap(float peakTension, float fixedDeltaTime)
        {
            float safePeakTension = math.isfinite(peakTension) ? math.max(0f, peakTension) : 0f;
            float safeFixedDeltaTime = math.isfinite(fixedDeltaTime) ? math.max(0f, fixedDeltaTime) : 0f;
            float snapThreshold = ResolveSnapTensionThreshold();
            float rawSnapDuration = _owner != null ? _owner.ResolveSnapStressDuration() : 0.1f;
            float snapDuration = math.isfinite(rawSnapDuration) ? math.max(0.1f, rawSnapDuration) : 0.1f;

            if (safePeakTension > snapThreshold)
            {
                _stressTimer += safeFixedDeltaTime;
            }
            else
            {
                _stressTimer = math.max(0f, _stressTimer - (safeFixedDeltaTime * 0.5f));
            }

            if (!math.isfinite(_stressTimer))
                _stressTimer = 0f;

            _stress01 = math.saturate(_stressTimer * math.rcp(snapDuration));
            if (_stressTimer < snapDuration)
                return false;

            Vector3 rawOwnerAnchor = _owner != null ? _owner.ResolveTowAnchorPosition() : Vector3.zero;
            Vector3 ownerAnchor = IsFinite(rawOwnerAnchor) ? rawOwnerAnchor : Vector3.zero;
            Vector3 payloadCenter = _payloadBody != null && IsFinite(_payloadBody.worldCenterOfMass)
                ? _payloadBody.worldCenterOfMass
                : ownerAnchor;
            Vector3 playerSegmentDirection = _bendPointCount > 0
                ? ResolveSafeDirection(_bendPoints[0] - ownerAnchor, Vector3.zero)
                : ResolveSafeDirection(payloadCenter - ownerAnchor, Vector3.zero);
            Vector3 payloadSegmentDirection = _bendPointCount > 0
                ? ResolveSafeDirection(_bendPoints[_bendPointCount - 1] - payloadCenter, Vector3.zero)
                : ResolveSafeDirection(ownerAnchor - payloadCenter, Vector3.zero);
            float snapSeverity = math.saturate(safePeakTension * math.rcp(math.max(snapThreshold, 1f)));
            ClearDataVaultCableEntry();
            PublishSnapImpactSignal(ownerAnchor, safePeakTension, snapSeverity);
            PublishTetherSnappedSignal(ownerAnchor, safePeakTension, snapThreshold, snapSeverity, 1);
            InvokeSnapProtocol(playerSegmentDirection, payloadSegmentDirection, snapSeverity, false);
            return true;
        }

        private void PublishTetherSnappedSignal(
            Vector3 snapPosition,
            float peakTension,
            float snapThreshold,
            float snapSeverity,
            byte reason)
        {
            if (!IsFinite(snapPosition))
                snapPosition = transform.position;

            TetherSignals.PublishSnap(new TetherSnappedSignal
            {
                SnapAup = AbsoluteUniversePosition.FromRuntimePosition(snapPosition),
                TetherId = unchecked((uint)EntityId.ToULong(GetEntityId())),
                FrameIndex = unchecked((uint)_currentSimulationFrameIndex),
                PeakTension = peakTension,
                SnapThreshold = snapThreshold,
                Severity01 = math.saturate(snapSeverity),
                NodeCount = (ushort)math.clamp(_verletNodeCount, 0, ushort.MaxValue),
                Reason = reason,
                Flags = 0
            });
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
                float distanceSq = delta.sqrMagnitude;
                float minDistanceSq = MinDistance * MinDistance;
                if (!math.isfinite(distanceSq) || distanceSq <= minDistanceSq)
                    continue;

                ResolveLengthAndInvLength(distanceSq, out float distance, out _);
                float segmentInset = math.min(math.max(0.01f, _bendEndpointInset), distance * 0.25f);
                float castDistance = distance - segmentInset * 2f;
                if (castDistance <= MinDistance)
                    continue;

                bool foundBlockingHit = TryFindClosestObstacle(
                    start,
                    end,
                    out Vector3 obstructionPoint,
                    out _,
                    out _,
                    out _);
                if (!foundBlockingHit)
                    continue;

                if (HasSupportingBendPointForSegment(i, obstructionPoint))
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

                Vector3 ownerAnchor = _owner.ResolveTowAnchorPosition();
                Vector3 playerSegmentDirection = _bendPointCount > 0
                    ? ResolveSafeDirection(_bendPoints[0] - ownerAnchor, Vector3.zero)
                    : ResolveSafeDirection(_payloadBody.worldCenterOfMass - ownerAnchor, Vector3.zero);
                Vector3 payloadSegmentDirection = _bendPointCount > 0
                    ? ResolveSafeDirection(_bendPoints[_bendPointCount - 1] - _payloadBody.worldCenterOfMass, Vector3.zero)
                    : ResolveSafeDirection(ownerAnchor - _payloadBody.worldCenterOfMass, Vector3.zero);
                InvokeSnapProtocol(playerSegmentDirection, payloadSegmentDirection, 0f, true);
                return true;
            }

            _slicingSegmentIndex = -1;
            _slicingConsecutiveFrames = 0;
            return false;
        }

        private void CopyVisualSolverState(int anchorCount)
        {
            int safeAnchorCount = math.clamp(anchorCount, 0, MaxAnchors);
            for (int anchorIndex = 0; anchorIndex < safeAnchorCount; anchorIndex++)
            {
                Vector3 anchorPosition = IsFinite(_anchorPositions[anchorIndex]) ? _anchorPositions[anchorIndex] : Vector3.zero;
                _visualAnchorPositions[anchorIndex] = new float3(anchorPosition.x, anchorPosition.y, anchorPosition.z);
            }

            int segmentCount = math.max(0, safeAnchorCount - 1);
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
                _visualSegmentLengths[segmentIndex] = math.isfinite(_segmentLengths[segmentIndex]) ? math.max(0f, _segmentLengths[segmentIndex]) : 0f;
        }

        private bool HasSupportingBendPointForSegment(int segmentIndex, Vector3 hitPoint)
        {
            if (_bendPointCount <= 0)
                return false;

            float supportingRadius = math.max(_bendPointClearanceRadius, _bendSurfaceOffset) * 1.5f;
            float supportingRadiusSq = supportingRadius * supportingRadius;
            if (segmentIndex > 0)
            {
                Vector3 previousAnchor = _anchorPositions[segmentIndex];
                float previousDistanceSq = (previousAnchor - hitPoint).sqrMagnitude;
                if (math.isfinite(previousDistanceSq) && previousDistanceSq <= supportingRadiusSq)
                    return true;
            }

            int finalSegmentIndex = _bendPointCount;
            if (segmentIndex < finalSegmentIndex)
            {
                Vector3 nextAnchor = _anchorPositions[segmentIndex + 1];
                float nextDistanceSq = (nextAnchor - hitPoint).sqrMagnitude;
                if (math.isfinite(nextDistanceSq) && nextDistanceSq <= supportingRadiusSq)
                    return true;
            }

            return false;
        }

        private void InvokeSnapProtocol(
            Vector3 playerSegmentDirection,
            Vector3 payloadSegmentDirection,
            float snapSeverity,
            bool suppressPlayerFeedback)
        {
            ReleasePrimaryConstraint();
            _owner.HandleTetherSnap(
                playerSegmentDirection,
                payloadSegmentDirection,
                snapSeverity,
                suppressPlayerFeedback,
                _payloadBody,
                _payloadCollider);
        }

        private float ResolveTowSpringStiffness()
        {
            if (_manager != null)
                return _manager.ResolveTowSpringStiffness(_owner);

            return _owner != null ? _owner.ResolveTowSpringStiffness() : 0f;
        }

        private float ResolveTowOverDampingMultiplier()
        {
            if (_manager != null)
                return _manager.ResolveTowOverDampingMultiplier(_owner);

            return _owner != null ? _owner.ResolveTowOverDampingMultiplier() : 1f;
        }

        private float ResolveSnapTensionThreshold()
        {
            if (_verletTuning.IsCreated && _verletTuning.Length > 0)
            {
                float tunedBreakForce = _verletTuning[0].BreakForce;
                if (math.isfinite(tunedBreakForce) && tunedBreakForce > 1f)
                    return tunedBreakForce;
            }

            if (_manager != null)
                return math.max(1f, _manager.ResolveTowSnapTensionThreshold(_owner));

            return math.max(1f, _owner != null ? _owner.ResolveSnapTensionThreshold() : 1f);
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
            if (!_isActive || !IsFinite(shiftOffset) || shiftOffset.sqrMagnitude <= MinVectorMagnitudeSq)
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

        internal bool RebaseVerletRuntime(float3 shiftOffset)
        {
            if (!_isActive ||
                !_verletPositions.IsCreated ||
                !_verletPreviousPositions.IsCreated ||
                !math.all(math.isfinite(shiftOffset)) ||
                math.lengthsq(shiftOffset) <= MinVectorMagnitudeSq)
            {
                return false;
            }

            _verletSolverOrigin = SanitizeFinite(_verletSolverOrigin - shiftOffset);

            if (_visualSegmentPositions.IsCreated)
            {
                for (int pointIndex = 0; pointIndex < _visualSegmentPositions.Length; pointIndex++)
                    _visualSegmentPositions[pointIndex] = SanitizeFinite(_visualSegmentPositions[pointIndex] - shiftOffset);
            }

            return true;
        }

        private void RefreshTargetLengthFromOwner()
        {
            if (_owner == null)
                return;

            float targetLength = _owner.TargetLength;
            if (targetLength <= MinDistance || !math.isfinite(targetLength))
                return;

            float clamped = math.max(1.25f, targetLength);
            if (math.abs(clamped - _restLength) <= 0.001f)
                return;

            _restLength = clamped;
            _segmentRestLengthsDirty = true;
        }

        private void PublishTowLoadLimitIfNeeded(float peakTension)
        {
            if (_payloadBody == null || _playerRigidbody == null || _payloadMass01 < 0.75f || peakTension <= 0f || !math.isfinite(peakTension))
                return;

            int frame = _currentSimulationFrameIndex;
            if (IsFrameCooldownActive(frame, _lastTowLoadLimitCommandFrame, TowLoadLimitCommandCooldownFrames))
                return;

            float threshold = ResolveSnapTensionThreshold();
            float load01 = math.saturate(peakTension * math.rcp(math.max(threshold, 1f)));
            if (load01 < 0.65f)
                return;

            VehicleCommandSignal signal = new VehicleCommandSignal
            {
                TargetInstanceId = unchecked((int)EntityId.ToULong(_playerRigidbody.GetEntityId())),
                Pitch = 0f,
                Yaw = 0f,
                Throttle = math.lerp(0.65f, 0.25f, load01),
                BallastDelta = 0f,
                Sequence = 0u,
                Flags = (byte)(VehicleCommandSignalFlags.ManualThrottle | VehicleCommandSignalFlags.TowLoadLimit)
            };
            if (VehicleCommandSignalBus.Publish(in signal))
                _lastTowLoadLimitCommandFrame = frame;
        }

        internal void CommitVisualRebaseUpload()
        {
            if (VisualSegmentBuffer == null)
                return;

            if (_visualSegmentPositions.IsCreated)
                UploadVisualGpuBuffers(includeTension: _verletSegmentTensions.IsCreated);
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
            if (_owner != null && _payloadBody != null)
                SyncPrimaryConstraint(_owner.ResolveTowAnchorPosition(), _payloadBody.worldCenterOfMass);
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

        private void EnsurePrimaryConstraint(Vector3 anchorPositionWS, Vector3 payloadPositionWS)
        {
            SyncPrimaryConstraint(anchorPositionWS, payloadPositionWS);
        }

        private void SyncPrimaryConstraint(Vector3 anchorPositionWS, Vector3 payloadPositionWS)
        {
            if (_payloadBody == null || _playerRigidbody == null)
            {
                ReleasePrimaryConstraint();
                return;
            }

            Vector3 constraintAnchorPosition = _bendPointCount > 0
                ? _bendPoints[_bendPointCount - 1]
                : anchorPositionWS;
            Vector3 separation = payloadPositionWS - constraintAnchorPosition;
            float distanceSq = separation.sqrMagnitude;
            float minDistanceSq = MinDistance * MinDistance;
            if (!math.isfinite(distanceSq) || distanceSq <= minDistanceSq)
            {
                _primaryConstraintForceMagnitude = 0f;
                return;
            }

            float targetDistance = ResolvePrimaryConstraintTargetDistance();
            float targetDistanceSq = targetDistance * targetDistance;
            if (distanceSq <= targetDistanceSq)
            {
                _primaryConstraintForceMagnitude = 0f;
                return;
            }

            ResolveLengthAndInvLength(distanceSq, out float distance, out float invDistance);
            Vector3 directionAwayFromAnchor = separation * invDistance;
            float extension = distance - targetDistance;
            if (extension <= 0f)
            {
                _primaryConstraintForceMagnitude = 0f;
                return;
            }

            Vector3 anchorVelocity = _bendPointCount > 0
                ? Vector3.zero
                : _playerRigidbody.GetPointVelocity(anchorPositionWS);
            Vector3 payloadVelocity = _payloadBody.GetPointVelocity(payloadPositionWS);
            Vector3 targetPayloadPosition = constraintAnchorPosition + directionAwayFromAnchor * targetDistance;
            float safeReducedMass = math.max(_reducedMass, 0.0001f);
            float maxForceMagnitude = math.max(0f, _maxCableAcceleration) * safeReducedMass;
            Vector3 clampedPayloadVelocity = ClampPdDerivativeVelocity(
                anchorVelocity,
                payloadVelocity,
                _dampingCoefficient,
                maxForceMagnitude);
            float3 requestedForce3 = HectonContactJob.ResolveTractorBeamPdForce(
                targetPayloadPosition,
                payloadPositionWS,
                anchorVelocity,
                clampedPayloadVelocity,
                _springStiffness,
                _dampingCoefficient,
                maxForceMagnitude);
            Vector3 requestedForceVector = new Vector3(
                requestedForce3.x,
                requestedForce3.y,
                requestedForce3.z);
            float requestedForceMagnitude = ResolveMagnitude(requestedForceVector.sqrMagnitude);
            if (requestedForceMagnitude <= 0f)
            {
                _primaryConstraintForceMagnitude = 0f;
                return;
            }

            Vector3 requestedAccelerationVector = requestedForceVector * math.rcp(safeReducedMass);
            _primaryConstraintForceMagnitude = requestedForceMagnitude;
            ApplyReducedMassReactionForce(anchorPositionWS, requestedForceVector);
            ApplyClampedAcceleration(_payloadBody, requestedAccelerationVector, _maxCableAcceleration);
        }

        private void ApplyReducedMassReactionForce(Vector3 anchorPositionWS, Vector3 payloadForce)
        {
            if (_tetherClass != TetherClass.TowCable ||
                _bendPointCount > 0 ||
                _playerRigidbody == null ||
                _playerRigidbody.isKinematic ||
                payloadForce.sqrMagnitude <= MinVectorMagnitudeSq)
            {
                return;
            }

            Vector3 reactionForce = -payloadForce;
            if (reactionForce.sqrMagnitude <= MinVectorMagnitudeSq)
                return;

            PhysicsForceRouter.QueueForceAtPosition(
                _playerRigidbody,
                reactionForce,
                anchorPositionWS,
                ForceMode.Force);
        }

        private void RefreshPrimaryConstraintDrive()
        {
            _primaryConstraintForceMagnitude = 0f;
        }

        private float ResolvePrimaryConstraintForceMagnitude()
        {
            return _primaryConstraintForceMagnitude;
        }

        private void ReleasePrimaryConstraint()
        {
            _primaryConstraintForceMagnitude = 0f;
        }

        private float ResolvePrimaryConstraintTargetDistance()
        {
            int lastSegmentIndex = _bendPointCount;
            if (_segmentRestLengthsDirty ||
                lastSegmentIndex < 0 ||
                lastSegmentIndex >= MaxSegments ||
                _segmentRestLengths[lastSegmentIndex] <= MinDistance)
            {
                return math.max(_restLength, MinDistance);
            }

            return math.max(_segmentRestLengths[lastSegmentIndex], MinDistance);
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

            float safeMaxMagnitude = math.isfinite(maxMagnitude) ? math.max(0f, maxMagnitude) : 0f;
            float sqrMagnitude = value.sqrMagnitude;
            if (!math.isfinite(sqrMagnitude))
                return Vector3.zero;

            if (sqrMagnitude <= MinVectorMagnitudeSq || safeMaxMagnitude <= 0f)
                return Vector3.zero;

            float maxMagnitudeSq = safeMaxMagnitude * safeMaxMagnitude;
            if (sqrMagnitude <= maxMagnitudeSq)
                return value;

            return value * (safeMaxMagnitude * math.rsqrt(math.max(sqrMagnitude, MinVectorMagnitudeSq)));
        }

        private static Vector3 ResolveSafeDirection(Vector3 value, Vector3 fallback)
        {
            if (!IsFinite(value))
                value = fallback;

            float sqrMagnitude = value.sqrMagnitude;
            if (math.isfinite(sqrMagnitude) && sqrMagnitude > MinVectorMagnitudeSq)
                return value * math.rsqrt(math.max(sqrMagnitude, MinVectorMagnitudeSq));

            if (!IsFinite(fallback))
                return Vector3.zero;

            float fallbackSqrMagnitude = fallback.sqrMagnitude;
            return math.isfinite(fallbackSqrMagnitude) && fallbackSqrMagnitude > MinVectorMagnitudeSq
                ? fallback * math.rsqrt(math.max(fallbackSqrMagnitude, MinVectorMagnitudeSq))
                : Vector3.zero;
        }

        private static float ResolveMagnitude(float sqrMagnitude)
        {
            return math.isfinite(sqrMagnitude) && sqrMagnitude > MinVectorMagnitudeSq
                ? sqrMagnitude * math.rsqrt(math.max(sqrMagnitude, MinVectorMagnitudeSq))
                : 0f;
        }

        private static void ResolveLengthAndInvLength(float sqrMagnitude, out float length, out float invLength)
        {
            if (!math.isfinite(sqrMagnitude) || sqrMagnitude <= MinVectorMagnitudeSq)
            {
                length = 0f;
                invLength = 0f;
                return;
            }

            invLength = math.rsqrt(math.max(sqrMagnitude, MinVectorMagnitudeSq));
            length = sqrMagnitude * invLength;
            if (math.isfinite(length) && math.isfinite(invLength))
                return;

            length = 0f;
            invLength = 0f;
        }

        private static Vector3 ClampPdDerivativeVelocity(
            Vector3 targetVelocity,
            Vector3 currentVelocity,
            float dampingCoefficient,
            float maxDerivativeForceMagnitude)
        {
            if (!IsFinite(targetVelocity))
                return IsFinite(currentVelocity) ? currentVelocity : Vector3.zero;

            if (!IsFinite(currentVelocity))
                return targetVelocity;

            float safeDamping = math.isfinite(dampingCoefficient) ? math.max(0f, dampingCoefficient) : 0f;
            float safeMaxForce = math.isfinite(maxDerivativeForceMagnitude) ? math.max(0f, maxDerivativeForceMagnitude) : 0f;
            if (safeDamping <= MinDistance || safeMaxForce <= 0f)
                return currentVelocity;

            Vector3 velocityError = currentVelocity - targetVelocity;
            float errorMagnitudeSq = velocityError.sqrMagnitude;
            if (!math.isfinite(errorMagnitudeSq))
                return targetVelocity;

            float maxVelocityError = safeMaxForce * math.rcp(safeDamping);
            float maxVelocityErrorSq = maxVelocityError * maxVelocityError;
            if (!math.isfinite(maxVelocityError) || !math.isfinite(maxVelocityErrorSq) || errorMagnitudeSq <= maxVelocityErrorSq)
                return currentVelocity;

            return targetVelocity + velocityError * (maxVelocityError * math.rsqrt(math.max(errorMagnitudeSq, MinVectorMagnitudeSq)));
        }

        private static bool IsFinite(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3));
        }

        private static bool IsFrameCooldownActive(int currentFrame, int lastFrame, int cooldownFrames)
        {
            if (cooldownFrames <= 0 || lastFrame < 0)
                return false;

            long elapsed = currentFrame >= lastFrame
                ? currentFrame - lastFrame
                : (long)int.MaxValue - lastFrame + currentFrame + 1L;
            return elapsed < cooldownFrames;
        }

        private static float3 SanitizeFinite(float3 value)
        {
            return math.all(math.isfinite(value)) ? value : float3.zero;
        }

        private static float3 ClampCableVelocity(float3 value, float maxVelocity)
        {
            if (!math.all(math.isfinite(value)))
                return float3.zero;

            float safeMaxVelocity = math.isfinite(maxVelocity) ? math.max(0f, maxVelocity) : 0f;
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq))
                return float3.zero;

            if (safeMaxVelocity <= 0f)
                return float3.zero;

            float maxSq = safeMaxVelocity * safeMaxVelocity;
            if (lengthSq > maxSq)
                return value * (safeMaxVelocity * math.rsqrt(math.max(lengthSq, 0.000001f)));

            return value;
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
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

        private static float ResolveBlendFactor(float sharpness, float deltaTime)
        {
            if (!math.isfinite(sharpness) || !math.isfinite(deltaTime) || sharpness <= 0f || deltaTime <= 0f)
                return 0f;

            return math.saturate(sharpness * deltaTime);
        }
    }
}
