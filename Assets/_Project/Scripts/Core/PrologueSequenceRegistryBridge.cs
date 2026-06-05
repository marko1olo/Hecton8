using System;
using System.Collections.Generic;
using System.Threading;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Core-domain adapter between contract-only prologue pacing and concrete registry/signal services.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8540)]
    public sealed class PrologueSequenceRegistryBridge : MonoBehaviour, IPrologueSequenceRuntime, IGlobalRegistryHotSwapListener
    {
        private int _signalPushDropCount;
        private const uint SourceHash = PrologueSignalSourceHashes.SequenceDirector;
        private const uint ManualOverrideSourceHash = PrologueSignalSourceHashes.ManualOverrideLever;
        private const uint OrbitalRelativitySourceHash = PrologueSignalSourceHashes.OrbitalRelativityDirector;
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;
        private const string StandaloneOrbitSceneName = "01_ORBIT";
        private const uint MissingServiceHash = 0x50524D49u; // PRMI
        private const uint RegistrationRejectedHash = 0x5052524Au; // PRRJ
        private const uint CancellationFaultHash = 0x50524346u; // PRCF
        private const uint MuffledBreathingHash = 0x4D425254u; // MBRT
        private const uint HullTempCriticalHash = 0x4854454Du; // HTEM
        private const uint ManualReleaseHash = 0x4D52454Cu; // MREL
        private const uint ManualReleaseContextHash = 0x434F434Bu; // COCK
        private const uint ShallowWaterChunkHash = 0x53484C57u; // SHLW
        private const int SurvivalProxyHysteresisFrames = 150;
        private const int SurvivalProxyProbeIntervalFrames = 30;
        private const int StandaloneOrbitWhiteoutFallbackFrames = 48;
        private const byte CriticalMemoryPressureSeverity = 2;
        private const float ForcedMemoryPressureThreshold01 = 0.85f;
        private const float MassiveImpactSeverity = 1f;
        private const float MassiveImpactCameraAmplitudeScale = 1.45f;
        private const float MassiveImpactCameraTranslationGain = 1.20f;
        private const float MassiveImpactCameraRotationGain = 1.55f;
        private const float ReentryHeatStartThreshold01 = 0.001f;
        private const double SkipHoldSeconds = 1.5d;

        [SerializeField] private MonoBehaviour sequenceComponent;
        [SerializeField] private bool autoRunOnEnable = true;
        [SerializeField] private bool allowStandaloneOrbitWhiteoutFallback = true;
        [SerializeField] private bool allowStandaloneOrbitHydrationProxy = true;
        [SerializeField] private long oceanSurfaceChunkId;
        [SerializeField] private bool allowAnyHydratedChunkFallback = true;

        private IPrologueSequenceService _service;
        private IInputService _inputService;
        private IOrbitalDirector _orbitalDirector;
        private IStreamingBackpressureService _streamingBackpressure;
        private ITickDispatcher _tickDispatcher;
        private List<MonoBehaviour> _serviceDiscoveryScratch;
        private CancellationTokenSource _runCancellationSource;
        private bool _registeredService;
        private bool _registeredHotSwap;
        private bool _autoRunPending;
        private bool _isDevelopmentBuild;
        private float _cachedSurvivalProxyPressure01;
        private float _pendingSurvivalProxyPressure01;
        private bool _hasSurvivalProxyCache;
        private float _lastObservedSurvivalProxyPressure01;
        private bool _lastObservedForcedLowMemory;
        private bool _skipRequested;
        private double _skipHoldSeconds;
        private int _skipHoldFrameIndex = -1;
        private bool _observedHighResSurfaceReady;
        private bool _observedProxySurfaceReady;
        private bool _standaloneOrbitSceneActive;
        private bool _hasStandaloneWhiteoutFallback;
        private int _survivalProxyCandidateFrame;
        private int _survivalProxyPolicyProbeFrame = -1;
        private int _memoryPressureSnapshotFrame = -1;
        private int _memoryPressureSnapshotCursor;
        private int _atmosphereSnapshotFrame = -1;
        private int _completeSnapshotFrame = -1;
        private int _residencySnapshotFrame = -1;
        private int _standaloneWhiteoutFallbackFirstFrame = -1;
        private int _atmosphereSnapshotCursor;
        private int _completeSnapshotCursor;
        private int _residencySnapshotCursor;
        private uint _lastPlayerInputSignalSequence;
        private PrologueCompleteSnapshot _standaloneWhiteoutFallbackSnapshot;
        private ushort _sequence;

        public bool IsDevelopmentBuild => _isDevelopmentBuild;

        public float SurvivalProxyPressure01
        {
            get
            {
                return _hasSurvivalProxyCache ? _cachedSurvivalProxyPressure01 : 0f;
            }
        }

        public bool IsSurvivalProxySurfaceActive
        {
            get
            {
                return SurvivalProxyPressure01 >= PrologueSequenceQualityPolicy.SurvivalProxyActivationThreshold01;
            }
        }

        [Obsolete("Use SurvivalProxyPressure01 or IsSurvivalProxySurfaceActive. This member is a compatibility alias.")]
        public bool IsLowTier => IsSurvivalProxySurfaceActive;

        public bool IsStandaloneOrbitHandoffProxyAllowed
        {
            get
            {
                return allowStandaloneOrbitHydrationProxy && _standaloneOrbitSceneActive;
            }
        }

        public uint CurrentFrame => Hecton8.Core.SystemDispatcher.CurrentFrameId;

        public bool ShouldSkipPrologue
        {
            get
            {
                return _skipRequested;
            }
        }

        private void OnEnable()
        {
            _standaloneOrbitSceneActive = IsStandaloneOrbitSceneName(gameObject.scene.name);
            ResetTransientSequenceState();
            OpenOrDiscoverServiceForOwnerRoute();

            if (_service == null)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(MissingServiceHash, SourceHash, 1f);
                return;
            }

            IPrologueSequenceService registeredService = GlobalRegistry.PrologueSequence;
            if (registeredService != null && !ReferenceEquals(registeredService, _service))
            {
                GlobalTelemetryBus.PublishPerformanceWarning(RegistrationRejectedHash, SourceHash, 1f);
                return;
            }

            _service.Configure(this);
            if (GlobalRegistry.Phase == GlobalRegistry.RegistryPhase.Ready)
                GlobalRegistry.ReplacePrologueSequenceRuntime(_service);
            else
                GlobalRegistry.RegisterPrologueSequenceRuntime(_service);
            _registeredService = ReferenceEquals(GlobalRegistry.PrologueSequence, _service);
            if (!_registeredService)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(RegistrationRejectedHash, SourceHash, 1f);
                return;
            }

            CacheRuntimeServices();
            BindInputIfAvailable();
            if (Application.isPlaying)
                BaselineSkipInputSignalSequence();
            RegisterHotSwap();

            if (autoRunOnEnable && Application.isPlaying)
                TryStartAutoSequenceRun();
        }

        private void OnDisable()
        {
            _autoRunPending = false;
            RequestRunCancellation(PrologueCancelReasons.ExplicitCancel);

            if (_registeredService && _service != null)
            {
                GlobalRegistry.UnregisterPrologueSequenceRuntime(_service);
                _registeredService = false;
            }

            UnbindInput();
            UnregisterHotSwap();
            ClearRuntimeServiceCache();
            _skipRequested = false;
            _standaloneOrbitSceneActive = false;
        }

        private void OnDestroy()
        {
            DisposeRunCancellationSource();
        }

        public bool TryGetOrbitalSnapshot(out PrologueOrbitalSnapshot snapshot)
        {
            IOrbitalDirector orbital = _orbitalDirector;
            if (orbital != null && orbital.TryGetSnapshot(out OrbitalDirectorSnapshot source))
            {
                snapshot = new PrologueOrbitalSnapshot(
                    source.UniverseVelocity,
                    source.PlanetDistanceMeters,
                    source.ReentryHeat01,
                    source.CloudWhiteout01,
                    source.Sequence,
                    source.MathLod,
                    source.Flags);
                return true;
            }

            snapshot = default;
            return false;
        }

        public bool TryConsumeAtmosphericReentry(out PrologueAtmosphericReentrySnapshot snapshot)
        {
            ReadOnlySpan<AtmosphericReentrySignal> signals = SignalBus<AtmosphericReentrySignal>.GetFrameSnapshot();
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_atmosphereSnapshotFrame != frame)
            {
                _atmosphereSnapshotFrame = frame;
                _atmosphereSnapshotCursor = 0;
            }

            while (_atmosphereSnapshotCursor < signals.Length)
            {
                AtmosphericReentrySignal signal = signals[_atmosphereSnapshotCursor++];
                if (!IsValidAtmosphericReentrySignal(in signal))
                    continue;

                snapshot = new PrologueAtmosphericReentrySnapshot(
                    signal.AltitudeMeters,
                    signal.UniverseVelocityMetersPerSecond,
                    signal.Heat01,
                    signal.Sequence,
                    signal.Phase,
                    signal.Flags);
                return true;
            }

            snapshot = default;
            return false;
        }

        public bool TryConsumePrologueComplete(out PrologueCompleteSnapshot snapshot)
        {
            ReadOnlySpan<PrologueCompleteSignal> signals = SignalBus<PrologueCompleteSignal>.GetFrameSnapshot();
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_completeSnapshotFrame != frame)
            {
                _completeSnapshotFrame = frame;
                _completeSnapshotCursor = 0;
            }

            while (_completeSnapshotCursor < signals.Length)
            {
                PrologueCompleteSignal signal = signals[_completeSnapshotCursor++];
                if (IsValidManualCompleteSignal(in signal))
                {
                    ClearStandaloneWhiteoutFallback();
                    snapshot = new PrologueCompleteSnapshot(
                        signal.Frame,
                        signal.WhiteoutHoldSeconds,
                        signal.Sequence,
                        signal.Phase,
                        signal.Flags);
                    return true;
                }

                if (IsValidStandaloneOrbitWhiteoutFallbackSignal(in signal))
                    CaptureStandaloneWhiteoutFallback(in signal, frame);
            }

            if (TryConsumeManualReleaseInput(out snapshot))
                return true;

            if (TryConsumeOrbitalWhiteoutFallback(frame, out snapshot))
                return true;

            if (TryConsumePendingStandaloneWhiteoutFallback(frame, out snapshot))
                return true;

            snapshot = default;
            return false;
        }

        private bool TryConsumeManualReleaseInput(out PrologueCompleteSnapshot snapshot)
        {
            ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash != PlayerInputSignalSourceHash ||
                    !IsManualReleaseCommand(signal.Command) ||
                    !IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    continue;

                _lastPlayerInputSignalSequence = signal.Sequence;
                ClearStandaloneWhiteoutFallback();
                snapshot = new PrologueCompleteSnapshot(
                    signal.Frame != 0u ? signal.Frame : CurrentFrame,
                    0.4f,
                    NextNonZeroSequence(),
                    PrologueCompleteSignal.PhaseOceanHandoff,
                    PrologueCompleteSignal.FlagForceWhiteout);
                return true;
            }

            snapshot = default;
            return false;
        }

        public bool IsOceanSurfaceReady(bool allowProxy)
        {
            return _observedHighResSurfaceReady || (allowProxy && _observedProxySurfaceReady);
        }

        public void RefreshHydrationState(bool allowProxy)
        {
            if (_observedHighResSurfaceReady)
                return;

            RefreshSurvivalProxyPressureForFrame(SystemDispatcher.CurrentFrameIndex);

            IStreamingBackpressureService streaming = _streamingBackpressure;
            if (oceanSurfaceChunkId != 0 && streaming != null && streaming.IsChunkResident(oceanSurfaceChunkId))
            {
                _observedHighResSurfaceReady = true;
                return;
            }

            if (allowProxy && _observedProxySurfaceReady)
                return;

            if (allowProxy && allowStandaloneOrbitHydrationProxy && _standaloneOrbitSceneActive)
            {
                _observedProxySurfaceReady = true;
                return;
            }

            if (allowProxy && streaming != null && streaming.ActiveImpostorCount > 0)
            {
                _observedProxySurfaceReady = true;
                return;
            }

            ReadOnlySpan<SectorResidencyHydratedSignal> signals = SignalBus<SectorResidencyHydratedSignal>.GetFrameSnapshot();
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_residencySnapshotFrame != frame)
            {
                _residencySnapshotFrame = frame;
                _residencySnapshotCursor = 0;
            }

            while (_residencySnapshotCursor < signals.Length)
            {
                SectorResidencyHydratedSignal signal = signals[_residencySnapshotCursor++];
                bool proxy = (signal.Flags & SectorResidencyHydratedSignal.FlagProxyFallback) != 0;
                if (!MatchesOceanChunk(signal.ChunkId, allowProxy, proxy))
                    continue;

                if (proxy)
                    _observedProxySurfaceReady = true;
                else
                    _observedHighResSurfaceReady = true;

                if (!proxy || allowProxy)
                    return;
            }
        }

        public void PrepareSequenceRun()
        {
            ResetTransientSequenceState();
            RefreshSurvivalProxyPressureForFrame(SystemDispatcher.CurrentFrameIndex);
        }

        public void RefreshFrameState()
        {
            if (_skipRequested)
                return;

            ConsumeSkipInputSignals();
            if (_skipRequested)
                return;

            RefreshSkipHoldState(ResolveSkipHoldDeltaSeconds());
        }

        public Awaitable DelayDilatedAsync(float seconds, CancellationToken cancellationToken)
        {
            return DelayDilatedInterruptibleAsync(seconds, cancellationToken);
        }

        public Awaitable NextFrameAsync(CancellationToken cancellationToken)
        {
            return AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
        }

        public void PublishInputLock(PrologueInputLockFlags flags, bool paused)
        {
            SystemPauseSignal signal = default;
            signal.SourceHash = SourceHash;
            signal.Frame = CurrentFrame;
            signal.Sequence = unchecked(++_sequence);
            signal.Paused = paused ? (byte)1 : (byte)0;
            signal.Flags = (byte)flags;
            signal.RestoreScalar = 1f;
            SignalBus<SystemPauseSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        public void PublishMuffledBreathing(float intensity01, float durationSeconds)
        {
            float safeIntensity = math.saturate(intensity01);
            MixerStateSignal mixer = default;
            mixer.MixerStateHash = MuffledBreathingHash;
            mixer.SourceHash = SourceHash;
            mixer.Intensity01 = safeIntensity;
            mixer.DuckingDb = -18f * safeIntensity;
            mixer.Frame = CurrentFrame;
            mixer.Flags = 1;
            SignalBus<MixerStateSignal>.TryPushTracked(in mixer, ref _signalPushDropCount);

            AcousticPingSignal ping = default;
            ping.PositionAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            ping.RadiusMeters = math.max(8f, durationSeconds * 12f);
            ping.Intensity01 = safeIntensity;
            ping.SourceId = MuffledBreathingHash;
            ping.Channel = AcousticPingSignal.ChannelFabricScrape;
            ping.Flags = AcousticPingSignal.FlagFabricScrape;
            SignalBus<AcousticPingSignal>.TryPushTracked(in ping, ref _signalPushDropCount);
        }

        public void PublishHullTempCriticalWarning(float severity01)
        {
            VocalWarningSignal signal = default;
            signal.WarningHash = HullTempCriticalHash;
            signal.SourceId = SourceHash;
            signal.Severity01 = math.saturate(severity01);
            signal.CooldownSeconds = 2f;
            signal.Priority = 1;
            SignalBus<VocalWarningSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        public void PublishHeavyRumble(float intensity01, float durationSeconds)
        {
            HapticRequest signal = default;
            signal.Intensity01 = math.saturate(intensity01);
            signal.DurationSeconds = math.max(0.05f, durationSeconds);
            signal.Frequency01 = 0.92f;
            signal.SourceHash = SourceHash;
            signal.Frame = CurrentFrame;
            signal.Channel = HapticRequest.ChannelVehicleCritical;
            SignalBus<HapticRequest>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        public void PublishManualReleasePrompt()
        {
            DiegeticHudSignal diegetic = default;
            diegetic.MessageHash = ManualReleaseHash;
            diegetic.ContextHash = ManualReleaseContextHash;
            diegetic.SourceHash = SourceHash;
            diegetic.Frame = CurrentFrame;
            diegetic.PromptKind = DiegeticHudSignal.PromptManualRelease;
            diegetic.Priority = 3;
            diegetic.Flags = DiegeticHudSignal.FlagPersistent;
            SignalBus<DiegeticHudSignal>.TryPushTracked(in diegetic, ref _signalPushDropCount);

            HUDNotificationSignal hud = default;
            hud.MessageHash = ManualReleaseHash;
            hud.ContextHash = ManualReleaseContextHash;
            hud.SourceId = SourceHash;
            hud.Frame = CurrentFrame;
            hud.Severity = 2;
            hud.Flags = 1;
            SignalBus<HUDNotificationSignal>.TryPushTracked(in hud, ref _signalPushDropCount);
        }

        public void PublishMassiveImpact()
        {
            CameraJuiceSignals.TryPublishImpact(
                MassiveImpactSeverity,
                Vector3.zero,
                Vector3.down,
                CameraJuiceSignals.SharpKineticImpactProfileHash,
                MassiveImpactCameraAmplitudeScale,
                CameraJuiceSignals.CriticalPriority,
                0f,
                MassiveImpactCameraTranslationGain,
                MassiveImpactCameraRotationGain,
                SourceHash);
        }

        public void PublishOceanHandoff()
        {
            PrologueCompleteSignal signal = default;
            signal.CapsuleAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            signal.Frame = CurrentFrame;
            signal.SourceHash = SourceHash;
            signal.Sequence = unchecked(++_sequence);
            signal.WhiteoutHoldSeconds = 0.12f;
            signal.Flags = PrologueCompleteSignal.FlagForceWhiteout;
            signal.Phase = PrologueCompleteSignal.PhaseOceanHandoff;
            SignalBus<PrologueCompleteSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        public void ZeroUniverseVelocity()
        {
            IOrbitalDirector orbital = _orbitalDirector;
            orbital?.ForceZeroUniverseVelocity(0);
        }

        public void ForceShallowWaterHydration()
        {
            _observedHighResSurfaceReady = true;
            _observedProxySurfaceReady = true;

            SectorResidencyHydratedSignal signal = default;
            signal.CenterAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            signal.ChunkId = oceanSurfaceChunkId != 0 ? oceanSurfaceChunkId : ShallowWaterChunkHash;
            signal.Frame = CurrentFrame;
            signal.RadiusMetersQ = 64;
            signal.Flags = SectorResidencyHydratedSignal.FlagPinned | SectorResidencyHydratedSignal.FlagProxyFallback;
            signal.ResidencyState = 1;
            SignalBus<SectorResidencyHydratedSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        public void PushTelemetry(PrologueStage stage, uint stateHash, byte flags)
        {
            uint stageHash = unchecked(SourceHash ^ ((uint)stage << 24) ^ flags);
            GlobalTelemetryBus.PublishPrologueStage(stageHash, stateHash, flags);
        }

        public void DumpBlackBox()
        {
            GlobalTelemetryBus.RequestEmergencyFlushAsync();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Input:
                    UnbindInput();
                    BindInputIfAvailable(currentService as IInputService);
                    break;
                case GlobalRegistryServiceSlot.OrbitalDirectorRuntime:
                    _orbitalDirector = currentService as IOrbitalDirector;
                    break;
                case GlobalRegistryServiceSlot.StreamingBackpressureRuntime:
                    _streamingBackpressure = currentService as IStreamingBackpressureService;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _tickDispatcher = currentService as ITickDispatcher;
                    break;
            }

            if (_autoRunPending && isActiveAndEnabled)
                TryStartAutoSequenceRun();
        }

        private void OpenOrDiscoverServiceForOwnerRoute()
        {
            _service = null;

            if (sequenceComponent is IPrologueSequenceService serializedService)
            {
                _service = serializedService;
                return;
            }

            if (_serviceDiscoveryScratch == null)
                _serviceDiscoveryScratch = new List<MonoBehaviour>(4); // COLD ALLOC: List<MonoBehaviour> - reusable interface discovery buffer - owner: PrologueSequenceRegistryBridge

            _serviceDiscoveryScratch.Clear();
            GetComponents<MonoBehaviour>(_serviceDiscoveryScratch);
            for (int i = 0; i < _serviceDiscoveryScratch.Count; i++)
            {
                MonoBehaviour component = _serviceDiscoveryScratch[i];
                if (component is IPrologueSequenceService discovered)
                {
                    sequenceComponent = component;
                    _service = discovered;
                    _serviceDiscoveryScratch.Clear();
                    return;
                }
            }

            _serviceDiscoveryScratch.Clear();
        }

        private void RegisterHotSwap()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void UnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void CacheRuntimeServices()
        {
            _isDevelopmentBuild = GlobalRegistry.IsDevelopmentBuild;
            _orbitalDirector = GlobalRegistry.OrbitalDirector;
            _streamingBackpressure = GlobalRegistry.StreamingBackpressure;
            _tickDispatcher = GlobalRegistry.TickDispatcher;
        }

        private void ClearRuntimeServiceCache()
        {
            _isDevelopmentBuild = false;
            _inputService = null;
            _orbitalDirector = null;
            _streamingBackpressure = null;
            _tickDispatcher = null;
            ResetSurvivalProxyCache();
        }

        private bool TryStartAutoSequenceRun()
        {
            if (!autoRunOnEnable || !Application.isPlaying || _runCancellationSource != null)
                return false;

            IPrologueSequenceService service = _service;
            if (service == null || _tickDispatcher == null)
            {
                _autoRunPending = true;
                return false;
            }

            _autoRunPending = false;
            DisposeRunCancellationSource();
            // COLD ALLOC: CancellationTokenSource[1] - owner-run cancel source; disable/destroy cancels explicitly - owner: PrologueSequenceRegistryBridge
            _runCancellationSource = new CancellationTokenSource();
            _ = RunAutoSequenceAsync(service, _runCancellationSource);
            return true;
        }

        private void ResetSurvivalProxyCache()
        {
            _cachedSurvivalProxyPressure01 = 0f;
            _pendingSurvivalProxyPressure01 = 0f;
            _hasSurvivalProxyCache = false;
            _lastObservedSurvivalProxyPressure01 = 0f;
            _lastObservedForcedLowMemory = false;
            _survivalProxyCandidateFrame = 0;
            _survivalProxyPolicyProbeFrame = -1;
            _memoryPressureSnapshotFrame = -1;
            _memoryPressureSnapshotCursor = 0;
        }

        private void BindInputIfAvailable()
        {
            BindInputIfAvailable(GlobalRegistry.Input);
        }

        private void BindInputIfAvailable(IInputService input)
        {
            if (input == null)
                return;

            _inputService = input;
        }

        private void UnbindInput()
        {
            _inputService = null;
        }

        private void HandleSkipRequested()
        {
            if (_skipRequested)
                return;

            _skipHoldSeconds = 0d;
            _skipRequested = true;
            RequestRunCancellation(PrologueCancelReasons.DevSkip);
        }

        private void ConsumeSkipInputSignals()
        {
            ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash != PlayerInputSignalSourceHash ||
                    signal.Command != PlayerInputSignalCommands.Cancel ||
                    !IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    continue;

                _lastPlayerInputSignalSequence = signal.Sequence;
                return;
            }
        }

        private void RefreshSkipHoldState(double deltaSeconds)
        {
            if (!IsSkipInputHeld())
            {
                _skipHoldSeconds = 0d;
                return;
            }

            if (deltaSeconds <= 0d)
                return;

            _skipHoldSeconds += deltaSeconds;
            if (_skipHoldSeconds >= SkipHoldSeconds)
                HandleSkipRequested();
        }

        private double ResolveSkipHoldDeltaSeconds()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_skipHoldFrameIndex == frame)
                return 0d;

            ITickDispatcher dispatcher = _tickDispatcher;
            double deltaSeconds = dispatcher != null
                ? dispatcher.TimeSnapshot.DeltaTime
                : SystemDispatcher.CurrentFrameDeltaTime;
            if (!math.isfinite(deltaSeconds) || deltaSeconds <= 0d)
                return 0d;

            _skipHoldFrameIndex = frame;
            return deltaSeconds > SkipHoldSeconds ? SkipHoldSeconds : deltaSeconds;
        }

        private bool IsSkipInputHeld()
        {
            IInputService input = _inputService;
            if (input == null || !input.IsInitialized)
                return false;

            PlayerInputState state = input.GetState();
            return state.HasAction(PlayerInputAction.Cancel) ||
                   (IsDevelopmentBuild && IsDevelopmentSkipChordHeld(in state));
        }

        private static bool IsDevelopmentSkipChordHeld(in PlayerInputState state)
        {
            return state.HasAction(PlayerInputAction.Dash) &&
                   state.HasAction(PlayerInputAction.PrimaryFire) &&
                   state.HasAction(PlayerInputAction.SecondaryFire);
        }

        private void BaselineSkipInputSignalSequence()
        {
            ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash == PlayerInputSignalSourceHash &&
                    IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    _lastPlayerInputSignalSequence = signal.Sequence;
            }
        }

        private static bool IsNewerInputSequence(uint candidate, uint current)
        {
            return candidate != 0u && candidate != current && unchecked(candidate - current) < 0x80000000u;
        }

        private bool MatchesOceanChunk(long chunkId, bool allowProxy, bool proxy)
        {
            if (oceanSurfaceChunkId != 0)
                return chunkId == oceanSurfaceChunkId;

            if (chunkId == ShallowWaterChunkHash)
                return true;

            return allowAnyHydratedChunkFallback && allowProxy && proxy;
        }

        private static bool IsValidAtmosphericReentrySignal(in AtmosphericReentrySignal signal)
        {
            return math.isfinite(signal.AltitudeMeters) &&
                   math.isfinite(signal.UniverseVelocityMetersPerSecond) &&
                   math.isfinite(signal.Heat01) &&
                   (signal.Phase == AtmosphericReentrySignal.PhasePlasma ||
                    signal.Phase == AtmosphericReentrySignal.PhaseWhiteout) &&
                   signal.Heat01 > ReentryHeatStartThreshold01;
        }

        private bool IsValidManualCompleteSignal(in PrologueCompleteSignal signal)
        {
            return signal.SourceHash == ManualOverrideSourceHash &&
                   signal.Sequence != 0 &&
                   signal.Phase == PrologueCompleteSignal.PhaseOceanHandoff &&
                   (signal.Flags & PrologueCompleteSignal.FlagForceWhiteout) != 0 &&
                   math.isfinite(signal.WhiteoutHoldSeconds) &&
                   signal.WhiteoutHoldSeconds >= 0f;
        }

        private bool IsValidStandaloneOrbitWhiteoutFallbackSignal(in PrologueCompleteSignal signal)
        {
            return allowStandaloneOrbitWhiteoutFallback &&
                   _standaloneOrbitSceneActive &&
                   signal.SourceHash == OrbitalRelativitySourceHash &&
                   signal.Sequence != 0 &&
                   signal.Phase == PrologueCompleteSignal.PhaseWhiteout &&
                   (signal.Flags & PrologueCompleteSignal.FlagForceWhiteout) != 0 &&
                   math.isfinite(signal.WhiteoutHoldSeconds) &&
                   signal.WhiteoutHoldSeconds >= 0f;
        }

        private void CaptureStandaloneWhiteoutFallback(in PrologueCompleteSignal signal, int frame)
        {
            if (_hasStandaloneWhiteoutFallback)
                return;

            _hasStandaloneWhiteoutFallback = true;
            _standaloneWhiteoutFallbackFirstFrame = frame;
            _standaloneWhiteoutFallbackSnapshot = new PrologueCompleteSnapshot(
                signal.Frame,
                signal.WhiteoutHoldSeconds,
                signal.Sequence,
                PrologueCompleteSignal.PhaseOceanHandoff,
                signal.Flags);
        }

        private bool TryConsumeOrbitalWhiteoutFallback(int frame, out PrologueCompleteSnapshot snapshot)
        {
            snapshot = default;
            if (!allowStandaloneOrbitWhiteoutFallback ||
                !_standaloneOrbitSceneActive ||
                _orbitalDirector == null)
            {
                return false;
            }

            if (!_orbitalDirector.TryGetSnapshot(out OrbitalDirectorSnapshot orbital) ||
                !IsValidStandaloneOrbitWhiteoutSnapshot(in orbital))
            {
                ClearStandaloneWhiteoutFallback();
                return false;
            }

            if (!_hasStandaloneWhiteoutFallback)
            {
                _hasStandaloneWhiteoutFallback = true;
                _standaloneWhiteoutFallbackFirstFrame = frame;
                _standaloneWhiteoutFallbackSnapshot = new PrologueCompleteSnapshot(
                    CurrentFrame,
                    0.25f,
                    NextNonZeroSequence(),
                    PrologueCompleteSignal.PhaseOceanHandoff,
                    PrologueCompleteSignal.FlagForceWhiteout);
                return false;
            }

            if (frame - _standaloneWhiteoutFallbackFirstFrame < StandaloneOrbitWhiteoutFallbackFrames)
                return false;

            snapshot = _standaloneWhiteoutFallbackSnapshot;
            ClearStandaloneWhiteoutFallback();
            return true;
        }

        private bool TryConsumePendingStandaloneWhiteoutFallback(int frame, out PrologueCompleteSnapshot snapshot)
        {
            if (!_hasStandaloneWhiteoutFallback ||
                !_standaloneOrbitSceneActive ||
                !allowStandaloneOrbitWhiteoutFallback ||
                frame - _standaloneWhiteoutFallbackFirstFrame < StandaloneOrbitWhiteoutFallbackFrames)
            {
                snapshot = default;
                return false;
            }

            snapshot = _standaloneWhiteoutFallbackSnapshot;
            ClearStandaloneWhiteoutFallback();
            return true;
        }

        private void ClearStandaloneWhiteoutFallback()
        {
            _hasStandaloneWhiteoutFallback = false;
            _standaloneWhiteoutFallbackFirstFrame = -1;
            _standaloneWhiteoutFallbackSnapshot = default;
        }

        private static bool IsValidStandaloneOrbitWhiteoutSnapshot(in OrbitalDirectorSnapshot snapshot)
        {
            return math.isfinite(snapshot.CloudWhiteout01) &&
                   math.isfinite(snapshot.PlanetDistanceMeters) &&
                   snapshot.CloudWhiteout01 >= 0.98f &&
                   snapshot.PlanetDistanceMeters <= 1f;
        }

        private static bool IsManualReleaseCommand(byte command)
        {
            return command == PlayerInputSignalCommands.Interact ||
                   command == PlayerInputSignalCommands.PrimaryAction ||
                   command == PlayerInputSignalCommands.SecondaryAction;
        }

        private ushort NextNonZeroSequence()
        {
            unchecked
            {
                _sequence++;
                if (_sequence == 0)
                    _sequence++;
                return _sequence;
            }
        }

        private static bool IsStandaloneOrbitSceneName(string sceneName)
        {
            return string.Equals(sceneName, StandaloneOrbitSceneName, StringComparison.Ordinal);
        }

        private void RefreshSurvivalProxyPressureForFrame(int frame)
        {
            bool forcedLowMemory;
            float requestedPressure01 = RefreshObservedSurvivalProxyPressure(frame, out forcedLowMemory);

            if (!_hasSurvivalProxyCache)
            {
                _cachedSurvivalProxyPressure01 = requestedPressure01;
                _pendingSurvivalProxyPressure01 = requestedPressure01;
                _survivalProxyCandidateFrame = frame;
                _hasSurvivalProxyCache = true;
                return;
            }

            if (forcedLowMemory &&
                _cachedSurvivalProxyPressure01 < PrologueSequenceQualityPolicy.SurvivalProxyActivationThreshold01)
            {
                _cachedSurvivalProxyPressure01 = 1f;
                _pendingSurvivalProxyPressure01 = 1f;
                _survivalProxyCandidateFrame = frame;
                return;
            }

            bool requestedProxy = requestedPressure01 >= PrologueSequenceQualityPolicy.SurvivalProxyActivationThreshold01;
            bool cachedProxy = _cachedSurvivalProxyPressure01 >= PrologueSequenceQualityPolicy.SurvivalProxyActivationThreshold01;
            if (requestedProxy == cachedProxy)
            {
                _cachedSurvivalProxyPressure01 = requestedPressure01;
                _pendingSurvivalProxyPressure01 = requestedPressure01;
                _survivalProxyCandidateFrame = frame;
                return;
            }

            bool pendingProxy = _pendingSurvivalProxyPressure01 >= PrologueSequenceQualityPolicy.SurvivalProxyActivationThreshold01;
            if (requestedProxy != pendingProxy)
            {
                _pendingSurvivalProxyPressure01 = requestedPressure01;
                _survivalProxyCandidateFrame = frame;
                return;
            }

            if (frame - _survivalProxyCandidateFrame >= SurvivalProxyHysteresisFrames)
            {
                _cachedSurvivalProxyPressure01 = requestedPressure01;
                _pendingSurvivalProxyPressure01 = requestedPressure01;
                _survivalProxyCandidateFrame = frame;
            }
        }

        private float RefreshObservedSurvivalProxyPressure(int frame, out bool forcedLowMemory)
        {
            if (TryObserveCriticalMemoryPressure(frame))
            {
                forcedLowMemory = true;
                _lastObservedForcedLowMemory = true;
                _lastObservedSurvivalProxyPressure01 = 1f;
                _survivalProxyPolicyProbeFrame = frame;
                return 1f;
            }

            if (_survivalProxyPolicyProbeFrame < 0 ||
                frame - _survivalProxyPolicyProbeFrame >= SurvivalProxyProbeIntervalFrames)
            {
                _lastObservedSurvivalProxyPressure01 = ReadSurvivalProxyPressurePolicy(out forcedLowMemory);
                _lastObservedForcedLowMemory = forcedLowMemory;
                _survivalProxyPolicyProbeFrame = frame;
                return _lastObservedSurvivalProxyPressure01;
            }

            forcedLowMemory = _lastObservedForcedLowMemory;
            return _lastObservedSurvivalProxyPressure01;
        }

        private bool TryObserveCriticalMemoryPressure(int frame)
        {
            ReadOnlySpan<MemoryPressureSignal> signals = SignalBus<MemoryPressureSignal>.GetFrameSnapshot();
            if (_memoryPressureSnapshotFrame != frame)
            {
                _memoryPressureSnapshotFrame = frame;
                _memoryPressureSnapshotCursor = 0;
            }

            while (_memoryPressureSnapshotCursor < signals.Length)
            {
                MemoryPressureSignal signal = signals[_memoryPressureSnapshotCursor++];
                if (signal.Severity >= CriticalMemoryPressureSeverity)
                    return true;
            }

            return false;
        }

        private static float ReadSurvivalProxyPressurePolicy(out bool forcedLowMemory)
        {
            float qualityWeight01 = ResolveGlobalQualityWeight01();
            float survivalPressure01 = 1.0f - SmoothStep01(qualityWeight01);
            float homeostasisPressure01 = ResolveHomeostasisPressure01();
            float pressure01 = math.max(survivalPressure01, homeostasisPressure01);
            forcedLowMemory = pressure01 >= ForcedMemoryPressureThreshold01;
            return forcedLowMemory ? 1f : pressure01;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(qualityWeight) ? math.saturate(qualityWeight) : 1.0f;
        }

        private static float ResolveHomeostasisPressure01()
        {
            float pressure = HomeostasisBrain.SystemHealthIndex01;
            return math.isfinite(pressure) ? math.saturate(pressure) : 0.0f;
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3.0f - (2.0f * t));
        }

        private async Awaitable RunAutoSequenceAsync(IPrologueSequenceService service, CancellationTokenSource source)
        {
            try
            {
                await service.RunPrologueSequenceAsync(source.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(CancellationFaultHash, SourceHash, 1f);
            }
            finally
            {
                if (ReferenceEquals(_runCancellationSource, source))
                {
                    _runCancellationSource = null;
                    source.Dispose();
                }
            }
        }

        private async Awaitable DelayDilatedInterruptibleAsync(float seconds, CancellationToken cancellationToken)
        {
            if (!math.isfinite(seconds) || seconds <= 0f)
                return;

            double remainingSeconds = seconds;
            ITickDispatcher dispatcher = _tickDispatcher;
            while (remainingSeconds > 0d)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RefreshFrameState();
                if (ShouldSkipPrologue)
                {
                    RequestRunCancellation(PrologueCancelReasons.DevSkip);
                    throw new OperationCanceledException(cancellationToken);
                }

                await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
                dispatcher = _tickDispatcher;

                H8TimeSnapshot snapshot = dispatcher != null
                    ? dispatcher.TimeSnapshot
                    : new H8TimeSnapshot(0d, SystemDispatcher.CurrentFrameDeltaTime, 0d, SystemDispatcher.CurrentFrameUnscaledDeltaTime);
                double deltaTime = snapshot.DeltaTime;
                if (deltaTime > 0d && math.isfinite(deltaTime))
                    remainingSeconds -= deltaTime;
            }
        }

        private void ResetTransientSequenceState()
        {
            _skipRequested = false;
            _skipHoldSeconds = 0d;
            _skipHoldFrameIndex = -1;
            _observedHighResSurfaceReady = false;
            _observedProxySurfaceReady = false;
            _atmosphereSnapshotFrame = -1;
            _completeSnapshotFrame = -1;
            _residencySnapshotFrame = -1;
            _atmosphereSnapshotCursor = 0;
            _completeSnapshotCursor = 0;
            _residencySnapshotCursor = 0;
            ClearStandaloneWhiteoutFallback();
            ResetSurvivalProxyCache();
        }

        private void RequestRunCancellation(byte reason)
        {
            try
            {
                _service?.CancelSequence(reason);
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(CancellationFaultHash, SourceHash, 1f);
            }

            CancellationTokenSource source = _runCancellationSource;
            CancelRunSourceNoThrow(source);
        }

        private void CancelRunSourceNoThrow(CancellationTokenSource source)
        {
            if (source == null || source.IsCancellationRequested)
                return;

            try
            {
                source.Cancel();
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(CancellationFaultHash, SourceHash, 1f);
            }
        }

        private void DisposeRunCancellationSource()
        {
            CancellationTokenSource source = _runCancellationSource;
            if (source == null)
                return;

            _runCancellationSource = null;
            CancelRunSourceNoThrow(source);
            source.Dispose();
        }
    }
}
