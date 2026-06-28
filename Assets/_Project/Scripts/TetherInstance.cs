using System;
using System.IO;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Physics;
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
        private static int s_x001TetherInstanceSignalPushDropCount;
        private const int MaxSupportedBendPoints = 4;
        private const int MaxSegments = MaxSupportedBendPoints + 1;
        private const int MaxAnchors = MaxSegments + 1;
        private const int BendRecheckCooldownFrames = 3;
        private const uint KccVelocityTetherMaxAgeFrames = 12u;
        private const float PlayerAnchorEquivalentMassKg = 80f;
        private const float MinDistance = 0.0001f;
        private const float MinVectorMagnitudeSq = 0.000001f;
        private const float TowCableOverDampingMinimum = 1.2f;
        private const int MinVisualSegmentCount = 8;
        private const int MaxVisualSegmentCount = 24;
        private const float VisualSagScale = 0.05f;
        private const int VerletLowIterationCount = 3;
        private const int VerletMidIterationCount = 5;
        private const int VerletHighIterationCount = 8;
        private const int VerletUltraIterationCount = 15;
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
        private const string TetherTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_1303_Tethers.bin";
        private const string TetherTelemetryH8DumpRelativePath = "Docs/AgentLogs/Dump_1303_Tethers.h8dump";
        private const ulong TetherTelemetryDumpMagic = 0x00384E4F54434548ul;
        private const int TetherTelemetryDumpEntrySize = 64;
        private const uint TetherTelemetryFailureResolve = 1u;
        private const uint TetherTelemetryFailureLock = 2u;
        private const uint TetherTelemetryFailureLength = 3u;
        private const uint TetherTelemetryFailureState = 4u;
        private const int TetherTelemetryFailureDumpThreshold = 3;
        private const ulong VisualFallbackMutationGuardMask =
            (1UL << (((int)BufferID.TetherVisualSegmentPositions) & 31)) |
            (1UL << (((int)BufferID.TetherVisualAnchorPositions) & 31)) |
            (1UL << (((int)BufferID.TetherVisualSegmentLengths) & 31));
        private const ulong CableStateMutationGuardMask =
            (1UL << (((int)BufferID.TetherCablePositions) & 31)) |
            (1UL << (((int)BufferID.TetherCablePreviousPositions) & 31)) |
            (1UL << (((int)BufferID.TetherCableVelocities) & 31)) |
            (1UL << (((int)BufferID.TetherCableMasses) & 31)) |
            (1UL << (((int)BufferID.TetherCableSegmentTensions) & 31)) |
            (1UL << (((int)BufferID.VerletCableTensionForces) & 31));
        private const ulong TelemetryMutationGuardMask =
            (1UL << (((int)BufferID.TetherCableBlackBox) & 31)) |
            (1UL << (((int)BufferID.TetherCableBlackBoxHead) & 31));
        private const ulong VerletBootstrapMutationGuardMask =
            (1UL << (((int)BufferID.TetherVerletPositions) & 31)) |
            (1UL << (((int)BufferID.TetherVerletPreviousPositions) & 31)) |
            (1UL << (((int)BufferID.TetherVerletVelocities) & 31)) |
            (1UL << (((int)BufferID.TetherVerletPinnedPositions) & 31)) |
            (1UL << (((int)BufferID.TetherVerletPinnedMask) & 31)) |
            (1UL << (((int)BufferID.TetherVerletSegmentRestLengths) & 31)) |
            (1UL << (((int)BufferID.TetherVerletSegmentTensions) & 31)) |
            (1UL << (((int)BufferID.TetherVerletSolverStats) & 31)) |
            (1UL << (((int)BufferID.TetherVerletSolverFlags) & 31)) |
            (1UL << (((int)BufferID.TetherVerletNodeFaultFlags) & 31));
        private const ulong VerletSolveMutationGuardMask =
            VerletBootstrapMutationGuardMask |
            (1UL << (((int)BufferID.TetherVerletCorrections) & 31)) |
            (1UL << (((int)BufferID.TetherVerletCorrectionWeights) & 31)) |
            TelemetryMutationGuardMask;

        private static long s_dataVaultSlotReservationMask;

        private Vector3 _bendPoint0;
        private Vector3 _bendPoint1;
        private Vector3 _bendPoint2;
        private Vector3 _bendPoint3;
        private Vector3 _bendNormal0;
        private Vector3 _bendNormal1;
        private Vector3 _bendNormal2;
        private Vector3 _bendNormal3;
        private Vector3 _anchorPosition0;
        private Vector3 _anchorPosition1;
        private Vector3 _anchorPosition2;
        private Vector3 _anchorPosition3;
        private Vector3 _anchorPosition4;
        private Vector3 _anchorPosition5;
        private Vector3 _anchorVelocity0;
        private Vector3 _anchorVelocity1;
        private Vector3 _anchorVelocity2;
        private Vector3 _anchorVelocity3;
        private Vector3 _anchorVelocity4;
        private Vector3 _anchorVelocity5;
        private Vector3 _solverAnchorPosition0;
        private Vector3 _solverAnchorPosition1;
        private Vector3 _solverAnchorPosition2;
        private Vector3 _solverAnchorPosition3;
        private Vector3 _solverAnchorPosition4;
        private Vector3 _solverAnchorPosition5;
        private Vector3 _solverAnchorVelocity0;
        private Vector3 _solverAnchorVelocity1;
        private Vector3 _solverAnchorVelocity2;
        private Vector3 _solverAnchorVelocity3;
        private Vector3 _solverAnchorVelocity4;
        private Vector3 _solverAnchorVelocity5;
        private float _segmentRestLength0;
        private float _segmentRestLength1;
        private float _segmentRestLength2;
        private float _segmentRestLength3;
        private float _segmentRestLength4;
        private float _segmentLength0;
        private float _segmentLength1;
        private float _segmentLength2;
        private float _segmentLength3;
        private float _segmentLength4;
        private HectonVoxelVolume _bendVolume0;
        private HectonVoxelVolume _bendVolume1;
        private HectonVoxelVolume _bendVolume2;
        private HectonVoxelVolume _bendVolume3;
        private int _bendVolumeRuntimeStamp0;
        private int _bendVolumeRuntimeStamp1;
        private int _bendVolumeRuntimeStamp2;
        private int _bendVolumeRuntimeStamp3;
        private Vector3 _discardVectorSlot;
        private float _discardFloatSlot;
        private HectonVoxelVolume _discardBendVolumeSlot;
        private int _discardIntSlot;

        private TetherManager _manager;
        private HeavyTowWinch _owner;
        private HectonPlayerMotor _playerMotor;
        private Rigidbody _anchorBody;
        private Rigidbody _payloadBody;
        private Collider _payloadCollider;
        private HectonVoxelEngine _voxelEngineRuntime;
        private IVoxelSonarSdfReadModel _voxelSdfReadModel;
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

        private VaultGenerationHandle<float3> _visualSegmentPositionsHandle;
        private VaultGenerationHandle<GpuCableSplinePointDTO> _visualSegmentGpuPointsHandle;
        private VaultGenerationHandle<float3> _visualAnchorPositionsHandle;
        private VaultGenerationHandle<float> _visualSegmentLengthsHandle;
        private VaultGenerationHandle<float3> _verletPositionsHandle;
        private VaultGenerationHandle<float3> _verletPreviousPositionsHandle;
        private VaultGenerationHandle<float3> _verletVelocitiesHandle;
        private VaultGenerationHandle<float3> _verletPinnedPositionsHandle;
        private VaultGenerationHandle<byte> _verletPinnedMaskHandle;
        private VaultGenerationHandle<float> _verletSegmentRestLengthsHandle;
        private VaultGenerationHandle<float> _verletSegmentTensionsHandle;
        private VaultGenerationHandle<float3> _verletCorrectionsHandle;
        private VaultGenerationHandle<float> _verletCorrectionWeightsHandle;
        private VaultGenerationHandle<float> _verletSolverStatsHandle;
        private VaultGenerationHandle<int> _verletSolverFlagsHandle;
        private VaultGenerationHandle<byte> _verletNodeFaultFlagsHandle;
        private VaultGenerationHandle<TetherVerletTelemetryEntry> _verletTelemetryRingHandle;
        private VaultGenerationHandle<int> _verletTelemetryHeadHandle;
        private VaultGenerationHandle<CableTensionForceDTO> _verletTensionForcesHandle;
        private VaultGenerationHandle<VerletCableTuningDTO> _verletTuningHandle;
        private VaultGenerationHandle<float3> _dataVaultCablePositionsHandle;
        private VaultGenerationHandle<float3> _dataVaultCablePreviousPositionsHandle;
        private VaultGenerationHandle<float3> _dataVaultCableVelocitiesHandle;
        private VaultGenerationHandle<float> _dataVaultCableMassesHandle;
        private VaultGenerationHandle<float> _dataVaultCableSegmentTensionsHandle;
        private IDataVault _dataVault;
        private IDataVault _dataVaultCableWriteVault;
        private int _dataVaultSlot = -1;
        private int _dataVaultNativeStateMask;
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
        private int _consecutiveVaultAccessFailures;
        private HectonQualityTier _qualityTier = HectonQualityTier.Unknown;
        private float _qualityWeight01 = 1f;
        private JobHandle _pendingVerletSolveHandle;
        private bool _pendingVerletSolveActive;
        private IDataVault _pendingVerletSolveGuardVault;
        private bool _pendingVerletSolveGuardHeld;
        private Vector3 _pendingVerletAnchorPosition;
        private Vector3 _pendingVerletPayloadPosition;
        private float _pendingVerletFixedDeltaTime;
        private float _pendingVerletStretchThreshold01;
        private int _pendingVerletFrameIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticTetherSlotState()
        {
            System.Threading.Volatile.Write(ref s_dataVaultSlotReservationMask, 0L);
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
        public int VisualPointCount => _isActive ? math.max(_verletNodeCount, 0) : 0;

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

        internal void RebindDataVault(IDataVault currentVault)
        {
            if (ReferenceEquals(_dataVault, currentVault))
                return;

            FinalizePendingVerletSolveForBarrier(publishResults: false);
            DisposeDataVaultCableState();
            _dataVault = currentVault;
            _verletRuntimeInitialized = false;
            _consecutiveVaultAccessFailures = 0;

            if (_isActive && currentVault != null)
                EnsureDataVaultCableState(_verletNodeCount > 1 ? _verletNodeCount : ResolveVerletPointCount(_qualityWeight01));
        }

        /// <summary>
        /// Configures the tether against a player/payload pair.
        /// </summary>
        public void Configure(
            HeavyTowWinch owner,
            HectonPlayerMotor playerMotor,
            Rigidbody legacyAnchorBody,
            Rigidbody payloadBody,
            Collider payloadCollider,
            float initialDistance)
        {
            _ = legacyAnchorBody;
            float qualityWeight01 = _manager != null
                ? _manager.CachedQualityWeight01
                : ResolveCompatibilityTetherQualityWeight(HectonQualityTier.Unknown);
            Configure(owner, playerMotor, payloadBody, payloadCollider, initialDistance, qualityWeight01);
        }

        internal void Configure(
            HeavyTowWinch owner,
            HectonPlayerMotor playerMotor,
            Rigidbody payloadBody,
            Collider payloadCollider,
            float initialDistance,
            HectonQualityTier qualityTier)
        {
            Configure(
                owner,
                playerMotor,
                null,
                payloadBody,
                payloadCollider,
                initialDistance,
                qualityTier,
                ResolveCompatibilityTetherQualityWeight(qualityTier));
        }

        internal void Configure(
            HeavyTowWinch owner,
            HectonPlayerMotor playerMotor,
            Rigidbody payloadBody,
            Collider payloadCollider,
            float initialDistance,
            float qualityWeight01)
        {
            Configure(
                owner,
                playerMotor,
                null,
                payloadBody,
                payloadCollider,
                initialDistance,
                HectonQualityTier.Unknown,
                qualityWeight01);
        }

        private void Configure(
            HeavyTowWinch owner,
            HectonPlayerMotor playerMotor,
            Rigidbody anchorBody,
            Rigidbody payloadBody,
            Collider payloadCollider,
            float initialDistance,
            HectonQualityTier qualityTier,
            float qualityWeight01)
        {
            _qualityTier = TetherManager.SanitizeQualityTier(qualityTier);
            _qualityWeight01 = SanitizeQualityWeight(qualityWeight01);
            _owner = owner;
            _playerMotor = playerMotor;
            _anchorBody = anchorBody;
            _payloadBody = payloadBody;
            _payloadCollider = payloadCollider;
            _voxelEngineRuntime = owner != null ? owner.CachedTetherManagerVoxelEngine : null;
            _voxelSdfReadModel = owner != null ? owner.CachedTetherManagerVoxelSdf : null;
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
            _visualSegmentCount = ResolveVerletPointCount(_qualityWeight01);
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
            _consecutiveVaultAccessFailures = 0;
            ClearBendMetadata(0);
            EnsureVisualBuffers(_visualSegmentCount);
            EnsureDataVaultCableState();
            InitializeVerletRuntime(
                owner != null ? owner.ResolveTowAnchorPosition() : Vector3.zero,
                _payloadBody != null ? _payloadBody.worldCenterOfMass : Vector3.zero);
            GlobalPhysicsStateManager.RegisterTetherConnection(this, _anchorBody, _payloadBody);
            RefreshKinematicAnchorCompensationState(forceRecalculateDamping: true);
            RecalculateDampingCoefficient();
            EnsurePrimaryConstraint(
                owner != null ? owner.ResolveTowAnchorPosition() : Vector3.zero,
                _payloadBody != null ? _payloadBody.worldCenterOfMass : Vector3.zero);
            _isActive = true;
            _visualBounds = default;
            _visualBounds.center = owner != null ? owner.ResolveTowAnchorPosition() : Vector3.zero;
            _visualBounds.size = Vector3.one;
        }

        /// <summary>
        /// Queues an external cable-snare force sample for the next fixed-step solve.
        /// </summary>
        public void QueueExternalCableSnare(Vector3 anchorWS, float tension01, float cutProgress01)
        {
            float safeTension01 = math.isfinite(tension01) ? math.saturate(tension01) : 0f;
            float safeCutProgress01 = math.isfinite(cutProgress01) ? math.saturate(cutProgress01) : 1f;
            float effectiveTension01 = safeTension01 * (1f - safeCutProgress01);
            if (!IsFinite(anchorWS) || effectiveTension01 <= 0.0001f)
            {
                _bioCableRequestedThisStep = true;
                _bioCableRequestedAnchorWS = Vector3.zero;
                _bioCableRequestedTension01 = 0f;
                _bioCableRequestedCutProgress01 = 1f;
                _bioCableCurrentAnchorWS = Vector3.zero;
                _bioCableCurrentTension01 = 0f;
                _bioCableCurrentCutProgress01 = 1f;
                _bioCableHoldTimer = 0f;
                return;
            }

            _bioCableRequestedThisStep = true;
            _bioCableRequestedAnchorWS = anchorWS;
            _bioCableRequestedTension01 = safeTension01;
            _bioCableRequestedCutProgress01 = safeCutProgress01;
            _bioCableHoldTimer = math.isfinite(_bioCableHoldTime) ? math.max(0f, _bioCableHoldTime) : 0f;
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
            float qualityWeight01)
        {
            if (!_isActive || _owner == null || _payloadBody == null || (_playerMotor == null && _anchorBody == null))
                return TetherLifecycleState.Released;

            if (_owner.ShouldSuppressTow || !_owner.IsTowPayloadValid(_payloadBody, _payloadCollider))
                return TetherLifecycleState.Released;

            FinalizePendingVerletSolveNoWait(publishResults: true);
            _qualityWeight01 = SanitizeQualityWeight(qualityWeight01);
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
            UpdateVisuals(deltaTime, _qualityWeight01, null);
        }

        internal void UpdateVisuals(float deltaTime, float qualityWeight01, Plane[] frustumPlanes)
        {
            _visualCulledThisFrame = false;
            if (!_isActive)
                return;

            if (_pendingVerletSolveActive)
            {
                _visualCulledThisFrame = true;
                return;
            }

            _qualityWeight01 = SanitizeQualityWeight(qualityWeight01);
            if (VisualSegmentBuffer == null || VisualSegmentTensionBuffer == null || VisualDrawParamsBuffer == null)
                EnsureVisualBuffers(_verletNodeCount > 1 ? _verletNodeCount : ResolveVerletPointCount(_qualityWeight01));

            if (VisualSegmentBuffer == null || VisualSegmentTensionBuffer == null || VisualDrawParamsBuffer == null)
                return;

            int nodeCount = _verletNodeCount > 1 ? _verletNodeCount : ResolveVerletPointCount(_qualityWeight01);
            EnsureDataVaultCableState(nodeCount);
            nodeCount = ResolveDataVaultNodeCount(nodeCount);

            if (!TryResolveVerletPositions(nodeCount, out NativeArray<float3> _verletPositions))
                return;

            if (_verletPositions.IsCreated && _verletPositions.Length > 1)
            {
                UpdateVerletVisualUpload(_qualityWeight01, frustumPlanes);
                return;
            }

            bool visualGuardHeld = false;
            IDataVault visualGuardVault = null;
            bool uploadFallbackGpuBuffers = false;
            try
            {
                visualGuardVault = _dataVault;
                if (visualGuardVault == null ||
                    !visualGuardVault.TryAcquireMutationGuard(VisualFallbackMutationGuardMask))
                {
                    RecordVaultAccessFailure(BufferID.TetherVisualSegmentPositions, in _visualSegmentPositionsHandle, TetherTelemetryFailureLock, 0u);
                    RecordVaultAccessFailure(BufferID.TetherVisualAnchorPositions, in _visualAnchorPositionsHandle, TetherTelemetryFailureLock, 0u);
                    RecordVaultAccessFailure(BufferID.TetherVisualSegmentLengths, in _visualSegmentLengthsHandle, TetherTelemetryFailureLock, 0u);
                    return;
                }

                visualGuardHeld = true;
                bool visualPositionsResolved = TryResolveDataVaultCableSlice(
                    visualGuardVault,
                    in _visualSegmentPositionsHandle,
                    BufferID.TetherVisualSegmentPositions,
                    DataVaultScratchNodeCapacity,
                    _dataVaultSlot * DataVaultCablePointCount,
                    nodeCount,
                    out NativeArray<float3> _visualSegmentPositions);
                bool visualAnchorsResolved = TryResolveDataVaultCableSlice(
                    visualGuardVault,
                    in _visualAnchorPositionsHandle,
                    BufferID.TetherVisualAnchorPositions,
                    DataVaultVisualAnchorCapacity,
                    _dataVaultSlot * MaxAnchors,
                    MaxAnchors,
                    out NativeArray<float3> _visualAnchorPositions);
                bool visualLengthsResolved = TryResolveDataVaultCableSlice(
                    visualGuardVault,
                    in _visualSegmentLengthsHandle,
                    BufferID.TetherVisualSegmentLengths,
                    DataVaultVisualSegmentLengthCapacity,
                    _dataVaultSlot * MaxSegments,
                    MaxSegments,
                    out NativeArray<float> _visualSegmentLengths);
                if (!visualPositionsResolved || !visualAnchorsResolved || !visualLengthsResolved)
                {
                    if (!visualPositionsResolved)
                        RecordVaultAccessFailure(BufferID.TetherVisualSegmentPositions, in _visualSegmentPositionsHandle, TetherTelemetryFailureResolve, 0u);
                    if (!visualAnchorsResolved)
                        RecordVaultAccessFailure(BufferID.TetherVisualAnchorPositions, in _visualAnchorPositionsHandle, TetherTelemetryFailureResolve, 0u);
                    if (!visualLengthsResolved)
                        RecordVaultAccessFailure(BufferID.TetherVisualSegmentLengths, in _visualSegmentLengthsHandle, TetherTelemetryFailureResolve, 0u);
                    return;
                }

                Vector3 anchorPosition = _owner != null ? _owner.ResolveTowAnchorPosition() : Vector3.zero;
                Vector3 payloadPosition = _payloadBody != null ? _payloadBody.worldCenterOfMass : anchorPosition;
                ResolveSolverReferenceFrame();
                int anchorCount = BuildAnchorChain(anchorPosition, payloadPosition);
                if (anchorCount < 2)
                    return;

                float safeDeltaTime = math.isfinite(deltaTime) ? math.max(deltaTime, 0f) : 0f;
                float blendT = ResolveBlendFactor(_visualSegmentSmoothSpeed, safeDeltaTime);
                CopyVisualSolverState(anchorCount, _visualAnchorPositions, _visualSegmentLengths);
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
                    Vector3 blendedPointV3 = default;
                    blendedPointV3.x = blendedPoint.x;
                    blendedPointV3.y = blendedPoint.y;
                    blendedPointV3.z = blendedPoint.z;
                    minBounds = Vector3.Min(minBounds, blendedPointV3);
                    maxBounds = Vector3.Max(maxBounds, blendedPointV3);
                }

                _visualBounds.SetMinMax(minBounds, maxBounds);
                if (ShouldUploadVisualBounds(frustumPlanes))
                    uploadFallbackGpuBuffers = true;
                else
                    _visualCulledThisFrame = true;
                ResetVaultFailureStreak();
            }
            finally
            {
                if (visualGuardHeld && visualGuardVault != null)
                    visualGuardVault.ReleaseMutationGuard(VisualFallbackMutationGuardMask);
            }

            if (uploadFallbackGpuBuffers)
                UploadVisualGpuBuffers(includeTension: true);
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
                float3 sagOffset = default;
                sagOffset.y = -(sag * sagWeight);
                return SanitizeFinite(basePoint + sagOffset);
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
            FinalizePendingVerletSolveForBarrier(publishResults: false);
            GlobalPhysicsStateManager.UnregisterTetherConnection(this);
            ReleasePrimaryConstraint();
            DisposeDataVaultCableState();
            _isActive = false;
            _owner = null;
            _playerMotor = null;
            _anchorBody = null;
            _payloadBody = null;
            _payloadCollider = null;
            _voxelEngineRuntime = null;
            _voxelSdfReadModel = null;
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
            _visualBounds = default;
            _visualBounds.center = Vector3.zero;
            _visualBounds.size = Vector3.one;
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
            _consecutiveVaultAccessFailures = 0;
            _qualityTier = HectonQualityTier.Unknown;
            _qualityWeight01 = 1f;
            ClearBendMetadata(0);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Releases persistent native and GPU resources.
        /// </summary>
        public void DisposeRuntimeResources()
        {
            FinalizePendingVerletSolveForBarrier(publishResults: false);
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
            int nodeCount = ResolveDataVaultNodeCount(pointCount);
            int segmentCount = math.max(1, nodeCount - 1);
            if (!TryResolveVisualSegmentPositions(nodeCount, out NativeArray<float3> _visualSegmentPositions) ||
                !TryResolveVisualGpuPoints(nodeCount, out NativeArray<GpuCableSplinePointDTO> _visualSegmentGpuPoints) ||
                !TryResolveVisualAnchorPositions(out NativeArray<float3> _visualAnchorPositions) ||
                !TryResolveVisualSegmentLengths(out NativeArray<float> _visualSegmentLengths) ||
                !TryResolveVerletSegmentTensions(segmentCount, out NativeArray<float> _verletSegmentTensions))
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
            int nodeCount = ResolveDataVaultNodeCount(_verletNodeCount);
            int segmentCount = math.max(1, nodeCount - 1);
            if (!TryResolveVisualSegmentPositions(nodeCount, out NativeArray<float3> _visualSegmentPositions))
            {
                RecordVaultAccessFailure(BufferID.TetherVisualSegmentPositions, in _visualSegmentPositionsHandle, TetherTelemetryFailureResolve, 0u);
                return;
            }

            NativeArray<float> _verletSegmentTensions = default;
            bool hasTension = includeTension && TryResolveVerletSegmentTensions(segmentCount, out _verletSegmentTensions);
            bool gpuPointsLocked = TryAcquireDataVaultCableSlice(
                in _visualSegmentGpuPointsHandle,
                BufferID.VerletCableGpuSplinePoints,
                DataVaultScratchNodeCapacity,
                _dataVaultSlot * DataVaultCablePointCount,
                nodeCount,
                out NativeArray<GpuCableSplinePointDTO> _visualSegmentGpuPoints);
            if (!gpuPointsLocked)
            {
                RecordVaultAccessFailure(BufferID.VerletCableGpuSplinePoints, in _visualSegmentGpuPointsHandle, TetherTelemetryFailureLock, 0u);
                return;
            }

            try
            {
                int writeIndex = 1 - _visualGpuBufferIndex;
                GraphicsBuffer positionWriteBuffer = writeIndex == 0 ? _visualSegmentBufferA : _visualSegmentBufferB;
                if (positionWriteBuffer == null)
                    return;

                int pointCount = math.min(_visualSegmentPositions.Length, _visualSegmentGpuPoints.Length);
                PopulateVisualGpuSplinePoints(
                    _visualSegmentPositions,
                    hasTension ? _verletSegmentTensions : default,
                    _visualSegmentGpuPoints,
                    math.rcp(math.max(ResolveSnapTensionThreshold(), 1f)),
                    pointCount);

                GraphicsBufferUploadUtility.UploadNativeArray(
                    positionWriteBuffer,
                    _visualSegmentGpuPoints,
                    pointCount);

                if (hasTension && _verletSegmentTensions.IsCreated)
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
                ResetVaultFailureStreak();
            }
            finally
            {
                ReleaseDataVaultCableWriteLock(in _visualSegmentGpuPointsHandle, BufferID.VerletCableGpuSplinePoints);
            }
        }

        private static void PopulateVisualGpuSplinePoints(
            NativeArray<float3> positions,
            NativeArray<float> segmentTensions,
            NativeArray<GpuCableSplinePointDTO> gpuPoints,
            float invSnapTension,
            int pointCount)
        {
            if (!positions.IsCreated || !gpuPoints.IsCreated || pointCount <= 0)
                return;

            int safeCount = math.min(pointCount, math.min(positions.Length, gpuPoints.Length));
            float safeInvSnapTension = math.isfinite(invSnapTension) ? math.max(0f, invSnapTension) : 0f;
            for (int index = 0; index < safeCount; index++)
            {
                float tension = 0f;
                if (segmentTensions.IsCreated && segmentTensions.Length > 0)
                    tension = segmentTensions[math.min(index, segmentTensions.Length - 1)];

                GpuCableSplinePointDTO point = default;
                point.Position = positions[index];
                point.Tension01 = math.saturate(tension * safeInvSnapTension);
                gpuPoints[index] = point;
            }
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
            GpuCableDrawParamsDTO drawParams = default;
            drawParams.Color = math.float4(tetherColor.r, tetherColor.g, tetherColor.b, tetherColor.a);
            drawParams.StressColor = math.float4(tetherStressColor.r, tetherStressColor.g, tetherStressColor.b, tetherStressColor.a);
            drawParams.Params0 = math.float4(VisualStress01, safeSegmentStressScale, safePointCount, safeRadius);
            drawParams.Params1 = math.float4(useIndirect ? 1f : 0f, safeVisualTier, safeCrystalDensity, safeSiltIntensity);
            drawParams.Params2 = math.float4(safeVisualClock, 0f, 0f, 0f);

            NativeArray<GpuCableDrawParamsDTO> mapped = drawParamsWriteBuffer.LockBufferForWrite<GpuCableDrawParamsDTO>(0, 1);
            try
            {
                mapped[0] = drawParams;
            }
            finally
            {
                drawParamsWriteBuffer.UnlockBufferAfterWrite<GpuCableDrawParamsDTO>(1);
            }
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
            _verletNodeCount = _dataVaultCableStateReady ? ResolveDataVaultNodeCount(nodeCount) : 0;
        }

        private void InitializeVerletRuntime(Vector3 anchorPosition, Vector3 payloadPosition)
        {
            int nodeCount = ResolveDataVaultNodeCount(_verletNodeCount);
            int segmentCount = math.max(1, nodeCount - 1);
            int nodeOffset = _dataVaultSlot * DataVaultCablePointCount;
            int segmentOffset = _dataVaultSlot * DataVaultCableSegmentCount;
            int scalarOffset = _dataVaultSlot;
            bool verletBootstrapGuardHeld = false;
            IDataVault verletBootstrapGuardVault = null;

            try
            {
                verletBootstrapGuardVault = _dataVault;
                if (verletBootstrapGuardVault == null ||
                    !verletBootstrapGuardVault.TryAcquireMutationGuard(VerletBootstrapMutationGuardMask))
                {
                    return;
                }

                verletBootstrapGuardHeld = true;
                bool positionsResolved = TryResolveDataVaultCableSlice(
                    verletBootstrapGuardVault,
                    in _verletPositionsHandle,
                    BufferID.TetherVerletPositions,
                    DataVaultScratchNodeCapacity,
                    nodeOffset,
                    nodeCount,
                    out NativeArray<float3> _verletPositions);
                bool previousResolved = TryResolveDataVaultCableSlice(
                    verletBootstrapGuardVault,
                    in _verletPreviousPositionsHandle,
                    BufferID.TetherVerletPreviousPositions,
                    DataVaultScratchNodeCapacity,
                    nodeOffset,
                    nodeCount,
                    out NativeArray<float3> _verletPreviousPositions);
                bool velocitiesResolved = TryResolveDataVaultCableSlice(
                    verletBootstrapGuardVault,
                    in _verletVelocitiesHandle,
                    BufferID.TetherVerletVelocities,
                    DataVaultScratchNodeCapacity,
                    nodeOffset,
                    nodeCount,
                    out NativeArray<float3> _verletVelocities);
                bool pinnedPositionsResolved = TryResolveDataVaultCableSlice(
                    verletBootstrapGuardVault,
                    in _verletPinnedPositionsHandle,
                    BufferID.TetherVerletPinnedPositions,
                    DataVaultScratchNodeCapacity,
                    nodeOffset,
                    nodeCount,
                    out NativeArray<float3> _verletPinnedPositions);
                bool pinnedMaskResolved = TryResolveDataVaultCableSlice(
                    verletBootstrapGuardVault,
                    in _verletPinnedMaskHandle,
                    BufferID.TetherVerletPinnedMask,
                    DataVaultScratchNodeCapacity,
                    nodeOffset,
                    nodeCount,
                    out NativeArray<byte> _verletPinnedMask);
                bool nodeFaultFlagsResolved = TryResolveDataVaultCableSlice(
                    verletBootstrapGuardVault,
                    in _verletNodeFaultFlagsHandle,
                    BufferID.TetherVerletNodeFaultFlags,
                    DataVaultScratchNodeCapacity,
                    nodeOffset,
                    nodeCount,
                    out NativeArray<byte> _verletNodeFaultFlags);
                bool restLengthsResolved = TryResolveDataVaultCableSlice(
                    verletBootstrapGuardVault,
                    in _verletSegmentRestLengthsHandle,
                    BufferID.TetherVerletSegmentRestLengths,
                    DataVaultScratchSegmentCapacity,
                    segmentOffset,
                    segmentCount,
                    out NativeArray<float> _verletSegmentRestLengths);
                bool segmentTensionsResolved = TryResolveDataVaultCableSlice(
                    verletBootstrapGuardVault,
                    in _verletSegmentTensionsHandle,
                    BufferID.TetherVerletSegmentTensions,
                    DataVaultScratchSegmentCapacity,
                    segmentOffset,
                    segmentCount,
                    out NativeArray<float> _verletSegmentTensions);
                bool solverStatsResolved = TryResolveDataVaultCableSlice(
                    verletBootstrapGuardVault,
                    in _verletSolverStatsHandle,
                    BufferID.TetherVerletSolverStats,
                    DataVaultScratchScalarCapacity,
                    scalarOffset,
                    1,
                    out NativeArray<float> _verletSolverStats);
                bool solverFlagsResolved = TryResolveDataVaultCableSlice(
                    verletBootstrapGuardVault,
                    in _verletSolverFlagsHandle,
                    BufferID.TetherVerletSolverFlags,
                    DataVaultScratchScalarCapacity,
                    scalarOffset,
                    1,
                    out NativeArray<int> _verletSolverFlags);

                if (!positionsResolved ||
                    !previousResolved ||
                    !velocitiesResolved ||
                    !pinnedPositionsResolved ||
                    !pinnedMaskResolved ||
                    !nodeFaultFlagsResolved ||
                    !restLengthsResolved ||
                    !segmentTensionsResolved ||
                    !solverStatsResolved ||
                    !solverFlagsResolved ||
                    _verletPositions.Length < 2)
                {
                    return;
                }

                float3 anchor = default;
                anchor.x = anchorPosition.x;
                anchor.y = anchorPosition.y;
                anchor.z = anchorPosition.z;
                float3 payload = default;
                payload.x = payloadPosition.x;
                payload.y = payloadPosition.y;
                payload.z = payloadPosition.z;
                _verletSolverOrigin = SanitizeFinite(anchor);
                float3 localPayload = SanitizeFinite(payload - _verletSolverOrigin);
                nodeCount = _verletPositions.Length;
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
            }
            finally
            {
                if (verletBootstrapGuardHeld && verletBootstrapGuardVault != null)
                    verletBootstrapGuardVault.ReleaseMutationGuard(VerletBootstrapMutationGuardMask);
            }

            ClearDataVaultTelemetrySlot();
            int telemetryHeadIndex = ResolveTelemetryHeadIndex();
            bool telemetryHeadLocked = TryAcquireDataVaultCableSlice(
                in _verletTelemetryHeadHandle,
                BufferID.TetherCableBlackBoxHead,
                DataVaultTelemetryHeadCapacity,
                telemetryHeadIndex,
                1,
                out NativeArray<int> _verletTelemetryHead);
            if (telemetryHeadLocked)
            {
                try
                {
                    _verletTelemetryHead[0] = 0;
                }
                finally
                {
                    ReleaseDataVaultCableWriteLock(in _verletTelemetryHeadHandle, BufferID.TetherCableBlackBoxHead);
                }
            }

            _verletRuntimeInitialized = true;
        }

        private void RebaseVerletSolverOrigin(
            float3 nextOrigin,
            NativeArray<float3> verletPositions,
            NativeArray<float3> verletPreviousPositions,
            NativeArray<float3> verletPinnedPositions)
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

            if (!verletPositions.IsCreated ||
                !verletPreviousPositions.IsCreated ||
                !verletPinnedPositions.IsCreated)
            {
                return;
            }

            for (int i = 0; i < verletPositions.Length; i++)
            {
                verletPositions[i] = SanitizeFinite(verletPositions[i] + offset);
                if (i < verletPreviousPositions.Length)
                    verletPreviousPositions[i] = SanitizeFinite(verletPreviousPositions[i] + offset);
                if (i < verletPinnedPositions.Length)
                    verletPinnedPositions[i] = SanitizeFinite(verletPinnedPositions[i] + offset);
            }
        }

        private void EnsureDataVaultCableState(int requestedNodeCount = 0)
        {
            int nodeCount = ResolveDataVaultNodeCount(requestedNodeCount);
            _dataVault = _manager != null ? _manager.CachedDataVault : _dataVault;
            IDataVault vault = _dataVault;

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
                ref _dataVaultCablePositionsHandle,
                BufferID.TetherCablePositions,
                DataVaultCablePointCapacity,
                DataVaultFlagPositions);
            bool previousReady = EnsureDataVaultCableArray(
                ref _dataVaultCablePreviousPositionsHandle,
                BufferID.TetherCablePreviousPositions,
                DataVaultCablePointCapacity,
                DataVaultFlagPreviousPositions);
            bool velocitiesReady = EnsureDataVaultCableArray(
                ref _dataVaultCableVelocitiesHandle,
                BufferID.TetherCableVelocities,
                DataVaultCablePointCapacity,
                DataVaultFlagVelocities);
            bool massesReady = EnsureDataVaultCableArray(
                ref _dataVaultCableMassesHandle,
                BufferID.TetherCableMasses,
                DataVaultCablePointCapacity,
                DataVaultFlagMasses);
            bool tensionReady = EnsureDataVaultCableArray(
                ref _dataVaultCableSegmentTensionsHandle,
                BufferID.TetherCableSegmentTensions,
                DataVaultCableSegmentCapacity,
                DataVaultFlagSegmentTensions);
            bool telemetryReady = EnsureDataVaultCableArray(
                ref _verletTelemetryRingHandle,
                BufferID.TetherCableBlackBox,
                DataVaultTelemetryCapacity,
                DataVaultFlagTelemetryRing);
            bool telemetryHeadReady = EnsureDataVaultCableArray(
                ref _verletTelemetryHeadHandle,
                BufferID.TetherCableBlackBoxHead,
                DataVaultTelemetryHeadCapacity,
                DataVaultFlagTelemetryHead);
            bool tensionForcesReady = EnsureDataVaultCableArray(
                ref _verletTensionForcesHandle,
                BufferID.VerletCableTensionForces,
                DataVaultMaxTetherSlots,
                DataVaultFlagVerletTensionForces);
            bool tuningReady = EnsureDataVaultCableArray(
                ref _verletTuningHandle,
                BufferID.VerletCableTuning,
                1,
                DataVaultFlagVerletTuning);
            if (tuningReady)
                EnsureVerletTuningDefaults();
            bool visualPositionsReady = EnsureDataVaultSliceArray(
                ref _visualSegmentPositionsHandle,
                BufferID.TetherVisualSegmentPositions,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVisualSegmentPositions);
            bool visualGpuPointsReady = EnsureDataVaultSliceArray(
                ref _visualSegmentGpuPointsHandle,
                BufferID.VerletCableGpuSplinePoints,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVisualGpuSplinePoints);
            bool visualAnchorsReady = EnsureDataVaultSliceArray(
                ref _visualAnchorPositionsHandle,
                BufferID.TetherVisualAnchorPositions,
                DataVaultVisualAnchorCapacity,
                anchorOffset,
                MaxAnchors,
                DataVaultFlagVisualAnchorPositions);
            bool visualLengthsReady = EnsureDataVaultSliceArray(
                ref _visualSegmentLengthsHandle,
                BufferID.TetherVisualSegmentLengths,
                DataVaultVisualSegmentLengthCapacity,
                visualSegmentOffset,
                MaxSegments,
                DataVaultFlagVisualSegmentLengths);
            bool verletPositionsReady = EnsureDataVaultSliceArray(
                ref _verletPositionsHandle,
                BufferID.TetherVerletPositions,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVerletPositions);
            bool verletPreviousReady = EnsureDataVaultSliceArray(
                ref _verletPreviousPositionsHandle,
                BufferID.TetherVerletPreviousPositions,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVerletPreviousPositions);
            bool verletVelocitiesReady = EnsureDataVaultSliceArray(
                ref _verletVelocitiesHandle,
                BufferID.TetherVerletVelocities,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVerletVelocities);
            bool verletPinnedPositionsReady = EnsureDataVaultSliceArray(
                ref _verletPinnedPositionsHandle,
                BufferID.TetherVerletPinnedPositions,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVerletPinnedPositions);
            bool verletPinnedMaskReady = EnsureDataVaultSliceArray(
                ref _verletPinnedMaskHandle,
                BufferID.TetherVerletPinnedMask,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVerletPinnedMask);
            bool verletRestLengthsReady = EnsureDataVaultSliceArray(
                ref _verletSegmentRestLengthsHandle,
                BufferID.TetherVerletSegmentRestLengths,
                DataVaultScratchSegmentCapacity,
                segmentOffset,
                segmentCount,
                DataVaultFlagVerletSegmentRestLengths);
            bool verletTensionsReady = EnsureDataVaultSliceArray(
                ref _verletSegmentTensionsHandle,
                BufferID.TetherVerletSegmentTensions,
                DataVaultScratchSegmentCapacity,
                segmentOffset,
                segmentCount,
                DataVaultFlagVerletSegmentTensions);
            bool verletCorrectionsReady = EnsureDataVaultSliceArray(
                ref _verletCorrectionsHandle,
                BufferID.TetherVerletCorrections,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVerletCorrections);
            bool verletCorrectionWeightsReady = EnsureDataVaultSliceArray(
                ref _verletCorrectionWeightsHandle,
                BufferID.TetherVerletCorrectionWeights,
                DataVaultScratchNodeCapacity,
                nodeOffset,
                nodeCount,
                DataVaultFlagVerletCorrectionWeights);
            bool solverStatsReady = EnsureDataVaultSliceArray(
                ref _verletSolverStatsHandle,
                BufferID.TetherVerletSolverStats,
                DataVaultScratchScalarCapacity,
                scalarOffset,
                1,
                DataVaultFlagVerletSolverStats);
            bool solverFlagsReady = EnsureDataVaultSliceArray(
                ref _verletSolverFlagsHandle,
                BufferID.TetherVerletSolverFlags,
                DataVaultScratchScalarCapacity,
                scalarOffset,
                1,
                DataVaultFlagVerletSolverFlags);
            bool nodeFaultFlagsReady = EnsureDataVaultSliceArray(
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
        }

        private int ResolveDataVaultNodeCount(int requestedNodeCount)
        {
            int nodeCount = requestedNodeCount > 0
                ? requestedNodeCount
                : (_verletNodeCount > 1 ? _verletNodeCount : ResolveVerletPointCount(_qualityWeight01));
            return math.clamp(nodeCount, 2, DataVaultCablePointCount);
        }

        private bool EnsureDataVaultCableArray<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int length,
            int vaultFlag)
            where T : struct
        {
            if (length <= 0)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
            {
                ResetDataVaultCableView(ref handle, vaultFlag);
                return false;
            }

            if (OpenOrAcquireDataVaultCableBuffer(
                    vault,
                    ref handle,
                    bufferId,
                    length,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<T> vaultArray))
            {
                _dataVaultNativeStateMask |= vaultFlag;
                return true;
            }

            ResetDataVaultCableView(ref handle, vaultFlag);
            return false;
        }

        private bool EnsureDataVaultSliceArray<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int totalLength,
            int offset,
            int length,
            int vaultFlag)
            where T : struct
        {
            if (totalLength <= 0 ||
                offset < 0 ||
                length <= 0 ||
                offset > totalLength ||
                length > totalLength - offset)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
            {
                ResetDataVaultCableView(ref handle, vaultFlag);
                return false;
            }

            if (OpenOrAcquireDataVaultCableBuffer(
                    vault,
                    ref handle,
                    bufferId,
                    totalLength,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<T> vaultArray) &&
                offset <= vaultArray.Length &&
                length <= vaultArray.Length - offset)
            {
                _dataVaultNativeStateMask |= vaultFlag;
                return true;
            }

            ResetDataVaultCableView(ref handle, vaultFlag);
            return false;
        }

        private static bool OpenOrAcquireDataVaultCableBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            if (TryOpenDataVaultCableBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle))
                {
                    buffer = default;
                    return false;
                }

                return TryOpenDataVaultCableBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.Physics,
                options);
            return TryOpenDataVaultCableBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenDataVaultCableBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsDataVaultCableHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsDataVaultCableHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.Physics &&
                   handle.Generation != 0u;
        }

        private bool TryResolveDataVaultCableArray<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            return TryResolveDataVaultCableArray(_dataVault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryResolveDataVaultCableArray<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   requiredLength > 0 &&
                   IsDataVaultCableHandle(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryResolveDataVaultCableSlice<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int totalLength,
            int offset,
            int length,
            out NativeArray<T> slice)
            where T : struct
        {
            return TryResolveDataVaultCableSlice(_dataVault, in handle, bufferId, totalLength, offset, length, out slice);
        }

        private static bool TryResolveDataVaultCableSlice<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int totalLength,
            int offset,
            int length,
            out NativeArray<T> slice)
            where T : struct
        {
            slice = default;
            if (totalLength <= 0 ||
                offset < 0 ||
                length <= 0 ||
                offset > totalLength ||
                length > totalLength - offset)
                return false;

            if (!TryResolveDataVaultCableArray(vault, in handle, bufferId, totalLength, out NativeArray<T> buffer) ||
                offset > buffer.Length ||
                length > buffer.Length - offset)
            {
                return false;
            }

            slice = buffer.GetSubArray(offset, length);
            return slice.IsCreated && slice.Length == length;
        }

        private bool TryAcquireDataVaultCableArray<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                _dataVaultCableWriteVault != null ||
                requiredLength <= 0 ||
                !IsDataVaultCableHandle(in handle, bufferId) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.Physics, out buffer))
            {
                buffer = default;
                return false;
            }

            bool keepLock = false;
            try
            {
                if (buffer.IsCreated && buffer.Length >= requiredLength)
                {
                    _dataVaultCableWriteVault = vault;
                    keepLock = true;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (!keepLock)
                {
                    vault.ReleaseWriteLock(in handle, SystemID.Physics);
                    buffer = default;
                }
            }
        }

        private bool TryAcquireDataVaultCableSlice<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int totalLength,
            int offset,
            int length,
            out NativeArray<T> slice)
            where T : struct
        {
            slice = default;
            if (totalLength <= 0 ||
                offset < 0 ||
                length <= 0 ||
                offset > totalLength ||
                length > totalLength - offset)
                return false;

            if (!TryAcquireDataVaultCableArray(in handle, bufferId, totalLength, out NativeArray<T> buffer))
                return false;

            if (offset > buffer.Length ||
                length > buffer.Length - offset)
            {
                ReleaseDataVaultCableWriteLock(in handle, bufferId);
                return false;
            }

            slice = buffer.GetSubArray(offset, length);
            if (slice.IsCreated && slice.Length == length)
                return true;

            ReleaseDataVaultCableWriteLock(in handle, bufferId);
            slice = default;
            return false;
        }

        private void ReleaseDataVaultCableWriteLock<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : struct
        {
            IDataVault vault = _dataVaultCableWriteVault;
            _dataVaultCableWriteVault = null;
            if (vault != null && IsDataVaultCableHandle(in handle, bufferId))
                vault.ReleaseWriteLock(in handle, SystemID.Physics);
        }

        private void ResetVaultFailureStreak()
        {
            _consecutiveVaultAccessFailures = 0;
        }

        private void RecordVaultAccessFailure<T>(
            BufferID bufferId,
            in VaultGenerationHandle<T> handle,
            uint failureCode,
            uint faultFlags)
            where T : struct
        {
            if (_consecutiveVaultAccessFailures < int.MaxValue)
                _consecutiveVaultAccessFailures++;

            uint flags = ResolveVaultFailureFlags(failureCode, faultFlags);
            TryWriteVaultFailureTelemetry(
                (uint)bufferId,
                handle.BufferID,
                handle.Generation,
                failureCode,
                flags);

            if (_consecutiveVaultAccessFailures >= TetherTelemetryFailureDumpThreshold)
                DumpVerletTelemetryOnce(flags | (uint)TetherVerletFaultFlags.VaultFailureDumpRequested);
        }

        private void RecordVaultAccessFailureLockedTelemetry<T>(
            BufferID bufferId,
            in VaultGenerationHandle<T> handle,
            uint failureCode,
            uint faultFlags,
            NativeArray<TetherVerletTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryHead)
            where T : struct
        {
            if (_consecutiveVaultAccessFailures < int.MaxValue)
                _consecutiveVaultAccessFailures++;

            TryWriteVaultFailureTelemetryDirect(
                telemetryRing,
                telemetryHead,
                (uint)bufferId,
                handle.BufferID,
                handle.Generation,
                failureCode,
                ResolveVaultFailureFlags(failureCode, faultFlags));
        }

        private static uint ResolveVaultFailureFlags(uint failureCode, uint faultFlags)
        {
            uint flags = faultFlags;
            if (failureCode == TetherTelemetryFailureLock)
                return flags | (uint)TetherVerletFaultFlags.VaultLockFailed;

            if (failureCode == TetherTelemetryFailureLength)
                return flags | (uint)TetherVerletFaultFlags.VaultMetadataMismatch;

            return flags | (uint)TetherVerletFaultFlags.VaultResolveFailed;
        }

        private bool TryWriteVaultFailureTelemetry(
            uint expectedBufferId,
            uint observedBufferId,
            uint generation,
            uint failureCode,
            uint flags)
        {
            if (_dataVaultSlot < 0)
                return false;

            bool telemetryGuardHeld = false;
            IDataVault telemetryGuardVault = null;
            try
            {
                telemetryGuardVault = _dataVault;
                if (telemetryGuardVault == null ||
                    !telemetryGuardVault.TryAcquireMutationGuard(TelemetryMutationGuardMask))
                    return false;

                telemetryGuardHeld = true;
                bool telemetryRingResolved = TryResolveDataVaultCableArray(
                    telemetryGuardVault,
                    in _verletTelemetryRingHandle,
                    BufferID.TetherCableBlackBox,
                    DataVaultTelemetryCapacity,
                    out NativeArray<TetherVerletTelemetryEntry> telemetryRing);
                bool telemetryHeadResolved = TryResolveDataVaultCableArray(
                    telemetryGuardVault,
                    in _verletTelemetryHeadHandle,
                    BufferID.TetherCableBlackBoxHead,
                    DataVaultTelemetryHeadCapacity,
                    out NativeArray<int> telemetryHead);
                if (!telemetryRingResolved || !telemetryHeadResolved)
                    return false;

                return TryWriteVaultFailureTelemetryDirect(
                    telemetryRing,
                    telemetryHead,
                    expectedBufferId,
                    observedBufferId,
                    generation,
                    failureCode,
                    flags);
            }
            finally
            {
                if (telemetryGuardHeld && telemetryGuardVault != null)
                    telemetryGuardVault.ReleaseMutationGuard(TelemetryMutationGuardMask);
            }
        }

        private bool TryWriteVaultFailureTelemetryDirect(
            NativeArray<TetherVerletTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryHead,
            uint expectedBufferId,
            uint observedBufferId,
            uint generation,
            uint failureCode,
            uint flags)
        {
            if (!telemetryRing.IsCreated || !telemetryHead.IsCreated)
                return false;

            int telemetryOffset = ResolveTelemetryRingOffset();
            int telemetryHeadIndex = ResolveTelemetryHeadIndex();
            if ((uint)telemetryOffset >= (uint)telemetryRing.Length ||
                (uint)telemetryHeadIndex >= (uint)telemetryHead.Length)
            {
                return false;
            }

            int capacity = math.min(VerletTelemetryCapacity, telemetryRing.Length - telemetryOffset);
            if (capacity <= 0)
                return false;

            int localHead = telemetryHead[telemetryHeadIndex];
            if ((uint)localHead >= (uint)capacity)
                localHead = 0;

            TetherVerletTelemetryEntry entry = default;
            entry.FrameIndex = unchecked((uint)_currentSimulationFrameIndex);
            entry.NodeCount = math.max(_verletNodeCount, 0);
            entry.IterationCount = math.max(_lastVerletIterationCount, 0);
            entry.PeakConstraintDelta = math.isfinite(_lastVerletPeakDelta) ? math.max(0f, _lastVerletPeakDelta) : 0f;
            entry.PeakCableTension = math.isfinite(_primaryConstraintForceMagnitude) ? math.max(0f, _primaryConstraintForceMagnitude) : 0f;
            entry.AnchorPosition = float3.zero;
            entry.PayloadPosition = float3.zero;
            entry.Flags = flags;
            entry.BufferId = expectedBufferId;
            entry.Generation = generation;
            entry.FailureCode = failureCode;
            entry.Reserved0 = observedBufferId;
            telemetryRing[telemetryOffset + localHead] = entry;
            telemetryHead[telemetryHeadIndex] = (localHead + 1) % capacity;
            return true;
        }

        private void RecordSolverVaultAccessFailure<T>(
            BufferID bufferId,
            in VaultGenerationHandle<T> handle,
            uint failureCode,
            bool telemetryRingLocked,
            bool telemetryHeadLocked,
            NativeArray<TetherVerletTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryHead)
            where T : struct
        {
            if (telemetryRingLocked && telemetryHeadLocked)
            {
                RecordVaultAccessFailureLockedTelemetry(
                    bufferId,
                    in handle,
                    failureCode,
                    0u,
                    telemetryRing,
                    telemetryHead);
                return;
            }

            RecordVaultAccessFailure(bufferId, in handle, failureCode, 0u);
        }

        private bool TryResolveVisualSegmentPositions(int nodeCount, out NativeArray<float3> positions)
        {
            positions = default;
            return _dataVaultSlot >= 0 &&
                   TryResolveDataVaultCableSlice(
                       in _visualSegmentPositionsHandle,
                       BufferID.TetherVisualSegmentPositions,
                       DataVaultScratchNodeCapacity,
                       _dataVaultSlot * DataVaultCablePointCount,
                       nodeCount,
                       out positions);
        }

        private bool TryResolveVisualGpuPoints(int nodeCount, out NativeArray<GpuCableSplinePointDTO> points)
        {
            points = default;
            return _dataVaultSlot >= 0 &&
                   TryResolveDataVaultCableSlice(
                       in _visualSegmentGpuPointsHandle,
                       BufferID.VerletCableGpuSplinePoints,
                       DataVaultScratchNodeCapacity,
                       _dataVaultSlot * DataVaultCablePointCount,
                       nodeCount,
                       out points);
        }

        private bool TryResolveVisualAnchorPositions(out NativeArray<float3> anchors)
        {
            anchors = default;
            return _dataVaultSlot >= 0 &&
                   TryResolveDataVaultCableSlice(
                       in _visualAnchorPositionsHandle,
                       BufferID.TetherVisualAnchorPositions,
                       DataVaultVisualAnchorCapacity,
                       _dataVaultSlot * MaxAnchors,
                       MaxAnchors,
                       out anchors);
        }

        private bool TryResolveVisualSegmentLengths(out NativeArray<float> lengths)
        {
            lengths = default;
            return _dataVaultSlot >= 0 &&
                   TryResolveDataVaultCableSlice(
                       in _visualSegmentLengthsHandle,
                       BufferID.TetherVisualSegmentLengths,
                       DataVaultVisualSegmentLengthCapacity,
                       _dataVaultSlot * MaxSegments,
                       MaxSegments,
                       out lengths);
        }

        private bool TryResolveVerletPositions(int nodeCount, out NativeArray<float3> positions)
        {
            positions = default;
            return _dataVaultSlot >= 0 &&
                   TryResolveDataVaultCableSlice(
                       in _verletPositionsHandle,
                       BufferID.TetherVerletPositions,
                       DataVaultScratchNodeCapacity,
                       _dataVaultSlot * DataVaultCablePointCount,
                       nodeCount,
                       out positions);
        }

        private bool TryResolveVerletPreviousPositions(int nodeCount, out NativeArray<float3> previous)
        {
            previous = default;
            return _dataVaultSlot >= 0 &&
                   TryResolveDataVaultCableSlice(
                       in _verletPreviousPositionsHandle,
                       BufferID.TetherVerletPreviousPositions,
                       DataVaultScratchNodeCapacity,
                       _dataVaultSlot * DataVaultCablePointCount,
                       nodeCount,
                       out previous);
        }

        private bool TryResolveVerletVelocities(int nodeCount, out NativeArray<float3> velocities)
        {
            velocities = default;
            return _dataVaultSlot >= 0 &&
                   TryResolveDataVaultCableSlice(
                       in _verletVelocitiesHandle,
                       BufferID.TetherVerletVelocities,
                       DataVaultScratchNodeCapacity,
                       _dataVaultSlot * DataVaultCablePointCount,
                       nodeCount,
                       out velocities);
        }

        private bool TryResolveVerletPinnedPositions(int nodeCount, out NativeArray<float3> pinned)
        {
            pinned = default;
            return _dataVaultSlot >= 0 &&
                   TryResolveDataVaultCableSlice(
                       in _verletPinnedPositionsHandle,
                       BufferID.TetherVerletPinnedPositions,
                       DataVaultScratchNodeCapacity,
                       _dataVaultSlot * DataVaultCablePointCount,
                       nodeCount,
                       out pinned);
        }

        private bool TryResolveVerletPinnedMask(int nodeCount, out NativeArray<byte> mask)
        {
            mask = default;
            return _dataVaultSlot >= 0 &&
                   TryResolveDataVaultCableSlice(
                       in _verletPinnedMaskHandle,
                       BufferID.TetherVerletPinnedMask,
                       DataVaultScratchNodeCapacity,
                       _dataVaultSlot * DataVaultCablePointCount,
                       nodeCount,
                       out mask);
        }

        private bool TryResolveVerletSegmentRestLengths(int segmentCount, out NativeArray<float> restLengths)
        {
            restLengths = default;
            return _dataVaultSlot >= 0 &&
                   TryResolveDataVaultCableSlice(
                       in _verletSegmentRestLengthsHandle,
                       BufferID.TetherVerletSegmentRestLengths,
                       DataVaultScratchSegmentCapacity,
                       _dataVaultSlot * DataVaultCableSegmentCount,
                       segmentCount,
                       out restLengths);
        }

        private bool TryResolveVerletSegmentTensions(int segmentCount, out NativeArray<float> tensions)
        {
            tensions = default;
            return _dataVaultSlot >= 0 &&
                   TryResolveDataVaultCableSlice(
                       in _verletSegmentTensionsHandle,
                       BufferID.TetherVerletSegmentTensions,
                       DataVaultScratchSegmentCapacity,
                       _dataVaultSlot * DataVaultCableSegmentCount,
                       segmentCount,
                       out tensions);
        }

        private bool TryResolveVerletCorrections(int nodeCount, out NativeArray<float3> corrections)
        {
            corrections = default;
            return _dataVaultSlot >= 0 &&
                   TryResolveDataVaultCableSlice(
                       in _verletCorrectionsHandle,
                       BufferID.TetherVerletCorrections,
                       DataVaultScratchNodeCapacity,
                       _dataVaultSlot * DataVaultCablePointCount,
                       nodeCount,
                       out corrections);
        }

        private bool TryResolveVerletCorrectionWeights(int nodeCount, out NativeArray<float> weights)
        {
            weights = default;
            return _dataVaultSlot >= 0 &&
                   TryResolveDataVaultCableSlice(
                       in _verletCorrectionWeightsHandle,
                       BufferID.TetherVerletCorrectionWeights,
                       DataVaultScratchNodeCapacity,
                       _dataVaultSlot * DataVaultCablePointCount,
                       nodeCount,
                       out weights);
        }

        private bool TryResolveVerletNodeFaultFlags(int nodeCount, out NativeArray<byte> flags)
        {
            flags = default;
            return _dataVaultSlot >= 0 &&
                   TryResolveDataVaultCableSlice(
                       in _verletNodeFaultFlagsHandle,
                       BufferID.TetherVerletNodeFaultFlags,
                       DataVaultScratchNodeCapacity,
                       _dataVaultSlot * DataVaultCablePointCount,
                       nodeCount,
                       out flags);
        }

        private bool TryResolveVerletSolverStats(out NativeArray<float> stats)
        {
            stats = default;
            return _dataVaultSlot >= 0 &&
                   TryResolveDataVaultCableSlice(
                       in _verletSolverStatsHandle,
                       BufferID.TetherVerletSolverStats,
                       DataVaultScratchScalarCapacity,
                       _dataVaultSlot,
                       1,
                       out stats);
        }

        private bool TryResolveVerletSolverFlags(out NativeArray<int> flags)
        {
            flags = default;
            return _dataVaultSlot >= 0 &&
                   TryResolveDataVaultCableSlice(
                       in _verletSolverFlagsHandle,
                       BufferID.TetherVerletSolverFlags,
                       DataVaultScratchScalarCapacity,
                       _dataVaultSlot,
                       1,
                       out flags);
        }

        private bool TryResolveTelemetryRing(out NativeArray<TetherVerletTelemetryEntry> ring)
        {
            return TryResolveDataVaultCableArray(
                in _verletTelemetryRingHandle,
                BufferID.TetherCableBlackBox,
                DataVaultTelemetryCapacity,
                out ring);
        }

        private bool TryResolveTelemetryHead(out NativeArray<int> head)
        {
            return TryResolveDataVaultCableArray(
                in _verletTelemetryHeadHandle,
                BufferID.TetherCableBlackBoxHead,
                DataVaultTelemetryHeadCapacity,
                out head);
        }

        private bool TryResolveVerletTensionForces(out NativeArray<CableTensionForceDTO> forces)
        {
            return TryResolveDataVaultCableArray(
                in _verletTensionForcesHandle,
                BufferID.VerletCableTensionForces,
                DataVaultMaxTetherSlots,
                out forces);
        }

        private bool TryResolveVerletTuning(out NativeArray<VerletCableTuningDTO> tuning)
        {
            return TryResolveDataVaultCableArray(
                in _verletTuningHandle,
                BufferID.VerletCableTuning,
                1,
                out tuning);
        }

        private void DisposeDataVaultCableState()
        {
            ClearDataVaultCableEntry();
            ClearDataVaultTelemetrySlot();
            DisposeDataVaultCableArray(ref _dataVaultCablePositionsHandle, DataVaultFlagPositions);
            DisposeDataVaultCableArray(ref _dataVaultCablePreviousPositionsHandle, DataVaultFlagPreviousPositions);
            DisposeDataVaultCableArray(ref _dataVaultCableVelocitiesHandle, DataVaultFlagVelocities);
            DisposeDataVaultCableArray(ref _dataVaultCableMassesHandle, DataVaultFlagMasses);
            DisposeDataVaultCableArray(ref _dataVaultCableSegmentTensionsHandle, DataVaultFlagSegmentTensions);
            DisposeDataVaultCableArray(ref _verletTelemetryRingHandle, DataVaultFlagTelemetryRing);
            DisposeDataVaultCableArray(ref _verletTelemetryHeadHandle, DataVaultFlagTelemetryHead);
            DisposeDataVaultCableArray(ref _verletTensionForcesHandle, DataVaultFlagVerletTensionForces);
            DisposeDataVaultCableArray(ref _verletTuningHandle, DataVaultFlagVerletTuning);
            DisposeDataVaultCableArray(ref _visualSegmentPositionsHandle, DataVaultFlagVisualSegmentPositions);
            DisposeDataVaultCableArray(ref _visualSegmentGpuPointsHandle, DataVaultFlagVisualGpuSplinePoints);
            DisposeDataVaultCableArray(ref _visualAnchorPositionsHandle, DataVaultFlagVisualAnchorPositions);
            DisposeDataVaultCableArray(ref _visualSegmentLengthsHandle, DataVaultFlagVisualSegmentLengths);
            DisposeDataVaultCableArray(ref _verletPositionsHandle, DataVaultFlagVerletPositions);
            DisposeDataVaultCableArray(ref _verletPreviousPositionsHandle, DataVaultFlagVerletPreviousPositions);
            DisposeDataVaultCableArray(ref _verletVelocitiesHandle, DataVaultFlagVerletVelocities);
            DisposeDataVaultCableArray(ref _verletPinnedPositionsHandle, DataVaultFlagVerletPinnedPositions);
            DisposeDataVaultCableArray(ref _verletPinnedMaskHandle, DataVaultFlagVerletPinnedMask);
            DisposeDataVaultCableArray(ref _verletSegmentRestLengthsHandle, DataVaultFlagVerletSegmentRestLengths);
            DisposeDataVaultCableArray(ref _verletSegmentTensionsHandle, DataVaultFlagVerletSegmentTensions);
            DisposeDataVaultCableArray(ref _verletCorrectionsHandle, DataVaultFlagVerletCorrections);
            DisposeDataVaultCableArray(ref _verletCorrectionWeightsHandle, DataVaultFlagVerletCorrectionWeights);
            DisposeDataVaultCableArray(ref _verletSolverStatsHandle, DataVaultFlagVerletSolverStats);
            DisposeDataVaultCableArray(ref _verletSolverFlagsHandle, DataVaultFlagVerletSolverFlags);
            DisposeDataVaultCableArray(ref _verletNodeFaultFlagsHandle, DataVaultFlagVerletNodeFaultFlags);
            ReleaseDataVaultSlot();
            _dataVault = null;
            _dataVaultCableStateReady = false;
        }

        private void DisposeDataVaultCableArray<T>(
            ref VaultGenerationHandle<T> handle,
            int vaultFlag)
            where T : struct
        {
            handle = default;
            _dataVaultNativeStateMask &= ~vaultFlag;
        }

        private void ResetDataVaultCableView<T>(
            ref VaultGenerationHandle<T> handle,
            int vaultFlag)
            where T : struct
        {
            handle = default;
            _dataVaultNativeStateMask &= ~vaultFlag;
        }

        private void EnsureVerletTuningDefaults()
        {
            bool tuningLocked = TryAcquireDataVaultCableArray(
                in _verletTuningHandle,
                BufferID.VerletCableTuning,
                1,
                out NativeArray<VerletCableTuningDTO> _verletTuning);
            if (!tuningLocked)
                return;

            if (_verletTuning.Length == 0)
            {
                ReleaseDataVaultCableWriteLock(in _verletTuningHandle, BufferID.VerletCableTuning);
                return;
            }

            try
            {
                VerletCableTuningDTO tuning = _verletTuning[0];
                bool uninitialized = math.lengthsq(tuning.Gravity) <= 0.000001f &&
                                     tuning.ConstraintIterations == 0 &&
                                     tuning.BreakForce <= 0f;
                if (!uninitialized)
                    return;

                _verletTuning[0] = CreateDefaultVerletTuning();
            }
            finally
            {
                ReleaseDataVaultCableWriteLock(in _verletTuningHandle, BufferID.VerletCableTuning);
            }
        }

        private VerletCableTuningDTO ResolveVerletTuning()
        {
            if (TryResolveVerletTuning(out NativeArray<VerletCableTuningDTO> _verletTuning) &&
                _verletTuning.Length > 0)
            {
                return _verletTuning[0];
            }

            return CreateDefaultVerletTuning();
        }

        private static VerletCableTuningDTO CreateDefaultVerletTuning()
        {
            VerletCableTuningDTO tuning = default;
            tuning.Gravity.y = -HectonPhysicsContract.GravityMetersPerSecondSquaredConst;
            tuning.FluidFriction = VerletMidVelocityDamping;
            tuning.ConstraintIterations = 0;
            tuning.StretchThreshold01 = VerletPlasticStretch01;
            tuning.BreakForce = 0f;
            tuning.RockFriction01 = VerletRockFriction01;
            tuning.ReelSpeedMetersPerSecond = VerletReelSpeedMetersPerSecond;
            tuning.Reserved0 = 0f;
            tuning.Reserved1 = 0f;
            return tuning;
        }

        private bool TryAcquireDataVaultSlot()
        {
            for (int i = 0; i < DataVaultMaxTetherSlots; i++)
            {
                long slotMask = 1L << i;
                while (true)
                {
                    long observed = System.Threading.Volatile.Read(ref s_dataVaultSlotReservationMask);
                    if ((observed & slotMask) != 0L)
                        break;

                    long updated = observed | slotMask;
                    if (System.Threading.Interlocked.CompareExchange(
                            ref s_dataVaultSlotReservationMask,
                            updated,
                            observed) == observed)
                    {
                        _dataVaultSlot = i;
                        return true;
                    }
                }
            }

            return false;
        }

        private void ReleaseDataVaultSlot()
        {
            if (_dataVaultSlot >= 0 && _dataVaultSlot < DataVaultMaxTetherSlots)
            {
                long slotMask = 1L << _dataVaultSlot;
                while (true)
                {
                    long observed = System.Threading.Volatile.Read(ref s_dataVaultSlotReservationMask);
                    long updated = observed & ~slotMask;
                    if (updated == observed ||
                        System.Threading.Interlocked.CompareExchange(
                            ref s_dataVaultSlotReservationMask,
                            updated,
                            observed) == observed)
                    {
                        break;
                    }
                }
            }

            _dataVaultSlot = -1;
            _dataVaultCableStateReady = false;
        }

        private void PublishDataVaultCableState(float fixedDeltaTime, float peakTension)
        {
            if (!_dataVaultCableStateReady || _dataVaultSlot < 0)
                EnsureDataVaultCableState();

            if (!_dataVaultCableStateReady || _dataVaultSlot < 0)
                return;

            int nodeCount = ResolveDataVaultNodeCount(_verletNodeCount);
            int verletSegmentCount = math.max(1, nodeCount - 1);
            if (!TryResolveVerletPositions(nodeCount, out NativeArray<float3> _verletPositions) ||
                !TryResolveVerletPreviousPositions(nodeCount, out NativeArray<float3> _verletPreviousPositions) ||
                !TryResolveVerletSegmentTensions(verletSegmentCount, out NativeArray<float> _verletSegmentTensions))
            {
                ClearDataVaultCableEntry();
                return;
            }

            bool cableStateGuardHeld = false;
            IDataVault cableStateGuardVault = null;
            bool clearAfterRelease = false;

            try
            {
                cableStateGuardVault = _dataVault;
                if (cableStateGuardVault == null ||
                    !cableStateGuardVault.TryAcquireMutationGuard(CableStateMutationGuardMask))
                {
                    return;
                }

                cableStateGuardHeld = true;
                bool positionsResolved = TryResolveDataVaultCableArray(
                    cableStateGuardVault,
                    in _dataVaultCablePositionsHandle,
                    BufferID.TetherCablePositions,
                    DataVaultCablePointCapacity,
                    out NativeArray<float3> _dataVaultCablePositions);
                bool previousResolved = TryResolveDataVaultCableArray(
                    cableStateGuardVault,
                    in _dataVaultCablePreviousPositionsHandle,
                    BufferID.TetherCablePreviousPositions,
                    DataVaultCablePointCapacity,
                    out NativeArray<float3> _dataVaultCablePreviousPositions);
                bool velocitiesResolved = TryResolveDataVaultCableArray(
                    cableStateGuardVault,
                    in _dataVaultCableVelocitiesHandle,
                    BufferID.TetherCableVelocities,
                    DataVaultCablePointCapacity,
                    out NativeArray<float3> _dataVaultCableVelocities);
                bool massesResolved = TryResolveDataVaultCableArray(
                    cableStateGuardVault,
                    in _dataVaultCableMassesHandle,
                    BufferID.TetherCableMasses,
                    DataVaultCablePointCapacity,
                    out NativeArray<float> _dataVaultCableMasses);
                bool segmentTensionsResolved = TryResolveDataVaultCableArray(
                    cableStateGuardVault,
                    in _dataVaultCableSegmentTensionsHandle,
                    BufferID.TetherCableSegmentTensions,
                    DataVaultCableSegmentCapacity,
                    out NativeArray<float> _dataVaultCableSegmentTensions);

                if (!positionsResolved ||
                    !previousResolved ||
                    !velocitiesResolved ||
                    !massesResolved ||
                    !segmentTensionsResolved)
                {
                    return;
                }

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
                float playerMass = ResolvePlayerAnchorMassKg();
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
                    float stretch = SampleCanonicalCableTension(_verletSegmentTensions, i);
                    _dataVaultCableSegmentTensions[segmentOffset + i] = math.max(0f, stretch * math.max(0f, _springStiffness));
                }

                bool tensionForcesResolved = TryResolveDataVaultCableArray(
                    cableStateGuardVault,
                    in _verletTensionForcesHandle,
                    BufferID.VerletCableTensionForces,
                    DataVaultMaxTetherSlots,
                    out NativeArray<CableTensionForceDTO> _verletTensionForces);
                if (tensionForcesResolved && (uint)_dataVaultSlot < (uint)_verletTensionForces.Length)
                {
                    float3 anchor = _verletSolverOrigin;
                    float3 payload = _verletSolverOrigin + SampleCanonicalCablePoint(_verletPositions, DataVaultCablePointCount - 1);
                    float3 delta = payload - anchor;
                    float deltaLengthSq = math.lengthsq(delta);
                    float3 direction = math.isfinite(deltaLengthSq) && deltaLengthSq > MinVectorMagnitudeSq
                        ? delta * math.rsqrt(deltaLengthSq)
                        : float3.zero;
                    float safePeakTension = math.isfinite(peakTension) ? math.max(0f, peakTension) : 0f;
                    CableTensionForceDTO tensionForce = default;
                    tensionForce.Force = direction * safePeakTension;
                    tensionForce.ApplicationPoint = anchor;
                    tensionForce.Tension = safePeakTension;
                    tensionForce.CableId = unchecked((int)EntityId.ToULong(GetEntityId()));
                    _verletTensionForces[_dataVaultSlot] = tensionForce;
                }

                clearAfterRelease = !math.isfinite(peakTension);
            }
            finally
            {
                if (cableStateGuardHeld && cableStateGuardVault != null)
                    cableStateGuardVault.ReleaseMutationGuard(CableStateMutationGuardMask);
            }

            if (clearAfterRelease)
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
            if (!TryResolveTelemetryRing(out NativeArray<TetherVerletTelemetryEntry> _verletTelemetryRing))
                return 0;

            int offset = ResolveTelemetryRingOffset();
            if ((uint)offset >= (uint)_verletTelemetryRing.Length)
                return 0;

            return math.min(VerletTelemetryCapacity, _verletTelemetryRing.Length - offset);
        }

        private void ClearDataVaultTelemetrySlot()
        {
            bool telemetryRingLocked = TryAcquireDataVaultCableArray(
                in _verletTelemetryRingHandle,
                BufferID.TetherCableBlackBox,
                DataVaultTelemetryCapacity,
                out NativeArray<TetherVerletTelemetryEntry> _verletTelemetryRing);
            if (!telemetryRingLocked)
                return;

            try
            {
                int offset = ResolveTelemetryRingOffset();
                if ((uint)offset >= (uint)_verletTelemetryRing.Length)
                    return;

                int capacity = math.min(VerletTelemetryCapacity, _verletTelemetryRing.Length - offset);
                for (int i = 0; i < capacity; i++)
                    _verletTelemetryRing[offset + i] = default;
            }
            finally
            {
                ReleaseDataVaultCableWriteLock(in _verletTelemetryRingHandle, BufferID.TetherCableBlackBox);
            }
        }

        private void ClearDataVaultCableEntry()
        {
            if (_dataVaultSlot < 0)
                return;

            bool cableStateGuardHeld = false;
            IDataVault cableStateGuardVault = null;

            try
            {
                cableStateGuardVault = _dataVault;
                if (cableStateGuardVault == null ||
                    !cableStateGuardVault.TryAcquireMutationGuard(CableStateMutationGuardMask))
                {
                    return;
                }

                cableStateGuardHeld = true;
                bool positionsResolved = TryResolveDataVaultCableArray(
                    cableStateGuardVault,
                    in _dataVaultCablePositionsHandle,
                    BufferID.TetherCablePositions,
                    DataVaultCablePointCapacity,
                    out NativeArray<float3> _dataVaultCablePositions);
                bool previousResolved = TryResolveDataVaultCableArray(
                    cableStateGuardVault,
                    in _dataVaultCablePreviousPositionsHandle,
                    BufferID.TetherCablePreviousPositions,
                    DataVaultCablePointCapacity,
                    out NativeArray<float3> _dataVaultCablePreviousPositions);
                bool velocitiesResolved = TryResolveDataVaultCableArray(
                    cableStateGuardVault,
                    in _dataVaultCableVelocitiesHandle,
                    BufferID.TetherCableVelocities,
                    DataVaultCablePointCapacity,
                    out NativeArray<float3> _dataVaultCableVelocities);
                bool massesResolved = TryResolveDataVaultCableArray(
                    cableStateGuardVault,
                    in _dataVaultCableMassesHandle,
                    BufferID.TetherCableMasses,
                    DataVaultCablePointCapacity,
                    out NativeArray<float> _dataVaultCableMasses);
                bool segmentTensionsResolved = TryResolveDataVaultCableArray(
                    cableStateGuardVault,
                    in _dataVaultCableSegmentTensionsHandle,
                    BufferID.TetherCableSegmentTensions,
                    DataVaultCableSegmentCapacity,
                    out NativeArray<float> _dataVaultCableSegmentTensions);

                if (!positionsResolved ||
                    !previousResolved ||
                    !velocitiesResolved ||
                    !massesResolved ||
                    !segmentTensionsResolved)
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

                bool tensionForcesResolved = TryResolveDataVaultCableArray(
                    cableStateGuardVault,
                    in _verletTensionForcesHandle,
                    BufferID.VerletCableTensionForces,
                    DataVaultMaxTetherSlots,
                    out NativeArray<CableTensionForceDTO> _verletTensionForces);
                if (tensionForcesResolved && (uint)_dataVaultSlot < (uint)_verletTensionForces.Length)
                    _verletTensionForces[_dataVaultSlot] = default;
            }
            finally
            {
                if (cableStateGuardHeld && cableStateGuardVault != null)
                    cableStateGuardVault.ReleaseMutationGuard(CableStateMutationGuardMask);
            }
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

        private static float SampleCanonicalCableTension(NativeArray<float> _verletSegmentTensions, int canonicalSegmentIndex)
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
            if (!FinalizePendingVerletSolveNoWait(publishResults: true))
                return ResolvePrimaryConstraintForceMagnitude();

            EnsureDataVaultCableState(_verletNodeCount > 1 ? _verletNodeCount : ResolveVerletPointCount(_qualityWeight01));
            int nodeCount = ResolveDataVaultNodeCount(_verletNodeCount > 1 ? _verletNodeCount : ResolveVerletPointCount(_qualityWeight01));
            int segmentCount = math.max(1, nodeCount - 1);

            NativeArray<float3> _verletPositions;
            if (!_verletRuntimeInitialized ||
                !TryResolveVerletPositions(nodeCount, out _verletPositions) ||
                _verletPositions.Length < 2)
            {
                InitializeVerletRuntime(anchorPosition, payloadPosition);
            }

            if (!_verletRuntimeInitialized)
            {
                SyncPrimaryConstraint(anchorPosition, payloadPosition);
                return ResolvePrimaryConstraintForceMagnitude();
            }

            int nodeOffset = _dataVaultSlot * DataVaultCablePointCount;
            int segmentOffset = _dataVaultSlot * DataVaultCableSegmentCount;
            int scalarOffset = _dataVaultSlot;
            bool solveGuardHeld = false;
            bool solveGuardTransferred = false;
            IDataVault solveGuardVault = null;

            try
            {
                solveGuardVault = _dataVault;
                if (solveGuardVault == null ||
                    !solveGuardVault.TryAcquireMutationGuard(VerletSolveMutationGuardMask))
                {
                    RecordVaultAccessFailure(BufferID.TetherVerletPositions, in _verletPositionsHandle, TetherTelemetryFailureLock, 0u);
                    SyncPrimaryConstraint(anchorPosition, payloadPosition);
                    return ResolvePrimaryConstraintForceMagnitude();
                }

                solveGuardHeld = true;
                bool positionsResolved = TryResolveDataVaultCableSlice(
                    solveGuardVault,
                    in _verletPositionsHandle,
                    BufferID.TetherVerletPositions,
                    DataVaultScratchNodeCapacity,
                    nodeOffset,
                    nodeCount,
                    out _verletPositions);
                bool previousResolved = TryResolveDataVaultCableSlice(
                    solveGuardVault,
                    in _verletPreviousPositionsHandle,
                    BufferID.TetherVerletPreviousPositions,
                    DataVaultScratchNodeCapacity,
                    nodeOffset,
                    nodeCount,
                    out NativeArray<float3> _verletPreviousPositions);
                bool velocitiesResolved = TryResolveDataVaultCableSlice(
                    solveGuardVault,
                    in _verletVelocitiesHandle,
                    BufferID.TetherVerletVelocities,
                    DataVaultScratchNodeCapacity,
                    nodeOffset,
                    nodeCount,
                    out NativeArray<float3> _verletVelocities);
                bool pinnedPositionsResolved = TryResolveDataVaultCableSlice(
                    solveGuardVault,
                    in _verletPinnedPositionsHandle,
                    BufferID.TetherVerletPinnedPositions,
                    DataVaultScratchNodeCapacity,
                    nodeOffset,
                    nodeCount,
                    out NativeArray<float3> _verletPinnedPositions);
                bool pinnedMaskResolved = TryResolveDataVaultCableSlice(
                    solveGuardVault,
                    in _verletPinnedMaskHandle,
                    BufferID.TetherVerletPinnedMask,
                    DataVaultScratchNodeCapacity,
                    nodeOffset,
                    nodeCount,
                    out NativeArray<byte> _verletPinnedMask);
                bool nodeFaultFlagsResolved = TryResolveDataVaultCableSlice(
                    solveGuardVault,
                    in _verletNodeFaultFlagsHandle,
                    BufferID.TetherVerletNodeFaultFlags,
                    DataVaultScratchNodeCapacity,
                    nodeOffset,
                    nodeCount,
                    out NativeArray<byte> _verletNodeFaultFlags);
                bool restLengthsResolved = TryResolveDataVaultCableSlice(
                    solveGuardVault,
                    in _verletSegmentRestLengthsHandle,
                    BufferID.TetherVerletSegmentRestLengths,
                    DataVaultScratchSegmentCapacity,
                    segmentOffset,
                    segmentCount,
                    out NativeArray<float> _verletSegmentRestLengths);
                bool segmentTensionsResolved = TryResolveDataVaultCableSlice(
                    solveGuardVault,
                    in _verletSegmentTensionsHandle,
                    BufferID.TetherVerletSegmentTensions,
                    DataVaultScratchSegmentCapacity,
                    segmentOffset,
                    segmentCount,
                    out NativeArray<float> _verletSegmentTensions);
                bool correctionsResolved = TryResolveDataVaultCableSlice(
                    solveGuardVault,
                    in _verletCorrectionsHandle,
                    BufferID.TetherVerletCorrections,
                    DataVaultScratchNodeCapacity,
                    nodeOffset,
                    nodeCount,
                    out NativeArray<float3> _verletCorrections);
                bool correctionWeightsResolved = TryResolveDataVaultCableSlice(
                    solveGuardVault,
                    in _verletCorrectionWeightsHandle,
                    BufferID.TetherVerletCorrectionWeights,
                    DataVaultScratchNodeCapacity,
                    nodeOffset,
                    nodeCount,
                    out NativeArray<float> _verletCorrectionWeights);
                bool solverStatsResolved = TryResolveDataVaultCableSlice(
                    solveGuardVault,
                    in _verletSolverStatsHandle,
                    BufferID.TetherVerletSolverStats,
                    DataVaultScratchScalarCapacity,
                    scalarOffset,
                    1,
                    out NativeArray<float> _verletSolverStats);
                bool solverFlagsResolved = TryResolveDataVaultCableSlice(
                    solveGuardVault,
                    in _verletSolverFlagsHandle,
                    BufferID.TetherVerletSolverFlags,
                    DataVaultScratchScalarCapacity,
                    scalarOffset,
                    1,
                    out NativeArray<int> _verletSolverFlags);
                bool telemetryRingResolved = TryResolveDataVaultCableArray(
                    solveGuardVault,
                    in _verletTelemetryRingHandle,
                    BufferID.TetherCableBlackBox,
                    DataVaultTelemetryCapacity,
                    out NativeArray<TetherVerletTelemetryEntry> _verletTelemetryRing);
                bool telemetryHeadResolved = TryResolveDataVaultCableArray(
                    solveGuardVault,
                    in _verletTelemetryHeadHandle,
                    BufferID.TetherCableBlackBoxHead,
                    DataVaultTelemetryHeadCapacity,
                    out NativeArray<int> _verletTelemetryHead);

                if (!positionsResolved ||
                    !previousResolved ||
                    !velocitiesResolved ||
                    !pinnedPositionsResolved ||
                    !pinnedMaskResolved ||
                    !nodeFaultFlagsResolved ||
                    !restLengthsResolved ||
                    !segmentTensionsResolved ||
                    !correctionsResolved ||
                    !correctionWeightsResolved ||
                    !solverStatsResolved ||
                    !solverFlagsResolved ||
                    !telemetryRingResolved ||
                    !telemetryHeadResolved)
                {
                    if (telemetryRingResolved && telemetryHeadResolved)
                    {
                        if (!positionsResolved)
                            RecordSolverVaultAccessFailure(BufferID.TetherVerletPositions, in _verletPositionsHandle, TetherTelemetryFailureResolve, true, true, _verletTelemetryRing, _verletTelemetryHead);
                        if (!previousResolved)
                            RecordSolverVaultAccessFailure(BufferID.TetherVerletPreviousPositions, in _verletPreviousPositionsHandle, TetherTelemetryFailureResolve, true, true, _verletTelemetryRing, _verletTelemetryHead);
                        if (!velocitiesResolved)
                            RecordSolverVaultAccessFailure(BufferID.TetherVerletVelocities, in _verletVelocitiesHandle, TetherTelemetryFailureResolve, true, true, _verletTelemetryRing, _verletTelemetryHead);
                        if (!pinnedPositionsResolved)
                            RecordSolverVaultAccessFailure(BufferID.TetherVerletPinnedPositions, in _verletPinnedPositionsHandle, TetherTelemetryFailureResolve, true, true, _verletTelemetryRing, _verletTelemetryHead);
                        if (!pinnedMaskResolved)
                            RecordSolverVaultAccessFailure(BufferID.TetherVerletPinnedMask, in _verletPinnedMaskHandle, TetherTelemetryFailureResolve, true, true, _verletTelemetryRing, _verletTelemetryHead);
                        if (!nodeFaultFlagsResolved)
                            RecordSolverVaultAccessFailure(BufferID.TetherVerletNodeFaultFlags, in _verletNodeFaultFlagsHandle, TetherTelemetryFailureResolve, true, true, _verletTelemetryRing, _verletTelemetryHead);
                        if (!restLengthsResolved)
                            RecordSolverVaultAccessFailure(BufferID.TetherVerletSegmentRestLengths, in _verletSegmentRestLengthsHandle, TetherTelemetryFailureResolve, true, true, _verletTelemetryRing, _verletTelemetryHead);
                        if (!segmentTensionsResolved)
                            RecordSolverVaultAccessFailure(BufferID.TetherVerletSegmentTensions, in _verletSegmentTensionsHandle, TetherTelemetryFailureResolve, true, true, _verletTelemetryRing, _verletTelemetryHead);
                        if (!correctionsResolved)
                            RecordSolverVaultAccessFailure(BufferID.TetherVerletCorrections, in _verletCorrectionsHandle, TetherTelemetryFailureResolve, true, true, _verletTelemetryRing, _verletTelemetryHead);
                        if (!correctionWeightsResolved)
                            RecordSolverVaultAccessFailure(BufferID.TetherVerletCorrectionWeights, in _verletCorrectionWeightsHandle, TetherTelemetryFailureResolve, true, true, _verletTelemetryRing, _verletTelemetryHead);
                        if (!solverStatsResolved)
                            RecordSolverVaultAccessFailure(BufferID.TetherVerletSolverStats, in _verletSolverStatsHandle, TetherTelemetryFailureResolve, true, true, _verletTelemetryRing, _verletTelemetryHead);
                        if (!solverFlagsResolved)
                            RecordSolverVaultAccessFailure(BufferID.TetherVerletSolverFlags, in _verletSolverFlagsHandle, TetherTelemetryFailureResolve, true, true, _verletTelemetryRing, _verletTelemetryHead);
                    }
                    else
                    {
                        solveGuardVault.ReleaseMutationGuard(VerletSolveMutationGuardMask);
                        solveGuardHeld = false;
                        if (!telemetryRingResolved)
                            RecordVaultAccessFailure(BufferID.TetherCableBlackBox, in _verletTelemetryRingHandle, TetherTelemetryFailureResolve, 0u);
                        if (!telemetryHeadResolved)
                            RecordVaultAccessFailure(BufferID.TetherCableBlackBoxHead, in _verletTelemetryHeadHandle, TetherTelemetryFailureResolve, 0u);
                    }

                    SyncPrimaryConstraint(anchorPosition, payloadPosition);
                    return ResolvePrimaryConstraintForceMagnitude();
                }

                float3 anchor = default;
                anchor.x = anchorPosition.x;
                anchor.y = anchorPosition.y;
                anchor.z = anchorPosition.z;
                float3 payload = default;
                payload.x = payloadPosition.x;
                payload.y = payloadPosition.y;
                payload.z = payloadPosition.z;
                RebaseVerletSolverOrigin(
                    SanitizeFinite(anchor),
                    _verletPositions,
                    _verletPreviousPositions,
                    _verletPinnedPositions);
                float3 payloadLocal = SanitizeFinite(payload - _verletSolverOrigin);
                int lastNodeIndex = _verletPositions.Length - 1;
                _verletPinnedPositions[0] = float3.zero;
                _verletPinnedPositions[lastNodeIndex] = payloadLocal;
                _verletPinnedMask[0] = 1;
                _verletPinnedMask[lastNodeIndex] = 1;

                VerletCableTuningDTO tuning = ResolveVerletTuning();
                ApplyVerletRestLengthTarget(
                    _verletSegmentRestLengths,
                    math.max(_restLength, MinDistance),
                    fixedDeltaTime,
                    tuning.ReelSpeedMetersPerSecond);

                int iterationCount = ResolveVerletIterationCount(_qualityWeight01, tuning.ConstraintIterations);
                _lastVerletIterationCount = iterationCount;
                float dtSq = fixedDeltaTime * fixedDeltaTime;
                float3 defaultGravity = default;
                defaultGravity.y = -HectonPhysicsContract.GravityMetersPerSecondSquaredConst;
                float3 gravity = math.lengthsq(tuning.Gravity) > 0.000001f && math.all(math.isfinite(tuning.Gravity))
                    ? tuning.Gravity
                    : defaultGravity;
                float3 flowAcceleration = ToFloat3(ResolveVerletFlowAcceleration(payloadCurrentAcceleration));
                MockWorldSampler worldSampler = BuildVerletWorldSampler(payloadLocal, flowAcceleration);
                float velocityDamping = tuning.FluidFriction > 0f && math.isfinite(tuning.FluidFriction)
                    ? math.saturate(tuning.FluidFriction)
                    : ResolveVerletVelocityDamping(_qualityWeight01);
                TetherVerletIntegrationJob integrationJob = default;
                integrationJob.Positions = _verletPositions;
                integrationJob.PreviousPositions = _verletPreviousPositions;
                integrationJob.Velocities = _verletVelocities;
                integrationJob.NodeFaultFlags = _verletNodeFaultFlags;
                integrationJob.PinnedPositions = _verletPinnedPositions;
                integrationJob.PinnedMask = _verletPinnedMask;
                integrationJob.Acceleration = gravity;
                integrationJob.DeltaTimeSq = dtSq;
                integrationJob.VelocityDamping = velocityDamping;
                integrationJob.MaxCableVelocity = MaxCableVelocity * fixedDeltaTime;
                integrationJob.FloorY = VerletFloorY;
                integrationJob.NodeRadius = VerletNodeRadius;
                integrationJob.WorldSampler = worldSampler;
                integrationJob.RockFriction01 = tuning.RockFriction01 > 0f && math.isfinite(tuning.RockFriction01)
                    ? math.saturate(tuning.RockFriction01)
                    : VerletRockFriction01;
                integrationJob.WorldSamplerEnabled = 1;

                VerletCableSolverJob constraintJob = default;
                constraintJob.Positions = _verletPositions;
                constraintJob.Corrections = _verletCorrections;
                constraintJob.CorrectionWeights = _verletCorrectionWeights;
                constraintJob.SegmentTensions = _verletSegmentTensions;
                constraintJob.SolverStats = _verletSolverStats;
                constraintJob.SolverFlags = _verletSolverFlags;
                constraintJob.SegmentRestLengths = _verletSegmentRestLengths;
                constraintJob.PinnedPositions = _verletPinnedPositions;
                constraintJob.PinnedMask = _verletPinnedMask;
                constraintJob.NodeFaultFlags = _verletNodeFaultFlags;
                constraintJob.NodeCount = _verletPositions.Length;
                constraintJob.IterationCount = iterationCount;
                constraintJob.FloorY = VerletFloorY;
                constraintJob.NodeRadius = VerletNodeRadius;

                TetherVerletTelemetryJob telemetryJob = default;
                telemetryJob.TelemetryRing = _verletTelemetryRing;
                telemetryJob.TelemetryHead = _verletTelemetryHead;
                telemetryJob.SolverStats = _verletSolverStats;
                telemetryJob.SolverFlags = _verletSolverFlags;
                telemetryJob.FrameIndex = unchecked((uint)_currentSimulationFrameIndex);
                telemetryJob.NodeCount = _verletPositions.Length;
                telemetryJob.IterationCount = iterationCount;
                telemetryJob.PeakCableTension = 0f;
                telemetryJob.TensionScale = math.max(0f, _springStiffness);
                telemetryJob.AnchorPosition = anchor;
                telemetryJob.PayloadPosition = payload;
                telemetryJob.Flags = 0u;
                telemetryJob.TelemetryOffset = ResolveTelemetryRingOffset();
                telemetryJob.TelemetryCapacity = ResolveTelemetryCapacity();
                telemetryJob.TelemetryHeadOffset = ResolveTelemetryHeadIndex();
                int integrationBatch = SystemDispatcher.ResolveInnerloopBatchCount(_verletPositions.Length, 16, 64);
                JobHandle integrationHandle = integrationJob.Schedule(_verletPositions.Length, integrationBatch);
                JobHandle constraintHandle = constraintJob.Schedule(integrationHandle);
                _pendingVerletSolveHandle = telemetryJob.Schedule(constraintHandle);
                _pendingVerletSolveActive = true;
                H8Memory.RegisterActiveJob(SystemID.Physics, _pendingVerletSolveHandle);
                _pendingVerletAnchorPosition = anchorPosition;
                _pendingVerletPayloadPosition = payloadPosition;
                _pendingVerletFixedDeltaTime = fixedDeltaTime;
                _pendingVerletStretchThreshold01 = tuning.StretchThreshold01;
                _pendingVerletFrameIndex = _currentSimulationFrameIndex;
                _pendingVerletSolveGuardVault = solveGuardVault;
                _pendingVerletSolveGuardHeld = true;
                solveGuardTransferred = true;
                JobHandle.ScheduleBatchedJobs();
                ResetVaultFailureStreak();
                return ResolvePrimaryConstraintForceMagnitude();
            }
            finally
            {
                if (solveGuardHeld && !solveGuardTransferred && solveGuardVault != null)
                    solveGuardVault.ReleaseMutationGuard(VerletSolveMutationGuardMask);
            }
        }

        private bool FinalizePendingVerletSolveNoWait(bool publishResults)
        {
            if (!_pendingVerletSolveActive)
                return true;

            JobHandle handle = _pendingVerletSolveHandle;
            if (!DispatcherJobFence.TryFinalizeCompleted(ref handle))
                return false;

            return CommitPendingVerletSolve(publishResults);
        }

        private bool FinalizePendingVerletSolveForBarrier(bool publishResults)
        {
            if (!_pendingVerletSolveActive)
                return true;

            JobHandle handle = _pendingVerletSolveHandle;
            bool completed;
            DispatcherJobFence.BeginPostFixedSwapWindow();
            try
            {
                completed = DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostFixedSwapWindow();
            }

            if (!completed)
                return false;

            return CommitPendingVerletSolve(publishResults);
        }

        private bool CommitPendingVerletSolve(bool publishResults)
        {
            _pendingVerletSolveHandle = default;
            _pendingVerletSolveActive = false;
            ReleasePendingVerletSolveGuard();
            if (!publishResults)
                return true;

            int previousFrameIndex = _currentSimulationFrameIndex;
            _currentSimulationFrameIndex = _pendingVerletFrameIndex;
            FinalizeVerletSolveResults(
                _pendingVerletAnchorPosition,
                _pendingVerletPayloadPosition,
                _pendingVerletFixedDeltaTime,
                _pendingVerletStretchThreshold01);
            _currentSimulationFrameIndex = previousFrameIndex;
            return true;
        }

        private void ReleasePendingVerletSolveGuard()
        {
            if (!_pendingVerletSolveGuardHeld)
                return;

            IDataVault vault = _pendingVerletSolveGuardVault;
            _pendingVerletSolveGuardVault = null;
            _pendingVerletSolveGuardHeld = false;
            if (vault != null)
                vault.ReleaseMutationGuard(VerletSolveMutationGuardMask);
        }

        private void FinalizeVerletSolveResults(
            Vector3 anchorPosition,
            Vector3 payloadPosition,
            float fixedDeltaTime,
            float stretchThreshold01)
        {
            bool hasStats = TryResolveVerletSolverStats(out NativeArray<float> _verletSolverStats) &&
                            _verletSolverStats.Length > 0;
            bool hasFlags = TryResolveVerletSolverFlags(out NativeArray<int> _verletSolverFlags) &&
                            _verletSolverFlags.Length > 0;
            ApplyVerletPlasticDeformation(
                hasStats ? _verletSolverStats[0] : 0f,
                stretchThreshold01);
            float peakTension = hasStats
                ? _verletSolverStats[0] * math.max(0f, _springStiffness)
                : 0f;
            _lastVerletPeakDelta = hasStats ? _verletSolverStats[0] : 0f;
            if (hasFlags && _verletSolverFlags[0] != TetherVerletFaultFlags.None)
                DumpVerletTelemetryOnce((uint)_verletSolverFlags[0]);

            _primaryConstraintForceMagnitude = peakTension;
            PublishTetherTensionSignal(anchorPosition, payloadPosition, peakTension);
            PublishDataVaultCableState(fixedDeltaTime, peakTension);
            ApplyVerletEndpointForces(anchorPosition, payloadPosition, peakTension);
            EmitTensionCreakIfNeeded(anchorPosition, payloadPosition, peakTension);
        }

        private static void ApplyVerletRestLengthTarget(
            NativeArray<float> verletSegmentRestLengths,
            float targetCableLength,
            float fixedDeltaTime,
            float reelSpeedMetersPerSecond)
        {
            if (!verletSegmentRestLengths.IsCreated || verletSegmentRestLengths.Length == 0)
                return;

            int segmentCount = verletSegmentRestLengths.Length;
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
                float current = verletSegmentRestLengths[i];
                if (!math.isfinite(current) || current <= 0f)
                {
                    verletSegmentRestLengths[i] = targetSegmentRestLength;
                    continue;
                }

                float delta = targetSegmentRestLength - current;
                if (math.abs(delta) <= maxSegmentDelta)
                    verletSegmentRestLengths[i] = targetSegmentRestLength;
                else
                    verletSegmentRestLengths[i] = current + math.sign(delta) * maxSegmentDelta;
            }
        }

        private void ApplyVerletPlasticDeformation(float peakConstraintDelta, float stretchThreshold01)
        {
            int segmentCount = math.max(1, ResolveDataVaultNodeCount(_verletNodeCount) - 1);
            if (!math.isfinite(peakConstraintDelta) || peakConstraintDelta <= 0f)
            {
                return;
            }

            bool restLengthsLocked = TryAcquireDataVaultCableSlice(
                in _verletSegmentRestLengthsHandle,
                BufferID.TetherVerletSegmentRestLengths,
                DataVaultScratchSegmentCapacity,
                _dataVaultSlot * DataVaultCableSegmentCount,
                segmentCount,
                out NativeArray<float> _verletSegmentRestLengths);
            if (!restLengthsLocked)
            {
                RecordVaultAccessFailure(BufferID.TetherVerletSegmentRestLengths, in _verletSegmentRestLengthsHandle, TetherTelemetryFailureLock, 0u);
                return;
            }

            try
            {
                if (!_verletSegmentRestLengths.IsCreated || _verletSegmentRestLengths.Length == 0)
                    return;

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
            finally
            {
                ReleaseDataVaultCableWriteLock(in _verletSegmentRestLengthsHandle, BufferID.TetherVerletSegmentRestLengths);
            }
        }

        private static MockWorldSampler BuildVerletWorldSampler(float3 payloadLocal, float3 flowAcceleration)
        {
            float lengthSq = math.lengthsq(payloadLocal);
            float obstacleRadius = math.isfinite(lengthSq) && lengthSq > 144f
                ? math.min(0.65f, math.sqrt(lengthSq) * 0.025f)
                : 0f;
            float3 side = ResolveCablePerpendicular(payloadLocal);
            MockSDFSampler sdf = default;
            sdf.SphereCenter = payloadLocal * 0.52f + side * (obstacleRadius * 0.75f);
            sdf.SphereRadius = obstacleRadius;
            sdf.SecondarySphereCenter = payloadLocal * 0.74f - side * (obstacleRadius * 0.55f);
            sdf.SecondarySphereRadius = obstacleRadius * 0.72f;
            sdf.PlaneY = VerletFloorY;

            MockWorldSampler sampler = default;
            sampler.Sdf = sdf;
            sampler.FlowVelocity = flowAcceleration;
            sampler.FlowAccelerationScale = 1f;
            return sampler;
        }

        private static float3 ResolveCablePerpendicular(float3 axis)
        {
            float3 forward = default;
            forward.z = 1f;
            float3 safeAxis = math.lengthsq(axis) > MinVectorMagnitudeSq
                ? math.normalize(axis)
                : forward;
            float3 up = default;
            up.y = 1f;
            float3 side = math.cross(safeAxis, up);
            float sideLengthSq = math.lengthsq(side);
            if (!math.isfinite(sideLengthSq) || sideLengthSq <= MinVectorMagnitudeSq)
            {
                float3 right = default;
                right.x = 1f;
                side = math.cross(safeAxis, right);
            }

            sideLengthSq = math.lengthsq(side);
            if (math.isfinite(sideLengthSq) && sideLengthSq > MinVectorMagnitudeSq)
                return side * math.rsqrt(sideLengthSq);

            float3 fallbackRight = default;
            fallbackRight.x = 1f;
            return fallbackRight;
        }

        private void DumpVerletTelemetryOnce(uint reasonFlags)
        {
            if (_verletFaultDumpedThisActivation)
                return;

            bool telemetryGuardHeld = false;
            IDataVault telemetryGuardVault = null;
            try
            {
                telemetryGuardVault = _dataVault;
                if (telemetryGuardVault == null ||
                    !telemetryGuardVault.TryAcquireMutationGuard(TelemetryMutationGuardMask))
                    return;

                telemetryGuardHeld = true;
                bool telemetryRingResolved = TryResolveDataVaultCableArray(
                    telemetryGuardVault,
                    in _verletTelemetryRingHandle,
                    BufferID.TetherCableBlackBox,
                    DataVaultTelemetryCapacity,
                    out NativeArray<TetherVerletTelemetryEntry> _verletTelemetryRing);
                bool telemetryHeadResolved = TryResolveDataVaultCableArray(
                    telemetryGuardVault,
                    in _verletTelemetryHeadHandle,
                    BufferID.TetherCableBlackBoxHead,
                    DataVaultTelemetryHeadCapacity,
                    out NativeArray<int> _verletTelemetryHead);
                if (!telemetryRingResolved || !telemetryHeadResolved)
                    return;

                int capacity = ResolveTelemetryCapacity();
                int telemetryOffset = ResolveTelemetryRingOffset();
                int telemetryHeadIndex = ResolveTelemetryHeadIndex();
                if (capacity <= 0 ||
                    (uint)telemetryOffset >= (uint)_verletTelemetryRing.Length ||
                    capacity > _verletTelemetryRing.Length - telemetryOffset ||
                    (uint)telemetryHeadIndex >= (uint)_verletTelemetryHead.Length)
                {
                    return;
                }

                _verletFaultDumpedThisActivation = true;
                if (!TryResolveTetherDumpPaths(out string h8DumpPath, out string legacyDumpPath))
                    return;

                int head = _verletTelemetryHead[telemetryHeadIndex];
                if (head < 0 || head >= capacity)
                    head = 0;

                NativeArray<TetherVerletTelemetryEntry> telemetrySlice =
                    _verletTelemetryRing.GetSubArray(telemetryOffset, capacity);
                if (!TetherBlackBoxDumpWriter.TryQueuePrimaryAndLegacy(
                        h8DumpPath,
                        legacyDumpPath,
                        TetherTelemetryDumpMagic,
                        telemetrySlice,
                        head,
                        reasonFlags))
                {
                    TetherBlackBoxDumpWriter.TryWritePrimaryAndLegacy(
                        h8DumpPath,
                        legacyDumpPath,
                        TetherTelemetryDumpMagic,
                        telemetrySlice,
                        head,
                        reasonFlags);
                }
            }
            finally
            {
                if (telemetryGuardHeld && telemetryGuardVault != null)
                    telemetryGuardVault.ReleaseMutationGuard(TelemetryMutationGuardMask);
            }
        }

        private static bool TryResolveTetherDumpPaths(out string h8DumpPath, out string legacyDumpPath)
        {
            h8DumpPath = string.Empty;
            legacyDumpPath = string.Empty;

            try
            {
                DirectoryInfo projectRootInfo = Directory.GetParent(Application.dataPath);
                if (projectRootInfo == null)
                    return false;

                string projectRoot = projectRootInfo.FullName;
                char separator = Path.DirectorySeparatorChar;
                h8DumpPath = Path.Combine(
                    projectRoot,
                    TetherTelemetryH8DumpRelativePath.Replace('/', separator));
                legacyDumpPath = Path.Combine(
                    projectRoot,
                    TetherTelemetryDumpRelativePath.Replace('/', separator));
                return !string.IsNullOrEmpty(h8DumpPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            h8DumpPath = string.Empty;
            legacyDumpPath = string.Empty;
            return false;
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
                    Vector3 flowVector3 = default;
                    flowVector3.x = flowVector.x;
                    flowVector3.y = flowVector.y;
                    flowVector3.z = flowVector.z;
                    resolved += flowVector3 * 0.12f;
                }
                else
                {
                    IWeatherService weather = _manager != null ? _manager.CachedWeatherService : null;
                    if (weather != null && weather.IsInitialized)
                    {
                        WeatherRuntimeSnapshot snapshot = weather.GetRuntimeSnapshot();
                        float3 current = snapshot.CurrentMeta.GlobalBaseVector * math.max(0f, snapshot.CurrentMeta.GlobalScale);
                        Vector3 current3 = default;
                        current3.x = current.x;
                        current3.y = current.y;
                        current3.z = current.z;
                        resolved += current3 * 0.08f;
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

            if (!TryResolveAupFromRuntimeOrigin(midpoint, out AbsoluteUniversePosition pointAup))
                return;

            float intensity = math.saturate((peakTension - safeMargin) * math.rcp(math.max(1f, snapThreshold - safeMargin)));
            ImpactSignal signal = default;
            signal.PointAup = pointAup;
            signal.Force = peakTension;
            signal.Intensity = intensity;
            signal.MaterialHash = TetherCreakMaterialHash;
            signal.WeightClass = 2;
            signal.PrimaryMaterialId = 7;
            signal.SecondaryMaterialId = 0;
            signal.Flags = (byte)(peakTension >= snapThreshold * ReactiveVfxThreshold01 ? 3 : 1);
            SignalBus<ImpactSignal>.TryPushTracked(in signal, ref s_x001TetherInstanceSignalPushDropCount);
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
            if (!TryResolveAupFromRuntimeOrigin(safeAnchorPosition, out AbsoluteUniversePosition anchorAup) ||
                !TryResolveAupFromRuntimeOrigin(safePayloadPosition, out AbsoluteUniversePosition payloadAup))
            {
                return;
            }

            float reactiveVfx01 = math.saturate(
                (peakTension - snapThreshold * ReactiveVfxThreshold01) *
                math.rcp(math.max(1f, snapThreshold * (1f - ReactiveVfxThreshold01))));
            TetherTensionSignal signal = default;
            signal.AnchorAup = anchorAup;
            signal.PayloadAup = payloadAup;
            signal.DirectionToPayload = ToFloat3(direction);
            signal.TetherId = unchecked((uint)EntityId.ToULong(GetEntityId()));
            signal.FrameIndex = unchecked((uint)_currentSimulationFrameIndex);
            signal.TensionForce = peakTension;
            signal.SnapThreshold = snapThreshold;
            signal.Tension01 = math.saturate(peakTension * math.rcp(math.max(1f, snapThreshold)));
            signal.ReactiveVfx01 = reactiveVfx01;
            signal.NodeCount = (ushort)math.clamp(_verletNodeCount, 0, ushort.MaxValue);
            signal.Flags = (byte)(reactiveVfx01 > 0f ? 1 : 0);
            signal.Reserved = 0;
            TetherSignals.TryPublishTension(in signal);
        }

        private void PublishSnapImpactSignal(Vector3 snapPosition, float peakTension, float snapSeverity)
        {
            if (!IsFinite(snapPosition))
                snapPosition = IsFinite(transform.position) ? transform.position : Vector3.zero;

            float safePeakTension = math.isfinite(peakTension) ? math.max(0f, peakTension) : 0f;
            float safeSnapSeverity = math.isfinite(snapSeverity) ? math.saturate(snapSeverity) : 0f;

            if (!TryResolveAupFromRuntimeOrigin(snapPosition, out AbsoluteUniversePosition snapAup))
                return;

            ImpactSignal signal = default;
            signal.PointAup = snapAup;
            signal.Force = safePeakTension;
            signal.Intensity = safeSnapSeverity;
            signal.MaterialHash = TetherSnapImpactMaterialHash;
            signal.WeightClass = 2;
            signal.PrimaryMaterialId = 7;
            signal.SecondaryMaterialId = 0;
            signal.Flags = 2;
            SignalBus<ImpactSignal>.TryPushTracked(in signal, ref s_x001TetherInstanceSignalPushDropCount);
        }

        private void ApplyVerletEndpointForces(Vector3 anchorPosition, Vector3 payloadPosition, float peakTension)
        {
            if (_payloadBody == null || peakTension <= 0f || !math.isfinite(peakTension))
                return;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return;

            double3 localOriginAup = originAup.ToAbsoluteDouble3();
            if (!TryResolveAupAbsoluteDoubleFromRuntimeOrigin(anchorPosition, in originAup, out double3 anchorAup) ||
                !TryResolveAupAbsoluteDoubleFromRuntimeOrigin(payloadPosition, in originAup, out double3 payloadAup))
            {
                return;
            }

            double3 anchorLocal64 = anchorAup - localOriginAup;
            double3 payloadLocal64 = payloadAup - localOriginAup;
            double3 deltaLocal64 = payloadLocal64 - anchorLocal64;
            double distanceSq64 = math.lengthsq(deltaLocal64);
            if (!math.isfinite(distanceSq64) || distanceSq64 <= MinVectorMagnitudeSq)
                return;

            double invDistance = math.rsqrt(distanceSq64);
            Vector3 direction = default;
            direction.x = (float)(deltaLocal64.x * invDistance);
            direction.y = (float)(deltaLocal64.y * invDistance);
            direction.z = (float)(deltaLocal64.z * invDistance);
            float rawPlayerMass = ResolvePlayerAnchorMassKg();
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
            float3 payloadForce3 = default;
            payloadForce3.x = payloadForce.x;
            payloadForce3.y = payloadForce.y;
            payloadForce3.z = payloadForce.z;
            uint frameIndex = unchecked((uint)_currentSimulationFrameIndex);
            TetherForcePacketDTO anchorPacket = default;
            if (ShouldFlushAnchorForceToRigidbody())
            {
                Vector3 reaction = -payloadForce;
                float3 reactionForce = default;
                reactionForce.x = reaction.x;
                reactionForce.y = reaction.y;
                reactionForce.z = reaction.z;
                anchorPacket.ApplicationAUP = anchorAup;
                anchorPacket.Force = reactionForce;
                anchorPacket.Tension = scaledForce;
                anchorPacket.CableId = _dataVaultSlot;
                anchorPacket.BodySlot = 0;
                anchorPacket.Flags = TetherForcePacketFlags.EndpointAnchor;
                anchorPacket.FrameIndex = frameIndex;
            }

            TetherForcePacketDTO payloadPacket = default;
            payloadPacket.ApplicationAUP = payloadAup;
            payloadPacket.Force = payloadForce3;
            payloadPacket.Tension = scaledForce;
            payloadPacket.CableId = _dataVaultSlot;
            payloadPacket.BodySlot = 1;
            payloadPacket.Flags = TetherForcePacketFlags.EndpointPayload;
            payloadPacket.FrameIndex = frameIndex;
            TetherAupForcePacketBridge.FlushPacketPair(
                in anchorPacket,
                in payloadPacket,
                _anchorBody,
                _payloadBody,
                localOriginAup,
                maxPayloadForce);
        }

        private void UpdateVerletVisualUpload(float qualityWeight01, Plane[] frustumPlanes)
        {
            int nodeCount = ResolveDataVaultNodeCount(_verletNodeCount);
            if (!TryResolveVerletPositions(nodeCount, out NativeArray<float3> _verletPositions))
            {
                RecordVaultAccessFailure(BufferID.TetherVerletPositions, in _verletPositionsHandle, TetherTelemetryFailureResolve, 0u);
                return;
            }

            bool visualPositionsLocked = TryAcquireDataVaultCableSlice(
                in _visualSegmentPositionsHandle,
                BufferID.TetherVisualSegmentPositions,
                DataVaultScratchNodeCapacity,
                _dataVaultSlot * DataVaultCablePointCount,
                nodeCount,
                out NativeArray<float3> _visualSegmentPositions);
            if (!visualPositionsLocked)
            {
                RecordVaultAccessFailure(BufferID.TetherVisualSegmentPositions, in _visualSegmentPositionsHandle, TetherTelemetryFailureLock, 0u);
                return;
            }

            bool uploadGpuBuffers = false;
            try
            {
                if (_visualSegmentPositions.Length != _verletPositions.Length)
                {
                    RecordVaultAccessFailure(BufferID.TetherVisualSegmentPositions, in _visualSegmentPositionsHandle, TetherTelemetryFailureLength, 0u);
                    return;
                }

                float curveWeight01 = ResolveTautLineVisualCurveWeight(qualityWeight01);
                float3 start = _verletPositions[0];
                float3 end = _verletPositions[_verletPositions.Length - 1];
                float invLast = math.rcp(math.max(1, _visualSegmentPositions.Length - 1));
                for (int i = 0; i < _visualSegmentPositions.Length; i++)
                {
                    float3 straightPoint = math.lerp(start, end, i * invLast);
                    float3 localPoint = math.lerp(straightPoint, _verletPositions[i], curveWeight01);
                    _visualSegmentPositions[i] = SanitizeFinite(localPoint + _verletSolverOrigin);
                }

                Vector3 minBounds = default;
                minBounds.x = _visualSegmentPositions[0].x;
                minBounds.y = _visualSegmentPositions[0].y;
                minBounds.z = _visualSegmentPositions[0].z;
                Vector3 maxBounds = minBounds;
                for (int i = 1; i < _visualSegmentPositions.Length; i++)
                {
                    float3 point = _visualSegmentPositions[i];
                    Vector3 pointV3 = default;
                    pointV3.x = point.x;
                    pointV3.y = point.y;
                    pointV3.z = point.z;
                    minBounds = Vector3.Min(minBounds, pointV3);
                    maxBounds = Vector3.Max(maxBounds, pointV3);
                }

                _visualBounds.SetMinMax(minBounds, maxBounds);
                if (ShouldUploadVisualBounds(frustumPlanes))
                    uploadGpuBuffers = true;
                else
                    _visualCulledThisFrame = true;
                ResetVaultFailureStreak();
            }
            finally
            {
                ReleaseDataVaultCableWriteLock(in _visualSegmentPositionsHandle, BufferID.TetherVisualSegmentPositions);
            }

            if (uploadGpuBuffers)
                UploadVisualGpuBuffers(includeTension: true);
        }

        private bool ShouldUploadVisualBounds(Plane[] frustumPlanes)
        {
            if (frustumPlanes == null || frustumPlanes.Length < 6)
                return true;

            return GeometryUtility.TestPlanesAABB(frustumPlanes, _visualBounds);
        }

        private float ResolveTautLineVisualCurveWeight(float qualityWeight01)
        {
            float qualityWeight = SanitizeQualityWeight(qualityWeight01);
            float collapseWeight = math.saturate((0.35f - qualityWeight) * math.rcp(0.35f));
            float loadWeight = SmoothRange01(
                LowTierTautLineVisualThreshold01 - 0.08f,
                LowTierTautLineVisualThreshold01,
                math.max(_tension01, _stress01));
            return math.saturate(1f - collapseWeight * loadWeight);
        }

        private static int ResolveVerletIterationCount(float qualityWeight01, int tuningOverride)
        {
            if (tuningOverride > 0)
                return math.clamp(tuningOverride, VerletLowIterationCount, VerletUltraIterationCount);

            float qualityWeight = SanitizeQualityWeight(qualityWeight01);
            float qualityCurve = Smooth01(qualityWeight);
            return math.clamp(
                (int)math.round(math.lerp(VerletLowIterationCount, VerletUltraIterationCount, qualityCurve)),
                VerletLowIterationCount,
                VerletUltraIterationCount);
        }

        private static int ResolveVerletPointCount(float qualityWeight01)
        {
            return ResolveVerletSegmentCount(qualityWeight01) + 1;
        }

        private static int ResolveVerletSegmentCount(float qualityWeight01)
        {
            float qualityWeight = SanitizeQualityWeight(qualityWeight01);
            float qualityCurve = Smooth01(qualityWeight);
            return math.clamp(
                (int)math.round(math.lerp(VerletLowSegmentCount, DataVaultCableSegmentCount, qualityCurve)),
                VerletLowSegmentCount,
                DataVaultCableSegmentCount);
        }

        private static float ResolveVerletVelocityDamping(float qualityWeight01)
        {
            float qualityWeight = SanitizeQualityWeight(qualityWeight01);
            float qualityCurve = Smooth01(qualityWeight);
            return math.lerp(VerletLowVelocityDamping, VerletHighVelocityDamping, qualityCurve);
        }

        private static float ResolveCompatibilityTetherQualityWeight(HectonQualityTier qualityTier)
        {
            float tierOrdinal = math.clamp((int)TetherManager.SanitizeQualityTier(qualityTier), (int)HectonQualityTier.Low, (int)HectonQualityTier.Ultra);
            const float qualityTierRange = (int)HectonQualityTier.Ultra - (int)HectonQualityTier.Low;
            return math.saturate((tierOrdinal - (int)HectonQualityTier.Low) * math.rcp(qualityTierRange));
        }

        private static float SanitizeQualityWeight(float qualityWeight01)
        {
            return math.saturate(math.isfinite(qualityWeight01) ? qualityWeight01 : 1f);
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        private static float SmoothRange01(float min, float max, float value)
        {
            float width = max - min;
            if (!math.isfinite(width) || math.abs(width) <= 0.000001f)
                return value >= max ? 1f : 0f;

            return Smooth01((value - min) / width);
        }

        private void RecalculateDampingCoefficient()
        {
            float playerMass = ResolvePlayerAnchorMassKg();
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
                    _bioCableHoldTimer = math.isfinite(_bioCableHoldTime) ? math.max(0f, _bioCableHoldTime) : 0f;
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
            float3 safePayloadPosition3 = default;
            safePayloadPosition3.x = safePayloadPosition.x;
            safePayloadPosition3.y = safePayloadPosition.y;
            safePayloadPosition3.z = safePayloadPosition.z;
            float3 phantomCurrentSample = CurrentManager.SampleCurrent(
                safePayloadPosition3,
                time,
                _payloadCurrentNoiseScale,
                _payloadCurrentTimeScale,
                _payloadCurrentStrength,
                _payloadCurrentVerticalFactor);
            Vector3 phantomCurrent = default;
            phantomCurrent.x = phantomCurrentSample.x;
            phantomCurrent.y = phantomCurrentSample.y;
            phantomCurrent.z = phantomCurrentSample.z;
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

                PhysicsForceRouter.QueueAngularVelocitySet(
                    _payloadBody,
                    IsFinite(angularVelocity) ? angularVelocity : Vector3.zero);
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
                    if ((bendPoint - BendPointSlot(_bendPointCount - 1)).sqrMagnitude <= minSpacingSq)
                        break;
                }

                BendPointSlot(_bendPointCount) = bendPoint;
                BendNormalSlot(_bendPointCount) = bendNormal;
                BendVolumeSlot(_bendPointCount) = bendVolume;
                BendVolumeRuntimeStampSlot(_bendPointCount) = bendRuntimeStamp;
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
                HectonVoxelVolume cachedVolume = BendVolumeSlot(i);
                int cachedStamp = BendVolumeRuntimeStampSlot(i);
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
            if (!TryResolveSdfSurfaceHit(
                    rayOrigin,
                    rayDirection,
                    maxDistance,
                    stepMeters,
                    out HectonVoxelVolume resolvedVolume,
                    out Vector3 surfacePoint,
                    out Vector3 surfaceNormal,
                    out int surfaceRuntimeStamp) ||
                resolvedVolume == null)
            {
                return false;
            }

            Vector3 resolvedNormal = ResolveSafeDirection(surfaceNormal, fallbackNormal);
            if (!resolvedVolume.TryResolveNearestVoxelCorner(surfacePoint, resolvedNormal, out Vector3 cornerWorld))
                return false;

            bendPoint = cornerWorld + resolvedNormal * math.max(0.01f, _bendSurfaceOffset);
            bendNormal = resolvedNormal;
            bendVolume = resolvedVolume;
            bendRuntimeStamp = surfaceRuntimeStamp;
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
            if (!TryResolveSdfSurfaceHit(
                    origin,
                    direction,
                    castDistance,
                    stepMeters,
                    out HectonVoxelVolume resolvedVolume,
                    out Vector3 surfacePoint,
                    out Vector3 surfaceNormal,
                    out int surfaceRuntimeStamp) ||
                resolvedVolume == null ||
                !IsFinite(surfacePoint))
            {
                return false;
            }

            hitPoint = surfacePoint;
            hitNormal = ResolveSafeDirection(surfaceNormal, -direction);
            hitVolume = resolvedVolume;
            hitRuntimeStamp = surfaceRuntimeStamp;
            return true;
        }

        private bool TryResolveSdfSurfaceHit(
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            float stepMeters,
            out HectonVoxelVolume resolvedVolume,
            out Vector3 surfacePoint,
            out Vector3 surfaceNormal,
            out int surfaceRuntimeStamp)
        {
            resolvedVolume = null;
            surfacePoint = Vector3.zero;
            surfaceNormal = Vector3.up;
            surfaceRuntimeStamp = 0;

            IVoxelSonarSdfReadModel readModel = _voxelSdfReadModel;
            HectonVoxelEngine voxelEngine = _voxelEngineRuntime;
            if (readModel == null || voxelEngine == null)
                return false;

            float3 origin3 = ToFloat3(origin);
            float3 direction3 = ToFloat3(direction);
            if (!VoxelSonarSdfMath.TryResolveNearestSdfSurface(
                    readModel,
                    origin3,
                    direction3,
                    maxDistance,
                    stepMeters,
                    out VoxelSonarSdfRaycastHit hit) ||
                (hit.Flags & VoxelSonarSdfRaycastHit.FlagHit) == 0u)
            {
                return false;
            }

            surfacePoint = default;
            surfacePoint.x = hit.Point.x;
            surfacePoint.y = hit.Point.y;
            surfacePoint.z = hit.Point.z;
            Vector3 hitNormal = default;
            hitNormal.x = hit.Normal.x;
            hitNormal.y = hit.Normal.y;
            hitNormal.z = hit.Normal.z;
            surfaceNormal = ResolveSafeDirection(hitNormal, -direction);
            if (!IsFinite(surfacePoint) ||
                !voxelEngine.TryGetNearestActiveVolume(surfacePoint, out resolvedVolume) ||
                resolvedVolume == null)
            {
                resolvedVolume = null;
                return false;
            }

            surfaceRuntimeStamp = resolvedVolume.RuntimeStamp;
            return true;
        }

        // Scalar slots replace fixed managed arrays: no per-instance heap scratch for bend/anchor chains.
        private ref Vector3 BendPointSlot(int index)
        {
            switch (index)
            {
                case 0:
                    return ref _bendPoint0;
                case 1:
                    return ref _bendPoint1;
                case 2:
                    return ref _bendPoint2;
                case 3:
                    return ref _bendPoint3;
                default:
                    _discardVectorSlot = Vector3.zero;
                    return ref _discardVectorSlot;
            }
        }

        private ref Vector3 BendNormalSlot(int index)
        {
            switch (index)
            {
                case 0:
                    return ref _bendNormal0;
                case 1:
                    return ref _bendNormal1;
                case 2:
                    return ref _bendNormal2;
                case 3:
                    return ref _bendNormal3;
                default:
                    _discardVectorSlot = Vector3.zero;
                    return ref _discardVectorSlot;
            }
        }

        private ref HectonVoxelVolume BendVolumeSlot(int index)
        {
            switch (index)
            {
                case 0:
                    return ref _bendVolume0;
                case 1:
                    return ref _bendVolume1;
                case 2:
                    return ref _bendVolume2;
                case 3:
                    return ref _bendVolume3;
                default:
                    _discardBendVolumeSlot = null;
                    return ref _discardBendVolumeSlot;
            }
        }

        private ref int BendVolumeRuntimeStampSlot(int index)
        {
            switch (index)
            {
                case 0:
                    return ref _bendVolumeRuntimeStamp0;
                case 1:
                    return ref _bendVolumeRuntimeStamp1;
                case 2:
                    return ref _bendVolumeRuntimeStamp2;
                case 3:
                    return ref _bendVolumeRuntimeStamp3;
                default:
                    _discardIntSlot = 0;
                    return ref _discardIntSlot;
            }
        }

        private ref Vector3 AnchorPositionSlot(int index)
        {
            switch (index)
            {
                case 0:
                    return ref _anchorPosition0;
                case 1:
                    return ref _anchorPosition1;
                case 2:
                    return ref _anchorPosition2;
                case 3:
                    return ref _anchorPosition3;
                case 4:
                    return ref _anchorPosition4;
                case 5:
                    return ref _anchorPosition5;
                default:
                    _discardVectorSlot = Vector3.zero;
                    return ref _discardVectorSlot;
            }
        }

        private ref Vector3 AnchorVelocitySlot(int index)
        {
            switch (index)
            {
                case 0:
                    return ref _anchorVelocity0;
                case 1:
                    return ref _anchorVelocity1;
                case 2:
                    return ref _anchorVelocity2;
                case 3:
                    return ref _anchorVelocity3;
                case 4:
                    return ref _anchorVelocity4;
                case 5:
                    return ref _anchorVelocity5;
                default:
                    _discardVectorSlot = Vector3.zero;
                    return ref _discardVectorSlot;
            }
        }

        private ref Vector3 SolverAnchorPositionSlot(int index)
        {
            switch (index)
            {
                case 0:
                    return ref _solverAnchorPosition0;
                case 1:
                    return ref _solverAnchorPosition1;
                case 2:
                    return ref _solverAnchorPosition2;
                case 3:
                    return ref _solverAnchorPosition3;
                case 4:
                    return ref _solverAnchorPosition4;
                case 5:
                    return ref _solverAnchorPosition5;
                default:
                    _discardVectorSlot = Vector3.zero;
                    return ref _discardVectorSlot;
            }
        }

        private ref Vector3 SolverAnchorVelocitySlot(int index)
        {
            switch (index)
            {
                case 0:
                    return ref _solverAnchorVelocity0;
                case 1:
                    return ref _solverAnchorVelocity1;
                case 2:
                    return ref _solverAnchorVelocity2;
                case 3:
                    return ref _solverAnchorVelocity3;
                case 4:
                    return ref _solverAnchorVelocity4;
                case 5:
                    return ref _solverAnchorVelocity5;
                default:
                    _discardVectorSlot = Vector3.zero;
                    return ref _discardVectorSlot;
            }
        }

        private ref float SegmentRestLengthSlot(int index)
        {
            switch (index)
            {
                case 0:
                    return ref _segmentRestLength0;
                case 1:
                    return ref _segmentRestLength1;
                case 2:
                    return ref _segmentRestLength2;
                case 3:
                    return ref _segmentRestLength3;
                case 4:
                    return ref _segmentRestLength4;
                default:
                    _discardFloatSlot = 0f;
                    return ref _discardFloatSlot;
            }
        }

        private ref float SegmentLengthSlot(int index)
        {
            switch (index)
            {
                case 0:
                    return ref _segmentLength0;
                case 1:
                    return ref _segmentLength1;
                case 2:
                    return ref _segmentLength2;
                case 3:
                    return ref _segmentLength3;
                case 4:
                    return ref _segmentLength4;
                default:
                    _discardFloatSlot = 0f;
                    return ref _discardFloatSlot;
            }
        }

        private int BuildAnchorChain(Vector3 anchorPosition, Vector3 payloadPosition)
        {
            Vector3 safeAnchorPosition = IsFinite(anchorPosition) ? anchorPosition : Vector3.zero;
            Vector3 safePayloadPosition = IsFinite(payloadPosition) ? payloadPosition : safeAnchorPosition;
            AnchorPositionSlot(0) = safeAnchorPosition;
            AnchorVelocitySlot(0) = ResolveAnchorVelocity(safeAnchorPosition);
            int anchorCount = 1;

            for (int i = 0; i < _bendPointCount; i++)
            {
                Vector3 bendPoint = BendPointSlot(i);
                AnchorPositionSlot(anchorCount) = IsFinite(bendPoint) ? bendPoint : AnchorPositionSlot(anchorCount - 1);
                AnchorVelocitySlot(anchorCount) = Vector3.zero;
                anchorCount++;
            }

            AnchorPositionSlot(anchorCount) = safePayloadPosition;
            AnchorVelocitySlot(anchorCount) = _payloadBody != null && IsFinite(_payloadBody.linearVelocity) ? _payloadBody.linearVelocity : Vector3.zero;
            anchorCount++;

            PopulateSolverAnchors(anchorCount);

            float totalLength = 0f;
            int segmentCount = anchorCount - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                float segmentLength = ResolveMagnitude((SolverAnchorPositionSlot(i) - SolverAnchorPositionSlot(i + 1)).sqrMagnitude);
                SegmentLengthSlot(i) = segmentLength;
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
                    SegmentRestLengthSlot(i) = uniformLength;
                _segmentRestLengthsDirty = false;
                return;
            }

            for (int i = 0; i < segmentCount; i++)
            {
                float segmentLength = SegmentLengthSlot(i);
                segmentLength = math.isfinite(segmentLength) ? math.max(0f, segmentLength) : 0f;
                float fraction = segmentLength * math.rcp(safeTotalLength);
                SegmentRestLengthSlot(i) = safeRestLength * fraction;
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
                Vector3 toFirstBend = BendPointSlot(0) - _owner.ResolveTowAnchorPosition();
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

            if (Hecton8.PureLogic.Systems.TetherSnapLoadCalculator.Compute(safePeakTension, 1f, snapThreshold))
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
                ? ResolveSafeDirection(BendPointSlot(0) - ownerAnchor, Vector3.zero)
                : ResolveSafeDirection(payloadCenter - ownerAnchor, Vector3.zero);
            Vector3 payloadSegmentDirection = _bendPointCount > 0
                ? ResolveSafeDirection(BendPointSlot(_bendPointCount - 1) - payloadCenter, Vector3.zero)
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

            if (!TryResolveAupFromRuntimeOrigin(snapPosition, out AbsoluteUniversePosition snapAup))
                return;

            TetherSnappedSignal signal = default;
            signal.SnapAup = snapAup;
            signal.TetherId = unchecked((uint)EntityId.ToULong(GetEntityId()));
            signal.FrameIndex = unchecked((uint)_currentSimulationFrameIndex);
            signal.PeakTension = peakTension;
            signal.SnapThreshold = snapThreshold;
            signal.Severity01 = math.saturate(snapSeverity);
            signal.NodeCount = (ushort)math.clamp(_verletNodeCount, 0, ushort.MaxValue);
            signal.Reason = reason;
            signal.Flags = 0;
            TetherSignals.TryPublishSnap(in signal);
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
                Vector3 start = AnchorPositionSlot(i);
                Vector3 end = AnchorPositionSlot(i + 1);
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
                    ? ResolveSafeDirection(BendPointSlot(0) - ownerAnchor, Vector3.zero)
                    : ResolveSafeDirection(_payloadBody.worldCenterOfMass - ownerAnchor, Vector3.zero);
                Vector3 payloadSegmentDirection = _bendPointCount > 0
                    ? ResolveSafeDirection(BendPointSlot(_bendPointCount - 1) - _payloadBody.worldCenterOfMass, Vector3.zero)
                    : ResolveSafeDirection(ownerAnchor - _payloadBody.worldCenterOfMass, Vector3.zero);
                InvokeSnapProtocol(playerSegmentDirection, payloadSegmentDirection, 0f, true);
                return true;
            }

            _slicingSegmentIndex = -1;
            _slicingConsecutiveFrames = 0;
            return false;
        }

        private void CopyVisualSolverState(
            int anchorCount,
            NativeArray<float3> _visualAnchorPositions,
            NativeArray<float> _visualSegmentLengths)
        {
            int safeAnchorCount = math.clamp(anchorCount, 0, MaxAnchors);
            for (int anchorIndex = 0; anchorIndex < safeAnchorCount; anchorIndex++)
            {
                Vector3 anchorPosition = AnchorPositionSlot(anchorIndex);
                anchorPosition = IsFinite(anchorPosition) ? anchorPosition : Vector3.zero;
                float3 anchorPosition3 = default;
                anchorPosition3.x = anchorPosition.x;
                anchorPosition3.y = anchorPosition.y;
                anchorPosition3.z = anchorPosition.z;
                _visualAnchorPositions[anchorIndex] = anchorPosition3;
            }

            int segmentCount = math.max(0, safeAnchorCount - 1);
            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                float segmentLength = SegmentLengthSlot(segmentIndex);
                _visualSegmentLengths[segmentIndex] = math.isfinite(segmentLength) ? math.max(0f, segmentLength) : 0f;
            }
        }

        private bool HasSupportingBendPointForSegment(int segmentIndex, Vector3 hitPoint)
        {
            if (_bendPointCount <= 0)
                return false;

            float supportingRadius = math.max(_bendPointClearanceRadius, _bendSurfaceOffset) * 1.5f;
            float supportingRadiusSq = supportingRadius * supportingRadius;
            if (segmentIndex > 0)
            {
                Vector3 previousAnchor = AnchorPositionSlot(segmentIndex);
                float previousDistanceSq = (previousAnchor - hitPoint).sqrMagnitude;
                if (math.isfinite(previousDistanceSq) && previousDistanceSq <= supportingRadiusSq)
                    return true;
            }

            int finalSegmentIndex = _bendPointCount;
            if (segmentIndex < finalSegmentIndex)
            {
                Vector3 nextAnchor = AnchorPositionSlot(segmentIndex + 1);
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
            if (TryResolveVerletTuning(out NativeArray<VerletCableTuningDTO> _verletTuning) &&
                _verletTuning.Length > 0)
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
                BendPointSlot(i) -= shiftOffset;

            for (int i = 0; i < MaxAnchors; i++)
                AnchorPositionSlot(i) -= shiftOffset;

            if (!_solveInPlatformLocalSpace)
            {
                for (int i = 0; i < MaxAnchors; i++)
                    SolverAnchorPositionSlot(i) -= shiftOffset;
            }

            float requestedEffectiveTension01 = math.saturate(math.isfinite(_bioCableRequestedTension01) ? _bioCableRequestedTension01 : 0f) *
                (1f - math.saturate(math.isfinite(_bioCableRequestedCutProgress01) ? _bioCableRequestedCutProgress01 : 1f));
            if (_bioCableRequestedThisStep && requestedEffectiveTension01 > 0.0001f && IsFinite(_bioCableRequestedAnchorWS))
                _bioCableRequestedAnchorWS -= shiftOffset;
            else
                _bioCableRequestedAnchorWS = Vector3.zero;

            float currentEffectiveTension01 = math.saturate(math.isfinite(_bioCableCurrentTension01) ? _bioCableCurrentTension01 : 0f) *
                (1f - math.saturate(math.isfinite(_bioCableCurrentCutProgress01) ? _bioCableCurrentCutProgress01 : 1f));
            if (currentEffectiveTension01 > 0.0001f && IsFinite(_bioCableCurrentAnchorWS))
                _bioCableCurrentAnchorWS -= shiftOffset;
            else
                _bioCableCurrentAnchorWS = Vector3.zero;
            _visualBounds.SetMinMax(_visualBounds.min - shiftOffset, _visualBounds.max - shiftOffset);
        }

        internal bool RebaseVerletRuntime(float3 shiftOffset)
        {
            if (!_isActive ||
                !math.all(math.isfinite(shiftOffset)) ||
                math.lengthsq(shiftOffset) <= MinVectorMagnitudeSq)
            {
                return false;
            }

            if (!FinalizePendingVerletSolveForBarrier(publishResults: false))
                return false;

            int nodeCount = ResolveDataVaultNodeCount(_verletNodeCount);
            bool visualPositionsLocked = TryAcquireDataVaultCableSlice(
                in _visualSegmentPositionsHandle,
                BufferID.TetherVisualSegmentPositions,
                DataVaultScratchNodeCapacity,
                _dataVaultSlot * DataVaultCablePointCount,
                nodeCount,
                out NativeArray<float3> _visualSegmentPositions);
            if (!visualPositionsLocked)
            {
                RecordVaultAccessFailure(BufferID.TetherVisualSegmentPositions, in _visualSegmentPositionsHandle, TetherTelemetryFailureLock, 0u);
                return false;
            }

            try
            {
                _verletSolverOrigin = SanitizeFinite(_verletSolverOrigin - shiftOffset);
                for (int pointIndex = 0; pointIndex < _visualSegmentPositions.Length; pointIndex++)
                    _visualSegmentPositions[pointIndex] = SanitizeFinite(_visualSegmentPositions[pointIndex] - shiftOffset);
                ResetVaultFailureStreak();
                return true;
            }
            finally
            {
                ReleaseDataVaultCableWriteLock(in _visualSegmentPositionsHandle, BufferID.TetherVisualSegmentPositions);
            }
        }

        internal bool RebaseVisualStagingRuntime(float3 shiftOffset)
        {
            if (!_isActive ||
                !math.all(math.isfinite(shiftOffset)) ||
                math.lengthsq(shiftOffset) <= MinVectorMagnitudeSq)
            {
                return false;
            }

            if (!FinalizePendingVerletSolveForBarrier(publishResults: false))
                return false;

            int nodeCount = ResolveDataVaultNodeCount(_verletNodeCount);
            bool visualPositionsLocked = TryAcquireDataVaultCableSlice(
                in _visualSegmentPositionsHandle,
                BufferID.TetherVisualSegmentPositions,
                DataVaultScratchNodeCapacity,
                _dataVaultSlot * DataVaultCablePointCount,
                nodeCount,
                out NativeArray<float3> visualPoints);
            if (!visualPositionsLocked || !visualPoints.IsCreated || visualPoints.Length == 0)
            {
                RecordVaultAccessFailure(BufferID.TetherVisualSegmentPositions, in _visualSegmentPositionsHandle, TetherTelemetryFailureLock, 0u);
                return false;
            }

            try
            {
                for (int pointIndex = 0; pointIndex < visualPoints.Length; pointIndex++)
                    visualPoints[pointIndex] = SanitizeFinite(visualPoints[pointIndex] - shiftOffset);
                ResetVaultFailureStreak();
                return true;
            }
            finally
            {
                ReleaseDataVaultCableWriteLock(in _visualSegmentPositionsHandle, BufferID.TetherVisualSegmentPositions);
            }
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
            if (_payloadBody == null || (_playerMotor == null && _anchorBody == null) || _payloadMass01 < 0.75f || peakTension <= 0f || !math.isfinite(peakTension))
                return;

            int frame = _currentSimulationFrameIndex;
            if (IsFrameCooldownActive(frame, _lastTowLoadLimitCommandFrame, TowLoadLimitCommandCooldownFrames))
                return;

            float threshold = ResolveSnapTensionThreshold();
            float load01 = math.saturate(peakTension * math.rcp(math.max(threshold, 1f)));
            if (load01 < 0.65f)
                return;

            VehicleCommandSignal signal = default;
            signal.TargetInstanceId = ResolveAnchorEntityId();
            signal.Pitch = 0f;
            signal.Yaw = 0f;
            signal.Throttle = math.lerp(0.65f, 0.25f, load01);
            signal.BallastDelta = 0f;
            signal.Sequence = 0u;
            signal.Flags = (byte)(VehicleCommandSignalFlags.ManualThrottle | VehicleCommandSignalFlags.TowLoadLimit);
            if (VehicleCommandSignalBus.TryPublish(in signal))
                _lastTowLoadLimitCommandFrame = frame;
        }

        private int ResolveAnchorEntityId()
        {
            if (_anchorBody != null)
                return unchecked((int)EntityId.ToULong(_anchorBody.GetEntityId()));

            return _playerMotor != null ? unchecked((int)EntityId.ToULong(_playerMotor.GetEntityId())) : 0;
        }

        internal void CommitVisualRebaseUpload()
        {
            if (VisualSegmentBuffer == null)
                return;

            if (_pendingVerletSolveActive)
                return;

            int nodeCount = ResolveDataVaultNodeCount(_verletNodeCount);
            int segmentCount = math.max(1, nodeCount - 1);
            if (TryResolveVisualSegmentPositions(nodeCount, out _) &&
                TryResolveVerletSegmentTensions(segmentCount, out _))
            {
                UploadVisualGpuBuffers(includeTension: true);
            }
        }

        internal void RetargetAnchorEndpoint(HectonPlayerMotor playerMotor, Rigidbody anchorBody)
        {
            if (anchorBody == null)
                return;

            _playerMotor = playerMotor;
            _anchorBody = anchorBody;
            GlobalPhysicsStateManager.RegisterTetherConnection(this, _anchorBody, _payloadBody);
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
            if (_payloadBody == null || (_playerMotor == null && _anchorBody == null))
            {
                ReleasePrimaryConstraint();
                return;
            }

            Vector3 constraintAnchorPosition = _bendPointCount > 0
                ? BendPointSlot(_bendPointCount - 1)
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
                : ResolveAnchorVelocity(anchorPositionWS);
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
            Vector3 requestedForceVector = default;
            requestedForceVector.x = requestedForce3.x;
            requestedForceVector.y = requestedForce3.y;
            requestedForceVector.z = requestedForce3.z;
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
                _playerMotor == null ||
                payloadForce.sqrMagnitude <= MinVectorMagnitudeSq)
            {
                return;
            }

            Vector3 reactionForce = -payloadForce;
            if (reactionForce.sqrMagnitude <= MinVectorMagnitudeSq || !IsFinite(reactionForce))
                return;

            float playerMass = ResolvePlayerAnchorMassKg();
            Vector3 reactionAcceleration = ClampVector(reactionForce * math.rcp(playerMass), _maxCableAcceleration);
            if (reactionAcceleration.sqrMagnitude <= MinVectorMagnitudeSq || !IsFinite(reactionAcceleration))
                return;

            _playerMotor.ApplyAcceleration(reactionAcceleration);
        }

        private static float ResolvePlayerAnchorMassKg()
        {
            return PlayerAnchorEquivalentMassKg;
        }

        private Vector3 ResolveAnchorVelocity(Vector3 anchorPositionWS)
        {
            if (_anchorBody != null)
            {
                Vector3 anchorVelocity = _anchorBody.GetPointVelocity(anchorPositionWS);
                return IsFinite(anchorVelocity) ? anchorVelocity : Vector3.zero;
            }

            return CoreDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityTetherMaxAgeFrames, out Vector3 velocity)
                ? velocity
                : Vector3.zero;
        }

        private bool ShouldFlushAnchorForceToRigidbody()
        {
            return _anchorBody != null &&
                   (_playerMotor == null || !_playerMotor.HydrodynamicKccOwnsCollisionAuthority);
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
                SegmentRestLengthSlot(lastSegmentIndex) <= MinDistance)
            {
                return math.max(_restLength, MinDistance);
            }

            return math.max(SegmentRestLengthSlot(lastSegmentIndex), MinDistance);
        }

        private void PopulateSolverAnchors(int anchorCount)
        {
            if (_solveInPlatformLocalSpace && _solverPlatform != null && _solverPlatformTransform != null)
            {
                for (int i = 0; i < anchorCount; i++)
                {
                    Vector3 worldAnchor = AnchorPositionSlot(i);
                    SolverAnchorPositionSlot(i) = _solverWorldToLocalMatrix.MultiplyPoint3x4(worldAnchor);

                    if (i == 0 || i == anchorCount - 1)
                    {
                        Vector3 platformVelocity = _solverPlatform.GetPlatformPointVelocity(worldAnchor);
                        Vector3 relativeVelocity = AnchorVelocitySlot(i) - platformVelocity;
                        SolverAnchorVelocitySlot(i) = _solverWorldToLocalMatrix.MultiplyVector(relativeVelocity);
                    }
                    else
                    {
                        SolverAnchorVelocitySlot(i) = Vector3.zero;
                    }
                }

                return;
            }

            for (int i = 0; i < anchorCount; i++)
            {
                SolverAnchorPositionSlot(i) = AnchorPositionSlot(i);
                SolverAnchorVelocitySlot(i) = AnchorVelocitySlot(i);
            }
        }

        private bool InvalidateBendPointsForDynamicVoxelChange()
        {
            for (int i = 0; i < _bendPointCount; i++)
            {
                HectonVoxelVolume bendVolume = BendVolumeSlot(i);
                if (bendVolume != null && bendVolume.MatchesRuntimeStamp(BendVolumeRuntimeStampSlot(i)))
                    continue;

                if (bendVolume == null && BendVolumeRuntimeStampSlot(i) == 0)
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
                BendVolumeSlot(i) = null;
                BendVolumeRuntimeStampSlot(i) = 0;
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
            float3 value3 = default;
            value3.x = value.x;
            value3.y = value.y;
            value3.z = value.z;
            return math.all(math.isfinite(value3));
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!IsFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            double3 deltaMeters = default;
            deltaMeters.x = runtimePosition.x;
            deltaMeters.y = runtimePosition.y;
            deltaMeters.z = runtimePosition.z;
            aup = AbsoluteUniversePosition.OffsetMeters(in originAup, deltaMeters);
            return IsFiniteAup(in aup);
        }

        private static bool TryResolveAupAbsoluteDoubleFromRuntimeOrigin(
            Vector3 runtimePosition,
            in AbsoluteUniversePosition originAup,
            out double3 absoluteAup)
        {
            absoluteAup = default;
            if (!IsFinite(runtimePosition) || !IsFiniteAup(in originAup))
                return false;

            double3 deltaMeters = default;
            deltaMeters.x = runtimePosition.x;
            deltaMeters.y = runtimePosition.y;
            deltaMeters.z = runtimePosition.z;
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.OffsetMeters(in originAup, deltaMeters);
            if (!IsFiniteAup(in aup))
                return false;

            absoluteAup = aup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absoluteAup));
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
            float3 value3 = default;
            value3.x = value.x;
            value3.y = value.y;
            value3.z = value.z;
            return value3;
        }

        private void RefreshKinematicAnchorCompensationState(bool forceRecalculateDamping)
        {
            bool nextState = GlobalPhysicsStateManager.IsKinematicAnchorCompensationEnabled(this, PhysicsConnectionKind.Tether);
            if (!forceRecalculateDamping && nextState == _kinematicAnchorCompensationEnabled)
                return;

            _kinematicAnchorCompensationEnabled = nextState;
            if (_anchorBody != null && _payloadBody != null)
                RecalculateDampingCoefficient();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            int nodeCount = ResolveDataVaultNodeCount(_verletNodeCount);
            if (!_isActive ||
                !TryResolveVerletPositions(nodeCount, out NativeArray<float3> _verletPositions) ||
                _verletPositions.Length < 2)
            {
                return;
            }

            Gizmos.color = Color.green;
            for (int i = 0; i < _verletPositions.Length - 1; i++)
            {
                float3 a = SanitizeFinite(_verletPositions[i] + _verletSolverOrigin);
                float3 b = SanitizeFinite(_verletPositions[i + 1] + _verletSolverOrigin);
                Vector3 pointA = default;
                pointA.x = a.x;
                pointA.y = a.y;
                pointA.z = a.z;
                Vector3 pointB = default;
                pointB.x = b.x;
                pointB.y = b.y;
                pointB.z = b.z;
                Gizmos.DrawLine(pointA, pointB);
            }

            Gizmos.color = Color.red;
            for (int i = 0; i < _verletPositions.Length; i++)
            {
                float3 p = SanitizeFinite(_verletPositions[i] + _verletSolverOrigin);
                Vector3 point = default;
                point.x = p.x;
                point.y = p.y;
                point.z = p.z;
                Gizmos.DrawSphere(point, 0.035f);
            }
        }
#endif

        private static float ResolveBlendFactor(float sharpness, float deltaTime)
        {
            if (!math.isfinite(sharpness) || !math.isfinite(deltaTime) || sharpness <= 0f || deltaTime <= 0f)
                return 0f;

            return math.saturate(sharpness * deltaTime);
        }
    }
}
