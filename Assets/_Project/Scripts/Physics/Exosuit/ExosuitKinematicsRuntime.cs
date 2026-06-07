using System;
#if UNITY_EDITOR
using System.IO;
#endif
using Stopwatch = System.Diagnostics.Stopwatch;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Core.Scheduling;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics.Exosuit
{
    /// <summary>
    /// Runtime bridge that feeds the Burst exosuit SDF solver from DataVault buffers.
    /// </summary>
    [DisallowMultipleComponent]
    // Runs before general player presentation so late-frame readback can publish tactile and acoustic signals in the same visual frame.
    [DefaultExecutionOrder(-9827)]
    public sealed class ExosuitKinematicsRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001DirectSignalPushDropCount_ExosuitKinematicsRuntime;

        private const int TelemetryCapacity = 300;
#if UNITY_EDITOR
        private const int CsvScratchCapacity = 4096;
#endif
        private const uint ExoSourceHash = 0x53484E34u; // SHN4
        private const uint ExosuitFaultEventHash = 0x45584654u; // EXFT
        private const uint ExosuitFaultDumpHash = 0x45584450u; // EXDP
        private const int MechHapticExpectedSignals = 8;
        private const int MechHapticMaxFrameSignals = 16;
        private const int MechHapticMinimumQualityFrameSignals = 4;
        private const int SiltExpectedSignals = 4;
        private const int SiltMaxFrameSignals = 8;
        private const int SiltMinimumQualityFrameSignals = 2;
        private const int AcousticExpectedSignals = 8;
        private const int AcousticMaxFrameSignals = 16;
        private const int AcousticMinimumQualityFrameSignals = 4;
        private const uint MechHapticLaneHash = 0x4D484558u;
        private const uint SiltLaneHash = 0x4558494Cu;
        private const uint AcousticLaneHash = 0x45584F41u;
        private const uint CsvHashBaseMass = 0xB8E00391u;
        private const uint CsvHashBaseMassLabel = 0xB602DA18u;
        private const uint CsvHashHydraulicLatency = 0x0FB07203u;
        private const uint CsvHashHydraulicLatencyLabel = 0x3395F44Cu;
        private const uint CsvHashThrusterForce = 0x9D48E6BCu;
        private const uint CsvHashThrusterForceLabel = 0x919ECF43u;
        private const uint CsvHashMagneticClampRange = 0x4B28B8D1u;
        private const uint CsvHashMagneticClampRangeLabel = 0x57068D8Fu;
        private const uint CsvHashDrag = 0xB88C0D43u;
        private const uint CsvHashRadius = 0x0DBA4CB3u;
        private const uint CsvHashQualityWeight = 0x09B39C97u;
        private const uint CsvHashQualityWeightLabel = 0x397033AEu;
        private const uint CsvHashGlobalQualityWeight = 0xB00FB719u;
        private const uint CsvHashGlobalQualityWeightLabel = 0xC74CE627u;
        private const uint CsvHashPurgeImpulse = 0x6A07C538u;
        private const uint CsvHashPurgeImpulseLabel = 0xB362392Fu;
        private const uint CsvHashFootstepStride = 0xA2FDE3D1u;
        private const uint CsvHashFootstepStrideLabel = 0xD641F0D6u;
        private const uint CsvHashMaxSpeed = 0x65F6CBA5u;
        private const uint CsvHashMaxSpeedLabel = 0x86DF0364u;
        private const uint CsvHashCrushDepth = 0xC7A86F76u;
        private const uint CsvHashCrushDepthLabel = 0x8021C269u;
        private const uint CsvHashSdfEpsilon = 0x66BAB869u;
        private const uint CsvHashSdfEpsilonLabel = 0x3DEA16DEu;
        private const uint CsvHashGravityMultiplier = 0x592B7011u;
        private const uint CsvHashGravityMultiplierLabel = 0xDCECF566u;
        private const uint CsvHashMaxSubsteps = 0x067BC8D3u;
        private const uint CsvHashMaxSubstepsLabel = 0x5D7F47C0u;
        private const ulong GuardState = 1UL << ((int)BufferID.ShinobuExosuitState & 63);
        private const ulong GuardInput = 1UL << ((int)BufferID.ShinobuExosuitFrameInput & 63);
        private const ulong GuardTuning = 1UL << ((int)BufferID.ShinobuExosuitTuning & 63);
        private const ulong GuardTerrain = 1UL << ((int)BufferID.ShinobuExosuitMockTerrainSdf & 63);
        private const ulong GuardFlow = 1UL << ((int)BufferID.ShinobuExosuitMockFlowField & 63);
        private const ulong GuardCrushDepth = 1UL << ((int)BufferID.ShinobuExosuitMockCrushDepth & 63);
        private const ulong GuardOutput = 1UL << ((int)BufferID.ShinobuExosuitSolverOutput & 63);
        private const ulong GuardScreen = 1UL << ((int)BufferID.ShinobuExosuitScreenDto & 63);
        private const ulong GuardTelemetry = 1UL << ((int)BufferID.ShinobuExosuitTelemetryRing & 63);
        private const ulong GuardTelemetryCursor = 1UL << ((int)BufferID.ShinobuExosuitTelemetryCursor & 63);
        private const ulong GuardFootstep = 1UL << ((int)BufferID.ShinobuExosuitFootstepAccumulator & 63);
        private const ulong GuardHaptic = 1UL << ((int)BufferID.ShinobuExosuitHapticSignals & 63);
        private const ulong GuardSilt = 1UL << ((int)BufferID.ShinobuExosuitSiltSignals & 63);
        private const ulong GuardAcoustic = 1UL << ((int)BufferID.ShinobuExosuitAcousticTaps & 63);
        private const ulong GuardVoxelSdfPayloadDescriptor = 1UL << ((int)BufferID.VoxelSdfPayloadDescriptor & 63);
        private const ulong GuardVoxelSdfTexture3D = 1UL << ((int)BufferID.VoxelSdfTexture3D & 63);
        private const ulong FrameInputMutationGuardMask =
            GuardTuning |
            GuardInput |
            GuardTerrain |
            GuardFlow |
            GuardCrushDepth;
        private const ulong JobMutationGuardMask =
            GuardState |
            GuardInput |
            GuardTuning |
            GuardTerrain |
            GuardFlow |
            GuardCrushDepth |
            GuardOutput |
            GuardScreen |
            GuardTelemetry |
            GuardTelemetryCursor |
            GuardFootstep |
            GuardHaptic |
            GuardSilt |
            GuardAcoustic;
        private const ulong VoxelSdfPayloadMutationGuardMask =
            GuardVoxelSdfPayloadDescriptor |
            GuardVoxelSdfTexture3D;

        [Header("Mass And Hydraulics")]
        [SerializeField, Min(1f), Tooltip("Cold boot mass seeded into ExosuitTuningDTO before CSV/editor overrides.")]
        private float _baseMassKg = 8000f;
        [SerializeField, Range(0.05f, 3f), Tooltip("Hydraulic pressure rise time; higher values make stick input feel heavier.")]
        private float _hydraulicLatencySeconds = 0.5f;
        [SerializeField, Min(0f), Tooltip("Maximum thrust force applied by the 6D solver.")]
        private float _thrusterForceNewtons = 42000f;
        [SerializeField, Range(0f, 8f), Tooltip("Analytical drag coefficient used after thrust integration.")]
        private float _drag = 0.45f;
        [SerializeField, Min(0f), Tooltip("Vertical velocity injected by the one-shot thermal ballast purge.")]
        private float _purgeImpulseMetersPerSecond = 14f;
        [SerializeField, Range(0.25f, 40f), Tooltip("Deterministic speed cap after current and thrust integration.")]
        private float _maxSpeedMetersPerSecond = 9f;
        [SerializeField, Range(0.005f, 0.25f), Tooltip("SDF epsilon/skin consumed by deterministic depenetration.")]
        private float _sdfEpsilonMeters = 0.04f;
        [SerializeField, Range(0f, 2f), Tooltip("Gravity scalar applied inside the kinematic solver.")]
        private float _gravityMultiplier = 0.35f;
        [SerializeField, Range(2, 8), Tooltip("Maximum quality-scaled SDF substeps.")]
        private int _maxSubsteps = 8;

        [Header("SDF Cave Mock")]
        [SerializeField, Range(0.25f, 5f), Tooltip("Mathematical sphere radius; this replaces physical hand and foot colliders.")]
        private float _radiusMeters = 1.2f;
        [SerializeField, Range(0.25f, 5f), Tooltip("Distance threshold for SDF magnetic clamp.")]
        private float _magneticClampRangeMeters = 2f;
        [SerializeField, Min(1f), Tooltip("Analytic cave shaft radius for the blind SDF mock.")]
        private float _caveRadiusMeters = 6f;
        [SerializeField, Tooltip("Analytic cave floor in local AUP-relative coordinates.")]
        private float _caveFloorY = -2f;
        [SerializeField, Tooltip("Analytic cave ceiling in local AUP-relative coordinates.")]
        private float _caveCeilingY = 14f;
        [SerializeField, Tooltip("Local center of the analytic cave shaft mock.")]
        private Vector3 _caveCenterLocal = Vector3.zero;

        [Header("Mock Inputs")]
        [SerializeField, Tooltip("Blind movement axis until the input owner binds a real unmanaged input buffer.")]
        private Vector2 _mockMoveAxis = Vector2.zero;
        [SerializeField, Range(-1f, 1f), Tooltip("Blind vertical thrust axis.")]
        private float _mockVerticalAxis;
        [SerializeField, Tooltip("Desired yaw in radians used to orient local thrust.")]
        private float _mockYawRadians;
        [SerializeField, Tooltip("Mock grab bit for SDF magnetic clamp validation.")]
        private bool _mockGrab;
        [SerializeField, Tooltip("Mock one-shot purge bit.")]
        private bool _mockPurge;
        [SerializeField, Tooltip("Mock jump/thrust bit for pressure ramp validation.")]
        private bool _mockJump;

        [Header("Abyss And Scalability")]
        [SerializeField, Tooltip("Mock current velocity; ignored while SDF-clamped.")]
        private Vector3 _mockFlowVelocity = new Vector3(0.6f, 0f, -0.25f);
        [SerializeField, Range(0f, 1f), Tooltip("Continuous current intensity scalar.")]
        private float _mockFlowIntensity01 = 0.35f;
        [SerializeField, Min(0f), Tooltip("Mock depth used to degrade hydraulic pressure without Hull Integrity coupling.")]
        private float _mockDepthMeters = 1200f;
        [SerializeField, Range(0f, 1f), Tooltip("Continuous solver quality weight. Minimum values collapse to central SDF probes.")]
        private float _globalQualityWeight = 0.62f;
        [SerializeField, Range(0.25f, 12f), Tooltip("Meters between heavy acoustic stomp taps.")]
        private float _footstepStrideMeters = 3f;
        [SerializeField, Min(1f), Tooltip("Depth at which mock crush pressure reaches 1.0.")]
        private float _crushDepthMeters = 4000f;

        private IDataVault _dataVault;
        private Transform _cachedTransform;
        private VaultGenerationHandle<ExosuitStateDTO> _stateHandle;
        private VaultGenerationHandle<ExosuitFrameInputDTO> _inputHandle;
        private VaultGenerationHandle<ExosuitTuningDTO> _tuningHandle;
        private VaultGenerationHandle<MockTerrainSDF> _terrainHandle;
        private VaultGenerationHandle<MockFlowField> _flowHandle;
        private VaultGenerationHandle<MockCrushDepthSignal> _crushDepthHandle;
        private VaultGenerationHandle<ExosuitSolverOutput> _outputHandle;
        private VaultGenerationHandle<ExoScreenDTO> _screenHandle;
        private VaultGenerationHandle<ExosuitTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<float> _footstepAccumulatorHandle;
        private VaultGenerationHandle<MechHapticSignalDTO> _hapticHandle;
        private VaultGenerationHandle<SiltExplosionSignal> _siltHandle;
        private VaultGenerationHandle<ExosuitAcousticEchoTap> _acousticHandle;

        private JobHandle _jobHandle;
        private long _jobStartTimestamp;
        private bool _jobScheduled;
        private bool _jobBuffersLocked;
        private IDataVault _jobGuardedVault;
        private ulong _jobBufferGuardMask;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredLateFrame;
        private bool _hotSwapRegistered;
        private bool _pendingDisableTeardown;
        private int _droppedSignalCount;
        private bool _buffersInitialized;
        private bool _signalLanesReady;
        private bool _coreBlackboxWarmed;
        private bool _runtimeActive;
#if UNITY_EDITOR
        private bool _coldCsvApplied;
        private string _projectRoot;
        private string _csvPath;
        private long _lastCsvWriteTicks;
#endif
        private uint _scheduledFrame;
        private uint _lastDumpFrame = uint.MaxValue;
        private uint _pendingMockProceduralWeightMilli = 1000u;
        private ExosuitFrameInputDTO _pendingFrameInput;

        private static ExosuitKinematicsRuntime s_activeRuntime;

        public int DroppedSignalCount => _droppedSignalCount;

        private void Awake()
        {
            _cachedTransform = transform;
#if UNITY_EDITOR
            _projectRoot = ResolveProjectRoot();
            _csvPath = Path.Combine(_projectRoot, "Data", "Physics", "exosuit_performance_profiles.csv");
#endif
            EnsureSignalLanesReady();
        }

        private void OnEnable()
        {
            _runtimeActive = Application.isPlaying;
            if (!_runtimeActive)
                return;

            _pendingDisableTeardown = false;
            _droppedSignalCount = 0;
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
            if (EnsureBuffers(true))
            {
#if UNITY_EDITOR
                TryApplyColdCsvOverrides();
#endif
                WarmCoreBlackboxRoute();
                s_activeRuntime = this;
            }

            TryRegisterHotSwapListener();
            TryRegisterFixed();
            TryRegisterPostFixed();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            if (!_runtimeActive)
                return;

            _pendingDisableTeardown = true;
            TryUnregisterPostFixed();
            TryUnregisterFixed();
            TryUnregisterHotSwapListener();

            CompletePendingJob();
            if (_jobScheduled)
                return;

            FinishDisableTeardown();
        }

        private void FinishDisableTeardown()
        {
            TryUnregisterLateFrame();
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
            ReleaseVaultBuffers();
            _coreBlackboxWarmed = false;
            _pendingDisableTeardown = false;
            _runtimeActive = false;
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            if (_jobScheduled || !_runtimeActive)
                return;

            if (!EnsureBuffers(false))
                return;

            float safeDeltaTime = math.clamp(math.isfinite(fixedDeltaTime) ? fixedDeltaTime : 0.02f, 0.0001f, 0.05f);
            uint frame = WriteFrameInputs();
            ScheduleSolver(safeDeltaTime, frame);
        }

        /// <inheritdoc />
        public void PostFixedTick(float fixedDeltaTime)
        {
            CompletePendingJob();
            if (_pendingDisableTeardown && !_jobScheduled)
                FinishDisableTeardown();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            CompletePendingJob();
            if (_pendingDisableTeardown && !_jobScheduled)
                FinishDisableTeardown();
        }

        /// <summary>
        /// Reads the live unmanaged tuning DTO for the editor facade.
        /// </summary>
        public static bool TryReadTuning(out ExosuitTuningDTO tuning)
        {
            tuning = default;
            if (!TryGetCachedVault(out IDataVault vault) ||
                !TryReadExistingBuffer(vault, BufferID.ShinobuExosuitTuning, out NativeArray<ExosuitTuningDTO> buffer))
                return false;

            tuning = buffer[0];
            return true;
        }

        /// <summary>
        /// Writes sanitized tuning values into the DataVault authority buffer.
        /// </summary>
        public static bool TryWriteTuning(in ExosuitTuningDTO tuning)
        {
            if (!TryGetCachedVault(out IDataVault vault) ||
                !vault.TryGetGenerationHandle(BufferID.ShinobuExosuitTuning, out VaultGenerationHandle<ExosuitTuningDTO> handle) ||
                !IsHandleCreated(in handle) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.Physics, out NativeArray<ExosuitTuningDTO> buffer))
            {
                return false;
            }

            try
            {
                if (!buffer.IsCreated || buffer.Length <= 0)
                    return false;

                buffer[0] = SanitizeManagedTuning(tuning);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.Physics);
            }
        }

        public static bool HasActiveRuntime()
        {
            return s_activeRuntime != null;
        }

        /// <summary>
        /// Reads state, solver output, and tuning for editor-only visualizers.
        /// </summary>
        public static bool TryReadState(out ExosuitStateDTO state, out ExosuitSolverOutput output, out ExosuitTuningDTO tuning)
        {
            state = default;
            output = default;
            tuning = default;
            if (!TryGetCachedVault(out IDataVault vault))
                return false;

            if (!TryReadExistingBuffer(vault, BufferID.ShinobuExosuitState, out NativeArray<ExosuitStateDTO> stateBuffer) ||
                !TryReadExistingBuffer(vault, BufferID.ShinobuExosuitSolverOutput, out NativeArray<ExosuitSolverOutput> outputBuffer) ||
                !TryReadExistingBuffer(vault, BufferID.ShinobuExosuitTuning, out NativeArray<ExosuitTuningDTO> tuningBuffer))
            {
                return false;
            }

            state = stateBuffer[0];
            output = outputBuffer[0];
            tuning = tuningBuffer[0];
            return true;
        }

        public static bool TryReadScreen(out ExoScreenDTO screen)
        {
            screen = default;
            if (!TryGetCachedVault(out IDataVault vault) ||
                !TryReadExistingBuffer(vault, BufferID.ShinobuExosuitScreenDto, out NativeArray<ExoScreenDTO> buffer))
            {
                return false;
            }

            screen = buffer[0];
            return true;
        }

        public static bool TryReadLastTelemetry(out ExosuitTelemetryEntry entry)
        {
            entry = default;
            if (!TryGetCachedVault(out IDataVault vault) ||
                !TryReadExistingBuffer(vault, BufferID.ShinobuExosuitTelemetryRing, out NativeArray<ExosuitTelemetryEntry> telemetry) ||
                !TryReadExistingBuffer(vault, BufferID.ShinobuExosuitTelemetryCursor, out NativeArray<int> cursor))
            {
                return false;
            }

            int index = cursor[0] - 1;
            if (index < 0)
                index = telemetry.Length - 1;
            if ((uint)index >= (uint)telemetry.Length)
                return false;

            entry = telemetry[index];
            return true;
        }

        private static bool TryGetCachedVault(out IDataVault vault)
        {
            vault = null;
            ExosuitKinematicsRuntime runtime = s_activeRuntime;
            if (runtime == null || runtime._dataVault == null || runtime._jobBuffersLocked)
                return false;

            vault = runtime._dataVault;
            return true;
        }

        private static bool TryReadExistingBuffer<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                   handle.SystemID == (uint)SystemID.Physics &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u && handle.SystemID == (uint)SystemID.Physics;
        }

        private bool TryOpenBufferForOwner<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return _dataVault != null &&
                   IsHandleCreated(in handle) &&
                   _dataVault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private static bool IsVoxelSdfHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.SystemID == (uint)SystemID.WorldStreaming &&
                   handle.Generation != 0u;
        }

        private bool TryOpenHeldJobWriteBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _jobGuardedVault;
            return _jobBuffersLocked &&
                   vault != null &&
                   IsHandleCreated(in handle) &&
                   handle.BufferID == (uint)expectedBufferId &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private static bool TryOpenJobBufferForOwner<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   IsHandleCreated(in handle) &&
                   handle.BufferID == (uint)expectedBufferId &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private void ReleaseVaultBuffers()
        {
            IDataVault vault = _dataVault;
            ExosuitKinematicAuthority.Unbind(in _inputHandle);
            ReleaseVaultBuffer(vault, ref _stateHandle);
            ReleaseVaultBuffer(vault, ref _inputHandle);
            ReleaseVaultBuffer(vault, ref _tuningHandle);
            ReleaseVaultBuffer(vault, ref _terrainHandle);
            ReleaseVaultBuffer(vault, ref _flowHandle);
            ReleaseVaultBuffer(vault, ref _crushDepthHandle);
            ReleaseVaultBuffer(vault, ref _outputHandle);
            ReleaseVaultBuffer(vault, ref _screenHandle);
            ReleaseVaultBuffer(vault, ref _telemetryHandle);
            ReleaseVaultBuffer(vault, ref _telemetryCursorHandle);
            ReleaseVaultBuffer(vault, ref _footstepAccumulatorHandle);
            ReleaseVaultBuffer(vault, ref _hapticHandle);
            ReleaseVaultBuffer(vault, ref _siltHandle);
            ReleaseVaultBuffer(vault, ref _acousticHandle);
            _buffersInitialized = false;
#if UNITY_EDITOR
            _coldCsvApplied = false;
#endif
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private bool EnsureBuffers(bool allowColdInitialization)
        {
            if (_dataVault == null)
                return false;

            if (!IsHandleCreated(in _stateHandle))
            {
                if (!allowColdInitialization)
                    return false;

                if (_dataVault.IsAllocationLocked || _dataVault.IsCompactionFenceActive)
                    return false;

                if (!AllocateVaultBuffers(_dataVault))
                    return false;
            }

            if (!IsHandleCreated(in _stateHandle))
                return false;

            if (!_buffersInitialized && allowColdInitialization)
            {
                _buffersInitialized = GenerateEmergencyMockExoData();
            }

            return true;
        }

        private bool AllocateVaultBuffers(IDataVault vault)
        {
            if (vault == null || vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            _stateHandle = vault.EnsureGenerationHandle<ExosuitStateDTO>(BufferID.ShinobuExosuitState, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _inputHandle = vault.EnsureGenerationHandle<ExosuitFrameInputDTO>(BufferID.ShinobuExosuitFrameInput, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.EnsureGenerationHandle<ExosuitTuningDTO>(BufferID.ShinobuExosuitTuning, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _terrainHandle = vault.EnsureGenerationHandle<MockTerrainSDF>(BufferID.ShinobuExosuitMockTerrainSdf, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _flowHandle = vault.EnsureGenerationHandle<MockFlowField>(BufferID.ShinobuExosuitMockFlowField, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _crushDepthHandle = vault.EnsureGenerationHandle<MockCrushDepthSignal>(BufferID.ShinobuExosuitMockCrushDepth, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _outputHandle = vault.EnsureGenerationHandle<ExosuitSolverOutput>(BufferID.ShinobuExosuitSolverOutput, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _screenHandle = vault.EnsureGenerationHandle<ExoScreenDTO>(BufferID.ShinobuExosuitScreenDto, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.EnsureGenerationHandle<ExosuitTelemetryEntry>(BufferID.ShinobuExosuitTelemetryRing, TelemetryCapacity, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = vault.EnsureGenerationHandle<int>(BufferID.ShinobuExosuitTelemetryCursor, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _footstepAccumulatorHandle = vault.EnsureGenerationHandle<float>(BufferID.ShinobuExosuitFootstepAccumulator, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _hapticHandle = vault.EnsureGenerationHandle<MechHapticSignalDTO>(BufferID.ShinobuExosuitHapticSignals, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _siltHandle = vault.EnsureGenerationHandle<SiltExplosionSignal>(BufferID.ShinobuExosuitSiltSignals, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _acousticHandle = vault.EnsureGenerationHandle<ExosuitAcousticEchoTap>(BufferID.ShinobuExosuitAcousticTaps, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            ExosuitKinematicAuthority.Bind(vault, in _inputHandle);
            return true;
        }

        private bool GenerateEmergencyMockExoData()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(JobMutationGuardMask))
                return false;

            try
            {
                if (!TryOpenBufferForOwner(in _stateHandle, out NativeArray<ExosuitStateDTO> stateBuffer) ||
                    !TryOpenBufferForOwner(in _tuningHandle, out NativeArray<ExosuitTuningDTO> tuningBuffer) ||
                    !TryOpenBufferForOwner(in _inputHandle, out NativeArray<ExosuitFrameInputDTO> inputBuffer) ||
                    !TryOpenBufferForOwner(in _terrainHandle, out NativeArray<MockTerrainSDF> terrainBuffer) ||
                    !TryOpenBufferForOwner(in _flowHandle, out NativeArray<MockFlowField> flowBuffer) ||
                    !TryOpenBufferForOwner(in _crushDepthHandle, out NativeArray<MockCrushDepthSignal> crushBuffer) ||
                    !TryOpenBufferForOwner(in _outputHandle, out NativeArray<ExosuitSolverOutput> outputBuffer) ||
                    !TryOpenBufferForOwner(in _screenHandle, out NativeArray<ExoScreenDTO> screenBuffer) ||
                    !TryOpenBufferForOwner(in _hapticHandle, out NativeArray<MechHapticSignalDTO> hapticBuffer) ||
                    !TryOpenBufferForOwner(in _siltHandle, out NativeArray<SiltExplosionSignal> siltBuffer) ||
                    !TryOpenBufferForOwner(in _acousticHandle, out NativeArray<ExosuitAcousticEchoTap> acousticBuffer) ||
                    !TryOpenBufferForOwner(in _telemetryCursorHandle, out NativeArray<int> cursorBuffer) ||
                    !TryOpenBufferForOwner(in _footstepAccumulatorHandle, out NativeArray<float> footstepBuffer))
                {
                    return false;
                }

                double3 originAup = ResolveRuntimeOriginAupDouble();
                Vector3 runtimePosition = _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
                double3 aup = originAup + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);

                ExosuitStateDTO state = stateBuffer[0];
                ExosuitTuningDTO tuning = tuningBuffer[0];
                bool tuningBootstrapped = IsManagedTuningBootstrapped(tuning);
                if (!tuningBootstrapped)
                {
                    tuning = BuildSerializedTuning();
                    tuning.Flags = ExosuitStateFlags.EmergencyMockData;
                    tuningBuffer[0] = tuning;
                }

                if (!tuningBootstrapped || !IsStateBootstrapped(in state))
                {
                    state = default;
                    state.AUP_Position = aup;
                    state.Velocity = float3.zero;
                    state.AngularVelocity = float3.zero;
                    state.ThrusterHeat = 0.0f;
                    state.Flags = ExosuitStateFlags.Active | ExosuitStateFlags.EmergencyMockData;
                    stateBuffer[0] = state;
                }

                inputBuffer[0] = default;
                terrainBuffer[0] = BuildTerrain(originAup);
                flowBuffer[0] = BuildFlow();
                crushBuffer[0] = BuildCrushDepth(0u, tuning.CrushDepthMeters);
                outputBuffer[0] = default;
                screenBuffer[0] = default;
                hapticBuffer[0] = default;
                siltBuffer[0] = default;
                acousticBuffer[0] = default;
                cursorBuffer[0] = 0;
                footstepBuffer[0] = 0.0f;
                _scheduledFrame = 0u;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(JobMutationGuardMask);
            }
        }

        private uint WriteFrameInputs()
        {
            if (!TryOpenBufferForOwner(in _screenHandle, out NativeArray<ExoScreenDTO> screenBuffer))
                return _scheduledFrame;

            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(FrameInputMutationGuardMask))
                return _scheduledFrame;

            try
            {
                if (!TryOpenBufferForOwner(in _tuningHandle, out NativeArray<ExosuitTuningDTO> tuningBuffer) ||
                    !TryOpenBufferForOwner(in _inputHandle, out NativeArray<ExosuitFrameInputDTO> inputBuffer) ||
                    !TryOpenBufferForOwner(in _terrainHandle, out NativeArray<MockTerrainSDF> terrainBuffer) ||
                    !TryOpenBufferForOwner(in _flowHandle, out NativeArray<MockFlowField> flowBuffer) ||
                    !TryOpenBufferForOwner(in _crushDepthHandle, out NativeArray<MockCrushDepthSignal> crushBuffer))
                {
                    return _scheduledFrame;
                }

                ExosuitTuningDTO tuning = SanitizeManagedTuning(tuningBuffer[0]);
                tuning.StateHash = ComputeManagedHash(tuning);
                tuningBuffer[0] = tuning;

                uint frame = screenBuffer[0].Frame + 1u;
                if (frame == 0u)
                    frame = 1u;
                _scheduledFrame = frame;

                if (ExosuitKinematicAuthority.TryConsumePendingFrameInput(out ExosuitFrameInputDTO pendingInput))
                    inputBuffer[0] = pendingInput;

                ExosuitFrameInputDTO existingInput = inputBuffer[0];
                ExosuitFrameInputDTO input;
                if ((existingInput.ActionMask & ExosuitInputActions.ExternalAuthority) != 0u)
                {
                    input = existingInput;
                    input.ActionMask &= ~ExosuitInputActions.ExternalAuthority;
                    _pendingMockProceduralWeightMilli = 0u;
                }
                else
                {
                    uint actionMask = 0u;
                    if (_mockGrab)
                        actionMask |= ExosuitInputActions.Grab;
                    if (_mockPurge)
                        actionMask |= ExosuitInputActions.Purge;
                    if (_mockJump)
                        actionMask |= ExosuitInputActions.Jump;

                    input = default;
                    input.MoveAxis = new float2(_mockMoveAxis.x, _mockMoveAxis.y);
                    input.VerticalAxis = _mockVerticalAxis;
                    input.DesiredYawRadians = _mockYawRadians;
                    input.ActionMask = actionMask;
                    _pendingMockProceduralWeightMilli = 1000u;
                }

                input.Frame = frame;
                input.GlobalQualityWeight = ResolveFrameQualityWeight01(tuning.GlobalQualityWeight);
                inputBuffer[0] = input;
                _pendingFrameInput = input;

                double3 originAup = ResolveRuntimeOriginAupDouble();
                terrainBuffer[0] = BuildTerrain(originAup);
                flowBuffer[0] = BuildFlow();
                crushBuffer[0] = BuildCrushDepth(frame, tuning.CrushDepthMeters);
                return frame;
            }
            finally
            {
                vault.ReleaseMutationGuard(FrameInputMutationGuardMask);
            }
        }

        private void ScheduleSolver(float deltaTime, uint frame)
        {
            if (!TryAcquireJobBufferViews(
                    out NativeArray<ExosuitStateDTO> state,
                    out NativeArray<ExosuitFrameInputDTO> input,
                    out NativeArray<ExosuitTuningDTO> tuning,
                    out NativeArray<MockTerrainSDF> terrain,
                    out NativeArray<MockFlowField> flow,
                    out NativeArray<MockCrushDepthSignal> crushDepth,
                    out NativeArray<ExosuitSolverOutput> output,
                    out NativeArray<ExoScreenDTO> screen,
                    out NativeArray<ExosuitTelemetryEntry> telemetry,
                    out NativeArray<int> telemetryCursor,
                    out NativeArray<float> footstepAccumulator,
                    out NativeArray<MechHapticSignalDTO> haptics,
                    out NativeArray<SiltExplosionSignal> silt,
                    out NativeArray<ExosuitAcousticEchoTap> acoustic))
            {
                return;
            }

            TryAcquireVoxelSdfPayload(
                state[0],
                terrain[0],
                out NativeArray<byte> voxelSdf,
                out int3 voxelSdfDimensions,
                out float3 voxelSdfOrigin,
                out float3 voxelSdfCellSize,
                out float voxelSdfRangeMeters);
            uint sectorHash = ComputeSectorHash(terrain[0].CameraAup);

            ExosuitKinematicIntegrationJob job = new ExosuitKinematicIntegrationJob
            {
                State = state,
                Input = input,
                Tuning = tuning,
                Terrain = terrain,
                VoxelSdfTexture3D = voxelSdf,
                VoxelSdfDimensions = voxelSdfDimensions,
                VoxelSdfOrigin = voxelSdfOrigin,
                VoxelSdfCellSize = voxelSdfCellSize,
                VoxelSdfRangeMeters = voxelSdfRangeMeters,
                Flow = flow,
                CrushDepth = crushDepth,
                Output = output,
                Screen = screen,
                TelemetryRing = telemetry,
                TelemetryCursor = telemetryCursor,
                FootstepAccumulator = footstepAccumulator,
                HapticSignals = haptics,
                SiltSignals = silt,
                AcousticTaps = acoustic,
                DeltaTime = deltaTime,
                Frame = frame,
                ProceduralWeightMilli = _pendingMockProceduralWeightMilli,
                StableEntityHash = ExoSourceHash,
                SectorHash = sectorHash
            };

            if (!job.TryScheduleAdmitted(JobAdmissionLane.Lane0_Critical, default(JobHandle), out _jobHandle))
            {
                UnlockJobBuffers();
                return;
            }

            _jobStartTimestamp = Stopwatch.GetTimestamp();
            _jobScheduled = true;
            H8Memory.RegisterActiveJob(SystemID.Physics, _jobHandle);
            JobHandle.ScheduleBatchedJobs();
        }

        private void CompletePendingJob()
        {
            if (!_jobScheduled)
            {
                if (_jobBuffersLocked)
                    UnlockJobBuffers();
                return;
            }

            if (!_jobHandle.IsCompleted)
                return;

            // Non-blocking: live and teardown paths only reach Complete after IsCompleted.
            if (!DispatcherJobFence.TryFinalizeCompleted(ref _jobHandle))
                return;

            _jobScheduled = false;
            FinishCompletedJob();
        }

        private bool CompletePendingJobForRebind()
        {
            if (!_jobScheduled)
            {
                if (_jobBuffersLocked)
                    UnlockJobBuffers();
                return true;
            }

            DispatcherJobFence.BeginPostFixedSwapWindow();
            try
            {
                if (!DispatcherJobFence.TryComplete(ref _jobHandle, forceComplete: true))
                    return false;
            }
            finally
            {
                DispatcherJobFence.EndPostFixedSwapWindow();
            }

            _jobScheduled = false;
            FinishCompletedJob();
            return true;
        }

        private void FinishCompletedJob()
        {
            float elapsedMs = ResolveElapsedJobMs();
            bool budgetExceeded = elapsedMs > 0.1f;
            bool telemetryDumpStaged = false;
            float telemetryDumpScalar = 0.0f;
            uint telemetryDumpStateHash = 0u;
            try
            {
                PatchLastTelemetryElapsed(elapsedMs, budgetExceeded);
                if (budgetExceeded)
                    telemetryDumpStaged = TryStageTelemetryDumpBuffer(out telemetryDumpScalar, out telemetryDumpStateHash);
                if (EmitReadbackSignals() && !telemetryDumpStaged)
                    telemetryDumpStaged = TryStageTelemetryDumpBuffer(out telemetryDumpScalar, out telemetryDumpStateHash);
            }
            finally
            {
                UnlockJobBuffers();
            }

            JobAdmissionScheduleExtensions.ReportAdmittedJobCompleted<ExosuitKinematicIntegrationJob>(JobAdmissionLane.Lane0_Critical, elapsedMs);
            if (telemetryDumpStaged)
                PublishTelemetryDump(telemetryDumpScalar, telemetryDumpStateHash);
        }

        private float ResolveElapsedJobMs()
        {
            long delta = Stopwatch.GetTimestamp() - _jobStartTimestamp;
            if (delta <= 0L)
                return 0.0f;

            return (float)(delta * 1000.0 / Stopwatch.Frequency);
        }

        private void PatchLastTelemetryElapsed(float elapsedMs, bool budgetExceeded)
        {
            if (!TryOpenHeldJobWriteBuffer(in _telemetryHandle, BufferID.ShinobuExosuitTelemetryRing, out NativeArray<ExosuitTelemetryEntry> telemetry) ||
                !TryOpenHeldJobWriteBuffer(in _telemetryCursorHandle, BufferID.ShinobuExosuitTelemetryCursor, out NativeArray<int> cursorBuffer))
                return;

            int index = cursorBuffer[0] - 1;
            if (index < 0)
                index = telemetry.Length - 1;
            if ((uint)index >= (uint)telemetry.Length)
                return;

            ExosuitTelemetryEntry entry = telemetry[index];
            entry.SolverComputeTimeMs = elapsedMs;
            if (budgetExceeded)
                entry.Flags |= ExosuitStateFlags.BudgetExceeded;
            telemetry[index] = entry;
        }

        private void PatchLastTelemetryFlags(uint flags)
        {
            if (flags == 0u ||
                !TryOpenHeldJobWriteBuffer(in _telemetryHandle, BufferID.ShinobuExosuitTelemetryRing, out NativeArray<ExosuitTelemetryEntry> telemetry) ||
                !TryOpenHeldJobWriteBuffer(in _telemetryCursorHandle, BufferID.ShinobuExosuitTelemetryCursor, out NativeArray<int> cursorBuffer))
            {
                return;
            }

            int index = cursorBuffer[0] - 1;
            if (index < 0)
                index = telemetry.Length - 1;
            if ((uint)index >= (uint)telemetry.Length)
                return;

            ExosuitTelemetryEntry entry = telemetry[index];
            entry.Flags |= flags;
            telemetry[index] = entry;
        }

        private void RecordSignalDrop()
        {
            if (_droppedSignalCount < 0x3FFFFFFF)
                _droppedSignalCount++;

            PatchLastTelemetryFlags(ExosuitStateFlags.SignalDrop);
        }

        private bool EmitReadbackSignals()
        {
            if (!TryOpenBufferForOwner(in _outputHandle, out NativeArray<ExosuitSolverOutput> outputBuffer) ||
                !TryOpenBufferForOwner(in _stateHandle, out NativeArray<ExosuitStateDTO> stateBuffer))
                return false;

            ExosuitSolverOutput output = outputBuffer[0];
            ExosuitStateDTO state = stateBuffer[0];
            bool dumpRequested = (output.Flags & ExosuitSolverOutput.FlagFault) != 0u ||
                                 (state.Flags & ExosuitStateFlags.NaNDetected) != 0u;

            if ((output.Flags & ExosuitSolverOutput.FlagHaptic) != 0u)
                EmitHaptics(output.Frame);
            if ((output.Flags & ExosuitSolverOutput.FlagSilt) != 0u)
                EmitSilt();
            if ((output.Flags & ExosuitSolverOutput.FlagAcousticTap) != 0u)
                EmitAcoustic();
            return dumpRequested;
        }

        private void EmitHaptics(uint frame)
        {
            if (!TryOpenBufferForOwner(in _hapticHandle, out NativeArray<MechHapticSignalDTO> haptics))
                return;

            MechHapticSignalDTO mech = haptics[0];
            float amplitude = math.isfinite(mech.Amplitude) ? math.saturate(mech.Amplitude) : 0.0f;
            if (amplitude <= 0.0f)
                return;

            mech.Amplitude = amplitude;
            mech.Duration = math.isfinite(mech.Duration) ? math.max(0.01f, mech.Duration) : 0.01f;
            mech.Frequency = math.isfinite(mech.Frequency) ? math.max(0.0f, mech.Frequency) : 0.0f;
            if (!SignalBus<MechHapticSignalDTO>.TryPushTracked(in mech, ref s_x001DirectSignalPushDropCount_ExosuitKinematicsRuntime))
                RecordSignalDrop();

            bool lowFrequencyLoad = mech.Frequency <= 20.0f;
            HapticRequest request = default;
            request.Intensity01 = amplitude;
            request.DurationSeconds = mech.Duration;
            request.Frequency01 = math.saturate(mech.Frequency * (1.0f / 60.0f));
            request.SourceHash = ExoSourceHash;
            request.Frame = frame;
            request.Channel = lowFrequencyLoad ? HapticRequest.ChannelCrush : HapticRequest.ChannelGearScrape;
            request.Flags = lowFrequencyLoad ? HapticRequest.FlagCrush : HapticRequest.FlagLightThud;
            if (!SignalBus<HapticRequest>.TryPushTracked(in request, ref s_x001DirectSignalPushDropCount_ExosuitKinematicsRuntime))
                RecordSignalDrop();
        }

        private void EmitSilt()
        {
            if (TryOpenBufferForOwner(in _siltHandle, out NativeArray<SiltExplosionSignal> siltBuffer) &&
                siltBuffer[0].Intensity01 > 0.0f)
            {
                SiltExplosionSignal silt = siltBuffer[0];
                if (!math.all(math.isfinite(silt.AUP)))
                    return;
                silt.Intensity01 = math.isfinite(silt.Intensity01) ? math.saturate(silt.Intensity01) : 0.0f;
                if (silt.Intensity01 <= 0.0f)
                    return;
                if (!SignalBus<SiltExplosionSignal>.TryPushTracked(in silt, ref s_x001DirectSignalPushDropCount_ExosuitKinematicsRuntime))
                    RecordSignalDrop();
            }
        }

        private void EmitAcoustic()
        {
            if (TryOpenBufferForOwner(in _acousticHandle, out NativeArray<ExosuitAcousticEchoTap> acousticBuffer) &&
                acousticBuffer[0].Intensity01 > 0.0f)
            {
                ExosuitAcousticEchoTap tap = acousticBuffer[0];
                if (!math.all(math.isfinite(tap.AUP)))
                    return;
                tap.Intensity01 = math.isfinite(tap.Intensity01) ? math.saturate(tap.Intensity01) : 0.0f;
                if (tap.Intensity01 <= 0.0f)
                    return;
                if (!SignalBus<ExosuitAcousticEchoTap>.TryPushTracked(in tap, ref s_x001DirectSignalPushDropCount_ExosuitKinematicsRuntime))
                    RecordSignalDrop();
            }
        }

        private bool TryAcquireJobBufferViews(
            out NativeArray<ExosuitStateDTO> state,
            out NativeArray<ExosuitFrameInputDTO> input,
            out NativeArray<ExosuitTuningDTO> tuning,
            out NativeArray<MockTerrainSDF> terrain,
            out NativeArray<MockFlowField> flow,
            out NativeArray<MockCrushDepthSignal> crushDepth,
            out NativeArray<ExosuitSolverOutput> output,
            out NativeArray<ExoScreenDTO> screen,
            out NativeArray<ExosuitTelemetryEntry> telemetry,
            out NativeArray<int> telemetryCursor,
            out NativeArray<float> footstepAccumulator,
            out NativeArray<MechHapticSignalDTO> haptics,
            out NativeArray<SiltExplosionSignal> silt,
            out NativeArray<ExosuitAcousticEchoTap> acoustic)
        {
            state = default;
            input = default;
            tuning = default;
            terrain = default;
            flow = default;
            crushDepth = default;
            output = default;
            screen = default;
            telemetry = default;
            telemetryCursor = default;
            footstepAccumulator = default;
            haptics = default;
            silt = default;
            acoustic = default;

            IDataVault vault = _dataVault;
            if (_jobBuffersLocked || vault == null)
                return false;

            ulong guardMask;
            if (TryAcquireFullJobBufferGuard(vault))
            {
                guardMask = JobMutationGuardMask | VoxelSdfPayloadMutationGuardMask;
            }
            else if (TryAcquireFallbackJobBufferGuard(vault))
            {
                guardMask = JobMutationGuardMask;
            }
            else
            {
                return false;
            }

            bool guardTransferred = false;
            try
            {
                if (!TryOpenJobBufferForOwner(vault, in _stateHandle, BufferID.ShinobuExosuitState, out state) ||
                    !TryOpenJobBufferForOwner(vault, in _inputHandle, BufferID.ShinobuExosuitFrameInput, out input) ||
                    !TryOpenJobBufferForOwner(vault, in _tuningHandle, BufferID.ShinobuExosuitTuning, out tuning) ||
                    !TryOpenJobBufferForOwner(vault, in _terrainHandle, BufferID.ShinobuExosuitMockTerrainSdf, out terrain) ||
                    !TryOpenJobBufferForOwner(vault, in _flowHandle, BufferID.ShinobuExosuitMockFlowField, out flow) ||
                    !TryOpenJobBufferForOwner(vault, in _crushDepthHandle, BufferID.ShinobuExosuitMockCrushDepth, out crushDepth) ||
                    !TryOpenJobBufferForOwner(vault, in _outputHandle, BufferID.ShinobuExosuitSolverOutput, out output) ||
                    !TryOpenJobBufferForOwner(vault, in _screenHandle, BufferID.ShinobuExosuitScreenDto, out screen) ||
                    !TryOpenJobBufferForOwner(vault, in _telemetryHandle, BufferID.ShinobuExosuitTelemetryRing, out telemetry) ||
                    !TryOpenJobBufferForOwner(vault, in _telemetryCursorHandle, BufferID.ShinobuExosuitTelemetryCursor, out telemetryCursor) ||
                    !TryOpenJobBufferForOwner(vault, in _footstepAccumulatorHandle, BufferID.ShinobuExosuitFootstepAccumulator, out footstepAccumulator) ||
                    !TryOpenJobBufferForOwner(vault, in _hapticHandle, BufferID.ShinobuExosuitHapticSignals, out haptics) ||
                    !TryOpenJobBufferForOwner(vault, in _siltHandle, BufferID.ShinobuExosuitSiltSignals, out silt) ||
                    !TryOpenJobBufferForOwner(vault, in _acousticHandle, BufferID.ShinobuExosuitAcousticTaps, out acoustic) ||
                    telemetry.Length < TelemetryCapacity)
                {
                    return false;
                }

                _jobGuardedVault = vault;
                _jobBufferGuardMask = guardMask;
                _jobBuffersLocked = true;
                guardTransferred = true;
                return true;
            }
            finally
            {
                if (!guardTransferred)
                    vault.ReleaseMutationGuard(guardMask);
            }
        }

        private static bool TryAcquireFullJobBufferGuard(IDataVault vault)
        {
            return vault != null &&
                   vault.TryAcquireMutationGuard(JobMutationGuardMask | VoxelSdfPayloadMutationGuardMask);
        }

        private static bool TryAcquireFallbackJobBufferGuard(IDataVault vault)
        {
            return vault != null &&
                   vault.TryAcquireMutationGuard(JobMutationGuardMask);
        }

        private bool TryAcquireVoxelSdfPayload(
            in ExosuitStateDTO state,
            in MockTerrainSDF terrain,
            out NativeArray<byte> voxelSdf,
            out int3 dimensions,
            out float3 origin,
            out float3 cellSize,
            out float rangeMeters)
        {
            voxelSdf = default;
            dimensions = default;
            origin = float3.zero;
            cellSize = float3.zero;
            rangeMeters = 0.0f;

            if (!math.all(math.isfinite(state.AUP_Position)) ||
                !math.all(math.isfinite(terrain.CameraAup)))
            {
                return false;
            }

            double3 localDouble = state.AUP_Position - terrain.CameraAup;
            if (!math.all(math.isfinite(localDouble)))
                return false;

            IDataVault vault = _jobGuardedVault;
            if (!_jobBuffersLocked ||
                vault == null ||
                (_jobBufferGuardMask & VoxelSdfPayloadMutationGuardMask) != VoxelSdfPayloadMutationGuardMask)
                return false;

            if (!vault.TryGetGenerationHandle<VoxelSdfPayloadDescriptorDTO>(BufferID.VoxelSdfPayloadDescriptor, out VaultGenerationHandle<VoxelSdfPayloadDescriptorDTO> descriptorHandle) ||
                !IsVoxelSdfHandle(in descriptorHandle, BufferID.VoxelSdfPayloadDescriptor) ||
                !vault.TryReadHandle(in descriptorHandle, out NativeArray<VoxelSdfPayloadDescriptorDTO> descriptors) ||
                !descriptors.IsCreated ||
                descriptors.Length <= 0)
            {
                return false;
            }

            VoxelSdfPayloadDescriptorDTO descriptor = descriptors[0];
            int3 resolvedDimensions = descriptor.GridDimensions;
            if (!ExosuitKinematicIntegrationJob.TryResolveSdfVoxelCount(resolvedDimensions, out int expectedLength) ||
                expectedLength <= 0 ||
                descriptor.ByteCount != expectedLength ||
                descriptor.BufferId != unchecked((uint)(int)BufferID.VoxelSdfTexture3D) ||
                descriptor.OwnerSystemId != (uint)SystemID.WorldStreaming ||
                (descriptor.Flags & VoxelSdfPayloadDescriptorDTO.FlagValid) == 0u)
            {
                return false;
            }

            if (!vault.TryGetGenerationHandle<byte>(BufferID.VoxelSdfTexture3D, out VaultGenerationHandle<byte> sdfHandle) ||
                !IsVoxelSdfHandle(in sdfHandle, BufferID.VoxelSdfTexture3D) ||
                sdfHandle.Generation != descriptor.BufferGeneration ||
                !vault.TryReadHandle(in sdfHandle, out NativeArray<byte> resolvedSdf) ||
                !resolvedSdf.IsCreated ||
                resolvedSdf.Length < expectedLength)
            {
                return false;
            }

            float3 resolvedCellSize = new float3(
                math.max(0.0001f, math.abs(descriptor.VoxelCellSize.x)),
                math.max(0.0001f, math.abs(descriptor.VoxelCellSize.y)),
                math.max(0.0001f, math.abs(descriptor.VoxelCellSize.z)));
            float3 resolvedOrigin = descriptor.VolumeOrigin;
            float resolvedRange = math.max(0.0001f, math.isfinite(descriptor.SdfRangeMeters) ? descriptor.SdfRangeMeters : 0.0f);
            if (!resolvedSdf.IsCreated ||
                !math.all(math.isfinite(resolvedOrigin)) ||
                !math.all(math.isfinite(resolvedCellSize)) ||
                !math.isfinite(resolvedRange))
            {
                return false;
            }

            voxelSdf = resolvedSdf;
            dimensions = resolvedDimensions;
            origin = resolvedOrigin;
            cellSize = resolvedCellSize;
            rangeMeters = resolvedRange;
            return true;
        }

        private void UnlockJobBuffers()
        {
            if (!_jobBuffersLocked)
                return;

            IDataVault vault = _jobGuardedVault;
            ulong guardMask = _jobBufferGuardMask != 0UL ? _jobBufferGuardMask : JobMutationGuardMask;
            if (vault != null)
                vault.ReleaseMutationGuard(guardMask);

            _jobGuardedVault = null;
            _jobBufferGuardMask = 0UL;
            _jobBuffersLocked = false;
        }

#if UNITY_EDITOR
        private void TryApplyCsvOverrides()
        {
            if (string.IsNullOrEmpty(_csvPath))
                return;
            if (!File.Exists(_csvPath))
                return;

            long writeTicks = File.GetLastWriteTimeUtc(_csvPath).Ticks;
            if (writeTicks == _lastCsvWriteTicks)
                return;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsHandleCreated(in _tuningHandle) ||
                !vault.TryReadOnlyHandle(in _tuningHandle, out NativeArray<ExosuitTuningDTO>.ReadOnly tuningRead) ||
                tuningRead.Length <= 0)
            {
                return;
            }

            ExosuitTuningDTO tuning = tuningRead[0];
            int csvByteCount = TryLoadCsvTuningOverride(ref tuning, out bool parsedTuning);
            if (csvByteCount <= 0)
            {
                _lastCsvWriteTicks = writeTicks;
                return;
            }

            if (!parsedTuning)
            {
                _lastCsvWriteTicks = writeTicks;
                return;
            }

            tuning.CsvVersion++;
            if (TryCommitCsvTuningOverride(vault, in tuning))
                _lastCsvWriteTicks = writeTicks;
        }

        private int TryLoadCsvTuningOverride(ref ExosuitTuningDTO tuning, out bool parsedTuning)
        {
            parsedTuning = false;
            Span<byte> scratch = stackalloc byte[CsvScratchCapacity];
            int csvByteCount = ReadCsvBytes(_csvPath, scratch);
            if (csvByteCount > 0)
                parsedTuning = ParseCsvIntoTuning(scratch.Slice(0, csvByteCount), ref tuning);

            return csvByteCount;
        }

        private bool TryCommitCsvTuningOverride(IDataVault vault, in ExosuitTuningDTO tuning)
        {
            if (vault == null ||
                !vault.TryAcquireWriteLock(in _tuningHandle, SystemID.Physics, out NativeArray<ExosuitTuningDTO> tuningBuffer))
            {
                return false;
            }

            try
            {
                if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                    return false;

                tuningBuffer[0] = SanitizeManagedTuning(tuning);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _tuningHandle, SystemID.Physics);
            }
        }

        private void TryApplyColdCsvOverrides()
        {
            if (_coldCsvApplied || !_buffersInitialized)
                return;

            TryApplyCsvOverrides();
            _coldCsvApplied = true;
        }

        private static int ReadCsvBytes(string path, Span<byte> scratch)
        {
            int count = 0;
            if (scratch.Length <= 0)
                return 0;

            using (FileStream stream = File.OpenRead(path))
            {
                while (count < scratch.Length)
                {
                    int read = stream.Read(scratch.Slice(count));
                    if (read <= 0)
                        break;

                    count += read;
                }

                if (count >= scratch.Length && stream.Position < stream.Length)
                    return -count;
            }

            return count;
        }

        private static unsafe bool ParseCsvIntoTuning(NativeArray<byte> scratch, int count, ref ExosuitTuningDTO tuning)
        {
            if (!scratch.IsCreated || count <= 0)
                return false;

            int safeCount = math.min(count, scratch.Length);
            byte* bytes = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
            return ParseCsvIntoTuning(new ReadOnlySpan<byte>(bytes, safeCount), ref tuning);
        }

        private static bool ParseCsvIntoTuning(ReadOnlySpan<byte> scratch, ref ExosuitTuningDTO tuning)
        {
            bool changed = false;
            int index = 0;
            while (index < scratch.Length)
            {
                SkipSeparators(scratch, ref index);
                uint hash = 2166136261u;
                bool hasKey = false;
                while (index < scratch.Length)
                {
                    byte b = scratch[index];
                    if (b == (byte)',' || b == (byte)'=' || b == (byte)';' || b == (byte)'\n' || b == (byte)'\r')
                        break;
                    if (b > (byte)' ')
                    {
                        if (b >= (byte)'A' && b <= (byte)'Z')
                            b += 32;
                        hash = (hash ^ b) * 16777619u;
                        hasKey = true;
                    }

                    index++;
                }

                if (!hasKey)
                {
                    index++;
                    continue;
                }

                while (index < scratch.Length && scratch[index] != (byte)',' && scratch[index] != (byte)'=')
                    index++;
                if (index < scratch.Length)
                    index++;

                if (!TryParseFloat(scratch, ref index, out float value))
                    continue;

                changed |= ApplyCsvValue(hash, value, ref tuning);
                while (index < scratch.Length && scratch[index] != (byte)'\n')
                    index++;
            }

            return changed;
        }

        private static void SkipSeparators(ReadOnlySpan<byte> scratch, ref int index)
        {
            while (index < scratch.Length)
            {
                byte b = scratch[index];
                if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\n' && b != (byte)'\r')
                    return;
                index++;
            }
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> scratch, ref int index, out float value)
        {
            value = 0.0f;
            while (index < scratch.Length && (scratch[index] == (byte)' ' || scratch[index] == (byte)'\t'))
                index++;

            float sign = 1.0f;
            if (index < scratch.Length && scratch[index] == (byte)'-')
            {
                sign = -1.0f;
                index++;
            }

            double integral = 0.0;
            bool hasDigit = false;
            while (index < scratch.Length && scratch[index] >= (byte)'0' && scratch[index] <= (byte)'9')
            {
                integral = integral * 10.0 + scratch[index] - (byte)'0';
                hasDigit = true;
                index++;
            }

            double fraction = 0.0;
            double scale = 1.0;
            if (index < scratch.Length && scratch[index] == (byte)'.')
            {
                index++;
                while (index < scratch.Length && scratch[index] >= (byte)'0' && scratch[index] <= (byte)'9')
                {
                    fraction = fraction * 10.0 + scratch[index] - (byte)'0';
                    scale *= 10.0;
                    hasDigit = true;
                    index++;
                }
            }

            if (!hasDigit)
                return false;

            double parsed = integral + fraction / scale;
            if (index < scratch.Length && (scratch[index] == (byte)'e' || scratch[index] == (byte)'E'))
            {
                int exponentStart = index;
                index++;
                int exponentSign = 1;
                if (index < scratch.Length && scratch[index] == (byte)'-')
                {
                    exponentSign = -1;
                    index++;
                }
                else if (index < scratch.Length && scratch[index] == (byte)'+')
                {
                    index++;
                }

                int exponent = 0;
                bool hasExponentDigit = false;
                while (index < scratch.Length && scratch[index] >= (byte)'0' && scratch[index] <= (byte)'9')
                {
                    exponent = math.min(38, exponent * 10 + scratch[index] - (byte)'0');
                    hasExponentDigit = true;
                    index++;
                }

                if (hasExponentDigit)
                    parsed *= Pow10Clamped(exponentSign * exponent);
                else
                    index = exponentStart;
            }

            value = (float)(sign * parsed);
            return math.isfinite(value);
        }

        private static double Pow10Clamped(int exponent)
        {
            int steps = math.min(38, math.abs(exponent));
            double value = 1.0;
            for (int i = 0; i < steps; i++)
                value *= 10.0;

            return exponent >= 0 ? value : 1.0 / value;
        }

        private static bool ApplyCsvValue(uint hash, float value, ref ExosuitTuningDTO tuning)
        {
            if (!math.isfinite(value))
                return false;

            switch (hash)
            {
                case CsvHashBaseMass:
                case CsvHashBaseMassLabel:
                    tuning.BaseMass = math.max(1f, value);
                    tuning.CurrentMass = tuning.BaseMass;
                    return true;
                case CsvHashHydraulicLatency:
                case CsvHashHydraulicLatencyLabel:
                    tuning.HydraulicLatencySeconds = math.clamp(value, 0.05f, 3f);
                    return true;
                case CsvHashThrusterForce:
                case CsvHashThrusterForceLabel:
                    tuning.ThrusterForce = math.max(0f, value);
                    return true;
                case CsvHashMagneticClampRange:
                case CsvHashMagneticClampRangeLabel:
                    tuning.ClampRange = math.max(0.25f, value);
                    return true;
                case CsvHashDrag:
                    tuning.Drag = math.clamp(value, 0f, 8f);
                    return true;
                case CsvHashRadius:
                    tuning.Radius = math.clamp(value, 0.25f, 5f);
                    return true;
                case CsvHashQualityWeight:
                case CsvHashQualityWeightLabel:
                case CsvHashGlobalQualityWeight:
                case CsvHashGlobalQualityWeightLabel:
                    tuning.GlobalQualityWeight = math.saturate(value);
                    return true;
                case CsvHashPurgeImpulse:
                case CsvHashPurgeImpulseLabel:
                    tuning.PurgeImpulse = math.max(0f, value);
                    return true;
                case CsvHashFootstepStride:
                case CsvHashFootstepStrideLabel:
                    tuning.FootstepStrideMeters = math.max(0.25f, value);
                    return true;
                case CsvHashMaxSpeed:
                case CsvHashMaxSpeedLabel:
                    tuning.MaxSpeedMetersPerSecond = math.max(0.25f, value);
                    return true;
                case CsvHashCrushDepth:
                case CsvHashCrushDepthLabel:
                    tuning.CrushDepthMeters = math.max(1f, value);
                    return true;
                case CsvHashSdfEpsilon:
                case CsvHashSdfEpsilonLabel:
                    tuning.SdfEpsilonMeters = math.clamp(value, 0.005f, 0.25f);
                    return true;
                case CsvHashGravityMultiplier:
                case CsvHashGravityMultiplierLabel:
                    tuning.GravityMultiplier = math.clamp(value, 0f, 2f);
                    return true;
                case CsvHashMaxSubsteps:
                case CsvHashMaxSubstepsLabel:
                    tuning.MaxSubsteps = (uint)math.clamp((int)math.round(value), 2, 8);
                    return true;
                default:
                    return false;
            }
        }
#endif

        private bool TryStageTelemetryDumpBuffer(out float scalar, out uint stateHash)
        {
            scalar = 0.0f;
            stateHash = 0u;

            uint frame = _scheduledFrame;
            if (_lastDumpFrame == frame)
                return false;

            if (!TryOpenBufferForOwner(in _telemetryHandle, out NativeArray<ExosuitTelemetryEntry> telemetry) ||
                !TryOpenBufferForOwner(in _telemetryCursorHandle, out NativeArray<int> cursorBuffer))
                return false;

            _lastDumpFrame = frame;

            int cursor = cursorBuffer.IsCreated && cursorBuffer.Length > 0 ? cursorBuffer[0] : 0;
            if ((uint)cursor >= (uint)telemetry.Length)
                cursor = 0;

            if (!_coreBlackboxWarmed)
                return false;

            int latestIndex = cursor - 1;
            if (latestIndex < 0)
                latestIndex = telemetry.Length - 1;

            ExosuitTelemetryEntry latest = telemetry[latestIndex];
            scalar = math.isfinite(latest.SolverComputeTimeMs) ? latest.SolverComputeTimeMs : 0f;
            stateHash = latest.StateHash;
            return true;
        }

        private static void PublishTelemetryDump(float scalar, uint stateHash)
        {
            if (GlobalTelemetryBus.BlackboxActiveFrameCount <= 0)
                return;

            GlobalTelemetryBus.PushEvent(ExosuitFaultEventHash, scalar, stateHash);
            _ = GlobalTelemetryBus.TryDumpBlackboxNow(ExosuitFaultDumpHash);
        }

        private void WarmCoreBlackboxRoute()
        {
            if (_coreBlackboxWarmed || !Application.isPlaying)
                return;

            GlobalTelemetryBus.Initialize();
            _coreBlackboxWarmed = GlobalTelemetryBus.BlackboxActiveFrameCount > 0;
        }

        private void EnsureSignalLanesReady()
        {
            if (_signalLanesReady)
                return;

            SignalBus<MechHapticSignalDTO>.Configure(MechHapticExpectedSignals, MechHapticMaxFrameSignals, MechHapticMinimumQualityFrameSignals, MechHapticLaneHash);
            SignalBus<MechHapticSignalDTO>.EnsureInitialized();

            SignalBus<SiltExplosionSignal>.Configure(SiltExpectedSignals, SiltMaxFrameSignals, SiltMinimumQualityFrameSignals, SiltLaneHash);
            SignalBus<SiltExplosionSignal>.EnsureInitialized();

            SignalBus<ExosuitAcousticEchoTap>.Configure(AcousticExpectedSignals, AcousticMaxFrameSignals, AcousticMinimumQualityFrameSignals, AcousticLaneHash);
            SignalBus<ExosuitAcousticEchoTap>.EnsureInitialized();

            SignalBus<HapticRequest>.EnsureInitialized();
            _signalLanesReady = true;
        }

        private ExosuitTuningDTO BuildSerializedTuning()
        {
            ExosuitTuningDTO tuning = default;
            tuning.BaseMass = math.max(1f, math.isfinite(_baseMassKg) ? _baseMassKg : 8000f);
            tuning.CurrentMass = tuning.BaseMass;
            tuning.Drag = math.clamp(math.isfinite(_drag) ? _drag : 0.45f, 0f, 8f);
            tuning.ThrusterForce = math.max(0f, math.isfinite(_thrusterForceNewtons) ? _thrusterForceNewtons : 42000f);
            tuning.Radius = math.clamp(math.isfinite(_radiusMeters) ? _radiusMeters : 1.2f, 0.25f, 5f);
            float clampRange = math.isfinite(_magneticClampRangeMeters) ? _magneticClampRangeMeters : 2f;
            tuning.ClampRange = math.max(tuning.Radius, clampRange);
            tuning.HydraulicLatencySeconds = math.clamp(math.isfinite(_hydraulicLatencySeconds) ? _hydraulicLatencySeconds : 0.5f, 0.05f, 3f);
            tuning.PurgeImpulse = math.max(0f, math.isfinite(_purgeImpulseMetersPerSecond) ? _purgeImpulseMetersPerSecond : 14f);
            tuning.GlobalQualityWeight = SanitizeQualityWeight01(_globalQualityWeight, ExosuitMathGuards.DefaultQualityWeight);
            tuning.FootstepStrideMeters = math.max(0.25f, math.isfinite(_footstepStrideMeters) ? _footstepStrideMeters : 3f);
            tuning.MaxSpeedMetersPerSecond = math.max(0.25f, math.isfinite(_maxSpeedMetersPerSecond) ? _maxSpeedMetersPerSecond : 9f);
            tuning.CrushDepthMeters = math.max(1f, math.isfinite(_crushDepthMeters) ? _crushDepthMeters : 4000f);
            tuning.SdfEpsilonMeters = math.clamp(math.isfinite(_sdfEpsilonMeters) ? _sdfEpsilonMeters : 0.04f, 0.005f, 0.25f);
            tuning.GravityMultiplier = math.clamp(math.isfinite(_gravityMultiplier) ? _gravityMultiplier : 0.35f, 0f, 2f);
            tuning.MaxSubsteps = (uint)math.clamp(_maxSubsteps, 2, 8);
            tuning.StateHash = ComputeManagedHash(tuning);
            return tuning;
        }

        private MockTerrainSDF BuildTerrain(double3 cameraAup)
        {
            MockTerrainSDF terrain = default;
            terrain.CameraAup = math.all(math.isfinite(cameraAup)) ? cameraAup : double3.zero;
            terrain.CaveRadius = math.max(1f, math.isfinite(_caveRadiusMeters) ? _caveRadiusMeters : 6f);
            float floorY = math.isfinite(_caveFloorY) ? _caveFloorY : -2f;
            float ceilingY = math.isfinite(_caveCeilingY) ? _caveCeilingY : floorY + 16f;
            terrain.FloorY = math.min(floorY, ceilingY - 2f);
            terrain.CeilingY = math.max(ceilingY, terrain.FloorY + 2f);
            terrain.WallSoftnessMeters = 0.15f;
            terrain.CaveCenterLocal = SanitizeVector3(_caveCenterLocal);
            return terrain;
        }

        private static double3 ResolveRuntimeOriginAupDouble()
        {
            var originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return originAup.IsFinite() ? originAup.ToAbsoluteDouble3() : double3.zero;
        }

        private MockFlowField BuildFlow()
        {
            MockFlowField flow = default;
            flow.FlowVelocity = SanitizeVector3(_mockFlowVelocity);
            flow.Intensity01 = math.saturate(math.isfinite(_mockFlowIntensity01) ? _mockFlowIntensity01 : 0.0f);
            return flow;
        }

        private MockCrushDepthSignal BuildCrushDepth(uint frame, float crushDepthMeters)
        {
            MockCrushDepthSignal crush = default;
            crush.DepthMeters = math.max(0f, math.isfinite(_mockDepthMeters) ? _mockDepthMeters : 0.0f);
            crush.ExternalPressure01 = math.saturate(crush.DepthMeters * math.rcp(math.max(1f, crushDepthMeters)));
            crush.Frame = frame;
            return crush;
        }

        private static bool IsStateBootstrapped(in ExosuitStateDTO state)
        {
            return (state.Flags & ExosuitStateFlags.Active) != 0u &&
                   math.all(math.isfinite(state.AUP_Position)) &&
                   math.all(math.isfinite(state.Velocity)) &&
                   math.all(math.isfinite(state.AngularVelocity)) &&
                   math.isfinite(state.ThrusterHeat);
        }

        private static bool IsManagedTuningBootstrapped(in ExosuitTuningDTO tuning)
        {
            if (!math.isfinite(tuning.BaseMass) ||
                !math.isfinite(tuning.ThrusterForce) ||
                !math.isfinite(tuning.ClampRange) ||
                !math.isfinite(tuning.GlobalQualityWeight) ||
                tuning.BaseMass <= 0.0f)
            {
                return false;
            }

            return tuning.StateHash == ComputeManagedHash(SanitizeManagedTuning(tuning));
        }

        private static float3 SanitizeVector3(Vector3 value)
        {
            return new float3(
                math.isfinite(value.x) ? value.x : 0.0f,
                math.isfinite(value.y) ? value.y : 0.0f,
                math.isfinite(value.z) ? value.z : 0.0f);
        }

        private static float ResolveGlobalQualityWeight01()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, ExosuitMathGuards.DefaultQualityWeight);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : ExosuitMathGuards.DefaultQualityWeight);
        }

        private static float ResolveFrameQualityWeight01(float tuningQualityWeight)
        {
            return math.min(
                SanitizeQualityWeight01(tuningQualityWeight, ExosuitMathGuards.DefaultQualityWeight),
                ResolveGlobalQualityWeight01());
        }

        private static uint ComputeSectorHash(double3 cameraAup)
        {
            uint hash = 2166136261u;
            hash = (hash ^ QuantizeAupKilometer(cameraAup.x)) * 16777619u;
            hash = (hash ^ QuantizeAupKilometer(cameraAup.y)) * 16777619u;
            hash = (hash ^ QuantizeAupKilometer(cameraAup.z)) * 16777619u;
            return hash != 0u ? hash : 0x48534653u;
        }

        private static uint QuantizeAupKilometer(double value)
        {
            if (!math.isfinite(value))
                return 0u;

            double kilometers = System.Math.Floor(value * 0.001);
            if (kilometers >= int.MaxValue)
                return 0x7FFFFFFFu;
            if (kilometers <= int.MinValue)
                return 0x80000000u;

            return (uint)(int)kilometers;
        }

        private static ExosuitTuningDTO SanitizeManagedTuning(ExosuitTuningDTO tuning)
        {
            tuning.BaseMass = math.max(1f, math.isfinite(tuning.BaseMass) ? tuning.BaseMass : 8000f);
            float currentMass = math.isfinite(tuning.CurrentMass) ? tuning.CurrentMass : tuning.BaseMass;
            tuning.CurrentMass = currentMass > 0.0f ? math.max(1f, currentMass) : tuning.BaseMass;
            tuning.Drag = math.clamp(math.isfinite(tuning.Drag) ? tuning.Drag : 0.45f, 0f, 8f);
            tuning.ThrusterForce = math.max(0f, math.isfinite(tuning.ThrusterForce) ? tuning.ThrusterForce : 42000f);
            tuning.Radius = math.clamp(math.isfinite(tuning.Radius) ? tuning.Radius : 1.2f, 0.25f, 5f);
            tuning.ClampRange = math.max(tuning.Radius, math.isfinite(tuning.ClampRange) ? tuning.ClampRange : 2f);
            tuning.HydraulicLatencySeconds = math.clamp(math.isfinite(tuning.HydraulicLatencySeconds) ? tuning.HydraulicLatencySeconds : 0.5f, 0.05f, 3f);
            tuning.PurgeImpulse = math.max(0f, math.isfinite(tuning.PurgeImpulse) ? tuning.PurgeImpulse : 14f);
            tuning.GlobalQualityWeight = SanitizeQualityWeight01(tuning.GlobalQualityWeight, ExosuitMathGuards.DefaultQualityWeight);
            tuning.FootstepStrideMeters = math.max(0.25f, math.isfinite(tuning.FootstepStrideMeters) ? tuning.FootstepStrideMeters : 3f);
            tuning.MaxSpeedMetersPerSecond = math.max(0.25f, math.isfinite(tuning.MaxSpeedMetersPerSecond) ? tuning.MaxSpeedMetersPerSecond : 9f);
            tuning.CrushDepthMeters = math.max(1f, math.isfinite(tuning.CrushDepthMeters) ? tuning.CrushDepthMeters : 4000f);
            tuning.SdfEpsilonMeters = math.clamp(math.isfinite(tuning.SdfEpsilonMeters) ? tuning.SdfEpsilonMeters : 0.04f, 0.005f, 0.25f);
            tuning.GravityMultiplier = math.clamp(math.isfinite(tuning.GravityMultiplier) ? tuning.GravityMultiplier : 0.35f, 0f, 2f);
            tuning.MaxSubsteps = math.clamp(tuning.MaxSubsteps, 2u, 8u);
            tuning.StateHash = ComputeManagedHash(tuning);
            return tuning;
        }

        private static float SanitizeQualityWeight01(float value, float fallback)
        {
            float safeFallback = math.saturate(math.isfinite(fallback) ? fallback : ExosuitMathGuards.DefaultQualityWeight);
            return math.saturate(math.isfinite(value) ? value : safeFallback);
        }

        private static uint ComputeManagedHash(in ExosuitTuningDTO tuning)
        {
            uint hash = 2166136261u;
            hash = (hash ^ math.asuint(tuning.BaseMass)) * 16777619u;
            hash = (hash ^ math.asuint(tuning.ThrusterForce)) * 16777619u;
            hash = (hash ^ math.asuint(tuning.ClampRange)) * 16777619u;
            hash = (hash ^ math.asuint(tuning.GlobalQualityWeight)) * 16777619u;
            hash = (hash ^ math.asuint(tuning.SdfEpsilonMeters)) * 16777619u;
            hash = (hash ^ math.asuint(tuning.GravityMultiplier)) * 16777619u;
            hash = (hash ^ tuning.MaxSubsteps) * 16777619u;
            return hash != 0u ? hash : 1u;
        }

        private void TryRegisterFixed()
        {
            if (_registeredFixed || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterFixed()
        {
            if (!_registeredFixed)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
            _registeredFixed = false;
        }

        private void TryRegisterPostFixed()
        {
            if (_registeredPostFixed || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterPostFixed()
        {
            if (!_registeredPostFixed)
                return;

            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Player);
            _registeredPostFixed = false;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault currentVault = currentService as IDataVault;
                if (ReferenceEquals(_dataVault, currentVault))
                    return;

                if (!CompletePendingJobForRebind())
                    return;

                ReleaseVaultBuffers();
                _dataVault = currentVault;
                if (_dataVault != null && _runtimeActive && isActiveAndEnabled && EnsureBuffers(true))
                {
#if UNITY_EDITOR
                    TryApplyColdCsvOverrides();
#endif
                    WarmCoreBlackboxRoute();
                    s_activeRuntime = this;
                }
                else if (ReferenceEquals(s_activeRuntime, this))
                {
                    s_activeRuntime = null;
                }

                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregisterPostFixed();
            TryUnregisterFixed();
            TryUnregisterLateFrame();
            if (currentService == null || !isActiveAndEnabled)
                return;

            TryRegisterFixed();
            TryRegisterPostFixed();
            TryRegisterLateFrame();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

#if UNITY_EDITOR
        private static string ResolveProjectRoot()
        {
            string dataPath = Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath))
                return Path.GetFullPath(Path.Combine(dataPath, ".."));

            string currentDirectory = Directory.GetCurrentDirectory();
            return string.IsNullOrEmpty(currentDirectory)
                ? "."
                : Path.GetFullPath(currentDirectory);
        }
#endif

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!TryReadState(out ExosuitStateDTO state, out ExosuitSolverOutput output, out ExosuitTuningDTO tuning))
                return;

            Vector3 center = new Vector3(output.LocalPosition.x, output.LocalPosition.y, output.LocalPosition.z);
            float radius = math.max(0.25f, tuning.Radius);
            float halfHeight = radius * 1.35f;
            Vector3 top = center + Vector3.up * halfHeight;
            Vector3 bottom = center - Vector3.up * halfHeight;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawLine(top + Vector3.forward * radius, bottom + Vector3.forward * radius);
            Gizmos.DrawLine(top - Vector3.forward * radius, bottom - Vector3.forward * radius);
            Gizmos.DrawLine(top + Vector3.right * radius, bottom + Vector3.right * radius);
            Gizmos.DrawLine(top - Vector3.right * radius, bottom - Vector3.right * radius);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(center, center + new Vector3(output.PushNormal.x, output.PushNormal.y, output.PushNormal.z) * math.max(0.5f, output.PushOutMagnitude * 4f));
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(center, center + new Vector3(output.DesiredVelocity.x, output.DesiredVelocity.y, output.DesiredVelocity.z));
        }
#endif
    }
}
