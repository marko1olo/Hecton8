using System;
using System.IO;
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
    public sealed class ExosuitKinematicsRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, ILateFrameTickable
    {
        private const int TelemetryCapacity = 300;
        private const int CsvScratchCapacity = 4096;
        private const int TelemetryDumpHeaderSizeBytes = 24;
        private const int TelemetryEntrySizeBytes = 64;
        private const float DefaultCsvPollIntervalSeconds = 0.25f;
        private const ulong TelemetryDumpMagic = 0x00384E4F54434548UL; // HECTON8\0 in little-endian byte order
        private const uint TelemetryDumpVersion = 2u;
        private const uint ExoSourceHash = 0x53484E34u; // SHN4
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
        private VaultGenerationHandle<byte> _csvScratchHandle;

        private JobHandle _jobHandle;
        private long _jobStartTimestamp;
        private bool _jobScheduled;
        private bool _jobBuffersLocked;
        private bool _voxelSdfBufferLocked;
        private bool _voxelSdfDescriptorLocked;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredLateFrame;
        private bool _pendingDisableTeardown;
        private bool _buffersInitialized;
        private bool _coldCsvApplied;
        private bool _signalLanesReady;
        private string _projectRoot;
        private string _csvPath;
        private long _lastCsvWriteTicks;
        private float _csvPollCountdown;
        private uint _scheduledFrame;
        private uint _lastDumpFrame = uint.MaxValue;
        private uint _pendingMockProceduralWeightMilli = 1000u;
        private ExosuitFrameInputDTO _pendingFrameInput;

        private static ExosuitKinematicsRuntime s_activeRuntime;

        private void Awake()
        {
            _cachedTransform = transform;
            _projectRoot = ResolveProjectRoot();
            _csvPath = Path.Combine(_projectRoot, "Data", "Physics", "exosuit_performance_profiles.csv");
            EnsureSignalLanesReady();
        }

        private void OnEnable()
        {
            _pendingDisableTeardown = false;
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
            if (EnsureBuffers(true))
            {
                TryApplyColdCsvOverrides();
                s_activeRuntime = this;
            }

            TryRegisterFixed();
            TryRegisterPostFixed();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            _pendingDisableTeardown = true;
            TryUnregisterPostFixed();
            TryUnregisterFixed();

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
            _pendingDisableTeardown = false;
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            if (_jobScheduled || !Application.isPlaying)
                return;

            if (!EnsureBuffers(true))
                return;

            float safeDeltaTime = math.clamp(math.isfinite(fixedDeltaTime) ? fixedDeltaTime : 0.02f, 0.0001f, 0.05f);
#if UNITY_EDITOR
            TryApplyCsvOverrides(safeDeltaTime, false);
#endif
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

        private bool TryResolveBuffer<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return _dataVault != null &&
                   IsHandleCreated(in handle) &&
                   _dataVault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private bool TryResolveHeldJobWriteBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return _jobBuffersLocked &&
                   _dataVault != null &&
                   IsHandleCreated(in handle) &&
                   handle.BufferID == (uint)expectedBufferId &&
                   _dataVault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private bool TryAcquireWriteBuffer<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (_dataVault == null ||
                !IsHandleCreated(in handle) ||
                !_dataVault.TryAcquireWriteLock(in handle, SystemID.Physics, out buffer))
            {
                return false;
            }

            if (buffer.IsCreated && buffer.Length > 0)
                return true;

            _dataVault.ReleaseWriteLock(in handle, SystemID.Physics);
            buffer = default;
            return false;
        }

        private void ReleaseWriteBuffer<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (_dataVault != null && IsHandleCreated(in handle))
                _dataVault.ReleaseWriteLock(in handle, SystemID.Physics);
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
            ReleaseVaultBuffer(vault, ref _csvScratchHandle);
            _buffersInitialized = false;
            _coldCsvApplied = false;
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
                AllocateVaultBuffers(_dataVault);

            if (!IsHandleCreated(in _stateHandle))
                return false;

            if (!_buffersInitialized && allowColdInitialization)
            {
                _buffersInitialized = GenerateEmergencyMockExoData();
            }

            return true;
        }

        private void AllocateVaultBuffers(IDataVault vault)
        {
            _stateHandle = vault.GetGenerationHandle<ExosuitStateDTO>(BufferID.ShinobuExosuitState, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _inputHandle = vault.GetGenerationHandle<ExosuitFrameInputDTO>(BufferID.ShinobuExosuitFrameInput, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.GetGenerationHandle<ExosuitTuningDTO>(BufferID.ShinobuExosuitTuning, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _terrainHandle = vault.GetGenerationHandle<MockTerrainSDF>(BufferID.ShinobuExosuitMockTerrainSdf, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _flowHandle = vault.GetGenerationHandle<MockFlowField>(BufferID.ShinobuExosuitMockFlowField, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _crushDepthHandle = vault.GetGenerationHandle<MockCrushDepthSignal>(BufferID.ShinobuExosuitMockCrushDepth, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _outputHandle = vault.GetGenerationHandle<ExosuitSolverOutput>(BufferID.ShinobuExosuitSolverOutput, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _screenHandle = vault.GetGenerationHandle<ExoScreenDTO>(BufferID.ShinobuExosuitScreenDto, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.GetGenerationHandle<ExosuitTelemetryEntry>(BufferID.ShinobuExosuitTelemetryRing, TelemetryCapacity, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = vault.GetGenerationHandle<int>(BufferID.ShinobuExosuitTelemetryCursor, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _footstepAccumulatorHandle = vault.GetGenerationHandle<float>(BufferID.ShinobuExosuitFootstepAccumulator, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _hapticHandle = vault.GetGenerationHandle<MechHapticSignalDTO>(BufferID.ShinobuExosuitHapticSignals, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _siltHandle = vault.GetGenerationHandle<SiltExplosionSignal>(BufferID.ShinobuExosuitSiltSignals, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _acousticHandle = vault.GetGenerationHandle<ExosuitAcousticEchoTap>(BufferID.ShinobuExosuitAcousticTaps, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = vault.GetGenerationHandle<byte>(BufferID.ShinobuExosuitCsvScratch, CsvScratchCapacity, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            ExosuitKinematicAuthority.Bind(vault, in _inputHandle);
        }

        private bool GenerateEmergencyMockExoData()
        {
            bool stateLocked = TryAcquireWriteBuffer(in _stateHandle, out NativeArray<ExosuitStateDTO> stateBuffer);
            if (!stateLocked)
                return false;

            bool tuningLocked = false;
            bool inputLocked = false;
            bool terrainLocked = false;
            bool flowLocked = false;
            bool crushLocked = false;
            bool outputLocked = false;
            bool screenLocked = false;
            bool hapticLocked = false;
            bool siltLocked = false;
            bool acousticLocked = false;
            bool cursorLocked = false;
            bool footstepLocked = false;
            try
            {
                if (!(tuningLocked = TryAcquireWriteBuffer(in _tuningHandle, out NativeArray<ExosuitTuningDTO> tuningBuffer)) ||
                    !(inputLocked = TryAcquireWriteBuffer(in _inputHandle, out NativeArray<ExosuitFrameInputDTO> inputBuffer)) ||
                    !(terrainLocked = TryAcquireWriteBuffer(in _terrainHandle, out NativeArray<MockTerrainSDF> terrainBuffer)) ||
                    !(flowLocked = TryAcquireWriteBuffer(in _flowHandle, out NativeArray<MockFlowField> flowBuffer)) ||
                    !(crushLocked = TryAcquireWriteBuffer(in _crushDepthHandle, out NativeArray<MockCrushDepthSignal> crushBuffer)) ||
                    !(outputLocked = TryAcquireWriteBuffer(in _outputHandle, out NativeArray<ExosuitSolverOutput> outputBuffer)) ||
                    !(screenLocked = TryAcquireWriteBuffer(in _screenHandle, out NativeArray<ExoScreenDTO> screenBuffer)) ||
                    !(hapticLocked = TryAcquireWriteBuffer(in _hapticHandle, out NativeArray<MechHapticSignalDTO> hapticBuffer)) ||
                    !(siltLocked = TryAcquireWriteBuffer(in _siltHandle, out NativeArray<SiltExplosionSignal> siltBuffer)) ||
                    !(acousticLocked = TryAcquireWriteBuffer(in _acousticHandle, out NativeArray<ExosuitAcousticEchoTap> acousticBuffer)) ||
                    !(cursorLocked = TryAcquireWriteBuffer(in _telemetryCursorHandle, out NativeArray<int> cursorBuffer)) ||
                    !(footstepLocked = TryAcquireWriteBuffer(in _footstepAccumulatorHandle, out NativeArray<float> footstepBuffer)))
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
                if (footstepLocked)
                    ReleaseWriteBuffer(in _footstepAccumulatorHandle);
                if (cursorLocked)
                    ReleaseWriteBuffer(in _telemetryCursorHandle);
                if (acousticLocked)
                    ReleaseWriteBuffer(in _acousticHandle);
                if (siltLocked)
                    ReleaseWriteBuffer(in _siltHandle);
                if (hapticLocked)
                    ReleaseWriteBuffer(in _hapticHandle);
                if (screenLocked)
                    ReleaseWriteBuffer(in _screenHandle);
                if (outputLocked)
                    ReleaseWriteBuffer(in _outputHandle);
                if (crushLocked)
                    ReleaseWriteBuffer(in _crushDepthHandle);
                if (flowLocked)
                    ReleaseWriteBuffer(in _flowHandle);
                if (terrainLocked)
                    ReleaseWriteBuffer(in _terrainHandle);
                if (inputLocked)
                    ReleaseWriteBuffer(in _inputHandle);
                if (tuningLocked)
                    ReleaseWriteBuffer(in _tuningHandle);
                if (stateLocked)
                    ReleaseWriteBuffer(in _stateHandle);
            }
        }

        private uint WriteFrameInputs()
        {
            if (!TryResolveBuffer(in _screenHandle, out NativeArray<ExoScreenDTO> screenBuffer))
                return _scheduledFrame;

            bool tuningLocked = TryAcquireWriteBuffer(in _tuningHandle, out NativeArray<ExosuitTuningDTO> tuningBuffer);
            if (!tuningLocked)
                return _scheduledFrame;

            bool inputLocked = false;
            bool terrainLocked = false;
            bool flowLocked = false;
            bool crushLocked = false;
            try
            {
                if (!(inputLocked = TryAcquireWriteBuffer(in _inputHandle, out NativeArray<ExosuitFrameInputDTO> inputBuffer)) ||
                    !(terrainLocked = TryAcquireWriteBuffer(in _terrainHandle, out NativeArray<MockTerrainSDF> terrainBuffer)) ||
                    !(flowLocked = TryAcquireWriteBuffer(in _flowHandle, out NativeArray<MockFlowField> flowBuffer)) ||
                    !(crushLocked = TryAcquireWriteBuffer(in _crushDepthHandle, out NativeArray<MockCrushDepthSignal> crushBuffer)))
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
                if (crushLocked)
                    ReleaseWriteBuffer(in _crushDepthHandle);
                if (flowLocked)
                    ReleaseWriteBuffer(in _flowHandle);
                if (terrainLocked)
                    ReleaseWriteBuffer(in _terrainHandle);
                if (inputLocked)
                    ReleaseWriteBuffer(in _inputHandle);
                if (tuningLocked)
                    ReleaseWriteBuffer(in _tuningHandle);
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

            TryResolveVoxelSdfPayload(
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
            float elapsedMs = ResolveElapsedJobMs();
            bool budgetExceeded = elapsedMs > 0.1f;
            JobAdmissionScheduleExtensions.ReportAdmittedJobCompleted<ExosuitKinematicIntegrationJob>(JobAdmissionLane.Lane0_Critical, elapsedMs);
            PatchLastTelemetryElapsed(elapsedMs, budgetExceeded);
            UnlockVoxelSdfPayloadBuffers();
            if (budgetExceeded)
                DumpTelemetryBuffer();
            EmitReadbackSignals();
            UnlockJobBuffers();
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
            if (!TryResolveHeldJobWriteBuffer(in _telemetryHandle, BufferID.ShinobuExosuitTelemetryRing, out NativeArray<ExosuitTelemetryEntry> telemetry) ||
                !TryResolveHeldJobWriteBuffer(in _telemetryCursorHandle, BufferID.ShinobuExosuitTelemetryCursor, out NativeArray<int> cursorBuffer))
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

        private void EmitReadbackSignals()
        {
            if (!TryResolveBuffer(in _outputHandle, out NativeArray<ExosuitSolverOutput> outputBuffer) ||
                !TryResolveBuffer(in _stateHandle, out NativeArray<ExosuitStateDTO> stateBuffer))
                return;

            ExosuitSolverOutput output = outputBuffer[0];
            ExosuitStateDTO state = stateBuffer[0];

            if ((output.Flags & ExosuitSolverOutput.FlagHaptic) != 0u)
                EmitHaptics(output.Frame);
            if ((output.Flags & ExosuitSolverOutput.FlagSilt) != 0u)
                EmitSilt();
            if ((output.Flags & ExosuitSolverOutput.FlagAcousticTap) != 0u)
                EmitAcoustic();
            if ((output.Flags & ExosuitSolverOutput.FlagFault) != 0u ||
                (state.Flags & ExosuitStateFlags.NaNDetected) != 0u)
            {
                DumpTelemetryBuffer();
            }
        }

        private void EmitHaptics(uint frame)
        {
            if (!TryResolveBuffer(in _hapticHandle, out NativeArray<MechHapticSignalDTO> haptics))
                return;

            MechHapticSignalDTO mech = haptics[0];
            float amplitude = math.isfinite(mech.Amplitude) ? math.saturate(mech.Amplitude) : 0.0f;
            if (amplitude <= 0.0f)
                return;

            mech.Amplitude = amplitude;
            mech.Duration = math.isfinite(mech.Duration) ? math.max(0.01f, mech.Duration) : 0.01f;
            mech.Frequency = math.isfinite(mech.Frequency) ? math.max(0.0f, mech.Frequency) : 0.0f;
            SignalBus<MechHapticSignalDTO>.Push(in mech);

            bool lowFrequencyLoad = mech.Frequency <= 20.0f;
            HapticRequest request = default;
            request.Intensity01 = amplitude;
            request.DurationSeconds = mech.Duration;
            request.Frequency01 = math.saturate(mech.Frequency * (1.0f / 60.0f));
            request.SourceHash = ExoSourceHash;
            request.Frame = frame;
            request.Channel = lowFrequencyLoad ? HapticRequest.ChannelCrush : HapticRequest.ChannelGearScrape;
            request.Flags = lowFrequencyLoad ? HapticRequest.FlagCrush : HapticRequest.FlagLightThud;
            SignalBus<HapticRequest>.Push(in request);
        }

        private void EmitSilt()
        {
            if (TryResolveBuffer(in _siltHandle, out NativeArray<SiltExplosionSignal> siltBuffer) &&
                siltBuffer[0].Intensity01 > 0.0f)
            {
                SiltExplosionSignal silt = siltBuffer[0];
                if (!math.all(math.isfinite(silt.AUP)))
                    return;
                silt.Intensity01 = math.isfinite(silt.Intensity01) ? math.saturate(silt.Intensity01) : 0.0f;
                if (silt.Intensity01 <= 0.0f)
                    return;
                SignalBus<SiltExplosionSignal>.Push(in silt);
            }
        }

        private void EmitAcoustic()
        {
            if (TryResolveBuffer(in _acousticHandle, out NativeArray<ExosuitAcousticEchoTap> acousticBuffer) &&
                acousticBuffer[0].Intensity01 > 0.0f)
            {
                ExosuitAcousticEchoTap tap = acousticBuffer[0];
                if (!math.all(math.isfinite(tap.AUP)))
                    return;
                tap.Intensity01 = math.isfinite(tap.Intensity01) ? math.saturate(tap.Intensity01) : 0.0f;
                if (tap.Intensity01 <= 0.0f)
                    return;
                SignalBus<ExosuitAcousticEchoTap>.Push(in tap);
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

            if (_jobBuffersLocked || _dataVault == null)
                return false;

            int locked = 0;
            if (!TryAcquireJobWriteBuffer(in _stateHandle, BufferID.ShinobuExosuitState, out state))
                return false;
            locked++;

            if (!TryAcquireJobWriteBuffer(in _inputHandle, BufferID.ShinobuExosuitFrameInput, out input))
                return RollbackJobBufferLocks(locked);
            locked++;

            if (!TryAcquireJobWriteBuffer(in _tuningHandle, BufferID.ShinobuExosuitTuning, out tuning))
                return RollbackJobBufferLocks(locked);
            locked++;

            if (!TryAcquireJobReadBuffer(in _terrainHandle, BufferID.ShinobuExosuitMockTerrainSdf, out terrain))
                return RollbackJobBufferLocks(locked);
            locked++;

            if (!TryAcquireJobReadBuffer(in _flowHandle, BufferID.ShinobuExosuitMockFlowField, out flow))
                return RollbackJobBufferLocks(locked);
            locked++;

            if (!TryAcquireJobReadBuffer(in _crushDepthHandle, BufferID.ShinobuExosuitMockCrushDepth, out crushDepth))
                return RollbackJobBufferLocks(locked);
            locked++;

            if (!TryAcquireJobWriteBuffer(in _outputHandle, BufferID.ShinobuExosuitSolverOutput, out output))
                return RollbackJobBufferLocks(locked);
            locked++;

            if (!TryAcquireJobWriteBuffer(in _screenHandle, BufferID.ShinobuExosuitScreenDto, out screen))
                return RollbackJobBufferLocks(locked);
            locked++;

            if (!TryAcquireJobWriteBuffer(in _telemetryHandle, BufferID.ShinobuExosuitTelemetryRing, out telemetry))
                return RollbackJobBufferLocks(locked);
            locked++;

            if (!TryAcquireJobWriteBuffer(in _telemetryCursorHandle, BufferID.ShinobuExosuitTelemetryCursor, out telemetryCursor))
                return RollbackJobBufferLocks(locked);
            locked++;

            if (!TryAcquireJobWriteBuffer(in _footstepAccumulatorHandle, BufferID.ShinobuExosuitFootstepAccumulator, out footstepAccumulator))
                return RollbackJobBufferLocks(locked);
            locked++;

            if (!TryAcquireJobWriteBuffer(in _hapticHandle, BufferID.ShinobuExosuitHapticSignals, out haptics))
                return RollbackJobBufferLocks(locked);
            locked++;

            if (!TryAcquireJobWriteBuffer(in _siltHandle, BufferID.ShinobuExosuitSiltSignals, out silt))
                return RollbackJobBufferLocks(locked);
            locked++;

            if (!TryAcquireJobWriteBuffer(in _acousticHandle, BufferID.ShinobuExosuitAcousticTaps, out acoustic))
                return RollbackJobBufferLocks(locked);

            if (telemetry.Length < TelemetryCapacity)
            {
                UnlockJobBuffers(JobBufferLockCount);
                return false;
            }

            _jobBuffersLocked = true;
            return true;
        }

        private bool TryResolveVoxelSdfPayload(
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

            if (_voxelSdfBufferLocked || _voxelSdfDescriptorLocked)
                UnlockVoxelSdfPayloadBuffers();

            if (!math.all(math.isfinite(state.AUP_Position)) ||
                !math.all(math.isfinite(terrain.CameraAup)))
            {
                return false;
            }

            double3 localDouble = state.AUP_Position - terrain.CameraAup;
            if (!math.all(math.isfinite(localDouble)))
                return false;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryLockBuffer(BufferID.VoxelSdfPayloadDescriptor, SystemID.Physics))
            {
                return false;
            }

            _voxelSdfDescriptorLocked = true;
            if (!vault.TryGetGenerationHandle<VoxelSdfPayloadDescriptorDTO>(BufferID.VoxelSdfPayloadDescriptor, out VaultGenerationHandle<VoxelSdfPayloadDescriptorDTO> descriptorHandle) ||
                !vault.TryReadHandle(in descriptorHandle, out NativeArray<VoxelSdfPayloadDescriptorDTO> descriptors) ||
                !descriptors.IsCreated ||
                descriptors.Length <= 0)
            {
                UnlockVoxelSdfPayloadBuffers();
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
                UnlockVoxelSdfPayloadBuffers();
                return false;
            }

            if (!vault.TryLockBuffer(BufferID.VoxelSdfTexture3D, SystemID.Physics))
            {
                UnlockVoxelSdfPayloadBuffers();
                return false;
            }

            _voxelSdfBufferLocked = true;
            if (!vault.TryGetGenerationHandle<byte>(BufferID.VoxelSdfTexture3D, out VaultGenerationHandle<byte> sdfHandle) ||
                sdfHandle.Generation != descriptor.BufferGeneration ||
                !vault.TryReadHandle(in sdfHandle, out NativeArray<byte> resolvedSdf) ||
                !resolvedSdf.IsCreated ||
                resolvedSdf.Length < expectedLength)
            {
                UnlockVoxelSdfPayloadBuffers();
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
                UnlockVoxelSdfPayloadBuffers();
                return false;
            }

            voxelSdf = resolvedSdf;
            dimensions = resolvedDimensions;
            origin = resolvedOrigin;
            cellSize = resolvedCellSize;
            rangeMeters = resolvedRange;
            return true;
        }

        private bool TryAcquireJobWriteBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (_dataVault == null ||
                !IsHandleCreated(in handle) ||
                handle.BufferID != (uint)expectedBufferId ||
                !_dataVault.TryAcquireWriteLock(in handle, SystemID.Physics, out buffer))
            {
                return false;
            }

            if (buffer.IsCreated && buffer.Length > 0)
                return true;

            _dataVault.ReleaseWriteLock(in handle, SystemID.Physics);
            buffer = default;
            return false;
        }

        private bool TryAcquireJobReadBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (_dataVault == null ||
                !IsHandleCreated(in handle) ||
                handle.BufferID != (uint)expectedBufferId ||
                !_dataVault.TryLockBuffer(expectedBufferId, SystemID.Physics))
            {
                return false;
            }

            if (TryResolveBuffer(in handle, out buffer))
                return true;

            _dataVault.TryUnlockBuffer(expectedBufferId, SystemID.Physics);
            buffer = default;
            return false;
        }

        private bool RollbackJobBufferLocks(int lockedCount)
        {
            UnlockJobBuffers(lockedCount);
            return false;
        }

        private void UnlockJobBuffers()
        {
            UnlockVoxelSdfPayloadBuffers();
            if (!_jobBuffersLocked)
                return;

            UnlockJobBuffers(JobBufferLockCount);
            _jobBuffersLocked = false;
        }

        private void UnlockVoxelSdfPayloadBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault != null && _voxelSdfBufferLocked)
                vault.TryUnlockBuffer(BufferID.VoxelSdfTexture3D, SystemID.Physics);

            if (vault != null && _voxelSdfDescriptorLocked)
                vault.TryUnlockBuffer(BufferID.VoxelSdfPayloadDescriptor, SystemID.Physics);

            _voxelSdfBufferLocked = false;
            _voxelSdfDescriptorLocked = false;
        }

        private void UnlockJobBuffers(int lockedCount)
        {
            if (_dataVault == null)
                return;

            for (int i = lockedCount - 1; i >= 0; i--)
                UnlockJobBuffer(i);
        }

        private void UnlockJobBuffer(int index)
        {
            switch (index)
            {
                case 0:
                    ReleaseWriteBuffer(in _stateHandle);
                    break;
                case 1:
                    ReleaseWriteBuffer(in _inputHandle);
                    break;
                case 2:
                    ReleaseWriteBuffer(in _tuningHandle);
                    break;
                case 3:
                case 4:
                case 5:
                    _dataVault.TryUnlockBuffer(ResolveJobBufferId(index), SystemID.Physics);
                    break;
                case 6:
                    ReleaseWriteBuffer(in _outputHandle);
                    break;
                case 7:
                    ReleaseWriteBuffer(in _screenHandle);
                    break;
                case 8:
                    ReleaseWriteBuffer(in _telemetryHandle);
                    break;
                case 9:
                    ReleaseWriteBuffer(in _telemetryCursorHandle);
                    break;
                case 10:
                    ReleaseWriteBuffer(in _footstepAccumulatorHandle);
                    break;
                case 11:
                    ReleaseWriteBuffer(in _hapticHandle);
                    break;
                case 12:
                    ReleaseWriteBuffer(in _siltHandle);
                    break;
                default:
                    ReleaseWriteBuffer(in _acousticHandle);
                    break;
            }
        }

        private const int JobBufferLockCount = 14;

        private static BufferID ResolveJobBufferId(int index)
        {
            switch (index)
            {
                case 0: return BufferID.ShinobuExosuitState;
                case 1: return BufferID.ShinobuExosuitFrameInput;
                case 2: return BufferID.ShinobuExosuitTuning;
                case 3: return BufferID.ShinobuExosuitMockTerrainSdf;
                case 4: return BufferID.ShinobuExosuitMockFlowField;
                case 5: return BufferID.ShinobuExosuitMockCrushDepth;
                case 6: return BufferID.ShinobuExosuitSolverOutput;
                case 7: return BufferID.ShinobuExosuitScreenDto;
                case 8: return BufferID.ShinobuExosuitTelemetryRing;
                case 9: return BufferID.ShinobuExosuitTelemetryCursor;
                case 10: return BufferID.ShinobuExosuitFootstepAccumulator;
                case 11: return BufferID.ShinobuExosuitHapticSignals;
                case 12: return BufferID.ShinobuExosuitSiltSignals;
                default: return BufferID.ShinobuExosuitAcousticTaps;
            }
        }

        private void TryApplyCsvOverrides(float deltaTime, bool force)
        {
            if (!force)
            {
                _csvPollCountdown -= math.max(0.0f, deltaTime);
                if (_csvPollCountdown > 0.0f)
                    return;

                _csvPollCountdown = DefaultCsvPollIntervalSeconds;
            }

            if (string.IsNullOrEmpty(_csvPath))
                return;
            if (!File.Exists(_csvPath))
                return;

            long writeTicks = File.GetLastWriteTimeUtc(_csvPath).Ticks;
            if (!force && writeTicks == _lastCsvWriteTicks)
                return;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsHandleCreated(in _csvScratchHandle) ||
                !IsHandleCreated(in _tuningHandle) ||
                !vault.TryAcquireWriteLock(in _csvScratchHandle, SystemID.Physics, out NativeArray<byte> scratch))
            {
                return;
            }

            bool commitWriteTicks = false;
            try
            {
                int count = ReadCsvBytes(_csvPath, scratch);
                if (count <= 0)
                {
                    commitWriteTicks = true;
                    return;
                }

                if (!vault.TryAcquireWriteLock(in _tuningHandle, SystemID.Physics, out NativeArray<ExosuitTuningDTO> tuningBuffer))
                    return;

                try
                {
                    if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                        return;

                    ExosuitTuningDTO tuning = tuningBuffer[0];
                    if (ParseCsvIntoTuning(scratch, count, ref tuning))
                    {
                        tuning.CsvVersion++;
                        tuningBuffer[0] = SanitizeManagedTuning(tuning);
                    }

                    commitWriteTicks = true;
                }
                finally
                {
                    vault.ReleaseWriteLock(in _tuningHandle, SystemID.Physics);
                }
            }
            finally
            {
                vault.ReleaseWriteLock(in _csvScratchHandle, SystemID.Physics);
                if (commitWriteTicks)
                    _lastCsvWriteTicks = writeTicks;
            }
        }

        private void TryApplyColdCsvOverrides()
        {
            if (_coldCsvApplied || !_buffersInitialized)
                return;

            TryApplyCsvOverrides(DefaultCsvPollIntervalSeconds, true);
            _coldCsvApplied = true;
        }

        private static unsafe int ReadCsvBytes(string path, NativeArray<byte> scratch)
        {
            int count = 0;
            if (!scratch.IsCreated || scratch.Length <= 0)
                return 0;

            byte* bytes = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
            Span<byte> target = new Span<byte>(bytes, scratch.Length);
            using (FileStream stream = File.OpenRead(path))
            {
                while (count < target.Length)
                {
                    int read = stream.Read(target.Slice(count));
                    if (read <= 0)
                        break;

                    count += read;
                }

                if (count >= target.Length && stream.Position < stream.Length)
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

        private void DumpTelemetryBuffer()
        {
            uint frame = _scheduledFrame;
            if (_lastDumpFrame == frame)
                return;

            if (!TryResolveBuffer(in _telemetryHandle, out NativeArray<ExosuitTelemetryEntry> telemetry) ||
                !TryResolveBuffer(in _telemetryCursorHandle, out NativeArray<int> cursorBuffer))
                return;

            _lastDumpFrame = frame;

            string directory = Path.Combine(_projectRoot, "Docs", "AgentLogs");
            Directory.CreateDirectory(directory);
            WriteTelemetryDump(Path.Combine(directory, "Dump_EXO_KINEMATICS.bin"), telemetry, cursorBuffer);
            WriteTelemetryDump(Path.Combine(directory, "Dump_SHINOBU_276.bin"), telemetry, cursorBuffer);
        }

        private static unsafe void WriteTelemetryDump(string path, NativeArray<ExosuitTelemetryEntry> telemetry, NativeArray<int> cursorBuffer)
        {
            int cursor = cursorBuffer.IsCreated && cursorBuffer.Length > 0 ? cursorBuffer[0] : 0;
            if ((uint)cursor >= (uint)telemetry.Length)
                cursor = 0;

            int entrySize = UnsafeUtility.SizeOf<ExosuitTelemetryEntry>();
            if (entrySize != TelemetryEntrySizeBytes)
                return;

            Span<byte> header = stackalloc byte[TelemetryDumpHeaderSizeBytes];
            int offset = 0;
            WriteUInt64LittleEndian(header, ref offset, TelemetryDumpMagic);
            WriteUInt32LittleEndian(header, ref offset, TelemetryDumpVersion);
            WriteUInt32LittleEndian(header, ref offset, (uint)telemetry.Length);
            WriteUInt32LittleEndian(header, ref offset, (uint)entrySize);
            WriteUInt32LittleEndian(header, ref offset, (uint)cursor);

            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                stream.Write(header);
                int firstCount = telemetry.Length - cursor;
                if (firstCount > 0)
                    stream.Write(new ReadOnlySpan<byte>(basePtr + cursor * entrySize, firstCount * entrySize));
                if (cursor > 0)
                    stream.Write(new ReadOnlySpan<byte>(basePtr, cursor * entrySize));
            }
        }

        private static void WriteUInt32LittleEndian(Span<byte> target, ref int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
            offset += 4;
        }

        private static void WriteUInt64LittleEndian(Span<byte> target, ref int offset, ulong value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
            target[offset + 4] = (byte)(value >> 32);
            target[offset + 5] = (byte)(value >> 40);
            target[offset + 6] = (byte)(value >> 48);
            target[offset + 7] = (byte)(value >> 56);
            offset += 8;
        }

        private void EnsureSignalLanesReady()
        {
            if (_signalLanesReady)
                return;

            SignalBus<MechHapticSignalDTO>.Configure(MechHapticExpectedSignals, MechHapticMaxFrameSignals, MechHapticMinimumQualityFrameSignals, MechHapticLaneHash);
            SignalBus<SiltExplosionSignal>.Configure(SiltExpectedSignals, SiltMaxFrameSignals, SiltMinimumQualityFrameSignals, SiltLaneHash);
            SignalBus<ExosuitAcousticEchoTap>.Configure(AcousticExpectedSignals, AcousticMaxFrameSignals, AcousticMinimumQualityFrameSignals, AcousticLaneHash);
            SignalBus<MechHapticSignalDTO>.EnsureInitialized();
            SignalBus<SiltExplosionSignal>.EnsureInitialized();
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
            tuning.GlobalQualityWeight = ExosuitMathGuards.DefaultQualityWeight;
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
            var originAup = GlobalSignals.CurrentRuntimeOriginAup();
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
