using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using TMPro;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.UI.Navigation
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CompassBlackBoxEntry
    {
        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public float ActualHeadingDegrees;
        [FieldOffset(8)]
        public float CurrentHeadingDegrees;
        [FieldOffset(12)]
        public float DriftDegrees;
        [FieldOffset(16)]
        public float MaxGyroDriftDegrees;
        [FieldOffset(20)]
        public float AnomalyInterference01;
        [FieldOffset(24)]
        public float Power01;
        [FieldOffset(28)]
        public uint Flags;
        [FieldOffset(32)]
        public uint LastAupShiftFrameId;
        [FieldOffset(36)]
        public int CalibrationCount;
        [FieldOffset(40)]
        public ulong Padding0;
        [FieldOffset(48)]
        public ulong Padding1;
        [FieldOffset(56)]
        public ulong Padding2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct CompassPresentationStateDTO
    {
        [FieldOffset(0)]
        public float LastPresentedHeadingDegrees;
        [FieldOffset(4)]
        public float LastCompassGlassChromatic01;
        [FieldOffset(8)]
        public float LastCompassPower01;
        [FieldOffset(12)]
        public float LastCompassOverkill01;
        [FieldOffset(16)]
        public float ParticleDebt;
        [FieldOffset(20)]
        public float LastUploadedDialHeadingDegrees;
        [FieldOffset(24)]
        public float3 LastUploadedDialPosition;
        [FieldOffset(36)]
        public float4 LastUploadedDialRotation;
        [FieldOffset(52)]
        public float3 LastUploadedDialScale;
        [FieldOffset(64)]
        public int LastCardinalIndex;
        [FieldOffset(68)]
        public int LastPowerState;
        [FieldOffset(72)]
        public int DialMatrixWriteIndex;
        [FieldOffset(76)]
        public uint PresentationFlags;
    }

    public static class DiegeticCompassSignals
    {
        private const uint CompassCalibrationLaneHash = 0xC06A5511u;
        private const uint CompassAnomalyLaneHash = 0xC06A5512u;

        public static void ConfigureOwnedLanes()
        {
            SignalBus<AnomalyProximitySignal>.Configure(8, 16, 4, CompassAnomalyLaneHash);
            SignalBus<AnomalyProximitySignal>.EnsureInitialized();
            SignalBus<CompassCalibratedSignal>.Configure(4, 8, 2, CompassCalibrationLaneHash);
            SignalBus<CompassCalibratedSignal>.EnsureInitialized();
        }

        /// <summary>
        /// Publishes a finite compass recalibration command on the owned typed lane.
        /// </summary>
        /// <param name="frame">Source frame id for duplicate rejection.</param>
        /// <param name="quality01">Calibration quality scalar. Non-finite values publish as zero.</param>
        public static void PublishCalibration(uint frame, float quality01)
        {
            ConfigureOwnedLanes();
            CompassCalibratedSignal signal = new CompassCalibratedSignal
            {
                SourceHash = CompassCalibrationLaneHash,
                Frame = frame,
                CalibrationQuality01 = SanitizeUnit01(quality01),
                Flags = 1
            };
            SignalBus<CompassCalibratedSignal>.Push(in signal);
        }

        /// <summary>
        /// Publishes finite anomaly proximity data for compass drift falsification.
        /// </summary>
        /// <param name="sourceAup">Anomaly source AUP. Non-finite local offsets are zeroed.</param>
        /// <param name="frame">Source frame id for duplicate rejection.</param>
        /// <param name="proximity01">Normalized proximity scalar.</param>
        /// <param name="interference01">Normalized interference scalar.</param>
        public static void PublishAnomalyProximity(in AbsoluteUniversePosition sourceAup, uint frame, float proximity01, float interference01)
        {
            ConfigureOwnedLanes();
            AbsoluteUniversePosition safeAup = SanitizeAup(in sourceAup);
            AnomalyProximitySignal signal = new AnomalyProximitySignal
            {
                SourceAup = safeAup,
                Proximity01 = SanitizeUnit01(proximity01),
                Interference01 = SanitizeUnit01(interference01),
                SourceHash = CompassAnomalyLaneHash,
                Frame = frame,
                Flags = 1
            };
            SignalBus<AnomalyProximitySignal>.Push(in signal);
        }

        private static AbsoluteUniversePosition SanitizeAup(in AbsoluteUniversePosition sourceAup)
        {
            AbsoluteUniversePosition safeAup = sourceAup;
            if (!math.isfinite(safeAup.LocalX))
                safeAup.LocalX = 0f;
            if (!math.isfinite(safeAup.LocalY))
                safeAup.LocalY = 0f;
            if (!math.isfinite(safeAup.LocalZ))
                safeAup.LocalZ = 0f;

            return safeAup;
        }

        private static float SanitizeUnit01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Navigation/Diegetic Gyro Compass Runtime")]
    public sealed class DiegeticGyroCompassRuntime : MonoBehaviour, IInertialNavigationService, IFastTickable, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IScalabilityChangedEventListener
    {
        private const int StateLength = 1;
        private const int BlackBoxCapacity = 300;
        private const SystemID OwnerSystem = SystemID.UI;
        private const float DefaultSlowDeltaSeconds = 0.1f;
        private const float MaxIntegrationDeltaSeconds = 0.2f;
        private const float PowerDeathThreshold01 = 0.01f;
        private const float StressSlowThreshold01 = 0.8f;
        private const float RecalibrationHoldSeconds = 3f;
        private const float HeadingEpsilon = 0.001f;
        private const float ChromaticEpsilon = 0.001f;
        private const float VelocityClampMetersPerSecond = 100000f;
        private const int MaxAnomalyParticleBurst = 128;
        private const int DialMatrixStrideBytes = 64;
        private const float DialPositionUploadEpsilon = 0.000001f;
        private const float DialRotationUploadEpsilon = 0.0001f;
        private const float DialScaleUploadEpsilon = 0.000001f;
        private const uint DumpMagic = 0x4759434Fu;
        private const string DumpFileName = "Dump_COMPASS_GYRO_STABILIZER.bin";
        private const uint FlagInitialized = 1u << 0;
        private const uint FlagPowered = 1u << 1;
        private const uint FlagAnomalyUnstable = 1u << 2;
        private const uint FlagStressSlowCadence = 1u << 3;
        private const uint FlagCalibrationApplied = 1u << 4;
        private const uint FlagNonFiniteFallback = 1u << 5;
        private const uint FlagReducedQualityNoise = 1u << 6;
        private const uint FlagIndirectDial = 1u << 7;
        private const uint FlagHasPreviousAup = 1u << 8;
        private const uint FlagCalibrationRequested = 1u << 9;
        private const uint PresentationFlagTextInitialized = 1u << 0;
        private const uint PresentationFlagDialInitialized = 1u << 1;
        private const uint PresentationFlagShaderInitialized = 1u << 2;
        private const uint PresentationFlagDialMatrixInitialized = 1u << 3;
        private const uint PresentationFlagDialMatrixDirty = 1u << 4;

        private static readonly int _CompassDialMatricesId = Shader.PropertyToID("_CompassDialMatrices");
        private static readonly int _CompassGlassChromaticId = Shader.PropertyToID("_CompassGlassChromatic");
        private static readonly int _CompassPowerId = Shader.PropertyToID("_CompassPower01");
        private static readonly int _CompassOverkillId = Shader.PropertyToID("_CompassOverkill01");

        [Header("Physical Tool Binding")]
        [SerializeField, Tooltip("Physical hand tool or cockpit instrument root. Presentation is skipped when unbound.")]
        private Transform toolRoot;

        [SerializeField, Tooltip("Physical compass dial pivot rotated by the drifted bearing.")]
        private Transform dialPivot;

        [SerializeField, Tooltip("Diegetic TMP label. TextMeshProUGUI is accepted only when its Canvas is World Space.")]
        private TMP_Text cardinalText;

        [SerializeField, Tooltip("Authored local dial offset in degrees.")]
        private float dialDegreesOffset;

        [Header("Indirect Dial")]
        [SerializeField, Tooltip("Allows High/Ultra tiers to draw the physical dial mesh through indirect instancing.")]
        private bool enableIndirectHighTier = true;

        [SerializeField, Tooltip("Dial mesh used by the High/Ultra indirect draw path.")]
        private Mesh dialMesh;

        [SerializeField, Tooltip("Instanced dial material that consumes _CompassDialMatrices.")]
        private Material dialIndirectMaterial;

        [SerializeField, Tooltip("Local indirect draw bounds centered on the physical dial source transform.")]
        private Bounds indirectDrawBounds = new Bounds(Vector3.zero, new Vector3(0.35f, 0.35f, 0.35f));

        [Header("Drift")]
        [SerializeField, Min(0f), Tooltip("Exponential catch-up rate from false bearing toward AUP north.")]
        private float headingCatchupRate = 3f;

        [SerializeField, Min(0f), Tooltip("Noise frequency for drift and anomaly wobble.")]
        private float driftNoiseFrequency = 0.17f;

        [SerializeField, Min(0f), Tooltip("Maximum anomaly-driven gyro noise in degrees.")]
        private float anomalyNoiseDegrees = 24f;

        [SerializeField, Min(0f), Tooltip("Anomaly failure spin rate when interference crosses the unstable threshold.")]
        private float wildSpinDegreesPerSecond = 720f;

        [Header("High Tier Failure VFX")]
        [SerializeField, Tooltip("Optional local particle emitter for salt/static bursts around the physical compass glass. High/Ultra only.")]
        private ParticleSystem anomalyFailureParticles;

        [SerializeField, Min(0), Tooltip("Maximum particles emitted per LateFrameTick while anomaly interference is saturated on High/Ultra tiers. Code clamps to 128.")]
        private int anomalyParticleBurst = 64;

        private IDataVault _vault;
        private IPlayerRuntimeContext _playerContext;
        private JobHandle _jobHandle;
        private bool _jobPending;
        private bool _registeredFastTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _registeredService;
        private bool _hotSwapListenerRegistered;
        private bool _scalabilityListenerRegistered;
        private bool _diegeticTextValid = true;
        private bool _blackBoxDumped;
        private float _qualityWeight01 = 1f;
        private float _visualOverkillWeight01 = 1f;
        private float _fastCadenceAccumulatedDelta;
        private int _fastCadenceStride = 1;
        private int _fastCadenceCounter;
        private VaultLane<CompassStateDTO> _stateLane;
        private VaultLane<CompassPresentationStateDTO> _presentationLane;
        private VaultLane<float> _headingOutputLane;
        private VaultLane<CompassBlackBoxEntry> _blackBoxLane;

        private readonly char[] _cardinalBuffer = new char[2]; // COLD ALLOC: char[2] - diegetic compass cardinal text buffer - owner: DiegeticGyroCompassRuntime
        private readonly uint[] _indirectArgs = new uint[5]; // COLD ALLOC: uint[5] - compass indirect draw args - owner: DiegeticGyroCompassRuntime
        private GraphicsBuffer _indirectArgsBuffer;
        private GraphicsBuffer _dialMatrixBufferA;
        private GraphicsBuffer _dialMatrixBufferB;
        private GraphicsBuffer _publishedDialMatrixBuffer;
        private GraphicsBuffer _boundDialMatrixBuffer;

        /// <inheritdoc />
        public InertialNavigationSnapshot Snapshot => TryGetSnapshot(out InertialNavigationSnapshot snapshot) ? snapshot : default;

        /// <inheritdoc />
        public double3 EstimatedAUP => TryGetSnapshot(out InertialNavigationSnapshot snapshot) ? snapshot.EstimatedAUP : double3.zero;

        /// <inheritdoc />
        public float GyroDriftError => TryGetSnapshot(out InertialNavigationSnapshot snapshot) ? snapshot.GyroDriftError : 0f;

        private struct VaultLane<T> where T : struct
        {
            public VaultGenerationHandle<T> Handle;
            public uint ExpectedBufferID;
            public int Length;
        }

        private void Awake()
        {
            ValidateDiegeticTextBinding();
        }

        private void OnEnable()
        {
            ConfigureSignalLanes();
            TryRegisterHotSwapListener();
            TryRegisterScalabilityListener();
            TryRegisterService();
            TryRegisterTickables();
        }

        private void Start()
        {
            ResolveColdDependencies();
            TryResolveVaultBuffers();
            TryRegisterService();
            TryRegisterTickables();
            EnsureIndirectBuffers();
        }

        private void OnDisable()
        {
            CompletePendingJob(forceComplete: true);
            TryUnregisterTickables();
            TryUnregisterService();
            TryUnregisterScalabilityListener();
            TryUnregisterHotSwapListener();
            ClearCompassShaderGlobals();
        }

        private void OnDestroy()
        {
            CompletePendingJob(forceComplete: true);
            ReleaseIndirectBuffers();
        }

        /// <inheritdoc />
        public bool TryGetSnapshot(out InertialNavigationSnapshot snapshot)
        {
            if (TryReadCompassState(out CompassStateDTO state))
            {
                snapshot = BuildSnapshot(in state);
                return (state.Flags & FlagInitialized) != 0u;
            }

            snapshot = default;
            return false;
        }

        /// <inheritdoc />
        public void RequestRecalibration()
        {
            if (!TryGetCompassBuffers(out var stateBuffer, out _, out _))
                return;

            CompassStateDTO state = stateBuffer[0];
            state.Flags |= FlagCalibrationRequested;
            state.RecalibrationHold01 = 1f;
            stateBuffer[0] = state;
        }

        /// <inheritdoc />
        public bool TryAccumulateRecalibrationHold(float deltaTime, out float progress01)
        {
            progress01 = 0f;
            if (!TryGetCompassBuffers(out var stateBuffer, out _, out _))
                return false;

            float safeDeltaTime = SanitizeDeltaTime(deltaTime);
            CompassStateDTO state = stateBuffer[0];
            SanitizeCompassStateScalars(ref state);
            state.RecalibrationHold01 = math.saturate(state.RecalibrationHold01 + safeDeltaTime * math.rcp(RecalibrationHoldSeconds));
            progress01 = state.RecalibrationHold01;
            if (state.RecalibrationHold01 >= 1f)
                state.Flags |= FlagCalibrationRequested;

            stateBuffer[0] = state;

            return true;
        }

        /// <inheritdoc />
        public void CancelRecalibrationHold()
        {
            if (!TryGetCompassBuffers(out var stateBuffer, out _, out _))
                return;

            CompassStateDTO state = stateBuffer[0];
            state.RecalibrationHold01 = 0f;
            stateBuffer[0] = state;
        }

        /// <summary>
        /// Caches bootstrap-owned dependencies for the compass hot path.
        /// </summary>
        /// <param name="playerContext">Authoritative player pose/AUP provider.</param>
        /// <param name="vault">Global vault owner for compass state, output, and blackbox buffers.</param>
        /// <param name="qualityWeight01">Boot-time continuous quality weight from HomeostasisBrain.</param>
        /// <remarks>Call from bootstrap or tool installation only; tick paths do not poll the registry.</remarks>
        public void InjectDependencies(IPlayerRuntimeContext playerContext, IDataVault vault, float qualityWeight01)
        {
            _playerContext = playerContext;
            _vault = vault;
            RefreshQualityPolicy(qualityWeight01);
            TryResolveVaultBuffers();
            EnsureIndirectBuffers();
        }

        /// <summary>
        /// Binds the runtime to a physical compass tool without relying on screen-space UI.
        /// </summary>
        /// <param name="nextToolRoot">Physical tool or cockpit instrument root.</param>
        /// <param name="nextDialPivot">Optional authored dial pivot.</param>
        /// <param name="nextCardinalText">Optional diegetic cardinal label.</param>
        /// <param name="nextDialMesh">Optional High/Ultra indirect dial mesh.</param>
        /// <param name="nextDialMaterial">Optional High/Ultra indirect dial material.</param>
        /// <remarks>Call from an authoring/bootstrap cold path. It releases and rebuilds GPU buffers when the mesh or material changes.</remarks>
        public void ConfigurePhysicalBinding(
            Transform nextToolRoot,
            Transform nextDialPivot,
            TMP_Text nextCardinalText,
            Mesh nextDialMesh,
            Material nextDialMaterial)
        {
            bool indirectBindingChanged = !ReferenceEquals(dialMesh, nextDialMesh) ||
                                          !ReferenceEquals(dialIndirectMaterial, nextDialMaterial);

            toolRoot = nextToolRoot;
            dialPivot = nextDialPivot;
            cardinalText = nextCardinalText;
            dialMesh = nextDialMesh;
            dialIndirectMaterial = nextDialMaterial;
            ResetPresentationState(resetDialMatrix: true);
            ValidateDiegeticTextBinding();

            if (indirectBindingChanged)
                ReleaseIndirectBuffers();

            EnsureIndirectBuffers();
        }

        /// <summary>
        /// Binds optional High/Ultra local failure VFX for the physical compass glass.
        /// </summary>
        /// <param name="nextAnomalyFailureParticles">Optional local anomaly particle emitter.</param>
        /// <param name="nextAnomalyParticleBurst">Authored burst budget. Runtime clamps to the internal safety cap.</param>
        /// <remarks>Call from a physical tool binding cold path; no gameplay authority depends on this emitter.</remarks>
        public void ConfigureFailureVfx(ParticleSystem nextAnomalyFailureParticles, int nextAnomalyParticleBurst)
        {
            anomalyFailureParticles = nextAnomalyFailureParticles;
            anomalyParticleBurst = math.clamp(nextAnomalyParticleBurst, 0, MaxAnomalyParticleBurst);
            ResetParticleDebt();
        }

        /// <inheritdoc />
        public void FastTick(float deltaTime)
        {
            float safeDeltaTime = SanitizeDeltaTime(deltaTime);
            _fastCadenceAccumulatedDelta = math.min(
                MaxIntegrationDeltaSeconds,
                _fastCadenceAccumulatedDelta + safeDeltaTime);

            if (!RefreshFastSignalInputs(out CompassStateDTO state))
                return;

            if (!ShouldUseFastCadence(in state))
                return;

            if (!ConsumeFastCadenceGate())
                return;

            float scheduledDelta = _fastCadenceAccumulatedDelta;
            _fastCadenceAccumulatedDelta = 0f;
            ScheduleDrift(scheduledDelta);
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (_playerContext == null || _vault == null)
                return;

            if (!RefreshFastSignalInputs(out CompassStateDTO state))
                return;

            if (ShouldUseFastCadence(in state))
                return;

            _fastCadenceAccumulatedDelta = 0f;
            ScheduleDrift(DefaultSlowDeltaSeconds);
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            CompletePendingJob(forceComplete: false);
            ApplyPresentation();
        }

        private void ConfigureSignalLanes()
        {
            DiegeticCompassSignals.ConfigureOwnedLanes();

            SignalBus<SurvivalVitalsChangedSignal>.EnsureInitialized();
            SignalBus<SystemHealthSignal>.EnsureInitialized();
            SignalBus<AupShiftSignal>.EnsureInitialized();
        }

        private void ResolveColdDependencies()
        {
            RefreshQualityPolicy();

            if (_playerContext == null)
                _playerContext = GlobalRegistry.Player;

            if (_vault == null)
                _vault = GlobalRegistry.DataVault;
        }

        private bool TryResolveVaultBuffers()
        {
            return TryGetCompassBuffers(out _, out _, out _);
        }

        private bool TryReadCompassState(out CompassStateDTO state)
        {
            state = default;
            if (!TryGetExistingStateBuffer(out var stateBuffer))
                return false;

            state = stateBuffer[0];
            if (SanitizeFiniteState(ref state))
                stateBuffer[0] = state;

            return true;
        }

        private bool TryGetExistingStateBuffer(out NativeSlice<CompassStateDTO> stateBuffer)
        {
            stateBuffer = default;
            if (!TryOpenExistingLane(
                    ref _stateLane,
                    BufferID.CompassState,
                    StateLength,
                    out NativeArray<CompassStateDTO> buffer))
                return false;

            stateBuffer = new NativeSlice<CompassStateDTO>(buffer);
            return true;
        }

        private bool TryGetExistingPresentationBuffer(out NativeSlice<CompassPresentationStateDTO> presentationBuffer)
        {
            presentationBuffer = default;
            if (!TryOpenExistingLane(
                    ref _presentationLane,
                    BufferID.CompassPresentationState,
                    StateLength,
                    out NativeArray<CompassPresentationStateDTO> buffer))
                return false;

            presentationBuffer = new NativeSlice<CompassPresentationStateDTO>(buffer);
            return true;
        }

        private bool TryGetPresentationBuffer(out NativeSlice<CompassPresentationStateDTO> presentationBuffer)
        {
            presentationBuffer = default;
            if (!TryOpenOrAcquireLane(
                    ref _presentationLane,
                    BufferID.CompassPresentationState,
                    StateLength,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<CompassPresentationStateDTO> buffer))
                return false;

            presentationBuffer = new NativeSlice<CompassPresentationStateDTO>(buffer);
            return true;
        }

        private void ResetPresentationState(bool resetDialMatrix)
        {
            if (!TryGetPresentationBuffer(out var presentationBuffer))
                return;

            CompassPresentationStateDTO presentation = presentationBuffer[0];
            presentation.LastPresentedHeadingDegrees = 0f;
            presentation.LastCompassGlassChromatic01 = 0f;
            presentation.LastCompassPower01 = 0f;
            presentation.LastCompassOverkill01 = 0f;
            presentation.ParticleDebt = 0f;
            presentation.LastCardinalIndex = 0;
            presentation.LastPowerState = 0;
            presentation.PresentationFlags &= ~(PresentationFlagTextInitialized |
                                                PresentationFlagDialInitialized |
                                                PresentationFlagShaderInitialized);

            if (resetDialMatrix)
                MarkDialMatrixPresentationDirty(ref presentation);

            presentationBuffer[0] = presentation;
        }

        private void ResetParticleDebt()
        {
            if (!TryGetExistingPresentationBuffer(out var presentationBuffer))
                return;

            CompassPresentationStateDTO presentation = presentationBuffer[0];
            presentation.ParticleDebt = 0f;
            presentationBuffer[0] = presentation;
        }

        private void MarkDialMatrixPresentationDirty()
        {
            if (!TryGetExistingPresentationBuffer(out var presentationBuffer))
                return;

            CompassPresentationStateDTO presentation = presentationBuffer[0];
            MarkDialMatrixPresentationDirty(ref presentation);
            presentationBuffer[0] = presentation;
        }

        private static void MarkDialMatrixPresentationDirty(ref CompassPresentationStateDTO presentation)
        {
            presentation.PresentationFlags &= ~PresentationFlagDialMatrixInitialized;
            presentation.PresentationFlags |= PresentationFlagDialMatrixDirty;
            presentation.DialMatrixWriteIndex = 0;
            presentation.LastUploadedDialHeadingDegrees = 0f;
            presentation.LastUploadedDialPosition = float3.zero;
            presentation.LastUploadedDialRotation = float4.zero;
            presentation.LastUploadedDialScale = float3.zero;
        }

        private void ClearCompassShaderGlobals()
        {
            Shader.SetGlobalFloat(_CompassGlassChromaticId, 0f);
            Shader.SetGlobalFloat(_CompassPowerId, 0f);
            Shader.SetGlobalFloat(_CompassOverkillId, 0f);

            if (!TryGetExistingPresentationBuffer(out var presentationBuffer))
                return;

            CompassPresentationStateDTO presentation = presentationBuffer[0];
            presentation.LastCompassGlassChromatic01 = 0f;
            presentation.LastCompassPower01 = 0f;
            presentation.LastCompassOverkill01 = 0f;
            presentation.PresentationFlags &= ~PresentationFlagShaderInitialized;
            presentationBuffer[0] = presentation;
        }

        private bool TryGetCompassBuffers(
            out NativeSlice<CompassStateDTO> stateBuffer,
            out NativeSlice<float> outputBuffer,
            out NativeSlice<CompassBlackBoxEntry> blackBox)
        {
            stateBuffer = default;
            outputBuffer = default;
            blackBox = default;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            if (!TryOpenOrAcquireLane(
                    ref _stateLane,
                    BufferID.CompassState,
                    StateLength,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<CompassStateDTO> state) ||
                !TryOpenOrAcquireLane(
                    ref _headingOutputLane,
                    BufferID.CompassHeadingOutput,
                    (int)CompassOutputSlot.Count,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<float> output) ||
                !TryOpenOrAcquireLane(
                    ref _blackBoxLane,
                    BufferID.CompassBlackBox,
                    BlackBoxCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<CompassBlackBoxEntry> telemetry))
            {
                return false;
            }

            stateBuffer = new NativeSlice<CompassStateDTO>(state);
            outputBuffer = new NativeSlice<float>(output);
            blackBox = new NativeSlice<CompassBlackBoxEntry>(telemetry);
            return true;
        }

        private bool TryOpenExistingLane<T>(
            ref VaultLane<T> lane,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _vault;
            if (vault == null || requiredLength <= 0)
                return false;

            if (OpenLane(vault, in lane, out buffer))
                return true;

            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existing))
                return false;

            lane = CreateLane(in existing, bufferId, requiredLength);
            return OpenLane(vault, in lane, out buffer);
        }

        private bool TryOpenOrAcquireLane<T>(
            ref VaultLane<T> lane,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryOpenExistingLane(ref lane, bufferId, requiredLength, out buffer))
                return true;

            IDataVault vault = _vault;
            if (vault == null || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            lane = AcquireLane<T>(vault, bufferId, requiredLength, options);
            return OpenLane(vault, in lane, out buffer);
        }

        private static VaultLane<T> AcquireLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null || requiredLength <= 0)
                return default;

            VaultGenerationHandle<T> handle = vault.GetGenerationHandle<T>(
                bufferId,
                requiredLength,
                OwnerSystem,
                options);
            return CreateLane(in handle, bufferId, requiredLength);
        }

        private static VaultLane<T> CreateLane<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            uint expectedBufferId = unchecked((uint)(int)bufferId);
            if (handle.BufferID != expectedBufferId || handle.Generation == 0u || requiredLength <= 0)
                return default;

            return new VaultLane<T>
            {
                Handle = handle,
                ExpectedBufferID = expectedBufferId,
                Length = requiredLength
            };
        }

        private static bool IsLaneBound<T>(in VaultLane<T> lane) where T : struct
        {
            return lane.ExpectedBufferID != 0u &&
                   lane.Handle.BufferID == lane.ExpectedBufferID &&
                   lane.Handle.Generation != 0u &&
                   lane.Length > 0;
        }

        private static bool OpenLane<T>(
            IDataVault vault,
            in VaultLane<T> lane,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || !IsLaneBound(in lane))
                return false;

            if (!vault.TryResolveHandle(in lane.Handle, out buffer))
                return false;

            return buffer.IsCreated && buffer.Length >= lane.Length;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    _playerContext = currentService as IPlayerRuntimeContext;
                    _fastCadenceAccumulatedDelta = 0f;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    _vault = currentService as IDataVault;
                    TryResolveVaultBuffers();
                    ResetPresentationState(resetDialMatrix: true);
                    break;
            }
        }

        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            RefreshQualityPolicy();
            EnsureIndirectBuffers();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void TryRegisterScalabilityListener()
        {
            if (_scalabilityListenerRegistered || !Application.isPlaying)
                return;

            ScalabilityEvents.Register(this);
            _scalabilityListenerRegistered = true;
        }

        private void TryUnregisterScalabilityListener()
        {
            if (!_scalabilityListenerRegistered)
                return;

            ScalabilityEvents.Unregister(this);
            _scalabilityListenerRegistered = false;
        }

        private void RefreshQualityPolicy()
        {
            RefreshQualityPolicy(HomeostasisBrain.GlobalQualityWeight);
        }

        private void RefreshQualityPolicy(float qualityWeight01)
        {
            _qualityWeight01 = math.saturate(math.isfinite(qualityWeight01) ? qualityWeight01 : 1f);
            float qualityCurve = SmoothStep01(_qualityWeight01);
            _fastCadenceStride = math.clamp(
                (int)math.round(math.lerp(6f, 1f, qualityCurve)),
                1,
                6);
            _visualOverkillWeight01 = SmoothStep01(math.saturate((_qualityWeight01 - 0.45f) * (1f / 0.55f)));
        }

        private void TryRegisterService()
        {
            if (_registeredService || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterInertialNavigationService(this);
            _registeredService = true;
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterInertialNavigationService(this);
            _registeredService = false;
        }

        private void TryRegisterTickables()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredFastTick)
                _registeredFastTick = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.UI);
            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterTickables()
        {
            if (_registeredFastTick)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.UI);
                _registeredFastTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }
        }

        private bool RefreshFastSignalInputs(out CompassStateDTO state)
        {
            state = default;
            if (!TryGetCompassBuffers(out var stateBuffer, out _, out _))
                return false;

            state = stateBuffer[0];
            SanitizeCompassStateScalars(ref state);
            if ((state.Flags & FlagInitialized) == 0u && state.Power01 <= 0f)
                state.Power01 = 1f;

            ReadOnlySpan<AnomalyProximitySignal> anomalySignals = SignalBus<AnomalyProximitySignal>.GetFrameSnapshot();
            float anomaly = state.AnomalyInterference01 * 0.88f;
            for (int i = 0; i < anomalySignals.Length; i++)
            {
                ref readonly AnomalyProximitySignal signal = ref anomalySignals[i];
                float interference = math.max(
                    SanitizeUnit01(signal.Proximity01),
                    SanitizeUnit01(signal.Interference01));
                anomaly = math.max(anomaly, interference);
            }

            state.AnomalyInterference01 = anomaly;

            ReadOnlySpan<CompassCalibratedSignal> calibrationSignals = SignalBus<CompassCalibratedSignal>.GetFrameSnapshot();
            if (calibrationSignals.Length > 0)
            {
                state.Flags |= FlagCalibrationRequested;
                state.RecalibrationHold01 = 1f;
            }

            ReadOnlySpan<SurvivalVitalsChangedSignal> vitalsSignals = SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < vitalsSignals.Length; i++)
            {
                ref readonly SurvivalVitalsChangedSignal signal = ref vitalsSignals[i];
                if ((signal.Flags & SurvivalVitalsChangedSignalFlags.Energy) != 0u && math.isfinite(signal.Energy01))
                    state.Power01 = math.saturate(signal.Energy01);
            }

            ReadOnlySpan<SystemHealthSignal> healthSignals = SignalBus<SystemHealthSignal>.GetFrameSnapshot();
            for (int i = 0; i < healthSignals.Length; i++)
            {
                ref readonly SystemHealthSignal signal = ref healthSignals[i];
                if (math.isfinite(signal.SystemHealthIndex01))
                    state.SystemStress01 = math.saturate(signal.SystemHealthIndex01);
            }

            ReadOnlySpan<AupShiftSignal> shiftSignals = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shiftSignals.Length; i++)
            {
                uint shiftFrame = shiftSignals[i].ShiftFrameId;
                if (IsNewerFrameId(shiftFrame, state.LastAupShiftFrameId))
                    state.LastAupShiftFrameId = shiftFrame;
            }

            stateBuffer[0] = state;
            return true;
        }

        private bool ShouldUseFastCadence(in CompassStateDTO state)
        {
            return state.SystemStress01 <= StressSlowThreshold01 &&
                   state.Power01 >= PowerDeathThreshold01;
        }

        private bool ConsumeFastCadenceGate()
        {
            int stride = math.clamp(_fastCadenceStride, 1, 6);
            _fastCadenceCounter++;
            if (_fastCadenceCounter < stride)
                return false;

            _fastCadenceCounter = 0;
            return true;
        }

        private void ScheduleDrift(float deltaTime)
        {
            if (_jobPending ||
                !TryGetCompassBuffers(
                    out var stateBuffer,
                    out var outputBuffer,
                    out var blackBox))
            {
                return;
            }

            if (!TryResolvePose(out PlayerRuntimePoseSnapshot pose))
                return;

            CompassStateDTO state = stateBuffer[0];
            SanitizeFiniteState(ref state);
            double3 actualAup = pose.Aup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(actualAup)))
            {
                state.Flags |= FlagNonFiniteFallback;
                stateBuffer[0] = state;
                DumpBlackBoxOnce(state.BlackBoxCursor, blackBox);
                return;
            }

            if ((state.Flags & FlagInitialized) == 0u && state.Power01 <= 0f)
                state.Power01 = 1f;

            float actualHeading = ResolveHeadingFromForward(pose.Forward, state.ActualHeadingDegrees);
            state.ActualAUP = actualAup;
            state.RawEstimatedAUP = actualAup;
            state.EstimatedAUP = actualAup;
            state.Velocity = ResolveVelocity(actualAup, deltaTime, ref state);
            state.ActualHeadingDegrees = actualHeading;
            state.DeltaSeconds = deltaTime;
            state.Frame = state.Frame == uint.MaxValue ? 1u : state.Frame + 1u;
            state.Flags |= FlagInitialized;
            state.Flags = _fastCadenceStride > 1 ? state.Flags | FlagReducedQualityNoise : state.Flags & ~FlagReducedQualityNoise;
            state.Flags = ShouldUseFastCadence(in state) ? state.Flags & ~FlagStressSlowCadence : state.Flags | FlagStressSlowCadence;
            state.Flags = ShouldUseVisualOverkill(in state) ? state.Flags | FlagIndirectDial : state.Flags & ~FlagIndirectDial;
            if ((state.Flags & FlagPowered) == 0u && state.Power01 >= PowerDeathThreshold01)
                state.CurrentHeadingDegrees = actualHeading;

            int resetDrift = (state.Flags & FlagCalibrationRequested) != 0u ? 1 : 0;
            if (resetDrift != 0)
            {
                state.Flags &= ~FlagCalibrationRequested;
                state.RecalibrationHold01 = 0f;
                state.CalibrationCount++;
            }

            state.NoiseClockSeconds += deltaTime;
            if (!math.isfinite(state.NoiseClockSeconds) || state.NoiseClockSeconds > 100000f)
                state.NoiseClockSeconds = 0f;

            stateBuffer[0] = state;

            GyroDriftJob job = new GyroDriftJob
            {
                State = stateBuffer,
                Output = outputBuffer,
                BlackBox = blackBox,
                DeltaSeconds = deltaTime,
                NoiseTime = state.NoiseClockSeconds,
                HeadingCatchupRate = headingCatchupRate,
                DriftNoiseFrequency = driftNoiseFrequency,
                AnomalyNoiseDegrees = anomalyNoiseDegrees,
                WildSpinDegreesPerSecond = wildSpinDegreesPerSecond,
                CalibrationCount = state.CalibrationCount,
                ResetDrift = resetDrift
            };

            _jobHandle = job.Schedule();
            _jobPending = true;
        }

        private bool TryResolvePose(out PlayerRuntimePoseSnapshot pose)
        {
            IPlayerRuntimeContext playerContext = _playerContext;
            if (playerContext != null && playerContext.TryGetPlayerPoseSnapshot(out pose))
                return true;

            pose = default;
            return false;
        }

        private void CompletePendingJob(bool forceComplete)
        {
            if (!_jobPending)
                return;

            if (!forceComplete && !_jobHandle.IsCompleted)
                return;

            Hecton8.Core.DispatcherJobFence.TryComplete(ref _jobHandle, forceComplete);
            _jobPending = false;
            CommitCompletedState();
        }

        private void CommitCompletedState()
        {
            if (!TryGetCompassBuffers(
                    out var stateBuffer,
                    out _,
                    out var blackBox))
            {
                return;
            }

            CompassStateDTO state = stateBuffer[0];
            if (SanitizeFiniteState(ref state))
            {
                state.Flags |= FlagNonFiniteFallback;
                stateBuffer[0] = state;
                DumpBlackBoxOnce(state.BlackBoxCursor, blackBox);
            }

            stateBuffer[0] = state;
        }

        private static InertialNavigationSnapshot BuildSnapshot(in CompassStateDTO state)
        {
            return new InertialNavigationSnapshot
            {
                ActualAUP = state.ActualAUP,
                RawEstimatedAUP = state.RawEstimatedAUP,
                EstimatedAUP = state.EstimatedAUP,
                SubmarineVelocity = state.Velocity,
                GyroDriftError = state.DriftDegrees,
                FalseBearingDegrees = state.CurrentHeadingDegrees,
                RecalibrationHold01 = state.RecalibrationHold01,
                DriftGlitch01 = state.Glitch01,
                CalibrationCount = state.CalibrationCount,
                Flags = state.Flags,
                LastAupShiftFrameId = state.LastAupShiftFrameId,
                LastImpactFrame = 0u,
                LastBrownoutFrame = 0u
            };
        }

        private void ApplyPresentation()
        {
            if (!TryGetCompassBuffers(out var stateBuffer, out var outputBuffer, out _) ||
                !TryGetPresentationBuffer(out var presentationBuffer))
                return;

            CompassStateDTO state = stateBuffer[0];
            if (SanitizeFiniteState(ref state))
                stateBuffer[0] = state;

            CompassPresentationStateDTO presentation = presentationBuffer[0];
            float power = SanitizeUnit01(outputBuffer[(int)CompassOutputSlot.Power01]);
            float heading = NormalizeHeading(outputBuffer[(int)CompassOutputSlot.CurrentHeadingDegrees]);
            float anomaly = SanitizeUnit01(outputBuffer[(int)CompassOutputSlot.AnomalyInterference01]);
            bool powered = power >= PowerDeathThreshold01;
            int cardinalIndex = powered ? ResolveCardinalIndex(heading) : -1;
            bool presentationDirty = ApplyCardinalText(cardinalIndex, powered, ref presentation);

            bool shouldDrawIndirect = ShouldDrawIndirectDial(in state);
            bool shouldApplyHeading = ShouldApplyDialHeading(heading, in presentation);
            if (powered && (shouldDrawIndirect || shouldApplyHeading))
            {
                presentationDirty |= ApplyDialHeading(heading, in state, ref presentation);
                if (shouldApplyHeading)
                {
                    presentation.LastPresentedHeadingDegrees = heading;
                    presentation.PresentationFlags |= PresentationFlagDialInitialized;
                    presentationDirty = true;
                }
            }

            float chromatic = powered && anomaly > 0.8f ? math.saturate((anomaly - 0.8f) * 5f) : 0f;
            float overkill = powered && ShouldUseVisualOverkill(in state) ? math.saturate(anomaly * 1.35f * _visualOverkillWeight01) : 0f;
            presentationDirty |= ApplyChromatic(chromatic, power, overkill, ref presentation);
            presentationDirty |= EmitHighTierFailureParticles(powered, anomaly, in state, ref presentation);

            if (presentationDirty)
                presentationBuffer[0] = presentation;
        }

        private bool ShouldApplyDialHeading(float heading, in CompassPresentationStateDTO presentation)
        {
            return (presentation.PresentationFlags & PresentationFlagDialInitialized) == 0u ||
                   DeltaHeadingAbs(presentation.LastPresentedHeadingDegrees, heading) > HeadingEpsilon;
        }

        private bool ApplyDialHeading(float heading, in CompassStateDTO state, ref CompassPresentationStateDTO presentation)
        {
            float safeHeading = NormalizeHeading(heading);
            if (ShouldDrawIndirectDial(in state))
            {
                return DrawIndirectDial(safeHeading, ref presentation);
            }

            if (dialPivot == null)
                return false;

            dialPivot.localRotation = Quaternion.AngleAxis(NormalizeHeading(safeHeading + dialDegreesOffset), Vector3.up);
            return true;
        }

        private bool ShouldDrawIndirectDial(in CompassStateDTO state)
        {
            return enableIndirectHighTier &&
                   IsValidBuffer(_indirectArgsBuffer) &&
                   IsValidBuffer(_dialMatrixBufferA) &&
                   IsValidBuffer(_dialMatrixBufferB) &&
                   dialMesh != null &&
                   dialIndirectMaterial != null &&
                   SupportsIndirectDial() &&
                   _visualOverkillWeight01 > 0.001f &&
                   state.SystemStress01 <= StressSlowThreshold01;
        }

        private bool ShouldUseVisualOverkill(in CompassStateDTO state)
        {
            return _visualOverkillWeight01 > 0.001f &&
                   state.SystemStress01 <= StressSlowThreshold01;
        }

        private bool DrawIndirectDial(float heading, ref CompassPresentationStateDTO presentation)
        {
            Transform source = dialPivot != null ? dialPivot : (toolRoot != null ? toolRoot : transform);
            float resolvedHeading = NormalizeHeading(heading + dialDegreesOffset);
            Vector3 position = source.position;
            Quaternion rotation = source.rotation * Quaternion.AngleAxis(resolvedHeading, Vector3.up);
            Vector3 scale = source.lossyScale;
            bool stateDirty;
            GraphicsBuffer matrixBuffer = ResolveDialMatrixBuffer(position, rotation, scale, resolvedHeading, ref presentation, out stateDirty);
            if (!IsValidBuffer(matrixBuffer))
                return stateDirty;

            if (_boundDialMatrixBuffer != matrixBuffer)
            {
                dialIndirectMaterial.SetBuffer(_CompassDialMatricesId, matrixBuffer);
                _boundDialMatrixBuffer = matrixBuffer;
            }

            Bounds bounds = indirectDrawBounds;
            bounds.center = position;
            UnityEngine.Graphics.DrawMeshInstancedIndirect(
                dialMesh,
                0,
                dialIndirectMaterial,
                bounds,
                _indirectArgsBuffer,
                0,
                null,
                ShadowCastingMode.Off,
                false,
                source.gameObject.layer,
                null,
                LightProbeUsage.Off,
                null);
            return stateDirty;
        }

        private unsafe GraphicsBuffer ResolveDialMatrixBuffer(
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            float heading,
            ref CompassPresentationStateDTO presentation,
            out bool stateDirty)
        {
            stateDirty = false;
            if ((presentation.PresentationFlags & PresentationFlagDialMatrixDirty) == 0u &&
                IsValidBuffer(_publishedDialMatrixBuffer) &&
                !NeedsDialMatrixUpload(position, rotation, scale, heading, in presentation))
            {
                return _publishedDialMatrixBuffer;
            }

            int writeIndex = presentation.DialMatrixWriteIndex & 1;
            GraphicsBuffer writeBuffer = writeIndex == 0 ? _dialMatrixBufferA : _dialMatrixBufferB;
            if (!IsValidBuffer(writeBuffer))
                return _publishedDialMatrixBuffer;

            Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);
            var mapped = writeBuffer.LockBufferForWrite<Matrix4x4>(0, 1);
            UnsafeUtility.MemCpy(mapped.GetUnsafePtr(), UnsafeUtility.AddressOf(ref matrix), DialMatrixStrideBytes);
            writeBuffer.UnlockBufferAfterWrite<Matrix4x4>(1);

            presentation.DialMatrixWriteIndex = writeIndex ^ 1;
            _publishedDialMatrixBuffer = writeBuffer;
            presentation.LastUploadedDialPosition = ToFloat3(position);
            presentation.LastUploadedDialRotation = ToFloat4(rotation);
            presentation.LastUploadedDialScale = ToFloat3(scale);
            presentation.LastUploadedDialHeadingDegrees = heading;
            presentation.PresentationFlags |= PresentationFlagDialMatrixInitialized;
            presentation.PresentationFlags &= ~PresentationFlagDialMatrixDirty;
            stateDirty = true;
            return writeBuffer;
        }

        private static bool NeedsDialMatrixUpload(
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            float heading,
            in CompassPresentationStateDTO presentation)
        {
            return (presentation.PresentationFlags & PresentationFlagDialMatrixInitialized) == 0u ||
                   DeltaHeadingAbs(presentation.LastUploadedDialHeadingDegrees, heading) > HeadingEpsilon ||
                   math.lengthsq(ToFloat3(position) - presentation.LastUploadedDialPosition) > DialPositionUploadEpsilon ||
                   QuaternionChanged(ToFloat4(rotation), presentation.LastUploadedDialRotation) ||
                   math.lengthsq(ToFloat3(scale) - presentation.LastUploadedDialScale) > DialScaleUploadEpsilon;
        }

        private static bool IsValidBuffer(GraphicsBuffer buffer)
        {
            return buffer != null && buffer.IsValid();
        }

        private static float DeltaHeadingAbs(float previous, float current)
        {
            if (!math.isfinite(previous) || !math.isfinite(current))
                return float.PositiveInfinity;

            float delta = math.fmod(current - previous + 540f, 360f) - 180f;
            return math.abs(delta);
        }

        private static bool QuaternionChanged(float4 current, float4 previous)
        {
            return math.abs(current.x - previous.x) > DialRotationUploadEpsilon ||
                   math.abs(current.y - previous.y) > DialRotationUploadEpsilon ||
                   math.abs(current.z - previous.z) > DialRotationUploadEpsilon ||
                   math.abs(current.w - previous.w) > DialRotationUploadEpsilon;
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static float4 ToFloat4(Quaternion value)
        {
            return new float4(value.x, value.y, value.z, value.w);
        }

        private bool ApplyCardinalText(int cardinalIndex, bool powered, ref CompassPresentationStateDTO presentation)
        {
            if (!_diegeticTextValid || cardinalText == null)
                return false;

            int powerState = powered ? 1 : 0;
            if ((presentation.PresentationFlags & PresentationFlagTextInitialized) != 0u &&
                cardinalIndex == presentation.LastCardinalIndex &&
                powerState == presentation.LastPowerState)
            {
                return false;
            }

            int length;
            if (!powered)
            {
                _cardinalBuffer[0] = '-';
                _cardinalBuffer[1] = '-';
                length = 2;
            }
            else
            {
                length = WriteCardinal(cardinalIndex, _cardinalBuffer);
            }

            cardinalText.SetCharArray(_cardinalBuffer, 0, length);
            presentation.LastCardinalIndex = cardinalIndex;
            presentation.LastPowerState = powerState;
            presentation.PresentationFlags |= PresentationFlagTextInitialized;
            return true;
        }

        private bool ApplyChromatic(float chromatic, float power, float overkill, ref CompassPresentationStateDTO presentation)
        {
            float safeChromatic = SanitizeUnit01(chromatic);
            float safePower = SanitizeUnit01(power);
            float safeOverkill = SanitizeUnit01(overkill);
            if ((presentation.PresentationFlags & PresentationFlagShaderInitialized) != 0u &&
                math.abs(presentation.LastCompassGlassChromatic01 - safeChromatic) <= ChromaticEpsilon &&
                math.abs(presentation.LastCompassPower01 - safePower) <= ChromaticEpsilon &&
                math.abs(presentation.LastCompassOverkill01 - safeOverkill) <= ChromaticEpsilon)
            {
                return false;
            }

            Shader.SetGlobalFloat(_CompassGlassChromaticId, safeChromatic);
            Shader.SetGlobalFloat(_CompassPowerId, safePower);
            Shader.SetGlobalFloat(_CompassOverkillId, safeOverkill);
            presentation.LastCompassGlassChromatic01 = safeChromatic;
            presentation.LastCompassPower01 = safePower;
            presentation.LastCompassOverkill01 = safeOverkill;
            presentation.PresentationFlags |= PresentationFlagShaderInitialized;
            return true;
        }

        private bool EmitHighTierFailureParticles(bool powered, float anomaly, in CompassStateDTO state, ref CompassPresentationStateDTO presentation)
        {
            if (!powered ||
                anomaly <= 0.8f ||
                anomalyFailureParticles == null ||
                anomalyParticleBurst <= 0 ||
                !ShouldUseVisualOverkill(in state))
            {
                if (presentation.ParticleDebt == 0f)
                    return false;

                presentation.ParticleDebt = 0f;
                return true;
            }

            int burst = math.clamp(
                (int)math.round(math.min(anomalyParticleBurst, MaxAnomalyParticleBurst) * _visualOverkillWeight01),
                0,
                MaxAnomalyParticleBurst);
            if (burst <= 0)
            {
                presentation.ParticleDebt = 0f;
                return true;
            }

            float debt = presentation.ParticleDebt + math.saturate((anomaly - 0.8f) * 5f) * burst;
            if (!math.isfinite(debt))
                debt = 0f;

            int emitCount = (int)math.floor(debt);
            if (emitCount <= 0)
            {
                presentation.ParticleDebt = debt;
                return true;
            }

            debt -= emitCount;
            presentation.ParticleDebt = debt;
            anomalyFailureParticles.Emit(emitCount);
            return true;
        }

        private void EnsureIndirectBuffers()
        {
            if (!enableIndirectHighTier ||
                _visualOverkillWeight01 <= 0.001f ||
                dialMesh == null ||
                dialIndirectMaterial == null ||
                !SupportsIndirectDial())
            {
                ReleaseIndirectBuffers();
                return;
            }

            if (IsValidBuffer(_indirectArgsBuffer) &&
                IsValidBuffer(_dialMatrixBufferA) &&
                IsValidBuffer(_dialMatrixBufferB))
            {
                return;
            }

            ReleaseIndirectBuffers();
            _indirectArgs[0] = dialMesh.GetIndexCount(0);
            _indirectArgs[1] = 1u;
            _indirectArgs[2] = dialMesh.GetIndexStart(0);
            _indirectArgs[3] = dialMesh.GetBaseVertex(0);
            _indirectArgs[4] = 0u;
            _indirectArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, sizeof(uint) * _indirectArgs.Length); // COLD ALLOC: GraphicsBuffer[1] - compass indirect args - owner: DiegeticGyroCompassRuntime
            _dialMatrixBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, DialMatrixStrideBytes); // COLD ALLOC: GraphicsBuffer[1] - compass dial matrix buffer A - owner: DiegeticGyroCompassRuntime
            _dialMatrixBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, DialMatrixStrideBytes); // COLD ALLOC: GraphicsBuffer[1] - compass dial matrix buffer B - owner: DiegeticGyroCompassRuntime
            UploadIndirectArgs();
            MarkDialMatrixPresentationDirty();
        }

        private void ReleaseIndirectBuffers()
        {
            ReleaseGraphicsBuffer(ref _indirectArgsBuffer);
            ReleaseGraphicsBuffer(ref _dialMatrixBufferA);
            ReleaseGraphicsBuffer(ref _dialMatrixBufferB);
            _publishedDialMatrixBuffer = null;
            _boundDialMatrixBuffer = null;
            MarkDialMatrixPresentationDirty();
        }

        private unsafe void UploadIndirectArgs()
        {
            if (!IsValidBuffer(_indirectArgsBuffer))
                return;

            var mapped = _indirectArgsBuffer.LockBufferForWrite<uint>(0, _indirectArgs.Length);
            fixed (uint* source = _indirectArgs)
            {
                UnsafeUtility.MemCpy(mapped.GetUnsafePtr(), source, sizeof(uint) * _indirectArgs.Length);
            }

            _indirectArgsBuffer.UnlockBufferAfterWrite<uint>(_indirectArgs.Length);
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void ValidateDiegeticTextBinding()
        {
            _diegeticTextValid = true;
            TextMeshProUGUI uiText = cardinalText as TextMeshProUGUI;
            if (uiText == null)
                return;

            Canvas canvas = uiText.canvas;
            _diegeticTextValid = canvas != null && canvas.renderMode == RenderMode.WorldSpace;
        }

        private static float3 ResolveVelocity(double3 actualAup, float deltaTime, ref CompassStateDTO state)
        {
            if ((state.Flags & FlagHasPreviousAup) == 0u || !math.isfinite(deltaTime) || deltaTime <= math.EPSILON)
            {
                state.PreviousActualAUP = actualAup;
                state.Flags |= FlagHasPreviousAup;
                return float3.zero;
            }

            double invDelta = 1d / math.max(deltaTime, math.EPSILON);
            double3 velocity = (actualAup - state.PreviousActualAUP) * invDelta;
            state.PreviousActualAUP = actualAup;
            if (!math.all(math.isfinite(velocity)))
                return float3.zero;

            velocity = math.clamp(
                velocity,
                new double3(-VelocityClampMetersPerSecond),
                new double3(VelocityClampMetersPerSecond));
            return new float3((float)velocity.x, (float)velocity.y, (float)velocity.z);
        }

        private static void WriteBlackBox(ref CompassStateDTO state, NativeSlice<CompassBlackBoxEntry> blackBox)
        {
            if (blackBox.Length < BlackBoxCapacity)
                return;

            int cursor = state.BlackBoxCursor;
            if (cursor < 0 || cursor >= BlackBoxCapacity)
                cursor = 0;

            blackBox[cursor] = new CompassBlackBoxEntry
            {
                Frame = state.Frame,
                ActualHeadingDegrees = state.ActualHeadingDegrees,
                CurrentHeadingDegrees = state.CurrentHeadingDegrees,
                DriftDegrees = state.DriftDegrees,
                MaxGyroDriftDegrees = state.MaxGyroDriftDegrees,
                AnomalyInterference01 = state.AnomalyInterference01,
                Power01 = state.Power01,
                Flags = state.Flags,
                LastAupShiftFrameId = state.LastAupShiftFrameId,
                CalibrationCount = state.CalibrationCount
            };

            cursor++;
            if (cursor >= BlackBoxCapacity)
                cursor = 0;

            state.BlackBoxCursor = cursor;
        }

        private void DumpBlackBoxOnce(int blackBoxCursor, NativeSlice<CompassBlackBoxEntry> blackBox)
        {
            if (_blackBoxDumped || blackBox.Length < BlackBoxCapacity)
                return;

            _blackBoxDumped = true;
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string directory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, DumpFileName);
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(DumpMagic);
                    writer.Write(BlackBoxCapacity);
                    int cursor = blackBoxCursor;
                    if (cursor < 0 || cursor >= BlackBoxCapacity)
                        cursor = 0;

                    writer.Write(cursor);
                    for (int i = 0; i < BlackBoxCapacity; i++)
                    {
                        int index = cursor + i;
                        if (index >= BlackBoxCapacity)
                            index -= BlackBoxCapacity;

                        CompassBlackBoxEntry entry = blackBox[index];
                        writer.Write(entry.Frame);
                        writer.Write(entry.ActualHeadingDegrees);
                        writer.Write(entry.CurrentHeadingDegrees);
                        writer.Write(entry.DriftDegrees);
                        writer.Write(entry.MaxGyroDriftDegrees);
                        writer.Write(entry.AnomalyInterference01);
                        writer.Write(entry.Power01);
                        writer.Write(entry.Flags);
                        writer.Write(entry.LastAupShiftFrameId);
                        writer.Write(entry.CalibrationCount);
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static bool SupportsIndirectDial()
        {
            GraphicsDeviceType deviceType = SystemInfo.graphicsDeviceType;
            if (deviceType == GraphicsDeviceType.OpenGLES2 || deviceType == GraphicsDeviceType.OpenGLES3)
                return false;

            return SystemInfo.supportsInstancing && SystemInfo.supportsComputeShaders;
        }

        private static bool IsNewerFrameId(uint frame, uint lastFrame)
        {
            return frame != 0u &&
                   frame != lastFrame &&
                   unchecked(frame - lastFrame) < 0x80000000u;
        }

        private static float SanitizeDeltaTime(float deltaTime)
        {
            return math.isfinite(deltaTime) ? math.clamp(deltaTime, 0f, MaxIntegrationDeltaSeconds) : 0f;
        }

        private static float ResolveHeadingFromForward(float3 forward, float fallback)
        {
            forward.y = 0f;
            if (!math.all(math.isfinite(forward)) || math.lengthsq(forward) < 0.0001f)
                return NormalizeHeading(fallback);

            float heading = math.degrees(math.atan2(forward.x, forward.z));
            return NormalizeHeading(heading);
        }

        private static int ResolveCardinalIndex(float heading)
        {
            float normalized = NormalizeHeading(heading);
            int index = (int)math.floor((normalized + 22.5f) * (1f / 45f));
            return index & 7;
        }

        private static int WriteCardinal(int cardinalIndex, char[] buffer)
        {
            switch (cardinalIndex & 7)
            {
                case 0:
                    buffer[0] = 'N';
                    return 1;
                case 1:
                    buffer[0] = 'N';
                    buffer[1] = 'E';
                    return 2;
                case 2:
                    buffer[0] = 'E';
                    return 1;
                case 3:
                    buffer[0] = 'S';
                    buffer[1] = 'E';
                    return 2;
                case 4:
                    buffer[0] = 'S';
                    return 1;
                case 5:
                    buffer[0] = 'S';
                    buffer[1] = 'W';
                    return 2;
                case 6:
                    buffer[0] = 'W';
                    return 1;
                default:
                    buffer[0] = 'N';
                    buffer[1] = 'W';
                    return 2;
            }
        }

        private static float NormalizeHeading(float heading)
        {
            if (!math.isfinite(heading))
                return 0f;

            float normalized = math.fmod(heading, 360f);
            return normalized < 0f ? normalized + 360f : normalized;
        }

        private static float SanitizeUnit01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static bool SanitizeUnit01(ref float value)
        {
            float safe = SanitizeUnit01(value);
            if (value == safe)
                return false;

            value = safe;
            return true;
        }

        private static bool SanitizeFiniteFloat(ref float value, float fallback)
        {
            if (math.isfinite(value))
                return false;

            value = fallback;
            return true;
        }

        private static bool SanitizeAbsFloat(ref float value)
        {
            float safe = math.isfinite(value) ? math.abs(value) : 0f;
            if (value == safe)
                return false;

            value = safe;
            return true;
        }

        private static bool SanitizeHeadingDegrees(ref float value)
        {
            float safe = NormalizeHeading(value);
            if (value == safe)
                return false;

            value = safe;
            return true;
        }

        private static bool SanitizeDouble3Zero(ref double3 value)
        {
            if (math.all(math.isfinite(value)))
                return false;

            value = double3.zero;
            return true;
        }

        private static bool SanitizeFloat3Zero(ref float3 value)
        {
            if (math.all(math.isfinite(value)))
                return false;

            value = float3.zero;
            return true;
        }

        private static bool SanitizeNoiseClock(ref float value)
        {
            float safe = math.isfinite(value) && value >= 0f && value <= 100000f ? value : 0f;
            if (value == safe)
                return false;

            value = safe;
            return true;
        }

        private static bool SanitizeCompassStateScalars(ref CompassStateDTO state)
        {
            bool changed = false;
            changed |= SanitizeUnit01(ref state.AnomalyInterference01);
            changed |= SanitizeUnit01(ref state.Power01);
            changed |= SanitizeUnit01(ref state.Glitch01);
            changed |= SanitizeUnit01(ref state.RecalibrationHold01);
            changed |= SanitizeUnit01(ref state.SystemStress01);
            changed |= SanitizeNoiseClock(ref state.NoiseClockSeconds);
            changed |= SanitizeAbsFloat(ref state.MaxGyroDriftDegrees);
            return changed;
        }

        private static bool SanitizeFiniteState(ref CompassStateDTO state)
        {
            bool changed = SanitizeCompassStateScalars(ref state);
            changed |= SanitizeDouble3Zero(ref state.ActualAUP);
            changed |= SanitizeDouble3Zero(ref state.RawEstimatedAUP);
            changed |= SanitizeDouble3Zero(ref state.EstimatedAUP);
            if (SanitizeDouble3Zero(ref state.PreviousActualAUP))
            {
                state.Flags &= ~FlagHasPreviousAup;
                changed = true;
            }

            changed |= SanitizeFloat3Zero(ref state.Velocity);
            changed |= SanitizeHeadingDegrees(ref state.ActualHeadingDegrees);
            changed |= SanitizeHeadingDegrees(ref state.CurrentHeadingDegrees);
            changed |= SanitizeFiniteFloat(ref state.DriftDegrees, 0f);
            float safeDeltaSeconds = SanitizeDeltaTime(state.DeltaSeconds);
            if (state.DeltaSeconds != safeDeltaSeconds)
            {
                state.DeltaSeconds = safeDeltaSeconds;
                changed = true;
            }

            return changed;
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(math.isfinite(value) ? value : 1f);
            return t * t * (3f - 2f * t);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct GyroDriftJob : IJob
        {
            [NoAlias] public NativeSlice<CompassStateDTO> State;
            [NoAlias] public NativeSlice<float> Output;
            [NoAlias] public NativeSlice<CompassBlackBoxEntry> BlackBox;
            public float DeltaSeconds;
            public float NoiseTime;
            public float HeadingCatchupRate;
            public float DriftNoiseFrequency;
            public float AnomalyNoiseDegrees;
            public float WildSpinDegreesPerSecond;
            public int CalibrationCount;
            public int ResetDrift;

            public void Execute()
            {
                CompassStateDTO state = State[0];
                float deltaTime = math.isfinite(DeltaSeconds) ? math.clamp(DeltaSeconds, 0f, MaxIntegrationDeltaSeconds) : 0f;
                float catchupRate = SanitizeNonNegative(HeadingCatchupRate);
                float noiseFrequency = SanitizeNonNegative(DriftNoiseFrequency);
                float noiseDegrees = SanitizeNonNegative(AnomalyNoiseDegrees);
                float wildSpinRate = SanitizeNonNegative(WildSpinDegreesPerSecond);
                float actualHeading = NormalizeAngle(state.ActualHeadingDegrees);
                float currentHeading = (state.Flags & FlagInitialized) != 0u
                    ? NormalizeAngle(state.CurrentHeadingDegrees)
                    : actualHeading;
                float power = SanitizeUnit01(state.Power01);
                float anomaly = SanitizeUnit01(state.AnomalyInterference01);

                uint flags = state.Flags | FlagInitialized;
                flags &= ~FlagCalibrationApplied;
                flags = power >= PowerDeathThreshold01 ? flags | FlagPowered : flags & ~FlagPowered;
                flags = anomaly > 0.8f ? flags | FlagAnomalyUnstable : flags & ~FlagAnomalyUnstable;

                if (ResetDrift != 0)
                {
                    currentHeading = actualHeading;
                    flags |= FlagCalibrationApplied;
                }
                else if (power >= PowerDeathThreshold01)
                {
                    float headingDelta = DeltaAngleDegrees(currentHeading, actualHeading);
                    float alpha = SanitizeUnit01(catchupRate * deltaTime);
                    float noiseValue = ResolveNoiseValue(NoiseTime, noiseFrequency, flags);
                    currentHeading += headingDelta * alpha;
                    currentHeading += noiseValue * noiseDegrees * anomaly * deltaTime;
                    if (anomaly > 0.8f)
                    {
                        float spinSign = noiseValue < 0f ? -1f : 1f;
                        currentHeading += spinSign * wildSpinRate * anomaly * deltaTime;
                    }
                }

                currentHeading = NormalizeAngle(currentHeading);
                float drift = DeltaAngleDegrees(actualHeading, currentHeading);
                float maxDrift = math.max(math.abs(state.MaxGyroDriftDegrees), math.abs(drift));
                float glitch = SanitizeUnit01(anomaly * 1.25f + SanitizeUnit01(math.abs(drift) * (1f / 90f)) * 0.25f);

                if (!math.isfinite(currentHeading) ||
                    !math.isfinite(actualHeading) ||
                    !math.isfinite(drift) ||
                    !math.isfinite(maxDrift))
                {
                    currentHeading = 0f;
                    actualHeading = 0f;
                    drift = 0f;
                    maxDrift = 0f;
                    glitch = 1f;
                    flags |= FlagNonFiniteFallback;
                }

                state.ActualHeadingDegrees = actualHeading;
                state.CurrentHeadingDegrees = currentHeading;
                state.DriftDegrees = drift;
                state.AnomalyInterference01 = anomaly;
                state.Power01 = power;
                state.Glitch01 = glitch;
                state.MaxGyroDriftDegrees = maxDrift;
                state.CalibrationCount = CalibrationCount;
                state.Flags = flags;
                WriteBlackBox(ref state, BlackBox);
                State[0] = state;

                Output[(int)CompassOutputSlot.CurrentHeadingDegrees] = currentHeading;
                Output[(int)CompassOutputSlot.ActualHeadingDegrees] = actualHeading;
                Output[(int)CompassOutputSlot.DriftDegrees] = drift;
                Output[(int)CompassOutputSlot.AnomalyInterference01] = anomaly;
                Output[(int)CompassOutputSlot.Power01] = power;
                Output[(int)CompassOutputSlot.Glitch01] = glitch;
                Output[(int)CompassOutputSlot.CardinalIndex] = ResolveCardinal(currentHeading);
                Output[(int)CompassOutputSlot.MaxGyroDriftDegrees] = maxDrift;
            }

            private static float NormalizeAngle(float heading)
            {
                if (!math.isfinite(heading))
                    return 0f;

                float normalized = math.fmod(heading, 360f);
                return normalized < 0f ? normalized + 360f : normalized;
            }

            private static float DeltaAngleDegrees(float from, float to)
            {
                float delta = NormalizeAngle(to) - NormalizeAngle(from);
                delta = math.fmod(delta + 540f, 360f) - 180f;
                return math.isfinite(delta) ? delta : 0f;
            }

            private static float ResolveCardinal(float heading)
            {
                float normalized = NormalizeAngle(heading);
                int index = (int)math.floor((normalized + 22.5f) * (1f / 45f));
                return index & 7;
            }

            private static float ResolveNoiseValue(float noiseTime, float noiseFrequency, uint flags)
            {
                if (!math.isfinite(noiseTime) || !math.isfinite(noiseFrequency))
                    return 0f;

                float t = noiseTime * noiseFrequency;
                if ((flags & FlagReducedQualityNoise) != 0u)
                    return TriangleNoise(t);

                float baseNoise = noise.cnoise(new float2(t, 17.371f));
                if ((flags & FlagIndirectDial) == 0u)
                    return baseNoise;

                return math.clamp(baseNoise + noise.cnoise(new float2(t * 2.07f, 43.113f)) * 0.35f, -1f, 1f);
            }

            private static float TriangleNoise(float t)
            {
                if (!math.isfinite(t))
                    return 0f;

                float phase = math.frac(t);
                return 1f - math.abs(phase * 4f - 2f);
            }

            private static float SanitizeUnit01(float value)
            {
                return math.isfinite(value) ? math.saturate(value) : 0f;
            }

            private static float SanitizeNonNegative(float value)
            {
                return math.isfinite(value) ? math.max(0f, value) : 0f;
            }
        }
    }
}
