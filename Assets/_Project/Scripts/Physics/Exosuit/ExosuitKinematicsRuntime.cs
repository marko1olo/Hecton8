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
        [SerializeField, Range(0f, 1f), Tooltip("Continuous solver quality weight. Low values collapse to central SDF probes.")]
        private float _globalQualityWeight = 0.62f;
        [SerializeField, Range(0.25f, 12f), Tooltip("Meters between heavy acoustic stomp taps.")]
        private float _footstepStrideMeters = 3f;
        [SerializeField, Min(1f), Tooltip("Depth at which mock crush pressure reaches 1.0.")]
        private float _crushDepthMeters = 4000f;
        [SerializeField, Tooltip("Presentation-only transform drive from solver output.")]
        private bool _driveTransformFromSolver = true;

        private IDataVault _dataVault;
        private Transform _cachedTransform;
        private VaultBufferHandle<ExosuitStateDTO> _stateHandle;
        private VaultBufferHandle<MockInputBuffer> _inputHandle;
        private VaultBufferHandle<ExosuitTuningDTO> _tuningHandle;
        private VaultBufferHandle<MockTerrainSDF> _terrainHandle;
        private VaultBufferHandle<MockFlowField> _flowHandle;
        private VaultBufferHandle<MockCrushDepthSignal> _crushDepthHandle;
        private VaultBufferHandle<ExosuitSolverOutput> _outputHandle;
        private VaultBufferHandle<ExoScreenDTO> _screenHandle;
        private VaultBufferHandle<ExosuitTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<int> _telemetryCursorHandle;
        private VaultBufferHandle<float> _footstepAccumulatorHandle;
        private VaultBufferHandle<MechHapticSignalDTO> _hapticHandle;
        private VaultBufferHandle<SiltExplosionSignal> _siltHandle;
        private VaultBufferHandle<AcousticEchoTap> _acousticHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;

        private JobHandle _jobHandle;
        private long _jobStartTimestamp;
        private bool _jobScheduled;
        private bool _jobBuffersLocked;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredLateFrame;
        private bool _pendingDisableTeardown;
        private bool _buffersInitialized;
        private bool _signalLanesReady;
        private string _projectRoot;
        private string _csvPath;
        private long _lastCsvWriteTicks;
        private float _csvPollCountdown;
        private uint _scheduledFrame;
        private uint _lastDumpFrame = uint.MaxValue;

        private static ExosuitKinematicsRuntime s_activeRuntime;

        private void Awake()
        {
            _cachedTransform = transform;
            _projectRoot = ResolveProjectRoot();
            _csvPath = Path.Combine(_projectRoot, "exo_physics.csv");
            EnsureSignalLanesReady();
        }

        private void OnEnable()
        {
            _pendingDisableTeardown = false;
            _dataVault = GlobalRegistry.DataVault;
            if (EnsureBuffers(true))
                s_activeRuntime = this;

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TryApplyCsvOverrides(safeDeltaTime);
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
            ExosuitKinematicsRuntime runtime = s_activeRuntime;
            if (runtime != null && runtime._jobBuffersLocked)
                return false;

            IDataVault vault = ResolveVault();
            if (vault == null || !vault.TryGetBufferHandle(BufferID.ShinobuExosuitTuning, out VaultBufferHandle<ExosuitTuningDTO> handle))
                return false;

            NativeArray<ExosuitTuningDTO> buffer = handle.Resolve(vault);
            if (!buffer.IsCreated || buffer.Length <= 0)
                return false;

            tuning = buffer[0];
            return true;
        }

        /// <summary>
        /// Writes sanitized tuning values into the DataVault authority buffer.
        /// </summary>
        public static bool TryWriteTuning(in ExosuitTuningDTO tuning)
        {
            ExosuitKinematicsRuntime runtime = s_activeRuntime;
            if (runtime != null && runtime._jobBuffersLocked)
                return false;

            IDataVault vault = ResolveVault();
            if (vault == null || !vault.TryGetBufferHandle(BufferID.ShinobuExosuitTuning, out VaultBufferHandle<ExosuitTuningDTO> handle))
                return false;

            ref ExosuitTuningDTO target = ref handle.GetElementAsRef(vault, 0);
            target = SanitizeManagedTuning(tuning);
            return true;
        }

        /// <summary>
        /// Reads state, solver output, and tuning for editor-only visualizers.
        /// </summary>
        public static bool TryReadState(out ExosuitStateDTO state, out ExosuitSolverOutput output, out ExosuitTuningDTO tuning)
        {
            state = default;
            output = default;
            tuning = default;
            ExosuitKinematicsRuntime runtime = s_activeRuntime;
            if (runtime != null && runtime._jobBuffersLocked)
                return false;

            IDataVault vault = ResolveVault();
            if (vault == null)
                return false;

            if (!vault.TryGetBufferHandle(BufferID.ShinobuExosuitState, out VaultBufferHandle<ExosuitStateDTO> stateHandle) ||
                !vault.TryGetBufferHandle(BufferID.ShinobuExosuitSolverOutput, out VaultBufferHandle<ExosuitSolverOutput> outputHandle) ||
                !vault.TryGetBufferHandle(BufferID.ShinobuExosuitTuning, out VaultBufferHandle<ExosuitTuningDTO> tuningHandle))
            {
                return false;
            }

            NativeArray<ExosuitStateDTO> stateBuffer = stateHandle.Resolve(vault);
            NativeArray<ExosuitSolverOutput> outputBuffer = outputHandle.Resolve(vault);
            NativeArray<ExosuitTuningDTO> tuningBuffer = tuningHandle.Resolve(vault);
            if (!stateBuffer.IsCreated || stateBuffer.Length <= 0 ||
                !outputBuffer.IsCreated || outputBuffer.Length <= 0 ||
                !tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
            {
                return false;
            }

            state = stateBuffer[0];
            output = outputBuffer[0];
            tuning = tuningBuffer[0];
            return true;
        }

        private static IDataVault ResolveVault()
        {
            return s_activeRuntime != null && s_activeRuntime._dataVault != null
                ? s_activeRuntime._dataVault
                : GlobalRegistry.DataVault;
        }

        private bool EnsureBuffers(bool allowColdInitialization)
        {
            if (_dataVault == null)
                return false;

            if (!_stateHandle.IsCreated)
                AllocateVaultBuffers(_dataVault);

            if (!_stateHandle.IsCreated)
                return false;

            if (!_buffersInitialized && allowColdInitialization)
            {
                GenerateEmergencyMockExoData();
                _buffersInitialized = true;
            }

            return true;
        }

        private void AllocateVaultBuffers(IDataVault vault)
        {
            _stateHandle = vault.GetBufferHandle<ExosuitStateDTO>(BufferID.ShinobuExosuitState, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _inputHandle = vault.GetBufferHandle<MockInputBuffer>(BufferID.ShinobuExosuitFrameInput, 1, SystemID.GameplayPlayer, NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.GetBufferHandle<ExosuitTuningDTO>(BufferID.ShinobuExosuitTuning, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _terrainHandle = vault.GetBufferHandle<MockTerrainSDF>(BufferID.ShinobuExosuitMockTerrainSdf, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _flowHandle = vault.GetBufferHandle<MockFlowField>(BufferID.ShinobuExosuitMockFlowField, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _crushDepthHandle = vault.GetBufferHandle<MockCrushDepthSignal>(BufferID.ShinobuExosuitMockCrushDepth, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _outputHandle = vault.GetBufferHandle<ExosuitSolverOutput>(BufferID.ShinobuExosuitSolverOutput, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _screenHandle = vault.GetBufferHandle<ExoScreenDTO>(BufferID.ShinobuExosuitScreenDto, 1, SystemID.GameplayPlayer, NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.GetBufferHandle<ExosuitTelemetryEntry>(BufferID.ShinobuExosuitTelemetryRing, TelemetryCapacity, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = vault.GetBufferHandle<int>(BufferID.ShinobuExosuitTelemetryCursor, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _footstepAccumulatorHandle = vault.GetBufferHandle<float>(BufferID.ShinobuExosuitFootstepAccumulator, 1, SystemID.Physics, NativeArrayOptions.UninitializedMemory);
            _hapticHandle = vault.GetBufferHandle<MechHapticSignalDTO>(BufferID.ShinobuExosuitHapticSignals, 1, SystemID.GameplayPlayer, NativeArrayOptions.UninitializedMemory);
            _siltHandle = vault.GetBufferHandle<SiltExplosionSignal>(BufferID.ShinobuExosuitSiltSignals, 1, SystemID.GameplayPlayer, NativeArrayOptions.UninitializedMemory);
            _acousticHandle = vault.GetBufferHandle<AcousticEchoTap>(BufferID.ShinobuExosuitAcousticTaps, 1, SystemID.GameplayPlayer, NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = vault.GetBufferHandle<byte>(BufferID.ShinobuExosuitCsvScratch, CsvScratchCapacity, SystemID.GameplayPlayer, NativeArrayOptions.UninitializedMemory);
        }

        private void GenerateEmergencyMockExoData()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            double3 originAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            Vector3 runtimePosition = _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
            double3 aup = originAup + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);

            ref ExosuitStateDTO state = ref _stateHandle.GetElementAsRef(vault, 0);
            if ((state.StateMask & ExosuitStateFlags.Active) == 0u || !math.all(math.isfinite(state.AUP)))
            {
                state = default;
                state.AUP = aup;
                state.Velocity = float3.zero;
                state.HydraulicPressure = 0.0f;
                state.AnchorNormal = new float3(0.0f, 1.0f, 0.0f);
                state.StateMask = ExosuitStateFlags.Active | ExosuitStateFlags.EmergencyMockData;
            }

            ref ExosuitTuningDTO tuning = ref _tuningHandle.GetElementAsRef(vault, 0);
            if (tuning.BaseMass <= 0.0f || !math.isfinite(tuning.BaseMass))
            {
                tuning = BuildSerializedTuning();
                tuning.Flags = ExosuitStateFlags.EmergencyMockData;
            }

            _inputHandle.GetElementAsRef(vault, 0) = default;
            _terrainHandle.GetElementAsRef(vault, 0) = BuildTerrain(originAup);
            _flowHandle.GetElementAsRef(vault, 0) = BuildFlow();
            _crushDepthHandle.GetElementAsRef(vault, 0) = BuildCrushDepth(0u, tuning.CrushDepthMeters);
            _outputHandle.GetElementAsRef(vault, 0) = default;
            _screenHandle.GetElementAsRef(vault, 0) = default;
            _hapticHandle.GetElementAsRef(vault, 0) = default;
            _siltHandle.GetElementAsRef(vault, 0) = default;
            _acousticHandle.GetElementAsRef(vault, 0) = default;
            _telemetryCursorHandle.GetElementAsRef(vault, 0) = 0;
            _footstepAccumulatorHandle.GetElementAsRef(vault, 0) = 0.0f;
            _scheduledFrame = 0u;
        }

        private uint WriteFrameInputs()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return _scheduledFrame;

            ExosuitTuningDTO tuning = SanitizeManagedTuning(_tuningHandle.GetElementAsRef(vault, 0));
            tuning.StateHash = ComputeManagedHash(tuning.BaseMass, tuning.ThrusterForce, tuning.ClampRange, tuning.GlobalQualityWeight);
            _tuningHandle.GetElementAsRef(vault, 0) = tuning;

            uint frame = _screenHandle.GetElementAsRef(vault, 0).Frame + 1u;
            if (frame == 0u)
                frame = 1u;
            _scheduledFrame = frame;

            uint actionMask = 0u;
            if (_mockGrab)
                actionMask |= ExosuitInputActions.Grab;
            if (_mockPurge)
                actionMask |= ExosuitInputActions.Purge;
            if (_mockJump)
                actionMask |= ExosuitInputActions.Jump;

            MockInputBuffer input = default;
            input.MoveAxis = new float2(_mockMoveAxis.x, _mockMoveAxis.y);
            input.VerticalAxis = _mockVerticalAxis;
            input.DesiredYawRadians = _mockYawRadians;
            input.ActionMask = actionMask;
            input.Frame = frame;
            input.GlobalQualityWeight = math.saturate(tuning.GlobalQualityWeight);
            _inputHandle.GetElementAsRef(vault, 0) = input;

            double3 originAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            _terrainHandle.GetElementAsRef(vault, 0) = BuildTerrain(originAup);
            _flowHandle.GetElementAsRef(vault, 0) = BuildFlow();
            _crushDepthHandle.GetElementAsRef(vault, 0) = BuildCrushDepth(frame, tuning.CrushDepthMeters);
            return frame;
        }

        private void ScheduleSolver(float deltaTime, uint frame)
        {
            if (!TryLockJobBuffers())
                return;

            if (!ResolveJobBuffers(
                    out NativeArray<ExosuitStateDTO> state,
                    out NativeArray<MockInputBuffer> input,
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
                    out NativeArray<AcousticEchoTap> acoustic))
            {
                UnlockJobBuffers();
                return;
            }

            Exosuit6DIntegratorJob job = new Exosuit6DIntegratorJob
            {
                State = state,
                Input = input,
                Tuning = tuning,
                Terrain = terrain,
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
                Frame = frame
            };

            if (!job.TryScheduleAdmitted(JobAdmissionLane.Lane0_Critical, default, out _jobHandle))
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
            _jobHandle.Complete();
            _jobScheduled = false;
            float elapsedMs = ResolveElapsedJobMs();
            JobAdmissionScheduleExtensions.ReportAdmittedJobCompleted<Exosuit6DIntegratorJob>(JobAdmissionLane.Lane0_Critical, elapsedMs);
            PatchLastTelemetryElapsed(elapsedMs);
            EmitReadbackSignals();
            UnlockJobBuffers();
            _jobHandle = default;
        }

        private float ResolveElapsedJobMs()
        {
            long delta = Stopwatch.GetTimestamp() - _jobStartTimestamp;
            if (delta <= 0L)
                return 0.0f;

            return (float)(delta * 1000.0 / Stopwatch.Frequency);
        }

        private void PatchLastTelemetryElapsed(float elapsedMs)
        {
            NativeArray<ExosuitTelemetryEntry> telemetry = _telemetryHandle.Resolve(_dataVault);
            NativeArray<int> cursorBuffer = _telemetryCursorHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated || telemetry.Length <= 0 || !cursorBuffer.IsCreated || cursorBuffer.Length <= 0)
                return;

            int index = cursorBuffer[0] - 1;
            if (index < 0)
                index = telemetry.Length - 1;
            if ((uint)index >= (uint)telemetry.Length)
                return;

            ExosuitTelemetryEntry entry = telemetry[index];
            entry.SolverComputeTimeMs = elapsedMs;
            telemetry[index] = entry;
        }

        private void EmitReadbackSignals()
        {
            NativeArray<ExosuitSolverOutput> outputBuffer = _outputHandle.Resolve(_dataVault);
            NativeArray<ExosuitStateDTO> stateBuffer = _stateHandle.Resolve(_dataVault);
            if (!outputBuffer.IsCreated || outputBuffer.Length <= 0 || !stateBuffer.IsCreated || stateBuffer.Length <= 0)
                return;

            ExosuitSolverOutput output = outputBuffer[0];
            ExosuitStateDTO state = stateBuffer[0];
            if (_driveTransformFromSolver && _cachedTransform != null)
                _cachedTransform.position = new Vector3(output.LocalPosition.x, output.LocalPosition.y, output.LocalPosition.z);

            if ((output.Flags & ExosuitSolverOutput.FlagHaptic) != 0u)
                EmitHaptics(output.Frame);
            if ((output.Flags & ExosuitSolverOutput.FlagSilt) != 0u)
                EmitSilt();
            if ((output.Flags & ExosuitSolverOutput.FlagAcousticTap) != 0u)
                EmitAcoustic();
            if ((output.Flags & ExosuitSolverOutput.FlagFault) != 0u ||
                (state.StateMask & ExosuitStateFlags.NaNDetected) != 0u)
            {
                DumpTelemetryBuffer();
            }
        }

        private void EmitHaptics(uint frame)
        {
            NativeArray<MechHapticSignalDTO> haptics = _hapticHandle.Resolve(_dataVault);
            if (!haptics.IsCreated || haptics.Length <= 0)
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
            NativeArray<SiltExplosionSignal> siltBuffer = _siltHandle.Resolve(_dataVault);
            if (siltBuffer.IsCreated && siltBuffer.Length > 0 && siltBuffer[0].Intensity01 > 0.0f)
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
            NativeArray<AcousticEchoTap> acousticBuffer = _acousticHandle.Resolve(_dataVault);
            if (acousticBuffer.IsCreated && acousticBuffer.Length > 0 && acousticBuffer[0].Intensity01 > 0.0f)
            {
                AcousticEchoTap tap = acousticBuffer[0];
                if (!math.all(math.isfinite(tap.AUP)))
                    return;
                tap.Intensity01 = math.isfinite(tap.Intensity01) ? math.saturate(tap.Intensity01) : 0.0f;
                if (tap.Intensity01 <= 0.0f)
                    return;
                SignalBus<AcousticEchoTap>.Push(in tap);
            }
        }

        private bool ResolveJobBuffers(
            out NativeArray<ExosuitStateDTO> state,
            out NativeArray<MockInputBuffer> input,
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
            out NativeArray<AcousticEchoTap> acoustic)
        {
            state = _stateHandle.Resolve(_dataVault);
            input = _inputHandle.Resolve(_dataVault);
            tuning = _tuningHandle.Resolve(_dataVault);
            terrain = _terrainHandle.Resolve(_dataVault);
            flow = _flowHandle.Resolve(_dataVault);
            crushDepth = _crushDepthHandle.Resolve(_dataVault);
            output = _outputHandle.Resolve(_dataVault);
            screen = _screenHandle.Resolve(_dataVault);
            telemetry = _telemetryHandle.Resolve(_dataVault);
            telemetryCursor = _telemetryCursorHandle.Resolve(_dataVault);
            footstepAccumulator = _footstepAccumulatorHandle.Resolve(_dataVault);
            haptics = _hapticHandle.Resolve(_dataVault);
            silt = _siltHandle.Resolve(_dataVault);
            acoustic = _acousticHandle.Resolve(_dataVault);

            return state.IsCreated && state.Length > 0 &&
                   input.IsCreated && input.Length > 0 &&
                   tuning.IsCreated && tuning.Length > 0 &&
                   terrain.IsCreated && terrain.Length > 0 &&
                   flow.IsCreated && flow.Length > 0 &&
                   crushDepth.IsCreated && crushDepth.Length > 0 &&
                   output.IsCreated && output.Length > 0 &&
                   screen.IsCreated && screen.Length > 0 &&
                   telemetry.IsCreated && telemetry.Length >= TelemetryCapacity &&
                   telemetryCursor.IsCreated && telemetryCursor.Length > 0 &&
                   footstepAccumulator.IsCreated && footstepAccumulator.Length > 0 &&
                   haptics.IsCreated && haptics.Length > 0 &&
                   silt.IsCreated && silt.Length > 0 &&
                   acoustic.IsCreated && acoustic.Length > 0;
        }

        private bool TryLockJobBuffers()
        {
            if (_jobBuffersLocked)
                return true;
            if (_dataVault == null)
                return false;

            int locked = 0;
            for (int i = 0; i < JobBufferLockCount; i++)
            {
                if (!_dataVault.TryLockBuffer(ResolveJobBufferId(i), SystemID.Physics))
                {
                    UnlockJobBuffers(locked);
                    return false;
                }

                locked++;
            }

            _jobBuffersLocked = true;
            return true;
        }

        private void UnlockJobBuffers()
        {
            if (!_jobBuffersLocked)
                return;

            UnlockJobBuffers(JobBufferLockCount);
            _jobBuffersLocked = false;
        }

        private void UnlockJobBuffers(int lockedCount)
        {
            if (_dataVault == null)
                return;

            for (int i = lockedCount - 1; i >= 0; i--)
                _dataVault.TryUnlockBuffer(ResolveJobBufferId(i), SystemID.Physics);
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

        private void TryApplyCsvOverrides(float deltaTime)
        {
            _csvPollCountdown -= math.max(0.0f, deltaTime);
            if (_csvPollCountdown > 0.0f || string.IsNullOrEmpty(_csvPath))
                return;

            _csvPollCountdown = DefaultCsvPollIntervalSeconds;
            if (!File.Exists(_csvPath))
                return;

            long writeTicks = File.GetLastWriteTimeUtc(_csvPath).Ticks;
            if (writeTicks == _lastCsvWriteTicks)
                return;

            NativeArray<byte> scratch = _csvScratchHandle.Resolve(_dataVault);
            if (!scratch.IsCreated || scratch.Length <= 0)
                return;

            int count = ReadCsvBytes(_csvPath, scratch);
            if (count <= 0)
            {
                _lastCsvWriteTicks = writeTicks;
                return;
            }

            ExosuitTuningDTO tuning = _tuningHandle.GetElementAsRef(_dataVault, 0);
            if (ParseCsvIntoTuning(scratch, count, ref tuning))
            {
                tuning.CsvVersion++;
                _tuningHandle.GetElementAsRef(_dataVault, 0) = SanitizeManagedTuning(tuning);
            }

            _lastCsvWriteTicks = writeTicks;
        }

        private static int ReadCsvBytes(string path, NativeArray<byte> scratch)
        {
            int count = 0;
            using (FileStream stream = File.OpenRead(path))
            {
                int next;
                while (count < scratch.Length && (next = stream.ReadByte()) >= 0)
                    scratch[count++] = (byte)next;

                if (count >= scratch.Length && stream.Position < stream.Length)
                    return -count;
            }

            return count;
        }

        private static bool ParseCsvIntoTuning(NativeArray<byte> scratch, int count, ref ExosuitTuningDTO tuning)
        {
            bool changed = false;
            int index = 0;
            while (index < count)
            {
                SkipSeparators(scratch, count, ref index);
                uint hash = 2166136261u;
                bool hasKey = false;
                while (index < count)
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

                while (index < count && scratch[index] != (byte)',' && scratch[index] != (byte)'=')
                    index++;
                if (index < count)
                    index++;

                if (!TryParseFloat(scratch, count, ref index, out float value))
                    continue;

                changed |= ApplyCsvValue(hash, value, ref tuning);
                while (index < count && scratch[index] != (byte)'\n')
                    index++;
            }

            return changed;
        }

        private static void SkipSeparators(NativeArray<byte> scratch, int count, ref int index)
        {
            while (index < count)
            {
                byte b = scratch[index];
                if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\n' && b != (byte)'\r')
                    return;
                index++;
            }
        }

        private static bool TryParseFloat(NativeArray<byte> scratch, int count, ref int index, out float value)
        {
            value = 0.0f;
            while (index < count && (scratch[index] == (byte)' ' || scratch[index] == (byte)'\t'))
                index++;

            float sign = 1.0f;
            if (index < count && scratch[index] == (byte)'-')
            {
                sign = -1.0f;
                index++;
            }

            double integral = 0.0;
            bool hasDigit = false;
            while (index < count && scratch[index] >= (byte)'0' && scratch[index] <= (byte)'9')
            {
                integral = integral * 10.0 + scratch[index] - (byte)'0';
                hasDigit = true;
                index++;
            }

            double fraction = 0.0;
            double scale = 1.0;
            if (index < count && scratch[index] == (byte)'.')
            {
                index++;
                while (index < count && scratch[index] >= (byte)'0' && scratch[index] <= (byte)'9')
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
            if (index < count && (scratch[index] == (byte)'e' || scratch[index] == (byte)'E'))
            {
                int exponentStart = index;
                index++;
                int exponentSign = 1;
                if (index < count && scratch[index] == (byte)'-')
                {
                    exponentSign = -1;
                    index++;
                }
                else if (index < count && scratch[index] == (byte)'+')
                {
                    index++;
                }

                int exponent = 0;
                bool hasExponentDigit = false;
                while (index < count && scratch[index] >= (byte)'0' && scratch[index] <= (byte)'9')
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
                default:
                    return false;
            }
        }

        private void DumpTelemetryBuffer()
        {
            uint frame = _scheduledFrame;
            if (_lastDumpFrame == frame)
                return;
            _lastDumpFrame = frame;

            NativeArray<ExosuitTelemetryEntry> telemetry = _telemetryHandle.Resolve(_dataVault);
            NativeArray<int> cursorBuffer = _telemetryCursorHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            string directory = Path.Combine(_projectRoot, "Docs", "AgentLogs");
            Directory.CreateDirectory(directory);
            WriteTelemetryDump(Path.Combine(directory, "Dump_EXO_KINEMATICS.bin"), telemetry, cursorBuffer);
            WriteTelemetryDump(Path.Combine(directory, "Dump_SHINOBU_47.bin"), telemetry, cursorBuffer);
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

            SignalBus<MechHapticSignalDTO>.Configure(8, maxFrameSignals: 16, lowTierFrameSignals: 4, laneHash: 0x4D484558u);
            SignalBus<SiltExplosionSignal>.Configure(4, maxFrameSignals: 8, lowTierFrameSignals: 2, laneHash: 0x4558494Cu);
            SignalBus<AcousticEchoTap>.Configure(8, maxFrameSignals: 16, lowTierFrameSignals: 4, laneHash: 0x45584F41u);
            SignalBus<HapticRequest>.EnsureInitialized();
            _signalLanesReady = true;
        }

        private ExosuitTuningDTO BuildSerializedTuning()
        {
            ExosuitTuningDTO tuning = default;
            tuning.BaseMass = math.max(1f, _baseMassKg);
            tuning.CurrentMass = tuning.BaseMass;
            tuning.Drag = math.clamp(_drag, 0f, 8f);
            tuning.ThrusterForce = math.max(0f, _thrusterForceNewtons);
            tuning.Radius = math.clamp(_radiusMeters, 0.25f, 5f);
            tuning.ClampRange = math.max(tuning.Radius, _magneticClampRangeMeters);
            tuning.HydraulicLatencySeconds = math.clamp(_hydraulicLatencySeconds, 0.05f, 3f);
            tuning.PurgeImpulse = math.max(0f, _purgeImpulseMetersPerSecond);
            tuning.GlobalQualityWeight = math.saturate(_globalQualityWeight);
            tuning.FootstepStrideMeters = math.max(0.25f, _footstepStrideMeters);
            tuning.MaxSpeedMetersPerSecond = math.max(0.25f, _maxSpeedMetersPerSecond);
            tuning.CrushDepthMeters = math.max(1f, _crushDepthMeters);
            tuning.StateHash = ComputeManagedHash(tuning.BaseMass, tuning.ThrusterForce, tuning.ClampRange, tuning.GlobalQualityWeight);
            return tuning;
        }

        private MockTerrainSDF BuildTerrain(double3 cameraAup)
        {
            MockTerrainSDF terrain = default;
            terrain.CameraAup = cameraAup;
            terrain.CaveRadius = math.max(1f, _caveRadiusMeters);
            terrain.FloorY = math.min(_caveFloorY, _caveCeilingY - 2f);
            terrain.CeilingY = math.max(_caveCeilingY, terrain.FloorY + 2f);
            terrain.WallSoftnessMeters = 0.15f;
            terrain.CaveCenterLocal = new float3(_caveCenterLocal.x, _caveCenterLocal.y, _caveCenterLocal.z);
            return terrain;
        }

        private MockFlowField BuildFlow()
        {
            MockFlowField flow = default;
            flow.FlowVelocity = new float3(_mockFlowVelocity.x, _mockFlowVelocity.y, _mockFlowVelocity.z);
            flow.Intensity01 = math.saturate(_mockFlowIntensity01);
            return flow;
        }

        private MockCrushDepthSignal BuildCrushDepth(uint frame, float crushDepthMeters)
        {
            MockCrushDepthSignal crush = default;
            crush.DepthMeters = math.max(0f, _mockDepthMeters);
            crush.ExternalPressure01 = math.saturate(crush.DepthMeters * math.rcp(math.max(1f, crushDepthMeters)));
            crush.Frame = frame;
            return crush;
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
            tuning.GlobalQualityWeight = math.saturate(math.isfinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : 0.62f);
            tuning.FootstepStrideMeters = math.max(0.25f, math.isfinite(tuning.FootstepStrideMeters) ? tuning.FootstepStrideMeters : 3f);
            tuning.MaxSpeedMetersPerSecond = math.max(0.25f, math.isfinite(tuning.MaxSpeedMetersPerSecond) ? tuning.MaxSpeedMetersPerSecond : 9f);
            tuning.CrushDepthMeters = math.max(1f, math.isfinite(tuning.CrushDepthMeters) ? tuning.CrushDepthMeters : 4000f);
            return tuning;
        }

        private static uint ComputeManagedHash(float a, float b, float c, float d)
        {
            uint hash = 2166136261u;
            hash = (hash ^ math.asuint(a)) * 16777619u;
            hash = (hash ^ math.asuint(b)) * 16777619u;
            hash = (hash ^ math.asuint(c)) * 16777619u;
            hash = (hash ^ math.asuint(d)) * 16777619u;
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
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(center, math.max(0.25f, tuning.Radius));
            Gizmos.color = Color.red;
            Gizmos.DrawLine(center, center + new Vector3(output.PushNormal.x, output.PushNormal.y, output.PushNormal.z) * math.max(0.5f, output.PushOutMagnitude * 4f));
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(center, center + new Vector3(output.DesiredVelocity.x, output.DesiredVelocity.y, output.DesiredVelocity.z));
        }
#endif
    }
}
