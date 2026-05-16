using System;
using System.IO;
using Hecton8.Animation.Fauna;
using Hecton8.Animation.IK;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FaunaBrain))]
    internal sealed class FaunaKinematicsRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IOriginShiftListener, IDisposable
    {
        private const string TelemetryDumpRelativePath = "Docs/AgentLogs/Dump_LEVIATHAN_KINEMATICS_SOLVER.bin";
        private const string BiteTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_FAUNA_BITE_IK_SOLVER.bin";
        private const ulong TelemetryDumpMagic = 0x4C455649494B3031UL;
        private const ulong BiteTelemetryDumpMagic = 0x4642494B30303031UL;
        private const int TelemetryEntryPayloadBytes = 96;
        private const int BiteTelemetryEntryPayloadBytes = 128;
        private const int TelemetryEntryPaddingFloatCount = 7;
        private const float ConstraintIterationHysteresisSeconds = 2.5f;
        private const int MaxSegments = LeviathanTerrainIkConstants.MaxSegments;
        private const int LowTierSegments = LeviathanTerrainIkConstants.LowTierSegments;
        private const float MinVectorMagnitudeSq = 0.0001f;
        private const float BiteFeedbackCooldownSeconds = 0.18f;
        private const float BiteAudioCooldownSeconds = 0.24f;
        private const ushort BiteLowTierDebrisQuantity = 4;
        private const ushort BiteMidTierDebrisQuantity = 32;
        private const ushort BiteHighTierDebrisQuantity = 512;
        private const ushort BiteUltraTierDebrisQuantity = 2048;
        private const float BiteHullDentLowRadiusMeters = 0.35f;
        private const float BiteHullDentHighRadiusMeters = 1.35f;
        private const float BiteHullDentLowDepthMeters = 0.035f;
        private const float BiteHullDentHighDepthMeters = 0.28f;
        private const uint BiteSparksSignalHash = 0x42505453u; // BPTS

        private static readonly int _LeviathanBonesId = Shader.PropertyToID("_H8LeviathanBones");
        private static readonly int _LeviathanBoneCountId = Shader.PropertyToID("_H8LeviathanBoneCount");
        private static readonly int _LeviathanIkTierId = Shader.PropertyToID("_H8LeviathanIkTier");
        private static readonly int _LeviathanTailWhipId = Shader.PropertyToID("_H8LeviathanTailWhip01");
        private static readonly int _LeviathanSegmentLengthId = Shader.PropertyToID("_H8LeviathanSegmentLength");
        private static readonly int _LeviathanGpuSkinningId = Shader.PropertyToID("_H8LeviathanGpuSkinning");

        [Header("Spine")]
        [Tooltip("High-tier spine segment count. Low tier is hard-gated to eight segments.")]
        [SerializeField, Range(LowTierSegments, MaxSegments)] private int _highTierSegmentCount = MaxSegments;

        [Tooltip("Meters between consecutive procedural spine segments.")]
        [SerializeField, Range(0.25f, 8f)] private float _segmentLength = 2.5f;

        [Tooltip("Visual radius written into each procedural bone matrix.")]
        [SerializeField, Range(0.05f, 6f)] private float _bodyRadius = 1.15f;

        [Tooltip("Follower damping for the Verlet tail cache.")]
        [SerializeField, Range(0f, 1f)] private float _verletDamping = 0.87f;

        [Tooltip("High-tier constraint iterations. Low tier is forced to one.")]
        [SerializeField, Range(1, 4)] private int _highTierConstraintIterations = 3;

        [Header("Terrain Hugging")]
        [Tooltip("Meters added above SDF or heightmap contact to prevent visual z-fighting.")]
        [SerializeField, Range(0f, 2f)] private float _terrainClearance = 0.35f;

        [Tooltip("Enable SDF terrain pushout on non-low tiers.")]
        [SerializeField] private bool _enableSdfHugging = true;

        [Tooltip("Use cached MapMagic height samples when no SDF contact exists.")]
        [SerializeField] private bool _enableMapMagicFallback = true;

        [Header("Strike")]
        [Tooltip("Meters of procedural tail wave during a strike.")]
        [SerializeField, Range(0f, 12f)] private float _tailWhipAmplitudeMeters = 4.5f;

        [Tooltip("Seconds terrain constraints are bypassed for the strike tail wave.")]
        [SerializeField, Range(0.1f, 1.5f)] private float _tailWhipDurationSeconds = 1f;

        [Tooltip("Maximum jaw IK reach in meters before the procedural miss recovery takes over.")]
        [SerializeField, Range(1f, 30f)] private float _biteJawReachMeters = 10f;

        [Tooltip("Visual mandible opening offset in meters for non-low quality tiers.")]
        [SerializeField, Range(0f, 4f)] private float _biteJawOpenMeters = 0.8f;

        [Tooltip("Bounds padding used when deciding whether teeth have scraped the target hull.")]
        [SerializeField, Range(0f, 1f)] private float _biteContactPaddingMeters = 0.08f;

        [Header("GPU Skinning")]
        [Tooltip("Material using the existing compute/GPU skinning path. The bone buffer is rebound every visual sync.")]
        [SerializeField] private Material _skinningMaterial;

        [Tooltip("Also publish the current spine buffer as a global shader buffer for shared compute skinning.")]
        [SerializeField] private bool _publishGlobalBoneBuffer = true;

        private Rigidbody _body;
        private Transform _cachedTransform;
        private IDataVault _dataVault;
        private MapMagicBridge _mapMagic;

        private NativeArray<float3> _segmentPositions;
        private NativeArray<float3> _previousSegmentPositions;
        private NativeArray<float4x4> _leviathanBones;
        private NativeArray<LeviathanTerrainIkTelemetryEntry> _telemetryRing;
        private NativeArray<int> _telemetryCursor;

        private GraphicsBuffer _bonesGraphicsBufferA;
        private GraphicsBuffer _bonesGraphicsBufferB;
        private int _gpuUploadBufferIndex;

        private JobHandle _pendingHandle;
        private JobHandle _disposeHandle;
        private bool _solverScheduled;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredOriginShiftListener;
        private bool _disposed;
        private bool _telemetryDumped;
        private bool _biteTelemetryDumped;
        private bool _pendingOriginShiftRebase;
        private bool _gpuUploadDirty;
        private bool _strikeActive;
        private bool _wasStrikeActiveLastTick;
        private bool _headLookTargetActive;
        private bool _globalGpuSkinningPublished;
        private bool _gpuBufferDataValid;
        private bool _biteVaultReady;
        private bool _strikeSignalActive;
        private int _frameIndex;
        private int _lastBiteFeedbackFrame = -1;
        private int _lastBiteAudioFrame = -1;
        private int _activeSegmentCount = LowTierSegments;
        private int _resolvedConstraintIterations = 1;
        private int _pendingConstraintIterations = 1;
        private bool _motionIntentPending;
        private float _constraintIterationSwitchTimer;
        private float _tailWhipSecondsRemaining;
        private float _attackTelegraphBlend;
        private float3 _pendingOriginShiftOffset;
        private float3 _motionIntentVelocity;
        private float3 _motionIntentHeadTarget;
        private float3 _headLookTargetWorldPosition;
        private float3 _strikeTargetWorldPosition;
        private HectonQualityTier _qualityTier = HectonQualityTier.Unknown;
        private Transform _strikeTarget;
        private Rigidbody _strikeTargetRigidbody;
        private Collider _strikeTargetCollider;

        internal bool TryGetLeviathanBones(out NativeArray<float4x4>.ReadOnly bones, out int activeSegmentCount)
        {
            if (_disposed || _solverScheduled || !_leviathanBones.IsCreated || _activeSegmentCount <= 0)
            {
                bones = default;
                activeSegmentCount = 0;
                return false;
            }

            activeSegmentCount = math.min(_activeSegmentCount, _leviathanBones.Length);
            bones = _leviathanBones.AsReadOnly();
            return activeSegmentCount > 0;
        }

        internal bool TryGetLeviathanBoneGraphicsBuffer(out GraphicsBuffer buffer, out int activeSegmentCount)
        {
            if (_disposed || _activeSegmentCount <= 0)
            {
                buffer = null;
                activeSegmentCount = 0;
                return false;
            }

            activeSegmentCount = math.min(_activeSegmentCount, MaxSegments);
            GraphicsBuffer candidate = _gpuUploadBufferIndex == 0 ? _bonesGraphicsBufferB : _bonesGraphicsBufferA;
            if (!_gpuUploadDirty && _gpuBufferDataValid && HasValidGraphicsBuffer(candidate, activeSegmentCount))
            {
                buffer = candidate;
                return true;
            }

            buffer = null;
            activeSegmentCount = 0;
            return false;
        }

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _body);
            RefreshColdDependencies();
            EnsurePersistentBuffers();
            SeedSpineFromOwner();
        }

        private void OnEnable()
        {
            if (_disposed || !Application.isPlaying)
                return;

            CompleteScheduledSolverForLifecycle();
            RefreshColdDependencies();
            EnsurePersistentBuffers();
            SeedSpineFromOwner();
            ResetConstraintIterationHysteresis();
            TryRegister();
            TryRegisterOriginShiftListener();
        }

        private void OnDisable()
        {
            if (_disposed || !Application.isPlaying)
                return;

            TryUnregisterOriginShiftListener();
            TryUnregister();
            CompleteScheduledSolverForLifecycle();
            ClearGpuSkinningBinding();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            TryUnregisterOriginShiftListener();
            TryUnregister();
            ClearGpuSkinningBinding();
            DisposePersistentBuffers();
            ReleaseGraphicsBuffers();
        }

        public void Tick(float deltaTime)
        {
            if (_disposed ||
                !_segmentPositions.IsCreated ||
                !_leviathanBones.IsCreated ||
                _solverScheduled ||
                !math.isfinite(deltaTime) ||
                deltaTime <= 0f)
            {
                return;
            }

            HectonQualityTier qualityTier = GlobalRegistry.ScalabilityTier;
            float safeDeltaTime = math.min(math.max(0f, deltaTime), 0.05f);
            _qualityTier = qualityTier;
            ApplyPendingOriginShiftRebase();
            RefreshQualityState(safeDeltaTime, qualityTier);
            CaptureFallbackMotionIntent();
            ApplyPresentationIntentTargets();
            ResolveTerrainPayload(
                qualityTier,
                out NativeArray<byte> sdfTexture3D,
                out int3 sdfDimensions,
                out float3 sdfOrigin,
                out float3 sdfCellSize,
                out float sdfRange,
                out NativeArray<ushort> heightSamples,
                out float3 terrainOrigin,
                out float3 terrainSize,
                out int terrainResolution);

            float safeTailWhipSecondsRemaining = ResolveSafeTailWhipSecondsRemaining();
            if (safeTailWhipSecondsRemaining > 0f)
            {
                safeTailWhipSecondsRemaining = math.max(0f, safeTailWhipSecondsRemaining - safeDeltaTime);
                _tailWhipSecondsRemaining = safeTailWhipSecondsRemaining;
            }

            ConsumeStrikeSignals();
            float systemStress01 = ResolveSystemStress01();
            uint runtimeFlags = ResolveRuntimeFlags(qualityTier);
            bool biteTargetReady = PrepareBiteTarget(
                out NativeArray<JawIkTarget> jawIkTargets,
                out NativeArray<CurrentJawPos> currentJawPos,
                out NativeArray<BiteIkSolveEvent> biteIkSolveEvents,
                out NativeArray<int> biteIkTelemetryCursor);
            LeviathanTerrainIkJob job = new LeviathanTerrainIkJob
            {
                SegmentPositions = _segmentPositions,
                PreviousSegmentPositions = _previousSegmentPositions,
                LeviathanBones = _leviathanBones,
                TelemetryRing = _telemetryRing,
                TelemetryCursor = _telemetryCursor,
                VoxelSdfTexture3D = sdfTexture3D,
                TerrainHeightSamples = heightSamples,
                VoxelSdfDimensions = sdfDimensions,
                VoxelSdfOrigin = sdfOrigin,
                VoxelSdfCellSize = sdfCellSize,
                VoxelSdfRange = sdfRange,
                TerrainOrigin = terrainOrigin,
                TerrainSize = terrainSize,
                TerrainResolution = terrainResolution,
                DeltaTime = safeDeltaTime,
                Damping = _verletDamping,
                SegmentLength = _segmentLength,
                BodyRadius = _bodyRadius,
                TerrainClearance = _terrainClearance,
                TailWhipSecondsRemaining = safeTailWhipSecondsRemaining,
                TailWhipDurationSeconds = _tailWhipDurationSeconds,
                TailWhipAmplitudeMeters = _tailWhipAmplitudeMeters,
                HeadTargetPosition = _motionIntentHeadTarget,
                IntendedVelocity = _motionIntentVelocity,
                OwnerForward = ResolveOwnerForward(),
                WorldUp = new float3(0f, 1f, 0f),
                RequestedSegmentCount = _activeSegmentCount,
                ConstraintIterations = _resolvedConstraintIterations,
                FrameIndex = _frameIndex,
                RuntimeFlags = runtimeFlags
            };

            JobHandle scheduledHandle = job.Schedule();
            if (biteTargetReady)
            {
                ProceduralBiteJob biteJob = new ProceduralBiteJob
                {
                    JawIkTargets = jawIkTargets,
                    CurrentJawPos = currentJawPos,
                    LeviathanBones = _leviathanBones,
                    BiteIkSolveEvents = biteIkSolveEvents,
                    TelemetryCursor = biteIkTelemetryCursor,
                    PredatorAup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(ResolveOwnerRuntimePosition())),
                    PredatorPosition = ResolveOwnerRuntimePosition(),
                    PredatorForward = ResolveOwnerForward(),
                    PredatorUp = new float3(0f, 1f, 0f),
                    PredatorRight = ResolveOwnerRight(),
                    DeltaTime = safeDeltaTime,
                    BodyRadius = _bodyRadius,
                    SegmentLength = _segmentLength,
                    JawReachMeters = _biteJawReachMeters,
                    JawOpenMeters = _biteJawOpenMeters,
                    SystemStress01 = systemStress01,
                    TargetIndex = 0,
                    FrameIndex = _frameIndex,
                    HeadBoneIndex = ProceduralBiteIkConstants.DefaultHeadBoneIndex,
                    UpperJawBoneIndex = ProceduralBiteIkConstants.DefaultUpperJawBoneIndex,
                    LowerJawBoneIndex = ProceduralBiteIkConstants.DefaultLowerJawBoneIndex,
                    FirstTentacleBoneIndex = ProceduralBiteIkConstants.DefaultFirstTentacleBoneIndex,
                    TentacleBoneCount = ProceduralBiteIkConstants.MaxTentacleBones,
                    RuntimeFlags = ResolveBiteRuntimeFlags(qualityTier, systemStress01)
                };
                scheduledHandle = biteJob.Schedule(scheduledHandle);
            }

            _pendingHandle = scheduledHandle;
            _solverScheduled = true;
        }

        public void LateFrameTick()
        {
            if (_disposed)
                return;

            if (!_solverScheduled)
            {
                bool rebased = ApplyPendingOriginShiftRebase();
                if (rebased || _gpuUploadDirty)
                    _gpuUploadDirty = !UploadBonesToGpu();
                return;
            }

            if (!DispatcherJobSwap.TryComplete(ref _pendingHandle, forceComplete: false))
                return;

            _solverScheduled = false;
            AdvanceFrameIndex();
            ApplyPendingOriginShiftRebase();

            if (TelemetryHasInvalidFrame())
                DumpTelemetryBlackBoxOnce();

            PublishBiteFeedbackIfNeeded();
            _gpuUploadDirty = !UploadBonesToGpu();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (_disposed)
                return;

            float3 offset = (float3)shiftData.ShiftOffset;
            if (!math.all(math.isfinite(offset)))
            {
                DumpTelemetryBlackBoxOnce();
                return;
            }

            if (_solverScheduled)
            {
                if (!DispatcherJobSwap.TryFinalizeCompleted(ref _pendingHandle))
                {
                    QueueOriginShiftRebase(offset);
                    return;
                }

                _solverScheduled = false;
                AdvanceFrameIndex();
                if (TelemetryHasInvalidFrame())
                    DumpTelemetryBlackBoxOnce();
            }

            ApplyOriginShiftRebase(offset);
        }

        internal void BindFromFauna(FaunaBrain faunaBrain, Rigidbody body)
        {
            _body = body;
            _cachedTransform = faunaBrain != null ? faunaBrain.transform : transform;
            CompleteScheduledSolverForLifecycle();
            RefreshColdDependencies();
            EnsurePersistentBuffers();
            SeedSpineFromOwner();
            ResetConstraintIterationHysteresis();
        }

        internal void BindSkinningMaterial(Material material)
        {
            if (_skinningMaterial == material)
                return;

            ClearMaterialGpuSkinningBinding(_skinningMaterial);
            _skinningMaterial = material;
            ClearMaterialGpuSkinningBinding(_skinningMaterial);
            _gpuUploadDirty = material != null || _publishGlobalBoneBuffer;
            if (!_gpuUploadDirty)
                _gpuBufferDataValid = false;
        }

        internal void SetMotionIntent(Vector3 intendedVelocity, Vector3 headTargetWorldPosition)
        {
            _motionIntentVelocity = SanitizeFiniteInputFloat3((float3)intendedVelocity, float3.zero);
            _motionIntentHeadTarget = SanitizeFiniteInputFloat3((float3)headTargetWorldPosition, ResolveOwnerRuntimePosition());
            _motionIntentPending = true;
        }

        internal void SetStrikeIntent(Transform target, Vector3 targetWorldPosition, bool strikeActive)
        {
            _strikeActive = strikeActive && target != null;
            if (!_strikeActive)
            {
                _strikeTarget = null;
                _strikeTargetRigidbody = null;
                _strikeTargetCollider = null;
                _wasStrikeActiveLastTick = false;
                return;
            }

            if (_strikeTarget != target)
            {
                _strikeTarget = target;
                _strikeTargetRigidbody = null;
                _strikeTargetCollider = null;
                target.TryGetComponent(out _strikeTargetRigidbody);
                target.TryGetComponent(out _strikeTargetCollider);
            }

            _strikeTargetWorldPosition = _strikeTargetRigidbody != null
                ? SanitizeFiniteInputFloat3((float3)_strikeTargetRigidbody.position, ResolveOwnerRuntimePosition())
                : SanitizeFiniteInputFloat3((float3)targetWorldPosition, ResolveOwnerRuntimePosition());

            if (!_wasStrikeActiveLastTick)
                _tailWhipSecondsRemaining = math.max(
                    SanitizePositiveFinite(_tailWhipSecondsRemaining, 0f, 0f),
                    SanitizePositiveFinite(_tailWhipDurationSeconds, 1f, 0.0001f));

            _wasStrikeActiveLastTick = true;
        }

        internal void SetAttackTelegraph(float blend01)
        {
            _attackTelegraphBlend = math.isfinite(blend01) ? math.saturate(blend01) : 0f;
        }

        internal void SetHeadLookTarget(Vector3 worldPosition, bool active)
        {
            _headLookTargetWorldPosition = SanitizeFiniteInputFloat3((float3)worldPosition, ResolveOwnerRuntimePosition());
            _headLookTargetActive = active;
        }

        private void RefreshColdDependencies()
        {
            _dataVault = GlobalRegistry.DataVault;
            _mapMagic = GlobalRegistry.MapMagic;
        }

        private void EnsurePersistentBuffers()
        {
            if (_segmentPositions.IsCreated &&
                _previousSegmentPositions.IsCreated &&
                _leviathanBones.IsCreated &&
                _telemetryRing.IsCreated &&
                _telemetryCursor.IsCreated)
            {
                TryResolveBiteIkVaultBuffers(out _, out _, out _, out _);
                return;
            }

            DisposePersistentBuffers();
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                ClearNativeBufferViews();
                return;
            }

            _segmentPositions = vault.GetBuffer<float3>(BufferID.LeviathanSegmentPositions, MaxSegments, SystemID.AnimationFauna, NativeArrayOptions.ClearMemory);
            _previousSegmentPositions = vault.GetBuffer<float3>(BufferID.LeviathanPreviousSegmentPositions, MaxSegments, SystemID.AnimationFauna, NativeArrayOptions.ClearMemory);
            _leviathanBones = vault.GetBuffer<float4x4>(BufferID.LeviathanBoneMatrices, MaxSegments, SystemID.AnimationFauna, NativeArrayOptions.ClearMemory);
            _telemetryRing = vault.GetBuffer<LeviathanTerrainIkTelemetryEntry>(BufferID.LeviathanTerrainIkTelemetryRing, LeviathanTerrainIkConstants.TelemetryCapacity, SystemID.AnimationFauna, NativeArrayOptions.ClearMemory);
            _telemetryCursor = vault.GetBuffer<int>(BufferID.LeviathanTerrainIkTelemetryCursor, 1, SystemID.AnimationFauna, NativeArrayOptions.ClearMemory);
            TryResolveBiteIkVaultBuffers(out _, out _, out _, out _);
        }

        private bool TryResolveBiteIkVaultBuffers(
            out NativeArray<JawIkTarget> jawIkTargets,
            out NativeArray<CurrentJawPos> currentJawPos,
            out NativeArray<BiteIkSolveEvent> biteIkSolveEvents,
            out NativeArray<int> biteIkTelemetryCursor)
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                _biteVaultReady = false;
                jawIkTargets = default;
                currentJawPos = default;
                biteIkSolveEvents = default;
                biteIkTelemetryCursor = default;
                return false;
            }

            jawIkTargets = vault.GetBuffer<JawIkTarget>(
                BufferID.JawIkTargets,
                ProceduralBiteIkConstants.TargetCapacity,
                SystemID.AnimationFauna,
                NativeArrayOptions.ClearMemory);
            currentJawPos = vault.GetBuffer<CurrentJawPos>(
                BufferID.CurrentJawPos,
                ProceduralBiteIkConstants.CurrentJawPoseCapacity,
                SystemID.AnimationFauna,
                NativeArrayOptions.ClearMemory);
            biteIkSolveEvents = vault.GetBuffer<BiteIkSolveEvent>(
                BufferID.BiteIkSolveEvents,
                ProceduralBiteIkConstants.TelemetryCapacity,
                SystemID.AnimationFauna,
                NativeArrayOptions.ClearMemory);
            biteIkTelemetryCursor = vault.GetBuffer<int>(
                BufferID.BiteIkTelemetryCursor,
                1,
                SystemID.AnimationFauna,
                NativeArrayOptions.ClearMemory);
            _biteVaultReady = jawIkTargets.IsCreated &&
                              currentJawPos.IsCreated &&
                              biteIkSolveEvents.IsCreated &&
                              biteIkTelemetryCursor.IsCreated;
            return _biteVaultReady;
        }

        private void DisposePersistentBuffers()
        {
            if (_solverScheduled)
                DispatcherJobSwap.TryComplete(ref _pendingHandle, forceComplete: true);

            _disposeHandle = default;
            DispatcherJobSwap.TryFinalizeCompleted(ref _disposeHandle);
            _pendingHandle = default;
            _solverScheduled = false;
            _gpuUploadDirty = false;
            _gpuBufferDataValid = false;
            ClearNativeBufferViews();
        }

        private void ClearNativeBufferViews()
        {
            _segmentPositions = default;
            _previousSegmentPositions = default;
            _leviathanBones = default;
            _telemetryRing = default;
            _telemetryCursor = default;
            _biteVaultReady = false;
        }

        private void CompleteScheduledSolverForLifecycle()
        {
            if (!_solverScheduled)
                return;

            DispatcherJobSwap.TryComplete(ref _pendingHandle, forceComplete: true);
            _solverScheduled = false;
            AdvanceFrameIndex();
            if (TelemetryHasInvalidFrame())
                DumpTelemetryBlackBoxOnce();
        }

        private void SeedSpineFromOwner()
        {
            if (!_segmentPositions.IsCreated || !_previousSegmentPositions.IsCreated || !_leviathanBones.IsCreated)
                return;

            float3 origin = ResolveOwnerRuntimePosition();
            float3 forward = ResolveOwnerForward();
            float segmentLength = SanitizePositiveFinite(_segmentLength, LeviathanTerrainIkConstants.DefaultSegmentLength, LeviathanTerrainIkConstants.MinSegmentLength);
            float bodyRadius = SanitizePositiveFinite(_bodyRadius, 1.15f, 0.01f);
            for (int i = 0; i < MaxSegments; i++)
            {
                float3 position = origin - forward * (segmentLength * i);
                _segmentPositions[i] = position;
                _previousSegmentPositions[i] = position;
                _leviathanBones[i] = float4x4.TRS(position, quaternion.LookRotationSafe(forward, new float3(0f, 1f, 0f)), new float3(bodyRadius, bodyRadius, segmentLength));
            }

            _motionIntentVelocity = float3.zero;
            _motionIntentHeadTarget = origin + forward * segmentLength;
            _headLookTargetWorldPosition = _motionIntentHeadTarget;
            _strikeTargetWorldPosition = _motionIntentHeadTarget;
            _motionIntentPending = false;
            _gpuUploadDirty = true;
            _gpuBufferDataValid = false;
        }

        private void CaptureFallbackMotionIntent()
        {
            float3 ownerPosition = ResolveOwnerRuntimePosition();
            float3 ownerForward = ResolveOwnerForward();
            if (_motionIntentPending)
            {
                _motionIntentPending = false;
                return;
            }

            float3 velocity = _body != null
                ? SanitizeFiniteInputFloat3((float3)_body.linearVelocity, float3.zero)
                : float3.zero;
            float bodySpeed = ResolveBodySpeed();
            if (math.lengthsq(velocity) <= MinVectorMagnitudeSq)
                velocity = ownerForward * math.max(0.1f, bodySpeed);

            _motionIntentVelocity = velocity;
            float segmentLength = SanitizePositiveFinite(_segmentLength, LeviathanTerrainIkConstants.DefaultSegmentLength, LeviathanTerrainIkConstants.MinSegmentLength);
            _motionIntentHeadTarget = ownerPosition + NormalizeSafe(velocity, ownerForward) * math.max(segmentLength, bodySpeed * 0.35f);
        }

        private void ApplyPresentationIntentTargets()
        {
            if (_strikeActive)
            {
                _motionIntentHeadTarget = SanitizeFiniteInputFloat3(_strikeTargetWorldPosition, _motionIntentHeadTarget);
                return;
            }

            if (!_headLookTargetActive)
                return;

            float blend = math.saturate(0.35f + _attackTelegraphBlend * 0.35f);
            _motionIntentHeadTarget = math.lerp(
                _motionIntentHeadTarget,
                SanitizeFiniteInputFloat3(_headLookTargetWorldPosition, _motionIntentHeadTarget),
                blend);
        }

        private bool PrepareBiteTarget(
            out NativeArray<JawIkTarget> jawIkTargets,
            out NativeArray<CurrentJawPos> currentJawPos,
            out NativeArray<BiteIkSolveEvent> biteIkSolveEvents,
            out NativeArray<int> biteIkTelemetryCursor)
        {
            if (!TryResolveBiteIkVaultBuffers(out jawIkTargets, out currentJawPos, out biteIkSolveEvents, out biteIkTelemetryCursor))
                return false;

            bool active = _strikeActive || _strikeSignalActive;
            if (!active || _strikeTarget == null)
            {
                ClearBiteTarget(jawIkTargets, currentJawPos);
                return false;
            }

            Bounds bounds;
            if (_strikeTargetCollider != null)
            {
                bounds = _strikeTargetCollider.bounds;
                if (!IsFiniteBounds(bounds) || bounds.extents.sqrMagnitude <= MinVectorMagnitudeSq)
                    bounds = BuildFallbackStrikeBounds(_strikeTargetWorldPosition);
            }
            else
            {
                bounds = BuildFallbackStrikeBounds(_strikeTargetWorldPosition);
            }

            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            Transform targetTransform = _strikeTarget;
            uint targetHash = _strikeTargetCollider != null
                ? unchecked((uint)_strikeTargetCollider.GetInstanceID())
                : unchecked((uint)targetTransform.GetInstanceID());
            if (targetHash == 0u)
                targetHash = 1u;

            JawIkTarget target = new JawIkTarget
            {
                CenterAup = AbsoluteUniversePosition.FromRuntimePosition(center),
                RuntimeCenter = SanitizeFiniteInputFloat3((float3)center, ResolveOwnerRuntimePosition()),
                Extents = SanitizeFiniteInputFloat3((float3)extents, new float3(0.5f)),
                Forward = targetTransform != null ? SanitizeFiniteInputFloat3((float3)targetTransform.forward, new float3(0f, 0f, 1f)) : new float3(0f, 0f, 1f),
                Up = targetTransform != null ? SanitizeFiniteInputFloat3((float3)targetTransform.up, new float3(0f, 1f, 0f)) : new float3(0f, 1f, 0f),
                Right = targetTransform != null ? SanitizeFiniteInputFloat3((float3)targetTransform.right, new float3(1f, 0f, 0f)) : new float3(1f, 0f, 0f),
                MaxReachMeters = SanitizePositiveFinite(_biteJawReachMeters, ProceduralBiteIkConstants.DefaultJawReachMeters, 0.1f),
                CylinderRadiusMeters = math.max(extents.x, extents.z),
                ContactPaddingMeters = SanitizePositiveFinite(_biteContactPaddingMeters, 0.08f, 0f),
                TargetHash = targetHash,
                Frame = unchecked((uint)_frameIndex)
            };

            jawIkTargets[0] = target;
            return true;
        }

        private void ClearBiteTarget(NativeArray<JawIkTarget> jawIkTargets, NativeArray<CurrentJawPos> currentJawPos)
        {
            if (jawIkTargets.IsCreated && jawIkTargets.Length > 0)
                jawIkTargets[0] = default;

            if (!currentJawPos.IsCreated || currentJawPos.Length <= 0)
                return;

            float3 ownerPosition = ResolveOwnerRuntimePosition();
            float3 ownerForward = ResolveOwnerForward();
            float safeSegmentLength = SanitizePositiveFinite(_segmentLength, LeviathanTerrainIkConstants.DefaultSegmentLength, LeviathanTerrainIkConstants.MinSegmentLength);
            CurrentJawPos restPose = default;
            restPose.HeadPosition = ownerPosition;
            restPose.HeadRotation = quaternion.LookRotationSafe(ownerForward, new float3(0f, 1f, 0f));
            restPose.JawTipPosition = ownerPosition + ownerForward * safeSegmentLength;
            restPose.Frame = unchecked((uint)math.max(0, _frameIndex));
            restPose.StateHash = 1u;
            currentJawPos[0] = restPose;
        }

        private void ConsumeStrikeSignals()
        {
            _strikeSignalActive = false;
            ReadOnlySpan<FaunaStateChangedSignal> signals = SignalBus<FaunaStateChangedSignal>.GetFrameSnapshot();
            if (signals.Length <= 0)
                return;

            AbsoluteUniversePosition ownerAup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(ResolveOwnerRuntimePosition()));
            for (int i = 0; i < signals.Length; i++)
            {
                FaunaStateChangedSignal signal = signals[i];
                if (signal.StateKind != FaunaStateChangedSignalKinds.Strike)
                    continue;

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in ownerAup, in signal.PositionAup);
                if (distanceSq > 3600d)
                    continue;

                _strikeSignalActive = (signal.Flags & FaunaStateChangedSignalFlags.StateActive) != 0;
            }
        }

        private float ResolveSystemStress01()
        {
            float stress01 = 0f;
            ReadOnlySpan<SystemHealthIndexSignal> signals = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
                stress01 = math.max(stress01, math.saturate(signals[i].Pressure01));
            return stress01;
        }

        private uint ResolveBiteRuntimeFlags(HectonQualityTier tier, float systemStress01)
        {
            uint flags = 0u;
            if (_strikeActive || _strikeSignalActive)
                flags |= ProceduralBiteIkConstants.RuntimeFlagStrikeActive;
            if (IsLowTier(tier))
                flags |= ProceduralBiteIkConstants.RuntimeFlagLowTier;
            if (tier == HectonQualityTier.High)
                flags |= ProceduralBiteIkConstants.RuntimeFlagHighTier;
            if (tier == HectonQualityTier.Ultra)
                flags |= ProceduralBiteIkConstants.RuntimeFlagUltraTier;
            if (systemStress01 > ProceduralBiteIkConstants.StressFallbackThreshold01)
                flags |= ProceduralBiteIkConstants.RuntimeFlagSystemStressFallback;
            return flags;
        }

        private void PublishBiteFeedbackIfNeeded()
        {
            if (!TryResolveBiteIkVaultBuffers(
                    out _,
                    out NativeArray<CurrentJawPos> currentJawPos,
                    out _,
                    out _) ||
                currentJawPos.Length <= 0)
                return;

            CurrentJawPos pose = currentJawPos[0];
            if (pose.TargetHash == 0u)
                return;

            uint completedFrame = unchecked((uint)(_frameIndex == 0 ? int.MaxValue : _frameIndex - 1));
            uint currentFrame = unchecked((uint)math.max(0, _frameIndex));
            if (pose.Frame != completedFrame && pose.Frame != currentFrame)
                return;

            if ((pose.Flags & ProceduralBiteIkConstants.ResultFlagInvalid) != 0u)
                DumpBiteTelemetryBlackBoxOnce();

            int frame = Time.frameCount;
            if ((pose.Flags & ProceduralBiteIkConstants.ResultFlagFeedback) != 0u &&
                frame - _lastBiteFeedbackFrame >= math.max(1, (int)math.ceil(BiteFeedbackCooldownSeconds * 60f)))
            {
                _lastBiteFeedbackFrame = frame;
                AbsoluteUniversePosition pointAup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(pose.JawTipPosition));
                DebrisSpawnSignal debris = default;
                debris.PositionAup = pointAup;
                debris.SpeciesHash = BiteSparksSignalHash;
                debris.SourceEntityId = pose.TargetHash;
                debris.Intensity01 = math.saturate(1f - pose.ContactDistanceMeters);
                debris.DebrisKind = DebrisSpawnSignal.DebrisKindSparks;
                debris.Flags = ResolveBiteDebrisFlags(pose.Flags);
                debris.Quantity = ResolveBiteDebrisQuantity(_qualityTier, pose.Flags);
                GlobalSignals.Publish(in debris);
                PublishBiteHullDent(in pose, frame, debris.Intensity01);

                HapticRequest haptic = default;
                haptic.Intensity01 = debris.Intensity01;
                haptic.DurationSeconds = math.lerp(0.05f, 0.18f, haptic.Intensity01);
                haptic.Frequency01 = 0.85f;
                haptic.SourceHash = pose.TargetHash;
                haptic.Frame = unchecked((uint)frame);
                haptic.Channel = HapticRequest.ChannelCrush;
                haptic.Flags = HapticRequest.FlagCrush;
                GlobalSignals.Publish(in haptic);
            }

            if ((pose.Flags & ProceduralBiteIkConstants.ResultFlagAudioJawSnap) != 0u &&
                frame - _lastBiteAudioFrame >= math.max(1, (int)math.ceil(BiteAudioCooldownSeconds * 60f)))
            {
                _lastBiteAudioFrame = frame;
                AcousticPingSignal signal = default;
                signal.PositionAup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(pose.JawTipPosition));
                signal.RadiusMeters = 18f;
                signal.Intensity01 = math.saturate(1f - pose.TargetDistanceMeters * 0.5f);
                signal.SourceId = pose.TargetHash;
                signal.Channel = AcousticPingSignal.ChannelJawSnap;
                signal.Flags = AcousticPingSignal.FlagJawSnap;
                GlobalSignals.Publish(in signal);
            }
        }

        private void PublishBiteHullDent(in CurrentJawPos pose, int frame, float intensity01)
        {
            bool overkill = (pose.Flags & ProceduralBiteIkConstants.ResultFlagVisualOverkill) != 0u;
            float radius = math.lerp(BiteHullDentLowRadiusMeters, BiteHullDentHighRadiusMeters, overkill ? intensity01 : intensity01 * 0.35f);
            float depth = math.lerp(BiteHullDentLowDepthMeters, BiteHullDentHighDepthMeters, overkill ? intensity01 : intensity01 * 0.25f);
            byte flags = HullDeformedSignal.LegacyLocalPointFlag;
            if (IsLowTier(_qualityTier))
                flags |= HullDeformedSignal.LowTierVisualOnlyFlag;

            HullDeformedSignal dent = default;
            dent.LocalPoint = SanitizeFiniteInputFloat3(pose.JawTipPosition, ResolveOwnerRuntimePosition());
            dent.Radius = SanitizePositiveFinite(radius, BiteHullDentLowRadiusMeters, 0.01f);
            dent.Depth = SanitizePositiveFinite(depth, BiteHullDentLowDepthMeters, 0f);
            dent.Intensity01 = math.saturate(intensity01);
            dent.TargetHash = pose.TargetHash;
            dent.SourceHash = BiteSparksSignalHash;
            dent.Frame = unchecked((uint)math.max(0, frame));
            dent.TargetId = ClampHashToUShort(pose.TargetHash);
            dent.SourceId = 0;
            dent.ActiveDentCount = 0;
            dent.Flags = flags;
            dent.QualityTier = ResolveQualityTierByte(_qualityTier);
            dent.Channel = AcousticPingSignal.ChannelJawSnap;
            dent.DamageType = BiteSparksSignalHash;
            GlobalSignals.Publish(in dent);
        }

        private static byte ResolveBiteDebrisFlags(uint poseFlags)
        {
            byte flags = DebrisSpawnSignal.FlagToolSparks;
            if ((poseFlags & ProceduralBiteIkConstants.ResultFlagVisualOverkill) != 0u)
                flags |= DebrisSpawnSignal.FlagComputeShard;
            return flags;
        }

        private static ushort ResolveBiteDebrisQuantity(HectonQualityTier tier, uint poseFlags)
        {
            if ((poseFlags & ProceduralBiteIkConstants.ResultFlagVisualOverkill) != 0u)
                return tier == HectonQualityTier.Ultra ? BiteUltraTierDebrisQuantity : BiteHighTierDebrisQuantity;
            if (tier == HectonQualityTier.Mid)
                return BiteMidTierDebrisQuantity;
            return BiteLowTierDebrisQuantity;
        }

        private static ushort ClampHashToUShort(uint value)
        {
            return value > ushort.MaxValue ? ushort.MaxValue : (ushort)value;
        }

        private static byte ResolveQualityTierByte(HectonQualityTier tier)
        {
            return (byte)math.clamp((int)tier, 0, byte.MaxValue);
        }

        private static Bounds BuildFallbackStrikeBounds(float3 center)
        {
            return new Bounds(ToVector3(center), new Vector3(1.2f, 1.2f, 1.2f));
        }

        private static bool IsFiniteBounds(Bounds bounds)
        {
            return IsFiniteVector(bounds.center) &&
                   IsFiniteVector(bounds.extents) &&
                   IsFiniteVector(bounds.size);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private void RefreshQualityState(float deltaTime, HectonQualityTier tier)
        {
            int requestedSegmentCount = IsLowTier(tier)
                ? LowTierSegments
                : math.clamp(_highTierSegmentCount, LowTierSegments, MaxSegments);
            _activeSegmentCount = requestedSegmentCount;

            int requestedIterations = IsLowTier(tier) ? 1 : math.clamp(_highTierConstraintIterations, 1, 4);
            if (_resolvedConstraintIterations < 1)
            {
                _resolvedConstraintIterations = requestedIterations;
                _pendingConstraintIterations = requestedIterations;
                _constraintIterationSwitchTimer = 0f;
                return;
            }

            if (requestedIterations == _resolvedConstraintIterations)
            {
                _pendingConstraintIterations = requestedIterations;
                _constraintIterationSwitchTimer = 0f;
                return;
            }

            if (requestedIterations != _pendingConstraintIterations)
            {
                _pendingConstraintIterations = requestedIterations;
                _constraintIterationSwitchTimer = 0f;
                return;
            }

            _constraintIterationSwitchTimer += math.max(0f, deltaTime);
            if (_constraintIterationSwitchTimer >= ConstraintIterationHysteresisSeconds)
            {
                _resolvedConstraintIterations = requestedIterations;
                _constraintIterationSwitchTimer = 0f;
            }
        }

        private void ResolveTerrainPayload(
            HectonQualityTier qualityTier,
            out NativeArray<byte> sdfTexture3D,
            out int3 sdfDimensions,
            out float3 sdfOrigin,
            out float3 sdfCellSize,
            out float sdfRange,
            out NativeArray<ushort> heightSamples,
            out float3 terrainOrigin,
            out float3 terrainSize,
            out int terrainResolution)
        {
            sdfTexture3D = default;
            sdfDimensions = default;
            sdfOrigin = float3.zero;
            sdfCellSize = float3.zero;
            sdfRange = 0f;
            heightSamples = default;
            terrainOrigin = float3.zero;
            terrainSize = float3.zero;
            terrainResolution = 0;

            float3 ownerPosition = ResolveOwnerRuntimePosition();
            ResolveSdfPayload(qualityTier, ownerPosition, out sdfTexture3D, out sdfDimensions, out sdfOrigin, out sdfCellSize, out sdfRange);
            ResolveMapMagicPayload(ownerPosition, out heightSamples, out terrainOrigin, out terrainSize, out terrainResolution);
        }

        private void ResolveSdfPayload(
            HectonQualityTier qualityTier,
            float3 targetPosition,
            out NativeArray<byte> sdfTexture3D,
            out int3 sdfDimensions,
            out float3 sdfOrigin,
            out float3 sdfCellSize,
            out float sdfRange)
        {
            sdfTexture3D = default;
            sdfDimensions = default;
            sdfOrigin = float3.zero;
            sdfCellSize = float3.zero;
            sdfRange = 0f;

            if (!_enableSdfHugging || IsLowTier(qualityTier))
                return;

            if (!HectonVoxelVolume.TryGetClosestPublishedSonarSdfPayload(
                    ToVector3(targetPosition),
                    out NativeArray<byte> publishedSdf,
                    out _,
                    out Vector3Int dimensions,
                    out Vector3 origin,
                    out Vector3 cellSize,
                    out float range,
                    out _))
            {
                return;
            }

            int3 resolvedDimensions = new int3(dimensions.x, dimensions.y, dimensions.z);
            if (!LeviathanTerrainIkJob.TryResolveSdfVoxelCount(resolvedDimensions, out int expectedLength) ||
                !publishedSdf.IsCreated ||
                publishedSdf.Length != expectedLength)
            {
                return;
            }

            NativeArray<byte> resolvedSdf = publishedSdf;
            IDataVault vault = _dataVault;
            if (vault != null &&
                vault.TryGetBuffer(BufferID.VoxelSdfTexture3D, out NativeArray<byte> vaultSdf) &&
                vaultSdf.IsCreated &&
                vaultSdf.Length == expectedLength)
            {
                resolvedSdf = vaultSdf;
            }

            sdfTexture3D = resolvedSdf;
            sdfDimensions = resolvedDimensions;
            sdfOrigin = (float3)origin;
            sdfCellSize = (float3)cellSize;
            sdfRange = math.max(0f, range);
        }

        private void ResolveMapMagicPayload(
            float3 targetPosition,
            out NativeArray<ushort> heightSamples,
            out float3 terrainOrigin,
            out float3 terrainSize,
            out int terrainResolution)
        {
            heightSamples = default;
            terrainOrigin = float3.zero;
            terrainSize = float3.zero;
            terrainResolution = 0;
            if (!_enableMapMagicFallback || _mapMagic == null)
                return;

            if (!_mapMagic.TryGetQuantizedHeightmapPayload(targetPosition.x, targetPosition.z, out MapMagicBridge.QuantizedHeightmapPayload payload) ||
                !payload.IsValid)
            {
                return;
            }

            if (!LeviathanTerrainIkJob.TryResolveTerrainHeightSampleCount(payload.HeightmapResolution, out int expectedLength) ||
                !payload.HeightSamples.IsCreated ||
                payload.HeightSamples.Length < expectedLength)
            {
                return;
            }

            float3 resolvedTerrainOrigin = (float3)payload.TerrainPosition;
            float3 resolvedTerrainSize = (float3)payload.TerrainSize;
            if (!math.all(math.isfinite(resolvedTerrainOrigin)) ||
                !math.all(math.isfinite(resolvedTerrainSize)) ||
                resolvedTerrainSize.x <= LeviathanTerrainIkConstants.MinTerrainSize ||
                resolvedTerrainSize.y <= LeviathanTerrainIkConstants.MinTerrainSize ||
                resolvedTerrainSize.z <= LeviathanTerrainIkConstants.MinTerrainSize)
            {
                return;
            }

            NativeArray<ushort> resolvedHeight = payload.HeightSamples;
            IDataVault vault = _dataVault;
            if (vault != null &&
                vault.TryGetBuffer(BufferID.TerrainSeamHeightmap, out NativeArray<ushort> vaultHeightmap) &&
                vaultHeightmap.IsCreated &&
                vaultHeightmap.Length == expectedLength)
            {
                resolvedHeight = vaultHeightmap;
            }

            heightSamples = resolvedHeight;
            terrainOrigin = resolvedTerrainOrigin;
            terrainSize = resolvedTerrainSize;
            terrainResolution = payload.HeightmapResolution;
        }

        private bool UploadBonesToGpu()
        {
            if (!_leviathanBones.IsCreated)
            {
                _gpuBufferDataValid = false;
                ClearGpuSkinningBinding();
                return false;
            }

            if (!_publishGlobalBoneBuffer && _globalGpuSkinningPublished)
                ClearGlobalGpuSkinningBinding();

            if (_skinningMaterial == null && !_publishGlobalBoneBuffer)
            {
                _gpuBufferDataValid = false;
                return true;
            }

            EnsureGraphicsBuffers();
            GraphicsBuffer writeBuffer = _gpuUploadBufferIndex == 0 ? _bonesGraphicsBufferA : _bonesGraphicsBufferB;
            if (!HasValidGraphicsBuffer(writeBuffer, MaxSegments))
            {
                _gpuBufferDataValid = false;
                ClearGpuSkinningBinding();
                return false;
            }

            GraphicsBufferUploadUtility.UploadNativeArray(writeBuffer, _leviathanBones, MaxSegments);
            float ikTier = IsLowTier(_qualityTier) ? 0f : 1f;
            float safeSegmentLength = SanitizePositiveFinite(_segmentLength, LeviathanTerrainIkConstants.DefaultSegmentLength, LeviathanTerrainIkConstants.MinSegmentLength);
            float safeTailWhipDuration = SanitizePositiveFinite(_tailWhipDurationSeconds, 1f, 0.0001f);
            float safeTailWhipSecondsRemaining = ResolveSafeTailWhipSecondsRemaining();
            float tailWhip01 = math.saturate(safeTailWhipSecondsRemaining * math.rcp(safeTailWhipDuration));
            if (_skinningMaterial != null)
            {
                _skinningMaterial.SetBuffer(_LeviathanBonesId, writeBuffer);
                _skinningMaterial.SetFloat(_LeviathanBoneCountId, _activeSegmentCount);
                _skinningMaterial.SetFloat(_LeviathanIkTierId, ikTier);
                _skinningMaterial.SetFloat(_LeviathanTailWhipId, tailWhip01);
                _skinningMaterial.SetFloat(_LeviathanSegmentLengthId, safeSegmentLength);
                _skinningMaterial.SetFloat(_LeviathanGpuSkinningId, 1f);
            }

            if (_publishGlobalBoneBuffer)
            {
                Shader.SetGlobalBuffer(_LeviathanBonesId, writeBuffer);
                Shader.SetGlobalFloat(_LeviathanBoneCountId, _activeSegmentCount);
                Shader.SetGlobalFloat(_LeviathanIkTierId, ikTier);
                Shader.SetGlobalFloat(_LeviathanTailWhipId, tailWhip01);
                Shader.SetGlobalFloat(_LeviathanSegmentLengthId, safeSegmentLength);
                Shader.SetGlobalFloat(_LeviathanGpuSkinningId, 1f);
                _globalGpuSkinningPublished = true;
            }

            _gpuUploadBufferIndex ^= 1;
            _gpuBufferDataValid = true;
            return true;
        }

        private void ClearGpuSkinningBinding()
        {
            _gpuBufferDataValid = false;
            ClearMaterialGpuSkinningBinding(_skinningMaterial);

            if (_publishGlobalBoneBuffer || _globalGpuSkinningPublished)
                ClearGlobalGpuSkinningBinding();
        }

        private static void ClearMaterialGpuSkinningBinding(Material material)
        {
            if (material == null)
                return;

            material.SetFloat(_LeviathanBoneCountId, 0f);
            material.SetFloat(_LeviathanIkTierId, 0f);
            material.SetFloat(_LeviathanTailWhipId, 0f);
            material.SetFloat(_LeviathanSegmentLengthId, LeviathanTerrainIkConstants.DefaultSegmentLength);
            material.SetFloat(_LeviathanGpuSkinningId, 0f);
        }

        private void ClearGlobalGpuSkinningBinding()
        {
            Shader.SetGlobalFloat(_LeviathanBoneCountId, 0f);
            Shader.SetGlobalFloat(_LeviathanIkTierId, 0f);
            Shader.SetGlobalFloat(_LeviathanTailWhipId, 0f);
            Shader.SetGlobalFloat(_LeviathanSegmentLengthId, LeviathanTerrainIkConstants.DefaultSegmentLength);
            Shader.SetGlobalFloat(_LeviathanGpuSkinningId, 0f);
            _globalGpuSkinningPublished = false;
        }

        private void EnsureGraphicsBuffers()
        {
            if (!HasValidGraphicsBuffer(_bonesGraphicsBufferA, MaxSegments))
            {
                ReleaseGraphicsBuffer(ref _bonesGraphicsBufferA);
                _bonesGraphicsBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4x4>(MaxSegments); // COLD ALLOC: GraphicsBuffer[20 float4x4] - leviathan bone upload A - owner: FaunaKinematicsRuntime
            }

            if (!HasValidGraphicsBuffer(_bonesGraphicsBufferB, MaxSegments))
            {
                ReleaseGraphicsBuffer(ref _bonesGraphicsBufferB);
                _bonesGraphicsBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4x4>(MaxSegments); // COLD ALLOC: GraphicsBuffer[20 float4x4] - leviathan bone upload B - owner: FaunaKinematicsRuntime
            }
        }

        private void ReleaseGraphicsBuffers()
        {
            ReleaseGraphicsBuffer(ref _bonesGraphicsBufferA);
            ReleaseGraphicsBuffer(ref _bonesGraphicsBufferB);
            _gpuBufferDataValid = false;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            if (buffer.IsValid())
                buffer.Release();
            buffer = null;
        }

        private static bool HasValidGraphicsBuffer(GraphicsBuffer buffer, int requiredCount)
        {
            return buffer != null && buffer.IsValid() && buffer.count >= requiredCount;
        }

        private void TryRegister()
        {
            if (GlobalRegistry.Dispatcher == null)
                return;

            if (_registeredUpdate && _registeredLateFrame)
                return;

            if (_registeredUpdate || _registeredLateFrame)
                TryUnregister();

            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            if (_registeredUpdate && _registeredLateFrame)
                return;

            if (_registeredUpdate)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            if (_registeredLateFrame)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

            _registeredUpdate = false;
            _registeredLateFrame = false;
        }

        private void TryUnregister()
        {
            if (_registeredUpdate)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            if (_registeredLateFrame)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

            _registeredUpdate = false;
            _registeredLateFrame = false;
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShiftListener = false;
        }

        private void QueueOriginShiftRebase(float3 offset)
        {
            _pendingOriginShiftOffset += offset;
            _pendingOriginShiftRebase = true;
        }

        private bool ApplyPendingOriginShiftRebase()
        {
            if (!_pendingOriginShiftRebase)
                return false;

            float3 offset = _pendingOriginShiftOffset;
            _pendingOriginShiftOffset = float3.zero;
            _pendingOriginShiftRebase = false;
            ApplyOriginShiftRebase(offset);
            return true;
        }

        private void ApplyOriginShiftRebase(float3 offset)
        {
            if (!_segmentPositions.IsCreated || !_previousSegmentPositions.IsCreated || !_leviathanBones.IsCreated)
                return;

            if (!math.all(math.isfinite(offset)))
            {
                DumpTelemetryBlackBoxOnce();
                return;
            }

            for (int i = 0; i < MaxSegments; i++)
            {
                _segmentPositions[i] = SanitizeFiniteInputFloat3(_segmentPositions[i] - offset, float3.zero);
                _previousSegmentPositions[i] = SanitizeFiniteInputFloat3(_previousSegmentPositions[i] - offset, _segmentPositions[i]);
                float4x4 matrix = _leviathanBones[i];
                float4 c3 = matrix.c3;
                float3 matrixRawPosition = new float3(c3.x - offset.x, c3.y - offset.y, c3.z - offset.z);
                float3 matrixPosition = SanitizeFiniteInputFloat3(matrixRawPosition, _segmentPositions[i]);
                matrix.c3 = new float4(matrixPosition, math.isfinite(c3.w) ? c3.w : 1f);
                _leviathanBones[i] = matrix;
            }

            float3 ownerFallback = ResolveOwnerRuntimePosition();
            _motionIntentHeadTarget = SanitizeFiniteInputFloat3(_motionIntentHeadTarget - offset, ownerFallback);
            _headLookTargetWorldPosition = SanitizeFiniteInputFloat3(_headLookTargetWorldPosition - offset, _motionIntentHeadTarget);
            _strikeTargetWorldPosition = SanitizeFiniteInputFloat3(_strikeTargetWorldPosition - offset, _motionIntentHeadTarget);
            _gpuUploadDirty = true;
            _gpuBufferDataValid = false;
        }

        private bool TelemetryHasInvalidFrame()
        {
            if (!_telemetryRing.IsCreated || !_telemetryCursor.IsCreated || _telemetryRing.Length <= 0 || _telemetryCursor.Length <= 0)
                return false;

            int index = (_telemetryCursor[0] - 1) % _telemetryRing.Length;
            if (index < 0)
                index += _telemetryRing.Length;

            return (_telemetryRing[index].Flags & LeviathanTerrainIkConstants.TelemetryFlagInvalid) != 0u;
        }

        private void AdvanceFrameIndex()
        {
            _frameIndex = _frameIndex == int.MaxValue ? 0 : _frameIndex + 1;
        }

        private void DumpTelemetryBlackBoxOnce()
        {
            if (_telemetryDumped || !_telemetryRing.IsCreated)
                return;

            DumpTelemetryBlackBox();
            _telemetryDumped = true;
        }

        private void DumpTelemetryBlackBox()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dumpPath = Path.Combine(projectRoot, TelemetryDumpRelativePath);
            string directory = Path.GetDirectoryName(dumpPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            int cursor = _telemetryCursor.IsCreated && _telemetryCursor.Length > 0 ? _telemetryCursor[0] : 0;
            using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(TelemetryDumpMagic);
            int ringLength = _telemetryRing.IsCreated ? math.min(LeviathanTerrainIkConstants.TelemetryCapacity, _telemetryRing.Length) : 0;
            int entryCount = cursor >= ringLength ? ringLength : math.max(0, cursor);
            int firstEntryIndex = entryCount == ringLength && ringLength > 0 ? cursor % ringLength : 0;
            writer.Write(entryCount);
            writer.Write(cursor);
            writer.Write(TelemetryEntryPayloadBytes);
            for (int i = 0; i < entryCount; i++)
            {
                int sourceIndex = (firstEntryIndex + i) % ringLength;
                LeviathanTerrainIkTelemetryEntry entry = _telemetryRing[sourceIndex];
                writer.Write(entry.FrameIndex);
                writer.Write(entry.ActiveSegmentCount);
                writer.Write(entry.Flags);
                writer.Write(entry.StateHash);
                writer.Write(entry.HeadPosition.x);
                writer.Write(entry.HeadPosition.y);
                writer.Write(entry.HeadPosition.z);
                writer.Write(entry.TailPosition.x);
                writer.Write(entry.TailPosition.y);
                writer.Write(entry.TailPosition.z);
                writer.Write(entry.IntendedVelocity.x);
                writer.Write(entry.IntendedVelocity.y);
                writer.Write(entry.IntendedVelocity.z);
                writer.Write(entry.MaxTerrainPushMeters);
                writer.Write(entry.TailWhipSecondsRemaining);
                writer.Write(entry.Padding0);
                writer.Write(entry.Padding1);
                for (int paddingIndex = 0; paddingIndex < TelemetryEntryPaddingFloatCount; paddingIndex++)
                    writer.Write(0f);
            }
        }

        private void DumpBiteTelemetryBlackBoxOnce()
        {
            if (_biteTelemetryDumped ||
                !TryResolveBiteIkVaultBuffers(
                    out _,
                    out _,
                    out NativeArray<BiteIkSolveEvent> biteIkSolveEvents,
                    out _) ||
                !biteIkSolveEvents.IsCreated)
                return;

            DumpBiteTelemetryBlackBox();
            _biteTelemetryDumped = true;
        }

        private void DumpBiteTelemetryBlackBox()
        {
            TryResolveBiteIkVaultBuffers(
                out _,
                out _,
                out NativeArray<BiteIkSolveEvent> biteIkSolveEvents,
                out NativeArray<int> biteIkTelemetryCursor);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dumpPath = Path.Combine(projectRoot, BiteTelemetryDumpRelativePath);
            string directory = Path.GetDirectoryName(dumpPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            int cursor = biteIkTelemetryCursor.IsCreated && biteIkTelemetryCursor.Length > 0 ? biteIkTelemetryCursor[0] : 0;
            using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(BiteTelemetryDumpMagic);
            int ringLength = biteIkSolveEvents.IsCreated ? math.min(ProceduralBiteIkConstants.TelemetryCapacity, biteIkSolveEvents.Length) : 0;
            int entryCount = cursor >= ringLength ? ringLength : math.max(0, cursor);
            int firstEntryIndex = entryCount == ringLength && ringLength > 0 ? cursor % ringLength : 0;
            writer.Write(entryCount);
            writer.Write(cursor);
            writer.Write(BiteTelemetryEntryPayloadBytes);
            for (int i = 0; i < entryCount; i++)
            {
                int sourceIndex = (firstEntryIndex + i) % ringLength;
                BiteIkSolveEvent entry = biteIkSolveEvents[sourceIndex];
                writer.Write(entry.FrameIndex);
                writer.Write(entry.Flags);
                writer.Write(entry.StateHash);
                writer.Write(entry.TargetHash);
                writer.Write(entry.JawTipPosition.x);
                writer.Write(entry.JawTipPosition.y);
                writer.Write(entry.JawTipPosition.z);
                writer.Write(entry.DistanceMeters);
                writer.Write(entry.ClosestPoint.x);
                writer.Write(entry.ClosestPoint.y);
                writer.Write(entry.ClosestPoint.z);
                writer.Write(entry.Reach01);
                writer.Write(entry.TargetLocalCenter.x);
                writer.Write(entry.TargetLocalCenter.y);
                writer.Write(entry.TargetLocalCenter.z);
                writer.Write(entry.SystemStress01);
                writer.Write(entry.HeadPosition.x);
                writer.Write(entry.HeadPosition.y);
                writer.Write(entry.HeadPosition.z);
                writer.Write(entry.ContactDistanceMeters);
                writer.Write(entry.WrapAnchor0.x);
                writer.Write(entry.WrapAnchor0.y);
                writer.Write(entry.WrapAnchor0.z);
                writer.Write(entry.Blend01);
                writer.Write(entry.WrapAnchor1.x);
                writer.Write(entry.WrapAnchor1.y);
                writer.Write(entry.WrapAnchor1.z);
                writer.Write(entry.Padding0);
                writer.Write(entry.Padding1.x);
                writer.Write(entry.Padding1.y);
                writer.Write(entry.Padding1.z);
                writer.Write(entry.Padding1.w);
            }
        }

        private void ResetConstraintIterationHysteresis()
        {
            _qualityTier = GlobalRegistry.ScalabilityTier;
            bool lowTier = IsLowTier(_qualityTier);
            _activeSegmentCount = lowTier
                ? LowTierSegments
                : math.clamp(_highTierSegmentCount, LowTierSegments, MaxSegments);
            _resolvedConstraintIterations = lowTier ? 1 : math.clamp(_highTierConstraintIterations, 1, 4);
            _pendingConstraintIterations = _resolvedConstraintIterations;
            _constraintIterationSwitchTimer = 0f;
        }

        private float3 ResolveOwnerRuntimePosition()
        {
            if (_body != null)
                return SanitizeFiniteInputFloat3((float3)_body.position, float3.zero);

            return _cachedTransform != null
                ? SanitizeFiniteInputFloat3((float3)_cachedTransform.position, float3.zero)
                : float3.zero;
        }

        private float3 ResolveOwnerForward()
        {
            float3 forward = _cachedTransform != null ? (float3)_cachedTransform.forward : new float3(0f, 0f, 1f);
            return NormalizeSafe(forward, new float3(0f, 0f, 1f));
        }

        private float3 ResolveOwnerRight()
        {
            float3 right = _cachedTransform != null ? (float3)_cachedTransform.right : new float3(1f, 0f, 0f);
            return NormalizeSafe(right, new float3(1f, 0f, 0f));
        }

        private float ResolveBodySpeed()
        {
            if (_body == null)
                return 0f;

            Vector3 velocity = _body.linearVelocity;
            float velocitySq = velocity.sqrMagnitude;
            return velocitySq > MinVectorMagnitudeSq ? velocitySq * math.rsqrt(velocitySq) : 0f;
        }

        private uint ResolveRuntimeFlags(HectonQualityTier tier)
        {
            uint flags = 0u;
            if (_enableSdfHugging)
                flags |= LeviathanTerrainIkConstants.RuntimeFlagSdfHugging;
            if (_enableMapMagicFallback)
                flags |= LeviathanTerrainIkConstants.RuntimeFlagTerrainFallback;
            if (IsLowTier(tier))
                flags |= LeviathanTerrainIkConstants.RuntimeFlagLowTier;
            return flags;
        }

        private static bool IsLowTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.Unknown || tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350;
        }

        private static float3 SanitizeFiniteInputFloat3(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        private static float SanitizePositiveFinite(float value, float fallback, float minValue)
        {
            return math.isfinite(value) ? math.max(value, minValue) : fallback;
        }

        private float ResolveSafeTailWhipSecondsRemaining()
        {
            float rawTailWhipSecondsRemaining = _tailWhipSecondsRemaining;
            float safeTailWhipSecondsRemaining = SanitizePositiveFinite(rawTailWhipSecondsRemaining, 0f, 0f);
            _tailWhipSecondsRemaining = safeTailWhipSecondsRemaining;
            if (!math.isfinite(rawTailWhipSecondsRemaining))
                DumpTelemetryBlackBoxOnce();
            return safeTailWhipSecondsRemaining;
        }

        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= MinVectorMagnitudeSq)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
