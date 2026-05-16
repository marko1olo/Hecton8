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
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.UI.Navigation
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]
    public struct CompassBlackBoxEntry
    {
        public uint Frame;
        public float ActualHeadingDegrees;
        public float CurrentHeadingDegrees;
        public float DriftDegrees;
        public float MaxGyroDriftDegrees;
        public float AnomalyInterference01;
        public float Power01;
        public uint Flags;
        public uint LastAupShiftFrameId;
        public int CalibrationCount;
    }

    public static class DiegeticCompassSignals
    {
        private const uint CompassCalibrationSourceHash = 0xC06A5511u;
        private const uint CompassAnomalySourceHash = 0xC06A5512u;

        public static void PublishCalibration(uint frame, float quality01)
        {
            CompassCalibratedSignal signal = new CompassCalibratedSignal
            {
                SourceHash = CompassCalibrationSourceHash,
                Frame = frame,
                CalibrationQuality01 = math.saturate(quality01),
                Flags = 1
            };
            SignalBus<CompassCalibratedSignal>.Push(in signal);
        }

        public static void PublishAnomalyProximity(in AbsoluteUniversePosition sourceAup, uint frame, float proximity01, float interference01)
        {
            AnomalyProximitySignal signal = new AnomalyProximitySignal
            {
                SourceAup = sourceAup,
                Proximity01 = math.saturate(proximity01),
                Interference01 = math.saturate(interference01),
                SourceHash = CompassAnomalySourceHash,
                Frame = frame,
                Flags = 1
            };
            SignalBus<AnomalyProximitySignal>.Push(in signal);
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Navigation/Diegetic Gyro Compass Runtime")]
    public sealed class DiegeticGyroCompassRuntime : MonoBehaviour, IInertialNavigationService, IFastTickable, ISlowTickable, ILateFrameTickable
    {
        private const int StateLength = 1;
        private const int BlackBoxCapacity = 300;
        private const float DefaultSlowDeltaSeconds = 0.1f;
        private const float MaxIntegrationDeltaSeconds = 0.2f;
        private const float PowerDeathThreshold01 = 0.01f;
        private const float StressSlowThreshold01 = 0.8f;
        private const float RecalibrationHoldSeconds = 3f;
        private const float HeadingEpsilon = 0.001f;
        private const float ChromaticEpsilon = 0.001f;
        private const float VelocityClampMetersPerSecond = 100000f;
        private const int MaxAnomalyParticleBurst = 128;
        private const uint DumpMagic = 0x4759434Fu;
        private const string DumpFileName = "Dump_COMPASS_GYRO_STABILIZER.bin";
        private const uint FlagInitialized = 1u << 0;
        private const uint FlagPowered = 1u << 1;
        private const uint FlagAnomalyUnstable = 1u << 2;
        private const uint FlagStressSlowCadence = 1u << 3;
        private const uint FlagCalibrationApplied = 1u << 4;
        private const uint FlagNonFiniteFallback = 1u << 5;
        private const uint FlagLowTier = 1u << 6;
        private const uint FlagIndirectDial = 1u << 7;

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
        private HectonQualityTier _cachedQualityTier = HectonQualityTier.Unknown;
        private JobHandle _jobHandle;
        private InertialNavigationSnapshot _snapshot;
        private double3 _lastActualAup;
        private float _suitPower01 = 1f;
        private float _anomalyInterference01;
        private float _systemStress01;
        private float _calibrationHold01;
        private float _noiseClock;
        private float _lastPresentedHeading = float.NaN;
        private float _lastChromatic = -1f;
        private float _lastPower = -1f;
        private float _lastOverkill = -1f;
        private float _particleDebt;
        private int _lastCardinalIndex = int.MinValue;
        private int _lastPowerState = int.MinValue;
        private int _blackBoxCursor;
        private int _calibrationCount;
        private uint _frameSequence;
        private uint _lastAupShiftFrameId;
        private bool _lowTier;
        private bool _hasLastActualAup;
        private bool _pendingCalibration;
        private bool _jobPending;
        private bool _registeredFastTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _registeredService;
        private bool _diegeticTextValid = true;
        private bool _blackBoxDumped;

        private readonly char[] _cardinalBuffer = new char[2]; // COLD ALLOC: char[2] - diegetic compass cardinal text buffer - owner: DiegeticGyroCompassRuntime
        private readonly uint[] _indirectArgs = new uint[5]; // COLD ALLOC: uint[5] - compass indirect draw args - owner: DiegeticGyroCompassRuntime
        private readonly Matrix4x4[] _dialMatrices = new Matrix4x4[1]; // COLD ALLOC: Matrix4x4[1] - compass indirect dial matrix - owner: DiegeticGyroCompassRuntime
        private ComputeBuffer _indirectArgsBuffer;
        private ComputeBuffer _dialMatrixBuffer;

        public InertialNavigationSnapshot Snapshot => _snapshot;

        public double3 EstimatedAUP => _snapshot.EstimatedAUP;

        public float GyroDriftError => _snapshot.GyroDriftError;

        private void Awake()
        {
            ValidateDiegeticTextBinding();
        }

        private void OnEnable()
        {
            ConfigureSignalLanes();
            ResolveColdDependencies();
            TryResolveVaultBuffers();
            TryRegisterService();
            TryRegisterTickables();
            EnsureIndirectBuffers();
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
            CompletePendingJob();
            TryUnregisterTickables();
            TryUnregisterService();
            ApplyChromatic(0f, 0f);
        }

        private void OnDestroy()
        {
            CompletePendingJob();
            ReleaseIndirectBuffers();
        }

        public bool TryGetSnapshot(out InertialNavigationSnapshot snapshot)
        {
            snapshot = _snapshot;
            return (_snapshot.Flags & FlagInitialized) != 0u;
        }

        public void RequestRecalibration()
        {
            _pendingCalibration = true;
            _calibrationHold01 = 1f;
        }

        public bool TryAccumulateRecalibrationHold(float deltaTime, out float progress01)
        {
            float safeDeltaTime = SanitizeDeltaTime(deltaTime);
            _calibrationHold01 = math.saturate(_calibrationHold01 + safeDeltaTime * math.rcp(RecalibrationHoldSeconds));
            progress01 = _calibrationHold01;
            if (_calibrationHold01 >= 1f)
                RequestRecalibration();

            return true;
        }

        public void CancelRecalibrationHold()
        {
            _calibrationHold01 = 0f;
        }

        /// <summary>
        /// Caches bootstrap-owned dependencies for the compass hot path.
        /// </summary>
        /// <param name="playerContext">Authoritative player pose/AUP provider.</param>
        /// <param name="vault">Global vault owner for compass state, output, and blackbox buffers.</param>
        /// <param name="qualityTier">Boot-time hardware quality tier.</param>
        /// <remarks>Call from bootstrap or tool installation only; tick paths do not poll the registry.</remarks>
        public void InjectDependencies(IPlayerRuntimeContext playerContext, IDataVault vault, HectonQualityTier qualityTier)
        {
            _playerContext = playerContext;
            _vault = vault;
            _cachedQualityTier = qualityTier;
            _lowTier = IsLowTier(qualityTier);
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
            _lastPresentedHeading = float.NaN;
            _lastCardinalIndex = int.MinValue;
            _lastPowerState = int.MinValue;
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
            _particleDebt = 0f;
        }

        public void FastTick(float deltaTime)
        {
            RefreshFastSignalInputs();
            if (!ShouldUseFastCadence())
                return;

            ScheduleDrift(SanitizeDeltaTime(deltaTime));
        }

        public void SlowTick()
        {
            if (_playerContext == null || _vault == null)
                return;

            RefreshFastSignalInputs();
            if (ShouldUseFastCadence())
                return;

            ScheduleDrift(DefaultSlowDeltaSeconds);
        }

        public void LateFrameTick()
        {
            CompletePendingJob();
            ApplyPresentation();
        }

        private void ConfigureSignalLanes()
        {
            GlobalSignals.InitializeAllQueues();
        }

        private void ResolveColdDependencies()
        {
            if (_playerContext == null)
                _playerContext = GlobalRegistry.Player;

            if (_vault == null)
                _vault = GlobalRegistry.DataVault;

            _cachedQualityTier = GlobalRegistry.ScalabilityTier;
            _lowTier = IsLowTier(_cachedQualityTier);
        }

        private bool TryResolveVaultBuffers()
        {
            return TryGetCompassBuffers(out _, out _, out _);
        }

        private bool TryGetCompassBuffers(
            out NativeArray<CompassStateDTO> stateBuffer,
            out NativeArray<float> outputBuffer,
            out NativeArray<CompassBlackBoxEntry> blackBox)
        {
            stateBuffer = default;
            outputBuffer = default;
            blackBox = default;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            stateBuffer = vault.GetBuffer<CompassStateDTO>(
                BufferID.CompassState,
                StateLength,
                SystemID.UI,
                NativeArrayOptions.ClearMemory);

            outputBuffer = vault.GetBuffer<float>(
                BufferID.CompassHeadingOutput,
                (int)CompassOutputSlot.Count,
                SystemID.UI,
                NativeArrayOptions.ClearMemory);

            blackBox = vault.GetBuffer<CompassBlackBoxEntry>(
                BufferID.CompassBlackBox,
                BlackBoxCapacity,
                SystemID.UI,
                NativeArrayOptions.ClearMemory);

            return stateBuffer.IsCreated &&
                   stateBuffer.Length >= StateLength &&
                   outputBuffer.IsCreated &&
                   outputBuffer.Length >= (int)CompassOutputSlot.Count &&
                   blackBox.IsCreated &&
                   blackBox.Length >= BlackBoxCapacity;
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

        private void RefreshFastSignalInputs()
        {
            ReadOnlySpan<AnomalyProximitySignal> anomalySignals = SignalBus<AnomalyProximitySignal>.GetFrameSnapshot();
            float anomaly = _anomalyInterference01 * 0.88f;
            for (int i = 0; i < anomalySignals.Length; i++)
            {
                ref readonly AnomalyProximitySignal signal = ref anomalySignals[i];
                float interference = math.max(signal.Proximity01, signal.Interference01);
                if (math.isfinite(interference))
                    anomaly = math.max(anomaly, math.saturate(interference));
            }

            _anomalyInterference01 = anomaly;

            ReadOnlySpan<CompassCalibratedSignal> calibrationSignals = SignalBus<CompassCalibratedSignal>.GetFrameSnapshot();
            if (calibrationSignals.Length > 0)
            {
                _pendingCalibration = true;
                _calibrationHold01 = 1f;
            }

            ReadOnlySpan<SurvivalVitalsChangedSignal> vitalsSignals = SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < vitalsSignals.Length; i++)
            {
                ref readonly SurvivalVitalsChangedSignal signal = ref vitalsSignals[i];
                if ((signal.Flags & SurvivalVitalsChangedSignalFlags.Energy) != 0u && math.isfinite(signal.Energy01))
                    _suitPower01 = math.saturate(signal.Energy01);
            }

            ReadOnlySpan<SystemHealthSignal> healthSignals = SignalBus<SystemHealthSignal>.GetFrameSnapshot();
            for (int i = 0; i < healthSignals.Length; i++)
            {
                ref readonly SystemHealthSignal signal = ref healthSignals[i];
                if (math.isfinite(signal.SystemHealthIndex01))
                    _systemStress01 = math.saturate(signal.SystemHealthIndex01);
            }

            ReadOnlySpan<AupShiftSignal> shiftSignals = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shiftSignals.Length; i++)
            {
                uint shiftFrame = shiftSignals[i].ShiftFrameId;
                if (IsNewerFrameId(shiftFrame, _lastAupShiftFrameId))
                    _lastAupShiftFrameId = shiftFrame;
            }
        }

        private bool ShouldUseFastCadence()
        {
            return !_lowTier &&
                   _systemStress01 <= StressSlowThreshold01 &&
                   _suitPower01 >= PowerDeathThreshold01;
        }

        private void ScheduleDrift(float deltaTime)
        {
            if (_jobPending ||
                !TryGetCompassBuffers(
                    out NativeArray<CompassStateDTO> stateBuffer,
                    out NativeArray<float> outputBuffer,
                    out NativeArray<CompassBlackBoxEntry> blackBox))
            {
                return;
            }

            if (!TryResolvePose(out PlayerRuntimePoseSnapshot pose))
                return;

            CompassStateDTO state = stateBuffer[0];
            double3 actualAup = pose.Aup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(actualAup)))
            {
                state.Flags |= FlagNonFiniteFallback;
                stateBuffer[0] = state;
                DumpBlackBoxOnce(blackBox);
                return;
            }

            float actualHeading = ResolveHeadingFromForward(pose.Forward, state.ActualHeadingDegrees);
            state.ActualAUP = actualAup;
            state.RawEstimatedAUP = actualAup;
            state.EstimatedAUP = actualAup;
            state.Velocity = ResolveVelocity(actualAup, deltaTime);
            state.ActualHeadingDegrees = actualHeading;
            state.AnomalyInterference01 = _anomalyInterference01;
            state.Power01 = _suitPower01;
            state.RecalibrationHold01 = _calibrationHold01;
            state.DeltaSeconds = deltaTime;
            _frameSequence++;
            state.Frame = _frameSequence;
            state.LastAupShiftFrameId = _lastAupShiftFrameId;
            state.Flags |= FlagInitialized;
            state.Flags = _lowTier ? state.Flags | FlagLowTier : state.Flags & ~FlagLowTier;
            state.Flags = ShouldUseFastCadence() ? state.Flags & ~FlagStressSlowCadence : state.Flags | FlagStressSlowCadence;
            state.Flags = ShouldUseVisualOverkill() ? state.Flags | FlagIndirectDial : state.Flags & ~FlagIndirectDial;
            if ((state.Flags & FlagPowered) == 0u && _suitPower01 >= PowerDeathThreshold01)
                state.CurrentHeadingDegrees = actualHeading;

            stateBuffer[0] = state;
            int resetDrift = _pendingCalibration ? 1 : 0;
            if (_pendingCalibration)
            {
                _pendingCalibration = false;
                _calibrationHold01 = 0f;
                _calibrationCount++;
            }

            _noiseClock += deltaTime;
            if (!math.isfinite(_noiseClock) || _noiseClock > 100000f)
                _noiseClock = 0f;

            GyroDriftJob job = new GyroDriftJob
            {
                State = stateBuffer,
                Output = outputBuffer,
                DeltaSeconds = deltaTime,
                NoiseTime = _noiseClock,
                HeadingCatchupRate = headingCatchupRate,
                DriftNoiseFrequency = driftNoiseFrequency,
                AnomalyNoiseDegrees = anomalyNoiseDegrees,
                WildSpinDegreesPerSecond = wildSpinDegreesPerSecond,
                CalibrationCount = _calibrationCount,
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

        private void CompletePendingJob()
        {
            if (!_jobPending)
                return;

            _jobHandle.Complete();
            _jobPending = false;
            CommitCompletedState();
        }

        private void CommitCompletedState()
        {
            if (!TryGetCompassBuffers(
                    out NativeArray<CompassStateDTO> stateBuffer,
                    out _,
                    out NativeArray<CompassBlackBoxEntry> blackBox))
            {
                return;
            }

            CompassStateDTO state = stateBuffer[0];
            if (!IsFiniteState(in state))
            {
                state.Flags |= FlagNonFiniteFallback;
                stateBuffer[0] = state;
                DumpBlackBoxOnce(blackBox);
            }

            _snapshot = new InertialNavigationSnapshot
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

            WriteBlackBox(in state, blackBox);
        }

        private void ApplyPresentation()
        {
            if (!TryGetCompassBuffers(out _, out NativeArray<float> outputBuffer, out _))
                return;

            float power = outputBuffer[(int)CompassOutputSlot.Power01];
            float heading = outputBuffer[(int)CompassOutputSlot.CurrentHeadingDegrees];
            float anomaly = outputBuffer[(int)CompassOutputSlot.AnomalyInterference01];
            bool powered = power >= PowerDeathThreshold01;
            int cardinalIndex = powered ? ResolveCardinalIndex(heading) : -1;
            ApplyCardinalText(cardinalIndex, powered);

            if (powered && math.abs(heading - _lastPresentedHeading) > HeadingEpsilon)
            {
                ApplyDialHeading(heading);
                _lastPresentedHeading = heading;
            }

            float chromatic = powered && anomaly > 0.8f ? math.saturate((anomaly - 0.8f) * 5f) : 0f;
            float overkill = powered && ShouldUseVisualOverkill() ? math.saturate(anomaly * 1.35f) : 0f;
            ApplyChromatic(chromatic, power, overkill);
            EmitHighTierFailureParticles(powered, anomaly);
        }

        private void ApplyDialHeading(float heading)
        {
            if (ShouldDrawIndirectDial())
            {
                DrawIndirectDial(heading);
                return;
            }

            if (dialPivot == null)
                return;

            dialPivot.localRotation = Quaternion.AngleAxis(heading + dialDegreesOffset, Vector3.up);
        }

        private bool ShouldDrawIndirectDial()
        {
            return enableIndirectHighTier &&
                   _indirectArgsBuffer != null &&
                   _dialMatrixBuffer != null &&
                   dialMesh != null &&
                   dialIndirectMaterial != null &&
                   SupportsIndirectDial() &&
                   _cachedQualityTier >= HectonQualityTier.High &&
                   _systemStress01 <= StressSlowThreshold01;
        }

        private bool ShouldUseVisualOverkill()
        {
            return _cachedQualityTier >= HectonQualityTier.High &&
                   _systemStress01 <= StressSlowThreshold01;
        }

        private void DrawIndirectDial(float heading)
        {
            Transform source = dialPivot != null ? dialPivot : (toolRoot != null ? toolRoot : transform);
            Quaternion rotation = source.rotation * Quaternion.AngleAxis(heading + dialDegreesOffset, Vector3.up);
            _dialMatrices[0] = Matrix4x4.TRS(source.position, rotation, source.lossyScale);
            _dialMatrixBuffer.SetData(_dialMatrices, 0, 0, 1);

            Bounds bounds = indirectDrawBounds;
            bounds.center = source.position;
            Graphics.DrawMeshInstancedIndirect(
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
        }

        private void ApplyCardinalText(int cardinalIndex, bool powered)
        {
            if (!_diegeticTextValid || cardinalText == null)
                return;

            int powerState = powered ? 1 : 0;
            if (cardinalIndex == _lastCardinalIndex && powerState == _lastPowerState)
                return;

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
            _lastCardinalIndex = cardinalIndex;
            _lastPowerState = powerState;
        }

        private void ApplyChromatic(float chromatic, float power, float overkill = 0f)
        {
            float safePower = math.saturate(power);
            float safeOverkill = math.saturate(overkill);
            if (math.abs(_lastChromatic - chromatic) <= ChromaticEpsilon &&
                math.abs(_lastPower - safePower) <= ChromaticEpsilon &&
                math.abs(_lastOverkill - safeOverkill) <= ChromaticEpsilon)
            {
                return;
            }

            Shader.SetGlobalFloat(_CompassGlassChromaticId, chromatic);
            Shader.SetGlobalFloat(_CompassPowerId, safePower);
            Shader.SetGlobalFloat(_CompassOverkillId, safeOverkill);
            _lastChromatic = chromatic;
            _lastPower = safePower;
            _lastOverkill = safeOverkill;
        }

        private void EmitHighTierFailureParticles(bool powered, float anomaly)
        {
            if (!powered ||
                anomaly <= 0.8f ||
                anomalyFailureParticles == null ||
                anomalyParticleBurst <= 0 ||
                !ShouldUseVisualOverkill())
            {
                _particleDebt = 0f;
                return;
            }

            int burst = math.min(anomalyParticleBurst, MaxAnomalyParticleBurst);
            _particleDebt += math.saturate((anomaly - 0.8f) * 5f) * burst;
            int emitCount = (int)math.floor(_particleDebt);
            if (emitCount <= 0)
                return;

            _particleDebt -= emitCount;
            anomalyFailureParticles.Emit(emitCount);
        }

        private void EnsureIndirectBuffers()
        {
            if (!enableIndirectHighTier ||
                _cachedQualityTier < HectonQualityTier.High ||
                dialMesh == null ||
                dialIndirectMaterial == null ||
                !SupportsIndirectDial() ||
                _indirectArgsBuffer != null)
            {
                return;
            }

            _indirectArgs[0] = dialMesh.GetIndexCount(0);
            _indirectArgs[1] = 1u;
            _indirectArgs[2] = dialMesh.GetIndexStart(0);
            _indirectArgs[3] = dialMesh.GetBaseVertex(0);
            _indirectArgs[4] = 0u;
            _indirectArgsBuffer = new ComputeBuffer(1, sizeof(uint) * _indirectArgs.Length, ComputeBufferType.IndirectArguments); // COLD ALLOC: ComputeBuffer[1] - compass indirect args - owner: DiegeticGyroCompassRuntime
            _dialMatrixBuffer = new ComputeBuffer(1, 64, ComputeBufferType.Structured); // COLD ALLOC: ComputeBuffer[1] - compass dial matrix buffer - owner: DiegeticGyroCompassRuntime
            _indirectArgsBuffer.SetData(_indirectArgs, 0, 0, _indirectArgs.Length);
            dialIndirectMaterial.SetBuffer(_CompassDialMatricesId, _dialMatrixBuffer);
        }

        private void ReleaseIndirectBuffers()
        {
            if (_indirectArgsBuffer != null)
            {
                _indirectArgsBuffer.Release();
                _indirectArgsBuffer = null;
            }

            if (_dialMatrixBuffer != null)
            {
                _dialMatrixBuffer.Release();
                _dialMatrixBuffer = null;
            }
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

        private float3 ResolveVelocity(double3 actualAup, float deltaTime)
        {
            if (!_hasLastActualAup || deltaTime <= 0f)
            {
                _lastActualAup = actualAup;
                _hasLastActualAup = true;
                return float3.zero;
            }

            double invDelta = 1d / deltaTime;
            double3 velocity = (actualAup - _lastActualAup) * invDelta;
            _lastActualAup = actualAup;
            if (!math.all(math.isfinite(velocity)))
                return float3.zero;

            velocity = math.clamp(
                velocity,
                new double3(-VelocityClampMetersPerSecond),
                new double3(VelocityClampMetersPerSecond));
            return new float3((float)velocity.x, (float)velocity.y, (float)velocity.z);
        }

        private void WriteBlackBox(in CompassStateDTO state, NativeArray<CompassBlackBoxEntry> blackBox)
        {
            if (!blackBox.IsCreated || blackBox.Length < BlackBoxCapacity)
                return;

            blackBox[_blackBoxCursor] = new CompassBlackBoxEntry
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

            _blackBoxCursor++;
            if (_blackBoxCursor >= BlackBoxCapacity)
                _blackBoxCursor = 0;
        }

        private void DumpBlackBoxOnce(NativeArray<CompassBlackBoxEntry> blackBox)
        {
            if (_blackBoxDumped || !blackBox.IsCreated)
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
                    writer.Write(_blackBoxCursor);
                    for (int i = 0; i < BlackBoxCapacity; i++)
                    {
                        int index = _blackBoxCursor + i;
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

        private static bool IsLowTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350 || tier == HectonQualityTier.Unknown;
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

        private static bool IsFiniteState(in CompassStateDTO state)
        {
            return math.all(math.isfinite(state.ActualAUP)) &&
                   math.all(math.isfinite(state.RawEstimatedAUP)) &&
                   math.all(math.isfinite(state.EstimatedAUP)) &&
                   math.all(math.isfinite(state.Velocity)) &&
                   math.isfinite(state.ActualHeadingDegrees) &&
                   math.isfinite(state.CurrentHeadingDegrees) &&
                   math.isfinite(state.DriftDegrees) &&
                   math.isfinite(state.MaxGyroDriftDegrees);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct GyroDriftJob : IJob
        {
            public NativeArray<CompassStateDTO> State;
            public NativeArray<float> Output;
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
                float deltaTime = math.clamp(DeltaSeconds, 0f, MaxIntegrationDeltaSeconds);
                float actualHeading = NormalizeAngle(state.ActualHeadingDegrees);
                float currentHeading = (state.Flags & FlagInitialized) != 0u
                    ? NormalizeAngle(state.CurrentHeadingDegrees)
                    : actualHeading;
                float power = math.saturate(state.Power01);
                float anomaly = math.saturate(state.AnomalyInterference01);

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
                    float alpha = math.saturate(HeadingCatchupRate * deltaTime);
                    float noiseValue = ResolveNoiseValue(NoiseTime, DriftNoiseFrequency, flags);
                    currentHeading += headingDelta * alpha;
                    currentHeading += noiseValue * AnomalyNoiseDegrees * anomaly * deltaTime;
                    if (anomaly > 0.8f)
                    {
                        float spinSign = noiseValue < 0f ? -1f : 1f;
                        currentHeading += spinSign * WildSpinDegreesPerSecond * anomaly * deltaTime;
                    }
                }

                currentHeading = NormalizeAngle(currentHeading);
                float drift = DeltaAngleDegrees(actualHeading, currentHeading);
                float maxDrift = math.max(math.abs(state.MaxGyroDriftDegrees), math.abs(drift));
                float glitch = math.saturate(anomaly * 1.25f + math.saturate(math.abs(drift) * (1f / 90f)) * 0.25f);

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
                float t = noiseTime * noiseFrequency;
                if ((flags & FlagLowTier) != 0u)
                    return TriangleNoise(t);

                float baseNoise = noise.cnoise(new float2(t, 17.371f));
                if ((flags & FlagIndirectDial) == 0u)
                    return baseNoise;

                return math.clamp(baseNoise + noise.cnoise(new float2(t * 2.07f, 43.113f)) * 0.35f, -1f, 1f);
            }

            private static float TriangleNoise(float t)
            {
                float phase = math.frac(t);
                return 1f - math.abs(phase * 4f - 2f);
            }
        }
    }
}
