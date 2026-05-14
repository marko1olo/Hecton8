using System;
using System.IO;
using Hecton8.Animation.IK;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Memory;
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
        private const ulong TelemetryDumpMagic = 0x4C455649494B3031UL;
        private const int TelemetryEntryPayloadBytes = 96;
        private const int TelemetryEntryPaddingFloatCount = 7;
        private const float ConstraintIterationHysteresisSeconds = 2.5f;
        private const int MaxSegments = LeviathanTerrainIkConstants.MaxSegments;
        private const int LowTierSegments = LeviathanTerrainIkConstants.LowTierSegments;
        private const float MinVectorMagnitudeSq = 0.0001f;

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
        private bool _pendingOriginShiftRebase;
        private bool _gpuUploadDirty;
        private bool _strikeActive;
        private bool _wasStrikeActiveLastTick;
        private bool _headLookTargetActive;
        private bool _globalGpuSkinningPublished;
        private int _frameIndex;
        private int _activeSegmentCount = MaxSegments;
        private int _resolvedConstraintIterations = 1;
        private int _pendingConstraintIterations = 1;
        private int _motionIntentFrame = -1;
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
            buffer = _gpuUploadBufferIndex == 0 ? _bonesGraphicsBufferB : _bonesGraphicsBufferA;
            return HasValidGraphicsBuffer(buffer, activeSegmentCount);
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
            JobHandle dependency = _solverScheduled ? _pendingHandle : default;
            ClearGpuSkinningBinding();
            DisposePersistentBuffers(dependency);
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

            if (_tailWhipSecondsRemaining > 0f)
                _tailWhipSecondsRemaining = math.max(0f, _tailWhipSecondsRemaining - safeDeltaTime);

            uint runtimeFlags = ResolveRuntimeFlags(qualityTier);
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
                TailWhipSecondsRemaining = _tailWhipSecondsRemaining,
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

            _pendingHandle = job.Schedule();
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
            _frameIndex = _frameIndex == int.MaxValue ? 0 : _frameIndex + 1;
            ApplyPendingOriginShiftRebase();

            if (TelemetryHasInvalidFrame())
                DumpTelemetryBlackBoxOnce();

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
                _frameIndex = _frameIndex == int.MaxValue ? 0 : _frameIndex + 1;
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
        }

        internal void BindSkinningMaterial(Material material)
        {
            if (_skinningMaterial == material)
                return;

            ClearMaterialGpuSkinningBinding(_skinningMaterial);
            _skinningMaterial = material;
            ClearMaterialGpuSkinningBinding(_skinningMaterial);
            _gpuUploadDirty = material != null || _publishGlobalBoneBuffer;
        }

        internal void SetMotionIntent(Vector3 intendedVelocity, Vector3 headTargetWorldPosition)
        {
            _motionIntentVelocity = SanitizeFiniteInputFloat3((float3)intendedVelocity, float3.zero);
            _motionIntentHeadTarget = SanitizeFiniteInputFloat3((float3)headTargetWorldPosition, ResolveOwnerRuntimePosition());
            _motionIntentFrame = Time.frameCount;
        }

        internal void SetStrikeIntent(Transform target, Vector3 targetWorldPosition, bool strikeActive)
        {
            _strikeActive = strikeActive && target != null;
            if (!_strikeActive)
            {
                _strikeTarget = null;
                _strikeTargetRigidbody = null;
                _wasStrikeActiveLastTick = false;
                return;
            }

            if (_strikeTarget != target)
            {
                _strikeTarget = target;
                _strikeTargetRigidbody = null;
                target.TryGetComponent(out _strikeTargetRigidbody);
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
                return;
            }

            JobHandle dependency = _solverScheduled ? _pendingHandle : default;
            DisposePersistentBuffers(dependency);
            _segmentPositions = H8Memory.Allocate<float3>(MaxSegments, SystemID.External, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[20] - leviathan spine positions - owner: FaunaKinematicsRuntime
            _previousSegmentPositions = H8Memory.Allocate<float3>(MaxSegments, SystemID.External, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[20] - leviathan previous spine positions - owner: FaunaKinematicsRuntime
            _leviathanBones = H8Memory.Allocate<float4x4>(MaxSegments, SystemID.External, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float4x4>[20] - GPU bone matrix SOA - owner: FaunaKinematicsRuntime
            _telemetryRing = H8Memory.Allocate<LeviathanTerrainIkTelemetryEntry>(LeviathanTerrainIkConstants.TelemetryCapacity, SystemID.External, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<LeviathanTerrainIkTelemetryEntry>[300] - black box circular buffer - owner: FaunaKinematicsRuntime
            _telemetryCursor = H8Memory.Allocate<int>(1, SystemID.External, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[1] - black box write cursor - owner: FaunaKinematicsRuntime
        }

        private void DisposePersistentBuffers(JobHandle dependency)
        {
            JobHandle releaseDependency = JobHandle.CombineDependencies(_disposeHandle, dependency);
            releaseDependency = H8Memory.Release(ref _segmentPositions, releaseDependency, SystemID.External);
            releaseDependency = H8Memory.Release(ref _previousSegmentPositions, releaseDependency, SystemID.External);
            releaseDependency = H8Memory.Release(ref _leviathanBones, releaseDependency, SystemID.External);
            releaseDependency = H8Memory.Release(ref _telemetryRing, releaseDependency, SystemID.External);
            releaseDependency = H8Memory.Release(ref _telemetryCursor, releaseDependency, SystemID.External);
            _disposeHandle = releaseDependency;
            DispatcherJobSwap.TryFinalizeCompleted(ref _disposeHandle);
            _pendingHandle = default;
            _solverScheduled = false;
            _gpuUploadDirty = false;
        }

        private void CompleteScheduledSolverForLifecycle()
        {
            if (!_solverScheduled)
                return;

            DispatcherJobSwap.TryComplete(ref _pendingHandle, forceComplete: true);
            _solverScheduled = false;
            if (TelemetryHasInvalidFrame())
                DumpTelemetryBlackBoxOnce();
        }

        private void SeedSpineFromOwner()
        {
            if (!_segmentPositions.IsCreated || !_previousSegmentPositions.IsCreated || !_leviathanBones.IsCreated)
                return;

            float3 origin = ResolveOwnerRuntimePosition();
            float3 forward = ResolveOwnerForward();
            float segmentLength = SanitizePositiveFinite(_segmentLength, 2.5f, 0.05f);
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
            _motionIntentFrame = -1;
            _gpuUploadDirty = true;
        }

        private void CaptureFallbackMotionIntent()
        {
            float3 ownerPosition = ResolveOwnerRuntimePosition();
            float3 ownerForward = ResolveOwnerForward();
            if (_motionIntentFrame == Time.frameCount)
                return;

            float3 velocity = _body != null
                ? SanitizeFiniteInputFloat3((float3)_body.linearVelocity, float3.zero)
                : float3.zero;
            float bodySpeed = ResolveBodySpeed();
            if (math.lengthsq(velocity) <= MinVectorMagnitudeSq)
                velocity = ownerForward * math.max(0.1f, bodySpeed);

            _motionIntentVelocity = velocity;
            float segmentLength = SanitizePositiveFinite(_segmentLength, 2.5f, 0.05f);
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
            terrainOrigin = (float3)payload.TerrainPosition;
            terrainSize = (float3)payload.TerrainSize;
            terrainResolution = payload.HeightmapResolution;
        }

        private bool UploadBonesToGpu()
        {
            if (!_leviathanBones.IsCreated)
                return false;

            if (!_publishGlobalBoneBuffer && _globalGpuSkinningPublished)
                ClearGlobalGpuSkinningBinding();

            if (_skinningMaterial == null && !_publishGlobalBoneBuffer)
                return true;

            EnsureGraphicsBuffers();
            GraphicsBuffer writeBuffer = _gpuUploadBufferIndex == 0 ? _bonesGraphicsBufferA : _bonesGraphicsBufferB;
            if (!HasValidGraphicsBuffer(writeBuffer, MaxSegments))
                return false;

            GraphicsBufferUploadUtility.UploadNativeArray(writeBuffer, _leviathanBones, MaxSegments);
            float ikTier = IsLowTier(_qualityTier) ? 0f : 1f;
            float safeSegmentLength = SanitizePositiveFinite(_segmentLength, 2.5f, 0.05f);
            float safeTailWhipDuration = SanitizePositiveFinite(_tailWhipDurationSeconds, 1f, 0.0001f);
            float tailWhip01 = math.saturate(_tailWhipSecondsRemaining * math.rcp(safeTailWhipDuration));
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
            return true;
        }

        private void ClearGpuSkinningBinding()
        {
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
            material.SetFloat(_LeviathanSegmentLengthId, 1f);
            material.SetFloat(_LeviathanGpuSkinningId, 0f);
        }

        private void ClearGlobalGpuSkinningBinding()
        {
            Shader.SetGlobalFloat(_LeviathanBoneCountId, 0f);
            Shader.SetGlobalFloat(_LeviathanIkTierId, 0f);
            Shader.SetGlobalFloat(_LeviathanTailWhipId, 0f);
            Shader.SetGlobalFloat(_LeviathanSegmentLengthId, 1f);
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

        private void ResetConstraintIterationHysteresis()
        {
            _qualityTier = GlobalRegistry.ScalabilityTier;
            _resolvedConstraintIterations = IsLowTier(_qualityTier) ? 1 : math.clamp(_highTierConstraintIterations, 1, 4);
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
