using System;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Animation.FaunaProcedural
{
    [DisallowMultipleComponent]
    public sealed class ProceduralBoneBlenderRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IDisposable
    {
        private const int ProceduralBoneShaderGlobalsBytes = 32;
        private const string BlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_1403_PROCEDURAL_BONE.bin";
        private const string BlackBoxDumpPayloadLabel = "proceduralBoneTelemetryDumpPayload";

        private static readonly int ProceduralBoneMatricesId = Shader.PropertyToID("_H8ProceduralBoneMatrices");
        private static readonly int ProceduralBoneGlobalsId = Shader.PropertyToID("_H8ProceduralBoneGlobals");
        private const ulong JobBufferMutationGuardMask =
            (1UL << ((int)ProceduralBoneBlenderBufferIds.Rigs & 31)) |
            (1UL << ((int)ProceduralBoneBlenderBufferIds.FrameInputs & 31)) |
            (1UL << ((int)ProceduralBoneBlenderBufferIds.ParentIndices & 31)) |
            (1UL << ((int)ProceduralBoneBlenderBufferIds.BindPoses & 31)) |
            (1UL << ((int)ProceduralBoneBlenderBufferIds.BoneStates & 31)) |
            (1UL << ((int)ProceduralBoneBlenderBufferIds.BoneMatrices & 31)) |
            (1UL << ((int)ProceduralBoneBlenderBufferIds.FrameStats & 31)) |
            (1UL << ((int)ProceduralBoneBlenderBufferIds.TelemetryRing & 31)) |
            (1UL << ((int)ProceduralBoneBlenderBufferIds.TelemetryCursor & 31)) |
            (1UL << ((int)ProceduralBoneBlenderBufferIds.Tuning & 31)) |
            (1UL << ((int)ProceduralBoneBlenderBufferIds.MockAiSignals & 31));
        private const int ProceduralBoneGlobalsScalars0Offset = 0;
        private const int ProceduralBoneGlobalsScalars1Offset = 16;

        [SerializeField, Range(1, ProceduralBoneBlenderConstants.DefaultSkeletonCapacity)]
        private int _skeletonCapacity = ProceduralBoneBlenderConstants.DefaultSkeletonCapacity;

        [SerializeField, Range(ProceduralBoneBlenderConstants.EmergencyMockBoneCount, ProceduralBoneBlenderConstants.DefaultBoneCapacity)]
        private int _boneCapacity = ProceduralBoneBlenderConstants.DefaultBoneCapacity;

        [SerializeField] private bool _seedEmergencyMockRig;

        private IDataVault _dataVault;
        private VaultGenerationHandle<ProceduralBoneRigDTO> _rigsHandle;
        private VaultGenerationHandle<ProceduralBoneFrameInputDTO> _frameInputsHandle;
        private VaultGenerationHandle<int> _parentIndicesHandle;
        private VaultGenerationHandle<float4x4> _bindPosesHandle;
        private VaultGenerationHandle<BoneStateDTO> _boneStatesHandle;
        private VaultGenerationHandle<float4x4> _boneMatricesHandle;
        private VaultGenerationHandle<ProceduralBoneFrameStatsDTO> _frameStatsHandle;
        private VaultGenerationHandle<ProceduralBoneTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<ProceduralBoneRigTuningDTO> _tuningHandle;
        private VaultGenerationHandle<MockAiVelocitySignal> _mockAiSignalsHandle;

        private GraphicsBuffer _matrixBufferA;
        private GraphicsBuffer _matrixBufferB;
        private GraphicsBuffer _shaderGlobalsBufferA;
        private GraphicsBuffer _shaderGlobalsBufferB;
        private GraphicsBuffer _activeShaderGlobalsBuffer;
        private GraphicsBuffer _publishedSkinningMatrixBuffer;
        private JobHandle _pendingHandle;
        private float _simulationTime;
        private float _accumulatedDelta;
        private float _lastQuality = 1f;
        private uint _frameCounter;
        private uint _latestMatrixStateHash;
        private uint _uploadedMatrixStateHash;
        private int _gpuUploadBufferIndex;
        private int _shaderGlobalsUploadBufferIndex;
        private int _activeMatrixUploadCount;
        private int _activeSkeletonCount;
        private int _uploadedMatrixCount;
        private int _publishedSkinningMatrixCount = -1;
        private int _uploadedSkeletonCount;
        private IDataVault _jobBufferGuardVault;
        private bool _jobBufferGuardHeld;
        private float _uploadedQuality = -1f;
        private bool _solverScheduled;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _gpuUploadDirty;
        private bool _gpuShaderConstantsDirty;
        private bool _gpuBufferDataValid;
        private bool _globalGpuSkinningPublished;
        private bool _supportsConstantBufferBinding;
        private bool _disposed;
        private bool _dumpedFault;
        private readonly ProceduralBoneTelemetryEntry[] _blackBoxDumpSnapshot = new ProceduralBoneTelemetryEntry[ProceduralBoneBlenderConstants.TelemetryCapacity];
        private int _blackBoxDumpSnapshotCursor;
        private int _blackBoxDumpSnapshotCount;
        private int _blackBoxDumpInFlight;
        private uint _blackBoxDumpHash;

        private static ProceduralBoneBlenderRuntime _activeRuntimeInstance;

        [StructLayout(LayoutKind.Explicit, Size = ProceduralBoneShaderGlobalsBytes)]
        private struct ProceduralBoneShaderGlobalsDTO
        {
            [FieldOffset(0)] public float4 Scalars0;
            [FieldOffset(16)] public float4 Scalars1;
        }

        private static bool ValidateProceduralBoneShaderGlobalsLayout()
        {
            return UnsafeUtility.SizeOf<ProceduralBoneShaderGlobalsDTO>() == ProceduralBoneShaderGlobalsBytes &&
                   ProceduralBoneGlobalsScalars0Offset == 0 &&
                   ProceduralBoneGlobalsScalars1Offset == 16;
        }

        public static bool TryGetActiveRuntimeInstance(out ProceduralBoneBlenderRuntime runtime)
        {
            runtime = _activeRuntimeInstance;
            return runtime != null && !runtime._disposed;
        }

        public bool TryGetProceduralBoneGraphicsBuffer(out GraphicsBuffer buffer, out int matrixCount)
        {
            matrixCount = _activeMatrixUploadCount;
            GraphicsBuffer candidate = _gpuUploadBufferIndex == 0 ? _matrixBufferB : _matrixBufferA;
            if (!_disposed &&
                !_gpuUploadDirty &&
                _gpuBufferDataValid &&
                HasValidGraphicsBuffer(candidate, matrixCount))
            {
                buffer = candidate;
                return matrixCount > 0;
            }

            buffer = null;
            matrixCount = 0;
            return false;
        }

        public bool TryResolveTuningForEditor(out NativeArray<ProceduralBoneRigTuningDTO>.ReadOnly tuning)
        {
            tuning = default;
            if (!TryResolveTuningMutable(out NativeArray<ProceduralBoneRigTuningDTO> mutableTuning))
                return false;

            tuning = mutableTuning.AsReadOnly();
            return true;
        }

        public bool TryApplyEditorTuning(in ProceduralBoneRigTuningDTO tuning)
        {
            if (!OpenOrAcquireVaultBuffersForOwnerRoute())
                return false;

            if (!TryResolveTuningMutable(out NativeArray<ProceduralBoneRigTuningDTO> mutableTuning))
                return false;

            mutableTuning[0] = ProceduralBoneSanitizer.SanitizeTuning(tuning);
            return true;
        }

        private bool TryResolveTuningMutable(out NativeArray<ProceduralBoneRigTuningDTO> tuning)
        {
            tuning = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!IsOwnedVaultHandle(in _tuningHandle, ProceduralBoneBlenderBufferIds.Tuning))
                return false;

            return TryResolveOwnedVaultBuffer(
                vault,
                ProceduralBoneBlenderBufferIds.Tuning,
                in _tuningHandle,
                ProceduralBoneBlenderConstants.TuningCapacity,
                out tuning);
        }

        public bool TryResolveMatricesForEditor(
            out NativeArray<float4x4>.ReadOnly matrices,
            out NativeArray<int>.ReadOnly parentIndices,
            out int matrixCount)
        {
            matrices = default;
            parentIndices = default;
            matrixCount = 0;
            if (_solverScheduled)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.BoneMatrices, in _boneMatricesHandle, 1, out NativeArray<float4x4> mutableMatrices) ||
                !TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.ParentIndices, in _parentIndicesHandle, 1, out NativeArray<int> mutableParentIndices))
            {
                return false;
            }

            matrices = mutableMatrices.AsReadOnly();
            parentIndices = mutableParentIndices.AsReadOnly();
            matrixCount = math.min(math.min(_activeMatrixUploadCount, matrices.Length), parentIndices.Length);
            return matrixCount > 0;
        }

#if UNITY_EDITOR
        public bool TryApplyCsvProfile(string csvText)
        {
            if (csvText == null ||
                csvText.Length == 0 ||
                !OpenOrAcquireVaultBuffersForOwnerRoute() ||
                !TryResolveTuningMutable(out NativeArray<ProceduralBoneRigTuningDTO> tuning))
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.Rigs, in _rigsHandle, 1, out NativeArray<ProceduralBoneRigDTO> rigs))
                return false;

            if (rigs[0].BoneCount <= 0)
                GenerateEmergencyMockRigs();

            if (!TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.Rigs, in _rigsHandle, 1, out rigs))
                return false;

            ProceduralBoneRigTuningDTO dto = tuning[0];
            ProceduralBoneRigDTO rig = rigs[0];
            bool result = ProceduralBoneProfileCsvParser.TryApply(csvText.AsSpan(), ref dto, ref rig);
            tuning[0] = dto;
            rigs[0] = rig;
            return result;
        }
#endif

        private void Awake()
        {
            if (_activeRuntimeInstance == null)
                _activeRuntimeInstance = this;

            RefreshColdDependencies();
            if (OpenOrAcquireVaultBuffersForOwnerRoute())
                EnsureGraphicsBuffers();
            if (ShouldSeedEmergencyMockRig())
                GenerateEmergencyMockRigs();
        }

        private void OnEnable()
        {
            if (_disposed || !Application.isPlaying)
                return;

            CompletePendingSolverForTeardown();
            RefreshColdDependencies();
            if (OpenOrAcquireVaultBuffersForOwnerRoute())
                EnsureGraphicsBuffers();
            if (ShouldSeedEmergencyMockRig())
                GenerateEmergencyMockRigs();
            TryRegister();
        }

        private void OnDisable()
        {
            if (_disposed || !Application.isPlaying)
                return;

            TryUnregister();
            CompletePendingSolverForTeardown();
            ClearGpuSkinningBinding();
            ReleaseVaultHandles();
            ClearHandles();
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
            if (ReferenceEquals(_activeRuntimeInstance, this))
                _activeRuntimeInstance = null;

            TryUnregister();
            CompletePendingSolverForTeardown();
            ClearGpuSkinningBinding();
            ReleaseVaultHandles();
            ReleaseGraphicsBuffers();
            ClearHandles();
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

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            ProceduralBoneRigTuningDTO tuning = ProceduralBoneRigTuningDTO.Default();
            float globalQuality = _lastQuality;

            if (!TryResolveOwnedVaultBuffer(
                    vault,
                    ProceduralBoneBlenderBufferIds.Tuning,
                    in _tuningHandle,
                    ProceduralBoneBlenderConstants.TuningCapacity,
                    out NativeArray<ProceduralBoneRigTuningDTO> tuningRead))
            {
                return;
            }

            tuning = ProceduralBoneSanitizer.SanitizeTuning(tuningRead[0]);
            globalQuality = math.saturate(math.select(_lastQuality, tuning.GlobalQualityWeight, math.isfinite(tuning.GlobalQualityWeight)));
            tuning.GlobalQualityWeight = globalQuality;
            tuning.ActiveSkeletonCount = math.clamp(tuning.ActiveSkeletonCount, 0, _skeletonCapacity);
            _lastQuality = globalQuality;

            float safeDelta = math.clamp(deltaTime, ProceduralBoneBlenderConstants.MinDeltaTime, ProceduralBoneBlenderConstants.MaxDeltaTime);
            _accumulatedDelta = math.min(_accumulatedDelta + safeDelta, ProceduralBoneBlenderConstants.MaxDeltaTime);
            float updateHz = math.lerp(tuning.LowQualityUpdateHz, tuning.HighQualityUpdateHz, ProceduralBoneMath.Smooth01(globalQuality));
            float updateInterval = math.rcp(math.max(1f, updateHz));
            if (_accumulatedDelta + 0.00001f < updateInterval && _frameCounter != 0u)
                return;

            int activeSkeletons = tuning.ActiveSkeletonCount;
            if (activeSkeletons <= 0)
                return;

            NativeArray<ProceduralBoneRigDTO> rigs;
            NativeArray<ProceduralBoneFrameInputDTO> inputs;
            NativeArray<int> parents;
            NativeArray<float4x4> bindPoses;
            NativeArray<BoneStateDTO> boneStates;
            NativeArray<float4x4> matrices;
            NativeArray<ProceduralBoneFrameStatsDTO> stats;
            NativeArray<ProceduralBoneTelemetryEntry> telemetry;
            NativeArray<int> cursor;
            NativeArray<ProceduralBoneRigTuningDTO> tuningArray;
            NativeArray<MockAiVelocitySignal> mockSignals;

            if (!TryGuardJobBuffersAndResolveBuffers(
                    vault,
                    out rigs,
                    out inputs,
                    out parents,
                    out bindPoses,
                    out boneStates,
                    out matrices,
                    out stats,
                    out telemetry,
                    out cursor,
                    out tuningArray,
                    out mockSignals))
                return;

            activeSkeletons = math.min(tuning.ActiveSkeletonCount, math.min(rigs.Length, inputs.Length));
            if (activeSkeletons <= 0)
            {
                ReleaseJobBufferPins();
                return;
            }

            float solveDelta = _accumulatedDelta;
            _accumulatedDelta = 0f;
            _simulationTime += solveDelta;
            uint frame = _frameCounter + 1u;

            bool scheduled = false;
            try
            {
                MockAiVelocitySignalJob mockJob = default;
                mockJob.Signals = mockSignals;
                mockJob.SectorHash = tuning.SectorHash;
                mockJob.SimulationFrame = frame;
                mockJob.GlobalQualityWeight = globalQuality;
                JobHandle handle = mockJob.Schedule(activeSkeletons, 64);

                ProceduralBoneSolveJob solveJob = default;
                solveJob.Rigs = rigs;
                solveJob.Inputs = inputs;
                solveJob.ParentIndices = parents;
                solveJob.BindPoses = bindPoses;
                solveJob.BoneStates = boneStates;
                solveJob.BoneMatrices = matrices;
                solveJob.Stats = stats;
                solveJob.MockSignals = mockSignals;
                solveJob.Tuning = tuningArray;
                solveJob.GlobalQualityWeight = globalQuality;
                solveJob.DeltaTime = solveDelta;
                solveJob.SimulationTime = _simulationTime;
                solveJob.SimulationFrame = frame;
                handle = solveJob.Schedule(activeSkeletons, 16, handle);

                ProceduralBoneTelemetryReduceJob telemetryJob = default;
                telemetryJob.Stats = stats;
                telemetryJob.TelemetryRing = telemetry;
                telemetryJob.TelemetryCursor = cursor;
                telemetryJob.ActiveSkeletonCount = activeSkeletons;
                telemetryJob.SimulationFrame = frame;
                telemetryJob.GlobalQualityWeight = globalQuality;
                _pendingHandle = telemetryJob.Schedule(handle);
                H8Memory.RegisterActiveJob(SystemID.AnimationFauna, _pendingHandle);
                _solverScheduled = true;
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                    ReleaseJobBufferPins();
            }
        }

        public void LateFrameTick()
        {
            if (_disposed)
                return;

            if (_solverScheduled && !TryFinalizePendingSolverNoWait())
                return;

            if (_gpuUploadDirty)
                _gpuUploadDirty = !UploadMatricesToGpu();
            else if (_gpuShaderConstantsDirty)
                _gpuShaderConstantsDirty = !PublishCurrentGpuSkinningBinding();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault currentVault = currentService is IDataVault nextVault ? nextVault : null;
            IDataVault previousVault = previousService is IDataVault oldVault ? oldVault : null;
            BindDataVaultForLifecycle(currentVault, previousVault);
            if (OpenOrAcquireVaultBuffersForOwnerRoute())
                EnsureGraphicsBuffers();
            if (ShouldSeedEmergencyMockRig())
                GenerateEmergencyMockRigs();
        }

        public bool GenerateEmergencyMockRigs()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return false;
#endif
            IDataVault vault = _dataVault;
            if (vault == null || !OpenOrAcquireVaultBuffersForOwnerRoute())
                return false;

            if (!TryResolveRuntimeBuffers(
                    vault,
                    out NativeArray<ProceduralBoneRigDTO> rigs,
                    out NativeArray<ProceduralBoneFrameInputDTO> inputs,
                    out NativeArray<int> parents,
                    out NativeArray<float4x4> bindPoses,
                    out NativeArray<BoneStateDTO> boneStates,
                    out NativeArray<float4x4> matrices,
                    out _,
                    out NativeArray<ProceduralBoneTelemetryEntry> telemetry,
                    out NativeArray<int> cursor,
                    out NativeArray<ProceduralBoneRigTuningDTO> tuningArray,
                    out NativeArray<MockAiVelocitySignal> mockSignals))
            {
                return false;
            }

            if (rigs.Length <= 0 ||
                inputs.Length <= 0 ||
                parents.Length < ProceduralBoneBlenderConstants.EmergencyMockBoneCount ||
                bindPoses.Length < ProceduralBoneBlenderConstants.EmergencyMockBoneCount ||
                boneStates.Length < ProceduralBoneBlenderConstants.EmergencyMockBoneCount ||
                matrices.Length < ProceduralBoneBlenderConstants.EmergencyMockBoneCount)
            {
                return false;
            }

            ProceduralBoneRigTuningDTO tuning = ProceduralBoneRigTuningDTO.Default();
            tuning.GlobalQualityWeight = ResolveGlobalQualityWeight(vault);
            tuning.ActiveSkeletonCount = 1;
            tuningArray[0] = tuning;

            ProceduralBoneRigDTO rig = default;
            rig.SkeletonHash = 0x53484E68u;
            rig.Flags = ProceduralBoneBlenderConstants.RigFlagEmergencyMock |
                        ProceduralBoneBlenderConstants.RigFlagVisible |
                        ProceduralBoneBlenderConstants.RigFlagHasJaw;
            rig.BoneStart = 0;
            rig.BoneCount = ProceduralBoneBlenderConstants.EmergencyMockBoneCount;
            rig.PrimaryBoneCount = 2;
            rig.JawBoneIndex = 4;
            rig.RootBoneIndex = 0;
            rig.ReservedIndex = 0;
            rig.BaseScale = 1f;
            rig.BoneLengthMeters = 1.25f;
            rig.BaseWaveSpeed = 1.35f;
            rig.VelocityWaveMultiplier = 0.22f;
            rig.BaseAmplitudeRadians = 0.28f;
            rig.PhaseOffset = 0.72f;
            rig.DampingRatio = 0.82f;
            rig.NaturalFrequencyHz = 5.5f;
            rig.TraumaSeconds = 0f;
            rig.WaveSpeedState = rig.BaseWaveSpeed;
            rig.WaveSpeedVelocityState = 0f;
            rig.AmplitudeState = rig.BaseAmplitudeRadians;
            rig.AmplitudeVelocityState = 0f;
            rig.StableSeed = 0x68A11B0Du;
            rigs[0] = rig;

            ProceduralBoneFrameInputDTO input = default;
            input.RootLocalPosition = float3.zero;
            input.Visible01 = 1f;
            input.RootRotation = quaternion.identity;
            input.VelocityLocal = ProceduralBoneMath.Float3(0f, 0f, 2.4f);
            input.GlobalQualityWeight = tuning.GlobalQualityWeight;
            input.JawTargetLocal = ProceduralBoneMath.Float3(0.35f, 0.25f, 4.5f);
            input.JawOpen01 = 0.6f;
            input.SimulationTickDelta = 1f / 60f;
            input.SimulationTime = 0f;
            input.BaseScaleOverride = 1f;
            input.Flags = ProceduralBoneBlenderConstants.InputFlagVisible;
            inputs[0] = input;

            float segment = rig.BoneLengthMeters;
            for (int i = 0; i < ProceduralBoneBlenderConstants.EmergencyMockBoneCount; i++)
            {
                parents[i] = i == 0 ? -1 : i - 1;
                bindPoses[i] = i == 0 ? float4x4.identity : float4x4.Translate(ProceduralBoneMath.Float3(0f, 0f, segment));
                matrices[i] = i == 0 ? float4x4.identity : float4x4.Translate(ProceduralBoneMath.Float3(0f, 0f, segment * i));

                BoneStateDTO state = default;
                state.LocalMatrix = bindPoses[i];
                state.Phase = i * rig.PhaseOffset;
                state.BoneHash = rig.SkeletonHash ^ (uint)i * 0x9E3779B9u;
                state._pad0 = 0UL;
                boneStates[i] = state;
            }

            if (mockSignals.Length > 0)
            {
                MockAiVelocitySignal signal = default;
                signal.VelocityLocal = input.VelocityLocal;
                signal.Weight01 = 1f;
                signal.IkTargetLocal = input.JawTargetLocal;
                signal.JawOpen01 = input.JawOpen01;
                signal.EntityHash = rig.SkeletonHash;
                signal.SectorHash = tuning.SectorHash;
                signal.SimulationFrame = _frameCounter;
                signal.Flags = ProceduralBoneBlenderConstants.TelemetryFlagMockSignal;
                signal.NoisePhase = 0f;
                signal.SpeedHint = 2.4f;
                signal._pad0 = 0UL;
                mockSignals[0] = signal;
            }

            if (cursor.Length > 0)
                cursor[0] = 0;
            int telemetryCount = math.min(telemetry.Length, ProceduralBoneBlenderConstants.TelemetryCapacity);
            for (int i = 0; i < telemetryCount; i++)
                telemetry[i] = default;

            _activeMatrixUploadCount = ProceduralBoneBlenderConstants.EmergencyMockBoneCount;
            _activeSkeletonCount = 1;
            _latestMatrixStateHash = 0u;
            _uploadedMatrixStateHash = 0u;
            _uploadedMatrixCount = 0;
            _uploadedSkeletonCount = 0;
            _uploadedQuality = -1f;
            _gpuUploadDirty = true;
            _gpuShaderConstantsDirty = true;
            _gpuBufferDataValid = false;
            return true;
        }

        private bool ShouldSeedEmergencyMockRig()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return _seedEmergencyMockRig;
#else
            return false;
#endif
        }

        private void RefreshColdDependencies()
        {
            _supportsConstantBufferBinding = SystemInfo.supportsSetConstantBuffer;
            BindDataVaultForLifecycle(GlobalRegistry.DataVault, null);
        }

        private void BindDataVaultForLifecycle(IDataVault currentVault, IDataVault releaseVaultOverride)
        {
            if (ReferenceEquals(_dataVault, currentVault))
                return;

            CompletePendingSolverForTeardown();
            ReleaseVaultHandles(_dataVault ?? releaseVaultOverride);
            ClearHandles();
            _dataVault = currentVault;
            _activeSkeletonCount = 0;
            _activeMatrixUploadCount = 0;
            _latestMatrixStateHash = 0u;
            _uploadedMatrixStateHash = 0u;
            _uploadedMatrixCount = 0;
            _uploadedSkeletonCount = 0;
            _uploadedQuality = -1f;
            _gpuUploadDirty = true;
            _gpuShaderConstantsDirty = true;
            _gpuBufferDataValid = false;
        }

        private bool OpenOrAcquireVaultBuffersForOwnerRoute()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !ProceduralBoneBlenderLayout.Validate())
                return false;

            if (_dataVault != null && !ReferenceEquals(_dataVault, vault))
            {
                ReleaseVaultHandles(_dataVault);
                ClearHandles();
            }

            _dataVault = vault;
            int skeletonCapacity = math.clamp(_skeletonCapacity, 1, ProceduralBoneBlenderConstants.DefaultSkeletonCapacity);
            int boneCapacity = math.clamp(_boneCapacity, ProceduralBoneBlenderConstants.EmergencyMockBoneCount, ProceduralBoneBlenderConstants.DefaultBoneCapacity);
            bool resolved = OpenOrAcquireVaultBufferForOwnerRoute(
                vault,
                ProceduralBoneBlenderBufferIds.Rigs,
                skeletonCapacity,
                ref _rigsHandle,
                out _) &&
            OpenOrAcquireVaultBufferForOwnerRoute(
                vault,
                ProceduralBoneBlenderBufferIds.FrameInputs,
                skeletonCapacity,
                ref _frameInputsHandle,
                out _) &&
            OpenOrAcquireVaultBufferForOwnerRoute(
                vault,
                ProceduralBoneBlenderBufferIds.ParentIndices,
                boneCapacity,
                ref _parentIndicesHandle,
                out _) &&
            OpenOrAcquireVaultBufferForOwnerRoute(
                vault,
                ProceduralBoneBlenderBufferIds.BindPoses,
                boneCapacity,
                ref _bindPosesHandle,
                out _) &&
            OpenOrAcquireVaultBufferForOwnerRoute(
                vault,
                ProceduralBoneBlenderBufferIds.BoneStates,
                boneCapacity,
                ref _boneStatesHandle,
                out _) &&
            OpenOrAcquireVaultBufferForOwnerRoute(
                vault,
                ProceduralBoneBlenderBufferIds.BoneMatrices,
                boneCapacity,
                ref _boneMatricesHandle,
                out _) &&
            OpenOrAcquireVaultBufferForOwnerRoute(
                vault,
                ProceduralBoneBlenderBufferIds.FrameStats,
                skeletonCapacity,
                ref _frameStatsHandle,
                out _) &&
            OpenOrAcquireVaultBufferForOwnerRoute(
                vault,
                ProceduralBoneBlenderBufferIds.TelemetryRing,
                ProceduralBoneBlenderConstants.TelemetryCapacity,
                ref _telemetryRingHandle,
                out _,
                NativeArrayOptions.ClearMemory) &&
            OpenOrAcquireVaultBufferForOwnerRoute(
                vault,
                ProceduralBoneBlenderBufferIds.TelemetryCursor,
                1,
                ref _telemetryCursorHandle,
                out _,
                NativeArrayOptions.ClearMemory) &&
            OpenOrAcquireVaultBufferForOwnerRoute(
                vault,
                ProceduralBoneBlenderBufferIds.Tuning,
                ProceduralBoneBlenderConstants.TuningCapacity,
                ref _tuningHandle,
                out _,
                NativeArrayOptions.ClearMemory) &&
            OpenOrAcquireVaultBufferForOwnerRoute(
                vault,
                ProceduralBoneBlenderBufferIds.MockAiSignals,
                skeletonCapacity,
                ref _mockAiSignalsHandle,
                out _);

            if (!resolved)
                return false;

            if (TryResolveOwnedVaultBuffer(
                    vault,
                    ProceduralBoneBlenderBufferIds.Tuning,
                    in _tuningHandle,
                    ProceduralBoneBlenderConstants.TuningCapacity,
                    out NativeArray<ProceduralBoneRigTuningDTO> tuning) &&
                tuning.Length > 0)
            {
                tuning[0] = ProceduralBoneSanitizer.SanitizeTuning(tuning[0].HighQualityUpdateHz > 0f ? tuning[0] : ProceduralBoneRigTuningDTO.Default());
            }

            return true;
        }

        private static bool IsVaultHandleForBuffer<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId && handle.Generation != 0u;
        }

        private static bool TryResolveOwnedVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   IsOwnedVaultHandle(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   !vault.IsCompactionFenceActive &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool OpenOrAcquireVaultBufferForOwnerRoute<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where T : struct
        {
            buffer = default;
            if (IsOwnedVaultHandle(in handle, bufferId) &&
                TryResolveOwnedVaultBuffer(vault, bufferId, in handle, requiredLength, out buffer))
            {
                return true;
            }

            if (vault == null)
                return false;

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.AnimationFauna,
                options);
            if (!TryResolveOwnedVaultBuffer(vault, bufferId, in acquired, requiredLength, out buffer))
            {
                if (IsOwnedVaultHandle(in acquired, bufferId))
                    vault.ReleaseBuffer(in acquired);

                return false;
            }

            handle = acquired;
            return true;
        }

        private bool TryResolveRuntimeBuffers(
            IDataVault vault,
            out NativeArray<ProceduralBoneRigDTO> rigs,
            out NativeArray<ProceduralBoneFrameInputDTO> inputs,
            out NativeArray<int> parents,
            out NativeArray<float4x4> bindPoses,
            out NativeArray<BoneStateDTO> boneStates,
            out NativeArray<float4x4> matrices,
            out NativeArray<ProceduralBoneFrameStatsDTO> stats,
            out NativeArray<ProceduralBoneTelemetryEntry> telemetry,
            out NativeArray<int> cursor,
            out NativeArray<ProceduralBoneRigTuningDTO> tuning,
            out NativeArray<MockAiVelocitySignal> mockSignals)
        {
            rigs = default;
            inputs = default;
            parents = default;
            bindPoses = default;
            boneStates = default;
            matrices = default;
            stats = default;
            telemetry = default;
            cursor = default;
            tuning = default;
            mockSignals = default;

            if (vault == null)
                return false;

            int skeletonCapacity = math.clamp(_skeletonCapacity, 1, ProceduralBoneBlenderConstants.DefaultSkeletonCapacity);
            int boneCapacity = math.clamp(_boneCapacity, ProceduralBoneBlenderConstants.EmergencyMockBoneCount, ProceduralBoneBlenderConstants.DefaultBoneCapacity);
            return TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.Rigs, in _rigsHandle, skeletonCapacity, out rigs) &&
                   TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.FrameInputs, in _frameInputsHandle, skeletonCapacity, out inputs) &&
                   TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.ParentIndices, in _parentIndicesHandle, boneCapacity, out parents) &&
                   TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.BindPoses, in _bindPosesHandle, boneCapacity, out bindPoses) &&
                   TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.BoneStates, in _boneStatesHandle, boneCapacity, out boneStates) &&
                   TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.BoneMatrices, in _boneMatricesHandle, boneCapacity, out matrices) &&
                   TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.FrameStats, in _frameStatsHandle, skeletonCapacity, out stats) &&
                   TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.TelemetryRing, in _telemetryRingHandle, ProceduralBoneBlenderConstants.TelemetryCapacity, out telemetry) &&
                   TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.TelemetryCursor, in _telemetryCursorHandle, 1, out cursor) &&
                   TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.Tuning, in _tuningHandle, ProceduralBoneBlenderConstants.TuningCapacity, out tuning) &&
                   TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.MockAiSignals, in _mockAiSignalsHandle, skeletonCapacity, out mockSignals) &&
                   tuning.Length >= ProceduralBoneBlenderConstants.TuningCapacity &&
                   telemetry.Length >= ProceduralBoneBlenderConstants.TelemetryCapacity &&
                   cursor.Length >= 1;
        }

        private bool TryFinalizePendingSolverNoWait()
        {
            if (!_solverScheduled)
                return true;

            if (!_pendingHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingHandle))
                return false;

            return FinishPendingSolverCompletion();
        }

        private bool CompletePendingSolverForTeardown()
        {
            if (!_solverScheduled)
                return true;

            DispatcherJobFence.BeginLateFrameSwapWindow();
            try
            {
                if (!DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true))
                    return false;
            }
            finally
            {
                DispatcherJobFence.EndLateFrameSwapWindow();
            }

            return FinishPendingSolverCompletion();
        }

        private bool FinishPendingSolverCompletion()
        {
            _solverScheduled = false;
            bool shouldDumpFault = false;
            try
            {
                _frameCounter++;
                RefreshLatestTelemetrySnapshot();
                _gpuUploadDirty = ShouldUploadMatrices();
                shouldDumpFault = !_dumpedFault && LatestTelemetryHasInvalidFlag();
            }
            finally
            {
                ReleaseJobBufferPins();
            }

            if (shouldDumpFault)
                DumpBlackBoxOnce();

            return true;
        }

        private bool TryGuardJobBuffersAndResolveBuffers(
            IDataVault vault,
            out NativeArray<ProceduralBoneRigDTO> rigs,
            out NativeArray<ProceduralBoneFrameInputDTO> inputs,
            out NativeArray<int> parents,
            out NativeArray<float4x4> bindPoses,
            out NativeArray<BoneStateDTO> boneStates,
            out NativeArray<float4x4> matrices,
            out NativeArray<ProceduralBoneFrameStatsDTO> stats,
            out NativeArray<ProceduralBoneTelemetryEntry> telemetry,
            out NativeArray<int> cursor,
            out NativeArray<ProceduralBoneRigTuningDTO> tuning,
            out NativeArray<MockAiVelocitySignal> mockSignals)
        {
            rigs = default;
            inputs = default;
            parents = default;
            bindPoses = default;
            boneStates = default;
            matrices = default;
            stats = default;
            telemetry = default;
            cursor = default;
            tuning = default;
            mockSignals = default;
            _jobBufferGuardVault = null;
            _jobBufferGuardHeld = false;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                return false;
            }

            bool resolved = false;
            try
            {
                if (!TryAcquireJobBufferMutationGuard(vault))
                {
                    return false;
                }

                if (!TryResolveRuntimeBuffers(
                        vault,
                        out rigs,
                        out inputs,
                        out parents,
                        out bindPoses,
                        out boneStates,
                        out matrices,
                        out stats,
                        out telemetry,
                        out cursor,
                        out tuning,
                        out mockSignals))
                {
                    return false;
                }

                resolved = true;
                return true;
            }
            finally
            {
                if (!resolved)
                    ReleaseJobBufferPins();
            }
        }

        private bool TryAcquireJobBufferMutationGuard(IDataVault vault)
        {
            if (_jobBufferGuardHeld)
                return ReferenceEquals(_jobBufferGuardVault, vault);

            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(JobBufferMutationGuardMask))
            {
                return false;
            }

            _jobBufferGuardVault = vault;
            _jobBufferGuardHeld = true;
            return true;
        }

        private void ReleaseJobBufferPins()
        {
            IDataVault vault = _jobBufferGuardVault;
            bool held = _jobBufferGuardHeld;
            _jobBufferGuardVault = null;
            _jobBufferGuardHeld = false;
            if (held)
                vault?.ReleaseMutationGuard(JobBufferMutationGuardMask);
        }

        private float ResolveGlobalQualityWeight(IDataVault vault)
        {
            if (TryResolveOwnedVaultBuffer(
                    vault,
                    ProceduralBoneBlenderBufferIds.Tuning,
                    in _tuningHandle,
                    ProceduralBoneBlenderConstants.TuningCapacity,
                    out NativeArray<ProceduralBoneRigTuningDTO> tuning))
            {
                if (tuning.Length > 0 && math.isfinite(tuning[0].GlobalQualityWeight))
                    return math.saturate(tuning[0].GlobalQualityWeight);
            }

            return math.saturate(math.select(1f, _lastQuality, math.isfinite(_lastQuality)));
        }

        private void RefreshLatestTelemetrySnapshot()
        {
            _activeMatrixUploadCount = 0;
            _activeSkeletonCount = 0;
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (!TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.TelemetryRing, in _telemetryRingHandle, 1, out NativeArray<ProceduralBoneTelemetryEntry> telemetry) ||
                !TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.TelemetryCursor, in _telemetryCursorHandle, 1, out NativeArray<int> cursor) ||
                telemetry.Length <= 0 ||
                cursor[0] <= 0)
                return;

            int index = ProceduralBoneMath.PositiveModulo(cursor[0] - 1, telemetry.Length);
            ProceduralBoneTelemetryEntry entry = telemetry[index];
            int matrixCount = math.clamp(entry.MatrixUploadCount, 0, _boneCapacity);
            int skeletonCount = math.max(0, entry.ActiveSkeletons);
            float quality = math.saturate(math.select(_lastQuality, entry.GlobalQualityWeight, math.isfinite(entry.GlobalQualityWeight)));
            _gpuShaderConstantsDirty |=
                matrixCount != _uploadedMatrixCount ||
                skeletonCount != _uploadedSkeletonCount ||
                math.abs(quality - _uploadedQuality) > 0.0001f;
            _activeMatrixUploadCount = matrixCount;
            _activeSkeletonCount = skeletonCount;
            _latestMatrixStateHash = entry.StateHash;
            _lastQuality = quality;
        }

        private bool ShouldUploadMatrices()
        {
            return _activeMatrixUploadCount > 0 &&
                   (!_gpuBufferDataValid ||
                    _activeMatrixUploadCount != _uploadedMatrixCount ||
                    _latestMatrixStateHash != _uploadedMatrixStateHash);
        }

        private bool LatestTelemetryHasInvalidFlag()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.TelemetryRing, in _telemetryRingHandle, 1, out NativeArray<ProceduralBoneTelemetryEntry> telemetry) ||
                !TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.TelemetryCursor, in _telemetryCursorHandle, 1, out NativeArray<int> cursor) ||
                telemetry.Length <= 0 ||
                cursor[0] <= 0)
                return false;

            int index = ProceduralBoneMath.PositiveModulo(cursor[0] - 1, telemetry.Length);
            return (telemetry[index].Flags & ProceduralBoneBlenderConstants.TelemetryFlagInvalid) != 0u;
        }

        private void DumpBlackBoxOnce()
        {
            if (_dumpedFault || Volatile.Read(ref _blackBoxDumpInFlight) != 0)
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            Volatile.Write(ref _blackBoxDumpInFlight, 1);
            try
            {
                if (!TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.TelemetryRing, in _telemetryRingHandle, 1, out NativeArray<ProceduralBoneTelemetryEntry> telemetry) ||
                    !TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.TelemetryCursor, in _telemetryCursorHandle, 1, out NativeArray<int> cursor))
                {
                    return;
                }

                if (!TryStageBlackBoxDumpSnapshot(telemetry, cursor))
                    return;

                if (TryWriteBlackBoxSnapshotCold())
                {
                    _dumpedFault = true;
                    return;
                }
            }
            finally
            {
                Volatile.Write(ref _blackBoxDumpInFlight, 0);
            }
        }

        private bool TryStageBlackBoxDumpSnapshot(
            NativeArray<ProceduralBoneTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryCursor)
        {
            if (!telemetryRing.IsCreated ||
                telemetryRing.Length < ProceduralBoneBlenderConstants.TelemetryCapacity ||
                !telemetryCursor.IsCreated ||
                telemetryCursor.Length <= 0)
            {
                return false;
            }

            int cursor = telemetryCursor[0];
            int start = cursor >= ProceduralBoneBlenderConstants.TelemetryCapacity
                ? ProceduralBoneMath.PositiveModulo(cursor - ProceduralBoneBlenderConstants.TelemetryCapacity, telemetryRing.Length)
                : 0;
            for (int i = 0; i < ProceduralBoneBlenderConstants.TelemetryCapacity; i++)
            {
                int sourceIndex = ProceduralBoneMath.PositiveModulo(start + i, telemetryRing.Length);
                _blackBoxDumpSnapshot[i] = telemetryRing[sourceIndex];
            }

            _blackBoxDumpSnapshotCursor = cursor;
            _blackBoxDumpSnapshotCount = ProceduralBoneBlenderConstants.TelemetryCapacity;
            return true;
        }

        private unsafe bool TryWriteBlackBoxSnapshotCold()
        {
            if (!ProceduralBoneBlenderLayout.Validate() ||
                _blackBoxDumpSnapshot == null ||
                _blackBoxDumpSnapshotCount < ProceduralBoneBlenderConstants.TelemetryCapacity)
            {
                return false;
            }

            uint hash = 2166136261u ^
                (uint)_blackBoxDumpSnapshotCount ^
                (uint)_blackBoxDumpSnapshotCursor ^
                0x414E494Du;
            int entryBytes = UnsafeUtility.SizeOf<ProceduralBoneTelemetryEntry>();
            int count = ProceduralBoneBlenderConstants.TelemetryCapacity;
            const int telemetryDumpEntryBytes = 64;
            if (entryBytes != telemetryDumpEntryBytes)
                return false;

            for (int i = 0; i < count; i++)
            {
                ProceduralBoneTelemetryEntry entry = _blackBoxDumpSnapshot[i];
                byte* bytes = (byte*)UnsafeUtility.AddressOf(ref entry);
                for (int byteIndex = 0; byteIndex < entryBytes; byteIndex++)
                    hash = (hash ^ bytes[byteIndex]) * 16777619u;
            }

            _blackBoxDumpHash = hash == 0u ? 2166136261u : hash;
            const int headerBytes = 24;

            int byteCount = headerBytes + count * telemetryDumpEntryBytes;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(ProceduralBoneBlenderRuntime),
                    BlackBoxDumpPayloadLabel,
                    NativeArrayOptions.ClearMemory);
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                Span<byte> header = new Span<byte>(destination, headerBytes);
                WriteUIntLittleEndian(header.Slice(0, 4), 0x50424F4Eu);
                WriteUIntLittleEndian(header.Slice(4, 4), 1u);
                WriteUIntLittleEndian(header.Slice(8, 4), (uint)_blackBoxDumpSnapshotCount);
                WriteUIntLittleEndian(header.Slice(12, 4), (uint)_blackBoxDumpSnapshotCursor);
                WriteUIntLittleEndian(header.Slice(16, 4), (uint)entryBytes);
                WriteUIntLittleEndian(header.Slice(20, 4), _blackBoxDumpHash);

                byte* rowDestination = destination + headerBytes;
                for (int i = 0; i < count; i++)
                {
                    ProceduralBoneTelemetryEntry entry = _blackBoxDumpSnapshot[i];
                    UnsafeUtility.MemCpy(rowDestination + i * telemetryDumpEntryBytes, UnsafeUtility.AddressOf(ref entry), telemetryDumpEntryBytes);
                }

                return NativeFaultDumpWriter.TryWriteAll(BlackBoxDumpRelativePath, payload, byteCount);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(ProceduralBoneBlenderRuntime),
                    BlackBoxDumpPayloadLabel);
            }
        }

        private static void WriteUIntLittleEndian(Span<byte> destination, uint value)
        {
            destination[0] = (byte)value;
            destination[1] = (byte)(value >> 8);
            destination[2] = (byte)(value >> 16);
            destination[3] = (byte)(value >> 24);
        }

        private bool UploadMatricesToGpu()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                ClearGpuSkinningBinding();
                return false;
            }

            if (!TryResolveOwnedVaultBuffer(vault, ProceduralBoneBlenderBufferIds.BoneMatrices, in _boneMatricesHandle, 1, out NativeArray<float4x4> matrices))
            {
                ClearGpuSkinningBinding();
                return false;
            }

            int count = math.min(math.min(_activeMatrixUploadCount, matrices.Length), _boneCapacity);
            if (count <= 0)
            {
                ClearGpuSkinningBinding();
                return true;
            }

            int bufferCapacity = ResolveGraphicsBufferCapacity();
            if (!HasGraphicsBuffersReady(bufferCapacity))
            {
                ClearGpuSkinningBinding();
                return false;
            }

            GraphicsBuffer writeBuffer = _gpuUploadBufferIndex == 0 ? _matrixBufferA : _matrixBufferB;
            if (!HasValidGraphicsBuffer(writeBuffer, count))
            {
                ClearGpuSkinningBinding();
                return false;
            }

            ProceduralBoneGraphicsBufferUpload.UploadNativeArray(writeBuffer, matrices, count);
            if (!PublishGpuSkinningBinding(writeBuffer, count))
            {
                ClearGpuSkinningBinding();
                return false;
            }

            _gpuUploadBufferIndex ^= 1;
            _gpuBufferDataValid = true;
            _uploadedMatrixStateHash = _latestMatrixStateHash;
            _uploadedMatrixCount = count;
            _uploadedSkeletonCount = _activeSkeletonCount;
            _uploadedQuality = _lastQuality;
            _gpuShaderConstantsDirty = false;
            return true;
        }

        private bool PublishCurrentGpuSkinningBinding()
        {
            int count = _uploadedMatrixCount;
            GraphicsBuffer buffer = _gpuUploadBufferIndex == 0 ? _matrixBufferB : _matrixBufferA;
            if (!_gpuBufferDataValid ||
                count <= 0 ||
                !HasValidGraphicsBuffer(buffer, count))
            {
                return false;
            }

            if (!PublishGpuSkinningBinding(buffer, count))
                return false;

            _uploadedSkeletonCount = _activeSkeletonCount;
            _uploadedQuality = _lastQuality;
            return true;
        }

        private bool PublishGpuSkinningBinding(GraphicsBuffer buffer, int count)
        {
            ProceduralBoneShaderGlobalsDTO globals = new ProceduralBoneShaderGlobalsDTO
            {
                Scalars0 = new float4(count, _activeSkeletonCount, _lastQuality, 1f),
                Scalars1 = float4.zero
            };
            if (!PublishProceduralBoneGlobals(in globals))
                return false;

            if (!_globalGpuSkinningPublished ||
                !ReferenceEquals(_publishedSkinningMatrixBuffer, buffer) ||
                _publishedSkinningMatrixCount != count)
            {
                Shader.SetGlobalBuffer(ProceduralBoneMatricesId, buffer);
                _publishedSkinningMatrixBuffer = buffer;
                _publishedSkinningMatrixCount = count;
            }

            _globalGpuSkinningPublished = true;
            return true;
        }

        private bool PublishProceduralBoneGlobals(in ProceduralBoneShaderGlobalsDTO globals)
        {
            if (!ValidateProceduralBoneShaderGlobalsLayout() ||
                !_supportsConstantBufferBinding ||
                !HasShaderGlobalsBuffersReady())
                return false;

            GraphicsBuffer writeBuffer = _shaderGlobalsUploadBufferIndex == 0 ? _shaderGlobalsBufferA : _shaderGlobalsBufferB;
            NativeArray<ProceduralBoneShaderGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<ProceduralBoneShaderGlobalsDTO>(0, 1);
            try
            {
                mapped[0] = globals;
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<ProceduralBoneShaderGlobalsDTO>(1);
            }

            _shaderGlobalsUploadBufferIndex ^= 1;
            _activeShaderGlobalsBuffer = writeBuffer;
            Shader.SetGlobalConstantBuffer(ProceduralBoneGlobalsId, _activeShaderGlobalsBuffer, 0, ProceduralBoneShaderGlobalsBytes);
            return true;
        }

        private bool EnsureShaderGlobalsBuffers()
        {
            if (!_supportsConstantBufferBinding)
            {
                ReleaseGraphicsBuffer(ref _shaderGlobalsBufferA);
                ReleaseGraphicsBuffer(ref _shaderGlobalsBufferB);
                return false;
            }

            if (!HasValidShaderGlobalsBuffer(_shaderGlobalsBufferA))
            {
                ReleaseGraphicsBuffer(ref _shaderGlobalsBufferA);
                _shaderGlobalsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, ProceduralBoneShaderGlobalsBytes); // COLD ALLOC: GraphicsBuffer[32B] - procedural bone globals A - owner: SHINOBU_305
            }

            if (!HasValidShaderGlobalsBuffer(_shaderGlobalsBufferB))
            {
                ReleaseGraphicsBuffer(ref _shaderGlobalsBufferB);
                _shaderGlobalsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, ProceduralBoneShaderGlobalsBytes); // COLD ALLOC: GraphicsBuffer[32B] - procedural bone globals B - owner: SHINOBU_305
            }

            return HasValidShaderGlobalsBuffer(_shaderGlobalsBufferA) &&
                   HasValidShaderGlobalsBuffer(_shaderGlobalsBufferB);
        }

        private void EnsureGraphicsBuffers()
        {
            int count = ResolveGraphicsBufferCapacity();
            if (!HasValidGraphicsBuffer(_matrixBufferA, count))
            {
                ReleaseGraphicsBuffer(ref _matrixBufferA);
                _matrixBufferA = ProceduralBoneGraphicsBufferUpload.CreateStructuredLockBuffer<float4x4>(count);
                _gpuBufferDataValid = false;
            }

            if (!HasValidGraphicsBuffer(_matrixBufferB, count))
            {
                ReleaseGraphicsBuffer(ref _matrixBufferB);
                _matrixBufferB = ProceduralBoneGraphicsBufferUpload.CreateStructuredLockBuffer<float4x4>(count);
                _gpuBufferDataValid = false;
            }

            EnsureShaderGlobalsBuffers();
        }

        private void ClearGpuSkinningBinding()
        {
            _gpuBufferDataValid = false;
            _gpuUploadDirty = false;
            _gpuShaderConstantsDirty = false;
            _uploadedMatrixStateHash = 0u;
            _uploadedMatrixCount = 0;
            _uploadedSkeletonCount = 0;
            _uploadedQuality = -1f;
            if (_globalGpuSkinningPublished || _activeShaderGlobalsBuffer != null)
            {
                ProceduralBoneShaderGlobalsDTO disabled = new ProceduralBoneShaderGlobalsDTO
                {
                    Scalars0 = float4.zero,
                    Scalars1 = float4.zero
                };
                PublishProceduralBoneGlobals(in disabled);
                _globalGpuSkinningPublished = false;
                _publishedSkinningMatrixBuffer = null;
                _publishedSkinningMatrixCount = -1;
            }
        }

        private void ReleaseGraphicsBuffers()
        {
            ReleaseGraphicsBuffer(ref _matrixBufferA);
            ReleaseGraphicsBuffer(ref _matrixBufferB);
            ReleaseGraphicsBuffer(ref _shaderGlobalsBufferA);
            ReleaseGraphicsBuffer(ref _shaderGlobalsBufferB);
            _activeShaderGlobalsBuffer = null;
            _publishedSkinningMatrixBuffer = null;
            _publishedSkinningMatrixCount = -1;
            _gpuBufferDataValid = false;
        }

        private void ReleaseVaultHandles()
        {
            ReleaseVaultHandles(_dataVault);
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseVaultHandle(vault, ProceduralBoneBlenderBufferIds.Rigs, ref _rigsHandle);
            ReleaseVaultHandle(vault, ProceduralBoneBlenderBufferIds.FrameInputs, ref _frameInputsHandle);
            ReleaseVaultHandle(vault, ProceduralBoneBlenderBufferIds.ParentIndices, ref _parentIndicesHandle);
            ReleaseVaultHandle(vault, ProceduralBoneBlenderBufferIds.BindPoses, ref _bindPosesHandle);
            ReleaseVaultHandle(vault, ProceduralBoneBlenderBufferIds.BoneStates, ref _boneStatesHandle);
            ReleaseVaultHandle(vault, ProceduralBoneBlenderBufferIds.BoneMatrices, ref _boneMatricesHandle);
            ReleaseVaultHandle(vault, ProceduralBoneBlenderBufferIds.FrameStats, ref _frameStatsHandle);
            ReleaseVaultHandle(vault, ProceduralBoneBlenderBufferIds.TelemetryRing, ref _telemetryRingHandle);
            ReleaseVaultHandle(vault, ProceduralBoneBlenderBufferIds.TelemetryCursor, ref _telemetryCursorHandle);
            ReleaseVaultHandle(vault, ProceduralBoneBlenderBufferIds.Tuning, ref _tuningHandle);
            ReleaseVaultHandle(vault, ProceduralBoneBlenderBufferIds.MockAiSignals, ref _mockAiSignalsHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, BufferID bufferId, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (IsOwnedVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsOwnedVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return IsVaultHandleForBuffer(in handle, expectedBufferId) &&
                   handle.SystemID == (uint)SystemID.AnimationFauna;
        }

        private void ClearHandles()
        {
            _rigsHandle = default;
            _frameInputsHandle = default;
            _parentIndicesHandle = default;
            _bindPosesHandle = default;
            _boneStatesHandle = default;
            _boneMatricesHandle = default;
            _frameStatsHandle = default;
            _telemetryRingHandle = default;
            _telemetryCursorHandle = default;
            _tuningHandle = default;
            _mockAiSignalsHandle = default;
        }

        private void TryRegister()
        {
            if (!_registeredUpdate)
                _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            if (!_registeredHotSwap)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregister()
        {
            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdate = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registeredHotSwap)
            {
                GlobalRegistry.UnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }
        }

        private static bool HasValidGraphicsBuffer(GraphicsBuffer buffer, int requiredCount)
        {
            return buffer != null && buffer.IsValid() && buffer.count >= requiredCount && buffer.stride == UnsafeUtility.SizeOf<float4x4>();
        }

        private int ResolveGraphicsBufferCapacity()
        {
            return math.clamp(_boneCapacity, ProceduralBoneBlenderConstants.EmergencyMockBoneCount, ProceduralBoneBlenderConstants.DefaultBoneCapacity);
        }

        private bool HasGraphicsBuffersReady(int requiredCount)
        {
            return HasValidGraphicsBuffer(_matrixBufferA, requiredCount) &&
                   HasValidGraphicsBuffer(_matrixBufferB, requiredCount) &&
                   HasShaderGlobalsBuffersReady();
        }

        private static bool HasValidShaderGlobalsBuffer(GraphicsBuffer buffer)
        {
            return buffer != null && buffer.IsValid() && buffer.count >= 1 && buffer.stride == ProceduralBoneShaderGlobalsBytes;
        }

        private bool HasShaderGlobalsBuffersReady()
        {
            return HasValidShaderGlobalsBuffer(_shaderGlobalsBufferA) &&
                   HasValidShaderGlobalsBuffer(_shaderGlobalsBufferB);
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            if (buffer.IsValid())
                buffer.Release();
            buffer = null;
        }

        private void OnDrawGizmosSelected()
        {
            if (!TryResolveMatricesForEditor(out NativeArray<float4x4>.ReadOnly matrices, out NativeArray<int>.ReadOnly parents, out int count))
                return;

            int drawCount = math.min(count, 512);
            Gizmos.color = Color.cyan;
            for (int i = 0; i < drawCount; i++)
            {
                int parent = parents[i];
                if (parent < 0 || parent >= drawCount)
                    continue;

                Vector3 a = (Vector3)matrices[i].c3.xyz;
                Vector3 b = (Vector3)matrices[parent].c3.xyz;
                Gizmos.DrawLine(a, b);
            }
        }
    }

    internal static class ProceduralBoneGraphicsBufferUpload
    {
        public static GraphicsBuffer CreateStructuredLockBuffer<T>(int count) where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                UnsafeUtility.SizeOf<T>());
        }

        public static void UploadNativeArray<T>(GraphicsBuffer destination, NativeArray<T> source, int count) where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source.IsCreated ? source.Length : 0, count);
            if (safeCount <= 0)
                return;

            NativeArray<T> mapped = destination.LockBufferForWrite<T>(0, safeCount);
            try
            {
                unsafe
                {
                    void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                    void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                    UnsafeUtility.MemCpy(destinationPtr, sourcePtr, (long)UnsafeUtility.SizeOf<T>() * safeCount);
                }
            }
            finally
            {
                destination.UnlockBufferAfterWrite<T>(safeCount);
            }
        }

        private static int ResolveSafeWriteCount<T>(GraphicsBuffer destination, int sourceLength, int requestedCount) where T : struct
        {
            if (destination == null || requestedCount <= 0 || sourceLength <= 0 || destination.count <= 0)
                return 0;

            int stride = UnsafeUtility.SizeOf<T>();
            if (destination.stride != stride)
                return 0;

            return math.min(math.min(requestedCount, sourceLength), destination.count);
        }
    }
}
