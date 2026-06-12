using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Animation.Fauna;
using Hecton8.Animation.IK;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Rendering;

namespace Hecton8.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FaunaBrain))]
    internal sealed class FaunaKinematicsRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IOriginShiftListener, IDisposable, ILeviathanProceduralTunerSource, IGlobalRegistryHotSwapListener
    {
        private int _signalPushDropCount;
        private const string TelemetryDumpRelativePath = "Docs/AgentLogs/Dump_1702.bin";
        private const string BiteTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_1702.bin";
        private const string TelemetryDumpPayloadLabel = "faunaKinematicsTelemetryDumpPayload";
        private const string BiteTelemetryDumpPayloadLabel = "faunaKinematicsBiteTelemetryDumpPayload";
        private const ulong TelemetryDumpMagic = 0x4C455649494B3031UL;
        private const ulong BiteTelemetryDumpMagic = 0x4642494B30303031UL;
        private const int TelemetryDumpHeaderBytes = 20;
        private const int TelemetryEntryPayloadBytes = 96;
        private const int BiteTelemetryEntryPayloadBytes = 128;
        private const float ConstraintIterationHysteresisSeconds = 2.5f;
        private const int MaxSegments = LeviathanTerrainIkConstants.MaxSegments;
        private const int MinimumQualitySegments = LeviathanTerrainIkConstants.MinimumQualitySegments;
        private const float MinVectorMagnitudeSq = 0.0001f;
        private const float OriginShiftUsableMagnitudeSq = 0.000001f;
        private const float BiteFeedbackCooldownSeconds = 0.18f;
        private const float BiteAudioCooldownSeconds = 0.24f;
        private const ushort BiteMinimumQualityDebrisQuantity = 4;
        private const ushort BiteMiddleQualityDebrisQuantity = 32;
        private const ushort BiteMaximumQualityDebrisQuantity = 512;
        private const ushort BiteOverkillQualityDebrisQuantity = 2048;
        private const float BiteHullDentMinimumRadiusMeters = 0.35f;
        private const float BiteHullDentMaximumRadiusMeters = 1.35f;
        private const float BiteHullDentMinimumDepthMeters = 0.035f;
        private const float BiteHullDentMaximumDepthMeters = 0.28f;
        private const uint LeviathanRigMagicH8lr = 0x524C3848u; // H8LR
        private const uint LeviathanRigMagicLvrg = 0x4752564Cu; // LVRG
        private const BufferID TerrainSdfSnapshotBuffer = BufferID.FaunaKinematicsRuntime_TerrainSdfSnapshotBuffer;
        private static readonly ulong TerrainSdfSnapshotMutationGuardMask =
            FaunaVaultMutationGuardBit(TerrainSdfSnapshotBuffer);
        private const int LeviathanRigHeaderBytes = 16;
        private const int LeviathanRigRowBytes = 16;
        private const int LeviathanIkGlobalsBytes = 32;
        private const uint BiteSparksSignalHash = 0x42505453u; // BPTS

        private static readonly int _LeviathanBonesId = Shader.PropertyToID("_H8LeviathanBones");
        private static readonly int _LeviathanIkGlobalsId = Shader.PropertyToID("_H8LeviathanIkGlobals");
        private const int LeviathanIkGlobalsScalars0Offset = 0;
        private const int LeviathanIkGlobalsScalars1Offset = 16;

        [Header("Spine")]
        [Tooltip("Maximum-quality spine segment count. Minimum quality resolves to eight segments.")]
        [FormerlySerializedAs("_highTierSegmentCount")]
        [SerializeField, Range(MinimumQualitySegments, MaxSegments)] private int _maximumQualitySegmentCount = MaxSegments;

        [Tooltip("Meters between consecutive procedural spine segments.")]
        [SerializeField, Range(0.25f, 8f)] private float _segmentLength = 2.5f;

        [Tooltip("Visual radius written into each procedural bone matrix.")]
        [SerializeField, Range(0.05f, 6f)] private float _bodyRadius = 1.15f;

        [Tooltip("Procedural swim wave frequency in cycles per second.")]
        [SerializeField, Range(0.05f, 3f)] private float _swimWaveFrequencyHz = 0.55f;

        [Tooltip("Base lateral swim wave amplitude in meters before velocity and quality scaling.")]
        [SerializeField, Range(0f, 6f)] private float _swimWaveAmplitudeMeters = 1.1f;

        [Tooltip("FABRIK convergence tolerance exposed for rigger tuning and named job tests.")]
        [SerializeField, Range(0.001f, 0.5f)] private float _fabrikToleranceMeters = 0.025f;

        [Tooltip("Follower damping for the Verlet tail cache.")]
        [SerializeField, Range(0f, 1f)] private float _verletDamping = 0.87f;

        [Tooltip("Maximum constraint iterations at GlobalQualityWeight=1.0. Thermal collapse lerps this to one.")]
        [FormerlySerializedAs("_highTierConstraintIterations")]
        [SerializeField, Range(1, 10)] private int _maximumQualityConstraintIterations = 10;

        [Tooltip("Optional H8LR rig rows generated by the offline fauna rigger. Parsed once during cold vault hydration.")]
        [SerializeField] private TextAsset _generatedRigDefinitionBinary;

        [Header("Terrain Hugging")]
        [Tooltip("Meters added above SDF or heightmap contact to prevent visual z-fighting.")]
        [SerializeField, Range(0f, 2f)] private float _terrainClearance = 0.35f;

        [Tooltip("Enable SDF terrain pushout; continuous quality scales the influence weight.")]
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

        [Tooltip("Visual mandible opening offset in meters once continuous quality clears the authored minimum threshold.")]
        [SerializeField, Range(0f, 4f)] private float _biteJawOpenMeters = 0.8f;

        [Tooltip("Bounds padding used when deciding whether teeth have scraped the target hull.")]
        [SerializeField, Range(0f, 1f)] private float _biteContactPaddingMeters = 0.08f;

        private Rigidbody _body;
        private Transform _cachedTransform;
        private IDataVault _dataVault;
        private ITerrainHeightSampleReadModel _terrainHeightSamples;
        private FaunaBrain _faunaBrain;

        private VaultGenerationHandle<float3> _segmentPositionsHandle;
        private VaultGenerationHandle<float3> _previousSegmentPositionsHandle;
        private VaultGenerationHandle<LeviathanBoneDTO> _leviathanBonesHandle;
        private VaultGenerationHandle<LeviathanBoneConstraintsDTO> _boneConstraintsHandle;
        private VaultGenerationHandle<LeviathanCapsuleColliderDTO> _colliderProxiesHandle;
        private VaultGenerationHandle<byte> _rigCsvScratchHandle;
        private VaultGenerationHandle<LeviathanTerrainIkTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<JawIkTarget> _jawIkTargetsHandle;
        private VaultGenerationHandle<CurrentJawPos> _currentJawPosHandle;
        private VaultGenerationHandle<BiteIkSolveEvent> _biteIkSolveEventsHandle;
        private VaultGenerationHandle<int> _biteIkTelemetryCursorHandle;
        private VaultGenerationHandle<byte> _terrainSdfSnapshotHandle;
        private IDataVault _terrainSdfSnapshotGuardVault;

        private GraphicsBuffer _bonesGraphicsBufferA;
        private GraphicsBuffer _bonesGraphicsBufferB;
        private GraphicsBuffer _ikGlobalsBufferA;
        private GraphicsBuffer _ikGlobalsBufferB;
        private GraphicsBuffer _activeIkGlobalsBuffer;
        private int _gpuUploadBufferIndex;
        private int _ikGlobalsUploadBufferIndex;

        private JobHandle _pendingHandle;
        private JobHandle _disposeHandle;
        private long _solverScheduleTimestamp;
        private float _lastBurstSolveMicros;
        private bool _solverScheduled;
        private bool _terrainSdfSnapshotGuardHeld;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredOriginShiftListener;
        private bool _registeredHotSwapListener;
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
        private bool _supportsConstantBufferBinding;
        private int _frameIndex;
        private int _lastBiteFeedbackFrame = -1;
        private int _lastBiteAudioFrame = -1;
        private int _activeSegmentCount = MinimumQualitySegments;
        private int _resolvedConstraintIterations = 1;
        private int _pendingConstraintIterations = 1;
        private bool _motionIntentPending;
        private float _constraintIterationSwitchTimer;
        private float _tailWhipSecondsRemaining;
        private float _attackTelegraphBlend;
        private float _globalQualityWeight = 1f;
#if UNITY_EDITOR
        private bool _editorQualityOverrideActive;
        private float _editorQualityOverrideWeight = 1f;
#endif
        private float3 _pendingOriginShiftOffset;
        private float3 _motionIntentVelocity;
        private float3 _motionIntentHeadTarget;
        private float3 _headLookTargetWorldPosition;
        private float3 _strikeTargetWorldPosition;
        private Transform _strikeTarget;

        [StructLayout(LayoutKind.Explicit, Size = LeviathanIkGlobalsBytes)]
        private struct LeviathanIkShaderGlobalsDTO
        {
            [FieldOffset(0)] public float4 Scalars0;
            [FieldOffset(16)] public float4 Scalars1;
        }

        private static bool ValidateLeviathanIkShaderGlobalsLayout()
        {
            return UnsafeUtility.SizeOf<LeviathanIkShaderGlobalsDTO>() == LeviathanIkGlobalsBytes &&
                   LeviathanIkGlobalsScalars0Offset == 0 &&
                   LeviathanIkGlobalsScalars1Offset == 16;
        }

        internal bool TryGetLeviathanBones(out NativeArray<LeviathanBoneDTO>.ReadOnly bones, out int activeSegmentCount)
        {
            if (_disposed ||
                _solverScheduled ||
                _activeSegmentCount <= 0 ||
                !TryResolveSpineVaultBuffers(
                    out _,
                    out _,
                    out NativeArray<LeviathanBoneDTO> leviathanBones,
                    out _,
                    out _))
            {
                bones = default;
                activeSegmentCount = 0;
                return false;
            }

            activeSegmentCount = math.min(_activeSegmentCount, leviathanBones.Length);
            bones = leviathanBones.AsReadOnly();
            return activeSegmentCount > 0;
        }

        public void GetLeviathanProceduralTunerSnapshot(out LeviathanProceduralTunerSnapshot snapshot)
        {
            snapshot = new LeviathanProceduralTunerSnapshot
            {
                ActiveSegmentCount = _activeSegmentCount,
                ConstraintIterations = _resolvedConstraintIterations,
                BurstSolveMicros = _lastBurstSolveMicros,
                GlobalQualityWeight = _globalQualityWeight
            };
        }

#if UNITY_EDITOR
        public void ApplyLeviathanProceduralEditorTuning(float sineWaveAmplitudeMeters, float sineWaveSpeed, int maxFabrikIterations, float globalQualityWeight)
        {
            _swimWaveAmplitudeMeters = math.clamp(SanitizePositiveFinite(sineWaveAmplitudeMeters, _swimWaveAmplitudeMeters, 0f), 0f, 6f);
            _swimWaveFrequencyHz = math.clamp(SanitizePositiveFinite(sineWaveSpeed, _swimWaveFrequencyHz, 0.01f), 0.05f, 3f);
            _maximumQualityConstraintIterations = math.clamp(maxFabrikIterations, 1, 10);
            _editorQualityOverrideWeight = SanitizeQualityWeight01(globalQualityWeight);
            _editorQualityOverrideActive = true;
            ResetConstraintIterationHysteresis();
            _gpuUploadDirty = true;
        }

        public void ClearLeviathanProceduralEditorQualityOverride()
        {
            _editorQualityOverrideActive = false;
            ResetConstraintIterationHysteresis();
        }

        public bool TryGetLeviathanProceduralTelemetryForEditor(
            out int activeSegmentCount,
            out int constraintIterations,
            out float burstSolveMicros,
            out float globalQualityWeight,
            out uint flags)
        {
            activeSegmentCount = _activeSegmentCount;
            constraintIterations = _resolvedConstraintIterations;
            burstSolveMicros = _lastBurstSolveMicros;
            globalQualityWeight = _globalQualityWeight;
            flags = 0u;

            if (!TryResolveSpineVaultBuffers(
                    out _,
                    out _,
                    out _,
                    out NativeArray<LeviathanTerrainIkTelemetryEntry> telemetryRing,
                    out NativeArray<int> telemetryCursor) ||
                !telemetryRing.IsCreated ||
                !telemetryCursor.IsCreated ||
                telemetryRing.Length <= 0 ||
                telemetryCursor.Length <= 0)
            {
                return false;
            }

            int cursor = telemetryCursor[0];
            if (cursor == 0)
                return false;

            int index = (cursor - 1) % telemetryRing.Length;
            if (index < 0)
                index += telemetryRing.Length;

            LeviathanTerrainIkTelemetryEntry entry = telemetryRing[index];
            activeSegmentCount = entry.ActiveSegmentCount;
            constraintIterations = math.clamp((int)math.round(entry.AverageFabrikIterations), 1, _maximumQualityConstraintIterations);
            burstSolveMicros = SanitizePositiveFinite(entry.BurstSolveMicros, _lastBurstSolveMicros, 0f);
            globalQualityWeight = SanitizeQualityWeight01(entry.GlobalQualityWeight);
            flags = entry.Flags;
            return true;
        }
#endif

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
            RefreshGraphicsCapabilitySnapshotCold();
            RefreshColdDependencies();
            EnsurePersistentBuffers();
            HydrateRigDefinitionsOrMockCold();
            EnsureVisualGpuBuffersCold();
        }

        private void OnEnable()
        {
            if (_disposed || !Application.isPlaying)
                return;

            CompleteScheduledSolverForLifecycle();
            RefreshGraphicsCapabilitySnapshotCold();
            RefreshColdDependencies();
            EnsurePersistentBuffers();
            HydrateRigDefinitionsOrMockCold();
            EnsureVisualGpuBuffersCold();
            ResetConstraintIterationHysteresis();
            TryRegister();
            TryRegisterHotSwapListener();
            TryRegisterOriginShiftListener();
        }

        private void OnDisable()
        {
            if (_disposed || !Application.isPlaying)
                return;

            TryUnregisterOriginShiftListener();
            TryUnregisterHotSwapListener();
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
            TryUnregisterHotSwapListener();
            TryUnregister();
            ClearGpuSkinningBinding();
            DisposePersistentBuffers();
            ReleaseGraphicsBuffers();
        }

        public void Tick(float deltaTime)
        {
            if (_disposed ||
                _solverScheduled ||
                !math.isfinite(deltaTime) ||
                deltaTime <= 0f)
            {
                return;
            }

            if (!TryResolveSpineVaultBuffers(
                    out NativeArray<float3> segmentPositions,
                    out NativeArray<float3> previousSegmentPositions,
                    out NativeArray<LeviathanBoneDTO> leviathanBones,
                    out NativeArray<LeviathanTerrainIkTelemetryEntry> telemetryRing,
                    out NativeArray<int> telemetryCursor))
            {
                return;
            }

            float qualityWeight = ResolveGlobalQualityWeight();
            float safeDeltaTime = math.min(math.max(0f, deltaTime), 0.05f);
            _globalQualityWeight = qualityWeight;
            ApplyPendingOriginShiftRebase();
            RefreshQualityState(safeDeltaTime, qualityWeight);
            CaptureFallbackMotionIntent();
            ApplyIntentTargetOverrides();
            ResolveTerrainPayload(
                qualityWeight,
                out NativeArray<byte>.ReadOnly sdfTexture3D,
                out int3 sdfDimensions,
                out float3 sdfOrigin,
                out float3 sdfCellSize,
                out float sdfRange,
                out NativeArray<ushort> heightSamples,
                out float3 terrainOrigin,
                out float3 terrainSize,
                out int terrainResolution,
                out bool terrainSdfSnapshotLocked);

            float safeTailWhipSecondsRemaining = ResolveSafeTailWhipSecondsRemaining();
            if (safeTailWhipSecondsRemaining > 0f)
            {
                safeTailWhipSecondsRemaining = math.max(0f, safeTailWhipSecondsRemaining - safeDeltaTime);
                _tailWhipSecondsRemaining = safeTailWhipSecondsRemaining;
            }

            ConsumeStrikeSignals();
            AbsoluteUniversePosition ownerAup = ResolveOwnerAup();
            double3 ownerAupDouble = ownerAup.ToAbsoluteDouble3();
            float3 ownerRuntimePosition = ResolveOwnerRuntimePosition();
            double3 headTargetAup = ownerAupDouble;
            uint runtimeFlags = ResolveRuntimeFlags();
            if (TryResolveAupFromRuntimeOrigin(_motionIntentHeadTarget, out AbsoluteUniversePosition resolvedHeadTargetAup))
            {
                headTargetAup = resolvedHeadTargetAup.ToAbsoluteDouble3();
                runtimeFlags |= LeviathanTerrainIkConstants.RuntimeFlagHeadTargetAup;
            }

            TryResolveProceduralAuxVaultBuffers(
                out NativeArray<LeviathanBoneConstraintsDTO> boneConstraints,
                out NativeArray<LeviathanCapsuleColliderDTO> colliderProxies,
                out _);
            bool biteTargetReady = PrepareBiteTarget(
                out NativeArray<JawIkTarget> jawIkTargets,
                out NativeArray<CurrentJawPos> currentJawPos,
                out NativeArray<BiteIkSolveEvent> biteIkSolveEvents,
                out NativeArray<int> biteIkTelemetryCursor);
            LeviathanTerrainIkJob job = new LeviathanTerrainIkJob
            {
                SegmentPositions = segmentPositions,
                PreviousSegmentPositions = previousSegmentPositions,
                LeviathanBones = leviathanBones,
                BoneConstraints = boneConstraints,
                ColliderProxies = colliderProxies,
                TelemetryRing = telemetryRing,
                TelemetryCursor = telemetryCursor,
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
                SwimWaveFrequencyHz = _swimWaveFrequencyHz,
                SwimWaveAmplitudeMeters = _swimWaveAmplitudeMeters,
                FabrikToleranceMeters = _fabrikToleranceMeters,
                TerrainClearance = _terrainClearance,
                TailWhipSecondsRemaining = safeTailWhipSecondsRemaining,
                TailWhipDurationSeconds = _tailWhipDurationSeconds,
                TailWhipAmplitudeMeters = _tailWhipAmplitudeMeters,
                GlobalQualityWeight = qualityWeight,
                HeadTargetPosition = _motionIntentHeadTarget,
                IntendedVelocity = _motionIntentVelocity,
                OwnerForward = ResolveOwnerForward(),
                WorldUp = new float3(0f, 1f, 0f),
                RequestedSegmentCount = _activeSegmentCount,
                ConstraintIterations = _resolvedConstraintIterations,
                FrameIndex = _frameIndex,
                BurstSolveMicros = _lastBurstSolveMicros,
                RootAup = ownerAupDouble,
                HeadTargetAup = headTargetAup,
                RootRuntimePosition = ownerRuntimePosition,
                RuntimeFlags = runtimeFlags
            };

            bool terrainSdfSnapshotClaimed = false;
            JobHandle scheduledHandle = default;
            try
            {
                scheduledHandle = job.Schedule();
                if (terrainSdfSnapshotLocked)
                {
                    _terrainSdfSnapshotGuardHeld = true;
                    terrainSdfSnapshotClaimed = true;
                }

                _pendingHandle = scheduledHandle;
                _solverScheduled = true;
            }
            finally
            {
                if (!terrainSdfSnapshotClaimed)
                    UnlockTerrainSdfSnapshot(ref terrainSdfSnapshotLocked);
            }

            if (biteTargetReady)
            {
                ProceduralBiteJob biteJob = new ProceduralBiteJob
                {
                    JawIkTargets = jawIkTargets,
                    CurrentJawPos = currentJawPos,
                    LeviathanBones = leviathanBones,
                    BiteIkSolveEvents = biteIkSolveEvents,
                    TelemetryCursor = biteIkTelemetryCursor,
                    PredatorAup = ownerAup,
                    PredatorPosition = ownerRuntimePosition,
                    PredatorForward = ResolveOwnerForward(),
                    PredatorUp = new float3(0f, 1f, 0f),
                    PredatorRight = ResolveOwnerRight(),
                    DeltaTime = safeDeltaTime,
                    BodyRadius = _bodyRadius,
                    SegmentLength = _segmentLength,
                    JawReachMeters = _biteJawReachMeters,
                    JawOpenMeters = _biteJawOpenMeters,
                    SystemStress01 = 0f,
                    VisualOverkillWeight01 = ResolveBiteVisualOverkillWeight(_globalQualityWeight),
                    TargetIndex = 0,
                    FrameIndex = _frameIndex,
                    HeadBoneIndex = ProceduralBiteIkConstants.DefaultHeadBoneIndex,
                    UpperJawBoneIndex = ProceduralBiteIkConstants.DefaultUpperJawBoneIndex,
                    LowerJawBoneIndex = ProceduralBiteIkConstants.DefaultLowerJawBoneIndex,
                    FirstTentacleBoneIndex = ProceduralBiteIkConstants.DefaultFirstTentacleBoneIndex,
                    TentacleBoneCount = ProceduralBiteIkConstants.MaxTentacleBones,
                    RuntimeFlags = ResolveBiteRuntimeFlags()
                };
                scheduledHandle = biteJob.Schedule(scheduledHandle);
                _pendingHandle = scheduledHandle;
            }

            _solverScheduleTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
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
            ReleaseTerrainSdfSnapshotLock();
            CaptureCompletedSolverTelemetry();
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

            Vector3 shiftOffset = shiftData.ShiftOffset;
            float3 offset = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            if (!IsFiniteOriginShiftOffset(offset))
            {
                DumpTelemetryBlackBoxOnce();
                return;
            }

            if (!IsUsableOriginShiftOffset(offset))
                return;

            if (_solverScheduled)
            {
                if (!DispatcherJobSwap.TryFinalizeCompleted(ref _pendingHandle))
                {
                    QueueOriginShiftRebase(offset);
                    return;
                }

                _solverScheduled = false;
                ReleaseTerrainSdfSnapshotLock();
                CaptureCompletedSolverTelemetry();
                AdvanceFrameIndex();
                if (TelemetryHasInvalidFrame())
                    DumpTelemetryBlackBoxOnce();
            }

            ApplyOriginShiftRebase(offset);
        }

        internal void BindFromFauna(FaunaBrain faunaBrain, Rigidbody body)
        {
            _faunaBrain = faunaBrain;
            _body = body;
            _cachedTransform = faunaBrain != null ? faunaBrain.transform : transform;
            CompleteScheduledSolverForLifecycle();
            RefreshColdDependencies();
            EnsurePersistentBuffers();
            HydrateRigDefinitionsOrMockCold();
            ResetConstraintIterationHysteresis();
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
                _wasStrikeActiveLastTick = false;
                return;
            }

            _strikeTarget = target;

            _strikeTargetWorldPosition = SanitizeFiniteInputFloat3(
                (float3)targetWorldPosition,
                ResolveOwnerRuntimePosition());

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
            _terrainHeightSamples = GlobalRegistry.TerrainHeightSamples;
        }

        private void EnsurePersistentBuffers()
        {
            if (TryResolveSpineVaultBuffers(out _, out _, out _, out _, out _) &&
                TryResolveProceduralAuxVaultBuffers(out _, out _, out _) &&
                TryResolveBiteIkVaultBuffers(out _, out _, out _, out _))
            {
                return;
            }

            DisposePersistentBuffers();
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                ClearNativeBufferViews();
                return;
            }

            bool primaryReady = OpenOrAcquireVaultBuffer(vault, ref _segmentPositionsHandle, BufferID.LeviathanSegmentPositions, MaxSegments, NativeArrayOptions.UninitializedMemory, out _) &&
                                OpenOrAcquireVaultBuffer(vault, ref _previousSegmentPositionsHandle, BufferID.LeviathanPreviousSegmentPositions, MaxSegments, NativeArrayOptions.UninitializedMemory, out _) &&
                                OpenOrAcquireVaultBuffer(vault, ref _leviathanBonesHandle, BufferID.LeviathanBoneMatrices, MaxSegments, NativeArrayOptions.UninitializedMemory, out _) &&
                                OpenOrAcquireVaultBuffer(vault, ref _telemetryRingHandle, BufferID.LeviathanTerrainIkTelemetryRing, LeviathanTerrainIkConstants.TelemetryCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                                OpenOrAcquireVaultBuffer(vault, ref _telemetryCursorHandle, BufferID.LeviathanTerrainIkTelemetryCursor, 1, NativeArrayOptions.ClearMemory, out _);
            bool auxReady = OpenOrAcquireVaultBuffer(vault, ref _boneConstraintsHandle, BufferID.LeviathanProceduralBoneConstraints, MaxSegments, NativeArrayOptions.UninitializedMemory, out _) &&
                            OpenOrAcquireVaultBuffer(vault, ref _colliderProxiesHandle, BufferID.LeviathanCreatureColliderProxies, MaxSegments, NativeArrayOptions.UninitializedMemory, out _) &&
                            OpenOrAcquireVaultBuffer(vault, ref _rigCsvScratchHandle, BufferID.LeviathanRigCsvScratch, 4096, NativeArrayOptions.UninitializedMemory, out _);
            bool biteReady = EnsureBiteIkVaultHandles(vault);
            if (!primaryReady || !auxReady || !biteReady)
                ClearNativeBufferViews();
        }

        private bool TryResolveSpineVaultBuffers(
            out NativeArray<float3> segmentPositions,
            out NativeArray<float3> previousSegmentPositions,
            out NativeArray<LeviathanBoneDTO> leviathanBones,
            out NativeArray<LeviathanTerrainIkTelemetryEntry> telemetryRing,
            out NativeArray<int> telemetryCursor)
        {
            segmentPositions = default;
            previousSegmentPositions = default;
            leviathanBones = default;
            telemetryRing = default;
            telemetryCursor = default;

            IDataVault vault = _dataVault;
            if (vault == null)
            {
                return false;
            }

            return TryOpenVaultBuffer(vault, ref _segmentPositionsHandle, BufferID.LeviathanSegmentPositions, MaxSegments, out segmentPositions) &&
                   TryOpenVaultBuffer(vault, ref _previousSegmentPositionsHandle, BufferID.LeviathanPreviousSegmentPositions, MaxSegments, out previousSegmentPositions) &&
                   TryOpenVaultBuffer(vault, ref _leviathanBonesHandle, BufferID.LeviathanBoneMatrices, MaxSegments, out leviathanBones) &&
                   TryOpenVaultBuffer(vault, ref _telemetryRingHandle, BufferID.LeviathanTerrainIkTelemetryRing, LeviathanTerrainIkConstants.TelemetryCapacity, out telemetryRing) &&
                   TryOpenVaultBuffer(vault, ref _telemetryCursorHandle, BufferID.LeviathanTerrainIkTelemetryCursor, 1, out telemetryCursor);
        }

        private bool TryResolveProceduralAuxVaultBuffers(
            out NativeArray<LeviathanBoneConstraintsDTO> boneConstraints,
            out NativeArray<LeviathanCapsuleColliderDTO> colliderProxies,
            out NativeArray<byte> rigCsvScratch)
        {
            boneConstraints = default;
            colliderProxies = default;
            rigCsvScratch = default;

            IDataVault vault = _dataVault;
            if (vault == null)
            {
                return false;
            }

            return TryOpenVaultBuffer(vault, ref _boneConstraintsHandle, BufferID.LeviathanProceduralBoneConstraints, MaxSegments, out boneConstraints) &&
                   TryOpenVaultBuffer(vault, ref _colliderProxiesHandle, BufferID.LeviathanCreatureColliderProxies, MaxSegments, out colliderProxies) &&
                   TryOpenVaultBuffer(vault, ref _rigCsvScratchHandle, BufferID.LeviathanRigCsvScratch, 4096, out rigCsvScratch);
        }

        private bool TryResolveBiteIkVaultBuffers(
            out NativeArray<JawIkTarget> jawIkTargets,
            out NativeArray<CurrentJawPos> currentJawPos,
            out NativeArray<BiteIkSolveEvent> biteIkSolveEvents,
            out NativeArray<int> biteIkTelemetryCursor)
        {
            jawIkTargets = default;
            currentJawPos = default;
            biteIkSolveEvents = default;
            biteIkTelemetryCursor = default;

            IDataVault vault = _dataVault;
            if (vault == null)
            {
                _biteVaultReady = false;
                return false;
            }

            _biteVaultReady = TryOpenVaultBuffer(vault, ref _jawIkTargetsHandle, BufferID.JawIkTargets, ProceduralBiteIkConstants.TargetCapacity, out jawIkTargets) &&
                              TryOpenVaultBuffer(vault, ref _currentJawPosHandle, BufferID.CurrentJawPos, ProceduralBiteIkConstants.CurrentJawPoseCapacity, out currentJawPos) &&
                              TryOpenVaultBuffer(vault, ref _biteIkSolveEventsHandle, BufferID.BiteIkSolveEvents, ProceduralBiteIkConstants.TelemetryCapacity, out biteIkSolveEvents) &&
                              TryOpenVaultBuffer(vault, ref _biteIkTelemetryCursorHandle, BufferID.BiteIkTelemetryCursor, 1, out biteIkTelemetryCursor);
            return _biteVaultReady;
        }

        private bool TryResolveCurrentJawPosVaultBuffer(out NativeArray<CurrentJawPos> currentJawPos)
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                currentJawPos = default;
                return false;
            }

            return TryOpenVaultBuffer(vault, ref _currentJawPosHandle, BufferID.CurrentJawPos, ProceduralBiteIkConstants.CurrentJawPoseCapacity, out currentJawPos);
        }

        private bool TryResolveBiteTelemetryVaultBuffers(
            out NativeArray<BiteIkSolveEvent> biteIkSolveEvents,
            out NativeArray<int> biteIkTelemetryCursor)
        {
            biteIkSolveEvents = default;
            biteIkTelemetryCursor = default;

            IDataVault vault = _dataVault;
            if (vault == null)
            {
                return false;
            }

            return TryOpenVaultBuffer(vault, ref _biteIkSolveEventsHandle, BufferID.BiteIkSolveEvents, ProceduralBiteIkConstants.TelemetryCapacity, out biteIkSolveEvents) &&
                   TryOpenVaultBuffer(vault, ref _biteIkTelemetryCursorHandle, BufferID.BiteIkTelemetryCursor, 1, out biteIkTelemetryCursor);
        }

        private bool EnsureBiteIkVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return false;

            return OpenOrAcquireVaultBuffer(vault, ref _jawIkTargetsHandle, BufferID.JawIkTargets, ProceduralBiteIkConstants.TargetCapacity, NativeArrayOptions.ClearMemory, out _) &&
                   OpenOrAcquireVaultBuffer(vault, ref _currentJawPosHandle, BufferID.CurrentJawPos, ProceduralBiteIkConstants.CurrentJawPoseCapacity, NativeArrayOptions.ClearMemory, out _) &&
                   OpenOrAcquireVaultBuffer(vault, ref _biteIkSolveEventsHandle, BufferID.BiteIkSolveEvents, ProceduralBiteIkConstants.TelemetryCapacity, NativeArrayOptions.ClearMemory, out _) &&
                   OpenOrAcquireVaultBuffer(vault, ref _biteIkTelemetryCursorHandle, BufferID.BiteIkTelemetryCursor, 1, NativeArrayOptions.ClearMemory, out _);
        }

        private bool OpenOrAcquireVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryOpenVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.AnimationFauna,
                options);
            return TryOpenVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private bool TryOpenVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                requiredLength <= 0 ||
                !IsMatchingVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                vault.IsCompactionFenceActive ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsMatchingVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.AnimationFauna &&
                   handle.Generation != 0u;
        }

        private static bool TryOpenExistingVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                requiredLength <= 0 ||
                !vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) ||
                !IsMatchingVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                vault.IsCompactionFenceActive ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private void DisposePersistentBuffers()
        {
            if (_solverScheduled)
            {
                DispatcherJobSwap.TryComplete(ref _pendingHandle, forceComplete: true);
                ReleaseTerrainSdfSnapshotLock();
                CaptureCompletedSolverTelemetry();
            }

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
            _segmentPositionsHandle = default;
            _previousSegmentPositionsHandle = default;
            _leviathanBonesHandle = default;
            _boneConstraintsHandle = default;
            _colliderProxiesHandle = default;
            _rigCsvScratchHandle = default;
            _telemetryRingHandle = default;
            _telemetryCursorHandle = default;
            _jawIkTargetsHandle = default;
            _currentJawPosHandle = default;
            _biteIkSolveEventsHandle = default;
            _biteIkTelemetryCursorHandle = default;
            _terrainSdfSnapshotHandle = default;
            _terrainSdfSnapshotGuardVault = null;
            _biteVaultReady = false;
        }

        private void CompleteScheduledSolverForLifecycle()
        {
            if (!_solverScheduled)
                return;

            DispatcherJobSwap.TryComplete(ref _pendingHandle, forceComplete: true);
            _solverScheduled = false;
            ReleaseTerrainSdfSnapshotLock();
            CaptureCompletedSolverTelemetry();
            AdvanceFrameIndex();
            if (TelemetryHasInvalidFrame())
                DumpTelemetryBlackBoxOnce();
        }

        private void ReleaseTerrainSdfSnapshotLock()
        {
            if (!_terrainSdfSnapshotGuardHeld)
                return;

            IDataVault vault = _terrainSdfSnapshotGuardVault;
            if (vault != null)
                vault.ReleaseMutationGuard(TerrainSdfSnapshotMutationGuardMask);

            _terrainSdfSnapshotGuardHeld = false;
            _terrainSdfSnapshotGuardVault = null;
        }

        private void HydrateRigDefinitionsOrMockCold()
        {
            bool hydrated = TryHydrateRigDefinitionsBinaryCold();
            if (!hydrated)
                GenerateEmergencyMockRig();

            TryHydrateRigConstraintsCsvCold();
        }

        private bool TryHydrateRigDefinitionsBinaryCold()
        {
            if (!TryResolveSpineVaultBuffers(
                    out NativeArray<float3> segmentPositions,
                    out NativeArray<float3> previousSegmentPositions,
                    out NativeArray<LeviathanBoneDTO> leviathanBones,
                    out _,
                    out _) ||
                !TryResolveProceduralAuxVaultBuffers(
                    out NativeArray<LeviathanBoneConstraintsDTO> boneConstraints,
                    out NativeArray<LeviathanCapsuleColliderDTO> colliderProxies,
                    out NativeArray<byte> rigScratch) ||
                !rigScratch.IsCreated ||
                rigScratch.Length < LeviathanRigHeaderBytes + LeviathanRigRowBytes)
            {
                return false;
            }

            if (TryHydrateRigDefinitionBinaryCold(
                    _generatedRigDefinitionBinary,
                    rigScratch,
                    segmentPositions,
                    previousSegmentPositions,
                    leviathanBones,
                    boneConstraints,
                    colliderProxies))
            {
                return true;
            }

            string streamingPath = Path.Combine(Application.streamingAssetsPath, "leviathan_rig_definitions.h8bin");
            if (TryHydrateRigDefinitionBinaryCold(
                    streamingPath,
                    rigScratch,
                    segmentPositions,
                    previousSegmentPositions,
                    leviathanBones,
                    boneConstraints,
                    colliderProxies))
            {
                return true;
            }

            string archivePath = Path.Combine(Application.dataPath, "_Project/_Archive/leviathan_rig_definitions.h8bin");
            return TryHydrateRigDefinitionBinaryCold(
                archivePath,
                rigScratch,
                segmentPositions,
                previousSegmentPositions,
                leviathanBones,
                boneConstraints,
                colliderProxies);
        }

        private bool TryHydrateRigDefinitionBinaryCold(
            TextAsset asset,
            NativeArray<byte> rigScratch,
            NativeArray<float3> segmentPositions,
            NativeArray<float3> previousSegmentPositions,
            NativeArray<LeviathanBoneDTO> leviathanBones,
            NativeArray<LeviathanBoneConstraintsDTO> boneConstraints,
            NativeArray<LeviathanCapsuleColliderDTO> colliderProxies)
        {
            if (asset == null)
                return false;

            byte[] bytes = asset.bytes; // COLD ALLOC: generated rig TextAsset payload is parsed only during vault hydration.
            if (bytes == null || bytes.Length < LeviathanRigHeaderBytes + LeviathanRigRowBytes)
                return false;

            int maxBytes = Math.Min(rigScratch.Length, Math.Min(bytes.Length, 4096));
            for (int i = 0; i < maxBytes; i++)
                rigScratch[i] = bytes[i];

            return TryParseRigDefinitionBinary(
                rigScratch,
                maxBytes,
                ResolveOwnerRuntimePosition(),
                ResolveOwnerForward(),
                SanitizePositiveFinite(_bodyRadius, 1.15f, 0.01f),
                segmentPositions,
                previousSegmentPositions,
                leviathanBones,
                boneConstraints,
                colliderProxies,
                out int activeBoneCount) &&
                activeBoneCount > 1;
        }

        private bool TryHydrateRigDefinitionBinaryCold(
            string path,
            NativeArray<byte> rigScratch,
            NativeArray<float3> segmentPositions,
            NativeArray<float3> previousSegmentPositions,
            NativeArray<LeviathanBoneDTO> leviathanBones,
            NativeArray<LeviathanBoneConstraintsDTO> boneConstraints,
            NativeArray<LeviathanCapsuleColliderDTO> colliderProxies)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            try
            {
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length < LeviathanRigHeaderBytes + LeviathanRigRowBytes)
                    return false;

                int maxBytes = (int)Math.Min(rigScratch.Length, Math.Min(stream.Length, 4096L));
                Span<byte> coldReadBuffer = stackalloc byte[4096];
                int read = stream.Read(coldReadBuffer.Slice(0, maxBytes));
                for (int i = 0; i < read; i++)
                    rigScratch[i] = coldReadBuffer[i];

                return TryParseRigDefinitionBinary(
                    rigScratch,
                    read,
                    ResolveOwnerRuntimePosition(),
                    ResolveOwnerForward(),
                    SanitizePositiveFinite(_bodyRadius, 1.15f, 0.01f),
                    segmentPositions,
                    previousSegmentPositions,
                    leviathanBones,
                    boneConstraints,
                    colliderProxies,
                    out int activeBoneCount) &&
                    activeBoneCount > 1;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        private bool TryParseRigDefinitionBinary(
            NativeArray<byte> bytes,
            int length,
            float3 rootPosition,
            float3 ownerForward,
            float bodyRadius,
            NativeArray<float3> segmentPositions,
            NativeArray<float3> previousSegmentPositions,
            NativeArray<LeviathanBoneDTO> leviathanBones,
            NativeArray<LeviathanBoneConstraintsDTO> boneConstraints,
            NativeArray<LeviathanCapsuleColliderDTO> colliderProxies,
            out int activeBoneCount)
        {
            activeBoneCount = 0;
            if (!bytes.IsCreated ||
                length < LeviathanRigHeaderBytes + LeviathanRigRowBytes ||
                !segmentPositions.IsCreated ||
                !previousSegmentPositions.IsCreated ||
                !leviathanBones.IsCreated ||
                !boneConstraints.IsCreated)
            {
                return false;
            }

            uint magic = ReadUInt32Little(bytes, 0);
            bool swapEndian = false;
            if (!IsLeviathanRigMagic(magic))
            {
                uint swappedMagic = ReverseBytes(magic);
                if (!IsLeviathanRigMagic(swappedMagic))
                    return false;

                swapEndian = true;
            }

            uint rowCountRaw = ReadUInt32(bytes, 8, swapEndian);
            uint headerBytesRaw = ReadUInt32(bytes, 12, swapEndian);
            int headerBytes = math.clamp((int)headerBytesRaw, LeviathanRigHeaderBytes, length);
            int availableRows = (length - headerBytes) / LeviathanRigRowBytes;
            int declaredRows = rowCountRaw > int.MaxValue ? int.MaxValue : (int)rowCountRaw;
            int rowCount = math.clamp(Math.Min(declaredRows, availableRows), 2, MaxSegments);
            if (rowCount <= 1)
                return false;

            float3 forward = NormalizeSafe(ownerForward, new float3(0f, 0f, 1f));
            float3 up = new float3(0f, 1f, 0f);
            float3 cursor = SanitizeFiniteInputFloat3(rootPosition, float3.zero);
            for (int i = 0; i < MaxSegments; i++)
            {
                int rowOffset = headerBytes + math.min(i, rowCount - 1) * LeviathanRigRowBytes;
                int parentIndex = i == 0 ? -1 : i - 1;
                ushort chainId = 0;
                ushort flags = 0;
                float segmentLength = LeviathanTerrainIkConstants.DefaultSegmentLength;
                float maxBendRadians = math.radians(45f);

                if (i < rowCount && rowOffset + LeviathanRigRowBytes <= length)
                {
                    parentIndex = ReadInt32(bytes, rowOffset, swapEndian);
                    chainId = ReadUInt16(bytes, rowOffset + 4, swapEndian);
                    flags = ReadUInt16(bytes, rowOffset + 6, swapEndian);
                    segmentLength = SanitizePositiveFinite(ReadFloat32(bytes, rowOffset + 8, swapEndian), LeviathanTerrainIkConstants.DefaultSegmentLength, LeviathanTerrainIkConstants.MinSegmentLength);
                    maxBendRadians = SanitizePositiveFinite(ReadFloat32(bytes, rowOffset + 12, swapEndian), math.radians(45f), 0f);
                }

                if (i == 0)
                    cursor = rootPosition;
                else
                    cursor -= forward * segmentLength;

                segmentPositions[i] = SanitizeFiniteInputFloat3(cursor, rootPosition);
                previousSegmentPositions[i] = segmentPositions[i];

                LeviathanBoneDTO bone = default;
                bone.LocalToWorld = float4x4.TRS(segmentPositions[i], quaternion.LookRotationSafe(forward, up), new float3(bodyRadius, bodyRadius, segmentLength));
                leviathanBones[i] = bone;

                LeviathanBoneConstraintsDTO constraint = default;
                constraint.ParentIndex = i == 0 ? -1 : math.clamp(parentIndex, 0, i - 1);
                constraint.ChainId = chainId;
                constraint.Flags = flags;
                constraint.SegmentLengthMeters = segmentLength;
                constraint.MaxBendRadians = maxBendRadians;
                boneConstraints[i] = constraint;

                if (colliderProxies.IsCreated && i < colliderProxies.Length)
                    colliderProxies[i] = default;
            }

            _activeSegmentCount = rowCount;
            _motionIntentVelocity = float3.zero;
            _motionIntentHeadTarget = segmentPositions[0] + forward * math.max(LeviathanTerrainIkConstants.MinSegmentLength, boneConstraints[0].SegmentLengthMeters);
            _headLookTargetWorldPosition = _motionIntentHeadTarget;
            _strikeTargetWorldPosition = _motionIntentHeadTarget;
            _motionIntentPending = false;
            _gpuUploadDirty = true;
            _gpuBufferDataValid = false;
            activeBoneCount = rowCount;
            return true;
        }

        private static bool IsLeviathanRigMagic(uint magic)
        {
            return magic == LeviathanRigMagicH8lr || magic == LeviathanRigMagicLvrg;
        }

        private static uint ReadUInt32Little(NativeArray<byte> bytes, int offset)
        {
            return (uint)(bytes[offset] |
                (bytes[offset + 1] << 8) |
                (bytes[offset + 2] << 16) |
                (bytes[offset + 3] << 24));
        }

        private static uint ReadUInt32(NativeArray<byte> bytes, int offset, bool swapEndian)
        {
            uint value = ReadUInt32Little(bytes, offset);
            return swapEndian ? ReverseBytes(value) : value;
        }

        private static uint ReverseBytes(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }

        private static int ReadInt32(NativeArray<byte> bytes, int offset, bool swapEndian)
        {
            return (int)ReadUInt32(bytes, offset, swapEndian);
        }

        private static ushort ReadUInt16(NativeArray<byte> bytes, int offset, bool swapEndian)
        {
            return swapEndian
                ? (ushort)((bytes[offset] << 8) | bytes[offset + 1])
                : (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static float ReadFloat32(NativeArray<byte> bytes, int offset, bool swapEndian)
        {
            return math.asfloat(ReadUInt32(bytes, offset, swapEndian));
        }

        private void GenerateEmergencyMockRig()
        {
            if (!TryResolveSpineVaultBuffers(
                    out NativeArray<float3> segmentPositions,
                    out NativeArray<float3> previousSegmentPositions,
                    out NativeArray<LeviathanBoneDTO> leviathanBones,
                    out _,
                    out _))
            {
                return;
            }

            float3 origin = ResolveOwnerRuntimePosition();
            float3 forward = ResolveOwnerForward();
            float qualityWeight = ResolveGlobalQualityWeight();
            float qualityCurve = SmoothQualityCurve(qualityWeight);
            float segmentLength = SanitizePositiveFinite(_segmentLength, LeviathanTerrainIkConstants.DefaultSegmentLength, LeviathanTerrainIkConstants.MinSegmentLength);
            float bodyRadius = SanitizePositiveFinite(_bodyRadius, 1.15f, 0.01f);
            TryResolveProceduralAuxVaultBuffers(
                out NativeArray<LeviathanBoneConstraintsDTO> boneConstraints,
                out NativeArray<LeviathanCapsuleColliderDTO> colliderProxies,
                out _);
            for (int i = 0; i < MaxSegments; i++)
            {
                float3 position = origin - forward * (segmentLength * i);
                segmentPositions[i] = position;
                previousSegmentPositions[i] = position;
                LeviathanBoneDTO bone = default;
                bone.LocalToWorld = float4x4.TRS(position, quaternion.LookRotationSafe(forward, new float3(0f, 1f, 0f)), new float3(bodyRadius, bodyRadius, segmentLength));
                leviathanBones[i] = bone;
                if (boneConstraints.IsCreated && i < boneConstraints.Length)
                {
                    LeviathanBoneConstraintsDTO constraint = default;
                    constraint.ParentIndex = i == 0 ? -1 : i - 1;
                    constraint.ChainId = 0;
                    constraint.Flags = (ushort)(i < LeviathanTerrainIkConstants.FallbackMockBoneCount ? 1 : 0);
                    constraint.SegmentLengthMeters = segmentLength;
                    constraint.MaxBendRadians = math.radians(math.lerp(18f, 70f, qualityCurve));
                    boneConstraints[i] = constraint;
                }

                if (colliderProxies.IsCreated && i < colliderProxies.Length)
                    colliderProxies[i] = default;
            }

            _activeSegmentCount = LeviathanTerrainIkConstants.FallbackMockBoneCount;
            _motionIntentVelocity = float3.zero;
            _motionIntentHeadTarget = origin + forward * segmentLength;
            _headLookTargetWorldPosition = _motionIntentHeadTarget;
            _strikeTargetWorldPosition = _motionIntentHeadTarget;
            _motionIntentPending = false;
            _gpuUploadDirty = true;
            _gpuBufferDataValid = false;
        }

        private void TryHydrateRigConstraintsCsvCold()
        {
#if !UNITY_EDITOR
            return;
#else
            if (!TryResolveProceduralAuxVaultBuffers(
                    out NativeArray<LeviathanBoneConstraintsDTO> boneConstraints,
                    out _,
                    out NativeArray<byte> csvScratch) ||
                !boneConstraints.IsCreated ||
                !csvScratch.IsCreated)
            {
                return;
            }

            string csvPath = Path.Combine(Application.dataPath, "_SourceData", "Fauna", "leviathan_rig_constraints.csv");
            if (!File.Exists(csvPath))
                return;

            try
            {
                using FileStream stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                int maxBytes = math.min(csvScratch.Length, 4096);
                byte[] coldReadBuffer = new byte[maxBytes]; // COLD ALLOC: Editor/boot CSV bridge only; parser below consumes bytes without strings.
                int read = stream.Read(coldReadBuffer, 0, maxBytes);
                for (int i = 0; i < read; i++)
                    csvScratch[i] = coldReadBuffer[i];

                ParseConstraintCsv(csvScratch, read, boneConstraints);
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
#endif
        }

#if UNITY_EDITOR
        private static void ParseConstraintCsv(
            NativeArray<byte> bytes,
            int length,
            NativeArray<LeviathanBoneConstraintsDTO> boneConstraints)
        {
            int cursor = 0;
            while (cursor < length)
            {
                int boneIndex = ParsePositiveInt(bytes, length, ref cursor);
                SkipCsvSeparator(bytes, length, ref cursor);
                float segmentLength = ParsePositiveFloat(bytes, length, ref cursor);
                SkipCsvSeparator(bytes, length, ref cursor);
                float maxBendDegrees = ParsePositiveFloat(bytes, length, ref cursor);
                SkipLine(bytes, length, ref cursor);

                if ((uint)boneIndex >= (uint)boneConstraints.Length)
                    continue;

                LeviathanBoneConstraintsDTO constraint = boneConstraints[boneIndex];
                constraint.SegmentLengthMeters = SanitizePositiveFinite(segmentLength, constraint.SegmentLengthMeters, LeviathanTerrainIkConstants.MinSegmentLength);
                constraint.MaxBendRadians = math.radians(SanitizePositiveFinite(maxBendDegrees, math.degrees(constraint.MaxBendRadians), 0f));
                boneConstraints[boneIndex] = constraint;
            }
        }

        private static int ParsePositiveInt(NativeArray<byte> bytes, int length, ref int cursor)
        {
            int value = 0;
            while (cursor < length)
            {
                byte b = bytes[cursor];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                value = math.min(9999, value * 10 + (b - (byte)'0'));
                cursor++;
            }

            return value;
        }

        private static float ParsePositiveFloat(NativeArray<byte> bytes, int length, ref int cursor)
        {
            float value = 0f;
            float scale = 0.1f;
            bool fraction = false;
            while (cursor < length)
            {
                byte b = bytes[cursor];
                if (b == (byte)'.')
                {
                    fraction = true;
                    cursor++;
                    continue;
                }

                if (b < (byte)'0' || b > (byte)'9')
                    break;

                int digit = b - (byte)'0';
                if (fraction)
                {
                    value += digit * scale;
                    scale *= 0.1f;
                }
                else
                {
                    value = value * 10f + digit;
                }

                cursor++;
            }

            return value;
        }

        private static void SkipCsvSeparator(NativeArray<byte> bytes, int length, ref int cursor)
        {
            while (cursor < length)
            {
                byte b = bytes[cursor];
                if (b == (byte)',' || b == (byte)';' || b == (byte)' ' || b == (byte)'\t')
                {
                    cursor++;
                    continue;
                }

                break;
            }
        }

        private static void SkipLine(NativeArray<byte> bytes, int length, ref int cursor)
        {
            while (cursor < length)
            {
                byte b = bytes[cursor++];
                if (b == (byte)'\n')
                    break;
            }
        }
#endif

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

        private void ApplyIntentTargetOverrides()
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

            Bounds bounds = BuildFallbackStrikeBounds(_strikeTargetWorldPosition);

            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            Transform targetTransform = _strikeTarget;
            uint targetHash = Hecton8.Core.RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(targetTransform.GetEntityId()));
            if (targetHash == 0u)
                targetHash = 1u;

            if (!TryResolveAupFromRuntimeOrigin(center, out AbsoluteUniversePosition centerAup))
                return false;

            JawIkTarget target = new JawIkTarget
            {
                CenterAup = centerAup,
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

            if (!TryResolveOwnerAup(out AbsoluteUniversePosition ownerAup))
                return;

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

        private uint ResolveBiteRuntimeFlags()
        {
            uint flags = 0u;
            if (_strikeActive || _strikeSignalActive)
                flags |= ProceduralBiteIkConstants.RuntimeFlagStrikeActive;
            return flags;
        }

        private void PublishBiteFeedbackIfNeeded()
        {
            if (!TryResolveCurrentJawPosVaultBuffer(out NativeArray<CurrentJawPos> currentJawPos) ||
                currentJawPos.Length <= 0)
            {
                return;
            }

            CurrentJawPos pose = currentJawPos[0];
            if (pose.TargetHash == 0u)
                return;

            uint completedFrame = unchecked((uint)(_frameIndex == 0 ? int.MaxValue : _frameIndex - 1));
            uint currentFrame = unchecked((uint)math.max(0, _frameIndex));
            if (pose.Frame != completedFrame && pose.Frame != currentFrame)
                return;

            if ((pose.Flags & ProceduralBiteIkConstants.ResultFlagInvalid) != 0u)
                DumpBiteTelemetryBlackBoxOnce();

            int frame = math.max(0, _frameIndex);
            int biteFeedbackCooldownFrames = math.max(1, (int)math.ceil(BiteFeedbackCooldownSeconds * 60f));
            int biteFeedbackElapsedFrames = _lastBiteFeedbackFrame < 0 || frame < _lastBiteFeedbackFrame
                ? biteFeedbackCooldownFrames
                : frame - _lastBiteFeedbackFrame;
            if ((pose.Flags & ProceduralBiteIkConstants.ResultFlagFeedback) != 0u &&
                biteFeedbackElapsedFrames >= biteFeedbackCooldownFrames)
            {
                _lastBiteFeedbackFrame = frame;
                if (!TryResolveAupFromRuntimeOrigin(pose.JawTipPosition, out AbsoluteUniversePosition pointAup))
                    return;

                DebrisSpawnSignal debris = default;
                debris.PositionAup = pointAup;
                debris.SpeciesHash = BiteSparksSignalHash;
                debris.SourceEntityId = pose.TargetHash;
                debris.Intensity01 = math.saturate(1f - pose.ContactDistanceMeters);
                debris.DebrisKind = DebrisSpawnSignal.DebrisKindSparks;
                debris.Flags = ResolveBiteDebrisFlags();
                debris.Quantity = ResolveBiteDebrisQuantity(_globalQualityWeight);
                SignalBus<DebrisSpawnSignal>.TryPushTracked(in debris, ref _signalPushDropCount);
                PublishBiteHullDent(in pose, frame, debris.Intensity01);

                HapticRequest haptic = default;
                haptic.Intensity01 = debris.Intensity01;
                haptic.DurationSeconds = math.lerp(0.05f, 0.18f, haptic.Intensity01);
                haptic.Frequency01 = 0.85f;
                haptic.SourceHash = pose.TargetHash;
                haptic.Frame = unchecked((uint)frame);
                haptic.Channel = HapticRequest.ChannelCrush;
                haptic.Flags = HapticRequest.FlagCrush;
                SignalBus<HapticRequest>.TryPushTracked(in haptic, ref _signalPushDropCount);
            }

            int biteAudioCooldownFrames = math.max(1, (int)math.ceil(BiteAudioCooldownSeconds * 60f));
            int biteAudioElapsedFrames = _lastBiteAudioFrame < 0 || frame < _lastBiteAudioFrame
                ? biteAudioCooldownFrames
                : frame - _lastBiteAudioFrame;
            if ((pose.Flags & ProceduralBiteIkConstants.ResultFlagAudioJawSnap) != 0u &&
                biteAudioElapsedFrames >= biteAudioCooldownFrames)
            {
                _lastBiteAudioFrame = frame;
                AcousticPingSignal signal = default;
                if (!TryResolveAupFromRuntimeOrigin(pose.JawTipPosition, out AbsoluteUniversePosition jawTipAup))
                    return;

                signal.PositionAup = jawTipAup;
                signal.RadiusMeters = 18f;
                signal.Intensity01 = math.saturate(1f - pose.TargetDistanceMeters * 0.5f);
                signal.SourceId = pose.TargetHash;
                signal.Channel = AcousticPingSignal.ChannelJawSnap;
                signal.Flags = AcousticPingSignal.FlagJawSnap;
                SignalBus<AcousticPingSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
            }
        }

        private void PublishBiteHullDent(in CurrentJawPos pose, int frame, float intensity01)
        {
            float visualOverkillWeight = ProceduralBiteIkConstants.DecodeVisualOverkillWeight01(pose.Flags);
            float radiusWeight = intensity01 * math.lerp(0.35f, 1f, visualOverkillWeight);
            float depthWeight = intensity01 * math.lerp(0.25f, 1f, visualOverkillWeight);
            float radius = math.lerp(BiteHullDentMinimumRadiusMeters, BiteHullDentMaximumRadiusMeters, radiusWeight);
            float depth = math.lerp(BiteHullDentMinimumDepthMeters, BiteHullDentMaximumDepthMeters, depthWeight);
            byte flags = HullDeformedSignal.LegacyLocalPointFlag;

            HullDeformedSignal dent = default;
            dent.LocalPoint = SanitizeFiniteInputFloat3(pose.JawTipPosition, ResolveOwnerRuntimePosition());
            dent.Radius = SanitizePositiveFinite(radius, BiteHullDentMinimumRadiusMeters, 0.01f);
            dent.Depth = SanitizePositiveFinite(depth, BiteHullDentMinimumDepthMeters, 0f);
            dent.Intensity01 = math.saturate(intensity01);
            dent.TargetHash = pose.TargetHash;
            dent.SourceHash = BiteSparksSignalHash;
            dent.Frame = unchecked((uint)math.max(0, frame));
            dent.TargetId = ClampHashToUShort(pose.TargetHash);
            dent.SourceId = 0;
            dent.ActiveDentCount = 0;
            dent.Flags = flags;
            dent.QualityTier = ResolveQualityWeightByte(_globalQualityWeight);
            dent.Channel = AcousticPingSignal.ChannelJawSnap;
            dent.DamageType = BiteSparksSignalHash;
            SignalBus<HullDeformedSignal>.TryPushTracked(in dent, ref _signalPushDropCount);
        }

        private static byte ResolveBiteDebrisFlags()
        {
            return DebrisSpawnSignal.FlagToolSparks | DebrisSpawnSignal.FlagComputeShard;
        }

        private static ushort ResolveBiteDebrisQuantity(float qualityWeight)
        {
            float quality = SmoothQualityCurve(qualityWeight);
            float overkillWeight = ResolveBiteVisualOverkillWeight(qualityWeight);
            float standardQuantity = math.lerp(BiteMinimumQualityDebrisQuantity, BiteMiddleQualityDebrisQuantity, quality);
            float highFidelityQuantity = math.lerp(BiteMiddleQualityDebrisQuantity, BiteMaximumQualityDebrisQuantity, quality);
            float overkillQuantity = math.lerp(BiteMaximumQualityDebrisQuantity, BiteOverkillQualityDebrisQuantity, quality);
            float visualQuantity = math.lerp(standardQuantity, highFidelityQuantity, quality);
            return (ushort)math.clamp(
                (int)math.round(math.lerp(visualQuantity, overkillQuantity, overkillWeight)),
                BiteMinimumQualityDebrisQuantity,
                BiteOverkillQualityDebrisQuantity);
        }

        private static float ResolveBiteVisualOverkillWeight(float qualityWeight)
        {
            return SmoothQualityCurve(qualityWeight);
        }

        private static ushort ClampHashToUShort(uint value)
        {
            return value > ushort.MaxValue ? ushort.MaxValue : (ushort)value;
        }

        private static byte ResolveQualityWeightByte(float qualityWeight)
        {
            return (byte)math.clamp((int)math.round(SanitizeQualityWeight01(qualityWeight) * byte.MaxValue), 0, byte.MaxValue);
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

        private void RefreshQualityState(float deltaTime, float qualityWeight)
        {
            float weight = SanitizeQualityWeight01(qualityWeight);
            float curve = SmoothQualityCurve(weight);
            int requestedSegmentCount = math.clamp(
                (int)math.round(math.lerp(MinimumQualitySegments, math.clamp(_maximumQualitySegmentCount, MinimumQualitySegments, MaxSegments), curve)),
                MinimumQualitySegments,
                MaxSegments);
            _activeSegmentCount = requestedSegmentCount;

            int requestedIterations = math.clamp(
                (int)math.round(math.lerp(1f, math.clamp(_maximumQualityConstraintIterations, 1, 10), curve)),
                1,
                10);
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
            float qualityWeight,
            out NativeArray<byte>.ReadOnly sdfTexture3D,
            out int3 sdfDimensions,
            out float3 sdfOrigin,
            out float3 sdfCellSize,
            out float sdfRange,
            out NativeArray<ushort> heightSamples,
            out float3 terrainOrigin,
            out float3 terrainSize,
            out int terrainResolution,
            out bool terrainSdfSnapshotLocked)
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
            terrainSdfSnapshotLocked = false;

            float3 ownerPosition = ResolveOwnerRuntimePosition();
            ResolveSdfPayload(
                qualityWeight,
                ownerPosition,
                out sdfTexture3D,
                out sdfDimensions,
                out sdfOrigin,
                out sdfCellSize,
                out sdfRange,
                out terrainSdfSnapshotLocked);
            ResolveMapMagicPayload(ownerPosition, out heightSamples, out terrainOrigin, out terrainSize, out terrainResolution);
        }

        private void ResolveSdfPayload(
            float qualityWeight,
            float3 targetPosition,
            out NativeArray<byte>.ReadOnly sdfTexture3D,
            out int3 sdfDimensions,
            out float3 sdfOrigin,
            out float3 sdfCellSize,
            out float sdfRange,
            out bool snapshotLocked)
        {
            sdfTexture3D = default;
            sdfDimensions = default;
            sdfOrigin = float3.zero;
            sdfCellSize = float3.zero;
            sdfRange = 0f;
            snapshotLocked = false;

            float sdfHuggingWeight = ResolveSdfHuggingWeight(qualityWeight);
            if (!_enableSdfHugging || sdfHuggingWeight <= 0f)
                return;

            if (!HectonVoxelVolume.TryAcquireClosestPublishedSonarSdfPayloadReadLease(
                    ToVector3(targetPosition),
                    out HectonVoxelVolume publishedVolume,
                    out NativeArray<byte>.ReadOnly publishedSdf,
                    out _,
                    out Vector3Int dimensions,
                    out Vector3 origin,
                    out Vector3 cellSize,
                    out float range,
                    out _,
                    out HectonVoxelVolume.PublishedSonarSdfReadLease publishedLease))
            {
                return;
            }

            bool accepted = false;
            try
            {
                int3 resolvedDimensions = new int3(dimensions.x, dimensions.y, dimensions.z);
                if (!LeviathanTerrainIkJob.TryResolveSdfVoxelCount(resolvedDimensions, out int expectedLength) ||
                    !publishedSdf.IsCreated ||
                    publishedSdf.Length != expectedLength)
                {
                    return;
                }

                if (!TryCopyTerrainSdfLeaseToSnapshot(publishedSdf, expectedLength, out NativeArray<byte>.ReadOnly resolvedSdf, out snapshotLocked))
                    return;

                sdfTexture3D = resolvedSdf;
                sdfDimensions = resolvedDimensions;
                sdfOrigin = (float3)origin;
                sdfCellSize = (float3)cellSize;
                sdfRange = math.max(0f, range) * sdfHuggingWeight;
                accepted = true;
            }
            finally
            {
                if (publishedVolume != null)
                    publishedVolume.ReleasePublishedSonarSdfPayloadReadLease(in publishedLease);
                if (!accepted)
                    UnlockTerrainSdfSnapshot(ref snapshotLocked);
            }
        }

        private unsafe bool TryCopyTerrainSdfLeaseToSnapshot(
            NativeArray<byte>.ReadOnly sourceSdf,
            int requiredLength,
            out NativeArray<byte>.ReadOnly snapshotSdf,
            out bool snapshotLocked)
        {
            snapshotSdf = default;
            snapshotLocked = false;
            if (!sourceSdf.IsCreated || requiredLength <= 0 || sourceSdf.Length < requiredLength)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return false;

            if (!TryOpenVaultBuffer(vault, ref _terrainSdfSnapshotHandle, TerrainSdfSnapshotBuffer, requiredLength, out NativeArray<byte> snapshot))
            {
                if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                    return false;

                _terrainSdfSnapshotHandle = vault.EnsureGenerationHandle<byte>(
                    TerrainSdfSnapshotBuffer,
                    requiredLength,
                    SystemID.AnimationFauna,
                    NativeArrayOptions.UninitializedMemory);
            }

            if (vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(TerrainSdfSnapshotMutationGuardMask))
            {
                return false;
            }

            snapshotLocked = true;
            _terrainSdfSnapshotGuardVault = vault;
            bool handedOff = false;
            try
            {
                if (vault.IsCompactionFenceActive)
                    return false;

                if (!TryOpenVaultBuffer(vault, ref _terrainSdfSnapshotHandle, TerrainSdfSnapshotBuffer, requiredLength, out snapshot))
                    return false;

                void* sourcePtr = sourceSdf.GetUnsafeReadOnlyPtr();
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafePtr(snapshot);
                if (sourcePtr == null || destinationPtr == null)
                    return false;

                UnsafeUtility.MemCpy(destinationPtr, sourcePtr, requiredLength);

                snapshotSdf = snapshot.AsReadOnly();
                handedOff = true;
                return true;
            }
            finally
            {
                if (!handedOff)
                    UnlockTerrainSdfSnapshot(ref snapshotLocked);
            }
        }

        private static float ResolveSdfHuggingWeight(float qualityWeight)
        {
            return SmoothQualityCurve(SanitizeQualityWeight01(qualityWeight));
        }

        private void UnlockTerrainSdfSnapshot(ref bool locked)
        {
            if (!locked)
                return;

            IDataVault vault = _terrainSdfSnapshotGuardVault;
            if (vault != null)
                vault.ReleaseMutationGuard(TerrainSdfSnapshotMutationGuardMask);

            locked = false;
            if (!_terrainSdfSnapshotGuardHeld)
                _terrainSdfSnapshotGuardVault = null;
        }

        private static ulong FaunaVaultMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
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
            ITerrainHeightSampleReadModel terrainHeightSamples = _terrainHeightSamples;
            if (!_enableMapMagicFallback || terrainHeightSamples == null)
                return;

            if (!terrainHeightSamples.TryGetTerrainHeightSamplePayload(targetPosition.x, targetPosition.z, out TerrainHeightSamplePayloadDTO payload) ||
                !TerrainHeightSamplePayloadDTO.IsValid(in payload))
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
            if (TryOpenExistingVaultBuffer(vault, BufferID.TerrainSeamHeightmap, expectedLength, out NativeArray<ushort> vaultHeightmap) &&
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
            if (!TryResolveSpineVaultBuffers(
                    out _,
                    out _,
                    out NativeArray<LeviathanBoneDTO> leviathanBones,
                    out _,
                    out _))
            {
                _gpuBufferDataValid = false;
                ClearGpuSkinningBinding();
                return false;
            }

            if (!HasValidGraphicsBuffer(_bonesGraphicsBufferA, MaxSegments) ||
                !HasValidGraphicsBuffer(_bonesGraphicsBufferB, MaxSegments))
            {
                _gpuBufferDataValid = false;
                ClearGpuSkinningBinding();
                return false;
            }

            GraphicsBuffer writeBuffer = _gpuUploadBufferIndex == 0 ? _bonesGraphicsBufferA : _bonesGraphicsBufferB;
            if (!HasValidGraphicsBuffer(writeBuffer, MaxSegments))
            {
                _gpuBufferDataValid = false;
                ClearGpuSkinningBinding();
                return false;
            }

            int uploadCount = math.clamp(_activeSegmentCount, 1, math.min(MaxSegments, leviathanBones.Length));
            if (!UploadLeviathanBonesToGpu(writeBuffer, leviathanBones, uploadCount))
            {
                _gpuBufferDataValid = false;
                ClearGpuSkinningBinding();
                return false;
            }

            float ikQuality = SanitizeQualityWeight01(_globalQualityWeight);
            float safeSegmentLength = SanitizePositiveFinite(_segmentLength, LeviathanTerrainIkConstants.DefaultSegmentLength, LeviathanTerrainIkConstants.MinSegmentLength);
            float safeTailWhipDuration = SanitizePositiveFinite(_tailWhipDurationSeconds, 1f, 0.0001f);
            float safeTailWhipSecondsRemaining = ResolveSafeTailWhipSecondsRemaining();
            float tailWhip01 = math.saturate(safeTailWhipSecondsRemaining * math.rcp(safeTailWhipDuration));
            LeviathanIkShaderGlobalsDTO globals = new LeviathanIkShaderGlobalsDTO
            {
                Scalars0 = new float4(_activeSegmentCount, ikQuality, tailWhip01, safeSegmentLength),
                Scalars1 = new float4(1f, 0f, 0f, 0f)
            };
            if (!PublishLeviathanIkGlobals(in globals))
            {
                _gpuBufferDataValid = false;
                ClearGpuSkinningBinding();
                return false;
            }

            Shader.SetGlobalBuffer(_LeviathanBonesId, writeBuffer);
            _globalGpuSkinningPublished = true;

            _gpuUploadBufferIndex ^= 1;
            _gpuBufferDataValid = true;
            return true;
        }

        private static unsafe bool UploadLeviathanBonesToGpu(GraphicsBuffer destination, NativeArray<LeviathanBoneDTO> source, int count)
        {
            int safeCount = ResolveLeviathanBoneGpuUploadCount(destination, source.IsCreated ? source.Length : 0, count);
            if (safeCount <= 0)
                return false;

            NativeArray<LeviathanBoneDTO> mapped = destination.LockBufferForWrite<LeviathanBoneDTO>(0, safeCount);
            try
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<LeviathanBoneDTO>() * safeCount;
                long destinationBytes = (long)UnsafeUtility.SizeOf<LeviathanBoneDTO>() * mapped.Length;
                if (sourcePtr == null || destinationPtr == null || copyBytes <= 0L || destinationBytes < copyBytes)
                    return false;

                UnsafeUtility.MemCpy(destinationPtr, sourcePtr, copyBytes);
            }
            finally
            {
                destination.UnlockBufferAfterWrite<LeviathanBoneDTO>(safeCount);
            }

            return true;
        }

        private static int ResolveLeviathanBoneGpuUploadCount(GraphicsBuffer destination, int sourceLength, int requestedCount)
        {
            if (destination == null || requestedCount <= 0 || sourceLength <= 0 || destination.count <= 0)
                return 0;
            if (destination.stride != UnsafeUtility.SizeOf<LeviathanBoneDTO>())
                return 0;

            return math.min(math.min(requestedCount, sourceLength), destination.count);
        }

        private void ClearGpuSkinningBinding()
        {
            _gpuBufferDataValid = false;

            if (_globalGpuSkinningPublished || _activeIkGlobalsBuffer != null)
                ClearGlobalGpuSkinningBinding();
        }

        private void ClearGlobalGpuSkinningBinding()
        {
            LeviathanIkShaderGlobalsDTO disabled = new LeviathanIkShaderGlobalsDTO
            {
                Scalars0 = new float4(0f, 0f, 0f, LeviathanTerrainIkConstants.DefaultSegmentLength),
                Scalars1 = float4.zero
            };
            PublishLeviathanIkGlobals(in disabled);
            _globalGpuSkinningPublished = false;
        }

        private bool PublishLeviathanIkGlobals(in LeviathanIkShaderGlobalsDTO globals)
        {
            if (!ValidateLeviathanIkShaderGlobalsLayout() ||
                !_supportsConstantBufferBinding ||
                !HasValidGraphicsBuffer(_ikGlobalsBufferA, 1) ||
                !HasValidGraphicsBuffer(_ikGlobalsBufferB, 1))
                return false;

            GraphicsBuffer writeBuffer = _ikGlobalsUploadBufferIndex == 0 ? _ikGlobalsBufferA : _ikGlobalsBufferB;
            NativeArray<LeviathanIkShaderGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<LeviathanIkShaderGlobalsDTO>(0, 1);
            try
            {
                mapped[0] = globals;
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<LeviathanIkShaderGlobalsDTO>(1);
            }

            _ikGlobalsUploadBufferIndex ^= 1;
            _activeIkGlobalsBuffer = writeBuffer;
            Shader.SetGlobalConstantBuffer(_LeviathanIkGlobalsId, _activeIkGlobalsBuffer, 0, LeviathanIkGlobalsBytes);
            return true;
        }

        private void RefreshGraphicsCapabilitySnapshotCold()
        {
            _supportsConstantBufferBinding = SystemInfo.supportsSetConstantBuffer;
        }

        private void EnsureVisualGpuBuffersCold()
        {
            EnsureGraphicsBuffersCold();
            if (_supportsConstantBufferBinding)
                EnsureIkGlobalsBuffersCold();
        }

        private bool EnsureIkGlobalsBuffersCold()
        {
            if (!HasValidGraphicsBuffer(_ikGlobalsBufferA, 1))
            {
                ReleaseGraphicsBuffer(ref _ikGlobalsBufferA);
                _ikGlobalsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, LeviathanIkGlobalsBytes); // COLD ALLOC: GraphicsBuffer[32B] - leviathan IK shader globals A - owner: SHINOBU_305
            }

            if (!HasValidGraphicsBuffer(_ikGlobalsBufferB, 1))
            {
                ReleaseGraphicsBuffer(ref _ikGlobalsBufferB);
                _ikGlobalsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, LeviathanIkGlobalsBytes); // COLD ALLOC: GraphicsBuffer[32B] - leviathan IK shader globals B - owner: SHINOBU_305
            }

            return _ikGlobalsBufferA != null && _ikGlobalsBufferA.IsValid() &&
                   _ikGlobalsBufferB != null && _ikGlobalsBufferB.IsValid();
        }

        private void EnsureGraphicsBuffersCold()
        {
            if (!HasValidGraphicsBuffer(_bonesGraphicsBufferA, MaxSegments))
            {
                ReleaseGraphicsBuffer(ref _bonesGraphicsBufferA);
                _bonesGraphicsBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<LeviathanBoneDTO>(MaxSegments); // COLD ALLOC: GraphicsBuffer[20 64B bone DTO] - leviathan bone upload A - owner: FaunaKinematicsRuntime
            }

            if (!HasValidGraphicsBuffer(_bonesGraphicsBufferB, MaxSegments))
            {
                ReleaseGraphicsBuffer(ref _bonesGraphicsBufferB);
                _bonesGraphicsBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<LeviathanBoneDTO>(MaxSegments); // COLD ALLOC: GraphicsBuffer[20 64B bone DTO] - leviathan bone upload B - owner: FaunaKinematicsRuntime
            }
        }

        private void ReleaseGraphicsBuffers()
        {
            ReleaseGraphicsBuffer(ref _bonesGraphicsBufferA);
            ReleaseGraphicsBuffer(ref _bonesGraphicsBufferB);
            ReleaseGraphicsBuffer(ref _ikGlobalsBufferA);
            ReleaseGraphicsBuffer(ref _ikGlobalsBufferB);
            _activeIkGlobalsBuffer = null;
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_disposed || !Application.isPlaying || !isActiveAndEnabled)
                return;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    if (currentService == null)
                        return;

                    TryRegister();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    CompleteScheduledSolverForLifecycle();
                    DisposePersistentBuffers();
                    _dataVault = currentService is IDataVault currentVault ? currentVault : null;
                    EnsurePersistentBuffers();
                    HydrateRigDefinitionsOrMockCold();
                    break;
                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    _terrainHeightSamples = currentService as ITerrainHeightSampleReadModel;
                    break;
            }
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
            if (!IsFiniteOriginShiftOffset(offset))
            {
                DumpTelemetryBlackBoxOnce();
                return;
            }

            if (!IsUsableOriginShiftOffset(offset))
                return;

            _pendingOriginShiftOffset += offset;
            if (!IsFiniteOriginShiftOffset(_pendingOriginShiftOffset))
            {
                _pendingOriginShiftOffset = float3.zero;
                _pendingOriginShiftRebase = false;
                DumpTelemetryBlackBoxOnce();
                return;
            }

            _pendingOriginShiftRebase = true;
        }

        private bool ApplyPendingOriginShiftRebase()
        {
            if (!_pendingOriginShiftRebase)
                return false;

            float3 offset = _pendingOriginShiftOffset;
            _pendingOriginShiftOffset = float3.zero;
            _pendingOriginShiftRebase = false;

            if (!IsFiniteOriginShiftOffset(offset))
            {
                DumpTelemetryBlackBoxOnce();
                return false;
            }

            if (!IsUsableOriginShiftOffset(offset))
                return false;

            ApplyOriginShiftRebase(offset);
            return true;
        }

        private void ApplyOriginShiftRebase(float3 offset)
        {
            if (!IsFiniteOriginShiftOffset(offset))
            {
                DumpTelemetryBlackBoxOnce();
                return;
            }

            if (!IsUsableOriginShiftOffset(offset))
                return;

            if (!TryResolveSpineVaultBuffers(
                    out NativeArray<float3> segmentPositions,
                    out NativeArray<float3> previousSegmentPositions,
                    out NativeArray<LeviathanBoneDTO> leviathanBones,
                    out _,
                    out _))
            {
                return;
            }

            for (int i = 0; i < MaxSegments; i++)
            {
                segmentPositions[i] = SanitizeFiniteInputFloat3(segmentPositions[i] - offset, float3.zero);
                previousSegmentPositions[i] = SanitizeFiniteInputFloat3(previousSegmentPositions[i] - offset, segmentPositions[i]);
                LeviathanBoneDTO bone = leviathanBones[i];
                float4x4 matrix = bone.LocalToWorld;
                float4 c3 = matrix.c3;
                float3 matrixRawPosition = new float3(c3.x - offset.x, c3.y - offset.y, c3.z - offset.z);
                float3 matrixPosition = SanitizeFiniteInputFloat3(matrixRawPosition, segmentPositions[i]);
                matrix.c3 = new float4(matrixPosition, math.isfinite(c3.w) ? c3.w : 1f);
                bone.LocalToWorld = matrix;
                leviathanBones[i] = bone;
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
            if (!TryResolveSpineVaultBuffers(
                    out _,
                    out _,
                    out _,
                    out NativeArray<LeviathanTerrainIkTelemetryEntry> telemetryRing,
                    out NativeArray<int> telemetryCursor) ||
                telemetryRing.Length <= 0 ||
                telemetryCursor.Length <= 0)
            {
                return false;
            }

            int index = (telemetryCursor[0] - 1) % telemetryRing.Length;
            if (index < 0)
                index += telemetryRing.Length;

            return (telemetryRing[index].Flags & LeviathanTerrainIkConstants.TelemetryFlagInvalid) != 0u;
        }

        private void CaptureCompletedSolverTelemetry()
        {
            CaptureSolverLatencyMicros();
            PatchLatestTelemetrySolveMicros();
        }

        private void CaptureSolverLatencyMicros()
        {
            long start = _solverScheduleTimestamp;
            _solverScheduleTimestamp = 0L;
            if (start <= 0L)
            {
                _lastBurstSolveMicros = 0f;
                return;
            }

            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - start;
            double elapsedMicros = elapsedTicks > 0L
                ? (elapsedTicks * 1000000.0) / System.Diagnostics.Stopwatch.Frequency
                : 0.0;
            _lastBurstSolveMicros = !double.IsNaN(elapsedMicros) && !double.IsInfinity(elapsedMicros)
                ? (float)math.min(elapsedMicros, 1000000.0)
                : 0f;
        }

        private void PatchLatestTelemetrySolveMicros()
        {
            if (!TryResolveSpineVaultBuffers(
                    out _,
                    out _,
                    out _,
                    out NativeArray<LeviathanTerrainIkTelemetryEntry> telemetryRing,
                    out NativeArray<int> telemetryCursor) ||
                telemetryRing.Length <= 0 ||
                telemetryCursor.Length <= 0)
            {
                return;
            }

            int cursor = telemetryCursor[0];
            if (cursor == 0)
                return;

            int index = (cursor - 1) % telemetryRing.Length;
            if (index < 0)
                index += telemetryRing.Length;

            LeviathanTerrainIkTelemetryEntry entry = telemetryRing[index];
            entry.BurstSolveMicros = SanitizePositiveFinite(_lastBurstSolveMicros, 0f, 0f);
            telemetryRing[index] = entry;
        }

        private void AdvanceFrameIndex()
        {
            _frameIndex = _frameIndex == int.MaxValue ? 0 : _frameIndex + 1;
        }

        private void DumpTelemetryBlackBoxOnce()
        {
            if (_telemetryDumped ||
                !TryResolveSpineVaultBuffers(
                    out _,
                    out _,
                    out _,
                    out NativeArray<LeviathanTerrainIkTelemetryEntry> telemetryRing,
                    out _ ) ||
                !telemetryRing.IsCreated)
            {
                return;
            }

            if (DumpTelemetryBlackBox())
                _telemetryDumped = true;
        }

        private bool DumpTelemetryBlackBox()
        {
            TryResolveSpineVaultBuffers(
                out _,
                out _,
                out _,
                out NativeArray<LeviathanTerrainIkTelemetryEntry> telemetryRing,
                out NativeArray<int> telemetryCursor);
            int cursor = telemetryCursor.IsCreated && telemetryCursor.Length > 0 ? telemetryCursor[0] : 0;
            int ringLength = telemetryRing.IsCreated ? math.min(LeviathanTerrainIkConstants.TelemetryCapacity, telemetryRing.Length) : 0;
            int entryCount = cursor >= ringLength ? ringLength : math.max(0, cursor);
            int firstEntryIndex = entryCount == ringLength && ringLength > 0 ? cursor % ringLength : 0;
            int byteCount = TelemetryDumpHeaderBytes + entryCount * TelemetryEntryPayloadBytes;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(FaunaKinematicsRuntime),
                    TelemetryDumpPayloadLabel);
                int writeCursor = 0;
                WriteUInt64LittleEndian(payload, ref writeCursor, TelemetryDumpMagic);
                WriteInt32LittleEndian(payload, ref writeCursor, entryCount);
                WriteInt32LittleEndian(payload, ref writeCursor, cursor);
                WriteInt32LittleEndian(payload, ref writeCursor, TelemetryEntryPayloadBytes);

                for (int i = 0; i < entryCount; i++)
                {
                    int sourceIndex = (firstEntryIndex + i) % ringLength;
                    LeviathanTerrainIkTelemetryEntry entry = telemetryRing[sourceIndex];
                    WriteInt32LittleEndian(payload, ref writeCursor, entry.FrameIndex);
                    WriteInt32LittleEndian(payload, ref writeCursor, entry.ActiveSegmentCount);
                    WriteUInt32LittleEndian(payload, ref writeCursor, entry.Flags);
                    WriteUInt32LittleEndian(payload, ref writeCursor, entry.StateHash);
                    WriteFloat3LittleEndian(payload, ref writeCursor, entry.HeadPosition);
                    WriteFloat3LittleEndian(payload, ref writeCursor, entry.TailPosition);
                    WriteFloat3LittleEndian(payload, ref writeCursor, entry.IntendedVelocity);
                    WriteFloatLittleEndian(payload, ref writeCursor, entry.MaxTerrainPushMeters);
                    WriteFloatLittleEndian(payload, ref writeCursor, entry.TailWhipSecondsRemaining);
                    WriteFloatLittleEndian(payload, ref writeCursor, entry.GlobalQualityWeight);
                    WriteDouble3LittleEndian(payload, ref writeCursor, entry.RootAup);
                    WriteFloatLittleEndian(payload, ref writeCursor, entry.AverageFabrikIterations);
                    WriteFloatLittleEndian(payload, ref writeCursor, entry.BurstSolveMicros);
                }

                return writeCursor == byteCount && NativeFaultDumpWriter.TryWriteAll(TelemetryDumpRelativePath, payload, byteCount);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(FaunaKinematicsRuntime),
                    TelemetryDumpPayloadLabel);
            }
        }

        private void DumpBiteTelemetryBlackBoxOnce()
        {
            if (_biteTelemetryDumped ||
                !TryResolveBiteTelemetryVaultBuffers(
                    out NativeArray<BiteIkSolveEvent> biteIkSolveEvents,
                    out _) ||
                !biteIkSolveEvents.IsCreated)
            {
                return;
            }

            if (DumpBiteTelemetryBlackBox())
                _biteTelemetryDumped = true;
        }

        private bool DumpBiteTelemetryBlackBox()
        {
            TryResolveBiteTelemetryVaultBuffers(
                out NativeArray<BiteIkSolveEvent> biteIkSolveEvents,
                out NativeArray<int> biteIkTelemetryCursor);

            int cursor = biteIkTelemetryCursor.IsCreated && biteIkTelemetryCursor.Length > 0 ? biteIkTelemetryCursor[0] : 0;
            int ringLength = biteIkSolveEvents.IsCreated ? math.min(ProceduralBiteIkConstants.TelemetryCapacity, biteIkSolveEvents.Length) : 0;
            int entryCount = cursor >= ringLength ? ringLength : math.max(0, cursor);
            int firstEntryIndex = entryCount == ringLength && ringLength > 0 ? cursor % ringLength : 0;
            int byteCount = TelemetryDumpHeaderBytes + entryCount * BiteTelemetryEntryPayloadBytes;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(FaunaKinematicsRuntime),
                    BiteTelemetryDumpPayloadLabel);
                int writeCursor = 0;
                WriteUInt64LittleEndian(payload, ref writeCursor, BiteTelemetryDumpMagic);
                WriteInt32LittleEndian(payload, ref writeCursor, entryCount);
                WriteInt32LittleEndian(payload, ref writeCursor, cursor);
                WriteInt32LittleEndian(payload, ref writeCursor, BiteTelemetryEntryPayloadBytes);

                for (int i = 0; i < entryCount; i++)
                {
                    int sourceIndex = (firstEntryIndex + i) % ringLength;
                    BiteIkSolveEvent entry = biteIkSolveEvents[sourceIndex];
                    WriteInt32LittleEndian(payload, ref writeCursor, entry.FrameIndex);
                    WriteUInt32LittleEndian(payload, ref writeCursor, entry.Flags);
                    WriteUInt32LittleEndian(payload, ref writeCursor, entry.StateHash);
                    WriteUInt32LittleEndian(payload, ref writeCursor, entry.TargetHash);
                    WriteFloat3LittleEndian(payload, ref writeCursor, entry.JawTipPosition);
                    WriteFloatLittleEndian(payload, ref writeCursor, entry.DistanceMeters);
                    WriteFloat3LittleEndian(payload, ref writeCursor, entry.ClosestPoint);
                    WriteFloatLittleEndian(payload, ref writeCursor, entry.Reach01);
                    WriteFloat3LittleEndian(payload, ref writeCursor, entry.TargetLocalCenter);
                    WriteFloatLittleEndian(payload, ref writeCursor, entry.SystemStress01);
                    WriteFloat3LittleEndian(payload, ref writeCursor, entry.HeadPosition);
                    WriteFloatLittleEndian(payload, ref writeCursor, entry.ContactDistanceMeters);
                    WriteFloat3LittleEndian(payload, ref writeCursor, entry.WrapAnchor0);
                    WriteFloatLittleEndian(payload, ref writeCursor, entry.Blend01);
                    WriteFloat3LittleEndian(payload, ref writeCursor, entry.WrapAnchor1);
                    WriteFloatLittleEndian(payload, ref writeCursor, entry.VisualOverkillWeight01);
                    WriteFloat4LittleEndian(payload, ref writeCursor, entry.Padding1);
                }

                return writeCursor == byteCount && NativeFaultDumpWriter.TryWriteAll(BiteTelemetryDumpRelativePath, payload, byteCount);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(FaunaKinematicsRuntime),
                    BiteTelemetryDumpPayloadLabel);
            }
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> payload, ref int cursor, int value)
        {
            WriteUInt32LittleEndian(payload, ref cursor, (uint)value);
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> payload, ref int cursor, uint value)
        {
            payload[cursor++] = (byte)value;
            payload[cursor++] = (byte)(value >> 8);
            payload[cursor++] = (byte)(value >> 16);
            payload[cursor++] = (byte)(value >> 24);
        }

        private static void WriteUInt64LittleEndian(NativeArray<byte> payload, ref int cursor, ulong value)
        {
            WriteUInt32LittleEndian(payload, ref cursor, (uint)value);
            WriteUInt32LittleEndian(payload, ref cursor, (uint)(value >> 32));
        }

        private static void WriteFloatLittleEndian(NativeArray<byte> payload, ref int cursor, float value)
        {
            WriteUInt32LittleEndian(payload, ref cursor, math.asuint(value));
        }

        private static void WriteDoubleLittleEndian(NativeArray<byte> payload, ref int cursor, double value)
        {
            WriteUInt64LittleEndian(payload, ref cursor, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
        }

        private static void WriteFloat3LittleEndian(NativeArray<byte> payload, ref int cursor, float3 value)
        {
            WriteFloatLittleEndian(payload, ref cursor, value.x);
            WriteFloatLittleEndian(payload, ref cursor, value.y);
            WriteFloatLittleEndian(payload, ref cursor, value.z);
        }

        private static void WriteFloat4LittleEndian(NativeArray<byte> payload, ref int cursor, float4 value)
        {
            WriteFloatLittleEndian(payload, ref cursor, value.x);
            WriteFloatLittleEndian(payload, ref cursor, value.y);
            WriteFloatLittleEndian(payload, ref cursor, value.z);
            WriteFloatLittleEndian(payload, ref cursor, value.w);
        }

        private static void WriteDouble3LittleEndian(NativeArray<byte> payload, ref int cursor, double3 value)
        {
            WriteDoubleLittleEndian(payload, ref cursor, value.x);
            WriteDoubleLittleEndian(payload, ref cursor, value.y);
            WriteDoubleLittleEndian(payload, ref cursor, value.z);
        }

        private void ResetConstraintIterationHysteresis()
        {
            _globalQualityWeight = ResolveGlobalQualityWeight();
            float curve = SmoothQualityCurve(_globalQualityWeight);
            _activeSegmentCount = math.clamp(
                (int)math.round(math.lerp(MinimumQualitySegments, math.clamp(_maximumQualitySegmentCount, MinimumQualitySegments, MaxSegments), curve)),
                MinimumQualitySegments,
                MaxSegments);
            _resolvedConstraintIterations = math.clamp(
                (int)math.round(math.lerp(1f, math.clamp(_maximumQualityConstraintIterations, 1, 10), curve)),
                1,
                10);
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

        private double3 ResolveOwnerAupDouble3()
        {
            AbsoluteUniversePosition ownerAup = ResolveOwnerAup();
            return ownerAup.ToAbsoluteDouble3();
        }

        private AbsoluteUniversePosition ResolveOwnerAup()
        {
            if (TryResolveOwnerAup(out AbsoluteUniversePosition ownerAup))
                return ownerAup;

            return TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup)
                ? originAup
                : default;
        }

        private bool TryResolveOwnerAup(out AbsoluteUniversePosition ownerAup)
        {
            if (_faunaBrain != null && _faunaBrain.TryResolveLogicAup(out ownerAup))
                return IsFiniteAup(in ownerAup);

            return TryResolveAupFromRuntimeOrigin(ResolveOwnerRuntimePosition(), out ownerAup);
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

        private uint ResolveRuntimeFlags()
        {
            uint flags = 0u;
            if (_enableSdfHugging)
                flags |= LeviathanTerrainIkConstants.RuntimeFlagSdfHugging;
            if (_enableMapMagicFallback)
                flags |= LeviathanTerrainIkConstants.RuntimeFlagTerrainFallback;
            return flags;
        }

        internal bool TrySelfAudit(out uint faultFlags)
        {
            faultFlags = 0u;
            if (LeviathanTerrainIkLayout.BoneDtoBytes != Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<LeviathanBoneDTO>())
                faultFlags |= 1u << 0;
            if (LeviathanTerrainIkLayout.BoneConstraintDtoBytes != Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<LeviathanBoneConstraintsDTO>())
                faultFlags |= 1u << 1;
            if (LeviathanTerrainIkLayout.ColliderProxyDtoBytes != Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<LeviathanCapsuleColliderDTO>())
                faultFlags |= 1u << 2;
            if (!LeviathanTerrainIkLayout.Validate())
                faultFlags |= 1u << 7;
            if (!ValidateLeviathanIkShaderGlobalsLayout())
                faultFlags |= 1u << 8;

            if (!TryResolveSpineVaultBuffers(
                    out _,
                    out _,
                    out NativeArray<LeviathanBoneDTO> leviathanBones,
                    out NativeArray<LeviathanTerrainIkTelemetryEntry> telemetryRing,
                    out NativeArray<int> telemetryCursor))
            {
                faultFlags |= 1u << 3;
                return false;
            }

            int count = math.min(_activeSegmentCount, leviathanBones.Length);
            for (int i = 0; i < count; i++)
            {
                float4x4 matrix = leviathanBones[i].LocalToWorld;
                if (!math.all(math.isfinite(matrix.c0)) ||
                    !math.all(math.isfinite(matrix.c1)) ||
                    !math.all(math.isfinite(matrix.c2)) ||
                    !math.all(math.isfinite(matrix.c3)))
                {
                    faultFlags |= 1u << 4;
                    break;
                }
            }

            if (!telemetryRing.IsCreated || telemetryRing.Length < LeviathanTerrainIkConstants.TelemetryCapacity)
                faultFlags |= 1u << 5;
            if (!telemetryCursor.IsCreated || telemetryCursor.Length < 1)
                faultFlags |= 1u << 6;

            return faultFlags == 0u;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || _solverScheduled)
                return;

            if (!TryResolveSpineVaultBuffers(
                    out _,
                    out _,
                    out NativeArray<LeviathanBoneDTO> leviathanBones,
                    out _,
                    out _) ||
                leviathanBones.Length <= 1)
            {
                return;
            }

            int count = math.min(_activeSegmentCount, leviathanBones.Length);
            bool activeIk = _strikeActive || _strikeSignalActive || _headLookTargetActive;
            bool tailSpringActive = math.isfinite(_tailWhipSecondsRemaining) && _tailWhipSecondsRemaining > 0f;
            int secondaryStart = math.max(1, count >> 1);
            for (int i = 1; i < count; i++)
            {
                float4 previous = leviathanBones[i - 1].LocalToWorld.c3;
                float4 current = leviathanBones[i].LocalToWorld.c3;
                if (!math.all(math.isfinite(previous)) || !math.all(math.isfinite(current)))
                    continue;

                Gizmos.color = activeIk && i <= 3
                    ? Color.red
                    : tailSpringActive && i >= secondaryStart
                        ? Color.blue
                        : Color.green;
                Gizmos.DrawLine(new Vector3(previous.x, previous.y, previous.z), new Vector3(current.x, current.y, current.z));
                float3 position = new float3(current.x, current.y, current.z);
                float3 forward = NormalizeSafe(new float3(leviathanBones[i].LocalToWorld.c2.x, leviathanBones[i].LocalToWorld.c2.y, leviathanBones[i].LocalToWorld.c2.z), ResolveOwnerForward());
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(ToVector3(position), ToVector3(forward * (_segmentLength * 0.35f)));
            }

            if (activeIk && count > 0)
            {
                float4 head = leviathanBones[0].LocalToWorld.c3;
                float3 target = _strikeActive || _strikeSignalActive
                    ? _strikeTargetWorldPosition
                    : _headLookTargetWorldPosition;
                if (math.all(math.isfinite(head)) && math.all(math.isfinite(target)))
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(new Vector3(head.x, head.y, head.z), ToVector3(target));
                }
            }
        }
#endif

        private float ResolveGlobalQualityWeight()
        {
#if UNITY_EDITOR
            if (_editorQualityOverrideActive)
                return SanitizeQualityWeight01(_editorQualityOverrideWeight);
#endif
            return SanitizeQualityWeight01(HomeostasisBrain.GlobalQualityWeight);
        }

        private static float SanitizeQualityWeight01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SmoothQualityCurve(float value)
        {
            float weight = SanitizeQualityWeight01(value);
            return weight * weight * (3f - 2f * weight);
        }

        private static float3 SanitizeFiniteInputFloat3(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        private static bool IsFiniteOriginShiftOffset(float3 offset)
        {
            float offsetLengthSq = math.lengthsq(offset);
            return math.all(math.isfinite(offset)) && math.isfinite(offsetLengthSq);
        }

        private static bool IsUsableOriginShiftOffset(float3 offset)
        {
            return IsFiniteOriginShiftOffset(offset) && math.lengthsq(offset) > OriginShiftUsableMagnitudeSq;
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static bool TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup)
        {
            originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return IsFiniteAup(in originAup);
        }

        private static bool TryResolveAupFromRuntimeOrigin(float3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.all(math.isfinite(runtimePosition)))
                return false;

            if (!TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            float3 runtimeLocal = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            return TryResolveAupFromRuntimeOrigin(runtimeLocal, out positionAup);
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
