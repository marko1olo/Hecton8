using System;
using System.Threading;
using Hecton8.Core.Contracts;
using Hecton8.Core.Signals;
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
        private const uint SourceHash = 0x50524C47u; // PRLG
        private const uint MissingServiceHash = 0x50524D49u; // PRMI
        private const uint MuffledBreathingHash = 0x4D425254u; // MBRT
        private const uint HullTempCriticalHash = 0x4854454Du; // HTEM
        private const uint ManualReleaseHash = 0x4D52454Cu; // MREL
        private const uint ManualReleaseContextHash = 0x434F434Bu; // COCK
        private const uint ShallowWaterChunkHash = 0x53484C57u; // SHLW
        private const float MassiveImpactSeverity = 1f;

        [SerializeField] private MonoBehaviour sequenceComponent;
        [SerializeField] private bool autoRunOnEnable = true;
        [SerializeField] private long oceanSurfaceChunkId;
        [SerializeField] private bool allowAnyHydratedChunkFallback = true;

        private IPrologueSequenceService _service;
        private IInputService _inputService;
        private CancellationTokenSource _runCancellationSource;
        private bool _registeredService;
        private bool _registeredHotSwap;
        private bool _inputSubscribed;
        private bool _skipRequested;
        private bool _observedHighResSurfaceReady;
        private bool _observedProxySurfaceReady;
        private int _atmosphereSnapshotFrame = -1;
        private int _completeSnapshotFrame = -1;
        private int _residencySnapshotFrame = -1;
        private int _atmosphereSnapshotCursor;
        private int _completeSnapshotCursor;
        private int _residencySnapshotCursor;
        private ushort _sequence;

        public bool IsDevelopmentBuild => GlobalRegistry.IsDevelopmentBuild;

        public bool IsLowTier
        {
            get
            {
                HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
                return GlobalRegistry.H8_LOW_MEMORY_PROFILE ||
                       tier == HectonQualityTier.Unknown ||
                       tier == HectonQualityTier.Low ||
                       tier == HectonQualityTier.Mx350;
            }
        }

        public uint CurrentFrame => unchecked((uint)math.max(0, Time.frameCount));

        public bool ShouldSkipPrologue
        {
            get
            {
                if (!IsDevelopmentBuild)
                    return false;

                if (_skipRequested)
                    return true;

                IInputService input = _inputService;
                if (input == null || !input.IsInitialized)
                    input = GlobalRegistry.Input;

                if (input == null || !input.IsInitialized)
                    return false;

                PlayerInputState state = input.GetState();
                return state.HasAction(PlayerInputAction.Dash) &&
                       state.HasAction(PlayerInputAction.PrimaryFire) &&
                       state.HasAction(PlayerInputAction.SecondaryFire);
            }
        }

        private void OnEnable()
        {
            ResolveService();
            BindInputIfAvailable();
            RegisterHotSwap();

            if (_service == null)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(MissingServiceHash, SourceHash, 1f);
                return;
            }

            _service.Configure(this);
            GlobalRegistry.RegisterPrologueSequenceRuntime(_service);
            _registeredService = ReferenceEquals(GlobalRegistry.PrologueSequence, _service);

            if (autoRunOnEnable && Application.isPlaying)
            {
                CancellationToken runToken = destroyCancellationToken;
                if (IsDevelopmentBuild)
                {
                    DisposeRunCancellationSource();
                    // COLD ALLOC: CancellationTokenSource[1] - dev skip cancellation bridge for auto-run Awaitable - owner: PrologueSequenceRegistryBridge
                    _runCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
                    runToken = _runCancellationSource.Token;
                }

                _ = _service.RunPrologueSequenceAsync(runToken);
            }
        }

        private void OnDisable()
        {
            RequestRunCancellation(PrologueCancelReasons.ExplicitCancel);

            if (_registeredService && _service != null)
            {
                GlobalRegistry.UnregisterPrologueSequenceRuntime(_service);
                _registeredService = false;
            }

            UnbindInput();
            UnregisterHotSwap();
            _skipRequested = false;
        }

        private void OnDestroy()
        {
            DisposeRunCancellationSource();
        }

        public bool TryGetOrbitalSnapshot(out PrologueOrbitalSnapshot snapshot)
        {
            IOrbitalDirector orbital = GlobalRegistry.OrbitalDirector;
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
            int frame = Time.frameCount;
            if (_atmosphereSnapshotFrame != frame)
            {
                _atmosphereSnapshotFrame = frame;
                _atmosphereSnapshotCursor = 0;
            }

            if (_atmosphereSnapshotCursor < signals.Length)
            {
                AtmosphericReentrySignal signal = signals[_atmosphereSnapshotCursor++];
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
            int frame = Time.frameCount;
            if (_completeSnapshotFrame != frame)
            {
                _completeSnapshotFrame = frame;
                _completeSnapshotCursor = 0;
            }

            if (_completeSnapshotCursor < signals.Length)
            {
                PrologueCompleteSignal signal = signals[_completeSnapshotCursor++];
                snapshot = new PrologueCompleteSnapshot(
                    signal.Frame,
                    signal.WhiteoutHoldSeconds,
                    signal.Sequence,
                    signal.Phase,
                    signal.Flags);
                return true;
            }

            snapshot = default;
            return false;
        }

        public bool IsOceanSurfaceReady(bool allowProxy)
        {
            if (_observedHighResSurfaceReady || (allowProxy && _observedProxySurfaceReady))
                return true;

            IStreamingBackpressureService streaming = GlobalRegistry.StreamingBackpressure;
            if (oceanSurfaceChunkId != 0 && streaming != null && streaming.IsChunkResident(oceanSurfaceChunkId))
            {
                _observedHighResSurfaceReady = true;
                return true;
            }

            if (allowProxy && streaming != null && streaming.ActiveImpostorCount > 0)
            {
                _observedProxySurfaceReady = true;
                return true;
            }

            ReadOnlySpan<SectorResidencyHydratedSignal> signals = SignalBus<SectorResidencyHydratedSignal>.GetFrameSnapshot();
            int frame = Time.frameCount;
            if (_residencySnapshotFrame != frame)
            {
                _residencySnapshotFrame = frame;
                _residencySnapshotCursor = 0;
            }

            while (_residencySnapshotCursor < signals.Length)
            {
                SectorResidencyHydratedSignal signal = signals[_residencySnapshotCursor++];
                if (!MatchesOceanChunk(signal.ChunkId))
                    continue;

                bool proxy = (signal.Flags & SectorResidencyHydratedSignal.FlagProxyFallback) != 0;
                if (proxy)
                    _observedProxySurfaceReady = true;
                else
                    _observedHighResSurfaceReady = true;

                if (!proxy || allowProxy)
                    return true;
            }

            return false;
        }

        public Awaitable DelayDilatedAsync(float seconds, CancellationToken cancellationToken)
        {
            if (!IsDevelopmentBuild)
                return AwaitableExtension.DelayDilated(seconds, cancellationToken);

            return DelayDilatedDevelopmentInterruptibleAsync(seconds, cancellationToken);
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
            GlobalSignals.Publish(in signal);
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
            GlobalSignals.Publish(in mixer);

            AcousticPingSignal ping = default;
            ping.PositionAup = Hecton8.World.AbsoluteUniversePosition.FromRuntimePosition(Vector3.zero);
            ping.RadiusMeters = math.max(8f, durationSeconds * 12f);
            ping.Intensity01 = safeIntensity;
            ping.SourceId = MuffledBreathingHash;
            ping.Channel = AcousticPingSignal.ChannelFabricScrape;
            ping.Flags = AcousticPingSignal.FlagFabricScrape;
            GlobalSignals.Publish(in ping);
        }

        public void PublishHullTempCriticalWarning(float severity01)
        {
            VocalWarningSignal signal = default;
            signal.WarningHash = HullTempCriticalHash;
            signal.SourceId = SourceHash;
            signal.Severity01 = math.saturate(severity01);
            signal.CooldownSeconds = 2f;
            signal.Priority = 1;
            GlobalSignals.Publish(in signal);
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
            GlobalSignals.Publish(in signal);
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
            GlobalSignals.Publish(in diegetic);

            HUDNotificationSignal hud = default;
            hud.MessageHash = ManualReleaseHash;
            hud.ContextHash = ManualReleaseContextHash;
            hud.SourceId = SourceHash;
            hud.Frame = CurrentFrame;
            hud.Severity = 2;
            hud.Flags = 1;
            GlobalSignals.Publish(in hud);
        }

        public void PublishMassiveImpact()
        {
            CameraJuiceSignals.PublishImpact(MassiveImpactSeverity, Vector3.zero, Vector3.down);
        }

        public void PublishOceanHandoff()
        {
            PrologueCompleteSignal signal = default;
            signal.CapsuleAup = Hecton8.World.AbsoluteUniversePosition.FromRuntimePosition(Vector3.zero);
            signal.Frame = CurrentFrame;
            signal.SourceHash = SourceHash;
            signal.Sequence = unchecked(++_sequence);
            signal.WhiteoutHoldSeconds = 0.12f;
            signal.Flags = PrologueCompleteSignal.FlagForceWhiteout;
            signal.Phase = PrologueCompleteSignal.PhaseOceanHandoff;
            GlobalSignals.Publish(in signal);
        }

        public void ZeroUniverseVelocity()
        {
            IOrbitalDirector orbital = GlobalRegistry.OrbitalDirector;
            orbital?.ForceZeroUniverseVelocity(0);
        }

        public void ForceShallowWaterHydration()
        {
            _observedHighResSurfaceReady = true;
            _observedProxySurfaceReady = true;

            SectorResidencyHydratedSignal signal = default;
            signal.CenterAup = Hecton8.World.AbsoluteUniversePosition.FromRuntimePosition(Vector3.zero);
            signal.ChunkId = oceanSurfaceChunkId != 0 ? oceanSurfaceChunkId : ShallowWaterChunkHash;
            signal.Frame = CurrentFrame;
            signal.RadiusMetersQ = 64;
            signal.Flags = SectorResidencyHydratedSignal.FlagPinned | SectorResidencyHydratedSignal.FlagProxyFallback;
            signal.ResidencyState = 1;
            SignalBus<SectorResidencyHydratedSignal>.Push(in signal);
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
            if (serviceSlot != GlobalRegistryServiceSlot.Input)
                return;

            UnbindInput();
            BindInputIfAvailable();
        }

        private void ResolveService()
        {
            if (sequenceComponent is IPrologueSequenceService serializedService)
            {
                _service = serializedService;
                return;
            }

            MonoBehaviour[] components = GetComponents<MonoBehaviour>(); // COLD ALLOC: MonoBehaviour[] - one-shot interface discovery for inspector-free bridge setup - owner: PrologueSequenceRegistryBridge
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is IPrologueSequenceService discovered)
                {
                    sequenceComponent = components[i];
                    _service = discovered;
                    return;
                }
            }
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

        private void BindInputIfAvailable()
        {
            if (_inputSubscribed)
                return;

            IInputService input = GlobalRegistry.Input;
            if (input == null || !input.IsInitialized)
                return;

            _inputService = input;
            _inputService.OnCancel += HandleSkipRequested;
            _inputSubscribed = true;
        }

        private void UnbindInput()
        {
            if (!_inputSubscribed || _inputService == null)
                return;

            _inputService.OnCancel -= HandleSkipRequested;
            _inputService = null;
            _inputSubscribed = false;
        }

        private void HandleSkipRequested()
        {
            if (!IsDevelopmentBuild)
                return;

            _skipRequested = true;
            RequestRunCancellation(PrologueCancelReasons.DevSkip);
        }

        private bool MatchesOceanChunk(long chunkId)
        {
            if (oceanSurfaceChunkId != 0)
                return chunkId == oceanSurfaceChunkId;

            return allowAnyHydratedChunkFallback || chunkId == ShallowWaterChunkHash;
        }

        private async Awaitable DelayDilatedDevelopmentInterruptibleAsync(float seconds, CancellationToken cancellationToken)
        {
            if (!math.isfinite(seconds) || seconds <= 0f)
                return;

            double remainingSeconds = seconds;
            ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;
            while (remainingSeconds > 0d)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ShouldSkipPrologue)
                {
                    RequestRunCancellation(PrologueCancelReasons.DevSkip);
                    throw new OperationCanceledException(cancellationToken);
                }

                await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);

                if (dispatcher == null)
                    dispatcher = GlobalRegistry.TickDispatcher;

                H8TimeSnapshot snapshot = dispatcher != null
                    ? dispatcher.TimeSnapshot
                    : new H8TimeSnapshot(0d, SystemDispatcher.CurrentFrameDeltaTime, 0d, SystemDispatcher.CurrentFrameUnscaledDeltaTime);
                double deltaTime = snapshot.DeltaTime;
                if (deltaTime > 0d && math.isfinite(deltaTime))
                    remainingSeconds -= deltaTime;
            }
        }

        private void RequestRunCancellation(byte reason)
        {
            if (_service != null)
                _service.CancelSequence(reason);

            CancellationTokenSource source = _runCancellationSource;
            if (source == null || source.IsCancellationRequested)
                return;

            source.Cancel();
        }

        private void DisposeRunCancellationSource()
        {
            if (_runCancellationSource == null)
                return;

            _runCancellationSource.Dispose();
            _runCancellationSource = null;
        }
    }
}
