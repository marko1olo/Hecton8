using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using TMPro;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

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
        [FieldOffset(40)] private byte _pad0;
        [FieldOffset(41)] private byte _pad1;
        [FieldOffset(42)] private byte _pad2;
        [FieldOffset(43)] private byte _pad3;
        [FieldOffset(44)] private byte _pad4;
        [FieldOffset(45)] private byte _pad5;
        [FieldOffset(46)] private byte _pad6;
        [FieldOffset(47)] private byte _pad7;
        [FieldOffset(48)] private byte _pad8;
        [FieldOffset(49)] private byte _pad9;
        [FieldOffset(50)] private byte _pad10;
        [FieldOffset(51)] private byte _pad11;
        [FieldOffset(52)] private byte _pad12;
        [FieldOffset(53)] private byte _pad13;
        [FieldOffset(54)] private byte _pad14;
        [FieldOffset(55)] private byte _pad15;
        [FieldOffset(56)] private byte _pad16;
        [FieldOffset(57)] private byte _pad17;
        [FieldOffset(58)] private byte _pad18;
        [FieldOffset(59)] private byte _pad19;
        [FieldOffset(60)] private byte _pad20;
        [FieldOffset(61)] private byte _pad21;
        [FieldOffset(62)] private byte _pad22;
        [FieldOffset(63)] private byte _pad23;
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
        private static int s_x001DiegeticGyroCompassRuntimeSignalPushDropCount;
        private const uint CompassCalibrationLaneHash = 0xC06A5511u;
        private const uint CompassAnomalyLaneHash = 0xC06A5512u;

        public static void ConfigureOwnedLanes()
        {
            SignalBus<AnomalyProximitySignal>.Configure(
                AnomalyProximitySignal.ExpectedCapacity,
                AnomalyProximitySignal.MaxFrameSignals,
                AnomalyProximitySignal.LowTierFrameSignals,
                AnomalyProximitySignal.LaneHash);
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
            SignalBus<CompassCalibratedSignal>.TryPushTracked(in signal, ref s_x001DiegeticGyroCompassRuntimeSignalPushDropCount);
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
            SignalBus<AnomalyProximitySignal>.TryPushTracked(in signal, ref s_x001DiegeticGyroCompassRuntimeSignalPushDropCount);
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
    public sealed class DiegeticGyroCompassRuntime : MonoBehaviour, IInertialNavigationService, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
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
        private const float DegreesToRadians = 0.01745329252f;
        private const float TwoPi = 6.28318530718f;
        private const float HalfPi = 1.57079632679f;
        private const float Pi = 3.14159265359f;
        private const int MaxAnomalyParticleBurst = 128;
        private const int DialMatrixStrideBytes = 64;
        private const float DialPositionUploadEpsilon = 0.000001f;
        private const float DialRotationUploadEpsilon = 0.0001f;
        private const float DialScaleUploadEpsilon = 0.000001f;
        private const uint DumpMagic = 0x4759434Fu;
        private const string DumpFileName = "Dump_COMPASS_GYRO_STABILIZER.bin";
        private const string DumpPayloadLabel = "diegeticGyroCompassDumpPayload";
        private const uint FlagInitialized = 1u << 0;
        private const uint FlagPowered = 1u << 1;
        private const uint FlagAnomalyUnstable = 1u << 2;
        private const uint FlagStressSlowCadence = 1u << 3;
        private const uint FlagCalibrationApplied = 1u << 4;
        private const uint FlagNonFiniteFallback = 1u << 5;
        private const uint FlagReducedQualityNoise = 1u << 6;
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
        [SerializeField, FormerlySerializedAs("enableIndirectHighTier"), Tooltip("Allows capable presentation routes to draw the physical dial mesh through indirect instancing. Quality scales visual weight only.")]
        private bool enableIndirectVisualRoute = true;

        [SerializeField, Tooltip("Dial mesh used by the indirect presentation route.")]
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

        [Header("Anomaly Failure VFX")]
        [SerializeField, Tooltip("Optional local particle emitter for salt/static bursts around the physical compass glass. Emission follows continuous visual overkill weight.")]
        private ParticleSystem anomalyFailureParticles;

        [SerializeField, Min(0), Tooltip("Maximum particles emitted per LateFrameTick while anomaly interference is saturated. Code clamps to 128.")]
        private int anomalyParticleBurst = 64;

        private IDataVault _vault;
        private IPlayerRuntimeContext _playerContext;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _registeredService;
        private bool _hotSwapListenerRegistered;
        private bool _diegeticTextValid = true;
        private bool _blackBoxDumpQueued;
        private bool _blackBoxDumped;
        private bool _indirectBuffersDirty;
        private bool _supportsIndirectDialCold;
        private float _qualityWeight01 = 1f;
        private float _visualOverkillWeight01 = 1f;
        private float _fastCadenceAccumulatedDelta;
        private int _fastCadenceStride = 1;
        private int _fastCadenceCounter;
        private int _queuedBlackBoxCursor;
        private bool _manualRecalibrationRequested;
        private float _manualRecalibrationHold01;
        private VaultLane<CompassStateDTO> _stateLane;
        private VaultLane<CompassPresentationStateDTO> _presentationLane;
        private VaultLane<float> _headingOutputLane;
        private VaultLane<CompassBlackBoxEntry> _blackBoxLane;

        private readonly char[] _cardinalBuffer = new char[2]; // COLD ALLOC: char[2] - diegetic compass cardinal text buffer - owner: DiegeticGyroCompassRuntime
        private readonly uint[] _indirectArgs = new uint[5]; // COLD ALLOC: uint[5] - compass indirect draw args - owner: DiegeticGyroCompassRuntime
        private GraphicsBuffer _indirectArgsBufferA;
        private GraphicsBuffer _indirectArgsBufferB;
        private GraphicsBuffer _activeIndirectArgsBuffer;
        private GraphicsBuffer _dialMatrixBufferA;
        private GraphicsBuffer _dialMatrixBufferB;
        private GraphicsBuffer _publishedDialMatrixBuffer;
        private GraphicsBuffer _boundDialMatrixBuffer;
        private int _indirectArgsUploadBufferIndex;

        /// <inheritdoc />
        public InertialNavigationSnapshot Snapshot => TryGetSnapshot(out InertialNavigationSnapshot snapshot) ? snapshot : default;

        /// <inheritdoc />
        public double3 EstimatedAUP => TryGetSnapshot(out InertialNavigationSnapshot snapshot) ? snapshot.EstimatedAUP : double3.zero;

        /// <inheritdoc />
        public float GyroDriftError => TryGetSnapshot(out InertialNavigationSnapshot snapshot) ? snapshot.GyroDriftError : 0f;

        private struct VaultLane<T> where T : unmanaged
        {
            public VaultGenerationHandle<T> Handle;
            public uint ExpectedBufferID;
            public int Length;
        }

        private void Awake()
        {
            CacheGraphicsCapabilitiesCold();
            ValidateDiegeticTextBinding();
        }

        private void OnEnable()
        {
            CacheGraphicsCapabilitiesCold();
            ConfigureSignalLanes();
            CacheColdDependencies();
            TryResolveVaultBuffers();
            TryRegisterHotSwapListener();
            TryRegisterService();
            TryRegisterTickables();
        }

        private void Start()
        {
            CacheGraphicsCapabilitiesCold();
            CacheColdDependencies();
            TryResolveVaultBuffers();
            TryRegisterService();
            TryRegisterTickables();
            EnsureIndirectBuffersCold();
            _indirectBuffersDirty = ShouldRequireIndirectBuffersCold() && !HasIndirectBuffersReady();
        }

        private void OnDisable()
        {
            FlushQueuedBlackBoxDump();
            TryUnregisterTickables();
            TryUnregisterService();
            TryUnregisterHotSwapListener();
            ClearCompassShaderGlobals();
        }

        private void OnDestroy()
        {
            FlushQueuedBlackBoxDump();
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
            _manualRecalibrationRequested = true;
            _manualRecalibrationHold01 = 1f;

            if (!TryAcquireStateWriteBuffer(out NativeArray<CompassStateDTO> stateBuffer, out IDataVault writeVault))
                return;

            try
            {
                CompassStateDTO state = stateBuffer[0];
                state.Flags |= FlagCalibrationRequested;
                state.RecalibrationHold01 = 1f;
                stateBuffer[0] = state;
                _manualRecalibrationRequested = false;
                _manualRecalibrationHold01 = 0f;
            }
            finally
            {
                ReleaseStateWriteBuffer(writeVault);
            }
        }

        /// <inheritdoc />
        public bool TryAccumulateRecalibrationHold(float deltaTime, out float progress01)
        {
            progress01 = 0f;
            float safeDeltaTime = SanitizeDeltaTime(deltaTime);

            if (!TryAcquireStateWriteBuffer(out NativeArray<CompassStateDTO> stateBuffer, out IDataVault writeVault))
                return false;

            try
            {
                CompassStateDTO state = stateBuffer[0];
                SanitizeCompassStateScalars(ref state);
                state.RecalibrationHold01 = math.saturate(
                    math.max(state.RecalibrationHold01, _manualRecalibrationHold01) +
                    safeDeltaTime * math.rcp(RecalibrationHoldSeconds));
                progress01 = state.RecalibrationHold01;
                if (state.RecalibrationHold01 >= 1f)
                {
                    state.Flags |= FlagCalibrationRequested;
                    _manualRecalibrationRequested = false;
                }

                stateBuffer[0] = state;
                _manualRecalibrationHold01 = 0f;
            }
            finally
            {
                ReleaseStateWriteBuffer(writeVault);
            }

            return true;
        }

        /// <inheritdoc />
        public void CancelRecalibrationHold()
        {
            _manualRecalibrationRequested = false;
            _manualRecalibrationHold01 = 0f;

            if (!TryAcquireStateWriteBuffer(out NativeArray<CompassStateDTO> stateBuffer, out IDataVault writeVault))
                return;

            try
            {
                CompassStateDTO state = stateBuffer[0];
                state.RecalibrationHold01 = 0f;
                stateBuffer[0] = state;
            }
            finally
            {
                ReleaseStateWriteBuffer(writeVault);
            }
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
            RefreshQualityPolicy(qualityWeight01);
            RebindDataVaultForLifecycle(vault);
            EnsureIndirectBuffersCold();
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsIndirectDialCold = SupportsIndirectDialCold();
        }

        /// <summary>
        /// Binds the runtime to a physical compass tool without relying on screen-space UI.
        /// </summary>
        /// <param name="nextToolRoot">Physical tool or cockpit instrument root.</param>
        /// <param name="nextDialPivot">Optional authored dial pivot.</param>
        /// <param name="nextCardinalText">Optional diegetic cardinal label.</param>
        /// <param name="nextDialMesh">Optional indirect dial mesh.</param>
        /// <param name="nextDialMaterial">Optional indirect dial material.</param>
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

            EnsureIndirectBuffersCold();
            _indirectBuffersDirty = ShouldRequireIndirectBuffersCold() && !HasIndirectBuffersReady();
        }

        /// <summary>
        /// Binds optional visual-overkill local failure VFX for the physical compass glass.
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
        private void AdvanceFastCompassPresentation(float deltaTime)
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
            RefreshQualityPolicy();
            FlushQueuedBlackBoxDump();
            if (ShouldRequireIndirectBuffersCold() && !HasIndirectBuffersReady())
                _indirectBuffersDirty = true;

            if (_indirectBuffersDirty)
                FlushIndirectBuffersRepairSlow();

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
            AdvanceFastCompassPresentation(SystemDispatcher.CurrentFrameDeltaTime);

            if (_indirectBuffersDirty)
                return;

            ApplyPresentation();
        }

        private void ConfigureSignalLanes()
        {
            DiegeticCompassSignals.ConfigureOwnedLanes();

            SignalBus<SurvivalVitalsChangedSignal>.EnsureInitialized();
            SignalBus<SystemHealthSignal>.EnsureInitialized();
            SignalBus<AupShiftSignal>.EnsureInitialized();
        }

        private void CacheColdDependencies()
        {
            RefreshQualityPolicy();

            if (_playerContext == null)
                _playerContext = GlobalRegistry.Player;

            IDataVault currentVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_vault, currentVault))
                RebindDataVaultForLifecycle(currentVault);
        }

        private bool TryResolveVaultBuffers()
        {
            bool compassReady = TryPrepareCompassBuffersCold();
            bool presentationReady = TryPreparePresentationBufferCold();
            return compassReady && presentationReady;
        }

        private bool TryReadCompassState(out CompassStateDTO state)
        {
            state = default;
            if (!TryGetExistingStateBuffer(out var stateBuffer))
                return false;

            state = stateBuffer[0];
            SanitizeFiniteState(ref state);

            return true;
        }

        private bool TryGetExistingStateBuffer(out NativeArray<CompassStateDTO>.ReadOnly stateBuffer)
        {
            stateBuffer = default;
            return TryReadExistingLane(
                    ref _stateLane,
                    BufferID.CompassState,
                    StateLength,
                    out stateBuffer);
        }

        private bool TryGetExistingPresentationBuffer(out NativeArray<CompassPresentationStateDTO>.ReadOnly presentationBuffer)
        {
            presentationBuffer = default;
            return TryReadExistingLane(
                    ref _presentationLane,
                    BufferID.CompassPresentationState,
                    StateLength,
                    out presentationBuffer);
        }

        private bool TryGetPresentationBuffer(out NativeArray<CompassPresentationStateDTO>.ReadOnly presentationBuffer)
        {
            presentationBuffer = default;
            return TryReadExistingLane(
                    ref _presentationLane,
                    BufferID.CompassPresentationState,
                    StateLength,
                    out presentationBuffer);
        }

        private bool TryPreparePresentationBufferCold()
        {
            IDataVault vault = _vault;
            return TryBindOrAcquireLane(
                    vault,
                    ref _presentationLane,
                    BufferID.CompassPresentationState,
                    StateLength,
                    NativeArrayOptions.ClearMemory);
        }

        private void ResetPresentationState(bool resetDialMatrix)
        {
            if (!TryAcquireOrCreatePresentationWriteBuffer(out NativeArray<CompassPresentationStateDTO> presentationBuffer, out IDataVault writeVault))
                return;

            try
            {
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
            finally
            {
                ReleasePresentationWriteBuffer(writeVault);
            }
        }

        private void ResetParticleDebt()
        {
            if (!TryAcquirePresentationWriteBuffer(out NativeArray<CompassPresentationStateDTO> presentationBuffer, out IDataVault writeVault))
                return;

            try
            {
                CompassPresentationStateDTO presentation = presentationBuffer[0];
                presentation.ParticleDebt = 0f;
                presentationBuffer[0] = presentation;
            }
            finally
            {
                ReleasePresentationWriteBuffer(writeVault);
            }
        }

        private void MarkDialMatrixPresentationDirty()
        {
            if (!TryAcquirePresentationWriteBuffer(out NativeArray<CompassPresentationStateDTO> presentationBuffer, out IDataVault writeVault))
                return;

            try
            {
                CompassPresentationStateDTO presentation = presentationBuffer[0];
                MarkDialMatrixPresentationDirty(ref presentation);
                presentationBuffer[0] = presentation;
            }
            finally
            {
                ReleasePresentationWriteBuffer(writeVault);
            }
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

            if (!TryAcquirePresentationWriteBuffer(out NativeArray<CompassPresentationStateDTO> presentationBuffer, out IDataVault writeVault))
                return;

            try
            {
                CompassPresentationStateDTO presentation = presentationBuffer[0];
                presentation.LastCompassGlassChromatic01 = 0f;
                presentation.LastCompassPower01 = 0f;
                presentation.LastCompassOverkill01 = 0f;
                presentation.PresentationFlags &= ~PresentationFlagShaderInitialized;
                presentationBuffer[0] = presentation;
            }
            finally
            {
                ReleasePresentationWriteBuffer(writeVault);
            }
        }

        private bool TryPrepareCompassBuffersCold()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return false;

            return TryBindOrAcquireLane(vault, ref _stateLane, BufferID.CompassState, StateLength, NativeArrayOptions.ClearMemory) &&
                   TryBindOrAcquireLane(vault, ref _headingOutputLane, BufferID.CompassHeadingOutput, (int)CompassOutputSlot.Count, NativeArrayOptions.ClearMemory) &&
                   TryBindOrAcquireLane(vault, ref _blackBoxLane, BufferID.CompassBlackBox, BlackBoxCapacity, NativeArrayOptions.ClearMemory);
        }

        private bool TryBindExistingLane<T>(
            ref VaultLane<T> lane,
            BufferID bufferId,
            int requiredLength) where T : unmanaged
        {
            IDataVault vault = _vault;
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsLaneBound(in lane) && lane.Length >= requiredLength)
                return true;

            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existing))
                return false;

            lane = CreateLane(in existing, bufferId, requiredLength);
            return IsLaneBound(in lane) && lane.Length >= requiredLength;
        }

        private static bool TryBindOrAcquireLane<T>(
            IDataVault vault,
            ref VaultLane<T> lane,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : unmanaged
        {
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsLaneBound(in lane) && lane.Length >= requiredLength)
                return true;

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existing))
            {
                lane = CreateLane(in existing, bufferId, requiredLength);
                if (IsLaneBound(in lane) && lane.Length >= requiredLength)
                    return true;
            }

            lane = AcquireLane<T>(vault, bufferId, requiredLength, options);
            return IsLaneBound(in lane) && lane.Length >= requiredLength;
        }

        private static VaultLane<T> AcquireLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : unmanaged
        {
            if (vault == null || requiredLength <= 0)
                return default;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                OwnerSystem,
                options);
            return CreateLane(in handle, bufferId, requiredLength);
        }

        private static VaultLane<T> CreateLane<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : unmanaged
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

        private static bool IsLaneBound<T>(in VaultLane<T> lane) where T : unmanaged
        {
            return lane.ExpectedBufferID != 0u &&
                   lane.Handle.BufferID == lane.ExpectedBufferID &&
                   lane.Handle.Generation != 0u &&
                   lane.Length > 0;
        }

        private bool TryReadExistingLane<T>(
            ref VaultLane<T> lane,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : unmanaged
        {
            buffer = default;
            IDataVault vault = _vault;
            if (vault == null ||
                requiredLength <= 0 ||
                !TryBindExistingLane(ref lane, bufferId, requiredLength) ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (!vault.TryReadOnlyHandle(in lane.Handle, out buffer))
                return false;

            return !vault.IsCompactionFenceActive && buffer.IsCreated && buffer.Length >= lane.Length;
        }

        private bool TryAcquireExistingLaneWrite<T>(
            ref VaultLane<T> lane,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer,
            out IDataVault writeVault) where T : unmanaged
        {
            buffer = default;
            writeVault = null;
            IDataVault vault = _vault;
            if (vault == null ||
                requiredLength <= 0 ||
                !TryBindExistingLane(ref lane, bufferId, requiredLength) ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in lane.Handle, OwnerSystem, out buffer))
                return false;

            bool releaseOnExit = true;
            try
            {
                if (!vault.IsCompactionFenceActive && buffer.IsCreated && buffer.Length >= lane.Length)
                {
                    writeVault = vault;
                    releaseOnExit = false;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (releaseOnExit)
                    vault.ReleaseWriteLock(in lane.Handle, OwnerSystem);
            }
        }

        private bool TryAcquireOrCreateLaneWrite<T>(
            ref VaultLane<T> lane,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer,
            out IDataVault writeVault) where T : unmanaged
        {
            buffer = default;
            writeVault = null;
            IDataVault vault = _vault;
            if (!TryBindOrAcquireLane(vault, ref lane, bufferId, requiredLength, options) ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in lane.Handle, OwnerSystem, out buffer))
                return false;

            bool releaseOnExit = true;
            try
            {
                if (!vault.IsCompactionFenceActive && buffer.IsCreated && buffer.Length >= lane.Length)
                {
                    writeVault = vault;
                    releaseOnExit = false;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (releaseOnExit)
                    vault.ReleaseWriteLock(in lane.Handle, OwnerSystem);
            }
        }

        private bool TryAcquireStateWriteBuffer(out NativeArray<CompassStateDTO> buffer, out IDataVault writeVault)
        {
            return TryAcquireExistingLaneWrite(ref _stateLane, BufferID.CompassState, StateLength, out buffer, out writeVault);
        }

        private void ReleaseStateWriteBuffer(IDataVault writeVault)
        {
            writeVault?.ReleaseWriteLock(in _stateLane.Handle, OwnerSystem);
        }

        private bool TryAcquireOutputWriteBuffer(out NativeArray<float> buffer, out IDataVault writeVault)
        {
            return TryAcquireExistingLaneWrite(
                ref _headingOutputLane,
                BufferID.CompassHeadingOutput,
                (int)CompassOutputSlot.Count,
                out buffer,
                out writeVault);
        }

        private void ReleaseOutputWriteBuffer(IDataVault writeVault)
        {
            writeVault?.ReleaseWriteLock(in _headingOutputLane.Handle, OwnerSystem);
        }

        private bool TryAcquireBlackBoxWriteBuffer(out NativeArray<CompassBlackBoxEntry> buffer, out IDataVault writeVault)
        {
            return TryAcquireExistingLaneWrite(
                ref _blackBoxLane,
                BufferID.CompassBlackBox,
                BlackBoxCapacity,
                out buffer,
                out writeVault);
        }

        private void ReleaseBlackBoxWriteBuffer(IDataVault writeVault)
        {
            writeVault?.ReleaseWriteLock(in _blackBoxLane.Handle, OwnerSystem);
        }

        private bool TryAcquirePresentationWriteBuffer(out NativeArray<CompassPresentationStateDTO> buffer, out IDataVault writeVault)
        {
            return TryAcquireExistingLaneWrite(
                ref _presentationLane,
                BufferID.CompassPresentationState,
                StateLength,
                out buffer,
                out writeVault);
        }

        private bool TryAcquireOrCreatePresentationWriteBuffer(out NativeArray<CompassPresentationStateDTO> buffer, out IDataVault writeVault)
        {
            return TryAcquireOrCreateLaneWrite(
                ref _presentationLane,
                BufferID.CompassPresentationState,
                StateLength,
                NativeArrayOptions.ClearMemory,
                out buffer,
                out writeVault);
        }

        private void ReleasePresentationWriteBuffer(IDataVault writeVault)
        {
            writeVault?.ReleaseWriteLock(in _presentationLane.Handle, OwnerSystem);
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
                    IDataVault nextVault = currentService is IDataVault dataVault ? dataVault : null;
                    RebindDataVaultForLifecycle(nextVault);
                    break;
            }
        }

        private void RebindDataVaultForLifecycle(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
            {
                TryResolveVaultBuffers();
                return;
            }

            ClearVaultLanes();
            _vault = vault;
            _fastCadenceAccumulatedDelta = 0f;
            _fastCadenceCounter = 0;

            TryResolveVaultBuffers();
            ResetPresentationState(resetDialMatrix: true);
        }

        private void ClearVaultLanes()
        {
            _stateLane = default;
            _presentationLane = default;
            _headingOutputLane = default;
            _blackBoxLane = default;
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
            _visualOverkillWeight01 = ResolveVisualOverkillWeight01(_qualityWeight01);
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
            if (!Application.isPlaying)
                return;

            if (!_registeredSlowTick)
                _registeredSlowTick = SystemDispatcher.Register((ISlowTickable)this, PriorityLayer.UI);
            if (!_registeredLateFrame)
                _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void TryUnregisterTickables()
        {
            if (_registeredSlowTick)
            {
                SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrame)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }
        }

        private bool RefreshFastSignalInputs(out CompassStateDTO state)
        {
            state = default;
            if (!TryGetExistingStateBuffer(out var stateBuffer))
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

            return TryCommitCompassState(in state);
        }

        private void ApplyQueuedManualRecalibration(ref CompassStateDTO state)
        {
            if (_manualRecalibrationRequested)
            {
                state.Flags |= FlagCalibrationRequested;
                state.RecalibrationHold01 = math.max(state.RecalibrationHold01, 1f);
                _manualRecalibrationRequested = false;
                _manualRecalibrationHold01 = 0f;
                return;
            }

            if (_manualRecalibrationHold01 <= 0f)
                return;

            state.RecalibrationHold01 = math.max(state.RecalibrationHold01, _manualRecalibrationHold01);
            if (state.RecalibrationHold01 >= 1f)
                state.Flags |= FlagCalibrationRequested;

            _manualRecalibrationHold01 = 0f;
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
            if (!TryReadCompassState(out CompassStateDTO state))
                return;

            if (!TryResolvePose(out PlayerRuntimePoseSnapshot pose))
                return;

            SanitizeFiniteState(ref state);
            double3 actualAup = pose.Aup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(actualAup)))
            {
                state.Flags |= FlagNonFiniteFallback;
                if (TryCommitCompassState(in state))
                    QueueBlackBoxDump(state.BlackBoxCursor);
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
            if ((state.Flags & FlagPowered) == 0u && state.Power01 >= PowerDeathThreshold01)
                state.CurrentHeadingDegrees = actualHeading;

            ApplyQueuedManualRecalibration(ref state);

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

            ResolveDriftStep(
                ref state,
                deltaTime,
                state.NoiseClockSeconds,
                headingCatchupRate,
                driftNoiseFrequency,
                anomalyNoiseDegrees,
                wildSpinDegreesPerSecond,
                state.CalibrationCount,
                resetDrift,
                out float currentHeading,
                out float outputActualHeading,
                out float drift,
                out float anomaly,
                out float power,
                out float glitch,
                out float maxDrift,
                out float cardinalIndex);

            int blackBoxCursor = ResolveBlackBoxCursor(state.BlackBoxCursor);
            CompassBlackBoxEntry blackBoxEntry = CreateBlackBoxEntry(in state);
            state.BlackBoxCursor = AdvanceBlackBoxCursor(blackBoxCursor);

            if (!TryCommitCompassState(in state))
                return;

            TryCommitCompassOutput(
                currentHeading,
                outputActualHeading,
                drift,
                anomaly,
                power,
                glitch,
                cardinalIndex,
                maxDrift);
            TryCommitBlackBoxEntry(in blackBoxEntry, blackBoxCursor);
        }

        private bool TryResolvePose(out PlayerRuntimePoseSnapshot pose)
        {
            IPlayerRuntimeContext playerContext = _playerContext;
            if (playerContext != null && playerContext.TryGetPlayerPoseSnapshot(out pose))
                return true;

            pose = default;
            return false;
        }

        private bool TryCommitCompassState(in CompassStateDTO state)
        {
            if (!TryAcquireStateWriteBuffer(out NativeArray<CompassStateDTO> stateBuffer, out IDataVault writeVault))
                return false;

            try
            {
                stateBuffer[0] = state;
                return true;
            }
            finally
            {
                ReleaseStateWriteBuffer(writeVault);
            }
        }

        private bool TryCommitCompassOutput(
            float currentHeading,
            float actualHeading,
            float drift,
            float anomaly,
            float power,
            float glitch,
            float cardinalIndex,
            float maxDrift)
        {
            if (!TryAcquireOutputWriteBuffer(out NativeArray<float> outputBuffer, out IDataVault writeVault))
                return false;

            try
            {
                outputBuffer[(int)CompassOutputSlot.CurrentHeadingDegrees] = currentHeading;
                outputBuffer[(int)CompassOutputSlot.ActualHeadingDegrees] = actualHeading;
                outputBuffer[(int)CompassOutputSlot.DriftDegrees] = drift;
                outputBuffer[(int)CompassOutputSlot.AnomalyInterference01] = anomaly;
                outputBuffer[(int)CompassOutputSlot.Power01] = power;
                outputBuffer[(int)CompassOutputSlot.Glitch01] = glitch;
                outputBuffer[(int)CompassOutputSlot.CardinalIndex] = cardinalIndex;
                outputBuffer[(int)CompassOutputSlot.MaxGyroDriftDegrees] = maxDrift;
                return true;
            }
            finally
            {
                ReleaseOutputWriteBuffer(writeVault);
            }
        }

        private bool TryCommitBlackBoxEntry(in CompassBlackBoxEntry entry, int index)
        {
            if (index < 0 || index >= BlackBoxCapacity ||
                !TryAcquireBlackBoxWriteBuffer(out NativeArray<CompassBlackBoxEntry> blackBox, out IDataVault writeVault))
            {
                return false;
            }

            try
            {
                blackBox[index] = entry;
                return true;
            }
            finally
            {
                ReleaseBlackBoxWriteBuffer(writeVault);
            }
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
            if (!TryGetExistingStateBuffer(out var stateBuffer) ||
                !TryReadExistingLane(
                    ref _headingOutputLane,
                    BufferID.CompassHeadingOutput,
                    (int)CompassOutputSlot.Count,
                    out NativeArray<float>.ReadOnly outputBuffer) ||
                !TryGetPresentationBuffer(out var presentationBuffer))
            {
                return;
            }

            CompassStateDTO state = stateBuffer[0];
            if (SanitizeFiniteState(ref state))
                TryCommitCompassState(in state);

            CompassPresentationStateDTO presentation = presentationBuffer[0];
            float power = SanitizeUnit01(outputBuffer[(int)CompassOutputSlot.Power01]);
            float heading = NormalizeHeading(outputBuffer[(int)CompassOutputSlot.CurrentHeadingDegrees]);
            float anomaly = SanitizeUnit01(outputBuffer[(int)CompassOutputSlot.AnomalyInterference01]);
            bool powered = power >= PowerDeathThreshold01;
            int cardinalIndex = powered ? ResolveCardinalIndex(heading) : -1;
            bool presentationDirty = ApplyCardinalText(cardinalIndex, powered, ref presentation);
            float visualOverkillWeight = ResolvePresentationVisualOverkillWeight01(in state);
            float presentedHeading = ResolveVisualDialHeading(heading, anomaly, visualOverkillWeight, state.NoiseClockSeconds);

            bool shouldDrawIndirect = ShouldDrawIndirectDial();
            bool shouldApplyHeading = ShouldApplyDialHeading(presentedHeading, in presentation);
            if (powered && (shouldDrawIndirect || shouldApplyHeading))
            {
                presentationDirty |= ApplyDialHeading(presentedHeading, ref presentation);
                if (shouldApplyHeading)
                {
                    presentation.LastPresentedHeadingDegrees = presentedHeading;
                    presentation.PresentationFlags |= PresentationFlagDialInitialized;
                    presentationDirty = true;
                }
            }

            float chromatic = powered && anomaly > 0.8f ? math.saturate((anomaly - 0.8f) * 5f) : 0f;
            float overkill = powered ? math.saturate(anomaly * 1.35f * visualOverkillWeight) : 0f;
            presentationDirty |= ApplyChromatic(chromatic, power, overkill, ref presentation);
            presentationDirty |= EmitVisualOverkillFailureParticles(powered, anomaly, visualOverkillWeight, ref presentation);

            if (!presentationDirty ||
                !TryAcquirePresentationWriteBuffer(out NativeArray<CompassPresentationStateDTO> writeBuffer, out IDataVault writeVault))
            {
                return;
            }

            try
            {
                writeBuffer[0] = presentation;
            }
            finally
            {
                ReleasePresentationWriteBuffer(writeVault);
            }
        }

        private bool ShouldApplyDialHeading(float heading, in CompassPresentationStateDTO presentation)
        {
            return (presentation.PresentationFlags & PresentationFlagDialInitialized) == 0u ||
                   DeltaHeadingAbs(presentation.LastPresentedHeadingDegrees, heading) > HeadingEpsilon;
        }

        private bool ApplyDialHeading(float heading, ref CompassPresentationStateDTO presentation)
        {
            float safeHeading = NormalizeHeading(heading);
            if (ShouldDrawIndirectDial())
            {
                return DrawIndirectDial(safeHeading, ref presentation);
            }

            if (dialPivot == null)
                return false;

            dialPivot.localRotation = ApproximateRotationDegreesNoTrig(NormalizeHeading(safeHeading + dialDegreesOffset), Vector3.up);
            return true;
        }

        private bool ShouldDrawIndirectDial()
        {
            return enableIndirectVisualRoute &&
                   HasIndirectBuffersReady() &&
                   dialMesh != null &&
                   dialIndirectMaterial != null &&
                   _supportsIndirectDialCold;
        }

        private bool DrawIndirectDial(float heading, ref CompassPresentationStateDTO presentation)
        {
            Transform source = dialPivot != null ? dialPivot : (toolRoot != null ? toolRoot : transform);
            float resolvedHeading = NormalizeHeading(heading + dialDegreesOffset);
            Vector3 position = source.position;
            Quaternion rotation = source.rotation * ApproximateRotationDegreesNoTrig(resolvedHeading, Vector3.up);
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
                _activeIndirectArgsBuffer,
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
            try
            {
                UnsafeUtility.MemCpy(mapped.GetUnsafePtr(), UnsafeUtility.AddressOf(ref matrix), DialMatrixStrideBytes);
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<Matrix4x4>(1);
            }

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

        private bool EmitVisualOverkillFailureParticles(
            bool powered,
            float anomaly,
            float visualOverkillWeight01,
            ref CompassPresentationStateDTO presentation)
        {
            float anomalyWeight = powered ? SmoothStep01((anomaly - 0.8f) * 5f) : 0f;
            float emissionWeight = math.saturate(anomalyWeight * visualOverkillWeight01);
            if (!powered ||
                emissionWeight <= 0f ||
                anomalyFailureParticles == null ||
                anomalyParticleBurst <= 0)
            {
                if (presentation.ParticleDebt == 0f)
                    return false;

                presentation.ParticleDebt = 0f;
                return true;
            }

            int burst = math.clamp(
                (int)math.round(math.min(anomalyParticleBurst, MaxAnomalyParticleBurst) * emissionWeight),
                0,
                MaxAnomalyParticleBurst);
            if (burst <= 0)
            {
                presentation.ParticleDebt = 0f;
                return true;
            }

            float debt = presentation.ParticleDebt + emissionWeight * burst;
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

        private bool HasIndirectBuffersReady()
        {
            return IsValidBuffer(_activeIndirectArgsBuffer) &&
                   IsValidBuffer(_indirectArgsBufferA) &&
                   IsValidBuffer(_indirectArgsBufferB) &&
                   IsValidBuffer(_dialMatrixBufferA) &&
                   IsValidBuffer(_dialMatrixBufferB);
        }

        private bool ShouldRequireIndirectBuffersCold()
        {
            return enableIndirectVisualRoute &&
                   dialMesh != null &&
                   dialIndirectMaterial != null &&
                   _supportsIndirectDialCold;
        }

        private void FlushIndirectBuffersRepairSlow()
        {
            CacheGraphicsCapabilitiesCold();
            if (!ShouldRequireIndirectBuffersCold())
            {
                if (HasIndirectBuffersReady())
                    ReleaseIndirectBuffers();

                _indirectBuffersDirty = false;
                return;
            }

            EnsureIndirectBuffersCold();
            _indirectBuffersDirty = !HasIndirectBuffersReady();
        }

        private void EnsureIndirectBuffersCold()
        {
            if (!ShouldRequireIndirectBuffersCold())
            {
                ReleaseIndirectBuffers();
                return;
            }

            if (HasIndirectBuffersReady())
            {
                return;
            }

            ReleaseIndirectBuffers();
            _indirectArgs[0] = dialMesh.GetIndexCount(0);
            _indirectArgs[1] = 1u;
            _indirectArgs[2] = dialMesh.GetIndexStart(0);
            _indirectArgs[3] = dialMesh.GetBaseVertex(0);
            _indirectArgs[4] = 0u;
            _indirectArgsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, sizeof(uint) * _indirectArgs.Length); // COLD ALLOC: GraphicsBuffer[1] - compass indirect args A - owner: DiegeticGyroCompassRuntime
            _indirectArgsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, sizeof(uint) * _indirectArgs.Length); // COLD ALLOC: GraphicsBuffer[1] - compass indirect args B - owner: DiegeticGyroCompassRuntime
            _dialMatrixBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, DialMatrixStrideBytes); // COLD ALLOC: GraphicsBuffer[1] - compass dial matrix buffer A - owner: DiegeticGyroCompassRuntime
            _dialMatrixBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, DialMatrixStrideBytes); // COLD ALLOC: GraphicsBuffer[1] - compass dial matrix buffer B - owner: DiegeticGyroCompassRuntime
            _indirectArgsUploadBufferIndex = 0;
            UploadIndirectArgs();
            MarkDialMatrixPresentationDirty();
        }

        private void ReleaseIndirectBuffers()
        {
            ReleaseGraphicsBuffer(ref _indirectArgsBufferA);
            ReleaseGraphicsBuffer(ref _indirectArgsBufferB);
            _activeIndirectArgsBuffer = null;
            _indirectArgsUploadBufferIndex = 0;
            ReleaseGraphicsBuffer(ref _dialMatrixBufferA);
            ReleaseGraphicsBuffer(ref _dialMatrixBufferB);
            _publishedDialMatrixBuffer = null;
            _boundDialMatrixBuffer = null;
            MarkDialMatrixPresentationDirty();
        }

        private unsafe void UploadIndirectArgs()
        {
            GraphicsBuffer writeBuffer = (_indirectArgsUploadBufferIndex & 1) == 0
                ? _indirectArgsBufferA
                : _indirectArgsBufferB;
            if (!IsValidBuffer(writeBuffer))
                return;

            var mapped = writeBuffer.LockBufferForWrite<uint>(0, _indirectArgs.Length);
            try
            {
                fixed (uint* source = _indirectArgs)
                {
                    UnsafeUtility.MemCpy(mapped.GetUnsafePtr(), source, sizeof(uint) * _indirectArgs.Length);
                }
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<uint>(_indirectArgs.Length);
            }

            _activeIndirectArgsBuffer = writeBuffer;
            _indirectArgsUploadBufferIndex ^= 1;
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

        private void QueueBlackBoxDump(int blackBoxCursor)
        {
            if (_blackBoxDumped)
                return;

            _queuedBlackBoxCursor = blackBoxCursor;
            _blackBoxDumpQueued = true;
        }

        private void FlushQueuedBlackBoxDump()
        {
            if (!_blackBoxDumpQueued)
                return;

            _blackBoxDumpQueued = false;
            DumpBlackBoxOnce(_queuedBlackBoxCursor);
        }

        private unsafe void DumpBlackBoxOnce(int blackBoxCursor)
        {
            if (_blackBoxDumped || !IsLaneBound(in _blackBoxLane))
            {
                _blackBoxDumpQueued = !_blackBoxDumped;
                return;
            }

            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string path = Path.Combine(projectRoot, "Docs", "AgentLogs", DumpFileName);
                const int headerBytes = 12;
                const int rowBytes = 64;
                int byteCount = headerBytes + BlackBoxCapacity * rowBytes;
                NativeArray<byte> payload = default;
                try
                {
                    payload = NativeFaultDumpWriter.CreateTransientPayload(
                        byteCount,
                        nameof(DiegeticGyroCompassRuntime),
                        DumpPayloadLabel,
                        NativeArrayOptions.UninitializedMemory);
                    byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                    int cursor = blackBoxCursor;
                    if (cursor < 0 || cursor >= BlackBoxCapacity)
                        cursor = 0;

                    Span<byte> header = new Span<byte>(destination, headerBytes);
                    BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(0, 4), DumpMagic);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4, 4), BlackBoxCapacity);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(8, 4), cursor);

                    for (int i = 0; i < BlackBoxCapacity; i++)
                    {
                        int index = cursor + i;
                        if (index >= BlackBoxCapacity)
                            index -= BlackBoxCapacity;

                        if (!TryReadBlackBoxEntry(index, out CompassBlackBoxEntry entry))
                        {
                            _blackBoxDumpQueued = true;
                            return;
                        }

                        Span<byte> row = new Span<byte>(destination + headerBytes + i * rowBytes, rowBytes);
                        WriteCompassBlackBoxEntry(row, in entry);
                    }

                    if (NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount))
                        _blackBoxDumped = true;
                    else
                        _blackBoxDumpQueued = true;
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(DiegeticGyroCompassRuntime),
                        DumpPayloadLabel);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        private bool TryReadBlackBoxEntry(int index, out CompassBlackBoxEntry entry)
        {
            entry = default;
            if (index < 0 ||
                index >= BlackBoxCapacity ||
                !TryReadExistingLane(
                    ref _blackBoxLane,
                    BufferID.CompassBlackBox,
                    BlackBoxCapacity,
                    out NativeArray<CompassBlackBoxEntry>.ReadOnly blackBox) ||
                blackBox.Length <= index)
            {
                return false;
            }

            entry = blackBox[index];
            return true;
        }

        private static void WriteCompassBlackBoxEntry(Span<byte> destination, in CompassBlackBoxEntry entry)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            WriteFloatLittleEndian(destination.Slice(4, 4), entry.ActualHeadingDegrees);
            WriteFloatLittleEndian(destination.Slice(8, 4), entry.CurrentHeadingDegrees);
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.DriftDegrees);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.MaxGyroDriftDegrees);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.AnomalyInterference01);
            WriteFloatLittleEndian(destination.Slice(24, 4), entry.Power01);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(28, 4), entry.Flags);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(32, 4), entry.LastAupShiftFrameId);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(36, 4), entry.CalibrationCount);
            BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(40, 8), 0ul);
            BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(48, 8), 0ul);
            BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(56, 8), 0ul);
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(value));
        }

        private static bool SupportsIndirectDialCold()
        {
            GraphicsDeviceType deviceType = SystemInfo.graphicsDeviceType;
            if (deviceType == GraphicsDeviceType.OpenGLES3)
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

            float heading = math.degrees(MathLodApproximation.ApproxAtan2Fast(forward.x, forward.z));
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

        private static Quaternion ApproximateRotationDegreesNoTrig(float angleDegrees, Vector3 normalizedAxis)
        {
            ApproximateSinCosFullNoTrig(angleDegrees * DegreesToRadians * 0.5f, out float sinHalf, out float cosHalf);
            Quaternion rotation = new Quaternion(
                normalizedAxis.x * sinHalf,
                normalizedAxis.y * sinHalf,
                normalizedAxis.z * sinHalf,
                cosHalf);
            return NormalizeQuaternionNoSqrt(rotation);
        }

        private static void ApproximateSinCosFullNoTrig(float radians, out float sin, out float cos)
        {
            float x = radians - (TwoPi * math.round(radians / TwoPi));
            float cosSign = 1f;
            if (x > HalfPi)
            {
                x = Pi - x;
                cosSign = -1f;
            }
            else if (x < -HalfPi)
            {
                x = -Pi - x;
                cosSign = -1f;
            }

            float x2 = x * x;
            sin = x * (1f - (x2 * (0.16666667f - (x2 * 0.008333333f))));
            cos = cosSign * (1f - (x2 * (0.5f - (x2 * 0.041666667f))));
        }

        private static Quaternion NormalizeQuaternionNoSqrt(Quaternion value)
        {
            float4 q = new float4(value.x, value.y, value.z, value.w);
            float lengthSq = math.lengthsq(q);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return Quaternion.identity;

            q *= math.rsqrt(lengthSq);
            return new Quaternion(q.x, q.y, q.z, q.w);
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

        private static void ResolveDriftStep(
            ref CompassStateDTO state,
            float deltaTime,
            float noiseTime,
            float catchupRateSource,
            float noiseFrequencySource,
            float noiseDegreesSource,
            float wildSpinRateSource,
            int calibrationCount,
            int resetDrift,
            out float currentHeading,
            out float actualHeading,
            out float drift,
            out float anomaly,
            out float power,
            out float glitch,
            out float maxDrift,
            out float cardinalIndex)
        {
            float safeDeltaTime = SanitizeDeltaTime(deltaTime);
            float catchupRate = SanitizeNonNegative(catchupRateSource);
            float noiseFrequency = SanitizeNonNegative(noiseFrequencySource);
            float noiseDegrees = SanitizeNonNegative(noiseDegreesSource);
            float wildSpinRate = SanitizeNonNegative(wildSpinRateSource);
            actualHeading = NormalizeHeading(state.ActualHeadingDegrees);
            currentHeading = (state.Flags & FlagInitialized) != 0u
                ? NormalizeHeading(state.CurrentHeadingDegrees)
                : actualHeading;
            power = SanitizeUnit01(state.Power01);
            anomaly = SanitizeUnit01(state.AnomalyInterference01);

            uint flags = state.Flags | FlagInitialized;
            flags &= ~FlagCalibrationApplied;
            flags = power >= PowerDeathThreshold01 ? flags | FlagPowered : flags & ~FlagPowered;
            flags = anomaly > 0.8f ? flags | FlagAnomalyUnstable : flags & ~FlagAnomalyUnstable;

            if (resetDrift != 0)
            {
                currentHeading = actualHeading;
                flags |= FlagCalibrationApplied;
            }
            else if (power >= PowerDeathThreshold01)
            {
                float headingDelta = DeltaAngleDegrees(currentHeading, actualHeading);
                float alpha = SanitizeUnit01(catchupRate * safeDeltaTime);
                float noiseValue = ResolveNoiseValue(noiseTime, noiseFrequency, flags);
                currentHeading += headingDelta * alpha;
                currentHeading += noiseValue * noiseDegrees * anomaly * safeDeltaTime;
                if (anomaly > 0.8f)
                {
                    float spinSign = noiseValue < 0f ? -1f : 1f;
                    currentHeading += spinSign * wildSpinRate * anomaly * safeDeltaTime;
                }
            }

            currentHeading = NormalizeHeading(currentHeading);
            drift = DeltaAngleDegrees(actualHeading, currentHeading);
            maxDrift = math.max(math.abs(state.MaxGyroDriftDegrees), math.abs(drift));
            glitch = SanitizeUnit01(anomaly * 1.25f + SanitizeUnit01(math.abs(drift) * (1f / 90f)) * 0.25f);

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
            state.CalibrationCount = calibrationCount;
            state.Flags = flags;
            cardinalIndex = ResolveCardinalIndex(currentHeading);
        }

        private static float DeltaAngleDegrees(float from, float to)
        {
            float delta = NormalizeHeading(to) - NormalizeHeading(from);
            delta = math.fmod(delta + 540f, 360f) - 180f;
            return math.isfinite(delta) ? delta : 0f;
        }

        private static float ResolveNoiseValue(float noiseTime, float noiseFrequency, uint flags)
        {
            if (!math.isfinite(noiseTime) || !math.isfinite(noiseFrequency))
                return 0f;

            float t = noiseTime * noiseFrequency;
            if ((flags & FlagReducedQualityNoise) != 0u)
                return TriangleNoise(t);

            return TriangleNoise(t + 0.371f);
        }

        private static float TriangleNoise(float t)
        {
            if (!math.isfinite(t))
                return 0f;

            float phase = math.frac(t);
            return 1f - math.abs(phase * 4f - 2f);
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static int ResolveBlackBoxCursor(int cursor)
        {
            return cursor >= 0 && cursor < BlackBoxCapacity ? cursor : 0;
        }

        private static int AdvanceBlackBoxCursor(int cursor)
        {
            cursor++;
            return cursor >= BlackBoxCapacity ? 0 : cursor;
        }

        private static CompassBlackBoxEntry CreateBlackBoxEntry(in CompassStateDTO state)
        {
            return new CompassBlackBoxEntry
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
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(math.isfinite(value) ? value : 1f);
            return t * t * (3f - 2f * t);
        }

        private static float ResolveVisualOverkillWeight01(float qualityWeight01)
        {
            float quality = SmoothStep01(qualityWeight01);
            return math.saturate(quality * quality * math.lerp(0.5f, 1f, quality));
        }

        private float ResolvePresentationVisualOverkillWeight01(in CompassStateDTO state)
        {
            float stressHeadroom = 1f - SmoothStep01(state.SystemStress01);
            return math.saturate(_visualOverkillWeight01 * stressHeadroom);
        }

        private static float ResolveVisualDialHeading(
            float heading,
            float anomaly,
            float visualOverkillWeight01,
            float noiseClockSeconds)
        {
            float anomalyWeight = SmoothStep01((anomaly - 0.55f) * 2.2222223f);
            float wobbleDegrees = 1.75f * anomalyWeight * visualOverkillWeight01;
            if (wobbleDegrees <= 0f)
                return NormalizeHeading(heading);

            float wobble = TriangleVisualNoise(noiseClockSeconds * 1.73f + 0.27f);
            return NormalizeHeading(heading + wobble * wobbleDegrees);
        }

        private static float TriangleVisualNoise(float t)
        {
            if (!math.isfinite(t))
                return 0f;

            float phase = math.frac(t);
            return 1f - math.abs(phase * 4f - 2f);
        }
    }
}
