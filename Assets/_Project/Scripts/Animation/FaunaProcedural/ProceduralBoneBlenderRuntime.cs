using System;
using System.IO;
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
        private const int LockRigs = 1 << 0;
        private const int LockInputs = 1 << 1;
        private const int LockParents = 1 << 2;
        private const int LockBindPoses = 1 << 3;
        private const int LockBoneStates = 1 << 4;
        private const int LockMatrices = 1 << 5;
        private const int LockStats = 1 << 6;
        private const int LockTelemetry = 1 << 7;
        private const int LockCursor = 1 << 8;
        private const int LockTuning = 1 << 9;
        private const int LockMockSignals = 1 << 10;

        private static readonly int ProceduralBoneMatricesId = Shader.PropertyToID("_H8ProceduralBoneMatrices");
        private static readonly int ProceduralBoneMatrixCountId = Shader.PropertyToID("_H8ProceduralBoneMatrixCount");
        private static readonly int ProceduralBoneActiveSkeletonsId = Shader.PropertyToID("_H8ProceduralBoneActiveSkeletons");
        private static readonly int ProceduralBoneQualityId = Shader.PropertyToID("_H8ProceduralBoneQuality");
        private static readonly int ProceduralBoneGpuSkinningId = Shader.PropertyToID("_H8ProceduralBoneGpuSkinning");

        [SerializeField, Range(1, ProceduralBoneBlenderConstants.DefaultSkeletonCapacity)]
        private int _skeletonCapacity = ProceduralBoneBlenderConstants.DefaultSkeletonCapacity;

        [SerializeField, Range(ProceduralBoneBlenderConstants.EmergencyMockBoneCount, ProceduralBoneBlenderConstants.DefaultBoneCapacity)]
        private int _boneCapacity = ProceduralBoneBlenderConstants.DefaultBoneCapacity;

        [SerializeField] private Material _gpuSkinningMaterial;
        [SerializeField] private bool _publishGlobalBuffer = true;
        [SerializeField] private bool _seedEmergencyMockRig = true;

        private IDataVault _dataVault;
        private VaultBufferHandle<ProceduralBoneRigDTO> _rigsHandle;
        private VaultBufferHandle<ProceduralBoneFrameInputDTO> _frameInputsHandle;
        private VaultBufferHandle<int> _parentIndicesHandle;
        private VaultBufferHandle<float4x4> _bindPosesHandle;
        private VaultBufferHandle<BoneStateDTO> _boneStatesHandle;
        private VaultBufferHandle<float4x4> _boneMatricesHandle;
        private VaultBufferHandle<ProceduralBoneFrameStatsDTO> _frameStatsHandle;
        private VaultBufferHandle<ProceduralBoneTelemetryEntry> _telemetryRingHandle;
        private VaultBufferHandle<int> _telemetryCursorHandle;
        private VaultBufferHandle<ProceduralBoneRigTuningDTO> _tuningHandle;
        private VaultBufferHandle<MockAiVelocitySignal> _mockAiSignalsHandle;

        private GraphicsBuffer _matrixBufferA;
        private GraphicsBuffer _matrixBufferB;
        private JobHandle _pendingHandle;
        private float _simulationTime;
        private float _accumulatedDelta;
        private float _lastQuality = 1f;
        private uint _frameCounter;
        private uint _latestMatrixStateHash;
        private uint _uploadedMatrixStateHash;
        private int _gpuUploadBufferIndex;
        private int _activeMatrixUploadCount;
        private int _activeSkeletonCount;
        private int _uploadedMatrixCount;
        private int _uploadedSkeletonCount;
        private int _lockedBuffers;
        private float _uploadedQuality = -1f;
        private bool _solverScheduled;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _gpuUploadDirty;
        private bool _gpuShaderConstantsDirty;
        private bool _gpuBufferDataValid;
        private bool _globalGpuSkinningPublished;
        private bool _disposed;
        private bool _dumpedFault;

        private static ProceduralBoneBlenderRuntime _activeRuntimeInstance;

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

        public bool TryResolveTuningForEditor(out NativeArray<ProceduralBoneRigTuningDTO> tuning)
        {
            tuning = default;
            IDataVault vault = _dataVault ?? GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            if (!_tuningHandle.IsCreated)
                EnsureVaultBuffers();

            tuning = _tuningHandle.Resolve(vault);
            return tuning.IsCreated && tuning.Length >= ProceduralBoneBlenderConstants.TuningCapacity;
        }

        public bool TryResolveMatricesForEditor(
            out NativeArray<float4x4> matrices,
            out NativeArray<int> parentIndices,
            out int matrixCount)
        {
            matrices = default;
            parentIndices = default;
            matrixCount = 0;
            if (_solverScheduled)
                return false;

            IDataVault vault = _dataVault ?? GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            matrices = _boneMatricesHandle.Resolve(vault);
            parentIndices = _parentIndicesHandle.Resolve(vault);
            if (!matrices.IsCreated || !parentIndices.IsCreated)
                return false;

            matrixCount = math.min(math.min(_activeMatrixUploadCount, matrices.Length), parentIndices.Length);
            return matrixCount > 0;
        }

        public bool TryApplyCsvProfile(string csvText)
        {
            if (string.IsNullOrEmpty(csvText) || !TryResolveTuningForEditor(out NativeArray<ProceduralBoneRigTuningDTO> tuning))
                return false;

            IDataVault vault = _dataVault ?? GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            NativeArray<ProceduralBoneRigDTO> rigs = _rigsHandle.Resolve(vault);
            if (!rigs.IsCreated || rigs.Length <= 0)
                return false;

            if (rigs[0].BoneCount <= 0)
                GenerateEmergencyMockRigs();

            rigs = _rigsHandle.Resolve(vault);
            if (!rigs.IsCreated || rigs.Length <= 0)
                return false;

            ProceduralBoneRigTuningDTO dto = tuning[0];
            ProceduralBoneRigDTO rig = rigs[0];
            bool result = ProceduralBoneProfileCsvParser.TryApply(csvText.AsSpan(), ref dto, ref rig);
            tuning[0] = dto;
            rigs[0] = rig;
            return result;
        }

        private void Awake()
        {
            if (_activeRuntimeInstance == null)
                _activeRuntimeInstance = this;

            RefreshColdDependencies();
            if (EnsureVaultBuffers())
                EnsureGraphicsBuffers();
            if (_seedEmergencyMockRig)
                GenerateEmergencyMockRigs();
        }

        private void OnEnable()
        {
            if (_disposed || !Application.isPlaying)
                return;

            CompletePendingSolver(true);
            RefreshColdDependencies();
            if (EnsureVaultBuffers())
                EnsureGraphicsBuffers();
            if (_seedEmergencyMockRig)
                GenerateEmergencyMockRigs();
            TryRegister();
        }

        private void OnDisable()
        {
            if (_disposed || !Application.isPlaying)
                return;

            TryUnregister();
            CompletePendingSolver(true);
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
            if (ReferenceEquals(_activeRuntimeInstance, this))
                _activeRuntimeInstance = null;

            TryUnregister();
            CompletePendingSolver(true);
            ClearGpuSkinningBinding();
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

            if (!TryResolveRuntimeBuffers(
                    vault,
                    out NativeArray<ProceduralBoneRigDTO> rigs,
                    out NativeArray<ProceduralBoneFrameInputDTO> inputs,
                    out NativeArray<int> parents,
                    out NativeArray<float4x4> bindPoses,
                    out NativeArray<BoneStateDTO> boneStates,
                    out NativeArray<float4x4> matrices,
                    out NativeArray<ProceduralBoneFrameStatsDTO> stats,
                    out NativeArray<ProceduralBoneTelemetryEntry> telemetry,
                    out NativeArray<int> cursor,
                    out NativeArray<ProceduralBoneRigTuningDTO> tuningArray,
                    out NativeArray<MockAiVelocitySignal> mockSignals))
            {
                EnsureVaultBuffers();
                return;
            }

            ProceduralBoneRigTuningDTO tuning = ProceduralBoneSanitizer.SanitizeTuning(tuningArray[0]);
            float globalQuality = ResolveGlobalQualityWeight(vault);
            tuning.GlobalQualityWeight = globalQuality;
            tuning.ActiveSkeletonCount = math.clamp(tuning.ActiveSkeletonCount, 0, _skeletonCapacity);
            tuningArray[0] = tuning;
            _lastQuality = globalQuality;

            float safeDelta = math.clamp(deltaTime, ProceduralBoneBlenderConstants.MinDeltaTime, ProceduralBoneBlenderConstants.MaxDeltaTime);
            _accumulatedDelta = math.min(_accumulatedDelta + safeDelta, ProceduralBoneBlenderConstants.MaxDeltaTime);
            float updateHz = math.lerp(tuning.LowQualityUpdateHz, tuning.HighQualityUpdateHz, ProceduralBoneMath.Smooth01(globalQuality));
            float updateInterval = math.rcp(math.max(1f, updateHz));
            if (_accumulatedDelta + 0.00001f < updateInterval && _frameCounter != 0u)
                return;

            int activeSkeletons = math.min(tuning.ActiveSkeletonCount, math.min(rigs.Length, inputs.Length));
            if (activeSkeletons <= 0)
                return;

            if (!TryLockJobBuffers(vault))
                return;

            float solveDelta = _accumulatedDelta;
            _accumulatedDelta = 0f;
            _simulationTime += solveDelta;
            uint frame = _frameCounter + 1u;

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
            _solverScheduled = true;
        }

        public void LateFrameTick()
        {
            if (_disposed)
                return;

            if (_solverScheduled && !CompletePendingSolver(false))
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

            CompletePendingSolver(true);
            _dataVault = currentService as IDataVault;
            ClearHandles();
            if (EnsureVaultBuffers())
                EnsureGraphicsBuffers();
            if (_seedEmergencyMockRig)
                GenerateEmergencyMockRigs();
        }

        public bool GenerateEmergencyMockRigs()
        {
            IDataVault vault = _dataVault ?? GlobalRegistry.DataVault;
            if (vault == null || !EnsureVaultBuffers())
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

        private void RefreshColdDependencies()
        {
            _dataVault = GlobalRegistry.DataVault;
        }

        private bool EnsureVaultBuffers()
        {
            IDataVault vault = _dataVault ?? GlobalRegistry.DataVault;
            if (vault == null || !ProceduralBoneBlenderLayout.Validate())
                return false;

            _dataVault = vault;
            int skeletonCapacity = math.clamp(_skeletonCapacity, 1, ProceduralBoneBlenderConstants.DefaultSkeletonCapacity);
            int boneCapacity = math.clamp(_boneCapacity, ProceduralBoneBlenderConstants.EmergencyMockBoneCount, ProceduralBoneBlenderConstants.DefaultBoneCapacity);
            _rigsHandle = vault.GetBufferHandle<ProceduralBoneRigDTO>(
                ProceduralBoneBlenderBufferIds.Rigs,
                skeletonCapacity,
                SystemID.AnimationFauna,
                NativeArrayOptions.UninitializedMemory);
            _frameInputsHandle = vault.GetBufferHandle<ProceduralBoneFrameInputDTO>(
                ProceduralBoneBlenderBufferIds.FrameInputs,
                skeletonCapacity,
                SystemID.AnimationFauna,
                NativeArrayOptions.UninitializedMemory);
            _parentIndicesHandle = vault.GetBufferHandle<int>(
                ProceduralBoneBlenderBufferIds.ParentIndices,
                boneCapacity,
                SystemID.AnimationFauna,
                NativeArrayOptions.UninitializedMemory);
            _bindPosesHandle = vault.GetBufferHandle<float4x4>(
                ProceduralBoneBlenderBufferIds.BindPoses,
                boneCapacity,
                SystemID.AnimationFauna,
                NativeArrayOptions.UninitializedMemory);
            _boneStatesHandle = vault.GetBufferHandle<BoneStateDTO>(
                ProceduralBoneBlenderBufferIds.BoneStates,
                boneCapacity,
                SystemID.AnimationFauna,
                NativeArrayOptions.UninitializedMemory);
            _boneMatricesHandle = vault.GetBufferHandle<float4x4>(
                ProceduralBoneBlenderBufferIds.BoneMatrices,
                boneCapacity,
                SystemID.AnimationFauna,
                NativeArrayOptions.UninitializedMemory);
            _frameStatsHandle = vault.GetBufferHandle<ProceduralBoneFrameStatsDTO>(
                ProceduralBoneBlenderBufferIds.FrameStats,
                skeletonCapacity,
                SystemID.AnimationFauna,
                NativeArrayOptions.UninitializedMemory);
            _telemetryRingHandle = vault.GetBufferHandle<ProceduralBoneTelemetryEntry>(
                ProceduralBoneBlenderBufferIds.TelemetryRing,
                ProceduralBoneBlenderConstants.TelemetryCapacity,
                SystemID.AnimationFauna,
                NativeArrayOptions.ClearMemory);
            _telemetryCursorHandle = vault.GetBufferHandle<int>(
                ProceduralBoneBlenderBufferIds.TelemetryCursor,
                1,
                SystemID.AnimationFauna,
                NativeArrayOptions.ClearMemory);
            _tuningHandle = vault.GetBufferHandle<ProceduralBoneRigTuningDTO>(
                ProceduralBoneBlenderBufferIds.Tuning,
                ProceduralBoneBlenderConstants.TuningCapacity,
                SystemID.AnimationFauna,
                NativeArrayOptions.ClearMemory);
            _mockAiSignalsHandle = vault.GetBufferHandle<MockAiVelocitySignal>(
                ProceduralBoneBlenderBufferIds.MockAiSignals,
                skeletonCapacity,
                SystemID.AnimationFauna,
                NativeArrayOptions.UninitializedMemory);

            if (_tuningHandle.IsCreated)
            {
                NativeArray<ProceduralBoneRigTuningDTO> tuning = _tuningHandle.Resolve(vault);
                if (tuning.IsCreated && tuning.Length > 0)
                    tuning[0] = ProceduralBoneSanitizer.SanitizeTuning(tuning[0].HighQualityUpdateHz > 0f ? tuning[0] : ProceduralBoneRigTuningDTO.Default());
            }

            return _rigsHandle.IsCreated &&
                   _frameInputsHandle.IsCreated &&
                   _parentIndicesHandle.IsCreated &&
                   _bindPosesHandle.IsCreated &&
                   _boneStatesHandle.IsCreated &&
                   _boneMatricesHandle.IsCreated &&
                   _frameStatsHandle.IsCreated &&
                   _telemetryRingHandle.IsCreated &&
                   _telemetryCursorHandle.IsCreated &&
                   _tuningHandle.IsCreated &&
                   _mockAiSignalsHandle.IsCreated;
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

            rigs = _rigsHandle.Resolve(vault);
            inputs = _frameInputsHandle.Resolve(vault);
            parents = _parentIndicesHandle.Resolve(vault);
            bindPoses = _bindPosesHandle.Resolve(vault);
            boneStates = _boneStatesHandle.Resolve(vault);
            matrices = _boneMatricesHandle.Resolve(vault);
            stats = _frameStatsHandle.Resolve(vault);
            telemetry = _telemetryRingHandle.Resolve(vault);
            cursor = _telemetryCursorHandle.Resolve(vault);
            tuning = _tuningHandle.Resolve(vault);
            mockSignals = _mockAiSignalsHandle.Resolve(vault);
            return rigs.IsCreated &&
                   inputs.IsCreated &&
                   parents.IsCreated &&
                   bindPoses.IsCreated &&
                   boneStates.IsCreated &&
                   matrices.IsCreated &&
                   stats.IsCreated &&
                   telemetry.IsCreated &&
                   cursor.IsCreated &&
                   tuning.IsCreated &&
                   mockSignals.IsCreated &&
                   tuning.Length >= ProceduralBoneBlenderConstants.TuningCapacity &&
                   telemetry.Length >= ProceduralBoneBlenderConstants.TelemetryCapacity &&
                   cursor.Length >= 1;
        }

        private bool CompletePendingSolver(bool forceComplete)
        {
            if (!_solverScheduled)
                return true;

            if (!forceComplete && !_pendingHandle.IsCompleted)
                return false;

            _pendingHandle.Complete();
            _pendingHandle = default;
            _solverScheduled = false;
            UnlockJobBuffers();
            _frameCounter++;
            ReadLatestTelemetry();
            _gpuUploadDirty = ShouldUploadMatrices();
            if (!_dumpedFault && LatestTelemetryHasInvalidFlag())
            {
                DumpBlackBoxOnce();
            }

            return true;
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            _lockedBuffers = 0;
            return TryLock(vault, ProceduralBoneBlenderBufferIds.Rigs, LockRigs) &&
                   TryLock(vault, ProceduralBoneBlenderBufferIds.FrameInputs, LockInputs) &&
                   TryLock(vault, ProceduralBoneBlenderBufferIds.ParentIndices, LockParents) &&
                   TryLock(vault, ProceduralBoneBlenderBufferIds.BindPoses, LockBindPoses) &&
                   TryLock(vault, ProceduralBoneBlenderBufferIds.BoneStates, LockBoneStates) &&
                   TryLock(vault, ProceduralBoneBlenderBufferIds.BoneMatrices, LockMatrices) &&
                   TryLock(vault, ProceduralBoneBlenderBufferIds.FrameStats, LockStats) &&
                   TryLock(vault, ProceduralBoneBlenderBufferIds.TelemetryRing, LockTelemetry) &&
                   TryLock(vault, ProceduralBoneBlenderBufferIds.TelemetryCursor, LockCursor) &&
                   TryLock(vault, ProceduralBoneBlenderBufferIds.Tuning, LockTuning) &&
                   TryLock(vault, ProceduralBoneBlenderBufferIds.MockAiSignals, LockMockSignals);
        }

        private bool TryLock(IDataVault vault, BufferID bufferId, int bit)
        {
            if (vault.TryLockBuffer(bufferId, SystemID.AnimationFauna))
            {
                _lockedBuffers |= bit;
                return true;
            }

            UnlockJobBuffers();
            return false;
        }

        private void UnlockJobBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || _lockedBuffers == 0)
            {
                _lockedBuffers = 0;
                return;
            }

            Unlock(vault, ProceduralBoneBlenderBufferIds.Rigs, LockRigs);
            Unlock(vault, ProceduralBoneBlenderBufferIds.FrameInputs, LockInputs);
            Unlock(vault, ProceduralBoneBlenderBufferIds.ParentIndices, LockParents);
            Unlock(vault, ProceduralBoneBlenderBufferIds.BindPoses, LockBindPoses);
            Unlock(vault, ProceduralBoneBlenderBufferIds.BoneStates, LockBoneStates);
            Unlock(vault, ProceduralBoneBlenderBufferIds.BoneMatrices, LockMatrices);
            Unlock(vault, ProceduralBoneBlenderBufferIds.FrameStats, LockStats);
            Unlock(vault, ProceduralBoneBlenderBufferIds.TelemetryRing, LockTelemetry);
            Unlock(vault, ProceduralBoneBlenderBufferIds.TelemetryCursor, LockCursor);
            Unlock(vault, ProceduralBoneBlenderBufferIds.Tuning, LockTuning);
            Unlock(vault, ProceduralBoneBlenderBufferIds.MockAiSignals, LockMockSignals);
            _lockedBuffers = 0;
        }

        private void Unlock(IDataVault vault, BufferID bufferId, int bit)
        {
            if ((_lockedBuffers & bit) == 0)
                return;

            vault.TryUnlockBuffer(bufferId, SystemID.AnimationFauna);
        }

        private float ResolveGlobalQualityWeight(IDataVault vault)
        {
            if (vault != null && _tuningHandle.IsCreated)
            {
                NativeArray<ProceduralBoneRigTuningDTO> tuning = _tuningHandle.Resolve(vault);
                if (tuning.IsCreated && tuning.Length > 0 && math.isfinite(tuning[0].GlobalQualityWeight))
                    return math.saturate(tuning[0].GlobalQualityWeight);
            }

            return math.saturate(math.select(1f, _lastQuality, math.isfinite(_lastQuality)));
        }

        private void ReadLatestTelemetry()
        {
            _activeMatrixUploadCount = 0;
            _activeSkeletonCount = 0;
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            NativeArray<ProceduralBoneTelemetryEntry> telemetry = _telemetryRingHandle.Resolve(vault);
            NativeArray<int> cursor = _telemetryCursorHandle.Resolve(vault);
            if (!telemetry.IsCreated || !cursor.IsCreated || telemetry.Length <= 0 || cursor.Length <= 0 || cursor[0] <= 0)
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

            NativeArray<ProceduralBoneTelemetryEntry> telemetry = _telemetryRingHandle.Resolve(vault);
            NativeArray<int> cursor = _telemetryCursorHandle.Resolve(vault);
            if (!telemetry.IsCreated || !cursor.IsCreated || telemetry.Length <= 0 || cursor.Length <= 0 || cursor[0] <= 0)
                return false;

            int index = ProceduralBoneMath.PositiveModulo(cursor[0] - 1, telemetry.Length);
            return (telemetry[index].Flags & ProceduralBoneBlenderConstants.TelemetryFlagInvalid) != 0u;
        }

        private void DumpBlackBoxOnce()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            NativeArray<ProceduralBoneTelemetryEntry> telemetry = _telemetryRingHandle.Resolve(vault);
            NativeArray<int> cursor = _telemetryCursorHandle.Resolve(vault);
            _dumpedFault = ProceduralBoneBlackBox.TryDumpTelemetry(ResolveProjectRoot(), telemetry, cursor);
        }

        private bool UploadMatricesToGpu()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                ClearGpuSkinningBinding();
                return false;
            }

            NativeArray<float4x4> matrices = _boneMatricesHandle.Resolve(vault);
            if (!matrices.IsCreated)
            {
                ClearGpuSkinningBinding();
                return false;
            }

            int count = math.min(math.min(_activeMatrixUploadCount, matrices.Length), _boneCapacity);
            if (count <= 0 || (_gpuSkinningMaterial == null && !_publishGlobalBuffer))
            {
                ClearGpuSkinningBinding();
                return true;
            }

            EnsureGraphicsBuffers();
            GraphicsBuffer writeBuffer = _gpuUploadBufferIndex == 0 ? _matrixBufferA : _matrixBufferB;
            if (!HasValidGraphicsBuffer(writeBuffer, count))
            {
                ClearGpuSkinningBinding();
                return false;
            }

            ProceduralBoneGraphicsBufferUpload.UploadNativeArray(writeBuffer, matrices, count);
            PublishGpuSkinningBinding(writeBuffer, count);

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
                (_gpuSkinningMaterial == null && !_publishGlobalBuffer) ||
                !HasValidGraphicsBuffer(buffer, count))
            {
                return false;
            }

            PublishGpuSkinningBinding(buffer, count);
            _uploadedSkeletonCount = _activeSkeletonCount;
            _uploadedQuality = _lastQuality;
            return true;
        }

        private void PublishGpuSkinningBinding(GraphicsBuffer buffer, int count)
        {
            if (_gpuSkinningMaterial != null)
            {
                _gpuSkinningMaterial.SetBuffer(ProceduralBoneMatricesId, buffer);
                _gpuSkinningMaterial.SetFloat(ProceduralBoneMatrixCountId, count);
                _gpuSkinningMaterial.SetFloat(ProceduralBoneActiveSkeletonsId, _activeSkeletonCount);
                _gpuSkinningMaterial.SetFloat(ProceduralBoneQualityId, _lastQuality);
                _gpuSkinningMaterial.SetFloat(ProceduralBoneGpuSkinningId, 1f);
            }

            if (_publishGlobalBuffer)
            {
                Shader.SetGlobalBuffer(ProceduralBoneMatricesId, buffer);
                Shader.SetGlobalFloat(ProceduralBoneMatrixCountId, count);
                Shader.SetGlobalFloat(ProceduralBoneActiveSkeletonsId, _activeSkeletonCount);
                Shader.SetGlobalFloat(ProceduralBoneQualityId, _lastQuality);
                Shader.SetGlobalFloat(ProceduralBoneGpuSkinningId, 1f);
                _globalGpuSkinningPublished = true;
            }
        }

        private void EnsureGraphicsBuffers()
        {
            int count = math.clamp(_boneCapacity, ProceduralBoneBlenderConstants.EmergencyMockBoneCount, ProceduralBoneBlenderConstants.DefaultBoneCapacity);
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
            if (_gpuSkinningMaterial != null)
            {
                _gpuSkinningMaterial.SetFloat(ProceduralBoneMatrixCountId, 0f);
                _gpuSkinningMaterial.SetFloat(ProceduralBoneActiveSkeletonsId, 0f);
                _gpuSkinningMaterial.SetFloat(ProceduralBoneQualityId, 0f);
                _gpuSkinningMaterial.SetFloat(ProceduralBoneGpuSkinningId, 0f);
            }

            if (_publishGlobalBuffer || _globalGpuSkinningPublished)
            {
                Shader.SetGlobalFloat(ProceduralBoneMatrixCountId, 0f);
                Shader.SetGlobalFloat(ProceduralBoneActiveSkeletonsId, 0f);
                Shader.SetGlobalFloat(ProceduralBoneQualityId, 0f);
                Shader.SetGlobalFloat(ProceduralBoneGpuSkinningId, 0f);
                _globalGpuSkinningPublished = false;
            }
        }

        private void ReleaseGraphicsBuffers()
        {
            ReleaseGraphicsBuffer(ref _matrixBufferA);
            ReleaseGraphicsBuffer(ref _matrixBufferB);
            _gpuBufferDataValid = false;
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

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            if (buffer.IsValid())
                buffer.Release();
            buffer = null;
        }

        private static string ResolveProjectRoot()
        {
            string dataPath = Application.dataPath;
            return string.IsNullOrEmpty(dataPath) ? "." : Path.GetFullPath(Path.Combine(dataPath, ".."));
        }

        private void OnDrawGizmosSelected()
        {
            if (!TryResolveMatricesForEditor(out NativeArray<float4x4> matrices, out NativeArray<int> parents, out int count))
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
            unsafe
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                UnsafeUtility.MemCpy(destinationPtr, sourcePtr, (long)UnsafeUtility.SizeOf<T>() * safeCount);
            }

            destination.UnlockBufferAfterWrite<T>(safeCount);
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
