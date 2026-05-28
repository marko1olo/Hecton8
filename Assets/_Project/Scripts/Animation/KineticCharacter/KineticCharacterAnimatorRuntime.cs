using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Determinism;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Animation.KineticCharacter
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Animation/Kinetic Character Matrix Runtime")]
    public sealed class KineticCharacterAnimatorRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IKineticCharacterPresentationSink, IDisposable
    {
        private const int LockRigs = 1 << 0;
        private const int LockInputs = 1 << 1;
        private const int LockParents = 1 << 2;
        private const int LockBindPoses = 1 << 3;
        private const int LockBoneOutputs = 1 << 4;
        private const int LockMatrices = 1 << 5;
        private const int LockIkTargets = 1 << 6;
        private const int LockStats = 1 << 7;
        private const int LockTelemetry = 1 << 8;
        private const int LockCursor = 1 << 9;
        private const int LockTuning = 1 << 10;
        private const int LockSdf = 1 << 11;
        private const int LockPlayerHandIkStates = 1 << 12;

        private static readonly int KineticBoneMatricesId = Shader.PropertyToID("_H8KineticCharacterBoneMatrices");
        private static readonly int KineticBoneMatrixCountId = Shader.PropertyToID("_H8KineticCharacterBoneMatrixCount");
        private static readonly int KineticActiveCharactersId = Shader.PropertyToID("_H8KineticCharacterActiveCharacters");
        private static readonly int KineticQualityId = Shader.PropertyToID("_H8KineticCharacterQuality");
        private static readonly int KineticGpuSkinningId = Shader.PropertyToID("_H8KineticCharacterGpuSkinning");

        [SerializeField, Range(KineticCharacterAnimatorConstants.EmergencyMockBoneCount, KineticCharacterAnimatorConstants.DefaultBoneCapacity)]
        private int _boneCapacity = KineticCharacterAnimatorConstants.DefaultBoneCapacity;

        [SerializeField] private Material _gpuSkinningMaterial;
        [SerializeField] private bool _publishGlobalBuffer = true;
        [SerializeField] private bool _seedEmergencyMockRig = true;
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private Vector3Int _sdfDimensions = new Vector3Int(64, 64, 64);
        [SerializeField] private Vector3 _sdfOrigin = new Vector3(-32f, -32f, -32f);
        [SerializeField] private Vector3 _sdfCellSize = Vector3.one;
        [SerializeField, Min(0.01f)] private float _sdfRangeMeters = 2f;

        private IDataVault _dataVault;
        private VaultGenerationHandle<KineticCharacterRigDTO> _rigsHandle;
        private VaultGenerationHandle<KineticCharacterFrameInputDTO> _inputsHandle;
        private VaultGenerationHandle<int> _parentIndicesHandle;
        private VaultGenerationHandle<float4x4> _bindPosesHandle;
        private VaultGenerationHandle<ProceduralBoneDTO> _boneOutputsHandle;
        private VaultGenerationHandle<float4x4> _matricesHandle;
        private VaultGenerationHandle<ProceduralIKTargetDTO> _ikTargetsHandle;
        private VaultGenerationHandle<KineticCharacterFrameStatsDTO> _statsHandle;
        private VaultGenerationHandle<KineticAnimationTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<KineticCharacterTuningDTO> _tuningHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _csvScratchHandle;
#endif

        private GraphicsBuffer _matrixBufferA;
        private GraphicsBuffer _matrixBufferB;
        private JobHandle _pendingHandle;
        private uint _frameCounter;
        private float _simulationTime;
        private float _submittedBreathingPhase;
        private float _submittedWaveForward;
        private float _submittedWaveLateral;
        private float _submittedCrestReach;
        private float _submittedDescentTuck;
        private float _submittedLeanWeight;
        private float _submittedImmersionDepth;
        private float _submittedToolWeight;
        private float3 _submittedDamageImpulseLocal;
        private float _submittedDamageImpulse01;
        private float4x4 _submittedToolPose = float4x4.identity;
        private uint _submittedToolHash;
        private uint _latestStateHash;
        private uint _uploadedStateHash;
        private int _gpuUploadBufferIndex;
        private int _activeMatrixUploadCount;
        private int _uploadedMatrixCount;
        private int _activeCharacterCount;
        private int _uploadedCharacterCount;
        private int _lockedBuffers;
        private float _lastQuality = 1f;
        private float _uploadedQuality = -1f;
        private bool _solverScheduled;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _gpuUploadDirty;
        private bool _gpuConstantsDirty;
        private bool _gpuBufferDataValid;
        private bool _globalGpuSkinningPublished;
        private bool _disposed;
        private bool _dumpedFault;

        private static KineticCharacterAnimatorRuntime _activeRuntimeInstance;

        public static bool TryGetActiveRuntimeInstance(out KineticCharacterAnimatorRuntime runtime)
        {
            runtime = _activeRuntimeInstance;
            return runtime != null && !runtime._disposed;
        }

        public bool TryGetKineticGraphicsBuffer(out GraphicsBuffer buffer, out int matrixCount)
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

        public bool TryResolveTuningForEditor(out NativeArray<KineticCharacterTuningDTO>.ReadOnly tuning)
        {
            tuning = default;
            if (!TryResolveTuningMutable(out NativeArray<KineticCharacterTuningDTO> mutableTuning))
                return false;

            tuning = mutableTuning.AsReadOnly();
            return true;
        }

        public bool TryApplyEditorTuning(in KineticCharacterTuningDTO tuning)
        {
            if (!TryResolveTuningMutable(out NativeArray<KineticCharacterTuningDTO> mutableTuning))
                return false;

            mutableTuning[0] = KineticCharacterSanitizer.SanitizeTuning(tuning);
            return true;
        }

        private bool TryResolveTuningMutable(out NativeArray<KineticCharacterTuningDTO> tuning)
        {
            tuning = default;
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return false;

            if (!IsVaultHandleCreated(in _tuningHandle))
                EnsureVaultBuffers();

            return TryResolveVaultBuffer(
                vault,
                in _tuningHandle,
                KineticCharacterAnimatorConstants.TuningCapacity,
                out tuning);
        }

        public bool TryResolveMatricesForEditor(out NativeArray<float4x4>.ReadOnly matrices, out NativeArray<int>.ReadOnly parents, out int matrixCount)
        {
            matrices = default;
            parents = default;
            matrixCount = 0;
            if (_solverScheduled)
                return false;

            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return false;

            if (!TryResolveVaultBuffer(vault, in _matricesHandle, 1, out NativeArray<float4x4> mutableMatrices) ||
                !TryResolveVaultBuffer(vault, in _parentIndicesHandle, 1, out NativeArray<int> mutableParents))
            {
                return false;
            }

            matrices = mutableMatrices.AsReadOnly();
            parents = mutableParents.AsReadOnly();
            matrixCount = math.min(math.min(_activeMatrixUploadCount, matrices.Length), parents.Length);
            return matrixCount > 0;
        }

#if UNITY_EDITOR
        public bool TryApplyCsvProfile(string csvText)
        {
            if (string.IsNullOrEmpty(csvText) || !TryResolveTuningMutable(out NativeArray<KineticCharacterTuningDTO> tuning))
                return false;

            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return false;

            if (!TryResolveVaultBuffer(vault, in _rigsHandle, 1, out NativeArray<KineticCharacterRigDTO> rigs))
                return false;

            if (rigs[0].BoneCount <= 0)
                GenerateEmergencyMockRig();

            if (!TryResolveVaultBuffer(vault, in _rigsHandle, 1, out rigs))
                return false;

            KineticCharacterTuningDTO tuningDto = tuning[0];
            KineticCharacterRigDTO rig = rigs[0];
            bool result = KineticCharacterRigCsvParser.TryApply(csvText.AsSpan(), ref tuningDto, ref rig);
            tuning[0] = tuningDto;
            rigs[0] = rig;
            return result;
        }

        public bool TryApplyCsvProfileBytes(ReadOnlySpan<byte> csvBytes)
        {
            if (csvBytes.Length <= 0 || !TryResolveTuningMutable(out NativeArray<KineticCharacterTuningDTO> tuning))
                return false;

            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return false;

            if (!TryResolveVaultBuffer(vault, in _rigsHandle, 1, out NativeArray<KineticCharacterRigDTO> rigs))
                return false;

            if (rigs[0].BoneCount <= 0)
                GenerateEmergencyMockRig();

            if (!TryResolveVaultBuffer(vault, in _rigsHandle, 1, out rigs))
                return false;

            KineticCharacterTuningDTO tuningDto = tuning[0];
            KineticCharacterRigDTO rig = rigs[0];
            bool result = KineticCharacterRigCsvParser.TryApply(csvBytes, ref tuningDto, ref rig);
            tuning[0] = tuningDto;
            rigs[0] = rig;
            return result;
        }

        public bool TryApplyCsvProfileFromVaultScratch(int byteCount)
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null || !EnsureVaultBuffers())
                return false;

            if (!TryResolveVaultBuffer(vault, in _csvScratchHandle, 1, out NativeArray<byte> scratch) || byteCount <= 0)
                return false;

            int safeCount = math.min(byteCount, scratch.Length);
            unsafe
            {
                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
                return TryApplyCsvProfileBytes(new ReadOnlySpan<byte>(ptr, safeCount));
            }
        }
#endif

        public void SubmitSwimPresentation(
            float waveForward,
            float waveLateral,
            float crestReach,
            float descentTuck,
            float leanWeight,
            float immersionDepth,
            float breathingPhase,
            float activeToolWeight)
        {
            _submittedWaveForward = math.clamp(waveForward, -1f, 1f);
            _submittedWaveLateral = math.clamp(waveLateral, -1f, 1f);
            _submittedCrestReach = math.saturate(crestReach);
            _submittedDescentTuck = math.saturate(descentTuck);
            _submittedLeanWeight = math.saturate(leanWeight);
            _submittedImmersionDepth = math.max(0f, math.select(0f, immersionDepth, math.isfinite(immersionDepth)));
            _submittedBreathingPhase = math.clamp(breathingPhase, -1f, 1f);
            _submittedToolWeight = math.saturate(activeToolWeight);
        }

        public void SubmitToolPose(float4x4 localToCameraMatrix, float weight01, uint toolHash)
        {
            if (KineticCharacterMath.IsFinite(localToCameraMatrix))
                _submittedToolPose = localToCameraMatrix;

            float weight = math.saturate(weight01);
            _submittedToolWeight = math.max(_submittedToolWeight, weight);
            if (weight > 0.0001f)
                _submittedToolHash = toolHash;
        }

        public void SubmitDamageImpulse(Vector3 localImpulse, float weight01)
        {
            SubmitDamageImpulse(new float3(localImpulse.x, localImpulse.y, localImpulse.z), weight01);
        }

        public void SubmitDamageImpulse(float3 localImpulse, float weight01)
        {
            float weight = math.saturate(weight01);
            if (weight <= 0f)
                return;

            float3 impulse = KineticCharacterMath.SanitizeFinite(localImpulse, float3.zero);
            _submittedDamageImpulseLocal = KineticCharacterMath.SanitizeFinite(_submittedDamageImpulseLocal + impulse * weight, float3.zero);
            _submittedDamageImpulse01 = math.max(_submittedDamageImpulse01, weight);
        }

        private void Awake()
        {
            if (_activeRuntimeInstance == null)
                _activeRuntimeInstance = this;

            RefreshColdDependencies();
            if (EnsureVaultBuffers())
                EnsureGraphicsBuffers();
            if (_seedEmergencyMockRig)
                GenerateEmergencyMockRig();
        }

        private void OnEnable()
        {
            if (_disposed || !Application.isPlaying)
                return;

            CompletePendingSolverForTeardown();
            RefreshColdDependencies();
            if (EnsureVaultBuffers())
                EnsureGraphicsBuffers();
            if (_seedEmergencyMockRig)
                GenerateEmergencyMockRig();
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

            TryUnregister();
            CompletePendingSolverForTeardown();
            UnlockJobBuffers();
            ClearGpuSkinningBinding();
            ReleaseVaultHandles();
            ReleaseGraphicsBuffers();
            ClearHandles();
            if (_activeRuntimeInstance == this)
                _activeRuntimeInstance = null;
            _disposed = true;
        }

        public void Tick(float deltaTime)
        {
            if (_disposed || !Application.isPlaying)
                return;

            if (_solverScheduled && !TryFinalizePendingSolverNoWait())
                return;

            IDataVault vault = _dataVault;
            if (vault == null || !EnsureVaultBuffers())
                return;

            _dataVault = vault;
            float dt = math.clamp(deltaTime, KineticCharacterAnimatorConstants.MinDeltaTime, KineticCharacterAnimatorConstants.MaxDeltaTime);
            _simulationTime += dt;
            float quality = ResolveGlobalQualityWeight(vault);
            _lastQuality = quality;

            if (!TryResolveRuntimeBuffers(
                    vault,
                    out NativeArray<KineticCharacterRigDTO> rigs,
                    out NativeArray<KineticCharacterFrameInputDTO> inputs,
                    out NativeArray<int> parents,
                    out NativeArray<float4x4> bindPoses,
                    out NativeArray<ProceduralBoneDTO> boneOutputs,
                    out NativeArray<float4x4> matrices,
                    out NativeArray<ProceduralIKTargetDTO> ikTargets,
                    out NativeArray<KineticCharacterFrameStatsDTO> stats,
                    out NativeArray<KineticAnimationTelemetryEntry> telemetry,
                    out NativeArray<int> cursor,
                    out NativeArray<KineticCharacterTuningDTO> tuning))
            {
                return;
            }

            bool hasPlayerState = WriteFrameInput(vault, inputs, dt, quality);
            bool includeSdf = TryResolveExternalVaultBuffer(vault, BufferID.VoxelSdfTexture3D, 1, out NativeArray<byte> sdf);
            bool includePlayerHandIk = TryResolveExternalVaultBuffer(
                vault,
                BufferID.PlayerHandIkPublishedStates,
                PlayerHandIkContract.HandCount,
                out NativeArray<IkHandStateDTO> playerHandIkStates);
            if (!TryLockJobBuffers(vault, ref includeSdf, ref includePlayerHandIk))
                return;

            if (!includeSdf)
                sdf = default;
            if (!includePlayerHandIk)
                playerHandIkStates = default;

            JobHandle dependency = default;
            if (!hasPlayerState)
            {
                dependency = new MockCharacterKinematicsJob
                {
                    Inputs = inputs,
                    Frame = _frameCounter,
                    DeltaTime = dt,
                    SimulationTime = _simulationTime,
                    GlobalQualityWeight = quality
                }.Schedule(KineticCharacterAnimatorConstants.CharacterCapacity, 1, dependency);
            }

            JobHandle ikHandle = new EvaluateWallProximityJob
            {
                Inputs = inputs,
                Targets = ikTargets,
                VoxelSdfTexture3D = sdf,
                SdfDimensions = new int3(math.max(0, _sdfDimensions.x), math.max(0, _sdfDimensions.y), math.max(0, _sdfDimensions.z)),
                SdfOrigin = new float3(_sdfOrigin.x, _sdfOrigin.y, _sdfOrigin.z),
                SdfCellSize = new float3(math.max(0.0001f, _sdfCellSize.x), math.max(0.0001f, _sdfCellSize.y), math.max(0.0001f, _sdfCellSize.z)),
                SdfRangeMeters = math.max(0.01f, _sdfRangeMeters),
                WallBraceDistanceMeters = tuning.IsCreated && tuning.Length > 0 ? KineticCharacterSanitizer.SanitizeTuning(tuning[0]).WallBraceDistanceMeters : 0.72f,
                AupSectorSizeMeters = HectonPhysicsContract.AupSectorSizeMetersDouble
            }.Schedule(KineticCharacterAnimatorConstants.CharacterCapacity, 1, dependency);

            JobHandle solveHandle = new ProceduralLocomotionPhaseJob
            {
                Rigs = rigs,
                Inputs = inputs,
                ParentIndices = parents,
                BindPoses = bindPoses,
                BoneOutputs = boneOutputs,
                Stats = stats,
                IkTargets = ikTargets,
                Tuning = tuning,
                GlobalQualityWeight = quality,
                DeltaTime = dt,
                SimulationTime = _simulationTime,
                Frame = _frameCounter,
                AupSectorSizeMeters = HectonPhysicsContract.AupSectorSizeMetersDouble
            }.Schedule(KineticCharacterAnimatorConstants.CharacterCapacity, 1, ikHandle);

            JobHandle boneOutputHandle = solveHandle;
            if (includePlayerHandIk)
            {
                boneOutputHandle = new ApplyPlayerHandIkToKineticBonesJob
                {
                    Rigs = rigs,
                    Inputs = inputs,
                    PlayerHandIkStates = playerHandIkStates,
                    BoneOutputs = boneOutputs,
                    Stats = stats,
                    AupSectorSizeMeters = HectonPhysicsContract.AupSectorSizeMetersDouble
                }.Schedule(KineticCharacterAnimatorConstants.CharacterCapacity, 1, solveHandle);
            }

            JobHandle matrixHandle = new ComputeFinalBoneMatricesJob
            {
                BoneOutputs = boneOutputs,
                Matrices = matrices
            }.Schedule(math.min(_boneCapacity, matrices.Length), 32, boneOutputHandle);

            _pendingHandle = new KineticAnimationTelemetryJob
            {
                Stats = stats,
                Inputs = inputs,
                Telemetry = telemetry,
                Cursor = cursor,
                Frame = _frameCounter
            }.Schedule(matrixHandle);

            H8Memory.RegisterActiveJob(SystemID.AnimationLocomotion, _pendingHandle);
            _solverScheduled = true;
        }

        public void LateFrameTick()
        {
            if (_disposed || !Application.isPlaying)
                return;

            TryFinalizePendingSolverNoWait();
            if (_gpuUploadDirty)
                UploadMatricesToGpu();
            else if (_gpuConstantsDirty)
                PublishCurrentGpuSkinningBinding();
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            BindDataVaultForLifecycle(currentService as IDataVault, previousService as IDataVault);
            ClearGpuSkinningBinding();
            if (_dataVault != null)
            {
                EnsureVaultBuffers();
                if (_seedEmergencyMockRig)
                    GenerateEmergencyMockRig();
            }
        }

        public void GenerateEmergencyMockRig()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null || !EnsureVaultBuffers())
                return;

            int boneCapacity = math.clamp(_boneCapacity, KineticCharacterAnimatorConstants.EmergencyMockBoneCount, KineticCharacterAnimatorConstants.DefaultBoneCapacity);
            if (!TryResolveVaultBuffer(vault, in _rigsHandle, 1, out NativeArray<KineticCharacterRigDTO> rigs) ||
                !TryResolveVaultBuffer(vault, in _parentIndicesHandle, boneCapacity, out NativeArray<int> parents) ||
                !TryResolveVaultBuffer(vault, in _bindPosesHandle, boneCapacity, out NativeArray<float4x4> bindPoses) ||
                !TryResolveVaultBuffer(vault, in _tuningHandle, KineticCharacterAnimatorConstants.TuningCapacity, out NativeArray<KineticCharacterTuningDTO> tuning) ||
                !TryResolveVaultBuffer(vault, in _inputsHandle, KineticCharacterAnimatorConstants.CharacterCapacity, out NativeArray<KineticCharacterFrameInputDTO> inputs) ||
                !TryResolveVaultBuffer(vault, in _ikTargetsHandle, KineticCharacterAnimatorConstants.CharacterCapacity * KineticCharacterAnimatorConstants.IkTargetCount, out NativeArray<ProceduralIKTargetDTO> targets))
            {
                return;
            }

            KineticCharacterRigDTO rig = default;
            rig.SkeletonHash = 0x53484E88u;
            rig.Flags = KineticCharacterAnimatorConstants.RigFlagEmergencyMock |
                        KineticCharacterAnimatorConstants.RigFlagVisible |
                        KineticCharacterAnimatorConstants.RigFlagHasToolSocket;
            rig.BoneStart = 0;
            rig.BoneCount = KineticCharacterAnimatorConstants.EmergencyMockBoneCount;
            rig.RootIndex = 0;
            rig.SpineIndex = 1;
            rig.ChestIndex = 2;
            rig.NeckIndex = 3;
            rig.HeadIndex = 4;
            rig.LeftShoulderIndex = 5;
            rig.LeftElbowIndex = 6;
            rig.LeftHandIndex = 7;
            rig.RightShoulderIndex = 8;
            rig.RightElbowIndex = 9;
            rig.RightHandIndex = 10;
            rig.LeftHipIndex = 11;
            rig.LeftKneeIndex = 12;
            rig.LeftFootIndex = 13;
            rig.RightHipIndex = 14;
            rig.RightKneeIndex = 15;
            rig.RightFootIndex = 16;
            rig.ToolSocketIndex = 17;
            rig.ShoulderWidth = 0.42f;
            rig.HipWidth = 0.32f;
            rig.ArmUpperLength = 0.34f;
            rig.ArmLowerLength = 0.32f;
            rig.LegUpperLength = 0.46f;
            rig.LegLowerLength = 0.45f;
            rig.SpineLength = 0.54f;
            rig.NeckLength = 0.12f;
            rig.BreathAmplitudeMeters = 0.01f;
            rig.LocomotionAmplitudeMeters = 0.08f;
            rig.DamageDecayHz = 1f;
            rig.StableSeed = 0x53484E42u;
            rig.ActiveBoneCount = rig.BoneCount;
            rig.MaxIkIterations = 6;
            rigs[0] = rig;

            WriteParent(parents, 0, -1);
            WriteParent(parents, 1, 0);
            WriteParent(parents, 2, 1);
            WriteParent(parents, 3, 2);
            WriteParent(parents, 4, 3);
            WriteParent(parents, 5, 2);
            WriteParent(parents, 6, 5);
            WriteParent(parents, 7, 6);
            WriteParent(parents, 8, 2);
            WriteParent(parents, 9, 8);
            WriteParent(parents, 10, 9);
            WriteParent(parents, 11, 0);
            WriteParent(parents, 12, 11);
            WriteParent(parents, 13, 12);
            WriteParent(parents, 14, 0);
            WriteParent(parents, 15, 14);
            WriteParent(parents, 16, 15);
            WriteParent(parents, 17, 10);

            for (int i = 0; i < math.min(bindPoses.Length, _boneCapacity); i++)
                bindPoses[i] = float4x4.identity;

            tuning[0] = KineticCharacterSanitizer.SanitizeTuning(tuning[0].LocomotionFrequencyHz > 0f ? tuning[0] : KineticCharacterTuningDTO.Default());
            KineticCharacterFrameInputDTO input = default;
            input.RootRotation = quaternion.identity;
            input.ToolPoseMatrix = float4x4.identity;
            input.Visible01 = 1f;
            input.GlobalQualityWeight = 1f;
            input.OxygenLevel01 = 1f;
            input.CameraForwardLocal = new float3(0f, 0f, 1f);
            input.Flags = KineticCharacterAnimatorConstants.InputFlagVisible | KineticCharacterAnimatorConstants.InputFlagMock;
            inputs[0] = input;

            for (int i = 0; i < targets.Length; i++)
                targets[i] = default;
        }

        private static void WriteParent(NativeArray<int> parents, int child, int parent)
        {
            if ((uint)child < (uint)parents.Length)
                parents[child] = parent;
        }

        private void RefreshColdDependencies()
        {
            CacheDataVaultCold();
            if (_cameraTransform == null)
            {
                Camera camera = GetComponentInChildren<Camera>();
                if (camera != null)
                    _cameraTransform = camera.transform;
            }
        }

        private bool EnsureVaultBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !KineticCharacterAnimatorLayout.Validate())
                return false;

            _dataVault = vault;
            int boneCapacity = math.clamp(_boneCapacity, KineticCharacterAnimatorConstants.EmergencyMockBoneCount, KineticCharacterAnimatorConstants.DefaultBoneCapacity);
            bool resolved = TryResolveOrAcquireVaultBuffer(
                vault,
                KineticCharacterAnimatorBufferIds.Rigs,
                KineticCharacterAnimatorConstants.CharacterCapacity,
                ref _rigsHandle,
                out _) &&
            TryResolveOrAcquireVaultBuffer(
                vault,
                KineticCharacterAnimatorBufferIds.FrameInputs,
                KineticCharacterAnimatorConstants.CharacterCapacity,
                ref _inputsHandle,
                out _) &&
            TryResolveOrAcquireVaultBuffer(
                vault,
                KineticCharacterAnimatorBufferIds.ParentIndices,
                boneCapacity,
                ref _parentIndicesHandle,
                out _) &&
            TryResolveOrAcquireVaultBuffer(
                vault,
                KineticCharacterAnimatorBufferIds.BindPoses,
                boneCapacity,
                ref _bindPosesHandle,
                out _) &&
            TryResolveOrAcquireVaultBuffer(
                vault,
                KineticCharacterAnimatorBufferIds.BoneOutputs,
                boneCapacity,
                ref _boneOutputsHandle,
                out _) &&
            TryResolveOrAcquireVaultBuffer(
                vault,
                KineticCharacterAnimatorBufferIds.BoneMatrices,
                boneCapacity,
                ref _matricesHandle,
                out _) &&
            TryResolveOrAcquireVaultBuffer(
                vault,
                KineticCharacterAnimatorBufferIds.IkTargets,
                KineticCharacterAnimatorConstants.CharacterCapacity * KineticCharacterAnimatorConstants.IkTargetCount,
                ref _ikTargetsHandle,
                out _) &&
            TryResolveOrAcquireVaultBuffer(
                vault,
                KineticCharacterAnimatorBufferIds.FrameStats,
                KineticCharacterAnimatorConstants.CharacterCapacity,
                ref _statsHandle,
                out _,
                NativeArrayOptions.ClearMemory) &&
            TryResolveOrAcquireVaultBuffer(
                vault,
                KineticCharacterAnimatorBufferIds.TelemetryRing,
                KineticCharacterAnimatorConstants.TelemetryCapacity,
                ref _telemetryHandle,
                out _,
                NativeArrayOptions.ClearMemory) &&
            TryResolveOrAcquireVaultBuffer(
                vault,
                KineticCharacterAnimatorBufferIds.TelemetryCursor,
                1,
                ref _telemetryCursorHandle,
                out _,
                NativeArrayOptions.ClearMemory) &&
            TryResolveOrAcquireVaultBuffer(
                vault,
                KineticCharacterAnimatorBufferIds.Tuning,
                KineticCharacterAnimatorConstants.TuningCapacity,
                ref _tuningHandle,
                out _,
                NativeArrayOptions.ClearMemory);
#if UNITY_EDITOR
            resolved = resolved &&
            TryResolveOrAcquireVaultBuffer(
                vault,
                KineticCharacterAnimatorBufferIds.CsvScratch,
                KineticCharacterAnimatorConstants.CsvScratchBytes,
                ref _csvScratchHandle,
                out _);
#endif

            if (!resolved)
                return false;

            if (TryResolveVaultBuffer(
                    vault,
                    in _tuningHandle,
                    KineticCharacterAnimatorConstants.TuningCapacity,
                    out NativeArray<KineticCharacterTuningDTO> tuning) &&
                tuning.Length > 0)
            {
                tuning[0] = KineticCharacterSanitizer.SanitizeTuning(tuning[0].LocomotionFrequencyHz > 0f ? tuning[0] : KineticCharacterTuningDTO.Default());
            }

            return true;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static bool TryResolveVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   IsVaultHandleCreated(in handle) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryResolveExternalVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) &&
                   TryResolveVaultBuffer(vault, in handle, requiredLength, out buffer);
        }

        private static bool TryResolveOrAcquireVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where T : struct
        {
            buffer = default;
            if (IsOwnedVaultHandle(in handle, bufferId) &&
                TryResolveVaultBuffer(vault, in handle, requiredLength, out buffer))
            {
                return true;
            }

            if (vault == null)
                return false;

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.AnimationLocomotion,
                options);
            if (!TryResolveVaultBuffer(vault, in acquired, requiredLength, out buffer))
                return false;

            handle = acquired;
            return true;
        }

        private bool TryResolveRuntimeBuffers(
            IDataVault vault,
            out NativeArray<KineticCharacterRigDTO> rigs,
            out NativeArray<KineticCharacterFrameInputDTO> inputs,
            out NativeArray<int> parents,
            out NativeArray<float4x4> bindPoses,
            out NativeArray<ProceduralBoneDTO> boneOutputs,
            out NativeArray<float4x4> matrices,
            out NativeArray<ProceduralIKTargetDTO> ikTargets,
            out NativeArray<KineticCharacterFrameStatsDTO> stats,
            out NativeArray<KineticAnimationTelemetryEntry> telemetry,
            out NativeArray<int> cursor,
            out NativeArray<KineticCharacterTuningDTO> tuning)
        {
            rigs = default;
            inputs = default;
            parents = default;
            bindPoses = default;
            boneOutputs = default;
            matrices = default;
            ikTargets = default;
            stats = default;
            telemetry = default;
            cursor = default;
            tuning = default;

            if (vault == null)
                return false;

            int boneCapacity = math.clamp(_boneCapacity, KineticCharacterAnimatorConstants.EmergencyMockBoneCount, KineticCharacterAnimatorConstants.DefaultBoneCapacity);
            return TryResolveVaultBuffer(vault, in _rigsHandle, KineticCharacterAnimatorConstants.CharacterCapacity, out rigs) &&
                   TryResolveVaultBuffer(vault, in _inputsHandle, KineticCharacterAnimatorConstants.CharacterCapacity, out inputs) &&
                   TryResolveVaultBuffer(vault, in _parentIndicesHandle, boneCapacity, out parents) &&
                   TryResolveVaultBuffer(vault, in _bindPosesHandle, boneCapacity, out bindPoses) &&
                   TryResolveVaultBuffer(vault, in _boneOutputsHandle, boneCapacity, out boneOutputs) &&
                   TryResolveVaultBuffer(vault, in _matricesHandle, boneCapacity, out matrices) &&
                   TryResolveVaultBuffer(vault, in _ikTargetsHandle, KineticCharacterAnimatorConstants.CharacterCapacity * KineticCharacterAnimatorConstants.IkTargetCount, out ikTargets) &&
                   TryResolveVaultBuffer(vault, in _statsHandle, KineticCharacterAnimatorConstants.CharacterCapacity, out stats) &&
                   TryResolveVaultBuffer(vault, in _telemetryHandle, KineticCharacterAnimatorConstants.TelemetryCapacity, out telemetry) &&
                   TryResolveVaultBuffer(vault, in _telemetryCursorHandle, 1, out cursor) &&
                   TryResolveVaultBuffer(vault, in _tuningHandle, KineticCharacterAnimatorConstants.TuningCapacity, out tuning);
        }

        private bool WriteFrameInput(IDataVault vault, NativeArray<KineticCharacterFrameInputDTO> inputs, float dt, float quality)
        {
            if (!TryResolveExternalVaultBuffer(
                    vault,
                    BufferID.PlayerKinematicState,
                    1,
                    out NativeArray<LockstepPlayerKinematicState> playerStates))
            {
                return false;
            }

            LockstepPlayerKinematicState player = playerStates[0];
            KineticCharacterFrameInputDTO input = inputs[0];
            input.RootSectorX = player.SectorX;
            input.RootSectorY = player.SectorY;
            input.RootSectorZ = player.SectorZ;
            input.RootLocalPosition = player.LocalPosition;
            input.GlobalQualityWeight = quality;
            input.RootRotation = ResolveRootRotation(player.Forward);
            input.VelocityLocal = player.Velocity;
            input.Visible01 = 1f;
            input.CameraSectorX = player.SectorX;
            input.CameraSectorY = player.SectorY;
            input.CameraSectorZ = player.SectorZ;
            input.CameraLocalPosition = ResolveCameraLocal(player.LocalPosition);
            input.StressLevel01 = 1f - quality;
            input.CameraForwardLocal = ResolveCameraForward(input.RootRotation);
            input.OxygenLevel01 = 1f;
            input.DamageImpulseLocal = _submittedDamageImpulseLocal;
            input.DamageImpulse01 = _submittedDamageImpulse01;
            input.ToolPoseMatrix = _submittedToolPose;
            input.SimulationTickDelta = dt;
            input.SimulationTime = _simulationTime;
            input.SwimWaveForward = _submittedWaveForward;
            input.SwimWaveLateral = _submittedWaveLateral;
            input.SwimCrestReach = _submittedCrestReach;
            input.SwimDescentTuck = _submittedDescentTuck;
            input.SwimLeanWeight = _submittedLeanWeight;
            input.ImmersionDepth = _submittedImmersionDepth;
            input.BreathingPhase = _submittedBreathingPhase;
            input.ActiveToolWeight01 = _submittedToolWeight;
            input.ActiveToolHash = _submittedToolHash;
            input.Frame = _frameCounter;
            input.Flags = KineticCharacterAnimatorConstants.InputFlagVisible;
            if (_submittedToolWeight > 0.0001f)
                input.Flags |= KineticCharacterAnimatorConstants.InputFlagToolActive;
            if (_submittedToolHash != 0u)
                input.Flags |= KineticCharacterAnimatorConstants.InputFlagToolHashValid;
            if (_submittedDamageImpulse01 > 0.0001f)
                input.Flags |= KineticCharacterAnimatorConstants.InputFlagDamageImpulse;
            if (_submittedLeanWeight > 0.0001f || _submittedCrestReach > 0.0001f || _submittedDescentTuck > 0.0001f)
                input.Flags |= KineticCharacterAnimatorConstants.InputFlagSurfaceSwim;
            inputs[0] = input;

            _submittedDamageImpulseLocal = math.lerp(_submittedDamageImpulseLocal, float3.zero, math.saturate(dt * 8f));
            _submittedDamageImpulse01 = math.max(0f, _submittedDamageImpulse01 - dt * 3f);
            _submittedToolWeight = math.saturate(_submittedToolWeight * MathLodApproximation.ApproxExpNegPade33Wide40(dt * 2f));
            if (_submittedToolWeight <= 0.0001f)
                _submittedToolHash = 0u;
            return true;
        }

        private quaternion ResolveRootRotation(float3 forward)
        {
            Vector3 fallback = transform.forward;
            float3 safeForward = KineticCharacterMath.NormalizeSafe(forward, new float3(fallback.x, fallback.y, fallback.z));
            return quaternion.LookRotationSafe(safeForward, KineticCharacterMath.Float3(0f, 1f, 0f));
        }

        private float3 ResolveCameraLocal(float3 fallbackRootLocal)
        {
            if (_cameraTransform == null)
                return fallbackRootLocal + KineticCharacterMath.Float3(0f, 1.58f, -0.08f);

            Vector3 cameraPosition = _cameraTransform.position;
            return new float3(cameraPosition.x, cameraPosition.y, cameraPosition.z);
        }

        private float3 ResolveCameraForward(quaternion fallbackRotation)
        {
            if (_cameraTransform == null)
                return math.mul(fallbackRotation, KineticCharacterMath.Float3(0f, 0f, 1f));

            Vector3 forward = _cameraTransform.forward;
            return KineticCharacterMath.NormalizeSafe(new float3(forward.x, forward.y, forward.z), math.mul(fallbackRotation, KineticCharacterMath.Float3(0f, 0f, 1f)));
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

            if (!DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true))
                return false;

            return FinishPendingSolverCompletion();
        }

        private bool FinishPendingSolverCompletion()
        {
            _solverScheduled = false;
            UnlockJobBuffers();
            _frameCounter++;
            ReadLatestTelemetry();
            _gpuUploadDirty = ShouldUploadMatrices();
            if (!_dumpedFault && LatestTelemetryHasInvalidFlag())
                DumpBlackBoxOnce();
            return true;
        }

        private bool TryLockJobBuffers(IDataVault vault, ref bool includeSdf, ref bool includePlayerHandIk)
        {
            _lockedBuffers = 0;
            if (!TryLockRequired(vault, KineticCharacterAnimatorBufferIds.Rigs, LockRigs) ||
                !TryLockRequired(vault, KineticCharacterAnimatorBufferIds.FrameInputs, LockInputs) ||
                !TryLockRequired(vault, KineticCharacterAnimatorBufferIds.ParentIndices, LockParents) ||
                !TryLockRequired(vault, KineticCharacterAnimatorBufferIds.BindPoses, LockBindPoses) ||
                !TryLockRequired(vault, KineticCharacterAnimatorBufferIds.BoneOutputs, LockBoneOutputs) ||
                !TryLockRequired(vault, KineticCharacterAnimatorBufferIds.BoneMatrices, LockMatrices) ||
                !TryLockRequired(vault, KineticCharacterAnimatorBufferIds.IkTargets, LockIkTargets) ||
                !TryLockRequired(vault, KineticCharacterAnimatorBufferIds.FrameStats, LockStats) ||
                !TryLockRequired(vault, KineticCharacterAnimatorBufferIds.TelemetryRing, LockTelemetry) ||
                !TryLockRequired(vault, KineticCharacterAnimatorBufferIds.TelemetryCursor, LockCursor) ||
                !TryLockRequired(vault, KineticCharacterAnimatorBufferIds.Tuning, LockTuning))
            {
                UnlockJobBuffers();
                includeSdf = false;
                return false;
            }

            if (includeSdf && vault.TryLockBuffer(BufferID.VoxelSdfTexture3D, SystemID.AnimationLocomotion))
                _lockedBuffers |= LockSdf;
            else
                includeSdf = false;

            if (includePlayerHandIk && vault.TryLockBuffer(BufferID.PlayerHandIkPublishedStates, SystemID.AnimationLocomotion))
                _lockedBuffers |= LockPlayerHandIkStates;
            else
                includePlayerHandIk = false;

            return true;
        }

        private IDataVault CacheDataVaultCold()
        {
            BindDataVaultForLifecycle(GlobalRegistry.DataVault, null);
            return _dataVault;
        }

        private void BindDataVaultForLifecycle(IDataVault currentVault, IDataVault releaseVaultOverride)
        {
            if (ReferenceEquals(_dataVault, currentVault))
                return;

            CompletePendingSolverForTeardown();
            UnlockJobBuffers();
            ReleaseVaultHandles(_dataVault ?? releaseVaultOverride);
            ClearHandles();
            _dataVault = currentVault;
            _dumpedFault = false;
            _latestStateHash = 0u;
            _uploadedStateHash = 0u;
            _activeMatrixUploadCount = 0;
            _uploadedMatrixCount = 0;
            _activeCharacterCount = 0;
            _uploadedCharacterCount = 0;
            _lastQuality = 1f;
            _uploadedQuality = -1f;
            _gpuUploadDirty = true;
            _gpuConstantsDirty = true;
            _gpuBufferDataValid = false;
        }

        private bool TryLockRequired(IDataVault vault, BufferID bufferId, int bit)
        {
            if (vault.TryLockBuffer(bufferId, SystemID.AnimationLocomotion))
            {
                _lockedBuffers |= bit;
                return true;
            }

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

            Unlock(vault, KineticCharacterAnimatorBufferIds.Rigs, LockRigs);
            Unlock(vault, KineticCharacterAnimatorBufferIds.FrameInputs, LockInputs);
            Unlock(vault, KineticCharacterAnimatorBufferIds.ParentIndices, LockParents);
            Unlock(vault, KineticCharacterAnimatorBufferIds.BindPoses, LockBindPoses);
            Unlock(vault, KineticCharacterAnimatorBufferIds.BoneOutputs, LockBoneOutputs);
            Unlock(vault, KineticCharacterAnimatorBufferIds.BoneMatrices, LockMatrices);
            Unlock(vault, KineticCharacterAnimatorBufferIds.IkTargets, LockIkTargets);
            Unlock(vault, KineticCharacterAnimatorBufferIds.FrameStats, LockStats);
            Unlock(vault, KineticCharacterAnimatorBufferIds.TelemetryRing, LockTelemetry);
            Unlock(vault, KineticCharacterAnimatorBufferIds.TelemetryCursor, LockCursor);
            Unlock(vault, KineticCharacterAnimatorBufferIds.Tuning, LockTuning);
            Unlock(vault, BufferID.VoxelSdfTexture3D, LockSdf);
            Unlock(vault, BufferID.PlayerHandIkPublishedStates, LockPlayerHandIkStates);
            _lockedBuffers = 0;
        }

        private void Unlock(IDataVault vault, BufferID bufferId, int bit)
        {
            if ((_lockedBuffers & bit) != 0)
                vault.TryUnlockBuffer(bufferId, SystemID.AnimationLocomotion);
        }

        private float ResolveGlobalQualityWeight(IDataVault vault)
        {
            float quality = math.saturate(HomeostasisBrain.GlobalQualityWeight);
            if (TryResolveVaultBuffer(
                    vault,
                    in _tuningHandle,
                    KineticCharacterAnimatorConstants.TuningCapacity,
                    out NativeArray<KineticCharacterTuningDTO> tuning))
            {
                if (tuning.Length > 0)
                    quality = math.min(quality, KineticCharacterSanitizer.SanitizeTuning(tuning[0]).GlobalQualityWeight);
            }

            return math.saturate(math.select(_lastQuality, quality, math.isfinite(quality)));
        }

        private void ReadLatestTelemetry()
        {
            _activeMatrixUploadCount = 0;
            _activeCharacterCount = 0;
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (!TryResolveVaultBuffer(vault, in _telemetryHandle, 1, out NativeArray<KineticAnimationTelemetryEntry> telemetry) ||
                !TryResolveVaultBuffer(vault, in _telemetryCursorHandle, 1, out NativeArray<int> cursor) ||
                telemetry.Length <= 0 ||
                cursor[0] <= 0)
                return;

            int index = KineticCharacterMath.PositiveModulo(cursor[0] - 1, telemetry.Length);
            KineticAnimationTelemetryEntry entry = telemetry[index];
            int matrixCount = math.clamp(entry.BonesEvaluated, 0, _boneCapacity);
            int activeCharacters = entry.BonesEvaluated > 0 ? 1 : 0;
            float quality = math.saturate(math.select(_lastQuality, entry.GlobalQualityWeight, math.isfinite(entry.GlobalQualityWeight)));
            _gpuConstantsDirty |= matrixCount != _uploadedMatrixCount ||
                                  activeCharacters != _uploadedCharacterCount ||
                                  math.abs(quality - _uploadedQuality) > 0.0001f;
            _activeMatrixUploadCount = matrixCount;
            _activeCharacterCount = activeCharacters;
            _latestStateHash = entry.StateHash;
            _lastQuality = quality;
        }

        private bool ShouldUploadMatrices()
        {
            return _activeMatrixUploadCount > 0 &&
                   (!_gpuBufferDataValid ||
                    _activeMatrixUploadCount != _uploadedMatrixCount ||
                    _latestStateHash != _uploadedStateHash);
        }

        private bool LatestTelemetryHasInvalidFlag()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!TryResolveVaultBuffer(vault, in _telemetryHandle, 1, out NativeArray<KineticAnimationTelemetryEntry> telemetry) ||
                !TryResolveVaultBuffer(vault, in _telemetryCursorHandle, 1, out NativeArray<int> cursor) ||
                telemetry.Length <= 0 ||
                cursor[0] <= 0)
                return false;

            int index = KineticCharacterMath.PositiveModulo(cursor[0] - 1, telemetry.Length);
            return (telemetry[index].Flags & KineticCharacterAnimatorConstants.TelemetryFlagInvalid) != 0u;
        }

        private void DumpBlackBoxOnce()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (!TryResolveVaultBuffer(vault, in _telemetryHandle, 1, out NativeArray<KineticAnimationTelemetryEntry> telemetry) ||
                !TryResolveVaultBuffer(vault, in _telemetryCursorHandle, 1, out NativeArray<int> cursor))
            {
                return;
            }

            _dumpedFault = KineticCharacterBlackBox.TryDumpTelemetry(ResolveProjectRoot(), telemetry, cursor);
        }

        private bool UploadMatricesToGpu()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                ClearGpuSkinningBinding();
                return false;
            }

            if (!TryResolveVaultBuffer(vault, in _matricesHandle, 1, out NativeArray<float4x4> matrices))
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

            KineticCharacterGraphicsBufferUpload.UploadNativeArray(writeBuffer, matrices, count);
            PublishGpuSkinningBinding(writeBuffer, count);
            _gpuUploadBufferIndex ^= 1;
            _gpuBufferDataValid = true;
            _uploadedStateHash = _latestStateHash;
            _uploadedMatrixCount = count;
            _uploadedCharacterCount = _activeCharacterCount;
            _uploadedQuality = _lastQuality;
            _gpuConstantsDirty = false;
            _gpuUploadDirty = false;
            return true;
        }

        private bool PublishCurrentGpuSkinningBinding()
        {
            int count = _uploadedMatrixCount;
            GraphicsBuffer buffer = _gpuUploadBufferIndex == 0 ? _matrixBufferB : _matrixBufferA;
            if (!_gpuBufferDataValid || count <= 0 || (_gpuSkinningMaterial == null && !_publishGlobalBuffer) || !HasValidGraphicsBuffer(buffer, count))
                return false;

            PublishGpuSkinningBinding(buffer, count);
            _uploadedCharacterCount = _activeCharacterCount;
            _uploadedQuality = _lastQuality;
            _gpuConstantsDirty = false;
            return true;
        }

        private void PublishGpuSkinningBinding(GraphicsBuffer buffer, int count)
        {
            if (_gpuSkinningMaterial != null)
            {
                _gpuSkinningMaterial.SetBuffer(KineticBoneMatricesId, buffer);
                _gpuSkinningMaterial.SetFloat(KineticBoneMatrixCountId, count);
                _gpuSkinningMaterial.SetFloat(KineticActiveCharactersId, _activeCharacterCount);
                _gpuSkinningMaterial.SetFloat(KineticQualityId, _lastQuality);
                _gpuSkinningMaterial.SetFloat(KineticGpuSkinningId, 1f);
            }

            if (_publishGlobalBuffer)
            {
                Shader.SetGlobalBuffer(KineticBoneMatricesId, buffer);
                Shader.SetGlobalFloat(KineticBoneMatrixCountId, count);
                Shader.SetGlobalFloat(KineticActiveCharactersId, _activeCharacterCount);
                Shader.SetGlobalFloat(KineticQualityId, _lastQuality);
                Shader.SetGlobalFloat(KineticGpuSkinningId, 1f);
                _globalGpuSkinningPublished = true;
            }
        }

        private void EnsureGraphicsBuffers()
        {
            int count = math.clamp(_boneCapacity, KineticCharacterAnimatorConstants.EmergencyMockBoneCount, KineticCharacterAnimatorConstants.DefaultBoneCapacity);
            if (!HasValidGraphicsBuffer(_matrixBufferA, count))
            {
                ReleaseGraphicsBuffer(ref _matrixBufferA);
                _matrixBufferA = KineticCharacterGraphicsBufferUpload.CreateStructuredLockBuffer<float4x4>(count);
                _gpuBufferDataValid = false;
            }

            if (!HasValidGraphicsBuffer(_matrixBufferB, count))
            {
                ReleaseGraphicsBuffer(ref _matrixBufferB);
                _matrixBufferB = KineticCharacterGraphicsBufferUpload.CreateStructuredLockBuffer<float4x4>(count);
                _gpuBufferDataValid = false;
            }
        }

        private void ClearGpuSkinningBinding()
        {
            _gpuBufferDataValid = false;
            _gpuUploadDirty = false;
            _gpuConstantsDirty = false;
            _uploadedStateHash = 0u;
            _uploadedMatrixCount = 0;
            _uploadedCharacterCount = 0;
            _uploadedQuality = -1f;
            if (_gpuSkinningMaterial != null)
            {
                _gpuSkinningMaterial.SetFloat(KineticBoneMatrixCountId, 0f);
                _gpuSkinningMaterial.SetFloat(KineticActiveCharactersId, 0f);
                _gpuSkinningMaterial.SetFloat(KineticQualityId, 0f);
                _gpuSkinningMaterial.SetFloat(KineticGpuSkinningId, 0f);
            }

            if (_publishGlobalBuffer || _globalGpuSkinningPublished)
            {
                Shader.SetGlobalFloat(KineticBoneMatrixCountId, 0f);
                Shader.SetGlobalFloat(KineticActiveCharactersId, 0f);
                Shader.SetGlobalFloat(KineticQualityId, 0f);
                Shader.SetGlobalFloat(KineticGpuSkinningId, 0f);
                _globalGpuSkinningPublished = false;
            }
        }

        private void ReleaseGraphicsBuffers()
        {
            ReleaseGraphicsBuffer(ref _matrixBufferA);
            ReleaseGraphicsBuffer(ref _matrixBufferB);
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

            ReleaseVaultHandle(vault, ref _rigsHandle);
            ReleaseVaultHandle(vault, ref _inputsHandle);
            ReleaseVaultHandle(vault, ref _parentIndicesHandle);
            ReleaseVaultHandle(vault, ref _bindPosesHandle);
            ReleaseVaultHandle(vault, ref _boneOutputsHandle);
            ReleaseVaultHandle(vault, ref _matricesHandle);
            ReleaseVaultHandle(vault, ref _ikTargetsHandle);
            ReleaseVaultHandle(vault, ref _statsHandle);
            ReleaseVaultHandle(vault, ref _telemetryHandle);
            ReleaseVaultHandle(vault, ref _telemetryCursorHandle);
            ReleaseVaultHandle(vault, ref _tuningHandle);
#if UNITY_EDITOR
            ReleaseVaultHandle(vault, ref _csvScratchHandle);
#endif
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (IsOwnedVaultHandle(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsOwnedVaultHandle<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return IsVaultHandleCreated(in handle) &&
                   handle.SystemID == (uint)SystemID.AnimationLocomotion;
        }

        private static bool IsOwnedVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return IsOwnedVaultHandle(in handle) &&
                   handle.BufferID == (uint)expectedBufferId;
        }

        private void ClearHandles()
        {
            _rigsHandle = default;
            _inputsHandle = default;
            _parentIndicesHandle = default;
            _bindPosesHandle = default;
            _boneOutputsHandle = default;
            _matricesHandle = default;
            _ikTargetsHandle = default;
            _statsHandle = default;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _tuningHandle = default;
#if UNITY_EDITOR
            _csvScratchHandle = default;
#endif
        }

        private void TryRegister()
        {
            if (!_registeredUpdate)
                _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
            if (!_registeredHotSwap)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregister()
        {
            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registeredUpdate = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrame = false;
            }

            if (_registeredHotSwap)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
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
            if (!TryResolveMatricesForEditor(out NativeArray<float4x4>.ReadOnly matrices, out NativeArray<int>.ReadOnly parents, out int count))
                return;

            int drawCount = math.min(count, 128);
            for (int i = 0; i < drawCount; i++)
            {
                int parent = parents[i];
                if (parent < 0 || parent >= drawCount)
                    continue;

                Vector3 a = (Vector3)matrices[i].c3.xyz;
                Vector3 b = (Vector3)matrices[parent].c3.xyz;
                Gizmos.color = i >= 5 && i <= 10 ? Color.red : Color.cyan;
                Gizmos.DrawLine(a, b);
            }
        }
    }

    internal static class KineticCharacterGraphicsBufferUpload
    {
        public static GraphicsBuffer CreateStructuredLockBuffer<T>(int count) where T : unmanaged
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                UnsafeUtility.SizeOf<T>());
        }

        public static void UploadNativeArray<T>(GraphicsBuffer destination, NativeArray<T> source, int count) where T : unmanaged
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

        private static int ResolveSafeWriteCount<T>(GraphicsBuffer destination, int sourceLength, int requestedCount) where T : unmanaged
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
